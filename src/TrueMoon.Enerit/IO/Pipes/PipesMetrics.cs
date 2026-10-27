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
    private static long _reconnects, _lateResponses;
    private static readonly long[] ClientFailures = new long[9], ServerFailures = new long[6], ConnectionFailures = new long[6];
    private static readonly long[] WaitCounts = new long[3], WaitTicks = new long[3];
    private static readonly string[] ClientReasons = ["timeout", "cancelled", "stopped", "overload", "serialize", "decode", "remote", "transport", "other"];
    private static readonly string[] ServerReasons = ["handler", "cancelled", "stopped", "overload", "encode", "response"];
    private static readonly string[] ConnectionReasons = ["protocol", "transport", "other"];
    private static readonly string[] WaitOutcomes = ["admitted", "cancelled", "stopped"];
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
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.client.failures", () => Failures(ClientFailures, ClientReasons), "{request}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.server.failures", () => Failures(ServerFailures, ServerReasons), "{request}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.connection.failures", ConnectionErrors, "{connection}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.client.reconnects", () => Interlocked.Read(ref _reconnects), "{connection}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.client.late_responses", () => Interlocked.Read(ref _lateResponses), "{response}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.client.queue.wait.count", QueueCounts, "{request}"));
        Publish(() => Meter.CreateObservableCounter("truemoon.enerit.pipes.client.queue.wait.duration", QueueDurations, "s"));
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
    internal void Reconnected() => Interlocked.Increment(ref _reconnects);
    internal void LateResponse() => Interlocked.Increment(ref _lateResponses);
    internal void ClientFailure(PipesClientFailure reason) => Interlocked.Increment(ref ClientFailures[(int)reason]);
    internal void ServerFailure(PipesServerFailure reason) => Interlocked.Increment(ref ServerFailures[(int)reason]);
    internal void ConnectionFailure(bool server, Exception error)
    {
        var reason = error is InvalidDataException ? 0 : error is IOException or ObjectDisposedException ? 1 : 2;
        Interlocked.Increment(ref ConnectionFailures[(server ? 3 : 0) + reason]);
    }
    internal void QueueWait(TimeSpan elapsed, PipesQueueWaitOutcome outcome)
    {
        Interlocked.Add(ref WaitTicks[(int)outcome], Math.Max(0, elapsed.Ticks));
        Interlocked.Increment(ref WaitCounts[(int)outcome]);
    }

    internal static PipesClientFailure ClassifyClientFailure(Exception error, bool stopped, bool callerCancelled,
        bool timedOut, PipesClientFailure? origin = null)
    {
        // Only an observed cancellation is attributed to a token; a late timer cannot relabel a real failure.
        if (error is OperationCanceledException)
        {
            if (stopped) return PipesClientFailure.Stopped;
            if (callerCancelled) return PipesClientFailure.Cancelled;
            if (timedOut) return PipesClientFailure.Timeout;
        }
        if (origin is PipesClientFailure.Serialize or PipesClientFailure.Decode or PipesClientFailure.Remote) return origin.Value;
        if (error is ObjectDisposedException && stopped) return PipesClientFailure.Stopped;
        if (origin is { } known) return known;
        return error is PipesInvocationOverloadedException ? PipesClientFailure.Overload :
            error is IOException or ObjectDisposedException ? PipesClientFailure.Transport : PipesClientFailure.Other;
    }
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
    private static IEnumerable<Measurement<long>> Failures(long[] counts, string[] reasons)
    {
        for (var i = 0; i < reasons.Length; i++) yield return new(Interlocked.Read(ref counts[i]), new KeyValuePair<string, object?>("reason", reasons[i]));
    }
    private static IEnumerable<Measurement<long>> ConnectionErrors()
    {
        for (var side = 0; side < 2; side++)
            for (var reason = 0; reason < ConnectionReasons.Length; reason++)
                yield return new(Interlocked.Read(ref ConnectionFailures[side * 3 + reason]), new("side", side == 0 ? "client" : "server"), new("reason", ConnectionReasons[reason]));
    }
    private static IEnumerable<Measurement<long>> QueueCounts()
    {
        for (var i = 0; i < WaitOutcomes.Length; i++) yield return new(Interlocked.Read(ref WaitCounts[i]), new KeyValuePair<string, object?>("outcome", WaitOutcomes[i]));
    }
    private static IEnumerable<Measurement<double>> QueueDurations()
    {
        for (var i = 0; i < WaitOutcomes.Length; i++) yield return new(Interlocked.Read(ref WaitTicks[i]) / (double)TimeSpan.TicksPerSecond, new KeyValuePair<string, object?>("outcome", WaitOutcomes[i]));
    }
    private static void Publish(Action publish) { try { publish(); } catch { /* Instrument discovery must not fail transport creation. */ } }
}

internal enum PipesClientFailure { Timeout, Cancelled, Stopped, Overload, Serialize, Decode, Remote, Transport, Other }
internal enum PipesServerFailure { Handler, Cancelled, Stopped, Overload, Encode, Response }
internal enum PipesQueueWaitOutcome { Admitted, Cancelled, Stopped }
