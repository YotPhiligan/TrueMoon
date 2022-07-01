using System.Threading.Tasks;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public class AppTests
{
    [Fact]
    public async Task AppCreate()
    {
        await using var app = App.Create(context => context
            .UseDependencyInjection<DependencyInjectionProvider>()
            .AddCommonDependencies(registrationContext => registrationContext
                .Add<ICommonService1, CommonService1>()
                .Add<CommonStartableService>(v=>v.With<IStartable>())
            )
        );

        await app.StartAsync();

        var startableService = app.Services.Resolve<IStartable>();

        Assert.IsType<CommonStartableService>(startableService);

        Assert.True((startableService as CommonStartableService).IsStarted);
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

public interface IService1
{
    
}

public class Service1 : IService1
{
    
}

public interface ICommonService1
{
    
}

public class CommonService1 : ICommonService1
{
    
}

public class CommonStartableService : IStartable
{
    public bool IsStarted { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        return Task.CompletedTask;
    }
}