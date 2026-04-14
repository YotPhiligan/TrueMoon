using MithrilTest.Services;
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;

await App.Builder(t=>t.UseDI())
    .RunAsync(context => context
        .PipesServiceTransport()
        .UseDiagnostics(configuration => configuration
            .OnEvent(@event => Console.WriteLine($"{@event}"))
            .Filters("TrueMoon")
        )
        .AddUnit(ctx => ctx
            .UseInvocationService<ITestService>()
            .Services(registrationContext => registrationContext
                .Composite<Sender,IStartable,IStoppable>()
            ) 
        )
        .AddUnit(configuration => configuration
            .ListenInvocationService<ITestService, ListenTestService>()
        )
    );