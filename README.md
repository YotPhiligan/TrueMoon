# TrueMoon

TrueMoon is a modular C# framework for application bootstrapping, service registration, lifecycle management, interprocess calls and optional UI. Applications select the modules they need and retain explicit ownership of their services, processes and graphics resources.

The project is under active **alpha** development. The source version is currently `0.2.2.7-alpha`; a version suffix or a successful build does not establish release readiness. See the [current status](src/docs/STATUS.md) and [roadmap](src/docs/PLAN.md) for verified configurations and remaining work.

## Modules

| Area | Projects | Responsibility |
| --- | --- | --- |
| Application core | [TrueMoon](src/TrueMoon), [Contracts](src/TrueMoon.Contracts), [Core](src/TrueMoon.Core) | App builder, configuration, diagnostics, startup/shutdown, shared service contracts and utilities |
| Dependency injection | [Cobalt](src/TrueMoon.Cobalt), [Cobalt.Generator](src/TrueMoon.Cobalt.Generator), [Extensions.DependencyInjection](src/TrueMoon.Extensions.DependencyInjection) | Service registration/resolution, lifetimes, generated registration and an optional Microsoft DI adapter |
| Interprocess calls | [Enerit](src/TrueMoon.Enerit), [Enerit.Generator](src/TrueMoon.Enerit.Generator) | Invocation client/server adapters, serialization and transport implementations |
| Units and processes | [Mithril](src/TrueMoon.Mithril) | Unit configuration, process hosting and lifecycle/restart policies |
| UI controls | [Argentis](src/TrueMoon.Argentis) | Elements, controls, properties, bindings, layout, input and themes |
| UI runtime | [Alloy](src/TrueMoon.Alloy) | Retained UI sessions and backend-independent rendering contracts |
| Rendering | [Rendering.Skia](src/TrueMoon.Alloy.Rendering.Skia) | Raster, OpenGL and Vulkan backends with bundled extended native Skia |
| Window integration | [Platform.Silk](src/TrueMoon.Alloy.Platform.Silk), [Alloy.Hosting](src/TrueMoon.Alloy.Hosting) | Windows/input integration, standalone windows, App registration and external-host adapters |

UI is optional: bootstrap, DI, IPC and units do not depend on the graphics modules. Argentis is independent of Alloy/Skia/Silk; Alloy depends on Argentis. Rendering and platform integration are separate, and Hosting combines them when needed. The [general plan](src/docs/PLAN.md#архитектура-и-зависимости) describes the current dependency boundaries.

## Build from source

Projects target `net10.0`. SDK selection is defined in [global.json](src/global.json): stable .NET SDK `10.0.100` with `latestMinor` roll-forward. The latest recorded checks used SDK `10.0.402` on Windows. Package versions are centralized in [Directory.Packages.props](src/Directory.Packages.props).

Run the following from the repository root:

```powershell
cd src
dotnet restore TrueMoon.slnx
dotnet build TrueMoon.slnx --configuration Debug --no-restore
```

Core/runtime component checks use xUnit with VSTest. From `src`, after the Debug build:

```powershell
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore
```

Read results per test project: `TrueMoon.Mithril.Tests` now has 89 executable cases; dated Debug/Release results cover App-local configuration, main/owned child/external hosting, controller cleanup and parent exit with App lifetime/`RestartPolicy.Never` on Cobalt and Microsoft DI; separate owned-process cases check bounded restart policies, budgets/delays and shutdown during restart. Native/GPU probes are separate from this test command. Build warnings remain; dated results and limitations are recorded in [STATUS](src/docs/STATUS.md).

The bundled native UI path currently supports **Windows x64**. OpenGL/Vulkan examples require a compatible graphics driver. Validation probes have additional tool requirements documented in the [Vulkan manual](src/ManualTests/AlloyVulkanTest/README.md). Ordinary `dotnet build` uses the checked-in Skia DLL and does not rebuild native Skia.

## Minimal application

Reference `TrueMoon` and `TrueMoon.Extensions.DependencyInjection` for this example. Within the source checkout, use ProjectReference to those projects. The independent [headless package consumer](src/ManualTests/CorePackageConsumer/README.md) verifies both providers, lifecycle faults and analyzer delivery in Debug/Release build/publish on Windows x64/net10.0. Applications using the default Cobalt provider must explicitly reference `TrueMoon.Cobalt.Generator` with `PrivateAssets="all"`; Microsoft DI can construct consumer services without it.

```csharp
using TrueMoon;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(builder => builder.UseDI()).RunAsync(app => app
    .UseDiagnostics(options => options
        .OnEvent(message => Console.WriteLine(message))
        .Filters("TrueMoon"))
    .Services(services => services.Composite<Worker, IStartable, IStoppable>()));

public sealed class Worker : IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Application started. Press Ctrl+C to stop.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Application stopped.");
        return Task.CompletedTask;
    }
}
```

`App.Builder(builder => builder.UseDI())` selects the Microsoft DI adapter at builder creation. `App.RunAsync(...)` uses the default Cobalt resolver. Configure services before Build; `RunAsync` manages startup, waits for application lifetime cancellation, then stops and disposes the application. Ctrl+C requests shutdown. Cancellation and shutdown are cooperative: services must observe the token for long-running work. See the [Core lifecycle contracts](src/docs/core/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10) and [DI ownership differences](src/docs/cobalt/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10).

## Standalone UI

Add `TrueMoon.Alloy.Hosting` alongside `TrueMoon` and `TrueMoon.Extensions.DependencyInjection`. The following view is defined by the consuming application:

```csharp
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(builder => builder.UseDI()).RunAsync(app =>
    app.UsePresentation<MainView>(options => options
        .UseSkiaOpenGL()
        .UseSilkWindow(window => window.Title = "My Alloy application")));

public sealed class MainView : View
{
    public MainView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("Hello from Alloy"),
        Button.Simple("Action").OnClick(() => Console.WriteLine("Clicked"))));
}
```

Select `UseSkiaVulkan()` before Build to use Vulkan. Raster rendering without a window is available through Rendering.Skia. For a HUD, the engine owns its window/device/frame loop, while a UI session owns its tree and rendering resources. Standalone windows share a UI owner thread; use `HostedUiWindow.Post(...)` for changes from other threads. Full binding, threading, GPU lease and cleanup rules are in the [Hosting guide](src/TrueMoon.Alloy.Hosting/README.md).

### Local NuGet consumer

From `src` on Windows x64 with PowerShell 7:

```powershell
pwsh -NoProfile -File ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1
```

This builds a local feed with a unique prerelease version, restores an independent application using only PackageReference and verifies Debug/Release build/publish. The custom Skia DLL is delivered automatically, including through transitive Hosting dependencies, at `truemoon-native/win-x64/libSkiaSharp.dll`. Packages are not published by the script.

The recorded check on **2026-10-11** passed 12 raster/OpenGL/Vulkan App processes and 4 direct renderer processes. It covers framework-dependent Windows x64/net10.0 delivery without a RID. Self-contained, single-file, trimming, NativeAOT, other architectures and other OS are unverified. See the [consumer instructions and scope](src/ManualTests/AlloyPackageConsumer/README.md).

## IPC and units

Enerit exposes `UseInvocationService<T>()` and `ListenInvocationService<T, TService>()`; a pipes transport is configured with `PipesServiceTransport()`. Mithril adds unit configuration through `AddUnit(...)`. The current example is [ManualTests/MithrilTest](src/ManualTests/MithrilTest/Program.cs).

The [bounded Mithril lifecycle probe](src/ManualTests/MithrilLifecycleProbe/README.md) verifies owned child startup/stop/disposal with `RestartPolicy.Never` in Debug and published Release. The [bounded Enerit pipes probe](src/ManualTests/EneritPipesProbe/README.md) verifies generated typed calls between owned processes, concurrent requests, local cancellation, remote errors, disconnect/reconnect and cleanup in Debug and published Release. Peers must use the same sources/generator; void/Task acknowledgements and nullable-string encoding changed.

The [Mithril composition probe](src/ManualTests/MithrilCompositionProbe/README.md) verifies App/AddUnit main/child/owned external hosting, rollback and parent exit on both DI providers (28 Debug/published Release cases). Main services share the App graph; External launches an owned executable. New child control uses an async named pipe; manually selected children without its endpoint only observe the parent.

The [restart probe](src/ManualTests/MithrilRestartProbe/README.md) checks Never/Once/OnCrash/Always, bounded attempts/delays, terminal-only callbacks and shutdown during restart (40 Debug/published Release cases). Always defaults to three automatic attempts with 250 ms delay.

The [App/generated pipes probe](src/ManualTests/AppUnitsPipesProbe/README.md) now verifies the combined App/AddUnit/IPC/restart scenario on Cobalt and Microsoft DI (36 Debug/published Release cases): pending calls fail without replay, the same proxy reconnects to a restarted child, exhausted restart budget cancels the App, and stop/startup rollback clean owned transports before App disposal. Abrupt parent exit verifies cooperative child/server cleanup; cleanup of the killed parent client cannot be observed.

Generated typed responses now reject trailing or malformed payloads; failed nested byte decoding returns its rentals, while successful results remain caller-owned. Services own successfully decoded input rentals from service entry, including when they throw. See the [decode ownership contract](src/docs/enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14). New generated code requires the matching updated runtime. The [independent package consumer](src/ManualTests/UnitsPackageConsumer/README.md) checks local NuGet runtime/analyzer delivery and own-child IPC/restart/cleanup on both providers, including Microsoft DI without the Cobalt analyzer. Dated results and limits are in the module statuses. Memory-mapped transport readiness remains open. Follow the [Enerit](src/docs/enerit/STATUS.md) and [Mithril](src/docs/mithril/STATUS.md) statuses before relying on a particular configuration.

## Examples and documentation

| Example | Purpose |
| --- | --- |
| [CorePackageConsumer](src/ManualTests/CorePackageConsumer/README.md) | Independent local NuGet consumers: headless lifecycle on Cobalt/Microsoft DI, explicit generator delivery, direct Cobalt and packaged README examples |
| [CobaltTest](src/ManualTests/CobaltTest/Program.cs) | Service registration and application configuration; separate runtime verification remains open |
| [UnitsPackageConsumer](src/ManualTests/UnitsPackageConsumer/README.md) | Independent local NuGet App/IPC/units consumer, analyzer provenance, both DI providers and delivered child restart/cleanup |
| [AppUnitsPipesProbe](src/ManualTests/AppUnitsPipesProbe/README.md) | App/AddUnit generated IPC, pending failure/reconnect, restart budget, stop/rollback and parent-exit cleanup on Cobalt/Microsoft DI |
| [EneritPipesProbe](src/ManualTests/EneritPipesProbe/README.md) | Bounded same-build generated pipes contract and owned-process cleanup; Debug/published Release runner |
| [MithrilRestartProbe](src/ManualTests/MithrilRestartProbe/README.md) | Bounded owned-process restart policies, budgets/delays, failure and concurrent shutdown |
| [MithrilCompositionProbe](src/ManualTests/MithrilCompositionProbe/README.md) | App-local units, main/owned child/external, rollback/control EOF/parent exit on Cobalt and Microsoft DI |
| [MithrilLifecycleProbe](src/ManualTests/MithrilLifecycleProbe/README.md) | Bounded owned child startup/stop/disposal; Debug/published Release runner |
| [AlloyTest](src/ManualTests/AlloyTest/README.md) | Standalone editable Settings UI with bindings, collections and themes |
| [AlloyVulkanTest](src/ManualTests/AlloyVulkanTest/README.md) | Raster/GPU/HUD, native window and diagnostic probes |
| [AlloyPackageConsumer](src/ManualTests/AlloyPackageConsumer/README.md) | Independent NuGet consumer and direct/transitive native delivery |

Start with the [documentation index](src/docs/README.md), [general roadmap](src/docs/PLAN.md) and [current status](src/docs/STATUS.md). Module plans contain contracts and acceptance criteria; module statuses describe implementation, dated checks and the next task. Detailed evidence lives in their HISTORY files. Planning documents are currently maintained in Russian.

The UI alpha acceptance remains open: fixed-font/full golden comparisons, physical DPI transitions, real device-loss behavior and long resource/VRAM checks are incomplete. The current tested NVIDIA ICD has an unresolved GDI resource issue during repeated Vulkan window creation; it is recorded as an external K7 blocker. UI package smoke does not establish full alpha readiness or readiness of the other modules.

## License

[MIT](LICENSE).
