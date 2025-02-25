namespace TrueMoon.Cobalt;

public interface IServicesRegistrationAccessor
{
    TInstance? GetInstance<TInstance>();
    IFactoryContainer<TService> GetFactory<TService>();
}