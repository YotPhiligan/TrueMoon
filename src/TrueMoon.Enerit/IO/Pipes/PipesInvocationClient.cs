using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClient<T> : PipeConectionHandler, IInvocationClient<T>
{
    private readonly IEventsSource<PipesInvocationClient<T>> _eventsSource;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ReadOnlyMemory<byte>>> _responses = new();
    private int _disposed;
    private volatile bool _connected;

    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource)
        : this(eventsSource, typeof(T).FullName!) { }

    /// <summary>Creates a client for an explicitly scoped endpoint.</summary>
    /// <param name="eventsSource">Diagnostics source.</param>
    /// <param name="pipeName">Endpoint name without the transport's tm_ prefix; both peers must agree.</param>
    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource, string pipeName) : base(pipeName)
    {
        ArgumentNullException.ThrowIfNull(eventsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _eventsSource = eventsSource;
        _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
        _taskScheduler = new TmTaskScheduler($"{Name}_write", 2);
        _token = _cts.Token;
        StartListening();
    }

    public bool IsConnected => _connected;

    public override void Dispose()
    {
        lock (_responses)
        {
            if (_disposed != 0) return;
            _disposed = 1;
        }
        _cts.Cancel();
        base.Dispose();
        _connected = false;
        FailPending(new ObjectDisposedException(GetType().Name));
        _listenTaskScheduler.Dispose();
        _taskScheduler.Dispose();
        _cts.Dispose();
    }

    private void FailPending(Exception error)
    {
        foreach (var pair in _responses)
            if (_responses.TryRemove(pair.Key, out var response)) response.TrySetException(error);
    }

    private void StartListening() => _ = Task.Factory.StartNew(() =>
    {
        try
        {
            Connect(_token);
            lock (_responses)
            {
                if (_disposed != 0) return;
                _connected = true;
            }
            _eventsSource.Write(() => "Connected");
            while (!_token.IsCancellationRequested)
            {
                var (guid, status, length) = PipeStream.GetResponseHeader();
                if (status is not (0 or 1)) throw new InvalidDataException($"Unknown invocation response status {status}.");
                Memory<byte> memory = default;
                try
                {
                    if (length > 0)
                    {
                        memory = MemoryPoolUtils.Create(length);
                        PipeStream.ReadFullBuffer(memory);
                    }
                    if (!_responses.TryRemove(guid, out var response))
                    {
                        // Cancellation removed this request locally. Drain its entire frame and keep peers alive.
                        _eventsSource.Write(() => $"Discarded late response {guid}", "LateResponse");
                        continue;
                    }
                    if (status == 1) response.TrySetException(new InvocationException(Encoding.UTF8.GetString(memory.Span)));
                    else if (response.TrySetResult(memory)) memory = default; // Ownership passes to the awaiting caller.
                }
                finally { memory.TryReturn(); }
            }
        }
        catch (Exception error)
        {
            if (!_token.IsCancellationRequested) Reconnect(error);
        }
    }, _token, TaskCreationOptions.LongRunning, _listenTaskScheduler);

    private void Reconnect(Exception error)
    {
        if (_token.IsCancellationRequested || IsDisposed) return;
        _connected = false;
        FailPending(error);
        Reset();
        StartListening();
    }

    private async Task CheckConnectedAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        while (!IsConnected)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            await Task.Delay(1, token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
    }

    public async Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action,
        Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken cancellationToken = default)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        await CheckConnectedAsync(cancellation.Token).ConfigureAwait(false);
        var response = InvokeCore(methodCode, action, cancellation.Token);
        // The registration completes the TCS. A response that already won the race must be consumed/returned.
        var memory = await response.Task.ConfigureAwait(false);
        try { return func(memory); }
        finally { memory.TryReturn(); }
    }

    private TaskCompletionSource<ReadOnlyMemory<byte>> InvokeCore(byte method, Action<IBufferWriter<byte>>? action, CancellationToken token)
    {
        var guid = Guid.NewGuid();
        var response = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_responses)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _responses.TryAdd(guid, response);
        }
        var registration = token.Register(() =>
        {
            if (_responses.TryRemove(guid, out var pending)) pending.TrySetCanceled(token);
        });
        _ = response.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            // Always execute cleanup even when the invocation was cancelled while queued.
            _ = Task.Factory.StartNew(() =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    using var writer = new ArrayPoolBufferWriter<byte>();
                    SerializationUtils.Write(guid, writer);
                    SerializationUtils.Write(method, writer);
                    var lengthPosition = writer.WrittenCount;
                    SerializationUtils.Write(0, writer);
                    action?.Invoke(writer);
                    var length = writer.WrittenCount - lengthPosition - sizeof(int);
                    PipesProtocol.ValidatePayloadLength(length);
                    MemoryMarshal.Write(writer.WrittenSpanWritable.Slice(lengthPosition, sizeof(int)), in length);
                    token.ThrowIfCancellationRequested();
                    if (_responses.ContainsKey(guid)) WriteFrame(writer.WrittenSpan);
                }
                catch (Exception error)
                {
                    if (_responses.TryRemove(guid, out var pending)) pending.TrySetException(error);
                }
            }, CancellationToken.None, TaskCreationOptions.PreferFairness, _taskScheduler);
        }
        catch (Exception error)
        {
            if (_responses.TryRemove(guid, out var pending)) pending.TrySetException(error);
        }
        return response;
    }

    /// <summary>Waits for the remote acknowledgement, including an empty successful response.</summary>
    public async Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
        => _ = await InvokeAsync(methodCode, action, static _ => 0, cancellationToken).ConfigureAwait(false);

    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func)
        => InvokeAsync(methodCode, action, func).GetAwaiter().GetResult();

    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action)
        => InvokeAsync(methodCode, action).GetAwaiter().GetResult();
}
