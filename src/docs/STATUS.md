# TrueMoon: общий статус

**2026-11-11 — CORE8.5 завершён; согласованный переход на .NET 11 проверен.**
[Поставка и команды](core/HISTORY.md#core85-delivery-2026-11-11), [миграция](core/DOTNET11_MIGRATION.md).
SDK 11.0.100 / net11.0 / runtime-async=on. Созданы 16 свежих локальных packages
одной версии (0.3.0.1-alpha.core85.20261111221352), проверены runtime/analyzer/native payloads и графы.
App/configuration: 15 source/published/fresh profiles, 690 checks / 12 children,
оба DI. Source/published example hosts — 4 cases, package game — 8 cases,
fresh native GPU viewport — 2 profiles. Remote publication не выполнялась.

Все 11 suites заново прошли Debug и Release: **Passed 8567, Failed 0, Skipped 0
на конфигурацию**. Ordinary solution builds D/R прошли. В CORE8.5 менялись
runners и документация; C# runtime, API, SDK pin и зависимости сохранены.
Physical/release/performance и причины прежних diagnostics/drain timeout
остаются отдельными. [Предыдущая сводка](HISTORY.md#common-status-before-core85-2026-11-11).

## Состояние модулей

| Направление | Что реализовано в проверенном scope | Что дальше у владельца |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE8.0–5 завершены: 16 согласованных packages, source/published/fresh App и actual child/native consumers, все 11 suites по 8567 тестов D/R | Функциональная очередь ARCH1f.4; Core/diagnostics follow-up отдельно |
| [Cobalt](cobalt/STATUS.md) | CORE8.2 net11: runtime/adapter179 и generator96 D/R; package generation; прежние COB6e/COB5a.1 сохранены | Измеренные allocation gains подтверждены; следующий measured backlog у Cobalt, scopes/массовая миграция не назначены |
| [Enerit](enerit/STATUS.md) | CORE8.2 net11: runtime354 и generator114 D/R, actual generated IPC/package proof; same-build contracts сохранены | ENE6c при расширении протокола; scalar CPU/allocations и queue distribution отдельно |
| [Mithril](mithril/STATUS.md) | Protected typed launch;155 D/R, current source/published/fresh App/child proof, stop/reap; lifecycle сохранён | MIT6 fault/containment/resource-trend; не второй restart поверх Aether |
| [Alloy/Argentis](alloy/STATUS.md) | Window/typed editors/styles/typography, UIA3d validation/shadows и UIA3e SplitView проверены в своих source/publish/package scopes | Studio интегрирует SplitView у consumer; PA8c.0 visual baseline и physical/release gates отдельно |
| [Aether/Editor](engine/STATUS.md) | CORE8.3 net11 frozen game profile; full Aether3243/Editor1496 D/R, source/published SDK11-only compile/reuse/load/Stop/reap | CORE8.5 завершён; [ARCH1f.4](engine/PLAN.md#arch1f-rendering) surface renderer и прежние reliability gates отдельно |
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
| 2026-11-11, CORE8.5 | 16 packages; 690 App/configuration checks; 4 source/published host cases, 8 package game cases, 2 fresh native profiles; все 11 suites D/R по 8567/8567, failed/skipped 0 | [Команды/границы](core/HISTORY.md#core85-delivery-2026-11-11) |
| 2026-11-11, CORE8.4 | 11 suites: 8567/8567 D/R, failed/skipped 0; 24 source/published native сценария; solution D/R без ошибок | [Команды/границы](core/HISTORY.md#core84-runtime-2026-11-11) |
| 2026-11-11, CORE8.3 |Full Aether3243/Editor1496 D/R; SDK11-only source/published game compile/reuse/load/Stop/reap,2children; solution D/R0errors | [Команды/границы](core/HISTORY.md#core83-frozen-2026-11-11) |
| 2026-11-11, CORE8.2 |4suites743/743 D/R,8fresh packages132DI/IPCscenarios; solution Debug0errors/510warnings, Release0errors/1414warnings, incremental | [Команды/исправления/границы](core/HISTORY.md#core82-generators-2026-11-11) |
| 2026-11-11, CORE8.1 profiles/restore | 45 runners, 61 net11 projects; external NU1008 исправлен; solution D/R0errors,16packages/15profiles/230checks и4children на вид поставки | [Inputs/команды/границы](core/HISTORY.md#core81-profiles-2026-11-11) |
| 2026-11-11, CORE8.1 TFM |61/61net11 evaluation,8profile controls, solution и6extra projects D/R0errors; manual NU1201 устранён | [Команды/границы](core/HISTORY.md#core81-tfm-2026-11-11) |
| 2026-11-11, CORE8.0 | Ordinary restore0/build D/R0errors/1556warnings;61evaluation,4manual NU1201 и1/1failed actual game build test | [Входные условия/команды/границы](core/HISTORY.md#core80-baseline-2026-11-11) |
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

- .NET 11: CORE8.0–5 завершены в описанном Windows x64 framework-dependent профиле. Source build/test, свежая согласованная поставка и actual source/published/fresh consumers проверены. Расширенная release/OS/RID/AOT матрица остаётся отдельной.

- Nom: actual VM crash verification отложена пользователем; hardware power-off исключён. Предыдущий isolated-volume disk-full UAC proof проверен отдельно; [scope](nom/DURABILITY.md).

- CORE7.6 Aether 3234/3234 D/R с test props override остаётся историческим результатом.
  CORE8.3/4 подтвердили ordinary nested restore и полный Aether 3243/3243 D/R
  без него. Старые non-green attempts и причины diagnostics timeout сохраняются
  отдельно. [Исторический профиль](core/HISTORY.md#core76-2026-11-10),
  [текущая проверка](core/HISTORY.md#core84-runtime-2026-11-11).
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

**CORE8 завершён.** Следующая функциональная очередь — ARCH1f.4 ниже; новые задачи требуют своего scope и проверок. [Итоговая поставка](core/HISTORY.md#core85-delivery-2026-11-11).


[ARCH1f.4](engine/PLAN.md#arch1f-rendering): actual Vulkan mesh/material surface
renderer и required-view Runtime Playing fence поверх готовых resource/snapshot
owners. Importer g.1 и audio h.0 остаются самостоятельными этапами; Nom g.2 после g.1.

Editor ED5b v1/observer4f завершены; расширение tools требует отдельного scope.
Reliability: текущие CORE8.3/4 full net11 проверки используют ordinary nested restore,
без test-only props. Причинное evidence прежнего post-reap drain/diagnostics timeout
остаётся отдельным шагом; исторический net10 baseline записан в CORE7.6.
Physical input, device loss/K7/VRAM и release matrix сохраняют отдельные gates.
