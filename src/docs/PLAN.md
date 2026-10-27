# TrueMoon: общий план развития

Создан 2026-10-09 по запросу разделить общий roadmap и планы модулей. [Корневой README](../../README.md), [индекс документации](README.md), [текущее состояние](STATUS.md), [формат ведения планов](PLANNING.md). Это план всего фреймворка; детальные задачи и проверки находятся в документах соответствующих модулей.

## Цель

Развивать TrueMoon как модульный C# фреймворк для запуска приложений: конфигурация и lifecycle, DI, управление units/процессами, межпроцессные вызовы и необязательная UI подсистема. Пользователь должен подключать нужные функции через понятные контракты, сохранять контроль над ресурсами и получать наблюдаемые ошибки.

Проверить три сквозных сценария:

1. Приложение без UI: конфигурация, сервисы, startup, cancellation, shutdown и diagnostics.
2. Приложение с units: запуск собственного дочернего процесса, IPC вызов, завершение или отказ и согласованный restart/shutdown.
3. UI приложение и внешний HUD: Alloy/Argentis использует общие lifecycle/DI contracts; renderer/device/window ownership остаётся в UI интеграциях и host.

UI alpha и контрольное beta приложение имеют собственные критерии. Готовность UI не означает готовность всех модулей TrueMoon; обратное тоже не следует из общего build.

Текущий приоритет2026-10-27 —
[ARCH2: Aether/Editor packages и executable hosts](engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27).
ARCH2a–c реализованы; ARCH2d/e предшествуют новым Editor features. Текущие проверки и открытые quality failures — в STATUS.

## Карта модулей

| Направление | Проекты и ответственность | План |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core: bootstrap, configuration, diagnostics, lifecycle, общие сервисные контракты и utilities | [Core](core/PLAN.md) |
| Cobalt / DI | TrueMoon.Cobalt, Cobalt.Generator, Extensions.DependencyInjection: resolver, регистрация, lifetime, generation и альтернативная DI интеграция | [Cobalt](cobalt/PLAN.md) |
| Enerit | TrueMoon.Enerit, Enerit.Generator: invocation client/server, IPC transports, serialization и generated service adapters | [Enerit](enerit/PLAN.md) |
| Mithril | TrueMoon.Mithril: units, hosting/startup/lifetime/restart policies, процессные handles | [Mithril](mithril/PLAN.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: retained UI, renderer, окно/input, standalone и external HUD | [UI](alloy/PLAN.md) |
| Aether / Engine | TrueMoon.Aether: library/package общего engine/components/authoring/runtime, sessions/control/frames/builds/checkpoint/Replace; отдельные executable/game consumers | [Aether](engine/PLAN.md), [ARCH2](engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) |
| Aether Editor | TrueMoon.Aether.Editor — подключаемая library/package с View/Data/styles, commands и прикладными services; App integration и explicit runtime реализованы в ARCH2b/c, hosts/assets/independent package proof открыты | [Подключаемый Editor ED1–ED8](engine/EDITOR_PLAN.md), [package/host переход ARCH2](engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) |

Тестовые проекты, generators и ManualTests относятся к своим направлениям. Они не образуют отдельный продуктовый roadmap. Общие CI/build/package задачи остаются на уровне этого документа.

## Архитектура и зависимости

Решение2026-10-22: [план приложения Aether.Editor](engine/EDITOR_PLAN.md) задаёт
единый прикладной маршрут ED1–ED8 и первый Transform/preview/Undo/save/reopen срез.
Уточнение2026-10-23: [минимальный ARCH1b и headless proof](engine/PLAN.md#основа-api-перед-редактируемым-ui--2026-10-23)
предшествовали Edit preview и редактируемому inspector/tools; ED1a project/session
выполнен в своём scope. Полный World/ECS/runtime edit не нужен для первого среза.
[Enerit выбран для IPC Editor↔Runtime](engine/PLAN.md#ipc-editor-и-runtime-enerit--2026-10-23),
frame payload остаётся в отдельном shared RAM/GPU transport, процессы — у Mithril.
Общие overlays/forms/tree/commands — UI dependencies. Минимальные authoring API
и независимый engine preview реализованы2026-10-23; ED1a project/session и
ED2a/b document persistence и UI commands/close policy также готовы. Далее
ED3a подключил Transform inspector/Undo/Redo и independent preview к обычному UI.
Новых assembly dependencies нет.

Уточнение приоритета по запросу пользователя2026-10-24: после первого
edit/save/preview цикла развиваем [обозреватель ED4c](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Общий TreeView — в Argentis, смена родителя — минимальное расширение общего
authoring API/history. Следующее уточнение пользователя2026-10-24 —
[динамический Inspector ED4d](engine/EDITOR_PLAN.md#ed4d-динамический-инспектор-компонентов--начат-2026-10-24):
schema-driven поля и компоненты через общий authoring API, реализованы2026-10-24
с проверками history/save/preview. Затем сохраняемые имена и порядок соседей, create/delete
общего API с UI, чтобы New был пригоден для работы; после — project picker/switch
и ED1/ED6 workflow. Детали и критерии ведутся в прикладном плане редактора.
UI развивается по этим consumers. Core/Cobalt поддерживаются адресно; ENE6c/d и
полный MIT6a/c — следующие задачи надёжности до рабочей поставки, hardware UI
acceptance остаётся отдельной. Ранние датированные маршруты ниже — история;
текущая точка продолжения находится в STATUS и модульных STATUS.

UI приоритет2026-10-23 выполнен в bounded scope —
[компактный Editor header ED4a](engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23)
на общих menus/overlays/transitions UI; теперь возвращаемся к create/delete.
Детали дизайна и tasks ведутся у владельцев, прежние проверки не считаются новым proof.

UI authoring2026-10-22: в AGENTS закреплены Fluent View<TData>, отдельные optional
Data/ViewModel и typed IViewStyle<TView>. Реализация/критерии находятся в
[UIA1](alloy/PLAN.md#uia1-view-dataviewmodel-и-scoped-styles--2026-10-22), первый
production consumer — Aether.Editor. DI/owner-thread factory route сохранён;
новых межмодульных dependencies это изменение не добавляет.

### Aether и Editor как подключаемые пакеты — 2026-10-27

По решению пользователя TrueMoon.Aether содержит основной функционал движка,
компоненты и runtime; TrueMoon.Aether.Editor становится library/NuGet package
с UI/View, Data/ViewModel/styles и прикладными services редактора. Пользовательский
executable либо тонкий пример владеет Main/CLI/App composition и выбором host.
Editor зависит от Aether; runtime/game consumer использует Aether без Editor.
Общие engine APIs доступны headless; пользовательская игра остаётся отдельной.

Подробный переход и acceptance — только в
[ARCH2a–e](engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27): общие API/
ownership, Editor library/App integration, explicit runtime executable/bootstrap/
identity, тонкие hosts/resources, independent NuGet consumption.
ARCH2a вынес shared scene persistence в Aether и определил карту owners;
ARCH2b превратил Editor в library/packable module с App/View/window integration,
entry points/smoke consumer — AetherEditorHost. ARCH2c добавил explicit runtime
command/bootstrap и точную identity пары host/runtime; отдельный AetherRuntimeHost
работает без Editor/UI. Source Debug/published Release headless DLL/apphost
проверены отдельно от GPU/package acceptance; Release CPU race и legacy probe
fixtures failures сохраняются. Следующие шаги — ARCH2d/e;
отдельные результаты и ограничения проверок находятся в STATUS. Весь ARCH2 предшествует
console/export и новым authoring UI features. После него —
ED6a/ED8a, ED4c.2/3 и project/build/play workflow; оставшиеся Core/DI/IPC/UI roadmaps
не становятся обязательными prerequisites и не считаются завершёнными.

### Единая структурированная диагностика — 2026-10-25

По решению пользователя развивать собственный TrueMoon API: структурированные
события и уровни, ограниченный асинхронный pipeline/retention, operation traces,
metrics, общую историю Editor/Runtime и диагностический отчёт.
Microsoft.Extensions.Logging и его адаптер пока не требуются. Первый bounded
Core/Aether профиль реализован и проверен2026-10-25; полный roadmap остаётся частичным.
26октября добавлен [ENG8a.2 retention завершённых build jobs](engine/PLAN.md#retention-диагностики-сборок-eng8a2--2026-10-26);
Следующим срезом того же26октября добавлены [Core emergency и managed JSONL runs](core/PLAN.md#emergency-и-managed-jsonl-runs--2026-10-26):
конечный fallback и count/bytes/age закрытых logger runs.
Следующий профиль26октября добавляет [операции CORE6d](core/PLAN.md#операции-core6d--2026-10-26)
и [Aether ENG8a.1/3](engine/PLAN.md#операции-eng8a13--2026-10-26): sampled traces,
bounded operation metrics, Replace/recovery, preview и frame-fault events.
Следующий срез26октября добавляет [ENE6d.1 IPC trace context](enerit/PLAN.md#ene6d1-bounded-trace-header-и-ene6c-compatibility--2026-10-26):
bounded optional header, Client/Server correlation и штатный Aether consumer.
Следующий [ENE6d.2 resource meter](enerit/PLAN.md#ene6d2-агрегированные-метрики-ресурсов--2026-10-26)
показывает process aggregate queue/pending/server requests/bytes/connections и
фиксированные overload counters; measurement callbacks выполняет observer.
Профиль27октября [ENE6d.3](enerit/PLAN.md#ene6d3-исходы-ipc-и-ожидание-admission--2026-10-27)
добавляет fixed request/connection failure classes, reconnect/late counters и
pull-only queue wait count/sum. [Child delivery ENG8a.3/ENE6d.4](engine/PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27)
использует separate bounded endpoint и Core history/import views, без replay и
shared control queue. Далее bounded Editor console/export, затем domain/frame aggregates;
полный diagnostics roadmap остаётся частичным.

Детали у владельцев: [Core CORE6](core/PLAN.md#core6-структурированная-диагностика--2026-10-25)
— schema/API/pipeline/sinks/lifecycle и typed generation;
[Enerit ENE6d](enerit/PLAN.md#ene6d-корреляция-и-диагностика--2026-10-25) — IPC context;
[Mithril MIT6c](mithril/PLAN.md#mit6c-диагностика-и-захват-process-output--уточнение-2026-10-25)
— process acceptance; [Aether ENG8a](engine/PLAN.md#eng8a-диагностика-сессий-и-процессного-вывода--2026-10-25)
— engine events и stdout/stderr capture;
[Editor ED6a/ED8a](engine/EDITOR_PLAN.md#ed6a-консоль-диагностики--2026-10-25) — консоль/экспорт.
Первый diagnostics срез: CORE6a–c + ENG8a.1/2, headless overload/failure/stop proof,
затем расширение traces/metrics и UI. Текущая очередь authoring/editor работ и
прежние quality/hardware gates сохраняются; полный generator не prerequisite.

### Сверка основы перед Aether — 2026-10-23

Ревью планов и исходников не выявило нового обязательного полного прохода Core/
Cobalt перед локальным authoring API. Их bounded quality/lifecycle/factory scope
уже выполнен; allocation backlog, новые DI scopes и общий scheduler redesign не
становятся prerequisites ARCH1b. Исторические passing результаты не повторялись.

| Когда | Предлагаемая зависимость | Владелец деталей |
| --- | --- | --- |
| Локальный authoring/editor | ARCH1b.1/preview/ED1a/ED2a/b и ED3a реализованы; далее create/delete через общий authoring API/history. Текущих App/Cobalt/IFactory contracts достаточно | [Editor](engine/EDITOR_PLAN.md#ed3-общие-операции-редактирования-и-undoredo) |
| Интеграция preview/input RPC | Использовать первый проверенный finite admission/async stop/owned kill/reap профиль; оставшийся fault matrix проверять адресно по consumer | [ENE6a/b](enerit/PLAN.md#ene6-интерактивный-consumer-aether--ревью-2026-10-23), [MIT6a](mithril/PLAN.md#mit6-укрепление-процессов-и-ipc--ревью-2026-10-23) |
| При расширении IPC contracts | Fail-fast совместимость peers, correlation/overload/latency diagnostics; same-build exact-match достаточно первого профиля | ENE6c/d |
| Перед рабочей поставкой | Длительные resource trends, abrupt parent death с uncooperative child, bounded log retention и фактические platform/package scenarios | MIT6b/c и ED8 |
| По измерениям/потребителю | CPU/allocation profile Cobalt/Enerit, дополнительные restart policies, MMF и другие OS/transports | Собственные планы модулей |

ENE6/MIT6 — предложения этого ревью, ещё не реализация или утверждённые публичные
API. Сначала ограниченный contract и воспроизводимый fault/load scenario; нельзя
объявлять готовыми непроверенные bounds. Частый preview — отдельная интеграционная
граница, а не повод останавливать всю headless работу над движком. Readiness/
heartbeat/session recovery уже есть в Aether; Mithril их не дублирует, Enerit не
получает scene mutations или UI зависимости. Frames сохраняют отдельный transport.

Следующим запросом2026-10-23 пользователь поручил приступить к работе: выбран
[первый ENE6a/MIT6a контракт](enerit/PLAN.md#ene6a-и-mit6a-первый-контракт--2026-10-23)
с finite admission, async stop/Completion и own-child saturated-stop/kill proof.
Детали реализации/проверок — в локальных STATUS; остальные ENE6/MIT6 предложения
не становятся автоматически завершёнными или обязательными API.

Последующие шаги2026-10-23: первый локальный
[ARCH1b.1 Transform/query/history API](engine/PLAN.md#arch1b1-локальная-transform-authoring-session--2026-10-23)
и [независимый engine Edit preview](engine/PLAN.md#edit-preview-первый-engine-profile--2026-10-23)
реализованы/проверены через App/Cobalt/IFactory и Microsoft DI. Preview использует
Enerit/Mithril и GPU transport без Build/Play/Game.Logic; normal Editor UI ещё
не подключён. Новых межмодульных dependencies/assembly не добавлено.
ED1a project/session и ED2a/b documents/save/reopen/UI commands/Save-Discard-Cancel
реализованы следующими шагами; далее Transform/Undo/preview integration.
Полный ARCH1b/ED1–ED8 этими engine API срезами не закрыты.

Фактические ProjectReference, сверенные2026-10-09:

- Contracts и Core не имеют ProjectReference на другие модули. Общие app/service contracts находятся в Contracts, utilities — в Core.
- TrueMoon использует Contracts, Cobalt и Cobalt.Generator. Cobalt использует Contracts и собственный generator; Extensions.DependencyInjection использует Contracts.
- Enerit использует Contracts, Core и свой generator. Mithril использует Contracts/Core; прямой ProjectReference Mithril→Enerit отсутствует. Их взаимодействие проверяется как composition приложения.
- Argentis не имеет ProjectReference; Alloy использует Argentis и (с2026-10-22) provider-neutral Contracts для общего IFactory UI sessions. Rendering.Skia и Platform.Silk используют Alloy. Alloy.Hosting связывает эти интеграции и общие Contracts/Core.

Уточнение2026-10-19 по запросу пользователя: engine implementation/contracts
вынесены в одну assembly **TrueMoon.Aether**, с dependencies на Contracts/Enerit/
Mithril и Enerit generator; UI/Game.Logic references отсутствуют. Прежний
Engine.Probe.Contracts project объединён с модулем, tests перенесены в
TrueMoon.Aether.Tests. [Границы модуля](engine/PLAN.md#truemoonaether-одна-engine-assembly--2026-10-19).

ENG4b2026-10-20 добавляет Aether→Silk.NET.Vulkan2.23.0 для явного Windows x64
shared-image probe; App startup остаётся lazy. GPU resources принадлежат
AetherHost.Gpu, borrowed UI/GL owner живёт до их cleanup. UI/Skia/Game.Logic
dependencies в engine assembly не добавлены.

Сохранять отсутствие зависимости общего bootstrap/DI/IPC/units от UI и графических пакетов. Контракты интеграции не должны требовать конкретного renderer, DI provider или IPC transport без явного выбора. Генератор относится к своему модулю; доставка analyzer в package проверяется отдельно от работы через ProjectReference.

Изменение этой карты фиксировать здесь и в локальном плане, не выполняя перестройку зависимостей ради нового формата документации. Вопросы будущего API и версионирования протоколов решаются в соответствующих этапах.

### Подключение функциональных модулей через App — 2026-10-19

Уточнение2026-10-22: унификация Aether/Editor и зависимостей реализована в
[DI1–DI4](engine/PLAN.md#приведение-к-правилу-cobaltifactory). Contracts содержит
IFactory<T,TArguments>/Factory adapters и non-owning AppServiceLifecycle; App
использует default Cobalt. Enerit/Mithril/Alloy сохраняют независимость от DI provider.
Полная миграция остальных consumers не назначена.

По запросу пользователя закреплено в [правилах репозитория](../AGENTS.md):
модули с сервисами/configuration/background work/owned resources предоставляют
публичное расширение IAppConfigurationContext и штатно подключаются через
App.Builder.Setup/App.Build/App.RunAsync. Extension валидирует/snapshots options,
регистрирует App-local services и определяет start/stop/drain/dispose ownership,
включая startup/cleanup failures и duplicate registration. Disposable aliases
не должны создавать несколько владельцев ресурса. Один ManualTests example
не заменяет такую module entry point; low-level utilities/contracts сохраняют
независимое использование и не требуют фиктивного UseX.

App contracts находятся в TrueMoon.Contracts; concrete bootstrap/UI/provider
добавлять в dependencies только по необходимости. Aether реализует этот путь
через [UseAether/AetherHost](../TrueMoon.Aether/README.md#подключение-к-app),
без новых общих Core/DI/lifecycle APIs. Явный запуск/остановка игровых сессий
отделён от App startup и terminal App shutdown.

Аудит остальных модулей2026-10-19 подтвердил существующие Enerit/Mithril/Hosting
entry points, встроенный Cobalt и выбор DI через App.Builder. Отдельные utilities,
UI слои и generators не требуют фиктивного UseX; UI подключается через Hosting.
[Таблица API, focused checks и границы](HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).
Пропущенных functional module entry points не найдено, очередность не меняется.

### Размещение типов внутри проекта — 2026-10-21

По запросу пользователя в [правилах репозитория](../AGENTS.md#пространства-имён-и-структура-файлов)
закреплена группировка по предметному или функциональному назначению. Типы без
такой категории размещаются в корне проекта; вид типа сам по себе не требует
папок Contracts/Models/DTOs/Interfaces. Namespace следует фактическому пути.
Решение касается структуры внутри проектов. TrueMoon.Contracts — общий пакет
контрактов, базовых моделей и интерфейсов для взаимодействия модулей, включая
App; module-specific типы остаются у своего владельца. Общая сборка не получает
dependencies на реализации модулей или providers.

Исходный аудит2026-10-21 выявил42namespace замечания и12файлов в5технических
папках. По следующему запросу пользователя все замечания исправлены:44проекта,
597C#files, namespace/category violations0. [Миграция public API и проверки](HISTORY.md#исправление-структуры-проектов--2026-10-21).
Исходный реестр сохранён в HISTORY как датированный снимок, а не текущий backlog.
Одна engine assembly, shared contracts и функциональный маршрут ENG4e сохранены.

### DI и фабрики систем и сервисов — 2026-10-22

По решению пользователя в [правилах ИИ-агентов](../AGENTS.md#di-и-создание-систем-и-сервисов)
закреплены Cobalt как штатный контейнер, constructor injection и внедрённые
IFactory для динамического создания систем/сервисных объектов. Прямой Resolve
сосредоточен в composition root и инфраструктуре фабрик/DI; ownership результата
фабрики определяется явно. Общие contracts остаются независимыми от provider.
[Подробные границы и применение](cobalt/PLAN.md#организация-систем-и-сервисов-через-di-и-ifactory--2026-10-22)
ведутся в Cobalt плане. Это документационное решение; аудит и массовая миграция
consumers не выполнены, порядок функциональных работ и готовность прежние.

Следующий запрос пользователя2026-10-22 переименовал редактор в
TrueMoon.Aether.Editor/Tests и назначил аудит Aether по этому правилу. Аудит
показал частичное соответствие; ручной service graph и game activation требуют
[DI1–DI4 в engine плане](engine/PLAN.md#переименование-редактора-и-аудит-di--2026-10-22).
Переименование проверено managed/native runs; на момент аудита DI требовал fixes.
Последующим шагом DI1–DI4 реализованы и проверены2026-10-22; текущая сводка и
ограничения — в [engine STATUS](engine/STATUS.md), прежний аудит сохранён в HISTORY.

## Порядок развития

Актуальная точка2026-10-21: ENG1–ENG3/ENG4a/b/c реализованы в TrueMoon.Aether;
ENG4d borrowed texture/retained GPU copy bridge проверен в actual reference viewport.
Lifetime, premul/stored sRGB/origin, scale/clipping и presentation проверены отдельно
от imported readback. [Контракт](engine/PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20).
ENG4d2 подключил ordinary GameEditor GPU transport/selected DLL/Build/Replace/
checkpoint/first-present pipeline, сохранив CPU default. ENG4e measured CPU/GPU
baseline получен2026-10-21:12cases/36intervals; найден/исправлен Mithril graceful
delivery race. [Метрики/ограничения](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
ENG5a реализует первый depth-tested Vulkan3D primitive/camera/material в текущем
App-owned GPU viewport; [контракт и проверки](engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
ENG5b input/camera routing выполнен; [контракт/evidence](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
ARCH1a.1/2 immutable model, scene aggregate/resolver и projection реализованы2026-10-22;
ENG5c.1 scene consumer/комната и ENG5c.2 reach/raycast/interaction реализованы;
по запросу пользователя2026-10-22 после UI стиля реализован
[ENG7a основной экран TrueMoon.Aether.Editor](engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22).
Это отдельное UI application, не новая engine assembly; authoring grid editing,
tree/input/persistence продолжаются отдельно, property grid пока read-only.
Gameplay продолжение — [ENG5c.3: collisions камеры](engine/PLAN.md#eng5c3-следующий-шаг-collisions-камеры), затем HUD;
CPU Debug budget breaches/reference staging allocations остаются measured backlog.
ENG4 в целом и UI hardware/release readiness открыты.

2026-10-22 согласованы [ARCH1: модель мира и API Aether](engine/PLAN.md#arch1-entity-components-и-api--2026-10-22):
Entity/Components с наследованием, authoring/runtime API, readonly Play с явной
snapshot edit transaction и локальные агенты через MCP adapter. Уточнён
двухпроцессный Editor/Runtime: Aether координирует запуск через Mithril, команды/
input/state передаются через Enerit IPC, frames — отдельным transport. ARCH1a.1/2
выполнены, ENG5c.1 подключил static runtime/renderer scene consumer; toolset развивается
в ENG7. Детали/acceptance ведутся только в engine плане. Dependency graph прежний;
ARCH1a.1/2 проверены managed suites, остальные архитектурные возможности запланированы.

Уточнение2026-10-17: по запросу пользователя перед ENG1 реализован отдельный
UI компонент [RichTextEditor/PA7a](alloy/PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17)
поверх Topten.RichTextKit с options. Детальный контракт и acceptance ведутся
только в UI плане; назначенный после него ENG1 выполнен, текущий шаг указан выше.
Это расширение не закрывает UI alpha/PA7/beta и не создаёт engine code.

По решению пользователя2026-10-17 дальнейшие улучшения редактора сохранены в
[отдельном UI плане](alloy/RICH_TEXT_EDITOR_PLAN.md) и отложены до развития других
функций UI-модуля. Этот backlog не меняет текущую engine очередность.

По запросу пользователя2026-10-18 в UI плане сохранён дополнительный
[инфраструктурный backlog INF0–INF7](alloy/PLAN.md#дополнительный-план-улучшения-инфраструктуры-alloy--2026-10-18):
registry cleanup, dispatcher, scheduling/close, diagnostics и integration checks.
По следующим запросам пользователя INF0/INF1/INF2 выполнены2026-10-18: registry
cleanup retry, bounded dispatcher/awaitable dispatch и real assembly tests.
Минимальный INF5 реализован следующим шагом: opt-in bounded session/phase/queue
diagnostics; полный INF5 остаётся частичным. Детальные задачи/acceptance только в
UI плане. INF3 scheduling и INF4 async close выполнены следующими шагами2026-10-18;
INF6 explicit factories/compatibility и INF7 воспроизводимый integration runner
для real assemblies/factory/package checks выполнены2026-10-19 в bounded scope.
ENG3/ENG4a–c и ENG4d reference viewport bridge завершены в bounded scope;
ENG4d2 ordinary GameEditor GPU transport выполнен; ENG4e baseline получен,
следующий общий функциональный шаг — ENG5.
Полный INF5 и отложенный RTE backlog сохраняются отдельно; общая UI alpha этим
не объявляется завершённой.

По запросу пользователя2026-10-21 добавлен отдельный UI этап
[PA8a: дефолтный стиль контролов и оформление окна](alloy/PLAN.md#pa8a-дефолтный-стиль-контролов-и-оформление-окна--2026-10-21):
минималистичное цветное оформление с прямыми углами и готовые borderless window
widgets. Подробные решения, подэтапы и acceptance ведутся только в UI плане.
По запросу пользователя2026-10-22 PA8a0–2 реализованы, автоматическая часть PA8a3
проверена: Dark/Light style, стандартная рамка, gallery/native/golden/packages.
Physical DPI/OS acceptance остаётся открытым. Engine очередность развивается
отдельно: следующий шаг ENG5c.3 collisions камеры. [API](alloy/DEFAULT_STYLE.md),
[текущий UI статус](alloy/STATUS.md).

| Этап | Результат | Зависимость и готовность |
| --- | --- | --- |
| G1. Исходное состояние | Сверенная карта проектов, scope модулей, перечень поддерживаемых конфигураций и отдельные baseline результаты build/tests | Документы созданы; проверки всех модулей ещё нужны. Каждый failure закреплён за модулем, skipped и пустая discovery не считаются passing coverage |
| G2. Bootstrap и DI | Проверенные startup/stop/cancellation/error/disposal contracts приложения и обоих выбранных DI путей | [CORE1–CORE3](core/PLAN.md), [COB1–COB3](cobalt/PLAN.md). Ошибка интеграции разбирается на app/resolver границе |
| G3. IPC и units | Рабочий ограниченный сценарий собственного child process с invocation и управляемым завершением/restart | [ENE1–ENE3](enerit/PLAN.md), [MIT1–MIT3](mithril/PLAN.md), затем совместный integration proof |
| G4. UI alpha | Закрытые UI критерии K1–K9 и consumer/native delivery на заявленной конфигурации | [UI маршрут](alloy/PLAN.md#актуальная-точка-продолжения--реализация-dpi-2026-10-09). Работать независимо от G3; общие Core/DI ошибки остаются integration блокерами там, где влияют на UI |
| G5. Сквозные приложения | Небольшие воспроизводимые examples для headless, units/IPC и UI; failures/cancellation/cleanup подтверждены вне отдельных component tests | G2 и нужные локальные этапы. UI не обязателен для headless/units сценария, IPC не обязателен для UI |
| G6. Поставка и общий выпуск | Package consumers, CI/configuration matrix, compatibility notes и воспроизводимые release artifacts | Проверенные критерии выпуска ниже; version suffix и solution build не заменяют их |

G1–G6 — общий roadmap, а не новые UI требования. За исключением выполненной документальной карты в G1, готовность этих этапов требует отдельных запусков. Даты выпуска и новые публичные API этим планом не назначаются.

Актуальный порядок, сверка2026-10-11: CORE5/COB4 independent headless package/analyzer
consumer проверен на Windows x64/net10.0/SDK10.0.401 — 64 matrix + 3 delivered README
processes, Debug/Release build/publish без RID; текущие Core81/Cobalt37/generator6
регрессии прошли. [Детальный сценарий у Core](core/PLAN.md#core5--cob4-независимый-headless-consumer--2026-10-11).
MIT0 bounded own-child baseline проверен2026-10-11: Mithril14/14 Debug и14/14
Release, manual16/16 Debug/published Release; `Never` handle scope без App/IPC.
[Локальный контракт](mithril/PLAN.md#mit0-own-child-контракт--2026-10-11).
ENE1/ENE2 bounded same-build pipes проверен2026-10-11: runtime71/71 и generator34/34
в Debug/Release, manual12/12; [контракт](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11).
MIT1/MIT2 App/controller/parent-exit проверен2026-10-11: Mithril65/65 Debug/Release, App matrix28/28 на Cobalt/DI, fresh legacy16/16 в App/Never scope; [контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11).
MIT3 bounded restart проверен2026-10-12: Mithril89/89 Debug/Release, restart40/40, App Never regression28/28 и legacy16/16; [контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12).
MIT4/ENE4 bounded composition проверен2026-10-14: Enerit102/102, generator34/34,
Mithril89/89, Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release;
App/pipes/restart matrix36/36 Cobalt/DI Debug/published Release, isolated pipes12/12.
[Единый контракт](mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14).
Enerit strict response/decode ownership проверены далее2026-10-14:
runtime126/126, generator114/114, Mithril89/89 в Debug/Release, fresh App36/isolated12;
[локальный контракт](enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14).
MIT5/ENE5 package portion проверена далее2026-10-14:9nupkgs,108consumer cases,
Cobalt/DI включая DI без Cobalt analyzer, Debug/Release build/publish;
[контракт](mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14).
На момент package portion ближайшим шагом был измеренный IPC baseline до оптимизаций;
На тот момент operational diagnostics MIT5 и performance ENE5 были открытыми частями;
их отдельные результаты2026-10-15 сведены в [quality matrix](QUALITY.md).
Текущий приоритет уточнён ниже2026-10-14: качество существующих модулей, начиная с Cobalt.
UI owner/package5.4–5.5 и fixed-font K2/K4 проверены ранее2026-10-11:
UI532/532, golden360/360, validation0/0; эти UI проверки текущим headless шагом не повторены.
NVIDIA GDI defect остаётся внешним K7 блокером; поиск app-side workaround отложен
по решению пользователя, lifecycle acceptance и условия возврата сохраняются.
Physical DPI/4.9, real loss/VRAM и итоговый alpha этап остаются в UI плане.

## Будущее игровое направление — 2026-10-13

По обсуждению зафиксирован [план engine/editor](engine/PLAN.md) для одной
не очень сложной 3D-игры: Editor и Game.Runtime в разных процессах, viewport
кадры, обновление игровой логики, сохранение editor state и standalone game.
[Статус](engine/STATUS.md):2026-10-15 определён
[ENG0 scope](engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15).
ENG1 headless supervisor реализован/проверен2026-10-17; probe projects находятся
в ManualTests, production packages ещё не введены. ENG2 CPU viewport реализован; ENG3–ENG8 запланированы;
детальные contracts/tasks ведутся только в engine плане.

Направление использует Core/Cobalt, Mithril/Enerit и Alloy как отдельные
boundaries, не меняет текущую dependency map. Repeatable unit sessions, frame
transport и cross-process GPU ownership требуют новой реализации и проверки.
После bounded quality Core/Cobalt/Enerit/Mithril/UI пользователь2026-10-15 выбрал
ограниченный engine prototype с открытыми UI рисками. ENG0 фиксирует demo-сцену,
Windows/net10/OpenGL Editor, CPU frames и versioned child replacement.
Headless ENG1 supervisor/identity/lifecycle выполнен2026-10-17;
ENG2 CPU frames/viewport выполнен; следующий code этап — replacement в ENG3.
UI alpha/K1–K9/PA1–PA10 и внешний K7 блокер не отменяются.

## Качество модулей до engine/editor — 2026-10-14

**Предыдущий RichTextEditor шаг2026-10-17:** [RichTextEditor/PA7a](alloy/PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17)
реализован перед ENG1: UI563/563 Debug/Release,6native demo cases,40fresh package
processes и прежний golden360. [Проверки](alloy/HISTORY.md#richtexteditor--2026-10-17).
Own headless suites этим прежним UI запуском не обновлялись; текущая точка ENG2/ENG3 указана выше.

**Предыдущая сводка2026-10-15:** [quality matrix](QUALITY.md) обновлена после
[UI package/Hosting quality](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15):
10fresh packages,36App +4direct processes, explicit fonts/Cobalt/DI/analyzer/native
provenance; исправлен воспроизведённый dispatcher startup defect. UI532/Core107/
Cobalt179/generator91 отдельно Debug/Release и read-only golden360 прошли.
После этих отдельных runs пользователь выбрал ENG0; его scope/contracts определены
документом; тогда следующим code шагом назначен ENG1. K7/physical DPI/loss/VRAM сохраняются;
допуск прототипа не закрывает alpha. Ниже — датированная последовательность решений.

По решению пользователя сначала довести текущие модули до рабочего уровня качества,
затем начинать движок. Первое направление — [Cobalt COB6/COB5/COB7](cobalt/PLAN.md#качество-cobalt-до-engineeditor--решение2026-10-14):
корректность и диагностика, удобство существующего API, измерения, сравнение с
Microsoft DI и условный переход consumers. Автоматическая миграция не назначена.

Уточнение2026-10-14: выбран собственный TrueMoon API и **два Cobalt projects в
одном consumer package**. Microsoft DI ecosystem bridge/контракты не являются
целью; существующий adapter сохраняется. [Принятый scope/поставка](cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14).

Уточнение2026-10-15 после [COB7 comparison](cobalt/COMPARISON.md): Cobalt COB6a–d,
COB5 baseline и COB7 проверены в bounded headless scope. Cobalt остаётся default,
массовая migration explicit Microsoft DI consumers отложена; adapter сохраняется.
Далее собственный Core quality review по порядку ниже. Подтверждённый hot-path
backlog Cobalt ведётся в его плане отдельно, UI migration требует своего proof.

Уточнение2026-10-15 после [Core quality](core/PLAN.md#сквозной-headless-proof--2026-10-15):
configuration/bootstrap, diagnostics и цельный headless source/package proof
проверены в bounded Windows x64/net10.0 scope. Ограничения Core сохранены в его
STATUS; новых APIs/runtime fixes финальный proof не потребовал. Следующий модуль —
Enerit quality review и ENE5 measured IPC baseline, затем Mithril operational
diagnostics. Это не общий выпуск и не завершение readiness остальных модулей.

Уточнение2026-10-15 после [ENE5 baseline](enerit/BASELINE.md):10payload/concurrency
cases и current Enerit runtime/generator regression проверены; измерительная часть
ENE5 закрыта в same-build pipes scope. Старый package proof сохраняет свою дату,
MMF/versioning/backpressure и другие режимы не объявляются готовыми. Следующий
функциональный этап — Mithril MIT5 operational diagnostics. Enerit allocation/CPU
profile, как и Cobalt hot-path investigation, остаётся отдельным measured backlog
перед выбором оптимизаций, без автоматического расширения API.

Рабочий уровень оценивается в заявленном scope каждого модуля:

- Поддержанные API/limitations и ownership contracts понятны из примера и документации.
- Invalid input, startup/operation failure и cancellation имеют ясную исходную
  причину; повторные операции и cleanup соответствуют контракту.
- Есть текущие поведенческие проверки модуля и нужной интеграции, включая errors
  и lifecycle; успешный соседний модуль не подменяет собственные результаты.
- Independent consumer подтверждает нужные runtime/analyzer/native assets;
  warnings рассмотрены по причине, а не массово подавлены.
- Performance workload и baseline зафиксированы перед оптимизацией; неподтверждённые
  платформы, транспорты и deployment modes остаются явно ограниченными.

Детальные задачи остаются у владельцев: после Cobalt — Core bootstrap/configuration/
diagnostics, Enerit serialization/transport/errors и ENE5 measurements, Mithril
lifecycle/operational diagnostics, Alloy/Argentis UI acceptance. Объём следующего
прохода определяется ревью соответствующего модуля. ENE3/MMF и новые DI/UI APIs
не становятся обязательными только ради расширения возможностей.
UI K1–K9/PA1–PA10 и внешний K7 сохраняются; подготовка аппаратного окружения
для physical DPI/real loss/VRAM остаётся отдельным условием проверки.
Перед ENG0 свести достигнутый уровень и оставшиеся ограничения этих модулей;
требование качества не означает закрытие всех будущих roadmap пунктов.

Уточнение2026-10-15: [Mithril MIT5](mithril/PLAN.md#mit5-operational-diagnostics--2026-10-15)
дополнил bounded quality проход process diagnostics и свежей independent package
regression. Следующий этап — сводная quality matrix Core/Cobalt/Enerit/Mithril и
оставшегося UI acceptance перед выбором ENG0. Other platform modes, долгие stress
checks и measured allocation backlog не становятся автоматически новым functional scope.
## Сквозные правила

- **Lifecycle и ownership:** определить владельца app, service, process, transport и graphics resources; startup failure, cancellation и shutdown имеют проверяемый результат и cleanup.
- **Ошибки и диагностика:** исходная причина доступна вызывающему коду; module error не маскируется успехом другого сценария. Согласовать, какие diagnostics нужны для расследования отказа.
- **Конфигурация:** проверять обязательные параметры до запуска внешних ресурсов; precedence/providers и изменяемость settings документировать в Core.
- **Generators:** сохранять runtime/generated contracts, проверять диагностику ошибок и поставку analyzer в отдельном consumer.
- **Производительность:** измерять контрольный сценарий перед оптимизацией. UI K9 остаётся локальным baseline; его результат не переносится на IPC/DI/process startup.
- **Совместимость:** breaking changes и migration notes фиксировать у владельца API; объединённый release описывает их вместе. Сейчас общая версия0.2.2.7-alpha задаётся [Directory.Build.props](../Directory.Build.props), отдельные версии модулей не вводятся этой реорганизацией.

## Метаданные NuGet-пакетов — 2026-10-19

Общие Authors/Company/Product/version/copyright, project/repository URLs,
repository type, license/icon и базовые tags наследуются из Directory.Build.props.
Asset paths привязаны к MSBuildThisFileDirectory, а не к глубине project directory.
PackageId/version используют штатные SDK defaults; не требуется дублировать их
в каждом csproj. Каждый предназначенный для поставки проект задаёт meaningful
Description, тематические tags и собственный PACKAGE.md с явным включением в nupkg.
Runtime/generator package ownership сохраняется в планах модулей.

Исторический срез2026-10-19 фиксировал13packable projects и deferred Aether.
В текущем source Aether и Editor — library/IsPackable=true; Editor получил
metadata/PACKAGE.md и local pack proof в ARCH2b, explicit runtime/headless process
proof — в ARCH2c. Independent NuGet/asset/full native acceptance остаётся ARCH2d/e. Cobalt.Generator поставляется внутри Cobalt,
отдельным пакетом не объявляется; Enerit.Generator — отдельный analyzer package.
Tests/benchmarks/executable examples не становятся framework package candidates.

[Check-PackageMetadata.ps1](../eng/Check-PackageMetadata.ps1) проверяет вычисленные
MSBuild properties всех repository projects. `-Pack` дополнительно собирает ready
packages и explicit Aether preview в отдельные directories, сверяет nuspec/README/
license/icon/XML/analyzer/native payloads и hashes. Existing evidence не overwrite;
обычный IsPackable флаг deferred проекта не меняется. Это не NuGet push и не замена
runtime/consumer/native/release acceptance R1–R6.

## Общие критерии выпуска

| Критерий | Требуемое свидетельство |
| --- | --- |
| R1. Сборка и проверки | Датированные restore/build и результаты по каждому заявленному тестовому проекту, включая discovery, failed/skipped и ограничения. Политика warnings и поддерживаемые SDK/TFM записаны явно |
| R2. Контракты | Локальные критерии включённых в выпуск модулей пройдены; непроверенные режимы не выдаются за поддержанные |
| R3. Интеграция | Собственные процессы, DI, cancellation и ошибки проверены в заявленных сквозных examples; ресурсы не остаются после завершения |
| R4. Поставка | Pack/publish и независимые consumers подтверждают references, generators, transitive dependencies, native/assets delivery; GPU consumer проверяется на заявленной UI конфигурации |
| R5. Документация | Карта подключения модулей, рабочие примеры, ownership/threading/errors, ограничения и migration notes соответствуют выпускаемому артефакту |
| R6. Воспроизводимость | Версии source/SDK/packages/native/assets/configuration и точные команды сохранены; release readiness не выводится из исторических результатов |

Набор модулей общего выпуска и заявленные платформы определяются явно перед G6. Локальный выпуск одного модуля проверяется по его собственным критериям и зависимостям; он не помечает общий roadmap завершённым.

## После первой проверенной поставки

Развивать реальные consumers: расширение конфигурации и диагностики Core, DI после измерения startup/allocations, новые IPC capabilities по потребности приложения, операционную диагностику units и UI beta по PA1–PA10. Эти направления уточняются после первой рабочей проверки соответствующего модуля; сетевые transports, distributed orchestration или новые UI API не обещаются текущим общим планом.

## Поддержание плана

Соблюдать [общий формат](PLANNING.md). Новые глобальные задачи получают G/R идентификаторы, локальные — идентификаторы своего модуля. Подробные проверки и история хранятся локально; общий STATUS содержит ссылки и межмодульные ограничения. Согласованные UI1–5/K1–K9/PA1–PA10 сохраняются.
