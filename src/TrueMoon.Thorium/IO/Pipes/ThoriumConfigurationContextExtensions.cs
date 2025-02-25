using TrueMoon.Dependencies;

namespace TrueMoon.Thorium.IO.Pipes;

public static class ThoriumConfigurationContextExtensions
{
    public static IThoriumConfigurationContext Pipes(this IThoriumConfigurationContext context)
    {
        context.AppConfigurationContext.AddDependencies(registrationContext => registrationContext
            .RemoveAll<IInvocationClientFactory>()
            .RemoveAll<IInvocationServerFactory>()
            .Add<IInvocationClientFactory, PipesInvocationClientFactory>()
            .Add<IInvocationServerFactory, PipesInvocationServerFactory>()
        );
        return context;
    }
}