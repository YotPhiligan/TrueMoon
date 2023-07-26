using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

await App.RunAsync(context => context
    .UseDiagnostics(configuration => configuration
        .OnEvent(@event => Console.WriteLine($"{@event}"))
        .Filters("TrueMoon")
    )
    .UseDI()
    .UsePresentation()
);