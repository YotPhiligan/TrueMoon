# TrueMoon.Aether

Experimental TrueMoon game-engine module: session lifecycle, control contracts,
owned game processes and bounded CPU frame transport. Current prototype scope:
Windows x64 and .NET 10.

The repository project is packable. The API remains experimental/alpha;
local package validation does not imply publication or production readiness.

Connect the module through `UseAether(...)` on `IAppConfigurationContext` inside
`App.Build`, `App.Builder(...).Setup(...)` or `App.RunAsync`. Configure host/game
build identities and a report directory; an optional runtime-factory delegate
can use application services. Options are snapshotted during registration.

Resolve `AetherHost` from App services. App owns the host and runtime factory;
the host owns `Supervisor` and `Frames`. App startup does not launch a game.
Start game sessions explicitly; App Stop permanently closes the host, fences
new starts, drains frame workers and stops/reaps the child. Use session Stop for
repeatable Play/Stop while App remains running. Do not separately register or
dispose the borrowed supervisor/frame session. Cleanup failures remain observable
and an explicit host Dispose retry is supported.

The default native runtime factory expects an executable with the matching child
role/bootstrap handler, as provided by the repository's EngineSessionProbe example.
The registration extension does not generate an executable entry point.

The single Aether assembly contains `TrueMoon.Aether` and
`TrueMoon.Aether.Contracts`. It uses TrueMoon Contracts/Enerit/Mithril without
depending on UI, Skia/Silk or a particular game DLL. The application supplies
Editor/Runtime entry points and its `IGameSession` implementation.

Game logic remains a separate assembly. CPU frame transport and session control
are implemented; compiler jobs, checkpoints, versioned replacement, 3D rendering
and shared GPU transport remain later milestones. Same-build protocol identity,
owned-process cleanup and frame/thread ownership remain required.

Source, integration example and roadmap:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
