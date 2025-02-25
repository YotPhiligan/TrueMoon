namespace TrueMoon.Modules;

public class ModulesConfigurationContext : IModuleConfigurationContext
{
    private readonly List<IModule> _modules = [];
    
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
}