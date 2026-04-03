namespace TrueMoon.Cobalt.Tests;

public static class TestModuleInitializer
{
    //[ModuleInitializer]
    public static void Init()
    {
        ServiceResolvers.Shared.Add(() => new MainServiceResolver());
        ServiceResolvers.Shared.Add(() => new Service1Resolver());
        ServiceResolvers.Shared.Add(() => new Service2Resolver());
        ServiceResolvers.Shared.Add(() => new SubService1Resolver());
        ServiceResolvers.Shared.Add(() => new SubService2Resolver());
        ServiceResolvers.Shared.Add(typeof(ITestGenericService<>),() => new TestGenericServiceResolver());
        ServiceResolvers.Shared.Add(typeof(ITestGenericService2<>),() => new TestGenericService2Resolver());
        ServiceResolvers.Shared.Add(() => new Service5Resolver(resolver => new Service5()));
    }
}