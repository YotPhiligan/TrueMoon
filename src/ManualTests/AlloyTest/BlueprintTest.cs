using TrueMoon.Aluminum;

namespace AlloyTest;

public class View2 : View<object>
{
    public View2(object data) 
        => this.Content(data,d => Stack
            .Vertical(Shapes.Line(),
                Shapes.Rectagle()));
}

public class View1 : View
{
    public View1(View2 view2) : base()
        => this.Content(() => Stack
            .Vertical(Shapes.Line(),
                Shapes.Rectagle(),
                new Button(),
                view2));
}