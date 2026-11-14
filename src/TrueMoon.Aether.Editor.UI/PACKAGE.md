# TrueMoon.Aether.Editor.UI

Independent Editor views, data, styles and presentation ports over Runtime and Alloy.Hosting.
This package has no Tooling/compiler/project dependency.

Use `IAppConfigurationContext.UseAetherEditorUi(PlayerStart, Action<RuntimeOptions>)`
in the consumer App. Prepared artifact/scene and matching runtime identity are explicit.
The module registers one borrowed-runtime session, model, style and `IFactory<EditorView>`.
Optional `UseAetherEditorUiWindow(fonts)` provides the standard Windows/OpenGL host.
`UseConfiguredAetherEditorUi` binds the exact `RuntimeConfigurationSchemas.Engine`;
`UseConfiguredAetherEditorUiWindow` binds `EditorUiConfigurationSchemas.Window`
(`Editor:RuntimeWindow`) from explicitly registered schemas/sources.

Start/Play/Pause/Step/Stop/Cancel, actual GPU viewport and read-only captured-scene
inspection are available. No source build, project/document IO or game load happens
at registration/idle startup. Build/Replace and authoring panels are unavailable.
The game owns its runtime camera; inspection is not a live World edit API.

Create views through the App factory on the UI owner thread. Parent owns View/controller;
View borrows models/ports. Embedded hosts call `RuntimeEditorSession.Present` only after
actual presentation, and drain Stop before releasing their rendering context. One live
Runtime View presents each App. RuntimeEngine stays App-owned. Mesh Playing retains its
actual render-view fence; stale tickets are not acknowledged.

For full project/document/build/drafts/history/preview capabilities, add the optional
`TrueMoon.Aether.Editor.Tooling` package. Previous `TrueMoon.Aether.Editor` imports/package
require migration and a coherent rebuild; no binary forwarding facade is provided.
