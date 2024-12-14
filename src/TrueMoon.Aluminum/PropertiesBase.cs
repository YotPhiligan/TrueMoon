namespace TrueMoon.Aluminum;

public abstract class PropertiesBase : IProperties
{
    private readonly Dictionary<string,IProperty> _properties = [];
    
    public T? Get<T>(string? name = default)
    {
        var n = name ?? typeof(T).Name;
        if (_properties.TryGetValue(n, out var value))
        {
            return value is IProperty<T> m ? m.Value : default;
        }

        return default;
    }

    public void Set<T>(string name, T? value)
    {
        var n = string.IsNullOrWhiteSpace(name) ? typeof(T).Name : name;

        SetCore(n, value);
    }

    private void SetCore<T>(string name, T? value)
    {
        if (_properties.TryGetValue(name, out var property) && property is IProperty<T> m)
        {
            m.Value = value;
        }
        else
        {
            _properties.Add(name, new Property<T>(name,value));
        }
    }

    public void Set<T>(T? value)
    {
        var n = typeof(T).Name;

        SetCore(n, value);
    }

    public bool Has(string name) => _properties.ContainsKey(name);

    public bool Has<T>()
    {
        var n = typeof(T).Name;
        return Has(n);
    }

    public bool TryGet<T>(string name, out T? parameter)
    {
        var n = name ?? typeof(T).Name;
        if (_properties.TryGetValue(n, out var value))
        {
            if (value is IProperty<T> m)
            {
                parameter = m.Value;
                return true;
            }

            parameter = default;
            return true;
        }

        parameter = default;
        return false;
    }
}