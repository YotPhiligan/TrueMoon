using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServiceResolverBuilder : IServiceResolverBuilder
{
    public IServiceResolver Build(IEnumerable<Action<IServicesRegistrationContext>> registrations)
    {
        var ctx = new CobaltServicesRegistrationContext();
        foreach (var registration in registrations)
        {
            registration(ctx);
        }
        
        var container = ctx.GetContainer();
        var resolver = new CobaltServiceResolver(container);
        
        return resolver;
    }
}