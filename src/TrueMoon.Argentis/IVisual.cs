namespace TrueMoon.Argentis;

public interface IVisual : IEnumerable<IVisual>
{
    int X { get; set; }
    int Y { get; set; }
    int Width { get; set; }
    int Height { get; set; }
    bool IsVisible { get; set; }
    
    IVisual? Root { get; set; }
    IContentPresenter? Presenter { get; }

    void Add(IVisual visual);
}