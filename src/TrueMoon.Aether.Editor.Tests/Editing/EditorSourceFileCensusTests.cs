using System.Security;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceFileCensusTests
{
    [Fact]
    public async Task Scan_DefaultNativeReaderUsesOpenedMetadataAndFreshlyChecksKnownAndAbsentProbes()
    {
        using var files = new SourceWatchFiles(); var absent = Path.Combine(files.Root, "optional.props");
        var calls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var descriptor = files.Descriptor(probes: [new(files.Source, true), new(absent, false)]);
        var census = new EditorSourceCensus(TimeProvider.System, metadata: path =>
        {
            calls[path] = calls.GetValueOrDefault(path) + 1;
            return EditorSourceFile.ReadMetadata(path);
        });
        AssertSame(await census.ScanAsync(descriptor, default));
        Assert.Equal(3, calls.Count); Assert.Equal(1, calls[files.Project.GameProjectPath]);
        Assert.Equal(2, calls[files.Source]); Assert.Equal(1, calls[absent]);
        calls.Clear(); File.WriteAllText(absent, "new optional input");
        var changed = await census.ScanAsync(descriptor, default);
        Assert.True(changed.Available); Assert.True(changed.Changed); Assert.Equal(new[] { absent }, changed.ChangedPaths);
        Assert.Equal(3, calls.Count); Assert.Equal(1, calls[files.Project.GameProjectPath]);
        Assert.Equal(2, calls[files.Source]); Assert.Equal(1, calls[absent]);
        using var exclusive = new FileStream(files.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(8, exclusive.Length);
    }

    [Fact]
    public async Task Scan_InjectedReaderRetainsBeforeAndAfterChecksAndReleasesAllOwnersBeforeSame()
    {
        using var files = new SourceWatchFiles(); var owners = new Owners();
        var calls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var census = new EditorSourceCensus(TimeProvider.System, owners.Open, owners.CreateCache, path =>
        { calls[path] = calls.GetValueOrDefault(path) + 1; return EditorSourceFile.ReadMetadata(path); });
        AssertSame(await census.ScanAsync(files.Descriptor(), default));
        Assert.Equal(2, calls.Count); Assert.Equal(2, calls[files.Project.GameProjectPath]); Assert.Equal(2, calls[files.Source]);
        Assert.Single(owners.Caches); Assert.Equal(2, owners.Streams.Count); owners.AssertDrained();
    }

    [Theory]
    [InlineData("missing")] [InlineData("directory")] [InlineData("length")] [InlineData("timestamp")]
    [InlineData("denied")] [InlineData("io")] [InlineData("security")] [InlineData("reparse")]
    public async Task Scan_PostReadMetadataInstabilityCannotPublishSameAndEveryAttemptDrains(string failure)
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var clock = new SourceWatchClock();
        var readSource = false; var finalQueries = 0;
        owners.OnAttempt = () => readSource = false;
        owners.OnRead = path => { if (path == files.Source) readSource = true; };
        var census = new EditorSourceCensus(clock, owners.Open, owners.CreateCache, path =>
        {
            var snapshot = EditorSourceFile.ReadMetadata(path);
            if (path != files.Source || !readSource) return snapshot;
            finalQueries++; return Failure(failure, snapshot!.Value);
        });
        var result = await FinishRetries(clock, census.ScanAsync(files.Descriptor(), default));
        var permanent = failure == "reparse"; var attempts = permanent ? 1 : 4;
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Equal(!permanent, result.Recoverable);
        Assert.Contains(permanent ? "reparse" : "coverage failed", result.Reason);
        Assert.Equal(attempts, finalQueries); Assert.Equal(attempts, owners.Caches.Count);
        Assert.Equal(attempts * 2, owners.Streams.Count); owners.AssertDrained();
    }

    [Fact]
    public async Task Scan_TransientPostReadDeniedRecoversOnlyWithFreshMetadataAndDrainedPriorAttempt()
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var clock = new SourceWatchClock();
        var readSource = false; var failures = 0;
        owners.OnAttempt = () => readSource = false;
        owners.OnRead = path => { if (path == files.Source) readSource = true; };
        var census = new EditorSourceCensus(clock, owners.Open, owners.CreateCache, path =>
        {
            if (path == files.Source && readSource && owners.Caches.Count == 1)
            { failures++; throw new UnauthorizedAccessException("transient post-read coverage"); }
            return EditorSourceFile.ReadMetadata(path);
        });
        AssertSame(await FinishRetries(clock, census.ScanAsync(files.Descriptor(), default)));
        Assert.Equal(1, failures); Assert.Equal(2, owners.Caches.Count); Assert.Equal(4, owners.Streams.Count); owners.AssertDrained();
    }

    [Theory]
    [InlineData("missing")] [InlineData("directory")] [InlineData("reparse")] [InlineData("denied")]
    public async Task Scan_BeforeReadMetadataPreservesDifferenceUnsupportedAndDeniedPartitions(string failure)
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var clock = new SourceWatchClock(); var rejected = 0;
        var census = new EditorSourceCensus(clock, owners.Open, owners.CreateCache, path =>
        {
            var snapshot = EditorSourceFile.ReadMetadata(path);
            if (path != files.Source) return snapshot;
            rejected++; return Failure(failure, snapshot!.Value);
        });
        var result = await FinishRetries(clock, census.ScanAsync(files.Descriptor(), default));
        var changed = failure is "missing" or "directory"; var attempts = failure == "denied" ? 4 : 1;
        Assert.Equal(changed, result.Available); Assert.Equal(changed, result.Changed);
        if (changed) Assert.Equal(new[] { files.Source }, result.ChangedPaths);
        else { Assert.Equal(failure == "denied", result.Recoverable); Assert.Contains(failure == "denied" ? "coverage failed" : "reparse", result.Reason); }
        Assert.Equal(attempts, rejected); Assert.Equal(attempts, owners.Streams.Count); Assert.Equal(attempts, owners.Caches.Count);
        Assert.All(owners.Streams, stream => Assert.Equal(files.Project.GameProjectPath, stream.Path)); owners.AssertDrained();
    }

    [Theory]
    [InlineData("directory")] [InlineData("reparse")] [InlineData("denied")] [InlineData("io")] [InlineData("security")]
    public async Task Scan_ProbeMetadataErrorsNeverBecomeExpectedAbsence(string failure)
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var clock = new SourceWatchClock();
        var probe = Path.Combine(files.Root, "optional.props"); var queries = 0;
        var census = new EditorSourceCensus(clock, owners.Open, owners.CreateCache, path =>
        {
            if (path != probe) return EditorSourceFile.ReadMetadata(path);
            queries++; return Failure(failure, new(FileAttributes.Normal, 0, 0));
        });
        var result = await FinishRetries(clock, census.ScanAsync(files.Descriptor(probes: [new(probe, false)]), default));
        var permanent = failure is "directory" or "reparse"; var attempts = permanent ? 1 : 4;
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Equal(!permanent, result.Recoverable);
        Assert.Contains(permanent ? failure : "coverage failed", result.Reason);
        Assert.Equal(attempts, queries); Assert.Equal(attempts, owners.Caches.Count); Assert.Equal(attempts * 2, owners.Streams.Count);
        owners.AssertDrained();
    }

    [Fact]
    public async Task Scan_PresentKnownFileProbeIsRecheckedAfterTheReadInsteadOfReusingFileMetadata()
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var queries = 0;
        var census = new EditorSourceCensus(TimeProvider.System, owners.Open, owners.CreateCache, path =>
        {
            if (path == files.Source && ++queries == 3) return null;
            return EditorSourceFile.ReadMetadata(path);
        });
        var result = await census.ScanAsync(files.Descriptor(probes: [new(files.Source, true)]), default);
        Assert.True(result.Available); Assert.True(result.Changed); Assert.Equal(new[] { files.Source }, result.ChangedPaths);
        Assert.Equal(3, queries); Assert.Single(owners.Caches); Assert.Equal(2, owners.Streams.Count); owners.AssertDrained();
    }

    [Theory]
    [InlineData("read")] [InlineData("final-metadata")]
    public async Task Scan_CancellationDuringReadOrFinalMetadataDrainsWithoutPublishingAResult(string stage)
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); using var cancellation = new CancellationTokenSource();
        var readSource = false; var finalQueries = 0;
        owners.OnRead = path => { if (path == files.Source) { readSource = true; if (stage == "read") cancellation.Cancel(); } };
        var census = new EditorSourceCensus(TimeProvider.System, owners.Open, owners.CreateCache, path =>
        {
            if (path == files.Source && readSource) { finalQueries++; if (stage == "final-metadata") cancellation.Cancel(); }
            return EditorSourceFile.ReadMetadata(path);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => census.ScanAsync(files.Descriptor(), cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested); Assert.Equal(stage == "read" ? 0 : 1, finalQueries);
        Assert.Single(owners.Caches); Assert.Equal(2, owners.Streams.Count); owners.AssertDrained();
    }

    [Fact]
    public async Task Scan_OversizedBeforeMetadataRejectsWithoutOpeningTheRejectedFile()
    {
        using var files = new SourceWatchFiles(); var owners = new Owners(); var sourceQueries = 0;
        var census = new EditorSourceCensus(TimeProvider.System, owners.Open, owners.CreateCache, path =>
        {
            var value = EditorSourceFile.ReadMetadata(path);
            if (path != files.Source) return value;
            sourceQueries++; return value!.Value with { Length = GameBuildSourceWatchPolicy.MaximumFileBytes + 1 };
        });
        var result = await census.ScanAsync(files.Descriptor(), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
        Assert.Equal(1, sourceQueries); Assert.Single(owners.Caches);
        Assert.Equal(files.Project.GameProjectPath, Assert.Single(owners.Streams).Path); owners.AssertDrained();
    }

    private static EditorSourceFile.Snapshot? Failure(string kind, EditorSourceFile.Snapshot value) => kind switch
    {
        "missing" => null, "directory" => value with { Attributes = FileAttributes.Directory },
        "length" => value with { Length = value.Length + 1 }, "timestamp" => value with { Written = value.Written + 1 },
        "reparse" => value with { Attributes = FileAttributes.ReparsePoint },
        "denied" => throw new UnauthorizedAccessException("metadata access denied"),
        "io" => throw new IOException("metadata IO coverage"), "security" => throw new SecurityException("metadata security coverage"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
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
    private sealed class Owners
    {
        internal List<EditorSourceDirectoryCache> Caches { get; } = [];
        internal List<Pin> Pins { get; } = [];
        internal List<OwnedStream> Streams { get; } = [];
        internal Action? OnAttempt { get; set; }
        internal Action<string>? OnRead { get; set; }
        internal EditorSourceDirectoryCache CreateCache(CancellationToken token)
        {
            AssertDrained(); OnAttempt?.Invoke();
            var cache = new EditorSourceDirectoryCache(token, _ => { var pin = new Pin(); Pins.Add(pin); return pin; });
            Caches.Add(cache); return cache;
        }
        internal Stream Open(string path)
        {
            var native = EditorSourceFile.Open(path) ?? throw new FileNotFoundException("Expected test input", path);
            var owned = new OwnedStream(path, native, () => OnRead?.Invoke(path)); Streams.Add(owned); return owned;
        }
        internal void AssertDrained()
        {
            Assert.All(Caches, cache => Assert.Equal(0, cache.Count));
            Assert.All(Pins, pin => Assert.Equal(1, pin.Disposals));
            Assert.All(Streams, stream => Assert.Equal(1, stream.Disposals));
        }
    }
    private sealed class Pin : IDisposable
    { internal int Disposals { get; private set; } public void Dispose() => Disposals++; }
    private sealed class OwnedStream(string path, Stream inner, Action firstRead) : Stream
    {
        private bool _read, _disposed;
        internal string Path { get; } = path;
        internal int Disposals { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { Begin(); return inner.Read(buffer, offset, count); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Begin(); return inner.ReadAsync(buffer, cancellationToken); }
        private void Begin() { if (!_read) { _read = true; firstRead(); } }
        protected override void Dispose(bool disposing)
        { if (disposing && !_disposed) { _disposed = true; inner.Dispose(); Disposals++; } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync()
        { if (!_disposed) { _disposed = true; await inner.DisposeAsync(); Disposals++; } }
    }
}
