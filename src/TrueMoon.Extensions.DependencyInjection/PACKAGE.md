# TrueMoon.Extensions.DependencyInjection

Microsoft.Extensions.DependencyInjection 10 adapter for TrueMoon service
contracts. Requires .NET 10; alpha API. For an application, reference `TrueMoon`
and this package at matching versions:

```csharp
using TrueMoon;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(builder => builder.UseDI()).RunAsync(app => app
    .Services(services => services.Composite<Worker, IStartable, IStoppable>()));

public sealed class Worker(IAppLifetime lifetime) : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lifetime.Cancel();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

The provider is selected at builder creation. Microsoft DI constructs these
consumer services without Cobalt generation. The transitive `TrueMoon.Cobalt`
package bundles its analyzer; Microsoft DI consumers may disable it with
`<EnableCobaltGenerator>false</EnableCobaltGenerator>`.

The application owns the provider and handles startup, cooperative cancellation,
reverse stop/rollback and disposal. The adapter preserves Microsoft DI semantics:
disposable Composite aliases may be disposed more than once, so their Dispose
must be idempotent. Cleanup after a provider disposal exception also follows
Microsoft DI rules; Cobalt's reference-unique/continue-on-error guarantee does
not apply here.

[Source, module plans and package consumers](https://github.com/YotPhiligan/TrueMoon).
