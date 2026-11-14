# Kton PHYS1: нейтральные контракты v1

**Решение2026-11-12 по запросу пользователя, после PHYS0.**
[План](PLAN.md#phys1), [статус](STATUS.md), [история проверок](HISTORY.md#phys1-2026-11-12),
[сводка PHYS1](PHYS1_RESULTS.json), [доказательства/пределы PHYS0](HISTORY.md#phys0-2026-11-12).

Это первая компилируемая alpha-граница API. В `TrueMoon.Kton` реализованы значения,
профили, typed errors и pure validation. `IPhysicsWorld` задаёт обязательства будущего
provider; сама neutral сборка не содержит simulation/allocator/resource owner.
PHYS2–3 реализовали rigid-body provider/queries/events отдельно; PHYS4 —
[public App/configuration/DI/lifetime](APP.md). Aether bridge остаётся PHYS5.
Проверки этого документа не объявляют эти этапы пройденными. Изменение API по итогам
реализации фиксируется явно, совместимость не выводится из alpha package version.

**Дельта PHYS2,2026-11-12:** [provider guide](PROVIDER.md) фиксирует реализацию.
В IPhysicsWorld добавлен `Capabilities`/`PhysicsWorldCapabilities`: PHYS2 объявляет
только RigidBodySimulation. Queries/ContactEvents явно UnsupportedProfile;
0published events не означает отсутствие физических collisions. Consumer требует
нужные flags до публикации session. Sleep очищает pending force, SetVelocity её
сохраняет. Current neutral API заново проверен131checks в3scopes; исходный PHYS1
manifest остаётся датированным evidence, не переписывается.

**Уточнение PHYS3,2026-11-12:** Queries/ContactEvents реализованы и включены вместе
с RigidBodySimulation; прежняя дельта PHYS2 выше — исторический срез. [Точная policy](QUERIES_CONTACTS.md)
задаёт per-buffer MaxContactEvents, collector+conservative BVH stack scratch budget,
mesh front-side start-COM culling и End-before-Begin для одного pair key после teleport.
Public signatures сохранены;309provider checks/profile и current neutral131×3.

## 1. Сборки и создание мира

| Сборка | Принятая граница |
| --- | --- |
| `TrueMoon.Kton` | Только BCL/System.Numerics: `IPhysicsWorld`, values, profiles и validation. Уже создана; package dependencies отсутствуют |
| `TrueMoon.Kton.Bepu` | PHYS2–3 rigid-body provider с exact BEPU packages, bounded Queries/ContactEvents и resource owners; без Aether/Tooling/UI |
| `TrueMoon.Aether.Kton` | Будущий bridge, без BEPU в ECS/scene contracts; размещение относительно Abstractions/Runtime уточняется ARCH3 |

В этом этапе `TrueMoon.Kton` не требует даже TrueMoon.Contracts: определения
данных и world interface не используют его типы. Composition root provider
регистрирует существующий **`IFactory<IPhysicsWorld, PhysicsWorldOptions>`** из
TrueMoon.Contracts. Новых factory abstractions/scopes не вводится. Factory result
caller-owned, сессия освобождает его после join; DI aliases не создают второго
владельца. Публичный UseBepuPhysics/UseConfiguredBepuPhysics реализован и проверен
в PHYS4 на Cobalt/Microsoft DI; [App guide](APP.md). У библиотеки только контрактов нет собственного
таймера или owned ресурсов, поэтому фиктивный `UseKton` в PHYS1 не нужен.

Роли namespace соответствуют файлам: корень `TrueMoon.Kton` — world/options/errors/
validation; `Bodies`, `Shapes`, `Joints`, `Queries`, `Contacts` — предметные подсистемы.
Не создаются папки по виду типов и не переносятся module-specific values в Contracts.

## 2. Единицы, геометрия и данные

- Метры, килограммы, секунды; right-handed, +Y вверх, quaternion XYZW. Pose — центр
  масс, unit scale; координаты по модулю <=10000. Ориентация должна быть finite/unit:
  `abs(lengthSquared-1)<=1e-4`; скрытой нормализации/decomposition нет. Default pose
  с quaternion0000 отклоняется, используется `PhysicsPose.Identity`.
- Box.Size — полные XYZ размеры. Capsule направлена вдоль локального +Y;
  CylinderLength — расстояние между центрами полусфер, полная высота `L+2R`.
  Нулевая длина capsule отклоняется: для сферы есть отдельный тип.
- Первое реализуемое множество: `PhysicsSphere`, `PhysicsBox`, `PhysicsCapsule`,
  `PhysicsStaticMesh`. Минимальная primitive dimension/radius0.001; полные размеры
  <=1000. Для sphere radius<=500, для capsule `L+2R<=1000`.
- Mesh: finite vertices, index triples, индексы в диапазоне, cross-product
  lengthSquared>1e-12; clockwise front winding, backface culling. Mesh только Static;
  Dynamic и Kinematic concave meshes возвращают UnsupportedGeometry. Closed-volume
  inside для concave/open mesh не обещается.
- Convex hull/compound из первоначального предложения плана **отложены** относительно
  начального API: PHYS0 проверил primitives/static mesh, не cooking/child-transform
  policy этих типов. Неизвестная производная ShapeDefinition отклоняется явно.
  Добавление требует отдельного расширения contracts/limits и provider acceptance.
- ReadOnlyMemory mesh input **заимствуется до возврата CreateShape**; readonly view
  не делает underlying array immutable. Caller не меняет его во время вызова;
  provider валидирует и захватывает собственные данные до публикации ID. Изменение
  source array после возврата не меняет shape. Shapes неизменяемы и принадлежат миру,
  разделяются несколькими bodies без shared Dispose. RemoveShape с body reference
  возвращает ResourceInUse.

Масштаб/reparent, visual origin↔centre-of-mass и S*R*T Aether преобразует bridge,
не Kton. Никакие matrix bytes между подсистемами не копируются как готовый contract.

## 3. Runtime ID и ошибки

`PhysicsWorldId(Guid)` уникален для **каждого создания** мира; повторное открытие
той же сцены получает другой ID. `PhysicsBodyId`, `PhysicsShapeId`, `PhysicsJointId`
содержат WorldId, неотрицательный Slot и uint Generation, начинающийся с1.
Типы различны; BEPU slots, EntityHandle, pointers и managed objects отсутствуют.

Default/zero generation/negative slot — InvalidIdentifier; другой WorldId —
ForeignWorld; отсутствующий/удалённый слот либо другое поколение — StaleIdentifier.
Проверки выполняются именно в таком порядке, до доступа к ресурсам. Значения ID
могут конструироваться provider/codec, но `IsValid` проверяет только структуру.
`PhysicsIdentity.Validate(owner, requested, actualLiveId)` реализует проверку
сопоставления; provider обязан передавать настоящий lookup результата слота,
default для отсутствующего, а не сам requested ID. Allocation/reuse/retirement реализованы provider PHYS2; нейтральная validation сама не выполняет lookup.

При удалении поколение не может ожить. После uint.MaxValue slot навсегда retire;
при исчерпании slots/generations — CapacityExceeded, без wrap/reset. На диске и
через scene documents сохраняются стабильные authoring IDs, а не runtime handles.

`PhysicsError(Code, Field)` — результат pure validation; default означает success.
`ThrowIfFailure` преобразует failure в `PhysicsException` с тем же error. Provider
использует те же коды, без разбора текста исключения. Полный каталог —
[PhysicsError.cs](../../TrueMoon.Kton/PhysicsError.cs): invalid input/ID, foreign/stale,
capacity, unsupported geometry/profile, ResourceInUse, Busy, Disposed, Faulted,
ContactOverflow, ScratchLimitExceeded, InvalidTick. Отмена до Step — стандартный
OperationCanceledException. Сообщения не включают все входные данные/окружение.

Pure validators не объявляют live body lookup, atomic mutation или backend
allocation проверенными. Для body validation используется actual captured shape;
отдельно обязательны live shape ID/ownership и current world state.

## 4. Мир, время, mutation и ownership

`PhysicsWorldOptions` и nested `PhysicsLimits` — sealed records с init-only values,
без delegates и изменяемых collections. Factory валидирует/снимает snapshot до
ресурсов; параметры не перечитываются из IConfiguration в tick. Configuration
binding/sources живут в App composition root, не в этом пакете.

Один внешний владелец сериализует mutation/query/Step/Dispose. Конкурентный либо
reentrant вызов — Busy. Мир сам не тикает; pause — отсутствие Step. Первый tick1,
дальше ровно LastCompletedTick+1; повтор/пропуск/overflow ulong.MaxValue — InvalidTick
до mutation. Delta фиксирована профилем; визуальная interpolation принадлежит consumer.

| Состояние | Разрешённое поведение |
| --- | --- |
| Ready | Admission, mutation, copied reads, queries, следующий Step, Dispose |
| Stepping | Workers выполняются синхронно; другие вызовы Busy, никаких callback в ECS/UI/IO |
| Faulted | После начавшегося сбоя — только Id/Capabilities/Options/State/LastCompletedTick и Dispose; дальнейшая симуляция запрещена |
| Disposing | Cleanup идёт либо завершилась ошибкой; owner остаётся у caller для retry |
| Disposed | Metadata читается; Dispose idempotent, остальные операции Disposed |

Step сначала проверяет state/tick/отмену/admission. Cancellation после начала не
абортирует BEPU: дождаться workers и закончить/зафиксировать failure. Полностью
успешный Step публикует tick и полный event snapshot; частичный сбой не двигает
LastCompletedTick и переводит мир в Faulted. Rollback/replay того же tick не обещаны.
Dispose не забывает Simulation/pools/worker owner при ошибке и не сообщает Disposed
до реальной очистки. Детальные retry/failure proofs — PHYS2/4.

## 5. Bodies, материал, joints и команды

`PhysicsBodyDescription` задаёт shape, motion, pose, mass, velocities, material,
layer/mask и sensor. Static имеет mass0/velocities0; Kinematic — mass0, явные
скорости без gravity; Dynamic — mass0.01..10000, solver-owned pose. Допустимый
live dynamic mass ratio<=100 — консервативная admission policy, проверяется provider
включая candidate до публикации; это не доказанный предел устойчивости BEPU.
Угловая скорость input<=64rad/s, gravity magnitude<=1000m/s².

`PhysicsMaterial` содержит friction0..4. **Pair friction=min(A,B)**: симметричное
нейтральное правило, которое provider обязан реализовать/проверить; смешанные
материалы PHYS0 не проверял. Contact spring/damping/recovery задаются **только
профилем мира**, поэтому arbitrary soft-contact override не ослабляет FastLinear.
Restitution coefficient не вводится: BEPU spring semantics не подменяются чужой моделью.

Layer — index0..31, Mask — uint bits; обе стороны должны разрешить пару. Mask0
отключает столкновения. Sensor не имеет solver response. Для body/body событий v1
нужно хотя бы одно Dynamic тело; static/static и kinematic/kinematic sensors не
объявляются отдельным broadphase sensor engine.

Первый joint — `PhysicsBallSocket`, anchors в local centre-of-mass frames,
spring30Hz/damping1, connected collision disabled. Оба body non-static, хотя бы
одно Dynamic; distinct/live/same-world IDs обязательны. Angular joint limits,
hinge/motor и ragdoll graph — последующие расширения. RemoveBody с существующим
joint — ResourceInUse; caller сначала удаляет joints, скрытого cascade нет.

`ApplyCommands(ReadOnlySpan<PhysicsBodyCommand>)` заимствует batch синхронно.
Весь batch проверяется **до первой записи**, включая ordered state-dependent
velocity effects, live IDs, modes, remaining command/event budget. Сумма batch sizes
между успешными Step ограничена общим лимитом; разбиение на маленькие batches его
не обходит. Shape/body/joint create/remove имеют собственные admission counts.

| Команда | Эффект |
| --- | --- |
| SetVelocity | Dynamic/Kinematic: обе скорости, wake; explicit speeds в envelope профиля |
| ApplyLinearImpulse | Dynamic, импульс в центре масс, wake; provider проверяет результирующую скорость |
| ApplyLinearForce | Dynamic, одно F*fixedDelta для следующего Step, wake; pause не умножает силу на wall-clock time |
| Teleport | Pose discontinuity, скорость сохраняется, wake; прежние contact identities завершаются, interpolation reset; Static также допускается |
| Wake/Sleep | Dynamic; sleep обнуляет скорости и pending force, сохраняет contact identity; BEPU может усыплять connected island |

Linear force/impulse input magnitude<=1e6, finite. Невалидные параметры/режимы
отклоняются до mutation; непредвиденный failure уже начатой mutation — Faulted,
без обещания transaction rollback. После интеграции nonfinite/out-of-envelope
state должен стать наблюдаемым Faulted; молчаливое clamp/телепортирование не принято.
Границы служат защитой численной области, а не доказательством CCD/solver quality.

## 6. CCD и quality profiles

`PhysicsProfileSettings.For(...)` возвращает immutable known settings. Неизвестный
enum — UnsupportedProfile, без downgrade/fallback. Пользователь выбирает профиль,
а не независимо комбинирует substeps, sweep и softness.

| Параметр | Baseline60 | FastLinear60 |
| --- | --- | --- |
| Fixed dt / workers | 1/60s /1 | 1/60s /1 |
| Velocity iterations / solver substeps / collision steps | 8 /4 /1 | 8 /4 /1 |
| CCD | Speculative, без sweeps | Swept, sweep-assisted speculative contacts |
| Maximum speculative margin | float.MaxValue, unbounded | 0.01m |
| Minimum sweep timestep / convergence | Не используются,0 | 1e-6s /1e-6s |
| Contact spring / damping / recovery | 120Hz /1 /2m/s | 600Hz /1 /2m/s |
| Explicit linear velocity admission | <=60m/s | <=600m/s |

Baseline сохраняет обычный PHYS0 preset. FastLinear перенимает **конкретную**
проверку sphere radius0.1, speed600, wall0.04, первый impact плюс60ticks,
position tolerance0.005. Spring120+Swept пропустил сферу сквозь стену и **не является
доступным пресетом**. Изменение sweep tolerance либо только substeps это не исправило.
600Hz — минимальная проверенная частота из диагностической матрицы PHYS0, а не
математически найденный минимальный порог для всех сцен.

Нет гарантии no-tunnelling для любых shapes/thickness/relative velocity, angular
motion, moving targets, multiple/secondary impacts или arbitrary mass ratio.
Особенно не выводить angular CCD из численного допуска AngularVelocity. Contact
spring может давать отскок; Swept не является hard TOI clamp. Проверки расширенной
геометрии/скоростей/материалов обязательны PHYS2/8. Provider не может объявить новый
профиль поддержанным только потому, что neutral enum компилируется.

## 7. Лимиты и точность

Значения в `PhysicsLimits` можно **уменьшать** до допустимого минимума. Повышение
ceilings требует новой явной задачи и evidence. Существующие поля не превращаются
в backend allocator capacity/гарантированное резервирование памяти.

| Admission/diagnostic policy | Значение/ceiling v1 | Область |
| --- | ---: | --- |
| MaxBodies / MaxShapes | 1024 /2048 | Все body modes вместе; shared shape считается один раз |
| MaxJoints / MaxCommandsPerTick | 4096 /4096 | Joints live, commands cumulative между Step |
| MaxContactEvents | 8192 | Capacity каждого raw/cache/pending-End/published buffer, bounded отдельно; overflow fault |
| MaxQueryHits / MaxQueryScratchBytes | 256 /1MiB | Returned prefix / collector и conservative traversal-stack payload; не retained allocator blocks |
| MaxMeshVertices / MaxMeshTriangles | 65536 /65536 | На одно captured mesh |
| MaxMeshInputBytes | 16MiB | vertices.Length*12 + indices.Length*4, long arithmetic |
| PoolDiagnosticBudgetBytes | 16MiB/world | Sampled BEPU pool threshold; не hard total-RAM cap |
| StepDiagnosticBudgetMilliseconds | 1ms, настраиваемое положительное finite | Diagnostic target, не hardware-independent runtime gate |

Минимумы counts1, vertices3, mesh input48bytes; upper profile ceilings проверяются
pure API. Pool/scratch ownership и runtime enforcement — provider acceptance.
4096joints/commands,256hits, mass/size/coordinate envelopes — **принятые policy
границы**, не результаты полного физического workload PHYS0. Только наблюдённый
baseline PHYS0:100active bodies/600ticks, p99Release0.564ms, high-water5,308,416bytes;
16MiB pools и1ms p99 нельзя распространять на все1024bodies/4096joints.

Допуски PHYS0 остаются acceptance fixtures: free motion1e-5–1e-3m,
box/capsule rest0.04/0.06m, BallSocket anchor0.04m, sweep0.01m, CCD0.005m.
Они не являются contact slop или обещанием точности каждой операции. Ties queries
сравниваются по вычисленной distance, без неявного fuzzy equality. Fixed step и
сортировка не обещают cross-platform bitwise determinism.

## 8. Queries и lifetime результата

`PhysicsRay` — origin/unit direction/positive MaximumDistance<=20000m. Sweep —
translational convex shape + pose + unit direction/distance; angular sweep и concave
query shape отклоняются. Overlap — exact convex test, AABB candidates не результат.
Filter отдельно задаёт layers/sensors и optional **live** excluded body; default
ExcludeBody означает отсутствие исключения, malformed/stale/foreign — ошибка.

RaycastNearest возвращает bool и copied hit. Raycast/Sweep возвращают ближайший
prefix не больше min(span.Length, MaxQueryHits), сортировка distance → body slot →
generation → subshape. Overlap сортируется body slot → generation → subshape.
При exact tie используется этот порядок, callback order BEPU не является API.
Mesh subshape — triangle index, primitive=-1; по каждой subshape максимум один hit.
Body-owned shape неизменяем в v1, поэтому generation/subshape identity стабильна.

Caller владеет Span и его содержимым после возврата; только WrittenCount элементов
инициализируется, хвост не трогается. Empty output — допустим: count0, Truncated=true
если hits существуют. Truncated не является fault и не обещает total hit count.
Если сам scratch budget не позволяет выполнить bounded query — ScratchLimitExceeded,
без бесконтрольного роста и частичной записи output. Provider обязан выбрать
bounded nearest-prefix algorithm, не собирать все candidates в unbounded List.

Ray внутри convex volume и sweep с initial overlap: distance0, StartedInside=true,
HasNormal=false, Point/Normal не определены. Mesh backfaces cull; закрытый volume
для static mesh не синтезируется. Overlap возвращает только identities, не выдуманные
нормали/глубины из stationary sweep. Столкновение/касание — narrowphase hit, без
добавления speculative-only AABB hits. PHYS0 подтвердил лишь часть этих сценариев;
many-hit/ties/inside/backfaces runtime semantics проверены PHYS3 в [указанной матрице](QUERIES_CONTACTS.md).

## 9. Contacts, overflow и наблюдаемость

Events — owned value payloads, один gameplay transition на normalized
body-generation/subshape pair, опубликованный после worker join в успешном tick.
Нормализация A/B: slot/generation/subshape; normal A→B. Representative contact —
наиболее глубокий физический contact, tie по world point XYZ; Depth>=0.
Speculative-only contacts не вызывают gameplay Begin. Sensor помечен явно.
Это новый reduction contract, а не обещание raw PHYS0 manifold order.

- Begin — новая пара; Stay — сохраняющаяся awake пара. Sleep удерживает identity
  без Stay каждый tick; wake продолжает Stay, не создаёт ложный Begin.
- End — separated/body removed/teleported; HasContact=false. Delete/teleport
  queues End со старым полным ID на следующий успешный tick; новая body generation
  не наследует старую пару. Teleport затем может породить новый Begin.
- Workers не вызывают пользовательский код, ECS/DI/UI/IO. Заполняют bounded local
  buffers; owner join/merge/sort. Worker callback не бросает исключение при overflow.
  После join overflow — ContactOverflow/Faulted, без публикации неполного snapshot.
- Pending transitions и event admission также bounded; operation до Step, которая
  уже знает, что не сможет сохранить End, отклоняется до mutation. При неизвестном
  размере после simulation overflow переводит мир в Faulted; gameplay events
  молча не теряются и не заменяются диагностическим warning.
- CopyContactEvents копирует **полностью** last-successful snapshot; недостаточный
  output — CapacityExceeded без записей. Повторное копирование разрешено, это не
  drain. Snapshot живёт до следующего успешного Step/disposal; owned caller copies
  независимы. Никаких refs на borrowed BEPU manifolds.

Aether bridge в PHYS5 сохраняет свой bounded event snapshot, потребляемый Gameplay
следующего tick. Same-tick reactions и дополнительные ECS phases здесь не введены.

## 10. Реальная приёмка и продолжение

[KtonContracts consumer](../../ManualTests/KtonContracts/README.md) исполняет реальные
pure ID/profile/validation checks и компилирует world usage. Проверяются struct/value
payloads, fresh package graph и actual DLL SHA. Fake physics implementation для
«прохождения» world semantics не создаётся.

PHYS2 реализовал [live allocation/retirement, admission и resource lifecycle](PROVIDER.md); исходные требования:
shape ownership, two worlds, bodies/joint/commands/Step, state faults и cleanup retry;
profile mapping на точный beta.33 и новые физические fixtures обязательны. PHYS3
реализовал [bounded queries, ordering/inside/backfaces, pair events/overflow](QUERIES_CONTACTS.md) в single-worker scope.
PHYS4 реализовал [public App/IFactory/options schema/DI lifetime](APP.md),192checks
на профиль (96Cobalt+96MicrosoftDI). Наличие neutral пакета и эти ограниченные
результаты не подтверждают ECS/platform/final release readiness.

## 11. Уточнение PHYS4: App ownership и defaults, 2026-11-12

Neutral API и PHYS1 ceilings не изменены. App provider регистрирует существующие
IFactory<IPhysicsWorld,PhysicsWorldOptions> и IFactory<IPhysicsWorld> как borrowed
недиспозабельные aliases. Create() использует validated startup defaults,
Create(options) полностью заменяет их explicit аргументами. До Kton startup и после
stop/dispose создание закрыто; параметры валидируются до backend allocations.

Единственный App owner — BepuPhysicsHost, он очищает failed unpublished creation.
Published world исключительно caller-owned: сессия join/drain/dispose, затем App.
App не тикает и не освобождает миры. Мир не заимствует App resources, поэтому
original owner может завершить Dispose после App shutdown, включая retry.
IsReleased host не означает отсутствие published caller worlds. Stop cleanup
синхронный и non-cancellable; failure сохраняет owner и закрытую admission.
Если App/DI закешировал Dispose failure, retry выполняется на заранее сохранённом
borrowed BepuPhysicsHost, без нового Resolve. Полная политика — [APP.md](APP.md).
