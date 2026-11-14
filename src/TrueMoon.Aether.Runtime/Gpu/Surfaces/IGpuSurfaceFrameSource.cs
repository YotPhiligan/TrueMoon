using TrueMoon.Aether.Runtime.Rendering.Resources;

namespace TrueMoon.Aether.Runtime.Gpu.Surfaces;

/// <summary>Explicit surface-local frame source. Its owners must drain before the surface context is released.</summary>
public interface IGpuSurfaceFrameSource
{
    /// <summary>Prepares/captures one frame; the returned owned snapshot reservation transfers to the producer.</summary>
    Task<GpuFrameData> CaptureAsync(GpuSurfaceRenderRequest request, CancellationToken cancellationToken);
    /// <summary>Drains all view/resource owners borrowing the retiring context, including failed preparation.</summary>
    Task DetachAsync();
    /// <summary>Observes actual GPU production completion, before any presentation acknowledgement.</summary>
    void Produced(GpuFrameData frame);
}

/// <summary>Typed surface arguments; the backend factory borrows the exact live surface device and exposes no native handles.</summary>
public sealed record GpuSurfaceRenderRequest(GpuFrameStamp Stamp, int Width, int Height,
    IFactory<IRenderResourceBackend, RenderDeviceRequest> Backends);
