using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Preview;

/// <summary>Actual-presentation receipt, retaining only immutable CPU data and a surface ticket, never native handles.</summary>
public sealed class PreviewPresentedView
{
    internal PreviewPresentedView(ViewportFrame frame, PreviewViewState state, CameraProjection projection, PreviewPickSnapshot picks,
        ViewportImageBounds image, ViewportImageBounds clip, ulong layoutRevision)
    { Frame = frame; State = state; Projection = projection; Picks = picks; ImageBounds = image; ClipBounds = clip; LayoutRevision = layoutRevision; }
    /// <summary>Gets the actual acknowledged ticket, including exact runtime/surface/frame/slot.</summary>
    public ViewportFrame Frame { get; }
    /// <summary>Gets camera/opening/content values captured at production.</summary>
    public PreviewViewState State { get; }
    /// <summary>Gets frozen projection and actual physical extent.</summary>
    public CameraProjection Projection { get; }
    /// <summary>Gets strongly retained exact projected pick geometry.</summary>
    public PreviewPickSnapshot Picks { get; }
    /// <summary>Gets the actually drawn logical image bounds.</summary>
    public ViewportImageBounds ImageBounds { get; }
    /// <summary>Gets the frozen effective rectangular clip.</summary>
    public ViewportImageBounds ClipBounds { get; }
    /// <summary>Gets the UI binding's positive layout revision.</summary>
    public ulong LayoutRevision { get; }
}
