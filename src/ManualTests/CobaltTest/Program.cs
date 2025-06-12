using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Diagnostics;

var r = ServiceResolvers.Shared.GetResolvers();

await App.RunAsync(context => context
    .UseDiagnostics(t=>t.AddEventListener(Console.WriteLine))
    .Services(registrationContext => registrationContext
        .Transient<IMainTestClass, MainTestClass>()
        .Singleton<IService1, Service1>()
        .Singleton<IService2, Service2>()
        .Singleton<IServiceTest2>(resolver => resolver.Resolve<Service2>())
        .Singleton(typeof(ITestGenericClass<>), typeof(TestGenericClass<>)
        )
    )
);