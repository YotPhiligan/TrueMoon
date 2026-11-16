# Kton ↔ Aether: PHYS5

Профиль2026-11-15: реальный BEPU provider через общий
[session-subsystem слой](../engine/SESSION_SUBSYSTEMS.md). [План](PLAN.md#phys5),
[текущий статус](STATUS.md), [независимый consumer](../../ManualTests/KtonEcs/README.md).

## Пакеты и подключение

| Пакет | Содержание и зависимости |
| --- | --- |
| `TrueMoon.Aether.Kton` | Reference-free body/velocity/teleport/contact components, exact scene schemas и phase command helpers. Только Aether.Abstractions + neutral Kton; без Runtime/Tooling/UI/BEPU |
| `TrueMoon.Aether.Kton.Runtime` | Registry bindings, App/DI factories, reconciliation/Step/writeback/contact projection, session-owned world. Зависит от SDK выше и Aether.Runtime; без BEPU/Tooling/UI reference |
| `TrueMoon.Kton.Bepu` | Явно выбранный composition root provider. Его зависимости и ownership не меняются |

Contract-only пакет отделён по реальному dependency closure: игры/моды могут
ссылаться на физические данные без concrete Runtime или BEPU. Имена/namespace
следуют проектам и файлам; provider-specific handles в ECS не передаются.

Composition root приложения использует matching версии пакетов:

```csharp
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Kton.Bepu;

context.UseBepuPhysics(); // или другой provider IFactory<IPhysicsWorld>
context.UseAetherEcs().UseAetherPrimitiveEcs();
context.UseAetherKton(options => options.MaximumContactEvents = 64);
context.UseAetherSceneLoading(_ =>
    PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
context.UseAetherSceneSubsystems(subsystems => subsystems.AddKtonPhysics());
```

Не вызывать второй `UseAetherSceneSubsystems` для другого модуля: все выбранные
подсистемы добавляются в один callback. Game/session создаётся через DI-фабрику;
готовый пример `IFactory<PrimitiveGameSession>` — в consumer Program.cs.
Build/Start/разрешение фабрик не создают world/pool. Фабрика
`IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments>` получает новый independent
caller-owned мир через provider `IFactory<IPhysicsWorld>`; provider defaults не
дублируются и не перечитываются на tick.

`UseConfiguredAetherKton` использует точный `PhysicsBridgeConfigurationSchema.Default`.
Единственный bridge key — `Kton:Bridge:MaximumContactEvents`, Int32, default64,
StartupOnly, допустимо1..256. Источники/приоритеты и configure overrides разрешает
общий configuration stage. Это отдельная схема от
`BepuPhysicsConfigurationSchema.Default` с `Kton:DefaultWorld:*`.
Duplicate/mixed registration и отсутствующий provider отклоняются до создания мира.
Composition root выбирает подключение для Game/Player роли. Наличие физических
schemas в Editor само по себе не включает simulation в EditPreview.

## Компоненты и профиль геометрии

- `PhysicsBody`: Static/Dynamic/Kinematic, box/full XYZ, sphere/X radius с YZ0,
  capsule/X radius/Y cylinder length/Z0, mass/material/filter/sensor. Нулевой shape
  невалиден. Dynamic mass положительна, static/kinematic mass0; limits и mass ratio
  проверяются neutral Kton validators и фактическим provider.
- `PhysicsVelocity`: linear m/s, angular rad/s; optional initial schema. Отсутствие
  в исходной сцене означает zero. После Step содержит output; изменение значения
  до следующего Movement — явная velocity command. Unchanged solver output не
  переинтерпретируется как новая команда с ограничением admission speed.
- `PhysicsTeleport`: positive monotonic ulong sequence, centre-of-mass pose и
  clear-velocity flag. Equal sequence допустим только с тем же payload; older/zero
  sequence или изменённый replay отклоняются. ClearVelocity выполняется до Step;
  gravity в этом Step снова действует.
- `ExternalMotion`: marker общего Aether SDK, исключающий built-in LinearVelocity
  integration. Пока PhysicsBody прикреплён, physics является единственным pose owner.
- `PhysicsContact`: transient event entity с копией native-neutral
  `PhysicsContactEvent` и исходными generational EntityA/B.

У всех тел только root `LocalTransform` с unit scale; visual origin = centre of
mass, координаты/metres/quaternion convention общие с Kton. Parent/reparent,
nonunit scale и произвольная запись LocalTransform на attached body отклоняются.
Для pose discontinuity используется PhysicsTeleport; обычное изменение данных
authoring или native body handle не является командой телепортации.
Meshes/asset cooking, compound shapes, joints ECS, controller и ragdoll не входят
в этот профиль. Native provider по-прежнему может использоваться отдельно.

Body и optional Velocity имеют отдельные stable TypeId/schema version1; Body требует
canonical Transform, Velocity требует Body. Runtime imports не меняют исходные
schemas, persistent IDs и scene revision. Contact/teleport/runtime handles не
сериализуются как authoring data. Unknown schema versions не получают fallback.

## Подготовка и fixed tick

Фабрика проверяет точный `TimeSpan.FromSeconds(1d/60)` до backend allocation.
Оба поддержанных Kton quality profile работают60Hz; другой scene step требует
нового согласованного профиля, не silent substepping или отдельного таймера.

1. На private scene проверяются все body inputs, root/scale и capacity/mass envelope.
   Создаётся по одной immutable shape на attached body и соответствующее тело.
   Добавляются недостающая PhysicsVelocity и exclusive ExternalMotion. Общий
   Primitive profile затем инициализирует derived Transform roles и tick0 frame.
2. Один `kton.physics` system выполняется в Movement **до** `builtin.movement`.
   Он собирает полный candidate list, проверяет inputs/cancellation до mutation,
   удаляет stale attachments, пересоздаёт changed descriptors и добавляет новые.
3. Применяются velocity/teleport commands, выполняется ровно один Step с target tick,
   дожидается native join и только затем обновляются LocalTransform/PhysicsVelocity.
   Следующие Transform/Extraction видят same-tick poses.
4. Старые собственные contact entities удаляются, новый полный event batch
   записывается через deferred Movement commands. Gameplay tick N+1 читает события
   Step N; sleeping pairs не обязаны выдавать Stay каждый tick.

Gameplay-phase spawn/attach/detach/delete становится виден physics в Movement того
же tick. Изменения на конце Movement вступают в reconciliation следующего tick.
`PhysicsEntityCommands.CreateBody/AddBody/RemoveBody` записывают полный набор ролей
в одну phase batch; `DeclareWrites` добавляет необходимые access declarations.
Сама игра задаёт visual/gameplay components отдельно. Пустой default Transform
невалиден; CreateBody получает явный unit-scale pose.

Runtime attachment требует complete TransformParent/WorldTransform, PhysicsVelocity
и ExternalMotion. Прямое удаление только PhysicsBody с оставленными owned roles
отклоняется; helper detach сохраняет entity/transform и снимает все физические роли.
Удаление entity целиком поддержано. До next reconciliation `TryGetBody/TryGetEntity`
уже возвращают false для удалённых/откреплённых/stale/foreign identities.
Новые поколения body/entity не подменяют исходные End events; retired mapping живёт
до копирования событий успешного Step. Shape/descriptor change даёт новое body ID.

## Limits, ошибки и ownership

`MaximumContactEvents` ограничивает полную ECS projection; overflow явно fault-ит
сессию, truncation нет. Дополнительно действуют native limits, World entity/type
capacity и общий command budget. На замену batch требуется `oldCount + 2*newCount`
команд; default bridge64 укладывается в default phase4096. Event entities не имеют
Transform и не меняют initial scene identity map. Они очищаются при очередном
Movement или вместе с World при завершении сессии.

Reconciliation/preparation failure не публикует candidate. После начала native
mutation нет обещания rollback: завершается начатый Step/join, позднюю cancellation
наблюдает scheduler на своей границе. Неудачный tick/command/contact publication
оставляет ECS session terminally faulted без нового completed frame; требуется
Stop/Dispose. LastCompletedTick подсистемы отражает именно native completion.

Одна session owns один native world; pause — отсутствие Step, Paused Step разрешён.
Stop/Replace освобождает scheduler systems → subsystem/native world → borrowed
SceneWorld → App dependencies. Подсистема не освобождает чужой World, provider host
или DI factory. Native Dispose failure сохраняет owner для retry; общий ARCH1j
удерживает World lease до успешного завершения. Failed candidate не меняет уже
живую old session. Detached low-level использование фабрики допустимо при той же
внешней сериализации и ручном соблюдении disposal order.

## Границы ARCH3 и следующие этапы

Опциональные SDK assemblies не добавляются скрыто в fixed game-artifact ABI.
При использовании этих типов через managed Mod loader composition root должен
явно указать `typeof(PhysicsBody).Assembly` и `typeof(IPhysicsWorld).Assembly` в
существующем `ModLoaderOptions.AdditionalSharedAssemblies`, сохраняя exact SHA/identity.
Копирование DLL само по себе не подтверждает совместимость private CLR generations
со host registry. Отдельной game compiler/shared-ABI или mod/physics replacement
приёмки этот PHYS5 consumer не заявляет.

Общий checkpoint/IPC не расширен. Snapshot/restore native world, native-state
migration/mod reload, physics interpolation, queries для игровых систем как
отдельный injected session reader и meshes требуют своих контрактов. Character
controller добавлен отдельно в [PHYS6](CHARACTERS.md), завершён16.11.2026:
optional schema/input/state, sequenced jump и same-tick support. Kul ECS adapter
также завершён; следующий физический этап — PHYS8.
Headless/CPU evidence отделён от actual GPU/physical/platform/release acceptance.
