# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-31, ED4c.3 операции объектов, поиск и context menu**.
Общие create/delete/duplicate batches используют прежние owner/revision/receipts/history.
New позволяет создать объект/куб и child; shared icons/Insert/Ctrl+D/Delete/context
commands, поиск по имени/ID и inspection работают с persisted scene. Retained tree
projection согласует items/selection, не сбрасывает выбор при удалении строки.
Aether1629/Editor883/Alloy909/Hosting117 отдельно Debug/Release, failed/skipped0.
Final15fresh packages/headless8 и published source/NuGet standard/Cobalt workflows2/2:
New→create→Transform→duplicate/delete/Undo/Redo→search→Save/reopen прошёл;
14native children reaped/released,12graceful validation0/0, UI/GPU/handles0,4PNG просмотрены.
[Команды/initial findings/evidence](HISTORY.md#ed4c3-операции-объектов-поиск-и-контекстное-меню--2026-10-31).
Далее project picker/switch и ED1/ED6 workflow. Full ARCH1b/ED4 и прежний intermittent
output-drain follow-up остаются открытыми; новый passing не объявляет его исправленным.

Предыдущая работа — **2026-10-31, ED4c.2 сохраняемые имена и порядок сущностей**.
Общие EntityInstance/ResolvedEntity metadata, atomic HierarchyEditBatch и JSON v1/v2
подключены к прежним document owner/revision/history. Editor даёт inline F2/Enter/Esc,
Ctrl+↑/↓ и before/after drop; ID, component patches, selection и retained controls сохраняются.
Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release, failed/skipped0.
[Audit/8TRX и новые cases](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json).
Final fresh15packages/headless8 и published SourceRelease/PackageRelease standard/Cobalt
workflows2/2 прошли;14children reaped/released,12graceful Vulkan validation0/0,
UI/GPU/handles0.4finalPNG просмотрены после исправления hint glyphs.
[Приёмка/границы и отдельный drain follow-up](HISTORY.md#ed4c2-имена-и-порядок-сущностей--2026-10-31).
Следом ED4c.3 create/delete/duplicate, затем project/workflow; полный ARCH1b и ED4 открыты.

Предыдущая работа — **2026-10-31, ED8a.2 child health/device и acceptance**.
Version2 body в прежнем diagnostic lane доставляет bounded numeric child health и
last selected Vulkan device. UI/report читает cached immutable observation/time,
сохраняет его после crash и отличает unknown/omitted от healthy0. Cached UI device
capability даёт actual presentation metadata без native queries при export.
Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release, final failed/skipped0;
initial Release1fixture failure исправлен first-poll barrier, budgets прежние.
Fresh15NuGet packages:8headless Game cases/24children. SourceRelease и PackageRelease
standard/Cobalt GUI:2/2workflows,14children/2owned crashes,12graceful validation0/0,
UI/GPU/handles0.4PNG просмотрены. [Evidence/границы](HISTORY.md#ed8a2-child-health-и-device-observations--2026-10-31).
ED8a закрыт в этом Windows x64/net10/OpenGL UI↔Vulkan preview и CPU Game scope.
Далее ED4c.2 сохраняемые имена, затем ED4c.3. Persistent compiler history, extended
native/provider/backend matrix, physical/device loss/VRAM/long-load и общий release
остаются отдельными. Core/Enerit/Mithril/full solution suites этим шагом не повторялись.

Предыдущая работа — **2026-10-30, ED8a.1 JSONL history и управляемый report export**.
Schema2 объединяет bounded parent archive/memory, effective scalar config и observed
backend/viewport; explicit text/history choices, privacy notice, user cancel и
single-flight/drain. Default metadata-only, opt-in raw/compiler/exception/source text;
child sink health/device/persistent compiler history явно unavailable.
Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0. Published
Release/Cobalt consumer вне checkout: реальные CPU preview normal/crashed children,
rotated/malformed/incomplete history,5000imports и Dark/Light native Skia raster;
оба children reaped/released, View/App cleanup прошёл. Paused-follow backend gap
исправлен, full Editor повторён. [Evidence/границы](HISTORY.md#ed8a1-архив-и-управляемый-экспорт--2026-10-30).
Далее ED8a.2 child health/device и window/game/NuGet acceptance, затем ED4c.2/3.
Full solution/UI/Hosting/Enerit/Mithril suites, окно/GPU и hardware gates не повторялись.

Предыдущая работа — **2026-10-30, ED6a.3 compiler diagnostics/source navigation**.
GameBuildCompiler читает bounded SARIF2.1 после compile/reap; job/revision/artifact
и failed diagnostics сохраняются после successful retry. SARIF включён в owned
retention; initial full Debug выявил4regressions allowlist, исправлены с повтором.
Editor Compiler mode и borrowed ISourceFileNavigator/command hook дают переход по
frozen location, cancellation/drain и observed failure. Metadata export не содержит
compiler text/source paths. [Контракт, команды и evidence](HISTORY.md#ed6a3-compiler-diagnostics-и-source-navigation--2026-10-30).
Текущие Aether1570/1570 и Editor833/833 отдельно Debug/Release, failed/skipped0;
narrow19+34. SourceDebug/Cobalt/native Skia raster: реальный failure→navigation→
themes→retry,3PNG просмотрены, View/App cleanup. Окно/GPU/внешний editor process,
published/packages/full solution и остальные suites не проверялись этим срезом.
Следующий шаг — полный ED8a report и scoped window/published/flood acceptance.

Предыдущая фиксация планов — **2026-10-30, agent integration и Game AI**.
По запросу пользователя уточнены [ARCH1d.1–4](PLAN.md#локальные-агенты-и-mcp-adapter):
общий API, локальный MCP, Runtime observation/control, authoring/jobs и эксперименты.
Добавлены [ENG9a–d](PLAN.md#eng9-ии-мобов-и-агентные-проверки--2026-10-30): поведение
охранника, bounded decision traces/scenario assertions и optional LLM-персонаж;
UI consumers — ED4g/ED7a в EDITOR_PLAN.md. Все новые этапы запланированы.
Изменены только планы/статусы, ссылки и согласованность проверены чтением;
build/tests/native/packages не запускались. Очередь ED6a/ED8a → ED4c.2/3 →
project/workflow и hardware/release ограничения сохранены. При начале agent среза —
ARCH1d.1: сверка API и contract local attach/headless/identity/budgets/ownership.

Предыдущая реализация и проверка — **2026-10-29, ED6a.2 raw streams и health**.
В console добавлены RAW stdout/RAW stderr/Health: borrowed in-memory runtime/build
snapshots, parent Core sink/ring/emergency/retention и child delivery/capture counters.
Default16processes/4096chars per stream; raw severity не выводится из stderr,
inspected text frozen до явного refresh. Compiler хранит16completed jobs+active,
native64recent leases; nonce/endpoints не входят в snapshot API. Metadata report
включает numeric health, исключает raw/error text и явно указывает budget omissions.
Core387, Aether1556, Editor823 и Hosting117 — Debug/Release, failed/skipped0 в final
runs. Native SourceDebug/standard/Cobalt прошёл raw/health/mode cycle/crash/reopen,
7children reaped,6graceful Vulkan reports0/0, UI/GPU/handles0. Native finding исправлен
в Hosting: visual dirty после Update переносит presentation на следующий bounded tick.
[История и audit](HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
Далее compiler file/line/column navigation и полный ED8a: history/raw export policy/
child sink health/backend/config/user cancel. Published/provider/flood/physical gates открыты.

Предыдущая работа — **2026-10-29, ED6a: structured console и первый partial export**.
«Вид → Диагностика»: bounded reader, фильтры/search/grouping, inspect/copy,
clear view и pause follow-tail; retained View/Data/style/IFactory и App ownership.
Metadata-only JSON export до512KiB/cooperative5s, без текста/stack/arbitrary properties,
с explicit partial/unavailable и no-overwrite. Editor808/808 Debug/Release,
failed/skipped0. Native SourceDebug/standard window/Cobalt: live child history,
crash/recovery, filters/groups/retained inspect/reopen/export;7children reaped,
6graceful Vulkan core/sync reports0errors/0warnings, UI/GPU/handles0 после Dispose.
[Evidence и границы](HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).
Далее ED6a.2: raw stdout/stderr и borrowed sink/delivery/capture health, затем
compiler file/line/column navigation и полный ED8a report. Полные ED6a/ED8a,
published/package/native-provider matrix и flood/slow-UI acceptance ещё открыты.

Предыдущая работа — **2026-10-29, ARCH2e.4: independent file worker**.
После EOF file-drain больше не зависит от свободного shared ThreadPool worker:
dedicated worker + sync wake-up/IO, queue8/budgets/borrowing и2s/5s deadlines прежние.
Controlled RED→GREEN:24bytes/EOF завершены, единственный pool worker удерживается,
оба completion tasks и файл завершаются. Один worker на active file capture —
явная цена; no-file mode без worker. Core374/374 и Aether1550/1550 Debug/Release,
headless8/8 на15fresh packages/625inputs, native24/24/56children и48graceful
validation0/0 прошли. UI/GPU/handles0 после App dispose, owned processes0.
[Audit/current4TRX](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json),
[headless](../../TestResults/ARCH2e4-FileDrain-Packages-20261029-2/verification.json),
[Core proof/решение](../core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).

Scoped ARCH2e.4 contract выполнен; первый ED6a/ED8a consumer реализован выше.
Точная причинная цепочка исторического intermittent timeout не установлена:
controlled proof и passing matrix не выдаются за failure heap прежнего эпизода.
Observation и atomic marker/phase counters сохранены; при повторе нужен targeted
analysis. Arbitrary filesystem stalls/global real-time guarantee не обещаются.
Hosting/Editor/UI suites и full solution в ARCH2e.4 не повторялись; новый Editor run указан выше. Hardware/
K7/VRAM/general release и причина BSOD отдельно; массовый stress не запускался.
Launch roles ENG7b, workflow ED6b и runtime HUD ENG5c.4 остаются в плане;
Aether core не получает UI dependency.

## Реализация и связь с планом

ARCH2a–d и quality follow-ups входят в предыдущие commits; текущая работа
добавляет ARCH2e.1–3 package/native proof, runtime startup и Hosting cleanup fixes. Engine/Editor остаются
library/packable modules, App/DI/lifecycle и единственный owner сохраняются.

| Изменения рабочего дерева | Этап и текущее состояние |
| --- | --- |
| Create/delete/duplicate batches, New presets, search/context commands, retained tree projection | ED4c.3 checked: Aether1629/Editor883/UI908/Hosting117 Debug/Release; final15packages/headless8/native2. Typed entity references/catalog management/multiselect и full ARCH1b/ED4 отдельно |
| Persistent Name/SiblingOrder, HierarchyEditBatch/history, JSON v1/v2; Editor inline rename/reorder | ED4c.2 checked: Aether1602/Editor866/UI900/Hosting117 Debug/Release; final15packages/headless8/native2. Intermittent headless drain observation остаётся отдельным follow-up |
| Runtime observation/version2 body; cached Vulkan/OpenGL device metadata; Editor health/report; package/native assertions | ED8a.2 checked: Aether1581/Editor847/UI893/Hosting117 Debug/Release;15fresh packages/headless8 и source/NuGet published native2, validation12×0/0. Extended matrix/release отдельно |
| Core archive/configuration views, Editor schema2/text/history/cancel/viewport | ED8a.1: Core398/Aether1570/Editor842 Debug/Release; published Release/Cobalt CPU preview normal/crash + raster/flood. Child health/device и broader native/NuGet acceptance открыты |
| Core borrowed health/window APIs, Aether process history/source, Editor raw/health, Hosting dirty-frame deferral | ED6a.2 checked: Core387/Aether1556/Editor823/Hosting117 Debug/Release; native SourceDebug/Cobalt1/1. Compiler navigation/full ED8a и wider acceptance открыты |
| Editor Diagnostics, header/layout/session и Gui consumer | ED6a structured console + ED8a metadata-only partial export: Editor808 Debug/Release и один native SourceDebug/Cobalt workflow; full raw/health/compiler/published acceptance открыт |
| AetherPackageConsumer/Gui/VerifyGui, Hosting callback lifetime capture | ARCH2e.3 native24/24, Hosting114/Editor763 Debug/Release и fresh package/headless8/8 после fix; native cancellation RED→GREEN, полный teardown до passing report |
| AetherPackageConsumer, AetherRuntimeHost worker startup, RuntimeLaunchProbe/NativeRuntimeProcessTests | ARCH2e.1/2 и CPU startup fix проверены29октября; предыдущий Aether1543/1543 Debug/Release не считается новым run этого native шага |
| Examples hosts/resources и VerifyExampleHosts | Resource/headless proof28октября дополнен standard-window/embedded-View native composition29октября. GUI matrix проверяет публичный модульный API, не каждый CLI branch Examples |
| FrameWorkers/SharedFrameRing и FrameTransportTests | ENG2 pending CPU frame больше не теряется при empty/stale poll и EOF; lazy reservation сохраняет два private buffers, UI lease и drain. Follow-up закрыт в проверенном CPU scope |
| BuildProbe/ReplacementProbe и NativeRuntimeProcessTests | ENG3 fixtures мигрированы на IGameSessionEntry/Contracts без ослабления production guards; build/selected artifact/checkpoint/rollback/cleanup follow-up закрыт |
| GameBuildCompiler/GameBuildRequest, deep-job regression | ENG3 single-project staging сокращает intermediate path при неизменной GUID isolation. MSB3030 follow-up закрыт в проверенном TEMP/LongPathsEnabled0 profile; arbitrary long paths не обещаются |
| ChildDiagnosticFixture/ForwarderTests/LimitsTests и delivery guide | ENG8a.3/ENE6d.4 test-quality fix: codec clipping отдельно от real-pipe bounded outcomes. Поздний ACK не переводит уже Unconfirmed в Accepted; runtime и budgets прежние |
| Mithril composition/restart probes и ReadyFileProcessTests | Связанный fixture quality fix закрыт в [Mithril STATUS](../mithril/STATUS.md); runtime/API Mithril и полный MIT6 этим не меняются |

Публичные границы/owners — [PACKAGE_BOUNDARIES](PACKAGE_BOUNDARIES.md).
Минимальные hosts — Examples/AetherEditor и Examples/AetherRuntimeHost;
directed workflows и explicit same-exe runtime — ManualTests/AetherEditorHost.

## Последние датированные проверки

Запуски29октября выполнены в текущей работе; более ранние сохраняют свои даты.

| Дата / профиль | Фактический результат | Evidence и границы |
| --- | --- | --- |
| 2026-10-31, ED4c.3 | Aether1629/Editor883/Alloy909/Hosting117 отдельно Debug/Release; final packages15/headless8/native2/2 | [Audit/8TRX](../../TestResults/EntityOperations-ED4c3-20261031-1/verification.json), [acceptance](../../TestResults/EntityOperations-ED4c3-20261031-1/acceptance.json); native RED→GREEN tree projection, 4PNG просмотрены |
| 2026-10-31, ED4c.2 | Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release; final packages15/headless8/native2/2 | [Audit/8TRX](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json), [acceptance](../../TestResults/HierarchyMetadata-ED4c2-20261031-1/acceptance.json); initial findings и новый drain snapshot сохранены |
| 2026-10-31, ED8a.2 | Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release; packages15/headless8, published native2/2 | [Audit/8TRX](../../TestResults/RuntimeObservation-ED8a2-20261031-1/verification.json), [acceptance](../../TestResults/RuntimeObservation-ED8a2-20261031-1/acceptance.json); initial fixture failures сохранены, scope выше |
| 2026-10-30, ED8a.1 report | Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0; published Release/Cobalt normal/crashed CPU preview + native Skia raster/flood | [Audit/TRX](../../TestResults/DiagnosticReport-ED8a-20261030-1/verification.json), [published proof](../../TestResults/DiagnosticReport-ED8a-20261030-1/published-proof.json); child sink health/device, window/game/NuGet и hardware gates открыты |
| 2026-10-29, ED6a.2 raw/health | Core387/Aether1556/Editor823/Hosting117 отдельно Debug/Release; Hosting RED3→GREEN3; native SourceDebug/Cobalt1/1 | [Audit/8TRX](../../TestResults/ED6a2-20261029-1/audit.json), [native](../../TestResults/ED6a2-Native-20261029-6/verification.json); initial failures сохранены, packages/other native profiles не запускались |
| 2026-10-29, ED6a structured console | Editor808/808 Debug/Release;45новых tests, failed/skipped0. SourceDebug/standard/Cobalt native1/1,7children/6graceful validation0/0 | [Audit](../../TestResults/ED6a-20261029-1/audit.json), [native](../../TestResults/ED6a-Native-20261029-7/verification.json); raw/health/compiler/full export и другие native/package profiles открыты |
| 2026-10-29, ARCH2e.4 file-worker contract | Core374/Aether1550 Debug/Release; controlled starvation RED→GREEN, fresh headless8/native24 | [Current audit](../../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json); exact historical attribution отдельно |
| 2026-10-29, ARCH2e.4 после resource fix | Core373/Aether1550 Debug/Release; headless8/8, native24/24,48validation0/0 | [Audit](../../TestResults/ARCH2e4-Native-20261029-1/audit.json); file-sink root cause открыт, run1 failure сохранён |
| 2026-10-29, ARCH2e.3 native |24/24cases,56children/8owned crashes,48graceful validation0/0,40PNG, teardown0 | [Verification](../../TestResults/ARCH2e3-20261029-9/verification.json), [audit](../../TestResults/ARCH2e3-20261029-9/audit.json);4source/package build/publish profiles, standard/Cobalt и embedded/MS DI |
| 2026-10-29, Hosting/Editor Debug/Release |114/114 и763/763 отдельно в каждой конфигурации, failed/skipped0 | После Hosting lifecycle fix;4TRX в native audit; UI/Aether/full solution suites не повторены |
| 2026-10-29, fresh packages после Hosting fix |15packages,8/8headless cases/24children,624source inputs | [Verification](../../TestResults/ARCH2e3-Packages-20261029-3/verification.json); новый immutable feed/version, отдельно от native run |
| 2026-10-29, ARCH2e packages/headless | 15fresh packages;8/8combinations,24children, MaxLive1;618source inputs и22resource delivery checks | [Verification](../../TestResults/ARCH2e-20261029-7/verification.json); CPU frames/App/DI/Build/Replace/checkpoint/diagnostics и cleanup; без GUI/native loading |
| 2026-10-29, Aether Debug/Release | 1543/1543 в каждой конфигурации, failed/skipped0; targeted RED2→GREEN2 | [История runtime fix](HISTORY.md#arch2e-independent-packages-и-cpu-runtime-startup--2026-10-29); full module, не full solution/Editor/UI |
| 2026-10-28, Aether Debug/Release | 1543/1543 в каждой конфигурации, failed/skipped0 | [Forwarding fix](HISTORY.md#diagnostic-forwarding-bounded-outcomes--2026-10-28), focused44/44 Debug; full module включает headless processes, отдельная published/native matrix не повторена |
| 2026-10-28, Editor Debug/Release | 763/763 в каждой конфигурации, failed/skipped0 | [ARCH2d](HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28); Cobalt/MS DI, options, factory/View ownership и startup/close, без окна/GPU |
| 2026-10-28, ARCH2d source Debug/published Release | 18resource hashes,16CLI checks, independent runtime DLL/apphost4/4;11source inputs unchanged | [Verification](../../TestResults/ARCH2d-20261028-1/verification.json); fresh external deployment/unrelated cwd. PE/resource checks не доказывают native/font loading/rendering |
| 2026-10-28, solution nonincremental Debug build | 0errors/1541warnings | [Build log](../../TestResults/ARCH2d-20261028-1/solution-Debug.log); это build, полный solution test run не повторён |
| 2026-10-27, ENG3 source Debug/published Release | Headless21/21 +21/21 после fixture migration; позднее compiler/checkpoint/Replace/App-stop4/4 +4/4 | [Migration](HISTORY.md#eng3-buildreplace-fixture-migration--2026-10-27), [staging fix](HISTORY.md#eng3-compiler-staging-paths--2026-10-27); последние8cases не являются повтором всей21-scenario matrix |
| 2026-10-27, ENG2 CPU/session/Frames Debug/Release | 94/94 в каждой конфигурации, deterministic RED→GREEN | [Pending frame fix](HISTORY.md#eng2-сохранение-pending-cpu-frame-при-пустом-poll--2026-10-27); hardware/GPU acceptance не входит |
| 2026-10-27, Mithril Debug/Release и fresh published Release | 111/111 в каждой конфигурации; composition14/restart20 | [Отдельный owner и scope](../mithril/STATUS.md) |

ENG2, legacy Build/Replace, compiler MSB3030 и ошибочное ожидание forwarding
Accepted закрыты соответствующими regressions. Конкретный источник задержки
исходного forwarding run из старого TRX не установлен. Исходные failed runs и
compact/default TEMP profiles сохранены в HISTORY; passing другого модуля
не превращает прежний full solution failure в новый общий green.

## Открытая acceptance и ограничения

- ARCH2e.3 проверен только в описанном Windows x64/net10.0/OpenGL↔Vulkan profile.
  Native consumers не подтверждают другие OS/RID/AOT, каждый CLI branch Examples,
  physical OS input/DPI/monitors, driver/device loss и долгие resource trends.
  Локальный validation layer использован process-local; причина прежнего BSOD
  не установлена, массовые GPU/K7 stress runs не повторялись.
- Новый общий full solution Debug/Release test run после quality fixes не выполнен.
  Последний сводный ARCH2a run от27октября: Debug4296/4296, Release4295/4296,
  failed1 в Mithril ready-file; последующий Mithril fix имеет отдельный proof.
- Physical DPI/K3/K7/K8/VRAM/final alpha и long resource/performance trends открыты.
  K7 workaround search остановлен по решению пользователя; публикации NuGet не было.

## Границы приложения и engine

ED1a выбирает один проект через explicit .aetherproj; без него пустая workspace,
legacy .csproj сохраняет игровой demo workflow. Project picker/switch UI,
templates/wizard/recent/preferences ещё нужны. ED2a/b .aetherscene v1/v2≤4MiB/
depth32; unknown versions отклоняются без migration, resolved/runtime/history не
сохраняются. Atomic store использует SHA256 guard и sidecar для cooperating writers;
arbitrary external rename, links sandbox и power-loss durability не гарантируются.

Document actions доступны при stopped/idle game/compiler или Edit preview; preview
останавливается до document replacement/close. Open document по-прежнему блокирует
Build/Play/Replace до ED6 identity/workflow integration. New создаёт пустую сцену.
Reparent, имена/порядок и schema-driven component edits реализованы в ED4c.1/2 и ED4d;
Create/delete/duplicate реализованы в ED4c.3; typed references, catalog management
и остальной ARCH1b открыты. Общая project/runtime
Switch/Close transaction остаётся ED1/ED6; ED2 document Close/Switch/window Exit profile
выполнен. Terminal shutdown forced discard/drain; native user close vetoable.

Preview GPU-only Windows x64/OpenGL UI/Vulkan child, fixed camera,640×360,≤64entities,
новый child при обновлении revision. Authoring hierarchy/properties не обрезаются
этим лимитом; preflight rejection сохраняет документ. Build/Play/Game.Logic для
preview не нужны. Frames не идут через IPC; Enerit — control, Mithril — процессы.
Failed cleanup удерживает ownership для retry; arbitrary driver/dispatcher hang
не прерывается deadline. API experimental, публикации не было. Physical DPI/K3/K7/
K8/VRAM/final alpha открыты; NVIDIA workaround search остановлен по решению пользователя.

## Следующий конкретный шаг

ARCH2e.4 scoped contract проверен прежними controlled RED/GREEN. Новый run2026-10-31
снова наблюдал stdout/stderr drain timeout после reap в headless exe/MS DI generation3:
оба readers WaitingForActivation,24bytes сохранены,9workers/pending0, все3children
уже завершились. Два exact binary replays прошли без смены deadlines. Причина не
установлена и не объявляется исправленной; [новый snapshot/follow-up](HISTORY.md#ed4c2-имена-и-порядок-сущностей--2026-10-31).

ARCH2 package/native и file-worker profile проверены. Публичные границы
и ownership — PACKAGE_BOUNDARIES.md; supported consumer commands —
ManualTests/AetherPackageConsumer/README.md. Headless и UI зависимости разделены;
launch roles/Player/HUD ещё запланированы и не входят в закрытие ARCH2.

Compiler diagnostics/source hook ED6a.3 и report ED8a.1/2 проверены в scopes выше.
Extended native/provider/backend matrix, persistent compiler history и command/frame
metrics остаются отдельными. ED4c.2/3 имена/порядок и операции объектов реализованы
через общую history. Следующий основной шаг — project picker/switch и ED1/ED6
document/build/play workflow с сохранением active document/unsaved/cancel/ownership contracts.
Детали — [ED4c](EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
ENG2 CPU FrameReceiver и legacy Build/Replace follow-ups проверены новыми full
Aether runs и headless source/published matrix выше; исходные failures сохранены.
Mithril ready-file race закрыт отдельными module/published proofs. Diagnostic
forwarding test follow-up закрыт2026-10-28 codec/late-ACK regressions и full runs
выше; конкретный источник задержки initial Debug run из старого TRX неизвестен.
MSB3030 закрыт новым staging layout, inner binlogs и default-TEMP full runs выше;
произвольно длинные paths вне проверенного профиля не обещаются. Общая очередь
теперь project/workflow; полный World/ECS не prerequisite.
Новый ED6b уточняет этот workflow; launch roles/Player — ENG7b. Runtime HUD
ENG5c.4 следует за gameplay collisions ENG5c.3, приёмка в Player зависит от ENG7b.2.
Full snapshot/runtime editing ARCH1c и смена launch role на ходу не prerequisites.
