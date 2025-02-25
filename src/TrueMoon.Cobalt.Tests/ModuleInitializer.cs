using System.Runtime.CompilerServices;

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
    }
}