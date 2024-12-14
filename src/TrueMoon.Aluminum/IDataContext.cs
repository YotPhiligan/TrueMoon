namespace TrueMoon.Aluminum;

public interface IDataContext<TData>
{
    TData? DataContext { get; set; }
}