# TrueMoon.Aether: состояние движка и редактора

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
| ED4g.1/2 | Compact typed editors, shared states, numeric step/drag/cancel и opt-in linked Scale реализованы; group reset/angle units ещё запланированы |
| ED5 | GPU Edit Preview уже подключён к документу: committed Apply/Undo/Redo → revision/presentation fence. Fixed camera,640×360,до64entities, child replacement; camera/picking/gizmos впереди |
| ED6b | Document-aware Build/Play/Replace, capture declarative/project/package inputs, process-local payload reuse и source watcher реализованы в ограниченных profiles |
| Observer ED6b.2e.4a–e | Directory cache/pins, owned native file metadata и bounded open batches4 реализованы; full content/membership/probe hashing и drain сохранены |
| ED6a / ED8a | Console/compiler diagnostics и report/child health реализованы в scoped consumers; aggregate metrics/extended acceptance отдельно |
| ARCH1c/d, ENG7b/ENG9, ED4h/ED7a | Live World edits, Player roles, agents и Game AI inspector не реализованы этим UI срезом |
| ARCH1e.1/2 | World/EntityHandle/value pools, scoped1/2type queries, exclusions, phase commands, sequential graph/access checks и App factories реализованы. World borrowed by scheduler; builtin transform/extraction добавлены в e.4, checkpoint ещё впереди |
| ARCH1e.3a/b | ComponentRegistry/SceneWorldLoader/Codec, initial Runtime capability/chunks и StartWorldAsync реализованы. Полный document, exact schema/refs/counts, atomic SceneWorldGameSession/owned cleanup; actual selected game в child, CPU consumers проверены |
| ARCH1e.4 | Explicit builtin bindings, Local/Parent/World cache, iterative hierarchy, parent-space velocity, ordered systems и detached primitive snapshots реализованы. PrimitiveGameSession готовит graph/frame0 до activation; source/published native pixels проверены |
| ARCH1f / ED4i | F.0/f.1 CPU API и [f.2 resource API](RENDER_RESOURCES.md) реализованы: counted catalog/scene/preparation leases, bounded device cache, atomic reload, cleanup retry и standalone Vulkan upload/readback. F.3/4 owned extraction/submitted-frame leases/surface renderer/Playing fence и ED4i inspector ещё нужны |

## Последние датированные проверки

Текущий f.2 resource run отделён от прежних full module/frame/performance результатов.

| Дата | Scope и результат | Evidence |
| --- | --- | --- |
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
E.4 primitive profile ранее реализован и проверен отдельным native consumer; existing
Editor Play/preview и old64entity projection не мигрированы автоматически.
Game.Logic в Editor не загружается; ECS checkpoint/Replace остаётся e.5,
wire/checkpoint limits не расширены.

Сохранённый UI маршрут: [ED4g.3 group reset](EDITOR_PLAN.md#ed4g3-group-reset), затем
ED4g.4 units и ED5a camera/picking. Numeric physical acceptance идёт отдельной проверкой.
Performance альтернатива — [ED6b.2e.4f](EDITOR_PLAN.md#ed6b2e4f-observer-cost),
а не повтор первого profiling/cache/open этапа. Reliability — ARCH2e.4b и текущий
full-solution baseline; release matrix после выбора supported scope.
Gameplay collisions→HUD/Player и Game AI/agents остаются отдельными ветками.
