namespace TrueMoon.Cobalt.Tests;

public class CobaltServiceResolverTests
{
    [Fact]
    public void Resolve()
    {
        TestModuleInitializer.Init();
        
        var ctx = new CobaltServiceResolver(new ServicesRegistrationContainer());

        var mainService = ctx.Resolve<IMainService>();
        Assert.NotNull(mainService);
        Assert.IsType<MainService>(mainService);
        var genericService = ctx.Resolve<ITestGenericService<object>>();
        Assert.NotNull(genericService);
        Assert.IsType<TestGenericService<object>>(genericService);
        
        var genericService2 = ctx.Resolve<ITestGenericService2<object>>();
        Assert.NotNull(genericService2);
        Assert.IsType<TestGenericService2<object>>(genericService2);
    }
}