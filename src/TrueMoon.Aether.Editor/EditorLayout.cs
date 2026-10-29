using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>A scene surface with anchored islands and optional chrome painted over the full client area.</summary>
public sealed class EditorLayout : ElementList
{
    /// <summary>Standalone toolbar, or contextual document tools in overlay mode.</summary>
    public Element Toolbar { get; }
    /// <summary>Scene surface painted below chrome and islands.</summary>
    public Element Viewport { get; }
    /// <summary>Left entity island.</summary>
    public Element Hierarchy { get; }
    /// <summary>Right contextual inspector island.</summary>
    public Element Inspector { get; }
    /// <summary>Bottom status row.</summary>
    public Element Status { get; }
    /// <summary>Optional bottom console, overlaying the scene above the status row.</summary>
    public Element? Diagnostics { get; }
    /// <summary>Whether the viewport extends behind window chrome and the status row.</summary>
    public bool OverlayChrome { get; }
    /// <summary>Logical title-bar space kept clear of tools and islands, without reducing the viewport.</summary>
    public float ChromeInset { get; }
    /// <summary>Adopts distinct fresh regions. Default composition retains standalone toolbar layout.</summary>
    /// <param name="toolbar">Toolbar or retained contextual tools.</param><param name="hierarchy">Left island.</param>
    /// <param name="viewport">Scene surface.</param><param name="inspector">Right island.</param><param name="status">Footer.</param>
    /// <param name="overlayChrome">Whether chrome overlays a full-area scene.</param><param name="chromeInset">Logical title-bar height in overlay mode.</param>
    public EditorLayout(Element toolbar, Element hierarchy, Element viewport, Element inspector, Element status, bool overlayChrome = false, float chromeInset = 36)
        : this(toolbar, hierarchy, viewport, inspector, status, null, overlayChrome, chromeInset) { }

    /// <summary>Adopts the existing regions and an optional bottom diagnostics overlay.</summary>
    public EditorLayout(Element toolbar, Element hierarchy, Element viewport, Element inspector, Element status, Element? diagnostics, bool overlayChrome = false, float chromeInset = 36)
    {
        if (!float.IsFinite(chromeInset) || chromeInset < 0) throw new ArgumentOutOfRangeException(nameof(chromeInset));
        Element[] children = overlayChrome ? [viewport, hierarchy, inspector, status, toolbar] : [toolbar, viewport, hierarchy, inspector, status];
        if (children.Any(x => x == null)) throw new ArgumentNullException(nameof(toolbar), "All shell regions are required.");
        if (diagnostics != null) children = [.. children, diagnostics];
        if (children.Distinct().Count() != children.Length || children.Any(x => x.Parent != null || x.IsAttached || x.IsDisposed)) throw new ArgumentException("Shell regions must be distinct, live and unowned.");
        Toolbar = toolbar; Hierarchy = hierarchy; Viewport = viewport; Inspector = inspector; Status = status;
        OverlayChrome = overlayChrome; ChromeInset = chromeInset;
        Diagnostics = diagnostics;
        try { foreach (var child in children) Add(child); } catch { Dispose(); throw; }
    }
    private float Top(float height) => Math.Min(OverlayChrome ? ChromeInset : Math.Max(52, Toolbar.DesiredSize.Height), height);
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Toolbar.Measure(OverlayChrome ? new(Math.Min(640, available.Width), Math.Max(0, available.Height - ChromeInset - 44)) : available, text);
        Status.Measure(available, text);
        var top = Top(available.Height); var footer = Math.Min(28, Math.Max(0, available.Height - top));
        var height = Math.Max(0, available.Height - top - footer);
        Diagnostics?.Measure(new(available.Width, ConsoleHeight(height)), text);
        height -= ConsoleHeight(height);
        Viewport.Measure(OverlayChrome ? available : new(available.Width, height), text);
        var inset = Math.Min(8, Math.Min(available.Width, height) / 2);
        var width = Math.Max(0, available.Width - inset * 2); var panelHeight = Math.Max(0, height - inset * 2);
        var gap = Math.Min(12, width); var budget = Math.Max(0, width - (Inspector.IsVisible && Hierarchy.IsVisible ? gap : 0));
        var left = Hierarchy.IsVisible ? Math.Min(264, Inspector.IsVisible ? budget / 2 : budget) : 0;
        var right = Inspector.IsVisible ? Math.Min(336, budget - left) : 0;
        Hierarchy.Measure(new(left, panelHeight), text); Inspector.Measure(new(right, panelHeight), text);
        return new(1100, 680);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var top = Top(area.Height); var footer = Math.Min(28, Math.Max(0, area.Height - top));
        Status.Arrange(new(area.X, area.Y + area.Height - footer, area.Width, footer));
        var height = Math.Max(0, area.Height - top - footer); var y = area.Y + top;
        var console = ConsoleHeight(height);
        Diagnostics?.Arrange(new(area.X, area.Y + area.Height - footer - console, area.Width, console));
        height -= console;
        Viewport.Arrange(OverlayChrome ? area : new(area.X, y, area.Width, height));
        var inset = Math.Min(8, Math.Min(area.Width, height) / 2);
        var width = Math.Max(0, area.Width - inset * 2); var panelHeight = Math.Max(0, height - inset * 2);
        var gap = Math.Min(12, width); var budget = Math.Max(0, width - (Inspector.IsVisible && Hierarchy.IsVisible ? gap : 0));
        var left = Hierarchy.IsVisible ? Math.Min(264, Inspector.IsVisible ? budget / 2 : budget) : 0;
        var right = Inspector.IsVisible ? Math.Min(336, budget - left) : 0;
        Hierarchy.Arrange(new(area.X + inset, y + inset, left, Math.Min(panelHeight, Hierarchy.DesiredSize.Height)));
        Inspector.Arrange(new(area.X + area.Width - inset - right, y + inset, right, Math.Min(panelHeight, Inspector.DesiredSize.Height)));
        if (OverlayChrome)
        {
            var toolsWidth = Math.Min(640, Math.Max(0, area.Width - inset * 2));
            Toolbar.Arrange(new(area.X + (area.Width - toolsWidth) / 2, y + inset, toolsWidth, Math.Min(panelHeight, Toolbar.DesiredSize.Height)));
        }
        else Toolbar.Arrange(new(area.X, area.Y, area.Width, top));
    }
    /// <inheritdoc />
    protected override Color ThemeBackground => Theme.Surface;
    private float ConsoleHeight(float available) => Diagnostics is { IsVisible: true } ? Math.Min(360, available * .6f) : 0;
}
