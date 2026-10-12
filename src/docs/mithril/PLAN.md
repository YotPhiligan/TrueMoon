# Mithril: план units и процессного lifecycle

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Уточнённые MIT0/MIT1/MIT2/MIT3 contracts записаны ниже; сквозная интеграция и поставка остаются открыты.

## Границы

TrueMoon.Mithril, Mithril.Tests, ManualTests/MithrilTest и bounded ManualTests/MithrilLifecycleProbe/MithrilCompositionProbe/MithrilRestartProbe. UnitsController, configuration, hosting/startup/lifetime/restart policies и handles main/child/external process.

App lifecycle/configuration принадлежат [Core](../core/PLAN.md), invocation/transport — [Enerit](../enerit/PLAN.md). У Mithril нет прямого ProjectReference на Enerit; совместный пример собирает их через приложение. Этот план не создаёт такой зависимости автоматически.

## Цель

Приложение запускает собственные units в заявленных hosting режимах, наблюдает exit/failure, применяет согласованные startup/restart policies и завершается без оставшихся принадлежащих ему процессов/handles. External process ownership описывается отдельно.

## Архитектура и решения

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

MIT0 не закрывается успешным build пустого test project. MIT1–MIT3 проверять на собственных тестовых процессах с bounded временем и cleanup. Системные службы/чужие приложения не используются как тестовые units.

## Критерии готовности

- Supported hosting/startup/lifetime/restart policies имеют зафиксированный результат и поведенческие checks.
- Failure/cancellation/shutdown не оставляют owned processes, handles или subscriptions; restart не продолжается после согласованного stop.
- Сохраняются исходные exit/failure причины и принадлежность процесса, public errors доступны caller/diagnostics.
- Independent consumer доставляет нужные executable/configuration artifacts и работает без исходного solution.
- Полный process/IPC scenario подтверждён на заявленной OS/configuration; неподдержанные режимы отмечены отдельно.

## После первой рабочей проверки

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
  Timeout/отмена graceful wait/закрытый stdin приводят к проверке exit и, если
  child жив, к `Kill(entireProcessTree:true)` только owned tree с ожиданием
  максимум5s. Cancellation stop сокращает graceful ожидание, cleanup продолжается.
  `true` означает exit наблюдён и `Process` освобождён; ошибка cleanup
  диагностируется и возвращает `false`, handle остаётся доступен для повторной
  попытки.
- Stop до старта/после exit/повторный stop успешны. Sync/async disposal сначала
  запрещает start, затем останавливает owned child; повторный disposal допустим.
  Cleanup failure при disposal — исключение, а не молчаливый успех.
- При `Never` natural exit даёт один callback, intentional stop callback не
  выдаёт. Restart/parent-crash и конкурентный пользовательский callback остаются
  отдельными MIT2/MIT3 критериями; наличие lifecycle gate не является их proof.

Следующая Mithril работа: MIT1 — изоляция static unit configuration между App
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

MIT1/MIT2 выполнены только в этом scope. Следующий этап MIT3, затем MIT4/ENE4.
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
