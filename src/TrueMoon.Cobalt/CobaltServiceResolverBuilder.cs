using System.Collections.Frozen;
using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServiceResolverBuilder : IServiceResolverBuilder
{
    public IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration, IServicesRegistrationContext>> registrations)
    {
        var ctx = new CobaltServicesRegistrationContext();
        foreach (var registration in registrations)
        {
            registration(configuration, ctx);
        }
        
        var servicesRegistrationContainer = ctx.GetContainer();
        
        var containers = new List<ITypeContainer>();
        var handles = servicesRegistrationContainer.GetHandles();
        var resolverFactories = ServiceResolvers.Shared.GetResolvers();
        
        foreach (var grouping in handles.GroupBy(t=>t.ServiceType))
        {
            var type = grouping.Key;
            var container = new TypeContainer(type);
            foreach (var handle in grouping)
            {
                if (handle.Resolver != null)
                {
                    container.Add(handle.Resolver);
                }
                else if (handle.ImplementationType != null)
                {
                    var resolver = resolverFactories[handle.ServiceType];
                    container.Add(resolver);
                }
                else if (handle.ServiceType is { IsInterface: false })
                {
                    var resolver = resolverFactories[handle.ServiceType];
                    container.Add(resolver);
                }
            }
            containers.Add(container);
        }
        
        var resolversContainer = containers.ToFrozenDictionary(t=>t.Type, t=>t);
        
        var serviceResolver = new CobaltServiceResolver(resolversContainer);
        
        return serviceResolver;
    }
}