using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Fixed chrome and a full-body viewport, with anchored content-height islands painted above it.</summary>
public sealed class EditorLayout : ElementList
{
    /// <summary>Top application/document controls.</summary>
    public Element Toolbar { get; }
    /// <summary>Full work-area viewport, drawn below both islands.</summary>
    public Element Viewport { get; }
    /// <summary>Left entity island.</summary>
    public Element Hierarchy { get; }
    /// <summary>Right contextual inspector island.</summary>
    public Element Inspector { get; }
    /// <summary>Bottom status row.</summary>
    public Element Status { get; }
    /// <summary>Adopts distinct fresh UI regions; the caller retains ownership if validation fails.</summary>
    /// <param name="toolbar">Top chrome.</param><param name="hierarchy">Left island.</param><param name="viewport">Scene surface.</param>
    /// <param name="inspector">Right island.</param><param name="status">Bottom chrome.</param>
    public EditorLayout(Element toolbar, Element hierarchy, Element viewport, Element inspector, Element status)
    {
        Element[] children = [toolbar, viewport, hierarchy, inspector, status];
        if (children.Any(x => x == null)) throw new ArgumentNullException(nameof(toolbar), "All shell regions are required.");
        if (children.Distinct().Count() != 5 || children.Any(x => x.Parent != null || x.IsAttached || x.IsDisposed)) throw new ArgumentException("Shell regions must be distinct, live and unowned.");
        Toolbar = toolbar; Hierarchy = hierarchy; Viewport = viewport; Inspector = inspector; Status = status;
        try { foreach (var child in children) Add(child); } catch { Dispose(); throw; }
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Toolbar.Measure(available, text); Status.Measure(available, text);
        var top = Math.Min(Math.Max(52, Toolbar.DesiredSize.Height), available.Height);
        var height = Math.Max(0, available.Height - top - Math.Min(28, Math.Max(0, available.Height - top)));
        Viewport.Measure(new(available.Width, height), text);
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
        var toolbar = Math.Min(Math.Max(52, Toolbar.DesiredSize.Height), area.Height);
        var footer = Math.Min(28, Math.Max(0, area.Height - toolbar));
        Toolbar.Arrange(new(area.X, area.Y, area.Width, toolbar));
        Status.Arrange(new(area.X, area.Y + area.Height - footer, area.Width, footer));
        var height = Math.Max(0, area.Height - toolbar - footer); var y = area.Y + toolbar;
        Viewport.Arrange(new(area.X, y, area.Width, height));
        var inset = Math.Min(8, Math.Min(area.Width, height) / 2);
        var width = Math.Max(0, area.Width - inset * 2); var panelHeight = Math.Max(0, height - inset * 2);
        var gap = Math.Min(12, width); var budget = Math.Max(0, width - (Inspector.IsVisible && Hierarchy.IsVisible ? gap : 0));
        var left = Hierarchy.IsVisible ? Math.Min(264, Inspector.IsVisible ? budget / 2 : budget) : 0;
        var right = Inspector.IsVisible ? Math.Min(336, budget - left) : 0;
        Hierarchy.Arrange(new(area.X + inset, y + inset, left, Math.Min(panelHeight, Hierarchy.DesiredSize.Height)));
        Inspector.Arrange(new(area.X + area.Width - inset - right, y + inset, right, Math.Min(panelHeight, Inspector.DesiredSize.Height)));
    }
    /// <inheritdoc />
    protected override Color ThemeBackground => Theme.Surface;
}
