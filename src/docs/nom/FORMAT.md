# Nom binary format v1 и read-only recovery

<a id="nom0-format-2026-11-07"></a>
## NOM0 — контракт 2026-11-07

Спецификация собственного backend. NOM0 codecs/recovery и NOM1 [KV writer/root Open/repair](KV_CORE.md#nom1-kv-2026-11-07) — internal слой TrueMoon.Nom. С NOM2 App/resource API используют этот core; [catalog records](CATALOG.md#nom2-catalog-2026-11-08). Checkpoint restore/compaction реализованы в [NOM4](COMPACTION.md#nom4-compaction-2026-11-08). [План](PLAN.md#nom-own-queue).

Все integers unsigned little-endian, counters ограничены Int64.MaxValue. GUID —16bytes
RFC/network order, не Guid.ToByteArray() default. SHA-256 digests на диске —32raw bytes,
в DTO —64lowercase hex. Строки — strict UTF-8 без BOM/нормализации/culture folding.
Неизвестные версии, flags/reserved bytes и trailing bytes отклоняются. Checksums
обнаруживают повреждения, не являются подписью против намеренной подмены.

## Authority и файловая раскладка

- `nom.root` — единственный выбранный root descriptor, ровно256bytes. Отсутствие,
  повреждение или чужие IDs не разрешают Create/reset. Старый `catalog.litedb` root
  отклоняется до записи; filesystem preflight реализован в NOM1.
- `journal/{id:D20}.nom` — segments, перечисленные диапазоном root. Ни timestamps,
  ни максимальный найденный ID/epoch не выбирают authority. Неуказанные/временные
  файлы не становятся committed данными при сканировании каталога.
- `checkpoints/{digest}.nom` — exact checkpoint content; zero digest означает отсутствие.
  Root задаёт covered sequence/generation и первый следующий segment. Формат содержимого
  checkpoint и runnable restore закреплены в [NOM4](COMPACTION.md#checkpoint-format-v1).
- Segment до ActiveSegmentId закрыт и проверяется как sealed; active допускает только
  неполный EOF tail. Следующий header содержит SHA-256 всего предыдущего sealed файла,
  его конечные sequence/generation; смена IDs/пропуск сегмента отклоняются.
- Root Epoch монотонен, но не равен ContentGeneration. Root MinimumSequence — trusted
  lower bound известных commits. Он не обновляется автоматически кодеком на каждый batch.

NOM1 создаёт initial root: Epoch1, no checkpoint, first=active=1, minimum=0.
NOM4 подготавливает/flush/проверяет новый file set до публикации одного `nom.root` и сохраняет
старый set до подтверждённого switch/actual handle release. Public maintenance offline;
при corrupted current root fallback на «похожий» backup не разрешён. Process-kill и
physical power-loss acceptance для authority switch выполняются раздельно.

## Root descriptor —256bytes

| Offset | Size | Значение |
| --- | --- | --- |
|0|8|ASCII NOMROOT1|
|8|2|Version1|
|10|2|Header size256|
|12|4|Flags0|
|16|16|ProjectId, nonempty|
|32|16|StoreId, nonempty|
|48|8|Epoch≥1|
|56|8|CheckpointSequence|
|64|8|CheckpointGeneration≤CheckpointSequence|
|72|32|CheckpointDigest|
|104|8|FirstSegmentId≥1|
|112|8|ActiveSegmentId≥FirstSegmentId; window≤32segments|
|120|8|MinimumSequence≥CheckpointSequence|
|128|32|PreviousSegmentHash перед FirstSegmentId|
|160|64|Reserved zero|
|224|32|SHA256(bytes[0..224))|

No checkpoint: sequence/generation0, digest0, first1, previous hash0. Checkpoint present:
sequence>0, nonzero digest, first>1 и nonzero preceding seal hash. Decode требует
ожидаемые ProjectId/StoreId и повторно проверяет canonical encoding.

## Segment header —160bytes

| Offset | Size | Значение |
| --- | --- | --- |
|0|8|ASCII NOMSEG01|
|8|2|Version1|
|10|2|Header size160|
|12|4|Flags0|
|16|16|ProjectId|
|32|16|StoreId|
|48|8|SegmentId≥1|
|56|8|BaseSequence|
|64|8|BaseGeneration≤BaseSequence|
|72|32|PreviousSegmentHash|
|104|24|Reserved zero|
|128|32|SHA256(bytes[0..128))|

Segment1: base sequence/generation0 и previous hash0. Последующие segments требуют
nonzero predecessor digest. Одна header checksum не подтверждает существование
predecessor: recovery отдельно сверяет его sealed result/state.

## Atomic batch

Header96bytes, payload, footer48bytes. TotalLength включает всё. Payload содержит
1..8192 уникальных mutations, отсортированных по unsigned lexicographic key bytes.
Encoder сортирует input; decoder отклоняет другой порядок/дубликаты. Пустой Put value
допустим и отличается от Delete. Структурные flags0 обязательны.

| Header offset | Size | Значение |
| --- | --- | --- |
|0|8|ASCII NOMBAT01|
|8|2|Version1|
|10|2|Header size96|
|12|4|Flags0|
|16|4|TotalLength|
|20|4|MutationCount|
|24|8|Sequence≥1|
|32|8|ExpectedGeneration|
|40|8|ResultGeneration: expected либо expected+1;≤sequence|
|48|16|Nonempty OperationId|
|64|32|SHA256(header[0..64))|

Mutation: Kind u8 (1Put/2Delete), reserved3zero, KeyLength u32, ValueLength u32,
exact key bytes, exact value bytes. Delete требует ValueLength0. KV core принимает
opaque nonempty keys, domain key codec ниже дополнительно задаёт schema слою Nom.

Footer: SHA256(segmentHeaderDigest32 || batchHeader96 || payload), ASCII NOMEND01,
TotalLength u64. Любое несовпадение checksum/footer/trailing payload — corruption,
даже у последней полной записи. OperationId может повторяться для отдельных durable
шагов одного resource operation; idempotency state хранится domain control values.

Sequence возрастает ровно на1 после BaseSequence. ExpectedGeneration равен текущему
view; ResultGeneration сохраняется для metadata/reservation changes или увеличивается
на1 для content publication. Ни sequence, ни generation не переполняются.

## Recovery и неполный хвост

NomJournalRecovery.Recover заимствует readable seekable Stream с Position0. Length
снимок ограничивается до чтения; short reads поддержаны. Write/SetLength/Dispose
не вызываются. Результат содержит индекс key→(SegmentId, absolute file offset,
value length, sequence), последнее целое состояние, ValidLength и HasIncompleteTail.
Bytes ресурсов/все catalog values не сохраняются в результате replay.

Порядок проверки: segment header/expected authority → frame fixed header/length
budget/checksum → full frame/context checksum/footer → all mutations/schema/order →
sequence/generation → projected index budget → применение всей пачки. До результата
reader не получает частичный view. Cancellation/IO exceptions не превращаются в EOF.

Для EOF внутри последнего header допустим только prefix фиксированных magic/version/
flags; доступные полные length/count/counter fields тоже проверяются. Full header
с недостающим body классифицируется как incomplete только после проверки header hash,
sequence/generation и limits. Full frame с ошибкой — всегда отказ, resync по следующему
magic отсутствует. Sealed segment или невыполненный MinimumSequence запрещают возврат
с потерей требуемого состояния. Header store itself никогда не принимается усечённым.

При previous=null recovery принимает только initial segment1/base0. Для продолжения
нужен previous sealed result: точные IDs, consecutive segment ID, base counters и
actual SHA256 предыдущего файла. Индекс переносится вместе с locations/SegmentId;
Delete действует и на старые keys. Active/incomplete predecessor не допускается.
Checkpoint seed loading и authoritative filesystem owner реализованы в NOM1/4; выбор по произвольному filesystem scan запрещён.

Checksum/floor не позволяют определить намеренную обрезку active файла ровно на
старой valid границе, если root ещё не знает последующий commit. Заявленный первый
scope — process crash при сохранности уже flushed bytes, не универсальная защита
от произвольной порчи, malicious rollback или power loss.

Pure recovery не удаляет tail. NOM1 KV Open после exclusive validated chain выполняет явный truncate до ValidLength и flush; ошибки прерывают Open. После
failed write/flush запрещён дальнейший append до recovery. Полностью записанный batch
после потерянного acknowledgement может оказаться committed; ticket разрешается по
операционному журналу, а не объявляется Aborted по факту IOException.

## Canonical keys и metadata

Domain key: Version u8=1, KeySpace u8, FieldCount u16 (1..8), затем для каждого поля
Length u32>0 и bytes. Общий размер≤1024. Spaces: control1, asset16, head17, revision18,
binding19, tombstone20 (NOM5), operation32, operation-item33 (NOM2), manifest48, member49, pin50, garbage51 (NOM5), dependency64, reverse-dependency65 (reserved).
GUID fields RFC16bytes, digest fields raw32, sequence fields u64LE. Типизированные
составы catalog keys/values закреплены в NOM2; [NOM5 retention records](RETENTION.md#дополнительные-key-spaces-и-values), domain indexes восстанавливаемые.

Asset value: NOMAST01 (8), version u16=1, reserved u16=0, AssetId/SourceId/KindId по16
RFCbytes, LocatorLength u32, MetadataLength u32, strict UTF8 locator, opaque metadata.
Header68bytes, locator1..1024UTF8bytes, metadata0..65536bytes. IDs nonempty, locator
не пустой/whitespace и не содержит control chars; никакой BSON зависимости в encoding.

NOM5 добавляет [NOMAST02 links extension](RETENTION.md#metadata-и-совместимость), сохраняя точные01 bytes/hash при пустых links. Общий record≤128KiB; version u16 остаётся1, новый magic задаёт extension.

Revision = lowercase SHA256(`TrueMoon.Nom/resource/1\0` UTF8 domain || metadata value
length u32LE || canonical asset value || raw contentSHA256 || payload length u64LE).
Payload length0..512MiB. Domain завершается одним NUL byte. Это новый hash domain;
он не выдаётся за совместимый ast1b-resource/1. Старые revisions не пересчитываются
молча, legacy root не открывается с новым codec.

NOM6 добавляет независимый [backup manifest/current-format restore protocol](BACKUP.md#manifest-v1); root/journal/checkpoint wire versions не изменены.

## Numeric budgets

| Ресурс | Hard ceiling первого профиля |
| --- | --- |
| Key/value |1024bytes /128KiB|
| Batch |8192mutations /4MiB payload; frame overhead144bytes|
| Segment/root window |64MiB включая header /32segments|
| Replay |65536whole batches/segment,≤64MiB input; caller cancellation между bounded steps|
| Key index |2000000entries и256MiB logical accounting:96bytes+key length/live key|
| Scratch |Largest frame allocation≤4MiB+144bytes; decoded value≤128KiB, batch mutation array≤8192entries. Logical transient serialized buffers≤16MiB/call; inherited index copy учитывается отдельно|
| Checkpoint/compaction |Checkpoint≤256MiB, input/output journal set каждый≤2GiB; temporary disk headroom≤2GiB+256MiB+256bytes root. Enforcement и streamed checkpoint codec реализованы в NOM4|
| Root/staging/GC |Managed root≤32GiB, staging≤1GiB, GC workspace64MiB; enforcement — resource owner/maintenance NOM2/5|

NomFormatLimits принимает только положительные уменьшения ceilings. Минимум payload13,
segment160,index97. Index accounting не является CLR heap/RSS ceiling. NOM6 измерит
heap/RSS и allocations; raw key count сам по себе не означает допуск любого набора.
Recovery limits проверяются и для inherited state. При chaining временно существуют
старый и новый index, до2×256MiB logical accounting; owner не удерживает все прежние
copies. Эти ceilings не являются лимитом heap/RSS или резервом свободного диска.
Cancellation не прерывает насильно
зависший synchronous Stream.Read; owner/drain остаются ответственностью NOM1/2.

NOM1 writer подтверждает commit после Write+Flush(flushToDisk:true), см. [KV contract](KV_CORE.md#nom1-kv-2026-11-07).
[.NET Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
и [Windows FlushFileBuffers](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers)
задают primitive, но не заменяют проверку namespace publication/device durability.
В NOM0 write/flush/root switch/physical disk-full/power-loss proofs не заявлены.

## Проверки NOM0 — 2026-11-07

Финальные Nom.Tests: **233/233 каждый Debug/Release**, также solution-filter Debug233/233; failed/skipped0 (147 new cases).
Точные test names и assertions — NomFormatTests/NomRecoveryTests/NomFormatFixtures.

| Требование | Конкретная проверка |
| --- | --- |
| Независимый wire/metadata/hash golden | RootAndSegment_IndependentGoldenBytes_PinLayoutIdentityAndChecksums, MetadataAndRevision_IndependentGolden_PinUtf8GuidOrderAndHashDomain |
| Полное повреждение и строгий schema | Checksums_RejectEverySingleByteMutationOfCompleteGoldenFrames, Batch_RejectsAuthenticatedInvalidFramingAndPayload |
| Только целый view после обрыва | Recovery_EveryValidFinalFrameTruncation_RetainsOnlyPreviousWholeBatch |
| Segment continuity и старые keys | Recovery_SealedChain_CarriesOldLocationsAndKeepsPreviousViewImmutable |
| Limits и borrowed streams | Recovery_Limits_BoundSegmentIndexBytesAndReplayWithoutPartialSuccess, Recovery_IndependentGoldenJournal_IndexesExactValueLocationsWithoutWritingOrDisposing |

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug '-bl:TestResults/Nom0-Debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release '-bl:TestResults/Nom0-Release-{}.binlog'
```

SDK10.0.403/runtime10.0.14, Windows x64, xUnit2/VSTest. Scope — pure codecs/read-only
replay и повтор существующих safe Nom cases. Никаких writer/process-kill/root publication/
checkpoint restore/fresh-package/physical power-loss результатов эта запись не создаёт.
[Детальная история и evidence](HISTORY.md#nom0-2026-11-07).
