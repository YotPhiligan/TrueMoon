using TrueMoon.Dependencies;

namespace TrueMoon.Thorium.IO.SharedMemory;

public static class ThoriumConfigurationContextExtensions
{
    public static IThoriumConfigurationContext Memory(this IThoriumConfigurationContext context, Action<SignalsMemoryConfiguration>? action = default)
    {
        var ctx = new SignalsMemoryConfiguration();
        action?.Invoke(ctx);
        
        context.AppCreationContext.AddDependencies(registrationContext => registrationContext
            .RemoveAll<ISignalClientFactory>()
            .RemoveAll<ISignalServerFactory>()
            .Add<ISignalClientFactory, MemorySignalClientFactory>()
            .Add<ISignalServerFactory, MemorySignalServerFactory>()
            .Add<ISignalsHandleFactory, MemorySignalsHandleFactory>()
            .Add<SignalsTaskScheduler>()
            .Add(ctx)
        );
        return context;
    }
}