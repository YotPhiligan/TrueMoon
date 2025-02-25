using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Modules;
using TrueMoon.Services;

namespace TrueMoon;

/// <inheritdoc />
public class AppConfigurationContext : IAppConfigurationContext
{
    private readonly List<Action<IServicesRegistrationContext>> _servicesRegistrationsActions = [];
    private readonly List<Action<IModuleConfigurationContext>> _modulesConfigurationActions = [];
    private readonly List<Action<IConfiguration>> _configurationActions = [];
    
    public IReadOnlyList<Action<IServicesRegistrationContext>> GetServicesRegistrations() => _servicesRegistrationsActions;
    public IReadOnlyList<Action<IModuleConfigurationContext>> GetModulesConfigurations() => _modulesConfigurationActions;
    public IReadOnlyList<Action<IConfiguration>> GetConfigurations() => _configurationActions;

    public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _servicesRegistrationsActions.Add(action);
        return this;
    }

    public IAppConfigurationContext Modules(Action<IModuleConfigurationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _modulesConfigurationActions.Add(action);
        return this;
    }
    
    public IAppConfigurationContext Configuration(Action<IConfiguration> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _configurationActions.Add(action);
        return this;
    }
}