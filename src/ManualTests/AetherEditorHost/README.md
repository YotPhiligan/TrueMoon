# AetherEditorHost

Executable consumer of `TrueMoon.Aether.Editor`. Main/CLI, App composition,
diagnostics paths, process environment and directed smoke checks live here.
The reusable package owns View/Data/styles and its App registrations.

```powershell
dotnet run --project ManualTests/AetherEditorHost
dotnet run --project ManualTests/AetherEditorHost -- --project E:/games/MyGame/MyGame.aetherproj
dotnet run --project ManualTests/AetherEditorHost -- --check-project E:/games/MyGame/MyGame.aetherproj
```

An empty launch opens an empty workspace without SDK/compiler/game startup.
`.aetherproj` opens during App startup before the standard window. Explicit legacy
`.csproj` launches retain the old demo/build/play workflow. All existing CLI
options and smoke profiles are preserved; the package contains none of their types.

The host calls `UseAetherEditor(...)` and `UseAetherEditorWindow(...)`; it creates no
services outside App registration factories. The App owns engine/project/document
services and work. The window owns its root/header tree. It awaits `EditorWindow.Ready`
and `HostedUiWindow.Completion`, then drains App. See the
[module API](../../TrueMoon.Aether.Editor/PACKAGE.md) and
[screen/command guide](../../TrueMoon.Aether.Editor/README.md).

This consumer explicitly retains the compatible same-executable profile until
the host/resources stage. Its UseAetherEditor configureEngine sets:

```csharp
engine.RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime");
engine.HostContractId = BuildIdentity.HostId;
```

These types are in `TrueMoon.Aether.Hosting`. The descriptor preserves the previous
HostId and selects this host's existing child branch; no arbitrary executable is
launched implicitly. RuntimeEntryPoint calls `RuntimeBootstrap.ReadEnvironment()`
before App construction, configures UseAether from `.Launch`, and registers selected
game or built-in preview with `.ParentProcessId`. Main/App composition, diagnostics
and reporting remain here, not in the library.

ARCH2c also supports a separate runtime without Editor/UI dependencies:
[AetherRuntimeHost](../AetherRuntimeHost/README.md). A consuming host can configure
`engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll)` and
`engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll)` instead. For dotnet,
use its absolute executable and explicit `[absRuntimeDll, .. runtimeArguments]`.
A null descriptor permits registration/headless/custom RuntimeFactory use, but
default native Start reports a clear configuration error. There is no new runtime
selector CLI in this Editor example; configure a different profile in composition.

Pair identity covers labelled host/runtime entries and exact shared
Aether/Contracts/Enerit/Mithril, checking both directories and actual managed entry.
It is not authentication or a complete native/private-dependency hash; deployments
must not mutate in place during a session. A different pair invalidates old game
artifacts/checkpoints: rebuild games. Scene/project formats are unchanged.
[Bootstrap Version1/bounds and unchanged wire versions](../../TrueMoon.Aether/PACKAGE.md).

The example currently copies its fonts from AlloyTest; complete asset/example
delivery remains ARCH2d, independent NuGet/published acceptance remains ARCH2e.
Do not infer that matrix from source ProjectReference/process checks or the example's
explicit same-executable profile. No new published result is claimed here.

Native `--validation` additionally requires a Vulkan validation layer accessible
to the child process tree through its environment. Missing tooling yields
`ErrorLayerNotPresent`; an ordinary functional run without this flag proves no
Vulkan validation acceptance. No driver or machine configuration is changed.
