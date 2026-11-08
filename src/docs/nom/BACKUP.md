# Nom: verified backup/restore и развитие формата

<a id="nom6-backup-2026-11-08"></a>
## NOM6 — контракт 2026-11-08

Nom экспортирует согласованный локальный directory backup. Это отдельная явная
операция, без in-place migration, archive compression или implicit reset. Контракт
дополняет [формат KV](FORMAT.md), [retention](RETENTION.md) и [App ownership](APP_INTEGRATION.md).
Фактические проверки — [STATUS](STATUS.md), команды — [HISTORY](HISTORY.md#nom6-2026-11-08).

## Public API и владение

`IProjectAssetStore.ExportBackupAsync(destinationRoot, options, token)` возвращает
`NomBackupReceipt`: Root/ProjectId/StoreId/Sequence/Generation/Files/Bytes/ManifestDigest.
Files — число listed data files; пустой owner.lock и сам manifest не входят в count.
Bytes включает data и manifest. Receipt подтверждает проверку и namespace publication.

`UseNom` дополнительно регистрирует заимствованный
`IFactory<IProjectAssetStore, RestoreAssetStoreArguments>` через тот же App-owned
NomStoreHost. Typed arguments: BackupRoot, Root, expected ProjectId/StoreId, Options,
CancellationToken. Restore возвращает caller-owned обычный store; он занимает одну
из четырёх slots существующего registry, согласован с App stop/drain/failed startup.
NomStoreHost предоставляет Create overload; новых сервисных scopes/async activation нет.

```csharp
using TrueMoon.Nom;
using TrueMoon.Nom.Backup;

// Factories are constructor-injected into the consumer from UseNom registrations.
var backup = await store.ExportBackupAsync(absoluteBackupRoot);
await using var restored = restoreFactory.Create(new RestoreAssetStoreArguments(
    backup.Root, absentRestoreRoot, store.ProjectId, store.StoreId));
```

Caller закрывает snapshots/streams/store до App. Worker сериализует backup с остальными
commands. Export требует завершить reservation, закрыть read streams и продолжить
pending GC queue; runtime snapshot handles без streams допустимы. Entry/ongoing copy
удерживает source root и registry до actual IO/close. Stop/cancel waiter/timeout не
закрывает продолжающийся copy и не освобождает root раньше него.

`NomBackupOptions` — copied immutable record: MaximumFiles1..100256, MaximumBytes
256bytes..1TiB, default100256/16GiB. Это admission accounting, не физический reserve.
Destination обязан отсутствовать, parent — существовать. Local absolute paths,
volume/UNC/reparse roots и пересечение source/destination в любую сторону отклоняются.
Существующий даже пустой destination не перезаписывается.

## Export

1. Worker фиксирует exact KV sequence/generation. Source не checkpoint-ится и не
   меняется. Выбираются только nom.root, root-selected checkpoint/journals и CAS для
   **всех** stored revisions, включая пока не pruned unreachable revisions.
2. Проверяются paths/count/length/bytes до первого destination write. Staging,
   orphan CAS, obsolete metadata, unknown files не экспортируются. Same digest должен
   иметь одну declared length. Preservation all revisions сохраняет semantic exact
   reads и operation/snapshot/pin/tombstone state; GC не подменяет backup.
3. Создаётся unique sibling `<destination>.nom-<GuidN>.pending`. Каждый file читается
   ограниченным64KiB buffer, проверяются length/hash, output Flush(true), actual close
   и повторная проверка prepared file. CAS digest обязан совпасть с filename/metadata.
   Payload целиком не загружается. Read-sharing active journal учитывает существующий
   read/write handle; source owner/worker исключают concurrent Nom mutation.
4. Последними пишутся empty owner.lock и nom.backup manifest. Независимый owned KV
   verifier открывает prepared root **без tail repair**, проверяет выбранную chain,
   полный domain catalog, resource operation requests/outputs, exact dependencies,
   manifests/pins/tombstones и точное совпадение listed file closure. Incomplete tail,
   pending operation или GC queue запрещены. Проверяются обе стороны transport и domain,
   а не только checksum скопированного файла.
5. После actual close всех transfer/verifier handles — same-parent Directory.Move
   в отсутствующий destination. Copy/delete fallback отсутствует. Cancel до publication
   отклоняет операцию; после acknowledged move успех не переименовывается в cancellation.

Готовый backup содержит reserved marker nom.backup. Обычный Open такой root отвергает:
для работы с ним нужна явная restore factory. Backup остаётся immutable; его изменение
сделает subsequent verification неуспешной. Checksums обнаруживают corruption, не
подписывают archive от намеренной подмены.

## Restore и failure policy

Restore читает bounded manifest, проверяет expected identities/budgets и listed source
lengths. Затем копирует строго listed files в собственный unique sibling staging с
проверкой каждого digest и prepared contents. Создаёт empty owner.lock; **nom.backup
marker не переносится**. Domain/authority verifier работает до публикации destination.
После actual close — Directory.Move, затем обычный owned Open без изменения IDs,
sequence/generation, revision hash domains, operation retention floor и durable pins.
Отмена после acknowledged publication не отменяет final Open/возврат owner.

`NomBackupException` сохраняет cause, StagingRoot и PublicationAttempted. Lost move
ack может оставить whole valid final destination; он никогда не сбрасывается и не
перезаписывается. Caller проверяет этот root, а не начинает повтор поверх него.
Pre-publication cancellation возвращает OperationCanceledException; при созданном
prepared root `Data["NomBackupStagingRoot"]` сохраняет diagnostic path.

Failed prepared directories сохраняются. Автоматического recursive delete/recovery
по похожему имени нет. Следующий explicit retry использует новый staging и отсутствующий
destination. Cleanup снимает каждый transfer handle только после actual close;
ошибка cleanup сохраняет все causes и блокирует текущего owner до close/reopen.
Stop/Dispose повторяет retained cleanup на том же owner thread перед release KV/root.
Source остаётся usable после обычной transfer failure, если все owned handles закрыты.

Те же project/store identities в source и restored copy означают копию одного logical
store; automatic merge двух веток не предусмотрен. Restore не выдаёт новые IDs и не
объявляет прежние tickets новым identity domain.

## Manifest v1

`nom.backup`≤32MiB. Integers little-endian, counters≤Int64.MaxValue, GUID RFC/network16,
digests raw32, paths strict ordinal ASCII/UTF-8, separators `/`. Full trailing data,
неизвестные flags/versions и noncanonical order/duplicates отклоняются.

| Offset | Bytes | Значение |
| --- | --- | --- |
|0|8|ASCII NOMBK001|
|8|2|Version1|
|10|2|Header size128|
|12|4|Flags0|
|16|16|ProjectId nonempty|
|32|16|StoreId nonempty|
|48|8|Sequence>0|
|56|8|Generation≤Sequence|
|64|4|EntryCount1..100256|
|68|28|Reserved zero|
|96|32|SHA256(header[0..96))|

Затем sorted unique entries: PathLength u16 (1..160), Reserved u16zero, Length u64
(0..1TiB), Digest32, Path UTF-8 bytes. Footer32 — SHA256(header+entries). Exact manifest
size =160+Σ(44+pathUTF8Length). Header/count/size проверяются до allocation entries.
Read buffer payload≤64KiB, manifest может иметь отдельный bounded metadata buffer32MiB.

Allowed paths: `nom.root`, `journal/{D20}.nom`, `checkpoints/{lowerhex64}.nom`,
`objects/sha256/{lowerhex2}/{lowerhex64}.blob` с matching bucket. Требуются root и
как минимум один journal. Owner.lock/nom.backup/self-listing, traversal, absolute paths,
backslash, wrong buckets и unknown names в manifest запрещены. Decoder проверяет
structure; verifier отдельно доказывает authoritative selection и domain closure.

## Protocol изменения формата

Текущий runnable transport: backup-v1 / KV-v1 / NOMAST01 или02 → тот же current format.
Restore не является конвертером неизвестного future format и не читает legacy LiteDB.
Future change требует отдельного explicit versioned converter и independent goldens:

1. Read-only поддержанный source version, frozen input и declared source/target/hash domains.
2. Conversion в новый absent sibling root без записи в source; migrations IDs/revisions/
   operation tickets явно фиксируют compatibility policy до кода.
3. Verification всего target authority/domain/CAS current reader-ом; budget/cancel/fault
   policy сохраняет source и prepared target. Unsupported version отклоняется до publication.
4. После actual close — explicit publication нового root; caller отдельно переключает
   приложение. Automatic in-place upgrade, fallback на старую copy и silent revision
   rehash запрещены. Старый source удаляется только отдельным явным действием владельца.

Для несуществующего KV-v2 converter не объявлен реализованным. V1 read/reject/verified
repack/publication путь уже runnable и проверен. Фактические limits/performance —
[NOM6 workload](PERFORMANCE.md); fresh NuGet/RID/second consumer — NOM7. Physical
isolated-volume disk-full/power-loss gates NOM3/4 остаются открытыми.
