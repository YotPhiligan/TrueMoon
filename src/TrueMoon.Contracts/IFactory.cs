namespace TrueMoon;

public interface IFactory<out T>
{
    T? Create();
    T? Create<TData>(TData? data = default);
}