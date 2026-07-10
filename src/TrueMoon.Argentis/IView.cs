namespace TrueMoon.Argentis;

public interface IView : IProperties, IContent
{

}

public interface IView<TData> : IView, IContent<TData>
{
     
}