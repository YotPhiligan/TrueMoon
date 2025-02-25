using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IFactoryContainer
{
    string Id { get; }
}

public interface IFactoryContainer<TService> : IFactoryContainer
{
    Func<IServiceResolver, TService> Get();
}