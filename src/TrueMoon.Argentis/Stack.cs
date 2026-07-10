namespace TrueMoon.Argentis;

public static class Stack
{
    public static VStack Vertical(Action<IElementsList>? action = null)
    {
        var vStack = new VStack();
        
        return vStack;
    }
}

public interface IElementsList
{
    void Add1<TElement>(TElement element) where TElement : IElement;
}

public static class ElementsListExtensions
{
    public static IElementsList Add<T>(this IElementsList elementsList, T element)
        where T : IElement
    {
        elementsList.Add1(element);
        return elementsList;
    }
}