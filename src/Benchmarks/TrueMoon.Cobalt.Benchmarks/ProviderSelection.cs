using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

public enum Provider { Cobalt, Microsoft }

public static class ProviderSelection
{
    public static Provider Parse(string name) => name.ToLowerInvariant() switch
    {
        "cobalt" => Provider.Cobalt,
        "microsoft" => Provider.Microsoft,
        _ => throw new ArgumentException("Expected cobalt or microsoft.", nameof(name))
    };

    public static IServiceResolverBuilder CreateResolverBuilder(Provider provider) => provider switch
    {
        Provider.Cobalt => new CobaltServiceResolverBuilder(),
        Provider.Microsoft => new TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static IAppBuilder CreateAppBuilder(Provider provider) => App.Builder(context => {
        if (provider == Provider.Microsoft) context.UseDI();
    });
}

// Both providers execute Scenario.Register unchanged. App.Builder/Setup/Build is used
// symmetrically; the historical COB5 App.Build helper remains in Baselines.cs.
public sealed class ComparisonScenario
{
    private readonly Provider _provider;
    private readonly Action<IConfiguration, IServicesRegistrationContext>[] _registrations;
    private readonly Action<IAppConfigurationContext> _configure;
    public Scenario Graph { get; } = new();

    public ComparisonScenario(Provider provider)
    {
        _provider = provider;
        _registrations = [(_, services) => Graph.Register(services)];
        _configure = context => context.Services(Graph.Register);
    }

    public IServiceResolver CreateEmptyResolver() => ProviderSelection.CreateResolverBuilder(_provider).Build(null!, []);
    public IServiceResolver CreateResolver() => ProviderSelection.CreateResolverBuilder(_provider).Build(null!, _registrations);
    public IApp CreateApp() => ProviderSelection.CreateAppBuilder(_provider).Setup(_configure).Build();
    public IAppBuilder CreateAppBuilder() => ProviderSelection.CreateAppBuilder(_provider).Setup(_configure);
}
