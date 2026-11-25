namespace TrueMoon.Argentis.Views;

public interface IView : IProperties, IContent
{

}

public interface IView<TData> : IView, IContent<TData>
{
     
}