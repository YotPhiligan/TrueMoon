# TrueMoon

Headless application bootstrap, configuration, diagnostics and cooperative
service lifecycle. Requires .NET 10. Alpha API; this package does not require UI.

The default provider is Cobalt. In an application using generated registrations,
reference **both** `TrueMoon` and `TrueMoon.Cobalt.Generator` at matching versions.
The generator is a separate analyzer package and is not automatically delivered
by a runtime package dependency:

```xml
<PackageReference Include="TrueMoon" Version="YOUR_VERSION" />
<PackageReference Include="TrueMoon.Cobalt.Generator" Version="YOUR_VERSION" PrivateAssets="all" />
```

```csharp
using TrueMoon;
using TrueMoon.Diagnostics;

await App.RunAsync(app => app
    .UseDiagnostics(events => events.AddEventListener(Console.WriteLine))
    .Services(services => services.Composite<Worker, IStartable, IStoppable>()));

public sealed class Worker(IAppLifetime lifetime) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Started");
        lifetime.Cancel();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Stopped");
        return Task.CompletedTask;
    }
}
```

`RunAsync` owns the application: startup, lifetime wait, reverse shutdown and
disposal. Lifetime cancellation completes a normal run; external cancellation
reaches the caller as `OperationCanceledException` after cleanup. Startup errors
roll back successfully started services. Shutdown uses a separate cooperative
token with a 30-second timeout; services must observe cancellation themselves.

For Microsoft DI, also reference `TrueMoon.Extensions.DependencyInjection` and
use `App.Builder(builder => builder.UseDI())` with its namespace imported.
Microsoft DI does not need Cobalt generation for its own consumer registrations.
Configuration, diagnostics and service interfaces are in `TrueMoon.Contracts`.

Source, module plans and reproducible package consumers:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
