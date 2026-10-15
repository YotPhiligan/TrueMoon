# TrueMoon: общий статус

Последняя работа — **2026-10-15, сводная оценка качества перед движком**.
[Матрица и решение](QUALITY.md), [история сверки](HISTORY.md#матрица-качества-модулей--2026-10-15),
[общий план](PLAN.md). Сверены локальные планы/статусы и существующий UI package
consumer; новых code/build/tests/process/GPU/pack/performance запусков нет.

Рабочий bounded проход Core/Cobalt/Enerit/Mithril завершён в собственных scopes.
Следующий code этап — UI package/Hosting proof на текущих Core/Cobalt с explicit
fonts. Engine/editor пока запланированы. Полный UI alpha и общий release не закрыты.

## Состояние модулей

| Модуль | Предыдущий собственный результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) |2026-10-15:107/107 Debug и Release,52source +80package +3README; bounded configuration/bootstrap/diagnostics/lifecycle proof | Новых Core API для UI quality не требуется; cooperative shutdown и platform limits сохраняются |
| [Cobalt / DI](cobalt/STATUS.md) |2026-10-15 COB7:runtime179/generator91 Release, paired App comparison и independent consumers; два projects/один package | Default Cobalt/adapter сохранены; массовая UseDI migration отложена. UI получает свой proof, allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) |2026-10-15 ENE5:runtime126/generator114 Debug/Release,10cases/90samples. Позднее MIT5 повторил runtime126 Debug/Release/package108 | Same-build pipes scope; MMF/schema/backpressure/engine frames не подтверждены |
| [Mithril](mithril/STATUS.md) |2026-10-15 MIT5:94/94 Debug и Release,40restart +18shutdown,108fresh package cases; diagnostics проверены в96cases с живым parent | Bounded own-child/cooperative scope; game session supervisor и external restart не объявлены готовыми |
| [Alloy / Argentis](alloy/STATUS.md) |2026-10-11:UI532/532,golden360/360,validation0/0 Debug/published Release; package16processes в отдельном run | Свежий package/Hosting +Cobalt/fonts proof — следующий шаг. K3/K7/K8/VRAM/final matrix открыты |
| [Engine / Editor](engine/STATUS.md) | ENG0–ENG8 запланированы; своих проектов/checks нет | Game scenario/platform/contracts не выбраны; реализация этим шагом не начинается |

Подробные counts/configuration/commands и ошибки остаются у владельцев. Эта таблица
сводит разные предыдущие runs и не является одним full-solution regression pass.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI wiring проверяется отдельно |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Headless и units подтверждены отдельно; fresh UI consumer после Core/Cobalt/fonts — следующий scope |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний записанный общий build — **2026-10-15, MIT5**, nonincremental Debug:
0errors/1193warnings. В том же этапе Mithril94/Enerit126 в каждой Debug/Release:
4TRX/440passed/0failed/0skipped;40restart +18shutdown +108package cases прошли.
Own Core/Cobalt/generator suites, UI/native и performance в MIT5 не повторялись.
[Команды и scope](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).

Другие результаты — в [quality matrix](QUALITY.md#матрица) и локальных HISTORY.
Прежний длинный общий журнал [сохранён целиком](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Документационная сверка не создаёт нового passing build/tests/native результата.

## Общие блокеры и неопределённости

- UI package proof2026-10-11 не покрывает current Cobalt/fonts вместе. Existing
  consumer использует UseDI и backend overloads без explicit font catalog; это
  acceptance gap, новый runtime failure в этой сверке не воспроизводился.
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

1. Выполнить [UI package/Hosting quality](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15):
   existing consumer на Cobalt/DI, bundled analyzer и explicit fonts/native delivery,
   bounded lifecycle/cleanup, fresh Debug/Release build/publish evidence.
2. По результату обновить сводку и выбрать доступный UI acceptance либо явно
   ограниченный ENG0 scope. K7/physical checks не объявлять пройденными; driver/
   Windows settings не менять без соответствующего запроса. PA1–PA10 не начинать
   автоматически вместо согласованного quality прохода.
3. Cobalt allocation и Enerit scalar CPU/allocation investigation — отдельный
   measured backlog перед оптимизацией; default/adapter/migration решение прежнее.

## Изменения формата — 2026-10-09

Исходная история разделения планов и проверок сохранена в
[архиве общего статуса](HISTORY.md#сводка-до-quality-matrix--2026-10-15).
Обновление2026-10-15 сократило STATUS до текущей сводки и синхронизировало очередность;
критерии модулей, G1–G6/R1–R6 и UI K1–K9/PA1–PA10 не отменены.
