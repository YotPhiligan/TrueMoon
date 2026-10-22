using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

// A provider can dispose aliases separately. This facade never owns the shared registry.
internal sealed class BorrowedUiSessionFactory(HostedUiSessionFactory owner) : IUiWindowSessionFactory
{
    public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => owner.SupportsWindowGraphics(graphicsApi);
    public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
        => owner.Create(root, target, viewport, clipboard);
}
