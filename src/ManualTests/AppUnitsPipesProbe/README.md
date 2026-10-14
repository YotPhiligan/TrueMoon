# App / AddUnit / generated pipes probe

MIT4/ENE4 source consumer: parent App obtains an Enerit generated proxy from its
services; an App/AddUnit owned child obtains the generated handler and pipes server.
Both Cobalt and Microsoft DI use the same consumer declarations. The accepted
[contract](../../docs/mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14)
and [dated results](../../docs/mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14)
are maintained by Mithril; runtime/generator changes belong to Enerit.

From `src`, with PowerShell 7 and the SDK selected by global.json:

```powershell
pwsh -NoProfile -File ManualTests/AppUnitsPipesProbe/RunAppUnitsSmoke.ps1
```

The runner builds Debug and publishes framework-dependent Release without RID,
then launches 9 scenarios × 2 providers × 2 configurations = 36 owned probes.
The recorded configuration is Windows x64/net10.0, SDK10.0.402. Outputs, commands,
source SHA256 and fresh case directories are under ignored
`TestResults/AppUnitsPipes/manual`; `Completed=true` requires all 36 cases.
ResultsDirectory must remain inside the source workspace. Per-case deadline40s;
build/publish60s; failure cleanup kills/reaps only the launched owned process tree.

| Scenario | Observation |
| --- | --- |
| payload | DTO transform, Unicode, null, PID/generation and Task acknowledgement |
| concurrent | 32 correlated calls, exact transformed values and child identity |
| cancel-request | Local cancellation, late response drain, subsequent call succeeds |
| remote-error | InvocationException retains remote cause; subsequent acknowledgement/call works |
| restart | Kill owned child only; old pending call fails without retry, one new PID/control endpoint, same proxy/client calls new server |
| budget | One OnCrash restart; second crash exhausts limit and cancels controlling App; no third generation |
| stop-pending | App.Stop cancels cooperative handler, faults pending caller, gracefully stops child |
| startup-failure | Spawn delayed first unit, resolve/call client, then missing second executable; rollback cleans first child and client workers before App.Dispose |
| parent-exit | Supervisor kills parent only during pending RPC; production parent monitor stops child/server and local service exactly once |

The parent chooses a unique pipe endpoint and passes it to the child through its
inherited environment. Both call `UseDiagnostics()` and
`PipesServiceTransport(_ => endpoint)`; endpoint excludes the transport's `tm_`
prefix. Default transport overload retains the service full name. Invalid selected
names are rejected. Endpoint naming does not establish authentication or wire
compatibility.

Parent configuration uses `UseInvocationService<IProbeService>()`. The unit
explicitly registers its implementation before listening:

```csharp
unit.Services(r => r.Singleton<UnitState>()
    .Composite<ProbeService, IProbeService, IStartable, IStoppable>())
    .ListenInvocationService<IProbeService, ProbeService>();
```

Explicit consumer registration lets the Cobalt generator see local constructors.
The project references both Cobalt and Enerit generators as analyzers. Generated
Enerit adapters and internal transport factories use explicit registration factories,
so they do not depend on a sibling generator discovering generated classes. Static
storage contains compiled registration metadata; instances remain App-local.

Normal parent cleanup checks pending count0, completed client workers and no
controller handles after App.Stop, before App.Dispose. Cooperative child checks
server/listener/worker cleanup before its App.Dispose; successful child exit0 and
its service stop record are independent observations. In `parent-exit`, the parent
was killed: its client workers/pending map cannot be observed. The result reports
ClientCleanupObserved=false, WorkersCompleted=false and Pending=-1 for that case.

This is a same-source/same-generator ProjectReference proof, not independent NuGet
or cross-version delivery. Local request cancellation does not send a cancel frame;
failed calls are not replayed. Uncooperative handlers, backpressure/resource trends,
other OS, MMF, engine frame transport and hot reload remain outside this scope.
