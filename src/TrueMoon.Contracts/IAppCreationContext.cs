using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon;

/// <summary>
/// app creation context
/// </summary>
public interface IAppCreationContext
{
    /// <summary>
    /// App configuration
    /// </summary>
    IConfiguration Configuration { get; }
    /// <summary>
    /// Add configuration provider
    /// </summary>
    /// <param name="provider">instance of the configuration provider, may be null</param>
    /// <typeparam name="T">type of the configuration provider</typeparam>
    /// <returns></returns>
    IAppCreationContext AddConfigurationProvider<T>(T? provider = default) where T : IConfigurationProvider;
    /// <summary>
    /// Remove configuration provider
    /// </summary>
    /// <param name="provider">instance of the configuration provider, may be null</param>
    /// <typeparam name="T">type of the configuration provider</typeparam>
    /// <remarks>In case when instance is null, search will be applied by given type argument</remarks>
    /// <returns></returns>
    IAppCreationContext RemoveConfigurationProvider<T>(T? provider = default) where T : IConfigurationProvider;
    /// <summary>
    /// Add dependencies to be used in the app
    /// </summary>
    /// <param name="action">dependencies configuration delegate</param>
    /// <returns></returns>
    IAppCreationContext AddDependencies(Action<IDependenciesRegistrationContext> action);
    /// <summary>
    /// Replace dependency injection provider
    /// </summary>
    /// <param name="container">instance of dependency injection provider, may be null</param>
    /// <typeparam name="T">type of the dependency injection provider</typeparam>
    /// <returns></returns>
    IAppCreationContext UseProvider<T>(T? container = default) where T : IDependencyInjectionProvider;
    
    /// <summary>
    /// Get module
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T? GetModule<T>() where T : IModule;
    /// <summary>
    /// Add module
    /// </summary>
    /// <param name="module"></param>
    /// <typeparam name="T"></typeparam>
    void AddModule<T>(T? module = default) where T : IModule;
    /// <summary>
    /// Get modules list
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<IModule> GetModules();

    IEventsSource<T> CreateEventsSource<T>();

    IAppCreationContext Configure(Action<IConfiguration> action);
}