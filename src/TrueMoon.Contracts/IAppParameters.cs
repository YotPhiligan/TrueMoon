namespace TrueMoon;

public interface IAppParameters : IDictionary<string,object?>, IDisposable, IAsyncDisposable
{
    IAppParameters Set<T>(string key, T? value);
    T? Get<T>(string key);
}