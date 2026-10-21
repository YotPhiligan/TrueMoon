# TrueMoon.Aether

Экспериментальный модуль игрового движка TrueMoon. Одна библиотека содержит
contracts игровой сессии, supervisor, control protocol, CPU frame transport и
явный one-frame Vulkan/OpenGL import probe.
Текущая реализация рассчитана на Windows x64 и .NET 10.

[План](../docs/engine/PLAN.md), [статус и ограничения](../docs/engine/STATUS.md),
[пример Editor/Game.Runtime](../ManualTests/EngineSessionProbe/README.md).

## Состав

| Область | Назначение |
| --- | --- |
| `TrueMoon.Aether.Sessions` | Session identity, `IGameSession` и session lifecycle |
| `TrueMoon.Aether.Scenes` | Scene snapshots и entities |
| `TrueMoon.Aether.Transport` | Control DTOs, `IEngineControl` и adapters |
| `TrueMoon.Aether.Checkpoints` | Checkpoint state, capability, outcomes и codec |
| `SessionSupervisor` / `RuntimeProtocol` | Serialized commands, readiness, generations, heartbeat, stop/reap и observable failures |
| `SharedFrameRing` / `FrameProducer` / `FrameReceiver` | Bounded Windows shared-memory transport, validated frames и latest-frame receiver |
| `FrameSession` | Surface generations, first-present readiness, resize, drain и frame ownership |
| `NativeRuntimeFactory` / `BuildIdentity` | Owned Mithril child, Enerit client, bootstrap и same-build fingerprints |
| `GpuResourceHost` / `VulkanSharedFrame` / `OpenGlSharedFrame` | App-owned one-frame export/import, observed completion и retryable cleanup |

Contracts и реализация находятся в `TrueMoon.Aether.dll`. Модуль использует
Contracts/Enerit/Mithril и Silk.NET.Vulkan2.23.0; зависимостей на Alloy/Skia нет. Enerit generator
создаёт control client/server adapters при сборке самого Aether.

## Подключение

```xml
<ProjectReference Include="../TrueMoon.Aether/TrueMoon.Aether.csproj" />
```

Основные namespaces — `TrueMoon.Aether` (App extension), `TrueMoon.Aether.Hosting`,
`TrueMoon.Aether.Sessions`, `.Scenes`, `.Transport`, `.Checkpoints` и `.Frames`.
Editor владеет supervisor и receiver; child владеет игровой сессией и producer.
Host executable выбирает роли и создаёт конкретную реализацию `IGameSession`.
Готовый образец такой composition находится в `ManualTests/EngineSessionProbe`.

`ManualTests/Game.Logic` остаётся отдельной игровой DLL, подключённой к Aether.
Её нельзя объединять с engine assembly: смена игровой логики при сохранении
host/contracts является целью ENG3. Исполняемый пример и `TrueMoon.Aether.Tests`
также имеют отдельные сборки.

## Текущие границы

ENG1/ENG2 реализуют session lifecycle и CPU viewport; ENG3a добавляет compiler jobs
и явный выбор immutable game artifact, ENG3b — минимальный checkpoint/restore,
ENG3c — serialized Replace с однократным rollback, ENG3d — Editor commands/20Replace proof.
ENG4 добавляет shared GPU transport, ENG5a — первый Vulkan3D draw (куб,
perspective camera, depth buffer и простой opaque material). Снимок сцены —
optional `Rendering.IRenderGameSession`; `RuntimeProtocol.CaptureRenderFrame`
захватывает его и tick под simulation/checkpoint lock. `GpuFrameData.FromScene`
передаёт draw workload без CPU pixel buffer в существующий App-owned producer.
ENG5b добавляет optional `Input.IInputGameSession`, bounded `GameInputState` и
`Supervisor.SendInputAsync(expectedIdentity, state)`: input staged на command,
applied на fixed tick/Step. Камера/interaction сохраняются checkpoint, held/look/
actions transient; Pause/Stop/restore/host surface reset очищают input. Control
wire v2 требует rebuild matching host/game; frame/TMCK schemas остаются v1.
Комната, HUD и Hot Reload остаются дальнейшими этапами.
UI OpenGL viewport живёт в примере и подключает Alloy явно.
Новый namespace не добавляет поддержку других платформ или release readiness.

Типы пока экспериментальные; проект packable, публикация NuGet отдельно.
Metadata и самостоятельный [package README](PACKAGE.md) включены в local nupkg.
Сохраняются same-build/local same-user trust, thread/lease ownership и bounded
cleanup. Failed reap блокирует следующий launch до успешного cleanup.

## Подключение к App

```csharp
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;

await using var app = App.Build(context => context.UseAether(options =>
{
    options.HostContractId = BuildIdentity.HostId;
    options.GameBuildId = BuildIdentity.GameId;
    options.ReportDirectory = "engine-reports";
}));
await app.StartAsync();
var engine = app.Services.Resolve<AetherHost>();
await engine.Supervisor.StartAsync(new SceneSnapshot("demo", 0, []));
await engine.Supervisor.StopAsync();
await app.StopAsync();
```

Этот native пример требует entry executable с child-role/bootstrap handler,
как в EngineSessionProbe, и matching Game.Logic.dll. Сам UseAether регистрирует
App-owned services/lifecycle, не генерирует runtime entry point и не запускает игру.
Свой IRuntimeFactory задаётся через options.RuntimeFactory; callback должен вернуть
новый App-owned instance, а зависимости может получать из resolver. Factory/host
имеют одну регистрацию владельца; raw Supervisor/Frames не регистрируются aliases.

Options валидируются и копируются во время UseAether. Повторное подключение или
отдельная регистрация IRuntimeFactory отвергаются, чтобы не создать два владельца.
App Stop/Dispose permanently closes admission, завершает frames и supervisor;
Start после этого запрещён. Ошибки cleanup наблюдаемы, host Dispose допускает
явный retry после отказа. UI выбирает приложение и drains своё work до освобождения
зависимых canvas/native resources. Headless и viewport examples используют UseAether.

## Сборка и выбор игровой версии — ENG3a

После UseAether получите AetherHost. Его `Builds` работает независимо от текущей
игровой сессии; успешный результат не запускается и не применяется автоматически.

```csharp
using TrueMoon.Aether.Builds;

var candidate = await engine.Builds.BuildAsync(new GameBuildRequest(
    "Game.Logic/Game.Logic.csproj", "Game.Logic.dll", "Game.Logic.ProbeGame",
    SourceRevision: 12), cancellationToken);

// Явный выбор следующего запуска с reset из документа.
await engine.Supervisor.StopAsync();
await engine.Supervisor.StartAsync(scene, candidate, token: cancellationToken);
```

Для viewport существует `engine.Frames.StartAsync(scene, candidate, width, height)`
с прежним ожиданием first-present. Stop/reap предыдущей session остаётся обязательным.
Caller сопоставляет SourceRevision с актуальными изменениями перед явным выбором;
поздний результат сборки сам не возобновляет остановленную игру.

Game project — доверенная локальная net10.0 library; он должен использовать
переданный compiler свойством MSBuild `AetherContractPath` как non-copying reference:

```xml
<Reference Include="TrueMoon.Aether">
  <HintPath>$(AetherContractPath)</HintPath>
  <Private>false</Private>
</Reference>
```

См. conditional ProjectReference/Reference в ManualTests/Game.Logic. Compiler
отключает ProjectReference builds и shared build servers, направляет output/obj
в собственный job directory. Он не пересобирает host. Неподключённые dependencies
предварительно собирает caller; GameBuildRequest.Dependencies задаёт точные имена
private DLL, копируемых project output. Native DLL/assets/resolver probing вне scope.

`BuildDirectory` по умолчанию — ReportDirectory/builds, `BuildTimeout` —120s
(допустимо больше0 и не больше120s); options фиксируются при UseAether. Один
compiler job на App, отдельный от session gate. Manifest≤64KiB,1..16managed DLL,
каждая≤64MiB/суммарно≤128MiB; SHA256 manifest задаёт BuildId. Имена/полный набор
DLL/managed references/host и общий Aether contract hash проверяются до публикации
и повторно перед launch. Неполный output не публикуется, active artifacts не
перезаписываются и автоматически не удаляются. Trusted local MSBuild и game code
не изолированы как недоверенный код.

`GameBuildException` содержит exit code и каталог build.stdout.log/build.stderr.log;
каждый поток сохраняет до1Mi characters, остальное drain без накопления. Cancel
возвращает OperationCanceledException, deadline — TimeoutException после owned
kill/reap и staging cleanup. App Stop/Dispose отменяет compiler параллельно с engine
cleanup; ошибки cleanup наблюдаемы и допускают явный retry. Editor example drains
compiler/child/readers до освобождения UI. Артефакт pinned до child reap.

Child-role handler для выбранного SessionLaunch.Build вызывает GameBuildLoader.Load,
использует его Session и освобождает loader после workers. Loader допускает только
declared private assemblies, framework и один общий Aether contract. Контексты имеют
[отдельную область типов, а unload кооперативен](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext);
окончательная граница освобождения native runtime — завершение дочернего процесса.
Legacy fixed GameBuildId launch сохранён для существующих consumers.

## Checkpoint и восстановление — ENG3b

Игра может реализовать `ICheckpointGameSession` в дополнение к `IGameSession`.
Минимальная схема v1 содержит UInt64 tick, camera position/Euler radians и interaction
flag. Capture должен только читать состояние; Restore валидирует всё до атомарного
применения либо бросает `CheckpointRejectedException(CheckpointOutcome.RestoreRejected)`
без изменения игры. Неожиданная ошибка callback имеет неизвестный исход.

```csharp
await engine.Supervisor.SetPlayingAsync(false);
var checkpoint = await engine.Supervisor.CaptureCheckpointAsync(cancellationToken);
checkpoint.ValidateFor(BuildIdentity.HostId, scene); // До остановки прежней игры.
await engine.Supervisor.StopAsync();
await engine.Supervisor.StartAsync(scene, candidate, checkpoint, token: cancellationToken);
```

Для viewport используйте `engine.Frames.StopAsync()` и
`engine.Frames.StartAsync(scene, candidate, checkpoint, width, height)`; startup
завершится после presentation восстановленного кадра. Child-role handler передаёт
`SessionLaunch.RequireRestore` в RuntimeProtocol: после LoadScene выполняется restore,
затем разрешаются mode, surface и кадры. Startup budget15s включает restore и
first-present. Overloads без artifact восстанавливают fixed build; без checkpoint
выполняется явный reset из scene. Живую session восстановить на месте нельзя.

`GameCheckpointCodec` создаёт detached TMCK v1 payload с SHA256 integrity, strict
UTF8 и пределом64KiB. Host contract, scene ID и revision должны совпадать; source
build — provenance и может отличаться от новой совместимой сборки. Неизвестная
схема, NaN/Infinity, повреждение и несовместимость отвергаются до game restore.
Можно сохранить/прочитать bytes через Encode/Decode; произвольный object graph и
миграция между схемами не поддержаны.

Capture допускается только в Paused. Подтверждённые sequenced ответы Unsupported/
CaptureRejected возвращают `CheckpointRejectedException` и сохраняют paused runtime,
heartbeat и возможность Resume. Timeout, disconnect, malformed reply или callback
с неизвестным исходом переводят session в Faulted и запускают cleanup. Отказ restore
также завершает новый candidate. Отдельные шаги capture/start доступны независимо
от transaction API ниже.

## Замена игровой сборки — ENG3c

App-owned `engine.Replacement` использует те же Supervisor/Frames. Caller выбирает
готовый artifact и передаёт текущие session identity, immutable scene и source-input
revision. Поздний build result сам не применяется.

```csharp
using TrueMoon.Aether.Sessions;

var request = new GameReplacementRequest(candidate,
    engine.Supervisor.Status.Identity!, scene, currentSourceRevision);
var result = await engine.Replacement.ReplaceAsync(request, cancellationToken);
```

До Pause проверяются target identity/source revision, scene ID/revision и оба
artifact manifests; pins действуют до завершения transaction. Затем выполняются
Pause/capture → stop/reap → selected build start/restore → first-present при наличии
viewport → прежний Playing/Paused mode. Headless запуск ждёт control readiness.
Существующий Resize сначала завершается; во время reservation новые Start/Step/
mode/capture/Resize и конкурирующая Replace отвергаются. UI продолжает получать и
показывать кадры, чтобы first-present мог завершиться. При suspended surface сначала
возобновите её; Replace отвергает этот случай до Pause.

Candidate failure допускает один rollback к прежнему artifact или fixed build,
с тем же snapshot и fresh generation/nonce. `GameReplacementException` сохраняет
исходную ошибку в InnerException, Phase, RollbackAttempted/RollbackSucceeded и
отдельную RecoveryError. Исключение возвращается и при успешном rollback, когда
прежняя игра уже восстановлена. Подтверждённый capture rejection сохраняет прежний
процесс/кадр и восстанавливает mode; неизвестный исход сохраняет Faulted/cleanup.

Caller cancellation до Stop сохраняет здоровую old session; после Stop выполняется
попытка recovery без caller token. Явные Supervisor/Frames.Stop и App close отменяют
и recovery, исключают поздний launch и drains принятый Replace перед disposal.
Непринятая reservation, отменённая во время ожидания gates, возвращает обычную
OperationCanceledException; ошибки принятой transaction обёрнуты в GameReplacementException.

`AetherOptions.ReplacementDeadlines` фиксируется при UseAether: Total45s,
Forward18s, Cleanup10s. Total начинается при принятии reservation после текущей
frame operation. Forward включает всю первую попытку; оставшиеся27s дают cleanup≤10s,
startup/first-present≤15s и mode≤2s. Control/startup/recovery waits используют общий
budget; сохраняются bounded local artifact validation и frame-lease drain.
Total допустим больше0 и≤45s; Forward≤18s/Cleanup≤10s положительны и их сумма меньшеTotal.
Можно уменьшить limits. Deadline не подтверждает reap: незавершённый cleanup task/
lease сохраняется в CleanupFailed, новый launch запрещён до явного Stop retry.
Позднее завершение cleanup само не запускает игру и не снимает failure.

## Команды Editor — ENG3d

`engine.Editor` — заимствованный App-owned controller без зависимости от UI.
Он использует те же Builds/Replacement/Supervisor/Frames; отдельно регистрировать
или освобождать его не нужно. Установите immutable authoring context:

```csharp
using TrueMoon.Aether.Editing;

engine.Editor.SetContext(new GameEditorContext(
    new GameBuildRequest(project, "Game.Logic.dll", "Game.Logic.ProbeGame", sourceRevision),
    scene, selectedEntityId));
var candidate = await engine.Editor.BuildAsync(cancellationToken);
var result = await engine.Editor.ReplaceAsync(cancellationToken);
```

Build только выбирает готовый candidate. `SelectBuildAsync(artifact)` позволяет
явно переиспользовать ранее собранную версию с подходящими source revision/entry
point/host. Каждый SetContext копирует dependencies, меняет ContextVersion,
отменяет pending command и снимает candidate. Completion дополнительно проверяет
исходные session identity и Stop version, поэтому результат для другой сессии
не становится выбором. Artifact после такой отмены может остаться на диске.

`StartAsync(width,height)` запускает остановленную сцену с selected artifact или
fixed build при отсутствии выбора; ждёт first-present и сбрасывает candidate.
`PlayAsync`, `PauseAsync`, `StepAsync` сериализованы с Build/Select/Start/Replace.
Конкурирующая команда отвергается до завершения текущей. `CancelAsync` отменяет и
наблюдает текущую работу; Replace сохраняет свою recovery policy. `StopAsync`
сначала fences recovery, инвалидирует candidate, затем drains frames и принятую
команду/compiler. Одновременный Stop разделяет completion, failed cleanup допускает
явный retry. App close permanently closes admission до disposal зависимостей.

`Status` содержит ContextVersion/Context/Operation/Candidate и полный Exception
последней команды, включая исходную/recovery errors у GameReplacementException.
Stop сохраняет эту ошибку; новая команда/context очищает её. Raw session Stop или
смена generation также делают выбор устаревшим. Authoring document/Dirty/selection/
panels остаются собственностью Editor и не меняются контроллером.

[Viewport с Build/Replace/Cancel и native proof](../ManualTests/EngineSessionProbe/README.md#eng3d-команды-editor-и-native-proof)
проверяет20A/B Replace в одном App/window, actual DLL hashes/GPU body pixels,
checkpoint/first tick/новый Step и failure/cleanup matrix.

## GPU capabilities — ENG4a

`TrueMoon.Aether.Gpu` содержит pure capabilities snapshots и
`GpuTransportProbe.Evaluate(expectedIdentity, producer, consumer, width, height)`.
Snapshots копируют adapter lists; полный session stamp, schema/profile1,
Windows x64, device/driver UUID, Vulkan image/memory/semaphore и актуальные GL
requirements проверяются до eligibility. Результат содержит ProducerIndex,
DedicatedAllocation и structured Issues; Eligible разрешает только следующий
явный import experiment. Он не создаёт resources и не переключает CPU transport.
Это stateless utility, отдельная App registration/lifecycle не требуется.

Profile1: Vulkan1.1/variant0, RGBA8_UNORM/2D/optimal/single-sample,
ColorAttachment|Sampled|TransferSrc|TransferDst и OPAQUE_WIN32 NT handles для memory/
binary semaphores. Vulkan exporter требует Exportable и compatible opaque handle;
Importable диагностируется, importer — OpenGL. GL проверяет обе external-object
и win32 extensions, entry points, RGBA8/optimal tiling и fence polling.
Недостаточные queries/extent/minimum bytes, mismatched identity/version/profile,
UUID/driver и multiple GL devices дают явные blockers. Даже положительный результат
не доказывает actual allocation/import, pixel correctness или GPU completion.

[Native read-only probe](../ManualTests/EngineSessionProbe/README.md#eng4a-gpu-capabilities)
собирает Vulkan evidence в App-owned child и GL evidence на UI owner. Только этот
example собирает queries. В ENG4b Aether дополнительно использует Silk.NET.Vulkan
2.23.0 для явного native probe; UI/Skia dependencies отсутствуют. Control/frame
wire v1 не меняется. Повторяемый GPU transport относится к ENG4c–d по
[контракту](../docs/engine/PLAN.md#eng4a-capabilities-и-transport-decision--2026-10-20).

## One-frame GPU import — ENG4b

`AetherHost.Gpu` — borrowed App-owned `GpuResourceHost`. Startup остаётся lazy;
App shutdown closes admission и drains GPU workers/resources перед session/UI
cleanup. Не регистрируйте disposable aliases. Failed cleanup сохраняет resource
ownership и `CleanupError` для explicit Dispose retry. `Own(IGpuResource)` применяет
тот же lifecycle к дополнительным ресурсам.

Windows x64 probe использует `RunProducerProbeAsync(launch, token)` в owned child
и `ConnectAsync(NativeRuntimeLease)` в Editor. Отдельный CurrentUserOnly pipe
ограничен64KiB и проверяет full identity/sequence/schema. `CreateAsync` создаёт
dedicated RGBA8/optimal allocation и две binary semaphores без GPU submission.
`Import` дублирует NT handles через принадлежащий lease process handle, создаёт
GL imports на borrowed `GpuOpenGlContext` и сразу закрывает локальные NT copies.
Контекст и его owner dispatcher должны оставаться живы до успешного cleanup.

Порядок: Import → `ImportedAsync` → `ProduceAsync` (Vulkan fence observed) →
owner `Read(ready)` → owner `PollCompletion()` до non-null → `ReturnAsync`
(Vulkan retirement fence observed) → imports/connection Dispose. `Read` делает
полный GL framebuffer readback для proof; это не production presentation path.
Early/stale/unconfirmed completions запрещены до enqueue GPU wait. Ожидание GL
fence — неблокирующие polls с timeout0; unknown GPU use запрещает освобождение.
Cancel/timeout/transport failure закрывают connection без повторного применения.

16×16/64×32/1280×720 сравниваются целиком с reference: RGBA channels, разные строки,
stored sRGB/premultiplied bytes. Это проверка сохранности bytes; compositing и
цветопреобразование окна остаются ENG4d. Native probe также проверяет partial
import failure, failed duplication, cancel/deadline/crash до Ready и WM_CLOSE
с live imports. Validation включается явно через runner и local layer directory.
CPU viewport остаётся default; slots/resize/reuse/restart — ENG4c. Нет утверждений
о 3D, performance, driver isolation или hard deadline внутри driver call.

## Reusable GPU surfaces — ENG4c

`TrueMoon.Aether.Gpu.Surfaces.GpuSurfaceSession` получается через
`await host.Gpu.ConnectSurfaceAsync(ownedRuntime, adapter, borrowedGlContext)`.
На App разрешён один unreleased surface session; failed cleanup блокирует новый.
`ConfigureAsync(width, height)` создаёт три persistent slots на одном Vulkan
device/queue, с отдельными dedicated image/memory и binary semaphores на slot.

Для каждого кадра вызывайте `ProduceAsync(slot)` → `ReadAsync(ready)` →
`ReturnAsync(readback.Completion)`. В ENG4c Read делает diagnostic GL readback и
nonblocking fence polling. До Return/Vulkan Retire slot занят; допускаются три
outstanding Ready frames и произвольный порядок их возврата. Busy slot и concurrent
commands отклоняются. FrameId возрастает отдельно у каждого slot; Version2/full
session identity/SurfaceGeneration/Slot/FrameId проверяются до foreign GPU wait.
V1 one-frame descriptor/API сохраняет прежнюю строгую семантику.

Configure fences новые команды, ждёт принятую операцию, drains Ready/read-complete
slots, удаляет старые GL imports, подтверждает exporter detach и только затем
создаёт новую generation. `ConfigureAsync(0, 0)` suspends без allocation; resume
также создаёт новую generation. Неправильный extent отклоняется до изменения surface.
Параллельный Configure отклоняется; resize coalescing остаётся задачей consumer.

`await session.DisposeAsync()` немедленно fences admission, закрывает pipe и drains
accepted work/local GPU use. Failed cleanup сохраняет imports/error для explicit
retry; успешно released session можно заменить новым. Затем вызывайте runtime
Stop через Editor/Supervisor: GPU session заимствует lease и не владеет process
Stop/reap. App автоматически закрывает GPU до child/UI. Runtime exit немедленно
fences дальнейшие операции; retained imports освобождаются explicit session/App
cleanup. Restart требует fresh owned lease/full identity, FrameId/SurfaceGeneration
старого lease не применяются. Borrowed owner dispatcher/context живут до cleanup.

Runtime запускает `host.Gpu.RunSurfaceProducerAsync(launch, token, ..., validation)`
внутри своего UseAether/App lifecycle. Отдельный CurrentUserOnly pipe ограничен64KiB,
Connect3s/command8s, strict full stamps/sequence/schema. LastSurfaceProducer содержит
bounded diagnostics и actual create/release counters для surfaces/slots/devices.
Managed deadline не прерывает driver hang или произвольный borrowed dispatcher.
Ordinary GameEditor viewport CPU default; GPU bridge описан ниже.

## GPU viewport bridge — ENG4d

`UseTextureAsync(ready, consume)` заменяет diagnostic `ReadAsync` для доставки
кадра. Callback вызывается синхронно на borrowed GL owner после ready semaphore
wait. `GpuOpenGlTexture` предоставляет Name/Width/Height/Stamp только внутри
callback, на owner и пока session доступен. Native name нельзя сохранять или
использовать в отложенных draw commands. Все renderer commands должны быть submitted
до возврата callback, в том числе при исключении. Aether затем signals return и
observes local GL fence; `ReturnAsync(completion)` отдельно подтверждает Vulkan
retirement. Callback failure/cancel fences session; cleanup сохраняет ownership
при unconfirmed use. Configure drains Ready frames без CPU readback.

Для retained UI используйте `SkiaOpenGLSurface.CopyTexture(name, width, height)`
из optional Rendering.Skia: это одна GPU copy в независимое image; Aether не
зависит от UI. Скопированное image можно хранить после slot reuse/resize/runtime
Stop, рисовать и освобождать на исходном UI surface/context owner до UI teardown.
Профиль: RGBA8 premultiplied, stored sRGB, top-left. GPU completion не является
first-present; UI подтверждает текущий stamp после actual `FramePresented`.

Explicit `--gpu-viewport` example проверяет actual draw/compositing и lifetime.
Это одна GPU copy, не zero-copy presentation. CPU остаётся default режимом.
Hardware/driver loss, VRAM и performance требуют собственных проверок.

## Выбор transport в GameEditor — ENG4d2

`AetherOptions.ViewportTransport` — optional factory App-owned strategy. Null
сохраняет built-in CPU; для GPU верните `GpuViewportTransport(gpuOwner,
contextProvider, consume)` из factory. Context provider возвращает уже созданные
eligible adapter/borrowed GL owner. Consume синхронно копирует texture через
Rendering.Skia и хранит `GpuViewportImage.Frame` рядом с независимым UI image.
После actual `FramePresented` вызовите `host.Frames.Presented(frame)`. Stale/foreign/
closed ticket вернёт false. Публикация или GPU copy сами не подтверждают first-present.
Strategy/connection принадлежат FrameSession; UI callbacks и GPU owner — borrowed,
отдельных disposable DI aliases не добавляйте.

`FrameSession.Presentation` и `ViewportSurface` — общий contract CPU/GPU без native
или mapping handles. `Display`/`Surface` остаются legacy CPU API. GameEditor и
GameReplacement используют общий Start/Resize/Stop/first-present/checkpoint/rollback
lifecycle. Connection drain предшествует child Stop; failed cleanup сохраняет
connection, причины ошибки и блокирует новый Start до explicit Stop/cleanup retry.
Transport выбирается на App configuration, автоматического fallback при fault нет.
`Frames.TransportError` сохраняет background delivery/cleanup cause (включая обе
ошибки при failed cleanup); successful Stop retry не replay faulted watcher.
Новый admitted Start очищает прежнюю transport diagnostic.

Runtime должен через свой UseAether/App запустить `RunSurfaceProducerAsync` с
`frameProvider`: атомарно получить loaded/restored game tick/color и вернуть
`GpuFrameData.GamePattern(stamp, width, height, tick, color)` либо собственные
matching RGBA pixels/metadata. Metadata проверяется до native upload и после Ready;
pixels не идут в pipe. Reference pattern генерируется на CPU и загружается через
Vulkan staging; полноценный 3D renderer — отдельный этап. GPU metadata checksum
не заменяет native synchronization и не используется как CPU ring checksum.

Обычное окно Editor: `--viewport-gpu` или `--viewport-gpu-project <csproj>`;
прежние `--viewport`/`--viewport-project` выбирают CPU. UI и команды одинаковы.
`--editor-gpu-proof <directory>` повторяет selected A/B DLL/state/20Replace и
failure/rollback/close proof с actual GPU pixels. Source binding находится в
[EditorGpuViewportBinding.cs](../ManualTests/EngineSessionProbe/EditorGpuViewportBinding.cs).

## Проверки

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu
```

Тесты используют прямой ProjectReference на Aether; linked production source
copies и dependency на executable примера не требуются. Native runner отдельно
проверяет Debug apphost и published Release с own-process cleanup.
