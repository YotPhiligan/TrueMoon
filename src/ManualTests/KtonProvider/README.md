# Kton PHYS2–3: real BEPU provider acceptance

Из корня `src`, PowerShell7 и SDK из общего global.json:

```powershell
pwsh -NoProfile -File ManualTests/KtonProvider/Verify.ps1 -EvidenceDirectory TestResults/KtonPHYS3-local-001
```

Evidence directory должен быть новым. Без PackageDirectory runner скачивает два
официальных beta.33 nupkg и проверяет их зафиксированные SHA256. Для запуска без
скачивания передайте `-PackageDirectory` с bepuphysics/bepuutilities2.5.0-beta.33.nupkg.

Общий BuildProfile задаёт SDK/TF/TestsTF/LangVersion/Features; consumer изолирует CPM,
NuGet sources/cache и artifacts. Source Debug использует настоящий provider project.
Затем Contracts/Kton/Kton.Bepu собираются в3свежих пакета одной версии; consumer с
exact provider reference запускается Debug и published Release. Restored graph —
ровно5packages: эти3 и точные2BEPU. Все5loaded DLL SHA сравниваются с архивами.
Remote publish не выполняется. MSBuild вызовы имеют unique binlog, processes —
stdout/stderr,120s timeout/reap и exit code. Исходники сверяются по SHA в конце.

Проверки используют настоящую BEPU simulation: два мира, bodies/shared captured
mesh, filters/friction/sensor response, BallSocket/connected-pair filter, commands,
whole-batch/state-dependent admission, fixed ticks/cancellation/Busy/Faulted,
generation reuse/retirement, repeated create/remove/dispose и fast CCD fixture.
Mesh churn дополнительно проверяет pool plateau в одном живом мире.

PHYS3 matrix: nearest/all ray, sweep/overlap для9primitive pairs, translated/rotated
shapes и mesh, triangle identities/backfaces/ties, filters/sensors, generational
reuse, MaxDistance/MaxHits/empty output/tail preservation, scratch admission включая
BVH stacks. Repeated primitive и512triangle mesh queries сохраняют pool plateau и
bounded managed allocation после warmup. Это allocation/functional probe, не formal
performance benchmark и не frame-time guarantee для всех сцен.

Contact checks: Begin/Stay/End, physical/speculative distinction, exact mesh child
identity/point/normal, manual/automatic sleep/wake, support delete/slot reuse,
teleport End→Begin, cancellation с pending End, full snapshot copy и raw/event/
sleeping-cache overflow. Failed Step не публикует tick или доступный partial snapshot.

Внутренний friend seam `KtonProviderProbe` проверяет bounded failure points создания,
command commit, callback failure и cleanup stages. Поколение uint.MaxValue seeding
используется только для проверки реального allocator retirement без миллиардов
итераций. Эти hooks не являются public provider API. Cleanup fault injection
доказывает retry между завершёнными стадиями; произвольный partial failure внутри
BEPU.Dispose/heap corruption/power loss этим не моделируются. Один worker не
доказывает многопоточную callback/worker-error матрицу.

`Capabilities` содержит RigidBodySimulation/Queries/ContactEvents. Bounded snapshot
не сохраняет borrow refs; generation проверяется до query/admission и входит в
pair identity. App/DI/configuration — PHYS4, ECS — PHYS5; new factory в этом console
composition root не заменяет эти этапы.

Сводки: [PROVIDER](../../docs/physics/PROVIDER.md),
[queries/events](../../docs/physics/QUERIES_CONTACTS.md),
[HISTORY](../../docs/physics/HISTORY.md), [STATUS](../../docs/physics/STATUS.md).
Другие платформы/AOT/parallel workers и full solution tests требуют своих запусков.
