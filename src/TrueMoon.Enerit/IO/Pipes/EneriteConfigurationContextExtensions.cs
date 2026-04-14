namespace TrueMoon.Enerit.IO.Pipes;

public static class EneriteConfigurationContextExtensions
{
    public static IAppConfigurationContext PipesServiceTransport(this IAppConfigurationContext context)
    {
        context.Services(registrationContext => registrationContext
            .RemoveAll<IInvocationClientFactory>()
            .RemoveAll<IInvocationServerFactory>()
            .Singleton<IInvocationClientFactory, PipesInvocationClientFactory>()
            .Singleton<IInvocationServerFactory, PipesInvocationServerFactory>()
            .Transient<IInvocationServerHandlerResolver, InvocationServerHandlerResolver>()
        );
        return context;
    }
}