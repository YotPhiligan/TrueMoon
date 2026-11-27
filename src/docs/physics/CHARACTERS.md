# PHYS6 — контроллер капсулы

Реализован 16.11.2026, версия `1.2611.16.2-alpha`. [Задача](PLAN.md#phys6),
[команды и результаты](HISTORY.md#phys6-2026-11-16), [manifest/SHA](PHYS6_RESULTS.json).
Это ограниченный CPU/headless профиль Windows x64, BEPU 2.5.0-beta.33,
Baseline60/FastLinear60. У мира остаётся один внешний фиксированный Step 60Hz.

## Контракт и владение

Нейтральные типы находятся в `TrueMoon.Kton.Characters`. Опциональная capability
`PhysicsWorldCapabilities.Characters` требует реализации `IPhysicsCharacterWorld`;
старый `IPhysicsWorld` не расширен обязательными методами для других providers.
Мир получают из внедрённой App-фабрики `IFactory<IPhysicsWorld>` после
`UseBepuPhysics`/`UseConfiguredBepuPhysics`. Создание дополнительных сервисов не нужно.

`CreateCharacter(body, options)` заимствует существующее upright dynamic nonsensor
тело с `PhysicsCapsule`. +Y вверх, мировая XZ скорость, origin — центр масс капсулы.
Поза может иметь yaw; наклон и угловая скорость запрещены. Контроллер обнуляет
обратную вращательную инерцию, удерживая капсулу вертикально. На тело нельзя
одновременно назначить второй контроллер или BallSocket. Автоматического создания
и удаления заимствованного тела/shape нет.

`RemoveCharacter` удаляет motion constraint и восстанавливает исходную инерцию,
сохраняя тело и его линейную скорость. До `RemoveBody` нужно снять контроллер.
Dispose мира освобождает все контроллеры, simulation/dispatcher и pool; сохраняется
retry failed cleanup и App ownership из [PHYS4](APP.md). На каждый контроллер
выдаётся world-local generational ID; foreign/default/stale handles отвергаются.
Число контроллеров ограничено `MaxBodies`, максимум один на тело. Вызовы мира
внешне сериализованы тем же gate; concurrent/reentrant вызов возвращает Busy.

## Движение, опора и прыжок

`SetCharacterInput(id, new PhysicsCharacterInput(worldXZVelocity, jump))` заменяет
цель скорости, сохраняемую между Step. Модуль вектора не превышает MaximumSpeed;
система не нормализует ошибочный ввод молча. Локальное направление/камера/клавиши
принадлежат игре. Target zero тормозит на опоре; в воздухе остаются гравитация,
линейные импульсы и обычные столкновения. Air steering отсутствует.

`Jump=true` — один импульс на ближайшем начатом Step. Неудачная попытка в воздухе
тоже потребляется; ожидания следующей посадки нет. Прыжок задаёт скорость +Y
относительно текущей опоры и прикладывает противоположный импульс к dynamic support.
Moving support передаёт скорость точки контакта, включая вращение. Solver constraints
обеспечивают движение вдоль допустимых склонов и ограниченную силу удержания
при спуске. Sensors и исключённые фильтром пары не становятся опорой.

`GetCharacterState` возвращает актуальную позу/скорость тела и состояние последнего
collision analysis: `IsSupported`, live `SupportBody`, нормаль. При отсутствии опоры
ID/normal равны default. Это не отдельный raycast. Телепортация тела/опоры и удаление
опоры немедленно снимают старое ограничение и support state, пробуждая персонажа;
следующий Step ищет новую опору. Телепортация сохраняет заданную линейную скорость,
если caller отдельно не очистил её. SetVelocity допускается только с нулевой
angular velocity; pitch/roll teleport и Sleep контролируемого тела запрещены.
Весь командный batch проверяется до мутации, включая соседние обычные тела.

| Options | Default | Допустимо |
| --- | --- | --- |
| MaximumSpeed, м/с | 6 | finite, `0 < value <= 50` |
| MaximumSlopeRadians | π/4 | finite, `0 < value <= 1.5` |
| MaximumHorizontalForce, Н | 1000 | finite, `0 < value <= 100000` |
| MaximumVerticalForce, Н | 1000 | finite, `0 < value <= 100000` |
| JumpSpeed, м/с | 5 | finite, `0 < value <= 50` |
| MaximumStepHeight, м | 0 (выключено) | finite, `0 <= value <= 1` |
| MinimumTreadDepth, м | 0,30 | finite, `0 < value <= 2`, в том числе при выключенном шаге |

Границы настроек — admission policy, не гарантия для произвольного сочетания
масс/сил/геометрии. Общие world motion/contact/pool limits сохраняются.
Нет отдельного таймера, crouch, root motion, сложной локомоции или ragdoll.

## Опциональный автоматический шаг

Положительный `MaximumStepHeight` включает подъём в обоих профилях BEPU.
Вертикальная капсула начинает на плоском статическом несенсорном боксе: центр
должен оставаться над его следом, а ходьба уже замедлиться у препятствия.
Первое препятствие должно быть примыкающим статическим несенсорным боксом
без поворота. Проверяются высота, остаток проступи вдоль ввода и зазор для всей
капсулы при подъёме и переносе. Неколлидирующие пары и сенсоры игнорируются;
прочие солиды могут блокировать шаг, но mesh и подвижная опора не служат ступенью.

Успешный шаг смещает капсулу внутрь проступи до collision analysis текущего Step,
сохраняя XZ-скорость и обнуляя её Y-компоненту. Опору публикует solver, контакты
не инвалидируются как при телепортации. Отказ не корректирует позу/скорость;
прыжок, спуск и падение с края по-прежнему обрабатываются обычным контроллером.

## Сессия Aether

`PhysicsComponentRegistry.Register` добавляет две точные схемы с одним TypeId.
`PhysicsComponentSchemas.Character` v1 не менялась: speed/slope/horizontalForce/
verticalForce/jumpSpeed; обязательна схема Body. `PhysicsComponentSchemas.CharacterV2`
сохраняет эти поля и добавляет Number `maximumStepHeight` (0) и `minimumTreadDepth`
(0,30) в метрах. Без положительной высоты автошаг выключен: старые сцены
не мигрируют, а v2 без указания новых ключей ведёт себя как v1. Границы
проверяет `PhysicsCharacterOptions.Validate()`; явный ноль высоты не подменяется.

`PhysicsCharacter` — reference-free настройки, включая `MaximumStepHeight` /
`MinimumTreadDepth` для runtime. `new PhysicsCharacter()` имеет defaults,
`default(PhysicsCharacter)` невалиден. Настройки фиксируются до native
mutation; изменение descriptor пересоздаёт контроллер с сохранением тела.

`PhysicsCharacterInput` содержит world XZ velocity и `JumpSequence`. Начальное
значение0 — нет запроса. Для новой попытки увеличить UInt64 sequence; одинаковое
значение не повторяет прыжок, скорость при этом менять можно. Уменьшение запрещено;
sequence сохраняется при пересоздании body/controller одной живой entity. Runtime
support/inputs/native handles не входят в persisted schema.

При private scene preparation подсистема добавляет отсутствующие Input/State роли.
В runtime использовать `PhysicsEntityCommands.AddCharacter`/`RemoveCharacter` на
существующем PhysicsBody; `DeclareWrites` включает роли. `RemoveBody` снимает все
character роли. Неполные/orphan роли и внешняя правка `PhysicsCharacterState`
отвергаются preflight до Step и любых ECS writebacks. Команды, заменяющие позу,
идут через существующий `PhysicsTeleport`; правила root/unit-scale/ExternalMotion
из [PHYS5](ECS_BRIDGE.md) сохраняются.

В той же Movement фазе после Step публикуются LocalTransform, PhysicsVelocity и
`PhysicsCharacterState(IsSupported, SupportEntity, SupportNormal)`. Contact events
сохраняют прежнюю next-tick Gameplay семантику. Pause не вызывает Step; ручной Paused
Step работает. Cleanup controller предшествует body removal; весь world принадлежит
scene subsystem. SDK не зависит от BEPU/Runtime, runtime adapter — от BEPU/Tooling/UI.
Опциональные SDK не добавляются автоматически в fixed game-artifact ABI/Mod shared
assembly list; прежние ограничения PHYS5 остаются.

## Реализация и подтверждённые сценарии

Адаптирован BEPU demo commit `29ed466870534c413aba9eeccd813cf15d135d0b`, включая
одно- и двухтелые motion constraints. [Provenance/изменения](../../TrueMoon.Kton.Bepu/Characters/THIRD-PARTY.md)
и полный Apache-2.0 license входят в provider package. Оригинальный `.tt` сохранён;
TrueMoon сопровождает адаптацию. Demo camera/input/renderer не включены.

| Поведение | Проверка |
| --- | --- |
| Пол, capsule, скорость, торможение; обе60Hz настройки | `Capsule_FindsFloorMovesAtRequestedVelocityAndBrakes` |
| Мировые XZ независимо от yaw | `VelocityInput_UsesWorldXZRegardlessOfCapsuleYaw` |
| Швы треугольников и отдельных mesh тел | `MeshSeams_AcrossTriangleAndBodyBoundariesKeepSupport` |
| Slope limit и переход через вершину | `SlopeLimit_AcceptsWalkableAndRejectsSteepContact`, `SlopeCrest_AscendsAndDescendsWithoutLosingTheSurface` |
| Линейная/вращающаяся платформа | `MovingPlatform_TransfersLinearVelocityAndKeepsRelativePosition`, `RotatingPlatform_TransfersContactPointVelocity` |
| Прыжок, потолок, динамическая отдача | `Jump_IsOnePulseAndAnAirborneAttemptDoesNotBoostOrQueue`, `LowCeiling_BlocksJumpWithoutPenetrationAndAllowsLanding`, `JumpFromDynamicSupport_AppliesOpposingImpulseToSupport` |
| Push dynamic body | `Character_PushesDynamicBodyWithSolverResponse` |
| Teleport/потеря опоры | `CharacterTeleport_DropsOldSupportAndReacquiresOnlyAtDestination`, `RemovedOrTeleportedSupport_IsInvalidatedBeforeNextStepAndCharacterFalls` |
| Identity/ownership/inertia | `CharacterIds_RejectDefaultForeignStaleAndNeverAliasReusedSlot`, `DetachedController_RestoresRotationalResponseToOffCentreContact` |
| Aether, оба DI, pose/support/render | `AuthoredCharacter_MovesAndPublishesSupportAndRenderPoseInSameTick` |
| Runtime lifecycle/invalid whole-scene preflight | `RuntimeHelpers_DetachReattachReplaceAndDeleteReleaseControllerOwnership`, `InvalidCharacterEdit_FailsBeforeNativeTickOrNeighborWriteback` |
| Aether auto-step: legacy v1, v2 defaults, высота 0,25/0,30 м на обоих профилях | `AuthoredAutoStep_UsesExplicitOptInAndPublishesSupportWithPose` |
| Aether auto-step после Pause/Step/Replace, оба DI | `AutoStepSession_PauseAndStepThenReplaceReloadsPolicyAndRebuildsSupport` |

56 новых cases +68 PHYS5:124passed каждый Debug/Release,0failed/0skipped.
Independent consumer выполняет52checks на каждый source Debug/published Release/
fresh NuGet Debug/Release профиль, оба DI. PHYS2–3 regression309checks×3 отдельно.
Полные16suites, GPU/physical, other OS и PHYS8 performance/release matrix в эту
проверку не входят. Нет утверждения, что render primitive совпадает с capsule collider.
