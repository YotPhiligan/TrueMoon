# Диагностика child → host

Первый ENG8a.3/ENE6d.4 профиль2026-10-27. [Контракт и лимиты](PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27),
[текущие проверки](STATUS.md), [Core API](../core/DIAGNOSTICS.md).

## Штатное подключение

Host включает bounded Core pipeline и обычный Aether graph:

```csharp
using TrueMoon.Aether;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Diagnostics;

context.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = diagnosticRoot });
context.UseAether(options =>
{
    options.HostContractId = hostId;
    options.GameBuildId = gameId;
    options.ReportDirectory = reports;
    options.ChildDiagnostics = new ChildDiagnosticDeliveryOptions();
});
```

Options копируются при setup. Default NativeRuntimeFactory использует injected
receiver factory только при наличии IDiagnosticRecordImporter. Он передаёт copied
settings через additive SessionLaunch.DiagnosticDelivery; старый positional
constructor SessionLaunch сохранён. Standard RuntimeEntryPoint включает local
pipeline, копирует эти settings в UseAether и вызывает UseAetherRuntime либо
UseAetherPreviewRuntime. RuntimeHostFactory создаёт owned sender через IFactory.
Новая зависимость Aether на конкретный TrueMoon bootstrap не нужна: общие reader/
importer contracts находятся в TrueMoon.Contracts.

Legacy UseDiagnostics() без bounded pipeline, отсутствующий reader/importer либо
Enabled=false оставляют local-only capture. Прежние standalone NativeRuntimeFactory
constructors не добавляют host receiver. Standalone users могут заимствовать
IDiagnosticRecordReader/Importer и endpoint factories в ChildDiagnosticForwarder/
ChildDiagnosticReceiver; каждый product требует StopAsync/DisposeAsync у владельца.
Custom transport factories должны сохранять отдельные72KiB/1request/1connection
limits и отключённые diagnostic events/traces/metrics для самой delivery lane.

## Поток данных и сохранение контекста

Local Core ring → sender worker → отдельный Enerit endpoint → receiver →
Core TryImport → host ring и прежние sink queues/JSONL. Control endpoint, input,
frames и Mithril lifecycle pipes с этой очередью не объединяются. Raw stdout/stderr
не парсятся как structured events и не импортируются повторно. Их continuous
capture и local child JSONL сохраняются независимо от доставки.

Reader начинает с cursor0 и берёт ещё доступный startup prefix. Missing sequence
slots считаются известной потерей; они могут включать evictions и failed capture.
Ни emitter, ни game/control thread не ждут remote sink. Sender хранит один batch,
не создаёт неограниченную очередь и не публикует delivery errors в пересылаемый
pipeline. Records с Origin!=null подавляются — импортированные данные не идут назад.

Host назначает local ApplicationId/Sequence, сохраняя исходный emitting ProcessId,
UTC timestamp, trace/span, exception strings и truncation. Origin содержит child
ApplicationId/Sequence. Authoritative SessionId/Generation/RuntimeRole/GameBuildId
попадают первыми в общий Core property budget, remote values их не заменяют.
Импорт refreeze/clip и применяет current host filters/bounds/sink queues; он не
re-emits DiagnosticListener. Accepted означает admission в collector, а не обещание
retention или успешной записи во все sinks.

## Bounds и потери

Defaults:16records/32KiB JSON per packet,128records/s,256KiB/s с1024byte envelope
cost, poll50ms, call250ms, shutdown1s. Hard bounds — в плане. Records дополнительно
ограничены source/event/caller128chars, message2048, exception type128/message2048/
stack4096, properties16 с key64/value128. IsTruncated сохраняет факт clipping.
Nonce/Endpoint properties удаляются; capability передаётся только в wire envelope.
Free text сообщений остаётся ответственностью emitting application.

ChildDiagnosticStatus разделяет:

- Accepted — подтверждённые/imported records; Rejected — отказ receiver/importer.
- Lost — missing sequence slots и known local size/rate/backpressure drops;
  RateLimited — соответствующее подмножество. Suppressed — already-imported records.
- Unconfirmed — неизвестный remote outcome при timeout/disconnect/malformed ACK.
  Такой record мог попасть в host; автоматического replay нет.
- Failures/LastFailureType — численные ошибки и bounded type name без exception text.
- ChildReportedLost/ChildReportedUnconfirmed — последние cumulative counters в host,
  полученные от child. Empty heartbeat обновляет их после последнего data batch.
- Closed/Released/DrainComplete — закрытая admission, actual transport cleanup и
  обработка конечного source cursor до отмены. DrainComplete не обещает отсутствие
  известных drops или успешную доставку всех records.

Поздний ACK не переводит уже учтённый Unconfirmed в Accepted: локальный exchange
завершён, replay отсутствует. Это относится к ответам как до Stop, так и после
закрытия клиента. Ожидать Accepted для каждого отправленного batch нельзя;
проверка serialization должна быть независима от исхода ограниченного по времени
exchange. DrainComplete может сочетаться с Unconfirmed и Lost.

NativeRuntimeLease.ChildDiagnosticsStatus сохраняет final receiver snapshot после
reap/cleanup. AetherRuntimeHost.ChildDiagnosticsStatus и runtime cleanup report
содержат sender health. Без связи последний child status может не попасть в host;
local report/JSONL и raw output остаются fallback. Common Core health дополнительно
показывает ring evictions, sink drops/failures и retention.

## Wire, identity и lifecycle

DiagnosticDeliveryPacket version1 переносит exact SessionId/generation/host/game/
nonce/role/owned PID и child appid, packet/record sequences, loss totals и byte[]
JSON. Generation использует exact UInt64 bits в существующем Int64 scalar codec.
Endpoint равен private control endpoint + -diag; его значение не пишется в records.
Same-build Aether fingerprint уже включает assembly с этим DTO. Mixed-version
negotiation/security для произвольных peers этим профилем не обещаются.

Receiver проверяет count/bytes/depth и все rows до первого import. Rate budget
применяется до JSON parse, включая malformed body. Foreign identity/appid,
stale/duplicate sequence, unsupported version и invalid row не импортируются.
Importer false/throw даёт partial rejected ACK; consumed records не повторяются.
Sender проверяет ACK status/count и exact last payload sequence для processed
data packet. Unknown ACK outcome остаётся Unconfirmed. Новый batch не отправляется,
пока прежний locally cancelled call удерживает remote slot.

Lease открывает receiver после получения child PID и оставляет его до process reap
и raw reader drain. Затем Stop закрывает admission и дожидается accepted import.
Child останавливает game/control workers до sender cleanup. Sender фиксирует
конечный source cursor; половина shutdown budget отведена graceful log drain,
затем exchange отменяется и actual client закрывается. Cancelled/timeout wait
не бросает owned cleanup: blocked custom reader/importer требует сохранения owner
и повторного ожидания после освобождения blocker. In-process callbacks не
объявляются принудительно прерванными по timeout.

Full typed UI console/export, global App-private routing, absolute artifact quota,
arbitrary parent-death containment, GPU/display и long trends остаются отдельными
этапами. Этот transport не меняет game/session state и не заменяет typed outcomes.
