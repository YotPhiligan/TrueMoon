namespace TrueMoon.Tests.Services;

public class Configurator
{
    public void Configure(IAppConfigurationContext context)
        => context.Services(services => services.Singleton<IStartable, LifeTimeExecutor>());
}
