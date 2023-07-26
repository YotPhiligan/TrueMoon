using TrueMoon.Dependencies;

namespace TrueMoon.Thorium.IO;

public class SignalServerHandlerResolver : ISignalServerHandlerResolver
{
    private readonly IServiceProvider _serviceProvider;

    public SignalServerHandlerResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    public ISignalServerHandler<T> Resolve<T>() => _serviceProvider.Resolve<ISignalServerHandler<T>>();
}