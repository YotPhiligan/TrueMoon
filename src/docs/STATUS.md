# TrueMoon: общий статус

Последняя работа — **2026-10-22, ENG7a основной экран TrueMoon.Editor** по запросу
пользователя: отдельное App application, стандартная borderless рамка Alloy,
central3D viewport, scene selection/read-only inspector и рабочие Aether commands.
[Приложение](../TrueMoon.Editor/README.md), [scope](engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22),
[результаты](engine/HISTORY.md#eng7a-основной-экран-редактора--2026-10-22).

Текущий full solution: **2253/2253 Debug и2253/2253 Release**,10TRX/configuration,
failed/skipped0. Editor39/Aether780/UI643/Hosting79. Editor Debug/published Release
native: по8commands/3PNG/2reaped runtime children, Vulkan validation0/0 и zero owned
resources. Предыдущие PA8a golden/packages и engine native60 не повторялись.
UI/property editing, tree/picking/input и project persistence остаются следующими
ENG7 задачами; engine gameplay продолжение — ENG5c.3 collisions, затем HUD.
Physical DPI/K3/K7/K8/VRAM/final alpha прежние. Предыдущие сводки сохранены в HISTORY.

## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-21:95/95 Debug/Release, graceful delivery race red/green,20native shutdown и108fresh package cases; own standalone restart matrix отдельно не повторена | Прежний bounded own-child/cooperative scope, budgets не увеличены; external restart не объявлен готовым |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-22:ENG7a Editor consumer; UI643/Hosting79/Editor39 Debug/Release и native default-frame app. UI production код прежний | PA8a автоматический scope выполнен; current Editor39 отдельно от UI643. Physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) |2026-10-22:ENG7a standalone Editor UI/viewport/commands/readonly inspector, Aether780/Editor39 Debug/Release, новый native app proof | Далее ENG7 authoring property editing/tree/input; gameplay точка ENG5c.3 collisions. Full World/edit/MCP/performance открыты |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Эта таблица
сводит разные предыдущие runs и не является одним full-solution regression pass.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Headless, units и fresh UI consumer подтверждены отдельными runs; bounded CPU/GPU Editor/Replace, ENG5a3D renderer и ENG5b input/camera проверены; комната/HUD/standalone предстоят |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний full solution run — **2026-10-22, ARCH1a.2**:
Debug2098/2098 иRelease2098/2098, по9TRX, failed0/skipped0: Core107/Cobalt179/
generator91/Enerit126/generator114/Mithril95/UI615/Hosting79/Aether692.
Incremental dotnet test builds; nonincremental build не повторён.
[Точные current commands/evidence](engine/HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
Предыдущие reference performance12cases, GPU validation6 и Mithril native20 —
отдельные [ENG4e scopes/failures](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Прежние nonincremental0errors/1499warnings и fresh structural consumers находятся
в [предыдущей записи](HISTORY.md#исправление-структуры-проектов--2026-10-21).
Golden/RTE/full UI performance/hardware/release acceptance не повторялись;
исторические результаты сохраняют даты и ограничения у владельцев.

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

1. Пользовательские UI шаги PA8a и ENG7a выполнены в указанном scope; следующий
   локальный editor этап — authoring property editing через API. При возврате
   к движку продолжить [ENG5c.3 collisions камеры](engine/PLAN.md#eng5c3-следующий-шаг-collisions-камеры),
   затем HUD. ARCH1a.1/2 и ENG5c.1/2 сохраняют свой реализованный scope;
   full runtime snapshot/edit/MCP,3D performance и reference allocation debt открыты.
2. Для UI physical DPI/OS input, real loss/K8, VRAM и final alpha требуют собственных
   запусков. K7 workaround search остаётся остановленным; Windows/driver settings
   без запроса не менять. PA8a эти ограничения не снимает.
3. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation сохраняют
   отдельные планы. Новая массовая migration этим шагом не начинается.

## Изменения формата — 2026-10-09

Исходная история разделения планов и проверок сохранена в
[архиве общего статуса](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Обновление2026-10-15 сократило STATUS до текущей сводки и синхронизировало очередность;
критерии модулей, G1–G6/R1–R6 и UI K1–K9/PA1–PA10 не отменены.
