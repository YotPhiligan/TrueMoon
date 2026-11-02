# TrueMoon: общий статус

Последняя работа — **2026-11-03, ED6b.2e.2–3 Observer/source UI**.
Project-owned inert/activated observer, bounded census/hints/polling/drain и retained
status indicator реализованы. Source state отделён от scene/runtime; preflight и
existing ownership/admission fences сохранены. Full Editor1062/1062 Debug/Release
прошёл (84новых cases); source Release2/2 +fresh package Release2/2 прошли,
15packages/headless8/8,4game children reaped/released и Vulkan validation0/0.
[Срез/команды/evidence](engine/HISTORY.md#ed6b2e23-observer-и-source-ui--2026-11-03).
Далее measured observer cost на большом local graph; wider profiles и остальные
module/hardware/release gates прежние.

Предыдущая работа — **2026-11-02, ED6b.2e.1 descriptor и probes**.
Capture-derived immutable source descriptor, exact present/absent probes и общий
compiler exclusion predicate реализованы. Full input identity, schemas и обязательный
preflight сохранены. Final Aether26/26 (22новых cases +4integration) и full
Editor978/978 прошли Debug/Release; fresh15packages/headless8/8 без source drift.
[Реализация/проверки](engine/HISTORY.md#ed6b2e1-descriptor-и-probes--2026-11-02).
Далее ED6b.2e.2 project-owned observer, затем UI indicator; hardware/release gates
прочих направлений прежние.

Предыдущая работа — **2026-11-02, ED6b.2e contract source watcher/stale Build**.
Определены наблюдаемый source descriptor, state/identity rules, project-owned lifetime,
bounded hints/polling/debounce и UI snapshot delivery без auto Build/Play. Compiler
preflight остаётся обязательным. [Contract/этапы](engine/EDITOR_PLAN.md#ed6b2e-контракт-source-watcher-и-stale-build-status--2026-11-02).
В том срезе только source review и документация, C# API/observer/indicator не добавлены;
build/test/native не запускались. Contract определил ED6b.2e.1 descriptor/probes/shared policy,
затем observer и UI acceptance. Hardware/release gates и прочие направления прежние.

Предыдущая работа — **2026-11-02, ED6b.2b.2c.1 native package interrupts/preflight**.
Source/fresh NuGet Release ×standard/Cobalt и embedded/MS DI52/52native cases:
36interrupts +16stale/corrupt preflights перед Start/Replace.76Game children
reaped/released,76graceful validation0/0, maxLive1, final UI/GPU/handles0;
4recoveries только Restore+Cancel. Fresh15packages/headless8/8,2743hashes без drift.
Production runtime/API, unit/full solution и ordinary Workflow matrix не менялись/
не повторялись. [Contract/commands/evidence](engine/HISTORY.md#ed6b2b2c1-native-package-interrupts-и-preflight--2026-11-02).
Далее bounded source watcher/stale Build status без auto Build/Play; wider packages,
HUD/input/Player и physical/hardware/performance/release gates остаются отдельными.

Предыдущая работа — **2026-11-02, ED6b.2b.2c captured managed packages**.
Реализован явный local PackageDirectory + locked unsigned managed closure до SDK,
без package build/analyzer/native assets, с прежними reuse/prestart/ownership fences.
Project manifest v3 добавляет contained packageDirectory; v1/v2 совместимы.
Final package33/full Editor978 прошли Debug/Release, failed/skipped0; SDK lock входит
в package cases. Scoped293прошли до последнего nuspec guard, покрытого final package33.
Fresh15packages/headless8/8 и source/NuGet Release ×standard/Cobalt +embedded/MS DI
4/4native Workflow прошли:56children reaped/released,52graceful validation0/0,
20Game с package DLL только из selected artifact, UI/GPU/handles0,2059hashes без drift.
Далее directed native package interrupts/stale-archive cases; full solution и
hardware/performance/release gates не повторялись.
[Контракт/команды/результаты](engine/HISTORY.md#ed6b2b2c-captured-managed-packages--2026-11-02).

Предыдущая работа — **2026-11-02, ED6b.2b.2b.1 native graph interrupts**.
Source/fresh NuGet standard/Cobalt +embedded/MS DI36/36native cases: Cancel/Stop/Exit
в dependency SDK, graph verify и restore.52Game children reaped/released,52graceful
validation0/0, UI/GPU/handles0;4recoveries только Restore+Cancel, late child после
Stop/Exit нет. Fresh15packages/headless8/8,2735hash checks без drift.
[Contract/matrix/evidence](engine/HISTORY.md#ed6b2b2b1-native-graph-interrupts--2026-11-02).
Production C#/unit/full solution и ordinary Workflow matrix не менялись/не повторялись.
Далее — locked managed PackageReference closure без build/analyzer/native assets;
hardware/performance/release gates сохраняются.

Предыдущий graph profile и262/971 Debug/Release —
[в истории](engine/HISTORY.md#ed6b2b2b-captured-managed-project-graph--2026-11-02).

Предыдущий ED6b.2d.1 Stop/exit order и single-project native interrupt proof —
[в истории](engine/HISTORY.md#ed6b2d1-native-replace-interrupts--2026-11-02).

Предыдущая работа — **2026-11-01, ED6b.2d document-aware code Replace**.
Fresh15packages/headless8/8 и published source/NuGet standard/Cobalt +embedded/MS DI
4/4workflows:56children reaped/released,52graceful validation0/0, UI/GPU/handles0,
8new PNG просмотрены. Scoped Aether82/82 и full Editor957/957 Debug/Release; full
Aether/Alloy/Hosting/solution не повторены. Code swap сохраняет paused tick; compiler failure оставляет old child,
restore rejection возвращает old code/state/Playing mode. Authoring scene неизменна.
Следующий шаг — directed native Cancel/Stop/exit в середине Replace;
native Release workflow не закрывает physical/hardware/general release gates.
[Контракт, проверки и границы](engine/HISTORY.md#ed6b2d-document-aware-code-replace--2026-11-01).

Предыдущая реализация — **2026-10-31, ED6b.2b.2a declarative preparation closure**.
Scene Start фиксирует поддержанный project/import/source/reference graph до первого
SDK process. Windows SDK/runtime files read-leased; environment/home/temp/NuGet config
явные. Custom tasks/functions/SDK и package/project graphs отклоняются до execution.
Original paths и repeatable compiler bytes сохранены; legacy flags default false.
Aether1704/1704 и Editor943/943 отдельно Debug/Release, failed/skipped0. Другие module
suites и full solution не повторены; прежние failures сохранены в HISTORY.
Fresh15packages/headless8/8, exit0; native/window/GPU runs не повторены.
[Контракт и проверки](engine/HISTORY.md#ed6b2b2a-declarative-preparation-closure--2026-10-31).
Дальше — consumer acceptance и стоимость freeze; native/hardware/release gates,
расширенные profiles и artifact reuse/Replace этим не закрываются.

Предыдущая реализация — **2026-10-31, ED6b.2b.1 захваченная C#-компиляция**.
Scene Start собирает из owned copies source/generated/reference/config/compiler bytes
после trusted SDK preparation; original compiler/reference bytes также проверяются до Start.
Пути исходников/diagnostics сохраняются, staging удаляется до возврата Build result.
C# library profile fail-closed для opaque inputs/custom extensions; schema1 прежняя.
Aether1683/Editor943 отдельно Debug/Release, failed/skipped0;26новых cases.
Fresh15packages/headless8 и published source/NuGet standard/embedded4/4:44children
reaped/released,40graceful validation0/0, UI/GPU/handles0;4PNG просмотрены.
Каждая native compilation захватывает188files/42105392bytes; fingerprints повторных
сборок совпадают. [Scope/commands/findings](engine/HISTORY.md#ed6b2b1-захваченная-c-компиляция--2026-10-31).
Далее ED6b.2b.2 closure до evaluation/preparation. Arbitrary task/generator IO,
installed runtime/environment, cache/reuse/Replace/full ED6b/HUD/input/Player и
historical popup/output-drain/hardware/release follow-ups открыты. Другие suites не повторены.

Предыдущее планирование — **2026-10-31, качество UI-рендеринга и текста PA8c**.
[UI план](alloy/PLAN.md#pa8c-качество-рендеринга-и-текста--2026-10-31) содержит baseline,
text/DPI/font/image/composition улучшения, performance и visual acceptance.
Это документация, новых code/build/test/native/package результатов нет.
Приоритет ED6b сохранён; первый шаг новой ветки — PA8c.0.

Предыдущая реализация — **2026-10-31, UIA1e Bindable → ViewModel**.
Argentis переиспользует INotifyPropertyChanged в общей базе; Editor models/settings
sample подключены к иерархии. UI919/919 и Editor923/923 Debug/Release, failed/skipped0.
[Контракт/evidence](alloy/HISTORY.md#uia1e-bindable-и-viewmodel--2026-10-31).
Другие module/full solution suites, package/native/physical проверки в этом шаге
не повторялись. Общая очередь ED6b и прежние hardware/output-drain ограничения сохранены.

Предыдущая работа — **2026-10-31, ED1b.2 project picker и runtime rebinding**.
Retained project path UI, shared Save/Discard/Cancel и workspace/build identity
согласованы до возврата admission; Close очищает context. Profile требует stopped
game/idle compiler и drains active preview. Aether1631/Editor923 отдельно Debug/Release,
failed/skipped0. [Scope/evidence](engine/HISTORY.md#ed1b2-project-picker-и-runtime-rebinding--2026-10-31).
Final15fresh packages/headless8 и published source/NuGet standard/Cobalt +embedded/MS DI
4/4workflows прошли;32children reaped/released,28graceful validation0/0,14PNG просмотрены.
Далее ED6b document-aware Build/Play. Другие module suites и physical/release gates
этими Aether/Editor runs не закрываются; старый output-drain follow-up открыт.

Предыдущая работа — **2026-10-31, ED1b.1 project/document transitions**.
Project API получил Switch с explicit Save/Discard/Cancel и guarded Close; failed
preparation/cancel сохраняют текущую session/history, cleanup owners удерживаются для retry.
Editor903/903 отдельно Debug/Release,20новых cases, failed/skipped0.
[Scope/evidence](engine/HISTORY.md#ed1b1-projectdocument-transition-api--2026-10-31).
Следовавшие project picker и runtime/build rebinding реализованы в ED1b.2 выше;
ED1/ED6b остаются частичными. Другие suites и прежний package/native scope
этим API-запуском не повторены.

Предыдущая работа — **2026-10-31, ED4c.3 операции объектов и UI commands**.
Atomic create/delete/duplicate, New presets, поиск и context menu используют общую
engine history. Исправлена retained items/selection projection, включая несколько
commits в одном UI update. Aether1629/Editor883/Alloy909/Hosting117 отдельно
Debug/Release, failed/skipped0; final15packages/headless8/native workflows2/2 прошли.
14native children освобождены,12graceful validation0/0,4PNG просмотрены.
[Scope/evidence](engine/HISTORY.md#ed4c3-операции-объектов-поиск-и-контекстное-меню--2026-10-31).
Далее project picker/switch и ED1/ED6 workflow. Прежний intermittent runtime
output-drain follow-up не закрыт этими passing runs.

Предыдущая работа — **2026-10-31, ED4c.2 имена и порядок сущностей**.
Persistent engine metadata и JSON v1/v2 идут через общую atomic history; Editor
предоставляет inline rename, keyboard и before/after reorder с сохранением stable IDs.
Aether1602/Editor866/Alloy900/Hosting117 отдельно Debug/Release, failed/skipped0.
[Audit/8TRX](../TestResults/HierarchyMetadata-ED4c2-20261031-1/verification.json).
Final15fresh packages/headless8 и published source/NuGet workflows2/2 прошли,
14native children освобождены,12graceful validation0/0,4PNG просмотрены.
[Приёмка](../TestResults/HierarchyMetadata-ED4c2-20261031-1/acceptance.json).
Следующий основной шаг — ED4c.3 create/delete/duplicate, затем project/workflow.
Отдельный runtime follow-up: headless output-drain timeout после reap проявился снова;
два exact binary replays прошли без смены deadlines, причина остаётся неустановленной.

Предыдущая работа — **2026-10-31, ED8a.2 child health/device и acceptance**.
Runtime передаёт numeric health и cached selected device по прежнему diagnostic lane;
UI/report сохраняет last observation/time после crash, явно отмечает unavailable.
Aether1581/Editor847/Alloy893/Hosting117 отдельно Debug/Release, final failed/skipped0.
Fresh15packages/headless8 Game cases и source/NuGet published standard/Cobalt native2/2
прошли:38children reaped/released суммарно,12graceful GPU validation0/0, UI/GPU0.
[Scope и audit](engine/HISTORY.md#ed8a2-child-health-и-device-observations--2026-10-31).
ED8a закрыт в указанном scope; следующий основной шаг — ED4c.2 имена сущностей,
затем ED4c.3/project/workflow. Report остаётся partial; extended native/backend/provider,
physical/device loss/VRAM/long-load и release gates самостоятельны. Full solution и
Core/Enerit/Mithril suites заново не запускались; их старые counts не перенесены сюда.

Предыдущая работа — **2026-10-30, ED8a.1 archive и управляемый diagnostic report**.
Core даёт borrowed bounded JSONL/configuration views; Editor объединяет историю с
memory, добавляет explicit text opt-in, backend/viewport, user cancel и App drain.
Core398/Aether1570/Editor842 отдельно Debug/Release, failed/skipped0. Published
Release/Cobalt consumer вне checkout проверил live/crashed CPU preview children,
rotated/corrupt history, finite flood и native Skia raster; оба children освобождены.
[Evidence/границы](engine/HISTORY.md#ed8a1-архив-и-управляемый-экспорт--2026-10-30).
Далее ED8a.2 child health/device и window/game/NuGet acceptance, затем ED4c.2/3.
Отчёт остаётся partial с explicit unavailable; full solution/UI/Hosting suites,
окно/GPU и прежние hardware/release gates этим запуском не закрыты.

Предыдущая работа — **2026-10-30, ED6a.3 compiler diagnostics/source navigation**.
Bounded SARIF и Compiler mode сохраняют file/line/column/job correlation; host hook
переходит по frozen location и drain/cancel при Stop. SARIF retention исправлена
после initial4failures. Aether1570/Editor833 отдельно Debug/Release, failed/skipped0;
SourceDebug/Cobalt/native Skia raster consumer и3PNG проверены без окна/GPU.
[Точный scope и evidence](engine/HISTORY.md#ed6a3-compiler-diagnostics-и-source-navigation--2026-10-30).
Далее полный ED8a report; window/published/flood и прежние hardware/release gates
остаются отдельными. Общего solution/package proof в этом шаге нет.

Предыдущая фиксация планов — **2026-10-30, агенты и Game AI в Aether**.
[ARCH1d/ENG9](engine/PLAN.md#локальные-агенты-и-mcp-adapter) задают общий agent API,
локальный MCP, доступ к Runtime и создание/отладку поведения мобов; ED4g/ED7a
в Editor плане — UI consumers. LLM-персонаж выделен в optional последующий профиль.
Это документация: новых implementation/build/test/native/package результатов нет.
Текущая очередь compiler navigation/ED8a → ED4c.2/3 → project/workflow сохранена;
наблюдение/control не требует полного ARCH1c, произвольный live edit требует.

Предыдущая реализация и проверка — **2026-10-29, ED6a.2 raw streams и health**.
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
| [Alloy / Argentis](alloy/STATUS.md) | ED6b.2d source/NuGet published native4/4; UI/GPU/handles0,8new PNG просмотрены; Alloy/Hosting suites не повторены | PA8c.0; ED8 popup follow-up. Physical/full INF5/K3/K7/K8/VRAM/final alpha открыты |
| [Aether / Engine](engine/STATUS.md) | ED6b.2d scoped Aether82/82+full Editor957/957 Debug/Release, native4/4, fresh15packages/headless8/8; document-aware code Replace | ED6b.2d.1 native interrupt acceptance; scene migration/graphs/disk cache/full ED6b и прежние follow-ups отдельно |

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
  Undo/Redo/Edit preview и New→create/delete/duplicate→Save/reopen реализованы.
  Project picker/switch и Build/Play/ED6 workflow ещё нужны.
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

1. [ED6b.2d.1](engine/EDITOR_PLAN.md#ed6b2d-document-aware-code-replace--2026-11-01):
   directed native Cancel/Stop/exit в середине document-aware Replace, с проверкой
   recovery/admission/drain. Scene migration, disk cache и extended graphs отдельно.
2. Command/frame aggregates после scoped ED8a. App-private routing и общий artifact quota
   требуют отдельных контрактов; native/hardware stress gates не подменяются console UI.
3. При возврате к gameplay — ENG5c.3 collisions камеры, затем HUD/Player по их планам.
4. UI physical DPI/OS input, loss/K8, VRAM и final alpha требуют собственных runs.
   K7 workaround search остановлен; Windows/driver settings без запроса не менять.
5. Full INF5, RTE backlog, Cobalt allocation и Enerit scalar investigation
   сохраняют отдельные планы.

История реорганизации планов и предыдущих сводок — в [HISTORY](HISTORY.md).
