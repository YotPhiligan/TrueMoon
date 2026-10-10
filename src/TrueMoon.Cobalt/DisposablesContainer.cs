namespace TrueMoon.Cobalt;

public class DisposablesContainer : IDisposable, IAsyncDisposable
{
    private readonly Lock _lock = new();
    private readonly List<object> _disposables = [];
    private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);
    private Task? _disposeTask;
    private readonly AsyncLocal<bool> _disposing = new();

    public void Add<T>(T value)
    {
        if (value is not IDisposable and not IAsyncDisposable) return;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposeTask != null, this);
            if (_seen.Add(value)) _disposables.Add(value);
        }
    }

    public void Dispose() => DisposeCore(false).GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => new(DisposeCore(true));

    private Task DisposeCore(bool asynchronous)
    {
        if (_disposing.Value) return Task.CompletedTask;
        TaskCompletionSource completion;
        object[] items;
        lock (_lock)
        {
            if (_disposeTask != null) return _disposeTask;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            items = _disposables.ToArray();
            _disposables.Clear();
            _seen.Clear();
        }
        _ = DrainAsync(items, asynchronous, completion);
        return completion.Task;
    }

    private async Task DrainAsync(object[] items, bool asynchronous, TaskCompletionSource completion)
    {
        _disposing.Value = true;
        try
        {
            List<Exception> errors = [];
            for (var index = items.Length - 1; index >= 0; index--)
            {
                try
                {
                    if (items[index] is IAsyncDisposable asyncDisposable && (asynchronous || items[index] is not IDisposable))
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (items[index] is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception exception) { errors.Add(exception); }
            }
            if (errors.Count == 0) completion.TrySetResult();
            else completion.TrySetException(new AggregateException("Service cleanup failed.", errors));
            }
        finally { _disposing.Value = false; }
    }
}
