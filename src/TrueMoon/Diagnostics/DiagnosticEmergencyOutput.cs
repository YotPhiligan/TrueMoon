namespace TrueMoon.Diagnostics;

/// <summary>Independent finite channel. Never calls DiagnosticListener or an application sink.</summary>
internal sealed class DiagnosticEmergencyOutput
{
    private readonly Lock _gate = new();
    private readonly Queue<(string Stage, string Sink, string Error, long? Sequence)> _queue = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly DiagnosticEmergencyOptions _options;
    private readonly TextWriter _writer;
    private readonly Guid _applicationId;
    private readonly Task _worker;
    private TaskCompletionSource _progress = NewSignal();
    private bool _closed, _released;
    private long _attempts, _accepted, _written, _completed, _dropped, _suppressed, _failures;
    private int _pending;
    private string? _lastError;

    internal DiagnosticEmergencyOutput(DiagnosticEmergencyOptions options, Guid applicationId)
    {
        _options = options.Snapshot(); _writer = _options.Writer ?? Console.Error; _applicationId = applicationId;
        _worker = Task.Run(RunAsync);
    }

    internal void Report(string stage, string sink, Exception? error = null, long? sequence = null)
    {
        lock (_gate)
        {
            if (_closed || !_options.Enabled) return;
            if (_attempts >= _options.MaxReports) { _suppressed++; return; }
            _attempts++;
            if (_pending >= _options.QueueCapacity) { _dropped++; return; }
            _queue.Enqueue((Clean(stage, 128), Clean(sink, 128), Clean(error?.GetType().FullName ?? "-", 256), sequence));
            _pending++; _accepted++; _available.Release();
        }
    }

    internal DiagnosticEmergencyStatus Read()
    {
        lock (_gate) return new(_accepted, _written, _dropped, _suppressed, _failures, _pending, _lastError);
    }

    internal Task FlushAsync(CancellationToken token = default)
    {
        long target; lock (_gate) target = _accepted;
        return WaitAsync(target, token);
    }

    private async Task WaitAsync(long target, CancellationToken token)
    {
        while (true)
        {
            Task progress;
            lock (_gate) { if (_completed >= target) return; progress = _progress.Task; }
            await progress.WaitAsync(token).ConfigureAwait(false);
        }
    }

    internal void Close() { lock (_gate) { if (_closed) return; _closed = true; _available.Release(); } }
    internal async Task DisposeAsync()
    {
        Close(); await _worker.ConfigureAwait(false);
        lock (_gate) { if (_released) return; _available.Dispose(); _released = true; }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            await _available.WaitAsync().ConfigureAwait(false);
            (string Stage, string Sink, string Error, long? Sequence) item;
            lock (_gate)
            {
                if (_queue.Count == 0) { if (_closed) return; continue; }
                item = _queue.Dequeue();
            }
            try
            {
                var text = $"TrueMoon diagnostics emergency App={_applicationId:N} Stage={item.Stage} Sink={item.Sink} Error={item.Error} Sequence={item.Sequence?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}";
                // A caller's TextWriter.NewLine can be arbitrarily long. Use exactly one bounded literal LF.
                await _writer.WriteAsync(Clip(text, _options.MaxMessageChars - 1) + "\n").ConfigureAwait(false);
                lock (_gate) _written++;
            }
            catch (Exception error)
            {
                // In particular, do not read a virtual exception.Message or report this failure to ourselves.
                lock (_gate) { _failures++; _lastError = Clip(error.GetType().FullName ?? "Exception", 256); }
            }
            finally
            {
                lock (_gate)
                {
                    _pending--; _completed++;
                    var progress = _progress; _progress = NewSignal(); progress.TrySetResult();
                }
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string Clean(string text, int length)
    {
        var bounded = Clip(text, length);
        if (!bounded.Any(char.IsControl)) return bounded;
        var characters = bounded.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
            if (char.IsControl(characters[index])) characters[index] = ' ';
        return new(characters);
    }
    private static string Clip(string text, int length)
    {
        if (text.Length <= length) return text;
        if (length > 0 && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length])) length--;
        return text[..length];
    }
}
