namespace TrueMoon.Diagnostics;

public static class DiagnosticsConfigurationExtensions
{
    public static IDiagnosticsConfiguration Filters(this IDiagnosticsConfiguration configuration, params string[] includeFilters)
    {
        configuration.AddFilters(includeFilters);

        return configuration;
    }
    
    public static IDiagnosticsConfiguration OnEvent(this IDiagnosticsConfiguration configuration, Action<DiagnosticEvent> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        configuration.AddEventListener(action);
        return configuration;
    }
}