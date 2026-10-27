# Mithril: история реализации и проверок

## Сверка рабочего дерева и архив STATUS — 2026-10-28

Сверены bounded retry обоих probes и12cases ReadyFileProcessTests; ready-file
quality follow-up остаётся закрытым в проверенном Windows fixture scope. Полный
MIT6 не закрыт, runtime/API не менялись. Текущий общий следующий шаг исправлен
с legacy Build/Replace fixtures на ARCH2e.1/2 independent NuGet; legacy fixtures
уже исправлены с отдельным Aether proof. Прочитаны full TRX от27октября:
Mithril111/111 Debug/Release, failed/skipped0. Новых запусков нет. Старые сводки,
failures и тогдашние следующие шаги сохранены ниже как датированный архив.

### Снимок прежней сводки

Ниже сохранено содержимое STATUS до сверки. Указания «следующий шаг»,
«текущий» и открытые failures относятся к датам соответствующих записей;
актуальная очередь и закрытия находятся в STATUS и PLAN. Это архив,
не новые результаты запуска. Относительные ссылки сохранены.

Последняя работа — **2026-10-27, bounded ready-file read retry в обоих probes**.
Публикация temp+Move сохранена; reader повторяет missing-file и sharing/lock errors
в едином8s deadline с token у IO/delay. Malformed/null JSON не повторяется.
Directed process proof подтвердил RED2 → GREEN;12новых cases проверяют locked
partial publication, missing file, раннюю отмену, исходный deadline и invalid JSON.
Полный Mithril **111/111 Debug и111/111 Release**, failed/skipped0; fresh published
Release **composition14/14 на Cobalt/DI иrestart20/20**, ownership/identity/reap checks
сохранены. Solution nonincremental Debug build0errors/1541warnings.
[Причина, команды и evidence](HISTORY.md#ready-json-bounded-read-retry--2026-10-27).
Ready-file follow-up закрыт в этом scope. Runtime/API не менялись; full solution/
Aether/Editor tests, NuGet/GPU/performance и полный MIT6 acceptance не повторены.

Предыдущая связанная проверка — **2026-10-27, ARCH2a full run**.
Mithril99/99 Debug,98/99 Release, skipped0. Composition/cobalt probe получил
sharing IOException при ReadAllTextAsync ready.json в Fixture.ReadyAsync line172.
Source/fixture не менялись, исправления и повторного green нет.
[Подробности](HISTORY.md#composition-ready-json-sharing-failure--2026-10-27).
Текущий quality шаг охватывает безопасную ready publication/read в composition
и restart probes с deterministic overlap и прежними deadline/identity/reap checks.

Предыдущая проверка — **2026-10-26, общий Core diagnostics run**.
Initial Debug98/99: concurrent-stop получил sharing IOException на ready-1.json
в MithrilRestartProbe.Fixture.ReadyAsync. После отдельной правки Core test
isolation full Debug99/99 и Release99/99 прошли; Mithril production/fixture
не менялись, passing повтор не закрывает race. [Подробности](HISTORY.md#ready-file-race-повторное-наблюдение--2026-10-26).
Текущий quality шаг — atomic ready publication либо bounded read retry с
deterministic overlap proof и прежними deadline/identity/reap assertions.

Последняя работа — **2026-10-25, уточнение MIT6c diagnostics roadmap**.
[MIT6c](PLAN.md#mit6c-диагностика-и-захват-process-output--уточнение-2026-10-25) связывает
process acceptance с Core CORE6 и Aether ENG8a capture: continuous stdout/stderr
drain, bounded retention, overflow/EOF/failure outcomes и reader ownership при stop.
Реализация захвата остаётся задачей владельца Aether; Mithril API не расширен.
Изменены только планы/статусы; process/build/tests/packages не запускались. Следующий
quality шаг ready-file fixture и открытые long trends/parent-death MIT6c сохранены.

Предыдущая проверка — **2026-10-23, ED4a integration**. Runtime/API не менялись.
Последний Mithril Debug98/99: default-budget-delay restart probe получил IOException
на ready-4.json в Fixture.ReadyAsync; предыдущая existence проверка не гарантирует
окончание публикации. Последний Release99/99. Общий solution Debug3048/3049,
Release3049/3049; предыдущие99/99 не означают исправление fixture race.
Следующий quality шаг — безопасная публикация/чтение ready JSON с deterministic
overlap proof, сохраняя bounded deadline, identity и reap assertions.
[Evidence/границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).
Это отдельная проверочная задача; MIT6a/c и authoring порядок не заменяются.
Предыдущая реализация — **2026-10-23, первый MIT6a load-stop профиль**.
AppUnitsPipesProbe расширен saturated-stop/saturated-kill: pending2/queued1,
overload до dispatch, независимый Mithril control при active2 domain handlers,
cooperative drain либо bounded owned kill/reap, exact PID/diagnostic outcome и
zero client counters/Completion. Production changes находятся в Enerit;
Mithril runtime/API, restart policies и ownership modes не менялись.
[Контракт и границы](PLAN.md#mit6a-первый-load-stop-профиль--2026-10-23).
В предыдущем MIT6a проходе Mithril99/99 Debug/Release в full solution2361/configuration, failed0/
skipped0. Новый source/published профиль **44/44**, fresh package **132/132** на
Cobalt/DI/DI без Cobalt analyzer,8nupkgs; Completed=true. [Команды/evidence](HISTORY.md#mit6a-saturated-ipc-и-независимый-process-stop--2026-10-23).
MIT6a fault matrix в целом, parent-death containment и long trends MIT6c остаются
открытыми; локальный authoring не блокируется. Native Editor viewport не повторялся.

Текущая реализация — **2026-10-22, фабрики и единственный owner**:
UnitsController injects IFactory<IUnitHandle,IUnitConfiguration>; штатный
UnitHandleFactory выбирает hosting mode. Controller/parent создаются через App
factories; borrowed IUnitsController и non-disposable lifecycle adapters исключают
duplicate ownership. Public consumers должны обращаться к concrete registration
для диагностической inspection, а не приводить alias к implementation.
Mithril99/99 Debug/Release (+4cases) и реальные composition/process/pipe tests
прошли на обоих providers. Process budgets/policies прежние. Own standalone restart
matrix отдельно не повторялась. [Evidence](../engine/HISTORY.md#единые-фабрики-и-di--2026-10-22).

Последняя работа — **2026-10-21, graceful wait при ошибке доставки exit**.
В StopAsync исправлена гонка stdin/control pipe: ошибка доставки больше не
пропускает оставшийся graceful budget; timeout/cancellation/owned kill/reap
сохраняются. Новая delayed EOF regression подтверждена red/green; native20cases
Debug/published Release прошли. Mithril95/95 и full solution1907/1907 отдельно
Debug/Release, failed0/skipped0. Fresh units package matrix108/108cases прошла:
8nupkgs/128commands/206source fingerprints, Cobalt/DI, Debug/Release build/publish,
expected missing Enerit analyzerCS0234. Own standalone restart matrix не повторена.
[Причина, точные команды и ограничения](HISTORY.md#graceful-wait-после-ошибки-доставки-exit--2026-10-21).

Исправление структуры — **2026-10-21**: RootNamespace тестов исправлен на
TrueMoon.Mithril.Tests; TestPoco.cs размещён в корне MithrilTest, namespaces
manual/integration consumers согласованы. Module-specific UnitsConfigurationExtensions
находится в Mithril вместо Contracts. Mithril94/94 Debug/Release и свежая package
матрица108cases прошли; native children из затронутых editor scenarios освобождены.
[Миграция и точные команды](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

App entry-point audit — **2026-10-19**: AddUnit связывает App-local unit definitions,
configuration selection и UnitsController/parent-monitor lifecycle. Focused Debug
UnitCompositionTests:9passed/failed0/skipped0 на Cobalt/DI. Runtime code не менялся;
own full/Release/process/restart/package/performance matrix не повторялись.
[Общая таблица API и команды](../HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).

Package metadata check — **2026-10-19**: тематические tags и общие project/
repository metadata; Release pack/nuspec/README/license/icon payloads проверены.
Runtime code, own unit/process/restart/performance checks в этом metadata шаге
не менялись/не повторялись. [Общие команды/evidence](../HISTORY.md#nuget-package-metadata--2026-10-19).

Integration check2026-10-18 в UI INF3: первый full Debug run дал readiness-file
sharing IOException в Restart_OwnedSequence (always-crash); targeted20/20 и
повторный full Debug Mithril94/94 прошли. Код Mithril/probe не менялся, race не
объявлена исправленной. [Failure и точные runs](HISTORY.md#readiness-file-race-при-ui-inf3--2026-10-18).

Последняя работа — **2026-10-15, MIT5 operational diagnostics и свежая package regression**.
Добавлены UnitProcessDiagnostic/UnitProcessStage через существующий IEventsSource:
run/generation/PID, effective policy/budget/delays, launch/exit/restart/terminal,
kill/release/stop и исходные failures. Ownership/restart limits не расширены.
[Подключение и semantics](DIAGNOSTICS.md), [контракт](PLAN.md#mit5-operational-diagnostics--2026-10-15),
[команды/артефакты](HISTORY.md#mit5-operational-diagnostics--2026-10-15).

## Ready JSON: bounded read retry — 2026-10-27

Закрыт ready-file quality follow-up в MithrilCompositionProbe и MithrilRestartProbe.
Оба writer уже использовали temp-file + Move; публикация не менялась. Reader после
File.Exists выполнял один ReadAllTextAsync без cancellation token и мог получить
sharing IOException даже после появления final path.

ReadyAsync теперь имеет один linked deadline8s на весь цикл: чтение отсутствующего
файла повторяется, IOException с Win32 sharing/lock codes32/33 повторяется через10ms.
Token передаётся в IO и delay; budget не пересоздаётся на каждой попытке.
Другие IO errors и malformed JSON не маскируются; JSON null явно отвергается как
InvalidDataException до использования identity. PID/unit/parent/provider/endpoint
проверки, restart/stop budgets и owned process observers/reap assertions сохранены.
Mithril runtime, публичные contracts/API, App/DI/lifetime/restart policy не менялись.

Добавлен directed CLI --read-ready для обоих fixture executables: callback из
реального retry path пишет единственный RETRY; необязательная ранняя отмена задаётся
миллисекундами,0оставляет исходный8s deadline. Для restart mode читается generation1.
Это test-only процессный путь без создания App/units; обычные scenarios прежние.

Новые ReadyFileProcessTests выполняются против настоящих fixture processes:

- ReadyRead_SharingViolationWaitsForCompleteJsonAndPreservesIdentity: отдельный
  writer держит FileShare.None с половиной JSON до получения RETRY. Затем дописывает
  и закрывает файл; reader получает полный PID/parent/unit/provider/endpoint.
- ReadyRead_MissingPublicationWaitsThenReadsPublishedIdentity: после подтверждённой
  missing-file попытки публикуется готовый JSON через temp + Move.
- ReadyRead_SharingRetriesHonorCallerCancellation: writer остаётся открытым,
  caller cancellation600ms завершает retries, а не возвращает identity.
- ReadyRead_SharingRetriesDoNotExtendEightSecondDeadline: без внешней отмены writer
  остаётся открытым весь исходный8s budget; reader завершается cancellation, retries
  не продлевают deadline. Assert range7–12s после первого retry и bounded process reap.
- ReadyRead_InvalidPublishedJsonFailsWithoutRetry: malformed `{` и `null` завершаются
  соответствующей ошибкой без RETRY/RESULT.

Всего12cases: каждый сценарий проверяет оба probes, malformed/null дают4cases.
Sharing regression на исходном алгоритме чтения с directed entry получил RED в
обоих probes с точной IOException, затем GREEN без ослабления assertions.

Windows x64/SDK10.0.403/net10.0/xUnit2.9.3/VSTest.
[Exact commands, TRX names, source/binary SHA и7binlogs](../../TestResults/MithrilReady-20261027-1/verification.json).

| Проверка | Результат |
| --- | --- |
| Новый sharing regression, Release до fix | Passed0/Failed2/Skipped0, expected RED; [TRX](../../TestResults/MithrilReady-20261027-1/red/red.trx) |
| Новые ready-read cases, Release после fix | Passed12/Failed0/Skipped0; [TRX](../../TestResults/MithrilReady-20261027-1/green-Release/green.trx) |
| Полный Mithril Debug | Passed111/Failed0/Skipped0; [TRX](../../TestResults/MithrilReady-20261027-1/full-Debug/mithril.trx) |
| Полный Mithril Release | Passed111/Failed0/Skipped0; [TRX](../../TestResults/MithrilReady-20261027-1/full-Release/mithril.trx) |
| Fresh published Release composition/restart |14/14 composition на Cobalt/DI и20/20restart; [summary](../../TestResults/MithrilReady-20261027-1/published-summary.json), Completed=true/source changes0 |
| Solution nonincremental Debug build | Exit0/errors0/warnings1541; [log](../../TestResults/MithrilReady-20261027-1/solution-Debug.log) |

Full test command: dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj
-c Debug/Release --no-build, с отдельными TRX/results-directory/binlog flags в
evidence. [Published runner](../../TestResults/MithrilReady-20261027-1/VerifyPublished.ps1)
создаёт fresh framework-dependent deployments без RID и запускает apphosts.
Reports сверяют owned PID, effective identity, exit outcomes, monitor completion,
generation/restart diagnostics и cleanup; runtime assertions не менялись.

Результат закрывает указанный Windows fixture read race. Полные solution/Aether/
Editor tests, независимые NuGet consumers, GPU/physical/performance и полный
MIT6 fault/containment/trends acceptance не запускались. Исторические full failures
сохранены; новый passing Mithril не превращает их в общий solution green.
Следующий общий quality шаг — legacy Aether Build/Replace fixtures, затем ARCH2d/e.

## Composition ready JSON: sharing failure — 2026-10-27

Связанный ARCH2a full run: Mithril99/99 Debug,98/99 Release, skipped0.
Release CompositionProcessTests.AppUnits_OwnedProcessesAndLocalHosting_CleanupOnBothProviders
(scenario=composition,provider=cobalt) получил Exit1/sharing IOException на
composition-results/12a981853f2141b1ae916eaf3ec6bdc8/ready.json.
Stack: MithrilCompositionProbe.Fixture.ReadyAsync line172 → RunAsync line100/132 →
Program line38; test CompositionProcessTests line27/43. Source/fixture Mithril этим
срезом не менялись. Это наблюдение в composition probe дополнительно к прежним
restart ready-file failures; отдельный фикс не выполнен, повтор для green не делался.

Full Debug4296/4296, Release4295/4296; affected Aether1347/Editor743 прошли обе
конфигурации. [Команды/границы](../engine/HISTORY.md#arch2a-scene-persistence-в-aether--2026-10-27),
[TRX/log/binlog и failure](../../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json).
Следующий quality шаг — безопасная публикация/чтение ready JSON в composition
и restart fixtures с deterministic overlap proof, сохраняя deadline/identity/reap
assertions. Production fix/общий green, full fault matrix и containment не заявлены.

## Ready-file race: повторное наблюдение — 2026-10-26

В initial full Debug Core diagnostics run Mithril98/99: scenario concurrent-stop,
Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop, ready-1.json sharing
IOException в MithrilRestartProbe.Fixture.ReadyAsync line181 после File.Exists.
Mithril production/fixture не менялись. Последующие full Debug99/99 и Release99/99
не являются исправлением этого race; follow-up из плана сохраняется.
[Initial/final TRX и error](../../TestResults/DiagnosticsCore-20261026-1/verification.json),
[общие команды/scope](../core/HISTORY.md#emergency-и-managed-jsonl-runs--2026-10-26).

## ED4a: компактный заголовок и меню — 2026-10-23

Наблюдение общего UI integration run: в solution-183128349 full Debug Mithril98/99,
RestartProcessTests.Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop,
default-budget-delay, ready-4.json. Fixture.ReadyAsync проверяет File.Exists,
затем File.ReadAllTextAsync; чтение попало на sharing lock публикации и получило
IOException. Это наблюдение fixture, изменение runtime/API не выполнялось.
Последний Release99/99; прежние passing full не отменяют Debug failure.
Evidence: TestResults/MenuHeader-20261023/solution-183128349/test-Debug.log,
aggregate verification.json; follow-up — безопасная публикация/чтение ready JSON
с deterministic overlap proof и прежними identity/deadline/reap checks.

## MIT6a: saturated IPC и независимый process stop — 2026-10-23

По запросу приступить к следующему шагу реализован
[первый load-stop профиль](PLAN.md#mit6a-первый-load-stop-профиль--2026-10-23)
в существующих AppUnitsPipesProbe и process tests. Limits/async stop реализованы
в Enerit; Mithril runtime, public API, readiness/restart policies не менялись.

Сценарии saturated-stop/saturated-kill сначала занимают pending2 и bounded queue1,
подтверждают excess overload до handler и выполняют unit stop через независимый
control pipe, пока active handlers2. Cooperative child подтверждает handler/server
drain и exit0; uncooperative child — kill/reap, exact PID/nonzero exit, no restart
и client/monitor completion. Kill не объявляется исполнением managed finally.
Прежний App.Stop-first сценарий остаётся регрессией, но не используется как
доказательство control delivery под нагрузкой: он мог сначала отменить RPC.

Manual runner расширен36→44 Debug apphost/published Release scenarios;
independent packages108→132 cases (11scenarios×2config×2delivery×3provider).
Новые validators сохраняют exact PID/control endpoint, active2-at-control,
graceful3s/reap5s budgets,10s combined oracle, pending/queue0, outcomes и
нулевой restart. EndOfStreamException принимается как IOException subtype;
неожиданные timeout/errors не скрываются. Final commands/evidence размещаются в
TestResults/EneritAdmission-20261023/final3. Итог: full solution2361/2361 Debug/
Release, Mithril99/configuration, failed0/skipped0; source/published44/44 и fresh
package132/132,8nupkgs/152commands/213source fingerprints, оба Completed=true.
Final consumer paths: TestResults/AppUnitsPipes/ene6-20261023-093321145 и
TestResults/UnitsPackages/ene6-20261023-093321145; omitted-analyzer CS0234 ожидаем.

Полный standalone restart matrix, OS permission/reap failure injection,
parent-death containment, other OS и long resource trends этим не закрываются.
Общая transport ownership/review история — [Enerit](../enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23).

## MIT6, ревью process и IPC composition — 2026-10-23

Сверены plan/status, ProcessUnitHandle и parent observer, Aether native lease/
supervisor. Existing restart/stop fence/owned kill-reap сохраняются; OS start,
IPC connect и Aether Ready различаются. Предложены MIT6a–d: общий Enerit fault/
load shutdown contract, status по реальному consumer, resource/parent-death/log
retention acceptance и отложенные restart возможности. Подробные tasks ведутся
в [плане](PLAN.md#mit6-укрепление-процессов-и-ipc--ревью-2026-10-23),
границы очередности — в [общей записи](../HISTORY.md#сверка-основы-и-улучшения-ipcпроцессов--2026-10-23).
Код/API не менялись, build/tests/native/packages не запускались. Новые guarantees
или failures uncooperative shutdown этим чтением не подтверждены.

## Graceful wait после ошибки доставки exit — 2026-10-21

ENG4e CPU/GPU measurements выявили преждевременный kill первого GPU child при
Replace: child принял stdin exit, закрыл control pipe и продолжал cleanup/flush.
В ProcessUnitHandle.StopAsync доставка через оба канала и WaitForExitAsync были
в одном try. IOException/InvalidOperationException доставки пропускал оставшееся
graceful ожидание; Killing мог происходить уже за~0.5s при прежнем3s deadline.
Это гонка shutdown, не доказательство driver hang. При первом harness варианте
терялся и raw capture. Measurement worker теперь сохраняется до App teardown.

Минимальное исправление: best-effort delivery отделена от ожидания exit.
После ошибки канала ожидать оставшийся тот же linked deadline; timeout/cancellation
по-прежнему приводит к owned Kill/reap. Budgets, restart/ownership и public API не
расширены. [Уточнение MIT0](PLAN.md#mit0-own-child-контракт--2026-10-11).

Регрессия использует существующий MithrilLifecycleProbe: parent закрывает stdin,
child получает EOF и завершает1s cleanup с exit0. Старый код немедленно убивал
child, новый сохраняет graceful exit без Killing/неожиданного callback.

| Требование | Evidence |
| --- | --- |
| Ошибка delivery не сокращает оставшийся budget | `ProcessUnitHandleTests.OwnedChild_ShutdownScenario_ReapsProcessAndReleasesHandle("closed-stdin", false)`: red9passed/1failed, green10passed/0failed; новые assertions используют существующие exit0/Graceful/no Killing/handle release checks |
| Timeout/pre-cancelled Stop продолжают kill/reap, остальные scenarios сохранены | Та же focused theory10cases; native20cases —10Debug apphost +10published Release |
| Связанные App/IPC/engine contracts | Full solution Debug1907/1907 и Release1907/1907: Mithril95/Enerit126/Aether501, failed0/skipped0; прочие counts в engine HISTORY |

```powershell
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --filter FullyQualifiedName~OwnedChild_ShutdownScenario_ReapsProcessAndReleasesHandle --logger trx --results-directory TestResults/ViewportBaseline/development/mithril-green --verbosity quiet
./ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1 -ResultsDirectory TestResults/ViewportBaseline/mithril-verified-20261021
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Release --verbosity quiet
```

Raw red/green TRX/logs: `TestResults/ViewportBaseline/development/mithril-red` и
`mithril-green`; native `TestResults/ViewportBaseline/mithril-verified-20261021`.
Первый native wrapper выполнил20cases, но сохранил Completed=false из-за прежнего
hardcoded expected18; runner считает2×scenario count, fresh repeat Completed=true.
Свежий independent units consumer после fix:108/108cases,8packages/9projects,
128commands/206source fingerprints, Cobalt/DI/DI без Cobalt analyzer,
Debug/Release build/publish; omitted Enerit analyzer даёт expectedCS0234.
Version`0.2.2.7-alpha.units.20261021094727716`, evidence
`TestResults/UnitsPackages/eng4e-20261021/summary.json`, Completed=true.
Это прежний App/IPC/restart package scope, не новый package closed-stdin scenario.
Own standalone restart matrix/external process permissions/other OS отдельно
не повторены. Полная ENG4e measurement matrix
имеет собственный scope/results в [engine HISTORY](../engine/HISTORY.md).

## Readiness-file race при UI INF3 — 2026-10-18

Первый full Debug solution в UI INF3:1450total/1449passed/1failed, Mithril93/94.
`RestartProcessTests.Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop`
(`always-crash`, exitCodes23/23/23, restarts2, callbacks1) получил child Exit1:
IOException sharing violation при `ReadyAsync` чтении `ready-2.json`,
ManualTests/MithrilRestartProbe/Program.cs:178. Helper ждёт File.Exists, затем
однократно ReadAllTextAsync; наличие файла не гарантирует завершение writer.
Последнее объяснение — вывод по исходнику/stack, без нового runtime fix.

Точечная перепроверка того же метода без rebuild:20/20 Debug, failed/skipped0:

```powershell
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --no-build --filter 'FullyQualifiedName~Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop' --logger trx --results-directory TestResults/AlloyInfrastructure/INF3/mithril-targeted --verbosity quiet
```

Следующий full Debug solution прошёл1450/1450, включая Mithril94/94. Первый
failure сохранён в `TestResults/AlloyInfrastructure/INF3/final-checks/solution-Debug`,
успешный повтор — `final-checks-reviewed/solution-Debug`; подробные UI/native
[commands/evidence](../alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).
Исходники Mithril/RestartProbe и production policy/ownership не менялись;
Mithril Release и own native/package matrix в этом UI шаге не повторялись.
Passing retry не означает устранение readiness publication/read race. Отдельное
исследование helper reliability остаётся перед будущим расширением acceptance.

## Package regression с bundled Cobalt — 2026-10-14

UnitsPackageConsumer обновлён под единый Cobalt package:8nupkgs из9projects,
bundled Cobalt analyzer/explicit Enerit analyzer. Fresh108/108Debug/Release
build/publish cases прошли, Completed=true; existing process/cleanup contract
сохранён. Mithril runtime/tests не менялись, suite89 не повторена. [Команды/границы](../cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).

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

## MIT4/ENE4 App и generated pipes — 2026-10-14

Требование пользователя: «продолжи выполнение работы». Продолжен ранее выбранный
MIT4/ENE4, будущий engine/editor план сохранён. Windows x64/build26300, net10.0,
SDK10.0.402, xUnit2.9.3/VSTest. [Контракт](PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14),
[runner](../../ManualTests/AppUnitsPipesProbe/README.md).

### Изменения и найденные проблемы

Первый настоящий App/Cobalt consumer не мог разрешить внутреннюю pipes factory;
после explicit transport factories — generated proxy, невидимый sibling Cobalt
analyzer. Enerit generator теперь сохраняет typed registration actions; storage
содержит compiled metadata, instances App-local. Внутренние factories/resolver
регистрируются явно. Consumer подключает UseDiagnostics и объявляет local
implementation Composite до Listen. Mithril runtime не менялся.

App-owned client/server получили один Start/Stop lifetime wrapper. Startup-failure
probe сначала обнаружил живые client workers после rollback: IStoppable-only
wrapper не попадал в Core started set. Он теперь также IStartable, и regression
проверяет cleanup до App.Dispose. Listen не пропускает server при существующем
local service; Use/Listen повторяемы. Scoped pipe endpoint выбирается callback;
выбранное пустое имя не заменяется default.

Промежуточные ошибки fixture исправлены: remote error имеет InvocationException,
а не IOException; diagnostics factory требует явного UseDiagnostics. Новый
generator assertion сначала не компилировался из-за неверного имени result;
исправлен на generatorResult, финальные Debug/Release runs чистые.

Core regression сначала дала80passed/1failed: diagnostics build-failure test
поймал IAppLifetime.Cancel из parallel App, поскольку default filters включают
TrueMoon. Assertion теперь наблюдает уникальное событие собственного probe через
ConcurrentBag; IsEnabled=false/исходная причина/пустой журнал probe сохранены.
Runtime Core не менялся. Финальные Core suites81/81 в обеих configurations.

### Итоговые команды и evidence

Команды ниже выполнялись из src. Каждый project запускался отдельно с проверкой
exit code; полный solution build выполнялся после окончательных production правок.

```powershell
pwsh -NoProfile -File ManualTests/AppUnitsPipesProbe/RunAppUnitsSmoke.ps1 -ResultsDirectory TestResults/AppUnitsPipes/final-manual
pwsh -NoProfile -File ManualTests/EneritPipesProbe/RunPipesSmoke.ps1 -ResultsDirectory TestResults/AppUnitsPipes/isolated

$projects = @(
    @('TrueMoon.Enerit.Tests', 'enerit'),
    @('TrueMoon.Enerit.Generator.Tests', 'generator'),
    @('TrueMoon.Mithril.Tests', 'mithril'),
    @('TrueMoon.Tests', 'core'),
    @('TrueMoon.Cobalt.Tests', 'cobalt'),
    @('TrueMoon.Cobalt.Generator.Tests', 'cobalt-generator')
)
foreach ($project in $projects) {
    foreach ($configuration in @('Debug', 'Release')) {
        $name = $project[1] + '-' + $configuration.ToLowerInvariant()
        dotnet test (Join-Path $project[0] ($project[0] + '.csproj')) --configuration $configuration --no-restore --logger "trx;LogFileName=$name.trx" --results-directory TestResults/AppUnitsPipes/tests --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Failed $name" }
    }
}
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

| Проверка | Финальный результат |
| --- | --- |
| Enerit | Debug102/102 и Release102/102;31 новых cases:13registration +18App/process, прежние71 сохранены |
| Enerit generator | Debug34/34 и Release34/34;5existing inheritance cases дополнены initializer factory assertions и compile/emit |
| Mithril | Debug89/89 и Release89/89; отдельная regression, process runtime без изменений |
| Core | Debug81/81 и Release81/81 после исправления test isolation |
| Cobalt | Debug37/37 и Release37/37 |
| Cobalt generator | Debug6/6 и Release6/6 |
| App composition matrix | 36/36, Completed=true;38commands exit0, Cobalt/DI ×Debug/published Release ×9scenarios |
| Isolated pipes matrix | 12/12, Completed=true; прежний same-build probe повторён отдельно |
| Full solution | Non-incremental Debug exit0/errors0/warnings1226 |

Все12final TRX имеют Failed0/NotExecuted0, total698. Ignored artifacts:
`TestResults/AppUnitsPipes/tests/*.trx`, `generator-*.log`, `mithril-*.log`,
`core-*.log`, `cobalt-*.log`, `cobalt-generator-*.log`, `enerit-*.log`,
`solution-debug.log`; process summary/logs в `final-manual` и `isolated`.
SHA256 каждого файла final-manual SourceSha256 совпал с текущим исходником,
mismatches0. Каждый case имеет новый каталог/GUID endpoint, поэтому старые markers
не используются как readiness. Copy fixture target доставляет executable/dependencies
в существующий Enerit test project; новый project зарегистрирован в TrueMoon.slnx.

### Requirement / observable checks

| Requirement | Тест и наблюдение |
| --- | --- |
| «продолжи выполнение работы» | AppUnits_TypedPipes_RestartCancellationFailureAndShutdown:18cases, PID/exit codes/exact counts, same-proxy restart, rollback, child parent-exit cleanup |
| «продолжи выполнение работы» | StartupFailure_RollsBackPreviouslyResolvedClientBeforeAppDispose:2providers, original exception identity и release1 до provider disposal |
| «продолжи выполнение работы» | ProxyFactories_AreAppLocalIdempotentAndShareStartStopLifetime и ExistingLocalService_IsPreservedAndListenerStartsAndStopsOnce: per-App identity, single factories/aliases, borrowed local preservation |
| «продолжи выполнение работы» | PipeFactories_ResolveDefaultOrScopedEndpointsOnBothProviders и ExplicitEndpoint_InvalidValueIsRejectedInsteadOfDefaultFallback:4real connect/stop +3invalid endpoint cases |
| «продолжи выполнение работы» | GeneratedClientAndHandler_ImplementTheCompleteServiceInterface:typed factories + complete compiled/emit mapping; Debug/Release generator34/34 |

Assertion-quality и static behavioral gap review сохранены в ignored
`.git/testagent/enerit-mithril/status.md`; coverage/empirical mutation не запускались.

### Границы вывода

MIT4/ENE4 выполнены только в согласованном same-build cooperative own-child pipes
scope. Pending failed call не replayed; intermediate exit не отменяет controlling
App, terminal exhaustion отменяет. Parent normal Stop/failed Start проверяют workers
до App.Dispose. Parent abrupt exit убивает только известный parent; child выполняет
свой Stop и server cleanup, но client workers убитого parent не наблюдаем:
ClientCleanupObserved=false/WorkersCompleted=false/Pending=-1.

UI tests/native, independent NuGet/analyzer delivery, other OS, MMF, long stress/
resource trend, arbitrary custom transports/callbacks и uncooperative handlers не
проверены. Исторические MIT3 manual40/App28/legacy16 не считаются текущим запуском.
Следующий общий функциональный приоритет — response validation/partial nested-buffer
cleanup Enerit, затем MIT5/ENE5 package consumer. Коммит/пуш этим шагом не выполнялся.

Документация после обновления проверена2026-10-14:45Markdown documents,488local links,123heading links,0problems. Default git diff --check passed; новые untracked source/doc files проверены отдельно. Документальный checker в ignored TestResults/AppUnitsPipes/VerifyDocs.ps1, отчет links.json.

## Regression Enerit decode ownership — 2026-10-14

После [Enerit hardening](../enerit/HISTORY.md#strict-response-и-decode-ownership--2026-10-14)
Mithril89/89 Debug и89/89 Release прошли, Failed0/Skipped0. AppUnitsPipesProbe
36/36 Cobalt/DI Debug/published Release и isolated pipes12/12 Completed=true;
source fingerprints current. Mithril runtime/contract не менялись. Final solution
Debug non-incremental exit0/errors0/warnings1227. Scope same-build cooperative child,
killed parent client cleanup не наблюдаем; UI/Core/Cobalt/packages не повторены.
Точные команды/артефакты у Enerit; следующий MIT5/ENE5 independent consumer.

## MIT5/ENE5 independent package consumer — 2026-10-14

Требование: «давай следующий выполним» — package portion после strict decode
ownership. [Контракт](PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[runner](../../ManualTests/UnitsPackageConsumer/README.md).
Windows x64/build26300/net10.0/SDK10.0.402/xUnit2.9.3/VSTest.

Изменены metadata/README Enerit, Enerit.Generator, Mithril и Description Core.
Runtime/generator C# logic не менялась. Independent consumer имеет own MSBuild/CPM
boundary, explicit PackageReference analyzers и compile-time generated type evidence.
Program/Contract скопированы из AppUnitsPipesProbe; после копирования build/runtime
не обращаются к source references. Repo global.json выбирает SDK. NuGet mapping и
isolated cache проверяют origin/version каждого TrueMoon package, без UI/project
libraries. Actual compiler analyzer paths/SHA и generated sources сохранены.
Runtime outputs включают apphost/assembly/deps/runtimeconfig и dependency DLLs,
не содержат analyzer/Roslyn/graphics DLLs. Packages остаются local, не опубликованы.

Первый run ошибочно требовал ReplySerializationExtensions.g.cs вместо actual
Reply.SerializationExtensions.g.cs; следующий имел Verify-Case параметр Pid,
конфликтующий с read-only PowerShell PID. Исправлены только runner names/parameter,
oracles сохранены. Оба failed run directories сохранены; final fresh run полный.

Из src:

```powershell
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1
$projects = @(
    @('TrueMoon.Enerit.Tests', 'enerit'),
    @('TrueMoon.Enerit.Generator.Tests', 'generator'),
    @('TrueMoon.Mithril.Tests', 'mithril'),
    @('TrueMoon.Tests', 'core'),
    @('TrueMoon.Cobalt.Tests', 'cobalt'),
    @('TrueMoon.Cobalt.Generator.Tests', 'cobalt-generator')
)
foreach ($project in $projects) {
    foreach ($configuration in @('Debug', 'Release')) {
        $name = $project[1] + '-' + $configuration.ToLowerInvariant()
        dotnet test (Join-Path $project[0] ($project[0] + '.csproj')) --configuration $configuration --no-restore --logger "trx;LogFileName=$name.trx" --results-directory TestResults/UnitsPackages/tests --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Failed $name" }
    }
}
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

| Проверка | Финальный результат |
| --- | --- |
| Local feed |9packages; metadata/assets, TrueMoon dependency versions,2analyzer-only layouts/SHA проверены |
| Compiler/source graph |9TrueMoon package libraries в both-generators,8в microsoft-only;0project/UI libraries;4actual compiler configurations |
| Functional matrix |108/108cases =72Cobalt/DI +36DI без Cobalt analyzer; Debug/Release build/publish, Completed=true |
| Negative control |Without Enerit analyzer fresh build — ожидаемый CS0234, generated namespace отсутствует |
| Regression |Enerit126, generator114, Mithril89, Core81, Cobalt37/generator6 отдельно Debug/Release;12TRX/906passed/0failed/0notExecuted |
| Full solution |Final Debug non-incremental exit0/errors0/warnings1227 |

129runner commands:9packs,2positive restores/4builds/4publishes,108process cases,
negative restore/build. Result oracles проверяют actual root/child PID, expected1/2
owned generations/distinct identities, crash/noncrash exit codes, restart counts,
exact typed/concurrent/error/cancel outcomes, terminal budget cancellation и cleanup
scope. Probe внутри проверяет before-App.Dispose workers/pending/controller и
child Stop1/server drain. Parent-exit supervisor kills parent only; killed parent
client cleanup не наблюдаем и не маркируем passing.

| Requirement | Evidence |
| --- | --- |
| «давай следующий выполним» |both-generators-Release-publish-cobalt-restart/budget и соответствующие Debug/build/DI cases; delivered own child, pending fails без replay, same proxy reconnect, terminal cancellation |
| «давай следующий выполним» |microsoft-only-Debug-build-di-startup-failure/stop-pending/parent-exit и delivery siblings; no Cobalt compiler analyzer, cleanup original cause/PID/stop/worker assertions |
| «давай следующий выполним» |AnalyzerEvidence.cs +Verify-Assets/Verify-Generation: exact package source/version/cache/analyzer SHA; build-without-enerit-generator expectedCS0234 |

Raw summary/logs preserve all case records, commands, source/producer hashes,
package descriptions/assets and generated-source fingerprints. QA research/plan/status
в Git metadata .git/testagent/units-packages не stageable. No new C# unit-test cases:
existing typed/process/ownership suites reused; package assertions находятся в runner.
Coverage/empirical mutation не запускались. MIT5 diagnostics и ENE5 latency/throughput/
allocations остаются отдельными задачами; другие OS/RID/NativeAOT/MMF/stress/UI/
uncooperative shutdown не проверены. Commit/push не выполнялись.

Final package run: `TestResults\UnitsPackages\run-20261014-094823275`, version `0.2.2.7-alpha.units.20261014094823288`; SourceFiles mismatches0,9nupkgs,108cases,129commands, Completed=true.

Final docs validation:49Markdown documents/515local links/140heading links/0problems. Default git diff --check, new untracked source/doc whitespace/markers, runner parser and touched project XML checks passed. Artifacts ignored TestResults/UnitsPackages/links.json; module/source hashes mismatches0.

## MIT5 operational diagnostics — 2026-10-15

Scope: Windows x64/build26300/net10.0/SDK10.0.402, existing bounded same-build
cooperative owned processes. Добавлены public UnitProcessDiagnostic/UnitProcessStage
и category UnitProcess через IEventsSource, без новой зависимости Mithril на App,
Microsoft DI или logging. [Схема и подключение](DIAGNOSTICS.md),
[принятое решение](PLAN.md#mit5-operational-diagnostics--2026-10-15).

Payload фиксирует RunId explicit start, generation launch attempt, unit/owner/child
PID, consumed restarts, effective policy/limit/delays, stage/reason/exit/Error.
LaunchFailed не заменяет caller error; automatic SequenceFailed содержит тот же
Exception, что LastRestartError. Captured Stopped identity не меняется при следующем
start. Старые текстовые категории сохранены; sink exception/disposed source изолированы.
Callbacks остаются синхронными, arbitrary reentrancy/blocking не обещаются.
Stop/restart ownership contract и лимиты сохранены. Аргументы/env/control endpoint
не добавляются в payload; exception message может содержать OS details.

Проверки новых assertions опираются на OS handles/PID/exit codes существующих
fixtures. Restart fixture теперь использует настоящий EventsSource/DiagnosticSubscription
TrueMoon, а не no-op sink. Shared AppUnitsPipesProbe проверяет public payload через
App.UseDiagnostics; те же sources копируются independent package runner.
Lifecycle fixture добавляет throwing-sink scenario. Unit tests отдельно проверяют
lazy snapshot после второго Start, fresh RunId, original Exception identity,
отменённую preparation и disposed source. По finding-tests skill выполнен findTests,
но Rider ответил Tool not found; использованы существующие source/process suites.
Focused code-testing-agent workflow; assertion-quality/test-gap-analysis self-review
по .NET/xUnit guidance. Empirical mutation/coverage не запускались. Static review:
точные PID/поколения/exit/configuration/reasons/count/order, errors identity и negative
no-late-launch assertions ловят потерю корреляции/ошибки/defaults/cleanup. Real OS
StopFailed/reap denial и arbitrary monitor hooks не инъецировались; соответствующая
новая observational ветка не выдаётся за проверенную OS failure matrix.

Preflight: fixture сначала не имел ProjectReference на TrueMoon для concrete diagnostics;
добавлен только consumer reference, runtime Mithril dependency graph не изменён.
Первая Debug assertion run:85passed/9failed — read-only ParentProcessId recomputed
при JSON deserialization в test process. Исправлен в constructor payload; конечные
межпроцессные assertions проверяют transmitted owner PID. Initial failure TRX сохранён
как initial-assertion-failure.trx и не входит в passing totals.

### Текущие команды и результаты

Основной artifact directory: `TestResults/MithrilDiagnostics/run-20261015-175142306`.

```powershell
dotnet build TrueMoon.slnx -c Debug --no-incremental -m -v minimal
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj -c Debug --logger 'trx;LogFileName=mithril-debug.trx' --results-directory TestResults/MithrilDiagnostics/run-20261015-175142306
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj -c Release --logger 'trx;LogFileName=mithril-release.trx' --results-directory TestResults/MithrilDiagnostics/run-20261015-175142306
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj -c Debug --logger 'trx;LogFileName=enerit-debug.trx' --results-directory TestResults/MithrilDiagnostics/run-20261015-175142306
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj -c Release --logger 'trx;LogFileName=enerit-release.trx' --results-directory TestResults/MithrilDiagnostics/run-20261015-175142306
pwsh -NoProfile -File ManualTests/MithrilRestartProbe/RunRestartSmoke.ps1 -ResultsDirectory TestResults/MithrilDiagnostics/run-20261015-175142306/restart
pwsh -NoProfile -File ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1 -ResultsDirectory TestResults/MithrilDiagnostics/run-20261015-175142306/lifecycle
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1
```

- Full nonincremental Debug:exit0/errors0/warnings1193. Count как у previous ENE5
  build, включая generated benchmark CS8669; warnings не подавлялись. New payload XML
  documented. Это build, не запуск UI/native suites.
- Mithril:Debug94/94 и Release94/94; Enerit:Debug126/126 и Release126/126,
  Failed0/Skipped0,4current TRX/440passed. Own Core/Cobalt/generator suites не повторены.
- Restart:40/40 apphost cases Debug/published Release,42commands,129source SHA,
  Completed=true. JSON содержит безопасную ErrorType projection, полный Error проверен
  по identity внутри fixture. Lifecycle:18/18,20commands,7selected source SHA,
  Completed=true; дополнительно exact trace assertions выполнены hosted xUnit cases.
- Fresh independent package: `TestResults/UnitsPackages/run-20261015-125732143`,
  version `0.2.2.7-alpha.units.20261015125732156`,9producer projects/8nupkgs,
  128commands,205source fingerprints,108/108cases, Completed=true.96normal parent
  cases проверяют новый public payload через App.UseDiagnostics;12abrupt-parent-exit
  cases проверяют child/server cleanup, killed parent trace не наблюдается.
  Cobalt/DI и DI с отключённым Cobalt analyzer, Debug/Release build/publish;
  isolated cache/feed, compiler analyzer path/SHA/generated sources и runtime asset
  controls сохранены. Omitted Enerit analyzer negative control — expected CS0234.
  End-of-run source SHA comparison добавлена в package/restart/lifecycle runners;
  packages не публиковались.

| Requirement | Evidence: exact test / check |
| --- | --- |
| Policy/budget/defaults, restart failure, generation/PID/exit/order, no launch после Stop | RestartProcessTests.Restart_OwnedSequence_EnforcesPolicyBudgetDelayAndStop (20cases, AssertDiagnostics) |
| Graceful/forced cleanup и throwing sink не меняют owned lifecycle | ProcessUnitHandleTests.OwnedChild_ShutdownScenario_ReapsProcessAndReleasesHandle (9cases) |
| Fresh sequence identity, lazy snapshot и исходная ошибка caller | ProcessDiagnosticsTests.Diagnostics_FailedExplicitStarts_PreserveErrorAndUseFreshRunIdentity (2cases) |
| Cancelled preparation без успешного launch/terminal | ProcessDiagnosticsTests.Diagnostics_CancelledPreparation_IsReportedWithoutStartedOrTerminal |
| Disposed source не препятствует error/stop | ProcessDiagnosticsTests.Diagnostics_DisposedSource_DoesNotReplaceLaunchFailureOrPreventStop |
| App/package public diagnostics и integration cleanup | Shared AppUnitsPipesProbe.RunAsync assertions, ProcessDiagnosticsChecked=true в96package cases |
| Successful test execution | Четыре dotnet test команды выше:94/126 в каждой Debug/Release, exit0/440passed |

MIT5 завершён только в указанном bounded scope. Other OS/RID/self-contained/single-file/
trimming/NativeAOT, independent external restart, borrowed processes, arbitrary hooks/
blocking callbacks, uncooperative services, long resource trends и durable audit не
подтверждены. Performance/Core/Cobalt/generator/UI/native/new engine projects не
выполнялись. Следующий этап — сводная quality matrix модулей и оставшегося UI
acceptance перед выбором ENG0; measured allocation backlog сохраняется отдельно.

Final QA: `VerifyFinal.ps1`/`final-audit.json` в основном artifact directory:
341source hash checks (205package +129restart +7selected lifecycle), mismatches0;
8nupkg hashes и28generated source hashes совпали.4current TRX подтверждены;
10documents/151local links/80anchors без ошибок. Три изменённых PowerShell runners
прошли parser, changed fixture csproj — XML parse; scoped git diff --check passed.
Initial failed TRX исключён из440passing результатов. Commit/push не выполнялись.
