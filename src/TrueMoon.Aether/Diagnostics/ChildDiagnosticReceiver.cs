using System.Globalization;
using System.Collections.Frozen;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;

namespace TrueMoon.Aether.Diagnostics;

/// <summary>Owns one isolated per-generation receiver. The importer remains borrowed through actual drain.</summary>
public sealed class ChildDiagnosticReceiver : IChildDiagnosticDelivery, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SessionLaunch _launch;
    private readonly int _processId;
    private readonly IDiagnosticRecordImporter? _importer;
    private readonly ChildDiagnosticDeliveryOptions _options;
    private readonly ChildDiagnosticRate _rate;
    private readonly PipesInvocationServer<IChildDiagnosticDelivery>? _server;
    private TaskCompletionSource _drained = Completed();
    private Task? _stop;
    private bool _closed, _released, _handling;
    private Guid _applicationId;
    private long _packet, _sequence, _accepted, _rejected, _failures, _duplicates, _rateLimited, _childLost, _childUnconfirmed;
    private string? _lastFailure;
    private readonly bool _enabled;
    private RuntimeDiagnosticObservation? _observation;
    private long _observationOmitted;

    /// <summary>Creates a caller-owned receiver from copied launch options and an injected endpoint factory.</summary>
    /// <param name="launch">Immutable generation identity and optional delivery settings.</param><param name="processId">Owned child PID.</param>
    /// <param name="importer">Borrowed host history; null disables this receiver.</param><param name="servers">Borrowed isolated server factory.</param>
    public ChildDiagnosticReceiver(SessionLaunch launch, int processId, IDiagnosticRecordImporter? importer,
        IFactory<PipesInvocationServer<IChildDiagnosticDelivery>, PipesInvocationServerArguments<IChildDiagnosticDelivery>> servers)
    {
        ArgumentNullException.ThrowIfNull(launch); ArgumentNullException.ThrowIfNull(servers);
        launch.Identity.Validate(); ArgumentOutOfRangeException.ThrowIfLessThan(processId, 1);
        if (!Enum.IsDefined(launch.Role)) throw new ArgumentOutOfRangeException(nameof(launch));
        _launch = launch; _processId = processId; _importer = importer;
        _options = (launch.DiagnosticDelivery ?? new() { Enabled = false }).Snapshot();
        _rate = new(_options); _enabled = _options.Enabled && importer != null; _released = !_enabled;
        if (_enabled) _server = servers.Create(new(Endpoint(launch), new BoundedDiagnosticHandler(
            new ChildDiagnosticDeliveryInvocationServerHandler(this, new AetherEvents<ChildDiagnosticDeliveryInvocationServerHandler>()))));
    }
    internal static string Endpoint(SessionLaunch launch) => launch.Endpoint + "-diag";

    /// <summary>Independent bounded health, without bootstrap secrets or error text.</summary>
    public ChildDiagnosticStatus Read()
    {
        lock (_gate) return new(_enabled, _closed, _released, _released, _accepted, _rejected, 0, 0, _failures,
            _duplicates, _rateLimited, 0, _childLost, _childUnconfirmed, _lastFailure)
        { RuntimeObservation = _observation, ObservationOmittedPackets = _observationOmitted };
    }
    /// <inheritdoc />
    public Task<DiagnosticDeliveryReply> PushAsync(DiagnosticDeliveryPacket packet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        DiagnosticRecord[] records;
        lock (_gate)
        {
            var count = Math.Clamp(packet.RecordCount, 0, _options.MaxBatchRecords);
            if (_closed || !_enabled) return Task.FromResult(Reject(5, count));
            if (packet.Version is not (1 or 2) || packet.ApplicationId == Guid.Empty || packet.PacketSequence <= 0 || packet.Lost < 0 || packet.Unconfirmed < 0 ||
                packet.RecordCount < 0 || packet.RecordCount > _options.MaxBatchRecords || packet.Payload == null || packet.Payload.Length > _options.MaxBatchBytes)
                return Task.FromResult(Reject(1, count));
            var identity = _launch.Identity;
            if (packet.SessionId != identity.SessionId || unchecked((ulong)packet.Generation) != identity.Generation || packet.Nonce != identity.Nonce ||
                packet.HostContractId != identity.HostContractId || packet.GameBuildId != identity.GameBuildId || packet.Role != (int)_launch.Role ||
                packet.ProcessId != _processId || _applicationId != Guid.Empty && packet.ApplicationId != _applicationId)
                return Task.FromResult(Reject(2, count));
            if (packet.PacketSequence <= _packet) { _duplicates += count; return Task.FromResult(Reject(3, count)); }
            if (_handling) { _rateLimited += count; return Task.FromResult(Reject(4, count)); }
            // Charge even malformed JSON bodies before parsing them; invalid data cannot bypass the parse budget.
            if (!_rate.Enter(count, packet.Payload.Length)) { _rateLimited += count; return Task.FromResult(Reject(4, count)); }
            (DiagnosticRecord[] Records, RuntimeDiagnosticObservation? Observation, bool Omitted) decoded;
            try { decoded = ChildDiagnosticCodec.DecodeEnvelope(packet, _options); records = decoded.Records; }
            catch (Exception error) { Failed(error); return Task.FromResult(Reject(1, count)); }
            if (records.Length != 0 && records[0].Sequence <= _sequence)
            { _duplicates += count; return Task.FromResult(Reject(3, count)); }
            _applicationId = packet.ApplicationId; _packet = packet.PacketSequence;
            if (records.Length != 0) _sequence = records[^1].Sequence;
            _childLost = Math.Max(_childLost, packet.Lost); _childUnconfirmed = Math.Max(_childUnconfirmed, packet.Unconfirmed);
            if (decoded.Observation != null) _observation = decoded.Observation;
            if (decoded.Omitted) _observationOmitted++;
            _handling = true; _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return ImportAsync(records, cancellationToken);
    }
    private async Task<DiagnosticDeliveryReply> ImportAsync(DiagnosticRecord[] records, CancellationToken token)
    {
        await Task.Yield(); // Direct callers also avoid running an importer in PushAsync's admission section.
        var accepted = 0; var rejected = 0;
        var context = new Dictionary<string, string?>
        {
            ["SessionId"] = _launch.Identity.SessionId.ToString("D"), ["Generation"] = _launch.Identity.Generation.ToString(CultureInfo.InvariantCulture),
            ["RuntimeRole"] = _launch.Role.ToString(), ["GameBuildId"] = _launch.Identity.GameBuildId
        }.ToFrozenDictionary(StringComparer.Ordinal);
        try
        {
            foreach (var record in records)
            {
                if (token.IsCancellationRequested) { rejected++; continue; }
                try { if (_importer!.TryImport(record, context)) accepted++; else rejected++; }
                catch (Exception error) { lock (_gate) Failed(error); rejected++; }
            }
            lock (_gate)
            {
                _accepted += accepted; _rejected += rejected;
                return new() { Accepted = accepted, Rejected = rejected, LastSequence = _sequence };
            }
        }
        finally { lock (_gate) { _handling = false; _drained.TrySetResult(); } }
    }
    private DiagnosticDeliveryReply Reject(int status, int count)
    { _rejected += count; return new() { Status = status, Rejected = count, LastSequence = _sequence }; }
    private void Failed(Exception error)
    { _failures++; var name = error.GetType().FullName; _lastFailure = name?.Length > 256 ? name[..256] : name; }
    /// <summary>Closes admission and starts actual drain; a timeout/cancelled wait retains ownership for retry.</summary>
    /// <param name="cancellationToken">Cancels only this wait.</param><returns>Actual owned cleanup within the configured wait budget.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;
        lock (_gate) { _closed = true; stop = _stop ??= Task.Run(CleanupAsync); }
        return stop.WaitAsync(_options.ShutdownTimeout, cancellationToken);
    }
    private async Task CleanupAsync()
    {
        if (_server != null) await _server.StopAsync().ConfigureAwait(false);
        Task drain; lock (_gate) drain = _drained.Task;
        await drain.ConfigureAwait(false);
        lock (_gate) _released = true;
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
    private static TaskCompletionSource Completed()
    { var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); completion.SetResult(); return completion; }
}

/// <summary>Explicit owned child identity for receiver creation.</summary>
/// <param name="Launch">Launch containing copied delivery options.</param><param name="ProcessId">Owned child PID.</param>
public sealed record ChildDiagnosticReceiverArguments(SessionLaunch Launch, int ProcessId);
