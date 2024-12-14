using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Dependencies;

var r = ServiceResolvers.Shared.GetResolvers();

await App.RunAsync(context => context
    .AddDependencies(registrationContext => registrationContext
        .AddTransient<IMainTestClass, MainTestClass>()
        .AddSingleton<IService1, Service1>()
        .AddSingleton<IService2, Service2>()
    )
);