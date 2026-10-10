using TrueMoon.Dependencies;
using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Tests.Services;
using Xunit;

namespace TrueMoon.Tests;

public class AppTests
{
    [Fact]
    public async Task AppCreate()
    {
        await using var app = App.Build(context => context.Services(services => services
            .Singleton<ICommonService1, CommonService1>()
            .Singleton<IStartable, CommonStartableService>()));
        await app.StartAsync();
        var service = Assert.IsType<CommonStartableService>(Assert.Single(app.Services.ResolveAll<IStartable>()));
        Assert.True(service.IsStarted);
        Assert.Same(app.Services.Resolve<IAppLifetime>(), app.Services.Resolve<IAppLifetimeHandler>());
        await app.StopAsync();
    }

    [Fact]
    public void Create()
    {
        using var app = App.Build(context => context.Configuration(configuration => configuration.Set("appName", "CoreSmoke")));
        Assert.Equal("CoreSmoke", app.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync(bool microsoftDI)
    {
        LifeTimeExecutor? executor = null;
        await App.Builder(context => { if (microsoftDI) context.UseDI(); }).RunAsync(context => context
            .Services(services => services.Singleton<IStartable>(resolver =>
                executor = new LifeTimeExecutor(resolver.Resolve<IAppLifetime>()))));
        Assert.NotNull(executor);
        Assert.True(executor.IsStarted);
        Assert.True(executor.IsStopped);
        Assert.Equal(1, executor.DisposeCount);
    }

    [Fact]
    public async Task RunAsyncWithConfigurator()
    {
        // Use a holder: changes to AsyncLocal inside an async method do not flow back to its caller.
        var result = new List<LifeTimeExecutor>();
        ObservableConfigurator.Result.Value = result;
        try
        {
            await App.RunAsync<ObservableConfigurator>();
            var executor = Assert.Single(result);
            Assert.True(executor.IsStarted);
            Assert.True(executor.IsStopped);
            Assert.Equal(1, executor.DisposeCount);
        }
        finally { ObservableConfigurator.Result.Value = null; }
    }

    public class ObservableConfigurator
    {
        public static AsyncLocal<List<LifeTimeExecutor>?> Result { get; } = new();
        public void Configure(IAppConfigurationContext context)
            => context.Services(services => services.Singleton<IStartable>(resolver =>
            {
                var executor = new LifeTimeExecutor(resolver.Resolve<IAppLifetime>());
                Result.Value!.Add(executor);
                return executor;
            }));
    }
}
