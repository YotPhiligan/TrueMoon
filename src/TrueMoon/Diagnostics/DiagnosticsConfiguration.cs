using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public sealed class DiagnosticsConfiguration : ConfigurableBase, IDiagnosticsConfiguration
{
    public DiagnosticsConfiguration()
    {
        var listeners = new List<Action<DiagnosticEvent>>();
        Set(nameof(Listeners),listeners);
        
        var filters = new List<string>();
        Set(nameof(Filters),filters);
    }

    /// <inheritdoc />
    public void AddEventListener(Action<DiagnosticEvent> action)
    {
        var listeners = Get<List<Action<DiagnosticEvent>>>(nameof(Listeners));
        if (listeners is null)
        {
            throw new InvalidOperationException($"{nameof(Listeners)} is null");
        }
        listeners.Add(action);
    }

    /// <inheritdoc />
    public IReadOnlyList<Action<DiagnosticEvent>> Listeners 
        => Get<IReadOnlyList<Action<DiagnosticEvent>>>(nameof(Listeners))!;

    /// <inheritdoc />
    public void AddFilters(params string[] filters)
    {
        var filtersList = Get<List<string>>(nameof(Filters));
        if (filtersList is null)
        {
            throw new InvalidOperationException($"{nameof(Filters)} is null");
        }
        filtersList.AddRange(filters);
    }

    /// <inheritdoc />
    public void AddFilter(string filter)
    {
        var filtersList = Get<List<string>>(nameof(Filters));
        if (filtersList is null)
        {
            throw new InvalidOperationException($"{nameof(Filters)} is null");
        }
        filtersList.Add(filter);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Filters => Get<IReadOnlyList<string>>(nameof(Filters))!;
}