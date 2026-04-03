using TrueMoon.Configuration;

namespace TrueMoon.Services;

public interface IServiceResolverBuilder
{
    IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration,IServicesRegistrationContext>> registrations); 
}