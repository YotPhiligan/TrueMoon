using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrueMoon.Nom.Resources;

/// <summary>Explicit runtime-layout byte transfers for unmanaged resource data.</summary>
/// <remarks>These helpers do not serialize values. The consumer defines layout, byte order and padding,
/// including deterministic padding bytes for hashing. Memory remains borrowed until actual task completion.</remarks>
public static class AssetStoreRawBufferExtensions
{
    /// <summary>Publishes the raw bytes of a borrowed typed view using a matching single-resource ticket.</summary>
    /// <typeparam name="T">An unmanaged element whose runtime representation is the resource format.</typeparam>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Store-issued single-resource ticket.</param>
    /// <param name="assetId">The descriptor's logical identifier.</param>
    /// <param name="source">Unchanged memory, valid until the task actually finishes, including error/cancellation.</param>
    /// <param name="token">Publication cancellation; requesting cancellation does not release the borrowed memory.</param>
    /// <returns>The whole-resource publication/cleanup result, using byte lengths and digests.</returns>
    /// <remarks>Checks element count times runtime size before accessing memory. Committed replay never reads source bytes.
    /// The caller's memory owner is never disposed or pinned.</remarks>
    public static Task<AssetWriteReceipt> PublishRawResourceAsync<T>(this IProjectAssetStore store,
        AssetWriteTicket ticket, Guid assetId, ReadOnlyMemory<T> source, CancellationToken token = default) where T : unmanaged
    {
        ValidatePublication(store, ticket, assetId);
        var length = SourceBytes<T>(source.Length); token.ThrowIfCancellationRequested();
        var stream = new ReadOnlyRawMemoryResourceStream<T>(source, length);
        return AssetStoreBufferExtensions.PublishCoreAsync(store, ticket,
            new Dictionary<Guid, Stream> { [assetId] = stream }, assetId, token);
    }

    /// <summary>Publishes a writable typed memory view as borrowed read-only raw bytes, with generic type inference.</summary>
    /// <typeparam name="T">The unmanaged runtime element representation.</typeparam>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Matching single-resource ticket.</param>
    /// <param name="assetId">The descriptor's identifier.</param><param name="source">Memory unchanged until actual task completion.</param>
    /// <param name="token">Publication cancellation.</param><returns>The byte-resource publication result.</returns>
    public static Task<AssetWriteReceipt> PublishRawResourceAsync<T>(this IProjectAssetStore store,
        AssetWriteTicket ticket, Guid assetId, Memory<T> source, CancellationToken token = default) where T : unmanaged =>
        store.PublishRawResourceAsync(ticket, assetId, (ReadOnlyMemory<T>)source, token);

    /// <summary>Copies a typed span's raw bytes synchronously, then publishes the bounded snapshot asynchronously.</summary>
    /// <typeparam name="T">The unmanaged runtime element representation; no serialization or padding normalization occurs.</typeparam>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Matching single-resource ticket.</param>
    /// <param name="assetId">The descriptor's identifier.</param><param name="source">Span copied before returning the task.</param>
    /// <param name="maximumCopyBytes">Positive snapshot ceiling in bytes, at most512MiB, even for an empty span.</param>
    /// <param name="token">Publication cancellation.</param><returns>The asynchronous byte-resource publication result.</returns>
    /// <remarks>Every invocation copies, including committed replay. The caller may reuse its span after this call returns.
    /// Element count times runtime size is checked before projection or allocation.</remarks>
    public static Task<AssetWriteReceipt> PublishRawResourceCopyAsync<T>(this IProjectAssetStore store,
        AssetWriteTicket ticket, Guid assetId, ReadOnlySpan<T> source, int maximumCopyBytes,
        CancellationToken token = default) where T : unmanaged
    {
        ValidatePublication(store, ticket, assetId);
        if (maximumCopyBytes < 1 || maximumCopyBytes > ResourceCodec.MaximumObject || maximumCopyBytes > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(maximumCopyBytes), "Expected a positive byte ceiling of at most512MiB.");
        var length = SourceBytes<T>(source.Length);
        if (length > maximumCopyBytes) throw new ArgumentException("Raw source exceeds the explicit byte copy budget.", nameof(source));
        token.ThrowIfCancellationRequested();
        return store.PublishResourceCopyAsync(ticket, assetId, MemoryMarshal.AsBytes(source), maximumCopyBytes, token);
    }

    /// <summary>Copies a writable span through the read-only raw snapshot path, with generic type inference.</summary>
    /// <typeparam name="T">The unmanaged runtime element representation.</typeparam>
    /// <param name="store">Borrowed project store.</param><param name="ticket">Matching single-resource ticket.</param>
    /// <param name="assetId">The descriptor's identifier.</param><param name="source">Span reusable immediately after this call.</param>
    /// <param name="maximumCopyBytes">Positive ceiling in bytes, at most512MiB.</param>
    /// <param name="token">Publication cancellation.</param><returns>The asynchronous byte-resource publication result.</returns>
    public static Task<AssetWriteReceipt> PublishRawResourceCopyAsync<T>(this IProjectAssetStore store,
        AssetWriteTicket ticket, Guid assetId, Span<T> source, int maximumCopyBytes,
        CancellationToken token = default) where T : unmanaged =>
        store.PublishRawResourceCopyAsync(ticket, assetId, (ReadOnlySpan<T>)source, maximumCopyBytes, token);

    /// <summary>Copies a complete verified byte resource into typed memory and returns the number of elements copied.</summary>
    /// <typeparam name="T">The unmanaged runtime element representation expected by the consumer.</typeparam>
    /// <param name="store">Borrowed project store.</param><param name="exact">Exact version with a byte length divisible by runtime sizeof(T).</param>
    /// <param name="destination">Exclusive writable memory, valid until actual task completion, with room for every element.</param>
    /// <param name="token">Opening/read cancellation.</param><returns>The element count, not the byte count.</returns>
    /// <remarks>Admission occurs before opening or changing the destination. Its unused tail is preserved.
    /// Entered IO failure/cancellation can leave partial bytes, including part of an element; no successful result is returned.
    /// This helper does not verify the consumer's format or normalize endianness/padding. Caller memory is never pinned or disposed.</remarks>
    public static Task<int> ReadRawResourceAsync<T>(this IProjectAssetStore store, AssetResourceVersion exact,
        Memory<T> destination, CancellationToken token = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(exact);
        if (exact.Length < 0 || exact.Length > ResourceCodec.MaximumObject || exact.Length > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(exact), "Resource length exceeds the raw buffer profile.");
        var size = Unsafe.SizeOf<T>();
        if (exact.Length % size != 0) throw new ArgumentException("Resource byte length is not a whole number of raw elements.", nameof(exact));
        var capacity = checked((long)destination.Length * size);
        if (capacity < exact.Length) throw new ArgumentException("Destination cannot hold the complete raw resource.", nameof(destination));
        token.ThrowIfCancellationRequested();
        var elements = checked((int)(exact.Length / size));
        // Select only required elements before AsBytes, so a large unused destination
        // capacity cannot overflow an int-sized byte Span or expose its unused tail.
        return ReadCoreAsync(store, exact, destination[..elements], checked((int)exact.Length), size, token);
    }

    private static async Task<int> ReadCoreAsync<T>(IProjectAssetStore store, AssetResourceVersion exact,
        Memory<T> destination, int byteLength, int elementSize, CancellationToken token) where T : unmanaged
    {
        using var projection = new RawDestinationMemory<T>(destination, byteLength);
        var count = await store.ReadResourceAsync(exact, projection.View, token).ConfigureAwait(false);
        return count / elementSize;
    }

    private static int SourceBytes<T>(int count) where T : unmanaged
    {
        var bytes = checked((long)count * Unsafe.SizeOf<T>());
        if (bytes > ResourceCodec.MaximumObject || bytes > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(count), "Raw resource exceeds512MiB.");
        return checked((int)bytes);
    }

    private static void ValidatePublication(IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(ticket);
        if (assetId == Guid.Empty) throw new ArgumentException("A nonempty asset identifier is required.", nameof(assetId));
    }
}
