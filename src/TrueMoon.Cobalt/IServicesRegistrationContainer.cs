using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IServicesRegistrationContainer
{
    void RegisterInstance<TInstance>(TInstance instance);
    void RegisterFactory<TService>(Func<IServiceResolver, TService> factory);
}