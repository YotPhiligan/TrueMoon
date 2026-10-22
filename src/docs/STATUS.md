# TrueMoon: общий статус

Последняя работа — **2026-10-22, UIA1 View/Data/ViewModel и отдельные typed styles**.
Подход закреплён в AGENTS; Argentis предоставляет IViewStyle<TView>, scoped
root/part/roles и retained theme/style updates. Aether.Editor использует отдельные
EditorViewModel/EditorViewStyle, Fluent tree/bindings и owner-thread DI factory.
Cobalt default, Microsoft DI compatibility и DI1–DI4 ownership сохранены.
[Контракт](alloy/PLAN.md#uia1-view-dataviewmodel-и-scoped-styles--2026-10-22),
[tests/commands/границы](alloy/HISTORY.md#uia1-view-viewmodel-и-typed-styles--2026-10-22).

Текущий full solution: **2321/2321 Debug и2321/2321 Release**,10TRX/configuration,
failed/skipped0; добавлены28cases. Nonincremental Debug build0errors/1520warnings.
Release publish/native Editor Debug и published Release прошли: по8commands/3PNG/
2reaped children, validation0/0 и zero owned resources. Fresh14packages/20commands/
4Cobalt+DI consumers проверили реальный styles API и engine ownership. Physical
DPI/K3/K7/K8/VRAM/final alpha, полный golden/performance scope этим не закрываются.

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-22:Contracts typed factories/adapters и borrowed lifecycle; Core117/117 Debug/Release | Provider-neutral contracts, cooperative/platform limits прежние |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-22:runtime179/generator91 Debug/Release; Aether consumer factory/ownership tests на обоих providers | Runtime/generator/adapter API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-22:typed pipes/common factory registrations; runtime126/generator114 Debug/Release, реальные App+units+pipes | Same-build pipes scope, ENE5/MMF/schema/backpressure отдельно |
| [Mithril](mithril/STATUS.md) |2026-10-22:injected handle factory/borrowed aliases,99/99 Debug/Release, composition/process checks | Own-child budgets/policies прежние; standalone restart matrix отдельно |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-22:UIA1 typed view styles/VM; UI661/Hosting85/Editor49 Debug/Release, native и fresh packages | Explicit bindings/retained tree; physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) |2026-10-22:UIA1 Editor model/style/DI factory; Aether800/Editor49 Debug/Release, native и package consumers | Engine contracts прежние; ENG7 editing/ENG5c.3 collisions, World/edit/MCP/performance открыты |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Текущий
общий run указан ниже; отдельные historical/package/performance scopes не смешиваются.

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

Последний full solution run — **2026-10-22, UIA1 view authoring**:
Debug2321/2321 иRelease2321/2321, по10TRX, failed0/skipped0: Core117/Cobalt179/
generator91/Enerit126/generator114/Mithril99/UI661/Hosting85/Aether800/Editor49.
Nonincremental Debug build0errors/1520warnings; Release publish, native Debug/
published Release и fresh bounded package proof прошли.
[Точные команды/evidence/failures](alloy/HISTORY.md#uia1-view-viewmodel-и-typed-styles--2026-10-22).

Предыдущие reference performance12cases, GPU validation6 и Mithril native20 —
отдельные [ENG4e scopes/failures](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Прежние nonincremental0errors/1499warnings и fresh structural consumers находятся
в [предыдущей записи](HISTORY.md#исправление-структуры-проектов--2026-10-21).
Golden/RTE/full UI performance/hardware/release acceptance не повторялись;
исторические результаты сохраняют даты и ограничения у владельцев.

## Общие блокеры и неопределённости

- Aether DI1–DI4 закрыты в проверенном consumer/dependency scope; внешние games
  должны добавить explicit entry factory/Contracts reference и пересобрать artifacts.
  Массовая миграция всех independent consumers и полная framework readiness не заявлены.
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
