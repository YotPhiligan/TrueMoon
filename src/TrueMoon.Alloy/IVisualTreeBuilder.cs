using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

public interface IVisualTreeBuilder
{
    IVisualTree Build(IView? view);
}