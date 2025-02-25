using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class FactoryContainer<TService>(Func<IServiceResolver, TService> factory) : IFactoryContainer<TService> 
{
    public Func<IServiceResolver, TService> Get() => factory;

    public string Id { get; } = TypeUtils.GetTypeId<TService>();
}