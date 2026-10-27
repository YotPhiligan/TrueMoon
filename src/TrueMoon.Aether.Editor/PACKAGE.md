# TrueMoon.Aether.Editor

Reusable editor module for TrueMoon App, targeting net10.0. Includes retained
View/Data/styles, scene hierarchy/inspector, project/document coordination,
commands and an optional Windows x64 Skia/OpenGL window integration.

The consuming executable owns Main, CLI, App composition, diagnostics, fonts,
report paths and runtime bootstrap. This package creates no nested App and
contains no executable entry point or smoke checks.

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

For an embedded UI, omit UseAetherEditorWindow. Create a fresh EditorView through
IFactory<EditorView> on the parent UI owner thread; the parent owns its tree.
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
for source/published resource and headless checks; GUI/native acceptance and
independent NuGet consumption remain open. No NuGet publication is implied.
