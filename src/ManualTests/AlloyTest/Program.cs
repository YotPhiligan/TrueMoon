using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(t=>t.UseDI())
        .RunAsync(context => context
        .UseDiagnostics(configuration => configuration
            .OnEvent(@event => Console.WriteLine($"{@event}"))
        )
        .UsePresentation()
    );