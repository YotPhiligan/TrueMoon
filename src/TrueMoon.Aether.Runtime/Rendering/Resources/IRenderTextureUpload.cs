using TrueMoon.Aether.Abstractions.Rendering;

namespace TrueMoon.Aether.Runtime.Rendering.Resources;

internal interface IRenderTextureUpload : IAsyncDisposable
{
    bool IsReady { get; }
    bool IsReleased { get; }
    Task PrepareAsync(CancellationToken cancellationToken);
}

internal sealed record RenderTextureUploadRequest(RenderAssetEntry Texture, RenderAllocationBudget Budget);
