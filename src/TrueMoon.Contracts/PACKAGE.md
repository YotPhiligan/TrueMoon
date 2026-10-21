# TrueMoon.Contracts

Shared .NET 10 contracts, base models and interfaces for interoperability between
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

Use matching versions of TrueMoon modules. The current package consumer checks
net10.0 with the .NET 10 SDK on Windows; other configurations and a full framework
release are not implied by that bounded result.

[Source, contracts and module plans](https://github.com/YotPhiligan/TrueMoon).
