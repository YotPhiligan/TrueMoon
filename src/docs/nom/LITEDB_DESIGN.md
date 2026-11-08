# TrueMoon.Nom: первый профиль LiteDB

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

> Исторический LiteDB срез. Последующим решением2026-11-07 принята полная замена
> собственной реализацией Nom: [решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
> [активная очередь NOM0–NOM7](PLAN.md#nom-own-queue). Результаты ниже относятся
> только к старому backend; vendor fix/upgrade/isolation больше не активная задача.

<a id="litedb-v1-2026-11-07"></a>
## Решение и scope — 2026-11-07

Пользователь выбрал **LiteDB** после [критериального сравнения](COMPARISON.md).
Это действующее направление реализации. SQLite сохраняется как исследованная
альтернатива, сравнивать providers перед каждым следующим шагом не требуется.
Закреплённая точка отсчёта — [LiteDB5.0.21](https://www.nuget.org/packages/LiteDB/5.0.21), без перехода
на prerelease. [AST1a](AST1A_PROTOTYPE.md#ast1a-2026-11-07) закрепил archives/graph/DLL SHA и проверил .NET10 owner/schema/transactions в source Debug/published Release.

Ниже — целевой v1 для AST1; реализованный публичный срез AST2a описан в [App API](APP_INTEGRATION.md). Самостоятельный
модуль не зависит от Aether. Базовые решения: Direct mode, один project owner,
authoritative LiteDB catalog, immutable filesystem objects, явные snapshots/pins,
staged publication и offline GC. AST1a и AST1b streaming/CAS/journal core проверены; AST2a App API реализован; AST1b.f частичен, подтверждён native WAL blocker5.0.21. [Fault acceptance](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07); pins/GC и production delivery ещё впереди.

## Пакеты и подключение

Имя модуля — **TrueMoon.Nom**, утверждено пользователем2026-11-07. Текущий проект
TrueMoon.Nom содержит public App/storage API и private storage core; IsPackable пока false.
TrueMoon.Nom.Probe содержит core console proofs и isolated WAL fault runner, TrueMoon.Nom.Tests — safe App/IO/lifecycle tests. [Перенос и повторные проверки](NOM_RENAME.md).
В AST2b предполагается разделение storage contracts/values/common operations и
`TrueMoon.Nom.LiteDb` provider/App registration; такого provider project пока нет.
BSON, ILiteDatabase/ILiteCollection,
connection strings и IQueryable не входят в consumer API. Не вводить generic database
facade или runtime provider discovery для первой реализации.

Реализованное расширение IAppConfigurationContext — `UseNom(configure)`. Оно
валидирует/фиксирует options и регистрирует service/typed factory через App/Cobalt.
`IFactory<IProjectAssetStore, OpenAssetStoreArguments>` — публичная типизированная фабрика открытия
project owner; public signatures/ownership проверены в [AST2a](APP_INTEGRATION.md). Create и Open различаются:
Open отсутствующего/повреждённого catalog не создаёт новый store с новыми IDs.

App владеет общей инфраструктурой/фабрикой; project/session consumer владеет результатом
Create и обязан drain/dispose. DB/очередь/root lock принадлежат project store. Snapshot,
write operation и read stream — отдельные owned results; dispose alias не должен
повторно закрывать underlying owner. Aether adapter преобразует его специфичные keys
через bridge, без перемещения существующих публичных render types в этой задаче.

App-owned factory/coordinator удерживает bounded registry активных и failed owners.
Failed Create остаётся owned координатором, пока worker/DB/lock фактически не drained;
failed Dispose не удаляет owner из registry. Успешный consumer Dispose освобождает
запись; App stop координирует тот же idempotent drain, не создаёт второго владельца
через DI alias. Registry не превращает любой произвольный IFactory result в DI-owned
resource — это явный контракт именно Nom factory.

## Physical layout и authority

```text
<store-root>/
  catalog.litedb
  owner.lock
  objects/sha256/<first-two-hex>/<digest>.blob
  staging/<operation-sequence>/...
```

LiteDB datafile/log/temp обслуживаются library; catalog — **durable authority** для
ProjectId/StoreId, SourceId/AssetId, identity ledger/tombstones, heads/revisions,
dependencies и committed operations. Это не удаляемый search cache. Indexes могут
перестраиваться, authoritative records должны сохраняться. Для Git/переноса планируется
versioned logical export/import; sidecars не становятся вторым независимым authority.

Project owner удерживает exclusive root lock всё время жизни, включая staging,
migrations и GC. Только он открывает Direct database. Import workers передают detached
results, а не открывают shared writable catalog. Runtime получает immutable copied
bundle/manifest; не зависит от live Editor DB. Несуществующий catalog при непустом
objects root, mismatched ProjectId/StoreId, unsupported format или collation — explicit
open failure, без автоматического reset/rebuild/upgrade.

Roots — local filesystem; network/shared-machine store и reparse aliases не входят
в initial proof. Canonical root/handle identity и exclusive-open races проверяются
перед claim of support. Путь объекта строится из validated digest; source path/name
не используется как object path. Existing digest никогда не перезаписывается:
совпали hash/length — reuse, не совпали — integrity failure.

## LiteDB owner и transactions

Один выделенный owner thread создаёт, использует и закрывает LiteDatabase. Bounded
queue принимает detached typed commands. BeginTrans→read/write→Commit/Rollback
выполняется синхронно на этом потоке, без await, пользовательских callbacks, decode,
import или streaming source bytes внутри transaction. Collections/readers не покидают
owner: caller получает bounded DTO/page либо immutable snapshot.

Это соответствует [per-thread transaction contract5.0.21](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Client/Database/LiteDatabase.cs).
BeginTrans/Commit/Rollback results проверяются; native unique-key auto-rollback в5.0.21 может оставить Rollback=false (active transaction уже нет), см. [verified semantics](AST1A_PROTOTYPE.md#ast1a-2026-11-07). Unexpected nested transaction — owner
fault, а не повод silently продолжать batch. Failed rollback/close удерживает owner
и lock для последующего drain. Не использовать BsonMapper.Global или изменение
CurrentCulture; persistence codec имеет явное schema/field mapping.

Shared mode в v1 не включён. В [SharedEngine5.0.21](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Client/Shared/SharedEngine.cs)
внешний mutex ожидается без timeout; внутренний TIMEOUT pragma не доказывает finite
ожидание этого mutex. Parallel readers/write locks LiteDB не отменяют необходимость
generation consistency нашего модуля; первый provider намеренно сериализует metadata
commands. Blob reads могут идти параллельно по pin и не требуют DB cursor.

## Catalog schema v1

Названия ниже — persistent collections provider, не папки/namespace и не обещанный
public API. В каждой записи проверяется RecordSchemaVersion; arbitrary POCO mapping
и автоматическое создание AssetId библиотекой не используются.

| Collection | Содержание | Keys/indexes и инварианты |
| --- | --- | --- |
| store | Единственный StoreId/ProjectId, format/codec/profile, ContentGeneration, counters, operation sequence/floor | Singleton; generation меняется только при atomic content/identity publication, не на каждый read pin |
| sources | SourceId, logical source location, captured input/recipe provenance, ledger version | SourceId primary; exact source binding key unique; rename сохраняет ID |
| assets | AssetId, SourceId/KindId, semantic locator, current revision, display/tags, tombstone | AssetId primary, source/kind/binding indexes; GUID не выводится из name/hash/index |
| revisions | Immutable exact revision: AssetId/kind/schema, payload digest/length, semantic metadata/recipe digest | Exact revision key primary, asset lookup index; same key с другим содержимым — failure |
| dependencies | From exact revision → target exact revision, dependency role | Canonical edge key unique; forward/reverse indexes; только complete strong dependencies |
| identity_ledger | Source/Asset identity binding, active/tombstone state, explicit rename/restore provenance | Сохраняется атомарно с assets/sources; matching делает importer/consumer, storage проверяет IDs/conflicts |
| objects | Digest, verified length, codec/storage version, inventory/garbage state | Digest unique; content dedup не объединяет logical AssetId |
| manifests / members | Immutable snapshot header/digest и отдельные exact members | Manifest key + member key; не собирать весь проект в одном BSON document |
| pins | Snapshot/retention owner и purpose, exact manifest, release state | Pin primary, manifest/owner indexes; timeout не является доказательством отсутствия reader |
| operations | Store-issued sequence, request digest, expected/result generation, staged/committed/aborted state | Operation key unique; retry с другим request digest — conflict |

Все foreign references и deletion rules проверяются нашим provider внутри transaction;
DbRef не подменяет constraints. Для composite uniqueness используются явные derived
keys из versioned length-prefixed canonical fields. При совпавшем digest исходные
fields также сравниваются; collision не превращается в alias. GUID/digests имеют одно
canonical encoding, строки identity сравниваются ordinal.

На создании задаётся **Collation.Binary**; на Open проверяется сохранённая collation.
В source5.0.21 [она определена как invariant/ordinal](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Utils/Collation.cs).
Canonical hashing/sorting всё равно выполняет codec модуля, независимо от query order.
Changing collation существующей DB требует отдельной migration/rebuild проверки.
Не использовать default CurrentCulture/IgnoreCase для identity.

## Revisions и query model

AssetId — logical identity; payload digest — exact bytes; revision digest дополнительно
фиксирует kind/schema, semantic metadata, recipe и sorted exact dependencies. Display
name/location/tag changes могут менять catalog generation без изменения payload.
Canonical serialization versioned и не зависит от BSON layout, traversal order,
wall-clock timestamps или provider indexes. Importer recipe остаётся opaque для storage.

Первый strong-dependency profile — bounded DAG; missing targets/cycles/over-budget
closure отклоняются до publication. References внутри opaque payload не становятся
автоматически retention edges. Поздняя поддержка soft/cyclic logical references
требует отдельного schema решения.

Public query scope: ID/exact revision lookup, source/kind/tag pages, direct reverse
dependencies и bounded transitive closure. Traversal — explicit visited set/limits,
forward/reverse indexes; никаких lazy IQueryable/ILiteCollection с произвольным
кодом caller. Tags/metadata возвращаются detached; extension metadata хранится как
versioned bounded payload, custom arbitrary query/index API откладывается.

## Потоковые bytes — требование2026-11-07

Геометрия, изображения и шейдеры должны записываться и загружаться через Stream,
без обязательного whole-resource массива. Storage отвечает за bytes/identity/integrity,
consumer — за import/decode/CPU validation/shader compilation/GPU preparation.
Входные streams borrowed: non-seekable/short reads, без Length/Seek/Dispose provider.
Ссылки на inputs фиксируются до ожидания owner queue; metadata — detached snapshot.
Length/hash могут быть неизвестны, но per-object/total ceilings обязательны.

Первый [AST1b proof](AST1B_STREAMING.md#ast1b-2026-11-07) использует64KiB buffer и
owned seekable read stream. Перед возвратом проверяет полный hash тем же handle и
rewind; память bounded, но требуется предварительное полное I/O сканирование.
Progressive first-byte/chunk verification и GPU residency paging — отдельные задачи.
Stream удерживает owner/root до actual close, включая незавершённый read.

## Write operation и atomic publication
1. **Admit/reserve:** owner проверяет expected ContentGeneration и budgets, резервирует
   store-issued operation sequence/request digest. Одновременно одна write operation;
   она удерживает protection от GC до commit/abort/recovery.
2. **Stage:** вне DB transaction потоково записываются bounded bytes, считаются length/
   SHA256, проверяется token. Source handles/arrays не публикуются. Failed stage оставляет
   только owned staging/orphan data и не меняет visible assets/heads/ledger.
3. **Verify/finalize objects:** flush/close, проверка length/hash, no-overwrite move
   в CAS; existing object сверяется. Durability protocol зависит от OS/filesystem:
   atomic rename сам по себе не доказывает power-loss durability. Сначала process-kill
   recovery, затем отдельный durability gate; до этого соответствующие claims закрыты.
4. **Validate candidate:** проверить canonical revisions, counts, all dependencies,
   immutable existing records, identity changes и полный serialized BSON budget.
   Presence proof/pins защищают objects от GC до окончания операции.
5. **Commit:** на owner thread одна LiteDB transaction повторно проверяет generation,
   меняет affected assets/sources/ledger, добавляет revisions/edges/object inventory,
   увеличивает generation и записывает committed outcome. Публикуется весь candidate;
   полный project manifest при каждом маленьком import не переписывается.
6. **Complete:** вернуть committed operation/generation, освободить admission/staging
   protection. Неудачная очистка staging отражается отдельно; не превращать уже
   committed результат в «import cancelled».

Если token/deadline замечен до commit gate — abort. Во время commit не закрывать DB
с другого потока и не считать cancellation await завершением работы. Результат —
Committed либо Aborted; при failure с неизвестным commit outcome owner Faulted,
Recover/LookupOperation разрешает Unknown по journal/catalog после reopen.

Retry использует выданный OperationId и тот же request digest. История bounded:
после explicit retention pruning sequence≤retention floor возвращает OutcomeExpired,
никогда не выполняется заново. Не удалять idempotency records silently по времени и
не выдавать новый commit для повторного старого token. Overflow sequence/generation
отклоняется; rollover не допускается.

AST1b research ticket replay возвращает сохранённый outcome, replacement streams не
читаются. При неизвестном declared digest это не проверка совпадения новых bytes.
Mandatory resource_control создаётся с новым store и никогда не восстанавливается
из default при потере: старые AST1a stores без него явно отклоняются, migration — AST1d.
Request descriptors и committed output revision проверяются вместе до recovery mutation.
## Snapshots, retention и GC

Snapshot capture идёт короткой owner command: фиксирует текущие requested roots,
их exact closure и generation, создаёт immutable manifest+members и pin. Это не
запрос к произвольному прошлому ContentGeneration: прошлое доступно только через
сохранённый snapshot/retention manifest. Pages обычного поиска возвращают generation;
при смене generation continuation отклоняется, а не смешивает разные views.

Snapshot/read stream удерживает pin до фактического close. Cross-process build либо
удерживает parent-owned durable pin до confirmed child exit, либо читает полностью
скопированный immutable export. Pins предыдущего App run не истекают автоматически:
после crash консервативно удерживаются до доказанного release/recovery. PID/возраст
записи сами по себе не разрешают deletion.

Manifest содержит compact exact keys/header, не inline metadata каждого member;
весь serialized manifest≤1MiB. Metadata/bytes читаются отдельно с собственными limits.
OpenRead увеличивает удержание pin; Dispose snapshot запрещает новые opens, но уже
открытые streams продолжают держать pin до close. Ни snapshot dispose, ни stop timeout
не освобождают его раньше последнего stream.

GC v1 — explicit offline maintenance: exclusive owner, нет active write operations,
open snapshots/streams или unresolved pins. Roots — current heads и retained manifests;
сначала bounded mark/dry-run, затем journaled metadata pruning и object deletion.
GC не удаляет identity/tombstone records. Crash между metadata prune и file delete
оставляет resumable garbage; live reference на удалённые bytes недопустима. Перед
каждым delete проверяются digest/path containment и отсутствие reference; operation
recovery не принимает user/source paths как deletion paths. Online GC — отдельный шаг.

## Backup, migrations, stop и ошибки

Backup/export фиксирует manifest/ledger/schemas и complete verified object set.
Первая consistent DB backup — при drained owner с закрытым/checkpointed catalog,
по проверенной library policy; logical export может читаться отдельно. Копирование
только открытого datafile не заявляется backup. Restore сверяет StoreId/ProjectId,
versions/digests/closure; никакого auto-reset при corruption.

Explicit migrations выполняются эксклюзивно до admission, с backup/recovery outcome;
USER_VERSION не заменяет RecordSchemaVersion. [Upgrade/AutoRebuild settings](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Engine/EngineSettings.cs)
не включаются неявно. Read-only open неизвестного schema не пытается исправить store.

States: Opening → Ready → Draining → Closed; failure → Faulted/retained owner.
Stop закрывает admission, cancels queued/staging work согласно policy, дожидается
active transaction/readers и закрывает LiteDB на owner thread. ReleasePin/stream-close
имеют reserved/coalesced control path, не блокируются заполненной data queue и stop
admission. Timeout drain не освобождает DB/root lock из-под продолжающейся операции;
retained ownership и причина доступны diagnostics. Async API оборачивает owner queue,
не обещает принудительного прерывания зависшего filesystem/library call.

Ошибки различаются: Busy/Stopping/Cancelled, NotFound/NotRetained, GenerationConflict,
IdentityConflict, IntegrityFailure, BudgetExceeded, UnsupportedStoreFormat,
StorageFault/UnknownOutcome/OutcomeExpired. Diagnostics bounded, без исходных asset
bytes; retryability и committed status отражаются явно.

## Рабочие budgets AST1 — до измерений

Числа — начальные admission defaults для bounded prototype, не performance SLA.
Меняются явно с новым proof; import-specific g.0 budgets не переносятся молча.

| Ресурс | Default/max первого профиля |
| --- | --- |
| Owners |≤4active+failed stores/App;1per root;1metadata owner thread и1active write operation/store |
| Data queue |32commands и16MiB суммарного owned command payload; immediate Busy при переполнении |
| Batch |256affected assets,1024new revisions,8192dependency edges;≤4MiB serialized metadata changes |
| Record/extension metadata |≤128KiB BSON document /≤64KiB opaque metadata; limit проверяется до transaction |
| Locator/tags |≤1024UTF-8bytes locator;≤16tags/asset,≤64UTF-8bytes/tag; canonical key encoding отдельно от display strings |
| Object/stream |≤512MiB object;64KiB streaming buffer;≤16open read streams |
| Snapshot/closure |≤4096members,16384edges,64depth;≤1MiB manifest,≤64live pins |
| Query page |≤256items;≤1MiB detached response; bounded traversal без partial success |
| Catalog scale |≤100000assets,250000revisions,1000000edges;≤4096retained operation records |
| Disk admission |2GiB catalog datafile;32GiB всего managed root включая objects/staging/orphans/catalog/log/temp;≤1GiB active staging; log+temp high-water1GiB перед новой admission |
| Deadlines |Queued/metadata command10s, write staging30s, drain10s; при full queue immediate Busy; expiry не обещает остановку underlying I/O |

GC workspace имеет отдельный64MiB accounting budget и ceilings catalog scale; при
недостатке workspace mark завершается отказом до prune/delete. Snapshot closure limit
не является разрешением обрезать GC reachability. Payload accounting не равно heap/RSS.
Disk accounting не является физическим reserve свободного места; actual disk-full
проверяется. Log/temp high-water — admission/checkpoint gate, не OS hard cap внутри
текущего commit; export/backup имеют собственную квоту target. [LiteDB LIMIT_SIZE/TIMEOUT/CHECKPOINT](https://www.litedb.org/docs/pragmas/)
покрывают только соответствующие engine параметры, не heap/RSS/весь store-directory.
Режим flush/checkpoint фиксируется вместе с benchmark. [FileStorage](https://www.litedb.org/docs/filestorage/)
можно исследовать отдельным byte provider позднее; per-chunk upload пока не подменяет
staged publish v1. BSON16MB document limit не становится лимитом размера asset.

Порядок реализации/проверки AST1a–d ведётся только в [модульном PLAN](PLAN.md#litedb-ast1).
AST2a добавил public App/IFactory; AST2b — package/provider delivery; AST3 — consumer bridge/import/build.
Source console fixture не считается публичным module integration. AST1a и AST1b core
passing scope зафиксированы отдельно: process-kill recovery выполнен, safe write/cleanup/
transient close proof добавлен в AST1b.f. Native WAL/finalizer defect5.0.21 подтверждён,
physical disk-full permission-gated. Power-loss, package compatibility и весь v1 не закрыты.
