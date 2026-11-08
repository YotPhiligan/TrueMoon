using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomBufferFaultTests
{
    [Fact]
    public void CancelBeforeEntry_AllByteHelpersRejectWithoutAccessingOwnersOrStore()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([1, 2]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var token = cancellation.Token; var id = Nom5Context.Id(1);
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.PublishResourceAsync(Ticket, id, manager.View, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.PublishResourceCopyAsync(Ticket, id, new byte[] { 1 }, 1, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.PublishResourcesAsync(Ticket, new Dictionary<Guid, ReadOnlyMemory<byte>> { [id] = manager.View }, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.ReadResourceAsync(Exact(2), manager.View, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.LoadResourceAsync(Exact(2), 2, token); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, control.Publishes); Assert.Equal(0, manager.SpanRequests); Assert.Equal(0, manager.DisposeCalls);
    }

    [Fact]
    public async Task CancelDuringBorrowedRead_LeaseAndRootRemainUntilActualTaskCompletion_ThenTicketIsAborted()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); byte[] bytes = [1, 2, 3];
        var ticket = await Reserve(context.Store, row, bytes); using var manager = new NomBufferMemory(bytes); var gate = new NomFaultGate(); manager.Gate = gate;
        using var cancellation = new CancellationTokenSource(); var publishing = Task.Run(() => context.Store.PublishResourceAsync(ticket, row.Id, manager.View, cancellation.Token));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
            Assert.False(publishing.IsCompleted); Assert.Equal(0, manager.DisposeCalls); Assert.False(context.Store.IsClosed); NomTestDirectory.AssertRootLocked(context.Arguments);
        }
        finally { gate.Release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing); var reads = manager.SpanRequests; ((IDisposable)manager).Dispose();
        Assert.Equal("aborted", (await context.Store.LookupResourceOperationAsync(ticket)).Status); Assert.Empty(await context.Store.ReadAsync());
        await context.Reopen(); Assert.Equal(reads, manager.SpanRequests); Assert.Equal(0, context.Store.ContentGeneration);
        Assert.Equal("aborted", (await context.Store.LookupResourceOperationAsync(ticket)).Status);
    }

    [Fact]
    public async Task AppStop_BlockedMemoryOwnerRetainsRootAndLeaseAfterDrainTimeout_ThenCleansUpWithoutOwnerDisposal()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments();
        await using var app = App.Build(c => c.UseNom(o => o.DrainTimeout = TimeSpan.FromMilliseconds(50)));
        var store = NomTestDirectory.Factory(app).Create(args); var host = NomTestDirectory.Host(app); var row = Nom5Context.Asset(1); byte[] bytes = [1];
        var ticket = await Reserve(store, row, bytes); using var manager = new NomBufferMemory(bytes); var gate = new NomFaultGate(); manager.Gate = gate;
        var publishing = Task.Run(() => store.PublishResourceAsync(ticket, row.Id, manager.View));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); await NomTestDirectory.AssertDrainTimeout(() => app.StopAsync());
            Assert.False(publishing.IsCompleted); Assert.Equal(0, manager.DisposeCalls); Assert.False(store.IsClosed); Assert.False(host.IsReleased);
            Assert.Equal(1, host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        }
        finally { gate.Release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing); var reads = manager.SpanRequests; await host.StopAsync();
        Assert.Equal(reads, manager.SpanRequests); Assert.Equal(0, manager.DisposeCalls); Assert.True(store.IsClosed); Assert.True(host.IsReleased); NomTestDirectory.AssertRootReleased(args);
        await using var other = App.Build(c => c.UseNom()); await using var reopened = NomTestDirectory.Factory(other).Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(0, reopened.ContentGeneration); Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status);
    }

    [Fact]
    public async Task BatchStageFailure_PreservesOldWholeContentAndReleasesBothMemoryOwners()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); await using var store = context.Factory.Create(args);
        var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2); var (_, old) = await Nom3Fixtures.Publish(store, a, [11]);
        byte[] first = [1, 2], second = [3, 4]; var ticket = await store.ReserveResourcesAsync(1, [NomFaultContext.Spec(a, first), NomFaultContext.Spec(b, second)]);
        using var ma = new NomBufferMemory(first); using var mb = new NomBufferMemory(second); var fault = new IOException("Second resource staging fails.");
        context.Io.Arm("stage-write", fault, occurrence: 2, prefix: 1);
        Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => store.PublishResourcesAsync(ticket,
            new Dictionary<Guid, ReadOnlyMemory<byte>> { [a.Id] = ma.View, [b.Id] = mb.View })));
        Assert.Equal(0, ma.DisposeCalls); Assert.Equal(0, mb.DisposeCalls); Assert.Equal(1, store.ContentGeneration);
        Assert.Equal(old, await store.FindResourceAsync(a.Id)); Assert.Null(await store.FindResourceAsync(b.Id)); Assert.Single(await store.ReadAsync());
        Assert.Equal("aborted", (await store.LookupResourceOperationAsync(ticket)).Status); await NomFaultContext.AssertBytes(store, old, [11]);
        var reads = (ma.SpanRequests, mb.SpanRequests); ((IDisposable)ma).Dispose(); ((IDisposable)mb).Dispose();
        context.Io.Disarm(); await store.DisposeAsync(); Assert.Equal(reads, (ma.SpanRequests, mb.SpanRequests)); NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public async Task LostCommitAcknowledgement_ReopenFindsWholeBatch_AndMemoryReplayDoesNotReadReleasedOwners()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2); byte[] first = [1, 2], second = [3, 4];
        var ticket = await store.ReserveResourcesAsync(0, [NomFaultContext.Spec(a, first), NomFaultContext.Spec(b, second)]);
        using var ma = new NomBufferMemory(first); using var mb = new NomBufferMemory(second);
        var sources = new Dictionary<Guid, ReadOnlyMemory<byte>> { [a.Id] = ma.View, [b.Id] = mb.View };
        var fault = new IOException("Commit flush acknowledgement lost."); context.Io.Arm("flush-journal", fault, after: true);
        var error = await Assert.ThrowsAsync<UnknownOutcomeException>(() => store.PublishResourcesAsync(ticket, sources));
        Assert.Equal(ticket, error.Ticket); Assert.Contains(NomFaultContext.Flatten(error), e => ReferenceEquals(e, fault));
        Assert.Equal(0, ma.DisposeCalls); Assert.Equal(0, mb.DisposeCalls); var reads = (ma.SpanRequests, mb.SpanRequests);
        ((IDisposable)ma).Dispose(); ((IDisposable)mb).Dispose(); context.Io.Disarm(); await store.DisposeAsync();
        await using var reopened = context.Factory.Create(args with { Mode = AssetStoreOpenMode.Open }); var outcome = await reopened.LookupResourceOperationAsync(ticket);
        Assert.Equal("committed", outcome.Status); Assert.Equal(1, reopened.ContentGeneration); Assert.Equal(2, outcome.Resources.Length);
        var replay = await reopened.PublishResourcesAsync(ticket, sources); Assert.Equal(outcome.Resources, replay.Resources); Assert.Equal(1, replay.Generation);
        Assert.Equal(reads, (ma.SpanRequests, mb.SpanRequests)); Assert.False(replay.CleanupPending);
        await NomFaultContext.AssertBytes(reopened, replay.Resources[0], first); await NomFaultContext.AssertBytes(reopened, replay.Resources[1], second);
    }

    [Fact]
    public async Task CleanupPendingReplay_CompletesCleanupWithoutReadingReleasedMemoryOwner()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); await using var store = context.Factory.Create(args);
        var row = Nom5Context.Asset(1); byte[] bytes = [8, 9]; var ticket = await Reserve(store, row, bytes); using var manager = new NomBufferMemory(bytes);
        ReadOnlyMemory<byte> view = manager.View; context.Io.Arm("delete-directory", new IOException("Retained cleanup."), persistent: true);
        var receipt = await store.PublishResourceAsync(ticket, row.Id, view); Assert.True(receipt.CleanupPending); Assert.Equal(1, receipt.Generation);
        var reads = manager.SpanRequests; Assert.Equal(0, manager.DisposeCalls); ((IDisposable)manager).Dispose();
        var retained = await store.PublishResourceAsync(ticket, row.Id, view); Assert.True(retained.CleanupPending); Assert.Equal(reads, manager.SpanRequests);
        context.Io.Disarm(); var replay = await store.PublishResourceAsync(ticket, row.Id, view);
        Assert.False(replay.CleanupPending); Assert.Equal(receipt.Resources, replay.Resources); Assert.Equal(reads, manager.SpanRequests);
        Assert.False(Directory.Exists(Nom3Fixtures.StagePath(args, ticket))); await NomFaultContext.AssertBytes(store, Assert.Single(replay.Resources), bytes);
    }

    [Fact]
    public async Task Adapter_ReadsBoundedChunksAndSlices_WithoutSeekingPinningOrOwningManager()
    {
        var bytes = Payload(131080); using var manager = new NomBufferMemory(bytes); using var adapter = new ReadOnlyMemoryResourceStream(manager.View[3..^2]);
        Assert.True(adapter.CanRead); Assert.False(adapter.CanSeek); Assert.False(adapter.CanWrite); Assert.Equal(bytes.Length - 5, adapter.Length);
        var target = new byte[bytes.Length + 17]; var offset = 4;
        var first = await adapter.ReadAsync(target.AsMemory(offset)); Assert.Equal(65536, first); offset += first;
        var second = adapter.Read(target.AsSpan(offset)); Assert.Equal(65536, second); offset += second;
        var third = await adapter.ReadAsync(target, offset, target.Length - offset, CancellationToken.None); Assert.Equal(3, third); offset += third;
        Assert.Equal(0, adapter.Read(target.AsSpan(offset))); Assert.Equal(bytes[3..^2], target.AsSpan(4, offset - 4).ToArray());
        Assert.All(target[..4], b => Assert.Equal(0, b)); Assert.All(target[offset..], b => Assert.Equal(0, b)); Assert.Equal(0, manager.PinRequests);
        Assert.Throws<NotSupportedException>(() => adapter.Seek(0, SeekOrigin.Begin)); Assert.Throws<NotSupportedException>(() => adapter.Write([1], 0, 1));
        var reads = manager.SpanRequests; adapter.Dispose(); Assert.False(adapter.CanRead); Assert.Equal(0, manager.DisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => adapter.Read(target, 0, 1)); Assert.Equal(reads, manager.SpanRequests);
    }

    [Fact]
    public void Adapter_CanceledReadDoesNotTouchSourceOrDestination()
    {
        using var manager = new NomBufferMemory([1]); using var adapter = new ReadOnlyMemoryResourceStream(manager.View);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); byte[] destination = [0xcc];
        Assert.ThrowsAny<OperationCanceledException>(() => adapter.ReadAsync(destination.AsMemory(), cancellation.Token));
        Assert.Equal(new byte[] { 0xcc }, destination); Assert.Equal(0, manager.SpanRequests); Assert.Equal(0, manager.DisposeCalls);
    }
}
