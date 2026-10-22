# Рабочий уровень модулей перед движком

Editor ENG7a **2026-10-22**: новый desktop TrueMoon.Editor/Tests,39новых xUnit cases,
full Debug/Release2253/configuration,10TRX/failed0/skipped0. Native UI proof
Debug/published Release: по8commands/3PNG/9material pixels/2reaped runtime children,
core/sync validation0/0 и zero owned resources. Published WinExe apphost проверен
напрямую. Engine/UI production modules не менялись; прежние engine native60,
Alloy golden/package/performance/hardware матрицы не повторялись.
[Requirement/evidence](engine/HISTORY.md#eng7a-основной-экран-редактора--2026-10-22).

UI PA8a **2026-10-22**: дефолтный квадратный стиль Dark/Light и standard custom
frame реализованы.28новых xUnit cases; nonincremental Debug build, full Debug и
Release2214/configuration,9TRX/failed0/skipped0; UI643/Hosting79/Aether780.
SettingsSquare360full-frame comparisons,8default-frame native cases,
16source integration и10fresh packages/36App+4direct renderer cases прошли.
12gallery PNG synthetic100/150/200% просмотрены выборочно; прежние golden fixtures
и tolerances сохранены. Physical DPI/OS input/K3/K7/K8/VRAM/final alpha открыты.
Engine native/3D/performance matrix на этом UI этапе не повторялась.
[Requirement/evidence и промежуточные отказы](alloy/HISTORY.md#pa8a-дефолтный-квадратный-стиль--2026-10-22).

ENG5c.2 **2026-10-22**: nearest affine raycast/reach/opaque occlusion,
per-object switches/materials и explicit TMCK2 atomic restore.52новых cases,
focused102/102; nonincremental Debug build и full Debug/Release2186/configuration
прошли,9TRX/failed0/skipped0; Aether780.
Fresh full native60/60cases/62commands,478source/76binary fingerprints после
JSON compatibility fix. Debug/published Release interaction по10cases/9PNG,
прежние CPU/GPU/Editor/input/Replace/failures; core/sync0/0/zero ownership.
Local Aether pack/DLL SHA совпал с final proof; package drawing не запускался.
[Requirement/evidence](engine/HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Далее ENG5c.3 collisions камеры, затем HUD; full World/edit/MCP/raw input/3D perf
и прежние hardware/release gates остаются открытыми.

ENG5c.1 **2026-10-22**: built-in schemas и explicit parent world transforms,
control-v3 primitive payload и static authoring room в отдельном runtime.
36новых cases; full Debug/Release2134/2134/configuration,9TRX/failed0/skipped0,
Aether728. Fresh native58/58cases/60commands,473source/76binary hashes:
Debug/published Release room по5PNG и прежние CPU/GPU/input/Replace/failures;
core/sync0/0, zero ownership/imported readback. Local Aether pack и DLL hash
совпали с published proof; package drawing не запускался.
[Requirement/evidence/границы](engine/HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
Далее ENG5c.2 reach/raycast/interaction; full world snapshot/edit/MCP, raw OS input,
3D performance и прежние UI hardware/release gates остаются открытыми.

ARCH1a.2 **2026-10-22**: immutable scene aggregate, parent graph/versioned snapshot
и explicit projection к существующему game consumer;52новых cases, scene suite130.
Full Debug/Release2098/2098/configuration,9TRX/failed0/skipped0; Aether692.
[Requirement/evidence](engine/HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
На этапе ARCH1a.2 native/GPU/package checks не повторялись; wire/frame/TMCK прежние. Тогда далее ENG5c.1
runtime scene/multiple primitives; full snapshot/edit/MCP и release ограничения открыты.

ARCH1a.1 **2026-10-22**: immutable authoring model/resolver,78новых xUnit cases.
Full Debug/Release2046/2046/configuration,9TRX/failed0/skipped0; Aether640.
Прежние control/frame/TMCK не изменены; native/GPU/package checks не повторялись.
[Requirement/evidence](engine/HISTORY.md#arch1a1-immutable-модель-и-resolver--2026-10-22).
На момент ARCH1a.1 далее планировался ARCH1a.2; runtime editing,
full snapshot/MCP и прежние hardware/release/performance ограничения открыты.

ENG5b **2026-10-21**: control v2, optional gameplay input/camera на fixed tick,
capture/reset/identity fencing и interactive GPU sample. Full Debug/Release1968/1968,
9TRX/configuration/failed0/skipped0; Aether562. Native regression56/56 и отдельный
усиленный input proof2/2 по15cases/5PNG; validation0/0/zero ownership. Local
Aether/Argentis/Platform.Silk payload сверены с published proof, package drawing
consumer не запускался. [Evidence/границы](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
Порядок уточнён2026-10-22: ARCH1a.1/2 и ENG5c.1 выполнены, далее ENG5c.2;
raw/physical input и3D perf остаются открытыми.

ENG5a **2026-10-21**: первый Vulkan3D cube/camera/material/depth в существующем
App-owned GPU viewport. Debug/published Release: по5PNG assertions, resize/
suspend/resume/checkpoint Replace, core/sync0/0, zero owned resources и imported
ReadPixels0. Full Debug/Release1926/1926 в каждой конфигурации, failed0/skipped0;
Aether520. [Контракт/commands/failures](engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
Далее ENG5b input/camera и затем комната/HUD. Полный ENG5/3D performance/hardware
readiness не объявлены; исторический ENG4e ниже относится к reference workload.

ENG4e **2026-10-21**: CPU/GPU reference baseline12cases/36intervals,24cooperative
children, frozen budgets/raw captures. Release1280×720 median per-run p95
CPU65–66ms/GPU21–33ms, GPU runtime allocation~107MiB/s. Debug CPU имеет2FPS/
6latency advisory breaches; это measured debt, а не passing performance acceptance.
Mithril delivery/graceful wait race исправлен с red/green/native20; отдельный GPU
validation run6cases прошёл. Full Debug/Release1907/1907 в каждой конфигурации,
failed0/skipped0. Далее ENG5; reference pattern не доказывает будущий3D performance.
[Commands/raw/failures/границы](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).

ENG4d2 **2026-10-20**: общий CPU/GPU presentation contract и ordinary GPU Editor
selected DLL/Build/Replace/checkpoint/rollback/actual first-present. CPU default.
Aether493/493 и UI615/615 Debug/Release; full Debug1898/1898/9TRX/failed0/skipped0;
build0errors/1499warnings; fresh12packages/4consumers. 54native cases/56commands,406source/76binary fingerprints; per GPU Editor20Replace/53children, validation0/0/imported readback0.
[Evidence/границы](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
На момент ENG4d2 далее был ENG4e. Reference pattern CPU-generated/Vulkan
staging, не полноценный 3D renderer. 3D/performance/
VRAM/UI hardware acceptance открыты. [План](PLAN.md), [статус](STATUS.md).

## Матрица

| Направление | Достигнутый рабочий уровень | Предыдущие проверки | Что остаётся за границей |
| --- | --- | --- | --- |
| [Core](core/STATUS.md) | Bounded headless quality проход завершён: configuration/bootstrap, diagnostics, cooperative lifecycle, owned cleanup |2026-10-15:107/107 Debug и Release;52source +80package +3README processes на Cobalt/DI. [Evidence](core/HISTORY.md#сквозной-headless-proof--2026-10-15) | Uncooperative Task не прерывается timeout; concurrent atomic GetOrCreate, crash-safe JSON, другие platform/deployment modes и engine workloads не доказаны |
| [Cobalt / DI](cobalt/STATUS.md) | COB6a–d, COB5/COB7 завершены в собственном TrueMoon API scope. Два projects/один consumer package; Cobalt остаётся default, Microsoft adapter сохранён |2026-10-15 COB7:runtime179/generator91 **Release**, source20functional scenarios и package120behavior scenarios; paired baseline. Core/MIT5 имеют отдельные более поздние integration runs. [Evidence](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15) | Массовая migration explicit UseDI отложена; warm/generic/transient allocations требуют профиля. Scopes, reflection fallback, Microsoft ecosystem bridge и UI equivalence не заявлены |
| [Enerit](enerit/STATUS.md) | Typed same-build pipes, strict response/decode ownership, cancellation/error/reconnect и ENE5 baseline проверены; package composition с Mithril подтверждена отдельно |2026-10-15 ENE5:runtime126/generator114 Debug и Release;10cases/90samples. MIT5 позже:runtime126 Debug/Release и package108; generator/performance там не повторялись. [Baseline](enerit/BASELINE.md), [MIT5 evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15) | MMF/ENE3, schema negotiation, bounded queues/backpressure, remote handler cancellation и uncooperative shutdown не подтверждены. Frame transport ещё не реализован |
| [Mithril](mithril/STATUS.md) | MIT0–MIT5 bounded own-child/App scope; исправлена гонка delivery/graceful wait без расширения budgets |2026-10-21:95/95 Debug/Release, native20shutdown,108fresh package cases; [red/green и evidence](mithril/HISTORY.md#graceful-wait-после-ошибки-доставки-exit--2026-10-21). Standalone restart matrix имеет прежний scope | Borrowed attach отсутствует; independent external restart, arbitrary hooks/uncooperative callbacks и long resource trend не доказаны |
| [Alloy / Argentis](alloy/STATUS.md) | INF1–INF4/INF6/INF7/минимальный INF5; bounded work/frame/close, explicit root/compatibility и воспроизводимый runner |2026-10-21:UI615/Hosting79 Debug/Release, bounded engine measurements/validation отдельно. [INF7 evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19)/golden/RTE имеют свои прежние scopes | Full INF5 частичен. K3/K7/K8/VRAM/final matrix открыты; PA8a/RTE запланированы, далее общий ENG5 |
| [Aether / Engine](engine/STATUS.md) | Одна experimental assembly, App-owned lifecycle, ENG5a/b/c.1/2 room/reach/interaction и authoring ARCH1a.1/2 |2026-10-22:Aether780 Debug/Release, full2186/configuration; native60cases/core-sync0/0, local pack/payload; [evidence](engine/HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22) | Далее ENG5c.3 collisions камеры/HUD; full snapshot/MCP,3D performance и CPU Debug/reference staging debt, VRAM/hardware acceptance открыты |

Предыдущий solution test run — ENG5b2026-10-21: Aether562/Mithril95,
full Debug1968/1968 иRelease1968/1968, по9TRX/failed0/skipped0. Nonincremental
build/full UI packages/golden/RTE/hardware не повторялись; ENG4e performance
относится только к reference workload, GPU query/utilization/VRAM unavailable.

Предыдущий INF7:4TRX/1384passed/failed0/skipped0 (UI613/Hosting79 Debug/Release),
21runner guards,24native processes,10fresh packages. Full solution не повторялся.

Предыдущий INF6 full Debug1490/1490: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/engine87,9TRX/failed0/skipped0; Release UI613/Hosting79.
Source12native processes и8independent native consumers прошли. Nonincremental
build/golden в INF6 не повторялись.

Предыдущий INF4 full Debug solution1466/1466: Core107/Cobalt179/generator91/Enerit126/
generator114/Mithril94/UI613/Hosting55/engine87,9TRX/failed0/skipped0; Release UI613/
Hosting55 отдельно.10manual/engine native processes и2independent native package
consumers прошли. Nonincremental build/golden в INF4 не повторялись.

Последний общий nonincremental Debug build — **2026-10-18, UI INF3**:
0errors/1211warnings. Повторный full Debug1450/1450,9TRX/failed0/skipped0;
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting39/engine87.
Первый full1449/1450/Mithril93/94 и targeted20/20 сохранены отдельно; passing retry
не доказывает устранение readiness race. Release UI613/613 и Hosting39/39 отдельно;
полного Release solution run нет. Hosting project
проверяет реальную Hosting assembly, fake surfaces не выдаются за GPU proof.
10native processes Debug/published Release: scheduling, legacy dispatcher, HUD,
read-only golden360 и2ENG2 viewport; это не32case headless ENG1 run. Independent
frame API проверен на7fresh packages/2headless cases.
[INF3 evidence](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).
Предыдущие2026-10-17 engine87/UI570 Release, package/golden и rich editor matrix
сохраняют прежние scopes, не переносятся на final INF3 source автоматически.
Warnings не подавлены; UI hardware acceptance и общий release R1–R6 не закрыты.

## Решение о порядке работ

1. **UI package/Hosting proof выполнен2026-10-15** после bundled Cobalt и explicit
   font catalog. [Контракт и результат](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15).
   Воспроизведён missing HostedUiDispatcher resolver; explicit factory исправила
   startup без новой зависимости Hosting от Cobalt. Consumer/native/font/analyzer
   provenance и bounded startup/cleanup проверены свежими package consumers.
2. По решению пользователя2026-10-15 выбран ограниченный
   [ENG0 scope](engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15).
   Он определён документом; запрошенный перед ENG1 RichTextEditor/PA7a реализован
   и проверен2026-10-17. Затем выполнен ENG1 headless supervisor;
   ENG2 CPU ring/viewport, ENG3 replacement/Editor и ENG4a/b/c выполнены; далее ENG4d actual GPU viewport.
   K3/K7/K8/VRAM/final UI alpha сохраняются; engine readiness и вся beta не заявляются.
3. Cobalt hot-path allocation и Enerit scalar CPU/allocation profiles остаются
   отдельным измерительным backlog. Оптимизацию выбирать по профилю; новые DI/IPC
   capabilities не становятся обязательными для завершения quality прохода.

Исходная проверка до реализации показала: `AlloyPackageConsumer` выбирал
`UseDI()` без варианта Cobalt, вызывал backend setup без `SkiaFontCatalog`, а
runner сохранял SourceFiles до запуска без финального сравнения. Поэтому его
успех2026-10-11 не закрывал новый совместный scope. Этот gap закрыт fresh proof
2026-10-15; failures и final evidence сохранены в UI HISTORY.

## UI ограничения и условия продолжения

| Критерий | Состояние по предыдущим запускам | Требуемое продолжение |
| --- | --- | --- |
|5.4/5.5 package / Core-Cobalt-Hosting |2026-10-17:10fresh packages,36App +4direct processes с rich text, transitive Topten/HarfBuzz, explicit fonts/native/bundled analyzer и финальные fingerprints прошли | Bounded delivery/cleanup scope завершён; прочие deployment modes и final alpha отдельно |
| K3 / Windows4.9 | Native144DPI/Scale1.5 и directed scales проверены; physical100/200%/разные DPI мониторов не были доступны | Подготовленное окружение и оставшиеся physical input/chrome checks. Смена Windows settings не назначена этой сверкой |
| K7 / lifecycle | Предыдущий full240-window Debug/Release **failed**:GDI23→137 при USER26→26. Raw/debugger trace2026-10-10 локализует screen HDC allocation до NVIDIA ICD32.0.16.1742 | По решению пользователя app-side workaround search остаётся остановлен. Возврат при supported fix либо отдельно согласованной, полностью проверенной alternate GPU configuration; raw AMD stability не является full UI pass |
| K8 / loss и VRAM | Controlled failure/recreate проверены отдельно; physical device/context loss и long VRAM evidence отсутствуют | Собственные воспроизводимые проверки на заявленной конфигурации; не подменять controlled injection реальной потерей |
| Final UI / G4 | Частичный alpha, прежние K1–K9/PA1–PA10 сохранены | Полный согласованный проход остаётся отдельным критерием; package smoke его не закрывает |

Полный UI alpha не является условием выбранного пользователем ENG1–ENG3 prototype.
Допуск явно записан в ENG0; для actual Editor/viewport readiness UI ограничения
сохраняются. Ни K7/G4 pass, ни общий release этим решением не объявлены.
Кроме отдельно запрошенного PA7a, PA1–PA10, MMF, scopes, Hot Reload и GPU frame sharing
не входят в этот quality этап.

## Что означает завершение этой сверки

Сводка и очередность согласованы между общим и модульными STATUS; старые результаты
сохраняют даты. UI Hosting получил собственный bounded code/package/native proof;
Core107/Cobalt179/generator91 отдельно Debug/Release повторены вместе с UI532.
Другие modules/performance не повторялись; full-solution test pass не заявляется.
Выбор DI остальных приложений и системные настройки не менялись.
