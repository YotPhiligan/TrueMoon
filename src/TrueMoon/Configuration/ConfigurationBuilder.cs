namespace TrueMoon.Configuration;

public class ConfigurationBuilder : IConfigurationBuilder
{
    private readonly Lock _lock = new ();
    /// <inheritdoc />
    public void AddProvider(IConfigurationProvider configurationProvider)
    {
        lock (_lock)
        {
            // if (_configurationProviders.Contains(configurationProvider))
            // {
            //     return;
            // }
            //
            // if (_configurationProviders.Any(t=>t.Name == configurationProvider.Name))
            // {
            //     if (_configurationProviders.RemoveAll(t => t.Name == configurationProvider.Name) > 0)
            //     {
            //         _configurationProviders.Add(configurationProvider);
            //     }
            //
            //     return;
            // }
            //
            // _configurationProviders.Add(configurationProvider);
            //
            // RefreshCore();
        }
    }

    /// <inheritdoc />
    public void RemoveProvider<T>(T? configurationProvider = default) where T : IConfigurationProvider
    {
        lock (_lock)
        {
            // if (configurationProvider != null)
            // {
            //     _configurationProviders.Remove(configurationProvider);
            // }
            // else
            // {
            //     _configurationProviders.RemoveAll(t => t is T);
            // }
        }
    }

    public IConfiguration Build()
    {
        var argsSection = new CommandLineArgsProvider();
        var defaultProvider = new DefaultConfigurationProvider();
        var configuration = new CommonConfiguration([
            argsSection,
            defaultProvider,
        ]);
        
        return configuration;
    }
}