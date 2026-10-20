# TrueMoon: общий статус

Последняя проверка — **2026-10-19, аудит App entry points остальных модулей**.
Enerit/Mithril/Alloy.Hosting имеют публичные extensions; Cobalt встроен в App,
UseDI выбирает provider на builder. Utilities/contracts/UI layers/generators
сохраняют собственные границы, пропущенных functional entry points не найдено.
Focused Debug: Core46/Enerit13/Mithril9/Hosting38,106passed/failed0/skipped0,
4TRX; runtime code не менялся. Это отдельная проверка, без нового full solution/
Release/native/package запуска. [Таблица API и команды](HISTORY.md#аудит-точек-подключения-модулей-к-app--2026-10-19).
Следующий общий code шаг — ENG3a.

Последняя работа — **2026-10-19, Aether App integration**. UseAether/AetherHost
связывают engine services, options и cleanup с App; headless/viewport examples
используют эту точку подключения. Общее правило закреплено в
[AGENTS.md](../AGENTS.md#подключение-модулей-к-app).
Aether115/115 Debug/Release, full Debug1518/1518/9TRX/failed0/skipped0,
36native cases и4consumers из9fresh packages прошли. Aether IsPackable=true;
публикации не было, ENG3/replacement не реализован.
[Единственные подробные команды/evidence](engine/HISTORY.md#app-integration-реализация--2026-10-19).
Следующий общий code шаг — ENG3a; UI hardware/release ограничения сохраняются.

Предыдущая работа — **2026-10-19, NuGet metadata audit**. Проверены44projects:
13packable libraries/analyzer и подготовленный Aether с IsPackable=false.
Дополнены описания/tags/собственные package README; repository/project metadata и
asset paths централизованы.14локальных archives (13ready +1deferred preview)
проверены по nuspec и payload hashes, без публикации и новых unit/native runs.
[Проверка](../eng/Check-PackageMetadata.ps1),
[scope/evidence](HISTORY.md#nuget-package-metadata--2026-10-19).
Следующий engine code шаг остаётся ENG3a; общий release не объявлен готовым.

Предыдущая работа — **2026-10-19, TrueMoon.Aether**: engine implementation/contracts
объединены в одну сборку с TrueMoon префиксом; tests — TrueMoon.Aether.Tests.
Editor/Game.Runtime и Game.Logic остались отдельными consumers. Aether87/87
Debug/Release, full Debug1490/1490/9TRX/failed0/skipped0 и34native cases прошли.
Новый module не зависит от UI/Game.Logic, остаётся experimental/IsPackable=false.
[Границы/команды/evidence](engine/HISTORY.md#truemoonaether--2026-10-19).
Следующий code шаг — ENG3a в Aether; compiler/replacement ещё не реализованы.

Предыдущая работа — **2026-10-19, сверка плана после INF7**. Очередность ENG3
сохранена, [детализация ENG3a–d](engine/PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19)
связана с фактическими loader/protocol/frame paths; исправлен устаревший ENG1
в отложенном RTE плане. Следующий конкретный code шаг — ENG3a compiler/artifact.
Это документационная работа без новых build/tests/native/performance результатов.

Последняя реализация — **2026-10-19, UI INF7**: воспроизводимый integration runner
Assembly/Source/Package/All и отдельный read-only verifier. Production API прежний.
[Запуск](../ManualTests/AlloyFactoryConsumer/README.md#inf7-воспроизводимый-integration-runner),
[контракт/evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).
UI613/Hosting79 в каждой Debug/Release,4TRX/1384passed/failed0/skipped0;
21runner guard checks,24native processes и10fresh packages прошли. Source/package
проверены на Cobalt/DI×OpenGL/Vulkan×Debug/published Release; package graph без
Cobalt analyzer/view registration. Standalone Assembly и повторный verifier прошли.
Нового full-solution/nonincremental build/golden/engine viewport/native RTE/
performance запуска нет; full Debug1490 относится к предыдущему INF6.
Bounded INF0–INF4/INF6/INF7 и минимальный INF5 завершены; далее общий ENG3.
Full INF5 частичен, RTE/K3/K7/K8/VRAM сохраняются.

Предыдущая работа — **2026-10-18, UI INF3**: on-demand/continuous scheduling,
RequestFrame/work wake, cached expose, suspended work/resume и bounded shared wait.
[Contract/roadmap](alloy/PLAN.md#inf3inf6-условия-интеграции),
[commands/failures/evidence](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).
UI613/613 и Hosting39/39 Debug/Release; повторный full Debug1450/1450,9TRX/failed0/skipped0.
Первый full1449/1450 упал на независимой Mithril readiness sharing race; targeted20
и full94Mithril retry прошли, race не исправлялась. Build0errors/1211warnings.
Fresh7packages/2independent runs и10native processes Debug/published Release:
scheduling/legacy multi-window, external HUD, read-only golden360 и2ENG2 viewport
прошли с validation/cleanup в своих scopes. CPU/GPU speedup не измерен.
Далее UI INF4, full INF5 частичен. Full Release solution,32headless ENG1,
прежняя full10package/40consumer и native RTE matrix не повторялись. Engine ENG3,
[отложенный RTE backlog](alloy/RICH_TEXT_EDITOR_PLAN.md) и K3/K7/K8/VRAM сохраняются.

Предыдущий engine собственный этап2026-10-17 — ENG2 CPU viewport;
[scope/layout](engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17),
[commands/evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17).
Engine87/87 и UI570/570 Debug/Release,34native cases и прежний full Debug1368
относятся к тому запуску; общий [quality summary](QUALITY.md) различает scopes.

## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-15 MIT5:94/94 Debug и Release,40restart +18shutdown,108fresh package cases; diagnostics проверены в96cases с живым parent | Bounded own-child/cooperative scope; game session supervisor и external restart не объявлены готовыми |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-19:INF1–INF4/INF6/INF7/минимальный INF5; UI613/Hosting79 Debug/Release,21runner guards,24native processes,10fresh packages. [INF7 evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19); golden360 — INF3 | Bounded infrastructure завершена; далее общий ENG3. Full INF5 частичен; PA7a bounded scope, K3/K7/K8/VRAM/final matrix открыты |
| [Aether / Engine](engine/STATUS.md) |2026-10-19:UseAether/App-owned host; Aether115/115 Debug/Release, full Debug1518,36native cases и4independent consumers из9fresh packages; по20Start/Stop и actual GL pixels/cleanup | ENG3a compiler/artifact, затем checkpoint/replacement; API experimental, публикации/3D readiness нет |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Эта таблица
сводит разные предыдущие runs и не является одним full-solution regression pass.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Headless, units и fresh UI consumer подтверждены отдельными runs; ENG1 sessions и ENG2 CPU viewport проверены отдельно, versioned replacement предстоит |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний full solution test run — **2026-10-19, Aether App integration**:
full Debug1518/1518,9TRX/failed0/skipped0; Aether115/115 Debug/Release,
36native cases и4independent consumers из9fresh packages.
Nonincremental Debug build0errors/1507warnings.
[Команды/evidence](engine/HISTORY.md#app-integration-реализация--2026-10-19).
Последующий App entry-point audit повторил только focused106Debug tests.

Предыдущий solution test run — **2026-10-19, Aether extraction**: Aether87/87 Debug/Release,
full Debug1490/1490,9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/
generator114/Mithril94/UI613/Hosting79/Aether87. Native34/34 Debug apphost/published
Release; по20Start/Stop, actual pixels и cleanup. Новый nonincremental build,
full Release solution, package/golden/RTE/performance/hardware matrix не выполнялись.
[Команды/evidence](engine/HISTORY.md#truemoonaether--2026-10-19).

Предыдущий **2026-10-19, INF7**: UI613/Hosting79 в каждой Debug/Release,
4TRX/1384passed/failed0/skipped0;21runner guards,24native processes,10fresh packages.
All46commands и отдельный Assembly прошли, evidence/fingerprints перепроверены.
Own suites остальных модулей и full solution в этом шаге не запускались.
[Команды/evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).

Предыдущий **2026-10-19, INF6**: full Debug1490/1490,9TRX/failed0/skipped0:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting79/engine87.
Release UI613/Hosting79 отдельно; source12native processes и8independent native
package consumers прошли. Новый nonincremental build/golden не запускались.
[Команды/evidence](alloy/HISTORY.md#inf6-явные-factories-и-совместимость--2026-10-19).

Предыдущий **2026-10-18, INF4**: full Debug1466/1466,9TRX/failed0/skipped0:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting55/engine87.
Отдельно Release UI613/Hosting55,10manual/engine native processes и2independent
native package consumers прошли. Nonincremental build в INF4 не запускался;
предыдущий такой build и golden ниже относятся к INF3.
[Команды/evidence](alloy/HISTORY.md#inf4-асинхронное-закрытие--2026-10-18).

Последний общий build — **2026-10-18, INF3**, nonincremental Debug:
**0errors/1211warnings**. Повторный full Debug solution1450/1450:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI613/Hosting39/engine87,
9TRX/failed0/skipped0. Отдельно Release UI613/Hosting39; полный Release solution
не выполнялся. Первый full1449/1450/Mithril93/94, затем targeted20/20 и полный
повтор прошли; [исходный failure не скрыт](mithril/HISTORY.md#readiness-file-race-при-ui-inf3--2026-10-18).
10native processes включают scheduling/HUD/golden и2ENG2 viewport; это не
32ENG1 headless/новый own performance proof остальных модулей.
[Команды и логи](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).

Предыдущий MIT5 этап2026-10-15: Mithril94/Enerit126 в каждой Debug/Release:
4TRX/440passed/0failed/0skipped;40restart +18shutdown +108package cases прошли.
Own Core/Cobalt/generator suites, UI/native и performance в MIT5 не повторялись.
[Команды и scope](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).

Предыдущий UI quality run2026-10-15:8TRX/1818passed/0failed/0skipped (UI532/Core107/Cobalt179/
generator91 в каждой Debug/Release),40package runtime processes и360golden comparisons.
Это отдельные проверки, не full-solution suite; full solution build в UI шаге не выполнялся.

Другие результаты — в [quality matrix](QUALITY.md#матрица) и локальных HISTORY.
Прежний длинный общий журнал [сохранён целиком](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Документационная сверка не создаёт нового passing build/tests/native результата.

## Общие блокеры и неопределённости

- UI package/Hosting/fonts gap закрыт2026-10-15; fresh40process proof не закрывает
  physical/final UI acceptance. Cobalt concrete View/model registrations обязательны
  для consumer source generation; автоматическая массовая UseDI migration не назначена.
- K7: предыдущий full240-window failed (GDI23→137/USER26→26). Raw trace связан с
  NVIDIA ICD32.0.16.1742. По решению пользователя workaround search остановлен;
  возврат при supported fix либо согласованной полностью проверенной alternate
  configuration. Raw AMD comparison не является full UI pass.
- Physical DPI100/200%/разные мониторы не были доступны в последнем проверенном
  окружении; real device/context loss, VRAM и final UI matrix не подтверждены.
- Cooperative shutdown не прерывает игнорирующий token user Task. Provider alias
  disposal semantics различаются. Same-build proofs не обещают другие OS/RID/AOT,
  долгие resource trends, engine frames/reload/session lifecycle.
- Общий supported configuration/SDK/OS scope, warnings policy и R1–R6 release
  acceptance остаются отдельными решениями; historical suites нельзя суммировать
  в current full-solution pass.

## Следующий конкретный шаг

1. UI INF0–INF4/INF6/INF7 и минимальный INF5 завершены в указанном scope.
   Full INF5 частичен. ENG1/ENG2 сохраняют bounded proof.
   Следующий общий code шаг — [ENG3a](engine/STATUS.md#следующий-конкретный-шаг):
   compiler job/immutable artifact и выбор build на каждый generation; далее
   checkpoint и versioned replacement/rollback,
   затем20Replace cycles при одном живом Editor App.
   UI K3/K7/K8/VRAM/full alpha сохраняются; PA1–PA10 автоматически не начинаются.
2. K7 workaround search остаётся остановлен по решению пользователя. Supported fix
   или согласованная alternate configuration требуют собственного полного proof;
   driver/Windows settings без соответствующего запроса не менять.
3. Cobalt allocation и Enerit scalar CPU/allocation investigation — отдельный
   measured backlog перед оптимизацией; default/adapter/migration решение прежнее.

## Изменения формата — 2026-10-09

Исходная история разделения планов и проверок сохранена в
[архиве общего статуса](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Обновление2026-10-15 сократило STATUS до текущей сводки и синхронизировало очередность;
критерии модулей, G1–G6/R1–R6 и UI K1–K9/PA1–PA10 не отменены.
