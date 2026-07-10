namespace TrueMoon.Argentis;

public interface IProperty
{
    string Name { get; }
}

public interface IProperty<T> : IProperty
{
    T? Value { get; set; }
}