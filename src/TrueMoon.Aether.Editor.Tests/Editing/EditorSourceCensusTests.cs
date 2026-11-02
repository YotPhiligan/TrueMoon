using System.Security.Cryptography;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceCensusTests
{
    [Theory]
    [InlineData("content")] [InlineData("create")] [InlineData("rename")] [InlineData("delete")] [InlineData("subtree")]
    [InlineData("linked")] [InlineData("hint")] [InlineData("lock")] [InlineData("archive")]
    public async Task Scan_ByteMembershipAndExplicitInputsChangeWithoutTimestampShortcuts(string change)
    {
        using var files = new SourceWatchFiles();
        var extra = Path.Combine(files.Root, change switch { "hint" => "hint.dll", "archive" => "library.nupkg", "lock" => "packages.lock.json", _ => "linked.cs" });
        File.WriteAllText(extra, "original");
        var descriptor = files.Descriptor([files.Project.GameProjectPath, files.Source, extra]);
        var modified = files.Source;
        switch (change)
        {
            case "content":
                var written = File.GetLastWriteTimeUtc(files.Source); File.WriteAllText(files.Source, "modified"); File.SetLastWriteTimeUtc(files.Source, written); break;
            case "create": modified = Path.Combine(files.Tree, "added.txt"); File.WriteAllText(modified, "non-Compile member"); break;
            case "rename": modified = Path.Combine(files.Tree, "renamed.cs"); File.Move(files.Source, modified); break;
            case "delete": File.Delete(files.Source); break;
            case "subtree":
                var subtree = Path.Combine(files.Tree, "new", "nested"); Directory.CreateDirectory(subtree);
                modified = Path.Combine(subtree, "added.txt"); File.WriteAllText(modified, "new member"); break;
            default: modified = extra; File.WriteAllText(extra, "modified"); break;
        }
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default);
        Assert.True(result.Available); Assert.True(result.Changed);
        Assert.Contains(result.ChangedPaths, path => path == modified || change == "rename" && path == files.Source);
        Assert.InRange(result.ChangedPaths.Count, 1, 16);
    }

    [Theory]
    [InlineData("optional.props")] [InlineData("Directory.Build.props")] [InlineData("Directory.Build.targets")]
    [InlineData(".editorconfig")] [InlineData(".globalconfig")] [InlineData("global.json")]
    [InlineData("MSBuild.rsp")] [InlineData("Directory.Build.rsp")]
    public async Task Scan_ExpectedAbsentImportConfigurationAndResponseCreationIsChanged(string name)
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, name);
        var descriptor = files.Descriptor(probes: [new(path, false)]);
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default));
        File.WriteAllText(path, "new input");
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default);
        Assert.True(result.Available); Assert.True(result.Changed); Assert.Equal(new[] { path }, result.ChangedPaths);
    }

    [Fact]
    public async Task Scan_TouchEmptyExcludedAndOutsideEditsRemainUnchangedButExplicitBinInputIsObserved()
    {
        using var files = new SourceWatchFiles();
        var bin = Path.Combine(files.Tree, "bin"); Directory.CreateDirectory(bin);
        var exact = Path.Combine(bin, "explicit.dll"); File.WriteAllText(exact, "original");
        var descriptor = files.Descriptor([files.Project.GameProjectPath, files.Source, exact]);
        File.SetLastWriteTimeUtc(files.Source, DateTime.UtcNow.AddDays(-1));
        Directory.CreateDirectory(Path.Combine(files.Tree, "empty")); Directory.CreateDirectory(files.Artifacts);
        File.WriteAllText(Path.Combine(files.Artifacts, "owned-output.dll"), "ignored");
        File.WriteAllText(Path.Combine(files.Root, "unrelated.cs"), "ignored");
        foreach (var name in new[] { "bin", "obj", ".git", ".codex", ".agents" })
        { var directory = Path.Combine(files.Tree, name); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "ignored.cs"), "ignored"); }
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default));
        File.WriteAllText(exact, "modified");
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default);
        Assert.True(result.Changed); Assert.Equal(new[] { exact }, result.ChangedPaths);
        using var exclusive = new FileStream(exact, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(8, exclusive.Length); // No observation lease survives a completed pass.
    }

    [Fact]
    public async Task Scan_OccupiedAbsentProbeAndUnrepresentablePathArePermanentUnavailable()
    {
        using var files = new SourceWatchFiles(); var occupied = Path.Combine(files.Root, "optional.props"); Directory.CreateDirectory(occupied);
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(files.Descriptor(probes: [new(occupied, false)]), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.False(result.Recoverable);
        Assert.Contains("directory", result.Reason);
        var network = files.Descriptor(probes: [new(@"\\unavailable-host\share\optional.props", false)]);
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(network, default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.False(result.Recoverable);
        Assert.Contains("local", result.Reason);
    }

    [Fact]
    public async Task Scan_LockedInputRetriesThreeTimesThenReportsUnknownAndHonorsCancellation()
    {
        using var files = new SourceWatchFiles(); var descriptor = files.Descriptor(); var clock = new SourceWatchClock();
        using var held = new FileStream(files.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var scanning = new EditorSourceCensus(clock).ScanAsync(descriptor, default);
        await clock.WaitForDue(TimeSpan.FromMilliseconds(30)); Assert.False(scanning.IsCompleted); clock.Advance(TimeSpan.FromMilliseconds(30));
        await clock.WaitForDue(TimeSpan.FromMilliseconds(90)); Assert.False(scanning.IsCompleted); clock.Advance(TimeSpan.FromMilliseconds(60));
        await clock.WaitForDue(TimeSpan.FromMilliseconds(180)); Assert.False(scanning.IsCompleted); clock.Advance(TimeSpan.FromMilliseconds(90));
        var result = await scanning.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(result.Available); Assert.False(result.Changed); Assert.True(result.Recoverable); Assert.Contains("coverage failed", result.Reason);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorSourceCensus(clock).ScanAsync(descriptor, cancellation.Token));
    }

    [Fact]
    public async Task Scan_UnstableSameBytesAndLengthNeverBecomeSameAndEveryAttemptDisposesStreams()
    {
        using var files = new SourceWatchFiles(); var descriptor = files.Descriptor(); var clock = new SourceWatchClock();
        var opened = 0; var disposed = 0; var changed = 0;
        var census = new EditorSourceCensus(clock, path =>
        {
            Interlocked.Increment(ref opened);
            return new ReadHookStream(OpenShared(path), () =>
            {
                if (path == files.Source)
                { Interlocked.Increment(ref changed); File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(1)); }
            }, () => Interlocked.Increment(ref disposed));
        });
        var result = await FinishRetries(clock, census.ScanAsync(descriptor, default));
        Assert.False(result.Available); Assert.False(result.Changed); Assert.True(result.Recoverable); Assert.Contains("coverage failed", result.Reason);
        Assert.Equal(4, changed); Assert.Equal(8, opened); Assert.Equal(opened, disposed);
        using var exclusive = new FileStream(files.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(8, exclusive.Length);
    }

    [Fact]
    public async Task Scan_WholePassByteBudgetIncludesDiscardedRetryReadsAndNeverResetsPerAttempt()
    {
        using var files = new SourceWatchFiles(); var first = Path.Combine(files.Root, "large0.bin"); var second = Path.Combine(files.Root, "large1.bin");
        Resize(first, GameBuildSourceWatchPolicy.MaximumFileBytes); Resize(second, GameBuildSourceWatchPolicy.MaximumFileBytes);
        var descriptor = new GameBuildSourceWatchDescriptor(files.Project.GameProjectPath, new(files.Artifacts), [Identity(first), Identity(second)], [], [], []);
        var clock = new SourceWatchClock(); var opened = 0; var disposed = 0; var secondReads = 0;
        var census = new EditorSourceCensus(clock, path =>
        {
            Interlocked.Increment(ref opened);
            return new ReadHookStream(OpenShared(path), () =>
            {
                if (path == second && Interlocked.Increment(ref secondReads) <= 3)
                    File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(1));
            }, () => Interlocked.Increment(ref disposed));
        });
        // A reset-per-attempt defect would reach a stable fourth attempt and return Same.
        var result = await FinishRetries(clock, census.ScanAsync(descriptor, default));
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
        Assert.Equal(2, secondReads); Assert.Equal(4, opened); Assert.Equal(opened, disposed);
        using var exclusive = new FileStream(second, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(GameBuildSourceWatchPolicy.MaximumFileBytes, exclusive.Length);
    }

    [Fact]
    public async Task Scan_KnownFileAndProbeLimitsAccept4096AndReject4097()
    {
        using var files = new SourceWatchFiles(); var originals = files.Descriptor().Files.ToList();
        var directory = Path.Combine(files.Root, "known"); Directory.CreateDirectory(directory);
        for (var index = originals.Count; index < GameBuildSourceWatchPolicy.MaximumPaths; index++)
        { var path = Path.Combine(directory, index + ".txt"); File.WriteAllText(path, ""); originals.Add(Identity(path)); }
        var probes = Enumerable.Range(0, GameBuildSourceWatchPolicy.MaximumPaths).Select(index => new GameBuildSourceWatchProbe(Path.Combine(files.Root, "absent", index + ".props"), false)).ToArray();
        var exact = Descriptor(files, originals, [files.Tree, directory], probes, originals.Select(file => file.Path));
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(exact, default));
        var overFiles = Descriptor(files, originals.Append(new(Path.Combine(directory, "extra.txt"), 0, "unused")), probes: probes);
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(overFiles, default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        var laterFiles = Descriptor(files, originals.Concat([new(Path.Combine(directory, "extra.txt"), 0, "unused"), new(Path.Combine(directory, "later.txt"), 0, "unused")]));
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(laterFiles, default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        var overProbes = Descriptor(files, originals, probes: probes.Append(new(Path.Combine(files.Root, "extra.props"), false)));
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(overProbes, default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        var laterProbes = Descriptor(files, originals, probes: probes.Concat([new(Path.Combine(files.Root, "extra.props"), false), new(Path.Combine(files.Root, "later.props"), false)]));
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(laterProbes, default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        foreach (var count in new[] { GameBuildSourceWatchPolicy.MaximumPaths + 1, GameBuildSourceWatchPolicy.MaximumPaths + 2 })
        {
            var excessMembers = Enumerable.Range(0, count).Select(index => Path.Combine(directory, index + ".txt"));
            result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals, members: excessMembers), default);
            Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        }
    }

    [Fact]
    public async Task Scan_TreeLimitAcceptsFourAndRejectsFifth()
    {
        using var files = new SourceWatchFiles(); var originals = files.Descriptor().Files.ToList(); var roots = new List<string> { files.Tree };
        for (var index = 1; index <= 4; index++)
        {
            var tree = Path.Combine(files.Root, "tree" + index); Directory.CreateDirectory(tree); roots.Add(tree);
            var path = Path.Combine(tree, "Other.csproj"); File.WriteAllText(path, "<Project/>"); originals.Add(Identity(path));
        }
        var exact = Descriptor(files, originals.Take(originals.Count - 1), roots.Take(4), members: originals.Take(originals.Count - 1).Select(file => file.Path));
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(exact, default));
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals, roots, members: originals.Select(file => file.Path)), default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals, roots.Append(Path.Combine(files.Root, "sixth")), members: originals.Select(file => file.Path)), default);
        Assert.False(result.Available); Assert.False(result.Recoverable); Assert.Contains("path budget", result.Reason);
    }

    [Fact]
    public async Task Scan_EntryAndDepthLimitsAcceptExactBoundaryAndRejectNextEntryOrLevel()
    {
        using var files = new SourceWatchFiles(); var descriptor = files.Descriptor();
        for (var index = 0; index < GameBuildSourceWatchPolicy.MaximumEntries - descriptor.TreeFiles.Count; index++)
            Directory.CreateDirectory(Path.Combine(files.Tree, "d" + index));
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default));
        var extra = Path.Combine(files.Tree, "entry-over"); Directory.CreateDirectory(extra);
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(descriptor, default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("entry budget", result.Reason);

        using var deep = new SourceWatchFiles(); var path = deep.Tree;
        for (var depth = 0; depth < GameBuildSourceWatchPolicy.MaximumDepth; depth++) { path = Path.Combine(path, "d"); Directory.CreateDirectory(path); }
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(deep.Descriptor(), default));
        Directory.CreateDirectory(Path.Combine(path, "d"));
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(deep.Descriptor(), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("depth budget", result.Reason);
    }

    [Fact]
    public async Task Scan_FileAndTotalByteLimitsAcceptExactBoundaryAndRejectOneMoreByte()
    {
        using var files = new SourceWatchFiles(); var originals = files.Descriptor().Files.ToList();
        var path = Path.Combine(files.Root, "large0.bin"); Resize(path, GameBuildSourceWatchPolicy.MaximumFileBytes);
        originals.Add(Identity(path));
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default));
        Resize(path, GameBuildSourceWatchPolicy.MaximumFileBytes + 1);
        var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
        Resize(path, GameBuildSourceWatchPolicy.MaximumFileBytes + 2);
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
        Resize(path, GameBuildSourceWatchPolicy.MaximumFileBytes); originals[^1] = Identity(path);
        for (var index = 1; index < 4; index++)
        {
            path = Path.Combine(files.Root, "large" + index + ".bin");
            var length = index < 3 ? GameBuildSourceWatchPolicy.MaximumFileBytes : GameBuildSourceWatchPolicy.MaximumTotalBytes - originals.Sum(file => file.Length);
            Resize(path, length); originals.Add(Identity(path));
        }
        Assert.Equal(GameBuildSourceWatchPolicy.MaximumTotalBytes, originals.Sum(file => file.Length));
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default));
        Resize(path, originals[^1].Length + 1);
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
        Resize(path, originals[^1].Length + 2);
        result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(Descriptor(files, originals), default);
        Assert.False(result.Available); Assert.False(result.Changed); Assert.Contains("byte budget", result.Reason);
    }

    [Fact]
    public void Hints_ThirtyTwoHandleLimitFallsBackWithoutWatchingAncestorsOrDroppingTargets()
    {
        using var files = new SourceWatchFiles(); var originals = files.Descriptor().Files.ToList();
        for (var index = 0; index < 32; index++)
        {
            var directory = Path.Combine(files.Root, "hint" + index); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "external.dll"); File.WriteAllText(path, "original"); originals.Add(Identity(path));
        }
        var signals = 0;
        var drive = Path.GetPathRoot(files.Root)!;
        var rootProbes = new[] { "global.json", "Directory.Build.props" }.Select(name =>
        { var path = Path.Combine(drive, name); return new GameBuildSourceWatchProbe(path, File.Exists(path)); });
        using (var exact = new EditorSourceHints(Descriptor(files, originals.Take(originals.Count - 1), probes: rootProbes), new(), _ => Interlocked.Increment(ref signals)))
        {
            Assert.Equal(32, exact.Count); Assert.Null(exact.Warning);
            exact.Dispose(); Assert.Equal(0, exact.Count);
        }
        var descriptor = Descriptor(files, originals, probes: [new(Path.Combine(Path.GetPathRoot(files.Root)!, "missing.props"), false)]);
        using var over = new EditorSourceHints(descriptor, new(), _ => Interlocked.Increment(ref signals));
        Assert.Equal(32, over.Count); Assert.Contains("handle limit", over.Warning);
        using var disabled = new EditorSourceHints(descriptor, new() { MaximumWatchers = 0 }, _ => Interlocked.Increment(ref signals));
        Assert.Equal(0, disabled.Count); Assert.Null(disabled.Warning); Assert.Equal(0, signals);
    }

    private static GameBuildSourceWatchDescriptor Descriptor(SourceWatchFiles files, IEnumerable<GameBuildInputFile> originals,
        IEnumerable<string>? trees = null, IEnumerable<GameBuildSourceWatchProbe>? probes = null, IEnumerable<string>? members = null) =>
        new(files.Project.GameProjectPath, new(files.Artifacts), originals, trees ?? [files.Tree], probes ?? [], members ?? [files.Project.GameProjectPath, files.Source]);
    private static GameBuildInputFile Identity(string path)
    { using var stream = File.OpenRead(path); return new(path, stream.Length, Convert.ToHexString(SHA256.HashData(stream))); }
    private static void Resize(string path, long length) { using var stream = File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None); stream.SetLength(length); }
    private static void AssertSame(EditorSourceScan result)
    { Assert.True(result.Available, result.Reason); Assert.False(result.Changed); Assert.Null(result.Reason); Assert.Empty(result.ChangedPaths); }
    private static FileStream OpenShared(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
        65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private static async Task<T> FinishRetries<T>(SourceWatchClock clock, Task<T> work)
    {
        for (var timers = 0; timers < 8; timers++)
        {
            await SourceWatchFixture.Eventually(() => work.IsCompleted || clock.NextDue != null);
            if (work.IsCompleted) return await work;
            if (clock.NextDue is { } due) clock.Advance(TimeSpan.FromTicks(Math.Max(0, due - clock.GetTimestamp())));
        }
        return await work.WaitAsync(TimeSpan.FromSeconds(10));
    }
    private sealed class ReadHookStream(Stream inner, Action firstRead, Action disposed) : Stream
    {
        private int _read, _disposed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { BeginRead(); return inner.Read(buffer, offset, count); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { BeginRead(); return inner.ReadAsync(buffer, cancellationToken); }
        private void BeginRead() { if (Interlocked.Exchange(ref _read, 1) == 0) firstRead(); }
        protected override void Dispose(bool disposing)
        { if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) { try { inner.Dispose(); } finally { disposed(); } } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync()
        { if (Interlocked.Exchange(ref _disposed, 1) == 0) { try { await inner.DisposeAsync(); } finally { disposed(); } } }
    }
}
