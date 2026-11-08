using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Xunit;
using static TrueMoon.Nom.Tests.NomBufferFixtures;
using static TrueMoon.Nom.Tests.NomRawBufferFixtures;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomRawBufferPublicationTests
{
    [Theory]
    [InlineData("byte")][InlineData("short")][InlineData("int")][InlineData("long")][InlineData("float")]
    [InlineData("double")][InlineData("bool")][InlineData("char")][InlineData("enum")][InlineData("native")]
    [InlineData("decimal")][InlineData("int128")][InlineData("triple")][InlineData("vector12")][InlineData("padded")]
    public Task RawVariants_ProduceSameRuntimeBytesAndExactVersionAsByteStream(string type) => type switch
    {
        "byte" => CheckVariants<byte>([0, 1, 255]), "short" => CheckVariants<short>([0x1234, -1, short.MinValue]),
        "int" => CheckVariants<int>([0x12345678, -1, int.MinValue]), "long" => CheckVariants<long>([0x123456789abcdef, -1, long.MinValue]),
        "float" => CheckVariants<float>([-0f, 1.5f, float.PositiveInfinity]), "double" => CheckVariants<double>([-0d, 1.5, double.NegativeInfinity]),
        "bool" => CheckVariants<bool>([false, true, true]), "char" => CheckVariants<char>(['A', '\u03bb', '\uffff']),
        "enum" => CheckVariants<NomRawCode>([NomRawCode.Start, NomRawCode.End]), "native" => CheckVariants<nint>([1, -1, nint.MaxValue]),
        "decimal" => CheckVariants<decimal>([1.25m, -3.50m]), "int128" => CheckVariants<Int128>([Int128.MinValue, Int128.MaxValue]),
        "triple" => CheckVariants(Triples(5)), "vector12" => CheckVariants<NomRawVector>([new() { X = 1, Y = -2, Z = 3 }, new() { X = int.MinValue, Y = 20, Z = int.MaxValue }]),
        _ => CheckVariants<NomRawPadded>([new() { Tag = 0x56, Value = 0x12345678 }])
    };

    private static async Task CheckVariants<T>(T[] values) where T : unmanaged
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); var expected = Bytes(values);
        var (_, streamed) = await context.Publish(row, expected);
        var ticket = await Reserve(context.Store, row, expected); Memory<T> mutable = values;
        var memory = await context.Store.PublishRawResourceAsync(ticket, row.Id, mutable);
        ticket = await Reserve(context.Store, row, expected); ReadOnlyMemory<T> readOnly = values;
        var readonlyMemory = await context.Store.PublishRawResourceAsync(ticket, row.Id, readOnly);
        ticket = await Reserve(context.Store, row, expected); Span<T> span = values;
        var copy = await context.Store.PublishRawResourceCopyAsync(ticket, row.Id, span, Math.Max(1, expected.Length));
        ticket = await Reserve(context.Store, row, expected); ReadOnlySpan<T> readonlySpan = values;
        var readonlyCopy = await context.Store.PublishRawResourceCopyAsync(ticket, row.Id, readonlySpan, Math.Max(1, expected.Length));
        Assert.Equal(new long[] { 2, 3, 4, 5 }, new[] { memory.Generation, readonlyMemory.Generation, copy.Generation, readonlyCopy.Generation });
        foreach (var receipt in new[] { memory, readonlyMemory, copy, readonlyCopy })
        { Assert.Equal(streamed, Assert.Single(receipt.Resources)); Assert.False(receipt.CleanupPending); }
        Assert.Equal(expected.Length, streamed.Length); Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expected)), streamed.Digest);
        T[] destination = new T[values.Length + 1]; Assert.Equal(values.Length, await context.Store.ReadRawResourceAsync(streamed, destination.AsMemory()));
        Assert.Equal(expected, Bytes(destination)[..expected.Length]); Assert.All(Bytes(destination)[expected.Length..], b => Assert.Equal(0, b));
        await NomFaultContext.AssertBytes(context.Store, streamed, expected);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(21845)][InlineData(21846)][InlineData(43691)]
    public async Task RawPackedThreeByteElements_EmptySingletonAndCrossChunkReadsRemainExact(int elements)
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); var values = Triples(elements); var expected = Bytes(values);
        Assert.Equal(3, Unsafe.SizeOf<NomRawTriple>()); var ticket = await Reserve(context.Store, row, expected);
        var receipt = await context.Store.PublishRawResourceAsync(ticket, row.Id, values.AsMemory()); var exact = Assert.Single(receipt.Resources);
        var target = Triples(elements + 2); var before = Bytes(target);
        Assert.Equal(elements, await context.Store.ReadRawResourceAsync(exact, target.AsMemory(1)));
        var after = Bytes(target); Assert.Equal(before[..3], after[..3]); Assert.Equal(expected, after.AsSpan(3, expected.Length).ToArray()); Assert.Equal(before[^3..], after[^3..]);
        Assert.Equal(expected.Length, exact.Length); await NomFaultContext.AssertBytes(context.Store, exact, expected);
    }

    [Fact]
    public async Task RawPaddedStruct_PreservesNonzeroPaddingAndChangingOnlyPaddingChangesDigestAndRevision()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); var values = new NomRawPadded[1];
        var raw = MemoryMarshal.AsBytes(values.AsSpan()); raw.Fill(0xa7); values[0].Tag = 9; values[0].Value = 0x12345678;
        Assert.Equal(8, Unsafe.SizeOf<NomRawPadded>()); var firstBytes = Bytes(values); Assert.Equal(0xa7, firstBytes[1]);
        var ticket = await Reserve(context.Store, row, firstBytes); var first = (await context.Store.PublishRawResourceAsync(ticket, row.Id, values.AsMemory())).Resources.Single();
        MemoryMarshal.AsBytes(values.AsSpan())[1] = 0x6c; var secondBytes = Bytes(values); Assert.Equal(9, values[0].Tag); Assert.Equal(0x12345678, values[0].Value);
        ticket = await Reserve(context.Store, row, secondBytes); var second = (await context.Store.PublishRawResourceCopyAsync(ticket, row.Id, values.AsSpan(), 8)).Resources.Single();
        Assert.NotEqual(first.Digest, second.Digest); Assert.NotEqual(first.Revision, second.Revision);
        Assert.Equal(firstBytes, (await context.Store.LoadResourceAsync(first, 8)).ToArray()); Assert.Equal(secondBytes, (await context.Store.LoadResourceAsync(second, 8)).ToArray());
    }

    [Fact]
    public async Task RawManagerSourceSlice_IsBorrowedWithoutPinOrDisposal_AndCommittedReplaySkipsReleasedOwner()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); var values = Triples(22000); var expected = Bytes(values[2..^1]);
        using var owner = new NomRawBufferMemory<NomRawTriple>(values); ReadOnlyMemory<NomRawTriple> view = owner.View[2..^1];
        var ticket = await Reserve(context.Store, row, expected); var receipt = await context.Store.PublishRawResourceAsync(ticket, row.Id, view);
        Assert.True(owner.SpanRequests >= 2); Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls);
        var reads = owner.SpanRequests; ((IDisposable)owner).Dispose(); var replay = await context.Store.PublishRawResourceAsync(ticket, row.Id, view);
        Assert.Equal(receipt.Resources, replay.Resources); Assert.Equal(receipt.Generation, replay.Generation); Assert.Equal(reads, owner.SpanRequests);
        await NomFaultContext.AssertBytes(context.Store, Assert.Single(replay.Resources), expected);
    }

    [Theory]
    [InlineData(67108865)][InlineData(268435456)][InlineData(int.MaxValue)]
    public void RawMemory_RejectsByteCeilingAndIntSizedProjectionOverflowBeforeAccessOrPublication(int elements)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([], elements);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.PublishRawResourceAsync(Ticket, Nom5Context.Id(1), owner.View); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, owner.SpanRequests); Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls);
    }

    [Fact]
    public void RawPackedMemory_RejectsExactlyOneByteOverObjectCeilingBeforeProjection()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<NomRawTriple>([], 178956971);
        Assert.Equal(536870913L, (long)owner.View.Length * Unsafe.SizeOf<NomRawTriple>());
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.PublishRawResourceAsync(Ticket, Nom5Context.Id(1), owner.View); });
        Assert.Equal(0, control.Publishes); Assert.Equal(0, owner.SpanRequests); Assert.Equal(0, owner.PinRequests);
    }

    [Theory]
    [InlineData("store")][InlineData("ticket")][InlineData("id")]
    public void RawPublication_RejectsMissingStoreTicketOrIdentityBeforeAccessingCallerMemory(string missing)
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<int>([1]);
        var target = missing == "store" ? null! : store; var ticket = missing == "ticket" ? null! : Ticket; var id = missing == "id" ? Guid.Empty : Nom5Context.Id(1);
        Action memory = () => { _ = target.PublishRawResourceAsync(ticket, id, owner.View); };
        Action copy = () => { _ = target.PublishRawResourceCopyAsync(ticket, id, new int[] { 1 }.AsSpan(), 4); };
        if (missing == "id") { Assert.Throws<ArgumentException>(memory); Assert.Throws<ArgumentException>(copy); }
        else { Assert.Throws<ArgumentNullException>(memory); Assert.Throws<ArgumentNullException>(copy); }
        Assert.Equal(0, control.Publishes); Assert.Equal(0, owner.SpanRequests); Assert.Equal(0, owner.DisposeCalls);
    }

    [Fact]
    public async Task RawMemory_AdmitsExact512MiBByteCeilingWithoutReadingCommittedReplacement()
    {
        var (store, control) = NomBufferStoreProxy.Create(); using var owner = new NomRawBufferMemory<long>([], 67108864);
        Stream? submitted = null;
        control.Publish = (_, streams, _) => { submitted = streams.Values.Single(); Assert.Equal(536870912, submitted.Length); return Task.FromResult(new AssetWriteReceipt(7, [Exact(0)])); };
        Assert.Equal(7, (await store.PublishRawResourceAsync(Ticket, Nom5Context.Id(1), owner.View)).Generation);
        Assert.Equal(1, control.Publishes); Assert.Equal(0, owner.SpanRequests); Assert.Equal(0, owner.PinRequests); Assert.Equal(0, owner.DisposeCalls); Assert.False(submitted!.CanRead);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(536870913)][InlineData(int.MaxValue)]
    public void RawCopy_RejectsInvalidByteBudgetBeforePublication(int maximumBytes)
    {
        var (store, control) = NomBufferStoreProxy.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), new int[] { 1 }.AsSpan(), maximumBytes); });
        Assert.Equal(0, control.Publishes);
    }

    [Fact]
    public void RawCopy_RejectsIntOverflowByteProjectionBeforeAsBytesOrPublication()
    {
        var (store, control) = NomBufferStoreProxy.Create();
        void Invoke()
        {
            // A logical view is never dereferenced. Even a missing raw guard makes
            // AsBytes reject the >int byte length before any allocation or memory access.
            long value = 1; var logical = MemoryMarshal.CreateReadOnlySpan(ref value, 268435456);
            _ = store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), logical, 536870912);
        }
        Assert.Throws<ArgumentOutOfRangeException>(Invoke); Assert.Equal(0, control.Publishes);
    }

    [Theory]
    [InlineData(1)][InlineData(536870912)]
    public async Task RawEmptyCopy_AdmitsMinimumAndMaximumPositiveByteBudgets(int maximumBytes)
    {
        var (store, control) = NomBufferStoreProxy.Create();
        control.Publish = (_, streams, _) => { Assert.Equal(0, streams.Values.Single().Length); return Task.FromResult(new AssetWriteReceipt(1, [Exact(0)])); };
        Assert.Equal(1, (await store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), ReadOnlySpan<int>.Empty, maximumBytes)).Generation);
        Assert.Equal(1, control.Publishes);
    }

    [Fact]
    public async Task RawCopy_BudgetIsBytes_NotElementCount_AndExactBudgetIsAccepted()
    {
        var (store, control) = NomBufferStoreProxy.Create(); int[] values = [1, 2]; byte[]? observed = null;
        control.Publish = async (_, streams, _) => { using var copy = new MemoryStream(); await streams.Values.Single().CopyToAsync(copy); observed = copy.ToArray(); return new(1, [Exact(8)]); };
        Assert.Throws<ArgumentException>(() => { _ = store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), values.AsSpan(), 7); }); Assert.Equal(0, control.Publishes);
        await store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), values.AsSpan(), 8);
        Assert.Equal(Bytes(values), observed); Assert.Equal(1, control.Publishes);
    }

    [Theory]
    [InlineData("success")][InlineData("error")][InlineData("cancel")]
    public async Task RawSpanSnapshot_IsCapturedBeforeTaskReturn_AndRetainedUntilActualPublicationCompletion(string outcome)
    {
        var (store, control) = NomBufferStoreProxy.Create(); int[] values = [0x12345678, -1]; var expected = Bytes(values); byte[]? observed = null; Stream? submitted = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(); var failure = new IOException("Raw publication failed.");
        control.Publish = async (_, streams, token) =>
        {
            submitted = streams.Values.Single(); entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var copy = new MemoryStream(); await submitted.CopyToAsync(copy); observed = copy.ToArray();
            if (outcome == "cancel") throw new OperationCanceledException(token); if (outcome == "error") throw failure;
            return new(1, [Exact(8)]);
        };
        var publishing = store.PublishRawResourceCopyAsync(Ticket, Nom5Context.Id(1), values.AsSpan(), 8, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); values.AsSpan().Fill(99); if (outcome == "cancel") cancellation.Cancel();
            Assert.False(publishing.IsCompleted); Assert.True(submitted!.CanRead);
        }
        finally { release.TrySetResult(); }
        if (outcome == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing);
        else if (outcome == "error") Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => publishing)); else await publishing;
        Assert.Equal(expected, observed); Assert.Equal(new[] { 99, 99 }, values); Assert.False(submitted!.CanRead); Assert.Throws<ObjectDisposedException>(() => submitted.ReadByte());
    }

    [Fact]
    public async Task RawCopy_CommittedReplayReturnsOriginalVersion_AndStillEnforcesByteAdmission()
    {
        await using var context = new Nom6Context(); var row = Nom5Context.Asset(1); int[] original = [17, 23]; var expected = Bytes(original); var ticket = await Reserve(context.Store, row, expected);
        var first = await context.Store.PublishRawResourceCopyAsync(ticket, row.Id, original.AsSpan(), 8);
        var replay = await context.Store.PublishRawResourceCopyAsync<long>(ticket, row.Id, new long[] { 999 }, 8);
        Assert.Equal(first.Resources, replay.Resources); Assert.Equal(1, replay.Generation); await NomFaultContext.AssertBytes(context.Store, Assert.Single(replay.Resources), expected);
        Assert.Throws<ArgumentException>(() => { _ = context.Store.PublishRawResourceCopyAsync<long>(ticket, row.Id, new long[] { 999 }, 7); });
    }

    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task RawSingleHelper_RejectsWrongIdOrMultiTicketWithoutPartialPublication_AlsoOnCommittedReplay(bool committed, bool multi)
    {
        await using var context = new Nom6Context(); var a = Nom5Context.Asset(1); var b = Nom5Context.Asset(2); int[] values = [3]; var bytes = Bytes(values);
        var specs = multi ? new[] { NomFaultContext.Spec(a, bytes), NomFaultContext.Spec(b, bytes) } : [NomFaultContext.Spec(a, bytes)];
        var ticket = await context.Store.ReserveResourcesAsync(0, specs);
        if (committed)
        {
            var streams = new Dictionary<Guid, ReadOnlyMemory<byte>> { [a.Id] = bytes }; if (multi) streams[b.Id] = bytes;
            await context.Store.PublishResourcesAsync(ticket, streams);
        }
        using var owner = new NomRawBufferMemory<int>(values); ReadOnlyMemory<int> view = owner.View; ((IDisposable)owner).Dispose();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PublishRawResourceAsync(ticket, multi ? a.Id : b.Id, view));
        Assert.Equal(0, owner.SpanRequests); Assert.Equal(committed ? 1 : 0, context.Store.ContentGeneration);
        Assert.Equal(committed ? specs.Length : 0, (await context.Store.ReadAsync()).Length); Assert.Equal(committed ? "committed" : "aborted", (await context.Store.LookupResourceOperationAsync(ticket)).Status);
    }
}
