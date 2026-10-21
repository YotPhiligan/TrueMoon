# Engine / Editor: история решений и проверок

[Текущий статус](STATUS.md), [единственный детальный план](PLAN.md).

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
