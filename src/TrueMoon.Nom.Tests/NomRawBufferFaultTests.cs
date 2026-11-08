using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using static TrueMoon.Nom.Tests.NomRawBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomRawBufferFaultTests
{
    [Fact]
    public void RawHelpers_CancelBeforeEntryDoesNotAccessMemoryOrStartIo()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<int>([1, 2]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var token = cancellation.Token; var id = Nom5Context.Id(1);
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.PublishRawResourceAsync(Ticket, id, owner.View, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.PublishRawResourceCopyAsync(Ticket, id, new int[] { 1 }.AsSpan(), 4, token); });
        Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.ReadRawResourceAsync(Exact(8), owner.View, token); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, control.Opens); Assert.Equal(0, owner.SpanRequests); Assert.Equal(0, owner.DisposeCalls);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RawBlockedSource_CancelOrAppStopRetainsBorrowedOwnerAndRootUntilActualCompletion(bool stop)
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments();
        await using var app = App.Build(c => c.UseNom(o => o.DrainTimeout = TimeSpan.FromMilliseconds(50)));
        var factory = NomTestDirectory.Factory(app); var store = factory.Create(args); var host = NomTestDirectory.Host(app);
        var row = Nom5Context.Asset(1); int[] values = [17, 23]; var ticket = await Reserve(store, row, Bytes(values));
        using var owner = new NomRawBufferMemory<int>(values); var gate = new NomFaultGate(); owner.Gate = gate; using var cancellation = new CancellationTokenSource();
        var publishing = Task.Run(() => store.PublishRawResourceAsync(ticket, row.Id, owner.View, cancellation.Token));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (stop) await NomTestDirectory.AssertDrainTimeout(() => app.StopAsync()); else cancellation.Cancel();
            Assert.False(publishing.IsCompleted); Assert.Equal(0, owner.DisposeCalls); Assert.False(store.IsClosed); Assert.False(host.IsReleased);
            Assert.Equal(1, host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        }
        finally { gate.Release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing); var reads = owner.SpanRequests; Assert.Equal(0, owner.DisposeCalls); ((IDisposable)owner).Dispose();
        if (!stop) Assert.Equal("aborted", (await store.LookupResourceOperationAsync(ticket)).Status);
        await host.StopAsync(); Assert.True(host.IsReleased); Assert.True(store.IsClosed); Assert.Equal(reads, owner.SpanRequests); NomTestDirectory.AssertRootReleased(args);
        await using var other = App.Build(c => c.UseNom()); await using var reopened = NomTestDirectory.Factory(other).Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(0, reopened.ContentGeneration); Assert.Empty(await reopened.ReadAsync()); Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(ticket)).Status);
    }

    [Fact]
    public async Task RawUnknownCommit_ReopenResolvesExactOutcomeAndReplaySkipsDisposedTypedOwner()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        var row = Nom5Context.Asset(1); var values = Triples(5); var bytes = Bytes(values); var ticket = await Reserve(store, row, bytes);
        using var owner = new NomRawBufferMemory<NomRawTriple>(values); ReadOnlyMemory<NomRawTriple> view = owner.View;
        var failure = new IOException("Raw commit ack lost."); context.Io.Arm("flush-journal", failure, after: true);
        var unknown = await Assert.ThrowsAsync<UnknownOutcomeException>(() => store.PublishRawResourceAsync(ticket, row.Id, view));
        Assert.Equal(ticket, unknown.Ticket); Assert.Contains(NomFaultContext.Flatten(unknown), e => ReferenceEquals(e, failure)); Assert.Equal(0, owner.DisposeCalls);
        var reads = owner.SpanRequests; ((IDisposable)owner).Dispose(); context.Io.Disarm(); await store.DisposeAsync();
        await using var reopened = context.Factory.Create(args with { Mode = AssetStoreOpenMode.Open }); var outcome = await reopened.LookupResourceOperationAsync(ticket);
        Assert.Equal("committed", outcome.Status); Assert.Equal(1, reopened.ContentGeneration);
        var replay = await reopened.PublishRawResourceAsync(ticket, row.Id, view);
        Assert.Equal(outcome.Resources, replay.Resources); Assert.Equal(reads, owner.SpanRequests); Assert.False(replay.CleanupPending);
        await NomFaultContext.AssertBytes(reopened, Assert.Single(replay.Resources), bytes);
    }

    [Fact]
    public async Task RawCleanupPending_ReplayFinishesCleanupWithoutReadingReleasedTypedOwner()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); await using var store = context.Factory.Create(args);
        var row = Nom5Context.Asset(1); int[] values = [11, 22]; var bytes = Bytes(values); var ticket = await Reserve(store, row, bytes);
        using var owner = new NomRawBufferMemory<int>(values); ReadOnlyMemory<int> view = owner.View;
        context.Io.Arm("delete-directory", new IOException("Raw pending cleanup."), persistent: true);
        var first = await store.PublishRawResourceAsync(ticket, row.Id, view); Assert.True(first.CleanupPending); Assert.Equal(1, first.Generation); Assert.Equal(0, owner.DisposeCalls);
        var accesses = owner.SpanRequests; ((IDisposable)owner).Dispose(); var retained = await store.PublishRawResourceAsync(ticket, row.Id, view);
        Assert.True(retained.CleanupPending); Assert.Equal(accesses, owner.SpanRequests);
        context.Io.Disarm(); var replay = await store.PublishRawResourceAsync(ticket, row.Id, view);
        Assert.False(replay.CleanupPending); Assert.Equal(first.Resources, replay.Resources); Assert.Equal(accesses, owner.SpanRequests);
        Assert.False(Directory.Exists(Nom3Fixtures.StagePath(args, ticket))); await NomFaultContext.AssertBytes(store, Assert.Single(replay.Resources), bytes);
    }

    [Fact]
    public async Task RawAdapter_CapsByteChunksAcrossElementBoundaries_AndDisposalNeverOwnsTypedManager()
    {
        var values = Triples(43693); var expected = Bytes(values[1..^1]); using var owner = new NomRawBufferMemory<NomRawTriple>(values);
        using var stream = new ReadOnlyRawMemoryResourceStream<NomRawTriple>(owner.View[1..^1], expected.Length);
        Assert.True(stream.CanRead); Assert.False(stream.CanWrite); Assert.False(stream.CanSeek); Assert.Equal(expected.Length, stream.Length);
        byte[] target = new byte[expected.Length]; var first = await stream.ReadAsync(target.AsMemory()); Assert.Equal(65536, first);
        var second = stream.Read(target.AsSpan(first)); Assert.Equal(65536, second);
        var last = await stream.ReadAsync(target, first + second, target.Length - first - second, CancellationToken.None); Assert.Equal(1, last);
        Assert.Equal(-1, stream.ReadByte()); Assert.Equal(expected, target); Assert.Equal(0, owner.PinRequests);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin)); Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        var reads = owner.SpanRequests; stream.Dispose(); Assert.False(stream.CanRead); Assert.Equal(0, owner.DisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte()); Assert.Equal(reads, owner.SpanRequests);
    }

    [Fact]
    public void RawProjection_AdmissionDoesNotAccessCallerSpan_AndDisposeInvalidatesOnlyOwnedByteView()
    {
        using var owner = new NomRawBufferMemory<int>([7, 8]); using var projection = new RawDestinationMemory<int>(owner.View, 8); var bytes = projection.View;
        Assert.Equal(8, bytes.Length); Assert.Equal(0, owner.SpanRequests); Assert.Equal(Bytes(owner.Values), bytes.ToArray());
        Assert.Throws<NotSupportedException>(() => projection.Pin()); Assert.Equal(0, owner.PinRequests);
        var accesses = owner.SpanRequests; ((IDisposable)projection).Dispose(); Assert.Equal(0, owner.DisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => bytes.Span.ToArray()); Assert.Equal(accesses, owner.SpanRequests); Assert.Equal(new[] { 7, 8 }, owner.Values);
    }
}
