# TrueMoon.Aether.Editor: план рабочего приложения

Согласовано с пользователем **2026-10-22** после обсуждения перехода от стенда и
proof of concept к приложению для повседневной работы. Это прикладной подплан
направления Aether, развивающий ENG7; отдельного функционального модуля или новой
сборки этим документом не создаётся. **ED1a project/session и ED2a/b documents реализованы2026-10-23;
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
запускает игру и получает понятную диагностику ошибок. Приложение работает из
опубликованной поставки вне source checkout и не требует редактировать исходники
редактора для выбора игры или добавления панели/команды.

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
ещё не подключены к редактируемому inspector.

## Архитектура и ограничения сложности

### Границы ответственности

| Область | Ответственность |
| --- | --- |
| Editor View | Fluent tree, explicit bindings, визуальное представление состояния, подключение действий и UI lifetime |
| Data/ViewModel | Данные и состояние экрана, validation presentation, availability и вызов прикладных операций; без controls/theme/renderer/native handles |
| `IViewStyle<TView>` / `Theme` | Scoped appearance и общие visual tokens; без изменения документа или выполнения команд |
| Прикладные сервисы Editor | Project/document sessions, сохранение, координация команд/задач, selection, панели и tools |
| Общий authoring API Aether | Валидированные операции над SceneDocument/entities/components, revisions, atomic batches и undo/history по ARCH1b; доступен без UI |
| Runtime Aether | Preview/игровая сессия, simulation/rendering, IPC/frame production; отдельный процесс и собственное состояние |
| Argentis/Alloy и интеграции | Контролы, composition/layout/input/focus, overlays, sessions/rendering/platform; общие UI возможности принадлежат UI плану |

У документа один владелец committed state, revision и history. ViewModel не
поддерживает вторую независимо изменяемую сцену. Поля инспектора могут хранить
draft до commit/cancel, но не обходят authoring API прямыми mutable references.
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

## ED4. Рабочие панели и инспектор

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
- [ ] Проверить опубликованный app вне repository, доставку native/fonts/assets,
  project/scene compatibility и diagnostics без development-only путей.
- [ ] Зафиксировать supported OS/RID/backend и измеряемые responsiveness/resource
  budgets; собственные проверки UI K3/K7/K8/VRAM не заменять коротким smoke.
- [ ] Подготовить короткий пользовательский guide и воспроизводимый acceptance
  project; перечень известных ограничений соответствует фактическим проверкам.

Готовность: основной сценарий пройден на поставляемом приложении, recovery и
failures проверены, данные сохраняются, незавершённая работа наблюдаема. Общая
готовность framework/release не выводится из одного редактора.

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
к обычному UI. Следующий срез — Transform/Undo/Redo/preview. Остальные
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
