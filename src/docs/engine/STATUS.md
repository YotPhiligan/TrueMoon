# TrueMoon.Aether: состояние движка и редактора

Последняя связанная проверка — **2026-10-28, SkiaSharp/native4.154.0**.
Полный Editor763/763 Debug/Release прошёл с новой парой; UI893/Hosting114 также
прошли обе конфигурации. Source/published Vulkan copy/composition и backend
comparison принадлежат [UI quality scope](../alloy/HISTORY.md#skiasharp-41540-и-vulkan-interop--2026-10-28).
Nonincremental solution Debug build0errors/1541warnings. Aether/full solution tests,
Editor GUI/preview workflow и independent NuGet не повторены. ARCH2d/e native/window
acceptance остаётся открытой; далее ARCH2e.1/2 по существующему плану.

Предыдущая работа — **2026-10-28, сверка рабочего diff, планов и сохранённых evidence**.
Новых build/tests/process/GPU/package runs в этой сверке нет. Проверены исходники,
TRX counters и verification summaries; предыдущие результаты сохраняют свои даты
и scope. Накопленные сводки с исходными failures и прежней очередью перенесены в
[HISTORY](HISTORY.md#сверка-рабочего-дерева-и-архив-status--2026-10-28).

QA этой сверки:20документов/707локальных ссылок/425якорей, ошибок0;
53source fingerprints совпали,6сохранённых TRX подтверждены.
[Отчёт](../../TestResults/PlanningReview-20261028/verification.json).

## Реализация и связь с планом

ARCH2a–c уже входят в базовый commit `af03ca3`; текущий незакоммиченный diff
продолжает ARCH2d и закрывает отдельные quality follow-ups. Engine/Editor остаются
library/packable modules, App/DI/lifecycle и единственный owner сохраняются.

| Изменения рабочего дерева | Этап и текущее состояние |
| --- | --- |
| Examples/AetherEditor, перенос AetherRuntimeHost из ManualTests в Examples, solution/test references, host-owned fonts/OFL, VerifyExampleHosts | ARCH2d реализован и проверен source/published в resource/headless scope. GUI/native acceptance открыта; independent NuGet ещё не проверен |
| FrameWorkers/SharedFrameRing и FrameTransportTests | ENG2 pending CPU frame больше не теряется при empty/stale poll и EOF; lazy reservation сохраняет два private buffers, UI lease и drain. Follow-up закрыт в проверенном CPU scope |
| BuildProbe/ReplacementProbe и NativeRuntimeProcessTests | ENG3 fixtures мигрированы на IGameSessionEntry/Contracts без ослабления production guards; build/selected artifact/checkpoint/rollback/cleanup follow-up закрыт |
| GameBuildCompiler/GameBuildRequest, deep-job regression | ENG3 single-project staging сокращает intermediate path при неизменной GUID isolation. MSB3030 follow-up закрыт в проверенном TEMP/LongPathsEnabled0 profile; arbitrary long paths не обещаются |
| ChildDiagnosticFixture/ForwarderTests/LimitsTests и delivery guide | ENG8a.3/ENE6d.4 test-quality fix: codec clipping отдельно от real-pipe bounded outcomes. Поздний ACK не переводит уже Unconfirmed в Accepted; runtime и budgets прежние |
| Mithril composition/restart probes и ReadyFileProcessTests | Связанный fixture quality fix закрыт в [Mithril STATUS](../mithril/STATUS.md); runtime/API Mithril и полный MIT6 этим не меняются |

Публичные границы/owners — [PACKAGE_BOUNDARIES](PACKAGE_BOUNDARIES.md).
Минимальные hosts — Examples/AetherEditor и Examples/AetherRuntimeHost;
directed workflows и explicit same-exe runtime — ManualTests/AetherEditorHost.

## Последние датированные проверки

Это сохранённые запуски, сверенные с artifacts, а не новый test run.

| Дата / профиль | Фактический результат | Evidence и границы |
| --- | --- | --- |
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

- ARCH2e independent NuGet consumer ещё не выполнен. ProjectReference build/publish
  и local pack/metadata не доказывают независимую поставку.
- ARCH2d GUI/window/font/native loading и общая GUI/GPU acceptance ARCH2 открыты.
  GPU runs отложены после BSOD; причина и связь с проектом не установлены.
  Vulkan validation environment нужно восстановить; прежний functional ARCH2b
  proof выполнен без validation после ErrorLayerNotPresent.
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

Следующий доступный шаг — [ARCH2e.1/2](PLAN.md#arch2e-независимый-nuget-consumer--декомпозиция-2026-10-28):
fresh packages/isolated feed и отдельное решение вне checkout, затем independent
NuGet build/publish/headless/App/DI/resource profile. ARCH2d тонкие hosts и
доставка fonts/licenses/themes/shaders/native payloads проверены в source/published
headless scope выше, без source-checkout/ManualTests lookup в consumers.
ARCH2a–c реализованы; карта existing типов/owners — PACKAGE_BOUNDARIES.md.
Минимальные hosts — Examples/AetherEditor и Examples/AetherRuntimeHost; directed
smokes остаются в ManualTests/AetherEditorHost с explicit same-executable runtime.
GUI/GPU regression и затронутая native acceptance остаются открытыми; GPU runs
отложены до отдельного разбора BSOD, причина и связь с проектом пока не установлены.
Полный ARCH2 требует также native/window proof вне checkout и восстановления
Vulkan validation environment; headless passing не заменяет эти критерии.
Сохранить scene/project compatibility, headless Aether и существующий функционал;
новые console/authoring UI features идут после перехода.

После ARCH2 в diagnostics направлении — ED6a/ED8a bounded console/export в Editor
поверх Core reader и child delivery: retained rows, фильтры source/level/session,
явные loss indicators и bounded export без rebuilding View tree. Затем command/frame
metrics; UI/data/style/IFactory ownership остаётся штатным для Editor.
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
остаётся ARCH2e.1/2 → GUI/native proof ARCH2d/e; полный World/ECS не prerequisite.
После create/delete — project picker/switch и ED1/ED6 document/build/play workflow.
