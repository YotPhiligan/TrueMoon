using TrueMoon.Dependencies;
using TrueMoon.Enclaves;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class App
{
    public static IApp Create(Action<IAppCreationContext> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        
        var dependenciesContext = new DependenciesRegistrationContext();
        
        IAppParameters parameters = new AppParameters();

        var isEnclave = parameters.IsEnclave();
        IAppCreationContext ctx = isEnclave 
            ? new EnclaveCreationContext(parameters, dependenciesContext) 
            : new AppCreationContext(parameters, dependenciesContext);
        
        if (isEnclave)
        {
            ctx.AddCommonDependencies(t=>t.Add<IApp,EnclaveApp>());
        }
        else
        {
            ctx.AddCommonDependencies(t=>t.Add<IApp,KeeperApp>());
        }
        
        ctx.AddCommonDependencies(t=>t.Add(parameters));

        ctx.UseDependencyInjection<SimpleDependencyInjectionProvider>();
        
        try
        {
            action(ctx);
        }
        catch (Exception e)
        {
            throw new AppCreationException("Failed to create the app", e);
        }

        var serviceProvider = (ctx as IServiceProviderBuilder).Build();

        return serviceProvider.Resolve<IApp>() ?? throw new AppCreationException($"Failed to instantiate the \"{nameof(IApp)}\"");
    }
}