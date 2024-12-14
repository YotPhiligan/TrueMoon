namespace TrueMoon.Cobalt;

public interface IResolvingContext
{
    T Resolve<T>();
}