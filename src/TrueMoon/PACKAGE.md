# TrueMoon

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

Headless application bootstrap, configuration, diagnostics and cooperative
service lifecycle. Requires .NET 11. Alpha API; this package does not require UI.

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

## Standalone unified configuration

The opt-in `TrueMoon.Configuration` APIs `ConfigurationSchemas.Create` and
`ConfigurationProfiles.Create` provide normalized section keys, explicit source
priority (defaults < JSON < environment < arguments < overrides), typed invariant
conversion and immutable data snapshots without App or DI. Add sources through
`AddJsonFile`, `AddEnvironment` and `AddArguments`, await `profile.LoadAsync()`, then
use `schema.Bind(snapshot)` for an independent validated options copy. Custom value
types use explicit codecs; raw secret values are omitted from configuration errors.

Legacy `IConfiguration` and `ConfigurationBuilder` retain their behavior. Connect
an opt-in profile from App Setup with `context.UseConfiguration(profile)` and
`context.ConfigureModule(schema, (module, options) => module.UseYourModule(...))`.
Every schema is bound/validated before module registrations or service activation;
the callback receives an independent options copy. Use your module's typed options
or constructor registration, not container lookups in application systems.

App registers a nondisposable borrowed `IConfigurationReader`; `Current.GetOptions(schema)`
copies already-prepared options without rerunning callbacks. Disposing App closes
reader access; issued snapshots remain data. Standalone `GetOptions` still requires
an App stage and throws; use `schema.Bind(snapshot)` outside App. Custom profiles
need the optional schema catalog to join App composition. Borrowed `IConfigurationReloader`,
`IConfigurationChanges` and `IConfigurationWriter` provide explicit reload, bounded
notifications and JSON patches. Reload publishes a complete validated snapshot or
retains the old one; any changed startup field requires restart. Existing injected
options remain startup copies. Stop closes mutation/notification admission and drains
callbacks; reader access lasts until Dispose. Callbacks must return promptly and
must not wait for App shutdown. Writing does not reload or publish; reads never
persist JSON implicitly. Existing modules/CLI/private
process bootstrap are not automatically migrated.

## Standard Core settings

Add `CoreConfigurationSchemas.App` and `.Paths` (or call `profile.AddSchemas()`),
then call `context.UseCoreConfiguration()`. `App:Name` and `Paths:Root` are startup
settings. App name and the resolver keep private validated copies even when a
consumer mutates an injected DTO. Legacy-only applications keep dynamic `appName`.
`profile.AddLegacySources(legacy)` explicitly imports only supported default/env/args
aliases at their standard priorities. Captured original tokens retain common
separate-value parsing and alias ordering; legacy `Set` overrides remain explicit.

`DiagnosticsConfigurationSchema.Default` plus `UseConfiguredDiagnostics` bind
pipeline budgets, rules, retention, emergency and telemetry settings. Sink factories,
listeners and borrowed emergency output stay separate composition inputs. Startup
validation precedes resources; reload requires restarting these pipelines.
Diagnostic activation uses DI; the former object-bag entry is retained only by
legacy `UseDiagnostics` as a compatibility mirror.

`ConfigurationCommandLine.ParseKnown` is the minimal selected bootstrap parser;
strict `Parse` continues rejecting unknown options. `PathResolver` uses the same
parser and accepts validated `PathSettings` without building App/DI. Ordinary
file providers cannot replace the original bootstrap sections exposed through
optional `IConfigurationBootstrapInputs`.

`ConfigurationCommandLine.ParseSingleUse` shares the standard parser and rejects
duplicate scalar/flag aliases on command surfaces. `Parse`/`ParseKnown` retain their
existing last-wins behavior. Module libraries can define schemas and defer binding
through the neutral Contracts helpers without referencing this bootstrap assembly.
