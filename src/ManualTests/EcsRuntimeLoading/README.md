# ECS Runtime scene acceptance

Run from the `src` workspace on Windows with the cached .NET10 dependencies:

```powershell
& ./ManualTests/EcsRuntimeLoading/Verify.ps1
```

The script builds the source Debug proof, publishes parent/runtime Release, then
packs seven current framework packages and creates independent parent/runtime/game
projects without ProjectReferences in a fresh temporary directory. Restore uses
only a new local feed and empty cache; exact cached external nupkg are copied to
that feed. It never changes checkout versions, NuGet settings or source dependencies.

Every profile uses a selected `Game.Logic.WorldProbeGame` in a separate child,
complete inherited scene data beyond the64KiB/64entity legacy projection, CPU pixel
checks before/after Step, repeated generations, explicit Stop/reap and ownership
checks. The parent never resolves a Game.Logic CLR type. No GPU/window/release
acceptance is inferred. The [contract](../../docs/engine/ECS_RUNTIME_LOADING.md)
defines initial-only transfer, cancellation/drain and cleanup retry.

Optional `-EvidenceDirectory` and `-RunDirectory` must name fresh directories;
RunDirectory must be outside the checkout. Logs, report JSON, source hashes,
package/graph provenance and unique per-command binlogs are preserved on failure.
The script creates files and starts only its owned proof children; no cleanup/delete
of previous runs is performed.
