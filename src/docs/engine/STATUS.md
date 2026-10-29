# TrueMoon.Aether: состояние движка и редактора

Последняя работа — **2026-10-29, ED6a.2 raw streams и health**.
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
templates/wizard/recent/preferences ещё нужны. ED2a/b .aetherscene schema1≤4MiB/
depth32; unknown versions отклоняются без migration, resolved/runtime/history не
сохраняются. Atomic store использует SHA256 guard и sidecar для cooperating writers;
arbitrary external rename, links sandbox и power-loss durability не гарантируются.

Document actions доступны при stopped/idle game/compiler или Edit preview; preview
останавливается до document replacement/close. Open document по-прежнему блокирует
Build/Play/Replace до ED6 identity/workflow integration. New создаёт пустую сцену.
Reparent и schema-driven component edits реализованы в ED4c.1/ED4d; имена/порядок,
create/delete/duplicate и остальной ARCH1b открыты. Общая project/runtime
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

ARCH2e.4 scoped contract проверен controlled RED/GREEN и fresh consumers; сохранённое historical observation требует новых данных при повторе, не догадки о старом root cause.

ARCH2 package/native и file-worker profile проверены. Публичные границы
и ownership — PACKAGE_BOUNDARIES.md; supported consumer commands —
ManualTests/AetherPackageConsumer/README.md. Headless и UI зависимости разделены;
launch roles/Player/HUD ещё запланированы и не входят в закрытие ARCH2.

Следующий этап — compiler file/line/column diagnostics и navigation hook; raw вывод
не парсить как единственный источник классификации. Затем полный ED8a: persistent
history, raw-text/privacy policy, child sink health/backend/device/configuration,
explicit user cancellation и published/flood/slow-UI proof. Parent/process numeric
health уже входит в metadata report; command/frame metrics идут после этого scope.
Следующая authoring задача по прежней очереди:

ED4c.2: сохраняемые имена и порядок соседей через общие engine metadata/формат и
authoring history; затем inline rename и before/after reorder в существующем дереве.
Следом ED4c.3 create/delete/duplicate: New → добавить объект → изменить Transform →
удалить/Undo → Save/reopen. Сначала headless atomic/revision/identity/validation proof;
не создавать UI-only мутаций или второй history.
Детали — [ED4c](EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
ENG2 CPU FrameReceiver и legacy Build/Replace follow-ups проверены новыми full
Aether runs и headless source/published matrix выше; исходные failures сохранены.
Mithril ready-file race закрыт отдельными module/published proofs. Diagnostic
forwarding test follow-up закрыт2026-10-28 codec/late-ACK regressions и full runs
выше; конкретный источник задержки initial Debug run из старого TRX неизвестен.
MSB3030 закрыт новым staging layout, inner binlogs и default-TEMP full runs выше;
произвольно длинные paths вне проверенного профиля не обещаются. Общая очередь
теперь ED6a/ED8a → ED4c.2/3/workflow; полный World/ECS не prerequisite.
После create/delete — project picker/switch и ED1/ED6 document/build/play workflow.
Новый ED6b уточняет этот workflow; launch roles/Player — ENG7b. Runtime HUD
ENG5c.4 следует за gameplay collisions ENG5c.3, приёмка в Player зависит от ENG7b.2.
Full snapshot/runtime editing ARCH1c и смена launch role на ходу не prerequisites.
