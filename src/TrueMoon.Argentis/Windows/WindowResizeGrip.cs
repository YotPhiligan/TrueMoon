using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis.Windows;

/// <summary>Non-focusable native resize region over the content's bottom-right corner, independent of edge thickness.</summary>
public sealed class WindowResizeGrip : Element
{
    /// <summary>Creates the bottom-right grip, initially hidden until the host declares sizing support.</summary>
    public WindowResizeGrip() { WindowRegion = WindowRegionRole.ResizeBottomRight; IsVisible = false; }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(16, 16);
}