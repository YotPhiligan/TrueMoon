# Nom: индексы, snapshots, pins и offline GC

<a id="nom5-retention-2026-11-08"></a>
## NOM5 — контракт 2026-11-08

Реализован следующий срез [единственной очереди Nom](PLAN.md#nom-own-queue).
Канонический KV каталог остаётся authority; вторичного database/provider нет.
Текущие проверки и ограничения — [STATUS](STATUS.md), команды — [HISTORY](HISTORY.md#nom5-2026-11-08).

## Metadata и совместимость

`AssetRecord` добавляет copied `Tags` и `Dependencies`. Исходный конструктор с пятью
аргументами сохранён. Tags: до32 уникальных ordinal строк, каждая1..128strict UTF-8
bytes, без control/blank. Dependencies: до32 уникальных exact revisions; полный
`AssetResourceVersion` включает AssetId, KindId, Revision, Digest и Length. Порядок
входа не влияет на результат: tags сортируются ordinal, dependencies — по Revision.
Возвращаемые массивы detached, в том числе Members snapshot.

Пустые tags/dependencies сохраняют **точные прежние NOMAST01 bytes и revision**.
Иначе magic становится `NOMAST02`, базовый header/locator/opaque metadata прежние,
следом идёт `NOMLNK01`: TagCount u32, DependencyCount u32, затем tag length u32 +
UTF-8 bytes и104-byte exact versions. Exact version: AssetId16, KindId16, Revision32,
Digest32, Length8. GUID RFC/network order, integers little-endian. Общий record≤128KiB;
count/length проверяются до allocation. Дубликаты, неверный порядок, trailing bytes,
empty02 extension, неверные IDs/digests/length и UTF-8 отклоняются без нормализации.

`TrueMoon.Nom/resource/1\0` хеширует canonical metadata вместе с resource digest/length:
изменение links/tags создаёт другую exact revision. Binding domain не меняется.
Old reader не понимает02 и новые retention spaces; обновлённый reader открывает прежний
own-store root без переписывания старых revisions. Это additive alpha API/source proof,
а не объявление готового обновления опубликованного NuGet пакета.

## Индексы и страницы

`QueryAssetsAsync(new AssetQuery(Source, Kind, Tag, IncludeDeleted), cursor, pageSize)`
пересекает exact filters и возвращает Guid order. PageSize1..256, serialized response
≤1MiB; превышение byte ceiling отклоняет весь ответ. `ReadAsync` сохраняет прежний
лимит256/1MiB, скрывая tombstones. Cursor непрозрачен и привязан к StoreId, полному
KV Sequence и query. Даже control/retention commit инвалидирует старый cursor:
смешивать страницы разных состояний нельзя, обход начинается заново.

Source/kind/tag, forward/reverse dependencies — восстанавливаемые in-memory indexes.
Они строятся из verified canonical records при Open до tail repair. При изменении
asset/revision/tombstone новый bounded index готовится на transaction overlay **до**
durable batch, затем заменяется после success. Control-only commits его сохраняют.
Ресурсные payload не загружаются. Сейчас это полный bounded metadata pass на content
commit; incremental preparation и измерение его стоимости относятся к NOM6.

Пределы:100k logical identities,200k revisions,400k exact edges; с NOM6 default64MiB/explicit opt-in≤256MiB logical index
accounting. Учёт:256/asset +Σ(96+2×tagUTF8Bytes),384/revision +384/edge. Общий byte
ceiling может сработать раньше count ceilings. Это logical admission budget;
готовящийся и текущий indexes могут существовать одновременно, RSS им не измеряется.
Общие [KV/checkpoint budgets](FORMAT.md) также действуют.

`ReadDependenciesAsync(exact, reverse, maximum)` возвращает direct exact versions,
в том числе относящиеся к прежним heads; maximum1..4096. Reverse count проверяется
перед allocation ответа. `GetDependencyClosureAsync(roots, maximumMembers)` принимает
1..256 roots и возвращает полное deduplicated множество, sorted by revision,
≤4096members/32768 traversed edges. Missing/mismatched version и превышение бюджета
отклоняют весь запрос. Точная dependency должна уже существовать либо публиковаться
в той же atomic batch; проверить можно только полный transaction overlay.

## Stable identity и tombstones

`DeleteResourcesAsync(expectedGeneration, ids)` атомарно снимает heads для1..256IDs,
сохраняя Asset и Binding. Source/Kind существующего ID не переназначаются. Новому ID
нельзя занять binding удалённого asset. Нормальные query/read скрывают tombstones;
`IncludeDeleted` возвращает последний descriptor, чьи links не являются GC roots.
Они могут указывать на уже pruned revisions. Tombstones занимают identity/index budget
и не удаляются GC.

Actual delete повышает ContentGeneration один раз; повтор уже удалённого набора —
no-op. Republish того же ID/source/kind снимает tombstone; обычные правила смены
locator/binding сохраняются. Старые exact versions доступны до их явного GC.
CAS bytes и revision records не удаляются самим DeleteResourcesAsync.

## Frozen manifests, durable pins и runtime leases

`CreateSnapshotAsync(roots, maximumMembers)` сохраняет **полную exact closure**,
generation и digest manifest. ID — length-prefixed SHA256 domain
`TrueMoon.Nom/manifest/1`, ProjectId, StoreId, Generation и sorted exact members.
Retained manifests≤256, members≤4096 each. Manifest/member records публикуются одним
KV batch; повтор того же frozen state — no-op. ContentGeneration не меняется.

`PinSnapshotAsync(snapshotId, pinId)` durably привязывает nonempty caller-owned Guid
к одному manifest. Повтор пары idempotent, retarget запрещён; pins≤1024. Только
`ReleaseSnapshotPinAsync(pinId)` снимает pin. PID, возраст или завершение App этого
не делают. Caller использует новый Guid для каждого нового retention intent, не
переиспользуя released ID для другого намерения. Все retained manifests являются GC roots; pin дополнительно запрещает
их удаление через `RemoveSnapshotAsync`.

`OpenSnapshotAsync(id)` возвращает caller-owned `IAssetSnapshot`; отсутствующий ID
даёт KeyNotFoundException. Одновременно≤64handles. Handle разрешает opens только
exact Members и удерживает store/root lifetime. Dispose закрывает admission новых
opens и снимает lease handle; уже зарегистрированные streams остаются самостоятельными
owned leases. Manifest нельзя удалить до **actual close** последнего такого stream.
In-flight read и его Dispose/Stop проверены отдельным gated test. Snapshot не закрывает
выданные streams. Close streams и snapshot handles до Dispose store/App; timeout
сохраняет владельца, а не отменяет продолжающуюся IO.

Explicit `RemoveSnapshotAsync` требует отсутствия durable pins/handles/streams.
Unpin сам по себе не удаляет manifest. Durable pins не держат процессный owner живым
и переживают close/reopen; runtime handles не сериализуются. Нет automatic expiration.

## GC: mark, dry-run, bounded prune и resumable delete

GC explicit и offline относительно write reservations/read streams. Snapshot handles
без streams допустимы: их retained manifests остаются roots. Вызов не запускает
automatic compaction и не меняет ContentGeneration.

1. `PlanGarbageCollectionAsync` без записи marks heads, все manifests и committed
   retained operation outputs, затем exact dependencies. Pending reservation блокирует
   GC; после crash прежняя recovery abort-ит pending operation до Ready. Work ограничен
   index budgets,1.3m root/edge visits и400k edges.
2. Инвентаризация `objects/sha256/{lowerhex2}/{digest64}.blob` ограничена100256 direct
   entries (включая bucket/unknown names). Reparse entries отклоняются до mutation.
   Неподходящие имена/вложенные каталоги сохраняются. Live filenames/length обязаны
   присутствовать; полный повторный hash всех CAS при dry-run не выполняется:
   bytes immutable, отдельный OpenResource проверяет same-handle hash.
3. Plan выбирает≤2048 dead revisions **parents first**, не оставляя retained revision
   с missing dependency. CAS выбирается только при отсутствии ссылок от **всех**
   оставшихся revision records. В одном slice≤2048objects. Shared digest удаляется
   после последней retained revision. `HasMore` требует нового plan после завершения
   текущего slice. Для unreachable cycle без допустимого bounded prune — явный отказ.
4. `CollectGarbageAsync(plan)` проверяет StoreId, точный KV Sequence и свежий inventory
   fingerprint. Stale/foreign/changed plan не пишет. Одна atomic batch удаляет selected
   revisions и создаёт durable CAS deletion intents. До неё резервируются prune+ack
   frames: payload52r+152o, max batch52r+100o, mutations r+o, positive index delta
   max(0,o-r)×136. При o=0 достаточно одной prune frame. К общему payload добавляется
  144bytes framing/frame. Ошибка/unknown commit требует actual close/reopen; delete
   до success metadata commit не выполняется.
5. Только после prune удаляются CAS files. IOException/permission failure сохраняет
   intent и возвращает CleanupPending. Успешные intents снимаются одним durable ack
   batch. Cancel после prune возвращает acknowledged receipt с pending state.
   При unknown ack owner poisoned; reopen выбирает whole old/new metadata state.
6. `CollectGarbageAsync()` без plan **только** продолжает сохранённую очередь. Перед
   первым delete проверяется место под ack. Если журнал заполнен, explicit CompactAsync
   освобождает append budget, затем resume повторяется. Пока очередь не пуста,
   resource publication/CreateSnapshot/DeleteResources запрещены. Open валидирует
   intent↔key/length и отсутствие ссылок от любых retained revisions **до** tail repair;
   автоматически CAS не удаляет. Caller решает, когда продолжать maintenance.

Counts в receipt относятся к acknowledged intents и их declared bytes; lost delete
ack может означать, что file уже отсутствует. Повтор безопасен и не читает inputs.
`CollectGarbageAsync(plan)` не является бесконечным циклом до пустого root:

```csharp
await store.CollectGarbageAsync(); // Finish any durable pending cleanup first.
while (true)
{
    var plan = await store.PlanGarbageCollectionAsync();
    var result = await store.CollectGarbageAsync(plan);
    if (result.CleanupPending) break; // Explicit retry or close/reopen, retaining the intents.
    if (!plan.HasMore) break;
}
```

## Дополнительные key spaces и values

Все keys используют [NomKeyCodec v1 framing](FORMAT.md), digest raw32, Guid RFC16.

| Space / fields | Value |
| --- | --- |
| Tombstone20 / AssetId16 | `NOMBND01` + AssetId16 (24bytes) |
| Manifest48 / ManifestDigest32 | `NOMMAN01` + Generation8 + MemberCount4 (20bytes) |
| Member49 / ManifestDigest32, Revision32 | `NOMMEM01` + exact version104 (112bytes) |
| Pin50 / PinGuid16 | `NOMPIN01` + ManifestDigest32 (40bytes) |
| Garbage51 / ObjectDigest32 | `NOMGCQ01` + ObjectDigest32 + Length8 (48bytes) |

Manifest digest/count/member keys, exact closure, pins и GC non-reference constraints
проверяются при Open до любого repair/staging cleanup. Dependency64/Reverse65 остаются
reserved, durable records в этих spaces не создаются: indexes восстанавливаемые.
Metadata checkpoint/compaction сохраняют все новые records обычным KV путём.

NOM5 не добавляет background GC, clock/PID expiry, remote FS или native code.
Process kill, injected IO и physical disk-full/power loss — разные proofs. Открытый
Windows authorization gate NOM3/4 сохраняется, не закрывается этими проверками.
NOM6 [backup/format protocol](BACKUP.md) и [workload/limits](PERFORMANCE.md) проверены; fresh package/RID — NOM7.
