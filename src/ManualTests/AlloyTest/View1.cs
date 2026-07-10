using TrueMoon.Argentis;

namespace AlloyTest;

public class View1 : View
{
    public View1(View2 view2) : base()
        => this.Content(() => Stack
            .Vertical(list => list
                    .Add(Shapes.Line())
                    .Add(Shapes.Rectagle()
                        .Width(200)
                        .Height(100))
                    //.Add(view2)
                )
            .Width(100));
}