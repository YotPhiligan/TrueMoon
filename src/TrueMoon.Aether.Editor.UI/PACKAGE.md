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

In `1.2611.25.9-alpha` the hierarchy panel exposes its layout, overlay host,
action buttons, hint, diagnostic and scroll viewer as generated C# extension
properties. Rebuild consumers and import `TrueMoon.Aether.Editor.UI.Hierarchy`.
The property names are unchanged in source; they are no longer instance getters
for reflection or previously compiled binaries. Read parts after the first
attachment and before disposal; retain a control reference before disposing the
view if you need to inspect its disposal.

In `1.2611.26.5-alpha`, `EditorDocumentView` exposes its retained controls as
generated extension properties. Import `TrueMoon.Aether.Editor.UI.Documents`
and rebuild consumers; these are not CLR instance getters. Look up parts on the
UI owner thread after attachment, and retain a reference for disposal checks.
`ConfirmPath`, `CancelPath` and `DismissTools` exist only in compact mode.
`ScenesRoot` and `Choices` are styling roles, not public parts.
`Commands` was removed: compact actions live in the File menu; standalone
command buttons use semantic IDs such as `editor.document.command.Save`.
The constructor still borrows its model and style and accepts `compactHeader`.
Style failures occur during attachment; the caller disposes an unpublished view.

In `1.2611.26.6-alpha`, `EditorTransformView` builds its form on first attachment.
Rebuild consumers and import `TrueMoon.Aether.Editor.UI.Editing` for the generated
action and status parts. Read `Fields` and named parts only after attachment,
on the UI owner thread. `PreviewActions`, `ShowPreview`, `StopPreview` and
`PreviewStatus` exist only when the form hosts inline preview; use `TryGetPart`
when the mode is not known. `Form`, `Actions` and `History` are no longer public
parts. The public field, label and input style tokens remain unchanged; the
unused `ErrorRole` was removed. A field validation error is rendered by the
input itself. Style failures now surface at attachment.

In `1.2611.26.7-alpha`, `EditorStatusPopupView` prepares its content when an
overlay attaches it. Rebuild consumers for the generated extension parts and
resolve them on the session owner thread while the popup is live. `Body`,
`PreviewActions` and `PreviewDetails` are no longer public parts. A failed
style surfaces at attachment; the popup owner disposes the unpublished root.

In `1.2611.26.8-alpha`, `EditorStatusBarView` also prepares content at attachment.
Rebuild consumers for its generated extension parts and resolve them on the
session owner thread after attach. `EditorView.Status` follows this rule when
backed by the compact status bar. No control registration is needed to host the
status bar's internal layout and controls.

In `1.2611.26.9-alpha`, status text, availability, colors and actions all use
the status model supplied to the factory. Assigning a different `DataContext`
does not retarget the bar or its popup.

In `1.2611.26.10-alpha`, `EditorHeaderView` prepares its tree and style
at attachment. `Menu`, `BrandIcon`, `Caption` and the action buttons are
generated extension properties with the same names and types. Rebuild
consumers, import `TrueMoon.Aether.Editor.UI`, and resolve these parts after
attachment on the UI owner thread. They are no longer instance getters for
reflection or binary callers. Command objects, the window-specific factory
arguments and their ownership are unchanged.

In `1.2611.26.11-alpha`, the header uses `ObserveState` for its borrowed
models. The header and inspector share delayed hover validation. A pending
hint waits for a resize to update layout before it can open.
