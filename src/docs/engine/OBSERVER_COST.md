# ED6b.2e.4f: стоимость bounded open batches

Владелец: второй агент, 2026-11-09. Единственный task/acceptance —
[EDITOR_PLAN](EDITOR_PLAN.md#ed6b2e4f-observer-cost). Общие PLAN/STATUS/HISTORY
сводит координатор параллельного ARCH1f.3. Предшествующие ED5b/COB5a.1/SplitView/
COB6e и общая версия0.2.5.2-alpha сохраняются.

## Реализация

`EditorSourceOpenBatch` остаётся attempt-owned продуктом census, не DI сервисом
или глобальным worker pool. Capacity4/MinimumFiles256 не менялись. Малые graphs
и injected open/metadata/directory hooks сохраняют serial admission.

В прежнем batch создавались List<Task<Slot>>, отдельные capturing Task.Run jobs и
WhenAll с массивом результатов. Теперь nongeneric Task workers используют static
state delegate и записывают результаты каждый в свой фиксированный slot вместо
generic Task result. Четыре pending Task references хранятся в inline buffer;
nongeneric WhenAll получает только admitted span без List/Task[]. Массив Slot
остаётся единственным хранилищем reader/error результатов batch. ExecutionContext flow, default
TaskScheduler и DenyChildAttach сохраняют прежнюю Task.Run модель. Порядок handoff
задаёт прежний WhenAll, локальные workers принадлежат только одному batch.

Передача владельцу разрешена только после actual completion всех admitted jobs;
scheduler cancellation не используется. Каждый slot хранит reader либо captured
exception. Take сохраняет descriptor ordering, однократную передачу и ранний Changed
перед поздним staged error. Validation/queue failure прекращает будущий admission,
сохраняя earlier owners до drain. Cancellation после Open освобождает возвращённый
reader до completion. Ошибка cleanup также записывается в slot с retained reader.
Dispose закрывает unread owners, transferred reader остаётся
у census. Native APIs, hashing, directory pins, probe witnesses и retry budgets
не менялись. Public API/dependencies/protocol не менялись.

## Проверяемые контракты

| Требование | Прямой test evidence |
| --- | --- |
| Context flow и изоляция | `EditorSourceOpenBatchTests.Create_WorkersRetainEachAdmissionExecutionContextWithoutLeakingChangesToTheCoordinator`: разные AsyncLocal значения каждого validation/open, worker-only изменение не выходит в caller |
| Быстрое завершение/admission | `Create_ImmediatelyCompletedWorkersCannotLoseSlotsOrPublishBeforeAllAdmissions`:128batches, exact4validations/4single opens/4ordered missing slots, duplicate Take rejected |
| Реентрантный handoff | `Create_CompletionMayDisposeAndReadmitWithoutRetainingAnyEarlierNativeOwner`: gated completion, Dispose/reopen всех старых files и следующий batch из await continuation |
| Bounded overlap и actual ownership | `Create_FourNativeOpensOverlapButHandOffWaitsForEveryOwnerAndPreservesOrder`:4реально открытых file owners, gated completion, disposal unread/retention taken и exclusive reopen |
| Validation/cancel drain | `Create_ValidationFailureStopsFutureAdmissionButWaitsForEarlierNativeJobs`, `Create_CancellationAfterOpenWaitsForNativeFactoriesAndDisposesEveryReturnedProduct`, `Stop_WaitsForFourActualNativeOpensAndCannotPublishOrReleaseOwnershipBeforeTheyDrain` |
| Полный witness | `EditorSourceOpenBatchCensusTests` и прежние EditorSource census/watch/directory-pin suites: full hash, same length/mtime difference, tree/probe coverage, retry/changed ordering, canceled/closed epoch |

Static pseudo-mutation review: подавление context flow ловится per-path AsyncLocal; потеря
slot/двойной вызов — exact dictionary counts и duplicate Take; early handoff/lost
owner — gated real-file tests; out-of-order staged error — earlier Changed test;
early Stop completion — gated native Stop test. Это review assertions, не executed
mutation score и не coverage percentage. Неподтверждённые scheduler failures/native
hardware behavior не выдаются за выполненные проверки.

## Проверки и измерения

Windows x64, Ryzen9 9900X/24logical processors, SDK10.0.403/runtime10.0.14/net10.0,
09ноября2026. Focused `FullyQualifiedName~EditorSource`: **199/199 Release**.
Full Editor: **1468/1468 каждый Debug/Release, failed0/skipped0, exit0**;3новых cases
в одном существующем test class. Production Editor Debug/Release source build с
`BuildProjectReferences=false`:0errors/0warnings. Прежние xUnit1031 warnings в
существующих Diagnostics tests сохраняются. Изменения f.3 Aether проверяет
координатор отдельно; shared-output builds не запускались внутри perf windows.

```powershell
$env:APPDATA = (Resolve-Path TestResults/ObserverCost/settings).Path
dotnet build TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --no-restore -c Release -p:BuildProjectReferences=false '-bl:TestResults/ObserverCost/static-final-build-{}.binlog' -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Release -p:BuildProjectReferences=false --filter 'FullyQualifiedName~EditorSource' --logger 'trx;LogFileName=observer-final-focused-release.trx' --results-directory TestResults/ObserverCost '-bl:TestResults/ObserverCost/final-focused-Release-{}.binlog' -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Release -p:BuildProjectReferences=false --logger 'trx;LogFileName=observer-final-full-release.trx' --results-directory TestResults/ObserverCost '-bl:TestResults/ObserverCost/final-full-Release-{}.binlog' -v quiet
dotnet build TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --no-restore -c Debug -p:BuildProjectReferences=false '-bl:TestResults/ObserverCost/final-build-Debug-{}.binlog' -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug -p:BuildProjectReferences=false --logger 'trx;LogFileName=observer-final-full-debug.trx' --results-directory TestResults/ObserverCost '-bl:TestResults/ObserverCost/final-full-Debug-{}.binlog' -v quiet
```

Task-local APPDATA устраняет зависимость child SDK tooling от недоступной user
NuGet.Config. Глобальные настройки/версии/зависимости не менялись.

### Финальное парное измерение

Production source/harness сначала собраны до patch, полный Release output заморожен
в `TestResults/ObserverCost/baseline`. После patch собран только Editor; его DLL
заменяет ровно один файл в копии baseline (`final`). Из96файлов двух outputs
единственное отличие — `TrueMoon.Aether.Editor.dll`. Harness, Aether, Contracts,
Cobalt, остальные managed/native dependencies совпадают побайтно. F.3 source edits
и новые Aether binaries не подмешаны в comparison. Зафиксированы исходные SHA256:

| Объект | SHA256 |
| --- | --- |
| Baseline Editor DLL | E72E3848E3CBBEE706D21A9C2AB3588171DC409E2405064144689F6AAED13C22 |
| Final Editor DLL | 35E0E15F5D63C9D94736BCF2DBA0B9E61C8508E9988375B169A263479ACAA5EB |
| Baseline batch source | 577416CADF6BA71E04DCFDE283731FDBDB5A0FD623196E2393ABBB01EAD37447 |
| Final batch source | DBCF59A61AA36837FA395D6DD266A01AAEEBDA2D714A9E61C1D7C38EB32837BD |

Baseline source — blob `890b2162c0f5f1af88f33b86e47d198bf8cb8b53`, исходный
HEAD `2e3b2b1a00d67ef10d44e8c8deab1d811584d42d`; прочие текущие dirty features
были включены одинаково в оба frozen outputs. Свежая сборка будущего checkout не
обещает тот же binary SHA; before/after comparison требует снова одного frozen graph.

Final window16:19:36–16:24:57UTC:4последовательных fresh processes, Polling
baseline→final, Notifications final→baseline. Каждый использует неизменённый
`AetherSourceWatchPerformance.dll --profile files --notifications false|true --output fresh-dir`.
Обычный10s polling после completion/5s cooperative budget и3steady samples сохранены.
Actual four-project Build/initial pass отдельны от steady windows; caches warmed.
Каждый descriptor:2567files/2565tree files/84probes/12005643bytes. После нормализации
только run/framework roots у каждой пары полностью совпадают file SHA/length,
membership и probe paths/existence. Подмена descriptor/вымышленный Same не использовались.
Power plan до/после unchanged; CPU frequency/фон ОС не фиксировались.

| Case | Median scan bounds ms before → final | Whole-process CPU ms/cycle before → final | Allocated bytes/cycle before → final | Allocation delta |
| --- | --- | --- | --- | --- |
| files/Polling |1123.28–1148.28 →1140.67–1161.88 |1562.50 →1390.625 |9060296 →8746616 |−3.46% |
| files/Notifications |1124.31–1153.60 →1120.50–1143.89 |1484.375 →1453.125 |9065256 →8746624 |−3.51% |

Scan upper +1.2%/−0.8%: latency практически сохранена в разрешении этого профиля.
CPU values ниже на11%/2.1%, но3samples, whole-process counters и вариативность controls
не дают основания заявлять общий CPU speedup. Allocation reduction — подтверждённый
результат этого среза. Fresh paired comparison не заменяется числами04ноября.

4/4cases:16full-read Same passes,4same-length+mtime Changed, budget-exceeded0,
source drift0, NoBuild/Changed idle source reads0; финальный State=Stopped/watchers0,
все процессы exit0. Detection Polling11119→11384ms, Notifications1415→1394ms;
close/drain1.46→1.62ms и1.54→1.73ms. Ни detection speedup, ни faster cleanup не заявлены.

Отдельный task-scoped counter с test-friend доступом изолирует allocation overhead:
4synthetic missing opens/no-op validation,256warm batches,5samples по2048batches.
Median **1327.91→871.36B/batch (−34.4%)**. Тот же counter binary, разные frozen Editor
DLL; NativeIO=false/Timing=false. Это attribution без native IO и без method timing,
не независимый package consumer. Полный observer также делает file/native/polling/
sampler/process work, поэтому процент end-to-end меньше; остальные расходы отдельно
не атрибутированы.

### Отклонённые эксперименты и границы приёмки

Сначала проверен raw safe-ThreadPool/TCS вариант:12cases (files/bytes/control ×
Polling/Notifications × baseline/candidate),48Same/12Changed, все budget/drain/hash
predicates прошли. Array/list/closure allocations уменьшились, но CPU/latency tradeoff
не дал основания менять scheduler модель. Дополнительный inline-TCS experiment:
4cases/16Same/4Changed, Notifications scan upper1178→1324ms (+12.4%), CPU1438→1672ms.
Этот вариант отклонён, хотя аллокации снизились примерно на4.8%. Оба frozen experiments
и исходные logs сохранены в `paired`/`paired-inline`; production использует final
nongeneric Tasks/static state, не ThreadPool/TCS implementation.

После первого experiment явно выбраны bounded gates: correctness без regression,
synthetic batch allocation reduction≥25%, many-files whole-process reduction≥3%,
median scan upper без ухудшения>10%. Final candidate проходит эти gates; CPU gain
остаётся exploratory. Byte/control serial paths не менялись; их actual native/SDK
controls прошли в первой matrix, но final candidate повторял только affected many-files
profile. Current full Editor tests покрывают threshold255/256/257, serial custom hooks,
content/membership/probe changes, first-error ordering, pins/reparse/replacement,
cancellation и actual owner drain. Full Aether/general release результаты принадлежат
отдельной проверке координатора, не составляются из этой matrix.

Raw reproduction: `TestResults/ObserverCost/RunPairedFinal.ps1`, затем
`SummarizePaired.ps1 -EvidenceDirectory TestResults/ObserverCost/paired-final`.
Оба скрипта — task artifacts; постоянный CLI/harness находится в
[ManualTests/AetherSourceWatchPerformance](../../ManualTests/AetherSourceWatchPerformance/README.md).
Для повторения сначала заморозить полный baseline output, затем копировать только
Editor DLL в candidate output; запускать profile/mode выше с fresh directories
поочерёдно, без SDK builds/tests/CPU-heavy jobs внутри steady windows. Verification
обязательно сверяет all96output hashes, normalized fixture witnesses и actual exit/drain.

## Воспроизведение и хранение

Raw task artifacts находятся в ignored `TestResults/ObserverCost`: baseline/optimized
frozen outputs, исходный batch `.before.cs`, build/test binlogs/logs/TRX, paired
verification.json и per-case report.json/stdout/stderr. Сохраняются локально до
ручной очистки TestResults; versioned числа/команды/ограничения после приёмки
остаются здесь и в engine HISTORY, не зависят только от ignored файлов.

Не заявляются новый native UI/Editor input/fresh-package release proof, cold-disk,
другие OS/сетевые FS/driver kernel attribution и общий performance speedup. Все
CPU/allocations/IO показатели end-to-end включают process, sampler и polling wait;
snapshot-derived scan bounds и CPU timer resolution отделяются от exact method timing.
