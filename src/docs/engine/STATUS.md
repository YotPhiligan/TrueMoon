# TrueMoon.Aether: состояние движка и редактора

**2026-11-11 — CORE7.6: текущая полная сверка configuration/delivery.**
Aether3234/3234 и Editor1496/1496 каждый Debug/Release, Failed:0, Skipped:0.
Aether D/R запускались последовательно с bounded test props override; ordinary
nested legacy-tool restore и причины прежних diagnostics timeouts этим не исправлены.
[Точные inputs/команды](../core/HISTORY.md#core76-2026-11-10).

Source solution D/R0errors; fresh16packages одной версии, source/published/fresh
по230checks всей configuration collection, оба DI и4actual children/profile.
Aether child проверяет pre-App bootstrap/flags/protected captures и typed runtime
graph без его запуска, затем Core/Mithril lifecycle/stop/reap. Окно/GPU/game не
стартуют. [Guide](../core/CONFIGURATION_GUIDE.md), [каталог](../core/CONFIGURATION_KEYS.md).

ARCH1f.3, ED5b и observer4f сохранены; новых actual mesh surface/Playing/native
pixels/device-loss proofs нет. Engine f.4/physical/release очередь открыта.
[Предыдущий STATUS целиком](HISTORY.md#status-before-core76-2026-11-10).

## Реализация и связь с планом

| Область | Текущее состояние |
| --- | --- |
| ARCH2 / App/DI/owned children | Library/package Editor и executable hosts реализованы; bounded source/fresh NuGet/headless/native proof есть. Не полный release/platform scope |
| ED1–ED3 / ED4c/d | Project picker/switch в stopped/idle, persistence/conflicts, entity operations, schema-driven inspector и общая Undo/Redo history реализованы |
| ED4f.3 / UIA2 | EditorMainWindow использует Window; standard/Cobalt и embedded/MS DI сохраняют своих owners |
| Editor typography / PA8c.3a/3b | System catalog/default Variable Text, styled custom text и theme retention реализованы и проверены в указанном scope; повторное внедрение не назначено |
| ED4g.1/2/3/4 | Compact typed editors, numeric gestures/linked Scale, group reset и rad/deg реализованы; final Editor1353/1353 D/R + native source/publish; дальше ED5a |
| ED5a.1–4 | Camera/selection v1 завершён в bounded Windows x64 scope: source/published/fresh-package GPU и supported Windows input,640×360/до64entities. Physical held-RMB/MMB/multi-monitor gates отдельно; ED5b v1 завершён09ноября |
| ED5b.0–3 | World axes/planes, inverse-parent local position, transient future-bounds outline, один commit/Undo либо cancel; clean inspector/actual receipt fences. Full Editor1465 D/R, source/published/fresh-package native; [границы](HISTORY.md#ed5b-translation-2026-11-09) |
| ED6b | Document-aware Build/Play/Replace, capture declarative/project/package inputs, process-local payload reuse и source watcher реализованы в ограниченных profiles |
| Observer ED6b.2e.4a–f | Directory cache/pins/native metadata и bounded4opens; scheduling allocations снижены в4f при прежнем witness/drain. [Scope](OBSERVER_COST.md) |
| ED6a / ED8a | Console/compiler diagnostics и report/child health реализованы в scoped consumers; aggregate metrics/extended acceptance отдельно |
| ARCH1c/d, ENG7b/ENG9, ED4h/ED7a | Live World edits, Player roles, agents и Game AI inspector не реализованы этим UI срезом |
| ARCH1e.1/2 | World/EntityHandle/value pools, scoped1/2type queries, exclusions, phase commands, sequential graph/access checks и App factories реализованы. World borrowed by scheduler; builtin transform/extraction добавлены в e.4, checkpoint ещё впереди |
| ARCH1e.3a/b | ComponentRegistry/SceneWorldLoader/Codec, initial Runtime capability/chunks и StartWorldAsync реализованы. Полный document, exact schema/refs/counts, atomic SceneWorldGameSession/owned cleanup; actual selected game в child, CPU consumers проверены |
| ARCH1e.4 | Explicit builtin bindings, Local/Parent/World cache, iterative hierarchy, parent-space velocity, ordered systems и detached primitive snapshots реализованы. PrimitiveGameSession готовит graph/frame0 до activation; source/published native pixels проверены |
| ARCH1f / ED4i | F.0/f.1 CPU API и [f.2 resource API](RENDER_RESOURCES.md) реализованы: counted catalog/scene/preparation leases, bounded device cache, atomic reload, cleanup retry и standalone Vulkan upload/readback. F.3 owned extraction/submission leases реализованы; f.4 surface renderer/Playing fence и ED4i inspector ещё нужны |
| ARCH1g | [Выбран AssimpNetter6.0.5 low-level](MODEL_IMPORT_RESEARCH.md#decision-2026-11-07),8research profiles прошли. G.1 production adapter/captured-I/O/limits/identity ещё нужны; current assets ещё не file importer |
| ARCH1h | Запланировано исследование audio module/backend, real-time commands/clip leases/device lifecycle и ECS source/listener roles; код и package boundaries ещё не выбраны |

## Последние датированные проверки

| Дата | Scope и результат | Evidence |
| --- | --- | --- |
| 2026-11-11, итог | CORE7.6: Aether3234 и Editor1496 full D/R, documented inputs; current16package230checks/profile и actual bootstrap child без renderer | [Core](../core/HISTORY.md#core76-2026-11-10) |
| 2026-11-10 | CORE7.5: Editor1496 D/R, Aether47scoped serial D/R,110newcases total; source/published/fresh16packages76checks/profile. Full Aether non-green указан отдельно | [Configuration](../core/HISTORY.md#core75-2026-11-10) |
| 2026-11-09 | ARCH1f.3:1376scoped D/R,78new; source/published/fresh7pkg CPU21checks/profile/owners0. Observer:Editor1468 D/R и frozen allocation gains. Full Aether non-green отделён | [Snapshots/observer](HISTORY.md#render-snapshots-2026-11-09) |
| 2026-11-09 | ED5b: full Editor1465/1465 каждый D/R,53newcases, failed/skipped0. Source/published/fresh15package native4profiles: по5PNG/4reaped children, owners0/validation0/0; solution Debug0errors/1556warnings | [Translation](HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-08 | ED5a.4: Editor1412/Aether2104 D/R; source/published/fresh-package GPU и supported Windows SendInput. Это предшествующая отдельная проверка | [Camera/selection](HISTORY.md#ed5a4-acceptance-2026-11-08) |
| 2026-11-07 | ARCH1f.2: targeted ECS/Rendering1230 D/R, resource CPU/Vulkan source/published/fresh7package, owners0/validation0/0. Это предшествующая проверка, не текущий full Aether | [Resources](HISTORY.md#render-resources-2026-11-07) |

Типографика/UIA3d, importer research и прежние полные записи сохранены в
[снимке предыдущего STATUS](HISTORY.md#status-before-ed5b-2026-11-09).
Исторические результаты не считаются повторёнными текущей configuration задачей.

## Открытая acceptance и ограничения

- Current CORE7.6 full Aether3234/3234 D/R прошёл последовательно с bounded
  test props override; ordinary nested legacy-tool restore не объявлен исправленным.
  Старые failures/replays сохранены в [CORE7.5](../core/HISTORY.md#core75-2026-11-10),
  current inputs/commands — [CORE7.6](../core/HISTORY.md#core76-2026-11-10).
- Full Aether06ноября имеет diagnostics Stop/Dispose и отдельный Delivery.App timeout;
  focused replays прошли, причина не установлена. Это открытый reliability result.
- Post-reap drain timeout31октября не закрыт replays; нужен причинный evidence
  [ARCH2e.4b](PLAN.md#arch2e4b-drain-diagnostics).
- Numeric physical arrows/modifiers, held-button Esc, translation physical input и
  multi-monitor DPI не подтверждены; directed UI/native GPU не подменяет эти проверки.
- Primitive Edit Preview640×360/до64entities и stopped/idle project switch сохраняют
  границы. Mesh renderer/live World/runtime editing, scene migration и disk cache отдельно.
- Physical K3/K7/K8/VRAM/device loss и общая release matrix открыты; cooperative stop
  не прерывает произвольный зависший user Task/driver.

## Границы приложения и engine

Authoring/history/schemas/preview/session/build — Aether; views/styles/inspector/tools —
Editor; общие controls/input — Argentis/Alloy. Детальный backlog в одном плане владельца.
Поведение конкретной игры/мира принадлежит TrueMoon.Tales/Lore.

## Следующий конкретный шаг

Engine: [ARCH1f.4](PLAN.md#arch1f-rendering), actual Vulkan mesh/material surface
consumer и required-view Runtime Playing fence поверх готовых f.2/f.3 owners.
Production importer g.1/captured-I/O и h.0 audio — отдельные направления; Nom g.2
следует engine integration. Primitive renderer не мигрирован автоматически.

Editor: ED5b v1 и observer4f закрыты в своих scopes. Local-oriented handles/snapping
либо rotation/scale требуют отдельного выбора. Reliability: изолировать native game
fixtures от repo legacy CLI-tool restore в ordinary запуске; current full baseline
на bounded test inputs уже записан в CORE7.6. Прежний
post-reap drain timeout требует собственного причинного evidence. Physical/release отдельно.
