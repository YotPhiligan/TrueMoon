# TrueMoon.Aether.Editor.Tooling

Optional full Editor workflow over `TrueMoon.Aether.Tooling` and `TrueMoon.Aether.Editor.UI`.
Headless Tooling and Runtime + UI consumers do not need this package.

Call `UseAetherEditor` / `UseConfiguredAetherEditor` from the consumer App using
`TrueMoon.Aether.Editor.Tooling`; Views/styles and presentation data are in
`TrueMoon.Aether.Editor.UI` and its semantic subnamespaces. The optional standard
window uses `UseAetherEditorWindow` / `UseConfiguredAetherEditorWindow`.
Existing typed options, exact schema instances/keys, factory overrides and
open/edit/save/build/play/stop/Save-Discard-Cancel behavior are retained.

Concrete project/document/inspector/hierarchy/status/diagnostic ViewModels implement
UI-owned presentation ports without exposing Tooling types to Views. App owns models
and command/session services once; Views borrow them and own their subscriptions,
child controls and factory-created viewport controllers. Embedded hosts own View/UI,
call `EditorSession.Present` after presentation and drain before context release.
Selection/camera/draft/history ownership and stale-generation fences remain in adapters.

For imported models, register `UseAetherModelSourceCapture()` before Editor. Attach
the selected generations and their exact frozen render payload to the open document
with `SceneDocumentSession.SelectModelImportsAsync`. The status popup shows source
freshness per selected model. Building from the document checks each source before
compilation. The published build chooser opens the immutable artifact as before.
Register `UseAetherModelImport()` before Editor and attach `SceneModelReimportSettings`
to each selection that should offer a reimport button. It carries the existing
ledger, trusted worker deployment, scratch directory and project publisher. The
document command replaces that model's frozen resources once publication succeeds.
With `UseAetherSceneModelSelectionPersistence()` registered before Editor, opening
a scene restores its saved model selection and source statuses. Projects choose
whether to reconnect the reimport action through the trusted settings factory.
With `UseAetherProjectModelCatalog()` enabled, the status panel offers **Project
models**. Its checkboxes update the resource selection for the active scene.
Models come from the authoring pipeline's project registrations.

This replaces the previous `TrueMoon.Aether.Editor` package. Update PackageReferences
and namespaces and rebuild matching hosts/games/artifacts/captures together. No facade
or type forwarding is supplied; scene/project/control/frame/checkpoint formats stay
unchanged. Mod loading/hot replacement is a later architecture stage.
