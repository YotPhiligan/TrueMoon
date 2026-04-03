namespace TrueMoon.Services;

public interface IServiceResolver : IServiceProvider
{
    T Resolve<T>();
    T? TryResolve<T>();
}