# EngineSessionProbe — ENG1 / ENG2

Headless Windows x64/net10.0 prototype: один executable с ролями Editor и
Game.Runtime, Cobalt App, fresh owned Mithril ChildProcessUnitHandle на каждый
launch и generated Enerit same-build pipes. ENG2 добавляет одно Alloy/OpenGL
окно с CPU pattern из child через3slot shared-memory ring.

[Единственный engine план](../../docs/engine/PLAN.md),
[статус и границы](../../docs/engine/STATUS.md).

## Проекты и ownership

- Engine.Probe.Contracts: domain identity, validated scene snapshot, mutable wire
  DTOs и IEngineControl. Без Mithril/Enerit/Alloy/Skia references.
- Game.Logic: отдельная DLL с минимальным IGameSession stub, fixed tick counter
  и pattern color. Checkpoint/restore/versioned replacement относятся к ENG3.
- EngineSessionProbe: supervisor, child/bootstrap/runtime, transport adapter и
  bounded native scenarios. Эти типы пока не production engine API/packages.
- EngineSessionProbe.Tests: xUnit supervisor/protocol/codec checks с fake leases;
  зарегистрирован вместе с probe projects в TrueMoon.slnx, папка Engine.

Editor App сохраняется между Start/Stop, владеет authoring fixture с revision,
Dirty/selection/panels и shutdown hook supervisor. Runtime world не записывается
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
diagnostics. InputBatch/checkpoints/replacement ещё не реализованы.

Каждый reply проверяется по identity/versions/nonce, sequence, operation/PID и
применённым mode/revision/tick. HostContractId — SHA256 manifest host, contracts,
Enerit и Mithril binaries; GameBuildId — SHA256 manifest Game.Logic.dll.
Child независимо сверяет bootstrap с собственными binaries до открытия RPC.
Hello не добавляет schema negotiation; это локальная same-user/same-build модель.

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

## Запуск и проверки

Из src:

```powershell
dotnet run --project ManualTests/EngineSessionProbe -- repeat TestResults/EngineSessions/manual-repeat
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Debug
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1
```

Runner создаёт свежий TestResults/EngineSessions/run-* и последовательно выполняет
Debug apphost и published framework-dependent Release без RID. Он сохраняет
точные commands/exit codes, binary/source fingerprints, process stdout/stderr,
per-case report.json и summary.json. Build/publish≤180s, headless scenario≤65s,
опциональный viewport scenario≤180s;
при timeout завершается только принадлежащее runner дерево процессов.

16scenarios/configuration: repeat20Start/Stop, stale reply после смены generation,
bad Hello/scene acknowledgement, startup timeout/cancel, command timeout/cancel,
Stop при pending command, crash, live-process disconnect, heartbeat timeout,
forced stop/owned kill, зависший Stop RPC с игнорированием graceful shutdown,
launch failure и forced parent exit. Fault modes изолированы
в probe wrapper вокруг обычного RuntimeProtocol; они не являются normal commands.

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
не production engine packages, standalone game или versioned logic replacement.
InputBatch/focus routing войдёт в отдельный следующий engine input scope;
ENG2 кнопки управляют control protocol, не передают игровые input events.
