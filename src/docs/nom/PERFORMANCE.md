# Nom: workload, бюджеты и пределы

<a id="nom6-performance-2026-11-08"></a>
## NOM6 — методика 2026-11-08

Macro runner находится в `ManualTests/TrueMoon.Nom.Probe/NomPerformanceProof.cs`.
Он измеряет реальный журнал/CAS, Flush(true), replay, проверку length/hash и owned
worker lifecycle. BenchmarkDotNet или внешний package не добавляется: здесь важны
полные IO операции и большие catalogs, не isolated CPU microbenchmark.
[Backup/restore](BACKUP.md), [текущий статус](STATUS.md), [команды](HISTORY.md#nom6-2026-11-08).

Seed corpora: **1000assets/4000revisions/16000exact edges** и
**50000assets/100000revisions/300000exact edges**. Small opaque metadata, ordinal
tags и valid historical dependency DAG; shared64-byte CAS изолирует metadata cost.
Seed публикуется bounded canonical raw KV batches с тем же Flush(true), а затем
проверяется обычным catalog Open. Это не замер полного importer pipeline: timed
публикации проходят обычный resource reserve/stream/commit путь и добавляют5revisions.

Reopen:5samples в текущем процессе и5отдельных fresh child processes, все bounded и
reaped. Query:9samples; publication/stream:5; checkpoint:3. P50 — median, p95 — nearest
rank (для3/5/9samples фактически максимум); это empirical observations, не confidence
interval. Allocations — delta GC.GetTotalAllocatedBytes(true) всего процесса, включая
owner thread. Allocation median вычисляется отдельно от latency median. RSS/private
memory снимаются после каждой sample, PeakWorkingSet — process lifetime peak всего
workload, включая seed/admission stress. Это не heap или per-operation peak.

OS filesystem cache не очищается. Fresh-process open является process-cold, но **не**
physical cold-disk/cache proof. Текущий процесс уже JIT-warm после seed; background
OS/другие apps не изолированы. Results нельзя переносить на другой диск/OS/RID.
Сравнения с LiteDB и обещаний production latency не делается.

## Реализованные изменения

- KV `ReadPage` использует ленивый sorted key-reference array, binary lower bounds
  для prefix/exclusive continuation и ограниченный sequential page. WriteBatch/compaction
  инвалидируют cache после success; fault требует reopen как прежде. Arrays ответа
  detached; page≤256/1MiB, sequence/generation и single-row refusal сохранены.
  Extra cache — до2million references (около16MiB на x64), без копирования key bytes.
- `NomCatalog.Rows` использует уже прочитанный detached page value, если transaction
  overlay его не заменил; прежнее второе Get/IO/allocation исключено.
- Resource revision validation декодирует canonical metadata один раз и хеширует
  проверенные bytes. Обратная сериализация записи и повторный decode для той же
  проверки исключены. Magic/flags/length/order/hash/IDs проверяются прежними schemas;
  hash domain и exact revision bytes не меняются, independent goldens/recovery проходят.

Baseline малого corpus до оптимизаций: median reopen304.40ms /418849312allocated bytes,
publication168.99ms /184861736bytes. После ordered-page изменения сохранена actual
baseline DLL SHA256 `36EFE62C92221A87F85DBF1D90C6E3C291D367E88B4662D0A8DF21D18B7AEFD2`:
reopen222.82ms /412520960bytes, publication143.30ms /183652848bytes.
Первичный baseline JSON сохраняет exact module hash времени измерения; его бинарный
файл впоследствии пересобран. Ordered baseline binary сохранён отдельно локально.
Это датированные pre-final runs, не текущий passing proof.

## Фактические финальные результаты

| Corpus | Операция | p50 ms | p95 ms | Median allocated MiB | Max sampled RSS MiB |
| --- | --- | ---: | ---: | ---: | ---: |
| 1000/4000/16000 | fresh-process-open-os-cache-uncontrolled | 423.49 | 470.09 | 184.52 | 68.82 |
| 1000/4000/16000 | process-warm-cache-uncontrolled-reopen | 181.86 | 211.91 | 168.27 | 77.53 |
| 1000/4000/16000 | warm-indexed-query | 1.00 | 1.67 | 1.32 | 78.80 |
| 1000/4000/16000 | public-metadata-publication-flush | 74.93 | 115.45 | 73.11 | 105.20 |
| 1000/4000/16000 | checkpoint-compaction-plus-control-append | 135.76 | 156.54 | 6.81 | 105.38 |
| 1000/4000/16000 | checkpoint-reopen | 163.45 | 180.24 | 149.75 | 98.53 |
| 50000/100000/300000 | fresh-process-open-os-cache-uncontrolled | 5844.07 | 5937.17 | 4468.21 | 329.26 |
| 50000/100000/300000 | process-warm-cache-uncontrolled-reopen | 4925.79 | 4948.16 | 4445.26 | 386.78 |
| 50000/100000/300000 | warm-indexed-query | 0.73 | 1.06 | 1.12 | 368.64 |
| 50000/100000/300000 | public-metadata-publication-flush | 2043.72 | 2127.27 | 1733.45 | 782.53 |
| 50000/100000/300000 | checkpoint-compaction-plus-control-append | 3056.94 | 3155.59 | 205.02 | 842.83 |
| 50000/100000/300000 | checkpoint-reopen | 4917.96 | 4945.62 | 4229.79 | 416.86 |

| Streaming | p50 ms | p95 ms | Median allocated MiB |
| --- | ---: | ---: | ---: |
| stream-publish-16777216 | 40.07 | 41.97 | 0.23 |
| verified-open-and-stream-16777216 | 22.07 | 26.34 | 0.24 |
| stream-publish-134217728 | 236.99 | 252.14 | 0.23 |
| verified-open-and-stream-134217728 | 159.80 | 163.45 | 0.99 |

Steady-state amplification 1000assets: 1.02x; physical 3505933bytes, live logical KV+CAS 3448572bytes.
Steady-state amplification 50000assets: 1.02x; physical 90175257bytes, live logical KV+CAS 88173896bytes.
Full performance process PeakWorkingSet: 939.21MiB, runtime 10.0.14, logical CPUs 24.
Maximum streaming: 2objects × 536870912bytes за один 1073741824-byte batch, 1802.76ms, 314408allocated bytes; verified open обоих, +1object limit refused.
Capacity Debug:4/4groups, 92165.15ms, process PeakWorkingSet 1360.02MiB.
Capacity published Release:4/4groups, 79925.70ms, process PeakWorkingSet 1163.99MiB.

Steady-state disk amplification = physical files после measured checkpoint/control
append ÷ (live KV key/value bytes + referenced unique CAS bytes). Это не peak temporary
disk during compaction/export и не отношение к current heads only. После checkpoint
остаются последующие control frames. Во время compaction/backup нужно дополнительное
место для prepared copies; logical quotas не гарантируют свободный filesystem space.

Метрика `checkpoint-compaction-plus-control-append` включает explicit CompactAsync
и последующий reserve/abort control append, чтобы каждая следующая sample выполняла
реальную compaction. Нельзя трактовать её как изолированную чистую скорость codec.
Verified-open-and-stream включает full same-handle hash перед выдачей stream и затем
полное чтение/проверку payload consumer-ом. Payload buffers ограничены64KiB.

## Configurable domain admission

`NomOptions.DomainIndexBytes` copied при UseNom: default64MiB **сохранён**, supported
range1byte..256MiB. Configure larger projects explicitly:

```csharp
using TrueMoon.Nom;

App.Build(context => context.UseNom(options =>
    options.DomainIndexBytes = 256L * 1024 * 1024));
```

Count ceilings остаются100kassets/200krevisions/400kedges. Общий byte ceiling может
сработать раньше них: logical accounting256/asset +Σ(96+2×tagUTF8Bytes),384/revision
+384/edge. Restore использует такой же App budget при проверке prepared catalog;
слишком малый budget отказывает до publication. Snapshot/tag/query limits не повышены.
Logical bytes — **не RSS limit**: current и prepared indexes, recovery maps, strings,
ordered key cache и runtime allocations учитываются отдельно от этого admission.

Большой workload выше реально выполнен с explicit256MiB. При default64MiB synthetic
50k/100k/300k admission явно отказывает по byte quota без disk writes. Отдельно
проверены last accepted100000assets/200000revisions/400000edges и adjacent refusal;
count checks не означают, что максимальные tags/metadata/all counts одновременно
поместятся в одном budget.

Полный metadata domain index по-прежнему готовится bounded pass перед content commit.
Его оставшиеся allocations/latency приведены в таблице; incremental preparation не
объявляется реализованной. При потребности consumer это отдельный измеренный follow-up,
а не скрытое обещание, что операция становится O(changed rows).

## Фактический stress заявленных KV ceilings

`NomCapacityProof` выполняется отдельно от timed workload, source Debug и published
Release. Между независимыми stress groups выполняется explicit GC, не входящий в
latency measurements. Проверены actual files и replay:

| Предел | Actual acceptance и immediately adjacent refusal |
| --- | --- |
| Key/value/frame |1024-byte key,128KiBvalue,4MiBpayload и8192mutations; +1отказывает до IO/state change |
| Key count |2000000keys/200000000logical index bytes и reopen;2000001-й отвергнут |
| Index bytes |268435456logical bytes на250000keys; замена key с +1byte отвергнута атомарно |
| Checkpoint |Реальный268435456-byte checkpoint + verified reopen; +1live byte отказывает до root switch, уменьшение возвращает compaction |
| Replay |65536actual durable Flush(true) batches + reopen;65537-й отказал, CompactAsync восстановил append budget |
| Resource bytes |В performance profile опубликованы2×512MiB за один1GiBbatch и verified open; object maximum+1 отвергнут |

Это acceptance отдельных ceilings, не обещание всех максимальных измерений в одном
combined application. Physical disk-full/power-loss и unsupported FS/hardware не
закрываются stress/injected faults. Fresh NuGet/RID/second consumer — NOM7.

## Повторение

Из src, Windows x64/.NET10/PowerShell7; каждый раз новая evidence directory:

```powershell
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Performance -EvidenceDirectory TestResults/MyNomPerformance
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Capacity -EvidenceDirectory TestResults/MyNomCapacity
```

Performance запускается только published Release; Capacity — source Debug и published
Release. Default All выполняет functional120groups/profile без тяжёлых Performance/
Capacity profiles. Все SDK invocations имеют unique binlogs, child processes bounded
и reaped; restore/build/publish используются actual projects с empty feed/cache без
external packages. История сохраняет конкретные machine/run numbers и ограничения.
