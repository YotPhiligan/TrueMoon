# Bounded Mithril child-process baseline

Run from `src` with PowerShell 7 and .NET 10:

```powershell
./ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --logger "trx;LogFileName=mithril.trx" --results-directory TestResults/MithrilBaseline/tests
```

The runner builds Debug and publishes framework-dependent Release without a RID,
then executes nine cases through each apphost (18 total). Logs, source SHA256, commands,
PID/identity, exit codes and a `Completed` report are written to ignored
`TestResults/MithrilBaseline/manual`. Each process has a timeout; the runner only
kills its own probe tree. A child also has a 15-second deadline.

The xUnit project copies this executable fixture into its output and runs the
same cases through `dotnet MithrilLifecycleProbe.dll`, checking hosted launch as
well as unit identity, observed exit, released handle and repeat cleanup.

| Case | Required behavior |
| --- | --- |
| `graceful` | Child acknowledges `qe`, exits 0 before stop returns |
| `timeout` | Child ignores `qe`; stop kills and waits for exit |
| `cancel-stop` | Pre-cancelled stop shortens graceful waiting and still cleans up |
| `dispose`, `async-dispose` | Disposal stops an active child and releases its process handle |
| `already-exited` | Natural exit produces one callback; subsequent cleanup succeeds |
| `same-name` | Starting an empty controller preserves another owned test instance with the same executable name |
| `duplicate-start` | A second start is rejected without replacing or leaking the first child |
| `throwing-sink` | A diagnostic sink throws after every write; owned child still exits gracefully and its handle is released |

Reports include `UnitProcessDiagnostic` snapshots: launch PID/run/generation,
natural exit versus intentional stop, forced kill, wrapper release and completed
cleanup. The xUnit assertions check these against independently held OS handles.
Use a fresh `-ResultsDirectory` for each retained run. See
[operational diagnostics](../../docs/mithril/DIAGNOSTICS.md).

The production `ChildProcessUnitHandle` launches the current apphost or hosted
entry assembly. The probe subclass adds child arguments and redirects output
for observation; it does not replace process start/stop/ownership behavior.
Readiness is an explicit child identity record, so timing guesses are unnecessary.

This proves the handle boundary with `RestartPolicy.Never`. It does not establish
`AddUnit`/App composition, parent crash, restart policies, MainProcess/External
hosting, other OSes, IPC, or independent NuGet consumer readiness. The existing
`MithrilTest` IPC example is separate. See [Mithril status](../../docs/mithril/STATUS.md)
and [accepted scope](../../docs/mithril/PLAN.md#mit0-own-child-контракт--2026-10-11).
