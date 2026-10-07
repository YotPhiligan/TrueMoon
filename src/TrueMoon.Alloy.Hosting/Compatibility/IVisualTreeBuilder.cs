using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public interface IVisualTreeBuilder
{
    IVisualTree Build(IView? view);
}