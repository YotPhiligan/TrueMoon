# TrueMoon.Nom: собственное хранилище ресурсов

> NOM0 [format v1](FORMAT.md#nom0-format-2026-11-07) и NOM1 [рабочий KV writer/root Open](KV_CORE.md#nom1-kv-2026-11-07)
> реализованы; NOM2 [App/resource cutover](CATALOG.md) завершён2026-11-08;
> фактические проверки и ограничения — [STATUS](STATUS.md).

<a id="nom-own-store-2026-11-07"></a>
## Решение — 2026-11-07

Пользователь утвердил **полную замену LiteDB собственной реализацией Nom**.
Целевая архитектура: небольшой transactional Key-Value каталог, дописываемый журнал
и отдельные immutable content-addressed ресурсы с потоковым доступом. Nom остаётся
самостоятельным модулем вне Aether. Детальные этапы ведутся в [PLAN](PLAN.md#nom-own-queue).

NOM2 переключил исходники, tests и probes на собственный backend, удалил vendor
implementation/package/download paths. Старые результаты сохраняются в
[истории](HISTORY.md#nom-before-own-store-2026-11-07); новые proofs — [STATUS](STATUS.md).
NOM3 IO/process faults и [NOM4 explicit maintenance](COMPACTION.md) проверены. NOM5 [indexes/snapshots/pins/offline GC](RETENTION.md) реализованы и проверены в IO/process scope. NOM6 [backup/format protocol](BACKUP.md) и [performance/limits](PERFORMANCE.md) реализованы и проверены в указанном scope. Physical gates и delivery ещё нужны.

## Область первой версии

- Один локальный root, один процесс-владелец и один сериализованный writer.
- Key-Value каталог хранит небольшие versioned metadata values; большие ресурсы
  читаются и записываются через Stream, без загрузки целого payload в память.
- Операции ядра: exact-key lookup и атомарная ограниченная пачка Put/Delete с
  expected generation. Видимые изменения пачки публикуются целиком.
- ID/source/kind/tag/reverse-dependency indexes принадлежат предметному слою Nom.
  Универсальный query language и произвольные пользовательские indexes не нужны.
- Первый индекс ключей находится в памяти и ссылается на данные на диске. Его
  memory/admission limits обязательны; превышение бюджета даёт явный отказ.
- Checkpoints и offline compaction ограничивают replay и размер журнала.
  NOM5 snapshots/pins/GC реализованы отдельным этапом; production consumer delivery — NOM7.

В первую версию не входят multi-process writers, remote/network filesystem,
distributed replication, online compaction, encryption/compression, единый packfile
крупных ресурсов и progressive/chunk integrity. Эти возможности не подразумеваются
словом Key-Value. Runtime bundle format также остаётся отдельным контрактом.

## Слои и authority

| Часть | Ответственность |
| --- | --- |
| App/host/factory | UseNom, validated copied options, DI, registry, admission и stop/drain/dispose |
| Resource catalog | Logical IDs, versions/heads, identity bindings, operation outcomes и content generation |
| KV core | Binary key/value framing, atomic WriteBatch, replay, bounded key index, checkpoint/compaction |
| Object store | Staging, streaming SHA-256/length checks, immutable CAS files и owned read handles |
| Snapshot/retention | Exact manifests, durable pins, dependency closure и offline GC |

KV journal/checkpoint — authority метаданных. Индекс ключей восстанавливаемый;
он не является вторым источником identity. Object filenames обозначают exact bytes,
а не AssetId. Одинаковые bytes могут принадлежать разным logical assets.

Логические key spaces: store control, asset, revision, identity binding, operation,
manifest/member/pin и dependency edges. Это категории записей, не объявление новых
public namespaces или финального wire encoding. Canonical framing/key spaces/metadata закреплены в NOM0; domain-specific values/indexes — NOM2/5.
Проект не переписывается целиком на каждом import: обычный commit содержит изменения.

## Формат и recovery contract

[FORMAT](FORMAT.md#nom0-format-2026-11-07) фиксирует magic/version, StoreId/ProjectId, endian/length encoding,
canonical keys, value schema versions, batch/generation/operation sequence, checksum
и commit framing, segment rotation и authoritative checkpoint selection. Decoder
проверяет размеры/overflow до allocation; неизвестный format отклоняется до записи.
Формат не сериализует runtime type names и не зависит от culture или порядка Dictionary.

Каждая пачка имеет проверяемые границы, длины и checksum, охватывающую identity,
порядок и содержимое пачки. Memory index меняется только после подтверждения commit;
его обновление подготавливается до durable записи. Reader получает согласованный
view/generation, а не смесь independently прочитанных старых и новых keys.

Recovery начинает с проверенного checkpoint/root и применяет только целые committed
batches в правильном порядке. Обрыв активного хвоста допускает восстановление лишь
по явно описанным правилам границы последнего подтверждённого состояния. Повреждение
закрытого сегмента/подтверждённой области, sequence gap или конфликтующий checksum
не маскируются как обычный конец файла и не ведут к silent reset.

Ошибка записи или flush останавливает дальнейший append до close/recovery.
Если bytes могли полностью попасть на диск, outcome остаётся Unknown до проверки
journal по operation ID. Нельзя без проверки сообщать Aborted, выполнять ticket заново
или пытаться записать обязательный rollback поверх заполненного диска. Повторный Open
должен разрешать Unknown однозначно и сохранять monotonic sequence/retention floor.

Процессный crash и physical power loss — отдельные claims. Flush/rename сами по себе
не означают доказанную power-loss durability. NOM0 фиксирует OS/filesystem assumptions
и порядок синхронизации файлов/namespace; NOM3 проверяет заявленный scope.

## Потоковая публикация и lifetime

1. Проверить expected generation, limits и descriptors; выдать durable operation ID.
2. Потоково записать staging с ограниченным buffer, length/hash и cancellation checks.
3. Проверить/flush/закрыть bytes и завершить no-overwrite CAS publication по выбранному
   OS/filesystem протоколу. Сбой может оставить orphan, но не live reference на missing bytes.
4. Одной KV пачкой опубликовать metadata/heads/identity changes, outcome и generation.
5. Вернуть committed receipt; failed staging cleanup сохраняет CleanupPending/ownership
   и повторяется без чтения replacement inputs.

До commit cancellation может завершить операцию Aborted. После подтверждённого commit
возвращается committed outcome. Inputs borrowed, non-seekable/short reads допустимы,
они не закрываются хранилищем. Returned read streams owned и удерживают root/pin до
фактического close. Первая реализация сохраняет full same-handle hash verification
до выдачи seekable stream; изменение first-byte behavior требует отдельной задачи.

UseNom и IFactory<IProjectAssetStore, OpenAssetStoreArguments> сохраняются как штатный
путь. Сервисы создаются через DI/фабрики; никаких новых public generic KV API либо
provider selector для замены не требуется. KV core остаётся внутренней реализацией.
Concrete implementations не зависят от Aether/Cobalt bootstrap; общие App contracts
берутся из TrueMoon.Contracts. Сервисы располагаются по правилам namespace/структуры.

Stop закрывает admission всех owners до await, затем дожидается реальных writers/readers,
закрывает handles и освобождает root. Cancellation ожидания/timeout не прекращает владение.
Failed startup, IO и cleanup сохраняют все причины; retry завершает оставшееся cleanup.
Нет finalizer-driven catalog commit/rollback и освобождения live ресурсов после timeout.

## Checkpoint, compaction, snapshots и GC

Checkpoint фиксирует generation/sequence, key state и точный набор покрытых segments.
При его потере восстанавливается только документированный корректный путь; потеря
authority не превращается в новый пустой store. Offline compaction создаёт и проверяет
новый набор файлов, затем публикует authority switch по crash-safe протоколу.
Старый набор сохраняется до подтверждения switch и освобождения всех читателей.
Interrupted compaction и disk-full сохраняют recoverable старое либо новое состояние.

Snapshot — immutable manifest exact revisions и explicit pin. Закрытие snapshot
запрещает новые opens, существующие streams удерживают pin. PID/возраст pin сами по
себе не дают права удаления. Offline GC сначала выполняет bounded mark/dry-run от
heads/retained manifests/pins/active operations, затем resumable prune/delete.
Metadata compaction и GC ресурсных bytes имеют разные критерии живости и budgets.

## Совместимость и полное удаление LiteDB

- Имя TrueMoon.Nom, назначение App API, logical IDs и ownership сохраняются. Public
  контракт изменяется только при явной необходимости и с compatibility проверкой.
- Новый binary catalog имеет собственную версию. **Старый LiteDB root отклоняется
  без изменения файлов**. Create не перезаписывает existing root; auto-reset и implicit
  migration отсутствуют. Legacy LiteDB reader/provider в поставку не включается.
- Resource revision сейчас включает hash канонических metadata bytes, закодированных
  собственным canonical metadata codec. NOM0 задал новый codec/hash domain и golden vectors; существующие
  revisions нельзя незаметно пересчитать, выдав за совместимый format. Автоматическая
  миграция legacy data не входит в первый срез; отдельный запрос потребует отдельного
  offline transport/identity compatibility решения до обращения к пользовательским данным.
- NOM2 удалил LiteDB PackageReference/central version, using/types, BSON codec,
  native-engine-specific wrappers и исполняемые vendor probes/download/SHA checks.
  Проверяются все consumers: build, test, publish, fresh-package graph и output.
- LiteDB не остаётся опциональным provider, fallback, временной runtime dependency,
  vendor fork или обязательным external worker. Название TrueMoon.Nom.LiteDb из прежнего
  плана поставки отменено. Исторические документы и evidence сохраняют прежнее имя.
- Семантические тесты atomicity/streaming/identity/ownership переносятся на собственный
  backend; vendor-specific тесты заменяются проверками собственного journal/format.
  Прежние passing counts не переносятся автоматически.

## Budgets и критерии до кода

Текущие ceilings — исходная точка: 64KiB stream buffer, 512MiB object, 1GiB batch bytes,
256affected assets/4MiB metadata batch, 32queued commands/16MiB payload, 16read streams,
4owners, 4096retained operations. Их сохранение или изменение фиксируется в NOM0.
Отдельно нужны точные key/value/frame/segment/checkpoint limits, индекс памяти,
replay work/time, temporary disk headroom и compaction workspace. Нельзя выдавать
payload accounting за bounded heap/RSS либо admission quota за физический disk reserve.

Workload сохраняется: 1kassets/4krevisions/16kedges, затем 50k/100k/300k; small metadata
и16/128MiB blobs, плюс отдельная проверка заявленных максимальных scale limits.
Одинаковый durability profile обязателен для cold/warm/reopen, p50/p95, allocations,
process memory и disk amplification. NOM6 workload/allocations/RSS/disk amplification измерены в [указанном scope](PERFORMANCE.md); сравнение с LiteDB и performance advantage над ним не заявлены.

Готовность требует собственных source Debug/published Release/fresh-package proofs,
independent golden/replay model, bounded child kill/reap, IO fault matrix и фактического
отсутствия LiteDB в dependency/output graph. Physical disk-full и power-loss получают
собственные результаты или явно открытые environment gates. [Текущий статус](STATUS.md).
