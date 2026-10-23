# Mithril: план units и процессного lifecycle

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Уточнённые MIT0–MIT5 contracts записаны ниже; bounded App/IPC/package scope и operational diagnostics имеют отдельные датированные проверки в статусе.

## Границы

TrueMoon.Mithril, Mithril.Tests, ManualTests/MithrilTest и bounded ManualTests/MithrilLifecycleProbe/MithrilCompositionProbe/MithrilRestartProbe. UnitsController, configuration, hosting/startup/lifetime/restart policies и handles main/child/external process.

App lifecycle/configuration принадлежат [Core](../core/PLAN.md), invocation/transport — [Enerit](../enerit/PLAN.md). У Mithril нет прямого ProjectReference на Enerit; совместный пример собирает их через приложение. Этот план не создаёт такой зависимости автоматически.

## Цель

Приложение запускает собственные units в заявленных hosting режимах, наблюдает exit/failure, применяет согласованные startup/restart policies и завершается без оставшихся принадлежащих ему процессов/handles. External process ownership описывается отдельно.

## Архитектура и решения

Уточнение2026-10-22: [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory)
вводит injected IFactory<IUnitHandle,IUnitConfiguration> и штатный UnitHandleFactory.
UnitsController передаёт snapshot configuration фабрике и остаётся владельцем handles;
protected CreateHandle сохраняет extensibility. App controller/parent factories
зарегистрированы явно. Borrowed IUnitsController facade и AppServiceLifecycle
отделяют aliases от disposable owner. Process policies/budgets прежние.

Сохранять IUnitsController, IUnitConfiguration и IUnitHandle boundaries, явные policy values и configuration через Contracts. Runtime зависит от Contracts/Core, не от UI.

Определить semantics Start/Spawn/Stop/disposal, startup cancellation/failure, identity конфигурации/handle и допустимого restart. Разделить owned child и borrowed/external process: действия с чужими процессами не выводятся из общего app shutdown. Правила restart limits/delay и exit во время stop согласовать до реализации гарантий.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| MIT0 | Сверка discovery и рабочего manual scenario | Зафиксирован фактический набор тестов, в том числе zero discovery; пример запускает только собственный процесс и сохраняет результат |
| MIT1 | Configuration/startup/hosting contract | Main/child/external modes подтверждены в объявленном scope; invalid configuration/start failure/cancel не оставляют partial handles |
| MIT2 | Shutdown и resource ownership | Stop/disposal/repeat/cancel, parent exit и cleanup соответствуют контракту; внешние процессы обрабатываются по явному ownership правилу |
| MIT3 | Restart и отказ процесса | Normal/crash exits, policy selection и остановка во время restart воспроизводимы; нет непредусмотренного повторного запуска или неконтролируемого restart loop |
| MIT4 | Интеграция с Enerit/Core | Own child unit выполняет IPC, при exit/failure канал и процесс закрываются по контрактам, app shutdown согласован |
| MIT5 | Consumer и operational diagnostics | Published/package consumer запускает/останавливает own child; policy/error/exit diagnostics и commands/configuration воспроизводимы |
| MIT6 | Частично: первый MIT6a saturated-stop/kill профиль проверен2026-10-23; runtime API прежние | Полный fault matrix, status по потребителю и resource/parent-death scope остаются задачами ниже |

MIT0 не закрывается успешным build пустого test project. MIT1–MIT3 проверять на собственных тестовых процессах с bounded временем и cleanup. Системные службы/чужие приложения не используются как тестовые units.

## Критерии готовности

- Supported hosting/startup/lifetime/restart policies имеют зафиксированный результат и поведенческие checks.
- Failure/cancellation/shutdown не оставляют owned processes, handles или subscriptions; restart не продолжается после согласованного stop.
- Сохраняются исходные exit/failure причины и принадлежность процесса, public errors доступны caller/diagnostics.
- Independent consumer доставляет нужные executable/configuration artifacts и работает без исходного solution.
- Полный process/IPC scenario подтверждён на заявленной OS/configuration; неподдержанные режимы отмечены отдельно.

## После первой рабочей проверки

### MIT6: укрепление процессов и IPC — ревью 2026-10-23

Предложения основаны на чтении кода/планов; конкретные API и новые гарантии требуют
контракта перед реализацией. MIT0–MIT5 сохраняют проверенный Windows x64/net10.0
own-child/cooperative scope. Mithril управляет OS process/exit/restart/reap; Enerit
— каналом и invocation. Ready/Hello, heartbeat, game session/generation и выбор
восстановления состояния уже принадлежат Aether, не дублируются в Mithril.
У engine child уже RestartPolicy.Never: replacement/recovery координирует Aether;
не включать поверх него независимый automatic restart без отдельного решения.

| ID | Предлагаемая задача | Критерий и границы |
| --- | --- | --- |
| MIT6a | Общий Enerit/Mithril stop/drain и fault/load acceptance перед интерактивным preview | Одна согласованная последовательность: закрыть приём операций/зафиксировать session fence, отменить IO/pending, запросить cooperative shutdown, дождаться в пределах graceful budget, затем kill/reap owned process при необходимости и освободить transport/resources после подтверждённого завершения. Ожидание drain не должно блокировать переход к kill. Все фазы расходуют согласованный общий budget, cleanup failure наблюдаем и owner сохраняется для retry |
| MIT6b | Наблюдаемое состояние процесса для consumer | Проверить достаточность существующих UnitProcessDiagnostic/Completion. Если реальный consumer требует чтение через injected contract, добавить минимальный readonly snapshot/terminal completion без downcast; не навязывать новый generic lifecycle/state-machine framework. OS Started, IPC Connected и Aether Ready — разные события; PID не заменяет run/generation identity |
| MIT6c | Длительные сессии, parent death и ограниченные diagnostics | Повторные start/stop/crash/restart с pending IPC, handle/thread/process trends и точными own-PID outcomes. Проверить abrupt parent exit с child, не завершающимся кооперативно; существующий observer лишь запрашивает App cancellation. При необходимости отдельно спроектировать OS-level containment. У владельца redirected stdout/stderr обеспечить постоянное чтение с bounded retention и явным overflow/truncation, не держать весь log в памяти |
| MIT6d | Расширение restart/hosting по реальному сценарию | Exponential backoff/jitter/reset-after-stability, независимый external restart и новые hosting modes вводятся только при потребителе и новых tests. Текущие bounded limit3/delay250 и Never для Aether не менять ради списка возможностей; borrowed attach не смешивать с owned cleanup |

MIT6a — единый детальный межмодульный acceptance, Enerit ссылается на него из ENE6.
Отдельно зафиксировать доставку Stop до разрыва нужного control channel либо
через независимый Mithril lifecycle channel; закрытие admission не должно
запрещать сам shutdown. Domain save/snapshot перед Stop остаётся у Aether.
Нужны slow/nonreading peer, overload и pending calls при Stop, cooperative и
ignoring-cancellation handler в отдельном own child, crash/disconnect, stop во
время restart preparation/delay, repeated/concurrent cleanup и failed reap.
Проверять terminal outcomes, отсутствие replay/новой generation после fence,
возврат buffers и завершение owned tasks; Debug/published Release и затронутые
independent Cobalt/DI package consumers. Новые результаты не выводятся из старых
36/108-case матриц. Начать с контракта и воспроизводимого сценария, затем fixes.

В in-process/Main режиме uncooperative Task нельзя объявить завершённым по одному
timeout: dependencies остаются owned до реального завершения. Тот же предел есть
у user launch preparation, удерживающей lifecycle gate. Parent-death containment
и long resource trends — отдельная приёмка перед рабочей поставкой, не условие
локального headless authoring. Domain preview/frame lifecycle остаётся в Aether.

### MIT6a: первый load-stop профиль — 2026-10-23

По запросу приступить к работе выбран первый ограниченный срез с
[finite admission/async stop Enerit](../enerit/PLAN.md#ene6a-и-mit6a-первый-контракт--2026-10-23).
Использовать существующие AppUnitsPipesProbe, controller и отдельный Mithril
control endpoint; новое Mithril runtime API или второй readiness supervisor
для этого proof не нужны.

Два дополнительных сценария saturated-stop/saturated-kill на Cobalt/Microsoft DI:
запустить один owned child, занять два server handlers и client pending slots,
поставить один call в bounded wait queue и подтвердить reject следующего до
dispatch. Запросить stop owned unit через независимый lifecycle pipe, пока
доменные handlers активны; затем завершить App/client cleanup. Проверить exact
PID, control receipt, elapsed budget, terminal process diagnostics, no restart,
pending/queued0 и monitor/client Completion. Cooperative child выходит0 и
подтверждает server/handler drain. Ignoring-cancellation child получает kill/reap;
его managed finally/server drain после kill не объявляется выполненным.

Повторить Debug и published Release, затем fresh independent PackageReference
matrix с обоими providers и DI без Cobalt analyzer. Прежние9сценариев сохраняются;
расширенный runner содержит11×2config×2delivery×3provider =132cases. Каждое число
становится результатом только после текущего Completed=true в STATUS/HISTORY.
Общий budget этого профиля учитывает существующий3s graceful и bounded5s reap,
внешний10s oracle; arbitrary blocking launch preparation/in-process callbacks
по-прежнему кооперативны. Slow/malformed peers и stop callbacks дополнительно
проверяются напрямую в Enerit.

Срез не закрывает весь MIT6a fault matrix, real OS permission/reap failures,
parent-death containment или long resource trends MIT6c. Эти случаи остаются
отдельными criteria и не выводятся из successful saturated-stop/reap.

### MIT0 own-child контракт — 2026-10-11

Первый baseline ограничен `ChildProcessUnitHandle`, `RestartPolicy.Never`, Windows
x64/net10.0. [Воспроизведение](../../ManualTests/MithrilLifecycleProbe/README.md),
[фактические результаты](HISTORY.md#mit0-own-child-baseline--2026-10-11).
MIT0 выполнен в этом scope; MIT1–MIT5 не объявляются завершёнными.

- Handle владеет только процессом, который сам успешно запустил. Controller не
  перечисляет и не завершает другие приложения по имени executable. Прежняя
  `CheckTrailingProcesses` удалена: одинаковое имя не доказывает ownership.
- Child запускает текущий apphost; при `dotnet <assembly>` путь entry assembly
  добавляет именно child handle. `-u`/`-p` передаются через environment.
  Готовность процесса для приложения требует отдельного handshake, `StartAsync`
  подтверждает только успешный OS start.
- Pre-cancelled start и cancellation после launch preparation не запускают
  процесс. Negative `TerminationDelay` отвергается до старта. Failed OS start
  сохраняет исходное исключение и освобождает `Process`; controller удаляет
  неуспешный handle. Повторный start активного handle отвергается.
- Stop посылает `qe`, flush и ждёт exit до `TerminationDelay` (default1000ms).
  Timeout/отмена graceful wait приводят к проверке exit и, если
  child жив, к `Kill(entireProcessTree:true)` только owned tree с ожиданием
  максимум5s. Cancellation stop сокращает graceful ожидание, cleanup продолжается.
  `true` означает exit наблюдён и `Process` освобождён; ошибка cleanup
  диагностируется и возвращает `false`, handle остаётся доступен для повторной
  попытки.
- Уточнение2026-10-21 по ENG4e: ошибка доставки exit через stdin/control pipe
  не отменяет оставшийся graceful budget. Второй канал мог уже инициировать
  cooperative cleanup; после delivery IOException/InvalidOperationException
  дождаться exit до прежнего deadline и только затем применять owned kill/reap.
  Pre-cancelled/истёкший deadline по-прежнему сокращает ожидание; лимиты не увеличены.
  Regression — delayed EOF cleanup при закрытом parent stdin, exit0 без Killing.
- Stop до старта/после exit/повторный stop успешны. Sync/async disposal сначала
  запрещает start, затем останавливает owned child; повторный disposal допустим.
  Cleanup failure при disposal — исключение, а не молчаливый успех.
- При `Never` natural exit даёт один callback, intentional stop callback не
  выдаёт. Restart/parent-crash и конкурентный пользовательский callback остаются
  отдельными MIT2/MIT3 критериями; наличие lifecycle gate не является их proof.

На момент MIT0 следующая Mithril работа: MIT1 — изоляция static unit configuration между App
instances, реальный MainProcess contract и ошибки/отмена composition. Затем MIT2
parent-exit, controller shutdown/error propagation, callbacks/concurrent stop;
MIT3 restart limits/delay и stop-versus-restart. Общий следующий IPC шаг принадлежит
ENE1/ENE2; интеграция через `AddUnit` остаётся MIT4.

Развивать наблюдаемость, управляемые политики и удобство deployment по реальным units. Distributed orchestration, remote supervisors и расширение process ownership не входят автоматически в первую рабочую версию.

### MIT1/MIT2 App composition контракт — 2026-10-11

Решения уточнены при реализации. Проверенный scope — Windows x64/net10.0,
App lifetime, Never, Cobalt/Microsoft DI,
[source consumer](../../ManualTests/MithrilCompositionProbe/README.md).
Другие restart policies принадлежат MIT3; независимая поставка — MIT5.

- Registry принадлежит App configuration context, indexes начинаются с1.
  Configure callback выполняется при декларации; unit services/configuration
  callback — при build выбранного child или main units в parent. Повторный
  build создаёт независимые snapshots. Nested AddUnit отвергается.
- Snapshot до handles проверяет positive unique indexes, nonblank unique names,
  enum values, nonnegative TerminationDelay и required External executable path.
  Копируется список/settings; произвольные reference values не deep-cloned.
  Defaults child/immediate/app/always сохранены; проверен явно выбранный Never.
- MainProcess выполняет services/configuration в текущем App graph/PID; local
  lifecycle принадлежит App, отдельного process/DI scope нет. Поддержан Immediate.
  Main Delayed/None startup, Request/None lifetime явно NotSupported.
- ChildProcess запускает текущий apphost/hosted assembly. External запускает
  указанный executable и владеет им; attach/borrow отсутствует. Процессы по имени
  не завершаются; parent process является borrowed observer.
- Immediate units запускаются при App Start; Delayed/None доступны явному Spawn.
  Unknown/duplicate active unit и nonempty launch parameters отвергаются.
  Двухаргументный standalone controller без configurations означает ноль units.
- Controller сериализует lifecycle. Failed/cancelled start очищает partial и
  ранее принадлежащие handles в обратном порядке. После Stop/failed Start launch
  запрещён. Исходное исключение сохраняется; несколько errors агрегируются.
- Stop пытается очистить каждый handle даже после false/throw. Failed handle
  остаётся для retry; успешный stop/disposal удаляет его. Dispose запрещает
  launch, пытается очистить всех, сохраняет failures и делит completion task
  между callers. Stop может повторить retained cleanup после failed Dispose.
  Reentrant Dispose из собственного cleanup callback не ждёт сам себя.
- Natural exit controlling unit отменяет App один раз; intentional stop и поздние
  callbacks повторной отмены не дают. Арбитраж exit/restart/stop остаётся MIT3.
- Child command channel — async named pipe, per-launch endpoint передаётся в
  environment `TrueMoon.Mithril.ControlPipe`. qe/EOF/parent exit/missing parent
  отменяют child lifetime один раз; unknown commands игнорируются. Stop/Dispose
  отменяет accept/read/parent wait, ждёт owned tasks, освобождает pipe/observer/CTS.
  Reentrant lifetime callback запускает cleanup без ожидания самого monitor;
  внешний caller может дождаться Completion.
- Parent handle сохраняет stdin qe/flush для старых children и отправляет qe в
  новый pipe; ожидания ограничены graceful deadline. Timeout/cancellation ведёт
  к kill/reap только owned tree. Ручной selected child без endpoint имеет parent
  observation без stdin listener — явное изменение control protocol новых Apps.
- Mithril runtime не получает зависимости на Cobalt/Enerit. Внутренние
  controller/listener и aliases регистрируются явными factories на один instance;
  consumer Cobalt generator нужен его собственным services.

MIT1/MIT2 выполнены только в этом scope. На момент этого этапа далее MIT3, затем MIT4/ENE4.
Другие OS, package delivery, stress/resource trend, произвольные callbacks и
дополнительные lifetime/startup modes остаются открыты.

### MIT3 bounded restart контракт — 2026-10-12

Уточнение перед MIT3: прежний synchronous Restart повторно использовал Process и
вызывал OnExit каждой generation, включая промежуточные exits. Новый контракт
относится к owned process handles, App lifetime; Main local services не получают
process restart. [Consumer](../../ManualTests/MithrilRestartProbe/README.md).

- Never не перезапускает; Once допускает одну automatic attempt после любого
  exit; OnCrash допускает только nonzero OS exit; Always — любой OS exit.
- RestartLimit задаёт общее число automatic attempts на explicit Start,
  default3. Initial launch не считается, success не сбрасывает бюджет; zero
  отключает restart. Once использует меньший из limit и1. Unlimited режим не
  добавляется; default Always сохранён, но теперь bounded.
- RestartDelay задаёт milliseconds до каждого automatic attempt, default250.
  Обе настройки nonnegative; invalid values отвергаются до launch preparation
  в controller snapshot и direct process handle. IUnitConfiguration содержит
  default implementations новых settings через существующий key-value store.
- Каждая attempt строит новый ProcessStartInfo/Process/control endpoint.
  Предыдущий exited Process освобождается до следующего launch. Restart
  сериализован с Start/Stop; token прерывает delay и launch preparation.
- Stop/Dispose сначала запрещает scheduling/launch и отменяет monitor token,
  затем gracefully stops либо kill/reap только принадлежащий current tree и
  ждёт monitor completion. In-flight OS start до stop fence остаётся owned и
  очищается; после fence новая generation не запускается.
- Terminal OnExit приходит один раз при завершении policy/budget либо failed
  automatic start; intermediate exits не отменяют controlling App lifetime.
  Intentional Stop/Dispose уведомления не даёт. Callback, уже вошедший до Stop,
  может завершиться; reentrant Dispose из собственного terminal callback не
  ждёт сам себя, внешний caller может ждать Completion.
- Automatic failure не выходит unhandled из event callback: исходное исключение
  сохраняется в LastRestartError/diagnostics, budget attempt расходуется,
  sequence завершается. RestartCount и Completion доступны caller. Ошибка
  initial start по-прежнему идёт вызывающему StartAsync; cleanup error Stop
  возвращает false и сохраняет owned handle для retry.
- Protected extension hook Restart() заменён RestartAsync(CancellationToken).
  Custom launch overrides должны учитывать token и ownership. Diagnostics sink
  exception не препятствует cleanup. OnProcessExited по-прежнему наблюдает
  каждый natural exit; arbitrary hook/callback reentrancy не обещается.

Acceptance: normal/nonzero exit всех policies, explicit/default/zero budget,
explicit/default delay, missing executable automatic failure, cancellation
pending delay/preparation, stop restarted child, controlling lifetime и
reentrant/concurrent cleanup подтверждаются bounded own-process scenarios в
Debug/published Release. Затем MIT4/ENE4 App/AddUnit + generated Enerit IPC.
Другие OS, stress/resource trend, package delivery и recovery долгоживущих
production units не выводятся из этой матрицы.

### MIT4/ENE4 App и generated pipes контракт — 2026-10-14

Уточнение сквозного G3 после MIT3: один parent App и один owned AddUnit child,
App lifetime, Windows x64/net10.0, Cobalt/Microsoft DI. Вызовы проходят через
`UseInvocationService<T>` и `ListenInvocationService<T,TService>`, generated
proxy/handler и pipes factories; probe не создаёт adapters/transport напрямую.
[Consumer и runner](../../ManualTests/AppUnitsPipesProbe/README.md).
Это основной детальный контракт MIT4/ENE4; Enerit ссылается на него.

- Service/payload sources и версия generator одинаковы у peers. Parent выбирает
  уникальный endpoint, child наследует его; default service full name сохранён.
  Diagnostics подключается явно. Consumer local service регистрируется явно
  перед Listen, чтобы Cobalt видел его constructor graph.
- Generated registration содержит typed factories, внутренние transport factories
  также явные. Static storage хранит metadata/factories; service, client и handler
  instances принадлежат конкретному App. Повторный Use/Listen не создаёт второй
  transport; существующая local registration сохраняется, но не блокирует Listen.
- App Start/Stop aliases одного transport lifetime участвуют в startup rollback.
  У зарегистрированного здесь owned transport Stop выполняет async/sync Dispose,
  либо IStoppable fallback. Pipes cleanup повторяем; DI disposal позже допустим.
  Заранее зарегистрированный transport сохраняет lifecycle своего владельца.
- Typed DTO/null/Unicode,32concurrent, Task acknowledgement, remote exception,
  local request cancellation и следующий вызов проверяются на обоих providers.
- OnCrash/limit1/delay50ms: crash owned child завершает старый pending call ошибкой,
  без retry/replay. Intermediate exit не отменяет App; новый PID/control endpoint
  и server восстанавливают последующие вызовы через тот же client/proxy.
  Второй crash исчерпывает бюджет, отменяет controlling App, третьей generation нет.
- App.Stop при pending cooperative RPC и failed App.Start после предварительного
  Spawn очищают client pending/workers и owned controller handles до App.Dispose.
  Cooperative child gracefully выходит0, освобождает server workers и останавливает
  local service один раз. Stop повторяем.
- Parent abrupt exit проверяется supervisor: kill только известного parent,
  без entireProcessTree. Child production parent observer отменяет App, закрывает
  server/handler и выходит0. Client cleanup убитого parent не наблюдаем и не заявляем.

MIT4/ENE4 выполнены в этом bounded scope при passing Debug/published Release
36-case matrix и runtime/generator regressions. Нет зависимости Mithril→Enerit;
production изменения принадлежат Enerit. Независимая package delivery — MIT5/ENE5;
MMF, другие OS, stress/zero-leak proof, arbitrary custom transports/callbacks и
uncooperative handlers остаются отдельными критериями. Response validation и partial nested-buffer cleanup Enerit проверены позднее
того же дня; [локальное решение](../enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14).
Далее MIT5/ENE5 independent package composition.

### MIT5/ENE5 independent package contract — 2026-10-14

Основной детальный plan этой межмодульной поставки ведётся здесь.
Уточнение2026-10-14 после [принятого Cobalt delivery contract](../cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14):
те же9producer projects теперь дают8nupkgs, Cobalt analyzer bundled в runtime
package и отключается EnableCobaltGenerator=false; только Enerit analyzer остаётся
отдельной explicit PackageReference. Functional matrix/own-process scope ниже
сохраняются; повторные108cases и delivery evidence записаны у Cobalt HISTORY.
[Runner](../../ManualTests/UnitsPackageConsumer/README.md). Package portion
MIT5/ENE5 проверяет тот же bounded MIT4 own-child/same-build/cooperative pipes
contract на Windows x64/net10.0/.NET10 SDK compiler. Operational diagnostics MIT5
и latency/throughput/allocations baseline ENE5 остаются отдельными критериями.

- Собрать девять packages App/Contracts/Core/Cobalt/Cobalt.Generator/DI adapter/
  Enerit/Enerit.Generator/Mithril с одинаковой unique prerelease version в local feed.
  Generator packages analyzer-only; runtime packages имеют нужные dependencies и
  самостоятельную документацию/metadata assets. NuGet publish не выполняется.
- В fresh consumer directories скопировать согласованные Program/Contract probe
  sources как application code. Их build использует только PackageReference;
  Compile links/ProjectReference и Repo build/CPM defaults не допускаются. Отдельные
  Directory.Build props/targets/Directory.Packages.props и NuGet.Config являются
  boundary. Repo global.json по-прежнему выбирает заявленный SDK.
- TrueMoon dependencies должны реально прийти из unique local feed в isolated
  package cache. В assets нет project libraries или UI/native dependencies. Проверить
  metadata source/version/hash и фактические compiler Analyzer paths/bytes, generated
  client/handler/DTO/initializer sources. В deployed output нет generator/Roslyn DLLs.
- Explicit PrivateAssets=all references обоих analyzers для Cobalt/DI; дополнительный
  Microsoft DI consumer исключает Cobalt analyzer. Omitted Enerit analyzer — fresh
  negative compile control CS0234/CS0246 на concrete generated type evidence.
  Reflection/ручное construction proxy в functional path не заменяет App resolution.
- Matrix:9scenarios ×Debug/Release ×build/publish ×(Cobalt,DI,DI без Cobalt analyzer)
  =108owned case processes. Framework-dependent output без RID включает apphost,
  assembly/deps/runtimeconfig и dependency DLLs; selected child запускает delivered
  consumer. Payload/concurrent/cancel/error/restart/budget/Stop pending/rollback/
  abrupt parent exit сохраняют exact PID/exit/count/lifetime/cleanup oracles.
- Case deadlines и failure kill/reap ограничены own launched tree. Parent-exit
  supervisor убивает только известный parent; killed client workers не наблюдаем.
  Source SHA256/nupkg hashes/commands/actual results сохраняются, stale case markers
  исключены fresh directories/endpoints. Completed требует108cases и negative control.

Эта package acceptance не закрывает performance/diagnostics, MMF/schema/other OS,
RID/self-contained/single-file/trimming/NativeAOT, произвольные custom transports,
uncooperative shutdown или production resource trend. После неё измерить IPC
baseline, затем уточнить operational diagnostics, сохраняя эти boundaries.

### MIT5 operational diagnostics — 2026-10-15

Принято для quality прохода: наблюдаемость существующего bounded owned-process
sequence через TrueMoon `IEventsSource`, без новых Microsoft DI/logging contracts,
process ownership modes или recovery APIs. [Подключение/переходы](DIAGNOSTICS.md).

- Public `UnitProcessDiagnostic`/`UnitProcessStage`, category `UnitProcess`:
  RunId для каждого принятого explicit Start, attempt generation, unit/owner/child
  identity, effective policy/budget/delays, consumed attempts, exit code и исходный Error.
- Наблюдать preparation/OS start, natural exit/wrapper release, решение restart и
  terminal policy/budget/failure, intentional Stop, kill/reap outcome и cleanup failure.
  Started не означает service readiness; Terminal — решение, не delivery acknowledgement.
- Snapshot фиксируется до lazy sink; initial failure остаётся caller, automatic
  failure — тот же Exception в LastRestartError. Прежние текстовые события сохранены.
  Sink exception/disposed source не меняют lifecycle result и не препятствуют cleanup.
- Stop callbacks и monitor могут перемежаться; Stopped следует monitor drain, кроме
  reentrant terminal callback (TerminalCallback без self-wait). Repeated Stop может
  дать NoProcess. Новая sequence не подменяет captured identity старого Stop.
- Не записывать argv/environment/control endpoints в payload. Error может включать
  OS details. Синхронный observer не должен блокировать или reenter lifecycle.
- Acceptance: existing 20 restart +9 shutdown cases с exact trace identity/count/order,
  policy/limit/error/default settings, cancelled preparation/delay, forced stop и
  throwing sink, Debug hosted/apphost и published Release. Independent package
  App.UseDiagnostics проверяет те же public payload и Core pipeline на Cobalt/DI и
  DI с отключённым Cobalt analyzer. Abrupt-parent-exit не обещает trace killed parent.

MIT5 закрывается в этом Windows x64/net10.0 same-build/cooperative scope при текущих
runtime regressions, published traces и fresh package composition. Other OS/RID/AOT,
arbitrary extensions, independent external restart, long resource trend и durable
audit/recovery остаются отдельным развитием. Следом свести качество модулей и
оставшийся UI acceptance перед выбором ENG0; performance backlog сам по себе не
расширяет текущий functional scope.
