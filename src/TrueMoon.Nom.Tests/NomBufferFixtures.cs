using System.Buffers;
using System.Reflection;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

internal sealed class NomBufferMemory(byte[] bytes, int? logicalLength = null) : MemoryManager<byte>
{
    internal int SpanRequests, PinRequests, DisposeCalls;
    internal bool IsDisposed;
    internal NomFaultGate? Gate;
    internal Memory<byte> View => CreateMemory(logicalLength ?? bytes.Length);
    internal byte[] Bytes => bytes;
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this); Interlocked.Increment(ref SpanRequests);
        if (Gate is { } gate)
        {
            gate.Entered.TrySetResult();
            if (!gate.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Buffer owner test gate.");
        }
        return bytes;
    }
    public override MemoryHandle Pin(int elementIndex = 0) { PinRequests++; throw new NotSupportedException("This owner cannot pin or expose an array."); }
    public override void Unpin() => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { DisposeCalls++; IsDisposed = true; }
}

// Minimal public-interface seam for admission and entered-read behavior; real publication tests use App-backed stores.
public class NomBufferStoreProxy : DispatchProxy
{
    internal int Opens, Publishes;
    internal Func<AssetResourceVersion, CancellationToken, Task<Stream>> Open = (_, _) => throw new InvalidOperationException("Unexpected resource open.");
    internal Func<AssetWriteTicket, IReadOnlyDictionary<Guid, Stream>, CancellationToken, Task<AssetWriteReceipt>> Publish = (_, _, _) => throw new InvalidOperationException("Unexpected publication.");
    internal static (IProjectAssetStore Store, NomBufferStoreProxy Control) Create()
    {
        var store = DispatchProxy.Create<IProjectAssetStore, NomBufferStoreProxy>();
        return (store, (NomBufferStoreProxy)store);
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IProjectAssetStore.OpenResourceAsync))
        { Opens++; return Open((AssetResourceVersion)args![0]!, (CancellationToken)args[1]!); }
        if (targetMethod?.Name == nameof(IProjectAssetStore.PublishResourcesAsync))
        { Publishes++; return Publish((AssetWriteTicket)args![0]!, (IReadOnlyDictionary<Guid, Stream>)args[1]!, (CancellationToken)args[2]!); }
        throw new NotSupportedException(targetMethod?.Name);
    }
}

internal sealed class NomBufferPartialReadStream(byte[] bytes, Exception? failure = null, CancellationTokenSource? cancellation = null) : ShortSource(bytes)
{
    private int calls;
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (calls++ == 0) return base.ReadAsync(buffer, cancellationToken);
        cancellation?.Cancel();
        return ValueTask.FromException<int>(failure ?? new OperationCanceledException(cancellationToken));
    }
}

internal sealed class NomBufferCloseGateStream(byte[] bytes, Exception? failure = null) : ShortSource(bytes)
{
    internal readonly TaskCompletionSource Closing = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int calls;
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        calls++ > 0 && failure != null ? ValueTask.FromException<int>(failure) : base.ReadAsync(buffer, cancellationToken);
    public override async ValueTask DisposeAsync()
    {
        Closing.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(5)); Dispose();
    }
}

internal sealed class NomBufferSourceSet(int count, params KeyValuePair<Guid, ReadOnlyMemory<byte>>[] entries) : IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>
{
    public int Count => count;
    public IEnumerable<Guid> Keys => entries.Select(e => e.Key);
    public IEnumerable<ReadOnlyMemory<byte>> Values => entries.Select(e => e.Value);
    public ReadOnlyMemory<byte> this[Guid key] => entries.First(e => e.Key == key).Value;
    public bool ContainsKey(Guid key) => entries.Any(e => e.Key == key);
    public bool TryGetValue(Guid key, out ReadOnlyMemory<byte> value)
    { foreach (var e in entries) if (e.Key == key) { value = e.Value; return true; } value = default; return false; }
    public IEnumerator<KeyValuePair<Guid, ReadOnlyMemory<byte>>> GetEnumerator() => ((IEnumerable<KeyValuePair<Guid, ReadOnlyMemory<byte>>>)entries).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class NomBufferFixtures
{
    internal static AssetResourceVersion Exact(long length) => new(Nom5Context.Id(1), Nom5Context.Id(2), new string('a', 64), new string('b', 64), length);
    internal static AssetWriteTicket Ticket => new(Nom5Context.Id(3), 1, new string('c', 64));
    internal static byte[] Payload(int length) => Enumerable.Range(0, length).Select(i => (byte)(i * 31 + 7)).ToArray();
    internal static Task<AssetWriteTicket> Reserve(IProjectAssetStore store, AssetRecord asset, byte[] bytes) =>
        store.ReserveResourcesAsync(store.ContentGeneration, [NomFaultContext.Spec(asset, bytes)]);
}
