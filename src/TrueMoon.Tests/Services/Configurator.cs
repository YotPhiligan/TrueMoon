using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.Tests.Services;

public class Configurator
{
    public void Configure(IAppConfigurationContext context)
    {
        context.UseDI();
        context.AddDependencies(t => t.Add<LifeTimeExecutor>(d => d.With<IStartable>()));
    }
}