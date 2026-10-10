namespace TrueMoon.Configuration;

/// <summary>Builds configurations. Later additions take priority over earlier providers.</summary>
public class ConfigurationBuilder : IConfigurationBuilder
{
    private readonly Lock _lock = new();
    private readonly List<IConfigurationProvider> _providers =
        [new CommandLineArgsProvider(), new EnvironmentVariablesProvider(), new DefaultConfigurationProvider()];

    public void AddProvider(IConfigurationProvider configurationProvider)
    {
        ArgumentNullException.ThrowIfNull(configurationProvider);
        lock (_lock)
        {
            _providers.RemoveAll(provider => ReferenceEquals(provider, configurationProvider));
            _providers.Insert(0, configurationProvider);
        }
    }

    public void RemoveProvider<T>(T? configurationProvider = default) where T : IConfigurationProvider
    {
        lock (_lock)
        {
            _providers.RemoveAll(provider => configurationProvider is null
                ? provider is T : ReferenceEquals(provider, configurationProvider));
        }
    }

    public IConfiguration Build()
    {
        lock (_lock) return new CommonConfiguration(_providers.ToArray());
    }
}
