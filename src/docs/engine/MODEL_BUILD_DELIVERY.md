# ARCH1g.2 — импортированная модель в Build, Runtime и Player

Authoring App подключает `UseNom()`, `UseAetherModelImport()` и
`UseAetherNomContent()`. Мост `TrueMoon.Aether.Nom` принимает опубликованный
`PublishedModelImport` из [ledger](MODEL_SUBASSET_LEDGER.md) и заимствует открытый
проектный store на время вызова. Создание App и регистрация моста обходятся без
открытия store, запуска importer и компиляции.

```csharp
var snapshot = await bridge.PublishAsync(store, publishedModel, store.ContentGeneration, token);
var renderContent = await bridge.CaptureAsync(store, snapshot.Id, token);
var result = await compiler.BuildAsync(request with { RenderContent = renderContent }, token);
// In Runtime, after opening the selected artifact through GameBuildLoader:
using var content = catalogFactory.Create(loader.Artifact.RenderContent!);
```

`PublishAsync` отправляет целый набор mesh/material/binding-set ресурсов одним
Nom batch с ожидаемой generation. Ledger даёт постоянные AssetId, SlotId и
семантические locators. Canonical RenderContentCodec даёт байты и SHA256 каждой
ревизии. Binding set указывает точные ключи своего меша и материала. Все
ресурсы этой модели входят в roots снимка с exact versions из receipt, поэтому
захват не читает текущие heads по AssetId.

Nom сначала фиксирует ресурсы, затем создаёт снимок. Если вторая
операция завершится ошибкой, принятый commit Nom сохранится. Повторная публикация
проверяет текущую generation. Удержание снимка и закрытие store принадлежат caller.
Capture открывает lease и потоки внутри одного вызова, проверяет kind, длину,
digest и связи ресурсов, затем возвращает собственную копию байтов. Пределы
первого профиля — 256 roots и 12 MiB итогового payload. Удаление Nom heads при
удалении ресурсов из модели остаётся отдельной операцией.

Build копирует payload в тот же staging, что и игровую DLL. Schema 3 добавляет
фиксированный файл `render-content.bin` с проверяемыми длиной и SHA256. Его
SHA входит в `game-build.json`, а SHA манифеста задаёт BuildId. Публикация
использует существующий перенос целой staging-директории. Запрос без
`RenderContent` сохраняет schema 2 и форму манифеста. Managed compiler reuse
может повторно использовать DLL, а publish всегда берёт render input текущего
запроса.

Runtime открывает артефакт по точному BuildId, проверяет весь payload до
загрузки игровой сборки и включает файл в pin артефакта. Он восстанавливает
`RenderContentManifest` со своим BuildId. Runtime consumer явно получает
CPU lease через `UseAetherRenderResources()` и
`IFactory<RenderManifestLease, RenderContentManifest>`. Пакет Runtime может
читать артефакт самостоятельно. Nom, Tooling и Assimp нужны на стороне
authoring.

[Воспроизводимый прогон](../../ManualTests/ModelBuildConsumer/README.md) выполняет
два реальных импорта статического glTF с разной координатой вершины. Он
сверяет постоянные ID, точные старую и новую ревизии и совпадение байтов
игровой DLL. После второго импорта исходный снимок Nom снова строит прежний
BuildId. Отдельные Runtime-процессы получают обе версии из артефактов после
закрытия и перемещения Nom и исходников. Изменённая копия payload отклоняется.
[Фактические результаты](HISTORY.md#arch1g2a-2026-11-18)

<a id="arch1g2b-scene-gpu"></a>
## ARCH1g.2b — размещение и GPU

`ImportedModelScene.Place(scene, publishedModel, placement)` в Tooling возвращает
новую ревизию сцены. Постоянный ID экземпляра из ledger становится EntityId.
Экземпляры одного mesh используют общее определение с MeshRenderer, который хранит
стабильные mesh и binding-set AssetId. Поза узла из импорта компонуется с placement
и записывается в Transform v1. Этот формат поддерживает положительный масштаб,
поворот и перенос. Сдвиговая деформация (shear) и отражение отклоняются. Повторное
размещение одного импорта в той же сцене требует отдельной схемы ID размещения.
Caller сохраняет документ через существующий codec `.aetherscene`.

Игра с `RuntimeAccess` может реализовать `IGameBuildSessionEntry`. Штатная фабрика
Runtime передаёт ей проверенный `RenderContentManifest` выбранного артефакта на
время жизни сессии. Фабрика игры связывает `MeshRenderingComponents` с этим
манифестом, а `SceneWorldLoader` разрешает ссылки ECS при загрузке сцены.
Вход без render payload отклоняется при активации такого entry. Для
`IGameSessionEntry` и явно зарегистрированной пользовательской фабрики
действует прежний контракт.

Проверка использует одну сцену с камерой и двумя экземплярами одного
импортированного меша. Player получает её через `PlayerStart.CaptureAsync`.
Три последовательных запуска проверяют старый BuildId, новый BuildId и снова
старый BuildId. Проверка сверяет штамп сцены и манифеста, затем считывает пиксели
после представления Vulkan-кадра на Skia OpenGL surface. Замена одной вершины
увеличивает площадь изображения обеих копий, старый билд при повторном открытии
даёт идентичную маску. [Фактические результаты](HISTORY.md#arch1g2b-2026-11-18)

<a id="arch1g2c-editor-builds"></a>
## ARCH1g.2c — выбор опубликованного артефакта в Editor

`EditorScenePlayback.RunSelectedBuild(artifact)` передаёт полный документ
открытой сцены как `SceneWorldData`. Обычный допуск команд Editor фиксирует
активный проект и ревизию документа. Render content при наличии должен
принадлежать этому проекту. `GameEditor.SelectBuildForContextAsync` проверяет
артефакт и связывает кандидат с точной версией контекста. Startup ждёт
фактического GPU-present текущей поверхности перед переходом в Playing.
Команда использует опубликованный артефакт, поэтому ей достаточно его
runtime-совместимого BuildId и файлов. Проект и сцена остаются у Editor,
дочерняя игра владеет своей копией World.

Выбор через API координатора работает между `Stop` и следующим запуском.
`Stop` освобождает дочернюю сессию и её viewport. Устаревшее выделение сборки
и изменение контекста сохраняют прежние fences запуска. Для этого профиля
горячий `Replace` остаётся следующим срезом.
[Фактические результаты](HISTORY.md#arch1g2c-2026-11-18)
