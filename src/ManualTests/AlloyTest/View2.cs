using TrueMoon.Argentis;

namespace AlloyTest;

public class View2 : View<object>
{
    public View2() 
        => this.Content(new object(),d => Stack
            .Vertical());
}