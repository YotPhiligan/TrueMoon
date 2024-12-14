using System.Collections;
using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public class Visual : IVisual
{
    private IVisual[]? _items;
    private int _itemsCount;
    public IContentPresenter? Presenter { get; }

    public Visual(IContentPresenter? presenter)
    {
        Presenter = presenter;
    }

    public IEnumerator<IVisual> GetEnumerator()
    {   
        if (_items == null)
        {
            yield break;
        }   
        
        foreach (var visual in _items)
        {
            yield return visual;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsVisible { get; set; }
    public IVisual? Root { get; set; }
    public IContentPresenter? ContentPresenter { get; }
    
    public void Add(IVisual visual)
    {
        if (_items == null)
        {
            _items = new IVisual[2];
            _items[0] = visual;
        }
        else
        {
            if (_itemsCount >= _items.Length)
            {
                Array.Resize(ref _items, _items.Length*2);
            }
            _items[_itemsCount] = visual;
        }
        _itemsCount++;

        visual.Root = this;
    }
}