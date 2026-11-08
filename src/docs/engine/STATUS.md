# TrueMoon.Aether: состояние движка и редактора

**Editor consumer2026-11-08 — UIA3d.2:** квадратный validation badge применяется
общими TextBox/RichTextEditor/NumberBox; inline rename теперь передаёт InvalidName
в NameEditor с точным tooltip и сбросом при правке/Cancel. Full Editor1412/1412
каждый D/R прошёл. [Аудит/проверки/границы](../alloy/HISTORY.md#validation-badge-2026-11-08).
Нового native Editor/Windows input/full Aether workflow этим срезом не заявлено.

**Editor consumer2026-11-08 — UIA3d:** поля инспектора используют правый error icon/
validator tooltip вместо inline labels; общая Diagnostics и draft/history сохранены.
Island styles используют themed shadows. Full Editor1412/1412 D/R прошёл; это не
новый Aether/native Editor workflow run. [UI evidence](../alloy/HISTORY.md#validation-shadows-2026-11-08); [Vulkan frosted исправлен](../alloy/HISTORY.md#vulkan-backdrop-2026-11-08).

**Editor2026-11-08 — ED5a.4 завершён в bounded Windows x64 v1 scope.**
Camera/selection lifecycle проверен source Debug/published Release и независимым
fresh-package consumer:13groups/17GPU PNG,9children=reaped,8validation reports+
1explicit pre-GPU failure каждый. Windows SendInput Debug/published Release прошёл
выбор/clear, invalid text, wheel/F, LMB drag и RMB/MMB release; stable PID/surface,
invalid draft сохранён,1child=reaped. Все owned UI/GPU/mapping/handle counts0,
validation errors0/warnings0. Исправлен потерянный hidden-camera wait при resume.
Full Editor1412/1412 и bounded Aether2104/2104 каждый D/R, failed0/skipped0;
15fresh packages/8headless delivery cases и2GPU package profiles прошли.
[Команды/tests/границы](HISTORY.md#ed5a4-acceptance-2026-11-08).
Moving RMB/MMB/Esc/focus loss — directed UiSession/native GPU evidence;
Windows driver имеет только LMB drag. Physical held-button/multi-monitor DPI и
device-loss/K7/VRAM gates сохранены. Actual DPI1.5, deterministic tests1/1.5/2.
Далее [ED5b translation contract](EDITOR_PLAN.md#ed5b-translation).
Full solution/full Aether не повторены этой задачей; прежний non-green full attempt
остаётся в HISTORY. Engine f.3/f.4 и независимый Nom не менялись.

**Storage2026-11-08:** независимый Nom завершил [NOM2 App/catalog cutover](../nom/CATALOG.md#nom2-catalog-2026-11-08)
на собственный KV/CAS, LiteDB удалён. [Собственные proofs](../nom/STATUS.md) не
подменяют engine/GPU/package acceptance. Далее NOM3 integrated faults; f.3→f.4/
importer g.1 и зависимости Aether не менялись.

**Исследование2026-11-07:** после выбора [AssimpNetter6.0.5 low-level](MODEL_IMPORT_RESEARCH.md#decision-2026-11-07)
реализован [captured-import research prototype](MODEL_IMPORT_CAPTURE.md#capture-v1):
immutable dependency I/O без disk fallback, численные budgets, persistent ID ledger,
native worker/Windows Job512MiB и cancel→kill→actual exit→release. Source Debug и
published Release прошли по8групп/9started=reaped workers, LiveImports0;
[команды и ограничения](HISTORY.md#captured-import-2026-11-07). Прежнее comparison
regression4variants/8profiles также повторено. Production App/IFactory importer,
automatic dependency/identity discovery и full format/unsupported policy ещё нужны;
весь g.0 не закрыт, fresh production importer/GPU/Editor/Runtime proof не выполнялся.
[Audio ARCH1h.0](PLAN.md#arch1h-audio) запланирован; кодовая очередь ARCH1f.3 сохранена.

**2026-11-07 — ARCH1f.2: реализованы resource owners, leases и preparation.**
Перед ECS commit повторены1230/1230 каждый D/R и solution Debug0errors/1555warnings
с общей версией0.2.5.1-alpha; [текущие команды/scope](HISTORY.md#ecs-commit-2026-11-07).
[Resource API](RENDER_RESOURCES.md): App-owned catalog/device, counted CPU/ready leases,
bounded admission/allocations, atomic World rebind, retained failed cleanup и lazy Vulkan.
Current Debug/Release1230/1230 каждый:137new+1093прежних ECS/Rendering/frame cases.
Source Debug/published Release/fresh7-package CPU и actual Vulkan upload/readback
прошли:2generations+zero-budget denial каждый, validation0errors/0warnings, cleanup0.
Solution nonincremental Debug build0errors/1555warnings, warning catalogue unchanged.
Mesh snapshots/surface renderer/Runtime Playing fence ещё f.3/4.
Full Aether/performance в этом срезе не запускались. Прежний full6ноября
2536/2538 с diagnostics timeouts остаётся non-green; причина не установлена.
Preview/Editor Play/latency scope не обновлены.
[Очередь Editor](EDITOR_PLAN.md#current-editor-queue), [engine roadmap](PLAN.md#current-engine-queue),
[полный прежний STATUS](HISTORY.md#status-before-planning-revision-2026-11-05).

## Реализация и связь с планом

| Область | Текущее состояние |
| --- | --- |
| ARCH2 / App/DI/owned children | Library/package Editor и executable hosts реализованы; bounded source/fresh NuGet/headless/native proof есть. Не полный release/platform scope |
| ED1–ED3 / ED4c/d | Project picker/switch в stopped/idle, persistence/conflicts, entity operations, schema-driven inspector и общая Undo/Redo history реализованы |
| ED4f.3 / UIA2 | EditorMainWindow использует Window; standard/Cobalt и embedded/MS DI сохраняют своих owners |
| Editor typography / PA8c.3a/3b | System catalog/default Variable Text, styled custom text и theme retention реализованы и проверены в указанном scope; повторное внедрение не назначено |
| ED4g.1/2/3/4 | Compact typed editors, numeric gestures/linked Scale, group reset и rad/deg реализованы; final Editor1353/1353 D/R + native source/publish; дальше ED5a |
| ED5a.1–4 | Camera/selection v1 завершён в bounded Windows x64 scope: source/published/fresh-package GPU и supported Windows input,640×360/до64entities. Physical held-RMB/MMB/multi-monitor gates отдельно; далее ED5b translation contract |
| ED6b | Document-aware Build/Play/Replace, capture declarative/project/package inputs, process-local payload reuse и source watcher реализованы в ограниченных profiles |
| Observer ED6b.2e.4a–e | Directory cache/pins, owned native file metadata и bounded open batches4 реализованы; full content/membership/probe hashing и drain сохранены |
| ED6a / ED8a | Console/compiler diagnostics и report/child health реализованы в scoped consumers; aggregate metrics/extended acceptance отдельно |
| ARCH1c/d, ENG7b/ENG9, ED4h/ED7a | Live World edits, Player roles, agents и Game AI inspector не реализованы этим UI срезом |
| ARCH1e.1/2 | World/EntityHandle/value pools, scoped1/2type queries, exclusions, phase commands, sequential graph/access checks и App factories реализованы. World borrowed by scheduler; builtin transform/extraction добавлены в e.4, checkpoint ещё впереди |
| ARCH1e.3a/b | ComponentRegistry/SceneWorldLoader/Codec, initial Runtime capability/chunks и StartWorldAsync реализованы. Полный document, exact schema/refs/counts, atomic SceneWorldGameSession/owned cleanup; actual selected game в child, CPU consumers проверены |
| ARCH1e.4 | Explicit builtin bindings, Local/Parent/World cache, iterative hierarchy, parent-space velocity, ordered systems и detached primitive snapshots реализованы. PrimitiveGameSession готовит graph/frame0 до activation; source/published native pixels проверены |
| ARCH1f / ED4i | F.0/f.1 CPU API и [f.2 resource API](RENDER_RESOURCES.md) реализованы: counted catalog/scene/preparation leases, bounded device cache, atomic reload, cleanup retry и standalone Vulkan upload/readback. F.3/4 owned extraction/submitted-frame leases/surface renderer/Playing fence и ED4i inspector ещё нужны |
| ARCH1g | [Выбран AssimpNetter6.0.5 low-level](MODEL_IMPORT_RESEARCH.md#decision-2026-11-07),8research profiles прошли. G.1 production adapter/captured-I/O/limits/identity ещё нужны; current assets ещё не file importer |
| ARCH1h | Запланировано исследование audio module/backend, real-time commands/clip leases/device lifecycle и ECS source/listener roles; код и package boundaries ещё не выбраны |

## Последние датированные проверки

Текущий f.2 resource run отделён от прежних full module/frame/performance результатов.

| Дата | Scope и результат | Evidence |
| --- | --- | --- |
| 2026-11-07/08 | Default typography: Editor1300/1300 D/R,4native source/published dark/light runs, actual Variable Text600, owners0/validation0/0. Сверка артефактов08ноября без нового запуска | [Editor scope](HISTORY.md#editor-default-fonts-2026-11-08) |
| 2026-11-07 | Resource owners:1230/1230 каждый D/R,137newcases, failed/skipped0. Source Debug/published Release/fresh isolated7pkg CPU и Vulkan:3entities/6components/3assets,2resource generations+zero-budget denial каждый; upload/readback hashes, stale handles, final owners/counters0, core+sync validation0/0. Solution nonincremental Debug0errors/1555warnings. Full Aether/frame/performance не запускались | [Resources](HISTORY.md#render-resources-2026-11-07) |
| 2026-11-07 | CPU render contracts:1093/1093 каждый D/R,309newcases, failed/skipped0. Source Debug/published Release/fresh isolated7pkg CPU:3entities/6components/3assets, exact canonical bytes/hash/manifest, bounds/projection/tint/reset/App factory. Solution nonincremental Debug0errors/1555warnings. Full Aether/native не запускались | [CPU contracts](HISTORY.md#render-cpu-2026-11-07) |
| 2026-11-06 | ARCH1f.0: чтение текущих schema/loader/GPU contracts, выбор opaque-unlit/1 и проверка document links/IDs/budget/color vectors. C# sources/API/dependencies не менялись; новых build/test/native результатов нет | [Contract decisions](HISTORY.md#render-contracts-2026-11-06) |
| 2026-11-06 | Transform/primitive:751/751 D/R (750ECS+frame1),207newcases,10k deep chain. Source/published/fresh7pkg:133logical/2drawables/7source components,2generations каждый. Native source/published:4captures/2generations каждый, red movement/green stationary, validation0/0/final GPU/UI/process owners0. Solution Debug0errors/1555warnings. Full Debug2536/2538:2diagnostics cleanup timeouts, replay25/25; общий regression не green | [Primitive profile](HISTORY.md#ecs-primitives-2026-11-06) |
| 2026-11-06 | Initial Runtime loading: ECS543/543 +updated frame fixture1/1 каждый D/R; full Aether Debug2331/2331, failed/skipped0; solution nonincremental Debug0errors/1555warnings. Source Debug/published Release/fresh isolated NuGet Release:286046bytes/256entities/256components,2generations каждый, CPU pixels0→1tick, no Game.Logic in parent, confirmed cleanup | [Runtime load](HISTORY.md#ecs-runtime-loading-2026-11-06) |
| 2026-11-06 | Loading: all-ECS409/409 каждый D/R (новые201, прежние208), full Aether Debug2197/2197 включая все409ECS, failed/skipped0. Solution nonincremental Debug0errors/1555warnings, новых ECS warnings0. Registry/codec/context/resources/App ownership и Unicode fidelity | [Registry/load](HISTORY.md#ecs-loading-2026-11-06) |
| 2026-11-06 | Execution: latest ECS208/208 каждый D/R, Aether Debug snapshot1994/1994 (206ECS, до2добавленных Stop-error tests), failed/skipped0. Последние2cases проверены latest targeted D/R. Cached one-entity query/delegate workload10000ticks:0bytes D/R; solution Debug0errors/1555warnings | [Execution](HISTORY.md#ecs-execution-2026-11-06) |
| 2026-11-06 | Новый ECS57/57 каждый Debug/Release, full Aether Debug1845/1845, failed/skipped0. Identity/retirement, singleton values,10000entities, limits/options и Cobalt/MS DI ownership; solution nonincremental Debug0errors/1555warnings | [World v1](HISTORY.md#ecs-world-v1-2026-11-06) |
| 2026-11-05 | Full Editor1295/1295 Debug/Release; отдельно Alloy1101/Hosting154 D/R;4native OpenGL runs, standard с6Windows-input stages каждый, final UI/GPU/handles0 | [ED4g.2a](HISTORY.md#ed4g2a-native-numeric-2026-11-05) |
| 2026-11-04 | Observer many-files median scan upper335/470ms против545/602ms; allocations+23%, CPU/cycle+23–43%, budget0 и actual drain. Измеренный tradeoff, не общий speedup | [ED6b.2e.4e](HISTORY.md#ed6b2e4e-observer-open-batches-2026-11-04) |
| 2026-11-04 | Window source/published/fresh NuGet standard/embedded bounded workflows проверены; это прежний run, не пакетная проверка новых numeric controls | [Window consumer](HISTORY.md#ed4f3-window-root-2026-11-04) |

Более ранние full Aether runs и прежние compiler/package/preview traces
сохранены в архиве и HISTORY. Editor suite не считается runtime suite. Повторяемые
runners: ManualTests/AetherPackageConsumer/VerifyNumeric.ps1, VerifyGui.ps1,
VerifyPackages.ps1 и AetherSourceWatchPerformance; full-solution run не собран из их результатов.

## Открытая acceptance и ограничения

- Последний full Aether Debug6ноября имеет diagnostics Stop/Dispose timeouts; в первом
  прогоне был отдельный Delivery.App timeout. Focused replays прошли, но причина
  не установлена. Diagnostics source/таймауты не менялись; это открытый reliability
  результат, а не подтверждение исправления или доказанная регрессия ECS.
- Numeric physical arrows/modifiers/captured Esc/OS pointer modifiers открыты.
  UiKey.None при автоматизации — наблюдение, не доказанная причина реального keyboard defect.
- Drain timeout после reap31октября не закрыт успешными replays. Следующий
  [ARCH2e.4b](PLAN.md#arch2e4b-drain-diagnostics) сначала получает причинное evidence.
- Fixed-camera/GPU-only preview, stopped/idle project switch и поддержанный captured
  SDK graph сохраняют границы. Disk cache, scene migration, полный World/runtime editing отдельно.
- Physical K3/K7/K8/VRAM/general release у UI; восстановление stdout и UI/GPU cleanup
  имеют разных owners. Кооперативный stop не гарантирует прерывание arbitrary hang.

## Границы приложения и engine

Authoring/history/schemas/preview/session/build contracts — Aether; views/styles,
инспектор, единицы отображения и выбор UI — Editor; общие controls/input — Argentis/Alloy.
Подробный backlog находится в одном плане владельца. Новые scopes/protocols/ECS
и загрузка game assembly в CLR Editor для следующих UI задач не назначены.
Поведение конкретной игры/мира принадлежит TrueMoon.Tales/Lore.

## Следующий конкретный шаг

Следующий этап — [ARCH1f.3](PLAN.md#arch1f-rendering): explicit camera/mesh/material
extraction и bounded owned snapshots с согласованными World/tick/revision, masks/bounds,
drop/coalesce и leases до submitted completion. F.2 resource-ready уже реализован;
surface/device matching, frame presentation и required-view Runtime Playing fence — f.4.
PBR/checkpoint отдельно.
Независимое продолжение g.0 — статический model profile, captured dependency I/O,
stable reimport identities и budgets/native cancel/isolation перед g.1 на выбранном
AssimpNetter low-level. H.0 matrix audio backends/codec/lifetimes ещё впереди.
E.4 primitive profile ранее реализован и проверен отдельным native consumer; existing
Editor Play/preview и old64entity projection не мигрированы автоматически.
Game.Logic в Editor не загружается; ECS checkpoint/Replace остаётся e.5,
wire/checkpoint limits не расширены.

Для нашего UI/Editor потока следующий срез — [ED5a.1 camera values/ray math](EDITOR_PLAN.md#ed5a1-camera-values):
контракт ED5a.0 зафиксирован; затем versioned preview metadata и retained navigation/picking. Numeric physical acceptance идёт отдельной проверкой.
Performance альтернатива — [ED6b.2e.4f](EDITOR_PLAN.md#ed6b2e4f-observer-cost),
а не повтор первого profiling/cache/open этапа. Reliability — ARCH2e.4b и текущий
full-solution baseline; release matrix после выбора supported scope.
Gameplay collisions→HUD/Player и Game AI/agents остаются отдельными ветками.
