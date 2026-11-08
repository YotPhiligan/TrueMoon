# TrueMoon.Nom: план самостоятельного модуля хранения

<a id="current-assets-queue"></a>
<a id="current-nom-queue"></a>
<a id="nom-own-queue"></a>
## Текущая очередь — полная замена LiteDB, 2026-11-07

**Принято пользователем: Nom получает собственную реализацию хранения. LiteDB
полностью удаляется из целевой поставки, кода, тестов и исполняемых probes.**
Варианты vendor fix/upgrade/process isolation LiteDB и optional provider сняты с
активной очереди. [Архитектурное решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07).

NOM0 закрепил [format v1](FORMAT.md), NOM1 реализовал [KV core](KV_CORE.md),
а **NOM2 выполнен2026-11-08**: [App/resource catalog](CATALOG.md#nom2-catalog-2026-11-08)
переключён на собственный KV; LiteDB/BSON source/package/vendor runners удалены.
Семантика public UseNom/streams/CAS/journal/lifecycle перепроверена в source/publish.
Фактические counts и scope — [STATUS](STATUS.md), [HISTORY](HISTORY.md#nom2-2026-11-08).

**NOM3 проверен2026-11-08 в deterministic IO/process scope:**426cases D/R,
41integrated kill groups/profile, capacity/poisoned-read/reserving-stop fixes.
[Матрица](RECOVERY_ACCEPTANCE.md#nom3-recovery-2026-11-08), [evidence](HISTORY.md#nom3-2026-11-08).
По решению пользователя аппаратный power-off исключён из текущего Nom acceptance; прежний отказ
Windows authorization без Administrator снят UAC запуском NOM7: isolated-volume
disk-full/recovery проверен в5cases/profile; [границы](DURABILITY.md).

**NOM4 реализован и проверен2026-11-08:** [checkpoint, explicit rotation/offline compaction](COMPACTION.md#nom4-compaction-2026-11-08),
536cases D/R,92console groups/profile и16public App checks.
**NOM5 завершён2026-11-08 в IO/process scope:** [indexes/snapshots/pins/GC](RETENTION.md),
628cases D/R,104groups/profile и27public App checks. NOM6 [backup/restore](BACKUP.md) и [workload/limits](PERFORMANCE.md) проверены2026-11-08. **NOM7 [package delivery](DELIVERY.md) проверен2026-11-08:** четыре fresh packages, public App34checks и самостоятельный NomAssetTool211checks/profile; portable Debug/published Release и win-x64 framework-dependent Release. Повторены718cases D/R и120functional groups/profile; heavy capacity/workload остаются NOM6 evidence. Physical NOM3/NOM4 остаётся
открытым guest-VM acceptance до actual trials; hardware power-off исключён, isolated-volume disk-full проверен NOM7 UAC. Automatic scheduling нет; engine bridge согласуется отдельно.

**Решение пользователя2026-11-08 — actual VM crash verification отложена.**
[Стенд](VM_CRASH.md) и предыдущие98guards/10host protocol groups D/R сохраняются;
actual VM0trials, acceptance не закрыта. Подготовка image/credential не является
текущей задачей и не блокирует независимые Nom/engine features. Возобновлять actual
VM run только по новому запросу пользователя. Hardware power-off остаётся исключённым.
Кодовая очередь engine f.3/f.4/g.1 не меняется; Nom bridge g.2 зависит от g.1.

**NOM8a реализован2026-11-08 — byte Memory/Span API:** [контракт](BUFFER_API.md#nom8-buffer-api)
добавляет borrowed memory input/batch, whole-resource destination read,
explicit-copy Span publication и bounded detached load. Extension layer сохраняет
IProjectAssetStore, App registration, journal/CAS и format. Memory replay не читает
caller bytes; Copy всегда snapshot-ит span до Task return, включая replay.
Фактические unit/source/publish/fresh NuGet результаты — [STATUS](STATUS.md),
[история](HISTORY.md#nom8a-2026-11-08). **NOM8b реализован2026-11-08:** [raw T:unmanaged](BUFFER_API.md#nom8b-raw-api)
publication/copy/read, checked long byte sizing и explicit layout/endianness/padding;
Read возвращает elements, exact byte length обязана быть кратна sizeof(T).
Memory/readonly/Span inference, non-pinnable owners, partial-element failures и
managed-type compiler rejection проверяются в [NOM8b evidence](HISTORY.md#nom8b-2026-11-08).
Arbitrary managed serialization не вводится.
Сохраняется единый journal/CAS/ownership pipeline, Stream API и package compatibility.
Это ресурсные helpers, не публичный generic KV API. VM acceptance остаётся отложенной.

## Границы и принятые решения

- Самостоятельный модуль вне Aether; engine/editor/importer/audio остаются consumers.
- Каталог: single-writer transactional KV, append-only batches, bounded in-memory
  key index, checkpoint и offline compaction. Крупные bytes — immutable CAS files.
- Atomic batch + expected generation, stable logical IDs, exact revisions и durable
  operation outcomes. Storage не импортирует/декодирует данные и не владеет GPU/voice.
- Потоковая запись/чтение геометрии, изображений, шейдеров и других opaque ресурсов;
  bounded buffers, length/hash, borrowed inputs и owned read streams сохраняются.
- UseNom/App, constructor DI, typed IFactory и retained stop/drain/dispose — штатный
  путь. Generic public KV API, новые DI scopes и provider selector не вводятся автоматически.
- Новый format version; старые LiteDB roots отклоняются без изменения данных.
  Нет implicit migration/reset или legacy LiteDB reader в runtime. NOM0 явно фиксирует
  новый codec/hash domain и последствия для revision compatibility.
- Tag/reverse/dependency queries, snapshots/pins/GC и backup/export остаются требованиями
  Nom. Универсальный query engine и multi-process writers в первый профиль не входят.

## Реализация собственного хранилища

NOM0–2 проверены; NOM3 deterministic IO/process acceptance проверена, physical gate открыт. NOM4 maintenance проверена в IO/process scope, physical gate открыт. NOM5 retention/indexes/GC проверены в IO/process scope. NOM6 backup/limits/workload проверен в IO/process scope. NOM7 выполнен в local Windows x64 portable/win-x64 framework-dependent scope; external publish и остальные платформы не подтверждены.

| Этап | Результат | Критерий завершения |
| --- | --- | --- |
| NOM0, проверено2026-11-07 | [Format v1 и codec/read-only recovery](FORMAT.md#nom0-format-2026-11-07) | Magic/version/IDs, canonical key/value encoding, revisions/hash domains, checksum и batch framing, sequence/generation, tail/corruption policy, segment/checkpoint authority и OS/filesystem durability assumptions зафиксированы. Numeric memory/index/replay/disk budgets и independent golden vectors; old-root rejection/compatibility policy до записи |
| NOM1, проверено2026-11-07 | [Runnable transactional KV core](KV_CORE.md#nom1-kv-2026-11-07) | Create/Open, exact Get, bounded Put/Delete batch + expected generation, consistent read view, один writer, durable acknowledgement/Unknown и reopen/replay. Частичная запись/flush/cancel/kill, wrong format/IDs/oversized/corrupt frames проверены; нет частично видимой пачки, silent reset и append после ambiguous failure |
| NOM2, проверено2026-11-08 | [App/catalog cutover и удаление LiteDB](CATALOG.md) | Resource catalog/journal/heads/CAS/streams используют собственный core, UseNom/factory/lifetime работают. Семантические App/streaming/identity/fault tests перенесены. LiteDB/BSON implementation, package/CPM entry и vendor-specific runners/downloads удалены; dependency graph/source/output audit, build и source/published regression подтверждают отсутствие LiteDB |
| NOM3, частично2026-11-08 | [Fault/recovery/lifecycle acceptance](RECOVERY_ACCEPTANCE.md), deterministic/process профиль проверен; physical gate открыт | Zero/partial/full writes before throw, flush, lost ack, cancel/commit race, abort/recovery/cleanup failure, actual handle close и stop retry. Bounded killed/reaped children на каждом durable cutpoint, old либо new whole state и resolved ticket, bounded resources. Physical isolated-volume disk-full отдельно; power-loss не выдаётся за process kill |
| NOM4, проверено2026-11-08 в IO/process scope | [Checkpoint и offline compaction](COMPACTION.md), physical gate открыт | Bounded replay после checkpoint; verified new file set и crash-safe authority switch; old set удерживается до release readers. Corrupt/missing checkpoint, interrupted rotation/compaction, disk-full и cleanup failure оставляют восстановимое состояние; не теряются current keys, tombstones и operation floor |
| NOM5, проверено2026-11-08 в IO/process scope | [Domain indexes, snapshots/pins и offline GC](RETENTION.md) | ID/source/kind/tag pages, forward/reverse exact dependencies и bounded closure; stable identity/tombstones. Frozen manifests удерживают old bytes после нового import; durable pins/streams защищают от GC до actual release. Mark/dry-run, interrupted prune/delete и reopen recovery без dangling live refs |
| NOM6, проверено2026-11-08 в IO/process scope | [Backup/format protocol](BACKUP.md), [limits/performance](PERFORMANCE.md) | Verified consistent export/restore, explicit format migration protocol без legacy LiteDB runtime. Corpus1k/4k/16k, затем50k/100k/300k и заявленные max limits;16/128MiB bytes, cold/warm/reopen/compaction, p50/p95/allocations/RSS/disk amplification и бюджет отказов. Никакого неподтверждённого обещания выигрыша над LiteDB |
| NOM7, проверено2026-11-08 в local Windows x64 scope | [Package delivery и consumers](DELIVERY.md) | Собственный Nom в source Debug/published Release/fresh isolated NuGet App consumer, без friend access/Aether/LiteDB; metadata/licenses/dependency/output audit и supported OS/RID matrix. NomAssetTool подтверждает standalone scope,211checks/9successful+2rejected processes/profile; public App34checks. Portable Debug/Release и win-x64 framework-dependent Release проверены, остальные платформы/modes и external publish отдельно; engine g.2 bridge согласуется отдельно |
| NOM8a, реализован2026-11-08 | [Byte buffer API](BUFFER_API.md#nom8-buffer-api), memory/batch/copy/read/load и lifecycle | Byte mutable/readonly variants без overload ambiguity; Span snapshot до Task return, Memory borrowed до actual terminal state, no caller disposal. Checked byte ceilings, non-array/non-pinnable memory, empty/slices/tails/cancel/Stop/unknown/replay/atomicity; stream-equivalent exact versions, source/published/fresh-package regression и bounded IO allocation measurements |
| NOM8b, реализован и проверен2026-11-08 | [Explicit raw T:unmanaged helpers](BUFFER_API.md#nom8b-raw-api) поверх byte/Stream pipeline | Checked long elements×runtime sizeof(T) до projection/allocation, divisibility и element return counts; mutable/readonly variants/type inference. Layout/endianness/nonzero padding принадлежат consumer, format/type info автоматически не добавляются. Typed boundaries/overflow/non-pinnable ownership/cancel/Stop/unknown/replay, source/published/fresh-package acceptance и managed-T compiler rejection проверены |

NOM0–NOM8 выполнены в описанных локальных scope; NOM3/4 physical VM acceptance
остаётся отложенной. Следующий storage consumer bridge — engine ARCH1g.2 после
g.1 importer adapter, по согласованию владельца engine.

### Структура исходников — 2026-11-08

По прямому запросу пользователя реализовано [функциональное размещение](STRUCTURE.md#nom-source-layout-2026-11-08):
Catalog, Resources, Retention, Backup, Hosting, Storage и Storage/Format.
Namespace соответствует пути; partial-типы сгруппированы рядом с основным классом.
Публичные subsystem types перенесены вместе с реализациями, consumers/imports и
примеры обновлены. Это alpha API namespace migration с пересборкой clients, без
aliases старых namespace; формат и логика хранилища сохраняются. Результаты
проверок — [HISTORY](HISTORY.md#nom-structure-2026-11-08). Очередь функций не меняется.

### Решения реализации NOM5 — 2026-11-08

NOMAST02 только для nonempty tags/dependencies; old01 bytes/hash и5argument constructor
сохранены. Индексы rebuilt из canonical authority и подготовлены до content commit;
64MiB — logical accounting, не RSS. Frozen exact manifests/durable pins/runtime leases
разделены; actual stream close удерживает protection после snapshot Dispose.
GC явный, порциями2048revisions/objects и HasMore, parents-before-dependencies,
atomic prune+intent queue до delete и explicit resume после recovery. Никакой PID/age
expiry или automatic GC. Полные numeric/API/wire/ownership criteria — [контракт](RETENTION.md).
NOM6 измеряет текущий full metadata index rebuild и выполняет backup/export/limits;
NOM3/4 physical gates сохраняются отдельно.

### Решения реализации NOM6 — 2026-11-08

Backup — immutable directory/current-format manifest1; typed restore factory через
existing App host, all stored revisions/CAS сохраняются. Prepared file/domain verification
и actual close до publication; unknown format не мигрируется автоматически. Future
converter требует explicit source/target/identity protocol и нового root, source untouched.
DomainIndexBytes opt-in≤256MiB, default64MiB сохранён: большой corpus выполнен с opt-in,
default отказал явно. Actual default KV ceilings и resource maxima проверены отдельно.
KV pages sorted/binary, duplicate metadata IO/decode устранены; полный domain index
pass на content commit пока сохраняется и измерен. Incremental update — адресный
follow-up при необходимости consumer, не скрытая смена принятого алгоритма.
NOM7 отвечает за package/second consumer/RID; physical NOM3/4 gates не закрываются.

### Решения реализации NOM7 — 2026-11-08

IsPackable=true, самостоятельный PACKAGE.md, LICENSE/icon и minimal Contracts dependency.
Без нового публичного storage API, backend/provider selector или изменения общей версии.
Fresh local alpha closure из четырёх согласованных пакетов; source compiler cache и
consumer cache разделены, fallback/ProjectReference у package consumers запрещены.
Два различных assembly names вне friend list; actual negative controls CS0122/NU1101.
Самостоятельный App/typed-factory NomAssetTool и named receipt подтверждают exact
retention/backup/restore/GC/reopen в отдельных процессах. [Контракт](DELIVERY.md),
[фактические проверки](HISTORY.md#nom7-2026-11-08). Local Windows x64 scope принят явно:
portable и win-x64 framework-dependent; это не external publication или universal RID
acceptance. Следующее расширение — consumer-requested engine g.2 bridge либо отдельная
platform/physical release проверка; engine f.3→f.4/g.1 порядок не меняется.

## Общие acceptance gates

- Один источник metadata authority, atomic whole-batch publication и однозначный
  recovery outcome по operation ID; no silent replay старого/expired ticket.
- Новый journal проверяется independent decoder/model и golden bytes, а не только
  roundtrip одним codec. Нарушение подтверждённой области не скрывается как EOF.
- Bounded record/key/value/batch/index/queue/replay/staging/compaction resources;
  полный scan или allocation не обходят admission budgets.
- Stop/timeout/cancel/failed startup не освобождают продолжающуюся IO; cleanup errors
  сохраняют исходные причины, committed cleanup retry не читает replacement input.
- LiteDB отсутствует в production/test/probe references, restored graph, publish и
  fresh packages; нет fallback/provider/fork/worker с этой зависимостью.
- Public API/IDs/hash-domain compatibility проверяется явно. Old-format root остаётся
  неизменённым после rejection; consumer не получает незаметно новый пустой store.
- Source/published/fresh-package proofs и physical environment gates отделены.
  Прежние86tests/18groups/8vendor crashes не являются результатами нового backend.

<a id="litedb-ast1"></a>
## Исторические AST этапы и перенос требований

Идентификаторы AST сохраняют прежний смысл. LiteDB реализация остановлена по решению
пользователя; незавершённые этапы не помечаются выполненными. Полный прежний план —
[архив](HISTORY.md#nom-before-own-store-2026-11-07).

| Прежний этап | Исторический результат / дальнейшее место требований |
| --- | --- |
| AST0 | Provider research/comparison и первоначальный выбор LiteDB. Позднее заменён решением собственного backend; [сравнение](COMPARISON.md) сохраняется как история |
| AST1a | [Runnable LiteDB proof](AST1A_PROTOTYPE.md),10groups D/R на старом backend |
| AST1b | [Streaming/CAS/journal core](AST1B_STREAMING.md),18groups source/published; семантика переносится в NOM2/3 |
| AST1b.f | Частично:86safe cases D/R и8fatal WAL children. Vendor fix/isolation больше не планируются; общие IO/disk/cleanup требования переходят в NOM3 |
| AST1c | Не реализован; indexes/snapshots/pins/offline GC перенесены в NOM5 |
| AST1d | Не завершён; checkpoint/maintenance/backup/limits/performance — NOM4/6 |
| AST2a | UseNom/public factory/store/App lifecycle реализованы на старом backend. Сохранение и повторная проверка — NOM2 |
| AST2b | Не выполнен; LiteDb provider/package split отменён. Собственная package delivery — NOM7 |
| AST3 | Consumer integration не выполнена; требования сохраняются в NOM7 и engine ARCH1g.2 |

<a id="nom-fault-queue"></a>
## Прежний fault blocker и новое направление

[WAL/finalizer defect LiteDB5.0.21](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07) остаётся
подтверждённым результатом старой реализации. Отказ от backend не является исправлением
vendor defect. NOM0–2 foundation/core/App cutover и NOM3 deterministic/process профиль
проверены. Прежний physical disk-full permission gate снят NOM7 UAC проверкой нового
owned NTFS/VHDX; injected ошибки и killed children сами по себе этого не доказывают.
Actual VM crash acceptance отложена пользователем, hardware power-off исключён.

<a id="nom-app-queue"></a>
## App и поставка после смены backend

UseNom/public storage API сохранены и проверены на собственном backend в NOM2.
Реальные App/DI/failed startup/drain/retained cleanup tests перенесены и прошли.
NOM7 поставляет собственный package и independent consumers в [проверенном local scope](DELIVERY.md).
Отдельный TrueMoon.Nom.LiteDb не создаётся; IsPackable=true. Внешняя публикация,
другие OS/RID/modes и physical release acceptance не следуют из local delivery proof.

## Связь с engine roadmap

Детальная storage очередь — NOM0–NOM8 в этом PLAN. Engine f.3→f.4 и importer
g.1 не переупорядочены; storage не зависит от World, renderer, GPU, UI или Assimp.
Model import g.2, build/runtime и audio используют согласованные resource/snapshot
contracts как consumers, их реализации не переносятся в Nom.

Заявки потребителей — [общие правила](../requests/README.md), module nom.
Текущий прямой запрос не подменяет engine/alloy claims.
