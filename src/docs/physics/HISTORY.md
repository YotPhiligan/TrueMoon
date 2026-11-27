# Kton: история проверок

[План](PLAN.md), [текущий статус](STATUS.md).

<a id="phys6-aether-package-auto-step-2026-11-27"></a>
## PHYS6 — пакетный автошаг Aether, 2026-11-27

`1.2611.27.11-alpha`, исходный SHA `1e956858e8a031d621326753a8773d8926666fd9`,
Windows x64, SDK `11.0.100`, `net11.0`, BEPU `2.5.0-beta.33`. Чистый `ovnfs`
checkout этой ревизии; production-входы между проверками не менялись. Из `src`
с новым абсолютным каталогом `$R` под `%TEMP%`:

```powershell
dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj -c Release --framework net11.0 `
  --artifacts-path "$R/source-artifacts" `
  --filter 'FullyQualifiedName~PhysicsCharacterSceneTests|FullyQualifiedName~PhysicsRegistrationTests|FullyQualifiedName~ProviderCharacter' `
  "-bl:$R/source-{}.binlog" --results-directory "$R" --logger 'trx;LogFileName=source.trx'
pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory (Join-Path $R 'delivery')
```

- Исходники: 128 passed, 0 failed, 0 skipped, два binlog. В TRX сверены все
  шесть классов, восемь вариантов шага, Pause/Step/Replace на обоих DI,
  `invalid: 5` и `invalid: 6`.
- Независимый consumer: `source-debug`, `source-published`, `fresh-Debug`
  и `fresh-Release` прошли по 104 проверки; по шесть владельцев с нулём
  опубликованных миров, активных owners и оставшихся pool bytes в каждом.
- Cobalt и Microsoft DI × `Baseline60` и `FastLinear60`: v1 и v2 без opt-in
  блокируются у 0,25 м; v2 с opt-in проходит 0,25 м, публикуя позу и верхнюю
  опору в одном тике, но блокируется у 0,30 м.
- Закрытый feed: 14 собственных и 10 внешних архивов с SHA256; три записи
  `licenses/characters/` сверены с исходниками. Изолированный package-only
  graph содержит 20 библиотек. Для семи DLL в каждом fresh-профиле SHA
  загруженного файла совпало с runtime asset в cache и в архиве.
- Локальные артефакты: `%TEMP%/kton-phys6-package-ab03540c912d4e5cadbb1969c14e5142`;
  `source.trx`, `source.json`, `delivery/verification.json`,
  `delivery/delivery-audit.json`, `delivery/run/feed`. У каждого из 21
  MSBuild-шагов — непустой binlog; у вычисления профиля — отдельный.
  SHA256 `verification.json`: `11CD93C3C125DFFE626647FDFB95EA0B1E7CE2D8A9AC9AD7E3F68EAC3EA19B86`.

Приёмка ограничена headless ECS/CPU. Pause/Step/Replace проверен только на
исходниках. Native Player, T3, доставка и переключение движения Tales не проверялись.

<a id="phys6-aether-auto-step-2026-11-27"></a>
## PHYS6 — автошаг в ECS и сессии Aether, 2026-11-27

`1.2611.27.11-alpha`, Windows x64, SDK 11.0.100, Release/net11.0, VSTest/xUnit v2.
Из `src`, где `R` — новый абсолютный каталог под `%TEMP%`:

```powershell
dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj -c Release --framework net11.0 `
  --filter 'FullyQualifiedName~PhysicsCharacterSceneTests|FullyQualifiedName~PhysicsRegistrationTests|FullyQualifiedName~ProviderCharacter' `
  "-bl:$R/phys6-aether-{}.binlog" --results-directory "$R" --logger 'trx;LogFileName=phys6-aether.trx'
```

Exit 0; XML TRX: 128 passed, 0 failed, 0 skipped. `PhysicsCharacterSceneTests` — 27,
`PhysicsRegistrationTests` — 9; provider: 26 auto-step, 24 corpus, 19 character,
23 validation. TRX подтвердил восемь movement cases (по четыре на
`Baseline60` и `FastLinear60`), два случая Pause/Step/Replace (`Cobalt` /
`Microsoft DI`) и два отказа preflight (`invalid: 5/6`) до native tick и
изменения поз персонажа и соседа.

На каждом профиле v1 и v2 без новых ключей остановились у уступа 0,25 м;
v2 opt-in поднялся на него с позой и опорой в одном тике и остановился у
уступа 0,30 м. При Replace реальный протокол загрузил тот же документ в
новый мир с новой идентичностью сущностей. На паузе фиксированный вызов не
сдвинул тик 59; Step дошёл до 60. Кандидат восстановил опору replay 60 idle
тиков из пустого состояния опоры, затем повторил подъём ручными Step.
После Stop счётчики `PublishedWorlds` и `ActiveResourceOwners` равны нулю.

Артефакты: `%TEMP%\kton-phys6-aether-660d99d0-29d3-4140-b1c2-360f7a067f53`;
`phys6-aether.trx` (195134 байта),
`phys6-aether-20261127-171238--53932--jHXIo7.binlog` (923088 байт),
`phys6-aether-20261127-171241--53932--EICWWr.binlog` (1090441 байт).
Хранение по политике временных файлов машины.

In-process lease не запускает DLL кандидата, окно или IPC. Replay — только
контрольная точка этого теста, не сериализация Kton или гарантия произвольного
checkpoint. Native Player/ALC, пакетная матрица, T3 и миграция движения Tales
не проверялись; solver и package pins не менялись.

<a id="phys6-headless-corpus-2026-11-27"></a>
## PHYS6 — расширенный headless-корпус, 2026-11-27

`1.2611.27.9-alpha` без повышения версии; Windows x64, SDK 11.0.100,
Release/net11.0, VSTest/xUnit v2. Новый корпус использует только
`IPhysicsWorld` и `IPhysicsCharacterWorld` в существующем тестовом проекте.
Из `src`, где `R` — новый абсолютный каталог под `%TEMP%`:

```sh
dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj -c Release --framework net11.0 \
  --filter "FullyQualifiedName~ProviderCharacter" \
  "-bl:$R/phys6-corpus-{}.binlog" --results-directory "$R" --logger "trx;LogFileName=phys6-corpus.trx"
```

Exit 0; TRX: 92 passed, 0 failed, 0 skipped. Прежние классы: 26 автошаг,
19 provider, 23 валидация. В [новом классе](../../TrueMoon.Aether.Kton.Tests/ProviderCharacterCorpusTests.cs)
по аргументам theory подтверждены 12 исходов на профиль (`Baseline60`,
`FastLinear60`): 8 проёмов/настил, 1 тонкая стена с отходом, 2 направления
смешанного маршрута и 1 открытый край. На скосе опубликована опора mesh,
на подъёмах — опора соответствующего бокса. Каждая из четырёх пар запусков
совпала во всех 166 кадрах по значениям состояния; ID тел сопоставлялись
только внутри своего мира. Для падения пересечение края и потеря опоры
проверялись раздельно.

Локальные артефакты: `%TEMP%\kton-phys6-corpus-bd157b38-cda4-4e02-9c18-14cc4024d0a6`,
`phys6-corpus.trx` и два непустых `phys6-corpus-*.binlog`; хранение по политике
временных файлов машины. Это проверка upright capsule, в том числе низкого
прокси Ø0,50 м × 0,55 м, а не вытянутой формы Тени. Архив T3 не прогонялся;
ECS, App, пакеты, миграция игры и межплатформенный детерминизм не проверялись.

<a id="phys6-auto-step-2026-11-27"></a>
## PHYS6 — опциональный шаг на статические боксы, 2026-11-27

`1.2611.27.9-alpha`, Windows x64, .NET SDK 11.0.100, Release/net11.0,
BEPU 2.5.0-beta.33, `Baseline60` и `FastLinear60`.
Из `src` выполнена узкая проверка VSTest/xUnit v2:

```sh
dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj -c Release --framework net11.0 \
  --filter "FullyQualifiedName~ProviderCharacter|FullyQualifiedName~PhysicsCharacterSceneTests|FullyQualifiedName~PhysicsContactTests" \
  "-bl:phys6-auto-step-{}.binlog" --logger "trx;LogFilePrefix=phys6-auto-step"
```

TRX: 90 passed, 0 failed, 0 skipped (26 автошаг, 23 валидация, 19 прежний
provider, 15 ECS scene, 7 contact). Подъём подтверждён по оси и диагонали,
с опорой на верхнем боксе в первый тик подъёма и `Separated`, а не
`Teleported`, у завершённого нижнего контакта. Негативные проверки: высокий
уступ, выключенная политика, короткая проступь, потолок на подъёме и переносе,
щель и старт в воздухе. Сенсоры/фильтр, приоритет прыжка и независимый спуск
проверены отдельно.

Headless-сравнение с [baseline](STATUS.md#phys6-tales-route-2026-11-27):
исходный измеритель собран в Release с проектными ссылками, отдельным
`--artifacts-path` и binlog. В копии изменены только настройки контроллера
(`MaximumStepHeight = .25f`, `MinimumTreadDepth = .30f`). Запуск DLL на 11
сценариях: `Errors=[]`, оба flat controls и 22 точных same-process повтора.

| Наблюдение после прогрева | Baseline60 | FastLinear60 |
| --- | --- | --- |
| Ступени 1–4, тик смены опоры | 49, 55, 61, 67 | 49, 55, 61, 67 |
| Финиш подъёма, Z м / опора | −4,261513 / Room | −4,261242 / Room |
| Спуск, финиш / тики без опоры | Street / 18 | Street / 21 |
| Уступ 0,25 м, пересечение Z=29 / финишная опора | 19 / LedgeNorth | 19 / LedgeNorth |
| Уступ 0,30 м, пересечение / подъём | нет / 0 м | нет / 0 м |
| Задний край, пересечение / потеря опоры | 32 / 39 | 32 / 39 |

Оба скоса, связка скоса с комнатой и осевой проход двери достигли целей.
В 18 из 22 пар «до/после» совпали все поля трассы; изменились только два
подъёма на каждом профиле. Цели описывают исходы сценария, не паритет
координат с кинематикой. Полный корпус, измерение стоимости, PHYS5/Aether
opt-in, migration, Replace, межплатформенный детерминизм и пакеты не приняты.

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

<a id="physics-status-before-dep1-2026-11-15"></a>
## Previous physics STATUS before DEP1 — 2026-11-15

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-15 — PHYS5 ECS/session bridge реализован и проверен.**
[Контракт/API/limits](ECS_BRIDGE.md), [проверки/команды](HISTORY.md#phys5-2026-11-15),
[manifest/SHA](PHYS5_RESULTS.json), [independent consumer](../../ManualTests/KtonEcs/README.md).

`TrueMoon.Aether.Kton` отделяет neutral components/schemas/helpers от
`TrueMoon.Aether.Kton.Runtime` с App/DI/registry и session-owned physics world.
Provider выбирает composition root; основной Runtime и neutral Kton не получили
backend/bridge references. Общий ARCH1j source сохранён без изменений.

| Scope | Реализовано | Фактическая проверка15ноября |
| --- | --- | --- |
| App/DI/configuration | UseAetherKton/UseConfiguredAetherKton, exact schema Kton:Bridge:MaximumContactEvents; lazy typed factories, independent worlds |68tests D/R, Cobalt/MS DI и independent8provider executions |
| ECS/scene | Body/velocity schema v1, box/sphere/capsule, Static/Dynamic/Kinematic, root/unit-scale, explicit velocity/teleport, ExternalMotion | Real BEPU Step/writeback/rotation, schema/default/version/forwarding/filter и rejection cases |
| Phase lifecycle | Gameplay spawn/delete/attach/detach, descriptor replacement, stale/foreign generations | Same-tick reconciliation, fresh body IDs, original stale End mapping; helpers validate write declarations |
| Events | PhysicsContact transient entities после Movement, Gameplay next-tick, bounded full projection | Begin/Stay/End/retirement, mask0, exact capacity/overflow и command capacity failures |
| Ownership/failures | Scheduler systems → native world → borrowed ECS World; cleanup retry retains owner/lease | Cancel/factory/shape/Step/cleanup errors, failed candidate сохраняет old session; owners0/pools0 |
| Delivery/build |2new packages, common SDK/TFM/profile, pure SDK/explicit provider closure | Solution D/R0errors/1570existing warnings;2metadata archives; source/published/fresh42checks×4reports, closed runtime graph20libraries |

Новый xUnit suite: **68passed/0failed/0skipped каждый Debug/Release**. Точный slnx
entrypoint обнаружил68cases; common subsystem regression104D/R. Полный15-suite
run и новая native/GPU/physical/platform acceptance не выполнялись.

Ограничения: exact60Hz, root/unit-scale COM primitives, no mesh/joint ECS/controller.
Queries остаются neutral Kton API; отдельный injected game/session query reader,
physics checkpoint/native-state migration и game-artifact shared ABI не расширены.
Contact projection default64/max256, overflow terminal; Ecs command/entity/type
budgets проверяются существующим scheduler. PhysicsBody readonly pose меняется
через explicit teleport, runtime roles — через PhysicsEntityCommands.

## Следующий конкретный шаг

Общая последовательность ARCH1j → PHYS5 → [Kul ARCH1h.2a](../audio/ECS_BRIDGE.md)
завершена в своих профилях. Новая совместная same-entity проверка обоих DI и68PHYS5
regression cases каждый D/R выполнены в Kul acceptance; прежний PHYS5 manifest сохранён.
Следующий физический feature — [PHYS6 controller](PLAN.md#phys6), затем PHYS8 wider delivery/
performance по отдельному scope. PHYS7 ragdoll необязателен. Previous PHYS4 и
pre-PHYS5 STATUS сохранены в [истории](HISTORY.md#physics-status-before-phys5-2026-11-15);
старые manifests не переписывались. Claims/requests не менялись.

<a id="dep1-2026-11-15"></a>
## DEP1 dependency refresh — 2026-11-15

Новая полная D/R проверка, six-package version refresh и согласованная native Skia4.156.1 поставка выполнены в [единственной межмодульной записи](../core/HISTORY.md#dep1-2026-11-15). Прежние manifests и функциональные gates сохранены; точный scope нового запуска — в записи Core.

<a id="physics-status-before-phys6-2026-11-16"></a>
## STATUS перед PHYS6 — 16.11.2026

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-15 — DEP1: BEPU остаётся актуальным2.5.0-beta.33, PHYS5 regression прошёл.**
NuGet показывает stable2.4.0 и latest existing beta2.5.0-beta.33; downgrade не выполнялся.
Exact BepuPhysics/BepuUtilities ranges, provider/C# API/source и ownership сохранены.
Полный Aether.Kton suite68passed каждый Debug/Release,0failed/0skipped; actual BEPU
bridge/scene lifecycle входят в этот suite. Самостоятельный309-case provider runner
в dependency refresh отдельно не повторялся.

[Контракт PHYS5](ECS_BRIDGE.md), [его детальная задача](PLAN.md#phys5),
[DEP1 и новая межмодульная приёмка](../core/HISTORY.md#dep1-2026-11-15),
[manifest/SHA](../core/DEPENDENCY_REFRESH_20261115.json).
Прежние full provider и independent PHYS5 delivery results сохраняют свои даты:
[предыдущий STATUS](HISTORY.md#physics-status-before-dep1-2026-11-15).

Следующий физический feature — PHYS6 controller, PHYS8 по отдельному scope;
PHYS7 необязателен. Общая очередь остаётся у engine, Requests/Claims не менялись.

<a id="phys6-2026-11-16"></a>
## PHYS6 — контроллер капсулы / Aether — 16.11.2026

Версия `1.2611.16.2-alpha` зафиксирована до итоговых сборок/проверок. Начальная и
финальная origin/main — `54640e1eb7adf54d4e79c43afae37c7b80f7b583`, Version1.2611.16.1-alpha;
новая alpha версия выше при NuGet comparison. [Контракт/fixture matrix](CHARACTERS.md),
[единственная задача](PLAN.md#phys6), [источники/архивы/report SHA](PHYS6_RESULTS.json).

Добавлены neutral optional character capability и generation-safe handles; borrowed
upright dynamic capsule, locked rotation/inertia restoration, velocity/support/slope,
single jump, dynamic push/recoil, moving point velocity, teleport/remove invalidation.
BEPU demo exact commit29ed466870534c413aba9eeccd813cf15d135d0b адаптирован отдельно
от renderer/input; Apache license, provenance и исходный `.tt` входят в пакет.
В Aether — optional Character v1 schema, reference-free settings/input/state,
monotonic jump sequence, same-tick pose/support, runtime roles/cleanup и full preflight.

Финальные проверки, все exit0:

| Проверка | Команда/артефакт | Результат |
| --- | --- | --- |
| xUnit2/VSTest Debug/Release | `dotnet build TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj --no-restore -c Debug`; final Release built through solution; затем `dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj --no-build --no-restore -c Debug` / `-c Release`, TRX/logger/unique binlogs в `TestResults/PHYS6/accepted-*` | 124passed каждый,0failed/0skipped:56PHYS6+68PHYS5 |
| Solution build D/R | `dotnet build TrueMoon.slnx --no-restore -c Debug` / `-c Release`, unique binlogs `accepted-solution-*` | 0errors; существующие XML/nullability/analyzer warnings сохраняются |
| Independent delivery | `pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory TestResults/PHYS6-delivery-accepted` | 52checks каждый source Debug/published Release/fresh NuGet Debug/Release; оба DI;14coherent packages, exact external closure, output/cache SHA и3license entries проверены |
| PHYS2–3 regression | `pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/PHYS6-provider-accepted` | 309checks каждый source Debug/fresh package Debug/published package Release |

В финальных independent profiles world/resource owners и pool bytes после cleanup0.
Проверены все fixtures PHYS6: floor/mesh seams/crest/moving platform/ceiling/teleport/
support loss; обе60Hz настройки для floor/jump, linear+angular platforms, dynamic
jump recoil и filter/sensor exclusion. Exact test names — в CHARACTERS.md и тестах.
Gap/assertion review закрыл world-Z/yaw, внутренние mesh diagonals, dynamic recoil,
joint exclusion в обоих порядках и отсутствие preflight writeback движущегося соседа.
Статический pairing analyzer не запустился из-за отсутствующего Microsoft.CodeAnalysis4.14;
его результаты не используются. Research/plan/review остались в локальном temp scratch.

При проверке packaging обнаружен лишний path segment Characters; исправлен
PackagePath metadata по MSBuild item-management, затем delivery полностью повторён.
Валидатор input также проверяет валидность options; regression фиксирует NaN/ceilings.
Пакеты используют persisted repository Version, временный QA suffix не подменяет её.
Новые immutable feeds/evidence создавались отдельно; прежние archives/manifests не
перезаписывались. Raw artifacts локальны/ignored, retention до ручной очистки;
этот HISTORY и manifest сохраняют команды/scope/counts/SHA без зависимости от temp.

Это Windows x64 CPU/headless acceptance. Полные16suites, native/GPU/physical,
другие ОС, shared-game-ABI/mod-state migration и PHYS8 performance/release matrix
здесь не повторялись. Native Skia/SDK/TFM/dependencies не менялись и native rebuild
не требовался. Auto-stairs/crouch/root motion/air steering не входят в PHYS6.
Следующий физический этап — отдельно выбранный PHYS8 scope; PHYS7 ragdoll optional.

<a id="physics-status-before-phys81-2026-11-16"></a>
## STATUS перед PHYS8.1 — 16.11.2026

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-16 — PHYS6: контроллер капсулы и Aether integration завершены.**
Версия `1.2611.16.2-alpha`; [контракт/fixtures](CHARACTERS.md),
[единственная задача](PLAN.md#phys6), [команды](HISTORY.md#phys6-2026-11-16),
[manifest/SHA](PHYS6_RESULTS.json).

Нейтральная optional capability заимствует upright dynamic capsule, блокирует
вращение и восстанавливает инерцию после снятия. Скорость/торможение, support/slope,
прыжок, динамические и moving supports, teleport/invalidation реализованы. Aether
получает reference-free настройки/input/state, v1 authoring schema, jump sequence,
same-tick pose/support и phase-boundary lifecycle. BEPU demo license/provenance
и исходный constraint template включены в provider package.

Фактически выполнено:124passed каждый Debug/Release (56новых+68PHYS5),0failed/
0skipped; solution builds D/R0errors. Independent both-DI consumer52checks в каждом
source Debug/published Release/fresh NuGet D/R профиле;14точных пакетов одной версии,
license/content SHA и fresh graph/output hashes проверены. PHYS2–3 regression309×3
также прошёл. Финальные world owners/pools равны0.

Это CPU/headless Windows x64 scope. Полные16suites, GPU/physical/other OS, native
mod-state migration и полная release matrix сейчас не повторялись. Native Skia,
dependencies и SDK/TFM не менялись. Auto-stairs, crouch/root motion и air steering
в PHYS6 не входят. [Прежний статус](HISTORY.md#physics-status-before-phys6-2026-11-16).

Следующий физический этап — PHYS8: отдельно выбрать workload/performance и platform/
release scope. PHYS7 ragdoll остаётся необязательным; общая functional очередь у engine.

<a id="phys81-2026-11-16"></a>
## PHYS8.1 — Windows x64 baseline — 16.11.2026

[Метод/таблица/ограничения](PERFORMANCE.md), [задача](PLAN.md#phys81),
[полные Release числа/source/package/report SHA](PHYS81_RESULTS.json).
Только новый `ManualTests/KtonPerformance` consumer и документация; все93source
SHA PHYS6 сохранились, production API/build inputs/пакетное содержимое не менялись.
Версия1.2611.16.2-alpha сохранена; предыдущие PHYS6 manifests/archives не переписаны.

Финальная команда (exit0):
`pwsh -NoProfile -File ManualTests/KtonPerformance/Verify.ps1 -EvidenceDirectory TestResults/PHYS81-04`.
Shared BuildProfile/SDK11.0.100/net11/runtime-async и explicit CPM isolation;
14точных TrueMoon packages/closed external feed, fresh package-only graph и DLL
output/cache SHA проверены. Не выполнялось remote publishing.

| Профиль | Результат |
| --- | --- |
| source Debug | 12smoke workloads ×600samples, оба DI/оба пути |
| source published Release | 28baseline workloads ×600samples |
| fresh NuGet Debug | 12smoke workloads ×600samples |
| fresh published Release | 28baseline workloads ×600samples |

Всего80fixtures,48000measured Steps и120warmup ticks наfixture; в каждом App/world
независимы. Матрица100/1000active/sleeping bodies,1/32/256walking controllers,
standalone public Step и full PrimitiveGameSession.Tick. Отсутствуют drawable
components; сохраняется пустой render-snapshot bookkeeping. Worker1/8iterations/
4substeps/Baseline60, current body ceilings. Для256-controller ECS выбран explicit
allowed bridge MaximumContactEvents256(обычный default64), другиеfixtures сохраняют64.
Никакие production defaults/ceilings не повышались. Published gameplay contact
events суммарно1392000; raw narrowphase invocations не инструментированы.

На AMD Ryzen9 9900X/.NET11 x64/AVX2+AVX512F/workstation GC основной устойчивый
allocation witness: ECS1000active~444653–444764Б/tick, sleeping1000~444653Б/tick;
standalone1000active/sleeping0Б/tick. Full ECS active1000p99~0.897–1.1785мс,
sleeping1000~0.8099–1.0057мс, диапазон четырёх Release observations. Это весь session
tick; attribution не проводилась, виновник не устанавливается по одному macrobaseline.
Standalone32/256controllers дают около56Б/tick. Step p99 diagnostic1ms превышен
в двух Cobalt standalone256observations; бюджет не менялся и не является SLA.
Fixed-order/tiered-JIT bias не устранён, p99 не используется для DI comparison.

Pool high-water максимум5160960Б (только sampled BEPU pools, не process/native RSS).
Во всех80fixtures после Stop0worlds/owners/pool bytes. Максимальный scene/world
teardown4.229мс по всем и3.0446мс по Release; App Stop туда не включён.
GC allocation counters primary-thread/process-wide и approximate managed heap
записаны отдельно; искусственных GC нет. Таймеры используют preallocated buffers,
percentiles nearest rank; setup/validation/JSON/cleanup не входят в окно.

Свежая функциональная регрессия (оба exit0):
`dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj --no-build --no-restore -c Debug`
и `-c Release`, unique binlogs/TRX в `TestResults/PHYS81-04/regression-*` — каждый
124passed,0failed/0skipped. Source hashes подтверждают соответствие уже собранных
assemblies неизменным входам. Полные solution/16suites/native/GPU сейчас не повторялись.

Промежуточные failed runs сохранены отдельно: неверное чтение registry исправлено
на точный CPU string; probe factory/query signatures приведены к API. Попытка
drawable workload выявила существующий primitive limit; headless physical fixtures
используют сущности без Appearance.256-controller ECS default64contact projection
действительно вызвал typed ContactOverflow; финальный allowed preset256проходит.
Ни один из этих failed attempts не выдан за completed baseline.

Raw TestResults/temp feeds/caches локальны/ignored до ручной очистки. Этот HISTORY
и versioned manifest сохраняют scope/commands/counts/measurements/SHA без требования
доступности temp. Research не подтверждает10000bodies,2/4/8workers,ragdoll,raw
callback/stalls,visual/interpolation/physical,other OS/AOT или общую PHYS8 release
readiness. Следующий срез — PHYS8.2 attribution и уменьшение подтверждённых allocations.

<a id="physics-status-before-phys82-2026-11-16"></a>
## STATUS перед PHYS8.2 — 16.11.2026

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-16 — PHYS8.1: первый Windows x64 performance baseline завершён.**
[Задача/очередь](PLAN.md#phys81), [метод и числа](PERFORMANCE.md),
[команды/ограничения](HISTORY.md#phys81-2026-11-16), [manifest/SHA](PHYS81_RESULTS.json).
Production не менялся: все93source SHA PHYS6 сохранились, Version1.2611.16.2-alpha.

Новый non-packaged measurement consumer:100/1000active/sleeping bodies,
1/32/256walking controllers, standalone и Aether session, оба DI. Source/fresh Debug
smoke по12workloads и published source/fresh Release baseline по28;80fixtures,
48000measured Steps.14точных пакетов, closed feed/fresh graph/output hashes проверены.
По124functional tests каждый Debug/Release,0failed/0skipped. Owners/pools после
cleanup0во всех fixtures; общий PHYS8 ещё открыт.

На Ryzen9 9900X/.NET11 в Release ECS1000active: full tick p99~0.90–1.18мс,
около445КБ allocations/tick; sleeping1000также около445КБ/tick. Это наблюдение
полного session tick, точный вклад требует attribution. Standalone1000active
показал0Б/tick; standalone32/256controllers — около56Б/tick. SIMD/solver/pool/GC/
teardown и profile-specific значения сохранены. Fixed-order/tiered-JIT bias не
устранён, performance SLA/сравнение DI overhead не заявляются.

Baseline60/1worker/current ceilings. Для256controllers ECS явный allowed projection
preset256вместо default64; production defaults прежние.10000bodies/2/4/8workers,
raw callbacks/stalls, drawable/GPU/visual, other OS/AOT и release matrix отдельно.
[Прежняя PHYS6 сводка](HISTORY.md#physics-status-before-phys81-2026-11-16).

Следующий срез — [PHYS8.2](PLAN.md#phys82): attribution и уменьшение allocations
полного ECS-пути с сохранением preflight/identity/lifecycle/contacts и повторной
проверкой на baseline fixtures. PHYS7 ragdoll остаётся необязательным.

<a id="phys82-2026-11-16"></a>
## PHYS8.2 — attribution/оптимизация allocations — 16.11.2026

Версия1.2611.16.3-alpha выбрана до финальных сборок/проверок; origin/main на
выборе54640e1eb7adf54d4e79c43afae37c7b80f7b583,Version1.2611.16.1-alpha; local
предыдущий набор1.2611.16.2-alpha. Generation/stage не менялись. [Контракт/изменения/
результаты](ALLOCATION_OPTIMIZATION.md), [task](PLAN.md#phys82), [manifest/SHA](PHYS82_RESULTS.json).

Применены analyzing-dotnet-performance (critical+collections/memory/structural
references), dotnet-trace-collect, binlog-generation/run-tests, code-testing-agent.
Bounded scan recipes/counts хранятся в manifest;7/7class declarations sealed,
строковых critical patterns0; оставшиесяnewList/LINQ/ToArray в scan scope — cold
scheduler construction/cleanup, а не новый hot target. Sampled allocation stacks
подтвердили Candidate/PhysicsBodyDescription в Capture, entry snapshots в Reconcile
и capture closure. Shared contact command/validation types остаются отдельной целью.

Production changes: bounded per-world resettable Candidate scratch; immutable
body/controller settings; additive supplied-motion ValidateBody overload с прежним
полным policy/shape/motion/mass/material/filter admission; staged removals вместо
snapshot array; single-pass min/max mass; authored-root lookup вне nested hot capture.
Не пропущены full preflight/root/scale/roles/sequence/shape/motion checks, generation
reuse/delete/replace или retry ownership. Schema/contact entity timing прежние.

Финальные команды (все exit0, unique binlogs/TRX/report hashes в manifest):

| Проверка | Команда/результат |
| --- | --- |
| Solution | `dotnet build TrueMoon.slnx --no-restore -c Debug` / `-c Release`:0errors; существующие XML/nullability/analyzer warnings548/1570 |
| xUnit2/VSTest | `dotnet test TrueMoon.Aether.Kton.Tests/TrueMoon.Aether.Kton.Tests.csproj --no-build --no-restore -c Debug` / `-c Release`:155passed каждый,0failed/0skipped;31new cases |
| Macro+exact cohort attribution | `pwsh -NoProfile -File ManualTests/KtonPerformance/Verify.ps1 -EvidenceDirectory TestResults/PHYS82-final -ComparisonReport TestResults/PHYS81-04/verification.json`:4normal profiles/80fixtures +12before/12after attribution,104fixtures/62400samples |
| Neutral contracts | `pwsh -NoProfile -File ManualTests/KtonContracts/Verify.ps1 -EvidenceDirectory TestResults/PHYS82-contracts`:131checks×3 |
| Provider | `pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/PHYS82-provider`:309checks×3 |
| ECS delivery | `pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory TestResults/PHYS82-ecs`:52checks×4,обаDI,14exact packages и3license entries/hash |

Macro consumer проверен source Debug/published Release/fresh NuGet Debug/Release.
Для comparison один и тот же новый allocation-marker consumer восстановлен против
immutable14старых `.2-alpha` и14новых `.3-alpha` архивов в разныхfreshcache; exact
SDK/TFM/LangVersion/features, package-only graphs, source/archive/output SHA verified.
Владельцы worlds/resources и pool bytes после Stop0во всех104fixtures. Новые
артефакты/evidence создавались отдельно, immutable старые не перезаписывались.

Точные primary-thread results:1000active444653–444765→440–841.43Б/tick(>99.8%),
1000sleeping444653.44→440Б(99.90%),256controllers334266.08→209719–209732Б(~37.26%).
На1000sleeping phase breakdown:bridge444261.44→48Б,nativeStep0→0,movement96→96,
playback0→0,transform88→88,emptyextraction208→208. Для256controllers осталось
~112.7КБ recording+~96.6КБ playback и~56–68Б nativeStep. Shared scheduler/commands/
transforms production files не менялись. Attributed counters primary-thread,
all-thread totals отдельно. Timing SLA/DI comparison не заявляются из-за fixed
warmup/order/tiered-JIT bias; profiling trace timings не смешиваются с baseline.

Before/after EventPipe traces собраны cached dotnet-trace `collect --profile gc-verbose`
из immutable consumers; проанализированы cached TraceEvent3.1.21. Raw trace/parser
source/output SHA сохраняются в manifest. GCAllocationTick weights включают setup
и не выдаются за точные byte totals. После оптимизации прежние per-frame hot
types перестали доминировать; shared command/validation types подтверждены повторно.

Новые tests проверяют allocations warmed128body graph и реальный pose/tick/cleanup,
scratch shift/char roles/delete/generation reuse, изменённый descriptor/settings до
native replacement/writebacks, mass ratio100и соседний float, supplied-motion
валидацию всех immutable fields/pose/speed/static rules. Gap/assertion review завершён;
31cases/9methods/78assertion sites,0empty/trivial-only. Pairing analyzer запущен один
раз, prerequisite Microsoft.CodeAnalysis4.14 отсутствует; его результаты не использованы.
Промежуточные TraceLog nettrace-conversion и test ReadOnlyMemory compile ошибки
исправлены; failed attempts не названы итоговой приёмкой. Исходники тестов заморожены
до155-case D/R runs.

Raw TestResults/binlogs/TRX/traces/temp feed/cache локальны/ignored до ручной очистки;
versioned HISTORY+manifest сохраняют commands/scope/counts/measurements/SHA. SDK/TFM/
dependencies/native binaries не менялись и native rebuild не требовался. Не выполнена
новая full16suite/GPU/visual/otherOS/AOT/expanded workers acceptance; PHYS8 в целом
открыт. Следующая measured цель — shared command recording/playback с сохранением
atomic preflight, independent maps, token/generation и private-type/retired-ALC lifetime.

<a id="physics-status-before-phys83-2026-11-16"></a>
## STATUS перед PHYS8.3 — 16.11.2026

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-16 — PHYS8.2: allocation attribution и оптимизация ECS-пути завершены.**
Версия `1.2611.16.3-alpha`; [задача](PLAN.md#phys82),
[источники/изменения/числа](ALLOCATION_OPTIMIZATION.md),
[команды/границы](HISTORY.md#phys82-2026-11-16), [manifest/SHA](PHYS82_RESULTS.json).

Trace подтвердил per-body Candidate/PhysicsBodyDescription и entry-array snapshots
в bridge. Bounded per-world scratch и immutable templates, полный supplied-motion
validator, staged removals/mass scan убрали hot churn. Full preflight до native
mutation, roles/shape/motion/mass/sequence, identity, lifecycle и contact timing прежние.

Один probe наimmutable2/3-alpha package cohorts:1000body ECS~445КБ→440–842Б/tick
(>99.8%),256walking controllers334КБ→210КБ(~37.26%). Для1000sleeping bridge48Б,
native0Б, movement96Б, transforms88Б, empty extraction208Б. Phase counters и
allocation traces независимы от timing baseline; SLA/DI overhead comparison нет.

155functional tests каждый Debug/Release(31новый),0failed/0skipped; solution builds
D/R0errors. Neutral contracts131×3/provider309×3/independent ECS52×4 прошли;
14новых и14старых packages, обаfreshgraphs/cache/output/license SHA проверены.
104macro/attribution fixtures,62400measured Steps; финальные worlds/owners/pools0.
Native/SDK/TFM/dependencies не менялись; full16suites/GPU/other OS не повторялись.
[Предыдущий baseline статус](HISTORY.md#physics-status-before-phys82-2026-11-16).

Следующая измеренная цель — shared deferred contact command recording/playback:
~112.7КБ+96.6КБ/tick при256controllers, с отдельным контрактом immutable maps,
tokens/generations/private-type/ALC lifetime. Общий ECS buffer пока прежний.
Expanded capacities/workers, raw callbacks/stalls, visual/interpolation/other OS/AOT
и общий PHYS8 release scope открыты; PHYS7 ragdoll optional.

<a id="phys83-2026-11-16"></a>
## 16.11.2026 — PHYS8.3 deferred-команды контактов

Пользователь запросил уменьшить allocations deferred-команд контактов. Источники
из PHYS8.2 подтверждены phase counters:~112.7КБ recording/bridge +96.6КБ playback
на256controllers. Compact value records/copied payload blocks и reused validation/
targets/reservations устранили churn. Detached published mapping сохранён, private
Type handlers не кешируются статически и освобождаются после pending batch.
[Контракт/числа/точные test names](DEFERRED_COMMAND_ALLOCATIONS.md),
[manifest/SHA](PHYS83_RESULTS.json). Общий API/schema/generation/contact timing прежний.

Версия1.2611.16.4-alpha выбрана до финальных проверок относительно рабочей .3-alpha
и remote main .1-alpha (SHA54640e1eb7adf54d4e79c43afae37c7b80f7b583).
Промежуточные непубликованные QA feeds сохранены раздельно без перезаписи архивов;
финальная immutable cohort — PHYS83-final-03. Legacy PHYS6 stage label ECS consumer
описывает прежний набор52checks, а не итоговую readiness PHYS6/8 по новому запуску.

Фактически выполненные команды (каждой MSBuild команде отдельный binlog):

```powershell
pwsh -NoProfile -File ManualTests/KtonPerformance/Verify.ps1 -EvidenceDirectory TestResults/PHYS83-final-03 -ComparisonReport TestResults/PHYS82-final/verification.json
pwsh -NoProfile -File ManualTests/KtonEcs/Verify.ps1 -EvidenceDirectory TestResults/PHYS83-ecs-03
# Сначала solution nonincremental D/R, затем последовательные suites без других SDK работ:
foreach ($configuration in @('Debug','Release')) {
    dotnet build TrueMoon.slnx -c $configuration -m --no-restore --no-incremental '-bl:TestResults/PHYS83-new-build-{}.binlog'
}
foreach ($project in @('TrueMoon.Aether.Tests','TrueMoon.Aether.Kton.Tests','TrueMoon.Aether.Kul.Tests')) {
    foreach ($configuration in @('Debug','Release')) {
        dotnet test "$project/$project.csproj" -c $configuration --no-build --no-restore --logger trx --results-directory TestResults/PHYS83-new-tests '-bl:TestResults/PHYS83-new-test-{}.binlog'
    }
}
```

Полные Aether3554/Kton157/Kul103passed каждый Debug/Release,0failed/0skipped;
20новых ECS и2contact cases. Solution D/R0errors/1570warnings; independent ECS52×4;
macro4профиля80fixtures +before/after attribution12×2 =104fixtures/62400measuredSteps.
Оба DI, source/publish/fresh graphs/cache/output,14старых+14новых archives/license
SHA проверены; после всех fixtures owners/worlds/pools0. Exact-consumer256controller
full ECS209719–209732→6720Б/tick(~96.8%), bridge48Б/playback6224Б. Primary-thread phase
counters отделены от baseline timing; timing SLA не назначался.

Первый focused run выявил, что70KB struct не поддерживается текущим CLR T[] pool:
TypeLoadException; допустимый60KB value подтвердил oversized16KiB-block handling.
Первый full Debug дал diagnostic StopAsync timeout в minimum50ms fixture; связь с
параллельным forced GC вероятна, не доказана. Новые allocation/ALC tests получили
отдельную nonparallel xUnit collection, полные D/R повторены. Diagnostic tests и
production deadlines не менялись; первый failed TRX/input hashes сохранены.
Static pairing analyzer не запущен успешно из-за прежнего prerequisite
Microsoft.CodeAnalysis4.14; его выводы не считаются test evidence.

Native/ABI/dependencies/SDK/TFM прежние, native rebuild не требуется. Full16suites,
GPU/visual/interpolation/other OS/AOT, expanded limits/workers/raw callbacks/stalls
и общий PHYS8 release scope не подтверждались. Raw TestResults/temp feeds локальны;
versioned manifest хранит observed numbers/scope/source/archive/report/test hashes.

<a id="physics-status-before-phys84-2026-11-17"></a>
## STATUS перед PHYS8.4 — 17.11.2026

# TrueMoon.Kton: состояние и следующий шаг

**2026-11-16 — PHYS8.3: allocations deferred-команд контактов уменьшены.**
Версия `1.2611.16.4-alpha`; [задача](PLAN.md#phys83),
[реализация/числа](DEFERRED_COMMAND_ALLOCATIONS.md), [команды](HISTORY.md#phys83-2026-11-16),
[manifest/SHA](PHYS83_RESULTS.json). [Предыдущий статус](HISTORY.md#physics-status-before-phys83-2026-11-16).

Shared Runtime buffer хранит value records/copied payload blocks и переиспользует
validation/targets. Полный preflight, independent mappings, retry/fault semantics,
tokens/generations и private-type/ALC lifetime сохранены; API/schema прежние.

Один probe наimmutable `.3-alpha`/`.4-alpha` cohorts:256controllers full ECS
209719–209732→6720Б/tick(~96.8%) на обоих DI; bridge48Б, playback6224Б.
Последние6224Б сохраняют detached result/256reservation handles. Timing SLA нет.

Свежая регрессия D/R: Aether3554, Kton157, Kul103passed каждый,0failed/0skipped;
22новых cases, solution D/R0errors. Independent ECS52×4 и104macro/attribution
fixtures/62400measured Steps прошли;14старых/14новых package archives, fresh graphs,
output/license SHA и final owners/pools0 проверены. Первый full Debug имел diagnostic
50msStop timeout; после изоляции forced-GC/allocation tests полные D/R повторены.
Причина конкретного timeout не доказана, production deadlines не менялись.

Native/SDK/TFM/dependencies прежние, native rebuild не нужен. Full16suites/GPU/other OS
не повторялись. Далее PHYS8: expanded capacity/worker presets, raw callbacks/stalls,
visual pose/interpolation/Stop и platform/release matrix; PHYS7 ragdoll optional.

<a id="phys84-2026-11-17"></a>
## 17.11.2026 — PHYS8.4 визуальный уровень

По запросу пользователя добавлен непакетируемый ManualTests/KtonVisual на existing
App/DI/PrimitiveGameSession/Kton и Vulkan/OpenGL primitive surface. [Контракт/скриншот](VISUAL_PLAYGROUND.md),
[consumer/run instructions](../../ManualTests/KtonVisual/README.md), [manifest/SHA](PHYS84_RESULTS.json).
Уровень: пол/склон/платформа/кубы/потолок, actual PHYS6 capsule и stepped render proxy.
Consumer интерполирует копии соседних завершённых поз, сохраняет physics tick;
Pause/Step/teleport/reset не оставляют trails. Имеются WASD/Space и mode commands.

Канонический запуск:

```powershell
pwsh -NoProfile -File ManualTests/KtonVisual/Verify.ps1 -EvidenceDirectory TestResults/PHYS84-final-04 -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation
```

Script использует общий BuildProfile и unique binlog каждой MSBuild команды;
exact external archives из restored workspace cache, private offline feed, новый
NuGet cache/output вне checkout. Source Debug/published Release/fresh NuGet Debug/
published Release, каждый CPU/native на Cobalt/MS DI — 16 профилей. 12 CPU-групп и
12 PNG на соответствующий профиль: всего 96 групп/96 PNG. Loaded framework DLL
сопоставлены с isolated cache; graph не содержит project references, все 19 собственных
archives имеют persisted Version. Extended Skia DLL/font/license snapshots совпали
в source/fresh outputs. Source/validation hashes сохранены до и после матрицы.

Проверены actual UI tree/routed input/CPU raster, focus-loss и key-repeat через смену
режима, floor/slope/jump/landing/low ceiling/platform, adjacent pose endpoints и
midpoint, render centres и сохранение physics poses/tick, contacts, один Paused Step,
teleport/reset/Stop. Native три дробных кадра одного tick различаются; два paused
кадра совпадают по pixel SHA. UI команды проходят общий dispatcher interactive loop;
старый ticket после Stop отвергается, новый World имеет другую identity. Native
core/synchronization validation 0errors/0warnings; два surface/device поколения
на профиль освобождены, final GPU owners/handles/UI sessions/physics worlds/pools0.

Development attempts сохранены отдельно: вход склона исправлен с выступающей
ступеньки на плавное пересечение с полом; потолок опущен до реально ограничивающей
прыжок высоты. Первое native начало отказало ErrorLayerNotPresent; найден прежний
локальный Khronos layer, env задан только дочерним процессам. QA wrapper сначала
ожидал standalone Cobalt.Generator package, затем ошибочно переносил analyzer-only
PrivateAssets на runtime Cobalt; отдельная package reference исправлена, полная
матрица после исправления и уточнения потолка повторена. Production code не менялся.

Дополнительный Play.ps1 Release build открыл окно. Computer Use получил правильный
title/id, но снимки показывали перекрывающий Rider, reliable activation/input не
подтверждены. Ввод прекращён; после отмены exec оставшийся только наш child
остановлен по подтверждённым PID/command line. Это не passing ручного Win32 input
или штатного закрытия interactive loop; эта граница остаётся открытой.

Общая Version 1.2611.16.4-alpha сохранена: изменения только ManualTests/docs,
framework/native/dependencies/ABI/shaders прежние. Полные solution/unit suites
не запускались заново; исторические PHYS8.3 результаты не считаются новым прогоном.
Exact collider mesh, ручной Win32/DPI, Editor/Player IPC/ALC/reload, expanded limits/
workers/raw callbacks/stalls, device loss/other OS/AOT и общий PHYS8 release scope
самостоятельны. Raw TestResults/temp evidence локальны; tracked manifest сохраняет
hashes/counts/scope, один исходный native PNG сохранён в docs как пример, не baseline.
