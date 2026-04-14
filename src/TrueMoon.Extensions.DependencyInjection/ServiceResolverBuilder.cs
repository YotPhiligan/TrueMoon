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
        
        if (registrations is IReadOnlyList<Action<IConfiguration, IServicesRegistrationContext>> list)
        {
            var index = 0;
            while (index >= 0 && index < list.Count)
            {
                var registration = list[index];
                registration(configuration, ctx);

                index++;
            }
        }
        else
        {
            foreach (var registration in registrations)
            {
                registration(configuration, ctx);
            }
        }

        var serviceCollection = ctx.GetServiceCollection();
        
        serviceCollection.AddSingleton<IServiceResolver,ServiceResolver>();
        
        var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = Debugger.IsAttached });
        var resolver = new ServiceResolver(serviceProvider);
        
        return resolver;
    }
}