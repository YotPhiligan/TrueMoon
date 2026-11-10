# Unified module configuration acceptance

Run `./Verify.ps1` from PowerShell with the repository's .NET 10 SDK and restored
dependencies. The runner builds a separate source Debug consumer, publishes it in
Release, packs 16 local TrueMoon packages with a unique CORE7.5 version, then
restores and publishes the same program with PackageReferences only in a fresh
cache. Every profile runs from an unrelated working directory on Cobalt and the
Microsoft DI adapter. It copies only exact existing external package versions;
no network feed is used. Run/evidence directories must be fresh.

The program verifies common file/environment/argument binding, explicit overrides,
detached snapshots, real ECS entity admission and scheduler ticks, empty Editor
startup, actual caller-owned Nom stores and quota/cleanup, transport schema,
startup-only reload rejection, lazy window composition, runtime flag aliases and
the Editor library's independent bootstrap boundary. Environment values are set
only on each consumer process. No child runtime, native window or GPU is started.
The report records the exact assertion count and both DI providers.

Optional `-EvidenceDirectory` retains logs, unique binlogs, package/source hashes,
graph provenance and per-profile reports; `-RunDirectory` selects the independent
scratch directory outside the checkout. These directories are deliberately kept
for investigation. This proof does not replace native/physical release acceptance.
