namespace TrueMoon.Extensions.DependencyInjection;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseDI(this IAppBuilderConfigurationContext context)
    {
        return context.ServiceResolverBuilder<DependencyInjectionProvider>();
    }
}