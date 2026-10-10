using System.ComponentModel;

namespace AlloyTest;

/// <summary>One editable item, whose identity determines its retained UI row.</summary>
public sealed class SettingsRow : INotifyPropertyChanged
{
    private string _name;

    /// <summary>Creates an independent single-line row.</summary>
    /// <param name="name">The initial editable text.</param>
    public SettingsRow(string name) { Validate(name); _name = name; }

    /// <summary>The single-line text edited by the generated TextBox.</summary>
    public string Name
    {
        get => _name;
        set
        {
            Validate(value);
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private static void Validate(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Contains('\r') || name.Contains('\n')) throw new ArgumentException("Use a single-line row name.", nameof(name));
    }
}
