using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TrueMoon.Argentis;

namespace AlloyTest;

/// <summary>The retained state edited by the settings form.</summary>
public sealed class SettingsModel : INotifyPropertyChanged
{
    private string _name = "Алексей";
    private bool _enabled = true;
    private float _volume = .4f;
    private int _nextRow = 4;
    private readonly RowCollection _rows = new() { new("Первое дело"), new("Проверить 🧑‍💻"), new("Третье дело") };
    private LoadStatus _loadStatus = LoadStatus.Ready;
    private bool _isLight, _isCompact, _controlsEnabled = true;

    /// <summary>Whether the light palette is selected.</summary>
    public bool IsLight { get => _isLight; set { if (_isLight == value) return; _isLight = value; NotifyAppearance(nameof(IsLight)); } }
    /// <summary>Whether themed controls use compact spacing.</summary>
    public bool IsCompact { get => _isCompact; set { if (_isCompact == value) return; _isCompact = value; NotifyAppearance(nameof(IsCompact)); } }
    /// <summary>Whether the settings fields accept input; appearance controls remain usable.</summary>
    public bool ControlsEnabled { get => _controlsEnabled; set => Change(ref _controlsEnabled, value); }
    /// <summary>The palette and spacing applied to the existing tree.</summary>
    public Theme Appearance => (IsLight ? Theme.Light : Theme.Dark) with
    {
        ButtonPadding = IsCompact ? new Thickness(8, 4, 8, 4) : new Thickness(12, 8, 12, 8),
        EditorPadding = new Thickness(IsCompact ? 4 : 8), StackSpacing = IsCompact ? 6 : 12
    };
    /// <summary>Selects the light palette.</summary>
    public void UseLight() => IsLight = true;
    /// <summary>Selects the dark palette.</summary>
    public void UseDark() => IsLight = false;

    private void NotifyAppearance(string property)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Appearance)));
    }

    /// <summary>The desired branch/message of the separate status region.</summary>
    public LoadStatus LoadStatus => _loadStatus;
    /// <summary>Shows the controlled loading state; the example does not start a network request.</summary>
    public void BeginLoading() => ChangeLoadStatus(new LoadStatus(LoadPhase.Loading, "Загружаем данные…"));
    /// <summary>Shows a recoverable error in the conditional region.</summary>
    public void ShowLoadError() => ChangeLoadStatus(new LoadStatus(LoadPhase.Error, "Нет соединения. Повторите загрузку."));
    /// <summary>Returns the conditional region to ready content.</summary>
    public void CompleteLoading() => ChangeLoadStatus(LoadStatus.Ready);

    private void ChangeLoadStatus(LoadStatus status)
    {
        if (_loadStatus == status) return;
        _loadStatus = status;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadStatus)));
    }

    /// <summary>The editable list, preserved independently of its generated controls.</summary>
    public ObservableCollection<SettingsRow> Rows => _rows;
    /// <summary>Adds a new independent row.</summary>
    public void AddRow() => _rows.Add(new SettingsRow($"Дело {_nextRow++}"));
    /// <summary>Removes a model row; the collection binding releases its generated controls.</summary>
    /// <param name="row">The row to remove.</param>
    public void RemoveRow(SettingsRow row) => _rows.Remove(row);
    /// <summary>Moves a row one place upward without recreating its controls.</summary>
    /// <param name="row">The row to move.</param>
    public void MoveRowUp(SettingsRow row)
    {
        var index = _rows.IndexOf(row);
        if (index > 0) _rows.Move(index, index - 1);
    }
    /// <summary>Replaces a row with a new model instance.</summary>
    /// <param name="row">The old row, whose generated subtree will be released.</param>
    public void ReplaceRow(SettingsRow row)
    {
        var index = _rows.IndexOf(row);
        if (index >= 0) _rows[index] = new SettingsRow("Новое дело");
    }
    /// <summary>Reverses the same model instances using one Reset notification.</summary>
    public void ResetRows() => _rows.Reset(_rows.Reverse().ToArray());

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

    private sealed class RowCollection : ObservableCollection<SettingsRow>
    {
        internal void Reset(SettingsRow[] rows)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (var row in rows) Items.Add(row);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
