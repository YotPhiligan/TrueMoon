# TrueMoon.Aether.Editor: план подключаемого редактора

Согласовано с пользователем **2026-10-22** после обсуждения перехода от стенда и
proof of concept к приложению для повседневной работы. Это прикладной подплан
направления Aether, развивающий ENG7; отдельного функционального модуля или новой
engine assembly этим документом не создаётся. **Решение2026-10-27:** целевой
TrueMoon.Aether.Editor — library/NuGet package, подключаемый к executable host
пользовательского решения; Main/CLI/App composition остаются в host.
Переход [ARCH2](PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) предшествует
новым ED features. ARCH2a/b shared scene persistence и Editor library/App
integration и ARCH2c explicit runtime launch/bootstrap/identity реализованы;
ARCH2 source/package/headless/native transition проверен2026-10-29 в bounded
Windows x64 profile; full Editor ED1–ED8 и hardware/release gates не закрыты.
Минимальные executable consumers — Examples/
AetherEditor и AetherRuntimeHost; directed smoke workflows — AetherEditorHost.
**ED1a project/session и ED2a/b documents реализованы2026-10-23;
прикладные ED1–ED8 остаются частично открытыми**. Наличие плана не означает
реализации перечисленных возможностей.

[План движка и общие контракты](PLAN.md), [текущий статус Aether](STATUS.md),
[план UI](../alloy/PLAN.md), [статус UI](../alloy/STATUS.md),
[запуск текущего приложения](../../TrueMoon.Aether.Editor/README.md),
[правила планирования](../PLANNING.md).

Детальные задачи приложения ведутся здесь. PLAN.md хранит engine contracts,
ARCH1/ENG dependencies и ссылки на этот подплан; общий PLAN и UI roadmap не
дублируют ED checklist. Фактические результаты работы записываются в STATUS и
HISTORY с точной областью проверок.

## Цель и критерий первой рабочей версии

Пользователь создаёт или открывает собственный проект, редактирует сцену,
видит результат, отменяет изменения, сохраняет и повторно открывает документ,
запускает игру и получает понятную диагностику ошибок. Пользователь подключает
пакеты Aether/Editor в своё исполняемое приложение через публичную App integration.
Опубликованный host работает вне source checkout; для выбора игры, встраивания
Editor View или добавления панели/команды не требуется менять исходники редактора.
Стандартный executable-пример подтверждает подключение, но не является dependency
пакета. Подробные package/runtime/ownership критерии принадлежат ARCH2.

Основной сценарий:

1. Создать/открыть проект и сцену с устойчивыми IDs объектов и компонентов.
2. Выбрать объект в иерархии или viewport, изменить его Transform в инспекторе.
3. Получить проверенное изменение документа и соответствующий Edit preview.
4. Выполнить Undo/Redo; сохранить, закрыть и открыть сцену с тем же результатом.
5. Собрать игру, запустить/приостановить/остановить её, при необходимости применить
   новую сборку через существующий Replace. Ошибка runtime не закрывает документ.
6. Закрыть проект/приложение с Save/Discard/Cancel для несохранённых изменений;
   фоновая работа и принадлежащие приложению ресурсы завершаются наблюдаемым образом.

Исходная проверяемая конфигурация — Windows x64/net10.0/xUnit, существующий
OpenGL Editor/Vulkan child и выбранный явно transport. Первый срез работает с
одним активным проектом и одной сценой; multi-project/multi-document UX расширяется
по реальной потребности. Формат и расширения файлов, точные compatibility versions,
лимиты документов и budgets выбираются в соответствующих этапах до реализации.
Текущие engine/wire/frame limits не увеличиваются молча.

## Исходное состояние при фиксации плана

Реализованы bounded ENG7a shell/toolbar, отдельный game child, Build/Play/Pause/
Step/Stop/Replace, read-only inspector и выбор demo-объекта. UIA1 добавил Fluent
`View<EditorViewModel>`, отдельные model/style, typed view styles и DI factories.
Engine имеет immutable authoring model/scene resolution ARCH1a.1/2 и primitives
projection. Эти реализации и прежние проверки описаны в STATUS/HISTORY.

Текущий demo SceneSnapshot и source-checkout defaults ещё не являются форматом
проекта и сохраняемым authoring-документом. Нет завершённого пути project/document
persistence, общего mutation API с undo, editable inspector, независимого Edit
preview и контрактов расширения shell. Runtime viewport после запуска игры не
считается Edit preview. Предыдущие passing suites подтверждают свой scope, а не ED1–ED8.

После фиксации плана реализованы минимальный ARCH1b.1 Transform/query/history API,
независимый engine Edit preview, ED1a project/session и ED2a/b documents ниже.
Обычный Editor UI загружает/сохраняет документы; Transform/history/preview APIs
подключены к редактируемому inspector в ED3a. Это результаты прежнего WinExe
профиля, они не означают, что ARCH2 library/package transition выполнен.

## Архитектура и ограничения сложности

### Границы ответственности

| Область | Ответственность |
| --- | --- |
| Editor View | Fluent tree, explicit bindings, визуальное представление состояния, подключение действий и UI lifetime |
| Data/ViewModel | Данные и состояние экрана, validation presentation, availability и вызов прикладных операций; без controls/theme/renderer/native handles |
| `IViewStyle<TView>` / `Theme` | Scoped appearance и общие visual tokens; без изменения документа или выполнения команд |
| Прикладные сервисы Editor | Project/document sessions, сохранение, координация команд/задач, selection, панели и tools |
| Общий authoring API Aether | Engine model, общие scene formats/validation/resolution, операции над entities/components, revisions, atomic batches и undo/history по ARCH1b; доступен без UI |
| Runtime Aether | Preview/игровая сессия, simulation/rendering, IPC/frame production; отдельный процесс и собственное состояние |
| Исполняемый host | Main/CLI/App composition, выбор поддержанного window/backend/resources и explicit runtime launch; подключает пакеты, не содержит reusable Editor/engine implementation |
| Argentis/Alloy и интеграции | Контролы, composition/layout/input/focus, overlays, sessions/rendering/platform; общие UI возможности принадлежат UI плану |

У документа один владелец committed state, revision и history. ViewModel не
поддерживает вторую независимо изменяемую сцену. Поля инспектора могут хранить
draft до commit/cancel, но не обходят authoring API прямыми mutable references.
Editor library содержит не только View: её прикладные services/Data/styles/commands
остаются вместе с UI consumers. Общие engine операции и форматы доступны из Aether;
размещение existing типов и migration зафиксированы в
[ARCH2a карте](PACKAGE_BOUNDARIES.md), без папок по виду типа. SceneFileCodec/store и
file DTOs перенесены в Aether.Scenes; Editor document owner/close policy не перемещены.
Планируемый UseAetherEditor(...) предоставляет module registrations и фабрики;
Window/parent владеет созданным View, App/project — сервисами и задачами. Backend
и standalone/embedded host setup выбирает потребитель в поддержанной конфигурации.
Authoring document, resolved snapshot, live World revision и simulation tick
остаются различными понятиями.

Кнопка, меню и shortcut вызывают одну прикладную команду с одним состоянием
доступности/выполнения. UI-команды и undoable authoring mutations различаются:
Play, Cancel или Open не становятся обратимыми изменениями сцены автоматически.
Один drag или подтверждённое редактирование поля образуют осмысленную history
transaction; каждый вводимый символ не создаёт самостоятельную Undo-запись.

### Организация кода и владение

- Функциональные области: Projects, Documents, Commands, Scene, Inspector,
  Viewport, Build, Diagnostics. Это ориентир для предметной организации, а не
  требование заранее создать пустые папки и сервисы.
- Связанные View/ViewModel/style/interfaces/implementations размещаются вместе
  по назначению; общие папки Models/Interfaces/Services не вводятся. Namespace
  следует пути. Типы без самостоятельной категории остаются в корне проекта.
- Новые сборки появляются при самостоятельной поставке или реальной границе
  зависимостей. Абстракция не требует отдельного assembly сама по себе.
- App — composition/lifecycle boundary, Cobalt — default provider; существующий
  Microsoft DI adapter сохраняется. Services/models/styles/factories получают
  constructor dependencies, прямой Resolve остаётся в composition/factory code.
- IFactory создаёт project/document/panel/view instances с явным owner. Окно и
  дерево владеют UI, App/project session — своими сервисами. Borrowed aliases не
  добавляют disposable owners; подписки, watchers и tasks имеют явное завершение.
- При замене проекта/документа late results не применяются к новому владельцу.
  UI mutations выполняются на owner thread. FramePresented отвечает за presentation;
  прикладной workflow и сборка документа не должны разрастаться внутри callback.
- Прямые вызовы, небольшие сервисы и типизированные события достаточны до появления
  необходимости в более общем механизме. Интерфейс вводится для реальной границы,
  подмены или расширения. Внутренним helper-классам он не обязателен.

## Расширяемость

### Расширения приложения Editor

Расширение должно добавлять функциональность через App-local registration,
не требуя правок центрального shell/dispatch switch. Начальные кандидаты:

| Контракт | Назначение и требуемое поведение |
| --- | --- |
| IEditorCommand | Устойчивая идентичность, метаданные, availability/execution state, async execution/cancellation; общая операция для toolbar/menu/shortcut |
| IEditorPanelFactory | Создание панели с конкретными dependencies/arguments через IFactory; owner-thread, open/close, подписки и cleanup |
| IPropertyEditorFactory | Выбор редактора по схеме свойства и capabilities; draft/conversion/validation/commit/cancel, понятный fallback для неизвестного типа |
| IViewportTool | Активация/деактивация, обработка ввода, capture/focus, отмена и transaction boundaries выбора/transform |

Назначения согласованы; точные имена, signatures и минимальный состав public API
уточняются при первом потребителе. Все четыре интерфейса не создаются заранее ради
таблицы. Generic EditorContext с контейнером не передаётся расширениям вместо
constructor dependencies.

Регистрация сначала обычная, в доверенном App/module setup. Catalogs имеют явное
правило duplicate IDs, deterministic ordering и момент завершения registration.
Ошибки проверяются до выделения UI/native resources. Runtime ошибки расширения
должны быть диагностируемыми; частично повреждённый документ не объявляется успешным.
Dynamic DLL loading/unloading, hot reload и отдельная plugin-платформа не являются
условием первой версии.

### Пользовательские UI controls

Новый widget может быть композицией существующих элементов или наследником Element
с MeasureCore/ArrangeCore/DrawCore/HandleInput. Общие rendering/input capabilities
опираются на IDrawingContext/IInputContext; специфичный renderer не становится
зависимостью любого нового контрола.

Интерфейсы отдельных возможностей, например action source или selection model,
добавляются по общим потребителям. Одного IButton недостаточно для произвольной
замены: текущий runtime принимает Element, а IElement не описывает полный
layout/input/attachment lifetime. Полная замена Element интерфейсной иерархией —
отдельное изменение ядра, не обязательное для расширения редактора.

Контрольный пример расширения должен добавить command/menu-or-toolbar entry,
панель и пользовательский property editor без правки core shell. Отдельный
custom widget проверяет layout/input/focus/style/lifetime через существующие
backend-independent contracts. Общие реализации и acceptance ведутся в UI плане.

## Этапы и зависимости

| Этап | Результат | Зависимости |
| --- | --- | --- |
| [ARCH2](PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) | Проверен в bounded Windows x64 profile: headless8/8 и native24/24 source/package build/publish | Подробные этапы/evidence только в engine плане; physical/K3/K7/K8/VRAM и general release отдельно |
| ED1 | Проект и рабочая сессия | App/DI/lifecycle; текущий UIA1 |
| ED2 | Документы и сохранение | ED1; immutable authoring model |
| ED3 | Общие операции редактирования и Undo/Redo | ED2; минимальный ARCH1b и его headless proof до подключения edit UI |
| ED4 | Рабочие панели, инспектор, меню и shortcuts | ED1–ED3; редактирование подключается после engine authoring/preview proof; нужные UI capabilities |
| ED5 | Edit preview, camera/picking и viewport tools | Минимальный preview Aether — до editable UI; полноценные tools/selection интегрируются с ED2–ED4 |
| ED6 | Повседневный Build/Play/Replace workflow | ED1–ED5; существующие compiler/supervisor/replacement contracts |
| ED7 | Проверенная расширяемость | Минимальные contracts появляются в ED3–ED5; ED7 подтверждает независимое расширение |
| ED8 | Надёжность и поставка | Подтверждает ED1–ED7; recovery/diagnostics разрабатываются вместе с владельцами |

Это порядок пользовательских результатов. Нужные интерфейсы, cleanup и regression
tests вводятся вместе с соответствующей функциональностью, не откладываются до
ED7/ED8. Единичный generic UI control не требует завершить весь UI beta roadmap.
Сверка2026-10-29: [ARCH2](PLAN.md#arch2e-независимый-nuget-consumer--декомпозиция-2026-10-28)
проверен в source/package build/published headless/native profile, следующий
этап — ED6a/ED8a console/export после проверенного ARCH2e.4 contract. ENG2 CPU race, legacy Build/Replace,
MSB3030 staging paths и forwarding test follow-ups закрыты в своих scopes;
датированные TRX и ограничения — в STATUS/HISTORY. Native24/24 и свежий package
headless8/8 прошли после Hosting lifecycle fix; Hosting114/Editor763 Debug/Release
повторены, прежний Aether1543/1543 сохраняет свой run. Дальнейшая очередь —
ED6a → ED8a console/export в подключаемом Editor, ED4c.2 → ED4c.3,
project picker/switch и ED1/ED6 workflow. Gameplay ENG5c.3 и hardware gates прежние.

Уточнение2026-10-23: [общая основа API Aether](PLAN.md#основа-api-перед-редактируемым-ui--2026-10-23)
предшествует редактируемым inspector/tools: contracts → минимальный ARCH1b →
headless validation/conflict/atomicity/Undo proof → независимый Edit preview →
подключение UI. ED1a project/session идёт параллельно. Детальные engine tasks
ведутся по ссылке, а не дублируются в ED checklist. Полный World/ECS и ARCH1c
не блокируют первый срез; authoring и runtime API остаются разными границами.

### IPC и процессы

Перед частым preview/input трафиком проверить предложенный
[точечный проход Enerit/Mithril](../PLAN.md#сверка-основы-перед-aether--2026-10-23).
Он не блокирует локальный ARCH1b/headless или ED1a и не требует завершить все
базовые модули. Прикладные ED-задачи не дублируют transport/process acceptance.

**Enerit выбран для IPC Editor↔Runtime** ([решение и границы](PLAN.md#ipc-editor-и-runtime-enerit--2026-10-23)).
Управление, input, queries, результаты/ошибки/progress и frame descriptors идут
через typed invocation/generated adapters; текущий baseline — same-build named
pipes. Новый preview protocol проектируется с явными limits/version/fencing.
Pixels/shared GPU images остаются в отдельном frame transport. Mithril владеет
запуском/stop/reap процесса; локальный authoring service работает без IPC и окна.
Play не изменяет сохранённую сцену автоматически.
Runtime-host bootstrap/explicit executable и pair identity реализованы в ARCH2c;
current-executable --runtime остаётся только явным совместимым профилем.
Runtime не зависит от Editor package; hosts/assets/package acceptance продолжаются в ARCH2d/e.

## ED1. Проект и рабочая сессия

- [x] Определить versioned project description: игровой csproj, entry assembly/type,
  scenes/content roots и относительные пути; отдельно user-local recent projects,
  theme/layout/preferences. Не хранить machine-specific state в игровом документе.
- [x] Реализовать Create/Open/Close project и единственного владельца active session;
  валидировать пути/toolchain и сообщать отсутствующие файлы/SDK как actionable errors.
- [x] Убрать обязательную зависимость нормального запуска от demo и source checkout.
  Вход по явному пути допустим на этапе service/API; меню/диалоги подключаются в ED4.
- [ ] Определить project switch/cancel и завершение tasks/subscriptions/resources;
  не заменять рабочую сессию частично подготовленной новой.

Готовность: опубликованный редактор открывает пользовательский проект вне repo;
ошибка/cancel открытия сохраняют прежнюю рабочую сессию. Unsaved-document policy
добавляется ED2 и становится обязательной при интеграции.

### ED1a: первый project/session profile — 2026-10-23

`UseAetherEditorProjects` регистрирует App-owned `EditorProjects`, default
`IProjectStore`/`IProjectToolchain` и shared
`IFactory<ProjectSession, ProjectSessionArguments>`. Store/toolchain/factory можно
зарегистрировать до extension; coordinator их заимствует. Factory products
принадлежат coordinator и закрываются при successful switch/Close/App shutdown.
Lifecycle aliases не создают дополнительных disposable owners. Регистрация и
startup не открывают проект и не запускают окно, engine, SDK probe или compiler.

`.aetherproj` — flat UTF8 JSON schema1 с optional BOM, максимум64KiB вместе с BOM:
formatVersion, persistent nonempty id, name, gameProject, entryAssembly, entryType,
scenesRoot, contentRoot. Unknown/duplicate/case-mismatched members отклоняются.
Name≤128, entryAssembly≤128, paths/entryType≤512. Entry — flat DLL и qualified
non-generic type; project path — существующий csproj, roots — существующие каталоги.
Пути только относительные и лексически внутри manifest directory; нормализуются
независимо от CWD. Это trusted-local Windows profile, не sandbox поверх links.
Recent projects/theme/layout/preferences остаются user-local будущими настройками,
не входят в игровой manifest; scene format/schema/migrations принадлежат ED2.

Open/создание candidate сначала проверяют все metadata/paths и выбранный SDK через
owned bounded `dotnet --version` из game directory с учётом global.json. Требуется
SDK10+; никакой MSBuild evaluation/build или game activation при открытии нет.
Совместимость самого csproj/entry проверяет последующий explicit Build. Create
оборачивает уже существующий игровой csproj/roots новым manifest; шаблоны игры и
wizard не входят в этот срез. После preflight файл публикуется atomic rename в том
же каталоге без overwrite. Cancel после file commit может оставить валидный новый
manifest на диске, но candidate не становится active.

Только одна Open/Create операция; concurrent work отклоняется сразу. Ошибка/cancel
подготовки сохраняют active session. Close отменяет и дожидается accepted work,
не допускает late publish и затем закрывает active; после Close можно Open снова.
App shutdown закрывает admission окончательно и drains до DI dependency disposal.
Первый ProjectSession хранит metadata/SDK identity и выдаёт GameBuildRequest;
При подключённом ED2a ProjectSession также владеет document session; runtime
products пока не принадлежат ему. Metadata-only регистрация остаётся доступной.

Обычный запуск без аргументов — пустая workspace, game commands disabled;
`--project path.aetherproj` выбирает project/build identity с пустой сценой до ED2.
`--check-project path.aetherproj` выполняет тот же App/Cobalt preflight без окна и
выводит JSON diagnostics. Explicit legacy `--project game.csproj` сохраняет demo
workflow; автоматического поиска checkout/demo больше нет.
[Примеры API/manifest/CLI](../../TrueMoon.Aether.Editor/README.md#проект-и-сессия-ed1a).

Четвёртый ED1 пункт остаётся открытым для прикладной Switch/Close транзакции с
documents, compiler/preview/game work и subscriptions; metadata preparation уже
fenced/drained. ED2a добавил document API Save/Discard/Cancel и dirty/busy guard
project switch/Close. Общая UI-транзакция и подключение authoring/preview ещё нужны.
Полный ED1–ED8 этими срезами не закрыт.

## ED2. Документы и сохранение

- [x] Согласовать schema/version/migration и лимиты project/scene files; сохранять
  устойчивые entity/component/definition IDs и необходимые authoring catalogs.
- [x] Создать document session: committed SceneDocument, path, revision, saved
  checkpoint, dirty state и loading/saving/error states. SceneSnapshot не подменяет
  сохраняемую authoring model.
- [x] Реализовать Load/Save/SaveAs с atomic publication; failed write не повреждает
  прежний файл. Failed SaveAs не меняет current path и saved checkpoint.
- [x] При сохранении фиксировать конкретный snapshot. Изменения во время save
  остаются dirty; поздний completion не помечает новый документ сохранённым.
- [x] Реализовать Save/Discard/Cancel при document Close/Switch и window Exit; предусмотреть
  обнаружение внешнего изменения файла без молчаливого overwrite.

Готовность: сохранённая сцена после перезапуска восстанавливает согласованные
данные; invalid/unsupported files не публикуют частичный документ. IO errors и
cancel не теряют unsaved changes. Dirty учитывает возврат Undo к saved checkpoint.

### ED2a: document/persistence API — 2026-10-23

`.aetherscene` — строгий UTF8 JSON formatVersion1 с optional BOM, максимум4MiB
вместе с BOM и JSON depth32. Сохраняются исходные SceneDocument ID/revision,
entities/parent IDs, definition inheritance/patches и schema catalogs. Resolved
scene, runtime state и Undo history не сериализуются. Scalar kind явно отличает
Int64/finite Double/Boolean/String/null, отсутствие свойства сохраняется отдельно
от explicit null/default. Unknown/duplicate/case-mismatched members и unsupported
versions отклоняются; автоматических миграций нет до появления следующей версии.
Лимиты:4096entities/definitions,1024schemas; на patch256added/removed/restored и
1024overrides;256properties на component/schema и256required types. Engine
authoring factory валидирует документ до записи и после чтения; оба направления
ограничены. IDs и порядок значимых списков сохраняются, maps/sets кодируются
детерминированно. Пути только внутри scenes root; trusted-local lexical profile
не является sandbox поверх filesystem links.

`UseAetherAuthoring` → `UseAetherEditorDocuments` → `UseAetherEditorProjects`
регистрируют codec/store и shared typed document factory. ProjectSession владеет
созданной SceneDocumentSession, та — одним engine ISceneAuthoringSession; сервисы
store/factory заимствуются. Application code не выполняет Resolve внутри операций.
Committed state/history принадлежат engine, Editor хранит path/observed stamp и
saved ContentRevision. Undo к checkpoint снимает dirty несмотря на новую
Document.Revision. New всегда unsaved; Load публикует только готовый candidate.

Save фиксирует immutable snapshot, допускает последующие edits и подтверждает
именно сохранённую ContentRevision. Failed/cancelled SaveAs сохраняет прежние
path/checkpoint; поздний save не меняет нового/закрытого владельца. File operations
имеют fail-fast admission, accepted tasks отслеживаются и реально drains при
Close/Dispose, даже когда custom store игнорирует cancellation. Failed cleanup
сохраняет owned products для retry, включая rejected project candidates.

Store пишет временный файл рядом и публикует atomic rename/replace. SaveAs в
другой путь — create-only; текущий путь проверяется по SHA256 наблюдавшихся bytes.
Постоянный пустой `.write-lock` sidecar с exclusive handle согласует cooperating
writers. Это optimistic conflict detection: произвольный внешний rename между
последней проверкой и replace и power-loss durability не гарантируются. Внешние
изменение/удаление до проверки отклоняются без молчаливого overwrite.

Save/Discard/Cancel реализованы в document API, project Close/Switch отказывают
для dirty/busy document и атомарно закрывают clean admission перед заменой.
Terminal App/owner Dispose явно discard/drain; обычный пользовательский переход
должен сначала вызвать CloseAsync с выбранной policy. На этапе ED2a UI
Close/Switch/Exit ещё не был подключён; ED2b ниже реализует document transitions,
project/runtime coordination остаётся в ED1/ED4/ED6. Headless
`--project path.aetherproj --check-scene path.aetherscene` проверяет load/encode и
shutdown без compiler/game/window; [API/пример файла](../../TrueMoon.Aether.Editor/README.md#документ-сцены-ed2a).

ED2b подключает document open/save/close к обычному Editor UI: одно command state,
явный Save/Discard/Cancel и owner-thread обновления ViewModel. Затем Transform
inspector/Undo/Redo и independent preview завершают первый сквозной сценарий;
для него не требуется полный generic mutation API.

### ED2b: document commands и UI — 2026-10-23

Первый UI profile использует один проект, явно выбранный через .aetherproj CLI,
и один документ. Пятый ED2 пункт уточнён до document Close/Switch и window Exit:
эти transitions реализованы; общий project/runtime switch остаётся в ED1/ED6,
project picker и generic menus/dialogs — ED4. ED1 четвёртый пункт не закрыт.

EditorDocumentCommands сериализует New/Open/Save/SaveAs/Close и tracked Exit.
Пока dirty document ждёт Save/Discard/Cancel, admission удерживается. Invalid
candidate сохраняет предыдущий документ даже при выбранном Discard; failed Save
при Exit возвращает false и показывает error без закрытия окна. Concurrent close
делит одну task. Cancel/forced shutdown отменяют и реально drains work, включая
ignored cancellation и throwing callbacks; forced upgrade не ждёт user decision,
cleanup fault допускает retry. Runtime stop/drain завершается до успешного Exit.

EditorDocumentViewModel копирует snapshots/availability/errors на UI owner thread,
отдельно хранит path draft и first-save draft. EditorDocumentView создаётся typed
DI factory, принадлежит parent View, заимствует модель/style; имеет собственный
IViewStyle. Inline path/action/status и Save/Discard/Cancel доступны обычными
controls/keyboard; Ctrl+S/Shift+S/W идут в те же actions, Esc отменяет decision.
Safe focus — Cancel, затем live прежний control/path после завершения работы.
Это обычный inline блок, не реализация общей PA3 overlay/modality infrastructure.

Root workspace адресно показывает raw authoring entity IDs и resolved readonly
properties без legacy runtime projection/64-entity limit; strings экранируются для
single-line controls. Shared shell/viewport/path и неизменившиеся entity rows
сохраняются при Save/theme/state updates. Все IO/decision notifications приходят
через owner-thread refresh; worker не мутирует controls или ViewModel.
Документы доступны только при idle/stopped runtime/compiler; open authoring
document блокирует stale game actions до ED5/ED6 integration.

Acceptance этого профиля: открыть собственный файл, выбрать реальные объекты,
SaveAs/reopen, увидеть dirty/error, Cancel/Save/Discard при scene transitions и
native close; failed Save vetoes Exit, forced shutdown drains. Отдельный legacy
game workflow сохраняется. Editable Transform/Undo/Redo и preview ещё не подключены;
следующий срез — минимальный Transform inspector с Undo/Redo и independent preview.

## ED3. Общие операции редактирования и Undo/Redo

Приоритет после ED2b (сверка2026-10-23): сначала подключить существующий
ARCH1b.1 к минимальному Transform inspector и Undo/Redo, затем independent Edit
preview к committed revision. Поля сохраняют draft до явного commit/cancel;
invalid/stale ввод не меняет history, один commit — одна transaction.
Preview обновляется после commit/history, а не после каждого символа; допускается
только одно выполняемое и одно последнее ожидающее обновление. Его текущий лимит
64 entities и child replacement сообщаются явно; ошибка сохраняет документ.
New пока создаёт пустую сцену: следующий отдельный срез — минимальные create/delete
через общий authoring API/history. Полный World/ECS для этих шагов не требуется.
После первого edit/save/preview цикла — project picker/switch и ED1/ED6 workflow;
общие UI capabilities добавляются по конкретному consumer, без предварительной
docking/plugin платформы. ENE6c/d и оставшиеся MIT6a/c остаются отдельными этапами
надёжности до рабочей поставки; первый ENE6a/MIT6a proof их не закрывает.

- [ ] Подключить UI-independent authoring operations ARCH1b: create/delete entity,
  update properties/components и parent relationships с общими validators/resolver.
- [ ] Использовать stable IDs, expected revision и operation identity по engine
  contract; исключить частичное применение batch и повтор мутации при повторной delivery.
- [ ] Подключить document history/Undo/Redo общего authoring API и группировку
  edit gestures; использовать engine history limits/redo policy, не создавать
  вторую history в UI. Editor session хранит saved checkpoint и производный dirty.
- [ ] Сохранить draft/preview/commit/cancel как явные состояния редактирования,
  передавая committed изменения только через authoring API.
- [ ] Подключить command availability к document/operation state; UI и headless
  consumer должны вызывать одинаковую операцию, а не отдельные реализации мутации.

Готовность: UI/headless дают одинаковые данные и diagnostics; invalid/stale/cancelled
operation не оставляет частично изменённую сцену. Undo/Redo восстанавливает документ
и его dirty status; один подтверждённый edit gesture даёт одну history transaction.

### ED3a. Transform/history/preview в UI — 2026-10-23

Минимальный consumer существующего ARCH1b.1, без новых engine/wire contracts.
EditorTransformView/Data/Style имеет девять стабильных полей XYZ, pitch/yaw/roll
в радианах, scale XYZ. Явный Apply/Ctrl+Enter — одна atomic history transaction;
Cancel/Esc не меняет committed content. Числа invariant, finite Float и scale>0;
итоговые validators/resolver остаются общими. Ошибка сохраняет draft/history.
Смена выбранного entity отбрасывает его draft; внешняя смена той же document
revision/content сохраняет draft с conflict до явного Cancel. Undo/Redo недоступны
при draft; в TextBox Ctrl+Z не перехватывается как команда сцены. Вне поля
Ctrl+Z/Shift+Z/Y и кнопки используют общую history/saved checkpoint.

EditorPreview заимствует engine operations, сериализует один выполняемый и один
последний ожидающий committed snapshot. Старт/retry явный; после Apply/Undo/Redo
обновления автоматические. Ошибка не создаёт retry на каждом UI frame, прежняя
presentation не получает новую revision. Stop/New/Open/Close/Exit fence/drain
accepted preview work; Cancel close decision сохраняет preview. Cleanup failure
наблюдаем и допускает retry. View products owned родителем; model/style/coordinator
получаются через App/Cobalt/IFactory, без второй scene/history и service locator.

Текущий preview profile — fixed camera,640×360, Windows x64 OpenGL UI/Vulkan child,
до64entities, child replacement на обновление. Authoring document не обрезается.
Подтверждение среза требует реального edit/preview/Undo/Redo/save/reopen и cleanup;
точные результаты — в STATUS/HISTORY. Создание/удаление объектов и прочие ED3/ED4/
ED5 задачи остаются открытыми, весь ARCH1b этим consumer не закрывается.

### ED4a. Компактная верхняя панель — 2026-10-23

Реализован bounded header/menu срез2026-10-23; уточнение дизайна со знаком TrueMoon
и вращением точек реализовано2026-10-24. Текущие related UI1288/configuration
Debug/Release и4native profiles успешны. Full solution в уточнении не запускался;
прежний Mithril fixture failure остаётся открытым.
[Текущие проверки](HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24),
[первоначальный срез и его границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).
Полный ED4 и hardware gates этим не закрыты; следующий прикладной шаг — create/delete.

Дополнение2026-10-24: dropdown меню используют общий матовый материал
[PA8b](../alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24): blur текущего окна
и theme tint, foreground остаётся чётким, unsupported renderer использует opaque
fallback. Editor не вводит собственный capture/blur сервис; материал — UI value,
ViewModel/команды/DI ownership прежние. Настройка через Theme/typed styles,
`MenuBackdrop = null` отключает эффект. Срез проверен raster и Editor OpenGL;
прозрачность OS окна и native Vulkan effect acceptance остаются вне среза.

По запросу пользователя временный UI приоритет перед create/delete. Одна строка
заголовка: `[TrueMoon icon][⋮] TrueMoon Aether · Project [▶][⏸][■] [—][□][×]`. Hover раскрывает
`Файл / Правка / Вид / Проект / Симуляция` и плавно сдвигает название вправо;
simulation/window actions неподвижны, длинное название сокращается. Кнопка меню доступна
по click/keyboard; меню держится при hover/focus/open popup, light dismiss/Esc/
FocusLost закрывают. Небольшая задержка предотвращает случайное схлопывание.

Уточнение2026-10-24: слева фиксированный геометрический знак TrueMoon — полумесяц
со звездой20px в области28px, цвет Accent текущей темы. Он не получает фокус и
сохраняет Caption region. Сразу после него постоянная кнопка32px без обычного
бордера: точки поворачиваются из ⋮ в ⋯ вместе с reveal180мс и обратно. Размер,
hit area и положение кнопки не меняются. Hover раскрывает только названия групп;
click либо Enter/Space на кнопке закрепляет раскрытие и после завершения анимации
сразу открывает «Файл», фокусируя первый доступный пункт без выполнения команды.
Повторное нажатие закрывает меню и возвращает фокус кнопке. Esc/Tab/outside click/
FocusLost/detach отменяют отложенное открытие; после отмены popup не появляется.
F10 по-прежнему сразу раскрывает headings для клавиатурного выбора. Нулевая
длительность открывает меню без дополнительного жеста. Отрисовка не зависит от
glyph font или bitmap asset; смена темы не пересоздаёт UI-дерево. Фон hover/pressed
и focus indicator сохраняются. Раскрытие сдвигает только название, знак и кнопки
симуляции/окна остаются на месте. Геометрия и отмена проверяются deterministic
tests, desktop source/published proof отдельно; physical DPI acceptance остаётся открыт.

Большую runtime toolbar убрать; существующие file/edit/build/runtime/theme
actions разместить в меню, запуск/продолжение, pause и stop — icon buttons в
заголовке. Runtime semantics/availability используют существующий coordinator,
stop/cancel остаются наблюдаемыми и drainable. Preview и игровая симуляция не
смешиваются. Menu доступно без проекта, недоступные domain actions disabled.
Path draft и unsaved decision сохраняются; общие file picker/project wizard не
добавляются скрытым условием этого шага. New/Open/Save/Close shortcuts сохраняются.

View/Data/Style и UI-owner factories — через App/Cobalt/IFactory. Общие Menu/
Overlay/animation/icon contracts реализуются в [PA3a/PA4a/PA8b UI](../alloy/PLAN.md#pa3apa4apa8b-меню-и-компактный-анимированный-заголовок--2026-10-23).
Сначала общая основа и deterministic tests, затем Editor integration/native proof:
hover/click/key/focus, animations/reversal/idle, actual menu actions, runtime
start/pause/stop, narrow window/DPI logical layout, drag/Snap region metadata,
theme/retained tree и stop/exit cleanup. Hardware-only gates остаются открытыми.
После завершения возвращаемся к minimal create/delete общего authoring API/history.

### ED4b. Компактная строка состояния — 2026-10-24

Дизайн согласован пользователем и реализован2026-10-24. Следом возвращаемся
к create/delete. Высота28logical pixels, текст12, иконки14, interactive target24,
тонкий верхний разделитель и спокойная theme surface. Слева короткий статус с
иконкой/цветом/текстом; рядом отмена только текущей отменяемой операции. Справа
число объектов, Edit Preview и сведения. Tick/PID/full error доступны в подробностях.
Edit Preview переносится из инспектора в собственную матовую панель над статусбаром:
start/retry/stop и actual revision; игровые controls остаются в заголовке.

На малых ширинах secondary segments уходят в сведения; primary status и cancel
сохраняют приоритет. Popups используют общий OverlayHost/backdrop, Esc/outside/
FocusLost/focus restoration, keyboard и enabled actions. Ошибка различима иконкой,
цветом и текстом. Новых fake progress percentages/diagnostic counters нет.

EditorStatusBarView/Data/Style и UI-owner IFactory через App/Cobalt; родитель
владеет View, модель/стиль заимствуются. Structured state вместо разбора StatusText,
retained tree/focus/theme, отсутствие resolution на frame и dispose subscriptions.
Общий StatusBar/overflow contract принадлежит UI. Проверки: states/cancel/errors,
narrow geometry и hidden input, popup ownership/theme/dismissal, both DI providers,
реальный preview start/stop/Save/reopen и source/published native screenshots.

Проверено: UI759/Hosting98/Editor527 Debug/Release =1384/configuration;6native
cases/68PNG/12reaped children. [Evidence/границы](HISTORY.md#ed4b-компактная-строка-состояния--2026-10-24).

Уточнение разметки2026-10-24: StatusBar высотой28 доходит до внутреннего нижнего
бордера окна без пустой полосы. Общая рамка размещает resize grip16 поверх
правого нижнего угла; имеющийся отступ справа24 защищает footer actions.
Высота и положение footer согласованы при resize, maximize/restore и смене темы.

## ED4. Рабочие панели и инспектор

### ED4c. Обозреватель сущностей — начат 2026-10-24

По запросу пользователя следующий UI срез — настоящее дерево вместо плоского
списка: иконки, parent/child связи, раскрытие, выбор и синхронизация с инспектором.
Общий TreeView/ITreeView принадлежит Argentis; прикладные View/Data/Style и
типизированная фабрика — Editor. Изменения документа идут через общий authoring
API/history, с expected revision и проверкой актуального document owner.

1. **ED4c.1 — реализован 2026-10-24.** Иконки обычной/renderable/interactive сущности,
   раскрытие ветвей, один выбор, клавиатура и scroll reveal. Перенос на сущность
   меняет родителя, перенос в пустую область/«В корень» снимает родителя;
   Ctrl+←/→ дают keyboard outdent/indent. Общая engine Reparent transaction
   сохраняет локальный Transform и inheritance; запрещает self/cycles/missing
   targets, stale revision и правки во время IO/runtime/Transform draft.
   Undo/Redo, dirty/save/reopen и committed Edit Preview используют прежних owners.
2. **ED4c.2 — имена и порядок.** Сначала persistent engine metadata/формат и
   операции history: имя экземпляра и sibling order, совместимое чтение старых
   сцен. Затем inline rename (F2/Enter/Esc), before/after drop и клавиши перестановки.
   Нельзя хранить эти правки только в UI или подменять устойчивый ID именем.
3. **ED4c.3 — операции объектов.** Общий create/delete/duplicate с явной политикой
   descendants/references и один UI gesture = одна transaction. Поиск, контекстное
   меню и согласованные команды добавляются к тому же обозревателю.
4. **Дальше по workload.** Multiselect/batch moves, autoscroll/autoexpand при drag,
   virtualization, camera/world-preserving reparent и расширяемые icon providers.

Первый срез сохраняет Guid order родителей/соседей, не обещает rename/reorder.
Данные/состояние не содержат controls/тему; UI хранит только expansion/gesture/focus.
Theme/refresh сохраняют дерево/выбор/scroll; смена snapshot/detach/FocusLost/Esc
отменяет незавершённый drag. Иконки геометрические, чужие OS данные не принимаются.
Проверки: прямые engine/TreeView/Editor/DI tests и native перенос→Undo/Redo→Save/reopen.
Текущие результаты и ограничения — в [STATUS](STATUS.md), точные команды —
в [HISTORY](HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24).
Следующий конкретный шаг — ED4c.2: сохраняемые имена/порядок через общие engine
contracts/history, затем UI rename/reorder; ED4c.3 остаётся открытым.

- [ ] Иерархия сцены отражает реальные parent relationships: выбор, раскрытие,
  поиск и синхронизация с inspector; размер/virtualization проверяются на заданном workload.
- [ ] Inspector строится по component/property schemas: числа, строки, Boolean,
  enum/reference по мере появления schemas; неизвестное свойство имеет явный fallback.
- [ ] Conversion/validation errors остаются у соответствующего поля; commit/cancel,
  read-only и conflicting document revision имеют определённое поведение.
- [ ] Menu/toolbar/shortcut используют одну command registration; Undo/Redo/Save
  доступны с клавиатуры. Overlays/dialogs определяют modality/focus restoration.
- [ ] Минимальный panel host создаёт/закрывает панели фабриками; layout сохраняет
  нужные настройки без обязательной docking-платформы.

Готовность: пользователь редактирует сцену без обращения к коду приложения;
keyboard/focus/selection/scroll сохраняются при точечных обновлениях и смене темы.
Удаление выбранного объекта, закрытие панели и ошибки редактора свойства не оставляют
живых subscriptions или действий над уже закрытым документом.

### ED4d. Динамический инспектор компонентов — начат 2026-10-24

Реализован2026-10-24: retained `EditorInspectorView`/Data/Style, schema-driven
scalar/null/multiline editors, provenance/reset, Add/Remove/Restore и общий
`ComponentEditBatch`/history/Save/reopen/preview. Подробные фактические проверки —
в [HISTORY](HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Этот authoring-срез не закрывает полный ED4 или runtime ECS.

Уточнение UX2026-10-24 по скриншоту: основные действия — icon-only targets26
(reset24), геометрические символы и semantic names; Apply/Cancel/Undo/Redo/Add
собраны в одну строку, Delete рядом с заголовком компонента. Tooltip появляется
через500мс, в том числе у недоступных действий, не меняет focus и закрывается
при вводе/уходе/scroll/смене контекста. Используется существующий OverlayHost.
Schema choices и подписи скрытых компонентов остаются текстом, чтобы различать цели.
Без selected entity инспектор целиком скрыт; viewport постоянно занимает всю рабочую область (уточнение ED4e ниже);
Esc в обозревателе сначала отменяет drag, затем снимает выбор. Сам View остаётся
parent-owned и возвращается при выборе. В ED4c.3 создание открывает inspection
созданной сущности; отдельный UI до появления entity ID требует явного creation
context тогда, а не пустого инспектора при обычном запуске приложения.

Приоритет по запросу пользователя после ED4c.1: inspector строит секции только
существующих компонентов выбранной сущности, поля — по точной component/property
schema. Поддержать Boolean, Int64, finite Number, многострочный String и explicit
null для nullable; неизвестные типы не выдавать за Transform. Показывать provenance
и reset instance override к inherited/schema default. Устойчивые адреса —
EntityId/ComponentId/property key; визуальное имя не участвует в мутациях.

Сначала общий atomic ComponentEditBatch в существующей ISceneAuthoringSession:
Set/Reset property и Add/Remove/Restore/ResetRemoval component. Одна transaction,
expected revision, receipt journal, общие resolver/schema/dependency/multiplicity
checks и Undo/Redo. Локальное удаление убирает occurrence, унаследованное — маскирует;
удаление очищает instance property overrides этого occurrence, Undo восстанавливает
весь прежний patch. Definition catalogs не меняются. JSON v1 уже хранит эти данные.

Единый Inspector ViewModel владеет property drafts и прежними history/preview gates;
header/status/hierarchy используют тот же state. Во время draft/conflict нельзя
менять состав компонентов или родителя; external revision сохраняет draft и
требует явного cancel. Selection change переключает draft на текущую сущность.
Смена темы/обычный refresh сохраняют поля/фокус/выделение/scroll; структурные правки
меняют только затронутые секции. View/Data/Style и IFactory через App/Cobalt.
Generic редакторы выбираются по scalar kind; новый custom UI editor provider,
runtime World/ECS storage/scheduler, изменение templates и live runtime editing
не prerequisites и не входят в этот authoring-срез. Legacy runtime остаётся read-only.

Acceptance: direct engine tests + typed model/UI/DI tests; selection → scalar/null/
multiline edits → Apply → Undo/Redo → component Add/Remove/Restore → Save/reopen →
actual Edit Preview. Отдельно stale document, no-Transform/empty entity, dependency
failure, boundaries, retained UI/theme и ownership. Текущая готовность — в STATUS;
ED4c.2/3 сохраняются следующими этапами после этого уточнения приоритета.

### ED4e. Панели-островки поверх viewport — 2026-10-24

Согласованная компоновка: viewport постоянно занимает всю рабочую область между
верхними controls и status bar. Обозреватель закреплён слева, контекстный инспектор
справа поверх сцены; появление инспектора не меняет viewport/camera framing.
Видимый отступ16logical pixels, скругление8, тонкая граница и мягкая локальная тень.
Высота следует содержимому до доступного предела, длинные панели прокручиваются;
в узком окне ширины ограничиваются без пересечения островков.

Отдельный `Theme.PanelBackdrop` использует общий backdrop механизм: размывается
ранее нарисованная сцена, foreground/fields остаются чёткими. Dark/Light заданы
темой; null возвращает непрозрачный Surface, неподдерживающий backend — material
fallback. Родительский стиль не обходит внутренности вложенных View.

Parent-owned `EditorIsland` применяет fade/slide8px за160мс через существующий
frame clock и общую group opacity. Closing сразу запрещает ввод в содержимое;
завершение скрывает островок, detach завершает переход без фонового таймера.
Начальное состояние без selection скрыто сразу. Переоткрытие не пересоздаёт View.
Панель перехватывает ввод внутри своих bounds, свободная сцена доступна снаружи;
общий OverlayHost сохраняет menu/Tooltip выше островков.

Первый срез — закреплённые островки. Свободные drag/resize/docking, скрытие
обозревателя отдельной командой и выбор качества blur требуют отдельного UX/задачи.
Acceptance: bounds/paint/input/narrow/theme/retention/interrupt/detach tests,
raster group compositing и native source Debug/published Release с actual preview,
Dark/Light/null material и проверкой прежних document/hierarchy/history сценариев.
Это не physical DPI, VRAM или performance acceptance и не завершение ED4/ED5.

### ED4f. Viewport под оконным chrome — 2026-10-24

Реализовано в описанных границах; текущие проверки и ограничения — в
[истории](HISTORY.md#ed4f-viewport-под-оконным-chrome--2026-10-24).

По запросу пользователя убрать постоянную область path/title/scenes-root над сценой.
Viewport занимает всю клиентскую поверхность, включая пространство под title bar и
status bar; hover, menu reveal и document prompts не меняют его bounds/camera framing.
Островки остаются ниже caption с прежним видимым отступом16; footer28 накладывается
снизу. Рамка/resize grip и Caption/Client/Maximize native metadata сохраняются.

Общий опциональный `WindowFrame.TitleBarOverContent` рисует content первым,
title bar следующим. Default layout других consumers прежний; Hosting options
фиксируются при App composition. Header foreground и hit targets постоянны.
Idle backdrop имеет слабый tint без blur; hover проявляет PanelBackdrop за160мс.
Focus/menu в активном окне удерживают подложку, hover работает и в неактивном окне;
detach завершает переход, повторные одинаковые pointer events не создают idle work.
`PanelBackdrop = null` возвращает непрозрачную поверхность.

В caption компактно добавляются имя сцены, dirty marker и activity; полный путь
доступен по Tooltip с переносом длинных Unicode paths. Open/SaveAs/first Save сначала открывают retained path prompt
над viewport, затем требуют явного подтверждения. До него IO не начинается.
Save/Discard/Cancel и actionable errors используют ту же contextual поверхность;
idle tools скрыты и не резервируют высоту. Drafts/controls/borrowed services и
общая document/history/preview admission сохраняются. Ctrl+S/W действуют даже
после клика по сцене без focused control; text editor shortcuts остаются scoped.

`OverlayHost.PopupInsets` оставляет меню/Tooltip ниже caption без сдвига content.
View/Data/Style, Cobalt/IFactory и UI-owner lifecycle прежние; native OS file picker,
project wizard, docking и изменение engine frame transport не prerequisites.
Acceptance: default/overlay/narrow/logical DPI geometry, paint/hit/native regions,
background transition/reversal/idle/inactive-hover, foreground sharpness, path
confirm/cancel/error/retry/unsaved/focus, root shortcuts, retained theme switch;
native source Debug/published Release с actual scene, hover/idle/path Tooltip и
document prompt, прежними game/history/save/preview и zero ownership cleanup.
Physical DPI/OS snap hardware, performance/VRAM и полный ED4/ED5 остаются открытыми.

### ED4f.1. Прозрачный статус-бар — 2026-10-24

Реализовано и проверено в указанном scope;
[актуальные результаты и границы](HISTORY.md#ed4f1-прозрачный-статус-бар--2026-10-24).

По согласованному продолжению ED4f нижняя панель также накладывается на сцену:
height28, текст12/иконки14/targets24 и отступ справа24 сохраняются. Idle фон —
слабый tint без blur; hover/focus проявляют PanelBackdrop за160мс. Анимируется
только подложка, scene bounds/camera и foreground не меняются. Null material
возвращает opaque Surface, Theme/controls/focus сохраняются.

Слева прежний компактный status и отмена текущей операции; справа count/Preview/
details, на узком окне прежний overflow. Ошибки и busy/waiting удерживают материал
без наведения; открытые собственные подробности удерживают его, пока focus находится
в popup. Кадры backdrop не переводят focus на details. Повторный settled pointer
не создаёт idle work, detach/dispose отменяют анимацию. Details/диагностика по клику
используют прежний OverlayHost и общие команды, без нового popup/lifecycle сервиса.
Общий opt-in StatusBar.OverlayContent/KeepBackdropVisible ведётся в UI плане.

Acceptance:160мс/reversal/idle/focus/important state/popup/teardown, sharp foreground
при реальном backdrop, неизменная full-area geometry/resize grip, themes/null
fallback и native source Debug/published Release idle/hover поверх actual scene.

### ED4f.2. Системная граница главного окна — 2026-10-24

Реализовано25октября; [актуальные проверки и границы](HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).

Editor включает общий UseSystemAccentBorder в App composition. Активная граница
использует системный Windows accent, inactive — RGB60% с переходом160мс. При
недоступном цвете возвращается текущий Theme.Border; явный BorderColor в style/local
имеет приоритет. Изменения системного оформления обновляют cached цвет без restart.
Размер/толщина рамки, полный viewport, title/status islands, controls и document/
runtime state сохраняются. Получение цвета и native notifications принадлежат
platform/host, ViewModel не зависит от Windows. Shared contracts/acceptance — PA8b.4
UI плана; native Editor proof проверяет actual active/inactive border pixels,
owned HWND messages и cleanup без изменения настроек ОС.

## ED5. Edit preview и инструменты viewport

Первый [engine preview profile](PLAN.md#edit-preview-первый-engine-profile--2026-10-23)
реализован отдельно от normal Editor UI: immutable document/revision → isolated
built-in runtime → GPU actual-presentation. Использует Enerit/Mithril и прежний
renderer, без game build/Play/Game.Logic. First profile заменяет child на каждое
обновление; fixed camera и GPU-only bounds явно ограничены. Это engine proof,
не завершение пунктов ED5 ниже: прикладные mode switching, связь с active project/
document, tools/camera/picking и UI integration ещё нужны.

- [ ] Определить совместный Editor/Aether режим preview, независимый от ручного
  Build/Play и пользовательского Game.Logic; game code не загружается в CLR Editor.
- [ ] Передавать resolved document snapshots/updates в preview runtime через Enerit
  с document revision и session/generation fencing; не подтверждать устаревший кадр
  как новый. Frame payload остаётся в отдельном frame transport.
- [ ] Добавить editor camera, picking и синхронизацию selection viewport/tree/inspector.
  Разделить editor navigation и gameplay input, включая focus/capture/escape.
- [ ] Ввести transform tools по одному: сначала перемещение, затем необходимые
  вращение/масштаб; drag имеет preview, commit/cancel и одну Undo transaction.
- [ ] Сохранить resize/suspend/resume/first-present и shutdown ownership; сбой preview
  не теряет документ и даёт определённый restart path.

Готовность: сцена видна в Edit без старта игры; изменение Transform, Undo/Redo и
camera/selection отражаются на правильной revision. Cancel drag восстанавливает
исходное состояние. Полный live World editing не требуется: для Play действуют
readonly/snapshot transaction contracts ARCH1c, а не прямые изменения runtime.

## ED6. Игровой цикл разработки

Решение2026-10-29: Editor является launch role приложения; Play/Pause/Stop
управляют owned игровой сессией и не превращают Editor App в Player.
Режимы executable и standalone Player ведутся в
[ENG7b](PLAN.md#eng7b-режимы-запуска-editor-и-player--2026-10-29), runtime HUD —
в [ENG5c.4](PLAN.md#eng5c4-runtime-hud-поверх-alloyargentis--2026-10-29).

- [ ] Связать project/document identity с Build/Play/Stop/Replace; явно различать
  saved source, текущий authoring document и выбранный game artifact.
- [ ] Показывать tasks/progress/cancellation и bounded compiler/runtime diagnostics;
  предоставить переход к файлу/строке ошибки через определённый внешний editor hook.
- [ ] Compile errors сохраняют текущий рабочий artifact; crash/Replace failure не
  закрывают проект и не объявляют неизвестный runtime outcome успешным.
- [ ] Определить границу Edit preview ↔ Play и возврат к документу. Перенос результатов
  Play в authoring остаётся явной операцией, не происходит при обычном Stop.
- [ ] Исключить late publish/resume после Stop/Close/Switch, повторный concurrent
  запуск и удержание старых process/transport resources.

Готовность: полный цикл edit→build→play→replace→stop повторяется в одной сессии;
ошибки дают понятное восстановление. Доступность действий соответствует реальному
состоянию, UI не блокируется compiler/IPC и сохраняет unsaved document.

### ED6a. Консоль диагностики — 2026-10-25

Инфраструктурное дополнение26октября: Core emergency worker и managed JSONL runs
подключены к Editor/runtime. Обычный parent Editor использует постоянный root
между запусками; smoke/runtime хранят runs в своём report scope.
[Настройки и границы](../core/DIAGNOSTICS.md#хранение-jsonl-между-запусками).
Structured console/filter/copy и ED6a.2 raw/health реализованы bounded срезами
2026-10-29; compiler navigation и полная ED8a acceptance ниже остаются открытыми.

Согласовано пользователем, запланировано; общий pipeline —
[CORE6](../core/PLAN.md#core6-структурированная-диагностика--2026-10-25),
engine context/process capture — [ENG8a](PLAN.md#eng8a-диагностика-сессий-и-процессного-вывода--2026-10-25).
Editor — consumer ограниченной истории, без собственного конкурирующего logger.

Первый infrastructure consumer реализован2026-10-25: EditorApplication и runtime
включают общий bounded JSONL pipeline в report directory/diagnostics. View console
и metadata-only export UI добавлены2026-10-29; полные ED6a/ED8a acceptance этим
подключением не закрыты.

- [x] Structured просмотр общей истории Editor/Runtime/Build/IPC/graphics с фильтрами по
  source/level/event/session/generation/runtime role; текстовый поиск, clear view,
  pause/resume auto-scroll и группировка повторов со счётчиком. Очистка View не
  удаляет persistent diagnostics без отдельного явного действия.
- [x] Timestamp, source, EventName/message, exception/stack, bounded properties и
  inspect/copy structured event. Старые session/generation остаются в ограниченной
  истории, фильтры не меняют domain state. Observed sequence gaps, presentation
  eviction/truncation отделены от неизвестного sink health.
- [x] **ED6a.2:** подключить raw stdout/stderr и доступный health/loss Core sinks,
  child delivery и process capture через borrowed snapshots; явно отличать raw
  streams от structured events. Не выдавать observed gaps UI reader за sink loss
  или durability. Сохранить crashed/replaced generation в пределах budgets.

  Проверено2026-10-29: отдельные Structured/RAW stdout/RAW stderr/Health modes;
  borrowed Core parent health и runtime/compiler snapshots. Default16processes,
  4096chars/stream, compiler16completed jobs+active, native64recent leases.
  Raw source/session/generation/role/search filters сохраняют identity; level/event/
  grouping/clear относятся к structured. Health не фильтруется как log. Inspected
  raw/health text frozen до явного «Обновить детали»; polling не делает IO/IPC/SDK wait.
  Parent sink, child delivery lost/unconfirmed, capture eviction/file loss и display
  omissions разделены; child sink health остаётся unavailable. Metadata-only export
  содержит numeric health/correlation с отдельным budget/omission, raw/error text
  исключены. [Точные проверки и scoped native proof](HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
- [ ] Compiler diagnostics дают file/line/column и переход через определённый editor
  hook; классификацию не выводить только из stdout/stderr. Связь с build job/artifact
  сохраняется при compile failure и при последующем успешном Build.
- [x] Diagnostics View<TData>, отдельные Data/ViewModel/IViewStyle и UI-owner factory
  через App/Cobalt; получать bounded batches на UI thread, не вызывать контролы
  из producer callback. Ограничить retained rows/bytes и update budget; сохранить
  focus/selection/scroll при обновлении/filter/theme. При Dispose снять подписки,
  borrowed App-owned pipeline не освобождать. Нужные общие controls принадлежат UI плану.

**Первый проверенный срез2026-10-29:** `UseAetherEditor` подключает console;
меню «Вид → Диагностика», в standalone toolbar — одноимённая кнопка.
`UseAetherEditorDiagnostics(options)` позволяет заранее задать presentation budgets
или подключить console отдельно. Default:512 records,1MiB payload estimate,
8192chars/record,64records/100ms. Это отдельный presentation limit поверх Core ring,
не heap measurement или обещание восстановить JSONL. Group count относится к
retained window; pause отключает только follow-tail, ingestion продолжается.
Clear очищает только View и переводит cursor к текущей границе collector.
Core reader заимствован; дополнительные logger/subscriptions на producers не создаются.
Editor808/808 Debug/Release; native SourceDebug/standard window/Cobalt проверяет
live child history, crash/recovery, grouping/filter/inspect/reopen/export, UI owner
и сохранность dirty document. [Evidence и ограничения](HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).
Остальные native/provider/published profiles и полный flood/slow-UI acceptance
ED6a не выводятся из этого одного native case.

Acceptance: сообщения из child видны во время работы, фильтры/grouping/inspect/copy
и compiler navigation работают; flood/slow UI/повторные открытия панели и crash/
Replace не блокируют команды и не растят память/подписки. Проверки Data и native
UI выделяются отдельно, исторический shell proof не закрывает эту задачу.

### ED6b. Build/Play/Stop из редактора — 2026-10-29

Запланировано по согласованному startup-only выбору Editor/Player. Существующий
legacy demo Build/Replace и Edit preview не закрывают игровой workflow открытого
authoring document. Нужен следующий прикладной срез поверх общих engine contracts:

- [ ] Связать выбранные project/document revision и game artifact. До Play явно
  определить saved/draft policy и захватить согласованный immutable scene snapshot;
  не запускать другой artifact или изменённый источник по завершению stale build.
- [ ] Сохранить документ, selection/history и dirty state в родительском Editor.
  Обычный Play использует runtime copy; автоматического переноса live state назад
  нет. Build failure/cancel, child crash и Stop не уничтожают документ/Undo.
- [ ] Согласовать Edit preview → game Play и обратный путь: fence input/late results,
  stop/drain/reap прежней сессии до создания следующей, ownership/retry при cleanup
  failure. Не ослаблять существующие scene/session/surface limits и identity.
- [ ] Start/Play создаёт owned Runtime child того же exe либо explicit separate
  runtime через существующую factory/supervisor. Ready/Playing/Paused/Stopped,
  progress, ошибки и доступность действий отражают фактический runtime result.
- [ ] Игровой HUD приходит внутри готового runtime frame, Editor рисует собственные
  tools отдельно. Viewport input проходит Editor routing до runtime HUD/game;
  Stop/global shortcuts и focus/capture reset определены без двойного исполнения.
- [ ] Проверить повторные Build/Play/Pause/Resume/Stop, open/close/cancel/crash и
  устаревшие callbacks в source Debug/published Release и затронутом package
  consumer; user close по-прежнему применяет Save/Discard/Cancel policy.

Детальные runtime/Player/HUD contracts не дублируются здесь. Полный ARCH2
предшествует новым ED features; прежняя очередь console/export → names/order →
create/delete → project picker/switch и ED1/ED6 workflow сохраняется. Наличие
этого checklist не означает реализации ED6b или всего ED6.

## ED7. Проверенная расширяемость

- [ ] Уточнить минимальные public contracts из первых потребителей, ограничить
  зависимости расширений и документировать lifetime/thread/error contracts.
- [ ] Зарегистрировать built-in commands/panels/property editors через те же точки,
  что доступны дополнительному модулю; не создавать скрытый отдельный путь для built-ins.
- [ ] Реализовать пример отдельного подключаемого модуля: команда с toolbar/menu
  entry, панель и property editor без изменений shell/центральных switch.
- [ ] Проверить duplicate IDs/order/unsupported property/cancellation/close/failure;
  закрытие project/panel освобождает products и subscriptions ровно у одного owner.
- [ ] Проверить custom UI widget через Element и нужные capability interfaces,
  включая style/input/focus/cleanup на заявленных backend configurations.

Готовность: реальное расширение доказывает достаточность интерфейсов. Регистрация
App-local, dependencies внедрены; не требуется global locator, tree reflection или
переписывание редактора. Dynamic plugin loading этим этапом не обещается.

## ED8. Надёжность и поставка

- [ ] Добавить autosave/recovery отдельно от явного Save; восстановление предлагает
  пользователю выбор и не перезаписывает сохранённую сцену автоматически.
- [ ] Завершить policy внешних изменений файлов, interrupted save, damaged recovery,
  missing assets/toolchain и failed/cancelled project switching.
- [ ] Прогнать повторные Open/Close, panel/tool transitions, build/preview/game crashes,
  cancel и native close при pending tasks; проверить resource/subscription trends.
- [ ] Проверить опубликованный consuming host и независимое решение с NuGet packages
  вне repository, доставку native/fonts/assets, project/scene compatibility и
  diagnostics без development-only путей; package boundary acceptance — ARCH2e.
- [ ] Зафиксировать supported OS/RID/backend и измеряемые responsiveness/resource
  budgets; собственные проверки UI K3/K7/K8/VRAM не заменять коротким smoke.
- [ ] Подготовить короткий пользовательский guide и воспроизводимый acceptance
  project; перечень известных ограничений соответствует фактическим проверкам.

Готовность: основной сценарий пройден на поставляемом приложении, recovery и
failures проверены, данные сохраняются, незавершённая работа наблюдаема. Общая
готовность framework/release не выводится из одного редактора.

### ED8a. Экспорт диагностического отчёта — 2026-10-25

Первый partial export реализован2026-10-29: кнопка «Экспорт без текста» и явный
путь нового JSON файла в console. До512KiB, cooperative5s deadline, до1024recent
metadata records, атомарная публикация без перезаписи существующего файла; accepted
work отменяется/drain при App stop. Report содержит versions/OS, effective
presentation limits, session/generation/build ID allowlist и counters. Message,
exception/stack/caller и произвольные properties исключены; source/event identifiers
сохраняются. Это deliberate metadata-only privacy policy, а не автоматическая
очистка любого пользовательского текста. `partial=true`, omitted count и unavailable
список обязательны. ED6a.2 добавляет numeric parent health/process metadata с
explicit budget omission; raw/error text по-прежнему исключены. Optional raw export
policy, persistent history, child sink health/backend/device/collector configuration
и explicit user cancellation остаются следующей частью ED8a, checklist ниже открыт.
Tests проверяют size/order/privacy, write failure/no-overwrite и cancellation;
native проверяет после runtime crash при неизменном dirty document. Published
app/export и rotated disk history новым запуском не проверены.

- [ ] Явная команда экспорта bounded report: последние structured/raw события с
  session/generation/build correlation, loss/truncation/sink health, версии
  приложения/контрактов, OS/backend/device и effective diagnostic configuration.
  Framework storage/schema определяет CORE6, engine context — ENG8a; здесь только
  пользовательский сбор/выбор назначения и feedback/cancellation.
- [ ] Ограничить размер/время экспорта; исключить nonce/environment/control endpoints
  и произвольное содержимое проекта, определить обработку личных путей/exception
  text. Partial/unavailable данные обозначать явно, unsaved document не изменять.
- [ ] Проверить normal/crashed runtime, rotated/truncated history, write failure и
  cancellation, published app вне checkout. Экспорт после crash использует имеющиеся
  данные и не обещает восстановить потерянный хвост async queue.

## Первый сквозной срез

**Открыть сцену → изменить Transform в inspector → увидеть Edit preview →
Undo/Redo → сохранить → закрыть и повторно открыть.**

Срез собирает минимально необходимые части ED1–ED5, не требует сначала завершить
весь ED4 UI toolkit или ED6–ED8. Он не считается завершённым только по наличию
кнопок и отдельных сервисов. Контрольная сцена имеет устойчивые IDs, editable
Transform и проверяемое визуальное положение объекта.

Первый API срез чтения сцены, atomic Transform edit и Undo/Redo вынесен в
[ARCH1b.1](PLAN.md#arch1b1-локальная-transform-authoring-session--2026-10-23).
Он не закрывает прикладные ED3 tasks: UI ещё не вызывает этот API. После его
headless proof первый независимый Edit preview реализован по ссылке выше.
**ED1a** реализует project description/ProjectSession, Create/Open/Close и
paths/toolchain validation; **ED2a** — scene format/document owner/atomic persistence,
dirty/history checkpoint и close policy API. Оба профиля проверяются вне repo и
сохраняют active content при failed/cancelled подготовке.
ED2b подключает document open/save/close commands и явный Save/Discard/Cancel
к обычному UI. **ED3a** подключил Transform/Undo/Redo/preview и проверил весь первый
сценарий source Debug/published Release; exact evidence в STATUS/HISTORY. Далее
минимальные create/delete общего authoring API/history и их UI commands. Остальные
ED2/ED3 и минимальные ED4/ED5 доводят весь сценарий; очередное
UI-only demo не подменяет его acceptance. Завершать весь движок заранее не нужно.

## Проверки, риски и дальнейшие возможности

Для каждого этапа нужны прямые tests своего поведения, error/cancel/stale paths и
ownership. Сквозные scenarios дополняют их. Source/published native и независимые
package consumers повторяются по затронутым contracts, Debug/Release фиксируются
раздельно. Historical counts сохраняют дату; документационная фиксация плана
не создаёт нового build/test/native результата.

Основные зависимости/риски: первый ARCH1b Transform/history API и engine preview
требуют подключения к project/document и Editor; общие overlays/forms/tree/commands развиваются в UI; известные
physical DPI/K7/device loss/VRAM acceptance остаются открытыми. При изменении
порядка/объёма решение и новые критерии фиксируются явно у владельца.

После первой версии по реальным сценариям развиваются multi-document/docking UX,
asset browser/import pipeline, новые scene/render capabilities, live runtime edits
ARCH1c и MCP ARCH1d, dynamic extensions и automation. Они не становятся скрытыми
условиями первого Transform/save/preview среза.
