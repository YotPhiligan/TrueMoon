# Сценарный запуск оконного Player

Для автоматизации Editor используйте [сценарий с вводом по ID](#editor-scenario).

`PlayerProbe --scenario` запускает готовый Build и сцену в тестовом приложении.
Оно подаёт `GameInputState` через протокол Player и выполняет фиксированные тики
по запросу. На контрольном тике следующий шаг ждёт фактического представления
кадра. PNG содержит композицию окна с HUD.

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

В тестовом App передайте `options.ManualTicks = true` в `UseAetherPlayer`.
App владеет Player, окном и GPU-ресурсами. После запуска продвигайте время через
`PlayerWindow.AdvanceScenarioAsync`.

```csharp
var player = app.Services.Resolve<PlayerWindow>()!;
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
