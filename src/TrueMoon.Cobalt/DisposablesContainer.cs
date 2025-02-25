namespace TrueMoon.Cobalt;

public class DisposablesContainer : IDisposable, IAsyncDisposable
{
    private readonly List<object> _disposables = [];
    private readonly Lock _lock = new ();
    
    public void Add<T>(T value)
    {
        lock (_lock)
        {
            _disposables.Add(value!);
        }
    }

    public void Dispose()
    {
        if (_disposables.Count == 0)
        {
            return;
        }

        foreach (var item in _disposables)
        {
            switch (item)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case IAsyncDisposable asyncDisposable:
                    asyncDisposable
                        .DisposeAsync()
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposables.Count == 0)
        {
            return;
        }
        
        foreach (var item in _disposables)
        {
            switch (item)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}