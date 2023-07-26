namespace TrueMoon.Thorium;

public interface IThoriumModule : IModule
{
    void SetConfiguration(ThoriumConfiguration configuration);
    ThoriumConfiguration GetConfiguration();
    void UseService<T>();
    void ListenService<T, TService>() where TService : class, T;
    void SetConfigurationDelegate(Action<IThoriumConfigurationContext>? action);
}