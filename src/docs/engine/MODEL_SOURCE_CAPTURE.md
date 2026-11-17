# ARCH1g.1a — model source capture v1

17.11.2026. [Единственная задача](PLAN.md#arch1g1a-source-capture).
[История проверок](HISTORY.md#arch1g1a-2026-11-17),
[manifest/SHA](MODEL_SOURCE_CAPTURE_RESULTS.json).
Первый production-срез g.1 находится в `TrueMoon.Aether.Tooling.Importing`.
Он подготавливает immutable source bytes для отдельного [CPU importer g.1b](MODEL_IMPORT_WORKER.md). Assimp, конвертация
geometry/materials, semantic subasset ledger, Nom и доставка ассетов в Runtime
не подключаются этим API. Исследовательский [captured native prototype](MODEL_IMPORT_CAPTURE.md)
сохраняет собственные даты, профили и ограничения.

## App, фабрика и владельцы

```csharp
using TrueMoon;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Dependencies;

await using var app = App.Build(context => context.UseAetherModelSourceCapture());
await app.StartAsync();
var factory = app.Services.Resolve<IFactory<ModelSourceCapture, ModelSourceCaptureArguments>>()!;
// Persistent source identity is stored by the application, not generated on reimport.
var arguments = new ModelSourceCaptureArguments(sourceId, sourceDirectory, "models/scene.gltf");
CapturedModelSource captured;
await using (var operation = factory.Create(arguments)!)
    captured = await operation.CaptureAsync(cancellationToken);
using var stream = captured.GetFile("models/mesh.bin").OpenRead();
// Use the detached bytes outside simulation/render callbacks.
```

`sourceDirectory` — явно выбранный абсолютный DOS-drive путь; `sourceId` — ранее
сохранённый non-empty Guid. Регистрация и создание операции не читают файлы,
не запускают SDK, parser или Runtime. Повторная регистрация отклоняется.
Фабрика заимствуется из DI; опубликованный factory product принадлежит caller и
освобождается **до App shutdown**. App не удерживает все короткоживущие captures.
Один product допускает только один CaptureAsync; повтор после ошибки/отмены тоже
требует нового product. Factory products независимы на Cobalt и Microsoft DI.

`DisposeAsync` запрашивает отмену и ждёт фактического окончания операции/IO cleanup;
Dispose синхронно ждёт тот же результат. Повторный Dispose безопасен. Ошибка операции
наблюдается через её capture task. Завершённый task не удерживается disposed product.
Снимок — обычные immutable данные с GC lifetime. OpenRead создаёт отдельный readonly
cursor без доступа к underlying array; caller закрывает stream. CopyBytes возвращает
копию. После изменения/удаления source снимок остаётся доступен без disk lookup.

## Поддержанный input profile

| Root | Захват зависимостей | Явные ограничения |
| --- | --- | --- |
| glTF 2.0 JSON | `buffers[].uri`, относительно каталога root | Только plain local files; URI-less buffer запрещён без GLB BIN |
| GLB v2 | JSON первым, затем optional BIN; external buffers тоже захватываются | Header/total lengths/chunk boundaries/alignment проверяются; URI-less buffer только первый, с 0–3 байтами BIN padding |
| OBJ UTF-8 | `mtllib`, относительно OBJ; несколько plain `.mtl` names | Comments и UTF-8 BOM поддержаны; quoted names, continuations, `call`/`csh` запрещены |
| MTL UTF-8 dependency | Bytes сохраняются; texture directives проверяются | `map_*`, bump/disp/decal/refl/norm дают unsupported outcome |

glTF extensions и nonempty images/textures/samplers отклоняются, включая embedded
textures. Неизвестные GLB chunks в этом ограниченном профиле дают NotSupported;
это выбранное ограничение, не утверждение повреждения любого такого valid glTF.
JSON duplicate properties запрещены как неоднозначные. Declared buffer length
должна быть положительной и не превышать available bytes/budget.

Проверки container/references не являются полным glTF/OBJ validator. Например,
geometry topology, axes/units, normals/UV, materials, animation/skinning/morphs и
соответствие MeshDefinition limits решает следующий parser/projection срез. Capture
не должен молча выдавать непроверенные bytes за готовый renderer asset.

Пути case-sensitive в capsule. Backslash/slash и `.` segments нормализуются;
absolute/UNC/drive/URI/data URI, `..`, empty segments, percent/query/fragment,
controls, Windows device names и trailing dot/space запрещены. Case-insensitive
collisions между объявленными именами запрещены. Нет CWD/basename/default-IO fallback.
Повторные normalized names не открывают и не проверяют один MTL многократно;
для повторных buffer references применяется наибольшая declared minimum length.

## Численные границы

| Ресурс | Hard profile v1 |
| --- | ---: |
| Captured files, включая root |32 |
| Один файл |16 MiB |
| Суммарно captured bytes |32 MiB |
| Raw spelling relative name |1024 UTF-16 characters, до Split/normalization allocations |
| Normalized relative name |256 UTF-8 bytes |
| Full physical directory depth, drive excluded |128, до native opens; обход итеративный |
| JSON depth / lexical tokens |64 /131072, до JsonDocument projection |
| OBJ/MTL line |4096 UTF-8 bytes |

`ModelSourceCaptureLimits` в runtime arguments разрешает снизить Files/FileBytes/
TotalBytes, но не поднять hard maxima. File count/length/total bytes проверяются до
следующего file buffer allocation. JSON strings/metadata, normalization, hashing
и прочие временные managed объекты не входят в captured-bytes count. Это не общий
heap/process memory cap, SLA и не App-wide admission для параллельных products.

## Handles, отмена и целостность

Первый Windows x64 профиль открывает directory ancestors по одному с
OPEN_REPARSE_POINT и без delete sharing. Файлы открываются readonly без write/delete
sharing; reparse attributes, file kind и final path проверяются на **открытом handle**.
Все pins/streams удерживаются до полного closure, повторного SHA чтения и повторной
проверки directory handles. После native/IO cleanup token проверяется ещё раз и
возвращается только целый detached result. Missing/locked/unsafe/invalid/oversized
inputs, cancellation или failure не возвращают частичный source manifest.

Sharing исключает обычные конфликтующие opens/rename на период capture; это не
security sandbox, atomic directory enumeration или защита от hostile filesystem,
ACL/reparse mutation и memory-mapped writers. Нельзя превращать revalidation в
обещание универсального atomic filesystem snapshot. Синхронные Win32 opens/queries
не прерываются cancellation token; отмена проверяется между ними, при chunked read,
JSON/text scanning и перед результатом. Dispose ждёт actual completion.

Нормативные основания: [Win32 CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew),
[GetFinalPathNameByHandleW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew),
[glTF 2.0 / GLB layout](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#glb-file-format-specification).

## Identity и следующий шаг

InputRevision — SHA256 versioned recipe + SourceId + normalized root name + ordinal
sorted length-prefixed names/bytes. Физический directory и file creation order не
участвуют; RootName/dependency bytes/SourceId участвуют. SourceId не выводится из
пути, content hash, native pointer или mesh index. Его сохранение/rename/move —
ответственность приложения/source record. Этот hash **не** полный import cache key,
canonical MeshDefinition revision и не RenderAssetKey: converter/settings/native
identity и persistent ledger будут добавлены на соответствующих стадиях.

Следующий g.1 срез: selected-format/coordinate/material/unsupported policy,
App-owned isolated native parser и validated CPU candidates, затем semantic locator
matching и atomic candidate+ledger publication. Nom/build/runtime bridge — g.2.
Asset watcher и Editor browser/inspector сохраняют собственные задачи.

Повторение source/published/fresh matrix:
[ModelCaptureConsumer](../../ManualTests/ModelCaptureConsumer/README.md).
