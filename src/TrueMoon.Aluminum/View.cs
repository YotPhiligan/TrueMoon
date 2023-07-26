namespace TrueMoon.Aluminum;

public class View : IView
{
    public T? Get<T>(string? name = default)
    {
        throw new NotImplementedException();
    }

    public void Set<T>(string name, T? value)
    {
        throw new NotImplementedException();
    }

    public void Set<T>(T? value)
    {
        throw new NotImplementedException();
    }

    public bool Has(string name)
    {
        throw new NotImplementedException();
    }

    public bool Has<T>()
    {
        throw new NotImplementedException();
    }

    public bool TryGet<T>(string name, out T? parameter)
    {
        throw new NotImplementedException();
    }

    public object? Content { get; }
}