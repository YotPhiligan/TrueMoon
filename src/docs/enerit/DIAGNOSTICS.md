# Диагностика вызовов Enerit pipes

Профили ENE6d.1/2 от2026-10-26. [Trace contract](PLAN.md#ene6d1-bounded-trace-header-и-ene6c-compatibility--2026-10-26),
[resource metrics](PLAN.md#ene6d2-агрегированные-метрики-ресурсов--2026-10-26),
[текущие проверки](STATUS.md), [Core API](../core/DIAGNOSTICS.md#трассы-и-метрики-операций).

## Подключение

Настройте оба App через прежний публичный путь; options копируются при setup.

```csharp
context.UseDiagnostics(new DiagnosticPipelineOptions());
context.PipesServiceTransport(_ => endpoint, new PipesTransportOptions
{
    PropagateTraceContext = true,
    CaptureRequestEvents = true // optional detailed request events
});
```

Для трассировки/metrics подключите стандартные .NET ActivityListener/MeterListener
или exporter к имени **TrueMoon**. Источником служит borrowed App-owned
IDiagnosticTelemetry. Клиент создаёт **Enerit.Pipes.Client** с ActivityKind.Client;
сервер — **Enerit.Pipes.Server** с ActivityKind.Server и explicit remote parent.
Metric instruments — Core operation count/duration; tags только operation/outcome.
RequestId/ClientId/ConnectionId/service/method не становятся metric dimensions.

Обе trace/event опции по умолчанию false. PropagateTraceContext разрешает отправку и
использование parent context; CaptureRequestEvents включает подробные события.
Local operation traces/metrics независимы от этих опций и sampling выбирается
Core/listener. Без Activity listeners нет искусственного fallback Activity.
Sampling None/metrics-only не вычисляет trace properties. Если уже есть валидный
ambient W3C context, opt-in propagation передаёт его и без нового client span.

PipesServiceTransport требует прежнюю UseDiagnostics-регистрацию source factory;
legacy UseDiagnostics() без pipeline тоже подходит. Generic factories и direct
client/server/connection constructors имеют новые overload с последним borrowed
IDiagnosticTelemetry; прежние signatures сохранены. Владелец транспорта дожидается
StopAsync/Completion, не освобождая заимствованные handler/diagnostics раньше drain.
Сам transport не освобождает telemetry. Повторная App setup заменяет factories,
не создавая transport/telemetry во время конфигурации.

## Метрики ресурсов

Meter **TrueMoon.Enerit.Pipes** предоставляет process-wide агрегаты всех включённых
pipes owners. Дополнительная регистрация сервиса не нужна: App/generic factories
и low-level constructors используют copied PipesTransportOptions.MetricsEnabled
(по умолчанию true). Эта опция независима от Core DiagnosticTelemetryOptions.MetricsEnabled:
Core Meter TrueMoon продолжает отвечать только за operation count/duration.
Tracing и CaptureRequestEvents для resource metrics не требуются.

| Instrument | Вид / unit | Значение |
| --- | --- | --- |
|truemoon.enerit.pipes.client.pending|ObservableUpDownCounter / {request}|Admitted calls до remote reply/disconnect и local writer/decode cleanup|
|truemoon.enerit.pipes.client.queued|ObservableUpDownCounter / {request}|Callers, ожидающие admission без сериализации|
|truemoon.enerit.pipes.server.requests|ObservableUpDownCounter / {request}|Admitted read/queued/handler/response-write work|
|truemoon.enerit.pipes.server.request.bytes|ObservableUpDownCounter / By|Логические outer request payload bytes, удерживаемые admission|
|truemoon.enerit.pipes.server.connections|ObservableUpDownCounter / {connection}|Owned accepting/connected/draining connections|
|truemoon.enerit.pipes.rejected|ObservableCounter / {request}|Накопительные admission refusals с фиксированными side/reason|

Первые пять instruments не имеют tags. Rejected имеет ровно три комбинации:
side=client/reason=queue_full, side=server/reason=requests, side=server/reason=bytes.
При одновременном исчерпании server count/bytes приоритет у requests. Отказ считается
в момент admission decision, до drain/error response; он не означает доставку
ответа. Cancel/timeout, closed gate и malformed header не считаются overload.
Счётчик отказов сохраняется после Dispose продуктов и растёт в течение процесса.
Service/method/endpoint/IDs/error message не добавляют dimensions.

Observer считывает текущее состояние через MeterListener.RecordObservableInstruments
или exporter collection. Наблюдатель, подключённый после admission, также видит
текущие значения. Producers обновляют только числа: measurement callbacks не
вызываются из admission/Invoke/Stop. Сбои discovery не должны срывать создание
транспорта. Throwing/blocking measurement callback затрагивает сбор самим observer.
Пять значений — отдельные snapshots, не атомарная транзакция всей системы.

Numeric cell не хранит transport/handler/Activity/delegates. Один shared server
имеет одну cell для всех connections; standalone connection — собственную, с1owned
connection уже до Start. После успешного drain cell удаляется до Completion;
отмена ожидания Stop не удаляет её. Ошибка cleanup сохраняет регистрацию и текущие
значения (они могут уже быть0). Meter и накопительные overload totals живут до
завершения процесса. MetricsEnabled=false исключает owner из state и totals.
Это не измерение heap/pool cache, service-owned DTO/response buffers или wire bytes.
Queue-wait histograms, timeout/reconnect/error classification и child delivery —
следующие профили; operation duration уже есть в Core.

## Wire и совместимость

Обычный request header остаётся21byte: Guid16, method1, payload length Int32.
При PropagateTraceContext=true и валидном context пишется51byte:

| Offset | Размер | Поле |
| --- | --- | --- |
|0|16|RequestId, прежнее представление Guid|
|16|1|Method code, без изменения generated mapping|
|17|4|Length marker=-2|
|21|1|Trace extension version=1|
|22|1|W3C Recorded flag:0или1|
|23|16|TraceId bytes|
|39|8|SpanId bytes|
|47|4|Фактический payload length|

Далее идёт прежний service payload; его layout и лимит1MiB не меняются.
Server byte admission считает payload, фиксированный header учитывается отдельно
ограниченным числом admitted/queued requests/connections. Responses всегда имеют
прежний21-byte header. Int32/Guid сохраняют существующий native same-build codec;
этот профиль не обещает cross-architecture schema compatibility.

Новый reader принимает оба заголовка. Старый reader отвергает отрицательный
length marker до payload rental/handler. Fallback/replay вызова не выполняется;
reader reconnect позволяет только новые calls. Обновляйте обе стороны вместе.
Это additive request profile; общего negotiation/contract handshake здесь нет.

Нулевая последовательность TraceId/SpanId и flags вне0/1 игнорируются как trace
metadata: вызов сохраняет прежние dispatch/outcome. Unknown version/marker,
invalid payload length и truncated header закрывают connection до handler/rental.
Version проверяется сразу после byte21. Trace context не заменяет session identity,
authorization, method code или domain validation. Tracestate и baggage не передаются.

## Async flow, cancellation и late response

Client context фиксируется на стороне caller до admission/write queue. Изменения
контекста writer thread не перепривязывают вызов. На сервере не наследуется контекст
создания connection/listener: каждый handler начинает отдельный flow с переданным
parent или новым local root. Async/вложенные вызовы продолжают текущую server Activity;
Dispose восстанавливает flow, не смешивая соседние запросы.

Когда server sampling не создаёт Activity, принятые bounded IDs/flags сохраняются
во внутреннем async carrier для вложенных Enerit calls. Новый span не создаётся;
после handler carrier восстанавливается. Disabled/missing/invalid incoming context
очищает carrier, а connection loops не наследуют его от создания транспорта.
Это не общий HTTP/distributed context API: сторонние transports используют свои
propagators или наблюдаемую Activity.

Client span включает admission/connect/send/response/decode. Server span включает
handler, response write и request cleanup; он может закончиться после получения
ответа клиентом. Существующая local cancellation не посылает remote cancel frame.
Client span заканчивается с Cancelled, а capacity остаётся owned до remote
reply/disconnect и cleanup. Pending хранит только bounded context values и IDs,
не Activity/Exception. Legacy LateResponse diagnostic сохранён.

## События

При CaptureRequestEvents=true:

| Событие | ID |
| --- | --- |
|Pipes.Client.Starting/Completed/Cancelled/Failed|2100–2103|
|Pipes.Client.LateResponse|2104|
|Pipes.Server.Starting/Completed/Cancelled/Failed|2200–2203|

Properties содержат Service, RequestId, MethodCode и ClientId либо ConnectionId.
Server дополнительно пишет TraceContext (Remote/Missing/Invalid/Disabled),
ParentTraceId/ParentSpanId. LateResponse пишет исходные RequestTraceId/RequestSpanId
в properties; поле record.TraceId/SpanId не ретегируется через искусственную Activity.
Payloads и endpoint/nonce не записываются. Лимиты strings/properties, delivery queues
и snapshots принадлежат Core pipeline. Сбои diagnostics изолированы от transport;
синхронные legacy callbacks по прежнему должны завершаться без блокировки.

Aether включает propagation на обеих control peers через UseAether/UseAetherRuntime
factories, detailed request events оставляет выключенными. Same-build HostId включает
Enerit assembly; child/host/game пересобираются совместно. Resource metrics выше
включены стандартными options. Дополнительные IPC Start/Stop/latency/error показатели
и bounded child-log delivery остаются отдельными задачами.
