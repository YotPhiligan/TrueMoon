# TrueMoon.Aether.Tooling

Optional model import: `UseAetherModelSourceCapture()` provides caller-owned capture
operations, and `UseAetherModelImport()` provides a caller-owned
`IFactory<ModelImportOperation, ModelImportArguments>` plus an App-owned admission/stop
host. Supply captured glTF2/GLB bytes, a reviewed separately published Windows x64
Assimp worker manifest, an absolute dotnet host and scratch directory. Import yields
validated immutable mesh/material/instance CPU candidates only. One active operation,
zero queue; default deadline10s, maximum30s, kill/reap before release. No Assimp package
is loaded into Tooling and no native activation occurs at registration. Dispose factory
products; App Stop drains active import/publication work. Native cleanup failure retains
admission for explicit DisposeAsync retry.

The same opt-in module provides `IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>`.
`OpenAsync` reads a bounded checksummed sidecar without creating/migrating files.
`PublishAsync(candidate, expectedGeneration, edits)` atomically commits the CPU candidate,
permanent mesh/material/slot/binding-set/instance IDs and tombstones in one file.
Authored semantic names or `extras.truemoon.subasset` labels drive matching; ambiguous
keys require explicit labels, rename/restore require explicit same-kind mappings.
Persistent IDs survive content and discovery-order changes. SourceId is caller-owned;
move its record with the source and allocate a new SourceId for a copy. Cooperating
writers serialize on a permanent `.write-lock` sidecar; failed/stale/cancelled precommit
work preserves the old head. Store scope is local Windows process-crash consistency.
Nom, runtime manifests, build delivery and GPU activation remain separate integration.

Before assembling a new build, use a fresh capture operation's
`CheckFreshnessAsync(published, geometryScale, token)` for each selected import.
It compares a newly captured source closure and the requested scale with the exact
published import that supplied the build resources. Results are `Current`,
`Stale`, or `Unavailable`. This read-only check needs no worker deployment.

`UseAetherModelSourceCapture()` also registers
`IFactory<ModelSourceWatch, ModelSourceWatchArguments>`. Create a watch for
each selected published import and read its latest comparison with `Read()`.
File notifications wake the same byte-level check and complete polling covers
missed notifications. Dispose the watch when replacing the selected import or
settings. App shutdown drains outstanding watches. Use an explicit freshness
check when admitting a new build.

Project-owned Editor documents can manage these watches through
`SceneDocumentSession.SelectModelImportsAsync`. Register model source capture before
document services. Provide the current opening ID, published imports and the exact
frozen `GameBuildRenderContent`. The document session owns the watches and releases
them on reselection or close.

For an explicit reimport, attach `SceneModelReimportSettings` and use
`SceneDocumentSession.ReimportModelAsync` with the displayed opening, source ID and
ledger generation. The document validates compatible placements and resource IDs
before committing the worker result. It switches the selected resources after the
project publisher returns the exact new closure. The published ledger can be reused
on retry when the captured input and settings match.

Enable `UseAetherSceneModelSelectionPersistence()` before Editor document
registration to retain selections across openings. Explicit selection or successful
reimport saves the exact published generations in a project-local record keyed by
scene ID. Opening reads this record and reconstructs detached build resources.
Optionally supply `IFactory<SceneModelReimportSettings, SceneModelReimportRestoreArguments>`
to reattach trusted worker and project publisher settings to a saved ledger reference.

Headless project/document authoring, transactional history, scene persistence,
SDK preflight, frozen compilation/capture/publication, source observation,
preview coordination and bounded diagnostic export. No Editor/Alloy/Argentis
dependency. Runtime provides resolution and execution contracts; no runtime,
window or graphics/audio activation occurs during registration/empty startup.

Register once through `IAppConfigurationContext.UseAetherTooling(options)` with
explicit `HostContractId` (exact target host/pair identity) and `BuildDirectory`.
Resolve `ToolingHost` at the composition root. `host.Projects` owns project openings;
`ProjectSession.Documents` supplies load/edit/Undo/Redo/save/reopen. `host.BuildAsync`
builds a frozen candidate and updates LastGoodBuild/source baseline only after
successful owner checks. It never starts/replaces a game. Failed/cancelled Build
preserves the selected candidate and document. The low-level borrowed `host.Builds`
does not automatically select results or bind source baselines.

App alone owns ToolingHost and EditorProjects. Host alone owns its compiler product;
Projects owns active/retired project/document/source products. Factory products are
caller-owned, with no second disposable aliases. Existing stores/preflight/factory
overrides are preserved. Normal transitions require Save/Discard/Cancel. Terminal
Stop fences/drains the compiler and explicitly discards unsaved memory while closing
project products; it does not save files. Cleanup failures retain owners for retry.

For common App configuration add the exact ToolingConfigurationSchemas.Settings
instance to the profile, then UseConfiguredAetherTooling with explicit
ToolingComposition. Aether:Tooling:* contains startup data, not documents/state.
Host identity and concrete Runtime capability remain explicit composition inputs.
Default game ABI is Abstractions + Contracts; RuntimeAccess is opt-in. Artifact
schema2, exact identities/hashes and private shared-copy rejection remain strict.

The former full workflow now lives here: UseAether/AetherHost/GameEditor/ScenePreview
connect Runtime explicitly, and are mutually exclusive with the headless aggregate.
Migrate old TrueMoon.Aether package/CLR imports; no forwarding facade is supplied.
Rebuild hosts, game artifacts/captures from one coherent delivery. Scene/project/
control/frame/checkpoint formats stay unchanged. UI/optional bridge split is ARCH3.5.

ARCH3.6 adds the explicit Mods.ModArtifactPublisher utility for content-addressed
managed module directories. It copies private bytes and exact shared API hashes,
publishes by atomic directory rename and rejects corrupt existing publications
without overwrite. Runtime performs host/group admission and collectible loading;
the Runtime consumer does not receive Tooling. Live system replacement and World
migration remain separate stages.

Acceptance covers Windows x64 source, published and fresh package-only applications
on Cobalt and Microsoft DI. Other OS/RID, AOT/trimming, device loss, production mod
reload and general release remain separate acceptance tasks.
