# AST1b: потоковые ресурсы, CAS и журнал

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

> Исторический LiteDB срез. Последующим решением2026-11-07 принята полная замена
> собственной реализацией Nom: [решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
> [активная очередь NOM0–NOM7](PLAN.md#nom-own-queue). Результаты ниже относятся
> только к старому backend; vendor fix/upgrade/isolation больше не активная задача.

> Историческая запись до переименования в TrueMoon.Nom и разделения проектов.
> Старые counts/hashes/graph относятся к указанному запуску; текущая структура,
> повторные проверки и команда — [NOM_RENAME](NOM_RENAME.md#nom-rename-2026-11-07).
<a id="ast1b-2026-11-07"></a>
## Проверенный срез — 2026-11-07

В [runnable prototype](../../ManualTests/TrueMoon.Nom.Probe/README.md) продолжен AST1b:
геометрия, изображения и шейдеры записываются и читаются через `Stream`. Storage
работает с opaque bytes и KindId; декодеры, import, shader compilation и GPU upload
принадлежат consumers. Новых production проектов, зависимостей Aether или публичного
App API нет. Baseline остаётся LiteDB5.0.21, net10.0, Windows x64.

Source Debug и framework-dependent published Release прошли **по18групп**:10 прежних
AST1a и8 новых streaming groups. Это console proofs с проверками результатов,
не количество xUnit tests и не full unit suite. AST1b fault acceptance ещё не закрыта:
physical disk-full, failed cleanup/retry и power-loss остаются открытыми gates.

## Команда и повторяемость

Из `src`, PowerShell7 и SDK.NET10:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomRun
```

Каталог результатов должен быть новым. Runner создаёт временный проект/feed/cache,
проверяет SHA официальных archives, resolved graph и actual output-local LiteDB.dll,
выполняет restore/build/source/publish/published, сохраняет stdout/stderr и SDK binlogs.
Полный bootstrap и package provenance описаны в [AST1a](AST1A_PROTOTYPE.md#ast1a-2026-11-07).
Frozen sources включают все10 C# файлов и Verify.ps1; hash drift запрещён.
TrueMoon.Contracts подключён source ProjectReference ради настоящего IFactory.
Это не production Assets package delivery proof.

## Потоковый контракт

| Операция research API | Поведение |
| --- | --- |
| ReserveResourcesAsync(expectedGeneration, specs, maximumTotalBytes) | Копирует/кодирует bounded descriptors; резервирует StoreId/sequence/request digest и durable pending journal |
| PublishResourcesAsync(ticket, sources, token) | Синхронно фиксирует ссылки Guid→Stream до первого await; stage/finalize вне DB owner, одна metadata transaction для всей пачки |
| LookupResourceOperationAsync(ticket) | Detached pending/aborted/committed outcome и exact revisions; UnknownOutcome требует close/reopen |
| AbortResourceOperationAsync(ticket) | Явно завершает ещё не запущенную reservation; работающий publish отменяется через token/stop |
| PruneResourceOperationsAsync(sequence) | Явная retention floor; expired ticket никогда не исполняется заново; floor0/no-op допустима |
| FindResourceAsync(assetId) | Проверяет схему head и identity exact revision; отсутствующий head возвращает null |
| OpenResourceAsync(exact, token) | Возвращает owned read-only seekable Stream; exact revision должна совпадать целиком |

Входные streams **заимствованы**: provider не вызывает Length/Position/Seek/Dispose,
поддерживает non-seekable источники и short reads,0 означает EOF. Для каждого asset
нужен отдельный readable stream; alias одного stream внутри пачки отклоняется.
Known length/hash опциональны: они проверяются, если объявлены; maximum обязателен.
Все bytes читаются блоками≤64KiB, без обязательного payload-sized массива. Источники
нельзя менять/закрывать до фактического завершения публикации.

Read stream удерживает owner/root до **фактического close**, включая активное чтение.
Open проверяет length/SHA256 целиком тем же открытым FileStream, затем возвращает его
с Position0. Это bounded-memory streaming, но полное предварительное I/O сканирование:
progressive first-byte delivery, chunk hashes и GPU residency paging ещё не реализованы.
Seek допускается в диапазоне0..Length; каждый фактический read≤64KiB.

## Публикация и исход операции

Одна reservation/store удерживает право записи. Сначала каждый ресурс попадает в
`staging/<sequence>/<index>.tmp`, инкрементально проверяется length/SHA256 и flush/close.
Затем выполняется no-overwrite finalize в `objects/sha256/<prefix>/<digest>.blob`.
Existing CAS object проверяется по length и полному hash; несовпадение — ошибка,
его bytes не перезаписываются и не удаляются. Одинаковые bytes разных AssetId
дедуплицируются физически, logical identities остаются разными.

Одна синхронная owner-thread LiteDB transaction публикует assets, immutable revisions,
heads, все journal outputs, generation и committed outcome. Ошибка любого ресурса,
stale generation или native unique-binding collision не публикует часть пачки.
CAS objects после abort могут остаться orphan: это безопасный остаток до AST1c GC.

Cancel до commit gate даёт aborted outcome без новой generation. После успешного
commit отмена не переименовывает результат в cancelled. Потерянный acknowledgement
даёт UnknownOutcomeException с ticket; owner запрещает дальнейшие операции до reopen,
затем journal разрешает исход. Неудачная staging cleanup после commit отражается
CleanupPending; она не отменяет публикацию.

Retry committed ticket возвращает **сохранённый outcome**, не читая replacement
streams. Request digest фиксирует descriptors/limits/declared length/hash. При
неизвестном declared digest retry не доказывает совпадение новых входных bytes:
для другого содержимого нужна новая reservation. Aborted/expired ticket не запускается.

## Каталог, recovery и ownership

Research resource collections: resource_control, resource_operations,
resource_operation_items, resource_revisions и resource_heads. Control singleton
создаётся атомарно с новым store; allocation high-water commit предшествует pending
record commit. Не выданные allocated gaps не переиспользуются. Отсутствующий control
отклоняется даже после prune всех операций: silent sequence reset запрещён.

AST1a каталоги без этого control не мигрируются автоматически: текущий прототип
явно отклоняет их. Это уточнение research format, не production migration support;
старые AST1a source/hash/results остаются историческими.

Recovery сначала валидирует весь bounded journal, canonical request hashes и все
committed output references. Descriptor metadata, asset identity, declared length/hash
и byte budgets сверяются с exact revision, затем только pending переводится в aborted
и очищаются известные staged files. Corrupt journal не чинится подстановкой другой
валидной revision. Staging не становится опубликованным содержимым при reopen.

Owner thread продолжает сериализовать metadata без await внутри transaction.
Stage/read I/O идут вне него. Одна reserved control lane завершает/abort операции даже
при полной data queue и после stop. Stop прекращает новые admission, отменяет I/O и
ждёт readers/writer; timeout сохраняет factory ownership/DB/root lock. Unused reservation
abort выполняется также при stop. Borrowed streams не освобождаются owner.

| Проверенный admission профиль | Значение |
| --- | --- |
| Buffer / object / batch bytes |64KiB /512MiB /1GiB; фактический большой fixture32MiB, full maximum workload ещё не измерен |
| Batch descriptors / metadata |≤256 /≤4MiB serialized metadata;≤128KiB asset BSON,≤64KiB opaque metadata |
| Resource users |1write reservation и≤16read leases/store |
| Journal / queue |≤4096operations;32data commands/16MiB плюс1control slot |
| Time policy |30s write cancellation deadline,10s default drain; deadline не прерывает принудительно I/O, игнорирующий token |

Остальные design budgets, общий disk accounting/reserve и log/temp high-water пока
не объявлены реализованными. Timeout не означает разрешение удалить root.

## Проверки

| Новая группа | Наблюдаемое поведение |
| --- | --- |
| StreamResourceKinds | Literal GEO1 triangle, valid1×1BMP и GLSL450: short/non-seekable upload, independent header/indices/pixel/text readback, seek и exact kind/hash/length |
| BoundedLargeStreaming | Generated32MiB source без whole-array;64KiB requests, independent hash/readback, managed-allocation bounds |
| StreamLengthHashBudgets | Before/at/over per-object/total/declared lengths, truncated/hash mismatch, malformed/negative descriptors, duplicate IDs, throwing source; no partial heads/generation |
| AtomicBatchJournalAndDedup | Failed second stream, dedup across logical IDs, old exact revision after head advances, stale generation, descriptor/ref capture before queue, native110 rollback after first resource, journal retry/mismatch/expiry/prune0 и explicit abort |
| StreamCancellationCommitRaces | Before/after commit gates, UnknownOutcome/reopen/lookup/retry; committed result не превращается в cancelled |
| StreamOwnershipAndStop | Borrowed sources, full data queue plus reserved completion, source игнорирует cancel, root/factory retained until actual writer finish; idle и active-read lease drain, stop abort reservation проверен raw journal до recovery |
| CorruptObjectsAndReadLeases | Missing/truncated/modified CAS, preserved colliding object,16/17leases, cancel/open cleanup; A-head→B-revision и committed item→foreign revision отклоняются; missing control после prune не пересоздаётся |
| ResourceCrashRecovery |4реально остановленных и reaped child processes/profile: after-stage, after-finalize, before-commit, after-commit; первые3 дают прежнюю generation, последний — всю новую пачку |

FinalReviewed:2profiles,5commands exit0,3SDK binlogs,11frozen compiled/script hashes,
source drift0. Actual library5.0.21.0, runtime10.0.14/x64; package/DLL SHA совпали с
AST1a pinned baseline. Child kill/reopen — process-crash proof, не physical power loss.

|32MiB observation | Source Debug | Published Release |
| --- | ---: | ---: |
| Upload allocated bytes |491720 |491456 |
| Open + streamed read allocated bytes |537272 |524648 |
| Largest input request / read calls |65536 /513 |65536 /513 |

Метрика — GC.GetTotalAllocatedBytes delta на изолированном generated workload,
включает служебные allocations, не peak heap/RSS и не performance SLA. Каждая
операция должна выделить<16MiB; независимый readback проверяет содержимое.

Общий nonincremental solution Debug:0errors/1555existing warnings; warning delta0
от AST1a baseline. Prototype own warnings0. Full unit suite не запускалась;
прежние Aether diagnostic failures не закрыты. Catalog symlink fixture не выполнен:
Windows privilege gate0x80070522, reparse prechecks не равны handle isolation.

Raw ignored evidence: `TestResults/LiteDbAst1b-20261107/FinalReviewed/` содержит
verification.json, Debug.json, Release.json, logs/binlogs и отдельные crash roots.
Attempt1 сохранил compile failure fixture setters; Attempt2 предшествовал последним
регрессиям; Attempt3 прошёл, затем исправлен explicit abort/stop race и выполнен FinalReviewed.
Эта versioned запись и runner сохраняют scope/команду при отсутствии raw artifacts.

Следующий шаг — оставшиеся AST1b fault gates (target write/disk-full и retained cleanup),
затем AST1c snapshots/pins/offline GC. Production App/Cobalt/package и engine bridge —
AST2/AST3; image/model decoding, shader compilation и GPU streaming этим proof не проверялись.

## Frozen inputs FinalReviewed

| Файл | SHA256 |
| --- | --- |
|CatalogCodec.cs|C7BF95B0AB571FB6FAD375FA3599F081F52EC72D878B350E148095A5DAB7949B|
|CatalogOwner.cs|7047062B52DE6D599D23A0E4BB34AB245144F30CFF61ECF0D69C640D79B2D005|
|CatalogProof.cs|591521AAE90DD222EBC856A2C5719AEE3CBA6AD8F8A5CC7F8C6608C9676E434F|
|CatalogResources.cs|59436301EB9A724AC60E49700033DAA5213E2C8A5FDCFEBFFBB8042CD75AC31E|
|Program.cs|79803BB6F542CF783E0674B7AC1CA973933417D8B613B8FD30DE49C6A46E9F4E|
|ResourcePublisher.cs|3BEBCD26D3D758A2A20516FB4BDAAA8A582FEE89FE19E76A1DF797ABA132A48B|
|ResourceReadStream.cs|7EE1E071F9C632F2B751D15FBEBA71085F9095065BE0DBE924542CDF41D36C66|
|ResourceTypes.cs|86B685B64EE1663E52A0B7C36D9B3D6DCF6E86E92F4FF46B14E093E0EB3CC091|
|StreamFixtures.cs|7F97D7CDD18AD136B617A61FB142BEAAFF0E125149CC23B9993C7DAE5DC38DE0|
|StreamingProof.cs|4AEC407131105F0B790F58B9B7644F5022A06E99BFC6F2B5BE0B5AC8219B8A16|
|Verify.ps1|78C95087773E914512484CA212B0ED0E6DF88E00A5ACB750FFC6082E4E24AADE|
