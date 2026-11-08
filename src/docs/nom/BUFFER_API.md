# Nom: byte buffer API и unmanaged raw helpers

<a id="nom8-buffer-api"></a>
## NOM8a/b — buffer API, 2026-11-08

По запросу пользователя реализованы byte Memory/Span и ReadOnly варианты в
`AssetStoreBufferExtensions`; NOM8b добавляет explicit unmanaged raw helpers ниже.
Публичный IProjectAssetStore/codec/format/dependencies не менялись; новый public
API добавлен через extension methods, существующие implementers совместимы.

Уже сейчас возвращаемый OpenResourceAsync stream реализует Read(Span<byte>) и
ReadAsync(Memory<byte>). Byte helpers реализованы поверх существующего
IProjectAssetStore/Stream; новый generic KV API, service/lifetime или UseX не нужны.

## Разделение операций

| Данные | Назначение |
| --- | --- |
| ReadOnlyMemory<byte> | Канонический borrowed async input, без полной копии входа |
| Memory<byte> | Async destination; как source приводится к read-only view |
| ReadOnlySpan<byte> | Короткоживущий source; для async publication синхронно копируется в явно ограниченный snapshot |
| Span<byte> | Sync destination уже открытого verified stream; как source — тот же explicit-copy путь |
| ReadOnlyMemory<T>/Memory<T>, T:unmanaged | NOM8b: borrowed raw input и writable typed destination |
| ReadOnlySpan<T>/Span<T>, T:unmanaged | NOM8b: sync byte views либо explicit-copy publication |

Readonly destinations не принимаются: заполнение требует writable Memory/Span.
Для чтения read-only result используется detached LoadResourceAsync и его .Span view.
Writable→readonly conversions не копируют содержимое. Четыре byte варианта проверяются
компиляцией и выполнением; generic forwarding/inference также проверены в NOM8b.
Batch dictionaries invariant: dictionary
Memory<T> не считается автоматически dictionary ReadOnlyMemory<T>.

## Реализованные byte signatures

Публичный класс TrueMoon.Nom.Resources.AssetStoreBufferExtensions:

```csharp
using TrueMoon.Nom;
using TrueMoon.Nom.Resources;

Task<AssetWriteReceipt> PublishResourceAsync(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    ReadOnlyMemory<byte> source, CancellationToken token = default);

Task<AssetWriteReceipt> PublishResourceCopyAsync(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    ReadOnlySpan<byte> source, int maximumCopyBytes,
    CancellationToken token = default);

Task<AssetWriteReceipt> PublishResourcesAsync(
    this IProjectAssetStore store, AssetWriteTicket ticket,
    IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> sources,
    CancellationToken token = default);

Task<int> ReadResourceAsync(
    this IProjectAssetStore store, AssetResourceVersion exact,
    Memory<byte> destination, CancellationToken token = default);

Task<ReadOnlyMemory<byte>> LoadResourceAsync(
    this IProjectAssetStore store, AssetResourceVersion exact,
    int maximumBytes, CancellationToken token = default);
```

Single-resource publication требует ticket ровно с одним descriptor и matching assetId;
он не публикует часть multi-resource ticket. Batch вариант принимает отдельные memory
sources для всех IDs и сохраняет существующую atomic publication/expected generation.
Batch dictionary synchronously копируется как набор IDs/views до первого await;
дальнейшее изменение dictionary не меняет submitted set. Содержимое memory остаётся
borrowed. Count1..256, nonempty unique IDs,≤512MiB/view и≤1GiB/batch проверяются до
доступа к bytes; admission failure не передаёт работу publisher и не abort-ит ticket.
Неверный pending набор относительно opaque ticket, переданный publisher, следует его
abort policy. Single helper отклоняет также mismatching committed receipt; часть
multi-ticket не публикуется. Overlapping immutable memory views допустимы.

ReadResourceAsync копирует **весь exact resource**, требует destination.Length≥exact.Length,
проверяет длину до чтения/изменения destination и возвращает число bytes. Хвост большего
destination не меняется. Cancellation/IO failure после начала copy могут оставить
частично заполненный destination; это не successful result. Частичные/offset reads
остаются у verified stream с явными Position/Read/ReadAsync.
Read helper использует rented scratch≤64KiB: async IO заполняет owned array, затем
данные синхронно копируются в destination.Span. Поэтому non-array/non-pinnable
MemoryManager поддерживается и для input, и для destination. Stream закрывается
до terminal Task; при empty length verified Open/dispose всё равно выполняется.

LoadResourceAsync — явная полная bounded allocation в отдельный managed array, наружу
отдаётся read-only view. Результат detached: после окончания task не удерживает stream,
root или store, остаётся действителен после их Dispose. Это не zero-copy/mmap/pool lease.
Readonly view не является security guarantee неизменяемости underlying array.
maximumBytes/maximumCopyBytes обязательны, положительны,≤существующего512MiB/object
ceiling; checked lengths и Array.MaxLength проверяются до allocation. Empty resource
допустим без дополнительной allocation, сохраняя положительный admission ceiling.

## Span и асинхронное время жизни

Borrowed Memory source нельзя менять, возвращать в pool или освобождать его owner до
**фактического terminal state возвращённого Task**, включая error/cancel. Запрос
cancellation сам по себе не завершает lease. Nom не Dispose-ит caller MemoryManager/
IMemoryOwner. Destination Memory эксклюзивно предоставляется на тот же срок.

CopyAsync entry point с ReadOnlySpan — обычный non-async метод: synchronously validates
budgets/token и копирует span в owned snapshot до возврата Task. С этого момента caller
может переиспользовать исходный stack/array. Snapshot живёт до actual завершения IO и
освобождается на success/failure/cancel. Его полная копия отражена в имени Copy и лимите,
а не скрыта в якобы zero-copy overload. Блокирующего GetResult поверх async IO нет.
Синхронные span reads работают через уже открытый verified Stream.

**Уточнение draft при реализации:** CopyAsync копирует span при каждом вызове,
включая committed replay. Асинхронно узнать outcome раньше синхронной snapshot copy
и одновременно разрешить caller переиспользовать span до Task completion нельзя.
Publisher не читает owned snapshot на replay; memory helper вообще не обращается
к backing Span committed input. Длина/budget/token admission проверяются в обоих
случаях. Dispose adapter сбрасывает memory view, caller owner никогда не освобождается.

Span/ref-like state не переносится через await; допустимое использование ref-struct
locals в современных C# не отменяет эту границу. Правила borrowed memory:
[Microsoft memory guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines),
[C# ref structs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct).

<a id="nom8b-raw-api"></a>
## NOM8b — реализованный explicit raw contract

`TrueMoon.Nom.Resources.AssetStoreRawBufferExtensions` предоставляет методы с `where T : unmanaged`:

```csharp
using TrueMoon.Nom;
using TrueMoon.Nom.Resources;

Task<AssetWriteReceipt> PublishRawResourceAsync<T>(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    ReadOnlyMemory<T> source, CancellationToken token = default) where T : unmanaged;

Task<AssetWriteReceipt> PublishRawResourceAsync<T>(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    Memory<T> source, CancellationToken token = default) where T : unmanaged;

Task<AssetWriteReceipt> PublishRawResourceCopyAsync<T>(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    ReadOnlySpan<T> source, int maximumCopyBytes,
    CancellationToken token = default) where T : unmanaged;

Task<AssetWriteReceipt> PublishRawResourceCopyAsync<T>(
    this IProjectAssetStore store, AssetWriteTicket ticket, Guid assetId,
    Span<T> source, int maximumCopyBytes,
    CancellationToken token = default) where T : unmanaged;

Task<int> ReadRawResourceAsync<T>(
    this IProjectAssetStore store, AssetResourceVersion exact,
    Memory<T> destination, CancellationToken token = default) where T : unmanaged;
```

Writable Memory/Span forwarding сохраняет generic inference; все четыре варианты
собираются и выполняются без неоднозначности. Для массивов использовать AsMemory/
AsSpan или явный generic T. Single-ticket/ID и replay/abort policies те же, что у
byte helpers. Typed batch/load не добавлены: mixed-type batches остаются в byte/Stream
API, detached readonly result — LoadResourceAsync. Для readonly span чтения caller
использует read-only view уже полученной memory; для writable span — raw byte view
и ReadExactly на открытом stream. Нет await, удерживающего этот span.

Количество elements×sizeof(T) считается checked long **до** создания byte view/allocation.
Используется runtime Unsafe.SizeOf<T>(), а не interop Marshal.SizeOf:bool/char имеют
именно managed runtime representation. Source≤512MiB; maximumCopyBytes — positive
**byte** ceiling≤512MiB, не element count. Copy всегда snapshot-ит raw bytes до Task
return, включая committed replay; borrowed memory replay не запрашивает backing Span.
Read exact resource требует кратности byte length размеру T и достаточной destination;
возвращаемое значение typed read — число elements, byte read — число bytes.
Negative/oversized/nondivisible length и short destination отвергаются до Open и
изменения памяти. Проверенная long capacity может превышать int byte length: до
projection выбираются только нужные elements, поэтому большой unused tail не вызывает
AsBytes overflow. Byte reader использует bounded owned scratch, typed projection
только sync GetSpan; caller non-array/non-pinnable manager не Pin/Dispose-ится.
Empty read всё равно verified; tail сохраняется. Entered IO failure/cancel может
оставить часть **байтов** одного элемента, successful count при этом не возвращается.

unmanaged исключает managed references, но не задаёт portable serialization. Raw view
содержит runtime layout/endianness/padding, padding влияет на digest/revision.
Caller задаёт/версионирует layout в metadata и обеспечивает deterministic bytes;
автоматического изменения byte order, обнуления padding или type inference из CAS нет.
Canonical model/material/image formats продолжают использовать consumer codec и bytes.
Для Memory<string>/managed object graphs нужен внешний encoder, не raw memory cast.
Тип T автоматически не записывается в каталог. Совпавшая byte length не доказывает
правильность выбранного consumer типа. Перед MemoryMarshal.Cast<byte,T> detached
result проверить length%sizeof(T)==0: сам Cast допускает остаточные bytes, тогда как
ReadRawResourceAsync требует целое число элементов.
Семантика byte projection описана в
[MemoryMarshal.AsBytes](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.asbytes).

## Сохранённые гарантии и acceptance

- Тот же reserve/publish/journal/CAS путь: одинаковые bytes+metadata дают те же
  digest/revision/receipt при Stream,Memory и explicit Span copy. Format не меняется.
- Hash/length/byte limits, whole batch, cancellation/UnknownOutcome/replay/CleanupPending
  остаются прежними. Memory replay не читает caller bytes; Copy replay сначала snapshot-ит span.
- Любой MemoryManager-backed input поддерживается без предположения array-backed;
  адаптер использует bounded chunks. Не обещается OS zero-copy: core использует64KiB IO.
- Generic T не вводит кодеки/renderer/engine dependencies и не раскрывает внутренний KV.
- Extension layer не требует менять IProjectAssetStore и его существующих implementers.

NOM8a проверяет compiler/integration byte mutable/readonly variants, non-array
MemoryManager, slices/offsets/overlap, empty/equality/one-over/overflow и malformed
lengths. NOM8b проверяет T partitions/type inference/divisibility, runtime layout/
nonzero padding, checked sizing и typed owner lifecycle; compiler-negative consumer
подтверждает отказ managed references на publication/copy/read.
Guarded owner проверяет отсутствие
reads/writes после actual task completion; cancel-before-start/mid-IO/Stop и unknown
commit/reopen/replay сохраняют ownership. Destination tail и failed admission не меняются;
частичная copy при entered-IO failure проверяется согласно контракту.

Проверки source Debug/published Release/fresh NuGet public consumer обязательны,
как и stream-equivalence/hash/atomicity/fault regression. Allocations/throughput сравнивать
на существующем workload; никаких zero-copy/performance claims до измерений.
Независимый NomBufferConsumer проверяет public byte API и выполняет сравнительный
16MiB end-to-end IO workload: один warmup и3измерения на каждый Stream/Memory/SpanCopy,
GC.GetTotalAllocatedBytes(true) и elapsed time вокруг publication. Это fixed-order
smoke measurement на малом каталоге, без portable latency/performance gate; не замена
NOM6 large-catalog workload. Полные фактические результаты — [STATUS](STATUS.md),
[история NOM8a](HISTORY.md#nom8a-2026-11-08).
NomRawConsumer добавляет31public checks source/published/fresh NuGet, затем тот же
16MiB workload с uint memory/explicit span snapshot:1warmup+3samples/path. Это также
bounded IO observation без переносимых performance claims; [результаты NOM8b](HISTORY.md#nom8b-2026-11-08).
VM acceptance остаётся отложенной.
