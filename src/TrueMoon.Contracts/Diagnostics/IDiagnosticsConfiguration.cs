using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public interface IDiagnosticsConfiguration : IConfigurable
{
    void AddEventListener(Action<DiagnosticEvent> action);
    IReadOnlyList<Action<DiagnosticEvent>> Listeners { get; }

    void AddFilters(params string[] filters);
    void AddFilter(string filter);

    IReadOnlyList<string> Filters { get; }
}