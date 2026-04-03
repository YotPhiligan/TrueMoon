namespace TrueMoon.Aluminum;

public interface IContent
{
    void Content(Func<object> func);
    object? GetContent();
}

public interface IContent<TData> : IDataContext<TData>, IContent
{
    void Content(TData? data, Func<TData?,object> func);
}