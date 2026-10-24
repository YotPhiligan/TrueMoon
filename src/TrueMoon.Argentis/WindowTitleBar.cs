namespace TrueMoon.Argentis;

/// <summary>Observed window presentation, supplied by a host adapter; no native handles are retained.</summary>
/// <param name="Active">Whether the native window is focused.</param><param name="Maximized">Whether it is maximized.</param>
/// <param name="CanMinimize">Whether minimize is supported.</param><param name="CanMaximize">Whether maximize/restore is supported.</param>
/// <param name="CanResize">Whether native sizing is supported in the current state.</param>
public sealed record WindowFrameState(bool Active, bool Maximized, bool CanMinimize, bool CanMaximize, bool CanResize);

/// <summary>Default square title bar with caption, optional borrowed icon and standard window actions.</summary>
public sealed class WindowTitleBar : ElementList
{
    private string _title;
    private IImageSource? _icon;
    private bool _stateApplied;
    private Element? _header;
    private WindowFrameState _state = new(true, false, false, false, false);
    /// <summary>Creates the three standard action widgets in a native-caption region.</summary>
    /// <param name="title">Window caption.</param>
    public WindowTitleBar(string title)
    {
        ArgumentNullException.ThrowIfNull(title); _title = title; Height = 36; WindowRegion = WindowRegionRole.Caption;
        MinimizeButton = new(UiSymbol.Minimize, "Minimize window") { BorderWidth = 0 };
        MaximizeButton = new(UiSymbol.Maximize, "Maximize window") { BorderWidth = 0, WindowRegion = WindowRegionRole.Maximize };
        CloseButton = new(UiSymbol.Close, "Close window") { BorderWidth = 0, Tone = ControlTone.Danger };
        Add(MinimizeButton); Add(MaximizeButton); Add(CloseButton); ApplyState(_state);
    }
    /// <summary>Minimize action, disabled until supported by the host.</summary>
    public IconButton MinimizeButton { get; }
    /// <summary>Maximize/restore action, including native Snap region metadata.</summary>
    public IconButton MaximizeButton { get; }
    /// <summary>Close action; a host connects it to its cancellable close coordinator.</summary>
    public IconButton CloseButton { get; }
    /// <summary>Gets optional owned application header content, replacing only the painted caption.</summary>
    public Element? HeaderContent => _header;
    /// <summary>Adopts fresh custom content within the area left of standard window buttons. The caller owns any removed content.</summary>
    /// <param name="header">Fresh unowned header, or null to restore the ordinary caption.</param>
    public void SetHeader(Element? header)
    {
        VerifyAccess(); if (ReferenceEquals(_header, header)) return;
        if (header != null) ValidateChild(header);
        var previous = _header;
        if (previous != null) Items.Remove(previous);
        _header = header;
        if (header != null) Add(header);
        Invalidate(Invalidation.Layout);
    }
    /// <summary>Window caption; changing it invalidates only the drawing.</summary>
    public string Title { get => _title; set { VerifyAccess(); ArgumentNullException.ThrowIfNull(value); if (_title == value) return; _title = value; Invalidate(); } }
    /// <summary>Optional borrowed icon. The caller retains ownership.</summary>
    public IImageSource? Icon { get => _icon; set { VerifyAccess(); if (ReferenceEquals(_icon, value)) return; _icon = value; Invalidate(); } }
    /// <summary>Last observed host presentation.</summary>
    public WindowFrameState State => _state;
    /// <summary>Updates capabilities and maximize/restore symbol without rebuilding the tree.</summary>
    /// <param name="state">Current host state.</param>
    public void ApplyState(WindowFrameState state)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(state);
        if (_stateApplied && _state == state) return;
        _stateApplied = true;
        _state = state; MinimizeButton.IsEnabled = state.CanMinimize; MaximizeButton.IsEnabled = state.CanMaximize;
        MaximizeButton.Symbol = state.Maximized ? UiSymbol.Restore : UiSymbol.Maximize;
        MaximizeButton.AccessibleName = state.Maximized ? "Restore window" : "Maximize window"; Invalidate();
    }
    /// <inheritdoc />
    protected override Color ThemeBackground => _state.Active ? Theme.Control : Theme.Surface;
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    { foreach (var child in Children) child.Measure(available, text); return new(160, 36); }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var size = Math.Min(32, Math.Min(area.Height, area.Width / 3)); var start = area.X + area.Width - size * 3;
        Element[] actions = [MinimizeButton, MaximizeButton, CloseButton];
        for (var i = 0; i < actions.Length; i++) actions[i].Arrange(new(start + size * i, area.Y + (area.Height - size) / 2, size, size));
        _header?.Arrange(new(area.X + 6, area.Y, Math.Max(0, start - area.X - 12), area.Height));
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        var right = MinimizeButton.Bounds.X; var caption = new Rect(Bounds.X + 12, Bounds.Y, Math.Max(0, right - Bounds.X - 24), Bounds.Height);
        context.Save();
        try
        {
            context.Clip(caption); var x = caption.X;
            if (_header == null)
            {
                if (_icon != null) { context.Image(_icon, new(x, Bounds.Y + (Bounds.Height - 16) / 2, 16, 16)); x += 24; }
                context.Text(_title, x, Bounds.Y + (Bounds.Height - FontSize) / 2, _state.Active ? Foreground : Theme.Disabled, FontSize, FontFamily);
            }
        }
        finally { context.Restore(); }
        context.Fill(new(Bounds.X, Bounds.Y + Bounds.Height - 1, Bounds.Width, 1), _state.Active ? Theme.Accent : Theme.Border);
    }
}
