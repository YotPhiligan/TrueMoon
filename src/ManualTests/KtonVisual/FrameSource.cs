using TrueMoon.Aether.Runtime.Gpu.Surfaces;

namespace TrueMoon.ManualTests.KtonVisual;

// Borrowed session, serialized by the loop. FromScene supports multiple presentation
// samples of the same fixed tick without mutating ECS or inventing simulation ticks.
internal sealed class FrameSource(VisualSession session):IGpuSurfaceFrameSource
{
    internal float Alpha {get;set;}=1;
    internal int ProducedCount {get;private set;}
    public Task<GpuFrameData> CaptureAsync(GpuSurfaceRenderRequest request,CancellationToken token)
    {token.ThrowIfCancellationRequested();return Task.FromResult(GpuFrameData.FromScene(request.Stamp,session.Tick,session.Render(Alpha)));}
    public Task DetachAsync()=>Task.CompletedTask;
    public void Produced(GpuFrameData frame)
    {if(frame.Metadata.Tick!=session.Tick)throw new InvalidDataException("GPU metadata tick mismatch.");ProducedCount++;}
}
