namespace TrueMoon.Titanium;

public interface ITitaniumModule : IModule
{
    void AddUnitConfiguration(Action<IAppCreationContext> action,
        Action<IUnitConfiguration>? configureAction = default);
}