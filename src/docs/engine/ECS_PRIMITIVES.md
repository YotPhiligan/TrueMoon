# ECS Transform и primitive rendering

**2026-11-06, ARCH1e.4.** Выбранный первый профиль: existing BuiltIn Transform/
Appearance/Interaction v1 bindings, optional parent-space LinearVelocity и copied
primitive snapshots. Stable IDs/defaults и старый64entity SceneSnapshot adapter
сохраняются. Новая extraction ограничивает enabled drawables до64, а не группы.
Весь native/GPU owner остаётся у прежнего renderer/viewport, не в компонентах.

## Данные и математика

LocalTransform содержит parent-space Position, unit quaternion Rotation и positive
Scale. Default struct invalid; Identity явный. Quaternion не нормализуется молча:
допуск abs(lengthSquared-1)<=0.0001 по float calculation. Authoring Euler остаётся
pitch/yaw/roll в radians; binding использует CreateFromYawPitchRoll(yaw,pitch,roll).
Матрицы row-vector: S*R*T, затем local*parentWorld. Матрицы не декомпозируются,
shear от nonuniform scale/rotation сохраняется. Nonfinite/singular/overflow data
отклоняются; stored-sRGB RGB должен быть в[0,1], без новой color conversion.

Scene initialization создаёт LocalTransform.Identity для групп без authored
Transform и derived TransformParent/WorldTransform для всех initial entities.
Initial cache — invalid default matrix до первой полной Update, а не ложная identity tick0.
Parentdefault=root; nondefault должен быть live node того же World. Destroy не
делает implicit cascade/reparent: caller явно меняет ссылки до следующей оценки.
Union Local/Parent/WorldTransform обязан быть complete. Self/cycle/stale/foreign/
missing-node parent диагностируются, а не становятся roots или skipped entities.

## Порядок и публикация

MovementSystem меняет Local.Position на LinearVelocity*fixedDelta: скорость в
координатах parent, без применения собственной Rotation. Preflight готовит все
значения с double intermediates/strict finite Single range до commit.
TransformSystem выполняет iterative DFS без recursion; собирает/проверяет все
nodes и matrices до замены cache. First tick0 разрешён, далее stamp растёт.
Preflight error/cancellation сохраняет прошлые caches/stamp. После final token
check commit не прерывается. Это не rollback всего simulation tick: предыдущая
Movement phase может уже завершиться перед ошибкой Transform.

Extraction требует current-tick WorldTransform каждого Appearance, проверяет
color/matrix даже у disabled entries, сортирует enabled items по handle slot и
отклоняет превышение capacity без partial/silent truncation. Snapshot содержит
WorldId/SceneId/sourceRevision/tick, copied items и immutable PrimitiveScene.
World, services, handles GPU и leases в snapshot не удерживаются. Старый snapshot
остаётся пригоден после mutation/disposal World; source handles — metadata only.

PrimitiveSimulation owns scheduler: Movement→Transform→Extraction. Initial
Transform/extraction tick0 готовятся до ActiveScene publication. При tick
snapshot становится current только после успешного завершения scheduler.
PrimitiveGameSession реализует IRenderGameSession через этот complete snapshot;
RuntimeProtocol уже захватывает matching tick и scene на одном serialized boundary.

## App и lifetime

UseAetherPrimitiveEcs конфигурирует copied limits, fixedStep и fixed-view camera,
регистрируя typed factories для systems/controller. Startup не создаёт World или
native resources. UseAetherRuntime подключает lazy defaults, сохраняя explicit
controller/system factory overrides. Game entry собирает registry через
PrimitiveEcsComponents.Register(builder) и получает registry loader/controller из DI.
Existing BuiltInComponents.Schemas содержит прежние3roles; optional VelocitySchema
с новым stable1004ID добавляется только explicit runtime helper и требует Transform.

SceneWorldGameSession.PrepareSceneWorld выполняется на private candidate перед
final cancellation/publication. Returned IDisposable controller принадлежит base
и освобождается до scene. Partial factory failure обязан cleanup либо вернуть
SceneWorldPreparationException.CleanupOwner. Legacy DisposeGameResources hook
успешно завершается первым; controller и World затем освобождаются с retry flags.
Factory initialization может добавить derived pools до ошибки: private scene
после failed initialization нужно discard, а не считать World transaction.

MaximumTransformEntities default65536 (1..1048576), MaximumPrimitives default64
(0..64), fixedStep default1/60s (>0..1s). Camera/FOV/near/far — copied render-view
configuration, не camera component. Counts не являются byte quotas/performance SLA.
Queries/mutations serialized owner; ни Resolve, ни reflection, ни native calls в tick.

## Проверки и продолжение

Фактические проверки записываются в [STATUS](STATUS.md) и
[HISTORY](HISTORY.md#ecs-primitives-2026-11-06).
Headless10k scale отделён от native64primitive scope. Cancellation checks в
preflight/factory подтверждаются отдельно от физических timing races; специально
для тестов progress callback в public hot path не добавляется.
Новый mesh/material resource pipeline, camera selection/input, Editor Play/preview
migration, runtime hierarchy commands и logical checkpoint сюда не включены.
Следующее направление — ARCH1f material/resource contracts; ECS checkpoint — e.5.

Повторяемые runners — [ManualTests/EcsPrimitive](../../ManualTests/EcsPrimitive/README.md):
source Debug/published Release/fresh isolated7-package headless consumer и отдельно
source/published Vulkan→GL actual readback. CPU oracle кодирует literal source matrices/
colors, native oracle проверяет geometry/centroids/material/tick/retirement. Native
validation DLL/manifest передаются явно через process VK_LAYER_PATH с сохранением
hashes; первый missing-layer failure не считается успешной проверкой. Новый readback
backend, изменение native shaders/driver settings или fresh-package GUI proof не добавлены.
