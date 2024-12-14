namespace TrueMoon.Aluminum;

public interface IProperties
{
    T? Get<T>(string? name = default);
    void Set<T>(string name, T? value);
    void Set<T>(T? value);
    bool Has(string name);
    bool Has<T>();
    bool TryGet<T>(string name, out T? parameter);
}