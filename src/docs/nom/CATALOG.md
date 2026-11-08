# Nom: App и resource catalog на собственном KV

<a id="nom2-catalog-2026-11-08"></a>
## NOM2 — 2026-11-08

`UseNom` и `IFactory<IProjectAssetStore, OpenAssetStoreArguments>` используют
собственный [KV core](KV_CORE.md) и [format v1](FORMAT.md). LiteDB/BSON код,
PackageReference/CPM version и vendor-specific WAL runners/downloads удалены.
Fallback/provider selector и автоматической миграции нет. Старый `catalog.litedb`
отклоняется без изменения root. Исторические документы и evidence сохраняются.

Публичные имена, App registrations, ownership и потоковый API сохранены.
Nom зависит только от TrueMoon.Contracts; App/Cobalt/Aether не входят в его graph.
Core/probe восстанавливаются с пустым package feed/cache без внешних пакетов.
Публичный App consumer отдельно использует настоящий App/Cobalt через ProjectReference,
без friend access. Это source/published proof, не NuGet delivery NOM7.

## Каталог и транзакции

`NomCatalog` — типизированный adapter, не эмуляция document database. Begin создаёт
bounded mutation overlay; Rollback отбрасывает его без записи; Commit публикует
один канонический KV batch. Чтение учитывает overlay, поэтому конфликт binding
обнаруживается и между двумя новыми assets одной пачки. Duplicate ID/binding даёт
IOException; schema/cross-reference corruption — InvalidDataException.

Logical `ContentGeneration` изменяется при публикации content или logical delete/tombstone; snapshots/pins/GC его не повышают. Физический
KV Sequence также увеличивается для control/reservation/abort/prune. Header каталога
обязан совпадать с authoritative KV generation. KV Batch.OperationId — physical
ticket, а публичный AssetWriteTicket.StoreId/Sequence/RequestDigest — durable domain
operation; эти идентификаторы не подменяют друг друга.

Все KV handles открывает/закрывает прежний dedicated catalog worker. Bounded command
queue, reserved completion slot, constructor dependencies, typed factory, App host
registry и retained stop/drain/close retry сохранены. Factory products закрывает
consumer; App host обеспечивает fallback. Timeout/отмена ожидания не освобождают
живые handles, borrowed input или owned reader. Уже закрытый handle повторно не
закрывается после потерянного close acknowledgement.

## Keys и binary values

Все keys используют [NOM0 framing](FORMAT.md): version1, space, field count и
length-prefixed bytes. Счётчики little-endian; GUID RFC/network order; hashes raw32
bytes, строки strict UTF-8. Никаких BSON/runtime type names. Значения имеют exact
8-byte magic; flags/reserved/absent fields каноничны, trailing bytes запрещены.

| Space / key fields | Value, bytes |
| --- | --- |
| Control1 / UTF-8 `catalog` | `NOMCAT01`, ProjectId16, StoreId16, Generation8;48 |
| Control1 / UTF-8 `resources` | `NOMCTL01`, Next8, Floor8;24 |
| Asset16 / AssetId16 | NOM0 `NOMAST01` или NOM5 `NOMAST02` canonical metadata/links |
| Head17 / AssetId16 | `NOMHED01`, AssetId16, Revision32;56 |
| Revision18 / Revision32 | `NOMREV01`, AssetId16, KindId16, Revision32, Digest32, Length8, AssetLength4, Asset;116+asset |
| Binding19 / BindingDigest32 | `NOMBND01`, AssetId16;24 |
| Operation32 / Sequence8 | `NOMOPR01`, Sequence8, Request32, Expected8, Total8, Count4, State1, Reserved3zero, Result8;80 |
| OperationItem33 / Sequence8, Index4 | `NOMITM01`, Sequence8, Index4, Maximum8, HasLength1, Length8, HasDigest1, Digest32, HasRevision1, Revision32, AssetLength4, Asset;107+asset |

State0=pending,1=committed,2=aborted. Committed Result=Expected+1; иначе Result0,
Revision отсутствует. Optional flags0/1; отсутствующие numeric/digest bytes zero.
Next>0,0≤Floor<Next, retained operations Floor<Sequence<Next. Items имеют contiguous
indexes0..Count-1, unique AssetIds в Guid order; Count1..256. Sequence gaps после
неудавшегося reserve разрешены и никогда не переиспользуются.

Binding — length-prefixed SHA256 над доменом `TrueMoon.Nom/binding/1`, Source,
Kind и ordinal Locator. Request domain — `TrueMoon.Nom/request/1` плюс StoreId,
expected generation, total и canonical descriptors. Resource revision использует
NOM0 `TrueMoon.Nom/resource/1\0`. Это новые hash domains/metadata bytes: прежние
AST revisions/tickets не совместимы. Даже при одинаковом SHA content identity
logical AssetId и exact Revision остаются разными понятиями.

## Resource publication и recovery

1. Reserve фиксирует high-water Next отдельным durable batch до pending operation
   и items. Ошибка allocation не выдаёт ticket; uncertain allocation требует reopen.
2. Borrowed streams читаются short-read/non-seekable путём с64KiB buffer. Length,
   digest и total проверяются; staging flush-ится и закрывается, CAS no-overwrite
   move завершается до publication. Существующий object проверяется тем же handle.
3. Один KV batch меняет asset/binding/revision/head/items, operation outcome и
   logical generation. После начала KV write/flush ошибка означает Unknown с ticket,
   owner блокирует операции до close/reopen. Pre-IO quota/conflict не становится Unknown.
4. Committed retry возвращает exact durable outcome и повторяет cleanup без чтения
   replacement streams. Abort/prune не изменяют ContentGeneration и не удаляют CAS.

Open сначала проверяет всю journal chain и читает verified prefix **без tail repair**.
Далее обязательны header/control, canonical key/value correspondence, asset↔binding,
head↔asset/revision/metadata, operation ownership/closure/request, declared total,
expected/result generations и committed outputs. Только после полной domain validation
`CompleteRecovery` обрезает допустимый incomplete active tail/flush и abort-ит pending
operations с очисткой принадлежащих им staging files. Логическое повреждение никогда
не маскируется как EOF, не создаёт отсутствующий control и не запускает cleanup.

ReadPage сортирует ограниченный top-K набор keys (≤256), возвращает detached rows
≤1MiB с exclusive continuation/consistent Sequence+Generation. Каталог проверяет,
что sequence между страницами не меняется. Asset list по-прежнему≤256/1MiB, operation
history≤4096. [NOM5](RETENTION.md) добавляет source/kind/tag pages, exact dependencies, frozen manifests/pins и bounded offline GC.

NOM6 [backup/restore](BACKUP.md) сохраняет all stored revisions/authority/retention через verified immutable directory и typed App factory. [Ordered KV pages и устранение повторного decode/IO](PERFORMANCE.md) сохраняют schemas/hash domains; DomainIndexBytes настраивается через UseNom (default64MiB, opt-in≤256MiB).

## Дополнительные пределы

Помимо NomOptions.BatchBytes и DocumentBytes, **полная атомарная публикация обязана
помещаться в4MiB KV payload**. Preflight до admission:183+Σ(3×assetBytes+567)≤4194304.
При republish tombstone добавляется36bytes/delete key до reservation. Это консервативная оценка keys/entry headers, трёх копий metadata, header/outcome и
возможного удаления прежнего binding. Последняя достижимая граница4194303 принята
и опубликована тестом;+1metadata byte даёт4194306 и отклоняется без IO/sequence.
Resource bytes имеют отдельные512MiB/object и1GiB/batch ceilings.

NOM3 дополнительно резервирует space/replay/index capacity до allocation:
3frames,344+Σ(4L+710)payload bytes плюс framing,5n+1keys/112+632nindex bytes.
[Полная budget matrix и stop/recovery fixes](RECOVERY_ACCEPTANCE.md#исправленные-состояния).

Prune — одна atomic batch. Большой диапазон может превысить8192mutations/4MiB и
отклониться до IO без частичного удаления/floor advance; caller выбирает меньший
диапазон. Segment64MiB/65536frames остаётся явным capacity refusal. Automatic rotation,
checkpoint/compaction реализованы в [NOM4](COMPACTION.md), public CompactAsync требует закончить reservation и закрыть read leases. NOM3 IO/process matrix проверена; physical disk-full gate открыт.
OpenResourceAsync полностью проверяет hash открытого handle до возврата Stream;
немедленная выдача первого chunk/progressive integrity не обещана.

## Повторение

Из src, .NET10/PowerShell7, Windows x64:

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug '-bl:TestResults/Nom2-Debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release '-bl:TestResults/Nom2-Release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomCore
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/MyFreshNomApp
```

Фактические counts/commands/evidence — [STATUS](STATUS.md) и
[HISTORY](HISTORY.md#nom2-2026-11-08). Process kill не является power-loss acceptance.
Physical disk-full, hostile/reparse filesystem, supported RID matrix, performance,
snapshots/GC и fresh packages этим этапом не закрыты.
