using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TrueMoon.Argentis;

/// <summary>Base for data objects that notify bindings when their properties change.</summary>
/// <remarks>Notifications run synchronously on the caller's thread. Derived objects own synchronization and lifetime.</remarks>
public abstract class Bindable : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Updates a field and notifies observers when the value changes according to the default equality comparer.</summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The requested value.</param>
    /// <param name="propertyName">The property name, inferred from the caller when omitted.</param>
    /// <returns>True when the field changed; otherwise false.</returns>
    /// <remarks>The field is committed before notification. An observer exception propagates without rolling back the value.</remarks>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Notifies observers of a property change, including dependent or computed properties.</summary>
    /// <param name="propertyName">The property name, inferred from the caller when omitted; null or empty means all properties changed.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Releases current property observers when a derived object's owner disposes it.</summary>
    /// <remarks>This does not dispose dependencies or prevent later subscriptions; the derived object defines its lifetime.</remarks>
    protected void ClearPropertyChangedSubscribers() => PropertyChanged = null;
}
