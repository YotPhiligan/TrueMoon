# Engine / Editor: история решений и проверок

[Текущий статус](STATUS.md), [единственный детальный план](PLAN.md).

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
