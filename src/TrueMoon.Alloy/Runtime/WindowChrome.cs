using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

/// <summary>Immutable settings for a custom frame on an undecorated Windows window.</summary>
public sealed record WindowChromeOptions
{
    /// <summary>Opt-in system move loop for Caption regions. Defaults to false.</summary>
    public bool NativeDrag { get; init; }
    /// <summary>Opt-in Windows 11 Snap Layouts hit testing for Maximize regions; requires Resizable.</summary>
    public bool NativeSnapLayouts { get; init; }
    /// <summary>Enables the system menu through Alt+Space, caption right-click and the optional menu service.</summary>
    public bool SystemMenu { get; init; } = true;
    /// <summary>Whether invisible native resize edges and maximize are enabled.</summary>
    public bool Resizable { get; init; } = true;
    /// <summary>Resize edge thickness in 96-DPI pixels, scaled by the window's current native DPI.</summary>
    public float ResizeBorder { get; init; } = 6;
    /// <summary>Minimum client size in the same logical units as Argentis layout.</summary>
    public Size MinimumSize { get; init; } = new(160, 100);
    /// <summary>Maximum client size; positive infinity means no application limit.</summary>
    public Size MaximumSize { get; init; } = new(float.PositiveInfinity, float.PositiveInfinity);
    /// <summary>Validates sizes and border before native allocation.</summary>
    public void Validate()
    {
        if (NativeSnapLayouts && !Resizable) throw new ArgumentException("Native Snap Layouts require a resizable window.", nameof(NativeSnapLayouts));
        if (!float.IsFinite(ResizeBorder) || ResizeBorder < 0 || ResizeBorder > 64)
            throw new ArgumentOutOfRangeException(nameof(ResizeBorder));
        if (!float.IsFinite(MinimumSize.Width) || !float.IsFinite(MinimumSize.Height)
            || MinimumSize.Width <= 0 || MinimumSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumSize));
        if (float.IsNaN(MaximumSize.Width) || float.IsNaN(MaximumSize.Height)
            || MaximumSize.Width < MinimumSize.Width || MaximumSize.Height < MinimumSize.Height)
            throw new ArgumentOutOfRangeException(nameof(MaximumSize));
    }
    /// <summary>Rejects a requested client size outside the configured limits.</summary>
    /// <param name="size">Client size in logical units.</param>
    public void ValidateSize(Size size)
    {
        if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height)
            || size.Width < MinimumSize.Width || size.Height < MinimumSize.Height
            || size.Width > MaximumSize.Width || size.Height > MaximumSize.Height)
            throw new ArgumentOutOfRangeException(nameof(size));
    }
}

/// <summary>State of a standalone window, independent of Silk and Win32.</summary>
public enum UiWindowState
{
    /// <summary>Normal restored window.</summary>
    Normal,
    /// <summary>Minimized window.</summary>
    Minimized,
    /// <summary>Maximized window.</summary>
    Maximized
}

/// <summary>Optional owner-thread window commands, independent of native window types.</summary>
public interface IWindowCommands : IWindowHost
{
    /// <summary>Current native state.</summary>
    UiWindowState State { get; }
    /// <summary>Minimizes the window.</summary>
    void Minimize();
    /// <summary>Maximizes a resizable window.</summary>
    void Maximize();
    /// <summary>Restores a minimized or maximized window.</summary>
    void Restore();
}

/// <summary>Capabilities of the installed custom frame adapter, separate from appearance and graphics support.</summary>
/// <param name="CustomFrame">Whether full-client custom framing is available on this window.</param>
/// <param name="NativeMove">Whether the adapter can delegate Caption dragging to the operating system.</param>
/// <param name="NativeResize">Whether the adapter can delegate resize borders to the operating system.</param>
public sealed record WindowChromeCapabilities(bool CustomFrame, bool NativeMove, bool NativeResize)
{
    /// <summary>Platform support for native Snap Layouts, independent of Windows user settings.</summary>
    public bool SnapLayouts { get; init; }
    /// <summary>Whether the custom frame provides a native system menu.</summary>
    public bool SystemMenu { get; init; }
}

/// <summary>Optional system menu service on the window owner thread.</summary>
public interface IWindowSystemMenu : IWindowCommands
{
    /// <summary>Opens the native system menu at a client point and dispatches the selected system command.</summary>
    /// <param name="x">Horizontal logical client coordinate.</param>
    /// <param name="y">Vertical logical client coordinate.</param>
    void ShowSystemMenu(float x, float y);
}

/// <summary>Optional custom-frame geometry and capabilities on the window owner thread.</summary>
public interface IWindowChromeHost : IWindowCommands
{
    /// <summary>Immutable requested frame settings; null preserves the original platform behavior.</summary>
    WindowChromeOptions? Chrome { get; }
    /// <summary>Native adapter support, independent of whether move or resize is enabled in options.</summary>
    WindowChromeCapabilities ChromeCapabilities { get; }
    /// <summary>Publishes a geometry snapshot after arranging the UI.</summary>
    /// <param name="regions">Immutable title-bar regions in logical client coordinates.</param>
    void SetWindowRegions(WindowRegionMap regions);
}

/// <summary>Copied title-bar geometry. It holds no elements, delegates, or session references.</summary>
public sealed class WindowRegionMap
{
    private readonly (Rect Bounds, WindowRegionRole Role)[] _regions;
    private WindowRegionMap((Rect, WindowRegionRole)[] regions) => _regions = regions;
    /// <summary>An empty map leaves all input in the client area.</summary>
    public static WindowRegionMap Empty { get; } = new([]);
    /// <summary>Copies arranged geometry in paint order, including clipping and interactive exclusions.</summary>
    /// <param name="root">A live arranged tree on its owner thread.</param>
    /// <returns>An immutable snapshot, unaffected by subsequent tree changes.</returns>
    public static WindowRegionMap Create(Element root)
    {
        ArgumentNullException.ThrowIfNull(root); root.VerifyAccess();
        var regions = new List<(Rect, WindowRegionRole)>();
        Visit(root, root.Bounds, WindowRegionRole.Client, false, regions);
        return new(regions.ToArray());
    }
    private static void Visit(Element element, Rect clip, WindowRegionRole inherited, bool excluded,
        List<(Rect, WindowRegionRole)> regions)
    {
        if (element.IsDisposed || !element.IsVisible) return;
        var bounds = Intersect(clip, element.Bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        excluded |= element.WindowRegion == WindowRegionRole.Client
            || element.Focusable && element.WindowRegion != WindowRegionRole.Maximize;
        var role = excluded ? WindowRegionRole.Client
            : element.WindowRegion == WindowRegionRole.Inherit ? inherited : element.WindowRegion;
        if (role == WindowRegionRole.Maximize && !element.IsEffectivelyEnabled)
        { role = WindowRegionRole.Client; excluded = true; }
        regions.Add((bounds, role));
        var childClip = Intersect(bounds, element.ChildClipBounds);
        foreach (var child in element.Children) Visit(child, childClip, role, excluded, regions);
    }
    private static Rect Intersect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X); var y = Math.Max(a.Y, b.Y);
        return new(x, y, Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - x),
            Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - y));
    }
    /// <summary>Resolves a point in root logical coordinates; topmost geometry wins.</summary>
    /// <param name="x">Horizontal root coordinate.</param>
    /// <param name="y">Vertical root coordinate.</param>
    /// <returns>Caption, Maximize or Client; points outside the snapshot remain client input.</returns>
    public WindowRegionRole HitTest(float x, float y)
    {
        for (var i = _regions.Length - 1; i >= 0; i--)
            if (_regions[i].Bounds.Contains(x, y)) return _regions[i].Role;
        return WindowRegionRole.Client;
    }
}
