using TrueMoon.Argentis;
using System.Globalization;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Typed scalar draft and provenance, independent of controls, rendering and themes.</summary>
public class InspectorProperty : Bindable
{
    private readonly Action _verifyOwner;
    private string _value = "", _original = "", _error = "", _source = "";
    private bool _null, _originalNull, _reset, _local, _enabled;
    internal InspectorProperty(ComponentId component, ComponentPropertySchema schema, string label, Action verifyOwner)
    { ComponentId = component; Schema = schema; Label = label; _verifyOwner = verifyOwner; }
    /// <summary>Gets the component occurrence addressed by this field.</summary>
    public ComponentId ComponentId { get; internal set; }
    /// <summary>Gets the exact property contract.</summary>
    public ComponentPropertySchema Schema { get; }
    /// <summary>Gets the persistent property key.</summary>
    public string Key => Schema.Id;
    /// <summary>Gets the presentation label.</summary>
    public string Label { get; }
    /// <summary>Gets the scalar kind, independent of an explicit null value.</summary>
    public PropertyValueKind Kind => Schema.Kind;
    /// <summary>Gets whether explicit null is accepted.</summary>
    public bool IsNullable => Schema.IsNullable;
    /// <summary>Gets/sets a draft. Strings use LF for presentation; untouched committed text is never rewritten.</summary>
    public string Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value); _verifyOwner();
            if (_value == value && !_reset) return;
            _value = value; _reset = false; OnPropertyChanged(nameof(Value)); OnPropertyChanged(nameof(BooleanValue)); DraftChanged();
        }
    }
    /// <summary>Gets/sets a Boolean without parsing localized labels.</summary>
    public bool BooleanValue
    {
        get => _value == "true";
        set { if (Kind != PropertyValueKind.Boolean) throw new InvalidOperationException("This property is not Boolean."); Value = value ? "true" : "false"; }
    }
    /// <summary>Gets/sets explicit null, distinct from inheritance or the empty string.</summary>
    public bool IsNull
    {
        get => _null;
        set
        {
            _verifyOwner(); if (value && !IsNullable) throw new InvalidOperationException("The property is not nullable.");
            if (_null == value && !_reset) return;
            _null = value; _reset = false; OnPropertyChanged(nameof(IsNull)); DraftChanged();
        }
    }
    /// <summary>Gets whether this field is a changed draft or pending inheritance reset.</summary>
    public bool HasDraft => _reset || _value != _original || _null != _originalNull;
    /// <summary>Gets whether applying will remove the instance value.</summary>
    public bool IsReset => _reset;
    /// <summary>Gets whether an explicit local value or pending draft can be reset.</summary>
    public bool CanReset => _enabled && (_local || HasDraft);
    /// <summary>Gets whether an editor can accept input now.</summary>
    public bool CanWrite => _enabled && !_null && !_reset;
    /// <summary>Gets whether nullable controls are currently editable.</summary>
    public bool CanChangeNull => _enabled && !_reset;
    /// <summary>Gets effective-value provenance or a pending reset description.</summary>
    public string SourceText => _reset ? "Будет сброшено к наследуемому значению" : _source;
    /// <summary>Gets the field-local validation diagnostic.</summary>
    public string Error => _error;
    /// <summary>Gets whether a diagnostic is present.</summary>
    public bool HasError => _error.Length != 0;
    internal event Action? Changed;
    internal void Enable(bool enabled)
    { if (_enabled == enabled) return; _enabled = enabled; OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(CanChangeNull)); OnPropertyChanged(nameof(CanReset)); }
    internal void Load(PropertyValue value, string source, bool local)
    {
        _value = _original = Format(value).ReplaceLineEndings("\n");
        _null = _originalNull = value.Kind == PropertyValueKind.Null; _reset = false; _source = source; _local = local;
        foreach (var property in new[] { nameof(Value), nameof(BooleanValue), nameof(IsNull), nameof(HasDraft), nameof(IsReset), nameof(SourceText), nameof(CanWrite), nameof(CanChangeNull), nameof(CanReset) }) OnPropertyChanged(property);
        SetError("");
    }
    internal void Reset()
    { _verifyOwner(); if (!CanReset) return; _reset = true; OnPropertyChanged(nameof(IsReset)); DraftChanged(); }
    private void DraftChanged()
    {
        OnPropertyChanged(nameof(HasDraft)); OnPropertyChanged(nameof(IsReset)); OnPropertyChanged(nameof(SourceText)); OnPropertyChanged(nameof(CanWrite)); OnPropertyChanged(nameof(CanChangeNull)); OnPropertyChanged(nameof(CanReset)); Changed?.Invoke();
    }
    internal bool TryValue(out PropertyValue value)
    {
        value = default; string error = "";
        if (_reset) { SetError(""); return true; }
        if (_null) { if (!IsNullable) error = "Null недопустим"; }
        else switch (Kind)
        {
            case PropertyValueKind.Boolean:
                if (bool.TryParse(Value, out var boolean)) value = PropertyValue.From(boolean); else error = "Выберите true или false";
                break;
            case PropertyValueKind.Integer:
                if (long.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) value = PropertyValue.From(integer);
                else error = "Введите целое Int64"; break;
            case PropertyValueKind.Number:
                if (double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                {
                    value = PropertyValue.From(number);
                    if (this is TransformField && (!float.IsFinite((float)number) || Key is "sx" or "sy" or "sz" && (float)number <= 0))
                        error = Key is "sx" or "sy" or "sz" ? "Масштаб должен быть > 0" : "Введите конечное число с точкой";
                }
                else error = "Введите конечное число с точкой"; break;
            case PropertyValueKind.String:
                if (Value.Length > PropertyValue.MaximumStringLength) error = "Не более 4096 символов";
                else if (!ValidUtf16(Value)) error = "Некорректная Unicode-строка";
                else value = PropertyValue.From(Value);
                break;
            default: error = "Нет редактора для этого типа"; break;
        }
        SetError(error); return error.Length == 0;
    }
    internal void SetError(string value)
    { if (_error == value) return; _error = value; OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(HasError)); }
    internal static string Format(PropertyValue value) => value.Kind switch
    {
        PropertyValueKind.Boolean => value.GetBoolean() ? "true" : "false",
        PropertyValueKind.Integer => value.GetInteger().ToString(CultureInfo.InvariantCulture),
        PropertyValueKind.Number => value.GetNumber().ToString("R", CultureInfo.InvariantCulture),
        PropertyValueKind.String => value.GetString(), _ => ""
    };
    private static bool ValidUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }
}
