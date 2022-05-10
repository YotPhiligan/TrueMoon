using TrueMoon.Dependencies;

namespace TrueMoon;

public interface IAppCreationContext
{
    IAppParameters Parameters { get; }
    IAppCreationContext AddCommonDependencies(Action<IDependenciesRegistrationContext> action);
    IAppCreationContext AddProcessingEnclave(Action<IProcessingEnclaveConfigurationContext> action);
    IAppCreationContext UseDependencyInjection<T>(T? container = default) where T : IDependencyInjectionProvider;
}