using TrueMoon.Cobalt;
using TrueMoon.Services;

namespace TrueMoon;

public class AppBuilderConfigurationContext : IAppBuilderConfigurationContext
{
    private IServiceResolverBuilder? _serviceResolverBuilder;

    public IAppBuilderConfigurationContext ServiceResolverBuilder<TBuilder>(TBuilder? builder = default) where TBuilder : class, IServiceResolverBuilder
    {
        _serviceResolverBuilder = builder ?? Activator.CreateInstance<TBuilder>();
        return this;
    }

    public IServiceResolverBuilder GetServiceResolverBuilder() 
        => _serviceResolverBuilder ?? new CobaltServiceResolverBuilder();
}