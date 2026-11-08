using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom;

/// <summary>A caller-disposable store, retained by its App host until actual I/O and cleanup finish.</summary>
/// <remarks>Close read streams before disposing the store. Stop/Dispose timeout keeps catalog/root ownership;
/// after releasing streams, retry cleanup through the store or NomStoreHost. No decoder or GPU lifetime is owned here.</remarks>
public interface IProjectAssetStore : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the canonical local root.</summary>
    string Root { get; }
    /// <summary>Gets persistent project identity.</summary>
    Guid ProjectId { get; }
    /// <summary>Gets persistent store identity.</summary>
    Guid StoreId { get; }
    /// <summary>Gets the most recently observed committed content generation.</summary>
    long ContentGeneration { get; }
    /// <summary>Whether the catalog owner has confirmed closure and released its root lock.</summary>
    bool IsClosed { get; }
    /// <summary>Reads detached metadata, bounded to256items and1MiB; exceeding the response budget fails without partial success.</summary>
    /// <returns>Independent asset records sorted by identifier.</returns>
    Task<AssetRecord[]> ReadAsync();
    /// <summary>Reserves one operation against an exact generation, copying its descriptors.</summary>
    /// <param name="expectedGeneration">Required current generation.</param><param name="specs">Bounded resource descriptors.</param>
    /// <param name="maximumTotalBytes">Positive total byte ceiling, at most1GiB.</param><param name="token">Admission cancellation.</param>
    /// <returns>Durable operation identity; abort unused reservations explicitly or dispose the store.</returns>
    Task<AssetWriteTicket> ReserveResourcesAsync(long expectedGeneration, IReadOnlyList<AssetResourceWrite> specs,
        long maximumTotalBytes = 1024L * 1024 * 1024, CancellationToken token = default);
    /// <summary>Streams all resources and publishes them atomically. Inputs are borrowed and never sought or disposed.</summary>
    /// <param name="ticket">Reserved operation.</param><param name="sources">Distinct streams keyed by logical asset identifier; references captured before first await.</param>
    /// <param name="token">Cancels before commit; committed retry replays the saved outcome without reading replacement inputs.</param>
    /// <returns>Committed receipt; unknown acknowledgement requires close/reopen and journal lookup.</returns>
    Task<AssetWriteReceipt> PublishResourcesAsync(AssetWriteTicket ticket, IReadOnlyDictionary<Guid, Stream> sources, CancellationToken token = default);
    /// <summary>Looks up a retained journal outcome; an expired ticket cannot run again.</summary>
    /// <param name="ticket">Store-issued identity.</param><returns>Detached pending/committed/aborted state.</returns>
    Task<AssetWriteOutcome> LookupResourceOperationAsync(AssetWriteTicket ticket);
    /// <summary>Aborts a pending reservation whose publication has not started.</summary>
    /// <param name="ticket">Unused reservation.</param><returns>Completion after abort and staging cleanup.</returns>
    Task AbortResourceOperationAsync(AssetWriteTicket ticket);
    /// <summary>Explicitly expires retained operation identities up to an inclusive sequence floor.</summary>
    /// <param name="throughSequence">Monotonic floor, less than the next allocated sequence.</param><returns>Committed retention update.</returns>
    Task PruneResourceOperationsAsync(long throughSequence);
    /// <summary>Finds the current exact head of a logical asset.</summary>
    /// <param name="assetId">Logical asset identifier.</param><returns>Exact version or null when no head exists.</returns>
    Task<AssetResourceVersion?> FindResourceAsync(Guid assetId);
    /// <summary>Verifies length/hash on one handle and returns an owned seekable read-only stream at position zero.</summary>
    /// <param name="exact">Complete exact version.</param><param name="token">Opening cancellation.</param>
    /// <returns>A stream holding store/root ownership until actual close, with reads capped at64KiB.</returns>
    Task<Stream> OpenResourceAsync(AssetResourceVersion exact, CancellationToken token = default);
    /// <summary>Checkpoints the catalog and rotates its journal. Close read streams and finish reservations first.</summary>
    /// <param name="token">Cancels before root publication; a selected root remains successful despite later cancellation.</param>
    /// <returns>Unchanged content generation plus root epoch and retryable obsolete-file cleanup state.</returns>
    /// <remarks>Maintenance is explicit. A failed preparation/root switch requires close/reopen; no automatic rollback/reset is performed.</remarks>
    Task<NomCompactionReceipt> CompactAsync(CancellationToken token = default);
    /// <summary>Reads an indexed bounded page. Any catalog change invalidates an existing continuation.</summary>
    /// <param name="query">Intersected exact filters.</param><param name="cursor">Opaque continuation from the same query.</param>
    /// <param name="pageSize">From1to256; the response also has a1MiB ceiling.</param><param name="token">Admission/query cancellation.</param>
    /// <returns>A detached page in Guid order.</returns>
    Task<AssetPage> QueryAssetsAsync(AssetQuery query, AssetPageCursor? cursor = null, int pageSize = 64, CancellationToken token = default);
    /// <summary>Reads exact forward dependencies or exact reverse dependants.</summary>
    /// <param name="exact">A stored exact version.</param><param name="reverse">Returns dependants when true.</param>
    /// <param name="maximum">Positive response ceiling, at most4096.</param><param name="token">Admission cancellation.</param><returns>Detached exact versions in revision order.</returns>
    Task<AssetResourceVersion[]> ReadDependenciesAsync(AssetResourceVersion exact, bool reverse = false, int maximum = 256, CancellationToken token = default);
    /// <summary>Computes a complete bounded exact dependency closure.</summary>
    /// <param name="roots">From1to256 distinct exact versions.</param><param name="maximumMembers">Positive ceiling, at most4096.</param>
    /// <param name="token">Admission cancellation.</param><returns>Detached unique versions sorted by revision.</returns>
    Task<AssetResourceVersion[]> GetDependencyClosureAsync(IReadOnlyList<AssetResourceVersion> roots, int maximumMembers = 4096, CancellationToken token = default);
    /// <summary>Removes heads while retaining stable identity/binding tombstones.</summary>
    /// <param name="expectedGeneration">Exact required generation.</param><param name="assetIds">From1to256 distinct logical identities.</param>
    /// <param name="token">Cancellation before publication.</param><returns>The acknowledged content generation.</returns>
    Task<long> DeleteResourcesAsync(long expectedGeneration, IReadOnlyList<Guid> assetIds, CancellationToken token = default);
    /// <summary>Retains a durable frozen manifest including the exact dependency closure.</summary>
    /// <param name="roots">From1to256 distinct exact versions.</param><param name="maximumMembers">Positive ceiling, at most4096.</param>
    /// <param name="token">Cancellation before publication.</param><returns>The immutable manifest.</returns>
    Task<AssetSnapshotManifest> CreateSnapshotAsync(IReadOnlyList<AssetResourceVersion> roots, int maximumMembers = 4096, CancellationToken token = default);
    /// <summary>Acquires an owned runtime snapshot lease; unknown identifiers throw KeyNotFoundException.</summary>
    /// <param name="id">Canonical manifest digest.</param><param name="token">Admission cancellation.</param><returns>A lease owned by the caller.</returns>
    Task<IAssetSnapshot> OpenSnapshotAsync(string id, CancellationToken token = default);
    /// <summary>Removes an unpinned manifest after all its handles and streams close.</summary>
    /// <param name="id">Canonical manifest digest.</param><param name="token">Cancellation before publication.</param><returns>Completion; absent manifests are harmless.</returns>
    Task RemoveSnapshotAsync(string id, CancellationToken token = default);
    /// <summary>Durably binds a caller-owned pin ID to one retained manifest; identical retries are harmless.</summary>
    /// <param name="snapshotId">Canonical manifest digest.</param><param name="pinId">Nonempty explicit pin identity.</param>
    /// <param name="token">Cancellation before publication.</param><returns>Completion after durable publication.</returns>
    Task PinSnapshotAsync(string snapshotId, Guid pinId, CancellationToken token = default);
    /// <summary>Explicitly releases a durable pin; PID and age never release it.</summary>
    /// <param name="pinId">Nonempty pin identity.</param><param name="token">Cancellation before publication.</param><returns>Completion; absent pins are harmless.</returns>
    Task ReleaseSnapshotPinAsync(Guid pinId, CancellationToken token = default);
    /// <summary>Marks retained roots and inventories canonical CAS files without changing any files.</summary>
    /// <param name="token">Admission/mark cancellation.</param><returns>A bounded plan requiring unchanged metadata and object inventory.</returns>
    Task<NomGarbageCollectionPlan> PlanGarbageCollectionAsync(CancellationToken token = default);
    /// <summary>Atomically prunes unreachable revisions and records deletion intents, then resumes object cleanup.</summary>
    /// <param name="plan">A current dry-run plan; null resumes existing intents without discovering new garbage.</param>
    /// <param name="token">Cancels before prune; after prune a pending receipt describes unfinished cleanup.</param>
    /// <returns>Acknowledged prune/delete counts and durable pending state.</returns>
    Task<NomGarbageCollectionReceipt> CollectGarbageAsync(NomGarbageCollectionPlan? plan = null, CancellationToken token = default);
    /// <summary>Exports selected metadata and all stored revision bytes to a verified immutable backup directory.</summary>
    /// <param name="destinationRoot">Absent fully qualified local directory with an existing parent, separate from this root.</param>
    /// <param name="options">Optional copied transfer admission limits.</param><param name="token">Cancellation before directory publication.</param>
    /// <returns>An acknowledged manifest identity and transfer size.</returns>
    /// <remarks>Finish reservations, close read streams and resume pending GC first. Failed prepared directories are retained for diagnosis.
    /// Restore through the App-registered IFactory with RestoreAssetStoreArguments. No in-place migration or overwrite is performed.</remarks>
    Task<NomBackupReceipt> ExportBackupAsync(string destinationRoot, NomBackupOptions? options = null, CancellationToken token = default);
}
