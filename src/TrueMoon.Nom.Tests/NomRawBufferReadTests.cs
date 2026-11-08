using System.Runtime.InteropServices;
using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using static TrueMoon.Nom.Tests.NomRawBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomRawBufferReadTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void RawRead_NullStoreOrExactIsRejectedBeforeAccessingDestination(bool nullStore)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<int>([9]);
        var target = nullStore ? null! : store; var exact = nullStore ? Exact(4) : null!;
        Assert.Throws<ArgumentNullException>(() => { _ = target.ReadRawResourceAsync(exact, owner.View); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, owner.SpanRequests); Assert.Equal(new[] { 9 }, owner.Values);
    }

    [Fact]
    public async Task RawRead_NonPinnableDestinationSlice_ReturnsElementsAndPreservesNeighborBytes()
    {
        await using var context = new Nom6Context(); int[] input = [1, -123, int.MaxValue]; var bytes = Bytes(input); var (_, exact) = await context.Publish(Nom5Context.Asset(1), bytes);
        using var owner = new NomRawBufferMemory<int>([111, 222, 333, 444, 555, 666]);
        Assert.Equal(3, await context.Store.ReadRawResourceAsync(exact, owner.View.Slice(1, 4)));
        Assert.Equal(new[] { 111, 1, -123, int.MaxValue, 555, 666 }, owner.Values); Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls);
        await context.Store.DisposeAsync(); NomTestDirectory.AssertRootReleased(context.Arguments); Assert.Equal(input, owner.Values[1..4]);
    }

    [Theory]
    [InlineData(-1L)][InlineData(536870913L)][InlineData(2147483648L)][InlineData(long.MaxValue)]
    public void RawRead_RejectsMalformedByteLengthBeforeOpenOrDestinationAccess(long bytes)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([99]);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.ReadRawResourceAsync(Exact(bytes), owner.View); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, owner.SpanRequests); Assert.Equal(new long[] { 99 }, owner.Values);
    }

    [Theory]
    [InlineData(1)][InlineData(7)][InlineData(9)]
    public void RawRead_RejectsNondivisibleByteLengthBeforeOpenOrDestinationAccess(int bytes)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([99, 88]);
        Assert.Throws<ArgumentException>(() => { _ = store.ReadRawResourceAsync(Exact(bytes), owner.View); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, owner.SpanRequests); Assert.Equal(new long[] { 99, 88 }, owner.Values);
    }

    [Fact]
    public void RawRead_RejectsOneElementShortDestinationBeforeOpenOrMutation()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([99]);
        Assert.Throws<ArgumentException>(() => { _ = store.ReadRawResourceAsync(Exact(16), owner.View); });
        Assert.Equal(0, control.Opens); Assert.Equal(0, owner.SpanRequests); Assert.Equal(new long[] { 99 }, owner.Values);
    }

    [Theory]
    [InlineData(0)][InlineData(1)]
    public async Task RawRead_HugeUnusedLogicalCapacityIsSlicedBeforeByteProjection(int requestedElements)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([99], int.MaxValue);
        var source = new ShortSource(requestedElements == 0 ? [] : Bytes<long>([-123])); control.Open = (_, _) => Task.FromResult<Stream>(source);
        Assert.Equal(requestedElements, await store.ReadRawResourceAsync(Exact(requestedElements * 8), owner.View));
        Assert.Equal(requestedElements == 0 ? 99 : -123, owner.Values[0]); Assert.Equal(1, control.Opens); Assert.True(source.WasDisposed);
        Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls); if (requestedElements == 0) Assert.Equal(0, owner.SpanRequests);
    }

    [Theory]
    [InlineData(0)][InlineData(4)]
    public async Task RawRead_VerifiesEmptyAndNonemptyExactResourcesBeforeChangingDestination(int bytes)
    {
        await using var context = new Nom6Context(); var (_, exact) = await context.Publish(Nom5Context.Asset(1), Payload(bytes));
        File.WriteAllBytes(Nom5Context.ObjectPath(context.Arguments.Root, exact.Digest), [77]); int[] target = [123, 456];
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.ReadRawResourceAsync(exact, target.AsMemory()));
        Assert.Equal(new[] { 123, 456 }, target); await context.Store.DisposeAsync(); NomTestDirectory.AssertRootReleased(context.Arguments);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RawRead_EnteredFailureOrCancellationMayLeavePartialElementBytes_ButNeverSuccess(bool cancel)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var cancellation = new CancellationTokenSource(); var failure = new IOException("Raw partial-element read fault.");
        var source = new NomBufferPartialReadStream([1, 2, 3, 4, 5, 6, 7, 8], cancel ? null : failure, cancel ? cancellation : null);
        control.Open = (_, _) => Task.FromResult<Stream>(source); var target = new int[3]; MemoryMarshal.AsBytes(target.AsSpan()).Fill(0xcc);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadRawResourceAsync(Exact(8), target.AsMemory(), cancellation.Token));
        else Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => store.ReadRawResourceAsync(Exact(8), target.AsMemory())));
        Assert.Equal(new byte[] { 1, 2, 3, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc }, Bytes(target));
        Assert.True(source.WasDisposed); Assert.Equal(1, control.Opens);
    }

    [Fact]
    public async Task RawRead_TruncatedStreamThrowsAndClosesAfterPartialElementCopy()
    {
        var (store, control) = NomBufferStoreProxy.Create(); var source = new ShortSource([1, 2, 3]); control.Open = (_, _) => Task.FromResult<Stream>(source);
        int[] destination = [0];
        await Assert.ThrowsAsync<EndOfStreamException>(() => store.ReadRawResourceAsync(Exact(4), destination.AsMemory()));
        Assert.Equal(new byte[] { 1, 2, 3, 0 }, Bytes(destination)); Assert.True(source.WasDisposed);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RawRead_BorrowedDestinationLeaseEndsOnlyAfterActualStreamClose_OnSuccessAndFailure(bool fail)
    {
        var (store, control) = NomBufferStoreProxy.Create(); var failure = new IOException("Read after prefix failed.");
        var source = new NomBufferCloseGateStream([1, 2, 3, 4], fail ? failure : null); control.Open = (_, _) => Task.FromResult<Stream>(source);
        using var owner = new NomRawBufferMemory<int>([0, 99]); var reading = store.ReadRawResourceAsync(Exact(4), owner.View);
        try
        {
            await source.Closing.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(reading.IsCompleted); Assert.False(source.WasDisposed); Assert.Equal(0, owner.DisposeCalls);
        }
        finally { source.Release.TrySetResult(); }
        if (fail) Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => reading)); else Assert.Equal(1, await reading);
        Assert.True(source.WasDisposed); Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls); Assert.Equal(99, owner.Values[1]);
        var accesses = owner.SpanRequests; ((IDisposable)owner).Dispose(); Assert.Equal(accesses, owner.SpanRequests);
        Assert.Equal(fail ? new byte[] { 1, 2, 3, 0 } : new byte[] { 1, 2, 3, 4 }, Bytes(owner.Values)[..4]);
    }
}
