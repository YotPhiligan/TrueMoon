# ARCH1g.2a — одна модель через Nom, Build и Runtime

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
authoring. Экземпляры модели и их ID остаются в authoring ledger, размещение
scene entities и вывод через GPU относятся к следующему срезу.

[Воспроизводимый прогон](../../ManualTests/ModelBuildConsumer/README.md) выполняет
два реальных импорта статического glTF с разной координатой вершины. Он
сверяет постоянные ID, точные старую и новую ревизии и совпадение байтов
игровой DLL. После второго импорта исходный снимок Nom снова строит прежний
BuildId. Отдельные Runtime-процессы получают обе версии из артефактов после
закрытия и перемещения Nom и исходников. Изменённая копия payload отклоняется.
[Фактические результаты](HISTORY.md#arch1g2a-2026-11-18)
