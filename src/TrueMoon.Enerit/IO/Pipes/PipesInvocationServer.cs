using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesSignalServerConnection : PipeConectionHandler, IStoppable, IAsyncDisposable
{
    private readonly IEventsSource _eventsSource;
    private readonly IInvocationServerHandler _handler;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _execTaskScheduler;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly PipesServerAdmission _admission;
    private readonly PipesTransportOptions _options;
    private readonly IDiagnosticTelemetry? _telemetry;
    private readonly PipesMetrics? _metrics;
    private readonly Guid _connectionId = Guid.NewGuid();
    private readonly string _serviceName;
    private readonly ConcurrentDictionary<Task, byte> _invocations = new();
    private readonly object _lifecycleGate = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _externalCancellation;
    private Task _start = Task.CompletedTask, _listener = Task.CompletedTask;
    private Task? _stop;
    private bool _started, _disposed;
    public event EventHandler? Disconnected;

    public PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler)
        : this(name, eventsSource, handler, new PipesTransportOptions()) { }

    /// <summary>Creates a caller-owned connection with validated, copied resource limits.</summary>
    public PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler, PipesTransportOptions options)
        : this(name, eventsSource, handler, options, null) { }

    /// <summary>Creates a connection borrowing diagnostics; the caller retains ownership until actual stop.</summary>
    public PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler,
        PipesTransportOptions options, IDiagnosticTelemetry? telemetry)
        : this(name, eventsSource, handler, options, null, telemetry) { }

    internal PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler, PipesServerAdmission admission)
        : this(name, eventsSource, handler, new PipesTransportOptions(), admission, null) { }

    internal PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler,
        PipesServerAdmission admission, PipesTransportOptions options, IDiagnosticTelemetry? telemetry, string? serviceName = null)
        : this(name, eventsSource, handler, options, admission, telemetry, serviceName) { }

    private PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler,
        PipesTransportOptions options, PipesServerAdmission? admission, IDiagnosticTelemetry? telemetry, string? serviceName = null) : base(name, false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentNullException.ThrowIfNull(handler);
        _eventsSource = eventsSource;
        _handler = handler;
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Snapshot();
        _telemetry = telemetry;
        _serviceName = serviceName ?? handler.GetType().FullName!;
        _token = _cts.Token;
        // Shared server admission owns its one numeric cell; a direct connection owns a separate cell.
        _metrics = admission == null && _options.MetricsEnabled ? new PipesMetrics() : null;
        try
        {
            _admission = admission ?? new(_options, _metrics);
            _metrics?.Connections(1);
            _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
            _execTaskScheduler = new TmTaskScheduler($"{Name}_exec", 8);
        }
        catch { _metrics?.Dispose(); throw; }
    }

    /// <summary>Admitted requests, including reads, queued handlers and response writes.</summary>
    public int ActiveRequestCount => _admission.Requests;
    /// <summary>Admitted request payload bytes until final cleanup.</summary>
    public long BufferedRequestBytes => _admission.Bytes;
    /// <summary>Completes only after requested stop and actual handler/IO drain.</summary>
    public Task Completion => _completion.Task;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started) throw new InvalidOperationException("Connection has already been started.");
            _started = true;
            _externalCancellation = cancellationToken.Register(static state => ((PipesSignalServerConnection)state!).CancelIO(), this);
            return _start = StartCoreAsync();
        }
    }

    private async Task StartCoreAsync()
    {
        await ConnectAsync(_token).ConfigureAwait(false);
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            PipesDiagnostics.Legacy(() => _eventsSource.Write(() => "Connected"));
            _listener = Task.Factory.StartNew(() => Listen(_token), CancellationToken.None, TaskCreationOptions.LongRunning, _listenTaskScheduler);
        }
    }

    private void Listen(CancellationToken token)
    {
        Activity.Current = null;
        PipesDiagnostics.RemoteContext = null;
        Span<byte> scratch = stackalloc byte[4096];
        try
        {
            var stream = PipeStream;
            while (!token.IsCancellationRequested)
            {
                var header = PipesRequestHeader.Read(stream);
                var (guid, method, length) = (header.Id, header.Method, header.Length);
                if (guid == Guid.Empty) { NotifyDisconnected(); return; }
                bool admitted;
                lock (_lifecycleGate)
                {
                    if (_disposed) return;
                    admitted = _admission.TryEnter(length);
                }
                if (!admitted)
                {
                    // Never rent the rejected payload. One fixed scratch buffer per connection drains framing.
                    var remaining = length;
                    while (remaining > 0)
                    {
                        var chunk = Math.Min(remaining, scratch.Length);
                        stream.ReadExactly(scratch[..chunk]); remaining -= chunk;
                    }
                    InvokeAsync(header, default, token, false).GetAwaiter().GetResult();
                    continue;
                }
                Memory<byte> memory = default;
                var transferred = false;
                try
                {
                    if (length > 0) { memory = MemoryPoolUtils.Create(length); stream.ReadFullBuffer(memory); }
                    var requestMemory = memory;
                    var task = Task.Factory.StartNew(() => InvokeAsync(header, requestMemory, token),
                        CancellationToken.None, TaskCreationOptions.PreferFairness, _execTaskScheduler).Unwrap();
                    _invocations.TryAdd(task, 0);
                    _ = task.ContinueWith(t => _invocations.TryRemove(t, out _), CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    memory = default;
                    transferred = true;
                }
                finally
                {
                    memory.TryReturn();
                    if (!transferred) _admission.Release(length);
                }
            }
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested) { PipesDiagnostics.Legacy(() => _eventsSource.Exception(error)); NotifyDisconnected(); }
        }
    }

    private async Task InvokeAsync(PipesRequestHeader header, Memory<byte> memory, CancellationToken token, bool admitted = true)
    {
        // Each request starts in a clean flow. Listener/construction context is never a remote parent.
        var inherited = Activity.Current;
        var inheritedContext = PipesDiagnostics.RemoteContext;
        Activity.Current = null;
        var parent = _options.PropagateTraceContext ? header.Context : null;
        PipesDiagnostics.RemoteContext = parent;
        IReadOnlyDictionary<string, string?> Properties()
        {
            var properties = PipesDiagnostics.Properties(_serviceName, header.Id, _connectionId, header.Method, true);
            properties["TraceContext"] = !_options.PropagateTraceContext ? "Disabled" :
                header.InvalidContext ? "Invalid" : parent.HasValue ? "Remote" : "Missing";
            properties["ParentTraceId"] = parent?.TraceId.ToHexString();
            properties["ParentSpanId"] = parent?.SpanId.ToHexString();
            return properties;
        }
        using var operation = PipesDiagnostics.Start(_telemetry, "Enerit.Pipes.Server", ActivityKind.Server, parent, Properties);
        PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, 2200, "Pipes.Server.Starting", Properties);
        Exception? failure = null;
        try
        {
            using var writer = new ArrayPoolBufferWriter<byte>(128);
            var success = false;
            try
            {
                token.ThrowIfCancellationRequested();
                if (!admitted) failure = new PipesInvocationOverloadedException();
                else
                {
                    (success, failure) = await _handler.HandleAsync(header.Method, memory, writer, token).ConfigureAwait(false);
                    if (success) { PipesProtocol.ValidatePayloadLength(writer.WrittenCount); failure = null; }
                    else failure ??= new InvocationException("Remote invocation failed.");
                }
            }
            catch (Exception error) { (success, failure) = (false, error); }
            await WriteResponseAsync(header.Id, success ? writer.WrittenMemory : default,
                success ? null : failure, token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            failure = error;
            if (!token.IsCancellationRequested) { PipesDiagnostics.Legacy(() => _eventsSource.Exception(error)); NotifyDisconnected(); }
        }
        finally
        {
            memory.TryReturn();
            if (admitted) _admission.Release(header.Length);
            var cancelled = failure is OperationCanceledException;
            operation?.Complete(failure == null ? DiagnosticOperationOutcome.Succeeded : cancelled ?
                DiagnosticOperationOutcome.Cancelled : DiagnosticOperationOutcome.Failed, failure);
            PipesDiagnostics.Emit(_eventsSource, _options.CaptureRequestEvents, failure == null ? 2201 : cancelled ? 2202 : 2203,
                failure == null ? "Pipes.Server.Completed" : cancelled ? "Pipes.Server.Cancelled" : "Pipes.Server.Failed", Properties, failure);
            operation?.Dispose();
            Activity.Current = inherited;
            PipesDiagnostics.RemoteContext = inheritedContext;
        }
    }

    private async Task WriteResponseAsync(Guid guid, ReadOnlyMemory<byte> payload, Exception? error, CancellationToken token)
    {
        using var writer = new ArrayPoolBufferWriter<byte>(128);
        SerializationUtils.Write(guid, writer);
        SerializationUtils.Write(error is null ? (byte)0 : (byte)1, writer);
        if (error is not null)
        {
            var bytes = Encoding.UTF8.GetBytes($"{error}");
            if (bytes.Length > PipesProtocol.MaxPayloadLength) bytes = Encoding.UTF8.GetBytes("Remote error exceeded the pipes payload limit.");
            payload = bytes;
        }
        SerializationUtils.Write(payload.Length, writer);
        writer.Write(payload.Span);
        await WriteFrameAsync(writer.WrittenMemory, token).ConfigureAwait(false);
    }

    private void NotifyDisconnected() =>
        ThreadPool.QueueUserWorkItem(_ => Disconnected?.Invoke(this, EventArgs.Empty));

    private void CancelIO()
    {
        try { _cts.Cancel(); }
        finally { base.Dispose(); }
    }

    /// <summary>Stops admission and IO. Cancelling the wait preserves ownership until Completion actually drains.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;
        lock (_lifecycleGate) { _disposed = true; stop = _stop ??= Task.Run(StopCoreAsync); }
        return cancellationToken.CanBeCanceled ? stop.WaitAsync(cancellationToken) : stop;
    }

    private async Task StopCoreAsync()
    {
        try
        {
            List<Exception> errors = [];
            try { CancelIO(); } catch (Exception error) { errors.Add(error); }
            try { _externalCancellation.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { await _start.ConfigureAwait(false); }
            catch (Exception) when (_token.IsCancellationRequested) { }
            try { await _listener.ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            try { await Task.WhenAll(_invocations.Keys).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            _listenTaskScheduler.Dispose();
            _execTaskScheduler.Dispose();
            _cts.Dispose();
            if (errors.Count > 0) throw new AggregateException(errors);
            _metrics?.Connections(0);
            _metrics?.Dispose();
            _completion.TrySetResult();
        }
        catch (Exception error) { _completion.TrySetException(error); throw; }
    }

    public override void Dispose() => StopAsync().GetAwaiter().GetResult();
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

public class PipesInvocationServer<T> : IInvocationServer<T>, IStoppable, IDisposable, IAsyncDisposable
{
    private readonly IEventsSource<PipesInvocationServer<T>> _eventsSource;
    private readonly IInvocationServerHandler<T> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly List<PipesSignalServerConnection> _connections = [];
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _connectionSlots;
    private readonly PipesServerAdmission _admission;
    private readonly PipesTransportOptions _options;
    private readonly IDiagnosticTelemetry? _telemetry;
    private readonly PipesMetrics? _metrics;
    private readonly Task _listener;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _stop;
    private bool _disposed;

    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler)
        : this(eventsSource, handler, typeof(T).FullName!) { }

    /// <summary>Creates a server with finite default resource limits.</summary>
    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler, string pipeName)
        : this(eventsSource, handler, pipeName, new PipesTransportOptions()) { }

    /// <summary>Creates a caller-owned server; handler and diagnostics remain borrowed until actual stop completion.</summary>
    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler,
        string pipeName, PipesTransportOptions options)
        : this(eventsSource, handler, pipeName, options, null) { }

    /// <summary>Creates a server borrowing operation telemetry; accepted handlers remain owned through stop completion.</summary>
    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler,
        string pipeName, PipesTransportOptions options, IDiagnosticTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Snapshot();
        _options = limits; _telemetry = telemetry;
        Id = pipeName;
        _eventsSource = eventsSource;
        _handler = handler;
        _metrics = limits.MetricsEnabled ? new PipesMetrics() : null;
        try
        {
            _admission = new(limits, _metrics);
            _connectionSlots = new(limits.MaxConnections, limits.MaxConnections);
            _token = _cts.Token;
            _taskScheduler = new TmTaskScheduler($"{Id}_exec", 2);
            _listener = Task.Factory.StartNew(ListenAsync, CancellationToken.None, TaskCreationOptions.LongRunning, _taskScheduler).Unwrap();
        }
        catch { _metrics?.Dispose(); throw; }
    }

    public string Id { get; }
    /// <summary>Shared admitted request count across connections.</summary>
    public int ActiveRequestCount => _admission.Requests;
    /// <summary>Shared request payload bytes until response/handler cleanup.</summary>
    public long BufferedRequestBytes => _admission.Bytes;
    /// <summary>Owned connections, including the instance awaiting connection and connections still draining.</summary>
    public int ConnectionCount { get { lock (_sync) return _connections.Count; } }
    /// <summary>Completes only after requested stop and all owned connections/handlers actually drain.</summary>
    public Task Completion => _completion.Task;

    private async Task ListenAsync()
    {
        try
        {
            while (!_token.IsCancellationRequested)
            {
                await _connectionSlots.WaitAsync(_token).ConfigureAwait(false);
                PipesSignalServerConnection? item = null;
                try
                {
                    lock (_sync)
                    {
                        if (_disposed) { _connectionSlots.Release(); return; }
                        item = new PipesSignalServerConnection(Id, _eventsSource, _handler, _admission, _options, _telemetry, typeof(T).FullName);
                        item.Disconnected += ItemOnDisconnected;
                        _connections.Add(item);
                        _metrics?.Connections(_connections.Count);
                    }
                    await item.StartAsync(_token).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    if (item is null) _connectionSlots.Release();
                    else await RetireAsync(item).ConfigureAwait(false);
                    if (!_token.IsCancellationRequested) PipesDiagnostics.Legacy(() => _eventsSource.Exception(error));
                }
            }
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
    }

    private void ItemOnDisconnected(object? sender, EventArgs args)
    {
        if (sender is PipesSignalServerConnection item) _ = RetireAsync(item);
    }

    private async Task RetireAsync(PipesSignalServerConnection item)
    {
        item.Disconnected -= ItemOnDisconnected;
        try { await item.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { PipesDiagnostics.Legacy(() => _eventsSource.Exception(error)); return; }
        lock (_sync)
        {
            if (_connections.Remove(item)) { _metrics?.Connections(_connections.Count); _connectionSlots.Release(); }
        }
    }

    /// <summary>Closes admission and begins every connection's stop before waiting for handler drain.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stop;
        lock (_sync) { _disposed = true; _admission.Close(); stop = _stop ??= Task.Run(StopCoreAsync); }
        return cancellationToken.CanBeCanceled ? stop.WaitAsync(cancellationToken) : stop;
    }

    private async Task StopCoreAsync()
    {
        try
        {
            List<Exception> errors = [];
            try { _cts.Cancel(); } catch (Exception error) { errors.Add(error); }
            PipesSignalServerConnection[] connections;
            lock (_sync) connections = _connections.ToArray();
            var stops = connections.Select(c => c.StopAsync()).ToArray();
            try { await _listener.ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            try { await Task.WhenAll(stops).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            lock (_sync) { _connections.Clear(); _metrics?.Connections(0); }
            _taskScheduler.Dispose();
            _cts.Dispose();
            if (errors.Count > 0) throw new AggregateException(errors);
            _metrics?.Dispose();
            _completion.TrySetResult();
        }
        catch (Exception error) { _completion.TrySetException(error); throw; }
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
