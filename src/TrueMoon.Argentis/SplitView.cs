namespace TrueMoon.Argentis;

/// <summary>Two retained pane hosts separated by a keyboard and pointer operated divider.</summary>
/// <remarks>Owns its hosts and their content. Sizes are UI state; persistence and collapse belong to the consumer.</remarks>
public sealed class SplitView : Element
{
    /// <summary>Placement and resizing axis.</summary>
    public static readonly UiProperty<SplitOrientation> OrientationProperty = new("Orientation", SplitOrientation.Horizontal, Invalidation.Layout, Enum.IsDefined);
    /// <summary>Preferred first pane size in logical pixels; NaN selects half the usable space.</summary>
    public static readonly UiProperty<float> FirstPaneSizeProperty = new("FirstPaneSize", float.NaN, Invalidation.Layout, UiProperties.Dimension);
    /// <summary>Divider hit area in logical pixels, greater than zero.</summary>
    public static readonly UiProperty<float> DividerThicknessProperty = new("DividerThickness", 8, Invalidation.Layout, v => float.IsFinite(v) && v > 0);
    /// <summary>Arrow-key increment in logical pixels; Shift multiplies it by ten.</summary>
    public static readonly UiProperty<float> KeyboardStepProperty = new("KeyboardStep", 8, validate: v => float.IsFinite(v) && v > 0);

    private readonly ElementCollection _children;
    private readonly DividerElement _divider;
    private Rect _area;
    private float _lower, _upper;
    private bool _writing;
    private float _writtenSize;

    /// <summary>Retained first pane. Set content here and use its axis Min/Max dimensions as constraints.</summary>
    public ContentControl FirstPane { get; } = new();
    /// <summary>Retained second pane. Replacing content transfers the old subtree back to the caller.</summary>
    public ContentControl SecondPane { get; } = new();
    /// <summary>Focusable divider, exposed for accessible naming and local/typed styling.</summary>
    public Element Divider => _divider;
    /// <inheritdoc />
    public override IReadOnlyList<Element> Children => _children;
    /// <summary>Pane placement and resizing axis.</summary>
    public SplitOrientation Orientation { get => Get(OrientationProperty); set => Set(OrientationProperty, value); }
    /// <summary>Preferred first pane size. Layout clamps its actual size without changing this preference.</summary>
    public float FirstPaneSize { get => Get(FirstPaneSizeProperty); set => Set(FirstPaneSizeProperty, value); }
    /// <summary>Divider hit area in logical pixels.</summary>
    public float DividerThickness { get => Get(DividerThicknessProperty); set => Set(DividerThicknessProperty, value); }
    /// <summary>Base keyboard resizing step.</summary>
    public float KeyboardStep { get => Get(KeyboardStepProperty); set => Set(KeyboardStepProperty, value); }
    /// <summary>First pane allocation after constraints, excluding the divider.</summary>
    public float ActualFirstPaneSize { get; private set; }
    /// <summary>Whether the divider currently owns a pointer resize gesture.</summary>
    public bool IsResizing => _divider.IsResizing;

    /// <summary>Creates empty, owned pane hosts and their divider.</summary>
    public SplitView()
    {
        _divider = new(this) { AccessibleName = "Resize panels" };
        _children = new(this);
        _children.Add(FirstPane); _children.Add(_divider); _children.Add(SecondPane);
    }

    /// <summary>Creates a split view owning two distinct, live, unowned subtrees.</summary>
    /// <param name="first">First pane content.</param><param name="second">Second pane content.</param>
    public SplitView(Element first, Element second) : this()
    {
        ValidateChild(first); ValidateChild(second);
        if (ReferenceEquals(first, second)) throw new ArgumentException("Pane content must be distinct.", nameof(second));
        FirstPane.SetContent(first); SecondPane.SetContent(second);
    }

    /// <summary>Creates empty panes for Fluent configuration.</summary><returns>A new split view.</returns>
    public static SplitView Create() => new();

    private bool Horizontal => Orientation == SplitOrientation.Horizontal;
    private float Axis(Size size) => Horizontal ? size.Width : size.Height;
    private float Axis(Rect area) => Horizontal ? area.Width : area.Height;
    private float Coordinate(UiInput input) => Horizontal ? input.X : input.Y;
    private Size Sized(float axis, float cross) => Horizontal ? new(axis, cross) : new(cross, axis);
    private Rect Slot(Rect area, float offset, float length) => Horizontal
        ? new(area.X + offset, area.Y, length, area.Height) : new(area.X, area.Y + offset, area.Width, length);

    private (float First, float Second, float Divider, float Lower, float Upper) Allocate(float available)
    {
        var divider = Math.Min(available, DividerThickness);
        var space = Math.Max(0, available - divider);
        var min1 = Horizontal ? FirstPane.MinWidth + FirstPane.Margin.Horizontal : FirstPane.MinHeight + FirstPane.Margin.Vertical;
        var min2 = Horizontal ? SecondPane.MinWidth + SecondPane.Margin.Horizontal : SecondPane.MinHeight + SecondPane.Margin.Vertical;
        var max1 = Horizontal ? FirstPane.MaxWidth + FirstPane.Margin.Horizontal : FirstPane.MaxHeight + FirstPane.Margin.Vertical;
        var max2 = Horizontal ? SecondPane.MaxWidth + SecondPane.Margin.Horizontal : SecondPane.MaxHeight + SecondPane.Margin.Vertical;
        if ((double)min1 + min2 > space)
        {
            var compressed = (float)(space * ((double)min1 / ((double)min1 + min2)));
            return (compressed, space - compressed, divider, compressed, compressed);
        }
        space = (float)Math.Min(space, (double)max1 + max2);
        var lower = Math.Max(min1, space - max2);
        var upper = Math.Min(max1, space - min2);
        var first = Math.Clamp(float.IsNaN(FirstPaneSize) ? space / 2 : FirstPaneSize, lower, upper);
        return (first, space - first, divider, lower, upper);
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        var axis = Axis(available); var cross = Horizontal ? available.Height : available.Width;
        if (!float.IsFinite(axis))
        {
            FirstPane.Measure(available, text); SecondPane.Measure(available, text);
            axis = Axis(FirstPane.DesiredSize) + Axis(SecondPane.DesiredSize) + DividerThickness;
            if (!float.IsNaN(FirstPaneSize)) axis = Math.Max(axis, FirstPaneSize + DividerThickness + Axis(SecondPane.DesiredSize));
        }
        var sizes = Allocate(axis);
        FirstPane.Measure(Sized(sizes.First, cross), text); SecondPane.Measure(Sized(sizes.Second, cross), text);
        _divider.Measure(Sized(sizes.Divider, cross), text);
        var desiredCross = Horizontal ? Math.Max(FirstPane.DesiredSize.Height, SecondPane.DesiredSize.Height)
            : Math.Max(FirstPane.DesiredSize.Width, SecondPane.DesiredSize.Width);
        return Sized(axis, desiredCross);
    }

    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        if (area != _area) _divider.End(true);
        _area = area;
        var sizes = Allocate(Axis(area));
        if (sizes.Lower != _lower || sizes.Upper != _upper) { _divider.End(true); sizes = Allocate(Axis(area)); }
        _lower = sizes.Lower; _upper = sizes.Upper; ActualFirstPaneSize = sizes.First;
        FirstPane.Arrange(Slot(area, 0, sizes.First));
        _divider.Arrange(Slot(area, sizes.First, sizes.Divider));
        SecondPane.Arrange(Slot(area, sizes.First + sizes.Divider, sizes.Second));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(object property)
    {
        var sizeChange = ReferenceEquals(property, FirstPaneSizeProperty);
        if (!_writing || !sizeChange || !FirstPaneSize.Equals(_writtenSize)) _divider.End(!sizeChange);
        base.OnPropertyChanged(property);
    }

    private void Write(float size)
    {
        _writing = true; _writtenSize = size;
        try { FirstPaneSize = size; }
        finally { _writing = false; }
    }

    private sealed class DividerElement : Element
    {
        private readonly SplitView _owner;
        private IInputContext? _context;
        private float _start, _size, _original;
        internal bool IsResizing => _context != null;
        public DividerElement(SplitView owner)
        {
            _owner = owner;
            FocusChanged += _ => { if (!IsFocused) End(true); };
        }
        public override bool Focusable => true;
        protected override Color ThemeBackground => Theme.Surface;
        protected override Color ThemeForeground => !IsEffectivelyEnabled ? Theme.Disabled : IsFocused ? Theme.Focus
            : IsResizing || IsHovered ? Theme.BorderHover : Theme.Border;
        protected override void DrawCore(IDrawingContext context)
        {
            var horizontal = _owner.Horizontal;
            context.Fill(horizontal ? new(Bounds.X + Math.Max(0, (Bounds.Width - 2) / 2), Bounds.Y, Math.Min(2, Bounds.Width), Bounds.Height)
                : new(Bounds.X, Bounds.Y + Math.Max(0, (Bounds.Height - 2) / 2), Bounds.Width, Math.Min(2, Bounds.Height)), Foreground);
        }
        public override bool HandleInput(UiInput input, IInputContext context)
        {
            if (!IsEffectivelyEnabled) return false;
            try
            {
                if (IsResizing)
                {
                    if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) { End(true); return true; }
                    if (input.Kind == InputKind.PointerMove || input.Kind == InputKind.PointerUp && input.Button == 0)
                    {
                        var coordinate = _owner.Coordinate(input);
                        if (float.IsFinite(coordinate)) _owner.Write(coordinate == _start ? _original
                            : (float)Math.Clamp((double)_size + coordinate - _start, _owner._lower, _owner._upper));
                        if (input.Kind == InputKind.PointerUp) End(false);
                        return true;
                    }
                    return false;
                }
                if (_owner._upper <= _owner._lower) return false;
                if (input.Kind == InputKind.PointerDown && input.Button == 0 && Bounds.Contains(input.X, input.Y))
                {
                    _start = _owner.Coordinate(input); _size = _owner.ActualFirstPaneSize; _original = _owner.FirstPaneSize;
                    context.Focus(this); _context = context;
                    context.Capture(this); Invalidate(); return true;
                }
                if (input.Kind != InputKind.KeyDown || !IsFocused || input.Control) return false;
                var direction = input.Key == (_owner.Horizontal ? UiKey.Left : UiKey.Up) ? -1
                    : input.Key == (_owner.Horizontal ? UiKey.Right : UiKey.Down) ? 1 : 0;
                var next = input.Key == UiKey.Home ? _owner._lower : input.Key == UiKey.End ? _owner._upper
                    : (float)Math.Clamp((double)_owner.Allocate(_owner.Axis(_owner._area)).First + direction * (double)_owner.KeyboardStep * (input.Shift ? 10 : 1), _owner._lower, _owner._upper);
                if (direction == 0 && input.Key is not (UiKey.Home or UiKey.End)) return false;
                _owner.Write(next); return true;
            }
            catch (Exception original)
            {
                try { End(false); }
                catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
                throw;
            }
        }
        internal void End(bool cancel)
        {
            var context = _context;
            if (context == null) return;
            _context = null;
            try { context.ReleaseCapture(this); }
            finally { try { if (cancel) _owner.Write(_original); } finally { Invalidate(); } }
        }
        public override void OnInputCancelled() => End(true);
        public override void Dispose() { if (IsDisposed) return; try { End(true); } finally { base.Dispose(); } }
    }
}
