using TrueMoon.Aluminum;

namespace TrueMoon.Alloy;

public static class ViewManagerExtensions
{
    public static void ShowEmpty(this IViewManager viewManager)
    {
        viewManager.Show(null);
    }
}