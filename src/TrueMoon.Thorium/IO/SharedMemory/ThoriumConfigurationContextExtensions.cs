using TrueMoon.Dependencies;

namespace TrueMoon.Thorium.IO.SharedMemory;

public static class ThoriumConfigurationContextExtensions
{
    public static IThoriumConfigurationContext Memory(this IThoriumConfigurationContext context)
    {
        context.AppCreationContext.AddDependencies(registrationContext => registrationContext
            .RemoveAll<ISignalClientFactory>()
            .RemoveAll<ISignalServerFactory>()
            .Add<ISignalClientFactory, MemorySignalClientFactory>()
            .Add<ISignalServerFactory, MemorySignalServerFactory>()
            .Add<ISignalsHandleFactory, MemorySignalsHandleFactory>()
        );
        return context;
    }
}