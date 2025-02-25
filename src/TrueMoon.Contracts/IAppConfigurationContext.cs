using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Modules;
using TrueMoon.Services;

namespace TrueMoon;

/// <summary>
/// app creation context
/// </summary>
public interface IAppConfigurationContext
{
    /// <summary>
    /// Add serv to be used in the app
    /// </summary>
    /// <param name="action">dependencies configuration delegate</param>
    /// <returns></returns>
    IAppConfigurationContext Services(Action<IServicesRegistrationContext> action);
    IAppConfigurationContext Configuration(Action<IConfiguration> action);
    IAppConfigurationContext Modules(Action<IModuleConfigurationContext> action);
}