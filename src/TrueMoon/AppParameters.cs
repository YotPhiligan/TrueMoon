namespace TrueMoon;

public class AppParameters : Dictionary<string, object?>, IAppParameters
{
    public AppParameters()
    {
        var args = Environment.GetCommandLineArgs();

        foreach (var s in args)
        {
            if (s.Contains('='))
            {
                var parts = s.Split('=');
                var key = parts[0];
                var value = parts[1];
                
                Set(key,value);
            }
            else
            {
                Set(s, string.Empty);
            }
        }
    }
    
    public void Dispose()
    {
        foreach (var value in Values)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var value in Values)
        {
            switch (value)
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

    /// <inheritdoc />
    public IAppParameters Set<T>(string key, T? value)
    {
        if (ContainsKey(key))
        {
            var oldValue = this[key];
            if (oldValue is IDisposable disposable)
            {
                disposable.Dispose();
            }
            this[key] = value;
        }
        else
        {
            Add(key,value);
        }
        return this;
    }

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
        if (ContainsKey(key))
        {
            return (T?)this[key];
        }

        return default;
    }
}