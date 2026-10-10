using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

/// <summary>Mutable diagnostics settings. Public collections are snapshots.</summary>
public sealed class DiagnosticsConfiguration : ConfigurableBase, IDiagnosticsConfiguration
{
    private readonly Lock _lock = new();
    private readonly List<Action<DiagnosticEvent>> _listeners = [];
    private readonly List<string> _filters = [];

    public void AddEventListener(Action<DiagnosticEvent> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_lock) _listeners.Add(action);
    }

    public IReadOnlyList<Action<DiagnosticEvent>> Listeners
    {
        get { lock (_lock) return _listeners.ToArray(); }
    }

    public void AddFilters(params string[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        foreach (var filter in filters) ArgumentNullException.ThrowIfNull(filter);
        lock (_lock) _filters.AddRange(filters);
    }

    public void AddFilter(string filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        lock (_lock) _filters.Add(filter);
    }

    public IReadOnlyList<string> Filters
    {
        get { lock (_lock) return _filters.ToArray(); }
    }
}
