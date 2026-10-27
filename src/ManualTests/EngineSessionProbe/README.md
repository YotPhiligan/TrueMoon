# EngineSessionProbe — пример TrueMoon.Aether (ENG1–ENG5c.2)

## ENG8a.3: child diagnostic delivery

После build запустите `EngineSessionProbe --child-diagnostics-proof <new-directory>`.
Headless сценарий создаёт3Preview children одной session через штатные App factories;
каждый выполняет33control commands, одновременно выдавая structured log flood и
raw stdout/stderr. Host использует bounded Core pipeline со slow/throwing sinks.
Проверяются child origin/PID/time/trace, loss counters, local JSONL/raw limits и
actual process reap/resource release. Game.Logic, окно и GPU surface не запускаются.
Preview использует GetStatus/SetMode(false), Step для этой роли не разрешён.

Результаты: delivery-report.json, delivery-child-1/2/3.json, host/child JSONL и
runtime-output. Этот source proof также входит в ChildDiagnosticProcessTests;
он не заменяет published/package/GPU/performance acceptance.
[Подключение, budgets и ownership](../../docs/engine/DIAGNOSTICS_DELIVERY.md).

## Независимый Edit preview

`EngineSessionProbe --edit-preview-proof <new-directory>` использует
UseAetherAuthoring и host.Preview: committed Transform → pixels → Undo/Redo,
точные revision/runtime/surface stamps, resize/suspend/resume и закрытие окна
с pending first-present. Build/Play/game project не вызываются; встроенный child
не загружает Game.Logic. Профиль — существующий Windows x64 Vulkan/OpenGL GPU
viewport с fixed camera; camera/tools и normal Editor UI не входят.
Для validation задайте process-local TRUEMOON_GPU_VALIDATION=1 и VK_LAYER_PATH
на существующий SDK Bin. Результаты: preview-report.json,6PNG и runtime cleanup
reports. Published proof отдельно выполняется из копии без Game.Logic.dll и вне repo cwd.

## ENG5c.2: reach/raycast и переключатели объектов

Запустите `--room`, нажмите Play и кликните viewport. WASD/mouse приблизьтесь к
кубу и наведитесь на него; E меняет его цвет на жёлтый в пределах4world units от
камеры до поверхности. Повторное нажатие возвращает authored color. Ближайшая
opaque geometry блокирует цель, даже если Interaction отключён. Удержание E
не повторяет действие. Camera/tick и полный набор per-object switches переживают
Pause/Replace в TMCK2. Geometry/authoring data не редактируются.

`--interaction-proof <fresh-directory>` или runner `-IncludeInteraction` проверяет
10сценариев и9PNG на configuration: вне reach/подход, только nearest target,
однократный pulse, resize/suspend, Replace и input reset, возврат цвета,
заслонённую и отключённую цели. Четыре owned children, core/sync validation и
полный cleanup проверяются в Debug apphost/published Release. Отдельный прежний
input proof проходит через настоящий Silk keyboard callback; это не физический OS input.
Control3/Frame1/Snapshot2 требуют совместного rebuild; TMCK1 отклоняется.

## ENG5c.1: authoring-комната и несколько примитивов

```powershell
dotnet run --project ManualTests/EngineSessionProbe/EngineSessionProbe.csproj -- --room
```

Editor строит `RoomScene` через SceneDocument/SceneResolver/PrimitiveSceneAdapter;
отдельный child загружает `Game.Logic.RoomGame`. Семь кубов образуют пол, три стены
и три цветных объекта. Синий объект наследует rotation/scale группы. World-матрицы
и материалы передаются через control v3, а GPU frame transport остаётся прежним.
Play и click в viewport включают WASD/mouse; Escape освобождает capture.
Это статичная геометрия в рамках scene revision; TMCK2 сохраняет camera/tick/
interaction и object switches, геометрия повторно загружается при Replace.
Collisions и игровой HUD — следующие этапы.
Control v1/v2 отклоняются, rebuild Editor/Runtime/Aether/Game.Logic обязателен.

`--room-proof <fresh-directory>` проверяет три независимых center/material samples,
границы pixel counts, пол/стену/occlusion, fixed-tick camera, resize/suspend/resume,
Replace и supported checkpoint state. Пять PNG hashes, source/binary fingerprints,
validation/ownership cleanup фиксируются runner:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 `
  -RunDirectory TestResults/EngineSessions/my-room-run `
  -Scenarios @() -IncludeRoom `
  -GpuValidationLayerDirectory <validation-layer-Bin>
```

Debug apphost и published Release проверяются отдельно. Readback применяется
к итоговому UI screenshot для assertions, imported GPU texture не читается на CPU.

## ENG5b: управляемая камера и ввод

```powershell
dotnet run --project ManualTests/EngineSessionProbe/EngineSessionProbe.csproj -- --scene
```

Запускается `Game.Logic.CameraGame` через App/Build/Editor и GPU transport.
Нажмите Play, затем кликните по изображению viewport: WASD движет камеру по XZ,
мышь поворачивает её (logical deltas,0.0025rad/unit), E переключает demo color
при наличии interactive entity. Escape возвращает pointer/focus Editor.
Pointer capture не блокирует OS cursor; для полного вращения нужен дальнейший
raw/relative input профиль. Комната доступна через `--room`; collisions/raycast/HUD пока не реализованы.
Start/Play/Pause/Step/Stop/Build/Replace/Refresh/Cancel используют обычный GameEditor.
Нажатия E coalesced по rising edge, быстрый down/up не теряется, repeat не повторяет
действие. Sender один,30Hz, без task на event; accepted input применяется только
на fixed tick. Pause/focus loss/resize/suspend/Stop/Replace очищают transient state;
для нового capture нужен click. Camera/interaction переживают checkpoint Replace.
Reference ProbeGame/CPU examples сохраняют прежнее поведение.

Control protocol v3, Frame1/TMCK2; host/game пересобираются совместно.
Input watchdog —12fixed ticks (200ms при непрерывных60Hz), не wall-clock
timeout в Paused. Unsupported input capability возвращается отдельно от mode.

`--input-proof <fresh-directory>` вызывает настоящий keyboard callback Silk
для WASD/E/Escape и normalized UiSession pointer/focus events в GPU-окне:
15cases,5PNG, камера/checkpoint/color/Replace,
input sender drain и cooperative child cleanup. Это не physical keyboard/mouse
или OS focus automation. Пример воспроизводимого runner из `src`:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5b-local `
  -IncludeInput `
  -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin `
  -Scenarios @()
```

## ENG5a: первый Vulkan3D frame

`--scene-proof <fresh-directory>` выполняет bounded proof через обычные
App/UseAether/Editor/GPU viewport. Куб рисуется Vulkan pipeline с depth buffer,
perspective camera и простым opaque материалом из выбранной Game.Logic.dll.
CPU pixel buffer отсутствует. Проверяются front/back occlusion, clear/opacity,
aspect fit (Canvas сохраняет пропорции render target),
смещение camera после3Step, resize800×450, suspend/resume320×240 и Replace
на новый material с сохранением tick3/camera/Paused. Сохраняются5PNG и JSON;
пиксельный readback выполняется только для assertions после UI presentation.
Import bridge не читает pixels обратно на CPU.

Воспроизводимый Debug/published Release запуск из `src`:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 `
  -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5a-local `
  -IncludeScene `
  -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin `
  -Scenarios @()
```

Нужны Windows x64 и совместимые Vulkan/OpenGL external-memory profiles; путь к
validation layer можно заменить локальным SDK. Runner сохраняет exact commands,
shader/source/binary SHA256, проверяет5PNG hashes и неизменность inputs после
run. Успех требует cooperative child exit0, zero live resources/handles/mappings/
UI sessions и core/sync errors/warnings0. Это первый renderer proof; gameplay
WASD/mouse/E/Escape проверяются отдельным ENG5b proof выше; комната/HUD и3D
performance baseline остаются последующими этапами.
Shader sources и команда пересборки: [Shaders](../../TrueMoon.Aether/Gpu/Vulkan/Shaders/README.md).

## Предыдущие этапы

Headless Windows x64/net10.0 prototype: один executable с ролями Editor и
Game.Runtime, Cobalt App, fresh owned Mithril ChildProcessUnitHandle на каждый
launch и generated Enerit same-build pipes. ENG2 добавляет одно Alloy/OpenGL
окно с CPU pattern из child через3slot shared-memory ring.

[Единственный engine план](../../docs/engine/PLAN.md),
[статус и границы](../../docs/engine/STATUS.md).

## Проекты и ownership

- [TrueMoon.Aether](../../TrueMoon.Aether/README.md): одна engine assembly с
  contracts, supervisor, runtime protocol, native child adapter и CPU frame transport.
  Namespace contracts не содержит Mithril/Enerit/Alloy/Skia типов; сама библиотека
  использует Enerit/Mithril и не зависит от UI.
- Game.Logic: отдельная DLL с минимальным IGameSession stub, fixed tick counter
  и pattern color, optional минимальный checkpoint camera/interaction/tick.
  Transactional replacement/rollback доступны через AetherHost.Replacement.
- EngineSessionProbe: executable composition Editor/Game.Runtime, конкретная
  Game.Logic factory, Alloy viewport и bounded native/fault scenarios.
- TrueMoon.Aether.Tests: перенесённые xUnit supervisor/protocol/codec checks с
  fake leases и прямой ссылкой на Aether. Solution folder Aether; примеры — Engine.

Editor App сохраняется между Start/Stop, владеет authoring fixture с revision,
Dirty/selection/panels и engine host, подключённым через UseAether. Supervisor/
Frames получаются из App resolver через AetherHost; отдельного manual shutdown
adapter и внешних await using для них нет. Повторные session Stop не завершают App;
App Stop permanently closes engine admission и drains/освобождает module resources.
Сценарий app-stop проверяет этот путь с живым native child. Runtime world не записывается
обратно в authoring state. Начальная готовность требует OS start, Hello и
подтверждения загруженной сцены/Paused; эти отметки видны отдельно в trace.

Каждый launch получает тот же SessionId, новый UInt64 generation, уникальные
endpoint/nonce и новый handle с RestartPolicy.Never/IsControlAppLifetime=false.
Параметры передаются только в environment собственного ProcessStartInfo;
process-wide environment Editor не меняется. Mithril owns launch/kill/reap;
IUnitsController не переиспользуется после Stop. Reflection/borrowed attach и
Process.GetProcessById для получения child ownership не используются.

Supervisor сериализует команды. Stop немедленно ставит fence и отменяет локальный
wait; поздние replies не применяются, прерванная команда не повторяется.
Crash/disconnect переводят сессию в Faulted и запускают cleanup, оставляя App живым.
CleanupFailed сохраняет первичную причину и cleanup error, блокирует Start до
явного успешного Stop/cleanup. Stop acknowledgement не считается process reap.

## Control и deadlines

ENG1 реализует Hello, LoadScene, SetMode, Step, GetStatus и Stop. Runtime применяет
команды последовательно; Step разрешён только Paused и выполняет один tick.
Playing использует fixed-tick timer с target60Hz; performance guarantee не измерена.
ENG2 добавляет ConfigureSurface/SurfaceGeneration acknowledgement и frame/drop
diagnostics. ENG3b добавляет capture/startup restore, ENG3c — Replace/rollback;
InputBatch остаётся следующим отдельным направлением.

Каждый reply проверяется по identity/versions/nonce, sequence, operation/PID и
применённым mode/revision/tick. HostContractId — SHA256 manifest entry host, Aether,
Enerit и Mithril binaries; GameBuildId — SHA256 manifest Game.Logic.dll.
Child независимо сверяет bootstrap с собственными binaries до открытия RPC.

ENG3a добавляет SessionLaunch.Build: GameBuildId берётся из immutable manifest,
выбранный каталог/entry DLL и contract hash проверяются в Editor и child. Только
child создаёт GameBuildLoader/IGameSession; прежний fixed Game.Logic path сохранён
для ENG1/ENG2 regression. Native factory pins файлы до reap. Runtime self guard
теперь ограничивает2минуты без подтверждённых control requests; heartbeat продлевает
его, поэтому живой Editor может собирать candidate в пределах build120s.

## ENG3a: compiler и immutable artifacts

`build-artifacts` в default runner проверяет builds A/B в Debug apphost/published
Release: сборка при живой old session, compile failure/cancel, Stop во время сборки,
явный запуск выбранного build, actual loaded path/hash/color и CPU pixels, отсутствие
game assemblies в Editor CLR, manifest tamper rejection до child launch, file pins
до reap и освобождение после Stop. Это последовательные Start/Stop с выбором build,
не checkpoint/Replace/rollback; authoring fixture сохраняется.

Viewport native-close scenario начинает held compiler job при живом game child.
Closing вызывает terminal AetherHost.DisposeAsync, отменяет/reaps compiler tree,
drains child/frames/pending commands и только потом освобождает UI. Обычная кнопка
Stop продолжает останавливать только game session, сохраняя App.

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -Scenarios build-artifacts
```

Общий runner теперь запускает20headless +1viewport на конфигурацию при IncludeViewport;
все логи/manifest/артефакты сохраняются в отдельном run directory. Для применения
module API и формата game csproj см. [Aether README](../../TrueMoon.Aether/README.md#сборка-и-выбор-игровой-версии--eng3a).
Hello не добавляет schema negotiation; это локальная same-user/same-build модель.

## ENG3b: checkpoint и startup restore

`checkpoint` использует реальный generated IPC и6owned game children. Сохраняет
non-default tick/camera/interaction paused игры, preflights mismatch без остановки
старой session, компилирует выбранный artifact и восстанавливает его на fresh
generation. Первый CPU frame имеет сохранённый tick, recapture совпадает целиком,
дальнейшая симуляция продолжается. Отдельно проверяются explicit reset, подтверждённый
capture rejection с Resume, unknown capture timeout с Faulted, restore rejection
и Stop во время held restore без surface/mapping. Все дети reaped/released,
authoring state сохранён, Game.Logic в Editor не загружен. `checkpoint.bin` и SHA256
в report сохраняют точный payload; restored entry path/build ID проверены в child.

Viewport case `checkpoint-restored-before-first-present` выполняет capture/Stop/
startup restore и ждёт actual presentation; каждый viewport smoke теперь16cases,
26game children и20StartStop. Код UI libraries не менялся. Schema/лимиты/отказы и
публичные overloads описаны в [Aether README](../../TrueMoon.Aether/README.md#checkpoint-и-восстановление--eng3b).
Это отдельные primitives; transaction API описан ниже.

## ENG3c: Replace и rollback

`replacement` собирает4actual artifacts: A/B с разными цветом и дальнейшим camera
step, rejected restore и held restore. Через generated IPC и CPU ring выполняет
Replace A→B с точным checkpoint/first tick и фактическими DLL path/hash/pixels,
candidate failure с возвратом к B/Playing, Stop внутри зависшего game restore и
rollback failure с сохранением обеих причин.8owned game children, max live1,
все reaped/released; authoring state и Editor CLR сохранены. Generated inputs,
manifest/DLL hashes и diagnostics остаются в run directory.

Viewport case `replace-restored-before-first-present` применяет artifact через
публичный transaction API и actual OpenGL presentation. Каждый viewport теперь
17cases/27game children/20StartStop. UI libraries не менялись. Политика cancellation,
error и общего45s budget — в [Aether README](../../TrueMoon.Aether/README.md#замена-игровой-сборки--eng3c).
Editor commands и20Replace cycles при одном живом App/window описаны ниже.

## ENG3d: команды Editor и native proof

Интерактивный `--viewport` использует `AetherHost.Editor` для Start/Play/Pause/Step/
Stop/Build/Replace/Cancel. Строка статуса показывает operation, source revision,
active/candidate build IDs и выбранный project. Refresh source увеличивает input
revision, отменяет pending work и снимает candidate; authoring scene/Dirty/selection
при этом не меняются. Build выбирает candidate без применения, Replace применяет
его явно. Для Start сначала остановите текущую игру.

Можно указать другой trusted local project с entry `Game.Logic.ProbeGame` в
`Game.Logic.dll`, потребляющий frozen AetherContractPath:

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --viewport-project E:/games/Demo/Game.Logic.csproj
# Отдельный ограниченный proof, свежий каталог:
dotnet run --project ManualTests/EngineSessionProbe -- --editor-proof TestResults/EngineSessions/editor-manual
```

`--editor-proof` создаёт одно App/OpenGL окно и собирает A/B с разными цветом и
camera step.20Replace попеременно используют A/B и оба Paused/Playing mode.
Проверяются фактические child DLL path/SHA256, body color/frame/tick в GL readback,
полный runtime checkpoint до mode/frames, первый показанный tick и новый Step.
App/root/window/session identity и scene/Dirty/selection/panels сохраняются.

Отдельно выполняются20failure/interrupt cases: compile error/deadline/cancel,
context change, completed build для другой session, Stop build, missing/tampered
artifact, capture rejection/unknown timeout, candidate Hello/restore/first-frame,
double restore failure, Cancel/context/Stop replace, injected owned-native-lease
Stop failure и native close во время Replace вместе с compiler job. Injection
не означает воспроизведения OS kill failure; explicit retry reaps реальный child.
При native close engine drains до disposal Canvas. Все original/recovery exceptions
сохраняются в `editor-report.json`, вместе с20cycle records, input hashes и children;
`restored-*.json` и `loaded-*.json` дают отдельные runtime evidence, `editor.png` — кадр.

Proof≤300s; Build15s и Replace Total12s/Forward4s/Cleanup3s — уменьшенные limits.
Default45s replacement policy отдельно проверяется прежним `replacement` case.
`RunProbe.ps1 -IncludeViewport` запускает classic viewport и editor-proof отдельно
в каждой конфигурации, плюс20headless scenarios:44cases Debug/published Release.
Это bounded Windows/.NET10/CPU transport proof; 3D/shared GPU/packages/performance
и UI hardware acceptance сохраняют отдельные критерии.

Enerit generator сейчас поддерживает Int64, но не UInt64 scalar. Generation,
sequence и tick в wire DTO передают точный UInt64 bit pattern через Int64;
domain identity/counters остаются UInt64, преобразования unchecked только на
wire boundary, increments checked. Mutable DTOs требуются текущему generator;
scene декодируется и проверяется перед изменением game state, snapshot копирует entities.

Bounded adapter проверяет фактические generated request/response payloads до
передачи/декодирования: ≤64KiB. Scene:≤64distinct entities, IDs≤128characters,
finite coordinates. Общие Enerit/Mithril APIs и central packages не менялись.

Default limits: startup15s, command2s, heartbeat interval1s/не более5s без
подтверждённого ответа, единый graceful stop3s для RPC ack и Mithril stop,
затем owned kill/reap≤5s. Startup cancellation
прерывает ожидание, затем выполняется обязательный owned cleanup. Remote handler
cancellation этим не обещается. Runtime имеет аварийный self deadline2minutes.

## ENG4a: GPU capabilities

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --gpu-capabilities TestResults/EngineSessions/gpu-manual
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities
```

Отдельное Alloy/OpenGL окно и App-owned game child собирают read-only evidence.
Runtime создаёт/уничтожает Vulkan1.1 instance, перечисляет physical devices,
device/driver UUID/LUID, extension names, graphics queues и точные external image/
semaphore properties. Logical device/image/memory/export не создаются. Editor
query выполняется на current GL owner context: UUIDs, extensions, callable
functions, RGBA8 internal format/optimal tiling и fence polling availability.
Новые GL textures, imports/semaphores/fences для этой проверки не создаются;
обычные UI rendering resources принадлежат прежнему window lifecycle.

`GpuTransportProbe` проверяет immutable snapshots и full SessionIdentity из child
report, schema/profile и native requirements. В `gpu-report.json` — Capabilities,
raw Vulkan/GL query evidence, decision/issues, CPU selection/ImportTested=false,
process/UI cleanup. Child report≤64KiB, whole probe45s/runner60s, старые startup/
heartbeat/owned reap limits сохраняются. Диагностический файл — local probe
exchange; control/frame wire v1 не расширяется. Unsupported hardware/query failure
возвращает явные blockers и сохраняет CPU selection; Completed означает корректно
собранный/оценённый report и cleanup, не обязательную GPU eligibility.

Runner сохраняет source/binary/fonts/native fingerprints и добавляет по одному
gpu-capabilities case в Debug apphost и published Release. Вместе с20headless,
classic viewport и Editor proof это46native cases/48commands. Без нового switch
прежний runner scope сохраняется. No driver/Windows settings changes или K7
workaround; actual import/pixels/completion/resize/crash и shared GPU readiness
остаются ENG4b–d. CPU-reference contract и failure/ownership handshake — только
в [engine плане](../../docs/engine/PLAN.md#eng4a-capabilities-и-transport-decision--2026-10-20).

## Запуск и проверки

Из src:

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- repeat TestResults/EngineSessions/manual-repeat
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1
```

Runner создаёт свежий TestResults/EngineSessions/run-* и последовательно выполняет
Debug apphost и published framework-dependent Release без RID. Он сохраняет
точные commands/exit codes, binary/source fingerprints, process stdout/stderr,
per-case report.json и summary.json. Build/publish≤180s, headless scenario≤65s,
опциональный viewport scenario≤180s и отдельный editor-proof≤300s;
при timeout завершается только принадлежащее runner дерево процессов.

Базовые16ENG1scenarios/configuration: repeat20Start/Stop, stale reply после смены generation,
bad Hello/scene acknowledgement, startup timeout/cancel, command timeout/cancel,
Stop при pending command, crash, live-process disconnect, heartbeat timeout,
forced stop/owned kill, зависший Stop RPC с игнорированием graceful shutdown,
launch failure и forced parent exit. Fault modes изолированы
в probe wrapper вокруг обычного RuntimeProtocol; они не являются normal commands.
Ещё4headless cases — app-stop/build-artifacts/checkpoint/replacement; опциональные
UI/capability cases описаны в своих разделах выше.

Parent-exit scenario владеет отдельным parent executable, убивает только его,
ожидает child cleanup marker и read-only OS process census. Census не используется
для управления или kill чужого процесса. Повторный старт после ошибок подтверждает
восстановление под новым generation; во всех живых Editor проверяется authoring fixture.

ENG1 не создаёт viewport, CPU ring, frame codec, compiler/replacement/checkpoint,
3D renderer, независимый package consumer или final resource/performance proof.
UI hardware acceptance и отложенные RichTextEditor улучшения сохраняют свой scope.

## CPU viewport — ENG2

Интерактивное окно с Start/Play/Pause/Step/Stop:

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --viewport
```

Начальная surface1280×720. Receiver работает вне UI owner, UI копирует BGRA8
premultiplied/sRGB pixels через `SkiaImageSource.CopyBgraPixels`. Source buffer
сразу переиспользуется; текущий Skia image принадлежит Canvas и освобождается
на UI owner после его последнего drawing use. UI image/native/GPU storage
учитывается отдельно от двух receiver buffers; VRAM trend этим не измеряется.
Explicit Noto Sans и OFL берутся из existing AlloyTest assets, без system fonts.

Resize coalesces150ms и создаёт fresh mapping/surface generation. Old receiver
сначала drains, затем child подтверждает detach; old mapping закрывается только
после обоих условий. Zero-size/minimize приостанавливает surface, heartbeat
сохраняется. Restore требует fresh surface. Runtime crash/EOF invalidates viewport;
последнее изображение остаётся с отметкой unavailable. Native close сначала
останавливает child/readers, затем закрывает окно. Реализация minimize/restore
не заменяет общий physical K7/DPI/driver acceptance.

Ожидание Start включает Hello/scene/surface/первую реальную presentation в общие15s.
`SessionSupervisor.Status` описывает child mode; готовность viewport отдельно
видна по `FrameDisplay.Presented.Task`. Pause оставляет frame delivery активной,
Step изменяет simulation tick ровно на1. Игра и Editor authoring state раздельны;
Editor читает hash Game.Logic.dll, но не загружает эту assembly.

```powershell
# Full ENG1 regression + ENG2 window in Debug apphost / published Release:
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport
# One bounded window smoke; report directory must be fresh:
dotnet run --project ManualTests/EngineSessionProbe -- --viewport TestResults/EngineSessions/viewport-manual
```

Window smoke проверяет actual OpenGL readback corners/frame+tick counters,
CRC при receiver copy, resize16×16/320×180/1280×720/1920×1080, zero-size/resume,
slow UI/latest pending,20Start/Stop в одном App, crash, EOF при живом child,
first-frame cancellation/15s deadline и WM_CLOSE со своим owned cleanup.
Reports: `viewport-report.json`, `viewport.png`, stdout/stderr; bounded256samples
p50/p95 copy/upload/produce-to-present latency, pending replacements и observed
producer drops. Это наблюдения, не performance baseline или30fps guarantee.
Runner сохраняет source/binary/font/native fingerprints и возвращает failure
при изменении source во время run. Exact frame offsets/limits — только
[в engine плане](../../docs/engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17).

Headless scenarios продолжают работать без окна. Runtime self deadline2minutes
сохраняется как probe guard; интерактивный Editor ограничен1hour. Это ManualTests,
не standalone game или production 3D readiness; versioned logic replacement входит в ENG3 выше.
InputBatch/focus routing войдёт в отдельный следующий engine input scope;
ENG2 кнопки управляют control protocol, не передают игровые input events.

## ENG4b: one-frame Vulkan→OpenGL import

Отдельный bounded probe импортирует actual dedicated image и binary semaphores
из owned runtime. Обычный viewport остаётся CPU. Перед GL queue wait требуется
CPU-observed Vulkan fence; перед return/retirement — GL fence и Vulkan fence.
Полные RGBA images16×16/64×32/1280×720 сравниваются с asymmetric premultiplied
reference. Это GL framebuffer readback, presentation/compositing ещё не проверяются.

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --gpu-import TestResults/EngineSessions/gpu-import-manual
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Directory должен быть fresh; второй command требует доступные validation DLL/JSON
в явно указанной папке. Runner задаёт VK_LAYER_PATH и TRUEMOON_GPU_VALIDATION только
своему process tree, сохраняет hashes обоих файлов. Слой не входит в пакет Aether;
его можно получить из [официального Vulkan SDK](https://vulkan.lunarg.com/sdk/home).
Без параметра imports проверяются без validation и report явно содержит false.
Отсутствующий явно запрошенный слой завершает run ошибкой, не считается passing.

`gpu-import-report.json` содержит14проверок, whole-image SHA256 и8owned children.
Проверяются bad descriptor/stale identity, failed DuplicateHandle, missing GL
entry point, actual driver TexStorageMem error после частичного импорта, early/
stale Ready, cancel/8s deadline/crash до Ready, native WM_CLOSE с live imports.
Семь cooperative child reports включают validation после teardown; deliberate
crash не имеет cooperative teardown report. Все children должны быть reaped,
local NT handles/GPU resources/UI sessions —0. Крайнюю остановку driver hang
managed cancellation не гарантирует. Slots/reuse/resize/restart — ENG4c,
обычный viewport/цветопреобразование/compositing — ENG4d.

## ENG4c: reusable GPU slots и surface lifecycle

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --gpu-surface TestResults/EngineSessions/gpu-surface-manual
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Fresh directory, Windows x64 и actual eligible GL/Vulkan device обязательны.
Validation layer directory содержит DLL/JSON; runner fingerprints и задаёт
environment только своему process tree. Без явно выбранного слоя report содержит
ValidationRequested=false; это не validation pass. Native outer deadline200s,
внутренний proof180s, pipe command8s; произвольный driver hang ими не прерывается.

`gpu-surface-report.json`:21проверка/80проверенных whole-image кадров/12owned
children. Три persistent dedicated slots делят один Vulkan device/queue. Проверяются
exhaustion без overwrite,60повторных кадров с неизменными allocation offers,
возврат2/0/1, stale frame/surface/session, resize с3Ready frames,0×0 suspend/resume,
invalid extent, repeated resize с bounded local resources. Queued resize ждёт held
Produce; новые Produce/Configure отклоняются до завершения принятой операции.

Stop проверяется с Ready/read-complete slots; injected owner cleanup failure
сохраняет imports и блокирует новый surface session до retry. Cancel/8s deadline,
crash до Ready/после Ready/после GL use и fresh restart проверяются отдельно.
Native WM_CLOSE отменяет held Produce с live read-complete import и ждёт local
GPU cleanup до UI disposal. Девять cooperative teardown reports подтверждают
validation и actual create/release counters:15surfaces/devices,45slots;3deliberate
crashes подтверждаются owned reap, без cooperative teardown claim. Остатки parent
GPU resources/NT handles/UI sessions0, authoring unchanged.

Это diagnostic readback scope. Surface Dispose освобождает GPU ownership; process
Stop/reap остаётся у Editor/Supervisor. Ordinary GameEditor viewport всё ещё CPU;
отдельный actual GPU bridge proof описан ниже. Device loss/driver hang,
VRAM trend, performance и UI hardware acceptance этим probe не доказаны.

## ENG4d: actual GPU viewport bridge

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --gpu-viewport TestResults/EngineSessions/gpu-viewport-manual
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Те же reference Windows x64/device/validation prerequisites и fresh directory.
`gpu-viewport-report.json`:17checks/14presented captures/20GPU copies/6owned children.
Доставка: Produce → UseTextureAsync callback → SkiaOpenGLSurface.CopyTexture →
observed GL completion → Return/Vulkan retirement. Callback loan нельзя сохранять;
retained UI image независимо от imported slot, освобождается на его UI owner.
CPU readback импортированных textures инструментирован и должен остаться0.

Actual FramePresented предшествует проверке whole canvas pixels: asymmetric
reference formula, premultiplied alpha над RGB background, stored sRGB, top-left,
bilinear scale по actual viewport DPI и clipping. PNG/hash и full stamps сохраняются.
Кадр переживает slot reuse, surface resize/suspend, runtime Stop/crash; новая
runtime generation отвергает старый stamp. Return во время callback отклоняется,
consumer exception после submitted copy fences session и drains безопасно;
cancel перед owner dispatch исключает поздний callback. WM_CLOSE с live retained
GPU image освобождает Aether imports до UI disposal.

Пять cooperative teardown reports включают actual create/release counters и
core/sync validation, deliberate crash имеет только owned reap. Probe deadline150s,
runner180s; managed token не прерывает driver hang. Проверка читает retained UI
surface после presentation, не compositor/physical display. Это GPU copy reference
bridge; обычные GameEditor Build/Replace/checkpoint/first-present commands пока
используют CPU transport по умолчанию. Explicit GPU подключение — ниже.

## ENG4d2: GPU в обычном Editor

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- --viewport-gpu
dotnet run --project ManualTests/EngineSessionProbe -- --viewport-gpu-project ManualTests/Game.Logic/Game.Logic.csproj
dotnet run --project ManualTests/EngineSessionProbe -- --editor-gpu-proof TestResults/EngineSessions/editor-gpu-manual
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Обычный ViewportView и Start/Play/Pause/Step/Build/Replace/Cancel/Stop теперь могут
явно выбрать GPU. UseAether.ViewportTransport factory создаёт lazy strategy;
FrameSession сохраняет общий process/checkpoint/rollback lifecycle. Binding
EditorGpuViewportBinding получает borrowed GL context, copies в Skia GPU image и
хранит ViewportFrame ticket. Только actual FramePresented подтверждает его через
FrameSession; Stop/resize вправе отклонить устаревший ticket. CPU mode/default
сохраняется, автоматического fallback после fault нет.

Нормальный runtime предоставляет lazy GPU channel с frameProvider из atomic
loaded/restored selected game's tick/color. Reference pixels генерируются CPU и
загружаются через Vulkan staging; полноценный 3D renderer этим шагом не добавлен.
Pixels не идут через pipe, imported ReadPixels delivery отсутствует.

`editor-report.json` в GPU mode повторяет20A/B replacements в одном App/window,
20failure cases и53owned children, loaded DLL/manifest hashes, checkpoint/first
tick/next Step behavior. Дополнительно resize64×32,0×0suspend,resume160×90 и stale
ticket rejection; native close drains pending compiler/Replace до UI disposal.
Все53cooperative surface reports сверяются:47devices/141slots created/released,
core/sync validation0/0, local GPU resources/NT handles/mappings/UI sessions0.
Report содержит Transport/GpuCopies/ImportReadbacks. GPU mode проверяет actual
retained UI pixels; package consumers отдельно проверяют managed transport API.
Native proof budget300s/runner330s; driver hang/physical display/VRAM/performance
не получают passing результата из этих pixel/lifecycle checks.

## ENG4e: измеренный CPU/GPU viewport baseline

```powershell
# Полный experiment: Debug apphost и published Release,3повтора обоих modes.
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunViewportBaseline.ps1

# Короткая проверка самого инструмента; результаты не считать полным baseline.
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunViewportBaseline.ps1 -Configurations Debug -Repetitions 1 -WarmupSeconds 1 -MeasurementSeconds 1
```

RunDirectory должен быть новым каталогом внутри `TestResults/ViewportBaseline`.
Runner сначала сохраняет protocol/settings, затем собирает Debug/publishes Release
и последовательно запускает CPU/GPU, чередуя порядок пар между повторами.
Каждый процесс использует один App/Editor/window: Start640×360, Resize1280×720,
Replace1280×720, Stop. A/B artifacts имеют разные revisions/build identities,
но одинаковые DLL bytes и pattern/color; compilation вынесена из measured path.
Целевые cadence: simulation60Hz, producer30Hz, continuous UI60Hz. Каждый участок
проверяет реальные retained pixels, прогревается2s и измеряется5s. Pixel readback
и запись JSON не входят в measured interval. Native validation выключена только
для measurement processes; validation correctness checks запускаются отдельно.

Raw `baseline-editor.json` и `baseline-runtime-{generation}.json` содержат bounded
opt-in EventSource stages и cumulative process counters каждые200ms. Они включают
instrumentation overhead. Один frame идентифицируется session/runtime/surface/frame;
повторная presentation не увеличивает FPS. CPU/GPU wire ProducedTimestamp относятся
к разным моментам, поэтому общий latency рассчитывается от traced production start
до фактического FramePresented. Это CPU-observed path, не physical-display latency.
CPU production включает generation/CRC/MMF write; CPU copy — read/CRC. GPU production
включает CPU generation/Vulkan staging/ready fence; retained-copy измеряет submission
независимого Skia image, gpu-use — owner queue/use/observed GL fence. Import timings
учитываются отдельно как configuration cost. GPU query/utilization/VRAM без sampler
обозначены unavailable; private bytes не являются VRAM.

`baseline-report.json`: p50/p95/max всех samples интервала, production cohort
produced/received/distinct presented, producer-dropped attempts и unreceived/
unpresented (пересекающиеся величины), Start/Resize/Replace/Stop timings,
allocation rates, CPU cores, private/working-set bytes, handles и GC collections.
Resource deltas имеют точные границы внутреннего polling interval. Отсутствующие
timings имеют Samples0/null, а не измеренное значение0. `REPORT.md` сводит всю
матрицу; `summary.json` сохраняет exact commands, environment/driver metadata и
source/binary hashes, отклоняет overflow/broken traces/source changes/missing
children/failed cleanup. Завершённость experiment отделена от advisory budgets:
≥24fps, latency p95≤100ms, Editor/runtime allocation≤32/256MiB/s и lifecycle≤15s.
Budgets записаны до запуска и не подгоняются под результат.

Сравнение относится к reference CPU-generated/Vulkan-staging workload. 3D renderer,
long resource trend/VRAM, physical DPI/loss и K7/final UI acceptance остаются
отдельными задачами; driver/Windows settings этим инструментом не меняются.
