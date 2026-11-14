# Kton.Bepu: PHYS2–3 provider

**2026-11-12 — реализованы rigid-body simulation, bounded queries и contacts на exact beta.33.**
[Контракт](CONTRACTS.md), [план PHYS2](PLAN.md#phys2), [статус](STATUS.md),
[актуальные проверки](HISTORY.md#phys3-2026-11-12), [manifest](PHYS3_RESULTS.json),
[reproducible runner](../../ManualTests/KtonProvider/README.md).

## Подключение и граница этапа

`TrueMoon.Kton.Bepu` зависит от neutral `TrueMoon.Kton`, общих `TrueMoon.Contracts`
и **BepuPhysics/BepuUtilities2.5.0-beta.33**. CPM задаёт exact ranges `[2.5.0-beta.33]`;
другие consumers не переводятся на BEPU транзитивно. Neutral Kton остаётся BCL-only.
Нет зависимостей от Cobalt implementation, Aether/Tooling/UI/GPU/native physics DLL.

`BepuPhysicsWorldFactory` реализует существующий
`IFactory<IPhysicsWorld, PhysicsWorldOptions>`. Сама фабрика не создаёт ресурсы до
Create. В composition root low-level потребителя:

```csharp
using System.Numerics;
using TrueMoon;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;

using var owner = new BepuPhysicsWorldFactory();
IFactory<IPhysicsWorld, PhysicsWorldOptions> worlds = owner;
using var world = worlds.Create(new PhysicsWorldOptions());
var shape = world.CreateShape(new PhysicsSphere(0.5f));
var body = world.CreateBody(new(shape, PhysicsBodyMotion.Dynamic,
    new PhysicsPose(new Vector3(0, 5, 0), Quaternion.Identity)));
world.ApplyCommands([new(PhysicsCommandKind.ApplyLinearImpulse, body, Vector3.UnitX)]);
world.Step(1);
var state = world.GetBodyState(body);
```

В приложении фабрика должна внедряться через DI; этот пример — composition root,
не новый способ создания сервисов в gameplay. PHYS4 реализовал публичные
UseBepuPhysics/UseConfiguredBepuPhysics и both-DI lifetime acceptance;
[App/DI/ownership guide](APP.md). Low-level factory сохраняет самостоятельный путь,
а штатное подключение сервисов приложения выполняется через App.

PHYS2 добавил `IPhysicsWorld.Capabilities`, PHYS3 включил Queries/ContactEvents
вместе с RigidBodySimulation. [Query/event guide](QUERIES_CONTACTS.md) определяет
bounded prefix/BVH scratch, filters, generation-safe transitions и overflow.
Gameplay consumer проверяет required capabilities до публикации session.
Current neutral consumer заново прошёл131checks source/fresh package D/published R.

## Хранение и владение

У каждого создания мира новый Guid. World-local fixed-capacity stores тел/форм/
суставов отделены от backend handles. Slot generation начинается с1, reuse повышает
её; uint.MaxValue retire навсегда. Public ID lookup проверяет structural validity,
world и фактическую live slot identity. Static/dynamic/kinematic используют один
нейтральный body store; packed backend collidable map различает body/static handles.

World owns Simulation, BufferPool, ThreadDispatcher и captured shapes. Primitives
неизменяемы; mesh vertices/indices копируются, повторно проверяются и готовятся в
pooled Triangle/BVH до публикации shape ID. Shared shape имеет один owner и число
body references. RemoveShape с body references и RemoveBody с joints — ResourceInUse.
Удаление joints/shapes возвращает backend ресурсы, а pool blocks могут сохраняться
до Dispose; это ожидаемый bounded high-water, не отсутствие cleanup.

Factory result принадлежит caller. Factory хранит **только** один неопубликованный
failed-creation resource owner, если cleanup тоже завершился ошибкой. Published
worlds не удерживаются фабрикой в коллекции и не освобождаются её Dispose.
Factory закрывается после успешной очистки failed candidate; caller-owned worlds
сохраняют работоспособность и освобождаются сессией отдельно. При pending cleanup
повторный Create отклоняется, owner не теряется.

`BepuResourceSnapshot` показывает active resource owners, published worlds,
failed candidate, completed cleanups, sampled maximum per-world pool bytes,
remaining bytes после успешной очистки и число owners с превышением настроенного
diagnostic budget. Последнее не вызывает hard-memory failure. Snapshot — наблюдаемые
factory-local counters, а не единая транзакция состояния всех миров.

## Bodies, joints, callbacks

Static bodies имеют0mass/velocity, kinematic не получают gravity, dynamic используют
inertia primitive shape. Dynamic/kinematic concave meshes unsupported. Live dynamic
mass range учитывает candidate перед add, ratio<=100; удалённые bodies исключаются.
Shape/body/joint capacity и identity проверяются до backend mutation.

BallSocket использует PHYS1 anchors, spring30Hz/damping1. Оба body non-static,
хотя бы одно Dynamic. Connected collision disabled хранится ref-counted по pair:
удаление одного из нескольких joints не включает столкновения преждевременно.

NarrowPhase callbacks читают стабильную во время Step backend map, применяют
симметричные layer masks, connected-pair filter, sensor suppression и min friction.
Spring/damping/recovery берутся из whole-world profile. Callback не вызывает ECS,
DI, UI или внешнее IO. Неизвестная mapping/callback failure фиксирует флаг; ошибка
сообщается после возврата синхронного Timestep. PHYS3 редуцирует bounded physical samples в owned gameplay snapshots.

## Команды, fixed Step и ошибки

Owner gate отклоняет concurrent/reentrant вызовы как Busy. Metadata можно читать
без входа в mutable world. ApplyCommands сначала проверяет весь batch, затем строит
bounded per-body preview с current pose/velocities и pending impulses. Все live IDs,
режимы, cumulative command budget и каждый ordered velocity effect проверяются
**до первой записи**. Некорректный последний command не изменяет первый body.
Scratch arrays выделяются при создании мира, не растут от batch к batch.

Impulse применяется к текущей скорости; force накапливает один `F*fixedDelta` для
следующего Step, pause не добавляет wall-clock force. SetVelocity сохраняет pending
force; **Sleep обнуляет velocities и pending force**. Teleport сохраняет скорость и
обновляет bounds; Static teleport использует штатное backend ApplyDescription.
Wake/Sleep — requests для Dynamic; BEPU может активировать/усыплять connected island.
Собственной отдельной interpolation history provider не держит: consumer знает
об explicit Teleport и сбрасывает свою interpolation, будущий ECS bridge обязан это
сохранить. PHYS3 сохраняет pending End/Teleported и возможный новый Begin с полным generation key.

Step допускает ровно следующий tick, dt профиля1/60,8iterations/4solver substeps,
один full collision step и один worker. Сначала state/tick/cancellation; затем
pending impulses, синхронный Timestep/join, callback errors и проверка полученных
poses/velocities на numeric envelope. Только после полного успеха публикуется tick
и сбрасывается cumulative command budget. После начала cancellation не прерывает
simulation. Nonfinite/out-of-envelope state — Faulted, без silent clamp.

Unexpected mutation/Step failure переводит мир в Faulted. Последующие операции
кроме metadata/Dispose запрещены; частичная запись **не откатывается**. Admission
errors/cancellation до начала сохраняют Ready, tick и pending commands/forces.

## Очистка и предел доказательства

Dispose последовательно выполняет Simulation.Dispose → ThreadDispatcher.Dispose →
BufferPool.Clear, проверяет реальные pool counters и затем очищает managed stores/
maps/previews. Каждая успешно завершённая стадия имеет completion bit. Ошибка
сохраняет World.State=Disposing и owner; retry не повторяет уже завершённые стадии.
После полного успеха State=Disposed, counters освобождены, Dispose idempotent.

Fault probes моделируют ошибки **перед границами стадий**, включая failure создания
плюс failure cleanup. Это не доказательство retry-safety произвольного partial
failure внутри самого BEPU.Dispose, повреждения unsafe heap или аварии процесса.
Один worker не доказывает multi-worker exception/ownership матрицу.

Реальные проверки PHYS2:184checks на source Debug, fresh NuGet Debug и published
Release,64create/dispose cycles;20waves×16mesh create/remove в одном живом мире дали
pool plateau4,718,592bytes. В итоговом stress factory owners0/published0/pool bytes
after cleanup0; maximum sampled5,029,888bytes. Counters не доказывают отсутствие
всех managed/process leaks и не являются hard RAM cap. StepDiagnosticBudget остаётся
target для будущих измерений; PHYS2 не выполнял formal performance benchmark.

FastLinear60 повторил sphere0.1/speed600/wall0.04 fixture на61tick: максимальный
x=-0.11897472 при геометрической границе -0.12±0.005. Не заявляются arbitrary-scene,
angular/multiple-impact CCD guarantees. PHYS3 queries/events прошёл309checks/profile;
[границы](QUERIES_CONTACTS.md). PHYS4 добавил192checks/profile App/DI и повторил
309provider checks в3profiles; [evidence](HISTORY.md#phys4-2026-11-12).
PHYS5 ECS, AOT/другие OS/2+workers и full solution acceptance требуют своих результатов.
