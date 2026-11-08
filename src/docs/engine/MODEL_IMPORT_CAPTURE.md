# Model import: captured I/O, limits, identity и отмена

<a id="capture-v1"></a>
## Решение ARCH1g.0 — 2026-11-07

После [выбора AssimpNetter6.0.5 low-level](MODEL_IMPORT_RESEARCH.md#decision-2026-11-07)
реализован [проверяемый research consumer](../../ManualTests/ModelImportResearch/README.md).
Это Windows x64/net10.0 прототип и контракт следующего g.1 adapter. Production package,
App registration/IFactory, asset watcher, Editor и Runtime delivery пока отсутствуют.
Кодовая очередь f.3→f.4 сохраняется; g.0 ещё требует полного selected-format/unsupported
feature policy и coordinate/unit/material conversion decisions.

### Captured input и dependency closure

Capture владеет копиями root model и явно перечисленных dependencies. Файлы читаются
с FileShare.Read, проверкой длины и reparse ancestors; чтение проверяет отмену между
64KiB chunks. Список/count/bytes проверяются до allocations. Затем callback layer
видит только immutable memory map; никаких повторных открытий source paths, поиска
по CWD, сети или fallback к default Assimp I/O. В исследовательском API dependencies
задаются вызывающим кодом; automatic bounded glTF/OBJ dependency discovery — работа g.1.

Paths — case-sensitive relative UTF-8 names до256bytes. Slash/backslash и `./`
нормализуются; absolute/UNC/drive/URI, `..`, пустые segments, control characters,
percent/query/fragment и недопустимые Windows symbols отклоняются. Case-insensitive
collisions запрещены. Нет basename lookup и неявного выбора одноимённого файла.
Неоткрытая captured dependency допустима, но всякое запрошенное отсутствующее имя
или write access фиксирует ошибку. После native return ошибка запрещает публикацию,
даже если parser проигнорировал отсутствующий OBJ MTL и вернул scene.

Custom `aiFileIO` передаётся **четырёхаргументной** перегрузкой
`ImportFile(path, flags, fileIO, propStore)`. В Netter6.0.5 третий аргумент трёхаргументной
перегрузки — property store. ABI callbacks взят из [Assimp6.0.5 cfileio.h](https://github.com/assimp/assimp/blob/v6.0.5/include/assimp/cfileio.h):
Cdecl, size_t/nuint, fread item count, checked multiplication, bounded copies без
`byte[nativeRequestedSize]`, EOF/seek и rooted GCHandle owners. aiScene освобождается
после detached copy; callbacks/streams освобождаются после возврата native import.
Первое callback exception сохраняется как import failure и не переносится через ABI.

CaptureRevision — SHA256 versioned recipe + root name + ordinal-sorted names/lengths/
bytes. Recipe фиксирует Netter6.0.5, Windows x64/single precision, postprocess flags
и research projection version. Packet v1 — bounded little-endian binary transfer;
нет неограниченной JSON/base64 deserialization до проверки размеров. Revision независим
от input enumeration order; смена root/dependency bytes меняет revision. Это input
revision parser stage, **не** canonical MeshDefinition revision и не полный artifact
cache key: g.1/2 также должны включать source identity, frozen ID ledger/mappings,
import settings/schema и pinned native/adapter identity.

Capture пофайловый: это не atomic filesystem snapshot. Reparse precheck не устраняет
все ancestor replacement races; текущий FromDirectory рассчитан на доверенный local
research source. Для build интеграции нужен handle-verified capture/revalidation,
включённый в existing immutable build generation. Custom I/O ограничивает API чтения
Assimp; Job Object ограничивает ресурсы, но не является security sandbox native DLL.

### Численные budgets исследовательского профиля

| Ресурс | Максимум и точка проверки |
| --- | --- |
| Captured files, включая root |32; count до копирования |
| Один source/dependency / вся closure |16MiB /32MiB; длина до allocation |
| Callback open attempts / simultaneous streams |512 /16; до stream allocation |
| Суммарно скопированные native-read bytes |128MiB; до каждого copy, seek не обнуляет budget |
| Meshes / vertices / triangle faces |64 /65536per mesh /65536per mesh; до managed arrays |
| Nodes / depth / mesh instances |4096 /64(root depth0) /4096; до обхода/добавления |
| Detached position/index/instance payload |8MiB; checked accounting до каждой projection allocation |
| Native worker committed memory |512MiB, включая CLR/native/managed memory; Windows Job до activation/import |
| Concurrent imports / queued imports |1 /0; отказ до запуска второго worker |
| Import deadline |по умолчанию10s в proof; caller может уменьшить/изменить до30s, infinite запрещён |
| Kill/reap и diagnostic-reader drain |отдельный timeout10s; failed cleanup удерживает owner/capture и admission |
| Worker stdout / stderr |до8result/signal lines,16384chars/line; stderr retains8192chars и drains tail, overflow — failure |
| ID ledger |1024active /4096including tombstones, kind64chars/locator256chars; JSON input≤16Mi chars |

8MiB — счётчик detached payload, не managed heap/RSS cap: object headers, validation,
codec copies и parser allocations покрываются отдельно процессным committed-memory
лимитом. [Windows ProcessMemoryLimit](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information)
ограничивает committed virtual memory, не working set. Проверены installed flags и
actual VirtualAlloc denial после успешного малого allocation; parser-OOM corpus и
полный native allocator leak audit этим не заявлены. CPU profile f.1 остаётся строже
там, где его собственные MeshDefinition/codec limits применимы.

### Stable asset ID и reimport

SourceId создаётся один раз и сохраняется вместе с source import record. Rename/move
переносит этот record; копирование создаёт новый SourceId. Source path/content hash,
native pointer, mesh index и порядок обхода не используются как AssetId.

Versioned persistent ledger хранит `(kind, semantic locator) → Guid` отдельно от
content revisions. Locator должен быть однозначным в source namespace; неоднозначные
имена запрещают автоматический matching. Новая logical запись получает новый Guid;
перестановка discovery и изменение geometry сохраняют прежний ID. Rename требует
явной same-kind mapping, collision/multiple-target/unknown-source/still-present-source
отклоняются. Удаление оставляет tombstone; восстановление/переименование tombstone
требует явного mapping. Отсутствие mapping создаёт новый ID и оставляет старую ссылку
на tombstone, не перенаправляет ссылку на другой asset.

Ledger immutable: reconciliation создаёт candidate. Publish проверяет ожидаемую
generation и SourceId под одним gate, проверяет cancellation и затем заменяет весь
published record. Failed/cancelled/stale imports оставляют предыдущий record/ledger.
В research proof locators заданы явно; automatic extraction/matching native submeshes,
materials и slots, authoring sidecar persistence и atomic artifact+ledger transaction
ещё должны быть реализованы g.1. RenderAssetKey продолжает использовать project/kind/
schema/AssetId + canonical CPU revision из f.1; source revision его не подменяет.

### Worker ownership и отмена

Parent проверяет pre-cancel и admission, пишет bounded packet и запускает child с
redirected pipes без видимого окна. Child сообщает ready и ждёт go. Parent подключает
Windows Job: process memory cap512MiB, active process count1, kill-on-job-close;
проверяет реальные установленные limits, только затем разрешает native activation.
Assign/handshake failure не допускает import.

Native parser не считается cooperatively cancellable. Внешняя отмена/deadline приводят
к termination собственного Job и ожиданию **actual child exit**, затем drain stderr,
освобождение Job/process/packet и admission. Отмена await сама по себе не завершает
владение. При OS cleanup failure owner/capture сохраняются, capacity остаётся занятой;
это fail-closed путь, отдельная искусственная OS-failure проверка не выполнена.

После полного native return выполняются I/O-error gate, CPU validation и detached
fingerprint. Только complete result с совпадающей input revision и drained owners
возвращается parent. Перед publication token проверяется снова. Отмена после
успешного commit не откатывает уже опубликованную generation; stale completion её
не перезаписывает. Process memory reclaim при kill заменяет unsafe раннее освобождение
памяти/callbacks в процессе, который ещё исполняет native code.

### Фактически выполненные проверки 2026-11-07

```powershell
& ./ManualTests/ModelImportResearch/Verify.ps1 -Captured -EvidenceDirectory TestResults/CapturedImport-20261107/Final
```

Source Debug и framework-dependent published Release: **2/2profiles**, по8групп,
по9started/reaped workers, LiveImports0;6commands exit0,3unique MSBuild binlogs,
14frozen research source hashes и10fixture hashes. Actual output-local native6.0.5/
SHA/flags17 проверены. Evidence — ignored directory выше; [журнал](HISTORY.md#captured-import-2026-11-07).

| Требование | Конкретная проверка |
| --- | --- |
| Captured bytes/paths/packet |CapturePathsAndPacket: caller mutation, order/root digest, unsafe paths/collisions, truncation/trailing/oversized packet paths |
| No fallback, native dependency fidelity |CapturedOnlyAndMutation: literal geometry/slots/transforms, exact used closure GLB/glTF+BIN/OBJ+MTL/grid, live mutation+deletion, uncaptured dependency exists on disk but import fails |
| Limits |CaptureBudgetBoundaries, CallbackReadSeekAndQuotas, NativeProjectionBudgets, WorkerOutputBoundaries: before/at/over, EOF/overflow/seek, open/read/write/dispose, faces/depth/output rejection, bounded stderr/stdout |
| Stable ID |AssetIdentityReimport: serialized roundtrip, content/order changes, add/delete/rename/restore, ambiguity/collisions, escaped Unicode persistence, immutable failed candidate |
| Cancel/isolation/publication |WorkerCancellationAndPublication: pre-cancel, startup failure/recovery, actual commit-memory denial,3in-native-read cancels, competing admission rejection, deadline, corrupt input, recovery/multi-file packets, post-parse cancel, successful commit/stale/foreign-source rejection |

Прежнее comparison regression также повторено отдельно:4variants/8profiles; это
не performance comparison двух captured pipelines. GPU, Editor/Runtime/App lifecycle,
fresh production importer NuGet, all-RID, arbitrary third-party models и full solution
unit suite этим captured proof не проверялись. Полная g.1 реализация ещё впереди.
