namespace TrueMoon.Aluminum;

public static class ViewExtensions
{
    public static TView Content<TView>(this TView view, Func<object> func)
        where TView : IView
    {
        view.Content(func);
        return view;
    }
}