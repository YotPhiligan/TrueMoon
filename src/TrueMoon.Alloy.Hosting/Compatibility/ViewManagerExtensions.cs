using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting.Compatibility;

public static class ViewManagerExtensions
{
    public static void ShowEmpty(this IViewManager viewManager)
    {
        viewManager.Show(null);
    }
}