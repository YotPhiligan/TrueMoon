# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-22, UIA1 в Aether.Editor**:
Fluent EditorView<EditorViewModel>, отдельный IViewStyle<EditorView>, observable
UI-independent model и DI factory реализованы. Theme/selection/commands сохраняют
поведение; model/style заимствуются View, App owns model; failed constructor
освобождает unpublished tree. Общий контракт принадлежит
[UIA1](../alloy/PLAN.md#uia1-view-dataviewmodel-и-scoped-styles--2026-10-22),
[evidence](../alloy/HISTORY.md#uia1-view-viewmodel-и-typed-styles--2026-10-22).
Текущий полный Debug/Release2321/2321, Aether800/Editor49, failed0/skipped0.
Nonincremental Debug build0errors, Release publish/native оба exit0: каждый
8commands/3PNG/2reaped children, Vulkan0errors/0warnings и zero owned resources.
Fresh14packages/4consumers повторили UI styles и bounded engine ownership.
Engine protocol/lifecycle production код этим шагом не менялся. Inspector editing,
World/edit/MCP, ENG5c.3 collisions/HUD и performance scope остаются прежними.

Предыдущая работа — **2026-10-22, единые фабрики и DI**.
DI1–DI4 реализованы в Aether/Aether.Editor и затронутых dependency paths:
injected child factories/explicit AetherHost dependencies, App-registered Editor
graph, UseAetherRuntime/AetherRuntimeHost, typed runtime/viewport/pipe/receiver/game
factories и SceneResolutionService. Общие factory/lifecycle contracts остаются
provider-neutral. Borrowed aliases не создают дополнительных disposable owners.
[Архитектура/миграция](PLAN.md#приведение-к-правилу-cobaltifactory),
[команды и область проверок](HISTORY.md#единые-фабрики-и-di--2026-10-22).

Полный solution **2293/2293 Debug и2293/2293 Release**, по10TRX, failed0/skipped0.
Core117/Cobalt179/generator91/Enerit126/generator114/Mithril99/UI643/Hosting85/
Aether800/Aether.Editor39. Добавлены40cases: typed/legacy factories, ownership,
aggregate rollback, game entry DI/errors/pins, runtime registration, handles и UI
bridge. Microsoft DI compatibility повторена вместе с default Cobalt.
Release publish прошёл. Итоговые Debug/published Release native runs: каждый
8commands/3PNG/2reaped children, validation0/0 и zero owned resources. Fresh14packages/
20commands/4Cobalt+DI consumer runs прошли; payload hashes и isolated restore
проверены.257sources/235namespace declarations и413local links без замечаний.
Итоговый native/package evidence — в HISTORY.

Игровой entry теперь реализует IGameSessionEntry static Create(IServiceResolver?),
передавая runtime dependencies в constructor. Activator fallback отсутствует.
External game projects добавляют non-copying TrueMoonContractsPath reference;
HostId/ContractHash включают Aether и Contracts. Host/runtime/game и старые
artifacts необходимо пересобрать вместе. Wire versions прежние.
Inspector пока показывает authoring SceneSnapshot и read-only во всех режимах;
список плоский, render surface960x540, camera/gameplay input в новом приложении
ещё не подключены. Это ограниченный ENG7a, не полный toolset/World edit/MCP.
Предыдущие подробные сводки сохранены в HISTORY; их датированные результаты
не считаются текущими проверками нового приложения.

## Предыдущие проверки ENG4d2 — 2026-10-20

Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,
9TRX/failed0/skipped0. Nonincremental Debug0errors/1499warnings. Final native
54cases/56commands/exit0,406source/76binary fingerprints; Debug apphost и
published Release. Fresh12packages/4Cobalt+DI consumers/18commands проверили
neutral ticket/first-present/strategy ownership, новые APIs и exact managed/native
payload hashes. Evidence запуска2026-10-20 — complete3, package2 и eng4d2-complete-20261020.

Каждый ordinary GPU Editor proof:20A/B DLL Replace,20failure cases,53reaped children,
один App/window и MaxLive1. Проверяются loaded manifest/DLL hashes, complete state,
first presented tick после restore, selected next-Step behavior, прежний Paused/
Playing mode, compile/cancel/stale/Stop, single rollback/double failure, cleanup retry
и native close с pending Replace/compiler. Дополнительно GPU resize64×32/suspend/
resume160×90 и stale ticket rejection. Все53cooperative teardown reports:
47devices/141slots created/released, core/sync validation0errors/0warnings.
Imported ReadPixels0; GPU resources/NT handles/mappings/UI sessions0, authoring
сохранён, Game.Logic в Editor не загружен. Прежние ENG1–ENG4d regressions повторены.

Managed cases проверяют latest/foreign/closed tickets, общий CPU receiver ticket,
connection drain до process Stop, pending first-present cancellation, worker failure,
original+cleanup causes/retry, exactly-once strategy ownership, runtime loaded/
restored capture и RGBA frame metadata. Background failed cleanup доступен через
Frames.TransportError; успешный Stop retry не воспроизводит faulted watcher task.

## Границы

- Explicit --viewport-gpu/--viewport-gpu-project использует обычный Editor View и
  команды; --viewport/--viewport-project сохраняют CPU. Автоматического fallback
  при GPU fault нет; transport выбирается на App configuration.
- Runtime frameProvider получает atomic loaded/restored game's tick/color или
  optional primitive scene. Reference pattern остаётся CPU-generated/staging;
  ENG5a scene рисуется Vulkan без CPU pixel buffer. UI retained GPU copy остаётся.
  Pixels не передаются через pipe и не читаются из imports для delivery.
- Reference Windows x64/RTX5070Ti/OpenGL Editor/Vulkan child. Ordinary GPU proof
  extents64×32/160×90; larger1280×720 и premul/stored-sRGB150%-DPI/clipping проверены
  отдельными GPU probes. Retained UI pixels после actual FramePresented — не OS
  compositor/physical-display color proof. Bounds1920×1080 не равны новому native proof.
- UI consumer owns copied images на исходном surface/context thread до UI teardown;
  dispatcher/context borrowed до GPU cleanup. Failed cleanup retains connection/
  imports и блокирует новый Start до explicit retry. Arbitrary dispatcher/driver
  hang не прерывается managed deadline; device loss не инжектировался.
- API experimental/packable. Package consumers проверяют managed transport API,
  App ownership и native delivery; actual GPU drawing выполнен source apphost/
  published Release, не package consumer. Current full solution/ENG4e measurements
  указаны выше; own Aether package/native drawing, golden/RTE/full performance/
  VRAM/hardware/final UI matrix/publication этим не подтверждены. K3/K7/K8/VRAM/
  final alpha прежние; driver/Windows settings/K7 workaround policy не менялись.

## Следующий конкретный шаг

DI1–DI4 реализованы и проверены в указанном scope. Следующая функциональная
работа — ENG7 inspector editing или ENG5c.3 collisions по выбранному направлению.
Массовая миграция остальных независимых consumers фреймворка не назначена.

ENG7a основной экран реализован в ограниченном scope. Следующий локальный UI
этап — authoring inspector editing через согласованные SceneDocument/Entity API,
с валидацией/undo и сохранением; во время Play сохранить read-only до ARCH1c/d
snapshot edit transaction. Затем picking/tree/input и project persistence по ENG7.

При возврате к gameplay продолжить
[ENG5c.3 collisions камеры](PLAN.md#eng5c3-следующий-шаг-collisions-камеры), затем HUD.
ARCH1a.1/2 и ENG5c.1/2 выполнены; full runtime World/edit/MCP, raw input,
3D performance и CPU Debug/reference allocation backlog сохраняются.
PA8a реализован в автоматическом scope; physical UI acceptance/K3/K7/K8/VRAM/final
alpha этим экраном не закрыты, поиск NVIDIA workaround остаётся остановленным.
