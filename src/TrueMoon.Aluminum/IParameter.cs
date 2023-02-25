namespace TrueMoon.Aluminum;

public interface IParameter
{
    string Name { get; }
}

public interface IParameter<T> : IParameter
{
    T? Value { get; set; }

    void OnChanged(Action<T?> action);
}