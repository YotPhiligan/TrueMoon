using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>Thread-safe configuration storage with invariant typed conversion.</summary>
public abstract class ConfigurableBase : IConfigurable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, object?> _dictionary;

    protected ConfigurableBase(Dictionary<string, object?>? dictionary = default)
    {
        _dictionary = dictionary is null ? new() : new(dictionary);
    }

    public virtual bool Exist(string key)
    {
        lock (_lock) return _dictionary.ContainsKey(key);
    }

    public virtual void Set<T>(string key, T? value)
    {
        lock (_lock) _dictionary[key] = value;
    }

    public virtual T? Get<T>(string key)
    {
        object? stored;
        lock (_lock)
        {
            if (!_dictionary.TryGetValue(key, out stored)) return default;
        }
        if (TryConvert(stored, out T? result)) return result;
        throw new FormatException($"Configuration value '{key}' cannot be converted to {typeof(T)}.");
    }

    public virtual Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Get<T>(key));
    }

    public virtual bool TryGetValue<T>(string key, out T? value)
    {
        object? stored;
        lock (_lock)
        {
            if (!_dictionary.TryGetValue(key, out stored))
            {
                value = default;
                return false;
            }
        }
        return TryConvert(stored, out value);
    }

    private static bool TryConvert<T>(object? stored, out T? value)
    {
        value = default;
        if (stored is null) return default(T) is null;
        if (stored is T typed)
        {
            value = typed;
            return true;
        }
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        try
        {
            object? converted;
            if (stored is JsonElement json)
            {
                // Strings share the same conversion rules as environment and argument values.
                if (json.ValueKind == JsonValueKind.String)
                    return TryConvert(json.GetString(), out value);
                converted = JsonSerializer.Deserialize(json.GetRawText(), typeof(T));
            }
            else if (target.IsEnum && stored is string enumText)
                converted = Enum.Parse(target, enumText, ignoreCase: true);
            else if (stored is string text && TypeDescriptor.GetConverter(target) is { } converter && converter.CanConvertFrom(typeof(string)))
                converted = converter.ConvertFrom(null, CultureInfo.InvariantCulture, text);
            else if (stored is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
                converted = Convert.ChangeType(stored, target, CultureInfo.InvariantCulture);
            else return false;
            value = (T?)converted;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException or NotSupportedException or JsonException)
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetKeys()
    {
        lock (_lock) return _dictionary.Keys.ToArray();
    }

    public IReadOnlyList<object?> GetValues()
    {
        lock (_lock) return _dictionary.Values.ToArray();
    }

    public IReadOnlyList<(string key, object? value)> GetList()
    {
        lock (_lock) return _dictionary.Select(item => (item.Key, item.Value)).ToArray();
    }
}
