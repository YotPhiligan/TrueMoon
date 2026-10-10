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
        foreach (var group in context.GetContainer().GetHandles().GroupBy(h => h.ServiceType))
        {
            var container = new TypeContainer(group.Key);
            foreach (var handle in group)
                container.Add(handle.Resolver ?? ServiceResolvers.Shared.GetFactory(handle.ServiceType,
                    handle.ImplementationType ?? handle.ServiceType, handle.Lifetime));
            containers.Add(container);
        }
        return new CobaltServiceResolver(containers.ToFrozenDictionary(c => c.Type));
    }
}
