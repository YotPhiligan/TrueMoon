using TrueMoon.Configuration;
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
    IAppConfigurationContext Services(Action<IConfiguration, IServicesRegistrationContext> action);
    IAppConfigurationContext Configuration(Action<IConfiguration> action);
}