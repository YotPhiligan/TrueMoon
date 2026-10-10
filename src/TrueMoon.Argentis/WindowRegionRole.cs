namespace TrueMoon.Argentis;

/// <summary>Platform-independent role of an element in a custom window title bar.</summary>
public enum WindowRegionRole
{
    /// <summary>Inherits the ancestor role; focusable controls always remain client input.</summary>
    Inherit,
    /// <summary>Marks a region that can start native window movement when the host enables it.</summary>
    Caption,
    /// <summary>Excludes this subtree from window dragging, including disabled controls.</summary>
    Client,
    /// <summary>Identifies an enabled maximize/restore button for optional native Snap Layouts. Click remains a UI action.</summary>
    Maximize
}
