# Enerit: план IPC и генерации invocation

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Планируемые semantics и новые API уточняются перед соответствующим этапом.

## Границы

TrueMoon.Enerit, Enerit.Generator, Enerit.Tests и Enerit.Generator.Tests. Invocation client/server, handler resolution, сериализация, pipes/memory-mapped transports и generated service adapters.

Запуск и перезапуск процесса относится к [Mithril](../mithril/PLAN.md), app lifecycle и общие service contracts — к [Core](../core/PLAN.md), DI registration/resolution — к [Cobalt](../cobalt/PLAN.md). Транспорт не обязан управлять lifetime процесса.

## Цель

Два собственных процесса выполняют типизированный invocation через один заявленный transport, передают поддержанные значения, cancellation и ошибки, а завершение client/server не оставляет каналов или ожиданий. Возможности второго transport подтверждаются отдельно.

## Архитектура и решения

Решение2026-10-23: **Enerit выбран как IPC модуль Aether.Editor↔Runtime**.
Управление, input, queries, operation results/progress/errors и frame descriptors/
notifications используют typed invocation и generated adapters; текущая основа —
same-build named pipes. Подробные engine contracts и порядок authoring/preview
ведутся в [Aether](../engine/PLAN.md#ipc-editor-и-runtime-enerit--2026-10-23),
прикладные задачи — в [Editor ED1–ED8](../engine/EDITOR_PLAN.md).
Pixels/shared GPU images передаются отдельным frame transport; Mithril владеет
launch/stop/reap процессов. Локальный authoring API работает без IPC. Enerit не
получает зависимость от Editor/UI или ответственность за scene mutations.
Новые preview/edit invocation contracts ещё запланированы: DTO/limits, fencing,
cancellation/disconnect и wire compatibility проверяются отдельно; решение о
выборе IPC не расширяет текущий readiness или поддержку transport/OS.

Уточнение2026-10-22: [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory)
добавляет типизированные PipesInvocationClientFactory<T>/PipesInvocationServerFactory<T>
с IFactory<TProduct,TArguments>. App invocation registrations получают products
через общий IFactory поверх прежнего transport-specific factory. Wire и generated
handlers прежние; caller владеет динамическими products, зарегистрированные App
transports сохраняют lifecycle.

Сохранять IInvocationClient/IInvocationServer, factories/handlers и ISerializer boundaries. Runtime использует Contracts/Core и свой generator. Transport choice остаётся явным, generated adapters должны соответствовать runtime framing/serialization.

До утверждения production readiness определить framing/size limits, method mapping, совместимость payload, cancellation/timeout/disconnect semantics и владение буферами/каналами. Чтение существующих interfaces не означает, что эти гарантии уже выполнены. Pipes и memory-mapped реализации не объявляются равно проверенными.

### Совместимость с Core ownership — 2026-10-10

Исправление pools/scheduler Core потребовало ограниченных изменений pipes: deserialized bytes используют tracked whole rental; client/server владеют schedulers и connection tasks. Dispose прерывает connect/read/write, отклоняет новые invocation, завершает pending responses и дожидается async handlers до закрытия scheduler, нужного их continuations. Request/response buffers возвращаются в finally. Cancellation/disconnect/invalid или truncated response не оставляют ожидание до штатного50s timeout. Headers читаются полностью, EOF наблюдаем.

Эти контракты подтверждены Windows in-process regression tests ([HISTORY](HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10)). Framing не изменён; own-process transport, общий payload-size policy, MMF и межверсионная protocol compatibility остаются задачами ENE1–ENE3.

### ENE1 ENE2 контракт — 2026-10-11

Принят ограниченный Windows x64/net10.0 pipes baseline: оба peers и generated
adapters собраны из одинакового service/payload source одной версией генератора.
Это уточнение payload/error semantics, а не подтверждение protocol versioning,
межплатформенного ABI или всего G3 composition/restart сценария.

- Request/response frame: Guid (16 bytes), method/status (1 byte), signed payload
  length (4 bytes), затем payload. Допустимы 0–1 048 576 bytes без header.
  Входная длина проверяется до rental/dispatch. Полный frame пишется под одним
  write gate; concurrent responses коррелируются по Guid. Очереди пока без backpressure.
- Generated payloads: bool/byte/int/long/float/double, int-backed enums,
  Guid/DateTime/DateTimeOffset/TimeSpan, nullable поддержанные value types,
  string/string?, Memory<byte>/ReadOnlyMemory<byte>, одномерные arrays,
  List/IReadOnlyList и Dictionary/IReadOnlyDictionary с поддержанными элементами.
  Mutable DTO — nongeneric, nonabstract class без inheritance/records, с единственным
  public parameterless constructor, public readable/writable properties без init-only
  и mutable instance fields. DTO graph acyclic, не глубже32 узлов.
  Null и empty strings/collections/DTO различаются presence marker; bytes имеют
  length без null marker. Nullable string использует один marker своего codec.
- Collections ограничены65 536 elements; отрицательные/слишком большие counts,
  невозможные remaining bytes, state вне0/1, negative/odd UTF-16 lengths и truncation
  дают InvalidDataException. Generated handler отвергает trailing request bytes
  и неизвестный method code; service при этом не вызывается.
- TME001 diagnostic отвергает object, decimal/ValueTask, tuples/custom structs,
  nested collection elements, неподдержанные DTO shapes и static/generic/ref/out
  service methods, properties/events и более256 методов. Declared/inherited methods
  и equivalent signature dedup сохраняются. Method IDs зависят от Roslyn order,
  стабильная схема между разными сборками пока не установлена.
- Каждый вызов, включая void/Task, получает acknowledgement. Status0 — успешный
  payload, status1 — UTF-8 remote exception text, превращаемый в InvocationException.
  Handler throw/reported failure/oversized result также получают error response;
  слишком длинный error text заменяется bounded сообщением. Неизвестный status или
  invalid frame disconnect/fault pending; EOF сохраняется как IOException subtype.
- Request cancellation локальная: pending TCS удаляется/отменяется; отправленный
  handler продолжает работу до завершения или connection shutdown. Late response
  полностью читается и возвращается в pool без reconnect. Timeout50s имеет ту же
  локальную semantics. Retry invocation автоматически не выполняется; reader
  reconnect создаёт новое соединение для последующих вызовов, с возможными
  промежуточными попытками во время server teardown.
- Transport владеет целыми request/response rentals, schedulers и channel.
  ReadBytes создаёт отдельный tracked rental: handler/caller возвращает bytes,
  в том числе вложенные DTO/collections, через TryReturn после использования.
  Автоматический обход/возврат вложенных rentals при partial DTO decode ещё не
  реализован. Synchronous Dispose ждёт handlers, поэтому игнорирование cancellation
  может удерживать shutdown; bounded proof использует cooperative handlers.

**Изменение совместимости:** void/Task теперь ждут remote acknowledgement, nullable
strings больше не получают лишний marker. Старые и новые peers смешивать нельзя.
Scalar encoding использует native layouts; canonical cross-version/architecture
wire format, response trailing-byte validation и schema negotiation остаются открыты.
Старые object/tuple/custom-struct эксперименты MithrilTest исключены из generated
service interface; сохранены обычные методы implementation, Sender их не использует.

Проверяемый runner и границы — [EneritPipesProbe](../../ManualTests/EneritPipesProbe/README.md).
ENE1/ENE2 выполнены как этот bounded baseline; общая production/package readiness
и ENE3–ENE5 из него не следуют.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| ENE0 | Runtime/generator/transport baseline | Discovery/results существующих projects, активные и отключённые transport scenarios перечислены отдельно |
| ENE1 | Serialization и generated mapping contract | Supported values/null/collections и malformed/unsupported payloads имеют проверяемый результат; generated code компилируется и согласован с runtime |
| ENE2 | Один работоспособный IPC transport | Own-process invocation, concurrent requests в выбранном режиме, cancellation/disconnect/error/cleanup подтверждены с фиксированным transport |
| ENE3 | Второй transport и сопоставление capabilities | Общий сценарий повторён, transport-specific limits/failures и ownership задокументированы; неподтверждённый режим остаётся experimental |
| ENE4 | Совместный scenario с Mithril | Unit выполняет invocation, отказывает/завершается, согласованный client/server cleanup и дальнейшее поведение воспроизводятся |
| ENE5 | Package consumer и baseline | Generator/runtime/transport доставлены независимому consumer; измерены latency/throughput/allocations выбранного сценария до оптимизаций |
| ENE6 | Частично: finite admission и async stop первого ENE6a/MIT6a профиля реализованы/проверены2026-10-23 | Остальные compatibility/diagnostics, полный fault/load matrix и scope расширяются по подэтапам ниже |

Начать ENE2 с transport, подтвердившего ENE0; не назначать его успешным только из-за наличия реализации. ENE4 зависит от MIT1–MIT3 и согласованной boundary между channel lifecycle и restart process.

## Критерии готовности

- Typed invocation и serialization соответствуют заявленному API; generation/diagnostics и unsupported values проверены.
- Cancellation, remote error, disconnect, invalid/oversized payload и shutdown имеют согласованный результат без зависших ожиданий.
- Client/server/channel/buffer ownership и пределы ресурсов описаны и проверены.
- Заявленные transports проверены независимо; disabled/commented tests не выдаются за successful run.
- Separate-process и package consumers воспроизводимы, generator delivery проверен вне ProjectReference.

## После первой рабочей проверки

Развивать payload compatibility, diagnostics и throughput по реальному consumer. Reconnection/backpressure и новые transports требуют отдельного contract proposal; сетевой RPC этим планом не обещается.

### ENE6: интерактивный consumer Aether — ревью 2026-10-23

Это предложения по результатам чтения планов и исходников, не утверждённые новые
API или passing proof. Минимальный локальный authoring API Aether не зависит от
этого этапа. Перед частыми preview/input запросами уточнить и проверить ENE6a/b;
ENE6c нужен перед расширением совместимых service contracts, ENE6d — для приёмки
нагрузки. Текущая same-build pipes основа остаётся допустимой в прежних bounds.
У Aether уже есть command deadlines с передачей CancellationToken в Enerit;
сначала проверить достаточность этой композиции. Новый timeout/options API не
вводить, если нужный budget надёжно задаётся существующим токеном.

| ID | Предлагаемая задача | Критерий и границы |
| --- | --- | --- |
| ENE6a | Ограничить admission и ресурсы | Явные options для client pending, server in-flight/queued work, bytes и connections; ожидание слота отменяется/deadline-aware, overload имеет наблюдаемый результат. Admission не должен оставлять неограниченные rentals за ограниченной очередью. Под нагрузкой Stop/control не голодают; конкретную изоляцию/резерв выбрать в composition Aether без универсального priority framework |
| ENE6b | Deadlines, отмена и async stop/drain | Настраиваемый request/connect budget вместо единственного50s default; определить начало budget, cancellation до send/после send и late response. Local timeout не означает rollback или остановку remote handler. Закрыть admission, fault pending, прервать IO и наблюдать handler drain; согласовать с MIT6a, не освобождать dependencies исполняющегося handler. Нельзя обещать принудительный обрыв in-process Task |
| ENE6c | Fail-fast совместимость peers | Минимальный generated contract fingerprint/manifest и protocol version до business dispatch: несовпадение methods/DTO/generator policy отвергается явно, без вызова неверного handler. Решение о handshake/framing и миграции принять отдельно; это exact-match same-build защита, не обещание cross-version interoperability. Aether Hello/session fencing сохраняется как отдельный domain contract |
| ENE6d | Операционная наблюдаемость IPC | Connection/request correlation, method, duration, queue/pending/in-flight/bytes, local timeout/late response/reconnect/overload/error counters через существующие diagnostics. Не писать payload; observer failures не меняют transport outcome. Domain conflict/validation возвращаются typed Aether result, transport failure и неизвестный remote outcome различаются |

Историческая исходная сверка до ENE6a/MIT6a: PipesInvocationClient хранил неограниченный pending map,
CheckConnectedAsync опрашивает IsConnected с Task.Delay(1), timeout50s задан в
InvokeAsync; PipesSignalServerConnection накапливает handler tasks и их request
rentals, Dispose синхронно ждёт Task.WhenAll. Используемый TmTaskScheduler имеет
unbounded channel. Это свойства исходного профиля до первого среза ниже, а не текущая
гарантия отсутствия bounds и не новый измеренный leak/
deadlock. Limits/admission добавлять на границе Enerit; общий scheduler Core не
переписывать автоматически. Awaitable connection signal и сокращение dedicated
threads рассматривать после CPU/allocation profile существующего ENE5 workload.

Общий fault/load acceptance ведётся в [MIT6a](../mithril/PLAN.md#mit6-укрепление-процессов-и-ipc--ревью-2026-10-23).
Проверить sustained overload, cancellation во время admission/write/handler,
disconnect с pending, slow/nonreading peer, malformed/mismatched peer и Stop;
фиксировать upper bounds ресурсов и outcome, а не только число успешных calls.
Remote per-request cancellation — отдельный возможный wire contract: потребитель
не должен выводить его наличие из параметра CancellationToken. Transparent replay
мутаций не вводится; operation identity/revision/recovery принадлежат Aether.

ENE3/MMF, network RPC, streaming framework, canonical cross-architecture codecs и
новые OS не являются prerequisites первого preview. ENE5 CPU/allocation profile
остаётся отдельным измерительным шагом до оптимизаций.

### ENE6d: корреляция и диагностика — 2026-10-25

Согласовано пользователем в рамках общей системы логирования; первый bounded
trace profile реализован26октября ниже, полный ENE6d остаётся частичным.
Event schema/pipeline/sinks определяет
[Core CORE6](../core/PLAN.md#core6-структурированная-диагностика--2026-10-25),
а domain session/build context и доставка child logs —
[Aether ENG8a](../engine/PLAN.md#eng8a-диагностика-сессий-и-процессного-вывода--2026-10-25).

- Передавать operation trace context через process boundary явно: ограниченные
  metadata, parent/client/server activity correlation, connection/request identity.
  Проверить async flow, malformed/missing/disabled context и late responses.
  Формат/versioning/совместимость согласовать с ENE6c до изменения wire/generator;
  trace metadata не определяют authorization, dispatch или domain outcome.
- Emission использует общий injected diagnostics API; slow/throwing/overflowing
  sink не блокирует transport или управление. Не писать request/response payloads.
  Частые duration/queue/pending/bytes/error показатели агрегировать через metrics,
  подробные request events включать bounded capture. Request IDs не metric dimensions.
- Для child log delivery согласовать с Aether отдельные capacity/byte/rate budgets
  и способ изоляции от Stop/input до реализации. Flood diagnostics не голодает
  control, ошибки/потери видны; новый универсальный priority/transport API не вводится
  автоматически. Microsoft.Extensions.Logging/адаптер пока не требуются.

### ENE6d.1: bounded trace header и ENE6c compatibility — 2026-10-26

Первый срез передаёт W3C trace/span IDs и Recorded flag, создаёт Client/Server
операции через borrowed IDiagnosticTelemetry. Имена metrics фиксированы
Enerit.Pipes.Client/Server, tags только operation/outcome; request/client/connection
IDs остаются в traces/events. CaptureRequestEvents=false по умолчанию; detailed
events включаются отдельно. Legacy diagnostics callback failures изолированы.

PropagateTraceContext=false по умолчанию сохраняет legacy21-byte request header.
При opt-in и валидном ambient/client context length=-2 обозначает trace extension
version1: version(byte),flags(byte),TraceId16,SpanId8,actual length(Int32), всего51bytes.
Payload limit1MiB и byte admission считают только service payload; headers ограничены
фиксированным размером. Нет variable strings, tracestate, baggage или payload logs.

Новый reader принимает21/51bytes. Invalid trace/span/flags игнорируются как metadata,
без изменения handler/dispatch. Unknown version/marker, invalid payload length и
truncated header закрывают connection до handler/rental; unknown version проверяется
сразу после version byte. Старый reader отвергает отрицательный marker до dispatch;
автоматического fallback/replay нет. Responses сохраняют21-byte header.
Это additive same-build profile и минимальный ENE6c contract, не общий handshake,
service schema fingerprint negotiation или mixed-version compatibility guarantee.

Client context фиксируется до admission/write queue, server очищает случайный
inherited context и использует explicit remote parent. Missing/disabled/unsampled
context не создаёт fallback Activity. Accepted remote IDs/flags сохраняются во
внутреннем async carrier для nested Enerit calls без server Activity; missing/
invalid/disabled boundary очищает carrier, cleanup восстанавливает его после handler.
Carrier не удерживает Activity/payload и не заменяет общий distributed propagator.
Cancelled client scope заканчивается локально;
pending capacity остаётся owned до позднего response/disconnect. Для late response
сохраняются только bounded IDs/context values, не Activity/Exception; подробное
событие содержит RequestTraceId/RequestSpanId без искусственного повторного span.
Core overload имеет default interface fallback для прежних custom implementations;
точные Client/Server kind/remote-parent semantics предоставляет обновлённый Core owner.

App factories получают telemetry из resolver; standalone constructors прежних signatures
сохранены. Aether явно включает propagation на обеих control peers через штатные
factories; same-build identity включает Enerit assembly. Генератор/service payload
mapping не меняются. Следом — queue/pending/bytes metrics, handler/transport error
классификация и отдельный bounded child-log delivery contract с Aether.

### ENE6d.2: агрегированные метрики ресурсов — 2026-10-26

Следующий согласованный срез реализует queue/pending/request-byte/overload metrics
поверх существующих admission gates; общий Core API и wire не расширяются.
Process-wide Meter TrueMoon.Enerit.Pipes владеет шестью фиксированными instruments:
пять ObservableUpDownCounter текущих client.pending/client.queued/server.requests/
server.request.bytes/server.connections и ObservableCounter rejected.
Полные имена/units и подключение — в [руководстве](DIAGNOSTICS.md#метрики-ресурсов).
State instruments не имеют tags; rejected использует только side/reason и три
комбинации client/queue_full,server/requests,server/bytes. Count refusal имеет
приоритет над byte refusal; closed/cancel/malformed не считаются overload.

PipesTransportOptions.MetricsEnabled=true по умолчанию копируется штатными
App/generic/nongeneric factories и products. Опция независима от Core operation
MetricsEnabled, tracing и detailed events. Disabled owner не входит в агрегаты.
Current state доступен позднему observer, totals отказов живут до process exit.
Каждый enabled admission owner хранит только numeric cell; registry не содержит
transport/handler/delegates. Shared server имеет одну cell, direct connection —
собственную. Удаление только после успешного фактического cleanup до Completion;
cancelled Stop wait/failed cleanup сохраняют state. Существующая pending ownership
после local cancel и server response-write byte retention не меняются.

Producers обновляют только числа, без пользовательских callbacks из admission.
Observable collection даёт отдельные snapshots, не coherent snapshot всех owners.
Setup/discovery exceptions изолированы, measurement callback выполняется только
в consumer collection. Нет per-request logs/extra Activity и не обещается heap/
wire-bandwidth измерение. Числа connections включают accepting/draining instances.
Прямые проверки lifecycle, late observer, shared ownership, exact bounds,
cancel/overload, opt-out/options snapshot и observer faults выполнены в первом
bounded профиле; [результаты и ограничения](HISTORY.md#ene6d2-метрики-ресурсов-и-перегрузки--2026-10-26).
Далее timeout/reconnect/error classification и queue-wait duration, затем отдельный
bounded child-log delivery contract. ENE6d целиком остаётся частичным.

### ENE6d.3: исходы IPC и ожидание admission — 2026-10-27

По следующему запросу пользователя добавляются fixed request/connection failure
classes, local timeout/late response/successful reconnect counters и queue wait.
Общий Core API, wire, cancellation/ownership и method mapping сохраняются.
Meter TrueMoon.Enerit.Pipes расширен до13instruments/37fixed series; names/units/
reason precedence и connection/request различия ведутся в единственном
[руководстве](DIAGNOSTICS.md#исходы-reconnect-и-время-ожидания).

Решение: сохранить pull-only collection предыдущего профиля. Queue wait даёт
ObservableCounter count и cumulative duration seconds по admitted/cancelled/stopped,
не синхронный Histogram.Record из producer. Это позволяет получить mean по окну,
не даёт p95/distribution и не хранит individual samples. Immediate/rejected callers
не считаются ожидавшими queue. Метрики обновляются до admission drain signal.
Observer failures/slow callbacks не вызываются из Invoke/admission/Stop.

Client failure публикуется once при завершении instrumented Invoke; linked OCE
имеет priority Stop/caller/timeout, реальные callback/remote/transport ошибки не
переименовываются поздним timer. Server сохраняет первый request failure class,
response delivery failure учитывает отдельный connection counter. Shared server
заимствует одну numeric cell для всех connections; first-fault fence не допускает
повторного connection count при competing read/write faults. Local Stop исключён,
remote EOF учитывается как transport без предположения о намерениях peer.
Reconnect означает только последующий successful connection, late response
совпадает с прежним validated discard path и не создаёт новый request failure.

Прямые queue/cancel/timeout/rebind/malformed/handler/response proofs,
counter cardinality/metadata, options/обе DI, observer isolation и regression
прежнего resource profile выполнены в первом bounded профиле.
[Проверки и ограничения](HISTORY.md#ene6d3-исходы-ipc-и-ожидание-admission--2026-10-27).
Далее bounded child-log delivery совместно с Aether и оставшиеся domain/frame
aggregates/UI console/export. Полный ENE6d/ENE6c/long-load acceptance не закрывается.

### ENE6d.4: отдельный child diagnostic lane — 2026-10-27

Для Aether выбран отдельный same-build endpoint, existing typed invocation,
raw72KiB/one in-flight/one connection и собственные batch/rate/time budgets.
Не расширяются общие wire framing, method negotiation или scheduler priority API.
Diagnostic events/telemetry/metrics этой lane выключены; source logs остаются
в Core bounded ring/JSONL, raw capture не парсится. При flood control endpoint
не делит queue/IO с logs. Актуальный domain/lifecycle/identity/ACK/loss contract
ведётся только в [Aether ENG8a.3](../engine/PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27).
Core shared reader/importer не зависят от Enerit. Прямые и cross-process проверки
не заменяют ENE6c, arbitrary-peer security или long-load acceptance.
[Реализация и фактические проверки27октября](HISTORY.md#ene6d4-отдельный-child-log-канал--2026-10-27).

### ENE6a и MIT6a: первый контракт — 2026-10-23

Пользователь поручил приступить к следующему шагу. Первый implementation slice
ограничен finite pipes admission, наблюдаемым async stop и own-child load/stop
proof. Wire21-byte header, payload1MiB, generated mapping и same-build requirement
сохраняются; ENE6c/d, MMF и новая priority/streaming платформа не входят в срез.

- PipesTransportOptions валидируются и копируются при App configuration/factory/
  transport construction. Defaults: MaxPendingRequests64, MaxQueuedRequests64,
  MaxConcurrentRequests64, MaxBufferedRequestBytes4MiB, MaxConnections4 (1..64),
  RequestTimeout50s (>0, до24h). Caller cancellation/deadline может быть короче.
- Client admission ограничивает принятые calls и waiters до сериализации.
  Очередь ожидания отменяема, превышение даёт PipesInvocationOverloadedException
  до вызова payload writer. Budget включает admission/connect/send/response.
  Slot остаётся owned до writer cleanup, remote reply/disconnect и окончания
  local decode/Invoke. Sent local cancel не освобождает remote capacity и не
  посылает cancel frame; LateResponse сохраняется. Partial write cancellation
  закрывает текущий channel, replay отсутствует.
- Server admission общий для connections: read/queued/handler/response-write
  count и сумма логических request payload bytes. Отказ перед payload rental/
  handler entry; rejected frame дренируется одним4KiB scratch на connection и
  получает bounded status1 ENERIT_OVERLOADED. Control framing не меняется.
  Counts не являются process heap budget и не включают service-owned decoded
  objects или pool cache. Accepted/waiting/draining connections также ограничены.
- StopAsync закрывает admission и IO, завершает pending и наблюдает фактический
  drain. Cancelling caller wait не отменяет ownership: Completion остаётся
  незавершённым, borrowed dependencies живут до его исхода. Callback cancellation
  errors сохраняются после cleanup; один failed callback не пропускает drain.
  Decode-callback self-dispose только запрашивает stop и не ждёт самого себя.
  App передаёт shutdown token в IStoppable до успешной final disposal.
- Stop/control isolation первого профиля обеспечивается отдельным lifecycle
  pipe Mithril; доменный overload не обещает произвольные IPC priority lanes.
  Cooperative child drains; uncooperative child завершается владельцем через
  существующие graceful/kill/reap budgets. In-process Task не объявляется
  прерванным по timeout. Полный acceptance и границы — у MIT6a.

Реализация и фактически завершённые source/published/package проверки ведутся в
[STATUS](STATUS.md); эта запись не заменяет результаты и не закрывает ENE6 целиком.

### ENE4 App registration и lifecycle — 2026-10-14

Детальный совместный acceptance ведётся один раз в
[MIT4/ENE4 контракте Mithril](../mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14).
Transport остаётся независимым от process controller; same-build pipes scope
ENE1/ENE2 сохраняется. MMF ENE3 из этой интеграции не следует.

Для App composition generator публикует typed registration factories в
InvocationServiceStorage. Это compiled metadata, а не global service instances.
Pipes internal services также создаются explicit factories, без зависимости на
Cobalt. Новые generated factories требуют runtime с соответствующими overloads;
старые metadata Register/RegisterHandler без factory сохранены, но type-based
fallback сохраняет ограничения используемого resolver.

PipesServiceTransport(Func<Type,string>) выбирает service endpoint без `tm_`;
peers должны согласовать его сами, default overload не изменён. Выбранные null/
empty/blank names не подменяются default. UseDiagnostics обеспечивает требуемую
IEventsSourceFactory. Local consumer implementation должна иметь явную видимую
Cobalt registration; Listen сохраняет существующий service и всё равно запускает
listener. Use сохраняет existing local registration и не создаёт remote client.
Повторный вызов idempotent. Owned transports участвуют в App Start/Stop rollback;
собственные предварительно зарегистрированные transports сохраняют ownership.

На момент ENE4 следующим локальным шагом до ENE5 была строгая validation response
payload и cleanup nested ReadBytes rentals при частично неуспешном decode;
они реализованы следующим этапом2026-10-14 ниже, не остаются текущим backlog.
Backpressure, uncooperative shutdown и schema/version negotiation требуют
отдельного решения; это не обещание production readiness после ENE4.

### ENE1/ENE2 strict response и decode ownership — 2026-10-14

Уточнение перед independent ENE5/MIT5 consumer. Same-build pipes framing и
supported payload shapes сохраняются; корректный wire format не меняется.

- Generated typed response должен потребить payload полностью: лишний byte,
  truncation, invalid state/length дают InvalidDataException до возврата результата.
  Null marker — полный null payload, successful void/Task acknowledgement пуст.
  Read<bool> принимает только0/1. Duplicate/null dictionary keys отвергаются как
  InvalidDataException, без частичного результата. Scalar enum/domain ranges этим
  structural validation не ограничиваются.
- Status1 text — строгий UTF-8. Malformed text даёт InvalidDataException, fault
  pending и reconnect; matched request остаётся в pending map до успешного decode
  error text. Valid empty/Unicode exception message сохраняется. Decode failure
  внутри корректного success frame fault только этого invocation; frame уже прочитан,
  следующее сообщение может быть обработано без reconnect.
- Явный PayloadDecodeScope принадлежит одному synchronous decode. Все ReadBytes,
  включая вложенные DTO/arrays/lists/dictionaries/keys/parameters, регистрируются
  у общего root scope. При failure scope возвращает каждую whole rental; обход
  partially assigned object graph и ambient/static ownership не используются.
- Response scope Commit выполняется после полного decode и length validation.
  Caller становится владельцем всех whole nested rentals и возвращает их после
  использования. Handler Commit выполняется после decode ВСЕХ parameters и
  validation, перед service entry. После этого service отвечает за эти rentals,
  включая свой throw/cancellation; automatic return после service вызова не добавляется.
- Public DTO Deserialize сохраняет prefix/ref offset API и переносит rentals
  caller после успешного prefix decode; остаток span может содержать следующее значение.
  Internal nested reader заимствует root scope и самостоятельно не Commit.
  Throwing user setter/constructor сохраняет исходную ошибку, failure rentals возвращены.
  Partial failure не обещает откат offset или внешних side effects DTO user code.
- Opaque manual IInvocationClient decode callback должен сам обеспечить shape/
  consumed-length validation и nested ownership; transport знает только границы frame.
  Unscoped ручной SerializationUtils.ReadBytes сохраняет существующий contract.

Проверки: direct scope ownership/guards, реальные compiled generated callbacks и
handlers с malformed и valid prefixes/collections/DTO, raw pipes invalid ack/error,
последующее сообщение и outer response rental cleanup. Затем повторить App36 и
isolated pipes12 Debug/published Release после generator changes. MMF/old generated
code/schema negotiation/backpressure/uncooperative shutdown/other OS и performance
не объявляются готовыми. Новая generation требует runtime с PayloadDecodeScope.

### ENE5 package portion — 2026-10-14

Совместный detailed consumer contract ведётся один раз у
[Mithril MIT5/ENE5](../mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14).
Explicit Enerit analyzer/runtime доставляются в independent same-build pipes App
consumer на Cobalt/DI; Microsoft DI также без Cobalt analyzer. Generator-only layout,
compiler path/SHA, metadata/source/version и deployed own child проверяются отдельно
от source ProjectReference suite. Correct codecs/decode ownership contract сохраняется.

Package portion не означает полного ENE5: следующий отдельный шаг — измерить
latency/throughput/allocations выбранного scenario до оптимизаций. Operational
process diagnostics принадлежат MIT5; MMF ENE3, backpressure/schema/uncooperative
shutdown и другой compiler/platform требуют собственных решений/checks.

### ENE5 измерительный контракт — 2026-10-15

После завершённого bounded Core quality выбран reproducible same-build pipes
baseline. [Постоянный workload/runner](../../Benchmarks/TrueMoon.Enerit.Benchmarks/README.md)
использует реальный generated client/handler/DTO, без App/DI/Mithril и без
изменения runtime. Ось сравнения — размер/shape payload и concurrency; этот run
фиксирует исходную точку для будущих before/after измерений, не speedup.

10cases: scalar, byte echoes256/16384/65536 и DTO с1024byte buffer, каждый с1/8
outstanding calls по одному connection.10verify pairs проверяют результат и
cleanup. Затем3fresh client/server pairs на case, rotating order, warmup≥1.5s,
pilot512calls и3calibrated batches примерно1s (minimum250ms). Все90samples
сохраняют raw per-call Stopwatch ticks, elapsed/opcount, allocations и GC counts.

RTT заканчивается после typed await до validation/return rental. Batch throughput
включает validation/return/worker/clock overhead. Client/server allocations —
GC.GetTotalAllocatedBytes(true) по всем потокам каждого процесса, включая service
и небольшие harness/control/batch-boundary расходы; это не retained/native memory
и не MemoryDiagnoser одного потока. Управляющий stdin/stdout вне client окна;
server begin acknowledgement/end read overhead не вычитается. Report JSON/startup/
connect/raw arrays вне измерения, принудительного GC и изменения power plan нет.

Case summary — median9sample metrics, с throughput min/max; это3независимых pairs,
а не9независимых процессов. Closed-loop tails не являются open-loop SLA. Machine
frequency/background/OS caches не контролируются. Source/generated/published binary/
raw-report hashes и runtime/GC/CPU/SDK фиксируются; окончательные hashes/power plan
сверяются перед Completed. Матрица не обещает MMF/other OS/RID/AOT/package timings.

Критерий завершения этой части: полный10/30pair/90sample run без invocation errors,
current runtime/generator regression отдельно Debug/Release, датированный отчёт
с ограничениями и выбором следующего шага. ENE5 package portion сохраняет собственную
дату/доказательства; один baseline не завершает все будущие quality/API roadmap задачи.

Проверено2026-10-15:10verification/30measurement pairs,90samples/571944RPC,
runtime126/generator114 в каждой Debug/Release. [Числа и ограничения](BASELINE.md),
[команды/артефакты](HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Измерительная часть ENE5 завершена в этом scope; optimization не выполнялась.
Client all-thread allocation2.2–3.0KB/call и scalar/concurrency overhead требуют
отдельного allocation/CPU profile перед оптимизацией. Это measured backlog,
не подтверждённый correctness blocker и не автоматическое требование MMF/новых API.
Следующий общий функциональный этап — Mithril MIT5 operational diagnostics.
Generated nullable-context warnings CS8669 сохраняются отдельным quality backlog;
новый benchmark проявляет10таких warnings, массового подавления нет.
