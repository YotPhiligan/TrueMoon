using System.Collections.Frozen;

namespace TrueMoon.Cobalt.Tests;

public class CobaltServiceResolverTests
{
    [Fact]
    public void Resolve()
    {
        TestModuleInitializer.Init();

        var servicesRegistrationContainer = new ServicesRegistrationContainer();
        
        servicesRegistrationContainer.RegisterSingleton<IMainService,MainService>();
        servicesRegistrationContainer.RegisterSingleton<IService1,Service1>();
        servicesRegistrationContainer.RegisterSingleton<IService2,Service2>();
        servicesRegistrationContainer.RegisterSingleton<SubService1>();
        servicesRegistrationContainer.RegisterSingleton<SubService2>();
        servicesRegistrationContainer.RegisterSingleton(typeof(ITestGenericService<>), typeof(TestGenericService<>));
        servicesRegistrationContainer.RegisterSingleton(typeof(ITestGenericService2<>), typeof(TestGenericService2<>));
        
        var containers = new List<ITypeContainer>();
        
        var resolverFactories = ServiceResolvers.Shared.GetResolvers();
        
        var handles = servicesRegistrationContainer.GetHandles();
        
        foreach (var grouping in handles.GroupBy(t=>t.ServiceType))
        {
            var type = grouping.Key;
            var container = new TypeContainer(type);
            foreach (var handle in grouping)
            {
                if (handle.Resolver != null)
                {
                    container.Add(handle.Resolver);
                }
                else if (handle.ImplementationType != null)
                {
                    //resolverFactories.FirstOrDefault(t=>t.)
                    var resolver = resolverFactories[handle.ServiceType];
                    container.Add(resolver);
                }
                else if (handle.ServiceType is { IsInterface: false })
                {
                    var resolver = resolverFactories[handle.ServiceType];
                    container.Add(resolver);
                }
            }
            containers.Add(container);
        }
        
        var resolversContainer = containers.ToFrozenDictionary(t=>t.Type, t=>t);
        
        var ctx = new CobaltServiceResolver(resolversContainer);

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