# Kton: история проверок

[План](PLAN.md), [текущий статус](STATUS.md).

<a id="phys0-2026-11-12"></a>
## PHYS0 — exact BEPU packages и headless prototype, 2026-11-12

Прямой запрос пользователя: приступить к PHYS0 на общем .NET 11. Создан
[ManualTests/KtonResearch](../../ManualTests/KtonResearch/README.md), вне solution
и production graph. Приоритет BEPU подтверждён в исследованном профиле;
production PackageReference и defaults публичного API этим решением не принимаются.

### Пакеты, происхождение и границы

Оба официальных nupkg получены из NuGet flat container, проверены архивы/nuspec и
фактически загруженные DLL. Фиксация — **BepuPhysics 2.5.0-beta.33** и
**BepuUtilities 2.5.0-beta.33**, repo commit
`29ed466870534c413aba9eeccd813cf15d135d0b`, лицензия Apache-2.0.

| Пакет | nupkg SHA256 | Зависимости/содержимое |
| --- | --- | --- |
| BepuPhysics | `9C1DB047CD7B3597CD808D7B94A1EF6626E0041E305BB3D8FE41AA658C3F1D03` | BepuUtilities >=2.5.0-beta.33; один lib/net8.0 DLL |
| BepuUtilities | `D1CF1BB261720FEF05D197B4DE9F17E06BAC6D2A6C7574A6DE88BC21122C26EA` | Нет package dependencies; один lib/net8.0 DLL |

Exact direct references обоих пакетов и lock file с contentHash исключают
незаметное повышение транзитивной Utilities. В restored graph ровно две библиотеки.
Native/runtimes assets в архивах нет; это утверждение о двух проверенных пакетах,
а не готовность всех платформ/AOT. Лицензия проверена по nuspec, runtime SHA —
сравнением загруженной assembly с DLL в nupkg. [Версионированные данные](PHYS0_RESULTS.json).

Проверены реальные сигнатуры `Simulation.Create`, `SolveDescription`,
`INarrowPhaseCallbacks`, `IPoseIntegratorCallbacks`, BodyDescription/CollidableDescription,
BufferPool/ThreadDispatcher, RayCast/Sweep handlers, Mesh, BallSocket и disposal.
`IRayHitHandler.OnRayHit` принимает normal по значению; первая compile-попытка с
`in Vector3` исправлена до acceptance. Force в прототипе — внешний impulse `F*dt`,
не проверка встроенной persistent force API. Overlap — convex stationary sweep с
`OnHitAtZeroT`; это не готовый универсальный overlap API. Raw manifold callbacks
ещё не Begin/Stay/End события.

Целевые `TrueMoon.Kton` → Contracts/BCL, `TrueMoon.Kton.Bepu` → Kton/BEPU и
`TrueMoon.Aether.Kton` → Kton/Aether остаются границами плана. Коллизий таких
проектов в source tree не найдено; публичная доступность PackageId в NuGet не
проверялась и не резервировалась. Probe с RootNamespace KtonResearch зависит
только от двух BEPU packages/BCL; production assemblies не созданы. App/DI и ECS
подключение — PHYS4/5, Cobalt правила не подменяются исследовательской фабрикой.
Proposed game Request/Claim/Decision не менялись.

### Команда и воспроизводимость

Финальный запуск, все пять dotnet процессов exit 0:

```powershell
pwsh -NoProfile -File ManualTests/KtonResearch/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS0-final-20261112 -PackageDirectory E:/source/my/TrueMoon.Tales/TrueMoon/src/TestResults/KtonPHYS0-package-inspection-20261112
```

Для свежей машины опустить PackageDirectory и выбрать новый EvidenceDirectory.
Runner получает общий профиль через `ManualTests/BuildProfile.ps1`, сохраняет
его source SHA, создаёт изолированный consumer/CPM-off, local feed, config и cache.
Выполняются `restore --locked-mode`, source Debug build/run и FDD Release
publish/run; все MSBuild вызовы с уникальными binlog. Timeout каждого собственного
процесса 120 s, stdout/stderr и reap сохраняются. В процессе verification исходники
сверяются по SHA; actual DLL SHA совпал с архивами в обоих профилях.

Окружение: SDK **11.0.100**, runtime **11.0.0**, TF/TestsTF **net11.0**,
LangVersion latest, Features `;runtime-async=on`. Windows x64 build 26340,
AMD Ryzen 9 9900X, 24 logical processors; один BEPU worker, AVX2, Vector<float>.Count=8,
workstation GC. CPU name прочитан из локального registry, без профилирования CPU.

Raw evidence — игнорируемый `TestResults/KtonPHYS0-final-20261112`:
verification/probe JSON, logs, binlog, copied sources/profile/feed/private cache,
source-Debug/published-Release outputs. Переносимая сводка, outcome catalog,
profile/source/package/DLL/report hashes сохранены в [PHYS0_RESULTS.json](PHYS0_RESULTS.json),
поэтому вывод не зависит только от наличия локальных ignored файлов.

### Фактический результат

**114/114 проверок в каждом профиле**, owners=0 и bytes after cleanup=0.
Debug build — 0 warnings/0 errors; publish Release успешен. Сценарии:
два мира, gravity/free motion, impulse/force, kinematic motion; box/capsule/static
mesh, shared shape/delete, sleep/wake; ray/sweep/overlap/filter; raw contacts,
sensor callback без response, BallSocket; invalid mass/pose/world preset,
capacity rejection before add, idempotent Dispose, use-after-dispose; 64 цикла
создания/удаления/disposal с actual owner/pool counters, CCD 61 ticks.

| Измерение | Source Debug | Published Release |
| --- | ---: | ---: |
| Cold create + first Step + Dispose, ms | 68.7170 | 56.9097 |
| 100 active spheres, 600 measured ticks, p50/p95/p99, ms | 0.2957 / 0.4431 / 0.5866 | 0.2736 / 0.3341 / 0.5640 |
| Max measured Step, ms | 1.8801 | 1.3531 |
| Managed allocation delta за 600 measured ticks, bytes | 56 | 56 |
| Maximum sampled owned BEPU pools на один мир, bytes | 5,308,416 | 5,308,416 |
| Cleanup cycles / live owners / pool bytes after cleanup | 64 / 0 / 0 | 64 / 0 / 0 |

120 warmup ticks, sleep запрещён для measured 100 bodies. Таймер охватывает весь
research Step с reset samples и чтением pool counters; ECS отсутствует. Cold
измерение начинается внутри нового процесса после runtime checks, не включает
запуск ОС/dotnet. Это exploratory baseline одного прогона, не formal benchmark,
physics-only timing, сравнение с Jolt или гарантия FPS. Нулевые pool counters не
доказывают отсутствие всех GC/process leaks.

### CCD: обнаруженный предел и принятый research preset

Сфера radius 0.1 стартует x=-5, скорость +600, static wall thickness 0.04 при x=0.
Геометрическое положение центра у ближней грани x=-0.12. dt=1/60, 8 iterations,
4 solver substeps; sweep tolerances 1e-6 seconds, capped margin 0.01.

- Passive + capped margin — negative control: первый x=+5.000001, проход стены.
- Continuous + spring 600 Hz: первый x=-0.11897472 capped и -0.11897401 unbounded;
  оба остаются x<=-0.115 за 61 tick. Position tolerance 0.005; последующий небольшой
  отскок на ближнюю сторону допустим. Passive + unbounded также удерживает сферу
  speculative contacts; этот результат не приписывается sweep mode.
- **Known limitation:** Continuous + spring 120 Hz при том же capped fixture:
  первый x=+0.022162264, финальный +2.0216477 — стена пройдена. Он остаётся в каждом
  отчёте как diagnostic case, не маскируется общим `Completed=true`.

Предварительные attempts2–8 последовательно воспроизводили этот предел:
spring30/один solver step x=+0.1784935; замена temporal tolerance 1e-3→1e-6 не помогла;
spring30/4substeps +0.9007871; spring120/4substeps +0.022162264;
4 full collision steps при spring120/4substeps +0.16867298. Matrix margin0.01/0.1/1/
float.MaxValue сама проблему не устранила. Frequency600/1200/10000 в диагностической
матрице дали первый x около -0.119. Сохранён минимальный проверенный из этой матрицы
research вариант600, без перехода на master или замены nupkg.

Это наблюдение согласуется с spring-based contact response, но не является
доказанным upstream defect/root-cause analysis. Флаг Continuous не обещает жёсткий
TOI clamp; [документация зафиксированного commit](https://github.com/bepu/bepuphysics2/blob/29ed466870534c413aba9eeccd813cf15d135d0b/Documentation/ContinuousCollisionDetection.md)
описывает sweep + speculative contacts и spring materials. В первой acceptance
попытке неверное ожидание покоя без gravity заменено проверкой стороны стены:
результат отскока -1.9314 через секунду не означает проход стенки.

### Численные решения PHYS0 и продолжение

Только для research: fixed1/60, workers1, 8iterations/4substeps/1collision step;
base contact spring120Hz/damping1/friction0.8/recovery2, CCD fixture spring600Hz.
Лимиты bodies1024/shapes2048/raw samples8192, nearest query. Guards проверяют
bodies и sampled pools; shapes/sample capacities реализованы, их overflow stress
не является результатом текущего набора. 4096joints/commands и256query hits из
начального плана остаются предложениями PHYS1–3, не реализованными limits probe.

Tolerances: position/free motion1e-5–1e-3, mesh rest box0.04/capsule0.06,
joint anchor error0.04, sweep0.01, CCD0.005. Diagnostic step budget для measured
100 active bodies p99<=1ms достигнут в обоих profiles; это наблюдение, не обязательный
hardware-independent gate. Sampled pool smoke budget16MiB/world пройден;
allocation/RAM hard caps, parallel callbacks и product budgets требуют следующего этапа.

PHYS0 закрыт в ограниченном research scope. Далее PHYS1: нейтральные API/IDs,
validation/overflow, материал/CCD quality profile с явным исключением unsafe
soft-contact fast fixture, memory/step/shape/joint/query limits. Не переносить
фабрику/handles/experimental presets в API механически. Linux/macOS/ARM64,
mobile/WebAssembly/consoles, AOT/trimming, 2+workers, characters/ragdoll,
App/Cobalt/ECS, full solution suites и production NuGet delivery этим запуском
не проверялись. Результаты других модулей не наследуются.

<a id="phys1-2026-11-12"></a>
## PHYS1 — нейтральные contracts/IDs/limits/CCD, 2026-11-12

По прямому запросу пользователя определена [первая API граница](CONTRACTS.md) и
создан `TrueMoon.Kton`: BCL-only library на общем `$(TF)`, без PackageReference/
ProjectReference, BEPU/Aether/Tooling/UI/native dependencies. Проект включён в
solution отдельным Kton направлением; common CPM/SDK/версии не менялись этой задачей.
Provider, App/Cobalt/configuration integration и ECS bridge не реализовывались.

### Принятые решения и фактическая реализация

Runtime world incarnation(Guid) и типизированные body/shape/joint slot+uint-generation
IDs, default/stale/foreign errors и pure identity admission. Public world интерфейс,
options/limits, poses, static/dynamic/kinematic descriptors, primitive/static mesh
inputs, BallSocket, commands, bounded caller-owned query/event payloads, state/
typed errors определены без backend типов. `PhysicsValidation` реально исполняет
numeric/geometry/profile/structural-ID/capacity/tick/batch/query checks; он не
подменяет будущий live-slot allocator и state-dependent provider admission.

Существующий `IFactory<IPhysicsWorld, PhysicsWorldOptions>` из Contracts будет
регистрироваться provider/App composition root; neutral assembly не нуждается в
собственной зависимости от Contracts. Нет новой factory API, фиктивного UseKton,
service locator или прямого создания сервисного physics world в игровом коде.

Профили immutable: Baseline60 speculative/unbounded/120Hz, FastLinear60 swept/
margin0.01/tolerances1e-6s/600Hz. Оба1/60s,8iterations/4solver substeps/1collision
step/1worker. Independently adjustable softness отсутствует; известная PHYS0
комбинация soft120+swept не доступна как preset. Fast-профиль остаётся candidate
по одному sphere/wall fixture, без angular/arbitrary-scene CCD guarantee. Материал
задаёт friction0..4, combine=min; contact springs задаются профилем мира.

Counts/ceilings1024bodies/2048shapes/4096joints/commands/8192events/256hits;
query scratch1MiB, mesh vertices/triangles65536/65536, input16MiB; diagnostic pools
16MiB/world, step target1ms. Координаты<=10000m, primitive size0.001..1000,
dynamic mass0.01..10000kg/live ratio<=100, explicit speed60/600m/s, angular<=64rad/s.
Новые envelope/limits — policy, не утверждение измеренного физического workload.
Capsule cylinder length полная, dynamic/kinematic concave meshes unsupported.
Convex hull/compound отложены явно относительно первоначального предложения.

### Команда и результат

```powershell
pwsh -NoProfile -File ManualTests/KtonContracts/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS1-final-20261112
```

Runner [KtonContracts](../../ManualTests/KtonContracts/README.md) использует общий
BuildProfile, isolated consumer props/global.json/CPM-off, единственный local feed
и private cache без external sources. Source Debug consumer ссылается на настоящий
проект; затем Release pack создаёт свежий Kton-only nupkg, exact-version independent
consumer исполняется Debug и FDD published Release. Runtime DLL SHA сверён с DLL в
архиве; nuspec dependencies0 и assets graph ровно один package. SDK11.0.100,
TF/TestsTFnet11.0, runtime11.0.0, runtime-async=on, Windows x64. Все9dotnet processes
exit0; MSBuild вызовы имеют unique binlog; source hashes совпали в конце runner.

| Проверка | Исход |
| --- | --- |
| Source Debug real project consumer | **131/131passed**, build0warnings/0errors |
| Fresh NuGet Debug consumer | **131/131passed**, actual package DLL SHA matches |
| Fresh NuGet published Release consumer | **131/131passed**, actual package DLL SHA matches |
| Release library build/pack | Успех; nupkg `0.3.0.1-alpha.phys1.20261112133059`, dependencies0 |

Проверки охватывают default/foreign/removed/reused-slot IDs и exhausted-generation
comparison, typed error, zero/NaN/invalid options/pose/shape/mesh, mesh byte/count
limits, capacities с overflow arithmetic, tick repeat/skip/wrap, body modes/mass/
speed/friction/layer masks, command batches/cumulative capacity/invalid empty-world,
query shape/direction/exclusion и unmanaged payloads. Public `IPhysicsWorld` usage
компилируется, но **не исполнялся с fake provider**. Actual live retirement,
simulation/mutation/no-rollback, queries sorting/truncation/inside/backfaces,
contact/event overflow и cleanup state transitions остаются PHYS2–3.

Первый source/pack run уже прошёл130checks в трёх scopes, но имел2XML warnings
из-за literal `<=` в comment. Комментарий исправлен; при review добавлен reject
invalid-world для empty command batch. Финальный запуск131checks выполнен после
обеих правок; warnings в targeted build устранены. Симуляции PHYS0 не повторялись,
их результаты сохраняют прежнюю дату/scope.

Версионируемый [PHYS1_RESULTS.json](PHYS1_RESULTS.json) хранит profile/source/
package/DLL/report hashes, commands и полный outcome catalog. Raw ignored evidence:
`TestResults/KtonPHYS1-final-20261112` с logs/binlog/JSON/source/package consumers/feed.
Документационные links/anchors и own diff проверяются отдельно, не считаются
дополнительными physics tests. Remote NuGet publish, full solution builds/suites,
App/DI/configuration, provider performance, GPU, AOT/other OS этой задачей не
запускались. Соседние незакоммиченные audio/renderer/Player изменения сохранены;
proposed game Request/Claim/Decision не принимались и не менялись.

PHYS1 завершён в contract/validation scope. Следующий шаг PHYS2: exact beta.33
provider с live generational storage, full admission, two worlds/shared shapes,
bodies/BallSocket/commands, bounded fixed Step и failure/cleanup proofs.

<a id="phys2-2026-11-12"></a>
## PHYS2 — BEPU provider, generational storage и resource cleanup, 2026-11-12

По прямому запросу пользователя реализован [TrueMoon.Kton.Bepu](PROVIDER.md):
existing typed IFactory, captured shapes, static/dynamic/kinematic bodies,
BallSocket, full command-batch admission, fixed Step, state/error/owner cleanup.
Provider references neutral Kton/Contracts и exact BEPU beta.33; engine/UI отсутствуют.
В CPM добавлены только две exact BEPU pins, соседний Audio pin сохранён; SDK/TFM/
общая версия не менялись. Provider зарегистрирован в solution вместе с Kton.

В neutral API явно добавлен `PhysicsWorldCapabilities`/IPhysicsWorld.Capabilities.
PHYS2 объявляет RigidBodySimulation; queries/contact events — UnsupportedProfile
до PHYS3. Step0published events не маскирует физические contacts. Sleep уточнён:
очищает pending force, SetVelocity сохраняет её. Это датированная дельта PHYS1,
а не скрытая смена семантики; current BCL-only consumer перепроверен.

### Фактические сценарии

- Два независимых world incarnations и разные gravity; default/foreign/stale IDs,
  slot reuse и реальное retirement body/shape/joint slots с uint.MaxValue через
  internal seed seam. Static/dynamic разделяют neutral slots, backend map различает их.
- Captured static mesh не меняется после mutation исходных arrays; sphere/box/
  capsule, sleep/wake, shared shape/reference rejection, static teleport и typed modes.
- BallSocket anchor error<0.04, body-in-use rejection, ref-count connected-pair
  collision filtering при двух joints; последующее восстановление столкновений.
- Симметричные masks, sensor suppression, реальное zero/min friction sliding.
- Impulse mass scaling, force F*dt, Sleep clearing force, cancellation/retry,
  ordered projected speed admission, invalid last command без prefix mutation,
  cumulative command limits/reset, tick repeat/skip, kinematic motion/mode rejects.
- Create failures после pool/dispatcher/simulation и failure cleanup с сохранённым
  factory owner; retry каждой cleanup stage без alias double-dispose; factory
  Dispose не освобождает caller-owned успешный world.
- Partial command fault без rollback, callback error после join, post-integration
  envelope fault, cancellation после begin, concurrent read/Dispose Busy; cleanup
  counters проверены после каждого такого случая.
-64create/delete/dispose cycles и20waves×16mesh create/remove внутри одного live
  world с plateau pools; diagnostic budget1byte наблюдаемо превышается одним owner,
  не выдавая ложный hard-memory failure.
- Exact FastLinear60 mapping повторил sphere radius0.1, speed600, wall0.04,
  dt1/60,600Hz/8iterations/4substeps: первый/max x=-0.11897472 за61tick,
  допуск к поверхности -0.12 равен0.005. Другие CCD сцены не объявлены доказанными.

### Команды и итог

```powershell
pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS2-release-20261112 -PackageDirectory E:/source/my/TrueMoon.Tales/TrueMoon/src/TestResults/KtonPHYS0-package-inspection-20261112
pwsh -NoProfile -File ManualTests/KtonContracts/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS2-contract-regression-20261112
```

На свежей машине опустить PackageDirectory и выбрать новые evidence directories.
Runner получает общий BuildProfile, делает explicit CPM isolation, private local
feed/cache/config/artifacts, проверяет SHA официальных nupkg и copied source hashes.
Source Debug ссылается на реальный provider project. Затем свежие Contracts/Kton/
Kton.Bepu одной версии0.3.0.1-alpha.phys2.20261112143222 потребляются как packages:
exact graph5libraries, actual5DLL SHA сверены с nupkg. Remote publication не было.
11собственных dotnet commands exit0, плюс отдельный profile evaluation; MSBuild
binlog уникальны, stdout/stderr/timeout/reap сохранены. SDK11.0.100/net11.0/runtime11.0.0,
runtime-async=on, Windows x64, JIT,1worker. [Manifest](PHYS2_RESULTS.json).

| Проверка | Итог |
| --- | --- |
| Real provider source Debug | **184/184passed**, build0errors/167existing Contracts warnings; Kton/provider warnings0 |
| Fresh provider NuGet Debug | **184/184passed**, exact5package closure, actual DLL SHA |
| Fresh provider NuGet FDD published Release | **184/184passed**, actual DLL SHA |
| Current neutral API regression | **131/131passed** source Debug/fresh NuGet Debug/published Release; neutral graph1package без BEPU/Contracts/Aether |
|64cleanup cycles + mesh live-world churn + CCD в stress factory |66completed cleanups, active owners0/published worlds0/bytes after cleanup0; high-water5,029,888bytes |
|20mesh waves по16meshes | Все samples4,718,592bytes, пустые live stores после каждой волны; не all-process leak proof |

Первый run прошёл175checks в каждом profile; review добавил explicit cleanup
assertions после fault cases, mesh plateau и captured state при failed gate admission.
Следующий182check run и neutral regression прошли; финальный184check run добавил
наблюдаемый pool diagnostic overrun. Исходники финального run совпадают с manifest.
Warnings принадлежат существующему Contracts и не исправлялись этой задачей.

Raw evidence: ignored `TestResults/KtonPHYS2-release-20261112` и
`TestResults/KtonPHYS2-contract-regression-20261112`; переносимые scope/command/
outcome/source/profile/package/DLL/report hashes сохранены в PHYS2_RESULTS.json.
Старые PHYS0/PHYS1 manifests — датированные результаты прежних исходников, не
переписываются как current runs. Новая capability отражена в CONTRACTS.md.

### Границы и продолжение

Internal friend seams проверяют bounded creation/command/callback/cleanup
failures и generation exhaustion. Нельзя распространять stage-boundary retry
на произвольный partial failure внутри BEPU.Dispose/unsafe heap corruption/
process crash. Однопоточный callback failure не доказывает multi-worker exception
handling; pool counters не доказывают отсутствие всех managed/native/process leaks.
Диагностические thresholds не являются hard RAM/FPS guarantees.

PHYS2 завершён в rigid-body scope. Queries/gameplay contacts ещё явно недоступны;
следующий PHYS3 — bounded queries и pair events/overflow/identity. App/Cobalt/
configuration/полный bootstrap lifecycle — PHYS4, ECS — PHYS5. Other OS/AOT/parallel
workers, character/ragdoll, full solution builds/suites/native GPU/release matrix
этим этапом не запускались; предыдущие passing не наследуются. Соседние Player/
audio/renderer/Core изменения сохранены, proposed game Request/Claim/Decision не менялись.

<a id="phys3-2026-11-12"></a>
## PHYS3 — bounded queries, Begin/Stay/End и generation-safe contacts, 2026-11-12

По прямому запросу пользователя реализованы [query/contact contracts](QUERIES_CONTACTS.md)
в существующем TrueMoon.Kton.Bepu. Capabilities теперь RigidBodySimulation/Queries/
ContactEvents. Provider signatures не менялись, neutral XML limits уточняют per-buffer
event capacity и collector/BVH stack payload budget. App/ECS не реализовывались.

### Реализация

Bounded nearest-prefix collector, exact primitive RayTest/convex SweepTask, exact
overlap через stationary sweep; live query shape/exclusion validation, sorted
ties, explicit truncation и tail preservation. Mesh ray/BVH и overlap visitor
возвращают явные triangle identities, затем выполняется exact test. Front-side
mesh policy и scratch reservation описаны в QUERIES_CONTACTS, включая conservative
stack bound pinned Tree_RayCast/Tree_VolumeQuery. Unbounded candidate lists нет.

Contact callback копирует deepest physical manifold sample; normal canonical A→B,
point world-space, full body-generation/subshape key. Raw buffer bounded, worker
не бросает при overflow. Owner join/reduction/sort публикует Begin/Stay/End после
успеха. Sleep retains identity, wake Stay; delete/teleport preflight pending Ends,
старые IDs сохраняются даже при slot reuse. Same-pair End предшествует new Begin.
Cancellation до начала сохраняет pending Ends и previous published snapshot.
Raw, retained-cache и final-event overflow — ContactOverflow/Faulted без частичного
tick или доступного partial snapshot; insufficient copy output не трогается.

### Финальные команды

```powershell
pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS3-release-20261112 -PackageDirectory E:/source/my/TrueMoon.Tales/TrueMoon/src/TestResults/KtonPHYS0-package-inspection-20261112
pwsh -NoProfile -File ManualTests/KtonContracts/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS3-contracts-20261112
```

На новой машине опустить PackageDirectory и использовать новые evidence dirs.
Source/pack/fresh consumers используют общий BuildProfile, isolated CPM/config/
feed/cache/artifacts. Exact graph5packages, actual5DLL SHA совпали с nupkg;
BEPU beta.33 pins/SHA сохранены, новых dependencies или SDK/TFM pins нет. Native
physics binaries не добавлялись. Версия свежих3TrueMoon packages фиксируется в
[PHYS3_RESULTS.json](PHYS3_RESULTS.json); remote publication не выполнялась.
11runner dotnet commands плюс отдельный profile evaluation, unique binlog,
stdout/stderr/timeout/reap и source hashes сохранены. SDK11.0.100/net11/runtime11.0.0,
runtime-async=on, Windows x64/JIT/1worker.

| Scope | Фактический итог |
| --- | --- |
| Real provider source Debug | **309/309passed**,0errors/167existing Contracts warnings; Kton/provider warnings0 |
| Fresh NuGet provider Debug | **309/309passed**, actual5DLL SHA/exact closure |
| Fresh NuGet provider FDD published Release | **309/309passed**, actual5DLL SHA/exact closure |
| Current neutral API source/fresh NuGet D/published R | **131/131passed в каждом scope**, BCL-only1package graph |
|1000primitive overlap calls после warmup | Managed allocation delta0; pool4,194,304→4,194,304bytes |
|2000ray/sweep calls по512triangle mesh после warmup | Managed allocation delta0; pool4,194,304→4,194,304bytes |
|600ticks automatic sleep |1Begin/31Stay/0End; sleeping body, retained pair1; wake Stay |
| PHYS2 cleanup regression |64world cycles+20mesh waves/CCD, stress factory66completed cleanups, owners0/published0/after-cleanup bytes0; high-water5,029,888bytes |

Матрица125новых outcomes сверх184регрессии:9primitive source/target pairs,
ray/sweep/overlap, rotations/translated mesh/triangle ties/backfaces, query layers/
sensors/exclusion, stale shape/exclusion после reuse и foreign IDs, MaxDistance/
MaxHits/empty output/truncation/tail, conservative BVH scratch error до записи,
mesh BVH identity/allocation; physical/speculative Begin, normal/point, manual/auto
sleep/wake, support delete, slot reuse, teleport End→Begin, cancel/pending End,
raw/event/sleeping-cache overflow и cleanup после faults. Fake physics provider нет.

Первый compile run показал static IShape.TypeId API beta.33; исправлено TA/TB.TypeId.
Последующие passing матрицы расширены до финальных309 после mesh BVH traversal,
точной scratch reservation и public package/XML docs. Предыдущие passing и compile
failure не подменяются текущим run; raw attempts сохранены отдельно.

Raw ignored evidence — `TestResults/KtonPHYS3-release-20261112` и
`TestResults/KtonPHYS3-contracts-20261112`. Portable manifest содержит scope/commands,
outcome catalog, source/profile/package/DLL/report SHA и metrics. Соседние Kul/
Aether Abstractions/Player/Core изменения сохранены, game Request/Claim/Decision
не менялись; подробные tasks физики остаются в единственном PHYS-плане.

### Границы

World body scan bounded MaxBodies, mesh candidates используют BVH; frame-time
target для больших сцен не доказан. Scratch budget относится к buffer payload,
не GC headers/fixed ABI locals/retained allocator blocks; pool counters/plateau
не доказывают отсутствие всех process/GC leaks. Один worker не закрывает parallel
callback/overflow matrix. Fixed preset не обещает cross-platform determinism,
arbitrary-scene/angular CCD или physics stability всех policy ceilings.
Cleanup injection остаётся доказательством boundary retry, не recovery произвольной
BEPU unsafe corruption/partial Dispose. Performance benchmark, full solution
builds/suites, App/Cobalt/configuration/ECS, GPU/other OS/AOT/remote release не запускались.

PHYS3 завершён в указанном functional/allocation scope. Следующий PHYS4 — public
App/IFactory/options schema integration, duplicate/startup/stop/drain/disposal
ownership и both-DI consumers. Затем PHYS5 session/ECS bridge по ARCH3 boundaries.

<a id="phys4-2026-11-12"></a>
## PHYS4 — public App, configuration, factory и lifecycle, 2026-11-12

Прямой запрос пользователя: реализовать публичное подключение Kton к App,
configuration/DI factory/lifecycle и проверки на Cobalt/Microsoft DI.
[App guide](APP.md), [source/runner](../../ManualTests/KtonApp/README.md),
[frozen manifest](PHYS4_RESULTS.json). Изменены только Kton/provider/его acceptance
и относящиеся документы; параллельные Aether/Kul/Core изменения не откатывались.

### Реализация и принятые решения

UseBepuPhysics/UseConfiguredBepuPhysics в provider используют только общие Contracts.
16scalar fields schema Kton:DefaultWorld:*, validated detached startup options;
sources/priority — общие configuration providers. Никаких собственных OS readers.
DefaultWorld применяется через IFactory<IPhysicsWorld>.Create(); explicit
IFactory<IPhysicsWorld,PhysicsWorldOptions>.Create заменяет весь набор параметров.
Neutral assembly остаётся BCL-only; новых runtime packages/backend versions нет.

Единственный DI-owned disposable — Hosting.BepuPhysicsHost. Typed/base factory aliases
и IStartable/IStoppable shared adapter не IDisposable. Build/start не создают backend
ресурсов; Start открывает admission, Stop закрывает её навсегда и освобождает только
unpublished failed candidate. Published world остаётся исключительно caller-owned.
Нормальный drain — consumer join → world Dispose → Kton/App stop. Мир не заимствует
App resources и может быть освобождён тем же caller после App. IsReleased host
отдельно от PublishedWorlds. Hidden fallback ownership выданных миров не вводилось.

При failed cleanup host сохраняется с closed admission и повторяет remaining stages.
App/providers могут кешировать Dispose failure; явный retry выполняется на сохранённом
borrowed public host. Failed world disposal повторяет его original caller. Stop
owned cleanup синхронный и non-cancellable. Пользовательские scopes/async activation/
новые factory abstractions не добавлялись. DI generation/packaging проверены до фиксации API.

### Фактические команды и результаты

```powershell
pwsh -NoProfile -File ManualTests/KtonApp/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS4-final-20261112
pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS4-provider-final-20261112 -PackageDirectory TestResults/KtonPHYS0-package-inspection-20261112
```

Оба runner Completed=true. PHYS4: **192/192checks** на каждом source Debug,
fresh NuGet Debug и fresh NuGet published Release: **96Cobalt+96MicrosoftDI**.
Запущены настоящие App/DI/BEPU worlds. Cobalt consumer constructor activation
сгенерирован source analyzer и bundled package analyzer; generated source SHA
совпадает в3profiles. Шесть свежих TrueMoon packages одной версии плюс два BEPU и
Microsoft.Extensions.DependencyInjection11.0.0: exact9package graph, ProjectReferences0.
Все9loaded package DLL SHA совпали с nupkg; DI Abstractions фактически загружен из
Microsoft.NETCore.App11.0.0 shared framework, его path/SHA отражён отдельно.
Roslyn build-tool archives не входят в runtime graph. Consumers/feed/caches/artifacts
PHYS4 находятся вне checkout, CPMfalse. SDK11.0.100/TF=TestsTFnet11.0/LangVersionlatest/
runtime-async=on переданы общим BuildProfile. MSBuild unique binlogs/stdout/stderr/
exit codes/source+profile SHA сохранены. Source targeted build0errors/494existing
upstream warnings, Kton/provider/probe0warnings; fresh consumer D/R0warnings/0errors.

Подтверждены duplicates/conflicting factories/mixed registrations; null/invalid
defaults и runtime args до ресурсов; все16fields; priority/provenance/callback
precedence и detached configuration copies; startup без мира/pool/worker; independent
world/App identities и resource ledger; default/explicit factory behavior; раннее
закрытие world, последующий stop и repeated cleanup. Настоящий constructor-injected
consumer создаёт мир после старта Kton и закрывает до него при stop/startup rollback.

Failure seam проверяет реальный unpublished candidate после Simulation.Create:
creation+cleanup errors, blocked next Create, startup+rollback aggregation, failed
stop, DI dispose fallback, final Dispose failure и cached repeated App error,
explicit retained-host retry; caller-world stage failure/retry после закрытия App.
Host не забирает successful/failed caller world ownership. Все проверенные конечные
owners/published/pool-after-cleanup=0; released candidates освобождены ровно один раз.

Текущая PHYS2–3 regression — **309/309checks×3profiles**: bodies/joints/commands/Step,
live generational IDs/retirement, bounded ray/sweep/overlap и physical Begin/Stay/End,
sleep/delete/teleport/overflow и cleanup. Fresh5package graph/loaded DLL SHA проверены.
Neutral-only131suite отдельно не запускалась; прежние results не переименованы в текущие.

В exploratory runs исправлены только acceptance scaffolding: точная async source
сигнатура, ожидаемый ArgumentException missing schema и первоначальное ошибочное
ожидание10packages. Net11 shared DI Abstractions объясняет actual9package graph.
Финальные runs выше полностью повторены после правок package readme; предыдущие
raw failures и intermediate passing runs сохранены отдельно в ignored TestResults.

Raw artifacts — ignored TestResults/KtonPHYS4-final-20261112 и
KtonPHYS4-provider-final-20261112; external run path хранится в verification.json.
Автоматическая retention/remote upload не настроены; переносимая запись результатов,
команд, graph/package/source/loaded DLL hashes — этот HISTORY и PHYS4_RESULTS.json.
Без сохранённых raw файлов binlogs/полный check stream не восстанавливаются из текста.

### Границы и продолжение

Windows x64/.NET11/JIT/one worker, real source/fresh package consumers; не performance
benchmark и не proof arbitrary unsafe partial BEPU.Dispose safety/heap corruption.
Full solution/xUnit suites, GPU/window/native launch, ECS binding, other OS/AOT/
2+workers и remote release не запускались. Внутренний friend seam не public API.
App startup/stop не заменяет caller drain. Дальше PHYS5 ECS/session bridge по
согласованным ARCH3 Abstractions/Runtime boundaries; renderer/Player очередь,
requests/Claims/Decisions не менялись.

<a id="physics-status-before-phys4-2026-11-12"></a>
## Снимок physics STATUS перед PHYS4, 2026-11-12

Ниже сохранён прежний PHYS3 STATUS; его current/next утверждения исторические.
# TrueMoon.Kton: состояние и следующий шаг

**2026-11-12 — PHYS3 bounded queries и generation-safe contact stream реализованы и проверены.**

[Единый план](PLAN.md#current-physics-queue), [контракт](CONTRACTS.md), [provider](PROVIDER.md),
[queries/events](QUERIES_CONTACTS.md), [команды/границы](HISTORY.md#phys3-2026-11-12),
[manifest](PHYS3_RESULTS.json), [runner](../../ManualTests/KtonProvider/README.md).

| Область | Реализация | Фактическая проверка12ноября |
| --- | --- | --- |
| Provider/capabilities | RigidBodySimulation/Queries/ContactEvents, exact BEPU beta.33 | **309/309checks** source Debug/fresh NuGet Debug/published Release;5package closure/actual5DLL SHA; current neutral131×3 |
| Queries | Nearest sorted prefix в caller Span; exact primitive/triangle tests, mesh BVH, layer/sensor/live exclusion, bounded scratch |9shape pairs, transforms/inside/mesh child/backfaces/ties, MaxHits/MaxDistance/empty output/truncation/tail/error preservation, stale/foreign IDs |
| Contacts | Physical Begin/Stay/End, canonical world payload, full-generation/subshape pair key; pending delete/teleport Ends | Manual/auto sleep→wake Stay, support delete/slot reuse, same-pair End→Begin, cancel pending End, full/repeated copied snapshots |
| Overflow/ownership | Raw callback flag после join; cache/event overflow ContactOverflow/Faulted; no partial successful tick | Raw/event/sleeping-cache overflow и cleanup; known pending End preflight до mutation, snapshot ownership проверен |
| Allocation/cleanup | Fixed-capacity collector/caches и conservative BVH stack reservation |1000primitive/2000mesh queries после warmup:0managed bytes и pools4,194,304→4,194,304; auto sleep1Begin/31Stay/0End; PHYS2 stress owners0/after-cleanup0 |
| Остальные этапы | Public App/IFactory/configuration — PHYS4, ECS/session bridge — PHYS5 | Полный App/DI/bootstrap, full solution/GPU/other OS/AOT/parallel workers/release matrix не запускались |

## Ограничения

Поддержка проверена Windows x64/.NET11/JIT/1worker. World body enumeration bounded
MaxBodies, meshes используют BVH; arbitrary-scene latency/1ms target не доказан.
Scratch budget относится к collector/traversal payload, не allocator blocks/GC headers/
fixed ABI locals. MaxContactEvents — capacity каждого raw/cache/pending/published
buffer, не единый byte limit. Pool plateau не доказывает все process/GC leaks.
Targeted build0errors/167existing Contracts warnings; Kton/provider warnings0.

Single-sided mesh sweep/overlap требует start COM на front side. Fixed profiles не
обещают arbitrary-scene/angular CCD или cross-platform determinism. Cleanup failure
proofs сохраняют stage-boundary scope PHYS2. Исторические PHYS0–2 manifests не
переписаны; соседние изменения Aether/Kul/Core и game Claim/Decision сохранены.

## Следующий конкретный шаг

**[PHYS4](PLAN.md#phys4):** публичное IAppConfigurationContext подключение provider,
typed options/configuration schema/snapshots и existing IFactory registration;
App-local duplicates, startup/failed startup, caller-owned world lifetime и
stop/drain/dispose/retry на Cobalt и существующем Microsoft DI adapter.
Затем PHYS5 — ECS/session bridge по согласованным ARCH3 boundaries.

<a id="physics-status-before-phys5-2026-11-15"></a>
## Previous STATUS before PHYS5 — 2026-11-15

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-15:** [общий session-subsystem слой Aether](../engine/SESSION_SUBSYSTEMS.md)
реализован и проверен отдельно. PHYS5 выбран следующим этапом; Kton bridge ещё
не реализован. Новых backend-проверок Kton эта работа не добавляет.

**2026-11-14 — предкоммитная сверка:** App192checks и provider309checks повторены
каждый в source Debug/fresh package Debug/published Release; package metadata
Kton/Kton.Bepu прошла. [Состав, команды и границы](../COMMIT_AUDIT_2026-11-14.md).
PHYS5 и wider delivery/release readiness этим не объявляются.

**2026-11-12 — PHYS4 public App/configuration/DI/lifetime реализован и проверен.**

[Единый план](PLAN.md#current-physics-queue), [App guide](APP.md), [контракт](CONTRACTS.md),
[provider](PROVIDER.md), [queries/events](QUERIES_CONTACTS.md),
[команды/границы](HISTORY.md#phys4-2026-11-12), [manifest](PHYS4_RESULTS.json),
[runner](../../ManualTests/KtonApp/README.md).

| Область | Реализация | Фактическая проверка12ноября |
| --- | --- | --- |
| App/configuration | UseBepuPhysics/UseConfiguredBepuPhysics,16fields Kton:DefaultWorld:*, validated detached startup snapshot | Все fields, source priority/provenance/callback precedence, invalid/null/duplicates/mixed registration, no backend allocation at Build/start |
| DI/factory | Existing typed/base IFactory aliases, один App-owned BepuPhysicsHost, adapters не IDisposable | **192/192checks/profile:96Cobalt+96MicrosoftDI**, source Debug/fresh NuGet Debug/published Release; real generated consumer constructor; exact9package graph/9DLL SHA+1shared-framework DLL |
| World ownership | Create() defaults, Create(options) complete arguments; successful worlds exclusively caller-owned | Независимые миры/Apps, immutable snapshots, early disposal, live world после App disposal; no alias double Dispose |
| Lifecycle/failures | Reverse consumer stop/startup rollback, permanent admission close, retained failed-candidate owner | Startup/stop/DI Dispose/cached failure, cancellation boundaries, cleanup error aggregation, explicit retained-host retry и caller-world retry after App; final owners0/pools0 |
| Provider regression | PHYS2–3 bodies/joints/commands/fixed Step/queries/contacts/generational IDs | **309/309checks×3profiles** повторены на текущем provider;5package closure/actual DLL SHA; neutral-only suite отдельно не запускалась |
| Следующие этапы | PHYS5 Aether ECS/session bridge, PHYS6 controller, PHYS8 wider delivery/performance | Full solution tests/GPU/native launch/other OS/AOT/2+workers/remote release в PHYS4 не запускались |

## Ограничения

App закрывает фабрику и очищает unpublished failed creations; сессия join/drain/dispose
своих миров. Root fallback disposal выданных миров не введён. Host.IsReleased не
утверждает PublishedWorlds=0. После финального App/DI Dispose failure новая попытка
выполняется на заранее удержанном borrowed BepuPhysicsHost: App/provider могут
вернуть закешированную ошибку. Мир с failed Dispose сохраняется самим caller.

Профиль проверки: Windows x64/.NET11/JIT/1worker. Fault injection доказывает retry
между завершёнными стадиями, не arbitrary partial unsafe BEPU.Dispose recovery.
No-arg factory defaults фиксируются до ресурсов; App configuration reload не меняет
модуль/миры. Stop cleanup синхронный и не прерывается cancellation token.
Source targeted build0errors/494existing upstream XML/nullability warnings;
Kton/provider/probe warnings0; fresh consumer Debug/Release0warnings/0errors.
Budgets не являются hard RAM/frame-time гарантиями. Предыдущий PHYS3 STATUS сохранён
в [HISTORY](HISTORY.md#physics-status-before-phys4-2026-11-12), manifests PHYS0–3 не переписаны.

## Следующий конкретный шаг

**[PHYS5](PLAN.md#phys5):** согласовать Aether Abstractions/Runtime boundary по ARCH3,
добавить ECS value components/registry, prepare/update/writeback и session ownership.
Проверить spawn/delete/teleport, pause/Stop/Replace, next-tick contact events и cleanup
без освобождения borrowed ECS World/dependencies. Текущая очередь Aether/Claims не менялась.


<a id="phys5-2026-11-15"></a>
## PHYS5 — Aether ECS/session bridge, 2026-11-15

По запросу пользователя после общего ARCH1j реализовано подключение Kton к движку.
[Контракт/профиль](ECS_BRIDGE.md), [manifest/SHA](PHYS5_RESULTS.json),
[independent runner](../../ManualTests/KtonEcs/README.md).

Новые пакеты: `TrueMoon.Aether.Kton` (reference-free components/schemas/helpers;
только Abstractions+neutral Kton) и `TrueMoon.Aether.Kton.Runtime` (App/DI factories,
registry/reconciliation/Step/writeback/contact projection и session ownership).
Выбор provider остаётся у composition root. Existing Kton/Bepu/Runtime/Abstractions
source не менялся; проверены26protected hashes предыдущего ARCH1j. В solution
добавлены2production projects и один xUnit2/VSTest test project.

Profile v1: box/sphere/capsule, Static/Dynamic/Kinematic, root/unit-scale COM pose,
точный60Hz. ExternalMotion исключает двойную integration. Полная preflight перед
native mutation; explicit velocity/monotonic teleport, phase spawn/attach/detach/
delete и descriptor replacement. Retired body/entity generations сохраняются в
End events. PhysicsContact entities публикуются после Movement; Gameplay читает
события предыдущего tick. Overflow не обрезается; после mutation failure нужна
очистка terminally faulted session. Dispose order: systems → native world →
borrowed scene/World → App dependencies; native failure сохраняет owner/World lease.

Фактические проверки Windows x64 / SDK11.0.100 / net11.0 / runtime-async=on:

- `dotnet build TrueMoon.slnx -c Debug|Release --no-restore --no-incremental -m -bl:<unique>`:
 0errors/1570existing warnings в каждой конфигурации. Новые production проекты
 отдельно собраны Debug0warnings/0errors.
- `dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj -c Debug|Release --no-restore --logger trx --results-directory TestResults/PHYS5/tests -bl:<unique>`:
 **68passed/0failed/0skipped каждый D/R**,42methods. Только финальные
 `PHYS5-Debug-final.trx`/`PHYS5-Release-final.trx`; промежуточные66cases не подменяют их.
- `dotnet test TrueMoon.slnx -c Debug --no-build --no-restore --filter FullyQualifiedName~TrueMoon.Aether.Kton.Tests --logger trx --results-directory TestResults/PHYS5/discovery -bl:<unique>`:
 15TRX,68executed в новом проекте; остальные14suite не имели совпадений фильтра.
 Это discovery proof, а не полный15-suite regression.
- Common subsystem/movement/primitive/preparation/required-mesh regression:
 `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug|Release --no-build --no-restore --filter <five exact class selectors> ...`:
 **104passed каждый D/R**,failed/skipped0. Сами backend/module old suites этим
 не объявляются повторёнными; PHYS5 tests используют actual BEPU и оба DI.
- `pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory TestResults/PHYS5/consumer-03`:
 source Debug/published Release/fresh NuGet Debug/published Release, каждый с Cobalt
 и Microsoft DI. **42checks/report ×4reports =168checks**,8provider executions.
 Проверены real fall/settling/static/kinematic, same-tick CPU frame pose, next-tick
 contacts, generations/spawn/delete/teleport, invalid candidate сохраняет old session,
 Stop/replacement и final owners0/pools0.14coherent local archives одной версии,
 fresh runtime graph20libraries =13TrueMoon runtime +7external; Enerit.Generator —
 build-time archive, не runtime dependency. Cache/feed изолированы, all loaded
 TrueMoon/BEPU output DLL SHA совпали с installed closed cache.
- `eng/Check-PackageMetadata.ps1 -Pack -NoBuild -IncludeProject <two exact new csproj paths> -RunDirectory TestResults/PackageMetadata/PHYS5-20261115`:
 2projects/2archives metadata/README/XML/assets passed. Local pack, не external push.

Первые два independent attempts остановились на verifier assertions после успешных
source/published executions: analyzer ошибочно ожидался в runtime graph, затем
ненормализованный Windows feed path сравнивался с нормализованным metadata source.
Исправлен только runner, не ослаблена изоляция. Финальный consumer-03 прошёл полностью.
Static Roslyn source-pairing helper имел отдельный missing Microsoft.CodeAnalysis4.14
prerequisite; это не target compile/test failure, ручная pairing classification не
подставлялась. Полезные behavioral tests построены по матрице требований ниже.
Coverage percentages и production mutation runs не заявлены.

Raw logs/TRX/binlogs/cache/feed находятся в ignored TestResults/PHYS5,
TestResults/PackageMetadata/PHYS5-20261115 и temp root из consumer verification.json.
Это локальный retention без внешнего хранения; итог/scope/команды и компактный SHA
manifest сохраняются в versioned документах.

Новое GPU/window/physical/device-loss evidence не создавалось; CPU frame pose не
подменяет actual pixels или соответствие collider/render geometry. Native-state
checkpoint/mod migration, fixed GameBuildArtifact ABI extension, controller,
ragdoll и mesh asset import остаются вне PHYS5. Дальше по общей очереди — Kul
ECS/captured-assets adapter; следующий физический feature — PHYS6 отдельно.

## Requirement | Evidence

| Requirement | Exact generated test evidence |
| --- | --- |
| User: давай теперь подключим Kton к движку по PHYS5 | PhysicsSceneSubsystemTests.Registration_IsLazyAndSessionsOwnIndependentNativeWorlds; PhysicsSceneSubsystemTests.Step_RespectsStaticDynamicKinematicModesAndSkipsOrdinaryMovement |
| lazy App DI both providers | Registration_IsLazyAndSessionsOwnIndependentNativeWorlds (Cobalt/Microsoft cases), native owners0 before scene preparation and independent owners2 after two sessions |
| registry schemas | PhysicsContractsTests.Schemas_PinExactIdentitiesKindsDefaultsAndRequiredTypes; PhysicsRegistrationTests.Registry_RejectsUnsupportedSchemaVersionBeforeNativeCreation |
| neutral SDK / no Runtime or BEPU dependency | PhysicsContractsTests.ComponentValues_AreReferenceFreeAndContractsDoNotReferenceRuntimeOrBepu |
| actual BEPU static/dynamic/kinematic step/writeback, ExternalMotion no double movement | Step_RespectsStaticDynamicKinematicModesAndSkipsOrdinaryMovement; Gravity_AdvancesAllSupportedPrimitivesAndWritesBackVelocity; AngularVelocity_RotatesNativePoseAndChangedCommandWritesBack |
| complete schema-to-provider descriptor forwarding | PhysicsLifecycleTests.SceneData_ForwardsCompleteDescriptorToSelectedProvider (mass/material/filter/sensor/pose/linear/angular and native shape World identity) |
| helper phase spawn/delete/attach/detach | PhysicsContractsTests.PhaseHelpers_SpawnDetachReattachAndDeleteCompleteRolesWithoutChangingNeighbor; PhysicsReconciliationTests.GameplayHelpers_SpawnDetachReattachDeleteAndNeverReviveStaleGenerations |
| explicit PhysicsTeleport sequence/clear-velocity | Teleport_IsSequencedClearsVelocityAndReplaysWithoutRepeatingDiscontinuity; Teleport_RejectsZeroOrChangedReplayWithoutAdvancingNativeTick; DescriptorChange_RecreatesNativeIdentityWithoutChangingEcsIdentity |
| parent/scale/incomplete roles reject | UnitScaleAndRootParent_AreRequiredForEveryMode; MissingRuntimeRoles_AreRejectedBeforeNativeMutation; DirectBodyRemoval_WithResidualRolesRejectsBeforeStepping; DirectPoseEdit_IsRejectedBeforeAnyOtherBodyNativeWriteback |
| stale entity/body generations | GameplayHelpers_SpawnDetachReattachDeleteAndNeverReviveStaleGenerations; Mapping_RejectsForeignWorldIdentitiesEvenWhenSlotsMatch; RemovedBodyEnd_UsesOriginalGenerationsEvenWhenEntitySlotIsReused |
| next-tick contacts | PhysicsContactTests.Contacts_AreVisibleToGameplayOnNextTickAndPreviousBatchIsRetired (Begin/Stay/Teleported End and event-entity retirement) |
| capacity failure explicit, no truncation | ProjectionAtCapacity_PublishesCompleteEventAndRetiresPreviousBatch; ProjectionOverflow_IsExplicitAndDoesNotPublishPartialContactEntities; EcsCommandCapacityFailure_DoesNotPretendToPublishContactBatch |
| collision filter applied to native world | CollisionMaskZero_DisablesContactProjectionWithoutStoppingPhysics |
| pause/Stop/Replace/new scene failure preserves old session | Step_RespectsStaticDynamicKinematicModesAndSkipsOrdinaryMovement (paused no advance + one explicit step); FailedCandidatePreparation_LeavesOldSessionRunningAndReplacementIndependent (old continues after bad candidate, valid new session then old disposal) |
| cleanup retains native owner and borrowed World | PhysicsLifecycleTests.CleanupFailure_RetainsNativeOwnerAndBorrowedWorldUntilExplicitRetry; CancellationAndCleanupFailure_PreserveOriginalErrorsAndRetainedCandidateForRetry |
| cancellation/factory/native faults | CancellationAfterNativeCreation_ReleasesWorldWithoutPublishingCandidate; PrecancelledPreparation_RejectsBeforeNativeCreation; PreparationFailures_DoNotPublishOrLeakNativeResources; NativeStepFailure_PreservesErrorFaultsEcsAndDoesNotAdvancePublishedTick |
| common configuration | ConfiguredRegistration_UsesExactSchemaAndExplicitOverrideBeforeSnapshot; ConfiguredRegistration_RequiresRegisteredExactSchema; Registration_AdmitsExactProjectionBoundariesAndSnapshotsOptions; Registration_RejectsInvalidProjectionBoundsBeforeAnyNativeResources |
| empty session / fixed-step profile | EmptyScene_StepsWithoutInventingBodiesOrEventsAndReleasesNativeWorld; IncompatibleFixedStep_IsRejectedBeforeNativeWorldCreation |
| scaffolding and entry-point registration | TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj; parent-owned TrueMoon.slnx /Kton/ registration; direct project runs above |
