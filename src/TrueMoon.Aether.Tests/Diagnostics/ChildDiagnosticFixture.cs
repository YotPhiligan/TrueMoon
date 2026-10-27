using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

internal static class ChildDiagnosticFixture
{
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    internal static readonly DateTimeOffset Timestamp = new(2026, 10, 27, 9, 10, 11, TimeSpan.Zero);
    internal static ChildDiagnosticDeliveryOptions Options() => new()
    { MaxBatchRecords = 2, MaxBatchBytes = 4096, RecordsPerSecond = 4096, BytesPerSecond = 4 * 1024 * 1024, PollInterval = TimeSpan.FromMilliseconds(10), RequestTimeout = TimeSpan.FromMilliseconds(500), ShutdownTimeout = TimeSpan.FromSeconds(2) };
    internal static SessionLaunch Launch(ChildDiagnosticDeliveryOptions? options = null) => new(
        new(Guid.NewGuid(), 4, new('A', 64), new('B', 64), new('C', 64)), "engine-delivery-" + Guid.NewGuid().ToString("N"), "unused-delivery-tests")
    { DiagnosticDelivery = options ?? Options() };
    internal static DiagnosticRecord Record(long sequence = 1, Guid? application = null) => new(Timestamp, sequence, application ?? Guid.NewGuid(), Environment.ProcessId,
        "Child.Source", DiagnosticLevel.Warning, 701, "Child.Event", "record " + sequence, new Dictionary<string, string?>(),
        typeof(IOException).FullName, "child error", "child stack", "00112233445566778899aabbccddeeff", "0123456789abcdef", "Caller", false);
    internal static DiagnosticRecord SerializationRecord(SessionLaunch launch) => Record() with
    {
        Source = new('S', 130), MessageTemplate = new string('x', 2047) + "😀tail", ExceptionStackTrace = new('e', 5000),
        Properties = new Dictionary<string, string?> { ["nOnCe"] = launch.Identity.Nonce, ["ENDPOINT"] = launch.Endpoint, ["safe"] = launch.Endpoint, ["unicode"] = new string('v', 127) + "😀" }
    };
    internal static DiagnosticDeliveryPacket Packet(SessionLaunch launch, Guid application, long packet = 1, params DiagnosticRecord[] records) => new()
    {
        SessionId = launch.Identity.SessionId, Generation = unchecked((long)launch.Identity.Generation), HostContractId = launch.Identity.HostContractId,
        GameBuildId = launch.Identity.GameBuildId, Nonce = launch.Identity.Nonce, Role = (int)launch.Role, ProcessId = Environment.ProcessId,
        ApplicationId = application, PacketSequence = packet, RecordCount = records.Length, Payload = JsonSerializer.SerializeToUtf8Bytes(records)
    };
    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static async Task UntilAsync(Func<bool> predicate)
    { using var timeout = new CancellationTokenSource(Deadline); while (!predicate()) await Task.Delay(5, timeout.Token); }
    internal static async Task WaitWindowAsync(ChildDiagnosticRate rate)
    { using var timeout = new CancellationTokenSource(Deadline); while (!rate.Enter(0, 0)) await Task.Delay(10, timeout.Token); }
}

internal sealed class DeliveryImporter(Func<DiagnosticRecord, IReadOnlyDictionary<string, string?>?, bool>? import = null) : IDiagnosticRecordImporter, IDisposable
{
    internal readonly ConcurrentQueue<(DiagnosticRecord Record, IReadOnlyDictionary<string, string?> Context)> Calls = new();
    internal int Disposals;
    public bool TryImport(DiagnosticRecord record, IReadOnlyDictionary<string, string?>? context = null)
    { Calls.Enqueue((record, new Dictionary<string, string?>(context ?? new Dictionary<string, string?>()))); return import?.Invoke(record, context) ?? true; }
    public void Dispose() => Interlocked.Increment(ref Disposals);
}

internal sealed class DeliveryServers : IFactory<PipesInvocationServer<IChildDiagnosticDelivery>, PipesInvocationServerArguments<IChildDiagnosticDelivery>>, IDisposable
{
    private readonly ChildDiagnosticDeliveryOptions _options;
    internal int Calls, Disposals;
    internal PipesInvocationServerArguments<IChildDiagnosticDelivery>? Arguments;
    internal PipesInvocationServer<IChildDiagnosticDelivery>? Product;
    internal Exception? Failure;
    internal DeliveryServers(ChildDiagnosticDeliveryOptions? options = null) => _options = options ?? ChildDiagnosticFixture.Options();
    public PipesInvocationServer<IChildDiagnosticDelivery> Create(PipesInvocationServerArguments<IChildDiagnosticDelivery> arguments)
    {
        Interlocked.Increment(ref Calls); Arguments = arguments; if (Failure != null) throw Failure;
        return Product = new(new DeliveryEvents<PipesInvocationServer<IChildDiagnosticDelivery>>(), arguments.Handler, arguments.Endpoint, ChildDiagnosticFactories.Limits(_options));
    }
    public void Dispose() => Interlocked.Increment(ref Disposals);
}

internal sealed class DeliveryClients : IFactory<PipesInvocationClient<IChildDiagnosticDelivery>, string>, IDisposable
{
    private readonly PipesTransportOptions _options;
    internal int Calls, Disposals;
    internal string? Endpoint;
    internal PipesInvocationClient<IChildDiagnosticDelivery>? Product;
    internal Exception? Failure;
    internal DeliveryClients(ChildDiagnosticDeliveryOptions? options = null, bool unboundedFactoryLimits = false) =>
        _options = unboundedFactoryLimits ? new() { MetricsEnabled = false, MaxPendingRequests = 64, RequestTimeout = TimeSpan.FromSeconds(50) } : ChildDiagnosticFactories.Limits(options ?? ChildDiagnosticFixture.Options());
    public PipesInvocationClient<IChildDiagnosticDelivery> Create(string endpoint)
    {
        Interlocked.Increment(ref Calls); Endpoint = endpoint; if (Failure != null) throw Failure;
        return Product = new(new DeliveryEvents<PipesInvocationClient<IChildDiagnosticDelivery>>(), endpoint, _options);
    }
    public void Dispose() => Interlocked.Increment(ref Disposals);
}

internal sealed class DeliveryEvents<T> : IEventsSource<T>
{
    public string Name => typeof(T).FullName!;
    public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
    public void StopActivity(Activity activity) => activity.Stop();
    public void Write(Func<object>? value, string? category = null, string? caller = null) { }
}

internal sealed class DeliveryHandler(Func<DiagnosticDeliveryPacket, CancellationToken, Task<DiagnosticDeliveryReply>> push) : IInvocationServerHandler<IChildDiagnosticDelivery>
{
    // Delegates to the actual generated codec; tests control only the service behavior.
    private readonly IInvocationServerHandler<IChildDiagnosticDelivery> _inner = new TrueMoon.Generated.TrueMoon.Aether.ChildDiagnosticDeliveryInvocationServerHandler(
        new DeliveryService(push), new DeliveryEvents<TrueMoon.Generated.TrueMoon.Aether.ChildDiagnosticDeliveryInvocationServerHandler>());
    public Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default) => _inner.HandleAsync(method, memory, writer, token);
    private sealed class DeliveryService(Func<DiagnosticDeliveryPacket, CancellationToken, Task<DiagnosticDeliveryReply>> action) : IChildDiagnosticDelivery
    { public Task<DiagnosticDeliveryReply> PushAsync(DiagnosticDeliveryPacket packet, CancellationToken cancellationToken = default) => action(packet, cancellationToken); }
}

internal sealed class DeliveryReader : IDiagnosticRecordReader, IDisposable
{
    private readonly object _gate = new();
    private readonly List<DiagnosticRecord> _records = [];
    internal readonly ConcurrentQueue<(long After, int Maximum)> Reads = new();
    internal Func<long, int, IReadOnlyList<DiagnosticRecord>>? OnRead;
    internal Func<long>? OnLatest;
    internal int Disposals;
    public long LatestSequence { get { if (OnLatest != null) return OnLatest(); lock (_gate) return _records.Count == 0 ? 0 : _records[^1].Sequence; } }
    internal void Add(params DiagnosticRecord[] records) { lock (_gate) _records.AddRange(records); }
    public IReadOnlyList<DiagnosticRecord> ReadAfter(long afterSequence, int maximumCount)
    {
        Reads.Enqueue((afterSequence, maximumCount));
        if (OnRead != null) return OnRead(afterSequence, maximumCount);
        lock (_gate) return _records.Where(x => x.Sequence > afterSequence).Take(maximumCount).ToArray();
    }
    public void Dispose() => Interlocked.Increment(ref Disposals);
}

internal sealed class DeliveryRemote : IAsyncDisposable
{
    internal readonly ConcurrentQueue<DiagnosticDeliveryPacket> Packets = new();
    private readonly PipesInvocationServer<IChildDiagnosticDelivery> _server;
    internal DeliveryRemote(SessionLaunch launch, Func<DiagnosticDeliveryPacket, CancellationToken, Task<DiagnosticDeliveryReply>>? push = null)
    {
        var handler = new DeliveryHandler(async (packet, token) =>
        {
            Packets.Enqueue(packet);
            if (push != null) return await push(packet, token);
            var rows = JsonSerializer.Deserialize<DiagnosticRecord[]>(packet.Payload)!;
            return new() { Accepted = rows.Length, LastSequence = rows.LastOrDefault()?.Sequence ?? 0 };
        });
        _server = new(new DeliveryEvents<PipesInvocationServer<IChildDiagnosticDelivery>>(), handler,
            launch.Endpoint + "-diag", ChildDiagnosticFactories.Limits(launch.DiagnosticDelivery!));
    }
    internal DiagnosticDeliveryPacket[] Nonempty => Packets.Where(x => x.RecordCount != 0).ToArray();
    public ValueTask DisposeAsync() => _server.DisposeAsync();
}
