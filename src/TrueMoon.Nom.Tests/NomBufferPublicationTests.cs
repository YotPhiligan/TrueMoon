using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomBufferPublicationTests
{
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(65535)][InlineData(65536)][InlineData(65537)][InlineData(131089)]
    public async Task StreamMemoryAndSpan_IdenticalBytesProduceIdenticalExactVersions(int length)
    {
        await using var context = new Nom6Context(); var store = context.Store; var row = Nom5Context.Asset(1); var bytes = Payload(length);
        var (_, streamed) = await context.Publish(row, bytes);
        var ticket = await Reserve(store, row, bytes); Memory<byte> mutable = bytes;
        var memory = await store.PublishResourceAsync(ticket, row.Id, mutable);
        ticket = await Reserve(store, row, bytes); ReadOnlyMemory<byte> readOnly = bytes;
        var readonlyMemory = await store.PublishResourceAsync(ticket, row.Id, readOnly);
        ticket = await Reserve(store, row, bytes); Span<byte> span = bytes;
        var copied = await store.PublishResourceCopyAsync(ticket, row.Id, span, Math.Max(1, length));
        ticket = await Reserve(store, row, bytes); ReadOnlySpan<byte> readonlySpan = bytes;
        var readonlyCopy = await store.PublishResourceCopyAsync(ticket, row.Id, readonlySpan, Math.Max(1, length));
        Assert.Equal(streamed, Assert.Single(memory.Resources)); Assert.Equal(streamed, Assert.Single(readonlyMemory.Resources));
        Assert.Equal(streamed, Assert.Single(copied.Resources)); Assert.Equal(streamed, Assert.Single(readonlyCopy.Resources));
        Assert.Equal(new long[] { 2, 3, 4, 5 }, new[] { memory.Generation, readonlyMemory.Generation, copied.Generation, readonlyCopy.Generation });
        Assert.False(memory.CleanupPending || readonlyMemory.CleanupPending || copied.CleanupPending || readonlyCopy.CleanupPending);
        await NomFaultContext.AssertBytes(store, streamed, bytes);
    }

    [Fact]
    public async Task ManagerSource_SliceIsBorrowedWithoutPinningOrDisposal_AndReplayDoesNotReadReleasedOwner()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); var expected = Payload(65549);
        var backing = new byte[expected.Length + 11]; expected.CopyTo(backing, 5);
        using var manager = new NomBufferMemory(backing); ReadOnlyMemory<byte> view = manager.View.Slice(5, expected.Length);
        var ticket = await Reserve(context.Store, row, expected); var receipt = await context.Store.PublishResourceAsync(ticket, row.Id, view);
        Assert.True(manager.SpanRequests >= 2); Assert.Equal(0, manager.PinRequests); Assert.Equal(0, manager.DisposeCalls);
        var reads = manager.SpanRequests; ((IDisposable)manager).Dispose();
        var replay = await context.Store.PublishResourceAsync(ticket, row.Id, view);
        Assert.Equal(receipt.Generation, replay.Generation); Assert.Equal(receipt.Resources, replay.Resources); Assert.Equal(reads, manager.SpanRequests);
        await NomFaultContext.AssertBytes(context.Store, Assert.Single(receipt.Resources), expected);
    }

    [Fact]
    public async Task SpanCopy_CapturesSnapshotBeforeReturningTask_DespiteImmediateSourceMutation()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var expected = Payload(31); var original = (byte[])expected.Clone(); var id = Nom5Context.Id(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); byte[]? observed = null; Stream? captured = null;
        control.Publish = async (_, streams, _) =>
        {
            captured = streams[id]; entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var copy = new MemoryStream(); await captured.CopyToAsync(copy); observed = copy.ToArray();
            return new(1, [Exact(expected.Length)]);
        };
        var publishing = store.PublishResourceCopyAsync(Ticket, id, original.AsSpan(), original.Length);
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); original.AsSpan().Fill(0xee); Assert.False(publishing.IsCompleted); }
        finally { release.TrySetResult(); }
        await publishing;
        Assert.Equal(expected, observed); Assert.False(captured!.CanRead); Assert.Equal(1, control.Publishes);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SpanCopy_FailureOrCancellationReleasesSnapshotOnlyAfterUnderlyingPublicationFinishes(bool cancel)
    {
        var (store, control) = NomBufferStoreProxy.Create(); byte[] original = [1, 2, 3, 4]; var id = Nom5Context.Id(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var cancellation = new CancellationTokenSource();
        var failure = new IOException("Publish failed after snapshot read."); Stream? captured = null; byte[]? observed = null;
        control.Publish = async (_, streams, token) =>
        {
            captured = streams[id]; entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var copy = new MemoryStream(); await captured.CopyToAsync(copy); observed = copy.ToArray();
            if (cancel) throw new OperationCanceledException(token); throw failure;
        };
        var publishing = store.PublishResourceCopyAsync(Ticket, id, original, original.Length, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); original.AsSpan().Fill(99); if (cancel) cancellation.Cancel();
            Assert.False(publishing.IsCompleted); Assert.True(captured!.CanRead);
        }
        finally { release.TrySetResult(); }
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing);
        else Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => publishing));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, observed); Assert.False(captured!.CanRead);
        Assert.Throws<ObjectDisposedException>(() => captured.ReadByte()); Assert.Equal(new byte[] { 99, 99, 99, 99 }, original);
    }

    [Fact]
    public async Task Batch_CapturesDescriptorsBeforeAwait_AndAllowsOverlappingReadOnlyViews()
    {
        await using var context = new Nom6Context(); var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2);
        var all = Payload(9); var first = all[..6]; var second = all[3..];
        var ticket = await context.Store.ReserveResourcesAsync(0, [NomFaultContext.Spec(a, first), NomFaultContext.Spec(b, second)]);
        using var manager = new NomBufferMemory(all); var gate = new NomFaultGate(); manager.Gate = gate;
        var sources = new Dictionary<Guid, ReadOnlyMemory<byte>> { [a.Id] = manager.View[..6], [b.Id] = manager.View[3..] };
        var publishing = Task.Run(() => context.Store.PublishResourcesAsync(ticket, sources));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); sources.Clear(); sources[Guid.NewGuid()] = new byte[] { 99 };
            Assert.False(publishing.IsCompleted);
        }
        finally { gate.Release.Set(); }
        var receipt = await publishing; Assert.Equal(1, receipt.Generation); Assert.Equal(2, receipt.Resources.Length);
        Assert.Equal(a.Id, receipt.Resources[0].AssetId); Assert.Equal(b.Id, receipt.Resources[1].AssetId);
        await NomFaultContext.AssertBytes(context.Store, receipt.Resources[0], first); await NomFaultContext.AssertBytes(context.Store, receipt.Resources[1], second);
        Assert.Equal(0, manager.DisposeCalls); Assert.Equal(0, manager.PinRequests);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SingleHelper_RejectsPendingWrongIdOrMultiResourceTicket_WithoutPartialPublication(bool multi)
    {
        await using var context = new Nom6Context(); var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2); byte[] bytes = [1, 2];
        AssetResourceWrite[] specs = multi ? [NomFaultContext.Spec(a, bytes), NomFaultContext.Spec(b, bytes)] : [NomFaultContext.Spec(a, bytes)];
        var ticket = await context.Store.ReserveResourcesAsync(0, specs);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PublishResourceAsync(ticket, multi ? a.Id : b.Id, bytes));
        Assert.Equal(0, context.Store.ContentGeneration); Assert.Empty(await context.Store.ReadAsync());
        Assert.Equal("aborted", (await context.Store.LookupResourceOperationAsync(ticket)).Status);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task SingleHelper_RejectsMismatchedCommittedReceipt_WithoutReadingSource(bool multi)
    {
        await using var context = new Nom6Context(); var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2); byte[] bytes = [3];
        AssetResourceWrite[] specs = multi ? [NomFaultContext.Spec(a, bytes), NomFaultContext.Spec(b, bytes)] : [NomFaultContext.Spec(a, bytes)];
        var ticket = await context.Store.ReserveResourcesAsync(0, specs);
        var sources = new Dictionary<Guid, ReadOnlyMemory<byte>> { [a.Id] = bytes }; if (multi) sources[b.Id] = bytes;
        await context.Store.PublishResourcesAsync(ticket, sources);
        using var manager = new NomBufferMemory(bytes); ReadOnlyMemory<byte> view = manager.View; ((IDisposable)manager).Dispose();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PublishResourceAsync(ticket, multi ? a.Id : b.Id, view));
        Assert.Equal(0, manager.SpanRequests); Assert.Equal(1, context.Store.ContentGeneration);
        Assert.Equal(specs.Length, (await context.Store.ReadAsync()).Length); Assert.Equal("committed", (await context.Store.LookupResourceOperationAsync(ticket)).Status);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(536870913)][InlineData(int.MaxValue)]
    public void Copy_RejectsInvalidBudgetBeforePublication(int ceiling)
    {
        var (store, control) = NomBufferStoreProxy.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.PublishResourceCopyAsync(Ticket, Nom5Context.Id(1), new byte[] { 1 }, ceiling); });
        Assert.Equal(0, control.Publishes);
    }

    [Fact]
    public void Copy_RejectsOneByteOverExplicitBudgetBeforePublication()
    {
        var (store, control) = NomBufferStoreProxy.Create();
        Assert.Throws<ArgumentException>(() => { _ = store.PublishResourceCopyAsync(Ticket, Nom5Context.Id(1), new byte[] { 1, 2 }, 1); });
        Assert.Equal(0, control.Publishes);
    }

    [Theory]
    [InlineData(0)][InlineData(257)]
    public void Batch_RejectsCountOutsideAdmissionBeforeAccessingMemory(int count)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([1]);
        var sources = Enumerable.Range(1, count).ToDictionary(Nom5Context.Id, _ => (ReadOnlyMemory<byte>)manager.View);
        Assert.Throws<ArgumentException>(() => { _ = store.PublishResourcesAsync(Ticket, sources); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, manager.SpanRequests); Assert.Equal(0, manager.DisposeCalls);
    }

    [Theory]
    [InlineData(536870913, 1)][InlineData(536870912, 3)]
    public void MemoryAndBatch_RejectObjectOrAggregateOverflowWithoutAccessingLogicalMemory(int length, int count)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([], length);
        ReadOnlyMemory<byte> view = manager.View;
        if (count == 1) Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.PublishResourceAsync(Ticket, Nom5Context.Id(1), view); });
        else Assert.Throws<ArgumentException>(() => { _ = store.PublishResourcesAsync(Ticket, Enumerable.Range(1, count).ToDictionary(Nom5Context.Id, n => n == 3 ? view[..1] : view)); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, manager.SpanRequests); Assert.Equal(0, manager.PinRequests);
    }

    [Fact]
    public async Task MemoryBatch_AdmitsExactObjectAndAggregateCeilingsWithoutReadingCommittedReplacement()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([], 536870912);
        control.Publish = (_, streams, _) => Task.FromResult(new AssetWriteReceipt(7, [Exact(0)]));
        var receipt = await store.PublishResourceAsync(Ticket, Nom5Context.Id(1), manager.View); Assert.Equal(7, receipt.Generation);
        receipt = await store.PublishResourcesAsync(Ticket, new Dictionary<Guid, ReadOnlyMemory<byte>> { [Nom5Context.Id(1)] = manager.View, [Nom5Context.Id(2)] = manager.View });
        Assert.Equal(7, receipt.Generation); Assert.Equal(2, control.Publishes); Assert.Equal(0, manager.SpanRequests);
    }

    [Theory]
    [InlineData("empty-id")][InlineData("duplicate-id")][InlineData("count-drift")][InlineData("excess-enumeration")]
    public void Batch_RejectsMalformedDescriptorSetsBeforePublishingOrReadingMemory(string mode)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([1]);
        var pair = new KeyValuePair<Guid, ReadOnlyMemory<byte>>(Nom5Context.Id(1), manager.View);
        var sources = mode switch
        {
            "empty-id" => new NomBufferSourceSet(1, new KeyValuePair<Guid, ReadOnlyMemory<byte>>(Guid.Empty, manager.View)),
            "duplicate-id" => new NomBufferSourceSet(2, pair, pair),
            "count-drift" => new NomBufferSourceSet(2, pair),
            _ => new NomBufferSourceSet(1, Enumerable.Range(1, 257).Select(n => new KeyValuePair<Guid, ReadOnlyMemory<byte>>(Nom5Context.Id(n), manager.View)).ToArray())
        };
        Assert.Throws<ArgumentException>(() => { _ = store.PublishResourcesAsync(Ticket, sources); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, manager.SpanRequests); Assert.Equal(0, manager.DisposeCalls);
    }

    [Fact]
    public async Task Batch_AdmitsExactly256DescriptorsAndReleasesAdaptersAfterCompletion()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var sources = Enumerable.Range(1, 256).ToDictionary(Nom5Context.Id, _ => ReadOnlyMemory<byte>.Empty);
        Stream[]? captured = null;
        control.Publish = (_, streams, _) => { captured = streams.Values.ToArray(); Assert.Equal(256, streams.Count); return Task.FromResult(new AssetWriteReceipt(1, [])); };
        Assert.Equal(1, (await store.PublishResourcesAsync(Ticket, sources)).Generation); Assert.Equal(1, control.Publishes);
        Assert.Equal(256, captured!.Length); Assert.All(captured, stream => Assert.False(stream.CanRead));
    }

    [Fact]
    public async Task SpanCopy_CommittedReplayKeepsOriginalOutcomeAndStillEnforcesCopyAdmission()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); byte[] bytes = [1, 2];
        var ticket = await Reserve(context.Store, row, bytes); var first = await context.Store.PublishResourceCopyAsync(ticket, row.Id, bytes, 2);
        byte[] replacement = [99]; var replay = await context.Store.PublishResourceCopyAsync(ticket, row.Id, replacement, 1);
        Assert.Equal(first.Resources, replay.Resources); Assert.Equal(1, replay.Generation); await NomFaultContext.AssertBytes(context.Store, Assert.Single(replay.Resources), bytes);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = context.Store.PublishResourceCopyAsync(ticket, row.Id, replacement, 0); });
    }
}
