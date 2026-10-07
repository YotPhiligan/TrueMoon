using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AlloyTest;

/// <summary>The retained state edited by the settings form.</summary>
public sealed class SettingsModel : INotifyPropertyChanged
{
    private string _name = "Алексей";
    private bool _enabled = true;
    private float _volume = .4f;

    /// <summary>The single-line display name.</summary>
    public string Name
    {
        get => _name;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Contains('\r') || value.Contains('\n')) throw new ArgumentException("Use a single-line name.", nameof(value));
            Change(ref _name, value);
        }
    }
    /// <summary>Whether notifications are enabled.</summary>
    public bool Enabled { get => _enabled; set => Change(ref _enabled, value); }
    /// <summary>Volume in the inclusive range zero to one.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            Change(ref _volume, value);
        }
    }
    /// <summary>A description of the current model, observed by a one-way text binding.</summary>
    public string Summary => $"{Name} · уведомления: {(Enabled ? "вкл." : "выкл.")} · громкость: {Volume:P0}";
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Change<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }
    /// <summary>Restores the initial settings through model notifications.</summary>
    public void Reset() { Name = "Алексей"; Enabled = true; Volume = .4f; }
    /// <summary>Loads another set of values without rebuilding the form.</summary>
    public void LoadExample() { Name = "Привет 👋"; Enabled = false; Volume = .75f; }
}
