namespace TrueMoon.Argentis;

public static class Stack
{
    public static VStack Vertical(Action<IElementsList>? action = null)
    {
        var vStack = new VStack();
        action?.Invoke(vStack);
        return vStack;
    }

    public static HStack Horizontal(Action<IElementsList>? action = null)
    {
        var stack = new HStack();
        action?.Invoke(stack);
        return stack;
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
