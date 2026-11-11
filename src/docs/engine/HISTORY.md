# Engine / Editor: история решений и проверок

<a id="core85-2026-11-11"></a>
## CORE8.5: согласованная поставка — 2026-11-11

Полные suites D/R: Aether 3243, Editor 1496; Failed 0, Skipped 0.
Source/published Runtime examples и 8 independent package game cases (оба DI, DLL/apphost, build/publish) проверили compile/load/replacement/stop/reap; два fresh GPU viewport profiles прошли. F.4 mesh surface renderer/Playing fence, g.1 importer, h.0 audio и прежние reliability gates остаются отдельными. Proposed game заявка не принималась.
Общая поставка содержит 16 packages одной версии 0.3.0.1-alpha.core85.20261111221352.
Runtime/API/dependencies сохранены; изменения CORE8.5 относятся к runners/docs.
[Подробные команды/матрица/границы](../core/HISTORY.md#core85-delivery-2026-11-11).

<a id="engine-status-before-core85-2026-11-11"></a>
## STATUS до CORE8.5 — архив 2026-11-11

# TrueMoon.Aether: состояние движка и редактора

**2026-11-11 — CORE8.4: полная регрессия на .NET 11 / runtime-async=on прошла.**
Aether — 3243 теста, Editor — 1496 в каждой конфигурации Debug/Release; **Failed 0, Skipped 0**.
Выполнены явное обнаружение тестов и обычные restore/build без test-only overrides.
Production runtime, API и зависимости в CORE8.4 не менялись. Исправление четырёх
manual fixture templates и границы повторных проверок описаны в Core HISTORY.
[Команды и результаты](../core/HISTORY.md#core84-runtime-2026-11-11), [план](../core/PLAN.md#core8-dotnet11).

Сохранены frozen profile из CORE8.3, TargetFrameworkAttribute, Features witness, schema 1 net11 manifest/lock и явная пересборка старых artifacts. Текущие source/published CPU/GPU viewport/Editor пробы проверили lifetime и пиксели. Исправлены четыре manual fixture writer: generated game projects теперь явно ссылаются на TrueMoon.Contracts через прежний TrueMoonContractsPath. После исправления повторены NativeRuntimeProcessTests: 4/4 в каждой конфигурации, и все 24 native сценария. F.4 mesh surface renderer/Playing fence, g.1 importer, h.0 audio и прежние physical/reliability/release gates остаются отдельными. Proposed game заявка не принималась.

Общая точка продолжения — **CORE8.5**: свежая согласованная поставка и итоговая документация.
[Предыдущий STATUS целиком](HISTORY.md#engine-status-before-core84-2026-11-11).

<a id="core84-2026-11-11"></a>
## CORE8.4: регрессия .NET 11 / runtime-async — 2026-11-11

Aether — 3243 теста, Editor — 1496 в каждой конфигурации Debug/Release; Failed 0, Skipped 0.
Тесты обнаружены явно; исполнение включает обычные restore/build без test-only overrides.
Production runtime, API и зависимости не менялись; четыре manual fixture template
получили явную ссылку на Contracts. Полные suites выполнены до этого исправления;
после него повторены затронутые NativeRuntimeProcessTests (4/4 D/R) и 24 native сценария.

Сохранены frozen profile из CORE8.3, TargetFrameworkAttribute, Features witness, schema 1 net11 manifest/lock и явная пересборка старых artifacts. Текущие source/published CPU/GPU viewport/Editor пробы проверили lifetime и пиксели. Исправлены четыре manual fixture writer: generated game projects теперь явно ссылаются на TrueMoon.Contracts через прежний TrueMoonContractsPath. После исправления повторены NativeRuntimeProcessTests: 4/4 в каждой конфигурации, и все 24 native сценария. F.4 mesh surface renderer/Playing fence, g.1 importer, h.0 audio и прежние physical/reliability/release gates остаются отдельными. Proposed game заявка не принималась.

[Подробная запись с командами, матрицей и границами](../core/HISTORY.md#core84-runtime-2026-11-11).

<a id="engine-status-before-core84-2026-11-11"></a>
## STATUS до CORE8.4 — архив 2026-11-11

# TrueMoon.Aether: состояние движка и редактора

**2026-11-11 — CORE8.3 завершён: frozen game build и actual Runtime на .NET11.**
[План](../core/PLAN.md#core8-dotnet11), [команды и границы](../core/HISTORY.md#core83-frozen-2026-11-11).
Full Aether3243/3243 и Editor1496/1496 каждый D/R, failed/skipped0, ordinary restore
без test-only props override. Target/SDK/Features/lock/manifest согласованы; SDK10
selector blocker устранён. Старые artifacts требуют явной пересборки, schema1
сохраняется. Capture/source witness/cache verification/ownership не ослаблены.

Source Debug и published Release: actual frozen repository game compile/reuse/Open
на root с только SDK11.0.100/runtime11.0.0; independent Runtime Step/checkpoint/
Stop/reap,2children, parent game не загружает, exact child artifact и CoreLib проверены.
Это headless/CPU/control proof; новых surface/GPU/physical/release proofs нет.
Shell/PID assumptions исправлены в fixtures; исходные failed trials сохранены.
[Прежний STATUS целиком](HISTORY.md#engine-status-before-core83-2026-11-11).

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
| 2026-11-11, CORE8.3 |Full Aether3243/Editor1496 D/R, source/published SDK11-only game compile/reuse/load/Stop/reap,2children | [Core evidence](../core/HISTORY.md#core83-frozen-2026-11-11) |
| 2026-11-11, предыдущий CORE7.6 | CORE7.6: Aether3234 и Editor1496 full D/R, documented inputs; current16package230checks/profile и actual bootstrap child без renderer | [Core](../core/HISTORY.md#core76-2026-11-10) |
| 2026-11-10 | CORE7.5: Editor1496 D/R, Aether47scoped serial D/R,110newcases total; source/published/fresh16packages76checks/profile. Full Aether non-green указан отдельно | [Configuration](../core/HISTORY.md#core75-2026-11-10) |
| 2026-11-09 | ARCH1f.3:1376scoped D/R,78new; source/published/fresh7pkg CPU21checks/profile/owners0. Observer:Editor1468 D/R и frozen allocation gains. Full Aether non-green отделён | [Snapshots/observer](HISTORY.md#render-snapshots-2026-11-09) |
| 2026-11-09 | ED5b: full Editor1465/1465 каждый D/R,53newcases, failed/skipped0. Source/published/fresh15package native4profiles: по5PNG/4reaped children, owners0/validation0/0; solution Debug0errors/1556warnings | [Translation](HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-08 | ED5a.4: Editor1412/Aether2104 D/R; source/published/fresh-package GPU и supported Windows SendInput. Это предшествующая отдельная проверка | [Camera/selection](HISTORY.md#ed5a4-acceptance-2026-11-08) |
| 2026-11-07 | ARCH1f.2: targeted ECS/Rendering1230 D/R, resource CPU/Vulkan source/published/fresh7package, owners0/validation0/0. Это предшествующая проверка, не текущий full Aether | [Resources](HISTORY.md#render-resources-2026-11-07) |

Типографика/UIA3d, importer research и прежние полные записи сохранены в
[снимке предыдущего STATUS](HISTORY.md#status-before-ed5b-2026-11-09).
Исторические native/GPU/performance результаты не считаются повторёнными CORE8.3.

## Открытая acceptance и ограничения

- Current CORE8.3 full Aether3243/Editor1496 D/R прошёл с обычным nested restore,
  без test props override. Предыдущие CORE7.5/7.6 failures/inputs сохранены в истории;
  старые diagnostics/drain причины не объявляются исправленными этим passing run.
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

Migration: [CORE8.4/5](../core/PLAN.md#core8-dotnet11), остальные suites/runtime async и final fresh-package/native/delivery. CORE8.3 SDK11 frozen game profile и actual source/published runtime proof завершены. Затем feature очередь ниже.

Engine: [ARCH1f.4](PLAN.md#arch1f-rendering), actual Vulkan mesh/material surface
consumer и required-view Runtime Playing fence поверх готовых f.2/f.3 owners.
Production importer g.1/captured-I/O и h.0 audio — отдельные направления; Nom g.2
следует engine integration. Primitive renderer не мигрирован автоматически.

Editor: ED5b v1 и observer4f закрыты в своих scopes. Local-oriented handles/snapping
либо rotation/scale требуют отдельного выбора. Reliability: current native game
fixtures/restore сохраняют обычные inputs в CORE8.3; current full net11 baseline
и его точные границы записаны в Core HISTORY. Прежний
post-reap drain timeout требует собственного причинного evidence. Physical/release отдельно.

<a id="core83-2026-11-11"></a>
## CORE8.3: frozen game profile .NET11 — 2026-11-11

Внутренний GameBuildProfile использует стандартный module TargetFrameworkAttribute,
согласуя SDK selection/MSBuild/compiler/evaluation/lock/manifest. Editor preflight
требует stableSDK11. Features участвуют в frozen witness/csc/fingerprint;
runtime-async=on сохранён из root props. Manifest schema1, lock schema1/net11.0,
managed lib admission добавляетnet11 и сохраняет прежние compatible targets.
Старые game artifacts отклоняются с rebuild diagnostic; auto-migration нет.
Capture/source identities, verified process-local reuse и stop/drain/reap сохранены.

Полные Aether3243/3243 и Editor1496/1496 каждый Debug/Release, Failed0/Skipped0,
обычный restore/build без прежних props overrides.9new targeted cases, focused21.
Source Debug/published Release: actual frozen repository game на root с единственным
SDK11.0.100/runtime11.0.0 → verified reuse/Open → independent Runtime load/Step1/
checkpoint1/Stop/reap,2children. Parent game не грузит, child получает exact artifact;
UI/GPU не активируются этим proof. Отдельно исправлены helper shell и тестовое
предположение об уникальности PID после завершения child: valid SessionIdentity,
точные3/8leases и каждый cleanup проверены. Первый failed/aborted trial сохранён.

[Единственная подробная запись с командами, scopes и retention](../core/HISTORY.md#core83-frozen-2026-11-11).
Root props/package/dependency versions и public API не менялись. Historical drain/
diagnostics causes, native/physical/release/performance gates не закрыты passing run.

<a id="engine-status-before-core83-2026-11-11"></a>
## Engine STATUS до CORE8.3 — архив 2026-11-11

# TrueMoon.Aether: состояние движка и редактора

**2026-11-11 — CORE8.0: SDK11 source compilation проходит, actual repository-game build заблокирован selector.**
Общий solution Debug/Release0errors/1556warnings, Aether/Editor и test projects
скомпилированы на net11.0/runtime-async=on. Выбранный RepositoryGameProject test
выполнен1/1failed: Unsupported frozen SDK selection policy, SelectSdk line548
отклоняет major11 до inner restore. [Команды/TRX/binlogs](../core/HISTORY.md#core80-baseline-2026-11-11).
Исправлений production/fixtures нет; full Aether/Editor tests, game load/child,
native/GPU/package acceptance не запускались.

Первый migration blocker подтверждён, последующие frozen snapshot/manifest/lock
policies ещё проверяются после него. Root legacy dotnet-xunit в current SDK11
ordinary restore не вызвал failure; historical nested issue не объявлен исправленным.
Единственный подробный план — [CORE8.3](../core/PLAN.md#core8-dotnet11), после общей
сверки manual consumers8.1. Затем engine f.4 и прежняя feature очередь ниже.

ARCH1f.3, ED5b, observer4f и CORE7 configuration/delivery сохраняют свои net10
proofs с прежними датами. [Последний CORE7.6 scope](../core/HISTORY.md#core76-2026-11-10),
[предыдущий STATUS](HISTORY.md#engine-status-before-core80-2026-11-11).
Новых physical/device-loss/VRAM/reliability/release proofs нет.

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

Migration: [CORE8.3](../core/PLAN.md#core8-dotnet11), SDK11 frozen game profile и fixtures после общих CORE8.1 inputs; фактический selector failure подтверждён в CORE8.0. Затем feature очередь ниже.

Engine: [ARCH1f.4](PLAN.md#arch1f-rendering), actual Vulkan mesh/material surface
consumer и required-view Runtime Playing fence поверх готовых f.2/f.3 owners.
Production importer g.1/captured-I/O и h.0 audio — отдельные направления; Nom g.2
следует engine integration. Primitive renderer не мигрирован автоматически.

Editor: ED5b v1 и observer4f закрыты в своих scopes. Local-oriented handles/snapping
либо rotation/scale требуют отдельного выбора. Reliability: изолировать native game
fixtures от repo legacy CLI-tool restore в ordinary запуске; current full baseline
на bounded test inputs уже записан в CORE7.6. Прежний
post-reap drain timeout требует собственного причинного evidence. Physical/release отдельно.

<a id="core80-sdk-baseline-2026-11-11"></a>
## CORE8.0: воспроизведение game build blocker на SDK11 — 2026-11-11

Общий ordinary source build Debug/Release прошёл0errors/1556warnings каждого
profile. Единственный выбранный RepositoryGameProject_FreezesAncestorPropsAndContractReferences
упал1/1: Unsupported frozen SDK selection policy, GameBuildPreparation.SelectSdk
line548 отклоняет SDK major11 до запуска внутреннего restore. Code/fixtures не
правились, full Aether/Editor suites и runtime/native acceptance не повторялись.
Детальные inputs/команды/TRX/binlogs и границы — [CORE8.0](../core/HISTORY.md#core80-baseline-2026-11-11).
Единственный migration roadmap — [CORE8.3](../core/PLAN.md#core8-dotnet11);
он сохраняет frozen input/manifest/capture/process contracts. Новых engine APIs нет.

<a id="engine-status-before-core80-2026-11-11"></a>
## Engine STATUS до CORE8.0 — архив2026-11-11

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

<a id="core76-2026-11-10"></a>
## CORE7.6: текущая итоговая сверка — 2026-11-10

[Единственная подробная запись и команды](../core/HISTORY.md#core76-2026-11-10),
[guide](../core/CONFIGURATION_GUIDE.md).
7suites6945/6945 каждый D/R; module-specific counts и exclusions указаны в текущем
STATUS. Source solution0errors в D/R;16fresh packages одной версии,5programs×3forms,
по230checks/4owned children на source/published/fresh profile, оба DI.
Window/GPU/game/native runtime не запускаются в новых configuration consumers.
Aether full3234/3234 D/R выполнен последовательно с test-only props copy; ordinary
nested restore и прежние diagnostics reliability причины не исправлены этим proof.
Current managed public API8assemblies unchanged; source C#/csproj1673/1673 сохранены.
Дальнейшая очередь модуля и hardware/native/release gates остаются самостоятельными.


<a id="core75-2026-11-10"></a>
## CORE7.5: конфигурация Aether/Editor — 2026-11-10

[Единственная детальная запись/команды](../core/HISTORY.md#core75-2026-11-10),
[ключи и границы](../core/CONFIGURATION_MODULES.md).
Aether/Editor/ECS/render/authoring schemas, frozen child bootstrap/flags и общий
Editor CLI реализованы с существующими DI/factory owners. Editor1496/1496 D/R;
Aether42new +5old replay cases =47/47 D/R последовательно. Latest extra full Debug
3232/3234 (diagnostics timeout и SDK exclusive lock), full Release3233/3234
(SDK lock); initial Debug3232/3234 native fixture restore и final Debug3233/3234
NU1008 записаны в Core history. Полная suite не green, replay не скрывает failures.
Source/published/fresh16packages76checks/profile на обоих DI без window/GPU/child
runtime; existing compiled-game2scenario fixtures также прошли в serial source
replay. Нового native UI/GPU acceptance нет. f.4/ED/physical/release очередь сохранена.


<a id="render-snapshots-2026-11-09"></a>
## ARCH1f.3 и ED6b.2e.4f: owned snapshots и observer allocations — 2026-11-09

Запрос после выбора пары: «давай так поступим». Основной агент реализовал
[ARCH1f.3](PLAN.md#arch1f-rendering), второй — [ED6b.2e.4f](EDITOR_PLAN.md#ed6b2e4f-observer-cost).
Engine/общие документы свёл координатор. Предшествующие ED5b, COB5a.1, SplitView,
COB6e, пользовательская версия0.2.5.2-alpha и src/.codex сохранены; commits/push не делались.

### Контракт, код и границы f.3

[RENDER_SNAPSHOTS](RENDER_SNAPSHOTS.md) задаёт один owned view поверх f.1 content/f.2
resources. UseAetherRenderResources регистрирует non-disposable typed factory
IFactory<RenderSnapshotView,RenderSnapshotViewRequest>. Host отслеживает продукты
до успешного release, освобождает views перед bindings/devices/catalog и при failed
view cleanup не force-dispose dependencies. Borrowed World не освобождается view.

CaptureAsync вызывается владельцем на serialized completed-Transform boundary;
ожидание binding gate предшествует синхронному World обходу. Async IEcsSystem/
DI activation не вводились. Explicit entity или external rigid camera, current-tick
WorldTransform, full affine bounds по8углам, disabled/masked validation, exact
mesh/binding/material revisions, stable entity/slot order и linear instance tint.
Двухпроходная extraction проверяет размер до allocation; массив draw values immutable,
без managed refs/World pointers. Snapshot stamps: view/session/world/project/build/
catalog/scene/source revision/tick/publication sequence/binding revision/device/
camera revision/surface generation. Source revision не объявлена автоматическим
счётчиком произвольных World mutations: caller сериализует World/scheduler/reload.

Internal ready-resource retain под device/binding gates удерживает canonical CPU
scope и mesh entries независимо от ready binding. Reload/delete/coalescing не
освобождают ресурсы старого accepted use. Current/pending разделяют allocation,
recording переносится в ledger до factory/native work; aliases backend owners
отвергаются по reference identity. Backend ExecuteAsync/IsComplete/DisposeAsync/
IsReleased обязаны подтверждать actual completion/safe release. Unknown outcome
закрывает view и device preparation/allocation admission, сохраняя исходную ошибку
и retained references до безопасного cleanup. Failed mesh/submission cleanup
сохраняет CPU lease, payload и byte reservations для retry. Stop wait cancellation
не является actual drain. Capture wait за reload отменяется собственным view stop.

Ceilings:10000logical entities/4096renderers и4096draw ranges;2MiB payload,16MiB
retained buffers,7envelopes,3accepted submissions. Payload=256accounted header+
exact array length×Unsafe.SizeOf<RenderDrawRange> (448bytes/element на этом runtime).
Это buffer/metadata admission, не общая RAM/VRAM quota.0×0 suspend освобождает
current/pending и fences stale recording, accepted uses остаются owned. Camera/
extent changes требуют monotonic revisions; paused same-tick capture допустим
при новом view/resource binding. Нет truncate/implicit latest/fallback material.

Primitive renderer, Runtime/control/frame/checkpoint formats и production game
contracts не менялись. Реальный mesh surface backend/pixels/required-view Runtime
Playing fence — ARCH1f.4. F.3 backend use proof ниже deterministic memory, не новая
GPU submission/device-loss acceptance. Public API/readme и namespaces актуализированы.

### Тесты и assertion review

Windows x64 / SDK10.0.403 / net10.0 / xUnit/VSTest. Финальный scoped
**1376/1376 каждый Debug/Release, failed0/skipped0, exit0**: ECS, Rendering.Resources,
Rendering.Snapshots, Rendering.Render* и RuntimeBootstrapTests. Включены78новых
snapshot cases; runtime bootstrap negative cases исправлены без изменения production
versions. Последний nonincremental solution Debug build:0errors/1556warnings.
В новых snapshot sources/tests warnings не найдено; старые generated/Diagnostics
warnings не подавлялись. Full-solution tests не запускались.

| Требование | Прямое evidence |
| --- | --- |
| Explicit camera, stamps, affine shear, tint, unmanaged detached draws | SnapshotExtractionTests.ExplicitCamera_DetachesOrderedExactRevisionsAffineBoundsAndLinearTint (entity/external); InvalidHiddenRenderer_IsRejectedBeforeVisibilityAndPreservesCurrent (10partitions); ExplicitCamera_InvalidPartitionsNeverSelectAnotherCamera (11partitions) |
| Masks/empty/ordering и budgets | EnabledAndMasks_SelectDrawsWithoutChangingWorld; RangeLimit_BeforeAtAndAfterBoundaryNeverTruncates; PayloadLimit_ExactElementBytesBoundaryIsCheckedBeforePublication; SnapshotBoundaryTests.RetainedBudgetCountsOldAndCandidateUntilAtomicReplacement; OptionsAreCopiedAndHardDrawRangeLimitCountsAllSlotsRatherThanEntities (4096ranges/8slots); LogicalEntityCeilingIsInclusiveAndRejectsTheNextWithoutLosingCurrent (10000/10001) |
| Current/pending/recording и old revisions | SnapshotOwnershipTests.MailboxCoalescesAndRecordingTransferSharesOneEnvelopeUntilItsLastRoleReleases; ReloadKeepsOldRevisionAndHandleAliveThroughSubmittedUseUntilActualCompletion |
| Bounded admission/ownership/cancel | SubmissionCapacity_RetainsAcceptedUsesAndRejectsTheNextBeforeFactory (limits1/2/3); AliasedBackendOwnerIsRejectedWithoutExecutingOrDisposingAnotherAcceptedUse; ForeignRecordingAndForeignBindingAreRejectedWithoutTransferringTheirOwners; CancellationBeforeAdmissionKeepsTheReservationAndNeverInvokesBackendFactory; AcceptedCancellationIsCooperativeAndRetainsUseUntilActualCompletion; CancelledStopWaitLeavesActualInFlightOwnershipUntilBackendCompletes |
| Unknown/partial/errors/retry | MissingCompletionEvidenceFencesTheViewAndCleanupFailureRetainsOwnerForRetry (также closed device/budget); PartialFactoryFailureIsTrackedBeforeInvocationAndRetainsItsCleanupOwner; BackendExecuteFailurePreservesOriginalOutcomeAndRequiresSafeCleanup; SnapshotLifecycleTests.LastMeshCleanupFailureRetainsSnapshotPayloadCpuLeaseAndAllocationUntilRetry |
| Stop/configuration/App | CameraGenerationAndSuspendInvalidateUnsubmittedRecordingAndAllowPausedResume; SnapshotLifecycleTests.SuspensionAndEarlyDeviceStopCannotFreeResourcesStillUsedByAcceptedSubmission; HostStopRetainsDependenciesWhenSubmissionCleanupFailsThenRetriesInOwnershipOrder; StopCancelsCaptureWaitingBehindResourceReloadWithoutWaitingForUnrelatedUpload; AppRegistersOneLazyNonOwningViewFactoryAndPreservesProviderLifecycle (Cobalt/MS DI) |

Pseudo-mutation/assertion review: world diagonal-only/decomposition ловится ожидаемым
shear AABB ±1.25 и exact matrix; wrong tint space — независимыми .10702/.21404 linear
vectors; partial/truncated publication — exact count/stamp/old object identity;
lease release on reload/timeout — live handle, upload dispose counts, CPU/budget
counters до actual completion; alias double dispose — один backend cleanup;
unknown outcome without device fence — explicit closed preparation/budget errors;
lost failed-cleanup owner — payload/lease/geometry сохранены до retry. Это статический
review mutations, не executed mutation score/coverage. No Skip/Ignore additions.
Roslyn parse-only pairing до tests:11sources/3existing tests,7unpaired/4paired;
IDE findTests unavailable. Это identifier map, не line/branch coverage. Research/
plan/status/pairing находятся в ignored TestResults/RenderSnapshots.

```powershell
$env:APPDATA = (Resolve-Path TestResults/RenderSnapshots/offline-settings).Path
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~Rendering.Snapshots|FullyQualifiedName~Rendering.Resources|FullyQualifiedName~Ecs|FullyQualifiedName~Rendering.Render|FullyQualifiedName~RuntimeBootstrapTests' --logger 'trx;LogFileName=render-scope-Debug-complete.trx' --results-directory TestResults/RenderSnapshots '-bl:TestResults/RenderSnapshots/render-scope-Debug-complete-{}.binlog' -v quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Rendering.Snapshots|FullyQualifiedName~Rendering.Resources|FullyQualifiedName~Ecs|FullyQualifiedName~Rendering.Render|FullyQualifiedName~RuntimeBootstrapTests' --logger 'trx;LogFileName=render-scope-Release-complete.trx' --results-directory TestResults/RenderSnapshots '-bl:TestResults/RenderSnapshots/render-scope-Release-complete-{}.binlog' -v quiet
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/RenderSnapshots/solution-Debug-complete-{}.binlog' -v quiet
./ManualTests/RenderSnapshots/Verify.ps1 -EvidenceDirectory TestResults/RenderSnapshots/delivery-complete
```

### Поставка и фактические non-green результаты

Independent source Debug/published Release/fresh7package CPU consumer: по21checks,
explicit camera/shear/tint, coalescing/recording, pre-factory ledger, old resources
через reload до completion, suspend/stale recording/paused resume, detached inspection
и final snapshot bytes/resource leases/geometry bytes0. App-owned factory/profile,
не ProjectReference-only substitute for package proof. Fresh graph без framework
ProjectReferences, private feed/cache, exact third-party cached archives, installed/
output Aether DLL hashes совпали. Source hashes проверены после сборок/pack. Source
consumer использует предварительно проверенные Debug/Release framework outputs,
restore --no-dependencies и BuildProjectReferences=false; fresh package restore полный.
Версия и archive SHA final delivery подставлены ниже из фактического manifest.

Первые source restore попытки не green: legacy dotnet-xunit из repo CPM попал в P2P
restore; изолированы consumer Directory.Build/Packages и source no-dependencies.
Следующий consumer build выявил missing using TrueMoon.Dependencies; исправлен до
финальных21checks. Первые unit builds выявили test namespace lookup Ecs.Rendering
в Tests и missing UseDI using; test imports исправлены. Первый scoped1273 run имел
GameBuildFixture AccessDenied user NuGet.Config; task-local APPDATA дал1273/1273.
Последующие safety review добавили backend alias guard, view-stop linked capture,
device-wide unknown-generation fence и retained last-mesh cleanup regression;
финальные1376 D/R и fresh consumer выполнены после production fixes.

Full Aether Release attempt **3187/3191**,4failed/0skipped за9m11s: два bootstrap
negative tests задавали **ныне поддержанные Control4/Frame2** как invalid. Они теперь
используют current+1; positive round-trip и102bootstrap cases проходят в scoped D/R.
Ещё два NativeRuntimeProcessTests.CompiledGameFixtures scenarios build-artifacts/
replacement падают **до нового snapshot API** на legacy dotnet-xunit/SDK restore:
NU1100/NU1301 и NuGet path-null при закрытой сети. Current replay с offline task-local
settings:1375passed/2native failed (1377total). Это не полный green и не доказательство
исправления прежних diagnostics/drain timeouts. Native fixture isolation/SDK restore
нуждается в отдельной работе; production compiler/protocol этой задачей не менялись.
Текущий final scoped run охватывает только перечисленные ECS/rendering/bootstrap
suites; native class в него не входит. Full run не собран арифметически из passing replays. Полный Aether Debug/
Release после этих testdata fixes не повторялся; full solution tests не заявляются.

### Параллельный observer

Второй агент изменил только EditorSourceOpenBatch.cs, его существующий test class
и [OBSERVER_COST](OBSERVER_COST.md). Подробный task — один в EDITOR_PLAN. Static state,
nongeneric Task workers/inline4Task buffer сохранили scheduler/context/ordering,
concurrency≤4, полный content/membership/probe witness и actual native file drain.
Full Editor **1468/1468 каждый D/R**, focused199Release;3new regressions context flow,
fast jobs/single Take и reentrant completion/cleanup. Exact test names/commands/SHA
в OBSERVER_COST, новых dependency/version/native graphics paths нет.

Final4fresh processes: allocations/cycle Polling9060296→8746616B (−3.46%),
Notifications9065256→8746624B (−3.51%); median scan upper1148→1162ms и1154→1144ms.
Whole-process CPU1562.5→1390.625ms и1484.375→1453.125ms — exploratory3samples,
общий speedup не заявлен. Synthetic no-native-IO counter1327.91→871.36B/batch (−34.4%),
отдельный attribution, не timing.1из96frozen output files отличается (Editor DLL),
normalized fixture witnesses совпали;16Same/4same-length+mtime Changed, budget0,
watchers0/Stopped. Power plan unchanged; measurement windows без parent heavy jobs.
Rejected ThreadPool/TCS experiments сохранены с худшими latency/CPU, не подменяют final.

Raw evidence: ignored TestResults/RenderSnapshots/*-complete.trx/log/binlog и
delivery-complete/verification.json+3profile JSON; TestResults/ObserverCost/paired-final/
summary.json и матрицы rejected candidates. Локальное хранение до ручной очистки;
versioned запись и OBSERVER_COST сохраняют scope/команды/outcomes/limits без raw файлов.
Следующий engine шаг — f.4 actual Vulkan surface/Playing fence. Импорт g.1, Nom bridge
g.2 и physical/device-loss/release gates не закрыты этими CPU/file-workload proofs.

Final delivery: **0.2.5.2-rendersnapshots.20261109214531**; TrueMoon.Aether archive SHA256 **1EF491CE556093CB9FD41F0E53D858D7317FE199F4287295F0200DB8BA6978AC**;7packages/265source guards. Evidence run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-RenderSnapshotsConsumer-983851d7be6b40e4a8f573176969528a`.


<a id="status-before-snapshots-observer-2026-11-09"></a>
## Статус перед snapshots/observer — снимок 2026-11-09

**2026-11-09 — ED5b translation v1 завершён в bounded Windows x64 scope.**
World XYZ/XY/XZ/YZ с записью local position через inverse parent; при drag виден
будущий контур, release создаёт один общий commit/Undo, Esc отменяет. Черновики
инспектора, source/selection/camera/layout и capture cleanup проверены.
Full Editor1465/1465 каждый Debug/Release, failed0/skipped0; solution nonincremental
Debug0errors/1556warnings. Source Debug/published Release и независимые fresh-package
Debug/Release:5translation PNG,4children=reaped каждый, final UI/GPU/handles/mappings0,
validation0errors/0warnings. [Контракт](EDITOR_TRANSLATION.md),
[команды, тесты, failed attempts и limits](HISTORY.md#ed5b-translation-2026-11-09).

Engine/Aether source и protocol этой задачей не менялись; full Aether tests не повторены.
Предыдущие ED5a.4 camera/selection proofs от08ноября сохраняют свой scope, включая
Windows input; новый physical translation input не проверялся. Local-oriented handles,
snapping, rotation/scale и live World editing — отдельные задачи.

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

| Дата | Scope и результат | Evidence |
| --- | --- | --- |
| 2026-11-09 | ED5b: full Editor1465/1465 каждый D/R,53newcases, failed/skipped0. Source/published/fresh15package native4profiles: по5PNG/4reaped children, owners0/validation0/0; solution Debug0errors/1556warnings | [Translation](HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-08 | ED5a.4: Editor1412/Aether2104 D/R; source/published/fresh-package GPU и supported Windows SendInput. Это предшествующая отдельная проверка | [Camera/selection](HISTORY.md#ed5a4-acceptance-2026-11-08) |
| 2026-11-07 | ARCH1f.2: targeted ECS/Rendering1230 D/R, resource CPU/Vulkan source/published/fresh7package, owners0/validation0/0. Это предшествующая проверка, не текущий full Aether | [Resources](HISTORY.md#render-resources-2026-11-07) |

Типографика/UIA3d, importer research и прежние полные записи сохранены в
[снимке предыдущего STATUS](HISTORY.md#status-before-ed5b-2026-11-09).
Исторические результаты не считаются повторёнными ED5b.

## Открытая acceptance и ограничения

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

Engine: [ARCH1f.3](PLAN.md#arch1f-rendering), owned camera/mesh/material snapshots и
leases до submitted completion; затем f.4 surface renderer/Runtime Playing fence.
Production importer g.1 на AssimpNetter low-level/captured-I/O и исследование h.0
audio остаются самостоятельными направлениями; Nom g.2 следует engine integration.

Прикладной Editor: ED5b v1 закрыт; перед расширением translation выбрать отдельный
scope local-oriented handles/snapping либо rotation/scale. Независимая измеримая
задача — [ED6b.2e.4f observer cost](EDITOR_PLAN.md#ed6b2e4f-observer-cost).
Physical input/reliability/release gates можно вести отдельно.



<a id="ed5b-translation-2026-11-09"></a>
## ED5b: translation v1, два параллельных владельца — 2026-11-09

Запрос: «Начни ED5b, а второму агенту передай задачу COB5a». Основной агент вёл
[ED5b](EDITOR_PLAN.md#ed5b-translation), отдельный агент — только runtime Cobalt и
его PLAN/STATUS/HISTORY; [самостоятельный COB5a.1 proof](../cobalt/HISTORY.md#cob5a1-production-2026-11-09).
Предшествующие незакоммиченные SplitView/COB6e и пользовательская общая версия
0.2.5.2-alpha сохранены. Production Aether/protocol/dependencies не менялись.

### Контракт и реализация

[EDITOR_TRANSLATION](EDITOR_TRANSLATION.md) фиксирует bounded first profile:
один выбранный видимый primitive cube/Transform v1, world X/Y/Z и XY/XZ/YZ.
Записывается local position через inverse parent linear matrix, включая rotated/
nonuniform parent и shear без декомпозиции. Local-oriented handles, snapping,
rotation/scale tools, multiple selection и live World mutation в этот профиль не входят.

TranslationGeometry/Drag/Target — detached CPU values в смысловой папке Translation.
Проекция берётся из actual-presented receipt; оси около72logical px, hit tolerance6,
вырожденные оси/плоскости скрыты. Ray/axis и ray/plane constraints используют frozen
view, finite/parallel/depth checks; no-op сохраняет исходные double values.

Существующий App/IFactory-owned EditorViewportController рисует Theme.Focus bounds
и Theme.Modified future-bounds выбранного объекта/видимых descendants. Исходный solid
frame остаётся до commit. Overlay не выдаётся за новый runtime frame/receipt, не
подменяет picking source и не создаёт GPU resources на move. Подсказка размещена
снизу image; custom labels передают FontWeight/FontItalic. Снимок финального native
outline осмотрен: контур/оси и подсказка видны, панели инспектора сохраняются.

Admission требует clean inspector: любой draft (включая валидный неприменённый),
conflict, pending reset/numeric gesture запрещает drag. Capture/state очищаются
перед внешними release callbacks, затем повторяются document/opening/selection
intent/camera/content/surface/layout/viewport fences. Changed-only x/y/z поступают
одним ComponentEditBatch через общий EditorDocumentCommands/Undo; rotation/scale,
inheritance остальных свойств и чужие overrides сохранены. Exact no-op не очищает Redo.
Esc/focus/capture/popup/hide/disable/detach/dispose/resize/DPI/selection/source/camera/
Stop отменяют только transient outline. Invalid move сохраняет последний кандидат;
invalid release отменяет его. Same semantic view/new frame разрешён; новая camera
не откатывается. Cleanup освобождает capture даже при throwing invalidation/release.

### Тесты и review assertions

Windows x64, SDK10.0.403/net10.0, xUnit/VSTest. Финальный full Editor:
**1465/1465 каждый Debug и Release, failed0/skipped0, exit0**. Это53новых cases
относительно1412, включая CLI translation profile. Никаких Skip/Ignore не добавлено.

| Требование | Прямое evidence в новых тестах |
| --- | --- |
| Все3оси и3плоскости | TranslationDragTests.AxisMove_MatchesKnownWorldDeltaAndPreservesOtherAxes и PlaneMove_MatchesKnownWorldDeltaAndPreservesNormal: известные world deltas, сохранённые остальные координаты/normal, exact zero |
| Parent/local и inheritance | TranslationTargetTests.InverseParent_RotatedNonuniformScaleIgnoresTranslationAndChildRotationScale; RootDelta_ChangesOnlyXAndNoOpPreservesExactDoubleValuesAndInheritance: численные ожидаемые local значения, изменённые property IDs, сохранённые pitch/scale, exact doubles |
| Один commit/Undo/Redo | EditorViewportTranslationTests.Drag_UsesOutlineWithoutMutatingDocumentThenReleaseCommitsOnePositionEditWithUndoRedo: exact original document/history на move, candidate outline, one revision/history change, Undo/Redo values и retained element identity |
| Cancel/stale/reentrancy | Cancellation_DropsCandidateWithoutChangingHistoryOrRestoringAnOldCamera (18reasons); NewFrameOfSameView_DoesNotInvalidateFrozenDragButSourceChangeDoes; ReleaseCallbackWithNewSelectionIntent_PreventsLateCommitAfterCaptureIsReleased |
| Draft/no-op/invalid | ExistingInspectorDraft_BlocksTranslationAndPreservesExactInput (4cases); ExactNoOpAndReturnToStart_PreserveDocumentAndExistingRedo; InvalidRelease_DiscardsLastValidCandidateAndSecondaryButtonDoesNotComplete; InvalidMove_PreservesLastValidOutlineUntilAnExplicitValidRelease |
| Cleanup/idle | ThrowingInvalidation_StillClearsGestureAndCaptureBeforeCleanupCallbacks; ThrowingReleaseCallback_DoesNotCommitOrRetainGesture; MissingActualReceipt_ObservingPresentationSettlesWithoutContinuousIdleInvalidation |

Inline assertion-quality/pseudo-mutation review: X/Y/Z sign/swap и plane normal bugs
ловятся независимыми численными ожиданиями; потеря inverse parent — parent values;
commit-on-move/multiple-history — document identity/count до release; stale apply —
новый intent в release callback; no-op clearing Redo — exact Undo/Redo counts;
cleanup-before-release regression — captured state и release callback count.
Это статический review возможных mutations, не исполненный mutation score/coverage.
Roslyn source-to-test pairing выполнен для9source/3existing test files:8unpaired/1paired
до новых тестов; identifier matching не равно coverage и не учитывает полностью partial
controller/интеграционный вызов. План/исследование/review — ignored TestResults, не production docs.

Команды (PowerShell; каждый MSBuild запуск с отдельным timestamp binlog):

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --logger 'trx;LogFileName=editor-Debug-verified.trx' --results-directory TestResults/EditorTranslation '-bl:TestResults/EditorTranslation/editor-Debug-verified-{}.binlog' -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=editor-Release-verified.trx' --results-directory TestResults/EditorTranslation '-bl:TestResults/EditorTranslation/editor-Release-verified-{}.binlog' -v quiet
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EditorTranslation/solution-Debug-{}.binlog' -v minimal
```

Solution Debug nonincremental: **exit0, errors0/warnings1556**. Это текущая сборка
всего workspace, а не full-solution tests; новый ED5b source/test warnings не найден.
Full Aether suite этой задачей не повторялась; прежние diagnostics timeouts не закрыты.

### Native и fresh package delivery

Новый `AetherEditorHost --smoke-translation` требует disposable `.aetherproj` и fresh
`--smoke` directory; несовместимые profiles отвергаются CLI. Scene template —
`ManualTests/AetherPackageConsumer/Viewport/Transform.aetherscene`, two inherited cubes;
task-local fixture в `TestResults/EditorTranslation/Fixture`, копия восстанавливается
перед source run. Host сохраняет изменённую disposable сцену, затем закрывается без
unsaved prompt. Production scenes/assets не правились.

```powershell
$env:VK_LAYER_PATH = (Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
dotnet ManualTests/AetherEditorHost/bin/Debug/net10.0/AetherEditorHost.dll --project TestResults/EditorTranslation/Fixture/fixture.aetherproj --smoke TestResults/EditorTranslation/native-debug-final --smoke-translation --validation
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj --no-restore -c Release -o TestResults/EditorTranslation/publish '-bl:TestResults/EditorTranslation/publish-{}.binlog' -v quiet
dotnet TestResults/EditorTranslation/publish/AetherEditorHost.dll --project TestResults/EditorTranslation/Fixture/fixture.aetherproj --smoke TestResults/EditorTranslation/native-release-final --smoke-translation --validation
```

Оба final profiles exit0/Passed=true:5translation PNG (initial/outline/committed/Undo/
Redo),313actual Theme.Modified outline edge pixels до commit, source revision7 на
move,8commit/9Undo/10Redo, actual solid pixels после commit, draft denial+Esc cancel.
Каждый4children=4reaped/4validation reports; UiSessions/Mappings/GpuResources/GpuHandles0,
ValidationErrors0/ValidationWarnings0. Направленный production UiSession/native GPU
input подтверждён; Windows SendInput/physical translation этим не проверялись.

Fresh15packages собраны из текущих Release outputs с task-specific версией
**0.2.5.2-ed5b.20261109.1** (`pack --no-build --no-restore -p:Version=...` для Contracts,
Core,Cobalt,TrueMoon,Extensions.DependencyInjection,Enerit.Generator,Enerit,Mithril,
Argentis,Alloy,Alloy.Rendering.Skia,Alloy.Platform.Silk,Alloy.Hosting,Aether,Aether.Editor).
TrueMoon cold private cache/feed, third-party exact dependencies seeded из assets.
Source guards и package SHA записаны в packages/verification.json. Archive SHA256:

| Package | SHA256 |
| --- | --- |
| TrueMoon.Aether.Editor |5BCB1C765277D451494555CAE2498DB078928C115FE9A353F424CFD785FCC6FB |
| TrueMoon.Aether |0E70ACAA3E062035079AA29AF29EF63EB2F47FEFD44016DE91A0D3165D932A4A |
| TrueMoon.Cobalt |3441C388A8BCC5A08214A83773BD2DD912515AFB71EFF01D57683E7CEE53A584 |

`VerifyViewport.ps1 -Profile translation` создаёт внешний host без ProjectReference/
framework source fallback, строит Debug и публикует Release с PackageReference,
private feed/cache, проверяет installed/output Aether+Editor DLL и native hashes.
`-DependencyFeed` позволяет использовать exact offline third-party feed без изменения
default nuget.org policy. Profile по умолчанию остаётся viewport.

```powershell
./ManualTests/AetherPackageConsumer/VerifyViewport.ps1 -PackageVerification TestResults/EditorTranslation/packages/verification.json -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -EvidenceDirectory TestResults/EditorTranslation/package-native-offline -Profile translation -DependencyFeed C:/Users/MEGA6/.nuget/packages
```

Оба package profiles прошли exit0/Completed=true/Failure=null, SourceFallback=false/
ProjectReferences0; каждый те же5PNG/313outline witnesses/4reaped children, final owners0,
validation0/0. Внешний run: `%LOCALAPPDATA%/Temp/TrueMoon-ViewportPackage-1732c793df974470a2ebf50d22848396`.
NuGet tooling использовало task-local APPDATA и отключённый audit; глобальная
конфигурация/установки не менялись. Свежий Cobalt runtime включён как dependency,
но это Editor delivery proof, не замена самостоятельным Cobalt workload tests.

### Неуспешные попытки, хранение и границы

Первые CPU failures выявили fixture, не вызвавшую production Inspector.Refresh перед
admission; fixture исправлена. Regression для detach сначала неверно пыталась
изменять внутреннюю фиксированную ViewportLayout; теперь отсоединяет layout через
публичный ViewportFrame.SetContent(null) и явно Dispose detached owner. Final18cancel
cases и1465full D/R прошли после исправления. Ошибка styled Text overload обнаружена
типографической регрессией и исправлена до final source/package builds.

Первый native-debug-1 не дал passing report: найден idle invalidation loop при null
receipt (nullable layout comparison); исправлен null→0, добавлен точный regression.
GUI apphost не обеспечивал надёжный capture exit/output, final runs явно через dotnet
DLL. Первый outline capture ждал NeedsUpdate=false, что невозможно во время активного
gesture; host теперь принимает outline по actual current receipt/context. Первый
package-native restore с nuget.org завершился NU1301, offline attempt отдельно прошёл.
Ранние failed editor-Debug-complete/detach логи не являются final passing evidence.

Raw artifacts — ignored `TestResults/EditorTranslation`: final *-verified.trx/log,
solution-Debug.log/binlog, native-debug-final/native-release-final PNG/JSON,
packages/verification.json и package-native-offline/verification.json/native-*.
Failed attempts сохранены в соседних логах/каталогах. Локальное хранение до ручной
очистки TestResults; versioned запись выше остаётся даже при удалении raw файлов.
Не заявляются full Aether/full-solution tests, arbitrary geometry/World editing,
physical held-button/multi-monitor, K7/device-loss/VRAM и общий release matrix.
Engine f.3→f.4 и Nom остаются независимыми. Дальнейшее расширение tools требует
отдельного scope: local-oriented axes/snapping либо rotation/scale, не скрытый ED5b tail.

<a id="status-before-ed5b-2026-11-09"></a>
## Статус перед ED5b — снимок 2026-11-09

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



<a id="ed5a4-acceptance-2026-11-08"></a>
## ED5a.4: Windows-ввод, lifecycle и fresh-package acceptance — 2026-11-08

Запрос: «Продолжай проверку Windows-ввода в published Release и заверши ED5a.4».
[ED5a.4](EDITOR_PLAN.md#ed5a4-acceptance) завершён для bounded Windows x64 v1:
primitive Edit Preview640×360/до64entities, native input доступного supported scope.
Полный hardware/platform release и физические held-RMB/MMB gestures этим результатом
не объявлены. Следующий прикладной шаг — контракт [ED5b translation](EDITOR_PLAN.md#ed5b-translation).
Незакоммиченные предшествующие font/ED4g/ED5a файлы, Nom, shared dependencies/
solution и src/.codex сохранены. Package/public protocol versions не менялись.

### Исправление и проверяемый host

`ScenePreview.SetPresentationSuspended(false)` заменял незавершённый camera wait,
созданный QueueCamera во время UI suspension. Native minimize/resume зависал,
хотя latest candidate был отправлен. Resume теперь сохраняет незавершённый wait;
он завершается только actual presentation ack. Регрессия
`ScenePreviewViewTests.UiSuspend_CancelsPendingWaitRetainsLatestCameraAndResumesSameChildAndSurface`
удерживает original queued-while-hidden task, проверяет incomplete до ack и ждёт
именно его после ack; PID/surface и latest camera также проверяются.

Добавлены16Editor cases в `EditorViewportAdmissionTests` и
`ViewportAcceptanceLaunchOptionsTests`. Fixture выдаёт монотонные actual frame tickets,
позволяет отдельно upload/draw и presentation ack, освобождает detached View.

Host получил два отдельных явных профиля, оба требуют disposable `.aetherproj`
и fresh `--smoke` directory, исключают смешивание с другими smoke profiles:

- `--smoke-viewport`: directed production UiSession в настоящем native GPU окне.
 13групп:50orbit samples/pending admission/invalid draft/unchanged document+tick;
 pan/dolly/F/Esc/focus loss; UI resize/render aspect/letterbox; native minimize/
 latest camera/resume same child+surface; render0×0/latest/resume; newer selection
 intent; delete/old click/Undo; same-ID reopen fresh epoch/default camera; project
 switch/reap; actual Build→Play→Stop/input denial; failed Game activation; first
 presentation cancel; recovery/final Stop.17GPU PNG/profile включают actual pixel
 comparison с цветом red/green cube по projected center (tolerance2).
- `--smoke-native-input`: observer только `SilkWindowHost.Input`, не инъецирует
 события. Через Windows SendInput выполняются выбор обоих кубов, same/background,
 inspector Ctrl+A/invalid text, wheel dolly, F, LMB drag, RMB/MMB press/release.
 Live JSON/PNG фиксируют input counts, capture, camera/frame/layout/DPI/PID/surface,
 invalid draft retention и неизменность документа/history/tick. После native close
 проверяется actual reap и zero owned resources.

`RuntimeEntryPoint` пишет cleanup report также при failed App startup/Game activation.
`EditorSmoke` принимает отсутствие GPU validation только при явном pre-GPU failure
phase/type, RuntimeActivated=false, NoGpuSurfaceProduced=true и нулевых ресурсах.
Остальные children обязаны предоставить validation report. Поэтому в каждом
lifecycle profile **8 GPU reports +1 failed-before-GPU**, а не9GPU validations.
Acceptance compiler jobs используют уникальный external temp BuildDirectory:
inherited repository SourceLink не обходит frozen compiler policy. App/DI ownership,
production compiler allowlist и пользовательские build options не расширены.

### Тесты и матрица доказательств

SDK10.0.403, net10.0, xUnit2/VSTest. Все четыре final TRX имеют failed0/skipped0.
Host publish успешен с4distinct CS8602 diagnostics в прежнем EditorViewportInputSmoke.cs
(ED5a.3); это не zero-warning build. Новые ED5a.4 host files таких diagnostics не дали.
Команды выполнены из src; `$r=TestResults/ViewportAcceptance-mw7vu0lb`.
В изолированных artifacts paths предотвращено пересечение параллельных builds.

```powershell
foreach ($c in 'Debug','Release') {
  $name=$c.ToLowerInvariant()
  dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c $c --artifacts-path "$r/artifacts" --logger "trx;LogFileName=editor-$name-final.trx" --results-directory "$r/results" "-bl:$r/editor-$name-final-{}.binlog"
}
$filter='FullyQualifiedName~TrueMoon.Aether.Tests.Preview.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.|FullyQualifiedName~FrameTransportTests|FullyQualifiedName~SessionSupervisorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Input.|FullyQualifiedName~TrueMoon.Aether.Tests.Checkpoints.'
foreach ($c in 'Debug','Release') {
  $name=$c.ToLowerInvariant()
  $artifacts=if($c -eq 'Debug'){"$r/engine-artifacts"}else{"$r/engine-release-artifacts"}
  dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c $c --artifacts-path $artifacts --filter $filter --logger "trx;LogFileName=aether-$name-final.trx" --results-directory "$r/results" "-bl:$r/aether-$name-final-{}.binlog"
}
```

| Требование | Конкретное доказательство | Результат |
| --- | --- | --- |
| Editor retained input/context | Full Editor Debug и Release, включая16новых cases |1412/1412 каждый |
| Camera/preview/render/transport/scene/ECS/input/checkpoint regression | Bounded Aether Debug и Release по filter выше |2104/2104 каждый |
| Same semantic view/new FrameId | `Click_AllowsNewFrameIdOfSameSemanticViewWithoutRetargetingItsPickSource` | selection admitted, exact pick source retained |
| Control ack/upload без actual presentation | `Camera_ControlAckAndUploadedDrawCannotAdmitPickingBeforeActualPresentation(false/true)` | stale click rejected; actual ack admits |
| Context interruption | `Click_CannotPublishAcrossDocumentProjectStopPlayDeletionOrSurfaceChange(opening/project/stop/play/delete/surface)` | no late selection/intent; Stop releases runtime |
| Capture/geometry ownership | `ActiveGesture_ContextLossReleasesCaptureAndNeverRollsBackIntoForeignGeometry(dpi/hidden-parent/geometry/dispose/detach)` | capture released, no rollback into foreign context/history |
| Hidden camera wait | `UiSuspend_CancelsPendingWaitRetainsLatestCameraAndResumesSameChildAndSurface` | original hidden task completes after ack |
| CLI/fixture isolation | `AcceptanceProfiles_RequireExplicitFixtureAndCannotCombineWithOtherProfiles` for both flags |2cases, accepted options and rejected combinations |
| Existing DPI1/1.5/2/input/selection policy | `DpiResizeAndHide_RequireFreshDrawBeforeInputAndKeepRetainedTree`, `Picking_UsesSharedSelectionAndDraftPolicyForSameDifferentAndBackground`, `Cancellation_ReleasesCaptureAndRestoresOriginalPoseWithNewRevision` | included in full Editor D/R |
| Source Debug + published Release GPU lifecycle | native-debug-complete / native-release |13groups,17PNG,9started=reaped each |
| Fresh NuGet delivery | packages/verification.json + package-viewport-2/verification.json |15fresh packages,8headless cases +2GPU profiles |
| Windows input Debug + published Release | os-debug-1 / os-release-verified native-input.json + report.json | Passed/exit0 each;6PNG each, camera1→2dolly→3F,1child=reaped |

Assertions reopened and reviewed: selection ID and intent, actual wait completion,
capture release, document Undo count, exact pick-source ownership and both pre/post-ack
states are checked. No new test is assertion-free/trivial-only; null guards are
followed by observable value/state checks. Static pseudo-mutation review identifies
late click publication, accepting upload without ack, lost hidden wait and capture
retention as caught behaviors. Lost wait was reproduced before the fix. This is
static gap review plus the actual regression, not a whole-suite mutation/coverage run.

### Native и поставка: воспроизведение и результаты

```powershell
$env:VK_LAYER_PATH=(Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
# Each run uses its own disposable fixture and evidence directory.
dotnet build ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Debug --artifacts-path "$r/artifacts" "-bl:$r/host-pixels-{}.binlog"
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release --artifacts-path "$r/release-artifacts" -o "$r/published" "-bl:$r/publish-{}.binlog"
dotnet "$r/artifacts/bin/AetherEditorHost/debug/AetherEditorHost.dll" --project "$r/project-debug/fixture.aetherproj" --smoke "$r/native-debug-complete" --smoke-viewport --validation
dotnet "$r/published/AetherEditorHost.dll" --project "$r/project-release/fixture.aetherproj" --smoke "$r/native-release" --smoke-viewport --validation
# OS input profile, separately for Debug and published Release; interact then close.
dotnet "$r/published/AetherEditorHost.dll" --project "$r/project-os-release/fixture.aetherproj" --smoke "$r/os-release-verified" --smoke-native-input --validation
```

Все4source/package GPU lifecycle profiles: actual monitor DPI1.5,13groups,
17PNG/profile,9children=reaped,8GPU validation reports и1explicit failed-before-GPU;
final UiSessions/Mappings/GpuResources/GpuHandles0, validation errors0/warnings0.
Windows Debug/Release input profiles имеют1GPU validation report каждый, те же
нулевые cleanup/error counters, stable PID/surface и сохранённый invalid draft.
Release window закрыто через native X; исчезновение окна наблюдалось в Windows.

`VerifyPackages.ps1` создал15fresh packages версии
`0.2.2.7-arch2e.20261108190228`, independent feed/cache/consumer вне checkout.
Debug/Release DLL/apphost × Cobalt/MS DI:8headless cases, no-bootstrap rejection.
`VerifyViewport.ps1 -PackageVerification "$r/packages/verification.json" -ValidationDirectory $env:VK_LAYER_PATH -EvidenceDirectory "$r/package-viewport-2"`
проверил package/source SHA, скопировал только application host и font/license,
использовал PackageReference (0ProjectReference/0framework source fallback), построил
Debug и published Release, выполнил тот же13group lifecycle. Installed/output
Aether+Editor DLL hashes совпали. Команды/logs/binlogs сохранены в manifests.
Package docs отражают момент pack; дальнейшие README/STATUS edits не меняют DLL proof.
Пакеты никуда не публиковались, постоянные версии/dependencies не изменены этой работой.

### Неуспешные попытки и пределы вывода

- native-debug-1: orphaned hidden-camera wait после minimize; исходный regression
  failed до исправления. Зависшие собственные parent/runtime остановлены только
  после сверки PID, parent, exact DLL и `--runtime`; список сохранён.
- native-debug-2/3: inherited SourceLink, затем неподдерживаемая frozen MSBuild property.
  Убрана лишняя property и compiler staging вынесен за Git, allowlist не ослаблен.
- native-debug-4/5: сценарий дошёл до recovery, но early-failure cleanup reporting
  был неполным/неправильно классифицирован. Финальные отчёты различают startup,
  game activation и actual GPU validation, отсутствие отчёта не засчитывается успехом.
- admission-1: ручной frame500 нарушал fixture ordering; исправлен monotonic issuer.
  package-viewport: disposable project template не имел id; fresh second run прошёл.
- os-release был прерван остановкой Computer Use пользователем и не засчитан.
  По новому запросу выполнен отдельный os-release-verified до native close/exit0.
- Sky Windows driver поддерживает drag только LMB, без раздельного held-button/key
  sequence. RMB/MMB press/release подтверждены OS; moving orbit/pan, Esc/focus loss
  подтверждены directed UiSession/native GPU и tests, **не physical held-button proof**.
  Фактический DPI1.5;1/1.5/2 — deterministic UI tests. Physical multi-monitor DPI,
  held-RMB/MMB+Esc, device loss/K7/VRAM остаются hardware/input gates. Проверки
  inspector numeric ED4g.2a не закрываются автоматически этим viewport acceptance.
- Full solution/full Aether/Nom suite не выполнялись этой задачей. Прежний non-green
  full Aether attempt остаётся историческим; engine f.3/f.4 и Nom очередь сохранены.

Raw evidence находится локально в ignored `TestResults/ViewportAcceptance-mw7vu0lb`:
TRX/log/binlog/JSON/PNG, `audit.ps1`/`audit.json` (counts/cleanup/packages/SHA checks).
Внешней выгрузки/долговременного хранения нет; хранить до интеграции/ревью, удаление
только явной уборкой. External compiler/package consumer jobs — в unique `%TEMP%`
директориях, указанным manifests; временная OS cleanup может их удалить.
Эта versioned запись сохраняет scope/команды/outcome/limits при утрате raw artifacts.


<a id="ed5a3-input-2026-11-08"></a>
## ED5a.3: retained camera controller и общий selection intent — 2026-11-08

По запросу пользователя реализован [ED5a.3](EDITOR_PLAN.md#ed5a3-navigation-picking).
Сохранены предшествующие dirty font/ED4g/ED5a.0–2 файлы, Nom, shared solution/
dependencies и src/.codex. Новых зависимостей, protocol versions, scopes и game
World contracts этот срез не вводит. Cobalt и Microsoft DI composition проверены.

### Результат и ownership

Default GPU EditorPreview передаёт frozen PreviewViewState в ShowViewAsync;
capture выполняется на requesting UI owner до worker dispatch. SceneDocumentState
имеет свежий OpeningId после successful opening, независимо от SceneId/revision/
того же immutable document reference. Workspace хранит opening/content token и
checked SelectionRevision, который растёт даже для explicit same-ID/empty Clear.
Commits/Undo/Redo сохраняют camera, reopen/project/binding renewal сбрасывают её.
Inspector synchronizes committed workspace до preview capture; прежний двухаргументный
публичный constructor сохранён. Existing same-ID replacement draft conflict policy
сохранена: черновик не исчезает только из-за обновления opening observation.

App владеет EditorViewportState и coordinator, View — product injected factory
IFactory<EditorViewportController,EditorViewportArguments>. Product на UI owner
заимствует services, владеет subscriptions/capture. Один active attached binding;
foreign/empty identity не меняет visibility/ownership. Detach/dispose освобождает
capture/subscriptions немедленно и передаёт async Stop/drain App coordinator.
Custom external viewport transport сохраняет прежний legacy Show и не ждёт
стандартного окна ради своего presentation consumer.

RMB orbit, MMB pan, wheel dolly, F fit выбранного affine opaque cube подключены
к retained UiSession routing. Wheel сохраняет focus, F требует canvas focus.
Navigation сохраняет document/history/invalid inspector draft и не делает game tick.
Esc/focus loss/capture cancellation возвращают pose с новой revision; source/context
change отбрасывает старый жест. LMB click≤4logical pixels публикует Hit/Miss через
общий EditorViewModel selection path. Same-ID сохраняет draft; different/clear
использует existing policy. После query и release capture проверяются opening/view/
content/camera/surface/layout и intent; pending camera/Play/unsupported/stale results
не выбирают объект. Routing учитывает islands/chrome/overlays/foreign capture.

UI dimensions/scale/visibility имеют отдельную drawn geometry epoch. Resize/render
aspect/DPI/hide требуют нового actual layout receipt. SetPresentationSuspended
отменяет presentation waits и убирает receipt, удерживая latest desired camera и
physical child/surface; resume требует actual ack. Hidden binding прекращает texture
copies. Initial hidden default Show ждёт visibility до allocations; source queue
остаётся one-current/latest waiting, visibility cancellation retries после resume.

### Автоматические проверки и quality review

33new Editor cases (7math,24controller,1capture,1CLI) +1Aether suspend case.
Existing dual-provider composition проверяет exclusive first/second View ownership
и takeover после Dispose, custom transport проверяет прежнюю автономность.

| Проверка | Конкретное evidence |
| --- | --- |
| Camera geometry/math | Orbit_UsesTotalLogicalDeltaClampsPitchAndReturnsToExactPoseWithFreshRevision; Pan_UsesImageLogicalHeightAndMovesPivotWithCamera; Dolly_IsExponentialBoundedAndPreservesOrientation; Frame_FitsAllAffineCornersWithMarginAndPreservesRotation; InvalidMathRetainsLastValidValueAndRevisionExhaustionNeverWraps |
| Same source/opening/camera | SameImmutableDocumentReopen_RenewsEpochResetsCameraAndRejectsOldView; CommittedEditUndoRedo_PreserveCameraRenewContentAndInvalidateCapturedGesture; ExplicitCapture_RunsOnRequestOwnerAndCoalescesFrozenContextOnlyChanges |
| Input/selection/draft | Navigation_RetainsInvalidDraftAndDocumentAndUsesSameChildAndSurface; Picking_UsesSharedSelectionAndDraftPolicyForSameDifferentAndBackground; Click_OlderThanTreeOrClearIntentCannotPublishSelection (same/clear/other); Picking_RejectsDragPendingCameraRuntimeChromeAndOverlay |
| Ownership/terminal input | Cancellation_ReleasesCaptureAndRestoresOriginalPoseWithNewRevision (Esc/focus/foreign capture/popup); Binding_IsExclusiveAndDisposedOwnerDrainsWithoutDisposingBorrowedServices; WheelAndFrame_AreCameraOnlyAndFrameRequiresFocusedCanvasAndRenderableSelection |
| DPI/letterbox/suspend | DpiResizeAndHide_RequireFreshDrawBeforeInputAndKeepRetainedTree (1/1.5/2); RenderResize_UsesNewProjectionAndRejectsLetterboxAndForeignCapture; Visibility_FencesOnlyActualOwnerAndSuspendsWithoutLosingDesiredCamera; UiSuspend_CancelsPendingWaitRetainsLatestCameraAndResumesSameChildAndSurface |

Static pseudo-mutation/assertion review без изменения production mutants: independent
literal yaw/pitch/dolly/tolerance/corner-in-frustum witnesses, exact IDs/revisions,
negative admission и unchanged document/draft/child/surface дополняют acceptance.
Review добавил render-resize/letterbox/foreign-capture case и root/ancestor visibility
witness в DPI1/1.5/2. Controller наблюдает root invalidation и снимает эту подписку
на detach/dispose: скрытый parent не оставляет active camera waits/copies. Новые tests не являются
presence-only/roundtrip checks. Polling в deterministic fixture ограничен5s;
он ждёт наблюдаемое protocol состояние, а не фиксированную sleep задержку.
Fixture воспроизводит actual intermediate frames: race между draw и new camera
может отвергнуть старый ack, затем обязателен admitted desired frame. Coverage %,
полная native lifecycle matrix и physical host input этим review не измерялись.

### Фактически выполненные команды / итог

Из src, SDK10.0.403, net10/xUnit2/VSTest. Каждый MSBuild invocation — с unique binlog,
артефакты изолированы от Nom и прежних output directories:

```powershell
$run = 'TestResults/ViewportInput-mw7qaq1w'
$filter = 'FullyQualifiedName~TrueMoon.Aether.Tests.Preview.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.|FullyQualifiedName~FrameTransportTests|FullyQualifiedName~SessionSupervisorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Input.|FullyQualifiedName~TrueMoon.Aether.Tests.Checkpoints.'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --artifacts-path "$run/artifacts" --logger 'trx;LogFileName=editor-debug-root.trx' --results-directory "$run/results" "-bl:$run/editor-debug-root-{}.binlog"
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --artifacts-path "$run/release-artifacts" --logger 'trx;LogFileName=editor-release-root.trx' --results-directory "$run/results" "-bl:$run/editor-release-root-{}.binlog"
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --no-build --artifacts-path "$run/artifacts" --filter $filter --logger 'trx;LogFileName=aether-debug-verified.trx' --results-directory "$run/results" "-bl:$run/aether-debug-verified-{}.binlog"
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --artifacts-path "$run/release-artifacts" --filter $filter --logger 'trx;LogFileName=aether-release-verified.trx' --results-directory "$run/results" "-bl:$run/aether-release-verified-{}.binlog"
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release --artifacts-path "$run/release-artifacts" -o "$run/published-root" "-bl:$run/publish-root-{}.binlog"
$env:VK_LAYER_PATH = (Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
dotnet "$run/artifacts/bin/AetherEditorHost/debug/AetherEditorHost.dll" --project "$run/project-debug/fixture.aetherproj" --smoke "$run/native-debug-root" --smoke-navigation --validation
dotnet "$run/published-root/AetherEditorHost.dll" --project "$run/project-release/fixture.aetherproj" --smoke "$run/native-release-root" --smoke-navigation --validation
```

- Final full Editor **1396passed/0failed/0skipped** каждый Debug/Release.
- Final bounded Aether **2104passed/0failed/0skipped** каждый Debug/Release.
- Release publish успешен. Новые production files не добавили compiler warnings;
  существующие warnings не объявляются исправленными. Full solution/full Aether/
  fresh NuGet и Nom suites не выполнялись.
- Native source Debug/published Release directed UiSession input:6navigation PNG
  каждый (default/orbit/pan/dolly/frame/cancel), actual green cube pixel #ff33cc4c,
  camera revisions1/2/3/4/5/7, runtime generation1/surface1,1started=reaped child.
  Hit/same/different/miss и shared tree/inspector; invalid draft сохранён при camera
  navigation, document/history/tick unchanged. report.json: UiSessions/Mappings/
  GpuResources/GpuHandles0, ViewDisposed=true, validation0errors/0warnings. Child
  runtime report: surface1created/1released, slots3created/3released, live0/handles0.
- Raw TRX/binlog/JSON/PNG и SHA verification manifest сохраняются локально в ignored
  TestResults/ViewportInput-mw7qaq1w; команды, outcome и limitations остаются здесь.

Ранние запуски не были green: baseline1360/1363 обнаружил inspector same-ID draft
policy regression и custom viewport capture rejection. Исправлены policy и legacy
external transport path, старые assertions сохранены. Промежуточные compile/fake UI
профили input-1…8/editor-debug выявляли проблемы оснастки: ожидание Show completion
до IsCurrent и необходимость подтверждать intermediate frames перед coalesced latest;
при stale draw ack должен отвергаться и повторяться на актуальном ticket. Итоговые
профили выше прошли. Первый native-debug завершился до window/profile из-за отсутствия
Content в disposable fixture; каталог добавлен, source/published runs прошли. Прежний
non-green full Aether attempt ED5a.2 не считается исправленным этим bounded срезом.

Следующий этап — [ED5a.4](EDITOR_PLAN.md#ed5a4-acceptance): physical host mouse/key input,
расширенная native DPI/suspend/reopen/project/Play interruption и delivery matrix.
Directed UiSession input в настоящем окне не является physical input proof; full ED5a
и прежние engine f.3/f.4/release gates остаются открытыми.

<a id="ed5a2-view-2026-11-08"></a>
## ED5a.2: explicit preview camera, frame stamp и actual receipt — 2026-11-08

По продолжению пользователя реализован следующий [ED5a.2](EDITOR_PLAN.md#ed5a2-preview-view).
Прежние dirty font/ED4g/ED5a.0/1 изменения, Nom, shared solution/dependencies и src/.codex
сохранены. Новых сервисов/DI aliases/scopes не создавалось: ScenePreview продолжает
принадлежать App/AetherHost и заимствует FrameSession; новые captures/receipts — CPU values.

### Результат и границы

Общая SessionIdentity теперь строго Control4/Frame2/Snapshot2. Существующие native
counterparts требуют одинаковые новые Aether binaries; старый peer отвергается.
Hello у built-in EditPreview сообщает PreviewViewVersion1, у game0. Dedicated
PreviewViewCodec имеет state256bytes/stamp148bytes с exact version/length, rigid
matrix/perspective/pivot/distance и explicit opening/view/content/scene/revision.
Camera UInt64 bits сохраняются полностью; Tick/SceneRevision не используются как
camera revision. Frame2: TMFRAME2/header1536/slot384/3slots, stamp field offset176.
GPU JSON stamp требует Version1, существующий GPU synchronization stamp version2
не переопределён. CPU reference pattern остаётся untagged/non-geometric; обе frame
codec формы способны явно нести/отвергать stamp, но CPU pattern не становится pickable.
Полный [layout и API](EDITOR_CAMERA_SELECTION.md#ed5a-view-contract-v1) зафиксирован.

ShowViewAsync передаёт scene + initial PreviewViewState одним LoadScene до surface
readiness, проверяет exact capability/stamp acknowledgement. RuntimeProtocol применяет
SetPreviewView и CaptureRenderFrame под одним command gate. PrimitiveScene immutable
capture хранит external camera matrix; GpuFrameData.FromScene берёт stamp именно от
этого scene, а не от позднего latest desired. Exact frame/slot/use остаются проверены;
Validate отвергает mixed scene/camera metadata. Legacy ShowAsync остаётся static/untagged.

QueueCamera — один in-flight control/presentation wait и один latest waiting state.
Task на каждый mouse sample не создаётся; новые samples заменяют waiting. CameraUpdates
не заканчивается на control ack/upload. Camera-only update не запускает Build/game tick,
не меняет child/surface. Show/Resize/Stop закрывают admission и сначала отменяют batch
completion/presentation wait, затем drain-ят accepted control под supervisor deadline.
Suspend хранит latest desired до Resume. Error/failure cleanup сохраняет owner для retry.
New document epoch допускает same-ID reopen с меньшей authoring revision; старый context
не может обновить/выбрать новую сцену. Caller обязан создавать эти explicit epochs/tokens.

Presented(frame,image,clip,layout) фиксирует PreviewPresentedView только после successful
actual acknowledgement. Retained camera/matrices/extent/pick snapshot/frame/geometry
принадлежат оригинальному capture, не latest mutable model. IsCurrent проверяет desired
camera/context/runtime/surface; новый FrameId того же semantic view разрешён. EditorViewport
различает upload/draw/layout, не подтверждает hidden/zero-size/rearranged/foreign image;
MatchesPresentedLayout разрешает новый incoming FrameId с прежней geometry.
EditorSession.ReadPresentedView на UI owner добавляет drawn stamp/layout/clip/visibility
и не делает повторный ack. GPU handles остаются у прежних owners, receipt только CPU.

Default EditorPreview coordinator пока вызывает legacy Show; new explicit API opt-in.
Новая native acceptance использует его через имеющийся Editor frame callback/reader.
Retained controller, active viewport binding, document/context и selection intent owner,
orbit/pan/dolly/F/click input ведутся в ED5a.3. Full ED5a/f.3/f.4 не закрыты.

### Прямые автоматические проверки

| Scope | Evidence |
| --- | --- |
| PreviewViewCodec24cases | Codec_UsesExplicitOffsetsAndExactUnsignedRevisionWithoutAliasing; malformed envelope/identity/camera partitions; FrameV2_PreservesStampSlotAndSimulationCountersAndRejectsOldHeader; EneritControlAndGpuJson_RoundTripExplicitCapabilityAndDetachedState, включая required GPU Version |
| PreviewViewProtocol10cases | Command_InstallsInitialViewAndCapturesImmutableSceneStampBeforeCompletion; stale/reused/foreign context reject с сохранением capture; game/legacy denial, initial view/revision mismatch; tick0/scene/primitives invariants и mixed GPU metadata reject |
| ScenePreviewView18cases | ShowView_RequiresActualAckAndFreezesCameraGeometryLayoutAndExactTicket; Camera_CoalescesOneHundredSamplesAndWaitsForActualPresentationWithoutNewRuntimeOrSurface (ровно2commands/100samples); unknown stamps, stop wait/control drain, failure/retry, same-ID reopen, suspend/resume, rejected older ticket и invalid candidate |
| Editor10cases + existing composition guards | Draw_FreezesLetterboxAndClipAndKeepsLayoutRevisionAcrossNewFrameIds; Draw_HiddenTransparentSuspendedAndForeignUploadCannotAcknowledgeAnOldImage; CameraSmoke CLI8cases; existing view factory checks empty/disposed ReadPresentedView |

Итого52new Aether +10new Editor. Literal bytes/matrices/actual pixels дополняют
roundtrips. Static gap/assertion review записан в non-stageable task temp, без
coverage percent. Roslyn narrow-root static scan256sources/0sibling test files —
не line/branch coverage; существующие тесты читались напрямую (IDE findTests unavailable).

### Фактические команды / outcomes

Из src, SDK-style net10/xUnit2/VSTest. Каждый build/test/publish имел unique binlog;
артефакты разнесены, чтобы не трогать Nom и существующие outputs:

```powershell
$run = 'TestResults/PreviewView-mw7mujqb'
$filter = 'FullyQualifiedName~TrueMoon.Aether.Tests.Preview.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.|FullyQualifiedName~FrameTransportTests|FullyQualifiedName~SessionSupervisorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Input.|FullyQualifiedName~TrueMoon.Aether.Tests.Checkpoints.'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --artifacts-path "$run/final-artifacts" --filter $filter --logger 'trx;LogFileName=aether-debug.trx' --results-directory "$run/results" "-bl:$run/aether-debug-{}.binlog"
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --artifacts-path "$run/final-artifacts" --filter $filter --logger 'trx;LogFileName=aether-release.trx' --results-directory "$run/results" "-bl:$run/aether-release-{}.binlog"
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --artifacts-path "$run/editor-artifacts" --logger 'trx;LogFileName=editor-debug-verified.trx' --results-directory "$run/results" "-bl:$run/editor-debug-verified-{}.binlog"
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --artifacts-path "$run/editor-release-artifacts" --logger 'trx;LogFileName=editor-release-verified.trx' --results-directory "$run/results" "-bl:$run/editor-release-verified-{}.binlog"
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release --artifacts-path "$run/editor-release-artifacts" -o "$run/published" "-bl:$run/publish-{}.binlog"
```

- Final bounded Aether **2103passed/0failed/0skipped каждый Debug/Release**.
- Full Editor **1363passed/0failed/0skipped каждый Debug/Release**.
- Release publish успешен. Existing warnings сохранены; новый source warning scope
  сверяется отдельно с verification manifest. Full-solution/Nom результаты не присваивались.

Полный Aether был запущен до final cancellation fix:
`dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --artifacts-path TestResults/PreviewView-mw7mujqb/artifacts --logger 'trx;LogFileName=aether-full-debug.trx' --results-directory TestResults/PreviewView-mw7mujqb/results '-bl:TestResults/PreviewView-mw7mujqb/aether-full-debug-{}.binlog'`.
Он **не green и не завершён**: выявил Stop_CancelsPresentationWaitAndDrainsCameraBeforeReapingWithoutFurtherUiFrames
(`No exception was thrown`, ожидался OperationCanceledException) и
RepositoryGameProject_FreezesAncestorPropsAndContractReferences (`Expected an existing game csproj`,
GameBuildPreparationTests.cs720). После более5минут дальнейшей compiler/process работы
остановлено только собственное дерево этого test invocation; final total/TRX отсутствует.
Причина repository path assumption здесь не установлена/не исправлялась. Новый full
passing baseline этим targeted run не заменён, общий release gate остаётся открытым.

Наблюдённый completion/cancellation race исправлен в production: batch TrySetCanceled
вызывается **до** CTS.Cancel и его callbacks, чтобы worker finally не мог опубликовать
success вместо cancellation. Final target включает прежний failing case, accepted
control drain и failure/retry; requirements/assertions не ослаблялись. Intermediate
33format failures были прежними fixed1024/v1/Control3 assertions: обновлены под explicit
v2 bytes/offsets и проверены напрямую. Три новых fixture mistakes (неизменившийся length
byte, GPU synchronization stamp version и Issue на уже закрытой surface) исправлены;
отдельные missing-using build attempts сохранены. Все original logs доступны в run path.

### Native source Debug / published Release

`--smoke-camera` требует fresh output и disposable .aetherproj с Transform.aetherscene:
один inherited cube (-1.5,1,-1), identity rotation, scale1, Appearance(.8,.2,.15).
В этом запуске fixture скопирован5files из сохранённого ED4g fixture в отдельные
`native-project-debug-verified` и `native-project-release`; source fixture не изменён.
CLI/API updates направлены только в собственный App; глобального input/settings нет.

```powershell
$env:VK_LAYER_PATH = (Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
dotnet TestResults/PreviewView-mw7mujqb/editor-artifacts/bin/AetherEditorHost/debug/AetherEditorHost.dll --project TestResults/PreviewView-mw7mujqb/native-project-debug-verified/fixture.aetherproj --smoke TestResults/PreviewView-mw7mujqb/native-debug-verified --smoke-camera --validation
dotnet TestResults/PreviewView-mw7mujqb/published/AetherEditorHost.dll --project TestResults/PreviewView-mw7mujqb/native-project-release/fixture.aetherproj --smoke TestResults/PreviewView-mw7mujqb/native-release --smoke-camera --validation
```

Оба exit0, camera.json Passed=true.50camera samples завершились revision51 с прежними
PID/runtime generation/surface1; cube pixel `#ffcc3326` переместился, прежняя позиция
стала background `#ff0a0f19`. Resize800×450 дал surface2 с тем же camera51; suspend
сбросил receipt, latest queued camera после Resume640×360 пришла с camera52/surface4.
На каждый final profile4camera PNG (default/moved/resized/resumed) и pixels/metadata
в camera.json. Authoring object/history/dirty/selection не изменены, tick0, child1=reaped.
ReadPresentedView проверялся на actual FramePresented UI owner. report.json каждого:
UiSessions/Mappings/GpuResources/GpuHandles0, ViewDisposed, Reaped=true,
ValidationRequested=true, ValidationErrors0/ValidationWarnings0; project cleanup passed.
Первый native Debug до финального reader/version update тоже passed, но final proof —
именно native-debug-verified/native-release выше.

Raw binlogs/TRX/build/native logs, PNG/JSON и SHA manifest находятся в ignored run path
как дополнительное evidence без обещания бессрочного хранения. Scope/commands/outcomes
остаются в versioned HISTORY. Native profile доказывает explicit engine camera API,
не physical navigation/selection input, не mesh/gameplay camera и не fresh NuGet delivery.
Следующий [ED5a.3](EDITOR_PLAN.md#ed5a3-navigation-picking): explicit context ownership
в default Editor consumer, one active viewport binding, retained input и общий selection intent.

<a id="ed5a1-values-2026-11-08"></a>
## ED5a.1: immutable camera, projection/unprojection и ray math — 2026-11-08

По запросу пользователя реализован CPU срез [ED5a.1](EDITOR_PLAN.md#ed5a1-camera-values).
Незакоммиченные font/ED4g изменения, Nom, solution/dependencies и src/.codex сохранены.
Новых сервисов/DI lifetimes, transport/native/GPU и UI input изменений нет.

`Rendering.PerspectiveProjection` содержит прежние settings/extent/proper-rigid
validation и Vulkan View*Projection из ECS PerspectiveCamera. Его публичный API
сохранён;44existing tests прошли. Внешний PreviewCamera хранит get-only matrix,
perspective, pivot/distance и positive ulong revision без Euler roundtrip. Exact
no-op возвращает прежний instance; изменения pose/projection/pivot/distance и rollback
получают новую revision; checked overflow не wrap-ит. Extent-dependent validation
выполняется перед publication через CreateProjection, не через скрытый viewport owner.

CameraProjection фиксирует matrix/inverse/physical extent; Project/Unproject используют
double intermediates, NDC depth[0,1], один Y flip и finite homogeneous division.
Для Single inverse проверяются оба identity products с double accumulation и
absolute element error≤0.001: Matrix4x4.Invert success/finite zero после overflow
недостаточно. Invalid inverse отклоняется при capture; invalid W/points/segment
возвращают false без partial output. Прежний forward-only ECS contract не ужесточён.
ViewportImageBounds использует half-open logical rectangle и physical pixel center,
без повторного DPI. Ray3 нормализует world direction и ограничен near→far длиной;
cube inverse direction не нормализуется, world t сохраняется при scale/shear.
Near-inside origin выбирает positive exit; parallel axes/near/far/behind handled.

PreviewPickSnapshot.Capture снимается с того же projected SceneSnapshot, не с поздней
authoring model: detached persistent GUIDs, projected order/world/inverse, bound64.
Interaction не фильтрует rendering cubes; invisible groups не дают hit. Nearest
surface выигрывает, exact ties сохраняют draw order, не GUID sort. Неизвестный
complete geometry profile `editor.opaque-cubes/1` или invalid inverse делает весь
query Unavailable, не выбирает cube за skipped occluder. Некорректные/пустые/normalized
duplicate IDs отвергаются до capture. Result сохраняет exact snapshot и различает
Hit(EntityId,world distance), valid Miss и Unavailable(reason). Это geometry result,
не admission к Select/Clear и не доказательство фактического presentation.

### Новые прямые xUnit проверки

| Production API / cases | Независимое доказательство |
| --- | --- |
| PreviewCamera8 | Whole candidate copies, default compatibility, exact no-op, changed field revision, rollback increment, invalid candidate preservation и max-ulong overflow |
| CameraProjection/PerspectiveProjection39 | Literal matrix/near/far, arbitrary rigid pose, camera-space expected points, single Y flip, pixel centers, DPI1/1.5/2 logical arithmetic, letterbox/half-open/invalid bounds, malformed/Single settings, inverse overflow/unstable pose/zero W/overflowed NDC |
| Ray3 17 | Direction scale epsilon..Max normalization, inclusive far before/at/after, near-inside exit/exact surface, parallel edge/just outside, behind/disjoint/near clipped miss, nonuniform/shear/reflection/full hierarchy product и rotated-cube world-AABB false positive |
| PreviewPickSnapshot13 | Frozen source/returned-copy mutation,64entries/last hit, nearest/overlap/reversed-GUID tie, default ray vs empty/background, invalid inverse/unsupported complete profile, pixel query/outside/zero-W, bad IDs и exact authoring hierarchy payload shared with PreviewGameSession renderer |

Всего77newcases. Roundtrip checks дополнены literal/independent geometry witnesses;
static gap/assertion review сохранён в non-stageable task temp. Roslyn pairing
analyzer запущен один раз на narrow Aether root:245sources,0test files внутри этого
root; sibling test projects он не оценил. Это не line/branch coverage. IDE findTests
недоступен; existing PerspectiveCameraTests прочитан непосредственно.

### Фактические команды и результаты

SDK-style net10/xUnit2/VSTest; каждый вызов использовал отдельный binlog и isolated
artifacts. Запуск из src, SDK/NuGet.Config под разрешённой escalation:

```powershell
$run = 'TestResults/CameraValues-mw7m0ihh'
$filter = 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.|FullyQualifiedName~TrueMoon.Aether.Tests.Preview.'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --artifacts-path "$run/artifacts" --filter $filter --logger 'trx;LogFileName=aether-debug.trx' --results-directory "$run/results" "-bl:$run/aether-debug-{}.binlog"
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --artifacts-path "$run/artifacts" --filter $filter --logger 'trx;LogFileName=aether-release.trx' --results-directory "$run/results" "-bl:$run/aether-release-{}.binlog"
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --artifacts-path "$run/editor-artifacts" --logger 'trx;LogFileName=editor-debug.trx' --results-directory "$run/results" "-bl:$run/editor-debug-{}.binlog"
```

- Aether bounded ECS/Rendering/Scenes/Preview: **1857passed/0failed/0skipped**
  каждый Debug и Release, в том числе77new+44existing camera cases. Это не full Aether.
- Full Editor compatibility: **1353passed/0failed/0skipped Debug**.
  Editor Release в этом срезе не повторялся; прежний ED4g.4 D/R/native остаётся историческим.
- Intermediate narrow run:103passed/1failed из-за exact double equality для
  результата Single inverse4.5000000447; assertion уточнён до1e-6. Review run:
  120passed/1failed — тест ожидал admission у large translated unstable inverse;
  исправлен на ожидаемый rejection, production admission не ослаблялся.
  Оба raw failure attempts сохранены вместе с final passing runs.
- Existing source/analyzer warnings сохранены; сообщений по новым production/test
  файлам в final build logs нет. Diff whitespace, namespaces, links и SHA сверены.

Raw binlogs/TRX/build logs и verification manifest сохранены под ignored run path
выше как дополнительное evidence; versioned scope/outcomes/commands находятся здесь.
Это локальные task artifacts без обещания бессрочного хранения.
Native/published/fresh package/physical input/full Aether/full solution/performance
здесь не запускались. Synthetic DPI arithmetic не доказывает native DPI routing;
CPU cube picking не обещает raster-ID equivalence на triangle/depth edges.

Планы/статусы обновлены. Следующий срез — [ED5a.2](EDITOR_PLAN.md#ed5a2-preview-view):
versioned camera/control/frame metadata, serialized capture и bounded update lane
в том же EditPreview child, затем actual Presented receipt. Document/view/content/
camera/surface/layout/intent admission и UI navigation/selection ещё впереди.
Full ED5a и renderer f.3/f.4 этим срезом не закрыты; Nom очередь независима.

<a id="ed5a0-contract-2026-11-08"></a>
## ED5a.0: контракт камеры и выбора объекта — 2026-11-08

По запросу пользователя начат ED5a с контрактного среза. Чтением текущей dirty
рабочей копии проверены ScenePreview/PreviewGameSession, EditorPreview,
ViewportPresentation/FrameMetadata/GpuFrameData, EditorViewport/Binding/Session,
Workspace/ViewModel/Hierarchy selection, PrimitiveSceneAdapter/PrimitiveScene,
PerspectiveCamera, existing App composition и control/session versions.
Исторические Editor1353/1353 D/R и native результаты ED4g.4 не запускались заново.
Nom и его параллельные изменения сохранены.

Зафиксирован [контракт editor.preview-view/1](EDITOR_CAMERA_SELECTION.md#ed5a-view-contract-v1):
внешняя transient rigid/perspective camera с прежними defaults; отдельные desired,
produced и actually presented состояния; document/view/content/camera/surface/
layout/selection intent fences. Текущий frame metadata не несёт camera stamp,
а Show заменяет child по document revision: camera-only lane и versioned metadata
требуются до navigation, без child-per-move и retag старого кадра.

Первый picking profile — CPU ray по exact projected opaque cubes, persistent IDs
и full affine matrices. World AABB не заменяет local cube test; inverse-transformed
direction не нормализуется. Frame ray идёт через surface pixel center по frozen
ViewProjection/letterbox. С Vulkan flipped-Y используется NDC y=2v-1, без повторного
DPI flip/scale. Hit/Miss/Unavailable различны; stale/unsupported не очищает selection.
Один owner — Workspace; явный hit/clear использует existing tree draft policy,
camera navigation сохраняет даже invalid inspector draft. Новые FrameIds одного
semantic view разрешены; desired camera pending, same-ID reopen, Play/Stop/resize,
newer selection intent или deleted entity запрещают позднюю публикацию.

Navigation v1 планирует RMB orbit/MMB pan/wheel dolly/F focus через existing input;
ownership/IFactory/controller и cancel/drain определены. Новые C# production API,
camera control/frame codec, viewport controller и UI picking ещё не реализованы.
Они разбиты на ED5a.1 values/math → ED5a.2 transport/actual receipt → ED5a.3 input/
selection → ED5a.4 acceptance в единственном [EDITOR_PLAN](EDITOR_PLAN.md#ed5a-camera-picking).
Wire version numbers/bytes и concrete C# names принадлежат реализации ED5a.2,
а не существующим Control3/Frame1 fields; protocol без объявления не расширялся.
Mesh/future renderer f.3/f.4, live World и gameplay camera не подменяются этим профилем.

Новая исполняемая проверка — [AetherCameraContractProbe](../../ManualTests/AetherCameraContractProbe/README.md),
net10.0 console без native окна/GPU/child, с ProjectReference на текущий Aether.
Она использует существующие PerspectiveCamera.CreateViewProjection и PrimitiveScene;
ray/cube helpers являются prototype выбранной математики, не production picker.
Fresh Debug run прошёл20checks, exit0; JSON отмечает NativeWindow=false и
ProductionNavigation=false. Сначала throwaway prototype в evidence прошёл19checks,
затем воспроизводимый ManualTests source добавил rejection invalid logical coordinates.

```powershell
dotnet run --project ManualTests/AetherCameraContractProbe/AetherCameraContractProbe.csproj --artifacts-path TestResults/CameraSelectionContract-mw6vq88d/manual-artifacts "-bl:TestResults/CameraSelectionContract-mw6vq88d/manual-probe-{}.binlog" -- TestResults/CameraSelectionContract-mw6vq88d/manual-probe.json
```

Evidence в `TestResults/CameraSelectionContract-mw6vq88d/`: log/binlog/JSON, исходный prototype и
source/doc SHA manifest. Versioned ManualTests source и эта запись позволяют
повторить probe после отсутствия ignored raw artifacts. Build/run использовал
текущую .NET/NuGet конфигурацию; это не новый full Aether/Editor test run.

| Численный инвариант | Check names / outcome |
| --- | --- |
| Default camera projection совместима с нынешним preview | ExistingProjectionMatchesPrimitiveCamera — passed |
| Near/far используют z0/z1, без OpenGL −1..1 remap | NearPlaneDepthZero, FarPlaneDepthOne — passed; world near(0,1,5.9), far(0,1,−93.99809) при ожидаемом−94 с Single tolerance0.02 |
| Верх/низ и pixel-center ray | TopLeftImageYUsesNdcMinusOne, BottomImageYUsesNdcPlusOne, PixelCenterReprojection — passed |
| Synthetic DPI normalization и letterbox bounds | LogicalCoordinatesDpi1/1.5/2, InvalidLogicalCoordinatesRejected, HalfOpenLetterboxEdges — passed; это арифметика logical mapping, не platform/native input proof |
| Clip segment и visible surface | NearPlaneInsideCubePicksExitSurface, FarClippedSurfaceDoesNotHitAtZero, BehindCameraDoesNotHit — passed |
| Nonuniform scale/shear и world-distance t | LocalRayKeepsWorldDistanceParameter, AffineShearCubePick — passed; t5.5 в обоих случаях |
| Rigid rotated pose и rejected invalid camera | RigidRotatedCameraCenterRay, ReflectedCameraRejected, ScaledCameraRejected, SuspendedExtentHasNoProjection — passed |

Контракт и links проверены, shared документы перечитаны и SHA проверены перед
атомарной заменой. Production API/unit/native/source-package camera acceptance
не объявляется пройденной: numerical prototype её не заменяет. Следующий
конкретный шаг — ED5a.1 immutable camera/pick values и прямые automated tests
на существующей projection math; camera/picking всё ещё отсутствуют в приложении.


<a id="ed4g4-angle-units-2026-11-08"></a>
## ED4g.4: переключение радиан/градусов — 2026-11-08

По запросу пользователя добавлен retained checkbox «Углы в градусах» под Rotation,
default radians. В текущей preference меняются label/accessible names/numeric step
(0.01rad/1degree) и adjustment limits. Existing Vector3Editor/NumberBox/CheckBox,
typed View/model/style и shared authoring используются без новых generic/DI/
engine/wire APIs. Compatibility EditorTransformView также показывает DisplayValue.

До кода в EDITOR_PLAN зафиксирована policy: любой invalid/incomplete rotation
text, включая pending reset, блокирует unit switch до исправления/Cancel/Apply
reset. Unrelated invalid fields не мешают; active drag/busy/read-only/conflict/
missing Transform также запрещают switch. Unit preference переживает selection,
Cancel/history/theme и не является persistent scene metadata.

`TransformField.Value` остаётся canonical radians; `DisplayValue` — UI projection.
Unit-only transition не меняет canonical string/dirty/history. Degree edits
конвертируются один раз, без wrap/clamp;720° остаются4πrad. Canonical Float validation
применяется после conversion; например1e40degrees допустимы, если radians ещё Float.
Exact degree text кэшируется для обратного switch. Эквивалентный numeric display
format сохраняет canonical precision без нового draft. Gesture snapshot отдельно
сохраняет display text, поэтому Esc возвращает точную строку и canonical draft.
Labels/details/reset help/diagnostics следуют выбранным units. Theme/resize/focus
и retained control identity сохранены; canonical reset/Apply/history не расширялись.

Свежие проверки в `TestResults/AngleUnits-mw6tzf8q/`: baseline Editor1323/1323 Debug;
final Editor1353/1353 каждый Debug/Release, failed0/skipped0 (+30 direct cases).
Первый focused angle run30/30 имел duplicate signed-zero InlineData warning;
он удалён и заменён отдельной signed-zero/culture проверкой. Итоговые suites
новых warnings не добавили; прежние generated/XML CS1591 и diagnostics xUnit1031
остались в своём scope. Ранний Inspector run85fail из-за неподдержанных computed
binding selectors исправлен прямыми presentation properties; следующий Inspector
175/175 прошёл до новых angle tests. Итоговый full suite включает все случаи.

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --artifacts-path TestResults/AngleUnits-mw6tzf8q/artifacts --logger "trx;LogFileName=editor-debug.trx" --results-directory TestResults/AngleUnits-mw6tzf8q "-bl:TestResults/AngleUnits-mw6tzf8q/editor-debug-{}.binlog" -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --artifacts-path TestResults/AngleUnits-mw6tzf8q/artifacts --logger "trx;LogFileName=editor-release.trx" --results-directory TestResults/AngleUnits-mw6tzf8q "-bl:TestResults/AngleUnits-mw6tzf8q/editor-release-{}.binlog" -v quiet
dotnet build ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Debug --artifacts-path TestResults/AngleUnits-mw6tzf8q/native-artifacts "-bl:TestResults/AngleUnits-mw6tzf8q/native-verified-build-{}.binlog"
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release --artifacts-path TestResults/AngleUnits-mw6tzf8q/native-artifacts -o TestResults/AngleUnits-mw6tzf8q/native-published "-bl:TestResults/AngleUnits-mw6tzf8q/native-verified-publish-{}.binlog"
```

Source Debug `native-debug-verified/` и published Release `native-release-verified/`
прошли с process exit0. Каждый профиль использовал новый disposable project,
сгенерированный `fixture/Fixture.csproj` + `fixture/Program.cs` из evidence через
SceneFileCodec; game compiler не запускался. Запуск:

```powershell
$env:VK_LAYER_PATH = (Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
dotnet TestResults/AngleUnits-mw6tzf8q/native-artifacts/bin/AetherEditorHost/debug/AetherEditorHost.dll --project TestResults/AngleUnits-mw6tzf8q/native-project-debug-verified/fixture.aetherproj --smoke fresh-source-directory --smoke-transform --validation
dotnet TestResults/AngleUnits-mw6tzf8q/native-published/AetherEditorHost.dll --project TestResults/AngleUnits-mw6tzf8q/native-project-release-verified/fixture.aetherproj --smoke fresh-published-directory --smoke-transform --validation
```

Native assertions подтверждают no-history unit switch, invalid angle admission,
exact9.000e1→rad→9.000e1 draft round trip,90°→π/2 canonical Apply, Undo/Redo,
SaveAs/Close/reopen с radians в файле и сохранённой presentation preference.
Actual preview frame/revision/pixel проверяет повёрнутый объект; checkbox/labels
и existing group reset/Cancel/scale linkage также проходят. По11PNG/10reaped child
processes, notifications только на UI owner; report.json: sessions/mappings/GPU
resources/GPU handles0, validation0errors/0warnings, disposed/reaped true.
Project cleanup Passed true. Native degree PNG просмотрен: label/checkmark/XYZ,
reset и scale link остаются в своих строках, clipping не обнаружен.

Сохранены неуспешные attempts, без заявления о production renderer/input fix:
`native-debug/` остановился на старом ожидаемом front-face pixel после +90° yaw.
Фактический #ff852119 соответствует left-face lighting0.65 из cube.vert; acceptance
теперь явно ожидает эту грань (133/33/25 вместо204/51/38 с прежней tolerance2).
Следующий `native-debug-final/` прошёл ещё до раскрытия popup correction.
`native-release/` получил3minute timeout на Preview popup wait до angle edit workflow.
Root cause этого единичного native focus/popup ожидания не установлен. Harness
теперь удерживает только disclosure, как существующий header helper; application
Start/Stop не повторяются. Await expressions и last sampled popup/update/draft/unit
state пишутся в progress.log для будущего failure evidence.
`native-release-acceptance/` затем остановился на status-bar hover line40 ещё до
Transform workflow. Он сохранён с exit1. StatusBarSmoke теперь повторяет directed
pointer move во время ожидания hover frame через существующий Header UntilAsync;
production status/input code не менялся, причина отдельных native interruptions
не объявлена установленной. Финальные source/
published profiles с этим harness прошли, первоначальные failure logs/exit1 сохранены.

| Требование | Прямое evidence: InspectorAngleUnitTests |
| --- | --- |
| Все оси, repeated switching, immutable canonical/dirty/history | UnitSwitch_PreservesCanonicalBitsAndHistory |
| Degree edits, exact text, Apply/Undo/Redo/save/reopen | DegreeDraft_CommitsRadiansAndRoundTripsExactText |
| Format equivalence, signed zero и invariant culture | EquivalentDegreeFormatting_DoesNotCreateDraftOrLoseCanonicalPrecision; SignedZeroAndInvariantCulture_SurvivePresentationAndCommit |
| Invalid/incomplete/pending reset и unrelated fields/Cancel | InvalidAngle_BlocksSwitchAndPreservesPendingResetText; UnrelatedDraftAndCancel_KeepUnitsAndExactRadianDraft |
| Busy/read-only/conflict/saving/missing Transform/selection | BusyOrConflictingState_RejectsUnitChange; SavingOrMissingTransform_DisablesUnitChangesWithoutLosingPreference |
| Group reset и unrelated groups/link preference | ValidPendingReset_CanSwitchWithoutChangingFlagsOrUnrelatedGroups |
| Float limits после conversion, оба знака | FloatLimits_AreAppliedToCanonicalRadians |
| Retained checkbox/labels/step/theme/resize/focus и rejected two-way setter | UnitControl_RetainsTreeFocusDraftAndTypedUnits |
| Capture/invalid switch/Esc exact rollback или one Apply | DegreeDrag_CancelsExactTextOrAppliesOneRadianTransaction |
| Degree step и Shift/Ctrl | DegreeArrowStep_UsesOneDegreeAndSupportsModifiers |
| Compatibility View canonical/display separation | LegacyView_UsesSharedDisplayWithoutChangingCanonicalContract |

Versioned история содержит дату/команды/outcomes/ограничения; ignored raw evidence
TRX/binlog/PNG/JSON/source SHA manifest остаётся дополнительным локальным материалом.
Новых full Aether/solution/Alloy/Hosting suites, fresh Editor nupkg и physical input
matrix запусков нет. ED4g.2 physical/release и независимые engine/Nom blockers
не закрыты. Следующий UI этап — ED5a, начиная с camera/projection и presented
revision/identity/selection contract; [единственный detailed task](EDITOR_PLAN.md#ed4g4-angle-units).


<a id="ed4g3-group-reset-2026-11-08"></a>
## ED4g.3: групповой сброс Transform — 2026-11-08

По запросу пользователя реализованы три retained reset icons24×24 у Position,
Rotation и Scale. Layout резервирует action width до responsive vector measure;
узкая панель сохраняет равные numeric columns, theme/resize — tree/focus/draft.
Tooltip500мс/accessibility и Tab + Enter/Space используют existing Button/input.
Новые `TransformGroup`, `ResetTransformGroup` и три cached admission flags живут
в Editor. Generic controls, engine/DI/lifetime/protocol не менялись.

Reset использует existing InspectorProperty semantics: local values/changed drafts
становятся pending reset, чистые inherited fields пропускаются. Apply удаляет
overrides/initial property values и проявляет inherited/schema defaults одной
authoring transaction. Cancel не меняет committed document. Linked Scale не
пересчитывает peers, preference и unrelated diagnostics/drafts сохраняются.
Stale baseline/conflict, missing selection/Transform, runtime/document busy и
active numeric gesture отклоняют действие. Late cancellation не отменяет новый reset.

Evidence: `TestResults/TransformGroupReset-mw6ss2ok/`. Свежий baseline Editor Debug
1300/1300; final Debug/Release1323/1323 каждый, failed0/skipped0, +23cases.
Focused Inspector173/173 был до последних двух drag/selection cases; это не final total.
Использован run-tests/code-testing workflow; TRX, unique binlogs и source SHA manifest
сохранены в evidence. Known CS1591 generated/XML и xUnit1031 diagnostics warnings
остаются; числовые tests/harness не расширяют production warning scope.

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --artifacts-path TestResults/TransformGroupReset-mw6ss2ok/artifacts --logger "trx;LogFileName=editor-final-debug.trx" --results-directory TestResults/TransformGroupReset-mw6ss2ok "-bl:TestResults/TransformGroupReset-mw6ss2ok/editor-final-debug-{}.binlog" -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --artifacts-path TestResults/TransformGroupReset-mw6ss2ok/artifacts --logger "trx;LogFileName=editor-final-release.trx" --results-directory TestResults/TransformGroupReset-mw6ss2ok "-bl:TestResults/TransformGroupReset-mw6ss2ok/editor-final-release-{}.binlog" -v quiet
dotnet build ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Debug --artifacts-path TestResults/TransformGroupReset-mw6ss2ok/native-artifacts "-bl:TestResults/TransformGroupReset-mw6ss2ok/native-build-{}.binlog"
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release -o TestResults/TransformGroupReset-mw6ss2ok/native-published --artifacts-path TestResults/TransformGroupReset-mw6ss2ok/native-artifacts "-bl:TestResults/TransformGroupReset-mw6ss2ok/native-publish-{}.binlog"
```

`fixture/Program.cs` и Fixture.csproj внутри evidence генерируют новый disposable
project и SceneFileCodec scene: inherited cube Position(-1.5,1,-1), Appearance(.8,.2,.15),
без instance overrides. Источник не перезаписывается. Native launch:

```powershell
$env:VK_LAYER_PATH = (Resolve-Path TestResults/EditorWindow-20261104/tools/validation).Path
dotnet TestResults/TransformGroupReset-mw6ss2ok/native-artifacts/bin/AetherEditorHost/debug/AetherEditorHost.dll --project TestResults/TransformGroupReset-mw6ss2ok/native-project/fixture.aetherproj --smoke fresh-source-directory --smoke-transform --validation
dotnet TestResults/TransformGroupReset-mw6ss2ok/native-published/AetherEditorHost.dll --project TestResults/TransformGroupReset-mw6ss2ok/native-project-release-final/fixture.aetherproj --smoke fresh-published-directory --smoke-transform --validation
```

Фактический source Debug запуск использовал apphost exe и сохранил `native-debug/`;
published Release через dotnet host — `native-release-final/`, process exit0.
Оба прошли staged invalid Rotation/linked Scale + Cancel, keyboard Position reset,
Apply одной history operation, Undo/Redo, actual preview revision/pixel checks,
SaveAs/Close/reopen с inherited provenance. По7PNG/7reaped preview children,
compiler не запускался, notifications только на owner thread. `report.json` обоих:
UI sessions/mappings/GPU resources/GPU handles0, reaped/disposed true,
Vulkan validation0errors/0warnings; project-cleanup Passed true.
Native PNG вручную просмотрен: action остаётся возле своей группы, pending reset
сохраняет исходный invalid string до Apply, preview показывает committed revision.

Сохранённые неуспешные native попытки не объявлены исправлениями:
`native-release/` переиспользовал Debug project; SaveAs штатно отклонён, так как
Edited.aetherscene уже существовал (exit1). `native-release-fresh/` остановился
на status-bar hover ожидании до запуска Transform workflow (exit1); причина
не установлена. В следующем отдельном profile использован новый project и обычный
dotnet host; он прошёл. Production status/input code по этим попыткам не менялся.

| Требование | Прямое evidence: InspectorTransformGroupResetTests, если не указан другой класс |
| --- | --- |
| Три группы, наследование, одна transaction, Undo/Redo, save/reopen | GroupReset_StagesOnlyItsGroupAndApplyUndoRedoSaveReopenPreserveInheritance (Position/Rotation/Scale) |
| Draft/Cancel и сохранение остальных групп | CancelGroupReset_PreservesCommittedDocumentAndOtherDraftUntilExplicitCancel |
| Pristine inherited/mixed/invalid и schema defaults local component | InheritedGroup_IsDisabledUntilDraftAndResetLeavesCleanPeersUntouched; LocalComponentGroupReset_UsesSchemaDefaultsAndKeepsOtherGroups |
| Linked Scale и unrelated error admission | LinkedScaleReset_ClearsInvalidRatioDraftAsOneBatchWithoutPropagatingValues; OtherGroupReset_PreservesLinkedScaleErrorAndBlocksWholeDraftCommit |
| Runtime/document busy, stale/conflict, missing selection | RuntimeOrDocumentBusy_GroupResetDoesNotAlterDraft; ExternalRevision_GroupResetRejectsStaleActionAndPreservesConflictingDraft; MissingTransformOrSelection_GroupResetCannotReusePreviousFields |
| Selection и active/late drag | SelectionChange_DiscardsPendingGroupResetAndRecomputesAdmissionForTheNewEntity; ActiveNumericGesture_DisablesGroupResetUntilCompletionOrCancellation; InspectorNumericGestureTests.GroupReset_DuringCapturedDragIsDisabledAndCannotBeOverwrittenByLateCancellation |
| Layout/keyboard/theme/resize/tree/focus и tooltip | GroupButtons_StayVisibleRetainControlsAndSupportKeyboardResetAcrossThemeAndResize; InspectorIconActionsTests.GroupResetTooltip_ExplainsInheritanceAndDisabledPendingResetWithoutStealingFocus |

Scope: full Editor suite и два passing native consumers в указанной Windows x64
конфигурации. Новых Alloy/Hosting/full Aether/solution/fresh Editor nupkg/physical
keyboard/pointer matrix запусков нет. ED4g.2 physical gate и engine/release blockers
не закрыты. Детальный task только в [EDITOR_PLAN](EDITOR_PLAN.md#ed4g3-group-reset).
Следующий UI этап — ED4g.4 angle units с явной invalid-draft policy, затем ED5a.


<a id="nom-own-store-2026-11-07"></a>
## Nom сменил целевой backend — 2026-11-07

По решению пользователя Nom полностью заменяет LiteDB собственной KV/CAS реализацией.
Единственная detailed очередь — [NOM0–NOM7](../nom/PLAN.md#nom-own-queue); код backend
ещё прежний. G.1 import/validation и g.2 consumer/build boundary сохранены; f.3→f.4
не переупорядочены, новых зависимостей/engine sources/build/test/GPU results нет.

<a id="nom-app-2026-11-07"></a>
## Nom App integration как consumer dependency — 2026-11-07

Независимый Nom добавил public UseNom/store/factory и App-owned retained drain;
57/57 D/R + solution discovery выполнены у Nom. Engine consumer bridge/dependencies,
decoder/GPU не менялись и не проверялись этим срезом. [Команда/scope](../nom/APP_INTEGRATION.md).
Очередь f.3→f.4 сохранена. Ниже прежняя storage-сводка из Engine STATUS (историческая):
**Storage2026-11-07:** независимый [Nom prototype](../nom/AST1B_STREAMING.md#ast1b-2026-11-07)
добавил streamed geometry/image/shader bytes, CAS/journal/atomic batch и process-kill
recovery;18groups каждый source Debug/published Release выполнены у Assets.
Engine importer/build остаются consumers; production engine integration, decoder/GPU
streaming этими проверками не подтверждены. Далее AST1b.f fault gates;
f.3→f.4 сохраняется, public module/package — AST2.
Переименование: research library **TrueMoon.Nom** и отдельный Probe теперь в общем
solution;18groups D/R повторены после переноса. [Scope](../nom/NOM_RENAME.md).
Следующий Nom-срез AST2a реализовал [UseNom/App API](../nom/APP_INTEGRATION.md):
57/57 D/R + solution discovery и retained storage drain. Это не engine bridge/GPU proof;
публичная пакетная поставка ещё AST2b. Engine dependency/code этим запросом не расширены.


<a id="nom-rename-2026-11-07"></a>
## Storage dependency получил имя TrueMoon.Nom — 2026-11-07

Библиотека TrueMoon.Nom и отдельный Probe добавлены в общий solution; storage docs
перенесены в docs/nom, engine references обновлены. Повторные18groups D/R принадлежат
Nom и не доказывают production engine/decoder/GPU integration. Engine code/dependencies
этим переименованием не менялись; f.3→f.4 и importer g.1 сохраняются.
[Scope и проверки](../nom/NOM_RENAME.md#nom-rename-2026-11-07).

<a id="asset-storage-streaming-2026-11-07"></a>
## Assets streaming proof: consumer dependency — 2026-11-07

Независимый [Assets AST1b core](../nom/AST1B_STREAMING.md#ast1b-2026-11-07) проверил
потоковые geometry/image/shader bytes, CAS/journal/atomic publication и process-kill
recovery в18groups каждый D/R. Это storage evidence, не engine import/decode/GPU
streaming или production bridge proof. Engine code/contracts/dependencies этим шагом
не изменены; f.3→f.4 и g.1 остаются в прежней очереди. Далее у Assets AST1b.f fault gates.

<a id="asset-storage-planning-2026-11-07"></a>
## Storage выделен в самостоятельный план для обсуждения — 2026-11-07

По пользовательскому требованию persistent asset storage проектируется вне движка.
[Assets AST0–AST3](../nom/PLAN.md) владеет catalog/blob/snapshot/backend decisions;
[предварительное исследование](../nom/RESEARCH.md) сравнивает LiteDB/SQLite/files.
Backend/package/API не утверждены, implementation/spike не выполнялись. G.1 importer
и g.2 delivery будут consumers storage contracts; rendering f.1/2 ownership/validation
сохраняется, существующие публичные типы не перемещаются. Текущая очередь f.3→f.4
и прежние captured-import результаты не изменены. Следующий storage шаг — обсуждение
authority IDs/metadata, portable records, process model и границ catalog/bytes/export.

<a id="captured-import-2026-11-07"></a>
## ARCH1g.0: captured I/O, limits, asset ID и native cancel — 2026-11-07

По запросу продолжения исследования реализован [captured import v1](MODEL_IMPORT_CAPTURE.md#capture-v1)
в existing ManualTests/ModelImportResearch, без production package/App registrations.
Captured map копирует root/dependency bytes, canonicalizes paths, rejects collisions/
unsafe names и передаётся native parser через собственный bounded Cdecl aiFileIO.
Missing captured dependency запрещает публикацию даже при permissive OBJ parser;
fallback к live disk отсутствует. SHA256 включает versioned recipe/root/sorted closure.
Заданы numeric limits до file/callback/projection allocations. Selected low-level
projection использует реальные MeshDefinition/RenderContentCodec validation.

Persistent semantic-ID ledger сохраняет IDs при order/content changes, explicit
rename/restore; deletes оставляют tombstones. Ambiguity/collision и stale/foreign-source
completion отклоняются; reconciliation immutable, publication под generation gate.
Locators/dependencies пока задаются явно, production native discovery/persistence
и artifact+ledger transaction не заявлены готовыми.

Windows x64 worker ждёт parent admission до native activation; Job512MiB committed
memory/active process1/kill-on-close проверяется query и actual VirtualAlloc denial.
Cancel/deadline внутри заблокированного native read callback убивает own Job, ждёт
actual exit, drains bounded stderr, затем освобождает process/job/capture и capacity.
Failed cleanup сохраняет owner/capture и занятую capacity; искусственный OS cleanup
failure не воспроизводился. Профиль1active/0queued, finite import deadline≤30s.

```powershell
& ./ManualTests/ModelImportResearch/Verify.ps1 -Captured -EvidenceDirectory TestResults/CapturedImport-20261107/Final
& ./ManualTests/ModelImportResearch/Verify.ps1 -EvidenceDirectory TestResults/CapturedImport-20261107/ComparisonRegression
dotnet build TrueMoon.slnx -c Debug --no-incremental '-bl:TestResults/CapturedImport-20261107/Solution-Debug-{}.binlog'
```

Final: **source Debug + framework-dependent published Release2/2profiles**,6commands
exit0/3unique binlogs,14frozen source hashes/10fixture hashes, source drift0. Actual
output-local Netter native6.0.5/SHA90984E88…/flags17 совпадают с pinned archive.
Каждый профиль — **8/8групп,9workers started/reaped, LiveImports0**:
CapturePathsAndPacket, CaptureBudgetBoundaries, CallbackReadSeekAndQuotas,
CapturedOnlyAndMutation, NativeProjectionBudgets, AssetIdentityReimport,
WorkerOutputBoundaries, WorkerCancellationAndPublication.

Проверены literal geometry/slots/transforms/used closure GLB/glTF+BIN/OBJ+MTL/grid;
изменение+удаление source/dependencies после capture, dependency есть на диске, но
не в capture, corrupt/truncated/trailing packets. Before/at/over limits, fread partial
EOF/size_t overflow/seek/write/open/dispose, native mesh/face/node/depth/instance/
output rejection с ReleaseImport, bounded stdout/stderr. IDs roundtrip/order/content/
add/delete/rename/restore/mapping collisions/escaped-Unicode ledger. Cancel before
capture/admission, startup failure/recovery, actual Job memory commit denial,
3native-read cancels, simultaneous admission rejection, native-read deadline, corrupt
input, multi-file recovery, post-parse pre-publication cancel и stale/foreign-source
publication. Все packet files удалены после confirmed reap.

Independent test-gap/assertion review выявил и помог закрыть: unstarted Process cleanup
ghost owner, unbounded diagnostic reads, ledger escaped-JSON roundtrip bound, Faces/
Depth/mapping/capacity gaps. Initial Attempt1 исправил compile errors нового harness;
Attempt2/3 дали access violation из-за ошибочной передачи fileIO в property-store
трёхаргументной перегрузки Netter. Исправлена четырёхаргументная перегрузка; callback
trace и failed logs сохранены, gates не ослаблены. Attempt4/5 passing предшествуют
финальным review cases и не подменяют Final.

ComparisonRegression: **8/8profiles**,21commands exit0/12binlogs, frozen source drift0;
прежний four-variant geometry/delivery/repeatability corpus сохранён. Это regression,
нового сравнительного performance вывода для captured path нет. Общий solution
nonincremental Debug:0errors/1555warnings; full unit suite не запускалась. Прежние
full-Aether diagnostics Stop/Dispose timeout failures не объявлены исправленными.

Raw evidence/logs/binlogs остаются ignored TestResults/CapturedImport-20261107;
повторяемый runner и контракт содержатся в исходниках. Per-file trusted capture не
равен race-hardened/atomic directory capture; Job — resource containment, не native
security sandbox. App/IFactory integration, automatic dependency/semantic-ID discovery,
полный format/coordinate/unit/material/unsupported policy, artifacts/Runtime/Editor/
fresh production importer package/GPU/all-RID требуют g.1/2. G.0 частичен; f.3→f.4 и
audio h.0 сохранены. Production C#/csproj/CPM/native assets не менялись.

<a id="assimp-selection-2026-11-07"></a>
## ARCH1g.0: comparative import spike и выбор AssimpNetter — 2026-11-07

По запросу продолжить исследование и выбрать кандидата добавлен headless
[ManualTests/ModelImportResearch](../../ManualTests/ModelImportResearch/README.md).
Исследованы Netter6.0.5 high-level, Netter6.0.5 low-level, Silk2.23.0/Ultz6.0.2 и
SharpGLTF.Core1.0.7 на одном созданном нами corpus: GLB, glTF+BIN, OBJ+MTL, minimal
ASCII FBX, grid16384vertices/96774indices и3negative inputs. Оба Assimp providers
прошли multi-format cases, SharpGLTF явно пропускает OBJ/FBX. Проверены2material
assignments/4shared instances/nonzero parent-child transforms, full grid topology/
winding/bounds, real MeshDefinition/RenderContentCodec и11repeat fingerprints.

FinalReusable: **8/8profiles** Debug/published Release,21commands exit0,12unique
binlogs. Candidate projects/caches/feed создаются вне checkout, Aether — source
ProjectReference для CPU contracts. Pinned NuGet archives скачиваются с SHA gates,
raw inventory прошлой фазы не требуется; missing-archive bootstrap также проверен.
Actual native6.0.5(Netter)/6.0.2(Ultz), compile flags17/single precision/output-local
DLL SHA matching archive/source/publish; tracked LiveImports0. Native scenes освобождаются
в finally после detached copy. Это не полный native allocator leak/peak memory proof.

**Решение: AssimpNetter6.0.5 low-level для g.1 Editor/build importer.** На последнем
Release grid workload full import+CPU projection: high-level7.3421ms/12302752managed bytes,
low-level4.2793ms/4672008bytes, Silk4.9298ms/4675784bytes, SharpGLTF7.6702ms/5910880bytes.
Это7warm samples/single local run, не parser-only measurement/SLA; process peak working
set включает CLR/DLL/предыдущие fixtures. Выбор опирается на fidelity/delivery и отсутствие
промежуточной managed scene при native6.0.5, не на небольшой timing выигрыш.
[Полная таблица, ограничения и решение](MODEL_IMPORT_RESEARCH.md#decision-2026-11-07).

```powershell
& ./ManualTests/ModelImportResearch/Verify.ps1 -EvidenceDirectory TestResults/ModelImportSpike-20261107/FinalReusable -ArchiveDirectory TestResults/ModelImportSpike-20261107/no-initial-archives
```

Initial Attempt1 исправил M-prefixed field names в новом Silk probe; assertion/version
gates не ослаблялись. Далее добавлены low-level Netter, independent grid topology,
native version/SHA gates и reproducible archive bootstrap; failed evidence сохранено.
Final raw artifacts/source/fixture hashes — ignored TestResults/ModelImportSpike-20261107/
FinalReusable.8compiled research source hashes и10fixture hashes зафиксированы.
Production C#/project/CPM/native assets и solution не менялись. Full solution/unit tests,
GPU renderer, all-RID, native in-flight cancel и production package proof не выполнялись.
G.0 частичен до captured I/O/stable IDs/numeric budgets/cancel/isolation decisions;
G.1 production adapter не реализован. Audio h.0 и кодовая очередь f.3 сохранены.

<a id="assimp-research-2026-11-07"></a>
## ARCH1g.0: первичная оценка Assimp и .NET packages — 2026-11-07

По запросу проверить Assimp прочитаны official upstream/header/license/loader sources
и NuGet metadata. [Исследование с источниками и package SHA](MODEL_IMPORT_RESEARCH.md)
сравнивает Silk.NET.Assimp2.23.0+Ultz.Native.Assimp6.0.2, AssimpNetter6.0.5,
legacy AssimpNet4.1.0/5.0.0-beta1 и managed SharpGLTF.Core1.0.7 для glTF-only control.
Четыре official nupkg скачаны в ignored TestResults/AssimpResearch-20261107;
проверены ZIP entries/nuspec/dependencies/RIDs/licenses/hashes без установки или DLL load.
Win-x64 native assets есть у обоих Assimp paths. Compile support/net10 runtime delivery,
actual native ABI/version и format/data fidelity этим не доказаны.

Вывод-предложение: Assimp — приоритетный multi-format importer для Editor/build stage,
Runtime получает canonical engine artifact/f.2 leases. Binding не выбран: требуется
одинаковый fixture spike для native delivery, copy/release, dependency capture,
hierarchy/slots/units и peak memory. Legacy AssimpNet не первый кандидат по опубликованным
датам пакета; SharpGLTF — отдельный glTF-focused вариант. Обнаруженные source-provenance
вопросы Ultz и loader alias5/6 записаны как открытые, без утверждения native mismatch.
G.0 остаётся частичным: numeric budgets, identity/reimport и final decision ещё нужны.
G.1/native importer не реализован; audio h.0 не исследовался этим срезом.
C#/project/package versions/App/native assets не менялись. Новых build/test/native/
performance результатов нет; прежние исследовательские и ECS записи сохранены.

<a id="model-audio-planning-2026-11-07"></a>
## Исследования model assets и аудиомодуля — 2026-11-07

По запросу добавить направления в планы заведены [ARCH1g.0–2](PLAN.md#arch1g-model-assets)
и [ARCH1h.0–2](PLAN.md#arch1h-audio). G.0 сравнивает форматы/importers, canonical CPU
conversion, identities/dependencies/captured inputs и artifact delivery; далее bounded
CPU prototype и Runtime/render integration после нужных f.3/4 capabilities. H.0 выбирает
audio module/backend/codec scope, App/IFactory/ECS boundaries, real-time queues и
clip/stream/device ownership; далее offline reference vectors и actual playback proofs.

Результаты исследований — decision records, матрицы вариантов/лицензий, explicit
supported/unsupported scope, численные budgets, fixtures и планы прототипов. glTF/GLB,
source/listener и audio package названы кандидатами/ролями, public API не утверждён.
Текущий f.2 builtin/in-memory content не объявлен file importer; renderer types не
распространяются автоматически на audio. Очередь f.3→f.4 сохранена, исследования
доступны независимо. C#/dependencies/native и игровые assets не менялись; проверялись
только diff, task IDs и ссылки. Новых build/test/native/performance результатов нет.

<a id="ecs-commit-2026-11-07"></a>
## ECS и resource contracts: проверка перед коммитом0.2.5.1-alpha — 2026-11-07

По запросу закоммитить оставшийся ECS блок собраны ранее реализованные ARCH1e.1–4,
f.0–2, registry/initial Runtime scene loading, hierarchy/primitive extraction и
resource owners/leases/preparation с tests/ManualTests и сопутствующими PLAN/STATUS/
HISTORY. Directory.Build.props фиксирует выбранную пользователем общую версию
0.2.5.1-alpha и снятие explicit TieredCompilationQuickJit/TieredPGO overrides;
строки/пробелы нормализованы без изменения остальных build settings.

Новые проверки именно с этой конфигурацией: ECS/Rendering/old-frame fixture
**1230/1230 каждый Debug/Release**, failed/skipped0, exit0. Scope834ECS+395Rendering
(включая137Resources)+frame1. Solution nonincremental Debug build exit0,
0errors/1555warnings;1422normalized distinct warning messages/files, delta0 к
предыдущему Skia4.155 build. Сохранённые native/package результаты f.2 относятся
к предыдущему запуску, в этом commit-срезе не повторялись. Full Aether/solution tests
и performance не запускались; прежние diagnostics timeouts остаются открытыми.

```powershell
foreach($config in @('Debug','Release')) {
  dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c $config --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger "trx;LogFileName=ecs-render-$config.trx" --results-directory "TestResults/EcsCommit-20261107/$config" "-bl:TestResults/EcsCommit-20261107/ecs-render-$config-{}.binlog"
}
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsCommit-20261107/solution-Debug-{}.binlog'
```

Raw logs/TRX/unique binlogs и precommit inventory — ignored TestResults/EcsCommit-20261107;
native Skia update уже в предыдущем коммите84a4985. Local .codex и ignored build/source/
evidence artifacts не входят в ECS commit. Следующий engine этап остаётся ARCH1f.3.

<a id="render-resources-2026-11-07"></a>
## ARCH1f.2: resource owners, leases и preparation — 2026-11-07

По запросу «Переходи к реализации ARCH1f.2: владельцев ресурсов, leases и подготовки»
реализован [resource API](RENDER_RESOURCES.md) поверх frozen CPU definitions f.1.
UseAetherRenderResources регистрирует App-owned RenderResourceHost/catalog и typed
non-owning factory wrappers; custom backend factory сохраняется. Caller owns device,
preparation и World binding; Host держит fallback ownership до successful release.
Startup CPU-only/lazy, DI aliases не удваивают disposable ownership. Cobalt и existing
Microsoft DI adapter проверены отдельно; новые services не используют service locator.

Catalog считает exact revisions, shared physical definition arrays, frozen scopes и
exclusive CPU lease wrappers. Одинаковые keys канонизируются на retained definition;
last release удаляет metadata/buffers из catalog accounting. RenderResourceComponents
использует прежние schemas/converters и scene-owned import retains; CPU validation
остаётся до World allocation, scene loader не выполняет GPU work. World values не
получили services/managed leases/native handles. Из существующего C# изменена только
доступность двух conversion helpers private→internal для reuse без изменения поведения.

Device owner pinning включает catalog/session/world/project/build/generation;
handles — owner/device/slot generations, без native pointer. Shared upload имеет
отдельный cancellation owner; отмена одного preparation не ломает соседний lease.
Bounded active4/queued16 jobs, entry/byte reservations и last-use retirement учитывают
pending/current/candidate/failed cleanup. Unknown outcome закрывает admission и
сохраняет owners/bytes до подтверждённого завершения; отмена Stop wait не отменяет drain.
Partial-creation cleanup owner и original error остаются observable/retryable.

World binding готовит весь candidate до публикации, затем сверяет captured membership
и component values. Rebind меняет pinned keys всех renderers, сохраняя tint/masks;
invalid/failed/cancelled candidate не меняет current. Cleanup error старого после commit
возвращается вместе с Committed=true, old owner retained. Host stop сразу закрывает
CPU/device admission, затем bindings/preparations→devices/backend→catalog. Live CPU
leases и failed cleanup не считаются освобождёнными по timeout.

Дополнительные metadata ceilings выбраны явно:64CPU scopes/16384lease wrappers/
2048slot records, host1device/1required World binding, binding4096renderers. Следующий
renderer отвергается до backend activation/CPU retain, без truncation. Retired exhausted
slot records входят в тот же cap. Geometry64MiB, upload/CPU staging16MiB каждый;
native allocations резервируются по actual memory requirements до VkAllocateMemory.
Metadata/driver/surface RAM/VRAM не объявлены byte quota. Options copied; только lower
limits, byte budgets0 допустимы. Scene/wire/checkpoint/primitive limits не увеличены.

Default Vulkan backend владеет отдельным lazy instance/device/graphics queue/command
pool, upload owners заимствуют context под NativeGate. Packed mesh upload использует
device-local geometry и host-visible/coherent staging, transfer barriers и actual fence
completion. Optional verification делает GPU readback и SHA256 comparison; staging/CPU
bytes освобождаются до ready, geometry — после final preparation lease. Unknown submit
cleanup требует confirmed idle, иначе owner retained. Callback ABI использует existing
Silk declarations и rooted GCHandle до teardown, exceptions не уходят через native ABI.
Surface adapter matching/shared device, command submission для frames и Playing fence
ещё f.3/4. Native context здесь не создаёт window/shader/pipeline/draw/external handle.

Новые73methods/137cases в8test classes+fixture (9files). Final Debug и Release:
**1230passed/0failed/0skipped каждый**, exit0. Scope=137new+1093прежних ECS/Rendering/
old frame cases. В каждом TRX834ECS,395Rendering (включая137Resources), frame1;
case sets D/R совпадают. Последний review добавил3cases о4096/4097renderer и одном
host binding; все вошли в оба final runs, package/native artifacts пересобраны после них.

| Запрос пользователя | Exact direct evidence |
| --- | --- |
| «владельцев ресурсов» | AppShutdown_ReleasesOutstandingFactoryProductsBeforeCatalogExactlyOnce; BackendFactoryPartialCreation_RetainsFailedCleanupOwnerUntilRetry; CleanupFailure_RetainsOnlyFailedEntryAndBytesAndRetriesWithoutRepeatingSuccessfulStage |
| «leases» | ExactKey_IsCanonicalizedAcrossScopesUntilTheFinalLeaseReleases; SceneWorld_OwnsDistinctCpuRetainsAfterRegistryRootLeaseReleases; SharedMesh_UsesOneUploadAndWaitsForLastPreparationRelease |
| «подготовки» | PendingPreparations_ShareOneReadinessFenceAndPublishOnlyAfterUploadCompletion; PreparationLimits_AdmitFourActiveSixteenQueuedAndRejectNextBeforeRetain; InitializeAndReload_PublishOnlyAfterReadyAndAtomicallyRebindEveryRendererWithoutChangingOtherState |
| Запуск проверок | Два успешных dotnet test commands ниже, TRX1230/1230 каждый; source/published/fresh CPU и Vulkan runners exit0; solution build exit0 |

Повторяемые команды из src (PowerShell):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-resources-final-Debug.trx' --results-directory TestResults/RenderResources-20261107/Debug '-bl:TestResults/RenderResources-20261107/ecs-resources-final-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-resources-final-Release.trx' --results-directory TestResults/RenderResources-20261107/Release '-bl:TestResults/RenderResources-20261107/ecs-resources-final-Release-{}.binlog'
& ./ManualTests/RenderResources/Verify.ps1 -EvidenceDirectory TestResults/RenderResources-20261107/Consumers-final
& ./ManualTests/RenderResources/VerifyNative.ps1 -ConsumerEvidenceDirectory TestResults/RenderResources-20261107/Consumers-final -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -EvidenceDirectory TestResults/RenderResources-20261107/Native-final
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/RenderResources-20261107/solution-Debug-{}.binlog'
```

CPU/public API consumer compiles unchanged Program.cs в source Debug, published
Release и fresh isolated NuGet Release:3entities/6components/3assets,2fresh resource
generations, shared upload/lease, full World rebind, independent tint, stale handle
rejection, zero geometry budget и actual tracked cleanup. CPU mode injects deterministic
backend и не доказывает GPU work. Новые7local framework packages, empty isolated cache,
zero ProjectReferences в fresh graph, exact cached external nupkg/offline feed.
SDK fixture builds/offline restores получили разрешённое чтение user NuGet.Config;
global settings/dependency versions/project files не менялись. SDK10.0.403/net10.0/
xUnit2.9.3/VSTest. Последний consumer version0.2.2.7-renderresources.20261107013551.

Отдельный native runner запускает те же frozen3artifacts с actual Vulkan backend:
NVIDIA GeForce RTX5070Ti, core+synchronization validation. Каждый profile использует
2fresh App/World/device generations с2uploads+2verified readbacks на generation;
device1/1, buffers4/4, allocations4/4 и live uploads/CPU catalog/lease counters0 после
cleanup. Третий App с geometry budget0 получает LimitExceeded до VkAllocateMemory:
buffers2/2, allocations0/0, uploads0, owners released. Во всех3profiles и denial
**validation errors0/warnings0**, включая teardown. Старые handles отвергаются после
reload и другой generation. Existing validation JSON/DLL хэшированы, VK_LAYER_PATH
process-local; инструменты/driver settings не устанавливались и не изменялись.
Это upload/readback/ownership proof, не mesh pixels/frames/Editor Play/device loss.

Solution nonincremental Debug build exit0,0errors/1555warnings;1422normalized distinct
warning messages/files совпадают с f.1, delta0. Initial test helper compile error
(generic IFactory.Create) исправлен в новом fixture. Первый executed snapshot131/132
имел race исключительно в fake CancellationObserved marker: callback мог удаляться
после другого cancellation continuation. Fake теперь фиксирует actual token-cancelled
OCE; assertions ownership не ослаблены. Следующие132/132,1227/1227 и final1230/1230
passed. Production review исправил queued-cancel capacity, initial CPU lease release,
operation cleanup publication timing, changed-pinned-key Conflict ordering и закрытие
CPU admission в начале host stop. Failed artifacts сохранены, replay не заменял fixes.

Static assertion/gap review Strong:819physical Assert sites, public lifecycle/identity/
byte-count partitions и exact before/at/after limits. Measured coverage, empirical mutation
и performance SLA не заявлены. Единственный mandatory Roslyn pairing attempt failed
до parser на protected NuGet.Config; без retry/install/manual pairing success claim.
Research/plan/status/reviews/source hashes находятся в non-stageable TEMP.

Frozen25addressed C# files:14newproduction,9newtest/helper,1newmanual consumer и1existing
helper accessibility edit. Raw logs/TRX/unique binlogs/failed attempts/frozen hashes/
packages/native validation records — ignored TestResults/RenderResources-20261107;
versioned команды/outcomes/границы сохранены здесь, ignored artifacts не являются
единственной записью. Final audits:1571прежний файл побайтно сохранён,11bounded
existing edits (10docs+helper),28newfiles. Все7package DLL совпадают с current Release,
packaged Aether README — с source; old projects/dependencies/protocol/shaders сохранены.
Docs37Markdown/1916valid local links, новых link failures0;247прежних distinct issues
сохранены отдельно,7STATUS archives verbatim. Git diff --check passed.
Local .codex не редактировался и исключён из inventory по исходному правилу.

Full Aether в этой фазе не запускался: прежний2536/2538 с diagnostics Stop/Dispose
timeouts не объявлен исправленным. Performance, surface/frame/native pixels, UI/Editor
и полный solution test run также не запускались. Следующий этап ARCH1f.3 — owned
camera/mesh/material snapshots с revision/mask/bounds и drop/coalesce/submitted leases;
f.4 — surface/device/frame integration и required-view Runtime Playing readiness.
External files/PBR/checkpoint e.5/f.6/ED4i остаются отдельными задачами.

<a id="render-cpu-2026-11-07"></a>
## ARCH1f.1: CPU types, schemas и validation — 2026-11-07

По запросу «Переходи к реализации ARCH1f.1: типов данных, schemas и CPU validation»
реализован [CPU v1](RENDERING_CONTRACTS.md#cpu-v1) выбранного opaque-unlit profile.
RenderContentHash/RenderAssetKey unmanaged; definitions immutable, входные коллекции
копируются после count admission. Mesh проверяет topology/layout/UInt32 indices,
finite coordinates/bounds и точное покрытие ordered slot ranges. Binding set pins
mesh/material keys; manifest проверяет kind/project/version/revision/closure, не
выбирая latest. Unknown versions/invalid/missing/duplicate references дают явные errors.
Builtin cube8vertices/36indices, white material и default binding-set имеют stable IDs.

Canonical TMRA codec использует RFC4122 GUID bytes, little-endian scalars и positive
zero. Decode проверяет limits/shape/semantic data/exact canonical encoding; expected
revision проверяет manifest, inline checksum не придуман. TMRM — fingerprint sorted
exact keys/project/build без process-local generation, не новый control transport.
Material хранит sRGB base и decoded linear value; independent instance tint/reset
не меняет shared content. Bounds учитывают8corners/shear; PerspectiveCamera проверяет
явный rigid pose, projection/extents и finite result.

MeshRenderingComponents.Register добавляет только две exact singleton schemas с
required Transform; primitive schemas/IDs остаются прежними. Helper удерживает один
immutable CPU manifest и явно ставит builder AssetValidator; другие asset domains
требуют explicit composition. Import validation идёт до World allocation, включая
disabled/mask0/tintoff, double domain до Single и float domain после conversion.
Appearance+MeshRenderer ambiguity отвергается. Никакого global latest/service locator
в importer. Existing App/IFactory path создаёт caller-owned SceneWorld.

Новые94methods/309cases в9test classes+fixture (10files). Current final Debug и Release:
**1093passed/0failed/0skipped каждый**, exit0. Scope=309new+750oldECS+33oldRendering+
old frame fixture1. All834ECS и258Rendering cases присутствуют в каждом TRX, frame1
отдельно; D/R case sets совпадают. Первый narrow Debug336/336 включал303new+33old;
после static gap review добавлены6meaningful cases, вошедшие в оба final runs.
Новых test failures/production fixes по unit execution не потребовалось.

| Запрос пользователя | Exact direct evidence |
| --- | --- |
| «типов данных» | Key_EqualitySeparatesEveryPinnedDimensionAndValuesEnterWorldWithoutManagedReferences; Constructor_CopiesAllInputsAndPreservesCompleteOrderedGeometry; Bindings_CopyOrderedEntriesAndAllowTheSameMaterialForDistinctSlots; Manifest_SnapshotsAndSortsCompleteClosureAndMatchesIndependentFingerprint |
| «schemas» | Schemas_UseReservedIdsExactDefaultsAndRequiredTransformWithoutChangingLegacyCatalog; Load_DefaultsProducePinnedUnmanagedComponentsWithoutNativeOrDerivedRoles; Load_ResolvesInheritedAndInstanceOverridesWithUInt32BoundaryAndLinearTintIsolation; Register_CapturesEachFrozenManifestAndNeverReadsAnotherRegistrysLatestRevision |
| «CPU validation» | Constructor_RejectsInvalidOrIncompleteOrderedSlotPartitions; Transform_PreservesFullShearExtremaFromAllEightCorners; EffectiveLinearColor_AndEncodeMatchIndependentGrayTransferVectors; ValidateAgainst_RequiresExactSlotSetAndItsOrder; Manifest_RejectsIncompleteOrIncompatiblePinnedDependencyClosure; EncodeDecode_MaterialMatchesIndependentFortyFourByteGoldenPayloadAndRevision; Load_RejectsCameraRangesBeforeAndAfterSingleConversion; CreateViewProjection_ChecksEachRigidBasisTermIndependentlyOfDeterminant |
| Запуск проверок | Два успешных dotnet test commands ниже и final TRX1093/1093 каждый; solution build exit0 |

Повторяемые команды из src (PowerShell):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-render-Debug.trx' --results-directory TestResults/RenderingCpu-20261106/Debug '-bl:TestResults/RenderingCpu-20261106/ecs-render-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName~TrueMoon.Aether.Tests.Rendering.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-render-Release.trx' --results-directory TestResults/RenderingCpu-20261106/Release '-bl:TestResults/RenderingCpu-20261106/ecs-render-Release-{}.binlog'
& ./ManualTests/RenderingCpu/Verify.ps1 -EvidenceDirectory TestResults/RenderingCpu-20261106/Consumers-final
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/RenderingCpu-20261106/solution-Debug-{}.binlog'
```

Source Debug/published Release/fresh isolated NuGet Release CPU consumers прошли:
3entities/6source components/3assets, literal material bytes/SHA256, cube byte counts,
manifest digest/order/generation, App/Cobalt SceneWorld factory, shared bindings и
independent gray tint/reset, literal shear bounds/explicit perspective. Все три profiles
имеют одинаковый manifest digest06ab027aa3d4d7187285bf1ac9fedd2fca4626218f89b57e23d14988b4a88401.
Новые7local framework packages, zero ProjectReferences в fresh graph, empty isolated
cache и exact external nupkg из existing cache/offline feed. Никакого GPU/window/native
activation. Первый manual consumer build нашёл2ошибки примера (App.Builder callback и
using Dependencies для Resolve); исправлены только в новом Program.cs. Неуспешный
artifact set сохранён; final runner использует новый независимый scratch/evidence.

Solution nonincremental Debug build exit0,0errors/1555warnings;1422normalized distinct
warning messages/files совпадают с previous e.4 build, delta0. API Debug build также
exit0,0errors/322existing warnings. SDK10.0.403/net10.0/xUnit2.9.3/VSTest. Fixture SDK
builds/offline consumer restore получили разрешённый read доступ к user NuGet.Config;
глобальные настройки/версии/dependencies не менялись.

Static assertion-quality/test-gap review Strong:401physical Assert sites, direct public
partitions/invariants, no skipped/assertion-free cases. Уточнены независимые camera Gram
guards, nearest-valid numeric conversions, RFC GUID sorting и nonzeroZ/conservative
bounds canonical bytes. Measured coverage/empirical mutation/performance SLA не заявлены.
IDE findTests dispatcher отсутствовал; единственный Roslyn pairing attempt failed до
parser на protected NuGet.Config, без retry/install/manual-pairing success claim.
Research/plan/status/reviews/hashes — non-stageable TEMP.

Frozen28new C# files:17production/10test+helper/1manual consumer; existing C# не менялся.
Raw logs/TRX/unique binlogs/failed attempt/consumer packages/source hashes сохранены
в ignored TestResults/RenderingCpu-20261106 (начало работы6ноября, execution7ноября).
Durable commands/outcomes/границы сохранены здесь. Final audits:1542прежних файла
побайтно сохранены, existing C#/project/dependency/native/protocol edits0. Все7новых
package DLL hashes совпадают с current Release, packaged Aether README совпадает
с final source. Docs35Markdown/1896valid local links, новых link failures0,
7STATUS archives verbatim;247прежнихdistinct link issues сохранены отдельно.
Git diff --check passed. Full Aether и native/GPU suites в этой фазе не запускались;
прежний full2536/2538 с diagnostics Stop/Dispose timeouts не объявлен исправленным.

Следующий этап — ARCH1f.2: реальные App-owned content/device owners, counted leases,
bounded preparation/upload, async render readiness и drain/reload/failed cleanup.
Pure CPU manifest не подменяет этот service. Native mesh output/owned extraction,
Editor Play/preview, checkpoint/Replace и общий release acceptance впереди.

<a id="render-contracts-2026-11-06"></a>
## ARCH1f.0: mesh/material/resource contracts до кода — 2026-11-06

По запросу перейти к ARCH1f.0 выбраны [opaque-unlit/1 contracts](RENDERING_CONTRACTS.md).
Reserved type IDs1101/1102, asset kinds1201–1203 и builtin family/assets/slot1301/1401–4
не конфликтуют с текущими1001–4. Definitions/value keys/manifest отделены от services/
leases/native handles. Mesh — P3f32/UInt32 triangle list с exact ordered slots/ranges;
binding set pins mesh/material revisions, missing/extra/duplicate slots дают ошибку.
Instance tint один на renderer, linear multiplier, не shared asset mutation.
Base stored-sRGB decode→linear tint→explicit encode в UNORM; gray reference vectors
отличают linear multiplication от double/no conversion. Perspective camera выбирается
явно, pose rigid; bounds учитывают shear.

Решения относительно первого общего design уточнены явно: strict CPU content готов
до World activation, GPU upload/ready bindings — до required-view render-ready/Playing.
Current synchronous SceneWorldLoader и IDisposable PrepareSceneWorld не получают
скрытое ожидание GPU. Async preparation/drain принадлежит render host. Первое content
delivery — builtin/typed in-memory data pinned build, parent Game CLR не загружает;
external files/streaming/new content transport отдельно. Scene/control budgets сохранены.

Count/byte budgets включают старую/candidate/retiring revisions; failed reload preflight
сохраняет old current, explicit rebind меняет pinned keys/manifest вместе. Snapshot
current/pending/recording/submitted/candidate leases bounded; submitted owner создаётся
до возможного неизвестного submit outcome. Producer-only mesh use и shared target
GL-return retirement имеют разные completion dependencies; timeout не освобождает
неподтверждённые resources. Stop/drain/failed cleanup retry и App/IFactory ownership
зафиксированы. Новый renderer/capability/API этими решениями не реализованы.

Прочитаны текущие BuiltInComponents/PropertyValue/ComponentReference/load contexts,
SceneWorldGameSession, PrimitiveScene/RenderPrimitive, GPU frame/offer/surface ledger,
VulkanPrimitiveRenderer и GpuResourceHost. Math/format references сверены с первичными
W3C/Khronos страницами, ссылки сохранены в contract. C# source/project/package/native
shaders не менялись. Build/tests/native и эмпирические performance/coverage/mutation
в этом запуске не выполнялись; e.4 test counts и diagnostics failures ниже исторические.

Текущие проверки:7existing docs+1new contract,1544остальных pre-task files побайтно
сохранены;10reserved GUIDs unique и не используются в C#;5color reference vectors
сверены с transfer formulas, mesh/cube/snapshot byte arithmetic согласована.
Общий docs audit:34Markdown/1882valid local links, новых link failures0,
7STATUS archives сохранены verbatim;247прежнихdistinct link issues не исправлялись.
Git diff --check прошёл. Эти checks не являются unit/native/performance proof.

Команды документационных проверок из src:

```powershell
& ./TestResults/RenderingContracts-20261106/VerifyContracts.ps1
& ./TestResults/RenderingContracts-20261106/AuditDocs.ps1
git -c core.safecrlf=false diff --check
```

Baseline/hashes/audit scripts и расчетные vectors сохраняются в ignored
TestResults/RenderingContracts-20261106; durable outcome/границы записаны здесь.
Следующий этап — f.1 CPU contracts/canonical digest/schema validation, затем f.2
catalog/leases/preparation и f.3/4 extraction/native consumers. E.5/f.6 checkpoint,
ED4i inspector и прежние release/reliability/input blockers сохраняются.

<a id="ecs-primitives-2026-11-06"></a>
## ARCH1e.4: Transform, movement и primitive extraction — 2026-11-06

По запросу «давай следующий шаг плана» реализован [primitive ECS profile](ECS_PRIMITIVES.md).
Existing BuiltIn Transform/Appearance/Interaction v1 IDs/defaults/dependencies не менялись;
explicit PrimitiveEcsComponents.Register добавляет bindings и optional Velocity schema1004.
LocalTransform — parent-space position/unit quaternion/positive scale. Iterative graph
без recursion считает S*R*T и local*parentWorld; shear не decomposed/не теряется.
Identity groups/Parent/World cache roles готовятся до game activation. Union roles
должен быть complete; self/cycle/dangling/foreign/stale parents и invalid values
отвергаются до cache commit. Destroy не делает implicit cascade/reparent.

Movement preflights double-intermediate displacement; ordered scheduler выполняет
Movement→Transform→Extraction. Cache/frame publication — после complete preflight/
tick, cancelled/invalid preparation сохраняет прежние caches/snapshot. Это не rollback
всех фаз tick: предыдущая Movement phase может уже быть применена до Transform fault.
Snapshot содержит World/scene/revision/tick и detached immutable values, без World/
provider/native handles. Limit64 относится к enabled drawables, не logical groups;
65th drawable отвергает весь frame без silent truncation. Camera — fixed view config.

UseAetherPrimitiveEcs registers copied options/typed factories; Runtime lazy defaults
preserve overrides. SceneWorldGameSession.PrepareSceneWorld готовит controller на
private candidate перед final token/publication. Base owns returned IDisposable,
retains SceneWorldPreparationException.CleanupOwner, releases legacy hook/controller/
World/resources по явным retry flags. PrimitiveGameSession implements IRenderGameSession;
actual selected PrimitiveProbeGame создаёт registry/loader/controller только в child.
Новых shaders/backend/PInvoke/protocol/DI scopes/project dependencies нет.

Новые83methods/207cases в8test files+helper; прежние750-207=543ECS cases сохранены.
Final targeted Debug751/751 и Release751/751 (750ECS +old frame fixture1), failed/
skipped0, exit0. Full Aether regression:2536/2538,2diagnostics Stop/Dispose timeouts,
skipped0, exit1; все750ECS passed. Solution nonincremental Debug build exit0,
0errors/1555warnings. Полный regression не считается green.
Functional10k deep reverse-index chain не является latency/performance SLA.
Нормализованный warning catalogue совпадает с предыдущим e.3b build:
1422distinct messages/locations без line/column, delta0. Новых warnings этим срезом нет.

| Требование | Exact witness |
| --- | --- |
| «давай следующий шаг плана»: Transform/hierarchy | Update_EvaluatesTenThousandDeepReverseIndexChainWithoutRecursion; Update_PreservesShearAndEquivalentQuaternionSigns |
| Cache/movement | LatePreflightFailure_PreservesEveryOldCacheAndTick_AndAllowsSameTickRetry; Advance_UsesDoubleIntermediateSoLargeFiniteTermsMayCancelBeforeFloatConversion |
| Atomic complete extraction | Capture_SixtyFifthDrawableRejectsWholeFrameAndRetryDoesNotTruncate |
| App/owned preparation | Registration_IsLazyPreservesInjectedFactoriesAndCreatesCallerOwnedGraph; Dispose_OrdersLegacyHookPreparedGraphWorldAndLeasesAndRetriesOnlyFailedStage |
| Actual consumer | ManualTests/EcsPrimitive/Verify.ps1 и VerifyNative.ps1 |

Headless source Debug/published Release/fresh isolated7-package Release consumers
прошли133logical entities/2drawables/7source components, identity groups/inherited
values/velocity,2generations каждый. CPU pixels кодируют checksum всех extracted
matrices/colors и сверяются с literal source oracle0xff7b018d→0xffd9f18d на tick0→1.
Parent Game.Logic CLR не загружает; initial World/registry IDs/nonce различны,
process/frame/artifact owners released. Fresh Parent/Runtime/Game —0ProjectReferences,
пустой cache и local offline feed; packaged API/docs проверяются на final package set.

Native source Debug и published Release проверены через существующий Vulkan exporter/
GL readback:2generations/4captures каждый, red/green front-face pixels, moved red
centroid и stationary green centroid, matching tick/slot, explicit return retirement.
Core+synchronization validation enabled, errors0/warnings0 включая teardown; финальные
GPU resources/handles/frame mappings/UI sessions0, child reaped/released. Первый
native run без VK_LAYER_PATH failed ErrorLayerNotPresent и очистил всех owners;
финальный runner задаёт process-only path к existing extracted validation DLL/JSON,
их SHA256 сохранены. Нового tooling install/driver/OS changes нет. Fresh-package GUI,
physical input/K7/device-loss/VRAM/release acceptance не объявлены.

Повторяемые commands из src (PowerShell; каждый MSBuild имеет unique binlog):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-frame-Debug.trx' --results-directory TestResults/EcsPrimitive-20261106/Debug '-bl:TestResults/EcsPrimitive-20261106/ecs-frame-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-frame-Release.trx' --results-directory TestResults/EcsPrimitive-20261106/Release '-bl:TestResults/EcsPrimitive-20261106/ecs-frame-Release-{}.binlog'
& ./ManualTests/EcsPrimitive/Verify.ps1 -EvidenceDirectory TestResults/EcsPrimitive-20261106/Consumers-final
& ./ManualTests/EcsPrimitive/VerifyNative.ps1 -ConsumerEvidenceDirectory TestResults/EcsPrimitive-20261106/Consumers -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -EvidenceDirectory TestResults/EcsPrimitive-20261106/Native-validated
```

Оба targeted и оба runner commands завершились exit0. Native использует первый
consumer artifact set; Consumers-final повторяет package proof после final README.
C# source hashes совпадают. SDK fixture/fresh restore прочитал protected user
NuGet.Config через разрешённый запуск; settings/global cache не изменялись.

SDK10.0.403/net10.0/xUnit2.9.3/VSTest. Ранний API build использовал неверные короткие
World method names; исправлен на существующие *Component APIs. Первый scoped run
747cases (692pass/55fail) нашёл camera preflight extent1x1 вне старого min16 profile;
исправлен только preflight на16x16. Final all-ECS750/750 прошёл без изменения expected
assertions. Early full2537/2538 timed out на unchanged ChildDiagnosticFactoriesTests
AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts(di:false),
Until10s. Same-binary focused replay обоих providers2/2 passed; code/timeout не менялись.
Прямого semantic path от e.4 не найдено; scheduling/500ms delivery timeout — гипотеза,
не установленная причина. Unchanged-binary full repeat:2536passed/2failed/0skipped
из2538, exit1,8m5s. Failures:
ChildDiagnosticReceiverTests.InvalidRow_RejectsTheWholePacketBeforeEvenTheEarlierValidRowIsImported(field:pid)
при awaitusing DisposeAsync (ShutdownTimeout2s) и
ChildDiagnosticReceiverTests.ExactOptionBounds_AreCopiedBeforeUseAndAcceptOneFullBatch(maximum:false)
при StopAsync (50ms). Acceptance/rejection assertions до cleanup прошли. Focused
same-binary replay обоих методов25/25 passed,217ms, exit0. Все750ECS прошли в обоих
full runs; diagnostics receiver/tests/fixtures/forwarder/rate/server7source hashes
совпадают с pre-task baseline. Underlying cleanup task/listener/scheduling cause
не установлена. Production/timeout/tests не менялись; третьего full replay нет.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --logger 'trx;LogFileName=aether-Debug-final.trx' --results-directory TestResults/EcsPrimitive-20261106/Debug '-bl:TestResults/EcsPrimitive-20261106/aether-Debug-final-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --filter 'FullyQualifiedName=TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticFactoriesTests.AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts' --logger 'trx;LogFileName=diagnostics-replay.trx' --results-directory TestResults/EcsPrimitive-20261106/Debug '-bl:TestResults/EcsPrimitive-20261106/diagnostics-replay-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --filter 'FullyQualifiedName=TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticReceiverTests.InvalidRow_RejectsTheWholePacketBeforeEvenTheEarlierValidRowIsImported|FullyQualifiedName=TrueMoon.Aether.Tests.Diagnostics.ChildDiagnosticReceiverTests.ExactOptionBounds_AreCopiedBeforeUseAndAcceptOneFullBatch' --logger 'trx;LogFileName=diagnostics-receiver-replay.trx' --results-directory TestResults/EcsPrimitive-20261106/Debug '-bl:TestResults/EcsPrimitive-20261106/diagnostics-receiver-replay-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsPrimitive-20261106/solution-Debug-{}.binlog'
```

Static assertion/gap review Strong. Mid-loop cancellation races/unknown commit fault
injection и measured coverage/empirical mutation не заявляются. Единственный pairing
attempt остановился до parsing на protected user NuGet.Config; повторов/tool install
не было. Internal research/plan/status — non-stageable TEMP. Raw logs/TRX/unique
binlogs/failed attempts/source freeze/consumer/native reports — ignored
TestResults/EcsPrimitive-20261106; durable scope/outcomes сохранены здесь.
Scope audit сохранил1416прежних файлов;34addressed C# sources frozen, project/
dependency files не менялись. СемьSTATUS archives сохранены verbatim. Docs audit:
33Markdown/1857valid local links, новых link failures0 (247прежнихdistinct issues
сохранены отдельно); anchors/fences проверены отдельно от выполнения тестов.

<a id="ecs-runtime-loading-2026-11-06"></a>
## ARCH1e.3b: initial Runtime scene transfer и активация World — 2026-11-06

По запросу «давай следующий шаг плана» реализован
[initial-only Runtime contract](ECS_RUNTIME_LOADING.md). Hello сообщает explicit
capability/version, registry generation и limits только для SceneWorldGameSession.
Полный envelope передаётся appended Begin/Append/Activate/Abort operations через
прежний SendAsync:32KiB sequential chunks, один staging до4MiB+44bytes, один transfer
на runtime generation. Existing64KiB actual generated message/checkpoint и64primitive
limits сохранены. Session/build/host/generation/nonce/sequence дополняются exact
operation/registry/scene ID/revision, outer SHA256 и inner TMWL/strict decode checks.
Failed/aborted initial transfer не replay-ится; требуется новый child generation.

SceneWorldGameSession получает registry-specific loader через child DI/entry factory,
owns candidate/active scene и retained CleanupOwner. ActiveScene публикуется без user
callback после complete import и final cancellation check. Failed tick terminally
faults World; cleanup-started fence запрещает дальнейший Tick/Load, успешные disposals
не повторяются, failed cleanup остаётся для retry. Owned derived systems освобождаются
до borrowed World. GameBuildLoader теперь сохраняет failed game/ALC/artifact pin до
успешного cleanup вместо преждевременного unload и потери owner.

RuntimeProtocol owns staging, borrows game/codec; Dispose cancels до ожидания command
lock и drains cooperative importer даже при throwing cancellation callback. Server
закрывает admission/cancels connections и awaits handlers до game cleanup. Frames
блокируются до load, после Stop/disposal и terminal World fault. Game CLR в parent
не загружается. Parent App injects neutral codec/resolver; StartWorldAsync проверяет
каждый ack, source counts и immutable active identities, затем прежний readiness path.
FrameSession.StartWorldAsync использует прежние surface/first-presentation owners.

Добавлены5runtime test files:50methods/134cases; прежние409ECS cases сохранены.
Current all-ECS Debug543/543 и Release543/543, failed/skipped0, exit0.
Final full Aether Debug2331/2331, failed/skipped0, exit0: включает все543ECS cases
и обновлённый producer fixture. Production и134новых tests не менялись между proofs.
Latest targeted runs включают также обновлённый producer fixture:544/544 каждый
Debug/Release (543ECS +1FrameSession), failed/skipped0. Production не менялся.
Final nonincremental solution Debug build: exit0,0errors/1555warnings. Total и
1422distinct warning locations совпадают с прежней сборкой; один CS1591 прежнего
undocumented SessionSupervisor ctor получил расширенную сигнатуру в тексте warning.
Это сохранённый warning debt, не утверждение о warning-free workspace.

| Требование | Exact witness |
| --- | --- |
| «давай следующий шаг плана»: Runtime загрузка | CompleteTransfer_ActivatesFullCustomSceneBeforeTicksFramesAndMetadata; StartWorld_WaitsForActivationThenTransfersExactFullPayloadAndPublishesInitialCounts |
| Atomic initial ownership | LoadSceneWorld_PublishesOnlyCompleteCandidateAndOwnsItsLifetime; FailedGameDisposal_RetainsSessionContextAndArtifactPinUntilSuccessfulRetry |
| Actual consumer | ManualTests/EcsRuntimeLoading/Verify.ps1; source Debug, published Release и fresh isolated NuGet Release — Completedtrue |

Actual WorldProbeGame consumer проверен на полном payload286046bytes с256entities/
256components,2definitions/variant inheritance, nullable refs, overrides и Unicode.
Каждый из3profiles выполнил2generations, CPU pixel oracle до/после Step0→1,
distinct World/registry/nonce/generation, no Game.Logic in parent, pinned artifact
пока active и confirmed release/reap/rings0 после Stop. Fresh parent/runtime/game
имеют0ProjectReferences;7новых local framework packages, пустой cache и offline
feed из exact cached external nupkg. Ни network install, ни global settings не менялись.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Debug-final.trx' --results-directory TestResults/EcsRuntimeLoading-20261106/Debug '-bl:TestResults/EcsRuntimeLoading-20261106/ecs-Debug-final-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Release.trx' --results-directory TestResults/EcsRuntimeLoading-20261106/Release '-bl:TestResults/EcsRuntimeLoading-20261106/ecs-Release-{}.binlog'
& ./ManualTests/EcsRuntimeLoading/Verify.ps1 -EvidenceDirectory 'TestResults/EcsRuntimeLoading-20261106/Consumers-verified'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-frame-Debug.trx' --results-directory TestResults/EcsRuntimeLoading-20261106/Debug '-bl:TestResults/EcsRuntimeLoading-20261106/ecs-frame-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.|FullyQualifiedName=TrueMoon.Aether.Tests.FrameSessionTests.Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces' --logger 'trx;LogFileName=ecs-frame-Release.trx' --results-directory TestResults/EcsRuntimeLoading-20261106/Release '-bl:TestResults/EcsRuntimeLoading-20261106/ecs-frame-Release-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --logger 'trx;LogFileName=aether-Debug-final.trx' --results-directory TestResults/EcsRuntimeLoading-20261106/Debug '-bl:TestResults/EcsRuntimeLoading-20261106/aether-Debug-final-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsRuntimeLoading-20261106/solution-Debug-{}.binlog'
```

SDK10.0.403/net10.0/xUnit2.9.3/VSTest. First test compilation caught2async Throws
assertion overloads; fixed to awaited ThrowsAsync, one nullable test warning fixed.
Default sandbox all-ECS542/543 был blocked только existing SDK fixture access к
user NuGet.Config, не behavioral failure; elevated run543/543 прошёл без изменения
settings. Аналогично first fresh restore был blocked; последующий graph assertion
ошибочно ожидал generator как runtime dependency, runner исправлен на7реальных
runtime packages. Все failed logs/binlogs и intermediate successes сохранены.
Первый full module2330/2331 нашёл старый producer fixture, который ожидал кадры
без Hello/Load. Исправлен только setup штатной readiness, прежние EOF/frame ID
assertions сохранены. Updated fixture прошёл latest targeted D/R; final full run
2331/2331 проверил этот новый test binary отдельно, не выдаёт первый failed run за green.

Static assertion/gap review Strong, measured coverage/empirical mutation не заявляются.
Однократный Roslyn pairing attempt не дошёл до parsing из-за user NuGet.Config access.
Internal research/plan/status — non-stageable TEMP; raw outputs/TRX/unique binlogs,
source freeze/provenance и audit — ignored TestResults/EcsRuntimeLoading-20261106.
Durable scope/outcomes сохранены здесь, независимо от raw artifact retention.
Final audit:31Markdown documents/1839valid local links/0new link failures и7verbatim
STATUS archives;4new production/2manual consumers/5new test files,15addressed existing
sources и1353previous files byte-preserved. Project/dependency files не менялись;
namespace/path, D/R/full case sets, unique binlogs, actual consumer reports, package
DLL↔current Release hashes и git diff --check прошли.

Это actual Runtime/CPU consumer proof; новый native GPU/UI/Editor Play/preview,
full solution tests и performance baseline не заявляются. Existing server drain и
new direct protocol/supervisor cancellation tests не выдаются за отдельный physical
native cancellation campaign. Builtin Transform/extraction — e.4, resources — f,
ECS checkpoint/Replace — e.5; ECS capture явно Unsupported.

<a id="ecs-loading-2026-11-06"></a>
## ARCH1e.3a: registry и headless scene loading — 2026-11-06

По запросу пользователя добавлен [контракт загрузки](ECS_LOADING.md): frozen
ComponentRegistry с exact schema/type-version bindings, explicit entity/asset reference
semantics и data validators; SceneWorldLoader/typed factory, scoped import contexts и
SceneWorld-owned World/leases. Register<T> проверяет value storage, singleton profile
и однозначность stable ID↔CLR type; Build snapshots не меняются последующим builder.
Unknown type/version/schema drift/RequiredTypes/reference/multiplicity и source/resolved
budgets не приводят к silent drop или partial World publication.

Полный immutable SceneDocument с definitions/inheritance/overrides/defaults/null и
entity metadata переносится SceneWorldData/Codec: TMWL/version1/length/SHA256 + existing
strict authoring v1/v2 body. Max4MiB+44bytes, checksum — integrity, не authentication.
Codec не активирует registry/Game.Logic. Некорректный UTF16 проверяется во всём source
catalog/defaults/overrides до serialization/import: существующий Utf8JsonWriter иначе
заменял lone surrogates на U+FFFD. Старый SceneFileCodec не менялся; paired Unicode/NUL
и точные Int64/Number kinds сохраняются. Data decode использует старые strict JSON checks.

После data validation private fresh World получают через injected IFactory, резервируют
все entities в hierarchy order, связывают persistent IDs с handles, готовят все typed
values/leases и только затем publish. Mutable World не передаётся contexts; private
write/query-write/dispose/scheduler activation fenced. Used-empty/retired/owned/nonempty
World factory results отклоняются без disposal чужого World. Source document/history
не меняются; initial identity mapping/metadata после runtime mutation не выдаются за live state.

Acquire резервирует capacity до provider callback, в том числе при nested acquisition;
tracks returned lease до cancellation, rejects duplicate references без double dispose.
SceneWorld.Dispose сначала инвалидирует World (после scheduler/query completion), затем
reverse leases; successful disposals не повторяются, failed leases сохраняются для retry.
Reentrant disposal отвергается. Failed preparation+cleanup возвращает typed exception
с исходной ошибкой/cleanup aggregate и CleanupOwner. Pure callback errors имеют stable
load code и scene/entity/component/property location; nullable/finite/range helpers не
делают implicit coercion. Asset validation пока GUID/provider contract, не готовый asset pipeline.

UseAetherSceneLoading создаёт App-local registry/loader/typed factory и neutral codec,
borrowed dependencies выбираются в composition root. Private default authoring validation
graph codec не меняет global overrides и не создаёт store/UI/native resources. App startup
не создаёт Worlds/imported components; successful factory results caller-owned.

Actual IEngineControl/IGameSession/preview/Player/checkpoint/render consumers не изменены.
E.3a — headless load contract, e.3b ещё требует bounded transport/staging/identity/atomic
active-world handoff и independent consumers. Старые64KiB control/checkpoint и64primitive
limits не расширены; builtin Transform mapping/renderer и asset revisions — следующие stages.
Project/dependency/TFM files не менялись. Все прежние незакоммиченные source/docs сохранены.

Новые71methods/201cases в6test files + helper; прежние208ECS cases не менялись.
Current successful runs, exit0, SDK10.0.403/net10.0/xUnit2.9.3/VSTest:

| Scope | Passed | Failed/Skipped | Уточнение |
| --- | --- | --- | --- |
| All ECS Debug |409|0/0| Новые201 + прежние208 |
| All ECS Release |409|0/0| Те же409cases |
| Full Aether Debug |2197|0/0| Включает все409latest ECS cases, binary/source freeze не менялся |
| Solution nonincremental Debug build |—|0errors|1555existing warnings, новых ECS warnings0 |

Earlier helper params-array target-typed new compile failure исправлен explicit
ComponentPropertySchema; failed log/binlog сохранён. Intermediate405-case run
не подменяет final409 после добавления numeric/empty quota boundary cases.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Debug-final.trx' --results-directory TestResults/EcsLoading-20261106/Debug '-bl:TestResults/EcsLoading-20261106/ecs-Debug-final-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Release.trx' --results-directory TestResults/EcsLoading-20261106/Release '-bl:TestResults/EcsLoading-20261106/ecs-Release-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --logger 'trx;LogFileName=aether-Debug.trx' --results-directory TestResults/EcsLoading-20261106/Debug '-bl:TestResults/EcsLoading-20261106/aether-Debug-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsLoading-20261106/solution-Debug-{}.binlog'
```

Direct evidence:

| Требование | Exact test witness |
| --- | --- |
| registry | Build_ProducesIndependentFrozenRegistriesWithoutInvokingCallbacks; Binding_RequiresExactVersionAndReportsStableSceneContext; Binding_RejectsSchemaDriftUnderSameStableVersion |
| загрузка сцены в World | Load_ResolvesInheritanceForwardReferencesHierarchyAndIndependentWorlds; InvalidFactoryWorld_IsRejectedWithoutDisposingForeignOwnership |
| Full payload | RoundTrip_PreservesFullAuthoringMetadataAndResolvedInheritance; Encode_RejectsLoneUtf16SurrogatesInsteadOfSilentlyReplacingPayload; Decode_AcceptsExactMaximumBodyAndRejectsAdjacentOversize |
| Ownership/failure | Acquire_NestedProviderCannotConsumeOuterReservationBudget; CleanupFailure_RetainsOwnerOriginalErrorAndOnlyRetriesFailedLease; SceneDisposal_PreservesResourcesUntilActiveQueryOrSchedulerCompletes; ResourceDisposal_RejectsReentrancyWithoutDoubleReleaseAndOwnerRemainsIdempotent |

SDK10.0.403/net10.0/xUnit2.9.3/VSTest. Broad test research/plan/status — non-stageable TEMP;
static assertion/gap review Strong, без измеренного coverage/empirical mutation claims.
Raw logs/TRX/unique binlogs/source hashes/audit — ignored `TestResults/EcsLoading-20261106/`;
scope и outcomes хранятся здесь независимо от raw retention. Новый GPU/native UI/
package consumer/latency proof не запускался; full module использует разрешённый запуск
вне sandbox для existing SDK subprocess user NuGet.Config access, без изменения settings.

Final audit:29изменённых Markdown documents,1819валидных local links,0новых link
failures;7STATUS archives verbatim сохранены. Scope:14новых production files,
6test files + helper,2адресных изменения World.cs/World.Execution.cs;64прежних
файла сохранены byte-for-byte. Namespace/path, dependency/project scope, source
hashes, TRX case-set equality D/R/full module и git diff --check проверены.

<a id="ecs-execution-2026-11-06"></a>
## ARCH1e.2: queries, command buffer и системы — 2026-11-06

По запросу пользователя реализован [headless execution profile](ECS_EXECUTION.md).
World/ComponentPool расширены scoped1/2type queries с readonly/write-first/write-both,
до8distinct exclusions и меньшим required pool как candidate set. Descriptor/pool
lookups не регистрируют тип; nested/structural/Set/dispose/playback во время обхода
отклоняются. Finally снимает borrow guard, но предыдущие callback writes не откатываются.

EntityCommandBuffer хранит bounded FIFO records, deferred buffer/batch GUID tokens
и immutable playback mappings. Preflight симулирует touched allocator/membership и
retirement/type budgets до commit. Validation failure не меняет World, cancel до
commit оставляет tokens для explicit retry; commit после final check не прерывается.
Unknown commit failure сохраняет applied-prefix diagnostics, делает World Faulted,
блокирует mutations, сохраняя inspection reads. Reset/Dispose не освобождают borrowed
component values. AppliedCommandCount сбрасывается и при pre-cancel новой попытки.

EcsSystemDescription/Registration задают copied Read/Write/Before/After/phase metadata;
graph проверяется до activation. EcsScheduler выполняет systems последовательно,
flush после Input/Gameplay/Movement/Transform/Extraction; tick/delta даёт внешний
owner, без timer или Resolve в кадре. Factory activation не может менять World.
UseAetherEcsSystems регистрирует App-local IFactory<EcsScheduler,World>, используя
внедрённые IFactory<TSystem,World>. Context.Commands — recording facade с invocation
generation: retained writer не оживает на следующем Execute той же системы.

Scheduler exclusively borrows World lease и owns system results, не factories или
dependencies. Stop/cancel discard pending commands без rollback старых writes;
execution failure terminal Faulted, исходная ошибка сохраняется при stop failures.
Reverse cleanup best-effort, успешные disposals не повторяются и ссылки снимаются,
failed entries/lease остаются для retry. Failed creation+cleanup возвращает
EcsSchedulerCreationException.CleanupOwner. World.Dispose требует завершить graph cleanup.
Runtime/preview/load/checkpoint/render consumers не мигрировали; e.3/4/f ещё впереди.
Проекты/пакеты/dependencies/target frameworks и существующие wire limits не менялись.

Добавлены4test files:72methods/151cases; прежние57cases сохранены без изменения.
Прямые witnesses: `ReadPair_IntersectsRequiredPoolsIndependentlyOfPoolSize`,
`Playback_AppliesFifoCommandsAndResolvesDestroyedReservations`,
`PhaseBoundary_CommitsCommandsBeforeNextPhaseAndQueriesObserveMovement`.
Дополнительно `RetainedWriter_IsRejectedDuringNextInvocationWhileFreshWriterRecordsCorrectly`,
`CancellationWithStopFailure_PreservesCancellationAndFaultsWorld` и
`ExplicitStopFailure_AttemptsEveryCallbackOnceAndRetainsFaultUntilDisposal`.
Static gap/assertion review выполнен; measured coverage/empirical mutation не заявляются.
Единственный static pairing attempt не дошёл до parsing из-за доступа к user NuGet.Config;
новые проверки реально скомпилированы/выполнены в существующем xUnit project.

SDK10.0.403/net10.0, xUnit2.9.3/VSTest; current successful runs, exit0:

| Scope | Passed | Failed/Skipped | Уточнение |
| --- | --- | --- | --- |
| Latest all ECS Debug |208|0/0| Новые151 + прежние57 |
| Latest all ECS Release |208|0/0| Те же cases, без full Aether Release повторения |
| Full Aether Debug binary snapshot |1994|0/0|7m59s, snapshot с206ECS до добавления последних2test-only cases; production code тот же |
| Solution nonincremental Debug build | build exit0 |0errors/1555warnings|9.39s, без новых ECS warnings; это не solution-wide test run |

Последние два Stop-error tests добавлены после freeze полного binary snapshot и
прошли latest narrow D/R; module count не выдан за текущий208-case ECS binary.
Первый all-ECS build обнаружил неоднозначность внутреннего registration ctor для
friend tests; заменён private ctor + internal FromPlan, failed log/binlog сохранён.
Ранний queries/commands subset59/59 и ECS205/206 этапы не подменяют final208 evidence.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Debug-final.trx' --results-directory TestResults/EcsExecution-20261106/Debug '-bl:TestResults/EcsExecution-20261106/ecs-Debug-final-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Release.trx' --results-directory TestResults/EcsExecution-20261106/Release '-bl:TestResults/EcsExecution-20261106/ecs-Release-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --logger 'trx;LogFileName=aether-Debug.trx' --results-directory TestResults/EcsExecution-20261106/Debug '-bl:TestResults/EcsExecution-20261106/aether-Debug-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsExecution-20261106/solution-Debug-{}.binlog'
```

Full module run использовал разрешённый execution вне sandbox: его existing SDK/
process fixtures читают user NuGet.Config. User settings не менялись; ECS narrow/
build работают с existing assets и --no-restore. GPU/native UI/fresh-package tests
не запускались. Observable allocation test `CachedQueryAndDelegates_ReportSteadyAllocationsWithVerifiedState`:
одна entity, cached query/delegates,10000ticks,0bytes каждый D/R после warmup;
точный state проверен assertions. Это не latency/throughput benchmark, не quota
для любых систем и не профиль structural recording (там records могут аллоцировать).
Logs/TRX/unique binlogs/source hashes и audit — ignored `TestResults/EcsExecution-20261106/`.
Final audit:28изменённых Markdown documents,1805валидных local links,0новых link
failures;7STATUS archives verbatim сохранены. Scope проверил19новых production/
4новых test files,3адресных source изменения и39прежних файлов без изменений;
namespace/path и git diff --check прошли, dependency/project files не затронуты.
Долгосрочные scope/outcomes сохранены здесь, internal test research/plan/status — TEMP,
не stageable workspace files. Предыдущие незакоммиченные изменения/архивы сохранены.

<a id="ecs-world-v1-2026-11-06"></a>
## ARCH1e.1: первый headless World — 2026-11-06

По запросу приступить к реализации/проектированию добавлены `Ecs.World`,
`EntityHandle`, `WorldOptions`, internal typed `ComponentPool<T>` и публичное
`UseAetherEcs`. Первые решения e.0 зафиксированы в [World v1](ECS_DESIGN.md#world-v1):
functional workload10000entities, typed sparse/dense pools, singleton value components
без managed references, copy reads и serialized caller access. World GUID/uint
generation отделены от persistent authoring IDs; slot exhaustion не оборачивает
generation, retired slots входят в конечный бюджет. Dense removal обновляет sparse
mapping; lookup absent не регистрирует type, duplicate Add/missing Set не меняют данные.

Options копируются/валидируются до использования; default65536slots/128types/initial64,
верхние count limits1048576/1024. Лимиты количества не резервируют память и не являются
байтовой квотой или performance SLA. Add/growth готовят массивы перед публикацией;
constructor failure не публикует World и не оставляет native/owned dependencies.
Values не получают вызов Dispose. App-local IFactory создаёт caller-owned Worlds,
без singleton alias/startup world/runtime/window/GPU; Dispose App не подменяет
Dispose сессии/World. Microsoft DI adapter и Cobalt проверяются отдельно.

Queries/ref scopes/phase commands/systems, schema materializer/AllowMultiple admission,
full load/checkpoint и runtime/preview integration ещё не реализованы. Binding-set
arrays для будущего MeshRenderer будут принадлежать отдельному versioned resource
owner, не скрытому managed payload в value pool; ARCH1f остаётся следующим design track.
Существующие сцены,64-entity primitive limits, wire/checkpoint/API consumers не мигрировали.
Новых пакетов, references, target frameworks или assembly split нет.

Добавлены `WorldTests`, `WorldOptionsTests`, `EcsConfigurationTests`:57cases в27methods.
Новый narrow Debug run — passed57/failed0/skipped0, exit0, SDK10.0.403/net10.0,
xUnit2.9.3/VSTest. Команда из src:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Debug.trx' --results-directory TestResults/EcsWorld-20261106/Debug '-bl:TestResults/EcsWorld-20261106/ecs-Debug-{}.binlog'
```

Конкретное evidence: `InvalidHandles_RejectEveryComponentOperationWithoutChangingLiveEntity`,
`GenerationExhaustion_RetiresSlotsWithoutRevivingOldHandles`,
`GrowthAndReuse_PreserveSparseDenseMappingsForTenThousandEntities`,
`ManagedValues_AreRejectedByEveryComponentApiWithoutRegisteringPool`,
`ComponentCopies_DoNotOwnDisposableValues`,
`Registration_SnapshotsOptionsAndCreatesIndependentCallerOwnedWorlds`.
Broad test research/plan/quality status хранятся в non-stageable TEMP scratch;
static gap/assertion review не является измеренным coverage/empirical mutation run.
IDE findTests отсутствует; статический pairing tool остановился на доступе к
пользовательскому NuGet.Config, поэтому успешный pairing не заявляется.

Первый full Aether Debug внутри sandbox встретил NuGet.Config access denied во
вложенных SDK restores/builds и был прерван; итоговый full count для него не заявлен.
Даже fixture с explicit --configfile обращался к user settings. Ошибки/partial log
сохранены отдельно; повтор разрешён вне sandbox без изменения user settings.
Повторный full Aether Debug вне sandbox: passed1845/failed0/skipped0, exit0,
duration7m29s. Это module suite с existing SDK/process tests, не full solution suite.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-build --no-restore -c Debug --logger 'trx;LogFileName=aether-Debug-unrestricted.trx' --results-directory TestResults/EcsWorld-20261106/Debug '-bl:TestResults/EcsWorld-20261106/aether-Debug-unrestricted-{}.binlog'
```

Новый ECS Release: passed57/failed0/skipped0, exit0. Full Aether Release в этом
срезе не повторялся; Debug suite и Release targeted evidence имеют разные scopes.
Workspace nonincremental Debug build: exit0,0errors/1555warnings,13.61s.
Предупреждения не относятся к новым ECS файлам; это build, не full solution test run.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Ecs.' --logger 'trx;LogFileName=ecs-Release.trx' --results-directory TestResults/EcsWorld-20261106/Release '-bl:TestResults/EcsWorld-20261106/ecs-Release-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -m '-bl:TestResults/EcsWorld-20261106/solution-Debug-{}.binlog'
```

Все successful TRX/binlogs и source hashes проверены отдельно. Документационный
audit проверяет local links/aliases,7старых STATUS archives verbatim и прежние
release/UI acceptance rows; source scope —5новых production и3test files, без
project/dependency changes. Подробный REPORT и machine-readable manifests — в raw
artifact directory ниже; они не заменяют эту versioned запись результата.
Raw logs/TRX/binlogs — ignored `TestResults/EcsWorld-20261106/`; новые GPU/native UI/
fresh-package/performance proofs этим storage срезом не выполнялись.

<a id="ecs-rendering-plan-2026-11-06"></a>
## ECS в планах и проект системных компонентов отображения — 2026-11-06

По запросу пользователя направление ARCH1e внесено в план; обсуждённые data/system/
identity/phase/snapshot границы зафиксированы, а workload/storage/multiple policy
и конкретные public API остаются открытыми до e.0. Добавлены ARCH1f.0–6 и
[RENDERING_DESIGN](RENDERING_DESIGN.md). ED4i владеет будущим inspector consumer,
не дублирует engine resource lifecycle или generic UI controls.

Прочитаны RenderPrimitive, PrimitiveScene/IRenderGameSession, ScenePrimitive,
PreviewGameSession, GpuFrameData, VulkanPrimitiveRenderer, GpuResourceHost и surface
producer. Текущий путь — immutable matrix+stored-sRGB primitive snapshot,64cubes,
embedded shaders/draw36, slot-owned depth/framebuffer/pipeline и retryable host
cleanup. Общего mesh/material asset catalog/cache эти контракты не предоставляют.
Эта исходная сверка не является новой native проверкой.

Предлагаемые роли: MeshRenderer/Camera — World data, extraction — system,
renderer/backend и asset preparation — DI services; shared immutable material
revisions отделены от per-entity overrides. Проработаны slot/type/layout validation,
strict/optional readiness, bounded queues/memory, generation fences поздних loads,
pinned resource leases в snapshots и retirement после всех зависимостей completion.
Первый consumer — built-in cube/shared opaque unlit material/две entities с tint,
затем отдельные texture/lighting profiles. Logical checkpoint сохраняет IDs/revisions,
не handles; новые content inputs требуют собственного captured build/delivery contract.

Production code, зависимости, assembly layout и существующие schema/wire/checkpoint
budgets не менялись; build/tests/performance/native/package не запускались.
Предыдущие незакоммиченные документы/архивы сохранены. Baseline и audit документации —
ignored `TestResults/EcsRenderingPlan-20261106/`; долгосрочные решения/ограничения
сохранены здесь и в versioned планах, raw evidence не является единственным источником.
Документационный audit:25изменённых документов относительно HEAD,1765валидных
local links,0новых link failures;7старых STATUS archives сохранены verbatim.
Отдельная scope-проверка подтвердила16не затронутых этим запросом документов,
единственные таблицы этапов e.0–5/f.0–6 и одного consumer ED4i; diff --check чистый.

<a id="ecs-design-2026-11-05"></a>
## Проработка системы компонентов ECS — 2026-11-05

По запросу пользователя прочитаны ComponentSchema/ComponentData/PropertyValue,
resolved model, IGameSession, primitive projection и checkpoint state. Уже есть
stable IDs/version/multiplicity/dependencies/inheritance; runtime full component
World/typed pools/queries этим не предоставляются. Primitive path оставляет custom
types authoring-only и ограничен64entities; его нельзя считать full load bridge.
Уточнена прежняя запись checkpoint: v2 включает per-entity switches, но не full World.

Добавлен [черновик ECS_DESIGN](ECS_DESIGN.md) и будущая ветка ARCH1e.0–5 в engine
плане: identity/pools, queries/phase commands/systems, materialization/versioned
load, transform/extraction, затем logical checkpoint/Replace. Основная гипотеза —
sparse/dense typed pools и последовательная simulation; archetype storage остаётся
альтернативой до выбранного workload/measurement. Multiple/runtime-managed policy
и error outcomes ещё нужно выбрать. Компоненты — данные, systems/world создаются
через App/Cobalt/IFactory, game rules — Tales/Lore. Нет нового public API/assembly.

Справочные первичные sources: EnTT storage, Unity Entity и ECB — ссылки в черновике.
Они использованы для проверки общих инвариантов, не выбора внешней зависимости и
не performance обещания. New code/build/test/native/performance/package runs нет.
Прежняя ревизия23документов и её архивы сохранены; текущая очередь отражает ECS
design request, предложенный UI маршрут не удалён. Evidence чтения/ссылок —
ignored `TestResults/EcsDesign-20261105/`.


<a id="planning-revision-2026-11-05"></a>
## Ревизия очереди Editor/engine — 2026-11-05

Документационный проход после db5ea97. Текущий код EditorPreview/Inspector
подтвердил наличие прикладного snapshot→presentation path; первая часть ED5
больше не описана как отсутствующий Editor consumer. Camera/picking/tools открыты.
Коллизия ID ED4g устранена переобозначением будущего Game AI inspector в ED4h;
старый anchor совместим, scope/acceptance AI не урезаны.

Предложены ED4g.3 group reset, ED4g.4 rad/deg presentation и ED5a camera/picking.
Observer продолжает ED6b.2e.4f с измеренного CPU/allocation tradeoff, а не с
повтора первых traces/cache/open. ARCH2e.4b получает отдельный план причинной
диагностики31октября; replays не закрывают повторный output drain timeout.
Full-solution baseline и release matrix не подменяются Editor/Alloy suites.

Старый STATUS сохранён целиком внизу HISTORY, текущий — короткая сводка с одной
очередью и датированными evidence. Новых code/build/test/native/performance
результатов нет. Общий протокол и audit —
[ревизия планов](../HISTORY.md#planning-revision-2026-11-05).


<a id="ed4g2a-native-numeric-2026-11-05"></a>
## ED4g.2a: native numeric acceptance — 2026-11-05

Добавлен `Gui/NumericWorkflow.cs` и отдельный `VerifyNumeric.ps1` в
ManualTests/AetherPackageConsumer. Скрипт строит source Debug и publishes Release
Editor/runtime с парными shared binaries; создаёт отдельные проекты, сохраняет
binlogs/stdout/stderr/assembly paths, native OpenGL PNG, source hashes и report.
Routed подаёт UiSession.HandleInput, WindowsPointer/WindowsArrows ожидают настоящий
ввод в окне и валидируют переходы через stage.json. Standard/Cobalt и embedded/
MS DI используют существующие App factories/Window/cleanup; runtime child не
запрашивается. Native/font asset проверяется тем же bootstrap.

До исправления быстрый Windows drag давал pointer-down и pointer-up в конечной
точке. Причина уточнена по реализации
[GlfwMouse.Position](https://github.com/dotnet/Silk.NET/blob/v2.23.0/src/Input/Silk.NET.Input.Glfw/GlfwMouse.cs):
это live cursor query, а не координата доставляемого button event; наш callback
boundary выполняет действие сразу. Platform.Silk теперь хранит последнее ordered
cursor event отдельно для каждой мыши, использует его для button/wheel и передаёт
Shift/Ctrl. Native proof после правки показывает down1134/up1174 и
Scale2/3/5→2.1/3.15/5.25 без auto Apply. Регрессии — в UI истории.

Свежие завершённые runs в `TestResults/NumericNative-20261105/`:

| Profile | Composition | Input | Result |
| --- | --- | --- | --- |
| SourceDebug-Fixed | standard/Cobalt | 4routed +6Windows cases | passed |
| PublishedRelease-Paired | standard/Cobalt | 4routed +6Windows cases | passed |
| Repeatable-Embedded/SourceDebug-report | embedded/MS DI | 4routed cases | passed |
| Repeatable-Embedded/PublishedRelease-report | embedded/MS DI | 4routed cases | passed |

Проверены arrows/Shift/Ctrl, invalid NaN без coercion, exact02.0/03.0/05.0 rollback
на captured Esc, release draft, explicit Ctrl+Enter и одна shared history entry.
Windows дополнительно проверяет настоящий checkbox, быстрый drag/release, Esc
после release, Ctrl+Enter и Undo button. Controls retained; final active UI/GPU/
handles0, runtime children/reports0 во всех четырёх;28native readback PNG retained.
Свежие full Alloy1101/1101, Hosting154/154, Editor1295/1295 каждый Debug/Release,
failed/skipped0; focused pointer6/6 Debug. Полный solution test run не выполнялся.

Незавершённые диагностические запуски сохранены: первоначальный harness ожидал
неполную синхронизацию Open→workspace (исправлено ожиданием EntityIds); автоматические
Up/Shift+Up приходили как UiKey.None, хотя Ctrl+A распознавался; pre-fix pointer
run воспроизвёл live-position ошибку; первоначальный Release вызов с Debug runtime
был отвергнут штатным fingerprint guard, затем выбран парный published Release.
Эти результаты не включены в четыре passing run. Все тестовые процессы/окна закрыты.

Граница: нет утверждения о физической клавиатуре, arrows с modifiers через Windows,
Esc при удерживаемой кнопке или pointer modifiers через OS. У текущего automation
API нет отдельных down/up для смешанного mouse/key жеста. Эти gates идут следующими,
затем group reset/angle units. Fresh packages, Vulkan validation/runtime preview,
device-loss, driver recovery и release matrix этим OpenGL smoke не повторялись.
Отчёт/аудит: `TestResults/NumericNative-20261105/REPORT.md`, `verification.json`.


<a id="ed4g2-numeric-gestures-2026-11-05"></a>

## ED4g.2: numeric gestures и proportional Scale — 2026-11-05

Реализован следующий UI пункт плана [ED4g.2](EDITOR_PLAN.md#ed4g2-numeric-gestures).
Все current numeric editors включают общий NumberBox adjustment: Integer1, Real/
Position0.1, Rotation(rad)/Scale0.01, Shift×10/Ctrl×0.1. Gesture является draft-only:
pointer release не Apply; Esc отменяет локальный gesture прежде общего Cancel;
Ctrl+Enter завершает capture перед одной authoring transaction. Input capture и
callbacks UI-owned, модель хранит только immutable draft snapshots/identity, не View.

Transform footer LinkScale выключен по умолчанию и не меняет dirty/history сам.
Typing/step/drag сохраняют XYZ ratios. Drag targets вычисляются от captured3axis
baseline, предотвращая accumulation drift; возврат к началу сохраняет raw peer
strings. Decimal multiplication-before-division используется только для round-trip
operands, иначе finite Double fallback: простые2.01/3.015/5.025 и6.02/9.03/15.05
показываются без binary tails. Invalid source/peer, overflow и pending-reset targets
не вызывают частичных peer writes; source draft/diagnostic сохранены, Apply blocked.
Snapshot exact3flags/value restores прежние drafts на Esc/input loss. Selection,
document ref/revision и unique per-gesture epoch не допускают stale rollback, в том
числе при двух Inspector Views с общей borrowed моделью. Engine/schema/wire/DI
lifetimes и explicit history contract прежние.

Fresh [evidence](../../TestResults/NumericGestures-20261105/REPORT.md): full Editor
**1295/1295 D/R**, failed/skipped0,24новых Editor cases. Отдельно Alloy **1095/1095**
и Hosting **154/154** D/R; reviewed controls40/40 Debug. Focused Inspector180/180
Debug прошёл перед последним formatting-only fix; финальные полные D/R после него
подтвердили exact string assertions и весь новый subset. Final nonincremental Debug
build0errors/1555warnings, full solution tests не выполнялись.11Skia CPU frames
прошли no-auto-Apply/one-transaction/Undo/exactcancel assertions и визуальную проверку;
preview build0warnings/errors. Native/GPU/fresh package/physical acceptance не повторена.

В fix cycles исправлены реальный fractional-coordinate tick и callback cleanup;
два overflow fixtures сначала не проходили scene admission из-за parent×child world
scale, окончательный witness1e10→target1e40 admitted и проверяет atomic preflight.
Один прежний compact test намеренно учитывает footer (3Vectorrows+CheckBox,116px);
остальные focus/history/selection/lifetime assertions сохранены. Manual inspector
smoke проверяет3actualVectorrows и наличие footer, native run не повторён.

Команды full: `dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj
--no-restore -c Debug` и Release с TRX/results/unique binlog. Авторитетные финальные
TRX: `Editor-Final-Debug` / `Editor-Final-Release`; предыдущие циклы сохранены отдельно.
Сборка: `dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug` с `-bl`.
Далее source/published native numeric gesture/cancel proof; group reset/angle units
и Observer scheduling/allocation остаются отдельными задачами.


<a id="ed4g1-generic-editors-2026-11-05"></a>

## ED4g.1: остальные редакторы и их состояния — 2026-11-05

Реализован [ED4g.1](EDITOR_PLAN.md#ed4g1-generic-editors). Все existing Number/Integer/
Boolean/String/nullable поля строятся через общую responsive PropertyEditorRow.
Числа используют NumberBox с прежним typed TextBox Input, boolean отображает «Да»/
«Нет», строки сохраняют RichTextEditor и автоматически растут26–78. Null показывает
«Не задано», скрывая, но сохраняя тот же контрол и stored draft. Пустая строка,
null и0 остаются различными. GenericEditors доступны по прежним точным addresses;
удаление selection/schema/dispose очищает owned controls и mapping. Reset/provenance
раскрываются на полной ширине; ошибка/modified/hover/focus/disabled/read-only
применяют общие роли и actual checkbox indicator. Engine schemas/wire/DI lifetimes
не менялись. Apply/Ctrl+Enter, Cancel/Esc и одна shared transaction сохраняются.

Свежие [результаты](../../TestResults/InspectorEditors-20261105/REPORT.md): full
Editor **1271/1271 Debug/Release**, failed/skipped0;15новых Editor cases. Отдельно
full Alloy **1055/1055 D/R**, Hosting **154/154 D/R**; focused Inspector
**156/156 Debug**. Null Integer/Number/Boolean проверены отдельными случаями сверх
строкового fixture: retained typed draft, toggle, commit/Undo; RuntimeIdle false→true
сохраняет draft/null/reset/history при временно недоступных inputs/accessory.
Валидация invalid numeric drafts, multiline undo/selection/resize/theme и lifetime
имеет прямые assertions. Новых analyzer warnings нет; два прежних xUnit1031 в
Diagnostics сохранены. Общий nonincremental Debug build0errors/1555warnings,
полного solution test run нет.14реальных Skia CPU captures просмотрены; source/
published/fresh NuGet native, runtime/GPU/physical acceptance не повторялись.

Команды: full `dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj
--no-restore -c Debug` и Release с TRX/results directory/unique binlog. Final focused
filter: `FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Inspector|FullyQualifiedName~EditorTransformView`.
Сборка: `dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug` с `-bl`.
Два intentional legacy теста адаптируют auto height и scroll precondition; история/
валидация/выделение/cleanup не ослаблялись. Остальные code changes предыдущих задач
и локальный .codex сохранены. Далее числовые gestures/step/linked scale; Observer
scheduling/allocation follow-up остаётся отдельной engine-задачей.


<a id="ed4g-compact-transform-2026-11-05"></a>

## ED4g: компактный Transform и цветовые состояния — 2026-11-05

Реализован согласованный [ED4g](EDITOR_PLAN.md#ed4g-compact-transform): отдельный
parent-owned UI control TransformEditor содержит Position XYZ, Rotation P/Y/R в
радианах и Scale XYZ на общих NumberBox/Vector3Editor. При выборе reserved built-in
TypeId/schema v1 сохраняются девять точных property addresses и TextBox identities;
другие типы используют generic fallback, неподдержанный reserved v2 отвергается
существующим engine validator до публикации controls. Схемы/wire/history не изменены.

Подписи и содержимое TextBox центрированы; три равные колонки адаптируются к ширине.
«⋯» раскрывает metadata и full-width provenance/индивидуальные reset actions для
конкретного компонента. Reset остаётся pending draft. Ошибки получают короткую метку
и полный AccessibleName, а полные field+mutation diagnostics показаны в read-only
копируемой панели с переносом строк. Draft/ошибка/доступность принадлежат модели;
цветовые роли/приоритет событий описаны в [общем дизайн-коде](../alloy/DESIGN.md).

Состояния полей обновляются одним coalesced callback View. InspectorProperty.Load
сравнивает observable values до/после загрузки, не засыпает bounded64 dispatcher
неизменёнными уведомлениями. Это исправило обнаруженные selection/Cancel/Undo
регрессии без изменения dispatcher budget и без ослабления прежних однообновленных
assertions. Изменение UI не меняет Apply/Ctrl+Enter, Cancel/Esc или общую history
transaction; View по-прежнему заимствует модель и владеет только деревом/подписками.

Fresh [evidence](../../TestResults/CompactInspector-20261105/REPORT.md):
full Editor **1256/1256 Debug/Release**, failed/skipped0,12новых Editor cases.
Отдельно full Alloy **1020/1020** и Hosting **154/154** в обеих конфигурациях.
После assertions-only усиления multi-field diagnostics финальные focused
**141/141 Debug/Release** прошли; production source прежний. Новых test warnings нет,
сохраняются два прежних xUnit1031 в Diagnostics. Общая nonincremental Debug сборка
**0errors/1555warnings**; полный solution test run не выполнялся.

Команды full: `dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj
--no-restore -c Debug` и Release, с отдельными TRX/log/unique binlog.
Финальный filter: `FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Inspector|FullyQualifiedName~EditorTransformView`.
Сборка: `dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug` с `-bl`.
Точные runnable commands и artifacts — в отчёте. CPU-preview runner в ignored
TestResults получил9PNG с реальным Skia text layout; Dark/Light/narrow/состояния
визуально проверены. Manual inspector smoke обновлён для нового disclosure/reset
пути и3row identity proof; native run, fresh NuGet, runtime/GPU и physical acceptance
не повторялись. Предыдущие Window/Observer proofs остаются историческими.

Далее numeric gestures/step и linked-scale с явными границами commit/cancel;
Observer scheduling/allocation follow-up сохраняется отдельной задачей.


<a id="ed6b2e4e-observer-open-batches-2026-11-04"></a>

## ED6b.2e.4e: ограниченная параллельность native open — 2026-11-04

По запросу пользователя продолжена оптимизация native open. Baseline — предыдущий
`TestResults/ObserverNativeOpen-20261104`. Абсолютные NT API сначала сравнены отдельно:
Win32Basic51.92ms, NtOpenFull51.27ms, NtCreateFull51.92ms, minimal rights52.54ms на2567
files; преимущества для нового NT API не подтверждены. Warmed bounded parallel
open+metadata experiment дал59.37ms degree1,30.33ms degree2,16.57ms degree4.
Это exploratory open-only4round comparison, не end-to-end speedup. `Add-Type`
первого probe остановился на compile diagnostic недостижимой ветки; ветка удалена,
final probe JSON/source сохранены отдельно. Один auto-review timeout параллельного
probe повторён разрешённым retry, затем approved; unresolved approval blockers нет.
Production не получает NT calls, новые права, OS/thread-pool configuration или
persistent handle cache. Полные API/schema/dependencies и polling10s/budget5s прежние.

### Реализация, порядок ошибок и lifetime

Новый internal EditorSourceOpenBatch применяется default Census от256known files,
capacity4. Coordinator последовательно валидирует paths и один владеет directory
cache; короткие Task.Run jobs только открывают файлы/получают initial metadata.
Task.WhenAll завершает все начатые opens до передачи batch, включая cancellation.
Scheduler cancellation не используется: каждый job возвращает reader или captured
error, открытый product закрывается при cancellation после open.

Missing/exception хранятся в descriptor order, Take передаёт reader один раз.
Поздняя speculative ошибка не заменяет доказанный Changed более раннего файла.
Validation error на позиции j прекращает последующий admission, но предыдущие jobs
дожидаются завершения. File reads/SHA256/cumulative byte budget последовательны,
один64KiBbuffer; одновременно максимум4staged native file handles. Перед новым
batch предыдущий закрывается, finally закрывает всё непотреблённое перед retry,
Changed/Unavailable/cancel/exception, probes и final directory revalidation.
Stop сохраняет actual worker/open drain; отдельный synchronous OS call не получает
hard timeout/forced cancellation guarantee. Не добавлены callback IO/UI связи.

Small graphs и injected open/metadata/directory hooks сохраняют serial path. Новый
optional internal batches factory нужен для deterministic real Census/Watch drain
tests, не является App API/service lifecycle. Metadata/bytes не кэшируются; native
sharing/no-follow flags, fresh path/probe checks и directory caps прежние. Initial
snapshots будущих4files могут предшествовать чтению; sampled/ABA/неатомарные границы
наблюдения сохраняются. Любой scheduling/allocation cost оценивается вместе с
end-to-end scan, а не скрывается за native-only experiment.

### Проверки

13methods/21новый case в OpenBatchTests/CensusTests: четыре opens реально overlap,
handoff ждёт все jobs; offset/tail/invalid inputs, missing/error/validation order,
transfer-once/disposal, preCancel/cancel-after-open cleanup. Default255/256/257files
проверяют threshold, полные hashes/fresh probes/exclusive reopen; injected hooks
остаются serial. Early Changed выигрывает у later locked input. Retry factory
проверяет prior batch disposed и exclusive reopen unread product до следующего
admission. Real Watch Stop остаётся incomplete до4native products и не публикует
поздний snapshot. Test gates bounded и без timing sleeps/OS privilege changes.

Первый focused145/145 прошёл. Затем усилен cleanup самих gated tests и direct
retry-admission assertions; production не менялась. Full Editor1244/1244 отдельно
Debug/Release прошли; окончательный focused145/145 Debug иRelease после review
также прошёл, failed/skipped0. Новые files без analyzer warnings; два прежних
xUnit1031 в Diagnostics tests сохранены. Final Release harness0warnings/0errors.
Assertion-quality/static mutation review выполнены; empirical mutation/coverage
score не заявляется. Internal research/plan/status — OS temp, не intended commit.
IDE findTests отсутствует; Roslyn pairing один раз22sources/0tests в narrowEditing,
не вывод об отсутствии module tests. Предыдущие dirty files/.codex сохранены.

SDK commands последовательны/unique binlogs, native tests с ordinary user token
вне filesystem sandbox. Final focused commands используют одинаковый filter:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceFile|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceDirectory|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceNativeMetadata|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceOpenBatch|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceCensusTests' --logger 'trx;LogFileName=Focused-Review-Debug.trx' --results-directory TestResults/ObserverNativeOpen2-20261104 '-bl:TestResults/ObserverNativeOpen2-20261104/focused-review-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceFile|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceDirectory|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceNativeMetadata|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceOpenBatch|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceCensusTests' --logger 'trx;LogFileName=Focused-Review-Release.trx' --results-directory TestResults/ObserverNativeOpen2-20261104 '-bl:TestResults/ObserverNativeOpen2-20261104/focused-review-Release-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Editor-Debug.trx' --results-directory TestResults/ObserverNativeOpen2-20261104 '-bl:TestResults/ObserverNativeOpen2-20261104/editor-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Editor-Release.trx' --results-directory TestResults/ObserverNativeOpen2-20261104 '-bl:TestResults/ObserverNativeOpen2-20261104/editor-release-tests-{}.binlog'
dotnet build ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj -c Release --no-restore '-bl:TestResults/ObserverNativeOpen2-20261104/harness-final-{}.binlog'
& ./ManualTests/AetherSourceWatchPerformance/Run.ps1 -EvidenceDirectory TestResults/ObserverNativeOpen2-20261104/Performance -Profiles @('files','bytes','control') -SkipBuild
& ./ManualTests/AetherSourceWatchPerformance/Summarize.ps1 -Verification TestResults/ObserverNativeOpen2-20261104/Performance/verification.json
& ./ManualTests/AetherSourceWatchPerformance/Profile.ps1 -TraceTool TestResults/ObserverProfile-20261104/tools/dotnet-trace.exe -EvidenceDirectory TestResults/ObserverNativeOpen2-20261104/Profiles -BaselineVerification TestResults/ObserverNativeOpen2-20261104/Performance/verification.json
dotnet build TrueMoon.slnx -c Debug --no-restore --no-incremental '-bl:TestResults/ObserverNativeOpen2-20261104/solution-debug-{}.binlog'
```

No SDK/tests/analysis запускаются внутри measurement windows; общий solution build
выполняется после profile lane. Новые raw manifests/reports/traces/analysis outputs
сохраняются отдельно; старые/current traces используют один classifier с OpenBatch
worker roots. Сумма параллельного sampled thread-time не равна elapsed scan time.

### Повторный unprofiled matrix и tradeoff

Те же6isolated Release/net10 cases/default10s/5s, actual four-project descriptors,
warmed cache, Win11/Ryzen9 9900X/24logical CPUs/.NET10.0.14/SDK10.0.403. В этот раз
many-files cases выполнены первыми; baseline order был bytes/files/control. Каждый
case всё равно имеет свой process/fixture/actual Build и3steady samples. Bounds
snapshot-derived, CPU/allocation/IO whole-process+sampler+polling wait. Нет cold-disk,
physical UI, statistical или exact method-timer guarantee.

| Case | Serial-open baseline bounds ms | Batch bounds ms | CPU ms/cycle before → after | Alloc MiB/cycle before → after |
| --- | ---: | ---: | ---: | ---: |
| files-Polling | 514.26–545.47 | 312.11–335.47 | 406.25 → 500.00 | 3.66 → 4.50 |
| files-Notifications | 579.41–602.08 | 439.95–469.99 | 468.75 → 671.88 | 3.67 → 4.50 |
| bytes-Polling | 78.03–108.09 | 77.81–105.40 | 265.62 → 140.62 | 0.40 → 0.40 |
| bytes-Notifications | 77.23–109.20 | 77.63–102.34 | 203.12 → 140.62 | 0.40 → 0.40 |
| control-Polling | 0.00–30.76 | 0.00–23.12 | 62.50 → 62.50 | 0.31 → 0.31 |
| control-Notifications | 0.00–21.23 | 0.00–23.56 | 31.25 → 125.00 | 0.31 → 0.31 |

На2567files median upper bounds уменьшились на38.50% polling/21.94% notifications;
notification detection824.26→658.80ms. Цена — allocations+22.8%, CPU/cycle+23.1%/
43.3%:4.84%/6.41% одного logical core вместо3.90%/4.41%. Это latency/throughput
tradeoff, не CPU/allocation optimization. Физический объём bytes/native opens не
сокращён; OtherIO16685/cycle прежний. Byte/control graphs serial, отличия их CPU/
коротких bounds нельзя приписывать batching; small lower0 — sampler resolution.

6/6cases:24full-read Same,6same-length+mtime Changed, budget0, idle source reads0,
все owners Stopped/watchers0. Polling detection10346.85ms(files)/10148.86ms(bytes),
notifications658.80/381.79ms. Close/drain1.37–1.84ms. Sampled private peaks70.51–
120.49MiB/working set107.03–153.41MiB — whole process с retained Build memory,
не batch-only.54current source/DLL/harness input hashes без drift. Raw options,
commands/environment/descriptor provenance — Performance/verification.json/reports.

### Повторные profiles, classifier review и финальная сверка

3fresh EventPipe30s traces,2many-files repeats +byte control, прежние tool/options,
attach после Build/initial pass.3targets/collectors exit0,12Same/3Changed, budget0,
owners Stopped/watchers0, token не elevated. Old/current traces разобраны одним
final classifier; actual OpenBatch frames подтверждены в2many-files, byte control
остался serial. Stack ordering/balance/category conservation прошли.

| Trace | Census sampled thread ms before → after | Open/handle metadata/close % before → after | New unresolved context % | Batch |
| --- | ---: | ---: | ---: | --- |
| files-1 | 1191.36 → 1396.13 | 57.88 → 72.74 | 66.47 | да |
| files-2 | 1137.82 → 1394.52 | 58.89 → 75.43 | 52.96 | да |
| bytes-3 | 187.04 → 233.41 | 42.56 → 13.34 | 6.66 | нет |

Thread weights суммируют работу concurrent workers и НЕ измеряют elapsed scan time
или whole-process CPU %. Большее значение при меньшем scan wall time соответствует
tradeoff, а не опровергает unprofiled result. Open category включает metadata/close,
не isolated CreateFile syscall timer. Inlining/limited samples/неполная native
symbolization ограничивают attribution, нулевые categories не равны zero work.
Byte hash67.03% относится только к этому serial control. Следующий адресный шаг —
снижение task/allocation/scheduling overhead batch без потери concurrency/ownership;
kernel/filter-driver attribution и hardware acceptance остаются отдельными.

При review обнаружен classifier bug: frame CreateAsync может остаться ниже inline
Census continuation. Предварительный match всего EditorSourceOpenBatch относил
часть дальнейшей работы к open. Final match ограничен producer Open; прежний
classifier сохранён как AnalyzeProfile-at-collection.ps1 с hash из collection
manifest. Post-measurement изменение только analysis script записано в
analysis-revision.json; production/harness/runner inputs и binaries не менялись.
Все6old/new traces заново разобраны final revision в *-before/after-final.json,
preliminary outputs сохранены отдельно. Это исправление анализа, не новый run.

Общий nonincremental Debug build0errors/1555warnings; полный solution test run
не повторён. Aggregate audit:56collection input records (54matrix +script/tool,
classifier проверен по сохранённому snapshot),1current classifier revision check,
10827restored fixture hashes,6current raw trace hashes,9reports, drift0. Всего
36full-read Same/9content changes, budget0/actual drain. Unprofiled ReadBytes exact
descriptor bytes; diagnostic extra process IO attached targets сохранён отдельно.
SourceFile/compiler/benchmark C#/Run.ps1 и policy прежние; intended Census/new
batch/binary/analyzer differences отражены в comparison-audit.json.

Evidence: `TestResults/ObserverNativeOpen2-20261104/REPORT.md`,
`Performance/verification.json`, `Profiles/verification.json`, `comparison-audit.json`,
`analysis-revision.json`, `Comparison/*-final.json` и `CompareAndAudit.ps1`.
GPU/UI/package/window/hardware/release gates не закрываются private scanner scope.

<a id="ed6b2e4d-observer-native-open-2026-11-04"></a>

## ED6b.2e.4d: native open и opened metadata — 2026-11-04

По запросу пользователя продолжена оптимизация оставшегося ED6b.2e.4c участка.
Private EditorSourceFile/Census изменены, public API/packages/dependencies/policy
и directory-cache ownership сохранены. Базовое сравнение — непосредственно
предыдущий `TestResults/ObserverFileIO-20261104`, а не ранний uncached scanner.

### Выбор изменения по native experiment

Read-only open+metadata comparison использует предыдущий owned fixture2567files/
11.11MiB. Четыре alternating-order rounds, отдельный warmup64files/mode, без hashing/
polling/sampler в timed loop. Это exploratory warmed open-only experiment,
не полная стоимость Observer и не statistical guarantee. First/expanded JSON и
Add-Type C# source сохранены в `TestResults/ObserverNativeOpen-20261104`.

| Native variant | Expanded median ms /2567files |
| --- | ---: |
| CreateFileW +legacy handle metadata | 60.28 |
| CreateFileW +FILE_BASIC_INFO/size | 55.57 |
| Relative NtCreateFile +legacy metadata | 57.64 |
| Relative NtCreateFile +basic/size | 55.28 |
| CreateFile2 +basic/size | 56.02 |
| CreateFileW without sequential hint +basic/size | 55.92 |
| CreateFile2 without hint +basic/size | 56.39 |

Initial4mode run дал63.86ms legacy vs56.63ms basic/size; expanded7mode run подтверждает
примерно8% open-only improvement. Relative/CreateFile2/no-hint не дали устойчивого
дополнительного выигрыша относительно basic/size. Поэтому production не получает
NT APIs, borrowed parent handles или новые pin rights.2567fixture hashes после
экспериментов совпали с descriptor, drift0. `native-probe-verification.json` фиксирует
scope/source hash; source/full-framework/hardware acceptance им не заменяется.

### Реализация и safety

Opened-before metadata теперь получает FILE_BASIC_INFO40 и GetFileSizeEx вместо
legacy volume/file-ID record. Reparse/directory guard выполняется до size query;
negative size отвергается. Legacy GetFileInformationByHandle52 остаётся только
для unsupported errors1/50/87. Denied/sharing/IO failures по-прежнему дают coverage
failure и закрывают failed handle. Совместимость unsupported-provider fallback
проверена по исходникам, но на текущем local filesystem эта ветка не вызвана.
ABI сверены с [FILE_BASIC_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_basic_info),
[GetFileInformationByHandleEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex)
и [GetFileSizeEx](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfilesizeex).
LARGE_INTEGER — long, attributes DWORD — uint, BOOL — int, SafeFileHandle ownership
и SetLastError сохранены. GenericRead/shareRWD/no-follow/sequential flags прежние.

Один reader готовит extended native path один раз. Instance ReadMetadata() использует
это имя для новой path query, не заменяет её opened-handle snapshot и не следует
за переименованным исходным handle. Rename/missing/replacement/directory/reparse
occupation по original path остаются наблюдаемыми. Dispose запрещает новую instance
query. Census использует prepared name только при отсутствии injected metadata
delegate; delegate priority/counts/fresh semantics сохранены. Static probe metadata
queries также свежие, без persistent name/result cache.

Все EOF bytes по-прежнему хешируются; sizes/timestamps не заменяют content check.
Before остаётся immutable initial snapshot, final metadata всегда перечитывается.
Не добавлены atomic snapshot/file identity/ABA гарантии или hard OS IO timeout.
Directory revalidation, cumulative retry/byte budgets, cooperative cancel и actual
Stop/drain прежние. Семантические тесты не отличают эквивалентную повторную подготовку
имени; экономия подготовки/аллокаций оценивается отдельными measurements.

### Текущие тесты и команды

Два новых test files,6methods/8cases: short/long Unicode fresh fields, неизменность
Before, metadata query без изменения read position, disposed guard/exclusive reopen,
original name после rename→replacement, directory/junction occupation и cleanup,
default Census explicit long-path full hashing при same-length/mtime edits, приоритет
injected metadata над native reader и reparse failure без retained owners.
Initial focused124:122passed/2failed из-за fixture WriteAllBytes(CREATE_ALWAYS) по
hidden file. Writer заменён на FileMode.Open +SetLength/Write, hidden assertions
сохранены. Final focused124/124, full Editor1223/1223 Debug/Release, failed/skipped0.
Новые files без analyzer warnings; два прежних xUnit1031 в Diagnostics tests остаются.
Editor Release и final harness build0warnings/0errors. Assertion-quality/static
pseudo-mutation review выполнены, empirical mutation/coverage claims отсутствуют.

IDE findTests недоступен; Roslyn pairing один раз дал22sources/0tests в narrow
Editing subtree, не project-wide missing tests. Internal research/plan/status в
OS temp вне intended commit. SDK commands последовательны с unique binlogs;
native tests/fixtures используют обычный user token вне filesystem sandbox,
без elevation/ACL/privilege/OS changes. Все предыдущие dirty Window/cache files
и локальный .codex сохранены. Commands:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceFile|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceDirectory|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceNativeMetadata|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceCensusTests' --logger 'trx;LogFileName=Focused-Final-Debug.trx' --results-directory TestResults/ObserverNativeOpen-20261104 '-bl:TestResults/ObserverNativeOpen-20261104/focused-final-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Editor-Debug.trx' --results-directory TestResults/ObserverNativeOpen-20261104 '-bl:TestResults/ObserverNativeOpen-20261104/editor-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Editor-Release.trx' --results-directory TestResults/ObserverNativeOpen-20261104 '-bl:TestResults/ObserverNativeOpen-20261104/editor-release-tests-{}.binlog'
dotnet build ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj -c Release --no-restore '-bl:TestResults/ObserverNativeOpen-20261104/harness-final-{}.binlog'
& ./ManualTests/AetherSourceWatchPerformance/Run.ps1 -EvidenceDirectory TestResults/ObserverNativeOpen-20261104/Performance -Profiles @('bytes','files','control') -SkipBuild
& ./ManualTests/AetherSourceWatchPerformance/Summarize.ps1 -Verification TestResults/ObserverNativeOpen-20261104/Performance/verification.json
& ./ManualTests/AetherSourceWatchPerformance/Profile.ps1 -TraceTool TestResults/ObserverProfile-20261104/tools/dotnet-trace.exe -EvidenceDirectory TestResults/ObserverNativeOpen-20261104/Profiles -BaselineVerification TestResults/ObserverNativeOpen-20261104/Performance/verification.json
dotnet build TrueMoon.slnx -c Debug --no-restore --no-incremental '-bl:TestResults/ObserverNativeOpen-20261104/solution-debug-{}.binlog'
```

Полный solution build выполняется после measurement windows. Raw TRX/logs/binlogs,
performance/profiles manifests/reports/traces сохраняются в новом ignored evidence
directory; прежние outputs не перезаписываются. Старые/new traces анализируются
одним текущим classifier, включая ReadOpenedMetadata в open category.

### Повторный unprofiled Observer matrix

Прежние Release/net10.0, Windows11/Ryzen9 9900X/24logical CPUs/.NET10.0.14/
SDK10.0.403, actual four-project descriptors, warmed caches,10s polling/5s budget.
По3steady samples; snapshot-derived bounds и process-wide CPU/allocation/logical IO,
включая sampler и polling wait. Build/setup/final hashes вне measured windows.

| Case | File-IO baseline scan bounds ms | Native metadata scan bounds ms | CPU ms/cycle before → after | Other IO ops before → after | Allocation MiB before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| bytes-Polling | 77.63–109.25 | 78.03–108.09 | 296.88 → 265.62 | 1406 → 1128 | 0.42 → 0.40 |
| bytes-Notifications | 78.14–103.51 | 77.23–109.20 | 234.38 → 203.12 | 1406 → 1128 | 0.42 → 0.40 |
| files-Polling | 588.45–613.58 | 514.26–545.47 | 453.12 → 406.25 | 21819 → 16685 | 4.08 → 3.66 |
| files-Notifications | 560.74–592.55 | 579.41–602.08 | 421.88 → 468.75 | 21819 → 16685 | 4.08 → 3.67 |
| control-Polling | 0.00–23.10 | 0.00–30.76 | 187.50 → 62.50 | 862 → 720 | 0.32 → 0.31 |
| control-Notifications | 0.00–21.40 | 0.00–21.23 | 125.00 → 31.25 | 862 → 720 | 0.32 → 0.31 |

На2567files allocations снизились на10.1%, OtherOperations — на5134/cycle (2per-file
по process counters). Это logical IO observation, не instrumented syscall count.
Polling median upper bound снизился на11.10%; notifications выше baseline на1.61%,
его CPU/cycle также выше. Byte notifications и малый polling не улучшили upper bound.
Общий latency/CPU speedup на всех profiles не заявляется;3samples и заметный разброс
не являются statistical guarantee. Control lower bound0 означает sampler resolution,
не zero cost. Memory peaks whole-process/retained Build, не reader-only:
private75.73–131.82MiB, working set113.29–168.98MiB.

6/6cases,24full-read Same,6same-length+mtime Changed, budget0/owners Stopped/watchers0;
idle source reads0. Notifications detection824.26ms(files)/389.08ms(bytes),
polling10545.94/10114.90ms; close/drain1.47–1.91ms.53source/DLL/harness hashes
без drift. Actual byte counts/options/environment, descriptor provenance и case
commands сохранены в Performance/verification.json +per-case report.json.

### Повторные profiles и финальная сверка

3fresh EventPipe30s traces на тех же default polling options/tool/buffer, attach
после actual Build/initial pass:2many-files +byte control.3targets/collectors exit0,
12Same/3same-length+mtime Changed, budget0, owners Stopped/watchers0, token не elevated.
Old file-IO и new traces разобраны одним текущим AnalyzeProfile с новыми output
filenames; старые outputs сохранены. Stack/order/category conservation прошли.

| Trace | Census sampled thread ms before → after | Open/handle metadata/close % before → after | Path metadata % before → after | New unresolved context % |
| --- | ---: | ---: | ---: | ---: |
| files-1 | 1190.67 → 1191.36 | 59.23 → 57.88 | 2.52 → 14.57 | 62.00 |
| files-2 | 1228.87 → 1137.82 | 63.22 → 58.89 | 8.96 → 9.59 | 63.27 |
| bytes-3 | 200.41 → 187.04 | 30.91 → 42.56 | нет samples → 8.04 | 50.15 |

Open остаётся главным resolved участком many-files. Profile weights — sampled
managed thread-time conditional на Census/owned helpers, не CPU %, не полный async
wall breakdown и не isolated CreateFile/metadata syscall time. Новое разделение
ReadOpenedMetadata/QueryMetadata и inlining меняют caller attribution: увеличение
видимой path metadata share не доказывает изменение стоимости самой path query.
Нулевые cache/ancestor samples не означают zero work; sources cache не менялись,
его tests прошли. Unresolved native/inlined context62–63% в repeats ограничивает
следующий вывод. Ни uniform speedup, ни точный kernel/filter-driver/lost-event
разбор не заявлены. Следующий шаг — отдельная native/OS attribution оставшегося
open cost; новые optimization decisions принимать по ней, сохраняя full hashing.

Общий nonincremental Debug build прошёл0errors/1555warnings, полный solution test
run не повторялся. Final aggregate audit:55current input hashes (53matrix +script/
tool),10827restored fixture file hashes,6raw trace hashes,9reports, drift0. Всего
36full-read Same/9content changes в matrix+profiles. Unprofiled ReadBytes exact
descriptor bytes; extra process IO attached targets фиксируется отдельно.
Benchmark C#/project/Run.ps1, compiler и directory-cache sources совпадают с
file-IO baseline; intentional SourceFile/Census/binaries/analyzer differences
записаны в comparison-audit.json. Experiments и test intermediates не предназначены
для commit. UI/GPU/package/hardware/release gates сохраняют собственный scope.

Evidence: `TestResults/ObserverNativeOpen-20261104/REPORT.md`,
`comparison-audit.json`, `Performance/verification.json`, `Profiles/verification.json`,
`Comparison/*-before/after.json`, `CompareAndAudit.ps1`; initial failed/final passing
test artifacts сохранены отдельно. Native open-only experiment не заменяет эти
whole Observer measurements и не выдаётся за полноценный OS trace.

<a id="ed6b2e4c-observer-file-io-2026-11-04"></a>

## ED6b.2e.4c: открытие файлов и file/probe metadata — 2026-11-04

По запросу пользователя оптимизированы подтверждённые ED6b.2e.4b costs: constructor
OSFileStreamStrategy42.59–42.70% inclusive и file/probe metadata32.90–34.16% census
sampled thread weight. Private/internal EditorSourceFile добавлен в Editing;
публичные API, dependencies/packages, descriptor, App/DI/lifecycle не менялись.
Предыдущий directory-cache baseline — ignored `TestResults/ObserverCache-20261104`;
его binaries/hashes намеренно отличаются от нового implementation snapshot.

### Изменение file IO и границы

На один known file теперь один owned SafeFileHandle: CreateFileW с GENERIC_READ,
READ|WRITE|DELETE sharing, OPEN_EXISTING и SEQUENTIAL_SCAN|OPEN_REPARSE_POINT
(0x08200000). Нет OVERLAPPED/IOCP binding, дополнительного FileStream buffer или
queued continuation на каждый small-file read. ReadFile синхронно заполняет общий
64KiB Census buffer, ReadAsync возвращает completed ValueTask. Это IO на прежнем
Task.Run Observer worker, не на UI thread. Cancellation проверяется до/после read;
Stop ждёт actual completion перед disposal. Отдельный OS read не получает hard
timeout/cancel guarantee; cooperative scan budget5s и polling10s сохранены.
BACKUP_SEMANTICS в file-open flags отсутствует, privilege/ACL/OS settings не менялись.

Attributes/length/raw FILETIME перед чтением берутся одним GetFileInformationByHandle
с открытого no-follow handle. После EOF — свежая GetFileAttributesExW query по
исходному path, проверяющая отсутствие reparse/directory, те же length/write time
и фактически прочитанный объём. Full SHA256 сравнивается с frozen descriptor,
включая same-length/same-mtime edits. Это sampled observation, не атомарный snapshot
и не обещание обнаружить transient change/revert или любую file replacement ABA.
Read/write/delete sharing сохраняет возможность saves/rename/delete во время read.

Native WIN32_FILE_ATTRIBUTE_DATA36bytes даёт attributes/length/time одним вызовом;
BY_HANDLE_FILE_INFORMATION52bytes содержит Volume перед SizeHigh/SizeLow. DWORD и
FILETIME представлены uint fields, BOOL — int, UTF16 LibraryImport/SafeFileHandle
с SetLastError. Native declarations сверены с
[Microsoft layout](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information),
[attribute data](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-win32_file_attribute_data)
и [ReadFile contract](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-readfile).
Create/query rejection закрывает свой handle; directory occupancy определяется
без traversal и без retained file owner. Reparse отвергается при open и final query.

Probes получают fresh metadata без FileInfo/исключений для ожидаемого отсутствия.
Только Win32 errors2/3 возвращают null; denied/sharing/прочие ошибки дают retryable
coverage failure, а reparse/occupied probe directory — permanent Unavailable.
Known-file probes не используют metadata, сохранённые после hashing: presence
проверяется заново в probe phase. Metadata/file contents не кэшируются между reads,
attempts или passes. Directory cache/pins/final revalidation, tree membership,
exclusions, full byte hashing и cumulative retry budgets сохранены.

Прежний injected stream hook остаётся: для него выполняются path-before/path-after
queries. Новый optional internal metadata delegate принадлежит одному Census и
нужен для deterministic error/race assertions; globals/public API не добавлены.

### Текущие проверки

Два новых test files: EditorSourceFileTests и EditorSourceFileCensusTests,
17methods/41cases. Native read0/1/65535/65536/65537bytes, span slice guards/EOF,
synchronous ValueTask/cancel-no-consumption, read/write/delete sharing, long Unicode
paths, missing/directory/locked/junction cleanup и HRESULT mapping проверены.
Census tests подтверждают opened-before metadata/fresh final query, повторный
known-file probe, denied/IO/security/reparse/loss/instability retries без false Same,
fresh recovery, cancellation/disposal и oversize-before-open для injected path.
Существующие byte-budget/retry/hash/membership/probe/directory-cache regressions
прошли с новой реализацией.

Первый focused116/116 passed. Static review добавил peer reader assertion для
SHARE_READ; два новых CA2022 в expected-throw async assertions исправлены capture
count +Assert.Fail, без suppression. Итоговый focused116/116 и full Editor1215/1215
отдельно Debug/Release прошли, failed/skipped0. Новые files без analyzer warnings;
остались два прежних xUnit1031 в EditorFullDiagnosticReportTests.cs130/132.
Release Editor и финальная harness build прошли0warnings/0errors.
Assertion-quality и static pseudo-mutation review выполнены; empirical mutation
score/coverage percentage не заявляются. Промежуточные research/plan/status хранятся
в OS temp, не среди intended commit files. IDE findTests недоступен (Tool not found
после уточнения rootFolder); existing tests/helpers прочитаны напрямую. Roslyn
parse-only pairing выполнен однажды: narrow Editing21sources/0tests не означает
отсутствие тестов у модуля.

Все SDK invocations последовательные, с уникальными binlogs. Native tests и
performance/profile работают с обычным пользовательским token вне filesystem
sandbox, без elevation/admin/privilege changes; junctions только owned fixtures.
Commands (полный solution build выполняется после measurement windows):

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceFile|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceDirectory|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceCensusTests' --logger 'trx;LogFileName=Focused-Final-Debug.trx' --results-directory TestResults/ObserverFileIO-20261104 '-bl:TestResults/ObserverFileIO-20261104/focused-final-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Editor-Debug.trx' --results-directory TestResults/ObserverFileIO-20261104 '-bl:TestResults/ObserverFileIO-20261104/editor-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Editor-Release.trx' --results-directory TestResults/ObserverFileIO-20261104 '-bl:TestResults/ObserverFileIO-20261104/editor-release-tests-{}.binlog'
dotnet build ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj -c Release --no-restore '-bl:TestResults/ObserverFileIO-20261104/harness-final-{}.binlog'
& ./ManualTests/AetherSourceWatchPerformance/Run.ps1 -EvidenceDirectory TestResults/ObserverFileIO-20261104/Performance -Profiles @('bytes','files','control') -SkipBuild
& ./ManualTests/AetherSourceWatchPerformance/Summarize.ps1 -Verification TestResults/ObserverFileIO-20261104/Performance/verification.json
& ./ManualTests/AetherSourceWatchPerformance/Profile.ps1 -TraceTool TestResults/ObserverProfile-20261104/tools/dotnet-trace.exe -EvidenceDirectory TestResults/ObserverFileIO-20261104/Profiles -BaselineVerification TestResults/ObserverFileIO-20261104/Performance/verification.json
dotnet build TrueMoon.slnx -c Debug --no-restore --no-incremental '-bl:TestResults/ObserverFileIO-20261104/solution-debug-{}.binlog'
```

В новом `TestResults/ObserverFileIO-20261104` сохраняются TRX/logs/binlogs,
Performance/Profiles manifests и raw reports/traces. Один обновлённый AnalyzeProfile
повторно разбирает previous cache и new traces, сохраняя Comparison/*-before/after.json.
Классификация теперь учитывает EditorSourceFile и OSFileStreamStrategy constructor;
open category включает initial handle metadata/close и не выдаётся за isolated
CreateFile syscall timer. Прежние conservative outputs не перезаписываются.

### Повторная unprofiled matrix после file IO изменений

Та же машина/Release/net10/.NET10.0.14/SDK10.0.403, warmed cache и actual four-project
descriptors, по3steady samples. Snapshot-derived bounds и process-wide CPU/alloc/IO
включают sampler и polling wait; generation/Build/final hashes за пределами windows.
Сравнение с непосредственно предыдущим directory-cache baseline:

| Case | Cache baseline scan bounds ms | File IO scan bounds ms | CPU ms/cycle before → after | Other IO ops before → after | Allocation MiB before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| bytes-Polling | 109.11–130.42 | 77.63–109.25 | 250.00 → 296.88 | 1819 → 1406 | 0.50 → 0.42 |
| bytes-Notifications | 109.02–136.33 | 78.14–103.51 | 343.75 → 234.38 | 1819 → 1406 | 0.50 → 0.42 |
| files-Polling | 821.78–846.38 | 588.45–613.58 | 718.75 → 453.12 | 29515 → 21819 | 4.95 → 4.08 |
| files-Notifications | 827.70–854.66 | 560.74–592.55 | 750.00 → 421.88 | 29515 → 21819 | 4.95 → 4.08 |
| control-Polling | 0.00–29.10 | 0.00–23.10 | 62.50 → 187.50 | 1072 → 862 | 0.37 → 0.32 |
| control-Notifications | 0.00–27.88 | 0.00–21.40 | 93.75 → 125.00 | 1071 → 862 | 0.37 → 0.32 |

Many-files median upper bounds снизились ещё на27.50% polling/30.67% notifications,
allocation на17.6%; process CPU/cycle4.28%/3.99% одного logical core вместо6.63%/6.92%.
Byte/control scan bounds и allocations уменьшились, но bytes-Polling/control CPU
выше baseline. Нельзя объявлять universal CPU improvement, вычитать короткий idle
как точный observer cost или объяснять этот рост без отдельного attribution proof.
Memory peaks whole-process/retained Build, не reader-only: private70.50–142.60MiB,
working set106.97–180.03MiB. Small control lower bound0 — ограничение resolution,
не zero scan cost. Three-sample warmed-cache сравнение не statistical guarantee.

6/6cases,24initial+steady Same с exact descriptor read bytes,6same-length/mtime
Changed, budget0, все owners Stopped/watchers0, idle source reads0. Notifications
detection822.39ms(files)/402.09ms(bytes); polling10577.42/10121.62ms. Close/drain
1.45–2.01ms.53current source/DLL/harness hashes без drift. Actual sizes/counts,
compiler descriptors, commands/options/environment — в Performance/verification.json
и per-case report.json; previous baseline raw evidence сохранены.

### Повторное профилирование и итоговая сверка

Три fresh EventPipe30s traces тем же local Microsoft dotnet-trace10.0.750501:
files-1/files-2/bytes-3, attach после Build/initial pass,64MiB buffer, прежний
polling-only режим. Token не elevated,3targets/3collectors exit0,12Same/3Changed,
budget0, все owners Stopped/watchers0. Старые cache и новые traces повторно
разобраны одним текущим analyzer, balanced stack/order/category conservation прошли.

| Trace | Census thread weight ms before → after | Open/handle metadata/close % before → after | File/probe path metadata % before → after | New unresolved caller context % |
| --- | ---: | ---: | ---: | ---: |
| files-1 | 1674.16 → 1190.67 | 44.87 → 59.23 | 32.90 → 2.52 | 57.93 |
| files-2 | 1696.15 → 1228.87 | 44.48 → 63.22 | 34.16 → 8.96 | 51.87 |
| bytes-3 | 225.64 → 200.41 | 23.64 → 30.91 | 17.82 → нет samples | 23.21 |

Это disjoint sampled managed thread weights в контексте Census/owned IO helpers,
не CPU %, не полный async wall-time breakdown и не independent syscall timers.
Async→sync меняет форму стеков; inlining/sampling оставляют часть работы в caller/
Other census frames. В files-1/bytes-3 cache frames не попали в samples, в files-2
видны pins1.26%; это не отсутствие ancestor checks: directory-cache sources
не менялись, его native/regression tests прошли. Нулевой category sample не равен
нулевой стоимости. Profile totals не используются как unprofiled speedup estimate.

Initial metadata теперь входит в Open; поэтому рост доли open category и перенос
работы из FileInfo не являются изолированным CreateFile regression/improvement.
Оставшийся основной resolved участок — native Open +handle metadata/close59–63%.
В byte control доминирует hashing46.37% и read22.72%. Точный kernel/filesystem/
filter-driver cost и dropped-event accounting этими EventPipe данными не доказаны.
Следующий конкретный Observer шаг — отдельная native/OS attribution открытия
файлов; выбирать новую оптимизацию по ней, сохраняя обязательный full hashing.
Повторное внедрение timestamp/content/handle cache этим результатом не разрешается.

Общая nonincremental Debug сборка TrueMoon.slnx прошла0errors/1555warnings;
полный solution test run не повторён. Aggregate audit:55current input hashes
(53matrix +collector script/tool),10827restored fixture hashes,6raw trace hashes,
9reports, drift0. Всего36full Same/9same-length+mtime Changed в matrix+profiles,
budget0/owners drained. Unprofiled process ReadBytes exact descriptor bytes;
attached targets имеют diagnostic extra233/0/28bytes в steady windows, записанные
отдельно, а не объявленные source-only IO. Benchmark C#/project/Run.ps1 и compiler
sources совпали с cache baseline; intended Census/new reader/binaries/analyzer
различия записаны в `comparison-audit.json`.

Читаемый отчёт — `TestResults/ObserverFileIO-20261104/REPORT.md`, raw manifests —
`Performance/verification.json` и `Profiles/verification.json`, повторный анализ —
`Comparison/*-before/after.json`, aggregate verification — `comparison-audit.json`.
`CompareAndAudit.ps1` воспроизводит проверки/таблицы; test state остаётся в OS temp.
UI/GPU/window/package/native consumer и hardware/release/cold-disk/network gates
не закрываются private scanner tests/performance; прежние passing results ниже
сохраняют собственный scope. Новый native file IO проверен текущими тестами/graph runs.

<a id="ed6b2e4b-observer-directory-cache-2026-11-04"></a>

## ED6b.2e.4b: кэш каталогов и повторное профилирование — 2026-11-04

По запросу пользователя реализован private/internal per-attempt ancestor cache
в EditorSourceCensus: новые EditorSourceDirectoryCache и EditorSourceDirectoryPin,
без изменения public API/packages/dependencies/descriptor и scheduling limits.
Общий baseline — предыдущие ED6b.2e.4 performance и ED6b.2e.4a traces ниже;
их passing результаты не выдаются за новые проверки.

### Границы reuse и native ownership

OrdinalIgnoreCase positive cache ограничен 256каталогами. При первом обращении
цепочка собирается итеративно и закрепляется root-first. Cached descendant допустим
только при закреплённой цепочке до drive root. Windows CreateFileW использует
LIST_DIRECTORY|READ_ATTRIBUTES (0x81), READ|WRITE sharing (3), OPEN_EXISTING,
BACKUP_SEMANTICS|OPEN_REPARSE_POINT (0x02200000). Owned SafeFileHandle проверяет
FILE_ATTRIBUTE_TAG_INFO; opened reparse отвергается без traversal/retained handle.
Отсутствие DELETE sharing запрещает rename/replacement каталога до disposal,
при этом child-file writes/add/rename/delete остаются допустимыми.

Pins optional: при обычном native IO/access failure выполняется fresh path metadata
check, без memoization всей незакреплённой цепочки. Поэтому новый кэш не требует
list permission там, где раньше достаточно было metadata permission. Missing,
non-directory, failed acquisition/admission и overflow не становятся cache hits.
За пределами cap прежние ancestor checks продолжаются, новый coverage limit не вводится.

Sharing не запрещает in-place ACL/attribute/reparse changes. Непосредственно перед
Same все cached directories заново проверяются по path: persistent reparse, access
error, исчезновение или изменение типа отвергаются. Это sampled observation с
прежними race bounds, не атомарный filesystem snapshot и не обещание поймать
невидимый transient change/revert. Полные bytes/hashes, tree membership, probes,
exclusions и cumulative retry budget не заменены metadata/timestamp shortcut.

Одна попытка Census владеет кэшем через using; следующая retry получает новый после
actual disposal предыдущего. Early Changed/Unavailable/cancel/error также закрывают
владельцев. Reverse cleanup идёт до конца с aggregation ошибок; rejected product
с failed cleanup остаётся отдельным owned product для Dispose retry и никогда
не считается validated. Default SafeFileHandle disposal не бросает; seam не обещает
автоматически восстановить навсегда падающий custom disposer без владельца.
Новый typed internal pin/attribute delegate seam не вводит globals или public DI API.

### Текущие проверки и исправления, найденные тестами

Добавлены27test methods /48cases в трёх новых Editing test files: cap0/1/255/256,
case-insensitive/root-first reuse, fresh overflow, missing/non-directory/unpinnable,
final reparse/denied/IO/security/loss, isolation across attempts/calls, cancel,
cleanup errors/retry и настоящие Windows rename/child IO/junction случаи.
Junctions создаются только между owned GUID fixture children без ACL/OS изменений.

Первый focused Debug72cases дал44passed/28failed: READ_ATTRIBUTES-only handle не
создавал rename fence, а sandbox не разрешал native open user ancestors. Исправлен
desired access на LIST_DIRECTORY|READ_ATTRIBUTES; optional pin fallback сохраняет
metadata-only semantics. Assertions не ослаблены. Повтор ordinary-user access дал
74/75: единственная ошибка была в test fixture, повторно вычислявшей Tree через
project после его успешного rename. Сохранены исходные path strings до move.
Final focused **75/75**, failed/skipped0, включая48новых и27existing Census cases.
Full Editor **1174/1174 Debug и1174/1174 Release**, failed/skipped0. Только два
прежних xUnit1031 предупреждения в EditorFullDiagnosticReportTests.cs130/132,
новые тесты без analyzer warnings. Финальная Release harness build:0warnings/0errors.
После test phases выполнен полный nonincremental Debug build TrueMoon.slnx:
0errors/1555warnings. Это новая общая сборка, не полный solution test run.

SDK commands выполнялись последовательно; native tests/performance использовали
обычный user token вне filesystem sandbox, без elevation/admin/privilege changes.
Static source pairing выполнен однажды; narrow Editing subtree19sources/0tests
не является project-wide missing-test выводом. IDE findTests оказался недоступен
(`Tool findTests not found`), существующие Census tests/helpers прочитаны напрямую.
Assertion-quality и pseudo-mutation review выполнены; empirical mutation score
и coverage percentage не заявляются.

Команды (у каждого SDK invocation уникальный `-bl:...-{}.binlog`, restore не повторялся):

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceDirectory|FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Editing.EditorSourceCensusTests' --logger 'trx;LogFileName=CacheFocused-Final-Debug.trx' --results-directory TestResults/ObserverCache-20261104 '-bl:TestResults/ObserverCache-20261104/focused-final-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore --logger 'trx;LogFileName=Editor-Debug.trx' --results-directory TestResults/ObserverCache-20261104 '-bl:TestResults/ObserverCache-20261104/editor-debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Editor-Release.trx' --results-directory TestResults/ObserverCache-20261104 '-bl:TestResults/ObserverCache-20261104/editor-release-tests-{}.binlog'
dotnet build ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj -c Release --no-restore '-bl:TestResults/ObserverCache-20261104/harness-final-{}.binlog'
dotnet build TrueMoon.slnx -c Debug --no-restore --no-incremental '-bl:TestResults/ObserverCache-20261104/solution-debug-{}.binlog'
& ./ManualTests/AetherSourceWatchPerformance/Run.ps1 -EvidenceDirectory TestResults/ObserverCache-20261104/Performance -Profiles @('bytes','files','control') -SkipBuild
& ./ManualTests/AetherSourceWatchPerformance/Summarize.ps1 -Verification TestResults/ObserverCache-20261104/Performance/verification.json
& ./ManualTests/AetherSourceWatchPerformance/Profile.ps1 -TraceTool TestResults/ObserverProfile-20261104/tools/dotnet-trace.exe -EvidenceDirectory TestResults/ObserverCache-20261104/Profiles -BaselineVerification TestResults/ObserverCache-20261104/Performance/verification.json
```

Артефакты — ignored `TestResults/ObserverCache-20261104`: исходные failed/retry/final
TRX/logs/binlogs сохранены отдельно; Performance и Profiles получают собственные
immutable manifests/raw reports/traces. AnalyzeProfile теперь учитывает cache/pin
frames, отдельную pin category и явно называет ValidationModel legacy comparison,
не фактическим optimized syscall count. Один и тот же analyzer применяется к старым
и новым traces с новыми output filenames; прежние evidence не перезаписываются.

### Повторный unprofiled performance matrix

Те же Windows11/AMD Ryzen9 9900X/24logical CPUs/.NET10.0.14/SDK10.0.403,
Release/net10, warmed filesystem cache и actual four-project diamond descriptors.
Шесть isolated sequential processes, по initial+3steady passes, обычные10s/5s.
Build/generation/final hashes вне measured windows; короткие idle windows не
вычитаются как точная observer CPU attribution. Медианы трёх steady samples:

| Case | Baseline scan bounds ms | Cache scan bounds ms | Process CPU ms/cycle before → after | Other IO ops before → after | Allocation MiB before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| bytes-Polling | 155.89–181.64 | 109.11–130.42 | 265.62 → 250.00 | 5468 → 1819 | 0.62 → 0.50 |
| bytes-Notifications | 156.00–179.72 | 109.02–136.33 | 312.50 → 343.75 | 5468 → 1819 | 0.62 → 0.50 |
| files-Polling | 1494.21–1524.24 | 821.78–846.38 | 1359.38 → 718.75 | 81200 → 29515 | 6.75 → 4.95 |
| files-Notifications | 1265.92–1291.21 | 827.70–854.66 | 1343.75 → 750.00 | 81200 → 29515 | 6.75 → 4.95 |
| control-Polling | 31.95–61.98 | 0.00–29.10 | 156.25 → 62.50 | 3368 → 1072 | 0.44 → 0.37 |
| control-Notifications | 16.02–45.17 | 0.00–27.88 | 62.50 → 93.75 | 3368 → 1071 | 0.44 → 0.37 |

Для2567files/11.11MiB median upper bound сократился на44.47% polling и33.81%
notifications; process CPU/cycle6.63%/6.92% одного logical core вместо11.81%/11.74%.
129.11MiB control уменьшил scan bounds на24–28%, но process CPU в notifications
выше baseline (343.75vs312.50ms); общей гарантии CPU improvement для всех cases нет.
Control scans короче sampler resolution, поэтому lower bound0 не означает zero cost.
Это snapshot bounds/three-sample measured comparison, не statistical guarantee.
OtherOperations — process logical IO, не точный syscall counter или physical disk IO.

6/6cases прошли, все24initial+steady Same прочитали exact descriptor bytes;
6same-length+same-mtime edits обнаружены, budget expiry0, idle source reads0,
Stopped/watchers0. Notifications detection1115.57ms(files)/396.04ms(bytes),
polling10838.49/10148.15ms; close/drain1.56–2.13ms. Sampled private peak78.89–136.64MiB,
working set115.27–171.52MiB — whole process с retained Build memory, не cache-only.
52production/harness/loaded DLL input hashes совпали до/после matrix.

### Повторные profiles, оставшийся расход и финальный audit

Три fresh EventPipe collections тем же local Microsoft dotnet-trace10.0.750501:
files-1/files-2/bytes-3,30s после Build/initial pass, прежние64MiB/settings,
3targets и3collectors exit0, token не elevated.12Same/3same-length+mtime Changed,
budget0, все owners Stopped/watchers0. Один текущий AnalyzeProfile.ps1 повторно
разобрал старые и новые Speedscope traces в Comparison/*-before/after.json,
не меняя старые outputs. Event ordering/stack balance/weight conservation прошли.

| Trace | Census sampled thread weight ms before → after | Ancestor attributes % before → after | Cache pin category % | Other file/probe metadata % | Unresolved caller context % |
| --- | ---: | ---: | ---: | ---: | ---: |
| files-1 | 2785.32 → 1674.16 | 39.95 → 0.67 | 0.47 | 32.90 | 55.99 |
| files-2 | 3040.30 → 1696.15 | 41.90 → 1.07 | 0.48 | 34.16 | 55.91 |
| bytes-3 | 219.67 → 225.64 | 6.80 → 1.09 | в samples не выделена | 17.82 | 41.65 |

Это условный sampled managed thread time реальных Census/cache/pin frames,
не CPU percent и не полный async wall-time breakdown; profile totals не подменяют
unprofiled speedup. Native/inlined unresolved caller context55.91–55.99% у files
ограничивает attribution: точный kernel/syscall cost и dropped events не установлены.
Conservative disjoint classifier оставляет OSFileStreamStrategy..ctor в Other
census frames; его отдельно проверенный inclusive caller weight42.59–42.70%
не суммируется с category shares. FileInfo.Exists16.49–17.47% и
Census.Attributes16.31–16.64% составляют resolved metadata cost. Следующий
конкретный Observer шаг — уточнить per-file open/constructor/native и metadata
cost, затем выбирать следующую адресную оптимизацию без ослабления byte/race fences.
Directory ancestor validation больше не основной resolved hot path.

Final aggregate audit:54current input hashes (52matrix +collector script/tool),
10827restored fixture file hashes,6raw trace hashes,9reports, drift0. Всего36Same
и9content changes в matrix+profiles. Unprofiled ReadBytes exact descriptor bytes;
attached process имеет ещё233/0/28bytes в steady windows во всех трёх cases.
Initial audit ошибочно требовал source-only equality от process-wide counter и
остановился; final audit требует equality без collector и >=descriptor с collector,
сохраняя каждый extra byte count. Production/tests/измерения не менялись.
Diagnostic IPC в attached process ограничивает интерпретацию IO этих окон.

Полный машинный audit и сравнение: `TestResults/ObserverCache-20261104/comparison-audit.json`;
читаемый отчёт: `TestResults/ObserverCache-20261104/REPORT.md`; reproducible audit:
`CompareAndAudit.ps1`, original audit note — `audit-attempts.md`. Native directory
tests/profile запускались, UI/GPU/window/package acceptance в этом срезе не повторена.
Hardware/release/cold-disk/network и полные ED4/ED6 gates остаются отдельными.

<a id="ed6b2e4a-observer-profile-2026-11-04"></a>

## ED6b.2e.4a: конкретное узкое место Observer — 2026-11-04

По запросу пользователя выполнено call-stack profiling вместо дальнейшего вывода
только по объёму/числу файлов. Microsoft dotnet-trace10.0.750501 установлен локально
в ignored `TestResults/ObserverProfile-20261104/tools`, version commit
`ad724c4b85e7ae775e42f393c6e95a7557a57954`; project dependencies/SDK/global tools и
production C# не менялись. Token не elevated; использован EventPipe, не kernel ETW.
Все48source/framework/harness hashes предыдущего baseline совпали до/после сборов.
Новые manual scripts — Profile.ps1 и AnalyzeProfile.ps1, docs/README обновлены.

### Сбор и scope

Тот же public Observer/Release harness/actual frozen four-project descriptor,
Windows11/AMD Ryzen9 9900X/24logical processors/.NET10.0.14 из baseline. Profiles:
два повтора2567known files/11.11MiB и один139files/129.11MiB control. Notifications
disabled, остальные default10s polling after completion/5s budget прежние.
Каждый target создан runner, PID дополнительно подтверждён `dotnet-trace ps`;
чужие command lines не сохраняются. Attach начинается после successful frozen Build
и initial observation, на первом steady polling wait, поэтому source generation,
SDK/compilation и первоначальная JIT/attach hints фаза вне исследуемого scanner scope.

Collection: `dotnet-common,dotnet-sampled-thread-time`,64MiB buffer,30s duration,
NetTrace +local Speedscope conversion/rundown. Target продолжает полный benchmark,
включая3steady observations, same-length/same-mtime edit/revert/sticky Changed и
actual drain.3targets и3collectors exit0,12initial/steady Same passes,3Changed,
budget-exceeded0, Stopped/watchers0, source-restoration assertions прошли.
В профиле captured worker thread-time, включая возможное ожидание; это не raw CPU
cycles. Global topN в основном показывает idle/provider waits: анализировать его
проценты как долю стоимости census нельзя.

### Подтверждённый hot path

Analyzer replay проверяет chronological/balanced Speedscope stacks и берёт только
интервалы с frame настоящего EditorSourceCensus. Categories взаимоисключающие;
их сумма и сумма leaf weights равны census weight. Inclusive method totals
пересекаются и отдельно не складываются. Веса по resolved caller context:

| Case | Census sampled thread-time, ms | ValidatePath → ancestor Attributes | File/probe metadata вне ValidatePath | Other census frames | Unresolved frames в census context |
| --- | --- | --- | --- | --- | --- |
| files repeat1 |2785.32 |39.95% |20.52% |28.35% |37.70% |
| files repeat2 |3040.30 |41.90% |18.72% |23.71% |29.32% |
| bytes control |219.67 |6.80% |7.29% |<0.01% |21.86% |

Resolved ancestor checking — наибольшая branch в обоих many-files repeats;
вместе с file/probe metadata это60.47–60.62% census stack weight. Tree enumeration+
entry attributes4.04–4.48%, resolved file read2.78–8.62%, open/handle/dispose2.01–2.25%,
hash creation/update/finalization0.56–1.00%; unknown/inlined native work не объявляется
исследованным. Byte control смещается к resolved hash43.20% и open/handle/dispose35.93%.
Профилировщик добавляет overhead; эти значения не подменяют unprofiled wall/CPU
baseline ED6b.2e.4 и не являются полным async critical-path breakdown.

Конкретный код:
[EditorSourceCensus.ValidatePath](../../TrueMoon.Aether.Editor/Editing/EditorSourceCensus.cs)
для каждого root/file/probe проходит `Path.GetDirectoryName` до drive root и вызывает
Attributes для каждого ancestor. Helper вызывает File.GetAttributes; common parents
не дедуплицируются между файлами. Из реального descriptor и неизменённого loop:

| Graph | ValidatePath calls | Ancestor Attributes visits на clean pass | Distinct ancestors | Повторные visits |
| --- | --- | --- | --- | --- |
| files, каждый repeat |2655 |26203 |103 |26100 |
| bytes control |227 |1919 |27 |1892 |

Это source-derived count обязательных loop iterations при полном clean pass,
не API instrumentation/syscall count; retries могут добавить visits, а tree/file/
probe checks вне ValidatePath идут дополнительно. Таким образом не только количество
файлов, но и конкретный повторяемый путь затрат установлен двумя traces плюс code model.
Native kernel/driver attribution и независимое lost-event accounting не собраны;
unresolved frames сохраняют видимый managed caller, а не получают выдуманный native symbol.

### Следующее изменение

Адресный target — bounded deduplication/reuse общей ancestor validation в пределах
pass. До реализации выбрать и проверить safety contract для ancestor rename/replacement,
reparse changes, absent/access-error probes, IO retries и actual cancellation/drain.
Не заменять content hashes timestamp cache, не менять census exclusions/limits и
не переносить cache между owners/baselines без отдельного контракта. Native safety
и ABA/race fences важнее механического удаления Attributes calls. Production
оптимизация в этом задании не выполнялась; speedup после неё ещё предстоит измерить
на тех же graphs/политике, отдельно от profiler overhead.

### Команды, validation и artifacts

Из `src`:

```powershell
dotnet tool install dotnet-trace --tool-path TestResults/ObserverProfile-20261104/tools --version 10.0.750501 --configfile NuGet.Config
& ./ManualTests/AetherSourceWatchPerformance/Profile.ps1 -TraceTool TestResults/ObserverProfile-20261104/tools/dotnet-trace.exe -EvidenceDirectory TestResults/ObserverProfile-20261104/Trace4
foreach ($name in @('files-1','files-2','bytes-3')) {
    & ./ManualTests/AetherSourceWatchPerformance/AnalyzeProfile.ps1 -Speedscope "TestResults/ObserverProfile-20261104/Trace4/$name/observer.speedscope.json" -Output "TestResults/ObserverProfile-20261104/Trace4/$name/analysis-final.json"
}
pwsh -NoProfile -NonInteractive -File TestResults/ObserverProfile-20261104/Audit.ps1
```

Каждый case дополнительно сохранил `dotnet-trace report <trace> topN --number80
--inclusive --verbose` и exclusive variant. Точные collector args/PID/start timestamps,
tool version/hash и discovery — `Trace4/verification.json`. Raw traces4.54–4.73MB,
Speedscope/etlx caches, topN, profiled benchmark report, stdout/stderr и
`analysis-final.json` сохранены для каждого case. Canonical
[REPORT](../../TestResults/ObserverProfile-20261104/REPORT.md), root `audit.json`;
повторный runner не перезаписывает прежние verification/report files.

Initial Trace1–3 attempts завершились до полезного сбора: empty live stdout и
несовместимый read sharing mode. Trace4 использует FileShare.ReadWrite для
live reader и проходит3/3. Первый derived analysis.json сохранён отдельно;
analysis-final.json использует финальный analyzer с loop-count model и исправленным
console projection. Эти setup ошибки не относятся к Observer и не смешиваются с traces.
Unit/full solution/native UI/GPU/fresh package tests не запускались: новые изменения
только в manual PS scripts/docs. Ранее незакоммиченные Window/Editor/performance
изменения и src/.codex сохранены, commit/push не выполнялись.

<a id="ed6b2e4-observer-performance-2026-11-04"></a>

## ED6b.2e.4: производительность Observer — 2026-11-04

По запросу пользователя выполнен первый measured baseline на большом поддерживаемом
local graph при штатных polling10s после previous completion и cooperative budget5s.
Production Observer/compiler/UI API, limits, SDK/dependencies и runtime lifecycle не
менялись. Добавлен отдельный headless consumer
[AetherSourceWatchPerformance](../../ManualTests/AetherSourceWatchPerformance/README.md):
App DI регистрирует real GameBuildCompiler и IFactory<EditorSourceWatch,ResolvedProject>;
observer caller-owned, compiler App-owned. Только public API, без fabricated descriptor,
friend assembly/reflection/copied census. Каждый case получает descriptor из actual
successful frozen Build четырёх проектов; compilation и source generation вне windows.
Game/runtime/native UI не запускаются. Исторические Window/Observer unit/native/package
результаты этим performance run не переобъявляются текущими.

Финальная матрица: Windows11 Pro for Workstations10.0.26300, AMD Ryzen9 9900X,
12cores/24logical processors, SDK10.0.403, runtime/net10.0.14, Release, workstation GC.
Шесть последовательных отдельных процессов на той же машине; power/priority/cache/
OS settings не менялись. Cache warmed by fixture generation/Build, cold-disk guarantee
нет. Полные OS/CPU/SDK, paths, commands, binary/source/report hashes — в manifests.

### Протокол и границы метрик

Три source fixtures: four-project diamond с64C# files/64KiB(control),2560C# files/
10MiB(files),128non-Compile data members/128MiB(bytes) плюс4small C# marker.
Actual descriptor включает также4csproj, entry и exact framework-contract files:
71/2567/139known files,4trees и84exact probes на case. Shared tree policy наблюдает
non-Compile files; byte fixture измеряет объём census в пределах compiler profile.
Нет PackageReference/network/reparse/cold storage или UI presentation profile.

Для каждого fixture default notifications off/on; default debounce250ms/max1s,
max32watchers. В enabled cases actual mode NotificationsAndPolling,6watchers;
disabled — Polling/0watchers. Затем2s inert NoBuild, initial full pass,3steady polling cycles,
late generated file mutation с прежними length/mtime, восстановление байта,
2s sticky Changed и repeated awaited Dispose/drain. Snapshot-derived scan bounds
получены с requested5ms sampling; actual max gaps16.46–19.89ms. Lower/upper bounds
учитывают начальную/конечную sample uncertainty; это не точный method profiler.

CPU — whole target process milliseconds и %одного logical core за полный polling
cycle, включая10swait и sampler. Managed allocations/GC также process-wide.
NoBuild2s — startup calibration, Changed2s — post-work calibration; их не вычитаем
как точный census CPU. CPU granularity Windows заметна на коротких windows.
GetProcessIoCounters ReadTransfer — logical process bytes, включая cache/metadata,
не physical disk traffic. Private/working set sampled100ms +endpoints включает
retained setup/build baseline и может пропустить более короткий peak.
Setup forced GC, source restoration audit и report/hash writes вне measured windows;
внутри windows induced GC нет. Cooperative5s не означает hard realtime для stalls.

### Фактические результаты финальной матрицы

Медианы3steady samples; первая observation отдельно сохранена в raw report.

| Graph / mode | Actual files / MiB | Median scan bounds, ms | CPU ms/cycle / %one core | Alloc MiB/cycle | Change publication, ms |
| --- | --- | --- | --- | --- | --- |
| control / polling |71 /1.18 |31.95–61.98 |156.25 /1.55 |0.44 |10044.59 |
| control / notifications |71 /1.18 |16.02–45.17 |62.50 /0.62 |0.44 |299.94 |
| files / polling |2567 /11.11 |1494.21–1524.24 |1359.38 /11.81 |6.75 |11365.55 |
| files / notifications |2567 /11.11 |1265.92–1291.21 |1343.75 /11.74 |6.75 |1578.53 |
| bytes / polling |139 /129.11 |155.89–181.64 |265.62 /2.61 |0.62 |10180.79 |
| bytes / notifications |139 /129.11 |156.00–179.72 |312.50 /3.07 |0.62 |429.01 |

6/6case processes exit0, initial6+steady18 full Same passes, budget-exceeded0;
каждый Same pass ReadTransfer равен полному descriptor byte count. Changed6/6
при прежних length/mtime и после revert остаётся latched, idle ReadBytes0.
Sampled private peaks76.92–141.20MiB, working-set115.95–176.15MiB по всему процессу,
а не память одного Observer. В files steady samples — по1Gen0 collection на mode,
Gen1/2=0; в остальных samples GC0. Close/drain1.48–2.48ms, Stopped/watchers0 у всех
шести owners; target processes reaped штатно. Source/loaded DLL/harness hashes48,
restored fixture file hashes5554, report hashes6 — drift0. Detailed raw memory,
CPU, IO operations, sampler calibration и timing windows —
[REPORT](../../TestResults/ObserverPerformance-20261104/Final/REPORT.md).

Observed inference: many small files здесь дороже byte volume. OtherOperationCount
median81200/cycle(files) против5468(bytes), а byte profile читает примерно в11.6раз
больше данных. Кандидат для следующей attribution — repeated local-drive/ancestor
metadata validation в EditorSourceCensus.ValidatePath и entry/file attribute calls.
Call stacks или syscall breakdown пока не собраны: конкретная горячая функция
не объявляется доказанной причиной. Производственный cache/быстрый путь не добавлялся.
Далее измерить эту часть отдельно, затем оптимизировать подтверждённую стоимость,
сохраняя content hashes, exact membership/probes, reparse/byte/entry/depth bounds,
cooperative cancel и actual drain. Три samples дают baseline, не release guarantee.

### Команды и evidence

Из `src` (действительно выполненные final commands):

```powershell
dotnet build ManualTests/AetherSourceWatchPerformance/AetherSourceWatchPerformance.csproj -c Release --no-restore -v quiet '-bl:TestResults/ObserverPerformance-20261104/harness-release-data-{}.binlog'
& ./ManualTests/AetherSourceWatchPerformance/Run.ps1 -EvidenceDirectory TestResults/ObserverPerformance-20261104/Final -Profiles @('bytes','files','control') -SkipBuild
pwsh -NoProfile -NonInteractive -File ManualTests/AetherSourceWatchPerformance/Summarize.ps1 -Verification TestResults/ObserverPerformance-20261104/Final/verification.json
pwsh -NoProfile -NonInteractive -File TestResults/ObserverPerformance-20261104/Audit.ps1
pwsh -NoProfile -NonInteractive -File TestResults/ObserverPerformance-20261104/AuditDocs.ps1
```

Final harness build0errors/0warnings; unique binlog и console log сохранены.
Run.ps1 сам умеет Release build с unique binlog; final matrix reuses тот же проверенный
binary без SDK rebuild между observation cases. Frozen compiler внутри case сохраняет
свои обычные per-phase SDK stdout/stderr logs. Unit/full-solution tests, native GPU/UI
и fresh NuGet acceptance в этом lane не запускались: изменены только manual harness/docs.

Evidence: `TestResults/ObserverPerformance-20261104/Final/{verification.json,summary.json,REPORT.md}`,
six case reports/stdout/stderr, root `audit.json`, `final2.log/exit`, final harness
binlog и logs. Fixture/artifact temp directories сохранены и перечислены в reports.
Snapshots фиксируют исходники и точные DLL, а не только historical test counts.
Docs audit:7PLAN/STATUS/HISTORY files +7new manual files,51local links/anchors,
broken/trailing whitespace0, git diff --check exit0; `docs-audit.json` сохранён.

Начальные attempts сохранены отдельно и не включены в final table:

- First restore в sandbox не смог прочитать user NuGet.Config; ordinary approved
  restore/build выполнен. Затем исправлен missing App.Builder callback в harness,
  не production API. Final build выше прошёл с existing assets.
- Pilot1 source fixture внутри checkout наследовала Directory.Build.props, mirror
  path превысил SDK260chars limit до restore. Fixture вынесена в short unique temp
  вне checkout hierarchy; limits/OS settings не менялись.
- Pilot2 прошёл, но Process.Refresh100ms sampling давал material overhead на small
  graph. Финальный sampler использует Win32 IO/memory/handle counters с borrowed
  SafeProcessHandle и BOOL/int +DWORD/SIZE_T/ULONGLONG layout; signatures сверены с
  official GetProcessIoCounters/GetProcessMemoryInfo/IO_COUNTERS docs. Pilot CPU
  не используется как production performance result.
- Matrix1:4valid control/files cases, затем128MiB C# fixture отклонена aggregate
  compiler-input budget до Observer. Byte profile заменён non-Compile data той же
  tree policy; исходная ошибка — compiler fixture boundary, не scan-budget timeout.
  Все6final cases повторены на одном исправленном binary; старые4не смешиваются с final.
- CLI `pwsh -File ... -Profiles bytes,files,control` передал одну строку и был
  отклонён ValidateSet до старта. Final command передаёт typed PowerShell array.

Существующие незакоммиченные Window/Editor изменения и src/.codex сохранены;
commit/push/package publication не выполнялись. Wider hardware/physical/K7/release
gates и UNC/network/reparse/HUD/Player остаются отдельными.

<a id="ed4f3-window-root-2026-11-04"></a>

## ED4f.3: Window root стандартного Editor — 2026-11-04

По запросу пользователя реализована адресная интеграция existing Argentis Window
в standard Editor. [EditorMainWindow](../../TrueMoon.Aether.Editor/EditorMainWindow.cs)
наследует Window<EditorViewModel>, содержит один IFactory-created EditorView,
владеет им и подпиской на document presentation; main/document models, factories
и styles заимствуются. Detached/disposed/foreign factory products проверяются;
failed constructor очищает только принятый fresh product, сохраняя original/cleanup
errors. Dispose снимает подписку; retained caption dispatch coalesces owner updates
и отбрасывает старое attachment. Wrapper style не обходит nested view scope.

UseAetherEditorWindow регистрирует IViewStyle<EditorMainWindow> и
IFactory<EditorMainWindow, EditorMainWindowArguments> через существующий App/DI
composition, сохраняя pre-registered factories/style и Cobalt/MS DI. Immutable
arguments фиксируют effective base Title/ClientSize после configureWindow callback;
default1280×800/min1100×700 и custom overrides сохранены. Изменение исходных options
после Build не меняет root/native snapshot. Embedded UseAetherEditor не регистрирует
standalone wrapper. Новых assemblies/dependencies/scopes нет.

EditorMainWindow.FromRoot принимает только direct component или один WindowFrame,
проверяет owner/lifetime и возвращает typed nested EditorView. EditorWindow.Presented
и standard manual consumer используют этот путь. ConfigureDefaultTitleBar,
включая сохранённый default callback, получает прежний EditorView. Для default
header берётся immutable base title, чтобы full root caption не удваивал suffix.
EditorDocumentViewModel.GetWindowCaption/GetWindowCaptionDescription — общий
UI-independent formatter для native Window и header: base/project/scene/dirty/busy/path.

EditorWindow/EditorSession остаются единственным Save/Discard/Cancel owner.
Component RequestClose, header и native request входят в прежнюю coordination;
Window не добавляет второй document Closing handler. Shutdown сохраняет Stop/drain;
GPU/context initialization остаётся в first-presentation/Ready, root constructor
предшествует native allocation. Adapter dispose не освобождает заимствованные
дерево/engine. Core/headless/offscreen/HUD dependencies прежние; Player — ENG7b.2.

### Requirement и точные direct tests

Добавлены4файла/25methods/64cases в TrueMoon.Aether.Editor.Tests; final Debug и
Release содержат все64cases. Примеры проверяемых контрактов:

| Requirement | Evidence: exact test name |
| --- | --- |
| Интегрировать Window в Editor через typed root и один owned view | Factory_CreatesOneTypedEditorPerRootAndOnlyRootOwnsItsIndependentProduct |
| Сохранить overrides, early root и callback compatibility | StandardRootCallback_UsesCopiedEffectiveSettingsAndPreservesCustomFactoryBeforeNativeAllocation; DefaultHeaderCallback_ChainingReceivesOriginalEditorAndUsesBaseTitleExactlyOnce |
| Сохранить embedded DI profile | BothProviders_EmbeddedCompositionRetainsRawFactoryAndRegistersNoStandaloneRoot |
| Одна close policy для component/header/native ×Save/Discard/Cancel | ComponentHeaderAndNativeClose_ReachOneExistingDecisionWithoutDuplicateSaveOrOwnership |
| Ошибки Save, concurrent drain и forced Shutdown | SaveErrorVeto_IsRetryableWithoutLateCloseOrSecondSaveAndBorrowedRootRemainsAlive; ConcurrentComponentAndNativeClose_DrainIgnoredSaveOnceBeforeRuntimeStop; Shutdown_UpgradesPendingDecisionAndStopsOnceWithoutASecondUserExitOrSave |
| Caption/source updates сохраняют draft/history/controls и current owner | CaptionDetachReattach_DiscardsOldQueuedWorkAndCoalescesLatestStateWithoutRebuilding; WrappedSourceUpdatesAndProjectReopen_PreserveDraftHistoryControlsAndRejectOldOwner |
| Failed construction и subscription lifetime | RootStyleFailure_DisposesAcceptedTreeAndPreservesBorrowedModelsAndBothErrors; Disposal_ReleasesCaptionSubscriptionSoBorrowedDocumentModelCannotRetainDisposedRoot |
| Presented/Ready и borrowed adapter lifetime | EmptyWorkspacePresentation_ReadiesTypedRootOnceWithoutGpuAndAdapterDisposalBorrowsTreeAndEngine |

Test-quality review добавил WeakReference regression: одного отсутствия позднего
callback недостаточно, поскольку disposed Element.Dispatch мог скрыть утечку
подписки. Borrowed document model остаётся alive, disposed root должен собраться;
NoInlining helper и3bounded GC cycles, без sleeps/native IO/production seam.
Pseudo-mutation review выполнен по исходникам, empirical mutation score и coverage
percentage не заявляются. Required parse-only source pairing tool не стартовал
из-за отсутствующих analyzer assets; IDE findTests отсутствовал. Эти ограничения
не подменены утверждением о выполненном анализаторе. Первые test cycles: compile
ошибка двух using Scenes и затем62/63 из-за попытки fixture pre-register header
factory, запрещённой existing contract; fixtures исправлены, assertions не ослаблены.
Focused63/63 предшествует добавлению WeakReference case; final source proof —1126.

### Текущие проверки и область

- Full Editor1126/1126, Alloy976/976 и Hosting154/154 **отдельно Debug/Release**,
  failed/skipped0. Final Editor — `EditorFinal-{Debug,Release}.trx`,64new cases
  независимо сверены. Первые1125Editor TRX исторические для промежуточного состояния.
- Full solution Debug `--no-restore --no-incremental`:0errors/1584existing warnings.
  Full solution/Aether tests в этом срезе не запускались. Cached-assets NU1900 и
  прежние compiler/analyzer warnings не означают нового clean-warning build.
- VerifyPackages:15fresh packages версии `0.2.2.7-arch2e.20261104003745`,8headless
  Debug build/published Release ×DLL/apphost ×Cobalt/MS DI и2no-bootstrap negative
  controls. Isolated consumer/cache, packages не опубликованы в remote feed.
- VerifyGui WindowRoot: source Debug, source published Release, fresh NuGet
  published Release ×standard/Cobalt и embedded/MS DI —6/6cases. По14owned children,
  13graceful/5Game/maxLive1 на case;84children reaped/released,78validation-enabled
  reports0errors/0warnings,30Game с selected artifact private DLL/BuildId. Шесть
  deliberate crashes восстановлены. Final UI sessions/GPU/resources/handles0.
- Standard lane проверяет copied custom Unicode title/1360×840, default header
  callback с nested EditorView, одну рамку/neutral geometry, retained root/view,
  clean+dirty native/header/component captions и3current source owners. Три actual
  component close requests проходят Cancel, failed Save и final clean close через
  прежнюю policy. Underlying Workflow сохраняет actual preview/game, document/history,
  project switch/close, Build/reuse/paused Replace/compiler rejection/rollback,
  selected ProjectReference+locked PackageReference closure и diagnostics.
- Embedded lane сохраняет raw tree/отдельный View recreation; root/header style
  creates0. Startup failure/cancel на тех же6verified binaries:12/12cases,
  failure до native allocation, cancel после first frame, error recorded/completion
  true, children0, final UI/GPU/handles0. Injected failure cleanup error сохранён
  как ожидаемый result; не скрыт под утверждением об отсутствии startup errors.
- Просмотрены3PNG: SourceDebug-Standard/edited, PackageRelease-Standard/
  diagnostics-health, PackageRelease-Embedded/hierarchy-named. Одна рамка/caption,
  preview/inspector/diagnostics и embedded geometry сохранены в этих снимках.
- Evidence audit:2855producer/consumer/package/validation/startup binary hashes,
  drift0. `VerifyEvidence.ps1` проверяет TRX, exits,6native+12startup reports,
  resource ledgers и source snapshots. Центральные docs не входят в package snapshot.
  Документационный audit9PLAN/STATUS/HISTORY +report:54added local links/anchors,
  broken0, git diff --check exit0; `docs-verification.json` сохраняет результат.

Exact source/test/package/native commands (cwd `src`; logs/TRX/unique binlogs
сохранены в `TestResults/EditorWindow-20261104`):

```powershell
foreach ($c in @('Debug','Release')) {
    dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c $c --no-restore --logger "trx;LogFileName=EditorFinal-$c.trx" --results-directory TestResults/EditorWindow-20261104 "-bl:TestResults/EditorWindow-20261104/EditorFinal-$c-{}.binlog"
}
pwsh -NoProfile -NonInteractive -File TestResults/EditorWindow-20261104/RunTests.ps1
dotnet build TrueMoon.slnx -c Debug --no-restore --no-incremental -v quiet '-bl:TestResults/EditorWindow-20261104/solution-debug-{}.binlog'
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyPackages.ps1 -EvidenceDirectory TestResults/EditorWindow-20261104/Packages
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/EditorWindow-20261104/Packages/verification.json -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -RunProfile SourceDebug -HostKind All -Scenario WindowRoot -EvidenceDirectory TestResults/EditorWindow-20261104/SourceDebug1
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/EditorWindow-20261104/Packages/verification.json -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -RunProfile SourceRelease -HostKind All -Scenario WindowRoot -EvidenceDirectory TestResults/EditorWindow-20261104/SourceRelease
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/EditorWindow-20261104/Packages/verification.json -ValidationDirectory TestResults/EditorWindow-20261104/tools/validation -RunProfile PackageRelease -HostKind All -Scenario WindowRoot -EvidenceDirectory TestResults/EditorWindow-20261104/PackageRelease
pwsh -NoProfile -NonInteractive -File TestResults/EditorWindow-20261104/RunStartupCases.ps1
pwsh -NoProfile -NonInteractive -File TestResults/EditorWindow-20261104/VerifyEvidence.ps1
```

RunTests.ps1 содержит первые Editor1125 +full Alloy/Hosting commands; последние
не менялись после дополнительного Editor-only test. Final Editor loop выше —
авторитетный1126result. Это список выполненных команд, не их временная очередность.
После выбора binlog-generation все последующие MSBuild commands записывали unique
binlogs; два начальных bootstrap build имели только console logs.

Для strict Vulkan lane скачан [официальный Khronos nightly archive](https://github.com/KhronosGroup/Vulkan-ValidationLayers/releases/tag/nightly),
published2026-11-03T08:13:38Z, commit `e34b578596abaab2fc61a906a9f036b6fbe4e312`,
SHA256 `2CF7A41DF60BE9944499A232178CB7AAB1FB68F4265E6B374F3D8835336F5B93`.
Metadata/download/digest записаны в `tools/validation-provenance.json`. Layer
распакован в ignored evidence, VK_LAYER_PATH задан child environment; SDK/driver/
registry/OS settings не менялись. Доступ к обычному restore/user NuGet.Config
разрешён для текущих package/native scripts; старое ограничение run2026-11-03
не является текущим blocker. Validation означает только OpenGL Editor parent/
Vulkan runtime profile, а не новый proof прежнего plain Window Vulkan UI backend.

Evidence: `TestResults/EditorWindow-20261104/{verification.json,hash-checks.json,REPORT.md}`,
`Packages/verification.json`, `{SourceDebug1,SourceRelease,PackageRelease}/verification.json`,
`startup-cases.json` и12Startup reports. UIA2d targeted independent consumer закрыт
этим profile. Следующий основной шаг — measured source observer cost на большом
local graph с ordinary10s/5s. Wider Window backends/HUD/Player, physical/DPI/hardware/
K7/release gates и full ED4/ED6 не объявляются завершёнными.

<a id="window-engine-integration-review-2026-11-03"></a>

## Window: интеграция с движком и Editor — сверка 2026-11-03

По запросу пользователя обновлены PLAN/STATUS после реализации UIA2 и проверена
необходимость engine integration. Только source review и документация; C# migration,
новые build/test/native не выполнялись. Результаты предыдущего UIA2 code run —
[UI HISTORY](../alloy/HISTORY.md#uia2-window-component--2026-11-03):94new cases,
Alloy976/Hosting154/Editor1062 отдельно Debug/Release, source/published Window/App
proof и2producer packages. Editor regression там ещё использует прежний view root.
Missing validation layer/fresh restore и independent NuGet ограничения сохранены.

Проверены источники:

- [EditorWindowConfigurationExtensions](../../TrueMoon.Aether.Editor/EditorWindowConfigurationExtensions.cs):
  UseAetherEditorWindow создаёт IFactory<EditorView>, host options задают1280×800,
  min1100×700, custom frame/header; configure callback выполняется последним.
- [EditorWindow](../../TrueMoon.Aether.Editor/EditorWindow.cs): Presented ожидает
  EditorView в WindowFrame.Content; Closing делегирует user close в
  EditorSession.RequestCloseAsync, Shutdown — в StopAsync. Adapter владеет подписками.
- [EditorView](../../TrueMoon.Aether.Editor/EditorView.cs) — reusable View<EditorViewModel>;
  [EditorSession](../../TrueMoon.Aether.Editor/EditorSession.cs) принимает именно его
  для Present и сохраняет document close policy. Embedded consumer использует тот же
  View factory напрямую, поэтому его нельзя автоматически превратить в Window.
- [EditorViewportBinding](../../TrueMoon.Aether.Editor/EditorViewportBinding.cs):
  GPU initialization опирается на ready native context через PostWindow после первого
  presentation; её нельзя перенести в ранний constructor нового root.
- [GUI package consumer](../../ManualTests/AetherPackageConsumer/Gui/Program.cs):
  standard/embedded ветки и direct WindowFrame.Content cast потребуют адресного
  обновления/отдельной проверки, а не вывода из generic Window probe.
- [Aether csproj](../../TrueMoon.Aether/TrueMoon.Aether.csproj) содержит Contracts,
  Enerit/Mithril/Vulkan, без Argentis/Alloy/Hosting. Editor csproj уже содержит Hosting.

Вывод: рекомендовать отдельный standalone Window<EditorViewModel> wrapper для
standard Editor, сохранить существующий EditorWindow lifecycle adapter и reusable
EditorView. Component defaults960×540 конфликтуют с editor min1100×700, а Title/
ClientSize имеют приоритет над host options: explicit snapshot/creation-arguments
bridge нужен для прежних configure overrides. Typed root/header access, одна рамка,
общий caption source и один document close owner — части предлагаемого среза
[ED4f.3](EDITOR_PLAN.md#ed4f3-window-root), не уже реализованные свойства Editor.

Core/headless и offscreen Runtime/preview не требуют Window. HUD остаётся
external-host View, Player рассматривает компонент только в ENG7b.2 UI-host с
единственным native/device owner. Публичный Window API и его acceptance ведутся
в UI плане; shared engine contracts, wire/checkpoints/Replace не меняются.
Прикладная миграция не назначена автоматически новой основной очередью: measured
observer cost остаётся текущим Aether шагом. Ссылки и changed-doc diff проверяются
отдельно от runtime tests; исторические отсутствующие ignored TestResults не
восстанавливаются и не заменяются новыми passing утверждениями.

Документационный audit прошёл:9documents,35added links/anchors без ошибок,
git diff --check exit0,16source/2package hash checks drift0,6сохранённых TRX сверены.
Evidence — `TestResults/WindowPlanning-20261103/verification.json`; новые runtime
проверки этой записью не объявляются.

## ED6b.2e.2–3: Observer и source UI — 2026-11-03

Работа начата2ноября, финальная матрица завершена3ноября. Реализован
`Editing.EditorSourceWatch`: inert project product, fresh owner key, immutable
versioned snapshot, один worker и один coalesced wakeup. Bind actual successful
Build/reuse начинает Checking; source hints только меняют signal/version.
Full original-source census сверяет TreeFiles, bytes/SHA256 и present/absent probes,
разделяет compiler exclusions и budgets. Timestamp/length не доказывают Same.
Changed sticky до новой successful baseline; неполный/locked/unstable/unsupported
pass не публикует Same. Recoverable failures повторяются polling, permanent
unsupported paths остаются inert. Ancestor/drive-root probes опрашиваются exact,
не создают broad subscriptions и не дают ложного hint-fallback warning.

Сохранены defaults250ms debounce,1s max wait,10s maintenance после completion,
5s cooperative pass budget,3transient retries и32handles×8KiB. Bytes/entries,
потраченные на отброшенные retry reads, учитываются в общем pass budget.
Hashing uses async ReadWrite|Delete sharing, buffer64KiB/pass и actual IO drain.
Внутренний stream factory — узкий test seam для controlled unstable-read metadata;
default open/share/lifetime прежние, нового public IO abstraction/DI scope нет.

Full UseAetherEditor копирует/валидирует SourceTracking и регистрирует preserved
IFactory до project factory. ProjectSession владеет document+source products;
partial factory failure пытается очистить оба, объединяет errors и сохраняет
failed-cleanup orphans у factory до retry. Close fences callbacks, cancels pass,
quiesces hints, ждёт actual worker drain и освобождает handles. Project/retired
owner сохраняется при cleanup failure. Legacy public constructors оставлены.
Open/empty startup не активирует observer или SDK ради tracking.

ScenePlayback связывает actual Candidate.Build после повторного VerifyCapture и
context-version check. Raw GameEditor callers не приобретают автоматическое
наблюдение. Failed/cancelled/stale completion сохраняет прежнюю baseline.
Scene edits/Save/Undo/selection и game Stop её не сбрасывают; source errors не
становятся GameEditor.Error и не меняют running child/admission. Preflight прежний.

EditorViewModel.Source делает owner pull после project/document binding и не
выдаёт old-owner snapshot даже во время промежуточных binding notifications.
Retained status SourceButton и live popup details styled в scoped IViewStyle;
primary runtime/command/error precedence не меняется. Worker не держит View и не
вызывает PropertyChanged. Legacy scene-only UI без ProjectSession не получает
лишний source badge/backdrop или новые thread restrictions. Custom host должен
вызывать Present/owner Refresh регулярно (≤1s active window); native embedded
consumer использует Continuous pump. On-demand host без pump показывает last read.

Проверки текущего среза, Windows x64/net10.0/SDK10.0.403/xUnit2 VSTest:

- Full Editor **1062/1062 отдельно Debug и Release**, failed/skipped0; **84новых
  cases** worker/census/options/DI/project/playback/UI. Exact names и requirement
  matrix — `TestResults/SourceObserver-20261102/REPORT.md`, final TRX в том же каталоге.
- Deterministic clock/hints/scan gates проверяют debounce/max deadline, signal/baseline
  races, ignored cancellation actual drain, sticky Changed, recovery и cleanup retry.
  Real filesystem проверяет exact/adjacent path/tree/entry/depth/file/total budgets,
  32/33handles, original references/locks/archives, missing probes и exclusions.
  Controlled streams доказывают unstable-read rejection/disposal и cumulative retry
  byte budget. Static assertion/mutation review не выдаётся за executed mutation run.
- Source Release standard/Cobalt +embedded/MS DI **2/2**: real observer/UI, Same после
  touch, same-length/same-mtime byte diff, Changed после revert, fresh Build reset,
  source read failure при Playing, сохранение runtime identity/primary status,
  focused NaN draft/control identities/theme/history, mandatory stale preflight,
  project close drain и same-manifest reopen NoBuild. Standard использует FSW+poll,
  embedded — намеренно polling-only; acceptance interval500ms вместо default10s.
- Fresh15packages/headless **8/8** и package Release standard/Cobalt+embedded/MS DI
  **2/2** прошли. Итого4native cases,4game children reaped/released,4graceful runtime
  reports с Vulkan validation **0errors/0warnings**, final UI/GPU/handles0. Source
  и package inputs проверены без drift; packaged lane использует только installed
  package graph/cache, без repository ProjectReference.
- Source PNGs Same/Changed/PlayingChanged просмотрены: отдельный badge, running game
  и focused draft видимы, parent-owned embedded layout сохраняется.
- Final verification: **2095input hash checks**, drift0; **123local links** и
  `git diff --check` прошли. User-staged ReplaceInterruptWorkflow сохраняет183строки
  в index; существующие dirty files и local .codex не очищались/не stage-ились.
- Full solution, ordinary Workflow/Failure/Cancel/interrupt52 matrix, physical and
  hardware/release gates не повторялись и этим срезом не закрываются.

Основные команды (все логи/binlogs/TRX/PNG сохраняются):

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --logger 'trx;LogFileName=FinalEditor-Debug.trx' --results-directory TestResults/SourceObserver-20261102
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --logger 'trx;LogFileName=FinalEditor-Release.trx' --results-directory TestResults/SourceObserver-20261102
./ManualTests/AetherPackageConsumer/VerifyPackages.ps1 -EvidenceDirectory TestResults/SourceObserver-20261102/Packages
./ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/SourceObserver-20261102/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -RunProfile PackageRelease -HostKind All -Scenario SourceWatch -EvidenceDirectory TestResults/SourceObserver-20261102/PackageNative
```

Source lane — `RunSourceTrial5.ps1` с previous package manifest (source changes
разрешены только SourceRelease и отдельно записаны). Все current production inputs,
включая новые файлы, заново hashed; final fresh package lane требует полного совпадения.
Earlier attempts сохранены: InitialCompile — misplaced test using directives;
FirstFull — legacy source-backdrop/thread compatibility и новые fixture bindings;
SecondFull/ранний final — owned-child detach и attach-before-binding assertion.
Native SourceTrial — missing readback extension import; Trial2 — document frame handoff;
Trial3 — cached inspector input до selection reconciliation; Trial4 — весь scenario
прошёл, final mode check выявил drive-root hint warning. Эти failures исправлены и
не суммируются с passing final matrix. Trial5 — final source acceptance.

Подтверждённый следующий шаг — measured observer cost на большом local graph с
обычными10s/5s defaults; эти bounds пока не являются performance guarantee.
UNC/network/reparse, wider packages, HUD/Player и прежние hardware/release work
остаются отдельными. [Contract/checklist](EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).

## ED6b.2e.1: descriptor и probes — 2026-11-02

Реализован immutable `GameBuildSourceWatchDescriptor` в Aether.Builds: original
project path, known path/length/SHA256, project-only tree roots, exact present/absent
probes и отдельный source fingerprint. Capture собирает probes при существующих
SDK/global.json, import и ancestor-config/response проверках. Общий
`GameBuildSourceWatchPolicy` используется самим recursive compiler census: прежние
case-sensitive имена bin/obj/.git/.codex/.agents, artifact-owner subtree и budgets
не меняются. Explicit HintPath/linked inputs под bin/obj остаются known files.
Отдельный `TreeFiles` фиксирует actual recursive membership, включая случай explicit
пути `BIN/file` к excluded `bin/file` на Windows. Membership нельзя выводить только
из spelling known paths; fingerprint различает membership при одинаковом known set.

`Inputs.SourceWatch` делегирует `Preparation.SourceWatch`. Данные не владеют
ресурсами; original locks/nupkg сохраняются, expanded package DLL и toolchain/job
census не становятся watch targets. Source identity canonical/order stable и не
содержит ticket, job, toolchain/environment/evaluation identity. Полный fingerprint
и artifact/checkpoint/control/project schemas прежние. Новые snapshot properties
имеют JsonIgnore: большие Files arrays не дублируются в diagnostics. Reopen
по-прежнему возвращает null Inputs/Compilation и не восстанавливает descriptor.

Watch-specific лимит4096deduplicated probes или невозможность доказать regular-file
presence дают null descriptor с bounded `Preparation.SourceWatchUnavailableReason`.
Это не делает supported Build ошибочным и не ослабляет compiler guards: новый
response file всё так же запрещён до SDK admission. Рабочие observer, handles,
polling, DI lifetime и UI delivery не добавлены.

Проверки текущего среза (Windows x64, SDK10.0.403, net10.0, xUnit2/VSTest):

- Final Aether delta **26/26 Debug и Release**, failed/skipped0: **22 новых cases**
  и4дополненных package/diamond/reuse fixtures. Проверены immutable original closure, отсутствующие
  imports/configs/response probes, same-length edit, create/delete/rename/new subtree,
  touch/empty/excluded/unrelated false positives, Windows case/order identity,
  artifact-owner policy, fail-soft probe budget и occupied absent path.
- Дополнены существующие package/diamond/reuse fixtures: locks/archives только по
  original paths,4project trees/shared-path deduplication, fresh descriptor при reuse
  и прежний reopen fence.
- Full Editor **978/978 Debug и Release**, failed/skipped0, повторён после final delta.
- Ранее в этом срезе scoped Builds/ChildDiagnosticFactories/GameEditor **279/279
  Debug и Release** прошли до добавления отдельного TreeFiles списка. Это regression
  исходной реализации; последняя membership поправка проверена final26 отдельно.
- Fresh15NuGet packages/headless **8/8**: Debug build и published Release,
  DLL/apphost ×Cobalt/MS DI; source/package hashes проверены без drift.
- Final verification: **703source/package hash checks**, drift0; **121local links**
  в актуальных планах/статусах/истории и `git diff --check` прошли.
- Native/UI/full solution runs не выполнялись; прежние native results не являются
  acceptance будущего observer/indicator.

Команды первоначального regression прогона (для каждой из Debug и Release):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Builds|FullyQualifiedName~ChildDiagnosticFactoriesTests|FullyQualifiedName~TrueMoon.Aether.Tests.Editing.GameEditorTests' --logger 'trx;LogFileName=Aether-Debug.trx' --results-directory TestResults/SourceWatch-20261102
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --logger 'trx;LogFileName=Editor-Debug.trx' --results-directory TestResults/SourceWatch-20261102
./ManualTests/AetherPackageConsumer/VerifyPackages.ps1 -EvidenceDirectory TestResults/SourceWatch-20261102/Packages
```

Для Release заменены `-c` и имена TRX. Final delta использует фильтр22новых cases
и4дополненных fixtures из `RunFinalDelta.ps1`; полный фильтр, точные test names
и команды находятся также в `TestResults/SourceWatch-20261102/REPORT.md`.
Evidence сохраняется в
`TestResults/SourceWatch-20261102`: runner/logs/TRX, package manifest и final
`verification.json`. Первичный focused14/14 Debug предшествует расширению до22cases;
результаты не суммируются с final scoped run. Rider `findTests` недоступен в этом
tool set; использованы существующие fixtures через source lookup.

Далее ED6b.2e.2: inert/project-owned observer с deterministic state/coverage/debounce/
polling/drain tests; UI attach/indicator и его acceptance принадлежат ED6b.2e.3.
[Contract и checklist](EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).

## ED6b.2e: контракт source watcher и stale Build status — 2026-11-02

Документационная работа: contract определён в
[Editor плане](EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02)
по текущим implementation/ownership paths. C# API, watcher, UI indicator и tests
в этом срезе не добавлялись; build/test/package/native runs не выполнялись.
Предыдущий native52/52 не становится acceptance watcher.

Исходная сверка и следствия:

- `TrueMoon.Aether/Builds/GameBuildPreparationSnapshot.cs`: Files сохраняет known
  original identities, но не является descriptor новых tree members/missing probes.
- `Builds/GameBuildPreparation.cs`, CopyDirectoryAsync/CaptureProjectAsync/
  ReadProjectAsync/Resolve/SelectSdk: compiler знает source trees, exclusions,
  explicit references, original packages и ancestor/import probes. Descriptor нужно
  получать из этого capture; повторный MSBuild parser в Editor не вводится.
- `Builds/GameBuildInputSnapshot.cs`: full fingerprint включает evaluation,
  preparation/toolchain/environment. Offline source hash нельзя сравнивать с ним
  как с доказательством preflight; требуется отдельный SourceFingerprint.
- `TrueMoon.Aether.Editor/Editing/EditorScenePlayback.cs`, RunAsync: baseline attach
  возможен после successful `_dispatch(Build)` и VerifyCapture, **до** return для
  explicit Build или Start/Replace. Периодическое чтение Candidate недостаточно:
  GameEditor скрывает/очищает candidate после смены runtime/startup.
- `Projects/EditorProjects.cs`, `ProjectSession.cs`, `ProjectSessionFactory.cs`,
  `EditorProjectsLifetime.cs`: factory products owned проектом, retired owners
  сохраняют cleanup retry. Текущий DisposeAsync закрывает document; implementation
  watcher потребует actual drain обоих продуктов с сохранением старых constructors.
- `EditorConfigurationExtensions.cs`, UseAetherEditorProjects: optional factory
  dependencies выбираются во время setup через Exist. Source factory надо добавить
  до этого вызова, а не Resolve в прикладной системе/каждом кадре.
- `AetherEditorConfigurationExtensions.cs`, `EditorSession.cs`, `EditorViewModel.cs`,
  `EditorStatusBarViewModel.cs`: shared DI +owner-thread Refresh уже есть. Monitor
  публикует immutable data; Present/Refresh только читает snapshot и changed values,
  не запускает filesystem scan или UI callbacks из worker.
  `EditorWindowConfigurationExtensions.cs` использует Continuous по умолчанию;
  custom on-demand host должен обеспечить owner Refresh pump/wakeup отдельно.

[FileSystemWatcher .NET10](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0)
сверен по первичной документации: notifications могут дублироваться/теряться при
overflow. Поэтому hints +bounded full polling, Checking/Unavailable и obligatory
compiler preflight закреплены явно. Не используется timestamp-only trust.

Определены NoBuild/Checking/ObservedUnchanged/Changed/Unavailable/Stopped; Changed
sticky после доказанного diff до нового successful Build. Code/source, authoring
scene и running artifact — отдельные оси. Наблюдение ограничено disk closure текущей
загруженной конфигурации, без external IDE buffers/autoreload .aetherproj. Empty
project inert; ProjectSession — единственный owner фабричного source product.
Политики scheduling/IO/handles/path budget, rebinding epochs, cancellation/drain,
fallback polling, UI delivery и negative/race/lifecycle acceptance записаны в
одном плане. Numeric defaults — design choices, не performance results.
Проверка документации:123local links/anchors, git diff check пройдены. Read-only
сверка646code/project/build input hashes с census предыдущего native lane дала0drift;
это проверка отсутствия code changes, не повторный build/test. Evidence:
`TestResults/SourceWatchContract-20261102/verification.json` и `Verify.ps1`.

Следующий конкретный implementation шаг ED6b.2e.1 — immutable capture-derived
descriptor/probes/shared exclusion policy с unit checks и без изменения full input
fingerprint/manifest schemas. Затем ED6b.2e.2 observer/lifetime и ED6b.2e.3 retained
indicator +source/fresh NuGet acceptance. Прежние hardware/release gates открыты.

## ED6b.2b.2c.1: native package interrupts и preflight — 2026-11-02

Изменён только acceptance consumer/runner и документы. Production runtime/API,
dependencies, control/checkpoint schemas и App/Cobalt ownership не менялись.
UseAether/Editor по-прежнему владеют compiler, frame/session и child lifecycle.

ReplaceInterruptWorkflow использует v3 fixture с captured ProjectReference +locked
Game.Packaged PackageReference. Startup capture должен содержать package DLL; при
Build/Verify SDK query сравниваются owned archive copy с original и lock SHA512,
captured package DLL с выбранным artifact SHA256. Query log связывается с live owned
PID по creation/start time и незавершённому stdout. Restore читает published candidate
после staging drain, связывает build ID с реальным restore child. Candidate record
сохраняется во время preflight: GameEditor.Status скрывает его после смены origin.
Cancel до transaction сохраняет старый Playing runtime, после candidate admission
восстанавливает old code/state/mode. Stop/exit запрещают late/recovery child, document
admission закрыта до actual drain. Parent dirty/history/selection не меняются.

PackageInputWorkflow использует UI scene capture/Build и public GameEditor
StartBuildAsync/ReplaceBuildAsync API. Четыре cases: Start/Replace ×stale/corrupt.
Stale обновляет только unsigned archive и lock: новый валидный DLL имеет дополнительный
NOP, consumer-visible getter всё ещё возвращает1. Corrupt дописывает archive bytes,
оставляя прежний lock. C# source hash не меняется между Build и rejection, следовательно
ошибка не маскируется code edit. Ожидаются GameBuildInputsChangedException/InvalidDataException.
Stale/corrupt не запускают child, не меняют stopped/Playing state/identity/child count
и не входят в checkpoint. Consumer-owned checkpoint marker отсутствует до rejection
и обязан появиться при успешном fresh Replace, доказывая, что marker активен.
Corrupt failure не имеет SDK stdout logs. Новый Build с новым ticket не reuse old
payload, захватывает изменённую package DLL, затем actual native Start/Replace успешен.
Final reports требуют private DLL только из selected artifact, parent CLR их не грузит.

Этот raw GameEditor preflight lane не заявляет ScenePlayback document-operation
admission; сохранение parent проверяется, а admission/drain — отдельными interrupt cases.
Новых production hooks нет. Restore barrier и checkpoint marker находятся только
в consumer game. Проверки ограничены case/compiler/UI deadlines.

Новая CLI опция `VerifyGui.ps1 -Scenario PackageAcceptance` —9interrupt +4input cases
на host/profile; `PackageInputs` отдельно запускает4input cases. Original All по-прежнему
означает Workflow/Failure/Cancel. Проверяются source/fresh NuGet published Release ×
standard/Cobalt и embedded/MS DI. План52cases:36interrupts/52children и16preflight/
24children; все76children должны быть reaped/released с graceful validation0/0,
maxLive1 и final UI/GPU/handles0. Ordinary Workflow matrix/unit/full solution не
считаются повторёнными этим lane.

Первый SourceTrial прошёл6Build/Verify cases, затем ошибся в новом restore observer:
NullReferenceException при чтении скрытого Status.Candidate. Это harness failure,
не production runtime regression. Поправка сохраняет candidate во время preflight
и сверяет его с actual restore identity; весь SourceTrial повторяется в SourceTrial2.
Initial logs/runtime cleanup evidence сохранены в TestResults/PackageInterrupt-20261102.

Commands из src:

```powershell
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyPackages.ps1 -EvidenceDirectory TestResults/PackageInterrupt-20261102/Packages
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/Packages-20261102/Final/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/PackageInterrupt-20261102/SourceTrial2 -Profile SourceRelease -HostKind Standard -Scenario PackageAcceptance
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/PackageInterrupt-20261102/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/PackageInterrupt-20261102/SourceEmbedded -Profile SourceRelease -HostKind Embedded -Scenario PackageAcceptance
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/PackageInterrupt-20261102/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/PackageInterrupt-20261102/PackageNative -Profile PackageRelease -HostKind All -Scenario PackageAcceptance
```

Final `TestResults/PackageInterrupt-20261102/verification.json`: Completed=true,
52/52native cases (36interrupt +16preflight).76/76Game children reaped/released,
76graceful reports, Vulkan validation0errors/0warnings, maxLive1, final UI sessions/
GPU resources/handles0.4recoveries только Restore+Cancel; Stop/exit не создают late
child.16preflight cases подтверждают package-only mutation, отказ до checkpoint/
child и successful fresh native Start/Replace с новым package payload. Corrupt
archive не достигает SDK. Все76child reports загружают Game.Shared.dll и
Game.Packaged.dll только из selected artifact, parent CLR их не загружает.

Fresh15packages/headless8/8 завершены;2743input hash checks без drift, git diff check
пройден. SourceTrial2 Standard13cases +SourceEmbedded13cases +PackageNative26cases;
первый6case/restore-observer failure не включён в final totals. Native standalone
cases не создают screenshots; visual acceptance этим lane не заявляется. Unit/full
solution и ordinary Workflow matrix не повторялись; предыдущие package33/Editor978
и ordinary native4/4 относятся к ED6b.2b.2c.

External evidence roots:

- SourceTrial2: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e3-0244daab00d04bb0a006a2ff9318f89d`.
- SourceEmbedded: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e3-f486cc47e44d4d968902d840411a4d1c`.
- PackageNative: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e3-a4645ceba8e74996b9fa5f79b8a08d35`.
- Packages/headless: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e-ca0b4bc0b49c4b51bc24f910b74274c8`.

Следующий конкретный шаг — bounded source watcher и stale Build status для текущего
project/document: определить invalidation/ownership/debounce/drain/UI delivery
contract без auto Build/Play. Hardware/physical/performance/release gates, wider
package profiles и HUD/input/Player остаются отдельными.

## ED6b.2b.2c: captured managed packages — 2026-11-02

Добавлен первый bounded PackageReference profile в compiler preparation, выбранный
после ED6b.2b.2b.1. Public opt-in — GameBuildRequest.PackageDirectory (только вместе
с CapturePreparation), manifest v3 packageDirectory и session mapping абсолютного пути.
v1/v2 writer по-прежнему опускает отсутствующий новый member. App/Cobalt/factory/
runtime/checkpoint/control contracts не менялись.

Каждый package-consuming csproj имеет schema1 packages.lock.json с одним net10.0
target. Literal stable x.y.z/[x.y.z], direct resolved exact-match, dependency edges/
ranges и unsigned archive SHA512 проверяются до SDK. Отдельный local directory
с lowercase id.version.nupkg не наследует feeds/cache/network. Стандартные
RestorePackagesWithLockFile/RestoreLockedMode декларации допустимы: package admission
выполняет наш capture, последующий SDK restore получает только mirrors с Reference
и собственный work/restore.lock.json; исходный lock не переписывается.
[NuGet lock semantics](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
сверены; отдельный regression использует настоящий dotnet restore для создания lock.
Это наш узкий profile поверх package metadata, не общий NuGet resolver.

Один lib target net10.0/net9.0/net8.0/netstandard2.1/netstandard2.0; matching dependency
group либо ungrouped dependencies. Только flat IL-only DLL с matching assembly name,
optional XML/PDB и обычная package metadata. Signatures, variants/ref/runtime assets,
build/buildTransitive, analyzers/generators, native/content/framework assets и
meta-packages и nuspec references/developmentDependency unsupported. ZIP paths/duplicates/links и declared/actual DLL lengths
ограничены. До16packages на весь graph; archive+expanded entries делят source budget
4096files/64MiB per file/256MiB. Прежний aggregate Csc budget и entry+15private DLL
сохранены. Shared packages используют одни copies; conflicting version/hash,
DLL-name collision, undeclared DLL, cycle и unreachable lock row отклоняются.

PackageReference удаляется только в mirrors, Reference указывает на captured DLL;
package references распространяются по ProjectReference graph. Before/after/prestart/
reuse recapture включает original lock/archive identities; Csc читает byte copies.
Изменённый valid closure требует нового Build; hash mismatch отклоняется до SDK.
Cleanup удаляет package staging внутри preparation. Original lock/archive не меняются,
Editor не загружает private DLL; runtime получает только selected flat artifact.

Новые regression cases (TrueMoon.Aether.Tests.Builds.GameBuildPreparationTests):

| Требование | Exact test method |
| --- | --- |
| Admission до job | Packages_RequirePreparationBeforeJobAdmission |
| Actual transitive DLL loading, prestart stale rejection, changed payload и reuse | Packages_BuildLockedTransitiveDllsVerifyChangesAndReuseOnlyVerifiedPayload |
| Lock от настоящего SDK, ranges/contentHash и сохранение original inputs | Packages_ConsumesSdkGeneratedLockWithoutChangingOriginalLockOrArchive |
| Package в dependency project, root private payload и verify | Packages_ProjectGraphPropagatesCapturedDllsFromDependencyToRoot |
| 28 pre-SDK negative cases и drain | Packages_RejectUnsupportedOrChangedClosureBeforeSdkAndDrain |
| Edit archive после SDK admission, no publication и cleanup | Packages_EditAfterSdkAdmissionFailsWithoutPublishingAndReleasesCopies |

Editor Projects.JsonProjectStoreTests:
VersionThree_LocalPackageDirectoryRoundTripsAndMapsToAbsoluteBuildInput (1case),
PackageDirectory_RejectsInvalidVersionOrPathBeforePublication (6cases).
Итого33новых engine и7Editor cases. `finding-tests` прочитан; execute_tool/findTests
недоступен, использованы существующие fixtures. Test platform net10.0/xUnit2/VSTest,
overlay отсутствует; зависимости runner не менялись.

Initial focused run:21/25passed,4failure из-за неполного списка ожидаемых exception
types в negative harness; сами rejections происходили до SDK. Initial Editor54/55:
старый negative test использовал3как unsupported version; заменён на int.MaxValue.
Focused final30/30; настоящий SDK lock probe1/1 отдельно Debug/Release. Первый scoped
run292/292 и full Editor978/978 Debug/Release прошли до последнего lock-property
follow-up; следующий scoped293/293 и full Editor978/978 Debug/Release прошли на
lock-property follow-up. При окончательной source review затем найдено, что nuspec
references/developmentDependency могли молча терять asset-selection semantics:
добавлен fail-closed guard и2negative cases. После этой последней production delta
повторены все33package cases и full Editor978 отдельно Debug/Release, failed/skipped0;
scoped293не выдаётся за повторный запуск после guard. Его остальные code paths
не менялись. На первых sources ordinary native4/4 также прошли; окончательные
package/native proofs выполняются отдельно в Final и не смешиваются с ними.
Все initial logs/TRX сохранены в `TestResults/Packages-20261102/`.

Commands (PowerShell, из src):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~GameBuildPreparationTests.Packages_' --logger 'trx;LogFileName=Packages.trx' --results-directory TestResults/Packages-20261102/FocusedFinal
# Final matrix: повторить оба test commands отдельно с Debug и Release.
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Builds|FullyQualifiedName~TrueMoon.Aether.Tests.Editing.GameEditorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Replacements' --logger 'trx;LogFileName=Tests.trx' --results-directory TestResults/Packages-20261102/Final-engine-Debug
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --logger 'trx;LogFileName=Tests.trx' --results-directory TestResults/Packages-20261102/Final-editor-Debug
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyPackages.ps1 -EvidenceDirectory TestResults/Packages-20261102/Packages
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/Packages-20261102/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/Packages-20261102/SourceNative -Profile SourceRelease -HostKind All -Scenario Workflow
pwsh -NoProfile -NonInteractive -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/Packages-20261102/Packages/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/Packages-20261102/PackageNative -Profile PackageRelease -HostKind All -Scenario Workflow
```

Окончательная acceptance после nuspec guard использует те же package/native commands
с EvidenceDirectory под `TestResults/Packages-20261102/Final/`; final targeted tests:

```powershell
# Повторено отдельно с Debug и Release; Editor — full suite той же команды выше.
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~GameBuildPreparationTests.Packages_' --logger 'trx;LogFileName=Tests.trx' --results-directory TestResults/Packages-20261102/Final/Engine-Debug
pwsh -NoProfile -NonInteractive -File TestResults/Packages-20261102/RunFinalAcceptance.ps1
```

Native fixture теперь v3 с Game.Shared ProjectReference и locked Game.Packaged
PackageReference в Shared. IL archive создаётся PersistedAssemblyBuilder без load
в parent CLR. Game.Load обращается к packaged getter через Shared.Constants;
runtime reports требуют обе DLL из selected artifact и ни одной в parent.
Workflow требует archive/lock во всех6preparation snapshots, package DLL в root
compiler capture, verified whole-payload reuse, paused Replace и rollback.
Окончательный `TestResults/Packages-20261102/Final/verification.json`: Completed=true,
package33/33 и full Editor978/978 отдельно Debug/Release, failed/skipped0. Fresh
15packages/headless8/8; published source/NuGet ×standard/Cobalt и embedded/MS DI
4/4ordinary native workflows.56children reaped/released,52graceful reports с Vulkan
validation0errors/0warnings;20Game загрузили Game.Shared.dll и Game.Packaged.dll
только из selected artifact. Parent ни одной private assembly не загрузил; maxLive1,
final UI sessions/GPU resources/handles0. Все24before/after/prestart observations
scene Start содержат original archive/lock identities, reuse и paused Replace/
compiler failure/rollback подтверждены. Source/package census2059hash checks без
drift, git diff check пройден.2representative final replaced-paused PNG просмотрены
(source standard и NuGet embedded); остальные изображения сохранены без blanket
visual claim. Последний production change — nuspec guard, после него выполнена вся
Final package/native acceptance. Initial и pre-final lanes сохранены отдельно.

Final external evidence roots:

- Packages: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e-af4ed023f33d411c98ffe8cd8416ac59`.
- Source native: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e3-d625036e20e94205a022a6b5f9ef6c10`.
- NuGet native: `C:/Users/MEGA6/AppData/Local/Temp/TrueMoon-ARCH2e3-c636b62561cb41b692c96209f2789029`.

Следующий конкретный шаг — directed native Cancel/Stop/exit в package profile и
stale/corrupt archive перед Start/Replace; ordinary workflows этого не заменяют.
Wider package profiles, full Aether/Alloy/Hosting/solution и physical/hardware/
performance/general release gates этим не объявляются закрытыми. Не добавлены
dependencies, новые App/DI/lifetime contracts, scopes или native/GPU production paths.


## ED6b.2b.2b.1: native graph interrupts — 2026-11-02

Расширен только ManualTests/AetherPackageConsumer acceptance harness; production API/
runtime C# не менялись. Fixture использует manifest v2, captured ProjectReference
Game.Shared.dll и настоящий compiler/RPC/runtime/viewport. Startup capture должен
содержать ровно один child project с этой DLL; родитель не загружает игровой код.

Девять cases на каждый profile/host:

| Stage | Условие допуска interrupt | Actions |
| --- | --- | --- |
| DependencySdkQuery | GameEditor Build, graph-<16HEX>-query.stdout.log создан после StartTime наблюдаемого live compiler PID, current output ещё открыт | Cancel / Stop / Exit |
| GraphPrestartQuery | GameEditor Replace, live verify-query, DocumentOperation.Runtime, staged Game.Shared.dll совпадает с candidate artifact | Cancel / Stop / Exit |
| CandidateRestore | Native Game вошёл в bounded RestoreCheckpoint barrier; old child reaped/released, candidate PID совпадает с lease | Cancel / Stop / Exit |

Наблюдение и отправка interrupt находятся в одном UI owner invocation. Нет guessed
delay, нового framework test hook, PInvoke/process suspension или расширения deadline.
Public ProcessId/ReadDiagnostics и job files дают корреляцию SDK stage; timestamp
исключает старый dependency log от предыдущего SDK процесса. Verify сохраняет SHA256
сравнённого selected private payload. Короткая marker-write race ждёт завершения записи;
release native barrier теперь в finally вокруг всей стадии наблюдения/interrupt.

После actual drain проверяются отсутствие preparation/capture/graph/work/output staging,
завершение compiler PID, completed/resources-released diagnostic owners, закрытая до
drain admission и отсутствие позднего child. Cancel до transaction оставляет old Playing
identity; после restore admission допускает ровно один rollback с old code/state/mode.
Stop/Exit запрещают recovery, explicit Stop вновь допускает Save. Content/dirty/history/
selection сохраняются до exit decision. Все Game children обязаны загрузить Game.Shared.dll
только из selected artifact, получить graceful validation report и освободить ресурсы.

Текущий запуск — `TestResults/GraphInterrupt-20261102/`:

- `SourceTrial`: source published Release standard/Cobalt9/9, functional trial прошёл
  с первой попытки. Эти текущие9cases сохраняются как standard source proof без повторения.
  Package baseline предыдущего graph среза имеет те же framework sources; изменены только
  acceptance harness files, которые runner фиксирует отдельно. Это не исторический run.
- Fresh15packages/headless8/8 прошли в `RunFinal.ps1`. Source embedded/MS DI9/9 и fresh
  NuGet standard/Cobalt +embedded/MS DI18/18 также прошли. Итого36/36native cases,
  52Game children reaped/released,52graceful Vulkan reports0errors/0warnings, maxLive1,
  final UI sessions/GPU resources/handles0. Ни одного intentionally killed runtime в
  этой матрице нет. Во всех52reports Game.Shared.dll загружена только из selected artifact.
- Двенадцать наблюдений накаждуюstage и12накаждыйaction. Четыре successful recoveries
  относятся только к Restore+Cancel; Stop/Exit не допускают late/recovery child. Verify
  cases записали12SHA256 selected/staged private DLL; SDK cases24PID/timestamp pairs.
- `Collect.ps1` проверяет required SDK stage/timestamp, private payload SHA, scenario
  matrix/recovery counts, selected paths/role isolation, staging/resource cleanup и hashes.
  Итоговый `verification.json` Completed=true,2735input hashes без drift, collect.exit0,
  diff-check.log пустой. Новый lane прошёл с первой попытки, без runtime changes/ослабления
  assertions/deadlines. Source standard9cases не дублировались после успешного trial.

```powershell
pwsh -NoProfile -NonInteractive -File TestResults/GraphInterrupt-20261102/RunTrial.ps1
pwsh -NoProfile -NonInteractive -File TestResults/GraphInterrupt-20261102/RunFinal.ps1
pwsh -NoProfile -NonInteractive -File TestResults/GraphInterrupt-20261102/Collect.ps1
```

Source standard current run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-6a12c3e8b46f43b49e7b54eb91272411`.
Source embedded current run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-791945d804ec495483a774bcae448332`.
Fresh package/headless run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e-7688b59df851434098552420a4cc7a28`.
Fresh package native run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-836f2519e9a14c7e915280f98969436d`.
Перед SDK launch исправлены mixed line endings working-copy staged harness file;
index/staged content, старые evidence/temp dirs, user files и src/.codex не менялись.

Новых unit/full solution запусков и ordinary Workflow matrix нет; прежние262/971
Debug/Release относятся к ED6b.2b.2b. Матрица использует Playing game и настоящие host
windows; Paused/terminal App shutdown за пределами этих конкретных native cases.
Physical/DPI/hardware/performance/VRAM/device-loss/release gates не закрываются.
Далее — минимальный locked managed PackageReference closure без build/analyzer/native
assets; текущий compiler по-прежнему отклоняет все PackageReference.

## ED6b.2b.2b: captured managed project graph — 2026-11-02

Профиль `GameBuildRequest.CaptureProjectReferences` требует CapturePreparation (и тем
самым TrackInputs/CaptureCompilation). До первого SDK process захватывается DAG максимум4
C# net10.0 library projects вместе с entry, в distinct non-overlapping directories.
References — unconditional literal Include без metadata; dependency AssemblyName —
имя csproj либо один explicit unconditional literal. Все private DLL перечислены в
Dependencies; ordinary HintPath references прежние. Cycles, output-name collisions,
overlapping source directories, разные SDK, condition/metadata/package/custom task graphs
и context-dependent shared imports отклоняются до SDK execution.

ReadProject работает с копией исходного XML для каждого owner. Shared imports допускаются
только при одинаковом результате path rewriting. В mirrors ProjectReference становится
explicit private Reference; transitive edges добавляются явно. Original files не меняются.
Topological order содержит каждый dependency project один раз, включая diamond graphs.
SDK BuildProjectReferences остаётся false; query/prepare и Csc каждого узла управляет один
compiler job, с отдельными work/capture paths и общим flat graph output. Csc каждого узла
читает byte copies. Один aggregate capture budget4096files/64MiB на файл/256MiB суммарно
распространяется на все invocations; source/toolchain/deadline limits прежние.

`GameBuildCompilationSnapshot.Projects` содержит original project/DLL name, child input
witness и captured invocation. Root content identity включает child fingerprints. TotalBytes
суммирует root и dependency captures. Before/after/prestart capture фиксирует весь source/
imports/glob/config/reference graph. При verify/reuse private DLL копируются из pinned
выбранного artifact, привязанного к исходному compilation witness; они не берутся из
original bin/obj. Input-only verification графа отклоняется до admission; нужен overload
VerifyInputsAsync(inputs, compilation). Open не восстанавливает provenance. Changed/cold/
missing/corrupt payload идёт в fresh dependency/root compilation; same-ticket corrupt
published destination сохраняет прежнее fail-closed поведение без repair.

Cleanup удаляет graph/work/capture/preparation/output staging и reaps текущий SDK/Csc.
Diagnostic retention распознаёт только известные suffixes после graph-<16uppercaseHEX>,
максимум3dependency prefixes, regular files only. Unknown files/links сохраняются.
Нет новых DI services/scopes/factory APIs; низкоуровневый graph/budget принадлежит job.

Manifest formatVersion2 добавляет explicit dependencies (до15distinct flat DLL names,
без entryAssembly). Read/Write version1 сохраняют прежние восемь полей без dependencies.
Version1 с непустым allowlist отклоняется. Resolve и CreateBuildRequest фиксируют immutable
copies; empty request.Dependencies остаётся null для прежних consumers. Стандартный
ScenePlayback включает CaptureProjectReferences в Build/Start/Replace. Root artifact,
checkpoint и control schemas не меняются. Runtime child загружает private DLL из selected
artifact; родительский Editor не загружает Game.Logic/Game.Shared.

Focused requirement matrix (16новых engine cases и8Editor cases):

| Требование | Exact test name |
| --- | --- |
| Opt-in требует preparation до job admission | `ProjectGraph_RequiresPreparationBeforeJobAdmission` |
| Single edge, runtime value42, original source witness, input-only rejection, full verify, reuse и изменённая dependency | `ProjectGraph_BuildsCapturedLibraryVerifiesChangesAndReusesOnlyVerifiedPayload` |
| Cycles/metadata/condition/undeclared/duplicate/limit/task/package/overlap до SDK | `ProjectGraph_RejectsUnsupportedClosureBeforeSdkAndDrains` (9cases) |
| Diamond4projects, dependency order/once, separate intermediates, byte-repeatable4DLLs | `ProjectGraph_DiamondCompilesEachDependencyOnceWithIsolatedIntermediates` |
| Missing/corrupt selected private payload rebuilds | `ProjectGraph_UnavailablePrivatePayloadRebuildsInsteadOfReusing` (2cases) |
| Child compiler diagnostics сохраняют original source path | `ProjectGraph_CompilerFailureReportsDependencySourceAndReleasesWholeGraph` |
| Cancel во время dependency SDK, actual drain и повторный admission | `ProjectGraph_CancelDuringDependencySdkWorkDrainsBeforeNextAdmission` |
| v2 roundtrip, immutable allowlist/build mapping | `VersionTwo_PrivateDependenciesRoundTripAsAnImmutableBuildAllowlist` |
| v1/null/duplicates/entry/path/extension/count rejection | `Dependencies_RejectInvalidAllowlistBeforeManifestPublication` (7cases) |

Existing ScenePlayback test теперь проверяет graph flag; прежний version1 roundtrip
проверяет точные восемь полей. Scope runner — xUnit2/VSTest/net10.0.

Промежуточные результаты сохранены под `TestResults/ProjectGraph-20261102/`:

- First:9passed/1failed, функциональные single-edge build/verify уже прошли; retention
  не распознавал graph diagnostics. Добавлен строгий suffix/hash/count allowlist.
- Second:9passed/1failed — fixture LoadFromAssemblyPath удерживал Frozen.dll при temp
  cleanup. Fixture загружает test DLL из stream и unloads collectible context.
- Third:12passed/1failed — diamond выявил отсутствие transitive Leaf.dll в root copy
  list. Добавлены explicit transitive references в mirrors, без SDK graph build.
- Fourth:13/13SDK cases и158/158Project/ScenePlayback tests, failed/skipped0. Ещё4engine
  cases (missing/corrupt, options, overlap) включены в final scope, passing здесь не перенесён.
- SourceTrial: standard/Cobalt published Release Workflow прошёл с Game.Shared.dll:
  paused Start/reuse/Replace/compiler failure/rollback и прежний document/preview workflow.
  Это functional trial со старым package baseline; новый source file отсутствовал в старом
  input inventory. Final source proof использует fresh baseline с полным текущим inventory.
- Итоговый `RunFinal.ps1`: Builds +GameEditor +Replacements262/262 и full Editor971/971
  отдельно Debug/Release, failed/skipped0. Debug engine5m47s, Release5m44s; это scope
  actual SDK/compile regression, не full Aether/solution run.
- `Packages/verification.json`:15fresh packages, headless8/8cases Debug/published Release
  ×DLL/apphost ×Cobalt/MS DI, exit0. Independent consumer использует package references,
  без project/linked source references к фреймворку.
- `SourceNative` и `PackageNative`: по2/2published Release workflows standard/Cobalt и
  embedded/MS DI, итого4/4cases.56children reaped/released,52graceful Vulkan validation
  0errors/0warnings,20Game reports с Game.Shared.dll из selected artifact, maxLive1,
  final UI sessions/GPU resources/handles0. Четыре intentionally killed previews не
  дают final runtime report; это прежний crash/recovery workflow, не графовая ошибка.
- Native JSON содержит16полных root graph captures (4наcase). Ещё4partial root captures
  без Projects относятся к намеренно ошибочному C# source: SARIF подтверждает error,
  а child SDK inputs/prepare diagnostics сохранены отдельно. Они не выдаются за
  published compilation witness. Second Start reuse skips dependency Csc as well.
- `Collect.ps1` проверил TRX, required method names, role/private-path isolation,
  captures/reuse/replace/resource proofs и2056input hashes без drift. `verification.json`
  Completed=true; `collect.exit.txt`0, `diff-check.log` пустой. Первый collector call
  завис; собственный process16660 остановлен, automatic PowerShell `$input` в loop
  заменён на `$entry`, final noninteractive collection завершился успешно.
- Два representative final PNG просмотрены: SourceRelease-Standard scene-replaced-paused
  и PackageRelease-Embedded scene-rollback-paused. Red cube, Scene Guardian selection,
  X1.65, paused mode и dirty document сохранены; все12paused frame files существуют,
  остальные screenshot/pixel assertions проверены самим native workflow.

```powershell
pwsh -NoProfile -File TestResults/ProjectGraph-20261102/RunFinal.ps1
# Engine command in each Debug/Release configuration:
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Builds|FullyQualifiedName~GameEditorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Replacements' --logger trx --results-directory TestResults/ProjectGraph-20261102/Engine-Debug -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --logger trx --results-directory TestResults/ProjectGraph-20261102/Editor-Debug -v quiet
pwsh -NoProfile -NonInteractive -File TestResults/ProjectGraph-20261102/Collect.ps1
```

Final source native run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-e42aaf24dd3749269c7a581e0cac0cb2`.
Final package native run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-7a3f98bee70748358b91352b37c9d7b7`.
Fresh package/headless run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e-8f01fd82ee1e4b988cbe219b13190fac`.

Full Aether/Alloy/Hosting/solution suites, новая native interrupt matrix, physical/hardware/
performance/release gates отдельно. PackageReference/custom build graph, более4projects,
reference metadata/conditions, source-dir overlap, scene migration/live copyback/HUD/Player/
full ED6b не добавляются этим срезом.
Следующий ED6b.2b.2b.1 — native Cancel/Stop/exit в dependency SDK work, graph verification
и restore. SDK cancel и generic engine fences текущего запуска не заменяют native lane.
Затем — минимальный locked managed PackageReference closure, без custom build/analyzer/
native assets; текущий профиль пока отклоняет все PackageReference.

## ED6b.2d.1: native Replace interrupts — 2026-11-02

Native acceptance использует тот же реальный compiler/RPC/runtime/viewport consumer.
`ReplaceInterruptWorkflow` наблюдает Build и preflight verification по GameEditorOperation
и живому compiler PID, затем отправляет interrupt в том же UI invocation. Restore case
изменяет только consumer-owned Game.cs: candidate RestoreCheckpoint пишет entered marker
и ждёт release не дольше10s. Родитель подтверждает old-child reap/release, кандидатный PID
и закрытую document/runtime admission до release. Это реальный native RPC, без новых
test hooks, фабрик, DI scopes или ослабления transaction deadlines во фреймворке.

Обнаружены две ошибки порядка: ScenePlayback отменял caller token перед engine Stop;
EditorDocumentCommands Exit ждал Replace перед Stop, допуская recovery child. Исправление
вызывает Stop синхронно до cancellation. Только in-flight Replace получает early exit Stop;
обычный document IO сохраняет прежний Save/Discard/Cancel порядок. Exit удерживает parent
до drain операции и runtime cleanup, затем спрашивает dirty decision/закрывает document.
Failed early Stop сохраняет authoring product и tracked error для explicit cleanup retry.
Cancel сохраняет прежнюю recovery policy; game scene/checkpoint/API/deadlines не меняются.
При veto dirty exit окно/document сохраняются, но уже начатая остановка Runtime
не отменяется и recovery не возобновляется. Обычный document IO без Replace не
получает early Stop; его прежние Save/Discard/Cancel tests прошли в full Editor suite.

Focused test matrix (xUnit2/VSTest, net10.0):

| Требование | Exact test name |
| --- | --- |
| Stop fences recovery перед cancellation, Cancel не Stop | `EditorScenePlaybackTests.Replace_InterruptFencesStopBeforeCancellingTransaction` (Cancel/Stop) |
| Exit/terminal Stop ждёт actual Replace и cleanup до close/decision, veto сохраняет document | `DocumentCommandsTests.ExitDuringReplace_FencesBeforeCancellationAndDrainsBeforeDocumentClose` (force false/true) |
| Failed early cleanup сохраняет document/error и допускает retry | `DocumentCommandsTests.ExitDuringReplace_CleanupFailureKeepsDocumentAndAllowsRetry` (force false/true) |

Первый RED unit run:1passed/3failed из4cases в `TestsRed`; native `RedExit4` создал
runtime-1/2/3.json при ожидаемых old/candidate двух процессах, затем отклонил late rollback.
Предыдущие harness-only attempts сохранены: `RedExit`/`RedExit2` не компилировались из-за
using/type, `RedExit3` выбирал entity до публикации workspace. Дожидание workspace count
и missing namespace исправлены; product bug подтверждён только отдельным `RedExit4`.
`TestsGreen`77/77 проверил два затронутых набора после исправления. Ещё два cleanup-failure
cases включены в финальный full Editor run; это6новых cases суммарно.

Текущие проверки:

- `RunFinal.ps1`: scoped Aether82/82 и full Editor963/963 отдельно Debug/Release,
  failed/skipped0. Full Aether/Alloy/Hosting/solution suites не повторены.
- `SourceGreen/verification.json`: published source Release ×standard/Cobalt и
  embedded/MS DI18/18native cases (Build/Verify/Restore ×Cancel/Stop/Exit),26children
  reaped/released,26graceful Game validation reports0errors/0warnings, maxLive1,
  UI sessions/GPU resources/handles0. Каждая case проверяет сохранение parent
  content/history/dirty/selection до close decision и пригодность Save после Stop.
- `Packages/verification.json`: fresh15packages и independent headless8/8cases,
  Debug/published Release ×DLL/apphost ×Cobalt/MS DI; exit0. Это отдельный lane.
- `PackageNative/verification.json`: fresh NuGet published Release ×standard/Cobalt
  и embedded/MS DI18/18cases,26children/26graceful Game reports. Итого36/36native cases,
  52children reaped/released,52graceful Vulkan validation0errors/0warnings, maxLive1,
  final UI/GPU/handles0. В родительский Editor Game.Logic не загружался; все game paths
  принадлежат выбранному artifact. Ни одного forced crash/kill в этой матрице нет.
- `Collect.ps1` проверил final TRX/case/child/resource proofs и2053input hashes без drift.
  `verification.json` имеет Completed=true; `diff-check.log` пустой, exit0.

Команды (из src; отчёты под `TestResults/ReplaceInterrupt-20261102/`):

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~GameEditorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Replacements' --logger trx --results-directory TestResults/ReplaceInterrupt-20261102/Engine-Debug -v quiet
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --logger trx --results-directory TestResults/ReplaceInterrupt-20261102/Editor-Debug -v quiet
# RunFinal.ps1 repeats each test command with Release and a separate result directory.
pwsh -NoProfile -File ManualTests/AetherPackageConsumer/VerifyGui.ps1 -PackageVerification TestResults/DocumentReplace-20261101/PackagesFinal/verification.json -ValidationDirectory TestResults/SkiaUpgrade-20261028/tools/validation -EvidenceDirectory TestResults/ReplaceInterrupt-20261102/SourceGreen -Profile SourceRelease -HostKind All -Scenario ReplaceInterrupt
pwsh -NoProfile -File TestResults/ReplaceInterrupt-20261102/RunFinal.ps1
pwsh -NoProfile -File TestResults/ReplaceInterrupt-20261102/Collect.ps1
```

Source native run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-dd541944e21f4d33acb1ff689997aebc`.
Package native run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e3-c5d3e082cde543479fa29c9a9531d00d`.
Fresh package/headless run: `C:\Users\MEGA6\AppData\Local\Temp\TrueMoon-ARCH2e-f5bb2325919f4accbdb569dfdcf5dc04`.
No old evidence/temp directories removed. Тестовая file barrier ограничена10s и снимается
в finally; никаких native process suspension/новых PInvoke/kill injection нет.
Native game mode в этой матрице Playing; Paused recovery остаётся engine/model proof,
а paused swap/rejection — предыдущим ED6b.2d native proof. Полный прежний Workflow и
startup Failure/Cancel matrix, performance, physical input/DPI/VRAM/device loss/K7/release
gates не повторены. Scene migration/live copyback, widened graphs/cache/HUD/Player/full
ED6b не добавлены. Дальше — минимальный managed ProjectReference profile ED6b.2b.2.

## ED6b.2d: document-aware code Replace — 2026-11-01

Первый application profile заменяет код owned Playing/Paused runtime, сохраняя тот
же captured project/document state и совместимую scene revision. Authoring edits
при игре остаются закрыты; другая сцена требует Stop/Start. Для stateful Replace
game реализует ICheckpointGameSession. Новая scene migration policy или live copyback
не добавлены; game-specific restore validation остаётся у game.

ScenePlayback хранит borrowed project/content и identity принадлежащего ему runtime.
Replace фиксирует immutable scene и новый source ticket, не останавливает preview/
старый world во время compilation, затем повторяет project/document/runtime checks.
Shared document-session capture gate охватывает всю async runtime подготовку.
DocumentOperation.Runtime блокирует direct Apply/Undo/Redo и file IO до окончания
verification/start/Replace. Close отменяет её и ждёт actual drain до authoring disposal.
Используются существующие pending/work/close ownership, без нового DI scope/lease API.
Синхронный engine admission rejection остаётся синхронным и не приобретает чужой runtime.
Чужая identity, изменённый authoring state и superseded context не получают поздний
Replace. Parent document/dirty/history/selection сохраняются. После success или
successful rollback обновляется owned identity; failure не вызывает лишний Stop
восстановленного мира. Stop сохраняет существующую retry admission и очищает borrowed
capture после успешного drain. Новых сервисов, DI scopes/factories/owned handles нет.

GameEditor.ReplaceBuildAsync(expectedContextVersion) требует exact context/candidate.
Общая VerifyCandidateAsync теперь обслуживает Start и обе Replace точки: tracked
input и original captured compiler inputs проверяются до Pause и reservation
GameReplacement. Reopened witness отклоняется; после SDK verification повторяется
engine context/runtime fence. SDK verification использует compiler budget отдельно
от прежних replacement forward/total deadlines. GameReplacement и его checkpoint/
rollback/explicit Stop interruption policy не менялись.

| Требование | Exact test evidence |
| --- | --- |
| Новый source ticket, code build без лишнего preview stop, old mode и parent history | `Replace_BuildsCapturedDocumentAndPreservesModeAndHistory` (Paused/Playing) |
| Другой document/runtime не допускается, stale completion не доходит до transaction | `Replace_RejectsDifferentDocumentOrRuntimeBeforeBuild`, `Replace_RejectsStaleCompletionBeforeTransaction` |
| Build/transaction/recovery failure сохраняют owned old/recovered world | `Replace_FailureKeepsOldOrRecoveredRuntimeOwned` (3cases) |
| Cancel/Stop во время подготовки ждут actual drain, не применяют поздний candidate | `Replace_InterruptDuringBuildDrainsBeforeAdmission` (2cases) |
| Registered ViewModel маршрутизирует Replace через scene queue | `ViewModel_RoutesRunStopThroughSceneQueueAndRejectsUnappliedInspectorDraft` (добавлена Replace assertion) |
| Stale input и reopened witness не Pause/Stop старый runtime | `TrackedReplace_RejectsChangedInputsBeforePauseOrStop`, `TrackedReplace_RejectsReopenedWitnessBeforePauseOrStop` |
| Superseded context не применяет другой selected candidate | `ReplaceBuild_RejectsSupersededContextBeforeTransaction` |
| Direct edit/IO не меняют захваченный document во время async runtime work | `RuntimeCapture_FencesDirectEditsAndIoUntilActualDrain` |
| Close отменяет и drains runtime capture до authoring disposal | `RuntimeCapture_CloseCancelsAndDrainsBeforeAuthoringDisposal` |
| Прежние checkpoint/rollback/cancel/Stop/close fences | `Replace_UsesExplicitSelectionAndPreservesStateAndMode`, `HeldReplace_CancelRecoversButStopAndCloseNeverLaunchRollback`, GameReplacementTests |

Финальные runs: scoped Aether Editing/GameEditor + Replacements82/82 и full
Editor957/957 отдельно Debug/Release, failed/skipped0. Добавлено17cases (3engine,
12model,2document owner). Полные Aether/Alloy/Hosting/solution suites не повторялись. Namespace,
project wiring и зависимости прежние. IDE analysis изменённых engine/coordinator
files без errors; реальные CLI builds/tests проверили обе конфигурации.

Fresh15packages/headless8/8 и published Release source/fresh NuGet ×standard/Cobalt
и embedded/MS DI4/4workflows. Consumer-owned ProbeGame получил checkpoint capability.
После двух обычных Start/reuse выполняются: code swap при Paused с точным tick,
intentional C# compiler failure без остановки старого child, candidate с confirmed
RestoreRejected и rollback к старому коду/state/Playing mode. После Pause проверяется
та же authoring reference, undo/dirty/selection; Stop reaps актуальный recovered child.

На каждый native case14runtime children,13graceful reports,5game; всего56reaped/
released,52graceful Vulkan validation0errors/0warnings,20game, maxLive1, final UI/GPU/
handles0. Восемь новых scene-replaced-paused/scene-rollback-paused PNG просмотрены:
red cube, Scene Guardian selected, X1.65, simulation paused, Edit Preview off,
standard dirty caption сохранён. Original Scene Start pixels также проверяет runner.
2031framework source hash checks после source/package verification без drift.
Это не GPU stress/physical-device acceptance и не performance attribution.

Исходные failures сохранены. Initial Debug81/82: новый stale Replace test вызывал
Play до SetContext; исправлен порядок fixture setup. Initial SourceRelease workflow
прошёл Replace/compile-failure/rollback, но завершился на старом total-child count11.
Ожидание обновлено до14 (candidate success + rejected candidate + old recovery);
owner-thread/maxLive/reap/validation assertions не ослаблены. Свежий SourceRelease2
и NuGet workflow прошли. Финальная сверка затем выявила окно direct-API mutation
между captured invocation и async verification: добавлен Runtime guard полного
времени действия, два owner tests и полный Editor run. После этой правки заново
выполнены SourceFinal/PackagesFinal/PackageFinal. Предварительные82/36 и native
результаты сохранены в verification-preliminary.json и не выданы за final binaries.

Final evidence: `TestResults/DocumentReplace-20261101/verification.json`, `RunFinal.ps1`,
`CollectFinal.ps1`, `SourceFinal/verification.json`, `PackagesFinal/verification.json`,
`PackageFinal/verification.json`, четыре `Engine|Editor-configuration-final` TRX.
Исходные failures — первый Debug log/TRX и `SourceRelease/verification.json`.
Runner `pwsh -NoProfile -File TestResults/DocumentReplace-20261101/RunFinal.ps1`
выполнил Debug/Release отдельно: `dotnet test <project> --no-restore -c <configuration>
--logger trx --results-directory <evidence> -v quiet`. Для Aether добавлен filter:
`FullyQualifiedName~GameEditorTests|FullyQualifiedName~TrueMoon.Aether.Tests.Replacements`;
Editor final запущен без filter. Тот же runner выполнил VerifyGui SourceRelease/
All hosts/Workflow, VerifyPackages, VerifyGui PackageRelease/
All hosts/Workflow, прежний validation directory. Новые private feed/cache; без push.

Следующий ED6b.2d.1 — directed source/NuGet native Cancel/Stop/exit во время Build/
verification и после old-child Stop. В текущем run engine/model interrupt tests
прошли, но не объявляются native interrupt proof. Scene migration/live copyback,
extended graphs/disk cache/HUD/input/Player/full ED6b, distinct-PID assumption и
прежние physical/hardware/drain/popup/release gates открыты.

## ED6b.2c: verified payload reuse — 2026-11-01

`GameBuildRequest.ReuseArtifact` opt-in требует CapturePreparation. Компилятор
сохраняет один последний successful supported-profile result после полного job
cleanup. Cache — immutable metadata/provenance, без живых streams/processes и без
нового DI service/scope. Следующий build job забирает этот result; failed/cancelled
job не возвращает его в cache. Failed prestart verification и CloseAdmission также
сбрасывают result. Новый compiler не читает persisted cache, Open теряет witness.

Reuse проверяет actual before/after SDK input fingerprints, полный source/toolchain
freeze, original non-generated compiler inputs и manifest/DLL hashes под Share.Read
Pin. При совпадении повторные prepare/Csc/copy compiler inputs не нужны; capture.json
сохраняет исходную immutable compilation provenance. Для нового SourceRevision
Publish копирует pinned DLL bytes и создаёт manifest текущего ticket. Same-ticket
reuse допускает прежний manifest ID. Input/compilation fingerprints не зависят от
compile ticket; scene/runtime/context identity по-прежнему выбирается отдельно.
Before/after/prestart observations и stale rejection не удалены. Reuse job сохраняет
reuse.json, но не создаёт prepare.json/build.stdout.log; diagnostics retention знает
этот новый flat file. Pin освобождается до завершения job; cache не удерживает leases.

Scene Start запрашивает reuse. Explicit Editor Build и обычный default BuildAsync
выполняют новую компиляцию, которая может подготовить result следующему Start.
Каждый Start заново фиксирует unsaved immutable scene, drains Edit preview и создаёт
новый runtime child. GameEditor source ticket/context-version fences, prestart checks,
authoring/history/dirty/selection и lifecycle admission не ослаблены.

| Требование | Exact test evidence |
| --- | --- |
| Same/new ticket reuse, DLL bytes и current manifest revision; explicit fresh Build; Open без provenance | `Reuse_RepublishesVerifiedPayloadForNewTicketWithoutCompiler` |
| Source/import/entry change заставляет компилировать | `Reuse_ChangedContentOrEntryRebuilds` (3cases) |
| Missing/corrupt artifact и новый compiler не дают cache hit | `Reuse_UnavailablePayloadOrNewCompilerRebuilds` (3cases) |
| Failed build и stale verification сбрасывают прошлый result | `Reuse_FailedBuildAndStaleVerificationDiscardPreviousResult` |
| Cancel drains SDK/leases и следующий запрос компилирует | `Reuse_CancelledProbeDrainsAndDiscardsPreviousResult` |
| Invalid flags отклоняются до admission | `Reuse_RequiresFrozenPreparationBeforeAdmission` |
| Scene Start включает reuse, сохраняет unsaved scene/history и новый ticket | `RunSimulation_UsesUnsavedSceneCopyAndPreservesHistoryThroughControlsStopAndRestart` |

Результат: Aether89/89 (preparation/compilation/GameEditor) и ScenePlayback18/18
отдельно Debug/Release, failed/skipped0;10новых SDK cases. Full module/solution suites
не повторялись. Initial compile failure CS0619/xUnit2014 сохранён в первом Debug log:
assertion lambda выбирала obsolete Func<Task> overload; исправлена на void lambda
для синхронной before-admission validation. Успешные TRX находятся в `*-2` каталогах.
IDE findTests отсутствует; использованы существующие SDK fixture/collection и xUnit
conventions. Утверждения проверены по конкретным behavior и вторичным признакам:
payload bytes/manifest/provenance, отсутствие compiler phases, cleanup/retention.

Fresh15packages/headless8/8 и published Release source/fresh NuGet ×standard/Cobalt
и embedded/MS DI4/4workflows. Native case требует first compile/second reuse, previous
BuildId в reuse.json, distinct tickets/manifests и fresh child processes. Input/
compilation/preparation fingerprints стабильны внутри case; у reused job нет
prepare.json/build output.44children reaped/released,8game,40graceful runtime reports
с Vulkan validation0errors/0warnings, final UI/GPU/handles0. Четыре paused PNG
просмотрены: red cube, Scene Guardian selected, X1.65, Edit Preview off, dirty caption
standard shell.2031source hash checks после проверки без drift.

| Native Release case | First Start, ms | Reused Start, ms | Parent peak working set, bytes |
| --- | ---: | ---: | ---: |
| Source standard/Cobalt | 10365.1929 | 8513.1464 | 344141824 |
| Source embedded/MS DI | 9943.6708 | 8233.1659 | 331460608 |
| NuGet standard/Cobalt | 10276.4258 | 7984.3661 | 312999936 |
| NuGet embedded/MS DI | 9670.8635 | 7661.6212 | 303890432 |

Медианы first/reused Start10.110/8.109s, по4samples. Предыдущий optimized always-compile
baseline second Start9.055s; сравнение локальное warm OS-cache, не независимый causal
benchmark. Текущий freeze median1.568s (24samples), toolchain6028files/755663686bytes
читается заново в каждой фазе. Toolchain/environment SHA совпадают с предыдущим
baseline; общий source fingerprint меняется при новой Aether.dll. Parent peak
handles12710–12727, before/after751→676,734→698,751→719,734→703. Working set и
handles —100ms samples родительского GUI process, без child-tree/CPU attribution.

Evidence: `TestResults/ArtifactReuse-20261101/verification.json`, `Run2.ps1`,
`Collect.ps1`, `SourceRelease/verification.json`, `Packages/verification.json`,
`PackageRelease/verification.json`, четыре scoped TRX `*-2`. Runner: `pwsh -NoProfile
-File TestResults/ArtifactReuse-20261101/Run2.ps1`. Он выполняет отдельно Debug/Release:
`dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c <configuration>
--filter 'FullyQualifiedName~GameBuildPreparationTests|FullyQualifiedName~GameBuildCompilationTests|FullyQualifiedName~GameEditorTests'
--logger trx --results-directory <evidence> -v quiet`; для Editor filter —
`FullyQualifiedName~EditorScenePlaybackTests`. Затем VerifyGui source Release,
VerifyPackages и VerifyGui fresh NuGet Release, All hosts/Workflow, прежний validation
directory. Уникальные private feed/cache, без NuGet push.

Границы: сохраняется trusted Windows/net10 declarative single-project profile,
не sandbox произвольного MSBuild. Если corrupt artifact уже занимает тот же
content-addressed destination при same-ticket publish, обычная пересборка может
завершиться fail-closed ошибкой Publish; автоматического удаления опубликованных
artifacts нет. Disk cache/graph extensions/watchers/document-aware Replace/full
ED6b и прежние physical/hardware/drain/popup/distinct-PID/release gates открыты.
Следующий срез — contract/admission document-aware Replace с captured scene/ticket
и rollback, с сохранением parent document/history.

## ED6b.2b.2a.1: toolchain freeze cost — 2026-10-31

Измеренный hot path — `GameBuildPreparation.LeaseToolchainAsync`: в baseline каждый
из6028files повторно проверял всю цепочку предков, а retained FileStream с buffer64KiB
мог удерживать индивидуальный managed buffer после SHA256 до cleanup всего job.
Проверка critical patterns: culture-sensitive IndexOf/StartsWith/EndsWith/Contains,
Substring, sync-over-async, stackalloc, new JsonSerializerOptions/HttpClient —0;
sealed classes1/1. Пять LINQ ordering sites нужны для стабильных manifest/path identities.
Scan checklist: `TestResults/PreparationCost-ED6b2b2a1-20261031/critical-scan.json`.

В этом срезе:

- Checked ancestor paths запоминаются только внутри enumeration/open pass над trusted,
  stable installed directories. Перед открытием leases cache очищается; новые
  before/after/verify captures всегда повторяют census/link checks/open/read/hash.
- Retained FileStream использует bufferSize1. Один owned64KiB byte buffer и
  IncrementalHash обслуживают все файлы последовательно; hash reset между файлами,
  точная проверка длины, cancellation и прежние file/byte/entry/depth budgets сохранены.
  Share.Read leases остаются живы до Dispose/cleanup compiler job; DI/lifetime API прежние.
- Readonly `ToolchainEnumerationMilliseconds`, `ToolchainOpenMilliseconds`,
  `ToolchainHashMilliseconds` разделяют wall time обхода/link checks, fresh checks/open/
  budgets и чтения/SHA256. Manifest serialization и прочие промежутки остаются в total.
  Эти поля, как и прежние timings, не участвуют в fingerprint. Это не CPU profiling.

Текущие проверки — focused `GameBuildPreparationTests|GameBuildCompilationTests`:
47/47 Debug и47/47 Release, failed/skipped0. TRX в `Focused2-Debug`/`Focused2-Release`.
Полные Aether/Editor/Alloy/Hosting/solution suites в этом срезе не повторялись.

| Требование | Проверка |
| --- | --- |
| Независимые leases после EOF и освобождение последнего owner | `Capture_PinsExecutableRuntimeSdkAndEnvironmentWithStableIdentity`: exclusive read SDK `.version` запрещён до последнего Dispose и доступен после него |
| Immutable copies, deterministic DLL и live stale rejection | `Capture_FreezesImportsLinkedSourcesAndGlobMembershipBeforeEvaluation`, `SdkFrozenBuild_VerifiesOriginalInputsPreservesPathsAndProducesRepeatableBytes` |
| Cancel/failure cleanup и original diagnostic paths | `CancelDuringFreeze_ReleasesOwnershipAndRemovesPreparation`, `FrozenCompilerFailure_ReportsOriginalLocationAndReleasesAllStaging` |

Fresh15packages/headless8/8; published Release source/fresh NuGet ×standard/Cobalt и
embedded/MS DI4/4workflows.44children reaped/released,8game,40graceful runtime reports
с Vulkan validation0errors/0warnings; final UI/GPU/handles0. Parent history/dirty/
selection, preview reap-before-game и removal staging до Start сохранены. Четыре
scene-play-paused PNG просмотрены: красный cube, Scene Guardian selected, X1.65,
simulation paused, Edit Preview off; standard shell сохраняет dirty caption.

| Local warm-cache metric | Предыдущий baseline | Этот run |
| --- | --- | --- |
| Freeze,24samples, min/median/max | 6.946/7.386/8.288s | 1.162/1.322/1.699s |
| Full Start,8samples, min/median/max | 26.432/27.826/30.302s | 8.950/9.218/9.796s |
| Parent GUI sampled peak working set,4cases | 1349–1459MiB | 294–319MiB |
| Parent GUI sampled peak handles,4cases | 12709–12734 | 12712–12734 |

Median speedup: freeze5.586×, Start3.019×. Current toolchain breakdown medians:
enumeration338.03ms, checks/open347.60ms, read/hash602.54ms. Каждый из24captures
прочитал6028files/755663686bytes; суммарно18135928464bytes, как в baseline.
Toolchain SHA256 `0A8FF873CAD65A80B5C8BFB6C1CAFDFB90229A18807996E4908E5D548C2E4F00`
и environment identity совпали с baseline во всех samples. Полные source/compiler
fingerprints стабильны внутри каждого case; между версиями Aether.dll они закономерно
меняются. Повторные DLL bytes проверяет SDK test, cross-version DLL equality не заявляется.
Parent handles до/после двух Scene Starts:751→716,736→699,757→718,736→701.

Scope: Windows x64, SDK10.0.403/runtime10.0.14, прежние GPU/driver/validation layer.
Fresh parent processes, без OS cache flush. WorkingSet/handles опрашиваются каждые100ms
у родительского GUI process: это не child-tree sum и не изолированный freeze allocation
profile. Два изменения применены вместе; отдельная причинная доля каждого не измерялась.
Эти локальные результаты не обещают такую же скорость на другом storage/toolchain.

Исходные failures сохранены. Первый Debug run:46/47; новая lease-probe использовала
MSBuild.dll, также удерживаемую build runner, поэтому post-Dispose exclusive read
был занят внешним owner. Probe перенесена на read-leased SDK `.version`, критерий
освобождения не ослаблен. Первый NuGet GUI publish завершился MSB3021/3027 с
`Недостаточно места на диске` при копировании SkiaSharp PDB, до native cases.
Удалены только cache/partial package-Release указанного failed temp run с проверкой
resolved absolute targets. Workspace logs/binlogs/report и consumer project сохранены;
новый independent cache/publish прошёл. Причина временного исчерпания диска отдельно
не профилировалась; более поздний free-space snapshot не описывает момент failure.

Evidence: `TestResults/PreparationCost-ED6b2b2a1-20261031/verification.json`,
`Run2.ps1`, `RunPackages.ps1`, `RetryPackageNative.ps1`, `Collect.ps1`, `cleanup.json`;
native reports: `SourceRelease/verification.json`, `PackageRelease2/verification.json`;
failed publish: `PackageRelease/verification.json`. Unit commands: `dotnet test
TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug|Release
--filter 'FullyQualifiedName~GameBuildPreparationTests|FullyQualifiedName~GameBuildCompilationTests'
--logger trx --results-directory <Focused2-configuration> -v quiet` (configuration
выбирается отдельно). Native: VerifyGui.ps1 с fresh package verification, прежним
validation directory, `-Profile SourceRelease|PackageRelease -HostKind All -Scenario Workflow`.
Оба профиля выполняются отдельно; package run использует новый feed/cache, без push.

Следующий шаг — process-local artifact reuse supported profile с content identity,
prestart verification, scene snapshot и прежним ownership. Disk cache, extended
graphs/tasks, document-aware Replace, gameplay HUD/input/Player/full ED6b и прежние
physical DPI/device-loss/K3/K7/K8/VRAM/popup/drain/distinct-PID/release gates открыты.

## ED6b.2b.2a: native acceptance and freeze baseline — 2026-10-31

По запросу продолжения выполнены native acceptance и измерение стоимости предыдущего
declarative preparation profile. В GameBuildPreparationSnapshot добавлены readonly
ElapsedMilliseconds/SourceMilliseconds/ToolchainMilliseconds/ToolchainFiles/ToolchainBytes.
Измеряется только freeze до SDK restore/evaluation; Source включает selection/copy/
validation, toolchain — enumeration/link checks/read leases/hash. Эти поля не входят
в content identity; новое наблюдение не включает cache/reuse или новые graph profiles.

GUI workflow требует CapturePreparation и6before/after/verify samples двух fresh
builds с единым preparation fingerprint; проверяет удаление preparation staging до
Start, прежние capture fingerprints, actual scene pixels, child-only Game.Logic,
preview reap, Pause/Step/Stop и parent history/dirty/selection. Сохраняются total Start
latencies и parent handles до/после. Runner дополнительно снимает wall/CPU time,
working set и handle peaks, не меняя300s command deadline.

Initial SourceRelease standard RED — старый30s UI polling timeout на втором Start.
Build/prestart ещё выполняли своё admitted work; сохранены5input snapshots с одинаковыми
source/preparation fingerprints. Toolchain6028files/755663686bytes, freeze7.43–8.76s
в этих диагностических files. [Initial evidence](../../TestResults/PreparationAcceptance-ED6b2b2a-20261031-1/SourceRelease/verification.json).
Приёмка теперь ждёт EditorScenePlayback.Pending под существующим case token до
UI-state assertion. Compiler120s, case4min и общий30s UI-state polling не увеличены.
Initial failure не выдан за passing и не удалён.

Новые scoped unit проверки: Capture_PinsExecutableRuntimeSdkAndEnvironmentWithStableIdentity,
Debug1/1 и Release1/1, failed/skipped0. Проверены positive bounded counters/durations
и сохранение fingerprint при повторных capture с разными timings. Полные Aether1704/
Editor943 от предыдущего среза остаются историческими, в этой работе не повторены.
Source published Release и fresh NuGet published Release ×standard/Cobalt и embedded/
MS DI:4/4workflows,44owned runtime children reaped/released,8game,40graceful Vulkan
validation reports0errors/0warnings; child producer devices/slots/surfaces и final
UI/GPU/handles0.4scene-play-paused PNG просмотрены: cube, Scene Guardian selection,
disabled X1.65, simulation paused, preview off, standard dirty caption сохранены.
Fresh15packages/headless8/8 также прошли; isolated feed/cache/external solution,
Debug build/Release publish ×DLL/apphost ×Cobalt/MS DI, публикации NuGet нет.

| Метрика | n | min | median | max |
|---|---:|---:|---:|---:|
| Freeze, ms |24|6946.27|7385.63|8287.51|
| Source, ms |24|11.68|14.17|41.35|
| Toolchain, ms |24|6931.34|7370.21|8246.17|
| Full Start, ms |8|26432.31|27825.56|30301.74|

Каждый capture проверяет755663686bytes(~720.66MiB) и6028toolchain files;24captures
прочитали16.89GiB. Toolchain фаза составляет99.755% суммарного freeze времени, но
CPU attribution между enumeration/link checks/open/hash отдельно не профилировалась.
Whole-app peaks12709–12734handles и1348.9–1459.4MiB working set; это не только freeze.
Parent handles до/после двух game runs:765→726,750→712,759→720,742→632.
Baseline локальный warm-cache, fresh parent processes; OS caches не очищались,
hardware/system/driver settings не менялись. Windows x64, SDK10.0.403/runtime10.0.14,
NVIDIA32.0.16.2101; validation layer manifest/DLL hashes сохранены в native reports.

[Общая сводка/24samples/PNG hashes](../../TestResults/PreparationAcceptance-ED6b2b2a-20261031-1/verification.json),
[source](../../TestResults/PreparationAcceptance-ED6b2b2a-20261031-1/SourceRelease2/verification.json),
[fresh packages](../../TestResults/PreparationAcceptance-ED6b2b2a-20261031-1/Packages/verification.json),
[NuGet native](../../TestResults/PreparationAcceptance-ED6b2b2a-20261031-1/PackageRelease/verification.json).
Точные commands/exit codes/inputs — там же; RunSource.ps1/RunSource2.ps1/RunPackages.ps1
и Collect.ps1 сохранены в evidence root. Основной путь — VerifyGui.ps1 с Profile
SourceRelease/PackageRelease, HostKind All, Scenario Workflow и extracted validation
directory; все commands имеют отдельные binlogs/output logs и собственные deadlines.

Следующий шаг — ED6b.2b.2a.1, измеренная стоимость toolchain. Поддержка arbitrary tasks/
generators/package/project graphs, watcher/cache/reuse/document-aware Replace и full
ED6b остаются отдельными. Это не physical DPI/OS-input/device-loss/K3/K7/K8/VRAM/final
alpha/release acceptance; прежние popup/output-drain/distinct-PID follow-ups не закрыты.

## ED6b.2b.2a: declarative preparation closure — 2026-10-31

Реализован opt-in CapturePreparation поверх TrackInputs+CaptureCompilation; Scene
Start включает новый профиль. Контракт и checklist — в
[Editor плане](EDITOR_PLAN.md#ed6b2b2a-снимок-declarative-preparation--2026-10-31).
Project tree, ancestor props/targets/editor configs, literal imports/linked sources,
HintPath и frozen contracts копируются до первого SDK process. XML читается из copies,
paths меняются только в copies. Custom tasks/targets, functions, SDKs/analyzers,
package/project graphs и external globs отклоняются до SDK execution.

Explicit installed dotnet/MSBuild.dll и exact --fx-version используют private
environment/home/temp/offline NuGet config. SDK/runtime/host/ref-pack files имеют
read leases до cleanup; их hashes и controlled environment входят в witness.
Installed directories/native Windows OS остаются trusted, это не arbitrary-MSBuild
sandbox. Source budget4096files/256MiB/64MiB per file; toolchain8192files/1GiB,
directory/import depth≤64. Legacy request flags default false и прежние custom
trusted preparation fixtures сохранены. Schema1/protocol/public DI APIs не менялись.

Проверки SDK строят source copies после изменения/удаления original source/import/
linked files и добавления late source; actual DLL содержит только frozen types.
Два fresh jobs имеют одинаковые fingerprints/DLL bytes; live source edit перед
VerifyInputs отклоняется. Проверены original error locations, cancel/cleanup,
private dependency bytes и реальный ManualTests/Game.Logic с ancestor props/contracts.
Job staging/leases очищаются до результата; failed cleanup сохраняет прежний retry.

Первоначальные findings и исправления:

- Full-drive mirror делал Windows working directory и внешние HintPath слишком
  длинными. Project scope и внешние directories получили короткие owned paths;
  mapping сохраняет original paths. После одного ResolveReferences без сокращения
  paths private dependency ещё отсутствовала; после short paths оба private cases прошли.
- NuGet ConfigurationDefaults при очищенном environment требовал machine-wide folder
  variables. Они явно направлены в private home; пользовательские NuGet feeds/
  environment не наследуются.
- Restore теперь идёт после freeze до первой observation, чтобы generated imports
  входили в обе observations. Нормализованы job/phase и rootless Directory metadata.
- Generated analyzer config содержит ProjectDir: он переписывается на original
  directory до Csc capture, сохраняя смысл paths и repeatable captured bytes.
- Первый полный Debug1703run имел1failure в новом private case; сохранён в
  [initial full](../../TestResults/ED6b2b2-20261031/TrueMoon.Aether.Tests-Debug/).
  После fixes полный Debug1704run имел1failure в прежнем
  ChildDiagnosticFactoriesTests.AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts(di:true):
  Accepted>0 относился к background error batch, а Delivery.App ещё не был импортирован.
  Ожидание привязано к конкретному event и child ApplicationId; assertions/deadlines
  не ослаблены. [Failure](../../TestResults/ED6b2b2-20261031/Final/TrueMoon.Aether.Tests-Debug/),
  [targeted2/2](../../TestResults/ED6b2b2-20261031/DiagnosticWait/).
- При review XML traversal исправлен пропуск siblings после удаления неактивного
  ProjectReference: traversal использует отдельный список элементов. Private case
  теперь проверяет этот порядок и HintPath metadata в форме attribute.
- Следующий Release1704run:1703passed/1failed в прежнем replacement process test,
  требующем distinct PID для всех8последовательных leases. В сохранённом
  [report](../../TestResults/CompiledGameFixtures/19666938d5644c11908aac1a0d525bf9/report.json)
  PID49676 встречается дважды; все8Reaped/ResourcesReleased=true и probe Passed=true.
  [TRX/log](../../TestResults/ED6b2b2-20261031/Final2/TrueMoon.Aether.Tests-Release/).
  Identity/reap assertions и production runtime в этой работе не менялись;
  distinct-PID assumption остаётся отдельным follow-up.
- Следующий Debug1704run:1703passed/1failed,50ms Stop retry timeout в прежнем
  ChildDiagnosticReceiverTests.BlockedImporter_RejectsConcurrentPacketsAndPreservesOwnedTransportAcrossTimeoutAndCancelledStopWait.
  [Failure](../../TestResults/ED6b2b2-20261031/Final3/TrueMoon.Aether.Tests-Debug/).
  Новые тяжёлые SDK integration fixtures (preparation/compilation) выделены в
  непараллельную collection по существующему test pattern; transport assertions,
  deadlines и production lifecycle не менялись. Не приписывать отдельному retry
  устранение всех прежних transport scheduling/OS PID ограничений.

Команды и сохранённые evidence: `TestResults/ED6b2b2-20261031/verify.ps1` запускает
`dotnet test <project>/<project>.csproj --no-restore --configuration <Debug|Release>
--logger trx --results-directory <case> -v quiet` для Aether/Editor по очереди.
Final results находятся в [Final4](../../TestResults/ED6b2b2-20261031/Final4/);
их counters/exit files нужно читать отдельно от initial failures. Final4: Aether1704/1704
и Editor943/943 отдельно Debug/Release, failed/skipped0, каждыйexit0.21новый case:
20preparation cases и1private-dependency variation. Новых tests для Renderer/Alloy/
Hosting и общего solution не запускали. SDK10.0.403,
runtime10.0.14, Windows x64. Рабочая копияmain имела исходные изменения root README,
untracked root AGENTS и src/.codex; они сохранены.

Свежая independent NuGet/headless проверка также завершена:15packages,8/8cases,
Debug build/Release publish ×DLL/apphost ×Cobalt/MS DI. Runtime DLL/apphost без
bootstrap отвергнуты как ожидается; graph/package/native payload/resources/loaded
assembly hashes и final source hashes проверены. Exit0, `GpuRun=false`, `WindowRun=false`.
Команда: `pwsh -NoProfile -File ManualTests/AetherPackageConsumer/VerifyPackages.ps1
-EvidenceDirectory TestResults/ED6b2b2-20261031/Packages`. [Package report](../../TestResults/ED6b2b2-20261031/Packages/verification.json),
[общая итоговая сводка](../../TestResults/ED6b2b2-20261031/verification.json).
Independent solution находится в private TEMP/feed/cache; публикации NuGet нет.
Этот existing headless lane использует low-level game requests и не заменяет новый
Scene Start preparation acceptance в native Editor.

Source/fresh-package native Scene Start acceptance и стоимость freeze учитываются отдельно.
UI renderer/Alloy APIs не менялись; physical/K3/K7/K8/VRAM, historical popup/drain,
new graph profiles, cache/reuse/Replace и полный ED6b остаются открытыми.
Документация MSBuild различает evaluation-only getItem/getProperty и выполнение
targets: [официальное описание](https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties).

## ED6b.2b.1: захваченная C#-компиляция — 2026-10-31

По запросу продолжения ED6b реализован первый compiler-input byte capture после
доверенной MSBuild preparation. Подробный контракт — только в
[ED6b.2b.1](EDITOR_PLAN.md#ed6b2b1-захваченная-c-компиляция--2026-10-31).
Scene requests включают CaptureCompilation+TrackInputs; legacy default false.
SDK Compile выполняется с SkipCompilerExecution/ProvideCommandLineArgs, отдаёт actual
Csc arguments после generated sources/ResolveReferences. Затем owned job копирует
source/generated/additional/config inputs, managed references/reference packs,
штатные SDK analyzers/generators и Csc DLL/deps/runtimeconfig/managed compiler files.
Отдельный dotnet exec компилирует эти copies без повторного выполнения compile targets.
Оригинальные witnessed files должны совпасть при capture. Действующие source evaluation
checks до/после Build и перед Start сохраняются. Дополнительно original non-generated
compiler/reference/config bytes сверяются после Build и перед Start; deleted generated
files завершённого job не нужны. Чужая пара witness/capture rejected до SDK job.

Capture — низкоуровневый resource compiler owner, не отдельный DI service. Прежние
single-job admission,120s maximum budget, cancel/Stop/reap/output drain/cleanup retry
сохраняются. Capture/work/output staging удаляется до возврата Build result;
prepare/capture diagnostics включены в exact retention allowlist. Artifact.Compilation
хранит immutable process-local hashes файлов/аргументов/SDK; schema1 прежняя, Open не
восстанавливает trusted capture. Private output DLLs берутся из captured inputs.

Profile — C# library с explicit compiler file inputs. Unknown/search/response-file
switches, custom compiler/environment, external analyzers, resource/ruleset/signing и
absolute analyzer-config sections fail closed. Entry DLL basename обязан совпасть.
Пределы4096files/64MiB per file/256MiB total включают compiler/reference packs;
preparation/response/config documents8MiB. Source tree mirror сохраняет relative config
globs. CallerFilePath и user PathMap сохраняются, generated/PDB job paths имеют stable
virtual prefix; structured source errors используют original paths после cleanup.
Выбранный способ проверен на текущем SDK и описан в
[официальном Roslyn C# target](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/MSBuildTask/Microsoft.CSharp.Core.targets).

Фактические Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest проверки:

| Suite | Debug | Release | Failed/skipped |
|---|---:|---:|---:|
| Aether |1683|1683|0/0|
| Editor |943|943|0/0|

4full runs5252cases;26новых cases. Узкие проверки29engine/build +18scene playback
прошли до full matrix. Captured Csc выполняется после порчи original source/reference/
compiler: PE constant остаётся authored, CallerFilePath проверен через IL literal с
обычным и user PathMap. SDK cases проверяют generated/reference pack/private files,
matching fingerprints и exact DLL bytes двух jobs/tickets, source diagnostics line1,
schema1 loss of provenance, MaxJobs1 retention и staging absence до смены diagnostic job.
Есть unsupported/bad/missing/oversize/cancelled capture partitions, original compiler/
reference changes, deleted job inputs, mismatched witness до SDK spawn. Real SDK search
path fail-closed и held preparation cancellation подтверждают cleanup/reap. Direct
GameEditor stale/matching Start используют capture; actual presentation и no-child
guards прежние. Scene coordinator test проверяет оба opt-in flags.
[4TRX/26cases/commands/source hashes](../../TestResults/CompilationCapture-ED6b2b-20261031-1/verification.json),
[final runner](../../TestResults/CompilationCapture-ED6b2b-20261031-1/RunFinal.ps1),
[quality review](../../TestResults/CompilationCapture-ED6b2b-20261031-1/test-quality.json),
[docs/namespace/hash audit](../../TestResults/CompilationCapture-ED6b2b-20261031-1/docs-audit.json).

Initial CallerFilePath RED выявил неверный Windows prefix при forward-slash mappings:
native separators исправили source identity, assertion не ослаблялся.
[RED TRX](../../TestResults/CompilationCapture-ED6b2b-20261031-1/CallerPath-first-red.trx).
Второй RED выявил volatile PDB/output paths при одинаковом input fingerprint; virtual
mapping теперь включает mirrored generated и actual owned output paths. Exact DLL-byte
equality прошла. [RED TRX](../../TestResults/CompilationCapture-ED6b2b-20261031-1/PdbPath-first-red.trx).
noconfig перенесён на actual command line за пределы response file, чтобы compiler
не игнорировал его и не добавлял warning. Initial synchronous-throw test overload
исправлен statement lambda; production guard и тип исключения сохранены.
Roslyn parse-only Builds root до новых files:12sources/0tests, sibling tests вне root;
это не coverage/absence conclusion. Rider findTests unavailable, fixtures прочитаны
напрямую. Broad research/plan/status — non-stageable OS-temp. Static gap/assertion
review завершён; empirical mutation/coverage не собирались. Deadlines не менялись.

Fresh15packages и8headless cases прошли с isolated feed/cache/external solution.
Published SourceRelease/PackageRelease ×standard/Cobalt и embedded/MS DI:4/4workflows
без дополнительных replay в этом срезе. Каждый выполняет2fresh scene game builds,
проверяет source/captured compiler fingerprints, actual GPU pixels/child-only game
DLL, preview reap до game startup, Pause/Step/Stop, parent history/dirty/selection.
На каждую native compilation188files/42105392bytes; fingerprints двух fresh builds
одинаковы при разных artifact/child IDs, capture staging удалён до Start.
44Runtime children reaped/released (8game),40graceful Vulkan reports0errors/0warnings,
resources/handles0, final UI/GPU/handles0.4PNG просмотрены: cube, selected Scene Guardian,
X1.65 disabled, paused status, preview off, standard dirty caption; geometry читаема.
Framework source/package hashes после runs совпадают.
[Packages](../../TestResults/CompilationCapture-Packages-20261031-1/verification.json),
[source GUI](../../TestResults/CompilationCapture-Native-SourceRelease-20261031-1/verification.json),
[NuGet GUI](../../TestResults/CompilationCapture-Native-PackageRelease-20261031-1/verification.json),
[acceptance/PNG hashes](../../TestResults/CompilationCapture-ED6b2b-20261031-1/acceptance.json).

Это не общий hermetic MSBuild build: preparation tasks/generators могут выполнять
произвольный IO, installed dotnet runtime и ambient environment остаются trusted.
Transient edits до freeze, полный pre-evaluation project/import closure и произвольные
postcompile/postbuild semantics не воспроизводятся этим profile. Owned job placement/
source-glob exclusion также входит в дальнейшую closure policy; проверенная matrix
держит job root вне игрового source tree. PackageReference/ProjectReference, arbitrary
compiler extensions, cache/reuse/Replace, full ED6b/HUD/input/Player и expanded native
game-crash/physical/loss/VRAM/release gates остаются открытыми. Historical popup и
output-drain causes этим passing run не объявляются исправленными. Другие module/
full solution suites не повторялись; control/frame/checkpoint formats прежние.

## ED6b.2a: наблюдаемые входы сборки — 2026-10-31

Продолжение ED6b добавляет process-local immutable content witness и stale input
rejection. Детальные критерии и дальнейший source capture ведутся только в
[ED6b.2a](EDITOR_PLAN.md#ed6b2a-наблюдаемые-входы-сборки-и-stale-start--2026-10-31).
Scene playback включает GameBuildRequest.TrackInputs; legacy requests default false.
Профиль — один net10.0 SDK project без PackageReference/ProjectReference, explicit
managed HintPath references. Неподдержанный graph отклоняется до compiler publication.

MSBuild возвращает evaluated items/properties и preprocessed imported project;
content identity включает bytes files, options/item metadata/order, imports и
configuration/reference/analyzer inputs. Compile ticket/timestamps и owned job paths
не входят в fingerprint. Потенциальные отсутствующие analyzer configs допускаются,
их появление меняет witness; missing Compile/reference не создаёт неполную identity.
Пределы:4096files,64MiB/file,256MiB total,8MiB/query и preprocessed document.
См. [официальную документацию evaluation queries](https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties)
и [MSBuild command implementation](https://github.com/dotnet/msbuild/blob/main/src/MSBuild/XMake.cs).

Проверка до/после Build предшествует Publish; GameEditor повторяет observation до
создания runtime, затем применяет прежние context/session/cancel fences. SDK probes
используют тот же compiler single-job gate,120s maximum budget, cancellation/reap/
output drain и retryable ownership. Diagnostic retention знает exact новые files.
Artifact.Inputs хранится только в памяти оригинального Build result: schema1 прежняя,
Open/Publish не создают trusted witness, tracked Start требует новый Build.

Фактические Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest проверки:

| Suite | Debug | Release | Failed/skipped |
|---|---:|---:|---:|
| Aether |1657|1657|0/0|
| Editor |943|943|0/0|

4full runs5200cases;22новых cases. Проверяются равные по длине byte edits с
восстановленным mtime, options/imports/order/additions/references/configs, bounds,
missing inputs/cancel, immutable copied dependencies/read-only files. Реальный SDK
проверяет stable fingerprint при разных jobs/tickets, >32KiB item metadata,
ErrorLog-conditional properties, schema1 reopening без witness и MaxJobs1 retention.
После source/addition/import/reference edits Verify отклоняет старый результат.
Held compile source edit не публикует artifact; cancel дожидается compiler/helper reap.
GameEditor stale Start не создаёт ни одной lease; matching Start ждёт actual frame
presentation точного BuildId и затем освобождает resources при Stop. Scene action
test проверяет включение TrackInputs. Existing legacy tests сохранены.
[4TRX/22cases/commands/source hashes](../../TestResults/BuildInputs-ED6b2a-20261031-1/verification.json),
[runner](../../TestResults/BuildInputs-ED6b2a-20261031-1/RunFinal.ps1),
[test-quality review](../../TestResults/BuildInputs-ED6b2a-20261031-1/test-quality.json),
[docs/namespace/hash audit](../../TestResults/BuildInputs-ED6b2a-20261031-1/docs-audit.json).

Initial SDK RED9 выявил потенциальные отсутствующие ancestor .globalconfig candidates:
[TRX](../../TestResults/BuildInputs-ED6b2a-20261031-1/Inputs-first-red.trx).
Следующие4fixture failures нарушали прежний exact DLL output allowlist случайным
basename private reference; fixture теперь сохраняет Game.Private.dll:
[TRX](../../TestResults/BuildInputs-ED6b2a-20261031-1/Inputs-second-red.trx).
Initial full Release RED6: diagnostic stdout file намеренно допускает chunk loss и
не подходит для machine JSON. Query теперь пишет непосредственно через MSBuild
-getResultOutputFile; размер проверяется до чтения, large metadata проверяется тестом.
Core capture contract не менялся. [Initial Release TRX](../../TestResults/BuildInputs-ED6b2a-20261031-1/InitialFull/TrueMoon.Aether.Tests-Release.trx).
При review выровнены ErrorLog global properties query/preprocess/compile; conditional
SDK option имеет прямое assertion. Все4full suites повторены на окончательном коде.
Roslyn parse-only Builds root до добавления файлов:9sources/0tests; sibling tests
вне этого root, вывода об отсутствии tests/coverage нет. Rider findTests недоступен;
existing fixtures прочитаны напрямую. Research/plan/status — non-stageable OS-temp.
Empirical mutations и coverage не собирались; deadlines/assertions не ослаблялись.

Fresh15packages и8headless cases прошли с isolated feed/cache/external solution.
Финальные published SourceRelease ×standard/Cobalt и embedded/MS DI, затем свежий
PackageRelease profile2 ×те же hosts:4/4workflows. Каждый проверяет2fresh game builds
с distinct artifact/child IDs и одинаковым verified input fingerprint, actual scene
GPU pixels, preview reap до game startup, Pause/Step/Stop и parent history/dirty/selection.
В финальных4cases44children reaped/released (8game),40graceful Vulkan reports0errors/
0warnings/resources/handles0; final UI/GPU/handles0.4scene-paused PNG просмотрены:
actual red cube, selection, inspector X1.65 disabled во время игры, paused status,
preview off и dirty caption standard host. Package source hashes после runs совпадают.
[Packages](../../TestResults/BuildInputs-Packages-20261031-1/verification.json),
[source GUI](../../TestResults/BuildInputs-Native-SourceRelease-20261031-1/verification.json),
[final NuGet GUI](../../TestResults/BuildInputs-Native-PackageRelease-20261031-2/verification.json),
[final package runner](../../TestResults/BuildInputs-ED6b2a-20261031-1/RunPackageFinal.ps1),
[acceptance/PNG hashes](../../TestResults/BuildInputs-ED6b2a-20261031-1/acceptance.json).

Первый package embedded case остановился до SDK Build на Until открытия status Preview
popup (Workflow line466,30s). Standard case этого profile прошёл; failed attempt
сохранён отдельно и не входит в final4cases. Exact frozen binary replay с новым
disposable case directory, прежними30s condition/300s process deadlines и без code
changes прошёл весь workflow. Затем rebuilt fresh package GUI/runtime profile2 дал2/2.
[Initial attempt/logs](../../TestResults/BuildInputs-Native-PackageRelease-20261031-1/verification.json),
[exact binary replay](../../TestResults/BuildInputs-ED6b2a-20261031-1/exact-replay.json).
Причина popup timeout не установлена, это открытый ED8 follow-up, не исправленный bug.
Replay/initial extra cases не суммируются с final44children или headless counts.

Witness обнаруживает наблюдаемые persistent changes, не изолирует transient edit-and-
revert, произвольный target/generator/environment IO или edits после последней проверки.
Далее ED6b.2b immutable supported source closure/capture до cache/reuse/Replace;
PackageReference/ProjectReference и source watcher ещё не добавлены. Полный ED6b,
input/HUD/Player/native game-crash matrix, physical/device loss/VRAM/release gates и
прежний output-drain cause остаются открытыми. Другие module/full solution suites
не повторялись; UI renderer/control/frame/checkpoint formats не менялись.

## ED6b.1: запуск открытой сцены — 2026-10-31

По запросу «Начни реализацию ED6b — запуска сцены из редактора» штатный App graph
регистрирует EditorScenePlayback. ▶ захватывает current committed in-memory scene,
включая dirty/untitled, валидирует bounded primitive projection, drains Edit Preview,
устанавливает context, делает fresh Build и Start→actual first-present→Play.
Отдельный Start оставляет Pause; Pause/Resume/Step/Stop используют тот же coordinator.
Документ/history/dirty checkpoint/selection не заменяются runtime state; Save не обязателен.
Неприменённые inspector drafts/conflicts veto-ят Build/Run. Подробный контракт ведётся
только в [ED6b.1](EDITOR_PLAN.md#ed6b1-запуск-runtime-копии-открытой-сцены--2026-10-31).

Game operations используют shared EditorDocumentCommands gate и drain barrier;
file/edit/project/exit не проходят через pending scene work. Exact project/content
guards отклоняют direct changes, replacement и IO до startup. Start acceptance
согласован с document owner gate. Новый GameEditor.StartBuildAsync атомарно требует
expected ContextVersion и candidate, без fixed-runtime fallback. Старый StartAsync
и пятиаргументный EditorViewModel constructor сохраняются; positional record API прежний.
Viewport readiness ожидается до acquisition runtime ownership. Failed startup drains
owned game; original+cleanup causes сохраняются, failed Stop допускает explicit retry.

Actual Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest:

| Suite | Debug | Release | Failed/skipped |
|---|---:|---:|---:|
| Aether |1635|1635|0/0|
| Editor |943|943|0/0|

4full runs5156cases;24новых cases (Aether4, Editor20). Прямые проверки включают
unsaved scene ID/revision/world coordinates, exact object/history/dirty preservation,
no Save writes, fresh compile tickets, controls/restart; uncooperative build/preview
drain, direct document/IO/context change, cancel before viewport readiness, failed
startup/cleanup/Stop retry и forced/user-vetoed Exit. Core cases исключают missing/
stale candidate fallback и требуют actual presentation. Cobalt/MS DI default graph
проверяет Run availability без предварительного candidate; legacy tests сохранены.
[Audit/4TRX/24cases/exact commands/source hashes](../../TestResults/ScenePlayback-ED6b1-20261031-1/verification.json),
[final runner](../../TestResults/ScenePlayback-ED6b1-20261031-1/RunFinal.ps1),
[quality review](../../TestResults/ScenePlayback-ED6b1-20261031-1/test-quality.json),
[docs/namespace/diff audit](../../TestResults/ScenePlayback-ED6b1-20261031-1/docs-audit.json).

Initial RED7: faulted/cancelled runtime task прерывала Exit до cleanup. Shared pending
теперь является actual drain barrier, outcome возвращается отдельному operation caller.
[RED TRX](../../TestResults/ScenePlayback-ED6b1-20261031-1/exit-drain-red.trx).
Первый compile shadowing исправлен. Новый availability fixture изначально мерил rich
inspector на simple headless surface; тест DI/state отделён от нерелевантного rendering.
Случайно изменённая строка прежнего test fixture возвращена к исходной Scene.
Assertions/deadlines не ослаблялись. Roslyn parse-only production Editor root:
87sources/0tests в этом root, без вывода об отсутствии sibling tests/coverage.
IDE findTests недоступен; existing fixtures прочитаны напрямую. Broad research/plan/
status — non-stageable OS-temp; empirical mutations и coverage не запускались.

Первый standard source probe прошёл до окончательного разделения viewport readiness/
startup admission и считается промежуточным:
[probe](../../TestResults/ScenePlayback-Probe-SourceRelease-20261031-1/verification.json).
Затем rebuilt fresh15NuGet packages, isolated feed/cache/external solution и8headless
cases прошли. Финальные published SourceRelease/PackageRelease ×standard/Cobalt и
embedded/MS DI:4/4workflows. Consumer компилирует собственную Game.Logic в отдельном
SDK process; каждый workflow делает2fresh game starts с distinct build IDs/children,
проверяет scene GPU pixel, Pause/Step/Stop, actual child-only game assembly paths,
preview reap до game start, parent history/dirty/selection и Undo после Stop.
Resume проверен прямым coordinator test; отдельного native Resume утверждения нет.
Существующий project/document/preview/diagnostics/crash/recovery workflow сохранён.

44native children reaped/released, из них8game;4owned preview crashes,40graceful
Vulkan reports errors0/warnings0 и resources/handles0, final UI/GPU/handles0.
Killed preview не предоставляет final report.4новых PNG просмотрены: actual cube,
paused status, retained dirty caption/selection и disabled authoring fields X1.65;
standard/embedded source/package geometry читаема. Fingerprints совпадают с final
package baseline. Headless8cases имеют собственный lifecycle scope и не суммируются
с native counts.
[Packages](../../TestResults/ScenePlayback-Packages-20261031-1/verification.json),
[source GUI](../../TestResults/ScenePlayback-Native-SourceRelease-20261031-1/verification.json),
[NuGet GUI](../../TestResults/ScenePlayback-Native-PackageRelease-20261031-1/verification.json),
[acceptance/4PNG hashes](../../TestResults/ScenePlayback-ED6b1-20261031-1/acceptance.json).

Compile SourceRevision — запросный ticket, не hash всего MSBuild source graph.
Hermetic capture/external source edits/artifact reuse и document-aware Replace — ED6b.2;
полный gameplay input/HUD/Player и native game crash/recovery matrix ещё нужны.
Primitive/control/frame/checkpoint limits и formats прежние. Другие module/full
solution suites, physical DPI/device loss/VRAM/long-load/release gates не повторялись.
Прежний intermittent output-drain cause не объявляется исправленным этими runs.

## Editor и общие Bindable/ViewModel — 2026-10-31

EditorViewModel, status/document/inspector/hierarchy/diagnostics models наследуют
Argentis ViewModel → Bindable вместо отдельных реализаций INotifyPropertyChanged.
InspectorProperty/TransformField используют Bindable для редактируемых значений.
Explicit dependent notifications, partial declarations, exact project admission,
owner-thread checks и конкретный Dispose cleanup сохранены; DI graph прежний.
Общий контракт/детальные проверки — [UIA1e](../alloy/PLAN.md#uia1e-bindable-и-viewmodel--2026-10-31),
[UI history](../alloy/HISTORY.md#uia1e-bindable-и-viewmodel--2026-10-31).

Текущий SDK10.0.403/net10.0/xUnit2/VSTest: Editor923/923 и UI919/919 отдельно
Debug/Release, failed/skipped0. Новые Bindable10cases входят в UI; существующие
Editor tests продолжают проверять models/views/subscriptions и Cobalt/MS DI compositions.
[Команды/5TRX/source hashes](../../TestResults/Bindable-20261031-1/verification.json).
Aether/Core/Hosting/другие suites, full solution и package/native/physical matrix
не повторялись. Предыдущая ED1b.2 package/native acceptance относится к предыдущим
input fingerprints. Следующий основной engine consumer — прежний ED6b.

## ED1b.2: project picker и runtime rebinding — 2026-10-31

Стандартное меню «Проект» использует новые OpenProject/CloseProject identities в
прежнем EditorDocumentCommands. Existing typed View/Data/Style path tools разделяют
project/scene/first-save drafts. Project snapshot и binding-ready observation расширяют
record init properties, не меняя positional constructor/deconstruction. Публичные
старые constructors сохранены; compositions без runtime binding имеют disabled project
actions. App startup по manifest использует ту же tracked queue и сохраняет rollback.
Подробный policy/ownership contract — только в
[ED1b.2](EDITOR_PLAN.md#ed1b2-выбор-проекта-в-ui-и-runtime-rebinding--2026-10-31).

Project candidate готовится до unsaved decision/preview stop. Coordinator остаётся busy
до actual preview drain, metadata commit и binding actual Active. GameEditor.SetContext
сбрасывает прежний candidate; новый ClearContext убирает project identity и fence-ит
pending results, не останавливая runtime сам. UI project actions требуют stopped game/
idle compiler. После switch EditorCommands остаётся reusable; terminal StopAsync не
вызывается вместо project transition. Cleanup/binding errors агрегируются, failed binding
закрывает game/document admission до recovery. Workspace очищает selection/data на UI
owner; stale presentation не допускает game action даже после отдельного model refresh.
Финальная source/assertion сверка выявила риск смешанного кадра: новый project snapshot
мог сочетаться с прежним runtime/candidate status. Present теперь читает document/project
перед runtime readers; exact project guard блокирует action при переходе между чтениями.
Регрессия ProjectSnapshot_PrecedesRuntimeReadersAndMixedFrameCannotEnableStaleCandidate
выполняет switch внутри runtime reader, проверяет отказ Run/Build и следующий coherent кадр.

Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest, actual final runs:

| Suite | Debug | Release | Failed/skipped |
|---|---:|---:|---:|
| Aether |1631|1631|0/0|
| Editor |923|923|0/0|

4full runs,5108cases суммарно;22new cases (Aether2, Editor20). New cases прямо проверяют
Save/Discard/Cancel, SDK/save/drain failures, uncooperative preparation/preview drain,
actual identity после cleanup/binding failures и recovery, unsupported legacy graph,
shared path/menu actions, retained focus/theme/control tree, fresh workspace admission,
две App/DI composition и actual GameEditor target/context clearing. Engine tests проверяют
candidate/version fencing, неизменённый borrowed runtime и observed held-build drain.
[Audit/4TRX/22cases/команды/source hashes](../../TestResults/ProjectPicker-ED1b2-20261031-1/verification.json),
[full runner](../../TestResults/ProjectPicker-ED1b2-20261031-1/RunFull.ps1),
[final Editor runner](../../TestResults/ProjectPicker-ED1b2-20261031-1/RunEditorFinal.ps1),
[bounded quality review](../../TestResults/ProjectPicker-ED1b2-20261031-1/test-quality.json),
[docs/namespace/diff audit](../../TestResults/ProjectPicker-ED1b2-20261031-1/docs-audit.json).
Roslyn parse-only run на production Editor root увидел86sources/0tests в этом root;
он не pair-ит sibling test project и не доказывает отсутствие tests/line coverage.
IDE findTests недоступен; existing fixtures прочитаны напрямую. Broad test research/
plan/status находятся в OS-temp и не входят в worktree. Empirical mutations и coverage
не запускались; source/assertion review выполнен без временных source mutations.

Initial header regression: fan-out notifications задержали RunButton enabled update.
Header refresh coalesced, unchanged path assignments больше не уведомляют bindings.
[Initial unit RED](../../TestResults/ProjectPicker-ED1b2-20261031-1/editor-header-latency-red.trx).
Native attempt1 нажал Cancel до layout решения; attempt2 standard прошёл, embedded
пытался вводить first-save path до layout. Это harness preconditions: input теперь ждёт
attached/enabled/visible bounds и settled layout; assertions/deadlines прежние.
[Attempt1](../../TestResults/ProjectPicker-Native-SourceRelease-20261031-1/verification.json),
[attempt2](../../TestResults/ProjectPicker-Native-SourceRelease-20261031-2/verification.json),
[embedded source probe](../../TestResults/ProjectPicker-Probe-Embedded-20261031-3/verification.json).

Final fresh15NuGet packages,8headless Game cases и isolated feed/cache/external solution
прошли. Published SourceRelease/PackageRelease ×standard/Cobalt +embedded/MS DI:
4/4workflows. Проверены path prompt/menu, Cancel с сохранением scene/history/selection,
Save untitled→switch на другой manifest/game path, real active preview reap/release до
publication, cleared context/selection при Close, reopen исходного проекта и persisted
scene с теми же IDs/value и clean history. Предыдущий edit/history/preview/diagnostics/
owned crash workflow тоже проходит.32native children reaped/released,4owned crashes;
28graceful Vulkan reports errors0/warnings0 и resources/handles0, final UI/GPU/handles0.
Killed child не предоставляет final validation report.14finalPNG просмотрены: readable
project actions/path prompt, новый caption, пустая hierarchy и скрытый inspector после
switch/close. Input fingerprints совпадают с final package baseline.
[Packages](../../TestResults/ProjectPicker-Packages-20261031-4/verification.json),
[source GUI](../../TestResults/ProjectPicker-Native-SourceRelease-20261031-4/verification.json),
[NuGet GUI](../../TestResults/ProjectPicker-Native-PackageRelease-20261031-4/verification.json),
[acceptance/14PNG hashes](../../TestResults/ProjectPicker-ED1b2-20261031-1/acceptance.json).
После snapshot-order regression Editor suites повторены; final package/native matrix4
собрана заново из исправленных sources. Passing matrix3 сохранена как промежуточная.

ED1 switch/Close определён в этом bounded idle/stopped profile. Document-aware ED6b
Build/Play, OS file picker/recents/wizard, extended backend/provider/native, physical
input/DPI/device loss/VRAM/long-load и release gates остаются отдельными. Другие module/
full-solution suites не повторялись. Прежний ED4c.2 intermittent output-drain cause
не объявляется исправленным этими passing runs. Далее ED6b source/artifact revision и
immutable authoring snapshot policy, затем игровой Build/Play workflow.

## ED1b.1: project/document transition API — 2026-10-31

EditorProjects получил SwitchAsync(path, DocumentCloseChoice, saveCurrentAs, token)
и explicit-policy CloseAsync. Используются прежние coordinator/factory/document owners,
без новых DI services/scopes. Подробная policy ведётся только в
[ED1b.1](EDITOR_PLAN.md#ed1b1-projectdocument-transition-api--2026-10-31).
TryFenceForProject сравнивает точный captured SceneDocument под тем же document gate,
что Apply/Undo/Redo, и только после полного candidate preflight разрешает discard.
Save использует существующий persistence/checkpoint path. Старые Open/Create и
clean-only Close signatures/behavior сохранены; metadata-only composition доступна.

Public remarks/packaged README явно требуют от host остановить/drain runtime/preview
и удержать admission команд. Текущий EditorSession задаёт GameEditor context только
при startup, Workspace.HasProject фиксирован при composition: UI picker пока нельзя
включить без dynamic context/artifact fencing и согласования models/subscriptions.
Этот завершённый API-срез предшествует общей UI-транзакции и ED6b Play workflow.

Фактические Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest результаты:

| Run | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Focused ProjectTransitionsTests, Debug |20|0|0|
| Full Editor, Debug |903|0|0|
| Full Editor, Release |903|0|0|

20new cases проверяют discard/save/untitled/existing path; invalid SDK, Cancel и
failed Save с сохранённым Undo; truthful checkpoint после file commit перед отменой;
late edit, same-ID/revision document replacement, edit during Save, pending IO;
Close drain с uncooperative preflight, dirty policies/reopen/failed Save retry;
post-publication cleanup failure/retired owner retry; invalid choices/precancel.
Прежние document/project/registration и остальные Editor cases включены в903.
Assertions сверены с source: значения/owner identity, exact saved snapshot/path/stamp,
history после failure, candidate disposal и отсутствие late publication проверяются
напрямую. Initial compile-only attempt исправил SceneFileStamp.Sha256, UndoCount и
xUnit async assertion signatures; тесты в той попытке не запускались. Initial16 и
final20 focused прошли; после compatibility refinement full Editor повторён в обеих
конфигурациях. IDE findTests недоступен, использованы existing fixtures через direct
search; coverage/project-wide source pairing не выполнялись.

[Audit/3TRX/cases/source hashes](../../TestResults/ProjectTransitions-ED1b1-20261031-1/verification.json),
[runner/точные команды](../../TestResults/ProjectTransitions-ED1b1-20261031-1/RunFull.ps1),
[initial compile log](../../TestResults/ProjectTransitions-ED1b1-20261031-1/compile-attempt1.log).
Local links/anchors, namespaces и git diff --check проверены;
[документационный audit](../../TestResults/ProjectTransitions-ED1b1-20261031-1/docs-audit.json), staged0.
Другие module/full solution suites, NuGet delivery, native UI/GPU/physical и runtime
rebinding этим шагом не повторялись. ED1 четвёртый пункт и ED6b остаются открытыми.
Прежний intermittent runtime output-drain follow-up сохраняется отдельно.
Следующий шаг: typed UI project picker вместе с workspace/runtime/build rebinding и
общей command admission; затем document-aware Build/Play.

## ED4c.3: операции объектов, поиск и контекстное меню — 2026-10-31

В существующий ISceneAuthoringSession добавлен optional EditEntities: closed family
CreateEntitiesBatch/DeleteEntitiesBatch/DuplicateEntitiesBatch. Прежние factory,
constructors и методы сохранены; custom sessions могут явно отклонить capability.
Общий Execute использует прежние revision/receipt/operation/history/limit/cancellation
contracts и один commit после полной scene resolution. Новых services/scopes/owners нет.
Детальная descendant/reference/catalog/placement policy ведётся только в
[ED4c](EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).

Editor добавляет root/child presets с Transform (cube также Appearance), создаёт
definition/missing canonical schemas тем же engine batch. Удаление — explicit subtree,
duplicate — fresh entity ID map с remap внутренних parents и сохранением child order,
inheritance/names/local patches. Catalogs при удалении не очищаются. Shared commands
дают icons, Insert/Ctrl+Insert/Ctrl+D/Delete и right-click/Shift+F10/context button.
Ctrl+F/Enter ищет name/ID с reveal без фильтрации/мутации scene. Model остаётся без UI
controls/theme; View/Data/Style, Cobalt factories и UI-owner lifecycle прежние.

Общий MenuPopup использует MenuAction/MenuItem/IUiCommand/OverlayHost, bounds1..64,
disabled navigation и dismiss-before-execute. Escape focused popup обрабатывает
раньше expanded menu bar и возвращает focus context trigger. Plain F10 сохранён, Shift+F10 не
перехватывается menu bar. Captured context version/selection и engine expected owner
fence не допускают late action над заменённым document. Retained projection coalesce-ит
latest items/selection вместе и подавляет feedback selection при SetItems. Commands
обновляют availability только при изменении соответствующих свойств.

Фактические Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest проверки:

| Suite | Debug | Release |
|---|---:|---:|
| Aether |1629|1629|
| Editor |883|883|
| Alloy |909|909|
| Hosting |117|117|

Все final8runs failed/skipped0,7076cases суммарно. Новых53cases: engine27, Editor17,
Alloy9. Initial focused engine26/26 до capacity boundary; Editor86/86 до queued
projection regression; final MenuPopup9/9. Editor/Alloy после последних fixes повторены
целиком Debug/Release.
IDE findTests недоступен; существующие fixtures/tests найдены прямым поиском, coverage
и project-wide source/test pairing не выполнялись.
[Audit/точные команды/8TRX/fully-qualified новые cases](../../TestResults/EntityOperations-ED4c3-20261031-1/verification.json),
[full runner](../../TestResults/EntityOperations-ED4c3-20261031-1/RunFull.ps1),
[final Editor/Alloy/acceptance runner](../../TestResults/EntityOperations-ED4c3-20261031-1/RunMenuFinal.ps1).

Initial focused Editor4failures сохранены: две неверные fixture preconditions
(New поверх dirty document не заменяет его), selection feedback при удалении строки
и redundant command notifications, откладывавшие inspector refresh. Исправлены fixtures,
projection guard и адресная availability notification.
[Initial TRX](../../TestResults/EntityOperations-ED4c3-20261031-1/editor-narrow-attempt1.trx).
Initial published SourceRelease обнаружил queued Select(newID) до SetItems при
нескольких commits в одном update. Это реальный UI defect, не изменение timeout:
добавлен QueuedSelectionAndMultipleStructuralCommits_ProjectLatestItemsBeforeSelectingNewIdentity,
latest items/selection применяются одним coalesced callback; reattachment сбрасывает
stale scheduling flag. [Native RED](../../TestResults/EntityOperations-Native-SourceRelease-20261031-1/verification.json).

Escape regression требует dismissal и focus context trigger при expanded menu bar.
Initial assertion ошибочно требовала сохранить expanded bar после OverlayHost.Closed;
исправлена на прежнюю collapse policy, защита focus от перехвата bar сохранена.
[Focused initial assertion](../../TestResults/EntityOperations-ED4c3-20261031-1/menu-escape-attempt1.trx),
[final focused9/9](../../TestResults/EntityOperations-ED4c3-20261031-1/menu-final.trx).

Final fresh15packages с окончательным packaged README, isolated feed/cache и external
solution прошли8headless Game cases (Debug/published Release ×dll/exe ×Cobalt/MS DI).
Published SourceRelease и PackageRelease standard/Cobalt workflows2/2 проверили New→
cube/child→Transform2.25→subtree duplicate/delete/Undo/Redo→ID search→Save/reopen с теми
же IDs/parent/value и clean history. Прежний preview/metadata/diagnostic/crash recovery
workflow также прошёл.14native children reaped/released,2owned crashes,12graceful
Vulkan reports errors0/warnings0, active UI/GPU resources/handles0. Killed child не
даёт final validation report. Source fingerprints совпали;4finalPNG просмотрены:
читаемые actions/shortcuts, раскрытый cube/child, selected child и сохранённый X2.25.
[Final packages](../../TestResults/EntityOperations-Packages-20261031-3/verification.json),
[source GUI](../../TestResults/EntityOperations-Native-SourceRelease-20261031-3/verification.json),
[package GUI](../../TestResults/EntityOperations-Native-PackageRelease-20261031-3/verification.json),
[acceptance/copies](../../TestResults/EntityOperations-ED4c3-20261031-1/acceptance.json).

ED4c.3 закрыт в этом bounded profile. Полный ARCH1b/ED4, multiselect/typed references/
catalog management, physical input/DPI, extended native/provider/backend matrix,
device loss/VRAM/long-load и release gates открыты. Core/Enerit/Mithril/full solution
suites не повторялись. Прежний intermittent output-drain snapshot ED4c.2 не объявлен
исправленным passing этого шага. Далее project picker/switch и ED1/ED6 workflow.
Итоговая документационная сверка проверяет добавленные local links/anchors, namespace
новых feature files и git diff --check; staged changes0.
[Audit](../../TestResults/EntityOperations-ED4c3-20261031-1/docs-audit.json).

## ED4c.2: имена и порядок сущностей — 2026-10-31

EntityInstance/ResolvedEntity получили optional Name/SiblingOrder; прежний constructor
сохранён. Имена принадлежат экземплярам, stable IDs не меняются. Полный контракт
валидации/порядка/совместимости ведётся в [Editor плане](EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Общий HierarchyEditBatch содержит names и последовательные Last/Before/After placements,
использует прежние expected revision, operation receipts, limits, history и один commit
после полной resolution/cancellation. Invalid name/anchor/cycle не публикует частичный
документ. Reparent/Transform/component clones сохраняют metadata. Сортировка siblings:
rank (null=0), затем Guid; destination ranks нормализуются, source declaration order
сохраняется. JSON reader принимает строгие v1/v2; writer выбирает v2 при наличии
metadata, без неё сохраняет v1. Старый reader отклонит v2 вместо silent downgrade.

Editor document commands/session используют прежний IO/runtime/owner gate. Model
хранит name draft и exact authoring state, не содержит controls. F2 открывает retained
TextBox поверх строки; Enter/пустое имя commit/reset, Esc/focus/selection/owner/revision/
admission/dispose отменяют draft. Unchanged name и reorder на ту же позицию не создают
UI history entry. Clipboard/caret остаются TextBox; document/history shortcuts во
время name draft не применяются. Ctrl+↑/↓ и края строки запрашивают before/after;
обычный перенос добавляет последним к новому parent. ID, local patches, selection,
expansion, scroll и View parts сохраняются. Новых DI services/scopes/owners нет.

Общий TreeView.ReorderEnabled default=false сохраняет прежних consumers. Новый
PlacementRequested не меняет source items. Element.FocusChanged покрывает transfer,
FocusLost и detach. UiSession отменяет input перед закрытием dispatcher; созданные
callback уведомления не исполняются при teardown. Focus больше не хранится в модели.

Фактические проверки Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest:

| Suite | Debug | Release |
|---|---:|---:|
| Aether |1602|1602|
| Editor |866|866|
| Alloy |900|900|
| Hosting |117|117|

Все8полных runs: failed/skipped0. Добавлено47cases: engine21, Editor19 и UI7.
Focused final до двух последних Editor cases: engine21/21, Editor135/135, Tree48/48;
оба последних cases входят в полные866/866. IDE findTests недоступен; существующие
fixture/test files найдены прямым поиском. Полный suite discovery/coverage не делался.
Точные команды, TRX и все fully-qualified новые cases — в
[verification.json](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json),
[full runner](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/RunFull.ps1).

Initial focused runs выявили ошибку ImmutableArray equality assertion, назначение
фокуса до применения queued visibility binding и notification в закрытый dispatcher
при Dispose. Исправлены sequence assertion, синхронная установка текущего draft
перед Focus (включая text в том же native input batch) и порядок CancelInput/Close.
Cancellation/dispose и shell shortcut regressions прошли в final full suites.

Первый package attempt отклонён hash guard: consumer README был изменён после
снятия source fingerprints. Evidence сохранён, passing не засчитан. Второй fresh
package/headless/native2/2 прошёл, но visual review обнаружил missing arrow glyphs
в Noto Sans hint. Подсказка заменена на ASCII Up/Down; Editor866/866 повторён
Debug/Release; новый final package/native proof после этого изменения приведён ниже.

Третий package run выявил отдельный output-drain timeout в build-Debug-exe-di,
generation3, после reap. stdout/stderr read=WaitingForActivation, file=WaitingForChunk;
весь фактический вывод24bytes/stream записан, dropped0, ThreadPool9/pending0.
Runtime metadata всех3children без ошибок; live PID check подтвердил отсутствие
всех3процессов. [Failure](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/headless-drain-failure.txt),
[snapshot](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/headless-drain-snapshot.json),
[неполный package manifest](../../TestResults/HierarchyMetadata-Packages-20261031-3/verification.json).
Два exact binary replays с теми же cache/provider/exe и прежними2s drain/240s outer
deadline прошли в новых case directories, без изменения production code или budgets.
[Команды/exit0](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/drain-replays.json).
Это новая intermittent observation ARCH2e.4; причина неизвестна. Ни прошедшие replays,
ни последующая приёмка не объявляют её исправленной; отдельный follow-up остаётся.

Финальный fourth fresh run:15NuGet packages, isolated feed/cache и independent
solution вне checkout;8headless Game cases прошли (Debug/published Release ×
dll/exe ×Cobalt/MS DI). Все source hashes совпадают с окончательными файлами.
SourceRelease и PackageRelease standard/Cobalt GUI workflows2/2 проверили F2 name,
Ctrl+Down reorder, общий Undo/Redo, Save/reopen JSON v2 с clean history и прежними IDs,
Transform/preview/crash recovery и сохранение inspector parts.14native children
reaped/released,2owned crashes;12graceful Vulkan reports errors0/warnings0,
active UI/GPU resources/handles0. Killed child не даёт final validation report.
4окончательных PNG просмотрены: исходный ID label/order и сохранённое Scene Guardian
после перестановки; Up/Down hint читается в используемом Noto Sans без missing glyphs.

[Final packages](../../TestResults/HierarchyMetadata-Packages-20261031-4/verification.json),
[source GUI](../../TestResults/HierarchyMetadata-Native-SourceRelease-20261031-4/verification.json),
[package GUI](../../TestResults/HierarchyMetadata-Native-PackageRelease-20261031-4/verification.json),
[acceptance audit/copies](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/acceptance.json),
[Editor final runner](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/RunEditorFinal.ps1),
[replays/final runner](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/RunRecoveryFinal.ps1).
Core/Enerit/Mithril/full solution suites, coverage, physical input/DPI, extended
backend/provider matrix, device loss/VRAM/long-load и release gates не повторялись.
ED4c.2 закрыт в этом scope. Далее ED4c.3 create/delete/duplicate, затем project/workflow;
новый intermittent output-drain follow-up самостоятельный и не закрыт passing rerun.

## ED8a.2: child health и device observations — 2026-10-31

В App forwarder добавлен borrowed memory-only RuntimeDiagnosticObservationSource:
Core health view и cached GpuResourceHost.LastDevice. Source не делает IO/native
queries/flush/wait и не владеет зависимостями. Device value фиксируется после
успешного создания реально выбранного Vulkan producer; reusable slots заимствуют
тот же value. LastDevice переживает cleanup, не содержит handles/UUID и не означает
current GPU health. Aether не получает UI dependency. Factory создаётся в App
composition, custom factories и прежний3-argument standalone sender сохранены.

DiagnosticDeliveryPacket binary envelope не меняется. Version1 body — прежний array;
version2 body содержит Records/Observation/ObservationOmittedByBudget. Общие body,
wire/rate/request/queue/shutdown limits прежние, wrapper резервирует128bytes.
Observation до8KiB/16sinks, numeric collector/ring/sink/emergency/retention counters,
optional last GPU device; text errors заменены фиксированным marker. Sinks и целый
snapshot могут быть omitted с отдельными counters. Empty ring может отправить
health heartbeat. Source failure не блокирует records. Receiver сохраняет exact
identity/rate guards, валидирует весь packet до import/cache, отвергает negative/
foreign/oversized/error-text input и публикует detached readonly snapshot. Legacy
packet/omission не стирает last observation. Native lease сохраняет его после reap;
child CapturedUtc не является freshness/final-health/durability гарантией.

Editor Health показывает последнее наблюдение с timestamp. Polling не переписывает
inspected details; explicit Refresh заменяет снимок. Schema2 report добавляет bounded
runtimeObservations по process/session/generation, numeric health/device availability
и budget omissions. Child sink error text исключён в обоих text profiles. Schema1
остаётся metadata-only; существующие report/DI APIs и signatures сохранены.
UI OpenGL/Vulkan surfaces предоставляют optional IUiRenderDiagnostics с cached
UiRenderDeviceInfo, заполненным host при initialization. Getter без native calls;
View копирует только strings в model. External raw hosts могут не дать capability,
CPU/raster device остаётся unavailable. [Контракт](DIAGNOSTICS_DELIVERY.md#runtime-observation-version2--2026-10-31).

Проверки Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest:

- Narrow Aether138/138, Editor84/84;11новых engine +5editor cases. Real-pipe empty
  ring/health, retained evidence/read-only collections, invalid identity/counters/
  sink count/error text/body size, byte/count omissions, source failure with records,
  no native work in source и Cobalt/MS DI factories проверены непосредственно.
- Полные Debug: **Aether1581, Editor847, Alloy893, Hosting117**, failed/skipped0.
  Initial Release Aether1580passed/1failed: `RuntimeHost_OwnsSenderAndDrainsItAfterPreviewGameCleanup(di: True)`
  ожидал callback count1, получил0. Fixture не устанавливал факт старта worker перед
  немедленным Stop. Добавлен first-poll barrier при сохранённой проверке disposed
  game и cleanup counters; production sender/deadlines не менялись ради этого теста.
  Финальный Release: **Aether1581, Editor847, Alloy893, Hosting117**, failed/skipped0.
  Initial failure/TRX сохранён, final callback/DI Debug повторён узко.
- Exact commands: `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --no-restore`,
  аналогично Editor/Alloy/Hosting, затем те же проекты с `-c Release`. Фактически с
  TRX/result directory, `-v minimal -p:UseSharedCompilation=false -nodeReuse:false`.
  [Full runner](../../TestResults/RuntimeObservation-ED8a2-20261031-1/RunFull.ps1),
  [final Release runner](../../TestResults/RuntimeObservation-ED8a2-20261031-1/RunReleaseFinal.ps1),
  [audit/TRX/commands](../../TestResults/RuntimeObservation-ED8a2-20261031-1/verification.json).
- `EmptyRing_TransmitsSanitizedHealthThroughRealPipeAndRetainsLastObservationAfterStop`,
  `InvalidObservation_RejectsWholePacketAndPreservesPreviousSnapshotAndRecordCursor`,
  `ObservationBudgetOmission_CountsPacketWithoutClearingCachedEvidenceOrBlockingRecords`
  проверяют actual receiver state и import cursor. `Report_ExportsNumericChildHealthAndDeviceAfterCrashWithoutSinkErrorText`,
  `SmallBudget_OmitsChildObservationExplicitlyAndKeepsValidPartialJson`,
  `HealthInspection_FreezesObservedChildStateUntilExplicitRefreshAndMarksMissingDataUnavailable`
  и `PresentationDevice_IsReadFromCachedOptionalSurfaceCapabilityAndCopiedAsValues`
  проверяют JSON privacy/budget и retained UI semantics.

Fresh package acceptance:15новых nupkg с unique version, isolated feed/cache/source
mapping и independent solution вне checkout;8cases в Debug/published Release,
DLL/apphost и Cobalt/MS DI.24CPU Game children reaped/released, health/jsonl sink
получен в parent, GPU device явно unavailable; schema2 Game report проверен.
Первый package run дошёл до consumer execution и остановился на новой fixture
проверке GetInt32 для null PID отвергнутого до spawn запуска. Исправлена проверка
nullable PID; старый deployment не менялся, fresh run2 прошёл8/8.
[Fail1](../../TestResults/RuntimeObservation-Packages-20261031-1/verification.json),
[passing2](../../TestResults/RuntimeObservation-Packages-20261031-2/verification.json).

Published GUI acceptance: SourceRelease и PackageRelease, по одному standard/Cobalt
workflow, самостоятельные UI/Runtime executables вне checkout. Native Skia/fonts/
shaders/assemblies delivery, Transform/Undo/Redo/save/close/recovery и dirty document
сохранены. Перед directed owned crash fixture ожидает получение device observation;
обычная авария без него по контракту оставляет unavailable.14children освобождены,
2killed исключены из12graceful reports; Vulkan core/sync validation0errors/0warnings,
surfaces/slots/devices/handles released, UI session/GPU/handles0 после App dispose.
Report содержит actual OpenGL UI device и last child Vulkan device/health после
crash; его private-text exclusion и size bound проверены. Mode-cycle fixture
учитывает добавленный ранее Compiler mode.4diagnostic PNG просмотрены:
[source console](../../TestResults/RuntimeObservation-ED8a2-20261031-1/SourceRelease-diagnostics.png),
[source health](../../TestResults/RuntimeObservation-ED8a2-20261031-1/SourceRelease-diagnostics-health.png),
[package console](../../TestResults/RuntimeObservation-ED8a2-20261031-1/PackageRelease-diagnostics.png),
[package health](../../TestResults/RuntimeObservation-ED8a2-20261031-1/PackageRelease-diagnostics-health.png).
[Runner](../../TestResults/RuntimeObservation-ED8a2-20261031-1/RunAcceptance.ps1),
[native audit](../../TestResults/RuntimeObservation-ED8a2-20261031-1/acceptance.json).

ED8a закрыт в этом scoped Windows x64/net10/OpenGL UI↔Vulkan preview + CPU Game
профиле; ED8/framework release в целом не закрыты. Report намеренно partial:
independent snapshots, missing capabilities и persistent compiler history unavailable.
Не повторялись full native/provider/backend matrix, native Vulkan UI metadata,
physical OS input/device loss/K3/K7/K8/VRAM/long-load и full solution/Core/Enerit/
Mithril suites. Следующий основной шаг — ED4c.2 имена сущностей, затем ED4c.3 и
project/workflow; agent/Game AI roadmap остаётся согласованным backlog.

## ED8a.1: архив и управляемый экспорт — 2026-10-30

Продолжена очередь ED8a. Report writer сохраняет прежний schema1 `WriteAsync`
metadata API и добавляет `WriteReportAsync`/schema2 с явными frozen options.
Core archive/configuration views заимствуются через App registration; IO выполняется
в background task. Snapshot объединяет до1024memory и1024archive entries по
ApplicationId/Sequence, memory выигрывает collision. Сохраняются collector/child
origin, correlation allowlist, timestamps и независимые loss/health counters.
Schema2 содержит versions/OS, scalar effective config, observed presentation backend/
viewport, parent sink health и process metadata. Budget omission отличается от
unavailable capability; child sink health/runtime device/persistent compiler history
в этом срезе явно unavailable, правило не заменено фиктивными healthy0.

Default metadata-only: без message/exception/stack/raw/compiler/source path/caller/
arbitrary properties. Explicit text opt-in добавляет bounded producer text, включая
raw stdout/stderr и compiler diagnostics; paths/user data могут сохраниться.
Bootstrap/environment/control fields никогда не копируются как данные. Строки с
известными control-field ключами и configured sensitive literals очищаются до
clipping, включая literal на границе output. Это не обещание автоматической
анонимизации любого пользовательского текста. UI показывает политику, text/history
checkboxes, новый путь и cancel. Path/options frozen до drain; user/App cancellation,
write error/deadline и no-overwrite оставляют destination и history нетронутыми,
собственный temporary file удаляется. Default512KiB/5s/4096text chars; limits options
проверяются до IO. Legacy API не начинает читать историю неявно.

Published proof выявил missing presentation при paused follow-tail: UiSession
вызывает frame participant только при NeedsFrame. Добавлен одноразовый observation
независимо от follow; UI export снимает текущий viewport со своего borrowed session
перед замораживанием DTO. View освобождает ссылку при Dispose, model не хранит UI
объекты. Регрессия проверяет initial paused mode, resize/scale и cancel; full Editor
Debug/Release повторён после исправления. Два первых запуска probe исправляли
невалидный fixture nonce/endpoint; engine guards не ослаблялись.

Проверки Windows x64/SDK10.0.403/net10.0/xUnit2/VSTest:

- Narrow Core56/56 и Editor47/47, failed/skipped0. Новых11Core +9Editor cases.
- Полные **Core398/398, Aether1570/1570, Editor842/842 отдельно Debug/Release**,
  failed/skipped0. После paused-presentation fix Editor842 повторён в обеих конфигурациях.
  [Full runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunFull.ps1),
  [final Core runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunCoreFinal.ps1),
  [final Editor runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunEditorFinal.ps1),
  [audit/TRX](../../TestResults/DiagnosticReport-ED8a-20261030-1/verification.json).
- Точные команды: `dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --no-restore`,
  аналогично `TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj` и
  `TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj`, затем `-c Release`.
  Фактические flags: `--logger trx;LogFileName=... --results-directory ... -v minimal
  -p:UseSharedCompilation=false -nodeReuse:false`; в runners logger argument передан
  одной строкой, без shell interpretation.
- `MetadataReport_MergesArchiveWithMemoryByCollectorIdentityAndOmitsAllProducerText`,
  `TextOptIn_ExportsRawCompilerAndExceptionTextWhileRedactingControlDataAndHostLiterals`,
  `SmallBudget_ProducesValidBoundedPartialJsonAndCountsDroppedRecords` проверяют
  actual JSON content, identities, explicit text/raw/compiler и4096byte budgets.
  `NewReport_NoOverwriteWriteFailureCancellationAndDeadlineLeaveNoTemporaryFiles`,
  `UserCancellation_DrainsBeforeRetryAndNeverChangesHistoryOrFrozenChoices` и
  `View_ExposesExplicitTextHistoryChoicesCancellationAndActualPresentationValues`
  проверяют failures/ownership/UI route; text boundary и optional source failures отдельно.
- Published **Release/Cobalt** consuming executable вне checkout и запуск из `%TEMP%`.
  Managed assemblies загружены из deployment; native Skia/fonts доставлены, fallback
  на source assets нет. Реальные CPU EditPreview children: первый экспорт при live
  normal runtime, второй после owned kill; оба reaped/resources released. UI keyboard
  экспортирует metadata и text profiles, проверяет control-value exclusion,
  actual backend/config/process capture, current + rotated closed JSONL,
  malformed/incomplete counters. Native raster Dark/Light PNG просмотрены.
- Flood5000imports при ring64 проверяет bounded presentation/report; отдельный text
  report с8192byte budget — valid partial JSON с omissions. Это finite headless/raster
  workload, не long-load/slow-UI/window responsiveness qualification.
  [Publish runner](../../TestResults/DiagnosticReport-ED8a-20261030-1/RunPublished.ps1),
  [probe](../../TestResults/DiagnosticReport-ED8a-20261030-1/PublishedProbe/Program.cs),
  [passing proof](../../TestResults/DiagnosticReport-ED8a-20261030-1/published-proof.json),
  [Dark](../../TestResults/DiagnosticReport-ED8a-20261030-1/normal-dark.png),
  [Light](../../TestResults/DiagnosticReport-ED8a-20261030-1/crashed-light.png).

Source/project-reference publish не является fresh NuGet consumer proof. Окно/GPU,
Game.Logic runtime, compiler navigation через внешний editor executable, broader
provider/native/package matrix, full solution и UI/Hosting/Enerit/Mithril suites
этим шагом не проверялись. K3/K7/K8/VRAM/physical/general release остаются открытыми.
ED8a.1 завершён в описанном scope; следующий ED8a.2 — child sink health/device
capability и window/game/package acceptance, затем прежний ED4c.2/3/project/workflow.

## ED6a.3: compiler diagnostics и source navigation — 2026-10-30

Продолжена прежняя очередь после фиксации agent/Game AI roadmap. C# compiler
получает job-owned ErrorLog/SARIF2.1; classification не выводится из raw stdout/
stderr. После reap читается до2MiB,128results, code128/message2048/path4096 chars.
Pending/Available/Unavailable/Failed/TooLarge различимы. Unsupported URI/base mapping
не даёт навигацию; file/line/column локального source имеют1-based координаты.
MSBuild/restore failure до C# compilation может не произвести SARIF.

ProcessDiagnosticSnapshot дополнен optional CompilerDiagnostics без изменения
старой positional signature.16completed jobs+active сохраняют failed diagnostics
после успешного retry; чтение UI snapshots остаётся in-memory. SARIF включён в
owned flat archive и общие count/bytes/age limits. Первый full Debug run обнаружил
пропуск SARIF в allowlist:1566passed/4failed, retention считала0recognized jobs.
Добавление owned filename восстановило retention/delete-failure contracts;
исходный RED сохранён в `engine-before-retention-fix.trx` в evidence ниже.

Editor добавляет Compiler mode (старые enum values сохранены), frozen job/source
selection, explicit refresh и bounded total entries/payload через существующие
MaximumRecords/MaximumRetainedBytes. Нет второй истории Core events. Detail text
нормализован до LF: UI regression поймала Windows AppendLine/CRLF, недопустимый
для RichTextEditor. Filter/theme/polling не переписывают inspected text; eviction
отключает source action. Metadata report экспортирует numeric compiler counters,
явно исключает compilerText/sourcePaths.

ISourceFileNavigator — borrowed App hook; UseAetherEditorSourceNavigation фиксирует
SourceEditorOptions и регистрирует CommandSourceFileNavigator. Аргументы с
{file}/{line}/{column} передаются через ArgumentList без shell. Внешний editor
process не App-owned: handle освобождается, exit не ожидается. Model сериализует
один accepted request, Stop отменяет/drain его до DI teardown; failure наблюдаем
через NavigationStatus. Кнопка доступна с клавиатуры по Tab/Enter. Custom hook
можно зарегистрировать через DI. Старые3/5-argument constructors сохранены,
добавлен6-argument вариант с optional hook; Cobalt/MS DI путь проверен отдельно.

Проверки текущего среза, Windows x64/SDK10.0.403/net10.0/xUnit2:

- Narrow: Aether19/19 (SARIF parser, actual failed→successful build, retention
  integration), Editor34/34 (compiler/context/View/configuration).
- Полный Aether **1570/1570 Debug и1570/1570 Release**, failed/skipped0.
- Полный Editor **833/833 Debug и833/833 Release**, failed/skipped0. После усиления
  assertions compiler metadata privacy/counts и both-DI hook оба Editor run повторены.
- Команды: `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug`
  и `dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug`,
  затем те же проекты с `-c Release`; фактически с `--no-restore`, TRX/result directory,
  `-p:UseSharedCompilation=false -nodeReuse:false`. [Exact runner](../../TestResults/CompilerNavigation-20261030-1/RunSuites.ps1),
  [четыре full TRX и логи](../../TestResults/CompilerNavigation-20261030-1/).
- Source Debug/Cobalt/native Skia raster consumer выполняет реальную compilation
  failure CS1029 → frozen coordinates/hook через focused button → Dark/Light →
  successful retry при сохранённой old-job selection → View/App cleanup.
  [Proof](../../TestResults/CompilerNavigation-20261030-1/raster/proof.json),
  [source probe](../../TestResults/CompilerNavigation-20261030-1/RasterProbe/Program.cs).
  Все3PNG просмотрены: [Dark](../../TestResults/CompilerNavigation-20261030-1/raster/compiler-dark.png),
  [Light](../../TestResults/CompilerNavigation-20261030-1/raster/compiler-light.png),
  [после retry](../../TestResults/CompilerNavigation-20261030-1/raster/compiler-after-retry.png).

Raster proof использует записывающий host hook, без запуска пользовательского
editor executable и без окна/GPU. Shell-free argv, missing source/cancel, DI,
source coordinates и failure handling проверены direct tests. Full solution,
Core/Enerit/Mithril/UI/Hosting suites, published/package/provider native matrix,
physical/K3/K7/K8/VRAM не повторялись. Нет нового general release proof.
Далее ED8a persistent history/raw policy/child sink health/backend/configuration/
user cancellation и scoped window/published/flood acceptance; полный ED6a открыт.

## ED6a.2: raw streams и health — 2026-10-29

Core добавил borrowed nondisposable IDiagnosticHealthReader с cached parent
collector/ring/sink/emergency/retention counters и ограниченное UTF16-safe чтение
output window. [Core semantics/proof](../core/HISTORY.md#borrowed-health-и-output-windows--2026-10-29).
UseAether регистрирует nondisposable IProcessDiagnosticsSource над injected runtime
factory и compiler. Native factory optional capability IRuntimeDiagnostics читает
thread-safe bounded history64leases; supervisor сохраняет actual ownership.
Compiler хранит16completed snapshots+active job, включая failed job после successful
retry, с PID/job/revision/build correlation. История не удерживает owned leases
compiler jobs и не читает files; disk retention остаётся независимой.
Snapshot API не включает nonce/endpoint/environment. Runtime factory без capability
даёт RuntimeAvailable=false; отсутствие capture — null, а не zero-byte healthy.

Editor modes Structured/RAW stdout/RAW stderr/Health используют одну retained
Tree/Scroll/readonly Details. Raw rows — presentation snapshots, не synthetic
DiagnosticRecord и не повторный import в Core. Нельзя выводить severity/compiler
classification из stderr. Raw filters: source/exact session/generation/role/search;
level/event/grouping/clear только structured. Health не фильтруется как log.
Default16processes/4096chars per stream; options разрешают до64/16384. Ingestion
сохраняет bounded history при закрытой панели, если host продолжает Present.
Inspected raw/health text frozen до явного refresh или eviction; polling/filter/
theme не сбрасывают его selection. Равные rows не публикуются повторно. Data/VM
не зависит от renderer/native handles; reader/source borrowed и внедрены через App.
Core counters относятся к parent; child sink health не переносится текущим protocol.
UI gaps, parent sink drops, receiver lost/unconfirmed, capture eviction/file loss и
display OmittedCharacters показаны отдельно, без заявления durable delivery.

Metadata report теперь включает numeric parent/process/capture/delivery health;
raw/message/exception/stack/caller/error text и arbitrary properties исключены.
Health/process metadata имеет часть общего byte budget, явные omitted flags/count;
512KiB/cooperative5s/no-overwrite/atomic publication и App stop/drain прежние.
Report81529bytes содержит162structured metadata records,7process metadata и1parent
sink в final native case. Existing report constructor/VM constructor/Read() APIs
сохранены; новых generic factory/scopes/async activation не вводилось.

Examples/AetherRuntimeHost получил opt-in `--console-diagnostics`: executable
явно включает существующий Core ConsoleOutput sink для stdout. Default CLI/startup
и bootstrap validation прежние; Editor/UI dependency в runtime не добавлена.
Gui acceptance передаёт флаг явно, поэтому raw panel проверяет настоящий output.
Stderr в данном native case пуст, но capture/finished status доступен; nonempty
stdout/stderr и graceful/natural/kill drain проверены real stdio subprocess tests.

Текущие full module tests, failed/skipped0 в final runs:

| Проект | Debug | Release |
| --- | --- | --- |
| TrueMoon.Tests |387/387|387/387|
| TrueMoon.Aether.Tests |1556/1556|1556/1556|
| TrueMoon.Aether.Editor.Tests |823/823|823/823|
| TrueMoon.Alloy.Hosting.Tests |117/117|117/117|

Commands: `dotnet test <project>/<project>.csproj --no-restore -c Debug` и `-c Release`,
с unique binlogs/TRX.37новых cases (Core13/Aether6/Editor15/Hosting3); existing
failed/success compiler, native raw drain и both-provider ownership tests усилены.
[Audit/8TRX/current sources](../../TestResults/ED6a2-20261029-1/audit.json).
Первый concurrent full Aether run имел1555pass/1EOF FirstFrame timeout3s;
focused1/1 прошёл42ms. Новый concurrent-reader test переведён на собственный worker,
не удерживает shared pool; final Debug/Release1556/1556 прошли. Причина единичного
старого EOF timeout не объявляется доказанной; production frame code/deadlines не менялись.

Native mode cycle выявил отдельный Hosting scheduling defect: Update после Tree
selection reveal оставляет NeedsRender. Premature PresentOpenGL вызывает strict
guard и terminal fault, после чего acceptance cleanup зависал. Сохранены stacks/
primary UI failure; [RED3→GREEN3/fix](../alloy/HISTORY.md#inf3-dirty-frame-после-update--2026-10-29).
Старый guard сохранён; pump defers dirty output на следующий bounded tick.
Native runs1/2/3 failures сохранены, failed owned tree3 завершён после фиксации
fault; run4 был заблокирован его loaded DLL. Run5 прошёл после fix; run6 подтвердил
final glyph correction. General terminal rendering-fault cleanup этим не закрыт.

Final native **SourceDebug/standard window/Cobalt1/1**,7children (1owned crash),
6graceful core/sync Vulkan reports0errors/0warnings; UI/GPU/handles0 и owned processes0
после Dispose.17imported child records и crashed PID сохранены, stdout534chars
прежней generation доступен после reap, raw stderr не получает warning severity.
Raw/health/structured mode cycle, public filters, reopen, frozen inspect и unchanged
dirty document/selection проверены directed input на owner13.4diagnostics PNG
осмотрены; standard preview5PNG сохраняют actual red-cube pixel assertions.
[Native manifest](../../TestResults/ED6a2-Native-20261029-6/verification.json),
[stdout](../../TestResults/ED6a2-20261029-1/diagnostics-stdout.png),
[health](../../TestResults/ED6a2-20261029-1/diagnostics-health.png).

ED6a.2 bounded scope выполнен. Далее compiler file/line/column diagnostics и
navigation hook, затем full ED8a: persistent/rotated history, raw-text/privacy policy,
child sink health/backend/device/configuration и explicit user cancellation.
Published/package/provider matrix нового consumer, broader flood/slow UI, UI893/
full solution, physical/K7/VRAM/BSOD/release gates не запускались и не закрыты.

## ED6a: structured console и первый partial export — 2026-10-29

`TrueMoon.Aether.Editor.Diagnostics` добавляет App-owned ViewModel над borrowed
`IDiagnosticRecordReader`, options, parent-owned typed View/style/IFactory и stateless
report writer. `UseAetherEditor` включает consumer, header открывает его через
«Вид → Диагностика», standalone toolbar имеет такую же команду. Lower-level
`UseAetherEditorDiagnostics(options)` можно подключить заранее; duplicate attachment
отклоняется, отсутствие bounded collector показано явно. Новый logger не создаётся.

Reader получает одну порцию64records/100ms; retained default512records/1MiB payload
estimate/8192chars per record. Counts/bytes/characters/update batch конфигурируются
и валидируются отдельно от Core budgets. Показаны timestamp/source/level/event,
message/exception/stack/properties, observed cursor gaps и presentation eviction/
truncation; unknown sink health не интерпретируется как0. Поиск, source/event,
minimum level и exact session/generation/role filters не меняют domain state.
Grouping учитывает process/context/build/exception, count относится только к
retained window и виден в начале строки. Clear меняет только presentation/cursor;
follow-tail pause не останавливает ingestion. Tree/scroll/detail controls retained;
filter скрывает выбранную строку без потери detail selection, пока событие retained.
Ctrl+C в списке и readonly detail дают явное копирование текста пользователем.
Данные нормализуются в LF/valid UTF-16; View Dispose снимает подписки, model/reader
заимствуются. Session.Present выполняет bounded polling на UI owner.

«Экспорт без текста» принимает явный путь нового JSON, writes recent metadata
в sequence order до512KiB/cooperative5s и атомарно публикует без overwrite.
Versions/OS/presentation limits и allowlisted correlation сохраняются; message,
exception/stack/caller/arbitrary properties исключены, source/event identifiers
сохраняются. Это metadata-only policy; `partial=true`, omissions и unavailable
raw/history/sink health/backend/configuration/compiler fields указаны явно.
Snapshot не зависит от display filter; task отменяется/drain при App stop/dispose.
Ошибки пути/записи/cancel показываются без исключения через UI command.

Проверки текущего кода:

- `dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --no-restore`
  и тот же command с `-c Release`, с TRX/unique binlogs: **808/808** в каждой,
  failed/skipped0.45новых tests проверяют exact byte/count boundary, truncation/
  gaps/clear/frozen snapshots, все filters, grouping identity, retained focus/detail/
  paused scroll/theme, copy/reopen/factory failure, Cobalt/MS DI borrowing, report
  privacy/size/order/no-overwrite/write failure/cancellation и Unicode/LF.
  Прежние public constructors EditorView/Header/Layout сохранены отдельными
  overloads; точные binary call signatures защищены regression test.
  [Debug](../../TestResults/ED6a-20261029-1/Editor-Debug.trx),
  [Release](../../TestResults/ED6a-20261029-1/Editor-Release.trx),
  [audit/current hashes](../../TestResults/ED6a-20261029-1/audit.json).
- `VerifyGui.ps1 -RunProfile SourceDebug -HostKind Standard -Scenario Workflow`
  поверх предыдущего package baseline, с process-local Vulkan validation: **1/1**,
 7owned children reaped/released, один explicit crash,6graceful core/sync reports
 0errors/0warnings; UI sessions/GPU resources/handles0 после Dispose. В console
  сохранены17imported child records, в том числе crashed PID; native input проверяет
 2groups(20+1), generation filter/retained inspect, новый report73164bytes и reopen
  при неизменном dirty document/selection. Notifications на UI owner13.
  [Native manifest](../../TestResults/ED6a-Native-20261029-7/verification.json),
  [actual screenshot](../../TestResults/ED6a-20261029-1/diagnostics.png).
  Extended Skia154.0 и NotoSans3748glyphs загружены; screenshot осмотрен. Visual QA
  исправила console-local input padding и читаемый repeat count.
- Промежуточный native run1 прерван tool session; run2 timeout маскировался finally
  с cancelled token. Phase markers показали в run3 ошибку harness: пустое Text event
  не очищает поле; теперь Ctrl+A/Delete. Run4 нарушал контракт OpenGL readback при
  pending update; screenshot перенесён в FramePresented. Run5 прошёл функционально,
  run6 повторил весь case после visual corrections; run7 подтвердил final DLL
  после сохранения прежних constructor overloads. Это test harness fixes, не
  изменение production input/render contracts. Earlier failed evidence сохранён.

Полные ED6a/ED8a остаются открыты: raw streams, borrowed sink/delivery/capture health,
compiler navigation, richer export/history/backend/configuration/user cancel,
flood/slow UI и published/package/provider matrix нового consumer. В этом шаге
не повторялись Core/Aether/Hosting/UI suites или full solution; прежние результаты
не объявляются новыми. SourceDebug native case не доказывает package/published
готовность новой console. K7/VRAM/BSOD/physical/release gates прежние.

## ARCH2e.4: file-worker liveness contract — 2026-10-29

Core устранил доказанную shared-pool dependency file-drain после EOF, включая
async channel-to-Task bridge. [Архитектура/RED/GREEN/стоимость](../core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).
Isolated Core-only child:24bytes записаны, reader завершён, единственный shared
worker удерживается, FileCompletion/Completion и файл закрываются в прежние2s.
Добавлена автоматическая Core regression; pool settings test host не меняются.
Full Core374/374 и Aether1550/1550 Debug/Release прошли.4TRX, native24/24,
56children/8owned crash cases/48graceful validation0/0 и full teardown0 проверены
[аудитом](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json).
Fresh15packages/headless8/8/625source inputs:
[verification](../../TestResults/ARCH2e4-FileDrain-Packages-20261029-2/verification.json).
Старые resource ownership/runtime startup fixes сохраняются; новое изменение
не меняет Aether wire/App/DI contracts. Shared utilities остаются без UI dependency.

Решение/границы acceptance явны в [ARCH2e.4](PLAN.md#arch2e4-устойчивость-output-drain--follow-up-2026-10-29):
закрыт proven scoped contract; точный cause прежнего intermittent observation не
приписывается без failure heap. Подготовлены sync atomic marker и targeted heap
collection в consumer;30cached replays/90children и fresh pre-fix run не дали
heap signal. Это не дополнительный historical-root-cause passing proof.
No worker mode, UTF8/tail/file budgets и IO loss/error visibility прежние;
новая цена — один dedicated file worker на active capture. Generic filesystem
stalls/global real-time guarantee не добавлены. Общие hardware/release gates прежние.
Далее ED6a/ED8a bounded console/export; launch roles/Player/HUD остаются в плане.

## ARCH2e.4: stdio ownership и диагностика drain — 2026-10-29

Исследован исходный timeout из ARCH2e3-Packages-20261029-2. Timeline показывает
StopRequested → через3s Killing/GracefulStopIncomplete → ProcessReleased/Killed
(exit-1), затем повторные2s output-drain timeouts. Child успел записать report с
Error=null и released diagnostics/resources. Это был forced reap, а не доказанное
штатное завершение. Шесть повторов неизменного headless consumer на предыдущем
feed прошли,18children; [attempts](../../TestResults/ARCH2e4-20261029-1/repeat-summary.json).
Первый reproduction fixture ошибочно использовал checkout для candidate и упал
при Build; затем исправлен на независимый TEMP. Он не считается reproduction
исходной ошибки. На этом этапе причина отсутствия completion ещё не была установлена; дальнейшее наблюдение ниже локализует file sink.

На свежем feed [ARCH2e4-Packages-20261029-1](../../TestResults/ARCH2e4-Packages-20261029-1/verification.json)
ошибка воспроизвелась с новой диагностикой: stdout и stderr ReadCompletion уже
RanToCompletion, FileCompletion у обоих WaitingForActivation, counters24written/
24characters/0dropped. [Полная ошибка](../../TestResults/ARCH2e4-Packages-20261029-1/build-Debug-dll-cobalt.stderr.log).
Значит незавершён именно file sink, а не pipe EOF. Также timeout Shutdown pipeline
есть в emergency output. Counters отражают принятые buffered writes, не fsync.
После этого8replays тех же новых пакетов прошли, как и cold feeds2/3/4.
Temporary Microsoft dotnet-stack10.0.750501 установлен только в ignored evidence.
[Stack медленного, но успешного cold4 consumer](../../TestResults/ARCH2e4-20261029-1/cold4-stack-37652.txt)
не застал зависание sink и не устанавливает его причину; OS/runtime/AV fault не
объявляется. Для следующего наблюдения добавлены FileStage и thread-pool counters,
без смены file IO реализации или увеличения deadlines.

Гипотеза о том, что Mithril Process.Dispose закрывает уже полученные readers,
не подтвердилась: [Process.Close в .NET10](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs#L809-L869)
оставляет accessed standard streams вызывающему коду. Найден отдельный production
ownership defect: NativeRuntimeLease и GameBuildCompiler не закрывали эти streams.
Реальная Windows regression получила OS SafeFileHandles stdin/stdout/stderr,
проверила graceful/natural/forced exit и повтор Stop: до fix stdout оставался
открыт во всех3cases. [RED3](../../TestResults/ARCH2e4-20261029-1/stdio-red-valid.trx).
Первый RED fixture имел неверный endpoint prefix; его падение отдельно сохранено
и не считается ownership proof.

Added internal Hosting.OwnedProcessOutput удерживает accessed stdout/stderr и
optional stdin, начинает bounded capture сразу, а после reap дожидается reader/file
completion и закрывает streams. Общий owner используется runtime и compiler;
compiler не получает фиктивный stdin. Timeout сохраняет drain task/streams и
возможность retry, successful releases не повторяются. Read/close failures не
подавляются; остальные streams всё равно получают release attempt. Core capture
по-прежнему borrowing: добавлены ReadCompletion/FileCompletion и FileStage для различения
ожидания EOF и file sink. Ошибка timeout содержит статусы и счётчики обоих
захватов без raw output. Production budgets остаются2s runtime/5s compiler.
Новых DI contracts/scopes и изменений Mithril runtime/API нет.

| Поведение | Точная проверка |
| --- | --- |
| Real child reap, persisted output и закрытие3OS handles при graceful/natural/kill | NativeRuntimeOutputTests.Stop_ReapsChildDrainsOutputAndClosesAllAccessedStandardStreams,3cases |
| Timeout сохраняет владельца, retry/concurrent waits завершают drain до однократного close; optional stdin | OwnedProcessOutputTests.Drain_TimeoutRetainsStreamsAndRetryPersistsOutputBeforeClosingOnce,2cases |
| Read failure сохраняется после file drain и release всех streams | OwnedProcessOutputTests.Drain_ReadFailurePreservesOriginalErrorAndClosesEveryStreamAfterFileDrain |
| Close failure не мешает другим releases; retry закрывает только failed owner | OwnedProcessOutputTests.Drain_CloseFailureAttemptsOtherStreamsAndRetriesOnlyUnreleasedOwner |

[GREEN7](../../TestResults/ARCH2e4-20261029-1/output-green-final.trx).
Initial full Debug1550 дал1549passing/1failed: existing
ChildDiagnosticForwarderTests.OwnRequestDeadlineAndPendingGuard_BoundAnInjectedClientWithLargeCapacityAndTimeout
не достиг remote barrier, потому что500ms request deadline включал cold connection.
Fixture теперь устанавливает physical connection до публикации record. Повтор
под compiler/process load всё ещё мог истратить500ms до handler barrier;
live-pipe forwarding class выделен в nonparallel xUnit collection, как существующий
exclusive bootstrap fixture. RequestTimeout/pending guard/assertions и production IPC прежние.
[Исходный full failure](../../TestResults/ARCH2e4-20261029-1/TrueMoon.Aether.Tests-Debug.trx)
сохранён отдельно от итоговых full Aether1550/1550 Debug/Release и Core373/373
Debug/Release; failed/skipped0. Точные команды (в каждой конфигурации отдельно):

```powershell
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --logger 'trx;LogFileName=Aether-complete-Debug.trx' --results-directory ./TestResults/ARCH2e4-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-20261029-1/Aether-complete-Debug-{}.binlog'
dotnet test ./TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --logger 'trx;LogFileName=Aether-complete-Release.trx' --results-directory ./TestResults/ARCH2e4-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-20261029-1/Aether-complete-Release-{}.binlog'
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Debug --logger 'trx;LogFileName=Core-complete-Debug.trx' --results-directory ./TestResults/ARCH2e4-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-20261029-1/Core-complete-Debug-{}.binlog'
dotnet test ./TrueMoon.Tests/TrueMoon.Tests.csproj -c Release --logger 'trx;LogFileName=Core-final-Release.trx' --results-directory ./TestResults/ARCH2e4-20261029-1 --verbosity quiet '-bl:./TestResults/ARCH2e4-20261029-1/Core-final-Release-{}.binlog'
```

Финальные fresh packages:15nupkg version0.2.2.7-arch2e.20261029151729,
625source fingerprints,8/8headless cases/24children/30commands,22resource checks.
[Verification](../../TestResults/ARCH2e4-Packages-20261029-4/verification.json).
Native повтор после resource fix и финальной диагностики:24/24cases/32commands,
source Debug/published Release и Package Debug/published Release, оба hosts.
56children/8owned crash tests;48graceful core+sync validation reports0errors/
0warnings, после App dispose UI/GPU/handles0.40PNG/160loaded-package DLL checks/
1261hashes/4current Core+Aether TRX и отсутствие owned процессов проверены
[аудитом](../../TestResults/ARCH2e4-Native-20261029-1/audit.json).
[Native verification](../../TestResults/ARCH2e4-Native-20261029-1/verification.json),
[package embedded recovered capture](../../TestResults/ARCH2e4-Native-20261029-1/captures/PackageRelease-Embedded-Workflow/recovered.png)
просмотрен визуально. Framework source changes against fresh feed0. Эти passing
повторы не отменяют file-sink failure в run1 и не закрывают его root cause.
28documents/722links/405anchors проверены без ошибок; source/package checks640
в [docs audit](../../TestResults/ARCH2e4-Native-20261029-1/docs-check.json).

Intermittent file-sink timeout не объявляется исправленным: EOF уже завершился,
resource leak доказан и закрыт отдельно. Причина незавершённого file sink не установлена. ARCH2e.4 остаётся
частично открытым; новое наблюдение должно сохранить read/file task statuses,
output counters, stop reason и runtime metadata. Старые hardware/BSOD/K7 gates
не меняются; UI/Hosting/Editor suites и полный solution run здесь не повторяются.

## ARCH2e.3: native Editor consumers и startup cancellation — 2026-10-29

По запросу перейти к следующему этапу выполнена bounded GUI/native acceptance.
Окружение подготовлено process-local: Windows26300 x64, SDK10.0.403, NVIDIA
RTX5070Ti32.0.16.2101 (также установлен AMD32.0.21042.62), локальный validation
layer1.4.363 из предыдущей pinned SDK поставки. Driver/OS settings не менялись,
K7/240-window stress и поиск workaround не запускались. Предыдущая пауза после
BSOD снята только для этой последовательной consumer matrix; причина BSOD не
установлена и исправление driver fault не объявляется.

Добавлен [Gui consumer/runner](../../ManualTests/AetherPackageConsumer/README.md#native-editor-acceptance-arch2e3):
source ProjectReference composition и отдельный pure-PackageReference template.
Custom host использует публичные View factory/App/viewport contracts, без
internal Editor binding или source linking библиотек. Native GL queries остаются
тонким host glue. Он сам задаёт
first-presentation readiness, GL context/adapter и factory transport, сохраняет
GL owner до drain, detach/dispose/recreate View и заимствует App models.
Системный input не отправляется: directed gestures поступают в своё UI дерево.

Native cancellation выявила production defect в Alloy.Hosting RegisterWindow:
callback закрытия выполнял `resolver.Resolve<IAppLifetime>()` во время Dispose
частично запущенного window. Resolver уже закрывал admission и выбрасывал
ObjectDisposedException. Borrowed lifetime теперь захватывается в registration
factory и callback вызывает `lifetime.Cancel`; ownership/API прежние.

| Проверка | Фактический результат/evidence |
| --- | --- |
| Native cancellation после первого frame до готовности Editor App | RED `SourceDebug-Standard-Cancel`, [ошибка](../../TestResults/ARCH2e3-20261029-5/SourceDebug-Standard-Cancel.stderr.log); после fix — GREEN, path в [указателе](../../TestResults/ARCH2e3-20261029-1/cancel-green-path.txt). Затем Standard/Embedded Cancel прошли во всех4profiles |
| Hosting/Editor regressions после fix | Hosting114/114 и Editor763/763 отдельно Debug/Release,4TRX/1754passed/failed0/skipped0; exact файлы/хеши — в [audit](../../TestResults/ARCH2e3-20261029-9/audit.json). UI/Aether/full solution suites в этом шаге не повторялись |
| Fresh package/headless regression | 15packages `0.2.2.7-arch2e.20261029143357`, isolated feed/cache,8/8cases/24children,624source inputs; [verification](../../TestResults/ARCH2e3-Packages-20261029-3/verification.json). Прежний package run сохранён со своей версией |
| GUI/native acceptance | **24/24**: source Debug, source published Release, NuGet Debug, NuGet published Release × Standard/Cobalt и Embedded/MS DI × Workflow/Failure/Cancel.32commands/8MSBuild binlogs; [verification](../../TestResults/ARCH2e3-20261029-9/verification.json) |

Финальная перепаковка включает актуальные packaged README. Первый native run
`ARCH2e3-20261029-8` уже прошёл24cases на предыдущем feed; финальный run9 повторяет
тот же код на новой версии. Дополнительный headless run2 завершился с единичным
[output-drain timeout](../../TestResults/ARCH2e3-Packages-20261029-2/build-Debug-dll-cobalt.stderr.log)
в NativeRuntimeLease.StopAsync после reap; runtime report содержит Error=null и
resources/handles0. [Адресный повтор](../../TestResults/ARCH2e3-Packages-20261029-2/retry.stdout.log)
на тех же пакетах завершился exit0; затем новый run3 дал8/8passing. Причина не
установлена. Открыт [ARCH2e.4](PLAN.md#arch2e4-устойчивость-output-drain--follow-up-2026-10-29),
успешные повторы этот эпизод не отменяют; timeout/production code не менялись.

В8workflow проверены Open → invalid draft/Theme → Transform через UI input →
actual GPU preview pixels → Undo/Redo → SaveAs/close/reopen. Retained inspector,
scene ID, source file и UI owner thread сохранены. Cancel dirty close и injected
save error сохраняют document/history; затем SaveAs восстанавливает работу.
После принудительного завершения только своего preview child окно остаётся
живым, dirty document/Undo/selection неизменны, explicit preview retry снова
представляет ту же revision, затем Save и clean close завершаются успешно.

Всего56owned preview children:7на workflow, MaxLive1, все reaped/released.
48graceful reports: core/sync validation enabled, Errors0/Warnings0 после teardown,
surfaces/slots/devices created=released, live slots/open handles/resources0.
8deliberately killed children не могут дать final validation report и не
добавляются к этим48. Во всех24cases App Dispose завершён/наблюдён до passing
report; UI sessions/GPU resources/handles0. Original injected startup IOException
остаётся observable из startup/Stop/Dispose; harness принимает только её exact
leaf marker, чужие cleanup errors не подавляются.

Actual loaded extended Skia hash `AAC2943459738978422DAC15F5A44A5813887448D4AFFB373110099FBC91DB1F`,
native154.0; Noto Sans face3748glyphs загружен из host font, без source lookup.
160loaded framework DLL checks совпали с fresh nupkg в package profiles,1259input/
package/layer hashes сверены.40PNG/24case JSON скопированы в evidence; pixel
`#ffcc3326` проверен для original/edited/undo/redo/recovered позиций. Representative
[standard edited](../../TestResults/ARCH2e3-20261029-9/captures/SourceDebug-Standard-Workflow/edited.png)
и [package embedded recovered](../../TestResults/ARCH2e3-20261029-9/captures/PackageRelease-Embedded-Workflow/recovered.png)
просмотрены визуально. Активных owned процессов после matrix нет.

Attempts1–7 сохранены: исправлены fixture public API/usings, PowerShell XML
variable collision, passing report до implicit Dispose и отсутствие собственной
first-frame readiness у custom host. Ни один неполный/failed attempt не считается
полной matrix. ARCH2e.3 проверен в описанном Windows x64/OpenGL↔Vulkan/net10 profile;
physical OS input/DPI/monitors, K3/K7/K8/VRAM/trends/general release отдельно.
Следующий этап — ARCH2e.4 output-drain follow-up, затем ED6a/ED8a bounded console/export и ED4c.2/3/workflow.

## ARCH2e: independent packages и CPU runtime startup — 2026-10-29

Добавлен [воспроизводимый NuGet consumer](../../ManualTests/AetherPackageConsumer/README.md):
fresh local feed/version/cache, отдельное решение вне checkout, PackageReference
без source/project links. Consumer owns Main/App/CLI/game/font/OFL; runtime child
подключает bootstrap/Aether без Editor/UI. Матрица build Debug/publish Release,
runtime DLL/apphost и Cobalt/Microsoft DI в parent выполняется отдельно от GUI.
Подробные package результаты — в [verification.json](../../TestResults/ARCH2e-20261029-7/verification.json).
Успешный run:15fresh packages `0.2.2.7-arch2e.20261029130748`, отдельный private
feed/cache и net10.0 solution под TEMP; source inputs618 и все package hashes
не изменились. SDK10.0.403, Windows x64, framework-dependent deployment.
30owned SDK/consumer commands, каждый MSBuild invocation с unique binlog.

Матрица **8/8**: Debug build/Release publish × Runtime DLL/apphost × parent
Cobalt/MS DI;24successful children, MaxLive1, mappings/GPU resources/handles0,
compiler и child processes reaped/released. Проверены Editor View factory/sole
parent ownership/borrowed models, common scene revision17 roundtrip/create-only
conflict, два game artifacts, CPU pixels обоих builds, tick3/interaction checkpoint,
Replace и повторный Start с восстановлением state. Game DLL отсутствует в parent
CLR; runtime не имеет Editor/UI package dependencies и не загружает их.
Exact selected game path/build sequence A→B→B проверена для каждого case.
Доставлены144пользовательских child records суммарно, context/redaction и raw
stdout/stderr markers проверены.8wrong-fingerprint launches отвергнуты до spawn;
2no-bootstrap CLI controls дали expected exit1. Negative exits не являются failures.

Проверены15package metadata/license/icon/readme/dependency sets, только package
assets graph и fresh-feed provenance; Cobalt/Enerit actual consumer generation,
4analyzer/buildTransitive payload hashes, loaded framework DLL hashes из nupkg.
22file delivery checks: managed assemblies, custom Skia native, stock Skia/
HarfBuzz/GLFW, fonts/OFL в двух profiles. Два embedded shaders совпали с source
в каждом case; native/font loading и actual GPU render этим не подтверждены.
Consumer build и Editor publish имеют прежний generated CS8669 warning; warnings
не исправлялись и не объявляются отсутствующими. Публикации на NuGet не было.

Найден production defect `AetherRuntimeHost.RunAsync`: Windows `Console.In`
может синхронно блокировать даже ReadLineAsync; вызов stdin reader внутри массива
workers происходил до запуска FrameProducer и установки fault continuations.
Поэтому Hello/scene/Step проходили, а CPU frame не появлялся до stdin Stop.
Reader перенесён в отдельную LongRunning owned task, включённую в общий drain;
frame/tick/GPU workers и наблюдение failures запускаются до ожидания stdin.
Новых public contracts, lifetime владельцев или UI dependencies нет.

Существующий process proof RuntimeLaunchProbe теперь конфигурирует shared CPU
surface, получает кадр после одного Step и проверяет selected-game pattern/tick
до отправки Stop. NativeRuntimeProcessTests проверяет эти поля вместе с прежними
identity/role/isolation/reap assertions; новая test project не создавалась.

| Требование регрессии | Фактическая проверка |
| --- | --- |
| CPU кадр выбранной игры до stdin Stop, DLL и apphost; tick1 и pattern | `IndependentRuntime_PreservesExactIdentityRoleIsolationAndOwnedCleanup`: RED0/2 → GREEN2/2 Debug, [RED TRX](../../TestResults/ARCH2e-RuntimeRegression-20261029/runtime-red.trx), [GREEN TRX](../../TestResults/ARCH2e-RuntimeRegression-20261029/runtime-green-Debug.trx) |
| Сохранить session/bootstrap/frames/build/checkpoint/Replace/diagnostic/lifecycle regressions | Полный Aether1543/1543 Debug и1543/1543 Release, failed/skipped0: [Debug](../../TestResults/ARCH2e-RuntimeRegression-20261029/Aether-Debug.trx), [Release](../../TestResults/ARCH2e-RuntimeRegression-20261029/Aether-Release.trx) |

Каждый dotnet test сохранил отдельный binlog. Начальная попытка regression build
имела ошибку namespace в assertion; после исправления RED получен именно на
отсутствующем кадре, до production fix. Полный solution/Editor/UI test run не
выполнялся, GPU/window/native loading не проверялись.

Первые package attempts сохранены в TestResults/ARCH2e-20261029-1…6: исправлены
fixture generated-output path (Debug sources попадали в Release glob), inherited
repo NuGet properties у candidate/report внутри checkout, тип PowerShell XML
variable, diagnostic source вне штатного TrueMoon.* collector filter и сравнение
смешанных Windows path separators в fixture. Код
framework изменён только для подтверждённого runtime worker startup defect.

## Launch roles и Runtime HUD: решение — 2026-10-29

По обсуждению с пользователем в PLAN добавлены
[ENG7b Editor/Player](PLAN.md#eng7b-режимы-запуска-editor-и-player--2026-10-29) и
[ENG5c.4 runtime HUD](PLAN.md#eng5c4-runtime-hud-поверх-alloyargentis--2026-10-29).
Роль выбирается до App.Build, Editor запускает owned child; game + Alloy HUD
компонуются в Runtime. Hot role switching не входит в первый профиль.
[ED6b](EDITOR_PLAN.md#ed6b-buildplaystop-из-редактора--2026-10-29) хранит прикладной
document/build/play workflow, общие UI capabilities — в UI плане.
Согласованы границы, зависимости и проверяемые подэтапы; implementation/API ещё
не выполнены. Ближайший ARCH2e и существующая функциональная очередь сохранены.
Работа документационная: code/build/tests/process/GPU/package runs не выполнялись.
Предыдущий Skia4.154 proof сохраняет дату28октября и свой scope.

## Сверка рабочего дерева и архив STATUS — 2026-10-28

Сопоставлены рабочие source/test/example изменения с ARCH2d, ENG2/ENG3 и
ENG8a.3/ENE6d.4, связанным Mithril fixture quality fix. ARCH2a–c входят в HEAD
af03ca3; independent package proof не найден. Последняя очередь уточнена до
ARCH2e.1/2 package headless, затем native proof после подготовки окружения;
полный ARCH2 предшествует новым ED features. Декомпозиция e.1–3 добавлена в PLAN
без сокращения acceptance. Код/контракты не менялись, новых build/tests/process/
GPU/package runs нет. Прочитаны сохранённые TRX и verification summaries:
Aether1543/Editor763 от28октября, Mithril111 от27октября Debug/Release.
Длинные датированные абзацы STATUS перенесены ниже без удаления исходных failures.

### Снимок прежней сводки

Ниже сохранено содержимое STATUS до сверки. Указания «следующий шаг»,
«текущий» и открытые failures относятся к датам соответствующих записей;
актуальная очередь и закрытия находятся в STATUS и PLAN. Это архив,
не новые результаты запуска. Относительные ссылки сохранены.

Последняя работа — **2026-10-28, ARCH2d hosts/resources verification**.
Тонкие Examples/AetherEditor и Examples/AetherRuntimeHost отделены от smoke host;
font/OFL принадлежат примеру, styles/themes — managed assemblies, SPIR-V — Aether,
native assets — renderer/platform. Добавлен VerifyExampleHosts с fresh publish вне
workspace, offline metadata/hash checks и запуском из unrelated working directory.
Source Debug/published Release:18resource hashes,16CLI checks и independent runtime
DLL/apphost **4/4**,11source inputs/changes0. Editor **763/763 Debug и763/763 Release**,
failed/skipped0. [Команды и точный scope](HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28).
Nonincremental solution Debug build0errors/1541warnings.
Ресурсный/headless срез ARCH2d проверен; окно/GPU/font rendering не запускались,
BSOD/validation/hardware gates прежние. Далее ARCH2e independent NuGet consumer;
общая GUI/native acceptance ARCH2 остаётся открытой. Aether/full solution tests
и прежние21-scenario source/published matrix этим шагом не повторялись.

Предыдущая работа — **2026-10-28, diagnostic forwarding test fix**.
Управляемый поздний ACK воспроизвёл10s timeout ожидания Accepted после Unconfirmed.
Codec/Unicode/secret-key checks отделены от транспорта; два real-pipe cases проверяют
bounded payload, Unconfirmed1/Lost2/Accepted0, отсутствие replay и drain с ответом
до/после Stop. Runtime и реальные500ms request/2s shutdown budgets не менялись.
Focused44/44 Debug; full Aether **1543/1543 Debug и1543/1543 Release**, failed/skipped0.
Nonincremental solution Debug build0errors/1541warnings.
[Причина test defect, команды и RED→GREEN](HISTORY.md#diagnostic-forwarding-bounded-outcomes--2026-10-28).
Quality follow-up закрыт для ошибочного ожидания; точный источник задержки прежнего
Debug run не восстановить из его TRX без sender snapshot. Editor/full solution tests,
отдельные published/native matrices, NuGet/GPU/performance не повторены; далее ARCH2d/e.

Предыдущая работа — **2026-10-27, compiler staging path fix**.
MSB3030 был ложным missing-file от native MSBuild Copy при существующей DLL на
длинном path/LongPathsEnabled0. Single-project job теперь исключает лишний
project-name segment в SDK artifacts layout; GUID ownership/cleanup прежние.
Новый deep-job test подтвердил RED1→GREEN. Full Aether **1541/1541 Debug и1541/1541
Release** в исходном TEMP, failed/skipped0; OS/TEMP settings не менялись.
Headless compiler/checkpoint/Replace/App-stop **4/4source Debug и4/4published Release**,
458source hashes/changes0. Nonincremental Debug build0errors/1541warnings.
[Причина, binlogs и exact scope](HISTORY.md#eng3-compiler-staging-paths--2026-10-27).
MSB3030 follow-up закрыт в этом scope. Initial forwarding serialization timeout
не повторился, но deterministic fix не выполнен. Editor/full solution tests,
NuGet/GPU/performance не повторены; ARCH2d/e и BSOD ограничения прежние.

Предыдущая работа — **2026-10-27, миграция Build/Replace fixtures**.
Generated games используют IGameSessionEntry.Create и non-copying Contracts reference;
loader/DI/session guards прежние. Новые process regressions подтвердили RED2→GREEN2.
Headless **21/21 source Debug и21/21 fresh published Release**,458source hashes/changes0.
Full Aether **1540/1540 Debug и1540/1540 Release** при compact process-local TEMP;
failed/skipped0. Solution nonincremental Debug build0errors/1541warnings.
[Причина, команды, TEMP profiles и все failures](HISTORY.md#eng3-buildreplace-fixture-migration--2026-10-27).
Исходный redirected TEMP: Debug1534/1540 (6fail), Release1535/1540 (5fail), skipped0.
Пять повторяемых compiler failures — MSB3030 missing intermediate; compact TEMP
устраняет наблюдение, runtime/OS settings не менялись, причина требует отдельного
разбора. Initial Debug forwarding serialization timeout также не исправлялся.
Legacy fixture follow-up закрыт в указанном scope. Editor/full solution tests,
NuGet/GPU/physical/performance не повторены; ARCH2d/e и BSOD ограничения прежние.

Связанное закрытие quality blocker — **2026-10-27, Mithril ready-file read race**.
Оба composition/restart probes получили bounded retry под прежним8s deadline.
Mithril111/111 Debug/Release и published composition14/restart20 прошли;
[точный scope](../mithril/HISTORY.md#ready-json-bounded-read-retry--2026-10-27).
Aether/Editor suites этим шагом не повторены. Следующий quality шаг — legacy
Build/Replace fixtures; ARCH2d/e и BSOD/GPU ограничения прежние.

Предыдущая работа — **2026-10-27, ENG2 CPU FrameReceiver pending-buffer race**.
Worker резервирует private buffer только после выбора нового shared frame;
empty/stale poll и EOF сохраняют pending frame. Два buffers, UI lease и drain
сохранены; ring IO/CRC не выполняются под UI gate. Новый deterministic regression
подтвердил RED(null) → GREEN, добавлен ring reservation/Ready retention test.
CPU transport/session и Frames managed tests: **94/94 Debug и94/94 Release**,
failed/skipped0; solution nonincremental Debug build0errors/1541warnings.
[Причина, exact commands и scope](HISTORY.md#eng2-сохранение-pending-cpu-frame-при-пустом-poll--2026-10-27).
ENG2 follow-up закрыт в этом scope; full module/solution tests, process/package/
GPU/performance не повторены. Mithril ready-file и legacy Build/Replace fixtures открыты.

Предыдущая сверка планов — **2026-10-27, документация и чтение исходников**.
ARCH2a–c реализованы, d начат без завершённой acceptance, e открыт. Таблица
Editor и общая очередь синхронизированы; Reparent и schema-driven component edits
подтверждены исходниками. Build/tests/process/GPU/package runs не выполнялись;
результаты ниже сохраняют даты и scope, quality failures не закрыты.

Ранее прерванная работа — **2026-10-27, ARCH2d hosts/resources, BSOD Windows**.
Добавлен минимальный Examples/AetherEditor с явным standalone runtime и собственными
Noto Sans/OFL assets; AetherRuntimeHost перенесён в Examples, test/solution references
обновлены. Directed smokes остаются в ManualTests/AetherEditorHost; font link больше
не зависит от AlloyTest. Темы — managed code, SPIR-V — embedded Aether resources,
native delivery остаётся у Rendering.Skia/Platform.Silk. Общие library API не менялись.
Solution Debug/Release builds завершились с0errors/1541warnings в каждой конфигурации.
ARCH2d regression/publish/native проверки не завершены: результатов нового Verify нет.
BSOD0xA и предыдущие системные crashes исследуются отдельно; причина и связь с проектом
не установлены. Сбор журналов — TestResults/BSOD-20261027; запуск GPU проверок отложен
до разбора. ARCH2d не закрыт, ARCH2e не начат; прежние quality/hardware limits сохранены.

Предыдущая работа — **2026-10-27, ARCH2c explicit runtime launch/bootstrap/identity**.
NativeRuntimeLaunch задаёт immutable executable/arguments/working directory;
RuntimeBootstrap проверяет bounded payload и actual host/runtime/shared binaries
до child App. Нет implicit current-exe/--runtime; совместимый профиль задаётся явно.
Отдельный AetherRuntimeHost использует bootstrap/Aether без Editor/UI.
[API/migration](../../TrueMoon.Aether/PACKAGE.md),
[контракт и история проверок](HISTORY.md#arch2c-explicit-runtime-launch-bootstrap-и-identity--2026-10-27).

На дату ARCH2c Aether **1536/1536 Debug,1535/1536 Release**, Editor **763/763** в каждой
конфигурации, skipped0. Новых189cases; они прошли в обеих конфигурациях.
Единственный managed failure — прежний CPU FrameReceiver test, Assert.NotNull
в FrameTransportTests.cs:177; повтор для green не делался. Solution builds Debug/
Release0errors; full solution tests и GPU/display/package/performance не повторены.
Source Debug и published Release independent runtime DLL/apphost —4/4, с отдельными
entry/directories, preview без game/UI, selected-game artifact и отказами до readiness.
Расширенные process probes42/46: по19/21 same-exe в каждой поставке и4independent.
Legacy build-artifacts fixture не реализует IGameSessionEntry, replacement fixture
не ссылается на Contracts (CS0234/CS0012); production contracts не ослаблялись.
Checkpoint/diagnostics, faults/cancellation/kill/reap/parent-exit прошли в обеих поставках.
[Evidence](../../TestResults/ARCH2c-20261027/verification.json):1035source hashes,
changes0. Исторический Mithril Release failure не перепроверялся.
Следующий шаг — ARCH2d hosts/resources, затем ARCH2e independent NuGet/native proof;
CPU race и legacy fixtures требуют отдельных quality fixes, общего green нет.

Предыдущая работа — **2026-10-27, ARCH2b Editor library/App integration**.
Editor теперь Library/IsPackable=true без зависимости на concrete TrueMoon bootstrap.
UseAetherEditor копирует options и подключает engine/project/document services,
models/styles и parent-owned View factories; EditorSession согласует startup,
presentation и close/drain. UseAetherEditorWindow предоставляет стандартный
Windows/OpenGL host adapter. App не вложенный, fonts/runtime/CLI выбирает consumer.
Main/CLI/runtime branch и smoke checks перенесены в ManualTests/AetherEditorHost.
[API/migration/ownership](../../TrueMoon.Aether.Editor/PACKAGE.md),
[подробные проверки](HISTORY.md#arch2b-editor-library-и-app-integration--2026-10-27).

Текущие Editor763/763 и Aether1347/1347 отдельно Debug/Release, failed/skipped0;
20новых composition cases включают обе DI реализации, options/duplicates,
UI ownership/failure cleanup/retry и custom viewport без standalone-window wait.
Source Debug и published Release empty/Transform workflows: 4functional processes,
8reaped children, UI sessions/mappings/GPU resources/handles0. PNG просмотрены.
Transform/preview/Undo/Redo/Save/reopen прошёл в обеих поставках.
Первый preview с --validation завершился ErrorLayerNotPresent; финальный GPU proof
без validation, нулевые validation counters не являются validation acceptance.
Solution Debug/Release builds0errors; local Editor pack/metadata inspection успешны,
independent NuGet consumption/полный ARCH2e не выполнялись.
[Evidence](../../TestResults/AetherPackages-ARCH2b-20261027-1/verification.json).
Full solution tests, legacy Build/Play/Replace и physical/performance проверки
не повторены; прежние CPU receiver/Mithril ready-file follow-ups открыты.
Следующий шаг — ARCH2c explicit runtime bootstrap/launch/identity, затем d/e.

Предыдущая работа — **2026-10-27, ARCH2a shared scene persistence**.
SceneFileCodec/ISceneDocumentStore/JsonSceneDocumentStore и SceneFile/SceneFileStamp/
SceneFileConflictException перенесены из Editor.Documents в Aether.Scenes.
Публичный UseAetherScenePersistence подключает lazy/idempotent defaults через App,
сохраняет overrides; Editor document registration делегирует ему persistence.
Editor сохраняет project/document owner, dirty/close UX и команды.
[Карта типов, ownership и migration](PACKAGE_BOUNDARIES.md),
[подробные проверки](HISTORY.md#arch2a-scene-persistence-в-aether--2026-10-27).

Aether1347/1347 и Editor743/743 Debug/Release; narrow engine9/9 и Editor102/102.
Новые9headless cases; Editor cooperation cases обновлены, net+2Editor cases.
Full Debug4296/4296; Release4295/4296, failed1/skipped0:
неизменённый MithrilCompositionProbe.ReadyAsync получил sharing IOException
на ready.json (composition/cobalt). Общего green нет; повтор для green не делался.
Builds0errors/1546warnings в каждой конфигурации: Debug nonincremental/Release
incremental, SDK10.0.403. [Evidence](../../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json).
Source hashes1024/changes0,20test-binary hashes. Native/GPU/published/package/
performance acceptance не повторены; исторический CPU FrameReceiver follow-up открыт.

На дату ARCH2a Editor ещё WinExe/IsPackable=false. Aggregate UseAetherEditor,
library conversion, explicit runtime executable/bootstrap/identity, hosts/resources
и independent NuGet proof остаются ARCH2b–e. [План](PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27).
После перехода — ED6a/ED8a, ED4c.2/3 и project/build/play workflow.

Предыдущая реализация и проверка — **2026-10-27, ENG8a.3/ENE6d.4 child diagnostic delivery**.
Штатные App factories передают bounded structured logs по отдельному Enerit
endpoint; options копируются в launch, receiver живёт до reap/raw drain.
Host сохраняет Origin/PID/time/trace и добавляет authoritative session context.
Known loss и unknown ACK outcome различаются; replay нет. Local JSONL и raw
stdout/stderr продолжают работать независимо.
[Контракт/API](DIAGNOSTICS_DELIVERY.md),
[матрица/команды и неуспешные попытки](HISTORY.md#child-diagnostic-delivery--2026-10-27).

Новые120Aether cases плюс59Core. Aether1338/1338, Editor741/741, full solution
4285/4285 Debug/Release, failed/skipped0. Builds0errors:
Debug nonincremental1546warnings, Release incremental1316warnings, SDK10.0.403.
[Evidence](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source proof:3real Preview children одной session,99control commands при log flood,
slow/broken host sinks, bounded local JSONL/raw, все processes reaped/resources released.
Это не gameplay/input/GPU/display/published/package/performance acceptance.

Короткий ordinary fixture cleanup deadline исправлен в тесте; production budgets
не менялись. Прежний CPU FrameReceiver failure не повторился, ENG2 follow-up открыт;
Mithril ready-file race также не объявлен исправленным.
После ARCH2 — ED6a/ED8a console/export, затем command/frame metrics. Authoring очередь
ED4c.2 → ED4c.3/workflow и прежние hardware gates сохраняются.
Предыдущие [IPC traces](HISTORY.md#ipc-trace-context-в-control-factories--2026-10-26),
[operations](HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[build retention](HISTORY.md#диагностика-сборок-ограничение-хранения--2026-10-26)
и [managed runs](HISTORY.md#managed-logger-runs-в-editor-и-runtime--2026-10-26)
сохраняют отдельные budgets; общего artifact quota нет.

Предыдущая работа — **2026-10-25, ED4f.2 системная граница окна**.
Editor включает UseSystemAccentBorder: Windows accent, theme fallback при отсутствии
цвета, RGB60% при inactive и focus transition160мс. Style/local BorderColor имеет
приоритет; cached цвет обновляется по native notifications без чтения на frame.
15новыхcases, narrow67/67. Related UI893/Hosting114/Editor741 Debug/Release:
1748/configuration,3496всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/206PNG/16reaped children,validation0/0 и zero
owned UI/GPU/mappings; actual active/inactive border pixels проверены, PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).
Evidence: TestResults/AccentBorder-20261024/verification.json. Aether/full solution
tests/packages/physical/performance не повторены. Реальная смена Windows accent
не выполнялась; очередь ED4c.2 names/order → ED4c.3 create/delete прежняя.

Предыдущая работа — **2026-10-24, ED4e островки поверх viewport**.
Обозреватель/инспектор закреплены над полной рабочей областью сцены; отступ16,
скругление8, мягкая тень, content height/scroll и независимый Theme.PanelBackdrop.
Контекстный инспектор сохраняет View и появляется/скрывается за160мс без resize
viewport; closing сразу закрывает ввод. Dark/Light/null fallback проверены.
67новыхcases; narrow103/103. Related UI842/Hosting98/Editor699 Debug/Release:
1639/configuration,3278всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/158PNG/16reaped children,validation0/0 и zero
owned UI/GPU/mappings; финальные native PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4e-островки-поверх-viewport--2026-10-24).
Evidence: TestResults/Islands-20261024/verification.json. Aether/full solution tests,
packages/physical/performance не повторены. Свободные drag/resize/docking отдельно.
Следующий engine шаг прежний: ED4c.2 names/order, затем ED4c.3 create/delete.

Предыдущая работа — **2026-10-24, ED4d.1 компактные действия и контекст инспектора**.
Иконки26/reset24, Tooltip500мс (включая disabled), непрозрачный фон обеих тем;
без выбора скрыта вся колонка, ширина передана viewport. Esc в обозревателе
отменяет drag, затем снимает выбор; повторный выбор сохраняет View/chrome.
25новыхcases, narrow181/181. Текущие UI801/Hosting98/Editor673 Debug/Release:
1572/configuration,3144всего,failed/skipped0. Nonincremental Debug solution build
0errors/1532warnings.12native cases/152PNG/16reaped children,validation0/0,
zero owned UI/GPU/mappings; финальные PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4d1-компактные-действия-и-контекст-инспектора--2026-10-24).
Evidence: TestResults/InspectorChrome-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance в этом уточнении не повторены.
Далее ED4c.2 имена/порядок, ED4c.3 create/delete/duplicate и контекст создания;
прежние CPU receiver/Mithril issues остаются открытыми.

Предыдущая работа — **2026-10-24, ED4d динамический инспектор компонентов**.
Schema-driven секции только фактических компонентов: Boolean/Int64/Number/String,
nullable, provenance/reset, Add/Remove/Restore; единые drafts/history/preview gates.
Общий ComponentEditBatch использует прежние revision/receipts/resolver/Undo/Redo и
scene JSON v1; definition catalogs не мутируются. Retained View/Data/Style,
App/Cobalt/IFactory и compatibility consumers проверены. Live legacy — read-only.
163новыхcases; текущие Aether1142/UI801/Hosting98/Editor648 Debug/Release
=2689/configuration,5378всего,failed/skipped0. Nonincremental Debug solution build:
0errors/1532warnings.12native source Debug/published Release cases,146PNG,
16reaped children,validation0/0 и zero owned UI/GPU/mappings. Финальные PNG просмотрены.
[Проверки и границы](HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Evidence: TestResults/Inspector-20261024/verification.json. Следом ED4c.2 имена/
порядок, затем ED4c.3 create/delete/duplicate. Runtime ECS World и custom editor
providers остаются отдельными задачами. Full solution tests/прочие модули/packages/
physical/performance не повторены; прежние CPU receiver/Mithril issues открыты.

Предыдущая работа — **2026-10-24, ED4c.1 дерево сущностей и Reparent**.
Общий TreeView с иконками/раскрытием/выбором, переносом мышью и клавиатурой;
Editor View/Data/Style и App/Cobalt/typed factory. Смена родителя сохраняет local
Transform/inheritance, использует общий atomic authoring API/history, revision,
Save/reopen и Edit Preview. IO/runtime/Transform draft и устаревшие жесты закрыты.
130новых cases; текущие Aether1050/UI801/Hosting98/Editor577 Debug/Release
=2526/configuration,failed/skipped0. Nonincremental Debug solution build:
0errors/1532warnings.10native source Debug/published Release cases,118PNG,
14reaped children,validation0/0 и zero owned UI/GPU/mappings. PNG просмотрены.
[Точные проверки и границы](HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24).
Evidence: TestResults/Hierarchy-20261024/verification.json.
Следом ED4c.2 — сохраняемые имена/порядок соседей, затем ED4c.3 create/delete/duplicate.
Full solution tests/packages/physical/performance не повторены; прежние CPU receiver/
Mithril fixture issues открыты. Первый срез не включает rename/reorder/multiselect.

Предыдущая работа — **2026-10-24, ED4b выравнивание статусбара**.
Удалена пустая полоса16 под footer: общий WindowFrame размещает grip16 поверх
правого нижнего угла. StatusBar28 и его контейнер имеют одну высоту, нижний край
совпадает с внутренним бордером; details не перекрывается grip. Resize, scale1/1.5/2,
maximize/restore/theme/idle проверены. UI760/Hosting98/Editor530 Debug/Release
=1388/configuration,failed/skipped0; narrow57/57.2native empty cases source Debug/
published Release,20PNG,zero owned resources; Dark/Light screenshots просмотрены.
[Проверки и границы](HISTORY.md#ed4b-выравнивание-статусбара--2026-10-24).
Evidence: TestResults/StatusBarAlignment-20261024/verification.json.
Full solution/headless/игровые native profiles/physical input не повторены;
прежние CPU receiver/Mithril fixture issues открыты. Следом create/delete.

Предыдущая работа — **2026-10-24, ED4b компактная строка состояния**.
Краткий status с иконкой и контекстной отменой слева; справа objects/Edit Preview/
details. Tick/PID, actual preview revision и выбираемые ошибки — в матовом popup.
Edit Preview start/retry/stop перенесены из production inspector в footer;
активный preview показывает «Редактирование сцены», игровые actions — в header.
Responsive overflow/focus/theme/dismissal, retained View/Data/Style и
App/Cobalt/typed factories с обоими DI providers проверены.

Текущие UI759/Hosting98/Editor527 Debug/Release =1384/configuration,80новыхcases,
failed/skipped0. Nonincremental Debug solution build0errors/1532warnings.
Source Debug/published Release вне repo:6native empty/legacy/Transform cases,
68PNG/12reaped children; preview через footer, Undo/Redo/Save/reopen, header
regressions,validation0/0 и zero UI/mapping/GPU ownership. PNG просмотрены.
[План](EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24),
[проверки/границы](HISTORY.md#ed4b-компактная-строка-состояния--2026-10-24).
Evidence: TestResults/StatusBar-20261024/verification.json.
Full solution tests/Aether/headless/packages/physical/performance не повторены;
прежние CPU receiver/Mithril fixture issues открыты. Следом create/delete.

Предыдущая работа — **2026-10-24, матовый фон dropdown меню ED4a**.
Editor использует общий BackdropMaterial/Theme.MenuBackdrop из Argentis/Skia:
blur фона текущего окна + tint, текст/иконки чёткие. Dark/Light/null, fallback,
retained popup/focus и прежние команды сохранены, новый прикладной сервис не нужен.
Текущие UI739/Hosting98/Editor467 Debug/Release =1304/configuration,failed/skipped0.
4native cases source Debug/published Release вне repo,28PNG/4reaped children,
validation0/0 и UI sessions/mappings/GPU resources/handles0. File/View PNG
просмотрены, full solution/Aether/headless/прочие native profiles не повторялись.
[Результаты](HISTORY.md#ed4a-матовый-фон-меню--2026-10-24),
[общий механизм и границы](../alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24).
Evidence: TestResults/Backdrop-20261024/verification.json. Следом create/delete;
прежние CPU receiver/Mithril fixture issues и hardware gates остаются открытыми.

Предыдущая работа — **2026-10-24, ED4a знак TrueMoon и анимированная кнопка меню**.
Слева фиксированный знак TrueMoon, после него кнопка без обычной рамки. Точки
поворачиваются ⋮→⋯ вместе с раскрытием; hover показывает группы, click/Enter/Space
после анимации открывает «Файл». Повторный click закрывает, отмена/detach исключают
late popup. Знак, hit area кнопки и simulation/window actions неподвижны;
caption сдвигается. Retained View/Style, App/Cobalt/IFactory и ownership прежние.
[Дизайн/план](EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки и границы](HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).

Текущие UI723/Hosting98/Editor467 Debug/Release:1288/configuration,13новыхcases,
failed/skipped0. Source Debug/published Release вне repo:4native cases/28PNG/
4reaped children; brand/disclosure/hover/keyboard/theme/tooltip/FocusLost и legacy
game commands, validation0/0 и UI sessions/mappings/GPU resources/handles0.
Финальные collapsed/File screenshots просмотрены. Evidence:
TestResults/HeaderBrand-20261024/verification.json. Full solution/Aether/headless/
остальные native profiles не повторены; прежние CPU FrameReceiver и Mithril
fixture failures не объявлены исправленными. Далее create/delete общего API/history.

Предыдущая работа — **2026-10-23, ED4a компактный заголовок и меню**.
⋯ раскрывает пять menu groups с анимацией180мс/leave grace240мс; название сдвигается,
▶/пауза/стоп и стандартные оконные кнопки неподвижны. Общие Menu/Command/Overlay/
frame transition contracts — в Alloy/Argentis. Header View/Style и UI-owner typed
factory подключены через App/Cobalt; окно владеет View, модели/style borrowed.
File/edit/build/simulation/theme используют общие availability/actions. ▶ —
tracked start→play/resume, stop/cancel/close исключают late Play и drains work.
Header shortcuts, path draft, unsaved decision и независимый Edit Preview сохранены.
[Дизайн/план](EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[руководство](../../TrueMoon.Aether.Editor/README.md#компактная-верхняя-панель),
[все проверки и границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

Related UI712/Hosting98/Editor465 Debug/Release:1275/configuration,105новыхcases,
failed/skipped0. Source Debug/published Release вне repo:10native/header cases,
64PNG/12reaped children; real menu/icon/document/Transform/preview/exit workflows,
validation0/0 и UI sessions/mappings/GPU resources/handles0.10headless cases отдельно.
Последний full Debug3048/3049 (Mithril ready-file fixture IOException), Release3049/3049;
Aether1008/1008 и related UI прошли. Nonincremental build0errors/1532warnings.
Evidence: TestResults/MenuHeader-20261023/verification.json. Общий green не заявлен.
Ранний CPU FrameReceiver null сохранён отдельной ENG2 quality задачей; повторные
passing не означают его исправление. Fresh packages/physical/full alpha не повторены.

Предыдущий ED3a Open→Transform→Preview→Undo/Redo→Save→reopen реализован и повторён
в новых native workflows. Atomic drafts/history, finite/positive validation,
conflict и latest-only preview coordinator остаются прежними;
[подробности ED3a](HISTORY.md#ed3a-transformhistorypreview-в-ui--2026-10-23).

## ARCH2d: hosts и доставка ресурсов — 2026-10-28

Завершён ресурсный/headless срез ARCH2d, прерванный27октября системным BSOD.
Тонкий [Examples/AetherEditor](../../Examples/AetherEditor/README.md) содержит только
Main/CLI, host paths и App composition с UseAetherEditor/UseAetherEditorWindow.
Он явно выбирает отдельный runtime apphost, не содержит --runtime child branch,
demo/SDK/game defaults и smoke implementations. Guard проверяет Windows и именно
Architecture.X64, а не произвольный64-bit процесс. [AetherRuntimeHost](../../Examples/AetherRuntimeHost/README.md)
использует bootstrap/Aether без Editor/UI; owned runtime Main/report остаётся в host.
Directed workflows, прежний CLI и explicit same-exe профиль — в
[ManualTests/AetherEditorHost](../../ManualTests/AetherEditorHost/README.md).
Solution/test references используют новый runtime path.

Owners ресурсов:

- Noto Sans Regular/OFL — собственные Assets/Fonts примера, CopyToOutput/Publish.
  Smoke host ссылается на этот owner, не на AlloyTest. Editor module заимствует
  переданный host font catalog и не ищет шрифты в source checkout.
- EditorViewStyle/Theme находятся в managed owning assemblies; внешних theme files нет.
- Два SPIR-V shaders embedded в Aether; runtime shader compiler/source lookup не нужен.
- Extended Skia поступает из Rendering.Skia в truemoon-native/win-x64, upstream
  Skia/HarfBuzz/GLFW — из существующего renderer/platform dependency closure.
  Vulkan/OpenGL driver/loader остаётся установленным platform prerequisite.

Добавлен [VerifyExampleHosts.ps1](../../ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1).
Он создаёт новый deployment вне source workspace, сохраняет все artifacts/logs,
build/publish binlogs, expected/actual exit codes и before/after hashes11inputs.
PowerShell verifier читает PE metadata и embedded bytes без загрузки UI/native
DLLs. Проверяются `.deps.json` closures (нет game/probe/executable dependencies у
examples, runtime без Editor/UI), exact font/OFL/native hashes, managed style/theme
types, SPIR-V magic/bytes и одинаковый Aether в editor/runtime deployment.
В child process удаляется inherited TM_ENGINE_BOOTSTRAP; invalid runtime startup
отказывает до App. CLI assertions проверяют конкретное сообщение, а не только exit1.
Внешние processes owned verifier, имеют deadline и tree kill/drain при timeout.

| Проверка | Фактический результат |
| --- | --- |
| Build source Debug editor/runtime/probe | 3/3 |
| Fresh publish Release editor/runtime/probe вне workspace | 3/3 |
| Resource hashes/embedded bytes | 18/18 в двух profiles; managed type/closure checks также прошли |
| CLI help/5editor rejections/2runtime rejections | 16/16 source/published, exact messages/exit codes |
| Independent runtime DLL/apphost | 4/4: source Debug2 и published Release2 |
| Full managed Editor Debug | passed763/failed0/skipped0 |
| Full managed Editor Release | passed763/failed0/skipped0 |
| Full solution nonincremental Debug build | errors0/warnings1541 |
| Before/after selected source/resource inputs | 11/11 unchanged, changes0 |

Все CLI/proof процессы запущены из unrelated external working directory. Published
closures находятся в новом external temp deployment, source binaries — в build
output. Каждый independent runtime proof включает2preview generations и selected
game artifact, identity/startup rejection/cancellation/immediate-exit cases и actual
reap/resource cleanup; parent не загружает игру, runtime не загружает Editor/UI,
GPU resources/handles0. Это existing headless proof, не новая GUI acceptance.
Compiler/SDK/game startup отсутствуют при empty editor registration; Cobalt/MS DI,
View ownership, project/document startup/close/rollback проверены existing managed
Editor suite, без изменения library APIs.

Команды для повтора из src (verifier создаёт fresh external deployment):

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug --results-directory 'TestResults/ARCH2d-20261028-1/editor-Debug' --logger 'trx;LogFileName=editor.trx' --verbosity quiet '-bl:TestResults/ARCH2d-20261028-1/editor-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --results-directory 'TestResults/ARCH2d-20261028-1/editor-Release' --logger 'trx;LogFileName=editor.trx' --verbosity quiet '-bl:TestResults/ARCH2d-20261028-1/editor-Release-{}.binlog'
dotnet build TrueMoon.slnx -c Debug --no-incremental -m --verbosity quiet '-bl:TestResults/ARCH2d-20261028-1/solution-Debug-{}.binlog'
```

Фактический verifier запуск через текущий PowerShell вызвал script с новым
RunDirectory; exact command arguments всех6 SDK,16 CLI и4proof процессов сохранены
в [verification.json](../../TestResults/ARCH2d-20261028-1/verification.json).
[Console log](../../TestResults/ARCH2d-20261028-1/deployment-verified.log),
[Debug TRX](../../TestResults/ARCH2d-20261028-1/editor-Debug/editor.trx),
[Release TRX](../../TestResults/ARCH2d-20261028-1/editor-Release/editor.trx),
[solution log](../../TestResults/ARCH2d-20261028-1/solution-Debug.log).
Evidence содержит absolute deployment/report paths и4headless proof reports;
внешние deployment files сохранены. Первые два development runs verifier также
прошли, финальный run дополнительно проверяет точные CLI errors/fault cleanup.

GUI/window startup, native Skia/GLFW loading, font decoding/rendering и GPU preview
не выполнялись. После BSOD причина/связь с проектом не установлены, GPU пауза и
Vulkan validation prerequisite сохраняются. Resource hashes не доказывают ABI,
driver/GPU correctness или полный Open→Transform→preview→Undo→Save/reopen cycle.
ARCH2d implementation/resource/headless scope проверен; общая GUI/native acceptance
ARCH2 остаётся открытой. Далее доступный шаг — ARCH2e fresh independent NuGet
consumer; ProjectReference publish не заменяет этот proof. Aether/full solution
tests, прежние21-scenario source/published matrices, performance/K3/K7/K8/VRAM не
повторялись. Editor/scene/project contracts и формат не менялись, OS/driver settings
не менялись, публикации NuGet не было.

## Diagnostic forwarding: bounded outcomes — 2026-10-28

Закрыт test-quality follow-up для
SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText. Прежний test
проверял clipping через реальный transport и ждал только Accepted==1. При
истечении500ms exchange budget sender учитывает record как Unconfirmed, сохраняет
продвинутый source cursor и не делает replay. Даже полученный позднее valid ACK
не превращает такой исход в Accepted: десятисекундное ожидание уже невыполнимо.
Это test defect; production contracts/options/код не менялись.

Исходный [Debug TRX](../../TestResults/CompiledGameFixtures-20261027-1/full-Debug/aether.trx)
содержит TaskCanceledException из UntilAsync после10s, но не final sender health
или transport timing. Конкретный источник задержки/финальный исход того run
ретроспективно не установлен; CPU load, cold connection или late ACK ему не
приписываются как доказанный факт. Новое воспроизведение проверяет конкретный
допустимый исход, при котором прежний assertion заведомо недостижим.

Controlled RED: server принимает исходный bounded record и удерживает ACK за
TaskCompletionSource barrier. Test ждёт Unconfirmed1 и проверяет Accepted0/Lost0,
отпускает ACK, дожидается PendingRequestCount0 и выполняет прежний Until(Accepted1).
Он падает с тем же TaskCanceledException через10s. [Reproduction source](../../TestResults/DiagnosticForwarding-20261027-1/red/ChildDiagnosticForwarderTests.cs)
и [RED TRX](../../TestResults/DiagnosticForwarding-20261027-1/red/delayed-original-wait.trx)
сохранены; это intentional failed run до исправления test.

Изменения:

- [ChildDiagnosticLimitsTests](../../TrueMoon.Aether.Tests/Diagnostics/ChildDiagnosticLimitsTests.cs):
  SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText теперь синхронно
  проверяет Bound → JSON packet → Decode: exact string limits, surrogate-safe
  clipping, удаление case-insensitive Nonce/Endpoint keys, сохранение свободного
  текста и IsTruncated. Добавлены assertions неизменности исходной записи.
- [ChildDiagnosticForwarderTests](../../TrueMoon.Aether.Tests/Diagnostics/ChildDiagnosticForwarderTests.cs):
  DelayedAck_KeepsBoundedPayloadUnconfirmedAndDrainsWithoutReplay(replyAfterStop:
  false/true) проверяет payload, действительно отправленный worker через generated
  codec/реальные pipes. Пустой reader позволяет сначала дождаться connection;
  затем исходный oversized record публикуется и ACK удерживается до actual timeout.
  Request500ms/shutdown2s остаются прежними, fixed sleep вместо barrier нет.
  Pending1 сохраняется после локального timeout; следующие2records становятся
  Lost2. Ответ до Stop либо после Stop не вызывает replay/переход в Accepted.
  Final Accepted0/Rejected0/Unconfirmed1/Lost2/Failures1, Closed/DrainComplete/
  Released=true, pending0, успешный client Completion; borrowed reader/factory
  не освобождены sender. Late service gate снимается в finally.
- [ChildDiagnosticFixture](../../TrueMoon.Aether.Tests/Diagnostics/ChildDiagnosticFixture.cs)
  хранит общий input record для обоих видов проверки.
- [Delivery guide](DIAGNOSTICS_DELIVERY.md) явно поясняет late ACK и возможность
  DrainComplete вместе с Unconfirmed/Lost.

Существующие Polling_BatchesInOrderCountsInitialAndInternalSourceGapsAndSuppressesImportedRows
и BlockedBorrowedReader_TimesOutOnlyTheStopWaitThenReleasesOnRetry проверяют
Accepted/порядок и cancelled/timed-out Stop wait/ownership retry; они прошли вместе
с новыми cases. Новые factory APIs, test seams и изменение Enerit не понадобились.

| Проверка | Результат |
| --- | --- |
| Старое ожидание с controlled late ACK, Debug | RED: failed1/passed0/skipped0 |
| Forwarder + limits, Debug | GREEN: passed44/failed0/skipped0 |
| Full Aether Debug | passed1543/failed0/skipped0 |
| Full Aether Release | passed1543/failed0/skipped0 |
| Solution Debug, nonincremental | errors0/warnings1541 |

Команды из корня src, каждый MSBuild invocation получил отдельный binlog:

```powershell
# RED: reproduction source сохранён до финального изменения tests.
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~ChildDiagnosticForwarderTests.SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText' --results-directory 'TestResults/DiagnosticForwarding-20261027-1/red' --logger 'trx;LogFileName=delayed-original-wait.trx' --verbosity quiet '-bl:TestResults/DiagnosticForwarding-20261027-1/red-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --filter 'FullyQualifiedName~ChildDiagnosticForwarderTests|FullyQualifiedName~ChildDiagnosticLimitsTests' --results-directory 'TestResults/DiagnosticForwarding-20261027-1/focused-Debug' --logger 'trx;LogFileName=diagnostics.trx' --verbosity quiet '-bl:TestResults/DiagnosticForwarding-20261027-1/focused-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug --no-build --results-directory 'TestResults/DiagnosticForwarding-20261027-1/full-Debug' --logger 'trx;LogFileName=aether.trx' --verbosity quiet '-bl:TestResults/DiagnosticForwarding-20261027-1/full-Debug-{}.binlog'
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release --results-directory 'TestResults/DiagnosticForwarding-20261027-1/full-Release' --logger 'trx;LogFileName=aether.trx' --verbosity quiet '-bl:TestResults/DiagnosticForwarding-20261027-1/full-Release-{}.binlog'
dotnet build TrueMoon.slnx -c Debug --no-incremental -m --verbosity quiet '-bl:TestResults/DiagnosticForwarding-20261027-1/solution-Debug-{}.binlog'
```

[Verification summary](../../TestResults/DiagnosticForwarding-20261027-1/verification.json),
[focused TRX](../../TestResults/DiagnosticForwarding-20261027-1/focused-Debug/diagnostics.trx),
[Debug TRX](../../TestResults/DiagnosticForwarding-20261027-1/full-Debug/aether.trx),
[Release TRX](../../TestResults/DiagnosticForwarding-20261027-1/full-Release/aether.trx),
[solution log](../../TestResults/DiagnosticForwarding-20261027-1/solution-Debug.log).
Каталог evidence начат27октября, завершение/обновление статуса —28октября по
Asia/Yekaterinburg. TEMP/OS settings не менялись. Full Aether включает существующие
headless process tests; отдельные source/published native matrices этого шага
не запускались. Editor/full solution tests, independent NuGet, GPU/display/
validation/performance не повторены; BSOD/hardware gates прежние. Далее ARCH2d/e.

## ENG3: compiler staging paths — 2026-10-27

Закрыт MSB3030 follow-up из предыдущего full run. Вопреки сообщению Copy DLL не
исчезала: standalone reproduction сохранил настоящую Game.Candidate.dll5120bytes,
которую .NET FileInfo/Exists видел по265-character path. Native file-state check
MSBuild Copy на этом же пути возвращал отсутствие файла при LongPathsEnabled0.

Reproduction повторяет compiler command/refs в сохранённом OS-temp workspace:
first короткий job exit0; repeat-deep и fresh-deep exit1/MSB3030, но DLL существует.
Fresh failure исключил повторную компиляцию/cache как необходимый trigger.
[Reproducer](../../TestResults/CompilerIsolation-20261027-1/Reproduce.ps1),
[commands/files](../../TestResults/CompilerIsolation-20261027-1/repro-summary.json).

Binlog MCP недоступен; execution binlog replay через dotnet msbuild подтвердил
успешный Csc, затем failing CopyFilesToOutputDirectory/Copy, SourceFiles=существующая
DLL, SkipUnchangedFiles=true. SDK10.0.403/MSBuild18.9.15 пишет отдельные restore и
build binlogs; анализ использует build events, не restore-only log.
[Task-level replay](../../TestResults/CompilerIsolation-20261027-1/build-replay.log).
ILSpy exact-node inspection установленного Microsoft.Build.Tasks.Core.dll,
Microsoft.Build.Tasks.FileState (token33554520), показал GetFileAttributesEx в
FileDirInfo и FileExists=false при сохранённом IO exception. Assembly SHA и policy
сохранены в evidence. Registry/OS settings не менялись.

Fix: GameBuildCompiler передаёт IncludeProjectNameInArtifactsPaths=false. Один
explicit net10.0 project уже изолирован unique job GUID; output/work ownership,
frozen references, выключенные ProjectReference builds/shared servers и cleanup
прежние. SDK убирает лишний project-name segment: воспроизводимый intermediate
path265→250, Copy и artifact publication проходят. Новых APIs/scopes/storage owners
нет; произвольно длинные paths и ProjectReference graph acceptance не заявляются.
Extended-path prefix вариант дал SDK import MSB4019 и не применяется.

Новая regression Compiler_DeepOwnedJobsBuildAndLoadSameProjectTwiceWithoutChangingFirstArtifact
создаёт owned root с прежним265-character intermediate path, дважды компилирует тот
же project с private DLL и revisions101/102. Проверяет разные jobs/build IDs/ALCs,
неизменные bytes/state первого pinned artifact, state/private behavior второго,
compiler reap, retained diagnostics и отсутствие work/output после cleanup.
На прежнем compiler RED1/MSB3030; после fix GREEN. Fixture сохраняет artifacts
при failure этой regression вместо удаления diagnostics; passing cleanup прежний.

Windows x64/SDK10.0.403/net10.0/xUnit2.9.3/VSTest; исходный redirected TEMP сохранён,
LongPathsEnabled0 до/после. [Exact commands, TRX, source/binary SHA и SDK inspection](../../TestResults/CompilerIsolation-20261027-1/verification.json).

| Проверка | Результат |
| --- | --- |
| Новая deep-job regression, Release до fix | Passed0/Failed1/Skipped0, expected RED; [TRX](../../TestResults/CompilerIsolation-20261027-1/red/red.trx) |
| Deep-job + прежние compiler consumers, Release после fix | Passed8/Failed0/Skipped0; [TRX](../../TestResults/CompilerIsolation-20261027-1/green-Release/green.trx) |
| Full Aether, исходный TEMP, Debug | Passed1541/Failed0/Skipped0; [TRX](../../TestResults/CompilerIsolation-20261027-1/full-Debug/aether.trx) |
| Full Aether, исходный TEMP, Release | Passed1541/Failed0/Skipped0; [TRX](../../TestResults/CompilerIsolation-20261027-1/full-Release/aether.trx) |
| Solution nonincremental Debug build | Exit0/errors0/warnings1541; [log](../../TestResults/CompilerIsolation-20261027-1/solution-Debug.log) |
| Compiler/checkpoint/Replace/App-stop headless, source Debug + fresh published Release |4/4 +4/4; [summary](../../TestResults/EngineSessions/compiler-isolation-20261027-1/summary.json), Completed=true,458source hashes/changes0 |

Full commands: dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj
-c Debug/Release --no-build с отдельными TRX/results-directory/binlog. RunProbe
использует -Scenarios build-artifacts,checkpoint,replacement,app-stop и сохраняет
build/publish commands. GPU/viewport/editor flags не заданы. Native matrix проверяет
selected artifact/frame provenance, state/checkpoint/rollback, Stop fences,
compiler tree kill/reap и released mappings/child resources.

Compiler path follow-up закрыт в проверенном single-project scope. Исходные
failures1534/1540 Debug и1535/1540 Release сохраняют прежнюю запись; compact TEMP
больше не требуется для текущего full run. Initial diagnostic forwarding timeout
в этом run не повторился, но его deterministic fix не выполнялся и follow-up открыт.
Editor/full solution tests, полная21-scenario process matrix, independent NuGet,
GPU/display/validation/performance не повторены. ARCH2d/e и BSOD/hardware gates прежние.

## ENG3: Build/Replace fixture migration — 2026-10-27

Закрыты две legacy fixture ошибки, сохранённые в ARCH2c. BuildProbe.Project
генерировал IGameSession без обязательного IGameSessionEntry: child loader завершался
до Hello acknowledgement. Generated game теперь реализует static Create с borrowed
IServiceResolver и caller-owned Game. Generated projects BuildProbe/ReplacementProbe
добавляют non-copying TrueMoon.Contracts reference через TrueMoonContractsPath наряду
с AetherContractPath. Replacement variants уже содержали entry из ProbeGame, но
теряли Contracts reference и получали CS0234/CS0012.

GameBuildCompiler, GameSessionFactory/loader, App/DI contracts, session/nonce/build
identity и protocol guards не менялись. Один owner, frame drain и child reap прежние.
RunProbe получил отдельные unique binlogs у своих build/publish invocations.

Добавлен NativeRuntimeProcessTests.CompiledGameFixtures_PreserveSelectedBuildCheckpointRollbackAndCleanup
с двумя scenarios. Процессы действительно компилируют и загружают selected games;
дополнительно к внутренним probe assertions проверяются:

- build-artifacts:2build IDs, revisions10/13, colors A/B, generations2/3, byte SHA,
  отсутствие copied Aether/Contracts DLLs, сохранение session при compile failure,
  отсутствие late launch после Stop, compiler reap и3released child leases;
- replacement:4builds/revisions7/8/9/10, checkpoint и first-frame tick/color,
  selected build/fresh generation, MaxLive1, успешный однократный rollback при
  candidate rejection, original/recovery errors при failed rollback, Stop fence,
  zero mappings и8reaped/released leases. Authoring state/parent CLR сохраняются.

На исходных fixtures новый test получил RED2: entry rejection в child и compiler
CS0234/CS0012 в replacement. После migration тот же test GREEN2; assertions/guards
не ослаблялись. Логи failed processes сохранены в TestResults/CompiledGameFixtures.

Windows x64/SDK10.0.403/net10.0/xUnit2.9.3/VSTest.
[Все команды, TRX, исходные failures, TEMP profiles и source/binary SHA](../../TestResults/CompiledGameFixtures-20261027-1/verification.json).

| Проверка | Результат |
| --- | --- |
| Новый process regression, Release до fix | Passed0/Failed2/Skipped0, expected RED; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/red/red.trx) |
| Тот же regression, Release после fix | Passed2/Failed0/Skipped0; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/green-Release/green.trx) |
| Solution nonincremental Debug build | Exit0/errors0/warnings1541; [log](../../TestResults/CompiledGameFixtures-20261027-1/solution-Debug.log) |
| Full Aether, исходный redirected TEMP, Debug | Passed1534/Failed6/Skipped0; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/full-Debug/aether.trx) |
| Full Aether, тот же TEMP, Release | Passed1535/Failed5/Skipped0; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/full-Release/aether.trx) |
| Full Aether, compact process-local TEMP, Debug | Passed1540/Failed0/Skipped0; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/compact-Debug/aether.trx) |
| Full Aether, тот же compact TEMP, Release | Passed1540/Failed0/Skipped0; [TRX](../../TestResults/CompiledGameFixtures-20261027-1/compact-Release/aether.trx) |
| Source Debug apphost + fresh published Release headless matrix |21/21 +21/21, [summary](../../TestResults/EngineSessions/compiled-game-20261027-1/summary.json), Completed=true,458source hashes/changes0 |

Full suite запускается dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj
-c Debug/Release --no-build с отдельными results-directory/TRX/binlog в evidence.
Headless RunProbe запускается с21scenario из Probe.Scenarios, включая diagnostics;
GPU/viewport/editor flags выключены.42cases подтверждают repeat20generations,
stale/invalid peers, startup/command cancel/timeouts, crash/disconnect/heartbeat,
forced/hung stop, parent-exit/App close, compiler/selected builds, checkpoint,
Replace/rollback и output diagnostics. Exact literal scenario list сохранён в evidence.

### Дополнительные full-suite failures и TEMP profile

Первый full Debug сохранил5compiler failures в AetherDiagnosticsTests (Cobalt/DI),
AetherTelemetryTests success (Cobalt/DI) и GameEditorTests.Build_PublishesCandidateWithoutLaunchingOrChangingRunningWorld.
Runtime compiler stdout терялся при fixture cleanup; диагностический повтор только
двух AppCompiler cases сохранил [MSB3030 stdout](../../TestResults/CompiledGameFixtures-20261027-1/observed-f8cd343abaf44de585ce92a5bede3263.log):
missing work/obj/Game.Candidate/release/Game.Candidate.dll, один из paths265characters.
Повтор под исходным TEMP опять Failed2. Первый укороченный TEMP внутри run directory
тоже Failed2; более компактный TestResults/t дал Passed2 без source changes.

Final compact runs задают TEMP=TMP=<src>/TestResults/t только дочернему process
environment. Пустой Directory.Build.props в этом ignored каталоге сохраняет
отсутствие repository props у generated fixture projects, как в исходном OS temp.
Постоянные OS settings и runtime compiler не менялись. Зависимость от профиля
TEMP подтверждена, точная SDK/filesystem причина MSB3030 и поддержка длинных paths
остаются отдельным quality follow-up; compact passing не означает их исправление.

Шестой initial Debug failure — ChildDiagnosticForwarderTests.SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText:
UntilAsync ожидал Accepted1 и завершился TaskCanceledException. Release/default и
оба compact runs прошли, но deterministic fix не выполнен; timeout остаётся
отдельным follow-up. Повторы для диагностики/изменённой конфигурации не переписывают
исходные failures как green. ENG2 CPU receiver regression прошёл в обоих full profiles.

Editor/full solution tests, independent NuGet consumers, GPU/display/validation/
performance acceptance не повторены. Legacy Build/Replace follow-up закрыт в
указанном source/published scope; ARCH2d/e и BSOD/hardware ограничения сохранены.

## ENG2: сохранение pending CPU frame при пустом poll — 2026-10-27

Исправлена гонка FrameReceiver: при удерживаемой UI lease второй буфер был
pending. Worker переводил его в worker/free и увеличивал Replaced до проверки,
есть ли новый shared frame. Пустой poll или EOF оставлял TakeLatest без последнего
кадра; Received при этом уже подтверждал его получение.

SharedFrameRing получил internal lazy destination reservation: сначала validation
и выбор newer Ready slot, затем короткий callback резервирует private buffer под
receiver gate. Metadata IO, pixel copy и CRC выполняются вне receiver gate. Если
UI успел занять оба буфера, slot остаётся Ready для следующего poll. Пустые/stale
polls и EOF не изымают pending storage. Сохраняются ровно2private pixel buffers,
один pending frame, неизменность UI lease, bounded latest-frame replacement,
observable Completion и ожидание UI leases при Dispose. Public API/wire/layout/
App/DI ownership не менялись; новых factories/scopes нет.

Regression tests в FrameTransportTests:

- Receiver_EofRetainsPendingFrameWhileUiLeaseIsHeldAndDrains удерживает first UI
  lease, получает второй pending frame, затем ждёт EOF Completion. Это гарантирует
  следующий worker scan без timed sleep. Проверяет frame2/tick20/CRC, неизменные
  first pixels, Received2/Replaced0 и незавершённый Dispose до возврата lease.
- Ring_CopyLatestReservesBufferOnlyForNewFrameAndRetainsReadyWhenUnavailable
  проверяет отсутствие reservation на empty/stale poll, сохранение Ready при
  недоступном буфере и последующее получение того же frame/tick/CRC.
- Прежний Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain сохранён;
  добавлен await using для cleanup receiver даже при failed assertion.

Первый новый receiver test на неизменённом production code получил RED:
Assert.IsType<FrameLease> получил null. После исправления GREEN с теми же
assertions; polling/retry-to-green или ослабления прежнего теста нет.

Windows x64/SDK10.0.403/net10.0/xUnit2.9.3/VSTest.
[Evidence, exact commands, source/binary hashes и5binlogs](../../TestResults/FrameReceiver-20261027-1/verification.json).

| Проверка | Результат |
| --- | --- |
| Новый receiver regression, Release до fix | Passed0/Failed1/Skipped0, expected RED; [TRX](../../TestResults/FrameReceiver-20261027-1/red/red.trx) |
| FrameTransportTests, Release после fix | Passed52/Failed0/Skipped0; [TRX](../../TestResults/FrameReceiver-20261027-1/green-Release/green.trx) |
| CPU transport/session и Frames managed contracts, Debug | Passed94/Failed0/Skipped0; [TRX](../../TestResults/FrameReceiver-20261027-1/cpu-Debug/cpu.trx) |
| Тот же scope, Release | Passed94/Failed0/Skipped0; [TRX](../../TestResults/FrameReceiver-20261027-1/cpu-Release/cpu.trx) |
| Solution build Debug --no-incremental -m | Exit0/errors0/warnings1541; [log](../../TestResults/FrameReceiver-20261027-1/solution-Debug.log) |

Final scope filter: FullyQualifiedName~TrueMoon.Aether.Tests.FrameTransportTests
OR FullyQualifiedName~TrueMoon.Aether.Tests.FrameSessionTests
OR FullyQualifiedName~TrueMoon.Aether.Tests.Frames. Exact runnable commands with
TRX/results-directory/binlog flags сохранены в evidence. Smokes используют
in-process runtime/transport fixtures и named CPU mappings, не native GPU/window.

ENG2 pending-buffer follow-up закрыт в указанном deterministic/narrow scope.
Полные Aether/Editor/solution tests, source/published process probes, NuGet/native/
GPU/physical/performance acceptance не повторены. Прежний общий full Release
failure не переписан как green; Mithril ready-file race, legacy Build/Replace
fixtures, BSOD investigation и ARCH2d/e остаются открытыми. Следующий quality
шаг — безопасная ready JSON publication/read у Mithril, затем legacy fixtures.

## ARCH2c: explicit runtime launch, bootstrap и identity — 2026-10-27

Реализован отдельный native launch contract без зависимости runtime на Editor.
NativeRuntimeLaunch — immutable executable/runtime managed entry/arguments/working
folder; аргументы копируются и передаются через ArgumentList, implicit --runtime и
выбор current process удалены. EngineChild использует ProcessUnitHandle, сохраняя
Mithril RestartPolicy.Never, parent/control environment и sole ownership stop/kill/reap.
AetherOptions.RuntimeLaunch передаётся штатному native factory через App/DI;
без descriptor headless registration работает, попытка native launch даёт понятную
configuration error. Custom RuntimeFactory не требует native configuration.
CurrentProcess("--runtime") — явный совместимый same-exe профиль; все затронутые
EngineSessionProbe и AetherEditorHost consumers его настраивают явно.

BuildIdentity.ForRuntime связывает role-labelled host/runtime managed entry bytes
и точные shared Aether/Contracts/Enerit/Mithril bytes. Обе deployment директории
проверяются относительно actual loaded shared assemblies; parent проверяет свой
entry до spawn, child — собственный entry и counterpart до App. Same-exe сохраняет
прежний HostId, separate pair не сводится к исключению entry assembly из hash.
RuntimeLaunchIdentity Version1 передаётся только в native SessionLaunch bootstrap;
Control3/Frame1/Snapshot2, session/generation/nonce/Hello/PID fences не менялись.
Директории не входят в content identity, но counterpart paths обязательны и absolute.
Проверка trusted-local, не authentication и не complete deployment/native fingerprint;
in-place binary replacement во время сессии не поддерживается. Новый pair требует
rebuild game artifacts; прежние checkpoints несовместимы. Scene/project compatibility сохранена.

RuntimeBootstrap содержит общие read/parse/validate/encode, JSON<=16384characters и
depth32, parent/endpoint/role/version validation, normalized absolute report/artifact
paths и copied diagnostics options. Strict game требует selected artifact; только
явный legacy probe profile допускает matching fixed Game.Logic. Bootstrap validation
не создаёт App/resources. Прямой runtime registration также отвергает malformed
artifact paths и проверяет native identity при её наличии; synthetic low-level
registrations не привязаны к testhost binary. Same-exe path equality согласована
с Windows case-insensitive filesystem semantics.

ManualTests/AetherRuntimeHost — отдельный Main/App composition с references только
на TrueMoon bootstrap/Aether. Он запускает built-in preview или selected-game,
пишет completion metadata/resources и не загружает Editor/UI. RuntimeLaunchProbe
и process tests проверяют реальные разные entries/directories, DLL/dotnet и apphost,
в том числе перенесённый runtime closure без UI/Game payload в каталоге с пробелами.
Game artifact публикуется из уже собранных bytes без SDK/compiler и загружается
только child. Дополнительно проверяются parent preflight mismatch, child-side
fingerprint/actual-entry rejection с обходом preflight, startup cancellation,
immediate exit и explicit same-exe regression. Никакая GPU surface не запрашивается.

### Текущие проверки и ограничения

Evidence: [verification.json](../../TestResults/ARCH2c-20261027/verification.json),
[bounded runner](../../TestResults/ARCH2c-20261027/Verify.ps1).
Windows x64/SDK10.0.403/net10.0/xUnit/VSTest; source code/project hashes1035,
изменений0. [SDK/runtime environment](../../TestResults/ARCH2c-20261027/dotnet-info.log).
Новых189test cases; все189прошли в обеих конфигурациях.

| Проверка | Текущий результат |
| --- | --- |
| Solution build Debug/Release --no-restore | Exit0 в обеих; warnings остаются, это не full-solution test run |
| Aether full Debug |1536/1536, failed/skipped0 |
| Aether full Release |1535/1536, failed1/skipped0: прежний CPU FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain, Assert.NotNull на line177 |
| Editor full Debug/Release |763/763 отдельно в каждой, failed/skipped0 |
| Publish Release parent/runtime | Exit0, отдельные deployment directories |
| Independent process proof |4/4: source Debug и published Release, каждый dotnet DLL/apphost |
| Extended same-exe process matrix |19/21 source и19/21 published; failures ниже |

Команды build/test/publish и literal arguments сохранены в verification.json.
Independent CLI: EngineSessionProbe --runtime-launch-proof <absolute runtime DLL/EXE>
<fresh report directory>. В каждом из4runs:2preview generations,1selected-game,
5negative/configuration cases и1explicit same-exe preview; все owned children reaped,
completion resources/handles0. DLL proof parent запускается через dotnet, apphost
proof — через parent.exe. В xUnit обе runtime формы дополнительно stage-ятся по
собственному dependency manifest без Editor/UI/Game files. Source/published proof
не заменяет NuGet consumer или GPU/presentation acceptance.

Extended matrix42/46вместе с4independent runs; успешны repeat20generations, stale,
bad-Hello/scene, startup/command cancellation/deadlines, stop-pending, crash,
disconnect, heartbeat, forced/hung stop, launch failure, parent-exit, App stop,
checkpoint restore и stdout/stderr/structured diagnostics в обеих поставках.
Два legacy scenario failed в каждой поставке:

- **build-artifacts:** unchanged generated BuildProbe.Project game implements only
  IGameSession, без обязательного IGameSessionEntry. Child loader корректно отвергает
  его; parent получает IOException до Hello acknowledgement. Новый independent
  selected-game proof использует правильный entry contract и проходит.
- **replacement:** unchanged generated project не ссылается на TrueMoon.Contracts;
  исходник игры использует TrueMoon.Services/IServiceResolver. Compiler возвращает
  CS0234/CS0012 до runtime Replace. Production contracts/loader не ослаблялись.

CPU Release failure и эти4fixture failures сохранены без retry-to-green. Поэтому
Completed=true в evidence означает завершение matrix, а AllTestsPassed=false и
AllProbesPassed=false сохраняют реальные quality blockers. Full solution tests,
Mithril ready-file race, GPU/validation/physical UI/VRAM/performance/NuGet checks
не повторены. Прежние результаты имеют прежние даты.

До финальной matrix выявлены недостающие using/TryResolve в новых tests и4старых
synthetic preview fixtures с relative build paths после tightening validation.
Исправлены только test imports/access и absolute fixture inputs; ранний failed TRX
сохранён как first-aether-debug-failed.trx. Guards не отключались. PowerShell runner
переведён на .NET SHA256 вместо недоступного Get-FileHash; validation commands
возобновлялись без повторного исполнения уже завершённых tests/probes.

ARCH2c реализован и проверен в указанном native/headless scope, не объявляет общего
green. Следующий шаг — ARCH2d hosts/resources, затем ARCH2e fresh independent NuGet
consumer/native acceptance. CPU receiver и legacy fixtures — отдельные quality
fixes. Console/export и новые authoring UI features остаются после всего ARCH2.

## ARCH2b: Editor library и App integration — 2026-10-27

TrueMoon.Aether.Editor переведён в Library/IsPackable=true с Description/tags и
собственным PACKAGE.md. Concrete bootstrap reference TrueMoon заменён на Contracts;
NuGet dependencies — Aether, Alloy.Hosting, Contracts. Пакет содержит Editor DLL,
PACKAGE.md/license/icon/README, без exe/runtimeconfig/font payload. Local pack
успешен; независимый package consumer не выполнялся и остаётся ARCH2e.

UseAetherEditor(configureEngine, configureEditor) собирает engine/authoring/
documents/projects и models/styles/typed factories. Initial ProjectPath фиксируется
на setup и открывается при startup. Duplicate aggregate/existing engine/project
graph отклоняются; custom stores/styles/root model/root factory сохраняются.
Никаких window/native/SDK/compiler/game действий при регистрации; empty startup
не открывает проект. Custom viewport не ждёт default window readiness.

EditorSession получает явные constructor dependencies, обновляет borrowed View
через Present на owner thread и согласует RequestClose/terminal Stop. DI владеет
services/models/tasks; project — document/authoring; parent/window — View. Lifecycle
aliases не disposable. Failed startup сохраняет причину и выполняет rollback;
factory failure освобождает unpublished tree; cleanup failure наблюдаем и retryable.
Viewport bridge хранит weak references на UI. UseAetherEditorWindow(fonts, configure)
заимствует host-selected font catalog, подключает Header/close policy. App startup
ждёт first presentation/viewport readiness; rollback явно останавливает окно,
даже когда ошибка возникает после native creation. Adapter cancel/drain освобождает
свои subscriptions/initialization work без второго владельца View/engine.

Program/EditorApplication/EditorLaunchOptions/ProjectEntryPoint/RuntimeEntryPoint
и smoke checks перенесены в ManualTests/AetherEditorHost (собственный RootNamespace).
Host использует публичные extensions; CLI regression tests ссылаются на host,
остальные tests продолжают использовать package assembly. Legacy --runtime и
entry-assembly fingerprint пока прежние: explicit runtime — ARCH2c. Host fonts
пока копируются из AlloyTest, завершение ресурсов/примеров — ARCH2d. Demo helper
не реорганизован этим шагом. Scene/project format compatibility не менялась.

**Фактически выполненные проверки:** SDK10.0.403, net10.0, xUnit2/VSTest.
Editor **763/763** Debug и Release, Aether **1347/1347** Debug и Release,
failed/skipped0. Новых20cases в AetherEditorCompositionTests; Cobalt и Microsoft DI
проверены отдельно через Theory. Финальные incremental solution builds Debug/Release:
0errors,0/175warnings; предыдущие промежуточные231/649warnings сохранены в своих logs.
Окончательные Editor builds/tests после readiness уточнения успешны. Publish Release и local pack финального
кода успешны. Full solution tests не запускались; прежний Mithril ready-file
Release failure и CPU FrameReceiver follow-up не считаются исправленными.

| Requirement | Evidence: методы AetherEditorCompositionTests |
| --- | --- |
| Lazy setup/startup и sole service disposal | EmptyApp_RegistrationAndStartupDoNotOpenProjectCreateViewOrLaunchRuntime |
| Parent-owned independent View, styles, owner thread, recreation | ViewFactory_ParentOwnsIndependentViewsAndAppPreservesInjectedStyle |
| Options snapshot, initial project, document save/Cancel/close | ProjectStartup_CopiesOptionsAndUsesSharedDocumentSaveAndClosePolicy |
| Startup rollback и original error | ProjectStartupFailure_RollsBackEngineAndProjectsAndPreservesOriginalError |
| Unpublished tree cleanup и borrowed model retry | FactoryFailure_DisposesUnpublishedTreeAndCanRetryWithSameBorrowedModels |
| Invalid options и duplicate attachment | InvalidOptions_AreRejectedBeforeEngineConfiguration; DuplicateOrPreexistingEngine_RejectsAmbiguousOwnership |
| Observable cleanup failure/retry | CleanupFailure_RemainsObservableAndCanRetryBeforeAppDisposal |
| Custom transport без default-window wait | CustomViewport_DoesNotWaitForStandardWindowToStartPreview |
| Library boundary без bootstrap/CLI/smoke types | LibraryArtifact_HasNoEntryPointBootstrapDependencyOrExecutableProbeTypes |

Основные команды (каждая с собственным binlog/log/TRX в evidence):

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Debug -bl:TestResults/AetherPackages-ARCH2b-20261027-1/editor-debug-final.binlog --logger "trx;LogFileName=editor-debug-final.trx" --results-directory TestResults/AetherPackages-ARCH2b-20261027-1
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release -bl:TestResults/AetherPackages-ARCH2b-20261027-1/editor-release-final.binlog --logger "trx;LogFileName=editor-release-final.trx" --results-directory TestResults/AetherPackages-ARCH2b-20261027-1
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Debug -bl:TestResults/AetherPackages-ARCH2b-20261027-1/aether-debug.binlog --logger "trx;LogFileName=aether-debug.trx" --results-directory TestResults/AetherPackages-ARCH2b-20261027-1
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj -c Release -bl:TestResults/AetherPackages-ARCH2b-20261027-1/aether-release.binlog --logger "trx;LogFileName=aether-release.trx" --results-directory TestResults/AetherPackages-ARCH2b-20261027-1
dotnet publish ManualTests/AetherEditorHost/AetherEditorHost.csproj -c Release --no-build -o TestResults/AetherPackages-ARCH2b-20261027-1/publish-final -bl:TestResults/AetherPackages-ARCH2b-20261027-1/publish-final.binlog
dotnet pack TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj -c Release --no-build -o TestResults/AetherPackages-ARCH2b-20261027-1/packages-verified -bl:TestResults/AetherPackages-ARCH2b-20261027-1/pack-verified.binlog
```

Финальные native directories — native-{empty,transform}-{debug,release}-v2:
**4/4functional processes**, source Debug/published Release, Release cwd вне repo.
Empty не создаёт game/compiler; Transform проходит invalid draft, theme retention,
actual preview pixels/ack, Undo/Redo, SaveAs/Close/reopen и clean saved checkpoint.
**8children drained/reaped**, каждый report: UI sessions/mappings/GPU resources/
handles0, ViewDisposed=true. Preview notifications остаются на UI owner, compiler
не запускается; source scene bytes не изменены. PNG прочитаны и визуально проверены.

Первый native preview с --validation остановился на **ErrorLayerNotPresent**:
в текущем окружении validation layer не найден, прежнего ignored tooling по
документированному пути нет. SDK/driver/settings не устанавливались и не менялись.
Финальные functional GPU runs **без validation**; нулевые validation counters
не доказывают validation acceptance. Это ограничение сохранено для ARCH2e.
Ранний empty --validation проверил только UI без Vulkan child и не закрывает его.

Неуспешные первые build attempts (using/relative namespace после переноса, затем
опечатка Choose в новом тесте) сохранены. Новый project/UI test сначала использовал
fake text backend без rich text; заменён на существующий InspectorFixture CPU
renderer. Ни один такой attempt не засчитан passing. Предварительные native runs
до readiness уточнения также отделены от финальных v2 artifacts.

Evidence: [verification.json](../../TestResults/AetherPackages-ARCH2b-20261027-1/verification.json),
TRX/logs/binlogs, final package inspection и SHA256 source/binary/artifact manifests.
NuGet publication, независимое NuGet решение, legacy Build/Play/Replace, physical
DPI/input, loss/VRAM, performance и full alpha не проверялись. Следующий шаг —
ARCH2c; ARCH2d/e и прежние ED6a/ED8a → ED4c.2/3/workflow остаются открыты.

## ARCH2a: scene persistence в Aether — 2026-10-27

Начало реализации согласованного package/host перехода. Статическая инвентаризация
engine/Editor типов, refs и ownership записана в [карте границ](PACKAGE_BOUNDARIES.md).
SceneFileCodec, ISceneDocumentStore, JsonSceneDocumentStore, SceneFile,
SceneFileStamp и SceneFileConflictException перенесены из Editor.Documents в
Aether.Scenes. DTOs имеют отдельные semantic files; namespace соответствует пути.
Production code не linked/duplicated, Aether не получил зависимости на Editor/UI/
concrete bootstrap. FormatVersion1, deterministic UTF8, bounds, SHA256 exact-byte
stamps, create-only/atomic replace/cooperative write-lock/cancellation contracts
не менялись. Public alpha namespace migration описан в guide/package README.

UseAetherScenePersistence регистрирует lazy shared codec/store через App после
borrowed authoring factory; repeated attachment idempotent, overrides сохраняются.
UseAetherEditorDocuments делегирует общую persistence registration, оставляет
Editor-owned document factory и отдельный private repeat-registration marker.
Можно подключить engine persistence до Editor; custom codec теперь переиспользуется.
Editor project/document/dirty/close/selection/workspace/View responsibilities
сохраняются. Aggregate UseAetherEditor, WinExe→library, hosts/bootstrap/identity/
resources и independent NuGet acceptance ещё не реализованы (ARCH2b–e).

IDE findTests недоступен; один Roslyn parse-only pairing run выявил existing codec/
store/document tests. Analyzer build binlog сохранён, repository под pairing не
собирался. Существующие behavioral tests используют новые engine types; namespace
imports обновлены у Editor consumers/smokes/tests. Новая focused suite —
ScenePersistenceRegistrationTests в Aether.Tests без reference на Editor executable.

| Требование ARCH2a | Точное доказательство |
| --- | --- |
| Shared API работает без Editor; scene bytes/stamps/replace/conflict корректны на обеих DI | ScenePersistenceRegistrationTests.App_DefaultPersistenceSharesServicesAndRoundTripsIndependentEngineDocuments (2cases): непустая scene, raw Transform value, byte hash, independent read, revision replacement, stale-stamp conflict без изменения последнего файла |
| Lazy/idempotent defaults, existing overrides и один App owner | App_ExistingOverridesRemainLazyAndHaveOneAppOwnerAcrossRepeatedAttachment (2); App_DefaultServicesDoNotCreateAuthoringProductsOrFilesDuringStartup (2) |
| Required authoring/null context validation до activation | App_MissingAuthoringFailsBeforeActivatingPersistenceOverrides (2); NullContext_IsRejectedBeforeRegistration (1) |
| Editor reuse общего persistence и caller-owned documents | DocumentRegistrationTests.App_DefaultCodecAndStoreAreSharedWhileDocumentFactoryProductsRemainCallerOwned (4cases, standalone/engine-first × bothDI); App_ExistingCodecIsReusedAndDefaultStoreUsesItsBorrowedValidationFactory (2) |
| Старые format/IO/error bounds сохраняются | SceneFileCodecTests17methods и JsonSceneDocumentStoreTests11methods; соответствующие migration/import changes без ослабления assertions. Combined codec/store/registration102/102 |

Inline assertion review: реальные file bytes/hash/revisions/values, exact conflict
outcome и сохранение нового файла; defaults/overrides проверены через one shared
registration, no-activation и exactly-once disposal counters. Обычные registration
guards/record properties не подменены mock-only presence assertions. No production
mutations/coverage collection; это не formal branch/coverage или performance proof.

Narrow Debug engine9/9 и Editor102/102, exit0:
```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.ScenePersistenceRegistrationTests' --logger 'trx;LogFileName=narrow-engine-1.trx' --results-directory TestResults/AetherPackages-ARCH2a-20261027-1 -bl:TestResults/AetherPackages-ARCH2a-20261027-1/narrow-engine-1.binlog
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~SceneFileCodecTests|FullyQualifiedName~JsonSceneDocumentStoreTests|FullyQualifiedName~DocumentRegistrationTests' --logger 'trx;LogFileName=narrow-editor-1.trx' --results-directory TestResults/AetherPackages-ARCH2a-20261027-1 -bl:TestResults/AetherPackages-ARCH2a-20261027-1/narrow-editor-1.binlog
```

Final source validation SDK10.0.403/net10.0/xUnit2.9.3/VSTest:
Debug nonincremental и Release incremental builds0errors/1546warnings каждый.
Aether1347/1347 (+9), Editor743/743 (net+2) в каждой конфигурации; Core373,
Cobalt179/generator91, Enerit343/generator114, UI893/Hosting114 также прошли обе.
Mithril99/99 Debug,98/99 Release. Full **Debug4296/4296, Release4295/4296**,
failed1/skipped0. Общего green нет, повтор для green не делался.
Release CompositionProcessTests.AppUnits_OwnedProcessesAndLocalHosting_CleanupOnBothProviders
(composition,cobalt) получил sharing IOException в неизменённом
MithrilCompositionProbe.Fixture.ReadyAsync line172 на ready.json.
[Отдельная Mithril запись](../mithril/HISTORY.md#composition-ready-json-sharing-failure--2026-10-27).

Точные four-step build/test arguments находятся в validation-steps.json,
TRX/log/binlogs сохранены. [Machine evidence](../../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json):
1024source hashes/changes0,20test-binary hashes,22TRX/7binlogs включая analyzer.
Новых native/GPU/published/package/performance runs нет; предыдущие platform и
FrameReceiver/ready-file quality follow-ups не закрыты. Дальше ARCH2b library/
App integration, согласованно с ARCH2c explicit runtime host.

## Пакетная архитектура ARCH2 — 2026-10-27

Пользователь согласовал развитие TrueMoon как инфраструктурного framework и
подключаемых пакетов: Aether содержит основной engine/components/runtime,
Editor становится library/package с View/Data/styles/commands и прикладными
sessions/workspace. Main/CLI/App composition и выбор host принадлежат consuming
executable; отдельный game/runtime consumer может использовать Aether без Editor.
Подробные этапы/решения/acceptance находятся только в
[ARCH2a–e](PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27), ED1–ED8 сохраняют
пользовательские цели и становятся развитием подключаемого редактора.

До записи решения прочитаны общие PLAN/STATUS/PLANNING, engine/Editor/UI plans и
statuses, текущие csproj/Program/EditorApplication/RuntimeEntryPoint/NativeRuntime.
Наблюдения: Aether уже library/packable; Editor WinExe/IsPackable=false, тот же
executable dispatches --runtime; EngineChild добавляет этот argument; HostId
fingerprint включает entry assembly; Editor font content ссылается на ManualTests.
Эти связи — задачи перехода, не исправленные текущей документационной работой.
Устаревшая запись общего package плана о deferred Aether уточнена по current source;
свежие MSBuild metadata/pack проверки не выполнялись.

Согласованы App/Cobalt registrations через планируемый UseAetherEditor, explicit
View factories/owners и standard/embedded host use. Общие engine formats/operations
доступны headless; Editor прикладные services не перемещаются массово в Aether.
Concrete App.Build/Run остаётся в executable; у Aether не появляется bootstrap/UI
dependency. Нужны explicit runtime executable/args и согласованный counterpart
identity contract, с сохранением same-build/session/nonce/generation fences.
Package resources/лицензии и independent NuGet consumer не зависят от source checkout.

Очередь изменена: ARCH2a APIs/ownership → ARCH2b/c library/runtime boundary →
ARCH2d hosts/resources → ARCH2e fresh package/source/published acceptance;
затем ED6a/ED8a console/export, ED4c.2/3 и project/build/play workflow. Общий
World/ECS/generator/MMF/UI beta не prerequisites; hardware/quality blockers и
предыдущие limits сохраняются.

Изменены только планы/статусы/навигация. Code/API/csproj не менялись; build/tests/
pack/publish/native не запускались. Предыдущие4285/4285 Debug/Release относятся к
child diagnostic delivery и не подтверждают ещё не реализованный ARCH2.
Документационная проверка:12Markdown files, local links/anchors, balanced fences
и git diff --check HEAD; вне docs изменений нет.
[Отчёт и hashes документов](../../TestResults/EditorPackagesPlan-20261027-1/verification.json).

## Child diagnostic delivery — 2026-10-27

ENG8a.3/ENE6d.4 после outcome/wait metrics: child poll-читает bounded Core ring,
отправляет один batch по отдельному typed Enerit endpoint, host импортирует в
обычные ring/sinks. Pixel/control/Mithril queues не используются для logs.
[Согласованный контракт](PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27),
[подключение/API/budgets](DIAGNOSTICS_DELIVERY.md).

Shared Contracts добавляет reader/importer и additive Origin без изменения старого
DiagnosticRecord constructor. Aether зависит от contracts, без новой зависимости
на TrueMoon bootstrap. Copied launch options и App/Cobalt factories задают delivery
products; low-level старые constructors остаются local-only. Lease владеет receiver
до child reap/raw drain, runtime владеет sender после game/control cleanup.
Cancelled/timeout wait сохраняет actual cleanup owner для retry.

Version/count/depth/size и все rows проверяются до import; exact session/generation/
nonce/host/game/role/PID fences, bound child appid и monotonic sequences не дают
смешивать generations. UInt64 generation переносится exact bits существующим
Int64 scalar: первый build с UInt64 дал TME001; выбран additive DTO long, generator
не менялся. Host назначает собственные appid/sequence и authoritative context,
сохраняет child origin/PID/time/trace. Import не re-emits DiagnosticListener;
sender подавляет Origin records, сама lane не публикует events/traces/metrics.

Known gap/size/rate/pending drops считаются Lost, unknown ACK outcome — Unconfirmed;
replay отсутствует. Sender имеет собственный per-call CTS и PendingRequestCount
guard даже с custom factory50s/64slots. ACK проверяет status/count/exact last payload
sequence. Rate учитывает JSON+1024bytes до parse, включая malformed body.
Stop фиксирует конечный source cursor; принудительная отмена tail видна в
DrainComplete. Local JSONL и continuous stdout/stderr сохраняются независимо.

Новые120Aether cases/38methods и59Core cases/18methods; всего179.
[Полная Requirement | Evidence matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-matrix.md),
[quality/gap review](../../TestResults/DiagnosticsDelivery-20261027-1/tests-quality.md),
[Core matrix](../../TestResults/DiagnosticsDelivery-20261027-1/tests-core-matrix.md).

| Требование | Точные тесты, TrueMoon.Aether.Tests.Diagnostics |
| --- | --- |
| Whole-packet validation, identities и monotonic fences | ChildDiagnosticReceiverTests.InvalidRow_RejectsTheWholePacketBeforeEvenTheEarlierValidRowIsImported; ForeignGenerationIdentity_IsRejectedBeforeImportAndDoesNotBindTheApplication; ApplicationPacketAndRecordFences_RejectStaleDuplicatesWhileLossHeartbeatsRemainMonotonic |
| Missing/malformed ACK без replay, own deadline и finite capacity | ChildDiagnosticForwarderTests.MalformedAck_MakesBatchUnconfirmedAndNeverReplaysIt; OwnRequestDeadlineAndPendingGuard_BoundAnInjectedClientWithLargeCapacityAndTimeout |
| Count/byte/rate/codec boundaries и no feedback | ChildDiagnosticLimitsTests.ReceiverWireBounds_AllowExactSizesAndNeverPublishOversizedResponse; RateBudget_IncludesEnvelopeAcceptsExactBoundaryAndRecoversInANewWindow; ChildDiagnosticForwarderTests.SerializationBounds_ClipSafelyRemoveSecretKeysAndKeepFreeText |
| Both DI, ownership/rollback/cleanup и independent control | ChildDiagnosticFactoriesTests.AppFactories_ExchangeUsingOptionalCoreAccessAndCopiedOptionsWithCallerOwnedProducts; RuntimeHost_OwnsSenderAndDrainsItAfterPreviewGameCleanup; RuntimeHost_DeliveryFactoryFailureRollsBackGameAndControlWithoutChangingCause; BlockedDiagnosticImporter_DoesNotBlockIndependentControlConnection |
| Finite drain и timeout не освобождает чужой/исполняющийся ресурс | ChildDiagnosticForwarderTests.StopCapturesOneFiniteTargetAndDoesNotChaseNewerReaderRecords; BlockedBorrowedReader_TimesOutOnlyTheStopWaitThenReleasesOnRetry; ChildDiagnosticReceiverTests.BlockedImporter_RejectsConcurrentPacketsAndPreservesOwnedTransportAcrossTimeoutAndCancelledStopWait |
| Actual source child flood/control/JSONL/raw/reap | ChildDiagnosticProcessTests.ThreeActualGenerations_ForwardProvenanceAndLossWhileControlAndLocalFilesSurviveFloodAndBrokenSinks |

Source proof использует реальные NativeRuntimeFactory, public UseAetherPreviewRuntime
и RuntimeHostFactory:3Preview children одной session, разные PID/appid,33control
commands на generation (Hello/LoadScene/GetStatus/SetMode(false)/Stop), slow sink
drops и throwing sink failures. Preview не поддерживает Step, game artifact/input/
GPU surface не открываются. На сохранённом отдельном запуске receiver принял12rows
на generation; host bounded history сохраняет4, child reported loss1741/1224/1237.
Все3child reaped, lease/receiver released.29raw/report/JSONL files:
[process evidence](../../TestResults/DiagnosticsDelivery-20261027-1/process-agent-debug/delivery-report.json).
Final full suites также выполняют этот process test в Debug/Release.

Непрошедшие попытки сохранены:
- Initial build: TME001 UInt64 и отсутствующие generated types, исправлен DTO scalar.
- Sender initial24/25: cold pipe не успевал к20ms barrier; обычный test budget500ms
  против injected50s сохраняет проверку own deadline/pending guard.
- Fixture compile: неверный TryResolve на IServiceProvider заменён GetService.
- DI116/118: неверное ожидание Completion unstarted Mithril handle убрано;
  PID/no-receiver/caller ownership остаются проверены.
- Process118/119: existing synchronous Console.In segment в RunAsync откладывал flood
  до Stop; fixture запускает runtime worker через Task.Run. Production не менялся.
- Первый full attempt прерван PowerShell NativeCommandError при stderr xUnit.
  Скрипт теперь собирает stdout/stderr до exitcode, attempt1 сохранён. Whole-Core RED
 372/373 подтвердил process-global event interference; App test изолирует свой origin
  и relative cursor, сохраняя normal routing. Whole-Core rebuilt GREEN373/373.
- Второй full Debug4283/4285:2InvalidRow cases прошли assertions, но Dispose timeout
  ordinary fixture200ms. Test-only deadline2s, специальные50ms timeout cases и
  min/max boundaries сохранены. Targeted23/23 и wholeAether1338/1338 прошли до
  окончательного full. Failed attempt2/10TRX/log/binlog/source hashes сохранён.

Final validation SDK10.0.403, xUnit/VSTest, net10.0:
Debug nonincremental build0errors/1546warnings; Release incremental0errors/1316warnings.
Core373, Cobalt179/generator91, Enerit343/generator114, Mithril99,
UI893/Hosting114, Aether1338, Editor741 — **4285/4285 в каждой конфигурации**,
failed/skipped0. Все179newcases passed в обеих конфигурациях.
[Machine verification](../../TestResults/DiagnosticsDelivery-20261027-1/verification.json):
1020source hashes, source changes0,20test-binary hashes,44TRX/24binlogs включая
неуспешные попытки. Pre-existing Microsoft DI10.0.14 сохранён по SHA256.

Команды финального профиля (точные arguments — validation-steps.json):
```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsDelivery-20261027-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsDelivery-20261027-1/Debug -bl:TestResults/DiagnosticsDelivery-20261027-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsDelivery-20261027-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsDelivery-20261027-1/Release -bl:TestResults/DiagnosticsDelivery-20261027-1/tests-Release.binlog
```

Это первый bounded profile, не весь ENG8a/ENE6d/ENE6c. GPU/display, gameplay/input,
published/package, arbitrary parent containment, long trends/performance не
повторены. Исторические CPU FrameReceiver и Mithril ready-file races не исправлялись.
Следующий diagnostics consumer — ED6a/ED8a bounded console/export, затем command/
frame metrics; authoring ED4c.2 → ED4c.3/workflow и hardware gates прежние.


## IPC trace context в control factories — 2026-10-26

UseAether и UseAetherRuntime (включая PreviewRuntime) внедряют borrowed telemetry и
PropagateTraceContext=true в штатные generic pipes client/server factories.
CaptureRequestEvents сохраняет false; standalone прежних signatures оставляет
legacy defaults. Factory override и App lifecycle/ownership сохраняются.
Same-build HostId уже включает Enerit assembly; generator/service payload прежние.
[API](../../TrueMoon.Aether/README.md#диагностика),
[wire и compatibility](../enerit/DIAGNOSTICS.md).

Две новые cases
AetherPipesTraceTests.AppGenericEngineControlFactories_PropagateTraceWithoutActivatingEngineOrRuntime
на Cobalt/MS DI создают App и реальные localpipe products через штатные factories.
Точные root/client/server IDs/kinds/remote parent, Current через await, scopes и
borrowed/App-owned lifetime проверены. Guard factories подтверждают отсутствие
активации host/runtime/frame/GPU/child; это не native engine process proof.

Final narrow2/2; Aether1218/1218 Debug и1217/1218 Release, Editor741/741 в обеих
конфигурациях. Release повторил прежний ENG2
FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain:
null из TakeLatest, Assert.NotNull line177 после Received>=5. CPU receiver и его тест
этим срезом не менялись. Claim pending до CopyLatest остаётся source inference;
нужен deterministic reproduction и отдельный ownership fix. Повтор для green не
делался; этот failure не скрыт за успешными100новыми cases или Debug4021/4021.
Release full4020/4021, failed1/skipped0. Builds0errors:
Debug nonincremental1530warnings, Release incremental1300warnings, SDK10.0.403.
[Полная matrix/команды](../enerit/HISTORY.md#ene6d1-trace-context-через-pipes--2026-10-26),
[evidence](../../TestResults/DiagnosticsIpc-20261026-1/verification.json).

Отдельный source Enerit generated-proxy/child trace proof выполнен full suites;
native Aether/published/GPU/package/performance acceptance не повторялись.
ENG8a частичен: далее IPC/command/frame aggregates, bounded child delivery и
ED6a/ED8a console/export. ED4c.2 → ED4c.3/workflow и прежние hardware gates прежние.

## Операции и трассы диагностики — 2026-10-26

Продолжение «давай следующий шаг»: первый ENG8a.1/3 operation consumer поверх
[Core CORE6d](../core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26).
UseAether передаёт borrowed events/telemetry через существующие factories,
старые standalone constructors сохранены. Новых DI aliases/owners нет.

Build scope включает reap/drain/cleanup, terminal log и measurement предшествуют
TCS completion. Replace scope включает recovery; terminal log сохраняет original
cause, phase, rollback flags и отдельный RecoveryErrorType. RecoveryResult имеет
отдельный error snapshot. Failed Replace остаётся Failed при successful rollback.
Preview.Show/Resize ждут acknowledged first-present; Stop scopes включают cleanup.
FrameFault всегда Failed и фиксирует старые identity/surface до drain, отдельный
FaultCleanupFailed сохраняет обе причины. Presented callbacks не добавляют logs/scopes.
[Схема событий и IDs](../../TrueMoon.Aether/README.md#диагностика).

| Требование | Точное доказательство в AetherTelemetryTests |
| --- | --- |
| Реальный compiler success/failure/cancel и terminal before TCS | Build_EndsCorrelatedScopeBeforeReturningResultFailureOrCancellation |
| Replace success/cancel/expected identity | Replace_SuccessAndPreflightCancellationPreserveExpectedIdentityAndOperationOutcome |
| Original/recovery причины, successful/failed rollback и correlation | Replace_RecoveryFieldsAndDistinctCausesRemainCorrelatedToOriginalFailure |
| Preview first-present/resize/stop, обе DI, отсутствие per-frame logs | Preview_ShowResizeAndStopCompleteOnlyAfterActualPresentationAndDoNotLogEveryFrame |
| Preview error/cancel и cleanup retry | Preview_FailureAndCancellationKeepOriginalOutcomeAndCloseTheOperation; PreviewStop_CleanupFailureIsReportedAndRetainedUntilSuccessfulRetry |
| Frame fault до/после presentation, metadata до drain и cleanup failure | FrameFault_CapturesPreDrainIdentitySurfaceAndOriginalFailedOutcome |
| Ошибающиеся injected diagnostics не меняют результат/exception | InjectedTelemetryFailure_DoesNotReplaceBusinessResultOrOriginalException; SafeOperation_OnlyCompletesAndDisposesBorrowedScopeOnce |

[Тестовый source](../../TrueMoon.Aether.Tests/AetherTelemetryTests.cs).
Новые25Aether cases; narrow32/32 включает прежние7diagnostics cases.
После найденной full-suite default-App registration regression Core добавил2cases,
Core narrow56/56 и полный Aether Debug1216/1216 прошли. Финальный full solution:
**3921/3921 Debug и3921/3921 Release**, failed/skipped0; Aether1216,
Editor741, Core308 в каждой конфигурации. SDK10.0.403, builds0errors:
Debug nonincremental1530warnings, Release incremental1300warnings.
[Все команды/initial failures](../core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26),
[verification](../../TestResults/DiagnosticsTelemetry-20261026-1/verification.json)
содержат34TRX/12binlogs и hashes. Нового native/published/GPU/package/performance
proof нет; текущие passing не закрывают прежние races или hardware acceptance.

ENG8a остаётся частичным: следующий срез — ENE6d IPC propagation, затем
Start/Stop/Play/command producers, frame/queue/latency aggregates и live delivery/
Editor console/export. Authoring очередь ED4c.2 → ED4c.3/workflow не изменена.

## Managed logger runs в Editor и Runtime — 2026-10-26

EditorApplication включает Core managed JSONL: normal root
%TEMP%/TrueMoon/Diagnostics/Editor, smoke report/diagnostics/editor.
RuntimeEntryPoint использует report/diagnostics/runtime, новый GUID run на
logger lifetime; quotas/leases/emergency принадлежат Core.
Reports/artifacts/raw output и legacy непомеченные каталоги отдельно.
Source wiring compiled; managed Aether1191 и Editor741 Debug/Release прошли,
Core252/full solution3840/configuration в финальных runs, failed/skipped0.
Initial Debug повторил Mithril race и Core test-isolation defect; последний
исправлен только в тесте. Новый manual native/published/GPU/performance/package
proof не запускался. [Матрица и команды](../core/HISTORY.md#emergency-и-managed-jsonl-runs--2026-10-26).

## Диагностика сборок: ограничение хранения — 2026-10-26

Продолжение ENG8a.2: GameBuildRetentionOptions фиксируется при UseAether setup и
при low-level создании GameBuildCompiler. Default — 16 завершённых jobs, 64 MiB
суммарно с ownership marker и 7 суток от завершения cleanup. Опубликованные
content-addressed игровые artifacts не входят в этот budget и не удаляются.

Перед запуском компилятора и после reap/drain/удаления work/output выполняется
best-effort sweep. Новые GUID jobs получают versioned .build-diagnostics marker;
эксклюзивная lease удерживается до окончания cleanup. Общая .retention.lock
согласует cleaners разных экземпляров/процессов без ожидания занятой lease.
Завершённые каталоги сортируются по completion UTC и ordinal имени при равенстве;
удаляются старейшие сверх count/bytes/age. Равенство лимиту допускается.

Последний завершённый job в корне и явно защищённый текущий job сохраняются,
даже если лимит невозможно выполнить. RetentionStatus сообщает retained bytes/
jobs, active/skipped/deleted, failures, bounded LastError, IsOverBudget и
MaintenanceBusy. Warning Build.RetentionLimited/1205 содержит JobId и counters;
сбой retention не подменяет успешный result или исходный GameBuildException.

Удаление строго нерекурсивное: только GUID job с корректным completed marker и
плоским набором build.stdout.log/build.stderr.log/build.output.json. Root/jobs/
job ancestry и entries проверяются на reparse points. Legacy unmarked jobs,
незавершённые/повреждённые markers, неизвестные файлы и abandoned staging после
crash сохраняются и отражаются отдельно. Поэтому это budget управляемых
завершённых diagnostics, а не обещание общего размера artifact root.
После блокировки файла marker сохраняется; следующий sweep повторяет cleanup.
Обслуживание запускается при сборках, фонового таймера нет; hostile внешняя
подмена путей между проверкой и удалением вне trusted-local/cooperating scope.

| Требование | Evidence: точное имя теста |
| --- | --- |
| Count и порядок по завершению, стабильное равенство дат | Sweep_CountLimitDeletesOldestByCompletionInsteadOfDirectoryMtime; Sweep_EqualCompletionTimesUseOrdinalDirectoryTieBreaker |
| Суммарные bytes с marker: меньше/равно/больше границы | Sweep_ByteBudgetIncludesMarkerAndRetainsExactEquality |
| Age строго сверх лимита, нормализация UTC | Sweep_AgeExpiresOnlyStrictlyAfterCompletionLimit |
| Newest/explicit protection и недостижимый count/bytes/age budget | Sweep_ProtectsNewestAndExplicitJobAndReportsUnsatisfiedBudget |
| Active job защищён от другого store, затем становится eligible | Sweep_ProtectsActiveJobAcrossStoreInstancesUntilItCompletes |
| Root contention без ожидания и retry | Sweep_MaintenanceContentionPreservesJobsAndRecoversWithoutBlocking; Sweep_MaintenanceFailureIsObservableAndCanBeRetried |
| Неизвестные/legacy/incomplete/nested jobs и artifacts сохранены | Sweep_PreservesUnknownIncompleteAndNonflatDirectoriesWhilePruningOwnedJobs; Sweep_NeverInspectsOrDeletesPublishedArtifactsOutsideJobs |
| Reparse root/jobs/job/entry не затрагивает eligible external archive | Sweep_RejectsReparseAncestryAndEntriesWithoutTouchingTargets |
| Failed delete сохраняет ownership, остальные jobs очищаются, retry проходит | Sweep_DeleteFailurePreservesOwnershipContinuesAndRetriesAfterHandleRelease |
| Options копируются, невалидные limits отклонены до IO | Store_CopiesAllRetentionLimitsBeforeCallerMutatesOptions; InvalidRetentionLimits_AreRejectedBeforeCreatingDirectories |
| App Cobalt/MS DI: success → failure сохраняет последний job и game artifact | AppBuildRetention_SnapshotsOptionsRetainsLatestFailureAndPreservesPublishedArtifact |
| Retention failure не заменяет success/compiler error, Warning1205 связан с JobId | Compiler_RetentionDeleteFailureDoesNotReplaceSuccessOrOriginalBuildError |
| Пустой store не создаёт архив | Sweep_EmptyStoreDoesNotCreateArchiveOrReportLoss |

Добавлены 42 cases (38 unit + 4 actual compiler/App), узкий Debug42/42.
Полные Aether1191/1191 и Editor741/741 в Debug/Release; всего solution3767/3767
в каждой конфигурации, failed/skipped0, 20TRX. Build Debug nonincremental и
Release incremental: 0errors/1530warnings каждый. SDK10.0.403, Windows x64/net10.0.
Source fingerprints968, tested binary fingerprints20 и 4 binlogs сохранены в
[verification.json](../../TestResults/DiagnosticsRetention-20261026-1/verification.json).
Исторический TestResults/Diagnostics-20261025-1792916778832 в текущем workspace
отсутствует; прежние ссылки сохраняют исходные пути. Его native/published
результаты ниже являются исторической записью, а не проверкой этого запуска.

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v:quiet -m -bl:TestResults/DiagnosticsRetention-20261026-1/build-Debug.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug -v:minimal --logger trx --results-directory TestResults/DiagnosticsRetention-20261026-1/Debug -bl:TestResults/DiagnosticsRetention-20261026-1/tests-Debug.binlog
dotnet build TrueMoon.slnx --no-restore -c Release -v:quiet -m -bl:TestResults/DiagnosticsRetention-20261026-1/build-Release.binlog
dotnet test TrueMoon.slnx --no-build --no-restore -c Release -v:minimal --logger trx --results-directory TestResults/DiagnosticsRetention-20261026-1/Release -bl:TestResults/DiagnosticsRetention-20261026-1/tests-Release.binlog
```

Это новые managed/SDK-process проверки; отдельный manual native/published/UI/
hardware/package/performance run в этом срезе не выполнялся. Исторические
Mithril ready-file и CPU FrameReceiver races не объявлены исправленными.
ENG8a.1/2 и CORE6 остаются частичными. Далее — Core emergency output и age/total
directories/runs retention, дополнительные engine events, затем traces/metrics/
IPC context и ED6a/ED8a console/export; очередь authoring/editor не менялась.

## Единая диагностика: первый профиль — 2026-10-25

Первый consumer CORE6 и ENG8a.1/2: UseAether/UseAetherRuntime используют injected
sources; session/process/build events имеют stable IDs, context и exception
snapshots. EditorApplication и runtime подключают App-owned JSONL pipeline.
Protocol/wire/session authority не менялись; Microsoft.Extensions.Logging не добавлен.

Runtime stdout/stderr — live32KiChar tails и четыре fixed file slots на поток,
каждый≤1MiB UTF8. Process diagnostics inspection≤256records/64leases, supervisor
остаётся owner. Compiler использует общий ProcessOutputCapture и build.output.json
с loss/error status; прежний exit code/DiagnosticsDirectory сохранён.
[API](../../TrueMoon.Aether/README.md#диагностика),
[CORE6 contracts/limitations](../core/DIAGNOSTICS.md).

| Требование | Evidence |
| --- | --- |
| State/scene/tick/generation correlation + legacy callback | SessionTransitions_AreCorrelatedAndPreserveConfiguredObserver, Cobalt/DI |
| Исходная launch error и Error/Phase metadata | SessionLaunchFailure_PreservesOriginalCauseAndPublishesErrorWithSessionIdentity, Cobalt/DI |
| Build job events + drained raw metadata | AppCompiler_ReportsSuccessfulJobAndPersistsDrainedOutputStatus, Cobalt/DI |
| Compile failure сохраняет logs/status/cause | AppCompiler_ReportsCompilerFailureAndRetainsTextAndOutputMetadata |
| Реальный own child/двойной flood/retention/cleanup | EngineSessionProbe diagnostics:6generations, each2 113 547chars/stream без newline, live tails≤32768chars,8files≤1MiB, Step/Stop, затем launch-failure, structured108records/written108/no sink losses и reap |

Новые Aether7cases прошли отдельно и в полных Aether1149/1149 Debug/Release;
Editor741/741 в каждой конфигурации. Full solution3725/3725 Debug и3725/3725 Release,
failed/skipped0,20TRX. Build0errors/1530warnings, Debug nonincremental.
Результаты Core/Cobalt/Enerit/Mithril/UI сохраняют собственные assembly counts
в [verification.json](../../TestResults/Diagnostics-20261025-1792916778832/verification.json).

Canonical process proof: current Debug apphost и published Release, скопированный
в свежий OS temp и запущенный с working directory там же; по6started children,
12reaped total, плюс по1expected launch failure без child. Дополнительный published
Release run внутри evidence тоже прошёл. До копирования опубликован один current
artifact; top-level payload hashes копии совпали. Source/binary fingerprints и
outside location записаны в verification.json/outside-location.json.

```powershell
dotnet publish ManualTests/EngineSessionProbe/EngineSessionProbe.csproj --no-restore -c Release -o TestResults/Diagnostics-20261025-1792916778832/publish -v:quiet -bl:TestResults/Diagnostics-20261025-1792916778832/publish-Release.binlog
./ManualTests/EngineSessionProbe/bin/Debug/net10.0/EngineSessionProbe.exe diagnostics TestResults/Diagnostics-20261025-1792916778832/native-final-Debug
```

Published executable выполнялся в OS temp; exact directories и captured report
указаны в [location](../../TestResults/Diagnostics-20261025-1792916778832/outside-location.json) и
[native report](../../TestResults/Diagnostics-20261025-1792916778832/native-outside-Release/diagnostics.json).
Общие build/test команды — в [Core history](../core/HISTORY.md#структурированная-диагностика-первый-профиль--2026-10-25).

Remaining: build jobs total/age retention, additional Replace/preview/frame event
coverage, cross-process trace context, spans/metrics, UI console/export и full
fault/long-load acceptance. Предыдущие Mithril ready-file/CPU receiver races не
объявлены исправленными новым passing run. Packages/physical/golden/hardware loss/
VRAM/performance/long trends не повторены; alpha readiness не закрыта.

## ED4f.2: системная граница окна — 2026-10-25

Согласованный24октября дизайн реализован и завершён25октября: главное окно включает
общую Hosting option UseSystemAccentBorder. System accent берётся из cached host
capability IWindowSystemColors, отсутствие цвета возвращает текущий Theme.Border.
Inactive RGB составляет60% выбранного цвета, focus transition160мс обратим; alpha/
толщина/viewport/foreground прежние. Explicit BorderColor local/style имеет приоритет.
Generic Windows/API и WinRT resources находятся в Platform.Silk; App/Cobalt/IFactory
путь и единственный window owner прежние. Default frames других consumers opt-out.

UISettings.GetColorValue(UIColorType.Accent) вызывается через проверенный по Windows
SDK10.0.26100.0 IUISettings3 ABI. Используются временные SafeHandle HSTRING/COM refs,
balanced RoInitialize(MTA/STA)/RoUninitialize; неуспех/API absence даёт null.
Native WM_SETTINGCHANGE/WM_THEMECHANGED/WM_DWMCOLORIZATIONCOLORCHANGED только ставят
dirty flag. Чтение и render request происходят после event pump вне WndProc, не
на каждом кадре. Аргумент цвета сообщения не принимается за текущий Windows accent.

Текущие проверки в TestResults/AccentBorder-20261024 (имя evidence-root сохранено
со времени начала задачи; завершение по локальной дате25октября):

- 15новых xUnit cases/8methods: UI7, Hosting8. Narrow51+16=67passed/0failed/0skipped:
  narrow-185021842, RunNarrow.ps1. Проверены accent/update/null/recovery, unsupported
  host, RGB60%/159–160мс/reversal/detach/idle/opt-out, theme/style/local precedence,
  immutable snapshot и ранняя validation до регистрации/activation.
- Related UI893/Hosting114/Editor741 в каждой Debug/Release:1748/configuration,
  3496passed/0failed/0skipped. TRX/log/binlog: managed-185909752. Все15новых cases
  отдельно найдены в обеих конфигурациях; тесты вручную перечитаны на силу assertions.
- Последний nonincremental Debug solution build0errors/1532warnings:
  solution-build-185934385/build.log и build.binlog. Ранее CS9191 указывал на
  эквивалентный ref/in argument; заменён на in, полный проход повторён.
- 12native source Debug/published Release cases: native-185940301/summary.json,
  206PNG/16reaped children,validation0errors/0warnings и zero owned UI sessions/
  mappings/GPU resources/handles. Новый border.json проверяет настоящий UISettings
  accent, refresh/render после directed WM_THEMECHANGED, owned HWND focus messages,
  неизменный viewport и actual border pixels с RGB tolerance1. На этой Windows
  active #04827D → inactive #024E4B. Финальные active/inactive PNG просмотрены.

Directed сообщения отправляются только собственному HWND на owner thread; исходное
наблюдаемое состояние восстановлено. Global SetFocus/cursor/Windows personalization
не меняются. Реальная пользовательская смена системного accent и физическая смена
фокуса не заменяются этим proof. Native HRESULT/loader errors не fault-injected;
null-provider fallback/recovery проверен напрямую, native cleanup/ABI проверены в
указанном supported desktop scope. Общая alpha/K7/K8/VRAM не закрыта.

Итог и свежие source/binary/PNG/test SHA256:
TestResults/AccentBorder-20261024/verification.json. Предыдущий native-185439428
прошёл12cases до правки ref/in и не считается итоговым binary proof. Первый narrow
не компилировался из-за test syntax/неподдерживаемого Select; исправлен через
реальные keyboard selection gestures, final passing narrow указан выше.
Aether/full solution tests/прочие модули/packages/performance не повторены.
Следующий engine шаг прежний: ED4c.2 names/order → ED4c.3 create/delete.

## ED4f.1: прозрачный статус-бар — 2026-10-24

По согласованному продолжению ED4f нижняя панель28 остаётся поверх полного viewport,
idle фон имеет слабый tint/sigma0; hover/keyboard focus проявляют PanelBackdrop
за160мс. Foreground и размеры/положение сцены не меняются. Ошибки, busy/waiting и
собственные details/Preview popup удерживают подложку. Смена темы/null fallback,
overflow, отступ24 под resize grip16 и прежние команды/данные/IFactory/ownership сохранены.
Оформление opt-in задаётся в EditorStatusBarViewStyle; presentation не знает тему.
Подсветка гибкой summary button ограничена иконкой/текстом вместо всей ширины.

Общий StatusBar.OverlayContent/KeepBackdropVisible принадлежит UI. В обоих frame
participants focus return выполняется только при собственном pending overflow,
поэтому animation frames не забирают focus из сцены или popup. Нет timers, resolution
на кадр или новых сервисов; detach/disable/dispose завершают переход.

Текущие проверки:

- 10новых cases/7methods: shared UI7, Editor3. Узкий запуск27+66=93passed,
  failed/skipped0: TestResults/StatusOverlay-20261024/narrow-180739181, RunNarrow.ps1.
  Проверены159/160мс, reversal/idle/focus/activity/teardown, Dark/Light/null и raster
  blur/sharp foreground; actual editor busy/error/ready и popup owner/focus.
- Related UI886/Hosting106 Debug/Release: managed-180831425. Финальные Editor741
  Debug и741 Release: editor-final-181252516. Итого1733/configuration,3466passed/
  0failed/0skipped; все10новых cases найдены в обеих конфигурациях. TRX/log/binlog
  лежат в этих папках TestResults/StatusOverlay-20261024.
- Последний nonincremental Debug solution build0errors/1532warnings:
  editor-final-181252516/build.log и build.binlog. Предыдущий build1533warnings
  содержал новый xUnit2031; test-only запись Assert.Single исправлена, Editor и
  solution повторены. Runtime source/binary hashes после этого совпадают с native.
- 12native source Debug/published Release cases: native-180910307/summary.json,
  182PNG/16reaped children,validation0errors/0warnings, zero owned UI sessions/
  mappings/GPU resources/handles. Footer idle/hover поверх actual scene, Light
  popup и opaque fallback просмотрены. Native-180005688 прерван после11cases без
  финального inspector Release teardown report; он исключён из итогового passing.

Итог и актуальные SHA256: TestResults/StatusOverlay-20261024/verification.json.
Focused assertion/gap review выполнен без empirical mutation/coverage metrics.
Outputs в C OS TEMP, evidence в ignored TestResults; OS/driver settings не менялись.
Aether/full solution tests/packages/physical DPI/input/performance/VRAM не повторены.
Fixed-camera640×360 transport/aspect-fit letterboxing прежние; alpha/K7/K8 и
CPU FrameReceiver/Mithril quality issues остаются открытыми. Очередь ED4c.2 → ED4c.3.

## ED4f: viewport под оконным chrome — 2026-10-24

По согласованному дизайну удалена постоянная полоса scene path/title/root. Editor
включает общий WindowFrame.TitleBarOverContent через Hosting composition; viewport
занимает внутреннюю клиентскую область под caption36 и footer28. Островки остаются
ниже caption с видимым отступом16. Hover/menu/path prompts не меняют scene bounds;
frame.Content используется вместо предположения о Children[1]. Default consumers
сохраняют прежний layout. View/Data/Style, App/Cobalt/IFactory и borrowed ownership
сохранены; новых service locator/scopes/services не добавлено.

В caption добавлены scene name/dirty/activity, полный путь доступен через Tooltip.
Tooltip ограничен560logical по тексту, переносит явные переводы строк и длинные
Unicode paths по grapheme boundaries; использует MenuBackdrop и opaque fallback.
Title backdrop меняется за160мс от слабого tint/sigma0 к PanelBackdrop без fade
foreground; hover работает в inactive окне, focus/menu удерживают active фон.
PopupInsets оставляет transient меню/Tooltip ниже caption без сдвига content.
Win32 передаёт HTCAPTION move/leave в UI, сохраняя native default dispatch.

Open/SaveAs/first Save открывают retained contextual path prompt без IO до явного
подтверждения. Path draft, controls и return focus сохраняются; Escape/outside click
отменяют запрос. Pending Save/Discard/Cancel не теряется при outside click; errors
можно закрыть и повторить, в том числе с тем же текстом. Обычный named Save остаётся
прямым. Hidden tools не резервируют высоту. Ctrl+S/W маршрутизируются root observer
даже без focused control; прочие text-editing shortcuts остаются scoped. Runtime/
preview admission, общий document/history и terminal close policy прежние.

Текущие проверки после финального Tooltip fix:

- 84новых xUnit cases/45methods в7files: UI37, Hosting8, Editor39. Narrow TRX и
  research/plan/assertion/static-gap review сохранены в OS TEMP
  TrueMoon-OverlayTests-845332f1c973409b841c1614f7915999. В итоговых TRX отдельно
  проверено присутствие всех84cases в Debug и Release; empirical mutation/coverage
  metrics не заявляются.
- Related full UI879/Hosting106/Editor738 Debug и столько же Release:
  1723/configuration,3446passed/0failed/0skipped. TRX/log/binlog:
  TestResults/OverlayChrome-20261024/managed-160136478.
- Nonincremental Debug TrueMoon.slnx build:0errors/1532warnings. Log/binlog:
  TestResults/OverlayChrome-20261024/solution-build-160200516. Outputs в уникальном
  C OS TEMP из-за малого свободного места E; настройки SDK/driver/OS не менялись.
- 12native source Debug/published Release profiles: empty, legacy, documents,
  transform, hierarchy, inspector.166PNG,16reaped children,validation0errors/0warnings,
  zero owned UI sessions/mappings/GPU resources/handles после teardown. Report:
  TestResults/OverlayChrome-20261024/native-160207207/summary.json. Source/binary/PNG
  SHA256 сверены; финальные idle/hover/Tooltip/path prompt и темы просмотрены.
- Новый owned-HWND probe проверяет WM_NCHITTEST=HTCAPTION и доставку координат
  WM_NCMOUSEMOVE до UI, затем owner-driven160мс переход и leave. Глобальный cursor/
  focus не перемещается; actual timed transition отдельно проверен UiSession и
  unit clock. Это directed native bridge proof, не physical Snap/drag acceptance.

В ранних попытках native held-hover не успокаивался из-за повторной render
invalidation; unchanged-target guard исправлен и закреплён двумя active/inactive
cases. Скорректирована inactive-hover admission. Native caption probe отделён от
положения реального курсора, а auto-confirm smoke helper ограничен document actions
после обнаруженного обращения к disposed UiSession при Exit. Первые green12native
не приняты как итог Tooltip: визуальная проверка выявила single-line path/CRLF glyph;
после wrapping добавлен Unicode regression и полный проход повторён. Эти попытки
сохранены в TestResults/OverlayChrome-20261024, финальным считается только run выше.

Итог: TestResults/OverlayChrome-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance/VRAM не повторены. Frame transport,
fixed-camera640×360 preview и его aspect-fit letterboxing прежние. Alpha/K7/K8 и
прежние CPU FrameReceiver/Mithril quality issues не закрыты. Следующий шаг ED4c.2
names/order, затем ED4c.3 create/delete; полный ED4/ED5 остаётся открытым.

## ED4e: островки поверх viewport — 2026-10-24

По согласованному дизайну EditorLayout сначала рисует viewport во всей рабочей
области, затем закреплённые обозреватель/инспектор. Видимый отступ16, radius8,
тонкая граница и shadow внутри8px gutter; размеры wrapper264/336 ограничиваются
доступной шириной без пересечения. Высота следует содержимому до предела, ScrollViewer
сохраняется. Убран служебный заголовок VIEWPORT. Parent-owned EditorIsland использует
ScalarTransition160мс, group opacity и slide8px; closing сразу запрещает ввод child,
wrapper продолжает frame participation до скрытия. Detach завершает переход.
Начальное IsOpen выставляется из модели до attach, исключая пустой инспектор на старте.
Темы и hide/show сохраняют retained View/fields/drafts/focus/scroll согласно контексту.
PanelBackdrop использует существующий blur/tint/fallback; fields остаются чёткими.
View/Data/Style scopes и App/Cobalt/IFactory ownership сохранены; новых сервисов нет.

Текущие проверки:67новыхcases (Editor26/UI41), narrow103passed; related UI842,
Hosting98, Editor699 в каждой Debug/Release, всего3278passed/0failed/0skipped.
Nonincremental Debug solution build0errors/1532warnings.12native source Debug/
published Release cases вне checkout,158PNG,16reaped children; validation0errors/
0warnings и zero owned UI sessions/mappings/GPU resources/handles. Все прежние
game/document/Transform/hierarchy/inspector сценарии повторены; actual preview
проверен по pixels/revision, Dark/Light/null material — по retained controls и bounds.
Нативный helper теперь ждёт layout после раскрытия/прокрутки дерева перед pointer click.
Первый полный native запуск был прерван без summary; следующий выявил этот stale
coordinate в documents fixture; финальный12/12 — native-142613712.
Итоговые Dark/Light/opaque preview PNG просмотрены, поля и текст чёткие.

Evidence: `TestResults/Islands-20261024/verification.json`; managed-140938913,
narrow-190757052, solution-build-final и native-142613712 содержат TRX/binlogs/
commands/PNG/JSON/source и binary hashes. Generated bin/obj находятся в C OS TEMP
из-за ограниченного свободного места E:. Tests quality/pseudo-mutation review
статический; coverage и mutation execution не заявлены. Aether/full solution tests,
packages, physical DPI, Vulkan/HUD effect matrix, performance/VRAM не повторены.
Свободные drag/resize/docking остаются отдельным UX этапом; engine очередь прежняя.

## ED4d.1: компактные действия и контекст инспектора — 2026-10-24

По скриншоту заменены текстовые actions: одна строка Apply/Cancel/Undo/Redo/Add,
26×26, reset24×24, Delete рядом с component disclosure. Геометрические Add/Delete
добавлены в конец UiSymbol, старые значения сохранены. Schema choices остаются
подписанными, у Restore видимое имя occurrence рядом с иконкой. Scoped style
задаёт размеры; accessible names содержат назначение и shortcuts.

Inspector использует прежний OverlayHost: Tooltip через500мс, включая disabled,
без смены focus, с непрозрачным Theme.Control и текстом12. Геометрия/clip/selection/
input/scroll/hide/detach/dispose отменяют hint; transient ancestor subscriptions
и host.Closed очищаются. Чужой popup не заменяется. Idle frames после delay нет.

EditorViewModel.IsInspectorVisible следует выбранной сущности; вся retained область
InspectorPanel скрывается, EditorLayout отдаёт viewport колонку и gap. ClearSelection
не меняет документ/history; draft переключаемой сущности отбрасывается по прежней
policy. Снятый выбор сохраняется при новой revision той же сцены. Escape в Tree
сначала отменяет drag; следующий bubble снимает selection. View/Form/Scroll/actions
остаются прежними, динамические property sections после нового выбора актуальны.
Будущее создание — ED4c.3: показ по контексту создаваемой сущности; фиктивная форма
без entity ID или незавершённый creation API не добавлены.

### Текущие проверки

25новыхcases/14methods:16icons/Tooltip и9selection/layout. Narrow181/181,
0failed/0skipped/exit0, `TestResults/InspectorIcons-20261024/narrow-130857356/`:
command.json, inspector-icons.trx/binlog/stdout/exit.txt. Включены156existingcases.
Manual clock доказывает499мс без popup и500мс с popup; Dark/Light live switch
сохраняет тот же непрозрачный popup. Assertion-quality/static test-gap-analysis
проверены; executed mutations/coverage percentage не заявлены.

| Требование пользователя | Точный тест |
| --- | --- |
| «небольшие кнопки с иконками» | `IconActions_UseCompactSemanticSymbolsWhileSchemaChoicesAndRemovedComponentNamesRemainVisible` |
| «Tooltip при наведении» | `Tooltip_DisabledIconAppearsAtExact500MillisecondsWithoutStealingFocusOrKeepingIdleFrames` |
| «не отображать панель инспектора если ничего не выбрано» | `Shell_ClearSelectionReclaimsColumnAndRestoresSameInspectorChromeWithoutCommittingDraftOrRetainingFocus` |
| Возврат после hide и lifecycle | `Tooltip_InputOrContextChangeDisposesOwnedHintWithoutPublishingAnEdit`,8вариантов; `Tooltip_DetachClosesPopupAndReattachmentRequiresFreshHoverWithoutOwningHostOrModel` |

Related suites **UI801/Hosting98/Editor673** в Debug/Release =1572/configuration,
**3144passed/0failed/0skipped**,6TRX в `TestResults/InspectorChrome-20261024/managed-131042124`.
SDK10.0.402/net10.0/xUnit2/VSTest. Commands `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/InspectorChrome-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<name>.trx" --results-directory <run> /bl:<run>/<name>.binlog
-v minimal`. Точные driver scripts — VerifyFinal.ps1/VerifyFinish.ps1, ignored.

Полная nonincremental Debug solution build сначала остановилась на disk-full при
копировании Skia PDB (`solution-build-131108180`), не на C# ошибке. Удалены только
ignored artifacts предыдущего неудачного narrow-130533692, отчёты/исходники сохранены.
Повторный build с `--artifacts-path <unique OS TEMP>` на C: завершился exit0,
**0errors/1532warnings**, `solution-build-131434548`; path сохранён в summary.json.
Passing tests не перезапускались из-за ошибки копирования.

Native final `native-131440978/summary.json`: **12/12** source Debug/published Release
вне repo, empty/legacy/documents/Transform/hierarchy/inspector. **152PNG/16reaped children**,
validation0errors/0warnings; UI sessions/mappings/GPU resources/handles0. В каждом
header5/status4 captures с BottomAligned/ResizeGripClear. Inspector8captures:
empty hidden, disabled Tooltip, Esc hidden-with-scene, icon sections, typed draft,
dependency error, empty entity Add и real actual-presented preview. Source/binary/
PNG hashes сохранены и повторно сверены. Финальные Dark Tooltip/Light component/
hidden viewport screenshots просмотрены; прежние authoring/save/history/preview,
legacy и document workflows прошли в обеих поставках.

Промежуточный native-130050236 не дошёл до окна: acceptance использовал private
header Move/Until helpers; сделаны internal для reuse. Preflight native-130311570
прошёл до финальных visual/lifecycle fixes. Первый narrow177/178 обнаружил stale
hint после hide: popup удалён, delay state оставался и мешал свежему hover. Добавлен
cleanup ancestor visibility/host.Closed; новый narrow181/181 включает регрессию.
Прозрачный фон Tooltip найден на PNG и исправлен до final build/native.

Evidence: `TestResults/InspectorChrome-20261024/verification.json`. Docs/anchors/
namespaces/diff whitespace проверены. Aether/full solution tests/прочие модули/
packages/physical DPI/input/performance/VRAM/final alpha не повторены; исторические
CPU receiver/Mithril races не объявлены исправленными. Далее ED4c.2/3 по плану.

## ED4d: динамический инспектор компонентов — 2026-10-24

По запросу пользователя inspector строит только фактические effective компоненты
и свойства выбранной сущности по exact schema version. Boolean/Int64/finite Number/
multiline String и explicit nullable, наследование/default/локальный источник,
reset, Add/Remove/Restore подключены к общему authoring документу. У пустой сущности
нет искусственных Transform полей. Unknown type отображается по ID с generic
scalar editors. Component ID и property key используются в мутациях, подписи — нет.

`ComponentEditBatch` расширяет ту же ISceneAuthoringSession: Set/Reset properties,
Add/Remove/Restore/ResetRemoval components, expected revision, operation journal,
one atomic publication/history entry, cancellation и общий resolver. Exact kinds,
nullable, dependency/multiplicity, built-in Transform validity проверяются до commit.
Remove local occurrence удаляет addition, inherited — маскирует, очищая instance
property overrides; Undo возвращает прежний patch. Reset локального addition
очищает и override, и исходное explicit data property, возвращая schema default.
Restore и property edits допустимы совместно. Catalogs/definitions неизменны;
существующий JSON v1 сохраняет patches, новый формат не введён.

`EditorInspectorViewModel` — единый owner drafts/history/preview gates для header,
hierarchy/status. Прежний Transform View factory заимствует этот state, compatibility
TransformViewModel оставлен для прямых consumers без второго DI owner. App/Cobalt
регистрирует model/style/IFactory; родитель владеет созданным View, dependencies
borrowed. Поля/секции кешируются по ID/key, обычный refresh/theme сохраняет controls,
focus/selection/scroll. Scalar row отдаёт input оставшуюся ширину; String полноширинный.
Ctrl+Enter/Esc обрабатывает inspector, Ctrl+Z внутри text editor остаётся его undo.
External commit/replaced document сохраняет draft с conflict; stale structural action
отклоняется. IO/runtime/draft/close gates общие. Legacy runtime — readonly.

### Текущие проверки

Windows x64, SDK10.0.402/net10.0/xUnit2/VSTest. **163новыхcases:92engine+71Editor**;
ещё38существующих DI cases адресно мигрированы. Narrow engine92/92 и Editor109/109
без failed/skipped, exact commands/TRX/binlog/stdout/exit0 в
`TestResults/Inspector-20261024/narrow-engine-102423665/` и
`narrow-editor-103718742/`. Assertion-quality и static pseudo-mutation review проверены;
executed mutation score/coverage percentage не заявлены. Найденные gaps закрыты
проверками boundary key128, Restore+Reset и неизменности нетронутого CRLF текста.

| Требование пользователя | Evidence: точный test method |
| --- | --- |
| «динамическое представление свойства выделенной сущности» | `Selection_ProjectsOnlyEffectiveComponentsWithExactSchemasKindsAndProvenance` |
| «показываем редакторы и свойства если они есть у сущности» | `SchemaEditors_UseCheckboxTextAndMultilineControlsAndRemoveSectionsAbsentFromSelection` |
| «отображение и редактирование компонентов (ECS)» | `SchemaChoices_AddCorrectOccurrenceAndApplyScopedStylesToNewDynamicEditors`; `TypedInputs_CommitBooleanNullIntegerAndMultilineTextAsOneTransactionThroughButtons` |
| Общий history/tree и один inspector | `Shell_SelectionGenericDraftAndHierarchyMovesShareHistoryAndUseOnlyOneInspector` |
| Stale action / same-ID document replacement | `StructuralAction_HeadlessCommitOrSameIdentityReplacementRejectsStaleActionUntilFreshAttempt` |
| DI/ownership | `BothProviders_ShareGenericModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedInspectors` |

Полные связанные suites: **Aether1142/UI801/Hosting98/Editor648** в Debug и Release,
**2689/configuration,5378всего,failed/skipped0**. Debug первые3 — `managed-105536563`,
финальный Editor Debug и все4Release — `managed-105853556`. Production этих первых3
между проверками не менялся. Commands — `VerifyFinal.ps1`/`VerifyFinish.ps1` в ignored
`TestResults/Inspector-20261024`: `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/Inspector-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<name>.trx" --results-directory <run> /bl:<run>/<name>.binlog
-v minimal`. Финальный nonincremental `dotnet build TrueMoon.slnx` с тем же artifacts
path, `-c Debug --no-incremental -v minimal /bl:<run>/build.binlog` — exit0,
**0errors/1532warnings**, `solution-build-105946391`. Это4test projects, не full suite.

### Native acceptance и границы

`native-105956351/summary.json`: **12/12** fresh processes, source Debug и published
framework-dependent Release вне repo: empty/legacy/documents/Transform/hierarchy/
inspector. **146PNG/16reaped children**, validation0errors/0warnings, UI sessions/
mappings/GPU resources/handles0. В каждом header5/status4 captures, BottomAligned/
ResizeGripClear. Source/binary/PNG hashes сохранены и повторно сверены.

Inspector обеих поставок: actual typed controls/input, nullable empty-string/null,
Int64.MaxValue, multiline Cyrillic/surrogate-pair text, single transaction Undo/Redo,
reset default7, dependency rejection без частичной публикации, inherited Remove/
Restore, Add к пустой сущности без Transform, SaveAs/Close/reopen с exact IDs и clean
history. Footer запускает реальный preview committed revision; actual presented
stamp и pixels красного куба проверены. Compiler не запускается, fixture source
не изменён, child drained/reaped. Финальные dark/light/empty/component-error/preview,
legacy и documents PNG просмотрены; null label spacing исправлен до final run.
Glyph coverage используемого font не расширялась: emoji хранится точно, на PNG может
использовать missing glyph. Physical OS input/DPI этим directed UiSession run не доказаны.

Промежуточные attempts сохранены и не засчитаны: native-104411704 — конфликт типов
параметра Delivery в evidence runner, native-104737874 — status smoke ожидал старый
Transform View. Исправлены runner и acceptance null guard. native-105120575 прошёл
Inspector Debug preflight. Первый полный Editor Debug647/648 ожидал текст Transform
в hierarchy hint; после перехода к любым property drafts assertion адресно обновлён,
повторный final648/648 в обеих конфигурациях. UI test helpers передают multiline
через Enter events и ждут deferred binding drain; production ввод не подменён.

Evidence: `TestResults/Inspector-20261024/verification.json`, exact commands/TRX/
binlogs/JSON/PNG и scripts ignored. Docs/local links/anchors/new namespaces/diff
whitespace проверены. Full solution tests/прочие модули/NuGet consumers/physical/
performance/VRAM/final alpha не повторены. Старые CPU FrameReceiver/Mithril ready-file
races не объявлены исправленными. Runtime ECS World/storage/scheduler, custom editor
providers, template editing и live runtime transaction не входят в этот срез.
Далее ED4c.2 persistent names/order, затем ED4c.3 create/delete/duplicate.

## ED4c.1: дерево сущностей и Reparent — 2026-10-24

Первый срез по запросу «показ иконок, возможность перемещать и редактировать дерево
и объекты». Общий `TreeView/ITreeView` в Argentis принимает frozen forest, рисует
геометрические Entity/Cube/Interaction и disclosure, поддерживает expansion,
selection, keyboard/reveal/scroll и parent move requests. View/Data/Style Editor
зарегистрированы через App/Cobalt/`IFactory<EditorHierarchyView>`; App владеет
model/style, родитель — созданным View. Подписки снимаются, borrowed зависимости
не освобождаются View. Существующие constructor consumers/legacy flat list сохранены.

`ReparentBatch` расширяет ту же ISceneAuthoringSession, expected revision, receipt
journal, cancellation, atomic resolver validation и Undo/Redo. Новый parent
сохраняет local component values/Transform и definition inheritance; мировое
положение может измениться. Self/cycles/missing targets/duplicates/stale intent
отвергаются без частичной публикации. ParentId уже входит в scene JSON v1, новый
UI-only формат не введён. Guid order сохраняется. Rename/sibling order и
create/delete/duplicate остаются ED4c.2/3.

Editor переносит на сущность или в пустую область дерева/«В корень»; Ctrl+Left/Right
выполняют outdent/indent. Общий выбор обновляет Transform inspector, история
смешивает parent и Transform edits, Save/reopen и committed Edit Preview используют
прежних owners. IO/runtime/exit/Transform draft/conflict закрывают admission.
Каждый committed snapshot отменяет drag. Дополнительно исправлен stale gesture:
Move фиксирует ожидаемый document до Refresh и отклоняет unseen headless commit
или замену документа с теми же IDs. Диагностика read-only, доступна для копирования.
Workspace кеширует семантические nodes; root style не обходит nested View.

### Текущие автоматические проверки

Windows x64, SDK10.0.402/net10.0/xUnit2/VSTest. Новые71test methods/130cases:
engine42, TreeView41, Editor47. Исходные изолированные Debug команды/TRX/log/binlog:
`TestResults/Hierarchy-20261024/narrow-135848457/`, финальные
`engine-reparent-final.trx`, `treeview-draw-final.trx`, `editor-hierarchy-stale.trx`;
все130passed/0failed/0skipped. Проверка assertions и plausible caller-visible
mutations закрыла gaps receipt source comparison, exact cancellation exception,
direct Draw icon routing, equal SetItems gesture cancellation и stale document.
Mutation execution/coverage percentages не заявлены.

| Requirement | Evidence: точный test method |
| --- | --- |
| «показ иконок» | `Draw_DisclosureAndPerItemIconsUseDeclaredSymbolsAndHierarchyDepth` |
| «возможность перемещать» | `PointerDrag_ParentAndBlankRootDropsPublishRequestsWithoutChangingItems` |
| «редактировать дерево и объекты» | `Shell_TreeSelectionTransformCommitParentMoveAndKeyboardHistoryShareOneRetainedDocument` |
| Сохранение local Transform и общий history | `History_TransformAndReparentUseOneUndoRedoStackAndRestoreContentCheckpoints` |
| Stale headless commit / same-ID replacement | `Move_HeadlessCommitOrSameIdentityDocumentReplacementRejectsStaleGestureUntilFreshAttempt`, оба варианта |
| DI и product ownership | `BothProviders_ShareModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedTrees` |

Полные связанные test projects Debug/Release: Aether1050, UI801, Hosting98,
Editor577 = **2526/configuration,5052cases**, failed/skipped0. Это четыре проекта,
не full solution tests. `managed-091444455` содержит8TRX; после финальной правки
UI-подсказки Editor577/configuration повторён в `finish-092447200`,2TRX.
Остальные production исходники между этими проверками не менялись.

Команды воспроизводятся `VerifyFinal.ps1` и `VerifyFinish.ps1` в ignored
`TestResults/Hierarchy-20261024/`: `dotnet test <project>/<project>.csproj
--artifacts-path TestResults/Hierarchy-20261024/artifacts -c <Debug|Release>
--logger "trx;LogFileName=<project>-<configuration>.trx" --results-directory <run>
/bl:<run>/<name>.binlog -v minimal`. Все commands завершились exit0.
Финальный `dotnet build TrueMoon.slnx --artifacts-path
TestResults/Hierarchy-20261024/artifacts --no-incremental -c Debug -v minimal
/bl:<finish>/solution-build.binlog`: exit0,0errors/1532warnings. Native runner
отдельно собирает Editor Debug/Release и framework-dependent Release publish.

### Native acceptance и границы

Финальный `native-092503209/summary.json`: **10/10** source Debug/published Release
processes вне repo: empty, legacy game, documents, Transform, hierarchy.
Всего118PNG/14reaped children; каждый report: UI sessions/mappings/GPU resources/
handles0, validation0errors/0warnings. Header5/status4 captures в каждом процессе,
включая BottomAligned/ResizeGripClear. Source/binary/PNG fingerprints сохранены
и сверены. Финальные dark hierarchy, published light preview, legacy light и
authoring documents PNG просмотрены; glyph-подсказка исправлена на Ctrl+Left/Right.

Hierarchy в обеих поставках: реальный pointer selection/drag, local Changes и
definition identity сохранены, один history entry, collapse/reveal, Ctrl+Z/Y,
retained theme/inspector, SaveAs/Close/Open и persisted ParentId с пустым ephemeral
history. «В корень» и Undo возвращают clean saved checkpoint. Footer запускает
реальный Edit Preview, actual presented revision и пиксели parented cube проверены;
один child drained/reaped, compiler не запущен, исходная fixture не изменена.
Старые document/Transform/legacy сценарии адаптированы к общему дереву.

Промежуточные native attempts не засчитаны: `native-090921771` остановился в
старом header menu wait; `native-091619401` — document Capture, ожидавший старую
длинную строку status; `native-091931087` — отложенный focus assertion header.
Document Capture теперь проверяет typed summary и отдельный bound entity count.
Header driver ждёт action нужной группы и повторяет только открытие dismissed
popup, никогда application command; focus после close проверяется в том же
owner callback, до дальнейших native FocusLost. После этих исправлений
`native-092055591` прошёл10/10; финальная glyph-правка повторена в указанном
`native-092503209`. Это улучшение directed driver, не доказательство причины
любых исторических физических input failures.

Evidence: `TestResults/Hierarchy-20261024/verification.json`, TRX/log/binlog/JSON/
PNG и scripts ignored. Общие plans/status/README/default style, ссылки/anchors,
namespaces новых файлов и diff whitespace проверены отдельно. Не запускались
full solution tests/новые NuGet consumers/physical DPI-input/performance/VRAM/
Vulkan UI effect matrix/final alpha. Старые CPU FrameReceiver и Mithril ready-file
race не объявлены исправленными; K7 workaround search не возобновлялся.

## ED4b: выравнивание статусбара — 2026-10-24

По скриншоту убрана пустая нижняя строка16logical: default WindowFrame больше
не резервирует высоту под grip, рисуя его поверх правого нижнего угла content.
StatusBar28 доходит до внутреннего бордера; его View/Bar bounds совпадают.
Правый отступ24 сохраняет доступность details рядом с grip16; DI/preview/actions
и retained View/Data/Style не менялись.

Narrow57/57; UI760/Hosting98/Editor530 Debug/Release =1388/configuration,
failed/skipped0.2native empty cases source Debug/published Release вне repo,
20PNG,zero owned UI/GPU/mappings,View disposed,compiler/children не запускались.
Dark/Light PNG просмотрены. Resize/scale1/1.5/2/maximize/restore/theme/idle и
region metadata проверены регрессией; native smoke подтверждает bottom alignment.
[Причина, exact tests/commands и границы](../alloy/HISTORY.md#pa4b-выравнивание-statusbar-по-нижней-границе--2026-10-24).
Evidence: TestResults/StatusBarAlignment-20261024/verification.json.
Full solution/Aether/headless/legacy/Transform native/physical input не повторены.
Прежние CPU receiver/Mithril fixture issues и порядок работ не изменены.

## ED4b: компактная строка состояния — 2026-10-24

Согласованный дизайн реализован:28logical footer,12text/14icons/24targets,
primary semantic status/cancel слева и objects/Edit Preview/details справа.
Tick/PID/full selectable error доступны в матовом popup над панелью.
Edit Preview actions перенесены из production inspector в footer; прежний
coordinator/committed history/lifecycle сохранён, game controls остались в header.
Активный preview обозначен «Редактирование сцены», без смешения с game pause.
Responsive overflow, keyboard/focus/dismissal/theme и retained tree проверены.
View/Data/Style и typed factories подключены к App/Cobalt; ownership явный,
Cobalt/MS DI/failed factory cleanup/detach проверены.

80новых cases; итог UI759/Hosting98/Editor527 Debug/Release =1384/configuration,
failed/skipped0. Nonincremental Debug solution build0errors/1532warnings.
Source Debug/published Release вне repo:6native empty/legacy/Transform cases,
68PNG/12reaped children,validation0/0 и zero UI/mapping/GPU ownership.
Footer start/stop с реальным preview, actual revisions/pixels, Undo/Redo/Save/reopen
и прежний header workflow прошли; compiler для preview fixture не запускался.
Dark/Light/details/Preview/Transform PNG просмотрены. Источники/binaries/PNG hashes
и exact TRX/commands: TestResults/StatusBar-20261024/verification.json.

[Общий механизм, точные тесты, исправления и границы](../alloy/HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24),
[дизайн](EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24).
Full solution tests/Aether/headless/packages/physical/performance не повторены.
Прежние CPU receiver/Mithril fixture issues открыты. Следом minimal create/delete
общего authoring API/history и UI commands; межмодульный порядок прежний.

## ED4a: матовый фон меню — 2026-10-24

Dropdown меню Editor используют общий материал Argentis/Skia: blur уже нарисованного
фона текущего окна + theme tint, foreground/children чёткие. Theme.MenuBackdrop
Dark/Light/null, fallback и обычный style precedence сохранены. Idle/disabled menu
rows прозрачны; команды/availability/focus/retained popup и App/Cobalt/IFactory
ownership прежние. Отдельного прикладного blur service или OS window transparency нет.

Текущие related UI739/Hosting98/Editor467 Debug/Release =1304/configuration,failed/
skipped0,6TRX. Narrow44/44,16новыхcases в UI. Source Debug/published Release вне repo:
4native empty/legacy cases,28PNG/4reaped children; header, simulation9commands,
theme/keyboard/FocusLost и cleanup успешны. Validation0/0, UI sessions/mappings/
GPU resources/handles0, view disposed/children reaped. File/View PNG просмотрены;
pixel samples отличают backdrop от opaque fallback. Источники и binary/PNG hashes,
commands/отчёты — TestResults/Backdrop-20261024/verification.json.

[Общий контракт](../alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24),
[точные тесты/команды/исправленные assertions и ограничения](../alloy/HISTORY.md#pa8b-матовые-поверхности--2026-10-24).
Native Vulkan/HUD effect proof, performance/physical/full alpha, full solution/
Aether/headless/остальные native profiles не повторены. Прежние CPU receiver и
Mithril fixture issues остаются отдельными; следующий шаг — create/delete общего API.

## ED4a: знак TrueMoon и анимированное раскрытие — 2026-10-24

По согласованному уточнению слева добавлен fixed TrueMoon crescent/star, затем
постоянная More кнопка без обычного бордера. Hover раскрывает headings; точки
вращаются ⋮→⋯ на том же ScalarTransition180мс. Caption сдвигается, brand/More hit
bounds и simulation/window actions остаются неподвижными. Hover/pressed/focus
состояния сохранены. Один click либо Enter/Space раскрывает headings и после
анимации открывает первую группу «Файл». Первый enabled пункт получает фокус,
команда при открытии не выполняется. Повторный click закрывает и возвращает фокус
More; Esc/Tab/outside/FocusLost/detach отменяют pending open без позднего popup.
F10 сохраняет прежний выбор heading; duration0 не требует дополнительного жеста.

MenuBar исполняет pending open на owner frame, не удерживает IInputContext между
кадрами и не добавляет Task/timer. Idle/detach прекращают работу. UiSymbol.TrueMoon
добавлен в конец enum, прежние значения сохранены; вырез полумесяца прозрачный,
не закрашивается цветом предполагаемого фона. BrandMark — owned UI element внутри
EditorHeaderView, nonfocusable Caption region,28px area/20px symbol. Accent задаёт
EditorHeaderViewStyle. Новый сервис/DI API не нужен; существующие App/Cobalt/
IFactory/ownership contracts сохранены. Theme/hover не пересоздают дерево.

Прямые regression checks в существующих xUnit suites (имена методов точные):

| Требование пользователя | Проверка поведения |
| --- | --- |
| «добавить иконку» | EditorHeaderViewTests.BrandMark_PrecedesMenuAndKeepsCaptionRegionAcrossThemes; MenuChromeTests.TrueMoonSymbol_PaintsCrescentAndLeavesTheCutoutTransparent |
| «3 точки вертикально», «3 точки горизонтально» | MenuInteractionTests.DisclosureDots_RotateWithRevealAndReverseWithoutMovingTheButton:0/90/180мс, reversal, геометрия точек, fixed bounds и idle |
| «откроется сразу же первоей меню» | MenuInteractionTests.DisclosureClick_RevealsThenOpensFirstMenuWithoutExecutingAction; EditorHeaderViewTests.DisclosureClick_OpensFileAfterRevealAndReturnsFocusOnClose:179/180мс, первый enabled item, no execution, повторный click/focus |
| Отмена/клавиатура/zero motion | DismissDuringDisclosure_CancelsPendingFirstMenu (5cases); DetachingDuringDisclosure_CancelsFirstMenuOnReattach; DisclosureKeyboardActivation_OpensFirstWithoutExecutingItsFocusedItem (Enter/Space); DisclosureClick_ZeroMotionOpensFirstWithoutExtraGesture |

13новыхcases: UI11, Editor2. SDK10.0.402/net10.0/xUnit2/VSTest/Windows x64.
Финальные unfiltered related tests: UI723 + Hosting98 + Editor467 =1288 в каждой
Debug/Release, failed/skipped0,6TRX. Отчёты/unique binlogs/exit logs находятся в
TestResults/HeaderBrand-20261024/managed-193341761. Это текущие related проверки,
не full solution; Aether/Mithril/Enerit suites в данном уточнении не запускались.

Команда для каждой конфигурации и каждого из трёх указанных test projects:

```powershell
dotnet test <project>/<project>.csproj --artifacts-path TestResults/HeaderBrand-20261024/artifacts -c <Debug|Release> --logger 'trx;LogFileName=<project>-<configuration>.trx' --results-directory <managed-run> /bl:<managed-run>/<project>-<configuration>.binlog -v minimal
```

VerifyUi.ps1 сохраняет counters в summary.json; VerifyNative.ps1 собирает Editor
Debug/Release с --no-restore/тем же artifacts-path и отдельными build binlogs,
публикует Release --no-build --no-restore --self-contained false в fresh OS temp.
Исходные bin/Debug и пользовательские Editor/Debugger процессы не изменялись.
App запущен из external cwd с --smoke <case> --validation, legacy дополнительно
получает --project <absolute ManualTests/Game.Logic/Game.Logic.csproj>. Только
process-local Vulkan validation layer; изменения OS/драйвера не требуются.

Финальный native run: TestResults/HeaderBrand-20261024/native-193402807:

| Delivery/profile | Header proof | PNG | Reaped children |
| --- | --- | --- | --- |
| source Debug / empty | passed,5header captures | 6 | 0 |
| source Debug / legacy | passed,5header captures | 8 | 2 |
| published Release / empty | passed,5header captures | 6 | 0 |
| published Release / legacy | passed,5header captures | 8 | 2 |

Всего4cases/28PNG/4children. В каждом header.json: BrandMark,
DisclosureOpensFirst, DisclosureToggle, hover/caption shift/fixed simulation,
keyboard/theme/tooltip/FocusLost=true. Empty без compiler/children; legacy по9
прежних команд, включая start/pause/resume/step/replace/stop. ViewDisposed/Reaped=true,
UI sessions/mappings/GPU resources/handles0, validation errors/warnings0 во всех
cases. Финальные empty-Debug/header-collapsed.png и
empty-published-Release/header-file-menu.png просмотрены: знак, обе ориентации
точек, caption и первый dropdown соответствуют дизайну. Native smoke содержит
directed input, не заменяет physical DPI/drag/Snap acceptance.

Aggregate evidence TestResults/HeaderBrand-20261024/verification.json связывает
6TRX, native header/cleanup reports и команды, source/binary/PNG SHA256. Содержимое
проверено повторно после финального запуска. VerifyFinal.ps1 последовательно
выполнил VerifyUi.ps1 и VerifyNative.ps1; stdout final.log, stderr final.err пустой.

Предварительные narrow-192439038 и managed-192724095/native-192939427 прошли,
но относятся к предыдущему варианту рисунка. На его PNG обнаружены горизонтальные
AA seams полумесяца; перекрытие соседних полос исправило рисунок, после чего
выполнены финальные managed/native проверки выше. Ранний черновой launcher выбирал
обычный старый bin; он исключён из текущего proof, scripts используют явный
isolated artifacts path. Passing предварительных запусков не подменяет финальные.

Full solution, headless manifest/SDK/scene, остальные project/documents/Transform/
exit workflows, fresh packages, golden/performance/HUD/physical/loss/VRAM/full alpha
в этом уточнении не повторены. Последние full результаты2026-10-23 и отдельные
CPU FrameReceiver/Mithril ready-file failures остаются историей и открытыми
quality tasks. Следующий прикладной шаг — minimal create/delete общего authoring
API/history, затем UI; уточнение header не меняет порядок остальных этапов.

## ED4a: компактный заголовок и меню — 2026-10-23

Зафиксирован и реализован дизайн: ⋯ раскрывает «Файл / Правка / Вид / Проект /
Симуляция» за180мс, название сдвигается; ▶/пауза/стоп и стандартные оконные
кнопки неподвижны. Leave grace240мс, click pin, F10/arrows/Enter/Esc/Tab,
outside dismiss и FocusLost. Длинный caption сокращается, fixed buttons имеют
геометрические иконки и delayed tooltip500мс, включая disabled state.

Общая основа Argentis/Alloy: IMenu, MenuBar/MenuItem, IUiCommand/DelegateUiCommand,
OverlayHost (один nonmodal popup), ScalarTransition и owner-thread
IUiFrameParticipant/IUiInputObserver. UiSession использует monotonic TimeProvider,
не начисляет idle новому transition, обрабатывает приоритет owner перед popup host,
не удерживает кадры hidden/detached participants. Пятиаргументный constructor
UiSession сохранён для binary compatibility. Popup размещается/обрезается в
host viewport. Menu owner detach очищается на следующем update; detach самого
host скрывает popup, снимает borrowed references и сохраняет retained tree до
Close/reattach/dispose. Modal stack, long-menu scrolling и generic dialogs не заявлены.

WindowTitleBar.SetHeader принимает owned content; стандартные minimize/maximize/
close и Snap role остаются у default frame. Hosting hook ConfigureDefaultTitleBar
создаёт header на UI owner и передаёт vetoable/drainable close callback. Неподходящие
frame combinations отвергаются до allocation. Callback-throw native rollback
отдельно не проверялся; существующий host rollback не заменялся test-only seam.

EditorHeaderView/Style и typed IFactory<EditorHeaderView,EditorHeaderArguments>
подключены через UseAetherEditorHeader/App/Cobalt. Window владеет View, presentation
и style borrowed. Body владеет OverlayHost; header заимствует его в той же session.
Production compact mode скрывает прежние action rows; standalone constructor
сохраняет прежние controls для совместимости. Поле пути, dirty/errors и inline
Save/Discard/Cancel сохранены. File/Undo/Redo/build/runtime/theme actions перенесены
в меню; document picker/project switch не добавлялись.

▶ использует один tracked start→play gesture либо resume; Stop/Cancel/close
отменяют gesture, запрещают late Play и drains accepted work. Открытый authoring
document по-прежнему блокирует game actions до ED6; Edit Preview остаётся отдельным.
Header routing сохраняет S/Shift+S/W/Z/Shift+Z/Y/Esc/Ctrl+Enter после возврата фокуса
на ⋯. Modified Enter обрабатывается до обычной Button activation; body TextBox
history keys не перехватываются header.

Проверки:
- 105 новых cases: UI51, Hosting13, Editor41. Related Debug/Release:
  UI712 + Hosting98 + Editor465 =1275/configuration, failed/skipped0.
  Deterministic reveal/reversal/zero-motion/idle, exact close/tooltip boundaries,
  disabled admission, retained theme/focus/tree, dispose/subscription cleanup,
  overlays, keyboard/pointer gestures, fixed chrome bounds/regions, both DI providers,
  shared history/file actions и controlled start/stop/cancel/drain.
- Последний nonincremental Debug build0errors/1532warnings.
  Последний full Debug3048/3049: MithrilRestartProbe ReadyAsync получил IOException
  при чтении ready-4.json одновременно с публикацией; UI/Editor и Aether прошли.
  Последний full Release3049/3049,10TRX/configuration, skipped0.
  До финального harness admission-wait изменения был full3049/3049 в обеих
  конфигурациях; это предыдущий запуск, он не отменяет последний Debug failure.
- Ранний full Debug3042/3043 до shortcut regressions: старый FrameReceiver test
  TakeLatest вернул null после Received>=5. Позднее Aether1008/1008 в обеих
  конфигурациях; production CPU transport не менялся и race не объявлен исправленным.
  Отдельные ENG2/Mithril quality tasks внесены в планы.
- Source Debug и published Release вне repo:10native cases (empty/project/legacy/
  documents/Transform ×2),10header proofs,64PNG и12reaped children. Header
  hover/caption shift/fixed icons/menu/theme/keyboard/tooltip/FocusLost проверены
  directed input; actual Build→Run/Pause/resume/Step/Replace/Stop, file menu actions,
  native close и Menu Exit с Save/Discard/Cancel/failed Save выполнены.
  Transform revisions/pixels/Save/reopen и cleanup повторены. По всем cases
  UI sessions/mappings/GPU resources/handles0, validation errors/warnings0.
  Ещё10headless manifest/SDK/scene cases отдельно. PNG source/published header,
  light runtime и Transform просмотрены.
- Общий evidence TestResults/MenuHeader-20261023/verification.json:
  UiScopePassed=true, FullLatestDebugPassed=false; source/binary SHA256,
  PNG hashes, per-project reports и ссылки на immutable runs.
  Debug: solution-183128349; Release: release-183338362.
  Native: regressions/consumers-184542662,
  native-Debug-184631157, native-published-Release-184637466.
  Предыдущий passing full: solution-182420183; ранний CPU failure:
  solution-181603083. Unique binlogs/TRX/exits сохранены.
- Проверки используют --artifacts-path TestResults/MenuHeader-20261023/artifacts:
  обычный bin/Debug удерживается пользовательским Editor/Debugger, их не завершали.
  VerifySolution.ps1 запускает nonincremental build и VSTest Debug/Release с
  --blame-hang-timeout60s/--logger trx и unique /bl; VerifyRelease.ps1 фиксирует
  последний Release run. VerifyRegressions.ps1 публикует --no-build --no-restore
  Release вне repo, затем запускает external-cwd headless/native profiles.
  VerifyNative.ps1 отдельно проверяет Transform в каждой delivery.
  Aggregate.ps1 проверяет10header JSON/64PNG/12children,1275related tests и hashes.

Финальные related tests после compatibility overload: related-184236954/summary.json.
Последний повторный native до driver fix: consumers-184300613, empty-Debug timeout;
он не включён в passing proof. Final source/binary fingerprints проверены заново.

Исправленные проверки/границы: первоначальный smoke нажимал ▶ до owner refresh
CanRun после Build — добавлено ожидание enabled state. Header smoke получал deadline
между theme/F10 и при ожидании tooltip; причина отдельно не установлена. Driver
теперь удерживает injected hover на owner polls и повторяет F10 после потери
раскрытия. Это directed profile; physical input не заявлен. Exact delays и FocusLost
отдельно проверены fake-clock tests. Ранний
Transform Debug запуск ошибочно выбрал обычный старый bin: исключён из нового
proof; финальный native-Debug-184631157 использует isolated artifacts и содержит
header.json/7PNG. Эти неуспехи не скрываются passing итогами.
Fresh NuGet packages, HUD/Vulkan UI control matrix, physical DPI/drag/Snap,
golden/performance/loss/VRAM/full alpha не повторены. Следом minimal create/delete
через общий authoring API/history; test stability follow-ups остаются отдельными.

[Текущий статус](STATUS.md), [план движка](PLAN.md), [подплан приложения Editor](EDITOR_PLAN.md).

## ED3a: Transform/history/preview в UI — 2026-10-23

Сверка планов исправила устаревшие указания ARCH1b.1/ED1a как будущей работы и
смешение document Close/Exit с ещё открытой project/runtime transaction. Принят
порядок: первый edit/save/preview цикл → минимальные create/delete общего API →
project picker/switch и ED1/ED6 workflow. Core/Cobalt не требуют нового полного
quality prerequisite; ENE6c/d и оставшийся MIT6a/c остаются отдельными задачами.
Исторические результаты QUALITY/ENE6 baseline помечены по их scope.

[ED3a](EDITOR_PLAN.md#ed3a-transformhistorypreview-в-ui--2026-10-23) подключил
существующий ARCH1b.1 к девяти retained numeric полям, atomic Apply/Ctrl+Enter,
Cancel/Esc и Undo/Redo. Invariant Float-representable values и positive scale
проверяются локально, общий engine проверяет итоговые матрицы/scene. Draft хранится
отдельно от committed content; selection change отбрасывает его, внешнее изменение
той же сцены сохраняет conflict до явного Cancel. File/history mutations имеют
общую admission и проверяют document identity под owner gate; UI не держит второй
history. Dirty следует saved ContentRevision. При draft history отключена; вне
полей Ctrl+Z/Shift+Z/Y используют общие actions, внутри TextBox их не перехватывают.
Общий text undo этим шагом не добавлен.

EditorPreview заимствует show/stop engine boundaries, хранит одно выполняемое и
одно последнее ожидающее обновление. Explicit start/retry, auto update после
commit/Undo/Redo; ввод текста не вызывает child replacement. Exact presentation
receipt проверяется; старый кадр не помечается новой revision. Ошибка сохраняет
document/history и не даёт auto retry на каждом frame. Stop и document transitions
fence/cancel/drain child work, cleanup error наблюдаем и допускает retry. Cancel
close decision не останавливает preview. App/Cobalt/IFactory регистрируют model,
style и UI-owner products; dependencies borrowed, aliases не удваивают dispose.

### Проверки и найденные регрессии

Добавлены66cases в8files Editing. Assert/gap review без production mutations;
direct tests затрагивают model, preview, guarded commands, view/shell, codec,
both-provider composition и CLI. Новый bounded набор66/66 Debug; весь Editor424/424
Debug/Release. Финальный full solution **2944/2944 Debug и2944/2944 Release**,
10TRX/configuration, failed/skipped0. Core117/Cobalt179/generator91/Enerit166/
generator114/Mithril99/UI661/Hosting85/Aether1008/Editor424. Nonincremental Debug
build:0errors/1531warnings, прежние warnings не подавлялись.

| Поведение | Прямая проверка |
| --- | --- |
| Девять полей, atomic commit/history | `Selection_LoadsNineStableFieldsAndAtomicCommitUsesOneSharedHistoryEntry` |
| Невалидные числа/scale без частичной мутации | `InvalidField_ReportsLocalErrorAndNeverCommitsPartialBatch` |
| Draft/conflict/cancel и dirty checkpoint | `Refresh_PreservesDraftAndExternalRevisionRequiresExplicitCancelBeforeCommit`, `UndoRedo_UsesSavedContentCheckpointAndDraftCommandsCannotDiscardText` |
| Live game/IO/exit admission | `BusySaveAndExitDecision_BlockMutationWithoutLosingDraft`, `ApplyAndHistory_RecheckLiveRuntimeAdmissionAndPreserveDraftUntilRuntimeBecomesIdle` |
| Exact revision и последняя ожидающая сцена | `WrongSceneOrRevisionAcknowledgement_IsRejectedWithoutInventingPresentedState`, `BusyPreview_KeepsCurrentAndOnlyNewestWaitingSnapshotWithoutRetaggingPresentedFrame` |
| Failure/cancel/drain/retry | `PreviewFailureAndRetry_PreserveCommittedDocumentHistoryAndUncommittedDraft`, `Stop_CancelsDropsWaitingAndDrainsIgnoredCancellationBeforeSharedCleanupThenAllowsRestart`, `StopCleanupFailure_IsObservedAndRetryPerformsActualCleanupBeforeClearingError` |
| Реальный UI input, stable tree/style/focus | `ShellSelectionAndNormalizedKeyboardInput_EditSharedDocumentRetainFormAndPreserveFocusedTextShortcutOwnership`, `RefreshAndThemeStyleChange_PreserveDraftTreeFocusSelectionAndInputIdentity` |
| DI/factory/cleanup | `BothProviders_ShareModelAndBorrowedDependenciesWhileFactoryCreatesIndependentUiOwnedViews`, `StyleFailure_DisposesUnpublishedFormAndBindingsWithoutOwningBorrowedModelOrDocument` |
| Save/reopen всех значений и CLI scope | `TransformCommit_SaveCloseAndReopenWithRealCodecPreserveAllNineValuesAndClearHistoryAtNewSession`, `TransformSmoke_RejectsMissingFixtureOutputLegacyAndMixedAcceptanceScopes` |

Первые tests выявили неподдержанный selector expression в Visible binding: заменён
прямым HasError. Native capture выявил unconditional ErrorText notification каждый
frame: теперь уведомления только при изменении; regression требует0model/field
notifications и ui.Update=false при stable Refresh. PNG review выявил clipping
цифр при default padding в компактном TextBox: scoped padding4/2/4/2 исправлен и
проверен тестом/PNG. Pixel proof использует публичную PreviewGameSession.Camera,
а не камеру legacy demo. Ранний проход, изменённый во время style fix, не считается
финальным: окончательный run имеет неизменные source fingerprints.

Source Debug/published Release вне repo с deliberately invalid game csproj: по
одному Transform workflow,3PNG/4children. Actual revisions8/9/10 и reopen10,
позиция/цвет куба проверены pixel checks; source fixture unchanged, compiler0,
notifications только на UI owner, stable fields/canvas/selection. Stop и document
Close reaped children; final UI sessions/mappings/GPU resources/handles0,
Vulkan validation0errors/0warnings. PNG обеих deliveries просмотрены.

Также повторены empty/project/legacy/documents на source Debug/published Release:
8native cases/18PNG/4legacy children и10headless scene/SDK cases,19consumer commands
с6expected negative exits. В сумме текущий native scope — **10cases/24PNG/12reaped
children**. Прежний document workflow: по19actions/4PNG/5saved files на delivery;
legacy: по8commands/3PNG/2children. Физические DPI/loss/VRAM и fresh packages не
повторялись, весь hardware/release scope этими окнами не закрывается.

### Команды, evidence и продолжение

Финальный orchestrator: TestResults/TransformUi-20261023/VerifyFinal.ps1.
Полная сборка: `dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal /bl:<unique.binlog>`.
Tests: `dotnet test TrueMoon.slnx --no-restore -c Debug --no-build --blame-hang --blame-hang-timeout 60s --logger trx --results-directory <Debug> /bl:<unique.binlog>`;
Release — та же команда с `-c Release`, без `--no-build`. Все завершены с exit0.
Publish: `dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --no-build --no-restore -c Release --self-contained false -o <outside-app> /bl:<unique.binlog>`.
Native: `<editor.exe> --project <outside-fixture.aetherproj> --smoke <fresh-output> --smoke-transform --validation`.
VerifyNative.ps1 сохраняет generated fixture, CWD/delivery/source/binary hashes,
PNG, runtime/project cleanup и JSON receipts. VerifyRegressions.ps1 фиксирует
прежние headless/native consumers и их actual arguments/exits.

Агрегат: **TestResults/TransformUi-20261023/verification.json**; final solution —
solution-165319129, native-Debug-165441567 и native-published-Release-165446134;
regressions/consumers-165623415/summary.json. Narrow TRX/logs скопированы в focused;
все MSBuild invocations имеют отдельный binlog. Новых engine/wire/assembly contracts
не введено, package/publication readiness не объявляется.

Первый сквозной цикл закрыт в этом bounded profile. Далее create/delete через
общий authoring API/history, затем UI New→Add→edit→delete/Undo→Save/reopen; после
него ED1/ED6 project/runtime workflow. Preview всё ещё fixed-camera640×360,
до64entities и с child replacement. Полный World/ECS, live edit/MCP, docking/plugins,
MMF/performance и hardware gates сохраняют отдельные задачи.

## ED2b: документы в обычном UI — 2026-10-23

[Document UI profile](EDITOR_PLAN.md#ed2b-document-commands-и-ui--2026-10-23)
подключает существующий ED2a API: New/Open/Save/SaveAs/Close, dirty/activity/errors,
inline Save/Discard/Cancel и native window Exit. Нормальный запуск с .aetherproj
показывает панель путей/actions; проекты по-прежнему выбираются явно через CLI.
Doc transitions доступны только при stopped/idle runtime/compiler; открытый
authoring document блокирует stale game commands до preview/ED6 integration.
Игровой legacy .csproj workflow проверяется отдельно.

EditorDocumentCommands имеет fail-fast single admission, shared exit completion,
tracked accepted task и user decision, не хранит controls. Open сначала передаёт
candidate validation нижнему API: даже выбранный Discard не теряет старую сцену
при malformed target. Save failure при Exit возвращает false и сохраняет окно/
unsaved content. Cancel и forced upgrade реально drains IO, не ждут новое решение.
Throwing cancellation callbacks превращаются в наблюдаемую ошибку после drain;
terminal cleanup допускает retry. Project/session/store заимствуются; их ownership
сохраняется у прежних App/project owners, без disposable aliases.

EditorDocumentViewModel копирует snapshots и сообщает binding updates на owner
thread; background completion не мутирует UI. Path draft для Open/SaveAs и отдельный
first-save draft для decision сохраняются независимо от completion. Nested typed
View/Style создаётся injected IFactory на UI thread, parent owns product. Стабильные
controls, bindings и собственный scoped style не требуют полного rebuild.
Ctrl+S/Shift+S/W и buttons идут в одни commands; Esc отменяет decision. Safe focus
переходит на Cancel, возвращается к live control/path после IO. Решение inline,
generic PA3 overlays/modality/menu infrastructure не добавлена.

Workspace показывает raw authoring hierarchy IDs и resolved readonly properties
без 64-entity runtime projection. Граничный тест использует130entities без truncation.
String values показаны в кавычках с escaped control characters: первый UI test
выявил, что исходная multiline string ломала single-line TextBox.Value; formatter
исправлен, regression выполняет реальный UiSession.Update. Full IDs доступны в
inspector, подписи Guid сокращены с различимыми окончаниями. Пустой authoring
viewport сообщает, что preview пока недоступен; не предлагает заблокированный Build.

### Прямые tests и quality review

42methods/74новых cases в DocumentsUi: coordinator decisions/admission/drain,
real filesystem conflict/reopen, owner-thread model, retained view/styles/focus/
shortcuts/dispose, shell/raw properties, both DI providers и launch parser.
Research/plan/status и pairing inventory хранятся в OS temp вне Git. Static pairing
не является line/branch coverage; production mutations не применялись.
Assertion/gap review добавил older captured Save + newer edits + Exit decision,
externally accepted document IO admission, prompt keyboard intent и ctor guards.
Первые test-authoring empty snapshot assertions исправлены (Content=null внутри
действующей session, а не отсутствие owner). Это не production failure.

| Требование | Exact evidence в TrueMoon.Aether.Editor.Tests.DocumentsUi |
| --- | --- |
| Реальные Save/reopen/path/stamp/conflict/recovery | NewSaveAsOpenAndSave_UseCurrentPathStampAndPreservePersistedRawScene; CommandsWithRealStore_SaveReopenAndExternalConflictPreserveFileAndRecoverBySaveAs |
| New/Open/Close/Exit Save/Discard/Cancel | DirtyTransition_CancelRetainsIdentityContentAndAllowsRetry; DirtyTransition_DiscardReleasesOriginalWithoutSavingAndCommitsIntendedTarget; DirtyTransition_SavePersistsOriginalBeforeReplacementOrExitUsingCorrectTarget |
| Failed Save veto и invalid Open с Discard | FailedDecisionSave_VetoesTransitionKeepsDirtyOriginalAndCanRetryIntent; DirtyOpenDiscard_InvalidCandidateDoesNotDiscardCurrentContentOrSaveIt |
| Ignored IO / late completion / shared Exit | RequestExit_DrainsIgnoredSaveBeforeStoppingRuntimeAndConcurrentRequestsShareCompletion; Exit_AfterIgnoredSaveOfOlderSnapshotStillRequiresDecisionForNewerEdits |
| Throwing cancellation / forced upgrade / retry | Exit_CallbackFailureReturnsTrackedDrainRetainsOwnerAndSupportsExplicitRetry; ExitCancelAndForcedUpgrade_SharePendingCloseAndNeverWaitForAnotherUserDecision |
| Fresh runtime guards / owner notifications | RuntimeBlocking_UsesFreshCoordinatorAdmissionBeforeNextPresentationRefresh; Refresh_PublishesChangesOnOwnerOnlyAndWorkerCompletionCannotNotifyBindings |
| Inline choices / keyboard / focus | InlineDecision_UsesSeparateSaveDraftAndSafeCancelFocusThenRestoresPriorInput; KeyboardDuringDecision_SaveUsesPromptPathAndCloseCannotReplaceAcceptedIntent |
| Retained tree/path/style/subscriptions | ThemeStyleAndRefresh_PreserveTreePathDraftFocusAndSelection; ViewDisposal_DetachesBindingsAndDoesNotDisposeBorrowedModelOrCommands |
| Raw readonly shell и оба providers | ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands; BothProviders_ResolveSharedModelStyleAndCoordinatorWhileUiOwnsIndependentFactoryProducts |

Final focused command:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.DocumentsUi. --logger trx --results-directory TestResults/DocumentsUi-20261023/narrow-final --verbosity quiet
```

74/74 passed, failed/skipped0. TRX:
TestResults/DocumentsUi-20261023/narrow-final/MEGA6_PH-DEV_2026-10-23_20_14_32_net10.0.trx.
После финальной UI text/label правки те же74case повторяются в обоих final full runs.

### Итоговая source/published проверка

TestResults/DocumentsUi-20261023/VerifySolutionCurrent.ps1:
nonincremental Debug solution build0errors/1531warnings; Debug и Release
**2878/2878** каждый,10TRX/configuration, failed/skipped0. Core117/Cobalt179/
generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/Aether1008/Editor358.
Debug test --no-build после build; Release test собирает актуальный source,
blame-hang60s.242source fingerprints неизменны до/после. Final data:
TestResults/DocumentsUi-20261023/solution-current/summary.json.

Дополнительный footer regression добавляет concrete model.StatusText и
bound view.Status.Value assertions в ShellRefresh_UpdatesOnlyAuthoringPartsKeepsHeaderAndSelectionAndBlocksLegacyCommands:
1/1passed, TestResults/DocumentsUi-20261023/footer-regression/
MEGA6_PH-DEV_2026-10-23_20_25_53_net10.0.trx. Native capture readiness усилен:
ждёт согласованных workspace count/model text/bound text, затем сохраняет эти
значения вместе с PNG. Final snapshots корректно показывают2/0/0/2entities;
предыдущий resized capture до этого ожидания показывал несогласованный footer.
Из этого промежуточного PNG production defect не выводится; final unit/native
данные совпадают, общий renderer/binding API не менялся.

VerifyConsumers.ps1: **consumers-153002186/summary.json Completed=true**.
Source Debug и published Release app/project/CWD вне repository, без shipped
Game.Logic.19commands: publish1/headless10/native8;6exit1 ожидаемы для missing SDK,
unsupported version и scene-root escape, остальные exit0.
Document UI в каждой delivery:19directed actions/4PNG/5actual saved files,
native close и real controls/keyboard. Проверены selection/raw properties,
SaveAs/reopen/stable IDs, external SHA256 conflict + recovery, invalid first Save,
New/Open Save/Discard/Cancel, invalid Open с Discard, failed Exit Save и Cancel
с прежним window/document, successful final Exit с exact saved SceneId.
Original fixture bytes неизменны; compiler/game children0, notifications только
на UI owner thread. Dark/Light,1100×700 resized и безопасный decision focus
визуально сверены. Saved files: Copied/Recovered/New/BeforeOpen/Exit.aetherscene.

Empty/project/legacy/documents —8native cases/18PNG,4game children reaped;
legacy по8commands/3PNG/2children. Validation0errors/0warnings, UI sessions/
mappings/GPU resources/handles0, selected project owners closed.45Editor source/
8binary fingerprints и PNG проверяются aggregate script. Итог:
TestResults/DocumentsUi-20261023/verification.json; logs/TRX/JSON рядом.

Первый consumer attempt остановился до запуска documents profile: helper неверно
вставил чтение documents.json перед процессом. Script исправлен уникальным
line-anchored match; это tooling failure, не приложение. Предварительные passing
solution/native runs до text/label и footer-capture правок сохранены отдельно;
актуальные fingerprints и результаты берутся только из указанных final runs.

ED2 single-project document profile закрыт, ED1 project/runtime transaction и
ED3–ED8 не закрыты. Нет editable Transform/Undo/Redo/preview integration, project
picker/file browser и generic modal/menu system. Runtime/engine/wire/UI-library
APIs/dependencies прежние; Editor non-packable. Fresh packages, independent
preview drawing, golden/RTE/full performance/physical input/loss/VRAM/K7 не
повторялись. Следующий срез — Transform inspector/Undo/Redo и independent preview.

## ED2a: документ сцены и persistence — 2026-10-23

Реализован [document/persistence profile](EDITOR_PLAN.md#ed2a-documentpersistence-api--2026-10-23).
SceneFileCodec строго сохраняет raw SceneDocument/catalogs/patches/IDs и tagged
scalar values, валидирует через owned engine factory product. JSON formatVersion1,
UTF8 optional BOM≤4MiB/depth32, finite collection limits; unknown/duplicate members,
invalid graph/scalar/version и превышение Encode/Decode bounds отклоняются.
Новых engine/wire/UI-library contracts/dependencies нет.

UseAetherAuthoring → UseAetherEditorDocuments → UseAetherEditorProjects создаёт
App-local shared codec/store/factories и owned ProjectSession→SceneDocumentSession→
ISceneAuthoringSession. Metadata-only UseProjects совместим. Editor не дублирует
committed state/history: dirty определяется saved ContentRevision, Undo/Redo
вызваны через engine API. Save captures immutable snapshot и оставляет новые edits
dirty. SaveAs failure/cancel не меняет выбранные path/stamp/checkpoint.
Close Save/Discard/Cancel fences/drains accepted IO, terminal Dispose явно forced
discard; custom store, игнорирующий token, не освобождается раньше завершения.
Project switch/Close блокируют dirty/busy document и атомарно закрывают clean
admission. Failed/rejected candidate cleanup завершается наблюдаемой ошибкой,
retained products остаются для retry; borrowed dependencies имеют одного App owner.

Json store пишет temp рядом, затем atomic rename/replace. SaveAs другого файла
create-only; существующий выбранный файл проверяется по SHA256 наблюдавшихся bytes.
Persistent .write-lock exclusive handle согласует cooperating writers. Проверены
change с тем же timestamp, missing target, locked commit failure, temp cleanup,
точные fingerprints, повтор после ошибки. Profile optimistic/trusted local:
arbitrary external rename между check/replace, filesystem links и power-loss
durability не являются гарантиями. Store API не обещает filesystem CAS.

### Требования и прямые тесты

Новый набор —58методов/143case: codec67/store12/document35/DI21/CLI8.
Assertion/gap review проверил concrete output/state/failure/ownership assertions;
production mutations не применялись. Дополнены independent Encode overflow,
CloseSave после edits во время старого save и rejected project cleanup completion.
При раннем чтении codec добавлена cancellation check после factory validation;
первые test-authoring failures исправлены до final narrow run, production defects
по ним не выводились. Research/plan/review/matrix находятся в OS temp вне Git.

| Требование | Exact test evidence в TrueMoon.Aether.Editor.Tests.Documents |
| --- | --- |
| Raw catalogs/inheritance/masks/restores/parents/scalar kinds | RoundTrip_PreservesRawCatalogsInheritanceMasksRestorationParentsAndExactTaggedScalars |
| Strict format и независимые Encode bounds | Decode_RejectsUnknownDuplicateMissingAndWrongCaseMembersAtEveryStructuredLevel; Encode_RejectsOversizedLocalCollectionsEvenWhenTheEngineCanResolveTheirContent |
| Conflict без timestamp и atomic failure | Replace_ExternalContentChangeWithIdenticalTimestampIsConflictAndRetryRequiresNewObservedStamp; Replace_FailedAtomicCommitPreservesOriginalAndCleansPreparedTemporaryForRetry |
| Dirty по content checkpoint / Undo / reload | NewSaveEditUndoRedoAndReload_UseContentCheckpointRatherThanMonotonicDocumentRevision |
| Captured save и следующий snapshot при Close Save | Save_CapturesImmutableSnapshotAllowsEditsAndDoesNotMarkNewerContentClean; CloseSave_AfterEditDuringPendingSavePersistsTheNewerSnapshotBeforeReleasingDocument |
| Ignored cancellation / late completion / ownership | CloseDiscard_CancelsAndDrainsIgnoredLoadBeforeDisposingActiveAndNeverSelectsLateCandidate; Dispose_FencesAndDrainsIgnoredSaveWithoutLateCheckpointOrDoubleRelease |
| Cobalt/MS DI, dirty/busy project guard, App drain | ProjectOwnedDocuments_BlockDirtySwitchAndNormalCloseUntilExplicitSaveOrDiscard; ProjectTransition_RefusesCleanDocumentWithAcceptedIoThenFencesOldOwnerAtSuccessfulSwitch; AppStop_ForceDiscardsDirtyDocumentsButDrainsAcceptedSaveBeforeBorrowedStoreDisposal |
| Rejected candidate cleanup / retry | ProjectCandidateCleanupFailure_CompletesRejectedOpenAndRetainsOwnedCandidateUntilCloseRetry |
| Explicit headless scene path | CheckScene_PreservesScenesRelativePathAndExplicitManifestWithoutSelectingLegacyRuntime |

Final narrow command:

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Documents. --logger trx --results-directory TestResults/Documents-20261023/narrow-final --verbosity quiet
```

143/143 passed, failed/skipped0. TRX:
TestResults/Documents-20261023/narrow-final/MEGA6_PH-DEV_2026-10-23_18_09_21_net10.0.trx.

### Full solution и опубликованное приложение

TestResults/Documents-20261023/VerifySolution.ps1: nonincremental Debug solution
build0errors/1531warnings; full Debug и Release **2804/2804** каждый,10TRX/config,
failed/skipped0. Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
UI661/Hosting85/Aether1008/Editor284. Debug test --no-build после full build;
Release test собирает актуальную конфигурацию, blame-hang60s.229source fingerprints
проверены до/после, включая все новые tests и Editor production files.

TestResults/Documents-20261023/VerifyConsumers.ps1:
consumers-131340219/summary.json Completed=true. Source Debug и published Release
app/project/CWD вне repository; shipped Game.Logic отсутствует.17commands:
publish1, headless10, native6. Шесть exit1 ожидаемы: missing SDK, unsupported scene
version и path traversal в каждой delivery; остальные exit0.
Rich scene содержит2entities/3definitions/1schema, stable IDs/revision12,
inheritance/remove/restore/parent links, long/number/Boolean/Unicode/null values.
--check-scene load→encode сохраняет исходный SHA256, GameLoaded=false,
SessionClosed/DocumentClosed/ActiveCleared=true, IsDirty=false. Невалидный XML
game csproj доказывает отсутствие MSBuild evaluation при project open.

Native empty/project/legacy:6cases/10PNG, workspace без compiler/child, legacy
по8commands/3PNG/2reaped children. Vulkan validation0/0, UI sessions/mappings/
GPU resources/handles0, selected project owner closed.39Editor source/8binary
fingerprints и все PNG перепроверены. Aggregate:
TestResults/Documents-20261023/verification.json; scripts/logs/TRX сохранены рядом.

Published scene check read-only; atomic writes/Save/reopen проверены direct
real-filesystem tests, а не опубликованным оконным Save workflow. Normal Editor
UI пока не вызывает Load/Save/authoring/preview, остаётся read-only. Четыре ED2
задачи закрыты; общий UI Save/Discard/Cancel и runtime switch/close ещё открыты.
Fresh package runs не нужны для non-packable Editor; engine packages, independent
preview drawing, golden/RTE/full performance/physical UI/real loss/VRAM не повторены.
Следующий срез — document commands/close policy в UI, затем Transform/Undo/preview.

## ED1a: project/session и запуск вне repository — 2026-10-23

Реализован [первый project/session profile](EDITOR_PLAN.md#ed1a-первый-projectsession-profile--2026-10-23).
UseAetherEditorProjects подключает независимые от UI/engine App-local store,
SDK preflight, typed session factory и одного App-owned EditorProjects. Products
принадлежат coordinator; borrowed store/toolchain/factory освобождаются своим DI
owner после Stop/drain. Lifecycle aliases non-disposable, duplicate attachment
отклоняется до activation. Cobalt default и Microsoft DI сохранены.

Manifest .aetherproj schema1 — strict UTF8 JSON с optional BOM, stable project ID,
game csproj/entry и relative scene/content roots. Максимум64KiB вместе с BOM;
неизвестные/повторные/case-mismatched поля, invalid version/metadata/paths и missing
files/roots отклоняются. Resolve использует manifest directory, не CWD; это
trusted-local lexical containment, не security sandbox поверх filesystem links.
Create работает с существующим игровым csproj/roots: после SDK preflight пишет
temporary file и atomic rename без overwrite. При cancel после file publication
валидный manifest остаётся на диске, cancelled candidate не становится active.

SDK preflight выполняет owned dotnet --version из game directory, учитывает
global.json, требует SDK10+ и не оценивает MSBuild/entry. Deadline15s отменяет
ожидание и kill/reap/drain probe; совместимость игры проверяется explicit Build.
ProjectSession хранит metadata/SDK identity и формирует GameBuildRequest.
Open/Create fail-fast при concurrent transition. Failed/cancelled candidate не
заменяет active; Close cancels/drains accepted work до закрытия active, reusable
после успешного Close; App shutdown закрывает admission окончательно. Late
completion от игнорирующей token зависимости не публикуется.

CLI: без аргументов пустая workspace; --project .aetherproj выбирает project/build
identity без demo и без загрузки scene document. --check-project запускает тот же
App/Cobalt API без окна и выводит JSON preflight/cleanup. Explicit --project
game.csproj сохраняет legacy demo/game smoke. Search repository/demo defaults удалён.
Редактор по-прежнему Fluent View/Data/Style; новые store/toolchain API — реальные
IO/process boundaries, generic framework/plugin/scopes не введены.

### Requirement / test evidence

Новых92case/35методов: Store40, coordinator15, registration10, launch23, session4.
Только6новых test files в TrueMoon.Aether.Editor.Tests/Projects; старые tests не менялись.

| Требование ED1a | Точные tests |
| --- | --- |
| Versioned portable manifest и canonical paths | `Resolve_NormalizesContainedPathsAndNameWithoutChangingPortableInput`; `WriteNew_RoundTripsStrictPortableMetadataAndLeavesNoTemporaryFiles` |
| Strict schema, required fields и finite bytes/text bounds | `Read_RejectsInvalidJsonShapeOrStrictMemberContract`; `Read_RequiresEveryVersionOneMemberInsteadOfAcceptingDeserializerDefaults`; `Read_EnforcesUtf8ByteLimitIncludingExactMaximum`; `Resolve_PreservesLastAllowedTextLengthAndRejectsFirstExcess` |
| Optional BOM считается в64KiB | `Read_AcceptsUtf8BomAtByteLimitButCountsItWhenRejectingFirstExcessByte` |
| Relative containment и missing files/roots | `Resolve_RejectsEachEscapingOrRootedPath`; `Resolve_RequiresExistingGameAndRootsWithoutCreatingThem` |
| Atomic new manifest без overwrite/temp leaks | `WriteNew_ExistingFileIsPreservedAndTemporaryOutputIsRemoved`; `WriteNew_RevalidatesInputAndRecomputesAbsolutePathsBeforeWriting` |
| Create preflight/write до active commit | `Create_PublishesManifestBeforeChangingActiveAndOwnsUnpublishedCandidate` |
| Failed/cancelled candidate сохраняет active/original error | `FailedCandidate_PreservesActiveAndOriginalErrorThenAllowsRetry`; `Cancellation_IgnoredByDependencyStillPreventsLateCandidatePublication` |
| Switch closes previous product, borrows dependencies | `Open_CommitsValidatedFactoryProductThenClosesOnlyPreviousSession`; `Dispose_ClosesAdmissionImmediatelyAndDrainsUnpublishedFactoryProduct` |
| Close fences/drains ignored cancellation, затем можно reopen | `Close_FencesAndDrainsIgnoredCancellationBeforeClosingActiveAndCanReopen`; `Close_ObservesCancellationCallbackFailureAndStillDrainsOwnedWork` |
| App lifecycle/providers/overrides, без двойного ownership | `App_PreservesInjectedStoreToolchainAndFactoryAndDisposesEachDependencyOnce`; `App_StopClosesAdmissionAndDrainsPendingOpenBeforeDisposingBorrowedDependencies` |
| Empty launch без implicit demo, guarded game commands | `EmptyLaunch_SelectsNoImplicitDemoAndNoProjectWorkspaceDisablesEveryCommand`; `WorkspaceSmoke_AllowsEmptyOrManifestWorkspaceWithoutSelectingLegacyGame` |
| SDK guards и explicit build mapping | `DefaultToolchain_PreCancelledAndMissingProjectDoNotStartAnSdkProbe`; `BuildRequest_MapsCanonicalProjectAndEntryWithoutOwningBorrowedMetadata` |

Final focused command, exit0, Passed92/Failed0/Skipped0:

~~~powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj --no-restore -c Debug --filter FullyQualifiedName~TrueMoon.Aether.Editor.Tests.Projects. --logger trx --results-directory TestResults/ED1a-20261023/narrow
~~~

TRX MEGA6_PH-DEV_2026-10-23_17_23_38_net10.0.trx. Первый run90cases дал87pass/3fail:
oracle ошибочно требовал exact JsonException вместо JsonReaderException subtype;
исправлен только assertion на assignable JsonException. Затем90/90, после optional
BOM и invalid DLL basename partitions final92/92. Source compile corrections:
missing Resolve/ReadbackOpenGLImage using и shadowed presentation-options lambda;
устранены до passing runs. Это не production behavioral RED.

260assertion sites: точные metadata/paths, original exceptions, ownership counts,
candidate ordering, forced race gates и immutable input. Quality review Strong;
actual mutation/coverage не заявлены. finding-tests IDE tool недоступен (Tool not
found). Один static pairing attempt остановлен NETSDK1004 на file-app cache
assets; installs/повторов/claims об отсутствии тестов не было. Research/plan/status
хранятся в OS temp TrueMoon-ED1a-tests-4bba6a9376ee44df814edb49c7889edc, не в Git.

### Full source, native и published validation

Final full solution2661/2661 Debug и2661/2661 Release,10TRX/configuration,
failed/skipped0: Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
UI661/Hosting85/Aether1008/Editor141. Nonincremental Debug0errors/1531warnings.
Current source hashes216; evidence:
TestResults/Projects-20261023/solution-final/summary.json, Completed=true.
Initial solution2661/config и initial consumers-122603087 также passed; после
визуальной проверки заменён внутренний project GUID в пустой scene label на
«Сцена не открыта»/«Проект не открыт», поэтому final runs выполнены заново.

~~~powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Projects-20261023/solution-final/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Projects-20261023/solution-final/Release
~~~

VerifyConsumers.ps1 сохраняет exact executable/arguments/cwd/exit codes и
process-local validation paths. Final native summary:
TestResults/Projects-20261023/consumers-123338696/summary.json.
11commands:1Release publish,4headless и6native cases. Published executable,
fonts/native assets и пользовательский project находятся в отдельном OS temp
вне repository, без Game.Logic.dll. Оба app deliveries запускаются с outside CWD.

- Headless Debug/published Release успешно читают один manifest с намеренно
  невалидным XML csproj: это доказывает отсутствие MSBuild evaluation/build при
  Open. SDK identity видна в JSON, selected session закрыта/active очищен,
  game assembly не загружена. Затем local global.json с999.0.100/rollForward:disable
  даёт ожидаемый exit1 и понятную SDK/PATH/global.json диагностику.4cases total;
  environment PATH/system SDK/registry не менялись.
- Empty workspace и .aetherproj workspace Debug/published Release:4native cases,
  по1PNG,0entities/0compiler/0game children; Build доступен только с project,
  остальные game actions недоступны. Standard-frame close закрывает project owner
  и UI; UI sessions/mappings/GPU resources/handles0.
- Legacy game workflow Debug/published Release: каждый8commands/3PNG/2reaped
  children, Build→Start→Play→Pause→Step→Build→Replace→Stop, selection/theme/resize
  и native close сохранены. Vulkan validation enabled0errors/0warnings и zero
  owned resources. Workspace cases не заявляют Vulkan validation, child там нет.
- Всего6native cases/10PNG/4reaped game children;32Editor source и8binary hashes
  сверены. PNG inspected для project workspace; показывается empty scene.
  Путь manifest/game/scenes/content и process profiles проверены независимо от repo CWD.

Итог TestResults/Projects-20261023/verification.json содержит source/binary/PNG
hashes,20TRX,focused92,local links/namespaces/checklist. Engine/runtime/wire и
production UI-library APIs не менялись; Editor IsPackable=false. Fresh packages,
independent preview drawing, full golden/RTE/performance/VRAM/physical UI и
hardware/final alpha gates этим шагом не повторялись. Packages не публиковались.
K7 workaround search/Windows/driver policy прежние.

Следом ED2 scene schema/document owner/Load/Save/SaveAs/dirty checkpoint и
Save/Discard/Cancel. Общий document/runtime switch/close transaction остаётся
открытым ED1 пунктом; интерфейс Open/menus — ED4, preview/game coordination — ED6.
Create пока оборачивает существующую игру; scaffold/wizard/user preferences позже.

## Edit preview: независимый engine profile — 2026-10-23

Следующий шаг после ARCH1b.1 — отдельный Edit preview без компиляции/игры.
[Контракт и пределы](PLAN.md#edit-preview-первый-engine-profile--2026-10-23).
AetherHost owns ScenePreview через injected IFactory<ScenePreview,FrameSession>;
Preview заимствует Frames. RuntimeRole.EditPreview выбирает PreviewId из Aether
assembly, built-in PreviewGameSession и тот же App runtime host. GameBuildLoader
при этом не создаётся, Game.Logic не читается/не загружается. Game entry/selected
build workflow сохраняется, unknown/несогласованный role/bootstrap отклоняется.
Новых assembly/protocol versions/DI scopes/IPC методов не добавлено.

ShowAsync сначала валидирует extent/transport/projection/revision/ownership.
Уже активная game session требует явного Stop. После preflight прежний owned
preview проходит drain/reap, новая generation получает immutable primitive scene.
Камера фиксирована, tick0; gameplay input/Step/Play отклоняются. Readiness —
фактический presented ticket exact runtime/surface/document revision. Describe/
Presented не переименовывают старый frame. Transform/Undo/Redo требуют явного
ShowAsync нового resolved snapshot. Resize/suspend/resume сохраняют document,
положительный resize снова ждёт presented ticket. Stop/Dispose fence и закрывают
admission до ожидания inflight work; original+cleanup causes доступны, retry
сохраняет ownership. Неиспользованный Preview не останавливает game workflow.

Это GPU-only Windows x64/OpenGL UI/Vulkan child profile, bounds16..1920×16..1080.
CPU reference pattern не представляет сцену и отклоняется до смены session.
Каждая revision заменяет child; incremental World/live edit не вводятся.
Normal Editor UI пока не подключён: native proof использует существующий
ViewportView fixture, Editor runtime bootstrap лишь научился маршрутизировать role.

### Requirement / test evidence

Новых121case: PreviewGameSession9, registration/ownership51, ScenePreview25,
GPU shutdown36. Оба providers включены там, где проверяется App graph.
Ни один существующий test не изменён.

| Требование | Точные тесты |
| --- | --- |
| Immutable render scene, transforms/materials, fixed camera/tick | `Load_CopiesPrimitiveTransformsMaterialsAndKeepsEarlierRenderSnapshots`; `Load_MaximumPrimitiveScenePreservesEveryOrderedPrimitive` |
| Revision привязана к actual presented ticket; authoring не меняется | `Show_BindsRevisionToBuiltInRuntimeAndActualPresentedTicketWithoutChangingAuthoring` |
| Transform/Undo/refresh заменяют children после reap, stale frame не retag | `Show_TransformUndoAndRefreshReplaceChildrenAfterReapAndNeverRetagOldFrames` |
| Preflight сохраняет рабочий preview | `Show_InvalidProjectionPreservesWorkingChildAndPresentation` |
| Cancellation до viewport allocation; разрешён retry | `Show_CancelledDuringSceneLoadReapsBeforeAnyViewportAllocationAndAllowsRetry` |
| Cleanup failure удерживает child до explicit Stop retry | `Show_FailedDeliveryCleanupRetainsChildUntilExplicitSuccessfulStopRetry` |
| Stop fences ignored-cancellation configure/presentation до фактического drain | `Stop_FencesLateConfigureAndPresentationUntilActualInflightShowHasDrained` |
| Borrowed Frames/game workflow остаётся рабочим | `Dispose_ClosesPreviewAdmissionButLeavesBorrowedFramesUsableForGameWorkflow`; `Show_DoesNotTakeOverOrStopAnExplicitGameSession` |
| Runtime factory не активирует loader; host rollback/borrowed dependencies | `RuntimeHost_UsesInjectedPreviewProductWithoutCreatingGameLoaderAndOwnsOnlyProduct`; `AetherHost_FailedLaterActivationRollsBackOwnedPreviewAndTransportWithoutStartingChildren` |
| Обычный Game role сохраняет Step/Play | `Protocol_DefaultGameRoleRetainsStepAndPlayBehavior` |
| Clean EOF только перед полным header; partial frame — fault | `BoundaryRead_CleanEofIsNullWhileStrictReadStillRejectsEofAndCancellation`; `BoundaryRead_TruncatedHeaderIsNeverCleanShutdown`; `BoundaryRead_TruncatedPayloadIsNeverCleanShutdown` |
| Malformed length/JSON и null JSON не трактуются как EOF | `BoundaryRead_InvalidLengthRemainsAProtocolFailure`; `BoundaryRead_MalformedOrUnmappedJsonRemainsAJsonFailure`; `BoundaryRead_NullJsonIsNotAnEndOfStreamMarker` |
| Только exact Win32 pipe-closed HRESULT; callback IO fault не подавляется | `PeerClosedFilter_AcceptsOnlyExactWin32PipeClosureHResults`; `Producer_CallbackIoFailureAlwaysFaultsWithOriginalIdentityRatherThanBecomingPeerShutdown` |
| Реальный suspended named pipe disconnect даёт successful worker и zero resources | `Producer_SuspendedClientDisconnectCompletesNormallyAndReleasesAllOwnedWork` |

Assertions используют exact scene/revision/identity/ticket, distinct matrices/colors,
original exception identity и resource ownership counts; races управляются gates,
bounded waits означают failure при timeout. Static assertion/gap review выполнен;
empirical mutation run не заявлен. finding-tests tool был недоступен, применены
existing sources и одно static Roslyn pairing исследование; это не coverage report.

### Команды и текущий full source run

Из src выполнены (exact invocation records и binlogs рядом с evidence):

~~~powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~TrueMoon.Aether.Tests.Preview.|FullyQualifiedName~TrueMoon.Aether.Tests.Gpu.GpuSurfacePipeTests' --logger trx --results-directory TestResults/EditPreview-20261023/shutdown-narrow '-bl:TestResults/EditPreview-20261023/shutdown-narrow-{}.binlog'
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/EditPreview-20261023/solution/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/EditPreview-20261023/solution/Release
~~~

Final focused134/134 =121новыйcase +13существующих GpuSurfacePipe cases,
failed/skipped0, exit0; TRX shutdown-narrow/MEGA6_PH-DEV_2026-10-23_15_57_59_net10.0.trx.
Предыдущий focused85/85 сохранён в narrow-final; сначала82case, затем добавлены
host rollback на обоих providers и early cancellation. Начальная compile ошибка
fixture (legacy IFactory.Create<TData> bridge) исправлена в новых tests; production
RED этим не заявлен.

Nonincremental Debug build0errors/1542warnings. Full solution **2569/2569 Debug и
2569/2569 Release**,10TRX/configuration, failed/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor49. Solution helper VerifySolution.ps1, summary.json,
20TRX/build logs и188source hashes — в TestResults/EditPreview-20261023/solution.
Warnings не подавлялись; результат не объявляет warning-free build.

### Native drawing и defect/failure history

Текущий final evidence: TestResults/EditPreview-20261023/consumers-110233808/
summary.json, Completed=true, Failure=null,6commands/4native cases/140source hashes.
VerifyConsumers.ps1 сохраняет exact exe/arguments/cwd/exit и process-local validation
environment, timeout/owned-child handling. Два publish commands, четыре app runs:

- Preview Debug apphost и published Release: по6PNG/5cooperatively reaped children.
  Revisions7→8→9→10, Transform x−1→+1, Undo→−1, Redo→+1; actual GL readback red
  centroid .41439→.59→.41439→.59. Затем resize800×450, suspend0×0,
  resume320×240, stale tickets/lower revision и close при pending first-present.
  Оригинальные640×360 screenshots/scene authoring сохранены, tick0, compiler не
  запускался, Game.Logic не загружался. Published app скопирован в OS temp вне
  repository без Game.Logic.*; report.NoGameFile=true.
- Runtime role1/buildnull/restorefalse проверены в пяти child reports каждого run.
  Vulkan validation enabled0errors/0warnings; created/released devices/surfaces/
  slots совпадают. Child stderr пуст, owned resources/handles/mappings/UI sessions0,
  imported image readback для delivery отсутствует.
- Прежний Editor Debug/published Release smoke: каждый8commands/3PNG/2reaped
  children, validation0/0 и zero owned resources. Это регрессия прежнего game
  workflow; она не заявляет integration нового preview в ordinary Editor UI.

Native debugging preserved in native-debug-*:
104558927 не прошёл из-за отсутствующего VK_LAYER_PATH; исправлен только environment
дочернего процесса с существующим TestResults/ENG4b/validation-layer/Bin.
104744243 и105027162 получили6PNG/5reaped children/zero resources, но child stderr
зафиксировал broken pipe на Retired reply и EndOfStream на следующем wire read.
Это actual RED: normal parent disconnect faulted producer и давал child exit1.

GpuWire получил отдельное nullable boundary read API: null только EOF до первого
байта четырёхбайтового header; strict ReadMessageAsync не изменил EOF semantics. Producer
нормально завершает read/write при exact HRESULT 0x8007006D/0x800700E8/0x800700E9,
не подавляя arbitrary IO/callback/native/protocol/cleanup errors. DestroySurface
остаётся в finally. Run105416402 прошёл после исправления, но его binary предшествует
final facility-prefix tightening; текущим proof считается только consumers run
после final full rebuild выше.

Consumers helper сначала искал cleanup.json вместо report.json; затем resume
упёрся в повторный Assets copy. Исправлены только helper paths/idempotent resume,
проверены прежние source hashes и сохранены только complete exit0 reports; недостающий
editor-published-Release и package proof выполнены. FailureHistory и stdout logs
сохранены, предыдущие failures не переписаны в passing results.

### Fresh packages и границы вывода

VerifyPackage.ps1 + PackageProgram.cs.txt выполнили22commands и8/8cases:
Debug/Release × build/publish × Cobalt/Microsoft DI. Version
0.2.2.7-alpha.preview.20261023110622747,9fresh nupkgs из10producer projects
(Cobalt generator входит в Cobalt);301source hashes.8runtime TrueMoon libraries
и build-only Enerit.Generator проверены в isolated feed/cache, consumer не имеет
ProjectReference. Actual UseAetherAuthoring + UseAether + Host.Preview используют
Apply/Show/stale ticket/Undo/Redo/suspend/resume/Stop,4owned runtime leases и
zero compiler activation; fake GPU presenter/runtime — managed API/composition
proof. Native drawing относится к отдельным apphost/published runs, не к package
consumer. Summary Completed=true:
TestResults/EditPreview-20261023/package-20261023-110622714/summary.json.
Итоговая read-only сверка source hashes/package payloads/docs/checklist —
TestResults/EditPreview-20261023/verification.json. Публикации не было.

ED1–ED8 checklist остаётся8stages/40unchecked tasks. Следом ED1a project/session
и ED2 document/save/reopen, затем подключение готовых authoring/preview к ordinary
Editor UI; ED5 tools/camera/picking ещё предстоят. Полный World/live edit/MCP,
golden/RTE/performance/VRAM/hardware/physical UI и alpha criteria этим не закрыты.
Windows/driver settings не менялись; поиск K7 workaround остаётся остановленным.

## ARCH1b.1: headless Transform authoring и история — 2026-10-23

По запросу «давай следующий шаг» реализован первый локальный API срез
[ARCH1b.1](PLAN.md#arch1b1-локальная-transform-authoring-session--2026-10-23).
UseAetherAuthoring регистрирует shared resolution и typed IFactory; отдельная
ISceneAuthoringSession владеет immutable document, resolved state, bounded
Undo/Redo и non-evicting terminal receipts. Ни окна, ни запущенный Runtime для
редактирования не нужны. UseAether сохраняет существующий graph; scene resolution
registration вынесена в общий helper для композиции в любом порядке.

Atomic Transform batches адресуют entity/component IDs, используют expected
revision/operation GUID/actor, проверяют local values, exact schema и полностью
resolved parent-composed transforms до single commit. Старые query snapshots,
catalogs и unrelated patches сохраняются. Undo/Redo увеличивают document revision
и восстанавливают ContentRevision. Limits/redo/duplicate/cancel/Dispose contract
подробно зафиксирован в PLAN и README/PACKAGE, а не продублирован в UI.

| Требование первого среза | Прямое evidence, namespace TrueMoon.Aether.Tests.Scenes.Authoring |
| --- | --- |
| Чтение и атомарное изменение всех9свойств по ID, immutable snapshots и unrelated data | SceneAuthoringSessionTests.Apply_PublishesCoherentNinePropertyBatchByStableIdsWithoutMutatingEarlierSnapshots; Apply_InstanceAddedTransformRetainsAddedDataAndRestoredComponentPresence |
| Invalid2nd target/duplicate/hidden/unsupported и invalid numbers/scales не публикуют часть batch | SceneAuthoringSessionTests.Apply_InvalidSecondTargetRejectsEntireBatchBeforeResolution; Apply_InvalidTransformRejectsWithoutPartialPublish |
| Exact schema, весь parent graph и float singular/overflow проверяются до commit | SceneAuthoringSessionTests.Resolution_ExactTransformSchemaRejectsFailedCreationWithoutPoisoningLaterValidEditing; Apply_WholeHierarchyValidationRejectsLocallyFiniteOverflowAtomically; Apply_FinitePositiveScalesWithSingularFloatDeterminantRejectAtomically |
| Cancel Apply/Undo/Redo до commit и unexpected failure не меняют content/history/receipt; retry безопасен | SceneAuthoringSessionTests.Cancellation_DuringCandidatePreparationPublishesNothingAndSameIdCanBeRetried; Preparation_UnexpectedFailurePreservesExceptionIdentityStateAndRetryableOperationId; Cancellation_PreCancelledNewIntentHasNoReceiptAndLateCancellationCannotUndoCommit |
| Revision conflict/concurrent requests и exact/changed operation ID | SceneAuthoringSessionTests.ConcurrentSameRevision_OneAtomicBatchWinsAndOneConflicts; Operations_RevisionConflictIsCachedAndDoesNotBecomeSuccessAfterStateCatchesUp; Operations_ExactRetryReturnsOriginalReceiptButChangedIntentConflicts |
| Bounded history, monotonic revision, restored checkpoint, new edit clears redo и zero-history profile | AuthoringHistoryTests.History_BoundsCombinedUndoRedoAndRestoresContentWithMonotonicDocumentRevisions; History_NoOpApplyIsTransactionAndClearsRedoWhileZeroLimitDisablesHistory; Revision_AtLongMaximumRejectsWithoutWrappingOrChangingContent |
| Journal at capacity сохраняет прежние success/error receipts; Undo/Redo retries не повторяют мутацию | AuthoringHistoryTests.Ledger_AtCapacityRejectsNewIdsWithoutEvictingSuccessOrErrorReceipts; Ledger_ExpectedEmptyHistoryErrorConsumesReceiptButCancellationDoesNot; History_ExactUndoRedoReceiptsAreStableAcrossLaterEditsAndCancelledRetries |
| Limits и frozen request boundaries, independent/closed products, failed creation и borrowed ownership | AuthoringContractsTests.Batch_FreezesCallerOrderAndAcceptsExact4096Boundary; Operation_RejectsInvalidIdentityRevisionAndActor; AuthoringFactoryRegistrationTests.Factory_CopiesEveryOptionBeforeCallerMutationAndProductsHaveIndependentOperationBudgets; Factory_ProductsHaveIndependentHistoryAndSnapshotsAndCloseWithoutDisposingBorrowedResolution; Factory_InvalidInitialScenePublishesNoSessionAndAllowsLaterValidCreation |
| App options snapshot/duplicate/lazy startup и Cobalt+Microsoft DI, UseAether в любом порядке | AuthoringFactoryRegistrationTests.App_CopiesAllAuthoringOptionsDuringConfigurationBeforeFactoryResolution; App_DuplicateOrExistingSessionFactoryFailsBeforeActivatingServices; App_AuthoringAloneProvidesSingletonFactoryAndIndependentSessionsWithoutStart; App_AetherCompositionInEitherOrderSharesInjectedResolutionWithoutCreatingRuntime |
| Authoring не связан с preview limit64/material policy | SceneAuthoringSessionTests.Apply_HeadlessAuthoringDoesNotImposePreviewEntityOrMaterialBounds |

Focused final run: **Passed:87, Failed:0, Skipped:0**, exit0:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes.Authoring. --logger trx --results-directory TestResults/ARCH1b-20261023/narrow-clean '-bl:TestResults/ARCH1b-20261023/narrow-clean-{}.binlog'
```

TRX: TestResults/ARCH1b-20261023/narrow-clean/MEGA6_PH-DEV_2026-10-23_15_17_57_net10.0.trx.
Первый prefix run130содержал84новых cases и46старых AuthoringModelTests; после
review добавлены3прямые проверки, final87 использует строгий namespace filter.
Промежуточный exact-schema test намеренно подменял каталог injected resolver
вопреки SceneDocument: это ошибочный collaborator oracle, не поддержанный contract.
Оставлен корректный failed initial schema/Create и subsequent valid editing proof;
провал87run(86pass/1fail) не выдаётся за production defect или passing evidence.
IDE findTests недоступен; использованы repository tests и один bounded Roslyn
pairing scan. Static pairing не означает coverage; coverage/performance не измерялись.

Финальный nonincremental Debug solution build: exit0,0errors/1522warnings.
Full Debug **2448/2448** иRelease **2448/2448**,10TRX/configuration, failed0/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether887/Editor49.139Aether production/test source fingerprints не изменились
во время проверки. Команды helper TestResults/Authoring-20261023/VerifySolution.ps1:

```powershell
dotnet build TrueMoon.slnx --no-restore --no-incremental -c Debug -v minimal
dotnet test TrueMoon.slnx --no-build --no-restore -c Debug --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Authoring-20261023/solution/Debug
dotnet test TrueMoon.slnx --no-restore -c Release --blame-hang --blame-hang-timeout 60s --logger trx --results-directory TestResults/Authoring-20261023/solution/Release
```

Fresh package proof: **8/8** = Debug/Release × build/publish × Cobalt/Microsoft DI.
Созданы9nupkgs из10producer projects, isolated feed/cache и8runtime TrueMoon
package dependencies; Enerit.Generator нужен producer, не transitive runtime
consumer. Version0.2.2.7-alpha.authoring.20261023102244607;22commands,299source
fingerprints, только PackageReference без ProjectReference. Consumer реально
выполняет Apply/conflict/duplicate/Undo/Redo/Dispose и App start/stop без runtime.
Final Completed=true:
TestResults/Authoring-20261023/package-20261023-102244573/summary.json.
Helper: TestResults/Authoring-20261023/VerifyPackage.ps1; ничего не публиковалось.

Два предварительных package runs сохранены: package-20261023-102016044 остановился
на неверном helper ожидании9runtime libraries вместо8 (build-only generator);
package-20261023-102117026 — CS1061 из-за пропущенного using TrueMoon.Dependencies
в consumer example. Исправлены проверочный helper и README/example, production
API не менялся. Passing evidence — только указанный final fresh run.
Финальная сверка:9Markdown documents/498local links/260anchors без ошибок,
11new source namespaces соответствуют путям, git diff HEAD --check прошёл.
ED1–ED8/40unchecked tasks сохранены.139solution и299package fingerprints совпали
с текущими исходниками;32сравнения deployed DLL с lib/net10.0 payload из nupkgs
прошли. Aggregate Completed=true: TestResults/Authoring-20261023/verification.json.

Native viewport и physical UI этим API срезом не изменялись/не повторялись.
ARCH1b в целом, ED1/ED2 persistence, editable UI, независимый preview и live World
не завершены. Следующий engine шаг — Edit preview, ED1a project/session параллельно.

## API перед UI и выбор Enerit IPC — 2026-10-23

По обсуждению с пользователем уточнён порядок первого рабочего среза Editor:
общие project/scene contracts → минимальный ARCH1b scene query/Transform mutation/
history/Undo/Redo → headless validation/conflict/duplicate/cancel/atomicity proof →
независимый Edit preview → подключение editable inspector/tools. ED1a project/
session выполняется параллельно. Полный generic World/ECS и live editing ARCH1c
не являются предварительным условием. Authoring/history имеют общего владельца
в Aether, Editor не создаёт вторую mutation/history реализацию; Play автоматически
не записывает изменения обратно в документ.

Enerit явно выбран для IPC Editor↔Runtime: typed invocation/generated adapters,
same-build named pipes как текущая основа, control/input/query/results/progress/
errors и frame descriptors. Pixels/shared GPU images остаются отдельным frame
transport; launch/stop/reap — у Mithril. Новые preview/edit invocation contracts
запланированы, wire versions/limits и transport readiness этим не расширяются.

Синхронизированы engine/editor, Enerit, общие и UI планы/статусы. Изменена только
документация; production code/API не менялись, build/tests/native/packages не
запускались. Проверены14documents/428local links/16added anchors: missing targets/
anchors0, ED1–ED8/40unchecked tasks сохранены, source changes0, git diff --check
прошёл. Evidence: TestResults/AetherApiPlan-20261023/verification.json.
Предыдущие passing результаты сохраняют свои даты и область вывода.

## План рабочего приложения Editor — 2026-10-22

По согласованному обсуждению создан EDITOR_PLAN.md в направлении Aether.
ED1–ED8 охватывают project/session, document persistence, общие authoring operations
и undo, панели/inspector/commands, Edit preview/tools, game workflow, extensions и
recovery/delivery. Первый цельный срез: Open scene → Transform edit → preview →
Undo/Redo → Save → reopen. Следующий конкретный шаг — ED1a project description и
ProjectSession; gameplay collisions сохраняют отдельную точку продолжения.

Зафиксированы функциональная организация кода, единый owner документа/history,
View/ViewModel/style и App/DI factory lifetime, различие UI commands/authoring
mutations, draft/commit/cancel и отдельные authoring/runtime states. Editor
extensions и custom UI controls имеют разные границы; интерфейсы вводятся по
первым потребителям, dynamic plugin loader/полная замена Element не обязательны.

Engine PLAN, общий PLAN/STATUS, UI PLAN/STATUS, индекс документации, README Editor
и AGENTS ссылаются на один прикладной checklist. Общие authoring contracts остаются
в ARCH1, controls/runtime UI — у Alloy/Argentis. Это документационная работа:
production code и public API не менялись, build/tests/native/packages не запускались;
предыдущие passing результаты сохраняют дату и область вывода. Проверяются
markdown links/anchors и git diff whitespace. Итоговая сверка:
11documents/374local links/5added anchors, missing targets/anchors0;
ED1–ED8 и40unchecked tasks присутствуют, неожиданных source changes0,
git diff --check exit0. Evidence: TestResults/EditorPlan-20261022/verification.json.

## Единые фабрики и DI — 2026-10-22

По следующему запросу пользователя после аудита реализованы DI1–DI4 в Aether,
Aether.Editor и используемых путях Enerit/Mithril/Alloy. Default Cobalt и Microsoft
DI adapter сохранены; runtime/generator Cobalt не менялись. Новые общие
IFactory<T,TArguments>, Factory adapters и AppServiceLifecycle находятся в Contracts,
без dependency на provider. Типизированные factories сохраняют legacy bridge;
аргументы проверяются до callback, ошибки callback не оборачиваются.

AetherHostFactory получает дочерние factories через DI, передаёт готовые
зависимости host и откатывает частичное создание в обратном порядке, сохраняя
original/cleanup causes. Aggregate остаётся единственным владельцем. App options
поддерживают typed viewport factory; runtime leases и CPU receivers также создаются
через IFactory. SceneResolutionService использует внедрённую catalog factory на
каждую revision; standalone SceneResolver adapter сохранён.

Editor workspace/binding/commands и owner-thread root factory регистрируются в App.
UseAetherRuntime после UseAether фиксирует launch, допускает factory overrides и
создаёт owned graph parent/game/producer/protocol/IPC. AetherRuntimeHost borrows
App/GPU, закрывает admission, отменяет/drains workers и освобождает игру после
завершения работы. Failed cleanup наблюдаем/retryable; aliases не владеют target.
Enerit имеет generic pipes factories/common App factory adapters. Mithril injects
UnitHandleFactory и snapshots; IUnitsController/lifecycle aliases — borrowed facades.
Alloy→Contracts добавлен для typed session bridge; Hosting предоставляет
UsePresentationFactory и non-owning service/lifecycle facades.

Game loader использует IFactory<IGameSession,Type>; штатный game entry реализует
IGameSessionEntry.static Create(IServiceResolver?), явно создаёт игру с constructor
dependencies, не используя Activator/constructor reflection fallback. Runtime
services заимствуются, game/context/artifact pin принадлежат loader. Generic static
dispatch не кеширует collectible types. Shared Aether и Contracts исключены из
private dependencies. HostId/ContractHash учитывают обе assembly. **Миграция:**
добавить entry factory и non-copying TrueMoonContractsPath reference наряду с
AetherContractPath; host/runtime/game и прежние artifacts пересобрать вместе.
Wire versions прежние. Manual game/project/compiler fixtures обновлены.

### Проверки и артефакты

Текущий output root: `TestResults/AetherDI-20261022-204227` (далее `$diDir`).
SDK10.0.402/net10.0/Windows x64. Все MSBuild commands сохранены с уникальными
binlogs; transient artifacts исключены из version control.

```powershell
$diDir = Get-Content TestResults/aether-di-current.txt
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory "$diDir/debug-verified" "-bl:$diDir/debug-verified-{}.binlog"
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory "$diDir/release-verified" "-bl:$diDir/release-verified-{}.binlog"
dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --configuration Release --output "$diDir/publish" "-bl:$diDir/editor-publish-{}.binlog"
```

Оба full runs exit0: **2293/2293/configuration**, по10TRX, failed0/skipped0.
Core117/Cobalt179/generator91/Enerit126/generator114/Mithril99/UI643/Hosting85/
Aether800/Aether.Editor39. Publish exit0. Добавлены40cases в17methods; read-only
assertion/gap review привёл к двум усилениям: реальный non-null runtime service и
независимый disposal counter frame-owned transport. Точные requirements/evidence:

| Требование | Проверка |
| --- | --- |
| Factory products caller-owned, typed/legacy/null/error contracts | FactoryTests.RegisteredAdapters_CreateSeparateCallerOwnedResults и typed bridge/argument/failure cases |
| Единственный disposable owner при lifecycle aliases | FactoryTests.BorrowedLifecycleAliases_ForwardAppCallsAndDisposeOwnerExactlyOnce |
| Injection и rollback aggregate с original+cleanup causes | AppIntegrationTests.RegisteredCompilerFactory_SuppliesHostDependencyAndTransfersOwnership; HostFactoryFailure_RollsBackAllAcquiredDependenciesAndPreservesCleanupErrors |
| Runtime precondition/duplicate/snapshot/overrides | RuntimeRegistration_RequiresAetherAndRejectsDuplicatesBeforeCreatingServices; RuntimeRegistration_UsesInjectedFactoryWithNormalizedLaunchSnapshot |
| Authoring factory на каждую revision | AuthoringResolution_UsesInjectedCatalogFactoryForEveryRevision |
| Game entry runtime DI/errors/pins | GameBuildTests.ExplicitGameEntry_InjectsBorrowedRuntimeServiceIntoCallerOwnedSessions; ExplicitGameEntry_PropagatesOriginalInjectedFailureAndReleasesPins; SelectedGameFactoryFailure_ReleasesPinsAndPreservesTypeAndError |
| Handle configuration snapshot/ownership/failure | UnitCompositionTests.InjectedHandleFactory_UsesSnapshotsAndControllerOwnsEveryResult |
| UI argument/clipboard/root/session ownership | HostedUiSessionFactoryTests.CommonFactoryBridge_PreservesCreationArgumentsAndSessionOwnership; CommonFactoryBridge_InvalidArgumentsDoNotTakeRootOwnership |
| Provider-neutral Alloy dependency boundary | CoreBoundaryTests.Alloy_AssemblyReferences_ContainOnlySharedContractsArgentisAndBcl |

Итоговые source Debug apphost и published Release запускались RunNative.ps1 с
`--smoke <fresh-directory> --validation`, VK_LAYER_PATH только в собственном
process tree. `native-debug-final`/`native-release-final`: оба exit0, каждый
8commands/3PNG/9material pixel checks/2reaped runtime children; zero UI sessions,
mappings/GPU resources/handles, disposed view. Каждый из4runtime reports:
Vulkan core/sync0errors/0warnings,1device/1surface/3slots created=released,
LiveSlots/OpenHandles0. Game.Logic пересобиралась compiler с обеими shared refs.

Первый full Debug выявил старые probe assumptions: reflection над implementation
через borrowed IUnitsController и поиск concrete parent среди lifecycle aliases;
Alloy allowlist ещё запрещал новую согласованную Contracts dependency. Probes
получают concrete owner из DI для inspection, интерфейсные calls сохранены;
boundary test требует Contracts и запрещает любые concrete providers/renderers.
Следующий run оставил2cancel-start failures из-за Resolve после disposed provider;
owner теперь фиксируется до disposal. Итоговый Debug выше прошёл целиком.
Первый native helper сам создал directory, которое smoke обязан создавать свежим;
helper исправлен, subsequent actual runs exit0. Эти failures сохранены в logs,
не скрыты passing итогом. Первичные compile issues typed handler/nullable bridge/
namespace исправлены до final runs.

Fresh isolated package matrix: `ValidatePackageFinal.ps1`/`VerifyPackageFinal.ps1`,
output `aether-package-final`, версия`0.2.2.7-alpha.aetherdi.20261022162035190`.
**14fresh nupkgs/20commands/4consumer runs**, все exit0: build Debug, publish Release,
каждый binary на Cobalt и Microsoft DI. Собственные feed/cache и packageSourceMapping
исключают подмену TrueMoon локальным историческим package. Consumer имеет только
PackageReference, использует typed viewport factory, проверяет common UI factory API,
runtime/lease/GPU/viewport exactly-once ownership, terminal admission и zero mappings.
Nuspec Vulkan2.23.0, actual Aether/Skia/native payload hashes и package immutability
проверены. Первая матрица также прошла, затем финальная повторена после обновления
packaged Alloy/Hosting README. Пакеты не публиковались.

`VerifyFinal.ps1` повторно сверил все20TRX/2native runs/4runtime reports/package
summary и записал `verification.json`. Source namespace check:7production projects,
257C#sources/235namespace declarations, нарушений0.22изменённых documents,
413local links, missing targets0; `git diff --check` exit0. Это path-link проверка,
не полный аудит всех исторических anchors. Полный source/structural matrix всего
репозитория и nonincremental build отдельно не повторялись.

Эти проверки не повторяют performance/allocations, golden/RTE, physical DPI,
device loss/VRAM, K3/K7/K8/final alpha, полный engine native60 или все independent
consumer matrices. Функциональные ENG5c/ENG7/World/edit/MCP backlog остаются.

## Переименование редактора и аудит DI — 2026-10-22

По запросу пользователя переименованы TrueMoon.Editor/Tests в
TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.Tests: каталоги/csproj, default assembly
names, namespaces, ProjectReference, CLI help, README и текущие документационные
ссылки. В точном TrueMoon.slnx оба проекта находятся в `/Aether/`; отдельная группа
`/Editor/` устранена. Listing проверен сразу после изменения. Window title теперь
TrueMoon Aether Editor. Исторические имена в предыдущей записи ниже сохранены;
её ссылка на README обновлена. Внешним consumers нужны новые using/references
и пересборка. UI остаётся отдельным application, engine library не получает UI.

Аудит production Aether и обеих process branches редактора показал **частичное
соответствие** правилу Cobalt/IFactory. Activation/lifetime contracts не менялись;
аудит не считается исправлением. [Замечания и единственный план DI1–DI4](PLAN.md#переименование-редактора-и-аудит-di--2026-10-22):

- AetherHost.cs:17–38 вручную создаёт Supervisor/Frames/Builds/Replacement/Editor/
  Gpu. Нужны зависимости/внедрённые factories при сохранении единственного владельца,
  stop/drain order и cleanup retry; disposable aliases добавлять нельзя.
- SessionSupervisor.cs:45/AetherOptions.cs:30–34 используют IRuntimeFactory/Func;
  общий IFactory<T> в production Aether/Editor не используется. Runtime factory
  уже внедрён, но унификация контрактов ещё не выполнена.
- GameBuildLoader.cs:25 создаёт IGameSession через Activator, без constructor DI.
  Требуется factory activation внутри child/artifact/AssemblyLoadContext boundary,
  без загрузки Game.Logic в Editor и без нового reflection fallback.
- EditorApplication.cs:23–45/RuntimeEntryPoint.cs:34–41 вручную собирают workspace/
  binding/commands и runtime protocol/frame producer/IPC. Captured dependencies
  не становятся DI services лишь из-за UsePresentation root callback.
- SceneResolver.cs:25–28 создаёт EntityResolver внутри статического authoring
  pipeline; для системного использования нужна service/factory boundary.

Существующие правильные границы: default Cobalt в App, snapshots/duplicate guards
UseAether, constructor dependencies GameEditor/GameReplacement, отсутствие owned
disposable aliases. IServiceResolver/DI Resolve ограничен registrations и process
bootstrap; EntityResolver.Resolve — доменный алгоритм, не service locator. DTO,
UI elements и low-level resources внутри их владельцев сами по себе допустимы.

### Проверки переименования

Windows x64/SDK10.0.402, net10.0/xUnit2.9.3/VSTest; ignored evidence:
`TestResults/AetherEditorRename-20261022-195846/`.

```powershell
dotnet sln TrueMoon.slnx list
$run = 'TestResults/AetherEditorRename-20261022-195846'
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory "$run/debug" "-bl:$run/debug-{}.binlog" --verbosity minimal
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory "$run/release" "-bl:$run/release-{}.binlog" --verbosity minimal
dotnet publish TrueMoon.Aether.Editor/TrueMoon.Aether.Editor.csproj --configuration Release --output "$run/publish" "-bl:$run/publish-{}.binlog" --verbosity minimal
```

Каждый test run: **Passed:2253, Failed:0, Skipped:0**,10complete TRX:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI643/Hosting79/
Aether780/Aether.Editor39. Новая test assembly обнаружена как
truemoon.aether.editor.tests.dll. Release runner/publish exit0; отдельные binlogs
Debug/Release/publish, stdout/stderr и release.exitcode сохранены. Nonincremental
build и independent package matrix не повторялись.

Direct Debug и published Release apphost `TrueMoon.Aether.Editor.exe` с
`--smoke <fresh-dir> --validation`: `native-final-Debug`/`native-final-Release`,
оба exit0/Passed=true. В каждом8UI commands/3PNG/9material pixel assertions,
selection/theme/resize, Build/Start/Play/Pause/Step/Build/Replace/Stop,
2separate reaped runtime children, view disposed/UI sessions/mappings/GPU resources/
handles0. Runtime reports подтверждают enabled core/sync validation0errors/
0warnings including teardown. VK_LAYER_PATH только для собственного process tree:
existing `TestResults/ENG4b/validation-layer/Bin`; Windows/driver settings прежние.

Первый helper ошибочно трактовал недоступный ExitCode у Start-Process как failure,
хотя Debug smoke report прошёл. Исходные logs/report сохранены; final helper
использует Diagnostics.Process/redirected streams и сохраняет фактический код.
Оба final runs повторены, exit0; helper failure не считается product regression.

Структурная проверка четырёх Aether projects:135C# sources/133namespace declarations,
нарушений0; все ProjectReference paths существуют, editor projects ровно по одному
в `/Aether/`, старого namespace в исходниках нет. Hashes/locations сохранены в
`structure-and-sources.json`. Passing tests/native подтверждают переименование,
но не устранение замечаний DI1–DI4. Physical DPI/K3/K7/K8/VRAM/performance/release
ограничения сохраняются. Итоговые whitespace/doc link checks выполнены отдельно.

## ENG7a: основной экран редактора — 2026-10-22

Запрос: «давай теперь возможно сделаем основной экран редактора? я думаю это
должно быть окно без системного оформления с нашим стандартным стилем + в центре
вьюпорт, и грид со свойствами и кнопками управлениями».

Созданы **TrueMoon.Editor** (desktop WinExe/net10.0/IsPackable=false) и отдельный
**TrueMoon.Editor.Tests** (xUnit). Оба зарегистрированы в точном TrueMoon.slnx;
`dotnet sln TrueMoon.slnx list` проверен сразу после wiring и в финале. В engine
assembly ничего не переносилось и новая engine сборка не создана.

Application использует App.UseAether + UsePresentation explicit root factory:
default custom WindowFrame, toolbar, список7demo объектов слева, живой3D viewport
по центру, read-only two-column property inspector справа и state/error presentation.
Выбор обновляет ID/XYZ/interactive/geometry/RGB и сохраняется при theme/Stop/Replace.
Build/Start/Play/Pause/Step/Stop/Replace/Cancel используют существующий GameEditor.
Stop/Cancel допускаются поверх pending command, close fences admission и ждёт
Stop/drain; после failed cleanup admission открывается для retry. UI events не
блокируются на IPC/compiler. WinExe не открывает дополнительную консоль.

NativeRuntimeFactory запускает отдельный процесс того же application binary с
`--runtime`. RuntimeEntryPoint — application composition существующих
RuntimeProtocol/GameBuildLoader/FrameProducer/GpuResourceHost/Enerit/Mithril;
game code загружается только в child. References на ManualTests executable нет.
В source checkout входным примером является внешний Game.Logic.csproj/RoomGame;
другой project/assembly/type задаются CLI. UI owns retained Skia copies, Aether
owns transport/imports, borrowed GL context удерживается до successful drain.
GL UUID query повторяет существующий read-only contract и проверяет owner/context/
процедуры/число устройств/error; native buffer16bytes, Windows x64.

[Запуск и ограничения](../../TrueMoon.Aether.Editor/README.md),
[единственный подробный scope/acceptance](PLAN.md#eng7a-основной-экран-редактора--2026-10-22).
Инспектор пока authoring SceneSnapshot и read-only во всех режимах. Список плоский,
render surface960×540 с aspect-preserving layout. Editor camera/gameplay input,
editable components/project persistence/tree/picking/gizmos/docking и полноценный
runtime World snapshot/edit/MCP — последующие этапы. Отдельная runtime executable
не поставлена; process isolation обеспечена отдельным `--runtime` процессом.

### Текущие проверки ENG7a

Windows x64/SDK10.0.402.39новых xUnit cases: workspace state/selection/properties/
error precedence, command serialization/interruption/close/retry/cancellation,
layout/readonly/theme/disposal и default frame на logical DPI1/1.5/2.

| Требование | Evidence |
| --- | --- |
| «окно без системного оформления с нашим стандартным стилем» | `EditorScreenTests.DefaultFrame_LayoutUsesLogicalUnitsAndSettlesWithoutRedraw`; native apphost screenshots/default frame, direct WinExe startup |
| «в центре вьюпорт» | `EditorScreenTests.ShellGeometry_ReservesToolbarInspectorAndStatusAroundCentralViewport`, `ShellGeometry_SmallBoundsStayFiniteNonnegativeAndClipped`; `EditorSmoke.RunAsync`9independent material pixels/configuration из child3D frame |
| «грид со свойствами и кнопками управлениями» | `EditorScreenTests.HierarchyInput_UpdatesReadOnlyGridAndThemePreservesSelectionAndFocus`, `CommandButtons_DispatchOnlyEnabledCommandsAndBusyStateCanStopOrCancel`; native UI selection and8commands/configuration |
| State/candidate availability | `EditorWorkspaceTests.CommandAvailability_FollowsAcknowledgedRuntimeAndCandidate`13literal expected command sets, `ClosedEditor_DisablesEveryCommandAndInvalidEnumIsRejected` |
| Empty/invalid/snapshot/format/error behavior | `EditorWorkspaceTests.EmptyScene_HasNoSelectionOrRowsAndRejectsUnknownEntity`, `Selection_ChangesInspectorOnceAndInvalidSelectionPreservesIt`, `Inspector_UsesImmutableAuthoringSnapshotAndInvariantNumbers`, `Refresh_ReportsActualPidTickAndErrorPriorityWithoutResettingSelection`, `Refresh_ErrorFallbackKeepsBuildAndRuntimeFailuresVisible` |
| Pending command/close/retry | `EditorCommandsTests.PendingWork_RejectsAnotherNormalCommandButAllowsInterrupt`, `Close_FencesNewWorkAndWaitsForAcceptedCommandAfterStop`, `FailedClose_ReopensAdmissionForCleanupRetry`, `Failure_IsObservedAndNextAcceptedCommandClearsIt`, `Cancellation_IsObservedWithoutAUserError`, `NormalCommand_ReceivesLifetimeAndInvalidCommandsNeverReachTheApi` |
| Ownership/cleanup | `EditorScreenTests.InvalidShellChildren_AreRejectedBeforeAdoption`, `Disposal_UnsubscribesWorkspaceAndKeepsTheSharedSceneUsable`; native runtime/UI/mappings/GPU resources/handles0, reaped children |

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m --verbosity quiet`:
  `TestResults/EditorScreen/build-desktop-final.log`, exit0. Existing repository
  warnings сохранены; новый Editor project ранее собран без warnings/errors.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EditorScreen/tests-desktop-debug --verbosity quiet`
  и аналог Release в `tests-desktop-release`: **2253passed/configuration**,
 10TRX/failed0/skipped0. Editor39/Aether780/UI643/Hosting79/Mithril95/Core107/
  Cobalt179/generator91/Enerit126/generator114. New project discovery не нулевая.
- Direct Debug WinExe apphost и published Release WinExe apphost с
  `--smoke <fresh-dir> --validation`, VK_LAYER_PATH из existing ENG4b validation
  layer. Reports: `TestResults/EditorScreen/native-apphost-Debug/report.json` и
  `native-apphost-Release/report.json`. Оба Passed=true:8actual commands
  (Build,Start,Play,Pause,Step,Build,Replace,Stop),3PNG,9material pixel assertions,
  selection, same UI root/window, theme/resize, preserved paused tick/selection,
  close/drain;2children/configuration имеют отдельные PID и reaped/released.
- Runtime cleanup JSON для каждой generation: validation requested/enabled,
  **core/sync0errors/0warnings including teardown**,1device created/released,
 3slots created/released, live slots/resources/handles0. Parent report:
  UI sessions/mappings/GPU resources/handles0, view disposed. Published
  deployment — framework-dependent Windows x64; NuGet publication не выполнялась.
- PNG Dark/Light и resized просмотрены. После просмотра укорочено имя blue cube,
  label «Интерактивен» уменьшен до font14 и footer переведён на русские состояния;
  финальные apphost screenshots используют эти изменения.

Промежуточные попытки сохранены: initial scaffold build отверг отсутствующий
ещё не созданный EditorSmoke; следующий build поймал ошибочный overload локального
Until и missing readback namespace. После исправления build прошёл. Native
`native-initial` failed из-за диагностического readback внутри UiSession.Invoke
во время Update; захват перенесён в stable FramePresented boundary. `native-second`
прошёл без validation. Затем final Debug/published Release и оба direct WinExe
apphost запуска повторены с enabled validation, все прошли. Failed попытки не
считаются passing evidence; защиту renderer не ослабляли.

Assertion/gap review: независимые literal command sets, exact Rect/values/colors/
selection/caret/offset/ownership, task gates вместо sleeps в unit tests. Добавлены
error fallback и failed-close retry cases по найденным gaps. Roslyn source pairing
запущен один раз перед тестами; это static pairing, не measured coverage. Research/
plan/status сохранены только в temporary testagent directory. Mutation/coverage
score не заявлен.

Предыдущие engine native60cases, UI golden/package/RTE/performance/hardware
матрицы этим запуском не повторены. ENG5c.3 collisions, full toolset, physical
DPI/OS input/K3/K7/K8/VRAM/final alpha остаются открытыми; NVIDIA workaround не
возобновлялся. Следующий локальный editor этап — authoring property editing
через согласованные Entity/Scene API с validation/undo; Play readonly сохраняется
до snapshot transactions.

## ENG5c.2: reach/raycast и interaction — 2026-10-22

Запрос: «давай приступим к следующему шагу»; точка продолжения — ENG5c.2.
В Aether добавлены stateless PrimitiveRaycast/InteractionBody/InteractionHit,
immutable GameObjectStates/GameObjectState и явный TMCK2 codec/profile.
Game.Logic.RoomGame использует bounded reach4, actual affine geometry/occlusion
и независимые переключатели объектов. Изменённый цвет наблюдаем в GPU frame,
editor authoring snapshot/geometry не меняются. [Контракт/следующий шаг](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
[публичный профиль](../../TrueMoon.Aether/README.md#reach-и-состояния-объектов).

Ray query — до64distinct opaque bodies, finite reach(0,1000], inclusive boundary;
inverse-transformed unit-box slabs сохраняют world distance, rotation/nonuniform
scale/shear. Local direction не нормализуется, near-parallel не заменяется zero.
Closest surface блокирует цель даже при Interactive=false; exact ties предпочитают
blocker, затем ordinal ID. Inside/on-surface distance0. E применяется после camera
input на fixed tick, только eligible hit меняет Boolean switch/материал. Active
RGB(1,.8,.1), повторное нажатие возвращает исходный RGB; остальные объекты независимы.

SnapshotVersion=2, control3/frame1 прежние. TMCK1/future schemas отвергаются;
matching Editor/Runtime/Aether/Game.Logic требуют rebuild. Base offsets через
scene ID сохранены; appended UInt16count и ordinal UTF8length/ID/flag records,
общий SHA256, FixedBytes149/max64KiB. До64unique IDs, строгие count/length/UTF8/
canonical order/flag/trailing checks. GameObjectStates deep-copies и даёт value
equality через capture/codec/restore. RoomGame проверяет exact loaded eligible-ID
set и Interaction=Any(Active) до atomic commit camera/tick/objects. Legacy demos
ProbeGame/CameraGame отвергают nonempty Objects без mutation. Runtime readiness
остаётся fenced при RestoreRejected, input transient и не входит в checkpoint.
Это минимальный game-owned switch checkpoint, не full World snapshot/live editing.

52новых xUnit cases: PrimitiveRaycastTests16, ObjectCheckpointTests24,
RoomInteractionTests12. Первые итерации тестов: compile failure из-за пропущенных
required GameInputTick arguments; после исправления96pass/1fail — invalid surrogate
в InlineData нормализовался metadata. Runtime-created surrogate проверяет нужную
ошибку; focused97/97, после gap review101/101. Production expectations не ослаблены.

Full nonincremental Debug build прошёл; первый full solution Debug2185/2185 и
Release2185/2185, по9TRX/failed0/skipped0: Aether779, UI615, Hosting79, Mithril95,
Core107, Cobalt179/generator91, Enerit126/generator114. Прежние/generated CS1591
warnings не подавлялись. Coverage/mutation percentages не измерены.

| Requirement | Evidence: exact xUnit methods |
| --- | --- |
| «ограниченный запрос взаимодействия из камеры» / «явной максимальной дистанцией» | `PrimitiveRaycastTests.ExactReach_IsInclusiveWithAdjacentFloatingPointBoundaries`; `EmptyMissParallelAndBehind_DoNotInventTargets`; `BodyCount_IsBounded` |
| «ближайшему eligible объекту» / «occlusion policy» | `NearestSurface_WinsRegardlessOfEnumerationAndOpaqueDisabledBodyBlocksTarget`; `Ties_PreferBlockerThenOrdinalIdIndependentlyOfInputOrder`; `InsideOrOnSurface_HitsAtZeroAndDoesNotSeeThroughBody` |
| Actual affine geometry | `AffineBoxes_KeepWorldDistanceAndRejectFalsePositiveWorldAabbHits`; `NearParallelAndPitchedRays_KeepSmallNonzeroDirectionAndCameraOrientation` |
| «E действует только на найденную цель и даёт наблюдаемый результат» | `RoomInteractionTests.Interact_ChangesOnlyNearestTargetMaterialAndKeepsIndependentObjectStates` |
| «вне reach, заслонённую/отключённую цель» | `UnreachableOccludedDisabledAndMissedTargets_DoNotToggleAnything` (4cases) |
| «input rising edge» / «восстановление состояния» | `RuntimePulseAndFreshRestore_PreserveCompleteObjectStateAndDoNotReplayInput` |
| «атомарность capture/restore/Replace» | `InvalidObjectSetRestore_IsRejectedBeforeAnyCameraTickOrStateMutation` (3); `RuntimeWrongObjectSet_IsRejectedWithoutMutationAndKeepsFramesFenced`; `InvalidCameraAndTickOverflow_LeaveCompleteRoomStateUnchanged`; `LegacyGames_RejectPerObjectStateWithoutPartialRestore` |
| «versioned checkpoint» | `ObjectCheckpointTests.CountAndUtf8Boundaries_RoundTripCompleteDetachedState` (0/1/64/65); `RehashedMalformedObjectSectionsAndOldSchemas_AreRejected` (12); `States_AreCanonicalCopiedAndValueEqualAcrossIndependentCaptures`; `SnapshotNegotiation_RequiresV2WhileControlAndFrameRemainCurrent` |
| Diagnostic JSON compatibility | `ObjectCheckpointTests.DiagnosticJson_RoundTripsValidatedStateWithoutWeakeningImmutability`: complete state/value equality, detached storage, duplicate-ID rejection |

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~Interactions|FullyQualifiedName~ObjectCheckpointTests|FullyQualifiedName~CheckpointCodecTests|FullyQualifiedName~RoomSceneTests|FullyQualifiedName~GameInputProtocolTests' --logger trx --results-directory TestResults/ENG5c2/focused-reviewed --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --logger trx --results-directory TestResults/ENG5c2/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c2/final-Release --verbosity quiet
```

Research/plan/status и Roslyn pairing — OS temp `truemoon-eng5c2-testagent-20261022`.
Dedicated generator/IDE findTests недоступны по предыдущей проверке; применены
inline Research→Plan→Implement, deterministic static pairing и direct code reads.
Whole-src pairing включает historical TestResults copies и не является coverage.
Assertion/gap review добавил near-parallel, count-zero trailing, object-set runtime
fence и tick-overflow/camera atomicity cases; clean focused101/101 подтвердил их.

Первая полная native-матрица `TestResults/EngineSessions/eng5c2-final` остановилась
на Debug-editor-gpu: старый diagnostic JSON consumer десериализует GameCheckpointState,
а IEnumerable constructor parameter GameObjectStates не соответствует ImmutableArray
property type для System.Text.Json. Предшествующие interaction/room/scene/input/
GPU import/surface/viewport cases прошли; весь запуск не считается passing.
Добавлен private JsonConstructor с exact ImmutableArray binding, делегирующий тому
же validation/copy path; публичный mutator не добавлен. Новая JSON regression
проверяет complete state roundtrip и сохранение duplicate-ID validation.
Focused после fix102/102, повторные полные проверки имеют отдельные свежие reports.

После JSON fix full Debug2186/2186 иRelease2186/2186,9TRX/configuration,
failed0/skipped0; Aether780, остальные project counts прежние. Последние passing
TRX — `TestResults/ENG5c2/final-Debug-jsonfix` и `final-Release-jsonfix`;
`managed-summary.json` ссылается только на эти18TRX. Итоговые команды:

```powershell
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5c2/final-Debug-jsonfix --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c2/final-Release-jsonfix --verbosity quiet
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng5c2-final-jsonfix -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -IncludeScene -IncludeInput -IncludeRoom -IncludeInteraction -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
```

Новый native interaction proof прошёл в Debug apphost и published Release:
по10cases/9PNG/4owned children, все cooperative/reaped/released. PNG assertions
проверяют три independent world centers/material colors, bounded pixel counts,
floor/back wall/occlusion; active blue child становится yellow, после Replace
цвет/флаг сохраняются, второй pulse возвращает authored blue. Отдельные сцены
revision8/9 проверяют noninteractive blocker перед target и disabled target.
80fixed ticks перемещают камеру сZ6кZ2; reach/outside, one-shot E, resize/suspend/
resume320×240, exact complete checkpoint state и transient-input reset проверены.
Core/sync validation errors/warnings0/0, resources/handles/mappings/UI sessions0,
imported ReadPixels0; readback только итогового UI screenshot для assertions.
Новый proof подаёт pulses через обычный Supervisor API; прежний input proof отдельно
использует настоящий Silk keyboard callback. Physical OS input/raw cursor не заявлены.
Визуально просмотрен active-object PNG, geometry/цвета соответствуют fixture.

Local `TrueMoon.Aether.0.2.2.7-alpha.nupkg` создан из final Release без rebuild;
DLL SHA256 совпал с managed и native published proof:
`3A31E2CA99389209923F15DBB907037879C10CDE1BAB31D873E42EDC5C8143F1`.
Команда: `dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5c2/packages --verbosity quiet`.
Отдельный package drawing consumer/публикация не выполнялись. Все52pending C#files
прошли namespace/whitespace checks, scratch artifacts в Git0. Reports —
`TestResults/ENG5c2/managed-summary.json`, `package-check.json`, `namespaces-check.json`,
`docs-check.json`, `native-interaction-summary.json`, `completion.json`.

Финальная полная native-матрица после JSON fix:
`TestResults/EngineSessions/eng5c2-final-jsonfix/summary.json`, **Completed=true,
60/60cases,62commands,478source/76binary fingerprints**; hashes неизменны на
протяжении run. Per configuration20headless и10UI/GPU cases: новый interaction,
прежние room/cube/input, CPU viewport/Editor, GPU import/surface/viewport/ordinary
Editor и checkpoint/Replace/failure regressions. Ordinary GPU Editor снова прошёл
20Replace/20failures после JSON fix; старые requirements не ослаблялись.
Passing CPU/GPU engine integration не подменяет full UI/golden/RTE/hardware,
package drawing или3D performance. Предыдущий interrupted native run сохранён
с исходной причиной и не включён в passing counts.

Completion report Passed=true: managed2186/configuration, native60cases, новый
interaction18PNG, namespace52files; documentation10Markdown/284links/189anchors,
errors0. Scratch остается вне Git. ENG5c.2 выполнен в заявленном scope;
далее ENG5c.3 collisions камеры, затем target/HUD/pause. Full World snapshot/edit/
MCP/standalone/raw cursor и прежние hardware/release gates открыты. Commit/push
пользователь этим шагом не запрашивал.

## ENG5c.1: scene consumers и несколько примитивов — 2026-10-22

Запрос: «давай приступим к следующему шагу»; текущая точка продолжения — ENG5c.1.
В одной Aether assembly добавлены BuiltInComponents/PrimitiveSceneAdapter,
RenderPrimitive/ScenePrimitive и bounded multi-draw PrimitiveScene. SceneSnapshot
копирует optional primitive payload; control identity переведена с2на3.
Frame1/TMCK1 прежние. Matching Editor/Runtime/Aether/Game.Logic пересобираются,
v1/v2 identity отклоняется; fallback нет. Старый cube constructor и CPU default
сохранены. [Контракт и следующий шаг](PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22),
[публичный профиль](../../TrueMoon.Aether/README.md#стандартные-компоненты-и-комната).

Built-in schemas v1 задают persistent role GUIDs, exact defaults/dependencies и
single occurrence. Transform local S*R*T, world local*parentWorld, quaternion
yaw/pitch/roll radians; shear сохраняется без decomposing SRT. Positive scale,
finite/nonsingular matrices и stored-sRGB RGB[0,1] проверяются до публикации.
Неизвестные роли authoring-only; no Appearance=no draw, no Transform group=identity.
Source scene revision обязана изменяться вместе с authoring data/definitions.
Wire primitive —16row-major floats+RGB, deep-copy на ingress/egress; до64entities.
Immutable frame — до64draws, включая empty list. Vulkan loop сохраняет pass/depth/
slot lifetime и shader/SPV; CPU pixels/staging для scene renderer не добавлены.

`Game.Logic.RoomGame` — отдельная DLL, static room geometry reload той же revision,
fixed-tick camera и checkpointed demo interaction. TMCK1 не объявляется full-world
snapshot. `--room` подключён к прежнему App/Editor/input sender; `--room-proof`
использует обычный separate child/IPC/imported GPU/retained UI path. RoomScene
создаёт пол, три стены, три объекта и parent group через authoring resolver;
синий child наследует90°rotation и2xscale. Reach/raycast/collisions/HUD — ENG5c.2+.

Managed evidence2026-10-22:36новых cases (BuiltInSceneTests22, RoomSceneTests14),
обновлён input codec regression control-v3. Первый focused Debug68/68, затем
full solution Debug2134/2134 иRelease2134/2134, по9TRX/failed0/skipped0:
Aether728, UI615, Hosting79, Mithril95, Core107, Cobalt179/generator91,
Enerit126/generator114. Existing CS1591 warnings в прежних/generated APIs не
подавлялись. Assertions/gap review выполнен, coverage/mutation metrics не измерены.

| Requirement | Evidence: точные xUnit methods |
| --- | --- |
| «минимальные стабильные component roles/schema IDs» | `BuiltInSceneTests.StableRoles_DefaultsAndDependenciesAreExplicit`; `BuiltInSchemaChanges_RequireExplicitMigration` (4cases) |
| «parent transforms определены и проверены» | `BuiltInSceneTests.ParentScaleRotationTranslation_ComposesInOrderAndRetainsShear`: world center(22,6,-2), dot=-6; `WorldMultiplicationOverflow_IsRejectedEvenWhenLocalTransformsAreFinite` |
| «позиции/материалы из scene data» | `VariantOverridesAndComponentRemoval_DriveActualDrawAndInteractionData`; `InteractionEnabledAndBoundaryColors_AreNotReplacedByDefaults` (2); `MissingRoles_AreIdentityGroupsAndNonRenderingMarkers` |
| Atomic invalid input/cancel и bounded snapshot | `InvalidGeometryOrMaterial_FailsAtomicallyWithEntityPath` (7); `RuntimeBound_IsExplicitAndAuthoringRemainsUnbounded` (0/64/65); `CancellationAndRepeatedProjection_DoNotPublishSharedMutablePayloads` |
| Wire ownership/version | `RoomSceneTests.Snapshot_DeepCopiesWirePayloadOnBothIngressAndEgress`; `MalformedWirePrimitive_IsRejectedBeforePublication` (7); `ControlV3_RoundTripsWorldMatrixColorAndNeutralGroupWithoutAliasing`; `GameInputProtocolTests.ControlV3_RoundTripsInputFlagsLookActionAndOutcomeAndRejectsOlderVersions` |
| Multiple primitives/legacy compatibility | `RoomSceneTests.ExplicitDrawList_IsBoundedAndCopiedWhileLegacyCubeRemainsCompatible` (0/64/65), прежние `PrimitiveSceneTests` |
| «scene revision/checkpoint/Replace» | `RoomSceneTests.RuntimeRoom_UsesAuthoredDrawsAndRetainsOnlySupportedCheckpointStateAcrossFreshLoad`; `InvalidLoad_DoesNotConsumeSequenceOrMutateGameAndChangedSceneRevisionRejectsRestore` |

Команды и артефакты:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~BuiltInSceneTests|FullyQualifiedName~RoomSceneTests|FullyQualifiedName~PrimitiveSceneTests|FullyQualifiedName~GameInputProtocolTests' --logger trx --results-directory TestResults/ENG5c1/focused-initial --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5c1/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ENG5c1/final-Release --verbosity quiet
```

`TestResults/ENG5c1/managed-summary.json` содержит18TRX paths/counts. Research/plan/
status и Roslyn pairing сохранены в OS temp `truemoon-eng5c1-testagent-20261022`;
dedicated testing-generator tool недоступен, pipeline выполнен inline. Rider
findTests недоступен по предыдущей проверке, использованы static pairing и direct
source/test reads. Scan whole src включает исторические TestResults copies и
не является coverage report. Проверены независимые expected coordinates/colors,
ошибки/code/path/cause, неизменность состояния, границы и исключение staged input
из checkpoint. Scratch не добавлен в Git.

Native evidence2026-10-22: первый scoped Debug room run прошёл с5PNG.
После final code review выполнена свежая полная матрица
`TestResults/EngineSessions/eng5c1-final/summary.json`: **Completed=true,58/58cases,
60commands,473source/76binary fingerprints**, source/binary hashes неизменны
на протяжении verification. Debug apphost и published Release отдельно.

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng5c1-final -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -IncludeScene -IncludeInput -IncludeRoom -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5c1/packages --verbosity quiet
```

Per configuration:20headless scenarios и9UI/GPU cases. Сохранены старые cube/input,
CPU viewport/Editor, GPU import/surface/viewport/ordinary Editor и replacement/
failure regressions. Новый room proof имеет5PNG, три hard-coded world centers/
material colors, bounded material pixel counts, floor/back wall samples и depth
occlusion (стена draw после объектов). Камера сдвинута на .15 за3fixed ticks;
resize800×450, suspend0/resume320×240, Replacegeneration2 сохраняет exact
camera/tick/interaction/Paused и положение/цвета сцены. Синий parented object
проверяет реальный перенос world matrix через generated IPC codec. Визуально
просмотрен первый PNG: три объекта и комната отображаются корректно.

GPU core/sync validation errors/warnings0/0; runtime cleanup reports и local
assertions проверили resources/handles/mappings/UI sessions0. Imported texture
ReadPixels0; PNG assertions используют readback итогового UI OpenGL screenshot.
В room по2owned child/configuration, все cooperative/reaped/released; game DLL
не загружена в Editor. Прежний input proof по15cases/5PNG/configuration, ordinary
GPU Editor по20Replace/20failures; это scoped engine integration, не вся UI
native/golden/RTE/hardware matrix и не physical OS keyboard/mouse acceptance.

Local `TrueMoon.Aether.0.2.2.7-alpha.nupkg` создан. DLL SHA256 совпадает с managed
Release и published native proof:
`5CEDCB47A36E50FCCD458078CA80B0B217C1FB987FF0B9979374C02EB0FF1659`.
`TestResults/ENG5c1/package-check.json` содержит payload/entries. Отдельный
package drawing consumer и публикация не выполнялись. Все42pending C#files
(включая24предыдущих этапов) прошли namespace/whitespace checks.
Итоговые reports: `managed-summary.json`, `namespaces-check.json`, `docs-check.json`,
`completion.json` в `TestResults/ENG5c1`. Required shaders/SPV не менялись.

ENG5c.1 выполнен в заявленном scope. Далее ENG5c.2 reach/raycast/interaction
с явным versioned object-state checkpoint до mutable gameplay. Full world snapshot,
runtime edits/MCP, collisions/HUD/standalone,3D performance/raw cursor и прежние
hardware/release блокеры остаются открытыми. Коммит/пуш этим шагом не запрошены.

## ARCH1a.2: агрегат сцены и адаптер — 2026-10-22

Запрос: «давай к следующему шагу»; точка продолжения — ARCH1a.2. Добавлены
SceneId/SceneDocument, SceneResolver/ResolvedSceneSnapshot, structured scene errors
и explicit SceneSnapshotAdapter/SceneEntityProjection в той же Aether assembly,
namespace Scenes. Это stateless in-memory API, без нового UseX или UI dependencies.
Profile и следующий ENG5c.1 ведутся в [PLAN](PLAN.md#реализация-arch1a2--2026-10-22),
публичная инструкция — [README](../../TrueMoon.Aether/README.md#агрегат-сцены-и-совместимый-snapshot).

SceneDocument фиксирует nonempty GUID scene ID, revision≥0 и frozen catalogs.
Whole-scene resolver проверяет duplicate entities/catalogs, parent presence/cycles
и every effective entity через существующий EntityResolver. Traversal/graph
итеративны; roots/siblings сортируются по Guid, preorder даёт parent-before-child.
Immutable snapshot публикуется только после полного успеха; referenced definition
revisions и exact schemas visible компонентов согласованы с тем же document.
Unused catalog graphs не разрешаются, duplicate catalog identities всё же invalid.
Revision — authoring version, не tick; полного simulation checkpoint здесь нет.

Projection получает source snapshot/entity и явно задаёт world XYZ/Interactive.
Adapter сохраняет canonical GUID IDs/revision и старый limit64entities, проверяет
finite floats, не публикует partial result при error/cancel. Parent/definition
metadata остаются в source; world transform composition и внешние эффекты callback
определяет consumer. Legacy64limit не накладывается на сам authoring aggregate.
OCE передаётся без wrapping; остальные projector failures сохраняют original cause
и failing scene/entity path. Старые SceneSnapshot/control/frame/TMCK не изменены.

Первый focused run:51passed/1failed из52новых cases. Ошибка assertion при сравнении
двух ImmutableArray с одинаковым содержимым исправлена явным AsEnumerable comparison;
проверки точного order/children/values сохранены. После этого scene suite130/130
и финальные solution runs ниже прошли. Production logic после первого focused
run не менялась. Требованиям соответствуют следующие tests в
`TrueMoon.Aether.Tests.Scenes`:

| Requirement | Evidence |
| --- | --- |
| «проверить уникальность EntityId» | `SceneResolverTests.DuplicateEntityIds_AreRejectedBeforeResolvingEitherInstance`: exact code/path/EntityId |
| «существование parent и отсутствие parent cycles» | `SceneResolverTests.ParentGraph_RejectsMissingParentWithStablePathAndEntityIdentity`, `ParentGraph_RejectsCyclesIncludingTailsEnteringACycle`; self-parent — `LocalContracts_RejectInvalidInputsBeforeSceneResolution` |
| «несколько instances, independent inheritance и parent hierarchy» | `SceneResolverTests.Forest_ResolvesInheritanceIndependentlyFromParentsAndPinsReferencedVersions`: five entities, roots/children/preorder, independent template values/provenance |
| «Контракт scene revision/definition versions» | `SceneResolverTests.RevisionMetadata_PinsUsedDefinitionsAndCoexistingExactSchemaVersions`: long.MaxValue document revision, referenced definitions, simultaneous v1/v2 schemas; `EmptyAndSingletonScenes_ReturnCompleteHierarchyAndOnlyUsedMetadata` covers revision0/1 |
| «atomic failed resolution» | `SceneResolverTests.EntityFailure_IsAtomicAndRetainsInstanceAndOriginalDefinitionCause`: later entity failure, no published result, earlier snapshot/source unchanged, subsequent valid resolution |
| «immutable aggregate» | `SceneResolverTests.FrozenDocument_RemainsConsistentAfterAllCallerCatalogsAreMutated`, `CancelledAndParallelResolutions_DoNotChangeSharedDocumentOrOtherSnapshots` |
| Deterministic parent-first order and independent error categories | `SceneResolverTests.DeclarationOrder_DoesNotChangeHierarchyOrResolvedValues`, `InheritanceAndSchemaFailures_AreNotConfusedWithAValidSceneParentGraph`, `CatalogErrors_RetainOriginalCategoryAndScenePathEvenForEmptyScenes` |
| Deep hierarchy without recursive traversal | `SceneResolverTests.DeepHierarchy_UsesIterativeTraversalAndDoesNotInheritDefinitionDepthBound`:4096entities, reverse-ID parent chain, exact order/leaves |
| «явную адаптацию минимальных component данных к текущим scene/game consumers» | `SceneSnapshotAdapterTests.Projection_PreservesCanonicalIdsRevisionOrderAndExplicitWorldSpacePolicy`: world X6/7 for child/grandchild, exact IDs/revision/order, mutable copy isolation; `Projection_RemainsLoadableByExistingGameAndCheckpointContract`: ProbeGame Load/ticks/interaction checkpoint and reset |
| Legacy entity/float bounds | `SceneSnapshotAdapterTests.ExistingEntityLimit_IsCheckedBeforeAnyProjectionAndDoesNotLimitAuthoringScenes`:0/1/63/64/65 and no callback at65; `NonFiniteWorldCoordinates_ReportEntityAndOriginalValidationCause`: all XYZ/NaN/±infinity; `FiniteCoordinateBoundariesAndNullContracts_AreExplicit` |
| Projection failure/cancel atomicity | `SceneSnapshotAdapterTests.ProjectorFailure_PreservesOriginalCauseAndPublishesNoPartialResult`, `Cancellation_BeforeOrDuringProjectionNeverPublishesOrCallsLaterEntities`: pre/first/last callback; `ProjectorCancellation_IsNotWrappedAsAnAuthoringFailure` |

Успешные команды:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes --logger trx --results-directory TestResults/ARCH1a2/focused-scenes --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ARCH1a2/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ARCH1a2/final-Release --verbosity quiet
```

Focused scenes130/130 (78previous +52new). Full Debug/Release:
**Passed:2098, Failed:0, Skipped:0** в каждой configuration,9TRX/configuration.
Counts: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI615/
Hosting79/Aether692. Final TRX включают все52новых и130scene cases/configuration;
отчёт — `TestResults/ARCH1a2/managed-summary.json`.

Review assertions проверил exact collections/metadata и errors/cause/paths,
secondary state после failure, input copy isolation, independent graphs и
before/at/after limits. Статические mutation candidates: пропуск parent/cycle
validation, изменение order, потеря used-version pruning, shared mutable catalogs,
частичное завершение projection/пропущенный cancellation check и off-by-one64
нарушили бы named assertions. Mutants/line coverage не запускались, score не
заявляется. Roslyn pairing выполнен один раз до реализации; whole-root counts
включают historical TestResults copies и не являются production coverage.
Research/plan/status workflow сохранён в OS temp вне Git; IDE findTests отсутствует.

Это managed source proof. Native/GPU/package checks не повторены, interactive
sample остаётся прежней fixture. Auto world transforms, full runtime snapshot/edit,
aggregate persistence/transport codec и MCP не объявлены готовыми. Следующий
ENG5c.1 — минимальные component roles и подключение scene data к runtime/renderer
нескольких примитивов с отдельным native pixel/validation/lifecycle proof.

Финальная проверка: 8 Markdown,204локальные ссылки/122anchors, ошибок0;
24pending C# files (7production+2test новых этого этапа) — namespace/whitespace0issues.
Tracked C# contracts без изменений, scratch artifacts в Git0. Итоговый
`TestResults/ARCH1a2/completion.json` сверяет9TRX/configuration,2098passed,
692Aether/130scene/52новых cases и текущие documentation/source checks.

## ARCH1a.1: immutable модель и resolver — 2026-10-22

Запрос: «давай приступим к работе» после назначения ARCH1a.1. В одной Aether
assembly, namespace Scenes, реализованы immutable authoring contracts: GUID IDs,
definitions/variants/instances, component schemas/data, patches/scalar values,
EntityResolver и immutable result с property/presence provenance. Новые классы
не требуют App extension: это stateless utilities, без owned resources.
Точный [profile и границы](PLAN.md#реализация-arch1a1--2026-10-22) записан в PLAN;
публичный пример — [README](../../TrueMoon.Aether/README.md#authoring-шаблоны-варианты-и-экземпляры).

Контракт первого profile: одна base definition, отдельный scene parent,64definitions
в chain; exact schema version, ordinal ASCII property IDs1..128, null/bool/Int64/
finite double/string≤4096UTF16, без numeric coercion. Значения по умолчанию схемы
имеют отдельный provenance flag. Remove маскирует occurrence, restore возвращает
hidden state; reset снимает только локальный override/mask. Occurrence IDs не
переиспользуются в цепочке. Final visible set проверяет multiplicity/dependencies.
Errors содержат code/path; невалидная instance не мешает разрешению другой.

Сначала focused run дал72passed/2failed: ImmutableDictionary builder принимал
duplicate property ID с тем же value/object. Добавлены явные duplicate checks
в ComponentSchema и AuthoringValidation.Values. Focused74/74 после исправления
прошёл; при review добавлены ещё4cases (version zero/negative и сохранение всех
patch operations после reset), всего78новых. Финальное уточнение ordinal test
проверяет разные explicit data values для `A`/`a`, не только schema defaults.
Все последующие финальные проверки выполнены после этого уточнения.

Все method names ниже относятся к `TrueMoon.Aether.Tests.Scenes`.

| Requirement | Evidence |
| --- | --- |
| «сущности могут наследоваться», «только 1 какое-то изменение в родительской» | `EntityResolverTests.DoorVariant_TwoInstances_ResolveIndependentValuesAndOrigins`: color red, inherited label/x/defaults, отдельные parent/definition и origin revision |
| «изменение свойств и добавление\удаление компонентов» | `EntityResolverTests.Components_AddRemoveRestoreAndResetMasks_PreserveValuesAndPresenceOrigins`: variant addition + mask, instance restore + color override, source и presence source |
| «Door → RedDoor → два instances сцены» | `EntityResolverTests.DoorVariant_TwoInstances_ResolveIndependentValuesAndOrigins`: independent transforms/color, IDs и повторное разрешение без утечки изменений |
| «property override/reset/null/default» | `EntityResolverTests.PropertyReset_DistinguishesMissingNullAndExplicitDefaultAndRetainsOtherChanges`, `AuthoringModelTests.NullableSchema_ResolvesNullDefaultAndAcceptsExplicitNonNullValue` |
| «base updates» | `EntityResolverTests.BaseRevision_UpdatesInheritedValuesWithoutOverwritingLocalOverridesOrOldResults` |
| «add/remove/reinstate components» и сохранение остальных changes | `EntityResolverTests.Components_AddRemoveRestoreAndResetMasks_PreserveValuesAndPresenceOrigins`, `AuthoringModelTests.PatchResets_PreserveUnrelatedAddRemoveRestoreAndPropertyChanges` |
| «cycles/missing base», «duplicate IDs/type conflicts» | `EntityResolverTests.InvalidDefinitionGraphs_ReportIdsAndDoNotTraverseSceneParents`, `InvalidChanges_ReportExactCodeAndTargetWithoutPoisoningResolver`, `Catalogs_RejectDuplicateDefinitionsAndExactSchemaVersionsWithPaths` |
| «component multiplicity/dependencies» | `EntityResolverTests.Multiplicity_IsTypeBasedAndPermitsIndependentOccurrencesOnlyWhenDeclared`, `FinalDependencies_AllowRestorationButRejectMissingRequiredTypes` |
| «provenance и неизменность исходных данных» | `AuthoringModelTests.Collections_AreFrozenTransitivelyBeforeCallerMutatesEveryInputCollection`, `EntityResolverTests.Catalogs_AreSnapshottedAndRepeatedParallelResolutionsDoNotShareMutableState` и DoorVariant test |
| «schema/version и допустимые значения» | `EntityResolverTests.SchemaVersions_AreExactAndUnknownSchemasAndInvalidInitialValuesAreRejected`, `AuthoringModelTests.ScalarValues_PreserveKindsBoundariesAndRejectCrossKindReads`, `ScalarNumbers_RejectNonFiniteValues`, `PropertySchemas_RejectInvalidKindsAndDefaults` |
| ID/key/version/depth bounds и typed errors | `AuthoringModelTests.Identifiers_RejectEmptyAndDefaultIdsAtEveryModelBoundary`, `PropertyIds_KeepOrdinalCaseAndEnforceExactLengthBoundary`, `AuthoringInputs_RejectInvalidLocalContractsBeforeResolution`, `EntityResolverTests.InheritanceDepth_EnforcesExactBoundWithoutRecursion`, `InvalidDefinitionValues_ReportDefinitionPathWithoutPoisoningOtherInstances` |

Команды, завершившиеся успешно:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~TrueMoon.Aether.Tests.Scenes --logger trx --results-directory TestResults/ARCH1a1/focused-green --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger trx --results-directory TestResults/ARCH1a1/aether-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ARCH1a1/final-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ARCH1a1/final-Release --verbosity quiet
```

Focused-green74/74 — промежуточный run; module Debug640/640. Финальные solution
Debug иRelease: по9TRX/configuration, **Passed:2046, Failed:0, Skipped:0**.
Counts: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/UI615/
Hosting79/Aether640; все78новых cases включены в каждый final run.
Managed evidence: `TestResults/ARCH1a1/managed-summary.json` и final TRX directories.

Test-quality review проверил named requirements, exact error code/path, inherited
и local provenance, secondary state после failures/reset, null/default differences,
boundary neighbors и immutable collections. Исправлена слабая проверка, создававшая
новый fixture вместо наблюдения результата; final assertion проверяет отсутствие
Extra на последующем valid result. Review статический: mutation score и line
coverage не измерялись. IDE findTests отсутствует; Roslyn pairing выполнен один раз,
обнаружил existing SceneSnapshot tests. Его общие counts включают исторические
TestResults copies и не объявляются аудитом production sources. Research/plan/status
test workflow сохранены в OS temp вне stageable repository content.

Изменения не трогают прежние SceneEntity/SceneSnapshot, RPC/control/frame/TMCK,
runtime simulation/input/renderer. Native/GPU/package checks этим шагом не
повторялись. Полный snapshot, live editing, MCP, persistence и parent graph aggregate
не объявляются готовыми. Далее ARCH1a.2 scene aggregate и адаптация для ENG5c.

Финальные checks: 8 Markdown, 197 локальных ссылок и 115 anchors, ошибок0;
15новых C# файлов/namespace violations0; tracked diff и new-file whitespace
проверены. `TestResults/ARCH1a1/completion.json` сверяет managed counts, все78новых
cases в каждом final TRX set, namespaces и отсутствие scratch artifacts в Git.

## Editor/Runtime: процессы и первый шаг — 2026-10-22

Пользователь подтвердил два процесса: owned Runtime запускается модулем,
редактор с UI работает отдельно; команды/input передаются через IPC. Затем
попросил внести решение в планы и определить начало работ. Существующее разделение
в PLAN сохранено и уточнено: Aether supervisor → Mithril launch/stop/reap,
Enerit control/input/state, отдельный RAM/GPU frame transport, readonly query,
runtime-owned snapshot и те же API для CLI/агентов. Уточнены input admission/reset
и recovery неизвестного результата edit после disconnect. Текущие probe роли
одного executable уже работают в разных процессах; production executables — ENG7.

Первым назначен [ARCH1a.1](PLAN.md#arch1a1-первый-шаг-модель-и-разрешение-наследования):
immutable definitions/variants/instances и pure effective entity resolver с
provenance. Контрольный пример Door → RedDoor → два независимых instances;
критерии покрывают overrides/reset, component add/remove, invalid graphs и
immutability. Затем минимальная интеграция модели в scene data/ENG5c; полный
authoring/runtime edit/MCP toolset развивается отдельно в ARCH1b–d/ENG7.
Общие PLAN/STATUS и module STATUS синхронизированы. Изменена документация;
новых code/build/test/native/package результатов нет.

Проверены 6 изменённых Markdown-файлов, 184 локальные ссылки и 107 anchors; ошибок нет;
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл.
Отдельный отчёт: `TestResults/ArchitectureProcesses-20261022/docs-check.json`;
предыдущая проверка ARCH1 сохранена отдельно.

## ARCH1: согласование модели мира и API — 2026-10-22

По обсуждению пользователь подтвердил Entity + Components и наследование данных:
варианты переопределяют свойства, добавляют и удаляют компоненты. Для Play выбран
readonly просмотр по умолчанию; явное редактирование ставит Runtime на паузу,
сохраняет согласованный snapshot, применяет изменения и возвращает прежний режим.
Принят первый локальный профиль агентов с MCP adapter поверх общих API.

В [PLAN](PLAN.md#arch1-entity-components-и-api--2026-10-22) записаны владельцы данных,
различие scene hierarchy/base definition, API для authoring/runtime, rollback,
границы полного snapshot и ARCH1a–d acceptance. При failed restore продолжение
частично изменённой симуляции запрещено. Перенос Play edits в документ явный;
TMCK v1 не расширен и не объявлен полным snapshot. Одна Aether assembly и App
integration сохранены; функциональный следующий шаг ENG5c учитывает модель ARCH1a,
полный toolset развивается в ENG7. Build/test/native/package checks не запускались;
предыдущие результаты2026-10-21 сохраняют свои scope и дату.

Проверены5изменённых Markdown,156локальных ссылок и88anchors, ошибок0;
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл.
Отдельный отчёт: `TestResults/Architecture-20261022/docs-check.json`;
исторические отчёты ENG5b не перезаписывались.

## ENG5b: gameplay input и камера — 2026-10-21

Запрос: «теперь давай приступим к следующему шагу»; точкой продолжения был ENG5b.
Реализованы `Input.GameInputState`, `GameInputTick`, optional `IInputGameSession`,
fixed-tick accumulator и `FirstPersonCamera`. `Supervisor.SendInputAsync` на
App-owned host проверяет exact ожидаемую identity внутри command gate; очередь
старого runtime не переадресуется новому. Accepted означает staging без Tick;
Unsupported не fault legacy game. Control wire v2 добавляет flags/capture/look/
interaction pulse/outcome; frame/TMCK schemas v1 прежние. Старый control v1 и
старые host fingerprints несовместимы; matching host/game надо пересобрать.

Input bounded: movement flags0..15, packet look±0.5rad, accumulated look±1rad,
не-finite/unknown flags/non-neutral release отвергаются до mutation. E pulse
сохраняет быстрый down/up, UI repeat фильтруется, несколько pulses до tick
coalesced в одно действие. Look/action consumed один раз, held lease12fixed
ticks без refresh (200ms при continuous60Hz, не wall-clock timeout в Paused).
Pause/mode/Stop/restore очищают input. CPU ConfigureSurface очищает accumulator;
GPU runtime adapter вызывает `ResetInput` при Configure/Detach. Reset не меняет
game/checkpoint state. Transient input не переносится в checkpoint/Replace.

`CameraGame` отдельно от legacy ProbeGame: XZ движение3units/s, diagonal
нормализована, look влияет на yaw/pitch, pitch ограничен, yaw wrapped. Сохраняет
camera/tick/interaction; E пока переключает demo material при наличии interactive
entity. Distance/raycast/collisions/комната/HUD остаются ENG5c.

В sample `--scene`: реальные GameEditor Start/Build/Play/Pause/Step/Stop/Replace/
Refresh/Cancel, click внутри fitted viewport, WASD/mouse/E и Escape → Editor.
Argentis keys W/S/D/E добавлены в конец enum, Silk mapping дополнен. Один30Hz
sender, bounded coalescing buffer, task на UI input event не создаётся.
Focus/capture loss, mode/resize/suspend/Stop/Replace сбрасывают state; capture
требует нового click. Sender использует capture epoch и не применяет stale
cleanup к новой capture; App composition drains его до Aether/GPU/UI teardown.
OS cursor lock/raw mouse отсутствуют; logical pointer deltas не дают unlimited
вращение у края OS window.

### Requirement | Evidence

42новых xUnit cases в `TrueMoon.Aether.Tests.Input`:

| Requirement | Evidence |
| --- | --- |
| Bounded packet/tick, exact valid boundaries, reject-before-mutation | `GameInputTests.State_ValidatesNeutralAndExactLookBoundaries`, `State_RejectsMalformedPacketsBeforeMutation`, `Tick_RejectsInvalidFlagsAndLook` |
| Consume look/E once, held lease12ticks, refresh/release/reset | `Accumulator_CoalescesLookAndActionsOnceAndExpiresAfterTwelveTicks`, `Accumulator_RefreshExtendsLeaseReleaseClearsPendingAndInvalidPacketDoesNotRefresh`, `GameInputProtocolTests.PlayingLease_ExpiresWithoutRefreshAndFreshPacketResumesMovement`, `HostReset_DropsHeldLookAndActionWithoutChangingCheckpointState` |
| Fixed speed/diagonal/opposites/yaw/pitch and camera checkpoint | `Camera_UsesFixedSpeedAndOppositeKeysCancel`, `Camera_NormalizesDiagonalAndTurnsMovementWithYaw`, `Camera_ClampsPitchWrapsYawAndRejectsInvalidPose`, `CameraGame_CheckpointRetainsPoseInteractionAndNeutralTicksStayStill` |
| Quick E/repeat, bounded mouse and clear UI state | `UiBuffer_PreservesQuickEKeyRepeatAndConsumesLookOnce`, `UiBuffer_BoundsMouseIgnoresInvalidCoordinatesAndResetDropsAllTransientState` |
| Ready/Paused/Step/mode/restore/Stop and optional capability | `Input_RequiresHelloAndLoadedScene`, `Input_StagesWhilePausedAndAppliesOnceAtStepThenReleaseClears`, `ModeChangeAndStop_ClearInputBeforeFutureTicks`, `RestoreReadinessAndOldSequence_RejectInputWithoutMutatingGame`, `UnsupportedInput_IsExplicitAndLegacyGameContinues` |
| Wire v2 floats/flags/action/outcome, reject v1 | `ControlV2_RoundTripsInputFlagsLookActionAndOutcomeAndRejectsV1` |
| Queued stale identity, local validation, Stop preemption, malformed ack | `Supervisor_FencesInputQueuedDuringNewRuntimeStartupWithoutFaultingIt`, `Supervisor_InvalidLocalInputAndPreCancelledWaitDoNotTouchRuntime`, `Stop_PreemptsPendingInputAndNoOldRequestReachesRestartedRuntime`, `Supervisor_RejectsMalformedInputAcknowledgementAndCleansOwnedRuntime` |
| Focused clean run | `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --no-build --filter FullyQualifiedName~TrueMoon.Aether.Tests.Input --logger trx --results-directory TestResults/ENG5b/focused-42 --verbosity quiet`: Passed42, Failed0, Skipped0 |
| Full managed regression | `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5b/final-Debug --verbosity quiet` и аналогично `Release`/`final-Release`: в каждой configuration Passed1968, Failed0, Skipped0,9TRX |

В каждом full run: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril95/
UI615/Hosting79/Aether562. Это test builds, не новый nonincremental/performance
baseline. Source/assertion review добавил readiness/playing lease/host reset/
unknown ack/Stop-in-flight checks. Assertion outcomes включают exact transforms,
state/mode/tick/resource/command side effects, exceptions и wire fields; static
mutation reasoning, не выполненный mutation score/coverage. IDE `findTests`
недоступен; Roslyn parse-only pairing подтвердил пять target source files с
новыми tests (interface/CLI native paths не равны line coverage). Broad working
artifacts в OS Temp, вне Git; framework/packages не менялись.

### Native input proof и ограничения

`--input-proof` вызывает настоящий private Silk KeyInput callback через reflection
для WASD/E/Escape (не OS messages), остальные normalized events — через реальный
UiSession в GPU окне.15cases: letterbox click не захватывает input; paused staging observational;
WASD/look/quick E на Step; held movement без повторного look/E; Escape/Editor focus;
Editor keys не двигают camera; staged input очищен при focus/capture loss;
resize/suspend; opposite WASD проверяет Silk mapping; camera/tick/interaction Replace;
old input не направляется новому runtime; held/pending E сброшены после Replace;
30Hz sender в Playing; Pause button neutral; сохранение Editor/authoring ownership.
5PNG фиксируют initial, moved/interacted, resize, replacement material и Playing→Pause.
Checkpoint assertions отделены от GPU pixels; imported ReadPixels0.

Полная matrix с первоначальным14-case input proof:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5b-20261021 -IncludeInput -IncludeScene -IncludeViewport -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin
```

Completed=true: **56cases/58commands**, Debug apphost и published Release:
по20headless control/lifecycle/build/checkpoint/replacement, CPU viewport/Editor,
GPU import/surface/viewport/ordinary Editor, ENG5a scene и ENG5b input.
440source/76binary fingerprints. После этого усилен только probe harness:
Silk keyboard callback вместо прямого UiKey, opposite keys, staged focus/capture
loss и staged suspend. Production библиотеки не менялись; отдельный final run:

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5b-keymap-20261021 -IncludeInput -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @()
```

**2cases/4commands/440source/76binary fingerprints**, в каждом15input cases/
5PNG/2cooperative children/22Silk key callbacks; sender packets81Debug/80Release.
Это correctness counts, не performance metric. Full run Debug binaries и два
исходника старого harness сохранены в `frozen-Debug`/`frozen-input-proof` внутри
первого run; Release binaries в исходном publish folder.31published library DLLs
(кроме EngineSessionProbe.dll) идентичны SHA256 между двумя runs.
Validation errors/warnings0, sender completed, imports ReadPixels0, final owned
GPU resources/handles/mappings/UI sessions0. Intentional fault/kill cases полной
matrix не объявлены normal cooperative exits; такое требование относится к
двум normal input children в каждой configuration.

Локальный `dotnet pack <project>/<project>.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5b/packages --verbosity quiet`
прошёл для TrueMoon.Aether, TrueMoon.Argentis и TrueMoon.Alloy.Platform.Silk.
`TestResults/ENG5b/package-report.json`: все3DLL внутри nupkg совпали SHA256 с
final published proof. Это payload check, не package drawing consumer/publication.
Независимая `TestResults/ENG5b/VerifyCompletion.ps1` Passed=true: обе managed
матрицы, сохранённые full440source/76binary и final440source/76binary inputs,
31library,10final input PNG hashes и3packages совпали. Исторический full run не
подменяется final harness; оба набора evidence сохранены раздельно.
Финальная документационная сверка:26изменённых Markdown, локальные links/anchors
проверены без ошибок; `git diff --check` с `core.whitespace=cr-at-eol` прошёл.
9новых namespaced C# files соответствуют путям; ещё1AssemblyInfo содержит только
assembly attribute. Это scoped audit новых файлов, не повторная проверка всех
проектов. Test-agent research/plan/status находятся только в OS Temp.

Первые development runs: `eng5b-dev-1` отвергнут из-за exact background RGBA8
сравнения; приведён к уже принятому±2 quantization допуску из ENG5a, aspect и
opacity assertions сохранены. `eng5b-dev-2` Debug input14cases/5PNG прошёл.
Первая test compilation указала на xUnit async Throws; исправлено на awaited
ThrowsAsync. Ни skipped tests, ни weakened mode/tick/ownership checks не добавлены.

Физические клавиатура/мышь/OS focus и raw cursor profile не автоматизированы;
native proof подтверждает normalized UiSession routing на actual GPU output.
3D performance/VRAM/loss/golden/RTE/full UI alpha и K3/K7/K8 остаются вне scope,
K7 workaround search не возобновлён. Следующий функциональный этап — ENG5c:
комната/пол/несколько примитивов, осмысленный interaction и Alloy HUD/pause.

## ENG5a: первый Vulkan 3D frame — 2026-10-21

Реализован ограниченный первый renderer в одной сборке TrueMoon.Aether.
`Rendering.IRenderGameSession` предоставляет immutable `PrimitiveScene`: один
unit cube, camera pose из checkpoint, rotation, RGB material, FOV/near/far.
`RuntimeProtocol.CaptureRenderFrame` захватывает scene и tick под общим lock с
commands/simulation/checkpoint; startup restore gate и stopping запрещают capture.
Capability optional; прежний IGameSession/CPU/reference pattern сохранён.

`GpuFrameData.FromScene` передаёт immutable draw workload и точный frame/tick;
Pixels пуст. Vulkan graphics pipeline, embedded SPIR-V, 36 vertices без vertex
buffer, D32 depth test/write и80-byte push constants рисуют непосредственно в
exportable RGBA8 image. Scene color — stored-sRGB с фиксированной интенсивностью
граней; это простой opaque material, не PBR/linear-light освещение.
Right-handed world, +Y up, -Z camera forward, Euler radians, depth [0,1]; Y
projection flipped для top-left image. Перед recording валидируются параметры
и representable matrix. Reference staging buffer теперь создаётся лениво;
3D не создаёт CPU pixel/staging allocation. Pipeline/framebuffer/depth/views
принадлежат slot, cleanup идёт после fence и до image/device. Существующие
external queue ownership barriers, semaphores,3slot ledger и surface generations
сохранены; UI владеет retained copy на своём thread/context.

`--scene-proof` использует обычные App/UseAether/Editor/Build/Replace и GPU
transport. Game.Logic остаётся только в child. Пять PNG/pixel assertions:
`01-cube` (bright front, darker back submitted later — depth oracle),
`02-camera` (3Step, projected centroid сдвигается влево), `03-resize`800×450,
`04-resume`320×240 после0×0, `05-replace-material` (новая DLL/color,
сохранены tick3/camera/Paused). Окно/root/authoring прежние, teardown GPU до UI.
Readback только после UI presentation для assertions; imported texture delivery
не вызывает ReadPixels. В scene proof включён aspect fit Canvas с проверкой
соотношения сторон image/render target; reference examples сохраняют прежний
режим. Readback assertions используют fitted image bounds.
RunProbe включает `-IncludeScene`, fingerprint GLSL/SPIR-V,
проверку PNG SHA256 и неизменности source/binary inputs.

### Requirement | Evidence

Новые focused xUnit проверки находятся в
`TrueMoon.Aether.Tests.Rendering.PrimitiveSceneTests`:

| Requirement | Evidence |
| --- | --- |
| Perspective, top-left Y, aspect, near/far Vulkan depth | `Transform_ProjectsCenterUpAspectAndVulkanNearFarPlanes` |
| Camera pose и model rotation влияют на projection | `Transform_UsesCameraPoseAndCubeRotation` |
| Invalid camera/FOV/clip/material, zero extent/overflow отвергаются | `Validate_RejectsInvalidCameraProjectionOrMaterial` (13 partitions); `Transform_RejectsSuspendedExtentAndUnrepresentableCamera` |
| GPU-only workload, точный tick/frame/slot и запрет mixed pixels/scene | `SceneFrame_ProducesExactMetadataWithoutCpuPixelsAndRejectsMixedWorkloads` |
| Capture не advances tick, совпадает с checkpoint camera, loaded/stop gate | `RenderCapture_RequiresReadinessCapturesCheckpointCameraAndRejectsStop` |
| Restore gate и optional capability | `RenderCapture_RejectsPendingRestoreAndUnsupportedCapability` |
| Focused validation | `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter FullyQualifiedName~PrimitiveSceneTests --logger trx --results-directory TestResults/ENG5a/managed-focused-green --verbosity quiet`: Passed19, Failed0, Skipped0 |
| Full managed regression | `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG5a/final-Debug --verbosity quiet` и аналогично `Release`/`final-Release`: в каждой конфигурации Passed1926, Failed0, Skipped0,9TRX |

В каждом full run: Core107, Cobalt179, generator91, Enerit126, generator114,
Mithril95, UI615, Hosting79, Aether520. Это test builds, не новый nonincremental
build/performance acceptance. IDE `findTests` вернул Tool not found; использованы
исходники соседних tests. Первый focused run:18passed/1failed из-за ожидаемого
exception type для16×0; production правильно бросал ArgumentOutOfRangeException,
исправлен test. Результат сохранён в `TestResults/ENG5a/managed-focused`.

### Итоговая native/package проверка

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng5a-20261021-3 -IncludeScene -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @('repeat')
```

Completed=true: **12cases/14commands**, Debug apphost + published Release,
430source/76binary fingerprints, source/binary inputs неизменны. В каждой
конфигурации scene5PNG/2children; старые one-frame import14cases/8children,
surface21cases/80frames/12children, viewport17cases/14captures/20copies/6children,
ordinary GPU Editor20Replace/20failure recoveries/53children и repeat20Start/Stop.
Fault cases старых probes намеренно завершают часть процессов принудительно;
только два normal scene children в каждой конфигурации требуют cooperative exit0.
Core/sync validation ошибок/предупреждений0; imports ReadPixels0. Все reports
подтвердили scoped cleanup. В scene runtime1 освободил3surfaces/9slots/3devices,
runtime2 —1surface/3slots/1device; live resources/handles/mappings/UI sessions0.

Scene assertions сохранены как `01-cube`, `02-camera`, `03-resize`, `04-resume`,
`05-replace-material` и SHA256 в `Debug-scene/scene-report.json` и
`Release-scene/scene-report.json`. Centroid front material смещается влево
после3Step и сохраняется при Replace (допуск0.005). PNG относятся
к final UI surface, не physical display/OS compositor color proof.

Локальный `dotnet pack TrueMoon.Aether/TrueMoon.Aether.csproj --configuration Release --no-build --no-restore --output TestResults/ENG5a/package --verbosity quiet`
прошёл. `package-report.json` подтвердил: DLL в
`TrueMoon.Aether.0.2.2.7-alpha.nupkg` совпадает SHA256 с DLL native published
Release proof (`5D5D41FB9EBF8327F110B745BA938EA7D673985C44A291820D9F1692E3ECBE6D`),
включая embedded shaders. Это payload/pack check, не отдельный package drawing
consumer и не публикация. shader compile + spirv-val Vulkan1.1 прошли для обоих
модулей; обычный build/publish не требует compiler.

Независимая итоговая сверка `TestResults/ENG5a/VerifyCompletion.ps1` прошла:
`completion.json` Passed=true,9TRX/1926passed в каждой configuration,
430source/76binary/10PNG SHA256 совпали, package payload прежний.
`CheckDocs.ps1`:26изменённых Markdown,716локальных links/313anchors, errors0.
`git -c core.safecrlf=false -c core.whitespace=cr-at-eol diff --check` прошёл;
4новых C# files проверены на namespace/path correspondence, violations0.
Это focused проверка новых files, не новый полный structural audit репозитория.

### Эксперименты и границы

Первый Debug proof `TestResults/EngineSessions/eng5a-dev-1` прошёл. Следующий
`eng5a-20261021` отвергнут: диагностическое обновление Status в FramePresented
инвалидировало UI до ReadbackOpenGLImage. Capture перенесён на следующий кадр
через PostWindow; не ослаблен dirty-output guard. В отвергнутом run один child
вышел с освобождёнными ресурсами, core/sync errors/warnings0; этот run не passing.
Матрица `eng5a-20261021-2` прошла до финального исправления aspect fit;
финальная source freeze/матрица — `eng5a-20261021-3`, без растягивания3D image.

Полный ENG5 не завершён. Далее ENG5b: input capture/routing WASD/mouse/E/Escape,
camera control на fixed tick и checkpoint/Replace; затем комната/пол/примитивы,
переключатель света/цвета и Alloy HUD/pause. ENG7 standalone отдельно. Текущий
ProbeGame всё ещё демонстрирует движение camera через Step reference simulation.
3D performance/VRAM/hardware loss не измерены; ENG4e reference timings сюда не
переносятся. PA8a/RTE/K3/K7/K8/final alpha остаются прежними, K7 search остановлен.

## ENG4e CPU/GPU baseline — 2026-10-21

Реализован [отдельный runner](../../ManualTests/EngineSessionProbe/RunViewportBaseline.ps1)
поверх обычных App/Editor/FrameSession transports. Opt-in internal EventSource
`TrueMoon-Aether-Frames` измеряет production, CPU read/copy/CRC, GPU import,
produce round-trip, retained submission и owner/use/fence. Без listener история
не хранится. CLI capture bounded:100000stage records/4096process snapshots;
overflow/EventSource errors/mismatched QPC frequency делают experiment неполным.
UI sample добавляет distinct actual FramePresented. CPU/GPU разные wire timestamps
не используются как одинаковая граница production: latency связывается с traced
start по session/runtime generation/surface generation/frame number. Frames,
начатые в warmup, в measured production cohort не входят. Повторная presentation
одного frame дедуплицируется; percentiles используют все samples интервала.

[Протокол и advisory budgets](PLAN.md#eng4e-протокол-измерений--2026-10-21)
записаны до полного запуска. Workload: одинаковая Game.Logic DLL/FramePattern/color,
artifacts двух revisions/manifest identities; один App/window на case,
Start640×360 → Resize1280×720 → Replace1280×720 → Stop. Simulation60Hz/
producer30Hz/UI60Hz targets; verification readback перед2s warmup и5s measurement.
По3повтора каждого mode/configuration, чередование CPU/GPU и GPU/CPU. JSON пишется
после измерений;200ms process polling/EventSource overhead включён в counters.
Дочерний capture worker drains/persists до App teardown.

Финальный experiment: **12/12cases,36intervals,24cooperative children,36pixel
verifications**,15commands,421source/94binary fingerprints; Completed=true.
Windows x64/build26300/SDK10.0.402/net10.0,24logical processors/QPC10MHz.
GPU adapter UUID `D69A207DD3EC6D8F5F325359A59FA086`, driver UUID
`8B2ED3A7D66651B8AD47BB85416B871B`; environment сохраняет NVIDIA RTX5070Ti
driver32.0.16.1742 и AMD Radeon32.0.21042.62. GPU transport использует eligible
GL adapter identity; driver/Windows settings не менялись. Validation layers
выключены только в measurement processes, correctness validation выполняется
отдельно. Нет imported ReadPixels, owned GPU resources/handles/mappings/UI sessions
после каждого case —0; Game.Logic в Editor не загружен.

```powershell
./ManualTests/EngineSessionProbe/RunViewportBaseline.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/ViewportBaseline/eng4e-20261021-3
```

Raw/commands/environment/fingerprints: `TestResults/ViewportBaseline/eng4e-20261021-3/summary.json`;
полная36строчная [таблица результатов](../../TestResults/ViewportBaseline/eng4e-20261021-3/REPORT.md).
Каждый case содержит experiment.json, baseline-report.json и3raw captures.
Всего **37766stage records**; measured cohorts **5244produced/5244received/
5182presented**, unreceived0/unpresented62. Последнее — produced frames без
distinct actual acknowledgement, включая границы resize/Replace/Stop; эта метрика
не объясняет единственную причину потери. Producer dropped attempts0 означает
отсутствие ring no-free-slot drops; пропущенные timer ticks не подсчитываются.
Unreceived/unpresented пересекаются, складывать их нельзя. FPS ниже — throughput
acknowledged production cohort за measured interval, не частота повторных redraw.

### Сводка трёх повторов

Задержки вms; p50/p95/production/retained — **медианы соответствующих per-run
percentiles**, не pooled percentile. FPS: медиана [min–max]; allocation — медиана
runtime managed MiB/s по фактическим inner polling intervals. Полные editor/runtime
CPU cores, allocations, handles, private/working-set peaks и GC deltas находятся
в raw/per-case report. Process private bytes не являются VRAM.

| Configuration | Mode | Участок | FPS [min–max] | Produce→present p50/p95 | Production p95 | Retained submit p95 | Runtime MiB/s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Debug | CPU |640×360 |30.19 [27.57–30.20] |41.00/49.38 |11.32 |0.26 |0.100 |
| Debug | CPU |resize1280×720 |24.80 [12.51–25.20] |89.99/105.95 |47.16 |0.69 |0.089 |
| Debug | CPU |Replace1280×720 |25.00 [23.39–26.60] |90.84/104.83 |46.96 |0.72 |0.087 |
| Debug | GPU |640×360 |30.20 [30.13–30.39] |13.73/22.21 |4.91 |0.64 |27.039 |
| Debug | GPU |resize1280×720 |30.20 [30.19–30.36] |21.39/30.14 |13.41 |0.74 |106.707 |
| Debug | GPU |Replace1280×720 |30.20 [29.74–30.20] |20.42/29.58 |13.08 |0.61 |107.401 |
| Release | CPU |640×360 |29.98 [29.37–30.00] |49.54/64.79 |2.55 |0.07 |0.100 |
| Release | CPU |resize1280×720 |29.99 [29.93–30.33] |50.34/65.42 |8.87 |0.65 |0.104 |
| Release | CPU |Replace1280×720 |29.92 [29.51–29.99] |51.91/66.24 |8.91 |1.10 |0.097 |
| Release | GPU |640×360 |30.19 [29.79–30.20] |12.70/21.35 |4.77 |0.70 |27.203 |
| Release | GPU |resize1280×720 |30.20 [29.73–30.33] |15.71/21.49 |5.80 |0.67 |106.701 |
| Release | GPU |Replace1280×720 |30.00 [28.36–30.20] |17.50/33.13 |5.78 |0.71 |106.673 |

Advisory breaches: **2/36intervals FPS<24 и6/36 latency p95>100ms**, все — Debug
CPU1280×720; outlier12.51fps сохранён. В Release и GPU intervals эти budgets
выполнены. Editor/runtime allocations не превысили32/256MiB/s; все48lifecycle
операций уложились в15s. Completed не означает все budgets met или30fps guarantee.
GPU runtime allocation~107MiB/s при1280×720 согласуется с new RGBA byte[] на каждый
FramePattern в GpuFrameData.GamePattern; это вывод по measured rate и исходнику,
не attribution profile. Этот reference path всё ещё CPU-generated/Vulkan staging,
а не performance будущего3D renderer/zero-copy. Нужные оптимизации выбирать по
реальному workload; временный pattern не становится minimum hardware promise.

### Проверки и первоначальные failures

Новые focused xUnit cases:8/8. В составе финального solution run Aether501/501,
Mithril95/95; UI615/Hosting79/Core107/Cobalt179/generator91/Enerit126/generator114.
**Debug1907/1907 и Release1907/1907, по9TRX, failed0/skipped0**. Это incremental
dotnet test builds; nonincremental build/golden/RTE/full hardware matrix здесь
не повторены. XML warnings backlog не подавлялся.

| Требование | Точный тест |
| --- | --- |
| Runtime/surface identities, warmup exclusion, distinct presented/cohort accounting | `BaselineAnalysisTests.Analyze_JoinsSurfaceAndRuntimeGenerations_DeduplicatesPresentsAndExcludesWarmup` |
| Foreign session и unavailable вместо0 | `BaselineAnalysisTests.Analyze_GpuReceivesUseExactSessionIdentity_AbsentResourcesRemainUnavailable` |
| Mismatched clocks/overflow/broken capture отвергаются | `BaselineAnalysisTests.Analyze_RejectsClockMismatchAndIncompleteCaptures` |
| Все samples, nearest-rank и empty/null | `BaselineAnalysisTests.Summarize_UsesAllSamplesAndNearestRank_EmptyIsUnavailable` |
| Negative/NaN/infinity не становятся latency | `BaselineAnalysisTests.Summarize_RejectsInvalidLatency` (3cases) |
| Actual opt-in payload/identity/timestamp соответствует runtime pattern | `FrameTraceTests.GamePattern_OptInTraceMatchesExactFrameAndEnclosesProductionTimestamp` |

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter "FullyQualifiedName~BaselineAnalysisTests|FullyQualifiedName~FrameTraceTests" --logger trx --results-directory TestResults/ViewportBaseline/development/focused-tests-final --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Debug --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --logger trx --results-directory TestResults/ViewportBaseline/development/verified-Release --verbosity quiet
```

До финальной матрицы short CPU прошёл; short GPU/первый full GPU иногда терял
первый child capture. После переноса flush перед App Stop второй experiment дошёл
до11/12accepted cases: последний Release GPU имел полный raw data, но был rejected
из-за Killing первого child. Найдена/исправлена
[гонка Mithril delivery/graceful wait](../mithril/HISTORY.md#graceful-wait-после-ошибки-доставки-exit--2026-10-21):
закрытие control pipe после принятого stdin exit пропускало graceful wait.
Budget3s не увеличен; deterministic red/green и native20shutdown cases прошли.
Предыдущие `eng4e-20261021-1/-2` остаются Completed=false; passing части не
переносятся в финальный proof. В финальном -3 все24children exit0/no Killing.

Ранний full Debug1906run дал Enerit restart IOException sharing violation при
чтении ready-2.json. Отдельная AppUnitsProcessTests theory18/18 и последующие
full Debug/Release прошли; unrelated readiness helper не менялся, race не объявлена
исправленной. Первая trace unit fixture содержала Version4 вместо Version2 GPU
stamp; исправлен только тестовый input,8/8после этого прошли.

GPU query/utilization/VRAM counters — unavailable, CPU durations не GPU timings.
Long resource trends, physical DPI/device loss, K7/final alpha и publication
остаются открытыми. Остановленный NVIDIA workaround search не возобновлялся.
Следующий функциональный этап — ENG5: минимальный3D renderer в существующий
viewport, с отдельным workload/baseline; CPU Debug debt/temporary staging allocation
сохранены как measured backlog, без автоматической смены default transport.

### Отдельная correctness и package regression

После measurement выполнен `RunProbe.ps1`: **6/6cases,8commands,421source/
76binary fingerprints**, Debug apphost/published Release. Actual GPU viewport,
ordinary GPU Editor и repeat lifecycle; core/sync validation0errors/0warnings.
Per Editor20Replace/20failure cases/53children, retained copies829Debug/813Release,
imported readbacks0, owned GPU handles/resources/mappings/UI sessions0. Эти checks
не добавляются к performance samples и не заменяют полный UI/hardware acceptance.

```powershell
./ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/eng4e-correctness-20261021 -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory E:/source/my/TrueMoon/src/TestResults/ENG4b/validation-layer/Bin -Scenarios @('repeat')
./ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/UnitsPackages/eng4e-20261021
```

Fresh units consumer: **108/108cases,8nupkgs/9projects,128commands/206source
fingerprints**, Completed=true, version`0.2.2.7-alpha.units.20261021094727716`.
Cobalt/DI/DI без Cobalt analyzer, Debug/Release build/publish; expected omitted
Enerit analyzer failureCS0234. Это delivery/App/IPC regression исправленного
Mithril, не Aether GPU package drawing proof. Пакеты локальные, не публиковались.

Итоговая integrity проверка `TestResults/ViewportBaseline/development/VerifyFinal.ps1`
независимо пересчитала cohort counts/p50/p95/FPS по всем36intervals/37766raw records.
Текущие1048source и170binary hash checks совпали;36raw capture hashes сохранены
в `completion.json`, Passed=true. Exact TRX totals проверены отдельно — по1907
passed/failed0/skipped0 в Debug/Release. Документационные проверки не создают
новых passing code/native runs.
Проверки документации:25изменённых Markdown files,711локальных ссылок/309якорей,
ошибок0; `git diff --check` прошёл. Scope включает сохранённые изменения предыдущих
шагов в текущем checkout; этот счётчик не является числом новых файлов ENG4e.

## ENG4d2 ordinary GPU GameEditor — 2026-10-20

Запрос «давай приступим к следующему шагу» реализован как следующий принятый ENG4d2.
Обычный ViewportView/команды Editor теперь явно выбирают CPU/GPU; shared lifecycle
в FrameSession не дублируется. App option ViewportTransport snapshots strategy
factory, connections принадлежат FrameSession, process Stop/reap — Supervisor,
imports — App GPU owner, независимый Skia image — UI consumer. Disposable strategy
освобождается exactly once; aliases в DI не добавлены. Aether остаётся одной engine
assembly, без UI/Skia/Game.Logic dependency.

Новый neutral contract: ViewportSurface (identity/generation/extent/mode без handles),
ViewportPresentation и выданный ею ViewportFrame ticket. Issue/copy не являются
first-present. FrameSession.Presented принимает только latest ticket активного
runtime/surface; stale/foreign/closed отклоняются. CPU Presentation разделяет
readiness с существующим receiver/display, старые CPU APIs сохранены. Replacement
использует neutral dimensions и тот же checkpoint/one rollback pipeline.

GpuViewportTransport подключает owned native lease, serial pump ProduceFrame →
owner callback/Skia GPU copy → observed GL completion → Return/Vulkan retirement.
Первый image pinned до actual presentation. Resize closes old tickets/stops pump
admission и drains accepted operation перед configure; Stop fences/cancels и drains
connection до child Stop. Failed cleanup retains connection и блокирует Start.
Background failed cleanup сохраняет cause/aggregate в Frames.TransportError;
успешный Stop retry не воспроизводит faulted watcher. Diagnostic очищается следующим
admitted Start. Caller/UI context живёт до успешного native cleanup.

Runtime normal role через свой UseAether hosts lazy GPU channel с optional
frameProvider. Атомарный CaptureFrame требует loaded/restored ready game и
отклоняет stopping generation. GpuFrameData валидирует tight RGBA8 shape,
slot/frame metadata/timestamp до upload; client проверяет metadata после Ready.
FrameNumber привязан к трём round-robin slots с checked overflow. Reference
GamePattern использует selected game's tick/color, CPU-generated RGBA и Vulkan
staging; полноценного GPU 3D renderer пока нет. Pixel payload не идёт в pipe,
imported ReadPixels не используется для delivery. Diagnostic v1/v2 defaults прежние.

Окончательный run: `TestResults/ENG4d2/complete3/summary.json`,7owned commands/
exit0; Aether493/493 Debug/Release, UI615/615 Debug/Release; full Debug1898/1898,
9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI615/Hosting79/Aether493. Nonincremental parallel Debug0errors/1499warnings.
Native `TestResults/EngineSessions/eng4d2-complete-20261020/summary.json`:
Completed=true,54aggregate cases/56commands/406source/76binary fingerprints.
Ordinary GPU Editor Debug815copies/791pixel checks; published Release800/783.
Each20cycles/20failure cases/53children, validation0/0,47devices/141slots balanced.
Old CPU Editor20Replace/53children, CPU viewport17cases/27children, headless20,
GPU capabilities/import/surface/reference viewport повторены в обеих configurations.
Separate retained GPU viewport proof снова проверил3,051,552pixels/config через
independent PNG decoder,14captures, imported readback0. These remain separate scopes.

Fresh package evidence — `TestResults/ENG4d2/package2/summary.json`:12packages/
18commands/4independent Cobalt+DI consumers (Debug + published Release), version
`0.2.2.7-alpha.aetherapp.20261020172158631`. Consumers compile/check GPU APIs,
TransportError и общий fake transport first-present contract; App startup lazy,
strategy/channel/GPU/factory/lease dispose exactly once, late tickets/Start rejected.
Consumer Aether/Skia DLL/native payload hashes match nupkg; no source ProjectReference.
This is managed package composition/native delivery, not package GPU rendering.

Commands (fresh output directories required):

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4d2/complete3/solution-Debug --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d2/complete3/ui-Release --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4d2/ValidatePackageFinal.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4d2-complete-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -IncludeEditorGpu -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Тесты новых API: latest/foreign/closed/invalid/suspended tickets; exact failure,
shared CPU receiver ticket/readiness; Start/resize/suspend/Stop и stale identity;
worker fault до/после first-present; connection cleanup до process Stop; combined
startup+cleanup errors/retry; background failed cleanup/retry без sticky watcher;
lazy App/exactly-once strategy ownership; loaded/stopping capture; RGBA order,
corners/frame/tick/color и invalid/overflow metadata. Pseudo-mutation/assertion
review выявил missing direct CPU-ticket и background cleanup-retry cases; оба
добавлены и проверены перед final snapshot. No coverage percentage/mutation-suite
execution claim. IDE findTests ранее недоступен; xUnit discovery/real runs использованы.

Native ordinary GPU proof повторяет20A/B Replace в одном App/window и20failure
cases с53owned children/MaxLive1. Loaded DLL/manifest hashes/source revisions,
complete checkpoint/restored state, first actual frame tick, next Step behavior
(camera X+.25/.125/Yaw+.01) и прежний mode проверяются. Compile failures/held jobs,
cancel/context/stale-session/Stop/deadline, missing/tampered builds, capture rejection/
unknown outcome, bad Hello/restore/no-frames, rollback/double failure, cleanup retry,
native WM_CLOSE с pending Replace/compiler покрыты. Extra GPU resize64×32/0×0/
160×90/stale ticket; все53cooperative reports дают47devices/141slots balanced,
core/sync validation0errors/0warnings. Imported readback0, local resources/handles/
mappings/UI sessions0, authoring unchanged, Game.Logic в Editor не загружен.

Исходный `native-initial` завис после capture первого поколения; остановлен только
owned process tree с проверкой PID/command line. Review нашёл unsafe assert, что
ticket обязан приниматься после diagnostic readback: Stop вправе успеть supersede
его. Assert удалён; Debug quick proof `eng4d2-debug-initial` прошёл20cycles/20failures/
53children/804copies/782pixel checks. Точный stack исходного stall не получен, он
не выдаётся за passing. Первый full `eng4d2-verified-20261020` прошёл54cases до
последующего UI availability/GL-ready/CPU-ticket review. Следующий rerun
`eng4d2-final-20261020` остановлен как owned superseded tree при обнаружении
background cleanup watcher gap. Только final snapshot выше является текущим.

Ordinary UI теперь показывает transport/neutral surface, ожидает GL binding ready
до Start и отображает transport failure; нет ложного GPU 'viewport unavailable'
из-за null legacy CPU Display. GPU mode явный (`--viewport-gpu`/project option),
CPU default сохранён; автоматического fallback на fault нет.

Limits: reference Windows x64/RTX5070Ti/150% DPI; ordinary GPU extents64×32/160×90,
larger extents/premul/compositing/clipping проверяются отдельными GPU probes.
Проверяется retained UI surface после FramePresented, не compositor/physical
display. Package consumers проверяют managed fake-transport API и delivery, не
actual native GPU drawing. Driver loss/hang, throughput/zero-copy/VRAM/allocations,
full Release solution/golden/RTE/final UI hardware acceptance/publication не
доказаны этим шагом. K3/K7/K8/VRAM/final alpha и K7 stop policy сохранены;
drivers/Windows settings не менялись. Следующий шаг — ENG4e measured baseline
на одинаковых CPU/GPU reference workloads перед ENG5/3D, без преждевременной
performance guarantee или оптимизации.

## ENG4d GPU viewport bridge — 2026-10-20

Запрос «давай приступим к следующему шагу» продолжен первым bounded результатом
ENG4d: actual reference GPU viewport. Остальной GameEditor transport integration
оставлен явным ENG4d2 в единственном engine PLAN/STATUS; ENG4 не объявлен закрытым.

`GpuSurfaceSession.UseTextureAsync` loans `GpuOpenGlTexture` на borrowed GL owner
после CPU ready + GPU wait. Все source uses должны быть submitted до callback
return/throw; после callback loan недоступен. Return semaphore/observed GL fence
предшествуют explicit Return/Vulkan retirement. Callback exception/cancel fences
admission. Configure drains Ready через пустой use без ReadPixels. Existing v1
Read/v2 diagnostic APIs сохраняются. App owns imports; runtime Stop/reap — Supervisor.

Generic Rendering.Skia `CopyTexture` borrows RGBA8 premul stored-sRGB/top-left
texture без adoption, copies в независимый GPU SKImage и submits source use даже
при исключении. Image bound к исходному GRContext/surface owner; drawing через
raster/foreign drawing context и use после dispose отвергаются. UI consumer
заменяет/disposes image до surface/context teardown. Aether→UI dependency нет.
Это одна GPU copy/frame; GPU completion и first actual presentation раздельны.
Primary API references: [SKImage.FromTexture](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skimage.fromtexture),
[GRContext.Flush](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.grcontext.flush).

`TestResults/ENG4d/complete/summary.json`:7owned commands/exit0. Nonincremental
parallel Debug build0errors/1499warnings. Aether453/453 Debug/Release. UI615/615
Debug/Release, full Debug1858/1858/9TRX/failed0/skipped0: Core107/Cobalt179/
generator91/Enerit126/generator114/Mithril94/UI615/Hosting79/Aether453.
Managed additions:9loan cases (all properties after end/foreign thread, exact
descriptor/session cause);2image ownership cases (actual drawing adapter context
guard, successful origin pixels, failed dispose/retry/use-after-dispose). Final
review strengthened the draw-path assertion; solution Debug/UI Release repeated
after that test-only change in `TestResults/ENG4d/review-tests.json`.

Final native evidence — `TestResults/EngineSessions/eng4d-verified-20261020/summary.json`:
Completed=true,52aggregate cases/54commands/398source/76binary fingerprints.
Each Debug apphost/published Release GPU viewport:17checks/14presented captures/
20copies/6children, imported ReadPixels count0, authoring unchanged, local GPU
resources/NT handles/UI sessions0. Five cooperative teardown reports:7devices/
21slots created/released, core/synchronization validation errors0/warnings0. One
deliberate crash has owned reap without cooperative teardown claim.

Whole interior canvas pixels checked after FramePresented: stored sRGB/premul over
RGB19/37/61, asymmetric origin, actual150% DPI/bilinear scale, clipping. Fractional
outer canvas edge excluded. Independent PNG decoder/filter reconstruction in
`TestResults/ENG4d/CheckViewportPixels.cs` rechecked3,051,552pixels/config from saved
PNG files/hashes without invoking production pattern/import/Skia readback. Capture
stamps and increasing actual PresentedFrames checked. Retained frame survives
12additional slot uses, resize/suspend, Stop/crash. New runtime rejects old stamp;
Return inside consumer rejected; submitted-copy exception and cancel before owner
dispatch fence late use; restart/WM_CLOSE drain before UI owner disposal.

ENG1–ENG3 and ENG4a/b/c repeated: each surface21checks/80frames/12children,9coops/
15devices/45slots, validation0/0; CPU viewport17cases/27children and Editor20Replace/
20failures/53children/MaxLive1. Per-run CPU pixel counts stay in native reports.
No Game.Logic assembly loaded into Editor; selected DLL/checkpoint proof remains CPU.

Fresh package evidence — `TestResults/ENG4d/package/summary.json`:12packages/
18commands/4independent Cobalt+DI consumers (Debug + published Release), version
`0.2.2.7-alpha.aetherapp.20261020140450577`. New UseTextureAsync/CopyTexture APIs
compiled/checked, lazy App/exactly-once GPU/factory/lease cleanup and late Start
rejection repeated. Consumer Aether/Skia DLL and transitive native DLL hashes match
fresh nupkgs; source ProjectReference absent. This is package API/delivery proof,
not native GPU drawing from a package consumer or NuGet publication.

Commands:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4d/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4d/complete/review-solution-Debug --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/ENG4d/complete/review-ui-Release --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4d/ValidatePackage.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4d-verified-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -IncludeGpuViewport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Initial native reference rejected actual150% DPI because it assumed100%; corrected
to physical pixel centers without changing Windows settings. Initial image retry
test exposed duplicate Size verification; Size now obtains the image once. Final
passes above supersede `native-initial`/initial focused failure, which remain logs.
`native-dpi`/`native-guards` were intermediate successful runs, not the final matrix.

Limits: reference Windows x64/RTX5070Ti; texture source16²/64×32/320×180 in GPU
viewport, larger1280×720 separately in ENG4c. Retained UI surface readback follows
actual presentation, not compositor/physical display color proof. GPU generation
replacement is reference producer Stop/Start, not Game.Logic DLL/checkpoint GPU
integration. Native driver loss/hang, VRAM/throughput/zero-copy, full Release
solution/golden/RTE/final hardware matrix and publication not claimed. K3/K7/K8/
VRAM/final alpha unchanged; no driver/Windows setting changes or K7 workaround work.

## ENG4c reusable slots и surface lifecycle — 2026-10-20

По запросу «давай приступим к следующему шагу» реализован следующий согласованный
ENG4c. `GpuSurfaceSession` и producer channel2 находятся в одной TrueMoon.Aether
assembly, получаются через `AetherHost.Gpu`/UseAether/App lifecycle. Новых package
dependencies/UI references нет; ENG4b/v1 строгие contracts сохранены.

### Контракт и реализация

Три persistent slots делят один Vulkan instance/device/queue. У каждого dedicated
image/memory, staging buffer, command buffer, fence и2binary semaphores. Второй/
третий native frames заимствуют device context первого; surface сериализует queue
operations и освобождает children раньше device owner. Failed child cleanup
сохраняет shared device для retry. Счётчики измеряют actual CreateDevice/DestroyDevice.

Version2 stamp: full SessionIdentity/SurfaceGeneration/Slot/FrameId. FrameId растёт
per-slot только после GL observed completion и Vulkan Retire; Ready/read-complete
slots нельзя overwrite. Допускаются3outstanding Ready frames с произвольным
порядком return. Native pixels зависят от surface/slot/frame, поэтому старый кадр
не проходит whole-image comparison. До foreign queue wait нужен CPU-observed fence.

Configure fences новые operations, ждёт принятую команду, drains Ready/read-complete
slots, освобождает GL imports, подтверждает exporter detach и только затем
создаёт fresh generation.0×0 suspends, resume также меняет generation; partial-zero/
invalid extent отвергается до I/O. Concurrent commands/Configure отклоняются,
unbounded queue нет. Stop/unknown result/runtime exit fences late work; owned
imports остаются до successful cleanup/retry. На App один unreleased reusable
session. GPU Dispose не присваивает process ownership: затем Editor/Supervisor
останавливает/reaps lease; App closes GPU до child/UI. Borrowed GL dispatcher/context
живут до cleanup. Ordinary viewport остаётся CPU, ReadAsync — diagnostic readback.

Отдельный CurrentUserOnly pipe:64KiB/strict JSON/full identity/sequence,
Connect3s/command8s/error reply2s, GL fence polls timeout0. End/accepted commands/
exit watcher входят в App drain. Runtime `RunSurfaceProducerAsync` владеет worker/
pool entries; LastSurfaceProducer сохраняет bounded aggregate counters/validation.
No DeviceWaitIdle/glFinish. Managed deadline не прерывает driver/native call или
произвольный user dispatcher.

### Требования и точные проверки

| Требование | Test / evidence |
| --- | --- |
| «давай приступим к следующему шагу» — ENG4c | `GpuSurfaceTests`, `GpuSurfacePipeTests`, native `--gpu-surface`; итоговые commands/reports ниже |
|3slots, bounded outstanding, no reuse до retire | `ThreeSlots_ExhaustionAndOutOfOrderRetirementNeverOverwriteBusyImage`; native three-slots-exhaustion/persistent-reuse (60frames, allocation offers прежние) |
| exact frame/surface/session/version/fence guards | `CompletionGuards_RejectEveryStaleDimensionWithoutAdvancingPhase` (42cases), `SlotGenerations_RejectPreviousUseAfterReuseAndFaultIsTerminal`; native stale-frame/resize/restart |
| immutable exact allocation set/V1 boundary/bounds | `SurfaceOffer_ValidatesEntireAllocationSetBeforeImport` (12), `SurfaceOffer_SnapshotsSourceAndOneFrameV1StillRejectsVersionTwo`, `Extent_PartialZeroAndUnsupportedBoundsAreRejected` (7) |
| changing channels/alpha/origin bytes | `ReferencePattern_IndependentFrameSlotAndSurfaceMarkersPreventStaleFrameAcceptance` pinned literals;80whole-image native frames/configuration и independent73reference hashes |
| pre-allocation protocol validation/suspend generations | `InvalidSurfaceRequest_CannotAllocateNativeResources` (9), `SuspendedSurface_RejectsRepeatedOrSkippedGenerationWithoutNativeWork` (2) |
| App cancellation/drain без native allocations | `AppClose_DrainsSuspendedSurfaceReaderAndRetainsExactZeroAllocationDiagnostics`, `AppClose_CancelsAcceptedConfigureHookBeforeGpuAllocation` |
| actual lifecycle/retry/crash/close | native21cases: exhaustion, persistent-reuse, stale-frame, resize-drains-ready, suspend-resume, invalid-extent-preserves-surface, resize-trend, queued-resize, cleanup-retry, stop-live-slots, cancel/deadline/3crash modes +5restarts, native-close-pending-live-imports |

### Запуски и raw evidence

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~GpuSurfaceTests|FullyQualifiedName~GpuSurfacePipeTests' --logger 'trx;LogFileName=surface-pipe.trx' --results-directory TestResults/ENG4c/focused --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4c/ValidateVerified.ps1
pwsh -NoProfile -File TestResults/ENG4c/Complete.ps1
```

ValidateVerified запускает nonincremental parallel full Debug build, Aether
Debug/Release, full Debug solution, fresh package consumer и всю native matrix:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4c/complete3/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4c/complete3/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4c/complete3/solution-Debug --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4c/ValidatePackageVerified.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4c-verified-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -IncludeGpuSurface -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

Focused78/78; Aether444/444 Debug/Release. Full Debug1847/1847,9TRX/failed0/skipped0:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting79/
Aether444. Nonincremental0errors/1499warnings; warning backlog прежний.
SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest. Базовый commit `dc64b63`, прежние
ENG3/ENG4a/b изменения сохранены, commit/push не выполнялись.

Каждый surface run:21checks/80frames/2982400pixels/12owned children reaped.
3crash modes: до Ready, после Ready перед GL wait, после GL use перед Retire;
после каждого failure fresh restart и old identity rejection. Native WM_CLOSE
отменяет accepted held Produce при live read-complete import, cleanup предшествует
UI disposal.9cooperative reports подтверждают15surfaces/devices и45slots actual
create/release, core+synchronization validation errors0/warnings0, live slots/
NT handles0.3deliberate crashes подтверждены owned reap; их cooperative teardown/
native counters не заявляются. Parent GPU resources/handles/UI sessions0, authoring
unchanged. Independent PowerShell computation сверяет73distinct surface/slot/frame
whole-image hashes; duplicate patterns между fresh identities учитываются отдельно
от identity guard.

Fresh9packages/15commands/4consumers Cobalt+DI × Debug/published Release:
`0.2.2.7-alpha.aetherapp.20261020132536109`. Isolated Directory.Build hierarchy/cache,
все assets libraries — packages, consumer DLL hash равен свежему nupkg DLL.
Проверен managed version2 ledger/pattern, lazy startup и exactly-once GPU/factory/
lease cleanup. Silk.NET.Vulkan2.23.0 прежний. Пакеты локальные, публикации нет;
actual native imports подтверждаются repository runner, не package consumer.

Итоговая полная native matrix включает50aggregate cases/52commands: по20headless,
capabilities, ENG4b import, ENG4c surface, classic viewport и20Replace Editor proof
в Debug apphost/published Release.394source/76binary fingerprints; результаты
final matrix и независимого verifier фиксируются в STATUS/completion.json.
Final ValidateVerified:6commands/exit0, Completed=true; native50cases/52commands/
exit0, independent Complete Passed=true. Classic viewport17cases/27children:
Debug140/Release145pixel checks. Editor20cycles/20failure cases/53children:
Debug779/Release788pixel checks, MaxLive1/один App/window. Остатки0.
Raw: `TestResults/ENG4c/complete3/summary.json`,11TRX,
`TestResults/ENG4c/package3/summary.json`, `TestResults/ENG4c/completion.json`,
`TestResults/EngineSessions/eng4c-verified-20261020/summary.json`, per-case reports,
`surface-reference-hashes.json`. Native source snapshot не менялся во время run.
Final hygiene:15Markdown files/548local links/251anchors,55namespace files,
whitespace errors/staged entries0;7owned helpers завершены. Read-only census
testhost/EngineSessionProbe пуст; validation DLL/manifest hashes повторно совпали.

### Исправления и self-review

Первый native-focused run выявил reuse bug: PollCompletion возвращал allocation
stamp первого кадра. Исправлен current-use stamp; native loop с FrameId>1 является
его regression. Этот failed run не считается passing. Native-reuse/native-retry
затем прошли с19/20cases. Queued-resize case добавлен при review: held Produce
должен завершиться перед detach; competing Produce/Configure отклоняются.

Первый полный50case run прошёл прежнюю20case surface matrix. Следующий run с21case
surface proof остановился в old Debug Editor deadline test: parent увидел `entered`
до закрытия writer и получил sharing violation. HeldProject helper теперь пишет
PID в `.tmp`, закрывает его, затем атомарно публикует marker через Move. Parent
может читать только completed marker; timeouts/driver settings не изменены.
Failed `eng4c-final-20261020` не считается итоговым passing evidence; final snapshot
— `eng4c-verified-20261020` после этого fix.

11new test methods (6facts/5theories),78expanded cases,83Assert call sites:
47Equal/1NotEqual/19exceptions/10Boolean/4collection/2Null. Все методы содержат
behavioral assertions; assertion-free/presence-only методов0. Статический
pseudo-mutation review: no-retire reuse ловится exhaustion/out-of-order tests;
stale/false guards —42cases; descriptor aliases/profile — offer tests; allocation
на invalid/suspend — explicit native device/slot counters; changing pixels — pinned
literals и native whole-image hashes. Native proof дополнительно закрывает queued
resize, cleanup ownership/retry и old identity после restart. Mutant runs,
line/branch coverage и mutation survival percentages не собирались.

ENG4c остаётся diagnostic readback на reference RTX5070Ti/Windows x64/net10.0.
No ordinary GPU viewport/presentation/compositing/first-present, throughput/VRAM
trend/driver isolation/3D claim. Device loss/driver hang не инжектировались;
in-flight driver completion не получает hard deadline. Full Release solution,
UI own Release/packages/golden/RTE/performance/hardware acceptance не повторялись.
K3/K7/K8/VRAM/final alpha и отложенный RTE backlog открыты; Windows/drivers прежние,
K7 workaround search остановлен. Validation layer — прежняя portable extraction
ENG4b; SDK installer не запускался. Следующий шаг — ENG4d actual GPU viewport.

## ENG4b one-frame import и retirement — 2026-10-20

По запросу «давай приступим к следующему шагу» выполнен следующий согласованный
ENG4b. Одна TrueMoon.Aether assembly получила explicit Windows x64 Vulkan exporter,
GL importer, bounded same-build probe channel и App-owned GPU cleanup. Ordinary
CPU viewport не переключается; это one-frame transport proof, без3D renderer.

### Реализация и ownership

- `AetherHost.Gpu` owns resources/connections/producer work, closes admission и
  drains перед session cleanup. Startup lazy; UI context/owner dispatcher borrowed,
  живут до успешного cleanup. Нет отдельного disposable DI alias. Failed cleanup
  сохраняет entry/error; explicit Dispose retry проверен через injected resource.
- Silk.NET.Vulkan2.23.0 — существующая central version, новая прямая зависимость
  Aether. UI/Skia/Game.Logic references не добавлены. Runtime использует UseAether
  через App; low-level native реализации находятся в Gpu/Vulkan и Gpu/OpenGl.
- Dedicated optimal RGBA8 allocation≤64MiB/offset0 и2binary semaphores экспортируются
  как OPAQUE_WIN32. Editor дублирует tokens только через уже owned child process
  SafeHandle; OpenProcess/PID attach/inheritance отсутствуют. Каждая сторона закрывает
  свои NT handles; GL imports сохраняют native references. Partial import unwind
  закрывает уже полученные duplicates/GL objects.
- CurrentUserOnly pipe отдельно от control/frame v1: length1..65536, strict JSON,
  full identity/sequence/stamp/profile. Unresponsive error reader ограничен2s,
  Connect3s/command8s, native probe180s с внешним own-process guard200s.
- ImportAck → Vulkan copy/release EXTERNAL+GENERAL/signal/fence → Ready → GL wait/
  whole-image readback/signal/fence → Release → Vulkan acquire/wait/fence → Retire.
  Ready/Release требуют matching CPU-observed completion. Нет DeviceWaitIdle/glFinish;
  GL fence polls timeout0, ReadPixels — синхронная diagnostic operation.
  Unknown submitted use не разрешает destroy/reuse. Cancel/fault closes channel;
  accepted connect/commands/producer workers входят в drain.
- `GpuImportProbe` сохраняет отдельный report и7cooperative producer teardown
  snapshots. Eighth child намеренно завершается Environment.Exit(43) до Produce;
  отсутствие его cooperative marker ожидаемо, owned reap обязательно.

### Матрица требований и проверок

| Требование текущего шага | Точная проверка / evidence | Результат |
| --- | --- | --- |
| «давай приступим к следующему шагу»: ENG4b one-frame transport | `ManualTests/EngineSessionProbe/GpuImportProbe.cs`, Debug apphost/published Release |3roundtrips на configuration; whole-image bytes совпали |
| Exact identity/profile/bounds/NT tokens до native use | `GpuFrameTransferTests.Offer_InvalidIdentityShapeOrHandleProfileIsRejectedBeforeNativeUse` (24cases), `Offer_ExactSupportedBoundsAreAcceptedWithoutAlteringSourceHandleTokens` (3cases) | Passing; lower/upper bounds, stale/schema/profile/handle aliases |
| Нельзя ждать/retire по early/stale/unconfirmed completion | `Handshake_RequiresImportAndBothObservedFencesBeforeRetirement`, `Handshake_UnconfirmedOrStaleCompletionCannotAdvanceOrAuthorizeGpuWait` (9cases); native early-ready/stale-ready | Passing; phase не продвигается, valid continuation работает |
| Channels/alpha/origin и native pixels | `ReferencePattern_HasIndependentChannelAlphaAndOrientationMarkers`;3native sizes, independent PowerShell SHA256 | Passing; pinned literal markers + whole-image comparison |
| App ownership, shared cleanup и retry | `AppGpuOwner_StartupIsLazyAndCleanupFailuresRetainOwnershipForExplicitRetry`, `GpuOwner_ConcurrentDisposeSharesCompletionAndDrainsAcceptedCleanup`; fresh consumers | Passing; reverse order, retained failed entry/error, closed admission, shared pending completion |
| Bounded protocol/no native work on invalid input | `GpuProbePipeTests.InvalidProtocolInput_CannotReachNativeAllocationAndWorkerIsDrained` (7cases), `MaximumAllowedPayload_ReachesPhaseValidationWithoutNativeWork` | Passing; malformed length/JSON/identity/sequence/phase;65536accepted/65537rejected |
| Unresponsive peer и accepted worker drain | `ErrorReply_UnresponsiveReaderCannotKeepProducerWorkerAliveIndefinitely`, `AppClose_CancelsAndDrainsAcceptedProducerWorkBeforeNativeAllocation` | Passing; original failure retained; App cancellation drains worker |
| Partial import/native failure и session cleanup | bad descriptor/stale-offer/duplicate-handle-failure/missing-gl-procedure/driver-storage-failure; cancel/timeout/crash-before-ready; native-close-live-imports |14cases/run,8owned children reaped, NT/GPU/UI resources0 |
| Validation + package delivery |7producer core/sync teardown reports/run;9fresh nupkg/4Cobalt+DI consumers | Errors0/warnings0; Silk.NET.Vulkan2.23.0 в nuspec/assets; consumer DLL hash == package DLL hash |

### Текущие запуски

Все команды ниже выполнены2026-10-20 на итоговых исходниках. Test platform:
xUnit2.9.3/VSTest, SDK10.0.402/runtime10.0.13. Baseline commit
`dc64b63d0e8da9a05396a74482af2931b30af8db`; прежние ENG3/ENG4a изменения сохранены,
staged entries0, commit/push не выполнялись.

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~GpuFrameTransferTests|FullyQualifiedName~GpuProbePipeTests' --logger 'trx;LogFileName=transfer-pipe-final.trx' --results-directory TestResults/ENG4b/focused --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4b/ValidateComplete.ps1
pwsh -NoProfile -File TestResults/ENG4b/Complete.ps1
```

ValidateComplete выполняет nonincremental parallel full Debug build, Aether
Debug/Release, full Debug solution, fresh package consumer и native runner:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4b/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4b/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4b/complete/solution-Debug --verbosity quiet
pwsh -NoProfile -File TestResults/ENG4b/ValidatePackage.ps1
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4b-complete-20261020 -IncludeViewport -IncludeGpuCapabilities -IncludeGpuImport -GpuValidationLayerDirectory TestResults/ENG4b/validation-layer/Bin
```

- Focused50/50. Aether366/366 Debug и Release. Full Debug1769/1769,9TRX:
  Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting79/
  Aether366; failed0/skipped0. Build0errors/1499warnings; warning backlog сохранён.
- Native48cases/50commands/exit0: прежние20headless +capabilities +GPU import
  +classic viewport +Editor20Replace на каждой configuration.388source/76binary
  hashes повторно сверены; source изменений во время run нет. Прежние selected
  DLL/checkpoint/replacement/20cycles/failure proofs также проверены отдельно.
- Каждый import report:14cases/8children,3roundtrips/923904pixels, handles/resources/
  UI sessions0, authoring preserved, CPU default.7producer validation reports:
  core+synchronization errors0/warnings0, включая teardown. Frame16×16 SHA256
  `CEFFF9288EA786F7337FF4BE76E80CB9082293D6D99164C93FED95C4156ECE58`;
  64×32 `E205F8B81E5DF10DCF6778A727FDC48E36899583AA087E24A43F2A3437E48AD7`;
  1280×720 `52150160BC773ABA67B7AE1FD80682763E0A9C75A853A5364422E2A3712A7655`.
- Fresh packages: version `0.2.2.7-alpha.aetherapp.20261020122712725`,9nupkg/
  15commands/4consumers, Cobalt/DI × Debug/published Release. Isolated restore
  cache/Directory.Build hierarchy; ProjectReference нет, все assets libraries —
  packages. NuSpec Vulkan2.23.0, App startup GPU allocation0, injected resource
  Dispose1, factory/lease Dispose1, late session Start rejected, остатки0.
  Пакеты локальные, публикации нет; native imports проверяются repository runner,
  этот package consumer отдельно проверяет delivery/lazy lifecycle.
- Raw evidence: `TestResults/ENG4b/complete/summary.json`,11TRX,
  `TestResults/ENG4b/package/summary.json`, `TestResults/ENG4b/completion.json`,
  `TestResults/EngineSessions/eng4b-complete-20261020/summary.json` и per-case reports.
  Independent verifier повторяет hashes/completion stamps/cleanup/old proofs,
  проверяет package DLL и assets, namespaces и doc links. Scratch helpers/TRX/SDK
  ignored, не входят в package или commit.
- Финальная hygiene проверка:15Markdown files/547local links/248anchors,
  50namespace files, whitespace/staged errors0;6owned helpers завершились.
  Read-only census testhost/EngineSessionProbe процессов пуст.

Первый validation attempt завершился ErrorLayerNotPresent до allocation;
`native-validation.exit.txt=1` не считается passing. Официальный
[SDK1.4.363.0](https://sdk.lunarg.com/sdk/download/1.4.363.0/windows/vulkansdk-windows-X64-1.4.363.0.exe)
SHA256 `94A82D378F7A5E3E54C9DB7D2FB7016AF136E14AC0A18DBF0F2F67A36352D141`
скачан в TestResults, validation DLL/JSON извлечены7-Zip без запуска installer.
VK_LAYER_PATH/TRUEMOON_GPU_VALIDATION задаются только собственному test process
tree; runner fingerprints обоих файлов. Drivers/Windows settings не менялись.
Preliminary native-focused/native-faults были без validation; final matrix выше —
с core/sync validation. Отдельный диагностический test run сначала завис из-за
непрочитанного error reply в тесте, был остановлен hang guard; тест исправлен,
final focused/full runs passing. Последняя правка ограничивает production error
reply2s и проверяется отдельным non-reading peer case.

### Self-review и оставшиеся границы

11новых test methods (7facts/4theories),50expanded cases,65Assert call sites:
36Equal/15exception/9Boolean/3Null/2Same. Все11методов имеют behavioral assertions;
assertion-free/presence-only methods0. Native custom assertions дополнительно
проверяют pixels, stamps, cleanup/validation/owned processes. Reference literal
markers независимы от generator; whole-image hashes вычислены отдельно.

Pseudo-mutation review статический, выполненных mutant runs нет: удаление
FenceCompleted/stamp/phase guard ловится false/stale/null cases; ослабление bounds/
profile/duplicates — descriptor cases; длина/identity/sequence — pipe tests;
пропуск resource cleanup/error/retry — trace/count/identity tests; смена channels/
alpha/origin — pinned markers/whole-image native hashes. Отсутствие native proof
для device loss/driver hang/in-flight cancellation/slot reuse явно сохраняется.
Line/branch coverage и mutation-survival percentages не собирались.

ENG4b подтверждает bytes и one-frame retirement; actual window presentation/
compositing/color conversion, reusable slots/resize/restart, полная in-flight
crash/recovery matrix — ENG4c/d. Native maximum checked image1280×720; descriptor
bounds1920×1080 проверены unit tests, ordinary CPU viewport имеет свой scope.
Driver isolation, hard real-time deadline, долгосрочный VRAM/resource trend,
performance/3D/shared viewport readiness не объявляются. Full Release solution,
UI Release/golden/RTE/UI packages/hardware acceptance этим шагом не повторялись.
K3/K7/K8/VRAM/final alpha и отложенный RTE backlog прежние; K7 workaround search
остаётся остановленным. Следующий code шаг — ENG4c slots/surface lifecycle.

## ENG4a GPU capabilities и transport decision — 2026-10-20

По запросу «давай приступим к следующему шагу» выполнен ENG4a. Aether/Gpu содержит
immutable capabilities snapshots и pure GpuTransportProbe без UI/native dependencies.
Full SessionIdentity/schema/profile предшествуют adapter matching. Device/driver
UUID — native bytes/hex без Guid byte-order преобразований. Structured Issues
оставляют CPU path; Eligible разрешает только следующий явный import-probe.
DedicatedOnly сохраняется, Vulkan Importable не требуется exporter (importer — GL).
Profile и будущий completion/retirement handshake — [в единственном плане](PLAN.md#eng4a-capabilities-и-transport-decision--2026-10-20).
Stateless utility не требует фиктивного UseX; App integration/lifecycle прежние.
Control/frame wire v1 не меняется. Collector использует существующие graphics
bindings только в example, без новых package versions или Aether dependencies.

Native --gpu-capabilities: Alloy/OpenGL window и App-owned child. Runtime создаёт/
уничтожает Vulkan1.1 instance, перечисляет physical devices/UUID/LUID/extensions/
graphics queues и exact RGBA8_UNORM/2D/optimal/usage23 external image +binary
OPAQUE_WIN32 semaphore properties. Logical device/image/allocation/export не
создаются. GL queries на current UI owner: UUIDs/extensions/procedures/RGBA8/
optimal tiling/fence polling; новые imports/textures/semaphores/fences collector
не создаёт. Обычные Skia UI resources сохраняют прежний lifecycle. Child report
≤64KiB — correlated local prototype evidence, не production capability exchange.
Whole probe45s/runner60s; managed token не гарантирует прерывания native driver call.

### Actual reference configuration и решение

Debug apphost и published Release выбрали index0: NVIDIA GeForce RTX5070Ti,
Vulkan api4211039 (1.4.351), GL3.3.0 NVIDIA617.42. Device UUID
`D69A207DD3EC6D8F5F325359A59FA086`, driver UUID
`8B2ED3A7D66651B8AD47BB85416B871B` совпали в обоих API. Vulkan также перечислил
AMD Radeon(TM) Graphics; он не используется actual GL context и не выбирался.
Image query Success/usage23, memory/semaphore export/opaque compatible bits,
single sample/extent/resource size и все GL requirements доступны, tiling0x9584,
DedicatedOnly=false. **Разрешён ENG4b Vulkan→OpenGL import-probe на этой configuration.**
CPU selected/ImportTested=false. Actual import/pixels/completion/shared GPU readiness,
driver isolation и performance не доказаны queries. Drivers/Windows settings прежние,
K7 workaround search не возобновлялся.

### Фактически выполненные проверки

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m`:
  0errors/1499warnings, новых warnings в GPU files нет.
- `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG4a/complete/tests-Debug --verbosity quiet`:
  316/316, failed0/skipped0; аналогичный Release с tests-Release:316/316.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG4a/complete/solution-Debug --verbosity quiet`:
  1719/1719,9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/
  generator114/Mithril94/UI613/Hosting79/Aether316.
- `pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng4a-complete-20261020 -IncludeViewport -IncludeGpuCapabilities`:
  46/46native cases,48commands/exit0,377source/76binary hashes: по20headless,
  classic viewport, Editor20Replace proof и gpu-capabilities/configuration.
  Новый switch opt-in, прежний runner scope без него сохраняется.
- Оба GPU reports: Eligible=true/index0/issues0/CPU selected/ImportTested=false,
  instance destroyed/1child reaped/released, mappings/UI sessions0/authoring preserved.
  Старые ENG1–ENG3 regressions прошли; individual pixel/child counts сохранены в
  per-case reports и completion.json, без переноса результатов на UI acceptance.

Evidence: `TestResults/ENG4a/complete/summary.json`/logs/11TRX;
`TestResults/EngineSessions/eng4a-complete-20261020/summary.json`, Debug/Release
`gpu-report.json`/`gpu-producer-1.json` с raw tuples/UUIDs и cleanup.
Итоговый read-only fingerprint/report/namespace/doc guard —
`TestResults/ENG4a/completion.json`. Предварительные focused55/60tests/native
сохраняют свои input sets и не заменяют final run. Первый compile исправил
Silk EnumerateInstanceVersion pointer/ref вместо out.

### Матрица ENG4a requirement / evidence

Все строки относятся к запросу «давай приступим к следующему шагу» и принятому ENG4a.
Префикс unit tests: `TrueMoon.Aether.Tests.Gpu.GpuTransportProbeTests.`

| Requirement | Evidence: exact test / run |
| --- | --- |
| Adapter/driver/explicit eligibility/dedicated flag | Eligible_CorrelatesActualDeviceAndDriverPreservesDedicatedAndDoesNotRequireVulkanImport |
| Identity/schema/profile/platform/query rejection | InvalidPeerEvidence_RejectsBeforeMatchingAndKeepsExplicitReason |
| Exact Vulkan image/memory/semaphore/queue/API requirements | NativeRequirementMissing_CannotBecomeEligible |
| Each mandatory GL feature | EachOpenGlRequirement_IsRequiredEvenWhenUuidMatches |
| Extent16..1920×16..1080/minimum bytes | Dimensions_UseExactViewportLimitsAndMinimumResourceSize |
| Immutable lists/all independent GL blockers | Snapshots_DoNotFollowCallerArrayMutationsAndReportAllGlBlockers |
| Incomplete evidence/malformed GL UUID | IncompleteNativeEvidence_DoesNotBecomeEligibleOrThrow; InvalidConsumerUuid_IsReportedBeforeAdapterMatching |
| Last allowed device count/select last adapter | LastAllowedProducerCountAndExactMaximumViewport_CanStillChooseTheLastAdapter |
| Actual App/owned child/current GL/no import/cleanup | Debug/Release-gpu-capabilities: matched UUID/profile, Eligible/CPU selected/ImportTested=false/1reaped child each |
| ENG1–ENG3/build/tests regression | Exact successful commands above:316Aether Debug/Release,1719full Debug,46native cases |

Pre-completion test-gap-analysis/assertion-quality:9methods/62cases,43Assert call
sites; zero assertion-free/trivial-only methods. Two Importable=false fixture checks
are additional to independent result/index/dedicated/issues assertions. Meaningful
reason/index/flag/extent/size/snapshot outcomes и both sides/boundaries защищены;
static verdict Strong, без mutation/coverage-percentage run. Minor argument/null
envelope guards не расширялись. IDE findTests недоступен (Tool not found);
существующий xUnit project/conventions использован без reconstruction workspace.

ENG4a завершён, ENG4 в целом открыт. Далее ENG4b: enabled logical device extensions,
dedicated RGBA8 allocation/export/owned peer import, binary semaphores/CPU-visible
completion, actual CPU-reference pixels и failed-import/cancel/crash/timeout/
handle retirement. Fresh GPU API package consumers/full Release solution, 3D/
long resource/performance и UI K3/K7/K8/VRAM/final alpha не подтверждены.

Final read-only guard:15Markdown documents/544local links/245anchors,
40namespace files,377source/76binary hashes; actual GPU query flags/profile/UUID,
native artifacts/reports и TRX повторно сверены. Whitespace/staged guard прошёл,
staged0; оба owned helpers завершены, baseline dc64b63 прежний. Existing uncommitted
ENG3 changes сохранены, commit/push не выполнялись.

## ENG3d команды Editor и 20Replace proof — 2026-10-20

Реализован следующий согласованный подэтап ENG3. App-owned `AetherHost.Editor`
сериализует Build/Select/Start/Replace/Play/Pause/Step без зависимости от UI.
SetContext snapshots build/dependencies/scene/selection, меняет version, отменяет
pending command и снимает candidate. Completion проверяет также originating runtime
identity/Stop version. Build не применяет artifact автоматически, Select явный.
Cancel сохраняет recovery policy; Stop fences recovery до cancellation, drains
commands/compiler/frames, сохраняет исходную ошибку и допускает cleanup retry.
Close permanently closes admission, host drains Editor до disposal зависимостей.
Новых package dependencies и изменений Core/Enerit/Mithril/UI production APIs нет.

Viewport использует публичные команды, показывает operation/source/project/active/
candidate/errors; Refresh source меняет input revision без изменения authoring.
`--viewport-project` выбирает trusted local Game.Logic project. Дополнительный
`--editor-proof` использует одно App/OpenGL window и actual A/B DLL с разными body
color и camera step. На каждый cycle сверяются loaded path/SHA256, runtime restored
state до mode/frames, first GPU tick, полный checkpoint и следующий Step. GPU
readback проверяет body color вместе с corners/frame/tick, включая bilinear sampling.

### Фактически выполненные проверки

- `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m`:
  0errors/1499warnings; warnings не подавлялись, новых Editor warnings нет.
- `dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3d/complete/tests-Debug --verbosity quiet`:
  254/254, failed0/skipped0. Аналогичный Release с tests-Release:254/254.
- `dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3d/complete/solution-Debug --verbosity quiet`:
  1657/1657,9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/
  generator114/Mithril94/UI613/Hosting79/Aether254.
- `pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3d-complete-20261020 -IncludeViewport`:
  44/44native cases,46commands/exit0,372source/76binary hashes.20headless +classic
  viewport +editor-proof в каждой Debug apphost/published Release configuration.
- Каждый editor-proof:20A/B Replace при одном App/root/window/session, оба modes,
  20failure/interrupt cases,775actual GL pixel checks,53children/maxlive1/allreaped/
  released. Original/recovery exceptions, selected hashes и input hashes сохранены;
  authoring unchanged, Game.Logic в Editor не загружен. Mappings/UI sessions0.
- Classic viewport:17cases/27children/20StartStop в каждой configuration; actual
  GL body/counters/corners, compiler close и cleanup проверены. Default45s Replace
  policy повторена отдельным native replacement case; proof снижает limits до
  Build15s/Replace12s,forward4s,cleanup3s; общий proof≤300s.

Failure matrix: compiler error/cancel/deadline, context change, completed build для
другой session без caller cancellation, Stop build, missing/tampered artifact,
confirmed capture rejection/unknown timeout, candidate bad Hello/restore/first-frame,
double restore failure, Cancel/context/Stop replace, injected lease Stop failure с
реальным owned child и explicit retry, native close при Replace и compiler job.
Injected failure не выдаётся за воспроизведение OS kill failure.

Evidence: `TestResults/ENG3d/complete/summary.json`, individual logs/11TRX;
`TestResults/EngineSessions/eng3d-complete-20261020/summary.json`, per-case reports,
`editor-report.json`, `restored-*.json`, `loaded-*.json`, artifacts/manifests и PNG.
Итоговая повторная сверка — `TestResults/ENG3d/completion.json`.
Предварительный focused native run отдельно:20cycles/20failures/772pixel checks;
он не заменяет final matrix. Первые focused19tests имели2ошибки test expectations:
повторная компиляция не обязана иметь тот же DLL hash, Stop может завершиться до
повторного вызова. Проверки исправлены; separate held-reap test фиксирует shared
completion именно при concurrent Stop. Исходные логи сохранены в ENG3d/focused.

### Матрица ENG3d requirement / evidence

Все строки относятся к запросу «давай следующий шаг» и принятому ENG3d плану.
Префикс unit tests: `TrueMoon.Aether.Tests.Editing.GameEditorTests.`

| Requirement | Evidence: exact test / run |
| --- | --- |
| Build выбирает candidate без изменения world | Build_PublishesCandidateWithoutLaunchingOrChangingRunningWorld; CompilerError_PreservesActiveWorldAndRetainsDiagnosticsForEditor |
| Immutable context и explicit selection | Context_SnapshotsDependenciesAndInvalidatesSelectionOnEveryChange; Select_MismatchDoesNotPauseOrStopOldRuntime |
| Поздняя сборка/selection не применяются к другой session | CompletedBuild_ForAnotherSessionIsRejectedEvenWithoutCallerCancellation; RawSessionChange_HidesCandidateAndRejectsReplaceBeforeApplyingIt; native build-stale-session |
| State/mode и original/recovery errors | Replace_UsesExplicitSelectionAndPreservesStateAndMode; ReplaceFailure_RetainsOriginalAndRecoveryErrorsUntilNextCommand |
| Cancel/context/Stop/close compiler и replacement | HeldBuild_InterruptDrainsOwnedCompilerAndNeverSelectsLateArtifact; HeldReplace_CancelRecoversButStopAndCloseNeverLaunchRollback; native20failure cases |
| First actual presentation и owned cleanup/retry | Start_SelectedBuildWaitsForPresentationThenClearsCandidate; ConcurrentStop_SharesCompletionAndRejectsCommandsUntilOwnedReapCompletes; StopFailure_RetainsOwnedLeaseAndAllowsExplicitCleanupRetry |
| One App/window20Replace, selected DLL behavior/pixels/checkpoint/authoring | Debug-editor-proof/editor-report.json; Release-editor-proof/editor-report.json:20cycles each,775GPU checks each, full runtime restored state and subsequent Step |
| ENG1/ENG2 regression |44native cases, classic17viewport cases/configuration, full Debug1657; exact successful commands above |

Pre-completion test-gap-analysis/assertion-quality:13methods/23cases,109Assert
calls; zero assertion-free/trivial-only methods, awaited async exception checks.
Equality/identity, exceptions/type, deep checkpoint, state/side effects, collections,
null/negative outcomes проверены вместе. Критические stale/Stop/close/recovery/reap
branches защищены paired state/child-count/error assertions; static verdict Strong.
Default fixed-build Start delegates прежнему FrameSession path; отдельные minor
null/invalid-context variants не добавлялись. Это static outcome review, не mutation
или coverage-percentage run. IDE findTests unavailable (Tool not found); scoped
pairing analyzer выполнен один раз,22sources/0tests из-за sibling test project,
после чего использован существующий explicit test project/fixtures.

ENG3a–d завершены в bounded Windows x64/net10.0 CPU transport scope. Следующий
конкретный этап — ENG4a capabilities/transport decision до GPU import implementation.
Fresh package consumers нового Editor API, full Release solution, 3D/shared GPU,
long resource/performance и UI K3/K7/K8/VRAM/final alpha этим не подтверждаются.
Drivers/Windows settings не менялись, K7 workaround search не возобновлялся.

Финальный read-only guard:14Markdown documents,490local links/219anchors,
37namespace files,372source/76binary fingerprints; whitespace/staged guard прошёл,
staged0. Оба owned helpers завершены. Existing uncommitted ENG3a–c сохранены,
commit/push в этом шаге не выполнялись.

## ENG3c serialized Replace и rollback — 2026-10-20

Реализован следующий согласованный подэтап. App-owned AetherHost.Replacement
связывает Pause/capture → old stop/reap → selected candidate start/restore/
first-present → прежний mode, с однократным old-build rollback. Подробная политика,
численные бюджеты и public API ведутся в [плане](PLAN.md#eng3c-принятые-контракты--2026-10-20)
и [README](../../TrueMoon.Aether/README.md#замена-игровой-сборки--eng3c).

### Реализация и ownership

- GameReplacementRequest фиксирует candidate/expected identity/scene/current source
  revision. До Pause проверяются stale selection, scene ID/revision и оба artifacts;
  managed files pinned до конца transaction. Source revision независима от scene.
- Общая explicit reservation у Supervisor/Frames запрещает конкурирующие commands
  и Replace; authority передаётся typed internal lease, без AsyncLocal. Предыдущая
  frame operation завершается до admission; control gate drains начатые commands.
  Stop/close fences pending reservation и accepted work, не допускает late launch.
- Default total45s/forward18s/cleanup10s; phase caps startup15s/mode2s также применяются
  при увеличенных low-level session deadlines. Global deadline ограничивает recovery.
  Options snapshots/validation допускают уменьшение; forward+cleanup<total.
- Confirmed capture rejection сохраняет old child/frame и возвращает прежний mode.
  Unknown capture outcome fault/cleanup. После old Stop caller cancellation допускает
  one recovery без caller token; explicit Stop/App close отменяют и recovery.
- Candidate error всегда остаётся InnerException GameReplacementException, Phase
  и rollback flags наблюдаемы; RecoveryError отдельна. Неудачный rollback не повторяется.
  Fixed legacy и selected old builds поддержаны. Headless ждёт control readiness;
  visible surface — first-present, suspended case отвергается до Pause.
- CleanupAsync удерживает одну owned cleanup task и lease после deadline. Late task
  completion наблюдаем, не снимает CleanupFailed и не разрешает launch; явный Stop
  retry ждёт ту же task, а после fault допускает новую cleanup attempt. Frames
  сохраняют mappings при uncertain reap. App shutdown drains Replace до disposal,
  compiler cleanup выполняется независимо. Общие Core/Enerit/Mithril/UI APIs прежние.

### Матрица требований и новых тестов

GameReplacementTests:15methods/47cases. Real RuntimeProtocol/ProbeGame и frame
producer; fake только lease lifecycle/transport failure seam. Compiler fixture
создаёт actual validated artifacts. Все случаи входят в итоговые231Aether tests.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Paused/Playing × headless/viewport, полный snapshot, first-present gate, selected build/fresh identity и один child | Replace_PreservesCheckpointAndPreviousModeOnFreshSelectedGeneration |
| Hello/restore/first-frame/mode candidate failure восстанавливает fixed/selected old build/state/mode/кадр один раз | CandidateFailure_RestoresOldBuildStatePresentationAndModeOnce |
| Любая из4rollback failures сохраняет точные original/recovery errors, без второго rollback, cleanup | RollbackFailure_RetainsOriginalCandidateErrorWithoutSecondAttempt |
| Confirmed capture rejection сохраняет old child/frame/tick и прежний Paused/Playing mode | ConfirmedCaptureRejection_PreservesOldProcessFrameAndPreviousMode |
| Stale identity/source/scene, corrupt artifact, suspended surface отвергнуты до Pause/Stop | StaleOrInvalidSelection_IsRejectedBeforePauseOrOldStop |
| Capture timeout/disconnect fault и drains, без rollback | UnknownCaptureOutcome_FaultsAndDrainsWithoutRollback |
| Raw Step/mode/Start/Resize/capture и second Replace не вмешиваются в reservation | Reservation_RejectsCompetingCommandsAndSecondReplaceWithoutDisturbingOwner |
| Stop during capture/old-stop/Hello/restore/presentation/rollback fences late launches и drains ownership | StopDuringTransaction_FencesCandidateAndRollbackAndDrainsAllOwnership |
| Caller cancellation до destructive Stop сохраняет old, после reap восстанавливает old без caller token | CallerCancellation_PreservesOldBeforeStopOrRollsBackAfterReap |
| Отдельный cleanup deadline сохраняет одну task/lease, late completion не снимает failure, explicit retry | UnconfirmedCleanup_RetainsOneTaskAndBlocksLaunchUntilExplicitRetry |
| Уже начатый Resize drains до admission, queued Replace отменяется Stop | ExistingResize_DrainsBeforeReservationAndQueuedReplaceIsFencedByStop |
| Raw Supervisor.Stop также прерывает transaction и его frame ownership | RawSupervisorStop_InterruptsReplacementAndDrainsItsFrames |
| Failed reap запрещает rollback/launch; явный retry освобождает retained lease | FailedReap_BlocksRollbackAndCanBeRetriedWithoutLosingOriginalError |
| App close drains accepted Replace и fences restart, options snapshot на Cobalt/DI | AppClose_CancelsAcceptedReplaceBeforeDisposalAndSnapshotsBudgets |
| Positive/max/overflow/phase bounds и reserve, до reservation/process | InvalidBudgets_AreRejectedBeforeAnyReservationOrProcess |
| Actual A→B behavior/CPU pixels/state, rejected candidate recovery, held restore Stop, double failure и max live1 | replacement (Debug apphost, published Release) |
| Actual OpenGL first-present после transaction restore | viewport: replace-restored-before-first-present (обе конфигурации) |

### Финальные команды и результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest.
Итоговый run: TestResults/ENG3c/complete/summary.json.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3c/complete/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3c/complete/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3c/complete/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3c-complete-20261020 -IncludeViewport
```

Final summary Completed=true,5commands exit0. Nonincremental Debug build:
0errors/1499warnings, без warnings в новых replacement contract/test files.
Aether231/231 отдельно Debug/Release, failed0/skipped0. Full Debug1634/1634,
9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/
Hosting79/Aether231, failed0/skipped0. Focused reviewed47/47 Debug.

Native summary Completed=true,44commands exit0,42/42cases:20headless +1viewport
на конфигурацию Debug apphost/published framework-dependent Release без RID.
368source/76binary fingerprints. Каждый replacement case:4actual artifacts,
8game children/max live1, A→B loaded path/hash/CPU color, full checkpoint tick5,
successful old-build rollback/Playing, Stop during held game restore и сохранение
двух причин при failed rollback. Все leases reaped/released, mappings0, authoring/
Editor CLR сохранены. Каждый viewport:17cases/27game children/20StartStop,
actual GL pixels (Debug137/Release135checks), replace-restored-before-first-present
и прежние checkpoint/held compiler close cleanup, sessions/mappings0/childrenreleased.
Read-only TestResults/ENG3c/completion.json проверяет исходные/binary/report и все
selected manifest/DLL/payload hashes, TRX counts, namespace/doc links/whitespace,
staged-state и завершение owned helpers. Это единственный final input set.

### Промежуточные failures и review

Первая narrow build: CS0136 из-за совпадения local/catch variable name; исправлено.
Первый focused run42passed/1failed: fixture продолжал инжектировать candidate
failure в новый явный Start после cleanup retry; injection сброшена до нового
Start. Добавлены direct pre-existing Resize и raw Stop cases:45/45passed.
Initial native replacement прошёл Debug/published Release. Первый полный run:
Aether229/229 Debug/Release, full Debug1632/9TRX, native42 прошли до финального review.

Review уточнил9invalid budget partitions и exact recovery error assertions, cleanup
wait oracle короче total deadline. При первой повторной Debug проверке230/231:
точная timeout assertion обнаружила лишний повторный drain уже очищенного frame
канала после rollback deadline; cancellation добавлялась в Aggregate поверх исходной
ошибки. Production теперь не повторяет drain, если все resources уже освобождены.
После исправления выполнен новый узкий и полный run; failed attempt сохранён в
TestResults/ENG3c/verified, final evidence — только complete paths выше.

code-testing-agent workflow выполнен inline, специализированного generator здесь
нет. Research/plan/status вне Git в session temp (ignored path в
TestResults/ENG3c/state-directory.txt). IDE findTests недоступен: Tool not found.
Roslyn pairing один раз scoped Aether:19source/0tests до новых файлов, так как
test project соседний; exit0, это pairing, не line/branch coverage.
assertion-quality/test-gap-analysis использованы для15новых methods:145syntactic
Assert calls, нулевых/trivial-only нет. Exact state/value/sequence/mode/error identity,
side effects, отрицательные guards, task/failure retention и first-present проверены.
Review закрыл точность recovery errors и независимость cleanup cap; empirical
mutations/coverage metrics не запускались. Checklist привязан к exact names выше.

### Оставшийся scope

ENG3d: Editor build/replace commands и20Replace cycles при одном живом App/window,
полный согласованный failure/pixel proof. Текущие native smoke не заменяют этот
acceptance. Source revision и cancellation при смене editor selection/document
передаёт caller; подписки Aether на конкретную editor model нет. Бюджет ограничивает
control/startup/recovery waits, не гарантирует успешный OS reap: retained cleanup
требует явного retry. Сохраняются синхронная bounded artifact validation и прежний
bounded borrowed-frame drain ENG2; hard real-time завершение arbitrary caller/game
callbacks не заявляется.
Fresh package consumers нового API/full Release solution/performance/VRAM/3D/shared
GPU и hardware K3/K7/K8/final UI alpha не повторялись. Driver/Windows settings
прежние. Commit/push в этом шаге не выполнялись; ENG3a/b working tree сохранён.

## ENG3b checkpoint и startup restore — 2026-10-20

Реализован следующий согласованный подэтап после ENG3a. Optional
ICheckpointGameSession, immutable GameCheckpoint/State/CameraTransform и canonical
TMCK v1 codec находятся в Aether.Contracts. Схема/offsets/лимиты и ownership
зафиксированы в [плане](PLAN.md#eng3b-принятые-контракты--2026-10-20),
[публичный пример](../../TrueMoon.Aether/README.md#checkpoint-и-восстановление--eng3b).
Новых проектов/packages/dependencies и общих Core/Enerit/Mithril/UI API нет.

### Поведение и границы

Capture только в Paused под runtime gate, без продвижения tick или изменения мира.
Версия1 сохраняет host/source build/scene/revision и точный UInt64 tick, finite
camera position/Euler radians, interaction bool. Detached bytes содержат strict
UTF8 и SHA256 integrity. ValidateFor проверяет host/scene/revision до Stop и нового
generation; source build может отличаться от совместимого target build.

Control добавляет CaptureCheckpoint/RestoreCheckpoint и явный outcome. Только
валидированный sequenced Unsupported/CaptureRejected сохраняет paused child,
heartbeat/sequence и Resume. Неожиданная callback/transport error, timeout,
mutation или malformed response дают Faulted и cleanup. Даже исключение типа
CheckpointRejectedException, пришедшее до ACK, не обходит эту policy.

Fresh startup с checkpoint передаёт RequireRestore в child: после LoadScene
применяется snapshot и проверяется полное recaptured state, затем разрешены
SetMode/Step/surface/frames. Restore rejection очищает candidate. FrameSession
startup ждёт presentation восстановленного кадра в прежнем общем15s budget.
Без checkpoint Start явно reset из scene; live restore не поддерживается.
ProbeGame валидирует данные до non-throwing primitive-field commit.

### Матрица требований и тестов

Новые CheckpointCodecTests/CheckpointLifecycleTests:14methods/45cases; вместе
с прежними139 дают184. Fixture использует реальный Protocol/ProbeGame/FrameProducer,
fake lease — только process/transport seam; native cases отдельно проверяют IPC.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Точные UInt64 ticks0/1/MaxValue, canonical magic/version/tick offsets, floats, detached state | Codec_RoundTripsUnsignedTicksExactFloatsAndDetachedState |
| Scene1/128 characters, strict Unicode, нулевые revision/tick и false flag | Codec_AcceptsSceneCharacterBoundariesAndStrictUnicode |
| Version/magic/integrity/length/UTF8/NaN/Infinity/flag/revision и64KiB rejection, включая rehashed invalid fields | Codec_RejectsMalformedOrRehashedInvalidPayload |
| Invalid envelope и host/scene/revision preflight | Envelope_RejectsInvalidIdentitySceneAndCameraBeforeRestore |
| Observational capture и invalid restore без partial commit в игре | ProbeGame_RestoreValidatesBeforeAtomicCommitAndCaptureDoesNotTick |
| Generated request/reply bytes, outcome, exact unsigned tick bits и bounded wire | GeneratedWire_RoundTripsCheckpointBytesOutcomeAndUnsignedTickBits |
| Capture Paused/tick boundary, Playing rejected локально без remote command | Capture_IsObservationalPausedAndAcceptedSequenceRemainsUsable |
| Unsupported/expected rejection/invalid state: old child/heartbeat/sequence/Resume сохранены | AcknowledgedRejection_PreservesPausedGameHeartbeatSequenceAndResume |
| Timeout/disconnect/unacknowledged typed error, malformed ACK/payload/tick и game mutation: Faulted/reap | UnconfirmedOrMalformedCaptureOutcome_FaultsAndReapsInsteadOfPretendingRecovery |
| Pending restore запрещает mode/Step/surface/capture, не сдвигает sequence; после restore commands/frames доступны | RuntimePendingRestore_RejectsModeStepAndSurfaceWithoutAdvancingSequence |
| Complete state до surface/first frame, first-present gate и cross-build provenance | StartupRestore_GatesSurfaceAndFirstFrameUntilCompleteStateIsApplied |
| Unsupported/rejected/no-op/partial restore, corrupt request и bad ACK не дают surface, candidate очищен | FailedStartupRestore_NeverConfiguresSurfaceAndReapsCandidate |
| Host/scene/revision mismatch не меняет old session или следующий generation | IncompatibleCheckpoint_IsRejectedBeforeOldSessionOrGenerationChanges |
| Stop отменяет held restore без display/mapping и drains worker/child | StopDuringRestore_CancelsPendingStartupWithoutPublishingOrLeakingFrames |
| Actual IPC/selected DLL/full state/CPU pixels, explicit reset, rejection/Resume, timeout и Stop during restore | checkpoint (Debug apphost, published Release) |
| Восстановленный tick/state после actual OpenGL first-present | viewport: checkpoint-restored-before-first-present (обе конфигурации) |

### Финальные команды и результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest.
Итоговый run сохраняется в TestResults/ENG3b/verified; промежуточные результаты
не подменяют его. Команды:

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3b/verified/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3b/verified/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3b/verified/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3b-verified-20261020 -IncludeViewport
```

Nonincremental Debug build0errors/1499warnings; новые checkpoint classes/tests
без собственных warnings. Aether184/184 отдельно Debug/Release, failed0/skipped0.
Full Debug1587/1587,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/Aether184, failed0/skipped0.

Final summary Completed=true,5commands exit0. Native summary Completed=true,
42commands exit0,40/40cases:19headless +1viewport в каждой Debug apphost/published
framework-dependent Release без RID.362source/76binary fingerprints сохранены.
Каждый checkpoint case:6game children, saved/first-frame tick3, full recaptured
camera/interaction equality,156byte payload с сохранённым checkpoint.bin/SHA256,
selected artifact path/hash/manifest и source/target build различаются.
Capture rejection позволил Resume; timeout faulted, rejected/held restore очищены.
Authoring state сохранён; Game.Logic в Editor не загружен, все leases reaped/released.
Каждый viewport:16cases/26game children/20StartStop, actual GL pixels (Debug86,
Release84checks), checkpoint-restored-before-first-present и compiler close/reap;
sessions/mappings0/childrenreleased. Read-only completion verifier сохраняет
TestResults/ENG3b/completion.json: source/binary/report/payload/artifact hashes,
TRX counts, namespace/doc links/whitespace/staged-state и owned-helper exit checks.

### Review, промежуточные запуски и ограничения

Focused checkpoint Debug сначала43passed, после review45passed. Первые полные
Aether182/182 Debug/Release, full Debug1585 и native40 прошли до двух добавленных
cases; финальные counts выше относятся к повторному запуску обновлённого input set.
Native initial checkpoint отдельно прошёл в Debug и published Release. Test/code
failures не было; review выявил no-direct-test outcomes pending command gates и
partial restore — добавлены прямой runtime test и partial variant. Повторный full
run обусловлен этими изменениями и XML docs новых API.

code-testing-agent workflow выполнен inline, специализированный generator недоступен.
Research/plan/status сохранены вне Git в session temp (путь в ignored
TestResults/ENG3b/state-directory.txt). IDE findTests недоступен: Tool not found.
Roslyn pairing analyzer запущен один раз, exit0, scoped Aether:17source/0tests,
так как существующий test project находится рядом; это pairing, не coverage.
assertion-quality и test-gap-analysis применены к14новым methods:125syntactic
Assert calls, нулевых/trivial-only tests нет; exact format/value, rich-state,
exceptions, отрицательные assertions, side effects и ordering проверены.
Roundtrip assertions дополнены независимыми format/limit/value pins. После закрытия
двух review gaps core outcomes оценены Strong по source/assertion mapping;
эмпирических mutations и line/branch coverage не выполнялось.

Scope остаётся минимальным checkpoint v1; atomic/observational поведение game
callback — обязательный контракт trusted game. Runtime проверяет tick/revision и
полное state после restore, но не изолирует произвольный код. Replace/rollback,
общий45s budget/20Replace proof — следующие ENG3c/d. Package consumer для нового
API, full Release solution, performance/VRAM/3D/shared GPU и hardware K3/K7/K8/final
UI alpha этим шагом не проверялись. Driver/Windows settings прежние. Изменения не
коммитились/не отправлялись; baseline dc64b63 и ENG3a working tree сохранены.

## ENG3a: compiler, artifacts и child loader — 2026-10-20

Пользователь принял предложенный порядок: сначала commit накопленных изменений,
затем artifact descriptor/compiler job/selected child DLL. Baseline сохранён
локальным commit `dc64b63` (Extract Aether engine module and integrate App lifecycle).
Текущий ENG3a реализован поверх него; прежние extraction/App/metadata/rules изменения
не потеряны. Подробный контракт и численные лимиты ведутся только в
[engine плане](PLAN.md#eng3a-принятые-контракты--2026-10-20).

### Реализация и ownership

- Builds.GameBuildArtifact публикует завершённый managed output в SHA256 manifest
  directory: host/contract hash, entry DLL/type, revision, sorted files/lengths/hashes.
  Metadata чтение не загружает игру в Editor. Полный private dependency set проверен
  по PE metadata; framework/Aether copies, traversal/links/extra/missing/tampered
  payload отвергаются. Immutable-by-publication, с повторной проверкой/pins до reap.
- GameBuildCompiler создаёт owned dotnet process, отдельные output/obj/staging;
  frozen loaded Aether reference, без shared servers и ProjectReference builds.
  Один job,120s maximum (options могут уменьшить). Cancel/timeout закрывают compiler
  tree, ждут reap/log drain, сохраняют bounded diagnostics, удаляют working output.
  Failed cleanup удерживает owned job для retry; новый build до cleanup запрещён.
- AetherHost.Builds — единственный App-owned compiler owner, без disposable aliases.
  Options/request dependencies фиксируются. App Stop закрывает admission и drains
  compiler независимо от frames/supervisor. Completed candidate не вызывает Start:
  caller проверяет revision и явно выбирает Supervisor/Frames.StartAsync artifact.
- SessionLaunch.Build несёт expected directory/BuildId; identity каждого нового
  generation использует выбранный BuildId. Native lease pins files, child повторно
  валидирует manifest/host/contract и GameBuildLoader создаёт IGameSession в отдельном
  collectible AssemblyLoadContext. Shared contract — уже loaded Aether; private DLL
  только из manifest, native game imports вне scope. Context unload cooperative,
  окончательная native boundary — child exit, не одно заявление Dispose.
- RuntimeHost сохраняет legacy fixed Game.Logic branch для ENG1/ENG2. Для selected
  artifact пишет фактические entry assembly Location/hash/PatternColor в report;
  Editor не создаёт/не loads game types. Game.Logic csproj имеет conditional frozen
  AetherContractPath reference. Core/Cobalt/Enerit/Mithril/UI production APIs прежние.
- Viewport Closing теперь terminal AetherHost.DisposeAsync до UI disposal, включая
  compiler/child/readers/pending commands. Smoke запускает held compiler при живом
  child и проверяет cancellation/reap compiler и PowerShell worker до освобождения
  canvas. Self guard примера явно изменён на2minutes без успешных control requests:
  heartbeat продлевает budget при живом Editor/build120s; parent monitor сохранён.

### Матрица требований и новых тестов

Новый GameBuildTests:12methods/24cases, плюс прежние115. Прямые tests для новых
artifact/compiler/loader и existing supervisor integration; общий native proof
проверяет реальный child. Все случаи below входят в финальные139Debug/139Release.

| Требование / наблюдаемое поведение | Точное имя теста / native case |
| --- | --- |
| Immutable manifest ID, две разные game behaviors, shared Aether contract/private dependency | PublishedBuilds_LoadDifferentBehaviorAndShareOnlyHostContract |
| Pin не допускает перезапись и освобождается | PinnedArtifact_CannotBeOverwrittenAndReleasesEveryPinOnDispose |
| Manifest/DLL/missing/extra/host/ID corruption | Open_RejectsTamperedOrMismatchedArtifact |
| Rehashed schema/contract/traversal/framework/duplicate rejection | RehashedManifest_StillValidatesSchemaContractAndDependencyPolicy |
| Полный private dependency set до load | MissingPrivateDependency_IsRejectedBeforeAnyGameLoad |
| Invalid entry и release pins после отказа загрузки | InvalidEntryType_FailsLoadingAndReleasesArtifactPins |
| Compiler exit/diagnostics/no candidate/working cleanup/retry | CompilerFailure_PreservesDiagnosticsDoesNotPublishAndCanRetry |
| App Stop при game+compiler, option snapshot, new-job fence и both providers | AppStop_CancelsCompilerReapsGameAndFencesLateJobs |
| Mutable dependency list фиксируется до build | Compiler_SnapshotsDependenciesAndLeavesCandidateSelectionExplicit |
| Caller cancellation/timeout и фактический compiler/worker reap | CancelOrDeadline_ReapsCompilerTreeAndRetainsNoCandidate |
| Явный per-generation build, active-session conflict, fresh nonce/reap | ExplicitArtifactStart_ChangesBuildIdentityOnlyOnNewGeneration |
| Invalid compiler deadline до процесса | Compiler_RejectsInvalidBudgetBeforeAnyProcess |
| Active old child при compile error/cancel; Stop во время build; фактические A/B child path/hash/color/CPU pixels и file pins | build-artifacts (Debug, published Release) |
| Compiler/child/readers drained до UI disposal | viewport: native-close-cancels-compiler (Debug, published Release) |

Skill code-testing-agent: broad Research→Plan→Implement выполнен inline, поскольку
специализированного code-testing-generator здесь нет. Временные research.md/plan.md/
status.md находятся вне Git в session temp directory (путь в ignored
TestResults/ENG3a/state-directory.txt). IDE findTests вернул Tool not found; Roslyn
pairing analyzer запущен один раз, exit0. Его workspace scan захватил ignored vendor
TestResults sources (2332files), поэтому общие paired/unpaired counts не считаются
coverage или target completeness. Новые классы проверены прямыми tests и matrix.

### Финальные команды и фактические результаты

Windows x64/net10.0, SDK10.0.402/runtime10.0.13, xUnit2.9.3/VSTest. Финальный run:
`TestResults/ENG3a/final/summary.json`, Completed=true,5commands exit0.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3a/final/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/ENG3a/final/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/ENG3a/final/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/eng3a-final-20261020 -IncludeViewport
```

Nonincremental Debug build:0errors/1508warnings. Aether139/139 отдельно Debug и
Release, failed0/skipped0. Full Debug1542/1542,9TRX: Core107, Cobalt179/generator91,
Enerit126/generator114, Mithril94, UI613, Hosting79, Aether139; failed0/skipped0.
Это новый полный Debug run, не сумма исторических module passes.

Native summary Completed=true,38/38:18headless +1viewport в каждой Debug apphost/
published framework-dependent Release без RID. Каждый build-artifacts case запускает
old child плюс A/B (3game children), compiler builds при старой сессии, actual
loaded path/hash/CPU pattern и authoring/host preservation. Каждый viewport —
15cases/25game children/20StartStop, actual GL pixels, native close с held compiler,
sessions/mappings0/childrenreleased.356source/76binary fingerprints сохранены;
отдельный completion verifier проверяет их и raw report/manifest/DLL hashes.

`TestResults/ENG3a/completion.json`: Passed=true;14Markdown files/515local links/
242anchors,24Aether/Aether.Tests C# namespaces по фактическим папкам; whitespace
и staged guards прошли, все собственные validation helpers завершились. Staged0;
ENG3a code остаётся reviewable в рабочем дереве поверх baseline commit.

Предварительный focused6 был23/23 плюс2A/B native probes; после усиления App-close
и dependency snapshot финальные tests139/native38 повторены на окончательных
исходниках. Ранние логи сохранены: xUnit async assertion compile error, namespace
collision в generated fixture, неверное имя FramePattern.Pixel в probe, неверный
expected exception base type и cooperative ALC fixture cleanup исправлены.
Loader Dispose очищает references, fixture даёт collectible contexts GC до temp
cleanup; native test отдельно доказывает pins до reap и release после process exit.

### Границы и продолжение

ENG3a — trusted-local managed artifacts, не MSBuild/game sandbox, не автоматический
Hot Reload/Replace. Immutable artifacts не garbage-collected автоматически. Caller
явно выбирает candidate и сверяет SourceRevision; compiler не держит session gate.
Native/assets deployment, package consumers новой API, performance/long trends,
full Release solution, UI golden/RTE/hardware matrix не повторялись. K3/K7/K8/VRAM/
final alpha сохраняются, driver/Windows settings не менялись.

Следующий шаг — ENG3b checkpoint/restore и recoverable capture failure; затем
ENG3c serialized transaction/rollback с общим45s budget и ENG3d20Replace proof.
Checkpoint/restore/replacement/rollback/3D в этом шаге не реализованы.

## App integration: реализация — 2026-10-19

По запросу «давай реализуем» после аудита добавлены публичные UseAether и
AetherOptions; AetherHost предоставляет App-owned Supervisor/Frames. Options
валидируются и копируются при configuration, report path становится абсолютным.
RuntimeFactory получает App resolver и создаёт свежий provider-owned instance;
конфликт с отдельной регистрацией factory/повторный UseAether отклоняется до
создания factory. Aether использует существующие TrueMoon.Contracts, без новой
зависимости на concrete bootstrap, UI или Game.Logic.

Non-disposable lifetime aliases участвуют в IStartable/IStoppable; единственный
disposable host владеет sessions/frames. App startup готовит host без запуска игры.
App Stop закрывает admission до drain, завершает readers и reap child; sync/async
provider disposal и startup rollback используют тот же cleanup. Concurrent calls
разделяют completion; ошибка наблюдаема, явный Dispose retry допустим. Даже при
ошибке frame cleanup выполняется попытка supervisor cleanup. Borrowed Supervisor/
Frames не зарегистрированы отдельными DI aliases; factory освобождается provider
после host. App Stop terminal, повторные игровые Play/Stop используют session API.

Headless/viewport examples используют UseAether; ручной SupervisorShutdown удалён.
Viewport сохраняет UI close/drain порядок. NativeRuntimeFactory требует child-role
bootstrap и matching Game.Logic DLL в executable consumer; UseAether не создаёт
entry point. Новый native app-stop начинает реальную сессию, останавливает App,
проверяет reap/release и отказ новой generation.

По требованию «вот такие модули должны подключатся через App» правило закреплено
в [AGENTS.md](../../AGENTS.md#подключение-модулей-к-app) и
[общем плане](../PLAN.md#подключение-функциональных-модулей-через-app--2026-10-19).
Utilities/contracts без собственного lifecycle не требуют фиктивной регистрации.

### Требования и integration tests

Новая AppIntegrationTests:11methods/28cases, оба provider Cobalt/Microsoft DI,
существующие87cases сохранены. Прямые ProjectReferences тестов на App и DI adapter
добавлены без executable references; framework xUnit2.9.3/VSTest сохранён.

| Требование / поведение | Точное имя теста |
| --- | --- |
| «давай реализуем»: options snapshot, explicit start, App stop и однократное ownership | Registration_SnapshotsOptions_AndAppStopOwnsExplicitSession |
| Disposal без App.Start, sync/async | DisposeWithoutAppStart_ReapsResolvedHost_ExactlyOnce |
| Failed startup rollback | FailedStartup_RollsBackPreviouslyStartedAetherLifetime |
| Изоляция двух App | IndependentApps_HaveIndependentHostsAndShutdown |
| Повторная registration/factory conflict | DuplicateRegistrationOrFactoryConflict_FailsBeforeFactoryCreation |
| Concurrent shutdown и fence новой generation | ConcurrentShutdown_JoinsCleanup_AndRejectsNewGeneration |
| Наблюдаемый cleanup failure/retry | FailedCleanup_IsObservable_AndCanBeRetriedBeforeProviderDisposal |
| Реальные CPU MMF/receiver/producer drain | AppStop_DrainsFrameWorkersAndMappings |
| Invalid options во время configuration | InvalidOptions_AreRejectedDuringConfiguration |
| Null context/configure | NullArguments_AreRejectedBeforeConfiguration |
| Null factory result при startup | NullFactoryResult_IsAnObservableStartupFailure |

IDE findTests недоступен (Tool not found); использован существующий явно связанный
TrueMoon.Aether.Tests. Тестовые controlled leases проверяют exact disposal counts,
ошибки и ordering; MMF case использует настоящий producer/receiver без UI/GPU.

### Фактически выполненные проверки

Windows x64/net10.0, SDK10.0.402/runtime10.0.13. Финальный source set:
`TestResults/AetherApp/runtime-clean/summary.json`, Completed=true,4commands exit0.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AetherApp/runtime-clean/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherApp/runtime-clean/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-app-clean-20261019 -IncludeViewport
```

Nonincremental build:0errors/1507warnings; новых integration test warnings нет.
Aether115/115 Release и115/115 Debug в full solution. Full Debug1518/1518,9TRX,
failed0/skipped0: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI613, Hosting79, Aether115.

Native summary Completed=true:36/36, по17headless +1viewport на Debug apphost и
published framework-dependent Release без RID. Каждый viewport:14cases/25child
launches/20StartStop, actual GL pixels, native-close-before-UI-disposal,
mappings/sessions0/childrenreleased, Game.Logic не загружена в Editor.
349source/76binary fingerprints проверены после запуска.

Отдельный package slice: `TestResults/AetherApp/final/summary.json`, version
`0.2.2.7-alpha.aetherapp.20261019154515219`.9fresh packages: Contracts/Core/Cobalt/
TrueMoon/Extensions.DependencyInjection/Enerit.Generator/Enerit/Mithril/Aether.
Все9pack + consumer build/publish +4consumer commands exit0. Independent consumer
имеет только PackageReferences, отдельные build props/targets/CPM/global.json/
NuGet.Config/cache; consumer graph из8TrueMoon packages точной версии (Enerit
generator — private build dependency, отдельно упакован), metadata source
свежий feed, project dependencies отсутствуют. Cobalt analyzer отключён. Debug
apphost/published Release ×Cobalt/DI проверили явные Start/Step, App.Stop cleanup,
factory/lease disposal1 и поздний start rejection; loaded DLL hashes совпали с
`lib/net10.0/TrueMoon.Aether.dll` свежего nupkg. Все4processes Passed=true.

Combined package manifest честно сохраняет Completed=false: после успешного
package slice первый full build выявил missing using TrueMoon.Dependencies в
ViewportProbe. Исправлен example import, затем весь runtime scope повторён отдельно.
Ранее initial test compile выявил missing TrueMoon.Services import в AetherOptions;
он исправлен до package proof. Последние test nullable warnings устранены явными
Assert.IsType для Resolve results. Промежуточные logs сохранены; package library
code после pack не менялся. `TestResults/AetherApp/completion.json` проверяет
финальные TRX/native/source/binary/package graph/hashes/links/namespaces отдельно
от неуспешного раннего combined manifest.

Финальный verifier Passed=true:15Markdown files/560local links/234anchors,
18C# files с namespace по реальному пути, diff whitespace/staged guard прошли.
Все6собственных фоновых helpers завершились; staged entries0, commit/push в этом
шаге не выполнялись. Предшествующие uncommitted extraction/metadata изменения
сохранены. Финальные native fingerprints относятся к исходникам после nullable fix.

### Границы и продолжение

Aether наследует IsPackable=true после снятия пользователем прежнего ограничения;
его локальная package delivery теперь проверена. Публичной публикации не было.
API экспериментальный, default native bootstrap application-specific. ENG3a
compiler/artifact остаётся следующим шагом; compiler/checkpoint/replacement/3D
не реализованы. Core/UI production code не менялся. Own Release UI/golden/RTE,
полный Release solution, performance/long resource/hardware matrix не повторялись.
K3/K7/K8/VRAM/final alpha открыты; driver/Windows settings не менялись.

## Проверка App integration — 2026-10-19

По запросу пользователя проверено наличие встроенной точки подключения Aether
к TrueMoon.App. В9handwritten C# sources модуля отсутствуют App configuration
extension, UseAether, IStartable/IStoppable adapters и ссылки на App.Builder.
Это статическая проверка исходников/project graph; новых build/unit/native/
package/performance запусков нет, implementation не выполнялась.

- [SessionSupervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) —
  IAsyncDisposable с явными Start/Stop/control operations; не регистрирует себя
  в App lifecycle. [FrameSession](../../TrueMoon.Aether/Frames/FrameSession.cs)
  аналогично требует отдельного consumer ownership/Stop/drain.
- [Headless example](../../ManualTests/EngineSessionProbe/Program.cs) создаёт
  supervisor вручную, регистрирует private SupervisorShutdown как IStoppable и
  хранит authoring fixture в App. Это локальная composition, не API Aether package.
- [Viewport example](../../ManualTests/EngineSessionProbe/ViewportProbe.cs)
  отдельно создаёт supervisor/FrameSession, подключает Alloy через UsePresentation
  и вручную drains engine tasks/frames при Closing до UI disposal.
- [Child RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs)
  самостоятельно строит App, parent monitor, game/protocol/producer и Enerit server.
  Он не является общим extension подключения Aether к App.
- [IAppConfigurationContext](../../TrueMoon.Contracts/IAppConfigurationContext.cs)
  уже доступен через существующий TrueMoon.Contracts ProjectReference Aether.
  Для module extension зависимость на concrete TrueMoon bootstrap не требуется.
  Существующие примеры аналогичного registration pattern —
  [Mithril AddUnit](../../TrueMoon.Mithril/AppCreationContextExtensions.cs) и
  [Enerit UseInvocationService](../../TrueMoon.Enerit/AppCreationContextExtensions.cs).

Рекомендация: отдельный UseAether registration contract на IAppConfigurationContext,
options/factories и явный lifecycle adapter с определённым ownership/stop/drain/
dispose. Запуск игровой сессии оставить явной операцией; UI выбирает consumer.
Consumer должен получать зарегистрированный supervisor через resolver. При
реализации проверить App Stop/Dispose/startup failure, отсутствие живых owned
children/readers и repeated configuration/DI ownership. Это предложение, публичный
API/реализация в данном audit не добавлялись; ENG3a план не подменён.

Сохранены параллельные пользовательские изменения: namespaces уже соответствуют
подпапкам, в Aether csproj сняты IsPackable=false/GenerateDocumentationFile=false.
Current package eligibility наследуется true из общих props; исторические
metadata/native test fingerprints из предыдущих runs этим audit не обновлялись.
Изменены только engine STATUS/HISTORY/PLAN. Docs links/anchors/whitespace проверены
отдельно; пользовательские code/project изменения не переписывались.

## TrueMoon.Aether — 2026-10-19

По запросу пользователя выделен единый engine module с принятым именем
**TrueMoon.Aether**. Одна library assembly объединяет прежние Engine.Probe.Contracts
и реализацию sessions/protocol/CPU frame transport/native child bridge из
EngineSessionProbe. Namespace implementation — TrueMoon.Aether, contracts —
TrueMoon.Aether.Contracts. Файлы физически перемещены, Compile-linked copies нет.
Framework net10.0, API экспериментальный, IsPackable=false.
[Принятые границы](PLAN.md#truemoonaether-одна-engine-assembly--2026-10-19),
[README](../../TrueMoon.Aether/README.md).

Aether references только TrueMoon.Contracts/Enerit/Mithril и Enerit generator;
нет dependencies на Game.Logic/Alloy/Skia/Silk. Contracts остаются DTO/domain-only
на уровне типов, но теперь находятся в одной assembly с implementation. Editor и
Game.Runtime по-прежнему разные процессы одного example executable; App startup,
конкретная game factory, FaultControl/Records и Alloy viewport остаются example
composition. Game.Logic — отдельная DLL, иначе нарушилось бы требование ENG3
обновлять игровую часть независимо от host. Новых production common-module APIs нет.

TrueMoon.Aether.Tests физически заменил прежний EngineSessionProbe.Tests;
существующие3test files/87cases сохранены с новым namespace и generated adapter
namespace. Direct ProjectReference на Aether и Game.Logic, без executable reference.
Единственный Probe.WaitAsync вызов заменён локальным helper с тем же bounded polling;
assertions не переписывались. Пакеты/xUnit/SDK/TFM остались repository conventions.
IDE findTests недоступен; тесты найдены в существующем явном test project.

NativeRuntimeFactory bootstrap key больше не зависит от executable RuntimeHost;
BuildIdentity.HostId использует entry host +Aether/Enerit/Mithril binaries.
Это сохраняет same-build handshake после переноса contracts в module assembly.
GameBuildId пока вычисляется из AppContext.BaseDirectory/Game.Logic.dll; выбор
versioned artifact и checkpoint/replacement остаются ENG3a–d.

TrueMoon.slnx теперь содержит Aether/Aether.Tests в folder Aether, example/game —
в Engine; старые Contracts/test project entries удалены. Exact `dotnet sln
TrueMoon.slnx list` подтвердил обе новые регистрации. RunProbe source roots включают
Aether/Aether.Tests; Debug/Release publication доставляет новую assembly.
README/общая карта/active status обновлены; link targets на перемещённые sources
исправлены. Исторические команды/результаты старых namespaces ниже сохранены.

### Фактически выполненные проверки

Windows x64, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3. Exact commands:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherExtraction/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-20261019 -IncludeViewport
```

Aether: Passed87/Failed0/Skipped0 в каждой Debug/Release. Full Debug solution:
Passed1490/Failed0/Skipped0,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/Aether87. Это новый solution test run и discovery нового
test project через реальный entry point; не новый nonincremental build или full
Release solution. Generated Enerit nullable/compiler warnings не скрывались;
их policy/исправление не входят в перемещение assembly.

Native summary Completedtrue,34/34: по16headless scenarios и1viewport/configuration,
Debug apphost/published framework-dependent Release без RID. Headless повторяет
по20Start/Stop при одном App, identity/stale replies, startup/command timeout/cancel,
crash/disconnect/heartbeat, owned forced stop/parent exit/reap и authoring fixture.
Viewport — по14scenarios/25child launches, ещё по20Start/Stop при одном App/window,
actual GL pixels/BGRA/origin/counters, resize/zero/resume/slow UI, live-child EOF,
first-frame cancel/deadline и native close/drain-before-UI-disposal. Reports:
ChildrenReleasedtrue/Mappings0/ActiveUiSessions0/GameLogicLoadedInEditorfalse.
Source339/binary76 fingerprints сохранены; итоговая сверка evidence отдельно в
ignored `TestResults/AetherExtraction/completion.json`.

Итоговая сверка прошла: actual Aether test container обнаружен через solution,
test/native DLL hashes совпадают в обеих конфигурациях, старой Contracts assembly
нет в runtime deps graph.34report fingerprints сохранены;12file moves без source
дубликатов и завершение owned helper processes проверены. Документация:
14Markdown files/561local links/229anchors, git diff --check прошёл, staged0.

Own Release UI/golden/RTE/package/performance/physical hardware matrix не повторялись.
Новая assembly не означает готовность ENG3/3D/standalone game/поставки; UI K3/K7/K8/
VRAM/final alpha прежние. Предыдущая документальная сверка ниже остаётся отдельной.
Следующий code шаг — ENG3a внутри Aether с сохранением game DLL/host boundaries.

## Сверка плана после UI INF7 — 2026-10-19

По запросу пользователя проверить общий план сопоставлены общий/UI/engine roadmap,
отложенный RTE backlog и текущие probe sources. На начало сверки main/origin/main
совпадали на `29d9f79860418f86e8e2fca938aafc7fef5e6072`, worktree был clean.
Это review/документация: code/project files не менялись, build/tests/native/
performance/новой проверки runtime evidence не выполнялось.

Вывод: следующий общий этап ENG3 сохраняется; full INF5/RTE развиваются по
потребности UI, hardware alpha остаётся открытой. В
[ENG3 плане](PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19) добавлены
подэтапы compiler/artifact → checkpoint → replacement/rollback → integration proof.
Новых production API, packages, 3D/input/Hot Reload задач не назначено.

| Наблюдение по code/плану | Что уточнено в execution plan |
| --- | --- |
| [Host csproj](../../ManualTests/EngineSessionProbe/EngineSessionProbe.csproj) прямо ссылается на Game.Logic, [RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs) создаёт ProbeGame | Выбранный artifact/loader/dependency set и canonical contracts должны быть определены до ENG3a; Editor DLL не загружает |
| [BuildIdentity](../../TrueMoon.Aether/Hosting/NativeRuntime.cs) читает Game.Logic из AppContext.BaseDirectory, [supervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) хранит fixed game ID | Build descriptor/manifest выбирается на generation, включая rollback; compiler staging отдельно от immutable build-ID directory |
| [IGameSession/control](../../TrueMoon.Aether/Transport/IEngineControl.cs) не имеют capture/restore; [ProbeGame](../../ManualTests/Game.Logic/ProbeGame.cs) хранит только минимальный stub | ENG3b вводит ограниченный versioned checkpoint и проверку non-default state; полной object-graph migration нет |
| [FrameSession.StartAsync](../../TrueMoon.Aether/Frames/FrameSession.cs) уже configure/wait first-present | Candidate publication после restore; первый default-state кадр не считается readiness replacement |
| CommandAsync на любую ошибку вызывает cleanup; [RuntimeProtocol](../../TrueMoon.Aether/Runtime/RuntimeProtocol.cs) не ack sequence при exception | Нужен отдельный nonterminal checkpoint rejection; обещание old paused/Resume нельзя вывести из нынешней transport-fault policy |
| ENG0 требует1game child, а общий раздел допускал подготовку двух сессий | Для ENG3 явно оставлен sequential stop/reap/start; overlap требует нового решения |
| Build120s/Replace45s заданы без распределения общего budget; runtime self guard120s | Перед реализацией определить monotonic budget/rollback reserve/cleanup и совместимый safety guard. Принятые численные deadlines этой сверкой не изменены |

Дополнены вопросы cancellation до/после destructive boundary, late build result,
Stop/новый Start/document revision и Editor close; partial logs/hash/pid/mapping
proof должен принадлежать engine runner. Прежний UI INF7 не становится ENG3 proof.
Исправлены устаревший next ENG1 в RTE плане, InputBatch приписанный ENG2 и
непомеченные исторические формулировки ENG0/ENG1. Общий STATUS/PLAN и UI STATUS
синхронизированы, подробные ENG3 задачи сохранены только в engine PLAN.

Проверка документации прошла:11Markdown files/463local links/204heading anchors,
fences/NUL/conflicts/whitespace, git diff --check и ограничение diff Markdown файлами.
Результат — ignored `TestResults/PlanningFormat/review-20261019.json`; новых runtime результатов
эта проверка не создаёт. ENG3 не начат; следующий code подэтап ENG3a.

## ENG1 headless supervisor — 2026-10-17

После RichTextEditor/PA7a и сохранения отложенного RTE backlog пользователь
перешёл к следующему этапу. Реализован первый исполнимый
[ENG1 scope](PLAN.md#eng1-headless-supervisor--2026-10-17),
[probe guide/запуск](../../ManualTests/EngineSessionProbe/README.md).

### Реализация и границы

- Созданы ManualTests/Engine.Probe.Contracts, Game.Logic, EngineSessionProbe и
  EngineSessionProbe.Tests; все4projects зарегистрированы в TrueMoon.slnx/folder Engine.
  Это probe-only API без production engine packages. Contracts не зависят от
  Mithril/Enerit/Alloy/Skia; существующие production модули для ENG1 не изменялись.
- Supervisor живёт при одном Cobalt Editor App и сериализует session commands.
  Каждый launch создаёт fresh owned ChildProcessUnitHandle и Enerit client;
  RestartPolicy.Never/IsControlAppLifetime=false, process-wide environment не меняется.
  OS Started, HelloAccepted, SceneLoaded и Paused Ready наблюдаются отдельно.
- SessionId сохраняется, UInt64 generation растёт и не переиспользуется после
  failed startup; endpoint/nonce свежие. Host/contracts/Enerit/Mithril и Game.Logic
  имеют отдельные SHA256 manifests. Runtime проверяет bootstrap с собственными bytes
  до открытия RPC. Editor вычисляет Game.Logic hash по файлу без загрузки DLL;
  native assembly census проверяет child-only границу.
- Wire DTOs mutable из-за текущего generator; identity и scene snapshots immutable.
  Enerit Int64 несёт полный UInt64 bit pattern generation/sequence/tick, domain
  counters остаются UInt64 с checked increments. Это явное уточнение wire adapter,
  без расширения общего scalar codec/лимита1MiB/schema negotiation.
- Фактический generated request/response payload ограничивается≤64KiB; scene
  проверяет≤64distinct entities/finite transforms/ID lengths и копируется до mutation.
  Hello/LoadScene/SetMode/Step/GetStatus/Stop имеют explicit identity/sequence/PID/
  mode/revision/tick acknowledgement. Playing timer target60Hz, performance не измерена.
- Stop ставит fence и отменяет локальный wait до входа в gate. Поздние replies и
  queued commands не применяются, interrupted command не повторяется. Faulted
  сохраняет phase/первичную cause; cleanup обязателен. CleanupFailed удерживает
  lease и блокирует Start до явного успешного cleanup retry; acknowledgement не
  выдаётся за process reap. Observer/diagnostic failures не меняют ownership.
- Единый graceful Stop budget3s включает RPC acknowledgement и Mithril stop;
  затем owned kill/reap≤5s. Startup15s, ordinary command2s, heartbeat1s/не более5s
  без accepted reply. Child parent monitor и аварийный2minute self deadline ограничены.

Game.Logic — минимальный отдельный DLL stub с loaded scene/tick/pattern color.
Frame metadata/codec/ring/viewport и input/surface ещё ENG2; checkpoint/restore,
compiler/build directories/replacement/rollback ещё ENG3.20Replace cycles не
выполнялись. Full UI alpha, K3/K7/K8/VRAM, самостоятельная игра/package delivery,
3D/GPU isolation и long resource/performance trend не объявлены готовыми.

### Финальные проверки

Windows x64/build26300/net10.0/SDK10.0.402; xUnit2.9.3/VSTest, central versions
пользователя сохранены. Default Cobalt; отдельная engine Microsoft DI matrix не
требовалась. Исторические UI package/golden/native результаты не повторялись.

| Проверка | Результат | Ignored artifact внутри TestResults/EngineSessions |
| --- | --- | --- |
| Engine Debug suite через solution |31passed/0failed/0skipped | final-tests/solution-Debug/TRX; прежний direct run сохранён отдельно в tests/engine-debug-final.trx |
| Direct engine Release suite |31passed/0failed/0skipped | final-tests/engine-Release/TRX и stdout.log |
| Full Debug solution entry point |1305passed/0failed/0skipped,8TRX; engine31 обнаружен через TrueMoon.slnx | final-tests/solution-Debug/TRX и solution-Debug-tests.stdout.log |
| Nonincremental Debug build |exit0/0errors/1207warnings | final-tests/solution-Debug-build.stdout.log |
| Native Debug apphost |16/16scenarios, включая20Start/Stop в одном Editor App | final-native/Debug-*/report.json/logs |
| Published Release |16/16тех же scenarios, framework-dependent без RID | final-native/Release-*/report.json/logs и publish binaries |

Debug solution counts: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI563, engine31. Release — только engine31; суммарные9TRX/1336passed
не являются двумя полными solution suites. Семь новых warnings — existing
Enerit generator CS8669 на nullable annotations в generated code; warnings не подавлены.

Final native summary:Completed=true,32scenario processes,34commands,187source
fingerprints и24binary entries. По20cycles в каждом repeat scenario, отдельные
sequential generations и recovery после fault. Во всех живых Editor проверяется
неизменность authoring revision/Dirty/selection/panels и App lifetime; каждый
created lease заканчивается подтверждёнными reap/resource cleanup.
Launch-failure case не выдаётся за успешно запущенный процесс.

16scenarios/configuration: repeat, stale, bad-hello, bad-scene, startup-timeout,
startup-cancel, command-timeout, command-cancel, stop-pending, crash, disconnect,
heartbeat, forced-stop, hang-stop, launch-failure, parent-exit. Negative failures
внутри сценариев ожидаемы и проверяются;32passing reports относятся к outer probes.
Hang-stop одновременно не отвечает на Stop RPC и игнорирует graceful process
shutdown: проверены shared deadline, observed StopAcknowledgement cause и owned kill.
Parent-exit proof убивает только принадлежащий ему parent без tree-kill, ждёт
child monitor cleanup marker и read-only OS census; census не является ownership path.

Основные команды из src (точные output paths есть в summary/logs):

```powershell
dotnet sln TrueMoon.slnx list
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Debug --logger "trx;LogFileName=engine-debug-final.trx" --results-directory TestResults/EngineSessions/tests --verbosity quiet
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/final-native
```

Unit evidence: Start_WaitsForHelloAndAppliedSceneBeforeReady,
RepeatedStartStop_UsesFreshLeasesGenerationsAndNonces,
MismatchedHello_IsRejectedBeforeSceneAndConsumesGeneration,
CleanupFailure_PreservesPrimaryErrorAndBlocksStartUntilExplicitRetry,
Stop_FencesPendingAndQueuedCommandsAndDiscardsLateReply,
StartupCancellation_CleansWithoutPublishingReadyAndAllowsNextGeneration,
UnexpectedExit_FaultsAndCleansWithoutCallerPolling,
ModeAndStep_ApplyOnAcknowledgementAndRejectStepWhilePlayingLocally,
InvalidApplicationAcknowledgement_FaultsInsteadOfCommittingState,
GeneratedWire_RoundTripsAllUnsignedBitsThroughBoundedTransport,
OversizedGeneratedPayload_IsRejectedBeforeTransportInvocation,
RuntimeProtocol_RejectsOrderAndSceneErrorsBeforeMutation.
Тесты используют реальные generated codecs и управляемые fake leases; native
matrix отдельно доказывает actual process/IPC/parent-exit composition.

### Исходные failures и дополнительные сверки

Initial build остановился на TME001:UInt64 scalar unsupported; переход wire
fields к bit-preserving Int64 исправил contract без изменений Enerit. Далее
исправлены предположение о generated namespace (он относится к host assembly)
и отсутствующие namespace imports Resolve/UnitCodes. Финальная сборка прошла.
Unit runs с момента первой компилируемой версии31/31, без skipped.

Сначала прошли repeat20 и matrix30cases. При ручной сверке deadlines найден
лишний отдельный3s budget Stop RPC перед Mithril grace; объединён в один token,
добавлен hang-stop и повторены32cases. Затем устранён typeof(Game.Logic) при
Editor hashing, добавлен assembly-boundary assertion и повторена final-native
matrix32cases на финальном source. Ранние passing matrices сохраняют свой
source scope; не выдаются за финальный запуск.

Вспомогательный verifier после уже успешных tests сначала ошибся на TRX filename
с [1] из-за wildcard Get-Content; чтение переведено на LiteralPath. При правке
helper также исправлена лишняя скобка. Исходный failure сохранён в verification/
summary.json, resume — summary-final.json; final-tests выполнен заново после
последнего code change и не использует неполный summary как passing evidence.

Подробные source/test/native logs сохранены внутри ignored TestResults; до
начала работы в дереве были пользовательские изменения и10staged entries.
Они не сбрасывались и не использовались как основание для commit/release.

Финальная сверка:11Markdown documents/301local links/127heading links,
fences/conflicts/NUL/whitespace и новые files whitespace прошли; полная
git diff --check с Windows CR-at-EOL policy чистая. Solution содержит все4engine
projects и test→production reference;187source/24binary fingerprints совпали
после final test/build. Существующие10staged entries сохранены, engine changes
не staged. Отчёт ignored TestResults/EngineSessions/completion.json.
Следующий конкретный шаг — ENG2 CPU codec/ring/viewport с сохранением ENG1 guards.

## ENG0 scope и контракты — 2026-10-15

Пользователь одобрил ENG0 после UI package/Hosting proof: ограниченный Editor/child
prototype, demo-комната/одно взаимодействие, CPU shared-memory frames и versioned
Game.Logic replacement. Допуск с открытыми UI K3/K7/K8/VRAM/final alpha принят явно.
Эти критерии не отменены; full alpha/engine readiness не заявлены.

В PLAN определены Windows x64/net10.0/Cobalt/OpenGL Editor, CPU pattern ENG1–ENG3,
target1280×720/30fps и tick60Hz. Эти targets/deadlines/budgets назначены для будущих
проверок и не являются измеренной производительностью или minimum hardware promise.
Vulkan3D/GPU sharing, standalone delivery и окончательный жанр имеют следующие этапы.

Сверены исходники IUnitsController/UnitsController/ChildProcessUnitHandle/
ProcessUnitHandle/ExternalUnitHandle и UnitConfigurationExtensions: controller
terminal после Stop, общий nonempty parameters unsupported, child запускает entry app.
Выбран один probe executable с двумя process roles, fresh ChildProcessUnitHandle
на каждое поколение и probe-owned per-launch context через protected hooks.
Это проектирование; repeatable use этих hooks в engine ещё не подтверждён.
Изменений Mithril/Enerit/Core/Cobalt/UI runtime в ENG0 нет.

Определены SessionId/SessionGeneration и отдельный SurfaceGeneration, HostContractId/
GameBuildId/versions, фазовая readiness, typed same-build64KiB control, bounded input.
Первый frame transport —3slot CPU ring/BGRA8/sRGB с явными ownership transitions,
latest-ready/drop policy, копированием вне UI thread, bounded memory, stale-frame
rejection и terminal recovery при повреждении/abandoned metadata.
Новый viewport/upload adapter относится к ENG2; наличие готового Alloy API не обещано.

Game replacement: compile candidate отдельно от active build, pause/checkpoint,
stop/reap, candidate restore/first frame, максимум1rollback к last-known-good.
Compile failure сохраняет текущую сессию; snapshot mismatch не reset без явного
выбора. Editor documents/Dirty/selection/panels принадлежат Editor и переживают
обычный child crash/replace. Failed cleanup блокирует новый launch.

Следующий code scope ENG1 — headless host/contracts/logic stub/supervisor с20repeat
Start/Stop, identity/Hello, stale response, startup timeout/cancel, crash/disconnect,
parent exit и cleanup. ENG2 добавляет UI/frame ring; ENG3 compiler/replacement.
Новых csproj/public API/build/tests/process/GPU/performance результатов нет.
Детальные задачи не дублируются в общих документах; их очередность/допуск
синхронизированы с общим PLAN/STATUS/QUALITY и UI STATUS.

### Проверки текущего документа

Фактически выполнено2026-10-15:9Markdown documents,219local links,110heading links,
0issues; проверены fences/conflict markers/NUL/replacement characters, наличие
ENG0–ENG8 и identity/limits/headless milestone. Scoped git diff --check passed,
staged changes0. Артефакт: ignored
`TestResults/EnginePlanning/eng0-20261015/verification.json`; checker
`TestResults/EnginePlanning/CheckEng0.ps1`. Это только documentation validation,
без code tests или нового module/runtime passing evidence.

## Исходное документирование — 2026-10-13

По предыдущему STATUS:42Markdown documents/447local links/107heading links,
0problems; fences/conflict markers/NUL и git diff --check passed.
Артефакты ignored TestResults/EnginePlanning. Новые files проверялись отдельно
на whitespace и наличие ENG0–ENG8. Это историческая документационная проверка,
не текущий запуск и не engine code evidence.

## ENG2 CPU frame viewport — 2026-10-17

По запросу следующего шага реализован [ENG2 scope](PLAN.md#eng2-cpu-frame-viewport--2026-10-17).
Код transport/contracts/viewport остаётся в ManualTests. Единственное production
UI API расширение — owned CopyBgraPixels в SkiaImageSource. Backend-neutral image
borrowing, renderer/device/window ownership, Core/Cobalt/Enerit/Mithril APIs сохранены.

### Реализация

Explicit codec проверяет version/header/layout/capacity/session/build/child/surface;
BGRA8 premultiplied/sRGB/top-left pattern включает corners/frame/tick/actual CRC32.
Windows ring3slots использует named mutex только для metadata, pixels вне lock;
latest-ready освобождает older Ready, producer не перезаписывает Reading и drops
при no Free. Abandonment/corruption/100ms timeout/CRC/EOF terminal, events лишь hints.
Receiver вне UI owner имеет2reusable buffers/1pending; disposed lease отпускает
ссылку на pixels, old receiver drains до allocating replacement buffers.

ConfigureSurface ack следует detach old producer; old mapping живёт до local
reader drain и ack/reap. Resize serial,≤2mappings/50MiB. Zero-size без mapping,
heartbeat сохраняется. First copied frame не означает Ready: FrameSession.Start
завершается только после actual owner presentation, в общий15s launch deadline.
Stop fences queued work/cancels current wait; stale display не подтверждает новый
surface. CleanupFailed retains mappings until explicit successful reap retry.
Native close drains controller/readers/child до UI disposal.

### Проверки на финальном source

SDK10.0.402 / .NET10.0.13 / Windows x64. Final native artifact:
`TestResults/EngineSessions/eng2-final-source-native/summary.json`;
34/34 cases:32ENG1 regressions +2viewport runs, Debug apphost / published Release.
Each viewport run14scenarios,25child launches, по20Start/Stop в одном Editor App/window:
first-presented1280×720, mode/step, resize320×180/1920×1080/16×16/1280×720,
zero-size/resume, slow UI/latest pending, crash, EOF при живом child, first-frame
cancel/15s deadline и native WM_CLOSE. GPU corners/enlarged counter rows сверены
по actual readback с учётом linear sampling, CPU CRC — до публикации receiver.
Final screenshot inspected. Mappings0/UI sessions0/children released/editor preserved,
Game.Logic assembly в Editor не загружена. Source323/binary76fingerprints и36commands
сохранены; final повторная сверка — `TestResults/EngineSessions/eng2-completion.json`.

| Run | Copy p50/p95 ms | Upload p50/p95 ms | Produce→present p50/p95 ms | Pending replacements |
| --- | --- | --- | --- | --- |
| Debug | 4.3877/22.2671 | 0.0593/0.5373 | 31.3073/77.5625 | 21 |
| Published Release | 1.5674/7.0793 | 0.0546/0.6386 | 29.6735/55.3139 | 22 |

Bounded256sample diagnostic windows; observed producer drops0 в GUI runs,
no-free producer drop отдельно подтверждён ring test. Это не baseline/performance
pass, не VRAM/native long-run resource trend. UI hardware blockers не закрываются.

Exact commands записаны runner summary:

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -RunDirectory TestResults/EngineSessions/eng2-final-source-native
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/ui-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

Engine87/87 Debug/Release, UI570/570 Debug/Release; full Debug1368/1368,
8TRX/failed0/skipped0. Engine includes new56cases (codec/wire/ring/receiver/composition)
и31ENG1cases; UI добавлены7shape/ownership/pixel cases.
Build0errors/1211warnings; warnings не подавлены.
Detailed regression evidence: FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip,
Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded,
Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain;
FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay,
Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild,
MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry;
SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha.

### Найденные и исправленные failures

Initial compile corrected Span→array delegate, Check accessibility и font asset path.
First native startup exposed missing Cobalt resolver from generic UsePresentation
registration: added explicit Transient<ViewportView> plus factory Action<string>.
Unit cleanup assertion initially expected missing mapping, but first absent resource
is named mutex (WaitHandleCannotBeOpenedException); assertion corrected to actual API.
Regression on a second read after CRC/EOF exposed misplaced terminal guard; guard
moved into each metadata operation and verified by repeated-fault assertions.
Native IsClosing assignment closed window without native close callback; replaced
smoke trigger with Process.GetCurrentProcess().CloseMainWindow (real WM_CLOSE),
then confirmed drain before UI disposal. First copies-only metrics were overwritten
by no-frame session; switched to bounded aggregate samples. Final review cleared
pixels reference on disposed lease and synchronized receiver counters; full native
matrix repeated after these edits. Failed/intermediate runs remain under ignored
TestResults/EngineSessions/eng2-*; none заменяет final evidence.

Next ENG3 compiler/checkpoint/versioned replacement/rollback,20Replace ещё не было.
InputBatch/focus routing remains deferred explicitly in PLAN. RTE backlog unchanged.

### Матрица ENG2 requirement / evidence

| Requirement | Evidence: exact test / run |
| --- | --- |
| Little-endian schema, unsigned wire/capacity/identity validation | FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip; ConfigureSurface_GeneratedWirePreservesUnsignedGenerationAndDescriptor; Codec_RejectsEveryHeaderIdentityAndCapacityField |
| Latest-ready/drop/Reading ownership | FrameTransportTests.Ring_ChoosesNewestReadyReclaimsOlderAndDropsWhenFull; Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded |
| Terminal CRC/EOF/mutex faults | FrameTransportTests.Ring_CorruptionAndEofAreTerminal; Ring_MetadataTimeoutAndAbandonmentFailWithoutUnboundedWait |
| Two buffers/one pending and hint-only event | FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain; Receiver_ScansWithoutReadySignalAndPropagatesEof |
| Surface ack/presentation/stale/resize/suspend | FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay; ZeroSize_SuspendsReleasesMappingsKeepsHeartbeatAndResumesFreshSurface; Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces |
| Stop fence/cleanup retry | FrameSessionTests.Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild; MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry |
| Owned BGRA image, origin/alpha/shape | SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha; CopyBgraPixels_RejectsInvalidShapeBeforeNativeAllocation |
| Actual process/window/pixels/deadline/close | eng2-final-source-native Debug-viewport/Release-viewport reports;32ENG1 regressions;34/34 native |
| Successful validation commands | RunProbe.ps1 -IncludeViewport; dotnet test engine Release; dotnet test UI Release; dotnet test TrueMoon.slnx Debug; dotnet build TrueMoon.slnx Debug --no-incremental — exact arguments above |

Финальный documentation guard:13documents/470local links/154heading links,
fences/whitespace/conflicts/NUL и scoped/full git diff --check passed.
323source/76binary fingerprints повторно совпали после nonincremental build;
4engine projects/production test reference в solution проверены. Existing10staged
entries сохранены; новые engine/ENG2 test files не staged. Артефакт:
`TestResults/EngineSessions/eng2-completion.json`. Ни один новый package не публиковался.

## Сводки до ENG7a — 2026-10-22

Последняя работа — **2026-10-22, ENG5c.2: reach/raycast и per-object interaction**.
Actual affine cube raycast, nearest/opaque occlusion, inclusive reach и deterministic
ties реализованы в Aether. RoomGame использует reach4 и E pulse для переключения
только выбранного объекта, с возвратом authored RGB. TMCK2 переносит immutable
stable-ID switches, restore валидирует exact loaded set до atomic commit.
Control3/frame1 прежние, Snapshot2 требует rebuild matching binaries, TMCK1 отвергается.
52новых cases; focused102/102, nonincremental Debug build прошёл; full Debug/Release
2186/2186/configuration,9TRX/failed0/skipped0; Aether780/UI615/Hosting79/Mithril95.
[Контракт](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Найдена/исправлена diagnostic JSON constructor-binding regression immutable
object state; отдельный тест проверяет roundtrip и duplicate-ID rejection.
Новый native interaction proof Debug/published Release: по10cases/9PNG/4children,
core/sync0/0, resources/handles/mappings/UI sessions/imported ReadPixels0.
Local Aether pack/payload совпал SHA256 с final managed/native Release;
package drawing consumer не запускался. Все52pending C#namespace/whitespace checks прошли.
Итоговая fresh native matrix после JSON fix60/60cases/62commands,
478source/76binary fingerprints: новые interaction и прежние CPU/GPU/input/
Editor Replace/failure regressions прошли в Debug/published Release.
Summary — `TestResults/EngineSessions/eng5c2-final-jsonfix/summary.json`.
Completion Passed=true; docs10Markdown/284links/189anchors/errors0.
Reports: `TestResults/ENG5c2/managed-summary.json`, `docs-check.json`,
`namespaces-check.json`, `completion.json`; full TRX — `final-*-jsonfix`.
Далее [ENG5c.3](PLAN.md#eng5c3-следующий-шаг-collisions-камеры): минимальный объём
камеры/игрока и fixed-tick collisions, затем target/HUD/pause. Full World snapshot,
runtime edits/MCP, raw OS input/3D performance и прежние hardware/release gates открыты.

Предыдущая работа — **2026-10-22, ENG5c.1: scene consumers и несколько примитивов**.
Stable v1 Transform/Appearance/Interaction schemas, explicit local*parentWorld
projection, deep-owned control-v3 primitive payload и immutable multi-draw snapshot
реализованы. Vulkan рисует пол/стены/три объекта комнаты в прежний App-owned GPU
viewport; separate Game.Logic.RoomGame получает authoring data через IPC.
`--room` использует прежний fixed-tick input/Editor/Replace. Control v1/v2 отклоняются,
matching binaries требуют rebuild; frame1/TMCK1 прежние. Геометрия статична в
пределах scene revision, checkpoint только camera/tick/interaction.
Fresh native matrix58/58cases/60commands,473source/76binary fingerprints:
Debug apphost/published Release, новая комната и прежние CPU/GPU/input/Replace/
failure regressions прошли. Room по5PNG/configuration, core/sync validation0/0,
resources/handles/mappings/UI sessions0, imported ReadPixels0. Local Aether pack
прошёл, DLL совпала SHA256 с managed Release/published proof; package drawing
consumer не запускался. Все42pending C#namespace/whitespace checks прошли.
36новых cases; full Debug/Release2134/2134/configuration,9TRX/failed0/skipped0,
Aether728/UI615/Hosting79/Mithril95. [Контракт](PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
Отчёты: `TestResults/ENG5c1/managed-summary.json`, `package-check.json`,
`namespaces-check.json`, `docs-check.json`, `completion.json`;
native — `TestResults/EngineSessions/eng5c1-final/summary.json`.
На этапе ENG5c.1 далее планировался [ENG5c.2](PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22): bounded
reach/raycast target, наблюдаемое interaction и versioned object state/checkpoint;
затем нужные collisions/HUD. Full world snapshot/runtime editing/MCP остаются
ARCH1c/d/ENG7; performance/raw OS input/прежние UI hardware/release блокеры открыты.

Предыдущая работа — **2026-10-22, ARCH1a.2: scene aggregate/resolver и compatibility adapter**.
SceneDocument фиксирует revision/catalogs, SceneResolver проверяет instance IDs,
parent graph и все entities и публикует immutable ResolvedSceneSnapshot с
parent-first order и exact referenced definition/schema versions. Errors сохраняют
scene/entity path и original cause. SceneSnapshotAdapter задаёт explicit world-space
projection в прежний SceneSnapshot с canonical IDs/revision и limit64entities.
52новых xUnit cases, scene suite130/130. Full Debug/Release2098/2098/configuration,
9TRX/failed0/skipped0; Aether692/UI615/Hosting79/Mithril95.
[Контракт](PLAN.md#реализация-arch1a2--2026-10-22),
[Requirement/evidence/команды](HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
ProbeGame.Load/tick/checkpoint совместимость проверена. Native/GPU/package checks
не повторялись; прежние SceneSnapshot/control/frame/TMCK и интерактивный sample
не изменены. Полный runtime snapshot/edit и MCP запланированы.
На этапе ARCH1a.2 далее планировался ENG5c.1.
Документация: 8 Markdown, 204 локальные ссылки/122anchors, ошибок0. Все24pending
C# файла (9этого этапа) прошли namespace/whitespace checks; scratch artifacts в Git0.
Отчёты: `TestResults/ARCH1a2/managed-summary.json`, `docs-check.json`, `completion.json`.

Предыдущая реализация — **2026-10-22, ARCH1a.1**: immutable definitions/variants/
instances, components/schema/patches и EntityResolver;78новых cases. Full Debug/
Release2046/2046/configuration, Aether640. [Scope/evidence](HISTORY.md#arch1a1-immutable-модель-и-resolver--2026-10-22).

Предыдущая работа над реализацией — **2026-10-21, ENG5b gameplay input/camera**:
control v2, optional IInputGameSession, bounded staging/lease и fixed-tick camera,
отдельная CameraGame, interactive `--scene` с WASD/mouse/E/Escape. Input transient;
camera/tick/interaction сохраняются при checkpoint Replace. App-owned supervisor
проверяет exact identity внутри gate; stale input не retargeted. Один30Hz sender
drains до host/UI; Pause/focus/capture/resize/suspend/Stop/restore сбрасывают input.
Argentis W/S/D/E и Silk mapping дополнены. Raw mouse/cursor lock отсутствуют.
42новых xUnit cases; full Debug1968/1968 иRelease1968/1968,9TRX/configuration,
failed0/skipped0: Aether562/UI615/Hosting79/Mithril95.
Native control/CPU/GPU/3D/input regression56/56cases/58commands; усиленный input
proof2/2 (Debug/published Release), по15cases/5PNG с реальным Silk key callback.
Validation0/0, imported ReadPixels0, resources/handles/mappings/UI sessions0,
sender завершён. После full matrix изменён только proof harness; library DLLs
сверены между полным и усиленным run. Local packs Aether/Argentis/Platform.Silk
прошли, DLL payload сверены с published proof; отдельный package drawing не запускался.
[Контракт, exact tests/commands, provenance и ограничения](HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
На момент ENG5b следующим был ENG5c комната/interaction/HUD; весь ENG5 ещё частичен.

Предыдущая работа — **2026-10-21, ENG5a: первый Vulkan3D renderer**.
Optional immutable scene snapshot под simulation/checkpoint lock, perspective
camera, cube, opaque material и slot-owned depth/pipeline реализованы в одной
TrueMoon.Aether. Vulkan рисует в exportable image без CPU pixels/staging; обычный
App/Editor/GPU transport и default CPU/reference путь сохранены.
Debug и published Release 3D proof: по5PNG assertions (depth/material/camera,
resize/suspend/resume/checkpoint Replace), core/sync errors/warnings0,
resources/handles/mappings/UI sessions0, imported ReadPixels0.
Итоговая native matrix12/12cases/14commands,430source/76binary hashes: новые
scene и прежние import/surface/viewport/ordinary GPU Editor/repeat прошли.
Local pack прошёл; DLL package совпадает SHA256 с native published proof.
Новые focused tests19/19; full solution Debug1926/1926 иRelease1926/1926,
по9TRX/failed0/skipped0: Aether520, UI615, Hosting79, Mithril95.
Это source apphost/published3D proof; package GPU drawing и3D performance не
подтверждены. [Контракт, команды, failures и evidence](HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
ENG5a завершает первый renderer, весь ENG5 остаётся частичным; далее ENG5b input/camera.

Предыдущая работа — **2026-10-21, ENG4e CPU/GPU baseline**. Реализованы opt-in
stage timings, bounded raw/process captures и воспроизводимый runner.
Финальная матрица12/12cases/36intervals/24cooperative children/36pixel checks,
15commands/421source/94binary fingerprints, Completed=true. Release1280×720:
медиана per-run p95 CPU65–66ms/GPU21–33ms; GPU reference allocation~107MiB/s.
Debug CPU:2FPS/6latency advisory breaches, включая12.51fps outlier; budgets прежние.
Raw QPC stages37766, cohorts5244produced/received и5182presented. Local GPU
resources/handles/mappings/UI sessions0; imported ReadPixels0. Это reference
CPU-generated/Vulkan-staging workload, не3D/VRAM/physical-display performance.

Найден/исправлен Mithril exit-delivery/graceful-wait race с red/green regression;
native20shutdown cases прошли. Full solution Debug1907/1907 иRelease1907/1907,
по9TRX/failed0/skipped0: Aether501/UI615/Hosting79/Mithril95, остальные counts
сохранены. Отдельный GPU correctness run:6cases/8commands, Debug/published Release,
core/sync validation0/0; per ordinary GPU Editor20Replace/20failures/53children,
retained copies829/813, imported readback0. Nonincremental build/golden/RTE/full
UI/hardware/VRAM matrix этим не повторены. [Подробные результаты, исходные failures,
команды и ограничения](HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Fresh units packages108/108cases/8nupkgs проверили доставку Mithril fix; это не
Aether GPU package drawing. Независимый replay36intervals/raw37766records и
1048source/170binary hashes прошёл (`TestResults/ViewportBaseline/development/completion.json`).

Исправление структуры — **2026-10-21**: Aether.Contracts устранён;21тип размещён
по назначению в Sessions/Scenes/Transport/Frames/Checkpoints, engine assembly одна.
Все Aether/test/example namespace соответствуют путям. Aether493/493 Debug/Release;
общий solution1898/1898 в каждой конфигурации. Fresh12packages/4App consumers и
итоговая targeted native matrix20cases (Debug/published Release) прошли.
Native checks выявили PID reuse: GPU adapter искал lease по PID в истории.
Оба viewport examples теперь получают текущий owned NativeRuntimeLease напрямую,
а fault decorator раскрывает конкретный inner lease. Probe сохраняет исходную
ошибку до assertion; условия проверок сохранены. Итоговый run включает CPU/GPU
20Replace/20failures proofs. [Команды/границы/миграция](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. UseAether options
выбирают lazy App-owned IGameViewportTransport; default CPU. FrameSession сохраняет
общий Start/Resize/Stop/first-present lifecycle, GameEditor/GameReplacement —
selected DLL/checkpoint/rollback. ViewportSurface не содержит native/mapping handles;
ViewportFrame ticket подтверждается только для актуальной presentation.
GpuViewportTransport копирует borrowed texture в независимый UI image, returns
slot после observed GL completion и удерживает первый image до actual first-present.
Aether остаётся одной assembly без UI dependency.
[Контракт](PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20),
[API](../../TrueMoon.Aether/README.md#выбор-transport-в-gameeditor--eng4d2),
[evidence](HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).

<a id="status-before-planning-revision-2026-11-05"></a>
## Архив STATUS перед ревизией — 2026-11-05

Полный снимок до ревизии очереди после db5ea97. Даты и scope внутри принадлежат прежним работам; это не новые запуски. SHA256 исходного текста: `D20A8C04495C93822C3458601804D44974AE0A9A7C8162C5CEF87CE9D5BBBB0C`.

<details>
<summary>Прежний STATUS целиком</summary>

# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-11-05, ED4g.2a native numeric proof**.
Четыре свежих native OpenGL запуска: source Debug/published Release × standard/
Cobalt и embedded/MS DI. В standard через Windows проверены linked drag/release,
Esc draft cancel, Ctrl+Enter и один Undo; во всех четырёх routed arrows/Shift/Ctrl,
invalid input и exact captured Esc. Retained controls сохранены, final UI/GPU/
handles0, runtime children0. Исправлен ordered pointer position и передача Shift/
Ctrl в Platform.Silk. Свежие full Editor **1295/1295**, Alloy **1101/1101**, Hosting
**154/154** каждый Debug/Release; failed/skipped0, шесть новых UI cases.
[План](EDITOR_PLAN.md#ed4g2a-native-numeric),
[evidence и ограничения](HISTORY.md#ed4g2a-native-numeric-2026-11-05).
Следом physical ↑/↓+modifiers, Esc при удержании кнопки и pointer modifiers:
текущий automation driver отправляет arrows как UiKey.None. Затем group reset/
angle units. Fresh packages, Vulkan/runtime/device-loss/full-solution tests не
повторялись; Observer follow-up отдельно.


Предыдущая работа — **2026-11-05, ED4g.2 numeric gestures и proportional Scale**.
Typed arrow-step/drag меняют только draft; release не Apply. Esc локально отменяет
gesture с exact pre-existing drafts, Ctrl+Enter завершает capture и делает одну
authoring transaction. Scale link opt-in false сохраняет XYZ ratios при typing/
step/drag; preflight targets не затирает invalid/pending-reset peers. Selection/
revision/unique gesture epoch защищают rollback, включая два View с общей моделью.
Full Editor **1295/1295 D/R**, focused180/180 Debug, failed/skipped0;24новых cases.
Отдельно Alloy1095/1095 и Hosting154/154 D/R;64новых cases суммарно. Final Debug
solution build0errors/1555warnings, полный solution test run не выполнялся.
11реальных Skia CPU captures с assertions для no-auto-Apply, linked ratio, exact
cancel и one-history Apply/Undo проверены. Native/GPU/fresh package/physical scope
не повторялся. [Контракт](EDITOR_PLAN.md#ed4g2-numeric-gestures),
[evidence](HISTORY.md#ed4g2-numeric-gestures-2026-11-05).
Далее source/published native input/cancel proof; Observer follow-up отдельно.


Предыдущая работа — **2026-11-05, ED4g.1 остальные редакторы и состояния**.
Все существующие scalar kinds/nullable используют retained PropertyEditorRow:
одинаковая label column88, capped numeric/boolean, строка auto26–78 и компактный
null. Typed Input/addresses, stored drafts, Apply/Cancel/history и ownership
сохранены; actual checkbox indicator получает shared states. Full Editor
**1271/1271 Debug/Release**, failed/skipped0;15новых Editor cases. Отдельно Alloy
**1055/1055**, Hosting **154/154** D/R; focused156/156 Debug. Общая nonincremental
Debug сборка0errors/1555warnings, full solution tests не выполнялись.14Skia CPU
PNG проверены (темы/narrow/modified/focus/error/null/read-only/reset/disabled),
native/GPU/fresh package/physical acceptance не повторялась.
[ED4g.1](EDITOR_PLAN.md#ed4g1-generic-editors), [evidence](HISTORY.md#ed4g1-generic-editors-2026-11-05).
Следующий UI срез — numeric gestures/step/linked scale; Observer follow-up отдельно.


Предыдущая работа — **2026-11-05, ED4g компактный инспектор Transform**.
Три retained строки Position/Rotation(rad)/Scale на NumberBox/Vector3Editor;
labels/text центрированы, узкая панель переносит подпись над тремя равными полями.
Происхождение/индивидуальный reset и metadata раскрываются «⋯»; ошибки остаются
маркером в поле и полным копируемым diagnostics panel. Apply/Cancel/Undo/Redo,
точные schema IDs, ownership и engine contracts сохранены. Уведомления состояния
сведены к одному view update; Load не уведомляет неизменённые computed values.
Full Editor **1256/1256 Debug/Release**, failed/skipped0;12новых Editor cases.
Alloy1020/1020 и Hosting154/154 в обеих конфигурациях проверены отдельно.
[Дизайн-код](../alloy/DESIGN.md), [прикладной план](EDITOR_PLAN.md#ed4g-compact-transform).
CPU raster images покрывают темы/narrow/error/focus/modified/details; native/GPU/
fresh package acceptance не повторялась. Далее числовые gestures/step/linked scale;
Observer scheduling/allocation follow-up остаётся отдельной задачей.


Предыдущая работа — **2026-11-04, ED6b.2e.4e bounded native open batches**.
Default Census от256files открывает максимум4файла параллельно; coordinator один,
validation/hash/byte budgets последовательны, ordered errors/fresh metadata/actual
drain сохранены.21новый case, full Editor1244/1244 Debug/Release, окончательный
focused145/145 в обеих конфигурациях после review; failed/skipped0, новые warnings0.
6/6performance cases:24Same/6Changed, budget0/owners drained.2567files bounds312–
335ms polling/440–470ms notifications вместо514–545/579–602ms (upper−38.5%/−21.9%).
Tradeoff: alloc4.50vs3.66–3.67MiB/cycle, CPU500/672vs406/469ms/cycle. OtherIO/bytes
прежние, small graphs serial; универсальный CPU/memory improvement не заявляется.
54collection input hashes drift0.3fresh profiles/targets/collectors прошли,12Same/
3Changed; native batch подтверждён в2many-files, byte control serial. Concurrent
thread weights не равны scan wall time. Classifier уточнён после сбора с сохранением
collection snapshot, production inputs прежние.56collection records/10827fixtures/
6traces проверены,1current analysis revision отдельно, drift0. Общая nonincremental
Debug сборка0errors/1555warnings; full solution tests не повторены. Следующий шаг —
уменьшить scheduling/allocation overhead batch с прежними ownership/cancel fences.
[Решения/проверки/результаты](HISTORY.md#ed6b2e4e-observer-open-batches-2026-11-04).

Предыдущая работа — **2026-11-04, ED6b.2e.4d opened metadata/prepared native path**.
FILE_BASIC_INFO+size с unsupported-provider legacy fallback и reuse только native
имени для fresh final query реализованы. Full hashing/metadata override/reparse/
access/cancel/drain сохранены.8новых cases; focused124/124, full Editor1223/1223
Debug/Release, failed/skipped0; новые files без analyzer warnings, harness build0/0.
6/6cases:24Same/6content changes, budget0/owners drained.2567files allocation3.66–3.67
vs4.08MiB/cycle, OtherOps16685vs21819. Polling scan514–545msvs588–614ms; notifications
579–602msvs561–593ms, universal latency/CPU improvement не заявляется.53inputs drift0.
Relative/CreateFile2/no-hint native experiments не показали устойчивого extra benefit,
в production не добавлены.3fresh profiles/targets/collectors прошли,12Same/3Changed;
open+handle metadata/close57.88–58.89% census sampled thread weight остаётся главным
resolved участком; unresolved context62.00–63.27% ограничивает дальнейшую attribution.
Следующий шаг — отдельная native/OS attribution, не новая гипотеза о kernel cost.
55input/10827fixture/6trace hashes drift0; общая nonincremental Debug сборка0errors/
1555warnings, full solution tests не повторены.
[Scope/проверки/результаты](HISTORY.md#ed6b2e4d-observer-native-open-2026-11-04).

Предыдущая работа — **2026-11-04, ED6b.2e.4c file open/file-probe metadata**.
Owned no-follow native reader на прежнем worker заменяет per-file async FileStream
strategy/IOCP setup. Opened-before metadata +fresh path-after query, missing probes
без exceptions и без memoization; full hashing/cumulative budgets/ancestor safety
сохранены.41новый case, focused116/116 и full Editor1215/1215 отдельно Debug/Release,
failed/skipped0, новые files без analyzer warnings. Release harness build0/0.
Повторные6/6cases:24full-read Same/6same-length+mtime Changed, budget0, owners drained.
2567files: scan0.561–0.614s вместо0.822–0.855s, allocation4.08vs4.95MiB/cycle;
CPU process/cycle3.99–4.28% одного core вместо6.63–6.92%.53input hashes drift0.
Control/bytes-Polling CPU вырос: universal CPU improvement не заявляется.
3fresh profiles/targets/collectors прошли,12Same/3Changed без budget expiry.
Resolved path metadata32.90–34.16% →2.52–8.96%; open+handle metadata/close теперь
59.23–63.22% census thread weight. Inlining/async→sync и unresolved native context
51.87–57.93% ограничивают attribution; нулевые cache samples не означают zero work.
Следующий шаг — отдельная native/OS attribution оставшегося per-file Open cost.
55input/10827fixture/6trace hashes drift0. Общая nonincremental Debug сборка0errors/
1555warnings; полный solution test run не повторялся.
[Контракт/проверки/полные результаты](HISTORY.md#ed6b2e4c-observer-file-io-2026-11-04).

Предыдущая работа — **2026-11-04, ED6b.2e.4b bounded directory cache Observer**.
Per-attempt positive cache до256каталогов, root-first native rename pins,
fresh-check fallback при недоступном pin/overflow и final path revalidation перед
Same реализованы. Нет reuse между attempts/passes; full hashing/membership/probes,
10s/5s policy и cumulative retry budget сохранены.48новых cases, focused75/75 и
full Editor1174/1174 отдельно Debug/Release, failed/skipped0. Release harness build0/0.
Повторные6/6performance cases:24full-read Same,6same-length+mtime Changed, budget0,
Stopped/watchers0. Actual2567files/11.11MiB: median scan0.822–0.855s вместо1.266–1.524s,
CPU process/cycle6.63–6.92% одного core вместо11.74–11.81%, allocation4.95vs6.75MiB.
52source/DLL/harness hashes без drift. CPU control cases не все улучшились;
snapshot bounds/whole-process counters/warmed cache не являются performance guarantee.
Три fresh profiles:3targets/collectors exit0,12Same/3Changed, budget0; ancestor
attributes39.95–41.90% →0.67–1.07% census sampled thread weight. Следующий шаг —
уточнить per-file open/OSFileStreamStrategy constructor42.59–42.70% inclusive и
metadata32.90–34.16%; unresolved native caller context55.91–55.99% ограничивает
точную attribution.54input/10827fixture/6trace hashes drift0. Общая nonincremental
Debug сборка прошла:0errors/1555warnings; полный solution test run не повторялся.
[Safety/команды/полные результаты](HISTORY.md#ed6b2e4b-observer-directory-cache-2026-11-04).
UI/GPU/fresh package/full solution tests в cache lane не повторены.

Предыдущая работа — **2026-11-04, ED6b.2e.4a профилирование hot path Observer**.
Тот же Release binary/source48hashes сохранён.3managed thread-time traces после
Build/initial pass:2many-files repeats +byte control,3targets/collectors exit0,
12Same/3same-length+mtime Changed, budget0, Stopped/watchers0.
ValidatePath → ancestor Attributes занимает39.95%/41.90% веса census stacks;
вместе с file/probe metadata60.47–60.62%. Source-derived clean-pass model:
26203ancestor visits на103distinct directories (2655validated paths).
Это подтверждённый resolved hot path, не kernel/CPU-cycle/full async breakdown;
native/inlined unresolved context29.32–37.70% в repeats сохраняет свои ограничения.
[Профиль/методика/evidence](HISTORY.md#ed6b2e4a-observer-profile-2026-11-04).
Следующий шаг — bounded reuse/deduplication ancestor validation в пределах pass
с проверкой replacement/reparse/access errors/retry/cancel/drain и прежним full hashing.
Production оптимизация/API/policy и новые unit/native/package checks не выполнялись.

Предыдущая работа — **2026-11-04, ED6b.2e.4 measured Observer performance**.
Manual Release consumer получил real frozen Build descriptor для4project diamond;
production code/API/default10s polling/5s budget сохранены.6/6cases, initial6+
steady18full Same passes, budget-exceeded0,6same-length/same-mtime changes.
Actual2567files/11.11MiB: median scan1.27–1.52s, process CPU11.74–11.81%одного core,
allocation6.75MiB/cycle.139files/129.11MiB:0.156–0.182s, CPU2.61–3.07%,0.62MiB/cycle.
Notifications publication1.58s(files)/0.429s(bytes), polling11.37s/10.18s.
Read bytes равны descriptor на всех24passes; idle0, Stopped/watchers0 у6owners.
48source/DLL +5554restored fixture hashes без drift. CPU/allocations/memory — whole
process+sampler, filesystem cache warmed; native UI/GPU/package/unit/full solution
в этом performance lane не повторялись. [Методика/команды/evidence](HISTORY.md#ed6b2e4-observer-performance-2026-11-04).
Следующий шаг — attribution metadata/path-validation cost на many-files graph,
затем адресная оптимизация подтверждённых затрат с прежними correctness/drain fences.

Предыдущая работа — **2026-11-04, ED4f.3 Window root стандартного Editor**.
EditorMainWindow : Window<EditorViewModel> создаётся App/typed IFactory, содержит
один reusable EditorView и заимствует модель/style. Effective title/client size
копируются после configure callback, header получает прежний EditorView и общий
caption source. Existing EditorWindow/EditorSession сохраняют одну close policy и
first-presentation GPU initialization; embedded путь остаётся raw EditorView.
64новых cases, full Editor1126/1126, Alloy976/976 и Hosting154/154 отдельно
Debug/Release прошли, failed/skipped0; solution Debug nonincremental build0errors/
1584existing warnings. Fresh15packages/headless8/8; source Debug/published Release/
fresh NuGet Release ×standard/Cobalt и embedded/MS DI6/6native workflows:
84children reaped/released,78graceful Vulkan validation0/0,30Game, maxLive1,
final UI/GPU/handles0. Startup failure/cancel12/12,3PNG просмотрены,
2855input hashes без drift. [Реализация/команды/границы](HISTORY.md#ed4f3-window-root-2026-11-04).
UIA2d targeted independent package consumer подтверждён этим Editor profile;
full ED4/ED6 и physical/hardware/K7/release gates открыты. Следующий Aether шаг —
measured observer cost на большом local graph с ordinary10s/5s.

Предыдущая работа — **2026-11-03, source/документационная сверка Window integration**.
UIA2 API и сохранённые проверки сверены; текущий standard Editor ещё использует
EditorView +HostedUiWindow/WindowFrame, отдельного Window root нет. Рекомендуемый
consumer запланирован в [ED4f.3](EDITOR_PLAN.md#ed4f3-window-root): wrapper сохраняет
embedded EditorView, typed root access/overrides, header/viewport и одну close policy.
Core/headless/offscreen/HUD не получают Window; Player решает это в ENG7b.2 с одним
native/device owner. [Сверка и исходные точки](HISTORY.md#window-engine-integration-review-2026-11-03).
Предыдущий UIA2 code run отдельно подтвердил Editor1062/1062 Debug/Release regression;
новые engine build/test/native в текущей документационной работе не запускались.

Предыдущая code работа — **2026-11-03, ED6b.2e.2–3 Observer и source UI**.
Один project-owned worker, bounded hints+polling, full byte/membership/probe census,
sticky Changed, unavailable/recovery и actual stop/drain/retry реализованы. Open
инертен; successful current ScenePlayback Build/reuse привязывает baseline, source
status не заменяет preflight и не меняет game/scene state. Retained status badge/details
читают current-owner immutable snapshot только через owner Present/Refresh.
Full Editor1062/1062 Debug/Release, включая84новых cases, failed/skipped0. Source
Release standard/Cobalt+embedded/MS DI2/2 и fresh package Release2/2 прошли;
15packages/headless8/8,4game children reaped/released, Vulkan validation0/0.
[Решения/команды/evidence](HISTORY.md#ed6b2e23-observer-и-source-ui--2026-11-03).
Следующий шаг — measured cost на большом local graph с default10s/5s; UNC/network/
reparse, wider package/HUD/Player и прежние hardware/release gates отдельные.

Предыдущая работа — **2026-11-02, ED6b.2e.1 descriptor/probes/shared policy**.
Frozen capture выдаёт immutable original-source descriptor через Inputs.SourceWatch:
known files, project-only trees, exact present/absent probes и source fingerprint.
Recursive exclusions/limits общие с compiler; watch-only ошибки дают null/reason,
не меняя full fingerprint, schemas и preflight. Reopen не восстанавливает provenance;
новые properties не сериализуются в diagnostics. Final Aether26/26 (22новых cases
и4package/diamond/reuse) и full Editor978/978 прошли Debug/Release; fresh15packages/
headless8/8, source hashes без drift. Более широкий scoped279/279 Debug/Release
предшествует final TreeFiles уточнению и не подменяет его delta checks.
[Изменения/команды/evidence](HISTORY.md#ed6b2e1-descriptor-и-probes--2026-11-02).
Следующий шаг — ED6b.2e.2 project-owned observer; UI indicator — ED6b.2e.3.

Предыдущая работа — **2026-11-02, ED6b.2e contract source watcher/stale Build**.
По исходникам определены capture-derived descriptor, code/scene/runtime identities,
NoBuild/Checking/ObservedUnchanged/Changed/Unavailable/Stopped, project ownership,
bounded hints+polling/debounce/drain и owner-thread snapshot delivery. Watcher не
заменяет compiler preflight и не вызывает auto Build/Play. Contract находится в
[Editor плане](EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02),
[исходная сверка/границы](HISTORY.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).
В том документационном срезе C# API/observer/indicator не добавлялись, build/test/native
не запускались. Он определил ED6b.2e.1 descriptor/probes/shared policy и tests,
далее project-owned worker/retained indicator. Предыдущие passing результаты ниже
сохраняют собственную область; watcher/hardware/release acceptance ими не закрыта.

Предыдущая работа — **2026-11-02, ED6b.2b.2c.1 native package interrupts/preflight**.
Source/fresh NuGet published Release ×standard/Cobalt и embedded/MS DI52/52cases:
Build/Verify/Restore ×Cancel/Stop/exit и Start/Replace ×stale/corrupt archive.
Package copies/selected DLL и live SDK PID подтверждены; corruption отклоняется
до SDK, stale preflight сохраняет old Playing identity/mode и parent state.
Fresh Build меняет payload, затем actual Start/Replace успешен.76Game children
reaped/released,76graceful validation0/0, maxLive1, final UI/GPU/handles0;
4recoveries только Restore+Cancel. Fresh15packages/headless8/8,2743hash checks без drift.
Production runtime/API не менялись; unit/full solution/ordinary Workflow не повторены.
[Contract/commands/initial harness failure/evidence](HISTORY.md#ed6b2b2c1-native-package-interrupts-и-preflight--2026-11-02).
Далее bounded source watcher/stale Build status: сначала invalidation/ownership/
debounce/drain/UI delivery contract без auto Build/Play. Wider packages, HUD/Player
и прежние physical/hardware/performance/release gates остаются отдельными.

Предыдущая работа — **2026-11-02, ED6b.2b.2c captured managed packages**.
Явный PackageDirectory + schema1/net10.0 lock захватывают unsigned archives до SDK;
версии/edges/SHA512/ZIP/IL-only DLL проверяются, mirrors используют captured Reference.
Один lib target, без build/analyzer/native/content/ref assets; общий source/expanded
budget и private DLL allowlist сохранены. Project v3 добавляет contained packageDirectory,
v1/v2 совместимы. Before/after/prestart/reuse и cleanup используют прежние fences.
SDK-generated lock probe входит в final package33/33; full Editor978/978, отдельно
Debug/Release, failed/skipped0. Scoped293/293 Debug/Release прошли до последнего
nuspec guard; его delta покрывает final package33. Fresh15packages/headless8/8 и
source/NuGet Release ×standard/Cobalt и embedded/MS DI4/4native Workflow прошли:
56children reaped/released,52graceful validation0/0,20Game с selected package DLL,
maxLive1, final UI/GPU/handles0.2059input hashes без drift;2representative PNG просмотрены.
[Контракт, exact tests, commands и текущие результаты](HISTORY.md#ed6b2b2c-captured-managed-packages--2026-11-02).
Full Aether/Alloy/Hosting/solution и hardware/release gates не повторялись.
Следующий шаг — directed native package Cancel/Stop/exit и stale/corrupt archive
перед Start/Replace. Wider package profiles и full ED6b остаются отдельными.

Предыдущая работа — **2026-11-02, ED6b.2b.2b.1 native graph interrupts**.
Source/fresh NuGet published Release ×standard/Cobalt и embedded/MS DI36/36cases:
dependency SDK query, graph prestart verification и candidate restore ×Cancel/Stop/Exit.
SDK log timestamps относятся к live owned PID; verify сравнивает staged Game.Shared.dll
с selected candidate. Admission закрыта до actual drain; compiler staging удалён.
52Game children reaped/released,52graceful Vulkan validation0/0, maxLive1, final UI/GPU/
handles0.4recoveries только Restore+Cancel; Stop/Exit не запускают late child.
Fresh15packages/headless8/8,2735hash checks без drift. Production C# не менялся,
unit/full solution и ordinary Workflow matrix в этом lane не повторены.
[Contract/commands/matrix/evidence](HISTORY.md#ed6b2b2b1-native-graph-interrupts--2026-11-02).
Далее — минимальный locked managed PackageReference closure без build/analyzer/native
assets. PackageReference пока отклоняется; physical/hardware/performance/release gates открыты.

Предыдущий captured graph profile, scoped262/full Editor971 Debug/Release и4native
workflows — [ED6b.2b.2b](HISTORY.md#ed6b2b2b-captured-managed-project-graph--2026-11-02).

Предыдущий single-project native interrupt lane ED6b.2d.1 и исправление Stop/exit order
сохранены в [HISTORY](HISTORY.md#ed6b2d1-native-replace-interrupts--2026-11-02).

Предыдущая работа — **2026-11-01, ED6b.2d document-aware code Replace**.
ScenePlayback строит новый candidate для owned Playing/Paused runtime и того же
authoring state. GameEditor.ReplaceBuildAsync проверяет точный context и tracked
inputs до Pause; общая checkpoint/rollback транзакция прежняя. Scene/document/runtime
stale fences, dirty/history/selection сохранены; recovered child остаётся owned.
DocumentOperation.Runtime fences direct edit/IO до async drain; Close отменяет работу
до authoring disposal. Scoped Aether82/82 и full Editor957/957 отдельно Debug/Release,
failed/skipped0; full Aether/Alloy/Hosting/solution не повторены.
Fresh15packages/headless8/8; source/NuGet standard/Cobalt +embedded/MS DI
4/4workflows: paused swap с тем же tick, compiler failure без смены child, restore
rejection с rollback old code/state/Playing mode.56children reaped/released,20game,
52graceful validation0/0, maxLive1, UI/GPU/handles0;8new PNG просмотрены;2031hash checks
без drift. Initial test-setup и старый child-count harness failures сохранены.
[Contract/commands/matrix/evidence](HISTORY.md#ed6b2d-document-aware-code-replace--2026-11-01).
Профиль требует checkpoint capability и неизменной authoring scene: migration/live
copyback нет. Далее ED6b.2d.1 — directed native Cancel/Stop/exit во время Replace;
engine/model interrupt checks не заменяют этот native lane. Extended graphs/disk
cache/HUD/Player/full ED6b и прежние physical/drain/popup/distinct-PID/release gates открыты.
[Предыдущий reuse](HISTORY.md#ed6b2c-verified-payload-reuse--2026-11-01).

Предыдущая работа — **2026-10-31, ED6b.2b.2a declarative preparation closure**.
CapturePreparation включён в Scene Start и требует TrackInputs+CaptureCompilation.
Поддержанный project/import/source/reference graph копируется до SDK evaluation;
SDK/runtime/host/ref-pack files read-leased, процессы получают private environment/
home/temp/offline NuGet config. Custom tasks/functions/SDK/analyzers и package/project
graphs отклоняются до execution; Windows/installed directories остаются trusted.
Original source/diagnostic paths, generated ProjectDir, repeatable fingerprints/DLL
и lifecycle cleanup сохранены. Реальный Game.Logic/ancestor props/contracts и private
dependency подтверждены SDK tests. Final Aether1704/1704 и Editor943/943 отдельно
Debug/Release, failed/skipped0;21новый case, плюс исправлено ожидание diagnostic event
и изолированы тяжёлые SDK fixtures. [Scope/findings/evidence](HISTORY.md#ed6b2b2a-declarative-preparation-closure--2026-10-31).
Fresh15packages/headless8/8 Debug/published Release ×DLL/apphost ×Cobalt/MS DI,
exit0; window/GPU=false. Это прежний headless lane, не native preparation workflow.
Дальше consumer acceptance и стоимость freeze; native/hardware/release gates,
расширенные graph profiles/cache/reuse/Replace/full ED6b открыты.
Промежуточный Release failure из-за distinct-PID assumption сохранён:8leases,
один PID повторился, все reaped/released. Final passing не закрывает этот отдельный
test-contract follow-up; identity/reap assertions и runtime не менялись.

Предыдущая работа — **2026-10-31, ED6b.2b.1 захваченная C#-компиляция**.
Scene requests включают CaptureCompilation+TrackInputs: trusted SDK preparation отдаёт
actual Csc args; compiler/source/generated/reference/config bytes копируются в owned job.
Отдельный Csc читает эти copies. После Build и перед Start проверяются original
non-generated compiler/reference/config inputs; прежние source stale/context fences сохранены.
CallerFilePath/user PathMap, stable generated/PDB paths и original diagnostics проверены.
Job staging удаляется до возврата Build; cancellation/reap/retention остаются compiler-owned.
C# library profile отвергает opaque inputs/custom extensions; schema1 прежняя,
Compilation descriptor process-local. Полная MSBuild preparation не изолирована.
Aether1683/Editor943 отдельно Debug/Release, failed/skipped0;26новых cases.
Fresh15packages/headless8; published source/NuGet standard/Cobalt +embedded/MS DI4/4:
44children reaped/released (8game),40graceful validation0/0, final UI/GPU/handles0;
4PNG просмотрены. На native compile188files/42105392bytes, stable fingerprints двух builds.
[Команды/findings/границы](HISTORY.md#ed6b2b1-захваченная-c-компиляция--2026-10-31).
Далее ED6b.2b.2 immutable closure до evaluation/preparation, включая toolchain/runtime/
environment assumptions. Arbitrary tasks/generators, package/project graph, cache/reuse/
Replace/full ED6b открыты. Исторические popup/output-drain causes этим run не исправлены;
другие module/full solution suites и physical/release gates не повторялись.

Предыдущая связанная работа — **2026-10-31, общие Bindable/ViewModel**.
Шесть Editor ViewModels переиспользуют Argentis property notifications; InspectorProperty
использует Bindable. Owner-thread guards, dependent properties и Dispose subscriptions
сохранены. Editor923/923 и UI919/919 Debug/Release, failed/skipped0.
[Команды/границы](HISTORY.md#editor-и-общие-bindableviewmodel--2026-10-31).
Aether/другие suites и package/native runs в этом шаге не повторялись. Далее ED6b.

Предыдущая работа — **2026-10-31, ED1b.2 project picker и runtime rebinding**.
Меню «Проект» и retained path prompt используют общую document/project queue и
Save/Discard/Cancel. До возврата admission привязывается actual game build context;
Close его очищает. Workspace/selection обновляются на UI owner, stale actions fenced.
Profile: stopped game/idle compiler; active preview drains/reaps до publication.
Aether1631/Editor923 отдельно Debug/Release, failed/skipped0,22новых cases.
Final15fresh packages/headless8 и published source/NuGet ×standard/Cobalt +embedded/MS DI:
4/4workflows,32children reaped/released,28graceful validation0/0, UI/GPU/handles0;
14PNG просмотрены, final input fingerprints совпадают.
[Команды/initial findings/границы](HISTORY.md#ed1b2-project-picker-и-runtime-rebinding--2026-10-31).
Следующий шаг — ED6b source/artifact/immutable authoring snapshot policy и Build/Play.
OS picker/recents/wizard, extended physical/native/backend/release gates и прежний
intermittent output-drain follow-up остаются отдельными.

Предыдущая работа — **2026-10-31, ED1b.1 project/document transition API**.
Switch проверяет candidate до Save/Discard/Cancel; exact document guard сохраняет
новые edits/history при stale decision. Explicit Close drains preparation/IO, ошибки
Save и cancel сохраняют active owner; post-publication cleanup удерживается для retry.
Editor903/903 отдельно Debug/Release, failed/skipped0,20новых cases.
[Команды/границы](HISTORY.md#ed1b1-projectdocument-transition-api--2026-10-31).
UI picker и runtime/build rebinding после этого API-среза реализованы в ED1b.2 выше.
Общие ED1/ED6b, ARCH1b и прежний intermittent output-drain follow-up открыты.
Другие module suites, package/native/GPU/physical runs этим API-срезом не повторялись.

Предыдущая работа — **2026-10-31, ED4c.3 операции объектов, поиск и context menu**.
Общие create/delete/duplicate batches используют прежние owner/revision/receipts/history.
New позволяет создать объект/куб и child; shared icons/Insert/Ctrl+D/Delete/context
commands, поиск по имени/ID и inspection работают с persisted scene. Retained tree
projection согласует items/selection, не сбрасывает выбор при удалении строки.
Aether1629/Editor883/Alloy909/Hosting117 отдельно Debug/Release, failed/skipped0.
Final15fresh packages/headless8 и published source/NuGet standard/Cobalt workflows2/2:
New→create→Transform→duplicate/delete/Undo/Redo→search→Save/reopen прошёл;
14native children reaped/released,12graceful validation0/0, UI/GPU/handles0,4PNG просмотрены.
[Команды/initial findings/evidence](HISTORY.md#ed4c3-операции-объектов-поиск-и-контекстное-меню--2026-10-31).
Далее project picker/switch и ED1/ED6 workflow. Full ARCH1b/ED4 и прежний intermittent
output-drain follow-up остаются открытыми; новый passing не объявляет его исправленным.

Предыдущая работа — **2026-10-31, ED4c.2 сохраняемые имена и порядок сущностей**.
Общие EntityInstance/ResolvedEntity metadata, atomic HierarchyEditBatch и JSON v1/v2
подключены к прежним document owner/revision/history. Editor даёт inline F2/Enter/Esc,
Ctrl+↑/↓ и before/after drop; ID, component patches, selection и retained controls сохраняются.
Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release, failed/skipped0.
[Audit/8TRX и новые cases](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json).
Final fresh15packages/headless8 и published SourceRelease/PackageRelease standard/Cobalt
workflows2/2 прошли;14children reaped/released,12graceful Vulkan validation0/0,
UI/GPU/handles0.4finalPNG просмотрены после исправления hint glyphs.
[Приёмка/границы и отдельный drain follow-up](HISTORY.md#ed4c2-имена-и-порядок-сущностей--2026-10-31).
Следом ED4c.3 create/delete/duplicate, затем project/workflow; полный ARCH1b и ED4 открыты.

Предыдущая работа — **2026-10-31, ED8a.2 child health/device и acceptance**.
Version2 body в прежнем diagnostic lane доставляет bounded numeric child health и
last selected Vulkan device. UI/report читает cached immutable observation/time,
сохраняет его после crash и отличает unknown/omitted от healthy0. Cached UI device
capability даёт actual presentation metadata без native queries при export.
Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release, final failed/skipped0;
initial Release1fixture failure исправлен first-poll barrier, budgets прежние.
Fresh15NuGet packages:8headless Game cases/24children. SourceRelease и PackageRelease
standard/Cobalt GUI:2/2workflows,14children/2owned crashes,12graceful validation0/0,
UI/GPU/handles0.4PNG просмотрены. [Evidence/границы](HISTORY.md#ed8a2-child-health-и-device-observations--2026-10-31).
ED8a закрыт в этом Windows x64/net10/OpenGL UI↔Vulkan preview и CPU Game scope.
Далее ED4c.2 сохраняемые имена, затем ED4c.3. Persistent compiler history, extended
native/provider/backend matrix, physical/device loss/VRAM/long-load и общий release
остаются отдельными. Core/Enerit/Mithril/full solution suites этим шагом не повторялись.

Предыдущая работа — **2026-10-30, ED8a.1 JSONL history и управляемый report export**.
Schema2 объединяет bounded parent archive/memory, effective scalar config и observed
backend/viewport; explicit text/history choices, privacy notice, user cancel и
single-flight/drain. Default metadata-only, opt-in raw/compiler/exception/source text;
child sink health/device/persistent compiler history явно unavailable.
Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0. Published
Release/Cobalt consumer вне checkout: реальные CPU preview normal/crashed children,
rotated/malformed/incomplete history,5000imports и Dark/Light native Skia raster;
оба children reaped/released, View/App cleanup прошёл. Paused-follow backend gap
исправлен, full Editor повторён. [Evidence/границы](HISTORY.md#ed8a1-архив-и-управляемый-экспорт--2026-10-30).
Далее ED8a.2 child health/device и window/game/NuGet acceptance, затем ED4c.2/3.
Full solution/UI/Hosting/Enerit/Mithril suites, окно/GPU и hardware gates не повторялись.

Предыдущая работа — **2026-10-30, ED6a.3 compiler diagnostics/source navigation**.
GameBuildCompiler читает bounded SARIF2.1 после compile/reap; job/revision/artifact
и failed diagnostics сохраняются после successful retry. SARIF включён в owned
retention; initial full Debug выявил4regressions allowlist, исправлены с повтором.
Editor Compiler mode и borrowed ISourceFileNavigator/command hook дают переход по
frozen location, cancellation/drain и observed failure. Metadata export не содержит
compiler text/source paths. [Контракт, команды и evidence](HISTORY.md#ed6a3-compiler-diagnostics-и-source-navigation--2026-10-30).
Текущие Aether1570/1570 и Editor833/833 отдельно Debug/Release, failed/skipped0;
narrow19+34. SourceDebug/Cobalt/native Skia raster: реальный failure→navigation→
themes→retry,3PNG просмотрены, View/App cleanup. Окно/GPU/внешний editor process,
published/packages/full solution и остальные suites не проверялись этим срезом.
Следующий шаг — полный ED8a report и scoped window/published/flood acceptance.

Предыдущая фиксация планов — **2026-10-30, agent integration и Game AI**.
По запросу пользователя уточнены [ARCH1d.1–4](PLAN.md#локальные-агенты-и-mcp-adapter):
общий API, локальный MCP, Runtime observation/control, authoring/jobs и эксперименты.
Добавлены [ENG9a–d](PLAN.md#eng9-ии-мобов-и-агентные-проверки--2026-10-30): поведение
охранника, bounded decision traces/scenario assertions и optional LLM-персонаж;
UI consumers — ED4g/ED7a в EDITOR_PLAN.md. Все новые этапы запланированы.
Изменены только планы/статусы, ссылки и согласованность проверены чтением;
build/tests/native/packages не запускались. Очередь ED6a/ED8a → ED4c.2/3 →
project/workflow и hardware/release ограничения сохранены. При начале agent среза —
ARCH1d.1: сверка API и contract local attach/headless/identity/budgets/ownership.

Предыдущая реализация и проверка — **2026-10-29, ED6a.2 raw streams и health**.
В console добавлены RAW stdout/RAW stderr/Health: borrowed in-memory runtime/build
snapshots, parent Core sink/ring/emergency/retention и child delivery/capture counters.
Default16processes/4096chars per stream; raw severity не выводится из stderr,
inspected text frozen до явного refresh. Compiler хранит16completed jobs+active,
native64recent leases; nonce/endpoints не входят в snapshot API. Metadata report
включает numeric health, исключает raw/error text и явно указывает budget omissions.
Core387, Aether1556, Editor823 и Hosting117 — Debug/Release, failed/skipped0 в final
runs. Native SourceDebug/standard/Cobalt прошёл raw/health/mode cycle/crash/reopen,
7children reaped,6graceful Vulkan reports0/0, UI/GPU/handles0. Native finding исправлен
в Hosting: visual dirty после Update переносит presentation на следующий bounded tick.
[История и audit](HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
Далее compiler file/line/column navigation и полный ED8a: history/raw export policy/
child sink health/backend/config/user cancel. Published/provider/flood/physical gates открыты.

Предыдущая работа — **2026-10-29, ED6a: structured console и первый partial export**.
«Вид → Диагностика»: bounded reader, фильтры/search/grouping, inspect/copy,
clear view и pause follow-tail; retained View/Data/style/IFactory и App ownership.
Metadata-only JSON export до512KiB/cooperative5s, без текста/stack/arbitrary properties,
с explicit partial/unavailable и no-overwrite. Editor808/808 Debug/Release,
failed/skipped0. Native SourceDebug/standard window/Cobalt: live child history,
crash/recovery, filters/groups/retained inspect/reopen/export;7children reaped,
6graceful Vulkan core/sync reports0errors/0warnings, UI/GPU/handles0 после Dispose.
[Evidence и границы](HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).
Далее ED6a.2: raw stdout/stderr и borrowed sink/delivery/capture health, затем
compiler file/line/column navigation и полный ED8a report. Полные ED6a/ED8a,
published/package/native-provider matrix и flood/slow-UI acceptance ещё открыты.

Предыдущая работа — **2026-10-29, ARCH2e.4: independent file worker**.
После EOF file-drain больше не зависит от свободного shared ThreadPool worker:
dedicated worker + sync wake-up/IO, queue8/budgets/borrowing и2s/5s deadlines прежние.
Controlled RED→GREEN:24bytes/EOF завершены, единственный pool worker удерживается,
оба completion tasks и файл завершаются. Один worker на active file capture —
явная цена; no-file mode без worker. Core374/374 и Aether1550/1550 Debug/Release,
headless8/8 на15fresh packages/625inputs, native24/24/56children и48graceful
validation0/0 прошли. UI/GPU/handles0 после App dispose, owned processes0.
[Audit/current4TRX](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json),
[headless](../../TestResults/ARCH2e4-FileDrain-Packages-20261029-2/verification.json),
[Core proof/решение](../core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).

Scoped ARCH2e.4 contract выполнен; первый ED6a/ED8a consumer реализован выше.
Точная причинная цепочка исторического intermittent timeout не установлена:
controlled proof и passing matrix не выдаются за failure heap прежнего эпизода.
Observation и atomic marker/phase counters сохранены; при повторе нужен targeted
analysis. Arbitrary filesystem stalls/global real-time guarantee не обещаются.
Hosting/Editor/UI suites и full solution в ARCH2e.4 не повторялись; новый Editor run указан выше. Hardware/
K7/VRAM/general release и причина BSOD отдельно; массовый stress не запускался.
Launch roles ENG7b, workflow ED6b и runtime HUD ENG5c.4 остаются в плане;
Aether core не получает UI dependency.

## Реализация и связь с планом

ARCH2a–d и quality follow-ups входят в предыдущие commits; текущая работа
добавляет ARCH2e.1–3 package/native proof, runtime startup и Hosting cleanup fixes. Engine/Editor остаются
library/packable modules, App/DI/lifecycle и единственный owner сохраняются.

| Изменения рабочего дерева | Этап и текущее состояние |
| --- | --- |
| Create/delete/duplicate batches, New presets, search/context commands, retained tree projection | ED4c.3 checked: Aether1629/Editor883/UI908/Hosting117 Debug/Release; final15packages/headless8/native2. Typed entity references/catalog management/multiselect и full ARCH1b/ED4 отдельно |
| Persistent Name/SiblingOrder, HierarchyEditBatch/history, JSON v1/v2; Editor inline rename/reorder | ED4c.2 checked: Aether1602/Editor866/UI900/Hosting117 Debug/Release; final15packages/headless8/native2. Intermittent headless drain observation остаётся отдельным follow-up |
| Runtime observation/version2 body; cached Vulkan/OpenGL device metadata; Editor health/report; package/native assertions | ED8a.2 checked: Aether1581/Editor847/UI893/Hosting117 Debug/Release;15fresh packages/headless8 и source/NuGet published native2, validation12×0/0. Extended matrix/release отдельно |
| Core archive/configuration views, Editor schema2/text/history/cancel/viewport | ED8a.1: Core398/Aether1570/Editor842 Debug/Release; published Release/Cobalt CPU preview normal/crash + raster/flood. Child health/device и broader native/NuGet acceptance открыты |
| Core borrowed health/window APIs, Aether process history/source, Editor raw/health, Hosting dirty-frame deferral | ED6a.2 checked: Core387/Aether1556/Editor823/Hosting117 Debug/Release; native SourceDebug/Cobalt1/1. Compiler navigation/full ED8a и wider acceptance открыты |
| Editor Diagnostics, header/layout/session и Gui consumer | ED6a structured console + ED8a metadata-only partial export: Editor808 Debug/Release и один native SourceDebug/Cobalt workflow; full raw/health/compiler/published acceptance открыт |
| AetherPackageConsumer/Gui/VerifyGui, Hosting callback lifetime capture | ARCH2e.3 native24/24, Hosting114/Editor763 Debug/Release и fresh package/headless8/8 после fix; native cancellation RED→GREEN, полный teardown до passing report |
| AetherPackageConsumer, AetherRuntimeHost worker startup, RuntimeLaunchProbe/NativeRuntimeProcessTests | ARCH2e.1/2 и CPU startup fix проверены29октября; предыдущий Aether1543/1543 Debug/Release не считается новым run этого native шага |
| Examples hosts/resources и VerifyExampleHosts | Resource/headless proof28октября дополнен standard-window/embedded-View native composition29октября. GUI matrix проверяет публичный модульный API, не каждый CLI branch Examples |
| FrameWorkers/SharedFrameRing и FrameTransportTests | ENG2 pending CPU frame больше не теряется при empty/stale poll и EOF; lazy reservation сохраняет два private buffers, UI lease и drain. Follow-up закрыт в проверенном CPU scope |
| BuildProbe/ReplacementProbe и NativeRuntimeProcessTests | ENG3 fixtures мигрированы на IGameSessionEntry/Contracts без ослабления production guards; build/selected artifact/checkpoint/rollback/cleanup follow-up закрыт |
| GameBuildCompiler/GameBuildRequest, deep-job regression | ENG3 single-project staging сокращает intermediate path при неизменной GUID isolation. MSB3030 follow-up закрыт в проверенном TEMP/LongPathsEnabled0 profile; arbitrary long paths не обещаются |
| ChildDiagnosticFixture/ForwarderTests/LimitsTests и delivery guide | ENG8a.3/ENE6d.4 test-quality fix: codec clipping отдельно от real-pipe bounded outcomes. Поздний ACK не переводит уже Unconfirmed в Accepted; runtime и budgets прежние |
| Mithril composition/restart probes и ReadyFileProcessTests | Связанный fixture quality fix закрыт в [Mithril STATUS](../mithril/STATUS.md); runtime/API Mithril и полный MIT6 этим не меняются |

Публичные границы/owners — [PACKAGE_BOUNDARIES](PACKAGE_BOUNDARIES.md).
Минимальные hosts — Examples/AetherEditor и Examples/AetherRuntimeHost;
directed workflows и explicit same-exe runtime — ManualTests/AetherEditorHost.

## Последние датированные проверки

Запуски29октября выполнены в текущей работе; более ранние сохраняют свои даты.

| Дата / профиль | Фактический результат | Evidence и границы |
| --- | --- | --- |
| 2026-10-31, ED6b.1 | Aether1635/Editor943 отдельно Debug/Release; fresh15packages/headless8/native4/4 | [Audit/4TRX](../../TestResults/ScenePlayback-ED6b1-20261031-1/verification.json), [acceptance/4PNG](../../TestResults/ScenePlayback-ED6b1-20261031-1/acceptance.json); Runtime copy открытой сцены, ED6b.2/full ED6b впереди |
| 2026-10-31, ED1b.2 | Aether1631/Editor923 отдельно Debug/Release; final15packages/headless8/native4/4 | [Audit/4TRX](../../TestResults/ProjectPicker-ED1b2-20261031-1/verification.json), [acceptance/14PNG](../../TestResults/ProjectPicker-ED1b2-20261031-1/acceptance.json); stopped/idle project workflow, ED6b открыт |
| 2026-10-31, ED1b.1 | Editor903/903 отдельно Debug/Release; focused20/20, failed/skipped0 | [Audit/3TRX](../../TestResults/ProjectTransitions-ED1b1-20261031-1/verification.json); только project/document API, UI/runtime/NuGet/native не повторены |
| 2026-10-31, ED4c.3 | Aether1629/Editor883/Alloy909/Hosting117 отдельно Debug/Release; final packages15/headless8/native2/2 | [Audit/8TRX](../../TestResults/EntityOperations-ED4c3-20261031-1/verification.json), [acceptance](../../TestResults/EntityOperations-ED4c3-20261031-1/acceptance.json); native RED→GREEN tree projection, 4PNG просмотрены |
| 2026-10-31, ED4c.2 | Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release; final packages15/headless8/native2/2 | [Audit/8TRX](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json), [acceptance](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/acceptance.json); initial findings и новый drain snapshot сохранены |
| 2026-10-31, ED8a.2 | Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release; packages15/headless8, published native2/2 | [Audit/8TRX](../../TestResults/RuntimeObservation-ED8a2-20261031-1/verification.json), [acceptance](../../TestResults/RuntimeObservation-ED8a2-20261031-1/acceptance.json); initial fixture failures сохранены, scope выше |
| 2026-10-30, ED8a.1 report | Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0; published Release/Cobalt normal/crashed CPU preview + native Skia raster/flood | [Audit/TRX](../../TestResults/DiagnosticReport-ED8a-20261030-1/verification.json), [published proof](../../TestResults/DiagnosticReport-ED8a-20261030-1/published-proof.json); child sink health/device, window/game/NuGet и hardware gates открыты |
| 2026-10-29, ED6a.2 raw/health | Core387/Aether1556/Editor823/Hosting117 отдельно Debug/Release; Hosting RED3→GREEN3; native SourceDebug/Cobalt1/1 | [Audit/8TRX](../../TestResults/ED6a2-20261029-1/audit.json), [native](../../TestResults/ED6a2-Native-20261029-6/verification.json); initial failures сохранены, packages/other native profiles не запускались |
| 2026-10-29, ED6a structured console | Editor808/808 Debug/Release;45новых tests, failed/skipped0. SourceDebug/standard/Cobalt native1/1,7children/6graceful validation0/0 | [Audit](../../TestResults/ED6a-20261029-1/audit.json), [native](../../TestResults/ED6a-Native-20261029-7/verification.json); raw/health/compiler/full export и другие native/package profiles открыты |
| 2026-10-29, ARCH2e.4 file-worker contract | Core374/Aether1550 Debug/Release; controlled starvation RED→GREEN, fresh headless8/native24 | [Current audit](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json); exact historical attribution отдельно |
| 2026-10-29, ARCH2e.4 после resource fix | Core373/Aether1550 Debug/Release; headless8/8, native24/24,48validation0/0 | [Audit](../../TestResults/ARCH2e4-Native-20261029-1/audit.json); file-sink root cause открыт, run1 failure сохранён |
| 2026-10-29, ARCH2e.3 native |24/24cases,56children/8owned crashes,48graceful validation0/0,40PNG, teardown0 | [Verification](../../TestResults/ARCH2e3-20261029-9/verification.json), [audit](../../TestResults/ARCH2e3-20261029-9/audit.json);4source/package build/publish profiles, standard/Cobalt и embedded/MS DI |
| 2026-10-29, Hosting/Editor Debug/Release |114/114 и763/763 отдельно в каждой конфигурации, failed/skipped0 | После Hosting lifecycle fix;4TRX в native audit; UI/Aether/full solution suites не повторены |
| 2026-10-29, fresh packages после Hosting fix |15packages,8/8headless cases/24children,624source inputs | [Verification](../../TestResults/ARCH2e3-Packages-20261029-3/verification.json); новый immutable feed/version, отдельно от native run |
| 2026-10-29, ARCH2e packages/headless | 15fresh packages;8/8combinations,24children, MaxLive1;618source inputs и22resource delivery checks | [Verification](../../TestResults/ARCH2e-20261029-7/verification.json); CPU frames/App/DI/Build/Replace/checkpoint/diagnostics и cleanup; без GUI/native loading |
| 2026-10-29, Aether Debug/Release | 1543/1543 в каждой конфигурации, failed/skipped0; targeted RED2→GREEN2 | [История runtime fix](HISTORY.md#arch2e-independent-packages-и-cpu-runtime-startup--2026-10-29); full module, не full solution/Editor/UI |
| 2026-10-28, Aether Debug/Release | 1543/1543 в каждой конфигурации, failed/skipped0 | [Forwarding fix](HISTORY.md#diagnostic-forwarding-bounded-outcomes--2026-10-28), focused44/44 Debug; full module включает headless processes, отдельная published/native matrix не повторена |
| 2026-10-28, Editor Debug/Release | 763/763 в каждой конфигурации, failed/skipped0 | [ARCH2d](HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28); Cobalt/MS DI, options, factory/View ownership и startup/close, без окна/GPU |
| 2026-10-28, ARCH2d source Debug/published Release | 18resource hashes,16CLI checks, independent runtime DLL/apphost4/4;11source inputs unchanged | [Verification](../../TestResults/ARCH2d-20261028-1/verification.json); fresh external deployment/unrelated cwd. PE/resource checks не доказывают native/font loading/rendering |
| 2026-10-28, solution nonincremental Debug build | 0errors/1541warnings | [Build log](../../TestResults/ARCH2d-20261028-1/solution-Debug.log); это build, полный solution test run не повторён |
| 2026-10-27, ENG3 source Debug/published Release | Headless21/21 +21/21 после fixture migration; позднее compiler/checkpoint/Replace/App-stop4/4 +4/4 | [Migration](HISTORY.md#eng3-buildreplace-fixture-migration--2026-10-27), [staging fix](HISTORY.md#eng3-compiler-staging-paths--2026-10-27); последние8cases не являются повтором всей21-scenario matrix |
| 2026-10-27, ENG2 CPU/session/Frames Debug/Release | 94/94 в каждой конфигурации, deterministic RED→GREEN | [Pending frame fix](HISTORY.md#eng2-сохранение-pending-cpu-frame-при-пустом-poll--2026-10-27); hardware/GPU acceptance не входит |
| 2026-10-27, Mithril Debug/Release и fresh published Release | 111/111 в каждой конфигурации; composition14/restart20 | [Отдельный owner и scope](../mithril/STATUS.md) |

ENG2, legacy Build/Replace, compiler MSB3030 и ошибочное ожидание forwarding
Accepted закрыты соответствующими regressions. Конкретный источник задержки
исходного forwarding run из старого TRX не установлен. Исходные failed runs и
compact/default TEMP profiles сохранены в HISTORY; passing другого модуля
не превращает прежний full solution failure в новый общий green.

## Открытая acceptance и ограничения

- ARCH2e.3 проверен только в описанном Windows x64/net10.0/OpenGL↔Vulkan profile.
  Native consumers не подтверждают другие OS/RID/AOT, каждый CLI branch Examples,
  physical OS input/DPI/monitors, driver/device loss и долгие resource trends.
  Локальный validation layer использован process-local; причина прежнего BSOD
  не установлена, массовые GPU/K7 stress runs не повторялись.
- Новый общий full solution Debug/Release test run после quality fixes не выполнен.
  Последний сводный ARCH2a run от27октября: Debug4296/4296, Release4295/4296,
  failed1 в Mithril ready-file; последующий Mithril fix имеет отдельный proof.
- Physical DPI/K3/K7/K8/VRAM/final alpha и long resource/performance trends открыты.
  K7 workaround search остановлен по решению пользователя; публикации NuGet не было.

## Границы приложения и engine

ED1a выбирает один проект через explicit .aetherproj; без него пустая workspace,
legacy .csproj сохраняет игровой demo workflow. Project picker/switch реализован в
stopped-game/idle-compiler profile; templates/wizard/recent/preferences ещё нужны.
ED2a/b .aetherscene v1/v2≤4MiB/
depth32; unknown versions отклоняются без migration, resolved/runtime/history не
сохраняются. Atomic store использует SHA256 guard и sidecar для cooperating writers;
arbitrary external rename, links sandbox и power-loss durability не гарантируются.

Document actions доступны при stopped/idle game/compiler или Edit preview; preview
останавливается до document replacement/close. Штатный Open document допускает
fresh Build/Play in-memory scene copy через ED6b.1/2a; inspector draft/conflict требует
Apply/Cancel. Document-aware Replace ещё не подключён. New создаёт пустую сцену.
Reparent, имена/порядок и schema-driven component edits реализованы в ED4c.1/2 и ED4d;
Create/delete/duplicate реализованы в ED4c.3; typed references, catalog management
и остальной ARCH1b открыты. Общая project/runtime
Switch/Close transaction выполнена в stopped-game/idle-compiler ED1b.2 profile;
переключение при играющем runtime остаётся отдельным ED1/ED6 расширением.
ED2 document Close/Switch/window Exit profile выполнен. Terminal shutdown forced
discard/drain; native user close vetoable.

Preview GPU-only Windows x64/OpenGL UI/Vulkan child, fixed camera,640×360,≤64entities,
новый child при обновлении revision. Authoring hierarchy/properties не обрезаются
этим лимитом; preflight rejection сохраняет документ. Build/Play/Game.Logic для
preview не нужны. Frames не идут через IPC; Enerit — control, Mithril — процессы.
Failed cleanup удерживает ownership для retry; arbitrary driver/dispatcher hang
не прерывается deadline. API experimental, публикации не было. Physical DPI/K3/K7/
K8/VRAM/final alpha открыты; NVIDIA workaround search остановлен по решению пользователя.

## Следующий конкретный шаг

Основной Aether шаг — measured source observer cost на большом local graph при
обычных10s polling/5s budget. Общая UI поставка Window требует independent fresh
NuGet consumer. Если выбран Editor Window consumer — выполнить
[ED4f.3](EDITOR_PLAN.md#ed4f3-window-root) с собственным standard/embedded/native/package
proof; сегодняшняя сверка лишь определяет этот маршрут. Header/casts/initial size/
configure compatibility и close-once — необходимые части, простая смена базового
класса EditorView не подходит. Hardware/release gates и наблюдение ARCH2e.4 ниже прежние.

ARCH2e.4 scoped contract проверен прежними controlled RED/GREEN. Новый run2026-10-31
снова наблюдал stdout/stderr drain timeout после reap в headless exe/MS DI generation3:
оба readers WaitingForActivation,24bytes сохранены,9workers/pending0, все3children
уже завершились. Два exact binary replays прошли без смены deadlines. Причина не
установлена и не объявляется исправленной; [новый snapshot/follow-up](HISTORY.md#ed4c2-имена-и-порядок-сущностей--2026-10-31).

ARCH2 package/native и file-worker profile проверены. Публичные границы
и ownership — PACKAGE_BOUNDARIES.md; supported consumer commands —
ManualTests/AetherPackageConsumer/README.md. Headless и UI зависимости разделены;
launch roles/Player/HUD ещё запланированы и не входят в закрытие ARCH2.

Compiler diagnostics/source hook ED6a.3 и report ED8a.1/2 проверены в scopes выше.
Extended native/provider/backend matrix, persistent compiler history и command/frame
metrics остаются отдельными. ED4c.2/3 имена/порядок и операции объектов реализованы
через общую history. ED1b.2 project picker/switch и runtime context rebinding реализованы
в stopped-game/idle-compiler profile. ED6b.1 запускает in-memory scene copy через
fresh Build/Start/Play и сохраняет parent state. ED6b.2a/b.1 добавляют content witness,
stale Start rejection и C# compilation из captured bytes. Последующие freeze/reuse/
document-aware Replace, bounded project/package graphs и source watcher выполнены
в своих profiles, указанных в датированных записях выше. Текущий measured-cost шаг
относится к observer; эти completed slices не назначаются заново. Полный input/HUD/
Player остаются отдельно. Детали source tracking —
[ED6b.2e](EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).
ENG2 CPU FrameReceiver и legacy Build/Replace follow-ups проверены новыми full
Aether runs и headless source/published matrix выше; исходные failures сохранены.
Mithril ready-file race закрыт отдельными module/published proofs. Diagnostic
forwarding test follow-up закрыт2026-10-28 codec/late-ACK regressions и full runs
выше; конкретный источник задержки initial Debug run из старого TRX неизвестен.
MSB3030 закрыт новым staging layout, inner binlogs и default-TEMP full runs выше;
произвольно длинные paths вне проверенного профиля не обещаются. Общая очередь
теперь ED6b document/build/play workflow; полный World/ECS не prerequisite.
Новый ED6b уточняет этот workflow; launch roles/Player — ENG7b. Runtime HUD
ENG5c.4 следует за gameplay collisions ENG5c.3, приёмка в Player зависит от ENG7b.2.
Full snapshot/runtime editing ARCH1c и смена launch role на ходу не prerequisites.

</details>

<a id="editor-default-fonts-2026-11-08"></a>
## Editor: системная default typography — 2026-11-08

По прямому запросу пользователя выполнен consumer срез
[UI PA8c.3b](../alloy/PLAN.md#pa8c3b-default-fonts); подробный task/команды/evidence
ведутся только в [UI HISTORY](../alloy/HISTORY.md#default-fonts-2026-11-08).
Host и public example используют installed Variable Text/Segoe UI с final Noto
fallback; explicit --font остаётся file-only. Header/hint/status/viewport
согласуют styled measure/draw, тема сохраняет family/size/weight/italic.
Стиль title использует600; models/data/lifetimes и runtime контракты не менялись.

В этом запуске full Editor baseline Debug1295/1295, затем1300/1300 D/R с5новыми
cases и усиленным header-theme case. Source Debug/published Release × dark/light:
4final native workspace runs, effective Variable Text600 и native measure equal,
UI/GPU/mappings/children0, reaped/disposed/project-closed, validation0/0.
Harness до смены шрифтов падал на старом cast Frame.Content→EditorView; исправлен
доступ через EditorMainWindow.FromRoot, исходные Noto screenshots сохранены.
Scoped builds, example publish и exact4package raster consumer прошли в UI scope;
это не новый fresh Editor App/GPU package либо full Aether/solution/Playing run.
Nom/importer/render snapshots и очередь engine остаются за своими владельцами.

<a id="status-before-core75-2026-11-10"></a>
## Полный STATUS перед CORE7.5 — 2026-11-10

# TrueMoon.Aether: состояние движка и редактора

**2026-11-09 — ARCH1f.3 snapshots и ED6b.2e.4f observer завершены в указанных scopes.**
Owned view: explicit camera, same-tick affine draws/slots/tint, bounded mailbox и
independent resource retains до actual submission cleanup; unknown outcome fences
view/device, failed cleanup сохраняет budget/leases. [Контракт](RENDER_SNAPSHOTS.md).
Scoped ECS/rendering/bootstrap1376/1376 каждый D/R,78new snapshot cases; source Debug/
published Release/fresh7package CPU consumer21checks/profile, final counters0.
Solution nonincremental Debug0errors/1556warnings. Full Aether green не заявлен:
3187/3191 в первом Release attempt; stale bootstrap negatives исправлены, два native
fixture restore scenarios остаются non-green. [Команды, failures, limits](HISTORY.md#render-snapshots-2026-11-09).

Observer: allocations/cycle −3.46/−3.51% на frozen many-files Polling/Notifications,
batch synthetic −34.4%; latency близка к baseline, CPU exploratory. Full Editor1468
каждый D/R. [Evidence/границы](OBSERVER_COST.md). ED5b/COB5a/SplitView сохранены.
Новых actual mesh surface pixels/Playing/device-loss proofs нет: это f.4.

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
| 2026-11-09 | ARCH1f.3:1376scoped D/R,78new; source/published/fresh7pkg CPU21checks/profile/owners0. Observer:Editor1468 D/R и frozen allocation gains. Full Aether non-green отделён | [Snapshots/observer](HISTORY.md#render-snapshots-2026-11-09) |
| 2026-11-09 | ED5b: full Editor1465/1465 каждый D/R,53newcases, failed/skipped0. Source/published/fresh15package native4profiles: по5PNG/4reaped children, owners0/validation0/0; solution Debug0errors/1556warnings | [Translation](HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-08 | ED5a.4: Editor1412/Aether2104 D/R; source/published/fresh-package GPU и supported Windows SendInput. Это предшествующая отдельная проверка | [Camera/selection](HISTORY.md#ed5a4-acceptance-2026-11-08) |
| 2026-11-07 | ARCH1f.2: targeted ECS/Rendering1230 D/R, resource CPU/Vulkan source/published/fresh7package, owners0/validation0/0. Это предшествующая проверка, не текущий full Aether | [Resources](HISTORY.md#render-resources-2026-11-07) |

Типографика/UIA3d, importer research и прежние полные записи сохранены в
[снимке предыдущего STATUS](HISTORY.md#status-before-ed5b-2026-11-09).
Исторические результаты не считаются повторёнными ED5b.

## Открытая acceptance и ограничения

- Full Aether09ноября Release3187/3191:2stale bootstrap cases исправлены и проверены
  scoped;2native fixtures по-прежнему падают на legacy dotnet-xunit/SDK offline restore.
  [Точная область](HISTORY.md#render-snapshots-2026-11-09). Общая suite не green.
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
fixtures от repo legacy CLI-tool restore и получить current full baseline; прежний
post-reap drain timeout требует собственного причинного evidence. Physical/release отдельно.

<a id="status-before-core76-2026-11-10"></a>
## Полный STATUS перед CORE7.6 — 2026-11-10

# TrueMoon.Aether: состояние движка и редактора

**2026-11-10 — CORE7.5 подключил общую конфигурацию Aether/Editor.**
Engine/ECS/render/authoring и Editor/source/diagnostics/window schemas используют
common profile/deferred validation; protected runtime bootstrap и public flags
фиксируются до App, CLI использует общий parser. Identity/resources/captures и
существующие owners сохранены. [Guide](../core/CONFIGURATION_MODULES.md).

Текущий configuration scope:42new Aether cases +5old replays =47/47 D/R;
full Editor1496/1496 D/R. Source/published/fresh16packages по76checks на обоих DI,
без window/GPU/child runtime. Полные Aether attempts non-green: latest extra
Debug3232/3234, Release3233/3234; SDK lock/diagnostics timeout, прежние restore
attempts и serial replays явно разделены в [HISTORY](../core/HISTORY.md#core75-2026-11-10).

ARCH1f.3 snapshots, ED5b и observer4f сохранены; их прежние proofs не повторены
этим headless consumer. Engine f.4 и physical/release gates открыты.
[Предыдущий STATUS целиком](HISTORY.md#status-before-core75-2026-11-10).

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
| 2026-11-10 | CORE7.5: Editor1496 D/R, Aether47scoped serial D/R,110newcases total; source/published/fresh16packages76checks/profile. Full Aether non-green указан отдельно | [Configuration](../core/HISTORY.md#core75-2026-11-10) |
| 2026-11-09 | ARCH1f.3:1376scoped D/R,78new; source/published/fresh7pkg CPU21checks/profile/owners0. Observer:Editor1468 D/R и frozen allocation gains. Full Aether non-green отделён | [Snapshots/observer](HISTORY.md#render-snapshots-2026-11-09) |
| 2026-11-09 | ED5b: full Editor1465/1465 каждый D/R,53newcases, failed/skipped0. Source/published/fresh15package native4profiles: по5PNG/4reaped children, owners0/validation0/0; solution Debug0errors/1556warnings | [Translation](HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-08 | ED5a.4: Editor1412/Aether2104 D/R; source/published/fresh-package GPU и supported Windows SendInput. Это предшествующая отдельная проверка | [Camera/selection](HISTORY.md#ed5a4-acceptance-2026-11-08) |
| 2026-11-07 | ARCH1f.2: targeted ECS/Rendering1230 D/R, resource CPU/Vulkan source/published/fresh7package, owners0/validation0/0. Это предшествующая проверка, не текущий full Aether | [Resources](HISTORY.md#render-resources-2026-11-07) |

Типографика/UIA3d, importer research и прежние полные записи сохранены в
[снимке предыдущего STATUS](HISTORY.md#status-before-ed5b-2026-11-09).
Исторические результаты не считаются повторёнными текущей configuration задачей.

## Открытая acceptance и ограничения

- Full Aether10ноября остаётся non-green: extra Debug3232/3234, Release3233/3234,
  SDK exclusive-lock/diagnostics timeout. Serial47/47 D/R прошёл, включая2native
  compiled-game fixtures с ограниченным test props override; ordinary nested
  legacy tool restore не исправлен. [Все attempts](../core/HISTORY.md#core75-2026-11-10).
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
fixtures от repo legacy CLI-tool restore и получить current full baseline; прежний
post-reap drain timeout требует собственного причинного evidence. Physical/release отдельно.
