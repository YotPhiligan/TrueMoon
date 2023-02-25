using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <inheritdoc />
public class AppCreationContext : IAppCreationContext, IServiceProviderBuilder
{
    private readonly IDependenciesRegistrationContext _dependenciesRegistrationContext;
    private IDependencyInjectionProvider? _dependencyInjectionProvider;
    private readonly List<IModule> _modules = new ();

    private readonly IEventsSourceFactory _eventsSourceFactory = new EventsSourceFactory();
    
    public AppCreationContext(IConfiguration parameters,
        IDependenciesRegistrationContext dependenciesRegistrationContext)
    {
        _dependenciesRegistrationContext = dependenciesRegistrationContext;
        Configuration = parameters;
    }

    /// <inheritdoc />
    public IConfiguration Configuration { get; }

    /// <inheritdoc />
    public IAppCreationContext AddConfigurationProvider<T>(T? provider = default) where T : IConfigurationProvider
    {
        IConfigurationProvider p = provider ?? Activator.CreateInstance<T>();
        Configuration.AddProvider(p);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext RemoveConfigurationProvider<T>(T? provider = default) where T : IConfigurationProvider
    {
        Configuration.RemoveProvider(provider);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext AddDependencies(Action<IDependenciesRegistrationContext> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        action(_dependenciesRegistrationContext);
        return this;
    }

    /// <inheritdoc />
    public IAppCreationContext UseProvider<T>(T? container = default) where T : IDependencyInjectionProvider
    {
        IDependencyInjectionProvider c = container ?? Activator.CreateInstance<T>();

        _dependencyInjectionProvider = c;
        return this;
    }

    /// <inheritdoc />
    public T? GetModule<T>() where T : IModule => _modules.FirstOrDefault(t=>t is T) is T module 
        ? module 
        : default;

    /// <inheritdoc />
    public void AddModule<T>(T? module = default) where T : IModule
    {
        module ??= Activator.CreateInstance<T>();
        _modules.Add(module);
    }

    /// <inheritdoc />
    public IReadOnlyList<IModule> GetModules() => _modules;

    public IEventsSource<T> CreateEventsSource<T>() => _eventsSourceFactory.Create<T>();
    public IAppCreationContext Configure(Action<IConfiguration> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action(Configuration);
        return this;
    }

    /// <inheritdoc />
    public IServiceProvider Build()
    {
        var descriptors = _dependenciesRegistrationContext.GetDescriptors();
        return _dependencyInjectionProvider?.GetServiceProvider(descriptors) ?? throw new InvalidOperationException();
    }
}