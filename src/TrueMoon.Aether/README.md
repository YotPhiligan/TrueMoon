# TrueMoon.Aether

Экспериментальный модуль игрового движка TrueMoon. Одна библиотека содержит
contracts игровой сессии, supervisor, control protocol, CPU frame transport и
явный one-frame Vulkan/OpenGL import probe.
Текущая реализация рассчитана на Windows x64 и .NET 10.

[План](../docs/engine/PLAN.md), [статус и ограничения](../docs/engine/STATUS.md),
[пример Editor/Game.Runtime](../ManualTests/EngineSessionProbe/README.md),
[отдельный runtime без Editor/UI](../Examples/AetherRuntimeHost/README.md).

## Scene files без Editor

UseAetherAuthoring().UseAetherScenePersistence() подключает общие
SceneFileCodec/ISceneDocumentStore/JsonSceneDocumentStore из TrueMoon.Aether.Scenes.
Registration не открывает files/authoring sessions/runtime/UI; defaults ленивые,
existing overrides сохраняются и повторное подключение persistence idempotent.
SceneDocumentSession и Save/Discard/Cancel UX остаются в Editor.
[Подключение, ownership и migration прежнего Editor namespace](../docs/engine/PACKAGE_BOUNDARIES.md).
FormatVersion1 и file conflict/cancellation/atomic-publication contracts сохранены.

## Диагностика

UseAether подключает session/process/build/Replace/preview/frame-fault события к injected IEventsSource;
общий bounded pipeline включается через App.UseDiagnostics(options).
[Подключение, budgets и ownership](../docs/core/DIAGNOSTICS.md).
Editor включает managed JSONL в %TEMP%/TrueMoon/Diagnostics/Editor;
smoke — в report directory/diagnostics/editor, Runtime — в
report directory/diagnostics/runtime. Каждый logger получает GUID run;
closed runs по умолчанию ограничены8/32MiB/7суток, active отдельно.
ApplicationId
общего pipeline — collector identity; SessionId/Generation/GameBuildId/RunId
передаются отдельными properties. Nonce/control endpoint не включаются.

Build, Replace, Preview.Show/Resize/Stop и FrameFault используют injected
IDiagnosticTelemetry: ActivitySource/Meter **TrueMoon**, операции `Aether.Build`,
`Aether.Replace`, `Aether.Preview.Show`, `Aether.Preview.Resize`,
`Aether.Preview.Stop`, `Aether.FrameFault`. Нужен стандартный listener/exporter;
без него новые scopes не создаются. Count/duration имеют только operation/outcome
tags; session/build/generation остаются в traces/events.
[Sampling, limits и lifetime](../docs/core/DIAGNOSTICS.md#трассы-и-метрики-операций).

| События | IDs | Смысл |
| --- | --- | --- |
| Build.Starting/Completed/Cancelled/Failed |1200–1203| Compiler вместе с reap/drain/cleanup |
| Replace.Starting/Completed/Cancelled/Failed |1300–1303| Полная transaction, включая recovery; terminal error — original cause |
| Replace.RecoveryStarting/RecoveryCompleted/RecoveryResult |1304–1306| Phase и rollback flags, отдельная recovery error |
| Preview.Show.Starting/Completed/Cancelled/Failed |1400–1403| Completed только после acknowledged first-present |
| Preview.Resize.Starting/Completed/Cancelled/Failed |1410–1413| Suspend или готовность новой поверхности |
| Preview.Stop.Starting/Completed/Cancelled/Failed |1420–1423| Fencing и cleanup preview owner |
| Frame.Fault/FaultDrained/FaultCleanupFailed |1500/1501/1503| Исходные identity/phase/surface generation до очистки; отдельный cleanup error |

Replace terminal event содержит Phase/RollbackAttempted/RollbackSucceeded и
RecoveryErrorType; failed Replace остаётся Failed даже при успешном rollback.
FrameFault operation всегда Failed, включая успешный drain. Preview preflight
rejection наблюдаем без изменения работающего preview. Presented callbacks
не создают events/scopes на каждый кадр. Старые standalone constructors работают
без telemetry; UseAether передаёт borrowed diagnostics через штатные factories.
С26октября UseAether/UseAetherRuntime включают [Enerit trace propagation](../docs/enerit/DIAGNOSTICS.md)
на обеих control peers: Client/Server spans связывают host и child. Detailed request
events выключены; service payload и generated mapping прежние. Same-build HostId
включает Enerit assembly, поэтому host/child пересобираются вместе. Standalone старые
constructors сохраняют legacy options; кастомные factories выбирают propagation явно.
С27октября штатный App graph также передаёт bounded structured child logs в host
по отдельному Enerit endpoint. ChildDiagnostics options копируются в SessionLaunch;
нужны Core reader/importer в соответствующих Apps. Host сохраняет child origin/PID/
timestamp/trace и добавляет authoritative session/generation/build properties.
Limits, loss/unconfirmed counters и lifecycle описаны в
[руководстве child → host](../docs/engine/DIAGNOSTICS_DELIVERY.md).
Aggregate frame metrics и live Editor console остаются следующими срезами.

NativeRuntimeLease.StandardOutput/StandardError теперь возвращают live tails
до32KiChars, StandardOutputStatus/StandardErrorStatus — counters и file failures.
Оба потока вычитываются постоянно, в том числе без newline и после переполнения.
ReportDirectory/runtime-output содержит четыре rotating slots на поток, каждый
до1MiB UTF8. Файлы могут иметь пропуски при file-queue overload; loss отражён в status.
Factory хранит до256process diagnostics и64последних leases для inspection;
реальным owner каждой lease остаётся supervisor.

Compiler сохраняет bounded build.stdout.log/build.stderr.log и build.output.json
с live-tail/loss/error snapshots после drain; прежний DiagnosticsDirectory/exit
code contract сохранён. UseAether фиксирует options.BuildRetention
(GameBuildRetentionOptions): по умолчанию 16 завершённых jobs, 64 MiB всех их
файлов вместе с marker и 7 суток от завершения cleanup. Значения должны быть >0.
Перед запуском и после reap/drain/очистки staging удаляются старейшие собственные
jobs сверх count/bytes/age; равенство лимиту допускается. Последний завершённый
job в корне сохраняется даже сверх лимита, включая неудачную сборку.
DiagnosticsDirectory может быть удалён последующими сборками в том же корне.

Каждый compiler job сохраняет SDK output/intermediate в своих `output`/`work`
каталогах. Поскольку это isolated single-project build, artifacts layout не
добавляет повторное имя проекта: `IncludeProjectNameInArtifactsPaths=false`.
Это сокращает staging paths и устраняет наблюдавшийся Windows/MSBuild Copy failure
при длинном TEMP и выключенных OS long paths. Общие OS/SDK path limits сохраняются;
изменять глобальный TEMP или Windows settings для проверенного профиля не требуется.

RetentionStatus сообщает RetainedJobs/RetainedBytes, ActiveJobs/SkippedDirectories,
DeletedJobs, Failures/LastError, IsOverBudget и MaintenanceBusy за последний pass.
Build.RetentionLimited (Warning1205) содержит JobId и counters; retention failure
не заменяет compiler result/exception. Exclusive marker защищает активный job;
отдельная root lease согласует очистку разных compiler instances без ожидания.
После освобождения заблокированного файла следующий job повторит cleanup.

Очистка нерекурсивная, только completed GUID jobs с versioned ownership marker
и известными плоскими diagnostics files. Опубликованные игровые artifacts,
legacy unmarked jobs, incomplete/abandoned staging, неизвестные entries и links
сохраняются; active/skipped каталоги не входят в completed count/byte budget.
Общий размер artifact root и разных Editor runs этим не ограничен.
Core managed JSONL отдельно ограничивает закрытые logger runs внутри своего
постоянного корня; общие report directories с artifacts/raw output остаются вне
этого budget. Старые непомеченные JSONL каталоги автоматически не мигрируются.
Обслуживание выполняется при сборках, фонового age-таймера нет. Root/path checks
рассчитаны на trusted-local cooperating writers; произвольная внешняя подмена
пути между проверкой и удалением не поддерживается.
[Проверки и границы retention](../docs/engine/HISTORY.md#диагностика-сборок-ограничение-хранения--2026-10-26).

Raw stdout/stderr не определяет автоматически event severity. Консоль Editor,
экспорт, полноценные operation traces/metrics и IPC trace propagation ещё в плане.

## Состав

| Область | Назначение |
| --- | --- |
| `TrueMoon.Aether.Sessions` | Session identity, `IGameSession` и session lifecycle |
| `TrueMoon.Aether.Scenes` | Immutable authoring/scene aggregates, inheritance/parent graph resolution и явная проекция в существующий SceneSnapshot |
| `TrueMoon.Aether.Scenes.Authoring` | Caller-owned document session, atomic Transform batches, revision/operation receipts и bounded Undo/Redo без Runtime/UI |
| `TrueMoon.Aether.Preview` | Built-in static scene consumer и revision-bound Edit preview через existing supervisor/GPU viewport |
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

### Authoring: шаблоны, варианты и экземпляры

`Scenes.EntityDefinition` описывает template или variant с одной основой;
`EntityInstance` задаёт отдельный ID, scene parent и локальный `EntityPatch`.
`ComponentSchema` задаёт exact version, типы/nullable/defaults свойств, multiplicity
и required component types. CLR/game types для инспекции не загружаются.

```csharp
using TrueMoon.Aether.Scenes;

var appearanceType = new ComponentTypeId(Guid.NewGuid());
var appearance = new ComponentId(Guid.NewGuid());
var doorId = new EntityDefinitionId(Guid.NewGuid());
var redDoorId = new EntityDefinitionId(Guid.NewGuid());
var schema = new ComponentSchema(appearanceType, 1,
    [new("color", PropertyValueKind.String, PropertyValue.From("white"))]);
var door = new EntityDefinition(doorId,
    new(addedComponents: [new(appearance, appearanceType, 1)]));
var redDoor = new EntityDefinition(redDoorId,
    new(propertyOverrides: [new(appearance, "color", PropertyValue.From("red"))]), doorId);
var resolver = new EntityResolver([door, redDoor], [schema]);
var entity = resolver.Resolve(new EntityInstance(new EntityId(Guid.NewGuid()), redDoorId));
var color = entity.Components[appearance].Properties["color"];
// color.Value.GetString() == "red"; color.Source.DefinitionId == redDoorId
```

IDs создаются один раз и сохраняются в authoring данных. Вариант переопределяет
только нужные свойства, добавляет occurrences или скрывает inherited components.
`RestoredComponents` явно возвращает скрытый компонент с inherited values;
`WithoutComponentRemoval` снимает только локальную маску. `WithoutPropertyOverride`
создаёт новый patch без локального override, сохраняя остальные изменения.
Значение null/default остаётся явным override, отсутствие override наследует.

Resolver копирует catalogs, не мутирует inputs и возвращает immutable result с
value/presence provenance. Он проверяет graph, exact schemas, property types,
final multiplicity и dependencies; ошибка содержит `EntityResolutionException.Code`
и ID-based `Path`. Один resolver можно использовать для независимых instances.
Depth limit64definitions; property IDs ordinal ASCII1..128, values — null/bool/
Int64/finite double/string≤4096UTF16. Автоматических numeric conversions нет.

Это in-memory authoring model ARCH1a.1/2. Disk/wire serialization нового aggregate,
полный Runtime snapshot, live editing и MCP пока запланированы. Stateless model
используется напрямую; lifecycle сервисов движка подключается через App ниже.

### Агрегат сцены и совместимый snapshot

`SceneDocument` фиксирует `SceneId`, неотрицательную document revision и immutable
catalogs instances/definitions/schemas. `SceneResolver.Resolve(document, token)`
проверяет duplicate IDs, missing/cyclic parent, catalogs и все entities, затем
возвращает один `ResolvedSceneSnapshot`. Ошибка не публикует частичный результат;
в `SceneResolutionException` доступны Code/Path/EntityId и исходная InnerException.

```csharp
var parentId = new EntityId(Guid.NewGuid());
var childId = new EntityId(Guid.NewGuid());
var document = new SceneDocument(new SceneId(Guid.NewGuid()), 7,
    [new(parentId, doorId), new(childId, redDoorId, parentId: parentId)],
    [door, redDoor], [schema]);
var scene = SceneResolver.Resolve(document);
var parentFirstOrder = scene.HierarchyOrder;
```

HierarchyOrder — preorder с родителем перед детьми, roots/siblings отсортированы
по Guid. Children содержит каждый entity, включая пустые children у leaves.
DefinitionRevisions фиксирует только referenced inheritance chains, ComponentSchemas
— exact versions visible компонентов. Scene revision не является simulation tick.
Parent graph проверяется итеративно; depth limit64 относится только к inheritance.

`SceneSnapshotAdapter.Project(scene, projector, token)` создаёт прежний
`SceneSnapshot` для существующего game/control consumer. Projector получает
`(ResolvedSceneSnapshot, ResolvedEntity)` и возвращает `SceneEntityProjection`
с world-space XYZ и Interactive. Семантика компонентов и вычисление world pose
задаются явно потребителем. Adapter сохраняет canonical GUID IDs/revision,
проверяет finite coordinates и прежний limit64entities до первого callback.
Hierarchy/definitions/provenance остаются в authoring snapshot; callback может
использовать их для своей projection policy. При failure/cancel результата нет;
внешние side effects callback принадлежат вызывающей стороне.

Такой snapshot принимает IGameSession.Load. Для стандартных примитивов используйте
`PrimitiveSceneAdapter.Project(scene, token)`: он интерпретирует `BuiltInComponents.Schemas`
и передаёт world-матрицы/цвета вместе с canonical IDs и scene revision.

### Локальное редактирование без UI и Runtime

Подключите `UseAetherAuthoring()` в App. Он регистрирует общий resolver и
`IFactory<ISceneAuthoringSession, SceneDocument>`; `UseAether()` можно добавить
в любом порядке, если нужен runtime. Authoring не требует его configuration,
запуска процесса, IPC, окна или native device.

```csharp
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Dependencies;

await using var app = App.Builder(_ => { }).Setup(c => c.UseAetherAuthoring(o =>
{
    o.MaxHistoryEntries = 100;
    o.MaxBatchEdits = 64;
    o.MaxOperations = 4096;
})).Build();

// Resolution остаётся в composition root. Прикладной consumer получает factory в constructor.
var sessions = app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>();
var editableEntityId = new EntityId(Guid.NewGuid());
var transformId = new ComponentId(Guid.NewGuid());
var editableDefinition = new EntityDefinition(new(Guid.NewGuid()),
    new(addedComponents: [new(transformId, BuiltInComponents.TransformId, 1)]));
var editableDocument = new SceneDocument(new(Guid.NewGuid()), 0,
    [new(editableEntityId, editableDefinition.Id)], [editableDefinition], BuiltInComponents.Schemas);
using var session = sessions.Create(editableDocument);
var before = session.Read();
var target = before.Resolved.Entities[editableEntityId].Components.Values
    .Single(component => component.TypeId == BuiltInComponents.TransformId);
var edit = new TransformEditBatch(new(Guid.NewGuid(), before.Document.Revision, "user"),
    [new(editableEntityId, target.Id, new TransformValue(2, 0, 0))]);
var result = session.Apply(edit);
if (result.Succeeded)
{
    var undo = session.Undo(new(Guid.NewGuid(), result.Revision, "user"));
    var redo = session.Redo(new(Guid.NewGuid(), undo.Revision, "user"));
}
```

Factory snapshots limits. Каждая Create сначала полностью валидирует initial
document, затем возвращает независимую сессию. Consumer владеет product и
закрывает его до App; контейнер не удерживает/не освобождает созданные сессии,
сессия не освобождает заимствованный stateless resolver. Повторное подключение
authoring отклоняется; startup не создаёт сессий или фоновых задач.

Apply адресует visible built-in v1 Transform по entity/component occurrence ID.
Все девять свойств становятся instance overrides; definitions, parent links,
структурные patches и другие компоненты сохраняются. Кандидат проходит общий
resolver, exact Transform schema и finite/nonsingular parent-composed validation
до единой публикации. Authoring не наследует preview limit64entities или material
policy. Сохранять старые immutable query snapshots безопасно.

Document.Revision растёт на каждый successful Apply/Undo/Redo. ContentRevision
восстанавливает identity содержимого при Undo/Redo и подходит для session-local
saved checkpoint. Новый Apply, включая равные значения, создаёт transaction и
очищает Redo. Combined Undo/Redo bound — MaxHistoryEntries;0отключает историю.
Значения по умолчанию100/64/4096; допустимые пределы1024/4096/65536 соответственно.

Операции возвращают typed error/revision/path. Ожидаемая revision проверяется
перед подготовкой, batch применяется целиком. Сессия сериализует concurrent
requests. Cancel до commit и неожиданный preparation failure не публикуют данные
или receipt; retry того же ID безопасен. После commit cancel не маскирует успех.
Exact duplicate возвращает исходную small receipt даже после Undo/других edits;
изменение intent/actor/revision/payload с тем же ID даёт OperationIdConflict.
Terminal success/reject receipts хранятся до Dispose; при MaxOperations новые
IDs отклоняются, старые не вытесняются. Квитанции не удерживают старые документы.
Receipt identity не переживает закрытие/restart сессии; автоматического replay нет.

Этот первый ARCH1b срез не включает create/delete/reparent, generic property
editing/reset overrides, disk codec, UI inspector, Edit preview или live World.
Сохранение документов/проектов остаётся ED1/ED2; Play не меняет authoring автоматически.

### Независимый Edit preview

На App с `UseAether(...)` доступен `host.Preview`. Подключите explicit GPU viewport
через существующий ViewportTransportFactory; CPU pattern transport не рисует
примитивную сцену и для первого preview profile отклоняется до замены старого
preview. AetherHost владеет контроллером, он заимствует Frames/Supervisor/Gpu.
Регистрация и App startup не открывают preview или game process.

```csharp
// authoring — caller-owned session, host — App-owned AetherHost с GPU viewport.
var snapshot = authoring.Read().Resolved;
var shown = await host.Preview.ShowAsync(snapshot, 640, 360, token);
// Consumer сохраняет ViewportFrame с image и после фактического показа вызывает:
// host.Preview.Presented(drawnFrame);
// Этот callback должен работать уже во время ShowAsync: тот ждёт first-present.
await host.Preview.ResizeAsync(0, 0, token);       // suspend
await host.Preview.ResizeAsync(800, 450, token);   // resume и новое подтверждение
await host.Preview.StopAsync();                   // repeatable; не закрывает App
```

`ScenePreviewDocument` связывает SceneId/Revision с полной SessionIdentity.
`Describe(frame)` возвращает исходный stamp только для живой generation/surface;
`Presented(frame)` проверяет exact latest ticket, а `LastPresented` хранит реально
показанный кадр. Старый кадр после replacement/resize/Stop/crash не становится
кадром новой revision. Во время первого показа Document ещё null;0x0сохраняет
runtime/document, но снимает presentation. Show требует положительный extent,
Resize поддерживает0x0; остальные bounds —16..1920 ×16..1080.

Перед заменой projection/schema/material/limit64 проверяются локально. Ошибка
preflight сохраняет рабочий child. Первый profile обновляет документ через
stop/drain/reap → новую generation → bounded Enerit LoadScene → GPU first-present.
Это full snapshot replacement, не live World edit и не частый incremental update.
Transform/Undo/Redo вызывают тот же Show с текущей authoring revision; preview не
меняет документ/history. Нижняя revision того же SceneId отклоняется до Stop;
Stop сбрасывает эту session-local границу. Concurrent Show/Resize отклоняется,
новых неограниченных очередей нет. Cancel/error и Stop исключают late readiness;
cleanup error сохраняет ownership для явного Stop retry.

Preview и Play используют один viewport owner: сначала явно остановите текущий
режим. Preview не захватывает активную/неочищенную game session; неиспользованный
Preview.Stop/Dispose не останавливает игру. Отдельный orchestration в Editor и
автоматическое переключение Edit/Play остаются прикладными задачами.

Child bootstrap проверяет `SessionLaunch.Role`. Для EditPreview вызывайте
`UseAetherPreviewRuntime(launch, parentId)` после UseAether: нужен Build=null,
RequireRestore=false и GameBuildId=BuildIdentity.PreviewId. Этот ID считается
из Aether/profile и не читает Game.Logic.dll. PreviewGameSession рисует projected
primitives с фиксированной камерой(0,1,6), tick0; Play/Step/input не поддержаны.
Обычный selected game использует прежний UseAetherRuntime с Role.Game и artifact.
Core control остаётся Enerit, pixels — existing shared GPU transport, child — Mithril.

Полный native proof: `EngineSessionProbe --edit-preview-proof <new-directory>`.
Он использует прежний viewport fixture и проверяет actual pixels/revision,
Transform/Undo/Redo, resize/suspend/resume и native close с pending first-present.
Нормальный Editor UI пока не подключён к этому API; project/session, inspector,
camera/picking и storage остаются ED1–ED5. Editor/runtime/game artifacts после
изменения Aether пересобираются вместе; same-build fingerprint fencing сохраняется.

### Стандартные компоненты и комната

| Роль v1 | Постоянный ComponentTypeId | Свойства / defaults |
| --- | --- | --- |
| Transform | `6fc9fce0-9547-42f5-b4df-f05f306f1001` | Number x/y/z/pitch/yaw/roll=0, sx/sy/sz=1 |
| Appearance | `6fc9fce0-9547-42f5-b4df-f05f306f1002` | Number r/g/b=1; opaque unit cube |
| Interaction | `6fc9fce0-9547-42f5-b4df-f05f306f1003` | Boolean enabled=true; eligibility only |

Appearance/Interaction требуют Transform. Все роли single-occurrence; schema v1
проверяется целиком, включая defaults/dependencies. Иной version или переопределение
schema под тем же ID требует явной миграции. Неизвестные роли остаются authoring-only.
Нет Appearance — нет draw; нет Transform у группы — local identity.

Правая система координат, +Y вверх, forward -Z. Локальная матрица row-vector:
`Scale * Rotation(Quaternion.CreateFromYawPitchRoll) * Translation`, углы в радианах;
`world = local * parentWorld`. Матрицы не раскладываются обратно в SRT, shear
сохраняется. Scale строго положительный; nonfinite/singular/overflow отклоняются.
Материалы stored-sRGB [0,1], без текстур и физического lighting.

`SceneEntity.Primitive` — optional deep-owned `Rendering.ScenePrimitive`:16 row-major
floats и RGB. `Rendering.RenderPrimitive` — immutable world-space cube.
`new PrimitiveScene(camera, primitives)` фиксирует до64draws, пустой список рисует
фон. Старый constructor `(camera, rotation, color)` сохраняет куб в (0,1,0).
Renderer выполняет draws в одном depth-tested pass на slot-owned surface.

**Миграция control v2 → v3:** Editor/Runtime/Aether/game DLL пересобираются совместно;
v1/v2 control identity отклоняется, wire fallback отсутствует. Текущий профиль —
Control3/Frame1/Snapshot2 (TMCK2). Комната имеет статичную geometry в пределах scene
revision: checkpoint содержит camera/tick/interaction и per-object switches,
геометрия повторно загружается из того же SceneSnapshot. При изменении
scene data или её definitions требуется новая scene revision; полный runtime world
snapshot и live edits ещё не реализованы. Поле SnapshotVersion относится к TMCK.

Пример `ManualTests/EngineSessionProbe --room` строит authoring room с полом,
стенами, тремя объектами и transformed parent, загружает отдельный `Game.Logic.RoomGame`
в child и использует прежние App/input/Replace. Collisions и HUD — далее.

### Reach и состояния объектов

`Interactions.PrimitiveRaycast.Cast(bodies, camera, maximumDistance)` проверяет до64
opaque `InteractionBody` на forward-ray камеры. Reach finite в(0,1000], граница
включена. Точное пересечение inverse-transformed unit cube сохраняет rotation,
неравномерный scale/shear и world-space distance. Origin внутри/на поверхности
даёт distance0. Ближайшее opaque тело блокирует луч даже при Interactive=false;
одинаковая distance предпочитает blocker, затем ordinal entity ID. No hit → null.

`Game.Logic.RoomGame` использует reach4. На fixed tick камера сначала получает
input, затем один E pulse переключает только найденную eligible цель. Активный
объект жёлтый; следующий pulse возвращает authored RGB. Другие объекты независимы,
editor authoring snapshot не меняется. Удержание E не создаёт повторные pulses.

**TMCK1 → TMCK2:** encoder/decoder и SessionIdentity.SnapshotVersion теперь2;
формат1/future versions отклоняются. Matching binaries пересобираются вместе.
`GameCheckpointState.Objects` — immutable `GameObjectStates`, до64unique valid
entity IDs, ordinal sorted, с Boolean Active и value equality. Codec переносит
полный набор: count(UInt16), для каждого UTF8length(UInt16)/ID/flag(byte), затем
общий SHA256. Base offsets camera/tick сохранены, FixedBytes149. Invalid count,
UTF8/order/duplicates/flags/trailing data отклоняются. Поле Interaction в RoomGame
равно Any(Active); старые ProbeGame/CameraGame используют empty Objects.

Room restore проверяет exact loaded eligible-ID set и aggregate flag до commit
camera/tick/objects; несовпадение даёт RestoreRejected без mutation. Runtime держит
readiness/frame fence до успешного startup restore. Transient input не входит в
checkpoint. Это ограниченное game-owned состояние, не полный runtime World и не
API редактирования Play; default editor tools остаются read-only.

### ProjectReference

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
wire теперь v3 (ENG5c.1 scene primitives), требует rebuild matching host/game;
frame schema остаётся v1, TMCK теперь v2. ENG5c.1 добавляет authoring комнату,
ENG5c.2 — reach/occlusion и checkpointed per-object interaction;
collisions, HUD и Hot Reload остаются дальнейшими этапами.
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
    options.RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime");
}));
await app.StartAsync();
var engine = app.Services.Resolve<AetherHost>();
await engine.Supervisor.StartAsync(new SceneSnapshot("demo", 0, []));
await engine.Supervisor.StopAsync();
await app.StopAsync();
```

Это явно выбранный совместимый same-executable/legacy профиль: entry executable
реализует `--runtime` handler, как EngineSessionProbe, и содержит matching Game.Logic.dll.
`CurrentProcess("--runtime")` сохраняет прежний `BuildIdentity.HostId`, но не добавляет
runtime branch произвольному executable. Сам UseAether регистрирует App-owned
services/lifecycle, не генерирует runtime entry point и не запускает игру.
Свой IRuntimeFactory задаётся через options.RuntimeFactory; callback должен вернуть
новый App-owned instance, а зависимости может получать из resolver. Factory/host
имеют одну регистрацию владельца; raw Supervisor/Frames не регистрируются aliases.

Options валидируются и копируются во время UseAether. Повторное подключение или
отдельная регистрация IRuntimeFactory отвергаются, чтобы не создать два владельца.
App Stop/Dispose permanently closes admission, завершает frames и supervisor;
Start после этого запрещён. Ошибки cleanup наблюдаемы, host Dispose допускает
явный retry после отказа. UI выбирает приложение и drains своё work до освобождения
зависимых canvas/native resources. Headless и viewport examples используют UseAether.

### Explicit runtime launch и identity — ARCH2c

Для отдельного managed runtime в parent composition root задайте абсолютные пути:

```csharp
context.UseAether(engine =>
{
    engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll);
    engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll);
    engine.GameBuildId = new string('0', 64); // До выбора game artifact.
    engine.ReportDirectory = reportDirectory;
});
```

`absRuntimeExe` — apphost, `absRuntimeDll` — его managed entry assembly. Вариант через
абсолютный dotnet executable: `new NativeRuntimeLaunch(absDotnetExe, absRuntimeDll,
[absRuntimeDll, .. runtimeArguments])`. DLL и все arguments задаются явно, без shell
parsing и implicit `--runtime`. Descriptor immutable, копирует arguments; default
working directory — каталог runtime DLL. Standalone native factory создаётся как
`new NativeRuntimeFactory(runtimeLaunch)`.

`RuntimeLaunch` nullable: без descriptor registration, App startup, headless/common
services и custom `RuntimeFactory` работают. Попытка default native Start выдаёт
configuration error с требованием настроить `AetherOptions.RuntimeLaunch` либо явную
native factory. Child-only App descriptor не нужен.

`BuildIdentity.ForRuntime` учитывает role-labelled host/runtime managed entries и
exact shared Aether/Contracts/Enerit/Mithril binaries. Shared файлы проверяются в
обоих entry directories; parent launch и child bootstrap сверяют actual managed entry.
Для явно настроенного same-executable варианта остаётся прежний `BuildIdentity.HostId`.
Native counterpart identity имеет Version1; bootstrap JSON ограничен16384characters
и depth32. Control3/frame1/snapshot2 и session/nonce/generation fences не изменены.

Это trusted-local compatibility check, не authentication boundary и не полный hash
native/private dependencies. Deployment нельзя изменять in place во время session.
Смена pair identity делает старые game artifacts/checkpoints несовместимыми: игры
нужно пересобрать для новой пары. Scene/project file formats не меняются.

[Examples/AetherRuntimeHost](../Examples/AetherRuntimeHost/README.md) — отдельный
пример без Editor/UI dependencies, с selected-game и built-in preview ролями.
Main/CLI/App composition остаются в executable, не в Aether. Existing Editor example
пока явно выбирает same-exe профиль до host/resources stage. ARCH2d assets и ARCH2e
independent NuGet/published matrix не закрыты; новый
[`--runtime-launch-proof`](../ManualTests/EngineSessionProbe/README.md#arch2c-explicit-runtime-launch-и-independent-process-proof)
нужно отдельно запускать на source Debug и published Release, без переноса результатов.

### Создание сервисов и runtime через DI

`UseAether` регистрирует `IFactory<T>` и типизированные `IFactory<T, TArguments>`:
supervisor, GPU host, frames/receivers, compiler, replacement/editor, runtime leases,
game loader и authoring resolver. `AetherHost` получает готовые зависимости явно;
фабрики можно зарегистрировать перед `UseAether`, чтобы заменить штатное создание.
Результаты дочерних фабрик принадлежат aggregate host. Ошибка создания освобождает
уже полученные ресурсы в обратном порядке.

В executable child сначала прочитайте validated bootstrap, затем создайте App:

```csharp
var bootstrap = RuntimeBootstrap.ReadEnvironment();
var launch = bootstrap.Launch;
await using var runtimeApp = App.Build(context =>
{
    context.UseAether(options =>
    {
        options.HostContractId = launch.Identity.HostContractId;
        options.GameBuildId = launch.Identity.GameBuildId;
        options.ReportDirectory = launch.ReportDirectory;
        options.ChildDiagnostics = launch.DiagnosticDelivery ?? new() { Enabled = false };
    });
    if (launch.Role == RuntimeRole.EditPreview)
        context.UseAetherPreviewRuntime(launch, bootstrap.ParentProcessId);
    else context.UseAetherRuntime(launch, bootstrap.ParentProcessId);
});
await runtimeApp.StartAsync();
var runtime = runtimeApp.Services.Resolve<TrueMoon.Aether.Runtime.AetherRuntimeHost>();
try { await runtime.RunAsync(); }
finally { await runtime.DisposeAsync(); await runtimeApp.StopAsync(); }
```

`ReadEnvironment()` проверяет bounded JSON, role/identity/endpoint, parent PID и own
binary fingerprint до App construction. Strict profile принимает selected game
либо preview; только legacy Game.Logic probes явно включают `allowLegacyGame: true`,
включая IsPreview/IsChild classification до legacy dispatch. Общий parser заменяет
consumer deserialize/duplicate validation; Main, diagnostics и reports остаются у host.

Runtime registration фиксирует launch и регистрирует `AetherRuntimeHost` с фабриками
parent listener, game loader, frame producer, protocol и Enerit server. `RunAsync`
запускает work; shutdown drains workers до освобождения игры. Runtime host заимствует
App/GPU services. Повторное подключение отвергается.

Authoring-системы получают `SceneResolutionService` через constructor injection;
он использует `IFactory<EntityResolver, SceneDocument>`. Статический
`SceneResolver.Resolve` остаётся независимой точкой для low-level применения.
Документы, значения, UI elements и native handles не становятся DI services.

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
переданные compiler свойства MSBuild `AetherContractPath` и `TrueMoonContractsPath`
как non-copying references:

```xml
<Reference Include="TrueMoon.Aether">
  <HintPath>$(AetherContractPath)</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="TrueMoon.Contracts">
  <HintPath>$(TrueMoonContractsPath)</HintPath>
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
declared private assemblies, framework и общие Aether/TrueMoon.Contracts assemblies. Контексты имеют
[отдельную область типов, а unload кооперативен](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext);
окончательная граница освобождения native runtime — завершение дочернего процесса.
Legacy fixed GameBuildId launch сохранён для существующих consumers.

Выбранный entry type реализует `IGameSession` и `Runtime.IGameSessionEntry`:

```csharp
public static IGameSession Create(TrueMoon.Services.IServiceResolver? services)
    => new MyGame(services!.Resolve<IMyGameDependency>());
```

Это factory boundary внутри игровой сборки: зависимости передаются в constructor,
игра принадлежит loader, runtime services заимствуются. Low-level `Load` без App
передаёт null; можно явно передать свою `IFactory<IGameSession, Type>`.
Constructor reflection/Activator fallback отсутствует. Не храните runtime resolver
или collectible game types в static caches. **Миграция2026-10-22:** добавьте entry
factory и Contracts reference, пересоберите Editor/runtime/game вместе: HostId и
artifact ContractHash теперь учитывают обе общие assembly, старые artifacts несовместимы.

## Checkpoint и восстановление — ENG3b

Игра может реализовать `ICheckpointGameSession` в дополнение к `IGameSession`.
Минимальная схема v1 содержит UInt64 tick, camera position/Euler radians и interaction
flag. Capture должен только читать состояние; Restore валидирует всё до атомарного
применения либо бросает `CheckpointRejectedException(CheckpointOutcome.RestoreRejected)`
без изменения игры. Неожиданная ошибка callback имеет неизвестный исход.

```csharp
await engine.Supervisor.SetPlayingAsync(false);
var checkpoint = await engine.Supervisor.CaptureCheckpointAsync(cancellationToken);
checkpoint.ValidateFor(engine.Supervisor.Status.Identity!.HostContractId, scene); // До Stop; configured pair identity.
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
