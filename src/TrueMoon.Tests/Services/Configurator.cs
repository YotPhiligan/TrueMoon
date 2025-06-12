using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.Tests.Services;

public class Configurator
{
    public void Configure(IAppConfigurationContext context)
    {
        context.Services(t => t.Singleton<IStartable,LifeTimeExecutor>());
    }
}