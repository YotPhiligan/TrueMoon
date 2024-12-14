using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public class VisualTreeBuilder : IVisualTreeBuilder
{
    private readonly IFactory<IContentPresenter> _contentPresenterFactory;

    public VisualTreeBuilder(IFactory<IContentPresenter> contentPresenterFactory)
    {
        _contentPresenterFactory = contentPresenterFactory;
    }
    
    public IVisualTree Build(IView? view)
    {
        IVisual root = CreateVisual(view);
        
        var nestedContent = view?.GetContent();
        BuildContent(root, nestedContent);
        
        IVisualTree visualTree = new VisualTree(root);
        return visualTree;
    }

    private void BuildContent(IVisual? root, object? content)
    {
        if (content == null)
        {
            return;
        }
        
        var visual = CreateVisual(content);
        root?.Add(visual);
     
        switch (content)
        {
            case IContent c:
            {
                var nestedContent = c.GetContent();
                BuildContent(visual, nestedContent);
                break;
            }
            case IEnumerable<object> objects:
            {
                foreach (var o in objects)
                {
                    BuildContent(visual, o);
                }
                break;
            }
        }
    }

    private IVisual CreateVisual<T>(T content)
    {
        var presenter = _contentPresenterFactory.Create(content);
        IVisual visual = new Visual(presenter);
        return visual;
    }
}