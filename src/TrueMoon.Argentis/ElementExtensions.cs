namespace TrueMoon.Argentis;

public static class ElementExtensions
{
    // public static T Append<T, TAppend>(this T element, TAppend append) 
    //     where T : IElement where TAppend : IElement
    // {
    //     element.Add(append);
    //     return element;
    // }
    //     
    // public static T Set<T, TProperty>(this T element, TProperty property) 
    //     where T : IElement
    //     where TProperty : IParameter
    // {
    //     element.SetProperty(property);
    //     return element;
    // }
    //
    // public static T Background<T>(this T element, string color)
    //     where T : IElement
    // {
    //     element.Set(new Properties.Background());
    //     return element;
    // }
        
    // public static T Frame<T>(this T element, int width = -1, int height = -1, int x = 0, int y = 0)
    //     where T : IElement
    // {
    //     element.Set(new Frame{Width = width, Height = height, X = x, Y = y});
    //     return element;
    // }
    
    public static T Width<T>(this T element, float width)
        where T : IElement
    {
        element.Set(nameof(Width), width);
        return element;
    }
    
    public static T Height<T>(this T element, float height)
        where T : IElement
    {
        element.Set(nameof(Height), height);
        return element;
    }

    // public static T Classes<T>(this T element, string classes)
    //     where T : IUiObject
    // {
    //     element.Set(ClassesKey, classes);
    //     return element;
    // }
    //
    // public static T Classes<T>(this T element, params string[] classes)
    //     where T : IUiObject
    // {
    //     element.Set(ClassesKey, classes);
    //     return element;
    // }
    //
    // public static T Style<T>(this T element, IStyle<T> style)
    //     where T : IElement
    // {
    //     element.Set(StyleKey, style);
    //     return element;
    // }
}