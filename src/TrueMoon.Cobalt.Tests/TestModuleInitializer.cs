namespace TrueMoon.Cobalt.Tests;

public static class TestModuleInitializer
{
    //[ModuleInitializer]
    public static void Init()
    {
        ServiceResolvers.Shared.Add(a => new MainServiceResolver());
        ServiceResolvers.Shared.Add(a => new Service1Resolver());
        ServiceResolvers.Shared.Add(a => new Service2Resolver());
        ServiceResolvers.Shared.Add(a => new SubService1Resolver());
        ServiceResolvers.Shared.Add(a => new SubService2Resolver());
        ServiceResolvers.Shared.Add(typeof(ITestGenericService<>),a => new TestGenericServiceResolver());
        ServiceResolvers.Shared.Add(typeof(ITestGenericService2<>),a => new TestGenericService2Resolver());
        ServiceResolvers.Shared.Add(a => new Service5Resolver(a.GetFactory<IService5>().Get()));
    }
}