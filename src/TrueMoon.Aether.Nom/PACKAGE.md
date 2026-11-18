# TrueMoon.Aether.Nom

Register `UseAetherNomContent()` at the authoring App composition root alongside
`UseNom()`. Resolve `NomModelContent` and borrow an open project store. Publish a
committed `PublishedModelImport` against the expected Nom generation, then capture
its exact snapshot into `GameBuildRequest.RenderContent`. The compiler includes
those bytes in the build identity. Runtime exposes validated content through
`GameBuildLoader.Artifact.RenderContent` for use with its render catalog.

Store and snapshot retention belong to the caller. The bridge closes its read lease
and streams after capture. Build artifacts carry a self-contained copy of the render
closure. Runtime consumers only need the matching Runtime package.
