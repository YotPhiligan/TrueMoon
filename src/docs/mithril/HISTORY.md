# Mithril: история реализации и проверок

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## MIT0 own-child baseline — 2026-10-11

Scope: собственный `ChildProcessUnitHandle`, `RestartPolicy.Never`, Windows x64
10.0.26300, SDK10.0.401/net10.0, xUnit2.9.3/VSTest. Старый discovery0 от2026-10-10
заменён текущей executable suite. App/AddUnit/IPC composition этим шагом не
подтверждается. Проверки других модулей не повторены.

Удалена controller очистка всех одинаково названных процессов: name не является
ownership identity. Failed spawn удаляет partial handle. Process handle проверяет
start cancellation/negative delay/duplicate start, освобождает failed Process,
посылает и flush `qe`, после timeout/cancellation/закрытия pipe завершает только
owned tree и ждёт exit до освобождения. Sync/async disposal останавливает child,
повторное cleanup допустимо; cleanup failure не выдаётся за успех disposal.
Child launch через dotnet сохраняет entry assembly. Public signatures и package
versions/TFM не менялись; другие hosting/restart policies не подтверждены.

[Probe](../../ManualTests/MithrilLifecycleProbe/README.md) добавлен в solution,
test project строит и копирует fixture. Production launch/stop не подменяются:
subclass только добавляет child arguments и stdout/stderr для observation.
Child readiness передаёт PID/unit/parent identity. У probe есть runner timeout25s,
у child — deadline15s; kill применяется к собственному запущенному процессу.
Повторно открытый OS observer handle сохраняется до exit для независимого
наблюдения exit/exit code; после stop поле owned Process проверяется на null.

Фактически выполненные итоговые команды:

```powershell
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --no-restore --logger "trx;LogFileName=mithril-debug.trx" --results-directory TestResults/MithrilBaseline/tests --verbosity quiet
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Release --no-restore --logger "trx;LogFileName=mithril-release.trx" --results-directory TestResults/MithrilBaseline/tests --verbosity quiet
./ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1
dotnet build ManualTests/MithrilTest/MithrilTest.csproj --no-restore --verbosity quiet
```

- Debug tests: exit0, Passed14/Failed0/Skipped0. Release: exit0,
  Passed14/Failed0/Skipped0. По6 guards и8 actual process cases в каждой конфигурации.
  Tests запускают fixture через `dotnet <assembly>`.
- Manual final run: exit0, `Completed=true`,16/16 —8cases через Debug apphost
  и8через published Release apphost, framework-dependent без RID.
  Graceful/dispose/async-dispose/duplicate-start/same-name: ACK `qe`, exit0.
  Natural exit: exit0, один callback; timeout/cancel-stop: exit-1,
  callback0, exit подтверждён. Все16 released handles/repeated stops успешны.
  Same-name sentinel переживает controller start в обеих конфигурациях.
- Итоговые Debug build/Release publish runner — exit0/errors0,
 81existing Mithril XML/nullable warnings в каждом compile log. Прежний MithrilTest
  consumer incremental build exit0/warnings0/errors0; приложение не запускалось,
  IPC passing не заявляется.
- Артефакты ignored `TestResults/MithrilBaseline`: TRX tests, manual summary/logs,
  source SHA256/commands/configuration/PIDs/exit codes. Summary timestamp UTC
  `2026-10-11T15:05:28.1739354Z` для итогового16case run. Stop elapsed см. summary — observation,
  не performance benchmark и не контракт deadline точности.

| Проверяемое поведение | Тест |
| --- | --- |
| Cancellation до launch и после preparation | `Start_PreCancelled_DoesNotCreateLaunchInfoOrProcess`, `Start_CancelledDuringLaunchPreparation_DoesNotStartProcess` |
| Negative delay до process creation | `Start_NegativeTerminationDelay_RejectsBeforeLaunch` (-1/int.MinValue) |
| Original OS failure, released handle, retry | `Start_MissingExecutable_ReleasesFailedProcessAndPreservesError` |
| Dispose до start, repeat и запрет start | `Dispose_BeforeStart_IsRepeatableAndRejectsStart` |
|8own-child lifecycle cases/identity/exit/callback/released handle/sentinel | `OwnedChild_ShutdownScenario_ReapsProcessAndReleasesHandle` с8InlineData |

При inline review assertions проверены на реальные observable изменения:
убрать stop/kill/disposal/identity propagation или вернуть общий success — падают
process cases; убрать cancellation/validation/failure cleanup — падают guards.
Controller name-kill regression использует собственный test sentinel, не чужую
программу. Искусственных тестов для увеличения count/новых пакетов не добавлено.

Неуспешные промежуточные попытки: первая suite6passed/8failed из-за чтения
ExitCode observer без заранее удержанного OS handle; исправлено в probe.
При перемещении hosted launch в child override entry DLL временно добавлялся
дважды: Debug/Release6passed/8failed, часть cases вышла по bounded timeout;
failed logs/TRX сохранены в `attempts`, дублирование удалено. Повторные итоговые
Debug/Release14/14 и новая manual16/16 выполнены после исправления; промежуточные
passing apphost cases не использованы как hosted proof.

Открыто: App/AddUnit/static configurations, MainProcess/External semantics,
parent exit/EOF listener disposal, restart/error/callback policy и concurrency,
controller propagation of false Stop result, IPC, package consumer, другие OS,
stress/resource trend. MIT0 выполнен; MIT1–MIT5 остаются отдельными этапами.

## MIT1/MIT2 App composition — 2026-10-11

Windows x64/net10.0/SDK10.0.401; source ProjectReference consumer, App lifetime,
Never, Cobalt/Microsoft DI. MIT0 results above are historical; the following
checks ran again against MIT1/MIT2 sources.

Changes: app-local registry/snapshots, local MainProcess graph/PID, explicitly
owned launched External, serial transactional controller startup/reverse cleanup,
false/throw/aggregate failure propagation and retained cleanup retry. Concurrent
and reentrant disposal share cleanup; lifetime cancellation is once-only.
Parent listener owns cancellable async pipe/observer tasks and drains them on
Stop/Dispose; actual parent exit and control EOF cancel child lifecycle.

The old Console.In listener could block disposal and loop at EOF. Console input
ReadLineAsync executes synchronously according to the primary
[.NET documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.textreader.readlineasync?view=net-10.0).
The new pipe protocol is documented in [PLAN](PLAN.md#mit1mit2-app-composition-контракт--2026-10-11).
Legacy stdin-only children still receive qe/flush. New manually selected children
without a control endpoint only observe their parent.

Initial test run:47 passed/2 failed on Cobalt unresolved internal controller/local
fixture resolver. Fixed internal registrations with explicit singleton factories
and aliases; consumer/test projects explicitly reference Cobalt.Generator for
consumer services. Mithril runtime dependencies were not expanded to Cobalt.
Next49/49 passed; additional disposal/process cases brought the suite to65.
Early fixture compile errors (missing Resolve namespace and parameterless
App.Builder) were corrected. Final commands below all exited0, no skipped cases.

### Final commands

From `src`, PowerShell:

```powershell
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=mithril-debug.trx' --results-directory TestResults/MithrilComposition/tests --verbosity quiet
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=mithril-release.trx' --results-directory TestResults/MithrilComposition/tests --verbosity quiet
pwsh -NoProfile -File ManualTests/MithrilCompositionProbe/RunCompositionSmoke.ps1
pwsh -NoProfile -File ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1 -ResultsDirectory TestResults/MithrilComposition/legacy-lifecycle
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=enerit-debug.trx' --results-directory TestResults/MithrilComposition/tests --verbosity quiet
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=enerit-release.trx' --results-directory TestResults/MithrilComposition/tests --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

| Check | Current result | Ignored evidence under TestResults/MithrilComposition |
| --- | --- | --- |
| Mithril Debug |65Passed/0Failed/0Skipped, exit0 | `test-debug.log`, `tests/mithril-debug.trx` |
| Mithril Release |65Passed/0Failed/0Skipped, exit0 | `test-release.log`, `tests/mithril-release.trx` |
| App matrix |28/28, Completed=true;7 scenarios ×2 providers ×2 configurations | `manual/summary.json`, runner/build/publish/case logs |
| Legacy handle matrix |16/16, Completed=true;8 scenarios ×2 configurations | `legacy-lifecycle/summary.json`, case logs |
| Enerit runtime Debug/Release |71Passed/0Failed/0Skipped each, exit0 | `enerit-debug.log`, `enerit-release.log`, `tests/enerit-*.trx` |
| Full solution Debug |Exit0/errors0/warnings1220 | `solution-build.log` |

App matrix final UTC:2026-10-11T17:08:03.7567807Z (summary.json).
Runner fingerprints cover Mithril/TrueMoon/Core/Contracts/DI/Cobalt/generator/fixture
sources. Earlier runner draft mistakenly listed Enerit sources; corrected before
this final28-case run. Fresh unique case directories prevent stale handshake.

Actual process observations include PID/unit/parent/provider identity, exit0,
service Stop count1 and empty controller collection. The parent-exit supervisor
kills only its own parent (without tree kill); the child exits through the
production parent observer. On probe failure only known owned processes are
cleaned up. Unit fake tests separately assert original exception identity,
reverse cleanup order, per-handle call counts, continuation after cleanup errors
and retry; no resource-leak percentage is inferred from these checks.

Quality review:18 new test methods expand to51 cases;95 assertion call sites,
including collection helpers, average5.28 per method. No assertion-free, trivial
or self-referential test methods. Helper lifecycle methods are excluded from
this inventory. Equality, boolean, null, exception, type, collection, negative,
state and structural observations are represented. Exception-only guard tests
are deliberate. Static source/assertion review identified shared disposal failure,
reentrancy and controller lifetime repeated notification as distinct outcomes;
focused cases and implementation corrections were added before final runs.
No mutants/coverage tool ran; predicted regression detection is not empirical
mutation survival. Detailed scratch review lives outside stageable sources.

Remaining scope: defaultAlways/Once/OnCrash, restart limit/delay and
stop-versus-restart (MIT3), generated Enerit App composition (MIT4/ENE4), package
consumer (MIT5), additional OS/lifetime/startup modes and stress/resource trend.
Enerit generator/manual and other module/UI suites were not rerun. Earlier
results of those checks retain their own dates and scope.

Итоговая документация:39 Markdown files,414local links/102heading links,0problems. PowerShell runner и XML project manifests разобраны успешно; git diff --check чистый. Final source SHA256 проверен против manual summary без расхождений.

## MIT3 bounded restart — 2026-10-12

Windows x64/net10.0/SDK10.0.401. Source ProjectReference consumer, owned child
handles/controller lifetime boundary; App regression отдельно остаётся Never.

Changes: synchronous Exited-handler restart заменён owned async monitor;
каждая attempt создаёт новый Process/start info/control endpoint. Limit default3
и delay default250ms, оба nonnegative, zero limit disables; initial start не
считается, successful restart не сбрасывает бюджет. Terminal-only callback
предотвращает premature App cancellation. Stop fence отменяет delay/preparation,
очищает owned current process и дожидается monitor. Automatic failure сохраняет
исходное исключение LastRestartError/diagnostics и завершает sequence. Natural
exit/restart diagnostics сохранены; throwing sink не блокирует cleanup.
Protected Restart() migration к RestartAsync(token) записана в PLAN/README.

Первая существующая suite прошла65/65 после runtime изменений. Expanded suite
сначала69passed/20failed: probe возвращал deferred ExitCodes enumeration, но
освобождал Process observers до JSON serialization. Внутренние sequence checks
уже доходили до report; причиной failure была fixture, не policy expectations.
Exit codes materialized до observer disposal; final89/89. Дополнительно default
scenario теперь не задаёт явно policy/limit/delay и проверяет все три defaults.

### Final commands

From `src`, PowerShell; все команды exit0:

```powershell
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=mithril-debug.trx' --results-directory TestResults/MithrilRestart/tests --verbosity quiet
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=mithril-release.trx' --results-directory TestResults/MithrilRestart/tests --verbosity quiet
pwsh -NoProfile -File ManualTests/MithrilRestartProbe/RunRestartSmoke.ps1
pwsh -NoProfile -File ManualTests/MithrilCompositionProbe/RunCompositionSmoke.ps1 -ResultsDirectory TestResults/MithrilRestart/composition
pwsh -NoProfile -File ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1 -ResultsDirectory TestResults/MithrilRestart/legacy-lifecycle
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --no-restore --logger 'trx;LogFileName=enerit-debug.trx' --results-directory TestResults/MithrilRestart/tests --verbosity quiet
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Release --no-restore --logger 'trx;LogFileName=enerit-release.trx' --results-directory TestResults/MithrilRestart/tests --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

| Current check | Result | Ignored evidence under TestResults/MithrilRestart |
| --- | --- | --- |
| Mithril Debug/Release |89Passed/0Failed/0Skipped each | `test-debug.log`, `test-release.log`, `tests/mithril-*.trx` |
| Restart matrix |40/40, Completed=true,20 scenarios×2 configurations | `manual/summary.json`, build/publish/case logs;63source SHA256 |
| App/DI regression |28/28, Completed=true; Never/Cobalt/DI | `composition/summary.json`;130source SHA256 |
| MIT0 regression |16/16, Completed=true | `legacy-lifecycle/summary.json`;6scope SHA256 |
| Enerit runtime Debug/Release |71Passed/0Failed/0Skipped each | `enerit-*.log`, `tests/enerit-*.trx` |
| Full solution Debug |Exit0/errors0/warnings1220 | `solution-build.log` |

Fresh directories and owned observers pin each generation's PID/unit/parent/
endpoint and OS exit0/23. Expected code arrays/budgets/callback counts are literal
oracles in tests, independent of production policy decision. Delay timestamps
come from natural exit hook to next launch preparation;5ms timer tolerance.
Pending60s delay and pending preparation stop have3s deadline. Stopping an
already restarted child observes graceful exit0, monitor completion, Process=null
and repeated/concurrent cleanup. Controller component proves intermediate exit
leaves lifetime untouched and terminal exit cancels once. Reentrant terminal
Dispose cannot wait on its own monitor. Missing executable consumes one attempt
and exposes Win32Exception; no automatic failure retry loop.

Quality review applies assertion-quality and test-gap-analysis:2new methods
expand to24cases;24assertion API call sites including collection helpers.
No assertion-free/trivial/tautological methods; exact code arrays, process identity,
counts/negative observations, exception type/parameter and state/structural
checks. Report flags credited only with prior fixture checks. Static expected
regression detection recorded outside stageable sources; no mutants or coverage
percentages collected. Remaining unproven scope: external restart executable,
App+Enerit+restart composition, other OS, package consumer, arbitrary extension
hook reentrancy/throwing callbacks and long stress/resource trends.

Historical Enerit generator/manual, other module suites and UI/native checks
were not promoted to current results. Next functional work MIT4/ENE4.

Final restart matrix UTC:2026-10-12T12:55:07.4611964Z;63 fingerprints verified unchanged,0mismatches. Documentation:40 Markdown files/424local links/106anchors,0problems. PowerShell/XML parse and git diff --check passed.
