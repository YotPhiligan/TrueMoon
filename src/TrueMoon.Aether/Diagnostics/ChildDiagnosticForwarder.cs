using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Aether.Sessions;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;

namespace TrueMoon.Aether.Diagnostics;

/// <summary>Polls a borrowed bounded history on its own worker and owns one independent diagnostic client.</summary>
public sealed class ChildDiagnosticForwarder : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SessionLaunch _launch;
    private readonly IDiagnosticRecordReader? _reader;
    private readonly ChildDiagnosticDeliveryOptions _options;
    private readonly ChildDiagnosticRate _rate;
    private readonly CancellationTokenSource _end = new();
    private readonly PipesInvocationClient<IChildDiagnosticDelivery>? _client;
    private readonly IChildDiagnosticDelivery? _remote;
    private readonly Task _worker;
    private readonly bool _enabled;
    private Task? _stop;
    private bool _closed, _released, _drainComplete;
    private long _cursor, _target = -1, _packet, _accepted, _rejected, _lost, _unconfirmed, _failures, _rateLimited, _suppressed;
    private Guid _applicationId;
    private string? _lastFailure;
    private readonly IRuntimeDiagnosticObservationSource? _observations;

    /// <summary>Creates a caller-owned forwarder. A missing reader or disabled launch creates no transport.</summary>
    /// <param name="launch">Generation and copied delivery settings.</param><param name="reader">Borrowed source ring.</param>
    /// <param name="clients">Borrowed isolated client factory.</param>
    public ChildDiagnosticForwarder(SessionLaunch launch, IDiagnosticRecordReader? reader,
        IFactory<PipesInvocationClient<IChildDiagnosticDelivery>, string> clients)
        : this(launch, reader, clients, null) { }

    /// <summary>Creates a forwarder with a borrowed memory-only observation capability. No source is disposed by the lane.</summary>
    /// <param name="launch">Fixed generation identity.</param><param name="reader">Borrowed record ring.</param>
    /// <param name="clients">Borrowed transport factory.</param><param name="observations">Optional cached health/device source.</param>
    public ChildDiagnosticForwarder(SessionLaunch launch, IDiagnosticRecordReader? reader,
        IFactory<PipesInvocationClient<IChildDiagnosticDelivery>, string> clients, IRuntimeDiagnosticObservationSource? observations)
    {
        ArgumentNullException.ThrowIfNull(launch); ArgumentNullException.ThrowIfNull(clients); launch.Identity.Validate();
        if (!Enum.IsDefined(launch.Role)) throw new ArgumentOutOfRangeException(nameof(launch));
        _launch = launch; _reader = reader; _observations = observations; _options = (launch.DiagnosticDelivery ?? new() { Enabled = false }).Snapshot();
        _rate = new(_options); _enabled = _options.Enabled && reader != null; _released = _drainComplete = !_enabled;
        try
        {
            if (_enabled)
            {
                _client = clients.Create(ChildDiagnosticReceiver.Endpoint(launch)) ?? throw new InvalidOperationException("Diagnostic client factory returned null.");
                _remote = new ChildDiagnosticDeliveryGeneratedImplementation(new BoundedDiagnosticClient(_client));
                _worker = Task.Run(RunAsync);
            }
            else _worker = Task.CompletedTask;
        }
        catch { _client?.Dispose(); _end.Dispose(); throw; }
    }
    /// <summary>Independent bounded numeric health, without bootstrap secrets or error messages.</summary>
    public ChildDiagnosticStatus Read()
    {
        lock (_gate) return new(_enabled, _closed, _released, _drainComplete, _accepted, _rejected, _lost, _unconfirmed,
            _failures, 0, _rateLimited, _suppressed, 0, 0, _lastFailure);
    }
    private async Task RunAsync()
    {
        Activity.Current = null;
        var token = _end.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                bool closing; lock (_gate) closing = _closed;
                if (closing && _target < 0) _target = _reader!.LatestSequence;
                var records = _reader!.ReadAfter(_cursor, _options.MaxBatchRecords);
                var previousCursor = _cursor;
                var batch = new ArrayBufferWriter<byte>(); batch.Write("["u8); var count = 0; long lastSent = 0;
                var recordBudget = _options.MaxBatchBytes - (_observations == null ? 0 : 128);
                foreach (var record in records)
                {
                    if (closing && record.Sequence > _target) break;
                    if (record.Sequence <= _cursor) continue;
                    if (_applicationId == Guid.Empty && record.ApplicationId != Guid.Empty) _applicationId = record.ApplicationId;
                    if (record.Origin != null)
                    { Consume(record.Sequence); lock (_gate) _suppressed++; continue; }
                    byte[] bytes;
                    try { bytes = JsonSerializer.SerializeToUtf8Bytes(ChildDiagnosticCodec.Bound(record), ChildDiagnosticCodec.Json); }
                    catch (Exception error) { Consume(record.Sequence); lock (_gate) { _lost++; Failed(error); } continue; }
                    if (bytes.Length + 2 > recordBudget)
                    { Consume(record.Sequence); lock (_gate) _lost++; continue; }
                    if (batch.WrittenCount + bytes.Length + (count == 0 ? 0 : 1) + 1 > recordBudget) break;
                    Consume(record.Sequence);
                    if (count != 0) batch.Write(","u8);
                    batch.Write(bytes); count++; lastSent = record.Sequence;
                }
                batch.Write("]"u8);
                var payload = batch.WrittenSpan.ToArray();
                if (_observations != null)
                {
                    byte[]? observation = null; var omitted = false;
                    try
                    {
                        var value = _observations.Read();
                        if (_applicationId == Guid.Empty && value.Collector is { ApplicationId: var application } && application != Guid.Empty) _applicationId = application;
                        observation = RuntimeDiagnosticObservationCodec.Encode(value, _options.MaxBatchBytes - payload.Length - 128);
                        omitted = observation == null;
                    }
                    catch (Exception error) { lock (_gate) Failed(error); }
                    payload = ChildDiagnosticCodec.Envelope(payload, observation, omitted);
                }
                if (closing && count == 0 && _cursor < _target && _cursor == previousCursor)
                { lock (_gate) _lost += _target - _cursor; _cursor = _target; }
                if (_applicationId != Guid.Empty && (count != 0 || !closing))
                {
                    if (!_rate.Enter(count, payload.Length))
                    { lock (_gate) { _lost += count; _rateLimited += count; } }
                    else await SendAsync(payload, count, lastSent, token).ConfigureAwait(false);
                }
                if (closing && _cursor >= _target)
                { lock (_gate) _drainComplete = !token.IsCancellationRequested; break; }
                if (!closing) await Task.Delay(_options.PollInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                lock (_gate) Failed(error);
                try { await Task.Delay(_options.PollInterval, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
        }
    }
    private void Consume(long sequence)
    {
        if (sequence > _cursor + 1) lock (_gate) _lost += sequence - _cursor - 1;
        _cursor = sequence;
    }
    private async Task SendAsync(byte[] payload, int count, long lastSent, CancellationToken token)
    {
        if (_client!.PendingRequestCount != 0)
        { lock (_gate) _lost += count; return; }
        var identity = _launch.Identity;
        var packet = new DiagnosticDeliveryPacket
        {
            Version = _observations == null ? 1 : 2,
            SessionId = identity.SessionId, Generation = unchecked((long)identity.Generation), HostContractId = identity.HostContractId,
            GameBuildId = identity.GameBuildId, Nonce = identity.Nonce, Role = (int)_launch.Role, ProcessId = Environment.ProcessId,
            ApplicationId = _applicationId, PacketSequence = ++_packet, RecordCount = count, Payload = payload
        };
        lock (_gate) { packet.Lost = _lost; packet.Unconfirmed = _unconfirmed; }
        try
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
            request.CancelAfter(_options.RequestTimeout);
            var reply = await _remote!.PushAsync(packet, request.Token).ConfigureAwait(false);
            if (reply == null || reply.Status is < 0 or > 5 || reply.Accepted < 0 || reply.Rejected < 0 || reply.Accepted > count ||
                reply.Rejected != count - reply.Accepted || reply.Status != 0 && reply.Accepted != 0 || reply.LastSequence < 0 ||
                reply.Status == 0 && count != 0 && reply.LastSequence != lastSent)
                throw new InvalidDataException("Invalid diagnostic acknowledgement.");
            lock (_gate) { _accepted += reply.Accepted; _rejected += reply.Rejected; }
        }
        catch (Exception error)
        { lock (_gate) { _unconfirmed += count; Failed(error); } }
    }
    private void Failed(Exception error)
    { _failures++; var name = error.GetType().FullName; _lastFailure = name?.Length > 256 ? name[..256] : name; }
    /// <summary>Drains a captured source cursor, then closes transport. A cancelled/timed-out wait preserves ownership.</summary>
    /// <param name="cancellationToken">Cancels only this wait.</param><returns>Actual cleanup within the configured wait budget.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;
        lock (_gate)
        {
            _closed = true;
            if (_stop == null)
            {
                _end.CancelAfter(TimeSpan.FromTicks(_options.ShutdownTimeout.Ticks / 2));
                _stop = Task.Run(CleanupAsync);
            }
            stop = _stop;
        }
        return stop.WaitAsync(_options.ShutdownTimeout, cancellationToken);
    }
    private async Task CleanupAsync()
    {
        await _worker.ConfigureAwait(false);
        if (_client != null) await _client.StopAsync().ConfigureAwait(false);
        _end.Dispose(); lock (_gate) _released = true;
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
