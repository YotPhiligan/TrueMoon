namespace TrueMoon.Services;

public interface IServiceResolverBuilder
{
    IServiceResolver Build(IEnumerable<Action<IServicesRegistrationContext>> registrations); 
}