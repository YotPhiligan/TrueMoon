# TrueMoon.Contracts

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

Shared .NET 11 contracts, base models and interfaces for interoperability between
TrueMoon modules. Application, service registration, configuration and diagnostics
contracts are part of this shared API. Module-specific types belong to their
respective modules. Alpha API. The package has no dependency on a DI provider, UI,
graphics, IPC or process-host implementation.

Contracts include `IApp`, `IAppLifetime`, `IStartable`, `IStoppable`,
`IServiceResolver`, `IServicesRegistrationContext`, `IConfiguration` and
`IEventsSource`. Bootstrap implementations are supplied by `TrueMoon`, Cobalt
resolution by `TrueMoon.Cobalt`, and the alternative provider by
`TrueMoon.Extensions.DependencyInjection`.

Prefer constructor dependencies on these interfaces. Application `RunAsync`
owns its provider and performs cooperative startup/shutdown; individual
provider packages define their service/disposal semantics. Referencing this
contracts package alone does not supply application bootstrap or generated
construction code.

`IFactory<T>` and `IFactory<T, TArguments>` describe explicit service creation.
`Factory<T>` / `Factory<T, TArguments>` adapt registered construction callbacks.
Products belong to their caller unless explicitly registered as owned services;
the adapters do not dispose products. Typed factories require arguments and
validate calls through the legacy generic factory interface.
`AppServiceLifecycle` forwards start/stop without owning or disposing its target,
so lifecycle aliases do not duplicate provider ownership.

Use matching versions of TrueMoon modules. The current package consumer checks
net11.0 with the .NET 11 SDK on Windows; other configurations and a full framework
release are not implied by that bounded result.

[Source, contracts and module plans](https://github.com/YotPhiligan/TrueMoon).

## Unified configuration data contracts

`TrueMoon.Configuration` adds data-only profile, source, schema, snapshot and codec
contracts alongside the existing `IConfiguration` interfaces. Logical keys are
module-qualified paths. `ConfigurationKey.Codec` optionally carries a type-erased
explicit converter; generic codecs remain typed. Implementations live in the
TrueMoon package and do not require a specific DI adapter. Existing configuration
interfaces gain no mandatory members. Optional `IAppConfigurationStage` and
`IConfigurationReader` support deferred App composition and borrowed reads.
`IConfigurationProfileSchemas`/`IConfigurationSchemaVisitor` describe every typed
schema without reflection or new required members on `IConfigurationProfile`.
`IConfigurationReloader`, `IConfigurationChanges` and `IConfigurationWriter` expose
explicit atomic reload, bounded notifications and source patches. `ConfigurationEdit`
distinguishes deletion (missing value) from JSON null; `ConfigurationUpdate` owns
its changed-key list. Optional `IConfigurationMutableSource` and
`IConfigurationProfileWriter` add writing without changing existing read interfaces.
Implementation and single runtime ownership live in TrueMoon; public service aliases
are borrowed and nondisposable. Shutdown drains callbacks, which must not wait for
App shutdown themselves. Writes never publish a reader generation implicitly.

`IConfigurationBootstrapInputs` is an optional legacy bridge to the original
args/env/default provider snapshots before ordinary file/provider overlays.
It adds no required members to `IConfiguration`. Protected process identity readers
use these captured inputs; provider removal does not trigger another OS read.

## Module configuration definitions

`ConfigurationSchemaDefinitions.Create<T>` and `ConfigurationModuleBinding.Configure`
provide pure schema construction and optional `IAppConfigurationStage` binding for
module libraries. They do not select sources, read the OS, build App/DI or activate
resources. Standard scalar conversion and validation are shared with the bootstrap.
The existing `ConfigurationSchemas.Create` facade remains in TrueMoon for compatibility.
