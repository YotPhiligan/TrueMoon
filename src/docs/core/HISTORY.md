# Core: история проверок и ревью

## Borrowed health и output windows — 2026-10-29

Для ED6a.2 добавлен общий `IDiagnosticHealthReader`/`DiagnosticHealthSnapshot` в
Contracts. `DiagnosticRecordAccess` предоставляет этот alias вместе с history/
importer, не реализует Dispose и не становится отдельным owner pipeline.
UseDiagnostics legacy/repeated setup удаляет все три access registrations.
ReadHealth копирует collector counters под его gate, затем independent worker
snapshots; не flush-ит sinks, не запускает retention и не читает файлы. Optional
emergency/retention может быть disabled/not-yet-available. UI reader gaps, ring
eviction/capture failures и per-sink drops не суммируются в один loss. Accepted/
Written не объявляются durable delivery. Bounded LastError пригоден для local
display; Editor metadata exporter исключает его текст.

ProcessOutputCapture.Read(maxChars) копирует только выбранный suffix ring с UTF16
boundary. Прежний Read() сохранён. Original capture/file counters неизменны;
ProcessOutputSnapshot.OmittedCharacters отдельно описывает display window,
Limit(...) работает над independent snapshot. Window0 даёт counters без текста.
Reader/file tasks, queue8, file budget, borrowed TextReader и2s/5s owner waits
не менялись. Это не ещё одно исправление исторического file-worker timeout.

Core **387/387** Debug/Release, failed/skipped0;13новых cases. Tests
`Health_ReadsBlockedSinkWithoutDrainAndSeparatesMemoryEvictionFromSinkLoss`,
`AppHealth_IsBorrowedAndRepeatedLegacySetupRemovesEveryAccessView` (Cobalt/MS DI),
`FailingSink_HealthReportsFailureAndBoundedErrorInsteadOfSuccessfulWrite`,
`ReadWindow_PreservesCaptureLossAndSeparatesDisplayOmission`,
`WindowBoundary_DoesNotExposeHalfSurrogateOrDoubleCountCaptureLoss` и invalid-window
case проверяют новые contracts напрямую. Commands: `dotnet test
TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore -c Debug` и `-c Release`, с unique
binlogs/TRX. [Audit/8TRX](../../TestResults/ED6a2-20261029-1/audit.json).
Engine/UI/native results имеют [свой scope](../engine/HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29);
Core не получает UI dependency. Full solution/packages/published/hardware не запускались.

## File-drain без shared ThreadPool — 2026-10-29

По продолжению ARCH2e.4 добавлен [Core-only ProcessOutputProbe](../../ManualTests/ProcessOutputProbe/README.md).
Предварительные controls:1000pairs/2000files в одном процессе,50cold processes
по20pairs и1000pairs с App/JSON diagnostics прошли. Отдельный diagnostic consumer
на прежних nupkg с sync failure marker прошёл30replays/90children. Fresh run со
сбором heap только по фактическому marker также прошёл8cases; failure heap не
получен, дамп успешного процесса вместо fault не выдаётся за root-cause proof.
Исходный external consumer source восстановлен/проверен по baseline SHA перед
созданием отдельной diagnostic copy; старый immutable feed не менялся.

Контролируемый isolated process доказал конкретную liveness dependency: после
допуска/записи24bytes reader доходит до EOF, затем единственный shared worker
удерживается completion callback. Старый async file worker остаётся
WaitingForActivation/WaitingForChunk и не завершает drain за прежние2s при
ReadCompletion=RanToCompletion,24written/24characters/0dropped.
[Exact RED](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-exact-red/report.json).
Один LongRunning worker с прежним `WaitToReadAsync().AsTask()` тоже дал RED:
async-to-Task bridge всё ещё требовал pool callback.
[Промежуточный RED](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-green/report.json).

**Решение2026-10-29:** file-enabled capture получает один dedicated background
worker с synchronous file IO. Channel сохраняет8bounded chunks; TryWrite/drop и
UTF8/file budgets прежние. Sync semaphore будит worker отдельно на каждый
admitted chunk и на EOF. No-file mode worker не создаёт. Semaphore освобождается
после обоих tasks; borrowed reader остаётся у caller. FileStage/ReadCompletion/
FileCompletion и file errors/loss counters сохраняются. Runtime/compile budgets
2s/5s не увеличены. Цена решения — один background worker на active file capture;
caller по-прежнему управляет количеством captures. App/DI/async activation API
не менялись; arbitrary uncooperative filesystem liveness не обещается.

[GREEN](../../TestResults/ARCH2e4-FileDrain-20261029-1/starvation-green2/report.json)
получен при том же занятом pool worker. Автоматическая регрессия
`ProcessOutputFileWorkerTests.FileDrain_PoolWorkerHeldAfterEof_CompletesAndPreservesBorrowedReader`
проверяет оба completion tasks, реальное содержимое файла,24bytes/0loss/null IO
error и borrowed reader. Pool settings меняет только child process, test host не
меняется. Fixture подключён к существующему TrueMoon.Tests через ProjectReference
без runtime reference и Copy/FileWrites target, отдельного test project нет.
Core capture subset20/20, full Core374/374 и Aether1550/1550 Debug/Release прошли.
Post-fix controls:1000pairs/2000files непосредственно и ещё столько же внутри
App/JSON diagnostics прошли. Это lifecycle/content proof, не perf baseline.

Точные успешные test команды (в Debug и Release, отдельные TRX/binlogs):

```powershell
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=TrueMoon.Tests-Debug.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Tests-Debug-{}.binlog'
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=TrueMoon.Tests-Release.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Tests-Release-{}.binlog'
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --logger 'trx;LogFileName=TrueMoon.Aether.Tests-Debug.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Aether.Tests-Debug-{}.binlog'
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --logger 'trx;LogFileName=TrueMoon.Aether.Tests-Release.trx' --results-directory ./TestResults/ARCH2e4-FileDrain-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-FileDrain-20261029-1/TrueMoon.Aether.Tests-Release-{}.binlog'
```

Связь controlled starvation с точной причиной прежнего intermittent эпизода
не установлена: у исторического failure нет heap и точной file phase. Исправлен
доказанный shared-pool dependency, historical observation сохранён отдельно.
Passing повторы сами по себе не объявляются доказательством отсутствия иных IO
faults. Hardware/driver/K7/general release и полный solution scope не меняются.

## Фазы process output capture — 2026-10-29

Для ARCH2e.4 ProcessOutputCapture получил ReadCompletion/FileCompletion/FileStage при
прежнем Completion=WhenAll и borrowing contract. Это позволяет отличить EOF/read
failure от незавершённого file sink в bounded cleanup timeout. Constructor,
ProcessOutputSnapshot, бюджеты и поведение file errors не менялись. Actual stream
ownership исправлен в Aether, Core не закрывает borrowed reader.
Core373/373 Debug/Release прошли; новый combined read/close/timeout proof находится
в Aether output-owner tests. [Точная история, команды и ограничения](../engine/HISTORY.md#arch2e4-stdio-ownership-и-диагностика-drain--2026-10-29).
Свежий run e4-Packages1 показал completed readers при pending file sinks; добавлены FileStage и thread-pool counters. Исходная причина intermittent timeout не установлена; новая диагностика не
объявляется исправлением этого observation. Общий solution run не повторялся.

## Чтение и импорт child diagnostics — 2026-10-27

Contracts добавляет IDiagnosticRecordReader/Importer и DiagnosticOrigin; старый
DiagnosticRecord positional constructor сохранён. UseDiagnostics(pipeline)
регистрирует nondisposable borrowed access views, один owner pipeline для Cobalt
и MS DI. Повторный setup/legacy overload заменяет views без создания discarded sinks.
[API/семантика](DIAGNOSTICS.md#чтение-и-импорт-bounded-history),
[transport owner](../engine/DIAGNOSTICS_DELIVERY.md).

ReadAfter — exclusive cursor, oldest-first independent bounded snapshot1..1024.
LatestSequence включает unavailable slots. Import назначает host appid/sequence
и origin child appid/sequence, сохраняет PID/UTC/trace/exception strings через
local clipping/refreeze. Authoritative context копируется первым; live filters,
ring и sink queues остаются общими. Self/invalid/recursive origins отвергаются;
импорт не публикует global DiagnosticListener event.

Новые18methods/59cases:
[Requirement | Evidence matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-matrix.md),
[assertion/gap review](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-quality.md).

| Требование | DiagnosticImportTests |
| --- | --- |
| Cursor/bounds/eviction и independent snapshot | ReadAfter_UsesExclusiveHostCursorAndOldestFirstBoundedOrder; ReadAfter_MaximumPageAndEvictionKeepEarlierSnapshotsIndependent; ReadAfter_InvalidBoundsThrowWithoutChangingHistory |
| Refreeze/context priority/origin/trace и clipping | TryImport_RefreezesClippedAuthoritativeContextAndPreservesChildCorrelation; TryImport_PropertyBudgetPrioritizesContextAtAndBelowExactBoundary; TryImport_TracePairIsPreservedOrClearedTogether |
| Invalid/recursive/closed/sink budgets, no global replay | TryImport_InvalidIdentityOrRecursiveOriginRejectsBeforeAdmission; TryImport_DoesNotReemitToGlobalListenersOrFeedAnotherCollector; TryImport_UsesReservedBoundedSinkQueueAndClosedLateWriteAccounting; TryImport_MemoryAndSinkByteLimitsAcceptExactBoundaryAndRejectOneByteBelow |
| Both-DI replacement и exactly-once pipeline ownership | AppRegistration_ReplacementProvidesOneBorrowedAccessAndDrainsOwnedPipeline; AppRegistration_DefaultOrLegacyHasNoDeliveryAccessAndNeverCreatesDiscardedPipeline |

Initial narrow59/59. Whole-Core RED372/373 выявил неверное абсолютное ожидание
пустого collector в App-test: Expected0 Actual2, DiagnosticImportTests line395,
parallel Apps публиковали process-global TrueMoon events. Fix только в тесте:
уникальная imported provenance, sink barrier только для своей записи, relative
cursor и exact closed-history/disposal checks. Global routing не менялся.
Rebuilt whole-Core GREEN373/373; RED/GREEN artifacts core-full-debug-repro-1.*
и core-full-debug-fixed-1.* сохранены в evidence directory.

Final Core373/373 Debug/Release, failed/skipped0; все59newcases passed.
Full solution4285/4285 в каждой конфигурации, обе сборки0errors:
Debug nonincremental1546warnings/Release incremental1316warnings, SDK10.0.403.
[Общая matrix/команды/границы](../engine/HISTORY.md#child-diagnostic-delivery--2026-10-27),
[evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source hashes1020/changes0, test binaries20, user DI10.0.14 SHA unchanged.
Aether source3-child proof относится к transport consumer; GPU/published/package/
performance acceptance не повторён. CORE6 остаётся частичным.


## Explicit parent и kind для IPC — 2026-10-26

IDiagnosticTelemetry/DiagnosticTelemetry получили StartOperation(name,kind,parent,properties)
для Client/Server spans с explicit remote parent. Old overload использует Internal
и current flow. Parent argument обязателен, null выбирает current flow; default literal
в старом двухаргументном вызове не стал неоднозначным. Default interface implementation
вызывает старый API custom owner без принудительного вычисления properties.
Invalid ActivityKind отклоняется до observer/property callbacks. Bounded sampling,
метрики, callback isolation и deferred ownership release сохраняются.

Новые6cases в DiagnosticTelemetryTests: ExplicitRemoteParent_UsesRequestedKindInsteadOfAmbientParentAndRestoresCurrent(2),
InvalidActivityKind_IsRejectedBeforePropertiesOrObservers(2),
InterfaceKindOverload_ForwardsToExistingOldOnlyImplementationWithoutEvaluatingProperties(1),
OldOperationOverload_DefaultLiteralRemainsUnambiguousAndUsesInternalKind(1).
Final narrow62/62, full Core314/314 Debug/Release. Full solution Debug4021/4021,
Release4020/4021 из-за прежнего Aether CPU receiver failure; общий green не заявлен.
Builds0errors, Debug nonincremental1530warnings/Release incremental1300warnings,
SDK10.0.403. [Общая matrix/команды/ошибки](../enerit/HISTORY.md#ene6d1-trace-context-через-pipes--2026-10-26),
[evidence](../../TestResults/DiagnosticsIpc-20261026-1/verification.json).
Source fingerprints993/test binaries20, source changes0. Native/published/GPU/
package/performance проверки не повторены. CORE6 остаётся частичным.

## Операции и трассы диагностики — 2026-10-26

Продолжение запроса «давай следующий шаг»: первый CORE6d профиль и engine consumer.
Contracts определяет IDiagnosticTelemetry/IDiagnosticOperation/DiagnosticOperationOutcome;
TrueMoon владеет ActivitySource/Meter с именем TrueMoon. Опции копируются до resources.
Базовый App лениво регистрирует единственный interface owner; UseDiagnostics заменяет его.
[API, sampling, численные limits и lifetime](DIAGNOSTICS.md#трассы-и-метрики-операций).

Метрики truemoon.operation.count и truemoon.operation.duration (секунды) имеют только
operation/outcome tags. Registry64имени (предел256), trace properties16×256,
имя/ключ128; sampling None/PropagationData не вычисляет custom properties.
Первый Complete фиксирует outcome, Dispose публикует once; без Complete — Incomplete.
Observer/property failures и sync reentry изолированы, status содержит счётчики.
Закрытый owner сохраняет source/meter до Dispose последнего active scope.
Exception objects не удерживаются, error.type ограничен; внешних exporters/MEL нет.

| Требование | Точное поведенческое доказательство в DiagnosticTelemetryTests |
| --- | --- |
| Unobserved/disabled/sampling и lazy properties | NoConsumers_ReturnsNullWithoutEvaluatingPropertiesOrRejectingWork; EnableFlags_IndependentlyControlTraceAndMetricConsumers; Sampling_ControlsScopeAndLazyPropertiesWithoutCountingConsumerRejection; AllDataProperties_BoundInspectedEntriesClipSurrogatesAndPreserveReservedMetadata |
| Async parent и TraceId/SpanId событий | NestedAsyncOperations_RestoreCurrentAndCorrelateDiagnosticRecords |
| Counter/duration/outcomes и bounded dimensions | OperationOutcome_FirstCompleteWinsAndRepeatedDisposePublishesExactlyOnce; MetricsOnly_UsesNoActivityOrPropertiesAndNeverAddsHighCardinalityTags; NameCardinality_RejectsNewNamesAtLimitButContinuesKnownOperations |
| Options snapshot и границы | Options_AreCopiedBeforeCallerMutatesFlagsAndEveryLimit; InvalidBudgets_AreRejectedBeforeAppCreatesAnySink; BudgetBounds_AcceptFirstAndLastValidValues |
| Нет удержания Exception/payload | Completion_RetainsOnlyExceptionTypeWhileScopeIsStillActive |
| Listener faults/construction/reentry | ThrowingCallbacks_AreCountedAndRestoreParentWithoutBreakingCaller; ReentrantCallbacks_RejectNestedWorkWithoutRecursingOrCorruptingOuterScope; Constructor_ContainsInstrumentPublishedFailuresAndLeavesTracingUsable; Constructor_ContainsShouldListenFailureAndKeepsMetricsUsableWithoutActivitySource |
| Default App, обе DI, lifecycle и повторный setup | DefaultAppRegistration_ProvidesUsableTelemetryWithoutExplicitDiagnostics; AppRegistration_HasOneInterfaceOwnerAndDefersReleaseForActiveScope; AppRepeatedSetup_ReplacesTelemetryAndCopiesNestedOptionsForBothResolvers; CloseWhileActive_FencesAdmissionAndDefersInstrumentationReleaseUntilLastScope |

[Тестовый source](../../TrueMoon.Tests/DiagnosticTelemetryTests.cs).
Новые56Core cases; финальный Core308/308 Debug/Release.
[Aether matrix](../engine/HISTORY.md#операции-и-трассы-диагностики--2026-10-26) имеет25новых cases.
Полный solution **3921/3921 Debug и3921/3921 Release**, failed/skipped0:
Core308, Cobalt179/generator91, Enerit166/generator114, Mithril99,
UI893/Hosting114, Aether1216/Editor741 в каждой конфигурации.

SDK10.0.403. Финальный Debug build nonincremental0errors/1530warnings;
Release incremental0errors/1300warnings. Разница warnings отражает scope rebuild.
Команды из src, каждое MSBuild invocation имеет отдельный binlog:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsTelemetry-20261026-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsTelemetry-20261026-1/Debug -bl:TestResults/DiagnosticsTelemetry-20261026-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsTelemetry-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsTelemetry-20261026-1/Release -bl:TestResults/DiagnosticsTelemetry-20261026-1/tests-Release.binlog
```

Первый compile attempt имел3CS0200 из-за readonly return type preview context;
исправлен Dictionary return type. Initial full Debug до default registration:
3831passed/88failed из3919; все88 — IDiagnosticTelemetry отсутствовал в базовом App
без UseDiagnostics. Исправлен AppBuilder graph, добавлены2regression cases обеих DI.
Следующие Core narrow56/56 и полный Aether1216/1216 Debug прошли, затем приведённые
выше full Debug/Release. Failed artifacts сохранены в build-initial/Debug-initial;
это не скрытый повтор для получения green результата.

[Verification](../../TestResults/DiagnosticsTelemetry-20261026-1/verification.json):
34TRX,12binlogs,986source hashes,20test binary hashes, source changes во время
финальной проверки0. Narrow artifact prefixes agent-core-default-1793034022970
и agent-aether-full-1793034043722. Intermediate test-agent state сохранён в OS temp,
в git changes его нет. Assertions/gaps review не выявил оставшихся обязательных
fixes в объявленном scope; root повторно прочитал новые assertions.
Документационная проверка:87relative links/anchors разрешены,4ссылки на ранее
недоступный diagnostics artifact от25октября отмечены historical unavailable;
новых missing targets/anchors0. git diff --check HEAD прошёл.

Manual native/published/GPU/package/performance checks не повторялись.
Исторические Mithril ready-file/CPU receiver races этим passing run не исправлены.
CORE6d частичен: ENE6d IPC trace propagation, дополнительные domain/frame aggregate
metrics, live Editor console/export, bootstrap/private routing/generation и release gates
остаются отдельными задачами.

## Emergency и managed JSONL runs — 2026-10-26

Продолжен CORE6c: независимый finite emergency worker и opt-in run directories
под постоянным JSONL root. [API/defaults/ownership](DIAGNOSTICS.md).
DiagnosticEmergencyOptions: enabled, borrowed Console.Error, queue8 включая
in-flight,16attempts на pipeline, report≤1024chars с LF. Snapshot содержит только
App/Stage/Sink/type/sequence; без payload/exception objects/messages. Writer
failure не вызывает Report; reentry ограничен общим lifetime budget. Health
отделяет rejected queue attempts от последующих Suppressed.

Managed JSONL: JsonUseRunDirectories + copied JsonRetention, default8closed runs /
32MiB /7суток от creation UTC. Lazy GUID directory, exclusive versioned marker
до закрытия файлов, root maintenance lease, oldest-first pruning, строгие
границы с разрешённым equality, newest/explicit protection и observable
overbudget. Только известные плоские marker/events files удаляются нерекурсивно;
active, legacy/unknown/unsafe directories остаются отдельно. Crash освобождает
lease, распознанный run доступен следующему sweep. Flat API/старый4arg constructor
сохранены. Failed maintenance не подменяет успешную запись; status и emergency
показывают проблему. Никакого общего quota report/artifact roots не заявлено.

Normal sink cleanup запускается off caller, включая синхронную часть DisposeAsync.
Normal cleanup затем emergency drain используют один ShutdownTimeout; rollback
всех уже приобретённых sinks идёт параллельно и также имеет один deadline.
Timeout не освобождает исполняющееся IO и borrowed writer. Background cleanup
остаётся до завершения; direct pipeline disposal допускает retry. До создания
самого pipeline bootstrap capture этим срезом не реализован.

| Требование | Evidence: точное имя теста |
| --- | --- |
| Ограниченные metadata, control sanitization, LF и отсутствие exception retention | EmergencyReport_UsesOnlyBoundedMetadataAndOneLiteralNewline; EmergencyReport_DoesNotRetainExceptionWhileWriterIsBlocked |
| Queue включает executing, lifetime attempts/drop/suppression различаются | EmergencyReport_CountsExecutingQueueAndDroppedAttemptsBeforeLifetimeSuppression |
| Writer failure/borrowed lifetime и finite reentry | EmergencyWriterFailure_CountsTypeWithoutMessageOrRecursiveReportsAndKeepsWriterBorrowed; EmergencyWriterReentry_IsFiniteAndNeverWaitsOnItsOwnWorker |
| Normal/capture/overflow failures без payload | Pipeline_ReportsWriteAndDisposeFailuresWithoutPayloadOrExceptionMessage; Pipeline_CaptureFailureUsesEmergencyWithoutPublishingPartialRecord; Pipeline_ReportsOnlyErrorAndCriticalQueueLossWhileNormalSinkIsBlocked |
| Producer/healthy sink независимы от blocked emergency IO | BlockedEmergencyWriter_DoesNotBlockProducersOrHealthySink |
| Flush/timeout/retry/off-caller cleanup/common rollback | PipelineFlush_WaitsForEmergencyReportProducedAfterNormalFence; PipelineShutdownTimeout_RetainsBlockedOwnerAndAllowsRetry; PipelineDispose_SynchronousSinkCleanupRunsOffCallerAndTimeoutKeepsOwnership; ConstructorRollback_UsesOneTimeoutAndRetainsConcurrentCleanupUntilReleased |
| Startup error, disabled и validation до IO | PipelineConstructionFailure_DrainsStartupReportAndReleasesOwnedSinkOnly; Pipeline_EmergencyDisabledDoesNotWriteOrChangeNormalFailureHandling; InvalidEmergencyBudgets_AreRejectedEvenWhenDisabledBeforeSinkCreation |
| Closed runs count/bytes/age и equality | Sweep_CountLimitDeletesOldestByCreationInsteadOfDirectoryMtime; Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality; Sweep_AgeExpiresOnlyStrictlyAfterCreationLimit |
| Active/protected/unknown/reparse ownership | Sweep_ProtectsActiveRunAcrossStoreInstancesUntilItsLeaseCloses; Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget; Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs; Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets |
| Maintenance lease/failure/retry и legacy safety | Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking; Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease; Sweep_PreservesLegacyFlatFilesAndUnrelatedDirectories |
| Managed sink lazy/independent and prefix validation | ManagedJsonSink_CreatesLazyIndependentRunsAndRetainsOnlyNewestClosedRun; UnusedJsonSink_DisposeDoesNotCreateOutput; ManagedJsonSink_RejectsPrefixesOutsideOwnedWhitelistBeforeIo |
| Retention failure/overbudget до emergency closure | Pipeline_RetentionMaintenanceFailurePreservesRecordAndReportsEmergencyThenRecovers; PipelineDispose_ReportsProtectedOversizedRunBeforeClosingEmergencyOutput |
| App Cobalt/MS DI фиксирует nested options и ownership | AppManagedDiagnostics_CopiesNestedOptionsForBothResolvers |

Новые73cases: Emergency22, RunRetention41, ManagedPipeline10. Narrow134/134 с
прежними61pipeline/legacy cases. Полный Core252/252 Debug/Release. Финальные
solution3840/3840 Debug и3840/3840 Release, failed/skipped0; Aether1191/Editor741,
Cobalt179/generator91, Enerit166/generator114, Mithril99, UI893/Hosting114.
SDK10.0.403, Windows x64/net10.0. Debug nonincremental build0errors/1530warnings;
Release incremental0errors/302warnings (разный rebuild scope, не warning cleanup).
Evidence: [verification](../../TestResults/DiagnosticsCore-20261026-1/verification.json),
30TRX включают initial/final runs,6unique binlogs,978source и20tested binary hashes.

Первый полный Debug был3838/3840:73новыхcases прошли, но legacy
AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows использовал
TrueMoon-prefixed probe, на который подписывался параллельный App с default filter.
Тест исправлен на UniqueName()/Filters(name) вне общего префикса; lifecycle
assertions сохранены, production global routing/совместимость не менялись.
После этой правки отдельный Core252/252 и новый full Debug3840/3840 прошли.

В том же initial Debug повторился прежний MithrilRestartProbe ready-1.json sharing
IOException, scenario concurrent-stop. Mithril production/fixture не менялись;
последующие99/99 Debug/Release не означают исправление race. Initial failures
и exact errors сохранены в verification и initial Debug TRX/log; исходный run
не подменён passing повтором.

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsCore-20261026-1/build-Debug-final.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsCore-20261026-1/Debug-final -bl:TestResults/DiagnosticsCore-20261026-1/tests-Debug-final.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsCore-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsCore-20261026-1/Release -bl:TestResults/DiagnosticsCore-20261026-1/tests-Release.binlog
```

Parent Editor переключён на постоянный %TEMP%/TrueMoon/Diagnostics/Editor root;
smoke остаётся в report/diagnostics/editor, Runtime использует общий
report/diagnostics/runtime. Каждый logger владеет отдельным managed run.
Отдельные native/published/UI/GPU/hardware/package/performance acceptance не
повторены; source wiring compiled и managed Editor741/regressions прошли.
Общие reports/artifacts/raw output не становятся owned Core quota.
Далее CORE6d spans/metrics и engine producers/ENE6d IPC context; UI console/export,
App-private routing, bootstrap-before-pipeline capture, generator и измерения
allocations/throughput сохраняют самостоятельные задачи.

[План](PLAN.md), [текущий статус](STATUS.md), [общий статус](../STATUS.md).

## Структурированная диагностика: первый профиль — 2026-10-25

По запросу приступить к согласованным улучшениям реализован первый CORE6a–c профиль.
Own contracts DiagnosticLevel/Message/Record, lazy structured Write/IsEnabled,
frozen bounded snapshots, live source/event gates, per-sink levels, independent
bounded workers, memory ring, console/JSONL, health и bounded disposal с retry.
Legacy synchronous OnEvent/process-global subscription semantics сохранены.
[API, budgets и ownership](DIAGNOSTICS.md).

Core primitive ProcessOutputCapture читает borrowed TextReader независимо от
file writer, не ожидает newline, хранит bounded live tail и фиксирует file loss/
errors. UTF8 file limit учитывает bytes, а не characters. Source namespaces
соответствуют RootNamespace/пути; новых packages/providers/scopes нет.

| Поведение | Конкретная проверка |
| --- | --- |
| Lazy/live source/event/sink filters | SourceAndEventFilters_UseLongestPrefixSnapshotAndLiveChangesBeforeLazyProperties; SinkLevelFilters_AreIndependentSnapshotsAndDoNotFilterMemoryHistory |
| Immutable/clipped snapshots и Activity IDs | StructuredRecord_CopiesAndBoundsPropertiesMessagesAndExceptionsWithoutSplittingSurrogates; StructuredRecord_CapturesCollectorProcessUtcAndActivity |
| Queue count/bytes/in-flight и error reserve | BoundedSink_ReservesErrorSlotsAndCountsExecutingWrites; BoundedSink_RejectsByteOverflowAtExactBudgetAndRecoversAfterDrain |
| Slow/failing sink isolation | SlowAndFailingSinks_AreIndependentAndFailureDoesNotStopLaterWrites |
| Memory eviction/byte boundary и flush fence | MemoryRing_EnforcesExactByteBudgetAndRejectsOversizedRecord; FlushAsync_WaitsOnlyForWritesAcceptedBeforeItsCall |
| Shutdown/failed cleanup ownership | DisposeTimeout_ClosesAdmissionAndRetainsSinkOwnershipUntilRetry; SinkDisposeFailure_IsObservableAndRetryReleasesExactlyOnce |
| App setup/cleanup на Cobalt и Microsoft DI | RepeatedAppSetup_CreatesOnlyReplacementPipelineAndSink; AppDispose_DrainsStopAndServiceCleanupEventsBeforeDisposingSinks |
| JSONL ownership/rotation/reopen | JsonSink_ReopenShrinksOwnedSlotsAndKeepsOtherNamesAndPrefixes; JsonSink_ExclusiveOwnerRejectsConcurrentWriterAndReleasesLeaseOnDispose |
| Continuous raw capture/borrowed reader | TwoCaptures_ExposeLiveIndependentTailsBeforeEofAndBorrowReaders |
| UTF8/file failure не останавливает drainage | FileCapture_RespectsUtf8ByteBudgetAcrossSplitSurrogateChunks; FileFailure_IsObservableAndDoesNotStopReadingOrTailRetention |

Все новые assertions просмотрены, broad research/plan/status находятся в OS temp,
не в stageable repository content. Finding-tests provider отсутствовал (Tool
findTests not found); применён разрешённый source/search fallback и Roslyn pairing.
Во время narrow review обнаружен обход legacy Subscribe override при новом
predicate overload: legacy path возвращён,18existing diagnostics cases прошли.
Новых production fixes за пределами diagnostics/capture не заявлено.

Проверки Windows x64/net10.0/SDK10.0.402:

- Final focused Core80/80:43pipeline +19capture +18legacy; Aether7/7 отдельно.
- Full Core179/179 Debug и179/179 Release:62новых +117прежних cases.
- Full solution3725/3725 в каждой конфигурации, failed/skipped0,20TRX total.
- Nonincremental Debug build и Release build:0errors/1530warnings каждый.
- Current native consumer: Debug + published Release вне checkout, по6verbose
  children/stream bounds и launch failure; source/binary fingerprints сохранены
  в [общем evidence](../../TestResults/Diagnostics-20261025-1792916778832/verification.json).

Команды (каждая build/test/publish команда имела отдельный binlog):

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/Diagnostics-20261025-1792916778832/final-build-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/Diagnostics-20261025-1792916778832/final-build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/Diagnostics-20261025-1792916778832/Debug -bl:TestResults/Diagnostics-20261025-1792916778832/solution-tests-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/Diagnostics-20261025-1792916778832/Release -bl:TestResults/Diagnostics-20261025-1792916778832/solution-tests-Release.binlog
```

CORE6 в целом частичный: source-private App routing, bootstrap-before-resolver,
age/total directories retention, independent emergency output, operation spans/
metrics/IPC propagation и generator впереди. Fresh NuGet consumers, performance/
allocations, physical/golden/GPU loss/VRAM не повторены. Existing provider-specific
ownership и cooperative shutdown limits сохранены.

## Сквозной headless proof — 2026-10-15

Последняя часть согласованного Core quality прохода: configuration + diagnostics +
App lifecycle/Cobalt в настоящем console consumer. Production/runtime/contracts и
unit tests этим шагом не менялись. Расширены общие исходники CorePackageConsumer,
добавлен source project CoreHeadlessProbe и общий runner. Same sources используются
через ProjectReference и свежие независимые пакеты. Script не входит в solution;
сам source project собран и опубликован отдельно Debug/Release.

| Сценарий | Проверенные observables |
| --- | --- |
| normal | lifetime.Cancel → штатный stop/Dispose; точный trace |
| cancel | Original external token после cleanup; отдельный shutdown token |
| start-failure | Original StartError; reverse rollback только запущенных services |
| stop-failure | Original StopError; остановка остальных services и Dispose продолжаются |
| build-failure | Original BuildError, диагностическое payload identity; уже созданный ресурс закрыт, startup не начат |
| config-failure | Original ConfigurationError; регистрации/ресурсы/subscriptions не начинают работу |
| cleanup-failure, только Cobalt | AggregateException с двумя original StartError/CleanupError; остальные owned объекты освобождены |

Во всех runtime paths кроме config-failure JSON null преобразуется через
GetOrCreate в ConsumerOptions с BatchSize23, сохраняется в файл и повторно читается
с той же identity до refresh. Malformed JSON refresh сохраняет прежние sections;
после восстановления файла refresh работает, старший provider даёт Answer17.
Двойной UseDiagnostics не доставляет obsolete events и имеет одну subscription/
factory; throwing observer изолирован, framework/consumer events наблюдаемы.
После shutdown новые listeners не подписаны, запись в owned source запрещена.

OwnedFactory открывает FileStream/FileShare.None и пишет marker. Во время Start/
Stop/build-failure exclusive reopen отвергается, после cleanup внутри того же
процесса файл открывается эксклюзивно и marker читается. Счётчики собственных/
transient/borrowed objects проверяются независимо от файла. Для config-failure
файл не существует и opens/releases0; для остальных opens/releases1. Cobalt shared
Composite Dispose1, Microsoft DI alias calls сохраняют provider semantics.
Cleanup-failure специально ограничен Cobalt; native Microsoft DI Dispose-error
поведение не адаптировалось и не объявляется equivalent.

Предварительно: source Debug build0errors/0warnings;3Cobalt процессы normal/config-
failure/cleanup-failure прошли в TestResults/CoreHeadless/preflight-20261015-113857858.
Затем финальный единый run:

```powershell
pwsh -NoProfile -File ManualTests/CoreHeadlessProbe/RunHeadlessProof.ps1
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreHeadless/run-20261015-113928712/validation
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreHeadless/run-20261015-113928712/validation
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Все exit0; Windows x64/net10.0/SDK10.0.402/.NET10.0.13, xUnit2.9.3/VSTest.
Core107passed/0failed/0skipped в каждой Debug/Release,2TRX/214passed суммарно.
Full nonincremental Debug solution build0errors/1183warnings. Отдельных новых unit
tests нет; текущий suite является regression после свежей package сборки.

Source run: TestResults/CoreHeadless/run-20261015-113928712, Completed=true,
52processes (Cobalt28/Microsoft24), Debug/Release × build/publish;57commands
включая4build/publish и nested package runner. Source manifest130files.
Package run: TestResults/CorePackages/core-quality-20261015-113939900,
Completed=true; version0.2.2.7-alpha.headless.20261015113940134,4local nupkgs,
80matrix processes (Cobalt headless28 +direct4/Microsoft48) +3delivered README.
MS-only compiler opt-out проверен; negative opt-out при обязательной generation —
expected CS0246/RHeadlessConsumer. Package source manifest127files.

Пакеты восстановлены из собственного feed в изолированный cache с проверкой source
metadata/version/assets. Analyzer path/SHA и generated ModuleInitializer проверены;
runtime output не содержит analyzer/Roslyn/UI/IPC/units assemblies. Source и package
fingerprints повторно сверены до обоих Completed=true;57command logs,52report SHA
и package-summary SHA находятся в source summary, подробные package logs/reports
в nested summary. Старые64+3 runs относятся к прежней5-scenario матрице; текущая
80+3 включает реальные ресурсы и6/7сценариев, их нельзя смешивать с COB7 graph v1.
Benchmark graph v1/source-файлы не менялись, измерения этим шагом не повторены.

Финальный VerifyFinal.ps1/final-audit.json в source run:130/127source fingerprints,
mismatches0;52report SHA/package-summary SHA и3copies каждого consumer source
совпали. Из128App reports108 подтверждают file resource,20 — config failure без
ресурса,8 — Cobalt combined startup/cleanup failure. Все package App reports имеют
matching package informational version. Проверены2TRX,9документов/177local links/
94anchors, AST обоих runners и scoped git diff --check. Source/reference consumer
Debug/Release builds0warnings/0errors; это не zero-warning всего solution.

Согласованный Core quality проход закрыт в bounded scope: configuration/bootstrap,
diagnostics и source/package App proof. Нет подтверждённых новых Core блокеров
для следующего модуля; это не general release. Сохраняются cooperative shutdown30s,
process-global prefix diagnostics/in-flight callback limit, JSON concurrency/
crash-safe writes/permission/disk-full limitations, warnings и непроверенные
OS/RID/trimming/AOT modes. Не выполнялись собственные Cobalt/generator/Enerit/
Mithril suites, IPC/units, UI/native и performance; library→app/legacy delivery
controls отдельного RunDeliveryChecks также не повторялись.
Далее Enerit quality review/ENE5 baseline; движок ждёт сводки остальных модулей.

## Diagnostics quality — 2026-10-15

Продолжение собственного Core quality после configuration/bootstrap. Исправлены
DiagnosticObserver и UseDiagnostics registration; existing DiagnosticsTests
расширен7methods/10cases. App/service/diagnostics interfaces и dependencies прежние.

Подтверждены две ошибки: dictionary держал completed DiagnosticListener до Dispose
общей подписки; repeated UseDiagnostics оставлял прежние descriptors, и ResolveAll
создавал старые subscription/factory вопреки выбранной последней конфигурации.
Original red:3failed/0passed/0skipped (GC ownership1, повторный setup2providers).
Baseline8cases до изменений прошёл. Новые observers удаляют завершённый source по
identity и освобождают его handle. Reserve выполняется до Subscribe, сам Subscribe
и освобождение handles — вне общего lock; поздний handle после completion/Dispose
отсоединяется. Already-disposed listener с пустым handle также удаляется.

Поведение Subscribe для already-disposed source сверено с
[официальным исходником runtime10.0.0](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticListener.cs):
подписка может вернуться без OnCompleted. Текущая проверка воспроизводит это на
установленном runtime через полностью созданный DiagnosticListener subclass,
закрывающийся до/после base.Subscribe; отдельно считает Dispose возвращённого handle.
Первоначальный стенд с Dispose прямо из AllListeners.OnNext смешивал этот контракт
с глобальным списком во время конструктора источника и заменён. Intermediate
edge-cases log/TRX содержит также исправленную ошибку filter prefix тестового
multi-App event; это не final passing evidence и не новый внешний блокер.

Повторный UseDiagnostics удаляет прежние subscription/factory registrations до
добавления новых, сохраняя last-call-wins settings. Prefix observation остаётся
process-global; тесты нескольких Apps доказывают независимость cleanup, а не
приватность событий. Factory по-прежнему владеет своими sources до Dispose.

| Требование текущего этапа | Exact test name в TrueMoon.Tests.DiagnosticsTests |
| --- | --- |
| Завершённые источники не удерживаются живой подпиской, same-name survivor/replacement продолжают доставку | CompletedSourcesAreReleasedWhileSubscriptionRemainsAlive |
| Completion до возврата Subscribe и already-disposed source; exactly-once handle Dispose;2cases | SourceCompletingDuringSubscribeIsReleased |
| Повторная настройка оставляет одну subscription/factory, старые callbacks не возвращаются;2providers | RepeatedDiagnosticsSetupKeepsOnlyLastRegistration |
| Закрытие одной App не мешает другой; собственные sources закрываются, future sources наблюдаются;2providers | DisposingOneAppKeepsOtherAppDiagnosticsActive |
| Новые filters применяются только при discovery | NewFiltersOnlyApplyToSourcesDiscoveredAfterTheChange |
| Dispose из callback отключает оставшиеся callbacks и будущие sources | DisposingSubscriptionInsideCallbackStopsRemainingCallbacks |
| Параллельный discovery/completion/Dispose не оставляет активных subscriptions;128sources | ConcurrentSourceDiscoveryCompletionAndSubscriptionDisposalDetachAllSources |

Применены dotnet-best-practices/code-testing-agent/finding-tests/run-tests.
IDE execute_tool вызван: findTests отсутствует. Для двух production targets
research/plan/status созданы вне Git в OS temp, workflow выполнен inline без
доступного специализированного generator agent. Roslyn find-untested-sources
выполнен один раз на bounded snapshot7diagnostic source files/1existing test file:
paired4/unpaired3. Subscription/Configuration/Source/Factory paired с DiagnosticsTests;
AppCreationContextExtensions/internal Observer/LoggingObserver statically unpaired
(extension/indirect caller не содержит declaring-type token). Это static pairing,
не execution coverage; existing public tests расширены без дублирования suite.

Assertion-quality и test-gap-analysis применены при финальном review: у7newmethods
нет assertion-free/trivial-only cases; проверены collection contents, точные counts,
identity, inactive listeners, error guards и memory ownership. Добавлен независимый
handle Dispose counter: GC сам по себе не доказывает освобождение handle внутри
неукоренённого цикла. NoInlining helper/KeepAlive исключают локальный JIT root и
раннее исчезновение владельца. Original defects killed эмпирическим red run;
оставшиеся candidate mutations оценены по source/assertions, не выполнены.
Bounded concurrency test не объявляет exhaustive race/stress доказательство.

Команды из src; все final commands exit0:

```powershell
# Baseline до изменений:8passed/0failed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests --logger 'trx;LogFileName=baseline.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# До production fixes: exit1,3failed/0passed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter 'FullyQualifiedName~CompletedSourcesAreReleasedWhileSubscriptionRemainsAlive|FullyQualifiedName~RepeatedDiagnosticsSetupKeepsOnlyLastRegistration' --logger 'trx;LogFileName=red.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Final narrow:18passed/0failed/0skipped.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests --logger 'trx;LogFileName=diagnostics-debug.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Полный Core:107passed/0failed/0skipped в каждой конфигурации.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreQuality/diagnostics-20261015
# Solution compile:0errors/1183warnings.
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest. Final2CoreTRX214passed,
Failed0/Skipped0; diagnostics18 в каждой конфигурации входят в107, отдельный18-case
Debug TRX не суммируется в214. Артефакты/логи в ignored
TestResults/CoreQuality/diagnostics-20261015. Source manifests старых package или
benchmark runs после Core изменений не считаются current. Другие module suites,
package consumers/IPC/units/UI/native/performance не повторены.

Diagnostics stage закрыт в указанном scope. Следующий этап: цельный headless proof
configuration/diagnostics/Cobalt, normal/cancel/startup failure/shutdown/cleanup,
затем сводка ограничений Core перед Enerit. Callback quiescence, process-specific
event routing и новый factory release API не добавлены.

## Configuration и bootstrap quality — 2026-10-15

Первый собственный Core quality шаг после COB7. Прочитаны общие/local планы,
configuration/contracts, AppBuilder и diagnostics; production fix ограничен
ConfigurationExtensions.GetOrCreate, existing ConfigurationTests расширен.
Новых API/providers/dependencies, смены DI provider и engine кода нет.

Подтверждён defect: unqualified GetOrCreate при existing null в именованной
секции записывал объект в default; старший null продолжал маскировать результат,
следующий вызов возвращал другой объект. Red run:4cases,1failed/3passed/0skipped,
exit1. Default/named variants уже проходили; исправлен именно источник записи
при unqualified lookup. Теперь null replacement идёт в найденную секцию,
missing unqualified — по-прежнему default, invalid вызывает FormatException.
JSON null replacement сохраняется на диск, повторное чтение не пишет файл.

Focused tests в existing suite, без создания нового test project или broad
coverage pipeline. Применены dotnet-best-practices, code-testing-agent,
finding-tests и run-tests; IDE findTests/execute_tool недоступен, использованы
существующие ConfigurationTests/AppLifecycleTests. Repo overlay run-tests отсутствует.
Сохранены repo xUnit/собственные DI contracts вместо generic skill MSTest/MS DI.

| Поведение | Exact test name в TrueMoon.Tests.ConfigurationTests |
| --- | --- |
| Null replacement, implicit type key, priority и identity;4variants | GetOrCreateReplacesWinningNullAndRetainsIdentity |
| Обычный unqualified Set пишет default | UnqualifiedSetKeepsDefaultTargetWhenNamedSectionMasksTheKey |
| Invalid blocking value не заменяется и не пропускается;2variants | GetOrCreateRejectsInvalidWinningValueWithoutChangingProviders |
| JSON persistence, repeated read без записи, нижний provider не изменён | GetOrCreatePersistsWinningJsonNullWithoutWritingDefault |
| Configuration actions раньше registrations/factory; тот же IConfiguration в App/DI;2providers | BootstrapConfiguresValuesBeforeRegistrationsAndServiceCreation |
| Configuration validation error identity; registrations/factory не вызваны;2providers | BootstrapConfigurationFailurePreservesCauseAndSkipsRegistrations |
| Ни один provider не обновляется при failure, recovery и старые handles;sync/async | FailedRefreshPreservesAllProvidersAndAllowsRecovery |
| Serialization failure сохраняет file/snapshot, retry пишет | JsonSerializationFailurePreservesFileAndSnapshotAndAllowsRetry |
| Sharing violation сохраняет file/snapshot, retry после release handle | JsonWriteFailurePreservesSnapshotAndAllowsRetry |

Добавлено16cases, ConfigurationTests11→27, Core81→97. Review assertions проверил
identity/нижний provider/disk state/полный порядок callbacks/original exception
и recovery; production change имеет failing-before/passing-after свидетельство.
Concurrency гарантия GetOrCreate не добавлена; file errors не подавляются.

Команды из src (stdout/stderr перенаправлялись в ignored TestResults):

```powershell
# До production fix: exit1, failed1/passed3/skipped0.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --filter FullyQualifiedName~GetOrCreateReplacesWinningNullAndRetainsIdentity --logger 'trx;LogFileName=red.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Финальный узкий run: exit0, passed27/failed0/skipped0.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~TrueMoon.Tests.ConfigurationTests --logger 'trx;LogFileName=configuration-debug.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Финальные полные Core runs: exit0, passed97/failed0/skipped0 каждый.
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=core-debug.trx' --results-directory TestResults/CoreQuality/configuration-20261015
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=core-release.trx' --results-directory TestResults/CoreQuality/configuration-20261015
# Full solution compile: exit0, errors0/warnings1184.
dotnet build TrueMoon.slnx -c Debug --no-incremental --no-restore --verbosity quiet
```

Windows x64/net10.0/SDK10.0.402/xUnit2.9.3/VSTest. Два final Core TRX194passed,
Failed0/Skipped0; узкий27-case TRX отдельно. Full build выполнен до добавления
последнего JSON GetOrCreate test; финальный test source затем собран обоими Core
test runs. Production code после full build не менялся. Logs:
TestResults/core-quality-{red,configuration,debug,release,build}.log;
TRX в TestResults/CoreQuality/configuration-20261015. Red и initial24-case
configuration run предшествуют final27-case; intermediate96-case runs заменены
final97-case reports. Restore/build не подменяют test discovery.

Package/IPC/units/UI/native/performance и другие module suites не повторялись.
Общий build с warnings не доказывает их runtime готовность. Disk-full/permission
injection, process-shared JSON/atomic file replacement и concurrent GetOrCreate
не проверены. Собственный diagnostics quality и цельный новый headless proof
остаются следующими частями Core; текущие8 diagnostics tests — regression.

## Regression при Cobalt COB7 — 2026-10-15

Core/Contracts production/test без изменений; Core81/81 Release, Failed0/Skipped0.
Full nonincremental Release solution build exit0/errors0/warnings1185.
TrueMoon App graph проверен на Cobalt/MS в normal lifetime/external cancellation/
startup rollback/error recovery/cleanup paths. Exact source package consumer:
12processes/120scenarios Debug/Release build/publish, включая MS analyzer opt-out;
standard package64+3 иlibrary/app4/2constructor controls также прошли.
[Результаты/решение](../cobalt/COMPARISON.md), [команды](../cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15).
Debug unit suites/IPC/units/UI/native не повторены. Migration отложена, существующий
Cobalt default/DI adapter сохранены. Далее собственный Core quality review; этот
comparison не закрывает configuration/diagnostics/Core utility criteria.

## Regression при Cobalt COB5 — 2026-10-15

Core/Contracts production/test code без изменений. Core81/81 Release, Failed0/
Skipped0; Debug suites не повторены. Fixed headless App graph использует настоящий
App.Build/Start/Stop/Dispose, workload guard проверяет lifecycle1/1 и cleanup.
Startup/resolution/allocations измерены в Cobalt COB5, без изменения bootstrap.
Full nonincremental Release solution build exit0/errors0/warnings1185.
[Команды/3TRX/351passed и baseline scope](../cobalt/HISTORY.md#cob5-app-baseline--2026-10-15).
Package consumer/IPC/units/UI/native не повторены; прежние COB6d package/Debug
результаты не являются текущим run. Core quality roadmap сохраняется отдельно.

## Regression при Cobalt COB6d — 2026-10-15

Core production/test и Contracts code без изменений. Core81/81 в каждой
Debug/Release, Failed0/Skipped0. Fresh direct/headless/provider package64/64 +3actual
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1185.
[Команды,6TRX/702passed и module library/app consumer](../cobalt/HISTORY.md#cob6d-registration-workflow--2026-10-15).
Собственный Core quality scope не считается завершённым этой регрессией; other
module suites, UI/native/performance не повторены.

## Regression при Cobalt COB6c — 2026-10-15

Core production/test и Contracts code без изменений. Core81/81 в каждой
Debug/Release, Failed0/Skipped0. Fresh direct/headless/provider package64/64 +3actual
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1185.
[Команды,6TRX/644passed и independent registration consumer](../cobalt/HISTORY.md#cob6c-registration-contracts--2026-10-15).
Собственный Core quality scope не считается завершённым этой регрессией; other
module suites, UI/native/performance не повторены.

## Regression при Cobalt COB6b — 2026-10-14

Core production/test и Contracts не менялись. Core81/81 в каждой Debug/Release,
Failed0/Skipped0. Creation/rollback/failure identity regressions прошли при новом
Cobalt runtime guard/metadata. Fresh headless/direct/provider package64/64 +3exact
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1187.
[Команды,6TRX/454passed и independent error consumer](../cobalt/HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Собственный Core quality scope этой проверкой не завершается; UI/native/performance
и остальные module suites не повторены.

## Regression при Cobalt COB6a — 2026-10-14

Core production/test code без изменений. Core81/81 в каждой Debug/Release,
Failed0/Skipped0. Fresh bundled package direct/headless/provider64/64 +3delivered
README examples, Debug/Release build/publish; opt-out expected CS0246.
Full nonincremental Debug solution build exit0/errors0/warnings1191.
Собственная Core quality программа не завершена этой регрессией; UI/native/
performance и прочие module suites не повторены.
[Команды,6TRX/360passed и package scope](../cobalt/HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).

## Package regression с bundled Cobalt — 2026-10-14

TrueMoon package README/consumer references обновлены под bundled analyzer.
Fresh64headless cases +3точных delivered examples прошли Debug/Release build/publish,
negative generation control expected CS0246. Core81/81 отдельно Debug/Release,
Failed0/Skipped0; source Core logic/tests без изменений. [Команды/границы](../cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).

## Regression при начале качества Cobalt — 2026-10-14

Core production/test code не менялся. С изменённым Cobalt generator выполнен
unfiltered TrueMoon.Tests Debug/Release:81/81 в каждой configuration,
Failed0/Skipped0, commands exit0, Windows x64/net10.0/SDK10.0.402.
[Команды/артефакты/ограничения](../cobalt/HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).
Core package/headless consumers и другие module suites этим regression не повторены.

## Ревью ядра и CORE0 baseline — 2026-10-10

Запрос: посмотреть ядро TrueMoon и предложить улучшения. Прочитаны общие и локальные планы, bootstrap/lifecycle, configuration, diagnostics, Contracts и Core utilities. Cobalt runtime/generator и Microsoft DI adapter прочитаны в части app integration. Production код и тесты не изменялись; предложения ниже не являются принятыми изменениями PLAN.

### Фактически выполненные проверки

Windows, SDK **10.0.401**, Debug/net10.0, xUnit **2.9.3**, VSTest. `global.json`: 10.0.100 с `rollForward: latestMinor`. Overlay `src/.agents/skill-overlays/dotnet-test/run-tests.md` отсутствует.

```powershell
dotnet --version
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --framework net10.0 --logger 'trx;LogFileName=core-baseline.trx' --results-directory TestResults/CoreReview-20261010 --verbosity quiet
dotnet build TrueMoon.Core/TrueMoon.Core.csproj --configuration Debug --framework net10.0 --verbosity quiet
```

- TrueMoon.Tests: exit **1**, discovered **4**, **Passed: 3, Failed: 1, Skipped: 0**. Restore/build позволили выполнить тесты. Исторический AppCreate failure воспроизведён на текущем checkout.
- TrueMoon.Core: build exit **0**, errors **0**. Сборка incremental; runtime проверки scheduler/buffers не выполнялись. Этот проект не входит в зависимости TrueMoon.Tests.
- В логах есть NU1506 (повторные Roslyn PackageVersion), NU1507 (несколько package sources без mapping), в тестовом build — CS8602 в AppTests. Package настройки не исправлялись.
- Артефакты ignored: `TestResults/CoreReview-20261010/core-baseline.trx`, `TestResults/CoreReview-20261010/core-build.log`, `TestResults-CoreReview-20261010.log`.
- Cobalt.Tests/Cobalt.Generator.Tests, отдельный adapter scenario, Enerit/Mithril/UI suites, общий solution, Release/package consumer не запускались. Их исторические passing результаты сюда не переносятся.

### Воспроизведённый bootstrap/DI отказ

`TrueMoon.Tests.AppTests.AppCreate` падает с `ServiceResolvingException<IAppLifetime>: Could not resolve service for type: TrueMoon.IAppLifetime`.

Стек: `DefaultApp.StartAsync` → `ResolveAll<IStartable>` → `CobaltServiceResolverBase.ResolveEnumerable` → generated `LifeTimeExecutorResolver.Resolve` → `Resolve<IAppLifetime>`. В AppCreate зарегистрированы CommonStartableService и CommonService1; LifeTimeExecutor зарегистрирован в других сценариях того же test assembly.

По исходникам [CobaltServiceResolverBuilder](../../TrueMoon.Cobalt/CobaltServiceResolverBuilder.cs) выбирает все generated factories по одному ServiceType, не фильтруя выбранную ImplementationType/lifetime runtime handle. [ServiceResolvers](../../TrueMoon.Cobalt/ServiceResolvers.cs) хранит factories глобально, generator регистрирует их через ModuleInitializer. Поэтому runtime graph получает чужую implementation из той же сборки. Это не доказательство зависимости от порядка тестов: generated registrations общие уже при загрузке assembly.

Дополнительно по коду [generator](../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs): Composite aliases получают FactoryCode, но GenerateTypeResolver не использует его при создании singleton. Alias resolver может возвращать незаполненное `_instance`. Требуется проверка единого экземпляра IAppLifetime/IAppLifetimeHandler. Владение resolver/generator fixes — COB1/COB3; интеграционный сценарий — CORE3.

### Приоритеты улучшения

**P0 — наблюдаемый lifecycle и cleanup (CORE1).** [App.RunAsync](../../TrueMoon/App.cs) ловит Exception и только отправляет diagnostic event; caller может получить successful Task после startup failure. [AppExtensions.RunAsync](../../TrueMoon/AppExtensions.cs) выполняет тот же сценарий без такого catch. В обоих путях отмена/ошибка StartAsync или WaitAsync обходит StopAsync/Stopping/Stopped; await using запускает disposal, но это не эквивалент graceful stop. StopAsync получает тот же run token, который при внешней отмене уже отменён. DefaultApp.StopAsync также подавляет ошибки отдельных сервисов.

Предложение: один runner для обоих entry points; явно определить cancellation outcome, startup rollback и shutdown errors; использовать независимый ограниченный shutdown token; сохранять первичную причину при cleanup failure. Учитывать реально запущенные сервисы, останавливать их в обратном порядке; определить repeat/concurrent Start/Stop/Dispose. Это предложения к contract review, текущим PLAN эти semantics ещё не утверждены.

**P0 — изоляция graph и lifetime aliases (CORE3 ↔ COB1/COB3).** Factory registry должен давать способ создания выбранной runtime registration, включая implementation и lifetime. Generated resolver не должен автоматически включать сервис во все app graphs. Composite должен разрешать один instance через несколько interfaces. Cobalt и Microsoft DI нужно проверить на одинаковом небольшом app scenario.

**P1 — scheduler shutdown (CORE4).** [TmTaskScheduler](../../TrueMoon.Core/Threading/TmTaskScheduler.cs) в Dispose завершает writer, но ThreadLoop выходит только по внешнему cancellation token. Enerit consumers создают scheduler без такого token: idle worker продолжает цикл после dispose. QueueTask после завершения writer бесконечно повторяет TryWrite. TryExecuteTaskInline допускает исполнение на вызывающем потоке, поэтому отдельный STA worker не гарантирует affinity. Предложение: ожидание очереди с учётом completion, явный drain/cancel contract, отказ от новых задач после stop и управляемое завершение workers. Inline/affinity определить по требованиям consumers; не превращать общий scheduler в UI dispatcher без отдельного решения.

**P1 — владение буферами (CORE4).** [ArrayPoolBufferWriter.Dispose](../../TrueMoon.Core/ArrayPoolBufferWriter.cs) каждый раз возвращает тот же массив; методы продолжают обращаться к нему после возврата. Предложение: идемпотентный dispose, ObjectDisposedException при последующем использовании, очистка references при возврате и копирование только written region при росте. [MemoryPoolUtils](../../TrueMoon.Core/MemoryPoolUtils.cs) возвращает backing array любого array-backed Memory, не доказывая происхождение/владение, включая slices. Для нового API предпочтителен owner/lease с однократным Dispose; существующие Enerit consumers мигрировать отдельно. [.NET ArrayPool.Return](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.return?view=net-10.0) требует возврата rented buffer ровно один раз и прекращения использования после возврата.

**P1 — process и diagnostic subscriptions (CORE1/CORE2).** [DefaultApp](../../TrueMoon/DefaultApp.cs) подписывает анонимный Console.CancelKeyPress handler и не отписывается: static event удерживает app. Handler не устанавливает args.Cancel; для управляемого Ctrl+C shutdown это необходимо по [Console.CancelKeyPress contract](https://learn.microsoft.com/en-us/dotnet/api/system.console.cancelkeypress?view=net-10.0). Предложение: выбираемый console lifetime, хранение/освобождение delegate и явное поведение нескольких app instances.

[DiagnosticObserver](../../TrueMoon/Diagnostics/DiagnosticObserver.cs) теряет IDisposable от каждой value.Subscribe. DiagnosticSubscription освобождает только AllListeners subscription; EventsSource не освобождает собственный DiagnosticListener. Предложение: app-owned набор всех handles с cleanup при shutdown/build failure. LoggingObserver вызывает listeners без изоляции исключений: diagnostic listener способен прервать app operation. Определить error policy для callbacks.

**P1 — configuration APIs (CORE2).** [ConfigurationBuilder](../../TrueMoon/Configuration/ConfigurationBuilder.cs) игнорирует AddProvider/RemoveProvider и создаёт фиксированный набор. [JsonConfigurationSection](../../TrueMoon/Configuration/JsonConfigurationSection.cs) бросает NotImplementedException для всех операций чтения/записи. Предложение: реализовать минимальный заявленный набор providers либо явно ограничить доступный API; определить precedence и invalid-value errors до расширения.

Текущие способы доступа расходятся: ConfigurationExtensions.Get без section просматривает все секции; indexer/Exist/GetName обращаются к default; CommonConfiguration.GetSection берёт первую секцию с данным именем. Args/environment хранят строки, ConfigurableBase.Get<int> не преобразует их. Refresh обновляет список секций, но args/environment snapshot получен в constructor. Дополнительные дефекты по коду: Split('=') теряет хвост `token=a=b`; PathResolver передаёт в GetFullPath весь `-root=...`, а GetInfo возвращает processName вместо вычисленного productName; GetOrCreate с default key читает typeof(T).Name, но при создании передаёт null в Set. Отдельные поведенческие проверки этих случаев не запускались.

**P2 — API и зависимости после исправления поведения.** Сохранить разделение Contracts/bootstrap/utilities и отсутствие UI зависимости в Core. Выбор resolver уже есть через App.Builder/UseDI; перед перемещением Cobalt default в отдельный composition layer проверить package consumers. Для обычного hosting можно отдельно оценить adapter к [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host), который объединяет lifecycle, DI, configuration и logging. Это возможная интеграция, не решение заменить TrueMoon или добавить новую зависимость сейчас. Typed configurator вместо reflection Configure и XML docs полезны после фиксации contracts. Startup/allocations оптимизировать после измерений.

### Обзор четырёх существующих тестов

Использованы run-tests и test-anti-patterns с .NET extension; [AppTests](../../TrueMoon.Tests/AppTests.cs) не переписывались. Class-level fixtures/resources отсутствуют.

| Тест | Oracle и текущий результат | Оценка |
| --- | --- | --- |
| AppCreate | Тип CommonStartableService и IsStarted; Failed до assertions на resolution | Полезный smoke с проверяемым состоянием; отсутствие Stop assertion — отдельный пробел |
| Create | App not null, Name not empty; Passed | Корректный ограниченный smoke; не проверяет precedence/конкретное имя |
| RunAsync | await вызова и Assert.True(true); Passed | T1: Always-true assertion, не проверяет lifecycle outcome |
| RunAsyncWithConfigurator | await вызова и Assert.True(true); Passed | Тот же T1, отдельный reflection entry point |

Findings: **1 Critical (T1, два теста)**, **1 High (T2, общий helper двух тестов)**; Medium/Low findings не выделялись.

- **T1:** заменить константный assert наблюдаемым результатом runner. Предлагаемый oracle после принятия CORE1: trace строго `Start → Stopping → Stop → Stopped → Dispose`, каждое событие один раз; при failure сохранить исходный exception instance, остановить только реально запущенные сервисы, подтвердить disposal. Это будущие критерии, а не результаты текущего запуска.
- **T2:** LifeTimeExecutor.StartAsync запускает неотслеживаемый Task.Run с Task.Delay(500) и lifetime.Cancel. Нельзя дождаться фоновой операции и проверить её ошибку. Заменить fixed-delay coordination управляемым сигналом старта/отмены, хранить и await завершение helper task; проверять ровно один cancellation request. Длительность suite около100ms не доказывает, что scheduled task успела вызвать Cancel.
- Положительные наблюдения: app operations awaited; using/await using присутствуют; AppCreate проверяет тип и observable IsStarted; Create действительно проверяет создание/имя.
- Смежные непроверенные сценарии: startup rollback, external cancellation, stop failure, repeat/concurrent lifecycle, disposal/subscription ownership, оба DI providers. Coverage не собирался; отсутствие dedicated тестов не выдаётся за измеренный процент покрытия.

### Следующая конкретная работа

Сначала устранить загрязнение runtime graph и Composite alias resolution в COB1/COB3, сохранив AppCreate как regression proof. В CORE1 определить наблюдаемый runner/error/cancellation/rollback contract и заменить T1/T2 проверками принятого поведения. Затем закрыть scheduler/pool ownership по CORE4 и providers/diagnostic subscriptions по CORE2. Архитектурное расширение до этих шагов увеличит поверхность неподтверждённого поведения.

## Исправление проблем ядра — 2026-10-10

Запрос: «давай починим все проблемы» из предыдущего ревью. Область: lifecycle/bootstrap, Cobalt/generator/Microsoft DI adapter, configuration/diagnostics, scheduler/pools и необходимые Enerit consumers. Исходники поверх HEAD `9d7090a`, изменения не закоммичены. Существующие пользовательские UI изменения сохранены. Optional Generic Host adapter, новый typed-configurator API и переезд архитектуры не вводились.

### Реализация

- App и IApp.RunAsync используют один AppRunner. Ошибки больше не маскируются; startup/cleanup exceptions сохраняются, rollback/normal shutdown выполняются в reverse order. Shutdown token отдельный, timeout30s кооперативный. DefaultApp сериализует операции, повторные calls идемпотентны, restart запрещён; owned provider освобождается один раз, concurrent и recursive disposal не ждут сами себя.
- Console handler устанавливается только на работающем lifecycle, ставит Cancel=true и снимается после stop/failure/dispose. DefaultAppLifetime выполняет snapshots callbacks вне lock, завершает wait асинхронно, освобождает owned CTS и сохраняет borrowed. Последний review обнаружил Cancel/Dispose race: CTS теперь освобождается после активных callbacks; external borrowed source cancellation освобождает waiters.
- Configuration providers действительно add/remove; priority, reads и conversion согласованы. JSON sections читаются/сохраняются, failed refresh сохраняет snapshot. Исправлены args equals-tail/executable exclusion, root path/product metadata и default GetOrCreate key. Diagnostic subscriptions/factory созданы лениво и принадлежат resolver; individual handles и sources освобождаются, observers не ломают app.
- Scheduler больше не spin-wait, закрывает приём/drain и завершает workers. Writer защищает Dispose/use-after и copy только written region. Memory rentals имеют отдельного manager-owner, проверяют whole-region identity, отвергают foreign/sliced/double/stale return; добавлен IMemoryOwner API. Array extraction compatibility и обязательное прекращение работы с ранее полученными spans/pins описаны в PLAN.
- Cobalt exact registration metadata, aliases/generics, singleton/thread-safety и owned/borrowed disposal исправлены; подробности принадлежат [его HISTORY](../cobalt/HISTORY.md). Microsoft DI wrapper владеет provider, но стандартный многократный disposal disposable aliases сохраняется.
- Enerit переведён на tracked rental, собственный scheduler/connection cleanup и async handler drain. Partial header/EOF и pending-response failures исправлены без изменения framing; [его20 новых regression cases](../enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10).
- В Directory.Packages.props удалены lower-version дубликаты Roslyn CSharp/Analyzers: effective5.9.0 сохранена. Repository NuGet.Config задаёт один публичный source и mapping, не меняя global user config. После исправления NU1506/NU1507 в полном build отсутствуют. Проверка выполнена с .NET best practices, run-tests, code-testing-agent, find-untested-sources, assertion-quality/test-gap-analysis и msbuild-antipatterns skills.

### Итоговая валидация

Windows, Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Сначала direct Core run78/78 и Cobalt37/37+generator6/6; затем окончательные команды после всех правок:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --logger trx --results-directory TestResults/CoreFixes-Final-20261010 --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --list-tests --verbosity quiet
```

Все три команды exit0. Build: **0errors/1271warnings**, не warning-free. Tests: **643passed/0failed/0skipped**. TRX counters сверены по проектам:

| Проект | Discovered/Total | Passed | Failed | Skipped |
| --- | --- | --- | --- | --- |
| TrueMoon.Tests | 81 | 81 | 0 | 0 |
| TrueMoon.Cobalt.Tests | 37 | 37 | 0 | 0 |
| TrueMoon.Cobalt.Generator.Tests | 6 | 6 | 0 | 0 |
| TrueMoon.Enerit.Tests | 30 | 30 | 0 | 0 |
| TrueMoon.Enerit.Generator.Tests | 9 | 9 | 0 | 0 |
| TrueMoon.Alloy.Tests | 480 | 480 | 0 | 0 |
| TrueMoon.Mithril.Tests | 0 | 0 | 0 | 0 |

Mithril zero discovery — отсутствие executable тестов, а не successful lifecycle suite. Alloy480 проверяет unit scenarios на уже существующих UI изменениях; physical/native/soak результаты этим run не меняются. Core81 распределены: AppTests5, AppLifecycle23, AppLifetime12, Configuration11, Diagnostics8, CoreUtility22.

Ignored logs: `TestResults-CoreFixes-solution-build-final-20261010.log`, `TestResults-CoreFixes-solution-tests-final-20261010.log`, `TestResults-CoreFixes-discovery-final-20261010.log`. TRX: `TestResults/CoreFixes-Final-20261010/`, семь файлов с timestamps16:58:56–16:58:58 local. Intermediate failing compile/assertion attempts сохранены отдельно и не считаются final passing evidence: исправлены unconstrained nullable override, synchronous Throws overloads и exact JsonException assertion для derived JsonReaderException.

### Requirement → Evidence

| Requirement | Evidence |
| --- | --- |
| «давай починим все проблемы» — L1 runner/rollback/error/cancellation | `AppLifecycleTests.StartupFailure_RollsBackReverseOrderAndPreservesOriginalError`, `StartupAndCleanupFailures_PreserveEveryErrorAndContinueCleanup`, `ExternalCancellation_StopsWithIndependentTokenAndDisposes`, `InstanceRun_UsesSameShutdownOrderAsBuilderRun` — exact trace, original exception identity и dispose count на обоих providers |
| «давай починим все проблемы» — L2 repeat/subscription/lifetime ownership | `ConcurrentStartStopDispose_AreIdempotentAndRestartIsRejected`, `StoppedAndDisposedApplication_IsCollectibleAfterConsoleUnsubscription`, `AppLifetimeTests.Cancel_DoesNotRunWaitContinuationsInline`, `ConcurrentDispose_DefersSourceDisposalUntilCancellationCallbacksFinish`, `BorrowedSourceCancellation_ReleasesLifetimeWaiters` |
| «давай починим все проблемы» — D1 graph/aliases/containers | `AppTests.AppCreate`, `ResolverRegressionTests.RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution`, `Composite_AllAliasesShareOneOwnedInstance`, `TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix`; полный Cobalt matrix в его HISTORY |
| «давай починим все проблемы» — C1 providers/JSON/paths/conversion | `ConfigurationTests.BuilderAddsProvidersByPriorityAndRemovesByIdentityOrType`, `NamedAndUnqualifiedReadsUseTheSameHighestExistingKey`, `ValuesConvertUsingInvariantCultureAndPreserveMissingNullAndInvalidDistinctions`, `JsonInvalidFilesSurfaceErrorsAndFailedRefreshKeepsLastValidSnapshot`, `PathResolverParsesExactRootArgumentAndUsesProductMetadata` |
| «давай починим все проблемы» — G1 diagnostics | `DiagnosticsTests.DisposingSubscriptionDetachesExistingAndFutureSources`, `ListenerExceptionsDoNotStopDeliveryAndSubscriptionsRespectFilters`, `AppCreationFailureDisposesResolverOwnedSubscriptions`, `AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows` |
| «давай починим все проблемы» — U1 scheduler | `CoreUtilityTests.SchedulerDisposeDrainsAcceptedWorkAndTerminatesWorkers`, `SchedulerCancellationDrainsQueueAndRejectsNewTasks`, `SchedulerExposesPendingTasksAndRefusesForeignInlineExecution`, `SchedulerAllowsWorkerInlineExecutionAndSelfDisposalWithoutDeadlock` |
| «давай починим все проблемы» — U2 writer/rental/consumer ownership | `BufferWriterGrowsPreservingOnlyWrittenRegionAndReturnsOldBuffer`, `BufferWriterDisposeReturnsOnceClearsReferencesAndGuardsEveryMember`, `MemoryRentalRejectsForeignAndSlicedMemoryWithoutReturningWholeRental`, `MemoryOwnerDisposalAndReturnShareOwnershipAndStaleRentalCannotReturnNewLease`, `MemoryConcurrentReturnsHaveExactlyOneWinner`; Enerit `UtilityLifetimeTests.ConnectionDisposeWaitsAsyncHandlersAndReturnsTheirRequestMemory` |

### Assertion и static gap review

Новые/переписанные тесты перечитаны против исходников: exact lifecycle order и single disposal, отсутствие rollback незапущенных сервисов, original error identity/aggregate contents, live independent shutdown token, отсутствие retained console roots; configuration exact winners/shadowing/invalid/missing, реальная persisted JSON запись и snapshot retention; subscription delivery после individual cleanup и observer failure; scheduler accepted-work drain/foreign inline/worker identity; writer sentinel отличает copy-written от copy-capacity; rental stale lease не может вернуть новую rental. Старые Assert.True(true) удалены, delayed fire-and-forget helper заменён observable synchronous lifetime cancellation.

В bounded новых regression files нет assertion-free/always-true/self-field tests; identity assertions сравнивают независимо разрешённые объекты/известные исключения. Assertions охватывают equality, state/side effects, collections/deep sequence, negative, type/identity и errors. TCS/manual barriers вместо arbitrary sleep; timeout guards ограничивают зависший тест. Static pseudo-mutations remove-cleanup/error propagation/priority/ownership/queue close/EOF checks имеют наблюдаемый oracle. Mutations не выполнялись, empirical mutation score и line/branch coverage не заявляются.

Ограничения: cooperative user Task shutdown, standard Microsoft DI alias/error cleanup, disk/permission fault injection, multi-process JSON writes, pointer-level pinning/active-pin return behavior, independent package consumers и Release. Полный IPC/units/native UI matrix принадлежит другим этапам; исправление известных Core дефектов не закрывает G3/G4/G6.

Финальная документальная сверка:13 изменённых planning/status/history документов,113 local links, missing files/anchors0. `git -c core.safecrlf=false diff --check` exit0; удалена лишняя пустая строка JSON section EOF без semantic changes. Scratch research/plan/status/pairing files отсутствуют в working-tree change list. Root assertion inventory:65 named methods/320 static Assert call sites/0 assertion-free в6 Core test files; theory/loop executions не входят в эту метрику.

## CORE5 / COB4 headless package consumer — 2026-10-11

Реализован [воспроизводимый consumer](../../ManualTests/CorePackageConsumer/README.md)
с собственными исходниками и только PackageReference, вне solution. Runtime C# и
lifecycle/provider contracts не менялись. Пять package projects получили конкретные
Description и самостоятельные PACKAGE.md: TrueMoon, Contracts, Cobalt, generator,
Microsoft DI adapter. Общая версия и net10.0 сохранены, публикации пакетов не было.

Generator доставляется **явным** TrueMoon.Cobalt.Generator reference с PrivateAssets=all;
runtime nuspec не содержит его автоматической transitive зависимости. Это принятое
правило поставки, не исправление analyzer runtime. Actual compiler analyzer path/SHA256,
consumer-generated ModuleInitializer и resolution проверены на NuGet, отдельно от
ProjectReference unit tests. Microsoft DI consumer дополнительно собирается без generator.

### Итоговый запуск

Windows x64 build26300, SDK10.0.401/runtime.NET10.0.12, net10.0, framework-dependent
Debug/Release build/publish без RID. Свежая local version:
`0.2.2.7-alpha.headless.20261011142810069`. Generator DLL SHA256:
`AB7D23337C3866C6877264F92D825A961DA789FB450D1F3077F42FF8867C5288`.
Все пять TrueMoon packages получены из нового local feed в isolated cache; source
mapping и .nupkg.metadata сверены. Consumer assets только package, без UI/Skia/Silk,
IPC или units. Runtime output не содержит generator/Roslyn DLL.

```powershell
pwsh -NoProfile -File ManualTests/CorePackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/CorePackages/final-consumer

dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
dotnet test TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj --no-restore --logger "trx;LogFileName=TrueMoon.Cobalt.Generator.Tests.trx" --results-directory TestResults/CorePackages/tests --verbosity quiet
```

Повторять runner в **новом** каталоге; final-consumer уже содержит evidence и не
перезаписывается. Три component test commands завершились exit0, xUnit2.9.3/VSTest:
Core81/81, Cobalt37/37, generator6/6, Failed0/Skipped0; TRX totals совпадают.

| Проверка | Фактический результат |
| --- | --- |
| App + explicit generator, оба providers | 40/40 processes: 5scenarios ×2providers ×Debug/Release ×build/publish |
| Direct Cobalt без App/adapter | 4/4 processes, Composite/open generic/transient/borrowed cleanup |
| Microsoft DI без consumer generator | 20/20 processes, те же5scenarios ×Debug/Release ×build/publish |
| Generation | По8headless/6direct generated sources на configuration; 4 actual compiler path/hash checks; marker в consumer assembly и автоматически опубликованный graph |
| Consumer compilation/delivery | 6builds/6publishes exit0/0warnings/0errors |
| Delivered README | 3точных C# snippets извлечены из packaged PACKAGE.md, 3Release builds +3processes exit0/0warnings/0errors; примеры вручную не адаптировались |
| Missing generator control | Fresh isolated consumer без generator, compile-time marker сохранён; expected build exit1/CS0246 RHeadlessConsumer. Это отрицательный контроль, не passing build |
| Runner | exit0, summary.Completed=true, Cases64/ReadmeExamples3, Failure=null |

Сценарии: normal lifetime.Cancel; external cancellation с исходным token;
startup failure с rollback только worker; stop failure с продолжением worker cleanup;
build failure с resolver-owned source/subscription/factory cleanup. В normal/cancel/
stop-failure lifecycle order worker.Start→gate.Start→Stopping→gate.Stop→worker.Stop→Stopped;
startup failure worker.Start→gate.Start→worker.Stop→Stopping→Stopped, build failure
не запускает сервисы. Исходные exception identities сохранены (build может иметь wrapper).
Отдельный shutdown token остаётся cancelable и не canceled после отмены run token.

Composite Worker/aliases и lifetime aliases shared; open Repository<int>/closed
Repository<string> singleton возвращают Int32:17/String:17 из приоритетного provider,
app name write/read совпадают. Runtime factory singleton shared; две Ticket transient
instances различны и освобождены. Factory1/Gate1/Ticket2/WorkerResource1 releases,
borrowed instance0. Cobalt Worker.Dispose calls1, Microsoft DI calls3 в normal graph;
Worker идемпотентен и собственный ресурс освобождает один раз. Это сохранённое
provider difference, а не обещание reference-unique Microsoft DI cleanup.

Observer failure изолирован; framework и consumer diagnostics наблюдались.
После shutdown/build failure fresh DiagnosticListener не enabled, events не
доставляются, retained owned source отклоняет операции как disposed. Нет UI,
clipboard, system settings или чужих процессов; runner ограничивает только
свои children timeout и завершает только свои зависшие процессы.

Pack exit0 во всех5проектах, **535 warning entries** в pack logs (31/147/177/75/105),
не zero-warning source build и не535уникальных проблем. Shared XML/RS1041 и прочие
warnings не исправлялись. Полная solution сборка/другие suites не запускались.

### Промежуточные попытки, артефакты и ограничения

Первый runner `first-consumer`: все64runtime processes прошли, omitted-generator
build верно отказал CS0246, но verifier ожидал только CS0234 и завершился exit1.
Classifier исправлен на обе формы отсутствующего namespace; весь итоговый runner
повторён с новой версией/feed/cache. First summary.Completed=false сохранён и не
считается итоговым passing run. Позднее добавлена проверка примеров из actual package
README; она входит в final-consumer. Одна локальная reporting command имела PowerShell
foreach/pipeline parse error; исправленная сводка сверила все actual reports/TRX.

Ignored TestResults/CorePackages содержит оба runs, feed/package SHA256, isolated
cache, source copies/SHA256, compiler analyzer list/generated source hashes,
build/publish output, process reports/logs, negative control и summary; tests/TRX
отдельно. После executable proof менялась только навигационная документация/статусы.

Не проверялись другие OS/SDK/compiler hosts, физический Ctrl+C/kill/parent exit,
RID/self-contained/trimming/single-file/NativeAOT, owned disposal failure matrix
в отдельном package consumer, disk/JSON fault injection и performance. Component
coverage этих контрактов не выдаётся за более широкий package run. UI K7/physical
DPI/real loss/VRAM, Enerit/Mithril own-process и общий выпуск остаются открыты.
CORE5/COB4 выполнен в указанном scope; локально далее COB5 baseline фиксированного
graph, общий функциональный приоритет — MIT0 bounded own-child, затем ENE1/ENE2.

Final static verification: 31 changed/new text files без NUL/conflict markers,
source final newline, XML/PowerShell syntax valid. 34documents/374local links/85heading
links без ошибок; git diff --check exit0. Все123 recorded source fingerprints и
5package SHA256 совпадают; все64 child reports Completed=true. TestResults ignored.

## Diagnostics test isolation при MIT4 — 2026-10-14

Во время [MIT4/ENE4 regression](../mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14)
TrueMoon.Tests Debug сначала дал80passed/1failed/0skipped.
AppCreationFailureDisposesResolverOwnedSubscriptions получил IAppLifetime.Cancel
от параллельного теста через default TrueMoon filters. Исправлена только проверка:
ConcurrentBag собирает unique eventName собственного DiagnosticListener probe,
сохраняются original exception, IsEnabled=false и Assert.Empty для post-failure
события. Production Core без изменений. Final Debug81/81 и Release81/81,
Failed0/Skipped0, Windows x64/net10.0/SDK10.0.402. Команды/TRX в общем integration
журнале. Packages/headless64+3/UI этой regression не повторены.

## Package regression MIT5/ENE5 — 2026-10-14

[Общий package журнал/команды](../mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14).
Windows x64/net10.0/SDK10.0.402: independent App/IPC/units consumer108/108,
Cobalt/Microsoft DI включая DI без Cobalt analyzer. Actual packaged dependencies/
compiler path/SHA и no-runtime-analyzer delivery подтверждены. Related source
regressions Core81/81, Cobalt37/37/generator6/6 в Debug/Release, Failed0/Skipped0.
Core получил только package Description; Cobalt/DI adapter C# runtime без изменений.
Headless64+3/COB5 performance baseline не повторены. Full solution Debug nonincremental
exit0/errors0/warnings1227; другие UI/native checks не запускались.
