# TrueMoon: общий статус

Последняя работа — **2026-10-27, ENG8a.3/ENE6d.4 bounded child diagnostic delivery**.
Core предоставляет bounded history/import views; Aether App factories связывают
child с host через отдельный Enerit endpoint. Import сохраняет child origin/PID/
time/trace, назначает host sequence и authoritative session/build context.
Есть byte/count/rate/deadline limits, loss/unconfirmed/cleanup health; control
admission и local JSONL/raw capture независимы.
[Контракт/API](engine/DIAGNOSTICS_DELIVERY.md),
[результаты и история ошибок](engine/HISTORY.md#child-diagnostic-delivery--2026-10-27).

Новые179cases прошли в обеих конфигурациях. Full solution4285/4285 Debug и4285/4285
Release, failed/skipped0: Core373, Aether1338, Editor741, Enerit343/generator114.
Builds0errors: Debug nonincremental1546warnings, Release incremental1316warnings,
SDK10.0.403. [Evidence](../TestResults/DiagnosticsDelivery-20261027-1/verification.json).
Source/binary hashes проверены, source changes0; пользовательский Microsoft DI10.0.14
сохранён.3actual Preview children/99control commands, log flood, slow/broken sinks,
bounded JSONL/raw и reap/release проверены. GPU/display/published/package/performance
acceptance не повторены. Неуспешные попытки сохранены; две проблемы изоляции/
cleanup deadline новых тестов исправлены в тестах.
Прежние CPU FrameReceiver/Mithril ready-file races не объявлены исправленными.

Следующий diagnostics шаг — ED6a/ED8a bounded Editor console/export, затем
command/frame aggregates. Bootstrap/App-private routing, generator и общий artifact
quota частичны. Authoring очередь и quality gates сохраняются.
Предыдущие [IPC outcome/wait](enerit/HISTORY.md#ene6d3-исходы-ipc-и-ожидание-admission--2026-10-27),
[resource metrics](enerit/HISTORY.md#ene6d2-метрики-ресурсов-и-перегрузки--2026-10-26),
[operations](core/HISTORY.md#операции-и-трассы-диагностики--2026-10-26)
и [build retention](engine/HISTORY.md#диагностика-сборок-ограничение-хранения--2026-10-26)
сохраняют свои scope и budgets.

Предыдущая работа — **2026-10-25, ED4f.2 системный акцент границы окна**.
Windows cached accent/fallback подключён через общий host contract; Editor включает
рамку с inactive dimming160мс и приоритетом явного style/local цвета.
Related UI893/Hosting114/Editor741 Debug/Release:3496passed/0failed/0skipped,
15новыхcases; nonincremental Debug solution build0errors/1532warnings.
12native cases/206PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4f2-системная-граница-окна--2026-10-25).
Evidence: TestResults/AccentBorder-20261024/verification.json. Aether/full solution
tests/прочие модули/packages/physical/performance не повторены; Windows settings
не менялись. Порядок прежний: ED4c.2 → ED4c.3 → project/runtime workflow.

Предыдущая работа — **2026-10-24, ED4e островки над viewport**.
Editor использует матовые панели поверх полной рабочей области сцены, group
opacity/PanelBackdrop в общей UI-библиотеке, retained context/fade без resize.
Related UI842/Hosting98/Editor699 Debug/Release:3278passed/0failed/0skipped,
67новыхcases; nonincremental Debug solution build0errors/1532warnings.
12native cases/158PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4e-островки-поверх-viewport--2026-10-24).
Evidence: TestResults/Islands-20261024/verification.json. Aether/full solution tests,
прочие модули/packages/physical/performance не повторены. Порядок engine работ и
межмодульные блокеры прежние: ED4c.2 names/order, ED4c.3 create/delete, workflow.

Предыдущая работа — **2026-10-24, ED4d.1 компактный контекстный Inspector**.
Иконки/Tooltip и скрытие колонки без выбора подключены к существующим UI controls;
engine authoring API не менялся. Related UI801/Hosting98/Editor673 Debug/Release:
3144passed/0failed/0skipped,25новыхcases; nonincremental Debug solution build0errors.
12native cases/152PNG/16reaped children,validation0/0 и zero owned UI/GPU/mappings.
[Результаты](engine/HISTORY.md#ed4d1-компактные-действия-и-контекст-инспектора--2026-10-24).
Evidence: TestResults/InspectorChrome-20261024/verification.json. Aether/full solution
tests/прочие модули не повторены; прежние quality/hardware gates открыты.
Порядок прежний: ED4c.2 names/order, ED4c.3 create/delete/duplicate, project/workflow.

Предыдущая работа — **2026-10-24, ED4d динамический Inspector и component authoring**.
Схемы компонентов определяют фактические редакторы свойств; Add/Remove/Restore,
reset/provenance, history/save/preview используют общий engine API. Runtime World
не является prerequisite; live runtime edit остаётся отдельным этапом.
Текущие Aether1142/UI801/Hosting98/Editor648 Debug/Release =2689/configuration,
5378всего,failed/skipped0;163новыхcases. Nonincremental Debug solution build:
0errors/1532warnings.12native cases/146PNG/16reaped children,validation0/0,
zero owned UI/GPU/mappings. Full solution tests/прочие модули/packages/physical/
performance не повторены; прежние CPU receiver/Mithril issues открыты.
[План](engine/EDITOR_PLAN.md#ed4d-динамический-инспектор-компонентов--начат-2026-10-24),
[проверки](engine/HISTORY.md#ed4d-динамический-инспектор-компонентов--2026-10-24).
Evidence: TestResults/Inspector-20261024/verification.json. Далее ED4c.2 имена/
порядок соседей, ED4c.3 create/delete/duplicate, затем project/runtime workflow.
Новых базовых prerequisites нет.

Предыдущая работа — **2026-10-24, ED4c.1 обозреватель и PA4c TreeView**.
Первый срез дерева/иконок/выбора/переноса и общий Reparent API/history реализованы.
Текущие Aether1050/UI801/Hosting98/Editor577 Debug/Release =2526/configuration,
failed/skipped0;130новых cases. Nonincremental Debug solution build0errors/1532warnings.
10native source Debug/published Release cases,118PNG/14reaped children,
validation0/0 и zero owned UI/GPU/mappings. Full solution tests/прочие модули/
packages/physical/performance не повторены; прежние CPU receiver/Mithril issues открыты.
[План](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24),
[проверки](engine/HISTORY.md#ed4c1-дерево-сущностей-и-reparent--2026-10-24).
Evidence: TestResults/Hierarchy-20261024/verification.json. По запросу пользователя
приоритет — ED4c.2 сохраняемые имена/порядок, затем ED4c.3 create/delete/duplicate;
дальше project/runtime workflow. Базовые prerequisites не добавлены.

Предыдущая работа — **2026-10-24, ED4b строка состояния и PA4b StatusBar**.
Компактные status/icons/cancel, details и Edit Preview в footer; общая responsive
UI capability и typed View/Data/Style/App/Cobalt factories. Текущие связанные
UI759/Hosting98/Editor527 Debug/Release =1384/configuration,failed/skipped0.
Nonincremental Debug solution build0errors/1532warnings.6native source/published
empty/legacy/Transform cases,68PNG/12reaped children,validation0/0 и zero owned
UI/GPU/mappings. Full solution tests/Aether/headless не повторялись; прежние
CPU receiver/Mithril fixture failures остаются открытыми.
[План](engine/EDITOR_PLAN.md#ed4b-компактная-строка-состояния--2026-10-24),
[проверки](alloy/HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24).
Evidence: TestResults/StatusBar-20261024/verification.json. Далее minimal create/delete
общего API/history, затем UI и project/runtime workflow; порядок прежний.

Предыдущая работа — **2026-10-24, общий матовый backdrop и меню Editor**.
Общий UI material подключён к dropdown меню; theme tint/blur/fallback без новых
Editor services. Текущие UI739/Hosting98/Editor467 Debug/Release =1304/configuration,
failed/skipped0;4native source/published cases,28PNG/4reaped children,validation0/0
и zero owned UI/GPU/mappings. Full solution/Aether/headless в этом срезе не
повторялись. [Контракт](alloy/PLAN.md#pa8b-матовые-поверхности--2026-10-24),
[проверки/границы](alloy/HISTORY.md#pa8b-матовые-поверхности--2026-10-24).
Evidence: TestResults/Backdrop-20261024/verification.json. Порядок работ прежний:
minimal create/delete общего API/history, затем UI и project/runtime workflow.

Предыдущая работа — **2026-10-24, ED4a знак TrueMoon и анимированная кнопка меню**.
Знак и кнопка фиксированы слева; точки ⋮→⋯ поворачиваются вместе с раскрытием,
одно нажатие после анимации открывает «Файл», повторное закрывает. Отмена во время
раскрытия исключает late popup. Header сохраняет View/Style/App/Cobalt/IFactory.
Текущие UI723/Hosting98/Editor467 Debug/Release =1288/configuration,failed/skipped0;
4native source Debug/published Release cases вне repo,28PNG/4reaped children,
validation0/0 и zero UI/mapping/GPU ownership. Full solution/Aether/headless в этом
уточнении не повторялись; прежние CPU receiver/Mithril fixture failures открыты.
[План](engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки](engine/HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).
Evidence: TestResults/HeaderBrand-20261024/verification.json.

Предыдущая работа — **2026-10-23, ED4a компактный заголовок и меню Editor**.
Дизайн/план зафиксирован, Menu/Command/Overlay/frame transitions реализованы в UI;
Header View/Style через App/Cobalt/typed factory. Большие command rows скрыты,
⋯ раскрывает пять меню, title сдвигается, ▶/пауза/стоп и оконные actions неподвижны.
File/history/build/runtime/theme actions, shortcuts и unsaved exit policy сохранены.
[План](engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23),
[проверки и границы](engine/HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

105новыхcases; related UI712/Hosting98/Editor465 Debug/Release =1275/configuration,
failed/skipped0. Source Debug/published Release вне repo:10native/header cases,
64PNG/12reaped children, validation0/0 и UI sessions/mappings/GPU resources/handles0.
10headless scene/SDK cases отдельно. Evidence:
TestResults/MenuHeader-20261023/verification.json; source/binary fingerprints проверены.

Последний full Debug **3048/3049**: MithrilRestartProbe ready-file sharing IOException;
последний Release **3049/3049**,10TRX/configuration, skipped0. Nonincremental build
0errors/1532warnings. Ранний CPU FrameReceiver null и fixture race записаны в
engine/Mithril планы; исправление этих прежних путей данным UI срезом не заявлено.
Предыдущий full3049/3049 Debug/Release не отменяет последний Debug failure.
Fresh packages/physical/performance/golden/full alpha не повторены.

Текущий маршрут: ED4c.2 сохраняемые имена/порядок, затем ED4c.3 create/delete
общего authoring API/history и их UI commands; после — project picker/switch
и ED1/ED6 workflow. Общие engine/UI границы прежние; full
World/ECS и новый Core/Cobalt quality цикл не prerequisites. IPC — Enerit,
frames — отдельный transport, процессы — Mithril.

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE6 bounded pipeline/capture/emergency/operations + history/import views; current373/373 Debug/Release | Domain/aggregate telemetry, bootstrap/App-private/generation/performance; прежние cooperative/platform limits |
| [Cobalt / DI](cobalt/STATUS.md) | Текущий179/generator91 Debug/Release; document/project ownership на Cobalt/MS DI | API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) | ENE6d.1–3 metrics/traces и отдельный Aether child lane; current343/generator114 Debug/Release; прежний package proof отдельно | Queue distribution; ENE6c/MMF/trends и расширенная platform/package matrix отдельно |
| [Mithril](mithril/STATUS.md) | Current99/99 Debug/Release; исторический ready-file sharing race26октября не исправлялся в этом срезе | Ready JSON publication/read stability; full fault matrix/containment отдельно |
| [Alloy / Argentis](alloy/STATUS.md) | Current managed full UI893/Hosting114 Debug/Release; предыдущие native proofs сохраняют свои даты | Rename/reorder consumer; physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ENG8a telemetry/propagation и bounded child logs; current Aether1338/1338 и Editor741/741 Debug/Release; прежний CPU receiver failure не исправлен | ED6a/ED8a console/export, command/frame metrics и ED4c.2/3/workflow; ENG2 race открыт |

Подробные contracts/commands/failures ведутся у владельцев. Historical packages,
performance и аппаратные проверки не становятся частью текущего full solution.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Runtime/preview/project/documents/Transform/menu UI проверены; общий project/runtime switch открыт |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Предыдущий full — **2026-10-23, ED2b**:2878/2878 в каждой конфигурации,10TRX,
failed/skipped0: Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/
UI661/Hosting85/Aether1008/Editor358. Build0errors/1531warnings.
74новых focused cases; native/headless outside-repo scope:
[подробная matrix](engine/HISTORY.md#ed2b-документы-в-обычном-ui--2026-10-23).

Предыдущие ED2a2804/ED1a2661/configuration и independent preview2569 с fresh
managed packages8/8 сохраняют дату и [scope](engine/HISTORY.md).
Authoring2448, ENE6a/MIT6a2361 и UIA1 2321 находятся в [истории](HISTORY.md).
Editor non-packable; новых package runs в ED2b не было. Independent preview
drawing, golden/RTE/full performance/physical UI и real loss/VRAM не повторены.

## Общие блокеры и неопределённости

- Normal Editor .aetherproj выбирается через CLI; document UI и первый Transform/
  Undo/Redo/Edit preview цикл реализованы. New пока пустой, create/delete authoring
  operations, project picker/switch и Build/Play/ED6 workflow ещё нужны.
  Остальные component properties readonly; templates/wizard/recent/preferences в плане.
- Scene store optimistic/trusted local: cooperating writers согласованы sidecar;
  arbitrary external rename между hash check/replace, links и power-loss durability
  не гарантируются. Terminal Dispose forced discard требует user close policy заранее.
- Engine preview GPU-only Windows x64 с fixed camera и child replacement на revision.
  Активную game session перед preview нужно явно остановить. Runtime Play state
  не подменяет authoring document/сохранённый checkpoint.
- DI1–DI4 закрыты в проверенном consumer scope; внешние games требуют entry factory/
  Contracts reference и пересборки artifacts. Массовая migration не назначена.
- K7: previous full240-window failed (GDI23→137/USER26→26), trace связан с
  NVIDIA ICD32.0.16.1742. Workaround search остановлен по решению пользователя;
  возврат при supported fix либо согласованной проверенной alternate configuration.
- Physical DPI100/200%/разные мониторы, real device/context loss, VRAM и final UI
  matrix не подтверждены. Retained UI PNG не заменяет physical-display acceptance.
- Cooperative shutdown не прерывает игнорирующий token user Task или arbitrary
  driver/dispatcher hang. Same-build proof не обещает другие OS/RID/AOT и долгие
  resource trends. Failed cleanup требует сохранения ownership и явного retry.
- Общий supported configuration/SDK/OS scope, warnings policy и R1–R6 остаются
  отдельными решениями; публикации не было.

## Следующий конкретный шаг

1. В активном diagnostics направлении — ED6a/ED8a bounded console/export поверх
   [готовой child delivery lane](engine/DIAGNOSTICS_DELIVERY.md), затем command/frame
   aggregates. App-private routing и общий artifact quota требуют отдельных контрактов.
2. Продолжить [ED4c.2 имена и порядок соседей](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24)
   через общие engine metadata/history, затем UI rename/reorder. Следом ED4c.3
   create/delete/duplicate, чтобы New был пригоден для работы. Затем project picker/switch и
   ED1/ED6 document/build/play workflow. При возврате к gameplay — ENG5c.3 collisions камеры, затем HUD.
3. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
4. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).
