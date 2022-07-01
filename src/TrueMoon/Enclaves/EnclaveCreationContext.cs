using TrueMoon.Dependencies;

namespace TrueMoon.Enclaves;

/// <inheritdoc />
public class EnclaveCreationContext : IAppCreationContext, IServiceProviderBuilder
{
    private readonly IDependenciesRegistrationContext _dependenciesRegistrationContext;
    private Action<IProcessingEnclaveConfigurationContext>? _action;
    private readonly int _id;
    private int _enclavesCount;
    private IDependencyInjectionProvider _dependencyInjectionProvider;

    public EnclaveCreationContext(IAppParameters parameters,
        IDependenciesRegistrationContext dependenciesRegistrationContext)
    {
        _dependenciesRegistrationContext = dependenciesRegistrationContext;
        Parameters = parameters;

        _id = Parameters.GetEnclaveId() ?? default;
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
        if (_action != null)
        {
            return this;
        }
        
        if (_id == _enclavesCount)
        {
            _action = action;
        }
        _enclavesCount++;
        
        return this;
    }

    public IAppCreationContext UseDependencyInjection<T>(T? container = default) where T : IDependencyInjectionProvider
    {
        IDependencyInjectionProvider c = container ?? Activator.CreateInstance<T>();

        _dependencyInjectionProvider = c;
        return this;
    }

    public string Name { get; set; }
    
    public IServiceProvider Build()
    {
        var descriptors = _dependenciesRegistrationContext.GetDescriptors();
        return _dependencyInjectionProvider?.GetServiceProvider(descriptors) ?? throw new InvalidOperationException();
    }
}