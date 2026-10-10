using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesInvocationClient<T> : PipeConectionHandler, IInvocationClient<T>
{
    private readonly IEventsSource<PipesInvocationClient<T>> _eventsSource;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _token;
    private int _disposed;

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ReadOnlyMemory<byte>>> _responses = new ();

    public PipesInvocationClient(IEventsSource<PipesInvocationClient<T>> eventsSource) : base(typeof(T).FullName!)
    {
        _eventsSource = eventsSource;
        _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
        _taskScheduler = new TmTaskScheduler($"{Name}_write", 2);
        _cts = new CancellationTokenSource();
        _token = _cts.Token;
        StartListening();
    }

    public override void Dispose()
    {
        lock (_responses)
        {
            if (_disposed != 0) { return; }
            _disposed = 1;
        }
        _cts.Cancel();
        base.Dispose();
        IsConnected = false;
        foreach (var pair in _responses)
        {
            if (_responses.TryRemove(pair.Key, out var response))
            {
                response.TrySetException(new ObjectDisposedException(GetType().Name));
            }
        }
        _listenTaskScheduler.Dispose();
        _taskScheduler.Dispose();
        _cts.Dispose();
    }
    public bool IsConnected { get; private set; }

    private void StartListening()
    {
        _ = Task.Factory.StartNew(() =>
        {
            try
            {
                Connect(_token);
                _eventsSource.Write(()=>"Connected");
                IsConnected = true;
                while (!_cts.IsCancellationRequested)
                {
                    var (resultGuid, statusCode, len) = PipeStream.GetResponseHeader();

                    if (_responses.TryRemove(resultGuid, out var container))
                    {
                        Memory<byte> mem = default;
                        try
                        {
                            if (len < 0) { throw new InvalidDataException("Response payload length must not be negative."); }
                            switch (statusCode)
                            {
                                case 0:
                                    if (len > 0)
                                    {
                                        mem = MemoryPoolUtils.Create(len);
                                        PipeStream.ReadFullBuffer(mem);
                                    }
                                    if (!container.TrySetResult(mem)) { mem.TryReturn(); }
                                    mem = default;
                                    break;
                                case 1:
                                    string? error = null;
                                    if (len > 0)
                                    {
                                        mem = MemoryPoolUtils.Create(len);
                                        PipeStream.ReadFullBuffer(mem);
                                        error = Encoding.UTF8.GetString(mem.Span);
                                    }
                                    container.TrySetException(new InvocationException(error));
                                    break;
                                default:
                                    throw new InvalidDataException($"Unknown invocation response status {statusCode}.");
                            }
                        }
                        catch (Exception error)
                        {
                            container.TrySetException(error);
                            throw;
                        }
                        finally { mem.TryReturn(); }
                    }
                    else
                    {
                        _eventsSource.Write(()=>$"{resultGuid} not found");
                        Reconnect();
                        return;
                    }
                }
            }
            catch (IOException)
            {
                Reconnect();
            }
            catch (Exception e)
            {
                if (!_token.IsCancellationRequested)
                {
                    _eventsSource.Exception(e);
                    Reconnect();
                }
            }
        }, _token,
        TaskCreationOptions.LongRunning,
        _listenTaskScheduler);
    }

    private void Reconnect()
    {
        if (_cts.IsCancellationRequested || IsDisposed)
        {
            return;
        }
        _eventsSource.Trace();
        IsConnected = false;
        foreach (var pair in _responses)
        {
            if (_responses.TryRemove(pair.Key, out var response))
            {
                response.TrySetException(new IOException("The invocation connection was lost."));
            }
        }
        Reset();
        StartListening();
    }

    private async Task CheckConnectedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        while (!IsConnected)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void CheckConnected(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var spin = new SpinWait();
        while (!IsConnected)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            spin.SpinOnce();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
    public async Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken cancellationToken = default)
    {
        using var ctsTimer = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimer.Token, cancellationToken);
        var token = cts.Token;

        await CheckConnectedAsync(token);

        var tcs = InvokeCore(methodCode, action, token);

        var result = await tcs.Task.WaitAsync(token);
        try
        {
            return func.Invoke(result);
        }
        finally
        {
            result.TryReturn();
        }
    }

    private TaskCompletionSource<ReadOnlyMemory<byte>> InvokeCore(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
    {
        var guid = Guid.NewGuid();

        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_responses)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _responses.TryAdd(guid, tcs);
        }
        var registration = cancellationToken.Register(() =>
        {
            if (_responses.TryRemove(guid, out var pending)) { pending.TrySetCanceled(cancellationToken); }
        });
        _ = tcs.Task.ContinueWith(_ => registration.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try { ScheduleInvocationCore(guid, methodCode, action, cancellationToken); }
        catch (Exception error)
        {
            if (_responses.TryRemove(guid, out var pending)) { pending.TrySetException(error); }
        }
        return tcs;
    }

    private void ScheduleInvocationCore(Guid guid, byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken)
    {
        _ = Task.Factory.StartNew(() =>
        {
            try
            {
                using var writer = new ArrayPoolBufferWriter<byte>();
                SerializationUtils.Write(guid, writer);
                SerializationUtils.Write(methodCode, writer);
                var lenPosition = writer.WrittenCount;
                SerializationUtils.Write(0, writer);

                var len = writer.WrittenCount;

                action?.Invoke(writer);
                len = writer.WrittenCount - len;

                MemoryMarshal.Write(writer.WrittenSpanWritable.Slice(lenPosition, sizeof(int)), ref len);

                PipeStream.Write(writer.WrittenSpan);
            }
            catch (IOException error)
            {
                _eventsSource.Write(() => "IO error");
                if (_responses.TryRemove(guid, out var pending)) { pending.TrySetException(error); }
                Reconnect();
            }
            catch (Exception e)
            {
                if (_responses.TryRemove(guid, out var pending)) { pending.TrySetException(e); }
                _eventsSource.Exception(e);
            }
        }, cancellationToken, TaskCreationOptions.PreferFairness, _taskScheduler);
    }

    public async Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
    {
        using var ctsTimer = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimer.Token, cancellationToken);
        var token = cts.Token;

        await CheckConnectedAsync(token);

        var guid = Guid.NewGuid();

        TaskCompletionSource? tcs = action != null
            ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            : default;

        _ = Task.Factory.StartNew(() =>
        {
            try
            {
                using var writer = new ArrayPoolBufferWriter<byte>();
                SerializationUtils.Write(guid, writer);
                SerializationUtils.Write(methodCode, writer);
                var lenPosition = writer.WrittenCount;
                SerializationUtils.Write(0, writer);

                var len = writer.WrittenCount;

                action?.Invoke(writer);
                tcs?.TrySetResult();
                len = writer.WrittenCount - len;

                MemoryMarshal.Write(writer.WrittenSpanWritable.Slice(lenPosition, sizeof(int)), ref len);

                PipeStream.Write(writer.WrittenSpan);
            }
            catch (IOException)
            {
                var ioError = "IO error";
                _eventsSource.Write(() => ioError);
                Reconnect();
                tcs?.TrySetException(new InvocationException(ioError));
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
                tcs?.TrySetException(e);
            }
        }, cancellationToken, TaskCreationOptions.PreferFairness, _taskScheduler);

        if (action == null)
        {
            return;
        }

        if (tcs != null)
        {
            await tcs.Task.WaitAsync(token);
        }
    }

    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var token = cts.Token;

        CheckConnected(token);

        var tcs = InvokeCore(methodCode, action, token);

        tcs.Task.Wait(token);
        var result = tcs.Task.Result;
        try
        {
            return func.Invoke(result);
        }
        finally
        {
            result.TryReturn();
        }
    }

    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var token = cts.Token;

        CheckConnected(token);

        var guid = Guid.NewGuid();

        try
        {
            using var writer = new ArrayPoolBufferWriter<byte>();
            SerializationUtils.Write(guid, writer);
            SerializationUtils.Write(methodCode, writer);
            var lenPosition = writer.WrittenCount;
            SerializationUtils.Write(0, writer);

            var len = writer.WrittenCount;

            action?.Invoke(writer);
            len = writer.WrittenCount - len;

            MemoryMarshal.Write(writer.WrittenSpanWritable.Slice(lenPosition, sizeof(int)), ref len);

            PipeStream.Write(writer.WrittenSpan);
        }
        catch (IOException)
        {
            _eventsSource.Write(() => "IO error");
            Reconnect();
        }
        catch (Exception e)
        {
            _eventsSource.Exception(e);
        }
    }
}
