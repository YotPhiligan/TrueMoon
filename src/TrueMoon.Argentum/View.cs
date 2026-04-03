namespace TrueMoon.Aluminum;

public abstract class View : PropertiesBase, IView
{
    protected Func<object>? _contentFunc;
    public void Content(Func<object> func)
    {
        _contentFunc = func;
    }

    public virtual object? GetContent()
    {
        throw new NotImplementedException();
    }
}

public abstract class View<TData> : View, IView<TData>
{
    public void Content(TData? data, Func<TData?, object> func)
    {
        DataContext = data;
        _contentFunc = () => func(DataContext);
    }

    public TData? DataContext { get; set; }
}