namespace TrueMoon.Aluminum;

public class View2 : IWithContent
{
    public object? Content => Stack.Vertical(new Line(),
        new Rectagle(),
        new Button());
}

public class View1 : IWithContent
{
    private readonly View2 _view2;

    public View1(View2 view2)
    {
        _view2 = view2;
    }
    
    public object? Content => 
        Stack.Vertical(new Line(),
            new Rectagle(),
            new Button(),
            _view2);
}