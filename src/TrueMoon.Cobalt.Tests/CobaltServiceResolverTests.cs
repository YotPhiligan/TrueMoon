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
    }
}