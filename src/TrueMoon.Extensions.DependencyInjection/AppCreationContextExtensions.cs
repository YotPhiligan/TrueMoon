namespace TrueMoon.Extensions.DependencyInjection;

public static class AppCreationContextExtensions
{
    public static IAppBuilderConfigurationContext UseDI(this IAppBuilderConfigurationContext context) 
        => context.ServiceResolverBuilder<ServiceResolverBuilder>();
}