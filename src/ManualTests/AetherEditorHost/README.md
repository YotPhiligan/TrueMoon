# AetherEditorHost

Directed acceptance consumer of `TrueMoon.Aether.Editor`. Main/CLI, App composition,
diagnostics paths, process environment and directed smoke checks live here.
The reusable package owns View/Data/styles and its App registrations.

The minimal application host and independent runtime are in [Examples](../../Examples/README.md).
This test host keeps its legacy CLI, demo and smoke scenarios for regressions.

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

This acceptance consumer explicitly retains the compatible same-executable
profile for its legacy smoke scenarios. Its UseAetherEditor configureEngine sets:

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
[AetherRuntimeHost](../../Examples/AetherRuntimeHost/README.md). A consuming host can configure
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

This test host links the licensed font owned by Examples/AetherEditor at build
time; the font and OFL are copied beside its executable for build/publish.
No AlloyTest lookup remains. Independent NuGet acceptance remains ARCH2e.
Do not infer that matrix from source ProjectReference/process checks or the example's
explicit same-executable profile. No new published result is claimed here.

Native `--validation` additionally requires a Vulkan validation layer accessible
to the child process tree through its environment. Missing tooling yields
`ErrorLayerNotPresent`; an ordinary functional run without this flag proves no
Vulkan validation acceptance. No driver or machine configuration is changed.

ED5a.2 camera API proof: `--project fixture.aetherproj --smoke fresh-directory --smoke-camera --validation`.
Use a disposable Transform fixture with Transform.aetherscene containing one inherited
opaque cube, default rotation and Appearance. The profile opens it without Build/game
logic, installs an explicit view, sends50camera samples through the same child/surface,
checks actual GPU pixels and Editor ReadPresentedView, resizes/suspends/resumes, and
checks unchanged authoring/selection and zero cleanup. It records camera.json and four
camera PNGs in addition to existing window/header/status proofs. This exercises engine
camera APIs, not orbit/pan/dolly/select input. Source/published scope and exact fixture
are documented in docs/engine/HISTORY.md under ED5a.2.

ED5a.3 directed navigation proof:
`--project fixture.aetherproj --smoke fresh-directory --smoke-navigation --validation`.
Use a disposable Transform fixture with two independent inherited opaque cubes at
(-1.5,1,-1)/(1.5,1,-1), with red/green Appearance. The profile exercises production
UiSession routing: LMB hit/same/different/miss, shared tree/inspector policy, invalid
draft retention through RMB/MMB/wheel/F, Esc rollback, and actual GPU pixel readback.
navigation.json and six navigation PNGs retain camera/frame/layout evidence. Camera-only
input must preserve document/history/tick and the single runtime/surface. report.json
records zero owned UI/GPU/mapping/child resources and process-local validation.
Source Debug/published Release runs are recorded in docs/engine/HISTORY.md (ED5a.3).
This historical ED5a.3 profile is directed UI input in a native window. Current ED5a.4 results and limits are below.
ED5a.4 full lifecycle profile:
`--project fixture.aetherproj --smoke fresh-directory --smoke-viewport --validation`.
Use the two-cube [scene template](../AetherPackageConsumer/Viewport/Transform.aetherscene)
and disposable fixture/Other/Bad projects (Bad has a missing game entry); generate them
with [VerifyViewport.ps1](../AetherPackageConsumer/VerifyViewport.ps1).
The directed production UiSession scenario checks50orbit samples/pending, pan/dolly/F/
Esc/focus loss, invalid draft, actual pixels, UI/render resize, native minimize/resume,
render suspension, newer intent, delete/Undo, same-ID reopen, project switch, real
Build/Play/Stop, activation failure, first-presentation cancellation and recovery.
viewport-acceptance.json lists13groups and17PNG; report.json requires9reaped children,
8GPU validation reports plus1explicit pre-GPU failure, zero owned resources/errors.

Windows input observer:
`--project fixture.aetherproj --smoke fresh-directory --smoke-native-input --validation`.
This profile observes SilkWindowHost.Input and never injects events. In the window:
click each cube and empty background, focus inspector X, Ctrl+A and enter `invalid`,
wheel over canvas to dolly, focus canvas and press F, drag LMB then release, press/
release RMB and MMB. Close with native X. native-input-live.json exposes progress;
native-input.json records input counts, camera/frame/layout/DPI and final predicates.
Wheel steps depend on the input driver; observe actual camera revision/distance.
Debug/published Release Windows SendInput ran on DPI1.5 with camera1→2dolly→3F,
draft retained, same PID/surface,6PNG,1child=reaped and zero resources/validation.
Sky supplies LMB drag only: moving RMB/MMB/Esc/focus loss have directed UI/GPU tests,
physical held-button/multi-monitor DPI are separate gates. Neither profile proves
device-loss/K7/VRAM acceptance. [Exact current evidence](../../docs/engine/HISTORY.md#ed5a4-acceptance-2026-11-08).

## ED5b translation proof

`--project fixture.aetherproj --smoke fresh-directory --smoke-translation --validation`
uses the disposable two-cube scene template linked above. It checks clean-inspector
admission/draft denial, actual future-bounds outline without document mutation,
Esc cancellation, one release commit, actual solid pixels, Undo/Redo and final cleanup.
It saves the disposable fixture before closing. translation.json and five translation
PNGs accompany report.json; each profile requires four reaped children, zero owned
resources and validation0/0. The first profile supports world XYZ/XY/XZ/YZ; the native
scenario drags X. This is directed production UiSession/native GPU input, not physical
translation input. [Source/published/fresh-package results](../../docs/engine/HISTORY.md#ed5b-translation-2026-11-09).

## Unified configuration

The executable captures launch commands through the common `ParseSingleUse` schema
parser. Old aliases remain supported; inline/canonical forms and explicit false
flags are accepted, duplicate scalar/flag aliases are rejected. Without an explicit
validation flag, the host keeps legacy/canonical environment defaults; explicit
true/false takes precedence, including over invalid lower environment input. The frozen command
snapshot supplies explicit overrides to the App profile and configured Editor,
engine, diagnostics and window registrations. Child bootstrap captures protected
identity/parent/control separately; GPU validation is passed only to the owned
child's environment. No global validation environment is changed. Generic JSON/env
sources are an explicit composition-root choice, not automatic module IO.
[Module schemas and compatibility](../../docs/core/CONFIGURATION_MODULES.md).
