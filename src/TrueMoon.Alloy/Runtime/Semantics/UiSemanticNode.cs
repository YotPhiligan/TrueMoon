using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>Snapshot of a retained element with effective visibility, enablement and geometry.</summary>
/// <param name="ParentIndex">Parent node's position in the snapshot, or null for the root.</param>
/// <param name="Id">Explicit stable target ID. Unnamed elements have no addressable ID.</param>
/// <param name="Role">Role recognized by Alloy.</param>
/// <param name="AccessibleName">Optional accessible label, limited to 256 UTF-16 code units.</param>
/// <param name="Bounds">Arranged bounds in root logical coordinates.</param>
/// <param name="VisibleBounds">Intersection with the viewport and ancestor clips, before sibling overlap.</param>
/// <param name="IsVisible">Visibility including ancestors.</param>
/// <param name="IsEnabled">Enablement including ancestors.</param>
/// <param name="IsFocusable">Whether this element supports keyboard focus.</param>
/// <param name="IsFocused">Whether the session currently routes keyboard input to this element.</param>
/// <param name="HasPointerCapture">Whether the session currently routes captured pointer input here.</param>
/// <param name="IsSelected">Selection or checked state for controls that expose it.</param>
/// <param name="HasError">Validation error state.</param>
/// <param name="IsActivePopup">Whether this is the visible popup of an OverlayHost.</param>
public sealed record UiSemanticNode(int? ParentIndex, string? Id, UiSemanticRole Role, string? AccessibleName,
    Rect Bounds, Rect VisibleBounds, bool IsVisible, bool IsEnabled, bool IsFocusable, bool IsFocused,
    bool HasPointerCapture, bool? IsSelected, bool HasError, bool IsActivePopup);
