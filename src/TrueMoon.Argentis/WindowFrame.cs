namespace TrueMoon.Argentis;

/// <summary>Places a custom title bar above content, which receives the remaining client area.</summary>
public sealed class WindowFrame : Element
{
    private readonly IReadOnlyList<Element> _children;
    private readonly WindowResizeGrip? _grip;
    /// <summary>Default title bar, or null for caller-supplied title bars.</summary>
    public WindowTitleBar? DefaultTitleBar => Children[0] as WindowTitleBar;
    /// <summary>Visible sizing widget, or null for caller-supplied frames.</summary>
    public WindowResizeGrip? ResizeGrip => _grip;
    /// <summary>Creates the reusable default borderless composition. Window actions are connected by the host.</summary>
    /// <param name="title">Caption.</param><param name="content">Fresh unowned content.</param>
    public static WindowFrame CreateDefault(string title, Element content) => new(new WindowTitleBar(title), content, new WindowResizeGrip());
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Creates a frame and adopts two unowned elements.</summary>
    /// <param name="titleBar">Title bar, normally containing a Caption region and client buttons.</param>
    /// <param name="content">Main window content.</param>
    public WindowFrame(Element titleBar, Element content) : this(titleBar, content, null) { }
    private WindowFrame(Element titleBar, Element content, WindowResizeGrip? grip)
    {
        ArgumentNullException.ThrowIfNull(titleBar); ArgumentNullException.ThrowIfNull(content);
        if (titleBar == content || titleBar.Parent != null || content.Parent != null
            || titleBar.IsAttached || content.IsAttached || titleBar.IsDisposed || content.IsDisposed)
            throw new ArgumentException("Frame children must be distinct, live, unowned elements.");
        ValidateChild(titleBar); ValidateChild(content);
        _grip = grip;
        _children = Array.AsReadOnly(grip == null ? new[] { titleBar, content } : [titleBar, content, grip]);
        try
        {
            using var change = new TreeChange(this, titleBar, content, grip);
            Adopt(titleBar, change); Adopt(content, change); if (grip != null) Adopt(grip, change); change.Complete();
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    /// <inheritdoc />
    protected override Thickness ThemePadding => _grip == null ? default : new(BorderWidth);
    /// <inheritdoc />
    protected override float ThemeBorderWidth => _grip == null ? 0 : Theme.BorderWidth;
    /// <inheritdoc />
    protected override Color ThemeBackground => _grip == null ? Color.Transparent : Theme.Surface;
    /// <summary>Updates active/inactive framing, maximize/restore and sizing availability.</summary>
    /// <param name="state">Observed native presentation/capabilities.</param>
    public void ApplyWindowState(WindowFrameState state)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(state);
        DefaultTitleBar?.ApplyState(state);
        if (_grip != null) _grip.IsVisible = state.CanResize && !state.Maximized;
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var title = Children[0]; var content = Children[1];
        title.Measure(available, text);
        _grip?.Measure(available, text);
        content.Measure(new Size(available.Width, Math.Max(0, available.Height - title.DesiredSize.Height)), text);
        return new(Math.Max(title.DesiredSize.Width, content.DesiredSize.Width), title.DesiredSize.Height + content.DesiredSize.Height);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var height = Math.Min(area.Height, Children[0].DesiredSize.Height);
        Children[0].Arrange(new Rect(area.X, area.Y, area.Width, height));
        var contentHeight = Math.Max(0, area.Height - height);
        Children[1].Arrange(new Rect(area.X, area.Y + height, area.Width, contentHeight));
        var gripWidth = Math.Min(area.Width, _grip?.DesiredSize.Width ?? 0);
        var gripHeight = Math.Min(contentHeight, _grip?.DesiredSize.Height ?? 0);
        _grip?.Arrange(new Rect(area.X + area.Width - gripWidth, area.Y + area.Height - gripHeight, gripWidth, gripHeight));
    }
}

/// <summary>Non-focusable native resize region over the content's bottom-right corner, independent of edge thickness.</summary>
public sealed class WindowResizeGrip : Element
{
    /// <summary>Creates the bottom-right grip, initially hidden until the host declares sizing support.</summary>
    public WindowResizeGrip() { WindowRegion = WindowRegionRole.ResizeBottomRight; IsVisible = false; }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(16, 16);
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => UiSymbols.Draw(context, UiSymbol.ResizeGrip, Bounds.Deflate(new Thickness(2)), Theme.Border);
}
