# TrueMoon.Aether.Tooling

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
