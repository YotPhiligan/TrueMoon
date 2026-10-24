# TrueMoon: история общей документации

## ED4a: компактный заголовок и меню — 2026-10-23

По запросу пользователя дизайн/план и приоритет ED4a зафиксированы и реализованы.
Общие menu/command/overlay/transition contracts остаются в UI плане; Editor
подключает scoped Header View/Style через App/Cobalt/typed factory. После этого
среза следующий прикладной шаг снова minimal create/delete authoring API/history.

105 новых cases, related UI712/Hosting98/Editor465 Debug/Release. Native source
Debug/published Release:10header/application profiles,64PNG,12reaped children,
validation0/0 и zero UI/mapping/GPU ownership.10headless cases отдельно.
Последний full Debug3048/3049: MithrilRestartProbe ready-file IOException;
последний Release3049/3049, skipped0. Ранний FrameReceiver null и этот fixture
race внесены в engine/Mithril планы; runtime этих модулей не менялся. Предыдущий
full3049/3049 Debug/Release не отменяет последний Debug failure.
[Детали и exact evidence](engine/HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23).

## Сверка планов и ED3a Transform/history/preview — 2026-10-23

Согласован порядок: первый редактируемый Open/Transform/Preview/Undo/Save/reopen
цикл → минимальные create/delete общего authoring API/history → project picker/
switch и ED1/ED6 workflow. Устаревшие «следующие шаги» базовых модулей исправлены;
исторические QUALITY/ENE6 записи отделены от текущего scope. Новый Core/Cobalt
quality цикл не prerequisite; ENE6c/d и оставшийся MIT6a/c остаются отдельными.

ED3a реализовал первый цикл через существующий engine API, nested View/Data/Style
и App/Cobalt/IFactory. Atomic drafts/history, validation/conflicts, bounded preview
updates и child cleanup проверены. [Детали, requirement matrix и exact evidence](engine/HISTORY.md#ed3a-transformhistorypreview-в-ui--2026-10-23).
66новых cases; final full2944/2944 Debug и Release,10TRX/configuration,failed/skipped0;
nonincremental build0errors/1531warnings. Source Debug/published Release: текущие
10native cases/24PNG/12reaped children и10headless scene/SDK cases; validation0/0,
UI sessions/mappings/GPU resources/handles0. Source/binary fingerprints проверены.
Fresh packages и physical/loss/VRAM scope не повторялись. Evidence —
TestResults/TransformUi-20261023/verification.json.

## ED2b: документы в UI Editor — 2026-10-23

App/Cobalt/IFactory подключают New/Open/Save/SaveAs/Close и document-aware native
Exit. Inline Save/Discard/Cancel, owner-thread ViewModel/typed View/Style,
dirty/errors/path drafts, actual IO drain/forced upgrade и callback-failure retry
реализованы. Raw authoring properties читаются без legacy64 projection; inspector
ещё read-only, preview не подключён к normal UI. Project picker/runtime switch
остаются ED1/ED4/ED6. [Контракт и точные tests](engine/HISTORY.md#ed2b-документы-в-обычном-ui--2026-10-23).

74новых cases; full2878/2878 Debug и Release по10TRX/failed0/skipped0,
Editor358/Aether1008, UI661/Hosting85. Nonincremental build0errors/1531warnings.
Source/published app вне repo: documents по19UI actions/4PNG/5saved files,
Save/reopen/conflict/recovery/failed Exit Save/Cancel/Discard. Вся native matrix
8cases/18PNG/4reaped children, validation0/0 и zero UI/GPU owners;10headless cases
отдельно,19consumer commands всего с6ожидаемыми negative exits. Source242/Editor45/
binary8 fingerprints и PNG проверяются; TestResults/DocumentsUi-20261023/verification.json.
Новых engine/wire/UI-library APIs/dependencies нет; fresh package, physical/
performance/golden scopes не повторялись. Далее Transform inspector/Undo/Redo
и independent preview; ED2 single-project document profile завершён, весь ED1–ED8 нет.

## ED2a: document/persistence API Editor — 2026-10-23

Versioned .aetherscene codec/store и owned document/authoring session подключены
через App/Cobalt/IFactory. Atomic Load/Save/SaveAs, dirty по ContentRevision,
captured save, Undo checkpoint, SHA256 conflict guard и document Save/Discard/Cancel
API реализованы. Project dirty/busy guard и cleanup retry сохраняют ownership;
общая UI Close/Switch/Exit транзакция остаётся открытой. Новых engine/wire/UI-library
dependencies нет. Детали и exact requirement matrix —
[в истории Aether](engine/HISTORY.md#ed2a-документ-сцены-и-persistence--2026-10-23).

143новых cases, full2804/2804 Debug и Release по10TRX/failed0/skipped0;
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor284. Nonincremental build0errors/1531warnings.
Source Debug/published Release вне repo:10headless и6native cases/10PNG/4reaped
children, Vulkan0/0 и zero owned UI/GPU resources.17commands включая6ожидаемых
negative exits.229solution source/39Editor source/8binary hashes проверены;
TestResults/Documents-20261023/verification.json. Published scene check read-only,
Save/reopen/atomic failure — direct filesystem tests. Fresh packages и аппаратные/
performance/golden gates не повторялись. Далее document UI commands/close policy,
затем Transform/Undo/preview; первые4ED2 пункта закрыты, остальные ED1–ED8 открыты.

## ED1a: проект и сессия Editor — 2026-10-23

UseAetherEditorProjects предоставляет App/Cobalt/IFactory project/session layer,
strict versioned .aetherproj и selected SDK preflight без окна/engine/build.
Failed/cancelled Open сохраняет active, Close fences/drains; ownership и Microsoft
DI проверены. Normal launch больше не ищет checkout/demo; .aetherproj/--check-project
работают по explicit path, legacy csproj/game workflow сохраняется. Детали:
[контракт ED1a](engine/EDITOR_PLAN.md#ed1a-первый-projectsession-profile--2026-10-23),
[requirement/tests/commands/evidence](engine/HISTORY.md#ed1a-projectsession-и-запуск-вне-repository--2026-10-23).

Final full2661/2661 Debug и2661/2661 Release,10TRX/configuration,failed/skipped0,
Aether1008/Editor141;92новых cases. Nonincremental Debug0errors/1531warnings.
216source hashes; final source/published6native cases/10PNG/4reaped children,
4headless SDK/invalid-global.json cases,11commands/32Editor source/8binary hashes.
Published app/project/CWD вне repository без demo DLL. Workspace profiles не
стартуют compiler/child; legacy workflow повторён с Vulkan validation0/0.
Во всехnative случаях project/UI/GPU ownership освобождён.
Evidence TestResults/Projects-20261023/verification.json; initial passes до
последней UI placeholder correction сохранены отдельно.

Engine/runtime/wire/UI-library APIs/dependencies не менялись; packages/golden/
performance/hardware acceptance не повторялись. ED1 имеет3completed metadata/
startup tasks, общая document/runtime switch transaction остаётся открытой.
Следом ED2 scene format/document/load/save/reopen, затем authoring/preview UI.
Новых общих Core/Cobalt prerequisites нет; publication/commit не выполнялись.

## Независимый Edit preview Aether — 2026-10-23

После ARCH1b.1 реализован первый GPU Edit preview: resolved document/revision
передаётся через существующий Enerit control built-in paused render child,
Mithril сохраняет process ownership. Build/Play/Game.Logic не требуются; App/
Cobalt/IFactory управляют созданием и lifetime. Preview готов только после actual
presented ticket; replacement, resize/suspend/resume, cancel/Stop/cleanup retry
проверены. Engine profile не подключён к ordinary Editor UI; следом ED1a project/
session, документы/save/reopen и UI integration. Детали/requirement map:
[engine HISTORY](engine/HISTORY.md#edit-preview-независимый-engine-profile--2026-10-23).

Фактически выполнены nonincremental Debug build0errors/1542warnings и full solution
**2569/2569 Debug и2569/2569 Release**,10TRX/configuration, failed/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether1008/Editor49.121новыйcase, final focused134/134 с13existing GPU pipe cases.
Native preview Debug/published Release: по6PNG/5reaped children; published вне repo
без Game.Logic.dll. Старый Editor workflow Debug/published Release: по8commands/
3PNG/2reaped children. Все4cases validation0/0 и zero owned resources.
Normal peer disconnect defect GPU producer воспроизведён native и исправлен
точно на wire read/write; partial messages и arbitrary IO failures остаются faults.

Fresh package-only consumers8/8 (Debug/Release × build/publish × Cobalt/MS DI):
9nupkgs/22commands, isolated version0.2.2.7-alpha.preview.20261023110622747.
Это managed API/ownership proof; native drawing доказан отдельно.188solution/
140native/301package source hashes, retained failures/exact commands и итоговый
verification.json — в TestResults/EditPreview-20261023. Новых межмодульных
dependencies/assembly/protocol versions нет; пакеты не публиковались.
ED1–ED8/полный authoring/World/edit/MCP и hardware/final UI scope не закрыты.

## ARCH1b.1: локальное authoring API Aether — 2026-10-23

По запросу «давай следующий шаг» реализован минимальный общий scene query/
Transform/history API без UI и Runtime. UseAetherAuthoring/IFactory предоставляют
caller-owned sessions, shared resolver и finite limits; atomic batches, revision/
operation identity, cancel/error и Undo/Redo используют одного owner. Новых
assembly/межмодульных dependencies нет. Детали и requirement/test map ведутся
у [Aether](engine/HISTORY.md#arch1b1-headless-transform-authoring-и-история--2026-10-23).

Фактически выполнен nonincremental Debug build:0errors/1522warnings. Full solution
**2448/2448 Debug и2448/2448 Release**,10TRX/configuration, failed0/skipped0:
Core117/Cobalt179/generator91/Enerit166/generator114/Mithril99/UI661/Hosting85/
Aether887/Editor49.87новых headless cases, strict focused run87/87. Evidence:
TestResults/Authoring-20261023/solution/summary.json; текущие Aether production/test
source hashes139зафиксированы и проверены после runs. Fresh package proof8/8:
Debug/Release × build/publish × Cobalt/Microsoft DI,9nupkgs/22commands,299producer
source hashes, isolated local feed/cache. Package final Completed=true:
TestResults/Authoring-20261023/package-20261023-102244573/summary.json.
Markdown/namespace/diff и aggregate evidence — TestResults/Authoring-20261023/verification.json.
Native viewport, physical UI и performance
не повторялись. Первый ENE6a/MIT6a source/published44/package132 остаётся предыдущим
профилем, не новым результатом этого изменения.

Следующий engine шаг — независимый Edit preview через Enerit; ED1a project/session
параллельно. ED1/ED2 storage и прикладной Transform/preview/save/reopen срез ещё
нужны; full ARCH1b, runtime World/edit/MCP и прежние hardware ограничения открыты.

## Первый ENE6a/MIT6a implementation slice — 2026-10-23

По запросу приступить к следующему шагу реализованы finite pipes admission/
options через DI/IFactory и observable Stop/Completion. При cancellation сохраняется
ownership до фактического writer/handler/decode drain; process Stop использует
отдельный Mithril lifecycle pipe, не насыщенный domain RPC. Mithril/Core/Cobalt
runtime API не менялись, wire/generator mapping/same-build scope сохранены.
Queued-writer CTS lifetime defect подтверждён детерминированным RED/GREEN и
исправлен capture токена значением. Ready-file sharing violation в fixture
обрабатывается bounded retry только Win32 errors32/33; JSON/IPC не повторяются.

Окончательный final3 full solution2361/2361 Debug/Release,10TRX/configuration,
failed0/skipped0;40новых cases. Nonincremental Debug build0errors/1522warnings.
Source/published44/44 и fresh independent package132/132,8nupkgs/152commands,
expected omitted Enerit analyzer CS0234, Completed=true. Предыдущие failed/pre-fix
runs сохранены и не считаются passing финального кода. [Все команды, tests и границы](enerit/HISTORY.md#ene6a-finite-admission-и-наблюдаемая-остановка--2026-10-23),
[process acceptance](mithril/HISTORY.md#mit6a-saturated-ipc-и-независимый-process-stop--2026-10-23).

Native Editor viewport, performance, physical UI/hardware, полный fault matrix
и long resource trends не повторены/не закрыты. Первый bounded prerequisite
готов; локальный ARCH1b/headless и ED1a остаются следующим engine/application шагом.

## Сверка основы и улучшения IPC/процессов — 2026-10-23

По запросу пользователя проверены общие и Core/Cobalt/Enerit/Mithril/Aether планы/
статусы, quality matrix и ключевые transport/process integration paths. Чтение
исходников подтвердило unbounded pending map и scheduler queues Enerit,50s default
timeout, local-only request cancellation и synchronous handler drain в Dispose.
Это свойства текущего кода, не воспроизведённые новые leaks/deadlocks. Aether уже
передаёт command deadlines, ограничивает control payload64KiB и имеет Hello/
heartbeat/session fencing; Mithril уже имеет bounded restart, stop fence и kill/
reap owned process. Parent observer запрашивает App cancel; abrupt parent death
с uncooperative child и длительные resource trends отдельного proof не имеют.
Aether NativeRuntimeLease читает stdout/stderr через ReadToEndAsync: pipe дренируется,
но retained text до завершения процесса не ограничен.

Вывод: локальный ARCH1b/headless и ED1a могут продолжаться без нового полного
Core/Cobalt quality прохода. До частого Runtime preview предложены ENE6a/b и MIT6a:
точечный contract/admission/resource bounds и совместный stop/drain/kill/reap
fault/load proof. ENE6c/d — fail-fast совместимость и IPC diagnostics; MIT6b/c —
наблюдаемость по потребителю, bounded logs, parent-death/resource acceptance.
Новые транспорты/OS, расширенный restart и оптимизации после CPU/allocation profile
не блокируют первый срез. Публичные API и новые гарантии ещё не утверждены.
Единый детальный межмодульный acceptance ведётся у Mithril; [порядок и ссылки](PLAN.md#сверка-основы-перед-aether--2026-10-23).

Исправлены устаревшие current next steps Core/Cobalt/Enerit и открытый DI статус
в quality matrix; исторические даты/counts сохранены. Изменена документация,
production code/API не менялись, build/tests/native/packages/performance не
запускались. Проверка links/added anchors/whitespace сохраняется в
TestResults/FoundationReview-20261023/verification.json; её scope — документация.
Итог:21documents/782local links/38added anchors, missing targets/anchors0;
ED1–ED8/40unchecked tasks сохранены, source changes0, git diff --check прошёл.

## Исправление структуры проектов — 2026-10-21

По запросу пользователя исправлены замечания [исходного аудита](#аудит-размещения-типов-и-namespace--2026-10-21).
Все 44 проекта охвачены изменениями или проверкой; после разделения Control.cs
получилось 597 авторских C#-файлов вместо 590. Структурная проверка после переносов:
namespace mismatch0, global-type violations0, технические папки0, parse errors0,
orphaned sources0. Top-level statements сохранены, явно объявленные типы из
таких программ получили namespace своего проекта.

### Размещение и изменения public API

| Прежнее размещение / namespace | Текущее размещение / namespace |
| --- | --- |
| Aether.Contracts: IGameSession, SessionIdentity | Aether/Sessions, `TrueMoon.Aether.Sessions` |
| Aether.Contracts: SceneEntity, SceneSnapshot | Aether/Scenes, `TrueMoon.Aether.Scenes` |
| Aether.Contracts: IEngineControl, CommandKind, ControlRequest, ControlReply | Aether/Transport, `TrueMoon.Aether.Transport` |
| Aether.Contracts: frame types/codec/pattern | Aether/Frames/Frames.cs, `TrueMoon.Aether.Frames` |
| Aether.Contracts: checkpoint types/capability/outcomes/codec | Aether/Checkpoints, `TrueMoon.Aether.Checkpoints` |
| Alloy/Runtime с namespace TrueMoon.Alloy | `TrueMoon.Alloy.Runtime`, по реальной папке |
| Contracts/Exceptions/AppCreationException, TrueMoon.Exceptions | Корень Contracts, `TrueMoon.AppCreationException` |
| Contracts/Configuration/UnitsConfigurationExtensions | Корень Mithril, `TrueMoon.Mithril.UnitsConfigurationExtensions` |
| Enerit.Generator/Extensions и Utils | Корень generator, `TrueMoon.Enerit.Generator` |
| MithrilTest/Models/TestPoco.cs | Корень примера, `MithrilTest` |

IGameSession и остальные engine contracts остаются в одной Aether assembly.
Общая TrueMoon.Contracts сохраняет межмодульные contracts/base models/interfaces
без зависимости на реализации модулей. Units helper относится к module-specific
units configuration и теперь поставляется Mithril, который уже использует общий
IConfiguration. Его аргументы `-u`/`-p` и поведение не изменены.

У Mithril.Tests RootNamespace исправлен на TrueMoon.Mithril.Tests. У shared
CorePackageConsumer/CoreHeadlessProbe явно задан Headless.Consumer, у DirectCobalt —
Direct.Consumer, у Enerit benchmark — EneritBaseline. Эти значения сохраняют
намеренные shared consumer namespaces и имена исполняемых сборок, согласуя их
с правилом RootNamespace. 17файлов с global types получили namespace; ссылки
Cobalt/Enerit generator и примеры обновлены вместе с ними.

Изменены namespace публичных alpha API и assembly units helper: внешним
consumers нужны соответствующие using/qualified-name changes и пересборка.
Совместимые копии API в старых технических папках не оставлены. Обновлены C#
raw-source fixtures, package template, текущие README/PACKAGE и source links;
исторические записи ниже сохраняют прежнее состояние, их ссылки ведут к новым
исходникам. Логика production runtime, wire layout и версии протоколов не менялись;
отдельно исправлена обнаруженная во время проверки ошибка sample GPU binding.
Сравнение 26 650 C#-литералов до/после (с нормализацией перенесённых namespace
в C# samples) не выявило неожиданных изменений, включая raw strings.

### Выполненные managed проверки

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental -m --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --no-build --no-restore --logger trx --results-directory TestResults/StructureFix/tests-Debug-final2 --verbosity quiet
dotnet build TrueMoon.slnx --configuration Release --no-incremental -m --verbosity quiet
dotnet test TrueMoon.slnx --configuration Release --no-build --no-restore --logger trx --results-directory TestResults/StructureFix/tests-Release-final2 --verbosity quiet
```

Каждая сборка: errors0/warnings1499. Каждая test configuration: **Passed: 1898,
Failed: 0, Skipped: 0**,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI615/Hosting79/Aether493. RichTextEditorTest, находящийся вне solution,
отдельно собран в Release; source CoreHeadlessProbe отдельно собран в Debug и
Release с `--no-dependencies`, используя уже проверенные project references.
Первые промежуточные builds выявили недостающие Runtime
imports из дочерних Alloy namespaces, затем29duplicate usings; они исправлены
перед окончательными managed runs. Новых product test cases не добавлено.

### Consumer проверки

Последовательный wrapper `TestResults/StructureFix/ValidateConsumers.ps1` сохраняет
точные команды/exit codes в consumer-summary.json и отдельные stdout/stderr logs.
Core/units/UI package runners используют свежие версии, отдельные feeds/caches и
проверяют generated adapters/consumer graphs; публикация пакетов не выполнялась.

- `ManualTests/CorePackageConsumer/RunPackageSmoke.ps1`:80cases и3package README
  examples, выход0; `TestResults/CorePackages/structure-20261021/summary.json`.
- `ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1`:108cases, выход0;
  `TestResults/UnitsPackages/structure-20261021/summary.json`.
- `ManualTests/AlloyFactoryConsumer/RunIntegration.ps1`, profiles Source/Package:
  16source и8package native processes на Cobalt/DI × OpenGL/Vulkan × Debug/published
  Release; собственные runner/source/artifact checks прошли. Отчёты —
  `TestResults/AlloyInfrastructure/structure-{source,package}-20261021/summary.json`.
- `ManualTests/AlloyPackageConsumer/RunPackageSmoke.ps1`:36cases и4direct
  consumer cases; `TestResults/AlloyPackages/structure-20261021/summary.json`.
- `TestResults/StructureFix/ValidateAetherPackage.ps1`:12fresh packages и4App
  consumers (Cobalt/DI × Debug/published Release), managed GPU API/ownership и
  Skia native delivery. Независимая сверка payload hashes/nuspec/assets прошла;
  `aether-package/summary.json` и `aether-package-verified.json`.

### Найденная ошибка native composition и промежуточные runs

Первый run `TestResults/EngineSessions/structure-20261021` остановился после
14passing cases: Release GPU Editor прошёл20Replace, затем assertion ошибки
candidate/rollback завершил run. Само исходное исключение до assertion не было
записано. RecordFailureAsync теперь сохраняет ошибку, RecoveryError, flags и
editor-error identity до проверки; условия assertions не ослаблены. Два отдельных
Release GPU повтора в `TestResults/StructureFix/native-diagnostic` прошли20Replace/
20failure cases/53reaped children каждый; это не заменило общий повтор.

Второй общий run `TestResults/EngineSessions/structure-final-20261021` подтвердил
конкретную причину: `Single(l => l.ProcessId == runtime.ProcessId)` искал lease
среди всей истории запусков, а Windows повторно выдал PID58800. Две записи
дали `Sequence contains more than one matching element` при подключении GPU.
Первый failed report также содержит повторный PID58404, но его исходный
RecoveryError не был сохранён, поэтому точную причину первого assertion отдельно
не утверждаем. Оба failed runs сохранены с исходными logs и cleanup reports.

В EditorViewportProbe fault decorator теперь хранит и возвращает конкретный
NativeRuntimeLease, а ViewportProbe передаёт текущий native lease напрямую.
История/PID не используются для выбора процесса; владение и borrowed handle
остались прежними. Это устраняет подтверждённый механизм PID-reuse отказа.
После исправления повторены full solution Debug/Release builds/tests, указанные
выше. Package consumers от этого sample-only изменения не зависят и не повторялись.

### Итоговый native run и окончательная сверка

После исправления PID lookup выполнен `TestResults/StructureFix/ValidateNativeVerified.ps1`:
`ManualTests/EngineSessionProbe/RunProbe.ps1` с Scenarios=build-artifacts/checkpoint/
replacement, IncludeViewport/IncludeGpuCapabilities/IncludeGpuImport/IncludeGpuSurface/
IncludeGpuViewport/IncludeEditorGpu, Debug и published Release. Validation layer
из прежнего локального TestResults/ENG4b передан только принадлежащему runner
process tree. Никакие system/driver settings не менялись.

`TestResults/EngineSessions/structure-verified-20261021/summary.json`: Completed=true,
**20cases/22commands**,413source/76binary fingerprints. CPU и GPU Editor в каждой
конфигурации прошли20Replace/20failure cases/53reaped children/MaxLive1. UI sessions,
CPU mappings, parent GPU resources/handles0; GPU imported readbacks0. Исходный
native scope54cases ENG4d2 целиком не повторялся: этот targeted run проверяет
затронутые game artifacts/checkpoint/replace и CPU/GPU consumers.

Финальный Roslyn audit повторён после всех C# changes:44projects/88evaluations/
597files, namespace/category/parse/orphan violations0. Повторная literal review:
26 650 значений до/после без неожиданных различий. Project/source fingerprints,
TRX, native/editor reports, consumer summaries, Markdown links/anchors и diff
проверяются независимо в `TestResults/StructureFix/VerifyFinal.ps1`; результат
хранится в completion.json. Независимая сверка прошла:597source и44project hashes,
413native source/76binary hashes,18TRX,587локальных ссылок/249anchors;
`git diff --check` без ошибок. Failed промежуточные runs не переименованы в passing.
Аппаратные UI K3/K7/K8/VRAM/final alpha ограничения сохраняются; drivers/Windows
settings не менялись, поиск K7 workaround не возобновлялся. Функциональный
следующий шаг остаётся ENG4e — сопоставимый measured CPU/GPU baseline до 3D.

## Аудит размещения типов и namespace — 2026-10-21

Это исходный снимок до [исправлений](#исправление-структуры-проектов--2026-10-21).
Counts и замечания ниже сохраняют результат первоначальной проверки.

По запросу пользователя проверены все **44 C# проекта** репозитория, в том числе
7 проектов вне TrueMoon.slnx (в solution 37). Проверены 590 авторских C#-файлов,
включая тесты, генераторы, benchmarks и ManualTests. TrueMoon.Magnesium не содержит
авторских исходников/проекта и отдельной проверенной сборкой не считается.

Метод: список файлов git, 88 вычислений MSBuild properties/Compile items для
Debug/Release, затем Roslyn C# syntax trees с соответствующими defines.
Вызовы `dotnet msbuild` использовали `-getProperty` и `-getItem` без build targets.
RootNamespace и Compile membership одинаковы в обеих конфигурациях. Все 590 файлов
принадлежат проектам и включены в Compile; ошибок разбора 0. Это синтаксический
аудит, не сборка продукта. 24 linked inclusions в 3 проектах проверены по реальному проекту-владельцу файла;
виртуальный Link путь не считается его новым namespace. Raw strings с C# samples
в generator tests не считаются объявлениями, generated bin/obj sources исключены.
Top-level statements допустимы; явно объявленные global types учитываются отдельно.
Инструмент проверки собран через SDK Roslyn assemblies без
внешних PackageReference. Product build/test/native/pack в этом аудите не запускались.

Локальные evidence: `TestResults/StructureAudit/{evaluations.json,sources.json,audit.json}`,
Collect.ps1, tool/Program.cs, WriteReport.ps1. audit.json хранит все файлы/types/
namespace, source/project SHA256, baseline commit и ручную классификацию папок.
Эти ignored evidence не входят в поставку. Проверка от baseline784996a,
при существующих документационных изменениях правила; production sources не менялись.

### Назначение TrueMoon.Contracts

Общий пакет контрактов, базовых моделей и интерфейсов для взаимодействия модулей,
включая App/service/configuration/diagnostics API. Это не каталог всех интерфейсов
TrueMoon: engine/game/frame, UI и IPC-specific contracts остаются у своих модулей.
34 исходных файла соответствуют RootNamespace=`TrueMoon` и папкам; отличающееся имя
пакета TrueMoon.Contracts не является namespace нарушением. ProjectReference0,
PackageReference 0; папки Configuration/Diagnostics/Services/Dependencies имеют
функциональное назначение. ConfigurableBase и небольшие helpers не считаются
нарушением только из-за наличия реализации: они поддерживают общий API.

Два замечания разного рода: Exceptions/AppCreationException — техническая
группировка по виду типа (ниже); Configuration/UnitsConfigurationExtensions —
кандидат на перенос ответственности в Mithril. Helper задаёт `-u`/`-p`, определяет
processing unit и читает parent id; текущие authored callers — только Mithril.
Это смысловой вопрос публичного API, а не namespace ошибка и не основание
автоматически переносить все типы с одним текущим consumer. При исправлении
нужно определить целевое module API и совместимость extension methods/constants.

### Реестр всех проектов

«Namespace» — число файлов с несовпадением либо явно объявленными global types.
«Папки» — число файлов, нуждающихся в смысловой перегруппировке по ручному review.
Нулевые значения означают отсутствие замечаний в этом structural scope, не новую
готовность/runtime regression. У CoreHeadlessProbe собственных файлов0, но3linked.

| Проект | Собственные C#files | Namespace | Папки | Linked inclusions |
| --- | ---: | ---: | ---: | ---: |
| [TrueMoon.Cobalt.Benchmarks](../Benchmarks/TrueMoon.Cobalt.Benchmarks/TrueMoon.Cobalt.Benchmarks.csproj) | 8 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Benchmarks](../Benchmarks/TrueMoon.Enerit.Benchmarks/TrueMoon.Enerit.Benchmarks.csproj) | 3 | 2 | 0 | 0 |
| [AlloyFactoryConsumer](../ManualTests/AlloyFactoryConsumer/AlloyFactoryConsumer.csproj) | 1 | 1 | 0 | 0 |
| [AlloyPackageConsumer](../ManualTests/AlloyPackageConsumer/AlloyPackageConsumer.csproj) | 3 | 0 | 0 | 0 |
| [AlloyTest](../ManualTests/AlloyTest/AlloyTest.csproj) | 13 | 0 | 0 | 0 |
| [AlloyVulkanTest](../ManualTests/AlloyVulkanTest/AlloyVulkanTest.csproj) | 35 | 1 | 0 | 7 |
| [AppUnitsPipesProbe](../ManualTests/AppUnitsPipesProbe/AppUnitsPipesProbe.csproj) | 2 | 1 | 0 | 0 |
| [CobaltTest](../ManualTests/CobaltTest/CobaltTest.csproj) | 10 | 9 | 0 | 0 |
| [CoreHeadlessProbe](../ManualTests/CoreHeadlessProbe/CoreHeadlessProbe.csproj) | 0 | 0 | 0 | 3 |
| [CorePackageConsumer](../ManualTests/CorePackageConsumer/CorePackageConsumer.csproj) | 3 | 2 | 0 | 0 |
| [DirectCobalt](../ManualTests/CorePackageConsumer/DirectCobalt/DirectCobalt.csproj) | 2 | 1 | 0 | 0 |
| [EneritPipesProbe](../ManualTests/EneritPipesProbe/EneritPipesProbe.csproj) | 2 | 1 | 0 | 0 |
| [EngineSessionProbe](../ManualTests/EngineSessionProbe/EngineSessionProbe.csproj) | 13 | 0 | 0 | 0 |
| [Game.Logic](../ManualTests/Game.Logic/Game.Logic.csproj) | 1 | 0 | 0 | 0 |
| [MithrilCompositionProbe](../ManualTests/MithrilCompositionProbe/MithrilCompositionProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilLifecycleProbe](../ManualTests/MithrilLifecycleProbe/MithrilLifecycleProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilRestartProbe](../ManualTests/MithrilRestartProbe/MithrilRestartProbe.csproj) | 1 | 1 | 0 | 0 |
| [MithrilTest](../ManualTests/MithrilTest/MithrilTest.csproj) | 5 | 0 | 1 | 0 |
| [RichTextEditorTest](../ManualTests/RichTextEditorTest/RichTextEditorTest.csproj) | 2 | 0 | 0 | 0 |
| [UnitsPackageConsumer](../ManualTests/UnitsPackageConsumer/UnitsPackageConsumer.csproj) | 1 | 1 | 0 | 0 |
| [TrueMoon.Aether.Tests](../TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj) | 23 | 0 | 0 | 0 |
| [TrueMoon.Aether](../TrueMoon.Aether/TrueMoon.Aether.csproj) | 42 | 0 | 4 | 0 |
| [TrueMoon.Alloy.Hosting.Tests](../TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj) | 7 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Hosting](../TrueMoon.Alloy.Hosting/TrueMoon.Alloy.Hosting.csproj) | 47 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Platform.Silk](../TrueMoon.Alloy.Platform.Silk/TrueMoon.Alloy.Platform.Silk.csproj) | 10 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/TrueMoon.Alloy.Rendering.Skia.csproj) | 10 | 0 | 0 | 0 |
| [TrueMoon.Alloy.Tests](../TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj) | 33 | 0 | 0 | 14 |
| [TrueMoon.Alloy](../TrueMoon.Alloy/TrueMoon.Alloy.csproj) | 13 | 12 | 0 | 0 |
| [TrueMoon.Argentis](../TrueMoon.Argentis/TrueMoon.Argentis.csproj) | 48 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Generator.Tests](../TrueMoon.Cobalt.Generator.Tests/TrueMoon.Cobalt.Generator.Tests.csproj) | 4 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Generator](../TrueMoon.Cobalt.Generator/TrueMoon.Cobalt.Generator.csproj) | 5 | 0 | 0 | 0 |
| [TrueMoon.Cobalt.Tests](../TrueMoon.Cobalt.Tests/TrueMoon.Cobalt.Tests.csproj) | 26 | 0 | 0 | 0 |
| [TrueMoon.Cobalt](../TrueMoon.Cobalt/TrueMoon.Cobalt.csproj) | 26 | 0 | 0 | 0 |
| [TrueMoon.Contracts](../TrueMoon.Contracts/TrueMoon.Contracts.csproj) | 34 | 0 | 1 | 0 |
| [TrueMoon.Core](../TrueMoon.Core/TrueMoon.Core.csproj) | 5 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Generator.Tests](../TrueMoon.Enerit.Generator.Tests/TrueMoon.Enerit.Generator.Tests.csproj) | 9 | 0 | 0 | 0 |
| [TrueMoon.Enerit.Generator](../TrueMoon.Enerit.Generator/TrueMoon.Enerit.Generator.csproj) | 12 | 0 | 6 | 0 |
| [TrueMoon.Enerit.Tests](../TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj) | 21 | 0 | 0 | 0 |
| [TrueMoon.Enerit](../TrueMoon.Enerit/TrueMoon.Enerit.csproj) | 35 | 0 | 0 | 0 |
| [TrueMoon.Extensions.DependencyInjection](../TrueMoon.Extensions.DependencyInjection/TrueMoon.Extensions.DependencyInjection.csproj) | 4 | 0 | 0 | 0 |
| [TrueMoon.Mithril.Tests](../TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj) | 9 | 8 | 0 | 0 |
| [TrueMoon.Mithril](../TrueMoon.Mithril/TrueMoon.Mithril.csproj) | 19 | 0 | 0 | 0 |
| [TrueMoon.Tests](../TrueMoon.Tests/TrueMoon.Tests.csproj) | 13 | 0 | 0 | 0 |
| [TrueMoon](../TrueMoon/TrueMoon.csproj) | 28 | 0 | 0 | 0 |

### Namespace замечания: 42 файла в 14 проектах

25 файлов имеют явно указанный namespace, отличный от ожидаемого, ещё 17 содержат
явно объявленные типы в global namespace. Генерируемый compiler Program для
top-level statements не считается авторским global type. 548 остальных файлов
не имеют namespace замечаний; синтаксический анализ не проверяет emitted code.

| Файл | Объявленный namespace | Ожидаемый по текущему RootNamespace/пути |
| --- | --- | --- |
| [Benchmarks/TrueMoon.Enerit.Benchmarks/Contract.cs](../Benchmarks/TrueMoon.Enerit.Benchmarks/Contract.cs) | `EneritBaseline` | `TrueMoon.Enerit.Benchmarks` |
| [Benchmarks/TrueMoon.Enerit.Benchmarks/Measurement.cs](../Benchmarks/TrueMoon.Enerit.Benchmarks/Measurement.cs) | `EneritBaseline` | `TrueMoon.Enerit.Benchmarks` |
| [ManualTests/AlloyFactoryConsumer/Program.cs](../ManualTests/AlloyFactoryConsumer/Program.cs) | (global) | `AlloyFactoryConsumer` |
| [ManualTests/AlloyVulkanTest/AsyncCloseProbe.cs](../ManualTests/AlloyVulkanTest/AsyncCloseProbe.cs) | (global) | `AlloyVulkanTest` |
| [ManualTests/AppUnitsPipesProbe/Program.cs](../ManualTests/AppUnitsPipesProbe/Program.cs) | (global) | `AppUnitsPipesProbe` |
| [ManualTests/CobaltTest/IMainTestClass.cs](../ManualTests/CobaltTest/IMainTestClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IService1.cs](../ManualTests/CobaltTest/IService1.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IService2.cs](../ManualTests/CobaltTest/IService2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/IServiceTest2.cs](../ManualTests/CobaltTest/IServiceTest2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/ITestGenericClass.cs](../ManualTests/CobaltTest/ITestGenericClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/MainTestClass.cs](../ManualTests/CobaltTest/MainTestClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/Service1.cs](../ManualTests/CobaltTest/Service1.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/Service2.cs](../ManualTests/CobaltTest/Service2.cs) | (global) | `CobaltTest` |
| [ManualTests/CobaltTest/TestGenericClass.cs](../ManualTests/CobaltTest/TestGenericClass.cs) | (global) | `CobaltTest` |
| [ManualTests/CorePackageConsumer/ConsumerProbe.cs](../ManualTests/CorePackageConsumer/ConsumerProbe.cs) | `Headless.Consumer` | `CorePackageConsumer` |
| [ManualTests/CorePackageConsumer/Graph.cs](../ManualTests/CorePackageConsumer/Graph.cs) | `Headless.Consumer` | `CorePackageConsumer` |
| [ManualTests/CorePackageConsumer/DirectCobalt/Graph.cs](../ManualTests/CorePackageConsumer/DirectCobalt/Graph.cs) | `Direct.Consumer` | `DirectCobalt` |
| [ManualTests/EneritPipesProbe/Program.cs](../ManualTests/EneritPipesProbe/Program.cs) | (global) | `EneritPipesProbe` |
| [ManualTests/MithrilCompositionProbe/Program.cs](../ManualTests/MithrilCompositionProbe/Program.cs) | (global) | `MithrilCompositionProbe` |
| [ManualTests/MithrilLifecycleProbe/Program.cs](../ManualTests/MithrilLifecycleProbe/Program.cs) | (global) | `MithrilLifecycleProbe` |
| [ManualTests/MithrilRestartProbe/Program.cs](../ManualTests/MithrilRestartProbe/Program.cs) | (global) | `MithrilRestartProbe` |
| [ManualTests/UnitsPackageConsumer/AnalyzerEvidence.cs](../ManualTests/UnitsPackageConsumer/AnalyzerEvidence.cs) | (global) | `UnitsPackageConsumer` |
| [TrueMoon.Alloy/Runtime/IUiSessionFactory.cs](../TrueMoon.Alloy/Runtime/IUiSessionFactory.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/Rendering.cs](../TrueMoon.Alloy/Runtime/Rendering.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiCleanup.cs](../TrueMoon.Alloy/Runtime/UiCleanup.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiClipboardException.cs](../TrueMoon.Alloy/Runtime/UiClipboardException.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDiagnostics.cs](../TrueMoon.Alloy/Runtime/UiDiagnostics.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDispatcher.cs](../TrueMoon.Alloy/Runtime/UiDispatcher.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiDispatchOptions.cs](../TrueMoon.Alloy/Runtime/UiDispatchOptions.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiFrameMode.cs](../TrueMoon.Alloy/Runtime/UiFrameMode.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiRenderingException.cs](../TrueMoon.Alloy/Runtime/UiRenderingException.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/UiSession.cs](../TrueMoon.Alloy/Runtime/UiSession.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/WindowAppearance.cs](../TrueMoon.Alloy/Runtime/WindowAppearance.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Alloy/Runtime/WindowChrome.cs](../TrueMoon.Alloy/Runtime/WindowChrome.cs) | `TrueMoon.Alloy` | `TrueMoon.Alloy.Runtime` |
| [TrueMoon.Mithril.Tests/CompositionProcessTests.cs](../TrueMoon.Mithril.Tests/CompositionProcessTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/ProcessDiagnosticsTests.cs](../TrueMoon.Mithril.Tests/ProcessDiagnosticsTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/ProcessUnitHandleTests.cs](../TrueMoon.Mithril.Tests/ProcessUnitHandleTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/RestartProcessTests.cs](../TrueMoon.Mithril.Tests/RestartProcessTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/TestSupport.cs](../TrueMoon.Mithril.Tests/TestSupport.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitCompositionTests.cs](../TrueMoon.Mithril.Tests/UnitCompositionTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitParentProcessEventsHandlerTests.cs](../TrueMoon.Mithril.Tests/UnitParentProcessEventsHandlerTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |
| [TrueMoon.Mithril.Tests/UnitsControllerTests.cs](../TrueMoon.Mithril.Tests/UnitsControllerTests.cs) | `TrueMoon.Mithril.Tests` | `TrueMoon.Titanium.Tests` |

У Mithril.Tests первопричина — устаревший csproj RootNamespace
`TrueMoon.Titanium.Tests`:8файлов уже объявлены в TrueMoon.Mithril.Tests.
Исправлять следует RootNamespace, а не переименовывать тесты в Titanium.
Alloy/Runtime12files сейчас объявлены в TrueMoon.Alloy; реальная Runtime
подсистема должна отражаться в namespace. Это изменение public API: вместе
обновляются consumers, aliases, docs и генерация. Для shared consumer samples
Headless.Consumer/Direct.Consumer допустимо явно задать подходящий RootNamespace;
решение должно сохранять согласованность реального пути и linked compilation.

### Технические группы: 5 папок, 12 файлов в 4 проектах

Здесь namespace уже соответствуют текущим папкам; замечание относится к самим
категориям. Группировка по Contracts/Models/Exceptions/Extensions/Utils не
показывает назначения типов. Целевые имена категорий ниже — рекомендации,
а не выполненные переносы или новые обязательные проекты.

| Папка | Файлы | Причина и направление исправления |
| --- | --- | --- |
| `TrueMoon.Aether/Contracts` | [Control.cs](../TrueMoon.Aether/Transport/IEngineControl.cs), [Frames.cs](../TrueMoon.Aether/Frames/Frames.cs), [GameCheckpoint.cs](../TrueMoon.Aether/Checkpoints/GameCheckpoint.cs), [GameCheckpointCodec.cs](../TrueMoon.Aether/Checkpoints/GameCheckpointCodec.cs) | Control.cs смешивает session/game/control/scene contracts; Frames.cs содержит frame types; GameCheckpoint.cs и GameCheckpointCodec.cs относятся к checkpoint state/codec. Распределить по Frames, Sessions, checkpoint/control подсистемам; общие для Aether типы без категории — в корень Aether. Не переносить engine-only API в TrueMoon.Contracts. |
| `TrueMoon.Contracts/Exceptions` | [AppCreationException.cs](../TrueMoon.Contracts/AppCreationException.cs) | Единственный AppCreationException сгруппирован по виду типа. Перенести в корень общего проекта либо вместе с реальной App категорией; учесть публичный namespace TrueMoon.Exceptions. |
| `TrueMoon.Enerit.Generator/Extensions` | [GenerationContextExtensions.cs](../TrueMoon.Enerit.Generator/GenerationContextExtensions.cs), [SourceHandleExtensions.cs](../TrueMoon.Enerit.Generator/SourceHandleExtensions.cs), [SourceHandleSerializationExtensions.cs](../TrueMoon.Enerit.Generator/SourceHandleSerializationExtensions.cs), [SymbolExtensions.cs](../TrueMoon.Enerit.Generator/SymbolExtensions.cs) | Четыре файла объединены формой extension methods; содержат обработку symbols и генерацию методов/сериализации. Группировать по назначению генерации/сериализации/symbol analysis либо размещать в корне generator, рядом с GenerationContext/SourceHandle. |
| `TrueMoon.Enerit.Generator/Utils` | [GenerationUtils.cs](../TrueMoon.Enerit.Generator/GenerationUtils.cs), [WellKnownTypes.cs](../TrueMoon.Enerit.Generator/WellKnownTypes.cs) | GenerationUtils и WellKnownTypes скрывают конкретную работу с Roslyn type symbols под общей технической категорией. Расположить рядом с относящимися к ним типами или в смысловой категории обработки symbols; namespace должен следовать новому пути. |
| `ManualTests/MithrilTest/Models` | [TestPoco.cs](../ManualTests/MithrilTest/TestPoco.cs) | TestPoco.cs содержит набор тестовых DTO, records, enums и тестовый класс, объединённых видом типов. Для текущего небольшого примера — корень MithrilTest; отдельную категорию вводить только по конкретному сценарию. |

### Рекомендуемый порядок исправления

1. Исправить stale RootNamespace у Mithril.Tests;8замечаний устраняются одной
   настройкой без переименования существующих C#namespace.
2. Привести12Alloy/Runtime namespace к фактической подсистеме с обновлением
   source/package consumers, partial/aliases и module checks.
3. Перегруппировать4Aether/Contracts files по назначению, разбивая смешанные
   файлы при необходимости; публичные engine contracts остаются в Aether.
4. Разобрать AppCreationException и units helper в Contracts/Core/Mithril scope,
   сохранив независимость общей сборки и явно определив API compatibility.
5. Перегруппировать6generator helper files Enerit и согласовать benchmark root
   namespace; повторить generator output/consumer проверки после изменений.
6. Согласовать namespace20manual-consumer files и расположение MithrilTest Models;
   для linked/shared examples учитывать обе компиляции и source-generation fixtures.

Массовые переносы/namespace/API changes в этом аудите не выполнялись. Детальный
реестр ведётся здесь, module STATUS ссылаются на него. Функциональный engine
маршрут ENG4e и аппаратные/release ограничения не меняются. Следующий шаг по
этому review — пункт1, затем более широкий public API scope пункта2.

Завершающая сверка: SHA256 всех 590 C#-файлов и 44 проектов совпали с audit
snapshot; в 13 изменённых Markdown проверены 466 локальных ссылок и 201 anchor.
`git diff --check` прошёл. Результат — `TestResults/StructureAudit/verification.json`.

## Размещение типов по назначению — 2026-10-21

По запросу пользователя в AGENTS.md закреплено: типы без отдельной предметной
или функциональной категории размещаются в корне проекта. Подпапки группируют
связанные типы по назначению; вид типа (класс, DTO, модель, интерфейс) или статус
контракта сам по себе не требует Contracts/Models/DTOs/Interfaces. Namespace
соответствует фактическому пути. Отдельный TrueMoon.Contracts сохраняется.
Общий PLAN ссылается на правило, STATUS отделяет эту документационную правку от
проверок ENG4d2. Исходники/API не менялись; build/test/native не запускались.

## ENG4d2 ordinary GPU GameEditor — 2026-10-20

App option выбирает CPU(default)/GPU strategy; FrameSession сохраняет общий
Start/resize/Stop/first-present, GameEditor/Replacement — selected DLL/checkpoint/
rollback. Neutral surface/ticket не содержат native/mapping handles. Runtime GPU
metadata происходит из loaded/restored selected game; reference pixels пока
CPU-generated/Vulkan staging. UI production APIs прежние, engine одна Aether
assembly без UI dependency. Full Debug1898/1898/9TRX/failed0/skipped0, Aether493 и
UI615 Debug/Release, build0errors/1499warnings; fresh12packages/4App consumers.
Final54native cases/56commands/406source/76binary fingerprints; ordinary GPU Editor20Replace/20failure cases/53children per configuration, validation0/0,47devices/141slots balanced, imported readback0.
[Точные commands/evidence/limits и промежуточные ошибки](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед ENG5/3D. K3/K7/K8/VRAM/final alpha,
driver/Windows settings и остановка K7 workaround search прежние.

## ENG4d GPU viewport bridge — 2026-10-20

В Aether добавлен owner callback loan, в Rendering.Skia — независимая retained
GPU image copy. Reference actual viewport проверяет premul/stored sRGB/origin,
scale/clipping, presentation и image lifetime без imported readback delivery.
Ordinary GameEditor остаётся CPU; следующая точка ENG4d2 selected DLL/Build/Replace/
checkpoint GPU transport. Engine assembly одна; UI dependency в Aether не добавлена.

Full Debug1858/1858/9TRX/failed0/skipped0; Aether453 и UI615 Debug/Release.
Build0errors/1499warnings;52native cases/54commands,398source/76binary hashes;
fresh12packages/4consumers. GPU viewport per config17checks/14captures/3,051,552pixels,
20GPU copies/6reaped children, imported ReadPixels0, core/sync validation0/0.
Точные commands/reports, interim failures и ограничения —
[engine HISTORY](engine/HISTORY.md#eng4d-gpu-viewport-bridge--2026-10-20).
UI K3/K7/K8/VRAM/final alpha сохраняются; drivers/Windows settings не менялись.

## ENG4c reusable GPU surfaces — 2026-10-20

Aether App-owned surface session/channel2:3persistent slots/shared Vulkan device,
retirement перед reuse, resize/queued resize/suspend, Stop/cancel/deadline/crash/
restart и failed cleanup/retry. Ordinary viewport CPU default; далее ENG4d actual
GPU viewport/presentation/compositing. Все engine реализации в одной assembly,
новых dependencies/UI references нет.

Aether444/444 Debug/Release; full Debug1847/1847/9TRX/failed0/skipped0.
50native cases/52commands/exit0,394source/76binary fingerprints, build0errors/
1499warnings; fresh9packages/4Cobalt+DI consumers. Каждый surface run21checks/
80frames/12reaped children,9cooperative reports с15devices/45slots created/released,
validation core/sync errors0/warnings0. Остатки parent GPU/NT/UI0;3crash children
имеют owned reap без cooperative teardown claim. Old Editor held-worker marker
исправлен на atomic publication после воспроизведённой sharing violation; final
matrix повторена на fresh snapshot. [Commands/evidence/границы](engine/HISTORY.md#eng4c-reusable-slots-и-surface-lifecycle--2026-10-20).
Full Release solution/UI packages/golden/RTE/performance/hardware не повторялись;
K3/K7/K8/VRAM/final alpha прежние, drivers/Windows settings не менялись.

## ENG4b one-frame GPU import — 2026-10-20

TrueMoon.Aether получил App-owned lazy GPU resources, dedicated Vulkan image/
binary semaphores, owned-process NT duplication, GL import и строгий ready/return/
retirement channel. Debug/published Release:14checks/import run,3whole-image
roundtrips,8reaped children, core/sync validation errors0/warnings0, остатки0.
Ordinary viewport CPU default; следующий ENG4c bounded slots/surface lifecycle.

Aether366/366 Debug/Release; full Debug1769/1769/9TRX/failed0/skipped0;
48native cases/50commands,388source/76binary fingerprints, build0errors/
1499warnings. Fresh9packages/4Cobalt+DI consumers подтверждают delivery/lazy
ownership; Aether→Silk.NET.Vulkan2.23.0, UI references нет. Детали/commands/
границы — [engine HISTORY](engine/HISTORY.md#eng4b-one-frame-import-и-retirement--2026-10-20).
Full Release solution/UI packages/golden/RTE/performance/hardware не повторялись.
UI K3/K7/K8/VRAM/final alpha прежние; drivers/Windows settings не менялись.

## ENG4a GPU capability decision — 2026-10-20

Pure Aether GPU snapshots/decision и App-owned child/current GL context probe
проверены. RTX5070Ti UUID/driver/exact image/handle/semaphore/GL requirements
допускают ENG4b import-probe; CPU selected/ImportTested=false, ENG4 в целом открыт.
Aether316/316 Debug/Release; full Debug1719/1719/9TRX/failed0/skipped0;
nonincremental0errors/1499warnings.46native cases/48commands/exit0,
377source/76binary hashes; новые cases по1reaped child/instance destroyed/
mappings/UI sessions0/authoring preserved. Actual import/shared GPU/3D/package/
performance и UI hardware acceptance не доказаны queries, drivers/settings прежние.
[Подробные tests/raw queries/команды/evidence](engine/HISTORY.md#eng4a-gpu-capabilities-и-transport-decision--2026-10-20).

## ENG3d Editor commands и proof — 2026-10-20

AetherHost.Editor добавляет UI-independent App-owned commands, immutable authoring
context/source/selection version и stale-result fences. Viewport имеет Build/
Replace/Cancel/Refresh controls; Stop/App close drains accepted work до UI disposal.
ENG3a–d завершены в bounded Windows/.NET10 CPU scope, следующий ENG4a capabilities/
transport decision. Core/Enerit/Mithril/UI production APIs прежние.

Aether254/254 Debug/Release, full Debug1657/1657/9TRX/failed0/skipped0;
nonincremental Debug0errors/1499warnings.44native cases/46commands/exit0,
372source/76binary hashes. Каждый Debug/published Release Editor proof:
20A/B Replace/20failure cases/775actual GL checks/53children/maxlive1/allreleased,
state/first tick/selected Step и authoring сохранены. Classic viewport по17cases/
27children/20StartStop; mappings/UI sessions0. UI hardware/package/performance
acceptance не расширен. [Единственные подробные tests/failures/команды/evidence](engine/HISTORY.md#eng3d-команды-editor-и-20replace-proof--2026-10-20).

## ENG3c serialized Replace и rollback — 2026-10-20

AetherHost.Replacement объединяет capture/old stop/reap/selected restore/first-present
и прежний mode, один rollback, shared command reservation и global45s budget.
Stop/App close fences late starts; unconfirmed cleanup сохраняет task/lease для
явного retry. Original/recovery errors наблюдаемы отдельно. Shared Core/Enerit/
Mithril/UI production APIs прежние, новых package dependencies нет.

Aether231/231 Debug/Release, full Debug1634/1634/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1499warnings.42native cases Debug/published
Release прошли: по8replacement children/max live1, actual A→B DLL/hash/state/CPU,
rollback и Stop inside held restore; по17viewport cases/27children/20StartStop,
actual GL first-present и cleanup.368source/76binary fingerprints.
[Единственные подробные tests/failures/команды/evidence](engine/HISTORY.md#eng3c-serialized-replace-и-rollback--2026-10-20).
Следующий ENG3d — Editor commands/20Replace acceptance при одном App/window.
Новые package consumers/full Release solution/performance/hardware не проверялись.

## ENG3b checkpoint и startup restore — 2026-10-20

В Aether реализованы optional checkpoint capability, immutable bounded TMCK v1
snapshot, paused observational capture с подтверждённым nonterminal отказом и
fresh startup restore до surface/first-present. Поддержан selected-build restore,
host/scene/revision preflight и explicit reset без checkpoint. Unknown outcome
сохраняет fault/cleanup policy; Stop во время restore не публикует кадры.
Общие production Core/Enerit/Mithril/UI APIs/dependencies прежние.

Aether184/184 Debug/Release, full Debug1587/1587/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1499warnings.40native cases в Debug apphost/
published Release, по16viewport cases/26children/20StartStop, actual restored
GL first-present, compiler/child close cleanup и mappings/sessions0. Source362/
binary76 fingerprints сохранены. [Единственные подробные tests/команды/evidence](engine/HISTORY.md#eng3b-checkpoint-и-startup-restore--2026-10-20).
Следующий ENG3c объединяет primitives в Replace/однократный rollback с общим45s
deadline; ENG3d proof и общий UI hardware/release остаются открытыми. Package
consumer нового API/full Release solution/performance matrix не повторялись.

## ENG3a compiler и immutable game builds — 2026-10-20

По принятому пользователем порядку накопленные extraction/App/package/rules/docs
изменения зафиксированы dc64b63. Затем в Aether реализованы immutable manifest/
artifact, owned compiler job и выбранный child-only loader; SourceRevision/build
completion не применяются автоматически к текущей сессии. App/viewport Closing
отменяет/reaps compiler параллельно с engine cleanup, до UI disposal. Общие
Core/Cobalt/Enerit/Mithril/UI production APIs для ENG3a не расширялись.

Aether139/139 Debug/Release; full Debug1542/1542,9TRX/failed0/skipped0;
nonincremental Debug build0errors/1508warnings.38native cases прошли, включая
2A/B build probes с actual child DLL path/hash/color/CPU pixels и2viewport со
held compiler/native close.356source/76binary fingerprints сверены.
[Единственные подробные contracts, commands, test matrix и границы](engine/HISTORY.md#eng3a-compiler-artifacts-и-child-loader--2026-10-20).

ENG3a завершён в bounded trusted-local managed/net10.0/Windows x64 scope;
следующий шаг — ENG3b checkpoint/restore, затем ENG3c transaction/rollback.
Пакеты/full Release solution/performance/long resource/hardware matrix этим
шагом не повторялись; общий release/UI alpha не объявлены готовыми.

## Аудит точек подключения модулей к App — 2026-10-19

После реализации UseAether по запросу пользователя проверены остальные модули:
публичные App extensions, реальные service/lifecycle registrations, project graph
и существующие focused integration tests. Проверены15активных library/analyzer
projects верхнего уровня, включая уже подключённый Aether. Test/executable projects
не являются самостоятельными функциональными модулями. TrueMoon.Magnesium —
пустая папка без csproj/исходников и записи в solution, отдельного модуля сейчас нет.

| Модуль / сборки | Публичная точка / способ подключения | Что подключает |
| --- | --- | --- |
| TrueMoon bootstrap | App.Builder(...).Setup(...), App.Build(...), App.RunAsync(...) | App-local configuration/resolver/lifecycle; встроенная основа приложения |
| Diagnostics в TrueMoon | [UseDiagnostics](../TrueMoon/Diagnostics/AppCreationContextExtensions.cs) | Diagnostics configuration, provider-owned subscription/factory; повторная регистрация заменяет настройки |
| TrueMoon.Cobalt | [AppBuilderConfigurationContext.GetServiceResolverBuilder](../TrueMoon/AppBuilderConfigurationContext.cs) | Default CobaltServiceResolverBuilder; дополнительный UseCobalt для обычного App не требуется |
| TrueMoon.Extensions.DependencyInjection | [UseDI](../TrueMoon.Extensions.DependencyInjection/AppCreationContextExtensions.cs) | Выбор provider через IAppBuilderConfigurationContext при App.Builder, до Setup/service resolution |
| TrueMoon.Enerit | [UseInvocationService / ListenInvocationService](../TrueMoon.Enerit/AppCreationContextExtensions.cs), [PipesServiceTransport](../TrueMoon.Enerit/IO/Pipes/EneriteConfigurationContextExtensions.cs) | Generated proxy/handler, client/server/factories; non-disposable IStartable/IStoppable adapters запускают/останавливают transport |
| TrueMoon.Mithril | [AddUnit](../TrueMoon.Mithril/AppCreationContextExtensions.cs) | App-local unit declarations/snapshots, UnitsController и lifecycle; child selection/parent monitor, main-process composition |
| Alloy UI через TrueMoon.Alloy.Hosting | [UseAlloy / UsePresentation](../TrueMoon.Alloy.Hosting/AppConfigurationExtensions.cs) | Renderer/session factory для external host либо standalone window/dispatcher с IStartable/IStoppable |
| TrueMoon.Aether | [UseAether](../TrueMoon.Aether/AppConfigurationExtensions.cs) | App-owned host/supervisor/frames; собственная реализация и proof описаны в engine history |

UseDI — настройка bootstrap provider, поэтому принимает IAppBuilderConfigurationContext,
а функциональные extensions работают на IAppConfigurationContext. Это два этапа
одного App builder, а не отсутствующая module registration. Общая configuration
доступна через IAppBuilder.Configuration и IAppConfigurationContext.Configuration.

Отдельные UseX не требуются у TrueMoon.Contracts/Core (contracts/utilities),
Argentis/Alloy/Rendering.Skia/Platform.Silk (UI слои с явной optional интеграцией
через Hosting) и Cobalt.Generator/Enerit.Generator (compile-time analyzers).
Compatibility.UsePresentation находится в отдельном legacy namespace и не
заменяет современный explicit renderer/host API. Закомментированный Enerit MMF
prototype не учитывался как рабочая точка подключения.

Пропущенных публичных точек подключения действующих функциональных модулей не
найдено; runtime code/API/project graph менять не потребовалось. При чтении
проверены options/registration и lifecycle paths. Это не полный повторный audit
всех cleanup/error/ownership контрактов или доказательство общего release.

### Фактически выполненные проверки

Windows x64/net10.0/SDK10.0.402, xUnit2.9.3/VSTest. Использованы существующие тесты,
без новых test cases/packages. Четыре команды exit0;4TRX, Passed:106, Failed:0,
Skipped:0. Core46, Enerit13, Mithril9, Alloy.Hosting38. Filtered test names
проверены по каждому TRX; пустая discovery не засчитывалась.

```powershell
dotnet test TrueMoon.Tests/TrueMoon.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~TrueMoon.Tests.AppLifecycleTests|FullyQualifiedName~TrueMoon.Tests.DiagnosticsTests|FullyQualifiedName~TrueMoon.Tests.AppTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Core --verbosity quiet
dotnet test TrueMoon.Enerit.Tests/TrueMoon.Enerit.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~AppInvocationRegistrationTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Enerit --verbosity quiet
dotnet test TrueMoon.Mithril.Tests/TrueMoon.Mithril.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~UnitCompositionTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Mithril --verbosity quiet
dotnet test TrueMoon.Alloy.Hosting.Tests/TrueMoon.Alloy.Hosting.Tests.csproj --configuration Debug --filter 'FullyQualifiedName~AlloyIntegrationTests|FullyQualifiedName~HostedUiSessionFactoryTests' --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AppModuleAudit/20261019/Alloy --verbosity quiet
```

Core tests повторили App startup/rollback/stop/dispose и diagnostics на обоих
providers. Enerit — App-local/idempotent proxy/listener lifetime и реальное
соединение named pipes в одном процессе на Cobalt/DI. Mithril — repeated Build
isolation, main/child configuration selection и local lifecycle на Cobalt/DI.
Alloy — registration/validation/snapshot и session-factory cleanup guards;
native window/GPU и реальный standalone App window этим запуском не проверялись.

Артефакты: `TestResults/AppModuleAudit/20261019/summary.json`, Completed=true,
stdout/stderr и4TRX;463source/project/build-input SHA256 совпали до/после запуска.
Это текущие focused Debug results; full solution/Release, собственные Cobalt/
generator suites, child-process/restart/native GPU/package/performance матрицы
не повторялись. Прежний полный Debug1518 и Aether115/36native относятся к
предыдущей реализации App integration. Общая очередность ENG3a и UI ограничения
K3/K7/K8/VRAM/final alpha не менялись.

## Aether App integration — 2026-10-19

По запросу пользователя реализован UseAether/AetherHost: options/services и
owned session/frame cleanup подключены к App lifecycle. В AGENTS.md и общем
плане закреплено правило подключения функциональных модулей через App contracts.
Utilities/contracts без lifecycle не требуют фиктивного UseX. Headless/viewport
examples используют публичную регистрацию модуля. Core/UI production API прежние.

Aether115/115 Debug/Release; full Debug1518/1518/9TRX/failed0/skipped0;
nonincremental Debug build0errors/1507warnings.36native cases Debug/published
Release включают новый app-stop и2viewport с actual pixels/20StartStop/cleanup.
9fresh packages и4independent Cobalt/DI consumers проверили App integration.
Aether IsPackable=true; прежний13+1deferred metadata audit ниже исторический,
полная metadata matrix всех44projects в этом шаге не повторялась. Публикации нет.
[Единственные подробные команды, тесты, failures и fingerprint evidence](engine/HISTORY.md#app-integration-реализация--2026-10-19).
Следующий общий code шаг — ENG3a; UI аппаратные/release ограничения сохраняются.

## NuGet package metadata — 2026-10-19

По запросу пользователя проверены metadata всех проектов, предназначенных для
NuGet-поставки, включая подготовку будущего Aether package. Сохранены uncommitted
перенос Aether и предшествующая сверка ENG3 плана. Runtime C#/API/test code в этом
metadata шаге не менялся. Новых unit/native/consumer/performance запусков нет;
прошлые Aether1490/34native и UI INF7 не объявляются результатами этого audit.

### Findings и исправления

- Description отсутствовал у Argentis/Alloy/Platform.Silk. Добавлены собственные
  descriptions и тематические tags всем13packable projects плюс deferred Aether.
- Core/Argentis/Alloy/Silk выбирали общий repository README. Добавлены собственные
  PACKAGE.md с явными PackageReadmeFile/None Pack. Aether также получил standalone
  package README; IsPackable=false сохранён, обычная поставка не включена.
- Общие PackageProjectUrl/RepositoryUrl/RepositoryType теперь заданы в
  Directory.Build.props; Authors/Company/Product/version/copyright/license/icon
  унаследованы. PackageId и version используют SDK defaults, лишние одинаковые
  значения по csproj не копируются. Базовые tags отражают TrueMoon/.NET/modules,
  специальные tags добавляются в владельце проекта.
- Shared license/icon/fallback README Includes привязаны к MSBuildThisFileDirectory,
  поэтому не зависят от глубины project directory. Существующая MIT license не
  изменялась. PackageReference/project dependency и generator ownership сохранены.

### Inventory и actual package proof

| Направление | Intended packages | Статус |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core |3packable |
| Cobalt / DI | TrueMoon.Cobalt, TrueMoon.Extensions.DependencyInjection |2packable; Cobalt.Generator остаётся embedded analyzer, не отдельный package |
| Enerit | TrueMoon.Enerit, TrueMoon.Enerit.Generator |2packable, generator — отдельный analyzer package |
| Mithril | TrueMoon.Mithril |1packable |
| UI | TrueMoon.Argentis, TrueMoon.Alloy, TrueMoon.Alloy.Platform.Silk, TrueMoon.Alloy.Rendering.Skia, TrueMoon.Alloy.Hosting |5packable |
| Aether | TrueMoon.Aether |1deferred; metadata подготовлены, default IsPackable=false |

Всего44repository projects с учётом untracked Aether/Aether.Tests и удаления старых
Contracts/test projects:13packable,1deferred,30excluded tests/manual/benchmarks/
embedded generator. Evaluated properties берутся из MSBuild, не только XML csproj.

Добавлен tracked [eng/Check-PackageMetadata.ps1](../eng/Check-PackageMetadata.ps1).
По умолчанию проверяет metadata; `-Pack` создаёт fresh local nupkgs в packages/
deferred-preview scopes. Проверяет ID/version/authors/company/product/description/
copyright/tags/project/repository/license/icon/readme, отсутствие placeholder
Description и наличие локального PACKAGE.md. Пакеты сверяются по actual nuspec,
README/license/icon bytes, managed DLL/XML и Cobalt/Enerit analyzer/Skia native/
buildTransitive payloads.99metadata input hashes защищают run от изменения файлов;
58owned dotnet commands имеют timeout/exit/logs, partial manifest сохраняется.
Existing RunDirectory не overwrite, только owned process tree cleanup, NuGet push нет.

```powershell
pwsh -NoProfile -File eng/Check-PackageMetadata.ps1 -Pack -RunDirectory TestResults/PackageMetadata/completion-20261019
```

Completedtrue,44projects/14intended/14archives:13regular и1Aether preview,
version0.2.2.7-alpha. Metadata и required payload assertions прошли. Aether preview
явно использует command-local IsPackable=true, не меняет project default. Пакеты
созданы в ignored TestResults/PackageMetadata/completion-20261019, не опубликованы.

Первый review-20261019 остановился на verifier bug: wildcard TrueMoon.Alloy.*
выбирал также Hosting/Skia/Silk archives. Исправлен exact ID+version filename;
reviewed и final runs прошли. Completion run выполнен после whitespace cleanup
XML и усиления проверки XML/license/icon bytes; это authoritative final input set.
Существующие compiler/generator warnings не подавлялись, API warnings policy этим
audit не менялась. Archive proof не заменяет independent runtime consumers/тесты.

Итоговые docs links/anchors/whitespace и read-only archive/input fingerprint checks
сохраняются отдельно в ignored TestResults/PackageMetadata/completion-check.json.
Passedtrue:39Markdown files/741local links/314anchors;99metadata inputs,
14archives/73payload hashes сверены повторно. Aether default nonpackable,
owned helpers завершены, staged0, git diff --check прошёл.
Общие правила добавлены в PLAN/R4, статусы всех затронутых направлений ссылаются
сюда. Следующий engine code шаг остаётся ENG3a; metadata не закрывает ENG3/release/
hardware blockers.

## TrueMoon.Aether — 2026-10-19

По запросу пользователя engine implementation/contracts вынесены в одну сборку
TrueMoon.Aether, tests — TrueMoon.Aether.Tests с direct ProjectReference. UI/game/
executable composition остаются отдельными consumers. Updated solution/map/namespaces,
без изменения общего Core/DI/IPC/units/UI API. Aether87/87 Debug/Release,
full Debug1490/1490/9TRX/failed0/skipped0 и34native cases прошли. Детальные
[решения/команды/evidence](engine/HISTORY.md#truemoonaether--2026-10-19) ведутся у engine.
Следующий этап ENG3a в Aether; experimental assembly не является release/packages/3D proof.

## Сверка плана после UI INF7 — 2026-10-19

Общий/UI/engine roadmap сопоставлен с текущим probe code. Очередность ENG3
сохранена; следующий конкретный code подэтап ENG3a compiler/artifact, затем
checkpoint/replacement/rollback/integration proof. Уточнения loader/identity,
restore-before-present, recoverable checkpoint failure, cancellation и общего
deadline ведутся только в [engine плане](engine/PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19).
Исправлены устаревшие ENG1/InputBatch/overlapping sessions формулировки; INF5/RTE
и hardware blockers не отменены. Это документация без новых build/tests/native/
performance результатов. [Findings/проверка](engine/HISTORY.md#сверка-плана-после-ui-inf7--2026-10-19).

## UI INF7 integration runner — 2026-10-19

Сохранён воспроизводимый runner Assembly/Source/Package/All с bounded owned
processes, fresh evidence, строгими TRX/package/native assertions и read-only
verifier. Production API и project wiring прежние. All:46commands,4TRX/1384passed
(UI613/Hosting79 Debug/Release),24native processes,10fresh nupkgs и4required
analyzer/native payloads;21guard checks и standalone Assembly прошли.
394source inputs/840artifact fingerprints сверены. Новый full solution не
запускался;1490 относится к INF6. Bounded INF0–INF4/INF6/INF7 и минимальный INF5
завершены; далее ENG3. Full INF5/RTE/hardware backlog сохраняются.
[Контракт/команды/failures/evidence](alloy/HISTORY.md#inf7-воспроизводимый-integration-runner--2026-10-19).

## UI INF6 explicit root factories — 2026-10-19

Новый UsePresentation(rootFactory, configure) без hidden view registration, snapshot
до Build и explicit renderer/target/presenter preflight. Cobalt/DI/generic overload
сохранены; custom standalone factory теперь декларирует IUiWindowSessionFactory.
Full Debug1490/1490,9TRX/failed0/skipped0; UI613/Hosting79 Debug/Release. Source
8native consumers/48App cases +4close/HUD processes и10fresh packages/8independent
native consumers без Cobalt analyzer прошли. Source/binary/package hashes сверены.
Следующий UI шаг INF7 runner; full INF5 частичен, ENG3/RTE/hardware backlog прежний.
Новый nonincremental build/golden/engine viewport/performance не запускались.
[Contract/matrix/commands/failures/evidence](alloy/HISTORY.md#inf6-явные-factories-и-совместимость--2026-10-19).

## UI INF4 async close — 2026-10-18

Owner-safe RequestCloseAsync/Closing: cancel/save/drain, concurrent join, native
requests и forced App Stop, observable errors и cleanup-before-success. Engine
viewport переведён на этот API. Full Debug1466/1466,9TRX/failed0/skipped0;
UI613/Hosting55 Debug/Release. Fresh7packages/2native consumers и10manual/engine
native processes Debug/published Release прошли. Следующий UI этап INF6, full INF5
частичен; ENG3/RTE/hardware backlog сохраняется. Новый nonincremental build/golden/
performance в INF4 не выполнялся. [Полные команды/матрица/evidence](alloy/HISTORY.md#inf4-асинхронное-закрытие--2026-10-18).

## UI INF3 frame scheduling — 2026-10-18

Добавлены Automatic/OnDemand/Continuous, coalesced RequestFrame/work wake,
monotonic cadence, bounded shared wait и cached native expose. Output freshness
отделён от pending queue/future requests через NeedsRender.
[Contract](alloy/PLAN.md#inf3inf6-условия-интеграции),
[commands/failures/provenance](alloy/HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18)
ведутся в UI документации. Следующий UI этап INF4; full INF5 частичен.

Final UI613/Hosting39 Debug/Release; повторный full Debug1450/1450,
9TRX/failed0/skipped0. Первый1449/1450 имел Mithril readiness sharing failure;
targeted20 и full94 retry прошли, race не исправлялась. Build0errors/1211warnings.
Fresh7packages/2independent consumers,10native processes с scheduling/legacy multi-
window/HUD/golden360/engine viewport прошли Debug apphost/published Release.
Fixture hashes неизменны, validation/cleanup прошли в своих scopes. Это не
CPU/GPU speedup, full Release solution/32ENG1 headless/full10package/40consumer/
native RTE matrix и не закрытие UI hardware blockers. Engine ENG3/RTE backlog
сохранены; Mithril failure и engine integration отдельно отражены в module statuses.

## Минимальный UI INF5 — 2026-10-18

После INF2 добавлена opt-in fixed-storage diagnostics для session/queue/phases и
host presentation. Единственный [контракт/остаток INF5](alloy/PLAN.md#inf3inf6-условия-интеграции)
и [команды/evidence/overhead](alloy/HISTORY.md#inf5-минимальная-диагностика--2026-10-18)
ведутся в UI документации. Общий PLAN/STATUS/QUALITY обновлены; engine STATUS
отражает instrumentation существующего viewport probe, без начала ENG3.

UI612/Hosting18 Debug/Release; full Debug1428/1428,9TRX/failed0/skipped0;
nonincremental Debug build0errors/1211warnings. Два bounded ENG2 viewport smoke
с diagnostics прошли Debug apphost/published Release. Семь fresh local packages
и independent headless consumer2cases проверили новое public API; это не прежняя
full10package/40consumer native matrix. BDN4cases измерили on/off overhead без
обещания UI/GPU speedup. Full INF5 частичен; далее INF3, engine ENG3/RTE/K3/K7/K8/VRAM
остаются в своих scopes. Full Release solution/32ENG1 headless/golden/native RTE/
Vulkan matrix в этом шаге не повторялись.

## UI INF2 dispatcher — 2026-10-18

После INF1 реализован bounded session dispatcher, observable awaitable dispatch
и latest-value PropertyBinding coalescing. Единственные подробные
[contract/acceptance](alloy/PLAN.md#inf2-queue-и-dispatch-contract) и
[команды/evidence](alloy/HISTORY.md#inf2-ограниченный-dispatcher--2026-10-18) находятся
в UI документации. Общий PLAN/STATUS/QUALITY обновлены без дублирования tasks.

UI596/596 и Hosting14/14 Debug/Release; full Debug solution1408/1408,
9TRX/failed0/skipped0. Nonincremental Debug build0errors/1211warnings.
Два bounded ENG2 viewport smoke Debug apphost/published Release прошли с actual
pixels и cleanup. Это не full Release solution/packages/golden/32headless native
ENG1 run и не UI hardware/performance acceptance. Следующий инфраструктурный UI
шаг — минимум INF5 перед INF3; engine ENG3 и отложенный RTE backlog сохраняются.

## ENG1 headless supervisor — 2026-10-17

После UI RichTextEditor и сохранения его отложенного плана реализован ENG1:
probe host/contracts/Game.Logic stub/tests, fresh owned child generations,
Hello/scene readiness, serialized commands, timeout/cancel/crash/disconnect/parent
exit и cleanup при живом Editor App/authoring fixture. Единственные подробные
[contracts](engine/PLAN.md#eng1-headless-supervisor--2026-10-17) и
[commands/failures/evidence](engine/HISTORY.md#eng1-headless-supervisor--2026-10-17)
находятся в engine документах; проекты в ManualTests/TrueMoon.slnx folder Engine.

Engine31/31 Debug/Release, native32/32 Debug apphost/published Release с20Start/Stop
в каждом configuration. Full Debug solution tests1305/1305,8TRX/failed0/skipped0;
nonincremental Debug build0errors/1207warnings. Release остальных suites и UI
native/golden/package не повторялись. Следующий code шаг — ENG2 CPU ring/viewport;
replacement ENG3, UI hardware acceptance и общий release ещё открыты.

## RichTextEditor перед ENG1 — 2026-10-17

По запросу пользователя перед ENG1 реализован UI RichTextEditor/options поверх
Topten.RichTextKit с neutral Argentis contracts и Skia adapter. Единственный
детальный [контракт PA7a](alloy/PLAN.md#pa7a-richtexteditor-перед-eng1--2026-10-17),
[API guide](alloy/RICH_TEXT_EDITOR.md), [failures/commands/evidence](alloy/HISTORY.md#richtexteditor--2026-10-17).
UI563/563 отдельно Debug/Release,6native demo cases,40fresh package processes и
прежний golden360 с validation teardown прошли. Общий nonincremental Debug build:
0errors/1200warnings; own headless suites не повторялись. Общие/UI status и quality
matrix синхронизированы; ENG1 остаётся следующим code шагом, engine code пока нет.
PA7/beta, UI alpha и аппаратные ограничения не закрыты.

## ENG0 scope и допуск прототипа — 2026-10-15

После UI package/Hosting quality пользователь одобрил ограниченный engine/editor
prototype с открытыми UI рисками. В engine PLAN определены scope/demo, platform,
session/control/frame v1, ownership/backpressure, checkpoint/replacement/rollback,
deadlines и будущие acceptance cases. [Единственный детальный контракт](engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15),
[история и проверка документации](engine/HISTORY.md#eng0-scope-и-контракты--2026-10-15).

ENG0 — проектирование; ENG1–ENG8 ещё запланированы. Следующий code шаг — headless
ENG1 supervisor/Hello/repeat/cancel/crash/cleanup. Shared CPU viewport и versioned
Game.Logic replacement относятся к ENG2/ENG3. Новых engine projects/runtime APIs,
build/tests/process/GPU/performance результатов нет; численные targets не измерены.

Общие PLAN/STATUS/QUALITY и UI continuation синхронизированы. K3/K7/K8/VRAM/final
alpha и остановка NVIDIA workaround search сохранены. Обычный child crash не
объявляется изоляцией shared GPU/driver fault; driver/Windows settings не менялись.
Предыдущие module quality passes сохраняют даты и не считаются engine evidence.

## UI quality после общей матрицы — 2026-10-15

Выполнен следующий scope из quality matrix: fresh UI package/Hosting proof после
bundled Cobalt и explicit fonts. Воспроизведён и исправлен missing resolver
HostedUiDispatcher (constructor registration→explicit factory). Existing consumer:
10fresh nupkgs,36App Cobalt/DI/DI-without-analyzer +4direct renderer processes,
Debug/Release ×build/publish, fonts/OFL/native/analyzer/generated provenance и
финальные fingerprints; expected CS0246 negative control отдельно.
[Точные commands/failures/evidence и ограничения](alloy/HISTORY.md#ui-packagehosting-quality--2026-10-15).

Отдельно current UI532/Core107/Cobalt179/generator91 в каждой Debug/Release:
8TRX/1818passed/0failed/0skipped. Read-only golden360/360, fixtures unchanged,
validation teardown passed. Общий solution build, Enerit/Mithril/performance,
physical DPI/real loss/VRAM/long K7/final alpha не повторялись. Пакеты не опубликованы,
driver/Windows settings и default/adapter/migration решение сохранены.

Общие PLAN/STATUS/QUALITY и UI PLAN/STATUS обновлены: package gap закрыт;
следующий выбор — доступный UI acceptance либо отдельно согласованный bounded
ENG0 с сохранением UI рисков. Engine/PA1–PA10 этим шагом не начинаются. Engine
STATUS обновлён только по dependency summary; собственных engine results нет.

[Общий статус](STATUS.md), [план](PLAN.md), [индекс документов](README.md), [формат ведения](PLANNING.md). Подробные implementation/test/native записи принадлежат HISTORY соответствующего модуля; эта запись не заменяет их результаты.

## Общий README и навигация — 2026-10-11

По запросу пользователя сверены общий README, source projects/ProjectReference graph, solution, SDK/package/test настройки, общие PLAN/STATUS/PLANNING и локальные планы/статусы Core/Cobalt/Enerit/Mithril/Alloy. Корневой README сохраняет английский язык и расширен: назначение,14существующих module/runtime/generator проектов в8областях, зависимости, build/test команды из src, headless и standalone UI quickstart, local NuGet consumer, examples, license, readiness и deployment ограничения. Старые UseSignals/UseSignalService/ListenSignalService отсутствуют в текущем source API; README теперь указывает Enerit UseInvocationService/ListenInvocationService/PipesServiceTransport и builder-level Microsoft DI selection. Существующий MithrilTest приведён ссылкой как composition example с открытым own-process proof, не как подтверждённый запуск.

Добавлен `docs/README.md` с общей/модульной навигацией и практическими guides. В PLAN текущий порядок сведён в раздел развития; дублирующие UI записи убраны из общей release criteria section. Согласованные G1–G6/R1–R6, локальные критерии и architecture/dependencies не изменены. PLANNING уточняет роль root README/index как навигации, не второго roadmap. Общий и UI STATUS отделяют текущую документационную сверку от предыдущих package/native/test runs.

Проверки:

- Два **точных** fenced C# snippets из нового root README извлечены в isolated projects с собственными build/CPM settings. Headless имеет PackageReference на TrueMoon/Extensions.DependencyInjection, UI дополнительно Hosting. Использованы существующий local feed и version `0.2.2.7-alpha.consumer.20261011065747061` предыдущего UI package run, SDK10.0.401/net10.0/Windows/Debug. Оба `dotnet build` завершились exit0/0warnings/0errors. Это compile proof public API examples, не отдельный новый headless consumer lifecycle/COB4 proof.
- Проверка текущих Markdown документов: **26documents/339local links/76heading links**, problems0; существование локальных file/directory links, heading anchors, balanced code fences, conflict markers/NUL. Исторические модульные HISTORY записи не проверялись как текущие документы, но anchors ссылок на них разрешались. Результаты и exact scope сохранены в `links.json`; добавленные после первого прохода статусы/общая история включены в финальную проверку. `git diff --check` exit0.
- Артефакты ignored `TestResults/Documentation/Readme20261011`: подготовленный README, extracted source/projects, build logs, local settings, link-check script/report и diff-check log. Корневой README записан в явно запрошенный пользовательский файл; другие файлы вне src не менялись.

Приложения из README, unit/full solution tests, pack/publish и native/GPU checks в этом шаге не запускались. Исторические результаты остаются датированными; alpha/NVIDIA K7 blocker и module readiness не переоценивались. Команда `dotnet test` в README сверена с xUnit2/VSTest/global.json; пустая Mithril discovery не объявляется успешным suite. Общий root README включается в некоторые nupkg через Directory.Build.props, но артефакты предыдущего package run не пересобирались ради документационной правки; UI Hosting/renderer имеют самостоятельные PACKAGE.md.


## Сводка до quality matrix — 2026-10-15

Ниже сохранён прежний общий STATUS целиком; это архив результатов, а не новый запуск.

<details>
<summary>Прежняя сводка и датированный журнал</summary>

# TrueMoon: общий статус

Последняя работа — **2026-10-15, Mithril MIT5 operational diagnostics**.
Структурированная process trace через собственный IEventsSource: run/generation/PID,
effective policy/budget/delays, launch/exit/restart/terminal и cleanup outcomes.
Mithril94/94 и Enerit126/126 в каждой Debug/Release:4current TRX/440passed,
Failed0/Skipped0.40restart +18shutdown apphost/published cases и fresh package108/108
прошли, Completed=true;96package cases проверяют payload через App.UseDiagnostics,
12abrupt-parent-exit cases не наблюдают sink убитого parent.8nupkgs/9projects,
negative missing-analyzer CS0234;205package source fingerprints совпали.
Full nonincremental Debug build0errors/1193warnings. Core/Cobalt/generator suites,
UI/native и performance не повторены. [Scope/ограничения](mithril/STATUS.md),
[команды/evidence](mithril/HISTORY.md#mit5-operational-diagnostics--2026-10-15).
MIT5 закрыт в bounded Windows x64/net10.0 same-build/cooperative scope. Далее
сводная quality matrix модулей и оставшийся UI acceptance перед выбором ENG0;
новые engine/editor проекты этим шагом не создаются.

Предыдущая работа — **2026-10-15, Enerit ENE5 measured pipes baseline**.
10typed payload/concurrency cases,10verify +30fresh client/server pairs,
90samples/571944RPC;104source/10binary/4generated fingerprints совпали, power plan
не менялся. [Числа/ограничения](enerit/BASELINE.md),
[команды/scope](enerit/HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Scalar/c1: median RTT P50183.9µs,4896calls/s,client2512B/call; c8:779.3µs,
9062calls/s,2214B/call. Межзапусковый spread сохранён, не SLA/speedup.
Runtime126/generator114 в каждой Debug/Release,4TRX/480passed/0failed/0skipped.
Full nonincremental Debug build0errors/1193warnings;10дополнительных CS8669 от
generated benchmark не подавлены. Runtime/generator API/logic не менялись;
fresh package108 и собственные Core/Cobalt/Mithril suites/UI/native не повторены.
ENE5 measurement часть закрыта в bounded same-build Windows x64/net10.0 scope;
package portion имеет отдельную прежнюю дату. Далее Mithril MIT5 diagnostics,
allocation/CPU profile Enerit — отдельный measured backlog, engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core headless quality proof**.
Согласованный Core quality проход завершён в bounded Windows x64/net10.0 scope:
configuration/bootstrap, diagnostics и App/JSON/реальный FileStream через текущие
sources и свежие независимые пакеты. [Scope/ограничения](core/STATUS.md),
[команды/результаты](core/HISTORY.md#сквозной-headless-proof--2026-10-15).
52source +80package +3README processes прошли Debug/Release build/publish;
130/127source fingerprints совпали, negative opt-out expected CS0246. Cobalt7
scenarios включает startup+Dispose failure; Microsoft DI6compatibility scenarios
сохраняют native semantics. Core107/107 в каждой Debug/Release, Failed0/Skipped0,
current2TRX/214passed. Full nonincremental Debug build0errors/1183warnings.
Production/runtime API не менялись; другие module suites/IPC/units/UI/native и
performance не повторены. Далее Enerit quality/ENE5, затем Mithril MIT5 diagnostics;
engine ждёт сводки остальных модулей, общий выпуск не закрыт.

Предыдущая работа — **2026-10-15, Core diagnostics quality**.
Исправлены удержание completed DiagnosticListener и старые registrations при
повторном UseDiagnostics. Subscription completion/late handle cleanup, filters,
callback Dispose и независимое освобождение нескольких Apps проверены.
[Контракт](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/scope](core/HISTORY.md#diagnostics-quality--2026-10-15).
Core107/107 в каждой Debug/Release, Failed0/Skipped0, current2TRX/214passed;
diagnostics18cases в каждой конфигурации входят в107. Full nonincremental Debug
solution build exit0/errors0/warnings1183. Windows x64/net10.0/SDK10.0.402.
Другие module suites/package/IPC/units/UI/native/performance не повторены.
Далее цельный headless proof и сводка Core перед Enerit; engine ещё не начинается.

Предыдущая работа — **2026-10-15, Core configuration/bootstrap quality**.
Исправлен GetOrCreate при маскирующем null в именованной секции; новый объект
остаётся доступен повторным чтениям и сохраняется в JSON при JSON source.
Проверены порядок configuration/registrations/factories на Cobalt/Microsoft DI,
validation failure, whole-refresh failure/recovery и JSON serialization/sharing
errors/retry. [Контракт и следующий scope](core/PLAN.md#проход-качества-до-engineeditor--2026-10-15),
[команды/границы](core/HISTORY.md#configuration-и-bootstrap-quality--2026-10-15).
Core97/97 в каждой Debug/Release, Failed0/Skipped0, current2TRX/194passed;
ConfigurationTests27/27 отдельно Debug. Full nonincremental Debug build
exit0/errors0/warnings1184. Windows x64/net10.0/SDK10.0.402. Другие module suites,
package/IPC/units/UI/native/performance не повторены. Core quality частично:
далее diagnostics, затем цельный headless proof; порядок модулей сохраняется.

Предыдущая работа — **2026-10-15, Cobalt COB7: provider comparison**.
Один unchanged App graph проверен на Cobalt/Microsoft DI:20functional scenarios,
20Dry/20paired default measurements и24fresh startup samples, Completed=true.
[Решение и числа](cobalt/COMPARISON.md), [scope/команды](cobalt/HISTORY.md#cob7-provider-comparison--2026-10-15).
Cobalt остаётся existing default; **массовая migration explicit DI consumers отложена**.
Регистрация одинакова, Cobalt даёт structured path/unique cleanup, MS дешевле в hot
resolution. Cobalt App lifecycle16.91us против19.32us MS; singleton52.10ns/32B против
14.91ns/0B.127source fingerprints/power plan совпали; Ryzen9 9900X/Windows11 x64/
SDK10.0.402/.NET10.0.13. General release не объявлен.
Cobalt179/generator91/Core81 Release:3TRX/351passed/0failed/0skipped. Full Release
build exit0/errors0/warnings1185. Fresh package64+3, identical-graph consumer
12processes/120scenarios иlibrary/app4/2diagnostic controls прошли. Runtime/Contracts/
generator/adapter logic и provider choices не менялись. Debug unit suites/IPC/units/
UI/native не повторены; UI migration по headless proof не выполняется.
COB6a–d/COB5/COB7 закрыты в bounded scope; далее Core quality.

Предыдущая работа — **2026-10-14, Cobalt COB6b: runtime errors и recovery**.
Cycle/closed type guard, structured missing/depth/stack errors, immutable dependency
path и original factory/constructor exceptions реализованы. Failed/null singleton
retry и completed dependency ownership проверены. [Контракт](cobalt/PLAN.md#cob6b-runtime-errors-и-recovery--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6b-runtime-errors-и-recovery--2026-10-14).
Cobalt84/generator62/Core81 в каждой Debug/Release:6TRX/454passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1 и2constructor negative
builds; opt-out expected CS0246. Full nonincremental Debug build exit0/errors0/
warnings1187. Source manifest127files/mismatches0; packages local/unpublished.
Windows x64/net10.0/SDK10.0.402. Other module suites, IPC/units, UI/native/performance
не повторены. Далее COB6c registration contracts; полный COB6 и migration открыты.

Предыдущая работа — **2026-10-14, Cobalt COB6a: конструкторы и diagnostics**.
Закреплены unique longest public constructor, closed/open nullable/default semantics
и COB001–COB006 на registration call; internal types/factories/required members
проверены. [Контракт](cobalt/PLAN.md#cob6a-конструкторы-и-diagnostics--2026-10-14),
[команды/артефакты/границы](cobalt/HISTORY.md#cob6a-конструкторы-и-diagnostics--2026-10-14).
Generator62/Cobalt37/Core81 в каждой Debug/Release:6TRX/360passed/0failed/0skipped.
Fresh main package64/64 +3README, library/app4/4 +legacy1/1,2expected negative
constructor builds; opt-out expected CS0246. Full nonincremental Debug build
exit0/errors0/warnings1191. Source manifest127files/mismatches0; packages local.
Windows x64/net10.0/SDK10.0.402. IPC/units, прочие suites, UI/native/performance
не повторены; далее COB6b runtime cycles/error paths, полный COB6 открыт.

Предыдущая работа — **2026-10-14, единый Cobalt package и принятый TrueMoon scope**.
Cobalt runtime/generator остаются двумя projects, поставляются одним nupkg;
analyzer delivery автоматический direct/transitive, opt-out EnableCobaltGenerator=false.
Microsoft DI ecosystem bridge/соглашения не являются целью; существующий adapter
сохраняется. [Контракт](cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14),
[команды/scope](cobalt/HISTORY.md#единый-пакет-и-truemoon-scope--2026-10-14).
Fresh headless64/64 +3README, library/app4/4 +legacy1/1, App/IPC/units108/108;
negative controls expected. Cobalt37/generator16/Core81 отдельно Debug/Release:
6TRX/268passed/0failed/0skipped. Full nonincremental Debug build exit0/errors0/
warnings1227. Packages local, не опубликованы; UI/native/performance и остальные
module suites не повторены. Далее COB6a constructor diagnostics/selection.

Предыдущая работа — **2026-10-14, начало качества Cobalt и Try helper generation**.
По решению пользователя до engine/editor идёт качество существующих модулей;
начинаем с Cobalt: COB6 → COB5 → COB7 и условная миграция Microsoft DI consumers.
[Критерии и порядок](PLAN.md#качество-модулей-до-engineeditor--2026-10-14),
[Cobalt contract](cobalt/PLAN.md#качество-cobalt-до-engineeditor--решение2026-10-14).
Исправлена генерация для TrySingleton/TryTransient helpers; generator16/16,
Cobalt37/37 и Core81/81 отдельно Debug/Release — **6TRX/268passed/0failed/0skipped**.
Windows x64/net10.0/SDK10.0.402. Full nonincremental Debug solution build exit0,
errors0/warnings1227. Package/IPC/Mithril/UI/native checks не повторены; прежние package fingerprints
не являются подтверждением изменённого generator. Далее COB6a constructor diagnostics.
[Команды и scope](cobalt/HISTORY.md#cobalt-quality-и-try-helpers--2026-10-14).

Предыдущая работа — **2026-10-14, MIT5/ENE5 independent package portion**.
9local packages и независимый PackageReference App/IPC/units consumer:
**108/108cases** Debug/Release ×build/publish, Cobalt/DI и DI без Cobalt analyzer;
Completed=true. Actual package provenance/version,compiler analyzer path/SHA и
runtime output проверены; omitted Enerit analyzer даёт ожидаемый CS0234.
Packages не опубликованы. Source fingerprints current/mismatches0.
Windows x64/net10.0/SDK10.0.402: Enerit126/generator114,Mithril89,Core81,
Cobalt37/generator6 в каждой Debug/Release configuration — **12TRX/906passed**,
Failed0/Skipped0. Full non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14),
[команды и границы](mithril/HISTORY.md#mit5ene5-independent-package-consumer--2026-10-14),
[runner](../ManualTests/UnitsPackageConsumer/README.md).
Это package portion; **ENE5 performance и MIT5 operational diagnostics открыты**.
На момент package portion следующий функциональный шаг — фиксированный IPC latency/throughput/allocations
baseline до оптимизаций, затем process diagnostics. Same-build/cooperative scope;
UI/native/MMF/other OS/RID/NativeAOT/stress/uncooperative shutdown не подтверждены.

Предыдущая работа — **2026-10-14, Enerit strict response/decode ownership**.
Generated responses потребляют payload полностью; explicit decode scope возвращает
nested byte rentals при failure, включая DTO dictionary keys и более поздние
arguments. Successful caller/service ownership и DTO prefix API сохраняются.
Empty ack, canonical bool0/1, strict error UTF-8 и invalid dictionary keys проверены.
Windows x64/net10.0/SDK10.0.402: **Enerit126/126, generator114/114, Mithril89/89
отдельно Debug/Release**, Failed0/Skipped0; current6TRX/658passed.
App/IPC/restart **36/36** Cobalt/DI и isolated pipes **12/12** Debug/published
Release повторены, Completed=true; fingerprints current/mismatches0.
Final non-incremental Debug solution build exit0/errors0/warnings1227.
[Контракт](enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14),
[команды и границы](enerit/HISTORY.md#strict-response-и-decode-ownership--2026-10-14).
Core/Cobalt suites, UI/native/independent packages/performance не повторены;
ранние результаты этого же дня принадлежат предыдущему MIT4 run ниже.
Same-build/cooperative scope, killed parent client cleanup не наблюдаем.
На момент decode hardening далее была **MIT5/ENE5 package composition**, затем отдельный
IPC baseline до оптимизаций; backpressure/schema/MMF/uncooperative shutdown открыты.

Предыдущая работа с кодом и проверками — **2026-10-14, MIT4/ENE4**.
Parent App/AddUnit child, generated typed pipes, same-client reconnect после
bounded OnCrash restart, budget exhaustion, pending Stop/rollback и child cleanup
при abrupt parent exit проверены на Cobalt и Microsoft DI.
Windows x64/net10.0/SDK10.0.402: **Enerit102/102, generator34/34, Mithril89/89,
Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release**, Failed0/Skipped0;
**App matrix36/36 и isolated pipes12/12**, Completed=true.
Final non-incremental Debug solution build exit0/errors0/warnings1226.
[Единый контракт](mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14),
[команды/границы](mithril/HISTORY.md#mit4ene4-app-и-generated-pipes--2026-10-14).
Production исправления Enerit: explicit typed factories/App-local instances,
scoped endpoints, transport Start/Stop rollback; Core test isolation исправлена.
Mithril/Cobalt runtime не изменялись. Same-build/cooperative/ProjectReference scope;
client cleanup killed parent не наблюдаем. UI/native/package/другие OS/stress не повторены.
На момент MIT4 следующим шагом были Enerit response validation/partial nested-buffer
cleanup, затем MIT5/ENE5 independent package composition.

Будущий engine/editor план2026-10-13 сохранён:
[архитектура и этапы](engine/PLAN.md), [статус](engine/STATUS.md).
ENG0–ENG8 запланированы, проектов/реализации и собственных code/process/GPU checks
пока нет. Новый MIT4/ENE4 proof не является реализацией frame transport/hot reload.

Предыдущая работа с кодом и проверками — **2026-10-12, Mithril MIT3**.
Bounded Never/Once/OnCrash/Always, fresh process/control endpoint, budget/delay,
terminal callback, cancel/drain monitor при Stop/Dispose. Windows x64/net10.0:
**Mithril89/89 Debug +89/89 Release**, restart manual40/40; fresh App Never28/28
Cobalt/DI и legacy16/16. Enerit runtime71/71 в обеих configurations.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12),
[команды/границы](mithril/HISTORY.md#mit3-bounded-restart--2026-10-12).
На момент MIT3 следующим шагом был MIT4/ENE4: App/AddUnit + generated Enerit IPC/restart.
External restart/package/другие OS/stress и остальные suites/UI не проверены.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT1/MIT2**.
App-local configurations, main hosting в текущем graph/PID, owned external,
controller rollback/error propagation и cancellable parent/control listener.
Windows x64/net10.0/SDK10.0.401, App lifetime/Never, Cobalt и Microsoft DI:
**Mithril65/65 Debug +65/65 Release**, **App manual28/28**, fresh legacy16/16,
Completed=true; Enerit runtime regression71/71 в каждой configuration.
Non-incremental Debug solution build exit0/errors0/warnings1220.
[Контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11),
[команды и ограничения](mithril/HISTORY.md#mit1mit2-app-composition--2026-10-11).
На момент этого run следующим шагом был MIT3, затем MIT4/ENE4 App+IPC.
Другие module suites/UI, Enerit generator/manual и package consumers не повторены.

Предыдущая работа с кодом и проверками — **2026-10-11, Enerit ENE1/ENE2 pipes baseline**.
Runtime **71/71**, generator **34/34** отдельно Debug/Release, Failed0/Skipped0;
own-process apphost matrix **12/12**, Completed=true. Same-build typed payload,
32concurrent, local cancellation/late response, remote errors, disconnect/reconnect,
cooperative shutdown и owned child cleanup проверены на Windows x64/net10.0.
Non-incremental Debug solution build exit0/errors0/warnings1242;
другие модульные suites/UI этим запуском не проверялись.
[Контракт и ограничения](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11),
[команды](enerit/HISTORY.md#ene1-ene2-pipes-baseline--2026-10-11).
App/AddUnit/controller/parent-exit в том pipes run не проверялись; restart/общий IPC и Enerit packages остаются открыты.

Предыдущая работа с кодом и проверками — **2026-10-11, Mithril MIT0 own-child baseline**.
Убрана очистка одинаково названных процессов; child start/stop/disposal теперь
освобождают owned Process и ждут exit после принудительного завершения.
**Mithril14/14 Debug и14/14 Release**, Failed0/Skipped0; **manual16/16**
Debug/published Release apphost, Completed=true на Windows x64/net10.0/SDK10.0.401.
Проверен `Never` child-handle scope, App/AddUnit/parent-crash/restart/IPC — открыты.
[Команды и границы](mithril/HISTORY.md#mit0-own-child-baseline--2026-10-11),
[runner](../ManualTests/MithrilLifecycleProbe/README.md).
Другие unit suites/full solution/UI этим запуском не проверялись.

Предыдущая работа с кодом и проверками — **2026-10-11, CORE5/COB4 headless package consumer**.
Проверены **64/64 matrix processes + 3/3 packaged README examples** на local NuGet
в Debug/Release build/publish: Cobalt, Microsoft DI и Microsoft DI без consumer
generator. Actual analyzer path/SHA256/generated sources и отрицательная сборка
без generator подтверждены; metadata и package README дополнены.
Регрессии: **Core81/81, Cobalt37/37, generator6/6**, failed0/skipped0.
[Воспроизведение](../ManualTests/CorePackageConsumer/README.md),
[команды и scope](core/HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).
Full solution, другие модули, UI/IPC/units этим запуском не проверялись.
На момент pipes baseline следующим шагом были Mithril MIT1/MIT2
contracts; Core/Cobalt — COB5 измерения.

Предыдущая UI работа **2026-10-11 — K2/K4 fixed-font golden**: font catalog,
30 reference PNG, UI532/532, golden360/360 Debug/published Release,
Vulkan validation0/0 и negative controls. [Метод](alloy/GOLDEN_TESTS.md),
[scope](alloy/HISTORY.md#k2k4-fixed-font-golden--2026-10-11).
K7/physical DPI/real loss/VRAM остаются открыты; UI проверки не повторены в CORE5/COB4.

Предыдущая документационная работа: **2026-10-11 — общий README и навигация документации**. Корневой README дополнен картой существующих модулей, source build/test командами, headless/UI quickstart, examples, local NuGet delivery и ограничениями. Старый Signals пример заменён текущими API; добавлен [индекс документации](README.md), актуальный порядок собран в PLAN без дублирования в release criteria. Проверены локальные ссылки/anchors и компиляция двух точных README C# snippets на ранее подготовленных локальных пакетах:2builds exit0/0warnings/0errors. Приложения, unit tests, полный solution и native проверки в этом документационном шаге не запускались. [Область сверки](HISTORY.md#общий-readme-и-навигация--2026-10-11).

Предыдущая code/package работа: **2026-10-11 — UI package/independent consumer5.4–5.5**. Исправлена transitive native Skia delivery, подготовлены локальные NuGet artifacts и packaged инструкции.12App/backend +4direct renderer smoke прошли Windows x64/net10.0/SDK10.0.401, Debug/Release ×build/publish, framework-dependent без RID;4consumer builds/4publishes exit0/0warnings/0errors. Это UI поставка на указанной конфигурации, не Core5/COB4 headless/analyzer proof и не готовность alpha. На тот момент следующий независимый UI шаг — fixed-font golden K2/K4; внешний NVIDIA K7 блокер сохраняется. Unit/full solution tests этим шагом не повторялись. [Результаты и ограничения](alloy/HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

Предыдущая диагностическая работа: **2026-10-10 — UI WSI stages/raw Win32 и driver screen DC**. [Общий план](PLAN.md), [правила ведения](PLANNING.md), [UI статус](alloy/STATUS.md), [команды, native stack и scope](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10). Non-incremental Debug UI consumer build exit0/errors0/warnings780, Release publish exit0; native stage/raw/repeat/Hosting controls повторены в Debug/published Release. Unit tests и full solution build этим diagnostic шагом не повторялись.

UI Hosting использует shared process owner loop; concurrent/cancellation/fault/shutdown checks повторно прошли Debug/published Release, validation0/0. GDI trend связан с первым vkCreateSwapchain на новом HWND/surface; raw repro и debugger фиксируют GetDC(NULL) внутри текущего NVIDIA ICD32.0.16.1742 и HDC без последующего ReleaseDC в bounded lifecycle. Raw24cycles NVIDIA GDI6→29, AMD5→5 в Debug/Release; full UI на AMD не проверен. Production ownership/GPU policy не изменены, safe app-side fix не установлен; G4/K7 остаются открыты. Предыдущий full240-window failed USER26→26/GDI23→137; текущий diagnostic его не повторяет.

Предыдущий dispatcher run: UI suite488/488, solution build0errors/1271warnings, Debug/Release functional/short evidence. [Область и ограничения](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10); его passing results не считаются текущими unit/solution checks.

Предыдущий общий Core/DI/Enerit run2026-10-10: **643passed/0failed/0skipped**, Mithril discovery0 отдельно. AppCreate failure устранён, contracts зафиксированы в модульных PLAN; NU1506/NU1507 устранены локальными package/source настройками. [Результаты и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10). Эти результаты остальных модулей не являются новым UI run.

## Состояние модулей

| Модуль | Реализация и область вывода | Проверки и продолжение |
| --- | --- | --- |
| [Core](core/STATUS.md) | Runner/lifecycle, configuration/diagnostics и package consumer | **2026-10-15, COB7 regression:** Core81/81 Release, App comparison на обоих providers. Далее собственный Core quality review |
| [Cobalt / DI](cobalt/STATUS.md) | Собственный API, bundled generator, contracts и paired App comparison | **2026-10-15, COB7:**20measurements/24cold/120package scenarios, Release179+91regressions. Migration отложена, default/adapter сохранены; hot-path backlog отдельно |
| [Enerit](enerit/STATUS.md) | Same-build generated payload contract, TME001/size limits, acknowledged errors/void, concurrent framing и local cancellation/reconnect | **2026-10-14, hardening:** Runtime126/126/generator114/114 Debug/Release, App36/36 и isolated12/12. Strict response/failed nested decode cleanup проверены; Package portion108case проверена; MMF/performance открыты |
| [Mithril](mithril/STATUS.md) | App-local units, owned processes, controller/parent lifecycle и bounded process restart | **2026-10-14:**89/89 Debug/Release regression; App/generated IPC/restart36/36 Cobalt/DI. MIT4 и MIT5 package portion108 проверены; operational diagnostics открыты |
| [Alloy / Argentis](alloy/STATUS.md) | Shared owner, native package delivery, independent consumer, explicit font catalog и30full golden fixtures. Alpha частична | **2026-10-11:** UI532/532, golden360/360 Debug/published Release, validation0/0. Package16processes — предыдущий отдельный run; K7 failed/NVIDIA external blocker, physical DPI/real loss/VRAM сохраняются |

Наличие source/test projects означает наличие реализации или сценариев, а не успешное прохождение. Подробности и датированные результаты принадлежат локальным STATUS/HISTORY.

Текущий COB7 run:3TRX/351passed/0failed/0skipped Release,20measurements/24cold,
package64+3/12comparison/4library processes/2controls, full Release build exit0.
Предыдущий COB5:3TRX/351passed,10cases/12cold. Предыдущий COB6d:6TRX/702passed,
package64+3+5/2controls, full Debug build exit0.
Предыдущий COB6c run:6TRX/644passed, package64+3+5/2controls.
Предыдущий COB6b run:6TRX/454passed, package64+3+5/2controls.
Предыдущий COB6a run:6TRX/360passed, package64+3+5 и2controls.
Предыдущий bundled run:6TRX/268passed, package64+3+5 и108.
Предыдущий Try helper run имеет отдельные6TRX/268passed.
Предыдущий package portion run:12TRX/906passed/0failed/0skipped, package108.
Предыдущий decode hardening run:6TRX/658passed, App36 и isolated12.
Предыдущий MIT4/ENE4 run того же дня:12TRX/698passed/0failed/0skipped.
MIT3 restart40/App28/legacy16, MIT0/MIT1/MIT2, headless package64+3, UI532/golden360
и UI package — отдельные предыдущие runs с собственными датами и scope.

## Общие этапы

| Этап | Состояние |
| --- | --- |
| G1. Исходное состояние | Solution baseline2026-10-10 сохранён отдельно; тогда Mithril tests0. Текущий Mithril89+89; final full solution build успешен,12current module TRX отдельно |
| G2. Core и DI | Известные defects исправлены; component и bounded independent package lifecycle на обоих providers проверены. Дополнительные configuration/utility/platform критерии сохраняют свой scope |
| G3. IPC и units | MIT0–MIT3 и ENE1/ENE2 подтверждены ранее; MIT4/ENE4 same-build App/generated pipes/restart проверен2026-10-14. Strict response/nested failure cleanup и bounded independent package delivery проверены; расширенные transport criteria открыты |
| G4. UI alpha | Частично; UI критерии и собственный следующий шаг — в Alloy плане |
| G5. Сквозные examples | Headless package64+3 исторически проверен; source App/typed IPC/restart36case теперь подтверждён. IPC/units package108 теперь проверен; UI и дополнительные deployment/performance criteria отдельно |
| G6. Общая поставка | Общие R1–R6 не закрыты; версия0.2.2.7-alpha не является readiness свидетельством |

## Общие проверки — датированные сведения

**UI WSI stages2026-10-10:** current non-incremental AlloyVulkanTest Debug consumer build exit0/errors0/warnings780, Release publish exit0. Stage/raw/repeat matrix Debug/published Release: queries без GDI trend, первый swapchain на новой surface/window воспроизводит его; raw teardown orders и thread-exit control проверены. Debug native stacks — screen GetDC(NULL) внутри NVIDIA ICD32.0.16.1742; safe app-side fix не установлен. Hosted dispatcher Debug/Release regression passed/validation0/0. Unit/full solution/full240-window/physical/package checks не повторялись. [Команды и ограничения](alloy/HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

**UI dispatcher2026-10-10:** Windows/Debug/net10.0/SDK10.0.401; non-incremental `TrueMoon.slnx` build exit0/errors0/warnings1271, UI tests488passed/failed0/skipped0. Release UI consumer publish exit0. Native concurrent/callback/chrome/controlled failure/short cleanup Debug и published Release прошли, validation0/0; два полных240-window прогона **failed** на прежнем GDI limit и предшествуют финальным lifecycle guards. Native24-cycle controls определяют WSI scope, не exact allocator и не VRAM. [Подробности и фактические команды](alloy/HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Другие модульные tests, physical DPI и package consumers не запускались.

**Итог исправлений Core/DI2026-10-10:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. `dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet`: exit0/0errors/1271warnings. Стандартный solution test с no-build/no-restore и отдельная list-tests discovery: exit0. Core81, Cobalt37, Cobalt.Generator6, Enerit30, Enerit.Generator9, Alloy480 — все Passed, Failed0/Skipped0. Mithril TRX total0 не считается passing suite. [Команды, requirement matrix, TRX и ограничения](core/HISTORY.md#исправление-проблем-ядра--2026-10-10); локальные детали — [Cobalt](cobalt/HISTORY.md), [Enerit](enerit/HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10). Release/pack/own-process/native UI не запускались.

**Предыдущий Core review2026-10-10:** TrueMoon.Tests **Passed3/Failed1/Skipped0**, discovered4; AppCreate падал на `ServiceResolvingException<IAppLifetime>` через чужой LifeTimeExecutor resolver. TrueMoon.Core incremental build exit0/errors0. [Команды, scope и выводы](core/HISTORY.md#ревью-ядра-и-core0-baseline--2026-10-10). Два passing RunAsync теста тогда содержали Assert.True(true); в текущем исправлении заменены behavioral assertions.

Предыдущая UI investigation: consumer Debug build/published Release, native WGL/thread240-cycle comparisons и shared-loop prototype с двумя live окнами/close/recreate. [Команды и ограничения](alloy/HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Production dispatcher и полный mixed resource result этим шагом не исправлены; unit suite/общий baseline/другие модули той UI работой не запускались заново.

Предыдущая lifecycle работа2026-10-10: UI suite **480passed/0failed/0skipped**, short/callback/retirement proofs и два полных240-window Debug/published Release прогона. Оба полных window resource results **failed** на USER growth при нулевых hooks/registry/subscriptions/retained UI roots. [Команды и ограничения](alloy/HISTORY.md#lifecycle-resize-soak-и-native-resource-growth--2026-10-10). Предыдущие TextBox/Name20repeats и backend comparison — [отдельный запуск](alloy/HISTORY.md#textbox-control-text-и-name-smoke--2026-10-10).

**Интеграция2026-10-10, main `d56f217`:** Windows/Debug/net10.0, SDK10.0.401, xUnit2.9.3/VSTest. Enerit runtime **10passed**, generator **9passed**, Alloy UI **469passed**, во всех трёх проектах Failed0/Skipped0. AlloyVulkanTest consumer build exit0, **0errors/227warnings**. Debug native callback probe exit0:6direct+12hosted failures+12healthy recreations, HWND/tree/registry/hooks cleanup, Vulkan validation0errors/0warnings. Команды и локальный scope — [Enerit история](enerit/HISTORY.md#интеграция-в-main--2026-10-10), [UI история](alloy/HISTORY.md#интеграция-в-main--2026-10-10). Артефакты — ignored `TestResults/AgentMerge` в основном checkout. Общий solution build/tests, Release publish и physical DPI matrix при интеграции не повторялись; их исторические результаты не являются новым запуском.

Из записи предыдущего UI code run **2026-10-09** сохранено: non-incremental TrueMoon.slnx build **0errors/1341warnings**, SDK10.0.401/net10.0; локальные Release publish AlloyTest/AlloyVulkanTest exit0. Подробные команды, configuration и native ограничения — [UI история](alloy/HISTORY.md#49d--k3-native-windows-dpi--реализация-и-запуск-2026-10-09).

Это предыдущий общий build и два UI consumers, не successful full solution test suite и не проверка package consumers остальных модулей. При реорганизации2026-10-09 и интеграции2026-10-10 эти команды не повторялись.

Из прежних записей **2026-09-27**: AppCreate failure и отсутствие найденных Mithril tests. AppCreate отдельно воспроизведён review2026-10-10, затем исправлен и проверен тем run; Mithril discovery0 подтверждён2026-10-10, текущая suite14+14 добавлена MIT0. Старые index/Enerite записи сохранены в архиве UI документа как исторический контекст, не являются текущим blocker roadmap.

## Общие блокеры и неопределённости

- ENE5 measurements и MIT5 operational diagnostics проверены2026-10-15 в отдельных bounded scopes; fresh package108 повторён для MIT5 с проверкой diagnostics. Это не общий platform/release acceptance и не новый performance run.
- G2 headless package/analyzer consumer подтверждён предыдущим CORE5/COB4 run. Cooperative shutdown не прерывает user Task, игнорирующий cancellation; Microsoft DI alias disposal отличается от Cobalt. Контракты/limitations закреплены в локальных PLAN.
- Политика поддержанных configuration/SDK/OS, warnings и package consumer matrix общего выпуска требует отдельной фиксации.
- UI physical DPI matrix ограничена доступным150% монитором. Это ограничение конкретной проверки UI, не препятствие работе над Core/DI/IPC/units.
- UI K7: предыдущий full check **failed** USER26→26/GDI23→137. Текущий native trace локализует screen HDC allocation/lifetime до первого swapchain в NVIDIA ICD32.0.16.1742; raw AMD control не воспроизводит GDI trend, но full UI на AMD не проверен. Safe app-side fix не установлен; supported NVIDIA fix или полностью проверенная alternate configuration, затем full matrix с прежними limits. Private/handles и VRAM остаются; G4 не закрыт.
- Предыдущие same-build App/process/IPC/restart и package proofs не подтверждают automatically other OS/RID, uncooperative shutdown или UI/engine workloads; scope остаётся в локальных STATUS/HISTORY.

## Следующий конкретный шаг

1. Свести bounded quality Core/Cobalt/Enerit/Mithril и оставшийся UI acceptance в общую матрицу перед выбором ENG0. MIT5 diagnostics/package проверены; остальные module/package/platform criteria сохраняют собственный scope.
2. Cobalt hot-path allocation investigation и Enerit scalar c1/c8 allocation/CPU profile — отдельные measured backlog задачи перед оптимизацией. Cobalt default/adapter и отложенная migration прежние. Engine/editor ждёт сводки рабочего уровня остальных модулей.
3. UI: fixed-font golden K2/K4 проверен2026-10-11 в Debug/published Release; следующий [локальный scope](alloy/STATUS.md#следующий-конкретный-шаг) — оставшаяся physical4.9/K3, real loss/K8/VRAM и итоговая matrix. Independent package consumer5.4–5.5 — предыдущий отдельный run, новые font overloads через NuGet ещё не проверены. По решению пользователя NVIDIA GDI defect — внешний K7 блокер; поиск app-side workaround пока прекращён. Возврат при supported NVIDIA fix или полностью проверенной alternate GPU configuration, затем full check с прежними limits; private/handles отдельно. Alpha acceptance сохраняется; пользовательские драйвер/Windows settings без соответствующего запроса не менять.

## Изменения формата — 2026-10-09

Созданы общий PLAN/STATUS, правила и четыре модульных PLAN/STATUS. UI PLAN явно ограничен Alloy/Argentis и интеграциями; его цели/архитектура/идентификаторы/критерии сохраняются. Текущий UI STATUS сокращён; полный прежний документ сохранён в HISTORY. AGENTS указывает общую и локальные точки входа.

Проверки текущей правки прошли: чтение исходников/ProjectReference;17planning/AGENTS документов,270local links/21anchors, Markdown fences/whitespace/conflicts/NUL; полный прежний UI STATUS сохранён verbatim, K1–K9 table rows идентичны, PA1–PA10/net10.0/xUnit сохранены, пять модульных PLAN/STATUS пар присутствуют. Scoped git diff --check прошёл, source worktree status при финальной сверке не изменился, staged entries0. Артефакты документальной проверки — ignored TestResults/PlanningFormat. Новых passing build/tests/GPU/publish результатов эта запись не создаёт.

</details>

## Матрица качества модулей — 2026-10-15

По запросу следующего шага после MIT5 сведены общие PLAN/STATUS/PLANNING,
локальные Core/Cobalt/Enerit/Mithril/Alloy PLAN/STATUS и будущее engine направление.
[Результат](QUALITY.md): bounded проход первых четырёх модулей завершён в собственных
scopes; UI alpha остаётся частичной. Сравнение не складывает разные предыдущие runs
в current full-solution pass. Даты собственных suites, отдельных package и perf
runs сохранены; в частности COB7 own runtime/generator179/91 — Release,
Core107 Debug/Release — headless proof, Enerit114generator — ENE5 baseline,
Mithril94/Enerit126 и package108 — более поздний MIT5 run того же дня.

Исходная сверка AlloyPackageConsumer Program/csproj/runner и Hosting AlloyOptions
подтвердила пробел acceptance: consumer выбирает только UseDI и overloads без
explicit SkiaFontCatalog, runner не сравнивает source SHA в конце. Новый runtime
failure не воспроизводился. Следующий code scope уточнён только в
[плане владельца UI](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15):
fresh independent package/Hosting proof на Cobalt/DI, bundled analyzer, fonts/native
provenance, direct renderer и bounded lifecycle. Это дополнение existing consumer,
без массовой migration других UseDI apps и без нового engine проекта.

NVIDIA K7 и решение пользователя остановить workaround search, physical DPI,
real loss/VRAM/final UI matrix сохранены. Scope quality работы не требует закрыть
все будущие PA1–PA10/DI scopes/MMF/Hot Reload пункты; выбор ограниченного ENG0
прототипа требует явного рассмотрения оставшегося UI риска. Game scenario/API
сверка не выбрала за пользователя. Cobalt/Enerit measured allocation backlog
сохраняется отдельно от функционального порядка работы.

Общий STATUS сокращён до актуальной сводки; полный прежний текст сохранён в
[архиве](#сводка-до-quality-matrix--2026-10-15). Local STATUS/навигация синхронизированы:
убраны устаревшие следующие шаги Core/Enerit/Mithril. В Cobalt PLAN исправлена
старая фраза two-package на действующий один consumer package. Existing module
contracts, общие G1–G6/R1–R6, UI K1–K9/PA1–PA10 и engine ENG0–ENG8 не отменены.

Code/build/tests/pack/publish/process/GPU/performance в этом документационном шаге
не запускались. Артефакты сверки — ignored `TestResults/QualityReview/run-20261015-135635200`:
STATUS.before.md, runtime-sources.before.json и последующий отчёт проверки документов.
Изменений runtime/project files, системных настроек, commits/push нет.

Фактическая проверка документов: `VerifyReview.ps1`/`review.json` в указанном artifact
directory —29Markdown documents/623local links/202heading anchors,0problems;
balanced fences/conflict markers/NUL проверены. Прежний STATUS из347строк сохранён
verbatim в архиве, новая текущая сводка84строки.299runtime/project files совпали
по SHA256 с начальным snapshot; K1–K9 и PA1–PA10 rows UI PLAN/STATUS не изменены
относительно исходного tracked UI baseline. Scoped git diff --check passed.
Это documentation validation, не новый module regression/build/native pass.

## ENG2 CPU viewport — 2026-10-17

[Engine scope/layout](engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17) и
[evidence](engine/HISTORY.md#eng2-cpu-frame-viewport--2026-10-17):3slot CPU ring,
bounded receiver и одно Alloy/OpenGL окно показывают actual child frames.
Owned Skia BGRA image factory отдельно отражена в UI STATUS/HISTORY.
Native34/34 Debug apphost/published Release, по20Start/Stop, resized pixels,
EOF/deadline/native close/reap; engine87/UI570 Debug/Release, full Debug1368/1368.
Nonincremental Debug0errors/1211warnings. Source323/binary76fingerprints
и final staged/links/diff guard — TestResults/EngineSessions/eng2-completion.json.
Следующий этап ENG3 compiler/checkpoint/versioned replacement/rollback;20Replace
не выполнялись. UI hardware blockers и отложенный RTE backlog не менялись;
новых packages, shared GPU или performance baseline нет. Full Release solution
suite и UI package/golden/RichTextEditor native matrix не запускались.

## UI INF0/INF1 registry cleanup — 2026-10-18

После сохранения дополнительного INF roadmap пользователь выбрал рекомендуемый
UI шаг INF0→INF1. Исправлен repeat cleanup HostedUiSessionFactory после Update/
borrowed/owner/tree guard failure, Creation fence и causes preservation сохранены.
[Подробный contract/evidence](alloy/HISTORY.md#inf0inf1-повторяемый-cleanup-registry--2026-10-18).
Новый TrueMoon.Alloy.Hosting.Tests с production ProjectReference добавлен в
TrueMoon.slnx, source-linked UI tests не заменялись.14/14 Debug/Release;
full Debug solution1382/1382,9TRX/failed0/skipped0, nonincremental Debug build
0errors/1211warnings. Два bounded ENG2 OpenGL viewport runs Debug apphost/published
Release прошли: по25child launches/20Start-Stop, sessions0/mappings0/child cleanup.
Это не повтор32ENG1 headless cases, full Release solution, package/golden/RTE native
matrix, performance/VRAM или hardware acceptance. Следующий UI этап INF2,
engine продолжение ENG3 и отложенный RTE backlog сохраняются.
Новых пакетов/ABI/production dependencies нет, commit/push не выполнялись.

## Сводки до UI PA8a — 2026-10-22

Последняя работа — **2026-10-22, Aether ENG5c.2 reach/raycast/interaction**:
nearest affine geometry query, opaque occlusion и independent object switches,
TMCK2 atomic state restore реализованы. Control3/Frame1/Snapshot2 требуют rebuild;
full World/edit не заявлен.52новых cases, full Debug/Release2186/configuration,
9TRX/failed0/skipped0; Aether780/UI615/Hosting79/Mithril95.
Fresh native60/60cases/62commands Debug/published Release: interaction18PNG,
прежние CPU/GPU/input/Editor/Replace/failures; core/sync0/0/zero ownership.
Diagnostic JSON compatibility regression исправлена, полная матрица повторена.
Local Aether pack/DLL SHA совпал с final managed/native proof; package drawing
не запускался. Completion report Passed=true.
[Контракт/evidence](engine/HISTORY.md#eng5c2-reachraycast-и-interaction--2026-10-22).
Следующий шаг — [ENG5c.3 collisions камеры](engine/PLAN.md#eng5c3-следующий-шаг-collisions-камеры),
затем HUD; MCP/full snapshot/edit/raw input/3D performance/hardware gates открыты.

Предыдущая работа — **2026-10-22, Aether ENG5c.1 scene consumers/multiple primitives**:
standard v1 component roles, parent world transforms и статичная authoring-комната
подключены к отдельному runtime/многопримитивному Vulkan renderer через IPC.
Control v3 требует совместного rebuild, frame1/TMCK1 прежние; full world restore
не заявлен. 36новых cases; full Debug/Release2134/2134/configuration,
9TRX/failed0/skipped0; Aether728/UI615/Hosting79/Mithril95.
Fresh native58/58cases Debug/published Release, room по5PNG и прежние CPU/GPU/
input/Replace/failures; core/sync0/0, zero owned resources/imported readback.
Local Aether pack/DLL payload hash прошли; package drawing consumer не запускался.
[Контракт/evidence](engine/HISTORY.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22).
На этапе ENG5c.1 следующий шаг — [ENG5c.2 reach/raycast/interaction](engine/PLAN.md#eng5c2-reachraycast-и-interaction--2026-10-22),
затем collisions/HUD. Full runtime snapshot/edit, MCP,3D performance и прежние
hardware/release блокеры остаются открытыми.

Предыдущая работа — **2026-10-22, Aether ARCH1a.2 scene aggregate и adapter**:
immutable SceneDocument/ResolvedSceneSnapshot, parent graph, version metadata и
explicit projection в прежний SceneSnapshot реализованы;52новых cases, scene suite130.
Full Debug/Release2098/2098/configuration,9TRX/failed0/skipped0; Aether692/UI615/Hosting79/Mithril95.
[Контракт/evidence/границы](engine/HISTORY.md#arch1a2-агрегат-сцены-и-адаптер--2026-10-22).
Прежние control/frame/TMCK schemas и интерактивный sample не изменены;
native/GPU/package checks на этапе ARCH1a.2 не повторялись. Тогда следующим был [ENG5c.1](engine/PLAN.md#eng5c1-scene-consumers-и-несколько-примитивов--2026-10-22):
component roles, runtime scene consumer и несколько примитивов. Общий tooling API,
full runtime snapshot/edit и MCP запланированы; прежние release блокеры сохранены.

Предыдущая работа — **2026-10-22, ARCH1a.1**: immutable model/EntityResolver,
78новых cases; full Debug/Release2046/2046/configuration, Aether640.
[Scope/evidence](engine/HISTORY.md#arch1a1-immutable-модель-и-resolver--2026-10-22).

Предыдущая работа над реализацией — **2026-10-21, ENG5b input/camera**: control wire v2,
optional fixed-tick gameplay input, camera/checkpoint/Replace и интерактивный
GPU sample `--scene`. UI keys/mapping дополнены. Full Debug/Release1968/1968
в каждой configuration,9TRX/failed0/skipped0; Aether562/UI615/Hosting79/Mithril95.
Полная native regression56/56 и отдельный усиленный input proof2/2 по15cases/5PNG,
validation0/0/zero ownership. Три local packs/payload совпали с published DLLs;
отдельный package drawing не запускался. [Scope/commands/provenance](engine/HISTORY.md#eng5b-gameplay-input-и-камера--2026-10-21).
На момент ENG5b далее планировался ENG5c. Raw cursor/physical input/3D performance
и прежние UI hardware/release ограничения остаются открытыми.

Предыдущая работа — **2026-10-21, ENG5a Vulkan3D primitive renderer**:
immutable scene/camera/material, depth-tested cube в App-owned GPU viewport,
без CPU pixels/staging. Debug/published Release: по5PNG assertions, resize/
suspend/resume/checkpoint Replace, validation0/0 и zero owned resources после
teardown. Full solution Debug1926/1926 иRelease1926/1926, по9TRX/failed0/skipped0;
Aether520/UI615/Hosting79/Mithril95. [Контракт, commands и scope](engine/HISTORY.md#eng5a-первый-vulkan-3d-frame--2026-10-21).
Native matrix12/12cases/14commands,430source/76binary hashes; local Aether pack
и payload SHA256 check прошли, отдельный package drawing consumer не запускался.
Далее ENG5b input/camera; весь ENG5 и3D performance ещё открыты.

Предыдущая работа — **2026-10-21, ENG4e и Mithril graceful-stop fix**:
CPU/GPU baseline12cases/36intervals/24cooperative children,421source/94binary
fingerprints; raw captures и frozen budgets сохранены. Release1280×720 median
per-run p95: CPU65–66ms/GPU21–33ms; reference GPU allocation~107MiB/s.
Debug CPU имеет2FPS/6latency advisory breaches, budgets не ослаблены.
Исправлена гонка error delivery/remaining graceful wait; red/green и native20
Mithril shutdown cases прошли. Отдельный GPU validation run6cases прошёл.
Свежие units packages:108/108cases/8nupkgs; expected analyzer negative control.
Full solution Debug1907/1907 иRelease1907/1907, failed0/skipped0, по9TRX.
[Метрики, команды, failures и ограничения](engine/HISTORY.md#eng4e-cpugpu-baseline--2026-10-21).
Следующий функциональный шаг — ENG5; UI PA8a/RTE и прежние hardware/release
ограничения сохраняются. Reference timings не доказывают будущий3D performance.

Предыдущая работа — **2026-10-21, исправление структуры проектов**:44проекта/
597C#files, namespace/category violations0. Типы сгруппированы по назначению,
публичные namespace и consumers обновлены; общий Contracts остаётся независимым.
Full Debug/Release1898/1898 в каждой конфигурации, failed0/skipped0; build0errors/
1499warnings. Fresh Core80+3README/units108/UI16source+8package и36+4smoke/Aether4
App consumers прошли. Итоговая targeted native engine matrix20cases прошла;
выявленный lookup исторических leases по повторно использованному PID исправлен
на прямую ссылку текущего owned lease. [Миграция, команды, повторы и ограничения](HISTORY.md#исправление-структуры-проектов--2026-10-21).
На момент этой работы следующим был ENG4e; прежние UI hardware/release ограничения
сохраняются. Native probe теперь сохраняет исходные причины до assertion.

Предыдущая работа над реализацией — **2026-10-20, ENG4d2 ordinary GPU GameEditor**. Общий
transport/presentation contract подключён к Start/selected DLL/Build/Replace/
checkpoint/rollback; explicit GPU сохраняет обычный View/commands, CPU default.
Aether493/493 Debug/Release; UI615/615 Debug/Release; full Debug1898/1898,9TRX/
failed0/skipped0. Build0errors/1499warnings; fresh12packages/4App consumers.
Native54cases/56commands,406source/76binary fingerprints; per GPU Editor20Replace/20failures/53children, validation0/0, imported readback0.
[Команды/границы](engine/HISTORY.md#eng4d2-ordinary-gpu-gameeditor--2026-10-20).
Далее ENG4e measured CPU/GPU baseline перед 3D. K3/K7/K8/VRAM/final alpha
сохраняются; drivers/Windows settings/K7 stop policy прежние.

## Сводка PA8a перед ENG7a — 2026-10-22

Последняя работа — **2026-10-22, UI PA8a дефолтный квадратный стиль** по запросу
пользователя: Dark/Light controls, focus/error/read-only/disabled states,
scrollbar и стандартное borderless оформление через App/Hosting реализованы.
[API и галерея](alloy/DEFAULT_STYLE.md), [результаты/ограничения](alloy/STATUS.md),
[подробный журнал](alloy/HISTORY.md#pa8a-дефолтный-квадратный-стиль--2026-10-22).

Текущий full solution run: **2214/2214 Debug и2214/2214 Release**,9TRX в каждой
конфигурации, failed/skipped0; UI643/Hosting79/Aether780. UI golden360, новая рамка
8native cases, source integration16 и fresh packages36App+4direct прошли.
Это UI проверка; прежний engine native60/60 не повторялся. PA8a0–2 и automatic
PA8a3 выполнены, physical DPI/OS input/K3/K7/K8/VRAM/final alpha остаются открытыми.
Engine точка продолжения — ENG5c.3 collisions камеры, затем HUD. Предыдущие сводки
[сохранены в HISTORY](HISTORY.md#сводки-до-ui-pa8a--2026-10-22).
