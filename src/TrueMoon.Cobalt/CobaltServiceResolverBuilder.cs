using System.Collections.Frozen;
using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServiceResolverBuilder : IServiceResolverBuilder
{
    public IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration, IServicesRegistrationContext>> registrations)
    {
        var context = new CobaltServicesRegistrationContext();
        foreach (var registration in registrations)
            registration(configuration, context);
        var containers = new List<ITypeContainer>();
        foreach (var group in context.GetContainer().GetHandles().Select((handle, order) => (Handle: handle, Order: order))
                     .GroupBy(item => item.Handle.ServiceType))
        {
            var container = new TypeContainer(group.Key);
            foreach (var item in group)
            {
                var handle = item.Handle;
                container.Add(handle.Resolver ?? ServiceResolvers.Shared.GetFactory(handle.ServiceType,
                    handle.ImplementationType ?? handle.ServiceType, handle.Lifetime), item.Order);
            }
            containers.Add(container);
        }
        return new CobaltServiceResolver(containers.ToFrozenDictionary(c => c.Type));
    }
}
