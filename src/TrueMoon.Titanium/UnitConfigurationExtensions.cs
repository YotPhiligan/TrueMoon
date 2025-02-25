using TrueMoon.Titanium.Units;

namespace TrueMoon.Titanium;

public static class UnitConfigurationExtensions
{
    public static IUnitConfiguration Configure(this IUnitConfiguration configuration, Action<IAppConfigurationContext> action)
    {
        configuration.ConfigurationDelegate = action;
        return configuration;
    }
    
    public static IUnitConfiguration ExecuteInMainProcess(this IUnitConfiguration configuration)
    {
        configuration.HostingPolicy = UnitHostingPolicy.MainProcess;
        return configuration;
    }
    
    public static IUnitConfiguration ExecuteInChildProcess(this IUnitConfiguration configuration)
    {
        configuration.HostingPolicy = UnitHostingPolicy.ChildProcess;
        return configuration;
    }
    
    public static IUnitConfiguration StartImmediate(this IUnitConfiguration configuration)
    {
        configuration.StartupPolicy = UnitStartupPolicy.Immediate;
        return configuration;
    }
    
    public static IUnitConfiguration StartDelayed(this IUnitConfiguration configuration)
    {
        configuration.StartupPolicy = UnitStartupPolicy.Delayed;
        return configuration;
    }
    
    public static IUnitConfiguration ControlAppLifetime(this IUnitConfiguration configuration)
    {
        configuration.IsControlAppLifetime = true;
        return configuration;
    }
    
    public static IUnitConfiguration ExecuteExternal(this IUnitConfiguration configuration, string executableRelativePath)
    {
        configuration.HostingPolicy = UnitHostingPolicy.External;
        configuration.Set(ExternalUnitHandle.Key, executableRelativePath);
        return configuration;
    }
}