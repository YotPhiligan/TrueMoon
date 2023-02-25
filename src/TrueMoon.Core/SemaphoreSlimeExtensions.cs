namespace TrueMoon;

public static class SemaphoreSlimeExtensions
{
    public static async ValueTask WaitAsync(this SemaphoreSlim semaphoreSlim, Action action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            action();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
    
    public static async ValueTask WaitAsync(this SemaphoreSlim semaphoreSlim, Func<ValueTask> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            await action();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
    
    public static async ValueTask<T> WaitAsync<T>(this SemaphoreSlim semaphoreSlim, Func<T> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            return action();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
    
    public static async ValueTask<T> WaitAsync<T>(this SemaphoreSlim semaphoreSlim, Func<ValueTask<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}