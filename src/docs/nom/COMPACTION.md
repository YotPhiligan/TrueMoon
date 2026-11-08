# Nom: checkpoint, rotation и offline compaction

<a id="nom4-compaction-2026-11-08"></a>
## NOM4 — 2026-11-08

`IProjectAssetStore.CompactAsync(token)` явно сохраняет live metadata в checkpoint,
выбирает новый journal и освобождает прежний набор. Это offline metadata maintenance:
consumer заканчивает reservation и закрывает read streams перед вызовом. Auto-trigger,
CAS GC, snapshot pins, backup/export и format migration сюда не входят.
[Текущие результаты](STATUS.md), [команды/evidence](HISTORY.md#nom4-2026-11-08).

Public `NomCompactionReceipt` содержит Epoch, Sequence, Generation, Keys,
CheckpointBytes, CleanupPending и Changed. ContentGeneration, logical IDs/revisions,
resource operation outcomes и retention floor сохраняются. Repeated call без новых
KV batches не повышает epoch; он может повторить pending cleanup. Empty raw store
sequence0 тоже no-op. Успешный checkpoint содержит и metadata-only изменения, даже
если content generation равен0.

## Authority и переключение

1. Serialized owner проверяет ready/no-tail, byte/index/file budgets. Сначала
   закрываются/удаляются ранее abandoned/obsolete managed files; ошибка cleanup
   возвращает Changed=false/CleanupPending=true, без нового checkpoint.
2. Все live opaque keys сортируются ordinal byte order. Checkpoint записывается
   через bounded chunks в `checkpoints/{epoch:D20}.pending`, flush-ится, фактически
   закрывается и читается заново с проверкой hashes/schema/counters/index bounds.
3. No-overwrite move публикует `checkpoints/{wholeSHA256}.nom`. Новый segment с ID
   `oldActive+1`, base sequence/generation и SHA прежнего active segment проходит
   write/flush/close/read verification, затем move из `journal/{id:D20}.pending`
   в `journal/{id:D20}.nom`.
4. `nom.root.switch` write/flush/close/read проверяет полный256-byte root. Epoch
   повышается на1; checkpoint покрывает текущие sequence/generation; first=active
   указывает новый segment, MinimumSequence равен checkpoint sequence.
5. Exclusive owner.lock удерживается при закрытии прежнего root handle, same-directory
   overwrite rename switch→nom.root и открытии/проверке нового root. Выбор authority
   не зависит от timestamps/max найденного ID. Никакого fallback на orphan/backup.
6. Index locations переключаются на checkpoint (SegmentId0), новые writes идут в
   новый journal. Только затем закрываются старые handles и удаляются obsolete files.

Primitive — [.NET File.Move(source,destination,true)](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0)
между двумя именами одной проверенной local directory. Нет copy/delete fallback
в коде Nom. [Win32 MoveFileEx](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)
различает replacement и cross-volume copy; сетевые/reparse roots не поддерживаются.
Альтернативный [ReplaceFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)
имеет документированные failure states с отсутствующим destination, поэтому не
выбран для switch. Это primitive и проверенный process profile, не universal
power-loss/device-cache guarantee.

Ошибка после начала preparation делает owner faulted: данные читаются только после
close/reopen. `NomCompactionException.RootPublicationAttempted=false` означает, что
root rename ещё не вызывался; true требует авторитетного reopen для выяснения epoch.
Cancelled preparation до switch сохраняет cancellation, но owner тоже требует reopen.
После switch token не превращает успешное переключение в отмену. Cleanup failure после
выбранного root возвращает receipt с CleanupPending; старые файлы и ещё живые handles
сохраняются до actual release/retry. Dispose также удерживает root при failed handle close.

## Checkpoint format v1

Header128bytes, footer32bytes. Integers little-endian, counters≤Int64.MaxValue;
GUID RFC16bytes; hashes raw32. Строк/runtime type names нет.

| Offset | Bytes | Поле |
| --- | --- | --- |
|0|8|ASCII NOMCKP01|
|8|2|Version1|
|10|2|Header size128|
|12|4|Flags0|
|16|16|ProjectId|
|32|16|StoreId|
|48|8|Epoch>1|
|56|8|Sequence>0|
|64|8|Generation≤Sequence, включая0|
|72|8|PreviousSegmentId≥1, меньшеInt64.MaxValue|
|80|32|SHA256 прежнего sealed active segment|
|112|4|EntryCount|
|116|12|Reserved zero|

Record: KeyLength u32>0, ValueLength u32≥0, exact key bytes, exact value bytes.
Keys strictly increasing/unique; values opaque. Footer=SHA256(header||records);
root.CheckpointDigest=SHA256(entire file including footer). Reader проверяет оба,
exact length/count, root IDs/epoch/counters/predecessor и budgets до результата.
Empty checkpoint160bytes допустим после удаления всех keys. Tombstones сохраняются
семантически как отсутствие: root больше не replay-ит прежние journals, удалённые
keys не воскресают. Operation control/floor/outcomes/items — обычные live values.

Open читает только root-selected checkpoint и root-listed journal chain; cp seed
задаёт locations в checkpoint stream. First journal должен точно продолжать
checkpoint sequence/generation/previous segment anchor. Sealed successors и active
tail проверяются прежним codec. Missing/corrupt checkpoint отвергается до tail repair
и cleanup даже при наличии прежнего корректного file set. Catalog domain validation
по-прежнему предшествует CompleteRecovery.

Low-level physical write tickets≤CheckpointSequence теперь явно `Expired`, а не
NotCommitted. Unknown write owner не допускает compaction до reopen/resolution.
Публичные resource tickets продолжают разрешаться через сохранённые catalog values;
только явный PruneResourceOperationsAsync меняет их retention floor.

## Пределы и владение

- Checkpoint≤256MiB, key≤1024, value≤128KiB, logical index≤2million keys/256MiB.
  Maintenance output checkpoint+segment header+root≤2GiB+256MiB+256bytes.
- Value IO chunks≤64KiB; не создаётся buffer размером с checkpoint. Sorted key refs
  bounded≤2million. В фазах validation/replay возможны несколько bounded index copies;
  logical accounting не является измерением heap/RSS/SLA, measurements — NOM6.
- Scan metadata directories≤1024entries, включая unmanaged names; для нового set
  preflight оставляет2slots (existing≤1022). No-op может работать при1024.
- Cleanup удаляет только unselected journal `{D20}.nom/.pending`, checkpoint
  `{hex64}.nom/{D20}.pending` и `nom.root.switch`. Unmanaged lookalikes сохраняются;
  reparse targets отклоняются. CAS objects/staging не входят в этот cleanup.
- Один worker/serialized KV lock; Get/GetMany/ReadPage возвращают detached data,
  не удерживают старые backing handles. Public offline API отклоняет active read
  leases/reservations. Store/host/typed factory ownership и retained stop остаются прежними.

## Проверки и повторение

`Verify.ps1 -Profile Maintenance` запускает27groups source Debug/published Release:
24actual killed/reaped children на cp/segment/root writes+flush+publish, root close/
switch, obsolete handle close/delete;3semantic groups проверяют повторные epochs,
append/delete continuity, retained public operations/CAS/floor и corrupt selected
checkpoint без fallback/mutation. Default All запускает Core18+KV6+Fault41+Maintenance27.
Public `VerifyApp.ps1` теперь16checks/profile, включая CompactAsync без friend access.

```powershell
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Debug '-bl:TestResults/Nom4-Debug-{}.binlog'
dotnet test TrueMoon.Nom.Tests/TrueMoon.Nom.Tests.csproj -c Release '-bl:TestResults/Nom4-Release-{}.binlog'
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNom4
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/MyFreshNom4App
```

Physical isolated-volume disk-full NOM3/NOM4 и power-loss по-прежнему открыты.
Свежий New-VHD64MiB снова отклонён Windows authorization, Administrator=false;
файл/том не создан/не подключён/не форматирован. Safe injected disk-full и process
kill не закрывают эту проверку. В следующем срезе [NOM5 domain indexes/snapshots/pins/offline GC](RETENTION.md)
реализованы и проверены в IO/process scope; performance/export — NOM6,
fresh NuGet/RID delivery — NOM7. Описанный выше VHD отказ относится к запуску NOM4.

**NOM7 UAC2026-11-08:** checkpoint на реально полном owned128MiB NTFS/VHDX и retry
после освобождения filler прошли Debug/published Release fresh-package
consumer; exact old authority/content/acknowledged outcome сохранились. Все5disk-full
cases/profile завершились, VHDX отключён/cleanup0. [Полный scope](DURABILITY.md),
[команды/исправления harness](HISTORY.md#nom7-uac-2026-11-08). Это не physical power-loss proof.
