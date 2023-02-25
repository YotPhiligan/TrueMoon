using TrueMoon.Configuration;

namespace TrueMoon.Diagnostics;

public static class ConfigurationExtensions
{
    public const string DiagnosticsConfigurationName = "DiagnosticsConfiguration"; 
    public static IDiagnosticsConfiguration? GetDiagnosticsConfiguration(this IConfiguration configuration) 
        => configuration.Get<IDiagnosticsConfiguration>(DiagnosticsConfigurationName);
}