namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Immutable identity fence for one required resource view.</summary>
/// <param name="SessionId">Runtime/session identity.</param><param name="WorldId">Borrowed World identity.</param>
/// <param name="ProjectId">Content project.</param><param name="BuildId">Pinned build.</param><param name="CatalogGeneration">Captured CPU catalog generation.</param>
public readonly record struct RenderResourceIdentity(Guid SessionId, Guid WorldId, Guid ProjectId, RenderContentHash BuildId, Guid CatalogGeneration)
{
    /// <summary>Rejects incomplete ownership identity.</summary>
    public void Validate() => ResourceGuard.Require(SessionId != Guid.Empty && WorldId != Guid.Empty && ProjectId != Guid.Empty && !BuildId.IsEmpty &&
        CatalogGeneration != Guid.Empty, RenderResourceError.InvalidIdentity, "Resource session/world/content identity is incomplete.");
}
/// <summary>Explicit device creation arguments. Native activation remains lazy until mesh preparation.</summary>
/// <param name="Identity">Immutable session/world/content fence.</param><param name="DeviceGeneration">Nonzero device generation.</param>
/// <param name="EnableValidation">Request native core and synchronization validation.</param><param name="VerifyUploads">Request diagnostic GPU buffer readback verification.</param>
public sealed record RenderDeviceRequest(RenderResourceIdentity Identity, ulong DeviceGeneration = 1, bool EnableValidation = false, bool VerifyUploads = false);

/// <summary>Non-owning cache-local handle; it is not a native pointer or an Acquire.</summary>
/// <param name="OwnerId">Unique device owner.</param><param name="DeviceGeneration">Exact device generation.</param>
/// <param name="Slot">Local slot.</param><param name="SlotGeneration">Nonzero slot generation, retired rather than wrapped.</param>
public readonly record struct RenderResourceHandle(Guid OwnerId, ulong DeviceGeneration, int Slot, uint SlotGeneration);

/// <summary>Backend owner created through an injected factory. Dispose only after every upload owner released.</summary>
public interface IRenderResourceBackend : IAsyncDisposable
{
    /// <summary>CPU-only factories which return an owner before any native work.</summary>
    IFactory<IRenderMeshUpload, RenderMeshUploadRequest> MeshUploads { get; }
    /// <summary>Whether backend context ownership was fully released.</summary>
    bool IsReleased { get; }
}
/// <summary>Actual mesh allocation/upload owner, including partial creation and unknown submission outcomes.</summary>
public interface IRenderMeshUpload : IAsyncDisposable
{
    /// <summary>Prepares buffers and confirms upload completion.</summary><param name="cancellationToken">Cooperative cancellation; submitted work must still drain.</param><returns>Completion of preparation.</returns>
    Task PrepareAsync(CancellationToken cancellationToken);
    /// <summary>True only after confirmed device upload completion.</summary>
    bool IsReady { get; }
    /// <summary>True only after freeing all actual buffers and reservations.</summary>
    bool IsReleased { get; }
}
/// <summary>Borrowed immutable geometry and mandatory pre-allocation budget.</summary>
/// <param name="Mesh">Exact mesh entry.</param><param name="Budget">Reservations must outlive matching actual allocations.</param>
public sealed record RenderMeshUploadRequest(RenderAssetEntry Mesh, RenderAllocationBudget Budget);

/// <summary>Observed device counters, including retained failed cleanup.</summary>
/// <param name="EntryCount">Live/retiring mesh entries.</param><param name="ActivePreparations">Executing jobs.</param>
/// <param name="QueuedPreparations">Waiting jobs.</param><param name="LivePreparations">Tracked operation owners.</param>
/// <param name="PendingCleanup">Zero-reference retained entries.</param><param name="AdmissionClosed">No further preparation or use.</param>
public sealed record RenderDeviceCounts(int EntryCount, int ActivePreparations, int QueuedPreparations, int LivePreparations, int PendingCleanup, bool AdmissionClosed);

/// <summary>Typed preparation factory input.</summary><param name="Device">Borrowed device owned by the same host.</param>
/// <param name="Content">Live CPU scope.</param><param name="CancellationToken">Operation cancellation.</param>
public sealed record RenderPreparationRequest(RenderDeviceOwner Device, RenderManifestLease Content, CancellationToken CancellationToken = default);
