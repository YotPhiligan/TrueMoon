namespace TrueMoon.Argentis;

/// <summary>A compact numeric draft editor with optional parsed step/drag adjustment. Application validation and commit belong to the consumer.</summary>
/// <remarks>Owns its prefix and TextBox; editing preserves incomplete strings such as an empty value or minus sign.</remarks>
public sealed class NumberBox : ElementList
{
    /// <summary>Opt-in pointer and arrow-key adjustment. Ordinary text editing is always retained.</summary>
    public static readonly UiProperty<bool> AdjustmentEnabledProperty = new("AdjustmentEnabled", false, Invalidation.Layout);
    /// <summary>Arithmetic kind used for explicit adjustments.</summary>
    public static readonly UiProperty<NumericValueKind> NumericKindProperty = new("NumericKind", NumericValueKind.Real, validate: Enum.IsDefined);
    /// <summary>Positive base step; Shift multiplies by ten, Control divides by ten.</summary>
    public static readonly UiProperty<double> StepProperty = new("Step", .1d, validate: v => double.IsFinite(v) && v > 0);
    /// <summary>Inclusive lower bound; values outside bounds are rejected rather than clamped.</summary>
    public static readonly UiProperty<double> MinimumProperty = new("Minimum", double.NegativeInfinity, validate: v => !double.IsNaN(v));
    /// <summary>Inclusive upper bound; values outside bounds are rejected rather than clamped.</summary>
    public static readonly UiProperty<double> MaximumProperty = new("Maximum", double.PositiveInfinity, validate: v => !double.IsNaN(v));
    /// <summary>Whether numeric adjustment is enabled.</summary>
    public bool AdjustmentEnabled { get => Get(AdjustmentEnabledProperty); set => Set(AdjustmentEnabledProperty, value); }
    /// <summary>Whether adjustments use Double or checked Int64.</summary>
    public NumericValueKind NumericKind { get => Get(NumericKindProperty); set => Set(NumericKindProperty, value); }
    /// <summary>The base step in value units.</summary>
    public double Step { get => Get(StepProperty); set => Set(StepProperty, value); }
    /// <summary>Inclusive minimum adjusted value.</summary>
    public double Minimum { get => Get(MinimumProperty); set => Set(MinimumProperty, value); }
    /// <summary>Inclusive maximum adjusted value.</summary>
    public double Maximum { get => Get(MaximumProperty); set => Set(MaximumProperty, value); }
    /// <summary>Optional borrowed consumer admission gate, evaluated before every adjustment.</summary>
    public Func<bool>? CanAdjust { get; set; }
    /// <summary>Optional identity fence for an active gesture; false aborts without restoring a superseded value.</summary>
    public Func<bool>? IsGestureCurrent { get; set; }
    /// <summary>Raised after capture starts and before any draft change.</summary>
    public event Action? GestureStarted;
    /// <summary>Raised after capture is released; consumers may restore grouped drafts on cancellation.</summary>
    public event Action<NumericGestureResult>? GestureEnded;
    private IInputContext? _context;
    private string _original = "", _lastValue = "";
    private float _originX;
    private double _gestureStep;
    private bool _dragging, _writing;
    /// <summary>Whether this control owns a numeric pointer gesture.</summary>
    public bool IsDragging => _dragging;
    /// <summary>The current drag affordance, in root logical coordinates.</summary>
    public Rect DragBounds { get; private set; }
    /// <summary>The retained single-line draft editor. Bind its ValueProperty to the consumer's string draft.</summary>
    public TextBox Editor { get; } = new() { MinWidth = 0 };
    /// <summary>A short semantic prefix, such as an axis letter.</summary>
    public Text Prefix { get; } = new PrefixText() { VerticalAlignment = LayoutAlignment.Center };

    /// <summary>Creates an independent numeric editor with an optional prefix.</summary>
    public NumberBox(string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(prefix);
        Prefix.Value = prefix; Items.Add(Prefix); Items.Add(Editor);
        Editor.PropertyChanged += EditorChanged;
        Editor.FocusChanged += EditorFocusChanged;
        AttachmentChanged += Attachment;
        Invalidated += InteractionInvalidated;
    }

    /// <inheritdoc />
    protected override EditorColorRole ThemeColorRole => EditorColorRole.Number;

    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Prefix.Measure(available, text); Editor.Measure(available, text);
        return new(Math.Min(available.Width, 80), Math.Max(Prefix.DesiredSize.Height, Editor.DesiredSize.Height));
    }
    /// <inheritdoc />
    protected override void ArrangeCore(Rect area)
    {
        var prefix = Prefix.Value.Length == 0 ? AdjustmentEnabled ? Math.Min(area.Width, 14) : 0 : Math.Min(area.Width, Prefix.DesiredSize.Width + 5);
        DragBounds = new(area.X, area.Y, prefix, area.Height);
        Prefix.Arrange(new(area.X, area.Y, Math.Max(0, prefix - 5), area.Height));
        Editor.Arrange(new(area.X + prefix, area.Y, Math.Max(0, area.Width - prefix), area.Height));
    }
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context)
    {
        if (!AdjustmentEnabled || Prefix.Value.Length != 0 || DragBounds.Width <= 0) return;
        var color = Editor.IsEffectivelyEnabled && !Editor.IsReadOnly ? Theme.EditorColor(ColorRole) : Theme.Disabled;
        var x = DragBounds.X + DragBounds.Width / 2; var y = DragBounds.Y + DragBounds.Height / 2;
        context.Fill(new(x - 4, y, 8, 1), color);
        for (var i = 0; i < 3; i++) { context.Fill(new(x - 4 + i, y - i, 1, 1 + 2 * i), color); context.Fill(new(x + 3 - i, y - i, 1, 1 + 2 * i), color); }
    }
    private bool Allowed => AdjustmentEnabled && IsEffectivelyEnabled && Editor.IsEffectivelyEnabled && !Editor.IsReadOnly && (CanAdjust?.Invoke() ?? true);
    private double EffectiveStep(bool shift, bool control) =>
        (NumericKind == NumericValueKind.Integer ? Math.Max(1, Math.Round(Step, MidpointRounding.AwayFromZero)) : Step) * ((shift ? 10 : 1) * (control ? .1 : 1));
    /// <summary>Adjusts a valid draft by one step without committing; returns false when admission, parsing or bounds reject it.</summary>
    public bool TryStep(int direction, bool shift = false, bool control = false)
    {
        VerifyAccess();
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (_dragging || !Allowed || !NumericAdjustment.TryApply(Editor.Value, NumericKind, EffectiveStep(shift, control), direction, Minimum, Maximum, out var value)) return false;
        Write(value); return true;
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context)
    {
        try { return HandleNumericInput(input, context); }
        catch (Exception original)
        {
            try { End(NumericGestureResult.Interrupted); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    private bool HandleNumericInput(UiInput input, IInputContext context)
    {
        if (_dragging)
        {
            if (!(IsGestureCurrent?.Invoke() ?? true)) { End(NumericGestureResult.Interrupted); return false; }
            if (!Allowed) { CancelGesture(); return false; }
            if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) { CancelGesture(); return true; }
            if (input.Kind == InputKind.PointerMove && float.IsFinite(input.X))
            {
                Move(input.X);
                return true;
            }
            if (input.Kind == InputKind.PointerUp && input.Button == 0)
            { try { if (float.IsFinite(input.X)) Move(input.X); } finally { CompleteGesture(); } return true; }
            return input.Kind is InputKind.KeyDown or InputKind.Text;
        }
        if (input.Kind == InputKind.KeyDown && input.Key is UiKey.Up or UiKey.Down && Editor.IsFocused && AdjustmentEnabled)
        { TryStep(input.Key == UiKey.Up ? 1 : -1, input.Shift, input.Control); return true; }
        if (input.Kind != InputKind.PointerDown || input.Button != 0 || !float.IsFinite(input.X) || !DragBounds.Contains(input.X, input.Y) || !Allowed) return false;
        _gestureStep = EffectiveStep(input.Shift, input.Control);
        if (!NumericAdjustment.TryApply(Editor.Value, NumericKind, _gestureStep, 0, Minimum, Maximum, out _)) return false;
        context.Focus(Editor); _original = _lastValue = Editor.Value; _originX = input.X; _context = context; _dragging = true;
        try { context.Capture(this); GestureStarted?.Invoke(); }
        catch { End(NumericGestureResult.Cancelled); throw; }
        return true;
    }
    private void Write(string value)
    {
        _writing = true;
        try { Editor.Value = value; _lastValue = Editor.Value; }
        finally { _writing = false; }
    }
    private void Move(float x)
    {
        var position = ((double)x - _originX) / 4;
        var nearest = Math.Round(position);
        if (Math.Abs(position - nearest) < .0001) position = nearest; // Logical coordinates are Single; tolerate subpixel round-off at an exact tick.
        var ticks = Math.Truncate(position);
        if (ticks == 0) Write(_original);
        else if (ticks >= long.MinValue && ticks < long.MaxValue &&
            NumericAdjustment.TryApply(_original, NumericKind, _gestureStep, (long)ticks, Minimum, Maximum, out var value)) Write(value);
    }
    /// <summary>Ends capture, retaining the final draft without applying application data.</summary>
    public void CompleteGesture() => End(NumericGestureResult.Completed);
    /// <summary>Ends capture and restores the exact pre-gesture string, preserving unrelated prior drafts.</summary>
    public void CancelGesture() => End(NumericGestureResult.Cancelled);
    private void End(NumericGestureResult result)
    {
        if (!_dragging) return;
        VerifyAccess();
        _dragging = false; var context = _context; _context = null;
        try { context?.ReleaseCapture(this); }
        finally
        {
            try
            {
                if (result == NumericGestureResult.Cancelled)
                {
                    try { if (!(IsGestureCurrent?.Invoke() ?? true)) result = NumericGestureResult.Interrupted; }
                    catch { result = NumericGestureResult.Interrupted; throw; }
                    if (result == NumericGestureResult.Cancelled && Editor.Value == _lastValue) Write(_original);
                }
            }
            finally { GestureEnded?.Invoke(result); }
        }
    }
    private void EditorChanged(Element sender, object property)
    {
        if (!_dragging || _writing) return;
        if (ReferenceEquals(property, TextBox.ValueProperty)) End(NumericGestureResult.Interrupted);
        else if (!Editor.IsEffectivelyEnabled || Editor.IsReadOnly) CancelGesture();
    }
    private void EditorFocusChanged(Element sender) { if (!Editor.IsFocused) CancelGesture(); }
    private void Attachment() { if (!IsAttached) CancelGesture(); }
    private void InteractionInvalidated(Invalidation reason)
    {
        if (!_dragging || _writing) return;
        try { if (!IsVisible || !Allowed || !(IsGestureCurrent?.Invoke() ?? true)) CancelGesture(); }
        catch (Exception original)
        {
            try { End(NumericGestureResult.Interrupted); }
            catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    /// <inheritdoc />
    public override void OnInputCancelled() => CancelGesture();
    /// <inheritdoc />
    public override void Dispose()
    {
        if (IsDisposed) return;
        try { CancelGesture(); }
        finally { try { base.Dispose(); } finally { if (IsDisposed) { CanAdjust = null; IsGestureCurrent = null; GestureStarted = null; GestureEnded = null; } } }
    }
    private sealed class PrefixText : Text
    {
        protected override Color ThemeForeground => Theme.EditorColor(ColorRole);
    }
}
