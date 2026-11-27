# TrueMoon.Kton: самостоятельный модуль физики и ECS Aether

**2026-11-17 — PHYS8.4 visual consumer реализован и проверен автоматически; PHYS8 в целом открыт.**
BEPUphysics v2 принят как **приоритетный backend**. Это выбор направления, а не
утверждение полной App/ECS/platform readiness. Exact beta.33 принят для ограниченного PHYS2 provider scope.
Пользователь выбрал имя **TrueMoon.Kton**: [решение и источники лора](NAMING.md).
[Текущий статус](STATUS.md), [правила планирования](../PLANNING.md).

<a id="current-physics-queue"></a>
## Текущая очередь

[PHYS0 завершён в research scope](HISTORY.md#phys0-2026-11-12): exact beta.33,
по114checks source Debug/published Release, известный CCD/material предел сохранён.
[PHYS1 contracts/validation завершён](HISTORY.md#phys1-2026-11-12): neutral BCL-only
API и131checks source/fresh package D/published R; [граница v1](CONTRACTS.md).
[PHYS2 rigid-body provider завершён](HISTORY.md#phys2-2026-11-12):184checks source/
fresh package D/published R, actual resources/cleanup; [provider guide](PROVIDER.md).
[PHYS3 queries/events завершён](HISTORY.md#phys3-2026-11-12):309checks source/fresh
package D/published R, [bounded storage/transition policy](QUERIES_CONTACTS.md).
[PHYS4 App/configuration/DI/lifetime завершён](HISTORY.md#phys4-2026-11-12):192checks
на профиль (96Cobalt+96MicrosoftDI), source/fresh package D/published R;
[публичное подключение и ownership](APP.md). Current PHYS2–3 regression309×3.
**[PHYS5 ECS bridge](#phys5) завершён2026-11-15**: [контракт](ECS_BRIDGE.md),
[68tests D/R и independent source/publish/fresh](HISTORY.md#phys5-2026-11-15).
Kul ARCH1h.2a завершён в nonspatial scope; [PHYS6 завершён](#phys6); [PHYS8.1 baseline](#phys81) завершён; [PHYS8.2 завершён](#phys82); [PHYS8.3 завершён](#phys83); [PHYS8.4 automatic scope проверен](#phys84); далее expanded workload и остальные PHYS8 gates. PHYS7 — отдельное необязательное развитие
простого ragdoll, не prerequisite первого модуля.

[PHYS6: opt-in шаг](#phys6-auto-step), [headless-корпус](#phys6-headless-corpus)
и [сессия Aether](#phys6-aether-auto-step) проверены на исходниках.
[Пакетная приёмка автошага](STATUS.md#phys6-aether-package-auto-step-2026-11-27)
прошла отдельно; точный T3 через ECS выделен в [отдельный пакетный gate](#phys6-t3-ecs).
Переход штатного движения игры остаётся отдельной задачей.

Подробные задачи физики, включая ECS bridge, ведутся **только здесь**.
[ARCH1i](../engine/PLAN.md#arch1i-physics) — ссылка из engine-направления.
Профили самостоятельной физики работают headless. PHYS5 использует целевые
Aether Abstractions/Runtime после ARCH3.1–3. PHYS8.4 добавляет оконный
визуальный consumer для проверки контроллера.

## Решение и границы

- Самостоятельный модуль фреймворка: 3D rigid-body simulation, collision queries,
  материалы, фильтрация, события контактов, сочленения и управление ресурсами.
  Он пригоден для headless-сервера и приложения без Aether, окна, Editor или GPU.
- BEPU — первая целевая реализация. Jolt остаётся альтернативой для будущей
  отдельной задачи; одновременная реализация двух backends не входит в этот план.
- Aether предоставляет адаптер к своим ECS/session/scene contracts. Правила
  здоровья, урона, взаимодействий, фракций и анимационные решения принадлежат игре.
- Простой ragdoll — набор тел и суставов для падения/реакции на импульс.
  Active ragdoll, физическая локомоция, автоподъём из лежачей позы, полный animation
  graph и сложное смешивание анимации не требуются сейчас.
- Cloth/soft bodies/fluids/GPU physics, транспорт и сетевой rollback не входят в
  первую готовность. Фиксированный шаг не обещает cross-platform bitwise determinism.

Контрольный результат первой версии: независимое App создаёт два изолированных
физических мира, управляет static/dynamic/kinematic телами, выполняет запросы и
получает контакты. Aether-сессия использует те же возможности через ECS; корректно
переживает spawn/delete, pause, Stop/Replace и освобождение ресурсов. Падение тел
и простой управляемый персонаж проверены отдельно от необязательного ragdoll.

## Пакеты и направление зависимостей

Имя модуля Kton выбрано пользователем. PHYS0 проверил точные backend packages;
PHYS1 создал TrueMoon.Kton с dependency-free contract/validation API, PHYS2 —
TrueMoon.Kton.Bepu с exact beta.33 и existing IFactory из Contracts. PHYS4 добавил
публичное App/configuration/DI подключение. PHYS5 добавил ECS bridge к Runtime.

| Роль | Содержание | Зависимости |
| --- | --- | --- |
| Physics API (`TrueMoon.Kton`) | World/body/shape/joint identity, descriptors, запросы/результаты, options, ошибки и validation; реализован PHYS1 | Только BCL/System.Numerics, без package/project dependencies; существующий IFactory из Contracts использует provider/App composition root |
| BEPU provider (`TrueMoon.Kton.Bepu`) | Simulation, pools/dispatcher, shape/joint storage, mapping и публичное App-подключение | Physics API, точные BepuPhysics/BepuUtilities; без Aether/Tooling/UI |
| Aether SDK (TrueMoon.Aether.Kton) | Reference-free components, exact scene schemas и command helpers | Neutral Kton + Aether.Abstractions; без Runtime/BEPU/Tooling/UI |
| Aether bridge (TrueMoon.Aether.Kton.Runtime) | App/DI, registry bindings, reconciliation/Step/writeback/contact projection и session ownership | SDK выше + Aether.Runtime; provider выбирает composition root |

Разделение provider оправдано независимостью публичных контрактов и Mod SDK от
BEPU. Дополнительная Physics.Abstractions сборка и универсальный plugin framework
не требуются. Расположение bridge components и runtime-систем по целевым сборкам
уточняется на карте ARCH3: Mod SDK не должен транзитивно получать concrete Runtime
или BEPU. При необходимости маленький contract-only bridge отделяется по реальному
dependency closure, а не автоматически.

Backend-specific типы не попадают в публичные descriptors, scene documents,
сообщения или ECS. Специфичные физические контракты остаются в модуле, не
переносятся массово в TrueMoon.Contracts. Файлы/namespace следуют смысловой
подсистеме и реальному пути; папки Contracts/Models/DTOs по виду типа не создаются.

## Предлагаемые технические контракты

Первоначальные роли ниже уточнены [контрактом PHYS1 v1](CONTRACTS.md) и реальными
сигнатурами TrueMoon.Kton. При расхождении краткого предложения с детальным v1
действует CONTRACTS.md; callback/factory types не создаются в обход общих IFactory.

### Мир, время и идентификаторы

- Один внешний владелец сериализует mutation/query/Step/Dispose. Мир не запускает
  свой таймер. Step получает следующий tick, delta берётся из immutable quality
  profile; interpolation принадлежит consumer. Pause — отсутствие шагов, не Dispose.
- PhysicsWorldId и generational handles тел/форм/суставов отделены от EntityHandle.
  Default/stale/foreign-world handles отклоняются; исчерпанные поколения не оживают.
  Сериализуются стабильные authoring IDs, а не слоты BEPU или адреса памяти.
- World owns Simulation и временные ресурсы; первый probe использует один поток.
  Дополнительные workers имеют явного владельца и предел; не занимать автоматически
  все logical CPUs без учёта rendering/других миров. Общий App worker budget —
  открытое решение PHYS0/4, а не новый неявный scheduler.
- Begin-step validation/cancellation выполняются до mutation. Начатая BEPU-симуляция
  завершается и дожидается workers; принудительный abort не допускается. После
  частичного сбоя состояние Faulted, дальнейший Step запрещён до пересоздания.
  Повтор того же tick не является автоматическим восстановлением/rollback.

### Геометрия, материалы и команды

- Явные единицы: метр, килограмм, секунда; right-handed/Y-up, Quaternion XYZW.
  Переход к row-vector S*R*T Aether проверяется известными позами, а не копированием
  матричных байтов между API. Центр масс отделён от визуального origin.
- PHYS1 v1: sphere/box/capsule и static triangle mesh; convex hull/compound явно отложены до отдельного cooking/child-transform контракта и evidence.
  Неподдержанные dynamic concave meshes, degenerate/nonfinite shapes и некорректная
  масса отклоняются до публикации тела. Capsule length/axis и scale policy явные.
- Static/dynamic/kinematic имеют разные правила записи поз/скоростей. Force/impulse,
  kinematic target и teleport — явные операции с определённым эффектом на wake/sleep,
  контакты и предыдущую позу; запись transform не становится скрытым teleport.
- Collision layers/masks, sensor/trigger и self-collision policy фиксируются до
  шага. Сочленения проверяют оба body handles; уничтожение тела учитывает связанные
  joints и shapes. Shared shape reference не создаёт второго владельца Dispose.
- Материалы отражают реальные BEPU friction/spring/damping/recovery semantics.
  Обычный коэффициент restitution не объявляется прямым stock-BEPU параметром.
  Произвольная эмитация отскока и combine rules требуют отдельного решения/проверки.
- CCD modes и ограничения angular/linear motion проверяются на точной версии.
  Допустимые step/substeps/iterations и mass ratios получают численные presets.

### Запросы и контакты

- Raycast, shape sweep и overlap имеют bounded caller-owned results, явные filters,
  nearest/all semantics, inside/start-overlap, backface и tie-breaking policy.
  Порядок callback результатов BEPU не выдаётся за устойчивую сортировку.
- Begin/Stay/End и sensor events содержат world/body identity, tick, pair/subshape
  identity и собственные value payloads; refs/borrowed manifolds не живут после callback.
  Sleep/delete/teleport отдельно определяют завершение контакта.
- Worker callbacks не обращаются к ECS, App resolver, UI или внешнему IO. Они
  заполняют bounded worker-local buffers; владелец сводит/сортирует их после join.
  Ошибки и overflow фиксируются без исключения из worker callback; после шага —
  наблюдаемый Faulted outcome. Молчаливая потеря gameplay contacts не допускается.
- Limits охватывают bodies/shapes/joints, pending mutations, hit/event capacity и
  scratch growth. Admission limits не равны доказанному жёсткому пределу RAM:
  внутренние BEPU contact/broadphase pools и их high-water mark измеряются отдельно.

### App, DI и configuration

- Публичное расширение IAppConfigurationContext выбирает BEPU provider,
  snapshots/validates options и регистрирует IFactory<IPhysicsWorld, ...> с явными
  runtime arguments. Это роль контракта, не принятая generic-сигнатура.
- Configure callbacks и common IConfiguration/schema binding используют общие
  utilities TrueMoon.Contracts. Новые независимые args/env/JSON precedence rules
  не создаются. Fixed step/limits/workers фиксируются до создания мира; изменяемые
  позы/команды/scene content не становятся configuration providers.
- Регистрация App-local; повторное подключение отклоняется или явно idempotent
  по замороженному контракту. Конкретные systems/services создаёт DI/IFactory.
  Resolve допустим только в composition root/factory infrastructure, не внутри tick.
- World factory result caller-owned; App и aliases не освобождают его вторично.
  Сессия завершает graph/physics tasks, удаляет bodies/joints, освобождает world,
  затем borrowed App dependencies. Ошибка startup/cleanup сохраняет владельца для
  retry. Dispose failed owner не забывается и не выдаётся за успешный Stop.
- Cobalt — штатный путь; существующий Microsoft DI adapter сохраняется и входит
  в проверки App integration. Новые scopes/async activation не нужны для этого плана.

## Интеграция с ECS Aether

### Существующие ограничения

Сейчас [EcsScheduler](../../TrueMoon.Aether.Runtime/Ecs/EcsScheduler.cs) последовательный,
фазы [Input → Gameplay → Movement → Transform → Extraction](../../TrueMoon.Aether.Abstractions/Ecs/EcsPhase.cs)
заканчиваются command playback. Component values не содержат managed references;
World не thread-safe, query refs/context ограничены invocation. Change notifications
и общий event channel ещё не реализованы. Новые сборки ARCH3 пока запланированы.

### Предлагаемый первый bridge

1. Value components описывают body/collider/material reference и desired movement;
   runtime mapping (полный EntityHandle ↔ physics handles, generation/revision)
   принадлежит session owner. Ссылки на managed Body/Simulation в World не хранятся.
2. В Movement выполняются ordered prepare → character/physics update → writeback
   systems. Library workers завершаются до возврата Execute. Character pre/post
   update относительно rigid-body step определяется выбранным алгоритмом.
3. Prepare видит только structural changes, уже прошедшие phase playback.
   Команды текущей Movement вступят в силу после неё, без скрытого промежуточного
   playback. Add/remove выявляются явным reconciliation до появления согласованного
   change journal; не предполагать несуществующую подписку на каждую component mutation.
4. Для dynamic bodies physics — единственный автор позы. Существующий
   [MovementSystem](../../TrueMoon.Aether.Runtime/Ecs/Motion/MovementSystem.cs) исключает
   такие entities. Kinematic targets/teleports приходят явными командами.
   Writeback объявляет Write соответствующих компонентов и сохраняет scoped access.
5. Первый профиль разрешает корневые dynamic bodies с unit scale. Родитель,
   nonuniform scale/shear и runtime reparent требуют reject либо отдельно принятого
   world-to-local решения; lossless decomposition не обещается. Static geometry scale
   запекается при подготовке, не в каждом tick.
6. Contacts публикуются после physics step в собственном bounded буфере bridge.
   Первый consumer обрабатывает их в Gameplay **следующего tick**. Same-tick reactions
   потребуют явно согласованного post-physics execution profile, не скрытого изменения фаз.
   Событие к удалённому/stale EntityHandle не применяется к новой entity того же слота.
7. Pause/Stop/failed startup/Replace имеют явные old/candidate/retiring owners.
   Candidate World/physics готовятся private и публикуются вместе; отказ preparation
   не изменяет old session. При невозможности корректного physics checkpoint consumer
   объявляет unsupported capability до Replace, без частичного "восстановления".
8. Mod unload/reload не оставляет delegates/managed types модов в BEPU callbacks.
   Мост хранит стабильные value IDs и проверяет отписку; смена mod-owned component
   schemas следует ARCH3 migration, а не маскируется перестройкой physics map.

### Content, сохранение и наблюдение

- Collision shapes готовятся вне tick; static mesh/convex inputs имеют format version,
  asset identity/revision/hash и limits. В первом headless профиле достаточно
  generated/in-memory fixtures; model importer/Nom/GPU не становятся prerequisites.
- Production asset delivery использует captured bytes/definitions и существующие
  resource owners. Runtime не открывает произвольные source paths и не запускает
  cooking на кадре. Editor использует тот же модуль через свой bridge/preview owner.
- Scene authoring хранит нейтральные descriptors. Backend binary state/handles не
  становятся форматом документа. Точный checkpoint/replay — отдельный contract:
  позиции и скорости сами по себе не подтверждают воспроизведение solver history.
- Debug draw выдаёт immutable/buffer-owned линии/формы; renderer/GPU подключаются
  только consumer. Метрики показывают steps, active/sleeping bodies, contacts,
  callbacks/overflow, allocations/pool memory и cleanup owners.

## Этапы и приёмка

PHYS0 выполнен в research scope, PHYS1 — contract/validation, PHYS2 — rigid-body
provider, PHYS3 — bounded queries/contact events scope2026-11-12. PHYS4–8 запланированы.
Все3capabilities реализованы; App/ECS/platform readiness не наследуется от этих checks.

<a id="phys0"></a>
### PHYS0 — package boundaries, baseline и изолированный BEPU probe

- Проверить PackageId/namespace/dependency closure выбранного Kton и bridge,
  сохранив постоянный путь docs/physics.
  Проверить текущие requests/claims по [правилам](../requests/README.md); прямой
  запрос пользователя не принимает существующую proposed game-заявку автоматически.
- Первым проверить BepuPhysics **2.5.0-beta.33** и согласованный BepuUtilities:
  exact nupkg/dependency graph/SHA/license, реальные runtime API и compilation на
  общем net11 профиле. CPM/project references меняются только отдельной реализацией.
- Headless probe: два мира; box/capsule/static mesh, force/impulse, sleep, CCD,
  ray/sweep/overlap, контакт и связанная пара тел; создание/удаление и cleanup.
- Начальный экспериментальный preset: fixed 1/60 s, один worker, до1024bodies,
  2048shapes,4096joints/commands,8192contact events и256query hits. Это границы
  probe, не доказанный memory budget или окончательные defaults публичного API.
- Измерить холодный запуск отдельно от прогретого; выбрать численные limits,
  tolerances и memory/step budgets до PHYS1. Не обходить дефект переходом на master
  без новой фиксации пакета. Stop condition: воспроизведённый blocker с fixture;
  решение — адресный fix/pin либо повторное сравнение backend, не скрытый fallback.

Приёмка: reproducible source Debug/published Release probe с manifest результата,
известными исходами и нулём потерянных owners; отдельно перечислены непроверенные
платформы. Точный package ещё не считается production-принятым.

**Выполнено2026-11-12, только research scope:**
[runner/source](../../ManualTests/KtonResearch/README.md),
[команды/решения/известные ограничения](HISTORY.md#phys0-2026-11-12),
[versioned outcome/hash manifest](PHYS0_RESULTS.json). Оба exact packages beta.33,
net8 assets скомпилированы и исполнены на общем net11/SDK11.0.100/runtime-async=on;
114/114checks каждый Debug/published Release,64cleanup cycles и owners/pool bytes0.
Production project/CPM/solution references не добавлены.

Численные research решения: 8iterations/4solver substeps/1collision step,
base spring120Hz/damping1/friction0.8/recovery2; fast CCD fixture spring600Hz,
sweep tolerance1e-6s, position tolerance0.005m. Continuous со spring120Hz в том же
600m/s fixture проходит тонкую стену — known limitation, сохранённая в каждом
отчёте. Это не общий CCD guarantee; PHYS1 должен явно определить quality profile.
Body/shape/sample capacities1024/2048/8192, nearest queries; предложенные4096joints/
commands и256hits ещё не admission limits. Sampled pool budget16MiB/world;
diagnostic p99<=1ms для100active bodies достигнут, не hardware-independent gate.
Полные допуски/границы доказательства — в README/HISTORY. Смена backend/pin не нужна
для проверенного ограниченного preset; production acceptance остаётся PHYS1–8.

<a id="phys1"></a>
### PHYS1 — публичные контракты и границы

После PHYS0: descriptors/IDs/errors/query lifetime, units/material/CCD/overflow и
supported profile; карта API не содержит BEPU/Aether types. Headless independent
consumer компилируется без Aether/Tooling/UI. Validation/stale/foreign IDs,
неподдержанная геометрия и согласованные tolerances имеют наблюдаемые outcomes.

**Выполнено2026-11-12:** [граница v1/численные решения](CONTRACTS.md),
[код](../../TrueMoon.Kton/TrueMoon.Kton.csproj), [runner](../../ManualTests/KtonContracts/README.md),
[команды и ограничения](HISTORY.md#phys1-2026-11-12), [manifest](PHYS1_RESULTS.json).
131/131checks source Debug, fresh NuGet Debug и fresh NuGet published Release;
Kton package dependencies0, actual DLL SHA проверен, targeted build0warnings/0errors.
Реализованы pure validation/identity comparisons и public values/interfaces;
live allocator/provider/query/event/lifecycle runtime ещё не реализованы.
Baseline60 и FastLinear60 не допускают независимого soft120+swept override;
ceilings/units/material/query lifetime/overflow и typed errors определены явно.
Следующий PHYS2 обязан реализовать live generation/retirement, state-dependent
admission, profile mapping и failure/cleanup proofs; many-hit/events — PHYS3.

<a id="phys2"></a>
### PHYS2 — BEPU provider, ресурсы и Step

После PHYS1: world factory, bodies/shapes/materials, forces/teleport/sleep, минимальные
joint primitives и dispatcher ownership. Проверки: два мира и shared shapes без
alias double-dispose; invalid admission до mutation; failed creation, boundary
cancellation, worker error, Stop/cleanup retry и длительное create/destroy без
неограниченного resource trend. Частичный Step не обещает транзакционного rollback.

**Выполнено2026-11-12:** [provider/ownership guide](PROVIDER.md),
[source/runner](../../ManualTests/KtonProvider/README.md),
[команды/границы](HISTORY.md#phys2-2026-11-12), [manifest](PHYS2_RESULTS.json).
184/184checks source Debug/fresh NuGet Debug/published Release; graph5packages,
actual DLL SHA, generations/retirement, shared captured shapes, bodies/BallSocket,
full command admission/fixed Step и creation/fault/cleanup proofs.
64world cycles и20×16live mesh churn дали нулевые owners/remaining pools и plateau.
Neutral API regression131checks в3profiles. PHYS2 добавил Capabilities для явного
отсутствия PHYS3 queries/events, уточнил Sleep clearing pending forces; дельта CONTRACTS.
Один worker и boundary fault injection не доказывают arbitrary internal BEPU.Dispose
failure recovery или multi-worker exception matrix. App/Cobalt/configuration — PHYS4.

<a id="phys3"></a>
### PHYS3 — queries, filters, sensors и события

После PHYS2: ray/sweep/overlap и bounded Begin/Stay/End. Проверить filters/self
collision, inside/backfaces/ties, sleep/delete/teleport, overflow/error и valid
pair identity при reuse handles. Repeated queries/contacts не требуют IO/Resolve;
worker callbacks не мутируют внешний World, не удерживают borrowed payload.

**Выполнено2026-11-12:** [policy/algorithms](QUERIES_CONTACTS.md),
[команды/границы](HISTORY.md#phys3-2026-11-12), [manifest](PHYS3_RESULTS.json),
[source/runner](../../ManualTests/KtonProvider/README.md).309/309checks source Debug/
fresh NuGet Debug/published Release и current neutral131×3. Query collector bounded,
mesh BVH и exact tests, live IDs/filter/sensor checks, ties/truncation/inside/backfaces,
scratch admission включая conservative traversal-stack reserve. Physical contacts
reduce в generation/subshape keyed Begin/Stay/End; sleep/delete/teleport/cancel,
raw/event/sleeping-cache overflow и owned snapshot проверены.309 включает184PHYS2
regression outcomes; new PHYS3 outcomes125. Capabilities Queries/ContactEvents включены.
Default counts не являются hard RAM/frame-time гарантиями, one-worker scope сохранён.

<a id="phys4"></a>
### PHYS4 — App/DI/configuration и lifetime

После PHYS2–3: публичный UseModule/UseBepu путь, schema/options snapshots и внедрённые
фабрики. Both-DI consumer проверяет регистрацию, duplicates, startup без создания
мира, independent caller-owned results, failed startup/stop/drain/dispose и retry.
В headless приложении отсутствуют окно/Editor/native physics binaries. Public API
names замораживаются только после проверки DI generation и package closure.

**Выполнено2026-11-12:** [App guide](APP.md), [consumer/runner](../../ManualTests/KtonApp/README.md),
[команды/границы](HISTORY.md#phys4-2026-11-12), [manifest](PHYS4_RESULTS.json).
UseBepuPhysics/UseConfiguredBepuPhysics,16fields schema Kton:DefaultWorld:*, detached
startup snapshot и typed/base IFactory aliases.192/192checks/profile (96на каждом
DI) source Debug/fresh NuGet Debug/published Release; actual Cobalt generated
constructor и9package closure,9package DLL SHA+1shared-framework DLL identity.
Startup без BEPU ресурсов; reverse consumer stop/startup rollback; failed candidate,
stop/final Dispose и retained-owner retry; repeated cleanup и App isolation проверены.
Сохранён PHYS1 caller-owned world: App закрывает admission/failed candidates,
сессия drain/dispose своих миров. Это явное ownership решение, не root fallback
для забытых миров. Current PHYS2–3 regression309×3; neutral suite отдельно не повторялась.

<a id="phys5"></a>
### PHYS5 — ECS/session bridge Aether

**Завершён2026-11-15** после PHYS4 и общего [ARCH1j](../engine/SESSION_SUBSYSTEMS.md).
[Контракт/принятое разделение SDK и Runtime](ECS_BRIDGE.md),
[проверки/команды](HISTORY.md#phys5-2026-11-15), [manifest/SHA](PHYS5_RESULTS.json).

Value components/schemas и DI factories, full preflight/reconciliation, exact60Hz
Step/writeback/ExternalMotion, phase spawn/delete/attach/detach/descriptor replacement,
explicit teleport и next-tick contact entities реализованы. Родители/nonunit scale/
incomplete roles/stale generations имеют явные outcomes. Scene subsystem owns свой
physics world, не borrowed World/dependencies; failed cleanup сохраняет owner/lease,
failed candidate не нарушает old session. Общий checkpoint/IPC не расширялся.

Приёмка:68tests D/R, exact slnx discovery68, common regression104D/R; independent
source Debug/published Release/fresh NuGet Debug/published Release по42checks/report
на обоих DI, final owners0/pools0.2new package metadata/payload, solution D/R0errors.
Headless/CPU acceptance отделена от actual GPU/physical/native-state reload scope.
Kul ARCH1h.2a также завершён; PHYS6 завершён16.11.2026, PHYS8 остаётся отдельным физическим развитием.

<a id="phys6"></a>
### PHYS6 — простой игровой персонаж — завершён 16.11.2026

Реализован ограниченный controller на основе BEPU example с сохранением
license/provenance и собственным сопровождением адаптации. Capsule, управление
скоростью, ground/support, slope limit, прыжок и взаимодействие с dynamic body.
Fixtures: ровный пол, швы mesh, подъём/спуск через вершину склона, moving platform,
низкий потолок, teleport и потеря опоры. Auto-stairs, crouch/root motion/сложная
локомоция не объявляются готовыми; их necessity и acceptance определяются отдельно.

[Контракт и матрица PHYS6](CHARACTERS.md), [проверки/команды](HISTORY.md#phys6-2026-11-16),
[manifest/SHA](PHYS6_RESULTS.json). Нейтральная optional capability, заимствованная
капсула, App-owned factory/caller-owned world, reference-free Aether roles и sequenced
jump. 56новых cases, полный Kton/Aether suite124passed каждый D/R; independent
consumer52checks×4 на обоих DI и provider regression309×3. Версия1.2611.16.2-alpha.
PHYS8 performance/platform/release acceptance остаётся самостоятельной задачей.

<a id="phys6-tales-route-baseline"></a>
**27.11.2026 — headless baseline персонажа Ø0,6 м на маршруте с крыльцом.**
Оба профиля прошли ровный пол, скос в обе стороны и осевой дверной проход.
На подъёме оба блокируются первой ступенью и изолированным уступом 0,25 м.
На спуске достигают улицы с промежутками без опоры. Задняя кромка комнаты
пересекается с последующим падением. [Результат и границы](STATUS.md#phys6-tales-route-2026-11-27).

На этом baseline автошаг был выключен. Для opt-in требуются подъём на 0,25 м
без прыжка с опорой в том же тике, отказ на 0,30 м, проверка глубины проступи
и зазора над головой. Спуск и удержание на краю — отдельные задачи,
без изменения общей механики падения.

<a id="phys6-auto-step"></a>
#### PHYS6 — опциональный автоматический шаг, 27.11.2026

Ограниченный CPU-срез реализован и проверен на `Baseline60` и `FastLinear60`:
upright capsule поднимается между примыкающими статическими боксами без прыжка.
Opt-in `MaximumStepHeight` и `MinimumTreadDepth` задают высоту и глубину проступи;
полноразмерные sweep проверяют зазор. Отрицательный исход не корректирует позу,
опору публикует обычный collision analysis. [Контракт](CHARACTERS.md) ·
[Проверки](STATUS.md#phys6-auto-step-2026-11-27).

Следом проверены [расширенный корпус](#phys6-headless-corpus) и
[опциональный шаг в Aether](#phys6-aether-auto-step). Произвольные лестницы,
удержание на краю и межплатформенный детерминизм не входят в срез.

<a id="phys6-headless-corpus"></a>
#### PHYS6 — расширенный headless-корпус, 27.11.2026

24 сценария прошли на `Baseline60` и `FastLinear60`: габариты проёмов и
настила, остановка и отход от тонкой стены, ступени со скосом в обоих
направлениях и падение после пересечения открытого края. Для смешанного
маршрута проверено точное совпадение 166 кадров каждой из четырёх пар запусков.
Низкая upright capsule в трубе проверяет просвет, а не вытянутую форму Тени.
[Прогон и ограничения](HISTORY.md#phys6-headless-corpus-2026-11-27).

<a id="phys6-aether-auto-step"></a>
#### PHYS6 — opt-in автошаг в Aether, 27.11.2026

`Character` v1 неизменна; точная `CharacterV2` принимает высоту и глубину,
оставляя автошаг выключенным по умолчанию. Через ECS на обоих профилях
проверены подъём на 0,25 м с позой и новой опорой в одном тике, отказ без
opt-in и отказ на 0,30 м. На `Baseline60`, на обоих DI, in-process Pause,
ручной Step и Replace подтверждают восстановление опоры в новом мире,
повторный подъём и освобождение старого мира при Replace, нового — при Stop.
[Контракт](CHARACTERS.md#сессия-aether) · [Проверки](STATUS.md#phys6-aether-auto-step-2026-11-27).
Пакетная CPU/headless поставка `1.2611.27.11-alpha` принята
[независимым consumer](HISTORY.md#phys6-aether-package-auto-step-2026-11-27).
Native Player и переход кинематики Tales не проверялись.

<a id="phys6-t3-ecs"></a>
#### PHYS6 — точный T3 через ECS, 27.11.2026

`BodyV2` добавляет authored static mesh из снимка CPU-геометрии App.
Фикстура сохраняет исходный двухтреугольный скос; полный допуск геометрии
проходит до native attachment, без обработки треугольников на тике.
[Контракт](ECS_BRIDGE.md) · [независимый gate](../../ManualTests/KtonEcs/README.md).

Критерий: в fresh Release из закрытого feed 88 headless ECS трасс совпадают
точно с независимым ECS повтором и direct provider. Проверяются реальные
опоры на скосе, поза/опора в тике автошага, отказ на 0,30 м, спуск и падение
с открытого края. Player, GPU и переход движения Tales — вне этого gate.
[Результат пакетной проверки](HISTORY.md#phys6-t3-ecs-2026-11-27).

<a id="phys7"></a>
### PHYS7 — необязательный простой ragdoll

После PHYS2–5, по потребности игры: набор capsule/box частей, ограниченные joints,
collision groups и начальная скорость/импульс при переходе в падение. Физические
части принадлежат ragdoll owner; создание ECS entity на каждую кость не обязательно.
Проверить падение на пол/лестницу, joint error, self-collision, sleep, удаление
сущности/сессии и передачу поз скелету при наличии animation consumer. Полный
active ragdoll/get-up не является критерием PHYS7 или первого выпуска.

<a id="phys8"></a>
### PHYS8 — поставка, наблюдение и итоговая готовность

- После PHYS4–6: source Debug/published Release и fresh isolated NuGet consumer
  для standalone module и Aether bridge. Использовать global.json, $(TF)/$(TestsTF),
  runtime-async=on и [ManualTests/BuildProfile.ps1](../../ManualTests/BuildProfile.ps1),
  явную CPM isolation; не добавлять сторонние SDK/TFM pins.
- Windows x64 — первый проверяемый профиль. Linux x64/ARM64 и macOS x64/ARM64 —
  последующие отдельные проверки; чистый C# не равен готовой матрице. Android/iOS/
  WebAssembly/консоли требуют отдельного решения AOT/SIMD/threading/поставки.
- Будущий performance workload: 100/1000/10000rigid bodies, sleeping и active
  отдельно; 1/32/256controllers; optional1/32/128ragdolls с фиксированным числом
  частей. Пробовать1/2/4/8workers на доступном CPU; превышение probe limits только
  явным preset. Фиксировать версии/CPU/SIMD, dt/iterations/substeps/CCD и качество.
- Измерять physics-only и полный ECS→physics→ECS участок, p50/p95/p99, allocations,
  managed/native pool memory, callbacks и teardown. Profiler/Debug и Release результаты
  не смешивать; численный performance target принимается после baseline PHYS0.
- Integration probes проверяют identity/events/cleanup. Визуальный debug consumer
  отдельно проверяет соответствие physics/render poses, frame interpolation и Stop;
  GPU renderer acceptance остаётся у Aether и не выводится из headless тестов.
- Описать App standalone/ECS usage, limits, supported platforms, version upgrade,
  unsupported checkpoint/replay и Known Issues; все actual commands/outcomes — в
  HISTORY после первых запусков. API/package readiness не наследуется от других модулей.

<a id="phys81"></a>
#### PHYS8.1 — Windows x64 baseline — завершён 16.11.2026

Без изменения production API/defaults/ceilings: отдельный App/DI consumer,
100/1000active/sleeping bodies,1/32/256walking controllers, standalone и полный
headless session tick, Cobalt/MS DI. Source/fresh Debug smoke по12workloads,
published source/fresh Release по28;120warmup+600samples, p50/p95/p99/allocations/
pool high-water/teardown.256-controller ECS явно выбирает разрешённый projection
capacity256.14пакетов одной persisted версии и fresh graph/output hashes проверены;
124functional tests каждый D/R. Все93PHYS6source hashes сохранились, версия1.2611.16.2-alpha
не повышается для нового non-packaged ManualTests consumer и документации.
[Метод/наблюдения/ограничения](PERFORMANCE.md), [история](HISTORY.md#phys81-2026-11-16),
[manifest/SHA](PHYS81_RESULTS.json). Общая PHYS8 readiness не объявляется.

<a id="phys82"></a>
#### PHYS8.2 — allocation attribution и адресная оптимизация — завершён 16.11.2026

Baseline зафиксировал около445КБ/tick в полном ECS-пути на1000active и sleeping
bodies и около56Б/tick в standalone32/256controllers. Сначала разделить вклад
session/scheduler/transforms/bridge и backend, затем убрать подтверждённые повторные
allocations/подготовку. Сохранить полный preflight до мутации, identity/generations,
phase-boundary attach/detach, ownership/retry cleanup, contact timing и controller
поведение. После production правок — новая общая версия до финальных проверок,
те же workloads и функциональная регрессия; стабилизировать warmup/повторения до
сравнительного timing target. Не назначать numeric SLA по одному macrobaseline.

Выполнено: [источники/реализация/сравнение](ALLOCATION_OPTIMIZATION.md),
[команды](HISTORY.md#phys82-2026-11-16), [manifest/SHA](PHYS82_RESULTS.json).
Одним новым probe наimmutable `.2-alpha`/`.3-alpha` установлено:1000body full ECS
~445КБ→440–842Б/tick(>99.8%),256controllers334КБ→210КБ(~37.26%). Вклад native,
bridge, movement, playback, transforms и extraction разделён primary-thread counters;
allocation traces подтверждают hot callsites. Reused scratch/immutable templates,
новый allocation-free motion validator, removals scratch/mass scan сохраняют полный
preflight/identity/lifecycle/contact timing.155tests D/R,131×3contracts,309×3provider,
52×4independent ECS и104macro/attribution fixtures прошли. Версия1.2611.16.3-alpha
выбрана до финальных проверок; timing SLA не назначался.

Оставшиеся shared command recording/playback allocations — отдельная следующая
измеренная цель: сохранить immutable reservation maps, batch tokens, generations,
full-batch validation и отсутствие удержания private component types/retired ALC.
Общий EntityCommandBuffer в этом срезе не менялся. Визуальные/platform/release gates
не закрываются наличием performance результата.

Остальные критерии PHYS8 выше сохраняются: explicit expanded capacity/worker profiles,
raw callbacks/stall diagnostics, visual pose/interpolation/Stop и platform/release
matrix.10000bodies/2/4/8workers требуют отдельного preset/решения, ragdoll optional.

<a id="phys83"></a>
#### PHYS8.3 — deferred-команды контактов — завершён 16.11.2026

Измеренная следующая цель PHYS8.2: убрать per-command objects и per-playback scratch
без изменения публичного ECS API/форматов/contact timing. Общий Runtime buffer
теперь использует value records, скопированные reference-free payload blocks,
reused validation/targets/reservations и bounded instance handler cache. Full
preflight до commit, cancellation retry/fault behavior и independent published maps
сохранены; collectible Type/closed generic handlers освобождаются при завершении
batch/reset/dispose, World component pools сохраняют отдельное владение.

[Реализация/измерения/границы](DEFERRED_COMMAND_ALLOCATIONS.md),
[команды](HISTORY.md#phys83-2026-11-16), [manifest/SHA](PHYS83_RESULTS.json).
Один probe/immutable `.3-alpha` и `.4-alpha` cohorts:256controllers full ECS
209719–209732→6720Б/tick(~96.8%) на обоих DI; bridge48Б, playback6224Б на detached
result/256handles.20новых ECS cases +2contact cases, Aether3554/Kton157/Kul103passed
каждый D/R; solution0errors, independent ECS52×4 и104macro/attribution fixtures.
Первый full Debug с diagnostic50msStop timeout сохранён в evidence; новые forced-GC/
allocation tests изолированы в nonparallel collection, D/R повторены полностью.
Причинность timeout не установлена, production deadlines прежние. Версия1.2611.16.4-alpha
зафиксирована до финальных проверок. Timing SLA и общий PHYS8 release не объявляются.

<a id="phys84"></a>
#### PHYS8.4 — визуальный debug-consumer — автоматическая приёмка 17.11.2026

Реализован ManualTests/KtonVisual: actual PrimitiveGameSession/Kton и существующий
Vulkan/OpenGL primitive surface; floor/slope/platform/dynamic cubes/ceiling/capsule
controller. Интерполяция копий поз принадлежит consumer и не меняет Physics World.
Pause/Step/Stop/Reset, stale tickets/новая World identity и input edges проверены.
Капсула рисуется ступенчатым proxy, точная collider mesh не заявляется.

[Уровень/скриншот/запуск](VISUAL_PLAYGROUND.md), [команды](HISTORY.md#phys84-2026-11-17),
[manifest/SHA](PHYS84_RESULTS.json). 16 CPU/native профилей source Debug/published
Release/fresh NuGet Debug/published Release на обоих DI: 96 CPU-групп, 96 PNG,
validation 0/0 и final owners/handles/pools/UI sessions 0. Closed 19-package cohort,
cache/output/native/font SHA проверены. Общая Version 1.2611.16.4-alpha сохраняется:
production API/native/dependencies не изменены, consumer непакетируемый.

Ручной Win32 input/close остаётся открытым: Computer Use не смог надёжно наблюдать/
активировать окно; probe после отмены завершён по точному PID/command line.
Эта попытка отделена от успешной scripted-матрицы. Полные unit/solution suites,
Editor/Player IPC/ALC/reload, exact mesh, device loss/other OS/AOT не подтверждались.
Следующий независимый технический срез PHYS8 — расширенные 10k-body/worker presets
и raw callbacks/stalls; общая release readiness остаётся открыта.

## Источники решения

Первоначальное планирование12.11.2026 проверяло источники чтением. Последующий
PHYS0 в тот же день установил exact packages в изолированный cache и выполнил
source Debug/published Release; [свежие результаты и границы](HISTORY.md#phys0-2026-11-12).

- [BEPU repository](https://github.com/bepu/bepuphysics2) — C# rigid-body engine,
  SIMD, shapes, queries и constraints; Apache-2.0.
- [NuGet beta.33](https://www.nuget.org/packages/BepuPhysics/2.5.0-beta.33) —
  prerelease от11.10.2026, net8; stable2.4.0 от18.02.2022. Нужна точная проверка PHYS0.
- [beta.33 release notes](https://github.com/bepu/bepuphysics2/releases/tag/2.5.0-beta.33)
  и [изменение трения](https://github.com/bepu/bepuphysics2/pull/410) — продолжающееся
  сопровождение; обновление требует повторной проверки материальных presets.
- [BEPU Q&A](https://github.com/bepu/bepuphysics2/blob/master/Documentation/QuestionsAndAnswers.md)
  — границы determinism и material/restitution semantics; это источник для проверки
  выбранного пакета, не обещание совместимости любой версии.
- [Character example](https://github.com/bepu/bepuphysics2/tree/master/Demos/Demos/Characters),
  [Ragdoll example](https://github.com/bepu/bepuphysics2/blob/master/Demos/Demos/RagdollDemo.cs)
  — основания ограниченных PHYS6/7, а не готовая интеграция TrueMoon.
