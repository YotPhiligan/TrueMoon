namespace TrueMoon.Argentis;

/// <summary>Owns a title bar and content, optionally painting the title bar over the full client area.</summary>
public sealed class WindowFrame : Element, IUiFrameParticipant
{
    private readonly ScalarTransition _inactive = new();
    private bool _active = true;
    /// <summary>Uses the host's system accent for the normal outline and dims it on focus loss.</summary>
    public static readonly UiProperty<bool> UseSystemAccentBorderProperty = new("UseSystemAccentBorder", false, Invalidation.Render);
    /// <summary>Cached platform accent; null returns the normal theme border.</summary>
    public static readonly UiProperty<Color?> SystemAccentColorProperty = new("SystemAccentColor", null, Invalidation.Render);
    /// <summary>Whether the border follows the system accent and active/inactive state.</summary>
    public bool UseSystemAccentBorder { get => Get(UseSystemAccentBorderProperty); set => Set(UseSystemAccentBorderProperty, value); }
    /// <summary>Host-provided accent, or null when unavailable. Explicit BorderColor styles retain priority.</summary>
    public Color? SystemAccentColor { get => Get(SystemAccentColorProperty); set => Set(SystemAccentColorProperty, value); }
    /// <inheritdoc />
    public bool NeedsFrame => IsAttached && UseSystemAccentBorder && _inactive.IsRunning;
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context) { VerifyAccess(); if (_inactive.Advance(elapsed)) Invalidate(); }
    private readonly IReadOnlyList<Element> _children;
    private readonly IReadOnlyList<Element> _overlayChildren;
    private readonly WindowResizeGrip? _grip;
    /// <summary>Whether content extends beneath the title bar. Default framing reserves its height.</summary>
    public static readonly UiProperty<bool> TitleBarOverContentProperty = new("TitleBarOverContent", false, Invalidation.Layout | Invalidation.Tree);
    /// <summary>Owned title bar, independent of painting order.</summary>
    public Element TitleBar { get; }
    /// <summary>Owned main content, independent of painting order.</summary>
    public Element Content { get; }
    /// <summary>Extends content underneath the title bar, preserving title-bar input priority.</summary>
    public bool TitleBarOverContent { get => Get(TitleBarOverContentProperty); set => Set(TitleBarOverContentProperty, value); }
    /// <summary>Default title bar, or null for caller-supplied title bars.</summary>
    public WindowTitleBar? DefaultTitleBar => TitleBar as WindowTitleBar;
    /// <summary>Visible sizing widget, or null for caller-supplied frames.</summary>
    public WindowResizeGrip? ResizeGrip => _grip;
    /// <summary>Creates the reusable default borderless composition. Window actions are connected by the host.</summary>
    /// <param name="title">Caption.</param><param name="content">Fresh unowned content.</param>
    public static WindowFrame CreateDefault(string title, Element content) => new(new WindowTitleBar(title), content, new WindowResizeGrip());
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => TitleBarOverContent ? _overlayChildren : _children;
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
        TitleBar = titleBar; Content = content;
        _children = Array.AsReadOnly(grip == null ? new[] { titleBar, content } : [titleBar, content, grip]);
        _overlayChildren = Array.AsReadOnly(grip == null ? new[] { content, titleBar } : [content, titleBar, grip]);
        try
        {
            using var change = new TreeChange(this, titleBar, content, grip);
            Adopt(titleBar, change); Adopt(content, change); if (grip != null) Adopt(grip, change); change.Complete();
            AttachmentChanged += FrameAttachment;
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
    protected override Color ThemeBorderColor
    {
        get
        {
            if (!UseSystemAccentBorder) return base.ThemeBorderColor;
            var color = HasError ? Theme.Error : SystemAccentColor ?? Theme.Border;
            var factor = 1 - 0.4f * _inactive.Value;
            return new((byte)MathF.Round(color.R * factor), (byte)MathF.Round(color.G * factor), (byte)MathF.Round(color.B * factor), color.A);
        }
    }
    /// <inheritdoc />
    protected override Color ThemeBackground => _grip == null ? Color.Transparent : Theme.Surface;
    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        base.OnPropertyChanged(property);
        if (ReferenceEquals(property, TitleBarOverContentProperty) && DefaultTitleBar is { } title)
            title.OverlayContent = TitleBarOverContent;
        if (ReferenceEquals(property, UseSystemAccentBorderProperty)) UpdateBorderActivation();
    }
    private void UpdateBorderActivation() => _inactive.MoveTo(_active ? 0 : 1,
        UseSystemAccentBorder && IsAttached ? TimeSpan.FromMilliseconds(160) : TimeSpan.Zero);
    private void FrameAttachment() { if (!IsAttached) _inactive.MoveTo(_active ? 0 : 1, TimeSpan.Zero); }
    /// <summary>Updates active/inactive framing, maximize/restore and sizing availability.</summary>
    /// <param name="state">Observed native presentation/capabilities.</param>
    public void ApplyWindowState(WindowFrameState state)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(state);
        if (_active != state.Active) { _active = state.Active; UpdateBorderActivation(); Invalidate(); }
        DefaultTitleBar?.ApplyState(state);
        if (_grip != null) _grip.IsVisible = state.CanResize && !state.Maximized;
    }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var title = TitleBar; var content = Content;
        title.Measure(available, text);
        _grip?.Measure(available, text);
        content.Measure(new Size(available.Width, Math.Max(0, available.Height - (TitleBarOverContent ? 0 : title.DesiredSize.Height))), text);
        return new(Math.Max(title.DesiredSize.Width, content.DesiredSize.Width), TitleBarOverContent ? Math.Max(title.DesiredSize.Height, content.DesiredSize.Height) : title.DesiredSize.Height + content.DesiredSize.Height);
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var height = Math.Min(area.Height, TitleBar.DesiredSize.Height);
        TitleBar.Arrange(new Rect(area.X, area.Y, area.Width, height));
        var inset = TitleBarOverContent ? 0 : height;
        var contentHeight = Math.Max(0, area.Height - inset);
        Content.Arrange(new Rect(area.X, area.Y + inset, area.Width, contentHeight));
        var gripWidth = Math.Min(area.Width, _grip?.DesiredSize.Width ?? 0);
        var gripHeight = Math.Min(contentHeight, _grip?.DesiredSize.Height ?? 0);
        _grip?.Arrange(new Rect(area.X + area.Width - gripWidth, area.Y + area.Height - gripHeight, gripWidth, gripHeight));
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        if (IsDisposed) return;
        VerifyAccess(); AttachmentChanged -= FrameAttachment; _inactive.Complete(); base.Dispose();
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
