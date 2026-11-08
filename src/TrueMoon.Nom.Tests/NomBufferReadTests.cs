using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomBufferReadTests
{
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(65536)][InlineData(65537)][InlineData(131079)]
    public async Task Read_WholeResourceIntoNonPinnableManagerSlice_PreservesPrefixAndTail(int length)
    {
        await using var context = new Nom6Context(); var bytes = Payload(length); var (_, exact) = await context.Publish(Nom5Context.Asset(1), bytes);
        using var manager = new NomBufferMemory(Enumerable.Repeat((byte)0xcc, length + 13).ToArray());
        var count = await context.Store.ReadResourceAsync(exact, manager.View.Slice(4, length + 5));
        Assert.Equal(length, count); Assert.Equal(bytes, manager.Bytes.AsSpan(4, length).ToArray());
        Assert.All(manager.Bytes[..4], b => Assert.Equal(0xcc, b)); Assert.All(manager.Bytes[(length + 4)..], b => Assert.Equal(0xcc, b));
        Assert.Equal(0, manager.PinRequests); Assert.Equal(0, manager.DisposeCalls);
        await context.Store.DisposeAsync(); NomTestDirectory.AssertRootReleased(context.Arguments);
        Assert.Equal(bytes, manager.Bytes.AsSpan(4, length).ToArray());
    }

    [Fact]
    public void Read_ShortDestinationRejectedBeforeOpenOrMutation()
    {
        var (store, control) = NomBufferStoreProxy.Create(); byte[] destination = [0xcc, 0xdd];
        Assert.Throws<ArgumentException>(() => { _ = store.ReadResourceAsync(Exact(3), destination); });
        Assert.Equal(new byte[] { 0xcc, 0xdd }, destination); Assert.Equal(0, control.Opens);
    }

    [Theory]
    [InlineData(-1L)][InlineData(536870913L)][InlineData(2147483648L)][InlineData(long.MaxValue)]
    public void ReadAndLoad_MalformedLengthRejectedBeforeOpenOrDestinationAccess(long length)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var manager = new NomBufferMemory([0xcc]);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.ReadResourceAsync(Exact(length), manager.View); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.LoadResourceAsync(Exact(length), 1); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, manager.SpanRequests); Assert.Equal(new byte[] { 0xcc }, manager.Bytes);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(536870913)][InlineData(int.MaxValue)]
    public void Load_InvalidCeilingRejectedBeforeOpen(int maximum)
    {
        var (store, control) = NomBufferStoreProxy.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.LoadResourceAsync(Exact(0), maximum); }); Assert.Equal(0, control.Opens);
    }

    [Fact]
    public void Load_OneByteOverBudgetRejectedBeforeOpen()
    {
        var (store, control) = NomBufferStoreProxy.Create();
        Assert.Throws<ArgumentException>(() => { _ = store.LoadResourceAsync(Exact(3), 2); }); Assert.Equal(0, control.Opens);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(65537)]
    public async Task Load_DetachedBytesSurviveReimportGcStoreAndAppDisposal(int length)
    {
        var expected = Payload(length); ReadOnlyMemory<byte> loaded;
        await using (var context = new Nom6Context())
        {
            var row = Nom5Context.Asset(1); var (_, exact) = await context.Publish(row, expected);
            loaded = await context.Store.LoadResourceAsync(exact, Math.Max(1, length));
            Assert.Equal(expected, loaded.ToArray()); var another = await context.Store.LoadResourceAsync(exact, Math.Max(1, length));
            await context.Publish(row, [99, 88]);
            await context.Store.PruneResourceOperationsAsync(1);
            var plan = await context.Store.PlanGarbageCollectionAsync(); var gc = await context.Store.CollectGarbageAsync(plan);
            Assert.False(gc.CleanupPending); Assert.Equal(1, gc.RevisionsPruned);
            Assert.Equal(expected, loaded.ToArray()); Assert.Equal(expected, another.ToArray());
            if (length > 0)
            {
                Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(another, out var backing));
                backing.Array![backing.Offset] ^= 0xff; Assert.Equal(expected, loaded.ToArray());
            }
            await context.Store.DisposeAsync(); NomTestDirectory.AssertRootReleased(context.Arguments);
        }
        Assert.Equal(expected, loaded.ToArray());
    }

    [Theory]
    [InlineData(0)][InlineData(3)]
    public async Task ReadAndLoad_VerifyEvenEmptyObjectsAndLeaveDestinationUntouchedOnOpenFailure(int length)
    {
        await using var context = new Nom6Context(); var (_, exact) = await context.Publish(Nom5Context.Asset(1), Payload(length));
        var path = Nom5Context.ObjectPath(context.Arguments.Root, exact.Digest); File.WriteAllBytes(path, [77]);
        byte[] destination = Enumerable.Repeat((byte)0xcc, Math.Max(1, length)).ToArray();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.ReadResourceAsync(exact, destination));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.LoadResourceAsync(exact, Math.Max(1, length)));
        Assert.All(destination, b => Assert.Equal(0xcc, b));
        await context.Store.DisposeAsync(); NomTestDirectory.AssertRootReleased(context.Arguments);
    }

    [Theory]
    [InlineData("asset")][InlineData("kind")][InlineData("revision")][InlineData("digest")][InlineData("length")]
    public async Task ReadAndLoad_RejectNonExactIdentityWithoutChangingDestination(string part)
    {
        await using var context = new Nom6Context(); var (_, exact) = await context.Publish(Nom5Context.Asset(1), [1, 2, 3]);
        var invalid = part switch
        {
            "asset" => exact with { AssetId = Nom5Context.Id(99) }, "kind" => exact with { KindId = Nom5Context.Id(99) },
            "revision" => exact with { Revision = new string('f', 64) }, "digest" => exact with { Digest = new string('e', 64) },
            _ => exact with { Length = 2 }
        };
        byte[] destination = [0xcc, 0xcc, 0xcc, 0xcc];
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.ReadResourceAsync(invalid, destination));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.LoadResourceAsync(invalid, 10));
        Assert.Equal(new byte[] { 0xcc, 0xcc, 0xcc, 0xcc }, destination);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Read_EnteredFailureOrCancellationLeavesOnlyCopiedPrefix_AndClosesStream(bool cancel)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var cancellation = new CancellationTokenSource(); var fault = new IOException("Read fault after prefix.");
        var source = new NomBufferPartialReadStream([1, 2, 3, 4, 5, 6], cancel ? null : fault, cancel ? cancellation : null);
        control.Open = (_, _) => Task.FromResult<Stream>(source); byte[] destination = Enumerable.Repeat((byte)0xcc, 8).ToArray();
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadResourceAsync(Exact(6), destination, cancellation.Token));
        else Assert.Same(fault, await Assert.ThrowsAsync<IOException>(() => store.ReadResourceAsync(Exact(6), destination)));
        Assert.Equal(new byte[] { 1, 2, 3, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc }, destination);
        Assert.True(source.WasDisposed); Assert.Equal(1, control.Opens);
    }

    [Fact]
    public async Task ReadAndLoad_TruncatedCustomStreamFailsWithoutSuccessfulPartialResult_AndClosesStream()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var opened = new List<ShortSource>();
        control.Open = (_, _) => { var stream = new ShortSource([1]); opened.Add(stream); return Task.FromResult<Stream>(stream); };
        byte[] destination = [0xcc, 0xcc];
        await Assert.ThrowsAsync<EndOfStreamException>(() => store.ReadResourceAsync(Exact(2), destination));
        await Assert.ThrowsAsync<EndOfStreamException>(() => store.LoadResourceAsync(Exact(2), 2));
        Assert.Equal(new byte[] { 1, 0xcc }, destination); Assert.Equal(2, opened.Count); Assert.All(opened, stream => Assert.True(stream.WasDisposed));
    }

    [Fact]
    public async Task ReadAndLoad_EmptyVersionStillOpensAndDisposesVerifiedStream()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var opened = new List<ShortSource>();
        control.Open = (_, _) => { var stream = new ShortSource([]); opened.Add(stream); return Task.FromResult<Stream>(stream); };
        byte[] destination = [0xcc]; Assert.Equal(0, await store.ReadResourceAsync(Exact(0), destination));
        Assert.Empty((await store.LoadResourceAsync(Exact(0), 536870912)).ToArray());
        Assert.Equal(new byte[] { 0xcc }, destination); Assert.Equal(2, control.Opens); Assert.All(opened, stream => Assert.True(stream.WasDisposed));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Read_DestinationLeaseEndsOnlyAfterStreamCloseCompletes_OnSuccessAndFailure(bool fail)
    {
        var (store, control) = NomBufferStoreProxy.Create(); var failure = new IOException("Read failed after prefix.");
        var stream = new NomBufferCloseGateStream([1, 2, 3, 4, 5, 6], fail ? failure : null); control.Open = (_, _) => Task.FromResult<Stream>(stream);
        using var manager = new NomBufferMemory(Enumerable.Repeat((byte)0xcc, 8).ToArray());
        var reading = store.ReadResourceAsync(Exact(6), manager.View);
        try
        {
            await stream.Closing.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(reading.IsCompleted); Assert.False(stream.WasDisposed);
            Assert.Equal(0, manager.DisposeCalls); Assert.Equal(new byte[] { 1, 2, 3 }, manager.Bytes[..3]);
        }
        finally { stream.Release.TrySetResult(); }
        if (fail) Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => reading)); else Assert.Equal(6, await reading);
        Assert.True(stream.WasDisposed); Assert.Equal(0, manager.PinRequests); var reads = manager.SpanRequests; ((IDisposable)manager).Dispose();
        Assert.Equal(reads, manager.SpanRequests); Assert.All(manager.Bytes[6..], b => Assert.Equal(0xcc, b));
        if (fail) Assert.All(manager.Bytes[3..], b => Assert.Equal(0xcc, b));
    }

    [Fact]
    public async Task Load_ResultIsNotPublishedUntilOwnedStreamActuallyCloses()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var source = new NomBufferCloseGateStream([1, 2, 3]); control.Open = (_, _) => Task.FromResult<Stream>(source);
        var loading = store.LoadResourceAsync(Exact(3), 3);
        try { await source.Closing.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(loading.IsCompleted); Assert.False(source.WasDisposed); }
        finally { source.Release.TrySetResult(); }
        Assert.Equal(new byte[] { 1, 2, 3 }, (await loading).ToArray()); Assert.True(source.WasDisposed);
    }
}
