# TrueMoon.Enerit

Alpha typed IPC runtime for .NET10. Use the same package version and the same
service/payload sources on both peers. The named-pipe path targets the bounded
Windows x64 profile. MMF, other OS, cross-version schemas and production resource
trends remain separate acceptance. No automatic retry/replay of failed calls.

Reference TrueMoon, TrueMoon.Enerit and TrueMoon.Enerit.Generator explicitly;
the generator reference must use PrivateAssets="all". Add TrueMoon.Mithril for
App/AddUnit owned children. The TrueMoon.Cobalt package includes its analyzer;
no separate Cobalt.Generator package reference is needed. Microsoft DI uses the
TrueMoon.Extensions.DependencyInjection adapter and does not require that analyzer.
Generators require the .NET10 SDK compiler and belong at build time.

Configure UseDiagnostics(), PipesServiceTransport() or
PipesServiceTransport(serviceType => endpoint), then UseInvocationService<T>() on
the caller and ListenInvocationService<T,TImplementation>() on the server.
Both peers must agree on the endpoint without the tm_ prefix. With Cobalt, explicitly
register the local implementation before Listen, so its constructor graph is visible.
App-owned transports participate in Start/Stop and failed-start rollback.

Pipes transports copy and validate `PipesTransportOptions` at registration/factory
construction. Defaults: 64 admitted client requests, 64 admission waiters, 64
server requests, 4 MiB of server request payload and 4 connections (including
the accepting instance). The local request budget is 50 seconds and covers
admission, connection, writing and response; a shorter caller token still applies.

```csharp
context.PipesServiceTransport(_ => endpoint, new PipesTransportOptions
{
    MaxPendingRequests = 16,
    MaxQueuedRequests = 8,
    MaxConcurrentRequests = 16,
    MaxBufferedRequestBytes = 1024 * 1024,
    MaxConnections = 2,
    RequestTimeout = TimeSpan.FromSeconds(5)
});
```

Client admission waits without serializing. A full waiting queue throws
`PipesInvocationOverloadedException` before sending. Server admission is shared
across connections and lasts through handler and response-write cleanup. A
rejected frame is drained with fixed scratch space and answered with a normal
`InvocationException` containing `ENERIT_OVERLOADED`; it never enters the handler.
The payload framing limit remains 1 MiB. Byte accounting covers logical outer
request payloads, not service-owned decoded objects, pool cache or the process heap.

Supported codecs include native scalar values, nullable values/strings, byte Memory,
arrays/lists/dictionaries and supported mutable acyclic DTOs. Unsupported shapes give
TME001. Task/void calls wait for an empty acknowledgement; errors become
InvocationException. Response payloads must be consumed exactly. Cancellation is
local and does not send a cancel frame. Reader reconnect serves later calls only.
After a successful send, local cancellation keeps its client slot until the late
response or disconnect and local send/decode cleanup. Cancelling a partial write
closes that connection; a partially written frame is never replayed. The existing
LateResponse diagnostic is preserved.

App factories borrow IDiagnosticTelemetry for Enerit.Pipes.Client/Server operation
traces and count/duration metrics. Listen to ActivitySource/Meter TrueMoon; metric
dimensions are only operation/outcome. PropagateTraceContext is opt-in (false by
default) and transfers a fixed W3C trace/span/Recorded context before the unchanged
service payload. Legacy requests remain 21 bytes of header; traced requests use a
versioned 51-byte header. New readers accept both; old readers reject traced requests
before dispatch. Update both peers together; no fallback or replay is performed.
Malformed trace IDs are ignored without changing dispatch; malformed/unsupported
header framing closes the connection. Baggage/tracestate and payload logs are excluded.

CaptureRequestEvents is independently opt-in. Request/client/connection IDs remain
in traces/events, never metric dimensions. Local cancellation still retains sent
capacity; late response metadata contains the original request trace/span IDs without
retaining an Activity. Constructors/factories keep their former signatures and add
overloads borrowing telemetry; only the application owns that dependency.

The process-wide `TrueMoon.Enerit.Pipes` meter provides observable resource instruments that
report admitted/queued client calls, active server requests, logical request bytes
and owned connections. `truemoon.enerit.pipes.rejected` is cumulative and has only
three side/reason combinations: client/queue_full, server/requests, server/bytes.
Other resource instruments have no tags. `PipesTransportOptions.MetricsEnabled`
defaults to true and is copied with the limits; it is independent of Core operation
metrics. Observers collect current state, including work admitted before discovery;
measurement callbacks never run in admission/Invoke. Successful drain removes
numeric owner state; cancelling a Stop wait retains it. No transport/handler or
payload is retained by the metric registry. Overload totals live until process exit.

Client, server and standalone connection expose `StopAsync`, `Completion` and implement
`IAsyncDisposable`. Stop fences admission, interrupts IO and drains owned work.
Cancelling the Stop wait does not abandon that drain: keep borrowed handler
dependencies alive until Completion finishes. Disposal from the client's own
decode callback requests stop without waiting for itself. App forwards its stop
token before final disposal. A noncooperative in-process handler cannot be
forcibly aborted; an independently controlled Mithril-owned child can instead be
stopped, then killed/reaped within its process budgets. Process lifecycle control
uses its separate endpoint and does not queue behind saturated domain RPC.

Failed generated decode returns nested byte rentals. Successful response rentals
belong to the caller; validated request rentals belong to the service from entry,
including when it throws. Return each whole rental with TrueMoon.MemoryPoolUtils.TryReturn
after use. Manual unscoped readers/custom callbacks must manage ownership/validation.

The source repository's ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 builds
a local feed and isolated PackageReference consumer, checks analyzer provenance and
runs typed calls/restart/rollback/cleanup. Packages are not published by that script.
