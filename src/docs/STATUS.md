# TrueMoon: общий статус

Последняя работа — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. Общий
transport/presentation contract подключён к Start/selected DLL/Build/Replace/
checkpoint/rollback; explicit GPU сохраняет обычный View/commands, CPU default.
Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,9TRX/
failed0/skipped0. Build0errors/1499warnings; fresh12packages/4App consumers.
Native54cases/56commands,406source/76binary fingerprints; per GPU Editor20Replace/20failures/53children, validation0/0, imported readback0.
[Команды/границы](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед 3D. K3/K7/K8/VRAM/final alpha
сохраняются; drivers/Windows settings/K7 stop policy прежние.

## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-15 MIT5:94/94 Debug и Release,40restart +18shutdown,108fresh package cases; diagnostics проверены в96cases с живым parent | Bounded own-child/cooperative scope; game session supervisor и external restart не объявлены готовыми |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-19:INF1–INF4/INF6/INF7/минимальный INF5; UI613/Hosting79 Debug/Release,21runner guards,24native processes,10fresh packages. [INF7 evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19); golden360 — INF3 | Bounded infrastructure завершена; далее общий ENG4e baseline. Full INF5 частичен; PA7a bounded scope, K3/K7/K8/VRAM/final matrix открыты |
| [Aether / Engine](engine/STATUS.md) |2026-10-20:ENG3a–d/ENG4a–d2 ordinary GPU Editor; собственные проверки в engine STATUS/HISTORY | ENG4e measured CPU/GPU baseline до 3D; API experimental, publication/3D readiness нет |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Эта таблица
сводит разные предыдущие runs и не является одним full-solution regression pass.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Headless, units и fresh UI consumer подтверждены отдельными runs; ENG1 sessions и ENG2 CPU viewport проверены отдельно, ENG3 replacement/Editor proof завершён в bounded CPU scope; shared GPU/3D предстоят |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний full solution run — **2026-10-20, ENG4d2**: Debug1898/1898,
9TRX/failed0/skipped0: Core107/Cobalt179/generator91/Enerit126/generator114/Mithril94/
UI615/Hosting79/Aether493. Отдельно Aether493 и UI615 Debug/Release; 54native cases.
Nonincremental Debug0errors/1499warnings; fresh12packages/4Aether+Skia App consumers.
Full Release solution/golden/RTE/performance/hardware matrix не повторялись;
packages проверены в ограниченном GPU API/native delivery scope.
[Команды/evidence](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Прежние проверки сохраняют собственные даты/scopes в [HISTORY](HISTORY.md) и
модульных HISTORY; passing одного модуля не переносится на другие.

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
   ENG3a–d compiler/checkpoint/Replace/Editor commands и20Replace proof выполнены2026-10-20.
   ENG4a capability decision/read-only native probe выполнен в bounded scope.
   ENG4b one-frame import/completion/pixels/failure/cleanup проверен с validation.
   ENG4c slots/surface lifecycle/retry/queued resize проверен с validation.
   ENG4d borrowed texture/retained GPU copy/actual viewport bridge проверен.
   ENG4d2 ordinary GPU Editor/selected DLL/checkpoint/rollback выполнен.
   Следующий общий code шаг — [ENG4e](engine/STATUS.md#следующий-конкретный-шаг):
   сопоставимый CPU/GPU latency/allocations/resource baseline до 3D.
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
