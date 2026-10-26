using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace TrueMoon.Enerit.IO.Pipes;

// Process-wide instruments aggregate numeric cells, never transports, handlers or callbacks into application code.
// Producers only update numbers. Measurement callbacks run exclusively when an observer collects the instruments.
internal sealed class PipesMetrics : IDisposable
{
    internal const string InstrumentationName = "TrueMoon.Enerit.Pipes";
    private static readonly ConcurrentDictionary<PipesMetrics, byte> Owners = new();
    private static readonly Meter? Meter;
    private static long _clientRejected, _serverRequestsRejected, _serverBytesRejected;
    private long _pending, _queued, _requests, _bytes, _connections;

    static PipesMetrics()
    {
        try { Meter = new(InstrumentationName); } catch { return; }
        Publish(() => Meter.CreateObservableUpDownCounter("truemoon.enerit.pipes.client.pending", () => Sum(static owner => Interlocked.Read(ref owner._pending)), "{request}"));
        Publish(() => Meter.CreateObservableUpDownCounter("truemoon.enerit.pipes.client.queued", () => Sum(static owner => Interlocked.Read(ref owner._queued)), "{request}"));
        Publish(() => Meter.CreateObservableUpDownCounter("truemoon.enerit.pipes.server.requests", () => Sum(static owner => Interlocked.Read(ref owner._requests)), "{request}"));
        Publish(() => Meter.CreateObservableUpDownCounter("truemoon.enerit.pipes.server.request.bytes", () => Sum(static owner => Interlocked.Read(ref owner._bytes)), "By"));
        Publish(() => Meter.CreateObservableUpDownCounter("truemoon.enerit.pipes.server.connections", () => Sum(static owner => Interlocked.Read(ref owner._connections)), "{connection}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.rejected", Rejections, "{request}"));
    }

    internal PipesMetrics() => Owners.TryAdd(this, 0);
    internal void Client(int pending, int queued)
    { Interlocked.Exchange(ref _pending, pending); Interlocked.Exchange(ref _queued, queued); }
    internal void Server(int requests, long bytes)
    { Interlocked.Exchange(ref _requests, requests); Interlocked.Exchange(ref _bytes, bytes); }
    internal void Connections(int count) => Interlocked.Exchange(ref _connections, count);
    internal void RejectClient() => Interlocked.Increment(ref _clientRejected);
    internal void RejectServer(bool bytes)
    { if (bytes) Interlocked.Increment(ref _serverBytesRejected); else Interlocked.Increment(ref _serverRequestsRejected); }
    public void Dispose() => Owners.TryRemove(this, out _);

    private static long Sum(Func<PipesMetrics, long> read)
    {
        long total = 0;
        foreach (var owner in Owners) total += read(owner.Key);
        return total;
    }
    private static IEnumerable<Measurement<long>> Rejections()
    {
        yield return new(Interlocked.Read(ref _clientRejected), new("side", "client"), new("reason", "queue_full"));
        yield return new(Interlocked.Read(ref _serverRequestsRejected), new("side", "server"), new("reason", "requests"));
        yield return new(Interlocked.Read(ref _serverBytesRejected), new("side", "server"), new("reason", "bytes"));
    }
    private static void Publish(Action publish) { try { publish(); } catch { /* Instrument discovery must not fail transport creation. */ } }
}
