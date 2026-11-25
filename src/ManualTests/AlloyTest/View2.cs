using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace AlloyTest;

public class View2 : View<object>
{
    public View2() => Content(new object(), static ui => ui.VStack());
}


public class Test3View : View<object>
{
    public Test3View(object data)
        => Content(data, static ui => ui.Panel(
            ui.VStack(
                ui.Text("Header text"),
                ui.HStack(ui.TextBox(), ui.Button("Add"))))
            .Background(Color.Transparent));
}

public interface IUiContext
{
}

public static class TestExtensions
{
    public static T WithChildren1<T>(this T element, Func<T, IEnumerable<IElement>> func) where T : ElementList
    {
        // test
        return element;
    }
}
