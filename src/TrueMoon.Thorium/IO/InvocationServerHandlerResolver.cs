using TrueMoon.Dependencies;

namespace TrueMoon.Thorium.IO;

public class InvocationServerHandlerResolver : IInvocationServerHandlerResolver
{
    private readonly IServiceProvider _serviceProvider;

    public InvocationServerHandlerResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    public IInvocationServerHandler<T> Resolve<T>() => _serviceProvider.Resolve<IInvocationServerHandler<T>>();
}