# Nom KV core: Create/Open/Get/WriteBatch

<a id="nom1-kv-2026-11-07"></a>
## NOM1 — реализация 2026-11-07

Рабочий low-level KV owner поверх [binary format v1](FORMAT.md#nom0-format-2026-11-07).
NomKvStoreFactory создаёт caller-disposable NomKvStore и удерживает его в registry
до фактического закрытия handles. Все типы этого слоя пока internal; tests и Probe
используют friend access. С NOM2 UseNom/public resource API используют этот core:
[adapter, records и recovery ordering](CATALOG.md#nom2-catalog-2026-11-08).

## API и область

| API | Результат |
| --- | --- |
| Factory.Create(NomKvOpenArguments) | Создать новый store в отсутствующем или пустом local root |
| Factory.Open(arguments) | Открыть существующий root с точными ProjectId/StoreId и copied limits |
| Get(byte[] key) | Detached value, null если ключ отсутствует; empty Put возвращает empty array |
| GetMany(keys) | Detached values плюс consistent Sequence/Generation под одной serialized operation |
| WriteBatch(NomJournalBatch, token) | Atomic Put/Delete batch с exact next Sequence и ExpectedGeneration, committed receipt/ticket |
| LookupOutcome(ticket) | Committed/NotCommitted по exact physical commit после validated Open; covered by checkpoint → Expired, без повторного исполнения |
| Dispose | Закрыть admission, дождаться вошедшей операции, закрыть реальные handles, освободить root и registry |

Factory — composition root low-level graph: зависимости через constructor; raw
FileStreams создаются только отвечающим за них IO owner. С NOM2 graph регистрируется
через App/Contracts/typed factories. Новых public generic KV API/DI scopes нет.

Один синхронный owner сериализует Get/GetMany/WriteBatch/Lookup/Dispose. Вошедшая IO
может блокировать вызывающий thread; cancellation не обещает прервать зависший native
filesystem call или ожидание Monitor. App queue/async drain policy описана в NOM2.
Sequence/Generation свойства диагностические; для одной согласованной пары и values
используется GetMany. Writer не требует thread affinity.

## Create и authoritative Open

Create валидирует полные local paths/IDs/limits/token до admission/IO. Volume root,
UNC, пустые IDs и unsupported reparse paths отклоняются. В новом/пустом root:

1. Создаётся exclusive owner.lock и journal directory.
2. Segment1 header записывается/flush-ится.
3. nom.root.pending записывается/flush-ится и закрывается фактически.
4. No-overwrite move публикует nom.root последним; root открывается и проверяется.

При failed/cancelled Create uninitialized files сохраняются для явной диагностики,
а не удаляются/сбрасываются скрыто. Повторный Create в nonempty root отказывает.
Если publication успела пройти, после actual cleanup можно Open с исходными IDs.
Исключение Create не означает автоматически отсутствие созданного store.

Open до lock mutation читает exact nom.root и проверяет IDs/schema/checkpoint policy;
legacy catalog.litedb root отвергается без изменения файлов. Owner.lock открывается
FileMode.Open, missing lock не создаётся. Все root-listed segments1..Active читаются
с derived filenames и проверяются по sealed SHA/base counters. Неуказанные files и
nom.root.pending не выбирают новую authority. Checkpoint restore поддержан в NOM4 только для root-selected exact digest/anchor. Reparse prechecks не являются handle-verified hostile-filesystem isolation.

Полный root/chain/index/limits proof предшествует repair. Только valid incomplete EOF
tail активного segment можно обрезать до ValidLength, затем Flush(true). Полный
corrupt frame, чужие IDs, missing predecessor или trusted sequence floor не приводят
к «ремонту»/новому пустому store. RecoveredTailBytes отражает удалённый incomplete tail.
Ошибки truncate/flush прерывают Open и сохраняют committed prefix для следующей попытки.

Пока owner жив, реальные root/active/sealed file handles с FileShare.Read исключают
обычный external writer; owner.lock отдельно исключает другие Nom owners/processes.
Get использует verified immutable prefix и тот же owned handle, без повторного полного
frame scan на каждом lookup. Повреждения на диске проверяются при Open; malicious FS,
device corruption во время открытого owner и power-loss — отдельные scopes.

## WriteBatch и unknown acknowledgement

Batch.Sequence должен равняться current+1 без overflow, ExpectedGeneration — текущему
view. ResultGeneration сохраняется либо возрастает на1 согласно назначению batch.
Codec фиксирует copied canonical input и сохраняет Put(empty)/Delete различие.
Размер, frame/segment/replay и projected index quotas проверяются **до первого write**.

До записи подготавливаются изменения индекса, key arrays, dictionary capacity,
ticket и receipt. После последней проверки token/admission writer устанавливает
offset=ValidLength, записывает целый frame и выполняет Flush(flushToDisk:true).
После успешного flush публикуется весь in-memory index, sequence/generation и receipt.
GetMany не видит промежуточную пачку. Bytes не переписываются на месте; поздние Get и
Lookup перемещают file cursor, но следующий append всегда использует ValidLength.

С первого фактического IO вызова отмена не превращает commit в Aborted. Write/flush
может бросить до/после части или всех bytes. NomKvUnknownOutcomeException сохраняет
точный ticket и исходную cause; owner становится Faulted, продолжать Get/Write/Lookup
нельзя до actual close/reopen. Никакого дописывания rollback поверх полного диска.

Ticket содержит ProjectId/StoreId/Sequence/OperationId/SHA256(entire encoded frame).
Validated reopen возвращает целое old либо new состояние. LookupOutcome сканирует
retained journal в пределах replay quotas и сверяет точный frame/OperationId/digest.
Тот же sequence с другим frame — NotCommitted для прежнего ticket. Один OperationId
может иметь несколько разных sequential commits; generic KV не реализует domain
idempotency. Повтор старого NomJournalBatch.Sequence отклоняется. Stable resource
operation journal/expiry policy адаптируется в NOM2; physical tickets не новый public API.

## Limits и lifetime

Сохраняются [NOM0 ceilings](FORMAT.md#numeric-budgets): key1024/value128KiB,
payload4MiB/8192mutations, segment64MiB/65536batches,2million keys/256MiB logical
index accounting. GetMany≤256keys/1MiB total returned values; budget проверяется до
чтения payload. Потоки ресурсов в этот small-value KV API не помещаются: CAS streaming
остаётся отдельным слоем Nom для NOM2.

NOM1 не выполняет automatic rotation/compaction. При полном active segment/frame
budget WriteBatch отказывает до IO и оставляет старый owner usable. Explicit Compact/root switch реализован в NOM4, automatic scheduling нет. Open существующей root-listed chain≤32segments уже поддержан.
Recovery/index projection не переписывает весь каталог на диск. Initial Open может
временно держать до3 index copies (до3×256MiB logical accounting); steady writer —
один index плюс bounded batch scratch/capacity. Heap/RSS и latency SLA не заявлены;
NOM6 [измерения/limits](PERFORMANCE.md) выполнены; cache key references/RSS учитываются отдельно.

Store caller-owned, factory — fallback owner/capacity registry≤4. Factory Dispose
сначала закрывает admission и RequestStop **всех** owners, затем ждёт/закрывает каждый.
Вошедшая запись завершается до close. File close errors сохраняют registry/root для
explicit retry; закрытый SafeFileHandle повторно не закрывается. Root unlock происходит
после всех data/root handles. Если unlock бросил после actual close, owner уже IsClosed,
registry освобождён, но исходная ошибка возвращается: живых ресурсов не осталось.
Failed creation+cleanup сохраняет обе causes и удерживает capacity при live handles.

Внутренний per-factory NomKvFileOperations даёт6IO seams для deterministic fault tests.
Нет глобальных mutable overrides и fault callbacks в public options. Dispose не
пишет transaction/checkpoint и не запускает recovery; finalizer-driven commit отсутствует.

## Исполняемый proof

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Kv -EvidenceDirectory TestResults/MyFreshNomKv
```

Runner собирает actual Nom/Contracts/Probe и запускает source Debug + framework-dependent
published Release. В каждом profile:6groups,6started=reaped children, из них4actual
kill cutpoints: before-write/partial-write/after-write/after-flush. После reopen проверяются
обе keys, sequence/generation, ticket и целое old/new состояние. After-flush требует new.
Отдельный child доказывает lock contention и успешный Open после actual release.
Ранние/частичные cuts оставляют old, partial17byte tail подтверждённо очищается.

С NOM2 общий App/resource path использует этот KV; LiteDB удалён. Core/probe graph
не имеет external packages, no-friend public App source/published proof выполнен.
Fresh NuGet delivery остаётся NOM7. [Текущая fault acceptance](RECOVERY_ACCEPTANCE.md).
Process kill не является physical power loss; physical disk-full остаётся environment gate.
Финальные unit/build результаты и source freeze фиксируются в [STATUS](STATUS.md) и
[HISTORY](HISTORY.md#nom1-2026-11-07).

## NOM2: bounded pages и отложенный repair

С NOM6 bounded sorted key-reference cache/binary range заменяет repeated full scan; successful WriteBatch/compaction инвалидируют cache. ReadPage(prefix, afterExclusive, maximumItems≤256, maximumBytes≤1MiB) возвращает
detached lexicographic keys/values, Sequence/Generation и HasMore. Budget отказ до
IO сохраняет owner usable; IO failure не выдаёт partial page и требует reopen.
Top-K selection ограничен page count, полный sorted index не создаётся.

NomKvOpenArguments.RepairIncompleteTail по умолчанию true сохраняет NOM1 поведение.
Resource catalog передаёт false: verified prefix доступен для read-only validation,
append заблокирован до CompleteRecovery(). Оно idempotent, truncate+flush после
domain validation; repair failure poisons owner и удерживает actual ownership до close.
## NOM3: reservation capacity

RequireAppendCapacity(NomKvAppendBudget) проверяет aggregate segment/replay/index
и per-frame key/value/payload/mutation headroom до первого resource allocation.
[Численные оценки и инвариант](RECOVERY_ACCEPTANCE.md#исправленные-состояния).
Это internal preflight serialized owner, не новый public KV API или automatic rotation.

## NOM4: checkpoint и explicit maintenance

[Полный контракт](COMPACTION.md#nom4-compaction-2026-11-08): Compact(token) возвращает
NomCompactionReceipt, stream-ит live keys в verified checkpoint, выбирает новый root/segment
и retry-able obsolete cleanup. Physical tickets covered by checkpoint явно Expired.
No-op повтор не изменяет epoch/sequence; generation сохраняется. Public App adapter — CompactAsync.
