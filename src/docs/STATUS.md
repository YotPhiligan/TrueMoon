# TrueMoon: общий статус

**2026-11-11 — CORE7.0–7.6 завершены в configuration scope.**
[Рабочее руководство](core/CONFIGURATION_GUIDE.md), [139keys/17schemas](core/CONFIGURATION_KEYS.md),
[единый App/child runner](../ManualTests/ConfigurationDelivery/README.md),
[точные commands/limits](core/HISTORY.md#core76-2026-11-10).

Source solution Debug0errors/256warnings, Release0errors/190warnings.
7suites:6945/6945 каждый D/R, Failed:0, Skipped:0; Aether3234/3234 D/R
в текущем последовательном запуске с bounded test props override.
Это current passing на указанных inputs; ordinary nested restore и причины
старых diagnostics timeouts не объявлены исправленными.

16fresh packages одной версии;5programs×source/published/fresh,230checks/profile,
Cobalt/MS DI и4owned children/profile (12total). Fresh graphs0ProjectReference,
metadata/payload/runtime hashes проверены. Guide example исполнен,139keys
экспортированы из public metadata,8managed surfaces unchanged.
1673/1673baseline C#/csproj сохранены, source0.2.5.2-alpha не менялась.

Предкоммитная сверка11ноября дополнительно прошла: Cobalt179/generator96,
Enerit.Generator114 и Alloy1224 каждый D/R (1613cases/profile, failed/skipped0).
[Отдельные команды/inputs](HISTORY.md#precommit-2026-11-11). CORE7.6 collection
не повторялась, C#/csproj SHA unchanged. Это не один all-solution test run.
Новый native/GPU/physical acceptance не заявляется. Engine f.4 и остальные module/reliability/release очереди независимы.
[Предыдущая сводка целиком](HISTORY.md#status-before-core76-2026-11-10).

## Состояние модулей

| Направление | Что реализовано в проверенном scope | Что дальше у владельца |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE7.0–7.6 complete;6945checks/suite collection D/R, current16package230checks/profile и actual App/child delivery | Применять [guide](core/CONFIGURATION_GUIDE.md); CORE6 routing/metrics/quota отдельно |
| [Cobalt](cobalt/STATUS.md) | Runtime/generator/DI adapter/ownership, COB6e и COB5a.1 no-capture cycle check проверены | Измеренные allocation gains подтверждены; следующий measured backlog у Cobalt, scopes/массовая миграция не назначены |
| [Enerit](enerit/STATUS.md) | Same-build pipes/admission/stop/telemetry; CORE7.5 common limits, runtime354 D/R | ENE6c при расширении протокола; scalar CPU/allocations и queue distribution отдельно |
| [Mithril](mithril/STATUS.md) | Protected typed launch;155 D/R, current source/published/fresh App/child proof, stop/reap; lifecycle сохранён | MIT6 fault/containment/resource-trend; не второй restart поверх Aether |
| [Alloy/Argentis](alloy/STATUS.md) | Window/typed editors/styles/typography, UIA3d validation/shadows и UIA3e SplitView проверены в своих source/publish/package scopes | Studio интегрирует SplitView у consumer; PA8c.0 visual baseline и physical/release gates отдельно |
| [Aether/Editor](engine/STATUS.md) | Existing Editor/f.1–f.3; CORE7.6 full Aether3234 и Editor1496 D/R на указанном input profile, fresh bootstrap child proof | [ARCH1f.4](engine/PLAN.md#arch1f-rendering) actual surface; ordinary fixture restore/reliability и дальнейший Editor отдельно |
| [Nom/storage](nom/STATUS.md) | NOM0–NOM8, subsystem namespaces; CORE7.5 common admission,874cases D/R, actual stores в both-DI module consumer | Engine g.2 после g.1 по согласованию; VM verification отложена, power-off исключён; wider release отдельно |

## Общие этапы

G1–G3 и ARCH2 имеют bounded source/package/own-child proofs; это не универсальная
готовность. G4 UI alpha частичен: K7 и physical/device-loss/VRAM/final matrix открыты.
G5 продолжает прикладной Editor; весь ED1–ED8/Player/HUD/agents не завершён.
G6/R1–R6 не закрыты; alpha version сама по себе ничего не подтверждает.

## Общие проверки — датированные сведения

Результаты разделены по датам/scope; прежние модульные/full/frame проверки не считаются повторёнными текущей задачей.

| Дата и scope | Последний относящийся результат | Где подробности |
| --- | --- | --- |
| 2026-11-11, CORE7.6 итог | Solution D/R0errors;7suites6945/6945 D/R;16packages230checks/4owned children на source/published/fresh profile; metadata16/16,8managed APIs unchanged | [Core evidence/границы](core/HISTORY.md#core76-2026-11-10) |
| 2026-11-10, CORE7.5 | Six full suites D/R,110newcases; Aether47scoped serial D/R, full attempts non-green. Source/published/fresh16packages76checks/profile, оба DI; API removed0 в7assemblies | [Core evidence/limits](core/HISTORY.md#core75-2026-11-10) |
| 2026-11-10, CORE7.2 | Core554/554 D/R,48newcases; source/published/fresh6package36checks/profile, оба DI; ordinary incremental solution Debug0errors/855warnings | [Core scope](core/HISTORY.md#core72-2026-11-10) |
| 2026-11-10, CORE7.1 | Core506/506 D/R,108newcases; source/published/fresh3package consumer15checks/profile; incremental solution Debug0errors/236warnings | [Core scope](core/HISTORY.md#core71-2026-11-10) |
| 2026-11-09, snapshots/observer |1376scoped D/R и fresh7pkg CPU21checks/profile/owners0; Editor1468 D/R, workload allocations−3.46/−3.51%; solution Debug0errors/1556warnings. Full Aether non-green отделён | [Scope](engine/HISTORY.md#render-snapshots-2026-11-09) |
| 2026-11-09, ED5b и workspace build | Full Editor1465/1465 D/R; source/published/fresh-package native4profiles по5PNG/4reaped children, owners0/validation0/0. Solution nonincremental Debug0errors/1556warnings | [Translation](engine/HISTORY.md#ed5b-translation-2026-11-09) |
| 2026-11-09, COB5a.1 | Runtime/adapter179/generator96 каждый D/R; frozen paired allocations и40behavior scenarios; timing exploratory | [Cobalt](cobalt/HISTORY.md#cob5a1-production-2026-11-09) |
| 2026-11-08, Nom VM harness |98guards/10host protocol groups D/R,120functional/profile; VM0trials/Blocked image; hardware excluded; solution0errors/1560warnings | [Scope](nom/HISTORY.md#nom-vm-2026-11-08) |
| 2026-11-08, Nom UAC |5real-volume cases/166assertions D/R, free0/OS112, reopen/liveness; VHDX detached/cleanup0; power-loss open | [Scope](nom/HISTORY.md#nom7-uac-2026-11-08) |
| 2026-11-08, Nom physical follow-up |24pure guards/12assertions D/R; fresh package proof passed; physical0cases, New-VHD denied, exit2/Blocked; power-loss open | [Scope](nom/HISTORY.md#nom7-durability-2026-11-08) |
| 2026-11-08, Nom NOM7 |718/718 D/R;120functional/profile, App34source/published; fresh4pkg App34/tool211checks, portable/win-x64 FDD, negative controls; solution0errors/1555warnings | [NOM7](nom/HISTORY.md#nom7-2026-11-08) |
| 2026-11-08, Nom NOM6 |718/718 D/R+discovery;120functional+4Capacity/profile,16Backup kills/reaps; App34checks; actualmaxima/workload; solution0errors/1555warnings; physical gate открыт | [NOM6](nom/HISTORY.md#nom6-2026-11-08) |
| 2026-11-08, Nom NOM5 |628/628 D/R+discovery;104groups/profile, Retention12kills/reaps; public App27checks; solution0errors/1555warnings; physical gate открыт | [NOM5](nom/HISTORY.md#nom5-2026-11-08) |
| 2026-11-08, Nom NOM4 |536/536 D/R+discovery;92groups/profile, Maintenance27/24kills+reaps; public App16checks; solution0errors/1555warnings; physical gate открыт | [NOM4](nom/HISTORY.md#nom4-2026-11-08) |
| 2026-11-08, Nom NOM3 |426/426 D/R+discovery;65console groups/profile,48kills/reaps/Fault profile; App10checks/profile; solution0errors/1555warnings; physical gate открыт | [NOM3](nom/HISTORY.md#nom3-2026-11-08) |
| 2026-11-08, Nom NOM2 |382/382 D/R и solution-filter;18core+6KV groups source/published, public App10checks/profile; no LiteDB graphs/output; solution Debug0errors/1555warnings, Nom0warnings | [Catalog cutover](nom/HISTORY.md#nom2-2026-11-08) |
| 2026-11-07/08, default typography | Alloy1126/Hosting154/Editor1300 каждый D/R;4native Editor profiles и default3backend source/publish gallery, exact fresh4package raster consumer. Сверка TRX/JSON/source SHA08ноября; новые build/test не запускались | [PA8c.3b](alloy/HISTORY.md#default-fonts-2026-11-08) |
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

Последний nonincremental solution Debug build —8ноября0errors/1555warnings; Nom warnings0.
Проверки разных модулей и дат не суммируются в один solution run. Ссылки на старые
ignored TestResults могут отсутствовать в checkout; HISTORY сохраняет scope,
а повторяемые runners — в ManualTests. Для нового release нужен новый evidence set.

## Общие блокеры и неопределённости

- Nom: actual VM crash verification отложена пользователем; hardware power-off исключён. Предыдущий isolated-volume disk-full UAC proof проверен отдельно; [scope](nom/DURABILITY.md).

- Current CORE7.6 full Aether3234/3234 D/R прошёл последовательно с bounded
  test props override. Ordinary nested restore без него не объявлен исправленным;
  старые non-green attempts и diagnostics причины сохраняются отдельно.
  [Точный input profile/commands](core/HISTORY.md#core76-2026-11-10).
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
- Preview имеет camera/selection и translation v1 в bounded primitive GPU Windows profile; project switch — stopped/idle.
  Cooperative stop не прерывает произвольный зависший user Task/driver.
- Общая supported configuration, warnings policy и release scope ещё требуют
  решения. Произвольные OS/RID/AOT/потеря питания и долгие trends не обещаются.

## Следующий конкретный шаг

[ARCH1f.4](engine/PLAN.md#arch1f-rendering): actual Vulkan mesh/material surface
renderer и required-view Runtime Playing fence поверх готовых resource/snapshot
owners. Importer g.1 и audio h.0 остаются самостоятельными этапами; Nom g.2 после g.1.

Editor ED5b v1/observer4f завершены; расширение tools требует отдельного scope.
Reliability: изолировать native game fixture SDK inputs от legacy repo CLI tools,
стандартизовать ordinary fixture restore и получить причинное evidence прежнего drain timeout.
Current full baseline на bounded test inputs записан в CORE7.6.
Physical input, device loss/K7/VRAM и release matrix сохраняют отдельные gates.
