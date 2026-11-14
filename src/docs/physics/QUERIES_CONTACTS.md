# Kton PHYS3: bounded queries и gameplay contacts

**2026-11-12 — реализовано и проверено на exact BEPU beta.33, Windows x64/1worker.**
[Контракт v1](CONTRACTS.md), [provider](PROVIDER.md), [план](PLAN.md#phys3),
[команды/evidence](HISTORY.md#phys3-2026-11-12), [manifest](PHYS3_RESULTS.json),
[runner](../../ManualTests/KtonProvider/README.md).

Provider объявляет `RigidBodySimulation | Queries | ContactEvents`. Публичные
сигнатуры PHYS1 не менялись; XML descriptions limits уточнены. App/DI/configuration
реализован отдельно в [PHYS4](APP.md); ECS/session bridge — PHYS5.

## Запросы и lifetime

RaycastNearest возвращает copied hit; Raycast/Sweep — nearest sorted prefix в
caller-owned Span. Overlap возвращает только body/shape/subshape identities.
Primitive subshape=-1, mesh subshape=triangle index. Shapes живут в этом мире и
не меняются после создания. Optional ExcludeBody проходит live identity lookup:
default означает отсутствие exclusion, malformed/foreign/stale — typed error.

```csharp
using System.Numerics;
using TrueMoon.Kton.Queries;

var output = new PhysicsHit[16];
var result = world.Raycast(new PhysicsRay(new(0, 5, 0), -Vector3.UnitY, 20), output);
for (var i = 0; i < result.WrittenCount; i++) Consume(output[i]);
// result.Truncated explicitly reports additional hits beyond the returned prefix.
```

- Distance в метрах, direction unit. Ray/sweep ordering: computed distance →
  body slot → generation → subshape. Overlap: body slot → generation → subshape.
  Равная distance сравнивается точно, без скрытого fuzzy tie. Callback order BEPU
  не становится API, cross-platform bitwise determinism не обещается.
- Returned count<=min(output.Length, MaxQueryHits). Заполняется только prefix,
  tail остаётся прежним. Empty output допустим: count0/Truncated=true при наличии
  hits. Truncation не fault и не обещает total hit count. Ошибка validation/scratch
  происходит без записей caller output; Hit values не содержат borrowed refs.
- LayerMask — односторонний query filter, независимо от collision masks тел.
  IncludeSensors и live ExcludeBody применяются к ray/sweep/overlap одинаково.
- Origin внутри convex volume и sweep initial overlap дают distance0,
  StartedInside=true/HasNormal=false. Point/Normal тогда не определены. Для mesh
  нет синтезированного closed-volume inside. Ray на поверхности zero-thickness
  triangle может вернуть distance0 с известной face normal.
- Sweep только translational convex sphere/box/capsule. Scene bodies рассматриваются
  в текущей pose, их будущая velocity не интегрируется. Angular sweep/concave query
  shape UnsupportedGeometry. Exact narrowphase отвергает AABB-only overlap.
- Static mesh winding clockwise/front, backfaces cull. Sweep/overlap допускает
  triangle только при start centre-of-mass query shape на front plane side либо
  на plane. Обратная сторона не становится volume collision. Это явно выбранная
  single-sided policy, а не произвольный двусторонний overlap mesh.

## Bounded storage и BVH

На world creation выделяется один bounded PhysicsHit collector. Примерное значение
MaxQueryHits=256 не требует собирать все hits: sorted insertion хранит лишь nearest
prefix. Primitive scalar tests и exact pair SweepTask используют реальные BEPU
shapes. Mesh ray идёт через BEPU Mesh.RayTest/BVH; sweep/overlap — через BVH overlap
visitor с conservative local bounds, затем exact convex/triangle task. Triangle
identity приходит от leaf traversal, не угадывается из aggregated sweep callback.
Query callbacks provider-internal; IO/DI/ECS/user delegate отсутствуют.

World-level enumeration пока сканирует до MaxBodies. Для meshes проверяются BVH
candidates; unbounded candidate Lists и полный triangle scan не используются.
Conservative padding расширяет лишь broadphase bounds и не создаёт hits. Это
functional/allocation acceptance, не обещание1ms для любого большого world.

MaxQueryScratchBytes ограничивает **payload** collector и traversal stacks.
Коллектор выделяется с capacity=min(MaxQueryHits, budget/sizeof(PhysicsHit));
query admission проверяет нужный prefix и полный resident collector payload.
Для каждого включённого mesh заранее берётся максимальная conservative BVH reserve;
meshes обрабатываются последовательно, поэтому reserves не суммируются.

В pinned beta.33 tree traversal начинается со stackalloc int[256]. При overflow
stack удваивается через BufferPool; resize временно держит старый и новый buffers.
Для ntriangles<=256 reserve=1024bytes, иначе1024+6*nextPowerOfTwo(ntriangles).
Это upper bound по возможной глубине дерева, не измеренная средняя глубина.
Collector+reserve>budget — ScratchLimitExceeded **до traversal/output writes**,
мир остаётся Ready. Excluded/filtered meshes reserve не требуют. Малый budget
может консервативно отклонить query даже для ray, не попадающего в mesh.

Источники этой reserve: [pinned Tree_RayCast](https://github.com/bepu/bepuphysics2/blob/29ed466870534c413aba9eeccd813cf15d135d0b/BepuPhysics/Trees/Tree_RayCast.cs),
[pinned Tree_VolumeQuery](https://github.com/bepu/bepuphysics2/blob/29ed466870534c413aba9eeccd813cf15d135d0b/BepuPhysics/Trees/Tree_VolumeQuery.cs).
При обновлении BEPU алгоритм/размер stacks требуется пересверить. Фиксированные
ABI locals/array headers и retained BufferPool blocks не являются scratch payload;
backend pool high-water учитывается отдельно. Это не hard process RAM cap.

## Contact stream и generation

Контакты копируются в bounded single-worker samples непосредственно из BEPU
manifolds. Speculative-only negative depth не создаёт Begin. Из manifold берётся
самая глубокая физическая точка; tie — world PointXYZ. Для mesh используется
child callback с явным triangle index; root aggregate не дублирует его. Point
копируется в world space, normal переводится из B→A BEPU в canonical A→B.
Это narrowphase contact point внутри fixed tick, не обещание совпадения с будущей
post-integration/render pose без interpolation.

Pair key содержит **полные BodyId с World/Slot/Generation** и subshapes. Canonical
A/B выбирается по body slot/generation, независимо от backend pair order. IsSensor
есть в owned payload, solver response sensor pair отключён. Collision layers и
connected-joint filter применяются до record. Static/static и kinematic/kinematic
sensor events отдельно не синтезируются — v1 требует хотя бы одно Dynamic тело.

| Сценарий | Transition |
| --- | --- |
| Новая физическая/touching pair | Begin |
| Сохраняющаяся awake pair | Stay |
| Оба участника inactive/static | Pair identity сохраняется, повторных Stay нет |
| Wake сохранившейся pair | Stay, без ложного Begin |
| Реальное разделение / pair исключена collision filter | End / Separated |
| RemoveBody, в том числе support у sleeper | Pending End / BodyRemoved со старым ID |
| Teleport любого участника | Pending End / Teleported; возможен новый Begin следующего Step |
| Delete и reuse slot до следующего Step | Старый End и новый Begin имеют разные Generation |

End не содержит contact point/normal/depth. Если один и тот же live pair key
заново возникает после teleport в том же tick, **End идёт до Begin**. Повторные
teleports до Step не дублируют End уже invalidated pair. Pending Ends preflighted
до mutation, включая весь command batch; общая pair инвалидируется лишь однажды.
Предварительная cancellation/invalid tick не очищает pending transitions или
previous published snapshot. Retry публикует End один раз.

## Capacity, overflow и публикация

MaxContactEvents задаёт capacity **каждого** bounded raw sample, retained pair,
pending End, working/published event buffer; active/current caches также bounded.
Это уточнение PHYS1 field description, а не единый byte cap всех buffers. При
значении8192 одновременно возможны отдельные bounded snapshots/cache; их RAM
не следует считать как8192байта или складывать с MaxQueryScratchBytes.

Callback при исчерпании raw buffer только ставит overflow flag, без exceptions,
allocations и внешнего кода. После join owner делает dedup, pair diff, sleep retention
и deterministic event sort. При raw/cache/final event overflow —
**ContactOverflow + World.State=Faulted**; LastCompletedTick не продвигается,
частичный snapshot не становится доступным. Старые caller-owned copies независимы.
Например, capacity1 может вместить первый Begin, но не End+Begin после teleport
в сохраняющийся overlap: это явный fault, а не потеря одного gameplay event.

Если известные pending Ends не помещаются, operation отклоняется CapacityExceeded
до mutation. Новые contacts после simulation заранее неизвестны и могут вызвать
ContactOverflow. Counts не растут без предела и не переключаются на heap fallback.
Публикация snapshot происходит после успешного worker join/reduction; nonfinite
contact input сообщает callback failure и Faulted через существующий Step protocol.

CopyContactEvents требует output.Length>=полного snapshot count и иначе ничего
не пишет. Это copy, не drain; повторное чтение того же successful tick разрешено.
Только prefix count инициализируется. После следующего successful Step/disposal
provider-owned snapshot меняется, но copies caller остаются самостоятельными.

## Фактическая приёмка и продолжение

309checks/profile включают PHYS2 regression и125новых PHYS3 outcomes:9primitive
pairs, transforms/mesh/ties/backfaces, filters, complete generation checks,
scratch/capacity/MaxDistance/output ownership, Begin/Stay/End, auto/manual sleep,
delete/reuse/teleport/cancel, raw/event/sleeping-cache overflow и cleanup.

После warmup1000primitive overlap calls и2000ray/sweep calls по512triangle mesh
дали0managed allocation delta и одинаковые4,194,304pool bytes before/after.
Automatic sleep fixture за600ticks:1Begin/31Stay/0End, inactive body,1retained pair.
Это isolated observational counters, не all-input allocation/latency guarantee.

Матрица PHYS3 не проверяла App/Cobalt/configuration/IFactory/lifecycle;
их отдельный [PHYS4 proof](HISTORY.md#phys4-2026-11-12) завершён. ECS bridge — PHYS5.
Other OS/AOT/2+workers, full solution/GPU/
release/performance matrix этими checks не закрыты. Cleanup доказательства PHYS2
сохраняют свои ограничения stage-boundary failure injection.
