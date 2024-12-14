namespace TrueMoon.Cobalt.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var ctx = new ResolvingContext();

        var mainService = ctx.Resolve<IMainService>();
        
        Assert.NotNull(mainService);
    }
}