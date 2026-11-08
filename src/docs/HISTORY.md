# TrueMoon: история общей документации

<!-- Nom-only staging: module event records are included below; full unrelated UI/Editor
     status snapshots remain outside this commit. Nom-specific archives and complete
     command/result history are in docs/nom/HISTORY.md. Existing committed history is preserved. -->
<a id="nom-commit-2026-11-08"></a>
## Nom: проверка отдельного commit scope — 2026-11-08

Перед Nom-only commit/push повторены862/862 теста Nom каждый Debug/Release,
0failed/0skipped; C# и зависимости не менялись. Включены модуль/тесты/probes/tool,
документация Nom и его solution/navigation записи. Параллельные UI/Editor/engine
правки и newline-only Directory.Packages.props оставлены локально. В общих документах
сохраняются только записи Nom; полная module history — [здесь](nom/HISTORY.md#nom-commit-2026-11-08).
Предыдущие package/solution/native/physical proofs не выдаются за новый запуск.
VM acceptance отложена, hardware power-off исключён.

<a id="nom-structure-2026-11-08"></a>
## Nom: функциональная структура исходников — 2026-11-08

По запросу пользователя [Nom sources](nom/STRUCTURE.md#nom-source-layout-2026-11-08)
разложены по подсистемам с соответствующими namespace:60moves+NomLifecycle extraction,
66files. Публичные типы также перенесены; alpha consumers обновляют imports и
пересобираются. Код/формат/lifecycle/dependencies сохранены, bodies/partial groups
проверены отдельно. Source freeze теперь рекурсивный, package audit проверяет namespaces.

Новые фактические проверки:Nom862/862 каждый D/R;120functional groups/profile
source/published; public App34/Buffer28/Raw31checks D/R; fresh4package/4consumer
portable Debug/published Release/win-x64 FDD delivery, managed/internal negatives.
Durability template только compiled D/R, не запускался. Full nonincremental solution
Debug0errors/1560warnings, Nom/Nom.Tests0warnings. [Команды/границы](nom/HISTORY.md#nom-structure-2026-11-08).
Nom/engine/UI/release очередь сохраняется. Actual VM verification отложена,
hardware power-off исключён; native/GPU/physical/heavy capacity acceptance не повторялась.

<a id="nom8b-2026-11-08"></a>
## Nom: unmanaged raw helpers реализованы — 2026-11-08

[NOM8b](nom/BUFFER_API.md#nom8b-raw-api) добавляет raw T:unmanaged Memory/Span
publication/copy и complete typed read с element count. Checked byte sizing,
divisibility, runtime layout/endianness/padding и actual borrowed lifetime зафиксированы.
Storage interface/format/App/dependencies сохранены; portable serialization не вводится.

Actual proofs:862/862 Nom каждый D/R (73новых raw cases),120functional groups/profile
source/published; public Raw31/Buffer28/App34checks D/R; fresh4package/4consumer delivery
в portable Debug/published Release/win-x64 FDD Release. Managed T compiler negative
получил CS8377 на каждой из трёх операций. Full solution nonincremental Debug:
0errors/1560warnings, Nom/Nom.Tests0warnings. [Команды/точный scope](nom/HISTORY.md#nom8b-2026-11-08).
NOM8 завершён в local scope; следующий bridge — engine g.2 после importer g.1,
по согласованию владельца engine. UI/Editor/release очередь сохранена, текущий
Vulkan frosted MenuBackdrop blocker не относится к Nom и этим запуском не закрыт.
Actual VM verification отложена пользователем, hardware power-off исключён.

<a id="nom8a-2026-11-08"></a>
## Nom: byte buffer API реализован — 2026-11-08

[NOM8a](nom/BUFFER_API.md#nom8-buffer-api) добавляет borrowed memory publication/batch,
explicit Span copy, complete read в caller Memory и detached bounded load. App/DI,
storage interface/format/dependencies сохранены. Copy snapshot-ит каждый вызов до
Task return, включая replay; borrowed memory replay не читает source bytes.

Новые actual proofs:Nom789/789 каждый Debug/Release (71новый byte case),120functional
groups/profile source/published, public Buffer28/App34checks D/R, fresh4package
delivery с тремя consumers в portable Debug/Release и win-x64 FDD Release.
Full nonincremental solution Debug:0errors/1560warnings, Nom/Nom.Tests0warnings.
Bounded16MiB IO observation подтверждает стоимость explicit snapshot; performance
promises не добавлены. Exact commands/hashes/test matrix/scope — [Nom HISTORY](nom/HISTORY.md#nom8a-2026-11-08).
NOM8b raw T:unmanaged — следующий Nom этап; engine/UI/release очереди сохранены.
Actual VM verification остаётся отложенной пользователем, hardware power-off исключён.
Другие OS/RID/AOT/trimming/external publish и heavy NOM6 workload не повторялись.

<a id="nom8-design-2026-11-08"></a>
## Nom: buffer API предусмотрен, реализация ещё не начата — 2026-11-08

По запросу пользователя [NOM8 draft](nom/BUFFER_API.md#nom8-buffer-api) проектирует
Memory/Span и readonly variants:borrowed async memory, explicit bounded span copy,
detached reads и explicit unmanaged raw contract. Это план public resource helpers,
не изменение текущего API/package/format. Новых SDK/tests/benchmarks нет, performance
claims не делаются. VM verification остаётся отложенной, engine очередь не меняется.
[Единственный подробный план](nom/PLAN.md#nom-own-queue).

<a id="nom-vm-deferred-2026-11-08"></a>
## Nom VM verification отложена — 2026-11-08

По просьбе пользователя actual VM run отложен из-за подготовки image/credential.
Harness сохранён, acceptance не закрыта, аппаратный power-off исключён; дальнейшая
разработка Nom/engine этим не блокируется. Подготовка VM параметров не активная задача,
возврат — по новому запросу. Никаких новых build/test/VM results: это документационный
шаг. [Решение и scope](nom/HISTORY.md#nom-vm-deferred-2026-11-08).

<a id="nom-vm-2026-11-08"></a>
## Nom guest-crash harness, hardware power исключён — 2026-11-08

По решению пользователя аппаратное отключение питания исключено из текущей Nom
приёмки; реализован owned Hyper-V VM controller/external durable witness/raw snapshot/
distinct boot/recovery,7IO cutpoints.98guards и10host-process protocol groups каждый
D/R прошли,7children/profile reaped;120functional groups/profile повторены.
Full solution Debug0errors/1560warnings, Nom/Probe0; новые diagnostics в параллельном
EditorHost, его код не менялся в Nom. Actual VM0trials/Blocked:нужны prepared Windows
Gen2 VHDX и локальный guest credential. HardwarePowerLossRequired=false; VM acceptance
не подменяется process kills. Прежний UAC disk-full scope сохраняется, engine/Editor
очереди не меняются. [Контракт](nom/VM_CRASH.md), [команды/evidence](nom/HISTORY.md#nom-vm-2026-11-08).

<a id="nom7-uac-2026-11-08"></a>
## Nom: UAC real-volume disk-full/recovery passed — 2026-11-08

По разрешению пользователя runner запущен через Windows UAC, Administrator=true.
Новый owned128MiB NTFS/VHDX прошёл5physical cases/166assertions каждый Debug/published
Release consumer:staging/catalog append/checkpoint/export/restore; free0/OS112, exact
old bytes/head/ack outcome, reopen и новая generation2. VHDX отключён, cleanup0.
Исправлены assumptions harness о первом append при full volume и filler ownership;
production API/source не изменён.24guards/12assertions D/R и fresh package proof
App34/tool211 также прошли. Итоговый exit2/DiskFullPassed-PowerLossOpen ожидаем:
disk-full gate закрыт в bounded NTFS/VHDX scope, power-loss/остальные release gates
открыты. Unit/full solution/native не повторялись. Editor ED5a.3 и engine очередь
сохранены; [команды/failures/границы](nom/HISTORY.md#nom7-uac-2026-11-08).

<a id="nom7-durability-2026-11-08"></a>
## Nom physical durability follow-up — 2026-11-08

Добавлен isolated128MiB VHDX disk-full harness/public package consumer: staging,
catalog append,checkpoint,export,restore;24pure guards и12consumer assertions каждый
Debug/published Release прошли, compile0new warnings. Fresh4package delivery повторён:
App34checks/3NuGet profiles, tool211checks/4profiles. Physical cases0:Windows New-VHD
authorization снова отказала до создания/подключения/format, runner exit2/Blocked,
Completed=false. Power-cut rig не предоставлен, автоматического cut runner нет;
physical disk-full/power-loss остаются открыты. Unit/full solution/native не повторены.
Editor ED5a.3/engine и release очереди сохранены, shared docs с fresh SHA guards.
[Подробные команды/ошибка](nom/HISTORY.md#nom7-durability-2026-11-08),
[acceptance contract](nom/DURABILITY.md).

<a id="nom7-2026-11-08"></a>
## Nom NOM7: package delivery и independent consumer — 2026-11-08

Nom IsPackable=true, standalone README/license/icon и minimal Contracts dependency.
Fresh local4package closure, два public no-friend consumers без ProjectReference/fallback;
portable Debug/published Release и win-x64 framework-dependent Release проверены.
NomPublicConsumer34checks/profile, NomAssetTool211checks/9success+2admission-rejections
в source Debug и каждом из3NuGet profiles; exact pins/backup/typed restore/GC/reopen,
source сохранён. Actual internal CS0122/missing package NU1101 controls passed.
Nom.Tests718/718 каждый D/R,120functional groups/profile и App34checks source/published.
Solution nonincremental Debug0errors/1555warnings; Nom/Tests/Probe/Tool0warnings.
Full other-module suites/native и NOM6 heavy profiles здесь не запускались.
Общая версия не изменена, external publish не выполнялся; physical NOM3/4 и остальные
OS/RID/modes остаются открыты. Editor ED5a.2/engine f.3→f.4/g.1 очередь сохранена.
Detailed commands/version/four hashes/artifact retention — [Nom HISTORY](nom/HISTORY.md#nom7-2026-11-08),
[delivery scope](nom/DELIVERY.md). Shared docs заменены после fresh SHA check атомарно.

<a id="nom4-2026-11-08"></a>
## Nom NOM4: checkpoint и offline compaction — 2026-11-08

Explicit CompactAsync/checkpoint/rotation реализованы с сохранением public resource
outcomes/CAS/floor/generation.535initial→536finalcases:536/536 D/R+solution-filter,
110new, source/published92groups/profile (Maintenance27/24actual kills+reaps, прежние
Core18/KV6/Fault41 повторены). Public no-friend App16checks/profile. Solution Debug
0errors/1555warnings, Nom0warnings; full suite/engine/GPU/performance не запускались.
Physical VHD gate снова Windows denied/non-Administrator; disk-full/power-loss не
закрыты. Следующий implementation этап NOM5. [Scope/команды](nom/HISTORY.md#nom4-2026-11-08).

<a id="nom3-2026-11-08"></a>
## Nom NOM3: расширенные faults/recovery, physical gate открыт — 2026-11-08

426/426 Nom.Tests D/R и solution-filter,44newcases; Core18+KV6+Fault41 groups каждый
source Debug/published Release;48actual killed/reaped children/Fault profile.
Public no-friend App10checks/profile повторён. Исправлены reservation headroom,
poisoned pending-read stop и handoff при stop-during-reserving. Solution Debug
nonincremental0errors/1555warnings, Nom0warnings. Новых full suite/GPU/performance/
package результатов нет. Physical VHDX creation отказана Windows authorization,
Administrator=false: полный NOM3 остаётся частичным до isolated-volume acceptance.
[Scope/команды](nom/HISTORY.md#nom3-2026-11-08), [матрица](nom/RECOVERY_ACCEPTANCE.md).

<a id="nom2-2026-11-08"></a>
## Nom NOM2: собственный App/resource backend — 2026-11-08

UseNom/public factory/store теперь используют свой KV; typed catalog records и
atomic resource publication заменили LiteDB/BSON. Runtime/package/CPM и vendor
runners/downloads удалены. Legacy roots rejected/no reset; API/lifetimes сохранены.
382/382 Nom.Tests каждый D/R и solution-filter;18core+6KV groups каждый source Debug/
published Release, public no-friend App consumer10checks/profile. Isolated core graph
0external packages; restored/runtime/fresh output audit без LiteDB. Solution Debug
nonincremental0errors/1555warnings, Nom warnings0. Остальные модули не тестировались;
NOM3 integrated faults/physical gate, NOM4 maintenance и NOM7 delivery ещё нужны.
[Подробности и команды](nom/HISTORY.md#nom2-2026-11-08).

<a id="nom1-2026-11-07"></a>
## Nom NOM1: working KV writer и actual crash recovery — 2026-11-07

Реализован [low-level KV](nom/KV_CORE.md#nom1-kv-2026-11-07): Create/Open/Get/WriteBatch,
exact expected sequence/generation, durable flush/Unknown tickets и retained cleanup.
316/316 Nom.Tests каждый D/R и solution-filter,83newcases.6groups source Debug/
published Release,6children started=reaped/profile,4actual kill cutpoints/profile.
Solution nonincremental Debug0errors/1555warnings, Nom warnings0. Прежние2Skia warnings
ушли в параллельной UI работе, не исправлялись этим срезом. Full suite/GPU/fresh packages/
physical gates не проверялись, старые Aether/vendor failures не закрыты.
App/resource backend ещё LiteDB: полный cutover/удаление — NOM2.
[Подробный scope/команды/evidence](nom/HISTORY.md#nom1-2026-11-07).

<a id="nom0-2026-11-07"></a>
## Nom NOM0: binary codecs и read-only recovery — 2026-11-07

[Format v1](nom/FORMAT.md#nom0-format-2026-11-07) закрепил root/segment/batch authority,
strict keys/metadata и новый revision domain. Codecs и read-only bounded replay с
sealed chain state реализованы; 233/233 Nom.Tests каждый D/R, 147new cases,
failed/skipped0. Solution nonincremental Debug0errors/1557warnings (1555baseline+2CS8620 в SkiaFontCatalog).
App/storage backend остаётся LiteDB, dependency/public API/solution не менялись.
NOM1 writer/real Open/root publisher впереди; NOM2 полностью удаляет LiteDB.
Full suite/published/fresh packages/GPU/physical gates не проверялись, Aether failures
не закрыты. [Подробный scope/evidence](nom/HISTORY.md#nom0-2026-11-07).

<a id="nom-own-store-2026-11-07"></a>
## Принята собственная реализация Nom вместо LiteDB — 2026-11-07

Пользователь утвердил полное удаление LiteDB в пользу собственного transactional KV
каталога/append-only journal с immutable streamed CAS resources. Optional provider,
vendor fix/upgrade/process isolation и TrueMoon.Nom.LiteDb split отменены.
NOM0 format/recovery/budgets → NOM1 runnable core → NOM2 App/resource cutover и полное
удаление source/dependency/test/probe paths; последующие fault/compaction/pins/GC/
backup/performance/delivery этапы — только в [Nom PLAN](nom/PLAN.md#nom-own-queue).
[Архитектура/совместимость](nom/STORAGE_DESIGN.md#nom-own-store-2026-11-07).

Только документационные изменения; код ещё на LiteDB, package/solution не менялись,
новых build/test результатов нет. Прежние86safe cases/18groups/8fatal children
сохранены как история и не переносятся на новый backend. Legacy roots не сбрасываются
и не мигрируют автоматически. Engine f.3→f.4/importer g.1 не переупорядочены.

<a id="nom-faults-2026-11-07"></a>
## Nom AST1b.f: safe fixes и подтверждённый backend blocker — 2026-11-07

Исправлены sink/cleanup error preservation, retained write reservation и native
file/close ownership с явным retry на том же owner thread.86safe Nom.Tests каждый
Debug/Release и solution discovery, core18groups source/published прошли.
Solution nonincremental Debug0errors/1555existing warnings/delta0, new Nom warnings0.
Full suite/GPU не запускались, Aether failures не закрыты.

WAL fault acceptance **не прошла**:8/8 isolated children воспроизвели fatal finalizer
defect LiteDB5.0.21 после atomic reopen verification; все reaped, runner exit1.
Physical disk-full — OS permission gate New-VHD, attach/format не выполнялись.
AST1b.f/AST1 не закрыты; следующий приоритет — проверенный vendor fix/версия либо
process isolation, решение не принято. Dependency не менялась, engine очередь прежняя.
[Exact scope/команды](nom/FAULT_ACCEPTANCE.md#nom-faults-2026-11-07), [Nom queue](nom/PLAN.md#nom-fault-queue).

<a id="nom-app-2026-11-07"></a>
## Nom подключён через App — 2026-11-07

[AST2a](nom/APP_INTEGRATION.md#nom-app-2026-11-07) добавил public UseNom/options,
typed caller-disposable store factory и one owned host/shared lifecycle aliases.
DefaultApp startup rollback/RunAsync/stop/dispose и retained read/writer drain
проверены в57/57 Nom.Tests каждый D/R и solution-filter Debug. Core regression18groups
каждый source Debug/published Release. Solution nonincremental Debug0errors/
1555existing warnings/delta0; full suite не запускалась, Aether failures не закрыты.
Library по-прежнему non-packable: package/provider delivery — AST2b; disk/close fault,
GC/GPU/physical gates отдельно. Engine код/очередь f.3→f.4 не менялись.

<a id="nom-rename-2026-11-07"></a>
## Storage получил имя TrueMoon.Nom и проекты в solution — 2026-11-07

По решению пользователя модуль Assets переименован в TrueMoon.Nom. Core выделено в
отдельную internal/non-packable library, proofs — в TrueMoon.Nom.Probe с ProjectReference;
оба добавлены в TrueMoon.slnx. LiteDB5.0.21 включён в CPM, docs/nom — canonical docs,
прежние пути docs/assets оставлены pointers. Public App/provider/package — AST2.
Повторно18groups каждый source Debug/published Release,5commands0/3SDK binlogs/
15input hashes, separate module assembly и4actual kill/reap points/profile прошли.
Общий nonincremental solution Debug0errors/1555existing warnings/delta0. Full unit
suite не запускалась; Aether failures и physical/fault gates не закрыты.
[Подробности и команда](nom/NOM_RENAME.md#nom-rename-2026-11-07); очередь сохранена.

<a id="assets-ast1b-2026-11-07"></a>
## Assets AST1b: потоковые bytes и atomic publication — 2026-11-07

[Streaming core](nom/AST1B_STREAMING.md#ast1b-2026-11-07) реализован в research consumer
вне Aether: geometry/image/shader Streams,64KiB bounded I/O, CAS, atomic multi-resource
generation и operation journal.18groups каждый source Debug/published Release прошли,
4actual killed/reaped children/profile,5commands/3SDK binlogs/11source hashes/drift0.
Общий nonincremental solution Debug0errors/1555existing warnings/delta0. Full unit
suite не запускалась; Aether diagnostic failures не закрыты. Symlink fixture privilege-gated;
AST1b.f disk/write/cleanup faults, snapshots/GC, production App/package и GPU отдельно.
Детальная очередь остаётся в Assets; engine f.3→f.4 и g.1 не переупорядочены.

<a id="assets-ast1a-2026-11-07"></a>
## Assets AST1a: runnable LiteDB prototype проверен — 2026-11-07

[Standalone AST1a](nom/AST1A_PROTOTYPE.md#ast1a-2026-11-07) закрепил LiteDB5.0.21/
System.Buffers4.5.1 и actual DLL hash5.0.21.0, без Aether/production package graph edits.
Source Debug/published Release прошли по10groups: IDs/schema/collation/index,
exclusive root/Direct, owner-thread/rollback/reopen, serialized/query/queue limits и
startup-stop/factory drain.2profiles/5commands/3SDK binlogs, fresh archive bootstrap;
symlink fixture privilege-gated и не считается выполненной. Общий nonincremental
solution Debug0errors/1555existing warnings/delta0; full unit suite не запускалась,
старые diagnostics failures не закрыты. [Команды и ограничения](nom/HISTORY.md#ast1a-2026-11-07).
Далее AST1b CAS/journal/publication; App/package остаётся AST2. Engine f.3→f.4 не изменён.

<a id="litedb-assets-design-2026-11-07"></a>
## Assets: пользователь выбрал LiteDB — 2026-11-07

После comparison выбран LiteDB; [v1 design](nom/LITEDB_DESIGN.md#litedb-v1-2026-11-07)
фиксирует initial Direct/one-owner profile, catalog authority, immutable bytes,
staged generation publication, operation outcomes, snapshots/pins/offline GC и
numeric defaults. [Детальная очередь AST1a–d](nom/PLAN.md#litedb-ast1) теперь
LiteDB-specific; SQLite не текущий implementation target. Прежнее сравнение/история
сохранены. Следующий результат — pinned5.0.21 standalone source prototype; новые
production packages/API и runtime/build/tests/benchmark результаты не объявлены.
Engine остаётся consumer, его f.3→f.4 очередь не изменена.

<a id="asset-storage-planning-2026-11-07"></a>
## Самостоятельный модуль хранения ассетов: начало исследования — 2026-11-07

Пользователь предложил LiteDB и задал границу: подключаемый модуль вне движка,
выбор обсудить позднее. Добавлены [Assets PLAN/AST0–AST3](nom/PLAN.md),
[STATUS](nom/STATUS.md) и [сравнение](nom/RESEARCH.md) по official docs.
LiteDB/SQLite catalog, database BLOB/FileStorage и filesystem/manifests сравниваются
как варианты. SQLite+immutable content-addressed objects — предварительная гипотеза,
не утверждённый backend и не измеренный performance winner. FileStorage chunk
transactions и catalog↔external bytes consistency требуют явного publication/recovery.
Identity authority/portable records, process model, snapshots/pins/GC/cancel/commit и
App/IFactory ownership вынесены на обсуждение AST0. Детальные storage работы ведутся
в одном собственном плане; Aether g.1/2 и audio — consumers, f.3→f.4 не переупорядочены.
Документационное исследование: dependencies/C#/project files не менялись,
новых build/unit/native/database benchmark результатов нет.


<a id="ecs-commit-2026-11-07"></a>
## ECS и общая версия0.2.5.1-alpha — 2026-11-07

Перед коммитом оставшегося ECS/runtime/render-resource блока и сопутствующей ревизии
планов повторены targeted1230/1230 каждый Debug/Release, failed/skipped0. Новая
конфигурация Directory.Build.props:0.2.5.1-alpha, explicit Tiered JIT/PGO overrides сняты.
Solution nonincremental Debug build0errors/1555warnings,1422distinct warning catalogue
unchanged. [Точный scope и команды](engine/HISTORY.md#ecs-commit-2026-11-07).
Прежние native/package proofs имеют собственные даты и версии; full solution/Aether
tests и performance здесь не запускались. Diagnostics timeouts не объявлены исправленными.
SkiaSharp4.155.0 уже сохранён предыдущим коммитом84a4985; local .codex исключён.

<a id="skiasharp-41550-2026-11-07"></a>
## SkiaSharp/native4.155.0 — 2026-11-07

Уже выбранный managed4.155.0 был несовместим с bundled native154.0: официальный
version gate явно отказал exit2. Patched DLL пересобрана из exact NuGet source commit
SkiaSharp d25e7fa628a2576e50ecc42004cc4fd03c6657f5 / Skia c2d78902a56e24827a7273a4ed3461ce18780680
с неизменённым TrueMoon extension/ABI1/48bytes. SourcePins, DLL/build.json и инструкции
обновлены; пользовательский выбор версии в Directory.Packages.props сохранён.

Source Debug/published Release прошли ABI,18external readbacks/36GPU compositions/
raster/OpenGL/Vulkan comparison каждый, core+sync validation0/0 включая cleanup.
Alloy1101/1101, Hosting154/154 и Editor1295/1295 каждый D/R. Solution nonincremental
Debug0errors/1555warnings,1422distinct warning catalogue unchanged.
Fresh10local packages прошли36App+4direct
processes Debug/Release×build/publish, raster/OpenGL/Vulkan, native delivery/behavior/
cleanup и expected missing-analyzer failure. Runner добавляет unique binlogs и сверяет
exact direct graph4packages, включая Contracts для existing IUiSessionFactory; старое
ожидание3 исправлено без изменения production dependencies. Aggregate frozen proof
Completed=true; failed attempts сохраняют stale graph guard/changed README outcomes.
[Точные команды, SHA, scope и ограничения](alloy/HISTORY.md#skiasharp-41550-2026-11-07).
Scope audit:1598прежних файлов preserved/12bounded existing edits, новых versionable
files0; C#/production graph/user package selection preserved. Docs40Markdown/1947valid
links/no new failures,247old distinct issues,7STATUS archives verbatim; diff --check passed.

Новые C# API/extension changes, compiler/SDK install, driver/global settings и остановленный
K7 workaround search не назначены. Physical/K8/VRAM/final alpha и прежние reliability
failures не закрыты этим update. Engine очередь ARCH1f.3 не менялась.

<a id="render-resources-2026-11-07"></a>
## ARCH1f.2: resource owners, leases и preparation — 2026-11-07

Реализованы [App-owned resources](engine/RENDER_RESOURCES.md): counted CPU catalog/
scene/preparation leases, bounded exact-key device cache, generational handles, atomic
World rebind, cancellation/drain и retained failed cleanup. UseAetherRenderResources
предоставляет typed factories без двойного DI ownership; default Vulkan backend lazy.
Дополнительные count caps и actual native allocation budgets выбраны явно; scene/
wire/primitive limits, shaders, dependencies и прежний surface renderer сохранены.

Final targeted1230/1230 каждый Debug/Release,137newcases/73methods, failed/skipped0.
Source Debug/published Release/fresh isolated7pkg CPU и actual Vulkan consumers прошли:
3entities/6components/3assets,2fresh resource generations+zero-budget denial каждый,
upload/readback hashes, atomic reload/stale handles, final owners/counters0, core+sync
validation0errors/0warnings. Native denial происходит до AllocateMemory. Это не новый
frame/pixels/Editor Play proof. Solution nonincremental Debug0errors/1555warnings,
1422distinct warning catalogue unchanged. Все7package DLL/packaged README hashes verified.
[Команды, exact tests, failed attempts, ownership и ограничения](engine/HISTORY.md#render-resources-2026-11-07).

Scope audit сохраняет1571прежний файл;11bounded existing edits и28newfiles,25addressed
C# (14newproduction/9newtest+helper/1manual/1existing accessibility-only helper).
Docs37Markdown/1916valid local links, новых failures0,7STATUS archives verbatim;
247прежних distinct link issues сохранены отдельно. Git diff --check passed.
Projects/dependencies/protocol/shaders и local .codex не менялись. Full Aether/performance
в этой фазе не запускались; прежние diagnostics timeouts остаются открытыми.
Следующий шаг — ARCH1f.3 owned camera/mesh snapshots, затем f.4 surface/Playing fence.

<a id="render-cpu-2026-11-07"></a>
## ARCH1f.1: CPU mesh/material types и validation — 2026-11-07

Реализованы [CPU contracts](engine/RENDERING_CONTRACTS.md#cpu-v1): exact unmanaged
asset keys/hash, immutable definitions/frozen manifest, canonical codec и explicit
MeshRenderer/Camera schemas. Import numeric/reference/slot validation идёт до World,
default primitive/control formats сохранены. Resource cache/leases/GPU preparation
пока только следующий ARCH1f.2, новый renderer/extraction — f.3/4.

Current1093/1093 каждый Debug/Release,309newcases/94methods. Source Debug/published
Release/fresh isolated7pkg CPU consumer прошёл3entities/6components/3assets через
App/Cobalt factory и независимые literal/hash/tint/bounds/projection assertions.
Solution nonincremental Debug0errors/1555warnings, warning catalogue unchanged.
Scope audit сохранил1542прежних файла и oldC#/projects/dependencies/native/protocol;
7package DLL/README hashes verified. Docs35Markdown/1896valid links/no new failures,
7STATUS archives verbatim.
[Команды/границы/подробности](engine/HISTORY.md#render-cpu-2026-11-07).
Full Aether/native/GPU/performance не запускались; прежние diagnostics timeouts
не объявлены исправленными. Следующий шаг — f.2 owned resource lifecycle.

<a id="render-contracts-2026-11-06"></a>
## ARCH1f.0: контракты мешей, материалов и ресурсов — 2026-11-06

Выбран [opaque-unlit/1 contract](engine/RENDERING_CONTRACTS.md) до C# кода:
exact typed revisions/slots/bindings, renderer-wide linear tint, sRGB pipeline,
explicit perspective camera, strict CPU/GPU readiness, App/lease/submission owners
и bounded peak budgets. Первый content scope — builtin/typed in-memory pinned build.
Синхронный scene loader и прежние primitive/control limits сохранены.
[Сверка и детали](engine/HISTORY.md#render-contracts-2026-11-06).

Текущая работа документационная; новых build/test/native результатов нет. Прежний
e.4 общий regression с diagnostics timeouts не объявлен исправленным. Очередь
сдвинута к ARCH1f.1 CPU definitions/schemas/validation, затем f.2 resource preparation.

<a id="ecs-primitives-2026-11-06"></a>
## ECS Transform и primitive extraction — 2026-11-06

ARCH1e.4 добавляет Local/World Transform, iterative Parent hierarchy, parent-space
velocity и ordered movement/transform/extraction с detached snapshots; private
initial controller готовится до ActiveScene. Existing schemas/native renderer/legacy
64entity adapter сохранены; новый64drawable limit отделён от logical count.
[Контракт](engine/ECS_PRIMITIVES.md), [проверки и команды](engine/HISTORY.md#ecs-primitives-2026-11-06).

Current targeted751/751 D/R (750ECS+frame1), source/published/fresh7-package headless
133logical/2drawable/2generations и source/published native Vulkan geometry passed,
core+sync validation0/0/final owners0. Solution nonincremental Debug build exit0,
0errors/1555warnings. Первый full2537/2538 имел unchanged diagnostics delivery timeout,
focused same-binary replay2/2 passed. Final full2536/2538:2receiver Stop/Dispose timeouts,
focused replay25/25 passed; все750ECS passed в обоих full, но общий regression не green.
Причина не доказана; успешные replays/native proofs не считаются исправлением timeout.
Очередь — ARCH1f.0 mesh/material/resource contracts; checkpoint e.5 отдельно.

<a id="ecs-runtime-loading-2026-11-06"></a>
## Initial Runtime ECS scene loading — 2026-11-06

Следующий этап ARCH1e.3b добавляет capability/registry generation, bounded chunk
transfer полного payload и атомарную initial SceneWorld activation через явный ECS
game base. Parent App/SessionSupervisor и FrameSession предоставляют StartWorldAsync;
Game.Logic создаётся только в child, старые primitive/control/checkpoint limits сохранены.
Failed cleanup сохраняет World/game/ALC/artifact owners до retry.
[Контракт](engine/ECS_RUNTIME_LOADING.md), [подробные команды/проверки](engine/HISTORY.md#ecs-runtime-loading-2026-11-06).

Current ECS543/543 каждый Debug/Release, плюс обновлённый frame fixture1/1 D/R;
final full Aether Debug2331/2331, failed/skipped0. Source Debug/published Release/fresh isolated
NuGet Release profiles прошли286046byte/256entity scene,2generations каждый, CPU
pixels0→1tick, distinct World/registry IDs и подтверждённый cleanup. Это не новое
GPU/UI/Editor Play/preview или full solution test proof. Очередь — ARCH1e.4 Transform/
hierarchy/primitive extraction и независимый ARCH1f.0 resource design.
Final nonincremental solution Debug build: exit0,0errors/1555warnings. Аудит
сохранил1353прежних файла и7STATUS archives;31Markdown/1839valid local links,
новых link failures0. Project/dependency files не менялись.

<a id="ecs-loading-2026-11-06"></a>
## Registry и headless scene loading — 2026-11-06

ARCH1e.3a реализует immutable exact bindings, complete SceneDocument envelope codec,
scoped validation/import и private World materialization с tracked resource ownership.
[Контракт](engine/ECS_LOADING.md), [команды/проверки/ограничения](engine/HISTORY.md#ecs-loading-2026-11-06).
Current all-ECS409/409 каждый Debug/Release, full Aether Debug2197/2197 включая
все409ECS cases, failed/skipped0. Solution nonincremental Debug build0errors/
1555warnings; новых ECS warnings0. Это не full-solution test run.
Сохранены старые source/docs и wire budgets; e.3b actual Runtime load activation,
e.4 builtin/render extraction и e.5 checkpoint ещё нужны. Это не новое доказательство
готовности Editor Play/preview или общий release.

<a id="ecs-execution-2026-11-06"></a>
## Headless ECS execution — 2026-11-06

ARCH1e.2 реализует typed scoped queries, bounded preflight phase commands и
последовательные systems через App/IFactory, declared access/order и явные
cancel/fault/cleanup outcomes. [Архитектура](engine/ECS_EXECUTION.md), подробные
[проверки и команды](engine/HISTORY.md#ecs-execution-2026-11-06). Runtime/preview/
renderer/scene protocol не мигрировали; следующая engine задача — e.3 registry/load bridge.

Latest ECS208/208 каждый D/R, failed/skipped0; Aether Debug regression snapshot1994/1994
до2последних test-only cases, проверенных latest targeted runs на том же production
code. Solution nonincremental Debug build0errors/1555warnings. Это не full solution
test pass; GPU/package/latency acceptance не обновлены. Один cached one-entity workload
показал0bytes/10000ticks D/R, без общего performance обещания.

<a id="ecs-world-v1-2026-11-06"></a>
## Первый ECS storage срез — 2026-11-06

Запрос приступить к реализации перевёл ARCH1e от design к первому headless World:
generational handles, typed singleton value pools, finite options и App-local factory
с caller ownership. Выбран functional10000entity workload, не performance target.
Authoring/schema/wire/primitive consumers не мигрировали. Queries/commands/systems
следуют e.2, renderer/material resources — ARCH1f. Общая очередь и package/readme
отражают эти границы; прежние UI/Observer/reliability ветки сохранены.

Новый ECS57/57 каждый Debug/Release, full Aether Debug1845/1845, failed/skipped0.
Solution nonincremental Debug build0errors/1555warnings; это не full solution test run.
[Команды, ограничения и результаты](engine/HISTORY.md#ecs-world-v1-2026-11-06).

<a id="ecs-rendering-plan-2026-11-06"></a>
## ECS внесена в планы; системные компоненты отображения — 2026-11-06

Пользователь подтвердил включение обсуждённой ECS в планы и запросил renderer/
materials. ARCH1e теперь запланированное направление; новые ARCH1f.0–6 описывают
schemas/assets, resource preparation, extraction, первый Vulkan unlit consumer,
дальнейшие textures/lighting и checkpoint integration. Детали принадлежат engine
плану; Editor consumer — ED4i. Общая очередь/STATUS обновлены, прежние UI/performance/
reliability ветки и release criteria сохранены.

[RENDERING_DESIGN](engine/RENDERING_DESIGN.md) разделяет ECS data, DI systems/services,
shared MaterialAsset/instance overrides и backend GPU ownership. Документационная
работа не меняет code/API/packages/wire limits и не создаёт build/test/native result.
[Сверка источников и ограничения](engine/HISTORY.md#ecs-rendering-plan-2026-11-06).

<a id="ecs-design-2026-11-05"></a>
## Компоненты ECS: начало проектирования — 2026-11-05

После ревизии пользователь выбрал обсуждение системы компонентов. В engine
добавлены [ARCH1e](engine/PLAN.md#arch1e-ecs) и
[черновик](engine/ECS_DESIGN.md); текущие PLAN/STATUS отражают дизайн без объявления
реализации или миграции. Существующие authoring IDs/schema/override и bounded
runtime paths сохранены; прежний UI/performance/release backlog не отменён.
Новых code/build/test/package/native результатов нет.
[Подробности](engine/HISTORY.md#ecs-design-2026-11-05).


<a id="planning-revision-2026-11-05"></a>
## Общая ревизия планов — 2026-11-05

По запросу пользователя после db5ea97 сверены общий и шесть модульных roadmap,
Editor/preview/Transform и текущий Observer open-batch code. Код/зависимости/
версии не менялись; build/tests/performance/native/package не запускались.
Семь STATUS заменены текущими короткими сводками; полный прежний текст каждого
сохранён verbatim в соответствующем HISTORY под anchor
`status-before-planning-revision-2026-11-05`, с SHA256 и совместимыми STATUS anchors.

Устранены противоречащие «следующие шаги»: ARCH2, Window, первые Observer cache/
open/metadata, console/report больше не ставятся будущими prerequisites.
Основной предлагаемый маршрут — daily Editor: ED4g.3 group reset → ED4g.4 units →
ED5a camera/picking, затем translation tool. Независимые треки — Observer
ED6b.2e.4f CPU/allocations и reliability/release с current full-solution baseline
и ARCH2e.4b причинной диагностикой повторного post-reap drain timeout.
Новые срезы помечены предложениями, API/реализация не объявлены готовыми.

Коллизия ED4g исправлена: compact Transform сохраняет ID, будущий Game AI
inspector получает ED4h; действующие ссылки обновлены, старый anchor оставлен.
ED5 отмечает уже существующий прикладной preview/revision path, но не camera/tools.
Core/Cobalt/Enerit/Mithril получают собственные текущие очереди; старые даты/tests
не превращаются в новые результаты. R1–R6/K1–K9/PA1–PA10 и границы owners сохранены.
K7 workaround search остаётся остановленным; непроверенный input не назван
доказанным physical defect и не блокирует автоматически независимые features.

Документальные проверки и snapshots — ignored
`TestResults/PlanningRevision-20261105/{verification.json,status-archives.json,REPORT.md}`.
Проверены сохранность архивов, local links/anchors, уникальность Editor task IDs,
неизменность release/UI criteria, git diff --check и отсутствие production diff.
Прежние отсутствующие ignored evidence paths фиксируются отдельно; не восстанавливались.

## ED6a.2 raw streams и health — 2026-10-29

Core borrowed health/window APIs и Aether runtime/build memory snapshots подключены
к raw/health modes Editor. Metadata export содержит numeric counters без raw/error
text. Native finding потребовал INF3 dirty-frame deferral при сохранении output
guard. Final Core387/Aether1556/Editor823/Hosting117 Debug/Release и один native
SourceDebug/Cobalt case прошли; detailed failures/evidence и remaining acceptance
ведутся у [engine](engine/HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
Общий приоритет — compiler diagnostics/navigation, затем full ED8a report;
широкие package/published/physical gates остаются открыты.

## ED6a structured console и partial export — 2026-10-29

Общий приоритет уточнён после первого console consumer: ED6a.2 raw/health, затем
compiler navigation/full ED8a report и дальнейший authoring маршрут. Editor808/808
Debug/Release и один SourceDebug/standard/Cobalt native workflow проверены;
новый package/published/provider/flood acceptance открыт. Детальные задачи не
дублируются в общем плане. [Engine evidence](engine/HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).

## ARCH2e.4 file-worker contract — 2026-10-29

Подтверждённая pool dependency file-drain исправлена controlled RED/GREEN,
Core374/Aether1550 Debug/Release и fresh headless8/native24 proofs. Dedicated
file worker и sync signal сохраняют queue8/budgets/borrowing/deadlines. Historical
exact cause старого episode не объявляется установленным; scope/наблюдение
сохранены. [Core evidence](core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29),
[engine acceptance](engine/HISTORY.md#arch2e4-file-worker-liveness-contract--2026-10-29).
Следующий feature step — ED6a/ED8a, hardware/general release отдельно.

## ARCH2e.4: process streams и drain observation — 2026-10-29

Исправлена доказанная утечка accessed streams у Aether runtime/compiler,
Core capture expose read/file completion phases. Real-handle RED3→GREEN,
7directed cases и full Aether1550/Core373 Debug/Release прошли. Исправлен cold
connection setup одного existing deadline fixture без смены production IPC.
После6passing replays старого feed свежая упаковка повторила timeout с completed
readers/pending file sinks; root cause file-drain остаётся открытым. Связь с утечкой не доказана. ARCH2e.4 остаётся частично открытым;
дальше нужен исходный сбой с новой read/file/stop metadata, затем console/export.
[Подробная история/команды/evidence](engine/HISTORY.md#arch2e4-stdio-ownership-и-диагностика-drain--2026-10-29).
Готовность UI/аппаратных gates и full solution этим не меняются.

## ARCH2 native переход и архив сводки — 2026-10-29

ARCH2e.3 проверен в bounded Windows x64/net10 profile: native24/24 и fresh
package/headless8/8 после Hosting callback fix. Все подробности и evidence
ведутся у [engine](engine/HISTORY.md#arch2e3-native-editor-consumers-и-startup-cancellation--2026-10-29)
и [UI](alloy/HISTORY.md#arch2e3-native-consumers-и-lifetime-callback--2026-10-29).
Открыт ARCH2e.4 output-drain follow-up после единичного headless timeout; затем ED6a/ED8a console/export. Остальные quality/hardware/release gates прежние.
Full solution tests в этом шаге не запускались. Ниже сохранён предыдущий intro
общего STATUS без переноса его результатов в current run.

### Предыдущая сводка

Предыдущая работа — **2026-10-29, согласованные launch modes и план Runtime HUD**.
Editor/Player выбираются до App.Build; Editor Play запускает отдельный owned
Runtime, переключение живого Player в Editor пока не требуется. HUD принадлежит
Runtime и строится на Alloy/Argentis, final game + HUD frame экспортируется Editor
либо выводится Player. UI integration остаётся optional, Aether core без UI.
Детали — [ENG7b](engine/PLAN.md#eng7b-режимы-запуска-editor-и-player--2026-10-29),
[ENG5c.4](engine/PLAN.md#eng5c4-runtime-hud-поверх-alloyargentis--2026-10-29),
[ED6b](engine/EDITOR_PLAN.md#ed6b-buildplaystop-из-редактора--2026-10-29) и
[UI boundaries](alloy/PLAN.md#runtime-hud-aether--2026-10-29).
Это планирование; новых code/build/tests/GPU/package runs нет. ARCH2e.1/2 остаётся
ближайшим шагом. ED6b уточняет прежний project/workflow backlog, gameplay HUD
следует после ENG5c.3 и требует ENG7b.2 для standalone acceptance.

Предыдущая работа — **2026-10-28, обновление SkiaSharp/native4.154.0 по запросу пользователя**.
Новая extended DLL собрана из pinned source; Vulkan copy/return/redraw и GPU
composition прошли source Debug/published Release с core/sync validation0/0.
ABI/ownership сохраняются; build recipe и Raster probe исправлены. UI893/Hosting114/
Editor763 прошли отдельно Debug/Release,6TRX/3540passed/failed0/skipped0.
Nonincremental solution Debug build0errors/1541warnings; renderer nupkg payload/
dependency проверены. [Точный scope](alloy/HISTORY.md#skiasharp-41540-и-vulkan-interop--2026-10-28).
Это targeted native quality pass; independent NuGet ARCH2e, полный Editor GUI/
WSI/K7/physical/VRAM и full solution tests не закрыты. Далее ARCH2e.1/2.

Предыдущая работа — **2026-10-28, сверка рабочего diff и актуализация планов**.
ARCH2a–c уже входят в HEAD `af03ca3`. Текущие изменения продолжают ARCH2d:
тонкие Examples hosts/resources и перенос runtime; отдельно исправляют ENG2 CPU
frame race, ENG3 build/Replace fixtures и staging paths, Mithril ready-file reader
и ошибочное ожидание diagnostic forwarding Accepted.

ARCH2d реализован и проверен в source/published resource/headless scope.
Следующий доступный шаг — [ARCH2e.1/2 independent NuGet](engine/PLAN.md#arch2e-независимый-nuget-consumer--декомпозиция-2026-10-28).
GUI/native acceptance ARCH2d/e остаётся открытой; GPU runs отложены после BSOD,
причина и связь с проектом не установлены. Полный ARCH2 предшествует новым
ED6a/ED8a → ED4c.2/3 → project picker/switch и ED1/ED6 workflow.

В этой сверке прочитаны исходники, сохранённые TRX и verification artifacts;
новых build/tests/process/GPU/package runs нет. Последние module results:
Aether1543/1543 и Editor763/763 от28октября, Mithril111/111 от27октября,
каждый отдельно Debug/Release, failed/skipped0. Общий full test run после fixes
не повторён. Детальная связь diff с задачами — [engine STATUS](engine/STATUS.md),
накопленные сводки и прежние failures — [HISTORY](HISTORY.md#сверка-рабочего-дерева-и-архив-status--2026-10-28).

Документационный QA:20документов,707локальных ссылок/425якорей без ошибок;
53source fingerprints совпали,6сохранённых TRX сверены.
[Отчёт сверки](../TestResults/PlanningReview-20261028/verification.json).



## Сверка рабочего дерева и архив STATUS — 2026-10-28

Документационный QA:20документов/707локальных ссылок/425якорей, ошибок0;
53source fingerprints совпали,6сохранённых TRX подтверждены.
[Отчёт](../TestResults/PlanningReview-20261028/verification.json).
Новых build/tests/process/GPU/package runs нет.

Рабочее дерево сопоставлено с общим и модульными планами. Основной diff —
ARCH2d hosts/resources и quality follow-ups ENG2/ENG3/Mithril/diagnostic forwarding;
ARCH2a–c уже в HEAD af03ca3. Устранены устаревшие текущие указания начинать ARCH2c/d
и повторно закрывать уже исправленные fixtures. Очередь и открытая native acceptance
согласованы между общим, engine, Editor, Mithril и UI документами. Подробная
декомпозиция ARCH2e.1–3 находится только в engine PLAN. Новых build/tests/process/
GPU/package runs нет; сохранённые TRX и verification summaries сверены отдельно.
Прежние длинные записи общего STATUS перенесены ниже; их даты/исходные failures
не пересчитаны в новые passing results.

### Снимок прежней сводки

Ниже сохранено содержимое STATUS до сверки. Указания «следующий шаг»,
«текущий» и открытые failures относятся к датам соответствующих записей;
актуальная очередь и закрытия находятся в STATUS и PLAN. Это архив,
не новые результаты запуска. Относительные ссылки сохранены.

Последняя работа — **2026-10-28, ARCH2d hosts/resources**.
Ресурсный/headless срез завершён: тонкие Examples hosts и font/OFL без ManualTests,
fresh published closures вне workspace, managed metadata и18resource hashes,
16CLI checks, independent runtime DLL/apphost4/4 source Debug/published Release.
Editor763/763 Debug/Release, failed/skipped0;11source inputs/changes0.
Solution nonincremental Debug build0errors/1541warnings.
[Команды, evidence и границы](engine/HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28).
Окно/GPU/font rendering, NuGet consumer и общая native acceptance не проверялись;
BSOD/hardware/validation gates прежние. Следующий доступный шаг — ARCH2e.
Aether/full solution tests и прежняя21-scenario process matrix не повторены.

Предыдущая работа — **2026-10-28, diagnostic forwarding test fix**.
Неверное ожидание Accepted после Unconfirmed воспроизведено управляемым late ACK;
codec checks отделены от real-pipe outcome/drain tests. Runtime/budgets прежние.
Focused44/44 Debug; full Aether1543/1543 Debug/Release, failed/skipped0;
solution nonincremental Debug build0errors/1541warnings.
[Причина, RED→GREEN, команды и scope](engine/HISTORY.md#diagnostic-forwarding-bounded-outcomes--2026-10-28).
Test-quality follow-up закрыт; точная причина задержки старого Debug run неизвестна.
Editor/full solution tests, отдельные published/native matrices, NuGet/GPU/performance
не повторены. Следующий шаг — ARCH2d/e; BSOD/hardware gates прежние.

Предыдущая работа — **2026-10-27, исправление compiler MSB3030**.
DLL существовала; native MSBuild Copy не распознавал длинный staging path при
LongPathsEnabled0. Job-isolated single-project layout сокращает paths без смены
TEMP/OS settings. Deep-job regression RED1→GREEN; Full Aether1541/1541 Debug/Release,
failed/skipped0, в исходном TEMP. Compiler/checkpoint/Replace/App-stop headless8/8,
source changes0; solution nonincremental Debug build0errors/1541warnings.
[Точная причина, binlogs и scope](engine/HISTORY.md#eng3-compiler-staging-paths--2026-10-27).
Initial forwarding timeout остаётся отдельным follow-up. Editor/full solution tests,
NuGet/GPU/performance не повторены; ARCH2d/e и hardware/BSOD gates прежние.

Предыдущая работа — **2026-10-27, Build/Replace fixture migration и headless proof**.
IGameSessionEntry/Contracts migration подтверждена RED2→GREEN2; loader/identity
guards прежние. Headless21/21source Debug и21/21published Release, source changes0.
Full Aether1540/1540 Debug/Release при compact process-local TEMP, failed/skipped0.
Исходный redirected TEMP: Debug1534/1540 (6fail), Release1535/1540 (5fail) —
MSB3030 missing intermediate в5compiler cases и единичный Debug forwarding timeout;
они сохранены как отдельные follow-ups, исправления не заявлены.
Solution nonincremental Debug build0errors/1541warnings. Editor/full solution tests,
NuGet/GPU/performance не повторены. [Точные профили и evidence](engine/HISTORY.md#eng3-buildreplace-fixture-migration--2026-10-27).

Предыдущая работа — **2026-10-27, Mithril ready-file read race**.
Bounded missing/sharing/lock retry сохраняет общий8s deadline и identity/reap checks;
новый process regression подтвердил RED2 → GREEN. Полный Mithril111/111 Debug и
111/111 Release, failed/skipped0; fresh published Release composition14/14 на
Cobalt/DI иrestart20/20. Solution nonincremental Debug build0errors/1541warnings.
[Причина, команды и scope](mithril/HISTORY.md#ready-json-bounded-read-retry--2026-10-27).
Mithril runtime/API не менялись. Full solution/Aether/Editor tests, NuGet/GPU/
performance не повторены; следующий quality шаг — legacy Aether Build/Replace fixtures.

Предыдущая работа — **2026-10-27, исправление ENG2 CPU FrameReceiver race**.
Lazy buffer reservation сохраняет pending frame при empty/stale poll и EOF;
новый regression подтвердил RED(null) → GREEN. CPU transport/session/Frames
scope94/94 Debug и94/94 Release, failed/skipped0; solution nonincremental Debug
build0errors/1541warnings. [Причина и точная область проверок](engine/HISTORY.md#eng2-сохранение-pending-cpu-frame-при-пустом-poll--2026-10-27).
Full module/solution tests, process/package/GPU/performance не повторены;
Mithril ready-file и legacy Build/Replace fixtures остаются quality задачами.

Предыдущая сверка планов — **2026-10-27, документация и чтение исходников**.
Сверена прежняя очередь ARCH2d/e → ED6a/ED8a → ED4c.2/3 → project/workflow;
исправлены устаревшие статусы ARCH2c, Inspector и Reparent. Проверены17документов
планирования:579локальных ссылок и385якорей, отсутствующих целей нет. Сборка,
тесты, process/GPU/package runs в этой сверке не выполнялись; прежние failures открыты.

Незавершённая работа — **2026-10-27, ARCH2d hosts/resources, прервана BSOD Windows**.
Минимальный Editor host и standalone runtime находятся в Examples; host-owned font/OFL
больше не берутся из AlloyTest. Solution Debug/Release builds0errors/1541warnings,
новых завершённых regression/publish/native runs нет. Причина BSOD0xA и связь с проектом
исследуются; GPU проверки отложены. ARCH2d/e остаются открытыми. [Точный scope](engine/STATUS.md).

Предыдущая работа — **2026-10-27, ARCH2c explicit runtime launch/bootstrap/identity**.
Aether задаёт native command явно; общий bounded bootstrap проверяет actual entry
и точную пару host/runtime/shared binaries. AetherRuntimeHost не зависит от Editor/UI.
Current Aether1536/1536 Debug,1535/1536 Release (прежний CPU FrameReceiver race),
Editor763/763 Debug/Release, skipped0; новых189cases прошли в обеих конфигурациях.
Solution builds0errors, full solution tests не повторены. Source Debug/published
Release independent runtime DLL/apphost4/4; расширенные headless process probes42/46.
Legacy build/Replace fixtures failed: нет IGameSessionEntry либо Contracts reference;
они не переписаны, API guards не ослаблены. Общего green нет, повтор для green не делался.
GPU/native presentation/packages не повторены, прежний Mithril failure остаётся.
[Evidence](../TestResults/ARCH2c-20261027/verification.json),
[детали и ограничения](engine/HISTORY.md#arch2c-explicit-runtime-launch-bootstrap-и-identity--2026-10-27).
Далее ARCH2d hosts/resources, затем ARCH2e independent NuGet/native acceptance.

Предыдущая работа — **2026-10-27, ARCH2b Editor library/App integration**.
Editor стал packable library с UseAetherEditor/options/View factories и
стандартной оконной интеграцией; Main/CLI/runtime branch/smokes находятся в
ManualTests/AetherEditorHost. App/DI ownership и прежний Transform workflow сохранены.
Текущие Editor763/Aether1347 Debug/Release, failed/skipped0; 20новых cases.
Source/published functional native workflows прошли с8reaped children и zero owned
UI/GPU resources. Vulkan validation profile остановился на ErrorLayerNotPresent;
финальные GPU workflows без validation. Local Editor pack/metadata проверены;
independent NuGet proof и полный ARCH2e открыты. Solution Debug/Release builds0errors,
full solution tests не повторены; прежний Mithril Release failure не объявлен исправленным.
[API и ownership](../TrueMoon.Aether.Editor/PACKAGE.md),
[подробные проверки](engine/HISTORY.md#arch2b-editor-library-и-app-integration--2026-10-27).
Следующий шаг — ARCH2c explicit runtime launch/bootstrap/identity, затем hosts/assets
и independent NuGet consumer; console/export и authoring очередь идут после ARCH2.

Предыдущая работа — **2026-10-27, реализация ARCH2a scene persistence в Aether**.
Общие scene codec/store/results/conflict перенесены из Editor в Aether.Scenes;
UseAetherScenePersistence предоставляет headless App integration, Editor заимствует
его services и сохраняет свои document/project sessions и UI workflow.
[Границы и migration](engine/PACKAGE_BOUNDARIES.md),
[результаты](engine/HISTORY.md#arch2a-scene-persistence-в-aether--2026-10-27).
На дату ARCH2a Editor пока WinExe; следующими назначены ARCH2b–e, затем console/export
и authoring features. Это не завершение package/host перехода.

Aether1347/1347, Editor743/743 Debug/Release. Full Debug4296/4296, Release4295/4296:
один sharing IOException в Mithril composition/cobalt ReadyAsync на ready.json,
skipped0. Общего green нет; failure сохранён, повтор для green не делался.
Builds0errors/1546warnings в обеих конфигурациях, SDK10.0.403.
[Evidence](../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json).
Source changes0 по1024hashes,20test-binary hashes. Native/GPU/published/package/
performance acceptance не повторены; CPU FrameReceiver и ready-file quality
follow-ups остаются открытыми.

Предыдущая реализация и проверка — **2026-10-27, ENG8a.3/ENE6d.4 bounded child diagnostic delivery**.
Core предоставляет bounded history/import views; Aether App factories связывают
child с host через отдельный Enerit endpoint. Import сохраняет child origin/PID/
time/trace, назначает host sequence и authoritative session/build context.
Есть byte/count/rate/deadline limits, loss/unconfirmed/cleanup health; control
admission и local JSONL/raw capture независимы.
[Контракт/API](engine/DIAGNOSTICS_DELIVERY.md),
[результаты и история ошибок](engine/HISTORY.md#child-diagnostic-delivery--2026-10-27).

Новые179cases прошли в обеих конфигурациях. Full solution4285/4285 Debug и4285/4285
Release, failed/skipped0: Core373, Aether1338, Editor741, Enerit343/generator114.
Builds0errors: Debug nonincremental1546warnings, Release incremental1316warnings,
SDK10.0.403. [Evidence](../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source/binary hashes проверены, source changes0; пользовательский Microsoft DI10.0.14
сохранён.3actual Preview children/99control commands, log flood, slow/broken sinks,
bounded JSONL/raw и reap/release проверены. GPU/display/published/package/performance
acceptance не повторены. Неуспешные попытки сохранены; две проблемы изоляции/
cleanup deadline новых тестов исправлены в тестах.
Прежние CPU FrameReceiver/Mithril ready-file races не объявлены исправленными.

После ARCH2 следующий diagnostics шаг — ED6a/ED8a bounded Editor console/export, затем
command/frame aggregates. Bootstrap/App-private routing, generator и общий artifact
quota частичны. Authoring очередь и quality gates сохраняются.
Предыдущие [IPC outcome/wait](enerit/HISTORY.md#ene6d3-исходы-ipc-и-ожидание-admission--2026-10-27),
[resource metrics](enerit/HISTORY.md#ene6d2-метрики-ресурсов-и-перегрузки--2026-10-26),
[operations](core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26)
и [build retention](engine/HISTORY.md#диагностика-сборок-ограничение-хранения--2026-10-26)
сохраняют свои scope и budgets.

Предыдущая работа — **2026-10-25, ED4f.2 системный акцент границы окна**.
Windows cached accent/fallback подключён через общий host contract; Editor включает
рамку с inactive dimming160мс и приоритетом явного style/local цвета.
Related UI893/Hosting114/Editor741 Debug/Release:3496passed/0failed/0skipped,
15новыхcases; nonincremental Debug solution build0errors/1532warnings.
12native cases/206PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).
Evidence: TestResults/AccentBorder-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance не повторены; Windows settings
не менялись. Порядок прежний: ED4c.2 → ED4c.3 → project/runtime workflow.

Предыдущая работа — **2026-10-24, ED4e островки над viewport**.
Editor использует матовые панели поверх полной рабочей области сцены, group
opacity/PanelBackdrop в общей UI-библиотеке, retained context/fade без resize.
Related UI842/Hosting98/Editor699 Debug/Release:3278passed/0failed/0skipped,
67новыхcases; nonincremental Debug solution build0errors/1532warnings.
12native cases/158PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4e-островки-поверх-viewport--2026-10-24).
Evidence: TestResults/Islands-20261024/verification.json. Aether/full solution tests,
прочие модули/packages/physical/performance не повторены. Порядок engine работ и
межмодульные блокеры прежние: ED4c.2 names/order, ED4c.3 create/delete, workflow.

Предыдущая работа — **2026-10-24, ED4d.1 компактный контекстный Inspector**.
Иконки/Tooltip и скрытие колонки без выбора подключены к существующим UI controls;
engine authoring API не менялся. Related UI801/Hosting98/Editor673 Debug/Release:
3144passed/0failed/0skipped,25новыхcases; nonincremental Debug solution build0errors.
12native cases/152PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4d1-компактные-действия-и-контекст-инспектора--2026-10-24).
Evidence: TestResults/InspectorChrome-20261024/verification.json. Aether/full solution
tests/прочие модули не повторены; прежние quality/hardware gates открыты.
Порядок прежний: ED4c.2 names/order, ED4c.3 create/delete/duplicate, project/workflow.

Предыдущая работа — **2026-10-24, ED4d динамический Inspector и component authoring**.
Схемы компонентов определяют фактические редакторы свойств; Add/Remove/Restore,
reset/provenance, history/save/preview используют общий engine API. Runtime World
не является prerequisite; live runtime edit остаётся отдельным этапом.
Текущие Aether1142/UI801/Hosting98/Editor648 Debug/Release =2689/configuration,
5378всего,failed/skipped0;163новыхcases. Nonincremental Debug solution build:
0errors/1532warnings.12native cases/146PNG/16reaped children,validation0/0,
zero owned UI/GPU/mappings. Full solution tests/прочие модули/packages/physical/
performance не повторены; прежние CPU receiver/Mithril issues открыты.
[План](engine/EDITOR_PLAN.md#ed4d-динамический-инспектор-компонентов--начат-2026-10-24),
[проверки](engine/HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Evidence: TestResults/Inspector-20261024/verification.json. Далее ED4c.2 имена/
порядок соседей, ED4c.3 create/delete/duplicate, затем project/runtime workflow.
Новых базовых prerequisites нет.

Предыдущая работа — **2026-10-24, ED4c.1 обозреватель и PA4c TreeView**.
Первый срез дерева/иконок/выбора/переноса и общий Reparent API/history реализованы.
Текущие Aether1050/UI801/Hosting98/Editor577 Debug/Release =2526/configuration,
failed/skipped0;130новых cases. Nonincremental Debug solution build0errors/1532warnings.
10native source Debug/published Release cases,118PNG/14reaped children,
validation0/0 и zero owned UI/GPU/mappings. Full solution tests/прочие модули/
packages/physical/performance не повторены; прежние CPU receiver/Mithril issues открыты.
[План](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24),
[проверки](engine/HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24).
Evidence: TestResults/Hierarchy-20261024/verification.json. По запросу пользователя
приоритет — ED4c.2 сохраняемые имена/порядок, затем ED4c.3 create/delete/duplicate;
дальше project/runtime workflow. Базовые prerequisites не добавлены.

Предыдущая работа — **2026-10-24, ED4b строка состояния и PA4b StatusBar**.
Компактные status/icons/cancel, details и Edit Preview в footer; общая responsive
UI capability и typed View/Data/Style/App/Cobalt factories. Текущие связанные
UI759/Hosting98/Editor527 Debug/Release =1384/configuration,failed/skipped0.
Nonincremental Debug solution build0errors/1532warnings.6native source/published
empty/legacy/Transform cases,68PNG/12reaped children,validation0/0 и zero owned
UI/GPU/mappings. Full solution tests/Aether/headless не повторялись; прежние
CPU receiver/Mithril fixture failures остаются открытыми.
[План](engine/EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24),
[проверки](alloy/HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24).
Evidence: TestResults/StatusBar-20261024/verification.json. Далее minimal create/delete
общего API/history, затем UI и project/runtime workflow; порядок прежний.

Предыдущая работа — **2026-10-24, общий матовый backdrop и меню Editor**.
Общий UI material подключён к dropdown меню; theme tint/blur/fallback без новых
Editor services. Текущие UI739/Hosting98/Editor467 Debug/Release =1304/configuration,
failed/skipped0;4native source/published cases,28PNG/4reaped children,validation0/0
и zero owned UI/GPU/mappings. Full solution/Aether/headless в этом срезе не
повторялись. [Контракт](alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24),
[проверки/границы](alloy/HISTORY.md#pa8b-матовые-поверхности--2026-10-24).
Evidence: TestResults/Backdrop-20261024/verification.json. Порядок работ прежний:
minimal create/delete общего API/history, затем UI и project/runtime workflow.

Предыдущая работа — **2026-10-24, ED4a знак TrueMoon и анимированная кнопка меню**.
Знак и кнопка фиксированы слева; точки ⋮→⋯ поворачиваются вместе с раскрытием,
одно нажатие после анимации открывает «Файл», повторное закрывает. Отмена во время
раскрытия исключает late popup. Header сохраняет View/Style/App/Cobalt/IFactory.
Текущие UI723/Hosting98/Editor467 Debug/Release =1288/configuration,failed/skipped0;
4native source Debug/published Release cases вне repo,28PNG/4reaped children,
validation0/0 и zero UI/mapping/GPU ownership. Full solution/Aether/headless в этом
уточнении не повторялись; прежние CPU receiver/Mithril fixture failures открыты.
[План](engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки](engine/HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).
Evidence: TestResults/HeaderBrand-20261024/verification.json.

Предыдущая работа — **2026-10-23, ED4a компактный заголовок и меню Editor**.
Дизайн/план зафиксирован, Menu/Command/Overlay/frame transitions реализованы в UI;
Header View/Style через App/Cobalt/typed factory. Большие command rows скрыты,
⋯ раскрывает пять меню, title сдвигается, ▶/пауза/стоп и оконные actions неподвижны.
File/history/build/runtime/theme actions, shortcuts и unsaved exit policy сохранены.
[План](engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки и границы](engine/HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

105новыхcases; related UI712/Hosting98/Editor465 Debug/Release =1275/configuration,
failed/skipped0. Source Debug/published Release вне repo:10native/header cases,
64PNG/12reaped children, validation0/0 и UI sessions/mappings/GPU resources/handles0.
10headless scene/SDK cases отдельно. Evidence:
TestResults/MenuHeader-20261023/verification.json; source/binary fingerprints проверены.

На дату прежнего ED4a full Debug **3048/3049**: MithrilRestartProbe ready-file sharing IOException;
Release **3049/3049**,10TRX/configuration, skipped0. Nonincremental build
0errors/1532warnings. Ранний CPU FrameReceiver null и fixture race записаны в
engine/Mithril планы; исправление этих прежних путей данным UI срезом не заявлено.
Предыдущий full3049/3049 Debug/Release не отменяет последний Debug failure.
Fresh packages/physical/performance/golden/full alpha не повторены.

Текущий маршрут: ARCH2 package/host transition, затем ED6a/ED8a console/export;
далее ED4c.2 сохраняемые имена/порядок, затем ED4c.3 create/delete
общего authoring API/history и их UI commands; после — project picker/switch
и ED1/ED6 workflow. Общие engine/UI границы прежние; full
World/ECS и новый Core/Cobalt quality цикл не prerequisites. IPC — Enerit,
frames — отдельный transport, процессы — Mithril.

## ED4f.2: системный акцент границы окна — 2026-10-25

По согласованному24октября дизайну Editor включает platform accent/fallback для
рамки и inactive RGB60% с переходом160мс. Общие contracts/ownership — PA8b.4 UI;
прикладной consumer — ED4f.2. Текущие UI893/Hosting114/Editor741 Debug/Release:
3496passed/0failed/0skipped,15новыхcases. Последний nonincremental Debug solution
build0errors/1532warnings.12native cases/206PNG/16reaped children,validation0/0 и
zero owned UI/GPU/mappings. Evidence: TestResults/AccentBorder-20261024/verification.json;
[детали и ограничения](engine/HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).
OS settings не менялись. Aether/full solution tests/packages/physical/performance
не повторены; межмодульные блокеры и порядок ED4c.2 → ED4c.3 прежние.

## ED4f.1: прозрачный статус-бар — 2026-10-24

По согласованному продолжению ED4f статус-бар использует матовую hover/focus
подложку над полным viewport; important state и собственный popup удерживают фон.
Общая capability ведётся как PA8b.3, прикладной consumer — ED4f.1. Очередь и блокеры
прежние. Related UI886/Hosting106/Editor741 Debug/Release:3466passed/0failed/0skipped,
10новыхcases; последний nonincremental Debug solution build0errors/1532warnings.
12native cases/182PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
Evidence: TestResults/StatusOverlay-20261024/verification.json;
[детали и ограничения](engine/HISTORY.md#ed4f1-прозрачный-статус-бар--2026-10-24).
Aether/full solution tests/прочие модули/packages/physical/performance не повторены.

## ED4f: полная рабочая область редактора — 2026-10-24

По запросу пользователя Editor viewport расширен под title/status bar, постоянная
scene-path область заменена caption/Tooltip и contextual document prompts. Общий
WindowFrame/Hosting opt-in и OverlayHost safe insets ведутся в UI плане; прикладные
задачи — в ED4f. Порядок engine задач и межмодульные prerequisites не изменены.
Текущие related UI879/Hosting106/Editor738 Debug/Release:3446passed/0failed/0skipped,
84новыхcases; nonincremental Debug solution build0errors/1532warnings.12native
cases/166PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
Evidence: TestResults/OverlayChrome-20261024/verification.json;
[детали и границы](engine/HISTORY.md#ed4f-viewport-под-оконным-chrome--2026-10-24).
Aether/full solution tests/прочие модули/packages/physical/performance не повторены.

## ED4d: динамический Inspector и component authoring — 2026-10-24

По запросу пользователя уточнён приоритет после ED4c.1: schema-driven inspector
составляет редакторы по фактическим компонентам сущности. Общий ComponentEditBatch
использует прежние scene revision/receipts/resolver/history/save/preview; runtime
World не добавляет предварительный этап. Authoring-срез реализован и проверен:
163новыхcases,4related suites Debug/Release5378passed,12native cases/146PNG/
16reaped children. Full solution test green не заявлен, старые quality issues открыты.
Детали/commands/границы — [engine HISTORY](engine/HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Далее ED4c.2 persistent names/order, ED4c.3 create/delete/duplicate, project/workflow.

## ED4a: компактный заголовок и меню — 2026-10-23

По запросу пользователя дизайн/план и приоритет ED4a зафиксированы и реализованы.
Общие menu/command/overlay/transition contracts остаются в UI плане; Editor
подключает scoped Header View/Style через App/Cobalt/typed factory. После этого
среза следующий прикладной шаг снова minimal create/delete authoring API/history.

105 новых cases, related UI712/Hosting98/Editor465 Debug/Release. Native source
Debug/published Release:10header/application profiles,64PNG,12reaped children,
validation0/0 и zero UI/mapping/GPU ownership.10headless cases отдельно.
Последний full Debug3048/3049: MithrilRestartProbe ready-file IOException;
последний Release3049/3049, skipped0. Ранний FrameReceiver null и этот fixture
race внесены в engine/Mithril планы; runtime этих модулей не менялся. Предыдущий
full3049/3049 Debug/Release не отменяет последний Debug failure.
[Детали и exact evidence](engine/HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

## Сверка планов и ED3a Transform/history/preview — 2026-10-23

Согласован порядок: первый редактируемый Open/Transform/Preview/Undo/Save/reopen
цикл → минимальные create/delete общего authoring API/history → project picker/
switch и ED1/ED6 workflow. Устаревшие «следующие шаги» базовых модулей исправлены;
исторические QUALITY/ENE6 записи отделены от текущего scope. Новый Core/Cobalt
quality цикл не prerequisite; ENE6c/d и оставшийся MIT6a/c остаются отдельными.

ED3a реализовал первый цикл через существующий engine API, nested View/Data/Style
и App/Cobalt/IFactory. Atomic drafts/history, validation/conflicts, bounded preview
updates и child cleanup проверены. [Детали, requirement matrix и exact evidence](engine/HISTORY.md#ed3a-transformhistorypreview-в-ui--2026-10-23).
66новых cases; final full2944/2944 Debug и Release,10TRX/configuration,failed/skipped0;
nonincremental build0errors/1531warnings. Source Debug/published Release: текущие
10native cases/24PNG/12reaped children и10headless scene/SDK cases; validation0/0,
UI sessions/mappings/GPU resources/handles0. Source/binary fingerprints проверены.
Fresh packages и physical/loss/VRAM scope не повторялись. Evidence —
TestResults/TransformUi-20261023/verification.json.

## ED2b: документы в UI Editor — 2026-10-23

App/Cobalt/IFactory подключают New/Open/Save/SaveAs/Close и document-aware native
Exit. Inline Save/Discard/Cancel, owner-thread ViewModel/typed View/Style,
dirty/errors/path drafts, actual IO drain/forced upgrade и callback-failure retry
реализованы. Raw authoring properties читаются без legacy64 projection; inspector
ещё read-only, preview не подключён к normal UI. Project picker/runtime switch
остаются ED1/ED4/ED6. [Контракт и точные tests](engine/HISTORY.md#ed2b-документы-в-обычном-ui--2026-10-23).

74новых cases; full2878/2878 Debug и Release по10TRX/failed0/skipped0,
Editor358/Aether1008, UI661/Hosting85. Nonincremental build0errors/1531warnings.
Source/published app вне repo: documents по19UI actions/4PNG/5saved files,
Save/reopen/conflict/recovery/failed Exit Save/Cancel/Discard. Вся native matrix
8cases/18PNG/4reaped children, validation0/0 и zero UI/GPU owners;10headless cases
отдельно,19consumer commands всего с6ожидаемыми negative exits. Source242/Editor45/
binary8 fingerprints и PNG проверяются; TestResults/DocumentsUi-20261023/verification.json.
Новых engine/wire/UI-library APIs/dependencies нет; fresh package, physical/
performance/golden scopes не повторялись. Далее Transform inspector/Undo/Redo
и independent preview; ED2 single-project document profile завершён, весь ED1–ED8 нет.

## ED2a: document/persistence API Editor — 2026-10-23

Versioned .aetherscene codec/store и owned document/authoring session подключены
через App/Cobalt/IFactory. Atomic Load/Save/SaveAs, dirty по ContentRevision,
captured save, Undo checkpoint, SHA256 conflict guard и document Save/Discard/Cancel
API реализованы. Project dirty/busy guard и cleanup retry сохраняют ownership;
общая UI Close/Switch/Exit транзакция остаётся открытой. Новых engine/wire/UI-library
dependencies нет. Детали и exact requirement matrix —
[в истории Aether](engine/HISTORY.md#ed2a-документ-сцены-и-persistence--2026-10-23).

143новых cases, full2804/2804 Debug и Release по10TRX/failed0/skipped0;
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor284. Nonincremental build0errors/1531warnings.
Source Debug/published Release вне repo:10headless и6native cases/10PNG/4reaped
children, Vulkan0/0 и zero owned UI/GPU resources.17commands включая6ожидаемых
negative exits.229solution source/39Editor source/8binary hashes проверены;
TestResults/Documents-20261023/verification.json. Published scene check read-only,
Save/reopen/atomic failure — direct filesystem tests. Fresh packages и аппаратные/
performance/golden gates не повторялись. Далее document UI commands/close policy,
затем Transform/Undo/preview; первые4ED2 пункта закрыты, остальные ED1–ED8 открыты.

## ED1a: проект и сессия Editor — 2026-10-23

UseAetherEditorProjects предоставляет App/Cobalt/IFactory project/session layer,
strict versioned .aetherproj и selected SDK preflight без окна/engine/build.
Failed/cancelled Open сохраняет active, Close fences/drains; ownership и Microsoft
DI проверены. Normal launch больше не ищет checkout/demo; .aetherproj/--check-project
работают по explicit path, legacy csproj/game workflow сохраняется. Детали:
[контракт ED1a](engine/EDITOR_PLAN.md#ed1a-первый-projectsession-profile--2026-10-23),
[requirement/tests/commands/evidence](engine/HISTORY.md#ed1a-projectsession-и-запуск-вне-repository--2026-10-23).

Final full2661/2661 Debug и2661/2661 Release,10TRX/configuration,failed/skipped0,
Aether1008/Editor141;92новых cases. Nonincremental Debug0errors/1531warnings.
216source hashes; final source/published6native cases/10PNG/4reaped children,
4headless SDK/invalid-global.json cases,11commands/32Editor source/8binary hashes.
Published app/project/CWD вне repository без demo DLL. Workspace profiles не
стартуют compiler/child; legacy workflow повторён с Vulkan validation0/0.
Во всехnative случаях project/UI/GPU ownership освобождён.
Evidence TestResults/Projects-20261023/verification.json; initial passes до
последней UI placeholder correction сохранены отдельно.

Engine/runtime/wire/UI-library APIs/dependencies не менялись; packages/golden/
performance/hardware acceptance не повторялись. ED1 имеет3completed metadata/
startup tasks, общая document/runtime switch transaction остаётся открытой.
Следом ED2 scene format/document/load/save/reopen, затем authoring/preview UI.
Новых общих Core/Cobalt prerequisites нет; publication/commit не выполнялись.

## Независимый Edit preview Aether — 2026-10-23

После ARCH1b.1 реализован первый GPU Edit preview: resolved document/revision
передаётся через существующий Enerit control built-in paused render child,
Mithril сохраняет process ownership. Build/Play/Game.Logic не требуются; App/
Cobalt/IFactory управляют созданием и lifetime. Preview готов только после actual
presented ticket; replacement, resize/suspend/resume, cancel/Stop/cleanup retry
проверены. Engine profile не подключён к ordinary Editor UI; следом ED1a project/
session, документы/save/reopen и UI integration. Детали/requirement map:
[engine HISTORY](engine/HISTORY.md#edit-preview-независимый-engine-profile--2026-10-23).

Фактически выполнены nonincremental Debug build0errors/1542warnings и full solution
**2569/2569 Debug и2569/2569 Release**,10TRX/configuration, failed/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor49.121новыйcase, final focused134/134 с13existing GPU pipe cases.
Native preview Debug/published Release: по6PNG/5reaped children; published вне repo
без Game.Logic.dll. Старый Editor workflow Debug/published Release: по8commands/
3PNG/2reaped children. Все4cases validation0/0 и zero owned resources.
Normal peer disconnect defect GPU producer воспроизведён native и исправлен
точно на wire read/write; partial messages и arbitrary IO failures остаются faults.

Fresh package-only consumers8/8 (Debug/Release × build/publish × Cobalt/MS DI):
9nupkgs/22commands, isolated version0.2.2.7-alpha.preview.20261023110622747.
Это managed API/ownership proof; native drawing доказан отдельно.188solution/
140native/301package source hashes, retained failures/exact commands и итоговый
verification.json — в TestResults/EditPreview-20261023. Новых межмодульных
dependencies/assembly/protocol versions нет; пакеты не публиковались.
ED1–ED8/полный authoring/World/edit/MCP и hardware/final UI scope не закрыты.

## ARCH1b.1: локальное authoring API Aether — 2026-10-23

По запросу «давай следующий шаг» реализован минимальный общий scene query/
Transform/history API без UI и Runtime. UseAetherAuthoring/IFactory предоставляют
caller-owned sessions, shared resolver и finite limits; atomic batches, revision/
operation identity, cancel/error и Undo/Redo используют одного owner. Новых
assembly/межмодульных dependencies нет. Детали и requirement/test map ведутся
у [Aether](engine/HISTORY.md#arch1b1-headless-transform-authoring-и-история--2026-10-23).

Фактически выполнен nonincremental Debug build:0errors/1522warnings. Full solution
**2448/2448 Debug и2448/2448 Release**,10TRX/configuration, failed0/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether887/Editor49.87новых headless cases, strict focused run87/87. Evidence:
TestResults/Authoring-20261023/solution/summary.json; текущие Aether production/test
source hashes139зафиксированы и проверены после runs. Fresh package proof8/8:
Debug/Release × build/publish × Cobalt/Microsoft DI,9nupkgs/22commands,299producer
source hashes, isolated local feed/cache. Package final Completed=true:
TestResults/Authoring-20261023/package-20261023-102244573/summary.json.
Markdown/namespace/diff и aggregate evidence — TestResults/Authoring-20261023/verification.json.
Native viewport, physical UI и performance
не повторялись. Первый ENE6a/MIT6a source/published44/package132 остаётся предыдущим
профилем, не новым результатом этого изменения.

Следующий engine шаг — независимый Edit preview через Enerit; ED1a project/session
параллельно. ED1/ED2 storage и прикладной Transform/preview/save/reopen срез ещё
нужны; full ARCH1b, runtime World/edit/MCP и прежние hardware ограничения открыты.

## Первый ENE6a/MIT6a implementation slice — 2026-10-23

По запросу приступить к следующему шагу реализованы finite pipes admission/
options через DI/IFactory и observable Stop/Completion. При cancellation сохраняется
ownership до фактического writer/handler/decode drain; process Stop использует
отдельный Mithril lifecycle pipe, не насыщенный domain RPC. Mithril/Core/Cobalt
runtime API не менялись, wire/generator mapping/same-build scope сохранены.
Queued-writer CTS lifetime defect подтверждён детерминированным RED/GREEN и
исправлен capture токена значением. Ready-file sharing violation в fixture
обрабатывается bounded retry только Win32 errors32/33; JSON/IPC не повторяются.

Окончательный final3 full solution2361/2361 Debug/Release,10TRX/configuration,
failed0/skipped0;40новых cases. Nonincremental Debug build0errors/1522warnings.
Source/published44/44 и fresh independent package132/132,8nupkgs/152commands,
expected omitted Enerit analyzer CS0234, Completed=true. Предыдущие failed/pre-fix
runs сохранены и не считаются passing финального кода. [Все команды, tests и границы](enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23),
[process acceptance](mithril/HISTORY.md#mit6a-saturated-ipc-и-независимый-process-stop--2026-10-23).

Native Editor viewport, performance, physical UI/hardware, полный fault matrix
и long resource trends не повторены/не закрыты. Первый bounded prerequisite
готов; локальный ARCH1b/headless и ED1a остаются следующим engine/application шагом.

## Сверка основы и улучшения IPC/процессов — 2026-10-23

По запросу пользователя проверены общие и Core/Cobalt/Enerit/Mithril/Aether планы/
статусы, quality matrix и ключевые transport/process integration paths. Чтение
исходников подтвердило unbounded pending map и scheduler queues Enerit,50s default
timeout, local-only request cancellation и synchronous handler drain в Dispose.
Это свойства текущего кода, не воспроизведённые новые leaks/deadlocks. Aether уже
передаёт command deadlines, ограничивает control payload64KiB и имеет Hello/
heartbeat/session fencing; Mithril уже имеет bounded restart, stop fence и kill/
reap owned process. Parent observer запрашивает App cancel; abrupt parent death
с uncooperative child и длительные resource trends отдельного proof не имеют.
Aether NativeRuntimeLease читает stdout/stderr через ReadToEndAsync: pipe дренируется,
но retained text до завершения процесса не ограничен.

Вывод: локальный ARCH1b/headless и ED1a могут продолжаться без нового полного
Core/Cobalt quality прохода. До частого Runtime preview предложены ENE6a/b и MIT6a:
точечный contract/admission/resource bounds и совместный stop/drain/kill/reap
fault/load proof. ENE6c/d — fail-fast совместимость и IPC diagnostics; MIT6b/c —
наблюдаемость по потребителю, bounded logs, parent-death/resource acceptance.
Новые транспорты/OS, расширенный restart и оптимизации после CPU/allocation profile
не блокируют первый срез. Публичные API и новые гарантии ещё не утверждены.
Единый детальный межмодульный acceptance ведётся у Mithril; [порядок и ссылки](PLAN.md#сверка-основы-перед-aether--2026-10-23).

Исправлены устаревшие current next steps Core/Cobalt/Enerit и открытый DI статус
в quality matrix; исторические даты/counts сохранены. Изменена документация,
production code/API не менялись, build/tests/native/packages/performance не
запускались. Проверка links/added anchors/whitespace сохраняется в
TestResults/FoundationReview-20261023/verification.json; её scope — документация.
Итог:21documents/782local links/38added anchors, missing targets/anchors0;
ED1–ED8/40unchecked tasks сохранены, source changes0, git diff --check прошёл.

## Исправление структуры проектов — 2026-10-21

По запросу пользователя исправлены замечания [исходного аудита](#аудит-размещения-типов-и-namespace--2026-10-21).
Все 44 проекта охвачены изменениями или проверкой; после разделения Control.cs
получилось 597 авторских C#-файлов вместо 590. Структурная проверка после переносов:
namespace mismatch0, global-type violations0, технические папки0, parse errors0,
orphaned sources0. Top-level statements сохранены, явно объявленные типы из
таких программ получили namespace своего проекта.

### Размещение и изменения public API

| Прежнее размещение / namespace | Текущее размещение / namespace |
| --- | --- |
| Aether.Contracts: IGameSession, SessionIdentity | Aether/Sessions, `TrueMoon.Aether.Sessions` |
| Aether.Contracts: SceneEntity, SceneSnapshot | Aether/Scenes, `TrueMoon.Aether.Scenes` |
| Aether.Contracts: IEngineControl, CommandKind, ControlRequest, ControlReply | Aether/Transport, `TrueMoon.Aether.Transport` |
| Aether.Contracts: frame types/codec/pattern | Aether/Frames/Frames.cs, `TrueMoon.Aether.Frames` |
| Aether.Contracts: checkpoint types/capability/outcomes/codec | Aether/Checkpoints, `TrueMoon.Aether.Checkpoints` |
| Alloy/Runtime с namespace TrueMoon.Alloy | `TrueMoon.Alloy.Runtime`, по реальной папке |
| Contracts/Exceptions/AppCreationException, TrueMoon.Exceptions | Корень Contracts, `TrueMoon.AppCreationException` |
| Contracts/Configuration/UnitsConfigurationExtensions | Корень Mithril, `TrueMoon.Mithril.UnitsConfigurationExtensions` |
| Enerit.Generator/Extensions и Utils | Корень generator, `TrueMoon.Enerit.Generator` |
| MithrilTest/Models/TestPoco.cs | Корень примера, `MithrilTest` |

IGameSession и остальные engine contracts остаются в одной Aether assembly.
Общая TrueMoon.Contracts сохраняет межмодульные contracts/base models/interfaces
без зависимости на реализации модулей. Units helper относится к module-specific
units configuration и теперь поставляется Mithril, который уже использует общий
IConfiguration. Его аргументы `-u`/`-p` и поведение не изменены.

У Mithril.Tests RootNamespace исправлен на TrueMoon.Mithril.Tests. У shared
CorePackageConsumer/CoreHeadlessProbe явно задан Headless.Consumer, у DirectCobalt —
Direct.Consumer, у Enerit benchmark — EneritBaseline. Эти значения сохраняют
намеренные shared consumer namespaces и имена исполняемых сборок, согласуя их
с правилом RootNamespace. 17файлов с global types получили namespace; ссылки
Cobalt/Enerit generator и примеры обновлены вместе с ними.

Изменены namespace публичных alpha API и assembly units helper: внешним
consumers нужны соответствующие using/qualified-name changes и пересборка.
Совместимые копии API в старых технических папках не оставлены. Обновлены C#
raw-source fixtures, package template, текущие README/PACKAGE и source links;
исторические записи ниже сохраняют прежнее состояние, их ссылки ведут к новым
исходникам. Логика production runtime, wire layout и версии протоколов не менялись;
отдельно исправлена обнаруженная во время проверки ошибка sample GPU binding.
Сравнение 26 650 C#-литералов до/после (с нормализацией перенесённых namespace
в C# samples) не выявило неожиданных изменений, включая raw strings.

### Выполненные managed проверки

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --logger trx --results-directory TestResults/StructureFix/tests-Debug-final2 --verbosity quiet
dotnet build TrueMoon.slnx --configuration Release --no-incremental -m --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --no-build --no-restore --logger trx --results-directory TestResults/StructureFix/tests-Release-final2 --verbosity quiet
```

Каждая сборка: errors0/warnings1499. Каждая test configuration: **Passed: 1898,
Failed: 0, Skipped: 0**,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI615/Hosting79/Aether493. RichTextEditorTest, находящийся вне solution,
отдельно собран в Release; source CoreHeadlessProbe отдельно собран в Debug и
Release с `--no-dependencies`, используя уже проверенные project references.
Первые промежуточные builds выявили недостающие Runtime
imports из дочерних Alloy namespaces, затем29duplicate usings; они исправлены
перед окончательными managed runs. Новых product test cases не добавлено.

### Consumer проверки

Последовательный wrapper `TestResults/StructureFix/ValidateConsumers.ps1` сохраняет
точные команды/exit codes в consumer-summary.json и отдельные stdout/stderr logs.
Core/units/UI package runners используют свежие версии, отдельные feeds/caches и
проверяют generated adapters/consumer graphs; публикация пакетов не выполнялась.

- `ManualTests/CorePackageConsumer/RunPackageSmoke.ps1`:80cases и3package README
  examples, выход0; `TestResults/CorePackages/structure-20261021/summary.json`.
- `ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1`:108cases, выход0;
  `TestResults/UnitsPackages/structure-20261021/summary.json`.
- `ManualTests/AlloyFactoryConsumer/RunIntegration.ps1`, profiles Source/Package:
  16source и8package native processes на Cobalt/DI × OpenGL/Vulkan × Debug/published
  Release; собственные runner/source/artifact checks прошли. Отчёты —
  `TestResults/AlloyInfrastructure/structure-{source,package}-20261021/summary.json`.
- `ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1`:36cases и4direct
  consumer cases; `TestResults/AlloyPackages/structure-20261021/summary.json`.
- `TestResults/StructureFix/ValidateAetherPackage.ps1`:12fresh packages и4App
  consumers (Cobalt/DI × Debug/published Release), managed GPU API/ownership и
  Skia native delivery. Независимая сверка payload hashes/nuspec/assets прошла;
  `aether-package/summary.json` и `aether-package-verified.json`.

### Найденная ошибка native composition и промежуточные runs

Первый run `TestResults/EngineSessions/structure-20261021` остановился после
14passing cases: Release GPU Editor прошёл20Replace, затем assertion ошибки
candidate/rollback завершил run. Само исходное исключение до assertion не было
записано. RecordFailureAsync теперь сохраняет ошибку, RecoveryError, flags и
editor-error identity до проверки; условия assertions не ослаблены. Два отдельных
Release GPU повтора в `TestResults/StructureFix/native-diagnostic` прошли20Replace/
20failure cases/53reaped children каждый; это не заменило общий повтор.

Второй общий run `TestResults/EngineSessions/structure-final-20261021` подтвердил
конкретную причину: `Single(l => l.ProcessId == runtime.ProcessId)` искал lease
среди всей истории запусков, а Windows повторно выдал PID58800. Две записи
дали `Sequence contains more than one matching element` при подключении GPU.
Первый failed report также содержит повторный PID58404, но его исходный
RecoveryError не был сохранён, поэтому точную причину первого assertion отдельно
не утверждаем. Оба failed runs сохранены с исходными logs и cleanup reports.

В EditorViewportProbe fault decorator теперь хранит и возвращает конкретный
NativeRuntimeLease, а ViewportProbe передаёт текущий native lease напрямую.
История/PID не используются для выбора процесса; владение и borrowed handle
остались прежними. Это устраняет подтверждённый механизм PID-reuse отказа.
После исправления повторены full solution Debug/Release builds/tests, указанные
выше. Package consumers от этого sample-only изменения не зависят и не повторялись.

### Итоговый native run и окончательная сверка

После исправления PID lookup выполнен `TestResults/StructureFix/ValidateNativeVerified.ps1`:
`ManualTests/EngineSessionProbe/RunProbe.ps1` с Scenarios=build-artifacts/checkpoint/
replacement, IncludeViewport/IncludeGpuCapabilities/IncludeGpuImport/IncludeGpuSurface/
IncludeGpuViewport/IncludeEditorGpu, Debug и published Release. Validation layer
из прежнего локального TestResults/ENG4b передан только принадлежащему runner
process tree. Никакие system/driver settings не менялись.

`TestResults/EngineSessions/structure-verified-20261021/summary.json`: Completed=true,
**20cases/22commands**,413source/76binary fingerprints. CPU и GPU Editor в каждой
конфигурации прошли20Replace/20failure cases/53reaped children/MaxLive1. UI sessions,
CPU mappings, parent GPU resources/handles0; GPU imported readbacks0. Исходный
native scope54cases ENG4d2 целиком не повторялся: этот targeted run проверяет
затронутые game artifacts/checkpoint/replace и CPU/GPU consumers.

Финальный Roslyn audit повторён после всех C# changes:44projects/88evaluations/
597files, namespace/category/parse/orphan violations0. Повторная literal review:
26 650 значений до/после без неожиданных различий. Project/source fingerprints,
TRX, native/editor reports, consumer summaries, Markdown links/anchors и diff
проверяются независимо в `TestResults/StructureFix/VerifyFinal.ps1`; результат
хранится в completion.json. Независимая сверка прошла:597source и44project hashes,
413native source/76binary hashes,18TRX,587локальных ссылок/249anchors;
`git diff --check` без ошибок. Failed промежуточные runs не переименованы в passing.
Аппаратные UI K3/K7/K8/VRAM/final alpha ограничения сохраняются; drivers/Windows
settings не менялись, поиск K7 workaround не возобновлялся. Функциональный
следующий шаг остаётся ENG4e — сопоставимый measured CPU/GPU baseline до 3D.

## Аудит размещения типов и namespace — 2026-10-21

Это исходный снимок до [исправлений](#исправление-структуры-проектов--2026-10-21).
Counts и замечания ниже сохраняют результат первоначальной проверки.

По запросу пользователя проверены все **44 C# проекта** репозитория, в том числе
7 проектов вне TrueMoon.slnx (в solution 37). Проверены 590 авторских C#-файлов,
включая тесты, генераторы, benchmarks и ManualTests. TrueMoon.Magnesium не содержит
авторских исходников/проекта и отдельной проверенной сборкой не считается.

Метод: список файлов git, 88 вычислений MSBuild properties/Compile items для
Debug/Release, затем Roslyn C# syntax trees с соответствующими defines.
Вызовы `dotnet msbuild` использовали `-getProperty` и `-getItem` без build targets.
RootNamespace и Compile membership одинаковы в обеих конфигурациях. Все 590 файлов
принадлежат проектам и включены в Compile; ошибок разбора 0. Это синтаксический
аудит, не сборка продукта. 24 linked inclusions в 3 проектах проверены по реальному проекту-владельцу файла;
виртуальный Link путь не считается его новым namespace. Raw strings с C# samples
в generator tests не считаются объявлениями, generated bin/obj sources исключены.
Top-level statements допустимы; явно объявленные global types учитываются отдельно.
Инструмент проверки собран через SDK Roslyn assemblies без
внешних PackageReference. Product build/test/native/pack в этом аудите не запускались.

Локальные evidence: `TestResults/StructureAudit/{evaluations.json,sources.json,audit.json}`,
Collect.ps1, tool/Program.cs, WriteReport.ps1. audit.json хранит все файлы/types/
namespace, source/project SHA256, baseline commit и ручную классификацию папок.
Эти ignored evidence не входят в поставку. Проверка от baseline784996a,
при существующих документационных изменениях правила; production sources не менялись.

### Назначение TrueMoon.Contracts

Общий пакет контрактов, базовых моделей и интерфейсов для взаимодействия модулей,
включая App/service/configuration/diagnostics API. Это не каталог всех интерфейсов
TrueMoon: engine/game/frame, UI и IPC-specific contracts остаются у своих модулей.
34 исходных файла соответствуют RootNamespace=`TrueMoon` и папкам; отличающееся имя
пакета TrueMoon.Contracts не является namespace нарушением. ProjectReference0,
PackageReference 0; папки Configuration/Diagnostics/Services/Dependencies имеют
функциональное назначение. ConfigurableBase и небольшие helpers не считаются
нарушением только из-за наличия реализации: они поддерживают общий API.

Два замечания разного рода: Exceptions/AppCreationException — техническая
группировка по виду типа (ниже); Configuration/UnitsConfigurationExtensions —
кандидат на перенос ответственности в Mithril. Helper задаёт `-u`/`-p`, определяет
processing unit и читает parent id; текущие authored callers — только Mithril.
Это смысловой вопрос публичного API, а не namespace ошибка и не основание
автоматически переносить все типы с одним текущим consumer. При исправлении
нужно определить целевое module API и совместимость extension methods/constants.

### Реестр всех проектов

«Namespace» — число файлов с несовпадением либо явно объявленными global types.
«Папки» — число файлов, нуждающихся в смысловой перегруппировке по ручному review.
Нулевые значения означают отсутствие замечаний в этом structural scope, не новую
готовность/runtime regression. У CoreHeadlessProbe собственных файлов0, но3linked.

| Проект | Собственные C#files | Namespace | Папки | Linked inclusions |
| --- | ---: | ---: | ---: | ---: |
| [TrueMoon.Cobalt.Benchmarks](../Benchmarks/TrueMoon.Cobalt.Benchmarks/TrueMoon.Cobalt.Benchmarks.csproj) | 8 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Benchmarks](../Benchmarks/TrueMoon.Enerit.Benchmarks/TrueMoon.Enerit.Benchmarks.csproj) | 3 | 2 | 0 | 0 |
| [AlloyFactoryConsumer](../ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj) | 1 | 1 | 0 | 0 |
| [AlloyPackageConsumer](../ManualTests/AlloyPackageConsumer/AlloyPackageConsumer.csproj) | 3 | 0 | 0 | 0 |
| [AlloyTest](../ManualTests/AlloyTest/AlloyTest.csproj) | 13 | 0 | 0 | 0 |
| [AlloyVulkanTest](../ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj) | 35 | 1 | 0 | 7 |
| [AppUnitsPipesProbe](../ManualTests/AppUnitsPipesProbe/AppUnitsPipesProbe.csproj) | 2 | 1 | 0 | 0 |
| [CobaltTest](../ManualTests/CobaltTest/CobaltTest.csproj) | 10 | 9 | 0 | 0 |
| [CoreHeadlessProbe](../ManualTests/CoreHeadlessProbe/CoreHeadlessProbe.csproj) | 0 | 0 | 0 | 3 |
| [CorePackageConsumer](../ManualTests/CorePackageConsumer/CorePackageConsumer.csproj) | 3 | 2 | 0 | 0 |
| [DirectCobalt](../ManualTests/CorePackageConsumer/DirectCobalt/DirectCobalt.csproj) | 2 | 1 | 0 | 0 |
| [EneritPipesProbe](../ManualTests/EneritPipesProbe/EneritPipesProbe.csproj) | 2 | 1 | 0 | 0 |
| [EngineSessionProbe](../ManualTests/EngineSessionProbe/EngineSessionProbe.csproj) | 13 | 0 | 0 | 0 |
| [Game.Logic](../ManualTests/Game.Logic/Game.Logic.csproj) | 1 | 0 | 0 | 0 |
| [MithrilCompositionProbe](../ManualTests/MithrilCompositionProbe/MithrilCompositionProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilLifecycleProbe](../ManualTests/MithrilLifecycleProbe/MithrilLifecycleProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilRestartProbe](../ManualTests/MithrilRestartProbe/MithrilRestartProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilTest](../ManualTests/MithrilTest/MithrilTest.csproj) | 5 | 0 | 1 | 0 |
| [RichTextEditorTest](../ManualTests/RichTextEditorTest/RichTextEditorTest.csproj) | 2 | 0 | 0 | 0 |
| [UnitsPackageConsumer](../ManualTests/UnitsPackageConsumer/UnitsPackageConsumer.csproj) | 1 | 1 | 0 | 0 |
| [TrueMoon.Aether.Tests](../TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj) | 23 | 0 | 0 | 0 |
| [TrueMoon.Aether](../TrueMoon.Aether/TrueMoon.Aether.csproj) | 42 | 0 | 4 | 0 |
| [TrueMoon.Alloy.Hosting.Tests](../TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj) | 7 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Hosting](../TrueMoon.Alloy.Hosting/TrueMoon.Alloy.Hosting.csproj) | 47 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Platform.Silk](../TrueMoon.Alloy.Platform.Silk/TrueMoon.Alloy.Platform.Silk.csproj) | 10 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/TrueMoon.Alloy.Rendering.Skia.csproj) | 10 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Tests](../TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj) | 33 | 0 | 0 | 14 |
| [TrueMoon.Alloy](../TrueMoon.Alloy/TrueMoon.Alloy.csproj) | 13 | 12 | 0 | 0 |
| [TrueMoon.Argentis](../TrueMoon.Argentis/TrueMoon.Argentis.csproj) | 48 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Generator.Tests](../TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj) | 4 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Generator](../TrueMoon.Cobalt.Generator/TrueMoon.Cobalt.Generator.csproj) | 5 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Tests](../TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj) | 26 | 0 | 0 | 0 |
| [TrueMoon.Cobalt](../TrueMoon.Cobalt/TrueMoon.Cobalt.csproj) | 26 | 0 | 0 | 0 |
| [TrueMoon.Contracts](../TrueMoon.Contracts/TrueMoon.Contracts.csproj) | 34 | 0 | 1 | 0 |
| [TrueMoon.Core](../TrueMoon.Core/TrueMoon.Core.csproj) | 5 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Generator.Tests](../TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj) | 9 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Generator](../TrueMoon.Enerit.Generator/TrueMoon.Enerit.Generator.csproj) | 12 | 0 | 6 | 0 |
| [TrueMoon.Enerit.Tests](../TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj) | 21 | 0 | 0 | 0 |
| [TrueMoon.Enerit](../TrueMoon.Enerit/TrueMoon.Enerit.csproj) | 35 | 0 | 0 | 0 |
| [TrueMoon.Extensions.DependencyInjection](../TrueMoon.Extensions.DependencyInjection/TrueMoon.Extensions.DependencyInjection.csproj) | 4 | 0 | 0 | 0 |
| [TrueMoon.Mithril.Tests](../TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj) | 9 | 8 | 0 | 0 |
| [TrueMoon.Mithril](../TrueMoon.Mithril/TrueMoon.Mithril.csproj) | 19 | 0 | 0 | 0 |
| [TrueMoon.Tests](../TrueMoon.Tests/TrueMoon.Tests.csproj) | 13 | 0 | 0 | 0 |
| [TrueMoon](../TrueMoon/TrueMoon.csproj) | 28 | 0 | 0 | 0 |

### Namespace замечания: 42 файла в 14 проектах

25 файлов имеют явно указанный namespace, отличный от ожидаемого, ещё 17 содержат
явно объявленные типы в global namespace. Генерируемый compiler Program для
top-level statements не считается авторским global type. 548 остальных файлов
не имеют namespace замечаний; синтаксический анализ не проверяет emitted code.

| Файл | Объявленный namespace | Ожидаемый по текущему RootNamespace/пути |
| --- | --- | --- |
| [Benchmarks/TrueMoon.Enerit.Benchmarks/Contract.cs](../Benchmarks/TrueMoon.Enerit.Benchmarks/Contract.cs) | `EneritBaseline` | `TrueMoon.Enerit.Benchmarks` |
| [Benchmarks/TrueMoon.Enerit.Benchmarks/Measurement.cs](../Benchmarks/TrueMoon.Enerit.Benchmarks/Measurement.cs) | `EneritBaseline` | `TrueMoon.Enerit.Benchmarks` |
| [ManualTests/AlloyFactoryConsumer/Program.cs](../ManualTests/AlloyFactoryConsumer/Program.cs) | (global) | `AlloyFactoryConsumer` |
| [ManualTests/AlloyVulkanTest/AsyncCloseProbe.cs](../ManualTests/AlloyVulkanTest/AsyncCloseProbe.cs) | (global) | `AlloyVulkanTest` |
| [ManualTests/AppUnitsPipesProbe/Program.cs](../ManualTests/AppUnitsPipesProbe/Program.cs) | (global) | `AppUnitsPipesProbe` |
| [ManualTests/CobaltTest/IMainTestClass.cs](../ManualTests/CobaltTest/IMainTestClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IService1.cs](../ManualTests/CobaltTest/IService1.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IService2.cs](../ManualTests/CobaltTest/IService2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IServiceTest2.cs](../ManualTests/CobaltTest/IServiceTest2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/ITestGenericClass.cs](../ManualTests/CobaltTest/ITestGenericClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/MainTestClass.cs](../ManualTests/CobaltTest/MainTestClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/Service1.cs](../ManualTests/CobaltTest/Service1.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/Service2.cs](../ManualTests/CobaltTest/Service2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/TestGenericClass.cs](../ManualTests/CobaltTest/TestGenericClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CorePackageConsumer/ConsumerProbe.cs](../ManualTests/CorePackageConsumer/ConsumerProbe.cs) | `Headless.Consumer` | `CorePackageConsumer` |
| [ManualTests/CorePackageConsumer/Graph.cs](../ManualTests/CorePackageConsumer/Graph.cs) | `Headless.Consumer` | `CorePackageConsumer` |
| [ManualTests/CorePackageConsumer/DirectCobalt/Graph.cs](../ManualTests/CorePackageConsumer/DirectCobalt/Graph.cs) | `Direct.Consumer` | `DirectCobalt` |
| [ManualTests/EneritPipesProbe/Program.cs](../ManualTests/EneritPipesProbe/Program.cs) | (global) | `EneritPipesProbe` |
| [ManualTests/MithrilCompositionProbe/Program.cs](../ManualTests/MithrilCompositionProbe/Program.cs) | (global) | `MithrilCompositionProbe` |
| [ManualTests/MithrilLifecycleProbe/Program.cs](../ManualTests/MithrilLifecycleProbe/Program.cs) | (global) | `MithrilLifecycleProbe` |
| [ManualTests/MithrilRestartProbe/Program.cs](../ManualTests/MithrilRestartProbe/Program.cs) | (global) | `MithrilRestartProbe` |
| [ManualTests/UnitsPackageConsumer/AnalyzerEvidence.cs](../ManualTests/UnitsPackageConsumer/AnalyzerEvidence.cs) | (global) | `UnitsPackageConsumer` |
| [TrueMoon.Alloy/Runtime/IUiSessionFactory.cs](../TrueMoon.Alloy/Runtime/IUiSessionFactory.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/Rendering.cs](../TrueMoon.Alloy/Runtime/Rendering.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiCleanup.cs](../TrueMoon.Alloy/Runtime/UiCleanup.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiClipboardException.cs](../TrueMoon.Alloy/Runtime/UiClipboardException.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDiagnostics.cs](../TrueMoon.Alloy/Runtime/UiDiagnostics.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDispatcher.cs](../TrueMoon.Alloy/Runtime/UiDispatcher.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDispatchOptions.cs](../TrueMoon.Alloy/Runtime/UiDispatchOptions.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiFrameMode.cs](../TrueMoon.Alloy/Runtime/UiFrameMode.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiRenderingException.cs](../TrueMoon.Alloy/Runtime/UiRenderingException.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiSession.cs](../TrueMoon.Alloy/Runtime/UiSession.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/WindowAppearance.cs](../TrueMoon.Alloy/Runtime/WindowAppearance.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/WindowChrome.cs](../TrueMoon.Alloy/Runtime/WindowChrome.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Mithril.Tests/CompositionProcessTests.cs](../TrueMoon.Mithril.Tests/CompositionProcessTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/ProcessDiagnosticsTests.cs](../TrueMoon.Mithril.Tests/ProcessDiagnosticsTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/ProcessUnitHandleTests.cs](../TrueMoon.Mithril.Tests/ProcessUnitHandleTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/RestartProcessTests.cs](../TrueMoon.Mithril.Tests/RestartProcessTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/TestSupport.cs](../TrueMoon.Mithril.Tests/TestSupport.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitCompositionTests.cs](../TrueMoon.Mithril.Tests/UnitCompositionTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitParentProcessEventsHandlerTests.cs](../TrueMoon.Mithril.Tests/UnitParentProcessEventsHandlerTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitsControllerTests.cs](../TrueMoon.Mithril.Tests/UnitsControllerTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |

У Mithril.Tests первопричина — устаревший csproj RootNamespace
`TrueMoon.Titanium.Tests`:8файлов уже объявлены в TrueMoon.Mithril.Tests.
Исправлять следует RootNamespace, а не переименовывать тесты в Titanium.
Alloy/Runtime12files сейчас объявлены в TrueMoon.Alloy; реальная Runtime
подсистема должна отражаться в namespace. Это изменение public API: вместе
обновляются consumers, aliases, docs и генерация. Для shared consumer samples
Headless.Consumer/Direct.Consumer допустимо явно задать подходящий RootNamespace;
решение должно сохранять согласованность реального пути и linked compilation.

### Технические группы: 5 папок, 12 файлов в 4 проектах

Здесь namespace уже соответствуют текущим папкам; замечание относится к самим
категориям. Группировка по Contracts/Models/Exceptions/Extensions/Utils не
показывает назначения типов. Целевые имена категорий ниже — рекомендации,
а не выполненные переносы или новые обязательные проекты.

| Папка | Файлы | Причина и направление исправления |
| --- | --- | --- |
| `TrueMoon.Aether/Contracts` | [Control.cs](../TrueMoon.Aether/Transport/IEngineControl.cs), [Frames.cs](../TrueMoon.Aether/Frames/Frames.cs), [GameCheckpoint.cs](../TrueMoon.Aether/Checkpoints/GameCheckpoint.cs), [GameCheckpointCodec.cs](../TrueMoon.Aether/Checkpoints/GameCheckpointCodec.cs) | Control.cs смешивает session/game/control/scene contracts; Frames.cs содержит frame types; GameCheckpoint.cs и GameCheckpointCodec.cs относятся к checkpoint state/codec. Распределить по Frames, Sessions, checkpoint/control подсистемам; общие для Aether типы без категории — в корень Aether. Не переносить engine-only API в TrueMoon.Contracts. |
| `TrueMoon.Contracts/Exceptions` | [AppCreationException.cs](../TrueMoon.Contracts/AppCreationException.cs) | Единственный AppCreationException сгруппирован по виду типа. Перенести в корень общего проекта либо вместе с реальной App категорией; учесть публичный namespace TrueMoon.Exceptions. |
| `TrueMoon.Enerit.Generator/Extensions` | [GenerationContextExtensions.cs](../TrueMoon.Enerit.Generator/GenerationContextExtensions.cs), [SourceHandleExtensions.cs](../TrueMoon.Enerit.Generator/SourceHandleExtensions.cs), [SourceHandleSerializationExtensions.cs](../TrueMoon.Enerit.Generator/SourceHandleSerializationExtensions.cs), [SymbolExtensions.cs](../TrueMoon.Enerit.Generator/SymbolExtensions.cs) | Четыре файла объединены формой extension methods; содержат обработку symbols и генерацию методов/сериализации. Группировать по назначению генерации/сериализации/symbol analysis либо размещать в корне generator, рядом с GenerationContext/SourceHandle. |
| `TrueMoon.Enerit.Generator/Utils` | [GenerationUtils.cs](../TrueMoon.Enerit.Generator/GenerationUtils.cs), [WellKnownTypes.cs](../TrueMoon.Enerit.Generator/WellKnownTypes.cs) | GenerationUtils и WellKnownTypes скрывают конкретную работу с Roslyn type symbols под общей технической категорией. Расположить рядом с относящимися к ним типами или в смысловой категории обработки symbols; namespace должен следовать новому пути. |
| `ManualTests/MithrilTest/Models` | [TestPoco.cs](../ManualTests/MithrilTest/TestPoco.cs) | TestPoco.cs содержит набор тестовых DTO, records, enums и тестовый класс, объединённых видом типов. Для текущего небольшого примера — корень MithrilTest; отдельную категорию вводить только по конкретному сценарию. |

### Рекомендуемый порядок исправления

1. Исправить stale RootNamespace у Mithril.Tests;8замечаний устраняются одной
   настройкой без переименования существующих C#namespace.
2. Привести12Alloy/Runtime namespace к фактической подсистеме с обновлением
   source/package consumers, partial/aliases и module checks.
3. Перегруппировать4Aether/Contracts files по назначению, разбивая смешанные
   файлы при необходимости; публичные engine contracts остаются в Aether.
4. Разобрать AppCreationException и units helper в Contracts/Core/Mithril scope,
   сохранив независимость общей сборки и явно определив API compatibility.
5. Перегруппировать6generator helper files Enerit и согласовать benchmark root
   namespace; повторить generator output/consumer проверки после изменений.
6. Согласовать namespace20manual-consumer files и расположение MithrilTest Models;
   для linked/shared examples учитывать обе компиляции и source-generation fixtures.

Массовые переносы/namespace/API changes в этом аудите не выполнялись. Детальный
реестр ведётся здесь, module STATUS ссылаются на него. Функциональный engine
маршрут ENG4e и аппаратные/release ограничения не меняются. Следующий шаг по
этому review — пункт1, затем более широкий public API scope пункта2.

Завершающая сверка: SHA256 всех 590 C#-файлов и 44 проектов совпали с audit
snapshot; в 13 изменённых Markdown проверены 466 локальных ссылок и 201 anchor.
`git diff --check` прошёл. Результат — `TestResults/StructureAudit/verification.json`.

## Размещение типов по назначению — 2026-10-21

По запросу пользователя в AGENTS.md закреплено: типы без отдельной предметной
или функциональной категории размещаются в корне проекта. Подпапки группируют
связанные типы по назначению; вид типа (класс, DTO, модель, интерфейс) или статус
контракта сам по себе не требует Contracts/Models/DTOs/Interfaces. Namespace
соответствует фактическому пути. Отдельный TrueMoon.Contracts сохраняется.
Общий PLAN ссылается на правило, STATUS отделяет эту документационную правку от
проверок ENG4d2. Исходники/API не менялись; build/test/native не запускались.

## ENG4d2 ordinary GPU GameEditor — 2026-10-20

App option выбирает CPU(default)/GPU strategy; FrameSession сохраняет общий
Start/resize/Stop/first-present, GameEditor/Replacement — selected DLL/checkpoint/
rollback. Neutral surface/ticket не содержат native/mapping handles. Runtime GPU
metadata происходит из loaded/restored selected game; reference pixels пока
CPU-generated/Vulkan staging. UI production APIs прежние, engine одна Aether
assembly без UI dependency. Full Debug1898/1898/9TRX/failed0/skipped0, Aether493 и
UI615 Debug/Release, build0errors/1499warnings; fresh12packages/4App consumers.
Final54native cases/56commands/406source/76binary fingerprints; ordinary GPU Editor20Replace/20failure cases/53children per configuration, validation0/0,47devices/141slots balanced, imported readback0.
[Точные commands/evidence/limits и промежуточные ошибки](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед ENG5/3D. K3/K7/K8/VRAM/final alpha,
driver/Windows settings и остановка K7 workaround search прежние.

## ENG4d GPU viewport bridge — 2026-10-20

В Aether добавлен owner callback loan, в Rendering.Skia — независимая retained
GPU image copy. Reference actual viewport проверяет premul/stored sRGB/origin,
scale/clipping, presentation и image lifetime без imported readback delivery.
Ordinary GameEditor остаётся CPU; следующая точка ENG4d2 selected DLL/Build/Replace/
checkpoint GPU transport. Engine assembly одна; UI dependency в Aether не добавлена.

Full Debug1858/1858/9TRX/failed0/skipped0; Aether453 и UI615 Debug/Release.
Build0errors/1499warnings;52native cases/54commands,398source/76binary hashes;
fresh12packages/4consumers. GPU viewport per config17checks/14captures/3,051,552pixels,
20GPU copies/6reaped children, imported ReadPixels0, core/sync validation0/0.
Точные commands/reports, interim failures и ограничения —
[engine HISTORY](engine/HISTORY.md#eng4d-gpu-viewport-bridge--2026-10-20).
UI K3/K7/K8/VRAM/final alpha сохраняются; drivers/Windows settings не менялись.

## ENG4c reusable GPU surfaces — 2026-10-20

Aether App-owned surface session/channel2:3persistent slots/shared Vulkan device,
retirement перед reuse, resize/queued resize/suspend, Stop/cancel/deadline/crash/
restart и failed cleanup/retry. Ordinary viewport CPU default; далее ENG4d actual
GPU viewport/presentation/compositing. Все engine реализации в одной assembly,
новых dependencies/UI references нет.

Aether444/444 Debug/Release; full Debug1847/1847/9TRX/failed0/skipped0.
50native cases/52commands/exit0,394source/76binary fingerprints, build0errors/
1499warnings; fresh9packages/4Cobalt+DI consumers. Каждый surface run21checks/
80frames/12reaped children,9cooperative reports с15devices/45slots created/released,
validation core/sync errors0/warnings0. Остатки parent GPU/NT/UI0;3crash children
имеют owned reap без cooperative teardown claim. Old Editor held-worker marker
исправлен на atomic publication после воспроизведённой sharing violation; final
matrix повторена на fresh snapshot. [Commands/evidence/границы](engine/HISTORY.md#eng4c-reusable-slots-и-surface-lifecycle--2026-10-20).
Full Release solution/UI packages/golden/RTE/performance/hardware не повторялись;
K3/K7/K8/VRAM/final alpha прежние, drivers/Windows settings не менялись.

## ENG4b one-frame GPU import — 2026-10-20

TrueMoon.Aether получил App-owned lazy GPU resources, dedicated Vulkan image/
binary semaphores, owned-process NT duplication, GL import и строгий ready/return/
retirement channel. Debug/published Release:14checks/import run,3whole-image
roundtrips,8reaped children, core/sync validation errors0/warnings0, остатки0.
Ordinary viewport CPU default; следующий ENG4c bounded slots/surface lifecycle.

Aether366/366 Debug/Release; full Debug1769/1769/9TRX/failed0/skipped0;
48native cases/50commands,388source/76binary fingerprints, build0errors/
1499warnings. Fresh9packages/4Cobalt+DI consumers подтверждают delivery/lazy
ownership; Aether→Silk.NET.Vulkan2.23.0, UI references нет. Детали/commands/
границы — [engine HISTORY](engine/HISTORY.md#eng4b-one-frame-import-и-retirement--2026-10-20).
Full Release solution/UI packages/golden/RTE/performance/hardware не повторялись.
UI K3/K7/K8/VRAM/final alpha прежние; drivers/Windows settings не менялись.

## ENG4a GPU capability decision — 2026-10-20

Pure Aether GPU snapshots/decision и App-owned child/current GL context probe
проверены. RTX5070Ti UUID/driver/exact image/handle/semaphore/GL requirements
допускают ENG4b import-probe; CPU selected/ImportTested=false, ENG4 в целом открыт.
Aether316/316 Debug/Release; full Debug1719/1719/9TRX/failed0/skipped0;
nonincremental0errors/1499warnings.46native cases/48commands/exit0,
377source/76binary hashes; новые cases по1reaped child/instance destroyed/
mappings/UI sessions0/authoring preserved. Actual import/shared GPU/3D/package/
performance и UI hardware acceptance не доказаны queries, drivers/settings прежние.
[Подробные tests/raw queries/команды/evidence](engine/HISTORY.md#eng4a-gpu-capabilities-и-transport-decision--2026-10-20).

## ENG3d Editor commands и proof — 2026-10-20

AetherHost.Editor добавляет UI-independent App-owned commands, immutable authoring
context/source/selection version и stale-result fences. Viewport имеет Build/
Replace/Cancel/Refresh controls; Stop/App close drains accepted work до UI disposal.
ENG3a–d завершены в bounded Windows/.NET10 CPU scope, следующий ENG4a capabilities/
transport decision. Core/Enerit/Mithril/UI production APIs прежние.

Aether254/254 Debug/Release, full Debug1657/1657/9TRX/failed0/skipped0;
nonincremental Debug0errors/1499warnings.44native cases/46commands/exit0,
372source/76binary hashes. Каждый Debug/published Release Editor proof:
20A/B Replace/20failure cases/775actual GL checks/53children/maxlive1/allreleased,
state/first tick/selected Step и authoring сохранены. Classic viewport по17cases/
27children/20StartStop; mappings/UI sessions0. UI hardware/package/performance
acceptance не расширен. [Единственные подробные tests/failures/команды/evidence](engine/HISTORY.md#eng3d-команды-editor-и-20replace-proof--2026-10-20).

## ENG3c serialized Replace и rollback — 2026-10-20

AetherHost.Replacement объединяет capture/old stop/reap/selected restore/first-present
и прежний mode, один rollback, shared command reservation и global45s budget.
Stop/App close fences late starts; unconfirmed cleanup сохраняет task/lease для
явного retry. Original/recovery errors наблюдаемы отдельно. Shared Core/Enerit/
Mithril/UI production APIs прежние, новых package dependencies нет.

Aether231/231 Debug/Release, full Debug1634/1634/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1499warnings.42native cases Debug/published
Release прошли: по8replacement children/max live1, actual A→B DLL/hash/state/CPU,
rollback и Stop inside held restore; по17viewport cases/27children/20StartStop,
actual GL first-present и cleanup.368source/76binary fingerprints.
[Единственные подробные tests/failures/команды/evidence](engine/HISTORY.md#eng3c-serialized-replace-и-rollback--2026-10-20).
Следующий ENG3d — Editor commands/20Replace acceptance при одном App/window.
Новые package consumers/full Release solution/performance/hardware не проверялись.

## ENG3b checkpoint и startup restore — 2026-10-20

В Aether реализованы optional checkpoint capability, immutable bounded TMCK v1
snapshot, paused observational capture с подтверждённым nonterminal отказом и
fresh startup restore до surface/first-present. Поддержан selected-build restore,
host/scene/revision preflight и explicit reset без checkpoint. Unknown outcome
сохраняет fault/cleanup policy; Stop во время restore не публикует кадры.
Общие production Core/Enerit/Mithril/UI APIs/dependencies прежние.

Aether184/184 Debug/Release, full Debug1587/1587/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1499warnings.40native cases в Debug apphost/
published Release, по16viewport cases/26children/20StartStop, actual restored
GL first-present, compiler/child close cleanup и mappings/sessions0. Source362/
binary76 fingerprints сохранены. [Единственные подробные tests/команды/evidence](engine/HISTORY.md#eng3b-checkpoint-и-startup-restore--2026-10-20).
Следующий ENG3c объединяет primitives в Replace/однократный rollback с общим45s
deadline; ENG3d proof и общий UI hardware/release остаются открытыми. Package
consumer нового API/full Release solution/performance matrix не повторялись.

## ENG3a compiler и immutable game builds — 2026-10-20

По принятому пользователем порядку накопленные extraction/App/package/rules/docs
изменения зафиксированы dc64b63. Затем в Aether реализованы immutable manifest/
artifact, owned compiler job и выбранный child-only loader; SourceRevision/build
completion не применяются автоматически к текущей сессии. App/viewport Closing
отменяет/reaps compiler параллельно с engine cleanup, до UI disposal. Общие
Core/Cobalt/Enerit/Mithril/UI production APIs для ENG3a не расширялись.

Aether139/139 Debug/Release; full Debug1542/1542,9TRX/failed0/skipped0;
nonincremental Debug build0errors/1508warnings.38native cases прошли, включая
2A/B build probes с actual child DLL path/hash/color/CPU pixels и2viewport со
held compiler/native close.356source/76binary fingerprints сверены.
[Единственные подробные contracts, commands, test matrix и границы](engine/HISTORY.md#eng3a-compiler-artifacts-и-child-loader--2026-10-20).

ENG3a завершён в bounded trusted-local managed/net10.0/Windows x64 scope;
следующий шаг — ENG3b checkpoint/restore, затем ENG3c transaction/rollback.
Пакеты/full Release solution/performance/long resource/hardware matrix этим
шагом не повторялись; общий release/UI alpha не объявлены готовыми.

## Аудит точек подключения модулей к App — 2026-10-19

После реализации UseAether по запросу пользователя проверены остальные модули:
публичные App extensions, реальные service/lifecycle registrations, project graph
и существующие focused integration tests. Проверены15активных library/analyzer
projects верхнего уровня, включая уже подключённый Aether. Test/executable projects
не являются самостоятельными функциональными модулями. TrueMoon.Magnesium —
пустая папка без csproj/исходников и записи в solution, отдельного модуля сейчас нет.

| Модуль / сборки | Публичная точка / способ подключения | Что подключает |
| --- | --- | --- |
| TrueMoon bootstrap | App.Builder(...).Setup(...), App.Build(...), App.RunAsync(...) | App-local configuration/resolver/lifecycle; встроенная основа приложения |
| Diagnostics в TrueMoon | [UseDiagnostics](../TrueMoon/Diagnostics/AppCreationContextExtensions.cs) | Diagnostics configuration, provider-owned subscription/factory; повторная регистрация заменяет настройки |
| TrueMoon.Cobalt | [AppBuilderConfigurationContext.GetServiceResolverBuilder](../TrueMoon/AppBuilderConfigurationContext.cs) | Default CobaltServiceResolverBuilder; дополнительный UseCobalt для обычного App не требуется |
| TrueMoon.Extensions.DependencyInjection | [UseDI](../TrueMoon.Extensions.DependencyInjection/AppCreationContextExtensions.cs) | Выбор provider через IAppBuilderConfigurationContext при App.Builder, до Setup/service resolution |
| TrueMoon.Enerit | [UseInvocationService / ListenInvocationService](../TrueMoon.Enerit/AppCreationContextExtensions.cs), [PipesServiceTransport](../TrueMoon.Enerit/IO/Pipes/EneriteConfigurationContextExtensions.cs) | Generated proxy/handler, client/server/factories; non-disposable IStartable/IStoppable adapters запускают/останавливают transport |
| TrueMoon.Mithril | [AddUnit](../TrueMoon.Mithril/AppCreationContextExtensions.cs) | App-local unit declarations/snapshots, UnitsController и lifecycle; child selection/parent monitor, main-process composition |
| Alloy UI через TrueMoon.Alloy.Hosting | [UseAlloy / UsePresentation](../TrueMoon.Alloy.Hosting/AppConfigurationExtensions.cs) | Renderer/session factory для external host либо standalone window/dispatcher с IStartable/IStoppable |
| TrueMoon.Aether | [UseAether](../TrueMoon.Aether/AppConfigurationExtensions.cs) | App-owned host/supervisor/frames; собственная реализация и proof описаны в engine history |

UseDI — настройка bootstrap provider, поэтому принимает IAppBuilderConfigurationContext,
а функциональные extensions работают на IAppConfigurationContext. Это два этапа
одного App builder, а не отсутствующая module registration. Общая configuration
доступна через IAppBuilder.Configuration и IAppConfigurationContext.Configuration.

Отдельные UseX не требуются у TrueMoon.Contracts/Core (contracts/utilities),
Argentis/Alloy/Rendering.Skia/Platform.Silk (UI слои с явной optional интеграцией
через Hosting) и Cobalt.Generator/Enerit.Generator (compile-time analyzers).
Compatibility.UsePresentation находится в отдельном legacy namespace и не
заменяет современный explicit renderer/host API. Закомментированный Enerit MMF
prototype не учитывался как рабочая точка подключения.

Пропущенных публичных точек подключения действующих функциональных модулей не
найдено; runtime code/API/project graph менять не потребовалось. При чтении
проверены options/registration и lifecycle paths. Это не полный повторный audit
всех cleanup/error/ownership контрактов или доказательство общего release.

### Фактически выполненные проверки

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest. Использованы существующие тесты,
без новых test cases/packages. Четыре команды exit0;4TRX, Passed:106, Failed:0,
Skipped:0. Core46, Enerit13, Mithril9, Alloy.Hosting38. Filtered test names
проверены по каждому TRX; пустая discovery не засчитывалась.

```powershell
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~TrueMoon.Tests.AppLifecycleTests|FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests|FullyQualifiedName~TrueMoon.Tests.AppTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Core --verbosity quiet
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~AppInvocationRegistrationTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Enerit --verbosity quiet
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~UnitCompositionTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Mithril --verbosity quiet
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~AlloyIntegrationTests|FullyQualifiedName~HostedUiSessionFactoryTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Alloy --verbosity quiet
```

Core tests повторили App startup/rollback/stop/dispose и diagnostics на обоих
providers. Enerit — App-local/idempotent proxy/listener lifetime и реальное
соединение named pipes в одном процессе на Cobalt/DI. Mithril — repeated Build
isolation, main/child configuration selection и local lifecycle на Cobalt/DI.
Alloy — registration/validation/snapshot и session-factory cleanup guards;
native window/GPU и реальный standalone App window этим запуском не проверялись.

Артефакты: `TestResults/AppModuleAudit/20261019/summary.json`, Completed=true,
stdout/stderr и4TRX;463source/project/build-input SHA256 совпали до/после запуска.
Это текущие focused Debug results; full solution/Release, собственные Cobalt/
generator suites, child-process/restart/native GPU/package/performance матрицы
не повторялись. Прежний полный Debug1518 и Aether115/36native относятся к
предыдущей реализации App integration. Общая очередность ENG3a и UI ограничения
K3/K7/K8/VRAM/final alpha не менялись.

## Aether App integration — 2026-10-19

По запросу пользователя реализован UseAether/AetherHost: options/services и
owned session/frame cleanup подключены к App lifecycle. В AGENTS.md и общем
плане закреплено правило подключения функциональных модулей через App contracts.
Utilities/contracts без lifecycle не требуют фиктивного UseX. Headless/viewport
examples используют публичную регистрацию модуля. Core/UI production API прежние.

Aether115/115 Debug/Release; full Debug1518/1518/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1507warnings.36native cases Debug/published
Release включают новый app-stop и2viewport с actual pixels/20StartStop/cleanup.
9fresh packages и4independent Cobalt/DI consumers проверили App integration.
Aether IsPackable=true; прежний13+1deferred metadata audit ниже исторический,
полная metadata matrix всех44projects в этом шаге не повторялась. Публикации нет.
[Единственные подробные команды, тесты, failures и fingerprint evidence](engine/HISTORY.md#app-integration-реализация--2026-10-19).
Следующий общий code шаг — ENG3a; UI аппаратные/release ограничения сохраняются.

## NuGet package metadata — 2026-10-19

По запросу пользователя проверены metadata всех проектов, предназначенных для
NuGet-поставки, включая подготовку будущего Aether package. Сохранены uncommitted
перенос Aether и предшествующая сверка ENG3 плана. Runtime C#/API/test code в этом
metadata шаге не менялся. Новых unit/native/consumer/performance запусков нет;
прошлые Aether1490/34native и UI INF7 не объявляются результатами этого audit.

### Findings и исправления

- Description отсутствовал у Argentis/Alloy/Platform.Silk. Добавлены собственные
  descriptions и тематические tags всем13packable projects плюс deferred Aether.
- Core/Argentis/Alloy/Silk выбирали общий repository README. Добавлены собственные
  PACKAGE.md с явными PackageReadmeFile/None Pack. Aether также получил standalone
  package README; IsPackable=false сохранён, обычная поставка не включена.
- Общие PackageProjectUrl/RepositoryUrl/RepositoryType теперь заданы в
  Directory.Build.props; Authors/Company/Product/version/copyright/license/icon
  унаследованы. PackageId и version используют SDK defaults, лишние одинаковые
  значения по csproj не копируются. Базовые tags отражают TrueMoon/.NET/modules,
  специальные tags добавляются в владельце проекта.
- Shared license/icon/fallback README Includes привязаны к MSBuildThisFileDirectory,
  поэтому не зависят от глубины project directory. Существующая MIT license не
  изменялась. PackageReference/project dependency и generator ownership сохранены.

### Inventory и actual package proof

| Направление | Intended packages | Статус |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core |3packable |
| Cobalt / DI | TrueMoon.Cobalt, TrueMoon.Extensions.DependencyInjection |2packable; Cobalt.Generator остаётся embedded analyzer, не отдельный package |
| Enerit | TrueMoon.Enerit, TrueMoon.Enerit.Generator |2packable, generator — отдельный analyzer package |
| Mithril | TrueMoon.Mithril |1packable |
| UI | TrueMoon.Argentis, TrueMoon.Alloy, TrueMoon.Alloy.Platform.Silk, TrueMoon.Alloy.Rendering.Skia, TrueMoon.Alloy.Hosting |5packable |
| Aether | TrueMoon.Aether |1deferred; metadata подготовлены, default IsPackable=false |

Всего44repository projects с учётом untracked Aether/Aether.Tests и удаления старых
Contracts/test projects:13packable,1deferred,30excluded tests/manual/benchmarks/
embedded generator. Evaluated properties берутся из MSBuild, не только XML csproj.

Добавлен tracked [eng/Check-PackageMetadata.ps1](../eng/Check-PackageMetadata.ps1).
По умолчанию проверяет metadata; `-Pack` создаёт fresh local nupkgs в packages/
deferred-preview scopes. Проверяет ID/version/authors/company/product/description/
copyright/tags/project/repository/license/icon/readme, отсутствие placeholder
Description и наличие локального PACKAGE.md. Пакеты сверяются по actual nuspec,
README/license/icon bytes, managed DLL/XML и Cobalt/Enerit analyzer/Skia native/
buildTransitive payloads.99metadata input hashes защищают run от изменения файлов;
58owned dotnet commands имеют timeout/exit/logs, partial manifest сохраняется.
Existing RunDirectory не overwrite, только owned process tree cleanup, NuGet push нет.

```powershell
pwsh -NoProfile -File eng/Check-PackageMetadata.ps1 -Pack -RunDirectory TestResults/PackageMetadata/completion-20261019
```

Completedtrue,44projects/14intended/14archives:13regular и1Aether preview,
version0.2.2.7-alpha. Metadata и required payload assertions прошли. Aether preview
явно использует command-local IsPackable=true, не меняет project default. Пакеты
созданы в ignored TestResults/PackageMetadata/completion-20261019, не опубликованы.

Первый review-20261019 остановился на verifier bug: wildcard TrueMoon.Alloy.*
выбирал также Hosting/Skia/Silk archives. Исправлен exact ID+version filename;
reviewed и final runs прошли. Completion run выполнен после whitespace cleanup
XML и усиления проверки XML/license/icon bytes; это authoritative final input set.
Существующие compiler/generator warnings не подавлялись, API warnings policy этим
audit не менялась. Archive proof не заменяет independent runtime consumers/тесты.

Итоговые docs links/anchors/whitespace и read-only archive/input fingerprint checks
сохраняются отдельно в ignored TestResults/PackageMetadata/completion-check.json.
Passedtrue:39Markdown files/741local links/314anchors;99metadata inputs,
14archives/73payload hashes сверены повторно. Aether default nonpackable,
owned helpers завершены, staged0, git diff --check прошёл.
Общие правила добавлены в PLAN/R4, статусы всех затронутых направлений ссылаются
сюда. Следующий engine code шаг остаётся ENG3a; metadata не закрывает ENG3/release/
hardware blockers.

## TrueMoon.Aether — 2026-10-19

По запросу пользователя engine implementation/contracts вынесены в одну сборку
TrueMoon.Aether, tests — TrueMoon.Aether.Tests с direct ProjectReference. UI/game/
executable composition остаются отдельными consumers. Updated solution/map/namespaces,
без изменения общего Core/DI/IPC/units/UI API. Aether87/87 Debug/Release,
full Debug1490/1490/9TRX/failed0/skipped0 и34native cases прошли. Детальные
[решения/команды/evidence](engine/HISTORY.md#truemoonaether--2026-10-19) ведутся у engine.
Следующий этап ENG3a в Aether; experimental assembly не является release/packages/3D proof.

## Сверка плана после UI INF7 — 2026-10-19

Общий/UI/engine roadmap сопоставлен с текущим probe code. Очередность ENG3
сохранена; следующий конкретный code подэтап ENG3a compiler/artifact, затем
checkpoint/replacement/rollback/integration proof. Уточнения loader/identity,
restore-before-present, recoverable checkpoint failure, cancellation и общего
deadline ведутся только в [engine плане](engine/PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19).
Исправлены устаревшие ENG1/InputBatch/overlapping sessions формулировки; INF5/RTE
и hardware blockers не отменены. Это документация без новых build/tests/native/
performance результатов. [Findings/проверка](engine/HISTORY.md#сверка-плана-после-ui-inf7--2026-10-19).

## UI INF7 integration runner — 2026-10-19

Сохранён воспроизводимый runner Assembly/Source/Package/All с bounded owned
processes, fresh evidence, строгими TRX/package/native assertions и read-only
verifier. Production API и project wiring прежние. All:46commands,4TRX/1384passed
(UI613/Hosting79 Debug/Release),24native processes,10fresh nupkgs и4required
analyzer/native payloads;21guard checks и standalone Assembly прошли.
394source inputs/840artifact fingerprints сверены. Новый full solution не
запускался;1490 относится к INF6. Bounded INF0–INF4/INF6/INF7 и минимальный INF5
завершены; далее ENG3. Full INF5/RTE/hardware backlog сохраняются.
[Контракт/команды/failures/evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).

## UI INF6 explicit root factories — 2026-10-19

Новый UsePresentation(rootFactory, configure) без hidden view registration, snapshot
до Build и explicit renderer/target/presenter preflight. Cobalt/DI/generic overload
сохранены; custom standalone factory теперь декларирует IUiWindowSessionFactory.
Full Debug1490/1490,9TRX/failed0/skipped0; UI613/Hosting79 Debug/Release. Source
8native consumers/48App cases +4close/HUD processes и10fresh packages/8independent
native consumers без Cobalt analyzer прошли. Source/binary/package hashes сверены.
Следующий UI шаг INF7 runner; full INF5 частичен, ENG3/RTE/hardware backlog прежний.
Новый nonincremental build/golden/engine viewport/performance не запускались.
[Contract/matrix/commands/failures/evidence](alloy/HISTORY.md#inf6-явные-factories-и-совместимость--2026-10-19).

## UI INF4 async close — 2026-10-18

Owner-safe RequestCloseAsync/Closing: cancel/save/drain, concurrent join, native
requests и forced App Stop, observable errors и cleanup-before-success. Engine
viewport переведён на этот API. Full Debug1466/1466,9TRX/failed0/skipped0;
UI613/Hosting55 Debug/Release. Fresh7packages/2native consumers и10manual/engine
native processes Debug/published Release прошли. Следующий UI этап INF6, full INF5
частичен; ENG3/RTE/hardware backlog сохраняется. Новый nonincremental build/golden/
performance в INF4 не выполнялся. [Полные команды/матрица/evidence](alloy/HISTORY.md#inf4-асинхронное-закрытие--2026-10-18).

## UI INF3 frame scheduling — 2026-10-18

Добавлены Automatic/OnDemand/Continuous, coalesced RequestFrame/work wake,
monotonic cadence, bounded shared wait и cached native expose. Output freshness
отделён от pending queue/future requests через NeedsRender.
[Contract](alloy/PLAN.md#inf3inf6-условия-интеграции),
[commands/failures/provenance](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18)
ведутся в UI документации. Следующий UI этап INF4; full INF5 частичен.

Final UI613/Hosting39 Debug/Release; повторный full Debug1450/1450,
9TRX/failed0/skipped0. Первый1449/1450 имел Mithril readiness sharing failure;
targeted20 и full94 retry прошли, race не исправлялась. Build0errors/1211warnings.
Fresh7packages/2independent consumers,10native processes с scheduling/legacy multi-
window/HUD/golden360/engine viewport прошли Debug apphost/published Release.
Fixture hashes неизменны, validation/cleanup прошли в своих scopes. Это не
CPU/GPU speedup, full Release solution/32ENG1 headless/full10package/40consumer/
native RTE matrix и не закрытие UI hardware blockers. Engine ENG3/RTE backlog
сохранены; Mithril failure и engine integration отдельно отражены в module statuses.

## Минимальный UI INF5 — 2026-10-18

После INF2 добавлена opt-in fixed-storage diagnostics для session/queue/phases и
host presentation. Единственный [контракт/остаток INF5](alloy/PLAN.md#inf3inf6-условия-интеграции)
и [команды/evidence/overhead](alloy/HISTORY.md#inf5-минимальная-диагностика--2026-10-18)
ведутся в UI документации. Общий PLAN/STATUS/QUALITY обновлены; engine STATUS
отражает instrumentation существующего viewport probe, без начала ENG3.

UI612/Hosting18 Debug/Release; full Debug1428/1428,9TRX/failed0/skipped0;
nonincremental Debug build0errors/1211warnings. Два bounded ENG2 viewport smoke
с diagnostics прошли Debug apphost/published Release. Семь fresh local packages
и independent headless consumer2cases проверили новое public API; это не прежняя
full10package/40consumer native matrix. BDN4cases измерили on/off overhead без
обещания UI/GPU speedup. Full INF5 частичен; далее INF3, engine ENG3/RTE/K3/K7/K8/VRAM
остаются в своих scopes. Full Release solution/32ENG1 headless/golden/native RTE/
Vulkan matrix в этом шаге не повторялись.

## UI INF2 dispatcher — 2026-10-18

После INF1 реализован bounded session dispatcher, observable awaitable dispatch
и latest-value PropertyBinding coalescing. Единственные подробные
[contract/acceptance](alloy/PLAN.md#inf2-queue-и-dispatch-contract) и
[команды/evidence](alloy/HISTORY.md#inf2-ограниченный-dispatcher--2026-10-18) находятся
в UI документации. Общий PLAN/STATUS/QUALITY обновлены без дублирования tasks.

UI596/596 и Hosting14/14 Debug/Release; full Debug solution1408/1408,
9TRX/failed0/skipped0. Nonincremental Debug build0errors/1211warnings.
Два bounded ENG2 viewport smoke Debug apphost/published Release прошли с actual
pixels и cleanup. Это не full Release solution/packages/golden/32headless native
ENG1 run и не UI hardware/performance acceptance. Следующий инфраструктурный UI
шаг — минимум INF5 перед INF3; engine ENG3 и отложенный RTE backlog сохраняются.

## ENG1 headless supervisor — 2026-10-17

После UI RichTextEditor и сохранения его отложенного плана реализован ENG1:
probe host/contracts/Game.Logic stub/tests, fresh owned child generations,
Hello/scene readiness, serialized commands, timeout/cancel/crash/disconnect/parent
exit и cleanup при живом Editor App/authoring fixture. Единственные подробные
[contracts](engine/PLAN.md#eng1-headless-supervisor--2026-10-17) и
[commands/failures/evidence](engine/HISTORY.md#eng1-headless-supervisor--2026-10-17)
находятся в engine документах; проекты в ManualTests/TrueMoon.slnx folder Engine.

Engine31/31 Debug/Release, native32/32 Debug apphost/published Release с20Start/Stop
в каждом configuration. Full Debug solution tests1305/1305,8TRX/failed0/skipped0;
nonincremental Debug build0errors/1207warnings. Release остальных suites и UI
native/golden/package не повторялись. Следующий code шаг — ENG2 CPU ring/viewport;
replacement ENG3, UI hardware acceptance и общий release ещё открыты.

## RichTextEditor перед ENG1 — 2026-10-17

По запросу пользователя перед ENG1 реализован UI RichTextEditor/options поверх
Topten.RichTextKit с neutral Argentis contracts и Skia adapter. Единственный
детальный [контракт PA7a](alloy/PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17),
[API guide](alloy/RICH_TEXT_EDITOR.md), [failures/commands/evidence](alloy/HISTORY.md#richtexteditor--2026-10-17).
UI563/563 отдельно Debug/Release,6native demo cases,40fresh package processes и
прежний golden360 с validation teardown прошли. Общий nonincremental Debug build:
0errors/1200warnings; own headless suites не повторялись. Общие/UI status и quality
matrix синхронизированы; ENG1 остаётся следующим code шагом, engine code пока нет.
PA7/beta, UI alpha и аппаратные ограничения не закрыты.

## ENG0 scope и допуск прототипа — 2026-10-15

После UI package/Hosting quality пользователь одобрил ограниченный engine/editor
prototype с открытыми UI рисками. В engine PLAN определены scope/demo, platform,
session/control/frame v1, ownership/backpressure, checkpoint/replacement/rollback,
deadlines и будущие acceptance cases. [Единственный детальный контракт](engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15),
[история и проверка документации](engine/HISTORY.md#eng0-scope-и-контракты--2026-10-15).

ENG0 — проектирование; ENG1–ENG8 ещё запланированы. Следующий code шаг — headless
ENG1 supervisor/Hello/repeat/cancel/crash/cleanup. Shared CPU viewport и versioned
Game.Logic replacement относятся к ENG2/ENG3. Новых engine projects/runtime APIs,
build/tests/process/GPU/performance результатов нет; численные targets не измерены.

Общие PLAN/STATUS/QUALITY и UI continuation синхронизированы. K3/K7/K8/VRAM/final
alpha и остановка NVIDIA workaround search сохранены. Обычный child crash не
объявляется изоляцией shared GPU/driver fault; driver/Windows settings не менялись.
Предыдущие module quality passes сохраняют даты и не считаются engine evidence.

## UI quality после общей матрицы — 2026-10-15

Выполнен следующий scope из quality matrix: fresh UI package/Hosting proof после
bundled Cobalt и explicit fonts. Воспроизведён и исправлен missing resolver
HostedUiDispatcher (constructor registration→explicit factory). Existing consumer:
10fresh nupkgs,36App Cobalt/DI/DI-without-analyzer +4direct renderer processes,
Debug/Release ×build/publish, fonts/OFL/native/analyzer/generated provenance и
финальные fingerprints; expected CS0246 negative control отдельно.
[Точные commands/failures/evidence и ограничения](alloy/HISTORY.md#ui-packagehosting-quality--2026-10-15).

Отдельно current UI532/Core107/Cobalt179/generator91 в каждой Debug/Release:
8TRX/1818passed/0failed/0skipped. Read-only golden360/360, fixtures unchanged,
validation teardown passed. Общий solution build, Enerit/Mithril/performance,
physical DPI/real loss/VRAM/long K7/final alpha не повторялись. Пакеты не опубликованы,
driver/Windows settings и default/adapter/migration решение сохранены.

Общие PLAN/STATUS/QUALITY и UI PLAN/STATUS обновлены: package gap закрыт;
следующий выбор — доступный UI acceptance либо отдельно согласованный bounded
ENG0 с сохранением UI рисков. Engine/PA1–PA10 этим шагом не начинаются. Engine
STATUS обновлён только по dependency summary; собственных engine results нет.

[Общий статус](STATUS.md), [план](PLAN.md), [индекс документов](README.md), [формат ведения](PLANNING.md). Подробные implementation/test/native записи принадлежат HISTORY соответствующего модуля; эта запись не заменяет их результаты.

## Общий README и навигация — 2026-10-11

По запросу пользователя сверены общий README, source projects/ProjectReference graph, solution, SDK/package/test настройки, общие PLAN/STATUS/PLANNING и локальные планы/статусы Core/Cobalt/Enerit/Mithril/Alloy. Корневой README сохраняет английский язык и расширен: назначение,14существующих module/runtime/generator проектов в8областях, зависимости, build/test команды из src, headless и standalone UI quickstart, local NuGet consumer, examples, license, readiness и deployment ограничения. Старые UseSignals/UseSignalService/ListenSignalService отсутствуют в текущем source API; README теперь указывает Enerit UseInvocationService/ListenInvocationService/PipesServiceTransport и builder-level Microsoft DI selection. Существующий MithrilTest приведён ссылкой как composition example с открытым own-process proof, не как подтверждённый запуск.

Добавлен `docs/README.md` с общей/модульной навигацией и практическими guides. В PLAN текущий порядок сведён в раздел развития; дублирующие UI записи убраны из общей release criteria section. Согласованные G1–G6/R1–R6, локальные критерии и architecture/dependencies не изменены. PLANNING уточняет роль root README/index как навигации, не второго roadmap. Общий и UI STATUS отделяют текущую документационную сверку от предыдущих package/native/test runs.

Проверки:

- Два **точных** fenced C# snippets из нового root README извлечены в isolated projects с собственными build/CPM settings. Headless имеет PackageReference на TrueMoon/Extensions.DependencyInjection, UI дополнительно Hosting. Использованы существующий local feed и version `0.2.2.7-alpha.consumer.20261011065747061` предыдущего UI package run, SDK10.0.401/net10.0/Windows/Debug. Оба `dotnet build` завершились exit0/0warnings/0errors. Это compile proof public API examples, не отдельный новый headless consumer lifecycle/COB4 proof.
- Проверка текущих Markdown документов: **26documents/339local links/76heading links**, problems0; существование локальных file/directory links, heading anchors, balanced code fences, conflict markers/NUL. Исторические модульные HISTORY записи не проверялись как текущие документы, но anchors ссылок на них разрешались. Результаты и exact scope сохранены в `links.json`; добавленные после первого прохода статусы/общая история включены в финальную проверку. `git diff --check` exit0.
- Артефакты ignored `TestResults/Documentation/Readme20261011`: подготовленный README, extracted source/projects, build logs, local settings, link-check script/report и diff-check log. Корневой README записан в явно запрошенный пользовательский файл; другие файлы вне src не менялись.

Приложения из README, unit/full solution tests, pack/publish и native/GPU checks в этом шаге не запускались. Исторические результаты остаются датированными; alpha/NVIDIA K7 blocker и module readiness не переоценивались. Команда `dotnet test` в README сверена с xUnit2/VSTest/global.json; пустая Mithril discovery не объявляется успешным suite. Общий root README включается в некоторые nupkg через Directory.Build.props, но артефакты предыдущего package run не пересобирались ради документационной правки; UI Hosting/renderer имеют самостоятельные PACKAGE.md.


## Сводка до quality matrix — 2026-10-15

Ниже сохранён прежний общий STATUS целиком; это архив результатов, а не новый запуск.

<details>
<summary>Прежняя сводка и датированный журнал</summary>

# TrueMoon: общий статус

Последняя работа — **2026-10-15, Mithril MIT5 operational diagnostics**.
Структурированная process trace через собственный IEventsSource: run/generation/PID,
effective policy/budget/delays, launch/exit/restart/terminal и cleanup outcomes.
Mithril94/94 и Enerit126/126 в каждой Debug/Release:4current TRX/440passed,
Failed0/Skipped0.40restart +18shutdown apphost/published cases и fresh package108/108
прошли, Completed=true;96package cases проверяют payload через App.UseDiagnostics,
12abrupt-parent-exit cases не наблюдают sink убитого parent.8nupkgs/9projects,
negative missing-analyzer CS0234;205package source fingerprints совпали.
Full nonincremental Debug build0errors/1193warnings. Core/Cobalt/generator suites,
UI/native и performance не повторены. [Scope/ограничения](mithril/STATUS.md),
[команды/evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).
MIT5 закрыт в bounded Windows x64/net10.0 same-build/cooperative scope. Далее
сводная quality matrix модулей и оставшийся UI acceptance перед выбором ENG0;
новые engine/editor проекты этим шагом не создаются.

Предыдущая работа — **2026-10-15, Enerit ENE5 measured pipes baseline**.
10typed payload/concurrency cases,10verify +30fresh client/server pairs,
90samples/571944RPC;104source/10binary/4generated fingerprints совпали, power plan
не менялся. [Числа/ограничения](enerit/BASELINE.md),
[команды/scope](enerit/HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Scalar/c1: median RTT P50183.9µs,4896calls/s,client2512B/call; c8:779.3µs,
9062calls/s,2214B/call. Межзапусковый spread сохранён, не SLA/speedup.
Runtime126/generator114 в каждой Debug/Release,4TRX/480passed/0failed/0skipped.
Full nonincremental Debug build0errors/1193warnings;10дополнительных CS8669 от
generated benchmark не подавлены. Runtime/generator API/logic не менялись;
fresh package108 и собственные Core/Cobalt/Mithril suites/UI/native не повторены.
ENE5 measurement часть закрыта в bounded same-build Windows x64/net10.0 scope;
package portion имеет отдельную прежнюю дату. Далее Mithril MIT5 diagnostics,
allocation/CPU profile Enerit — отдельный measured backlog, engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core headless quality proof**.
Согласованный Core quality проход завершён в bounded Windows x64/net10.0 scope:
configuration/bootstrap, diagnostics и App/JSON/реальный FileStream через текущие
sources и свежие независимые пакеты. [Scope/ограничения](core/STATUS.md),
[команды/результаты](core/HISTORY.md#сквозной-headless-proof--2026-10-15).
52source +80package +3README processes прошли Debug/Release build/publish;
130/127source fingerprints совпали, negative opt-out expected CS0246. Cobalt7
scenarios включает startup+Dispose failure; Microsoft DI6compatibility scenarios
сохраняют native semantics. Core107/107 в каждой Debug/Release, Failed0/Skipped0,
current2TRX/214passed. Full nonincremental Debug build0errors/1183warnings.
Production/runtime API не менялись; другие module suites/IPC/units/UI/native и
performance не повторены. Далее Enerit quality/ENE5, затем Mithril MIT5 diagnostics;
engine ждёт сводки остальных модулей, общий выпуск не закрыт.

Предыдущая работа — **2026-10-15, Core diagnostics quality**.
Исправлены удержание completed DiagnosticListener и старые registrations при
повторном UseDiagnostics. Subscription completion/late handle cleanup, filters,
callback Dispose и независимое освобождение нескольких Apps проверены.
[Контракт](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/scope](core/HISTORY.md#diagnostics-quality--2026-10-15).
Core107/107 в каждой Debug/Release, Failed0/Skipped0, current2TRX/214passed;
diagnostics18cases в каждой конфигурации входят в107. Full nonincremental Debug
solution build exit0/errors0/warnings1183. Windows x64/net10.0/SDK10.0.402.
Другие module suites/package/IPC/units/UI/native/performance не повторены.
Далее цельный headless proof и сводка Core перед Enerit; engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core configuration/bootstrap quality**.
Исправлен GetOrCreate при маскирующем null в именованной секции; новый объект
остаётся доступен повторным чтениям и сохраняется в JSON при JSON source.
Проверены порядок configuration/registrations/factories на Cobalt/Microsoft DI,
validation failure, whole-refresh failure/recovery и JSON serialization/sharing
errors/retry. [Контракт и следующий scope](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/границы](core/HISTORY.md#configuration-и-bootstrap-quality--2026-10-15).
Core97/97 в каждой Debug/Release, Failed0/Skipped0, current2TRX/194passed;
ConfigurationTests27/27 отдельно Debug. Full nonincremental Debug build
exit0/errors0/warnings1184. Windows x64/net10.0/SDK10.0.402. Другие module suites,
package/IPC/units/UI/native/performance не повторены. Core quality частично:
далее diagnostics, затем цельный headless proof; порядок модулей сохраняется.

Предыдущая работа — **2026-10-15, Cobalt COB7: provider comparison**.
Один unchanged App graph проверен на Cobalt/Microsoft DI:20functional scenarios,
20Dry/20paired default measurements и24fresh startup samples, Completed=true.
[Решение и числа](cobalt/COMPARISON.md), [scope/команды](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15).
Cobalt остаётся existing default; **массовая migration explicit DI consumers отложена**.
Регистрация одинакова, Cobalt даёт structured path/unique cleanup, MS дешевле в hot
resolution. Cobalt App lifecycle16.91us против19.32us MS; singleton52.10ns/32B против
14.91ns/0B.127source fingerprints/power plan совпали; Ryzen9 9900X/Windows11 x64/
SDK10.0.402/.NET10.0.13. General release не объявлен.
Cobalt179/generator91/Core81 Release:3TRX/351passed/0failed/0skipped. Full Release
build exit0/errors0/warnings1185. Fresh package64+3, identical-graph consumer
12processes/120scenarios иlibrary/app4/2diagnostic controls прошли. Runtime/Contracts/
generator/adapter logic и provider choices не менялись. Debug unit suites/IPC/units/
UI/native не повторены; UI migration по headless proof не выполняется.
COB6a–d/COB5/COB7 закрыты в bounded scope; далее Core quality.

Предыдущая работа — **2026-10-14, Cobalt COB6b: runtime errors и recovery**.
Cycle/closed type guard, structured missing/depth/stack errors, immutable dependency
path и original factory/constructor exceptions реализованы. Failed/null singleton
retry и completed dependency ownership проверены. [Контракт](cobalt/PLAN.md#cob6b-runtime-errors-и-recovery--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Cobalt84/generator62/Core81 в каждой Debug/Release:6TRX/454passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1 и2constructor negative
builds; opt-out expected CS0246. Full nonincremental Debug build exit0/errors0/
warnings1187. Source manifest127files/mismatches0; packages local/unpublished.
Windows x64/net10.0/SDK10.0.402. Other module suites, IPC/units, UI/native/performance
не повторены. Далее COB6c registration contracts; полный COB6 и migration открыты.

Предыдущая работа — **2026-10-14, Cobalt COB6a: конструкторы и diagnostics**.
Закреплены unique longest public constructor, closed/open nullable/default semantics
и COB001–COB006 на registration call; internal types/factories/required members
проверены. [Контракт](cobalt/PLAN.md#cob6a-конструкторы-и-diagnostics--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).
Generator62/Cobalt37/Core81 в каждой Debug/Release:6TRX/360passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1,2expected negative
constructor builds; opt-out expected CS0246. Full nonincremental Debug build
exit0/errors0/warnings1191. Source manifest127files/mismatches0; packages local.
Windows x64/net10.0/SDK10.0.402. IPC/units, прочие suites, UI/native/performance
не повторены; далее COB6b runtime cycles/error paths, полный COB6 открыт.

Предыдущая работа — **2026-10-14, единый Cobalt package и принятый TrueMoon scope**.
Cobalt runtime/generator остаются двумя projects, поставляются одним nupkg;
analyzer delivery автоматический direct/transitive, opt-out EnableCobaltGenerator=false.
Microsoft DI ecosystem bridge/соглашения не являются целью; существующий adapter
сохраняется. [Контракт](cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14),
[команды/scope](cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).
Fresh headless64/64 +3README, library/app4/4 +legacy1/1, App/IPC/units108/108;
negative controls expected. Cobalt37/generator16/Core81 отдельно Debug/Release:
6TRX/268passed/0failed/0skipped. Full nonincremental Debug build exit0/errors0/
warnings1227. Packages local, не опубликованы; UI/native/performance и остальные
module suites не повторены. Далее COB6a constructor diagnostics/selection.

Предыдущая работа — **2026-10-14, начало качества Cobalt и Try helper generation**.
По решению пользователя до engine/editor идёт качество существующих модулей;
начинаем с Cobalt: COB6 → COB5 → COB7 и условная миграция Microsoft DI consumers.
[Критерии и порядок](PLAN.md#качество-модулей-до-engineeditor--2026-10-14),
[Cobalt contract](cobalt/PLAN.md#качество-cobalt-до-engineeditor--решение2026-10-14).
Исправлена генерация для TrySingleton/TryTransient helpers; generator16/16,
Cobalt37/37 и Core81/81 отдельно Debug/Release — **6TRX/268passed/0failed/0skipped**.
Windows x64/net10.0/SDK10.0.402. Full nonincremental Debug solution build exit0,
errors0/warnings1227. Package/IPC/Mithril/UI/native checks не повторены; прежние package fingerprints
не являются подтверждением изменённого generator. Далее COB6a constructor diagnostics.
[Команды и scope](cobalt/HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).

Предыдущая работа — **2026-10-14, MIT5/ENE5 independent package portion**.
9local packages и независимый PackageReference App/IPC/units consumer:
**108/108cases** Debug/Release ×build/publish, Cobalt/DI и DI без Cobalt analyzer;
Completed=true. Actual package provenance/version,compiler analyzer path/SHA и
runtime output проверены; omitted Enerit analyzer даёт ожидаемый CS0234.
Packages не опубликованы. Source fingerprints current/mismatches0.
Windows x64/net10.0/SDK10.0.402: Enerit126/generator114,Mithril89,Core81,
Cobalt37/generator6 в каждой Debug/Release configuration — **12TRX/906passed**,
Failed0/Skipped0. Full non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[команды и границы](mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14),
[runner](../ManualTests/UnitsPackageConsumer/README.md).
Это package portion; **ENE5 performance и MIT5 operational diagnostics открыты**.
На момент package portion следующий функциональный шаг — фиксированный IPC latency/throughput/allocations
baseline до оптимизаций, затем process diagnostics. Same-build/cooperative scope;
UI/native/MMF/other OS/RID/NativeAOT/stress/uncooperative shutdown не подтверждены.

Предыдущая работа — **2026-10-14, Enerit strict response/decode ownership**.
Generated responses потребляют payload полностью; explicit decode scope возвращает
nested byte rentals при failure, включая DTO dictionary keys и более поздние
arguments. Successful caller/service ownership и DTO prefix API сохраняются.
Empty ack, canonical bool0/1, strict error UTF-8 и invalid dictionary keys проверены.
Windows x64/net10.0/SDK10.0.402: **Enerit126/126, generator114/114, Mithril89/89
отдельно Debug/Release**, Failed0/Skipped0; current6TRX/658passed.
App/IPC/restart **36/36** Cobalt/DI и isolated pipes **12/12** Debug/published
Release повторены, Completed=true; fingerprints current/mismatches0.
Final non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14),
[команды и границы](enerit/HISTORY.md#strict-response-и-decode-ownership--2026-10-14).
Core/Cobalt suites, UI/native/independent packages/performance не повторены;
ранние результаты этого же дня принадлежат предыдущему MIT4 run ниже.
Same-build/cooperative scope, killed parent client cleanup не наблюдаем.
На момент decode hardening далее была **MIT5/ENE5 package composition**, затем отдельный
IPC baseline до оптимизаций; backpressure/schema/MMF/uncooperative shutdown открыты.

Предыдущая работа с кодом и проверками — **2026-10-14, MIT4/ENE4**.
Parent App/AddUnit child, generated typed pipes, same-client reconnect после
bounded OnCrash restart, budget exhaustion, pending Stop/rollback и child cleanup
при abrupt parent exit проверены на Cobalt и Microsoft DI.
Windows x64/net10.0/SDK10.0.402: **Enerit102/102, generator34/34, Mithril89/89,
Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release**, Failed0/Skipped0;
**App matrix36/36 и isolated pipes12/12**, Completed=true.
Final non-incremental Debug solution build exit0/errors0/warnings1226.
[Единый контракт](mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14),
[команды/границы](mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14).
Production исправления Enerit: explicit typed factories/App-local instances,
scoped endpoints, transport Start/Stop rollback; Core test isolation исправлена.
Mithril/Cobalt runtime не изменялись. Same-build/cooperative/ProjectReference scope;
client cleanup killed parent не наблюдаем. UI/native/package/другие OS/stress не повторены.
На момент MIT4 следующим шагом были Enerit response validation/partial nested-buffer
cleanup, затем MIT5/ENE5 independent package composition.

Будущий engine/editor план2026-10-13 сохранён:
[архитектура и этапы](engine/PLAN.md), [статус](engine/STATUS.md).
ENG0–ENG8 запланированы, проектов/реализации и собственных code/process/GPU checks
пока нет. Новый MIT4/ENE4 proof не является реализацией frame transport/hot reload.

Предыдущая работа с кодом и проверками — **2026-10-12, Mithril MIT3**.
Bounded Never/Once/OnCrash/Always, fresh process/control endpoint, budget/delay,
terminal callback, cancel/drain monitor при Stop/Dispose. Windows x64/net10.0:
**Mithril89/89 Debug +89/89 Release**, restart manual40/40; fresh App Never28/28
Cobalt/DI и legacy16/16. Enerit runtime71/71 в обеих configurations.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12),
[команды/границы](mithril/HISTORY.md#mit3-bounded-restart--2026-10-12).
На момент MIT3 следующим шагом был MIT4/ENE4: App/AddUnit + generated Enerit IPC/restart.
External restart/package/другие OS/stress и остальные suites/UI не проверены.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT1/MIT2**.
App-local configurations, main hosting в текущем graph/PID, owned external,
controller rollback/error propagation и cancellable parent/control listener.
Windows x64/net10.0/SDK10.0.401, App lifetime/Never, Cobalt и Microsoft DI:
**Mithril65/65 Debug +65/65 Release**, **App manual28/28**, fresh legacy16/16,
Completed=true; Enerit runtime regression71/71 в каждой configuration.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11),
[команды и ограничения](mithril/HISTORY.md#mit1mit2-app-composition--2026-10-11).
На момент этого run следующим шагом был MIT3, затем MIT4/ENE4 App+IPC.
Другие module suites/UI, Enerit generator/manual и package consumers не повторены.

Предыдущая работа с кодом и проверками — **2026-10-11, Enerit ENE1/ENE2 pipes baseline**.
Runtime **71/71**, generator **34/34** отдельно Debug/Release, Failed0/Skipped0;
own-process apphost matrix **12/12**, Completed=true. Same-build typed payload,
32concurrent, local cancellation/late response, remote errors, disconnect/reconnect,
cooperative shutdown и owned child cleanup проверены на Windows x64/net10.0.
Non-incremental Debug solution build exit0/errors0/warnings1242;
другие модульные suites/UI этим запуском не проверялись.
[Контракт и ограничения](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11),
[команды](enerit/HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11).
App/AddUnit/controller/parent-exit в том pipes run не проверялись; restart/общий IPC и Enerit packages остаются открыты.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT0 own-child baseline**.
Убрана очистка одинаково названных процессов; child start/stop/disposal теперь
освобождают owned Process и ждут exit после принудительного завершения.
**Mithril14/14 Debug и14/14 Release**, Failed0/Skipped0; **manual16/16**
Debug/published Release apphost, Completed=true на Windows x64/net10.0/SDK10.0.401.
Проверен `Never` child-handle scope, App/AddUnit/parent-crash/restart/IPC — открыты.
[Команды и границы](mithril/HISTORY.md#mit0-own-child-baseline--2026-10-11),
[runner](../ManualTests/MithrilLifecycleProbe/README.md).
Другие unit suites/full solution/UI этим запуском не проверялись.

Предыдущая работа с кодом и проверками — **2026-10-11, CORE5/COB4 headless package consumer**.
Проверены **64/64 matrix processes + 3/3 packaged README examples** на local NuGet
в Debug/Release build/publish: Cobalt, Microsoft DI и Microsoft DI без consumer
generator. Actual analyzer path/SHA256/generated sources и отрицательная сборка
без generator подтверждены; metadata и package README дополнены.
Регрессии: **Core81/81, Cobalt37/37, generator6/6**, failed0/skipped0.
[Воспроизведение](../ManualTests/CorePackageConsumer/README.md),
[команды и scope](core/HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).
Full solution, другие модули, UI/IPC/units этим запуском не проверялись.
На момент pipes baseline следующим шагом были Mithril MIT1/MIT2
contracts; Core/Cobalt — COB5 измерения.

Предыдущая UI работа **2026-10-11 — K2/K4 fixed-font golden**: font catalog,
30 reference PNG, UI532/532, golden360/360 Debug/published Release,
Vulkan validation0/0 и negative controls. [Метод](alloy/GOLDEN_TESTS.md),
[scope](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K7/physical DPI/real loss/VRAM остаются открыты; UI проверки не повторены в CORE5/COB4.

Предыдущая документационная работа: **2026-10-11 — общий README и навигация документации**. Корневой README дополнен картой существующих модулей, source build/test командами, headless/UI quickstart, examples, local NuGet delivery и ограничениями. Старый Signals пример заменён текущими API; добавлен [индекс документации](README.md), актуальный порядок собран в PLAN без дублирования в release criteria. Проверены локальные ссылки/anchors и компиляция двух точных README C# snippets на ранее подготовленных локальных пакетах:2builds exit0/0warnings/0errors. Приложения, unit tests, полный solution и native проверки в этом документационном шаге не запускались. [Область сверки](HISTORY.md#общий-readme-и-навигация--2026-10-11).

Предыдущая code/package работа: **2026-10-11 — UI package/independent consumer5.4–5.5**. Исправлена transitive native Skia delivery, подготовлены локальные NuGet artifacts и packaged инструкции.12App/backend +4direct renderer smoke прошли Windows x64/net10.0/SDK10.0.401, Debug/Release ×build/publish, framework-dependent без RID;4consumer builds/4publishes exit0/0warnings/0errors. Это UI поставка на указанной конфигурации, не Core5/COB4 headless/analyzer proof и не готовность alpha. На тот момент следующий независимый UI шаг — fixed-font golden K2/K4; внешний NVIDIA K7 блокер сохраняется. Unit/full solution tests этим шагом не повторялись. [Результаты и ограничения](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

Предыдущая диагностическая работа: **2026-10-10 — UI WSI stages/raw Win32 и driver screen DC**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [UI статус](alloy/STATUS.md), [команды, native stack и scope](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Non-incremental Debug UI consumer build exit0/errors0/warnings780, Release publish exit0; native stage/raw/repeat/Hosting controls повторены в Debug/published Release. Unit tests и full solution build этим diagnostic шагом не повторялись.

UI Hosting использует shared process owner loop; concurrent/cancellation/fault/shutdown checks повторно прошли Debug/published Release, validation0/0. GDI trend связан с первым vkCreateSwapchain на новом HWND/surface; raw repro и debugger фиксируют GetDC(NULL) внутри текущего NVIDIA ICD32.0.16.1742 и HDC без последующего ReleaseDC в bounded lifecycle. Raw24cycles NVIDIA GDI6→29, AMD5→5 в Debug/Release; full UI на AMD не проверен. Production ownership/GPU policy не изменены, safe app-side fix не установлен; G4/K7 остаются открыты. Предыдущий full240-window failed USER26→26/GDI23→137; текущий diagnostic его не повторяет.

Предыдущий dispatcher run: UI suite488/488, solution build0errors/1271warnings, Debug/Release functional/short evidence. [Область и ограничения](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10); его passing results не считаются текущими unit/solution checks.

Предыдущий общий Core/DI/Enerit run2026-10-10: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками. [Результаты и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Эти результаты остальных модулей не являются новым UI run.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Runner/lifecycle, configuration/diagnostics и package consumer | **2026-10-15, COB7 regression:** Core81/81 Release, App comparison на обоих providers. Далее собственный Core quality review |
| [Cobalt / DI](cobalt/STATUS.md) | Собственный API, bundled generator, contracts и paired App comparison | **2026-10-15, COB7:**20measurements/24cold/120package scenarios, Release179+91regressions. Migration отложена, default/adapter сохранены; hot-path backlog отдельно |
| [Enerit](enerit/STATUS.md) | Same-build generated payload contract, TME001/size limits, acknowledged errors/void, concurrent framing и local cancellation/reconnect | **2026-10-14, hardening:** Runtime126/126/generator114/114 Debug/Release, App36/36 и isolated12/12. Strict response/failed nested decode cleanup проверены; Package portion108case проверена; MMF/performance открыты |
| [Mithril](mithril/STATUS.md) | App-local units, owned processes, controller/parent lifecycle и bounded process restart | **2026-10-14:**89/89 Debug/Release regression; App/generated IPC/restart36/36 Cobalt/DI. MIT4 и MIT5 package portion108 проверены; operational diagnostics открыты |
| [Alloy / Argentis](alloy/STATUS.md) | Shared owner, native package delivery, independent consumer, explicit font catalog и30full golden fixtures. Alpha частична | **2026-10-11:** UI532/532, golden360/360 Debug/published Release, validation0/0. Package16processes — предыдущий отдельный run; K7 failed/NVIDIA external blocker, physical DPI/real loss/VRAM сохраняются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

Текущий COB7 run:3TRX/351passed/0failed/0skipped Release,20measurements/24cold,
package64+3/12comparison/4library processes/2controls, full Release build exit0.
Предыдущий COB5:3TRX/351passed,10cases/12cold. Предыдущий COB6d:6TRX/702passed,
package64+3+5/2controls, full Debug build exit0.
Предыдущий COB6c run:6TRX/644passed, package64+3+5/2controls.
Предыдущий COB6b run:6TRX/454passed, package64+3+5/2controls.
Предыдущий COB6a run:6TRX/360passed, package64+3+5 и2controls.
Предыдущий bundled run:6TRX/268passed, package64+3+5 и108.
Предыдущий Try helper run имеет отдельные6TRX/268passed.
Предыдущий package portion run:12TRX/906passed/0failed/0skipped, package108.
Предыдущий decode hardening run:6TRX/658passed, App36 и isolated12.
Предыдущий MIT4/ENE4 run того же дня:12TRX/698passed/0failed/0skipped.
MIT3 restart40/App28/legacy16, MIT0/MIT1/MIT2, headless package64+3, UI532/golden360
и UI package — отдельные предыдущие runs с собственными датами и scope.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Solution baseline2026-10-10 сохранён отдельно; тогда Mithril tests0. Текущий Mithril89+89; final full solution build успешен,12current module TRX отдельно |
| G2. Core и DI | Известные defects исправлены; component и bounded independent package lifecycle на обоих providers проверены. Дополнительные configuration/utility/platform критерии сохраняют свой scope |
| G3. IPC и units | MIT0–MIT3 и ENE1/ENE2 подтверждены ранее; MIT4/ENE4 same-build App/generated pipes/restart проверен2026-10-14. Strict response/nested failure cleanup и bounded independent package delivery проверены; расширенные transport criteria открыты |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | Headless package64+3 исторически проверен; source App/typed IPC/restart36case теперь подтверждён. IPC/units package108 теперь проверен; UI и дополнительные deployment/performance criteria отдельно |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**UI WSI stages2026-10-10:** current non-incremental AlloyVulkanTest Debug consumer build exit0/errors0/warnings780, Release publish exit0. Stage/raw/repeat matrix Debug/published Release: queries без GDI trend, первый swapchain на новой surface/window воспроизводит его; raw teardown orders и thread-exit control проверены. Debug native stacks — screen GetDC(NULL) внутри NVIDIA ICD32.0.16.1742; safe app-side fix не установлен. Hosted dispatcher Debug/Release regression passed/validation0/0. Unit/full solution/full240-window/physical/package checks не повторялись. [Команды и ограничения](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

**UI dispatcher2026-10-10:** Windows/Debug/net10.0/SDK10.0.401; non-incremental `TrueMoon.slnx` build exit0/errors0/warnings1271, UI tests488passed/failed0/skipped0. Release UI consumer publish exit0. Native concurrent/callback/chrome/controlled failure/short cleanup Debug и published Release прошли, validation0/0; два полных240-window прогона **failed** на прежнем GDI limit и предшествуют финальным lifecycle guards. Native24-cycle controls определяют WSI scope, не exact allocator и не VRAM. [Подробности и фактические команды](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Другие модульные tests, physical DPI и package consumers не запускались.

**Итог исправлений Core/DI2026-10-10:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet`: exit0/0errors/1271warnings. Стандартный solution test с no-build/no-restore и отдельная list-tests discovery: exit0. Core81, Cobalt37, Cobalt.Generator6, Enerit30, Enerit.Generator9, Alloy480 — все Passed, Failed0/Skipped0. Mithril TRX total0 не считается passing suite. [Команды, requirement matrix, TRX и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10); локальные детали — [Cobalt](cobalt/HISTORY.md), [Enerit](enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10). Release/pack/own-process/native UI не запускались.

**Предыдущий Core review2026-10-10:** TrueMoon.Tests **Passed3/Failed1/Skipped0**, discovered4; AppCreate падал на `ServiceResolvingException<IAppLifetime>` через чужой LifeTimeExecutor resolver. TrueMoon.Core incremental build exit0/errors0. [Команды, scope и выводы](core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Два passing RunAsync теста тогда содержали Assert.True(true); в текущем исправлении заменены behavioral assertions.

Предыдущая UI investigation: consumer Debug build/published Release, native WGL/thread240-cycle comparisons и shared-loop prototype с двумя live окнами/close/recreate. [Команды и ограничения](alloy/HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Production dispatcher и полный mixed resource result этим шагом не исправлены; unit suite/общий baseline/другие модули той UI работой не запускались заново.

Предыдущая lifecycle работа2026-10-10: UI suite **480passed/0failed/0skipped**, short/callback/retirement proofs и два полных240-window Debug/published Release прогона. Оба полных window resource results **failed** на USER growth при нулевых hooks/registry/subscriptions/retained UI roots. [Команды и ограничения](alloy/HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). Предыдущие TextBox/Name20repeats и backend comparison — [отдельный запуск](alloy/HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10).

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен тем run; Mithril discovery0 подтверждён2026-10-10, текущая suite14+14 добавлена MIT0. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- ENE5 measurements и MIT5 operational diagnostics проверены2026-10-15 в отдельных bounded scopes; fresh package108 повторён для MIT5 с проверкой diagnostics. Это не общий platform/release acceptance и не новый performance run.
- G2 headless package/analyzer consumer подтверждён предыдущим CORE5/COB4 run. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: предыдущий full check **failed** USER26→26/GDI23→137. Текущий native trace локализует screen HDC allocation/lifetime до первого swapchain в NVIDIA ICD32.0.16.1742; raw AMD control не воспроизводит GDI trend, но full UI на AMD не проверен. Safe app-side fix не установлен; supported NVIDIA fix или полностью проверенная alternate configuration, затем full matrix с прежними limits. Private/handles и VRAM остаются; G4 не закрыт.
- Предыдущие same-build App/process/IPC/restart и package proofs не подтверждают automatically other OS/RID, uncooperative shutdown или UI/engine workloads; scope остаётся в локальных STATUS/HISTORY.

## Следующий конкретный шаг

1. Свести bounded quality Core/Cobalt/Enerit/Mithril и оставшийся UI acceptance в общую матрицу перед выбором ENG0. MIT5 diagnostics/package проверены; остальные module/package/platform criteria сохраняют собственный scope.
2. Cobalt hot-path allocation investigation и Enerit scalar c1/c8 allocation/CPU profile — отдельные measured backlog задачи перед оптимизацией. Cobalt default/adapter и отложенная migration прежние. Engine/editor ждёт сводки рабочего уровня остальных модулей.
3. UI: fixed-font golden K2/K4 проверен2026-10-11 в Debug/published Release; следующий [локальный scope](alloy/STATUS.md#следующий-конкретный-шаг) — оставшаяся physical4.9/K3, real loss/K8/VRAM и итоговая matrix. Independent package consumer5.4–5.5 — предыдущий отдельный run, новые font overloads через NuGet ещё не проверены. По решению пользователя NVIDIA GDI defect — внешний K7 блокер; поиск app-side workaround пока прекращён. Возврат при supported NVIDIA fix или полностью проверенной alternate GPU configuration, затем full check с прежними limits; private/handles отдельно. Alpha acceptance сохраняется; пользовательские драйвер/Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.

</details>

## Матрица качества модулей — 2026-10-15

По запросу следующего шага после MIT5 сведены общие PLAN/STATUS/PLANNING,
локальные Core/Cobalt/Enerit/Mithril/Alloy PLAN/STATUS и будущее engine направление.
[Результат](QUALITY.md): bounded проход первых четырёх модулей завершён в собственных
scopes; UI alpha остаётся частичной. Сравнение не складывает разные предыдущие runs
в current full-solution pass. Даты собственных suites, отдельных package и perf
runs сохранены; в частности COB7 own runtime/generator179/91 — Release,
Core107 Debug/Release — headless proof, Enerit114generator — ENE5 baseline,
Mithril94/Enerit126 и package108 — более поздний MIT5 run того же дня.

Исходная сверка AlloyPackageConsumer Program/csproj/runner и Hosting AlloyOptions
подтвердила пробел acceptance: consumer выбирает только UseDI и overloads без
explicit SkiaFontCatalog, runner не сравнивает source SHA в конце. Новый runtime
failure не воспроизводился. Следующий code scope уточнён только в
[плане владельца UI](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15):
fresh independent package/Hosting proof на Cobalt/DI, bundled analyzer, fonts/native
provenance, direct renderer и bounded lifecycle. Это дополнение existing consumer,
без массовой migration других UseDI apps и без нового engine проекта.

NVIDIA K7 и решение пользователя остановить workaround search, physical DPI,
real loss/VRAM/final UI matrix сохранены. Scope quality работы не требует закрыть
все будущие PA1–PA10/DI scopes/MMF/Hot Reload пункты; выбор ограниченного ENG0
прототипа требует явного рассмотрения оставшегося UI риска. Game scenario/API
сверка не выбрала за пользователя. Cobalt/Enerit measured allocation backlog
сохраняется отдельно от функционального порядка работы.

Общий STATUS сокращён до актуальной сводки; полный прежний текст сохранён в
[архиве](#сводка-до-quality-matrix--2026-10-15). Local STATUS/навигация синхронизированы:
убраны устаревшие следующие шаги Core/Enerit/Mithril. В Cobalt PLAN исправлена
старая фраза two-package на действующий один consumer package. Existing module
contracts, общие G1–G6/R1–R6, UI K1–K9/PA1–PA10 и engine ENG0–ENG8 не отменены.

Code/build/tests/pack/publish/process/GPU/performance в этом документационном шаге
не запускались. Артефакты сверки — ignored `TestResults/QualityReview/run-20261015-135635200`:
STATUS.before.md, runtime-sources.before.json и последующий отчёт проверки документов.
Изменений runtime/project files, системных настроек, commits/push нет.

Фактическая проверка документов: `VerifyReview.ps1`/`review.json` в указанном artifact
directory —29Markdown documents/623local links/202heading anchors,0problems;
balanced fences/conflict markers/NUL проверены. Прежний STATUS из347строк сохранён
verbatim в архиве, новая текущая сводка84строки.299runtime/project files совпали
по SHA256 с начальным snapshot; K1–K9 и PA1–PA10 rows UI PLAN/STATUS не изменены
относительно исходного tracked UI baseline. Scoped git diff --check passed.
Это documentation validation, не новый module regression/build/native pass.

## ENG2 CPU viewport — 2026-10-17

[Engine scope/layout](engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17) и
[evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17):3slot CPU ring,
bounded receiver и одно Alloy/OpenGL окно показывают actual child frames.
Owned Skia BGRA image factory отдельно отражена в UI STATUS/HISTORY.
Native34/34 Debug apphost/published Release, по20Start/Stop, resized pixels,
EOF/deadline/native close/reap; engine87/UI570 Debug/Release, full Debug1368/1368.
Nonincremental Debug0errors/1211warnings. Source323/binary76fingerprints
и final staged/links/diff guard — TestResults/EngineSessions/eng2-completion.json.
Следующий этап ENG3 compiler/checkpoint/versioned replacement/rollback;20Replace
не выполнялись. UI hardware blockers и отложенный RTE backlog не менялись;
новых packages, shared GPU или performance baseline нет. Full Release solution
suite и UI package/golden/RichTextEditor native matrix не запускались.

## UI INF0/INF1 registry cleanup — 2026-10-18

После сохранения дополнительного INF roadmap пользователь выбрал рекомендуемый
UI шаг INF0→INF1. Исправлен repeat cleanup HostedUiSessionFactory после Update/
borrowed/owner/tree guard failure, Creation fence и causes preservation сохранены.
[Подробный contract/evidence](alloy/HISTORY.md#inf0inf1-повторяемый-cleanup-registry--2026-10-18).
Новый TrueMoon.Alloy.Hosting.Tests с production ProjectReference добавлен в
TrueMoon.slnx, source-linked UI tests не заменялись.14/14 Debug/Release;
full Debug solution1382/1382,9TRX/failed0/skipped0, nonincremental Debug build
0errors/1211warnings. Два bounded ENG2 OpenGL viewport runs Debug apphost/published
Release прошли: по25child launches/20Start-Stop, sessions0/mappings0/child cleanup.
Это не повтор32ENG1 headless cases, full Release solution, package/golden/RTE native
matrix, performance/VRAM или hardware acceptance. Следующий UI этап INF2,
engine продолжение ENG3 и отложенный RTE backlog сохраняются.
Новых пакетов/ABI/production dependencies нет, commit/push не выполнялись.

## Сводки до UI PA8a — 2026-10-22

Последняя работа — **2026-10-22, Aether ENG5c.2 reach/raycast/interaction**:
nearest affine geometry query, opaque occlusion и independent object switches,
TMCK2 atomic state restore реализованы. Control3/Frame1/Snapshot2 требуют rebuild;
full World/edit не заявлен.52новых cases, full Debug/Release2186/configuration,
9TRX/failed0/skipped0; Aether780/UI615/Hosting79/Mithril95.
Fresh native60/60cases/62commands Debug/published Release: interaction18PNG,
прежние CPU/GPU/input/Editor/Replace/failures; core/sync0/0/zero ownership.
Diagnostic JSON compatibility regression исправлена, полная матрица повторена.
Local Aether pack/DLL SHA совпал с final managed/native proof; package drawing
не запускался. Completion report Passed=true.
[Контракт/evidence](engine/HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Следующий шаг — [ENG5c.3 collisions камеры](engine/PLAN.md#eng5c3-следующий-шаг-collisions-камеры),
затем HUD; MCP/full snapshot/edit/raw input/3D performance/hardware gates открыты.

Предыдущая работа — **2026-10-22, Aether ENG5c.1 scene consumers/multiple primitives**:
standard v1 component roles, parent world transforms и статичная authoring-комната
подключены к отдельному runtime/многопримитивному Vulkan renderer через IPC.
Control v3 требует совместного rebuild, frame1/TMCK1 прежние; full world restore
не заявлен. 36новых cases; full Debug/Release2134/2134/configuration,
9TRX/failed0/skipped0; Aether728/UI615/Hosting79/Mithril95.
Fresh native58/58cases Debug/published Release, room по5PNG и прежние CPU/GPU/
input/Replace/failures; core/sync0/0, zero owned resources/imported readback.
Local Aether pack/DLL payload hash прошли; package drawing consumer не запускался.
[Контракт/evidence](engine/HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
На этапе ENG5c.1 следующий шаг — [ENG5c.2 reach/raycast/interaction](engine/PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
затем collisions/HUD. Full runtime snapshot/edit, MCP,3D performance и прежние
hardware/release блокеры остаются открытыми.

Предыдущая работа — **2026-10-22, Aether ARCH1a.2 scene aggregate и adapter**:
immutable SceneDocument/ResolvedSceneSnapshot, parent graph, version metadata и
explicit projection в прежний SceneSnapshot реализованы;52новых cases, scene suite130.
Full Debug/Release2098/2098/configuration,9TRX/failed0/skipped0; Aether692/UI615/Hosting79/Mithril95.
[Контракт/evidence/границы](engine/HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
Прежние control/frame/TMCK schemas и интерактивный sample не изменены;
native/GPU/package checks на этапе ARCH1a.2 не повторялись. Тогда следующим был [ENG5c.1](engine/PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22):
component roles, runtime scene consumer и несколько примитивов. Общий tooling API,
full runtime snapshot/edit и MCP запланированы; прежние release блокеры сохранены.

Предыдущая работа — **2026-10-22, ARCH1a.1**: immutable model/EntityResolver,
78новых cases; full Debug/Release2046/2046/configuration, Aether640.
[Scope/evidence](engine/HISTORY.md#arch1a1-immutable-модель-и-resolver--2026-10-22).

Предыдущая работа над реализацией — **2026-10-21, ENG5b input/camera**: control wire v2,
optional fixed-tick gameplay input, camera/checkpoint/Replace и интерактивный
GPU sample `--scene`. UI keys/mapping дополнены. Full Debug/Release1968/1968
в каждой configuration,9TRX/failed0/skipped0; Aether562/UI615/Hosting79/Mithril95.
Полная native regression56/56 и отдельный усиленный input proof2/2 по15cases/5PNG,
validation0/0/zero ownership. Три local packs/payload совпали с published DLLs;
отдельный package drawing не запускался. [Scope/commands/provenance](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
На момент ENG5b далее планировался ENG5c. Raw cursor/physical input/3D performance
и прежние UI hardware/release ограничения остаются открытыми.

Предыдущая работа — **2026-10-21, ENG5a Vulkan3D primitive renderer**:
immutable scene/camera/material, depth-tested cube в App-owned GPU viewport,
без CPU pixels/staging. Debug/published Release: по5PNG assertions, resize/
suspend/resume/checkpoint Replace, validation0/0 и zero owned resources после
teardown. Full solution Debug1926/1926 иRelease1926/1926, по9TRX/failed0/skipped0;
Aether520/UI615/Hosting79/Mithril95. [Контракт, commands и scope](engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
Native matrix12/12cases/14commands,430source/76binary hashes; local Aether pack
и payload SHA256 check прошли, отдельный package drawing consumer не запускался.
Далее ENG5b input/camera; весь ENG5 и3D performance ещё открыты.

Предыдущая работа — **2026-10-21, ENG4e и Mithril graceful-stop fix**:
CPU/GPU baseline12cases/36intervals/24cooperative children,421source/94binary
fingerprints; raw captures и frozen budgets сохранены. Release1280×720 median
per-run p95: CPU65–66ms/GPU21–33ms; reference GPU allocation~107MiB/s.
Debug CPU имеет2FPS/6latency advisory breaches, budgets не ослаблены.
Исправлена гонка error delivery/remaining graceful wait; red/green и native20
Mithril shutdown cases прошли. Отдельный GPU validation run6cases прошёл.
Свежие units packages:108/108cases/8nupkgs; expected analyzer negative control.
Full solution Debug1907/1907 иRelease1907/1907, failed0/skipped0, по9TRX.
[Метрики, команды, failures и ограничения](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Следующий функциональный шаг — ENG5; UI PA8a/RTE и прежние hardware/release
ограничения сохраняются. Reference timings не доказывают будущий3D performance.

Предыдущая работа — **2026-10-21, исправление структуры проектов**:44проекта/
597C#files, namespace/category violations0. Типы сгруппированы по назначению,
публичные namespace и consumers обновлены; общий Contracts остаётся независимым.
Full Debug/Release1898/1898 в каждой конфигурации, failed0/skipped0; build0errors/
1499warnings. Fresh Core80+3README/units108/UI16source+8package и36+4smoke/Aether4
App consumers прошли. Итоговая targeted native engine matrix20cases прошла;
выявленный lookup исторических leases по повторно использованному PID исправлен
на прямую ссылку текущего owned lease. [Миграция, команды, повторы и ограничения](HISTORY.md#исправление-структуры-проектов--2026-10-21).
На момент этой работы следующим был ENG4e; прежние UI hardware/release ограничения
сохраняются. Native probe теперь сохраняет исходные причины до assertion.

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. Общий
transport/presentation contract подключён к Start/selected DLL/Build/Replace/
checkpoint/rollback; explicit GPU сохраняет обычный View/commands, CPU default.
Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,9TRX/
failed0/skipped0. Build0errors/1499warnings; fresh12packages/4App consumers.
Native54cases/56commands,406source/76binary fingerprints; per GPU Editor20Replace/20failures/53children, validation0/0, imported readback0.
[Команды/границы](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед 3D. K3/K7/K8/VRAM/final alpha
сохраняются; drivers/Windows settings/K7 stop policy прежние.

## Сводка PA8a перед ENG7a — 2026-10-22

Последняя работа — **2026-10-22, UI PA8a дефолтный квадратный стиль** по запросу
пользователя: Dark/Light controls, focus/error/read-only/disabled states,
scrollbar и стандартное borderless оформление через App/Hosting реализованы.
[API и галерея](alloy/DEFAULT_STYLE.md), [результаты/ограничения](alloy/STATUS.md),
[подробный журнал](alloy/HISTORY.md#pa8a-дефолтный-квадратный-стиль--2026-10-22).

Текущий full solution run: **2214/2214 Debug и2214/2214 Release**,9TRX в каждой
конфигурации, failed/skipped0; UI643/Hosting79/Aether780. UI golden360, новая рамка
8native cases, source integration16 и fresh packages36App+4direct прошли.
Это UI проверка; прежний engine native60/60 не повторялся. PA8a0–2 и automatic
PA8a3 выполнены, physical DPI/OS input/K3/K7/K8/VRAM/final alpha остаются открытыми.
Engine точка продолжения — ENG5c.3 collisions камеры, затем HUD. Предыдущие сводки
[сохранены в HISTORY](HISTORY.md#сводки-до-ui-pa8a--2026-10-22).

<a id="status-before-planning-revision-2026-11-05"></a>
## Архив STATUS перед ревизией — 2026-11-05

Полный снимок до ревизии очереди после db5ea97. Даты и scope внутри принадлежат прежним работам; это не новые запуски. SHA256 исходного текста: `21ADD0F0B573140B1B65E76C5F6A6EEA9B916CEDF56D18C1688285A82C4BFB4A`.

<details>
<summary>Прежний STATUS целиком</summary>

# TrueMoon: общий статус

Последняя работа — **2026-11-05, native numeric acceptance**.
[ED4g.2a](engine/EDITOR_PLAN.md#ed4g2a-native-numeric): четыре source Debug/published
Release OpenGL smoke прошли в standard/Cobalt и embedded/MS DI. Windows linked
drag, Esc draft cancel, Ctrl+Enter и Undo подтверждены в standard; routed arrows/
captured Esc — во всех четырёх. Исправлен native pointer position/modifiers.
Свежие full Alloy1101/1101, Hosting154/154, Editor1295/1295 каждый Debug/Release,
failed/skipped0; финальные UI/GPU/handles0. Далее physical arrows/captured Esc/
pointer modifiers, затем group reset/angle units. Fresh package/Vulkan/device-loss
и полный solution test run не повторялись; Observer follow-up отдельно.


Предыдущая работа — **2026-11-05, числовые жесты/шаг и связанный масштаб**.
[ED4g.2](engine/EDITOR_PLAN.md#ed4g2-numeric-gestures) / UIA3c реализуют typed stepping,
pointer capture/cancel и proportional XYZ без auto Apply. Свежие full Editor1295/1295,
Alloy1095/1095, Hosting154/154 прошли каждый Debug/Release, failed/skipped0;
64новых cases. Focused Inspector180/180 и reviewed controls40/40 Debug прошли.
Итоговый nonincremental Debug build0errors/1555warnings; full solution tests нет.
11CPU raster previews проверены отдельно от native/GPU/package/physical gates.
Далее source/published native gesture proof; Observer optimization follow-up отдельно.


Предыдущая работа — **2026-11-05, оформление остальных редакторов инспектора**.
[ED4g.1](engine/EDITOR_PLAN.md#ed4g1-generic-editors) и UIA3b применяют общий
[дизайн-код](alloy/DESIGN.md) к numeric/boolean/string/nullable и их состояниям.
Свежие full Editor1271/1271, Alloy1055/1055, Hosting154/154 прошли каждый Debug/Release,
failed/skipped0;50новых cases. Focused Inspector156/156 Debug и общий nonincremental
Debug build0errors/1555warnings; полного solution test run нет.14CPU raster previews
проверены отдельно от native/GPU/package/physical gates. Numeric gestures/step/
linked scale и дальнейшая оптимизация Observer сохраняются отдельными задачами.


Предыдущая работа — **2026-11-05, компактный Transform и дизайн-код UI**.
[ED4g](engine/EDITOR_PLAN.md#ed4g-compact-transform) использует три адаптивных строки,
сохраняет drafts/history и раскрывает metadata/reset через «⋯». Общие controls,
семантические цвета и состояния границ принадлежат [UIA3/дизайн-коду](alloy/DESIGN.md).
Свежие full Editor1256/1256, Alloy1020/1020, Hosting154/154 прошли каждый Debug/Release,
failed/skipped0. CPU-raster previews проверены отдельно от native/GPU/package gates.
Numeric gestures/step/linked scale — следующий UI срез; дальнейшая оптимизация
Observer остаётся отдельной engine-задачей.


Предыдущая работа — **2026-11-04, native open batch4 Observer (ED6b.2e.4e)**.
Bounded opens для default graphs от256files, прежний full hashing/error order/
cancel/drain.21новый case: full Editor1244/1244 Debug/Release +final focused145/145
в обеих конфигурациях прошли.6/6cases,24Same/6Changed, budget0/owners drained.
2567files scan upper335/470ms вместо545/602ms; цена alloc+23%, CPU/cycle+23–43%.
Это выигрыш latency с дополнительными CPU/allocations, не uniform cost reduction.
3profiles/targets/collectors прошли,12Same/3Changed; batch frames подтверждены только
many-files.56collection inputs/10827fixtures/6traces проверены, corrected analysis
revision отдельно, drift0. Общая nonincremental Debug сборка0errors/1555warnings,
full solution tests не повторены. Далее — снижать batch scheduling/allocation cost.
[Методика/границы](engine/HISTORY.md#ed6b2e4e-observer-open-batches-2026-11-04).

Предыдущая работа — **2026-11-04, opened metadata и native path Observer (ED6b.2e.4d)**.
Basic/size queries +prepared name/fresh final metadata реализованы.8новых cases,
full Editor1223/1223 Debug/Release, failed/skipped0.6/6performance cases,24Same/
6Changed, budget0/owners drained.2567files: allocation−10%, OtherOps16685vs21819;
polling scan545msvs614ms median upper bound, notifications602msvs593ms. Универсальный
latency/CPU speedup не заявляется.53inputs drift0.
3fresh profiles: open+handle metadata остаётся главным resolved cost57.9–58.9%
census sampled thread weight, native/OS attribution требует отдельного trace.
55input/10827fixture/6trace hashes drift0,36Same/9Changed matrix+profiles; общая
nonincremental Debug сборка0errors/1555warnings, full solution tests не повторены.
[Результаты/границы](engine/HISTORY.md#ed6b2e4d-observer-native-open-2026-11-04).

Предыдущая работа — **2026-11-04, оптимизация file IO/metadata Observer (ED6b.2e.4c)**.
Native owned reader/opened metadata/fresh final path query и probes без missing
exceptions реализованы с прежними full hashing/cancel/drain fences.41новый case,
full Editor1215/1215 Debug/Release, failed/skipped0.6/6performance cases:
24Same/6content changes, budget0/owners drained;2567files scan0.56–0.61s вместо
0.82–0.85s, allocation4.08vs4.95MiB/cycle.53inputs без drift. Control CPU не весь
улучшился; whole-process sampled/warmed-cache метрики не являются гарантией.
3fresh profiles подтвердили сокращение видимого file/probe metadata cost;
следующий шаг — native/OS attribution оставшегося Open+handle metadata.55input/
10827fixture/6trace hashes drift0,36Same/9Changed matrix+profiles, budget0.
Общая nonincremental Debug сборка0errors/1555warnings; полный solution test run
и UI/GPU/package/hardware acceptance не повторялись.
[Подробные результаты/границы](engine/HISTORY.md#ed6b2e4c-observer-file-io-2026-11-04).

Предыдущая работа — **2026-11-04, кэш ancestor checks Observer (ED6b.2e.4b)**.
Bounded per-attempt positive cache с native directory pins/fresh fallback/final
revalidation реализован.48новых cases; full Editor1174/1174 Debug иRelease,
failed/skipped0. Повторные6/6cases:24full-read Same/6same-length+mtime Changed,
budget0/Stopped/watchers0.2567files: median scan0.82–0.85s вместо1.27–1.52s,
allocation4.95vs6.75MiB/cycle;52current input hashes без drift.
3fresh profiles подтвердили ancestor attributes40–42% →0.67–1.07% census thread
weight. Далее — attribution per-file open и file/probe metadata с учётом unresolved
native context.54input/10827fixture/6trace hashes drift0. Общая nonincremental Debug
сборка:0errors/1555warnings; полный solution test run не повторялся.
Метрики whole-process/warmed cache, не гарантия для всех machines/cases.
[Контракт/замеры/проверки](engine/HISTORY.md#ed6b2e4b-observer-directory-cache-2026-11-04).
UI/GPU/package/hardware/release gates сохраняют собственный scope.

Предыдущая работа — **2026-11-04, найден конкретный hot path Observer (ED6b.2e.4a)**.
3EventPipe profiles на неизменённом Release baseline: ValidatePath → ancestor
Attributes39.95–41.90% веса census stacks в2many-files repeats; вместе с остальными
file/probe metadata60.47–60.62%. Loop model даёт26203ancestor checks на103distinct
directories за clean pass.3targets/collectors exit0,12Same/3Changed, budget0,
Stopped/watchers0,48baseline hashes без drift. [Границы/команды/evidence](engine/HISTORY.md#ed6b2e4a-observer-profile-2026-11-04).
Далее — bounded reuse ancestor validation с safety proof при replacement/reparse/
access errors и прежними content/membership/probe/drain fences. Production optimization,
kernel attribution и новые unit/full solution/native UI/GPU/package runs пока не выполнены.

Предыдущая работа — **2026-11-04, стоимость Observer на больших local graphs (ED6b.2e.4)**.
При default polling10s/budget5s шесть Release headless cases и24full Same passes
прошли без budget expiry. Actual2567files/11.11MiB требуют1.27–1.52s на pass;
139files/129.11MiB —0.156–0.182s. Notifications дают detection1.58s/0.429s,
polling11.37s/10.18s.6owners drained/watchers0,48source/DLL и5554fixture hashes drift0.
Production code/limits прежние; CPU/allocations/memory process-wide, warmed cache.
[Протокол/метрики/ограничения](engine/HISTORY.md#ed6b2e4-observer-performance-2026-11-04).
Далее — attribution metadata/path checks на many-files graph и подтверждённая
оптимизация. Новых unit/full solution/native UI/GPU/package runs в этом lane нет;
остальные module/hardware/release gates сохраняют собственный scope.

Предыдущая работа — **2026-11-04, Window интегрирован в standard Editor (ED4f.3)**.
Typed Window root содержит reusable EditorView; сохраняет App/DI ownership,
настройки окна, header/caption и прежний document close/Ready path. Full Editor1126,
Alloy976 и Hosting154 прошли отдельно Debug/Release;64новых Editor cases.
Fresh15packages/headless8/8,6source/published/fresh NuGet native workflows и
12startup failure/cancel cases прошли;84children released,78graceful Vulkan
validation0/0, final UI/GPU/handles0. UIA2d targeted independent delivery profile
подтверждён через Editor. [Результаты/границы](engine/HISTORY.md#ed4f3-window-root-2026-11-04).
Core/headless/offscreen/HUD и Player scope прежние; основной следующий Aether шаг —
measured observer cost10s/5s, wider hardware/release gates остаются открытыми.

Предыдущая работа — **2026-11-03, сверка Window PLAN/STATUS и engine integration**.
По сохранённым результатам UIA2a–c реализованы/проверены в bounded scope, UIA2d
independent package consumer открыт. Рекомендуемый standard Editor consumer —
[ED4f.3](engine/EDITOR_PLAN.md#ed4f3-window-root): отдельный Window root, reusable
EditorView и прежний document close owner. Core/offscreen/HUD не получают Window;
Player решает host composition в ENG7b.2. [Сверка](engine/HISTORY.md#window-engine-integration-review-2026-11-03).
Это документационная работа: новые C# changes/build/test/native не выполнялись;
основной следующий Aether шаг — measured observer cost. Старые next-step записи
про уже завершённые reuse/Replace уточнены, остальные gates сохранены.

Предыдущая code работа — **2026-11-03, UIA2 Window в Argentis/Hosting**: provider-neutral
root component и App/window binding реализованы. Full Alloy976/Hosting154/Editor1062
отдельно Debug/Release прошли; solution Debug cached-assets build,2producer packages
и source/published native Window +Cobalt/MS DI factory proof выполнены.
[Результаты/ограничения](alloy/HISTORY.md#uia2-window-component--2026-11-03).
Validation layer недоступен; independent package consumer и hardware/release gates
не закрыты. Очередь Aether/source observer measured cost этим не меняется.

Предыдущая работа — **2026-11-03, ED6b.2e.2–3 Observer/source UI**.
Project-owned inert/activated observer, bounded census/hints/polling/drain и retained
status indicator реализованы. Source state отделён от scene/runtime; preflight и
existing ownership/admission fences сохранены. Full Editor1062/1062 Debug/Release
прошёл (84новых cases); source Release2/2 +fresh package Release2/2 прошли,
15packages/headless8/8,4game children reaped/released и Vulkan validation0/0.
[Срез/команды/evidence](engine/HISTORY.md#ed6b2e23-observer-и-source-ui--2026-11-03).
Далее measured observer cost на большом local graph; wider profiles и остальные
module/hardware/release gates прежние.

Предыдущая работа — **2026-11-02, ED6b.2e.1 descriptor и probes**.
Capture-derived immutable source descriptor, exact present/absent probes и общий
compiler exclusion predicate реализованы. Full input identity, schemas и обязательный
preflight сохранены. Final Aether26/26 (22новых cases +4integration) и full
Editor978/978 прошли Debug/Release; fresh15packages/headless8/8 без source drift.
[Реализация/проверки](engine/HISTORY.md#ed6b2e1-descriptor-и-probes--2026-11-02).
Далее ED6b.2e.2 project-owned observer, затем UI indicator; hardware/release gates
прочих направлений прежние.

Предыдущая работа — **2026-11-02, ED6b.2e contract source watcher/stale Build**.
Определены наблюдаемый source descriptor, state/identity rules, project-owned lifetime,
bounded hints/polling/debounce и UI snapshot delivery без auto Build/Play. Compiler
preflight остаётся обязательным. [Contract/этапы](engine/EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).
В том срезе только source review и документация, C# API/observer/indicator не добавлены;
build/test/native не запускались. Contract определил ED6b.2e.1 descriptor/probes/shared policy,
затем observer и UI acceptance. Hardware/release gates и прочие направления прежние.

Предыдущая работа — **2026-11-02, ED6b.2b.2c.1 native package interrupts/preflight**.
Source/fresh NuGet Release ×standard/Cobalt и embedded/MS DI52/52native cases:
36interrupts +16stale/corrupt preflights перед Start/Replace.76Game children
reaped/released,76graceful validation0/0, maxLive1, final UI/GPU/handles0;
4recoveries только Restore+Cancel. Fresh15packages/headless8/8,2743hashes без drift.
Production runtime/API, unit/full solution и ordinary Workflow matrix не менялись/
не повторялись. [Contract/commands/evidence](engine/HISTORY.md#ed6b2b2c1-native-package-interrupts-и-preflight--2026-11-02).
Далее bounded source watcher/stale Build status без auto Build/Play; wider packages,
HUD/input/Player и physical/hardware/performance/release gates остаются отдельными.

Предыдущая работа — **2026-11-02, ED6b.2b.2c captured managed packages**.
Реализован явный local PackageDirectory + locked unsigned managed closure до SDK,
без package build/analyzer/native assets, с прежними reuse/prestart/ownership fences.
Project manifest v3 добавляет contained packageDirectory; v1/v2 совместимы.
Final package33/full Editor978 прошли Debug/Release, failed/skipped0; SDK lock входит
в package cases. Scoped293прошли до последнего nuspec guard, покрытого final package33.
Fresh15packages/headless8/8 и source/NuGet Release ×standard/Cobalt +embedded/MS DI
4/4native Workflow прошли:56children reaped/released,52graceful validation0/0,
20Game с package DLL только из selected artifact, UI/GPU/handles0,2059hashes без drift.
Далее directed native package interrupts/stale-archive cases; full solution и
hardware/performance/release gates не повторялись.
[Контракт/команды/результаты](engine/HISTORY.md#ed6b2b2c-captured-managed-packages--2026-11-02).

Предыдущая работа — **2026-11-02, ED6b.2b.2b.1 native graph interrupts**.
Source/fresh NuGet standard/Cobalt +embedded/MS DI36/36native cases: Cancel/Stop/Exit
в dependency SDK, graph verify и restore.52Game children reaped/released,52graceful
validation0/0, UI/GPU/handles0;4recoveries только Restore+Cancel, late child после
Stop/Exit нет. Fresh15packages/headless8/8,2735hash checks без drift.
[Contract/matrix/evidence](engine/HISTORY.md#ed6b2b2b1-native-graph-interrupts--2026-11-02).
Production C#/unit/full solution и ordinary Workflow matrix не менялись/не повторялись.
Далее — locked managed PackageReference closure без build/analyzer/native assets;
hardware/performance/release gates сохраняются.

Предыдущий graph profile и262/971 Debug/Release —
[в истории](engine/HISTORY.md#ed6b2b2b-captured-managed-project-graph--2026-11-02).

Предыдущий ED6b.2d.1 Stop/exit order и single-project native interrupt proof —
[в истории](engine/HISTORY.md#ed6b2d1-native-replace-interrupts--2026-11-02).

Предыдущая работа — **2026-11-01, ED6b.2d document-aware code Replace**.
Fresh15packages/headless8/8 и published source/NuGet standard/Cobalt +embedded/MS DI
4/4workflows:56children reaped/released,52graceful validation0/0, UI/GPU/handles0,
8new PNG просмотрены. Scoped Aether82/82 и full Editor957/957 Debug/Release; full
Aether/Alloy/Hosting/solution не повторены. Code swap сохраняет paused tick; compiler failure оставляет old child,
restore rejection возвращает old code/state/Playing mode. Authoring scene неизменна.
Следующий шаг — directed native Cancel/Stop/exit в середине Replace;
native Release workflow не закрывает physical/hardware/general release gates.
[Контракт, проверки и границы](engine/HISTORY.md#ed6b2d-document-aware-code-replace--2026-11-01).

Предыдущая реализация — **2026-10-31, ED6b.2b.2a declarative preparation closure**.
Scene Start фиксирует поддержанный project/import/source/reference graph до первого
SDK process. Windows SDK/runtime files read-leased; environment/home/temp/NuGet config
явные. Custom tasks/functions/SDK и package/project graphs отклоняются до execution.
Original paths и repeatable compiler bytes сохранены; legacy flags default false.
Aether1704/1704 и Editor943/943 отдельно Debug/Release, failed/skipped0. Другие module
suites и full solution не повторены; прежние failures сохранены в HISTORY.
Fresh15packages/headless8/8, exit0; native/window/GPU runs не повторены.
[Контракт и проверки](engine/HISTORY.md#ed6b2b2a-declarative-preparation-closure--2026-10-31).
Дальше — consumer acceptance и стоимость freeze; native/hardware/release gates,
расширенные profiles и artifact reuse/Replace этим не закрываются.

Предыдущая реализация — **2026-10-31, ED6b.2b.1 захваченная C#-компиляция**.
Scene Start собирает из owned copies source/generated/reference/config/compiler bytes
после trusted SDK preparation; original compiler/reference bytes также проверяются до Start.
Пути исходников/diagnostics сохраняются, staging удаляется до возврата Build result.
C# library profile fail-closed для opaque inputs/custom extensions; schema1 прежняя.
Aether1683/Editor943 отдельно Debug/Release, failed/skipped0;26новых cases.
Fresh15packages/headless8 и published source/NuGet standard/embedded4/4:44children
reaped/released,40graceful validation0/0, UI/GPU/handles0;4PNG просмотрены.
Каждая native compilation захватывает188files/42105392bytes; fingerprints повторных
сборок совпадают. [Scope/commands/findings](engine/HISTORY.md#ed6b2b1-захваченная-c-компиляция--2026-10-31).
Далее ED6b.2b.2 closure до evaluation/preparation. Arbitrary task/generator IO,
installed runtime/environment, cache/reuse/Replace/full ED6b/HUD/input/Player и
historical popup/output-drain/hardware/release follow-ups открыты. Другие suites не повторены.

Предыдущее планирование — **2026-10-31, качество UI-рендеринга и текста PA8c**.
[UI план](alloy/PLAN.md#pa8c-качество-рендеринга-и-текста--2026-10-31) содержит baseline,
text/DPI/font/image/composition улучшения, performance и visual acceptance.
Это документация, новых code/build/test/native/package результатов нет.
Приоритет ED6b сохранён; первый шаг новой ветки — PA8c.0.

Предыдущая реализация — **2026-10-31, UIA1e Bindable → ViewModel**.
Argentis переиспользует INotifyPropertyChanged в общей базе; Editor models/settings
sample подключены к иерархии. UI919/919 и Editor923/923 Debug/Release, failed/skipped0.
[Контракт/evidence](alloy/HISTORY.md#uia1e-bindable-и-viewmodel--2026-10-31).
Другие module/full solution suites, package/native/physical проверки в этом шаге
не повторялись. Общая очередь ED6b и прежние hardware/output-drain ограничения сохранены.

Предыдущая работа — **2026-10-31, ED1b.2 project picker и runtime rebinding**.
Retained project path UI, shared Save/Discard/Cancel и workspace/build identity
согласованы до возврата admission; Close очищает context. Profile требует stopped
game/idle compiler и drains active preview. Aether1631/Editor923 отдельно Debug/Release,
failed/skipped0. [Scope/evidence](engine/HISTORY.md#ed1b2-project-picker-и-runtime-rebinding--2026-10-31).
Final15fresh packages/headless8 и published source/NuGet standard/Cobalt +embedded/MS DI
4/4workflows прошли;32children reaped/released,28graceful validation0/0,14PNG просмотрены.
Далее ED6b document-aware Build/Play. Другие module suites и physical/release gates
этими Aether/Editor runs не закрываются; старый output-drain follow-up открыт.

Предыдущая работа — **2026-10-31, ED1b.1 project/document transitions**.
Project API получил Switch с explicit Save/Discard/Cancel и guarded Close; failed
preparation/cancel сохраняют текущую session/history, cleanup owners удерживаются для retry.
Editor903/903 отдельно Debug/Release,20новых cases, failed/skipped0.
[Scope/evidence](engine/HISTORY.md#ed1b1-projectdocument-transition-api--2026-10-31).
Следовавшие project picker и runtime/build rebinding реализованы в ED1b.2 выше;
ED1/ED6b остаются частичными. Другие suites и прежний package/native scope
этим API-запуском не повторены.

Предыдущая работа — **2026-10-31, ED4c.3 операции объектов и UI commands**.
Atomic create/delete/duplicate, New presets, поиск и context menu используют общую
engine history. Исправлена retained items/selection projection, включая несколько
commits в одном UI update. Aether1629/Editor883/Alloy909/Hosting117 отдельно
Debug/Release, failed/skipped0; final15packages/headless8/native workflows2/2 прошли.
14native children освобождены,12graceful validation0/0,4PNG просмотрены.
[Scope/evidence](engine/HISTORY.md#ed4c3-операции-объектов-поиск-и-контекстное-меню--2026-10-31).
Далее project picker/switch и ED1/ED6 workflow. Прежний intermittent runtime
output-drain follow-up не закрыт этими passing runs.

Предыдущая работа — **2026-10-31, ED4c.2 имена и порядок сущностей**.
Persistent engine metadata и JSON v1/v2 идут через общую atomic history; Editor
предоставляет inline rename, keyboard и before/after reorder с сохранением stable IDs.
Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release, failed/skipped0.
[Audit/8TRX](../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json).
Final15fresh packages/headless8 и published source/NuGet workflows2/2 прошли,
14native children освобождены,12graceful validation0/0,4PNG просмотрены.
[Приёмка](../TestResults/HierarchyMetadata-ED4c2-20261031-1/acceptance.json).
Следующий основной шаг — ED4c.3 create/delete/duplicate, затем project/workflow.
Отдельный runtime follow-up: headless output-drain timeout после reap проявился снова;
два exact binary replays прошли без смены deadlines, причина остаётся неустановленной.

Предыдущая работа — **2026-10-31, ED8a.2 child health/device и acceptance**.
Runtime передаёт numeric health и cached selected device по прежнему diagnostic lane;
UI/report сохраняет last observation/time после crash, явно отмечает unavailable.
Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release, final failed/skipped0.
Fresh15packages/headless8 Game cases и source/NuGet published standard/Cobalt native2/2
прошли:38children reaped/released суммарно,12graceful GPU validation0/0, UI/GPU0.
[Scope и audit](engine/HISTORY.md#ed8a2-child-health-и-device-observations--2026-10-31).
ED8a закрыт в указанном scope; следующий основной шаг — ED4c.2 имена сущностей,
затем ED4c.3/project/workflow. Report остаётся partial; extended native/backend/provider,
physical/device loss/VRAM/long-load и release gates самостоятельны. Full solution и
Core/Enerit/Mithril suites заново не запускались; их старые counts не перенесены сюда.

Предыдущая работа — **2026-10-30, ED8a.1 archive и управляемый diagnostic report**.
Core даёт borrowed bounded JSONL/configuration views; Editor объединяет историю с
memory, добавляет explicit text opt-in, backend/viewport, user cancel и App drain.
Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0. Published
Release/Cobalt consumer вне checkout проверил live/crashed CPU preview children,
rotated/corrupt history, finite flood и native Skia raster; оба children освобождены.
[Evidence/границы](engine/HISTORY.md#ed8a1-архив-и-управляемый-экспорт--2026-10-30).
Далее ED8a.2 child health/device и window/game/NuGet acceptance, затем ED4c.2/3.
Отчёт остаётся partial с explicit unavailable; full solution/UI/Hosting suites,
окно/GPU и прежние hardware/release gates этим запуском не закрыты.

Предыдущая работа — **2026-10-30, ED6a.3 compiler diagnostics/source navigation**.
Bounded SARIF и Compiler mode сохраняют file/line/column/job correlation; host hook
переходит по frozen location и drain/cancel при Stop. SARIF retention исправлена
после initial4failures. Aether1570/Editor833 отдельно Debug/Release, failed/skipped0;
SourceDebug/Cobalt/native Skia raster consumer и3PNG проверены без окна/GPU.
[Точный scope и evidence](engine/HISTORY.md#ed6a3-compiler-diagnostics-и-source-navigation--2026-10-30).
Далее полный ED8a report; window/published/flood и прежние hardware/release gates
остаются отдельными. Общего solution/package proof в этом шаге нет.

Предыдущая фиксация планов — **2026-10-30, агенты и Game AI в Aether**.
[ARCH1d/ENG9](engine/PLAN.md#локальные-агенты-и-mcp-adapter) задают общий agent API,
локальный MCP, доступ к Runtime и создание/отладку поведения мобов; ED4g/ED7a
в Editor плане — UI consumers. LLM-персонаж выделен в optional последующий профиль.
Это документация: новых implementation/build/test/native/package результатов нет.
Текущая очередь compiler navigation/ED8a → ED4c.2/3 → project/workflow сохранена;
наблюдение/control не требует полного ARCH1c, произвольный live edit требует.

Предыдущая реализация и проверка — **2026-10-29, ED6a.2 raw streams и health**.
Editor показывает bounded runtime/build stdout/stderr и parent Core/child delivery/
capture health; raw severity и известные/unknown counters разделены. Metadata
export содержит numeric health без raw/error text. Core387/Aether1556/Editor823/
Hosting117 Debug/Release и один SourceDebug/Cobalt native case прошли. Native finding
потребовал Hosting fix: dirty после Update откладывает presentation до следующего
bounded tick, output guards сохранены. [Evidence/ограничения](engine/HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
Далее compiler diagnostics/navigation и полный ED8a report; broader delivery/package/
published/physical acceptance остаётся отдельной.

Предыдущая работа — **2026-10-29, ED6a structured console и первый partial export**.
Editor показывает bounded structured history, filter/group/inspect/copy/clear/follow-tail;
metadata-only report явно отмечает partial/unavailable и исключает message/stack/
arbitrary properties. Editor808/808 Debug/Release; один SourceDebug/Cobalt native
workflow проверяет live child history, crash/recovery, filter/group/reopen/export,
6graceful Vulkan reports0/0 и cleanup0. [Точный scope и evidence](engine/HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).
Следующий шаг — ED6a.2 raw streams/borrowed health, затем compiler navigation/full
ED8a report; package/published/provider/flood acceptance новой console открыт.

Предыдущая работа — **2026-10-29, ARCH2e.4 scoped file-drain contract**.
Исправлена подтверждённая зависимость file sink от shared ThreadPool после EOF:
dedicated worker/sync wake-up, прежние queue8/budgets/ownership и deadlines.
Controlled RED→GREEN; Core374/374 и Aether1550/1550 отдельно Debug/Release,
fresh15packages/headless8/8 и native24/24/56children прошли.48graceful Vulkan
validation reports0errors/0warnings, teardown0/owned processes0.
[Аудит/current TRX](../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json),
[Core proof/границы](core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).

ARCH2e.4 завершён в проверенном scope; первый ED6a/ED8a consumer описан выше.
Точная причинная цепочка старого intermittent episode не установлена: controlled
proof не подменяет его отсутствующий failure heap. Observation и инструменты
при повторе сохранены, arbitrary FS stalls не обещаются. Общие UI/hardware/K7/
VRAM/BSOD/general-release gates и полный solution run этим не закрываются.

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE6 + borrowed health/read-window APIs; Core387/387 Debug/Release, counters/ownership/UTF16 boundaries проверены | Domain/aggregate telemetry, bootstrap/App-private/generation/performance; прежние cooperative/platform limits |
| [Cobalt / DI](cobalt/STATUS.md) | Текущий179/generator91 Debug/Release; document/project ownership на Cobalt/MS DI | API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) | ENE6d.1–3 metrics/traces и отдельный Aether child lane; current343/generator114 Debug/Release; прежний package proof отдельно | Queue distribution; ENE6c/MMF/trends и расширенная platform/package matrix отдельно |
| [Mithril](mithril/STATUS.md) | Ready-file reader исправлен с RED2→GREEN; полный111/111 Debug/Release, published composition14/restart20. Runtime/API и identity/reap assertions прежние | Ready-file follow-up закрыт; полный MIT6 fault matrix/containment/trends остаётся отдельным |
| [Alloy / Argentis](alloy/STATUS.md) | ED6b.2d source/NuGet published native4/4; UI/GPU/handles0,8new PNG просмотрены; Alloy/Hosting suites не повторены | PA8c.0; ED8 popup follow-up. Physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ED6b.2d scoped Aether82/82+full Editor957/957 Debug/Release, native4/4, fresh15packages/headless8/8; document-aware code Replace | ED6b.2d.1 native interrupt acceptance; scene migration/graphs/disk cache/full ED6b и прежние follow-ups отдельно |

Подробные contracts/commands/failures ведутся у владельцев. Historical packages,
performance и аппаратные проверки не становятся частью текущего full solution.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные consumers | ARCH2e.3 native/headless profile проверен; ARCH2e.4 file-worker contract проверен; ED1–ED8, общий project/runtime switch и дальнейшие consumers открыты |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний найденный полный run — **2026-10-27, ARCH2a**,10test projects:
Debug4296/4296, Release4295/4296, failed1/skipped0. Release failure — Mithril
composition ready-file sharing IOException. [Итоги и исходный failure](../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json),
[история](engine/HISTORY.md#arch2a-scene-persistence-в-aether--2026-10-27).

Позднее ready-file fix прошёл отдельный Mithril111/111 Debug/Release и published
composition14/restart20; Aether1543/1543 и Editor763/763 Debug/Release проверены
в своих runs от28октября. Эти результаты не складываются в новый solution run.
Последний сохранённый solution nonincremental Debug build от28октября:
0errors/1541warnings, [log](../TestResults/ARCH2d-20261028-1/solution-Debug.log).

ARCH2d resource/headless verification:18hash checks,16CLI checks и independent
runtime DLL/apphost4/4 source Debug/published Release вне workspace. [Точный scope](engine/HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28).
GUI/native loading и independent NuGet подтверждены отдельными runs29октября
выше; это не новый общий solution/release run.
Более ранние full/package/native результаты — в [HISTORY](HISTORY.md).

## Общие блокеры и неопределённости

- Normal Editor .aetherproj выбирается через CLI/project picker; document UI,
  Transform/Undo/Redo/Edit preview и New→create/delete/duplicate→Save/reopen реализованы.
  Project switch проверен в stopped-game/idle-compiler profile; bounded Build/Play/
  Replace/source tracking реализованы. Full ED6/input/HUD/Player остаются открытыми.
  ED4d уже редактирует scalar/null/multiline свойства и состав компонентов через
  общие schemas/history; templates/wizard/recent/preferences остаются в плане.
- Scene store optimistic/trusted local: cooperating writers согласованы sidecar;
  arbitrary external rename между hash check/replace, links и power-loss durability
  не гарантируются. Terminal Dispose forced discard требует user close policy заранее.
- Engine preview GPU-only Windows x64 с fixed camera и child replacement на revision.
  Активную game session перед preview нужно явно остановить. Runtime Play state
  не подменяет authoring document/сохранённый checkpoint.
- DI1–DI4 закрыты в проверенном consumer scope; внешние games требуют entry factory/
  Contracts reference и пересборки artifacts. Массовая migration не назначена.
- K7: previous full240-window failed (GDI23→137/USER26→26), trace связан с
  NVIDIA ICD32.0.16.1742. Workaround search остановлен по решению пользователя;
  возврат при supported fix либо согласованной проверенной alternate configuration.
- Physical DPI100/200%/разные мониторы, real device/context loss, VRAM и final UI
  matrix не подтверждены. Retained UI PNG не заменяет physical-display acceptance.
- Cooperative shutdown не прерывает игнорирующий token user Task или arbitrary
  driver/dispatcher hang. Same-build proof не обещает другие OS/RID/AOT и долгие
  resource trends. Failed cleanup требует сохранения ownership и явного retry.
- ENG2 CPU receiver, legacy Build/Replace и MSB3030 staging paths прошли свои
  full/headless проверки, включая исходный TEMP. Forwarding test follow-up закрыт
  codec/late-ACK regressions2026-10-28; точная причина задержки прежнего Debug run
  неизвестна. Произвольно длинные paths и общий release scope автоматически не
  становятся поддержанными.
- Общий supported configuration/SDK/OS scope, warnings policy и R1–R6 остаются
  отдельными решениями; публикации не было.

## Следующий конкретный шаг

1. Измерить observer cost на большом local graph при ordinary10s polling/5s budget
   по [ED6b.2e](engine/EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).
   Directed Replace/package interrupts уже имеют отдельные completed scoped proofs.
2. Для Window — [UIA2d independent package consumer](alloy/PLAN.md#uia2-window);
   рекомендуемый Editor consumer [ED4f.3](engine/EDITOR_PLAN.md#ed4f3-window-root)
   остаётся отдельной C# integration, не выполненной документационной сверкой.
3. Command/frame aggregates после scoped ED8a. App-private routing и общий artifact quota
   требуют отдельных контрактов; native/hardware stress gates не подменяются console UI.
4. При возврате к gameplay — ENG5c.3 collisions камеры, затем HUD/Player по их планам.
5. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
6. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).

</details>
