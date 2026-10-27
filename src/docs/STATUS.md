# TrueMoon: общий статус

Последняя работа — **2026-10-28, сверка рабочего diff и актуализация планов**.
ARCH2a–c уже входят в HEAD `af03ca3`. Текущие изменения продолжают ARCH2d:
тонкие Examples hosts/resources и перенос runtime; отдельно исправляют ENG2 CPU
frame race, ENG3 build/Replace fixtures и staging paths, Mithril ready-file reader
и ошибочное ожидание diagnostic forwarding Accepted.

ARCH2d реализован и проверен в source/published resource/headless scope.
Следующий доступный шаг — [ARCH2e.1/2 independent NuGet](engine/PLAN.md#arch2e-независимый-nuget-consumer--декомпозиция-2026-10-28).
GUI/native acceptance ARCH2d/e остаётся открытой; GPU runs отложены после BSOD,
причина и связь с проектом не установлены. Полный ARCH2 предшествует новым
ED6a/ED8a → ED4c.2/3 → project picker/switch и ED1/ED6 workflow.

В этой сверке прочитаны исходники, сохранённые TRX и verification artifacts;
новых build/tests/process/GPU/package runs нет. Последние module results:
Aether1543/1543 и Editor763/763 от28октября, Mithril111/111 от27октября,
каждый отдельно Debug/Release, failed/skipped0. Общий full test run после fixes
не повторён. Детальная связь diff с задачами — [engine STATUS](engine/STATUS.md),
накопленные сводки и прежние failures — [HISTORY](HISTORY.md#сверка-рабочего-дерева-и-архив-status--2026-10-28).

Документационный QA:20документов,707локальных ссылок/425якорей без ошибок;
53source fingerprints совпали,6сохранённых TRX сверены.
[Отчёт сверки](../TestResults/PlanningReview-20261028/verification.json).

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE6 bounded pipeline/capture/emergency/operations + history/import views; current373/373 Debug/Release | Domain/aggregate telemetry, bootstrap/App-private/generation/performance; прежние cooperative/platform limits |
| [Cobalt / DI](cobalt/STATUS.md) | Текущий179/generator91 Debug/Release; document/project ownership на Cobalt/MS DI | API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) | ENE6d.1–3 metrics/traces и отдельный Aether child lane; current343/generator114 Debug/Release; прежний package proof отдельно | Queue distribution; ENE6c/MMF/trends и расширенная platform/package matrix отдельно |
| [Mithril](mithril/STATUS.md) | Ready-file reader исправлен с RED2→GREEN; полный111/111 Debug/Release, published composition14/restart20. Runtime/API и identity/reap assertions прежние | Ready-file follow-up закрыт; полный MIT6 fault matrix/containment/trends остаётся отдельным |
| [Alloy / Argentis](alloy/STATUS.md) | Current managed full UI893/Hosting114 Debug/Release; предыдущие native proofs сохраняют свои даты | Rename/reorder consumer; physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ARCH2d: resource/headless delivery proof,18hash checks/16CLI/runtime4/4 вне workspace; Editor763/763 Debug/Release. Предыдущие Aether1543/1543 и headless matrices сохраняют свой scope | ARCH2e.1/2 independent NuGet consumer → native proof → ED6a/ED8a → ED4c.2/3/workflow. GUI/GPU/native acceptance открыта, GPU отложен до разбора BSOD |

Подробные contracts/commands/failures ведутся у владельцев. Historical packages,
performance и аппаратные проверки не становятся частью текущего full solution.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные consumers | Runtime/preview/project/documents/Transform/menu UI имеют датированные proofs, ARCH2a–c реализованы; ARCH2d resource/headless проверен; independent NuGet, GUI/native acceptance и общий project/runtime switch открыты |
| G6. Общая поставка | R1–R6 не закрыты; версия0.2.2.7-alpha сама по себе readiness не подтверждает |

## Общие проверки — датированные сведения

Последний найденный полный run — **2026-10-27, ARCH2a**,10test projects:
Debug4296/4296, Release4295/4296, failed1/skipped0. Release failure — Mithril
composition ready-file sharing IOException. [Итоги и исходный failure](../TestResults/AetherPackages-ARCH2a-20261027-1/verification.json),
[история](engine/HISTORY.md#arch2a-scene-persistence-в-aether--2026-10-27).

Позднее ready-file fix прошёл отдельный Mithril111/111 Debug/Release и published
composition14/restart20; Aether1543/1543 и Editor763/763 Debug/Release проверены
в своих runs от28октября. Эти результаты не складываются в новый solution run.
Последний сохранённый solution nonincremental Debug build от28октября:
0errors/1541warnings, [log](../TestResults/ARCH2d-20261028-1/solution-Debug.log).

ARCH2d resource/headless verification:18hash checks,16CLI checks и independent
runtime DLL/apphost4/4 source Debug/published Release вне workspace. [Точный scope](engine/HISTORY.md#arch2d-hosts-и-доставка-ресурсов--2026-10-28).
GUI/native loading, независимый NuGet consumer и общая release matrix этим не
подтверждены. В текущей документационной сверке проверки не перезапускались.
Более ранние full/package/native результаты — в [HISTORY](HISTORY.md).

## Общие блокеры и неопределённости

- Normal Editor .aetherproj выбирается через CLI; document UI и первый Transform/
  Undo/Redo/Edit preview цикл реализованы. New пока пустой, create/delete authoring
  operations, project picker/switch и Build/Play/ED6 workflow ещё нужны.
  ED4d уже редактирует scalar/null/multiline свойства и состав компонентов через
  общие schemas/history; templates/wizard/recent/preferences остаются в плане.
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
- ENG2 CPU receiver, legacy Build/Replace и MSB3030 staging paths прошли свои
  full/headless проверки, включая исходный TEMP. Forwarding test follow-up закрыт
  codec/late-ACK regressions2026-10-28; точная причина задержки прежнего Debug run
  неизвестна. Произвольно длинные paths и общий release scope автоматически не
  становятся поддержанными.
- Общий supported configuration/SDK/OS scope, warnings policy и R1–R6 остаются
  отдельными решениями; публикации не было.

## Следующий конкретный шаг

1. [ARCH2e.1/2](engine/PLAN.md#arch2e-независимый-nuget-consumer--декомпозиция-2026-10-28): ARCH2a–c
   shared scene persistence, Editor library/App integration и explicit runtime
   launch/bootstrap/identity реализованы; ARCH2d hosts/resources проверены source/
   published headless. Далее fresh independent NuGet consumer; GUI/native acceptance
   остаётся открытой, GPU runs на паузе после BSOD. Editor и runtime подключаются из
   пользовательского решения, без source-checkout и executable dependencies пакетов.
2. После ARCH2 в diagnostics направлении — ED6a/ED8a bounded console/export поверх
   [готовой child delivery lane](engine/DIAGNOSTICS_DELIVERY.md), затем command/frame
   aggregates. App-private routing и общий artifact quota требуют отдельных контрактов.
3. Продолжить [ED4c.2 имена и порядок соседей](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24)
   через общие engine metadata/history, затем UI rename/reorder. Следом ED4c.3
   create/delete/duplicate, чтобы New был пригоден для работы. Затем project picker/switch и
   ED1/ED6 document/build/play workflow. При возврате к gameplay — ENG5c.3 collisions камеры, затем HUD.
4. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
5. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).
