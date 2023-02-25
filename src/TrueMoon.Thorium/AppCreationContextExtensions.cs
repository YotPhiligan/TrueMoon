using TrueMoon.Dependencies;
using TrueMoon.Thorium.Handlers;

namespace TrueMoon.Thorium;

public static class AppCreationContextExtensions
{
    public static IAppCreationContext UseSignals(this IAppCreationContext context, Action<ThoriumConfiguration>? action = default)
    {
        var thorium = context.GetThorium();
        
        var configuration = new ThoriumConfiguration();
        action?.Invoke(configuration);
        
        thorium.SetConfiguration(configuration);

        return context;
    }
    
    public static IThoriumModule GetThorium(this IAppCreationContext context)
    {
        var module = context.GetModule<IThoriumModule>();
        if (module is not null) return module;
        module = new ThoriumModule(context.CreateEventsSource<ThoriumModule>());
        context.AddModule(module);
        return module;
    }

    public static IAppCreationContext ListenSignal<TMessage>(this IAppCreationContext context)
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandleWrapper<TMessage>,SignalHandleWrapper<TMessage>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext OnSignal<TMessage>(this IAppCreationContext context, Action<TMessage> dataCallback)
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage>>(_ => new DelegateSignalHandler<TMessage>(dataCallback))
            .Add<ISignalHandleWrapper<TMessage>,SignalHandleWrapper<TMessage>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext OnSignal<TMessage>(this IAppCreationContext context, Action<TMessage,IServiceProvider> dataCallback)
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage>>(s => new DelegateSignalHandler<TMessage>(dataCallback, () => s))
            .Add<ISignalHandleWrapper<TMessage>,SignalHandleWrapper<TMessage>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }

    public static IAppCreationContext ListenSignal<TMessage,TResponse>(this IAppCreationContext context)
        where TMessage : IWithResponse<TResponse>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandleWrapper<TMessage,TResponse>,SignalHandleWrapper<TMessage,TResponse>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext OnSignal<TMessage,TResponse>(this IAppCreationContext context, Func<TMessage,TResponse> dataCallback)
        where TMessage : IWithResponse<TResponse>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage,TResponse>>(_ => new DelegateSignalHandler<TMessage,TResponse>(dataCallback))
            .Add<ISignalHandleWrapper<TMessage,TResponse>,SignalHandleWrapper<TMessage,TResponse>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }

    public static IAppCreationContext OnSignal<TMessage,TResponse>(this IAppCreationContext context, Func<TMessage,IServiceProvider,TResponse> dataCallback)
        where TMessage : IWithResponse<TResponse>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage,TResponse>>(s => new DelegateSignalHandler<TMessage,TResponse>(dataCallback,()=>s))
            .Add<ISignalHandleWrapper<TMessage,TResponse>,SignalHandleWrapper<TMessage,TResponse>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext SignalHandler<TMessage,TMessageHandler>(this IAppCreationContext context)
        where TMessageHandler : ISignalHandler<TMessage>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage>,TMessageHandler>()
            .Add<ISignalHandleWrapper<TMessage>,SignalHandleWrapper<TMessage>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext SignalHandler<TMessage,TResponse,TMessageHandler>(this IAppCreationContext context)
        where TMessage : IWithResponse<TResponse> 
        where TMessageHandler : ISignalHandler<TMessage,TResponse>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<ISignalHandler<TMessage,TResponse>,TMessageHandler>()
            .Add<ISignalHandleWrapper<TMessage,TResponse>,SignalHandleWrapper<TMessage,TResponse>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext AsyncSignalHandler<TMessage,TMessageHandler>(this IAppCreationContext context)
        where TMessageHandler : IAsyncSignalHandler<TMessage>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<IAsyncSignalHandler<TMessage>,TMessageHandler>()
            .Add<ISignalHandleWrapper<TMessage>,SignalHandleWrapper<TMessage>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
    
    public static IAppCreationContext AsyncSignalHandler<TMessage,TResponse,TMessageHandler>(this IAppCreationContext context)
        where TMessage : IWithResponse<TResponse> 
        where TMessageHandler : IAsyncSignalHandler<TMessage,TResponse>
    {
        var module = context.GetThorium();
        context.AddDependencies(d => d
            .Add<IAsyncSignalHandler<TMessage,TResponse>,TMessageHandler>()
            .Add<ISignalHandleWrapper<TMessage,TResponse>,SignalHandleWrapper<TMessage,TResponse>>()
        );
        module.ListenMessage<TMessage>();
        return context;
    }
}