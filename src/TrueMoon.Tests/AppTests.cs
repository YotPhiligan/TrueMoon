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
        await using var app = App.Create(context => context
            .UseDI()
            .AddDependencies(registrationContext => registrationContext
                .Add<ICommonService1, CommonService1>()
                .Add<CommonStartableService>(v=>v.With<IStartable>())
            )
        );

        await app.StartAsync();

        var service = app.Services.Resolve<IStartable>();

        Assert.IsType<CommonStartableService>(service);

        Assert.True((service as CommonStartableService).IsStarted);
    }
    
    [Fact]
    public void Create()
    {
        using var app = App.Create(context => context.UseDI());
        Assert.NotNull(app);
        Assert.NotEmpty(app.Name);
    }

    [Fact]
    public async Task RunAsync()
    {
        await App.RunAsync(context => context
            .UseDI()
            .AddDependencies(t=>t.Add<LifeTimeExecutor>(d=>d.With<IStartable>()))
        );
        
        Assert.True(true);
    }
    
    [Fact]
    public async Task RunAsyncWithConfigurator()
    {
        await App.RunAsync<Configurator>();
        
        Assert.True(true);
    }
    
    // [Fact]
    // public async Task AppCreateEnclave()
    // {
    //     await using var app = App.Create(context => context
    //         .UseDependencyInjection<DependencyInjectionProvider>()
    //         .AddCommonDependencies(registrationContext => registrationContext.Add<ICommonService1, CommonService1>())
    //         .AddProcessingEnclave(configurationContext => configurationContext
    //             .AddDependencies(registrationContext => registrationContext.Add<IService1, Service1>())
    //         )
    //     );
    //
    //     await app.StartAsync();
    // }
}