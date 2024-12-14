namespace TrueMoon.Aluminum;

public interface IView : IProperties, IContent
{

}

public interface IView<TData> : IView, IContent<TData>
{
     
}