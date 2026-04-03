using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseInvocationService<T>(this IAppConfigurationContext context) =>
        context.Services(ctx =>
        {
            var serviceType = typeof(T);
            var implementationType = InvocationServiceStorage.Shared.GetService<T>();
         
            if (ctx.Exist(serviceType, implementationType))
            {
                return;
            }
            
            ctx.Singleton(serviceType, implementationType);
            ctx.Singleton(provider =>
            {
                var factory = provider.Resolve<IInvocationClientFactory>();
                return factory.Create<T>();
            });
        });

    public static IAppConfigurationContext ListenInvocationService<T,TService>(this IAppConfigurationContext context)
        where TService: class, T =>
        context.Services(ctx =>
        {
            if (ctx.Exist<T,TService>())
            {
                return;
            }
            
            var handlerTypeAbstraction = typeof(IInvocationServerHandler<T>);
            var handlerType = InvocationServiceStorage.Shared.GetHandler<T>();
            var serviceType = typeof(T);
            var implementationType = typeof(TService);
            
            ctx.Singleton(serviceType, implementationType);
            ctx.Singleton(handlerTypeAbstraction, handlerType);
            
            ctx.Singleton<IStartable>(provider =>
            {
                var factory = provider.Resolve<IInvocationServerFactory>();
                return factory.Create<T>();
            });
        });
}