using TrueMoon.Dependencies;

namespace TrueMoon;

/// <inheritdoc />
public class AppCreationContext : IAppCreationContext
{
    private readonly object? _rawDiContainerObject;
    private readonly List<Action<IDependenciesRegistrationContext>> _commonDependenciesActions = new ();
    private readonly List<Action<IServiceConfigurationContext>> _standaloneServicesActions = new ();

    /// <inheritdoc />
    public IAppCreationContext AddCommonDependencies(Action<IDependenciesRegistrationContext> action)
    {
        _commonDependenciesActions.Add(action);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext AddStandaloneService(Action<IServiceConfigurationContext> action)
    {
        _standaloneServicesActions.Add(action);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext AddDependencies<T>(Action<T> action)
    {
        if (_rawDiContainerObject is T di)
        {
            action(di);
        }

        return this;
    }
}