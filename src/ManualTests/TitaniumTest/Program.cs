using TitaniumTest.Services;
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Thorium;
using TrueMoon.Titanium;

await App.RunAsync(context => context
    .UseDiagnostics(configuration => configuration
        .OnEvent(@event => Console.WriteLine($"{@event}"))
        .Filters("TrueMoon")
    )
    .UseDI()
    .UseInvocations()
    .AddUnit(ctx => ctx
        .UseInvocationService<ITestService>()
        .AddDependencies(registrationContext => registrationContext
            .Add<Sender>(dependencyRegistrationContext => dependencyRegistrationContext
                .WithAppLifetime())
        ) 
    )
    .AddUnit(configuration => configuration
        .ListenInvocationService<ITestService, ListenTestService>()
    )
);