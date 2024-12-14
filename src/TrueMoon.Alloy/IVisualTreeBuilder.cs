using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public interface IVisualTreeBuilder
{
    IVisualTree Build(IView? view);
}