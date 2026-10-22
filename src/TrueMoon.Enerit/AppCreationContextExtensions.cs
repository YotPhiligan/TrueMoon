using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit;

public static class AppCreationContextExtensions
{
    public static IAppConfigurationContext UseInvocationService<T>(this IAppConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services(r =>
        {
            if (r.Exist(typeof(T))) return;
            InvocationServiceStorage.Shared.ConfigureService<T>(r);
            if (!r.Exist<IInvocationClient<T>>())
            {
                if (!r.Exist<IFactory<IInvocationClient<T>>>())
                    r.Singleton<IFactory<IInvocationClient<T>>>(p => new Factory<IInvocationClient<T>>(() => p.Resolve<IInvocationClientFactory>().Create<T>()));
                r.Singleton<IInvocationClient<T>>(p => p.Resolve<IFactory<IInvocationClient<T>>>().Create()!);
                r.Singleton<InvocationTransportLifetime<IInvocationClient<T>>>(p => new(p.Resolve<IInvocationClient<T>>()));
                r.Singleton<IStartable>(p => p.Resolve<InvocationTransportLifetime<IInvocationClient<T>>>());
                r.Singleton<IStoppable>(p => p.Resolve<InvocationTransportLifetime<IInvocationClient<T>>>());
            }
        });
    }

    public static IAppConfigurationContext ListenInvocationService<T, TService>(this IAppConfigurationContext context) where TService : class, T
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services(r =>
        {
            if (!r.Exist(typeof(T))) r.Singleton(typeof(T), typeof(TService));
            if (!r.Exist<IInvocationServerHandler<T>>()) InvocationServiceStorage.Shared.ConfigureHandler<T>(r);
            if (!r.Exist<IInvocationServer<T>>())
            {
                if (!r.Exist<IFactory<IInvocationServer<T>>>())
                    r.Singleton<IFactory<IInvocationServer<T>>>(p => new Factory<IInvocationServer<T>>(() => p.Resolve<IInvocationServerFactory>().Create<T>()));
                r.Singleton<IInvocationServer<T>>(p => p.Resolve<IFactory<IInvocationServer<T>>>().Create()!);
                r.Singleton<InvocationTransportLifetime<IInvocationServer<T>>>(p => new(p.Resolve<IInvocationServer<T>>()));
                r.Singleton<IStartable>(p => p.Resolve<InvocationTransportLifetime<IInvocationServer<T>>>());
                r.Singleton<IStoppable>(p => p.Resolve<InvocationTransportLifetime<IInvocationServer<T>>>());
            }
        });
    }
}

internal sealed class InvocationTransportLifetime<TTransport>(TTransport transport) : IStartable, IStoppable
    where TTransport : class
{
    public Task StartAsync(CancellationToken cancellationToken = default)
        => transport is IStartable startable ? startable.StartAsync(cancellationToken) : Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (transport is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (transport is IDisposable disposable) disposable.Dispose();
        else if (transport is IStoppable stoppable) await stoppable.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
