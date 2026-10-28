# TrueMoon.Alloy: план первой рабочей версии

План восстановлен 2026-09-27 из предоставленного пользователем текста предыдущей сессии. Сохранены цели, архитектурные решения, последовательность работ, проверки и границы первой версии. Идентификаторы пунктов добавлены для отслеживания выполнения.

## Область плана — уточнение формата 2026-10-09

Это локальный план UI направления **Alloy / Argentis**: TrueMoon.Argentis, TrueMoon.Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting, их compatibility path, UI tests и manual consumers. Общие задачи bootstrap/DI/IPC/units, build всего solution и общий выпуск относятся к [общему плану TrueMoon](../PLAN.md) и планам Core/Cobalt/Enerit/Mithril.

Разделение документов не меняет UI цели, архитектуру, этапы1–5, K1–K9, PA1–PA10, net10.0/xUnit или требования alpha. Общий build и ошибки других модулей учитываются в [общем статусе](../STATUS.md); UI хранит собственные проверки и влияние общих зависимостей на интеграцию. Подробные прежние записи сохранены в [HISTORY.md](HISTORY.md), текущая сводка — в STATUS. Правила ведения — [PLANNING.md](../PLANNING.md).

Текущее состояние, доказательства и точка продолжения находятся в [STATUS.md](STATUS.md). Наличие пункта в этом документе означает требование к результату; статус реализации определяется отдельно.

## Дополнительный план улучшения инфраструктуры Alloy — 2026-10-18

### Поддержка рабочего приложения Aether.Editor — 2026-10-22

Решение2026-10-27: целевой TrueMoon.Aether.Editor — library/package, executable
host подключает reusable View и выбирает поддержанный window/backend/resources.
ARCH2b уже перевёл Editor в library/App module; оставшийся переход и acceptance ведутся в
[ARCH2](../engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27) до новых ED features.
Editor panels/styles/commands остаются в Editor; общие controls/input/layout —
здесь. Изменение формы поставки не закрывает и не расширяет K/PA/INF критерии.

Уточнение2026-10-23: редактируемые inspector/tools подключаются после
[минимального authoring API, headless proof и Edit preview Aether](../engine/PLAN.md#основа-api-перед-редактируемым-ui--2026-10-23).
ED1a project/session идёт параллельно; IPC Editor↔Runtime использует Enerit,
не становится новым UI transport. Полный движок и весь UI beta не являются
предварительным условием первого среза.

Согласован [прикладной план ED1–ED8](../engine/EDITOR_PLAN.md). Здесь остаются
общие UI capabilities и их acceptance; прикладной checklist не дублируется.
ED4 использует overlays/dialogs, command/menu/shortcut, tree/selection и forms
conversion/validation/commit/cancel. ED5 нуждается в input/focus/capture и viewport
tool integration. ED7 проверяет custom widgets через Element и backend-independent
drawing/input contracts, а также конкретные capability interfaces по потребителям.

IEditorCommand/panel/property-editor/tool contracts принадлежат пакету Editor;
пользовательские UI controls — Argentis/Alloy. Сам IButton не заменяет Element:
переход runtime на полностью интерфейсное дерево не назначен. Начать с минимальных
capabilities, необходимых первому Transform/preview/Undo/save срезу, сохраняя UIA1,
net10.0/xUnit/ownership и существующие PA/K критерии. Это документационное
планирование без новых code/API или текущих passing результатов.

### UIA1: View, Data/ViewModel и scoped styles — 2026-10-22

По запросу пользователя реализована штатная модель авторинга: `View<TData>` с
Fluent-композицией, отдельные optional Data/ViewModel и `IViewStyle<TView>`.
Argentis предоставляет provider-neutral style contracts/builders; Alloy сохраняет
retained tree, attachment/binding/thread ownership. Первый production consumer —
Aether.Editor: EditorViewModel/EditorViewStyle, DI-created owner-thread View.
Правило закреплено в AGENTS; новые public типы находятся в корне Argentis согласно
назначению и namespace. Общий детальный UIA1 план ведётся здесь, engine ссылается.

Контракты: Root и typed Part выбирают стабильные части текущего View; typed Role
покрывает существующие/будущие элементы, совпадает по identity token. Ближайший View
является строгой границей, включая корень вложенного View. Последнее совпавшее
правило выигрывает в пределах view layer; local/binding → direct Style → view style
→ Theme → default. WithViewStyle заменяет/снимает слой; старые значения не остаются.
Тематические значения вычисляются до commit. Declaration/value failures сохраняют
предыдущий слой/Theme, observer failures наблюдаемы после commit. Immutable
declarations переиспользуются, per-view builders замораживаются после Configure.

ViewModel хранит UI-independent state/actions/validation; обычным данным не нужен
обязательный base class. View подключает bindings и визуальное представление
состояния, владеет подписками и элементами. DI factories/styles/модели заимствуются
окном/деревом по lifetime. Style не удерживает live View и не запускает команды.
Theme/style обновляются без rebuild; dynamic roles пересчитываются при adopt/orphan.

| Этап | Критерий |
| --- | --- |
| UIA1a | Typed contracts/root/part/roles, precedence/order/replace/remove/scope/guards с direct tests |
| UIA1b | Theme switch и dynamic transfer без утраты tree/input/selection/scroll/bindings; failed value atomicity |
| UIA1c | EditorViewModel/Style и Fluent View через DI; notifications/commands/ownership tested без renderer |
| UIA1d | Debug/Release suites, native Editor/published proof, fresh packaged API consumer и docs/agent rules |

UIA1a–d выполнены2026-10-22 в указанном scope: UI661/Editor49, full2321/2321
Debug/Release, nonincremental Debug build, native Debug/published Release и fresh
14packages/4consumer proof. Датированные команды/ограничения — в HISTORY/STATUS.

Полный CSS cascade, selector language, implicit DataContext rebinding, templates,
animations и массовая migration consumers сохраняют собственные будущие задачи.
Фактические passing результаты отдельно в STATUS/HISTORY; UIA1 не закрывает
physical DPI/K3/K7/K8/VRAM/final alpha или ENG7 property editing.

Решение2026-10-22: [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory)
добавляет Alloy→Contracts для IUiSessionFactory : IFactory<UiSession,UiSessionCreation>.
Существующий четырёхаргументный Create сохранён через default interface bridge.
Hosting предоставляет UsePresentationFactory<TView>: injected root factory вызывается
на UI owner thread, окно владеет root. Borrowed session factory/lifecycle facades
не дублируют provider disposal owned window/dispatcher/session registry. Renderer,
targets и UI thread affinity прежние; конкретного DI provider в Alloy нет.

По запросу пользователя результаты [аудита2026-10-17](HISTORY.md#аудит-инфраструктуры-alloy--2026-10-17)
включены в модульный roadmap как **INF0–INF7**. Первичное добавление было
документационным. По следующему запросу пользователя2026-10-18 выполнен bounded
INF0 и реализованы INF1/INF2; после минимального INF5 выполнены INF3/INF4.
INF7 сопровождает их assembly-level checks и independent native/package consumers.
INF6 и bounded integration runner INF7 выполнены2026-10-19; расширение INF5
остаётся запланированным. [Реализация/собственные проверки INF1](HISTORY.md#inf0inf1-повторяемый-cleanup-registry--2026-10-18)
отделены от исходных audit evidence; общая готовность UI этим не объявляется.

Цель: UI сохраняет отзывчивость под потоком обновлений, завершает свои ресурсы
после отказа cleanup и предоставляет приложению явное управление кадрами/закрытием.
Scope: Argentis/Alloy runtime, Hosting, renderer/platform adapters и их проверки.
Существующие пять слоёв, thread affinity, borrowed GPU ownership, net10.0 и xUnit
сохраняются. Core/Cobalt/process lifecycle не переносятся в UI.

Это дополнительный UI backlog. Общая engine точка продолжения — в [его STATUS](../engine/STATUS.md);
[улучшения RichTextEditor](RICH_TEXT_EDITOR_PLAN.md) ведутся отдельно и отложены.
INF не заменяет этапы1–5/K1–K9/PA1–PA10, не снимает physical DPI/K7/K8/VRAM
и не возобновляет остановленный поиск NVIDIA workaround. Конкретные новые API,
лимиты очередей и compatibility policy уточнять перед соответствующим этапом.

### Этапы INF и критерии завершения

| ID | Результат | Состояние | Зависимость и критерий завершения |
| --- | --- | --- | --- |
| INF0 | Сверка исходного состояния и границ | Выполнен2026-10-18 в bounded audit scope | Повторить два audit repro на выбранном source; описать registry/queue/frame/close contracts и зафиксировать собственный baseline. Existing tests/native results сохраняют свои даты и scope |
| INF1 | Повторяемый cleanup registry | Реализован2026-10-18; проверки в STATUS/HISTORY | После начала Stop новые Create запрещены; failed cleanup не скрывает оставшиеся owned sessions; повторный Stop/Dispose пытается завершить их cleanup, состояние завершения означает отсутствие owned sessions. Первичная ошибка и ошибки cleanup наблюдаемы |
| INF2 | Ограниченный dispatcher/posted work | Реализован2026-10-18; проверки в STATUS/HISTORY | После INF1: ограничить work за один Update, определить capacity/overflow policy и atomic accept/close; предоставить awaitable dispatch с cancellation/error semantics. При нагрузке очередь не растёт бесконтрольно, layout/render/input и соседние окна получают выполнение |
| INF3 | Управление запросами кадров | Реализован2026-10-18; scope/checks в STATUS/HISTORY | После INF2 и минимальных INF5 metrics: wake/request на pending work и invalidation, idle/continuous режимы и явные update/render/present phases. Clean UI не требует постоянной presentation; expose/resize/DPI/animations/external HUD корректно возобновляют работу |
| INF4 | Согласованное закрытие окна | Реализован2026-10-18; собственные проверки в STATUS/HISTORY | Поверх INF1/INF2: owner-safe асинхронный close request с cancel/save/drain, различение запроса закрытия и факта закрытия. Native close, API close и App Stop согласованы; фоновые работы завершаются до освобождения зависимых UI/native ресурсов |
| INF5 | Общая диагностика UI | Минимум реализован2026-10-18; расширение запланировано | Session/window IDs, lifecycle state, queue depth/high-water/processed work, measure/arrange/render/present counters/timings и bounded snapshots. Existing Vulkan resource counters переиспользовать; diagnostics opt-in, без unbounded history и зависимости core UI от конкретного telemetry provider |
| INF6 | Явные factories и совместимость интеграций | Реализован2026-10-19; собственные проверки в STATUS/HISTORY | Root factory overload устраняет необходимость скрытой specialization generic TView для Cobalt; Cobalt/DI сохраняются. Renderer/target/presenter compatibility описана явно вместо неявного выбора private bool flags; unsupported pair отвергается до native allocation |
| INF7 | Проверки реальных сборок и поставки | Реализован2026-10-19 в bounded runner scope; проверки в STATUS/HISTORY | Сопровождает INF1–INF6, не откладывается до конца: новые contract checks обращаются к Hosting/Platform через ProjectReference и portable fake seams; native/package consumers отдельно проверяют compiled assemblies, analyzer/native delivery и cleanup |

### INF1: registry shutdown после отказа

- Воспроизвести вызов factory.Dispose из posted action во время UiSession.Update:
  запрет disposal внутри Update сохраняется, но subsequent cleanup должен оставаться
  возможным. Проверить partial failure с несколькими sessions и повторные Stop/Dispose.
- Разделить запрет новых sessions, незавершённый cleanup и окончательное завершение;
  конкретная форма состояний определяется в реализации. Не откатывать запрет Create
  после начала shutdown и не объявлять живую session освобождённой.
- Сохранить проверки owner thread, tree mutation/reentrancy и borrowed frame.
  После возврата lease/выхода из Update явный retry освобождает оставшиеся sessions;
  ошибки одного объекта не отменяют попытки cleanup остальных.

Уточнение реализации2026-10-18: принятый Stop/Dispose закрывает Create до любых
preflight checks; preflight failure сохраняет sessions для owner retry. Все
session guards проверяются до cleanup, затем ошибки отдельных Dispose собираются
без прекращения остальных попыток. Реентрантный cleanup возвращается к внешней
попытке. Stop из backend/attachment callback во время создания session отклоняется
до принятия shutdown request: неуспешный Create не забирает ownership root.
StopAsync остаётся синхронным owner-thread cleanup без прерывания посередине;
новый async close относится к INF4. TrueMoon.Alloy.Hosting.Tests использует реальный
ProjectReference на Hosting, без Compile-linked production copies, и включён в
TrueMoon.slnx. Новых native ABI/dependencies/public signatures не введено.

### INF2: queue и dispatch contract

Решение2026-10-18: `UiDispatchOptions` задаёт immutable `Capacity=1024` и
`ActionsPerUpdate=64`, оба строго положительные. Прежние constructor/Create
signatures сохранены; новые overloads принимают options после clipboard.
Hosted factories используют defaults; отдельная Hosting configuration здесь не
добавляется. Action budget ограничивает число начатых queued callbacks, включая
новые self-posts в этом Update, но не время одного callback. Свободное место
появляется при извлечении, отмене или закрытии; уже выполняемый callback не pending.

`Post(Action)` принимает FIFO work или синхронно бросает InvalidOperationException
при overflow; callback error выходит из Update, оставшиеся commands доступны
следующему Update. `InvokeAsync(Action, CancellationToken)` с worker ставит ту же
FIFO работу; task сообщает rejection/exception/completion. На owner callback
выполняется сразу, обходя очередь/capacity/budget, в том числе из Update: ожидание
своего вызова не требует следующего кадра. Это не async delegate API, task
continuations не привязываются к owner; блокировать owner на task, ранее поставленном
worker, нельзя. Pending task continuations выполняются асинхронно.

Cancellation до извлечения удаляет команду и освобождает capacity, после начала
callback не прерывает его и не меняет результат. Отменённый token на живой сессии
не занимает место даже при полной очереди. Accept/cancel/close сериализованы gate.
После успешных Dispose preflight checks очередь закрыта до owned cleanup;
ожидающие tasks fault с ObjectDisposedException. Failed guard сохраняет очередь.
Terminal rendering failure закрывает её с InvalidOperationException и первым
UiRenderingException в InnerException; сам callback, бросивший UiRenderingException,
faults свой task этой ошибкой и переводит session в terminal state. Обычная callback
ошибка InvokeAsync fault только task, не останавливая следующие commands.

Coalescing применяется явно только к latest-value PropertyBinding per attachment:
одна pending synchronization читает актуальный source, rejected enqueue сбрасывает
pending guard. CollectionBinding и attachment-generation guards сохранены.
Произвольные Action не объединяются. [Проверки INF2](HISTORY.md#inf2-ограниченный-dispatcher--2026-10-18).

Исходные acceptance требования сохраняются:

- Выбрать измеримый action budget или time slice и отдельно предел pending work.
  Self-posting/continuous producer не должен удерживать один Update бесконечно.
  Для очереди задать явную реакцию на overflow; произвольные commands/input cleanup
  не отбрасывать молча. Сохранить FIFO для принятых обычных commands.
- Awaitable dispatch должен сообщать completion/exception/cancellation, не допускать
  synchronous wait deadlock на UI owner и иметь определённое поведение при shutdown.
  Post versus Dispose/fault проверять как race принятия работы и закрытия очереди.
- Coalescing — явная политика для подходящих обновлений, например latest property
  synchronization; не применять её ко всем Action. CollectionBinding уже coalesces:
  сохранить его semantics и attachment-generation guards, учесть PropertyBinding.
- Проверить flood, очередь на границе capacity, отмену до/после принятия, callback
  error, detach/dispose и fairness нескольких окон. Это behavioral acceptance,
  а не обещание конкретного CPU/GPU ускорения.

### INF3–INF6: условия интеграции

INF3 — принято2026-10-18: runtime `UiFrameMode` нейтрален к renderer/platform.
`SilkUiWindowOptions.FrameMode=Automatic` сохраняет continuous polling при наличии
FramePresented observers и работает on-demand без них. Явный OnDemand не включает
polling из-за observers; Continuous задаёт его независимо от observers. Existing
consumers не теряют heartbeat. Engine viewport явно выбирает Continuous для
receiver polling и RequestFrame после dynamic pixel upload; event-based transport
notifications в engine этим шагом не добавляются.

`FramesPerSecond=60`, допустим1–240: лимит scheduled frame attempts на монотонном
TimeProvider. Native refresh и input geometry update могут выполняться сразу.
`UiSession.RequestFrame()` и `HostedUiWindow.RequestFrame()` thread-safe/coalesced,
не занимают command capacity. Session request означает redraw на следующем Update;
window request допускается до startup, отклоняется после Close. Request во время
draw сохраняется до следующего кадра. Invalidation/accepted Post/InvokeAsync
сигнализируют private host wake; произвольные user callbacks под queue gate не
выполняются. Close/fault закрывает запросы и снимает signal, failed Dispose guards
сохраняют live session. External host по-прежнему владеет своим loop/Update/output.

OnDemand разделяет Update, draw и output: clean state не вызывает Update/present,
native expose показывает last output без нового draw. Native resize/DPI/restore
применяются до output. Minimized viewport считается empty; posted work при
suspension обрабатывается, visual dirty/redraw сохраняются до resume. Skipped
presentation повторяется с frame deadline, ошибка сохраняется без замены причины.
Private UiFramePump имеет injectable TimeProvider для deterministic net10/xUnit
tests; real Hosting assembly используется через ProjectReference.

Shared owner выбирает minimum participant wait и будится на работу/close. Live
windows имеют bounded OS-event polling до16ms; idle CPU/GPU reduction без замеров
не объявляется. Startup batch32 и native commands64/iteration сохраняют fairness,
session callbacks ограничены INF2 budget. `SilkWindowHost.Pump()`/Run сохраняют
periodic semantics; overload `Pump(render:false)` выключает periodic DoRender,
сохраняя native refresh. Новые resize/state callbacks активны только в manual loop.

Уточнение output contract по native finding: `NeedsUpdate` включает pending FIFO/
future frame requests, `NeedsRender` — unapplied visual dirty текущего дерева/
viewport. Raster/OpenGL/Vulkan output guards используют NeedsRender. Пока queue
ещё ожидает следующего bounded Update, last completed image допустим для output;
прямое изменение tree/viewport по-прежнему запрещает export до Update. Export не
выполняет очередь и не обещает применение ещё ожидающих model commands. Это явное
расширение прежнего guard, необходимое для presentation при bounded queue/flood.
[Собственные regressions/native/package/golden evidence](HISTORY.md#inf3-запросы-кадров-и-scheduling--2026-10-18).

INF4 — решение2026-10-18: `HostedUiWindow.RequestCloseAsync(token)` — cancellable
user request (`Application`), native close — тот же путь с reason `Native`.
`Closing` содержит sequential `Func<UiCloseReason, Task<bool>>` handlers: snapshot
на попытку, начало каждого на owner, async continuation без UI affinity. False
прекращает user attempt; error/canceled handler оставляет окно открытым, faults
request, сохраняется в `CloseError`, включая native request. Retry сбрасывает error.
Concurrent requests делят попытку. Caller cancellation отменяет только его wait;
pre-canceled request не принимается. Never-started close не вызывает handlers.

`Close`/StopAsync/dispatcher App Stop остаются forced: upgrade pending attempt
не повторяет начатые handlers, игнорирует veto, остальные получают `Shutdown`.
Save/drain errors собираются до cleanup; одна ошибка не пропускает остальные
handlers. Owner продолжает обслуживать UI/другие окна, bounded handler batch64.
Native, handler и cleanup errors сохраняются вместе. Terminal native/graphics
failure может лишить drain возможности обращаться к UI, но не разрешает освобождать
зависимые ресурсы до уже начатого drain. Таймаут/abort чужих tasks не вводится.

`IsCloseRequested` — pending attempt; true result RequestCloseAsync и `IsClosed`
означают успешное завершение `Completion` после owner-thread native/GPU/UI cleanup.
Faulted cleanup не объявляется successful closed. Legacy constructor `closed`
сохраняет lifetime callback после попытки cleanup, включая failure, и явно не
является success notification. Внутри Closing нельзя ждать собственный close/
Completion/window или dispatcher StopAsync: cleanup зависит от возврата handler.
После await UI mutations возвращаются через InvokeAsync/PostWindow. Producers
останавливаются до ожидания принятых controller/compiler/game tasks. External HUD
сохраняет собственный owner lifecycle; engine viewport использует этот close API.

INF6 — решение2026-10-19: новый non-generic
`UsePresentation(Func<IServiceResolver, Element>, Action<AlloyOptions>)` получает
root на owner thread после готовности target, не регистрирует/не resolve-ит view.
Cobalt и Microsoft DI сохраняются. Полностью factory-based dependency graph не
требует generated view resolver; generic overload сохраняется с explicit consumer
graph/analyzer для Cobalt. Factory возвращает fresh detached root без Parent.
Null/disposed/parented/attached root отвергается; чужое дерево не disposing/detaching.
Допустимый returned root принадлежит окну, в том числе при subsequent startup failure;
partial objects, которые factory не вернула, остаются ответственностью factory.
Startup/Completion сохраняют причину ошибки. Dependencies живут до UI cleanup.

`UiRendererKind` вместо двух private selection flags описывает None/Custom/Raster/
OpenGL/Vulkan. `SilkUiWindowOptions.GraphicsApi` явно выбирает target/presenter;
null выводится из выбранного GPU renderer при UsePresentation, direct window
сохраняет default Vulkan. Explicit mismatch отвергается до регистрации services.
Настройки renderer/window копируются при регистрации до Build/service resolution;
appearance/chrome immutable, callback delegates/captures не deep-clone.

Известная matrix: Raster→RasterUiTarget только external; OpenGL→OpenGLUiTarget +
OpenGL presenter; Vulkan→VulkanUiTarget + Vulkan presenter. Target guards built-in
backends отвергают mismatch до native surface/attachment. `IUiWindowSessionFactory`
декларирует pure SupportsWindowGraphics до native window allocation; real
HostedUiSessionFactory предоставляет metadata известных Skia GPU pairs. Raster и
unknown custom backend standalone не заявляются поддержанными; UseRenderer остаётся
external-only. Для custom standalone factory одного IUiSessionFactory теперь
недостаточно: реализовать новый explicit capability contract. Это намеренное
ужесточение alpha preflight, existing signatures/generic path не удаляются.
Custom declaration не доказывается пробным native allocation. External factories,
five layers и borrowed GPU ownership сохраняются. Публичный Cobalt/DI consumer —
[AlloyFactoryConsumer](../../ManualTests/AlloyFactoryConsumer/README.md).

Минимальный INF5 — решение2026-10-18: `UiSession.EnableDiagnostics(long? windowId)`
включает observation с текущего момента. `Id` session/window стабилен в пределах
процесса, одинаковые числовые IDs разных domains не сравниваются без типа.
`SilkUiWindowOptions.EnableDiagnostics` включает session collector до первого
Update; `HostedUiWindow.Diagnostics` сохраняет его после cleanup. Existing
constructor/Create signatures и dependency graph не меняются.

`UiDiagnostics.Snapshot` — согласованная value copy под collector lock, доступная
с любого потока и после disposal. Lifecycle Active/Suspended/Faulted/Disposing/
Disposed, accepted viewport и nullable classification первого rendering failure
не требуют хранения exception/tree/device/callback references. Suspended означает
zero-size viewport; Idle отдельным lifecycle состоянием здесь не вводится.

Queue имеет InitialPending/Pending/HighWater и Accepted/Started/Completed/Failed/
Canceled/Rejected/Discarded с согласованным accept/cancel/close gate. InitialPending
отдельно от новых Accepted; owner-inline InvokeAsync и pre-canceled requests в FIFO
не входят. Capacity/closed rejections наблюдаемы. Команда, уже выполнявшаяся при
Enable, и её partial Update не записываются как новые операции.

Update/root measure/root arrange/surface render/host present учитываются раздельно:
attempts/completed/failed и total/last/max CPU wall time; present имеет skipped.
Clean/suspended Update — completed Update без фиктивных layout/render/present.
Root invocations не означают обход всех cached descendants. Update включает
вложенные фазы, totals не складываются. Present измеряется вокруг output, после
Update и до FramePresented observers; Vulkan false result — skipped. External
host может вызвать `RecordPresentation(elapsed, outcome)` на collector. Это не
presentation command и не GPU completion/timestamps. Vulkan resource counters
сохраняют существующий `VulkanWindowPresenter.Resources`, не дублируются.

Хранятся фиксированные скаляры и пять агрегатов фаз; ring/history/samples,
telemetry provider/events и reset/disable API не добавляются. Выключенный путь
не читает Stopwatch и не создаёт collector; остаются process IDs, cached host
delegate и null checks. Включённый путь добавляет collector locks, timestamps и
копирование фиксированных value counters; overhead проверяется отдельно на
headless idle/posted workload. CPU/GPU ускорение и idle policy не обещаются.
[Собственные проверки/измерения](HISTORY.md#inf5-минимальная-диагностика--2026-10-18).

Остаток полного INF5: window lifecycle до session creation/после failed startup,
Hosting command-queue metrics, общая resource/diagnostic projection и exporters/
нагрузочные contention measurements при конкретной потребности. Это не блокирует
bounded INF3 с уже доступными session/phase/queue observations. Performance budgets,
GPU timing/VRAM и аппаратная alpha matrix остаются самостоятельными acceptance.

- Frame scheduling вводить после baseline/минимальных diagnostics. Нужны explicit
  frame requests для dynamic image/engine viewport и отдельная возможность polling
  или continuous rendering: текущие FramePresented consumers не должны потерять
  источник следующих кадров. Показ неизменённого содержимого после expose/restore,
  resize/DPI и suspended zero-size проверять отдельно от dirty redraw.
- Close API не блокирует owner thread ожиданием controller/compiler/game tasks.
  Проверить отказ пользователя от закрытия, failed save/drain, повторные/concurrent
  close requests и cleanup ошибки; ошибку не маскировать успешным Closed событием.
  Семантику native close и принудительного App Stop зафиксировать раздельно.
- Metrics должны различать update/layout/render/present и suspended/failed state.
  Зафиксировать overhead diagnostics и bounded storage; idle CPU/GPU выводы делать
  только по замерам на указанной конфигурации, не по уменьшению числа callbacks.
- Factories/capabilities не требуют смены DI provider или удаления нынешних явных
  renderer overloads. Для нового root factory нужны Cobalt/DI source и independent
  package checks; для presenter/target — positive/negative compatibility scenarios.

### INF7: общая проверка изменений

Для каждого этапа — собственные xUnit regressions на net10.0 с assertions поведения,
отказа и cleanup; при изменении platform/Hosting контракта тестировать реальную
production assembly, не только Compile-linked copy исходника. Portable fake surface/
window seam не выдаётся за native proof. Existing linked tests не удалять массово:
перенос обосновывать конкретным contract и сохранением проверяемого поведения.

Relevant native сценарии выполнять bounded, с cleanup только owned процессов и
фактическими resource counters. Изменение scheduling/close требует standalone и
external HUD, нескольких окон, resize/suspend/resume, Debug apphost/published Release
в supported scope. Изменение public/package API требует independent consumer;
rendering/text изменения — existing read-only golden checks без подгонки эталонов.
Полный UI/native/performance pass и закрытие hardware blockers остаются отдельными
acceptance, historical results не переносить на новый source автоматически.

### Последующие исследования

INF7 — решение2026-10-19: tracked RunIntegration.ps1 и Integration helpers в
[AlloyFactoryConsumer](../../ManualTests/AlloyFactoryConsumer/README.md#inf7-воспроизводимый-integration-runner)
предоставляют Assembly/Source/Package/All profiles. Assembly — real Hosting и UI
suites Debug/Release; Source — factory/generic/failure consumers и native
scheduling/close/dispatcher/HUD; Package — fresh graph/isolated cache,8factory
consumers без analyzer/view registration и проверка обязательных analyzer/native
payloads. Все selected phases/report matrices должны завершиться; zero/failed/
skipped discovery не passing. Native validation обязательна и process-local.
Timeout завершает только owned process tree; logs/partial manifest сохраняются.

Fresh run directory не перезаписывается. Manifest фиксирует scope, SDK, commands,
reports, input set/source/binary/package hashes. Отдельный read-only verifier
повторяет completeness/provenance checks, не обновляя evidence. Tracked guards
проверяют quoting/UTF-8, failure/timeout/ownership, prerequisite rejection, TRX,
partial matrix, source/artifact drift, source package dependency и missing payload.
Временные scripts в TestResults не являются зависимостью runner. Producer
bin/obj общие: один runner на checkout, source/binary drift отвергается.
Existing linked checks не удаляются массово; при следующих contract changes
переносить targeted проверки на actual assembly с сохранением поведения.
Runner не объявляет новый full-solution/golden/RTE/engine/performance/hardware pass.

После diagnostics и конкретного workload рассмотреть scoped image/font resources,
асинхронную загрузку и reuse dynamic image storage с явным ownership. Выигрыш против
нынешней owned pixel copy подтвердить замерами; shared mapped/GPU pointers не
вводить как неявное ускорение. Dirty-subtree layout/virtualization — после baseline
large-tree сценария и проверки measure/arrange invalidation.

Для старых PropertiesBase/IView/Hosting.Compatibility описать поддерживаемую границу
и путь миграции. Пометка legacy/obsolete, перенос либо удаление требуют отдельного
compatibility решения и consumer checks; этот план сам по себе их не удаляет.

INF0–INF4/INF6/INF7 и минимальный INF5 выполнены в указанном scope. Bounded
инфраструктурная последовательность завершена; следующий общий шаг — **ENG3**.
Полный INF5 остаётся частичным и расширяется при конкретном consumer/workload;
linked checks переносить по конкретному contract, без массового удаления.
номера INF не задают новую глобальную очередь вместо engine/RTE планов.

## PA7a: RichTextEditor перед ENG1 — 2026-10-17

Дальнейшие улучшения вынесены в отдельный
[план RichTextEditor](RICH_TEXT_EDITOR_PLAN.md). По решению пользователя2026-10-17
они отложены до дальнейшей работы над функциями UI-модуля. Подробные задачи и
acceptance ведутся только там. Назначенный после PA7a ENG1 выполнен2026-10-17;
актуальный общий шаг — ENG3, [engine статус](../engine/STATUS.md).

По прямому запросу пользователя перед ENG1 реализуется RichTextEditor поверх
Topten.RichTextKit. Это отдельное ограниченное расширение текста, а не начало
всей beta/PA1–PA10 или закрытие PA7/alpha. net10.0/xUnit и независимость Argentis
от Skia/Alloy сохраняются. [API/options/границы](RICH_TEXT_EDITOR.md).

Контракт: neutral immutable rich document/styles/options и retained control в
Argentis; RichTextKit0.6.170 shaping/layout/hit testing/painting только в
Rendering.Skia. Common adapter используется raster/OpenGL/Vulkan. Поддержать
multiline input, UTF-16/grapheme selection/navigation, plain clipboard,
formatting selection/insertion, bounded undo/redo, read-only/word-wrap/alignment,
общие Element font/color/padding и независимый toolbar example.

Acceptance: behavioral xUnit regressions editing/binding/format/history/input/
cleanup и реальные raster pixels; Debug/published Release native smoke3backends;
fresh independent package consumer с rich editor и direct renderer; прежние
K2/K4 golden fixtures/tolerances не перезаписывать. Code/test/native/package
results фиксировать отдельно. IME/accessibility, bidi caret affinity, rich clipboard,
RTF/HTML import/export, lists/tables и long native resource trend вне этого scope.
RichTextKit process shaper caches не объявлять очищенными при session Dispose.

Результат2026-10-17: компонент реализован; UI563/563 Debug/Release,6native demo
cases,40fresh package processes и прежний read-only Settings golden360 прошли.
[Точные команды, failures и границы вывода](HISTORY.md#richtexteditor--2026-10-17).

После этого UI запроса выполнены headless ENG1 и ENG2 CPU viewport;
актуальная точка продолжения — ENG3. Само расширение PA7a engine code не создавало.
NVIDIA K7/physical DPI/loss/VRAM/final alpha сохраняются.

## Проход качества UI после headless модулей — 2026-10-15

По [общей сводке качества](../QUALITY.md) следующий ограниченный scope — обновить
5.4/5.5 independent package consumer после bundled Cobalt и explicit font catalog.
Это уточнение очередности quality работы, не изменение архитектуры, alpha criteria,
net10.0/xUnit, K1–K9/PA1–PA10 или решения пользователя остановить NVIDIA workaround search.

Исходная сверка: существующий AlloyPackageConsumer использует только UseDI и
backend overloads без explicit fonts. Предыдущие package16 и font golden360
имеют отдельные runs2026-10-11; совместного нового proof нет. Runtime failure
в этом quality шаге ещё не воспроизводился.

Acceptance следующего code этапа:

- Переиспользовать existing consumer: Cobalt и Microsoft DI как независимые варианты,
  общий View/model и собственный TrueMoon registration API. Не переводить остальные
  UI applications массово на Cobalt; adapter path и provider selectors сохранить.
- Fresh unique local packages/feed/cache, независимые consumer build/CPM settings,
  только PackageReference, Debug/Release ×build/publish, Windows x64/net10.0,
  framework-dependent без RID. Producer graph учитывает IsPackable=false generator;
  matching bundled analyzer Cobalt реально участвует в compilation, его DLL не
  попадает в runtime output. Вариант Microsoft DI с отключённым Cobalt analyzer
  подтверждает, что consumer не зависит от generated Cobalt graph.
- Сохранить Raster/OpenGL/Vulkan Hosting smoke и direct renderer без Hosting/App.
  Подключить существующий SkiaFontCatalog с лицензированными фиксированными fonts;
  убедиться в фактической доставке fonts/license/native DLL, matching source/package/
  compiler/generated provenance и достижимости новых overloads из пакетов.
- Проверять реальный render/update и bounded lifecycle: startup, model/binding
  updates, отсутствие owned sessions/subscriptions после Stop/Dispose, правильный
  native LoadedPath. Контракт владения каждого provider сохраняется, alias disposal
  counts не объявляются одинаковыми без проверки. Не заменять checks одним build.
- Сохранить failure logs, case deadlines, cleanup только own launched processes,
  fresh output directories и итоговую проверку source/nupkg/generated/native/font
  fingerprints. Точные commands и actual case counts записать после выполнения;
  Completed требует всей заявленной matrix, negative control учитывается отдельно.
- Выполнить текущую UI xUnit suite и relevant Core/Cobalt regressions по фактически
  затронутому коду. При изменении fonts/native/renderer read-only K2/K4 golden
  повторяется с прежними fixtures/tolerances; эталоны не перезаписывать ради passing.

Runtime fixes вносить только при воспроизводимом defect. Этот пакетный proof не
объявляет снятым K7, physical DPI/loss/VRAM или final alpha, не меняет driver/GPU/
Windows settings и не начинает engine. После него обновить сводку качества и
выбрать следующий доступный UI/ENG0 scope с явно сохранёнными ограничениями.

**Результат2026-10-15:** scope выполнен:10fresh nupkgs,36App +4direct processes,
Cobalt/DI и DI без analyzer, explicit fonts/native delivery, fingerprints и
negative analyzer control. Воспроизведён missing resolver HostedUiDispatcher;
регистрация заменена явной factory без зависимости Hosting от Cobalt.
Consumer явно регистрирует concrete View/model graph для source generator.
UI532/Core107/Cobalt179/generator91 отдельно Debug/Release; read-only golden360
с validation teardown прошли. [Команды, failures и границы](HISTORY.md#ui-packagehosting-quality--2026-10-15).
Архитектура и критерии alpha сохранены. Следующим решением пользователя2026-10-15
выбран [ограниченный ENG0](../engine/PLAN.md#eng0-принятый-scope-первого-прототипа--2026-10-15)
с открытыми UI рисками; тогда назначен headless ENG1 (выполнен2026-10-17). Это допуск
прототипа, не K3/K7/K8/VRAM/final alpha pass и не автоматическое начало PA1–PA10.

## Цель и основные решения

Создать гибкий UI-фреймворк с Fluent API на C# и сменяемыми бекендами, пригодный для самостоятельных Windows-приложений и встраивания в качестве GUI/HUD в Vulkan-приложение. Представления и пользовательские элементы описываются в C# классах.

Первая версия должна демонстрировать два сценария:

1. Небольшое самостоятельное приложение с динамическим содержимым.
2. Интерактивный HUD поверх графической сцены.

В обоих сценариях используются одинаковые контролы, layout, состояние и механизм обновления UI. Различаются владение окном и графическими ресурсами, а также организация цикла приложения. HUD получает размер поверхности, ввод и запрос на отрисовку от игры; собственное окно и цикл приложения ему не требуются.

Основная модель — постоянное дерево элементов с изменяемыми свойствами, явными привязками данных и заменяемыми поддеревьями. Точечные изменения включают обновление свойств, замену содержимого контейнера, добавление и удаление элементов коллекции. Неизменённые экземпляры сохраняют фокус, выделение и прокрутку.

Fluent-вызовы создают и настраивают элементы. Для динамических областей используются наблюдаемые коллекции и явная замена содержимого. Полное сопоставление заново построенных деревьев откладывается до появления такой потребности.

Согласовано с пользователем 2026-10-06: контролы можно создавать как публичным конструктором (`new Button(...)`, `new Text(...)`), так и статической фабрикой на самом типе (`Button.Simple(...)`, `Text.Plain(...)`). Оба способа равноправны и могут смешиваться в одной Fluent-разметке. Фабрика возвращает новый экземпляр конкретного контрола для дальнейшей настройки существующими Fluent-методами. Фабрики **4.8** реализованы для существующих типов 2026-10-07; исходное решение сохраняется. [Синтаксис и пример](../../TrueMoon.Alloy.Hosting/README.md#создание-контролов-и-fluent-разметка).

Основной графический стек — SkiaSharp поверх Vulkan. Возможность создания контекста через `GRContext.CreateVulkan` уже исследовалась в прототипе. Пригодность конкретных пакетов, работа поверх внешнего GPU-хоста, создание устройства, swapchain и синхронизация должны быть проверены первым графическим этапом.

## Архитектура и публичные границы

Сохранить Argentis и Alloy; конкретные интеграции вынести в отдельные сборки.

| Модуль | Ответственность |
| --- | --- |
| `TrueMoon.Argentis` | Контролы, композиция, свойства, стили, Fluent API и независимые контракты layout/рисования |
| `TrueMoon.Alloy` | Выполнение UI: обход дерева, layout, обновления, маршрутизация ввода, фокус и жизненный цикл |
| `TrueMoon.Alloy.Rendering.Skia` | Отрисовка и измерение текста через Skia; поверхности Vulkan, OpenGL и raster |
| `TrueMoon.Alloy.Platform.Silk` | Окна, события ОС и ввод через Silk.NET |
| `TrueMoon.Alloy.Hosting` | Интеграция с `App.Builder`, DI, жизненным циклом TrueMoon и связывание выбранного renderer с оконной платформой |

Уточнение имени по запросу пользователя, 2026-10-04: hosting-проект, каталог, сборка и namespace называются `TrueMoon.Alloy.Hosting`. Повторный суффикс `TrueMoon` удалён; ответственность модуля и API подключения сохраняются.

Уточнение поставки native Skia, 2026-10-04: проверенная DLL с Vulkan interop хранится в существующем `TrueMoon.Alloy.Rendering.Skia/NativeAssets/win-x64` вместе с revisions/hash. Renderer копирует её при build/publish в отдельный `truemoon-native/win-x64`, чтобы избежать конфликта со штатными native assets. Публичный SkiaNativeLibrary загружает один экземпляр DLL для обоих bindings. Отдельного native-проекта не создаётся. Инструкция пересборки: [README renderer](../../TrueMoon.Alloy.Rendering.Skia/README.md).

Обновление2026-10-28 по запросу пользователя: managed/native пара **4.154.0**
собрана и проверена в source Debug/published Release Vulkan interop/backend scope.
ABI v1/48-byte descriptor и sole ownership сохраняются; official version gate
и extension exports обязательны. Новые pins и условный depot_tools учитывают
изменения upstream build recipe. [Текущие результаты и границы](HISTORY.md#skiasharp-41540-и-vulkan-interop--2026-10-28).
Этот quality проход не закрывает independent NuGet ARCH2e, GUI/hardware/K7/VRAM.

Argentis не зависит от Alloy, SkiaSharp или Silk.NET. Реализация поведения и визуального представления контрола находится рядом с контролом и использует независимые контракты; Alloy организует их выполнение.

Основные точки расширения:

- `UiSession` — корневое представление, обработка ввода, обновление, layout, рендеринг и освобождение ресурсов.
- `IWindowHost` — окно, размер framebuffer, DPI, платформенные события и сервисы.
- `IRenderBackend` — создание графической сессии и вывод UI на поверхность.
- `IDrawingContext` и `ITextLayoutService` — геометрия, clipping, трансформации, изображения и текст без типов Skia.
- Отдельный Vulkan-контракт подключения внешнего устройства и обмена GPU-ресурсами. Vulkan-типы остаются внутри соответствующей интеграции.

Существующий `UsePresentation<TView>()` сохранить как удобную точку запуска с явным выбором платформы и рендерера. Для HUD предоставить создание `UiSession` без окна, DI-контейнера и собственного цикла приложения.

Уточнение подключения, согласованное и реализованное 2026-10-04: `Hosting.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost())` регистрирует renderer и `IUiSessionFactory` без окна/устройства. `UsePresentation<TView>(options => options.UseSkiaVulkan().UseSilkWindow(...))` запускает Argentis Element на выделенном UI-потоке и освобождает ресурсы через App lifecycle. Прямой `UiSession.Create` доступен без App. Выбор backend/хоста обязателен. После 2.1b (2026-10-05) старая перегрузка без настроек находится только в `Hosting.Compatibility` как opt-in legacy OpenGL API; из общего Alloy Skia/Silk/TrueMoon dependencies удалены.

Vulkan surface/lease и публичный `SkiaNativeLibrary` перенесены в Rendering.Skia. `VulkanUiTarget` принимает raw-handle `VulkanHostContext`; после разделения 2.1a Silk convenience target создаётся через `Hosting.SilkVulkanHost.CreateTarget(device)`. GPU обмен пока последовательный с ожиданием completion; контракта нескольких frames-in-flight ещё нет. [Примеры подключения и владение](../../TrueMoon.Alloy.Hosting/README.md).

Уточнение raw-handle подключения, 2026-10-04: host передаёт instance/physical device/device/graphics queue/family, запрошенную API version >=1.1, enabled extensions и указатели enabled features/features2/pNext вместе с собственным loader и проверяемым DeviceWaitIdle callback. Descriptor не владеет native handles; features memory/loader/callback targets живут до закрытия всех UI-сессий. Skia owns только свои context/surfaces/textures. Protected content, другие queues и frames-in-flight остаются за пределами последовательного режима. Первоначальные convenience overloads и Silk window presenter были сохранены в renderer; в 2.1a (2026-10-05) они заменены factories в Hosting, presenter/counters перенесены туда же, а dependency Rendering.Skia → Platform.Silk удалена. Это реализация предусмотренного внешнего Vulkan-контракта 1.5/2.2, цели и критерии alpha сохраняются.

Уточнение presentation, 2026-10-04: игровой хост композитит retained HUD со сценой на GPU, возвращает UI lease после sampling и передаёт готовую scene texture в `VulkanWindowPresenter.PresentImage`. Presenter заимствует источник и сообщает его фактический layout после завершённой GPU-работы; источник остаётся собственностью игры. `Present(UiSession)` сохраняется для standalone UI. Это реализация исходных целей 1.2–1.6/5.2–5.3; цели, архитектурные границы и критерии K1–K9 не меняются. [Оконный пример и автоматический сценарий](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение retirement, 2026-10-04: window device согласует KHR/EXT swapchain_maintenance1 и feature с необходимыми instance dependencies. Presenter использует present fences для reuse и уничтожения swapchain generations. Legacy fallback сохраняет поколение до подтверждённой reacquisition показанного image нового swapchain; shutdown без extension сохраняет ограничение WaitIdle базового Vulkan. Это не основание объявлять WaitIdle доказательством завершения presentation. K7 проверяется отдельным --retirement-soak с ownership counters, registry/subscriptions и WeakReference; счётчики handles не заменяют VRAM/driver profiling. Цели и критерии alpha не меняются. [Алгоритм, ограничения и команды](../../ManualTests/AlloyVulkanTest/README.md).

Уточнение raster, 2026-10-04: `SkiaRasterRenderBackend`/`RasterUiTarget` используют общий UiSession и Skia drawing/text adapter на CPU без window/device. RGBA premultiplied snapshot экспортируется как caller-owned immutable SKImage, который живёт независимо от session/resize и не требует lease Return. Native Skia Windows x64 сохраняется; ABI Vulkan не требуется на raster-пути. Выбор — `UseAlloy(...UseSkiaRaster().UseExternalHost())` или direct Create. На момент raster шага standalone был Vulkan-only, comparison охватывал raster/Vulkan; после OpenGL шага standalone поддерживает оба GPU backend, а comparison — все три. Selected-pixel/layout/input проверки не заменяют полное font golden сравнение K2/K4.

Уточнение OpenGL, 2026-10-04: второй явно выбираемый GPU renderer — `SkiaOpenGLRenderBackend` с borrowed `OpenGLUiTarget` (host loader/current-context guard). Общие UiSession/Argentis/Skia drawing/text сохраняются; GPU retained frame композитится в host-owned RGBA8 FBO через PresentOpenGL. Host owns context/FBO/window, сериализует работу и восстанавливает своё GL state после Skia. Cross-context texture lease/async handoff не включаются в эту миграцию. `UseSkiaOpenGL` работает с external host и standalone Silk presentation; последний освобождает Skia в Closing до уничтожения GL context. Managed callback errors переходят в Completion/StopAsync после закрытия, без unwind через GLFW. Одинаковый view проверяется на raster/OpenGL/Vulkan; legacy no-options Visual API остаётся compatibility path без экспериментального drawing/CreateVulkan/empty catch. Это завершает миграцию пути 2.5, но не разделение зависимостей 2.1; цели и K1–K9 сохраняются. Предыдущее ограничение standalone Vulkan-only отменено.

## Последовательность реализации

### Актуальная точка продолжения — реализация DPI 2026-10-09

Реализованы функциональные подэтапы3.5a/b/c/d,4.8,5.1a/b/c и общая Settings-сцена raster/OpenGL/Vulkan/HUD, контракт2.4/K8 с controlled failure/recreate, K9 baseline/profile/reuse и Windows appearance/custom chrome/native Snap4.9b/c/d. 2026-10-09 добавлен native Windows DPI contract: Scale, логические размеры и нормализованный ввод. Фактически выполненные текущие build/tests/GPU/publish и границы проверки находятся в STATUS. Реализация не означает итоговую готовность alpha.

Физический следующий шаг остаётся **4.9d / K3: 100/200% и переходы между разными DPI**, с сохранением logical client size, focus/selection и повтором chrome/input/alpha. K7 WSI investigation2026-10-10 разделила queries/create и локализовала GDI trend до driver-owned screen DC внутри первого vkCreateSwapchain на новой surface/window (NVIDIA ICD32.0.16.1742); тот же raw control на AMD сохраняет GDI. Safe app-side fix не установлен; нужен supported NVIDIA fix либо отдельно согласованная и проверенная alternate GPU configuration, затем полный240-window check с прежними limits. Ближайший независимый code deliverable — **5.4–5.5 package/independent UI consumer**. Fixed-font/full goldenK2/K4, длительныйK7/VRAM, real lossK8 и оставшаяся4.9matrix сохраняются перед итоговым alpha проходом. Directed DPI не заменяет physical transitions; Name mismatch и прежние opacity/legacy capture failures сохраняются как отдельные риски.

Этот маршрут актуализирует более ранние датированные формулировки «следующий шаг» ниже; цели, архитектура, net10.0/xUnit и критерии готовности сохраняются. Точная область прежних проверок и открытые ограничения — [STATUS.md](STATUS.md).

**Точка продолжения после K2/K4 — 2026-10-11:** explicit fonts/full golden проверены на текущем Windows x64/NVIDIA в Debug/published Release,360полных сравнений, validation0/0 и UI532/532. Ближайшая независимая общая задача — CORE5/COB4 package/analyzer consumer по планам владельцев. UI сохраняет physical4.9/K3, внешний K7, real loss/K8/VRAM и полный итоговый alpha проход. [Контракт сравнения](GOLDEN_TESTS.md).

**Предыдущая точка продолжения2026-10-11 (package шаг):** подготовка package/independent consumer5.4–5.5 проверена на Windows x64/net10.0/framework-dependent Debug/Release build/publish без RID. Исправлена native transitive delivery,16consumer процессов прошли, package README есть. Следующий независимый этап — fixed-font goldenK2/K4. Внешний NVIDIA K7 блокер, physical DPI/4.9matrix, real loss/VRAM и итоговая alpha acceptance сохраняются; публикации alpha не было. [Точные результаты и ограничения](HISTORY.md#package-и-независимый-ui-consumer--2026-10-11).

### Контракт Windows DPI 4.9d / K3 — уточнение 2026-10-09

По запросу начать следующий этап alpha реализовано уточнение координат существующего Silk host. Размеры UI, параметры создания/Resize и custom-frame MinimumSize/MaximumSize задаются в логических 96-DPI единицах. На Windows UiViewport.Width/Height — физические framebuffer pixels, Scale — GetDpiForWindow(HWND)/96 независимо от framebuffer/client ratio. NativeWindow.Size/Position и raw Win32 messages остаются физическими координатами. Silk mouse position и native caption input преобразуются в логические координаты ровно один раз; wheel delta не масштабируется. Hosting перед input синхронизирует изменённый viewport и layout/region map.

Логический client resize округляется до ближайшего физического пикселя (середина от нуля); native minimum округляется вверх, maximum вниз по текущему HWND DPI, в том числе до первой публикации regions. Custom chrome отвергает snapshot с устаревшими размером или Scale и сбрасывает его при WM_DPICHANGED. WM_GETDPISCALEDSIZE вычисляет новый physical client из прежнего logical client, для обычного окна добавляет рамку через AdjustWindowRectExForDpi. Предложенный Windows rectangle при WM_DPICHANGED применяет GLFW; второго SetWindowPos в DPI hook нет.

Окно создаётся в scoped PER_MONITOR_AWARE_V2 context потока; прежний context восстанавливается. Viewport/Resize и оконный loop используют такой же scope для callers с другим DPI awareness. Новый адаптер не меняет process-wide DPI settings приложения. DPI hook владеет только subclass/GC root и borrowed HWND, снимается до уничтожения окна; managed ошибки не выходят через native callback. HUD с внешним host сохраняет прежний контракт: host задаёт UiViewport/Scale и нормализует ввод сам. Public API, renderer ownership, native Skia ABI, пакеты и net10.0/xUnit не меняются.

Это исправляет Windows поведение на high DPI: клиент600×400 логических единиц при150% получает900×600 физических пикселей вместо прежнего physical600×400/Scale1. K3 по-прежнему требует реальные100/150/200% и переходы; арифметические/headless/directed проверки записываются отдельно от физической matrix.

Основания реализации: [GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow), [WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged), [thread DPI context](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext), [GLFW Windows DPI handling](https://github.com/glfw/glfw/blob/3.4/src/win32_window.c).


### Контракт отказа графики 2.4/K8 — уточнение 2026-10-07

`UiRenderingException` обозначает подтверждённый terminal failure с backend/operation, original cause и причиной DeviceLost, ContextLost либо BackendFailure. Обычные ошибки DrawCore сохраняют dirty state и допускают повторный Update; они не объявляются потерей устройства. Vulkan ErrorDeviceLost классифицируется явно, abandoned Skia context сообщает ContextLost. Для внешнего presentation/engine host сообщает подтверждённый отказ через UiSession.ReportRenderingFailure.

Первый отказ сохраняется в RenderingFailure, уведомляет RenderingFailed один раз на owner thread, отменяет focus/capture/hover и pending posts. Update/Resize/input/Post/theme/export/presentation больше не допускаются. Повторный report не заменяет первый cause. VerifyAccess и VerifyAvailable проверяют lifetime/thread/lease, допускают cleanup faulted context; проверка GPU health выполняется отдельно. Dispose из notification/Update и с borrowed frame отвергается до разрушения дерева. Ошибки cancellation/observers/cleanup сохраняются вместе с исходным отказом, остальные безопасные cleanup действия выполняются. Cleanup errors могут образовывать вложенные AggregateException.

Host владеет model/device/window/queues, UI — своим деревом, подписками и Skia wrappers. Model сохраняется независимо от Dispose; после освобождения старой сессии создаются новые controls/session из той же модели. Transient focus/capture/selection/scroll автоматически не переносятся. При настоящей потере устройства host сначала освобождает старый UI, затем сам пересоздаёт device/context/target. Автоматическое бесшовное восстановление не включается.

Vulkan lease обычно возвращается после завершённой host GPU работы с actual layout. Только подтверждённый DeviceLost разрешает AbandonAfterDeviceLoss без GPU ожидания и state-return; Skia context abandon выполняется без backend calls. Borrowed Vulkan handles и loader остаются живы до окончания cleanup даже после abandon. При неизвестном отказе completion нельзя разрушать потенциально in-flight host ресурсы: standalone сохраняет device, если UI/presenter cleanup не завершился. Best-effort cleanup не является обещанием полного освобождения driver allocations при неизвестном состоянии графики. [Подробный контракт и пример](../../TrueMoon.Alloy.Hosting/README.md#отказ-графики-и-явное-пересоздание-24k8).

Воспроизводимый `--settings-failure --validation` проверяет controlled BackendFailure на реальных raster/OpenGL/Vulkan surfaces, borrowed-frame guard/normal Return, renderer result classification, hosting draw/external-report/creation failures и explicit recreation с прежней SettingsModel. Контролируемые сценарии учитываются отдельно от физического device/context loss; последний этим probe не индуцируется. Критерии K1–K9 и требования итогового alpha прохода сохраняются.

### Согласованная корректировка порядка работ — 2026-10-06

После оценки текущей реализации пользователь согласовал уточнение плана. Архитектура модулей, retained дерево, net10.0/xUnit, два сценария запуска и критерии K1–K9 сохраняются. Крупные пункты разделяются на проверяемые подэтапы; графический прототип и функциональные механизмы учитываются отдельно от итоговой готовности alpha. Исходные номера пунктов сохраняются для ссылок и исторических записей. Это изменение плана, не реализация перечисленных возможностей.

Ближайший результат — одно небольшое AlloyTest-приложение с редактируемой моделью, динамическим списком и сохранением состояния неизменённых элементов при изменении дерева. Пример развивается вместе с API и затем используется для проверки общего runtime и графических адаптеров.

| Порядок | Пункты плана | Проверяемый результат |
| --- | --- | --- |
| 1 | 3.5b / 3.6 | Общие двусторонние привязки: TextBox.Value, CheckBox.IsChecked и Slider.Value; пользовательское редактирование, нормализация модели, отсутствие feedback и корректный attachment lifetime. |
| 2 | 4.8 / 5.1a | Фабрики рядом с публичными конструкторами и первая форма AlloyTest с моделью; примеры обоих способов создания контролов. |
| 3 | 3.5c / 3.5d / 5.1b | Наблюдаемый список и условные области загрузка/ошибка/содержимое; корректное владение generated nodes и сохранение неизменённых экземпляров. |
| 4 | 4.1–4.7 / 5.1c / 5.2–5.3 | Полные взаимодействия заявленных контролов, прокрутка, темы, пользовательский виджет и динамическая демонстрация в standalone/HUD. |
| 5 | 2.4 / K8 | Передача ошибок хосту, безопасное закрытие и создание новой сессии после графической ошибки. |
| 6 | K1–K9 / 5.4–5.5 | Воспроизводимый итоговый проход на заявленной Windows-конфигурации, measurements baseline, документация и проверка поставки отдельному consumer. |

При завершении каждого подэтапа STATUS должен указывать реализацию, конкретные прошедшие проверки, их дату и оставшийся объём. Завершённый подэтап не объявляет завершённым весь родительский пункт или alpha.

### Этап 1. Проверить графический стек и встраивание

Создать минимальный Vulkan-хост: простая сцена, текст и полупрозрачная панель Skia поверх неё.

- **1.1. Совместимость пакетов.** Проверить совместимость зафиксированных версий Silk.NET, SkiaSharp и нативных библиотек.
- **1.2. Жизненный цикл графики.** Реализовать создание GPU-контекста, поверхности, вывод кадра, изменение размера и освобождение ресурсов.
- **1.3. Графический пример.** Показать сцену, текст и полупрозрачную панель Skia в минимальном Vulkan-хосте.
- **1.4. UI-текстура HUD.** Использовать прозрачную GPU-текстуру с premultiplied alpha; хост накладывает её на сцену без копирования через CPU.
- **1.5. Владение ресурсами.** Хост владеет устройством, очередью и swapchain; Alloy владеет собственным Skia-контекстом и UI-текстурами.
- **1.6. Синхронизация.** Для первой версии использовать одно устройство и согласованное использование графической очереди. Явно передавать зависимости GPU-работы и сигнал завершения использования текстуры хостом. Проверить синхронизацию и переходы состояния изображений при обмене ресурсами со Skia.

Результат этапа — работающий пример и зафиксированный контракт взаимодействия. Если возможности пакета ограничивают интеграцию, оформить конкретное техническое препятствие до разработки основного рендерера.

### Этап 2. Разделить прототип на независимые слои

- **2.1. Сборки и зависимости.** Вынести зависимости Skia и Silk из общего API; переместить интеграцию с TrueMoon в hosting-сборку.
- **2.2. Контракты запуска.** Заменить привязанный к Silk.NET `IGraphicsPlatform` отдельными контрактами окна и рендерера. Реализовать `UiSession`, оконный и встраиваемый сценарии; сохранить `UsePresentation<TView>()` с явным выбором бекендов.
- **2.3. Дерево и время жизни.** Исправить хранение и обход дочерних элементов, родительские связи и освобождение подписок.
- **2.4. Диагностика.** Убрать экспериментальный вывод из основного пути рендеринга; обеспечить передачу ошибок в диагностику.
- **2.5. Другие графические пути.** Перенести существующий OpenGL-прототип на новые контракты как второй явно выбираемый GPU-путь. Raster использовать для проверок без окна.

Результат — одно представление запускается с разными графическими адаптерами без изменения кода контролов.

### Ближайшие подэтапы 2.1: разделение зависимостей

Сверка исходников 2026-10-04 показала: графический путь 2.5 уже был реализован, однако общий Alloy ещё содержал legacy presentation, а Rendering.Skia ссылался на Platform.Silk. Переносить связку renderer/window в Hosting: этот модуль уже зависит от обоих адаптеров и организует standalone запуск. Перенос 2.1a выполнен и проверен 2026-10-05: presenter/counters и `SilkVulkanHost` находятся в Hosting, renderer принимает только raw descriptor и больше не ссылается на Platform.Silk. Перенос 2.1b также выполнен и проверен 2026-10-05: 31 legacy-файл вынесен в Hosting.Compatibility, Alloy оставлен только с Runtime/Argentis, AlloyTest использует explicit Hosting API. Требуемые зависимости compatibility явно подключены в Hosting; транзитивные legacy dependencies через Alloy удалены. Фактические проверки и ограничения записаны в STATUS. Пять основных модулей, net10.0, xUnit и критерии K1–K9 сохраняются.

Целевые направления ProjectReference:

| Модуль | Зависимости UI после разделения |
| --- | --- |
| Argentis | Без зависимости от других UI-модулей, Skia и Silk |
| Alloy | Argentis; runtime и независимые контракты |
| Rendering.Skia | Alloy/Argentis, SkiaSharp; Vulkan bindings допустимы внутри Vulkan-интеграции, без Platform.Silk и оконного API |
| Platform.Silk | Alloy/Argentis, Silk; без Rendering.Skia и SkiaSharp |
| Hosting | Alloy, Rendering.Skia, Platform.Silk, TrueMoon lifecycle/DI contracts; интеграционные adapters и standalone presentation |

**2.1a — выполнен и проверен 2026-10-05: убрать Rendering.Skia → Platform.Silk.**

1. Перенести VulkanWindowPresenter и его presentation resource counters в Hosting; сохранить алгоритм retirement, Present/PresentImage, ownership и синхронизацию.
2. В Hosting вынести Silk→VulkanHostContext adapter. Заменить renderer API `FromSilkDevice`, `VulkanUiTarget(VulkanDevice)`/`Device` и `VulkanInteropSurface(VulkanDevice, ...)` на raw descriptor плюс явные convenience factories в Hosting. Raw host descriptor, enabled capabilities, lifetime callbacks и native ABI сохраняются.
3. Обновить HostedUiWindow, GPU manual probes и README на новые namespace/factories; удалить ProjectReference renderer→platform. Vulkan types, нужные renderer для lease/layout, оставить backend-specific package dependency; не переносить их в общий Alloy.
4. Проверить прямой raw/raster/OpenGL renderer без Hosting/Platform: исходники, ProjectReference и фактические сборочные зависимости. Наличие Silk.NET.Vulkan package внутри Vulkan renderer не равнозначно зависимости от оконного Platform.Silk. До 2.1b отдельно учитывать transitive Skia/Silk/TrueMoon зависимости через legacy Alloy: удаление renderer→platform не доказывает полного очищения всего графа.

Готовность 2.1a: нет циклов между сборками и ссылки renderer→Platform.Silk; публичный raw-host путь работает без Silk window/device wrapper; общие3backend/input/layout, external App и обе standalone ветки сохранены. Проверки после переноса: сборка решения и UI xUnit; raw-host, raw HUD, backend comparison, OpenGL external/window/error, Vulkan window, короткий retirement smoke в maintenance1/legacy режимах; publish и запуск опубликованного примера. Новые результаты записать в STATUS, прошлые логи не засчитывать автоматически. Это завершает только 2.1a, Alloy в этот момент ещё может содержать legacy зависимости.

**2.1b — выполнен и проверен 2026-10-05: оставить общий Alloy независимым.**

1. Вынести старую связку VisualTree/IGraphicsPlatform/ViewHandle/ViewManager, presenters и no-options UsePresentation из Alloy в явно выделенную compatibility область Hosting. Она переходная, не часть нового runtime. Не возвращать зависимости Hosting/Skia/Silk в Alloy ради совместимости.
2. Перевести найденный consumer `ManualTests/AlloyTest` на Hosting.UsePresentation с явным backend/host; View1/View2 уже наследуют Argentis.View/Element. Проверить оставшиеся callers, imports и сборочную принадлежность типов. Сохранение namespace само по себе не обеспечивает совместимость уже собранных приложений; миграция alpha API допускается планом и документируется с обновлением примеров.
3. Удалить из Alloy PackageReference Silk.NET/SkiaSharp/Topten.RichTextKit и ProjectReference Core/Contracts после переноса использующего их кода; проверить фактическую assembly dependency. Выделение compatibility области не означает исправление старого Visual.GetEnumerator: его проверка/вывод из использования учитывается отдельно в 2.3.

Готовность 2.1b: Alloy содержит runtime/независимые контракты и зависит только от Argentis среди проектов; обычное создание UiSession не требует TrueMoon App, Skia или Silk. Hosting остаётся опциональным. Примеры и проверки 2.1a повторяются для изменённых границ; путь миграции legacy callers описан. Реализация 2026-10-05 использует namespace `TrueMoon.Alloy.Hosting.Compatibility` (presenters — `.Compatibility.Presenters`), без type forwarders: consumers обновляют references/imports и пересобираются. Проверки assembly references и отдельные core-only/renderer-only consumers подтверждают новые границы; compatibility consumer проверяет регистрацию, не старый window/lifetime путь. [Миграция и ограничения](../../TrueMoon.Alloy.Hosting/Compatibility/README.md). Завершение 2.1 не объявляет завершёнными 2.3/2.4 или alpha.

После разделения: ownership Remove/SetContent/transfer (3.4/2.3) уточнён и проверен 2026-10-05; one-way3.5a и BindTwoWay3.5b реализованы и проверены 2026-10-06 в своих контрактных границах. Фабрики4.8, первая форма AlloyTest5.1a, коллекции3.5c и условные области3.5d/демонстрация5.1b реализованы и проверены 2026-10-07; window lifetime defect исправлен и повторно проверен в ограниченном Debug/published Release наборе. Следующий шаг — полные взаимодействия5.1c в той же сцене. Итоговые K1–K9 идут по согласованному порядку; реализация отдельных подэтапов не завершает весь3.6 или alpha. Дополнительные GPU/EXT-only/VRAM исследования вести по выявленным рискам; async frames-in-flight остаются за пределами текущего последовательного режима.

### Уточнение ownership и failure semantics — 2026-10-05

Это уточнение lifetime постоянного дерева для 2.3/3.4 перед bindings. Модули, net10.0/xUnit и критерии K1–K9 сохраняются.

- Контейнер владеет текущими детьми; UiSession владеет корнем и surface после успешного создания. Remove/Clear/замена элемента коллекции/SetContent отсоединяют старое поддерево без Dispose и возвращают владение вызывающему коду. Оно обязано либо повторно добавить его, либо Dispose.
- Перенос — явные Remove и Add/SetContent. Между вызовами поддерево принадлежит вызывающему коду. После detach очищаются focus/hover/capture/pressed state и ссылки прежней сессии; повторное подключение не восстанавливает ввод. Сохранённые данные, selection/scroll и экземпляры остаются. Move внутри одной коллекции не отсоединяет элемент и сохраняет ввод/подписки; замена соседней ветви сохраняет состояние неизменённых контролов.
- `Own` освобождает ресурс при Dispose и сохраняет его при Remove/переносе. Новый `OwnAttachment` требует live attached element, освобождает ресурс при текущем detach и не переносит subscription в новую attachment. Повторное подключение может создать новую подписку через AttachmentChanged. Это lifetime foundation, не реализация bindings.
- Прямые Detach/Dispose принадлежащего контейнеру ребёнка отклоняются до изменения. Сначала Remove/SetContent(null). Dispose контейнера автоматически освобождает оставшихся детей, в том числе virtual cleanup контролов. Корнем управляет UiSession; вручную отсоединять или передавать корень живой сессии нельзя.
- Invalid owner/cycle/disposed subtree/index/thread проверки выполняются до изменения дерева. Такие ошибки сохраняют прежнее владение и ввод. Внешние уведомления выполняются после согласованного изменения всех parent/attachment связей. Ошибка observer/cleanup не откатывает уже видимый commit: продолжаются остальные notifications/cleanup, затем исходная одиночная ошибка или AggregateException передаётся вызывающему коду. После такой ошибки Add/SetContent может уже передать владение контейнеру; автоматически повторять изменение нельзя.
- Структурные изменения и Dispose сессии внутри tree/lifetime notifications отклоняются; отложенную работу следует отправлять через UiSession.Post. Queued Element.Dispatch привязан к текущей attachment и пропускается после удаления/переноса. Прямой UiSession.Post остаётся явно управляемой работой вызывающего кода.
- Ошибка attachment при создании UiSession откатывает подключение всех узлов и attachment subscriptions; корень остаётся вызывающему коду. При Dispose ошибки отдельной подписки не прерывают освобождение siblings, surface и уведомление registry; повторный Dispose не повторяет cleanup.

Реализация и проверки описаны в STATUS и [Hosting README](../../TrueMoon.Alloy.Hosting/README.md#владение-динамическим-деревом). Это закрывает policy текущего retained дерева; legacy Visual.GetEnumerator и остальные требования 2.3/2.4 остаются отдельно.

### Этап 3. Реализовать дерево, layout и динамический контент

- **3.1. Свойства.** Ввести типизированные свойства с уведомлениями и признаками влияния на layout и отрисовку.
- **3.2. Layout.** Реализовать Measure/Arrange, логические координаты независимо от DPI, размеры, ограничения, margin, padding и выравнивание.
- **3.3. Контейнеры.** Добавить вертикальный и горизонтальный контейнеры, наложение элементов, clipping и прокрутку.
- **3.4. Динамическое дерево.** Поддержать замену содержимого контейнера и изменения коллекций. Неизменённые экземпляры сохраняют фокус и состояние; удалённые освобождают подписки и захват ввода.
- **3.5. Привязки.** Добавить явные типизированные привязки к `INotifyPropertyChanged` и наблюдаемым коллекциям, включая двустороннее обновление редактируемых значений.
- **3.6. Потоки.** Выполнять изменения UI последовательно на потоке-владельце. Фоновые обновления поступают через диспетчер; в HUD поток и последовательность вызовов задаёт хост.

Результат — постоянное дерево с корректным layout и точечными обновлениями, сохраняющими состояние оставшихся элементов.

### Проверяемые подэтапы привязок и динамической разметки

- **3.5a. Односторонние свойства.** Bind/BindText, direct selector, initial sync, owner-thread Dispatch и отключение/reconnect при detach/reattach. Контракт ниже; фактическая готовность отдельно в STATUS.
- **3.5b. Двустороннее редактирование.** Общий typed read/write механизм для TextBox.Value, CheckBox.IsChecked и Slider.Value. Для первой версии запись в модель выполняется при каждом изменении редактируемого значения; режим commit по потере фокуса/команде откладывается. UI setter и source getter/setter работают в согласованном owner-thread контексте; фоновые source events проходят через dispatcher.
- **3.5c. Наблюдаемые коллекции.** Initial population и INotifyCollectionChanged Add/Remove/Replace/Move/Reset. Добавление/удаление/перестановка сохраняют экземпляры и состояние незатронутых элементов; Move не отсоединяет nodes. До реализации определить соответствие model item → generated node, обработку повторяющихся items и Reset. Явно описать, какие generated nodes освобождает binding, а какие передаются caller; исходные Remove/SetContent ownership правила сохраняются.
- **3.5d. Условные области.** В той же демонстрации показать загрузку/ошибку/содержимое через ContentControl.SetContent. Неизменённые соседние ветви сохраняют состояние. Новую ветвь создавать и проверять до замены; старую переиспользовать либо Dispose по существующим правилам. BindContent — возможный следующий helper по выявленной потребности демонстрации, не обязательная новая API alpha и не автоматическое наблюдение за произвольными factory lambdas.

До реализации 3.5b зафиксировать публичную typed API и полный контракт: equality/feedback/reentrancy guard, чтение канонического значения после setter normalization, отображение отказа setter и состояние контрола после отказа. Ошибка не должна молча теряться или нарушать native callback boundary; нельзя считать её доказательством отката уже committed изменений. Обе стороны обязаны соблюдать инварианты контрола, включая selection/caret TextBox. Новое корректное изменение после ошибки должно допускать восстановление. Проверки: реальный ввод, переключение и drag/keys; отсутствие обратной записи при source → UI; normalization/equality/validation failure; detach/reattach/Move/transfer; stale queued/in-flight события и cleanup.

Для 3.5c отдельно проверить empty/single/multiple items, все пять действий коллекции, source changes во время detach, reconnect без повторных subscriptions, фоновые события и ошибки node factory/notifications. Неуспешное создание нового subtree должно иметь определённое владение и cleanup; ошибка notification после commit не повод автоматически повторять mutation. Для 3.5d проверить ошибку создания новой ветви, освобождение заменённой ветви и сохранение focus/selection/scroll соседей.

### Контракт коллекционных привязок 3.5c — согласован 2026-10-07 после 93a5cae

Пользователь разрешил реализацию предложенного контракта. Цели retained дерева, ownership обычных Remove/SetContent, границы модулей, net10.0/xUnit и K1–K9 сохраняются. Первая версия намеренно использует уникальные reference items; keys и повторяющиеся occurrences отложены.

- Публичная API Argentis: `container.BindItems(source, factory)` для ObservableCollection<TItem>/ReadOnlyObservableCollection<TItem>, `TItem : class`, factory возвращает Element; конкретный тип контейнера сохраняется. Источник фиксирован до Dispose, автоматическое наблюдение за заменой model property не добавляется. Один binding на первоначально пустой Items; после регистрации структура Items управляется исключительно binding, даже detached. Статические controls располагаются в соседних контейнерах. `RefreshItems()` запрашивает повторную синхронизацию; Unbind не вводится.
- Сопоставление только ReferenceEquals: null и повтор одной ссылки отклоняются до изменения дерева; равные по Equals разные объекты допустимы. Свойства item обслуживают существующие property bindings. Reset/reattach/Refresh сохраняют nodes для оставшихся объектов, переставляют без detach, создают новые и освобождают отсутствующие. Новый объект с прежним Id создаёт новую строку. Add/Remove/Replace/Move/Reset, включая range events, запрашивают сверку актуального snapshot; промежуточные состояния могут coalesce. Add+Remove до Update может не вызвать factory, Remove+Add той же ссылки — сохранить node. Replace той же ссылкой — no-op.
- Source и items принадлежат вызывающему коду. Factory возвращает новый live unowned/unattached subtree; disposed/owned/repeated nodes и ancestors/cycles отклоняются. До commit принятые drafts принадлежат binding, после — контейнеру. При удалении binding получает ownership отсоединённой строки и обязан Dispose. Detach всего контейнера сохраняет строки и отключает subscription; Dispose контейнера освобождает детей обычным путём без повторного cleanup binding. Factory отвечает за ресурсы, выделенные до throw и не возвращённые binding; чужой owned node binding не освобождает.
- Snapshot проверяется, все новые строки полностью создаются и проверяются до batch commit. Один внутренний TreeChange фиксирует конечный порядок детей, parent/attachment связи и mapping до notifications. Сохранённые строки не отсоединяются: focus/selection/subscriptions сохраняются, scroll offset ограничивается обычным layout clamp. Ошибка snapshot/factory/validation сохраняет прежнее дерево, cleanup drafts продолжается после отдельных ошибок. Source mutation из factory отклоняет подготовку по version guard; исходная модель не откатывается. Source changes из observers ставят следующую синхронизацию в очередь, без вложенного commit.
- Notification/attachment callback/cleanup failure после commit не откатывает дерево и mapping. Остальные observers и cleanup удалённых строк выполняются best-effort; одна ошибка сохраняет исходный тип, несколько передаются AggregateException. Повторное применение committed mutation не допускается. Следующий корректный event/RefreshItems допускает recovery.
- Registration живёт через Own, connection через OwnAttachment. До attach source не читается и factory не вызывается; attach/Bind уже attached контейнера подписывает source и ставит initial sync в очередь следующего Update до layout. Структурные mutations внутри AttachmentChanged по-прежнему запрещены. Detach/Dispose отключают subscription; captured attachment dispatcher и active guard отбрасывают старые queued/in-flight callbacks после reattach/transfer.
- Background notifications только запрашивают sync; snapshot/factory/tree mutations выполняются на owner thread. Владелец source обязан сериализовать changes/notifications и исключить конкурентное перечисление; ObservableCollection не становится потокобезопасной. AlloyTest меняет коллекцию на UI-потоке; фоновые результаты доставляются через dispatcher. Factory failure в initial Update оставляет direct session живой с прежним списком для recovery; standalone HostedUiWindow использует существующий callback boundary и передаёт ошибку через Completion/StopAsync после закрытия. Inline error rows — отдельный 3.5d через SetContent.

Проверки: initial empty/single/multiple, все пять actions/ranges, identity/duplicates/null/coalescing, retained input/selection/scroll/subscriptions, factory drafts/validation/errors, coherent postcommit notifications/cleanup/recovery, detach/reattach/transfer/background/stale events и эксклюзивность Items. Затем динамический редактируемый список в существующей SettingsModel/View1, sequential Debug/published Release window smoke. Фактические результаты реализации и пределы проверок — в STATUS; контракт не объявляет завершёнными 3.5d, 5.1b целиком или alpha.

### Реализация условных областей 3.5d / завершение 5.1b — 2026-10-07

В AlloyTest отдельная область переключается между загрузкой, ошибкой и готовым содержимым через существующий ContentControl.SetContent; форма и наблюдаемый список остаются соседними retained ветвями. Состояния демонстрации контролируются явными действиями модели, без реального сетевого запроса или обещания async loading/cancellation. Ошибка содержит действие «Повторить», возвращающее состояние загрузки. BindContent и новые library API/dependencies не вводятся.

Sample-specific LoadStatusRegion использует обычную typed property binding immutable LoadStatus snapshot (phase/message). Property hook ставит замену в очередь owner thread, включая initial attach; attachment generations отбрасывают stale work, reattach повторно сверяет desired/displayed состояние. Одинаковый snapshot сохраняет текущую ветвь без redraw/rebuild. Новая ветвь полностью строится до SetContent validation/commit. При factory/validation failure прежний child остаётся; принятый unowned draft очищается, foreign owned node/ancestor не освобождается. Factory отвечает за ресурсы, выделенные до throw без возврата.

После commit, в том числе когда SetContent бросил notification error, принятая ветвь остаётся собственностью контейнера, старая освобождается caller-кодом виджета. Cleanup error не останавливает оставшийся cleanup; single error сохраняет тип, несколько передаются AggregateException. Desired model/property snapshot не откатывается при ошибке подготовки; displayed child/status остаются прежними до успешного event/RefreshContent. Postcommit ошибка не запускает повторную замену автоматически. Общие Remove/SetContent ownership правила не меняются.

Проверки используют точные исходники sample widget через Compile links в существующем xUnit проекте, без ProjectReference на manual executable/App/Silk. Проверять все три ветви/messages, фабрику и validation before commit, old/draft cleanup и notification failures, equality/coalescing, owner-thread/background/attachment lifetime, input cleanup заменённой ветви и focus/selection/scroll соседей. Реальный View1/window smoke дополняет headless проверки transitions/retry/Dispose, отдельно от fonts/OS input/long soak/alpha. Результаты текущего запуска — в STATUS.

### Контракт двусторонних property bindings 3.5b — 2026-10-06

Для реализации принят `element.BindTwoWay(uiProperty, source, x => x.Property)`, сохраняющий конкретный тип Fluent-цепочки. Выбирается прямое readable/writable public instance property точно того же TValue; computed/nested/field/conversion/read-only/private-set/init-only selectors отклоняются до подключения. One-way Bind сохраняет свой контракт, включая стандартное приведение типа. На одно UiProperty остаётся одна registration любого направления до Dispose.

- Начальное значение и reattach берутся из source, без обратной записи. Пока элемент detached, обе стороны отключены. UI-изменение attached свойства записывает значение немедленно на owner thread, только если оно отличается от source. Локальное программное изменение также считается UI-изменением; source → UI и изменения во время синхронизации не вызывают повторный setter.
- После setter значение source перечитывается и применяется к контролу, включая normalization и setter без PropertyChanged. Same-thread echo выбранного свойства во время source setter подавляется; остальные source events, в том числе фоновые и изменения модели из UI observers при source → UI sync, остаются queued. Reentrant UI property events не запускают вложенный setter; по завершении записи применяется актуальное canonical source value. Это не транзакция модели.
- При setter/getter ошибке write-back пытается перечитать и применить актуальное source value, затем передаёт ошибку из Set/HandleInput. Если source уже частично изменён setter, применяется его фактическое состояние, а не предполагаемый rollback. Если readback/validation/observer cleanup также неудачны, одиночная ошибка сохраняет тип, несколько передаются AggregateException; последнее committed UI значение сохраняется там, где восстановление невозможно. Новое корректное изменение допускает recovery.
- Property change после commit выполняет invariant hook контрола, invalidation и всех подписчиков, даже когда отдельный observer бросает исключение; ошибки передаются после попыток уведомления. TextBox selection/caret остаются в пределах canonical текста и на границах графем при generic Set и normalized editing. Failed slider drag освобождает capture. Native callback boundary использует существующую передачу ошибки хосту; direct UiSession caller обрабатывает исключение.
- Registration/connection используют прежние Own/OwnAttachment, active guard и attachment-scoped Dispatch. Все source/UI subscriptions удаляются при detach/Dispose; stale queued/in-flight callbacks не читают source и не пишут в новую attachment. Потокобезопасность модели остаётся обязанностью её владельца.

Контракт реализован и проверен в подэтапе 2026-10-06: общий binding, property notification/invariant hooks, TextBox grapheme selection/caret и failed Slider capture cleanup. Конкретные команды, результаты и ограничения отдельно в STATUS; этот результат не заменяет полные control/window/alpha проверки.

### Уточнение односторонних property bindings — 2026-10-06

Первый подэтап 3.5/3.6 реализуется в Argentis без зависимостей от App, renderer или платформы. Общая цель привязок, net10.0/xUnit и критерии K1–K9 сохраняются; двусторонние и collection bindings остаются отдельными следующими подэтапами.

- Публичная Fluent API: `element.Bind(uiProperty, source, x => x.Property)`; для Text — `text.BindText(source, x => x.Name)`. Возвращается тот же конкретный тип контрола. Source — reference type с INotifyPropertyChanged; selector выбирает прямое instance property, включая стандартное приведение типа. Nested paths, поля и вычисляемые выражения явно отклоняются, автоматического наблюдения за ними нет.
- На одно UiProperty допускается одна binding registration до Dispose. Повторный Bind отклоняется без замены прежней привязки. Для этого подэтапа Unbind/замена регистрации не вводятся. Разные свойства можно привязывать независимо; DataContext не подключается автоматически.
- Регистрация живёт через Own до Dispose; connection живёт через OwnAttachment. До attach модель не читается и source subscription отсутствует. Attach/reattach синхронно считывает актуальное значение на UI-потоке и создаёт одну подписку. Detach отключает subscription, не меняя последнего значения; Move сохраняет connection.
- PropertyChanged выбранного свойства, null или пустое имя отправляет чтение через Element.Dispatch в следующий host Update, в том числе при уведомлении с UI-потока. Читается последнее состояние модели, не snapshot на момент события; getter должен быть безопасен на потоке-владельце. Прежние queued/in-flight notifications после detach/reattach/transfer не читают модель и не изменяют элемент.
- Направление только source → UI. Локальное изменение контрола не записывается в source. Validation и invalidation выполняются обычным Element.Set; одинаковое значение не вызывает redraw/property event.
- Ошибка первичной регистрации на attached element отключает её subscription и освобождает слот для повторного Bind. При неуспешном UiSession attach действуют существующие rollback/ownership правила: регистрация сохраняется для следующего attach. Ошибка getter/validation при queued update передаётся из Update; новое корректное уведомление позволяет повторить синхронизацию. Уже committed property/tree changes не откатываются из-за ошибки внешнего observer.
- Критерии этого подэтапа: initial attach, выбор уведомлений, layout invalidation, отсутствие обратной записи, повторные detach/reattach и Move, разные UI-потоки, stale queued/in-flight events, failure/retry и отсутствие повторных subscriptions. Фактические результаты и границы проверок — в STATUS.

### Этап 4. Добавить ввод, контролы и оформление

Уточнение реализации 5.1c /4.1–4.7, 2026-10-07: typed `UiProperties.Padding` и `Spacing` участвуют в local → style → theme → default. Тема содержит валидируемые immutable `ButtonPadding`, `EditorPadding`, `StackSpacing`; исходные constructor defaults сохраняются (button12/8, editor8, stack0). CheckBox добавляет24px слева для индикатора. Произвольные контейнеры по умолчанию имеют padding0; theme button/editor tokens применяются только к соответствующим controls. Width/Height/local padding/spacing и типографика продолжают перекрывать тему. Palette/density меняются в существующем дереве AlloyTest, без повторного создания формы/строк.

Для scrolling один `Element.ChildClipBounds` задаёт child viewport одновременно drawing/hit testing; ScrollViewer исключает padding из него. Wheel на границе не потребляется и может всплыть во внешний ScrollViewer. `BringIntoView(Rect)` раскрывает arranged root-coordinate bounds с clamp, oversized content выравнивается сверху; UiSession.Focus (включая Tab/Shift+Tab) применяет его от внутреннего viewport к внешнему с поправкой на фактическую смену Offset. Это не горизонтальная прокрутка и не виртуализация. Secondary pointer down/up не меняет primary Slider gesture или focus; FocusLost/disabled/detach сохраняют existing input cancellation policy.

AlloyTest показывает все заявленные control types, PNG32×32 через borrowed Image source, owned decoded source освобождается View1 после детей. Asset поставляется build/publish через PreserveNewest. VolumeMeter — sample-specific widget с собственным IDrawingContext drawing и существующим normalized ProgressBar property. Новых backend/native ABI/packages не вводится. Headless tests Compile-link точные demo sources, raster проверяет image/custom drawing и clipping; native OpenGL smoke дополняет keyboard/pointer/theme/density/scroll/disposal. OS clipboard и full shared raster/OpenGL/Vulkan/HUD/font/DPI/soak результаты учитываются отдельно в STATUS; этот функциональный этап не подменяет итоговый alpha проход.

- **4.1. Контролы первой поставки.** `Text`, `Image`, `Button`, `CheckBox`, однострочный `TextBox`, `Slider`, `ProgressBar`, `Border`, контейнеры и `ScrollViewer`.
- **4.2. Маршрутизация ввода.** Реализовать hit testing, порядок наложения, pointer capture, фокус, Tab-навигацию и активацию с клавиатуры.
- **4.3. Редактирование текста.** Разделить клавиши и текстовый ввод; поддержать латиницу, кириллицу, выделение и clipboard в `TextBox`. Согласованное уточнение 2026-10-07: Windows Get/Set используют единый Win32 путь с borrowed HWND. Get возвращает null только при отсутствии текстового формата; borrowed HGLOBAL не освобождается, lock/clipboard закрываются в finally. Set готовит UTF-16 GMEM_MOVEABLE до Open, владеет памятью до успешного SetClipboardData, затем передаёт её Windows; при отказе до передачи освобождает draft. Close failure после публикации не возвращает владение. Set заменяет содержимое clipboard текстом, откат прежних форматов после EmptyClipboard не обещается. Open допускает десять retry waits по10ms только при error5, operation/cleanup errors сохраняются. Operational errors оборачиваются в UiClipboardException с Read/Write и original cause; UiSession.HandleInput сообщает ClipboardFailed и сохраняет сессию. Прямой Get/Set, programming errors и ошибки notification handlers продолжают бросать исключения. TextBox изменяет value при Cut только после успешной записи. Clipboard доступен на owner thread до Dispose; null/embedded NUL отвергаются до native записи. GLFW error callback не меняется; другие платформы сохраняют Silk путь. Native copy/cut/paste smoke запускается только при точном заменяемом fixture `Alloy clipboard smoke fixture v1`; сохраняет/восстанавливает текст, не произвольные форматы. Controlled clipboard smoke проверяет окно/редактор отдельно от обмена с ОС. Фактические результаты и блокеры — в STATUS. Новых packages или изменения renderer ABI нет.
- **4.4. Совместная работа HUD и игры.** Возвращать хосту результат обработки события и состояние захвата мыши/клавиатуры.
- **4.5. Тема и состояния.** Добавить тему с цветами, типографикой и отступами, а также состояния hover, pressed, focused и disabled.
- **4.6. Приоритеты значений.** С уточнением UIA1 от2026-10-22: local/binding → direct Style элемента → scoped стиль View → Theme → значение по умолчанию. Снятие слоя возвращает значение следующего уровня без перестройки дерева.
- **4.7. Пользовательские виджеты.** Поддержать создание виджета композицией элементов либо собственным рисованием через `IDrawingContext`.
- **4.8. Статические фабрики контролов.** Добавить фабрики на типах контролов Argentis для удобной Fluent-разметки, сохранив публичные конструкторы и возможность смешивать оба способа создания. Начальные согласованные имена — `Button.Simple(string text = "")` и `Text.Plain(string value = "")`. Фабрика обычного контрола создаёт экземпляр с теми же значениями по умолчанию и текущим оформлением темы, что соответствующий конструктор; `Plain` обозначает обычный текст без обработки разметки. Для остальных контролов подобрать понятные имена при их реализации; если отдельного варианта нет, использовать `Create(...)` (например, `TextBox.Create()`, `VStack.Create()`). Имена дополнительных вариантов связывать с явно определённым внешним видом или поведением.

Приоритет 4.8 уточнён 2026-10-06: реализовать после 3.5b вместе с первой формой 5.1a, до collection bindings и завершения всего набора контролов. Принадлежность к этапу 4 и требования фабрик сохраняются.

Правила и готовность 4.8:

- Каждая фабрика объявляется на собственном типе и возвращает его конкретный тип, чтобы последующая Fluent-цепочка сохраняла все методы контрола. Для производных контролов нельзя подменять фабрику унаследованным методом, возвращающим базовый тип.
- Каждый вызов создаёт новый независимый элемент. Фабрика находится в Argentis, принимает параметры создания и не требует окна, UiSession, App/DI или графического backend. Attach/detach, ownership, свойства и динамическое дерево сохраняют принятые правила.
- Публичные конструкторы продолжают поддерживаться для обычного создания, наследования и пользовательских контролов. Фабрики и конструкторы можно использовать рядом в одном дереве; отдельного промежуточного builder не требуется.
- При реализации проверить конкретный возвращаемый тип и доступность Fluent-цепочки, независимость создаваемых экземпляров и эквивалентность обычной фабрики конструктору по исходным свойствам/поведению. Примеры и демонстрации должны показывать оба способа создания. Фактические проверки 2026-10-07 перечислены отдельно в STATUS.

Реализация 4.8 / 5.1a — 2026-10-07: доступны Button.Simple, Text.Plain и собственные Create на TextBox, CheckBox, ProgressBar, Slider, Image, Panel, ContentControl, Border, ScrollViewer, VStack, HStack, Line и Rectagle. Производные Slider/Border/ScrollViewer явно скрывают базовый Create, возвращая свой тип; это не virtual factory и не способ создания пользовательских наследников. Shapes и public constructors сохраняются; Line.Create не добавляет рисование к прежнему пустому Line. AlloyTest получает SettingsModel через DI, редактирует три свойства через BindTwoWay и показывает Summary через BindText; две кнопки меняют модель без перестройки дерева. После первоначального native0xC0000374 диагностика выявила input cleanup после уничтожения GLFW window. Этот lifetime defect исправлен; UI188/188, по10 form smoke с отладочной кучей и по6 lifecycle сценариев в Debug/published Release прошли. Связь исходного неперехваченного0xC0000374 с найденным дефектом — гипотеза, не отдельный полученный стек; long soak/alpha остаются впереди. Цели, зависимости, ownership и K1–K9 не меняются; следующий ближайший шаг — 3.5c/d / 5.1b.

Уточнение window lifetime, 2026-10-07: SilkWindowHost.Run завершает цикл без Reset, сохраняет native window/context и input до host.Dispose на том же потоке. Hosting освобождает renderer/session, input снимает GLFW callbacks с ещё живого окна, затем host уничтожает окно. OpenGL session по-прежнему освобождается в Closing; Render/Input callback errors перехватываются до выхода через native boundary. Прямой NativeWindow.Reset/Dispose до host.Dispose нарушает ownership. Это исправление teardown порядка внутри Platform.Silk, без новых зависимостей или изменений backend/Argentis tree contracts.
Уточнение callback boundary, 2026-10-10: Platform.Silk guards защищают native-origin mouse/wheel/key/text/focus/render handlers, включая DPI normalization до входа в Hosting. Pending Win32 hook fault проверяется внутри boundary до user handler; первый exception/stack сохраняется, поздние callbacks подавляются. Direct callers получают ошибку через VerifyWindowAccess после manual dispatch либо Run (включая финальный dispatch); Hosting сохраняет Completion/StopAsync и best-effort cleanup. Bounded fault/recreate proof — HISTORY/STATUS. Public API/ownership и требования physical DPI, долгого lifecycle/VRAM и full alpha matrix не сокращены.

Уточнение cleanup/K7, 2026-10-10: own Render/Focus subscriptions снимаются при constructor failure и Dispose SilkWindowHost; Pfn Vulkan validation wrapper сохраняется до DestroyInstance и освобождается после последнего native callback. Это исправления callback ownership без изменения public API. Window recreation/resize и native-only controls добавлены как воспроизводимые probes; текущий длительный resource result **failed** на USER/native growth GL/new-thread. Нулевые hooks/registry/WeakReference и presenter counters не заменяют общую resource stability/VRAM evidence. Причина native allocation и новая thread lifetime стратегия ещё не согласованы; текущий dedicated UI thread контракт сохраняется. Подробности — [статус](STATUS.md) и HISTORY. K7 и alpha требования не сокращены.

Уточнение следующего шага K7,2026-10-10: raw Win32/WGL controls воспроизводят USER growth без GLFW/Silk/UI/Skia; serial thread reuse и manual shared owner loop с двумя live окнами проверены отдельно. **Предлагаемое направление Hosting — app-lifetime dispatcher с единым owner event loop**, поскольку GLFW ограничивает window creation/event processing main thread. Worker pool не принимается как portable window scheduler. Production dedicated-thread-per-window контракт этим diagnostic шагом ещё не изменён. Перед его заменой нужны явные startup/shutdown/cancellation/command/fault contracts, concurrent-window isolation, context switching, Vulkan integration и повтор исходного full lifecycle/resource check без ослабления лимитов. Точный native allocator и mixed GL/Vulkan private/handles growth не объявляются устранёнными. [Основание GLFW](https://www.glfw.org/docs/latest/intro.html#thread_safety), [результаты](HISTORY.md#wgl-thread-lifetime-и-shared-owner-loop--2026-10-10). Alpha/K1–K9 сохраняются.

**Реализованный контракт Hosting dispatcher — 2026-10-10.** По запросу приступить к задачам заменён новый Thread на каждое окно: app-scoped HostedUiDispatcher владеет окнами приложения, все scopes используют один Windows process owner loop. Это явное уточнение lifetime предложенного app dispatcher: HWND/device/session имеют app/window lifetime, idle background owner thread сохраняется до выхода процесса, чтобы новый App/scope не возобновлял WGL thread growth. Scoped shutdown не выполняет global GLFW termination. Native ownership, последовательный GPU handoff и отдельный device/session каждого окна сохраняются. Linux/macOS portable main-thread scheduling не заявлен; direct Silk hosts не запускаются конкурентно на другом потоке.

- Start идемпотентен; cancellation отменяет wait, не принятую работу. Close-before-start завершает Completion без allocation и отменяет readiness; после accepted startup stop ждёт owner cleanup.
- Commands FIFO, finite64 batch; startup batch32; отказ одного окна не прекращает siblings. Readiness после window/device/session init; Completion после cleanup и close callback, original cause перед cleanup errors. Закрытые task/delegate targets удаляются из постоянно живущего loop.
- Scope Stop отклоняет новые starts и закрывает активные окна; canceled wait не отменяет shutdown. Завершённые faults наблюдаются через соответствующее окно. Owner callbacks используют Close; ожидание shutdown на owner thread отвергается.
- GL context явно выбирается перед commands/input/render/cleanup, включая sibling native callbacks. SilkWindowHost.Pump сохраняет DPI scope; WM_PAINT refresh через существующий DPI hook имеет callback/reentrancy guards, без нового HWND hook и без unwind в native code.
- App UsePresentation регистрирует dispatcher; прежний четырёхпараметровый HostedUiWindow constructor сохранён, дополнительная перегрузка принимает scope. HUD и renderer/native ABI не меняются.

Функциональная реализация, unit tests, concurrent/native callback/short/chrome/controlled-failure checks и App smoke записаны в [STATUS](STATUS.md)/[HISTORY](HISTORY.md#hosting-owner-dispatcher-и-vulkan-wsi-lifetime--2026-10-10). Full resource check остаётся **failed**: USER стабилен, GDI растёт. Isolated device/Skia controls не показывают GDI trend, но их handles/private также растут; native WSI queries/create/destroy и raw presenter воспроизводят GDI рост. Удержание loader не устраняет его. Это не доказательство конкретного driver allocator. Следующий доступный K7 шаг — изоляция native WSI lifetime и безопасного решения без скрытого shared-device cache, удаления чужих GDI objects или ослабления лимитов. Alpha/net10.0/xUnit/K1–K9 сохраняются.

**Результат следующего K7 investigation — 2026-10-10.** WSI stages/raw Win32/repeat и debugger controls отделили queries от первого swapchain create и зафиксировали screen HDC allocation внутри текущего NVIDIA ICD, без последующего ReleaseDC в bounded lifecycle. Raw surface-first/device-first teardown не меняет GDI trend; завершение raw owner thread очищает его, но не является принятым production scheduling fix: прежний WGL/new-thread USER growth и GLFW/concurrent-window contracts сохраняются. Raw vendor selector проверил установленный AMD: GDI5→5 против NVIDIA6→29 в Debug/Release; full UI на AMD этим не подтверждён, production GPU policy не меняется. Device/session/presenter ownership сохраняется, ICD-owned HDC не освобождаются приложением, thresholds не ослабляются. Нужен supported NVIDIA fix либо полностью проверенная alternate GPU configuration; private/handles growth остаётся отдельным вопросом. Independent package consumer5.4–5.5 можно выполнять до устранения environment-specific K7 blocker, однако итоговый alpha проход по-прежнему требует полного K7. [Evidence и воспроизведение](HISTORY.md#wsi-stages-raw-win32-и-driver-screen-dc--2026-10-10).

Результат — интерактивный базовый набор контролов, работающий через общую сессию UI в обоих сценариях запуска.

### Подзадача 4.9. Прозрачные окна и собственное оформление — добавлена 2026-10-08

По запросу пользователя расширить первую рабочую версию поддержкой прозрачных/полупрозрачных standalone-окон, отключения стандартной рамки и заголовка, а также опционального системного поведения при перетаскивании собственного заголовка. Отсутствие рамки, способ прозрачности и системное поведение настраиваются раздельно. Обычное окно по умолчанию сохраняет стандартное оформление и непрозрачность.

- **4.9a. Проверка оконной композиции.** До реализации публичного API сделать минимальный Windows x64 prototype на текущих OpenGL/Vulkan presentation путях: непрозрачное окно, общая opacity, per-pixel alpha0/0.5/1 поверх видимого фона другого окна. Проверить фактическую поддержку desktop composition, alpha format/blending и для Vulkan supported composite alpha; прозрачная offscreen UI-текстура/HUD не доказывает прозрачность standalone-окна. Зафиксировать capability matrix и ограничения Silk/GLFW/driver. Если текущий presenter не поддерживает требуемый режим, явно спроектировать отдельный Windows composition adapter; не переключать backend и не объявлять непрозрачный fallback успехом молча.
- **4.9b. Прозрачность.** Предусмотреть явные режимы opaque, общая непрозрачность окна (opacity0–1) и попиксельная прозрачность с полупрозрачным UI/непрозрачными контролами. Валидация и reporting неподдерживаемых сочетаний обязательны. Для текущего GLFW пути общую opacity и transparent framebuffer не включать одновременно: документация объявляет результат неопределённым. Поведение ввода в прозрачных областях определить отдельно и проверить нативно; визуальная alpha сама по себе не является контрактом click-through. Управляемый click-through, blur/Acrylic/Mica и тени DWM — отдельные возможные расширения, в обязательный объём4.9 не входят.
- **4.9c. Окно без системного оформления.** Отключаемая системная рамка/заголовок, собственный заголовок из Argentis-элементов и команды minimize/maximize/restore/close через оконный контракт. Явно определяемые drag regions и исключённые интерактивные области: кнопки, TextBox и другие controls не начинают перемещение окна. Для resizable окон определить невидимые границы/углы resize; учитывать min/max размеры, maximized work area и DPI. Визуально окно остаётся без системного оформления; необходимые Win32 styles выбираются с сохранением системного поведения.
- **4.9d. Опциональное системное перемещение и Snap.** Для собственного заголовка предоставить opt-in native move/resize: системная drag loop, Snap к краям/углам и Snap bar/layouts на поддерживаемой Windows11 при включённых пользователем настройках. Восстановление maximized окна перетаскиванием, double-click maximize/restore и системное меню должны сохранять ожидаемое поведение. Если есть собственная кнопка maximize/restore, поддержать Windows11 Snap Layouts при наведении и Win+Z. Направление реализации — Win32 non-client hit testing (`HTCAPTION`, resize codes, `HTMAXBUTTON`) и системная обработка сообщений; точный механизм согласовать после prototype, не имитировать системные previews рисованием в UI. Уважать Windows settings, не менять их; Aero Shake проверять отдельно при включённой настройке. Peek обозначает просмотр рабочего стола и не является названием Snap при перетаскивании.

**Границы архитектуры.** Конфигурация и capability/команды доступны через Hosting и независимый оконный контракт Alloy; HWND/DWM/message hooks и их owner-thread lifetime остаются в Platform.Silk/Windows adapter, выбор alpha/presentation — в Rendering/Hosting. Argentis описывает собственный заголовок через независимые роли/геометрию, без Win32/Skia/Silk типов. Конкретные имена API пока не согласованы. Внешний HUD не меняет оформление окна игры. Native callbacks/hooks снимаются до уничтожения HWND; исключения не выходят через native boundary. net10.0, xUnit, retained дерево и ownership сохраняются.

**Порядок и готовность.** Ближайший performance profile/подтверждённая оптимизация K9 сохраняются; затем4.9a,4.9b/c/d и оконная демонстрация до итогового alpha прохода5.5. Это явное расширение объёма первой версии: к K3 добавить прозрачность/собственный заголовок/native Snap, к K6/K7 — validation и lifecycle новых presentation/hooks. Проверять OpenGL/Vulkan на заявленной конфигурации, реальные desktop alpha, drag/excluded controls/resize/buttons/Snap, включённые и выключенные системные настройки, DPI100/150/200% и переход между мониторами, minimize/zero-size/restore, повторные create/close без retained callbacks/resources; выполнить Debug/published Release smoke и регрессию обычного окна/HUD. Прототип, реализация и прошедшие проверки фиксируются отдельно; неподдержанный обязательный режим остаётся блокером4.9, изменение требований фиксируется явно.

Источники для реализации: [GLFW: прозрачность окна](https://www.glfw.org/docs/latest/window_guide.html#window_transparency), [Win32 custom frame/DWM](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe), [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest), [Snap Layouts для custom title bar](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-snap-layout-menu), [Snap/Snap bar и настройки Windows](https://support.microsoft.com/en-us/windows/experience/snap-your-windows).

**Исторический результат 4.9a, 2026-10-08.** В Debug/published Release проверена реальная desktop-композиция поверх другого окна: OpenGL поддержал opaque/общую opacity/per-pixel alpha; Vulkan поддержал opaque/opacity, но не per-pixel ни с обычным Opaque, ни с internal PreMultiplied experiment. Driver объявляет Opaque|PreMultiplied, однако это не доказало прозрачность HWND. Публичный default presenter сохраняет Opaque-first; эксперимент не является публичным API или принятым режимом поставки. Подробные raw RGB, конфигурация и регрессия — [STATUS](STATUS.md).

**Пересмотр направления 4.9a.1 после изоляции, 2026-10-08.** Вывод о необходимости отдельного composition adapter был преждевременным: отсутствие desktop alpha в двух проверенных конфигурациях не исключало проблему HWND/DWM. По согласованной проверке сначала выполнен Vulkan proof без native Skia: `vkCmdClearColorImage` → существующий GPU blit/presenter → GLFW swapchain. В Debug/published Release transparent framebuffer + PreMultiplied в decorated окне повторил смешивание с белым; `PatBlt(BLACKNESS)` клиентского DC после show/resize устранил его. Alpha 0/128/255 над двумя фонами прошла initial/resize/restore; тот же эксперимент с настоящим Skia UI также прошёл. Borderless raw Vulkan прошёл на этой машине и без очистки; это наблюдение не отменяет явной инициализации redirection surface. Источники гипотезы: [GLFW PR #2815](https://github.com/glfw/glfw/pull/2815), [PatBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-patblt). Фактические измерения и ограничения — STATUS.

1. Следующий шаг — **4.9b: оконный configuration/capability contract и интеграция подтверждённого GLFW/Windows пути**. Для per-pixel явно запросить TransparentFramebuffer до создания окна, проверить supported composite alpha и выбрать PreMultiplied для premultiplied output. Windows adapter должен обеспечивать инициализацию redirection surface в нужные моменты lifecycle; выбирать механизм WM_PAINT/resize после проверки, а не копировать диагностическую очистку перед каждым frame. HWND/DC/hooks и owner-thread cleanup остаются в Platform/Hosting, Argentis/HUD не получают Windows типов.
2. Сохранить обычный opaque путь и отдельную whole-window opacity; неподдержанный per-pixel сообщает причину, без скрытого fallback. Обычный VkSwapchainKHR используется для подтверждённого режима, SkiaVulkan renderer и UI image ownership сохраняются. Readback в prototype проверяет источник и не участвует в desktop presentation.
3. Повторить desktop proof с публичной конфигурацией: оба backend, decorations on/off, resize/minimize/restore, repeated create/close, DPI/monitor transitions и регрессия обычного окна/HUD. Диагностический успех ещё не означает готовность lifecycle/configuration/input контракта. Borderless title bar/native Snap 4.9c/d следуют по прежнему порядку.
4. Layered HWND/UpdateLayeredWindow с явным CPU transfer и GPU DirectComposition/DXGI interop остаются резервными направлениями только для конфигураций, где существующий GLFW/Vulkan путь не проходит проверку. Их реализация сейчас не требуется; для любого будущего transport отдельно подтвердить capabilities, стоимость, ownership и cross-API completion.

Это явное изменение порядка технической реализации после нового desktop proof. Цели Vulkan/per-pixel, net10.0/xUnit, retained UI, HUD ownership и критерии готовности 4.9 сохраняются.

**Принятый и реализованный контракт 4.9b, 2026-10-08.** Alloy содержит immutable `WindowAppearance` (`Transparency`: Opaque/Opacity/PerPixel, initial `Opacity`, `Decorated`) и необязательный `IWindowAppearanceHost`, не меняющий существующий IWindowHost. Параметры валидируются до native allocation; finite opacity0–1 допускается только в Opacity режиме, другие режимы требуют initial opacity1. Native `WindowAppearanceCapabilities` отдельно сообщает transparent framebuffer и uniform opacity; Vulkan presenter отдельно проверяет supported composite alpha. Hosting выбирает premultiplied для PerPixel и opaque для остальных явных режимов, без скрытого fallback. No-argument Vulkan presenter сохраняет прежнюю alpha precedence для существующих callers/HUD. HostedUiWindow фиксирует snapshot options, сообщает combined per-pixel support после startup и ставит SetOpacity в очередь owner thread. Прямой SilkWindowHost принимает те же appearance settings и предоставляет thread-bound SetOpacity.

Windows per-pixel adapter использует SetWindowSubclass, сохраняет GLFW WndProc и обрабатывает creation/WM_PAINT/WM_SIZE/show/DPI/composition события. BLACKNESS очистка выполняется до GLFW damage callback, а не на каждом presentation frame. HWND заимствован, client DC имеет SafeHandle lifetime; static callback и GC root живут до RemoveWindowSubclass/WM_NCDESTROY. Ошибки native callback сохраняются для последующего managed reporting; hooks снимаются на owner thread до HWND destruction. Политика per-pixel input — обычная client-area hit testing, alpha не включает managed click-through. На проверенной Windows alpha0/128/255 области по WindowFromPoint принадлежат окну; полный native mouse interaction и другие configurations остаются отдельными проверками. Uniform opacity пока заявлена в adapter только для Windows.

`Decorated=false` в4.9b реализовал создание без системной рамки. Принятый ниже4.9c добавляет собственный Argentis title bar/команды/resize regions; полная native DPI/monitor/Snap matrix остаётся открыта. Реализация4.9b отделена от её полной переносимой верификации: фактические Debug/published Release, source/desktop/input/lifecycle checks и промежуточные failures — STATUS. net10.0/xUnit и исходные критерии готовности сохраняются.

**Принятый контракт4.9c, 2026-10-08.** Необязательные независимые `IWindowCommands` и `IWindowChromeHost` предоставляют state/minimize/maximize/restore/close, capabilities и публикацию immutable `WindowRegionMap`. `WindowChromeOptions` задаёт NativeDrag (defaultfalse), Resizable, DPI-aware resize border и min/max client sizes. Chrome opt-in требует Windows x64 и Decorated=false; null сохраняет исходный GLFW behavior. У Argentis появился WindowRegionRole Inherit/Caption/Client и WindowFrame с отдельной высотой заголовка; TitleBarFactory Hosting создаёт Argentis content на owner thread с независимыми commands. UI session владеет title/content, HUD не меняется.

Геометрия копируется с учётом paint order/clipping/visibility после update. Focusable controls (включая disabled) и explicit Client subtree исключены из перемещения; custom non-focusable input controls должны явно задавать Client. Native callback не вызывает tree/пользовательские delegates; stale caption map после resize не применяется. Windows adapter сохраняет SYSMENU/minimize/maximize/THICKFRAME semantics по настройкам, снимает видимую рамку через WM_NCCALCSIZE и возвращает native HTCAPTION/resize codes. WM_GETMINMAXINFO использует work area текущего монитора и application limits; resize через оконный контракт задаёт точный client size, обходя GLFW стандартный frame adjustment. Resize border масштабируется по GetDpiForWindow, limits/regions — по существующему UI viewport scale; общая DPI-модель не меняется. Hook/GC root lifetime и reporting callback errors сохраняют правила4.9b.

Это уточняет границу4.9c/d: базовый opt-in native move/resize нужен уже для4.9c;4.9d завершает Snap Layouts/Win+Z/maximize-button hit testing, menu/double-click/maximized drag и реальные interaction/settings/DPI/monitor проверки. Native previews не рисуются вручную. Наличие API и directed-message/desktop proofs не означает прохождение всего4.9 или alpha; фактические результаты — STATUS.

**Реализованный контракт4.9d, 2026-10-08; bounded physical/desktop proof выполнен, полная matrix остаётся открытой.** `WindowChromeOptions.NativeSnapLayouts` — отдельный opt-in (default false), требует Resizable и Windows11; platform capability `SnapLayouts` не означает включённые пользовательские настройки. Явная Argentis роль `Maximize` на enabled кнопке даёт `HTMAXBUTTON`, disabled/Client ancestor сохраняют Client. Non-client hover/leave передаётся DWM; press/release/cancel копируются в очередь UiInput и доставляются вне native callback. Командой владеет Argentis OnClick, native default maximize на press подавляется; Space/Enter сохраняются. Direct Silk hosts должны вызвать DispatchPendingWindowInput после DoEvents, Hosting делает это автоматически.

`SystemMenu` default true: Alt+Space, caption right-click и optional `IWindowSystemMenu.ShowSystemMenu(x,y)` с logical client point, queued equivalent в Hosting. Disabled menu не открывается; native menu учитывает state/Resizable и отправляет выбранный system command самому окну. Caption double-click делегируется системе. Пример использует adaptive header и minimum330×300 вместо760×400 для узких Snap zones. Это уточнение API/демонстрации не сокращает критерии готовности4.9d: real system move/resize, maximized drag, edge/corner/bar/hover/Win+Z, settings on/off, Aero Shake, physical DPI/monitor и lifetime требуют отдельного подтверждения. Исторический LockApp foreground/desktop blocker и новые physical результаты — STATUS; не считать направленные сообщения доказательством Shell Snap UI.

**Уточнение native styles после physical prototype4.9d, 2026-10-08.** Для custom frame снимается GLFW `WS_POPUP`: окно использует overlapped semantics с SYSMENU/minimize/THICKFRAME/maximize по настройкам, без `WS_CAPTION`. Caption regions задаются `HTCAPTION`; полноразмерный client сохраняется через `WM_NCCALCSIZE` с wParam=true. Добавление WS_CAPTION оказалось ненужным для Snap и добавляло невидимые maximize margins; этот вариант отвергнут. Рабочая область/max-size контракт4.9c сохраняется. Проверки видимого оформления используют actual HWND/client geometry, а не один style bit. Alt+Space обрабатывается также на WM_SYSKEYDOWN с Alt context и без autorepeat, до стандартного GLFW keyboard-menu/WM_SYSCHAR suppression; глобальные hotkeys ОС/других приложений имеют приоритет. Native Snap previews остаются ответственностью Windows. Фактические physical/desktop proofs и оставшиеся settings/DPI/monitor критерии — STATUS; прежний LockApp blocker относится к историческому запуску.

### Этап 5. Собрать демонстрации и подготовить alpha

- **5.1. Самостоятельное приложение.** Развивать один AlloyTest вместе с API: **5.1a** — форма с редактируемой моделью, TextBox/CheckBox/Slider и смешанным созданием через фабрики/конструкторы; **5.1b** — динамический список и условные области; **5.1c** — прокрутка, смена темы и полноценные взаимодействия всех заявленных контролов. Добавление/удаление, редактирование и переключатели должны сохранять состояние незатронутых элементов. Пример становится общей контрольной сценой для standalone, backend comparison и адаптируемого HUD.
- **5.2. Vulkan HUD.** Добавить показатели, динамический список и интерактивную панель поверх сцены.
- **5.3. Обновление по необходимости.** Самостоятельное приложение перерисовывается при изменениях и анимациях; HUD обновляет UI-текстуру по необходимости, а композицию выполняет хост.

Уточнение реализации общей сцены5.1c/5.2–5.3, 2026-10-07: AlloyTest сохраняет View1/SettingsModel и выбирает OpenGL по умолчанию или Vulkan через `--vulkan`. AlloyVulkanTest compile-link исходники той же формы, модели, generated rows/условной области/VolumeMeter и SettingsFormSmoke, доставляет тот же PNG; новой shared assembly или зависимости на manual executable нет. Один interaction script используется на raster/OpenGL/Vulkan при одинаковых logical viewport/scale1/1.5/2 и в Vulkan HUD. HUD оборачивает ту же форму прозрачным Panel, добавляет показатели/кнопку сцены, использует borrowed raw Vulkan handles и host-owned composition/presentation; вне панели ввод проходит в сцену. Хост композитит retained UI независимо от UiSession.Update, модель сохраняется при явном пересоздании UI. Нового runtime/backend/native API не вводится. Сравнение всех logical bounds/model/input steps и выбранных RGBA pixels с допуском1 — функциональный smoke, не fixed-font/full screenshot golden. Фактические проверки и K2/K3/K7–K9 ограничения находятся в STATUS. Следующий этап по согласованному порядку —2.4/K8: контракт ошибки backend и явного пересоздания из модели.

- **5.4. Документация.** Описать запуск, создание виджета, привязки, подключение бекенда, потоковую модель и владение GPU-ресурсами.
- **5.5. Выпуск.** Выпустить alpha после прохождения критериев готовности ниже.

**Порядок независимых работ — 2026-10-10.** По решению пользователя текущий NVIDIA GDI defect учитывается как внешний блокер: безопасное решение со стороны TrueMoon не найдено, дальнейший поиск app-side workaround пока прекращён. Это не отменяет K7 и не разрешает выпуск alpha с failed lifecycle check. Private/handles growth остаётся отдельной неопределённостью. Ближайшая доступная задача — подготовка поставки5.4/5.5: локальные NuGet artifacts и отдельный net10.0 consumer без ProjectReference/compile-link исходников TrueMoon, проверка transitive dependencies и bundled native Skia в build/publish output, запуск raster/OpenGL/Vulkan и App startup/shutdown в Debug/Release на Windows x64, документация подключения и ownership. Подготовка пакетов не является публикацией или выпуском alpha. Затем — K2/K4: доставляемые фиксированные шрифты с лицензией и полноценные golden fixtures общей сцены. Physical DPI/4.9 matrix, real device loss и VRAM evidence сохраняются; PA1/PA2 остаются после alpha. Возврат к NVIDIA K7 — при supported driver/ICD решении либо отдельно согласованной alternate GPU configuration и полном повторном проходе с прежними limits.

## Проверки и критерии готовности

**Уточнение K2/K4 — 2026-10-11.** Opt-in immutable `SkiaFontCatalog` в Rendering.Skia загружает managed snapshots лицензированных font files; backend и Hosting принимают каталог явно, surface сохраняет creator-thread/FIFO16/native font ownership. Unknown alias использует explicit default face, без системного fallback. Стандартный system-font путь, native ABI, net10.0 и xUnit сохраняются. Settings full golden:30CPU PNG (10script frames×3logical scales), точное layout/model/input сравнение и все premultiplied RGBA pixels; raster/golden и OpenGL/Vulkan channel tolerance2, GPU/raster symmetric radius1 envelope с contrast≥8/+24 и strict2 для flat/low-contrast. Нет masked/outlier pixels. Правило учитывает измеренные AA coverage differences, не обещает pixel-identical CPU/GPU или emoji shaping. Normal run read-only, baseline recording только в новый explicit directory после визуальной проверки. [Контракт, assets/license и команды](GOLDEN_TESTS.md). Проверка этого scope не отменяет K3/K7/K8/VRAM/final alpha matrix; следующий независимый общий шаг — CORE5/COB4.

- **K1. Автоматические проверки ядра.** Тесты дерева, layout, свойств, привязок, коллекций, фокуса и маршрутизации ввода без GPU.
- **K2. Рендеринг.** Проверки на raster-поверхности с фиксированными шрифтами; сравнение GPU-результатов с допустимой погрешностью.
- **K3. Windows.** Проверки resize, сворачивания, восстановления и DPI 100/150/200%; отсутствие рисования в поверхность нулевого размера.
- **K4. Сменяемость бекендов.** Одинаковое поведение демонстрационного UI через Vulkan и OpenGL.
- **K5. HUD.** Необработанный ввод проходит в сцену; сохраняется прозрачность; ресурсы хоста не уничтожаются библиотекой.
- **K6. Vulkan validation.** В демонстрационных сценариях нет ошибок синхронизации и времени жизни ресурсов.
- **K7. Повторное создание сессий.** Создание и закрытие сессий не вызывает накопления ресурсов или подписок.
- **K8. Потеря устройства.** Сессия сообщает ошибку хосту и допускает пересоздание. Автоматическое бесшовное восстановление не входит в первую версию.
- **K9. Производительность.** Зафиксировать время layout/отрисовки и аллокации на контрольной сцене. Статичный интерфейс не должен непрерывно перестраивать дерево и создавать графические ресурсы.

### Воспроизводимый итоговый проход alpha

Метод baseline K9 уточнён и реализован 2026-10-07: shared Settings View1,32 строки/195 исходных элементов (до200 при append), view720×1000/viewport900×1100/scale1, static/property-edit/list-edit на raster/OpenGL/Vulkan и Vulkan HUD. Два независимых Release-процесса, warmup≥100frames и≥500ms на case,500 samples; validation выключен при timing и включён в отдельных correctness checks. Owner-thread managed allocations и CPU wall time Update/MeasureCore/ArrangeCore/Render измеряет manual profiling root/surface proxy; вложенные фазы не суммируются повторно. GL completion, Vulkan export/return, HUD composition/presentation учитываются отдельно; GPU timestamps/native/VRAM не измеряются. Raw samples и source/binary/font/native/config fingerprints сохраняются в JSON, итоги — [PERFORMANCE_BASELINE.md](PERFORMANCE_BASELINE.md). Статичный UI проверяет сохранение tree/surface/image,0 layout/draw/Update allocations. Архитектура/runtime API и критерии K1–K9 сохраняются; performance budget не назначен. Следующая оптимизация выбирается после CPU/allocation profile выявленной фазы и проверяется повтором baseline.

Критерии K1–K9 сохраняются; функциональные результаты отдельных подэтапов не заменяют итоговый проход с актуальной демонстрацией. Зафиксировать поддержанную конфигурацию Windows x64, GPU/driver, SDK/packages/native revisions, команды и версии артефактов. Изменение конфигурации требует явно ограничить область вывода. Другие OS/GPU/EXT-only конфигурации — отдельные исследования по выявленным рискам.

- **K1 / controls.** На финальном коде выполнить UI suite, включающий two-way/collections/dynamic regions и реальные interactions всех заявленных контролов. Указать summary и ограничения; failures полного solution suite разбирать отдельно, не скрывая их результатами UI suite.
- **K2/K4.** Один view/model сценарий на raster/OpenGL/Vulkan: фиксированные доставляемые шрифты, одинаковые logical bounds и input results, прозрачность/clipping/text/images и документированный допуск сравнения pixels. Selected-pixel smoke отдельно от полного набора golden fixtures.
- **K3/K5/K6.** Resize/minimize/zero-size/restore и DPI 100/150/200%, включая переход DPI окна; HUD event handling/passthrough и сохранение ресурсов игры. На поддержанном Vulkan пути validation без ошибок синхронизации/lifetime, включая teardown.
- **K7.** Повторяемое создание/закрытие/resize с заранее фиксированным числом циклов; после завершения отсутствие накопления owned ресурсов, source subscriptions и retained roots. Counters не объявлять полным VRAM/driver профилем.
- **K8 / 2.4.** Определить контракт передачи backend/device/context ошибки хосту; прекратить недопустимую работу, выполнить безопасный cleanup и явно создать новую сессию/дерево из сохраняемой модели. Ошибка и recreate должны иметь воспроизводимый failure scenario с указанием, что проверялось реальной потерей устройства, а что контролируемой инъекцией. Автоматическое бесшовное восстановление не включается.
- **K9.** Зафиксировать сцену и размеры, число элементов, static/property-edit/list-edit режимы, warmup и число samples; измерить layout/draw time и allocations. Static UI не выполняет повторный layout/draw и не создаёт новые UI graphics resources; HUD composition и оконный presentation учитываются отдельно. Текущая invalidation приводит к layout от корня, когда layout требуется; dirty-subtree layout/partial redraw не обещаются. Оптимизацию выбирать после baseline и повторного измерения выявленной проблемы.
- **5.4/5.5 / поставка.** Debug/Release publish smoke на заявленной конфигурации, создание view из отдельного consumer с явными references и проверка доставки native assets. Проверить package/NuGet consumer для выбранного способа поставки и описать alpha migration/compatibility ограничения. Исторический успешный publish не заменяет проверку итогового артефакта.

Уточнение K9,2026-10-08: после sampled managed-stack/allocation profile реализован surface-owned paint/FIFO16fonts reuse для общего Skia drawing/text adapter, с owner-thread/lifetime guards и cleanup при eviction/disposal. Свежие baseline до/после и UI/native regression — [PERFORMANCE_BASELINE](PERFORMANCE_BASELINE.md#профиль-и-оптимизация-2026-10-08)/STATUS; это одна ограниченная оптимизация, без dirty-subtree/partial redraw/text-blob cache, public API/native ABI/package изменений. Следующий этап по согласованному порядку —4.9a. Alpha/K1–K9 сохраняются; первый нерегулярный standalone smoke failure учитывается отдельно от повторных успешных checks.

## Допущения и границы первой версии

- Использовать текущий `net10.0` и принятый в решении xUnit.
- Допускаются изменения API прототипа с обновлением примеров.
- Переключение бекенда происходит при создании сессии.
- Поддерживаемая ОС первой версии — Windows. Vulkan HUD проверяется небольшим самостоятельным хостом.
- Linux/macOS, нативные системные контролы, IME, полноценная accessibility-интеграция, сложный редактор текста, виртуализация, docking и дизайнер относятся к следующим этапам.
- Сроки оценивать после графического прототипа, снимающего основную неопределённость интеграции Skia/Vulkan.

## Развитие UI после alpha

Согласовано с пользователем **2026-10-09**. Этот раздел фиксирует будущие работы над TrueMoon.Alloy и TrueMoon.Argentis; статус всех новых пунктов — **запланировано**. Требования alpha, её критерии K1–K9 и текущая точка продолжения по DPI сохраняются. Конкретные публичные API и сроки уточняются при начале соответствующего подэтапа.

### Цель beta и контрольное приложение

Развивать UI на одном регулярно используемом приложении: редакторе настроек и ресурсов с деревом навигации, списком и фильтрацией, формой свойств и журналом операций. Приложение развивается вместе с библиотекой и показывает, где создание UI требует лишней инфраструктуры. Его повторяемые сценарии становятся проверками поведения, диагностики и производительности.

Приоритет первой beta — контролы, формы и инструменты диагностики. Целевой результат: приложение с меню, диалогами, редактируемыми формами и большим списком, которое удобно писать на Alloy и диагностировать. Виртуализация сначала вводится для одного списка контрольного приложения; развитие деревьев и таблиц идёт следующими подэтапами. Сроки и объём остальных направлений определяются по потребностям приложения.

### Направления и порядок работ

| Пункт | Приоритет | Планируемый результат |
| --- | --- | --- |
| PA1. Контрольное приложение | Начало beta | Навигация, фильтрация, редактирование и журнал операций на общей модели; воспроизводимые пользовательские сценарии и перечень неудобств API. |
| PA2. Инструменты разработчика | Первая beta, начиная с PA1 | Инспектор дерева и выбранного элемента: bounds, clipping, фокус, capture, источники свойств, причины invalidation и timings layout/draw. |
| PA3. Общие overlays и команды | Основа контролов первой beta | Единые правила popup/menu/tooltip/dialog для порядка рисования, clipping, размещения, маршрутизации ввода и возврата фокуса. Команды доступны кнопкам, меню и shortcuts, сообщают возможность и состояние выполнения. |
| PA4. Контролы и навигация | Первая beta после PA3 | Menu, context menu, ComboBox, tabs, dialogs, popup/tooltip; согласованная навигация с клавиатуры и shortcuts. Набор вводится по сценариям PA1. |
| PA5. Формы и модель | Первая beta | Валидация, преобразования значений, режимы commit; async loading/cancellation и состояния выполнения без ручного повторения инфраструктуры в каждой форме. |
| PA6. Большие данные | Список в первой beta; дерево и таблицы далее | Виртуализированные списки и деревья, затем таблицы; стабильные ключи, сохранение выделения, фокуса и прокрутки при обновлении и повторном использовании элементов. |
| PA7. Текст и доступность | Клавиатура вместе с PA3–PA5; отдельные следующие подэтапы | IME/composition, развитие текстового ввода, semantic tree и platform accessibility adapter для повседневной работы и автоматизации. |
| PA8. Оформление | PA8a — отдельный запланированный этап для существующих контролов; новые контролы оформлять после определения их поведения | Дефолтный минималистичный цветной стиль и оформление окна — PA8a ниже; системные theme tokens, templates, состояния, transitions и animations развиваются поверх общего поведения. |
| PA9. Vulkan/HUD | Отдельная ветка по реальной интеграции с движком | Асинхронный обмен кадрами, несколько frames-in-flight и явные completion tokens с новым контрактом владения ресурсами. |
| PA10. Производительность | По измерениям PA1 и последующих сценариев | Виртуализация, кеширование текста, уменьшение повторного layout и рисования; для каждого изменения профиль, baseline до/после и проверки корректности. |

### PA3a/PA4a/PA8b. Меню и компактный анимированный заголовок — 2026-10-23

Минимальный профиль ниже реализован2026-10-23 и проверен Editor consumer;
уточнение disclosure и знак TrueMoon реализованы2026-10-24.
[Текущие проверки](HISTORY.md#ed4a-знак-truemoon-и-анимированное-раскрытие--2026-10-24).
[Evidence и границы](HISTORY.md#ed4a-компактный-заголовок-и-меню--2026-10-23);
полные PA3/PA4/PA8, modal stack и hardware acceptance остаются открытыми.

Согласованный consumer — [ED4a Editor](../engine/EDITOR_PLAN.md#ed4a-компактная-верхняя-панель--2026-10-23).
Общие controls/contracts принадлежат UI, прикладные commands/layout — Editor.

1. PA3a: nonmodal OverlayHost с одним активным popup, размещением в пределах
   viewport/переворотом у края, paint/hit testing поверх content, light dismiss,
   Esc/FocusLost и возвратом живого focus target. Popup принадлежит host до Close;
   Close/dispose снимает bindings/capture и не удерживает owner. Detach самого host
   скрывает popup, снимает borrowed owner/focus references; retained popup/tree
   принадлежит caller до Close/reattach/dispose. Detach owner при живом host закрывает
   popup на следующем owner frame. Tooltip
   не получает фокус и не перехватывает clicks. Full dialogs/modal stack — отдельно.
2. PA4a: MenuBar/MenuItem и интерфейс меню, shared command contract с availability,
   icon/text/shortcut, disabled и keyboard navigation (F10, arrows, Enter, Esc).
   Click heading открывает dropdown; hover переключает headings уже открытого меню.
   More остаётся в дереве: hover раскрывает headings; click/Enter/Space раскрывает
   их и открывает первую группу после transition. Повторный click закрывает,
   Esc/Tab/outside/FocusLost/detach отменяют pending open. Первый доступный пункт
   получает фокус без выполнения команды; F10 сохраняет выбор heading.
   Кнопки/меню/shortcuts вызывают одну операцию; async lifetime остаётся у command owner.
3. PA8b: owner-thread frame participation с monotonic time и небольшой числовой
   transition. Reveal180ms, close delay240ms, reversible easing без скачка;
   duration0 — immediate/reduced-motion route. Нет фонового Task/timer на control,
   idle frame requests прекращаются, detach/dispose прекращает работу. Те же reveal
   progress и owner frame поворачивают три точки ⋮→⋯ и открывают первую группу;
   отложенная операция не удерживает IInputContext. Hit bounds More неподвижны;
   обычного бордера нет, hover/pressed/focus состояния сохраняются.
4. Дополнить геометрические UiSymbols (More/Play/Pause/Stop/file/edit/build/theme/TrueMoon),
   сохранить accessible names, tooltip и disabled/hover/pressed/focus состояния.
5. WindowTitleBar получает owned custom header content внутри caption area;
   штатные minimize/maximize/close и native drag/double-click/Snap сохраняются.
   Interactive subtrees — Client, caption — Caption, overlaps исключены на small widths.

Проверки: deterministic frame time/reversal/idle/dispose; overlays bounds/keyboard/
outside click/focus restoration, disabled commands, client/caption region maps,
both DI providers и failed creation cleanup. Standalone raster/native и Editor
source/published proof фиксируются отдельно. Этот bounded slice не закрывает все
PA3/PA4/PA8 или physical DPI/real loss/K7/VRAM acceptance.

#### PA8b: матовые поверхности — 2026-10-24

По запросу пользователя общий механизм реализован в Argentis/Skia; первый consumer —
dropdown меню Editor. BackdropMaterial — immutable value (Gaussian sigma0..64 logical
pixels, tint с alpha, opaque fallback), Element.Backdrop/UiProperties.Backdrop —
typed property с обычным style precedence. Доступны WithBackdrop и ViewStyleRule.Backdrop.
Фон рисуется до обычного Background, foreground и children; непрозрачный Background
может закрыть эффект. Материал не меняет layout, focus, input или ownership.

IBackdropDrawingContext — optional capability без изменения IDrawingContext.
Unsupported backend рисует opaque fallback; null отключает material. Skia берёт
только уже нарисованное в текущем surface/frame, включая earlier materials;
capture ограничен видимой областью с halo3sigma и размерами поверхности.
Scale/translation/clip/rounded bounds учитываются. Snapshot/filter/paint живут
только во время draw; CPU ReadPixels для эффекта не используется. Sigma0/opaque
tint обходятся без snapshot. Дополнительных frame requests/timers нет; изменение
фона обновляет материал при обычном render invalidation без кеша прошлого кадра.

Theme.MenuBackdrop задаёт Dark/Light tint; null возвращает opaque Surface. Обычные
и disabled menu rows прозрачны, hover/pressed и focus indicator остаются различимы.
Проверены raster pixels при scale1/1.5/2 и Editor OpenGL native source/published.
Vulkan adapter подключён к той же реализации и компилируется; отдельная native
Vulkan/HUD effect matrix и performance/VRAM acceptance этим не заявлены. Desktop
за окном, OS acrylic/Mica, arbitrary canvas transforms/layers, saturation/noise,
общая effect graph и blur cache не входят в этот срез.
[Проверки и ограничения](HISTORY.md#pa8b-матовые-поверхности--2026-10-24).

#### PA8b.1: group opacity и panel material — 2026-10-24

Consumer ED4e Editor использует отдельный `Theme.PanelBackdrop` для закреплённых
островков поверх сцены; MenuBackdrop остаётся независимым. Null отключает эффект
и consumer рисует opaque Surface. `UiProperties.Opacity`/`Element.Opacity` — конечное
значение0..1 с render invalidation, без изменения layout/input eligibility.
Нулевая opacity пропускает draw; единица сохраняет обычный путь. Дробная opacity
применяется один раз к composited subtree через optional `IOpacityDrawingContext`;
каждый успешный SaveOpacity балансируется Restore, включая ошибки и nested groups.
Skia реализует transient SaveLayer/paint без retained ресурсов. Backend без
capability рисует дробную opacity непрозрачно; нулевая остаётся скрытой.

Input/visibility admission при закрытии принадлежит consumer: opacity сама по себе
не отключает hit testing, focus или frame participation. Editor отключает child,
оставляя wrapper участником160мс перехода. Acceptance включает raster overlap,
nested groups/images/backdrop, invalid bounds/alpha, style/theme precedence,
balanced state и native OpenGL Editor. Общая effect graph, render cache и
performance/VRAM/physical matrix остаются за рамками этого среза.

#### PA8b.2: оконный заголовок поверх content — 2026-10-24

Consumer ED4f использует opt-in `WindowFrame.TitleBarOverContent` и одноимённую
snapshot/validated Hosting option. Stable Content/TitleBar properties не зависят
от Children paint order. Default frame продолжает резервировать caption height;
overlay frame сначала рисует полное content, затем title и resize grip. Сохраняются
Caption/Client/Maximize hit regions и host-owned close/minimize/maximize actions.

Windows NC caption move/leave должен доходить до logical UiInput даже при
выключенном NativeSnapLayouts. Hook сохраняет default native dispatch для
drag/doubleclick/system commands; owned-HWND probe дополняет UiSession proof,
physical input/Snap matrix остаётся отдельной приёмкой.

WindowTitleBar.OverlayContent использует frame-driven160мс ScalarTransition только
для backdrop: idle tint alpha≤72/sigma0, hover до theme PanelBackdrop. Foreground/
bounds не меняются. `IWindowHeaderActivity.KeepsTitleBarActive` удерживает фон при
custom menu/focus; inactive hover поддерживается, native FocusLost очищает pointer.
Повторные события без смены target не инвалидируют render. Detach/dispose завершают
переход без timers. Null material — opaque Surface; unsupported backdrop — fallback.

OverlayHost.PopupInsets ограничивает measure/place popup внутри safe area,
оставляя основное content полным; значения finite/nonnegative, tiny bounds clamped.
Editor хранит document prompts как contextual retained View в своём layout;
общий OverlayHost остаётся владельцем transient menus/Tooltip выше островков.
Критерии: direct frame/title/placement/hosting tests плюс consumer native OpenGL.
Это не desktop transparency, новый scene transport, general modal stack или
physical/performance/VRAM acceptance. Детальные document tasks — в Editor плане.

#### PA8b.3: статус-бар поверх content — 2026-10-24

Реализовано; [актуальные проверки и границы](HISTORY.md#pa8b3-статус-бар-поверх-content--2026-10-24).

Consumer ED4f.1 включает StatusBar.OverlayContent: idle tint alpha≤72/sigma0,
hover/keyboard focus → Theme.PanelBackdrop за160мс. KeepBackdropVisible — явный
сигнал consumer для важных состояний или popup с focus вне самого бара. Общий
контрол не знает document/runtime semantics. Foreground/Opacity/layout/hit targets
не меняются; тонкий верхний separator проявляется вместе с подложкой. Null material
даёт Surface, unsupported renderer использует opaque fallback.

Frame-driven ScalarTransition без timers, unchanged target без invalidate, detach/
dispose отменяют переход. Анимационные frames не запускают overflow focus return:
он выполняется только по собственному pending flag. Default OverlayContent=false
сохраняет прежний контрол и overflow/input/ownership. Высоту/положение над полным
viewport задаёт consumer. Проверки: deterministic transitions/focus/idle, raster
backdrop/sharp foreground, theme/null и Editor native actual-scene idle/hover.

#### PA8b.4: системный акцент границы окна — 2026-10-24

Реализовано25октября; [проверки и границы](HISTORY.md#pa8b4-системный-акцент-границы--2026-10-25).

По согласованному дизайну opt-in WindowFrame.UseSystemAccentBorder и одноимённая
Hosting option выбирают cached IWindowSystemColors.AccentColor; null возвращает
Theme.Border. По потере native focus RGB умножается на0.6 с reversible переходом
160мс; alpha/толщина/geometry/foreground прежние. Local/binding/element/view styles
для BorderColor сохраняют штатный приоритет. Default opt-out других consumers
сохраняет прежнюю рамку; unsupported host использует theme fallback.

Windows adapter читает именно UISettings.GetColorValue(UIColorType.Accent),
используя документированный IUISettings3 ABI. HSTRING/COM refs/apartment временные,
освобождаются на читающем owner thread. Ошибки активации/HRESULT/API availability
дают null и не блокируют startup. WM_SETTINGCHANGE/WM_THEMECHANGED/
WM_DWMCOLORIZATIONCOLORCHANGED помечают dirty cache; refresh и render request
выполняются после event pump вне WndProc. Polling/native read на frame отсутствует.
Опциональная capability остаётся в Alloy, platform code не входит в Argentis.
Существующий App/Cobalt/IFactory путь создания окна/ownership сохраняется.

Acceptance: accent/update/failure/fallback/recovery, active/inactive160мс/reversal/
idle/detach/opt-out, explicit style precedence, immutable options/early validation,
native Windows source Debug/published Release с actual border pixels и owned-HWND
color/focus notifications. Personalization OS не менять для автоматического proof;
реальная пользовательская смена акцента и physical input остаются отдельными checks.

#### PA4b: StatusBar — 2026-10-24

Согласованный consumer — ED4b Editor. Общий StatusBar и IStatusBar capability
размещают primary/action, ordered collapsible secondary items и details trigger.
Приоритет essential controls, secondary скрываются целыми с exclusion из input/
Tab; overflow доступен потребителю. Child visibility/bindings не переписывать,
скрывать owned slots. Сужение/расширение сохраняет элементы и состояние, возвращает
фокус на details при скрытии focused slot. Bounds конечны и не перекрываются.
Нет registry/plugins/docking framework; список частей фиксируется при создании.
Иконки backend-independent, popup behaviour остаётся общим OverlayHost contract.

Реализовано2026-10-24.20новых layout cases; related UI759/Hosting98/Editor527
Debug/Release =1384/configuration,6native Editor cases/68PNG/12reaped children.
[Точные проверки и границы](HISTORY.md#pa4b-statusbar-и-ed4b-editor--2026-10-24).

Уточнение разметки2026-10-24: стандартный WindowFrame не резервирует отдельную
нижнюю строку под resize grip. Grip16×16 рисуется поверх правого нижнего угла
content; контент и StatusBar доходят до внутренней нижней границы рамки.
Показ/скрытие grip при maximize/fixed-size не меняет доступную высоту content.
Интерактивные controls оставляют место в углу; Editor уже резервирует справа24.
Это заменяет прежнее решение об отдельной нижней строке в default frame.

#### PA4c: TreeView — начат 2026-10-24

Consumer — [ED4c.1 обозреватель сущностей](../engine/EDITOR_PLAN.md#ed4c-обозреватель-сущностей--начат-2026-10-24).
Общий TreeView/ITreeView принимает frozen forest с stable string IDs, parent links,
labels и геометрическими icons. Один выбранный узел, retained expansion, keyboard
navigation, clipped row drawing и ScrollViewer reveal; parent move requests через
drag/drop и keyboard. Контрол не меняет предметные данные и не владеет моделями.
Проверки graph/duplicate/missing/cycle предшествуют замене; максимум4096items в
первом bounded profile. Поддерживаются rejection self/descendant/same-parent и
cancel capture при Esc/FocusLost/disable/detach/new snapshot. Нет фоновых timers,
resolution на кадр или полного пересоздания row-controls при обновлении.
Порядок row siblings следует входным данным. Reorder/rename/multiselect/typeahead,
drag autoexpand/autoscroll и полная virtualization — следующие consumer-driven
расширения, их готовность данным первым срезом не заявляется.

Первый bounded TreeView реализован2026-10-24; editor использует общий контрол,
свой стиль и DI-created View. Прямые TreeView tests и native consumer проверяют
иконки/глубину/clip/выбор/раскрытие/перенос/capture/сохранение дерева при theme.
[Текущие проверки](STATUS.md), [точная область и команды](HISTORY.md#pa4c-treeview-и-ed4c1--2026-10-24).
Следующие rename/reorder capabilities добавляются по ED4c.2 после persistent
engine metadata, без UI-only изменений предметного дерева.

### PA8a: дефолтный стиль контролов и оформление окна — 2026-10-21

По запросу пользователя запланировать единое оформление Alloy/Argentis:
**минималистичные цветные контролы с прямыми углами**, включая рамку окна без
системного оформления и виджеты управления им. Это развитие существующих тем
4.5/4.6 и custom chrome 4.9c/d в рамках PA8. По запросу пользователя **2026-10-22
PA8a0–2 реализованы**, автоматическая часть PA8a3 проверена; physical DPI/OS
acceptance остаётся отдельным открытым пунктом. Отдельный отложенный план
RichTextEditor сохраняется. Этап не требует предварительной
реализации всего набора будущих beta-контролов.

**Визуальное направление.** Плоские цветовые поверхности, прямоугольные контролы
и квадратные кнопки с иконками, радиус углов — 0, тонкие чёткие бордеры, компактные
согласованные отступы и простая типографика. Цвет выделяет действие, выбор,
фокус и состояние ошибки; у текста и значимых границ достаточный контраст.
Фокус, недоступность и ошибки имеют также различимые форму/маркер/состояние.
Точные палитры, наличие светлого/тёмного вариантов, размеры и набор иконок
определены в [спецификации стиля](DEFAULT_STYLE.md) по общей контрольной сцене:
Dark/Light, radius0, border1, focus2, error marker3, title36/buttons32/grip16.
Style Gallery демонстрирует публичные API и смену темы. `UseDefaultWindowFrame`
по умолчанию true только для явно включённого Chrome; обычное системное окно
и приоритет TitleBarFactory сохранены. Новый golden baseline SettingsSquare
сосуществует с неизменённым Settings. Результаты и открытые ограничения —
[в текущем статусе](STATUS.md), подробные проверки —
[в истории](HISTORY.md#pa8a-дефолтный-квадратный-стиль--2026-10-22).

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| PA8a0 | Визуальная спецификация и сверка существующего API | Инвентаризация текущих контролов, тем и WindowFrame; контрольная сцена с вариантами оформления и состояниями. Зафиксированы палитра, типографика, размеры, иконки, focus indicator и границы первой реализации; определены необходимые расширения существующих contracts |
| PA8a1 | Дефолтная тема существующих контролов | Общие semantic tokens для фона/поверхностей, текста, акцента, границ, ошибок и disabled; единые spacing, размеры, толщины линий и состояния. Обычные конструкторы и Fluent-фабрики используют одну тему; local → style → theme → default сохраняется, настройки приложения продолжают работать |
| PA8a2 | Готовое оформление окна без системной рамки | Композиция WindowFrame: видимый бордер, собственный заголовок, кнопки minimize/maximize/restore/close, активное/неактивное окно и видимый resize grip в нижнем правом углу. Команды и доступность отражают реальное состояние/capabilities окна; drag/resize/input согласованы с существующим custom chrome |
| PA8a3 | Общая демонстрация, проверки и подключение | Галерея контролов/состояний и borderless-окно показывают единый стиль; проверены ввод, смена темы и native window behavior на заявленных backend/DPI. Документированы tokens, overrides, подключение и ownership; фактические build/test/native/package результаты и оставшиеся ограничения внесены в STATUS/HISTORY |

**Контролы и состояния.** Охватить существующие Button, CheckBox, TextBox,
RichTextEditor, Slider, ProgressBar, ScrollViewer/scrollbars, Border, контейнеры,
текст и используемые панели инструментов. Описать normal/hover/pressed/focused/
disabled, checked/selected, read-only и error там, где они применимы. Оформление
RichTextEditor опирается на его текущий API; функциональные улучшения остаются в
[собственном плане](RICH_TEXT_EDITOR_PLAN.md). Для будущих menu/tab/popup/dialog
переиспользовать tokens после появления поведения соответствующих контролов.
Смена темы в существующем дереве сохраняет значения, выделение, фокус и прокрутку.

**Окно и виджеты.** Собственный заголовок содержит название и опциональную иконку
окна, drag region и исключённые интерактивные области. Кнопки имеют единые размеры,
иконки, hover/pressed/disabled/focus и доступные названия; close вызывает общий
close request с действующими cancel/save/drain semantics INF4. Maximize/restore
отражает state и capabilities, сохраняет предусмотренную поддержку Snap/системного
меню. Resize grip использует тот же оконный resize contract, что границы/углы:
корректный курсор, hit testing, min/max client size и DPI. У maximized/fixed-size
окон resize grip и соответствующие зоны недоступны. Видимая толщина бордера
задаётся отдельно от удобной области захвата и не перекрывает ввод контента.
Двойной клик по заголовку, восстановление/перемещение и состояния активного окна
согласовать с уже принятым 4.9c/d behavior.

**Архитектура и совместимость.** Визуальные tokens и композиция принадлежат
Argentis; использовать существующие темы, WindowFrame, независимые IWindowCommands/
IWindowChromeHost и WindowRegionMap. Native hit testing, курсоры и оконные операции
остаются в платформенном adapter, интеграция — в Alloy/Hosting. Готовое оформление
подключается через существующую App/Hosting-конфигурацию custom chrome; обычное
окно сохраняет системное оформление по умолчанию согласно 4.9. HUD использует общую
тему контролов, а декорациями окна управляет его host. Конкретные новые имена API
уточняются на PA8a0; готовый набор templates/animations не является предпосылкой
первого визуального этапа.

**Проверки.** Зафиксировать visual fixtures общей сцены и матрицу состояний;
проверить raster/OpenGL/Vulkan, клавиатуру/Tab/активацию, pointer capture,
drag regions, minimize/restore/maximize/close cancellation, resize grip/границы и
повторное открытие окна. Размеры/hit regions проверять при 100/150/200% и переходах
между DPI; физические проверки учитывать отдельно от synthetic. Проверить idle
invalidation и cleanup после смены темы/закрытия. Изменения существующих golden
эталонов допускаются только после явной сверки нового оформления; прежние fixtures
и их результаты сохраняются как отдельный baseline. Сначала behavioral/raster
checks, затем native Debug/published Release и fresh package consumer по
затронутым contracts. Этот этап не закрывает K3/K7/K8/VRAM/final alpha и не
возобновляет остановленный поиск NVIDIA workaround.

### Архитектурные ориентиры

Интеграция2026-10-22: [TrueMoon.Aether.Editor](../../TrueMoon.Aether.Editor/README.md) использует
PA8a default frame/style для основного экрана, central viewport и read-only grid.
Детальные задачи и acceptance приложения принадлежат
[ENG7a](../engine/PLAN.md#eng7a-основной-экран-редактора--2026-10-22);
новые generic Grid/Tree/Docking controls этим этапом не объявлены реализованными.

- **Постоянное дерево и состояние.** Сохранить текущую модель элементов, точечных обновлений и явного lifetime. Повторное использование визуальных элементов при виртуализации требует определённых правил смены модели, подписок, выделения, фокуса и capture. Стабильные ключи коллекций проектируются отдельно от существующего BindItems с reference identity.
- **Overlays.** Сначала определить общий механизм, затем строить меню, popup, tooltip и dialog. Уточнить размещение у границ поверхности, порядок рисования относительно обычного контента, modal/input policy, закрытие и восстановление фокуса. Общий runtime должен работать в standalone и HUD; оконные возможности запрашиваются через независимые контракты.
- **Команды.** Кнопка, меню и shortcut вызывают одну операцию и используют одно состояние доступности/выполнения. Async-команды определяют cancellation и lifetime; изменения UI доставляются на owner thread. Существующие обработчики событий и bindings сохраняют явные правила владения.
- **Forms.** Определить правила conversion, validation, commit/cancel, нормализации модели и показа ошибок до расширения binding API. Async loading задаёт поведение устаревших результатов, cancellation и detach/dispose.
- **Templates и оформление.** Отделить заменяемое визуальное представление контрола от его поведения. Template сохраняет ввод, фокус, доступность и состояния; Fluent API остаётся понятным при композиции и настройке. Для animations определить scheduler, invalidation и остановку при detach/dispose.
- **Текст и accessibility.** Argentis/Alloy содержат независимые input/semantic контракты; native IME и accessibility adapters остаются в платформенной интеграции. Навигация с клавиатуры и focus policy входят в разработку новых контролов с самого начала.
- **Диагностика.** Инспектор показывает актуальные данные runtime и происхождение свойств. Определить lifetime диагностических подписок, чтобы инспектор не удерживал закрытые сессии и элементы.
- **Vulkan/HUD.** PA9 проектирует версионируемое расширение последовательного GPU-контракта: ownership каждого кадра, completion, image layout, reuse/retirement, resize и failure cleanup. Потребность и приоритет проверяются на конкретном engine host. Простое подключение standalone и существующий последовательный путь сохраняются.
- **Производительность.** PA10 опирается на замеры контрольного приложения. Кеш текста и ограничение layout/redraw выбираются по подтверждённым затратам; managed allocations, native ресурсы, GPU completion и presentation учитываются раздельно.

Текущие границы модулей, net10.0/xUnit и владение деревом/ресурсами остаются исходной архитектурой. Перечисленные ориентиры требуют уточнения контрактов и проверок при реализации; добавление раздела не утверждает готовность этих механизмов.

### Планируемые критерии первой beta

- Контрольное приложение проходит повторяемые сценарии меню/диалогов, редактирования с validation/commit/cancel, async операции и большого виртуализированного списка.
- Новые controls/overlays/commands имеют согласованные keyboard/focus/capture/disabled правила; закрытие popup/dialog возвращает фокус, detach/dispose освобождает подписки и отменяет связанную работу.
- Основные общие сценарии подтверждены на raster/OpenGL/Vulkan; переносимый набор контролов и overlays проверен в HUD с явной политикой передачи ввода игре. Фиксированные шрифты и допустимая погрешность сравнения сохраняют требования alpha.
- Инспектор объясняет layout, источники свойств и invalidation; диагностические подписки освобождаются при закрытии UI.
- Для большого списка и обычных форм есть воспроизводимые timings/allocations/resource baseline; static UI сохраняет отсутствие лишнего layout/draw. Число элементов, workload и бюджеты уточняются при подготовке PA1/PA6.
- Документированы создание приложения, новые контракты, compatibility/migration и поставка отдельному consumer. Реализация, фактически прошедшие проверки и ограничения каждого подэтапа отражаются отдельно в STATUS.

IME/accessibility, развитие trees/tables, templates/animations и асинхронный GPU-контракт имеют собственные критерии при старте подэтапов. Их порядок и включение в конкретный выпуск после первой beta уточняются по результатам контрольного приложения.

## Решения и свидетельства из восстановленных логов

Эти записи сохраняют контекст предыдущей сессии. Проверенное состояние текущего дерева находится в [STATUS.md](STATUS.md).

1. Исходный Alloy собирался, но содержал заглушки коллекций и layout; началась переработка ядра.
2. Сообщалось о доступности Vulkan и двух GPU на машине.
3. Сообщалось об отсутствии публичного API обмена Vulkan-семафорами в установленной версии SkiaSharp. Для первой рабочей интеграции была выбрана последовательная передача ресурса с ожиданием завершения GPU. Ограничение по параллельности должно быть отражено в API и документации. Это решение не снимает требований к владению ресурсами и переходам состояния изображений; ограничение API нужно проверить для фактически используемой версии перед реализацией контракта.
4. Первый прототип успешно создал Vulkan-контекст Skia и проверил цвет и прозрачность пикселей на GPU.
5. Последняя заявленная точка работы: независимые контракты Argentis и постоянное дерево элементов; затем подключение layout, ввода и рендерера.

## Поддержание плана между сессиями

Перед работой читать этот план и [STATUS.md](STATUS.md); для межмодульных задач также [общий план](../PLAN.md). После завершённого изменения обновлять дату сверки, статусы затронутых пунктов, результаты фактически выполненных проверок и следующий конкретный шаг. При изменении архитектурного решения фиксировать причину и затронутые критерии готовности. Длинные записи запусков хранить в [HISTORY.md](HISTORY.md), текущий статус оставлять кратким. Общие build/test/release вопросы обновлять в общем STATUS по [правилам](../PLANNING.md).

## ENG2: owned pixel image adapter — 2026-10-17

Для [engine CPU viewport](../engine/PLAN.md#eng2-cpu-frame-viewport--2026-10-17)
Rendering.Skia предоставляет `SkiaImageSource.CopyBgraPixels`: positive checked
shape/exact tight BGRA8 premultiplied/sRGB/top-left length, независимая owned copy.
Borrowing IImageSource/Argentis.Image contract сохранён; caller владеет disposal
после owner-thread drawing use. UI не читает mapped ring slots. Transport/readers/
process ownership и подробные ENG2 задачи ведутся только в engine плане.
[Результаты UI adapter](HISTORY.md#eng2-owned-pixel-image--2026-10-17).
Это ограниченное расширение не меняет alpha blockers или отложенный RTE backlog.

## ENG4d: retained GPU image bridge — 2026-10-20

Rendering.Skia предоставляет `SkiaOpenGLSurface.CopyTexture`: borrowed RGBA8
premultiplied stored-sRGB/top-left GL texture копируется в независимый GPU image,
last source use submitted до return/throw. Caller владеет image на исходном
surface/context owner до UI teardown; raster/foreign drawing context отвергается.
Engine transport/process ownership остаётся в Aether, без UI dependency в нём.
Подробный контракт/acceptance ведётся только в
[engine плане](../engine/PLAN.md#eng4d-gpu-viewport-bridge--2026-10-20).
Reference viewport proof не закрывает ordinary GameEditor GPU integration,
zero-copy/performance или K3/K7/K8/VRAM/final UI acceptance.
