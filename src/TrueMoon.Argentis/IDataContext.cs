namespace TrueMoon.Argentis;

public interface IDataContext<TData>
{
    TData? DataContext { get; set; }
}