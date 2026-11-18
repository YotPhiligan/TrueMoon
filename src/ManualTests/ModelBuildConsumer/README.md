# Model delivery acceptance

Run from the repository with its pinned SDK on Windows x64.

```powershell
pwsh -NoProfile -File ManualTests/ModelBuildConsumer/Verify.ps1 -EvidenceDirectory TestResults/ModelBuild-20261118/<fresh-run>
```

The runner publishes the Assimp worker, builds source Debug authoring and
Runtime consumers and repeats the scenario from a closed local NuGet feed
with published Release consumers. Both profiles run on Cobalt and Microsoft DI.
Authoring uses the real importer, ledger, Nom and game compiler. The Runtime
executable references the matching Runtime package, loads the game via
`GameBuildLoader` and inspects an owned render catalog lease.

The report includes both build/snapshot IDs, persistent asset IDs, topology,
vertex positions, geometry revisions and the Runtime assembly list. Logs
and package hashes stay in the selected evidence directory. A corrupt payload
test runs in a third process using a separate copy of the build.

[Contract and ownership](../../docs/engine/MODEL_BUILD_DELIVERY.md)
