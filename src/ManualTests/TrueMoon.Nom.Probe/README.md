# TrueMoon.Nom.Probe — собственный каталог и KV

Windows x64/net10.0 regression consumer для [TrueMoon.Nom](../../TrueMoon.Nom/README.md).
NOM2 заменил vendor-specific BSON/WAL/download runners. Исторические LiteDB результаты
сохранены в docs/nom; текущие runners не загружают и не используют прежний backend.

Из src, .NET10 SDK и PowerShell7:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomCore
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/MyFreshNomApp
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Buffer -EvidenceDirectory TestResults/MyFreshNomBuffers
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Raw -EvidenceDirectory TestResults/MyFreshNomRaw
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/MyFreshNomPackages
& ./ManualTests/TrueMoon.Nom.Probe/VerifyDurability.ps1 -EvidenceDirectory TestResults/MyFreshNomDurability
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile VmProtocol -EvidenceDirectory TestResults/MyFreshNomVmProtocol
```

Всегда выбирать новую evidence directory. Failed evidence и temporary artifacts
сохраняются. Каждый SDK command имеет log и уникальный binlog; процессы bounded,
создаются без видимых окон и завершаются/reap в finally. Source hashes защищают
результаты от drift во время запуска. Отсутствие removed backend проверяется в
restored/runtime graphs, binaries и loaded assemblies.

Verify.ps1 строит actual Nom/Contracts ProjectReferences с изолированными artifacts,
пустыми feed/cache и CPM без package entries. Graph содержит0external packages.
Source Debug и framework-dependent published Release исполняют:

-18catalog/streaming groups: own codec/identity/corruption/no-reset, owner thread,
  binding rollback, bounded metadata/queue, actual cross-process lock/write denial,
  failed startup/drain; geometry/BMP/GLSL/32MiB stream,64KiB buffer, lengths/hash,
  atomic CAS/batches/journal, cancellation/ownership/read leases, tamper rejection.
-6KV groups: Create/Get/WriteBatch/reopen, cross-process owner contention и4actual
  kills before-write/partial-write/after-write/after-flush.6started=reaped children/profile.
-4additional resource child kills/profile: after-stage/after-finalize/before-commit/
  after-commit. Проверяются whole old/new state, exact bytes и idempotent outcome.

NOM3 добавляет Fault41groups/profile:6domain phases×4cuts,3tail repair и14Create cuts.
48started=killed=reaped children/profile, включая7prepare children с настоящим pending.
Проверяются exact bytes/outcomes/generation/floor, no-reset и actual lock до kill.
NOM4 добавляет Maintenance27groups/profile:24actual killed/reaped children на cp/segment/
root preparation/switch/old-handle cleanup и3semantic groups. Проверяются epochs,
post-cp append/deletes, public journal/CAS и corrupt selected cp без fallback.
NOM5 добавляет Retention12groups/profile:12actual started=killed=reaped children на
prune write/partial/flush, CAS delete и ack write/partial/flush. Pinned old dependency
bytes/current heads сохраняются; explicit resume/unpin/GC подтверждают liveness.
Optional -Profile Core, Kv, Fault, Maintenance, Retention или Backup выбирает functional набор. NOM6 Backup добавляет16export/restore process kills/reaps/profile (write/partial/flush/actual close/directory publish); Default All исполняет120functional groups.
Reports содержат exact module path/version/SHA, framework, observations и failures.
Reparse fixture может быть privilege-gated; этот subcase не объявляется выполненным.

VerifyApp.ps1 копирует PublicAppConsumer.cs.template в independent OS TEMP project
с AssemblyName=NomPublicConsumer. Только публичные App/UseNom/factory/store APIs,
без friend access: source Debug и published Release по34checks,2MiB+17bytes exact
roundtrip/reopen/outcome, borrowed input, early product disposal/host stop; CompactAsync/read-lease refusal, retained tickets, idempotent repeat и post-cp append; NOM5 tags/reverse, frozen exact dependencies, durable pins/reimport/reopen/old bytes/stream retention/GC/post-GC checkpoint; NOM6 verified backup и typed restore factory/exact IDs/versions/dependencies/pins/owner registry/source isolation.
App graph использует реальные Cobalt/Generator и workspace CPM; он имеет Roslyn
build packages, поэтому не объявляется zero-package graph.

`VerifyApp.ps1 -Consumer Buffer` создаёт независимый NomBufferConsumer из
PublicBufferConsumer.cs.template:28checks source Debug/published Release для byte
Memory/Span/readonly API, non-pinnable managers/slices/overlap, whole batch/dictionary
capture, snapshot-before-return, replay/budgets/cancel/tail/detached lifetime.
Он также измеряет16MiB end-to-end publication:Stream/Memory/SpanCopy, warmup+3samples,
allocated bytes и elapsed time. Это bounded IO smoke, не переносимый benchmark.

`VerifyApp.ps1 -Consumer Raw` создаёт NomRawConsumer:31public checks D/R для raw
T:unmanaged, generic inference всех четырёх input variants, packed3byte chunk splits,
typed element counts/tails, nonpinnable managers/replay, bool/char/native/enum/padding,
empty/budgets/checked sizing/divisibility/cancel. Detached typed views остаются byte
load+explicit Cast.16MiB uint IO workload измеряет RawMemory/RawSpanCopy по warmup+3samples.

VerifyPackages.ps1 формирует четыре пакета одной уникальной local alpha версии:
TrueMoon.Contracts, TrueMoon.Cobalt (с generator), TrueMoon и TrueMoon.Nom.
Сборка пакетов использует существующий compiler cache для Roslyn; каждый внешний
consumer получает отдельный пустой cache и NuGet.Config только с новым local feed,
без fallback и ProjectReference. Public App consumer, NomBufferConsumer, NomRawConsumer и самостоятельный
[NomAssetTool](../NomAssetTool/README.md) выполняются вне checkout в portable Debug,
portable published Release и win-x64 framework-dependent Release. Используется
установленный .NET10 runtime; apphost/runtime packs не доставляются. Проверяются
nuspec/license/icon/readme, managed API/references, package hashes в caches/output,
ровно четыре зависимости и packaged Cobalt analyzer. Negative controls: отсутствие
Nom в feed (NU1101), запрет internal API (CS0122), CLI admission без изменения root.
RawManagedRejection.cs.template обязан получить CS8377 отдельно на publication/copy/
read lines для string и managed-field struct:ослабленный unmanaged contract не проходит
package acceptance. Consumer и compiler-negative assemblies не имеют friend access.
NomAssetTool повторяет cleanup/reopen и проверяет сохранность source после restore/GC.
Пакеты копируются в evidence/packages; verification.json содержит SHA и provenance.
Команда не публикует пакеты во внешний feed. Полный [scope NOM7](../../docs/nom/DELIVERY.md).

Process kill не заменяет physical disk-full/power loss. [NOM3 IO/process matrix](../../docs/nom/RECOVERY_ACCEPTANCE.md)
проверена, physical gate открыт. CheckFaultVolume.ps1 проверяет только создание нового64MiB
VHDX внутри workspace: Windows authorization пока отказывает; existing disks не затрагиваются.
[VerifyDurability.ps1](VerifyDurability.ps1) добавляет fresh-package public consumer и
пять real-volume сценариев на новом собственном128MiB VHDX: staging/catalog append/
checkpoint/export/restore.24pure guard cases и12consumer assertions/profile прошли;
после UAC physical5cases/166assertions каждый Debug/published Release также прошли,
новый VHDX отключён без cleanup errors. Exit2/DiskFullPassed-PowerLossOpen
и Completed=false сохраняют прежний legacy gate. В новом scope аппаратное отключение
питания исключено пользователем; [VM controller](../../docs/nom/VM_CRASH.md) реализован
отдельно и без готового образа возвращает Blocked до создания/TurnOff любой VM.
98VMguards и10host protocol groups D/R прошли; actual VM acceptance не выполнена и отложена пользователем.
[Контракт, безопасность и scope](../../docs/nom/DURABILITY.md).
[Rotation/checkpoint/compaction NOM4](../../docs/nom/COMPACTION.md) проверены в IO/process scope; [indexes/snapshots/pins/GC NOM5](../../docs/nom/RETENTION.md) также проверены,
NOM6 [backup](../../docs/nom/BACKUP.md) и [performance/limits](../../docs/nom/PERFORMANCE.md) проверены; fresh packages/RID matrix — [NOM7](../../docs/nom/DELIVERY.md). Другие OS/RID, self-contained, AOT и trimming не проверены. Aether/GPU integration не выполняется.

[Каталог и границы](../../docs/nom/CATALOG.md#nom2-catalog-2026-11-08),
[текущие результаты](../../docs/nom/STATUS.md), [план](../../docs/nom/PLAN.md#nom-own-queue).
NOM6 heavy profiles запускаются отдельно, не Default All:

```powershell
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Capacity -EvidenceDirectory TestResults/MyNomCapacity
pwsh -NoProfile -File ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -Profile Performance -EvidenceDirectory TestResults/MyNomPerformance
```

Capacity:4groups source/published, actual2mkeys/256MiBindex+checkpoint/65536flush batches
и adjacent refusals. Performance:только published Release,1k/4k/16k +50k/100k/300k,
fresh/reopen/p50/p95/allocations/RSS/disk и16/128MiB/1GiBbatch. Cache uncontrolled,
не physical cold-disk, никакого LiteDB speedup claim. Raw stress directories сохраняются.
