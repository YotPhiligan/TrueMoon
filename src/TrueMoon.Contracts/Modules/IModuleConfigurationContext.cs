namespace TrueMoon.Modules;

public interface IModuleConfigurationContext
{
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
}