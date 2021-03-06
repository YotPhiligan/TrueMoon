using TrueMoon.Dependencies;

namespace TrueMoon;

/// <inheritdoc />
public class AppCreationContext : IAppCreationContext, IServiceProviderBuilder
{
    private readonly IDependenciesRegistrationContext _dependenciesRegistrationContext;
    private readonly List<Action<IProcessingEnclaveConfigurationContext>> _enclavesActions = new ();
    private IDependencyInjectionProvider? _dependencyInjectionProvider;

    public AppCreationContext(IAppParameters parameters,
        IDependenciesRegistrationContext dependenciesRegistrationContext)
    {
        _dependenciesRegistrationContext = dependenciesRegistrationContext;
        Parameters = parameters;
    }

    public IAppParameters Parameters { get; }

    /// <inheritdoc />
    public IAppCreationContext AddCommonDependencies(Action<IDependenciesRegistrationContext> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        action(_dependenciesRegistrationContext);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext AddProcessingEnclave(Action<IProcessingEnclaveConfigurationContext> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        _enclavesActions.Add(action);
        return this;
    }

    public IAppCreationContext UseDependencyInjection<T>(T? container = default) where T : IDependencyInjectionProvider
    {
        IDependencyInjectionProvider c = container ?? Activator.CreateInstance<T>();

        _dependencyInjectionProvider = c;
        return this;
    }

    public IServiceProvider Build()
    {
        var descriptors = _dependenciesRegistrationContext.GetDescriptors();
        return _dependencyInjectionProvider?.GetServiceProvider(descriptors) ?? throw new InvalidOperationException();
    }
}