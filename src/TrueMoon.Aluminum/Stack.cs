namespace TrueMoon.Aluminum;

public static class Stack
{
    public static VStack Vertical(params object[] elements) => new VStack(elements);
    
    public static VStack Vertical(Action<VStack> action, params object[] elements)
    {
        var vStack = new VStack(elements);
        action(vStack);
        return vStack;
    }
}