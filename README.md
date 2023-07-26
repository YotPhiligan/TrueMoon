# TrueMoon

Small and simple framework for app boosttrapping.

```C#
await App.RunAsync(context => context
    .UseDiagnostics(configuration => configuration
        .OnEvent(@event => Console.WriteLine($"{@event}"))
        .Filters("TrueMoon")
    )
    .UseDI()
    .UseSignals()
    .AddUnit(ctx => ctx
        .UseSignalService<ITestService>()
        .AddDependencies(registrationContext => registrationContext
            .Add<Sender>(dependencyRegistrationContext => dependencyRegistrationContext
                .WithAppLifetime())
        ) 
    )
    .AddUnit(configuration => configuration
        .ListenSignalService<ITestService, ListenTestService>()
    )
);
```