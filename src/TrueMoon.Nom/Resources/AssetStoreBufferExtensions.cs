using System.Buffers;

namespace TrueMoon.Nom.Resources;

/// <summary>Byte-buffer adapters over the store's existing transactional streaming API.</summary>
/// <remarks>These helpers borrow the store. They never dispose a caller's memory owner.
/// ReadOnlyMemory is a view: callers must not mutate or release its backing memory until the returned task finishes.</remarks>
public static class AssetStoreBufferExtensions
{
    /// <summary>Publishes a borrowed byte view using a ticket reserved for exactly one resource.</summary>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Store-issued single-resource ticket.</param>
    /// <param name="assetId">The descriptor's exact logical identifier.</param><param name="source">Unchanged readable memory, valid until actual task completion.</param>
    /// <param name="token">Publication cancellation; requesting cancellation does not end the memory lease.</param>
    /// <returns>The acknowledged publication/cleanup result.</returns>
    /// <remarks>A committed replay does not access source contents. Invalid pending source sets follow the streaming publisher's abort policy.</remarks>
    public static Task<AssetWriteReceipt> PublishResourceAsync(this IProjectAssetStore store, AssetWriteTicket ticket,
        Guid assetId, ReadOnlyMemory<byte> source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(ticket);
        if (assetId == Guid.Empty) throw new ArgumentException("A nonempty asset identifier is required.", nameof(assetId));
        ValidateSourceLength(source.Length); token.ThrowIfCancellationRequested();
        var stream = new ReadOnlyMemoryResourceStream(source);
        return PublishCoreAsync(store, ticket, new Dictionary<Guid, Stream> { [assetId] = stream }, assetId, token);
    }

    /// <summary>Synchronously copies a span into a bounded owned snapshot, then publishes one resource asynchronously.</summary>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Store-issued single-resource ticket.</param>
    /// <param name="assetId">The descriptor's logical identifier.</param><param name="source">Span copied before this method returns its task.</param>
    /// <param name="maximumCopyBytes">Positive explicit snapshot ceiling, at most512MiB.</param><param name="token">Publication cancellation.</param>
    /// <returns>The asynchronous publication result; the caller may reuse the original span immediately after this call.</returns>
    /// <remarks>Every invocation copies the span, including committed-ticket replay. The publisher never rereads its owned snapshot on replay.</remarks>
    public static Task<AssetWriteReceipt> PublishResourceCopyAsync(this IProjectAssetStore store, AssetWriteTicket ticket,
        Guid assetId, ReadOnlySpan<byte> source, int maximumCopyBytes, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(ticket);
        if (assetId == Guid.Empty) throw new ArgumentException("A nonempty asset identifier is required.", nameof(assetId));
        ValidateAllocationLimit(maximumCopyBytes, nameof(maximumCopyBytes)); ValidateSourceLength(source.Length);
        if (source.Length > maximumCopyBytes) throw new ArgumentException("Source exceeds the explicit copy budget.", nameof(source));
        token.ThrowIfCancellationRequested();
        return store.PublishResourceAsync(ticket, assetId, source.ToArray(), token);
    }

    /// <summary>Atomically publishes the complete ticket from borrowed byte views, without copying whole inputs.</summary>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Store-issued resource batch ticket.</param>
    /// <param name="sources">All descriptor IDs and memory views. Descriptors are captured before the first await; underlying bytes remain borrowed.</param>
    /// <param name="token">Publication cancellation.</param><returns>The whole-batch publication result.</returns>
    /// <remarks>At most256views/512MiB per view/1GiB total. Overlapping read-only views are allowed while their underlying data remains unchanged.</remarks>
    public static Task<AssetWriteReceipt> PublishResourcesAsync(this IProjectAssetStore store, AssetWriteTicket ticket,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> sources, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count is < 1 or > 256) throw new ArgumentException("Expected1..256 resource views.", nameof(sources));
        token.ThrowIfCancellationRequested();
        var streams = new Dictionary<Guid, Stream>(); long total = 0;
        try
        {
            foreach (var pair in sources)
            {
                if (streams.Count >= 256 || pair.Key == Guid.Empty || streams.ContainsKey(pair.Key)) throw new ArgumentException("Invalid source identity/count.", nameof(sources));
                ValidateSourceLength(pair.Value.Length); total = checked(total + pair.Value.Length);
                if (total > ResourceCodec.MaximumBatch) throw new ArgumentException("Memory batch exceeds1GiB.", nameof(sources));
                streams.Add(pair.Key, new ReadOnlyMemoryResourceStream(pair.Value));
            }
            if (streams.Count != sources.Count) throw new ArgumentException("Source collection changed during capture.", nameof(sources));
            return PublishCoreAsync(store, ticket, streams, null, token);
        }
        catch { foreach (var stream in streams.Values) stream.Dispose(); throw; }
    }

    /// <summary>Copies a complete verified resource into caller-owned memory without changing the destination's unused tail.</summary>
    /// <param name="store">Borrowed project store.</param><param name="exact">Exact immutable version.</param>
    /// <param name="destination">Exclusive writable memory, valid until actual task completion; must hold the complete resource.</param>
    /// <param name="token">Opening/read cancellation.</param><returns>Number of bytes copied.</returns>
    /// <remarks>Length admission precedes opening or modifying the destination. Entered IO failure/cancellation may leave a partial copy.</remarks>
    public static Task<int> ReadResourceAsync(this IProjectAssetStore store, AssetResourceVersion exact,
        Memory<byte> destination, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store); var length = ValidateExactLength(exact);
        if (destination.Length < length) throw new ArgumentException("Destination cannot hold the complete resource.", nameof(destination));
        token.ThrowIfCancellationRequested();
        return ReadCoreAsync(store, exact, destination[..length], token);
    }

    /// <summary>Loads a verified resource into a detached managed array exposed as a read-only view.</summary>
    /// <param name="store">Borrowed project store.</param><param name="exact">Exact immutable version.</param>
    /// <param name="maximumBytes">Positive explicit allocation ceiling, at most512MiB.</param><param name="token">Opening/read cancellation.</param>
    /// <returns>Independent bytes that remain valid after store/App disposal; empty resources use empty memory.</returns>
    /// <remarks>This is a bounded full allocation, not a mapped/pool lease or an immutability security boundary.</remarks>
    public static Task<ReadOnlyMemory<byte>> LoadResourceAsync(this IProjectAssetStore store, AssetResourceVersion exact,
        int maximumBytes, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(store); var length = ValidateExactLength(exact);
        ValidateAllocationLimit(maximumBytes, nameof(maximumBytes));
        if (length > maximumBytes) throw new ArgumentException("Resource exceeds the explicit allocation budget.", nameof(maximumBytes));
        token.ThrowIfCancellationRequested();
        return LoadCoreAsync(store, exact, length, token);
    }

    internal static async Task<AssetWriteReceipt> PublishCoreAsync(IProjectAssetStore store, AssetWriteTicket ticket,
        Dictionary<Guid, Stream> streams, Guid? singleAsset, CancellationToken token)
    {
        try
        {
            var receipt = await store.PublishResourcesAsync(ticket, streams, token).ConfigureAwait(false);
            if (singleAsset is { } id && (receipt.Resources.Length != 1 || receipt.Resources[0].AssetId != id))
                throw new InvalidDataException("Single-resource helper requires a matching single-resource ticket, including replay.");
            return receipt;
        }
        finally { foreach (var stream in streams.Values) stream.Dispose(); }
    }

    private static async Task<int> ReadCoreAsync(IProjectAssetStore store, AssetResourceVersion exact,
        Memory<byte> destination, CancellationToken token)
    {
        await using var source = await store.OpenResourceAsync(exact, token).ConfigureAwait(false);
        if (destination.IsEmpty) return 0;
        // FileStream may pin its async destination. Use bounded owned IO memory so even
        // non-pinnable caller MemoryManagers only need to expose their writable Span.
        var buffer = ArrayPool<byte>.Shared.Rent(ResourceCodec.BufferBytes);
        try
        {
            var copied = 0;
            while (copied < destination.Length)
            {
                var count = await source.ReadAsync(buffer.AsMemory(0, Math.Min(ResourceCodec.BufferBytes, destination.Length - copied)), token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("Verified resource ended during copy.");
                token.ThrowIfCancellationRequested();
                buffer.AsSpan(0, count).CopyTo(destination.Span[copied..]);
                copied += count;
            }
            return copied;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static async Task<ReadOnlyMemory<byte>> LoadCoreAsync(IProjectAssetStore store, AssetResourceVersion exact,
        int length, CancellationToken token)
    {
        await using var source = await store.OpenResourceAsync(exact, token).ConfigureAwait(false);
        var bytes = length == 0 ? Array.Empty<byte>() : new byte[length];
        await source.ReadExactlyAsync(bytes.AsMemory(), token).ConfigureAwait(false);
        return bytes;
    }

    private static int ValidateExactLength(AssetResourceVersion exact)
    {
        ArgumentNullException.ThrowIfNull(exact);
        if (exact.Length < 0 || exact.Length > ResourceCodec.MaximumObject || exact.Length > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(exact), "Resource length exceeds the buffer profile.");
        return checked((int)exact.Length);
    }
    private static void ValidateSourceLength(int length)
    {
        if (length > ResourceCodec.MaximumObject) throw new ArgumentOutOfRangeException(nameof(length), "Resource view exceeds512MiB.");
    }
    private static void ValidateAllocationLimit(int maximumBytes, string name)
    {
        if (maximumBytes < 1 || maximumBytes > ResourceCodec.MaximumObject || maximumBytes > Array.MaxLength)
            throw new ArgumentOutOfRangeException(name, "Expected a positive allocation ceiling of at most512MiB.");
    }
}
