# ARCH1g.1c — постоянные subasset ID и atomic ledger

Windows x64, общий net11/SDK профиль. [Source capture](MODEL_SOURCE_CAPTURE.md) →
[изолированный importer](MODEL_IMPORT_WORKER.md) → complete CPU candidate →
semantic reconciliation → atomic sidecar. Это завершает ограниченный CPU-профиль
g.1; Nom/build/runtime assets и визуальный consumer остаются g.2.

## API и владельцы

`UseAetherModelImport()` теперь дополнительно регистрирует
`IFactory<ModelImportLedgerSession, ModelImportLedgerArguments>` и default
`IModelImportLedgerStore` (`FileModelImportLedgerStore`). Предварительно заданный
store сохраняется. Настройки источника, worker, sidecar и identity edits — явные
runtime arguments, не независимые JSON/env configuration providers.

```csharp
// Composition root: obtain the typed factory through DI.
await using var ledger = ledgerFactory.Create(new(sourceId, absoluteSidecarPath));
var previous = await ledger.OpenAsync(token); // read-only; absent => null
var published = await ledger.PublishAsync(candidate, previous?.Generation ?? 0,
    edits: explicitIdentityDecisions, cancellationToken: token);
```

Session и immutable snapshots принадлежат caller. Session не мигрирует и не создаёт
файлы при factory creation/Open. Один продукт допускает последовательные операции;
параллельный вызов отклоняется. App-owned ModelImportHost имеет общий admission
**1active/0queued** для native import, Open и публикации. Stop закрывает admission,
отменяет и дожидается активной операции. Idle products и published snapshots App
не удерживает и не освобождает за caller. Failed publication не заменяет Current.
Повторный Dispose безопасен, failed disposal можно повторить.

SourceId задаёт caller один раз, хранит в project/source record; он также записан
в sidecar и проверяется при каждом чтении/публикации. Rename/move переносит этот
record вместе с источником. Копия получает новый SourceId и новый sidecar; старый
record с иным SourceId отвергается. Пути и content hashes не создают persistent ID.

## Семантические ключи

Ledger schema1: `(ModelSubassetKind, ordinal locator) → random Guid + Active`.
Категории: Mesh, Material, Slot, BindingSet, Instance. IDs уникальны в ledger,
не пусты и не равны SourceId. Reconcile создаёт новый immutable ledger; перестановка
discovery и изменение CPU content сохраняют существующие IDs.

`ModelImportSemantics` содержит nullable Mesh/Material/Instance locators в локальном
порядке candidate. Null/duplicate допустимы для CPU-only import, но не для publication.
Пример автоматических ключей:

| Элемент | Locator |
|---|---|
| Однопримитивный mesh с авторским именем Hull | mesh/Hull |
| Primitive с extras.truemoon.subasset=front в mesh Hull | mesh/Hull/primitive/front |
| Material Paint | material/Paint |
| Встроенный glTF default material | builtin/material/default |
| Единственный slot / binding set mesh | slot/mesh/Hull; binding/mesh/Hull |
| Node Root/Child и primitive front | instance/node/Root/Child/primitive/front |

Label берётся из `extras.truemoon.subasset`, затем из авторского name. Primitive
не имеет стандартного name: несколько primitives требуют явных subasset labels
либо caller labels. Один primitive без label использует имя mesh. При нескольких
scenes instance namespace включает уникальное авторское имя scene; без него нужна
явная разметка. Материалы/meshes проверяются на неоднозначность **во всём исходном
namespace**, включая неиспользуемые объекты. Коллизия node path делает неоднозначными
и пути его потомков. Assimp может не возвращать неиспользуемые meshes/materials;
исчезнувшие из проекции ресурсы становятся tombstones.

Авторские segments требуют NFC, без control chars, ≤64 UTF8 bytes, экранируются
через Uri.EscapeDataString. Автоматический locator ≤240 UTF8 bytes; слишком длинные/
отсутствующие/неоднозначные hints требуют явной разметки. Явный locator — непрозрачная
NFC строка ≤256 UTF8 bytes, ordinal case-sensitive; он никогда не превращается в path.

`ModelImportIdentityEdits.Labels` задаёт `(kind, current candidate index) → locator`.
Индекс здесь только **явный selector оператора для текущего candidate**. Он не
является persistent key и не используется для автоматического matching. Для slot/
binding можно задать независимые labels; по умолчанию они выведены из effective mesh
locator. Уникальные авторские names/labels должны сохраняться exporter/автором;
движок не пытается угадать идентичность по похожей геометрии или имени нового файла.

## Rename, delete, restore

`ModelImportIdentityEdits.Renames` — bounded список `ModelSubassetRename(From, To)`:
только same-kind, существующий source key, один source/target на mapping, target
должен быть discovered. Target не может занимать другой существующий key, а старый
source при rename не должен одновременно присутствовать в discovery. Chains/swaps
через занятые keys отвергаются. Invalid решения не меняют ledger/file/snapshot.

Rename переносит прежний Guid на новый locator. При rename mesh обычно нужны три
mapping — Mesh, Slot, BindingSet; Instance зависит от node/primitive labels и может
сохраниться без изменения. Без mapping новое имя получает новый ID, старое остаётся
tombstone. Удаление сохраняет inactive entry/ID. Возвращение того же tombstone key
требует явного `From == To`; восстановление под новым именем — явный same-kind mapping.
Движок не оживляет и не перенаправляет старые ссылки молча.

## Provenance через native worker

Recipe/candidate protocol теперь **v2**; input capture packet и control handshake
остаются v1. Требуется worker из согласованной поставки. v1 worker отклоняется явно;
старые research/results manifests не переписаны.

В child из захваченного original JSON извлекаются авторские locators. Только name
полей meshes/materials/nodes заменяются временными ASCII transport tags, затем JSON/
GLB заново собирается с прежними BIN bytes. Tags проверяются на native meshes,
материалах, родительских node links и node→mesh→material references. Индексы внутри
tags адресуют source objects только на время вызова: ни tag, ни индекс не становится
persistent locator/Guid. Исходный InputRevision относится к **оригинальным captured
bytes**, recipe фиксирует metadata-only преобразование; disk fallback отсутствует.

Это учитывает lazy material/mesh tables и primitive-name suffix pinned Assimp6.0.5.
Основание: [source importer](https://github.com/assimp/assimp/blob/v6.0.5/code/AssetLib/glTF2/glTF2Importer.cpp).
Native версии/SHA/precision, Job/callback quotas, finite deadline/kill/reap остаются
из g.1b. Native input после tagging тоже ограничен16MiB/file/32MiB closure.

## Один commit для candidate и ledger

Sidecar — **один** bounded binary record, envelope TML1/schema1 + length + SHA256:
generation, SourceId/input revision, canonical v2 candidate, ledger/tombstones и exact
kind/index→key/ID associations. Decode проверяет checksum, counts, references, unique
IDs/keys, отсутствие лишних active entries и canonical re-encoding. Published meshes
содержат permanent SlotId; material/binding/instance references согласованы с ledger.
Asset revisions по-прежнему вычисляются из canonical CPU definitions f.1; candidate
InputRevision/Revision не подменяет RenderAssetKey.Revision. Runtime manifest здесь нет.

Publish берёт эксклюзивный OS FileStream `.write-lock`, читает целый current head,
проверяет SourceId и **expectedGeneration**, строит/валидирует полный next record,
пишет уникальный same-directory `.pending`, FlushAsync + Flush(true), проверяет
cancellation и выполняет File.Replace (existing) либо File.Move(overwrite:false)
(first generation). Это единственная точка публикации; все данные меняются вместе.
Generation начинается с1 и увеличивается при каждом commit, даже identical reimport.

До commit exception/cancel/stale сохраняет точные прежние head bytes и Current.
После commit token не проверяется и публикация не откатывается. `.write-lock`
остаётся пустым файлом: владение задаётся OS handle и снимается при exit/crash;
удалять файл lock между операциями нельзя. Contention writer отклоняется без очереди.
Readers допускают replacement и до20 попыток открытия при Windows sharing/lock
violations (5ms между ними); corrupt data/access denial не ретраятся. File content
никогда не принимается частично.

После process crash `.pending` может остаться; Open его игнорирует и не восстанавливает
неопубликованные данные. Старые orphan files можно удалить отдельно при отсутствии
writer; модуль не удаляет неизвестные файлы по маске. Обычная ошибка до rename очищает
свой staging file; cleanup failure сообщается вместе с исходной ошибкой. Все handles
при этом освобождаются, orphan не является head. Scope — cooperating writers на local
Windows filesystem, process-crash consistency после content flush; не ACL/hostile-FS
sandbox и не гарантии power-loss/сетевых volumes. Open не мигрирует неизвестные schemas.

Budgets:1024active/4096total ledger entries, включая slots/bindings/instances;
1024labels/renames, locator256UTF8bytes; candidate8MiB, sidecar12MiB. Existing CPU mesh
и native budgets сохраняются. Publication с превышением active budget отвергается
даже если отдельный CPU-only candidate укладывается в native geometry limits.

## Проверки и следующий этап

[Native/store/DI source-published-fresh runner](../../ManualTests/ModelLedgerConsumer/README.md),
[фактические результаты и команды](HISTORY.md#arch1g1c-2026-11-17),
[immutable manifest/SHA](MODEL_SUBASSET_LEDGER_RESULTS.json).
Статический CPU importer g.1 завершён в описанном профиле; следующий g.2 —
Nom/build/runtime artifacts, captured delivery и activation. GPU/Editor asset preview,
другие OS/AOT и release readiness этой проверкой не устанавливаются.
