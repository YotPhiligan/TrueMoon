# TrueMoon.Aether.Editor

Reusable editor module for TrueMoon App, targeting net10.0. Includes retained
View/Data/styles, scene hierarchy/inspector, project/document coordination,
commands and an optional Windows x64 Skia/OpenGL window integration.

The inspector exposes `ResetTransformGroup(TransformGroup)` with Position/Rotation/Scale
admission flags. It stages inherited/schema resets, preserves unrelated drafts and
the linked-scale preference, and uses the existing Apply/Cancel and shared history.
Numeric gestures must finish before a group reset.

`EditorInspectorViewModel.AnglesInDegrees` and `CanChangeAngleUnits` provide the
transient rotation preference and admission. `TransformField.Value` remains canonical
radians; `DisplayValue`/`DisplayLabel` expose selected units. Changing units alone
preserves canonical text and history; explicit degree edits convert once. Invalid
rotation text (including pending resets), gestures and busy/conflicting/read-only state
block the switch. Raw display text is retained across round trips and gesture rollback.
Rotation step is0.01rad/1degree, with limits checked in canonical Float radians.
The retained Degrees checkbox and labels reuse existing controls; legacy View also
binds the display projection. [ED4g.4 source/published checks](../docs/engine/HISTORY.md#ed4g4-angle-units-2026-11-08)
do not claim a fresh Editor nupkg delivery run. Each retained group row owns a
keyboard-accessible reset icon and reserves its width before responsive vector layout.
ED4g.3 source Debug/published Release native delivery and Editor tests are recorded in
[engine history](../docs/engine/HISTORY.md#ed4g3-group-reset-2026-11-08); fresh NuGet
delivery was not rerun for this slice.

The consuming executable owns Main, CLI, App composition, diagnostics, fonts,
report paths and runtime bootstrap. This package creates no nested App and
contains no executable entry point or smoke checks.

Default typography follows Argentis: `Segoe UI Variable Text`, regular400 for
body text and semibold600 for the compact caption and pane titles. Custom caption,
tooltip, status and viewport text pass weight/italic through measure and drawing;
the editor's light/dark commands preserve the selected family/size/weight/italic.
The host can pass `SkiaFontCatalog.SystemDefault` to UseAetherEditorWindow, or add
an explicit file fallback as in the example host:

```csharp
var fallback = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
{
    ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf")
});
var fonts = SkiaFontCatalog.FromSystemFonts(
    [TrueMoon.Argentis.FontDefaults.Family, TrueMoon.Argentis.FontDefaults.FallbackFamily], fallback);
```

```csharp
using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;

// The host supplies absolute runtime paths and explicitly loads its own font assets.
await using var app = App.Build(context =>
{
    context.UseAetherEditor(engine =>
    {
        engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll);
        engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll);
        engine.GameBuildId = new string('0', 64);
        engine.ReportDirectory = reportDirectory;
    }, editor => editor.ProjectPath = projectManifest); // optional .aetherproj
    context.UseAetherEditorWindow(fonts);
});
await app.StartAsync();
await app.Services.Resolve<EditorWindow>().Ready;
await app.Services.Resolve<TrueMoon.Alloy.Hosting.HostedUiWindow>().Completion;
await app.StopAsync();
```

`absRuntimeExe` is the runtime apphost; `absRuntimeDll` is its managed entry assembly.
For dotnet, use `new NativeRuntimeLaunch(absDotnetExe, absRuntimeDll,
[absRuntimeDll, .. runtimeArguments])`. Paths are absolute, arguments are copied
without shell parsing, and no implicit `--runtime` is added. The explicit
ForRuntime call reads counterpart binaries to verify their identity. A custom
`engine.RuntimeFactory = runtimeFactory` may instead provide a fresh App-owned
IRuntimeFactory. A null RuntimeLaunch permits registration/headless/custom-factory
use; default native Start fails with a clear configuration error.

Configure once per App. Options are validated/copied during setup. Duplicate
aggregate attachment, an existing Aether host or existing editor project graph
are rejected. Register custom stores/styles or root model/view factory beforehand.
Use the existing lower-level extensions for deliberately custom composition.
Module registration opens no project/scene files, starts no SDK/compiler/game
and allocates no native resources. An initial project is opened at App startup before the standard window.
With the standard window, App startup waits for first presentation/viewport
readiness; a failure rolls back the window and editor even after native creation.

The standard host now creates `EditorMainWindow : Window<EditorViewModel>` through
`IFactory<EditorMainWindow, EditorMainWindowArguments>`. The Window owns exactly
one fresh `EditorView`; models/styles/factories are borrowed. Register a custom
`IViewStyle<EditorMainWindow>` or typed root factory before UseAetherEditorWindow
to replace defaults. The default wrapper adds no padding/background of its own;
the nested EditorView keeps its own scoped style and embedded contract.

The existing configure callback still sets native/backend/chrome/frame options.
Title/Width/Height are copied after that callback into immutable root arguments,
preserving the 1280x800 default and supported caller overrides. Later mutations of
the captured options object have no effect. `ConfigureDefaultTitleBar` continues
to receive the nested EditorView as content, including callbacks chaining the
default implementation. Root factories must honour their creation arguments and
create on the UI owner before native/GPU allocation.

`EditorMainWindow.FromRoot(ui.Root).EditorView` reads the standard typed content
with or without its one WindowFrame. Embedded consumers retain their own explicit
view reference. Project/document/dirty caption text comes from the same document
view-model formatting used by the native title and compact header. Native
observations never overwrite requested ClientSize; content/focus/drafts/history
and source-owner binding remain retained.

EditorWindow remains the lifecycle adapter and the single document exit-policy
owner. Window/header close requests use the existing host coordinator; no second
Save/Discard/Cancel handler is installed on the component. GPU viewport setup stays
after first presentation/Ready. Failed early root creation disposes only accepted
fresh products and allocates no native window. Native/initialization failures and
App stop still drain through the existing lifecycle.

For an embedded UI, omit UseAetherEditorWindow and use IFactory<EditorView> on the
parent UI owner thread; the parent owns its tree and requires no Window wrapper.
After presentation call EditorSession.Present(ui, view). Call RequestCloseAsync
before user close to apply Save/Discard/Cancel. App shutdown drains accepted work
and terminally discards any remaining unsaved document. Embedded GPU viewport
integration requires a separately supplied supported transport; the default
GPU bridge is initialized by the standard window adapter.

App owns engine/project/document services, tasks and models. Views borrow these
services and release their subscriptions on disposal; disposing a View never
disposes its model. Non-disposable lifecycle aliases prevent double disposal.
Failed startup runs App rollback; cleanup failures remain observable and existing
document/engine cleanup methods retain their retry semantics.

With an open authoring scene, Run builds the selected game project and starts an
independent runtime copy of the current in-memory snapshot. Save is optional;
unapplied inspector drafts must be applied/cancelled first. Preview drains before
game startup. Pause/Resume/Step/Stop preserve the editor document, dirty checkpoint,
selection and history. File/project edits remain fenced while preparing/playing;
Stop cleanup failure retains retry admission. Each Start verifies or builds a candidate and requires
the exact candidate/context version, with no fixed-runtime fallback. Tracked scene
builds observe evaluated files/options, preprocessed imports and managed HintPath
references before/after compilation and again before Start. Persistent changes reject
startup. The initial profile is one net10.0 SDK project without PackageReference or
ProjectReference; unsupported graphs fail closed. The process-local witness is lost
when reopening an artifact; schema1 is unchanged. Scene startup also opts into
CaptureCompilation: trusted SDK preparation returns exact Csc args, compiler and
source/generated/reference/config bytes are copied, and Csc compiles from those copies.
Source paths/diagnostics are mapped back and staging is released before Start. This
bounded C# library profile rejects opaque compiler inputs/custom analyzers and
resource/ruleset/signing profiles. Scene Start additionally enables CapturePreparation:
the supported declarative project/import/source/reference closure is copied before
MSBuild evaluation. Explicit installed SDK/runtime files are read-leased, processes
use a private environment/home/temp and offline NuGet configuration, and original
source/diagnostic paths are preserved. This Windows Microsoft.NET.Sdk library profile
rejects custom tasks/targets, MSBuild functions, custom SDKs, package/project graphs
and external globs before SDK execution. Installed toolchain directories and the
Windows/native OS remain trusted; this is not an arbitrary-MSBuild sandbox.
Legacy requests can retain trusted preparation.
Scene Start opts into process-local ReuseArtifact: unchanged verified inputs and
pinned payloads reuse the last managed DLLs, while each new ticket republishes its
manifest and each Start captures a new scene. Explicit Build compiles again. Prestart
verification remains mandatory; cold/changed/unavailable payloads rebuild normally.
The existing Replace action now builds and applies code for the exact owned running
project/document snapshot. It keeps the old world running during compilation, verifies
tracked inputs before Pause, and uses the exact expected context version for the
checkpoint/recovery transaction. Compiler failure leaves the old world alive; failed
candidate restore can recover old code/state/mode. Explicit Stop/exit fences recovery.
Authoring changes still require Stop/Start; parent document/history/selection remain intact.
Replace requires ICheckpointGameSession. The document's Runtime operation also fences
direct edit/IO APIs for the full asynchronous transition; Close cancels and drains it
before disposing the authoring product, including SDK verification before Pause.
This slice does not add live copyback, gameplay input/HUD
or standalone Player features.

The aggregate also registers a retained diagnostics console, opened through
View / Diagnostics in the standard header. It borrows IDiagnosticRecordReader
from bounded UseDiagnostics; without one, the console explicitly shows history
as unavailable and creates no collector. Source, minimum level, event, exact
session/generation/role and text filters, repeat grouping, details/Ctrl+C, clear
view and follow-tail pause operate on a bounded presentation window. The default
limits are 512 records, a 1 MiB payload estimate, 8192 characters per record and
64 records per 100 ms. Ingestion continues while follow-tail is paused or the
panel is closed. Call EditorSession.Present on the UI owner in embedded hosts.
Register UseAetherEditorDiagnostics(new EditorDiagnosticsOptions { ... }) from
TrueMoon.Aether.Editor.Diagnostics before UseAetherEditor to customize these limits.

The mode button selects Structured, RAW stdout, RAW stderr or Health. Raw modes
show independent process snapshots with session/generation/role/build/job/revision
correlation, capture eviction, display-window omission, file loss/errors and reader/
file completion. Stderr does not imply error severity. Defaults add at most 16
processes and 4096 characters per stream; MaximumProcesses/MaximumRawCharacters
are copied from EditorDiagnosticsOptions. Runtime tracking keeps at most 64 recent
leases; compiler memory history keeps 16 completed jobs plus its current job.
These are memory windows, independent of disk retention. Consumers may implement
IRuntimeDiagnostics on a custom runtime factory; otherwise runtime capture is
explicitly unavailable. App UseAether provides the non-disposable aggregate
IProcessDiagnosticsSource; the Editor borrows it and IDiagnosticHealthReader.

Source/session/generation/role/search filter raw snapshots; level/event/group/clear
belong to structured events. Health has no log filters. Selected raw/health details
remain frozen across polling/filter/theme changes until Refresh details or bounded
history eviction. Polling never reads files or waits for IPC, SDK or process cleanup.
Core health belongs to the parent App; child sink health is not supplied by the
delivery protocol. Delivery loss/unconfirmed and capture/file loss remain separate.

Export without text writes a new, explicitly partial JSON metadata report, up to
512 KiB with a cooperative five-second deadline. It includes event identifiers,
allowlisted correlation, versions/OS, presentation limits and observed gaps and
truncation, numeric parent collector/sink and process capture/delivery health when
available. Metadata that cannot fit the bound is explicitly counted/marked omitted.
Message/exception/stack/caller/raw/error text and arbitrary properties are omitted;
source/event identifiers remain. Existing files are never overwritten. App stop
cancels and drains accepted export work. Persistent history import, child sink
health, collector configuration and backend/device details are explicitly
unavailable. These omissions, optional raw-text export policy and compiler navigation remain
open editor tasks; a partial report does not promise recovery of a lost async tail.

ARCH2c provides explicit launch, shared `RuntimeBootstrap.ReadEnvironment()` and
pair identity. The child executable configures UseAether from `.Launch`, registers
UseAetherRuntime or UseAetherPreviewRuntime with `.ParentProcessId`, then runs the
App-owned runtime host. Main/App composition, diagnostics and reports remain in
the executable. `Examples/AetherRuntimeHost` is a separate example without
Editor/UI dependencies.

`BuildIdentity.ForRuntime` covers labelled host/runtime entries plus exact shared
Aether/Contracts/Enerit/Mithril binaries, checking both entry directories and actual
entries. It is a trusted-local compatibility check, not authentication or a complete
native/private-dependency hash. Do not mutate deployments in place during a session.
Different pair identities invalidate old game artifacts/checkpoints; rebuild games.
Scene/project formats and control3/frame1/snapshot2 are unchanged. See the
[Aether runtime contract](../TrueMoon.Aether/PACKAGE.md) for bootstrap bounds.

The directed smoke consumer AetherEditorHost explicitly selects
`NativeRuntimeLaunch.CurrentProcess("--runtime")` with the previous `BuildIdentity.HostId`
as a compatible same-executable profile. The minimal
[AetherEditor example](../Examples/AetherEditor/README.md) selects a separately
deployed runtime and owns its font/OFL payload. Themes/styles are managed code;
SPIR-V is embedded in Aether, and rendering/platform modules supply native assets.
The package performs no repository or ManualTests font lookup. Installing it never
makes an arbitrary executable a runtime. See the [deployment verifier](../Examples/README.md#verify-deployment-without-opening-a-window)
for source/published resource and headless checks. The [independent consumer](../ManualTests/AetherPackageConsumer/README.md)
adds NuGet build/publish and standard/embedded native acceptance. The bounded
Windows x64/net10.0/OpenGL/Vulkan profile passed on 2026-10-29, including actual
font/extended-native loading, Transform/preview/Undo/Redo/save/reopen and
startup/close/crash recovery. Custom hosts own first-presentation readiness and
their viewport adapter; keep the GL owner alive until runtime/UI drain completes.
These checks do not establish physical OS input/DPI/monitors, driver/device loss,
K7 stress, VRAM or general release readiness. No NuGet publication is implied.

Project manifest formatVersion2 adds an explicit `dependencies` array of distinct
flat private managed DLL names (at most15, excluding entryAssembly). Version1
remains readable/writable with the original eight fields and no private dependencies.
Resolved allowlists and build requests own immutable snapshots of those names.
Scene Build/Start/Replace enable CaptureProjectReferences, extending the default
compiler profile to at most four net10.0 C# libraries with unconditional literal
references and no metadata; source directories must not overlap. All source/import/config inputs are frozen before SDK
execution; each node compiles from copies with isolated intermediates. Verification
and reuse cover the full graph and pinned selected payload. Only the child Game
loads these private assemblies. PackageReference without packageDirectory, cycles, custom tasks, mixed SDKs
and wider graph profiles remain unsupported.

Project formatVersion3 adds optional `packageDirectory`, a contained existing local
directory of lowercase `id.version.nupkg` archives. Versions1/2 remain readable and
writable with their previous fields. ProjectSession maps this path to the compiler's
absolute PackageDirectory. Every package-consuming project needs a schema1/net10.0
packages.lock.json; stable literal direct versions and unsigned archive SHA512 must
match. All package DLLs join the explicit dependencies allowlist. The initial
profile admits one managed lib target, with no signatures/build/analyzer/native/
content/ref assets; the Aether package documents exact limits. Build/Start/Replace
and reuse use the same before/after/prestart capture and cleanup fences. Source
archives/locks remain untouched; only child Game loads the selected private DLLs.

## Source observations and retained indicator

Full `UseAetherEditor` registers an inert, project-owned `EditorSourceWatch` factory
before project factories. No files, watcher handles, worker or SDK are started at
project open. Successful scene Build/Start/Replace preparation binds its actual
current candidate (including reuse); failed/cancelled/stale work preserves the
previous baseline. Metadata-only `UseAetherEditorProjects` remains valid without
tracking. A factory registered before the full editor is preserved.

Configure copied defaults through `AetherEditorOptions.SourceTracking`:
Enabled=true, UseFileSystemNotifications=true, Debounce250ms, MaximumDebounce1s,
PollInterval10s, ScanBudget5s and MaximumWatchers32. Register `TimeProvider` before
the editor to override scheduling. The default product never observes later
mutations of the supplied options. Explicit opt-out affects the default factory.

The observer hashes original descriptor inputs and file membership, including
expected-absent probes. It performs no SDK evaluation or package extraction.
Notifications are bounded hints; full polling also covers lost events, failed
attachments and targets exceeding the handle limit. Unknown/unstable/locked reads
do not become Same. UNC/network/reparse paths are unavailable in this first profile.
Known Changed is sticky until a new successful baseline; scene edits/Save/Undo,
selection and game Stop do not reset it. These observations never replace compiler
preflight or trigger automatic Build/Play/Stop.

ProjectSession owns stop/fence/actual worker+handle drain and retry after cleanup
failure. Views and models borrow it. `EditorViewModel.Refresh`/`EditorSession.Present`
pull one immutable current-owner snapshot; worker callbacks never notify controls.
The retained status segment and details keep source state separate from primary
runtime/command/error status. The standard window is continuous. A custom host
must call Present/owner Refresh regularly (at most1s while active), or provide its
own owner-thread wakeup. An on-demand host without a pump displays its last read
snapshot; no hidden cross-thread UI posting is performed.

Explicit preview-view integration uses the matching Aether Control4/Frame2 binaries.
EditorSession.Present acknowledges a stamped frame with its actually drawn image,
clip and layout revision. ReadPresentedView(view), called on the UI owner, returns
only an actual receipt admitted by current camera/context/runtime/surface and drawn
layout/visibility; newer received FrameIds of the same view are allowed. It does not
select or clear entities itself. Default GPU document preview uses explicit opening/
view/content identities. SceneDocumentState.OpeningId renews on every successful
opening, including the same immutable scene; Workspace.ContentToken renews for committed
content and SelectionRevision advances for every explicit Select/Clear intent.

App owns EditorViewportState and the preview coordinator. A View owns its UI-thread
IFactory<EditorViewportController,EditorViewportArguments> product and subscriptions;
dependencies are borrowed. One attached binding owns frame delivery/input; another
view cannot steal it. Detach/dispose releases capture and schedules tracked Stop/drain.
The previous two-argument inspector constructor remains available; full composition
injects the workspace to capture committed source coherently before worker dispatch.

RMB orbit, MMB pan, wheel dolly, and F on a focused canvas update only the transient
camera. LMB click/miss uses the shared selection owner and existing inspector draft
policy. Pending/stale/unsupported views, Play, overlays, chrome, letterbox and foreign
capture cannot select. Esc/focus/capture cancellation restores pose with a fresh camera
revision; source/context changes discard the old gesture. Commits/Undo/Redo preserve
the camera; reopening/project/binding renewal resets it. UI resize/DPI/hide invalidates
drawn geometry; resume requires fresh actual presentation. Initial hidden preview waits
for visibility before allocating a runtime. A custom external transport preserves the
legacy Show path and supplies its own presentation policy. Bounded Windows x64
source/published/fresh-package GPU lifecycle and supported Windows input acceptance
passed. Moving RMB/MMB/Esc is directed UiSession/native GPU evidence; physical
held-button and multi-monitor DPI remain separate hardware/input gates.

Validation UI uses a red icon at the right edge of TextBox/RichTextEditor and
numeric fields. Hover shows the exact validator reason; long text wraps/scrolls.
Inline field error rows are replaced; the shared copyable Diagnostics panel remains.
Drafts, selection, Apply/Cancel/Undo/Redo stay with the existing models. Editor island
styles use Theme.PanelShadow; menus use Theme.MenuShadow, with local Shadow=null
available. Full Editor D/R regression passed; full release/platform gates remain separate.

## Unified startup configuration

Add the exact `AetherConfigurationSchemas.Engine`/`Authoring` and
`EditorConfigurationSchemas.Options`/`Diagnostics` instances to the App profile,
then call `UseConfiguredAetherEditor(composition)`. Initial project/source-tracking
and diagnostic budgets are validated before registration. Add `SourceEditor` and
`UseConfiguredAetherEditorSourceNavigation` only when launching a source editor is
wanted; its command remains explicit. The standard window uses `Editor:Window`
through `UseConfiguredAetherEditorWindow(fonts)` with 1280x800 defaults and a
1100x700 minimum. Fonts, viewport factories, Build/Play captures, documents,
selection and camera state remain with their existing owners. All schemas are
startup-only. The library has no direct reference to the TrueMoon bootstrap assembly.
