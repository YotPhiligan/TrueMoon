# Engine / Editor: история решений и проверок

## Child diagnostic delivery — 2026-10-27

ENG8a.3/ENE6d.4 после outcome/wait metrics: child poll-читает bounded Core ring,
отправляет один batch по отдельному typed Enerit endpoint, host импортирует в
обычные ring/sinks. Pixel/control/Mithril queues не используются для logs.
[Согласованный контракт](PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27),
[подключение/API/budgets](DIAGNOSTICS_DELIVERY.md).

Shared Contracts добавляет reader/importer и additive Origin без изменения старого
DiagnosticRecord constructor. Aether зависит от contracts, без новой зависимости
на TrueMoon bootstrap. Copied launch options и App/Cobalt factories задают delivery
products; low-level старые constructors остаются local-only. Lease владеет receiver
до child reap/raw drain, runtime владеет sender после game/control cleanup.
Cancelled/timeout wait сохраняет actual cleanup owner для retry.

Version/count/depth/size и все rows проверяются до import; exact session/generation/
nonce/host/game/role/PID fences, bound child appid и monotonic sequences не дают
смешивать generations. UInt64 generation переносится exact bits существующим
Int64 scalar: первый build с UInt64 дал TME001; выбран additive DTO long, generator
не менялся. Host назначает собственные appid/sequence и authoritative context,
сохраняет child origin/PID/time/trace. Import не re-emits DiagnosticListener;
sender подавляет Origin records, сама lane не публикует events/traces/metrics.

Known gap/size/rate/pending drops считаются Lost, unknown ACK outcome — Unconfirmed;
replay отсутствует. Sender имеет собственный per-call CTS и PendingRequestCount
guard даже с custom factory50s/64slots. ACK проверяет status/count/exact last payload
sequence. Rate учитывает JSON+1024bytes до parse, включая malformed body.
Stop фиксирует конечный source cursor; принудительная отмена tail видна в
DrainComplete. Local JSONL и continuous stdout/stderr сохраняются независимо.

Новые120Aether cases/38methods и59Core cases/18methods; всего179.
[Полная Requirement | Evidence matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-matrix.md),
[quality/gap review](../../TestResults/DiagnosticsDelivery-20261027-1/tests-quality.md),
[Core matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-matrix.md).

| Требование | Точные тесты, TrueMoon.Aether.Tests.Diagnostics |
| --- | --- |
| Whole-packet validation, identities и monotonic fences | ChildDiagnosticReceiverTests.InvalidRow_RejectsTheWholePacketBeforeEvenTheEarlierValidRowIsImported; ForeignGenerationIdentity_IsRejectedBeforeImportAndDoesNotBindTheApplication; ApplicationPacketAndRecordFences_RejectStaleDuplicatesWhileLossHeartbeatsRemainMonotonic |
| Missing/malformed ACK без replay, own deadline и finite capacity | ChildDiagnosticForwarderTests.MalformedAck_MakesBatchUnconfirmedAndNeverReplaysIt; OwnRequestDeadlineAndPendingGuard_BoundAnInjectedClientWithLargeCapacityAndTimeout |
| Count/byte/rate/codec boundaries и no feedback | ChildDiagnosticLimitsTests.ReceiverWireBounds_AllowExactSizesAndNeverPublishOversizedResponse; RateBudget_IncludesEnvelopeAcceptsExactBoundaryAndRecoversInANewWindow; ChildDiagnosticForwarderTests.SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText |
| Both DI, ownership/rollback/cleanup и independent control | ChildDiagnosticFactoriesTests.AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts; RuntimeHost_OwnsSenderAndDrainsItAfterPreviewGameCleanup; RuntimeHost_DeliveryFactoryFailureRollsBackGameAndControlWithoutChangingCause; BlockedDiagnosticImporter_DoesNotBlockIndependentControlConnection |
| Finite drain и timeout не освобождает чужой/исполняющийся ресурс | ChildDiagnosticForwarderTests.StopCapturesOneFiniteTargetAndDoesNotChaseNewerReaderRecords; BlockedBorrowedReader_TimesOutOnlyTheStopWaitThenReleasesOnRetry; ChildDiagnosticReceiverTests.BlockedImporter_RejectsConcurrentPacketsAndPreservesOwnedTransportAcrossTimeoutAndCancelledStopWait |
| Actual source child flood/control/JSONL/raw/reap | ChildDiagnosticProcessTests.ThreeActualGenerations_ForwardProvenanceAndLossWhileControlAndLocalFilesSurviveFloodAndBrokenSinks |

Source proof использует реальные NativeRuntimeFactory, public UseAetherPreviewRuntime
и RuntimeHostFactory:3Preview children одной session, разные PID/appid,33control
commands на generation (Hello/LoadScene/GetStatus/SetMode(false)/Stop), slow sink
drops и throwing sink failures. Preview не поддерживает Step, game artifact/input/
GPU surface не открываются. На сохранённом отдельном запуске receiver принял12rows
на generation; host bounded history сохраняет4, child reported loss1741/1224/1237.
Все3child reaped, lease/receiver released.29raw/report/JSONL files:
[process evidence](../../TestResults/DiagnosticsDelivery-20261027-1/process-agent-debug/delivery-report.json).
Final full suites также выполняют этот process test в Debug/Release.

Непрошедшие попытки сохранены:
- Initial build: TME001 UInt64 и отсутствующие generated types, исправлен DTO scalar.
- Sender initial24/25: cold pipe не успевал к20ms barrier; обычный test budget500ms
  против injected50s сохраняет проверку own deadline/pending guard.
- Fixture compile: неверный TryResolve на IServiceProvider заменён GetService.
- DI116/118: неверное ожидание Completion unstarted Mithril handle убрано;
  PID/no-receiver/caller ownership остаются проверены.
- Process118/119: existing synchronous Console.In segment в RunAsync откладывал flood
  до Stop; fixture запускает runtime worker через Task.Run. Production не менялся.
- Первый full attempt прерван PowerShell NativeCommandError при stderr xUnit.
  Скрипт теперь собирает stdout/stderr до exitcode, attempt1 сохранён. Whole-Core RED
 372/373 подтвердил process-global event interference; App test изолирует свой origin
  и relative cursor, сохраняя normal routing. Whole-Core rebuilt GREEN373/373.
- Второй full Debug4283/4285:2InvalidRow cases прошли assertions, но Dispose timeout
  ordinary fixture200ms. Test-only deadline2s, специальные50ms timeout cases и
  min/max boundaries сохранены. Targeted23/23 и wholeAether1338/1338 прошли до
  окончательного full. Failed attempt2/10TRX/log/binlog/source hashes сохранён.

Final validation SDK10.0.403, xUnit/VSTest, net10.0:
Debug nonincremental build0errors/1546warnings; Release incremental0errors/1316warnings.
Core373, Cobalt179/generator91, Enerit343/generator114, Mithril99,
UI893/Hosting114, Aether1338, Editor741 — **4285/4285 в каждой конфигурации**,
failed/skipped0. Все179newcases passed в обеих конфигурациях.
[Machine verification](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json):
1020source hashes, source changes0,20test-binary hashes,44TRX/24binlogs включая
неуспешные попытки. Pre-existing Microsoft DI10.0.14 сохранён по SHA256.

Команды финального профиля (точные arguments — validation-steps.json):
```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsDelivery-20261027-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsDelivery-20261027-1/Debug -bl:TestResults/DiagnosticsDelivery-20261027-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsDelivery-20261027-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsDelivery-20261027-1/Release -bl:TestResults/DiagnosticsDelivery-20261027-1/tests-Release.binlog
```

Это первый bounded profile, не весь ENG8a/ENE6d/ENE6c. GPU/display, gameplay/input,
published/package, arbitrary parent containment, long trends/performance не
повторены. Исторические CPU FrameReceiver и Mithril ready-file races не исправлялись.
Следующий diagnostics consumer — ED6a/ED8a bounded console/export, затем command/
frame metrics; authoring ED4c.2 → ED4c.3/workflow и hardware gates прежние.


## IPC trace context в control factories — 2026-10-26

UseAether и UseAetherRuntime (включая PreviewRuntime) внедряют borrowed telemetry и
PropagateTraceContext=true в штатные generic pipes client/server factories.
CaptureRequestEvents сохраняет false; standalone прежних signatures оставляет
legacy defaults. Factory override и App lifecycle/ownership сохраняются.
Same-build HostId уже включает Enerit assembly; generator/service payload прежние.
[API](../../TrueMoon.Aether/README.md#диагностика),
[wire и compatibility](../enerit/DIAGNOSTICS.md).

Две новые cases
AetherPipesTraceTests.AppGenericEngineControlFactories_PropagateTraceWithoutActivatingEngineOrRuntime
на Cobalt/MS DI создают App и реальные localpipe products через штатные factories.
Точные root/client/server IDs/kinds/remote parent, Current через await, scopes и
borrowed/App-owned lifetime проверены. Guard factories подтверждают отсутствие
активации host/runtime/frame/GPU/child; это не native engine process proof.

Final narrow2/2; Aether1218/1218 Debug и1217/1218 Release, Editor741/741 в обеих
конфигурациях. Release повторил прежний ENG2
FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain:
null из TakeLatest, Assert.NotNull line177 после Received>=5. CPU receiver и его тест
этим срезом не менялись. Claim pending до CopyLatest остаётся source inference;
нужен deterministic reproduction и отдельный ownership fix. Повтор для green не
делался; этот failure не скрыт за успешными100новыми cases или Debug4021/4021.
Release full4020/4021, failed1/skipped0. Builds0errors:
Debug nonincremental1530warnings, Release incremental1300warnings, SDK10.0.403.
[Полная matrix/команды](../enerit/HISTORY.md#ene6d1-trace-context-через-pipes--2026-10-26),
[evidence](../../TestResults/DiagnosticsIpc-20261026-1/verification.json).

Отдельный source Enerit generated-proxy/child trace proof выполнен full suites;
native Aether/published/GPU/package/performance acceptance не повторялись.
ENG8a частичен: далее IPC/command/frame aggregates, bounded child delivery и
ED6a/ED8a console/export. ED4c.2 → ED4c.3/workflow и прежние hardware gates прежние.

## Операции и трассы диагностики — 2026-10-26

Продолжение «давай следующий шаг»: первый ENG8a.1/3 operation consumer поверх
[Core CORE6d](../core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26).
UseAether передаёт borrowed events/telemetry через существующие factories,
старые standalone constructors сохранены. Новых DI aliases/owners нет.

Build scope включает reap/drain/cleanup, terminal log и measurement предшествуют
TCS completion. Replace scope включает recovery; terminal log сохраняет original
cause, phase, rollback flags и отдельный RecoveryErrorType. RecoveryResult имеет
отдельный error snapshot. Failed Replace остаётся Failed при successful rollback.
Preview.Show/Resize ждут acknowledged first-present; Stop scopes включают cleanup.
FrameFault всегда Failed и фиксирует старые identity/surface до drain, отдельный
FaultCleanupFailed сохраняет обе причины. Presented callbacks не добавляют logs/scopes.
[Схема событий и IDs](../../TrueMoon.Aether/README.md#диагностика).

| Требование | Точное доказательство в AetherTelemetryTests |
| --- | --- |
| Реальный compiler success/failure/cancel и terminal before TCS | Build_EndsCorrelatedScopeBeforeReturningResultFailureOrCancellation |
| Replace success/cancel/expected identity | Replace_SuccessAndPreflightCancellationPreserveExpectedIdentityAndOperationOutcome |
| Original/recovery причины, successful/failed rollback и correlation | Replace_RecoveryFieldsAndDistinctCausesRemainCorrelatedToOriginalFailure |
| Preview first-present/resize/stop, обе DI, отсутствие per-frame logs | Preview_ShowResizeAndStopCompleteOnlyAfterActualPresentationAndDoNotLogEveryFrame |
| Preview error/cancel и cleanup retry | Preview_FailureAndCancellationKeepOriginalOutcomeAndCloseTheOperation; PreviewStop_CleanupFailureIsReportedAndRetainedUntilSuccessfulRetry |
| Frame fault до/после presentation, metadata до drain и cleanup failure | FrameFault_CapturesPreDrainIdentitySurfaceAndOriginalFailedOutcome |
| Ошибающиеся injected diagnostics не меняют результат/exception | InjectedTelemetryFailure_DoesNotReplaceBusinessResultOrOriginalException; SafeOperation_OnlyCompletesAndDisposesBorrowedScopeOnce |

[Тестовый source](../../TrueMoon.Aether.Tests/AetherTelemetryTests.cs).
Новые25Aether cases; narrow32/32 включает прежние7diagnostics cases.
После найденной full-suite default-App registration regression Core добавил2cases,
Core narrow56/56 и полный Aether Debug1216/1216 прошли. Финальный full solution:
**3921/3921 Debug и3921/3921 Release**, failed/skipped0; Aether1216,
Editor741, Core308 в каждой конфигурации. SDK10.0.403, builds0errors:
Debug nonincremental1530warnings, Release incremental1300warnings.
[Все команды/initial failures](../core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[verification](../../TestResults/DiagnosticsTelemetry-20261026-1/verification.json)
содержат34TRX/12binlogs и hashes. Нового native/published/GPU/package/performance
proof нет; текущие passing не закрывают прежние races или hardware acceptance.

ENG8a остаётся частичным: следующий срез — ENE6d IPC propagation, затем
Start/Stop/Play/command producers, frame/queue/latency aggregates и live delivery/
Editor console/export. Authoring очередь ED4c.2 → ED4c.3/workflow не изменена.

## Managed logger runs в Editor и Runtime — 2026-10-26

EditorApplication включает Core managed JSONL: normal root
%TEMP%/TrueMoon/Diagnostics/Editor, smoke report/diagnostics/editor.
RuntimeEntryPoint использует report/diagnostics/runtime, новый GUID run на
logger lifetime; quotas/leases/emergency принадлежат Core.
Reports/artifacts/raw output и legacy непомеченные каталоги отдельно.
Source wiring compiled; managed Aether1191 и Editor741 Debug/Release прошли,
Core252/full solution3840/configuration в финальных runs, failed/skipped0.
Initial Debug повторил Mithril race и Core test-isolation defect; последний
исправлен только в тесте. Новый manual native/published/GPU/performance/package
proof не запускался. [Матрица и команды](../core/HISTORY.md#emergency-и-managed-jsonl-runs--2026-10-26).

## Диагностика сборок: ограничение хранения — 2026-10-26

Продолжение ENG8a.2: GameBuildRetentionOptions фиксируется при UseAether setup и
при low-level создании GameBuildCompiler. Default — 16 завершённых jobs, 64 MiB
суммарно с ownership marker и 7 суток от завершения cleanup. Опубликованные
content-addressed игровые artifacts не входят в этот budget и не удаляются.

Перед запуском компилятора и после reap/drain/удаления work/output выполняется
best-effort sweep. Новые GUID jobs получают versioned .build-diagnostics marker;
эксклюзивная lease удерживается до окончания cleanup. Общая .retention.lock
согласует cleaners разных экземпляров/процессов без ожидания занятой lease.
Завершённые каталоги сортируются по completion UTC и ordinal имени при равенстве;
удаляются старейшие сверх count/bytes/age. Равенство лимиту допускается.

Последний завершённый job в корне и явно защищённый текущий job сохраняются,
даже если лимит невозможно выполнить. RetentionStatus сообщает retained bytes/
jobs, active/skipped/deleted, failures, bounded LastError, IsOverBudget и
MaintenanceBusy. Warning Build.RetentionLimited/1205 содержит JobId и counters;
сбой retention не подменяет успешный result или исходный GameBuildException.

Удаление строго нерекурсивное: только GUID job с корректным completed marker и
плоским набором build.stdout.log/build.stderr.log/build.output.json. Root/jobs/
job ancestry и entries проверяются на reparse points. Legacy unmarked jobs,
незавершённые/повреждённые markers, неизвестные файлы и abandoned staging после
crash сохраняются и отражаются отдельно. Поэтому это budget управляемых
завершённых diagnostics, а не обещание общего размера artifact root.
После блокировки файла marker сохраняется; следующий sweep повторяет cleanup.
Обслуживание запускается при сборках, фонового таймера нет; hostile внешняя
подмена путей между проверкой и удалением вне trusted-local/cooperating scope.

| Требование | Evidence: точное имя теста |
| --- | --- |
| Count и порядок по завершению, стабильное равенство дат | Sweep_CountLimitDeletesOldestByCompletionInsteadOfDirectoryMtime; Sweep_EqualCompletionTimesUseOrdinalDirectoryTieBreaker |
| Суммарные bytes с marker: меньше/равно/больше границы | Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality |
| Age строго сверх лимита, нормализация UTC | Sweep_AgeExpiresOnlyStrictlyAfterCompletionLimit |
| Newest/explicit protection и недостижимый count/bytes/age budget | Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget |
| Active job защищён от другого store, затем становится eligible | Sweep_ProtectsActiveJobAcrossStoreInstancesUntilItCompletes |
| Root contention без ожидания и retry | Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking; Sweep_MaintenanceFailureIsObservableAndCanBeRetried |
| Неизвестные/legacy/incomplete/nested jobs и artifacts сохранены | Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs; Sweep_NeverInspectsOrDeletesPublishedArtifactsOutsideJobs |
| Reparse root/jobs/job/entry не затрагивает eligible external archive | Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets |
| Failed delete сохраняет ownership, остальные jobs очищаются, retry проходит | Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease |
| Options копируются, невалидные limits отклонены до IO | Store_CopiesAllRetentionLimitsBeforeCallerMutatesOptions; InvalidRetentionLimits_AreRejectedBeforeCreatingDirectories |
| App Cobalt/MS DI: success → failure сохраняет последний job и game artifact | AppBuildRetention_SnapshotsOptionsRetainsLatestFailureAndPreservesPublishedArtifact |
| Retention failure не заменяет success/compiler error, Warning1205 связан с JobId | Compiler_RetentionDeleteFailureDoesNotReplaceSuccessOrOriginalBuildError |
| Пустой store не создаёт архив | Sweep_EmptyStoreDoesNotCreateArchiveOrReportLoss |

Добавлены 42 cases (38 unit + 4 actual compiler/App), узкий Debug42/42.
Полные Aether1191/1191 и Editor741/741 в Debug/Release; всего solution3767/3767
в каждой конфигурации, failed/skipped0, 20TRX. Build Debug nonincremental и
Release incremental: 0errors/1530warnings каждый. SDK10.0.403, Windows x64/net10.0.
Source fingerprints968, tested binary fingerprints20 и 4 binlogs сохранены в
[verification.json](../../TestResults/DiagnosticsRetention-20261026-1/verification.json).
Исторический TestResults/Diagnostics-20261025-1792916778832 в текущем workspace
отсутствует; прежние ссылки сохраняют исходные пути. Его native/published
результаты ниже являются исторической записью, а не проверкой этого запуска.

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsRetention-20261026-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsRetention-20261026-1/Debug -bl:TestResults/DiagnosticsRetention-20261026-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsRetention-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsRetention-20261026-1/Release -bl:TestResults/DiagnosticsRetention-20261026-1/tests-Release.binlog
```

Это новые managed/SDK-process проверки; отдельный manual native/published/UI/
hardware/package/performance run в этом срезе не выполнялся. Исторические
Mithril ready-file и CPU FrameReceiver races не объявлены исправленными.
ENG8a.1/2 и CORE6 остаются частичными. Далее — Core emergency output и age/total
directories/runs retention, дополнительные engine events, затем traces/metrics/
IPC context и ED6a/ED8a console/export; очередь authoring/editor не менялась.

## Единая диагностика: первый профиль — 2026-10-25

Первый consumer CORE6 и ENG8a.1/2: UseAether/UseAetherRuntime используют injected
sources; session/process/build events имеют stable IDs, context и exception
snapshots. EditorApplication и runtime подключают App-owned JSONL pipeline.
Protocol/wire/session authority не менялись; Microsoft.Extensions.Logging не добавлен.

Runtime stdout/stderr — live32KiChar tails и четыре fixed file slots на поток,
каждый≤1MiB UTF8. Process diagnostics inspection≤256records/64leases, supervisor
остаётся owner. Compiler использует общий ProcessOutputCapture и build.output.json
с loss/error status; прежний exit code/DiagnosticsDirectory сохранён.
[API](../../TrueMoon.Aether/README.md#диагностика),
[CORE6 contracts/limitations](../core/DIAGNOSTICS.md).

| Требование | Evidence |
| --- | --- |
| State/scene/tick/generation correlation + legacy callback | SessionTransitions_AreCorrelatedAndPreserveConfiguredObserver, Cobalt/DI |
| Исходная launch error и Error/Phase metadata | SessionLaunchFailure_PreservesOriginalCauseAndPublishesErrorWithSessionIdentity, Cobalt/DI |
| Build job events + drained raw metadata | AppCompiler_ReportsSuccessfulJobAndPersistsDrainedOutputStatus, Cobalt/DI |
| Compile failure сохраняет logs/status/cause | AppCompiler_ReportsCompilerFailureAndRetainsTextAndOutputMetadata |
| Реальный own child/двойной flood/retention/cleanup | EngineSessionProbe diagnostics:6generations, each2 113 547chars/stream без newline, live tails≤32768chars,8files≤1MiB, Step/Stop, затем launch-failure, structured108records/written108/no sink losses и reap |

Новые Aether7cases прошли отдельно и в полных Aether1149/1149 Debug/Release;
Editor741/741 в каждой конфигурации. Full solution3725/3725 Debug и3725/3725 Release,
failed/skipped0,20TRX. Build0errors/1530warnings, Debug nonincremental.
Результаты Core/Cobalt/Enerit/Mithril/UI сохраняют собственные assembly counts
в [verification.json](../../TestResults/Diagnostics-20261025-1792916778832/verification.json).

Canonical process proof: current Debug apphost и published Release, скопированный
в свежий OS temp и запущенный с working directory там же; по6started children,
12reaped total, плюс по1expected launch failure без child. Дополнительный published
Release run внутри evidence тоже прошёл. До копирования опубликован один current
artifact; top-level payload hashes копии совпали. Source/binary fingerprints и
outside location записаны в verification.json/outside-location.json.

```powershell
dotnet publish ManualTests/EngineSessionProbe/EngineSessionProbe.csproj --no-restore -c Release -o TestResults/Diagnostics-20261025-1792916778832/publish -v:quiet -bl:TestResults/Diagnostics-20261025-1792916778832/publish-Release.binlog
./ManualTests/EngineSessionProbe/bin/Debug/net10.0/EngineSessionProbe.exe diagnostics TestResults/Diagnostics-20261025-1792916778832/native-final-Debug
```

Published executable выполнялся в OS temp; exact directories и captured report
указаны в [location](../../TestResults/Diagnostics-20261025-1792916778832/outside-location.json) и
[native report](../../TestResults/Diagnostics-20261025-1792916778832/native-outside-Release/diagnostics.json).
Общие build/test команды — в [Core history](../core/HISTORY.md#структурированная-диагностика-первый-профиль--2026-10-25).

Remaining: build jobs total/age retention, additional Replace/preview/frame event
coverage, cross-process trace context, spans/metrics, UI console/export и full
fault/long-load acceptance. Предыдущие Mithril ready-file/CPU receiver races не
объявлены исправленными новым passing run. Packages/physical/golden/hardware loss/
VRAM/performance/long trends не повторены; alpha readiness не закрыта.

## ED4f.2: системная граница окна — 2026-10-25

Согласованный24октября дизайн реализован и завершён25октября: главное окно включает
общую Hosting option UseSystemAccentBorder. System accent берётся из cached host
capability IWindowSystemColors, отсутствие цвета возвращает текущий Theme.Border.
Inactive RGB составляет60% выбранного цвета, focus transition160мс обратим; alpha/
толщина/viewport/foreground прежние. Explicit BorderColor local/style имеет приоритет.
Generic Windows/API и WinRT resources находятся в Platform.Silk; App/Cobalt/IFactory
путь и единственный window owner прежние. Default frames других consumers opt-out.

UISettings.GetColorValue(UIColorType.Accent) вызывается через проверенный по Windows
SDK10.0.26100.0 IUISettings3 ABI. Используются временные SafeHandle HSTRING/COM refs,
balanced RoInitialize(MTA/STA)/RoUninitialize; неуспех/API absence даёт null.
Native WM_SETTINGCHANGE/WM_THEMECHANGED/WM_DWMCOLORIZATIONCOLORCHANGED только ставят
dirty flag. Чтение и render request происходят после event pump вне WndProc, не
на каждом кадре. Аргумент цвета сообщения не принимается за текущий Windows accent.

Текущие проверки в TestResults/AccentBorder-20261024 (имя evidence-root сохранено
со времени начала задачи; завершение по локальной дате25октября):

- 15новых xUnit cases/8methods: UI7, Hosting8. Narrow51+16=67passed/0failed/0skipped:
  narrow-185021842, RunNarrow.ps1. Проверены accent/update/null/recovery, unsupported
  host, RGB60%/159–160мс/reversal/detach/idle/opt-out, theme/style/local precedence,
  immutable snapshot и ранняя validation до регистрации/activation.
- Related UI893/Hosting114/Editor741 в каждой Debug/Release:1748/configuration,
  3496passed/0failed/0skipped. TRX/log/binlog: managed-185909752. Все15новых cases
  отдельно найдены в обеих конфигурациях; тесты вручную перечитаны на силу assertions.
- Последний nonincremental Debug solution build0errors/1532warnings:
  solution-build-185934385/build.log и build.binlog. Ранее CS9191 указывал на
  эквивалентный ref/in argument; заменён на in, полный проход повторён.
- 12native source Debug/published Release cases: native-185940301/summary.json,
  206PNG/16reaped children,validation0errors/0warnings и zero owned UI sessions/
  mappings/GPU resources/handles. Новый border.json проверяет настоящий UISettings
  accent, refresh/render после directed WM_THEMECHANGED, owned HWND focus messages,
  неизменный viewport и actual border pixels с RGB tolerance1. На этой Windows
  active #04827D → inactive #024E4B. Финальные active/inactive PNG просмотрены.

Directed сообщения отправляются только собственному HWND на owner thread; исходное
наблюдаемое состояние восстановлено. Global SetFocus/cursor/Windows personalization
не меняются. Реальная пользовательская смена системного accent и физическая смена
фокуса не заменяются этим proof. Native HRESULT/loader errors не fault-injected;
null-provider fallback/recovery проверен напрямую, native cleanup/ABI проверены в
указанном supported desktop scope. Общая alpha/K7/K8/VRAM не закрыта.

Итог и свежие source/binary/PNG/test SHA256:
TestResults/AccentBorder-20261024/verification.json. Предыдущий native-185439428
прошёл12cases до правки ref/in и не считается итоговым binary proof. Первый narrow
не компилировался из-за test syntax/неподдерживаемого Select; исправлен через
реальные keyboard selection gestures, final passing narrow указан выше.
Aether/full solution tests/прочие модули/packages/performance не повторены.
Следующий engine шаг прежний: ED4c.2 names/order → ED4c.3 create/delete.

## ED4f.1: прозрачный статус-бар — 2026-10-24

По согласованному продолжению ED4f нижняя панель28 остаётся поверх полного viewport,
idle фон имеет слабый tint/sigma0; hover/keyboard focus проявляют PanelBackdrop
за160мс. Foreground и размеры/положение сцены не меняются. Ошибки, busy/waiting и
собственные details/Preview popup удерживают подложку. Смена темы/null fallback,
overflow, отступ24 под resize grip16 и прежние команды/данные/IFactory/ownership сохранены.
Оформление opt-in задаётся в EditorStatusBarViewStyle; presentation не знает тему.
Подсветка гибкой summary button ограничена иконкой/текстом вместо всей ширины.

Общий StatusBar.OverlayContent/KeepBackdropVisible принадлежит UI. В обоих frame
participants focus return выполняется только при собственном pending overflow,
поэтому animation frames не забирают focus из сцены или popup. Нет timers, resolution
на кадр или новых сервисов; detach/disable/dispose завершают переход.

Текущие проверки:

- 10новых cases/7methods: shared UI7, Editor3. Узкий запуск27+66=93passed,
  failed/skipped0: TestResults/StatusOverlay-20261024/narrow-180739181, RunNarrow.ps1.
  Проверены159/160мс, reversal/idle/focus/activity/teardown, Dark/Light/null и raster
  blur/sharp foreground; actual editor busy/error/ready и popup owner/focus.
- Related UI886/Hosting106 Debug/Release: managed-180831425. Финальные Editor741
  Debug и741 Release: editor-final-181252516. Итого1733/configuration,3466passed/
  0failed/0skipped; все10новых cases найдены в обеих конфигурациях. TRX/log/binlog
  лежат в этих папках TestResults/StatusOverlay-20261024.
- Последний nonincremental Debug solution build0errors/1532warnings:
  editor-final-181252516/build.log и build.binlog. Предыдущий build1533warnings
  содержал новый xUnit2031; test-only запись Assert.Single исправлена, Editor и
  solution повторены. Runtime source/binary hashes после этого совпадают с native.
- 12native source Debug/published Release cases: native-180910307/summary.json,
  182PNG/16reaped children,validation0errors/0warnings, zero owned UI sessions/
  mappings/GPU resources/handles. Footer idle/hover поверх actual scene, Light
  popup и opaque fallback просмотрены. Native-180005688 прерван после11cases без
  финального inspector Release teardown report; он исключён из итогового passing.

Итог и актуальные SHA256: TestResults/StatusOverlay-20261024/verification.json.
Focused assertion/gap review выполнен без empirical mutation/coverage metrics.
Outputs в C OS TEMP, evidence в ignored TestResults; OS/driver settings не менялись.
Aether/full solution tests/packages/physical DPI/input/performance/VRAM не повторены.
Fixed-camera640×360 transport/aspect-fit letterboxing прежние; alpha/K7/K8 и
CPU FrameReceiver/Mithril quality issues остаются открытыми. Очередь ED4c.2 → ED4c.3.

## ED4f: viewport под оконным chrome — 2026-10-24

По согласованному дизайну удалена постоянная полоса scene path/title/root. Editor
включает общий WindowFrame.TitleBarOverContent через Hosting composition; viewport
занимает внутреннюю клиентскую область под caption36 и footer28. Островки остаются
ниже caption с видимым отступом16. Hover/menu/path prompts не меняют scene bounds;
frame.Content используется вместо предположения о Children[1]. Default consumers
сохраняют прежний layout. View/Data/Style, App/Cobalt/IFactory и borrowed ownership
сохранены; новых service locator/scopes/services не добавлено.

В caption добавлены scene name/dirty/activity, полный путь доступен через Tooltip.
Tooltip ограничен560logical по тексту, переносит явные переводы строк и длинные
Unicode paths по grapheme boundaries; использует MenuBackdrop и opaque fallback.
Title backdrop меняется за160мс от слабого tint/sigma0 к PanelBackdrop без fade
foreground; hover работает в inactive окне, focus/menu удерживают active фон.
PopupInsets оставляет transient меню/Tooltip ниже caption без сдвига content.
Win32 передаёт HTCAPTION move/leave в UI, сохраняя native default dispatch.

Open/SaveAs/first Save открывают retained contextual path prompt без IO до явного
подтверждения. Path draft, controls и return focus сохраняются; Escape/outside click
отменяют запрос. Pending Save/Discard/Cancel не теряется при outside click; errors
можно закрыть и повторить, в том числе с тем же текстом. Обычный named Save остаётся
прямым. Hidden tools не резервируют высоту. Ctrl+S/W маршрутизируются root observer
даже без focused control; прочие text-editing shortcuts остаются scoped. Runtime/
preview admission, общий document/history и terminal close policy прежние.

Текущие проверки после финального Tooltip fix:

- 84новых xUnit cases/45methods в7files: UI37, Hosting8, Editor39. Narrow TRX и
  research/plan/assertion/static-gap review сохранены в OS TEMP
  TrueMoon-OverlayTests-845332f1c973409b841c1614f7915999. В итоговых TRX отдельно
  проверено присутствие всех84cases в Debug и Release; empirical mutation/coverage
  metrics не заявляются.
- Related full UI879/Hosting106/Editor738 Debug и столько же Release:
  1723/configuration,3446passed/0failed/0skipped. TRX/log/binlog:
  TestResults/OverlayChrome-20261024/managed-160136478.
- Nonincremental Debug TrueMoon.slnx build:0errors/1532warnings. Log/binlog:
  TestResults/OverlayChrome-20261024/solution-build-160200516. Outputs в уникальном
  C OS TEMP из-за малого свободного места E; настройки SDK/driver/OS не менялись.
- 12native source Debug/published Release profiles: empty, legacy, documents,
  transform, hierarchy, inspector.166PNG,16reaped children,validation0errors/0warnings,
  zero owned UI sessions/mappings/GPU resources/handles после teardown. Report:
  TestResults/OverlayChrome-20261024/native-160207207/summary.json. Source/binary/PNG
  SHA256 сверены; финальные idle/hover/Tooltip/path prompt и темы просмотрены.
- Новый owned-HWND probe проверяет WM_NCHITTEST=HTCAPTION и доставку координат
  WM_NCMOUSEMOVE до UI, затем owner-driven160мс переход и leave. Глобальный cursor/
  focus не перемещается; actual timed transition отдельно проверен UiSession и
  unit clock. Это directed native bridge proof, не physical Snap/drag acceptance.

В ранних попытках native held-hover не успокаивался из-за повторной render
invalidation; unchanged-target guard исправлен и закреплён двумя active/inactive
cases. Скорректирована inactive-hover admission. Native caption probe отделён от
положения реального курсора, а auto-confirm smoke helper ограничен document actions
после обнаруженного обращения к disposed UiSession при Exit. Первые green12native
не приняты как итог Tooltip: визуальная проверка выявила single-line path/CRLF glyph;
после wrapping добавлен Unicode regression и полный проход повторён. Эти попытки
сохранены в TestResults/OverlayChrome-20261024, финальным считается только run выше.

Итог: TestResults/OverlayChrome-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance/VRAM не повторены. Frame transport,
fixed-camera640×360 preview и его aspect-fit letterboxing прежние. Alpha/K7/K8 и
прежние CPU FrameReceiver/Mithril quality issues не закрыты. Следующий шаг ED4c.2
names/order, затем ED4c.3 create/delete; полный ED4/ED5 остаётся открытым.

## ED4e: островки поверх viewport — 2026-10-24

По согласованному дизайну EditorLayout сначала рисует viewport во всей рабочей
области, затем закреплённые обозреватель/инспектор. Видимый отступ16, radius8,
тонкая граница и shadow внутри8px gutter; размеры wrapper264/336 ограничиваются
доступной шириной без пересечения. Высота следует содержимому до предела, ScrollViewer
сохраняется. Убран служебный заголовок VIEWPORT. Parent-owned EditorIsland использует
ScalarTransition160мс, group opacity и slide8px; closing сразу запрещает ввод child,
wrapper продолжает frame participation до скрытия. Detach завершает переход.
Начальное IsOpen выставляется из модели до attach, исключая пустой инспектор на старте.
Темы и hide/show сохраняют retained View/fields/drafts/focus/scroll согласно контексту.
PanelBackdrop использует существующий blur/tint/fallback; fields остаются чёткими.
View/Data/Style scopes и App/Cobalt/IFactory ownership сохранены; новых сервисов нет.

Текущие проверки:67новыхcases (Editor26/UI41), narrow103passed; related UI842,
Hosting98, Editor699 в каждой Debug/Release, всего3278passed/0failed/0skipped.
Nonincremental Debug solution build0errors/1532warnings.12native source Debug/
published Release cases вне checkout,158PNG,16reaped children; validation0errors/
0warnings и zero owned UI sessions/mappings/GPU resources/handles. Все прежние
game/document/Transform/hierarchy/inspector сценарии повторены; actual preview
проверен по pixels/revision, Dark/Light/null material — по retained controls и bounds.
Нативный helper теперь ждёт layout после раскрытия/прокрутки дерева перед pointer click.
Первый полный native запуск был прерван без summary; следующий выявил этот stale
coordinate в documents fixture; финальный12/12 — native-142613712.
Итоговые Dark/Light/opaque preview PNG просмотрены, поля и текст чёткие.

Evidence: `TestResults/Islands-20261024/verification.json`; managed-140938913,
narrow-190757052, solution-build-final и native-142613712 содержат TRX/binlogs/
commands/PNG/JSON/source и binary hashes. Generated bin/obj находятся в C OS TEMP
из-за ограниченного свободного места E:. Tests quality/pseudo-mutation review
статический; coverage и mutation execution не заявлены. Aether/full solution tests,
packages, physical DPI, Vulkan/HUD effect matrix, performance/VRAM не повторены.
Свободные drag/resize/docking остаются отдельным UX этапом; engine очередь прежняя.

## ED4d.1: компактные действия и контекст инспектора — 2026-10-24

По скриншоту заменены текстовые actions: одна строка Apply/Cancel/Undo/Redo/Add,
26×26, reset24×24, Delete рядом с component disclosure. Геометрические Add/Delete
добавлены в конец UiSymbol, старые значения сохранены. Schema choices остаются
подписанными, у Restore видимое имя occurrence рядом с иконкой. Scoped style
задаёт размеры; accessible names содержат назначение и shortcuts.

Inspector использует прежний OverlayHost: Tooltip через500мс, включая disabled,
без смены focus, с непрозрачным Theme.Control и текстом12. Геометрия/clip/selection/
input/scroll/hide/detach/dispose отменяют hint; transient ancestor subscriptions
и host.Closed очищаются. Чужой popup не заменяется. Idle frames после delay нет.

EditorViewModel.IsInspectorVisible следует выбранной сущности; вся retained область
InspectorPanel скрывается, EditorLayout отдаёт viewport колонку и gap. ClearSelection
не меняет документ/history; draft переключаемой сущности отбрасывается по прежней
policy. Снятый выбор сохраняется при новой revision той же сцены. Escape в Tree
сначала отменяет drag; следующий bubble снимает selection. View/Form/Scroll/actions
остаются прежними, динамические property sections после нового выбора актуальны.
Будущее создание — ED4c.3: показ по контексту создаваемой сущности; фиктивная форма
без entity ID или незавершённый creation API не добавлены.

### Текущие проверки

25новыхcases/14methods:16icons/Tooltip и9selection/layout. Narrow181/181,
0failed/0skipped/exit0, `TestResults/InspectorIcons-20261024/narrow-130857356/`:
command.json, inspector-icons.trx/binlog/stdout/exit.txt. Включены156existingcases.
Manual clock доказывает499мс без popup и500мс с popup; Dark/Light live switch
сохраняет тот же непрозрачный popup. Assertion-quality/static test-gap-analysis
проверены; executed mutations/coverage percentage не заявлены.

| Требование пользователя | Точный тест |
| --- | --- |
| «небольшие кнопки с иконками» | `IconActions_UseCompactSemanticSymbolsWhileSchemaChoicesAndRemovedComponentNamesRemainVisible` |
| «Tooltip при наведении» | `Tooltip_DisabledIconAppearsAtExact500MillisecondsWithoutStealingFocusOrKeepingIdleFrames` |
| «не отображать панель инспектора если ничего не выбрано» | `Shell_ClearSelectionReclaimsColumnAndRestoresSameInspectorChromeWithoutCommittingDraftOrRetainingFocus` |
| Возврат после hide и lifecycle | `Tooltip_InputOrContextChangeDisposesOwnedHintWithoutPublishingAnEdit`,8вариантов; `Tooltip_DetachClosesPopupAndReattachmentRequiresFreshHoverWithoutOwningHostOrModel` |

Related suites **UI801/Hosting98/Editor673** в Debug/Release =1572/configuration,
**3144passed/0failed/0skipped**,6TRX в `TestResults/InspectorChrome-20261024/managed-131042124`.
SDK10.0.402/net10.0/xUnit2/VSTest. Commands `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/InspectorChrome-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<name>.trx" --results-directory <run> /bl:<run>/<name>.binlog
-v minimal`. Точные driver scripts — VerifyFinal.ps1/VerifyFinish.ps1, ignored.

Полная nonincremental Debug solution build сначала остановилась на disk-full при
копировании Skia PDB (`solution-build-131108180`), не на C# ошибке. Удалены только
ignored artifacts предыдущего неудачного narrow-130533692, отчёты/исходники сохранены.
Повторный build с `--artifacts-path <unique OS TEMP>` на C: завершился exit0,
**0errors/1532warnings**, `solution-build-131434548`; path сохранён в summary.json.
Passing tests не перезапускались из-за ошибки копирования.

Native final `native-131440978/summary.json`: **12/12** source Debug/published Release
вне repo, empty/legacy/documents/Transform/hierarchy/inspector. **152PNG/16reaped children**,
validation0errors/0warnings; UI sessions/mappings/GPU resources/handles0. В каждом
header5/status4 captures с BottomAligned/ResizeGripClear. Inspector8captures:
empty hidden, disabled Tooltip, Esc hidden-with-scene, icon sections, typed draft,
dependency error, empty entity Add и real actual-presented preview. Source/binary/
PNG hashes сохранены и повторно сверены. Финальные Dark Tooltip/Light component/
hidden viewport screenshots просмотрены; прежние authoring/save/history/preview,
legacy и document workflows прошли в обеих поставках.

Промежуточный native-130050236 не дошёл до окна: acceptance использовал private
header Move/Until helpers; сделаны internal для reuse. Preflight native-130311570
прошёл до финальных visual/lifecycle fixes. Первый narrow177/178 обнаружил stale
hint после hide: popup удалён, delay state оставался и мешал свежему hover. Добавлен
cleanup ancestor visibility/host.Closed; новый narrow181/181 включает регрессию.
Прозрачный фон Tooltip найден на PNG и исправлен до final build/native.

Evidence: `TestResults/InspectorChrome-20261024/verification.json`. Docs/anchors/
namespaces/diff whitespace проверены. Aether/full solution tests/прочие модули/
packages/physical DPI/input/performance/VRAM/final alpha не повторены; исторические
CPU receiver/Mithril races не объявлены исправленными. Далее ED4c.2/3 по плану.

## ED4d: динамический инспектор компонентов — 2026-10-24

По запросу пользователя inspector строит только фактические effective компоненты
и свойства выбранной сущности по exact schema version. Boolean/Int64/finite Number/
multiline String и explicit nullable, наследование/default/локальный источник,
reset, Add/Remove/Restore подключены к общему authoring документу. У пустой сущности
нет искусственных Transform полей. Unknown type отображается по ID с generic
scalar editors. Component ID и property key используются в мутациях, подписи — нет.

`ComponentEditBatch` расширяет ту же ISceneAuthoringSession: Set/Reset properties,
Add/Remove/Restore/ResetRemoval components, expected revision, operation journal,
one atomic publication/history entry, cancellation и общий resolver. Exact kinds,
nullable, dependency/multiplicity, built-in Transform validity проверяются до commit.
Remove local occurrence удаляет addition, inherited — маскирует, очищая instance
property overrides; Undo возвращает прежний patch. Reset локального addition
очищает и override, и исходное explicit data property, возвращая schema default.
Restore и property edits допустимы совместно. Catalogs/definitions неизменны;
существующий JSON v1 сохраняет patches, новый формат не введён.

`EditorInspectorViewModel` — единый owner drafts/history/preview gates для header,
hierarchy/status. Прежний Transform View factory заимствует этот state, compatibility
TransformViewModel оставлен для прямых consumers без второго DI owner. App/Cobalt
регистрирует model/style/IFactory; родитель владеет созданным View, dependencies
borrowed. Поля/секции кешируются по ID/key, обычный refresh/theme сохраняет controls,
focus/selection/scroll. Scalar row отдаёт input оставшуюся ширину; String полноширинный.
Ctrl+Enter/Esc обрабатывает inspector, Ctrl+Z внутри text editor остаётся его undo.
External commit/replaced document сохраняет draft с conflict; stale structural action
отклоняется. IO/runtime/draft/close gates общие. Legacy runtime — readonly.

### Текущие проверки

Windows x64, SDK10.0.402/net10.0/xUnit2/VSTest. **163новыхcases:92engine+71Editor**;
ещё38существующих DI cases адресно мигрированы. Narrow engine92/92 и Editor109/109
без failed/skipped, exact commands/TRX/binlog/stdout/exit0 в
`TestResults/Inspector-20261024/narrow-engine-102423665/` и
`narrow-editor-103718742/`. Assertion-quality и static pseudo-mutation review проверены;
executed mutation score/coverage percentage не заявлены. Найденные gaps закрыты
проверками boundary key128, Restore+Reset и неизменности нетронутого CRLF текста.

| Требование пользователя | Evidence: точный test method |
| --- | --- |
| «динамическое представление свойства выделенной сущности» | `Selection_ProjectsOnlyEffectiveComponentsWithExactSchemasKindsAndProvenance` |
| «показываем редакторы и свойства если они есть у сущности» | `SchemaEditors_UseCheckboxTextAndMultilineControlsAndRemoveSectionsAbsentFromSelection` |
| «отображение и редактирование компонентов (ECS)» | `SchemaChoices_AddCorrectOccurrenceAndApplyScopedStylesToNewDynamicEditors`; `TypedInputs_CommitBooleanNullIntegerAndMultilineTextAsOneTransactionThroughButtons` |
| Общий history/tree и один inspector | `Shell_SelectionGenericDraftAndHierarchyMovesShareHistoryAndUseOnlyOneInspector` |
| Stale action / same-ID document replacement | `StructuralAction_HeadlessCommitOrSameIdentityReplacementRejectsStaleActionUntilFreshAttempt` |
| DI/ownership | `BothProviders_ShareGenericModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedInspectors` |

Полные связанные suites: **Aether1142/UI801/Hosting98/Editor648** в Debug и Release,
**2689/configuration,5378всего,failed/skipped0**. Debug первые3 — `managed-105536563`,
финальный Editor Debug и все4Release — `managed-105853556`. Production этих первых3
между проверками не менялся. Commands — `VerifyFinal.ps1`/`VerifyFinish.ps1` в ignored
`TestResults/Inspector-20261024`: `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/Inspector-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<name>.trx" --results-directory <run> /bl:<run>/<name>.binlog
-v minimal`. Финальный nonincremental `dotnet build TrueMoon.slnx` с тем же artifacts
path, `-c Debug --no-incremental -v minimal /bl:<run>/build.binlog` — exit0,
**0errors/1532warnings**, `solution-build-105946391`. Это4test projects, не full suite.

### Native acceptance и границы

`native-105956351/summary.json`: **12/12** fresh processes, source Debug и published
framework-dependent Release вне repo: empty/legacy/documents/Transform/hierarchy/
inspector. **146PNG/16reaped children**, validation0errors/0warnings, UI sessions/
mappings/GPU resources/handles0. В каждом header5/status4 captures, BottomAligned/
ResizeGripClear. Source/binary/PNG hashes сохранены и повторно сверены.

Inspector обеих поставок: actual typed controls/input, nullable empty-string/null,
Int64.MaxValue, multiline Cyrillic/surrogate-pair text, single transaction Undo/Redo,
reset default7, dependency rejection без частичной публикации, inherited Remove/
Restore, Add к пустой сущности без Transform, SaveAs/Close/reopen с exact IDs и clean
history. Footer запускает реальный preview committed revision; actual presented
stamp и pixels красного куба проверены. Compiler не запускается, fixture source
не изменён, child drained/reaped. Финальные dark/light/empty/component-error/preview,
legacy и documents PNG просмотрены; null label spacing исправлен до final run.
Glyph coverage используемого font не расширялась: emoji хранится точно, на PNG может
использовать missing glyph. Physical OS input/DPI этим directed UiSession run не доказаны.

Промежуточные attempts сохранены и не засчитаны: native-104411704 — конфликт типов
параметра Delivery в evidence runner, native-104737874 — status smoke ожидал старый
Transform View. Исправлены runner и acceptance null guard. native-105120575 прошёл
Inspector Debug preflight. Первый полный Editor Debug647/648 ожидал текст Transform
в hierarchy hint; после перехода к любым property drafts assertion адресно обновлён,
повторный final648/648 в обеих конфигурациях. UI test helpers передают multiline
через Enter events и ждут deferred binding drain; production ввод не подменён.

Evidence: `TestResults/Inspector-20261024/verification.json`, exact commands/TRX/
binlogs/JSON/PNG и scripts ignored. Docs/local links/anchors/new namespaces/diff
whitespace проверены. Full solution tests/прочие модули/NuGet consumers/physical/
performance/VRAM/final alpha не повторены. Старые CPU FrameReceiver/Mithril ready-file
races не объявлены исправленными. Runtime ECS World/storage/scheduler, custom editor
providers, template editing и live runtime transaction не входят в этот срез.
Далее ED4c.2 persistent names/order, затем ED4c.3 create/delete/duplicate.

## ED4c.1: дерево сущностей и Reparent — 2026-10-24

Первый срез по запросу «показ иконок, возможность перемещать и редактировать дерево
и объекты». Общий `TreeView/ITreeView` в Argentis принимает frozen forest, рисует
геометрические Entity/Cube/Interaction и disclosure, поддерживает expansion,
selection, keyboard/reveal/scroll и parent move requests. View/Data/Style Editor
зарегистрированы через App/Cobalt/`IFactory<EditorHierarchyView>`; App владеет
model/style, родитель — созданным View. Подписки снимаются, borrowed зависимости
не освобождаются View. Существующие constructor consumers/legacy flat list сохранены.

`ReparentBatch` расширяет ту же ISceneAuthoringSession, expected revision, receipt
journal, cancellation, atomic resolver validation и Undo/Redo. Новый parent
сохраняет local component values/Transform и definition inheritance; мировое
положение может измениться. Self/cycles/missing targets/duplicates/stale intent
отвергаются без частичной публикации. ParentId уже входит в scene JSON v1, новый
UI-only формат не введён. Guid order сохраняется. Rename/sibling order и
create/delete/duplicate остаются ED4c.2/3.

Editor переносит на сущность или в пустую область дерева/«В корень»; Ctrl+Left/Right
выполняют outdent/indent. Общий выбор обновляет Transform inspector, история
смешивает parent и Transform edits, Save/reopen и committed Edit Preview используют
прежних owners. IO/runtime/exit/Transform draft/conflict закрывают admission.
Каждый committed snapshot отменяет drag. Дополнительно исправлен stale gesture:
Move фиксирует ожидаемый document до Refresh и отклоняет unseen headless commit
или замену документа с теми же IDs. Диагностика read-only, доступна для копирования.
Workspace кеширует семантические nodes; root style не обходит nested View.

### Текущие автоматические проверки

Windows x64, SDK10.0.402/net10.0/xUnit2/VSTest. Новые71test methods/130cases:
engine42, TreeView41, Editor47. Исходные изолированные Debug команды/TRX/log/binlog:
`TestResults/Hierarchy-20261024/narrow-135848457/`, финальные
`engine-reparent-final.trx`, `treeview-draw-final.trx`, `editor-hierarchy-stale.trx`;
все130passed/0failed/0skipped. Проверка assertions и plausible caller-visible
mutations закрыла gaps receipt source comparison, exact cancellation exception,
direct Draw icon routing, equal SetItems gesture cancellation и stale document.
Mutation execution/coverage percentages не заявлены.

| Requirement | Evidence: точный test method |
| --- | --- |
| «показ иконок» | `Draw_DisclosureAndPerItemIconsUseDeclaredSymbolsAndHierarchyDepth` |
| «возможность перемещать» | `PointerDrag_ParentAndBlankRootDropsPublishRequestsWithoutChangingItems` |
| «редактировать дерево и объекты» | `Shell_TreeSelectionTransformCommitParentMoveAndKeyboardHistoryShareOneRetainedDocument` |
| Сохранение local Transform и общий history | `History_TransformAndReparentUseOneUndoRedoStackAndRestoreContentCheckpoints` |
| Stale headless commit / same-ID replacement | `Move_HeadlessCommitOrSameIdentityDocumentReplacementRejectsStaleGestureUntilFreshAttempt`, оба варианта |
| DI и product ownership | `BothProviders_ShareModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedTrees` |

Полные связанные test projects Debug/Release: Aether1050, UI801, Hosting98,
Editor577 = **2526/configuration,5052cases**, failed/skipped0. Это четыре проекта,
не full solution tests. `managed-091444455` содержит8TRX; после финальной правки
UI-подсказки Editor577/configuration повторён в `finish-092447200`,2TRX.
Остальные production исходники между этими проверками не менялись.

Команды воспроизводятся `VerifyFinal.ps1` и `VerifyFinish.ps1` в ignored
`TestResults/Hierarchy-20261024/`: `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/Hierarchy-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<project>-<configuration>.trx" --results-directory <run>
/bl:<run>/<name>.binlog -v minimal`. Все commands завершились exit0.
Финальный `dotnet build TrueMoon.slnx --artifacts-path
TestResults/Hierarchy-20261024/artifacts --no-incremental -c Debug -v minimal
/bl:<finish>/solution-build.binlog`: exit0,0errors/1532warnings. Native runner
отдельно собирает Editor Debug/Release и framework-dependent Release publish.

### Native acceptance и границы

Финальный `native-092503209/summary.json`: **10/10** source Debug/published Release
processes вне repo: empty, legacy game, documents, Transform, hierarchy.
Всего118PNG/14reaped children; каждый report: UI sessions/mappings/GPU resources/
handles0, validation0errors/0warnings. Header5/status4 captures в каждом процессе,
включая BottomAligned/ResizeGripClear. Source/binary/PNG fingerprints сохранены
и сверены. Финальные dark hierarchy, published light preview, legacy light и
authoring documents PNG просмотрены; glyph-подсказка исправлена на Ctrl+Left/Right.

Hierarchy в обеих поставках: реальный pointer selection/drag, local Changes и
definition identity сохранены, один history entry, collapse/reveal, Ctrl+Z/Y,
retained theme/inspector, SaveAs/Close/Open и persisted ParentId с пустым ephemeral
history. «В корень» и Undo возвращают clean saved checkpoint. Footer запускает
реальный Edit Preview, actual presented revision и пиксели parented cube проверены;
один child drained/reaped, compiler не запущен, исходная fixture не изменена.
Старые document/Transform/legacy сценарии адаптированы к общему дереву.

Промежуточные native attempts не засчитаны: `native-090921771` остановился в
старом header menu wait; `native-091619401` — document Capture, ожидавший старую
длинную строку status; `native-091931087` — отложенный focus assertion header.
Document Capture теперь проверяет typed summary и отдельный bound entity count.
Header driver ждёт action нужной группы и повторяет только открытие dismissed
popup, никогда application command; focus после close проверяется в том же
owner callback, до дальнейших native FocusLost. После этих исправлений
`native-092055591` прошёл10/10; финальная glyph-правка повторена в указанном
`native-092503209`. Это улучшение directed driver, не доказательство причины
любых исторических физических input failures.

Evidence: `TestResults/Hierarchy-20261024/verification.json`, TRX/log/binlog/JSON/
PNG и scripts ignored. Общие plans/status/README/default style, ссылки/anchors,
namespaces новых файлов и diff whitespace проверены отдельно. Не запускались
full solution tests/новые NuGet consumers/physical DPI-input/performance/VRAM/
Vulkan UI effect matrix/final alpha. Старые CPU FrameReceiver и Mithril ready-file
race не объявлены исправленными; K7 workaround search не возобновлялся.

## ED4b: выравнивание статусбара — 2026-10-24

По скриншоту убрана пустая нижняя строка16logical: default WindowFrame больше
не резервирует высоту под grip, рисуя его поверх правого нижнего угла content.
StatusBar28 доходит до внутреннего бордера; его View/Bar bounds совпадают.
Правый отступ24 сохраняет доступность details рядом с grip16; DI/preview/actions
и retained View/Data/Style не менялись.

Narrow57/57; UI760/Hosting98/Editor530 Debug/Release =1388/configuration,
failed/skipped0.2native empty cases source Debug/published Release вне repo,
20PNG,zero owned UI/GPU/mappings,View disposed,compiler/children не запускались.
Dark/Light PNG просмотрены. Resize/scale1/1.5/2/maximize/restore/theme/idle и
region metadata проверены регрессией; native smoke подтверждает bottom alignment.
[Причина, exact tests/commands и границы](../alloy/HISTORY.md#pa4b-выравнивание-statusbar-по-нижней-границе--2026-10-24).
Evidence: TestResults/StatusBarAlignment-20261024/verification.json.
Full solution/Aether/headless/legacy/Transform native/physical input не повторены.
Прежние CPU receiver/Mithril fixture issues и порядок работ не изменены.

## ED4b: компактная строка состояния — 2026-10-24

Согласованный дизайн реализован:28logical footer,12text/14icons/24targets,
primary semantic status/cancel слева и objects/Edit Preview/details справа.
Tick/PID/full selectable error доступны в матовом popup над панелью.
Edit Preview actions перенесены из production inspector в footer; прежний
coordinator/committed history/lifecycle сохранён, game controls остались в header.
Активный preview обозначен «Редактирование сцены», без смешения с game pause.
Responsive overflow, keyboard/focus/dismissal/theme и retained tree проверены.
View/Data/Style и typed factories подключены к App/Cobalt; ownership явный,
Cobalt/MS DI/failed factory cleanup/detach проверены.

80новых cases; итог UI759/Hosting98/Editor527 Debug/Release =1384/configuration,
failed/skipped0. Nonincremental Debug solution build0errors/1532warnings.
Source Debug/published Release вне repo:6native empty/legacy/Transform cases,
68PNG/12reaped children,validation0/0 и zero UI/mapping/GPU ownership.
Footer start/stop с реальным preview, actual revisions/pixels, Undo/Redo/Save/reopen
и прежний header workflow прошли; compiler для preview fixture не запускался.
Dark/Light/details/Preview/Transform PNG просмотрены. Источники/binaries/PNG hashes
и exact TRX/commands: TestResults/StatusBar-20261024/verification.json.

[Общий механизм, точные тесты, исправления и границы](../alloy/HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24),
[дизайн](EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24).
Full solution tests/Aether/headless/packages/physical/performance не повторены.
Прежние CPU receiver/Mithril fixture issues открыты. Следом minimal create/delete
общего authoring API/history и UI commands; межмодульный порядок прежний.

## ED4a: матовый фон меню — 2026-10-24

Dropdown меню Editor используют общий материал Argentis/Skia: blur уже нарисованного
фона текущего окна + theme tint, foreground/children чёткие. Theme.MenuBackdrop
Dark/Light/null, fallback и обычный style precedence сохранены. Idle/disabled menu
rows прозрачны; команды/availability/focus/retained popup и App/Cobalt/IFactory
ownership прежние. Отдельного прикладного blur service или OS window transparency нет.

Текущие related UI739/Hosting98/Editor467 Debug/Release =1304/configuration,failed/
skipped0,6TRX. Narrow44/44,16новыхcases в UI. Source Debug/published Release вне repo:
4native empty/legacy cases,28PNG/4reaped children; header, simulation9commands,
theme/keyboard/FocusLost и cleanup успешны. Validation0/0, UI sessions/mappings/
GPU resources/handles0, view disposed/children reaped. File/View PNG просмотрены;
pixel samples отличают backdrop от opaque fallback. Источники и binary/PNG hashes,
commands/отчёты — TestResults/Backdrop-20261024/verification.json.

[Общий контракт](../alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24),
[точные тесты/команды/исправленные assertions и ограничения](../alloy/HISTORY.md#pa8b-матовые-поверхности--2026-10-24).
Native Vulkan/HUD effect proof, performance/physical/full alpha, full solution/
Aether/headless/остальные native profiles не повторены. Прежние CPU receiver и
Mithril fixture issues остаются отдельными; следующий шаг — create/delete общего API.

## ED4a: знак TrueMoon и анимированное раскрытие — 2026-10-24

По согласованному уточнению слева добавлен fixed TrueMoon crescent/star, затем
постоянная More кнопка без обычного бордера. Hover раскрывает headings; точки
вращаются ⋮→⋯ на том же ScalarTransition180мс. Caption сдвигается, brand/More hit
bounds и simulation/window actions остаются неподвижными. Hover/pressed/focus
состояния сохранены. Один click либо Enter/Space раскрывает headings и после
анимации открывает первую группу «Файл». Первый enabled пункт получает фокус,
команда при открытии не выполняется. Повторный click закрывает и возвращает фокус
More; Esc/Tab/outside/FocusLost/detach отменяют pending open без позднего popup.
F10 сохраняет прежний выбор heading; duration0 не требует дополнительного жеста.

MenuBar исполняет pending open на owner frame, не удерживает IInputContext между
кадрами и не добавляет Task/timer. Idle/detach прекращают работу. UiSymbol.TrueMoon
добавлен в конец enum, прежние значения сохранены; вырез полумесяца прозрачный,
не закрашивается цветом предполагаемого фона. BrandMark — owned UI element внутри
EditorHeaderView, nonfocusable Caption region,28px area/20px symbol. Accent задаёт
EditorHeaderViewStyle. Новый сервис/DI API не нужен; существующие App/Cobalt/
IFactory/ownership contracts сохранены. Theme/hover не пересоздают дерево.

Прямые regression checks в существующих xUnit suites (имена методов точные):

| Требование пользователя | Проверка поведения |
| --- | --- |
| «добавить иконку» | EditorHeaderViewTests.BrandMark_PrecedesMenuAndKeepsCaptionRegionAcrossThemes; MenuChromeTests.TrueMoonSymbol_PaintsCrescentAndLeavesTheCutoutTransparent |
| «3 точки вертикально», «3 точки горизонтально» | MenuInteractionTests.DisclosureDots_RotateWithRevealAndReverseWithoutMovingTheButton:0/90/180мс, reversal, геометрия точек, fixed bounds и idle |
| «откроется сразу же первоей меню» | MenuInteractionTests.DisclosureClick_RevealsThenOpensFirstMenuWithoutExecutingAction; EditorHeaderViewTests.DisclosureClick_OpensFileAfterRevealAndReturnsFocusOnClose:179/180мс, первый enabled item, no execution, повторный click/focus |
| Отмена/клавиатура/zero motion | DismissDuringDisclosure_CancelsPendingFirstMenu (5cases); DetachingDuringDisclosure_CancelsFirstMenuOnReattach; DisclosureKeyboardActivation_OpensFirstWithoutExecutingItsFocusedItem (Enter/Space); DisclosureClick_ZeroMotionOpensFirstWithoutExtraGesture |

13новыхcases: UI11, Editor2. SDK10.0.402/net10.0/xUnit2/VSTest/Windows x64.
Финальные unfiltered related tests: UI723 + Hosting98 + Editor467 =1288 в каждой
Debug/Release, failed/skipped0,6TRX. Отчёты/unique binlogs/exit logs находятся в
TestResults/HeaderBrand-20261024/managed-193341761. Это текущие related проверки,
не full solution; Aether/Mithril/Enerit suites в данном уточнении не запускались.

Команда для каждой конфигурации и каждого из трёх указанных test projects:

```powershell
dotnet test <project>/<project>.csproj --artifacts-path TestResults/HeaderBrand-20261024/artifacts -c <Debug|Release> --logger 'trx;LogFileName=<project>-<configuration>.trx' --results-directory <managed-run> /bl:<managed-run>/<project>-<configuration>.binlog -v minimal
```

VerifyUi.ps1 сохраняет counters в summary.json; VerifyNative.ps1 собирает Editor
Debug/Release с --no-restore/тем же artifacts-path и отдельными build binlogs,
публикует Release --no-build --no-restore --self-contained false в fresh OS temp.
Исходные bin/Debug и пользовательские Editor/Debugger процессы не изменялись.
App запущен из external cwd с --smoke <case> --validation, legacy дополнительно
получает --project <absolute ManualTests/Game.Logic/Game.Logic.csproj>. Только
process-local Vulkan validation layer; изменения OS/драйвера не требуются.

Финальный native run: TestResults/HeaderBrand-20261024/native-193402807:

| Delivery/profile | Header proof | PNG | Reaped children |
| --- | --- | --- | --- |
| source Debug / empty | passed,5header captures | 6 | 0 |
| source Debug / legacy | passed,5header captures | 8 | 2 |
| published Release / empty | passed,5header captures | 6 | 0 |
| published Release / legacy | passed,5header captures | 8 | 2 |

Всего4cases/28PNG/4children. В каждом header.json: BrandMark,
DisclosureOpensFirst, DisclosureToggle, hover/caption shift/fixed simulation,
keyboard/theme/tooltip/FocusLost=true. Empty без compiler/children; legacy по9
прежних команд, включая start/pause/resume/step/replace/stop. ViewDisposed/Reaped=true,
UI sessions/mappings/GPU resources/handles0, validation errors/warnings0 во всех
cases. Финальные empty-Debug/header-collapsed.png и
empty-published-Release/header-file-menu.png просмотрены: знак, обе ориентации
точек, caption и первый dropdown соответствуют дизайну. Native smoke содержит
directed input, не заменяет physical DPI/drag/Snap acceptance.

Aggregate evidence TestResults/HeaderBrand-20261024/verification.json связывает
6TRX, native header/cleanup reports и команды, source/binary/PNG SHA256. Содержимое
проверено повторно после финального запуска. VerifyFinal.ps1 последовательно
выполнил VerifyUi.ps1 и VerifyNative.ps1; stdout final.log, stderr final.err пустой.

Предварительные narrow-192439038 и managed-192724095/native-192939427 прошли,
но относятся к предыдущему варианту рисунка. На его PNG обнаружены горизонтальные
AA seams полумесяца; перекрытие соседних полос исправило рисунок, после чего
выполнены финальные managed/native проверки выше. Ранний черновой launcher выбирал
обычный старый bin; он исключён из текущего proof, scripts используют явный
isolated artifacts path. Passing предварительных запусков не подменяет финальные.

Full solution, headless manifest/SDK/scene, остальные project/documents/Transform/
exit workflows, fresh packages, golden/performance/HUD/physical/loss/VRAM/full alpha
в этом уточнении не повторены. Последние full результаты2026-10-23 и отдельные
CPU FrameReceiver/Mithril ready-file failures остаются историей и открытыми
quality tasks. Следующий прикладной шаг — minimal create/delete общего authoring
API/history, затем UI; уточнение header не меняет порядок остальных этапов.

## ED4a: компактный заголовок и меню — 2026-10-23

Зафиксирован и реализован дизайн: ⋯ раскрывает «Файл / Правка / Вид / Проект /
Симуляция» за180мс, название сдвигается; ▶/пауза/стоп и стандартные оконные
кнопки неподвижны. Leave grace240мс, click pin, F10/arrows/Enter/Esc/Tab,
outside dismiss и FocusLost. Длинный caption сокращается, fixed buttons имеют
геометрические иконки и delayed tooltip500мс, включая disabled state.

Общая основа Argentis/Alloy: IMenu, MenuBar/MenuItem, IUiCommand/DelegateUiCommand,
OverlayHost (один nonmodal popup), ScalarTransition и owner-thread
IUiFrameParticipant/IUiInputObserver. UiSession использует monotonic TimeProvider,
не начисляет idle новому transition, обрабатывает приоритет owner перед popup host,
не удерживает кадры hidden/detached participants. Пятиаргументный constructor
UiSession сохранён для binary compatibility. Popup размещается/обрезается в
host viewport. Menu owner detach очищается на следующем update; detach самого
host скрывает popup, снимает borrowed references и сохраняет retained tree до
Close/reattach/dispose. Modal stack, long-menu scrolling и generic dialogs не заявлены.

WindowTitleBar.SetHeader принимает owned content; стандартные minimize/maximize/
close и Snap role остаются у default frame. Hosting hook ConfigureDefaultTitleBar
создаёт header на UI owner и передаёт vetoable/drainable close callback. Неподходящие
frame combinations отвергаются до allocation. Callback-throw native rollback
отдельно не проверялся; существующий host rollback не заменялся test-only seam.

EditorHeaderView/Style и typed IFactory<EditorHeaderView,EditorHeaderArguments>
подключены через UseAetherEditorHeader/App/Cobalt. Window владеет View, presentation
и style borrowed. Body владеет OverlayHost; header заимствует его в той же session.
Production compact mode скрывает прежние action rows; standalone constructor
сохраняет прежние controls для совместимости. Поле пути, dirty/errors и inline
Save/Discard/Cancel сохранены. File/Undo/Redo/build/runtime/theme actions перенесены
в меню; document picker/project switch не добавлялись.

▶ использует один tracked start→play gesture либо resume; Stop/Cancel/close
отменяют gesture, запрещают late Play и drains accepted work. Открытый authoring
document по-прежнему блокирует game actions до ED6; Edit Preview остаётся отдельным.
Header routing сохраняет S/Shift+S/W/Z/Shift+Z/Y/Esc/Ctrl+Enter после возврата фокуса
на ⋯. Modified Enter обрабатывается до обычной Button activation; body TextBox
history keys не перехватываются header.

Проверки:
- 105 новых cases: UI51, Hosting13, Editor41. Related Debug/Release:
  UI712 + Hosting98 + Editor465 =1275/configuration, failed/skipped0.
  Deterministic reveal/reversal/zero-motion/idle, exact close/tooltip boundaries,
  disabled admission, retained theme/focus/tree, dispose/subscription cleanup,
  overlays, keyboard/pointer gestures, fixed chrome bounds/regions, both DI providers,
  shared history/file actions и controlled start/stop/cancel/drain.
- Последний nonincremental Debug build0errors/1532warnings.
  Последний full Debug3048/3049: MithrilRestartProbe ReadyAsync получил IOException
  при чтении ready-4.json одновременно с публикацией; UI/Editor и Aether прошли.
  Последний full Release3049/3049,10TRX/configuration, skipped0.
  До финального harness admission-wait изменения был full3049/3049 в обеих
  конфигурациях; это предыдущий запуск, он не отменяет последний Debug failure.
- Ранний full Debug3042/3043 до shortcut regressions: старый FrameReceiver test
  TakeLatest вернул null после Received>=5. Позднее Aether1008/1008 в обеих
  конфигурациях; production CPU transport не менялся и race не объявлен исправленным.
  Отдельные ENG2/Mithril quality tasks внесены в планы.
- Source Debug и published Release вне repo:10native cases (empty/project/legacy/
  documents/Transform ×2),10header proofs,64PNG и12reaped children. Header
  hover/caption shift/fixed icons/menu/theme/keyboard/tooltip/FocusLost проверены
  directed input; actual Build→Run/Pause/resume/Step/Replace/Stop, file menu actions,
  native close и Menu Exit с Save/Discard/Cancel/failed Save выполнены.
  Transform revisions/pixels/Save/reopen и cleanup повторены. По всем cases
  UI sessions/mappings/GPU resources/handles0, validation errors/warnings0.
  Ещё10headless manifest/SDK/scene cases отдельно. PNG source/published header,
  light runtime и Transform просмотрены.
- Общий evidence TestResults/MenuHeader-20261023/verification.json:
  UiScopePassed=true, FullLatestDebugPassed=false; source/binary SHA256,
  PNG hashes, per-project reports и ссылки на immutable runs.
  Debug: solution-183128349; Release: release-183338362.
  Native: regressions/consumers-184542662,
  native-Debug-184631157, native-published-Release-184637466.
  Предыдущий passing full: solution-182420183; ранний CPU failure:
  solution-181603083. Unique binlogs/TRX/exits сохранены.
- Проверки используют --artifacts-path TestResults/MenuHeader-20261023/artifacts:
  обычный bin/Debug удерживается пользовательским Editor/Debugger, их не завершали.
  VerifySolution.ps1 запускает nonincremental build и VSTest Debug/Release с
  --blame-hang-timeout60s/--logger trx и unique /bl; VerifyRelease.ps1 фиксирует
  последний Release run. VerifyRegressions.ps1 публикует --no-build --no-restore
  Release вне repo, затем запускает external-cwd headless/native profiles.
  VerifyNative.ps1 отдельно проверяет Transform в каждой delivery.
  Aggregate.ps1 проверяет10header JSON/64PNG/12children,1275related tests и hashes.

Финальные related tests после compatibility overload: related-184236954/summary.json.
Последний повторный native до driver fix: consumers-184300613, empty-Debug timeout;
он не включён в passing proof. Final source/binary fingerprints проверены заново.

Исправленные проверки/границы: первоначальный smoke нажимал ▶ до owner refresh
CanRun после Build — добавлено ожидание enabled state. Header smoke получал deadline
между theme/F10 и при ожидании tooltip; причина отдельно не установлена. Driver
теперь удерживает injected hover на owner polls и повторяет F10 после потери
раскрытия. Это directed profile; physical input не заявлен. Exact delays и FocusLost
отдельно проверены fake-clock tests. Ранний
Transform Debug запуск ошибочно выбрал обычный старый bin: исключён из нового
proof; финальный native-Debug-184631157 использует isolated artifacts и содержит
header.json/7PNG. Эти неуспехи не скрываются passing итогами.
Fresh NuGet packages, HUD/Vulkan UI control matrix, physical DPI/drag/Snap,
golden/performance/loss/VRAM/full alpha не повторены. Следом minimal create/delete
через общий authoring API/history; test stability follow-ups остаются отдельными.

[Текущий статус](STATUS.md), [план движка](PLAN.md), [подплан приложения Editor](EDITOR_PLAN.md).

## ED3a: Transform/history/preview в UI — 2026-10-23

Сверка планов исправила устаревшие указания ARCH1b.1/ED1a как будущей работы и
смешение document Close/Exit с ещё открытой project/runtime transaction. Принят
порядок: первый edit/save/preview цикл → минимальные create/delete общего API →
project picker/switch и ED1/ED6 workflow. Core/Cobalt не требуют нового полного
quality prerequisite; ENE6c/d и оставшийся MIT6a/c остаются отдельными задачами.
Исторические результаты QUALITY/ENE6 baseline помечены по их scope.

[ED3a](EDITOR_PLAN.md#ed3a-transformhistorypreview-в-ui--2026-10-23) подключил
существующий ARCH1b.1 к девяти retained numeric полям, atomic Apply/Ctrl+Enter,
Cancel/Esc и Undo/Redo. Invariant Float-representable values и positive scale
проверяются локально, общий engine проверяет итоговые матрицы/scene. Draft хранится
отдельно от committed content; selection change отбрасывает его, внешнее изменение
той же сцены сохраняет conflict до явного Cancel. File/history mutations имеют
общую admission и проверяют document identity под owner gate; UI не держит второй
history. Dirty следует saved ContentRevision. При draft history отключена; вне
полей Ctrl+Z/Shift+Z/Y используют общие actions, внутри TextBox их не перехватывают.
Общий text undo этим шагом не добавлен.

EditorPreview заимствует show/stop engine boundaries, хранит одно выполняемое и
одно последнее ожидающее обновление. Explicit start/retry, auto update после
commit/Undo/Redo; ввод текста не вызывает child replacement. Exact presentation
receipt проверяется; старый кадр не помечается новой revision. Ошибка сохраняет
document/history и не даёт auto retry на каждом frame. Stop и document transitions
fence/cancel/drain child work, cleanup error наблюдаем и допускает retry. Cancel
close decision не останавливает preview. App/Cobalt/IFactory регистрируют model,
style и UI-owner products; dependencies borrowed, aliases не удваивают dispose.

### Проверки и найденные регрессии

Добавлены66cases в8files Editing. Assert/gap review без production mutations;
direct tests затрагивают model, preview, guarded commands, view/shell, codec,
both-provider composition и CLI. Новый bounded набор66/66 Debug; весь Editor424/424
Debug/Release. Финальный full solution **2944/2944 Debug и2944/2944 Release**,
10TRX/configuration, failed/skipped0. Core117/Cobalt179/generator91/Enerit166/
generator114/Mithril99/UI661/Hosting85/Aether1008/Editor424. Nonincremental Debug
build:0errors/1531warnings, прежние warnings не подавлялись.

| Поведение | Прямая проверка |
| --- | --- |
| Девять полей, atomic commit/history | `Selection_LoadsNineStableFieldsAndAtomicCommitUsesOneSharedHistoryEntry` |
| Невалидные числа/scale без частичной мутации | `InvalidField_ReportsLocalErrorAndNeverCommitsPartialBatch` |
| Draft/conflict/cancel и dirty checkpoint | `Refresh_PreservesDraftAndExternalRevisionRequiresExplicitCancelBeforeCommit`, `UndoRedo_UsesSavedContentCheckpointAndDraftCommandsCannotDiscardText` |
| Live game/IO/exit admission | `BusySaveAndExitDecision_BlockMutationWithoutLosingDraft`, `ApplyAndHistory_RecheckLiveRuntimeAdmissionAndPreserveDraftUntilRuntimeBecomesIdle` |
| Exact revision и последняя ожидающая сцена | `WrongSceneOrRevisionAcknowledgement_IsRejectedWithoutInventingPresentedState`, `BusyPreview_KeepsCurrentAndOnlyNewestWaitingSnapshotWithoutRetaggingPresentedFrame` |
| Failure/cancel/drain/retry | `PreviewFailureAndRetry_PreserveCommittedDocumentHistoryAndUncommittedDraft`, `Stop_CancelsDropsWaitingAndDrainsIgnoredCancellationBeforeSharedCleanupThenAllowsRestart`, `StopCleanupFailure_IsObservedAndRetryPerformsActualCleanupBeforeClearingError` |
| Реальный UI input, stable tree/style/focus | `ShellSelectionAndNormalizedKeyboardInput_EditSharedDocumentRetainFormAndPreserveFocusedTextShortcutOwnership`, `RefreshAndThemeStyleChange_PreserveDraftTreeFocusSelectionAndInputIdentity` |
| DI/factory/cleanup | `BothProviders_ShareModelAndBorrowedDependenciesWhileFactoryCreatesIndependentUiOwnedViews`, `StyleFailure_DisposesUnpublishedFormAndBindingsWithoutOwningBorrowedModelOrDocument` |
| Save/reopen всех значений и CLI scope | `TransformCommit_SaveCloseAndReopenWithRealCodecPreserveAllNineValuesAndClearHistoryAtNewSession`, `TransformSmoke_RejectsMissingFixtureOutputLegacyAndMixedAcceptanceScopes` |

Первые tests выявили неподдержанный selector expression в Visible binding: заменён
прямым HasError. Native capture выявил unconditional ErrorText notification каждый
frame: теперь уведомления только при изменении; regression требует0model/field
notifications и ui.Update=false при stable Refresh. PNG review выявил clipping
цифр при default padding в компактном TextBox: scoped padding4/2/4/2 исправлен и
проверен тестом/PNG. Pixel proof использует публичную PreviewGameSession.Camera,
а не камеру legacy demo. Ранний проход, изменённый во время style fix, не считается
финальным: окончательный run имеет неизменные source fingerprints.

Source Debug/published Release вне repo с deliberately invalid game csproj: по
одному Transform workflow,3PNG/4children. Actual revisions8/9/10 и reopen10,
позиция/цвет куба проверены pixel checks; source fixture unchanged, compiler0,
notifications только на UI owner, stable fields/canvas/selection. Stop и document
Close reaped children; final UI sessions/mappings/GPU resources/handles0,
Vulkan validation0errors/0warnings. PNG обеих deliveries просмотрены.

Также повторены empty/project/legacy/documents на source Debug/published Release:
8native cases/18PNG/4legacy children и10headless scene/SDK cases,19consumer commands
с6expected negative exits. В сумме текущий native scope — **10cases/24PNG/12reaped
children**. Прежний document workflow: по19actions/4PNG/5saved files на delivery;
legacy: по8commands/3PNG/2children. Физические DPI/loss/VRAM и fresh packages не
повторялись, весь hardware/release scope этими окнами не закрывается.

### Команды, evidence и продолжение

Финальный orchestrator: TestResults/TransformUi-20261023/VerifyFinal.ps1.
Полная сборка: `dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal /bl:<unique.binlog>`.
Tests: `dotnet test TrueMoon.slnx --no-restore -c Debug --no-build --blame-hang --blame-hang-timeout 60s --logger trx --results-directory <Debug> /bl:<unique.binlog>`;
Release — та же команда с `-c Release`, без `--no-build`. Все завершены с exit0.
Publish: `dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --no-build --no-restore -c Release --self-contained false -o <outside-app> /bl:<unique.binlog>`.
Native: `<editor.exe> --project <outside-fixture.aetherproj> --smoke <fresh-output> --smoke-transform --validation`.
VerifyNative.ps1 сохраняет generated fixture, CWD/delivery/source/binary hashes,
PNG, runtime/project cleanup и JSON receipts. VerifyRegressions.ps1 фиксирует
прежние headless/native consumers и их actual arguments/exits.

Агрегат: **TestResults/TransformUi-20261023/verification.json**; final solution —
solution-165319129, native-Debug-165441567 и native-published-Release-165446134;
regressions/consumers-165623415/summary.json. Narrow TRX/logs скопированы в focused;
все MSBuild invocations имеют отдельный binlog. Новых engine/wire/assembly contracts
не введено, package/publication readiness не объявляется.

Первый сквозной цикл закрыт в этом bounded profile. Далее create/delete через
общий authoring API/history, затем UI New→Add→edit→delete/Undo→Save/reopen; после
него ED1/ED6 project/runtime workflow. Preview всё ещё fixed-camera640×360,
до64entities и с child replacement. Полный World/ECS, live edit/MCP, docking/plugins,
MMF/performance и hardware gates сохраняют отдельные задачи.

## ED2b: документы в обычном UI — 2026-10-23

[Document UI profile](EDITOR_PLAN.md#ed2b-document-commands-и-ui--2026-10-23)
подключает существующий ED2a API: New/Open/Save/SaveAs/Close, dirty/activity/errors,
inline Save/Discard/Cancel и native window Exit. Нормальный запуск с .aetherproj
показывает панель путей/actions; проекты по-прежнему выбираются явно через CLI.
Doc transitions доступны только при stopped/idle runtime/compiler; открытый
authoring document блокирует stale game commands до preview/ED6 integration.
Игровой legacy .csproj workflow проверяется отдельно.

EditorDocumentCommands имеет fail-fast single admission, shared exit completion,
tracked accepted task и user decision, не хранит controls. Open сначала передаёт
candidate validation нижнему API: даже выбранный Discard не теряет старую сцену
при malformed target. Save failure при Exit возвращает false и сохраняет окно/
unsaved content. Cancel и forced upgrade реально drains IO, не ждут новое решение.
Throwing cancellation callbacks превращаются в наблюдаемую ошибку после drain;
terminal cleanup допускает retry. Project/session/store заимствуются; их ownership
сохраняется у прежних App/project owners, без disposable aliases.

EditorDocumentViewModel копирует snapshots и сообщает binding updates на owner
thread; background completion не мутирует UI. Path draft для Open/SaveAs и отдельный
first-save draft для decision сохраняются независимо от completion. Nested typed
View/Style создаётся injected IFactory на UI thread, parent owns product. Стабильные
controls, bindings и собственный scoped style не требуют полного rebuild.
Ctrl+S/Shift+S/W и buttons идут в одни commands; Esc отменяет decision. Safe focus
переходит на Cancel, возвращается к live control/path после IO. Решение inline,
generic PA3 overlays/modality/menu infrastructure не добавлена.

Workspace показывает raw authoring hierarchy IDs и resolved readonly properties
без 64-entity runtime projection. Граничный тест использует130entities без truncation.
String values показаны в кавычках с escaped control characters: первый UI test
выявил, что исходная multiline string ломала single-line TextBox.Value; formatter
исправлен, regression выполняет реальный UiSession.Update. Full IDs доступны в
inspector, подписи Guid сокращены с различимыми окончаниями. Пустой authoring
viewport сообщает, что preview пока недоступен; не предлагает заблокированный Build.

### Прямые tests и quality review

42methods/74новых cases в DocumentsUi: coordinator decisions/admission/drain,
real filesystem conflict/reopen, owner-thread model, retained view/styles/focus/
shortcuts/dispose, shell/raw properties, both DI providers и launch parser.
Research/plan/status и pairing inventory хранятся в OS temp вне Git. Static pairing
не является line/branch coverage; production mutations не применялись.
Assertion/gap review добавил older captured Save + newer edits + Exit decision,
externally accepted document IO admission, prompt keyboard intent и ctor guards.
Первые test-authoring empty snapshot assertions исправлены (Content=null внутри
действующей session, а не отсутствие owner). Это не production failure.

| Требование | Exact evidence в TrueMoon.Aether.Editor.Tests.DocumentsUi |
| --- | --- |
| Реальные Save/reopen/path/stamp/conflict/recovery | NewSaveAsOpenAndSave_UseCurrentPathStampAndPreservePersistedRawScene; CommandsWithRealStore_SaveReopenAndExternalConflictPreserveFileAndRecoverBySaveAs |
| New/Open/Close/Exit Save/Discard/Cancel | DirtyTransition_CancelRetainsIdentityContentAndAllowsRetry; DirtyTransition_DiscardReleasesOriginalWithoutSavingAndCommitsIntendedTarget; DirtyTransition_SavePersistsOriginalBeforeReplacementOrExitUsingCorrectTarget |
| Failed Save veto и invalid Open с Discard | FailedDecisionSave_VetoesTransitionKeepsDirtyOriginalAndCanRetryIntent; DirtyOpenDiscard_InvalidCandidateDoesNotDiscardCurrentContentOrSaveIt |
| Ignored IO / late completion / shared Exit | RequestExit_DrainsIgnoredSaveBeforeStoppingRuntimeAndConcurrentRequestsShareCompletion; Exit_AfterIgnoredSaveOfOlderSnapshotStillRequiresDecisionForNewerEdits |
| Throwing cancellation / forced upgrade / retry | Exit_CallbackFailureReturnsTrackedDrainRetainsOwnerAndSupportsExplicitRetry; ExitCancelAndForcedUpgrade_SharePendingCloseAndNeverWaitForAnotherUserDecision |
| Fresh runtime guards / owner notifications | RuntimeBlocking_UsesFreshCoordinatorAdmissionBeforeNextPresentationRefresh; Refresh_PublishesChangesOnOwnerOnlyAndWorkerCompletionCannotNotifyBindings |
| Inline choices / keyboard / focus | InlineDecision_UsesSeparateSaveDraftAndSafeCancelFocusThenRestoresPriorInput; KeyboardDuringDecision_SaveUsesPromptPathAndCloseCannotReplaceAcceptedIntent |
| Retained tree/path/style/subscriptions | ThemeStyleAndRefresh_PreserveTreePathDraftFocusAndSelection; ViewDisposal_DetachesBindingsAndDoesNotDisposeBorrowedModelOrCommands |
| Raw readonly shell и оба providers | ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands; BothProviders_ResolveSharedModelStyleAndCoordinatorWhileUiOwnsIndependentFactoryProducts |

Final focused command:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.DocumentsUi. --logger trx --results-directory TestResults/DocumentsUi-20261023/narrow-final --verbosity quiet
```

74/74 passed, failed/skipped0. TRX:
TestResults/DocumentsUi-20261023/narrow-final/MEGA6_PH-DEV_2026-10-23_20_14_32_net10.0.trx.
После финальной UI text/label правки те же74case повторяются в обоих final full runs.

### Итоговая source/published проверка

TestResults/DocumentsUi-20261023/VerifySolutionCurrent.ps1:
nonincremental Debug solution build0errors/1531warnings; Debug и Release
**2878/2878** каждый,10TRX/configuration, failed/skipped0. Core117/Cobalt179/
generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/Aether1008/Editor358.
Debug test --no-build после build; Release test собирает актуальный source,
blame-hang60s.242source fingerprints неизменны до/после. Final data:
TestResults/DocumentsUi-20261023/solution-current/summary.json.

Дополнительный footer regression добавляет concrete model.StatusText и
bound view.Status.Value assertions в ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands:
1/1passed, TestResults/DocumentsUi-20261023/footer-regression/
MEGA6_PH-DEV_2026-10-23_20_25_53_net10.0.trx. Native capture readiness усилен:
ждёт согласованных workspace count/model text/bound text, затем сохраняет эти
значения вместе с PNG. Final snapshots корректно показывают2/0/0/2entities;
предыдущий resized capture до этого ожидания показывал несогласованный footer.
Из этого промежуточного PNG production defect не выводится; final unit/native
данные совпадают, общий renderer/binding API не менялся.

VerifyConsumers.ps1: **consumers-153002186/summary.json Completed=true**.
Source Debug и published Release app/project/CWD вне repository, без shipped
Game.Logic.19commands: publish1/headless10/native8;6exit1 ожидаемы для missing SDK,
unsupported version и scene-root escape, остальные exit0.
Document UI в каждой delivery:19directed actions/4PNG/5actual saved files,
native close и real controls/keyboard. Проверены selection/raw properties,
SaveAs/reopen/stable IDs, external SHA256 conflict + recovery, invalid first Save,
New/Open Save/Discard/Cancel, invalid Open с Discard, failed Exit Save и Cancel
с прежним window/document, successful final Exit с exact saved SceneId.
Original fixture bytes неизменны; compiler/game children0, notifications только
на UI owner thread. Dark/Light,1100×700 resized и безопасный decision focus
визуально сверены. Saved files: Copied/Recovered/New/BeforeOpen/Exit.aetherscene.

Empty/project/legacy/documents —8native cases/18PNG,4game children reaped;
legacy по8commands/3PNG/2children. Validation0errors/0warnings, UI sessions/
mappings/GPU resources/handles0, selected project owners closed.45Editor source/
8binary fingerprints и PNG проверяются aggregate script. Итог:
TestResults/DocumentsUi-20261023/verification.json; logs/TRX/JSON рядом.

Первый consumer attempt остановился до запуска documents profile: helper неверно
вставил чтение documents.json перед процессом. Script исправлен уникальным
line-anchored match; это tooling failure, не приложение. Предварительные passing
solution/native runs до text/label и footer-capture правок сохранены отдельно;
актуальные fingerprints и результаты берутся только из указанных final runs.

ED2 single-project document profile закрыт, ED1 project/runtime transaction и
ED3–ED8 не закрыты. Нет editable Transform/Undo/Redo/preview integration, project
picker/file browser и generic modal/menu system. Runtime/engine/wire/UI-library
APIs/dependencies прежние; Editor non-packable. Fresh packages, independent
preview drawing, golden/RTE/full performance/physical input/loss/VRAM/K7 не
повторялись. Следующий срез — Transform inspector/Undo/Redo и independent preview.

## ED2a: документ сцены и persistence — 2026-10-23

Реализован [document/persistence profile](EDITOR_PLAN.md#ed2a-documentpersistence-api--2026-10-23).
SceneFileCodec строго сохраняет raw SceneDocument/catalogs/patches/IDs и tagged
scalar values, валидирует через owned engine factory product. JSON formatVersion1,
UTF8 optional BOM≤4MiB/depth32, finite collection limits; unknown/duplicate members,
invalid graph/scalar/version и превышение Encode/Decode bounds отклоняются.
Новых engine/wire/UI-library contracts/dependencies нет.

UseAetherAuthoring → UseAetherEditorDocuments → UseAetherEditorProjects создаёт
App-local shared codec/store/factories и owned ProjectSession→SceneDocumentSession→
ISceneAuthoringSession. Metadata-only UseProjects совместим. Editor не дублирует
committed state/history: dirty определяется saved ContentRevision, Undo/Redo
вызваны через engine API. Save captures immutable snapshot и оставляет новые edits
dirty. SaveAs failure/cancel не меняет выбранные path/stamp/checkpoint.
Close Save/Discard/Cancel fences/drains accepted IO, terminal Dispose явно forced
discard; custom store, игнорирующий token, не освобождается раньше завершения.
Project switch/Close блокируют dirty/busy document и атомарно закрывают clean
admission. Failed/rejected candidate cleanup завершается наблюдаемой ошибкой,
retained products остаются для retry; borrowed dependencies имеют одного App owner.

Json store пишет temp рядом, затем atomic rename/replace. SaveAs другого файла
create-only; существующий выбранный файл проверяется по SHA256 наблюдавшихся bytes.
Persistent .write-lock exclusive handle согласует cooperating writers. Проверены
change с тем же timestamp, missing target, locked commit failure, temp cleanup,
точные fingerprints, повтор после ошибки. Profile optimistic/trusted local:
arbitrary external rename между check/replace, filesystem links и power-loss
durability не являются гарантиями. Store API не обещает filesystem CAS.

### Требования и прямые тесты

Новый набор —58методов/143case: codec67/store12/document35/DI21/CLI8.
Assertion/gap review проверил concrete output/state/failure/ownership assertions;
production mutations не применялись. Дополнены independent Encode overflow,
CloseSave после edits во время старого save и rejected project cleanup completion.
При раннем чтении codec добавлена cancellation check после factory validation;
первые test-authoring failures исправлены до final narrow run, production defects
по ним не выводились. Research/plan/review/matrix находятся в OS temp вне Git.

| Требование | Exact test evidence в TrueMoon.Aether.Editor.Tests.Documents |
| --- | --- |
| Raw catalogs/inheritance/masks/restores/parents/scalar kinds | RoundTrip_PreservesRawCatalogsInheritanceMasksRestorationParentsAndExactTaggedScalars |
| Strict format и независимые Encode bounds | Decode_RejectsUnknownDuplicateMissingAndWrongCaseMembersAtEveryStructuredLevel; Encode_RejectsOversizedLocalCollectionsEvenWhenTheEngineCanResolveTheirContent |
| Conflict без timestamp и atomic failure | Replace_ExternalContentChangeWithIdenticalTimestampIsConflictAndRetryRequiresNewObservedStamp; Replace_FailedAtomicCommitPreservesOriginalAndCleansPreparedTemporaryForRetry |
| Dirty по content checkpoint / Undo / reload | NewSaveEditUndoRedoAndReload_UseContentCheckpointRatherThanMonotonicDocumentRevision |
| Captured save и следующий snapshot при Close Save | Save_CapturesImmutableSnapshotAllowsEditsAndDoesNotMarkNewerContentClean; CloseSave_AfterEditDuringPendingSavePersistsTheNewerSnapshotBeforeReleasingDocument |
| Ignored cancellation / late completion / ownership | CloseDiscard_CancelsAndDrainsIgnoredLoadBeforeDisposingActiveAndNeverSelectsLateCandidate; Dispose_FencesAndDrainsIgnoredSaveWithoutLateCheckpointOrDoubleRelease |
| Cobalt/MS DI, dirty/busy project guard, App drain | ProjectOwnedDocuments_BlockDirtySwitchAndNormalCloseUntilExplicitSaveOrDiscard; ProjectTransition_RefusesCleanDocumentWithAcceptedIoThenFencesOldOwnerAtSuccessfulSwitch; AppStop_ForceDiscardsDirtyDocumentsButDrainsAcceptedSaveBeforeBorrowedStoreDisposal |
| Rejected candidate cleanup / retry | ProjectCandidateCleanupFailure_CompletesRejectedOpenAndRetainsOwnedCandidateUntilCloseRetry |
| Explicit headless scene path | CheckScene_PreservesScenesRelativePathAndExplicitManifestWithoutSelectingLegacyRuntime |

Final narrow command:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Documents. --logger trx --results-directory TestResults/Documents-20261023/narrow-final --verbosity quiet
```

143/143 passed, failed/skipped0. TRX:
TestResults/Documents-20261023/narrow-final/MEGA6_PH-DEV_2026-10-23_18_09_21_net10.0.trx.

### Full solution и опубликованное приложение

TestResults/Documents-20261023/VerifySolution.ps1: nonincremental Debug solution
build0errors/1531warnings; full Debug и Release **2804/2804** каждый,10TRX/config,
failed/skipped0. Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
UI661/Hosting85/Aether1008/Editor284. Debug test --no-build после full build;
Release test собирает актуальную конфигурацию, blame-hang60s.229source fingerprints
проверены до/после, включая все новые tests и Editor production files.

TestResults/Documents-20261023/VerifyConsumers.ps1:
consumers-131340219/summary.json Completed=true. Source Debug и published Release
app/project/CWD вне repository; shipped Game.Logic отсутствует.17commands:
publish1, headless10, native6. Шесть exit1 ожидаемы: missing SDK, unsupported scene
version и path traversal в каждой delivery; остальные exit0.
Rich scene содержит2entities/3definitions/1schema, stable IDs/revision12,
inheritance/remove/restore/parent links, long/number/Boolean/Unicode/null values.
--check-scene load→encode сохраняет исходный SHA256, GameLoaded=false,
SessionClosed/DocumentClosed/ActiveCleared=true, IsDirty=false. Невалидный XML
game csproj доказывает отсутствие MSBuild evaluation при project open.

Native empty/project/legacy:6cases/10PNG, workspace без compiler/child, legacy
по8commands/3PNG/2reaped children. Vulkan validation0/0, UI sessions/mappings/
GPU resources/handles0, selected project owner closed.39Editor source/8binary
fingerprints и все PNG перепроверены. Aggregate:
TestResults/Documents-20261023/verification.json; scripts/logs/TRX сохранены рядом.

Published scene check read-only; atomic writes/Save/reopen проверены direct
real-filesystem tests, а не опубликованным оконным Save workflow. Normal Editor
UI пока не вызывает Load/Save/authoring/preview, остаётся read-only. Четыре ED2
задачи закрыты; общий UI Save/Discard/Cancel и runtime switch/close ещё открыты.
Fresh package runs не нужны для non-packable Editor; engine packages, independent
preview drawing, golden/RTE/full performance/physical UI/real loss/VRAM не повторены.
Следующий срез — document commands/close policy в UI, затем Transform/Undo/preview.

## ED1a: project/session и запуск вне repository — 2026-10-23

Реализован [первый project/session profile](EDITOR_PLAN.md#ed1a-первый-projectsession-profile--2026-10-23).
UseAetherEditorProjects подключает независимые от UI/engine App-local store,
SDK preflight, typed session factory и одного App-owned EditorProjects. Products
принадлежат coordinator; borrowed store/toolchain/factory освобождаются своим DI
owner после Stop/drain. Lifecycle aliases non-disposable, duplicate attachment
отклоняется до activation. Cobalt default и Microsoft DI сохранены.

Manifest .aetherproj schema1 — strict UTF8 JSON с optional BOM, stable project ID,
game csproj/entry и relative scene/content roots. Максимум64KiB вместе с BOM;
неизвестные/повторные/case-mismatched поля, invalid version/metadata/paths и missing
files/roots отклоняются. Resolve использует manifest directory, не CWD; это
trusted-local lexical containment, не security sandbox поверх filesystem links.
Create работает с существующим игровым csproj/roots: после SDK preflight пишет
temporary file и atomic rename без overwrite. При cancel после file publication
валидный manifest остаётся на диске, cancelled candidate не становится active.

SDK preflight выполняет owned dotnet --version из game directory, учитывает
global.json, требует SDK10+ и не оценивает MSBuild/entry. Deadline15s отменяет
ожидание и kill/reap/drain probe; совместимость игры проверяется explicit Build.
ProjectSession хранит metadata/SDK identity и формирует GameBuildRequest.
Open/Create fail-fast при concurrent transition. Failed/cancelled candidate не
заменяет active; Close cancels/drains accepted work до закрытия active, reusable
после успешного Close; App shutdown закрывает admission окончательно. Late
completion от игнорирующей token зависимости не публикуется.

CLI: без аргументов пустая workspace; --project .aetherproj выбирает project/build
identity без demo и без загрузки scene document. --check-project запускает тот же
App/Cobalt API без окна и выводит JSON preflight/cleanup. Explicit --project
game.csproj сохраняет legacy demo/game smoke. Search repository/demo defaults удалён.
Редактор по-прежнему Fluent View/Data/Style; новые store/toolchain API — реальные
IO/process boundaries, generic framework/plugin/scopes не введены.

### Requirement / test evidence

Новых92case/35методов: Store40, coordinator15, registration10, launch23, session4.
Только6новых test files в TrueMoon.Aether.Editor.Tests/Projects; старые tests не менялись.

| Требование ED1a | Точные tests |
| --- | --- |
| Versioned portable manifest и canonical paths | `Resolve_NormalizesContainedPathsAndNameWithoutChangingPortableInput`; `WriteNew_RoundTripsStrictPortableMetadataAndLeavesNoTemporaryFiles` |
| Strict schema, required fields и finite bytes/text bounds | `Read_RejectsInvalidJsonShapeOrStrictMemberContract`; `Read_RequiresEveryVersionOneMemberInsteadOfAcceptingDeserializerDefaults`; `Read_EnforcesUtf8ByteLimitIncludingExactMaximum`; `Resolve_PreservesLastAllowedTextLengthAndRejectsFirstExcess` |
| Optional BOM считается в64KiB | `Read_AcceptsUtf8BomAtByteLimitButCountsItWhenRejectingFirstExcessByte` |
| Relative containment и missing files/roots | `Resolve_RejectsEachEscapingOrRootedPath`; `Resolve_RequiresExistingGameAndRootsWithoutCreatingThem` |
| Atomic new manifest без overwrite/temp leaks | `WriteNew_ExistingFileIsPreservedAndTemporaryOutputIsRemoved`; `WriteNew_RevalidatesInputAndRecomputesAbsolutePathsBeforeWriting` |
| Create preflight/write до active commit | `Create_PublishesManifestBeforeChangingActiveAndOwnsUnpublishedCandidate` |
| Failed/cancelled candidate сохраняет active/original error | `FailedCandidate_PreservesActiveAndOriginalErrorThenAllowsRetry`; `Cancellation_IgnoredByDependencyStillPreventsLateCandidatePublication` |
| Switch closes previous product, borrows dependencies | `Open_CommitsValidatedFactoryProductThenClosesOnlyPreviousSession`; `Dispose_ClosesAdmissionImmediatelyAndDrainsUnpublishedFactoryProduct` |
| Close fences/drains ignored cancellation, затем можно reopen | `Close_FencesAndDrainsIgnoredCancellationBeforeClosingActiveAndCanReopen`; `Close_ObservesCancellationCallbackFailureAndStillDrainsOwnedWork` |
| App lifecycle/providers/overrides, без двойного ownership | `App_PreservesInjectedStoreToolchainAndFactoryAndDisposesEachDependencyOnce`; `App_StopClosesAdmissionAndDrainsPendingOpenBeforeDisposingBorrowedDependencies` |
| Empty launch без implicit demo, guarded game commands | `EmptyLaunch_SelectsNoImplicitDemoAndNoProjectWorkspaceDisablesEveryCommand`; `WorkspaceSmoke_AllowsEmptyOrManifestWorkspaceWithoutSelectingLegacyGame` |
| SDK guards и explicit build mapping | `DefaultToolchain_PreCancelledAndMissingProjectDoNotStartAnSdkProbe`; `BuildRequest_MapsCanonicalProjectAndEntryWithoutOwningBorrowedMetadata` |

Final focused command, exit0, Passed92/Failed0/Skipped0:

~~~powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Projects. --logger trx --results-directory TestResults/ED1a-20261023/narrow
~~~

TRX MEGA6_PH-DEV_2026-10-23_17_23_38_net10.0.trx. Первый run90cases дал87pass/3fail:
oracle ошибочно требовал exact JsonException вместо JsonReaderException subtype;
исправлен только assertion на assignable JsonException. Затем90/90, после optional
BOM и invalid DLL basename partitions final92/92. Source compile corrections:
missing Resolve/ReadbackOpenGLImage using и shadowed presentation-options lambda;
устранены до passing runs. Это не production behavioral RED.

260assertion sites: точные metadata/paths, original exceptions, ownership counts,
candidate ordering, forced race gates и immutable input. Quality review Strong;
actual mutation/coverage не заявлены. finding-tests IDE tool недоступен (Tool not
found). Один static pairing attempt остановлен NETSDK1004 на file-app cache
assets; installs/повторов/claims об отсутствии тестов не было. Research/plan/status
хранятся в OS temp TrueMoon-ED1a-tests-4bba6a9376ee44df814edb49c7889edc, не в Git.

### Full source, native и published validation

Final full solution2661/2661 Debug и2661/2661 Release,10TRX/configuration,
failed/skipped0: Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
UI661/Hosting85/Aether1008/Editor141. Nonincremental Debug0errors/1531warnings.
Current source hashes216; evidence:
TestResults/Projects-20261023/solution-final/summary.json, Completed=true.
Initial solution2661/config и initial consumers-122603087 также passed; после
визуальной проверки заменён внутренний project GUID в пустой scene label на
«Сцена не открыта»/«Проект не открыт», поэтому final runs выполнены заново.

~~~powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Projects-20261023/solution-final/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Projects-20261023/solution-final/Release
~~~

VerifyConsumers.ps1 сохраняет exact executable/arguments/cwd/exit codes и
process-local validation paths. Final native summary:
TestResults/Projects-20261023/consumers-123338696/summary.json.
11commands:1Release publish,4headless и6native cases. Published executable,
fonts/native assets и пользовательский project находятся в отдельном OS temp
вне repository, без Game.Logic.dll. Оба app deliveries запускаются с outside CWD.

- Headless Debug/published Release успешно читают один manifest с намеренно
  невалидным XML csproj: это доказывает отсутствие MSBuild evaluation/build при
  Open. SDK identity видна в JSON, selected session закрыта/active очищен,
  game assembly не загружена. Затем local global.json с999.0.100/rollForward:disable
  даёт ожидаемый exit1 и понятную SDK/PATH/global.json диагностику.4cases total;
  environment PATH/system SDK/registry не менялись.
- Empty workspace и .aetherproj workspace Debug/published Release:4native cases,
  по1PNG,0entities/0compiler/0game children; Build доступен только с project,
  остальные game actions недоступны. Standard-frame close закрывает project owner
  и UI; UI sessions/mappings/GPU resources/handles0.
- Legacy game workflow Debug/published Release: каждый8commands/3PNG/2reaped
  children, Build→Start→Play→Pause→Step→Build→Replace→Stop, selection/theme/resize
  и native close сохранены. Vulkan validation enabled0errors/0warnings и zero
  owned resources. Workspace cases не заявляют Vulkan validation, child там нет.
- Всего6native cases/10PNG/4reaped game children;32Editor source и8binary hashes
  сверены. PNG inspected для project workspace; показывается empty scene.
  Путь manifest/game/scenes/content и process profiles проверены независимо от repo CWD.

Итог TestResults/Projects-20261023/verification.json содержит source/binary/PNG
hashes,20TRX,focused92,local links/namespaces/checklist. Engine/runtime/wire и
production UI-library APIs не менялись; Editor IsPackable=false. Fresh packages,
independent preview drawing, full golden/RTE/performance/VRAM/physical UI и
hardware/final alpha gates этим шагом не повторялись. Packages не публиковались.
K7 workaround search/Windows/driver policy прежние.

Следом ED2 scene schema/document owner/Load/Save/SaveAs/dirty checkpoint и
Save/Discard/Cancel. Общий document/runtime switch/close transaction остаётся
открытым ED1 пунктом; интерфейс Open/menus — ED4, preview/game coordination — ED6.
Create пока оборачивает существующую игру; scaffold/wizard/user preferences позже.

## Edit preview: независимый engine profile — 2026-10-23

Следующий шаг после ARCH1b.1 — отдельный Edit preview без компиляции/игры.
[Контракт и пределы](PLAN.md#edit-preview-первый-engine-profile--2026-10-23).
AetherHost owns ScenePreview через injected IFactory<ScenePreview,FrameSession>;
Preview заимствует Frames. RuntimeRole.EditPreview выбирает PreviewId из Aether
assembly, built-in PreviewGameSession и тот же App runtime host. GameBuildLoader
при этом не создаётся, Game.Logic не читается/не загружается. Game entry/selected
build workflow сохраняется, unknown/несогласованный role/bootstrap отклоняется.
Новых assembly/protocol versions/DI scopes/IPC методов не добавлено.

ShowAsync сначала валидирует extent/transport/projection/revision/ownership.
Уже активная game session требует явного Stop. После preflight прежний owned
preview проходит drain/reap, новая generation получает immutable primitive scene.
Камера фиксирована, tick0; gameplay input/Step/Play отклоняются. Readiness —
фактический presented ticket exact runtime/surface/document revision. Describe/
Presented не переименовывают старый frame. Transform/Undo/Redo требуют явного
ShowAsync нового resolved snapshot. Resize/suspend/resume сохраняют document,
положительный resize снова ждёт presented ticket. Stop/Dispose fence и закрывают
admission до ожидания inflight work; original+cleanup causes доступны, retry
сохраняет ownership. Неиспользованный Preview не останавливает game workflow.

Это GPU-only Windows x64/OpenGL UI/Vulkan child profile, bounds16..1920×16..1080.
CPU reference pattern не представляет сцену и отклоняется до смены session.
Каждая revision заменяет child; incremental World/live edit не вводятся.
Normal Editor UI пока не подключён: native proof использует существующий
ViewportView fixture, Editor runtime bootstrap лишь научился маршрутизировать role.

### Requirement / test evidence

Новых121case: PreviewGameSession9, registration/ownership51, ScenePreview25,
GPU shutdown36. Оба providers включены там, где проверяется App graph.
Ни один существующий test не изменён.

| Требование | Точные тесты |
| --- | --- |
| Immutable render scene, transforms/materials, fixed camera/tick | `Load_CopiesPrimitiveTransformsMaterialsAndKeepsEarlierRenderSnapshots`; `Load_MaximumPrimitiveScenePreservesEveryOrderedPrimitive` |
| Revision привязана к actual presented ticket; authoring не меняется | `Show_BindsRevisionToBuiltInRuntimeAndActualPresentedTicketWithoutChangingAuthoring` |
| Transform/Undo/refresh заменяют children после reap, stale frame не retag | `Show_TransformUndoAndRefreshReplaceChildrenAfterReapAndNeverRetagOldFrames` |
| Preflight сохраняет рабочий preview | `Show_InvalidProjectionPreservesWorkingChildAndPresentation` |
| Cancellation до viewport allocation; разрешён retry | `Show_CancelledDuringSceneLoadReapsBeforeAnyViewportAllocationAndAllowsRetry` |
| Cleanup failure удерживает child до explicit Stop retry | `Show_FailedDeliveryCleanupRetainsChildUntilExplicitSuccessfulStopRetry` |
| Stop fences ignored-cancellation configure/presentation до фактического drain | `Stop_FencesLateConfigureAndPresentationUntilActualInflightShowHasDrained` |
| Borrowed Frames/game workflow остаётся рабочим | `Dispose_ClosesPreviewAdmissionButLeavesBorrowedFramesUsableForGameWorkflow`; `Show_DoesNotTakeOverOrStopAnExplicitGameSession` |
| Runtime factory не активирует loader; host rollback/borrowed dependencies | `RuntimeHost_UsesInjectedPreviewProductWithoutCreatingGameLoaderAndOwnsOnlyProduct`; `AetherHost_FailedLaterActivationRollsBackOwnedPreviewAndTransportWithoutStartingChildren` |
| Обычный Game role сохраняет Step/Play | `Protocol_DefaultGameRoleRetainsStepAndPlayBehavior` |
| Clean EOF только перед полным header; partial frame — fault | `BoundaryRead_CleanEofIsNullWhileStrictReadStillRejectsEofAndCancellation`; `BoundaryRead_TruncatedHeaderIsNeverCleanShutdown`; `BoundaryRead_TruncatedPayloadIsNeverCleanShutdown` |
| Malformed length/JSON и null JSON не трактуются как EOF | `BoundaryRead_InvalidLengthRemainsAProtocolFailure`; `BoundaryRead_MalformedOrUnmappedJsonRemainsAJsonFailure`; `BoundaryRead_NullJsonIsNotAnEndOfStreamMarker` |
| Только exact Win32 pipe-closed HRESULT; callback IO fault не подавляется | `PeerClosedFilter_AcceptsOnlyExactWin32PipeClosureHResults`; `Producer_CallbackIoFailureAlwaysFaultsWithOriginalIdentityRatherThanBecomingPeerShutdown` |
| Реальный suspended named pipe disconnect даёт successful worker и zero resources | `Producer_SuspendedClientDisconnectCompletesNormallyAndReleasesAllOwnedWork` |

Assertions используют exact scene/revision/identity/ticket, distinct matrices/colors,
original exception identity и resource ownership counts; races управляются gates,
bounded waits означают failure при timeout. Static assertion/gap review выполнен;
empirical mutation run не заявлен. finding-tests tool был недоступен, применены
existing sources и одно static Roslyn pairing исследование; это не coverage report.

### Команды и текущий full source run

Из src выполнены (exact invocation records и binlogs рядом с evidence):

~~~powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Preview.|FullyQualifiedName~TrueMoon.Aether.Tests.Gpu.GpuSurfacePipeTests' --logger trx --results-directory TestResults/EditPreview-20261023/shutdown-narrow '-bl:TestResults/EditPreview-20261023/shutdown-narrow-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/EditPreview-20261023/solution/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/EditPreview-20261023/solution/Release
~~~

Final focused134/134 =121новыйcase +13существующих GpuSurfacePipe cases,
failed/skipped0, exit0; TRX shutdown-narrow/MEGA6_PH-DEV_2026-10-23_15_57_59_net10.0.trx.
Предыдущий focused85/85 сохранён в narrow-final; сначала82case, затем добавлены
host rollback на обоих providers и early cancellation. Начальная compile ошибка
fixture (legacy IFactory.Create<TData> bridge) исправлена в новых tests; production
RED этим не заявлен.

Nonincremental Debug build0errors/1542warnings. Full solution **2569/2569 Debug и
2569/2569 Release**,10TRX/configuration, failed/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor49. Solution helper VerifySolution.ps1, summary.json,
20TRX/build logs и188source hashes — в TestResults/EditPreview-20261023/solution.
Warnings не подавлялись; результат не объявляет warning-free build.

### Native drawing и defect/failure history

Текущий final evidence: TestResults/EditPreview-20261023/consumers-110233808/
summary.json, Completed=true, Failure=null,6commands/4native cases/140source hashes.
VerifyConsumers.ps1 сохраняет exact exe/arguments/cwd/exit и process-local validation
environment, timeout/owned-child handling. Два publish commands, четыре app runs:

- Preview Debug apphost и published Release: по6PNG/5cooperatively reaped children.
  Revisions7→8→9→10, Transform x−1→+1, Undo→−1, Redo→+1; actual GL readback red
  centroid .41439→.59→.41439→.59. Затем resize800×450, suspend0×0,
  resume320×240, stale tickets/lower revision и close при pending first-present.
  Оригинальные640×360 screenshots/scene authoring сохранены, tick0, compiler не
  запускался, Game.Logic не загружался. Published app скопирован в OS temp вне
  repository без Game.Logic.*; report.NoGameFile=true.
- Runtime role1/buildnull/restorefalse проверены в пяти child reports каждого run.
  Vulkan validation enabled0errors/0warnings; created/released devices/surfaces/
  slots совпадают. Child stderr пуст, owned resources/handles/mappings/UI sessions0,
  imported image readback для delivery отсутствует.
- Прежний Editor Debug/published Release smoke: каждый8commands/3PNG/2reaped
  children, validation0/0 и zero owned resources. Это регрессия прежнего game
  workflow; она не заявляет integration нового preview в ordinary Editor UI.

Native debugging preserved in native-debug-*:
104558927 не прошёл из-за отсутствующего VK_LAYER_PATH; исправлен только environment
дочернего процесса с существующим TestResults/ENG4b/validation-layer/Bin.
104744243 и105027162 получили6PNG/5reaped children/zero resources, но child stderr
зафиксировал broken pipe на Retired reply и EndOfStream на следующем wire read.
Это actual RED: normal parent disconnect faulted producer и давал child exit1.

GpuWire получил отдельное nullable boundary read API: null только EOF до первого
байта четырёхбайтового header; strict ReadMessageAsync не изменил EOF semantics. Producer
нормально завершает read/write при exact HRESULT 0x8007006D/0x800700E8/0x800700E9,
не подавляя arbitrary IO/callback/native/protocol/cleanup errors. DestroySurface
остаётся в finally. Run105416402 прошёл после исправления, но его binary предшествует
final facility-prefix tightening; текущим proof считается только consumers run
после final full rebuild выше.

Consumers helper сначала искал cleanup.json вместо report.json; затем resume
упёрся в повторный Assets copy. Исправлены только helper paths/idempotent resume,
проверены прежние source hashes и сохранены только complete exit0 reports; недостающий
editor-published-Release и package proof выполнены. FailureHistory и stdout logs
сохранены, предыдущие failures не переписаны в passing results.

### Fresh packages и границы вывода

VerifyPackage.ps1 + PackageProgram.cs.txt выполнили22commands и8/8cases:
Debug/Release × build/publish × Cobalt/Microsoft DI. Version
0.2.2.7-alpha.preview.20261023110622747,9fresh nupkgs из10producer projects
(Cobalt generator входит в Cobalt);301source hashes.8runtime TrueMoon libraries
и build-only Enerit.Generator проверены в isolated feed/cache, consumer не имеет
ProjectReference. Actual UseAetherAuthoring + UseAether + Host.Preview используют
Apply/Show/stale ticket/Undo/Redo/suspend/resume/Stop,4owned runtime leases и
zero compiler activation; fake GPU presenter/runtime — managed API/composition
proof. Native drawing относится к отдельным apphost/published runs, не к package
consumer. Summary Completed=true:
TestResults/EditPreview-20261023/package-20261023-110622714/summary.json.
Итоговая read-only сверка source hashes/package payloads/docs/checklist —
TestResults/EditPreview-20261023/verification.json. Публикации не было.

ED1–ED8 checklist остаётся8stages/40unchecked tasks. Следом ED1a project/session
и ED2 document/save/reopen, затем подключение готовых authoring/preview к ordinary
Editor UI; ED5 tools/camera/picking ещё предстоят. Полный World/live edit/MCP,
golden/RTE/performance/VRAM/hardware/physical UI и alpha criteria этим не закрыты.
Windows/driver settings не менялись; поиск K7 workaround остаётся остановленным.

## ARCH1b.1: headless Transform authoring и история — 2026-10-23

По запросу «давай следующий шаг» реализован первый локальный API срез
[ARCH1b.1](PLAN.md#arch1b1-локальная-transform-authoring-session--2026-10-23).
UseAetherAuthoring регистрирует shared resolution и typed IFactory; отдельная
ISceneAuthoringSession владеет immutable document, resolved state, bounded
Undo/Redo и non-evicting terminal receipts. Ни окна, ни запущенный Runtime для
редактирования не нужны. UseAether сохраняет существующий graph; scene resolution
registration вынесена в общий helper для композиции в любом порядке.

Atomic Transform batches адресуют entity/component IDs, используют expected
revision/operation GUID/actor, проверяют local values, exact schema и полностью
resolved parent-composed transforms до single commit. Старые query snapshots,
catalogs и unrelated patches сохраняются. Undo/Redo увеличивают document revision
и восстанавливают ContentRevision. Limits/redo/duplicate/cancel/Dispose contract
подробно зафиксирован в PLAN и README/PACKAGE, а не продублирован в UI.

| Требование первого среза | Прямое evidence, namespace TrueMoon.Aether.Tests.Scenes.Authoring |
| --- | --- |
| Чтение и атомарное изменение всех9свойств по ID, immutable snapshots и unrelated data | SceneAuthoringSessionTests.Apply_PublishesCoherentNinePropertyBatchByStableIdsWithoutMutatingEarlierSnapshots; Apply_InstanceAddedTransformRetainsAddedDataAndRestoredComponentPresence |
| Invalid2nd target/duplicate/hidden/unsupported и invalid numbers/scales не публикуют часть batch | SceneAuthoringSessionTests.Apply_InvalidSecondTargetRejectsEntireBatchBeforeResolution; Apply_InvalidTransformRejectsWithoutPartialPublish |
| Exact schema, весь parent graph и float singular/overflow проверяются до commit | SceneAuthoringSessionTests.Resolution_ExactTransformSchemaRejectsFailedCreationWithoutPoisoningLaterValidEditing; Apply_WholeHierarchyValidationRejectsLocallyFiniteOverflowAtomically; Apply_FinitePositiveScalesWithSingularFloatDeterminantRejectAtomically |
| Cancel Apply/Undo/Redo до commit и unexpected failure не меняют content/history/receipt; retry безопасен | SceneAuthoringSessionTests.Cancellation_DuringCandidatePreparationPublishesNothingAndSameIdCanBeRetried; Preparation_UnexpectedFailurePreservesExceptionIdentityStateAndRetryableOperationId; Cancellation_PreCancelledNewIntentHasNoReceiptAndLateCancellationCannotUndoCommit |
| Revision conflict/concurrent requests и exact/changed operation ID | SceneAuthoringSessionTests.ConcurrentSameRevision_OneAtomicBatchWinsAndOneConflicts; Operations_RevisionConflictIsCachedAndDoesNotBecomeSuccessAfterStateCatchesUp; Operations_ExactRetryReturnsOriginalReceiptButChangedIntentConflicts |
| Bounded history, monotonic revision, restored checkpoint, new edit clears redo и zero-history profile | AuthoringHistoryTests.History_BoundsCombinedUndoRedoAndRestoresContentWithMonotonicDocumentRevisions; History_NoOpApplyIsTransactionAndClearsRedoWhileZeroLimitDisablesHistory; Revision_AtLongMaximumRejectsWithoutWrappingOrChangingContent |
| Journal at capacity сохраняет прежние success/error receipts; Undo/Redo retries не повторяют мутацию | AuthoringHistoryTests.Ledger_AtCapacityRejectsNewIdsWithoutEvictingSuccessOrErrorReceipts; Ledger_ExpectedEmptyHistoryErrorConsumesReceiptButCancellationDoesNot; History_ExactUndoRedoReceiptsAreStableAcrossLaterEditsAndCancelledRetries |
| Limits и frozen request boundaries, independent/closed products, failed creation и borrowed ownership | AuthoringContractsTests.Batch_FreezesCallerOrderAndAcceptsExact4096Boundary; Operation_RejectsInvalidIdentityRevisionAndActor; AuthoringFactoryRegistrationTests.Factory_CopiesEveryOptionBeforeCallerMutationAndProductsHaveIndependentOperationBudgets; Factory_ProductsHaveIndependentHistoryAndSnapshotsAndCloseWithoutDisposingBorrowedResolution; Factory_InvalidInitialScenePublishesNoSessionAndAllowsLaterValidCreation |
| App options snapshot/duplicate/lazy startup и Cobalt+Microsoft DI, UseAether в любом порядке | AuthoringFactoryRegistrationTests.App_CopiesAllAuthoringOptionsDuringConfigurationBeforeFactoryResolution; App_DuplicateOrExistingSessionFactoryFailsBeforeActivatingServices; App_AuthoringAloneProvidesSingletonFactoryAndIndependentSessionsWithoutStart; App_AetherCompositionInEitherOrderSharesInjectedResolutionWithoutCreatingRuntime |
| Authoring не связан с preview limit64/material policy | SceneAuthoringSessionTests.Apply_HeadlessAuthoringDoesNotImposePreviewEntityOrMaterialBounds |

Focused final run: **Passed:87, Failed:0, Skipped:0**, exit0:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.Authoring. --logger trx --results-directory TestResults/ARCH1b-20261023/narrow-clean '-bl:TestResults/ARCH1b-20261023/narrow-clean-{}.binlog'
```

TRX: TestResults/ARCH1b-20261023/narrow-clean/MEGA6_PH-DEV_2026-10-23_15_17_57_net10.0.trx.
Первый prefix run130содержал84новых cases и46старых AuthoringModelTests; после
review добавлены3прямые проверки, final87 использует строгий namespace filter.
Промежуточный exact-schema test намеренно подменял каталог injected resolver
вопреки SceneDocument: это ошибочный collaborator oracle, не поддержанный contract.
Оставлен корректный failed initial schema/Create и subsequent valid editing proof;
провал87run(86pass/1fail) не выдаётся за production defect или passing evidence.
IDE findTests недоступен; использованы repository tests и один bounded Roslyn
pairing scan. Static pairing не означает coverage; coverage/performance не измерялись.

Финальный nonincremental Debug solution build: exit0,0errors/1522warnings.
Full Debug **2448/2448** иRelease **2448/2448**,10TRX/configuration, failed0/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether887/Editor49.139Aether production/test source fingerprints не изменились
во время проверки. Команды helper TestResults/Authoring-20261023/VerifySolution.ps1:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Authoring-20261023/solution/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Authoring-20261023/solution/Release
```

Fresh package proof: **8/8** = Debug/Release × build/publish × Cobalt/Microsoft DI.
Созданы9nupkgs из10producer projects, isolated feed/cache и8runtime TrueMoon
package dependencies; Enerit.Generator нужен producer, не transitive runtime
consumer. Version0.2.2.7-alpha.authoring.20261023102244607;22commands,299source
fingerprints, только PackageReference без ProjectReference. Consumer реально
выполняет Apply/conflict/duplicate/Undo/Redo/Dispose и App start/stop без runtime.
Final Completed=true:
TestResults/Authoring-20261023/package-20261023-102244573/summary.json.
Helper: TestResults/Authoring-20261023/VerifyPackage.ps1; ничего не публиковалось.

Два предварительных package runs сохранены: package-20261023-102016044 остановился
на неверном helper ожидании9runtime libraries вместо8 (build-only generator);
package-20261023-102117026 — CS1061 из-за пропущенного using TrueMoon.Dependencies
в consumer example. Исправлены проверочный helper и README/example, production
API не менялся. Passing evidence — только указанный final fresh run.
Финальная сверка:9Markdown documents/498local links/260anchors без ошибок,
11new source namespaces соответствуют путям, git diff HEAD --check прошёл.
ED1–ED8/40unchecked tasks сохранены.139solution и299package fingerprints совпали
с текущими исходниками;32сравнения deployed DLL с lib/net10.0 payload из nupkgs
прошли. Aggregate Completed=true: TestResults/Authoring-20261023/verification.json.

Native viewport и physical UI этим API срезом не изменялись/не повторялись.
ARCH1b в целом, ED1/ED2 persistence, editable UI, независимый preview и live World
не завершены. Следующий engine шаг — Edit preview, ED1a project/session параллельно.

## API перед UI и выбор Enerit IPC — 2026-10-23

По обсуждению с пользователем уточнён порядок первого рабочего среза Editor:
общие project/scene contracts → минимальный ARCH1b scene query/Transform mutation/
history/Undo/Redo → headless validation/conflict/duplicate/cancel/atomicity proof →
независимый Edit preview → подключение editable inspector/tools. ED1a project/
session выполняется параллельно. Полный generic World/ECS и live editing ARCH1c
не являются предварительным условием. Authoring/history имеют общего владельца
в Aether, Editor не создаёт вторую mutation/history реализацию; Play автоматически
не записывает изменения обратно в документ.

Enerit явно выбран для IPC Editor↔Runtime: typed invocation/generated adapters,
same-build named pipes как текущая основа, control/input/query/results/progress/
errors и frame descriptors. Pixels/shared GPU images остаются отдельным frame
transport; launch/stop/reap — у Mithril. Новые preview/edit invocation contracts
запланированы, wire versions/limits и transport readiness этим не расширяются.

Синхронизированы engine/editor, Enerit, общие и UI планы/статусы. Изменена только
документация; production code/API не менялись, build/tests/native/packages не
запускались. Проверены14documents/428local links/16added anchors: missing targets/
anchors0, ED1–ED8/40unchecked tasks сохранены, source changes0, git diff --check
прошёл. Evidence: TestResults/AetherApiPlan-20261023/verification.json.
Предыдущие passing результаты сохраняют свои даты и область вывода.

## План рабочего приложения Editor — 2026-10-22

По согласованному обсуждению создан EDITOR_PLAN.md в направлении Aether.
ED1–ED8 охватывают project/session, document persistence, общие authoring operations
и undo, панели/inspector/commands, Edit preview/tools, game workflow, extensions и
recovery/delivery. Первый цельный срез: Open scene → Transform edit → preview →
Undo/Redo → Save → reopen. Следующий конкретный шаг — ED1a project description и
ProjectSession; gameplay collisions сохраняют отдельную точку продолжения.

Зафиксированы функциональная организация кода, единый owner документа/history,
View/ViewModel/style и App/DI factory lifetime, различие UI commands/authoring
mutations, draft/commit/cancel и отдельные authoring/runtime states. Editor
extensions и custom UI controls имеют разные границы; интерфейсы вводятся по
первым потребителям, dynamic plugin loader/полная замена Element не обязательны.

Engine PLAN, общий PLAN/STATUS, UI PLAN/STATUS, индекс документации, README Editor
и AGENTS ссылаются на один прикладной checklist. Общие authoring contracts остаются
в ARCH1, controls/runtime UI — у Alloy/Argentis. Это документационная работа:
production code и public API не менялись, build/tests/native/packages не запускались;
предыдущие passing результаты сохраняют дату и область вывода. Проверяются
markdown links/anchors и git diff whitespace. Итоговая сверка:
11documents/374local links/5added anchors, missing targets/anchors0;
ED1–ED8 и40unchecked tasks присутствуют, неожиданных source changes0,
git diff --check exit0. Evidence: TestResults/EditorPlan-20261022/verification.json.

## Единые фабрики и DI — 2026-10-22

По следующему запросу пользователя после аудита реализованы DI1–DI4 в Aether,
Aether.Editor и используемых путях Enerit/Mithril/Alloy. Default Cobalt и Microsoft
DI adapter сохранены; runtime/generator Cobalt не менялись. Новые общие
IFactory<T,TArguments>, Factory adapters и AppServiceLifecycle находятся в Contracts,
без dependency на provider. Типизированные factories сохраняют legacy bridge;
аргументы проверяются до callback, ошибки callback не оборачиваются.

AetherHostFactory получает дочерние factories через DI, передаёт готовые
зависимости host и откатывает частичное создание в обратном порядке, сохраняя
original/cleanup causes. Aggregate остаётся единственным владельцем. App options
поддерживают typed viewport factory; runtime leases и CPU receivers также создаются
через IFactory. SceneResolutionService использует внедрённую catalog factory на
каждую revision; standalone SceneResolver adapter сохранён.

Editor workspace/binding/commands и owner-thread root factory регистрируются в App.
UseAetherRuntime после UseAether фиксирует launch, допускает factory overrides и
создаёт owned graph parent/game/producer/protocol/IPC. AetherRuntimeHost borrows
App/GPU, закрывает admission, отменяет/drains workers и освобождает игру после
завершения работы. Failed cleanup наблюдаем/retryable; aliases не владеют target.
Enerit имеет generic pipes factories/common App factory adapters. Mithril injects
UnitHandleFactory и snapshots; IUnitsController/lifecycle aliases — borrowed facades.
Alloy→Contracts добавлен для typed session bridge; Hosting предоставляет
UsePresentationFactory и non-owning service/lifecycle facades.

Game loader использует IFactory<IGameSession,Type>; штатный game entry реализует
IGameSessionEntry.static Create(IServiceResolver?), явно создаёт игру с constructor
dependencies, не используя Activator/constructor reflection fallback. Runtime
services заимствуются, game/context/artifact pin принадлежат loader. Generic static
dispatch не кеширует collectible types. Shared Aether и Contracts исключены из
private dependencies. HostId/ContractHash учитывают обе assembly. **Миграция:**
добавить entry factory и non-copying TrueMoonContractsPath reference наряду с
AetherContractPath; host/runtime/game и прежние artifacts пересобрать вместе.
Wire versions прежние. Manual game/project/compiler fixtures обновлены.

### Проверки и артефакты

Текущий output root: `TestResults/AetherDI-20261022-204227` (далее `$diDir`).
SDK10.0.402/net10.0/Windows x64. Все MSBuild commands сохранены с уникальными
binlogs; transient artifacts исключены из version control.

```powershell
$diDir = Get-Content TestResults/aether-di-current.txt
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory "$diDir/debug-verified" "-bl:$diDir/debug-verified-{}.binlog"
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory "$diDir/release-verified" "-bl:$diDir/release-verified-{}.binlog"
dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --configuration Release --output "$diDir/publish" "-bl:$diDir/editor-publish-{}.binlog"
```

Оба full runs exit0: **2293/2293/configuration**, по10TRX, failed0/skipped0.
Core117/Cobalt179/generator91/Enerit126/generator114/Mithril99/UI643/Hosting85/
Aether800/Aether.Editor39. Publish exit0. Добавлены40cases в17methods; read-only
assertion/gap review привёл к двум усилениям: реальный non-null runtime service и
независимый disposal counter frame-owned transport. Точные requirements/evidence:

| Требование | Проверка |
| --- | --- |
| Factory products caller-owned, typed/legacy/null/error contracts | FactoryTests.RegisteredAdapters_CreateSeparateCallerOwnedResults и typed bridge/argument/failure cases |
| Единственный disposable owner при lifecycle aliases | FactoryTests.BorrowedLifecycleAliases_ForwardAppCallsAndDisposeOwnerExactlyOnce |
| Injection и rollback aggregate с original+cleanup causes | AppIntegrationTests.RegisteredCompilerFactory_SuppliesHostDependencyAndTransfersOwnership; HostFactoryFailure_RollsBackAllAcquiredDependenciesAndPreservesCleanupErrors |
| Runtime precondition/duplicate/snapshot/overrides | RuntimeRegistration_RequiresAetherAndRejectsDuplicatesBeforeCreatingServices; RuntimeRegistration_UsesInjectedFactoryWithNormalizedLaunchSnapshot |
| Authoring factory на каждую revision | AuthoringResolution_UsesInjectedCatalogFactoryForEveryRevision |
| Game entry runtime DI/errors/pins | GameBuildTests.ExplicitGameEntry_InjectsBorrowedRuntimeServiceIntoCallerOwnedSessions; ExplicitGameEntry_PropagatesOriginalInjectedFailureAndReleasesPins; SelectedGameFactoryFailure_ReleasesPinsAndPreservesTypeAndError |
| Handle configuration snapshot/ownership/failure | UnitCompositionTests.InjectedHandleFactory_UsesSnapshotsAndControllerOwnsEveryResult |
| UI argument/clipboard/root/session ownership | HostedUiSessionFactoryTests.CommonFactoryBridge_PreservesCreationArgumentsAndSessionOwnership; CommonFactoryBridge_InvalidArgumentsDoNotTakeRootOwnership |
| Provider-neutral Alloy dependency boundary | CoreBoundaryTests.Alloy_AssemblyReferences_ContainOnlySharedContractsArgentisAndBcl |

Итоговые source Debug apphost и published Release запускались RunNative.ps1 с
`--smoke <fresh-directory> --validation`, VK_LAYER_PATH только в собственном
process tree. `native-debug-final`/`native-release-final`: оба exit0, каждый
8commands/3PNG/9material pixel checks/2reaped runtime children; zero UI sessions,
mappings/GPU resources/handles, disposed view. Каждый из4runtime reports:
Vulkan core/sync0errors/0warnings,1device/1surface/3slots created=released,
LiveSlots/OpenHandles0. Game.Logic пересобиралась compiler с обеими shared refs.

Первый full Debug выявил старые probe assumptions: reflection над implementation
через borrowed IUnitsController и поиск concrete parent среди lifecycle aliases;
Alloy allowlist ещё запрещал новую согласованную Contracts dependency. Probes
получают concrete owner из DI для inspection, интерфейсные calls сохранены;
boundary test требует Contracts и запрещает любые concrete providers/renderers.
Следующий run оставил2cancel-start failures из-за Resolve после disposed provider;
owner теперь фиксируется до disposal. Итоговый Debug выше прошёл целиком.
Первый native helper сам создал directory, которое smoke обязан создавать свежим;
helper исправлен, subsequent actual runs exit0. Эти failures сохранены в logs,
не скрыты passing итогом. Первичные compile issues typed handler/nullable bridge/
namespace исправлены до final runs.

Fresh isolated package matrix: `ValidatePackageFinal.ps1`/`VerifyPackageFinal.ps1`,
output `aether-package-final`, версия`0.2.2.7-alpha.aetherdi.20261022162035190`.
**14fresh nupkgs/20commands/4consumer runs**, все exit0: build Debug, publish Release,
каждый binary на Cobalt и Microsoft DI. Собственные feed/cache и packageSourceMapping
исключают подмену TrueMoon локальным историческим package. Consumer имеет только
PackageReference, использует typed viewport factory, проверяет common UI factory API,
runtime/lease/GPU/viewport exactly-once ownership, terminal admission и zero mappings.
Nuspec Vulkan2.23.0, actual Aether/Skia/native payload hashes и package immutability
проверены. Первая матрица также прошла, затем финальная повторена после обновления
packaged Alloy/Hosting README. Пакеты не публиковались.

`VerifyFinal.ps1` повторно сверил все20TRX/2native runs/4runtime reports/package
summary и записал `verification.json`. Source namespace check:7production projects,
257C#sources/235namespace declarations, нарушений0.22изменённых documents,
413local links, missing targets0; `git diff --check` exit0. Это path-link проверка,
не полный аудит всех исторических anchors. Полный source/structural matrix всего
репозитория и nonincremental build отдельно не повторялись.

Эти проверки не повторяют performance/allocations, golden/RTE, physical DPI,
device loss/VRAM, K3/K7/K8/final alpha, полный engine native60 или все independent
consumer matrices. Функциональные ENG5c/ENG7/World/edit/MCP backlog остаются.

## Переименование редактора и аудит DI — 2026-10-22

По запросу пользователя переименованы TrueMoon.Editor/Tests в
TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.Tests: каталоги/csproj, default assembly
names, namespaces, ProjectReference, CLI help, README и текущие документационные
ссылки. В точном TrueMoon.slnx оба проекта находятся в `/Aether/`; отдельная группа
`/Editor/` устранена. Listing проверен сразу после изменения. Window title теперь
TrueMoon Aether Editor. Исторические имена в предыдущей записи ниже сохранены;
её ссылка на README обновлена. Внешним consumers нужны новые using/references
и пересборка. UI остаётся отдельным application, engine library не получает UI.

Аудит production Aether и обеих process branches редактора показал **частичное
соответствие** правилу Cobalt/IFactory. Activation/lifetime contracts не менялись;
аудит не считается исправлением. [Замечания и единственный план DI1–DI4](PLAN.md#переименование-редактора-и-аудит-di--2026-10-22):

- AetherHost.cs:17–38 вручную создаёт Supervisor/Frames/Builds/Replacement/Editor/
  Gpu. Нужны зависимости/внедрённые factories при сохранении единственного владельца,
  stop/drain order и cleanup retry; disposable aliases добавлять нельзя.
- SessionSupervisor.cs:45/AetherOptions.cs:30–34 используют IRuntimeFactory/Func;
  общий IFactory<T> в production Aether/Editor не используется. Runtime factory
  уже внедрён, но унификация контрактов ещё не выполнена.
- GameBuildLoader.cs:25 создаёт IGameSession через Activator, без constructor DI.
  Требуется factory activation внутри child/artifact/AssemblyLoadContext boundary,
  без загрузки Game.Logic в Editor и без нового reflection fallback.
- EditorApplication.cs:23–45/RuntimeEntryPoint.cs:34–41 вручную собирают workspace/
  binding/commands и runtime protocol/frame producer/IPC. Captured dependencies
  не становятся DI services лишь из-за UsePresentation root callback.
- SceneResolver.cs:25–28 создаёт EntityResolver внутри статического authoring
  pipeline; для системного использования нужна service/factory boundary.

Существующие правильные границы: default Cobalt в App, snapshots/duplicate guards
UseAether, constructor dependencies GameEditor/GameReplacement, отсутствие owned
disposable aliases. IServiceResolver/DI Resolve ограничен registrations и process
bootstrap; EntityResolver.Resolve — доменный алгоритм, не service locator. DTO,
UI elements и low-level resources внутри их владельцев сами по себе допустимы.

### Проверки переименования

Windows x64/SDK10.0.402, net10.0/xUnit2.9.3/VSTest; ignored evidence:
`TestResults/AetherEditorRename-20261022-195846/`.

```powershell
dotnet sln TrueMoon.slnx list
$run = 'TestResults/AetherEditorRename-20261022-195846'
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory "$run/debug" "-bl:$run/debug-{}.binlog" --verbosity minimal
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory "$run/release" "-bl:$run/release-{}.binlog" --verbosity minimal
dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --configuration Release --output "$run/publish" "-bl:$run/publish-{}.binlog" --verbosity minimal
```

Каждый test run: **Passed:2253, Failed:0, Skipped:0**,10complete TRX:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI643/Hosting79/
Aether780/Aether.Editor39. Новая test assembly обнаружена как
truemoon.aether.editor.tests.dll. Release runner/publish exit0; отдельные binlogs
Debug/Release/publish, stdout/stderr и release.exitcode сохранены. Nonincremental
build и independent package matrix не повторялись.

Direct Debug и published Release apphost `TrueMoon.Aether.Editor.exe` с
`--smoke <fresh-dir> --validation`: `native-final-Debug`/`native-final-Release`,
оба exit0/Passed=true. В каждом8UI commands/3PNG/9material pixel assertions,
selection/theme/resize, Build/Start/Play/Pause/Step/Build/Replace/Stop,
2separate reaped runtime children, view disposed/UI sessions/mappings/GPU resources/
handles0. Runtime reports подтверждают enabled core/sync validation0errors/
0warnings including teardown. VK_LAYER_PATH только для собственного process tree:
existing `TestResults/ENG4b/validation-layer/Bin`; Windows/driver settings прежние.

Первый helper ошибочно трактовал недоступный ExitCode у Start-Process как failure,
хотя Debug smoke report прошёл. Исходные logs/report сохранены; final helper
использует Diagnostics.Process/redirected streams и сохраняет фактический код.
Оба final runs повторены, exit0; helper failure не считается product regression.

Структурная проверка четырёх Aether projects:135C# sources/133namespace declarations,
нарушений0; все ProjectReference paths существуют, editor projects ровно по одному
в `/Aether/`, старого namespace в исходниках нет. Hashes/locations сохранены в
`structure-and-sources.json`. Passing tests/native подтверждают переименование,
но не устранение замечаний DI1–DI4. Physical DPI/K3/K7/K8/VRAM/performance/release
ограничения сохраняются. Итоговые whitespace/doc link checks выполнены отдельно.

## ENG7a: основной экран редактора — 2026-10-22

Запрос: «давай теперь возможно сделаем основной экран редактора? я думаю это
должно быть окно без системного оформления с нашим стандартным стилем + в центре
вьюпорт, и грид со свойствами и кнопками управлениями».

Созданы **TrueMoon.Editor** (desktop WinExe/net10.0/IsPackable=false) и отдельный
**TrueMoon.Editor.Tests** (xUnit). Оба зарегистрированы в точном TrueMoon.slnx;
`dotnet sln TrueMoon.slnx list` проверен сразу после wiring и в финале. В engine
assembly ничего не переносилось и новая engine сборка не создана.

Application использует App.UseAether + UsePresentation explicit root factory:
default custom WindowFrame, toolbar, список7demo объектов слева, живой3D viewport
по центру, read-only two-column property inspector справа и state/error presentation.
Выбор обновляет ID/XYZ/interactive/geometry/RGB и сохраняется при theme/Stop/Replace.
Build/Start/Play/Pause/Step/Stop/Replace/Cancel используют существующий GameEditor.
Stop/Cancel допускаются поверх pending command, close fences admission и ждёт
Stop/drain; после failed cleanup admission открывается для retry. UI events не
блокируются на IPC/compiler. WinExe не открывает дополнительную консоль.

NativeRuntimeFactory запускает отдельный процесс того же application binary с
`--runtime`. RuntimeEntryPoint — application composition существующих
RuntimeProtocol/GameBuildLoader/FrameProducer/GpuResourceHost/Enerit/Mithril;
game code загружается только в child. References на ManualTests executable нет.
В source checkout входным примером является внешний Game.Logic.csproj/RoomGame;
другой project/assembly/type задаются CLI. UI owns retained Skia copies, Aether
owns transport/imports, borrowed GL context удерживается до successful drain.
GL UUID query повторяет существующий read-only contract и проверяет owner/context/
процедуры/число устройств/error; native buffer16bytes, Windows x64.

[Запуск и ограничения](../../TrueMoon.Aether.Editor/README.md),
[единственный подробный scope/acceptance](PLAN.md#eng7a-основной-экран-редактора--2026-10-22).
Инспектор пока authoring SceneSnapshot и read-only во всех режимах. Список плоский,
render surface960×540 с aspect-preserving layout. Editor camera/gameplay input,
editable components/project persistence/tree/picking/gizmos/docking и полноценный
runtime World snapshot/edit/MCP — последующие этапы. Отдельная runtime executable
не поставлена; process isolation обеспечена отдельным `--runtime` процессом.

### Текущие проверки ENG7a

Windows x64/SDK10.0.402.39новых xUnit cases: workspace state/selection/properties/
error precedence, command serialization/interruption/close/retry/cancellation,
layout/readonly/theme/disposal и default frame на logical DPI1/1.5/2.

| Требование | Evidence |
| --- | --- |
| «окно без системного оформления с нашим стандартным стилем» | `EditorScreenTests.DefaultFrame_LayoutUsesLogicalUnitsAndSettlesWithoutRedraw`; native apphost screenshots/default frame, direct WinExe startup |
| «в центре вьюпорт» | `EditorScreenTests.ShellGeometry_ReservesToolbarInspectorAndStatusAroundCentralViewport`, `ShellGeometry_SmallBoundsStayFiniteNonnegativeAndClipped`; `EditorSmoke.RunAsync`9independent material pixels/configuration из child3D frame |
| «грид со свойствами и кнопками управлениями» | `EditorScreenTests.HierarchyInput_UpdatesReadOnlyGridAndThemePreservesSelectionAndFocus`, `CommandButtons_DispatchOnlyEnabledCommandsAndBusyStateCanStopOrCancel`; native UI selection and8commands/configuration |
| State/candidate availability | `EditorWorkspaceTests.CommandAvailability_FollowsAcknowledgedRuntimeAndCandidate`13literal expected command sets, `ClosedEditor_DisablesEveryCommandAndInvalidEnumIsRejected` |
| Empty/invalid/snapshot/format/error behavior | `EditorWorkspaceTests.EmptyScene_HasNoSelectionOrRowsAndRejectsUnknownEntity`, `Selection_ChangesInspectorOnceAndInvalidSelectionPreservesIt`, `Inspector_UsesImmutableAuthoringSnapshotAndInvariantNumbers`, `Refresh_ReportsActualPidTickAndErrorPriorityWithoutResettingSelection`, `Refresh_ErrorFallbackKeepsBuildAndRuntimeFailuresVisible` |
| Pending command/close/retry | `EditorCommandsTests.PendingWork_RejectsAnotherNormalCommandButAllowsInterrupt`, `Close_FencesNewWorkAndWaitsForAcceptedCommandAfterStop`, `FailedClose_ReopensAdmissionForCleanupRetry`, `Failure_IsObservedAndNextAcceptedCommandClearsIt`, `Cancellation_IsObservedWithoutAUserError`, `NormalCommand_ReceivesLifetimeAndInvalidCommandsNeverReachTheApi` |
| Ownership/cleanup | `EditorScreenTests.InvalidShellChildren_AreRejectedBeforeAdoption`, `Disposal_UnsubscribesWorkspaceAndKeepsTheSharedSceneUsable`; native runtime/UI/mappings/GPU resources/handles0, reaped children |

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m --verbosity quiet`:
  `TestResults/EditorScreen/build-desktop-final.log`, exit0. Existing repository
  warnings сохранены; новый Editor project ранее собран без warnings/errors.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EditorScreen/tests-desktop-debug --verbosity quiet`
  и аналог Release в `tests-desktop-release`: **2253passed/configuration**,
 10TRX/failed0/skipped0. Editor39/Aether780/UI643/Hosting79/Mithril95/Core107/
  Cobalt179/generator91/Enerit126/generator114. New project discovery не нулевая.
- Direct Debug WinExe apphost и published Release WinExe apphost с
  `--smoke <fresh-dir> --validation`, VK_LAYER_PATH из existing ENG4b validation
  layer. Reports: `TestResults/EditorScreen/native-apphost-Debug/report.json` и
  `native-apphost-Release/report.json`. Оба Passed=true:8actual commands
  (Build,Start,Play,Pause,Step,Build,Replace,Stop),3PNG,9material pixel assertions,
  selection, same UI root/window, theme/resize, preserved paused tick/selection,
  close/drain;2children/configuration имеют отдельные PID и reaped/released.
- Runtime cleanup JSON для каждой generation: validation requested/enabled,
  **core/sync0errors/0warnings including teardown**,1device created/released,
 3slots created/released, live slots/resources/handles0. Parent report:
  UI sessions/mappings/GPU resources/handles0, view disposed. Published
  deployment — framework-dependent Windows x64; NuGet publication не выполнялась.
- PNG Dark/Light и resized просмотрены. После просмотра укорочено имя blue cube,
  label «Интерактивен» уменьшен до font14 и footer переведён на русские состояния;
  финальные apphost screenshots используют эти изменения.

Промежуточные попытки сохранены: initial scaffold build отверг отсутствующий
ещё не созданный EditorSmoke; следующий build поймал ошибочный overload локального
Until и missing readback namespace. После исправления build прошёл. Native
`native-initial` failed из-за диагностического readback внутри UiSession.Invoke
во время Update; захват перенесён в stable FramePresented boundary. `native-second`
прошёл без validation. Затем final Debug/published Release и оба direct WinExe
apphost запуска повторены с enabled validation, все прошли. Failed попытки не
считаются passing evidence; защиту renderer не ослабляли.

Assertion/gap review: независимые literal command sets, exact Rect/values/colors/
selection/caret/offset/ownership, task gates вместо sleeps в unit tests. Добавлены
error fallback и failed-close retry cases по найденным gaps. Roslyn source pairing
запущен один раз перед тестами; это static pairing, не measured coverage. Research/
plan/status сохранены только в temporary testagent directory. Mutation/coverage
score не заявлен.

Предыдущие engine native60cases, UI golden/package/RTE/performance/hardware
матрицы этим запуском не повторены. ENG5c.3 collisions, full toolset, physical
DPI/OS input/K3/K7/K8/VRAM/final alpha остаются открытыми; NVIDIA workaround не
возобновлялся. Следующий локальный editor этап — authoring property editing
через согласованные Entity/Scene API с validation/undo; Play readonly сохраняется
до snapshot transactions.

## ENG5c.2: reach/raycast и interaction — 2026-10-22

Запрос: «давай приступим к следующему шагу»; точка продолжения — ENG5c.2.
В Aether добавлены stateless PrimitiveRaycast/InteractionBody/InteractionHit,
immutable GameObjectStates/GameObjectState и явный TMCK2 codec/profile.
Game.Logic.RoomGame использует bounded reach4, actual affine geometry/occlusion
и независимые переключатели объектов. Изменённый цвет наблюдаем в GPU frame,
editor authoring snapshot/geometry не меняются. [Контракт/следующий шаг](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
[публичный профиль](../../TrueMoon.Aether/README.md#reach-и-состояния-объектов).

Ray query — до64distinct opaque bodies, finite reach(0,1000], inclusive boundary;
inverse-transformed unit-box slabs сохраняют world distance, rotation/nonuniform
scale/shear. Local direction не нормализуется, near-parallel не заменяется zero.
Closest surface блокирует цель даже при Interactive=false; exact ties предпочитают
blocker, затем ordinal ID. Inside/on-surface distance0. E применяется после camera
input на fixed tick, только eligible hit меняет Boolean switch/материал. Active
RGB(1,.8,.1), повторное нажатие возвращает исходный RGB; остальные объекты независимы.

SnapshotVersion=2, control3/frame1 прежние. TMCK1/future schemas отвергаются;
matching Editor/Runtime/Aether/Game.Logic требуют rebuild. Base offsets через
scene ID сохранены; appended UInt16count и ordinal UTF8length/ID/flag records,
общий SHA256, FixedBytes149/max64KiB. До64unique IDs, строгие count/length/UTF8/
canonical order/flag/trailing checks. GameObjectStates deep-copies и даёт value
equality через capture/codec/restore. RoomGame проверяет exact loaded eligible-ID
set и Interaction=Any(Active) до atomic commit camera/tick/objects. Legacy demos
ProbeGame/CameraGame отвергают nonempty Objects без mutation. Runtime readiness
остаётся fenced при RestoreRejected, input transient и не входит в checkpoint.
Это минимальный game-owned switch checkpoint, не full World snapshot/live editing.

52новых xUnit cases: PrimitiveRaycastTests16, ObjectCheckpointTests24,
RoomInteractionTests12. Первые итерации тестов: compile failure из-за пропущенных
required GameInputTick arguments; после исправления96pass/1fail — invalid surrogate
в InlineData нормализовался metadata. Runtime-created surrogate проверяет нужную
ошибку; focused97/97, после gap review101/101. Production expectations не ослаблены.

Full nonincremental Debug build прошёл; первый full solution Debug2185/2185 и
Release2185/2185, по9TRX/failed0/skipped0: Aether779, UI615, Hosting79, Mithril95,
Core107, Cobalt179/generator91, Enerit126/generator114. Прежние/generated CS1591
warnings не подавлялись. Coverage/mutation percentages не измерены.

| Requirement | Evidence: exact xUnit methods |
| --- | --- |
| «ограниченный запрос взаимодействия из камеры» / «явной максимальной дистанцией» | `PrimitiveRaycastTests.ExactReach_IsInclusiveWithAdjacentFloatingPointBoundaries`; `EmptyMissParallelAndBehind_DoNotInventTargets`; `BodyCount_IsBounded` |
| «ближайшему eligible объекту» / «occlusion policy» | `NearestSurface_WinsRegardlessOfEnumerationAndOpaqueDisabledBodyBlocksTarget`; `Ties_PreferBlockerThenOrdinalIdIndependentlyOfInputOrder`; `InsideOrOnSurface_HitsAtZeroAndDoesNotSeeThroughBody` |
| Actual affine geometry | `AffineBoxes_KeepWorldDistanceAndRejectFalsePositiveWorldAabbHits`; `NearParallelAndPitchedRays_KeepSmallNonzeroDirectionAndCameraOrientation` |
| «E действует только на найденную цель и даёт наблюдаемый результат» | `RoomInteractionTests.Interact_ChangesOnlyNearestTargetMaterialAndKeepsIndependentObjectStates` |
| «вне reach, заслонённую/отключённую цель» | `UnreachableOccludedDisabledAndMissedTargets_DoNotToggleAnything` (4cases) |
| «input rising edge» / «восстановление состояния» | `RuntimePulseAndFreshRestore_PreserveCompleteObjectStateAndDoNotReplayInput` |
| «атомарность capture/restore/Replace» | `InvalidObjectSetRestore_IsRejectedBeforeAnyCameraTickOrStateMutation` (3); `RuntimeWrongObjectSet_IsRejectedWithoutMutationAndKeepsFramesFenced`; `InvalidCameraAndTickOverflow_LeaveCompleteRoomStateUnchanged`; `LegacyGames_RejectPerObjectStateWithoutPartialRestore` |
| «versioned checkpoint» | `ObjectCheckpointTests.CountAndUtf8Boundaries_RoundTripCompleteDetachedState` (0/1/64/65); `RehashedMalformedObjectSectionsAndOldSchemas_AreRejected` (12); `States_AreCanonicalCopiedAndValueEqualAcrossIndependentCaptures`; `SnapshotNegotiation_RequiresV2WhileControlAndFrameRemainCurrent` |
| Diagnostic JSON compatibility | `ObjectCheckpointTests.DiagnosticJson_RoundTripsValidatedStateWithoutWeakeningImmutability`: complete state/value equality, detached storage, duplicate-ID rejection |

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~Interactions|FullyQualifiedName~ObjectCheckpointTests|FullyQualifiedName~CheckpointCodecTests|FullyQualifiedName~RoomSceneTests|FullyQualifiedName~GameInputProtocolTests' --logger trx --results-directory TestResults/ENG5c2/focused-reviewed --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --logger trx --results-directory TestResults/ENG5c2/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c2/final-Release --verbosity quiet
```

Research/plan/status и Roslyn pairing — OS temp `truemoon-eng5c2-testagent-20261022`.
Dedicated generator/IDE findTests недоступны по предыдущей проверке; применены
inline Research→Plan→Implement, deterministic static pairing и direct code reads.
Whole-src pairing включает historical TestResults copies и не является coverage.
Assertion/gap review добавил near-parallel, count-zero trailing, object-set runtime
fence и tick-overflow/camera atomicity cases; clean focused101/101 подтвердил их.

Первая полная native-матрица `TestResults/EngineSessions/eng5c2-final` остановилась
на Debug-editor-gpu: старый diagnostic JSON consumer десериализует GameCheckpointState,
а IEnumerable constructor parameter GameObjectStates не соответствует ImmutableArray
property type для System.Text.Json. Предшествующие interaction/room/scene/input/
GPU import/surface/viewport cases прошли; весь запуск не считается passing.
Добавлен private JsonConstructor с exact ImmutableArray binding, делегирующий тому
же validation/copy path; публичный mutator не добавлен. Новая JSON regression
проверяет complete state roundtrip и сохранение duplicate-ID validation.
Focused после fix102/102, повторные полные проверки имеют отдельные свежие reports.

После JSON fix full Debug2186/2186 иRelease2186/2186,9TRX/configuration,
failed0/skipped0; Aether780, остальные project counts прежние. Последние passing
TRX — `TestResults/ENG5c2/final-Debug-jsonfix` и `final-Release-jsonfix`;
`managed-summary.json` ссылается только на эти18TRX. Итоговые команды:

```powershell
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5c2/final-Debug-jsonfix --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c2/final-Release-jsonfix --verbosity quiet
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng5c2-final-jsonfix -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -IncludeScene -IncludeInput -IncludeRoom -IncludeInteraction -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
```

Новый native interaction proof прошёл в Debug apphost и published Release:
по10cases/9PNG/4owned children, все cooperative/reaped/released. PNG assertions
проверяют три independent world centers/material colors, bounded pixel counts,
floor/back wall/occlusion; active blue child становится yellow, после Replace
цвет/флаг сохраняются, второй pulse возвращает authored blue. Отдельные сцены
revision8/9 проверяют noninteractive blocker перед target и disabled target.
80fixed ticks перемещают камеру сZ6кZ2; reach/outside, one-shot E, resize/suspend/
resume320×240, exact complete checkpoint state и transient-input reset проверены.
Core/sync validation errors/warnings0/0, resources/handles/mappings/UI sessions0,
imported ReadPixels0; readback только итогового UI screenshot для assertions.
Новый proof подаёт pulses через обычный Supervisor API; прежний input proof отдельно
использует настоящий Silk keyboard callback. Physical OS input/raw cursor не заявлены.
Визуально просмотрен active-object PNG, geometry/цвета соответствуют fixture.

Local `TrueMoon.Aether.0.2.2.7-alpha.nupkg` создан из final Release без rebuild;
DLL SHA256 совпал с managed и native published proof:
`3A31E2CA99389209923F15DBB907037879C10CDE1BAB31D873E42EDC5C8143F1`.
Команда: `dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5c2/packages --verbosity quiet`.
Отдельный package drawing consumer/публикация не выполнялись. Все52pending C#files
прошли namespace/whitespace checks, scratch artifacts в Git0. Reports —
`TestResults/ENG5c2/managed-summary.json`, `package-check.json`, `namespaces-check.json`,
`docs-check.json`, `native-interaction-summary.json`, `completion.json`.

Финальная полная native-матрица после JSON fix:
`TestResults/EngineSessions/eng5c2-final-jsonfix/summary.json`, **Completed=true,
60/60cases,62commands,478source/76binary fingerprints**; hashes неизменны на
протяжении run. Per configuration20headless и10UI/GPU cases: новый interaction,
прежние room/cube/input, CPU viewport/Editor, GPU import/surface/viewport/ordinary
Editor и checkpoint/Replace/failure regressions. Ordinary GPU Editor снова прошёл
20Replace/20failures после JSON fix; старые requirements не ослаблялись.
Passing CPU/GPU engine integration не подменяет full UI/golden/RTE/hardware,
package drawing или3D performance. Предыдущий interrupted native run сохранён
с исходной причиной и не включён в passing counts.

Completion report Passed=true: managed2186/configuration, native60cases, новый
interaction18PNG, namespace52files; documentation10Markdown/284links/189anchors,
errors0. Scratch остается вне Git. ENG5c.2 выполнен в заявленном scope;
далее ENG5c.3 collisions камеры, затем target/HUD/pause. Full World snapshot/edit/
MCP/standalone/raw cursor и прежние hardware/release gates открыты. Commit/push
пользователь этим шагом не запрашивал.

## ENG5c.1: scene consumers и несколько примитивов — 2026-10-22

Запрос: «давай приступим к следующему шагу»; текущая точка продолжения — ENG5c.1.
В одной Aether assembly добавлены BuiltInComponents/PrimitiveSceneAdapter,
RenderPrimitive/ScenePrimitive и bounded multi-draw PrimitiveScene. SceneSnapshot
копирует optional primitive payload; control identity переведена с2на3.
Frame1/TMCK1 прежние. Matching Editor/Runtime/Aether/Game.Logic пересобираются,
v1/v2 identity отклоняется; fallback нет. Старый cube constructor и CPU default
сохранены. [Контракт и следующий шаг](PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22),
[публичный профиль](../../TrueMoon.Aether/README.md#стандартные-компоненты-и-комната).

Built-in schemas v1 задают persistent role GUIDs, exact defaults/dependencies и
single occurrence. Transform local S*R*T, world local*parentWorld, quaternion
yaw/pitch/roll radians; shear сохраняется без decomposing SRT. Positive scale,
finite/nonsingular matrices и stored-sRGB RGB[0,1] проверяются до публикации.
Неизвестные роли authoring-only; no Appearance=no draw, no Transform group=identity.
Source scene revision обязана изменяться вместе с authoring data/definitions.
Wire primitive —16row-major floats+RGB, deep-copy на ingress/egress; до64entities.
Immutable frame — до64draws, включая empty list. Vulkan loop сохраняет pass/depth/
slot lifetime и shader/SPV; CPU pixels/staging для scene renderer не добавлены.

`Game.Logic.RoomGame` — отдельная DLL, static room geometry reload той же revision,
fixed-tick camera и checkpointed demo interaction. TMCK1 не объявляется full-world
snapshot. `--room` подключён к прежнему App/Editor/input sender; `--room-proof`
использует обычный separate child/IPC/imported GPU/retained UI path. RoomScene
создаёт пол, три стены, три объекта и parent group через authoring resolver;
синий child наследует90°rotation и2xscale. Reach/raycast/collisions/HUD — ENG5c.2+.

Managed evidence2026-10-22:36новых cases (BuiltInSceneTests22, RoomSceneTests14),
обновлён input codec regression control-v3. Первый focused Debug68/68, затем
full solution Debug2134/2134 иRelease2134/2134, по9TRX/failed0/skipped0:
Aether728, UI615, Hosting79, Mithril95, Core107, Cobalt179/generator91,
Enerit126/generator114. Existing CS1591 warnings в прежних/generated APIs не
подавлялись. Assertions/gap review выполнен, coverage/mutation metrics не измерены.

| Requirement | Evidence: точные xUnit methods |
| --- | --- |
| «минимальные стабильные component roles/schema IDs» | `BuiltInSceneTests.StableRoles_DefaultsAndDependenciesAreExplicit`; `BuiltInSchemaChanges_RequireExplicitMigration` (4cases) |
| «parent transforms определены и проверены» | `BuiltInSceneTests.ParentScaleRotationTranslation_ComposesInOrderAndRetainsShear`: world center(22,6,-2), dot=-6; `WorldMultiplicationOverflow_IsRejectedEvenWhenLocalTransformsAreFinite` |
| «позиции/материалы из scene data» | `VariantOverridesAndComponentRemoval_DriveActualDrawAndInteractionData`; `InteractionEnabledAndBoundaryColors_AreNotReplacedByDefaults` (2); `MissingRoles_AreIdentityGroupsAndNonRenderingMarkers` |
| Atomic invalid input/cancel и bounded snapshot | `InvalidGeometryOrMaterial_FailsAtomicallyWithEntityPath` (7); `RuntimeBound_IsExplicitAndAuthoringRemainsUnbounded` (0/64/65); `CancellationAndRepeatedProjection_DoNotPublishSharedMutablePayloads` |
| Wire ownership/version | `RoomSceneTests.Snapshot_DeepCopiesWirePayloadOnBothIngressAndEgress`; `MalformedWirePrimitive_IsRejectedBeforePublication` (7); `ControlV3_RoundTripsWorldMatrixColorAndNeutralGroupWithoutAliasing`; `GameInputProtocolTests.ControlV3_RoundTripsInputFlagsLookActionAndOutcomeAndRejectsOlderVersions` |
| Multiple primitives/legacy compatibility | `RoomSceneTests.ExplicitDrawList_IsBoundedAndCopiedWhileLegacyCubeRemainsCompatible` (0/64/65), прежние `PrimitiveSceneTests` |
| «scene revision/checkpoint/Replace» | `RoomSceneTests.RuntimeRoom_UsesAuthoredDrawsAndRetainsOnlySupportedCheckpointStateAcrossFreshLoad`; `InvalidLoad_DoesNotConsumeSequenceOrMutateGameAndChangedSceneRevisionRejectsRestore` |

Команды и артефакты:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~BuiltInSceneTests|FullyQualifiedName~RoomSceneTests|FullyQualifiedName~PrimitiveSceneTests|FullyQualifiedName~GameInputProtocolTests' --logger trx --results-directory TestResults/ENG5c1/focused-initial --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5c1/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c1/final-Release --verbosity quiet
```

`TestResults/ENG5c1/managed-summary.json` содержит18TRX paths/counts. Research/plan/
status и Roslyn pairing сохранены в OS temp `truemoon-eng5c1-testagent-20261022`;
dedicated testing-generator tool недоступен, pipeline выполнен inline. Rider
findTests недоступен по предыдущей проверке, использованы static pairing и direct
source/test reads. Scan whole src включает исторические TestResults copies и
не является coverage report. Проверены независимые expected coordinates/colors,
ошибки/code/path/cause, неизменность состояния, границы и исключение staged input
из checkpoint. Scratch не добавлен в Git.

Native evidence2026-10-22: первый scoped Debug room run прошёл с5PNG.
После final code review выполнена свежая полная матрица
`TestResults/EngineSessions/eng5c1-final/summary.json`: **Completed=true,58/58cases,
60commands,473source/76binary fingerprints**, source/binary hashes неизменны
на протяжении verification. Debug apphost и published Release отдельно.

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng5c1-final -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -IncludeScene -IncludeInput -IncludeRoom -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5c1/packages --verbosity quiet
```

Per configuration:20headless scenarios и9UI/GPU cases. Сохранены старые cube/input,
CPU viewport/Editor, GPU import/surface/viewport/ordinary Editor и replacement/
failure regressions. Новый room proof имеет5PNG, три hard-coded world centers/
material colors, bounded material pixel counts, floor/back wall samples и depth
occlusion (стена draw после объектов). Камера сдвинута на .15 за3fixed ticks;
resize800×450, suspend0/resume320×240, Replacegeneration2 сохраняет exact
camera/tick/interaction/Paused и положение/цвета сцены. Синий parented object
проверяет реальный перенос world matrix через generated IPC codec. Визуально
просмотрен первый PNG: три объекта и комната отображаются корректно.

GPU core/sync validation errors/warnings0/0; runtime cleanup reports и local
assertions проверили resources/handles/mappings/UI sessions0. Imported texture
ReadPixels0; PNG assertions используют readback итогового UI OpenGL screenshot.
В room по2owned child/configuration, все cooperative/reaped/released; game DLL
не загружена в Editor. Прежний input proof по15cases/5PNG/configuration, ordinary
GPU Editor по20Replace/20failures; это scoped engine integration, не вся UI
native/golden/RTE/hardware matrix и не physical OS keyboard/mouse acceptance.

Local `TrueMoon.Aether.0.2.2.7-alpha.nupkg` создан. DLL SHA256 совпадает с managed
Release и published native proof:
`5CEDCB47A36E50FCCD458078CA80B0B217C1FB987FF0B9979374C02EB0FF1659`.
`TestResults/ENG5c1/package-check.json` содержит payload/entries. Отдельный
package drawing consumer и публикация не выполнялись. Все42pending C#files
(включая24предыдущих этапов) прошли namespace/whitespace checks.
Итоговые reports: `managed-summary.json`, `namespaces-check.json`, `docs-check.json`,
`completion.json` в `TestResults/ENG5c1`. Required shaders/SPV не менялись.

ENG5c.1 выполнен в заявленном scope. Далее ENG5c.2 reach/raycast/interaction
с явным versioned object-state checkpoint до mutable gameplay. Full world snapshot,
runtime edits/MCP, collisions/HUD/standalone,3D performance/raw cursor и прежние
hardware/release блокеры остаются открытыми. Коммит/пуш этим шагом не запрошены.

## ARCH1a.2: агрегат сцены и адаптер — 2026-10-22

Запрос: «давай к следующему шагу»; точка продолжения — ARCH1a.2. Добавлены
SceneId/SceneDocument, SceneResolver/ResolvedSceneSnapshot, structured scene errors
и explicit SceneSnapshotAdapter/SceneEntityProjection в той же Aether assembly,
namespace Scenes. Это stateless in-memory API, без нового UseX или UI dependencies.
Profile и следующий ENG5c.1 ведутся в [PLAN](PLAN.md#реализация-arch1a2--2026-10-22),
публичная инструкция — [README](../../TrueMoon.Aether/README.md#агрегат-сцены-и-совместимый-snapshot).

SceneDocument фиксирует nonempty GUID scene ID, revision≥0 и frozen catalogs.
Whole-scene resolver проверяет duplicate entities/catalogs, parent presence/cycles
и every effective entity через существующий EntityResolver. Traversal/graph
итеративны; roots/siblings сортируются по Guid, preorder даёт parent-before-child.
Immutable snapshot публикуется только после полного успеха; referenced definition
revisions и exact schemas visible компонентов согласованы с тем же document.
Unused catalog graphs не разрешаются, duplicate catalog identities всё же invalid.
Revision — authoring version, не tick; полного simulation checkpoint здесь нет.

Projection получает source snapshot/entity и явно задаёт world XYZ/Interactive.
Adapter сохраняет canonical GUID IDs/revision и старый limit64entities, проверяет
finite floats, не публикует partial result при error/cancel. Parent/definition
metadata остаются в source; world transform composition и внешние эффекты callback
определяет consumer. Legacy64limit не накладывается на сам authoring aggregate.
OCE передаётся без wrapping; остальные projector failures сохраняют original cause
и failing scene/entity path. Старые SceneSnapshot/control/frame/TMCK не изменены.

Первый focused run:51passed/1failed из52новых cases. Ошибка assertion при сравнении
двух ImmutableArray с одинаковым содержимым исправлена явным AsEnumerable comparison;
проверки точного order/children/values сохранены. После этого scene suite130/130
и финальные solution runs ниже прошли. Production logic после первого focused
run не менялась. Требованиям соответствуют следующие tests в
`TrueMoon.Aether.Tests.Scenes`:

| Requirement | Evidence |
| --- | --- |
| «проверить уникальность EntityId» | `SceneResolverTests.DuplicateEntityIds_AreRejectedBeforeResolvingEitherInstance`: exact code/path/EntityId |
| «существование parent и отсутствие parent cycles» | `SceneResolverTests.ParentGraph_RejectsMissingParentWithStablePathAndEntityIdentity`, `ParentGraph_RejectsCyclesIncludingTailsEnteringACycle`; self-parent — `LocalContracts_RejectInvalidInputsBeforeSceneResolution` |
| «несколько instances, independent inheritance и parent hierarchy» | `SceneResolverTests.Forest_ResolvesInheritanceIndependentlyFromParentsAndPinsReferencedVersions`: five entities, roots/children/preorder, independent template values/provenance |
| «Контракт scene revision/definition versions» | `SceneResolverTests.RevisionMetadata_PinsUsedDefinitionsAndCoexistingExactSchemaVersions`: long.MaxValue document revision, referenced definitions, simultaneous v1/v2 schemas; `EmptyAndSingletonScenes_ReturnCompleteHierarchyAndOnlyUsedMetadata` covers revision0/1 |
| «atomic failed resolution» | `SceneResolverTests.EntityFailure_IsAtomicAndRetainsInstanceAndOriginalDefinitionCause`: later entity failure, no published result, earlier snapshot/source unchanged, subsequent valid resolution |
| «immutable aggregate» | `SceneResolverTests.FrozenDocument_RemainsConsistentAfterAllCallerCatalogsAreMutated`, `CancelledAndParallelResolutions_DoNotChangeSharedDocumentOrOtherSnapshots` |
| Deterministic parent-first order and independent error categories | `SceneResolverTests.DeclarationOrder_DoesNotChangeHierarchyOrResolvedValues`, `InheritanceAndSchemaFailures_AreNotConfusedWithAValidSceneParentGraph`, `CatalogErrors_RetainOriginalCategoryAndScenePathEvenForEmptyScenes` |
| Deep hierarchy without recursive traversal | `SceneResolverTests.DeepHierarchy_UsesIterativeTraversalAndDoesNotInheritDefinitionDepthBound`:4096entities, reverse-ID parent chain, exact order/leaves |
| «явную адаптацию минимальных component данных к текущим scene/game consumers» | `SceneSnapshotAdapterTests.Projection_PreservesCanonicalIdsRevisionOrderAndExplicitWorldSpacePolicy`: world X6/7 for child/grandchild, exact IDs/revision/order, mutable copy isolation; `Projection_RemainsLoadableByExistingGameAndCheckpointContract`: ProbeGame Load/ticks/interaction checkpoint and reset |
| Legacy entity/float bounds | `SceneSnapshotAdapterTests.ExistingEntityLimit_IsCheckedBeforeAnyProjectionAndDoesNotLimitAuthoringScenes`:0/1/63/64/65 and no callback at65; `NonFiniteWorldCoordinates_ReportEntityAndOriginalValidationCause`: all XYZ/NaN/±infinity; `FiniteCoordinateBoundariesAndNullContracts_AreExplicit` |
| Projection failure/cancel atomicity | `SceneSnapshotAdapterTests.ProjectorFailure_PreservesOriginalCauseAndPublishesNoPartialResult`, `Cancellation_BeforeOrDuringProjectionNeverPublishesOrCallsLaterEntities`: pre/first/last callback; `ProjectorCancellation_IsNotWrappedAsAnAuthoringFailure` |

Успешные команды:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes --logger trx --results-directory TestResults/ARCH1a2/focused-scenes --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ARCH1a2/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ARCH1a2/final-Release --verbosity quiet
```

Focused scenes130/130 (78previous +52new). Full Debug/Release:
**Passed:2098, Failed:0, Skipped:0** в каждой configuration,9TRX/configuration.
Counts: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI615/
Hosting79/Aether692. Final TRX включают все52новых и130scene cases/configuration;
отчёт — `TestResults/ARCH1a2/managed-summary.json`.

Review assertions проверил exact collections/metadata и errors/cause/paths,
secondary state после failure, input copy isolation, independent graphs и
before/at/after limits. Статические mutation candidates: пропуск parent/cycle
validation, изменение order, потеря used-version pruning, shared mutable catalogs,
частичное завершение projection/пропущенный cancellation check и off-by-one64
нарушили бы named assertions. Mutants/line coverage не запускались, score не
заявляется. Roslyn pairing выполнен один раз до реализации; whole-root counts
включают historical TestResults copies и не являются production coverage.
Research/plan/status workflow сохранён в OS temp вне Git; IDE findTests отсутствует.

Это managed source proof. Native/GPU/package checks не повторены, interactive
sample остаётся прежней fixture. Auto world transforms, full runtime snapshot/edit,
aggregate persistence/transport codec и MCP не объявлены готовыми. Следующий
ENG5c.1 — минимальные component roles и подключение scene data к runtime/renderer
нескольких примитивов с отдельным native pixel/validation/lifecycle proof.

Финальная проверка: 8 Markdown,204локальные ссылки/122anchors, ошибок0;
24pending C# files (7production+2test новых этого этапа) — namespace/whitespace0issues.
Tracked C# contracts без изменений, scratch artifacts в Git0. Итоговый
`TestResults/ARCH1a2/completion.json` сверяет9TRX/configuration,2098passed,
692Aether/130scene/52новых cases и текущие documentation/source checks.

## ARCH1a.1: immutable модель и resolver — 2026-10-22

Запрос: «давай приступим к работе» после назначения ARCH1a.1. В одной Aether
assembly, namespace Scenes, реализованы immutable authoring contracts: GUID IDs,
definitions/variants/instances, component schemas/data, patches/scalar values,
EntityResolver и immutable result с property/presence provenance. Новые классы
не требуют App extension: это stateless utilities, без owned resources.
Точный [profile и границы](PLAN.md#реализация-arch1a1--2026-10-22) записан в PLAN;
публичный пример — [README](../../TrueMoon.Aether/README.md#authoring-шаблоны-варианты-и-экземпляры).

Контракт первого profile: одна base definition, отдельный scene parent,64definitions
в chain; exact schema version, ordinal ASCII property IDs1..128, null/bool/Int64/
finite double/string≤4096UTF16, без numeric coercion. Значения по умолчанию схемы
имеют отдельный provenance flag. Remove маскирует occurrence, restore возвращает
hidden state; reset снимает только локальный override/mask. Occurrence IDs не
переиспользуются в цепочке. Final visible set проверяет multiplicity/dependencies.
Errors содержат code/path; невалидная instance не мешает разрешению другой.

Сначала focused run дал72passed/2failed: ImmutableDictionary builder принимал
duplicate property ID с тем же value/object. Добавлены явные duplicate checks
в ComponentSchema и AuthoringValidation.Values. Focused74/74 после исправления
прошёл; при review добавлены ещё4cases (version zero/negative и сохранение всех
patch operations после reset), всего78новых. Финальное уточнение ordinal test
проверяет разные explicit data values для `A`/`a`, не только schema defaults.
Все последующие финальные проверки выполнены после этого уточнения.

Все method names ниже относятся к `TrueMoon.Aether.Tests.Scenes`.

| Requirement | Evidence |
| --- | --- |
| «сущности могут наследоваться», «только 1 какое-то изменение в родительской» | `EntityResolverTests.DoorVariant_TwoInstances_ResolveIndependentValuesAndOrigins`: color red, inherited label/x/defaults, отдельные parent/definition и origin revision |
| «изменение свойств и добавление\удаление компонентов» | `EntityResolverTests.Components_AddRemoveRestoreAndResetMasks_PreserveValuesAndPresenceOrigins`: variant addition + mask, instance restore + color override, source и presence source |
| «Door → RedDoor → два instances сцены» | `EntityResolverTests.DoorVariant_TwoInstances_ResolveIndependentValuesAndOrigins`: independent transforms/color, IDs и повторное разрешение без утечки изменений |
| «property override/reset/null/default» | `EntityResolverTests.PropertyReset_DistinguishesMissingNullAndExplicitDefaultAndRetainsOtherChanges`, `AuthoringModelTests.NullableSchema_ResolvesNullDefaultAndAcceptsExplicitNonNullValue` |
| «base updates» | `EntityResolverTests.BaseRevision_UpdatesInheritedValuesWithoutOverwritingLocalOverridesOrOldResults` |
| «add/remove/reinstate components» и сохранение остальных changes | `EntityResolverTests.Components_AddRemoveRestoreAndResetMasks_PreserveValuesAndPresenceOrigins`, `AuthoringModelTests.PatchResets_PreserveUnrelatedAddRemoveRestoreAndPropertyChanges` |
| «cycles/missing base», «duplicate IDs/type conflicts» | `EntityResolverTests.InvalidDefinitionGraphs_ReportIdsAndDoNotTraverseSceneParents`, `InvalidChanges_ReportExactCodeAndTargetWithoutPoisoningResolver`, `Catalogs_RejectDuplicateDefinitionsAndExactSchemaVersionsWithPaths` |
| «component multiplicity/dependencies» | `EntityResolverTests.Multiplicity_IsTypeBasedAndPermitsIndependentOccurrencesOnlyWhenDeclared`, `FinalDependencies_AllowRestorationButRejectMissingRequiredTypes` |
| «provenance и неизменность исходных данных» | `AuthoringModelTests.Collections_AreFrozenTransitivelyBeforeCallerMutatesEveryInputCollection`, `EntityResolverTests.Catalogs_AreSnapshottedAndRepeatedParallelResolutionsDoNotShareMutableState` и DoorVariant test |
| «schema/version и допустимые значения» | `EntityResolverTests.SchemaVersions_AreExactAndUnknownSchemasAndInvalidInitialValuesAreRejected`, `AuthoringModelTests.ScalarValues_PreserveKindsBoundariesAndRejectCrossKindReads`, `ScalarNumbers_RejectNonFiniteValues`, `PropertySchemas_RejectInvalidKindsAndDefaults` |
| ID/key/version/depth bounds и typed errors | `AuthoringModelTests.Identifiers_RejectEmptyAndDefaultIdsAtEveryModelBoundary`, `PropertyIds_KeepOrdinalCaseAndEnforceExactLengthBoundary`, `AuthoringInputs_RejectInvalidLocalContractsBeforeResolution`, `EntityResolverTests.InheritanceDepth_EnforcesExactBoundWithoutRecursion`, `InvalidDefinitionValues_ReportDefinitionPathWithoutPoisoningOtherInstances` |

Команды, завершившиеся успешно:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes --logger trx --results-directory TestResults/ARCH1a1/focused-green --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger trx --results-directory TestResults/ARCH1a1/aether-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ARCH1a1/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ARCH1a1/final-Release --verbosity quiet
```

Focused-green74/74 — промежуточный run; module Debug640/640. Финальные solution
Debug иRelease: по9TRX/configuration, **Passed:2046, Failed:0, Skipped:0**.
Counts: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI615/
Hosting79/Aether640; все78новых cases включены в каждый final run.
Managed evidence: `TestResults/ARCH1a1/managed-summary.json` и final TRX directories.

Test-quality review проверил named requirements, exact error code/path, inherited
и local provenance, secondary state после failures/reset, null/default differences,
boundary neighbors и immutable collections. Исправлена слабая проверка, создававшая
новый fixture вместо наблюдения результата; final assertion проверяет отсутствие
Extra на последующем valid result. Review статический: mutation score и line
coverage не измерялись. IDE findTests отсутствует; Roslyn pairing выполнен один раз,
обнаружил existing SceneSnapshot tests. Его общие counts включают исторические
TestResults copies и не объявляются аудитом production sources. Research/plan/status
test workflow сохранены в OS temp вне stageable repository content.

Изменения не трогают прежние SceneEntity/SceneSnapshot, RPC/control/frame/TMCK,
runtime simulation/input/renderer. Native/GPU/package checks этим шагом не
повторялись. Полный snapshot, live editing, MCP, persistence и parent graph aggregate
не объявляются готовыми. Далее ARCH1a.2 scene aggregate и адаптация для ENG5c.

Финальные checks: 8 Markdown, 197 локальных ссылок и 115 anchors, ошибок0;
15новых C# файлов/namespace violations0; tracked diff и new-file whitespace
проверены. `TestResults/ARCH1a1/completion.json` сверяет managed counts, все78новых
cases в каждом final TRX set, namespaces и отсутствие scratch artifacts в Git.

## Editor/Runtime: процессы и первый шаг — 2026-10-22

Пользователь подтвердил два процесса: owned Runtime запускается модулем,
редактор с UI работает отдельно; команды/input передаются через IPC. Затем
попросил внести решение в планы и определить начало работ. Существующее разделение
в PLAN сохранено и уточнено: Aether supervisor → Mithril launch/stop/reap,
Enerit control/input/state, отдельный RAM/GPU frame transport, readonly query,
runtime-owned snapshot и те же API для CLI/агентов. Уточнены input admission/reset
и recovery неизвестного результата edit после disconnect. Текущие probe роли
одного executable уже работают в разных процессах; production executables — ENG7.

Первым назначен [ARCH1a.1](PLAN.md#arch1a1-первый-шаг-модель-и-разрешение-наследования):
immutable definitions/variants/instances и pure effective entity resolver с
provenance. Контрольный пример Door → RedDoor → два независимых instances;
критерии покрывают overrides/reset, component add/remove, invalid graphs и
immutability. Затем минимальная интеграция модели в scene data/ENG5c; полный
authoring/runtime edit/MCP toolset развивается отдельно в ARCH1b–d/ENG7.
Общие PLAN/STATUS и module STATUS синхронизированы. Изменена документация;
новых code/build/test/native/package результатов нет.

Проверены 6 изменённых Markdown-файлов, 184 локальные ссылки и 107 anchors; ошибок нет;
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл.
Отдельный отчёт: `TestResults/ArchitectureProcesses-20261022/docs-check.json`;
предыдущая проверка ARCH1 сохранена отдельно.

## ARCH1: согласование модели мира и API — 2026-10-22

По обсуждению пользователь подтвердил Entity + Components и наследование данных:
варианты переопределяют свойства, добавляют и удаляют компоненты. Для Play выбран
readonly просмотр по умолчанию; явное редактирование ставит Runtime на паузу,
сохраняет согласованный snapshot, применяет изменения и возвращает прежний режим.
Принят первый локальный профиль агентов с MCP adapter поверх общих API.

В [PLAN](PLAN.md#arch1-entity-components-и-api--2026-10-22) записаны владельцы данных,
различие scene hierarchy/base definition, API для authoring/runtime, rollback,
границы полного snapshot и ARCH1a–d acceptance. При failed restore продолжение
частично изменённой симуляции запрещено. Перенос Play edits в документ явный;
TMCK v1 не расширен и не объявлен полным snapshot. Одна Aether assembly и App
integration сохранены; функциональный следующий шаг ENG5c учитывает модель ARCH1a,
полный toolset развивается в ENG7. Build/test/native/package checks не запускались;
предыдущие результаты2026-10-21 сохраняют свои scope и дату.

Проверены5изменённых Markdown,156локальных ссылок и88anchors, ошибок0;
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл.
Отдельный отчёт: `TestResults/Architecture-20261022/docs-check.json`;
исторические отчёты ENG5b не перезаписывались.

## ENG5b: gameplay input и камера — 2026-10-21

Запрос: «теперь давай приступим к следующему шагу»; точкой продолжения был ENG5b.
Реализованы `Input.GameInputState`, `GameInputTick`, optional `IInputGameSession`,
fixed-tick accumulator и `FirstPersonCamera`. `Supervisor.SendInputAsync` на
App-owned host проверяет exact ожидаемую identity внутри command gate; очередь
старого runtime не переадресуется новому. Accepted означает staging без Tick;
Unsupported не fault legacy game. Control wire v2 добавляет flags/capture/look/
interaction pulse/outcome; frame/TMCK schemas v1 прежние. Старый control v1 и
старые host fingerprints несовместимы; matching host/game надо пересобрать.

Input bounded: movement flags0..15, packet look±0.5rad, accumulated look±1rad,
не-finite/unknown flags/non-neutral release отвергаются до mutation. E pulse
сохраняет быстрый down/up, UI repeat фильтруется, несколько pulses до tick
coalesced в одно действие. Look/action consumed один раз, held lease12fixed
ticks без refresh (200ms при continuous60Hz, не wall-clock timeout в Paused).
Pause/mode/Stop/restore очищают input. CPU ConfigureSurface очищает accumulator;
GPU runtime adapter вызывает `ResetInput` при Configure/Detach. Reset не меняет
game/checkpoint state. Transient input не переносится в checkpoint/Replace.

`CameraGame` отдельно от legacy ProbeGame: XZ движение3units/s, diagonal
нормализована, look влияет на yaw/pitch, pitch ограничен, yaw wrapped. Сохраняет
camera/tick/interaction; E пока переключает demo material при наличии interactive
entity. Distance/raycast/collisions/комната/HUD остаются ENG5c.

В sample `--scene`: реальные GameEditor Start/Build/Play/Pause/Step/Stop/Replace/
Refresh/Cancel, click внутри fitted viewport, WASD/mouse/E и Escape → Editor.
Argentis keys W/S/D/E добавлены в конец enum, Silk mapping дополнен. Один30Hz
sender, bounded coalescing buffer, task на UI input event не создаётся.
Focus/capture loss, mode/resize/suspend/Stop/Replace сбрасывают state; capture
требует нового click. Sender использует capture epoch и не применяет stale
cleanup к новой capture; App composition drains его до Aether/GPU/UI teardown.
OS cursor lock/raw mouse отсутствуют; logical pointer deltas не дают unlimited
вращение у края OS window.

### Requirement | Evidence

42новых xUnit cases в `TrueMoon.Aether.Tests.Input`:

| Requirement | Evidence |
| --- | --- |
| Bounded packet/tick, exact valid boundaries, reject-before-mutation | `GameInputTests.State_ValidatesNeutralAndExactLookBoundaries`, `State_RejectsMalformedPacketsBeforeMutation`, `Tick_RejectsInvalidFlagsAndLook` |
| Consume look/E once, held lease12ticks, refresh/release/reset | `Accumulator_CoalescesLookAndActionsOnceAndExpiresAfterTwelveTicks`, `Accumulator_RefreshExtendsLeaseReleaseClearsPendingAndInvalidPacketDoesNotRefresh`, `GameInputProtocolTests.PlayingLease_ExpiresWithoutRefreshAndFreshPacketResumesMovement`, `HostReset_DropsHeldLookAndActionWithoutChangingCheckpointState` |
| Fixed speed/diagonal/opposites/yaw/pitch and camera checkpoint | `Camera_UsesFixedSpeedAndOppositeKeysCancel`, `Camera_NormalizesDiagonalAndTurnsMovementWithYaw`, `Camera_ClampsPitchWrapsYawAndRejectsInvalidPose`, `CameraGame_CheckpointRetainsPoseInteractionAndNeutralTicksStayStill` |
| Quick E/repeat, bounded mouse and clear UI state | `UiBuffer_PreservesQuickEKeyRepeatAndConsumesLookOnce`, `UiBuffer_BoundsMouseIgnoresInvalidCoordinatesAndResetDropsAllTransientState` |
| Ready/Paused/Step/mode/restore/Stop and optional capability | `Input_RequiresHelloAndLoadedScene`, `Input_StagesWhilePausedAndAppliesOnceAtStepThenReleaseClears`, `ModeChangeAndStop_ClearInputBeforeFutureTicks`, `RestoreReadinessAndOldSequence_RejectInputWithoutMutatingGame`, `UnsupportedInput_IsExplicitAndLegacyGameContinues` |
| Wire v2 floats/flags/action/outcome, reject v1 | `ControlV2_RoundTripsInputFlagsLookActionAndOutcomeAndRejectsV1` |
| Queued stale identity, local validation, Stop preemption, malformed ack | `Supervisor_FencesInputQueuedDuringNewRuntimeStartupWithoutFaultingIt`, `Supervisor_InvalidLocalInputAndPreCancelledWaitDoNotTouchRuntime`, `Stop_PreemptsPendingInputAndNoOldRequestReachesRestartedRuntime`, `Supervisor_RejectsMalformedInputAcknowledgementAndCleansOwnedRuntime` |
| Focused clean run | `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --no-build --filter FullyQualifiedName~TrueMoon.Aether.Tests.Input --logger trx --results-directory TestResults/ENG5b/focused-42 --verbosity quiet`: Passed42, Failed0, Skipped0 |
| Full managed regression | `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5b/final-Debug --verbosity quiet` и аналогично `Release`/`final-Release`: в каждой configuration Passed1968, Failed0, Skipped0,9TRX |

В каждом full run: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/
UI615/Hosting79/Aether562. Это test builds, не новый nonincremental/performance
baseline. Source/assertion review добавил readiness/playing lease/host reset/
unknown ack/Stop-in-flight checks. Assertion outcomes включают exact transforms,
state/mode/tick/resource/command side effects, exceptions и wire fields; static
mutation reasoning, не выполненный mutation score/coverage. IDE `findTests`
недоступен; Roslyn parse-only pairing подтвердил пять target source files с
новыми tests (interface/CLI native paths не равны line coverage). Broad working
artifacts в OS Temp, вне Git; framework/packages не менялись.

### Native input proof и ограничения

`--input-proof` вызывает настоящий private Silk KeyInput callback через reflection
для WASD/E/Escape (не OS messages), остальные normalized events — через реальный
UiSession в GPU окне.15cases: letterbox click не захватывает input; paused staging observational;
WASD/look/quick E на Step; held movement без повторного look/E; Escape/Editor focus;
Editor keys не двигают camera; staged input очищен при focus/capture loss;
resize/suspend; opposite WASD проверяет Silk mapping; camera/tick/interaction Replace;
old input не направляется новому runtime; held/pending E сброшены после Replace;
30Hz sender в Playing; Pause button neutral; сохранение Editor/authoring ownership.
5PNG фиксируют initial, moved/interacted, resize, replacement material и Playing→Pause.
Checkpoint assertions отделены от GPU pixels; imported ReadPixels0.

Полная matrix с первоначальным14-case input proof:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5b-20261021 -IncludeInput -IncludeScene -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
```

Completed=true: **56cases/58commands**, Debug apphost и published Release:
по20headless control/lifecycle/build/checkpoint/replacement, CPU viewport/Editor,
GPU import/surface/viewport/ordinary Editor, ENG5a scene и ENG5b input.
440source/76binary fingerprints. После этого усилен только probe harness:
Silk keyboard callback вместо прямого UiKey, opposite keys, staged focus/capture
loss и staged suspend. Production библиотеки не менялись; отдельный final run:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5b-keymap-20261021 -IncludeInput -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @()
```

**2cases/4commands/440source/76binary fingerprints**, в каждом15input cases/
5PNG/2cooperative children/22Silk key callbacks; sender packets81Debug/80Release.
Это correctness counts, не performance metric. Full run Debug binaries и два
исходника старого harness сохранены в `frozen-Debug`/`frozen-input-proof` внутри
первого run; Release binaries в исходном publish folder.31published library DLLs
(кроме EngineSessionProbe.dll) идентичны SHA256 между двумя runs.
Validation errors/warnings0, sender completed, imports ReadPixels0, final owned
GPU resources/handles/mappings/UI sessions0. Intentional fault/kill cases полной
matrix не объявлены normal cooperative exits; такое требование относится к
двум normal input children в каждой configuration.

Локальный `dotnet pack <project>/<project>.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5b/packages --verbosity quiet`
прошёл для TrueMoon.Aether, TrueMoon.Argentis и TrueMoon.Alloy.Platform.Silk.
`TestResults/ENG5b/package-report.json`: все3DLL внутри nupkg совпали SHA256 с
final published proof. Это payload check, не package drawing consumer/publication.
Независимая `TestResults/ENG5b/VerifyCompletion.ps1` Passed=true: обе managed
матрицы, сохранённые full440source/76binary и final440source/76binary inputs,
31library,10final input PNG hashes и3packages совпали. Исторический full run не
подменяется final harness; оба набора evidence сохранены раздельно.
Финальная документационная сверка:26изменённых Markdown, локальные links/anchors
проверены без ошибок; `git diff --check` с `core.whitespace=cr-at-eol` прошёл.
9новых namespaced C# files соответствуют путям; ещё1AssemblyInfo содержит только
assembly attribute. Это scoped audit новых файлов, не повторная проверка всех
проектов. Test-agent research/plan/status находятся только в OS Temp.

Первые development runs: `eng5b-dev-1` отвергнут из-за exact background RGBA8
сравнения; приведён к уже принятому±2 quantization допуску из ENG5a, aspect и
opacity assertions сохранены. `eng5b-dev-2` Debug input14cases/5PNG прошёл.
Первая test compilation указала на xUnit async Throws; исправлено на awaited
ThrowsAsync. Ни skipped tests, ни weakened mode/tick/ownership checks не добавлены.

Физические клавиатура/мышь/OS focus и raw cursor profile не автоматизированы;
native proof подтверждает normalized UiSession routing на actual GPU output.
3D performance/VRAM/loss/golden/RTE/full UI alpha и K3/K7/K8 остаются вне scope,
K7 workaround search не возобновлён. Следующий функциональный этап — ENG5c:
комната/пол/несколько примитивов, осмысленный interaction и Alloy HUD/pause.

## ENG5a: первый Vulkan 3D frame — 2026-10-21

Реализован ограниченный первый renderer в одной сборке TrueMoon.Aether.
`Rendering.IRenderGameSession` предоставляет immutable `PrimitiveScene`: один
unit cube, camera pose из checkpoint, rotation, RGB material, FOV/near/far.
`RuntimeProtocol.CaptureRenderFrame` захватывает scene и tick под общим lock с
commands/simulation/checkpoint; startup restore gate и stopping запрещают capture.
Capability optional; прежний IGameSession/CPU/reference pattern сохранён.

`GpuFrameData.FromScene` передаёт immutable draw workload и точный frame/tick;
Pixels пуст. Vulkan graphics pipeline, embedded SPIR-V, 36 vertices без vertex
buffer, D32 depth test/write и80-byte push constants рисуют непосредственно в
exportable RGBA8 image. Scene color — stored-sRGB с фиксированной интенсивностью
граней; это простой opaque material, не PBR/linear-light освещение.
Right-handed world, +Y up, -Z camera forward, Euler radians, depth [0,1]; Y
projection flipped для top-left image. Перед recording валидируются параметры
и representable matrix. Reference staging buffer теперь создаётся лениво;
3D не создаёт CPU pixel/staging allocation. Pipeline/framebuffer/depth/views
принадлежат slot, cleanup идёт после fence и до image/device. Существующие
external queue ownership barriers, semaphores,3slot ledger и surface generations
сохранены; UI владеет retained copy на своём thread/context.

`--scene-proof` использует обычные App/UseAether/Editor/Build/Replace и GPU
transport. Game.Logic остаётся только в child. Пять PNG/pixel assertions:
`01-cube` (bright front, darker back submitted later — depth oracle),
`02-camera` (3Step, projected centroid сдвигается влево), `03-resize`800×450,
`04-resume`320×240 после0×0, `05-replace-material` (новая DLL/color,
сохранены tick3/camera/Paused). Окно/root/authoring прежние, teardown GPU до UI.
Readback только после UI presentation для assertions; imported texture delivery
не вызывает ReadPixels. В scene proof включён aspect fit Canvas с проверкой
соотношения сторон image/render target; reference examples сохраняют прежний
режим. Readback assertions используют fitted image bounds.
RunProbe включает `-IncludeScene`, fingerprint GLSL/SPIR-V,
проверку PNG SHA256 и неизменности source/binary inputs.

### Requirement | Evidence

Новые focused xUnit проверки находятся в
`TrueMoon.Aether.Tests.Rendering.PrimitiveSceneTests`:

| Requirement | Evidence |
| --- | --- |
| Perspective, top-left Y, aspect, near/far Vulkan depth | `Transform_ProjectsCenterUpAspectAndVulkanNearFarPlanes` |
| Camera pose и model rotation влияют на projection | `Transform_UsesCameraPoseAndCubeRotation` |
| Invalid camera/FOV/clip/material, zero extent/overflow отвергаются | `Validate_RejectsInvalidCameraProjectionOrMaterial` (13 partitions); `Transform_RejectsSuspendedExtentAndUnrepresentableCamera` |
| GPU-only workload, точный tick/frame/slot и запрет mixed pixels/scene | `SceneFrame_ProducesExactMetadataWithoutCpuPixelsAndRejectsMixedWorkloads` |
| Capture не advances tick, совпадает с checkpoint camera, loaded/stop gate | `RenderCapture_RequiresReadinessCapturesCheckpointCameraAndRejectsStop` |
| Restore gate и optional capability | `RenderCapture_RejectsPendingRestoreAndUnsupportedCapability` |
| Focused validation | `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~PrimitiveSceneTests --logger trx --results-directory TestResults/ENG5a/managed-focused-green --verbosity quiet`: Passed19, Failed0, Skipped0 |
| Full managed regression | `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5a/final-Debug --verbosity quiet` и аналогично `Release`/`final-Release`: в каждой конфигурации Passed1926, Failed0, Skipped0,9TRX |

В каждом full run: Core107, Cobalt179, generator91, Enerit126, generator114,
Mithril95, UI615, Hosting79, Aether520. Это test builds, не новый nonincremental
build/performance acceptance. IDE `findTests` вернул Tool not found; использованы
исходники соседних tests. Первый focused run:18passed/1failed из-за ожидаемого
exception type для16×0; production правильно бросал ArgumentOutOfRangeException,
исправлен test. Результат сохранён в `TestResults/ENG5a/managed-focused`.

### Итоговая native/package проверка

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5a-20261021-3 -IncludeScene -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @('repeat')
```

Completed=true: **12cases/14commands**, Debug apphost + published Release,
430source/76binary fingerprints, source/binary inputs неизменны. В каждой
конфигурации scene5PNG/2children; старые one-frame import14cases/8children,
surface21cases/80frames/12children, viewport17cases/14captures/20copies/6children,
ordinary GPU Editor20Replace/20failure recoveries/53children и repeat20Start/Stop.
Fault cases старых probes намеренно завершают часть процессов принудительно;
только два normal scene children в каждой конфигурации требуют cooperative exit0.
Core/sync validation ошибок/предупреждений0; imports ReadPixels0. Все reports
подтвердили scoped cleanup. В scene runtime1 освободил3surfaces/9slots/3devices,
runtime2 —1surface/3slots/1device; live resources/handles/mappings/UI sessions0.

Scene assertions сохранены как `01-cube`, `02-camera`, `03-resize`, `04-resume`,
`05-replace-material` и SHA256 в `Debug-scene/scene-report.json` и
`Release-scene/scene-report.json`. Centroid front material смещается влево
после3Step и сохраняется при Replace (допуск0.005). PNG относятся
к final UI surface, не physical display/OS compositor color proof.

Локальный `dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5a/package --verbosity quiet`
прошёл. `package-report.json` подтвердил: DLL в
`TrueMoon.Aether.0.2.2.7-alpha.nupkg` совпадает SHA256 с DLL native published
Release proof (`5D5D41FB9EBF8327F110B745BA938EA7D673985C44A291820D9F1692E3ECBE6D`),
включая embedded shaders. Это payload/pack check, не отдельный package drawing
consumer и не публикация. shader compile + spirv-val Vulkan1.1 прошли для обоих
модулей; обычный build/publish не требует compiler.

Независимая итоговая сверка `TestResults/ENG5a/VerifyCompletion.ps1` прошла:
`completion.json` Passed=true,9TRX/1926passed в каждой configuration,
430source/76binary/10PNG SHA256 совпали, package payload прежний.
`CheckDocs.ps1`:26изменённых Markdown,716локальных links/313anchors, errors0.
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл;
4новых C# files проверены на namespace/path correspondence, violations0.
Это focused проверка новых files, не новый полный structural audit репозитория.

### Эксперименты и границы

Первый Debug proof `TestResults/EngineSessions/eng5a-dev-1` прошёл. Следующий
`eng5a-20261021` отвергнут: диагностическое обновление Status в FramePresented
инвалидировало UI до ReadbackOpenGLImage. Capture перенесён на следующий кадр
через PostWindow; не ослаблен dirty-output guard. В отвергнутом run один child
вышел с освобождёнными ресурсами, core/sync errors/warnings0; этот run не passing.
Матрица `eng5a-20261021-2` прошла до финального исправления aspect fit;
финальная source freeze/матрица — `eng5a-20261021-3`, без растягивания3D image.

Полный ENG5 не завершён. Далее ENG5b: input capture/routing WASD/mouse/E/Escape,
camera control на fixed tick и checkpoint/Replace; затем комната/пол/примитивы,
переключатель света/цвета и Alloy HUD/pause. ENG7 standalone отдельно. Текущий
ProbeGame всё ещё демонстрирует движение camera через Step reference simulation.
3D performance/VRAM/hardware loss не измерены; ENG4e reference timings сюда не
переносятся. PA8a/RTE/K3/K7/K8/final alpha остаются прежними, K7 search остановлен.

## ENG4e CPU/GPU baseline — 2026-10-21

Реализован [отдельный runner](../../ManualTests/EngineSessionProbe/RunViewportBaseline.ps1)
поверх обычных App/Editor/FrameSession transports. Opt-in internal EventSource
`TrueMoon-Aether-Frames` измеряет production, CPU read/copy/CRC, GPU import,
produce round-trip, retained submission и owner/use/fence. Без listener история
не хранится. CLI capture bounded:100000stage records/4096process snapshots;
overflow/EventSource errors/mismatched QPC frequency делают experiment неполным.
UI sample добавляет distinct actual FramePresented. CPU/GPU разные wire timestamps
не используются как одинаковая граница production: latency связывается с traced
start по session/runtime generation/surface generation/frame number. Frames,
начатые в warmup, в measured production cohort не входят. Повторная presentation
одного frame дедуплицируется; percentiles используют все samples интервала.

[Протокол и advisory budgets](PLAN.md#eng4e-протокол-измерений--2026-10-21)
записаны до полного запуска. Workload: одинаковая Game.Logic DLL/FramePattern/color,
artifacts двух revisions/manifest identities; один App/window на case,
Start640×360 → Resize1280×720 → Replace1280×720 → Stop. Simulation60Hz/
producer30Hz/UI60Hz targets; verification readback перед2s warmup и5s measurement.
По3повтора каждого mode/configuration, чередование CPU/GPU и GPU/CPU. JSON пишется
после измерений;200ms process polling/EventSource overhead включён в counters.
Дочерний capture worker drains/persists до App teardown.

Финальный experiment: **12/12cases,36intervals,24cooperative children,36pixel
verifications**,15commands,421source/94binary fingerprints; Completed=true.
Windows x64/build26300/SDK10.0.402/net10.0,24logical processors/QPC10MHz.
GPU adapter UUID `D69A207DD3EC6D8F5F325359A59FA086`, driver UUID
`8B2ED3A7D66651B8AD47BB85416B871B`; environment сохраняет NVIDIA RTX5070Ti
driver32.0.16.1742 и AMD Radeon32.0.21042.62. GPU transport использует eligible
GL adapter identity; driver/Windows settings не менялись. Validation layers
выключены только в measurement processes, correctness validation выполняется
отдельно. Нет imported ReadPixels, owned GPU resources/handles/mappings/UI sessions
после каждого case —0; Game.Logic в Editor не загружен.

```powershell
./ManualTests/EngineSessionProbe/RunViewportBaseline.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/ViewportBaseline/eng4e-20261021-3
```

Raw/commands/environment/fingerprints: `TestResults/ViewportBaseline/eng4e-20261021-3/summary.json`;
полная36строчная [таблица результатов](../../TestResults/ViewportBaseline/eng4e-20261021-3/REPORT.md).
Каждый case содержит experiment.json, baseline-report.json и3raw captures.
Всего **37766stage records**; measured cohorts **5244produced/5244received/
5182presented**, unreceived0/unpresented62. Последнее — produced frames без
distinct actual acknowledgement, включая границы resize/Replace/Stop; эта метрика
не объясняет единственную причину потери. Producer dropped attempts0 означает
отсутствие ring no-free-slot drops; пропущенные timer ticks не подсчитываются.
Unreceived/unpresented пересекаются, складывать их нельзя. FPS ниже — throughput
acknowledged production cohort за measured interval, не частота повторных redraw.

### Сводка трёх повторов

Задержки вms; p50/p95/production/retained — **медианы соответствующих per-run
percentiles**, не pooled percentile. FPS: медиана [min–max]; allocation — медиана
runtime managed MiB/s по фактическим inner polling intervals. Полные editor/runtime
CPU cores, allocations, handles, private/working-set peaks и GC deltas находятся
в raw/per-case report. Process private bytes не являются VRAM.

| Configuration | Mode | Участок | FPS [min–max] | Produce→present p50/p95 | Production p95 | Retained submit p95 | Runtime MiB/s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Debug | CPU |640×360 |30.19 [27.57–30.20] |41.00/49.38 |11.32 |0.26 |0.100 |
| Debug | CPU |resize1280×720 |24.80 [12.51–25.20] |89.99/105.95 |47.16 |0.69 |0.089 |
| Debug | CPU |Replace1280×720 |25.00 [23.39–26.60] |90.84/104.83 |46.96 |0.72 |0.087 |
| Debug | GPU |640×360 |30.20 [30.13–30.39] |13.73/22.21 |4.91 |0.64 |27.039 |
| Debug | GPU |resize1280×720 |30.20 [30.19–30.36] |21.39/30.14 |13.41 |0.74 |106.707 |
| Debug | GPU |Replace1280×720 |30.20 [29.74–30.20] |20.42/29.58 |13.08 |0.61 |107.401 |
| Release | CPU |640×360 |29.98 [29.37–30.00] |49.54/64.79 |2.55 |0.07 |0.100 |
| Release | CPU |resize1280×720 |29.99 [29.93–30.33] |50.34/65.42 |8.87 |0.65 |0.104 |
| Release | CPU |Replace1280×720 |29.92 [29.51–29.99] |51.91/66.24 |8.91 |1.10 |0.097 |
| Release | GPU |640×360 |30.19 [29.79–30.20] |12.70/21.35 |4.77 |0.70 |27.203 |
| Release | GPU |resize1280×720 |30.20 [29.73–30.33] |15.71/21.49 |5.80 |0.67 |106.701 |
| Release | GPU |Replace1280×720 |30.00 [28.36–30.20] |17.50/33.13 |5.78 |0.71 |106.673 |

Advisory breaches: **2/36intervals FPS<24 и6/36 latency p95>100ms**, все — Debug
CPU1280×720; outlier12.51fps сохранён. В Release и GPU intervals эти budgets
выполнены. Editor/runtime allocations не превысили32/256MiB/s; все48lifecycle
операций уложились в15s. Completed не означает все budgets met или30fps guarantee.
GPU runtime allocation~107MiB/s при1280×720 согласуется с new RGBA byte[] на каждый
FramePattern в GpuFrameData.GamePattern; это вывод по measured rate и исходнику,
не attribution profile. Этот reference path всё ещё CPU-generated/Vulkan staging,
а не performance будущего3D renderer/zero-copy. Нужные оптимизации выбирать по
реальному workload; временный pattern не становится minimum hardware promise.

### Проверки и первоначальные failures

Новые focused xUnit cases:8/8. В составе финального solution run Aether501/501,
Mithril95/95; UI615/Hosting79/Core107/Cobalt179/generator91/Enerit126/generator114.
**Debug1907/1907 и Release1907/1907, по9TRX, failed0/skipped0**. Это incremental
dotnet test builds; nonincremental build/golden/RTE/full hardware matrix здесь
не повторены. XML warnings backlog не подавлялся.

| Требование | Точный тест |
| --- | --- |
| Runtime/surface identities, warmup exclusion, distinct presented/cohort accounting | `BaselineAnalysisTests.Analyze_JoinsSurfaceAndRuntimeGenerations_DeduplicatesPresentsAndExcludesWarmup` |
| Foreign session и unavailable вместо0 | `BaselineAnalysisTests.Analyze_GpuReceivesUseExactSessionIdentity_AbsentResourcesRemainUnavailable` |
| Mismatched clocks/overflow/broken capture отвергаются | `BaselineAnalysisTests.Analyze_RejectsClockMismatchAndIncompleteCaptures` |
| Все samples, nearest-rank и empty/null | `BaselineAnalysisTests.Summarize_UsesAllSamplesAndNearestRank_EmptyIsUnavailable` |
| Negative/NaN/infinity не становятся latency | `BaselineAnalysisTests.Summarize_RejectsInvalidLatency` (3cases) |
| Actual opt-in payload/identity/timestamp соответствует runtime pattern | `FrameTraceTests.GamePattern_OptInTraceMatchesExactFrameAndEnclosesProductionTimestamp` |

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter "FullyQualifiedName~BaselineAnalysisTests|FullyQualifiedName~FrameTraceTests" --logger trx --results-directory TestResults/ViewportBaseline/development/focused-tests-final --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Release --verbosity quiet
```

До финальной матрицы short CPU прошёл; short GPU/первый full GPU иногда терял
первый child capture. После переноса flush перед App Stop второй experiment дошёл
до11/12accepted cases: последний Release GPU имел полный raw data, но был rejected
из-за Killing первого child. Найдена/исправлена
[гонка Mithril delivery/graceful wait](../mithril/HISTORY.md#graceful-wait-после-ошибки-доставки-exit--2026-10-21):
закрытие control pipe после принятого stdin exit пропускало graceful wait.
Budget3s не увеличен; deterministic red/green и native20shutdown cases прошли.
Предыдущие `eng4e-20261021-1/-2` остаются Completed=false; passing части не
переносятся в финальный proof. В финальном -3 все24children exit0/no Killing.

Ранний full Debug1906run дал Enerit restart IOException sharing violation при
чтении ready-2.json. Отдельная AppUnitsProcessTests theory18/18 и последующие
full Debug/Release прошли; unrelated readiness helper не менялся, race не объявлена
исправленной. Первая trace unit fixture содержала Version4 вместо Version2 GPU
stamp; исправлен только тестовый input,8/8после этого прошли.

GPU query/utilization/VRAM counters — unavailable, CPU durations не GPU timings.
Long resource trends, physical DPI/device loss, K7/final alpha и publication
остаются открытыми. Остановленный NVIDIA workaround search не возобновлялся.
Следующий функциональный этап — ENG5: минимальный3D renderer в существующий
viewport, с отдельным workload/baseline; CPU Debug debt/temporary staging allocation
сохранены как measured backlog, без автоматической смены default transport.

### Отдельная correctness и package regression

После measurement выполнен `RunProbe.ps1`: **6/6cases,8commands,421source/
76binary fingerprints**, Debug apphost/published Release. Actual GPU viewport,
ordinary GPU Editor и repeat lifecycle; core/sync validation0errors/0warnings.
Per Editor20Replace/20failure cases/53children, retained copies829Debug/813Release,
imported readbacks0, owned GPU handles/resources/mappings/UI sessions0. Эти checks
не добавляются к performance samples и не заменяют полный UI/hardware acceptance.

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng4e-correctness-20261021 -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @('repeat')
./ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/UnitsPackages/eng4e-20261021
```

Fresh units consumer: **108/108cases,8nupkgs/9projects,128commands/206source
fingerprints**, Completed=true, version`0.2.2.7-alpha.units.20261021094727716`.
Cobalt/DI/DI без Cobalt analyzer, Debug/Release build/publish; expected omitted
Enerit analyzer failureCS0234. Это delivery/App/IPC regression исправленного
Mithril, не Aether GPU package drawing proof. Пакеты локальные, не публиковались.

Итоговая integrity проверка `TestResults/ViewportBaseline/development/VerifyFinal.ps1`
независимо пересчитала cohort counts/p50/p95/FPS по всем36intervals/37766raw records.
Текущие1048source и170binary hash checks совпали;36raw capture hashes сохранены
в `completion.json`, Passed=true. Exact TRX totals проверены отдельно — по1907
passed/failed0/skipped0 в Debug/Release. Документационные проверки не создают
новых passing code/native runs.
Проверки документации:25изменённых Markdown files,711локальных ссылок/309якорей,
ошибок0; `git diff --check` прошёл. Scope включает сохранённые изменения предыдущих
шагов в текущем checkout; этот счётчик не является числом новых файлов ENG4e.

## ENG4d2 ordinary GPU GameEditor — 2026-10-20

Запрос «давай приступим к следующему шагу» реализован как следующий принятый ENG4d2.
Обычный ViewportView/команды Editor теперь явно выбирают CPU/GPU; shared lifecycle
в FrameSession не дублируется. App option ViewportTransport snapshots strategy
factory, connections принадлежат FrameSession, process Stop/reap — Supervisor,
imports — App GPU owner, независимый Skia image — UI consumer. Disposable strategy
освобождается exactly once; aliases в DI не добавлены. Aether остаётся одной engine
assembly, без UI/Skia/Game.Logic dependency.

Новый neutral contract: ViewportSurface (identity/generation/extent/mode без handles),
ViewportPresentation и выданный ею ViewportFrame ticket. Issue/copy не являются
first-present. FrameSession.Presented принимает только latest ticket активного
runtime/surface; stale/foreign/closed отклоняются. CPU Presentation разделяет
readiness с существующим receiver/display, старые CPU APIs сохранены. Replacement
использует neutral dimensions и тот же checkpoint/one rollback pipeline.

GpuViewportTransport подключает owned native lease, serial pump ProduceFrame →
owner callback/Skia GPU copy → observed GL completion → Return/Vulkan retirement.
Первый image pinned до actual presentation. Resize closes old tickets/stops pump
admission и drains accepted operation перед configure; Stop fences/cancels и drains
connection до child Stop. Failed cleanup retains connection и блокирует Start.
Background failed cleanup сохраняет cause/aggregate в Frames.TransportError;
успешный Stop retry не воспроизводит faulted watcher. Diagnostic очищается следующим
admitted Start. Caller/UI context живёт до успешного native cleanup.

Runtime normal role через свой UseAether hosts lazy GPU channel с optional
frameProvider. Атомарный CaptureFrame требует loaded/restored ready game и
отклоняет stopping generation. GpuFrameData валидирует tight RGBA8 shape,
slot/frame metadata/timestamp до upload; client проверяет metadata после Ready.
FrameNumber привязан к трём round-robin slots с checked overflow. Reference
GamePattern использует selected game's tick/color, CPU-generated RGBA и Vulkan
staging; полноценного GPU 3D renderer пока нет. Pixel payload не идёт в pipe,
imported ReadPixels не используется для delivery. Diagnostic v1/v2 defaults прежние.

Окончательный run: `TestResults/ENG4d2/complete3/summary.json`,7owned commands/
exit0; Aether493/493 Debug/Release, UI615/615 Debug/Release; full Debug1898/1898,
9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI615/Hosting79/Aether493. Nonincremental parallel Debug0errors/1499warnings.
Native `TestResults/EngineSessions/eng4d2-complete-20261020/summary.json`:
Completed=true,54aggregate cases/56commands/406source/76binary fingerprints.
Ordinary GPU Editor Debug815copies/791pixel checks; published Release800/783.
Each20cycles/20failure cases/53children, validation0/0,47devices/141slots balanced.
Old CPU Editor20Replace/53children, CPU viewport17cases/27children, headless20,
GPU capabilities/import/surface/reference viewport повторены в обеих configurations.
Separate retained GPU viewport proof снова проверил3,051,552pixels/config через
independent PNG decoder,14captures, imported readback0. These remain separate scopes.

Fresh package evidence — `TestResults/ENG4d2/package2/summary.json`:12packages/
18commands/4independent Cobalt+DI consumers (Debug + published Release), version
`0.2.2.7-alpha.aetherapp.20261020172158631`. Consumers compile/check GPU APIs,
TransportError и общий fake transport first-present contract; App startup lazy,
strategy/channel/GPU/factory/lease dispose exactly once, late tickets/Start rejected.
Consumer Aether/Skia DLL/native payload hashes match nupkg; no source ProjectReference.
This is managed package composition/native delivery, not package GPU rendering.

Commands (fresh output directories required):

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4d2/complete3/solution-Debug --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/ui-Release --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4d2/ValidatePackageFinal.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4d2-complete-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Тесты новых API: latest/foreign/closed/invalid/suspended tickets; exact failure,
shared CPU receiver ticket/readiness; Start/resize/suspend/Stop и stale identity;
worker fault до/после first-present; connection cleanup до process Stop; combined
startup+cleanup errors/retry; background failed cleanup/retry без sticky watcher;
lazy App/exactly-once strategy ownership; loaded/stopping capture; RGBA order,
corners/frame/tick/color и invalid/overflow metadata. Pseudo-mutation/assertion
review выявил missing direct CPU-ticket и background cleanup-retry cases; оба
добавлены и проверены перед final snapshot. No coverage percentage/mutation-suite
execution claim. IDE findTests ранее недоступен; xUnit discovery/real runs использованы.

Native ordinary GPU proof повторяет20A/B Replace в одном App/window и20failure
cases с53owned children/MaxLive1. Loaded DLL/manifest hashes/source revisions,
complete checkpoint/restored state, first actual frame tick, next Step behavior
(camera X+.25/.125/Yaw+.01) и прежний mode проверяются. Compile failures/held jobs,
cancel/context/stale-session/Stop/deadline, missing/tampered builds, capture rejection/
unknown outcome, bad Hello/restore/no-frames, rollback/double failure, cleanup retry,
native WM_CLOSE с pending Replace/compiler покрыты. Extra GPU resize64×32/0×0/
160×90/stale ticket; все53cooperative reports дают47devices/141slots balanced,
core/sync validation0errors/0warnings. Imported readback0, local resources/handles/
mappings/UI sessions0, authoring unchanged, Game.Logic в Editor не загружен.

Исходный `native-initial` завис после capture первого поколения; остановлен только
owned process tree с проверкой PID/command line. Review нашёл unsafe assert, что
ticket обязан приниматься после diagnostic readback: Stop вправе успеть supersede
его. Assert удалён; Debug quick proof `eng4d2-debug-initial` прошёл20cycles/20failures/
53children/804copies/782pixel checks. Точный stack исходного stall не получен, он
не выдаётся за passing. Первый full `eng4d2-verified-20261020` прошёл54cases до
последующего UI availability/GL-ready/CPU-ticket review. Следующий rerun
`eng4d2-final-20261020` остановлен как owned superseded tree при обнаружении
background cleanup watcher gap. Только final snapshot выше является текущим.

Ordinary UI теперь показывает transport/neutral surface, ожидает GL binding ready
до Start и отображает transport failure; нет ложного GPU 'viewport unavailable'
из-за null legacy CPU Display. GPU mode явный (`--viewport-gpu`/project option),
CPU default сохранён; автоматического fallback на fault нет.

Limits: reference Windows x64/RTX5070Ti/150% DPI; ordinary GPU extents64×32/160×90,
larger extents/premul/compositing/clipping проверяются отдельными GPU probes.
Проверяется retained UI surface после FramePresented, не compositor/physical
display. Package consumers проверяют managed fake-transport API и delivery, не
actual native GPU drawing. Driver loss/hang, throughput/zero-copy/VRAM/allocations,
full Release solution/golden/RTE/final UI hardware acceptance/publication не
доказаны этим шагом. K3/K7/K8/VRAM/final alpha и K7 stop policy сохранены;
drivers/Windows settings не менялись. Следующий шаг — ENG4e measured baseline
на одинаковых CPU/GPU reference workloads перед ENG5/3D, без преждевременной
performance guarantee или оптимизации.

## ENG4d GPU viewport bridge — 2026-10-20

Запрос «давай приступим к следующему шагу» продолжен первым bounded результатом
ENG4d: actual reference GPU viewport. Остальной GameEditor transport integration
оставлен явным ENG4d2 в единственном engine PLAN/STATUS; ENG4 не объявлен закрытым.

`GpuSurfaceSession.UseTextureAsync` loans `GpuOpenGlTexture` на borrowed GL owner
после CPU ready + GPU wait. Все source uses должны быть submitted до callback
return/throw; после callback loan недоступен. Return semaphore/observed GL fence
предшествуют explicit Return/Vulkan retirement. Callback exception/cancel fences
admission. Configure drains Ready через пустой use без ReadPixels. Existing v1
Read/v2 diagnostic APIs сохраняются. App owns imports; runtime Stop/reap — Supervisor.

Generic Rendering.Skia `CopyTexture` borrows RGBA8 premul stored-sRGB/top-left
texture без adoption, copies в независимый GPU SKImage и submits source use даже
при исключении. Image bound к исходному GRContext/surface owner; drawing через
raster/foreign drawing context и use после dispose отвергаются. UI consumer
заменяет/disposes image до surface/context teardown. Aether→UI dependency нет.
Это одна GPU copy/frame; GPU completion и first actual presentation раздельны.
Primary API references: [SKImage.FromTexture](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skimage.fromtexture),
[GRContext.Flush](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.grcontext.flush).

`TestResults/ENG4d/complete/summary.json`:7owned commands/exit0. Nonincremental
parallel Debug build0errors/1499warnings. Aether453/453 Debug/Release. UI615/615
Debug/Release, full Debug1858/1858/9TRX/failed0/skipped0: Core107/Cobalt179/
generator91/Enerit126/generator114/Mithril94/UI615/Hosting79/Aether453.
Managed additions:9loan cases (all properties after end/foreign thread, exact
descriptor/session cause);2image ownership cases (actual drawing adapter context
guard, successful origin pixels, failed dispose/retry/use-after-dispose). Final
review strengthened the draw-path assertion; solution Debug/UI Release repeated
after that test-only change in `TestResults/ENG4d/review-tests.json`.

Final native evidence — `TestResults/EngineSessions/eng4d-verified-20261020/summary.json`:
Completed=true,52aggregate cases/54commands/398source/76binary fingerprints.
Each Debug apphost/published Release GPU viewport:17checks/14presented captures/
20copies/6children, imported ReadPixels count0, authoring unchanged, local GPU
resources/NT handles/UI sessions0. Five cooperative teardown reports:7devices/
21slots created/released, core/synchronization validation errors0/warnings0. One
deliberate crash has owned reap without cooperative teardown claim.

Whole interior canvas pixels checked after FramePresented: stored sRGB/premul over
RGB19/37/61, asymmetric origin, actual150% DPI/bilinear scale, clipping. Fractional
outer canvas edge excluded. Independent PNG decoder/filter reconstruction in
`TestResults/ENG4d/CheckViewportPixels.cs` rechecked3,051,552pixels/config from saved
PNG files/hashes without invoking production pattern/import/Skia readback. Capture
stamps and increasing actual PresentedFrames checked. Retained frame survives
12additional slot uses, resize/suspend, Stop/crash. New runtime rejects old stamp;
Return inside consumer rejected; submitted-copy exception and cancel before owner
dispatch fence late use; restart/WM_CLOSE drain before UI owner disposal.

ENG1–ENG3 and ENG4a/b/c repeated: each surface21checks/80frames/12children,9coops/
15devices/45slots, validation0/0; CPU viewport17cases/27children and Editor20Replace/
20failures/53children/MaxLive1. Per-run CPU pixel counts stay in native reports.
No Game.Logic assembly loaded into Editor; selected DLL/checkpoint proof remains CPU.

Fresh package evidence — `TestResults/ENG4d/package/summary.json`:12packages/
18commands/4independent Cobalt+DI consumers (Debug + published Release), version
`0.2.2.7-alpha.aetherapp.20261020140450577`. New UseTextureAsync/CopyTexture APIs
compiled/checked, lazy App/exactly-once GPU/factory/lease cleanup and late Start
rejection repeated. Consumer Aether/Skia DLL and transitive native DLL hashes match
fresh nupkgs; source ProjectReference absent. This is package API/delivery proof,
not native GPU drawing from a package consumer or NuGet publication.

Commands:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4d/complete/review-solution-Debug --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/ENG4d/complete/review-ui-Release --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4d/ValidatePackage.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4d-verified-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Initial native reference rejected actual150% DPI because it assumed100%; corrected
to physical pixel centers without changing Windows settings. Initial image retry
test exposed duplicate Size verification; Size now obtains the image once. Final
passes above supersede `native-initial`/initial focused failure, which remain logs.
`native-dpi`/`native-guards` were intermediate successful runs, not the final matrix.

Limits: reference Windows x64/RTX5070Ti; texture source16²/64×32/320×180 in GPU
viewport, larger1280×720 separately in ENG4c. Retained UI surface readback follows
actual presentation, not compositor/physical display color proof. GPU generation
replacement is reference producer Stop/Start, not Game.Logic DLL/checkpoint GPU
integration. Native driver loss/hang, VRAM/throughput/zero-copy, full Release
solution/golden/RTE/final hardware matrix and publication not claimed. K3/K7/K8/
VRAM/final alpha unchanged; no driver/Windows setting changes or K7 workaround work.

## ENG4c reusable slots и surface lifecycle — 2026-10-20

По запросу «давай приступим к следующему шагу» реализован следующий согласованный
ENG4c. `GpuSurfaceSession` и producer channel2 находятся в одной TrueMoon.Aether
assembly, получаются через `AetherHost.Gpu`/UseAether/App lifecycle. Новых package
dependencies/UI references нет; ENG4b/v1 строгие contracts сохранены.

### Контракт и реализация

Три persistent slots делят один Vulkan instance/device/queue. У каждого dedicated
image/memory, staging buffer, command buffer, fence и2binary semaphores. Второй/
третий native frames заимствуют device context первого; surface сериализует queue
operations и освобождает children раньше device owner. Failed child cleanup
сохраняет shared device для retry. Счётчики измеряют actual CreateDevice/DestroyDevice.

Version2 stamp: full SessionIdentity/SurfaceGeneration/Slot/FrameId. FrameId растёт
per-slot только после GL observed completion и Vulkan Retire; Ready/read-complete
slots нельзя overwrite. Допускаются3outstanding Ready frames с произвольным
порядком return. Native pixels зависят от surface/slot/frame, поэтому старый кадр
не проходит whole-image comparison. До foreign queue wait нужен CPU-observed fence.

Configure fences новые operations, ждёт принятую команду, drains Ready/read-complete
slots, освобождает GL imports, подтверждает exporter detach и только затем
создаёт fresh generation.0×0 suspends, resume также меняет generation; partial-zero/
invalid extent отвергается до I/O. Concurrent commands/Configure отклоняются,
unbounded queue нет. Stop/unknown result/runtime exit fences late work; owned
imports остаются до successful cleanup/retry. На App один unreleased reusable
session. GPU Dispose не присваивает process ownership: затем Editor/Supervisor
останавливает/reaps lease; App closes GPU до child/UI. Borrowed GL dispatcher/context
живут до cleanup. Ordinary viewport остаётся CPU, ReadAsync — diagnostic readback.

Отдельный CurrentUserOnly pipe:64KiB/strict JSON/full identity/sequence,
Connect3s/command8s/error reply2s, GL fence polls timeout0. End/accepted commands/
exit watcher входят в App drain. Runtime `RunSurfaceProducerAsync` владеет worker/
pool entries; LastSurfaceProducer сохраняет bounded aggregate counters/validation.
No DeviceWaitIdle/glFinish. Managed deadline не прерывает driver/native call или
произвольный user dispatcher.

### Требования и точные проверки

| Требование | Test / evidence |
| --- | --- |
| «давай приступим к следующему шагу» — ENG4c | `GpuSurfaceTests`, `GpuSurfacePipeTests`, native `--gpu-surface`; итоговые commands/reports ниже |
|3slots, bounded outstanding, no reuse до retire | `ThreeSlots_ExhaustionAndOutOfOrderRetirementNeverOverwriteBusyImage`; native three-slots-exhaustion/persistent-reuse (60frames, allocation offers прежние) |
| exact frame/surface/session/version/fence guards | `CompletionGuards_RejectEveryStaleDimensionWithoutAdvancingPhase` (42cases), `SlotGenerations_RejectPreviousUseAfterReuseAndFaultIsTerminal`; native stale-frame/resize/restart |
| immutable exact allocation set/V1 boundary/bounds | `SurfaceOffer_ValidatesEntireAllocationSetBeforeImport` (12), `SurfaceOffer_SnapshotsSourceAndOneFrameV1StillRejectsVersionTwo`, `Extent_PartialZeroAndUnsupportedBoundsAreRejected` (7) |
| changing channels/alpha/origin bytes | `ReferencePattern_IndependentFrameSlotAndSurfaceMarkersPreventStaleFrameAcceptance` pinned literals;80whole-image native frames/configuration и independent73reference hashes |
| pre-allocation protocol validation/suspend generations | `InvalidSurfaceRequest_CannotAllocateNativeResources` (9), `SuspendedSurface_RejectsRepeatedOrSkippedGenerationWithoutNativeWork` (2) |
| App cancellation/drain без native allocations | `AppClose_DrainsSuspendedSurfaceReaderAndRetainsExactZeroAllocationDiagnostics`, `AppClose_CancelsAcceptedConfigureHookBeforeGpuAllocation` |
| actual lifecycle/retry/crash/close | native21cases: exhaustion, persistent-reuse, stale-frame, resize-drains-ready, suspend-resume, invalid-extent-preserves-surface, resize-trend, queued-resize, cleanup-retry, stop-live-slots, cancel/deadline/3crash modes +5restarts, native-close-pending-live-imports |

### Запуски и raw evidence

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~GpuSurfaceTests|FullyQualifiedName~GpuSurfacePipeTests' --logger 'trx;LogFileName=surface-pipe.trx' --results-directory TestResults/ENG4c/focused --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4c/ValidateVerified.ps1
pwsh -NoProfile -File TestResults/ENG4c/Complete.ps1
```

ValidateVerified запускает nonincremental parallel full Debug build, Aether
Debug/Release, full Debug solution, fresh package consumer и всю native matrix:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4c/complete3/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4c/complete3/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4c/complete3/solution-Debug --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4c/ValidatePackageVerified.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4c-verified-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Focused78/78; Aether444/444 Debug/Release. Full Debug1847/1847,9TRX/failed0/skipped0:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting79/
Aether444. Nonincremental0errors/1499warnings; warning backlog прежний.
SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest. Базовый commit `dc64b63`, прежние
ENG3/ENG4a/b изменения сохранены, commit/push не выполнялись.

Каждый surface run:21checks/80frames/2982400pixels/12owned children reaped.
3crash modes: до Ready, после Ready перед GL wait, после GL use перед Retire;
после каждого failure fresh restart и old identity rejection. Native WM_CLOSE
отменяет accepted held Produce при live read-complete import, cleanup предшествует
UI disposal.9cooperative reports подтверждают15surfaces/devices и45slots actual
create/release, core+synchronization validation errors0/warnings0, live slots/
NT handles0.3deliberate crashes подтверждены owned reap; их cooperative teardown/
native counters не заявляются. Parent GPU resources/handles/UI sessions0, authoring
unchanged. Independent PowerShell computation сверяет73distinct surface/slot/frame
whole-image hashes; duplicate patterns между fresh identities учитываются отдельно
от identity guard.

Fresh9packages/15commands/4consumers Cobalt+DI × Debug/published Release:
`0.2.2.7-alpha.aetherapp.20261020132536109`. Isolated Directory.Build hierarchy/cache,
все assets libraries — packages, consumer DLL hash равен свежему nupkg DLL.
Проверен managed version2 ledger/pattern, lazy startup и exactly-once GPU/factory/
lease cleanup. Silk.NET.Vulkan2.23.0 прежний. Пакеты локальные, публикации нет;
actual native imports подтверждаются repository runner, не package consumer.

Итоговая полная native matrix включает50aggregate cases/52commands: по20headless,
capabilities, ENG4b import, ENG4c surface, classic viewport и20Replace Editor proof
в Debug apphost/published Release.394source/76binary fingerprints; результаты
final matrix и независимого verifier фиксируются в STATUS/completion.json.
Final ValidateVerified:6commands/exit0, Completed=true; native50cases/52commands/
exit0, independent Complete Passed=true. Classic viewport17cases/27children:
Debug140/Release145pixel checks. Editor20cycles/20failure cases/53children:
Debug779/Release788pixel checks, MaxLive1/один App/window. Остатки0.
Raw: `TestResults/ENG4c/complete3/summary.json`,11TRX,
`TestResults/ENG4c/package3/summary.json`, `TestResults/ENG4c/completion.json`,
`TestResults/EngineSessions/eng4c-verified-20261020/summary.json`, per-case reports,
`surface-reference-hashes.json`. Native source snapshot не менялся во время run.
Final hygiene:15Markdown files/548local links/251anchors,55namespace files,
whitespace errors/staged entries0;7owned helpers завершены. Read-only census
testhost/EngineSessionProbe пуст; validation DLL/manifest hashes повторно совпали.

### Исправления и self-review

Первый native-focused run выявил reuse bug: PollCompletion возвращал allocation
stamp первого кадра. Исправлен current-use stamp; native loop с FrameId>1 является
его regression. Этот failed run не считается passing. Native-reuse/native-retry
затем прошли с19/20cases. Queued-resize case добавлен при review: held Produce
должен завершиться перед detach; competing Produce/Configure отклоняются.

Первый полный50case run прошёл прежнюю20case surface matrix. Следующий run с21case
surface proof остановился в old Debug Editor deadline test: parent увидел `entered`
до закрытия writer и получил sharing violation. HeldProject helper теперь пишет
PID в `.tmp`, закрывает его, затем атомарно публикует marker через Move. Parent
может читать только completed marker; timeouts/driver settings не изменены.
Failed `eng4c-final-20261020` не считается итоговым passing evidence; final snapshot
— `eng4c-verified-20261020` после этого fix.

11new test methods (6facts/5theories),78expanded cases,83Assert call sites:
47Equal/1NotEqual/19exceptions/10Boolean/4collection/2Null. Все методы содержат
behavioral assertions; assertion-free/presence-only методов0. Статический
pseudo-mutation review: no-retire reuse ловится exhaustion/out-of-order tests;
stale/false guards —42cases; descriptor aliases/profile — offer tests; allocation
на invalid/suspend — explicit native device/slot counters; changing pixels — pinned
literals и native whole-image hashes. Native proof дополнительно закрывает queued
resize, cleanup ownership/retry и old identity после restart. Mutant runs,
line/branch coverage и mutation survival percentages не собирались.

ENG4c остаётся diagnostic readback на reference RTX5070Ti/Windows x64/net10.0.
No ordinary GPU viewport/presentation/compositing/first-present, throughput/VRAM
trend/driver isolation/3D claim. Device loss/driver hang не инжектировались;
in-flight driver completion не получает hard deadline. Full Release solution,
UI own Release/packages/golden/RTE/performance/hardware acceptance не повторялись.
K3/K7/K8/VRAM/final alpha и отложенный RTE backlog открыты; Windows/drivers прежние,
K7 workaround search остановлен. Validation layer — прежняя portable extraction
ENG4b; SDK installer не запускался. Следующий шаг — ENG4d actual GPU viewport.

## ENG4b one-frame import и retirement — 2026-10-20

По запросу «давай приступим к следующему шагу» выполнен следующий согласованный
ENG4b. Одна TrueMoon.Aether assembly получила explicit Windows x64 Vulkan exporter,
GL importer, bounded same-build probe channel и App-owned GPU cleanup. Ordinary
CPU viewport не переключается; это one-frame transport proof, без3D renderer.

### Реализация и ownership

- `AetherHost.Gpu` owns resources/connections/producer work, closes admission и
  drains перед session cleanup. Startup lazy; UI context/owner dispatcher borrowed,
  живут до успешного cleanup. Нет отдельного disposable DI alias. Failed cleanup
  сохраняет entry/error; explicit Dispose retry проверен через injected resource.
- Silk.NET.Vulkan2.23.0 — существующая central version, новая прямая зависимость
  Aether. UI/Skia/Game.Logic references не добавлены. Runtime использует UseAether
  через App; low-level native реализации находятся в Gpu/Vulkan и Gpu/OpenGl.
- Dedicated optimal RGBA8 allocation≤64MiB/offset0 и2binary semaphores экспортируются
  как OPAQUE_WIN32. Editor дублирует tokens только через уже owned child process
  SafeHandle; OpenProcess/PID attach/inheritance отсутствуют. Каждая сторона закрывает
  свои NT handles; GL imports сохраняют native references. Partial import unwind
  закрывает уже полученные duplicates/GL objects.
- CurrentUserOnly pipe отдельно от control/frame v1: length1..65536, strict JSON,
  full identity/sequence/stamp/profile. Unresponsive error reader ограничен2s,
  Connect3s/command8s, native probe180s с внешним own-process guard200s.
- ImportAck → Vulkan copy/release EXTERNAL+GENERAL/signal/fence → Ready → GL wait/
  whole-image readback/signal/fence → Release → Vulkan acquire/wait/fence → Retire.
  Ready/Release требуют matching CPU-observed completion. Нет DeviceWaitIdle/glFinish;
  GL fence polls timeout0, ReadPixels — синхронная diagnostic operation.
  Unknown submitted use не разрешает destroy/reuse. Cancel/fault closes channel;
  accepted connect/commands/producer workers входят в drain.
- `GpuImportProbe` сохраняет отдельный report и7cooperative producer teardown
  snapshots. Eighth child намеренно завершается Environment.Exit(43) до Produce;
  отсутствие его cooperative marker ожидаемо, owned reap обязательно.

### Матрица требований и проверок

| Требование текущего шага | Точная проверка / evidence | Результат |
| --- | --- | --- |
| «давай приступим к следующему шагу»: ENG4b one-frame transport | `ManualTests/EngineSessionProbe/GpuImportProbe.cs`, Debug apphost/published Release |3roundtrips на configuration; whole-image bytes совпали |
| Exact identity/profile/bounds/NT tokens до native use | `GpuFrameTransferTests.Offer_InvalidIdentityShapeOrHandleProfileIsRejectedBeforeNativeUse` (24cases), `Offer_ExactSupportedBoundsAreAcceptedWithoutAlteringSourceHandleTokens` (3cases) | Passing; lower/upper bounds, stale/schema/profile/handle aliases |
| Нельзя ждать/retire по early/stale/unconfirmed completion | `Handshake_RequiresImportAndBothObservedFencesBeforeRetirement`, `Handshake_UnconfirmedOrStaleCompletionCannotAdvanceOrAuthorizeGpuWait` (9cases); native early-ready/stale-ready | Passing; phase не продвигается, valid continuation работает |
| Channels/alpha/origin и native pixels | `ReferencePattern_HasIndependentChannelAlphaAndOrientationMarkers`;3native sizes, independent PowerShell SHA256 | Passing; pinned literal markers + whole-image comparison |
| App ownership, shared cleanup и retry | `AppGpuOwner_StartupIsLazyAndCleanupFailuresRetainOwnershipForExplicitRetry`, `GpuOwner_ConcurrentDisposeSharesCompletionAndDrainsAcceptedCleanup`; fresh consumers | Passing; reverse order, retained failed entry/error, closed admission, shared pending completion |
| Bounded protocol/no native work on invalid input | `GpuProbePipeTests.InvalidProtocolInput_CannotReachNativeAllocationAndWorkerIsDrained` (7cases), `MaximumAllowedPayload_ReachesPhaseValidationWithoutNativeWork` | Passing; malformed length/JSON/identity/sequence/phase;65536accepted/65537rejected |
| Unresponsive peer и accepted worker drain | `ErrorReply_UnresponsiveReaderCannotKeepProducerWorkerAliveIndefinitely`, `AppClose_CancelsAndDrainsAcceptedProducerWorkBeforeNativeAllocation` | Passing; original failure retained; App cancellation drains worker |
| Partial import/native failure и session cleanup | bad descriptor/stale-offer/duplicate-handle-failure/missing-gl-procedure/driver-storage-failure; cancel/timeout/crash-before-ready; native-close-live-imports |14cases/run,8owned children reaped, NT/GPU/UI resources0 |
| Validation + package delivery |7producer core/sync teardown reports/run;9fresh nupkg/4Cobalt+DI consumers | Errors0/warnings0; Silk.NET.Vulkan2.23.0 в nuspec/assets; consumer DLL hash == package DLL hash |

### Текущие запуски

Все команды ниже выполнены2026-10-20 на итоговых исходниках. Test platform:
xUnit2.9.3/VSTest, SDK10.0.402/runtime10.0.13. Baseline commit
`dc64b63d0e8da9a05396a74482af2931b30af8db`; прежние ENG3/ENG4a изменения сохранены,
staged entries0, commit/push не выполнялись.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~GpuFrameTransferTests|FullyQualifiedName~GpuProbePipeTests' --logger 'trx;LogFileName=transfer-pipe-final.trx' --results-directory TestResults/ENG4b/focused --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4b/ValidateComplete.ps1
pwsh -NoProfile -File TestResults/ENG4b/Complete.ps1
```

ValidateComplete выполняет nonincremental parallel full Debug build, Aether
Debug/Release, full Debug solution, fresh package consumer и native runner:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4b/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4b/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4b/complete/solution-Debug --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4b/ValidatePackage.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4b-complete-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

- Focused50/50. Aether366/366 Debug и Release. Full Debug1769/1769,9TRX:
  Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting79/
  Aether366; failed0/skipped0. Build0errors/1499warnings; warning backlog сохранён.
- Native48cases/50commands/exit0: прежние20headless +capabilities +GPU import
  +classic viewport +Editor20Replace на каждой configuration.388source/76binary
  hashes повторно сверены; source изменений во время run нет. Прежние selected
  DLL/checkpoint/replacement/20cycles/failure proofs также проверены отдельно.
- Каждый import report:14cases/8children,3roundtrips/923904pixels, handles/resources/
  UI sessions0, authoring preserved, CPU default.7producer validation reports:
  core+synchronization errors0/warnings0, включая teardown. Frame16×16 SHA256
  `CEFFF9288EA786F7337FF4BE76E80CB9082293D6D99164C93FED95C4156ECE58`;
  64×32 `E205F8B81E5DF10DCF6778A727FDC48E36899583AA087E24A43F2A3437E48AD7`;
  1280×720 `52150160BC773ABA67B7AE1FD80682763E0A9C75A853A5364422E2A3712A7655`.
- Fresh packages: version `0.2.2.7-alpha.aetherapp.20261020122712725`,9nupkg/
  15commands/4consumers, Cobalt/DI × Debug/published Release. Isolated restore
  cache/Directory.Build hierarchy; ProjectReference нет, все assets libraries —
  packages. NuSpec Vulkan2.23.0, App startup GPU allocation0, injected resource
  Dispose1, factory/lease Dispose1, late session Start rejected, остатки0.
  Пакеты локальные, публикации нет; native imports проверяются repository runner,
  этот package consumer отдельно проверяет delivery/lazy lifecycle.
- Raw evidence: `TestResults/ENG4b/complete/summary.json`,11TRX,
  `TestResults/ENG4b/package/summary.json`, `TestResults/ENG4b/completion.json`,
  `TestResults/EngineSessions/eng4b-complete-20261020/summary.json` и per-case reports.
  Independent verifier повторяет hashes/completion stamps/cleanup/old proofs,
  проверяет package DLL и assets, namespaces и doc links. Scratch helpers/TRX/SDK
  ignored, не входят в package или commit.
- Финальная hygiene проверка:15Markdown files/547local links/248anchors,
  50namespace files, whitespace/staged errors0;6owned helpers завершились.
  Read-only census testhost/EngineSessionProbe процессов пуст.

Первый validation attempt завершился ErrorLayerNotPresent до allocation;
`native-validation.exit.txt=1` не считается passing. Официальный
[SDK1.4.363.0](https://sdk.lunarg.com/sdk/download/1.4.363.0/windows/vulkansdk-windows-X64-1.4.363.0.exe)
SHA256 `94A82D378F7A5E3E54C9DB7D2FB7016AF136E14AC0A18DBF0F2F67A36352D141`
скачан в TestResults, validation DLL/JSON извлечены7-Zip без запуска installer.
VK_LAYER_PATH/TRUEMOON_GPU_VALIDATION задаются только собственному test process
tree; runner fingerprints обоих файлов. Drivers/Windows settings не менялись.
Preliminary native-focused/native-faults были без validation; final matrix выше —
с core/sync validation. Отдельный диагностический test run сначала завис из-за
непрочитанного error reply в тесте, был остановлен hang guard; тест исправлен,
final focused/full runs passing. Последняя правка ограничивает production error
reply2s и проверяется отдельным non-reading peer case.

### Self-review и оставшиеся границы

11новых test methods (7facts/4theories),50expanded cases,65Assert call sites:
36Equal/15exception/9Boolean/3Null/2Same. Все11методов имеют behavioral assertions;
assertion-free/presence-only methods0. Native custom assertions дополнительно
проверяют pixels, stamps, cleanup/validation/owned processes. Reference literal
markers независимы от generator; whole-image hashes вычислены отдельно.

Pseudo-mutation review статический, выполненных mutant runs нет: удаление
FenceCompleted/stamp/phase guard ловится false/stale/null cases; ослабление bounds/
profile/duplicates — descriptor cases; длина/identity/sequence — pipe tests;
пропуск resource cleanup/error/retry — trace/count/identity tests; смена channels/
alpha/origin — pinned markers/whole-image native hashes. Отсутствие native proof
для device loss/driver hang/in-flight cancellation/slot reuse явно сохраняется.
Line/branch coverage и mutation-survival percentages не собирались.

ENG4b подтверждает bytes и one-frame retirement; actual window presentation/
compositing/color conversion, reusable slots/resize/restart, полная in-flight
crash/recovery matrix — ENG4c/d. Native maximum checked image1280×720; descriptor
bounds1920×1080 проверены unit tests, ordinary CPU viewport имеет свой scope.
Driver isolation, hard real-time deadline, долгосрочный VRAM/resource trend,
performance/3D/shared viewport readiness не объявляются. Full Release solution,
UI Release/golden/RTE/UI packages/hardware acceptance этим шагом не повторялись.
K3/K7/K8/VRAM/final alpha и отложенный RTE backlog прежние; K7 workaround search
остаётся остановленным. Следующий code шаг — ENG4c slots/surface lifecycle.

## ENG4a GPU capabilities и transport decision — 2026-10-20

По запросу «давай приступим к следующему шагу» выполнен ENG4a. Aether/Gpu содержит
immutable capabilities snapshots и pure GpuTransportProbe без UI/native dependencies.
Full SessionIdentity/schema/profile предшествуют adapter matching. Device/driver
UUID — native bytes/hex без Guid byte-order преобразований. Structured Issues
оставляют CPU path; Eligible разрешает только следующий явный import-probe.
DedicatedOnly сохраняется, Vulkan Importable не требуется exporter (importer — GL).
Profile и будущий completion/retirement handshake — [в единственном плане](PLAN.md#eng4a-capabilities-и-transport-decision--2026-10-20).
Stateless utility не требует фиктивного UseX; App integration/lifecycle прежние.
Control/frame wire v1 не меняется. Collector использует существующие graphics
bindings только в example, без новых package versions или Aether dependencies.

Native --gpu-capabilities: Alloy/OpenGL window и App-owned child. Runtime создаёт/
уничтожает Vulkan1.1 instance, перечисляет physical devices/UUID/LUID/extensions/
graphics queues и exact RGBA8_UNORM/2D/optimal/usage23 external image +binary
OPAQUE_WIN32 semaphore properties. Logical device/image/allocation/export не
создаются. GL queries на current UI owner: UUIDs/extensions/procedures/RGBA8/
optimal tiling/fence polling; новые imports/textures/semaphores/fences collector
не создаёт. Обычные Skia UI resources сохраняют прежний lifecycle. Child report
≤64KiB — correlated local prototype evidence, не production capability exchange.
Whole probe45s/runner60s; managed token не гарантирует прерывания native driver call.

### Actual reference configuration и решение

Debug apphost и published Release выбрали index0: NVIDIA GeForce RTX5070Ti,
Vulkan api4211039 (1.4.351), GL3.3.0 NVIDIA617.42. Device UUID
`D69A207DD3EC6D8F5F325359A59FA086`, driver UUID
`8B2ED3A7D66651B8AD47BB85416B871B` совпали в обоих API. Vulkan также перечислил
AMD Radeon(TM) Graphics; он не используется actual GL context и не выбирался.
Image query Success/usage23, memory/semaphore export/opaque compatible bits,
single sample/extent/resource size и все GL requirements доступны, tiling0x9584,
DedicatedOnly=false. **Разрешён ENG4b Vulkan→OpenGL import-probe на этой configuration.**
CPU selected/ImportTested=false. Actual import/pixels/completion/shared GPU readiness,
driver isolation и performance не доказаны queries. Drivers/Windows settings прежние,
K7 workaround search не возобновлялся.

### Фактически выполненные проверки

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m`:
  0errors/1499warnings, новых warnings в GPU files нет.
- `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4a/complete/tests-Debug --verbosity quiet`:
  316/316, failed0/skipped0; аналогичный Release с tests-Release:316/316.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4a/complete/solution-Debug --verbosity quiet`:
  1719/1719,9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/
  generator114/Mithril94/UI613/Hosting79/Aether316.
- `pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4a-complete-20261020 -IncludeViewport -IncludeGpuCapabilities`:
  46/46native cases,48commands/exit0,377source/76binary hashes: по20headless,
  classic viewport, Editor20Replace proof и gpu-capabilities/configuration.
  Новый switch opt-in, прежний runner scope без него сохраняется.
- Оба GPU reports: Eligible=true/index0/issues0/CPU selected/ImportTested=false,
  instance destroyed/1child reaped/released, mappings/UI sessions0/authoring preserved.
  Старые ENG1–ENG3 regressions прошли; individual pixel/child counts сохранены в
  per-case reports и completion.json, без переноса результатов на UI acceptance.

Evidence: `TestResults/ENG4a/complete/summary.json`/logs/11TRX;
`TestResults/EngineSessions/eng4a-complete-20261020/summary.json`, Debug/Release
`gpu-report.json`/`gpu-producer-1.json` с raw tuples/UUIDs и cleanup.
Итоговый read-only fingerprint/report/namespace/doc guard —
`TestResults/ENG4a/completion.json`. Предварительные focused55/60tests/native
сохраняют свои input sets и не заменяют final run. Первый compile исправил
Silk EnumerateInstanceVersion pointer/ref вместо out.

### Матрица ENG4a requirement / evidence

Все строки относятся к запросу «давай приступим к следующему шагу» и принятому ENG4a.
Префикс unit tests: `TrueMoon.Aether.Tests.Gpu.GpuTransportProbeTests.`

| Requirement | Evidence: exact test / run |
| --- | --- |
| Adapter/driver/explicit eligibility/dedicated flag | Eligible_CorrelatesActualDeviceAndDriverPreservesDedicatedAndDoesNotRequireVulkanImport |
| Identity/schema/profile/platform/query rejection | InvalidPeerEvidence_RejectsBeforeMatchingAndKeepsExplicitReason |
| Exact Vulkan image/memory/semaphore/queue/API requirements | NativeRequirementMissing_CannotBecomeEligible |
| Each mandatory GL feature | EachOpenGlRequirement_IsRequiredEvenWhenUuidMatches |
| Extent16..1920×16..1080/minimum bytes | Dimensions_UseExactViewportLimitsAndMinimumResourceSize |
| Immutable lists/all independent GL blockers | Snapshots_DoNotFollowCallerArrayMutationsAndReportAllGlBlockers |
| Incomplete evidence/malformed GL UUID | IncompleteNativeEvidence_DoesNotBecomeEligibleOrThrow; InvalidConsumerUuid_IsReportedBeforeAdapterMatching |
| Last allowed device count/select last adapter | LastAllowedProducerCountAndExactMaximumViewport_CanStillChooseTheLastAdapter |
| Actual App/owned child/current GL/no import/cleanup | Debug/Release-gpu-capabilities: matched UUID/profile, Eligible/CPU selected/ImportTested=false/1reaped child each |
| ENG1–ENG3/build/tests regression | Exact successful commands above:316Aether Debug/Release,1719full Debug,46native cases |

Pre-completion test-gap-analysis/assertion-quality:9methods/62cases,43Assert call
sites; zero assertion-free/trivial-only methods. Two Importable=false fixture checks
are additional to independent result/index/dedicated/issues assertions. Meaningful
reason/index/flag/extent/size/snapshot outcomes и both sides/boundaries защищены;
static verdict Strong, без mutation/coverage-percentage run. Minor argument/null
envelope guards не расширялись. IDE findTests недоступен (Tool not found);
существующий xUnit project/conventions использован без reconstruction workspace.

ENG4a завершён, ENG4 в целом открыт. Далее ENG4b: enabled logical device extensions,
dedicated RGBA8 allocation/export/owned peer import, binary semaphores/CPU-visible
completion, actual CPU-reference pixels и failed-import/cancel/crash/timeout/
handle retirement. Fresh GPU API package consumers/full Release solution, 3D/
long resource/performance и UI K3/K7/K8/VRAM/final alpha не подтверждены.

Final read-only guard:15Markdown documents/544local links/245anchors,
40namespace files,377source/76binary hashes; actual GPU query flags/profile/UUID,
native artifacts/reports и TRX повторно сверены. Whitespace/staged guard прошёл,
staged0; оба owned helpers завершены, baseline dc64b63 прежний. Existing uncommitted
ENG3 changes сохранены, commit/push не выполнялись.

## ENG3d команды Editor и 20Replace proof — 2026-10-20

Реализован следующий согласованный подэтап ENG3. App-owned `AetherHost.Editor`
сериализует Build/Select/Start/Replace/Play/Pause/Step без зависимости от UI.
SetContext snapshots build/dependencies/scene/selection, меняет version, отменяет
pending command и снимает candidate. Completion проверяет также originating runtime
identity/Stop version. Build не применяет artifact автоматически, Select явный.
Cancel сохраняет recovery policy; Stop fences recovery до cancellation, drains
commands/compiler/frames, сохраняет исходную ошибку и допускает cleanup retry.
Close permanently closes admission, host drains Editor до disposal зависимостей.
Новых package dependencies и изменений Core/Enerit/Mithril/UI production APIs нет.

Viewport использует публичные команды, показывает operation/source/project/active/
candidate/errors; Refresh source меняет input revision без изменения authoring.
`--viewport-project` выбирает trusted local Game.Logic project. Дополнительный
`--editor-proof` использует одно App/OpenGL window и actual A/B DLL с разными body
color и camera step. На каждый cycle сверяются loaded path/SHA256, runtime restored
state до mode/frames, first GPU tick, полный checkpoint и следующий Step. GPU
readback проверяет body color вместе с corners/frame/tick, включая bilinear sampling.

### Фактически выполненные проверки

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m`:
  0errors/1499warnings; warnings не подавлялись, новых Editor warnings нет.
- `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3d/complete/tests-Debug --verbosity quiet`:
  254/254, failed0/skipped0. Аналогичный Release с tests-Release:254/254.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3d/complete/solution-Debug --verbosity quiet`:
  1657/1657,9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/
  generator114/Mithril94/UI613/Hosting79/Aether254.
- `pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3d-complete-20261020 -IncludeViewport`:
  44/44native cases,46commands/exit0,372source/76binary hashes.20headless +classic
  viewport +editor-proof в каждой Debug apphost/published Release configuration.
- Каждый editor-proof:20A/B Replace при одном App/root/window/session, оба modes,
  20failure/interrupt cases,775actual GL pixel checks,53children/maxlive1/allreaped/
  released. Original/recovery exceptions, selected hashes и input hashes сохранены;
  authoring unchanged, Game.Logic в Editor не загружен. Mappings/UI sessions0.
- Classic viewport:17cases/27children/20StartStop в каждой configuration; actual
  GL body/counters/corners, compiler close и cleanup проверены. Default45s Replace
  policy повторена отдельным native replacement case; proof снижает limits до
  Build15s/Replace12s,forward4s,cleanup3s; общий proof≤300s.

Failure matrix: compiler error/cancel/deadline, context change, completed build для
другой session без caller cancellation, Stop build, missing/tampered artifact,
confirmed capture rejection/unknown timeout, candidate bad Hello/restore/first-frame,
double restore failure, Cancel/context/Stop replace, injected lease Stop failure с
реальным owned child и explicit retry, native close при Replace и compiler job.
Injected failure не выдаётся за воспроизведение OS kill failure.

Evidence: `TestResults/ENG3d/complete/summary.json`, individual logs/11TRX;
`TestResults/EngineSessions/eng3d-complete-20261020/summary.json`, per-case reports,
`editor-report.json`, `restored-*.json`, `loaded-*.json`, artifacts/manifests и PNG.
Итоговая повторная сверка — `TestResults/ENG3d/completion.json`.
Предварительный focused native run отдельно:20cycles/20failures/772pixel checks;
он не заменяет final matrix. Первые focused19tests имели2ошибки test expectations:
повторная компиляция не обязана иметь тот же DLL hash, Stop может завершиться до
повторного вызова. Проверки исправлены; separate held-reap test фиксирует shared
completion именно при concurrent Stop. Исходные логи сохранены в ENG3d/focused.

### Матрица ENG3d requirement / evidence

Все строки относятся к запросу «давай следующий шаг» и принятому ENG3d плану.
Префикс unit tests: `TrueMoon.Aether.Tests.Editing.GameEditorTests.`

| Requirement | Evidence: exact test / run |
| --- | --- |
| Build выбирает candidate без изменения world | Build_PublishesCandidateWithoutLaunchingOrChangingRunningWorld; CompilerError_PreservesActiveWorldAndRetainsDiagnosticsForEditor |
| Immutable context и explicit selection | Context_SnapshotsDependenciesAndInvalidatesSelectionOnEveryChange; Select_MismatchDoesNotPauseOrStopOldRuntime |
| Поздняя сборка/selection не применяются к другой session | CompletedBuild_ForAnotherSessionIsRejectedEvenWithoutCallerCancellation; RawSessionChange_HidesCandidateAndRejectsReplaceBeforeApplyingIt; native build-stale-session |
| State/mode и original/recovery errors | Replace_UsesExplicitSelectionAndPreservesStateAndMode; ReplaceFailure_RetainsOriginalAndRecoveryErrorsUntilNextCommand |
| Cancel/context/Stop/close compiler и replacement | HeldBuild_InterruptDrainsOwnedCompilerAndNeverSelectsLateArtifact; HeldReplace_CancelRecoversButStopAndCloseNeverLaunchRollback; native20failure cases |
| First actual presentation и owned cleanup/retry | Start_SelectedBuildWaitsForPresentationThenClearsCandidate; ConcurrentStop_SharesCompletionAndRejectsCommandsUntilOwnedReapCompletes; StopFailure_RetainsOwnedLeaseAndAllowsExplicitCleanupRetry |
| One App/window20Replace, selected DLL behavior/pixels/checkpoint/authoring | Debug-editor-proof/editor-report.json; Release-editor-proof/editor-report.json:20cycles each,775GPU checks each, full runtime restored state and subsequent Step |
| ENG1/ENG2 regression |44native cases, classic17viewport cases/configuration, full Debug1657; exact successful commands above |

Pre-completion test-gap-analysis/assertion-quality:13methods/23cases,109Assert
calls; zero assertion-free/trivial-only methods, awaited async exception checks.
Equality/identity, exceptions/type, deep checkpoint, state/side effects, collections,
null/negative outcomes проверены вместе. Критические stale/Stop/close/recovery/reap
branches защищены paired state/child-count/error assertions; static verdict Strong.
Default fixed-build Start delegates прежнему FrameSession path; отдельные minor
null/invalid-context variants не добавлялись. Это static outcome review, не mutation
или coverage-percentage run. IDE findTests unavailable (Tool not found); scoped
pairing analyzer выполнен один раз,22sources/0tests из-за sibling test project,
после чего использован существующий explicit test project/fixtures.

ENG3a–d завершены в bounded Windows x64/net10.0 CPU transport scope. Следующий
конкретный этап — ENG4a capabilities/transport decision до GPU import implementation.
Fresh package consumers нового Editor API, full Release solution, 3D/shared GPU,
long resource/performance и UI K3/K7/K8/VRAM/final alpha этим не подтверждаются.
Drivers/Windows settings не менялись, K7 workaround search не возобновлялся.

Финальный read-only guard:14Markdown documents,490local links/219anchors,
37namespace files,372source/76binary fingerprints; whitespace/staged guard прошёл,
staged0. Оба owned helpers завершены. Existing uncommitted ENG3a–c сохранены,
commit/push в этом шаге не выполнялись.

## ENG3c serialized Replace и rollback — 2026-10-20

Реализован следующий согласованный подэтап. App-owned AetherHost.Replacement
связывает Pause/capture → old stop/reap → selected candidate start/restore/
first-present → прежний mode, с однократным old-build rollback. Подробная политика,
численные бюджеты и public API ведутся в [плане](PLAN.md#eng3c-принятые-контракты--2026-10-20)
и [README](../../TrueMoon.Aether/README.md#замена-игровой-сборки--eng3c).

### Реализация и ownership

- GameReplacementRequest фиксирует candidate/expected identity/scene/current source
  revision. До Pause проверяются stale selection, scene ID/revision и оба artifacts;
  managed files pinned до конца transaction. Source revision независима от scene.
- Общая explicit reservation у Supervisor/Frames запрещает конкурирующие commands
  и Replace; authority передаётся typed internal lease, без AsyncLocal. Предыдущая
  frame operation завершается до admission; control gate drains начатые commands.
  Stop/close fences pending reservation и accepted work, не допускает late launch.
- Default total45s/forward18s/cleanup10s; phase caps startup15s/mode2s также применяются
  при увеличенных low-level session deadlines. Global deadline ограничивает recovery.
  Options snapshots/validation допускают уменьшение; forward+cleanup<total.
- Confirmed capture rejection сохраняет old child/frame и возвращает прежний mode.
  Unknown capture outcome fault/cleanup. После old Stop caller cancellation допускает
  one recovery без caller token; explicit Stop/App close отменяют и recovery.
- Candidate error всегда остаётся InnerException GameReplacementException, Phase
  и rollback flags наблюдаемы; RecoveryError отдельна. Неудачный rollback не повторяется.
  Fixed legacy и selected old builds поддержаны. Headless ждёт control readiness;
  visible surface — first-present, suspended case отвергается до Pause.
- CleanupAsync удерживает одну owned cleanup task и lease после deadline. Late task
  completion наблюдаем, не снимает CleanupFailed и не разрешает launch; явный Stop
  retry ждёт ту же task, а после fault допускает новую cleanup attempt. Frames
  сохраняют mappings при uncertain reap. App shutdown drains Replace до disposal,
  compiler cleanup выполняется независимо. Общие Core/Enerit/Mithril/UI APIs прежние.

### Матрица требований и новых тестов

GameReplacementTests:15methods/47cases. Real RuntimeProtocol/ProbeGame и frame
producer; fake только lease lifecycle/transport failure seam. Compiler fixture
создаёт actual validated artifacts. Все случаи входят в итоговые231Aether tests.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Paused/Playing × headless/viewport, полный snapshot, first-present gate, selected build/fresh identity и один child | Replace_PreservesCheckpointAndPreviousModeOnFreshSelectedGeneration |
| Hello/restore/first-frame/mode candidate failure восстанавливает fixed/selected old build/state/mode/кадр один раз | CandidateFailure_RestoresOldBuildStatePresentationAndModeOnce |
| Любая из4rollback failures сохраняет точные original/recovery errors, без второго rollback, cleanup | RollbackFailure_RetainsOriginalCandidateErrorWithoutSecondAttempt |
| Confirmed capture rejection сохраняет old child/frame/tick и прежний Paused/Playing mode | ConfirmedCaptureRejection_PreservesOldProcessFrameAndPreviousMode |
| Stale identity/source/scene, corrupt artifact, suspended surface отвергнуты до Pause/Stop | StaleOrInvalidSelection_IsRejectedBeforePauseOrOldStop |
| Capture timeout/disconnect fault и drains, без rollback | UnknownCaptureOutcome_FaultsAndDrainsWithoutRollback |
| Raw Step/mode/Start/Resize/capture и second Replace не вмешиваются в reservation | Reservation_RejectsCompetingCommandsAndSecondReplaceWithoutDisturbingOwner |
| Stop during capture/old-stop/Hello/restore/presentation/rollback fences late launches и drains ownership | StopDuringTransaction_FencesCandidateAndRollbackAndDrainsAllOwnership |
| Caller cancellation до destructive Stop сохраняет old, после reap восстанавливает old без caller token | CallerCancellation_PreservesOldBeforeStopOrRollsBackAfterReap |
| Отдельный cleanup deadline сохраняет одну task/lease, late completion не снимает failure, explicit retry | UnconfirmedCleanup_RetainsOneTaskAndBlocksLaunchUntilExplicitRetry |
| Уже начатый Resize drains до admission, queued Replace отменяется Stop | ExistingResize_DrainsBeforeReservationAndQueuedReplaceIsFencedByStop |
| Raw Supervisor.Stop также прерывает transaction и его frame ownership | RawSupervisorStop_InterruptsReplacementAndDrainsItsFrames |
| Failed reap запрещает rollback/launch; явный retry освобождает retained lease | FailedReap_BlocksRollbackAndCanBeRetriedWithoutLosingOriginalError |
| App close drains accepted Replace и fences restart, options snapshot на Cobalt/DI | AppClose_CancelsAcceptedReplaceBeforeDisposalAndSnapshotsBudgets |
| Positive/max/overflow/phase bounds и reserve, до reservation/process | InvalidBudgets_AreRejectedBeforeAnyReservationOrProcess |
| Actual A→B behavior/CPU pixels/state, rejected candidate recovery, held restore Stop, double failure и max live1 | replacement (Debug apphost, published Release) |
| Actual OpenGL first-present после transaction restore | viewport: replace-restored-before-first-present (обе конфигурации) |

### Финальные команды и результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest.
Итоговый run: TestResults/ENG3c/complete/summary.json.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3c/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3c/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3c/complete/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3c-complete-20261020 -IncludeViewport
```

Final summary Completed=true,5commands exit0. Nonincremental Debug build:
0errors/1499warnings, без warnings в новых replacement contract/test files.
Aether231/231 отдельно Debug/Release, failed0/skipped0. Full Debug1634/1634,
9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/
Hosting79/Aether231, failed0/skipped0. Focused reviewed47/47 Debug.

Native summary Completed=true,44commands exit0,42/42cases:20headless +1viewport
на конфигурацию Debug apphost/published framework-dependent Release без RID.
368source/76binary fingerprints. Каждый replacement case:4actual artifacts,
8game children/max live1, A→B loaded path/hash/CPU color, full checkpoint tick5,
successful old-build rollback/Playing, Stop during held game restore и сохранение
двух причин при failed rollback. Все leases reaped/released, mappings0, authoring/
Editor CLR сохранены. Каждый viewport:17cases/27game children/20StartStop,
actual GL pixels (Debug137/Release135checks), replace-restored-before-first-present
и прежние checkpoint/held compiler close cleanup, sessions/mappings0/childrenreleased.
Read-only TestResults/ENG3c/completion.json проверяет исходные/binary/report и все
selected manifest/DLL/payload hashes, TRX counts, namespace/doc links/whitespace,
staged-state и завершение owned helpers. Это единственный final input set.

### Промежуточные failures и review

Первая narrow build: CS0136 из-за совпадения local/catch variable name; исправлено.
Первый focused run42passed/1failed: fixture продолжал инжектировать candidate
failure в новый явный Start после cleanup retry; injection сброшена до нового
Start. Добавлены direct pre-existing Resize и raw Stop cases:45/45passed.
Initial native replacement прошёл Debug/published Release. Первый полный run:
Aether229/229 Debug/Release, full Debug1632/9TRX, native42 прошли до финального review.

Review уточнил9invalid budget partitions и exact recovery error assertions, cleanup
wait oracle короче total deadline. При первой повторной Debug проверке230/231:
точная timeout assertion обнаружила лишний повторный drain уже очищенного frame
канала после rollback deadline; cancellation добавлялась в Aggregate поверх исходной
ошибки. Production теперь не повторяет drain, если все resources уже освобождены.
После исправления выполнен новый узкий и полный run; failed attempt сохранён в
TestResults/ENG3c/verified, final evidence — только complete paths выше.

code-testing-agent workflow выполнен inline, специализированного generator здесь
нет. Research/plan/status вне Git в session temp (ignored path в
TestResults/ENG3c/state-directory.txt). IDE findTests недоступен: Tool not found.
Roslyn pairing один раз scoped Aether:19source/0tests до новых файлов, так как
test project соседний; exit0, это pairing, не line/branch coverage.
assertion-quality/test-gap-analysis использованы для15новых methods:145syntactic
Assert calls, нулевых/trivial-only нет. Exact state/value/sequence/mode/error identity,
side effects, отрицательные guards, task/failure retention и first-present проверены.
Review закрыл точность recovery errors и независимость cleanup cap; empirical
mutations/coverage metrics не запускались. Checklist привязан к exact names выше.

### Оставшийся scope

ENG3d: Editor build/replace commands и20Replace cycles при одном живом App/window,
полный согласованный failure/pixel proof. Текущие native smoke не заменяют этот
acceptance. Source revision и cancellation при смене editor selection/document
передаёт caller; подписки Aether на конкретную editor model нет. Бюджет ограничивает
control/startup/recovery waits, не гарантирует успешный OS reap: retained cleanup
требует явного retry. Сохраняются синхронная bounded artifact validation и прежний
bounded borrowed-frame drain ENG2; hard real-time завершение arbitrary caller/game
callbacks не заявляется.
Fresh package consumers нового API/full Release solution/performance/VRAM/3D/shared
GPU и hardware K3/K7/K8/final UI alpha не повторялись. Driver/Windows settings
прежние. Commit/push в этом шаге не выполнялись; ENG3a/b working tree сохранён.

## ENG3b checkpoint и startup restore — 2026-10-20

Реализован следующий согласованный подэтап после ENG3a. Optional
ICheckpointGameSession, immutable GameCheckpoint/State/CameraTransform и canonical
TMCK v1 codec находятся в Aether.Contracts. Схема/offsets/лимиты и ownership
зафиксированы в [плане](PLAN.md#eng3b-принятые-контракты--2026-10-20),
[публичный пример](../../TrueMoon.Aether/README.md#checkpoint-и-восстановление--eng3b).
Новых проектов/packages/dependencies и общих Core/Enerit/Mithril/UI API нет.

### Поведение и границы

Capture только в Paused под runtime gate, без продвижения tick или изменения мира.
Версия1 сохраняет host/source build/scene/revision и точный UInt64 tick, finite
camera position/Euler radians, interaction bool. Detached bytes содержат strict
UTF8 и SHA256 integrity. ValidateFor проверяет host/scene/revision до Stop и нового
generation; source build может отличаться от совместимого target build.

Control добавляет CaptureCheckpoint/RestoreCheckpoint и явный outcome. Только
валидированный sequenced Unsupported/CaptureRejected сохраняет paused child,
heartbeat/sequence и Resume. Неожиданная callback/transport error, timeout,
mutation или malformed response дают Faulted и cleanup. Даже исключение типа
CheckpointRejectedException, пришедшее до ACK, не обходит эту policy.

Fresh startup с checkpoint передаёт RequireRestore в child: после LoadScene
применяется snapshot и проверяется полное recaptured state, затем разрешены
SetMode/Step/surface/frames. Restore rejection очищает candidate. FrameSession
startup ждёт presentation восстановленного кадра в прежнем общем15s budget.
Без checkpoint Start явно reset из scene; live restore не поддерживается.
ProbeGame валидирует данные до non-throwing primitive-field commit.

### Матрица требований и тестов

Новые CheckpointCodecTests/CheckpointLifecycleTests:14methods/45cases; вместе
с прежними139 дают184. Fixture использует реальный Protocol/ProbeGame/FrameProducer,
fake lease — только process/transport seam; native cases отдельно проверяют IPC.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Точные UInt64 ticks0/1/MaxValue, canonical magic/version/tick offsets, floats, detached state | Codec_RoundTripsUnsignedTicksExactFloatsAndDetachedState |
| Scene1/128 characters, strict Unicode, нулевые revision/tick и false flag | Codec_AcceptsSceneCharacterBoundariesAndStrictUnicode |
| Version/magic/integrity/length/UTF8/NaN/Infinity/flag/revision и64KiB rejection, включая rehashed invalid fields | Codec_RejectsMalformedOrRehashedInvalidPayload |
| Invalid envelope и host/scene/revision preflight | Envelope_RejectsInvalidIdentitySceneAndCameraBeforeRestore |
| Observational capture и invalid restore без partial commit в игре | ProbeGame_RestoreValidatesBeforeAtomicCommitAndCaptureDoesNotTick |
| Generated request/reply bytes, outcome, exact unsigned tick bits и bounded wire | GeneratedWire_RoundTripsCheckpointBytesOutcomeAndUnsignedTickBits |
| Capture Paused/tick boundary, Playing rejected локально без remote command | Capture_IsObservationalPausedAndAcceptedSequenceRemainsUsable |
| Unsupported/expected rejection/invalid state: old child/heartbeat/sequence/Resume сохранены | AcknowledgedRejection_PreservesPausedGameHeartbeatSequenceAndResume |
| Timeout/disconnect/unacknowledged typed error, malformed ACK/payload/tick и game mutation: Faulted/reap | UnconfirmedOrMalformedCaptureOutcome_FaultsAndReapsInsteadOfPretendingRecovery |
| Pending restore запрещает mode/Step/surface/capture, не сдвигает sequence; после restore commands/frames доступны | RuntimePendingRestore_RejectsModeStepAndSurfaceWithoutAdvancingSequence |
| Complete state до surface/first frame, first-present gate и cross-build provenance | StartupRestore_GatesSurfaceAndFirstFrameUntilCompleteStateIsApplied |
| Unsupported/rejected/no-op/partial restore, corrupt request и bad ACK не дают surface, candidate очищен | FailedStartupRestore_NeverConfiguresSurfaceAndReapsCandidate |
| Host/scene/revision mismatch не меняет old session или следующий generation | IncompatibleCheckpoint_IsRejectedBeforeOldSessionOrGenerationChanges |
| Stop отменяет held restore без display/mapping и drains worker/child | StopDuringRestore_CancelsPendingStartupWithoutPublishingOrLeakingFrames |
| Actual IPC/selected DLL/full state/CPU pixels, explicit reset, rejection/Resume, timeout и Stop during restore | checkpoint (Debug apphost, published Release) |
| Восстановленный tick/state после actual OpenGL first-present | viewport: checkpoint-restored-before-first-present (обе конфигурации) |

### Финальные команды и результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest.
Итоговый run сохраняется в TestResults/ENG3b/verified; промежуточные результаты
не подменяют его. Команды:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3b/verified/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3b/verified/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3b/verified/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3b-verified-20261020 -IncludeViewport
```

Nonincremental Debug build0errors/1499warnings; новые checkpoint classes/tests
без собственных warnings. Aether184/184 отдельно Debug/Release, failed0/skipped0.
Full Debug1587/1587,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/Aether184, failed0/skipped0.

Final summary Completed=true,5commands exit0. Native summary Completed=true,
42commands exit0,40/40cases:19headless +1viewport в каждой Debug apphost/published
framework-dependent Release без RID.362source/76binary fingerprints сохранены.
Каждый checkpoint case:6game children, saved/first-frame tick3, full recaptured
camera/interaction equality,156byte payload с сохранённым checkpoint.bin/SHA256,
selected artifact path/hash/manifest и source/target build различаются.
Capture rejection позволил Resume; timeout faulted, rejected/held restore очищены.
Authoring state сохранён; Game.Logic в Editor не загружен, все leases reaped/released.
Каждый viewport:16cases/26game children/20StartStop, actual GL pixels (Debug86,
Release84checks), checkpoint-restored-before-first-present и compiler close/reap;
sessions/mappings0/childrenreleased. Read-only completion verifier сохраняет
TestResults/ENG3b/completion.json: source/binary/report/payload/artifact hashes,
TRX counts, namespace/doc links/whitespace/staged-state и owned-helper exit checks.

### Review, промежуточные запуски и ограничения

Focused checkpoint Debug сначала43passed, после review45passed. Первые полные
Aether182/182 Debug/Release, full Debug1585 и native40 прошли до двух добавленных
cases; финальные counts выше относятся к повторному запуску обновлённого input set.
Native initial checkpoint отдельно прошёл в Debug и published Release. Test/code
failures не было; review выявил no-direct-test outcomes pending command gates и
partial restore — добавлены прямой runtime test и partial variant. Повторный full
run обусловлен этими изменениями и XML docs новых API.

code-testing-agent workflow выполнен inline, специализированный generator недоступен.
Research/plan/status сохранены вне Git в session temp (путь в ignored
TestResults/ENG3b/state-directory.txt). IDE findTests недоступен: Tool not found.
Roslyn pairing analyzer запущен один раз, exit0, scoped Aether:17source/0tests,
так как существующий test project находится рядом; это pairing, не coverage.
assertion-quality и test-gap-analysis применены к14новым methods:125syntactic
Assert calls, нулевых/trivial-only tests нет; exact format/value, rich-state,
exceptions, отрицательные assertions, side effects и ordering проверены.
Roundtrip assertions дополнены независимыми format/limit/value pins. После закрытия
двух review gaps core outcomes оценены Strong по source/assertion mapping;
эмпирических mutations и line/branch coverage не выполнялось.

Scope остаётся минимальным checkpoint v1; atomic/observational поведение game
callback — обязательный контракт trusted game. Runtime проверяет tick/revision и
полное state после restore, но не изолирует произвольный код. Replace/rollback,
общий45s budget/20Replace proof — следующие ENG3c/d. Package consumer для нового
API, full Release solution, performance/VRAM/3D/shared GPU и hardware K3/K7/K8/final
UI alpha этим шагом не проверялись. Driver/Windows settings прежние. Изменения не
коммитились/не отправлялись; baseline dc64b63 и ENG3a working tree сохранены.

## ENG3a: compiler, artifacts и child loader — 2026-10-20

Пользователь принял предложенный порядок: сначала commit накопленных изменений,
затем artifact descriptor/compiler job/selected child DLL. Baseline сохранён
локальным commit `dc64b63` (Extract Aether engine module and integrate App lifecycle).
Текущий ENG3a реализован поверх него; прежние extraction/App/metadata/rules изменения
не потеряны. Подробный контракт и численные лимиты ведутся только в
[engine плане](PLAN.md#eng3a-принятые-контракты--2026-10-20).

### Реализация и ownership

- Builds.GameBuildArtifact публикует завершённый managed output в SHA256 manifest
  directory: host/contract hash, entry DLL/type, revision, sorted files/lengths/hashes.
  Metadata чтение не загружает игру в Editor. Полный private dependency set проверен
  по PE metadata; framework/Aether copies, traversal/links/extra/missing/tampered
  payload отвергаются. Immutable-by-publication, с повторной проверкой/pins до reap.
- GameBuildCompiler создаёт owned dotnet process, отдельные output/obj/staging;
  frozen loaded Aether reference, без shared servers и ProjectReference builds.
  Один job,120s maximum (options могут уменьшить). Cancel/timeout закрывают compiler
  tree, ждут reap/log drain, сохраняют bounded diagnostics, удаляют working output.
  Failed cleanup удерживает owned job для retry; новый build до cleanup запрещён.
- AetherHost.Builds — единственный App-owned compiler owner, без disposable aliases.
  Options/request dependencies фиксируются. App Stop закрывает admission и drains
  compiler независимо от frames/supervisor. Completed candidate не вызывает Start:
  caller проверяет revision и явно выбирает Supervisor/Frames.StartAsync artifact.
- SessionLaunch.Build несёт expected directory/BuildId; identity каждого нового
  generation использует выбранный BuildId. Native lease pins files, child повторно
  валидирует manifest/host/contract и GameBuildLoader создаёт IGameSession в отдельном
  collectible AssemblyLoadContext. Shared contract — уже loaded Aether; private DLL
  только из manifest, native game imports вне scope. Context unload cooperative,
  окончательная native boundary — child exit, не одно заявление Dispose.
- RuntimeHost сохраняет legacy fixed Game.Logic branch для ENG1/ENG2. Для selected
  artifact пишет фактические entry assembly Location/hash/PatternColor в report;
  Editor не создаёт/не loads game types. Game.Logic csproj имеет conditional frozen
  AetherContractPath reference. Core/Cobalt/Enerit/Mithril/UI production APIs прежние.
- Viewport Closing теперь terminal AetherHost.DisposeAsync до UI disposal, включая
  compiler/child/readers/pending commands. Smoke запускает held compiler при живом
  child и проверяет cancellation/reap compiler и PowerShell worker до освобождения
  canvas. Self guard примера явно изменён на2minutes без успешных control requests:
  heartbeat продлевает budget при живом Editor/build120s; parent monitor сохранён.

### Матрица требований и новых тестов

Новый GameBuildTests:12methods/24cases, плюс прежние115. Прямые tests для новых
artifact/compiler/loader и existing supervisor integration; общий native proof
проверяет реальный child. Все случаи below входят в финальные139Debug/139Release.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Immutable manifest ID, две разные game behaviors, shared Aether contract/private dependency | PublishedBuilds_LoadDifferentBehaviorAndShareOnlyHostContract |
| Pin не допускает перезапись и освобождается | PinnedArtifact_CannotBeOverwrittenAndReleasesEveryPinOnDispose |
| Manifest/DLL/missing/extra/host/ID corruption | Open_RejectsTamperedOrMismatchedArtifact |
| Rehashed schema/contract/traversal/framework/duplicate rejection | RehashedManifest_StillValidatesSchemaContractAndDependencyPolicy |
| Полный private dependency set до load | MissingPrivateDependency_IsRejectedBeforeAnyGameLoad |
| Invalid entry и release pins после отказа загрузки | InvalidEntryType_FailsLoadingAndReleasesArtifactPins |
| Compiler exit/diagnostics/no candidate/working cleanup/retry | CompilerFailure_PreservesDiagnosticsDoesNotPublishAndCanRetry |
| App Stop при game+compiler, option snapshot, new-job fence и both providers | AppStop_CancelsCompilerReapsGameAndFencesLateJobs |
| Mutable dependency list фиксируется до build | Compiler_SnapshotsDependenciesAndLeavesCandidateSelectionExplicit |
| Caller cancellation/timeout и фактический compiler/worker reap | CancelOrDeadline_ReapsCompilerTreeAndRetainsNoCandidate |
| Явный per-generation build, active-session conflict, fresh nonce/reap | ExplicitArtifactStart_ChangesBuildIdentityOnlyOnNewGeneration |
| Invalid compiler deadline до процесса | Compiler_RejectsInvalidBudgetBeforeAnyProcess |
| Active old child при compile error/cancel; Stop во время build; фактические A/B child path/hash/color/CPU pixels и file pins | build-artifacts (Debug, published Release) |
| Compiler/child/readers drained до UI disposal | viewport: native-close-cancels-compiler (Debug, published Release) |

Skill code-testing-agent: broad Research→Plan→Implement выполнен inline, поскольку
специализированного code-testing-generator здесь нет. Временные research.md/plan.md/
status.md находятся вне Git в session temp directory (путь в ignored
TestResults/ENG3a/state-directory.txt). IDE findTests вернул Tool not found; Roslyn
pairing analyzer запущен один раз, exit0. Его workspace scan захватил ignored vendor
TestResults sources (2332files), поэтому общие paired/unpaired counts не считаются
coverage или target completeness. Новые классы проверены прямыми tests и matrix.

### Финальные команды и фактические результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest. Финальный run:
`TestResults/ENG3a/final/summary.json`, Completed=true,5commands exit0.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3a/final/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3a/final/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3a/final/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3a-final-20261020 -IncludeViewport
```

Nonincremental Debug build:0errors/1508warnings. Aether139/139 отдельно Debug и
Release, failed0/skipped0. Full Debug1542/1542,9TRX: Core107, Cobalt179/generator91,
Enerit126/generator114, Mithril94, UI613, Hosting79, Aether139; failed0/skipped0.
Это новый полный Debug run, не сумма исторических module passes.

Native summary Completed=true,38/38:18headless +1viewport в каждой Debug apphost/
published framework-dependent Release без RID. Каждый build-artifacts case запускает
old child плюс A/B (3game children), compiler builds при старой сессии, actual
loaded path/hash/CPU pattern и authoring/host preservation. Каждый viewport —
15cases/25game children/20StartStop, actual GL pixels, native close с held compiler,
sessions/mappings0/childrenreleased.356source/76binary fingerprints сохранены;
отдельный completion verifier проверяет их и raw report/manifest/DLL hashes.

`TestResults/ENG3a/completion.json`: Passed=true;14Markdown files/515local links/
242anchors,24Aether/Aether.Tests C# namespaces по фактическим папкам; whitespace
и staged guards прошли, все собственные validation helpers завершились. Staged0;
ENG3a code остаётся reviewable в рабочем дереве поверх baseline commit.

Предварительный focused6 был23/23 плюс2A/B native probes; после усиления App-close
и dependency snapshot финальные tests139/native38 повторены на окончательных
исходниках. Ранние логи сохранены: xUnit async assertion compile error, namespace
collision в generated fixture, неверное имя FramePattern.Pixel в probe, неверный
expected exception base type и cooperative ALC fixture cleanup исправлены.
Loader Dispose очищает references, fixture даёт collectible contexts GC до temp
cleanup; native test отдельно доказывает pins до reap и release после process exit.

### Границы и продолжение

ENG3a — trusted-local managed artifacts, не MSBuild/game sandbox, не автоматический
Hot Reload/Replace. Immutable artifacts не garbage-collected автоматически. Caller
явно выбирает candidate и сверяет SourceRevision; compiler не держит session gate.
Native/assets deployment, package consumers новой API, performance/long trends,
full Release solution, UI golden/RTE/hardware matrix не повторялись. K3/K7/K8/VRAM/
final alpha сохраняются, driver/Windows settings не менялись.

Следующий шаг — ENG3b checkpoint/restore и recoverable capture failure; затем
ENG3c serialized transaction/rollback с общим45s budget и ENG3d20Replace proof.
Checkpoint/restore/replacement/rollback/3D в этом шаге не реализованы.

## App integration: реализация — 2026-10-19

По запросу «давай реализуем» после аудита добавлены публичные UseAether и
AetherOptions; AetherHost предоставляет App-owned Supervisor/Frames. Options
валидируются и копируются при configuration, report path становится абсолютным.
RuntimeFactory получает App resolver и создаёт свежий provider-owned instance;
конфликт с отдельной регистрацией factory/повторный UseAether отклоняется до
создания factory. Aether использует существующие TrueMoon.Contracts, без новой
зависимости на concrete bootstrap, UI или Game.Logic.

Non-disposable lifetime aliases участвуют в IStartable/IStoppable; единственный
disposable host владеет sessions/frames. App startup готовит host без запуска игры.
App Stop закрывает admission до drain, завершает readers и reap child; sync/async
provider disposal и startup rollback используют тот же cleanup. Concurrent calls
разделяют completion; ошибка наблюдаема, явный Dispose retry допустим. Даже при
ошибке frame cleanup выполняется попытка supervisor cleanup. Borrowed Supervisor/
Frames не зарегистрированы отдельными DI aliases; factory освобождается provider
после host. App Stop terminal, повторные игровые Play/Stop используют session API.

Headless/viewport examples используют UseAether; ручной SupervisorShutdown удалён.
Viewport сохраняет UI close/drain порядок. NativeRuntimeFactory требует child-role
bootstrap и matching Game.Logic DLL в executable consumer; UseAether не создаёт
entry point. Новый native app-stop начинает реальную сессию, останавливает App,
проверяет reap/release и отказ новой generation.

По требованию «вот такие модули должны подключатся через App» правило закреплено
в [AGENTS.md](../../AGENTS.md#подключение-модулей-к-app) и
[общем плане](../PLAN.md#подключение-функциональных-модулей-через-app--2026-10-19).
Utilities/contracts без собственного lifecycle не требуют фиктивной регистрации.

### Требования и integration tests

Новая AppIntegrationTests:11methods/28cases, оба provider Cobalt/Microsoft DI,
существующие87cases сохранены. Прямые ProjectReferences тестов на App и DI adapter
добавлены без executable references; framework xUnit2.9.3/VSTest сохранён.

| Требование / поведение | Точное имя теста |
| --- | --- |
| «давай реализуем»: options snapshot, explicit start, App stop и однократное ownership | Registration_SnapshotsOptions_AndAppStopOwnsExplicitSession |
| Disposal без App.Start, sync/async | DisposeWithoutAppStart_ReapsResolvedHost_ExactlyOnce |
| Failed startup rollback | FailedStartup_RollsBackPreviouslyStartedAetherLifetime |
| Изоляция двух App | IndependentApps_HaveIndependentHostsAndShutdown |
| Повторная registration/factory conflict | DuplicateRegistrationOrFactoryConflict_FailsBeforeFactoryCreation |
| Concurrent shutdown и fence новой generation | ConcurrentShutdown_JoinsCleanup_AndRejectsNewGeneration |
| Наблюдаемый cleanup failure/retry | FailedCleanup_IsObservable_AndCanBeRetriedBeforeProviderDisposal |
| Реальные CPU MMF/receiver/producer drain | AppStop_DrainsFrameWorkersAndMappings |
| Invalid options во время configuration | InvalidOptions_AreRejectedDuringConfiguration |
| Null context/configure | NullArguments_AreRejectedBeforeConfiguration |
| Null factory result при startup | NullFactoryResult_IsAnObservableStartupFailure |

IDE findTests недоступен (Tool not found); использован существующий явно связанный
TrueMoon.Aether.Tests. Тестовые controlled leases проверяют exact disposal counts,
ошибки и ordering; MMF case использует настоящий producer/receiver без UI/GPU.

### Фактически выполненные проверки

Windows x64/net10.0, SDK10.0.402/runtime10.0.13. Финальный source set:
`TestResults/AetherApp/runtime-clean/summary.json`, Completed=true,4commands exit0.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AetherApp/runtime-clean/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherApp/runtime-clean/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-app-clean-20261019 -IncludeViewport
```

Nonincremental build:0errors/1507warnings; новых integration test warnings нет.
Aether115/115 Release и115/115 Debug в full solution. Full Debug1518/1518,9TRX,
failed0/skipped0: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI613, Hosting79, Aether115.

Native summary Completed=true:36/36, по17headless +1viewport на Debug apphost и
published framework-dependent Release без RID. Каждый viewport:14cases/25child
launches/20StartStop, actual GL pixels, native-close-before-UI-disposal,
mappings/sessions0/childrenreleased, Game.Logic не загружена в Editor.
349source/76binary fingerprints проверены после запуска.

Отдельный package slice: `TestResults/AetherApp/final/summary.json`, version
`0.2.2.7-alpha.aetherapp.20261019154515219`.9fresh packages: Contracts/Core/Cobalt/
TrueMoon/Extensions.DependencyInjection/Enerit.Generator/Enerit/Mithril/Aether.
Все9pack + consumer build/publish +4consumer commands exit0. Independent consumer
имеет только PackageReferences, отдельные build props/targets/CPM/global.json/
NuGet.Config/cache; consumer graph из8TrueMoon packages точной версии (Enerit
generator — private build dependency, отдельно упакован), metadata source
свежий feed, project dependencies отсутствуют. Cobalt analyzer отключён. Debug
apphost/published Release ×Cobalt/DI проверили явные Start/Step, App.Stop cleanup,
factory/lease disposal1 и поздний start rejection; loaded DLL hashes совпали с
`lib/net10.0/TrueMoon.Aether.dll` свежего nupkg. Все4processes Passed=true.

Combined package manifest честно сохраняет Completed=false: после успешного
package slice первый full build выявил missing using TrueMoon.Dependencies в
ViewportProbe. Исправлен example import, затем весь runtime scope повторён отдельно.
Ранее initial test compile выявил missing TrueMoon.Services import в AetherOptions;
он исправлен до package proof. Последние test nullable warnings устранены явными
Assert.IsType для Resolve results. Промежуточные logs сохранены; package library
code после pack не менялся. `TestResults/AetherApp/completion.json` проверяет
финальные TRX/native/source/binary/package graph/hashes/links/namespaces отдельно
от неуспешного раннего combined manifest.

Финальный verifier Passed=true:15Markdown files/560local links/234anchors,
18C# files с namespace по реальному пути, diff whitespace/staged guard прошли.
Все6собственных фоновых helpers завершились; staged entries0, commit/push в этом
шаге не выполнялись. Предшествующие uncommitted extraction/metadata изменения
сохранены. Финальные native fingerprints относятся к исходникам после nullable fix.

### Границы и продолжение

Aether наследует IsPackable=true после снятия пользователем прежнего ограничения;
его локальная package delivery теперь проверена. Публичной публикации не было.
API экспериментальный, default native bootstrap application-specific. ENG3a
compiler/artifact остаётся следующим шагом; compiler/checkpoint/replacement/3D
не реализованы. Core/UI production code не менялся. Own Release UI/golden/RTE,
полный Release solution, performance/long resource/hardware matrix не повторялись.
K3/K7/K8/VRAM/final alpha открыты; driver/Windows settings не менялись.

## Проверка App integration — 2026-10-19

По запросу пользователя проверено наличие встроенной точки подключения Aether
к TrueMoon.App. В9handwritten C# sources модуля отсутствуют App configuration
extension, UseAether, IStartable/IStoppable adapters и ссылки на App.Builder.
Это статическая проверка исходников/project graph; новых build/unit/native/
package/performance запусков нет, implementation не выполнялась.

- [SessionSupervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) —
  IAsyncDisposable с явными Start/Stop/control operations; не регистрирует себя
  в App lifecycle. [FrameSession](../../TrueMoon.Aether/Frames/FrameSession.cs)
  аналогично требует отдельного consumer ownership/Stop/drain.
- [Headless example](../../ManualTests/EngineSessionProbe/Program.cs) создаёт
  supervisor вручную, регистрирует private SupervisorShutdown как IStoppable и
  хранит authoring fixture в App. Это локальная composition, не API Aether package.
- [Viewport example](../../ManualTests/EngineSessionProbe/ViewportProbe.cs)
  отдельно создаёт supervisor/FrameSession, подключает Alloy через UsePresentation
  и вручную drains engine tasks/frames при Closing до UI disposal.
- [Child RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs)
  самостоятельно строит App, parent monitor, game/protocol/producer и Enerit server.
  Он не является общим extension подключения Aether к App.
- [IAppConfigurationContext](../../TrueMoon.Contracts/IAppConfigurationContext.cs)
  уже доступен через существующий TrueMoon.Contracts ProjectReference Aether.
  Для module extension зависимость на concrete TrueMoon bootstrap не требуется.
  Существующие примеры аналогичного registration pattern —
  [Mithril AddUnit](../../TrueMoon.Mithril/AppCreationContextExtensions.cs) и
  [Enerit UseInvocationService](../../TrueMoon.Enerit/AppCreationContextExtensions.cs).

Рекомендация: отдельный UseAether registration contract на IAppConfigurationContext,
options/factories и явный lifecycle adapter с определённым ownership/stop/drain/
dispose. Запуск игровой сессии оставить явной операцией; UI выбирает consumer.
Consumer должен получать зарегистрированный supervisor через resolver. При
реализации проверить App Stop/Dispose/startup failure, отсутствие живых owned
children/readers и repeated configuration/DI ownership. Это предложение, публичный
API/реализация в данном audit не добавлялись; ENG3a план не подменён.

Сохранены параллельные пользовательские изменения: namespaces уже соответствуют
подпапкам, в Aether csproj сняты IsPackable=false/GenerateDocumentationFile=false.
Current package eligibility наследуется true из общих props; исторические
metadata/native test fingerprints из предыдущих runs этим audit не обновлялись.
Изменены только engine STATUS/HISTORY/PLAN. Docs links/anchors/whitespace проверены
отдельно; пользовательские code/project изменения не переписывались.

## TrueMoon.Aether — 2026-10-19

По запросу пользователя выделен единый engine module с принятым именем
**TrueMoon.Aether**. Одна library assembly объединяет прежние Engine.Probe.Contracts
и реализацию sessions/protocol/CPU frame transport/native child bridge из
EngineSessionProbe. Namespace implementation — TrueMoon.Aether, contracts —
TrueMoon.Aether.Contracts. Файлы физически перемещены, Compile-linked copies нет.
Framework net10.0, API экспериментальный, IsPackable=false.
[Принятые границы](PLAN.md#truemoonaether-одна-engine-assembly--2026-10-19),
[README](../../TrueMoon.Aether/README.md).

Aether references только TrueMoon.Contracts/Enerit/Mithril и Enerit generator;
нет dependencies на Game.Logic/Alloy/Skia/Silk. Contracts остаются DTO/domain-only
на уровне типов, но теперь находятся в одной assembly с implementation. Editor и
Game.Runtime по-прежнему разные процессы одного example executable; App startup,
конкретная game factory, FaultControl/Records и Alloy viewport остаются example
composition. Game.Logic — отдельная DLL, иначе нарушилось бы требование ENG3
обновлять игровую часть независимо от host. Новых production common-module APIs нет.

TrueMoon.Aether.Tests физически заменил прежний EngineSessionProbe.Tests;
существующие3test files/87cases сохранены с новым namespace и generated adapter
namespace. Direct ProjectReference на Aether и Game.Logic, без executable reference.
Единственный Probe.WaitAsync вызов заменён локальным helper с тем же bounded polling;
assertions не переписывались. Пакеты/xUnit/SDK/TFM остались repository conventions.
IDE findTests недоступен; тесты найдены в существующем явном test project.

NativeRuntimeFactory bootstrap key больше не зависит от executable RuntimeHost;
BuildIdentity.HostId использует entry host +Aether/Enerit/Mithril binaries.
Это сохраняет same-build handshake после переноса contracts в module assembly.
GameBuildId пока вычисляется из AppContext.BaseDirectory/Game.Logic.dll; выбор
versioned artifact и checkpoint/replacement остаются ENG3a–d.

TrueMoon.slnx теперь содержит Aether/Aether.Tests в folder Aether, example/game —
в Engine; старые Contracts/test project entries удалены. Exact `dotnet sln
TrueMoon.slnx list` подтвердил обе новые регистрации. RunProbe source roots включают
Aether/Aether.Tests; Debug/Release publication доставляет новую assembly.
README/общая карта/active status обновлены; link targets на перемещённые sources
исправлены. Исторические команды/результаты старых namespaces ниже сохранены.

### Фактически выполненные проверки

Windows x64, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3. Exact commands:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherExtraction/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-20261019 -IncludeViewport
```

Aether: Passed87/Failed0/Skipped0 в каждой Debug/Release. Full Debug solution:
Passed1490/Failed0/Skipped0,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/Aether87. Это новый solution test run и discovery нового
test project через реальный entry point; не новый nonincremental build или full
Release solution. Generated Enerit nullable/compiler warnings не скрывались;
их policy/исправление не входят в перемещение assembly.

Native summary Completedtrue,34/34: по16headless scenarios и1viewport/configuration,
Debug apphost/published framework-dependent Release без RID. Headless повторяет
по20Start/Stop при одном App, identity/stale replies, startup/command timeout/cancel,
crash/disconnect/heartbeat, owned forced stop/parent exit/reap и authoring fixture.
Viewport — по14scenarios/25child launches, ещё по20Start/Stop при одном App/window,
actual GL pixels/BGRA/origin/counters, resize/zero/resume/slow UI, live-child EOF,
first-frame cancel/deadline и native close/drain-before-UI-disposal. Reports:
ChildrenReleasedtrue/Mappings0/ActiveUiSessions0/GameLogicLoadedInEditorfalse.
Source339/binary76 fingerprints сохранены; итоговая сверка evidence отдельно в
ignored `TestResults/AetherExtraction/completion.json`.

Итоговая сверка прошла: actual Aether test container обнаружен через solution,
test/native DLL hashes совпадают в обеих конфигурациях, старой Contracts assembly
нет в runtime deps graph.34report fingerprints сохранены;12file moves без source
дубликатов и завершение owned helper processes проверены. Документация:
14Markdown files/561local links/229anchors, git diff --check прошёл, staged0.

Own Release UI/golden/RTE/package/performance/physical hardware matrix не повторялись.
Новая assembly не означает готовность ENG3/3D/standalone game/поставки; UI K3/K7/K8/
VRAM/final alpha прежние. Предыдущая документальная сверка ниже остаётся отдельной.
Следующий code шаг — ENG3a внутри Aether с сохранением game DLL/host boundaries.

## Сверка плана после UI INF7 — 2026-10-19

По запросу пользователя проверить общий план сопоставлены общий/UI/engine roadmap,
отложенный RTE backlog и текущие probe sources. На начало сверки main/origin/main
совпадали на `29d9f79860418f86e8e2fca938aafc7fef5e6072`, worktree был clean.
Это review/документация: code/project files не менялись, build/tests/native/
performance/новой проверки runtime evidence не выполнялось.

Вывод: следующий общий этап ENG3 сохраняется; full INF5/RTE развиваются по
потребности UI, hardware alpha остаётся открытой. В
[ENG3 плане](PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19) добавлены
подэтапы compiler/artifact → checkpoint → replacement/rollback → integration proof.
Новых production API, packages, 3D/input/Hot Reload задач не назначено.

| Наблюдение по code/плану | Что уточнено в execution plan |
| --- | --- |
| [Host csproj](../../ManualTests/EngineSessionProbe/EngineSessionProbe.csproj) прямо ссылается на Game.Logic, [RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs) создаёт ProbeGame | Выбранный artifact/loader/dependency set и canonical contracts должны быть определены до ENG3a; Editor DLL не загружает |
| [BuildIdentity](../../TrueMoon.Aether/Hosting/NativeRuntime.cs) читает Game.Logic из AppContext.BaseDirectory, [supervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) хранит fixed game ID | Build descriptor/manifest выбирается на generation, включая rollback; compiler staging отдельно от immutable build-ID directory |
| [IGameSession/control](../../TrueMoon.Aether/Transport/IEngineControl.cs) не имеют capture/restore; [ProbeGame](../../ManualTests/Game.Logic/ProbeGame.cs) хранит только минимальный stub | ENG3b вводит ограниченный versioned checkpoint и проверку non-default state; полной object-graph migration нет |
| [FrameSession.StartAsync](../../TrueMoon.Aether/Frames/FrameSession.cs) уже configure/wait first-present | Candidate publication после restore; первый default-state кадр не считается readiness replacement |
| CommandAsync на любую ошибку вызывает cleanup; [RuntimeProtocol](../../TrueMoon.Aether/Runtime/RuntimeProtocol.cs) не ack sequence при exception | Нужен отдельный nonterminal checkpoint rejection; обещание old paused/Resume нельзя вывести из нынешней transport-fault policy |
| ENG0 требует1game child, а общий раздел допускал подготовку двух сессий | Для ENG3 явно оставлен sequential stop/reap/start; overlap требует нового решения |
| Build120s/Replace45s заданы без распределения общего budget; runtime self guard120s | Перед реализацией определить monotonic budget/rollback reserve/cleanup и совместимый safety guard. Принятые численные deadlines этой сверкой не изменены |

Дополнены вопросы cancellation до/после destructive boundary, late build result,
Stop/новый Start/document revision и Editor close; partial logs/hash/pid/mapping
proof должен принадлежать engine runner. Прежний UI INF7 не становится ENG3 proof.
Исправлены устаревший next ENG1 в RTE плане, InputBatch приписанный ENG2 и
непомеченные исторические формулировки ENG0/ENG1. Общий STATUS/PLAN и UI STATUS
синхронизированы, подробные ENG3 задачи сохранены только в engine PLAN.

Проверка документации прошла:11Markdown files/463local links/204heading anchors,
fences/NUL/conflicts/whitespace, git diff --check и ограничение diff Markdown файлами.
Результат — ignored `TestResults/PlanningFormat/review-20261019.json`; новых runtime результатов
эта проверка не создаёт. ENG3 не начат; следующий code подэтап ENG3a.

## ENG1 headless supervisor — 2026-10-17

После RichTextEditor/PA7a и сохранения отложенного RTE backlog пользователь
перешёл к следующему этапу. Реализован первый исполнимый
[ENG1 scope](PLAN.md#eng1-headless-supervisor--2026-10-17),
[probe guide/запуск](../../ManualTests/EngineSessionProbe/README.md).

### Реализация и границы

- Созданы ManualTests/Engine.Probe.Contracts, Game.Logic, EngineSessionProbe и
  EngineSessionProbe.Tests; все4projects зарегистрированы в TrueMoon.slnx/folder Engine.
  Это probe-only API без production engine packages. Contracts не зависят от
  Mithril/Enerit/Alloy/Skia; существующие production модули для ENG1 не изменялись.
- Supervisor живёт при одном Cobalt Editor App и сериализует session commands.
  Каждый launch создаёт fresh owned ChildProcessUnitHandle и Enerit client;
  RestartPolicy.Never/IsControlAppLifetime=false, process-wide environment не меняется.
  OS Started, HelloAccepted, SceneLoaded и Paused Ready наблюдаются отдельно.
- SessionId сохраняется, UInt64 generation растёт и не переиспользуется после
  failed startup; endpoint/nonce свежие. Host/contracts/Enerit/Mithril и Game.Logic
  имеют отдельные SHA256 manifests. Runtime проверяет bootstrap с собственными bytes
  до открытия RPC. Editor вычисляет Game.Logic hash по файлу без загрузки DLL;
  native assembly census проверяет child-only границу.
- Wire DTOs mutable из-за текущего generator; identity и scene snapshots immutable.
  Enerit Int64 несёт полный UInt64 bit pattern generation/sequence/tick, domain
  counters остаются UInt64 с checked increments. Это явное уточнение wire adapter,
  без расширения общего scalar codec/лимита1MiB/schema negotiation.
- Фактический generated request/response payload ограничивается≤64KiB; scene
  проверяет≤64distinct entities/finite transforms/ID lengths и копируется до mutation.
  Hello/LoadScene/SetMode/Step/GetStatus/Stop имеют explicit identity/sequence/PID/
  mode/revision/tick acknowledgement. Playing timer target60Hz, performance не измерена.
- Stop ставит fence и отменяет локальный wait до входа в gate. Поздние replies и
  queued commands не применяются, interrupted command не повторяется. Faulted
  сохраняет phase/первичную cause; cleanup обязателен. CleanupFailed удерживает
  lease и блокирует Start до явного успешного cleanup retry; acknowledgement не
  выдаётся за process reap. Observer/diagnostic failures не меняют ownership.
- Единый graceful Stop budget3s включает RPC acknowledgement и Mithril stop;
  затем owned kill/reap≤5s. Startup15s, ordinary command2s, heartbeat1s/не более5s
  без accepted reply. Child parent monitor и аварийный2minute self deadline ограничены.

Game.Logic — минимальный отдельный DLL stub с loaded scene/tick/pattern color.
Frame metadata/codec/ring/viewport и input/surface ещё ENG2; checkpoint/restore,
compiler/build directories/replacement/rollback ещё ENG3.20Replace cycles не
выполнялись. Full UI alpha, K3/K7/K8/VRAM, самостоятельная игра/package delivery,
3D/GPU isolation и long resource/performance trend не объявлены готовыми.

### Финальные проверки

Windows x64/build26300/net10.0/SDK10.0.402; xUnit2.9.3/VSTest, central versions
пользователя сохранены. Default Cobalt; отдельная engine Microsoft DI matrix не
требовалась. Исторические UI package/golden/native результаты не повторялись.

| Проверка | Результат | Ignored artifact внутри TestResults/EngineSessions |
| --- | --- | --- |
| Engine Debug suite через solution |31passed/0failed/0skipped | final-tests/solution-Debug/TRX; прежний direct run сохранён отдельно в tests/engine-debug-final.trx |
| Direct engine Release suite |31passed/0failed/0skipped | final-tests/engine-Release/TRX и stdout.log |
| Full Debug solution entry point |1305passed/0failed/0skipped,8TRX; engine31 обнаружен через TrueMoon.slnx | final-tests/solution-Debug/TRX и solution-Debug-tests.stdout.log |
| Nonincremental Debug build |exit0/0errors/1207warnings | final-tests/solution-Debug-build.stdout.log |
| Native Debug apphost |16/16scenarios, включая20Start/Stop в одном Editor App | final-native/Debug-*/report.json/logs |
| Published Release |16/16тех же scenarios, framework-dependent без RID | final-native/Release-*/report.json/logs и publish binaries |

Debug solution counts: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI563, engine31. Release — только engine31; суммарные9TRX/1336passed
не являются двумя полными solution suites. Семь новых warnings — existing
Enerit generator CS8669 на nullable annotations в generated code; warnings не подавлены.

Final native summary:Completed=true,32scenario processes,34commands,187source
fingerprints и24binary entries. По20cycles в каждом repeat scenario, отдельные
sequential generations и recovery после fault. Во всех живых Editor проверяется
неизменность authoring revision/Dirty/selection/panels и App lifetime; каждый
created lease заканчивается подтверждёнными reap/resource cleanup.
Launch-failure case не выдаётся за успешно запущенный процесс.

16scenarios/configuration: repeat, stale, bad-hello, bad-scene, startup-timeout,
startup-cancel, command-timeout, command-cancel, stop-pending, crash, disconnect,
heartbeat, forced-stop, hang-stop, launch-failure, parent-exit. Negative failures
внутри сценариев ожидаемы и проверяются;32passing reports относятся к outer probes.
Hang-stop одновременно не отвечает на Stop RPC и игнорирует graceful process
shutdown: проверены shared deadline, observed StopAcknowledgement cause и owned kill.
Parent-exit proof убивает только принадлежащий ему parent без tree-kill, ждёт
child monitor cleanup marker и read-only OS census; census не является ownership path.

Основные команды из src (точные output paths есть в summary/logs):

```powershell
dotnet sln TrueMoon.slnx list
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Debug --logger "trx;LogFileName=engine-debug-final.trx" --results-directory TestResults/EngineSessions/tests --verbosity quiet
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/final-native
```

Unit evidence: Start_WaitsForHelloAndAppliedSceneBeforeReady,
RepeatedStartStop_UsesFreshLeasesGenerationsAndNonces,
MismatchedHello_IsRejectedBeforeSceneAndConsumesGeneration,
CleanupFailure_PreservesPrimaryErrorAndBlocksStartUntilExplicitRetry,
Stop_FencesPendingAndQueuedCommandsAndDiscardsLateReply,
StartupCancellation_CleansWithoutPublishingReadyAndAllowsNextGeneration,
UnexpectedExit_FaultsAndCleansWithoutCallerPolling,
ModeAndStep_ApplyOnAcknowledgementAndRejectStepWhilePlayingLocally,
InvalidApplicationAcknowledgement_FaultsInsteadOfCommittingState,
GeneratedWire_RoundTripsAllUnsignedBitsThroughBoundedTransport,
OversizedGeneratedPayload_IsRejectedBeforeTransportInvocation,
RuntimeProtocol_RejectsOrderAndSceneErrorsBeforeMutation.
Тесты используют реальные generated codecs и управляемые fake leases; native
matrix отдельно доказывает actual process/IPC/parent-exit composition.

### Исходные failures и дополнительные сверки

Initial build остановился на TME001:UInt64 scalar unsupported; переход wire
fields к bit-preserving Int64 исправил contract без изменений Enerit. Далее
исправлены предположение о generated namespace (он относится к host assembly)
и отсутствующие namespace imports Resolve/UnitCodes. Финальная сборка прошла.
Unit runs с момента первой компилируемой версии31/31, без skipped.

Сначала прошли repeat20 и matrix30cases. При ручной сверке deadlines найден
лишний отдельный3s budget Stop RPC перед Mithril grace; объединён в один token,
добавлен hang-stop и повторены32cases. Затем устранён typeof(Game.Logic) при
Editor hashing, добавлен assembly-boundary assertion и повторена final-native
matrix32cases на финальном source. Ранние passing matrices сохраняют свой
source scope; не выдаются за финальный запуск.

Вспомогательный verifier после уже успешных tests сначала ошибся на TRX filename
с [1] из-за wildcard Get-Content; чтение переведено на LiteralPath. При правке
helper также исправлена лишняя скобка. Исходный failure сохранён в verification/
summary.json, resume — summary-final.json; final-tests выполнен заново после
последнего code change и не использует неполный summary как passing evidence.

Подробные source/test/native logs сохранены внутри ignored TestResults; до
начала работы в дереве были пользовательские изменения и10staged entries.
Они не сбрасывались и не использовались как основание для commit/release.

Финальная сверка:11Markdown documents/301local links/127heading links,
fences/conflicts/NUL/whitespace и новые files whitespace прошли; полная
git diff --check с Windows CR-at-EOL policy чистая. Solution содержит все4engine
projects и test→production reference;187source/24binary fingerprints совпали
после final test/build. Существующие10staged entries сохранены, engine changes
не staged. Отчёт ignored TestResults/EngineSessions/completion.json.
Следующий конкретный шаг — ENG2 CPU codec/ring/viewport с сохранением ENG1 guards.

## ENG0 scope и контракты — 2026-10-15

Пользователь одобрил ENG0 после UI package/Hosting proof: ограниченный Editor/child
prototype, demo-комната/одно взаимодействие, CPU shared-memory frames и versioned
Game.Logic replacement. Допуск с открытыми UI K3/K7/K8/VRAM/final alpha принят явно.
Эти критерии не отменены; full alpha/engine readiness не заявлены.

В PLAN определены Windows x64/net10.0/Cobalt/OpenGL Editor, CPU pattern ENG1–ENG3,
target1280×720/30fps и tick60Hz. Эти targets/deadlines/budgets назначены для будущих
проверок и не являются измеренной производительностью или minimum hardware promise.
Vulkan3D/GPU sharing, standalone delivery и окончательный жанр имеют следующие этапы.

Сверены исходники IUnitsController/UnitsController/ChildProcessUnitHandle/
ProcessUnitHandle/ExternalUnitHandle и UnitConfigurationExtensions: controller
terminal после Stop, общий nonempty parameters unsupported, child запускает entry app.
Выбран один probe executable с двумя process roles, fresh ChildProcessUnitHandle
на каждое поколение и probe-owned per-launch context через protected hooks.
Это проектирование; repeatable use этих hooks в engine ещё не подтверждён.
Изменений Mithril/Enerit/Core/Cobalt/UI runtime в ENG0 нет.

Определены SessionId/SessionGeneration и отдельный SurfaceGeneration, HostContractId/
GameBuildId/versions, фазовая readiness, typed same-build64KiB control, bounded input.
Первый frame transport —3slot CPU ring/BGRA8/sRGB с явными ownership transitions,
latest-ready/drop policy, копированием вне UI thread, bounded memory, stale-frame
rejection и terminal recovery при повреждении/abandoned metadata.
Новый viewport/upload adapter относится к ENG2; наличие готового Alloy API не обещано.

Game replacement: compile candidate отдельно от active build, pause/checkpoint,
stop/reap, candidate restore/first frame, максимум1rollback к last-known-good.
Compile failure сохраняет текущую сессию; snapshot mismatch не reset без явного
выбора. Editor documents/Dirty/selection/panels принадлежат Editor и переживают
обычный child crash/replace. Failed cleanup блокирует новый launch.

Следующий code scope ENG1 — headless host/contracts/logic stub/supervisor с20repeat
Start/Stop, identity/Hello, stale response, startup timeout/cancel, crash/disconnect,
parent exit и cleanup. ENG2 добавляет UI/frame ring; ENG3 compiler/replacement.
Новых csproj/public API/build/tests/process/GPU/performance результатов нет.
Детальные задачи не дублируются в общих документах; их очередность/допуск
синхронизированы с общим PLAN/STATUS/QUALITY и UI STATUS.

### Проверки текущего документа

Фактически выполнено2026-10-15:9Markdown documents,219local links,110heading links,
0issues; проверены fences/conflict markers/NUL/replacement characters, наличие
ENG0–ENG8 и identity/limits/headless milestone. Scoped git diff --check passed,
staged changes0. Артефакт: ignored
`TestResults/EnginePlanning/eng0-20261015/verification.json`; checker
`TestResults/EnginePlanning/CheckEng0.ps1`. Это только documentation validation,
без code tests или нового module/runtime passing evidence.

## Исходное документирование — 2026-10-13

По предыдущему STATUS:42Markdown documents/447local links/107heading links,
0problems; fences/conflict markers/NUL и git diff --check passed.
Артефакты ignored TestResults/EnginePlanning. Новые files проверялись отдельно
на whitespace и наличие ENG0–ENG8. Это историческая документационная проверка,
не текущий запуск и не engine code evidence.

## ENG2 CPU frame viewport — 2026-10-17

По запросу следующего шага реализован [ENG2 scope](PLAN.md#eng2-cpu-frame-viewport--2026-10-17).
Код transport/contracts/viewport остаётся в ManualTests. Единственное production
UI API расширение — owned CopyBgraPixels в SkiaImageSource. Backend-neutral image
borrowing, renderer/device/window ownership, Core/Cobalt/Enerit/Mithril APIs сохранены.

### Реализация

Explicit codec проверяет version/header/layout/capacity/session/build/child/surface;
BGRA8 premultiplied/sRGB/top-left pattern включает corners/frame/tick/actual CRC32.
Windows ring3slots использует named mutex только для metadata, pixels вне lock;
latest-ready освобождает older Ready, producer не перезаписывает Reading и drops
при no Free. Abandonment/corruption/100ms timeout/CRC/EOF terminal, events лишь hints.
Receiver вне UI owner имеет2reusable buffers/1pending; disposed lease отпускает
ссылку на pixels, old receiver drains до allocating replacement buffers.

ConfigureSurface ack следует detach old producer; old mapping живёт до local
reader drain и ack/reap. Resize serial,≤2mappings/50MiB. Zero-size без mapping,
heartbeat сохраняется. First copied frame не означает Ready: FrameSession.Start
завершается только после actual owner presentation, в общий15s launch deadline.
Stop fences queued work/cancels current wait; stale display не подтверждает новый
surface. CleanupFailed retains mappings until explicit successful reap retry.
Native close drains controller/readers/child до UI disposal.

### Проверки на финальном source

SDK10.0.402 / .NET10.0.13 / Windows x64. Final native artifact:
`TestResults/EngineSessions/eng2-final-source-native/summary.json`;
34/34 cases:32ENG1 regressions +2viewport runs, Debug apphost / published Release.
Each viewport run14scenarios,25child launches, по20Start/Stop в одном Editor App/window:
first-presented1280×720, mode/step, resize320×180/1920×1080/16×16/1280×720,
zero-size/resume, slow UI/latest pending, crash, EOF при живом child, first-frame
cancel/15s deadline и native WM_CLOSE. GPU corners/enlarged counter rows сверены
по actual readback с учётом linear sampling, CPU CRC — до публикации receiver.
Final screenshot inspected. Mappings0/UI sessions0/children released/editor preserved,
Game.Logic assembly в Editor не загружена. Source323/binary76fingerprints и36commands
сохранены; final повторная сверка — `TestResults/EngineSessions/eng2-completion.json`.

| Run | Copy p50/p95 ms | Upload p50/p95 ms | Produce→present p50/p95 ms | Pending replacements |
| --- | --- | --- | --- | --- |
| Debug | 4.3877/22.2671 | 0.0593/0.5373 | 31.3073/77.5625 | 21 |
| Published Release | 1.5674/7.0793 | 0.0546/0.6386 | 29.6735/55.3139 | 22 |

Bounded256sample diagnostic windows; observed producer drops0 в GUI runs,
no-free producer drop отдельно подтверждён ring test. Это не baseline/performance
pass, не VRAM/native long-run resource trend. UI hardware blockers не закрываются.

Exact commands записаны runner summary:

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -RunDirectory TestResults/EngineSessions/eng2-final-source-native
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/ui-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

Engine87/87 Debug/Release, UI570/570 Debug/Release; full Debug1368/1368,
8TRX/failed0/skipped0. Engine includes new56cases (codec/wire/ring/receiver/composition)
и31ENG1cases; UI добавлены7shape/ownership/pixel cases.
Build0errors/1211warnings; warnings не подавлены.
Detailed regression evidence: FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip,
Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded,
Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain;
FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay,
Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild,
MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry;
SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha.

### Найденные и исправленные failures

Initial compile corrected Span→array delegate, Check accessibility и font asset path.
First native startup exposed missing Cobalt resolver from generic UsePresentation
registration: added explicit Transient<ViewportView> plus factory Action<string>.
Unit cleanup assertion initially expected missing mapping, but first absent resource
is named mutex (WaitHandleCannotBeOpenedException); assertion corrected to actual API.
Regression on a second read after CRC/EOF exposed misplaced terminal guard; guard
moved into each metadata operation and verified by repeated-fault assertions.
Native IsClosing assignment closed window without native close callback; replaced
smoke trigger with Process.GetCurrentProcess().CloseMainWindow (real WM_CLOSE),
then confirmed drain before UI disposal. First copies-only metrics were overwritten
by no-frame session; switched to bounded aggregate samples. Final review cleared
pixels reference on disposed lease and synchronized receiver counters; full native
matrix repeated after these edits. Failed/intermediate runs remain under ignored
TestResults/EngineSessions/eng2-*; none заменяет final evidence.

Next ENG3 compiler/checkpoint/versioned replacement/rollback,20Replace ещё не было.
InputBatch/focus routing remains deferred explicitly in PLAN. RTE backlog unchanged.

### Матрица ENG2 requirement / evidence

| Requirement | Evidence: exact test / run |
| --- | --- |
| Little-endian schema, unsigned wire/capacity/identity validation | FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip; ConfigureSurface_GeneratedWirePreservesUnsignedGenerationAndDescriptor; Codec_RejectsEveryHeaderIdentityAndCapacityField |
| Latest-ready/drop/Reading ownership | FrameTransportTests.Ring_ChoosesNewestReadyReclaimsOlderAndDropsWhenFull; Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded |
| Terminal CRC/EOF/mutex faults | FrameTransportTests.Ring_CorruptionAndEofAreTerminal; Ring_MetadataTimeoutAndAbandonmentFailWithoutUnboundedWait |
| Two buffers/one pending and hint-only event | FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain; Receiver_ScansWithoutReadySignalAndPropagatesEof |
| Surface ack/presentation/stale/resize/suspend | FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay; ZeroSize_SuspendsReleasesMappingsKeepsHeartbeatAndResumesFreshSurface; Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces |
| Stop fence/cleanup retry | FrameSessionTests.Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild; MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry |
| Owned BGRA image, origin/alpha/shape | SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha; CopyBgraPixels_RejectsInvalidShapeBeforeNativeAllocation |
| Actual process/window/pixels/deadline/close | eng2-final-source-native Debug-viewport/Release-viewport reports;32ENG1 regressions;34/34 native |
| Successful validation commands | RunProbe.ps1 -IncludeViewport; dotnet test engine Release; dotnet test UI Release; dotnet test TrueMoon.slnx Debug; dotnet build TrueMoon.slnx Debug --no-incremental — exact arguments above |

Финальный documentation guard:13documents/470local links/154heading links,
fences/whitespace/conflicts/NUL и scoped/full git diff --check passed.
323source/76binary fingerprints повторно совпали после nonincremental build;
4engine projects/production test reference в solution проверены. Existing10staged
entries сохранены; новые engine/ENG2 test files не staged. Артефакт:
`TestResults/EngineSessions/eng2-completion.json`. Ни один новый package не публиковался.

## Сводки до ENG7a — 2026-10-22

Последняя работа — **2026-10-22, ENG5c.2: reach/raycast и per-object interaction**.
Actual affine cube raycast, nearest/opaque occlusion, inclusive reach и deterministic
ties реализованы в Aether. RoomGame использует reach4 и E pulse для переключения
только выбранного объекта, с возвратом authored RGB. TMCK2 переносит immutable
stable-ID switches, restore валидирует exact loaded set до atomic commit.
Control3/frame1 прежние, Snapshot2 требует rebuild matching binaries, TMCK1 отвергается.
52новых cases; focused102/102, nonincremental Debug build прошёл; full Debug/Release
2186/2186/configuration,9TRX/failed0/skipped0; Aether780/UI615/Hosting79/Mithril95.
[Контракт](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Найдена/исправлена diagnostic JSON constructor-binding regression immutable
object state; отдельный тест проверяет roundtrip и duplicate-ID rejection.
Новый native interaction proof Debug/published Release: по10cases/9PNG/4children,
core/sync0/0, resources/handles/mappings/UI sessions/imported ReadPixels0.
Local Aether pack/payload совпал SHA256 с final managed/native Release;
package drawing consumer не запускался. Все52pending C#namespace/whitespace checks прошли.
Итоговая fresh native matrix после JSON fix60/60cases/62commands,
478source/76binary fingerprints: новые interaction и прежние CPU/GPU/input/
Editor Replace/failure regressions прошли в Debug/published Release.
Summary — `TestResults/EngineSessions/eng5c2-final-jsonfix/summary.json`.
Completion Passed=true; docs10Markdown/284links/189anchors/errors0.
Reports: `TestResults/ENG5c2/managed-summary.json`, `docs-check.json`,
`namespaces-check.json`, `completion.json`; full TRX — `final-*-jsonfix`.
Далее [ENG5c.3](PLAN.md#eng5c3-следующий-шаг-collisions-камеры): минимальный объём
камеры/игрока и fixed-tick collisions, затем target/HUD/pause. Full World snapshot,
runtime edits/MCP, raw OS input/3D performance и прежние hardware/release gates открыты.

Предыдущая работа — **2026-10-22, ENG5c.1: scene consumers и несколько примитивов**.
Stable v1 Transform/Appearance/Interaction schemas, explicit local*parentWorld
projection, deep-owned control-v3 primitive payload и immutable multi-draw snapshot
реализованы. Vulkan рисует пол/стены/три объекта комнаты в прежний App-owned GPU
viewport; separate Game.Logic.RoomGame получает authoring data через IPC.
`--room` использует прежний fixed-tick input/Editor/Replace. Control v1/v2 отклоняются,
matching binaries требуют rebuild; frame1/TMCK1 прежние. Геометрия статична в
пределах scene revision, checkpoint только camera/tick/interaction.
Fresh native matrix58/58cases/60commands,473source/76binary fingerprints:
Debug apphost/published Release, новая комната и прежние CPU/GPU/input/Replace/
failure regressions прошли. Room по5PNG/configuration, core/sync validation0/0,
resources/handles/mappings/UI sessions0, imported ReadPixels0. Local Aether pack
прошёл, DLL совпала SHA256 с managed Release/published proof; package drawing
consumer не запускался. Все42pending C#namespace/whitespace checks прошли.
36новых cases; full Debug/Release2134/2134/configuration,9TRX/failed0/skipped0,
Aether728/UI615/Hosting79/Mithril95. [Контракт](PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
Отчёты: `TestResults/ENG5c1/managed-summary.json`, `package-check.json`,
`namespaces-check.json`, `docs-check.json`, `completion.json`;
native — `TestResults/EngineSessions/eng5c1-final/summary.json`.
На этапе ENG5c.1 далее планировался [ENG5c.2](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22): bounded
reach/raycast target, наблюдаемое interaction и versioned object state/checkpoint;
затем нужные collisions/HUD. Full world snapshot/runtime editing/MCP остаются
ARCH1c/d/ENG7; performance/raw OS input/прежние UI hardware/release блокеры открыты.

Предыдущая работа — **2026-10-22, ARCH1a.2: scene aggregate/resolver и compatibility adapter**.
SceneDocument фиксирует revision/catalogs, SceneResolver проверяет instance IDs,
parent graph и все entities и публикует immutable ResolvedSceneSnapshot с
parent-first order и exact referenced definition/schema versions. Errors сохраняют
scene/entity path и original cause. SceneSnapshotAdapter задаёт explicit world-space
projection в прежний SceneSnapshot с canonical IDs/revision и limit64entities.
52новых xUnit cases, scene suite130/130. Full Debug/Release2098/2098/configuration,
9TRX/failed0/skipped0; Aether692/UI615/Hosting79/Mithril95.
[Контракт](PLAN.md#реализация-arch1a2--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
ProbeGame.Load/tick/checkpoint совместимость проверена. Native/GPU/package checks
не повторялись; прежние SceneSnapshot/control/frame/TMCK и интерактивный sample
не изменены. Полный runtime snapshot/edit и MCP запланированы.
На этапе ARCH1a.2 далее планировался ENG5c.1.
Документация: 8 Markdown, 204 локальные ссылки/122anchors, ошибок0. Все24pending
C# файла (9этого этапа) прошли namespace/whitespace checks; scratch artifacts в Git0.
Отчёты: `TestResults/ARCH1a2/managed-summary.json`, `docs-check.json`, `completion.json`.

Предыдущая реализация — **2026-10-22, ARCH1a.1**: immutable definitions/variants/
instances, components/schema/patches и EntityResolver;78новых cases. Full Debug/
Release2046/2046/configuration, Aether640. [Scope/evidence](HISTORY.md#arch1a1-immutable-модель-и-resolver--2026-10-22).

Предыдущая работа над реализацией — **2026-10-21, ENG5b gameplay input/camera**:
control v2, optional IInputGameSession, bounded staging/lease и fixed-tick camera,
отдельная CameraGame, interactive `--scene` с WASD/mouse/E/Escape. Input transient;
camera/tick/interaction сохраняются при checkpoint Replace. App-owned supervisor
проверяет exact identity внутри gate; stale input не retargeted. Один30Hz sender
drains до host/UI; Pause/focus/capture/resize/suspend/Stop/restore сбрасывают input.
Argentis W/S/D/E и Silk mapping дополнены. Raw mouse/cursor lock отсутствуют.
42новых xUnit cases; full Debug1968/1968 иRelease1968/1968,9TRX/configuration,
failed0/skipped0: Aether562/UI615/Hosting79/Mithril95.
Native control/CPU/GPU/3D/input regression56/56cases/58commands; усиленный input
proof2/2 (Debug/published Release), по15cases/5PNG с реальным Silk key callback.
Validation0/0, imported ReadPixels0, resources/handles/mappings/UI sessions0,
sender завершён. После full matrix изменён только proof harness; library DLLs
сверены между полным и усиленным run. Local packs Aether/Argentis/Platform.Silk
прошли, DLL payload сверены с published proof; отдельный package drawing не запускался.
[Контракт, exact tests/commands, provenance и ограничения](HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
На момент ENG5b следующим был ENG5c комната/interaction/HUD; весь ENG5 ещё частичен.

Предыдущая работа — **2026-10-21, ENG5a: первый Vulkan3D renderer**.
Optional immutable scene snapshot под simulation/checkpoint lock, perspective
camera, cube, opaque material и slot-owned depth/pipeline реализованы в одной
TrueMoon.Aether. Vulkan рисует в exportable image без CPU pixels/staging; обычный
App/Editor/GPU transport и default CPU/reference путь сохранены.
Debug и published Release 3D proof: по5PNG assertions (depth/material/camera,
resize/suspend/resume/checkpoint Replace), core/sync errors/warnings0,
resources/handles/mappings/UI sessions0, imported ReadPixels0.
Итоговая native matrix12/12cases/14commands,430source/76binary hashes: новые
scene и прежние import/surface/viewport/ordinary GPU Editor/repeat прошли.
Local pack прошёл; DLL package совпадает SHA256 с native published proof.
Новые focused tests19/19; full solution Debug1926/1926 иRelease1926/1926,
по9TRX/failed0/skipped0: Aether520, UI615, Hosting79, Mithril95.
Это source apphost/published3D proof; package GPU drawing и3D performance не
подтверждены. [Контракт, команды, failures и evidence](HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
ENG5a завершает первый renderer, весь ENG5 остаётся частичным; далее ENG5b input/camera.

Предыдущая работа — **2026-10-21, ENG4e CPU/GPU baseline**. Реализованы opt-in
stage timings, bounded raw/process captures и воспроизводимый runner.
Финальная матрица12/12cases/36intervals/24cooperative children/36pixel checks,
15commands/421source/94binary fingerprints, Completed=true. Release1280×720:
медиана per-run p95 CPU65–66ms/GPU21–33ms; GPU reference allocation~107MiB/s.
Debug CPU:2FPS/6latency advisory breaches, включая12.51fps outlier; budgets прежние.
Raw QPC stages37766, cohorts5244produced/received и5182presented. Local GPU
resources/handles/mappings/UI sessions0; imported ReadPixels0. Это reference
CPU-generated/Vulkan-staging workload, не3D/VRAM/physical-display performance.

Найден/исправлен Mithril exit-delivery/graceful-wait race с red/green regression;
native20shutdown cases прошли. Full solution Debug1907/1907 иRelease1907/1907,
по9TRX/failed0/skipped0: Aether501/UI615/Hosting79/Mithril95, остальные counts
сохранены. Отдельный GPU correctness run:6cases/8commands, Debug/published Release,
core/sync validation0/0; per ordinary GPU Editor20Replace/20failures/53children,
retained copies829/813, imported readback0. Nonincremental build/golden/RTE/full
UI/hardware/VRAM matrix этим не повторены. [Подробные результаты, исходные failures,
команды и ограничения](HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Fresh units packages108/108cases/8nupkgs проверили доставку Mithril fix; это не
Aether GPU package drawing. Независимый replay36intervals/raw37766records и
1048source/170binary hashes прошёл (`TestResults/ViewportBaseline/development/completion.json`).

Исправление структуры — **2026-10-21**: Aether.Contracts устранён;21тип размещён
по назначению в Sessions/Scenes/Transport/Frames/Checkpoints, engine assembly одна.
Все Aether/test/example namespace соответствуют путям. Aether493/493 Debug/Release;
общий solution1898/1898 в каждой конфигурации. Fresh12packages/4App consumers и
итоговая targeted native matrix20cases (Debug/published Release) прошли.
Native checks выявили PID reuse: GPU adapter искал lease по PID в истории.
Оба viewport examples теперь получают текущий owned NativeRuntimeLease напрямую,
а fault decorator раскрывает конкретный inner lease. Probe сохраняет исходную
ошибку до assertion; условия проверок сохранены. Итоговый run включает CPU/GPU
20Replace/20failures proofs. [Команды/границы/миграция](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. UseAether options
выбирают lazy App-owned IGameViewportTransport; default CPU. FrameSession сохраняет
общий Start/Resize/Stop/first-present lifecycle, GameEditor/GameReplacement —
selected DLL/checkpoint/rollback. ViewportSurface не содержит native/mapping handles;
ViewportFrame ticket подтверждается только для актуальной presentation.
GpuViewportTransport копирует borrowed texture в независимый UI image, returns
slot после observed GL completion и удерживает первый image до actual first-present.
Aether остаётся одной assembly без UI dependency.
[Контракт](PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20),
[API](../../TrueMoon.Aether/README.md#выбор-transport-в-gameeditor--eng4d2),
[evidence](HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
