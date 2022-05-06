using TrueMoon.Dependencies;

namespace TrueMoon;

public interface IAppCreationContext
{
    IAppCreationContext AddCommonDependencies(Action<IDependenciesRegistrationContext> action);
    IAppCreationContext AddStandaloneService(Action<IServiceConfigurationContext> action);
    IAppCreationContext AddDependencies<T>(Action<T> action);
}