using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClient<T> : PipeConectionHandler, IInvocationClient<T>, IStoppable, IAsyncDisposable
{
    private readonly IEventsSource<PipesInvocationClient<T>> _eventsSource;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly Dictionary<Guid, Request> _responses = [];
    private readonly ConcurrentDictionary<Task, byte> _writes = new();
    private readonly PipesTransportOptions _options;
    private readonly IDiagnosticTelemetry? _telemetry;
    private readonly PipesMetrics? _metrics;
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly PipesClientAdmission _admission;
    private readonly Task _listener;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<bool> _decoding = new();
    private Task? _stopTask;
    private bool _disposed;
    private volatile bool _connected;

    private sealed class Request(Guid id, PipeStream stream, byte method, ActivityContext? context)
    {
        internal readonly Guid Id = id;
        internal readonly PipeStream Stream = stream;
        internal readonly byte Method = method;
        internal readonly ActivityContext? Context = context;
        internal readonly TaskCompletionSource<ReadOnlyMemory<byte>> Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Sent, WriteFinished, RemoteFinished, LocalFinished, Released;
    }

    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource)
        : this(eventsSource, typeof(T).FullName!) { }

    /// <summary>Creates a client for an explicitly scoped endpoint with finite default limits.</summary>
    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource, string pipeName)
        : this(eventsSource, pipeName, new PipesTransportOptions()) { }

    /// <summary>Creates a caller-owned client; options are validated and copied before any IO starts.</summary>
    /// <param name="eventsSource">Borrowed diagnostics.</param><param name="pipeName">Endpoint without tm_ prefix.</param>
    /// <param name="options">Resource and local request limits.</param>
    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource, string pipeName, PipesTransportOptions options)
        : this(eventsSource, pipeName, options, null) { }

    /// <summary>Creates a client borrowing operation telemetry; the caller owns the client until actual stop completion.</summary>
    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource, string pipeName, PipesTransportOptions options,
        IDiagnosticTelemetry? telemetry) : base(pipeName)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Snapshot();
        _eventsSource = eventsSource;
        _telemetry = telemetry;
        _metrics = _options.MetricsEnabled ? new PipesMetrics() : null;
        try
        {
            _admission = new(_options, _metrics);
            _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
            _taskScheduler = new TmTaskScheduler($"{Name}_write", 2);
            _token = _cts.Token;
            _listener = Task.Factory.StartNew(ListenAsync, CancellationToken.None, TaskCreationOptions.LongRunning, _listenTaskScheduler).Unwrap();
        }
        catch { _metrics?.Dispose(); throw; }
    }

    public bool IsConnected => _connected;
    /// <summary>Slots held until both local send cleanup and remote reply/disconnect, including locally cancelled sent calls.</summary>
    public int PendingRequestCount => _admission.Pending;
    /// <summary>Callers awaiting admission without serializing payloads.</summary>
    public int QueuedRequestCount => _admission.Queued;
    /// <summary>Completes after requested stop has drained all owned IO, writers and admission waiters.</summary>
    public Task Completion => _completion.Task;

    private async Task ListenAsync()
    {
        // A connection loop must not inherit the operation that happened to construct its owner.
        Activity.Current = null;
        PipesDiagnostics.RemoteContext = null;
        while (!_token.IsCancellationRequested)
        {
            PipeStream? stream = null;
            try
            {
                await ConnectAsync(_token);
                stream = PipeStream;
                lock (_responses)
                {
                    if (_disposed) return;
                    _connected = true;
                }
                PipesDiagnostics.Legacy(() => _eventsSource.Write(() => "Connected"));
                while (!_token.IsCancellationRequested)
                {
                    var (guid, status, length) = stream.GetResponseHeader();
                    if (status is not (0 or 1)) throw new InvalidDataException($"Unknown invocation response status {status}.");
                    Memory<byte> memory = default;
                    try
                    {
                        if (length > 0) { memory = MemoryPoolUtils.Create(length); stream.ReadFullBuffer(memory); }
                        var error = status == 1 ? PipesProtocol.DecodeError(memory.Span) : null;
                        var late = false;
                        Request? pending;
                        lock (_responses)
                        {
                            if (!_responses.Remove(guid, out pending)) late = true;
                            else
                            {
                                var request = pending;
                                request.RemoteFinished = true;
                                if (status == 1) late = !request.Response.TrySetException(new InvocationException(error!));
                                else if (request.Response.TrySetResult(memory)) memory = default;
                                else late = true;
                                ReleaseIfFinished(request);
                            }
                        }
                        if (late)
                        {
                            PipesDiagnostics.Legacy(() => _eventsSource.Write(() => $"Discarded late response {guid}", "LateResponse"));
                            PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, 2104, "Pipes.Client.LateResponse", () =>
                            {
                                var properties = PipesDiagnostics.Properties(typeof(T).FullName!, guid, _clientId, pending?.Method ?? 0, false);
                                properties["RequestTraceId"] = pending?.Context?.TraceId.ToHexString();
                                properties["RequestSpanId"] = pending?.Context?.SpanId.ToHexString();
                                return properties;
                            });
                        }
                    }
                    finally { memory.TryReturn(); }
                }
            }
            catch (Exception error)
            {
                lock (_responses)
                {
                    _connected = false;
                    FailPending(error, stream);
                }
                Reset(stream);
                if (!_token.IsCancellationRequested)
                {
                    try { await Task.Delay(10, _token); }
                    catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
                }
            }
        }
    }

    // All request state transitions are serialized by _responses; a cancelled caller does not release remote capacity.
    private void ReleaseIfFinished(Request request)
    {
        if (request.Released || !request.WriteFinished || !request.RemoteFinished || !request.LocalFinished) return;
        request.Released = true;
        _admission.Release();
    }

    private void FailPending(Exception error, PipeStream? stream = null)
    {
        foreach (var request in _responses.Values.Where(r => stream is null || ReferenceEquals(r.Stream, stream)).ToArray())
        {
            _responses.Remove(request.Id);
            request.RemoteFinished = true;
            request.Response.TrySetException(error);
            ReleaseIfFinished(request);
        }
    }

    private async Task CheckConnectedAsync(CancellationToken token)
    {
        while (!IsConnected)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(1, token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
    }

    public async Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action,
        Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        lock (_responses) ObjectDisposedException.ThrowIf(_disposed, this);
        var requestId = Guid.NewGuid();
        IReadOnlyDictionary<string, string?> Properties() => PipesDiagnostics.Properties(typeof(T).FullName!, requestId, _clientId, methodCode, false);
        using var operation = PipesDiagnostics.Start(_telemetry, "Enerit.Pipes.Client", ActivityKind.Client,
            Activity.Current == null ? PipesDiagnostics.RemoteContext : null, Properties);
        // Capture before admission/scheduling; writer threads never infer a parent from their own async context.
        var context = PipesDiagnostics.CurrentContext();
        PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, 2100, "Pipes.Client.Starting", Properties);
        try
        {
            using var timeout = new CancellationTokenSource(_options.RequestTimeout);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken, _token);
            var requestToken = cancellation.Token;
            await _admission.EnterAsync(requestToken).ConfigureAwait(false);
            var ownsSlot = true;
            Request? request = null;
            TResult result;
            try
            {
                await CheckConnectedAsync(requestToken).ConfigureAwait(false);
                lock (_responses)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (!_connected) throw new IOException("Invocation connection closed before admission.");
                    request = new(requestId, PipeStream, methodCode, context);
                    _responses.Add(request.Id, request);
                    try
                    {
                        var write = Task.Factory.StartNew(() => SendAsync(request, methodCode, action, requestToken),
                            CancellationToken.None, TaskCreationOptions.PreferFairness, _taskScheduler).Unwrap();
                        _writes.TryAdd(write, 0);
                        _ = write.ContinueWith(t => _writes.TryRemove(t, out _), CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                        ownsSlot = false;
                    }
                    catch { _responses.Remove(request.Id); throw; }
                }
                var registration = requestToken.Register(() => request.Response.TrySetCanceled(requestToken));
                _ = request.Response.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var memory = await request.Response.Task.ConfigureAwait(false);
                var previous = _decoding.Value;
                try { _decoding.Value = true; result = func(memory); }
                finally { _decoding.Value = previous; memory.TryReturn(); }
            }
            finally
            {
                if (ownsSlot) _admission.Release();
                else lock (_responses) { request!.LocalFinished = true; ReleaseIfFinished(request); }
            }
            operation?.Complete();
            PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, 2101, "Pipes.Client.Completed", Properties);
            return result;
        }
        catch (Exception error)
        {
            var cancelled = error is OperationCanceledException;
            operation?.Complete(cancelled ? DiagnosticOperationOutcome.Cancelled : DiagnosticOperationOutcome.Failed, error);
            PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, cancelled ? 2102 : 2103,
                cancelled ? "Pipes.Client.Cancelled" : "Pipes.Client.Failed", Properties, error);
            throw;
        }
    }

    private async Task SendAsync(Request request, byte method, Action<IBufferWriter<byte>>? action, CancellationToken token)
    {
        var success = false;
        try
        {
            token.ThrowIfCancellationRequested();
            using var writer = new ArrayPoolBufferWriter<byte>();
            var lengthPosition = PipesRequestHeader.Write(writer, request.Id, method, _options.PropagateTraceContext ? request.Context : null);
            action?.Invoke(writer);
            var length = writer.WrittenCount - lengthPosition - sizeof(int);
            PipesProtocol.ValidatePayloadLength(length);
            MemoryMarshal.Write(writer.WrittenSpanWritable.Slice(lengthPosition, sizeof(int)), in length);
            await WriteFrameAsync(writer.WrittenMemory, token, request.Stream, () =>
            {
                lock (_responses)
                {
                    token.ThrowIfCancellationRequested();
                    if (!_responses.ContainsKey(request.Id)) throw new IOException("Invocation connection closed before send.");
                    request.Sent = true;
                }
            }).ConfigureAwait(false);
            success = true;
        }
        catch (Exception error) { request.Response.TrySetException(error); }
        finally
        {
            lock (_responses)
            {
                request.WriteFinished = true;
                if (!request.Sent || !success)
                {
                    _responses.Remove(request.Id);
                    request.RemoteFinished = true;
                }
                ReleaseIfFinished(request);
            }
        }
    }

    /// <summary>Closes admission and IO, faults pending calls and keeps draining ownership even if the caller stops waiting.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;
        lock (_responses) { _disposed = true; _admission.Close(); stop = _stopTask ??= Task.Run(StopCoreAsync); }
        // A decode callback may request disposal, but cannot wait for its own response rental to be returned.
        if (_decoding.Value) return Task.CompletedTask;
        return cancellationToken.CanBeCanceled ? stop.WaitAsync(cancellationToken) : stop;
    }

    private async Task StopCoreAsync()
    {
        try
        {
            lock (_responses) { _connected = false; FailPending(new ObjectDisposedException(GetType().Name)); }
            _cts.Cancel();
            base.Dispose();
            await _listener.ConfigureAwait(false);
            await Task.WhenAll(_writes.Keys).ConfigureAwait(false);
            await _admission.Drained.ConfigureAwait(false);
            _listenTaskScheduler.Dispose();
            _taskScheduler.Dispose();
            _cts.Dispose();
            _metrics?.Dispose();
            _completion.TrySetResult();
        }
        catch (Exception error) { _completion.TrySetException(error); throw; }
    }

    public override void Dispose() => StopAsync().GetAwaiter().GetResult();
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>Waits for remote acknowledgement; cancellation after sending is local and does not roll back remote work.</summary>
    public async Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
        => _ = await InvokeAsync(methodCode, action, static payload =>
        {
            SerializationUtils.EnsureFullyConsumed(0, payload.Length);
            return 0;
        }, cancellationToken).ConfigureAwait(false);

    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func)
        => InvokeAsync(methodCode, action, func).GetAwaiter().GetResult();
    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action)
        => InvokeAsync(methodCode, action).GetAwaiter().GetResult();
}
