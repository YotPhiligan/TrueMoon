using TrueMoon.Dependencies;

namespace TrueMoon;

public interface IServiceConfigurationContext
{
    IDependenciesRegistrationContext Dependencies { get; }
}