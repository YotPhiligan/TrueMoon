using TrueMoon.Services;

namespace TrueMoon;

public interface IAppBuilderConfigurationContext
{
    IAppBuilderConfigurationContext ServiceResolverBuilder<TBuilder>(TBuilder? builder = default) 
        where TBuilder : class, IServiceResolverBuilder;
}