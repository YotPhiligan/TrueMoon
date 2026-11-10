using TrueMoon.Configuration;
using TrueMoon.Services;
using TrueMoon.Dependencies;

namespace TrueMoon;

/// <inheritdoc />
public class AppConfigurationContext : IAppConfigurationContext, IAppConfigurationStage
{
    private readonly List<Action<IConfiguration,IServicesRegistrationContext>> _servicesRegistrationsActions = [];
    private readonly List<Action<IConfiguration>> _configurationActions = [];
    private readonly ConfigurationStage _configurationStage = new();
    private bool _configurationComplete;
    
    public IReadOnlyList<Action<IConfiguration,IServicesRegistrationContext>> GetServicesRegistrations() => _servicesRegistrationsActions;
    public IReadOnlyList<Action<IConfiguration>> GetConfigurations() => _configurationActions;

    public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(action);
        _servicesRegistrationsActions.Add((_, context) => action(context));
        return this;
    }
    
    public IAppConfigurationContext Services(Action<IConfiguration,IServicesRegistrationContext> action)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(action);
        _servicesRegistrationsActions.Add(action);
        return this;
    }
    
    public IAppConfigurationContext Configuration(Action<IConfiguration> action)
    {
        EnsureRegistrationOpen();
        ArgumentNullException.ThrowIfNull(action);
        _configurationActions.Add(action);
        return this;
    }

    /// <inheritdoc />
    void IAppConfigurationStage.UseConfiguration(IConfigurationProfile profile) => _configurationStage.Use(profile);

    /// <inheritdoc />
    void IAppConfigurationStage.ConfigureModule<T>(IConfigurationSchema<T> schema,
        Action<IAppConfigurationContext, T> register, Action<T>? configure)
        => _configurationStage.Add(schema, register, configure);

    private void EnsureRegistrationOpen()
    {
        if (_configurationComplete && _configurationStage.HasRequests)
            throw new InvalidOperationException("The App configuration stage is complete.");
    }

    internal bool CompleteConfiguration(IConfiguration configuration)
    {
        var next = 0;
        if (!_configurationStage.HasRequests)
        {
            // Preserve legacy enumeration semantics until the caller explicitly selects a stage.
            foreach (var action in _configurationActions)
            {
                action(configuration); next++;
                if (_configurationStage.HasRequests) break;
            }
        }
        if (!_configurationStage.HasRequests) { _configurationStage.Freeze(); return false; }
        Drain();
        var prepared = _configurationStage.Prepare();
        foreach (var registration in prepared.Registrations) registration(this);
        Drain();
        // Start the borrowed lifetime before application services so failed-start rollback also closes it.
        _servicesRegistrationsActions.Insert(0, (_, services) => services.Singleton<IStartable>(resolver => resolver.Resolve<ConfigurationLifetime>()));
        Services(services =>
        {
            if (services.Exist<IConfigurationReader>() || services.Exist<IConfigurationReloader>() ||
                services.Exist<IConfigurationWriter>() || services.Exist<IConfigurationChanges>() || services.Exist<ConfigurationRuntime>())
                throw new InvalidOperationException("Configuration reader registrations belong to the App stage.");
            services.Singleton<ConfigurationRuntime>(_ => new ConfigurationRuntime(_configurationStage, prepared.Snapshot));
            services.Singleton<IConfigurationReader>(resolver => new ConfigurationReader(resolver.Resolve<ConfigurationRuntime>()));
            services.Singleton<IConfigurationReloader>(resolver => new ConfigurationReader(resolver.Resolve<ConfigurationRuntime>()));
            services.Singleton<IConfigurationWriter>(resolver => new ConfigurationReader(resolver.Resolve<ConfigurationRuntime>()));
            services.Singleton<IConfigurationChanges>(resolver => new ConfigurationReader(resolver.Resolve<ConfigurationRuntime>()));
            services.Singleton<ConfigurationLifetime>(resolver => new ConfigurationLifetime(resolver.Resolve<ConfigurationRuntime>()));
            services.Singleton<IStoppable>(resolver => resolver.Resolve<ConfigurationLifetime>());
        });
        _configurationComplete = true;
        return true;

        void Drain()
        {
            while (next < _configurationActions.Count) _configurationActions[next++](configuration);
        }
    }
}
