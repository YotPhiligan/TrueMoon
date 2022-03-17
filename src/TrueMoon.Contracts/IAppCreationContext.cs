using TrueMoon.Dependencies;

namespace TrueMoon;

public interface IAppCreationContext
{
    IAppCreationContext AddService(Action<IServiceConfigurationContext> action);
    IAppCreationContext ConfigureDependecies<T>(Action<T> action);
}