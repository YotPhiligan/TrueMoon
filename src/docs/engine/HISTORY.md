# Engine / Editor: история решений и проверок

[Текущий статус](STATUS.md), [единственный детальный план](PLAN.md).

## App integration: реализация — 2026-10-19

По запросу «давай реализуем» после аудита добавлены публичные UseAether и
AetherOptions; AetherHost предоставляет App-owned Supervisor/Frames. Options
валидируются и копируются при configuration, report path становится абсолютным.
RuntimeFactory получает App resolver и создаёт свежий provider-owned instance;
конфликт с отдельной регистрацией factory/повторный UseAether отклоняется до
создания factory. Aether использует существующие TrueMoon.Contracts, без новой
зависимости на concrete bootstrap, UI или Game.Logic.

Non-disposable lifetime aliases участвуют в IStartable/IStoppable; единственный
disposable host владеет sessions/frames. App startup готовит host без запуска игры.
App Stop закрывает admission до drain, завершает readers и reap child; sync/async
provider disposal и startup rollback используют тот же cleanup. Concurrent calls
разделяют completion; ошибка наблюдаема, явный Dispose retry допустим. Даже при
ошибке frame cleanup выполняется попытка supervisor cleanup. Borrowed Supervisor/
Frames не зарегистрированы отдельными DI aliases; factory освобождается provider
после host. App Stop terminal, повторные игровые Play/Stop используют session API.

Headless/viewport examples используют UseAether; ручной SupervisorShutdown удалён.
Viewport сохраняет UI close/drain порядок. NativeRuntimeFactory требует child-role
bootstrap и matching Game.Logic DLL в executable consumer; UseAether не создаёт
entry point. Новый native app-stop начинает реальную сессию, останавливает App,
проверяет reap/release и отказ новой generation.

По требованию «вот такие модули должны подключатся через App» правило закреплено
в [AGENTS.md](../../AGENTS.md#подключение-модулей-к-app) и
[общем плане](../PLAN.md#подключение-функциональных-модулей-через-app--2026-10-19).
Utilities/contracts без собственного lifecycle не требуют фиктивной регистрации.

### Требования и integration tests

Новая AppIntegrationTests:11methods/28cases, оба provider Cobalt/Microsoft DI,
существующие87cases сохранены. Прямые ProjectReferences тестов на App и DI adapter
добавлены без executable references; framework xUnit2.9.3/VSTest сохранён.

| Требование / поведение | Точное имя теста |
| --- | --- |
| «давай реализуем»: options snapshot, explicit start, App stop и однократное ownership | Registration_SnapshotsOptions_AndAppStopOwnsExplicitSession |
| Disposal без App.Start, sync/async | DisposeWithoutAppStart_ReapsResolvedHost_ExactlyOnce |
| Failed startup rollback | FailedStartup_RollsBackPreviouslyStartedAetherLifetime |
| Изоляция двух App | IndependentApps_HaveIndependentHostsAndShutdown |
| Повторная registration/factory conflict | DuplicateRegistrationOrFactoryConflict_FailsBeforeFactoryCreation |
| Concurrent shutdown и fence новой generation | ConcurrentShutdown_JoinsCleanup_AndRejectsNewGeneration |
| Наблюдаемый cleanup failure/retry | FailedCleanup_IsObservable_AndCanBeRetriedBeforeProviderDisposal |
| Реальные CPU MMF/receiver/producer drain | AppStop_DrainsFrameWorkersAndMappings |
| Invalid options во время configuration | InvalidOptions_AreRejectedDuringConfiguration |
| Null context/configure | NullArguments_AreRejectedBeforeConfiguration |
| Null factory result при startup | NullFactoryResult_IsAnObservableStartupFailure |

IDE findTests недоступен (Tool not found); использован существующий явно связанный
TrueMoon.Aether.Tests. Тестовые controlled leases проверяют exact disposal counts,
ошибки и ordering; MMF case использует настоящий producer/receiver без UI/GPU.

### Фактически выполненные проверки

Windows x64/net10.0, SDK10.0.402/runtime10.0.13. Финальный source set:
`TestResults/AetherApp/runtime-clean/summary.json`, Completed=true,4commands exit0.

```powershell
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet -m
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger 'trx;LogFileName=results.trx' --results-directory TestResults/AetherApp/runtime-clean/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherApp/runtime-clean/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-app-clean-20261019 -IncludeViewport
```

Nonincremental build:0errors/1507warnings; новых integration test warnings нет.
Aether115/115 Release и115/115 Debug в full solution. Full Debug1518/1518,9TRX,
failed0/skipped0: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI613, Hosting79, Aether115.

Native summary Completed=true:36/36, по17headless +1viewport на Debug apphost и
published framework-dependent Release без RID. Каждый viewport:14cases/25child
launches/20StartStop, actual GL pixels, native-close-before-UI-disposal,
mappings/sessions0/childrenreleased, Game.Logic не загружена в Editor.
349source/76binary fingerprints проверены после запуска.

Отдельный package slice: `TestResults/AetherApp/final/summary.json`, version
`0.2.2.7-alpha.aetherapp.20261019154515219`.9fresh packages: Contracts/Core/Cobalt/
TrueMoon/Extensions.DependencyInjection/Enerit.Generator/Enerit/Mithril/Aether.
Все9pack + consumer build/publish +4consumer commands exit0. Independent consumer
имеет только PackageReferences, отдельные build props/targets/CPM/global.json/
NuGet.Config/cache; consumer graph из8TrueMoon packages точной версии (Enerit
generator — private build dependency, отдельно упакован), metadata source
свежий feed, project dependencies отсутствуют. Cobalt analyzer отключён. Debug
apphost/published Release ×Cobalt/DI проверили явные Start/Step, App.Stop cleanup,
factory/lease disposal1 и поздний start rejection; loaded DLL hashes совпали с
`lib/net10.0/TrueMoon.Aether.dll` свежего nupkg. Все4processes Passed=true.

Combined package manifest честно сохраняет Completed=false: после успешного
package slice первый full build выявил missing using TrueMoon.Dependencies в
ViewportProbe. Исправлен example import, затем весь runtime scope повторён отдельно.
Ранее initial test compile выявил missing TrueMoon.Services import в AetherOptions;
он исправлен до package proof. Последние test nullable warnings устранены явными
Assert.IsType для Resolve results. Промежуточные logs сохранены; package library
code после pack не менялся. `TestResults/AetherApp/completion.json` проверяет
финальные TRX/native/source/binary/package graph/hashes/links/namespaces отдельно
от неуспешного раннего combined manifest.

Финальный verifier Passed=true:15Markdown files/560local links/234anchors,
18C# files с namespace по реальному пути, diff whitespace/staged guard прошли.
Все6собственных фоновых helpers завершились; staged entries0, commit/push в этом
шаге не выполнялись. Предшествующие uncommitted extraction/metadata изменения
сохранены. Финальные native fingerprints относятся к исходникам после nullable fix.

### Границы и продолжение

Aether наследует IsPackable=true после снятия пользователем прежнего ограничения;
его локальная package delivery теперь проверена. Публичной публикации не было.
API экспериментальный, default native bootstrap application-specific. ENG3a
compiler/artifact остаётся следующим шагом; compiler/checkpoint/replacement/3D
не реализованы. Core/UI production code не менялся. Own Release UI/golden/RTE,
полный Release solution, performance/long resource/hardware matrix не повторялись.
K3/K7/K8/VRAM/final alpha открыты; driver/Windows settings не менялись.

## Проверка App integration — 2026-10-19

По запросу пользователя проверено наличие встроенной точки подключения Aether
к TrueMoon.App. В9handwritten C# sources модуля отсутствуют App configuration
extension, UseAether, IStartable/IStoppable adapters и ссылки на App.Builder.
Это статическая проверка исходников/project graph; новых build/unit/native/
package/performance запусков нет, implementation не выполнялась.

- [SessionSupervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) —
  IAsyncDisposable с явными Start/Stop/control operations; не регистрирует себя
  в App lifecycle. [FrameSession](../../TrueMoon.Aether/Frames/FrameSession.cs)
  аналогично требует отдельного consumer ownership/Stop/drain.
- [Headless example](../../ManualTests/EngineSessionProbe/Program.cs) создаёт
  supervisor вручную, регистрирует private SupervisorShutdown как IStoppable и
  хранит authoring fixture в App. Это локальная composition, не API Aether package.
- [Viewport example](../../ManualTests/EngineSessionProbe/ViewportProbe.cs)
  отдельно создаёт supervisor/FrameSession, подключает Alloy через UsePresentation
  и вручную drains engine tasks/frames при Closing до UI disposal.
- [Child RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs)
  самостоятельно строит App, parent monitor, game/protocol/producer и Enerit server.
  Он не является общим extension подключения Aether к App.
- [IAppConfigurationContext](../../TrueMoon.Contracts/IAppConfigurationContext.cs)
  уже доступен через существующий TrueMoon.Contracts ProjectReference Aether.
  Для module extension зависимость на concrete TrueMoon bootstrap не требуется.
  Существующие примеры аналогичного registration pattern —
  [Mithril AddUnit](../../TrueMoon.Mithril/AppCreationContextExtensions.cs) и
  [Enerit UseInvocationService](../../TrueMoon.Enerit/AppCreationContextExtensions.cs).

Рекомендация: отдельный UseAether registration contract на IAppConfigurationContext,
options/factories и явный lifecycle adapter с определённым ownership/stop/drain/
dispose. Запуск игровой сессии оставить явной операцией; UI выбирает consumer.
Consumer должен получать зарегистрированный supervisor через resolver. При
реализации проверить App Stop/Dispose/startup failure, отсутствие живых owned
children/readers и repeated configuration/DI ownership. Это предложение, публичный
API/реализация в данном audit не добавлялись; ENG3a план не подменён.

Сохранены параллельные пользовательские изменения: namespaces уже соответствуют
подпапкам, в Aether csproj сняты IsPackable=false/GenerateDocumentationFile=false.
Current package eligibility наследуется true из общих props; исторические
metadata/native test fingerprints из предыдущих runs этим audit не обновлялись.
Изменены только engine STATUS/HISTORY/PLAN. Docs links/anchors/whitespace проверены
отдельно; пользовательские code/project изменения не переписывались.

## TrueMoon.Aether — 2026-10-19

По запросу пользователя выделен единый engine module с принятым именем
**TrueMoon.Aether**. Одна library assembly объединяет прежние Engine.Probe.Contracts
и реализацию sessions/protocol/CPU frame transport/native child bridge из
EngineSessionProbe. Namespace implementation — TrueMoon.Aether, contracts —
TrueMoon.Aether.Contracts. Файлы физически перемещены, Compile-linked copies нет.
Framework net10.0, API экспериментальный, IsPackable=false.
[Принятые границы](PLAN.md#truemoonaether-одна-engine-assembly--2026-10-19),
[README](../../TrueMoon.Aether/README.md).

Aether references только TrueMoon.Contracts/Enerit/Mithril и Enerit generator;
нет dependencies на Game.Logic/Alloy/Skia/Silk. Contracts остаются DTO/domain-only
на уровне типов, но теперь находятся в одной assembly с implementation. Editor и
Game.Runtime по-прежнему разные процессы одного example executable; App startup,
конкретная game factory, FaultControl/Records и Alloy viewport остаются example
composition. Game.Logic — отдельная DLL, иначе нарушилось бы требование ENG3
обновлять игровую часть независимо от host. Новых production common-module APIs нет.

TrueMoon.Aether.Tests физически заменил прежний EngineSessionProbe.Tests;
существующие3test files/87cases сохранены с новым namespace и generated adapter
namespace. Direct ProjectReference на Aether и Game.Logic, без executable reference.
Единственный Probe.WaitAsync вызов заменён локальным helper с тем же bounded polling;
assertions не переписывались. Пакеты/xUnit/SDK/TFM остались repository conventions.
IDE findTests недоступен; тесты найдены в существующем явном test project.

NativeRuntimeFactory bootstrap key больше не зависит от executable RuntimeHost;
BuildIdentity.HostId использует entry host +Aether/Enerit/Mithril binaries.
Это сохраняет same-build handshake после переноса contracts в module assembly.
GameBuildId пока вычисляется из AppContext.BaseDirectory/Game.Logic.dll; выбор
versioned artifact и checkpoint/replacement остаются ENG3a–d.

TrueMoon.slnx теперь содержит Aether/Aether.Tests в folder Aether, example/game —
в Engine; старые Contracts/test project entries удалены. Exact `dotnet sln
TrueMoon.slnx list` подтвердил обе новые регистрации. RunProbe source roots включают
Aether/Aether.Tests; Debug/Release publication доставляет новую assembly.
README/общая карта/active status обновлены; link targets на перемещённые sources
исправлены. Исторические команды/результаты старых namespaces ниже сохранены.

### Фактически выполненные проверки

Windows x64, SDK10.0.402/runtime10.0.13, VSTest/xUnit2.9.3. Exact commands:

```powershell
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Debug --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Debug --verbosity quiet
dotnet test TrueMoon.Aether.Tests/TrueMoon.Aether.Tests.csproj --configuration Release --logger "trx;LogFileName=results.trx" --results-directory TestResults/AetherExtraction/tests-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/AetherExtraction/solution-Debug --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory TestResults/EngineSessions/aether-20261019 -IncludeViewport
```

Aether: Passed87/Failed0/Skipped0 в каждой Debug/Release. Full Debug solution:
Passed1490/Failed0/Skipped0,9TRX: Core107/Cobalt179/generator91/Enerit126/generator114/
Mithril94/UI613/Hosting79/Aether87. Это новый solution test run и discovery нового
test project через реальный entry point; не новый nonincremental build или full
Release solution. Generated Enerit nullable/compiler warnings не скрывались;
их policy/исправление не входят в перемещение assembly.

Native summary Completedtrue,34/34: по16headless scenarios и1viewport/configuration,
Debug apphost/published framework-dependent Release без RID. Headless повторяет
по20Start/Stop при одном App, identity/stale replies, startup/command timeout/cancel,
crash/disconnect/heartbeat, owned forced stop/parent exit/reap и authoring fixture.
Viewport — по14scenarios/25child launches, ещё по20Start/Stop при одном App/window,
actual GL pixels/BGRA/origin/counters, resize/zero/resume/slow UI, live-child EOF,
first-frame cancel/deadline и native close/drain-before-UI-disposal. Reports:
ChildrenReleasedtrue/Mappings0/ActiveUiSessions0/GameLogicLoadedInEditorfalse.
Source339/binary76 fingerprints сохранены; итоговая сверка evidence отдельно в
ignored `TestResults/AetherExtraction/completion.json`.

Итоговая сверка прошла: actual Aether test container обнаружен через solution,
test/native DLL hashes совпадают в обеих конфигурациях, старой Contracts assembly
нет в runtime deps graph.34report fingerprints сохранены;12file moves без source
дубликатов и завершение owned helper processes проверены. Документация:
14Markdown files/561local links/229anchors, git diff --check прошёл, staged0.

Own Release UI/golden/RTE/package/performance/physical hardware matrix не повторялись.
Новая assembly не означает готовность ENG3/3D/standalone game/поставки; UI K3/K7/K8/
VRAM/final alpha прежние. Предыдущая документальная сверка ниже остаётся отдельной.
Следующий code шаг — ENG3a внутри Aether с сохранением game DLL/host boundaries.

## Сверка плана после UI INF7 — 2026-10-19

По запросу пользователя проверить общий план сопоставлены общий/UI/engine roadmap,
отложенный RTE backlog и текущие probe sources. На начало сверки main/origin/main
совпадали на `29d9f79860418f86e8e2fca938aafc7fef5e6072`, worktree был clean.
Это review/документация: code/project files не менялись, build/tests/native/
performance/новой проверки runtime evidence не выполнялось.

Вывод: следующий общий этап ENG3 сохраняется; full INF5/RTE развиваются по
потребности UI, hardware alpha остаётся открытой. В
[ENG3 плане](PLAN.md#eng3-уточнение-плана-перед-реализацией--2026-10-19) добавлены
подэтапы compiler/artifact → checkpoint → replacement/rollback → integration proof.
Новых production API, packages, 3D/input/Hot Reload задач не назначено.

| Наблюдение по code/плану | Что уточнено в execution plan |
| --- | --- |
| [Host csproj](../../ManualTests/EngineSessionProbe/EngineSessionProbe.csproj) прямо ссылается на Game.Logic, [RuntimeHost](../../ManualTests/EngineSessionProbe/RuntimeHost.cs) создаёт ProbeGame | Выбранный artifact/loader/dependency set и canonical contracts должны быть определены до ENG3a; Editor DLL не загружает |
| [BuildIdentity](../../TrueMoon.Aether/Hosting/NativeRuntime.cs) читает Game.Logic из AppContext.BaseDirectory, [supervisor](../../TrueMoon.Aether/Sessions/SessionSupervisor.cs) хранит fixed game ID | Build descriptor/manifest выбирается на generation, включая rollback; compiler staging отдельно от immutable build-ID directory |
| [IGameSession/control](../../TrueMoon.Aether/Contracts/Control.cs) не имеют capture/restore; [ProbeGame](../../ManualTests/Game.Logic/ProbeGame.cs) хранит только минимальный stub | ENG3b вводит ограниченный versioned checkpoint и проверку non-default state; полной object-graph migration нет |
| [FrameSession.StartAsync](../../TrueMoon.Aether/Frames/FrameSession.cs) уже configure/wait first-present | Candidate publication после restore; первый default-state кадр не считается readiness replacement |
| CommandAsync на любую ошибку вызывает cleanup; [RuntimeProtocol](../../TrueMoon.Aether/Runtime/RuntimeProtocol.cs) не ack sequence при exception | Нужен отдельный nonterminal checkpoint rejection; обещание old paused/Resume нельзя вывести из нынешней transport-fault policy |
| ENG0 требует1game child, а общий раздел допускал подготовку двух сессий | Для ENG3 явно оставлен sequential stop/reap/start; overlap требует нового решения |
| Build120s/Replace45s заданы без распределения общего budget; runtime self guard120s | Перед реализацией определить monotonic budget/rollback reserve/cleanup и совместимый safety guard. Принятые численные deadlines этой сверкой не изменены |

Дополнены вопросы cancellation до/после destructive boundary, late build result,
Stop/новый Start/document revision и Editor close; partial logs/hash/pid/mapping
proof должен принадлежать engine runner. Прежний UI INF7 не становится ENG3 proof.
Исправлены устаревший next ENG1 в RTE плане, InputBatch приписанный ENG2 и
непомеченные исторические формулировки ENG0/ENG1. Общий STATUS/PLAN и UI STATUS
синхронизированы, подробные ENG3 задачи сохранены только в engine PLAN.

Проверка документации прошла:11Markdown files/463local links/204heading anchors,
fences/NUL/conflicts/whitespace, git diff --check и ограничение diff Markdown файлами.
Результат — ignored `TestResults/PlanningFormat/review-20261019.json`; новых runtime результатов
эта проверка не создаёт. ENG3 не начат; следующий code подэтап ENG3a.

## ENG1 headless supervisor — 2026-10-17

После RichTextEditor/PA7a и сохранения отложенного RTE backlog пользователь
перешёл к следующему этапу. Реализован первый исполнимый
[ENG1 scope](PLAN.md#eng1-headless-supervisor--2026-10-17),
[probe guide/запуск](../../ManualTests/EngineSessionProbe/README.md).

### Реализация и границы

- Созданы ManualTests/Engine.Probe.Contracts, Game.Logic, EngineSessionProbe и
  EngineSessionProbe.Tests; все4projects зарегистрированы в TrueMoon.slnx/folder Engine.
  Это probe-only API без production engine packages. Contracts не зависят от
  Mithril/Enerit/Alloy/Skia; существующие production модули для ENG1 не изменялись.
- Supervisor живёт при одном Cobalt Editor App и сериализует session commands.
  Каждый launch создаёт fresh owned ChildProcessUnitHandle и Enerit client;
  RestartPolicy.Never/IsControlAppLifetime=false, process-wide environment не меняется.
  OS Started, HelloAccepted, SceneLoaded и Paused Ready наблюдаются отдельно.
- SessionId сохраняется, UInt64 generation растёт и не переиспользуется после
  failed startup; endpoint/nonce свежие. Host/contracts/Enerit/Mithril и Game.Logic
  имеют отдельные SHA256 manifests. Runtime проверяет bootstrap с собственными bytes
  до открытия RPC. Editor вычисляет Game.Logic hash по файлу без загрузки DLL;
  native assembly census проверяет child-only границу.
- Wire DTOs mutable из-за текущего generator; identity и scene snapshots immutable.
  Enerit Int64 несёт полный UInt64 bit pattern generation/sequence/tick, domain
  counters остаются UInt64 с checked increments. Это явное уточнение wire adapter,
  без расширения общего scalar codec/лимита1MiB/schema negotiation.
- Фактический generated request/response payload ограничивается≤64KiB; scene
  проверяет≤64distinct entities/finite transforms/ID lengths и копируется до mutation.
  Hello/LoadScene/SetMode/Step/GetStatus/Stop имеют explicit identity/sequence/PID/
  mode/revision/tick acknowledgement. Playing timer target60Hz, performance не измерена.
- Stop ставит fence и отменяет локальный wait до входа в gate. Поздние replies и
  queued commands не применяются, interrupted command не повторяется. Faulted
  сохраняет phase/первичную cause; cleanup обязателен. CleanupFailed удерживает
  lease и блокирует Start до явного успешного cleanup retry; acknowledgement не
  выдаётся за process reap. Observer/diagnostic failures не меняют ownership.
- Единый graceful Stop budget3s включает RPC acknowledgement и Mithril stop;
  затем owned kill/reap≤5s. Startup15s, ordinary command2s, heartbeat1s/не более5s
  без accepted reply. Child parent monitor и аварийный2minute self deadline ограничены.

Game.Logic — минимальный отдельный DLL stub с loaded scene/tick/pattern color.
Frame metadata/codec/ring/viewport и input/surface ещё ENG2; checkpoint/restore,
compiler/build directories/replacement/rollback ещё ENG3.20Replace cycles не
выполнялись. Full UI alpha, K3/K7/K8/VRAM, самостоятельная игра/package delivery,
3D/GPU isolation и long resource/performance trend не объявлены готовыми.

### Финальные проверки

Windows x64/build26300/net10.0/SDK10.0.402; xUnit2.9.3/VSTest, central versions
пользователя сохранены. Default Cobalt; отдельная engine Microsoft DI matrix не
требовалась. Исторические UI package/golden/native результаты не повторялись.

| Проверка | Результат | Ignored artifact внутри TestResults/EngineSessions |
| --- | --- | --- |
| Engine Debug suite через solution |31passed/0failed/0skipped | final-tests/solution-Debug/TRX; прежний direct run сохранён отдельно в tests/engine-debug-final.trx |
| Direct engine Release suite |31passed/0failed/0skipped | final-tests/engine-Release/TRX и stdout.log |
| Full Debug solution entry point |1305passed/0failed/0skipped,8TRX; engine31 обнаружен через TrueMoon.slnx | final-tests/solution-Debug/TRX и solution-Debug-tests.stdout.log |
| Nonincremental Debug build |exit0/0errors/1207warnings | final-tests/solution-Debug-build.stdout.log |
| Native Debug apphost |16/16scenarios, включая20Start/Stop в одном Editor App | final-native/Debug-*/report.json/logs |
| Published Release |16/16тех же scenarios, framework-dependent без RID | final-native/Release-*/report.json/logs и publish binaries |

Debug solution counts: Core107, Cobalt179/generator91, Enerit126/generator114,
Mithril94, UI563, engine31. Release — только engine31; суммарные9TRX/1336passed
не являются двумя полными solution suites. Семь новых warnings — existing
Enerit generator CS8669 на nullable annotations в generated code; warnings не подавлены.

Final native summary:Completed=true,32scenario processes,34commands,187source
fingerprints и24binary entries. По20cycles в каждом repeat scenario, отдельные
sequential generations и recovery после fault. Во всех живых Editor проверяется
неизменность authoring revision/Dirty/selection/panels и App lifetime; каждый
created lease заканчивается подтверждёнными reap/resource cleanup.
Launch-failure case не выдаётся за успешно запущенный процесс.

16scenarios/configuration: repeat, stale, bad-hello, bad-scene, startup-timeout,
startup-cancel, command-timeout, command-cancel, stop-pending, crash, disconnect,
heartbeat, forced-stop, hang-stop, launch-failure, parent-exit. Negative failures
внутри сценариев ожидаемы и проверяются;32passing reports относятся к outer probes.
Hang-stop одновременно не отвечает на Stop RPC и игнорирует graceful process
shutdown: проверены shared deadline, observed StopAcknowledgement cause и owned kill.
Parent-exit proof убивает только принадлежащий ему parent без tree-kill, ждёт
child monitor cleanup marker и read-only OS census; census не является ownership path.

Основные команды из src (точные output paths есть в summary/logs):

```powershell
dotnet sln TrueMoon.slnx list
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Debug --logger "trx;LogFileName=engine-debug-final.trx" --results-directory TestResults/EngineSessions/tests --verbosity quiet
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -RunDirectory E:/source/my/TrueMoon/src/TestResults/EngineSessions/final-native
```

Unit evidence: Start_WaitsForHelloAndAppliedSceneBeforeReady,
RepeatedStartStop_UsesFreshLeasesGenerationsAndNonces,
MismatchedHello_IsRejectedBeforeSceneAndConsumesGeneration,
CleanupFailure_PreservesPrimaryErrorAndBlocksStartUntilExplicitRetry,
Stop_FencesPendingAndQueuedCommandsAndDiscardsLateReply,
StartupCancellation_CleansWithoutPublishingReadyAndAllowsNextGeneration,
UnexpectedExit_FaultsAndCleansWithoutCallerPolling,
ModeAndStep_ApplyOnAcknowledgementAndRejectStepWhilePlayingLocally,
InvalidApplicationAcknowledgement_FaultsInsteadOfCommittingState,
GeneratedWire_RoundTripsAllUnsignedBitsThroughBoundedTransport,
OversizedGeneratedPayload_IsRejectedBeforeTransportInvocation,
RuntimeProtocol_RejectsOrderAndSceneErrorsBeforeMutation.
Тесты используют реальные generated codecs и управляемые fake leases; native
matrix отдельно доказывает actual process/IPC/parent-exit composition.

### Исходные failures и дополнительные сверки

Initial build остановился на TME001:UInt64 scalar unsupported; переход wire
fields к bit-preserving Int64 исправил contract без изменений Enerit. Далее
исправлены предположение о generated namespace (он относится к host assembly)
и отсутствующие namespace imports Resolve/UnitCodes. Финальная сборка прошла.
Unit runs с момента первой компилируемой версии31/31, без skipped.

Сначала прошли repeat20 и matrix30cases. При ручной сверке deadlines найден
лишний отдельный3s budget Stop RPC перед Mithril grace; объединён в один token,
добавлен hang-stop и повторены32cases. Затем устранён typeof(Game.Logic) при
Editor hashing, добавлен assembly-boundary assertion и повторена final-native
matrix32cases на финальном source. Ранние passing matrices сохраняют свой
source scope; не выдаются за финальный запуск.

Вспомогательный verifier после уже успешных tests сначала ошибся на TRX filename
с [1] из-за wildcard Get-Content; чтение переведено на LiteralPath. При правке
helper также исправлена лишняя скобка. Исходный failure сохранён в verification/
summary.json, resume — summary-final.json; final-tests выполнен заново после
последнего code change и не использует неполный summary как passing evidence.

Подробные source/test/native logs сохранены внутри ignored TestResults; до
начала работы в дереве были пользовательские изменения и10staged entries.
Они не сбрасывались и не использовались как основание для commit/release.

Финальная сверка:11Markdown documents/301local links/127heading links,
fences/conflicts/NUL/whitespace и новые files whitespace прошли; полная
git diff --check с Windows CR-at-EOL policy чистая. Solution содержит все4engine
projects и test→production reference;187source/24binary fingerprints совпали
после final test/build. Существующие10staged entries сохранены, engine changes
не staged. Отчёт ignored TestResults/EngineSessions/completion.json.
Следующий конкретный шаг — ENG2 CPU codec/ring/viewport с сохранением ENG1 guards.

## ENG0 scope и контракты — 2026-10-15

Пользователь одобрил ENG0 после UI package/Hosting proof: ограниченный Editor/child
prototype, demo-комната/одно взаимодействие, CPU shared-memory frames и versioned
Game.Logic replacement. Допуск с открытыми UI K3/K7/K8/VRAM/final alpha принят явно.
Эти критерии не отменены; full alpha/engine readiness не заявлены.

В PLAN определены Windows x64/net10.0/Cobalt/OpenGL Editor, CPU pattern ENG1–ENG3,
target1280×720/30fps и tick60Hz. Эти targets/deadlines/budgets назначены для будущих
проверок и не являются измеренной производительностью или minimum hardware promise.
Vulkan3D/GPU sharing, standalone delivery и окончательный жанр имеют следующие этапы.

Сверены исходники IUnitsController/UnitsController/ChildProcessUnitHandle/
ProcessUnitHandle/ExternalUnitHandle и UnitConfigurationExtensions: controller
terminal после Stop, общий nonempty parameters unsupported, child запускает entry app.
Выбран один probe executable с двумя process roles, fresh ChildProcessUnitHandle
на каждое поколение и probe-owned per-launch context через protected hooks.
Это проектирование; repeatable use этих hooks в engine ещё не подтверждён.
Изменений Mithril/Enerit/Core/Cobalt/UI runtime в ENG0 нет.

Определены SessionId/SessionGeneration и отдельный SurfaceGeneration, HostContractId/
GameBuildId/versions, фазовая readiness, typed same-build64KiB control, bounded input.
Первый frame transport —3slot CPU ring/BGRA8/sRGB с явными ownership transitions,
latest-ready/drop policy, копированием вне UI thread, bounded memory, stale-frame
rejection и terminal recovery при повреждении/abandoned metadata.
Новый viewport/upload adapter относится к ENG2; наличие готового Alloy API не обещано.

Game replacement: compile candidate отдельно от active build, pause/checkpoint,
stop/reap, candidate restore/first frame, максимум1rollback к last-known-good.
Compile failure сохраняет текущую сессию; snapshot mismatch не reset без явного
выбора. Editor documents/Dirty/selection/panels принадлежат Editor и переживают
обычный child crash/replace. Failed cleanup блокирует новый launch.

Следующий code scope ENG1 — headless host/contracts/logic stub/supervisor с20repeat
Start/Stop, identity/Hello, stale response, startup timeout/cancel, crash/disconnect,
parent exit и cleanup. ENG2 добавляет UI/frame ring; ENG3 compiler/replacement.
Новых csproj/public API/build/tests/process/GPU/performance результатов нет.
Детальные задачи не дублируются в общих документах; их очередность/допуск
синхронизированы с общим PLAN/STATUS/QUALITY и UI STATUS.

### Проверки текущего документа

Фактически выполнено2026-10-15:9Markdown documents,219local links,110heading links,
0issues; проверены fences/conflict markers/NUL/replacement characters, наличие
ENG0–ENG8 и identity/limits/headless milestone. Scoped git diff --check passed,
staged changes0. Артефакт: ignored
`TestResults/EnginePlanning/eng0-20261015/verification.json`; checker
`TestResults/EnginePlanning/CheckEng0.ps1`. Это только documentation validation,
без code tests или нового module/runtime passing evidence.

## Исходное документирование — 2026-10-13

По предыдущему STATUS:42Markdown documents/447local links/107heading links,
0problems; fences/conflict markers/NUL и git diff --check passed.
Артефакты ignored TestResults/EnginePlanning. Новые files проверялись отдельно
на whitespace и наличие ENG0–ENG8. Это историческая документационная проверка,
не текущий запуск и не engine code evidence.

## ENG2 CPU frame viewport — 2026-10-17

По запросу следующего шага реализован [ENG2 scope](PLAN.md#eng2-cpu-frame-viewport--2026-10-17).
Код transport/contracts/viewport остаётся в ManualTests. Единственное production
UI API расширение — owned CopyBgraPixels в SkiaImageSource. Backend-neutral image
borrowing, renderer/device/window ownership, Core/Cobalt/Enerit/Mithril APIs сохранены.

### Реализация

Explicit codec проверяет version/header/layout/capacity/session/build/child/surface;
BGRA8 premultiplied/sRGB/top-left pattern включает corners/frame/tick/actual CRC32.
Windows ring3slots использует named mutex только для metadata, pixels вне lock;
latest-ready освобождает older Ready, producer не перезаписывает Reading и drops
при no Free. Abandonment/corruption/100ms timeout/CRC/EOF terminal, events лишь hints.
Receiver вне UI owner имеет2reusable buffers/1pending; disposed lease отпускает
ссылку на pixels, old receiver drains до allocating replacement buffers.

ConfigureSurface ack следует detach old producer; old mapping живёт до local
reader drain и ack/reap. Resize serial,≤2mappings/50MiB. Zero-size без mapping,
heartbeat сохраняется. First copied frame не означает Ready: FrameSession.Start
завершается только после actual owner presentation, в общий15s launch deadline.
Stop fences queued work/cancels current wait; stale display не подтверждает новый
surface. CleanupFailed retains mappings until explicit successful reap retry.
Native close drains controller/readers/child до UI disposal.

### Проверки на финальном source

SDK10.0.402 / .NET10.0.13 / Windows x64. Final native artifact:
`TestResults/EngineSessions/eng2-final-source-native/summary.json`;
34/34 cases:32ENG1 regressions +2viewport runs, Debug apphost / published Release.
Each viewport run14scenarios,25child launches, по20Start/Stop в одном Editor App/window:
first-presented1280×720, mode/step, resize320×180/1920×1080/16×16/1280×720,
zero-size/resume, slow UI/latest pending, crash, EOF при живом child, first-frame
cancel/15s deadline и native WM_CLOSE. GPU corners/enlarged counter rows сверены
по actual readback с учётом linear sampling, CPU CRC — до публикации receiver.
Final screenshot inspected. Mappings0/UI sessions0/children released/editor preserved,
Game.Logic assembly в Editor не загружена. Source323/binary76fingerprints и36commands
сохранены; final повторная сверка — `TestResults/EngineSessions/eng2-completion.json`.

| Run | Copy p50/p95 ms | Upload p50/p95 ms | Produce→present p50/p95 ms | Pending replacements |
| --- | --- | --- | --- | --- |
| Debug | 4.3877/22.2671 | 0.0593/0.5373 | 31.3073/77.5625 | 21 |
| Published Release | 1.5674/7.0793 | 0.0546/0.6386 | 29.6735/55.3139 | 22 |

Bounded256sample diagnostic windows; observed producer drops0 в GUI runs,
no-free producer drop отдельно подтверждён ring test. Это не baseline/performance
pass, не VRAM/native long-run resource trend. UI hardware blockers не закрываются.

Exact commands записаны runner summary:

```powershell
pwsh -NoProfile -File ManualTests/EngineSessionProbe/RunProbe.ps1 -IncludeViewport -RunDirectory TestResults/EngineSessions/eng2-final-source-native
dotnet test ManualTests/EngineSessionProbe.Tests/EngineSessionProbe.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/engine-Release --verbosity quiet
dotnet test TrueMoon.Alloy.Tests/TrueMoon.Alloy.Tests.csproj --configuration Release --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/ui-Release --verbosity quiet
dotnet test TrueMoon.slnx --configuration Debug --logger trx --results-directory TestResults/EngineSessions/eng2-final-tests/solution-Debug --verbosity quiet
dotnet build TrueMoon.slnx --configuration Debug --no-incremental --verbosity quiet
```

Engine87/87 Debug/Release, UI570/570 Debug/Release; full Debug1368/1368,
8TRX/failed0/skipped0. Engine includes new56cases (codec/wire/ring/receiver/composition)
и31ENG1cases; UI добавлены7shape/ownership/pixel cases.
Build0errors/1211warnings; warnings не подавлены.
Detailed regression evidence: FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip,
Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded,
Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain;
FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay,
Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild,
MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry;
SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha.

### Найденные и исправленные failures

Initial compile corrected Span→array delegate, Check accessibility и font asset path.
First native startup exposed missing Cobalt resolver from generic UsePresentation
registration: added explicit Transient<ViewportView> plus factory Action<string>.
Unit cleanup assertion initially expected missing mapping, but first absent resource
is named mutex (WaitHandleCannotBeOpenedException); assertion corrected to actual API.
Regression on a second read after CRC/EOF exposed misplaced terminal guard; guard
moved into each metadata operation and verified by repeated-fault assertions.
Native IsClosing assignment closed window without native close callback; replaced
smoke trigger with Process.GetCurrentProcess().CloseMainWindow (real WM_CLOSE),
then confirmed drain before UI disposal. First copies-only metrics were overwritten
by no-frame session; switched to bounded aggregate samples. Final review cleared
pixels reference on disposed lease and synchronized receiver counters; full native
matrix repeated after these edits. Failed/intermediate runs remain under ignored
TestResults/EngineSessions/eng2-*; none заменяет final evidence.

Next ENG3 compiler/checkpoint/versioned replacement/rollback,20Replace ещё не было.
InputBatch/focus routing remains deferred explicitly in PLAN. RTE backlog unchanged.

### Матрица ENG2 requirement / evidence

| Requirement | Evidence: exact test / run |
| --- | --- |
| Little-endian schema, unsigned wire/capacity/identity validation | FrameTransportTests.Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip; ConfigureSurface_GeneratedWirePreservesUnsignedGenerationAndDescriptor; Codec_RejectsEveryHeaderIdentityAndCapacityField |
| Latest-ready/drop/Reading ownership | FrameTransportTests.Ring_ChoosesNewestReadyReclaimsOlderAndDropsWhenFull; Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded |
| Terminal CRC/EOF/mutex faults | FrameTransportTests.Ring_CorruptionAndEofAreTerminal; Ring_MetadataTimeoutAndAbandonmentFailWithoutUnboundedWait |
| Two buffers/one pending and hint-only event | FrameTransportTests.Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain; Receiver_ScansWithoutReadySignalAndPropagatesEof |
| Surface ack/presentation/stale/resize/suspend | FrameSessionTests.StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay; ZeroSize_SuspendsReleasesMappingsKeepsHeartbeatAndResumesFreshSurface; Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces |
| Stop fence/cleanup retry | FrameSessionTests.Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild; MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry |
| Owned BGRA image, origin/alpha/shape | SkiaPixelImageTests.CopyBgraPixels_OwnsPixelsPreservesChannelsOriginAndPremultipliedAlpha; CopyBgraPixels_RejectsInvalidShapeBeforeNativeAllocation |
| Actual process/window/pixels/deadline/close | eng2-final-source-native Debug-viewport/Release-viewport reports;32ENG1 regressions;34/34 native |
| Successful validation commands | RunProbe.ps1 -IncludeViewport; dotnet test engine Release; dotnet test UI Release; dotnet test TrueMoon.slnx Debug; dotnet build TrueMoon.slnx Debug --no-incremental — exact arguments above |

Финальный documentation guard:13documents/470local links/154heading links,
fences/whitespace/conflicts/NUL и scoped/full git diff --check passed.
323source/76binary fingerprints повторно совпали после nonincremental build;
4engine projects/production test reference в solution проверены. Existing10staged
entries сохранены; новые engine/ENG2 test files не staged. Артефакт:
`TestResults/EngineSessions/eng2-completion.json`. Ни один новый package не публиковался.
