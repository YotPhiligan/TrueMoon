namespace TrueMoon.Cobalt;

public interface ITypedResolversContainer
{
    public string TypeId { get; }
    public Type EnumerableType { get; }
    
    IResolver[] GetResolvers(IServicesRegistrationAccessor accessor);
    IResolver GetLastResolver(IServicesRegistrationAccessor accessor);
    IResolver GetFirstResolver(IServicesRegistrationAccessor accessor);
}

public interface ITypedResolversContainer<TService> : ITypedResolversContainer
{
    void Add(List<Func<IServicesRegistrationAccessor,IResolver<TService>>> resolverFactories);
    IResolver<TService>[] GetTypedResolvers(IServicesRegistrationAccessor accessor);
    
    IResolver<TService> GetLastResolver(IServicesRegistrationAccessor accessor);
    IResolver<TService> GetFirstResolver(IServicesRegistrationAccessor accessor);
}