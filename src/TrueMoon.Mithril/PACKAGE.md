# TrueMoon.Mithril

Alpha .NET10 App-local units and owned process lifecycle. Configure through
TrueMoon App/AddUnit. The package depends on Contracts/Core and does not depend
on Enerit or UI. Add Enerit and its generator explicitly when typed IPC is needed.
For default Cobalt, its generator is bundled in TrueMoon.Cobalt; Microsoft DI
can use TrueMoon.Extensions.DependencyInjection without the Cobalt analyzer.

Main services share the App graph. ChildProcess starts the current apphost/hosted
assembly with selected unit/parent environment; External launches and owns the
specified executable. Borrowed attach is not provided. Only owned processes are
stopped; other applications are not located or killed by executable name.

App lifecycle starts Immediate units; Delayed/None use explicit Spawn. The verified
scope is App lifetime, bounded Windows x64/net10.0 owned child handling. The child
uses an asynchronous per-launch control pipe and observes its parent. Stop requests
graceful exit, then kill/reap of the owned tree if its deadline expires.

Never does not restart; Once permits one attempt; OnCrash handles nonzero exit;
Always handles any exit. RestartLimit defaults to3 automatic attempts and
RestartDelay to250ms. Success does not reset the budget. Each generation has a
new Process/control endpoint. Only terminal exit cancels controlling App lifetime;
Stop/Dispose prevent further scheduling and drain the monitor.

Deploy the full build/publish output, including apphost or hosted assembly, deps,
runtimeconfig and dependency DLLs. A child starts that delivered consumer, not a
source project. The source repository's ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1
checks isolated package delivery and own-child typed IPC/restart/rollback/parent-exit.
Packages remain in a local feed. Other OS, arbitrary hooks/callbacks, long resource
trends, independent external restart and uncooperative services have separate criteria.

Enable diagnostics explicitly through the TrueMoon App configuration context:

```csharp
context.UseDiagnostics(d => d.OnEvent(e =>
{
    if (e.Category == "UnitProcess" && e.Payload is UnitProcessDiagnostic p)
        Console.WriteLine($"{p.RunId}/{p.Generation} unit={p.UnitIndex} pid={p.ProcessId} " +
            $"{p.Stage} reason={p.Reason} exit={p.ExitCode} error={p.Error?.Message}");
}));
```

Use namespaces TrueMoon.Diagnostics and TrueMoon.Mithril. A RunId identifies one
explicit start; Generation counts launch attempts (including failed preparation).
Started means OS start, not application readiness. Snapshots carry effective policy,
budget/delays, consumed restart attempts and owner PID. LaunchFailed/SequenceFailed
retain the original Exception; Terminal reasons distinguish Policy/RestartLimit/Failure.
ProcessReleased confirms wrapper disposal; Stopped follows cleanup and monitor drain,
except TerminalCallback skips self-wait. Repeated Stop can emit NoProcess. Concurrent
Stop notifications may interleave. Sink exceptions cannot prevent process cleanup;
blocking/reentrant callbacks are not supported. Arguments/environment/control endpoints
are omitted; exception messages can include OS details. MainProcess units do not emit
this process-specific trace. These events are observations, not a durable audit or recovery protocol.
