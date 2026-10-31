# TrueMoon.Aether.Editor

Подключаемая библиотека/NuGet-пакет редактора, net10.0. Публичный вход —
`UseAetherEditor(configureEngine, configureEditor)`; optional стандартное окно —
`UseAetherEditorWindow(fonts, configureWindow)`. [API, ownership и quickstart](PACKAGE.md).
Main/CLI/App composition находятся в тонком
[AetherEditor](../Examples/AetherEditor/README.md); directed smoke checks и явный
same-exe runtime profile — в [AetherEditorHost](../ManualTests/AetherEditorHost/README.md).
Host предоставляет
fonts и runtime configuration; пакет не создаёт вложенный App и не открывает
native resources при регистрации. ARCH2c предоставляет explicit runtime launch,
bootstrap и pair identity. [Examples guide](../Examples/README.md) описывает owners
ресурсов и source/published headless verifier; ARCH2e independent NuGet/native
проверен в bounded Windows profile. Новый console consumer пока проверен отдельно
в source Debug; его package/published acceptance открыт. Local pack не означает готовность поставки.

[План развития рабочего приложения](../docs/engine/EDITOR_PLAN.md) задаёт ED1–ED8:
project/documents/editing/preview/workflow/extensions/reliability. Первый сквозной
срез — открыть сцену, изменить Transform, увидеть Edit preview, Undo/Redo,
сохранить и повторно открыть. ED1a project/session и ED2a document/persistence API
реализованы; ED2b подключает документы к обычному UI. Первый Transform inspector,
Undo/Redo и independent Edit preview подключены к общему authoring API.

## Запуск открытой сцены — ED6b.1

В штатном UseAetherEditor графе ▶ запускает текущую authoring-сцену как отдельную
игровую копию. Save не обязателен: захватывается текущий committed in-memory document,
включая несохранённые изменения, но не неприменённые поля инспектора. Сначала Apply/
Cancel для draft/conflict. Документ, dirty checkpoint, selection и Undo/Redo остаются
в родительском Editor; изменения игры обратно не переносятся.

Каждый Start/Run заново собирает выбранный game csproj. Сначала проектируется immutable
scene snapshot, затем Edit Preview stop/drain/reap, установка context, Build и запуск
точного candidate/context version. Обычный ▶ включает Play после actual first-present;
«Запустить на паузе» оставляет Pause. Доступны Pause/Resume/Step/Stop. Во время подготовки
и игры file/project actions и authoring edits закрыты общей admission. Cancel ждёт
фактического завершения принятой работы; failed startup очищает owned runtime, failed
cleanup оставляет Stop retry. После Stop можно продолжить редактирование и отдельно
включить Edit Preview. Build failure и child crash не уничтожают authoring history.

EditorScenePlayback — App-owned coordinator; low-level GameEditor и document API
сохраняют отдельные contracts. Компиляционный SourceRevision здесь — монотонный номер
запроса. ED6b.2a дополнительно проверяет наблюдаемые MSBuild inputs до/после Build
и перед Start: содержимое evaluated files и HintPath references, настройки и
preprocessed imports. Изменённые входы запрещают запуск и требуют нового Build.
Профиль: один net10.0 SDK project без PackageReference/ProjectReference. Witness
хранится в памяти; повторное открытие artifact не восстанавливает доверенный witness.
Штатный scene Start также включает ED6b.2b.1 CaptureCompilation: после SDK preparation
компилятор, source/generated/reference/config inputs копируются в owned workspace;
отдельный Csc собирает из этих bytes без повторного MSBuild. После capture edits
оригиналов не влияют на compilation, но прежние stale checks всё равно требуют
актуальные исходники до Start. Source paths/diagnostics сохраняются, staging очищается.
Это ограниченный C# library profile; opaque compiler inputs/custom analyzers и
resource/ruleset/signing profiles отклоняются. MSBuild preparation, installed dotnet
runtime и ambient environment остаются trusted; transient edits во время preparation
и произвольный IO tasks/generators ещё не изолированы. Полный source closure,
переиспользование artifacts для изменяемых исходников, document-aware Replace, gameplay
input/HUD/Player расширения остаются следующими срезами ED6b. Предел projected runtime
scene64entities и текущий supported viewport profile сохраняются.

Первый основной экран редактора: стандартное borderless оформление Alloy,
компактный заголовок с меню и симуляцией, дерево сущностей слева, живой3D viewport в центре,
динамический инспектор компонентов справа и строка состояния снизу. Desktop host WinExe
не открывает дополнительную консоль. Dark/Light
переключаются через «Вид → Сменить тему». Editor — library, `IsPackable=true`;
«Вид → Диагностика» открывает bounded structured console: поиск, source/level/
event/session/generation/role фильтры, grouping, inspect/copy и pause follow-tail.
Переключатель режима добавляет RAW stdout/RAW stderr и Health: bounded runtime/build
snapshots, parent Core sinks, child delivery и capture/file counters. Уровень события
не определяется по stderr. «Обновить детали» явно заменяет зафиксированный снимок.
«Экспорт без текста» пишет partial metadata/health JSON без raw/message/exception/
stack/error text и произвольных properties. «История запусков» добавляет bounded
JSONL историю настроенного parent logger; остальные active runs могут быть пропущены.
«Включить текст» явно разрешает messages, exceptions/stacks, raw stdout/stderr и
compiler text/source paths. Такой отчёт может содержать личные пути и данные:
проверьте его перед передачей. Это не автоматическая анонимизация. Служебные
bootstrap/environment/control fields и произвольные properties не экспортируются;
строки с известными control-field ключами очищаются. Host может дополнительно задать
точные sensitive literals через `EditorDiagnosticExportOptions.RedactedValues`.
Отчёт включает доступные scalar settings, observed backend/viewport и counters;
child sink health и last selected runtime device берутся из последнего полученного
снимка с временем наблюдения. После crash он может быть устаревшим; отсутствие
снимка/device, в том числе CPU-only запуск, указано явно. Тексты ошибок child sinks
не передаются; persistent compiler history недоступна. Presentation device приходит
из optional cached capability поверхности; exporter не вызывает GPU API.
Путь задаёт новый файл; существующие файлы не перезаписываются. Default лимиты —
512KiB/cooperative5s; «Отменить экспорт» запрашивает отмену, повтор доступен после
завершения принятой операции. App stop также отменяет и drain-ит работу. Незавершённая запись удаляется.
ED8a проверен в scoped source/NuGet published window/GPU и headless Game профилях;
расширенная backend/provider matrix и общий release остаются открытыми. Checks — в
[статусе](../docs/engine/STATUS.md). Движок остаётся в сборке `TrueMoon.Aether`. Редактор и его tests
относятся к модулю Aether; прежние имена `TrueMoon.Editor`/`TrueMoon.Editor.Tests`
заменены на `TrueMoon.Aether.Editor`/`TrueMoon.Aether.Editor.Tests`.

JSONL диагностика обычного Editor хранится в
`%TEMP%/TrueMoon/Diagnostics/Editor/<run-guid>`, с общими лимитами закрытых
запусков8/32MiB/7суток. Smoke сохраняет логи внутри своего report directory,
Runtime — в `report/diagnostics/runtime/<run-guid>` с теми же лимитами на корень.
Active runs и неизвестные каталоги защищены; последний созданный closed run
сохраняется даже сверх лимита. Общие reports, build artifacts и raw stdout/stderr
имеют отдельные правила хранения.
[Настройки, аварийный канал и ownership](../docs/core/DIAGNOSTICS.md).

```powershell
dotnet run --project ManualTests/AetherEditorHost
```

Без аргументов открывается пустая рабочая область с отключёнными игровыми
командами. Приложение не ищет demo или repository root. Для прежней демонстрации
явно укажите `--project ManualTests/Game.Logic/Game.Logic.csproj`.
Нажмите **«Проект → Собрать проект»**, затем **▶**: приложение собирает
внешнюю Game.Logic, запускает отдельный runtime и включает симуляцию комнаты.
«Симуляция → Запустить на паузе» сохраняет отдельный запуск в Pause.
Затем доступны ▶/Пауза/Шаг/Стоп. Новая сборка во время работы готовит candidate;
«Применить сборку» использует существующий checkpoint/Replace и сохраняет состояние.
Ошибки возвращаются в инспектор; занятые/недоступные действия отключены.

Для существующего игрового csproj сохранён explicit legacy запуск:

```powershell
dotnet run --project ManualTests/AetherEditorHost -- `
  --project E:/games/MyGame/MyGame.csproj --assembly MyGame.dll --entry MyGame.Session
```

Выбранный тип должен реализовывать Aether game/render/checkpoint contracts,
`Runtime.IGameSessionEntry.Create(IServiceResolver?)` и
принимать демонстрационную SceneSnapshot. Этот legacy путь сохраняет прежнюю demo
сцену; обычный `.aetherproj` открывается с пустой сценой, доступной для New/Open.

## Обозреватель сущностей

Слева показана иерархия сцены с геометрическими иконками сущности, отображаемого
объекта и интерактивного объекта. Стрелка сворачивает/раскрывает ветвь; выбор строки
обновляет общий inspector. Up/Down/Home/End выбирают строки, Left/Right сворачивают
и раскрывают ветви, Enter/Space переключают раскрытие. Выбранный узел раскрывает
предков и прокручивается в видимую область.

Для смены родителя перетащите строку на другую сущность. Перенос на пустую область
дерева или кнопка **«В корень»** убирают родителя. Ctrl+Right помещает узел под
предыдущего соседа, Ctrl+Left выносит на уровень выше. Циклы и перенос в самого
себя отклоняются. Сохраняется **локальный Transform**: мировое положение может
измениться вслед за новым родителем. Это одна операция общего authoring API с
Undo/Redo, dirty state, Save/reopen и обновлением Edit Preview.

Перенос доступен для открытого authoring-документа, когда нет операции с файлом,
игрового runtime и незавершённой правки свойств/conflict. Устаревший жест после
смены документа отклоняется; повторите его на обновлённом дереве. Ошибки можно
выделить и скопировать. Legacy game scene пока доступна только для просмотра.

F2 открывает имя в строке; Enter сохраняет, Esc отменяет. Потеря фокуса, смена
selection/документа/revision отменяет незавершённое имя. Пустое имя возвращает label
из ID. Имена могут совпадать; ID не меняется. Ctrl+Up/Down переставляет соседей;
верхняя/нижняя четверть строки при drop задаёт before/after, центр меняет родителя.

После **New** кнопки обозревателя создают объект с Transform или видимый куб.
Insert создаёт объект в корне, Ctrl+Insert — под выбранным; Ctrl+D дублирует ветвь,
Delete удаляет выбранную ветвь с потомками. Это обратимые операции общей history.
Копия получает новые entity IDs, сохраняет inheritance/patches/имена и порядок детей;
её корень появляется после исходного. Удаление сохраняет definition/schema catalogs.
Правый клик, Shift+F10 или кнопка действий открывают те же команды, включая создание
дочернего куба. Созданная/скопированная сущность сразу выбрана для инспекции.
Ctrl+F переводит фокус в поиск по имени или ID; Enter/«Найти» циклически выбирает
совпадение и раскрывает предков. Поиск не фильтрует дерево и не меняет документ.

Реализован bounded [ED4c.1–3 profile](../docs/engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Общие limits authoring session применяются и к expanded delete/duplicate; ошибка
не меняет документ/history. Множественный выбор, autoscroll/autoexpand при drag,
typed entity references и управление каталогами остаются в плане.
Общий `TreeView`/`ITreeView` находится в Argentis,
прикладные View/Data/Style и typed factory — в Editor, без зависимости модели от UI.

## Компактная верхняя панель

Viewport занимает всю клиентскую область, включая пространство под заголовком и
нижней строкой состояния. Постоянная полоса пути/имени сцены убрана; островки
размещены ниже caption, сохраняя рабочую область. Заголовок имеет слабую подложку,
которая за160мс становится матовой при наведении; фокус/меню удерживают её в активном
окне. Текст, иконки и размеры вьюпорта не меняются. `Theme.PanelBackdrop = null`
включает непрозрачный фон. Обычный режим рамки других приложений сохранён.
Имя сцены, `*` и activity показаны рядом с проектом; Tooltip содержит полный путь
с переносом строк. Header/панели остаются частью того же UI, renderer и input session.

Слева находится знак TrueMoon, затем постоянная кнопка меню без обычной рамки.
Её точки плавно поворачиваются **⋮ → ⋯** вместе с раскрытием за180мс. Наведение
показывает «Файл / Правка / Вид / Проект / Симуляция»; заголовок проекта сдвигается,
знак и кнопки симуляции остаются на месте. После ухода мыши есть задержка240мс.
Одно нажатие на кнопку раскрывает панель и сразу открывает «Файл» после анимации;
повторное закрывает. Enter/Space на кнопке работают так же. **F10** открывает
клавиатурную навигацию, стрелки выбирают меню и пункты, Enter выполняет действие.
Esc, click снаружи или потеря фокуса закрывают меню. Tab возвращает обычную навигацию.
Отмена во время раскрытия исключает последующее открытие «Файл».
Пункты меню и иконки используют общее состояние доступности и те же команды.
Выпадающие меню имеют матовый полупрозрачный фон: содержимое окна за ними
размывается, текст и иконки остаются чёткими. Dark/Light выбирают tint через
`Theme.MenuBackdrop`; null отключает эффект. Общий механизм `Element.Backdrop`
доступен другим панелям через стили/Fluent, без зависимости ViewModel от renderer.
Наведение на ▶/паузу/стоп показывает подсказку, в том числе для отключённой кнопки.

«Файл» содержит New/Open/Save/SaveAs/Close и Выход. Open/SaveAs/первое Save открывают
компактную панель ввода пути над сценой. «Продолжить» подтверждает операцию,
«Отмена»/Esc/внешний click закрывают ввод без IO; draft сохраняется.
Native file picker и project picker будут отдельным этапом.
Несохранённые изменения по-прежнему требуют Save/Discard/Cancel, включая Выход
из меню. «Правка» использует общую authoring Undo/Redo history. Независимый Edit
Preview перенесён в строку состояния: кнопки заголовка управляют игровой симуляцией.

Header View/Style создаёт UI-owner `IFactory<EditorHeaderView, EditorHeaderArguments>`
из `UseAetherEditorHeader()`. Окно владеет View; модели и стиль заимствуются из App.
Общие `MenuBar`, `OverlayHost`, `IUiCommand` и frame-driven `ScalarTransition`
находятся в Argentis/Alloy. Стандартные minimize/maximize/close, caption drag и
Snap metadata сохраняются через default window frame.

## Граница главного окна

Граница главного окна использует системный акцент Windows. При потере фокуса RGB
плавно затемняется до60% за160мс, при возвращении восстанавливается. Если цвет
недоступен, используется текущий Theme.Border. Смена системного оформления обновляет
цвет без restart; явный BorderColor в стиле имеет приоритет.

## Строка состояния ED4b

Нижняя панель высотой28логических единиц использует текст12 и геометрические
иконки14. Слева — краткое состояние редактора; при отменяемой операции рядом
появляется «Отмена». Справа — число объектов, Edit Preview и кнопка подробностей.

Статус-бар накладывается на полную область viewport: в покое фон почти прозрачен,
при наведении или фокусе проявляется матовая подложка за160мс. Ошибки, текущая
операция и ожидание решения остаются на матовом фоне без наведения; открытые
подробности тоже удерживают подложку. Иконки/текст не исчезают, геометрия сцены
не меняется. Смена темы сохраняет controls/focus; null PanelBackdrop даёт opaque фон.
Tick/PID, текущая операция, ревизия preview и полная выбираемая диагностика
открываются над панелью в матовом popup. Ошибка имеет отдельную иконку и цвет.

Нажмите Edit Preview для запуска/остановки просмотра committed сцены. Эти действия
используют прежний preview coordinator; из production inspector они убраны.
При уменьшении окна второстепенные сегменты скрываются, а их данные и команды
остаются доступны через подробности. Esc, Tab, внешний click и потеря фокуса
закрывают popup; при смене темы существующие элементы и выделение сохраняются.

`UseAetherEditorStatusBar()` регистрирует data/style и типизированные фабрики через
App/Cobalt. Окно владеет View, OverlayHost — popup; общие модели заимствуются.
Общий layout `StatusBar`/`IStatusBar` находится в Argentis, без зависимости от Editor.

## Проект и сессия ED1a

Обычный запуск: `AetherEditorHost.exe --project E:/games/MyGame/MyGame.aetherproj`.
Проверка без окна: `AetherEditorHost.exe --check-project E:/games/MyGame/MyGame.aetherproj`.
Второй вариант выводит JSON с project/SDK identity и результатом закрытия сессии;
ошибка завершает процесс с ненулевым кодом. SDK preflight запускает только
`dotnet --version` из каталога игры, учитывает `global.json` и требует SDK10+.
MSBuild, compiler и runtime при открытии не запускаются; совместимость самой игры
проверяется отдельной сборкой.

Пример `.aetherproj`:

```json
{
  "formatVersion": 1,
  "id": "bd4bb877-de43-4819-a422-df8259e38daf",
  "name": "My Game",
  "gameProject": "Game/MyGame.csproj",
  "entryAssembly": "MyGame.dll",
  "entryType": "MyGame.Session",
  "scenesRoot": "Scenes",
  "contentRoot": "Content"
}
```

UTF8, optional BOM, максимум64KiB вместе с BOM. Неизвестные/повторные поля и
неподдержанная версия отклоняются. ID устойчивый и непустой; Name≤128,
relative paths/entryType≤512, entryAssembly≤128. Пути разрешаются относительно
manifest directory, нормализуются и не могут лексически выйти из неё; csproj и
оба корня должны существовать. Это trusted-local profile, не sandbox для links.
Scene files и project/user preferences имеют отдельные владельцы; recent projects,
theme/layout и machine-specific settings в manifest не сохраняются.

`UseAetherEditorProjects()` подключает API без окна/engine/native startup.
`EditorProjects.OpenAsync/CreateAsync/SwitchAsync/CloseAsync` управляют одним active project;
`IFactory<ProjectSession, ProjectSessionArguments>` создаёт owned products.
Create сначала проверяет существующий игровой проект/корни/SDK и атомарно
публикует новый manifest без overwrite. Шаблоны игры и project wizard ещё не введены.

```csharp
await using var app = App.Build(context => context.UseAetherEditorProjects());
await app.StartAsync();
var projects = app.Services.Resolve<EditorProjects>()!;
var session = await projects.OpenAsync(@"E:\games\MyGame\MyGame.aetherproj");
var request = session.CreateBuildRequest(sourceRevision: 0);
await projects.CloseAsync();
```

Для примера нужны `using TrueMoon.Aether.Editor;`,
`using TrueMoon.Aether.Editor.Projects;` и `using TrueMoon.Dependencies;`.
Session заимствуется потребителями; owner закрывает её при successful switch,
Close и App shutdown. Ошибка/cancel подготовки сохраняют прежнюю active session.
Concurrent Open/Create отклоняется; Close отменяет и ждёт accepted work, исключая
late publish. После Close сервис можно использовать снова; App shutdown закрывает
admission окончательно. Если отмена пришла после atomic manifest write, валидный
новый файл остаётся, но cancelled candidate не становится active.
Для тестов/интеграций store/toolchain/factory можно зарегистрировать до UseX;
они заимствуются coordinator и принадлежат DI согласно их регистрации.

ED1b.1 добавляет явную document policy для переключения и закрытия проекта.
Host сначала удерживает admission своих команд и останавливает/drains preview и game;
сам EditorProjects не владеет runtime. При подключённых documents:

```csharp
// using TrueMoon.Aether.Editor.Documents;
await projects.SwitchAsync(nextManifest, DocumentCloseChoice.Save,
    saveCurrentAs: "Untitled.aetherscene", cancellationToken: cancellationToken);
await projects.CloseAsync(DocumentCloseChoice.Discard);
```

Switch сначала валидирует manifest/SDK и создаёт candidate; только затем сохраняет
прежний документ либо применяет Discard при commit. Invalid candidate, failed Save
и cancellation до publication сохраняют прежнюю session и Undo/Redo. Решение относится
к точному document object: edit/replacement во время подготовки требует нового решения,
даже при совпадающих ID/revision. Pending document IO не прерывается ради Discard.
Save использует прежний project root и наблюдённый fingerprint; untitled требует
saveCurrentAs. Успешная запись перед поздней отменой оставляет честный saved checkpoint.
Cancel отменяет Task только при несохранённом документе; clean/empty project допускает переход.
Старый OpenAsync и CloseAsync без policy сохраняют strict clean-only поведение.

После publication отмена не откатывает новый проект. Ошибка disposal прежнего owner
может завершить Switch с ошибкой при уже новом Active: owner удерживается для Close retry,
дальнейший Open/Create/Switch запрещён до cleanup. Explicit Close ждёт подготовки/IO,
исключает late publish и после document commit завершает owned disposal без detach.
App shutdown остаётся terminal forced discard/drain.

ED1b.2 подключает эти transitions к стандартному меню «Проект»: «Открыть проект по пути»
показывает retained prompt для полного пути .aetherproj, «Закрыть проект» возвращает
пустую workspace. Project path, scene path и first-save path хранятся отдельно;
Escape отменяет ввод, Enter подтверждает. Это ввод явного пути; OS file picker,
recent projects и project wizard остаются следующими расширениями.

Project/document действия используют общую tracked queue и Save/Discard/Cancel.
Project commands доступны при stopped/idle game/compiler; сначала остановите игру.
Активный Edit Preview fences/drains после candidate validation и подтверждения решения.
Invalid manifest/SDK и Cancel оставляют прежние документ/history/selection и runtime context.
Failed Save/drain показывают ошибку с возможностью повторить действие. Switch не закрывает
весь runtime command service: после открытия другого проекта Build снова доступен.

До возврата command admission binding устанавливает GameEditor context нового проекта,
сбрасывая candidate прежней сборки. Close очищает context. Workspace, selection,
inspector/hierarchy и caption синхронизируются на UI owner; свежие guards не позволяют
вызвать game action из ещё старой presentation. Failed binding блокирует game/document
actions и сохраняет actual project identity; Close/reopen остаются recovery path.
Custom compositions без supplied project binding сохраняют прежние document commands,
project menu actions в них disabled. UseAetherEditor регистрирует binding через App/Cobalt;
новых scopes нет. Штатный граф дополнительно предоставляет document-aware Build/Play
через EditorScenePlayback по ED6b.1 выше. Custom graph без этого coordinator сохраняет
прежнюю блокировку game actions при открытом authoring document.

Открытие `.aetherproj` пока выбирает project/build identity, но не загружает сцену:
ED2a предоставляет document loading/save/reopen API, ED2b — панель документов и
Save/Discard/Cancel. Выбор другого проекта через shared path prompt реализован ED1b.2;
OS picker, recent projects и полный игровой workflow развиваются в ED1/ED4/ED6.
Edit Preview открытого документа доступен в строке состояния.

## Документ сцены ED2a

ARCH2a2026-10-27 перенёс SceneFileCodec/ISceneDocumentStore/JsonSceneDocumentStore и
SceneFile/SceneFileStamp/SceneFileConflictException в TrueMoon.Aether.Scenes.
UseAetherEditorDocuments подключает общую UseAetherScenePersistence и сохраняет
existing codec/store overrides; SceneDocumentSession/commands/close policy остаются
в Editor. FormatVersion1 не изменён. [Карта границ и migration](../docs/engine/PACKAGE_BOUNDARIES.md).

Подключение без окна, compiler и game activation:

```csharp
await using var app = App.Build(context =>
{
    context.UseAetherAuthoring();
    context.UseAetherEditorDocuments();
    context.UseAetherEditorProjects();
});
await app.StartAsync();
var projects = app.Services.Resolve<EditorProjects>()!; // composition root
var project = await projects.OpenAsync(@"E:\games\MyGame\MyGame.aetherproj");
var documents = project.Documents!; // borrowed; owned by ProjectSession
await documents.LoadAsync("Main.aetherscene"); // relative to scenesRoot
var state = documents.Read();
await documents.SaveAsync();
if (await documents.CloseAsync(DocumentCloseChoice.Cancel))
    await projects.CloseAsync();
```

Дополнительно нужны `using TrueMoon.Aether;` и
`using TrueMoon.Aether.Editor.Documents;`; для общих file APIs —
`using TrueMoon.Aether.Scenes;`. Порядок UseX существенен: authoring до
documents, documents до projects. Регистрация только UseAetherEditorProjects
сохраняет metadata-only поведение. Store и typed
`IFactory<SceneDocumentSession, ResolvedProject>` можно заменить до UseDocuments;
products принадлежат вызывающему коду либо ProjectSession, borrowed aliases не
становятся владельцами. Document session владеет engine authoring session,
заимствует store/factory и не освобождает их самостоятельно.

Минимальный `.aetherscene`:

```json
{
  "formatVersion": 1,
  "id": "15d92d9a-65fa-43e1-ad84-17a489e0e26a",
  "revision": 0,
  "entities": [],
  "definitions": [],
  "schemas": []
}
```

Сохраняется исходный authoring document со stable IDs, parent/base links,
definition/instance patches, masks/restores и schemas. Resolved scene, runtime
и Undo history в файл не входят. Значения имеют явный kind: `null`, `boolean`,
`integer` (Int64), `number` (finite Double), `string`; отсутствие свойства
отличается от explicit null/default. UTF8 optional BOM,≤4MiB включая BOM,
JSON depth32, строгие required/unknown/duplicate/case-sensitive fields. Reader принимает
v1/v2; writer выбирает v2 при наличии instance Name/SiblingOrder, иначе v1. Старые
сцены читаются без пересохранения; неизвестные версии отклоняются. Полные collection limits и
[контракт формата](../docs/engine/EDITOR_PLAN.md#ed2a-documentpersistence-api--2026-10-23).

NewAsync создаёт unsaved document; LoadAsync готовит/валидирует candidate до
смены текущего. Apply/Undo/Redo вызывают общий engine API; committed state и
history имеют одного владельца. Dirty сравнивает ContentRevision с saved
checkpoint, поэтому Undo к сохранённому содержимому снимает dirty при новой
Document.Revision. Save пишет захваченный immutable snapshot; следующие edits
разрешены и остаются dirty. Failed/cancelled SaveAs сохраняет path/checkpoint.

Paths разрешаются внутри scenesRoot и требуют существующего родительского
каталога. Store использует временный файл рядом и atomic rename/replace;
SaveAs в другой путь — create-only. Save текущего файла проверяет SHA256 ранее
наблюдавшихся bytes, включая whitespace/BOM: внешний change/delete даёт
SceneFileConflictException, следует reopen либо выбрать новый SaveAs path.
Постоянный пустой `.write-lock` sidecar согласует cooperating writers через
exclusive handle; после закрытия handle ownership не остаётся. Profile trusted
local/optimistic: произвольный внешний rename между проверкой и replace,
filesystem links и power-loss durability не покрываются этим контрактом.

CloseAsync принимает Save/Discard/Cancel (default Cancel), optional SaveAs path
для untitled документа. Cancel сохраняет dirty document; failed Save также
оставляет его доступным. Close/Dispose действительно ждут accepted IO даже при
игнорировании cancellation; cleanup fault удерживает products для retry.
Project Close/Switch отклоняет dirty/busy document. Обычный переход сначала
разрешает document policy; terminal App/owner Dispose явно выполняет forced
discard/drain и не является пользовательским Save dialog.

Проверка чтения/валидации без окна, сборки и записи файла:

```powershell
AetherEditorHost.exe --project E:/games/MyGame/MyGame.aetherproj --check-scene Main.aetherscene
```

JSON содержит identity/revision/catalog counts, roundtrip bytes, dirty и результаты
закрытия owners. Требуется явный `.aetherproj`; путь сцены relative к scenesRoot
либо абсолютный внутри него. Unsupported version/traversal/malformed input дают
ненулевой exit. Этот headless preflight остаётся read-only.

## Работа с документом в UI ED2b

Запустите редактор с явным `--project MyGame.aetherproj`. Действия доступны в меню «Файл»:

- **Новая** создаёт пустую несохранённую сцену с новым stable ID.
- **Открыть** читает указанный .aetherscene; путь абсолютный внутри scenesRoot
  либо относительный к нему. Корень проекта показан под строкой состояния документа.
- **Сохранить** пишет выбранный файл; для новой сцены сначала запрашивает путь.
- **Сохранить как** публикует новый файл без overwrite другого существующего файла.
- **Закрыть** освобождает документ, сохраняя проект и окно.

Ctrl+S, Ctrl+Shift+S и Ctrl+W в редакторе вызывают те же операции, что кнопки;
кнопки доступны через Tab/Enter. Ctrl+S/W работают и без фокуса после клика по сцене.
Поле пути в contextual панели — draft следующего Open/SaveAs;
завершение фоновой операции не заменяет введённый текст. Выбранный файл и dirty
маркер `*` показаны в заголовке. Errors сохраняют документ/путь и допускают retry;
после внешнего изменения файла используйте повторное Open либо другой SaveAs.

При New/Open/Close/выходе с unsaved content появляется contextual блок над сценой
**Сохранить / Не сохранять / Отмена**. Для untitled документа у него отдельное
поле пути сохранения. Без выбора transition не завершается; Esc/Отмена сохраняют
текущий документ. Ошибка Save при выходе оставляет окно открытым. Фокус сначала
переходит на безопасную «Отмена», затем возвращается в сохранившийся control либо
доступный control после actual IO drain. Это retained View приложения;
общий modal stack в UI-библиотеку не добавлен. Forced App shutdown отменяет решения и
drains работу, затем явно discard; пользовательский native close остаётся vetoable.

Список и readonly inspector показывают raw authoring IDs/parent/definition и
resolved component properties, включая неизвестные schemas; runtime 64-entity
projection для этого не используется. Строки показаны с кавычками и escaping
control characters, integer/number/null сохраняют смысл. Сохранение/смена темы
не перестраивают shell, viewport, поле пути или неизменившиеся entity controls.

Операции документов выполняются при stopped/idle runtime/compiler либо в Edit preview.
Во время IO/decision игровые действия заблокированы. Открытый authoring document
подключён к Build/Play в штатном графе через ED6b.1 выше; custom legacy graphs
сохраняют прежнюю блокировку. Project path prompt реализован; OS file browser ещё запланирован;
explicit legacy .csproj сохраняет прежний игровой workflow.

Directed acceptance: `--project fixture.aetherproj --smoke fresh-directory --smoke-documents`.
Только для disposable fixture с Rich.aetherscene/Unsupported.aetherscene; сценарий
пишет новые scene files в scenesRoot, проверяет реальные controls/shortcuts/native
close, Save/reopen/conflict/recovery и сохраняет PNG/JSON. Обычный запуск такой
сценарий не выполняет.

## Динамический инспектор компонентов

Обозреватель слева и инспектор справа размещены матовыми островками поверх сцены.
Viewport постоянно занимает всю рабочую область: выбор сущности не меняет его
размеры. Панели имеют отступ16, скругление8, границу и мягкую тень; высота следует
содержимому до доступного предела, длинное содержимое прокручивается.
`Theme.PanelBackdrop` задаёт материал обеих тем; null включает непрозрачный Surface.
Инспектор появляется при выборе сущности с fade/slide за160мс и скрывается без выбора.
Островки пока закреплены по сторонам; свободное перемещение/resize — отдельный этап.
Esc в обозревателе снимает выбор (во время drag сначала отменяет перенос).
Apply/Cancel/Undo/Redo/Add представлены квадратными иконками в одной строке,
удаление — возле заголовка компонента, сброс — возле свойства. Наведите мышь на
иконку на500мс для Tooltip с названием действия и сочетанием клавиш; у недоступной
команды подсказка также работает. Названия вариантов добавления остаются текстом.

Выберите сущность в дереве: инспектор показывает только её эффективные компоненты
и свойства точной версии схемы. У сущности без Transform соответствующих полей нет.
Секции можно сворачивать. Boolean редактируется переключателем, Integer — полем
Int64, Number — полем с десятичной точкой, String — многострочным текстом. Для
nullable-свойств переключатель `null` отличает отсутствие значения от пустой строки.
Неизвестный component type показывается по ID; его scalar-свойства доступны по схеме.

Под полем указан источник: локальное значение, наследуемое определение или default
схемы. «Сброс» подготавливает возврат к inherited/default; «Применить» подтверждает
все изменённые поля одним действием истории. Для локально добавленного компонента
сброс убирает также явно заданное начальное значение свойства. Нетронутый текст
сохраняется без перезаписи исходных переводов строк.

«Добавить компонент» предлагает схемы документа с учётом multiplicity и зависимостей.
Новый occurrence получает собственный ID. «Удалить» убирает локальный компонент или
маскирует наследуемый; instance-переопределения удалённого occurrence очищаются.
«Восстановить» возвращает наследуемый компонент; Undo возвращает весь прежний patch.
Удаление обязательной зависимости отклоняется без частичного изменения сцены.
Операции входят в общую историю со свойствами и переносами дерева, сохраняются в
scene JSON v1 и доступны headless через `ISceneAuthoringSession.EditComponents`.

Во время draft/conflict структурные действия отключены. IO, runtime и смена
документа используют общие admission gates. Темы и обычное обновление сохраняют
поля, выделение, фокус и прокрутку; добавление/удаление меняет затронутые секции.
Это редактирование authoring-компонентов. Live legacy runtime доступен для чтения;
runtime World/storage/scheduler и custom property-editor providers — отдельные этапы.

Directed acceptance: `--project fixture.aetherproj --smoke fresh-directory --smoke-inspector`.
Только disposable fixture с `Inspector.aetherscene`: scalar/null/multiline ввод,
component actions, зависимость, reset, theme retention, shared history, Save/reopen
и фактически показанный Edit Preview, PNG/JSON и cleanup дочернего процесса.

## Transform, Undo/Redo и Edit preview

Откройте сцену с built-in v1 Transform и выберите объект слева. Inspector показывает
девять полей: XYZ, Pitch/Yaw/Roll в радианах и Scale XYZ. Формат чисел — с точкой,
допускается экспонента. NaN/Infinity, значения вне Float и неположительный либо
округляющийся в ноль масштаб отклоняются рядом с полем. Engine дополнительно
проверяет итоговую сцену/мировые матрицы; отказ сохраняет черновик и документ.

**Применить / Ctrl+Enter** подтверждает изменённые поля одной atomic history transaction;
**Отменить / Esc** возвращает последнюю committed версию. Черновик сохраняется при
обновлении экрана и смене темы. Выбор другого объекта отбрасывает неподтверждённый
черновик; изменение той же сцены извне сохраняет его, показывает conflict и требует
явной отмены/повтора. Undo/Redo недоступны, пока есть черновик. Кнопки Undo/Redo и
Ctrl+Z / Ctrl+Shift+Z / Ctrl+Y вне полей вызывают общую engine history; внутри
TextBox/RichTextEditor Ctrl+Z не перехватывается как команда сцены. Dirty учитывает saved checkpoint.

**Edit Preview → Preview** в строке состояния включает независимый просмотр
committed сцены без Build/Play/Game.Logic.
После Apply/Undo/Redo он обновляется автоматически. Статус различает ревизию
документа и последнего реально показанного кадра. Один запрос выполняется, из
ожидающих сохраняется только последний; набор текста не запускает preview.
**Стоп** отменяет обновление и ждёт drain/reap. Open/New/Close/Exit останавливают
preview до смены документа; Cancel решения оставляет прежнюю сцену и preview.
Ошибка preview не откатывает правку и не удаляет history; повтор — кнопкой Preview.

Текущий профиль: Windows x64, OpenGL UI/Vulkan child, фиксированная камера,
surface640×360, до64entities и новый child при обновлении ревизии. Лимит относится
к preview: большие authoring документы не обрезаются и доступны для сохранения.
New создаёт пустую сцену; её можно наполнить через команды обозревателя.
Live runtime edits, gizmos и camera navigation остаются следующими задачами.

Directed acceptance: `--project fixture.aetherproj --smoke fresh-directory --smoke-transform`.
Только для disposable fixture с Transform.aetherscene: ввод/валидация/темы,
Apply/Preview/Undo/Redo, pixel checks, Save/Close/reopen и child/resource cleanup.

## Границы первого экрана

- Инспектор редактирует scalar-свойства и состав компонентов authoring документа.
  Legacy runtime остаётся readonly; live runtime edit transaction ещё нужна.
- Дерево поддерживает parent/child, иконки, выбор и перенос. Сохраняемые имена и
  порядок соседей, create/delete/duplicate, picking/gizmos/docking — следующие этапы.
- Viewport отображает реальную игру из child process через existing shared GPU
  transport. Размер legacy render surface960×540, Edit preview640×360; изображение вписывается без искажения;
  resize окна меняет layout. Editor camera navigation/gameplay input в этом
  приложении ещё не подключены; прежние input consumers сохранены отдельно.
- Текущий GPU путь — Windows x64, OpenGL UI + Vulkan runtime, совместимые UUID и
  extended native Skia. System settings/driver selection не меняются и implicit
  fallback не добавлен. Physical multi-monitor DPI и прежние UI release gates
  сохраняют собственные критерии.

## App и ownership

`UseAetherEditorEditing()` регистрирует shared `EditorInspectorViewModel`, scoped
`IViewStyle<EditorInspectorView>` и `IFactory<EditorInspectorView>` после настройки EditorDocumentCommands
и EditorPreview в composition root. Preview заимствует явные engine show/stop
operations; App владеет координатором и model/style, окно/родитель — View product.
Dispose View снимает bindings, не освобождая document/model/engine. Accepted preview
work отслеживается и drains до освобождения engine; failed cleanup допускает retry.
Header, hierarchy и status используют ту же модель и историю. Прежний
`EditorTransformView` factory сохранён для существующих consumers и заимствует
общую модель; compatibility `EditorTransformViewModel` не является вторым DI owner.

UI разделён на EditorWorkspace (данные/scene selection), EditorViewModel
(INotifyPropertyChanged, строки состояния, доступность и guarded commands),
EditorView (View<EditorViewModel>, Fluent-композиция/bindings) и EditorViewStyle
(IViewStyle<EditorView>, typed parts/roles/theme values). Все сервисы/style/model
получаются из DI, IFactory<EditorView> создаёт root на owner thread. View заимствует
model, снимает собственные subscriptions и освобождает элементы при Dispose или
ошибке construction. Model заимствует workspace/commands и отдельно принадлежит App.
Selection меняет inspector rows адресно; runtime snapshots обновляют bindings без
rebuild. Смена темы/стиля сохраняет input/focus/selection/viewport instances.

Application использует `UseAether` и `UsePresentationFactory<EditorView>` из `App.Build`. UI не
ссылается на test/probe executables и не загружает Game.Logic в свой CLR.
`GameBuildLoader`, RuntimeProtocol, producer, supervisor, Mithril process ownership
и Enerit IPC находятся в Aether. Здесь находятся composition и UI adapters.

NativeRuntimeFactory не выбирает consuming executable и не добавляет `--runtime`
неявно. Для отдельного runtime задайте в configureEngine абсолютные пути:

```csharp
engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll);
engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll);
```

Типы находятся в `TrueMoon.Aether.Hosting`. Для dotnet descriptor принимает
`absDotnetExe`, `absRuntimeDll` и явные `[absRuntimeDll, .. runtimeArguments]`.
Null RuntimeLaunch допускает registration/headless/custom RuntimeFactory, но default
native Start завершится понятной configuration error. Pair fingerprint включает
labelled host/runtime entries и exact shared Aether/Contracts/Enerit/Mithril, с
проверкой обоих каталогов и actual entry; это не authentication boundary и не полный
native/private-dependency hash. Deployment не меняют in place во время session.
Новая pair identity требует rebuild game artifacts и исключает старые checkpoints;
scene/project formats и control3/frame1/snapshot2 остаются прежними.

[Отдельный AetherRuntimeHost](../Examples/AetherRuntimeHost/README.md) не зависит от
Editor/UI. Child вызывает `RuntimeBootstrap.ReadEnvironment()` до App construction,
затем UseAether и runtime/preview registration; Main остаётся в executable.
[Bootstrap Version1/limits и runtime API](../TrueMoon.Aether/PACKAGE.md).

Directed smoke consumer AetherEditorHost **явно** выбирает
`NativeRuntimeLaunch.CurrentProcess("--runtime")` и прежний `BuildIdentity.HostId`:
это совместимый same-executable профиль с отдельным child process, не скрытое
требование к произвольному consuming exe. Его runtime branch не создаёт окно/Skia
session и загружает только выбранную игровую сборку либо built-in preview.

UI events не ждут IPC/compiler: EditorCommands отслеживает asynchronous work,
Stop/Cancel могут прервать pending command. Close прекращает admission и ждёт
Stop/drain до освобождения borrowed GL context. Неуспешный cleanup оставляет
возможность повторить завершение. Viewport владеет частной retained Skia copy;
Aether владеет transport/imports, окно/context остаются у Alloy.Hosting.

App использует Cobalt по умолчанию. Workspace/binding/commands регистрируются в DI;
`IFactory<EditorView>` создаёт UI root на owner thread. Binding реализует
`IFactory<IGameViewportTransport, GpuResourceHost>`. Child branch подключает
`UseAetherRuntime`, а выбранная игра создаётся явной entry factory с runtime DI.
Borrowed lifecycle/service facades не освобождают owned targets повторно.
[Решения и границы DI1–DI4](../docs/engine/PLAN.md#приведение-к-правилу-cobaltifactory),
[миграция игровых сборок](../TrueMoon.Aether/README.md#сборка-и-выбор-игровой-версии--eng3a).

## Диагностика компилятора и переход в исходник

В консоли «Диагностика» режим `Compiler` показывает ошибки и предупреждения C#/
analyzers с кодом, сообщением, файлом и координатами. Выберите строку ошибки и
нажмите «Открыть исходник»; кнопка доступна и с клавиатуры через Tab/Enter.
Для перехода host подключает `ISourceFileNavigator` через DI либо стандартный hook:

```csharp
context.UseAetherEditorSourceNavigation(new SourceEditorOptions
{
    ExecutablePath = sourceEditorExecutable, // абсолютный путь к executable
    Arguments = sourceEditorArguments       // отдельные аргументы CLI этого редактора
});
```

Типы и extension находятся в `TrueMoon.Aether.Editor.Diagnostics`. В аргументах
поддерживаются `{file}`, `{line}`, `{column}`; координаты начинаются с1. Например,
один аргумент `{file}:{line}:{column}` подходит executable с соответствующим CLI.
В массиве должен присутствовать `{file}`. Arguments не являются строкой shell:
каждый элемент передаётся отдельно через `ProcessStartInfo.ArgumentList`, без
добавления кавычек вокруг пути. Конфигурация фиксируется при сборке App; повторная
регистрация hook отклоняется. Сам внешний editor process принадлежит пользователю;
App освобождает process handle и drain/cancel только принятый navigation request.
Пользовательский hook заимствуется моделью и должен выполнять работу асинхронно,
учитывать cancellation и завершать accepted work до teardown приложения.

Источник structured данных — job-owned `compiler.sarif` формата2.1 через
[C# ErrorLog](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/errors-warnings#errorlog) после завершения
компилятора. Severity берётся из отчёта, не из stdout/stderr. Ошибка MSBuild/restore
до запуска C# compiler может не иметь SARIF: это `Unavailable`, raw вывод остаётся
в отдельных режимах. `Failed` и `TooLarge` отличаются от валидного пустого отчёта.
Поддерживаются локальные source locations; неизвестные URI/base mappings не дают
переход. Координаты относятся к исходной сборке, изменившийся файл не переадресуется.

Пределы: чтение SARIF до2MiB,128diagnostics/job,2048characters/message,
128/code,4096/source path. Compiler хранит16completed jobs+active; file retention
учитывает SARIF вместе с stdout/stderr. UI отдельно ограничивает compiler entries
через `EditorDiagnosticsOptions.MaximumRecords`/`MaximumRetainedBytes` (default512/
1MiB суммарно по показанным jobs), отмечает omissions/truncation и сохраняет
выбранную ошибку failed job после следующего successful Build в пределах истории.
Чтение UI snapshots не выполняет IO; source request использует frozen location.
Без hook или корректной source region переход недоступен, диагностика остаётся видна.

«Экспорт без текста» сохраняет только compiler state/count/omission metadata;
messages и source paths явно исключены. Persistent compiler history, полный
диагностический отчёт и platform/published acceptance продолжаются в ED8a.

## Проверки и published запуск

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj
dotnet publish ManualTests/AetherEditorHost --configuration Release --output TestResults/EditorScreen/publish
dotnet TestResults/EditorScreen/publish/AetherEditorHost.dll --project ManualTests/Game.Logic/Game.Logic.csproj
```

Это команды для отдельного запуска, не утверждение о текущем published результате
ARCH2c. Source Debug и published Release resource/runtime-launch proof выполняет
[VerifyExampleHosts](../ManualTests/EngineSessionProbe/VerifyExampleHosts.ps1);
он не открывает окно и не выполняет GPU smoke workflows. Independent NuGet и
GUI/native acceptance остаются открытыми.

`--light` выбирает светлую тему при старте. `--help` выводит CLI. Направленный smoke
работает только со своим окном и проверяет реальные game commands/pixels:

```powershell
$env:VK_LAYER_PATH = '<validation-layer-directory>'
dotnet run --project ManualTests/AetherEditorHost -- --smoke TestResults/EditorScreen/fresh --validation
```

Используйте новый output directory. Каждый smoke сохраняет5PNG заголовка,2PNG
границы и5PNG строки состояния, header/border/status JSON и runtime cleanup/validation reports.
Профиль добавляет свои interaction JSON/PNG: empty —1PNG, legacy —3PNG,
Transform —3PNG. Проверяются selection/inspector, separate PID,
Build/Start/Play/Pause/Step/Replace/Stop, theme/resize, та же UI tree/window,
close/drain и zero owned resources. Это native correctness proof, не performance
или physical DPI acceptance. Подробности — [план](../docs/engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22),
[статус](../docs/engine/STATUS.md), [история](../docs/engine/HISTORY.md).
