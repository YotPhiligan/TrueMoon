using TrueMoon.Configuration;

namespace TrueMoon.Titanium;

public class UnitConfiguration : ConfigurableBase, IUnitConfiguration
{
    public UnitConfiguration(int index, Action<IAppConfigurationContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        Index = index;
        ConfigurationDelegate = action;
        Name = $"unit{index}";
    }

    /// <inheritdoc />
    public int Index { get; }

    /// <inheritdoc />
    public Action<IAppConfigurationContext> ConfigurationDelegate
    {
        get => Get<Action<IAppConfigurationContext>>(nameof(ConfigurationDelegate))!; 
        set => Set(nameof(ConfigurationDelegate), value);
    }

    /// <inheritdoc />
    public UnitStartupPolicy? StartupPolicy
    {
        get => Get<UnitStartupPolicy?>(nameof(StartupPolicy)); 
        set => Set(nameof(StartupPolicy), value);
    }

    /// <inheritdoc />
    public UnitHostingPolicy? HostingPolicy
    {
        get => Get<UnitHostingPolicy?>(nameof(HostingPolicy)); 
        set => Set(nameof(HostingPolicy), value);
    }

    /// <inheritdoc />
    public UnitLifetimePolicy? LifetimePolicy
    {
        get => Get<UnitLifetimePolicy?>(nameof(LifetimePolicy)); 
        set => Set(nameof(LifetimePolicy), value);
    }

    /// <inheritdoc />
    public UnitRestartPolicy? RestartPolicy
    {
        get => Get<UnitRestartPolicy?>(nameof(RestartPolicy)); 
        set => Set(nameof(RestartPolicy), value);
    }

    /// <inheritdoc />
    public string Name
    {
        get => Get<string>(nameof(Name))!; 
        set => Set(nameof(Name), value);
    }

    /// <inheritdoc />
    public bool? IsControlAppLifetime
    {
        get => Get<bool?>(nameof(IsControlAppLifetime))!; 
        set => Set(nameof(IsControlAppLifetime), value);
    }

    /// <inheritdoc />
    public int? TerminationDelay
    {
        get => Get<int?>(nameof(TerminationDelay))!; 
        set => Set(nameof(TerminationDelay), value);
    }
}