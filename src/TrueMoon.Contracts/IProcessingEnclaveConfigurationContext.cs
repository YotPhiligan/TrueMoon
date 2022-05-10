using System.Diagnostics.CodeAnalysis;
using TrueMoon.Dependencies;

namespace TrueMoon;

public interface IProcessingEnclaveConfigurationContext
{
    IProcessingEnclaveConfigurationContext AddDependencies(Action<IDependenciesRegistrationContext> action);
}