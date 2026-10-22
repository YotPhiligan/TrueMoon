using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Fixed tool/status rows and proportional scene/viewport/inspector columns; no native handles.</summary>
public sealed class EditorLayout : ElementList
{
    public EditorLayout(Element toolbar, Element hierarchy, Element viewport, Element inspector, Element status)
    {
        Element[] children = [toolbar, hierarchy, viewport, inspector, status];
        if (children.Any(x => x == null)) throw new ArgumentNullException(nameof(toolbar), "All shell regions are required.");
        if (children.Distinct().Count() != 5 || children.Any(x => x.Parent != null || x.IsAttached || x.IsDisposed)) throw new ArgumentException("Shell regions must be distinct, live and unowned.");
        try { foreach (var child in children) Add(child); } catch { Dispose(); throw; }
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { foreach (var child in Children) child.Measure(available, text); return new(1100, 680); }
    protected override void ArrangeCore(Rect area)
    {
        var toolbar = Math.Min(52, area.Height); var footer = Math.Min(30, Math.Max(0, area.Height - toolbar));
        Children[0].Arrange(new(area.X, area.Y, area.Width, toolbar));
        Children[4].Arrange(new(area.X, area.Y + area.Height - footer, area.Width, footer));
        var height = Math.Max(0, area.Height - toolbar - footer); var gap = Math.Min(6, area.Width / 6);
        var width = Math.Max(0, area.Width - gap * 2); var left = Math.Min(224, width * .2f); var right = Math.Min(292, width * .25f);
        var middle = Math.Max(0, width - left - right); var y = area.Y + toolbar;
        Children[1].Arrange(new(area.X, y, left, height)); Children[2].Arrange(new(area.X + left + gap, y, middle, height));
        Children[3].Arrange(new(area.X + left + gap * 2 + middle, y, right, height));
    }
    protected override Color ThemeBackground => Theme.Surface;
}
