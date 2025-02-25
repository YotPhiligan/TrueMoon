using TrueMoon;
using TrueMoon.Cobalt;

var r = ServiceResolvers.Shared.GetResolvers();

await App.RunAsync(context => context
    .Services(registrationContext => registrationContext
        .Transient<IMainTestClass, MainTestClass>()
        .Singleton<IService1, Service1>()
        .Singleton<IService2, Service2>()
        //.Singleton<Service2>(resolver => resolver.Resolve<Service2>())
    )
);