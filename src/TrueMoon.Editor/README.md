# TrueMoon.Editor

Первый основной экран редактора: стандартное borderless оформление Alloy,
панель управления, список объектов сцены слева, живой3D viewport в центре,
таблица свойств выбранного объекта справа и строка состояния снизу. Desktop WinExe
не открывает дополнительную консоль. Dark/Light
переключаются кнопкой «Тема». Это desktop application, `IsPackable=false`;
движок остаётся в единственной сборке `TrueMoon.Aether`.

```powershell
dotnet run --project TrueMoon.Editor
```

В source checkout автоматически выбран `ManualTests/Game.Logic/Game.Logic.csproj`
и `Game.Logic.RoomGame`. Нажмите **«Собрать» → «Запустить»**: приложение собирает
внешнюю Game.Logic, запускает отдельный runtime в Pause и показывает комнату.
Затем доступны Play/Пауза/Шаг/Стоп. Новая сборка во время работы готовит candidate;
«Применить сборку» использует существующий checkpoint/Replace и сохраняет состояние.
Ошибки возвращаются в инспектор; занятые/недоступные действия отключены.

Для другого проекта:

```powershell
dotnet run --project TrueMoon.Editor -- `
  --project E:/games/MyGame/MyGame.csproj --assembly MyGame.dll --entry MyGame.Session
```

Выбранный тип должен реализовывать Aether game/render/checkpoint contracts и
принимать демонстрационную SceneSnapshot. Поддержка project/scene files, asset
browser и сохранения пользовательской сцены относится к следующему toolset.

## Границы первого экрана

- Инспектор показывает **authoring-свойства текущего снимка сцены**: ID, позицию,
  интерактивность, геометрию и RGB. Значения можно выделять/копировать. Пока он
  read-only и в Stop, и в Play; редактирование компонентов и runtime snapshot/edit
  transaction не реализованы этим экраном.
- Список объектов пока плоский. Tree view с parent/variant inheritance,
  picking/gizmos/docking и project persistence остаются отдельными этапами.
- Viewport отображает реальную игру из child process через existing shared GPU
  transport. Размер render surface960×540, изображение вписывается без искажения;
  resize окна меняет layout. Editor camera navigation/gameplay input в этом
  приложении ещё не подключены; прежние input consumers сохранены отдельно.
- Текущий GPU путь — Windows x64, OpenGL UI + Vulkan runtime, совместимые UUID и
  extended native Skia. System settings/driver selection не меняются и implicit
  fallback не добавлен. Physical multi-monitor DPI и прежние UI release gates
  сохраняют собственные критерии.

## App и ownership

Application использует `UseAether` и `UsePresentation` из `App.Build`. UI не
ссылается на test/probe executables и не загружает Game.Logic в свой CLR.
`GameBuildLoader`, RuntimeProtocol, producer, supervisor, Mithril process ownership
и Enerit IPC находятся в Aether. Здесь находятся composition и UI adapters.

Существующий NativeRuntimeFactory запускает **отдельный процесс того же
executable с `--runtime`**. Runtime branch не создаёт окно/Skia session и загружает
только выбранную игровую сборку. Отдельная поставляемая runtime executable —
последующая задача, не изменение process isolation этого экрана.

UI events не ждут IPC/compiler: EditorCommands отслеживает asynchronous work,
Stop/Cancel могут прервать pending command. Close прекращает admission и ждёт
Stop/drain до освобождения borrowed GL context. Неуспешный cleanup оставляет
возможность повторить завершение. Viewport владеет частной retained Skia copy;
Aether владеет transport/imports, окно/context остаются у Alloy.Hosting.

## Проверки и published запуск

```powershell
dotnet test TrueMoon.Editor.Tests/TrueMoon.Editor.Tests.csproj
dotnet publish TrueMoon.Editor --configuration Release --output TestResults/EditorScreen/publish
dotnet TestResults/EditorScreen/publish/TrueMoon.Editor.dll --project ManualTests/Game.Logic/Game.Logic.csproj
```

`--light` выбирает светлую тему при старте. `--help` выводит CLI. Направленный smoke
работает только со своим окном и проверяет реальные game commands/pixels:

```powershell
$env:VK_LAYER_PATH = '<validation-layer-directory>'
dotnet run --project TrueMoon.Editor -- --smoke TestResults/EditorScreen/fresh --validation
```

Используйте новый output directory. Smoke создаёт3PNG, interaction/report JSON и
runtime cleanup/validation reports. Проверяются selection/inspector, separate PID,
Build/Start/Play/Pause/Step/Replace/Stop, theme/resize, та же UI tree/window,
close/drain и zero owned resources. Это native correctness proof, не performance
или physical DPI acceptance. Подробности — [план](../docs/engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22),
[статус](../docs/engine/STATUS.md), [история](../docs/engine/HISTORY.md).
