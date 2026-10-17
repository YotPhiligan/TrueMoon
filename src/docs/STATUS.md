# TrueMoon: общий статус

Последняя работа — **2026-10-17, ENG2 CPU viewport**.
[Scope/layout](engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17),
[commands/evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17),
[engine статус](engine/STATUS.md), [quality matrix](QUALITY.md), [общий план](PLAN.md).
Probe-only CPU ring/receiver и одно Alloy/OpenGL окно отображают кадры owned child;
UI получил owned BGRA pixel image factory. Authoring state и Editor App сохраняются.

Engine87/87 и UI570/570 Debug/Release; full Debug1368/1368,8TRX/failed0/skipped0.
Native34/34 Debug apphost/published Release:32ENG1 regressions +2viewport runs,
включая по20Start/Stop, actual GL pixels, resize/EOF/deadline/native close cleanup.
Build0errors/1211warnings. Следующий code этап ENG3 versioned replacement;
checkpoint/compiler/20Replace ещё не реализованы. UI hardware blockers, общий release
и [отложенный RTE backlog](alloy/RICH_TEXT_EDITOR_PLAN.md) сохраняются.
## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-15 MIT5:94/94 Debug и Release,40restart +18shutdown,108fresh package cases; diagnostics проверены в96cases с живым parent | Bounded own-child/cooperative scope; game session supervisor и external restart не объявлены готовыми |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-17:RichTextEditor/options реализован; UI563/563 Debug/Release,6native demo cases, прежний golden360/360 с validation teardown;10fresh nupkgs/36App +4direct с rich text | PA7a bounded scope; IME/full bidi/rich clipboard вне реализации. K3/K7/K8/VRAM/final matrix открыты; physical UI acceptance отдельно |
| [Engine / Editor](engine/STATUS.md) |2026-10-17:ENG1/ENG2 CPU ring/OpenGL viewport; engine87/UI570 Debug/Release,34native cases, по20Start/Stop, actual pixels/resize/EOF/deadline/cleanup | ENG3 compiler/checkpoint/versioned replacement/rollback; probe-only, packages/3D/long resource/performance proof ещё нет |

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

Последний общий build — **2026-10-17, ENG2**, nonincremental Debug:
**0errors/1211warnings**. Full Debug solution1368/1368:
Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/UI570/engine87,
8TRX/failed0/skipped0. Отдельно Release engine87 и UI570; полный Release solution
не выполнялся. UI golden/package/RichTextEditor native matrix этим run не повторены.
[Команды и логи](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17).

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

1. ENG1/ENG2 реализованы и проверены в bounded probe scope.
   Следующий code шаг — [ENG3](engine/STATUS.md#следующий-конкретный-шаг):
   compiler/build-ID каталоги, checkpoint и versioned replacement/rollback,
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
