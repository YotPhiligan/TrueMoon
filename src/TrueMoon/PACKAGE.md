# TrueMoon

Headless application bootstrap, configuration, diagnostics and cooperative
service lifecycle. Requires .NET 10. Alpha API; this package does not require UI.

The default provider is Cobalt. Its matching generator is bundled in the
transitive `TrueMoon.Cobalt` package, so the application needs one reference:

```xml
<PackageReference Include="TrueMoon" Version="YOUR_VERSION" />
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
It can opt out with `<EnableCobaltGenerator>false</EnableCobaltGenerator>`.
Configuration, diagnostics and service interfaces are in `TrueMoon.Contracts`.

Bounded structured diagnostics can be enabled explicitly:

```csharp
context.UseDiagnostics(new DiagnosticPipelineOptions
{
    JsonDirectory = Path.Combine(Path.GetTempPath(), "TrueMoon", "MyAppDiagnostics"),
    JsonUseRunDirectories = true
});
```

Inject IEventsSource<T> and use Write(DiagnosticLevel.Information, 2001,
"Worker.Started", "Worker started"). Named properties are created lazily and
copied into bounded immutable records. DiagnosticPipeline.Read() exposes recent
history; Health() exposes per-sink loss/failure counters. Source/event levels
can be changed without recreating sources. Independent output queues prevent
slow sinks from blocking producers. App disposal drains accepted events with a
finite budget; a disposal timeout preserves ownership for an explicit retry.
Legacy OnEvent callbacks remain synchronous. No Microsoft logging adapter is required.

Inject IDiagnosticTelemetry for named operation scopes. ActivitySource and Meter
use the name TrueMoon and require standard .NET listeners; no exporter is installed.
StartOperation returns null when unobserved and evaluates properties only for
sampled traces requesting all data. Complete selects Succeeded/Failed/Cancelled;
Dispose publishes duration and count once, or Incomplete when Complete was omitted.
Metrics have only operation/outcome tags, with 64 distinct names per owner by default.
DiagnosticPipelineOptions.Telemetry copies sampling enable flags and name/property
limits during setup. Observer failures are isolated and counted by DiagnosticTelemetry.Read().
The App owns telemetry; active scopes retain instrumentation until their disposal.

Managed JSON output creates one owned GUID directory per sink lifetime. Closed
runs default to 8 directories / 32 MiB / 7 days, configured through JsonRetention.
Active and unrecognized directories are preserved; the newest closed run is
protected even over budget. JsonDirectory exposes the actual run path and
JsonRetentionStatus reports maintenance failures or an unsatisfied budget.
The default flat directory mode and four-argument JsonDiagnosticSink remain available.

Emergency output uses a separate worker and borrowed Console.Error by default:
8 pending reports, 16 total attempts, 1024 characters per report. Configure
Emergency or disable it. Reports contain internal stages/types, without event
payloads or exception messages. EmergencyStatus exposes loss/writer failures;
writer failures never report themselves. Flush/disposal include this channel
within the shutdown budget, and never dispose a borrowed writer.

Source, module plans and reproducible package consumers:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
