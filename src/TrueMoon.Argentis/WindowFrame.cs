namespace TrueMoon.Argentis;

/// <summary>Places a custom title bar above content, which receives the remaining client area.</summary>
public sealed class WindowFrame : Element
{
    private readonly IReadOnlyList<Element> _children;
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Creates a frame and adopts two unowned elements.</summary>
    /// <param name="titleBar">Title bar, normally containing a Caption region and client buttons.</param>
    /// <param name="content">Main window content.</param>
    public WindowFrame(Element titleBar, Element content)
    {
        ArgumentNullException.ThrowIfNull(titleBar); ArgumentNullException.ThrowIfNull(content);
        if (titleBar == content || titleBar.Parent != null || content.Parent != null
            || titleBar.IsAttached || content.IsAttached || titleBar.IsDisposed || content.IsDisposed)
            throw new ArgumentException("Frame children must be distinct, live, unowned elements.");
        ValidateChild(titleBar); ValidateChild(content);
        _children = Array.AsReadOnly(new[] { titleBar, content });
        try
        {
            using var change = new TreeChange(this, titleBar, content);
            Adopt(titleBar, change); Adopt(content, change); change.Complete();
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var title = Children[0]; var content = Children[1];
        title.Measure(available, text);
        content.Measure(new Size(available.Width, Math.Max(0, available.Height - title.DesiredSize.Height)), text);
        return new(Math.Max(title.DesiredSize.Width, content.DesiredSize.Width), title.DesiredSize.Height + content.DesiredSize.Height);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var height = Math.Min(area.Height, Children[0].DesiredSize.Height);
        Children[0].Arrange(new Rect(area.X, area.Y, area.Width, height));
        Children[1].Arrange(new Rect(area.X, area.Y + height, area.Width, Math.Max(0, area.Height - height)));
    }
}
