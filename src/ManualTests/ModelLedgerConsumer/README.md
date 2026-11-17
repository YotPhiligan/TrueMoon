# Stable model identity and atomic sidecar acceptance

Run `pwsh -NoProfile -File ManualTests/ModelLedgerConsumer/Verify.ps1 -EvidenceDirectory <fresh-directory>`
with the repository SDK/profile. The script retains a closed feed, fresh cache, worker
closure/licenses, logs, binlogs, native proofs and reader/publisher process evidence.

Ten profiles cover source Debug/published Release and fresh package Debug/published
Release on Cobalt and Microsoft DI, plus a Debug-worker pair. Each profile first runs
the prior g.1b native/failure/cleanup proof, then imports authored semantic locators and
checks permanent mesh/material/slot/binding/instance IDs through geometry edits,
discovery reorder, explicit rename, deletion/tombstones, explicit restore and source
glTF→GLB conversion. Global ambiguous names, including unused objects, require labels.
Unicode names and captured filenames are checked with the real pinned native provider.

Independent reader processes reopen complete records. A publisher process repeatedly
commits a grid candidate while another reader checks coherent monotonic snapshots;
after an owned staging file is observed the publisher is killed and reaped. The head
must decode as a complete old or new generation, and a later writer must acquire the
OS-released lease. This does not claim kill at one specific instruction or power-loss
durability. Crash leftovers are retained as ignored evidence; they are never read as head.

Fresh package consumers also copy a source-published ledger, verify its exact identity
fingerprint and republish the copy while preserving IDs. Source delivery is unchanged.
Loaded framework hashes must match the fresh package cache, whose graph contains zero
ProjectReferences and no Assimp. Assimp lives only in the reviewed separate worker.

See [contract/limits](../../docs/engine/MODEL_SUBASSET_LEDGER.md). No Nom, build/runtime,
GPU, other OS/AOT or full release acceptance is implied. No remote package publishing.
