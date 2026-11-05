using System.Collections.Concurrent;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceOpenBatchCensusTests
{
    [Theory]
    [InlineData(255, 0)] [InlineData(256, 64)] [InlineData(257, 65)]
    public async Task Scan_DefaultThresholdUsesBoundedNativeBatchesAndStillHashesBytesAndFreshProbes(int count, int expectedBatches)
    {
        using var graph = new Graph(count, "zz-staged"); var offsets = new List<int>(); var products = new List<EditorSourceOpenBatch>();
        var census = new EditorSourceCensus(TimeProvider.System, batches: async (files, offset, validate, token) =>
        {
            offsets.Add(offset); var batch = await EditorSourceOpenBatch.CreateAsync(files, offset, validate, token);
            products.Add(batch); return batch;
        });
        Assert.Equal(count, graph.Descriptor.Files.Count);
        var changedPath = graph.Descriptor.Files[^1].Path; var original = File.ReadAllBytes(changedPath); var stamp = File.GetLastWriteTimeUtc(changedPath);
        AssertSame(await census.ScanAsync(graph.Descriptor, default)); CheckBatchOffsets(); ReopenAll(graph);
        var changedBytes = original.ToArray(); changedBytes[0] ^= 0x17; File.WriteAllBytes(changedPath, changedBytes); File.SetLastWriteTimeUtc(changedPath, stamp);
        Assert.Equal(original.Length, new FileInfo(changedPath).Length); Assert.Equal(stamp, File.GetLastWriteTimeUtc(changedPath));
        var changed = await census.ScanAsync(graph.Descriptor, default);
        Assert.True(changed.Available); Assert.True(changed.Changed); Assert.Equal(new[] { changedPath }, changed.ChangedPaths);
        CheckBatchOffsets(); ReopenAll(graph);
        File.WriteAllBytes(changedPath, original); File.SetLastWriteTimeUtc(changedPath, stamp);
        AssertSame(await census.ScanAsync(graph.Descriptor, default)); CheckBatchOffsets();
        File.WriteAllText(graph.Probe, "new optional input");
        changed = await census.ScanAsync(graph.Descriptor, default);
        Assert.True(changed.Available); Assert.True(changed.Changed); Assert.Equal(new[] { graph.Probe }, changed.ChangedPaths);
        CheckBatchOffsets(); ReopenAll(graph);
        Assert.Equal(expectedBatches * 4, products.Count);
        Assert.All(products, batch => Assert.Throws<ObjectDisposedException>(() => batch.Take(0)));

        void CheckBatchOffsets()
        {
            Assert.Equal(Enumerable.Range(0, expectedBatches).Select(index => index * 4), offsets);
            offsets.Clear();
        }
    }

    [Theory]
    [InlineData("open")] [InlineData("metadata")] [InlineData("directories")]
    public async Task Scan_CustomHooksKeepSerialAdmissionEvenWhenTheDescriptorExceedsTheBatchThreshold(string hook)
    {
        using var graph = new Graph(256); var batches = 0; var nativeReaders = new List<EditorSourceFile>();
        var census = new EditorSourceCensus(TimeProvider.System,
            open: hook == "open" ? path => { var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); nativeReaders.Add(reader); return reader; } : null,
            directories: hook == "directories" ? token => new EditorSourceDirectoryCache(token) : null,
            metadata: hook == "metadata" ? EditorSourceFile.ReadMetadata : null,
            batches: (_, _, _, _) => { batches++; throw new InvalidOperationException("Custom hooks must retain serial admission."); });
        AssertSame(await census.ScanAsync(graph.Descriptor, default)); Assert.Equal(0, batches);
        Assert.Equal(hook == "open" ? 256 : 0, nativeReaders.Count);
        Assert.All(nativeReaders, reader => Assert.False(reader.CanRead)); ReopenAll(graph);
    }

    [Fact]
    public async Task Scan_ProvenEarlierContentChangeWinsOverALaterPrefetchedOpenFailureAndDrainsUnreadReaders()
    {
        using var graph = new Graph(256, "00-staged"); var first = graph.Descriptor.Files[0].Path; var locked = graph.Descriptor.Files[1].Path;
        Assert.Equal(graph.Generated[0], first); Assert.Equal(graph.Generated[1], locked);
        var stamp = File.GetLastWriteTimeUtc(first); File.WriteAllText(first, "modified"); File.SetLastWriteTimeUtc(first, stamp);
        using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await new EditorSourceCensus(TimeProvider.System).ScanAsync(graph.Descriptor, default);
            Assert.True(result.Available); Assert.True(result.Changed); Assert.Equal(new[] { first }, result.ChangedPaths); Assert.Null(result.Reason);
            foreach (var file in graph.Descriptor.Files.Where(file => file.Path != locked)) Exclusive(file);
            Assert.Equal(8, held.Length);
        }
        ReopenAll(graph);
    }

    [Fact]
    public async Task Scan_OrderedOpenFailureRetriesFreshBatchesAndReleasesEveryStagedFileBeforeUnavailable()
    {
        using var graph = new Graph(256, "00-staged"); var clock = new SourceWatchClock(); var locked = graph.Descriptor.Files[1].Path;
        var offsets = new List<int>(); var batches = new List<EditorSourceOpenBatch>();
        var census = new EditorSourceCensus(clock, batches: async (files, offset, validate, token) =>
        {
            if (batches.Count != 0)
            {
                Assert.Throws<ObjectDisposedException>(() => batches[^1].Take(0));
                Exclusive(graph.Descriptor.Files[2]); // No unread native product survives into the next attempt.
            }
            offsets.Add(offset); var batch = await EditorSourceOpenBatch.CreateAsync(files, offset, validate, token);
            batches.Add(batch); return batch;
        });
        using (var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await FinishRetries(clock, census.ScanAsync(graph.Descriptor, default));
            Assert.False(result.Available); Assert.False(result.Changed); Assert.True(result.Recoverable); Assert.Empty(result.ChangedPaths);
            Assert.Contains("coverage failed", result.Reason); Assert.Contains("IOException", result.Reason);
            Assert.Equal(new[] { 0, 0, 0, 0 }, offsets); Assert.Equal(4, batches.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(180).Ticks, clock.GetTimestamp());
            Assert.All(batches, batch => Assert.Throws<ObjectDisposedException>(() => batch.Take(0)));
            foreach (var file in graph.Descriptor.Files.Where(file => file.Path != locked)) Exclusive(file);
        }
        AssertSame(await new EditorSourceCensus(TimeProvider.System).ScanAsync(graph.Descriptor, default)); ReopenAll(graph);
    }

    [Fact]
    public async Task Stop_WaitsForFourActualNativeOpensAndCannotPublishOrReleaseOwnershipBeforeTheyDrain()
    {
        using var graph = new Graph(256); using var release = new ManualResetEventSlim(); var clock = new SourceWatchClock();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owners = new ConcurrentBag<EditorSourceFile>(); var entered = 0; var batchCalls = 0;
        var census = new EditorSourceCensus(clock, batches: (files, offset, validate, token) =>
        {
            Interlocked.Increment(ref batchCalls);
            return EditorSourceOpenBatch.CreateAsync(files, offset, validate, token, path =>
            {
                var reader = EditorSourceFile.Open(path) ?? throw new FileNotFoundException(path); owners.Add(reader);
                if (Interlocked.Increment(ref entered) == 4) ready.TrySetResult();
                try { if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Owned native open gate not released."); return reader; }
                catch { reader.Dispose(); throw; }
            });
        });
        var hints = new SourceWatchHints();
        var watch = new EditorSourceWatch(graph.Files.Project, new(), clock, census.ScanAsync, hints.Create);
        Task? stopping = null;
        try
        {
            watch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, graph.Descriptor);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); var before = watch.Read();
            Assert.True(before.IsScanning); Assert.Equal(EditorSourceState.Checking, before.State); Assert.Equal(1, batchCalls);
            stopping = watch.DisposeAsync().AsTask();
            await SourceWatchFixture.Eventually(() => Volatile.Read(ref hints.Latest.Stops) == 1);
            Assert.False(stopping.IsCompleted); Assert.Equal(4, owners.Count); Assert.All(owners, reader => Assert.True(reader.CanRead));
            Assert.Equal(EditorSourceState.Stopped, watch.Read().State); Assert.False(watch.Read().IsScanning);
            release.Set(); await stopping.WaitAsync(TimeSpan.FromSeconds(10)); await watch.DisposeAsync();
            var after = watch.Read(); Assert.Equal(EditorSourceState.Stopped, after.State); Assert.Equal(0, after.WatcherCount);
            Assert.Equal(before.ObservationVersion, after.ObservationVersion); Assert.False(after.IsScanning);
            Assert.Equal(1, batchCalls); Assert.Equal(1, hints.Latest.Stops); Assert.Equal(1, hints.Latest.Disposals); Assert.Equal(0, clock.TimerCount);
            Assert.All(owners, reader => Assert.False(reader.CanRead)); ReopenAll(graph);
        }
        finally
        {
            release.Set(); stopping ??= watch.DisposeAsync().AsTask(); await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var reader in owners) reader.Dispose();
        }
    }

    private static void AssertSame(EditorSourceScan result)
    { Assert.True(result.Available, result.Reason); Assert.False(result.Changed); Assert.Null(result.Reason); Assert.Empty(result.ChangedPaths); }
    private static void Exclusive(GameBuildInputFile file)
    { using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(file.Length, stream.Length); }
    private static void ReopenAll(Graph graph) { foreach (var file in graph.Descriptor.Files) Exclusive(file); }
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
    private sealed class Graph : IDisposable
    {
        internal SourceWatchFiles Files { get; } = new();
        internal string[] Generated { get; }
        internal string Probe { get; }
        internal GameBuildSourceWatchDescriptor Descriptor { get; }
        internal Graph(int count, string name = "batch-inputs")
        {
            var directory = Path.Combine(Files.Root, name); Directory.CreateDirectory(directory);
            Generated = Enumerable.Range(0, count - 2).Select(index =>
            { var path = Path.Combine(directory, index.ToString("D4") + ".bin"); File.WriteAllText(path, "original"); return path; }).ToArray();
            Probe = Path.Combine(Files.Root, "optional.props");
            Descriptor = Files.Descriptor(Generated.Concat(new[] { Files.Project.GameProjectPath, Files.Source }),
                probes: [new(Generated[^1], true), new(Probe, false)]);
        }
        public void Dispose() => Files.Dispose();
    }
}
