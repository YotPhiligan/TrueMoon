namespace TrueMoon.Cobalt;

public interface IUnboundResolversContainer : IResolversContainerBase
{
    void Add(List<Func<IServicesRegistrationAccessor,IUnboundGenericResolver>> resolverFactories);
    IUnboundGenericResolver GetLastResolver(IServicesRegistrationAccessor registrationAccessor);
}