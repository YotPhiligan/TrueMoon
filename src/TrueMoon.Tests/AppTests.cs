using System.Threading.Tasks;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Tests.Services;
using Xunit;

namespace TrueMoon.Tests;

public class AppTests
{
    [Fact]
    public async Task AppCreate()
    {
        await using var app = App.Build(context => context
            .Services(registrationContext => registrationContext
                .Singleton<ICommonService1, CommonService1>()
                .Singleton<IStartable,CommonStartableService>()
            )
        );

        await app.StartAsync();

        var service = app.Services.Resolve<IStartable>();
        var services = app.Services.ResolveAll<IStartable>();

        var service2 = services.FirstOrDefault(t => t.GetType() == typeof(CommonStartableService));

        Assert.IsType<CommonStartableService>(service2);

        Assert.True((service2 as CommonStartableService).IsStarted);
    }
    
    [Fact]
    public void Create()
    {
        using var app = App.Build();
        Assert.NotNull(app);
        Assert.NotEmpty(app.Name);
    }

    [Fact]
    public async Task RunAsync()
    {
        await App.RunAsync(context => context
            .Services(t=>t.Singleton<IStartable,LifeTimeExecutor>())
        );
        
        Assert.True(true);
    }
    
    [Fact]
    public async Task RunAsyncWithConfigurator()
    {
        await App.RunAsync<Configurator>();
        
        Assert.True(true);
    }
}