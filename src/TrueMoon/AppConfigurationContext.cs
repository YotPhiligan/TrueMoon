using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon;

/// <inheritdoc />
public class AppConfigurationContext : IAppConfigurationContext
{
    private readonly List<Action<IConfiguration,IServicesRegistrationContext>> _servicesRegistrationsActions = [];
    private readonly List<Action<IConfiguration>> _configurationActions = [];
    
    public IReadOnlyList<Action<IConfiguration,IServicesRegistrationContext>> GetServicesRegistrations() => _servicesRegistrationsActions;
    public IReadOnlyList<Action<IConfiguration>> GetConfigurations() => _configurationActions;

    public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _servicesRegistrationsActions.Add((_, context) => action(context));
        return this;
    }
    
    public IAppConfigurationContext Services(Action<IConfiguration,IServicesRegistrationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _servicesRegistrationsActions.Add(action);
        return this;
    }
    
    public IAppConfigurationContext Configuration(Action<IConfiguration> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _configurationActions.Add(action);
        return this;
    }
}