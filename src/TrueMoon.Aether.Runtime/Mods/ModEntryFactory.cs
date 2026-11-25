using System.Reflection;
using TrueMoon.Aether.Mods;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Ephemeral factory arguments. Implementations must not cache the collectible entry Type or returned instance.</summary>
public sealed record ModEntryArguments(Type EntryType, IModHostServices Services);

/// <summary>Default typed entry-point activation. Reflection is confined to this factory; there is no global Type cache.</summary>
public sealed class ModEntryFactory : IFactory<IModEntry, ModEntryArguments>
{
    /// <summary>Invokes the static SDK entry factory.</summary><param name="arguments">Collectible type and temporary host services.</param><returns>Exclusively group-owned entry.</returns>
    public IModEntry Create(ModEntryArguments arguments)
    {
        var type = arguments.EntryType;
        if (!type.IsPublic || type.IsAbstract || type.ContainsGenericParameters || !typeof(IModEntryPoint).IsAssignableFrom(type))
            throw new ModFailure(ModLoadError.InvalidEntry, "Entry type must be public, concrete and implement IModEntryPoint.");
        return (IModEntry)(typeof(ModEntryFactory).GetMethod(nameof(Activate), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type).Invoke(null, [arguments.Services]) ?? throw new ModFailure(ModLoadError.InvalidEntry, "Entry factory returned null."));
    }
    private static IModEntry Activate<T>(IModHostServices services) where T : IModEntryPoint => T.Create(services);
}

internal sealed class ModHostServices(IServiceResolver resolver, IReadOnlyCollection<Assembly> shared) : IModHostServices
{
    private IModGeneration? _generation;
    internal void BindGeneration(IModGeneration generation) => _generation = generation;
    private IServiceResolver? _resolver = resolver;
    private IReadOnlyCollection<Assembly>? _shared = shared;
    public T GetRequired<T>() where T : class
    {
        var current = _resolver ?? throw new InvalidOperationException("Module composition has ended.");
        if (typeof(T) == typeof(IModGeneration)) return (T)_generation!;
        if (!Stable(typeof(T))) throw new InvalidOperationException("Module types cannot enter host DI resolution, including generic arguments.");
        var value = current.GetService(typeof(T)) as T ?? throw new InvalidOperationException("Required stable host service is not registered.");
        if (Root(value.GetType())) throw new InvalidOperationException("Application/bootstrap/container owners cannot be passed to a module.");
        return value;
    }
    private static bool Root(Type type) => typeof(IServiceProvider).IsAssignableFrom(type) || typeof(IApp).IsAssignableFrom(type) ||
        typeof(IAppBuilder).IsAssignableFrom(type) || typeof(IAppConfigurationContext).IsAssignableFrom(type) ||
        typeof(IAppBuilderConfigurationContext).IsAssignableFrom(type) || typeof(IServicesRegistrationContext).IsAssignableFrom(type);
    private bool Stable(Type type) => !Root(type) && (_shared!.Contains(type.Assembly) ||
        ModArtifactGroup.Framework.ContainsKey(type.Assembly.GetName().Name!) && System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(type.Assembly) == System.Runtime.Loader.AssemblyLoadContext.Default) &&
        (!type.HasElementType || Stable(type.GetElementType()!)) && type.GetGenericArguments().All(Stable);
    internal void Seal() { _resolver = null; _shared = null; _generation = null; }
}
