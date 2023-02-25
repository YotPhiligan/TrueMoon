namespace TrueMoon.Aluminum;

public class Parameter<T> : IParameter<T>
{
    private List<Action<T?>>? _changeDelegates;
    private T? _value;

    public Parameter(string name, T? value = default)
    {
        Name = name;
        Value = value;
    }
    
    public Parameter()
    {
        Name = GetType().Name;
    }
    
    public string Name { get; }

    public T? Value
    {
        get => _value;
        set
        {
            if (Equals(_value, value)) return;
            
            _value = value;
            Change();
        }
    }

    protected virtual void Change()
    {
        if (_changeDelegates == null) return;
        
        foreach (var action in _changeDelegates)
        {
            action(_value);
        }
    }
    
    public void OnChanged(Action<T?> action)
    {
        _changeDelegates ??= new ();
        _changeDelegates.Add(action);
    }
}