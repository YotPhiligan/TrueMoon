using System.ComponentModel;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>One numeric draft and its validation error, independent of controls.</summary>
public sealed class TransformField : INotifyPropertyChanged
{
    private string _value = "", _error = "";
    private readonly Action _changed;
    internal TransformField(string key, string label, Action changed) { Key = key; Label = label; _changed = changed; }
    /// <summary>Gets the stable built-in property key.</summary>
    public string Key { get; }
    /// <summary>Gets the visible field label.</summary>
    public string Label { get; }
    /// <summary>Gets/sets uncommitted text, using invariant decimal notation.</summary>
    public string Value
    {
        get => _value;
        set { ArgumentNullException.ThrowIfNull(value); if (_value == value) return; _changed(); _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); Changed?.Invoke(); }
    }
    internal event Action? Changed;
    /// <summary>Gets the field-local numeric/scale error.</summary>
    public string Error => _error;
    /// <summary>Gets whether the field-local diagnostic is visible.</summary>
    public bool HasError => _error.Length != 0;
    internal void SetError(string value) { if (_error != value) { _error = value; PropertyChanged?.Invoke(this, new(nameof(Error))); PropertyChanged?.Invoke(this, new(nameof(HasError))); } }
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}
