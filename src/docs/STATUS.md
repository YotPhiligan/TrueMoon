# TrueMoon: общий статус

Последняя работа — **2026-10-23, ED2b документы в обычном UI Editor**.
App/Cobalt/IFactory подключают New/Open/Save/SaveAs/Close, dirty/errors/path drafts
и inline Save/Discard/Cancel. Failed Save vetoes native Exit; background IO,
decisions, forced shutdown/callback failures и cleanup retry отслеживаются.
Owner-thread ViewModel и nested View/Style сохраняют tree/path/focus; readonly
authoring properties не ограничены legacy64 projection. Cobalt и Microsoft DI
проверены. [Контракт](engine/EDITOR_PLAN.md#ed2b-document-commands-и-ui--2026-10-23),
[tests/commands/evidence](engine/HISTORY.md#ed2b-документы-в-обычном-ui--2026-10-23).

Final full solution **2878/2878 Debug и2878/2878 Release**,10TRX/configuration,
failed/skipped0; Aether1008/Editor358,74новыхcase. Nonincremental Debug build:
0errors/1531warnings. Source Debug/published Release:10headless cases,8native cases/
18PNG/4reaped game children. Document UI по19actions/4PNG/5actual saved files:
Save/reopen/conflict/recovery и close decisions. App/project/CWD вне repo без demo DLL.
Project/document/UI/GPU cleanup подтверждён;242source/45Editor source/8binary hashes.
Evidence: TestResults/DocumentsUi-20261023/verification.json.
Fresh packages, physical/performance/golden scopes этим шагом не повторялись.

ARCH1b.1, independent engine Edit preview, ED1a и ED2a/b documents реализованы.
Далее **минимальный Transform inspector/Undo/Redo и independent preview в UI**.
Полный ARCH1b и ED1–ED8 остаются
открытыми; UI document/runtime Switch/Close/Exit ещё требует общей транзакции.
Полный World/ECS и новый Core/Cobalt quality цикл не prerequisites первого среза.
IPC — Enerit, frames — отдельный transport, процессы — Mithril.

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Typed factories/adapters; текущий full117/117 Debug/Release | Provider-neutral contracts, cooperative/platform limits прежние |
| [Cobalt / DI](cobalt/STATUS.md) | Текущий179/generator91 Debug/Release; document/project ownership на Cobalt/MS DI | API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) | Текущий166/generator114 Debug/Release; прежние finite admission/stop published44/package132 | ENE6c/d, MMF, trends отдельно |
| [Mithril](mithril/STATUS.md) | Текущий99/99 Debug/Release; legacy Editor child reap повторён; saturated-stop/kill ранее | Полный fault matrix, parent-death/resource/log bounds отдельно |
| [Alloy / Argentis](alloy/STATUS.md) | Текущий UI661/Hosting85/Editor358 Debug/Release; document View/Data/Style и native integration | General UI APIs прежние; physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ARCH1b.1/preview/ED1a/ED2a/b; текущий Aether1008/Editor358 Debug/Release | Transform inspector/Undo/preview; project/runtime workflow и full World/edit/MCP/performance отдельно |

Подробные contracts/commands/failures ведутся у владельцев. Historical packages,
performance и аппаратные проверки не становятся частью текущего full solution.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные приложения | Runtime/preview/project/document APIs и document UI проверены; Transform editing/preview integration и общий project/runtime switch ещё в плане |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Текущий full — **2026-10-23, ED2b**:2878/2878 в каждой конфигурации,10TRX,
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

- Normal Editor .aetherproj выбирается через CLI; document UI New/Open/Save/Close
  реализован. Inspector read-only, preview/game actions при открытом документе
  заблокированы до следующей integration. Project picker/switch, Transform edits,
  preview/ED6 workflow ещё нужны. Templates/wizard/recent/preferences в плане.
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

1. Подключить [Transform inspector/Undo/Redo и preview](engine/EDITOR_PLAN.md#первый-сквозной-срез):
   draft/commit/cancel, numeric validation, один edit gesture/history transaction,
   dirty checkpoint и exact committed revision в independent preview. При возврате к gameplay — ENG5c.3
   collisions камеры, затем HUD.
2. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
3. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).
