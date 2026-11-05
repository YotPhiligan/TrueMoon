using System.Security;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceDirectoryCacheCensusTests
{
    [Fact]
    public async Task Scan_FinalCachedAncestorReparseNeverPublishesSameAndDisposesEveryProduct()
    {
        using var files = new SourceWatchFiles(); var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var census = Create(files, TimeProvider.System, attempts, streams, "reparse");
        var result = await census.ScanAsync(files.Descriptor(), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.False(result.Recoverable);
        Assert.Contains("reparse", result.Reason);
        var attempt = Assert.Single(attempts); Assert.True(attempt.Finishing); Assert.Equal(1, attempt.FinalFailures);
        Assert.Equal(2, streams.Count); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals));
        AssertDrained(attempts);
    }

    [Theory]
    [InlineData("missing")] [InlineData("file")] [InlineData("denied")]
    [InlineData("io")] [InlineData("security")]
    public async Task Scan_FinalValidationCoverageFailuresRetryFreshCachesThenRemainUnavailable(string kind)
    {
        using var files = new SourceWatchFiles(); var clock = new SourceWatchClock();
        var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var census = Create(files, clock, attempts, streams, kind);
        var result = await FinishRetries(clock, census.ScanAsync(files.Descriptor(), default));
        Assert.False(result.Available); Assert.False(result.Changed); Assert.True(result.Recoverable);
        Assert.Contains("coverage failed", result.Reason);
        Assert.Equal(4, attempts.Count); Assert.All(attempts, attempt => Assert.Equal(1, attempt.FinalFailures));
        Assert.Equal(8, streams.Count); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals));
        Assert.Equal(4, attempts.Select(attempt => attempt.Cache).Distinct().Count());
        AssertDrained(attempts);
    }

    [Fact]
    public async Task Scan_TransientFinalValidationFailureRecoversOnlyAfterPreviousAttemptPinsDrained()
    {
        using var files = new SourceWatchFiles(); var clock = new SourceWatchClock();
        var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var census = Create(files, clock, attempts, streams, "denied", onlyFirst: true);
        var result = await FinishRetries(clock, census.ScanAsync(files.Descriptor(), default));
        AssertSame(result); Assert.Equal(2, attempts.Count);
        Assert.Equal(1, attempts[0].FinalFailures); Assert.Equal(0, attempts[1].FinalFailures);
        Assert.Equal(4, streams.Count); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals));
        AssertDrained(attempts);
    }

    [Fact]
    public async Task Scan_CompletedCensusCannotReuseDirectoryApprovalOnAnotherCall()
    {
        using var files = new SourceWatchFiles(); var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var rejectNext = false;
        var census = new EditorSourceCensus(TimeProvider.System, path =>
        {
            var stream = new HookStream(Open(path), () => { }); streams.Add(stream); return stream;
        }, token =>
        {
            AssertDrained(attempts);
            var attempt = new Attempt(token, path => rejectNext && path == files.Tree ? FileAttributes.Directory | FileAttributes.ReparsePoint : ReadAttributes(path));
            attempts.Add(attempt); return attempt.Cache;
        });
        var descriptor = files.Descriptor();
        AssertSame(await census.ScanAsync(descriptor, default)); Assert.Equal(2, streams.Count); AssertDrained(attempts);
        rejectNext = true;
        var rejected = await census.ScanAsync(descriptor, default);
        Assert.False(rejected.Available); Assert.False(rejected.Changed); Assert.False(rejected.Recoverable); Assert.Contains("reparse", rejected.Reason);
        Assert.Equal(2, attempts.Count); Assert.Equal(2, streams.Count); AssertDrained(attempts);
    }

    [Fact]
    public async Task Scan_CancelledFileReadReleasesCurrentCacheAndEveryStreamBeforeCompletion()
    {
        using var files = new SourceWatchFiles(); using var cancellation = new CancellationTokenSource();
        var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var census = new EditorSourceCensus(TimeProvider.System, path =>
        {
            var stream = new HookStream(Open(path), () => { if (path == files.Source) cancellation.Cancel(); });
            streams.Add(stream); return stream;
        }, token => { var attempt = new Attempt(token, ReadAttributes); attempts.Add(attempt); return attempt.Cache; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => census.ScanAsync(files.Descriptor(), cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested); Assert.Single(attempts);
        Assert.Equal(2, streams.Count); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals)); AssertDrained(attempts);
    }

    [Fact]
    public async Task Scan_ProvenContentDifferenceReleasesCacheBeforeReturningChanged()
    {
        using var files = new SourceWatchFiles(); var descriptor = files.Descriptor();
        File.WriteAllText(files.Source, "modified");
        var attempts = new List<Attempt>(); var streams = new List<HookStream>();
        var census = new EditorSourceCensus(TimeProvider.System, path =>
        {
            var stream = new HookStream(Open(path), () => { }); streams.Add(stream); return stream;
        }, token => { var attempt = new Attempt(token, ReadAttributes); attempts.Add(attempt); return attempt.Cache; });
        var result = await census.ScanAsync(descriptor, default);
        Assert.True(result.Available); Assert.True(result.Changed); Assert.Equal(new[] { files.Source }, result.ChangedPaths);
        Assert.Single(attempts); Assert.Equal(2, streams.Count); Assert.All(streams, stream => Assert.Equal(1, stream.Disposals)); AssertDrained(attempts);
    }

    [Fact]
    public async Task Scan_DefaultNativePinsFenceRenameDuringReadAndAreGoneAfterSame()
    {
        using var files = new SourceWatchFiles(); var moved = Path.Combine(files.Root, "moved-game"); var protectedRead = false;
        var tree = files.Tree; var source = files.Source;
        var census = new EditorSourceCensus(TimeProvider.System, path => new HookStream(Open(path), () =>
        {
            if (path != source) return;
            Assert.ThrowsAny<IOException>(() => Directory.Move(tree, moved));
            protectedRead = true;
        }));
        AssertSame(await census.ScanAsync(files.Descriptor(), default)); Assert.True(protectedRead);
        Directory.Move(tree, moved); Assert.True(Directory.Exists(moved)); Assert.False(Directory.Exists(tree));
        Directory.Move(moved, tree); Assert.True(File.Exists(source));
    }

    private static EditorSourceCensus Create(SourceWatchFiles files, TimeProvider time, List<Attempt> attempts, List<HookStream> streams, string failure, bool onlyFirst = false)
    {
        Attempt? current = null;
        return new(time, path =>
        {
            var stream = new HookStream(Open(path), () => { if (path == files.Source) current!.Finishing = true; });
            streams.Add(stream); return stream;
        }, token =>
        {
            AssertDrained(attempts);
            var number = attempts.Count;
            Attempt? attempt = null;
            attempt = new(token, path =>
            {
                if (path != files.Tree || !attempt!.Finishing || onlyFirst && number > 0) return ReadAttributes(path);
                attempt.FinalFailures++;
                return failure switch
                {
                    "missing" => null, "file" => FileAttributes.Normal, "reparse" => FileAttributes.Directory | FileAttributes.ReparsePoint,
                    "denied" => throw new UnauthorizedAccessException("new ACL after reuse"),
                    "io" => throw new IOException("final path coverage"), "security" => throw new SecurityException("final access coverage"),
                    _ => throw new ArgumentOutOfRangeException(nameof(failure))
                };
            });
            attempts.Add(attempt); current = attempt; return attempt.Cache;
        });
    }
    private static FileAttributes? ReadAttributes(string path)
    { try { return File.GetAttributes(path); } catch (FileNotFoundException) { return null; } catch (DirectoryNotFoundException) { return null; } }
    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private static void AssertDrained(IEnumerable<Attempt> attempts)
    { foreach (var attempt in attempts) { Assert.Equal(0, attempt.Cache.Count); Assert.NotEmpty(attempt.Pins); Assert.All(attempt.Pins, pin => Assert.Equal(1, pin.Disposals)); } }
    private static void AssertSame(EditorSourceScan result)
    { Assert.True(result.Available, result.Reason); Assert.False(result.Changed); Assert.Null(result.Reason); Assert.Empty(result.ChangedPaths); }
    private static async Task<T> FinishRetries<T>(SourceWatchClock clock, Task<T> operation)
    {
        for (var step = 0; step < 8; step++)
        {
            await SourceWatchFixture.Eventually(() => operation.IsCompleted || clock.NextDue != null);
            if (operation.IsCompleted) return await operation;
            if (clock.NextDue is { } due) clock.Advance(TimeSpan.FromTicks(Math.Max(0, due - clock.GetTimestamp())));
        }
        return await operation.WaitAsync(TimeSpan.FromSeconds(10));
    }
    private sealed class Attempt
    {
        internal Attempt(CancellationToken token, Func<string, FileAttributes?> attributes) => Cache = new(token,
            _ => { var pin = new Pin(); Pins.Add(pin); return pin; }, attributes);
        internal EditorSourceDirectoryCache Cache { get; }
        internal List<Pin> Pins { get; } = [];
        internal bool Finishing { get; set; }
        internal int FinalFailures { get; set; }
    }
    private sealed class Pin : IDisposable
    { internal int Disposals { get; private set; } public void Dispose() => Disposals++; }
    private sealed class HookStream(Stream inner, Action onRead) : Stream
    {
        private int _read, _disposed;
        internal int Disposals { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { Begin(); return inner.Read(buffer, offset, count); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { Begin(); return inner.ReadAsync(buffer, cancellationToken); }
        private void Begin() { if (Interlocked.Exchange(ref _read, 1) == 0) onRead(); }
        protected override void Dispose(bool disposing)
        { if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) { Disposals++; inner.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync()
        { if (Interlocked.Exchange(ref _disposed, 1) == 0) { Disposals++; await inner.DisposeAsync(); } }
    }
}
