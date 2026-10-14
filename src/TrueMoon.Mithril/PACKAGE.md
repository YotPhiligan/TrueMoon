# TrueMoon.Mithril

Alpha .NET10 App-local units and owned process lifecycle. Configure through
TrueMoon App/AddUnit. The package depends on Contracts/Core and does not depend
on Enerit or UI. Add Enerit and its generator explicitly when typed IPC is needed.
For default Cobalt, reference TrueMoon.Cobalt.Generator explicitly; Microsoft DI
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
