# Сценарный запуск Player и Editor

Для автоматизации Editor используйте [сценарий с вводом по ID](#editor-scenario).

`PlayerProbe --scenario` запускает готовый Build и сцену в тестовом приложении.
Оно подаёт `GameInputState` через протокол Player и выполняет фиксированные тики
по запросу. На контрольном тике следующий шаг ждёт фактического представления
кадра. PNG содержит композицию окна с HUD.
[Headless-профиль](#headless-player-scenario) выполняет тот же сценарий через
общий драйвер и сверяет игровые наблюдения.

<a id="synthetic-scenario"></a>
## Синтетический сценарий

Используйте Windows x64 и GPU с поддержкой Vulkan/OpenGL в профиле Player.
Выполните [пакетный профиль Player](../../ManualTests/PlayerProbe/README.md)
и укажите в `$verificationPath` путь к его `verification.json`. Профиль
публикует `RenderSurface.Game.MeshGame` для точной идентичности пакетного
`PlayerProbe.dll`. Фикстура рисует два куба. Ввод `Forward` меняет цвет первого
куба, поэтому кадры до и после действия различаются.

Сохраните `synthetic.scenario.json` в текущем каталоге.

```json
{
  "formatVersion": 1,
  "maxTicks": 6,
  "timeoutMs": 30000,
  "width": 640,
  "height": 360,
  "input": [
    { "tick": 3, "state": { "captured": true, "buttons": "Forward" } }
  ],
  "checkpoints": [
    {
      "tick": 2, "id": "before-input", "expectedInput": "Accepted",
      "expectedObservations": [
        { "key": "first-cube.red", "version": 1, "unit": "linear-channel", "minimum": 1, "maximum": 1 }
      ]
    },
    {
      "tick": 6, "id": "after-input", "expectedInput": "Accepted",
      "expectedObservations": [
        { "key": "first-cube.red", "version": 1, "unit": "linear-channel", "minimum": 0.5, "maximum": 0.5 }
      ]
    }
  ]
}
```

Пустой `SceneSnapshot` служит входом для тестовой фикстуры. Она создаёт
собственные кубы при загрузке. Для этого формата используется `--legacy-scene`.

```powershell
$delivery = Get-Content $verificationPath -Raw | ConvertFrom-Json
if (!$delivery.Completed) { throw 'Player package verification is incomplete.' }
$evidence = Split-Path -Parent (Resolve-Path $verificationPath)
$buildLog = Join-Path $evidence 'fresh-publish-artifact.stdout.log'
$build = Get-Content $buildLog -Raw | ConvertFrom-Json
$probe = Join-Path $delivery.RunDirectory 'fresh-published/probe/PlayerProbe.dll'

$runRoot = Join-Path $PWD ('synthetic-player-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $runRoot | Out-Null
$scene = Join-Path $runRoot 'synthetic.scene.json'
'{"formatVersion":1,"id":"synthetic-cubes","revision":41,"entities":[]}' |
    Set-Content -LiteralPath $scene -Encoding ascii
$output = Join-Path $runRoot 'run'
dotnet $probe --scenario ./synthetic.scenario.json $build.DirectoryPath $scene $output --legacy-scene
$exitCode = $LASTEXITCODE
$report = Get-Content "$output/report.json" -Raw | ConvertFrom-Json
$report.checkpoints | Select-Object renderedTick, frameId, surfaceGeneration, passed, file, sha256
$report.checkpoints.observationChecks | Select-Object expected, actual, outcome
```

Ожидаются код `0`, принятый ввод и красный канал первого куба со значениями `1`
на тике `2` и `0.5` на тике `6`. При повторе выберите новый каталог. Перед сравнением PNG сверяйте
Build ID, хеши сцены и сценария, версии, физический размер, масштаб и backend.

## Наблюдения игрового состояния

Игра может реализовать `IGameScenarioObservationSource`. На запрошенном тике
Player вызывает `ReadScenarioObservation()` после симуляции и до захвата кадра.
Верните `GameScenarioSnapshot` с завершённым тиком, положительной версией схемы
и набором до 64 конечных числовых значений. Ключи и единицы сравниваются с
учётом регистра. Снимок копирует значения и может пережить игровую сессию.
Возвращайте `null`, когда чтение состояния недоступно. Метод читает состояние
игры без его изменения.

Контрольная точка может задать `expectedObservations`. Для каждой проверки
укажите `key`, `version`, `unit`, `minimum` и `maximum`. Границы включительны.
Отчёт содержит `observationTick`, `observationVersion` и проверки с ожидаемым и
фактическим значением. Возможные исходы — `Passed`, `OutOfRange`, `Unavailable`
для отсутствующего снимка или ключа и `Incompatible` при другой версии или единице.

## Формат и результаты

- Сценарий версии `1` принимает логические `width` и `height`. Размер по
  умолчанию 800×540. `validation: true` требует установленного слоя Khronos.
- Необязательный `seed` сохраняется в отчёте. Симуляция читает свой сид из сцены.
- `maxTicks` допускает 1–100 000 тиков, `timeoutMs` допускает 1–600 000 мс.
  Дедлайн включает загрузку Build, окно и тики. Cleanup выполняется после прогона.
- Запись в `input` заменяет полное состояние ввода на заданном тике. `captured`
  и `buttons` действуют до следующей записи. `yaw`, `pitch` и `interactPressed`
  действуют один тик. `captured: false` задаёт нейтральный ввод.
- Сценарий принимает от 1 до 64 контрольных точек с разными тиками. `id`
  используется в имени PNG и состоит из ASCII-букв, цифр или дефисов, до 48
  знаков. `expectedInput` проверяет результат протокола, `expectedHudStatus`
  сравнивает строку состояния HUD целиком.
- `report.json` публикуется после освобождения App. Уже записанные контрольные
  точки и PNG сохраняются при сбое. `diagnostics/` содержит вывод App. У каждого
  PNG есть фактический тик, `frameId`, поколение поверхности, размер, масштаб и SHA-256.

| Код | Результат |
| --- | --- |
| `0` | Сценарий завершён, ожидания выполнены |
| `2` | Нарушено ожидание |
| `3` | Истёк дедлайн прогона |
| `4` | Прогон прерван через Ctrl+C или закрытием окна |
| `5` | Ошибка входов, запуска, выполнения или валидации |
| `6` | Ошибка освобождения ресурсов. Первичная причина доступна в `error` |
| `7` | Запрошенное наблюдение недоступно или несовместимо |

## Использование из C#

Для готового JSON версии 1 вызывайте `TrueMoon.Aether.Player.Scenarios.PlayerScenarioRunner.RunAsync`
из своего EXE. Передайте новый каталог отчёта и токен хоста; фабрика
шрифтов нужна только для окна, `null` выбирает headless. Build должен быть
опубликован для managed entry этого EXE. Поле `versions.probe` остаётся
частью формата v1 и содержит информационную версию этого entry.

<a id="player-scenario-resume"></a>
### Сохранение и продолжение сценария

Перегрузка `RunAsync` принимает `PlayerScenarioCheckpointOptions` со способом
хранения, выбранным приложением. `Save` вызывается на `SaveAtTick` только после
принятого тика и успешной контрольной точки, если она есть. `Load` проверяет
совпадение хешей сценария и сцены и читает сохранённый `GameCheckpoint`
до запуска App. Player проверяет хост, тот же Build, ID и ревизию сцены;
затем подтверждает полное восстановление перед готовностью или первым кадром.

При восстановлении на тике N следующий тик — N+1. `maxTicks` и запланированные
точки остаются абсолютными. Точки до N включительно не повторяются, удерживаемые
кнопки восстанавливаются из `input`, однотиковые импульсы не повторяются.
`report.json` указывает `resumedFromTick` после успешного запуска и `savedAtTick`
после публикации снимка. Без сохранения или восстановления поля равны `null`.

Хост может кодировать checkpoint существующим `GameCheckpointCodec`. Player
не определяет файловый формат слота или политику его перезаписи. Это не
импорт пользовательского сохранения и не переход на другой Build.

### Покадровый вызов из тестового App

В тестовом App передайте `options.ManualTicks = true` в `UseAetherPlayer`.
App владеет Player, окном и GPU-ресурсами. После запуска продвигайте время через
`IPlayerScenarioDriver` из `TrueMoon.Aether.Player.Scenarios`.

```csharp
var player = app.Services.Resolve<IPlayerScenarioDriver>()!;
await app.StartAsync(token);
var step = await player.AdvanceScenarioAsync(
    new GameInputState(true, GameButtons.Forward),
    capture: true, observe: true, cancellationToken: token);
var state = step.Observation;
await File.WriteAllBytesAsync(framePath, step.Frame!.Png.AsMemory(), token);
```

При отмене ожидания тестовый хост завершает App вне UI owner thread.
Режим `ManualTicks` использует начальную область рендера. Сохраняйте размер
окна и держите его открытым до конца прогона.

<a id="headless-player-scenario"></a>
## Headless-профиль Player

Добавьте `--headless` к той же команде `PlayerProbe --scenario` и выберите
новый каталог вывода. В C# используйте `UseAetherHeadlessPlayer(start)`,
затем тот же `IPlayerScenarioDriver`. Он передаёт ввод в штатный протокол и
делает один явный шаг Paused. Game, загрузка Build и наблюдения проходят через
тот же локальный PlayerSession. Headless не требует поверхности, оконного
сервиса или шрифтов. Оконный профиль по-прежнему допускает Playing после
первого фактически представленного кадра.

При собственной композиции используйте `IPlayerScenarioDriver`, а не
последовательность низкоуровневых вызовов `PlayerSession`. После `App.StartAsync` сессия находится
в `Paused`, `Surface` равна `null`, `SupportsFrameCapture` возвращает `false`.
Передавайте `capture: false`, а `observe: true` — на контрольных тиках.
Отмена завершает ожидание драйвера, но не заменяет освобождение App.
По окончании, таймауту или отмене вызовите `App.StopAsync` в `finally`, не
передавая отменённый токен прогона. Ожидать `Completion` окна не нужно:
безоконная композиция его не создаёт.

Поле `tick` обозначает завершённый тик во всех профилях. `renderedTick`
относится к кадру. В headless отчёт содержит `frameAvailability:
UnavailableHeadless`, значения кадра равны `null`, PNG отсутствует.
В оконном профиле доступность обозначена `Captured`. Числовые ожидания,
коды выхода и правила остановки одинаковы. Настройки окна и Vulkan validation
применяются только в оконном профиле.

Запустите проверку с точным пакетным Probe и Build из синтетического примера.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ManualTests/PlayerProbe/VerifyScenarioProfiles.ps1 `
  -ProbeAssembly $probe -BuildDirectory $build.DirectoryPath `
  -ScenarioPath ./synthetic.scenario.json -ScenePath $scene `
  -EvidenceDirectory ./TestResults/PlayerScenarioParity -LegacyScene
```

Проверка сверяет идентичность входов, контрольные тики, результат ввода, HUD,
схему и значения игровых наблюдений. Она проверяет SHA-256 оконного PNG
и завершение обоих Player, затем сохраняет `comparison.json` рядом
с отчётами `windowed/report.json` и `headless/report.json`.
В синтетическом примере оба профиля дают красный канал `1` и `0.5`
на тиках `2` и `6`. Для игры, которая меняет логику в зависимости от режима
Playing, используйте сравнение её собственных наблюдений.

<a id="editor-scenario"></a>
## Сценарий Editor с вводом по ID

[Сценарий повторного открытия сцены](../../TrueMoon.Aether.Editor.Tests/DocumentsUi/scene-round-trip.scenario.json)
исполняется в тестовом проекте Editor. Он использует общие поля сценариев
`formatVersion`, `timeoutMs`, `width`, `height` и именованные `checkpoints`.
В профиле Editor у каждой контрольной точки есть упорядоченные события
`input` с `target` и `kind`, а также ожидания к документу и семантическому
снимку. Размер задан в логических единицах, срок выполнения — в миллисекундах.

Хост создаёт настоящие header, обозреватель и инспектор с CPU Skia. `Click`
вычисляет точку в видимой области семантического узла. `KeyDown` и `Text`
проходят через проверку фокуса. `$transform-x` подставляет ID поля X по ID
компонента, полученному после создания объекта. Операции документа завершаются
в пределах дедлайна. Контрольные точки сверяют состояние после layout
и записывают фактические значения в вывод теста.

Сцена находится во временном каталоге и сохраняется через
`JsonSceneDocumentStore`. Сценарий закрывает документ, проверяет исчезновение
поля Transform и заново открывает файл через header. После открытия он
сверяет ID объекта и компонента, значение X и сброшенную историю.

Запустите тест из каталога `src`.

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --filter 'FullyQualifiedName~EditorSceneScenarioTests' --logger 'trx;LogFileName=editor-scenario.trx'
```

<a id="editor-scenario-comparison"></a>
### Сравнение прогонов Editor

`SceneRoundTrip_ComparesIndependentRunsAndWritesDifferences` дважды
исполняет сценарий в независимых сессиях и сравнивает контрольные точки
по ID. Контрольный вариант вводит `53` вместо `37` и увеличивает ширину
viewport с 1500 до 1580. Запустите тест из `src` с переменной
`TRUEMOON_EDITOR_SCENARIO_RESULTS`, чтобы сохранить отчёты.

```powershell
$env:TRUEMOON_EDITOR_SCENARIO_RESULTS = Join-Path $PWD 'TestResults/EditorScenarioComparison'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --filter 'FullyQualifiedName~SceneRoundTrip_ComparesIndependentRunsAndWritesDifferences'
```

Тест создаёт уникальный каталог с подкаталогами `repeat` и `variant`.
В каждом лежат `baseline.json`, `candidate.json` и `comparison.json`.
Отчёт показывает значения слева и справа в `stateChanges` и
`geometryChanges`, количество отличающихся пикселей и пути к кадрам.
Сравниваются состояние документа, фокус и доступность именованных узлов,
их `Bounds` и `VisibleBounds`, размеры viewport и масштаб. В отчёте
созданные объект и Transform используют постоянные обозначения. Внутри
каждого прогона тест проверяет реальные ID через закрытие и открытие сцены.

Кадры CPU Skia снимаются после раскрытия или закрытия панели инспектора.
В сравнении пикселей пропускается строка с сокращённым случайным GUID
выбранного объекта. Координаты строки указаны в `selectionIdLabel`,
а исходные PNG содержат её целиком. Остальные декодированные
премультиплицированные RGBA сравниваются без допуска. Кадры разных размеров
дополняются прозрачными пикселями до общих границ.

Для каждой несовпавшей пары сохраняются исходные PNG и diff PNG. Яркость
пурпурного показывает величину отличия, чёрный обозначает равные и
исключённые пиксели. Если контрольная точка есть только в одном прогоне,
отчёт указывает отсутствующую сторону и сохраняет доступный кадр.
Без переменной тест удаляет временный каталог после проверки.

<a id="editor-scenario-recording"></a>
### Запись действий Editor

В тесте сравнения запись включается явно для первого прогона. Она получает
события после допуска через `UiSession`, объединяет нажатие и отпускание в
`Click` и сохраняет семантическую цель, клавиши и модификаторы между контрольными
точками. Динамический ID поля X записывается как `$transform-x`. Запись входит
в те же девять контрольных точек и формирует редактируемый
`recorded.scenario.json`.

Текст по умолчанию сохраняется через `textParameter`. Повторный ввод пути
использует один параметр `scene-path`, значение которого передаётся отдельно
при воспроизведении. Встроенная синтетическая правка X разрешена как литерал.
Отсутствующий или некорректный параметр останавливает действие до передачи
ввода в UI. Второй прогон загружает сохранённый JSON и сверяет результат с
первым через тот же отчёт сравнения.

Для отдельного воспроизведения изменённой копии задайте
`TRUEMOON_EDITOR_SCENARIO_INPUT` с путём к сценарию, а через
`TRUEMOON_EDITOR_SCENARIO_PARAMETERS` укажите отдельный файл параметров.
Для синтетической сцены используйте объект
`{"scene-path":"uiqa-round-trip.aetherscene"}`. Запустите следующий тест.

```powershell
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -c Release --filter 'FullyQualifiedName~SceneRoundTrip_ReplaysSemanticInputAndRestoresTheSavedTransform'
```

<a id="editor-visual-states"></a>
### Визуальные состояния инспектора Transform (UIQA.3)

Из `src` в PowerShell:

```powershell
$run = Join-Path $PWD ('TestResults/UIQA3-' + [Guid]::NewGuid().ToString('N'))
$env:TRUEMOON_UIQA_RESULTS = Join-Path $run 'raster'
dotnet test TrueMoon.Aether.Editor.Tests/TrueMoon.Aether.Editor.Tests.csproj -p:NuGetAudit=false `
  --filter 'FullyQualifiedName~EditorVisualStatesTests|FullyQualifiedName~EditorSemanticInputTests' `
  --results-directory $run --logger 'trx;LogFileName=raster.trx' -v:q
if ($LASTEXITCODE -ne 0) { throw 'UIQA raster failed.' }
dotnet run --project ManualTests/AetherEditorHost/AetherEditorHost.csproj --no-build -- --uiqa-visual (Join-Path $run 'native')
```

Raster проверяет Light/Dark при логических масштабах 1×, 1,25× и 1,75×.
Тест сверяет пиксели клипа и мягкого переноса, фокус после Tab, ошибку и
доступность Apply. Без `TRUEMOON_UIQA_RESULTS` raster удаляет временные
кадры. Нативный профиль создаёт собственную песочницу, повторяет ввод по ID,
фокус, диагностику и отмену в настоящем окне OpenGL при его текущем масштабе.
Он не компилирует и не запускает игру. Каждый отчёт содержит тему, backend,
масштаб, состояния именованных целей, имена и SHA-256 кадров. В `native`
есть отдельные отчёты об освобождении сессии и проекта.
