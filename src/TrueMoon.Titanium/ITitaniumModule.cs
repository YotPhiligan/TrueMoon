using TrueMoon.Modules;

namespace TrueMoon.Titanium;

public interface ITitaniumModule : IModule
{
    void AddUnitConfiguration(Action<IAppConfigurationContext> action,
        Action<IUnitConfiguration>? configureAction = default);
}