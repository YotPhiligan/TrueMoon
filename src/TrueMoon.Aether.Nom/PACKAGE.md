# TrueMoon.Aether.Nom

`UseAetherNomContent()` also provides `IFactory<IModelImportContentPublisher,
IProjectAssetStore>` for reimporting selected models in an Aether document. Each
publisher borrows an open project store. It publishes a model's render closure,
then captures the exact Nom snapshot as detached build input. The store owner
controls durable revision and snapshot retention.

Register `UseAetherNomContent()` at the authoring App composition root alongside
`UseNom()`. Resolve `NomModelContent` and borrow an open project store. Publish a
committed `PublishedModelImport` against the expected Nom generation, then capture
its exact snapshot into `GameBuildRequest.RenderContent`. The compiler includes
those bytes in the build identity. Runtime exposes validated content through
`GameBuildLoader.Artifact.RenderContent` for use with its render catalog.

For a scene with several models, pass their exact snapshot IDs to
`CaptureSnapshotsAsync(store, snapshotIds, token)`. The input can contain 1–32
snapshot IDs from the same store. Repeated assets with the same canonical bytes
appear once, and different revisions of one AssetId cause capture to fail.
Snapshot order does not affect the build identity. Keep the snapshots retained
until capture completes.

Store and snapshot retention belong to the caller. The bridge closes its read lease
and streams after capture. Build artifacts carry a self-contained copy of the render
closure. Runtime consumers only need the matching Runtime package.
