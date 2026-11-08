using TrueMoon.Aether.Scenes;

using System.Globalization;
using System.Runtime.CompilerServices;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Stable numeric Transform projection retained for existing form consumers.</summary>
public sealed class TransformField : InspectorProperty
{
    private readonly Action _verifyOwner;
    private bool _degrees, _editingDisplay;
    private string _canonical = "";
    private string? _degreeText;
    internal TransformField(string key, string label, Action changed)
        : base(default, new(key, PropertyValueKind.Number, PropertyValue.From(key is "sx" or "sy" or "sz" ? 1d : 0d)), label, changed) => _verifyOwner = changed;

    internal bool IsAngle => Key is "pitch" or "yaw" or "roll";
    internal bool HasValidAngle => !IsNull && TryNumber(Value, out var radians) && float.IsFinite((float)radians);
    /// <summary>The label in the inspector's selected display units. Value and schema remain canonical.</summary>
    public string DisplayLabel => IsAngle && _degrees ? Label.Replace("рад", "°", StringComparison.Ordinal) : Label;
    /// <summary>Accessible Transform input name in the current display units.</summary>
    public string InputAccessibleName => "Transform " + DisplayLabel;
    /// <summary>Accessible field reset action in the current display units.</summary>
    public string ResetAccessibleName => "Сбросить Transform · " + DisplayLabel + " к наследуемому значению или умолчанию";
    /// <summary>Numeric gesture help in the current display units.</summary>
    public string NumericGestureHint => DisplayLabel + " · перетаскивание: 4 пикселя на шаг; ↑/↓; Shift ×10, Ctrl ×0.1; Esc — отменить жест";
    /// <summary>Retained numeric text in display units. Angle edits convert to canonical radians;
    /// changing display units alone never rewrites Value or creates a draft.</summary>
    public string DisplayValue
    {
        get => !IsAngle || !_degrees ? Value : _degreeText ??= TryNumber(Value, out var radians)
            ? ToDegrees(radians).ToString("R", CultureInfo.InvariantCulture) : Value;
        set
        {
            ArgumentNullException.ThrowIfNull(value); _verifyOwner();
            if (DisplayValue == value && !IsReset) return;
            if (!IsAngle || !_degrees) { Value = value; return; }
            var previous = DisplayValue;
            var canonical = TryNumber(value, out var degrees)
                ? TryNumber(previous, out var displayed) && degrees == displayed ? Value : ToRadians(degrees).ToString("R", CultureInfo.InvariantCulture)
                : value;
            _editingDisplay = true; _degreeText = value;
            try { Value = canonical; }
            finally { _editingDisplay = false; }
            OnPropertyChanged();
        }
    }
    internal static double ToDegrees(double radians) => radians * (180d / Math.PI);
    internal static double ToRadians(double degrees) => degrees * (Math.PI / 180d);
    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    internal void SetDegrees(bool degrees)
    {
        if (!IsAngle || _degrees == degrees) return;
        _degrees = degrees; OnPropertyChanged(nameof(DisplayValue)); OnPropertyChanged(nameof(DisplayLabel));
    }
    internal void RestoreDisplayText(string value)
    {
        _verifyOwner();
        if (!IsAngle || !_degrees) return;
        _degreeText = value; OnPropertyChanged(nameof(DisplayValue));
    }
    /// <inheritdoc />
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(Value) && _canonical != Value)
        {
            _canonical = Value;
            if (!_editingDisplay) _degreeText = null;
        }
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(Value)) base.OnPropertyChanged(nameof(DisplayValue));
        if (propertyName == nameof(DisplayLabel))
        {
            base.OnPropertyChanged(nameof(InputAccessibleName)); base.OnPropertyChanged(nameof(ResetAccessibleName));
            base.OnPropertyChanged(nameof(NumericGestureHint));
        }
    }
}
