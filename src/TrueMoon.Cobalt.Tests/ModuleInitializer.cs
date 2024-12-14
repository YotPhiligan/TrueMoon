using System.Runtime.CompilerServices;

namespace TrueMoon.Cobalt.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        ServiceResolvers.Shared.Add(()=> new MainServiceResolver());
        ServiceResolvers.Shared.Add(()=> new Service1Resolver());
        ServiceResolvers.Shared.Add(()=> new Service2Resolver());
        ServiceResolvers.Shared.Add(()=> new SubService1Resolver());
        ServiceResolvers.Shared.Add(()=> new SubService2Resolver());
    }
}