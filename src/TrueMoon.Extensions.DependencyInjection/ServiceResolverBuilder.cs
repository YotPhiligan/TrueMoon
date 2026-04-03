using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServiceResolverBuilder : IServiceResolverBuilder
{
    public IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration,IServicesRegistrationContext>> registrations)
    {
        var ctx = new ServicesRegistrationContext();
        foreach (var registration in registrations)
        {
            registration(configuration, ctx);
        }

        var serviceCollection = ctx.GetServiceCollection();
        
        var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = Debugger.IsAttached });
        var resolver = new ServiceResolver(serviceProvider);
        
        return resolver;
    }
}