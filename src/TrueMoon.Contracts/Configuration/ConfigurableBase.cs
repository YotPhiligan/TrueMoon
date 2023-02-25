namespace TrueMoon.Configuration;

public abstract class ConfigurableBase : IConfigurable
{
    private readonly Dictionary<string, object?> _dictionary;
    protected ConfigurableBase(Dictionary<string, object?>? dictionary = default)
    {
        _dictionary = dictionary ?? new Dictionary<string, object?>();
    }

    /// <inheritdoc />
    public virtual bool Exist(string key)
    {
        return _dictionary.ContainsKey(key);
    }

    /// <inheritdoc />
    public virtual void Set<T>(string key, T? value)
    {
        if (_dictionary.ContainsKey(key))
        {
            _dictionary[key] = value;
        }
        else
        {
            _dictionary.Add(key, value);
        }
    }

    /// <inheritdoc />
    public virtual T? Get<T>(string key)
    {
        if (_dictionary.TryGetValue(key, out var v) && v is T?)
        {
            return (T?)_dictionary[key];
        }

        return default;
    }

    /// <inheritdoc />
    public virtual Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = Get<T>(key);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public virtual bool TryGetValue<T>(string key, out T? value)
    {
        value = Get<T>(key);
        return value != null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetKeys() => _dictionary.Keys.ToList();

    /// <inheritdoc />
    public IReadOnlyList<object?> GetValues() => _dictionary.Values.ToList();

    /// <inheritdoc />
    public IReadOnlyList<(string key, object? value)> GetList() => _dictionary.Select(t => (t.Key, t.Value)).ToList();
}