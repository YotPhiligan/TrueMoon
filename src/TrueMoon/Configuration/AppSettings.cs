using System.Reflection;

namespace TrueMoon.Configuration;

/// <summary>Startup settings for an application opting into the standard Core schema.</summary>
public sealed record AppSettings
{
    /// <summary>Application name, fixed when the App is built.</summary>
    public string Name { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "TrueMoon";
}
