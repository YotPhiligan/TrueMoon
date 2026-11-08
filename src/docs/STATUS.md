# TrueMoon: общий статус

**Nom2026-11-08:** [исходники организованы по подсистемам](nom/STRUCTURE.md#nom-source-layout-2026-11-08),
namespace соответствует пути. Public types перенесены вместе с реализациями;
alpha consumers требуют новых imports и пересборки. Формат/lifetime/dependencies
сохранены. Nom862/862 каждый D/R,120functional groups/profile source/published,
App34/Buffer28/Raw31checks D/R и fresh4package/4consumer delivery прошли. Full solution
nonincremental Debug0errors/1560warnings, Nom/Nom.Tests0warnings.
[Scope/команды](nom/HISTORY.md#nom-structure-2026-11-08). NOM8 готов; далее g.2 после
g.1 по согласованию engine. VM verification отложена, hardware power-off исключён;
UI/Editor/release очереди сохранены.


Перед ECS commit2026-11-07 проверена конфигурация **0.2.5.1-alpha**:
targeted ECS/Rendering1230/1230 каждый D/R, solution nonincremental Debug0errors/
1555existing warnings, новых warnings0. [Scope и команды](engine/HISTORY.md#ecs-commit-2026-11-07).

Последнее обновление зависимости — **2026-11-07, SkiaSharp/native4.155.0**.
Extended DLL пересобрана; source/published Vulkan validation0/0, fresh10-package/
40-process delivery и Alloy1101/Hosting154/Editor1295 каждый D/R прошли.
Solution nonincremental Debug0errors/1555existing warnings, новых warnings0.
[Результаты и ограничения](alloy/HISTORY.md#skiasharp-41550-2026-11-07).

**Актуализация 2026-11-07 после db5ea97:** ARCH1f.2 реализует
[resource owners/leases/preparation](engine/RENDER_RESOURCES.md), atomic World rebind
и standalone Vulkan uploads. Current targeted1230/1230 каждый D/R (137new),
source/published/fresh7pkg CPU и Vulkan consumers прошли:2generations+zero-budget
denial каждый, upload/readback, validation0/0 и cleanup0. Solution nonincremental
Debug build0errors/1555warnings. Следующий этап f.3 owned render snapshots;
surface renderer/Runtime Playing fence — f.4. Full Aether/performance здесь не запускались.
Прежний full6ноября2536/2538 с diagnostics cleanup timeouts остаётся non-green,
причина не установлена; replays не подтверждают исправление.
Preview/Editor Play/latency и общая release acceptance этим срезом не обновлены.
[Текущая очередь](PLAN.md#current-roadmap), [формат](PLANNING.md),
[прежний STATUS целиком](HISTORY.md#status-before-planning-revision-2026-11-05).

## Состояние модулей

| Направление | Что реализовано в проверенном scope | Что дальше у владельца |
| --- | --- | --- |
| [Core](core/STATUS.md) | App/configuration/lifecycle, bounded diagnostics, borrowed health/archive/configuration readers | CORE6: App-private/bootstrap routing и aggregate metrics по конкретному consumer; generation отдельно |
| [Cobalt](cobalt/STATUS.md) | Runtime/generator/DI adapter и ownership; COB5a attribution выполнена | Production no-capture cycle check и paired measurements; scopes/массовая миграция не назначены |
| [Enerit](enerit/STATUS.md) | Same-build pipes, finite admission/stop, telemetry и отдельный child-log lane у Aether | ENE6c при расширении протокола; scalar CPU/allocations profile и queue distribution отдельно |
| [Mithril](mithril/STATUS.md) | Owned process/restart/diagnostics; ready-file regression исправлен в своём scope | Остальной MIT6 fault/containment/resource-trend profile; не второй restart поверх Aether |
| [Alloy/Argentis](alloy/STATUS.md) | Window, compact typed editors, numeric gestures, styles/states; native pointer fix; SkiaSharp/native4.155.0 с source/published/fresh-package proof | Input acceptance, PA8c.0 baseline качества; hardware/final alpha отдельно |
| [Aether/Editor](engine/STATUS.md) | Documents/history/preview/build/source tracking; ECS primitive pipeline; f.0/f.1 CPU contracts и f.2 resource owners/leases/atomic reload/Vulkan upload с package proof | [ARCH1f.3 owned snapshots](engine/PLAN.md#arch1f-rendering), затем f.4 surface consumer/Playing fence; checkpoint/UI/Observer/reliability сохранены |
| [Nom/storage](nom/STATUS.md) | NOM0–NOM8: KV/CAS/App, snapshots/GC/backup, byte/raw API; subsystem namespaces;862cases D/R и4consumer fresh-package scope | Engine g.2 после g.1 по согласованию; VM verification отложена, hardware power-off исключён; другие OS/RID/modes и release gates отдельно |

## Общие этапы

G1–G3 и ARCH2 имеют bounded source/package/own-child proofs; это не универсальная
готовность. G4 UI alpha частичен: K7 и physical/device-loss/VRAM/final matrix открыты.
G5 продолжает прикладной Editor; весь ED1–ED8/Player/HUD/agents не завершён.
G6/R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе ничего не подтверждает.

## Общие проверки — датированные сведения

Результаты разделены по датам/scope: f.2 относится к текущему запуску7ноября;
старые модульные/full/frame результаты не считаются повторёнными этим запуском.

| Дата и scope | Последний относящийся результат | Где подробности |
| --- | --- | --- |
| 2026-11-08, Nom VM harness |98guards/10host protocol groups D/R,120functional/profile; VM0trials/Blocked image; hardware excluded; solution0errors/1560warnings | [Scope](nom/HISTORY.md#nom-vm-2026-11-08) |
| 2026-11-08, Nom UAC |5real-volume cases/166assertions D/R, free0/OS112, reopen/liveness; VHDX detached/cleanup0; power-loss open | [Scope](nom/HISTORY.md#nom7-uac-2026-11-08) |
| 2026-11-08, Nom physical follow-up |24pure guards/12assertions D/R; fresh package proof passed; physical0cases, New-VHD denied, exit2/Blocked; power-loss open | [Scope](nom/HISTORY.md#nom7-durability-2026-11-08) |
| 2026-11-08, Nom NOM7 |718/718 D/R;120functional/profile, App34source/published; fresh4pkg App34/tool211checks, portable/win-x64 FDD, negative controls; solution0errors/1555warnings | [NOM7](nom/HISTORY.md#nom7-2026-11-08) |
| 2026-11-08, Nom NOM6 |718/718 D/R+discovery;120functional+4Capacity/profile,16Backup kills/reaps; App34checks; actualmaxima/workload; solution0errors/1555warnings; physical gate открыт | [NOM6](nom/HISTORY.md#nom6-2026-11-08) |
| 2026-11-08, Nom NOM5 |628/628 D/R+discovery;104groups/profile, Retention12kills/reaps; public App27checks; solution0errors/1555warnings; physical gate открыт | [NOM5](nom/HISTORY.md#nom5-2026-11-08) |
| 2026-11-08, Nom NOM4 |536/536 D/R+discovery;92groups/profile, Maintenance27/24kills+reaps; public App16checks; solution0errors/1555warnings; physical gate открыт | [NOM4](nom/HISTORY.md#nom4-2026-11-08) |
| 2026-11-08, Nom NOM3 |426/426 D/R+discovery;65console groups/profile,48kills/reaps/Fault profile; App10checks/profile; solution0errors/1555warnings; physical gate открыт | [NOM3](nom/HISTORY.md#nom3-2026-11-08) |
| 2026-11-08, Nom NOM2 |382/382 D/R и solution-filter;18core+6KV groups source/published, public App10checks/profile; no LiteDB graphs/output; solution Debug0errors/1555warnings, Nom0warnings | [Catalog cutover](nom/HISTORY.md#nom2-2026-11-08) |
| 2026-11-07, SkiaSharp/native4.155.0 | Source build/ABI v1; source Debug/published Release по18readbacks/36compositions/backend comparison, validation0/0. Alloy1101/Hosting154/Editor1295 каждый D/R; fresh10pkg36App+4direct processes, native SHA/behavior/cleanup. Solution Debug0errors/1555warnings. Физическая UI acceptance не повторялась | [Native update](alloy/HISTORY.md#skiasharp-41550-2026-11-07) |
| 2026-11-07, ARCH1f.2 |1230/1230 каждый D/R,137newcases; source Debug/published Release/fresh isolated7pkg CPU и actual Vulkan,2resource generations+denial каждый, upload/readback/stale handles/cleanup0/validation0/0. Solution nonincremental Debug0errors/1555warnings; новых full-module/frame/performance результатов нет | [Resources](engine/HISTORY.md#render-resources-2026-11-07) |
| 2026-11-07, ARCH1f.1 |1093/1093 каждый D/R,309newcases; source Debug/published Release/fresh isolated7pkg CPU consumer,3entities/6components/3assets. Solution nonincremental Debug0errors/1555warnings; новых full-module/native результатов нет | [CPU contracts](engine/HISTORY.md#render-cpu-2026-11-07) |
| 2026-11-06, ARCH1f.0 | Выбран opaque-unlit/1: exact IDs/slots/versions, linear tint/sRGB encoding, CPU/GPU readiness, leases/completion и budgets. Только документационные проверки; C# не менялся | [Решения](engine/HISTORY.md#render-contracts-2026-11-06) |
| 2026-11-06, ECS primitive extraction |751/751 D/R; source/published/fresh7pkg headless133logical/2drawables/2generations; source/published Vulkan geometry/core+sync validation0/0/final owners0. Solution Debug0errors/1555warnings. Full Aether Debug2536/2538,2diagnostics cleanup timeouts/replay25/25, общий regression не green | [ARCH1e.4](engine/HISTORY.md#ecs-primitives-2026-11-06) |
| 2026-11-06, initial Runtime ECS load | ECS543/543 +frame fixture1/1 D/R; full Aether Debug2331/2331, failed/skipped0; solution nonincremental Debug0errors/1555warnings; source Debug/published Release/fresh isolated NuGet Release,256entities/286046bytes/2generations каждый, CPU pixels/tick/cleanup | [ARCH1e.3b](engine/HISTORY.md#ecs-runtime-loading-2026-11-06) |
| 2026-11-06, ECS registry/load | ECS409/409 D/R; full Aether Debug2197/2197, failed/skipped0; solution nonincremental Debug build0errors/1555warnings | [ARCH1e.3a](engine/HISTORY.md#ecs-loading-2026-11-06) |
| 2026-11-05, UI/Editor numeric | Alloy1101, Hosting154, Editor1295 в каждой Debug/Release;4native OpenGL runs standard/Cobalt и embedded/MS DI; final UI/GPU/handles0 | [ED4g.2a](engine/HISTORY.md#ed4g2a-native-numeric-2026-11-05) |
| 2026-11-04, Observer | Open batches ускорили many-files latency, но увеличили allocations/CPU; safety/drain fences сохранены | [ED6b.2e.4e](engine/HISTORY.md#ed6b2e4e-observer-open-batches-2026-11-04) |
| 2026-10-30/29/27, остальные модули | Собственные Core/Cobalt/Enerit/Mithril результаты имеют отдельные даты и scope | Локальные STATUS/HISTORY выше |
| Полный solution | Последняя запись в прежней общей сводке:27октября Debug4296/4296, Release4295/4296 с ready-file failure; позднее fix проверен отдельно | [Архив сводки](HISTORY.md#status-before-planning-revision-2026-11-05); это не новый full-solution green |

Последний относящийся nonincremental solution Debug build —8ноября0errors/1560warnings (Nom source organization); Nom warnings0. Это предыдущий build, не новый запуск при подготовке коммита.
Проверки разных модулей и дат не суммируются в один solution run. Ссылки на старые
ignored TestResults могут отсутствовать в checkout; HISTORY сохраняет scope,
а повторяемые runners — в ManualTests. Для нового release нужен новый evidence set.

## Общие блокеры и неопределённости

- Nom: actual guest-VM crash verification отложена пользователем; harness сохранён,
  но actual trials0. Hardware power-off исключён из текущего acceptance. Прежний
  isolated-volume disk-full проверен отдельным UAC запуском; [scope](nom/DURABILITY.md).
- Full Aether6ноября имеет diagnostics delivery/Stop/Dispose timeouts на unchanged
  исходниках; focused replays прошли, причина не установлена. Это открытый
  reliability result, не доказанная регрессия ECS и не исправленный timeout.
- Physical arrows/modifiers, Esc во время удержания кнопки и OS pointer modifiers
  numeric UI не подтверждены. Automation Up дал UiKey.None; причина не доказана
  как дефект физической клавиатуры. Эти проверки не заменяются routed events.
- K7: прежний240-window run failed по GDI; поиск NVIDIA workaround остановлен
  по решению пользователя. Новые driver/hardware/OS settings здесь не проверялись.
  Physical DPI/multi-monitor, device/context loss, VRAM и final UI matrix открыты.
- Post-reap stdout/stderr drain timeout наблюдался повторно31октября; успешные
  replays не доказывают устранение причины. [Диагностический шаг](engine/PLAN.md#arch2e4b-drain-diagnostics).
- Preview остаётся fixed-camera/GPU Windows profile; project switch — stopped/idle.
  Cooperative stop не прерывает произвольный зависший user Task/driver.
- Общая supported configuration, warnings policy и release scope ещё требуют
  решения. Произвольные OS/RID/AOT/потеря питания и долгие trends не обещаются.

## Следующий конкретный шаг

После Runtime/primitive ECS, CPU definitions f.1 и resource owners f.2 текущая очередь —
[ARCH1f.3](engine/PLAN.md#arch1f-rendering): camera/mesh/material extraction и bounded
owned snapshots, revision consistency/drop/coalesce/completion leases; f.4 — surface
renderer и required-view Runtime Playing fence.
Explicit Runtime ECS consumer и прежний primitive renderer уже подключены;
миграция preview, новый frame renderer и full checkpoint ещё впереди.

Сохранённый предложенный маршрут — **ежедневный Editor**: group reset Transform,
затем единицы углов, затем camera/picking; детали только в
[Editor плане](engine/EDITOR_PLAN.md#current-editor-queue).
Physical input acceptance можно закрывать параллельно при подходящем вводе;
она ограничивает заявление о готовности, но не назначает бесконечный повтор одного smoke.

Альтернативный performance трек — ED6b.2e.4f, снижение CPU/allocations batch при
сохранении выигрыша latency. Reliability/release трек — текущий full-solution
Debug/Release baseline, диагностируемый drain failure и затем fresh package/native
matrix на явно выбранном scope. Это варианты очереди, не объявление их реализации.
Game AI/agents, Player/HUD и новые протоколы остаются последующими направлениями.
