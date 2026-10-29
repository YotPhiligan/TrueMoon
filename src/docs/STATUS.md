# TrueMoon: общий статус

Последняя работа — **2026-10-29, ED6a.2 raw streams и health**.
Editor показывает bounded runtime/build stdout/stderr и parent Core/child delivery/
capture health; raw severity и известные/unknown counters разделены. Metadata
export содержит numeric health без raw/error text. Core387/Aether1556/Editor823/
Hosting117 Debug/Release и один SourceDebug/Cobalt native case прошли. Native finding
потребовал Hosting fix: dirty после Update откладывает presentation до следующего
bounded tick, output guards сохранены. [Evidence/ограничения](engine/HISTORY.md#ed6a2-raw-streams-и-health--2026-10-29).
Далее compiler diagnostics/navigation и полный ED8a report; broader delivery/package/
published/physical acceptance остаётся отдельной.

Предыдущая работа — **2026-10-29, ED6a structured console и первый partial export**.
Editor показывает bounded structured history, filter/group/inspect/copy/clear/follow-tail;
metadata-only report явно отмечает partial/unavailable и исключает message/stack/
arbitrary properties. Editor808/808 Debug/Release; один SourceDebug/Cobalt native
workflow проверяет live child history, crash/recovery, filter/group/reopen/export,
6graceful Vulkan reports0/0 и cleanup0. [Точный scope и evidence](engine/HISTORY.md#ed6a-structured-console-и-первый-partial-export--2026-10-29).
Следующий шаг — ED6a.2 raw streams/borrowed health, затем compiler navigation/full
ED8a report; package/published/provider/flood acceptance новой console открыт.

Предыдущая работа — **2026-10-29, ARCH2e.4 scoped file-drain contract**.
Исправлена подтверждённая зависимость file sink от shared ThreadPool после EOF:
dedicated worker/sync wake-up, прежние queue8/budgets/ownership и deadlines.
Controlled RED→GREEN; Core374/374 и Aether1550/1550 отдельно Debug/Release,
fresh15packages/headless8/8 и native24/24/56children прошли.48graceful Vulkan
validation reports0errors/0warnings, teardown0/owned processes0.
[Аудит/current TRX](../TestResults/ARCH2e4-FileDrain-Native-20261029-1/audit.json),
[Core proof/границы](core/HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).

ARCH2e.4 завершён в проверенном scope; первый ED6a/ED8a consumer описан выше.
Точная причинная цепочка старого intermittent episode не установлена: controlled
proof не подменяет его отсутствующий failure heap. Observation и инструменты
при повторе сохранены, arbitrary FS stalls не обещаются. Общие UI/hardware/K7/
VRAM/BSOD/general-release gates и полный solution run этим не закрываются.

## Состояние модулей

| Модуль | Текущий результат и область вывода | Продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | CORE6 + borrowed health/read-window APIs; Core387/387 Debug/Release, counters/ownership/UTF16 boundaries проверены | Domain/aggregate telemetry, bootstrap/App-private/generation/performance; прежние cooperative/platform limits |
| [Cobalt / DI](cobalt/STATUS.md) | Текущий179/generator91 Debug/Release; document/project ownership на Cobalt/MS DI | API прежние; performance/allocation backlog отдельно |
| [Enerit](enerit/STATUS.md) | ENE6d.1–3 metrics/traces и отдельный Aether child lane; current343/generator114 Debug/Release; прежний package proof отдельно | Queue distribution; ENE6c/MMF/trends и расширенная platform/package matrix отдельно |
| [Mithril](mithril/STATUS.md) | Ready-file reader исправлен с RED2→GREEN; полный111/111 Debug/Release, published composition14/restart20. Runtime/API и identity/reap assertions прежние | Ready-file follow-up закрыт; полный MIT6 fault matrix/containment/trends остаётся отдельным |
| [Alloy / Argentis](alloy/STATUS.md) | INF3 dirty-frame RED3→GREEN3; Hosting117/Editor823 Debug/Release и native ED6a.2 consumer1/1. UI893 сохраняет предыдущий run | Compiler navigation/full report и rename/reorder consumer; physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ED6a.2 raw/health: Aether1556/Editor823 Debug/Release; Core387/Hosting117 отдельно, native SourceDebug/Cobalt1/1. Предыдущие ARCH2 package/native результаты сохраняют scope | Compiler navigation/full ED8a → ED4c.2/3/project/workflow. Hardware/release gates отдельно |

Подробные contracts/commands/failures ведутся у владельцев. Historical packages,
performance и аппаратные проверки не становятся частью текущего full solution.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Карта/локальные результаты сведены; общий supported configuration/release scope ещё требует фиксации |
| G2. Core и DI | Bounded headless source/package proof завершён; UI package wiring проверен2026-10-15 в своём scope |
| G3. IPC и units | Same-build own-child/App/pipes/restart/package и ENE5/MIT5 проверены в локальных scopes |
| G4. UI alpha | Частично; K7 failed, остальные аппаратные/final acceptance ограничения сохранены |
| G5. Сквозные consumers | ARCH2e.3 native/headless profile проверен; ARCH2e.4 file-worker contract проверен; ED1–ED8, общий project/runtime switch и дальнейшие consumers открыты |
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
GUI/native loading и independent NuGet подтверждены отдельными runs29октября
выше; это не новый общий solution/release run.
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

1. Compiler file/line/column diagnostics/navigation hook и полный ED8a report:
   history/raw/health/backend/device/configuration, user cancellation и published/flood proof.
   [Детальный план Editor](engine/EDITOR_PLAN.md#ed6a-консоль-диагностики--2026-10-25).
2. Затем command/frame aggregates. App-private routing и общий artifact quota
   требуют отдельных контрактов; native/hardware stress gates не подменяются console UI.
3. Продолжить [ED4c.2 имена и порядок соседей](engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24)
   через общие engine metadata/history, затем UI rename/reorder. Следом ED4c.3
   create/delete/duplicate, чтобы New был пригоден для работы. Затем project picker/switch и
   ED1/ED6 document/build/play workflow. При возврате к gameplay — ENG5c.3 collisions камеры, затем HUD.
4. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
5. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).
