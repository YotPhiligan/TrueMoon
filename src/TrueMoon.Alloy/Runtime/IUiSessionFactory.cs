using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Creates UI sessions after their external graphics target becomes ready.</summary>
public interface IUiSessionFactory : IFactory<UiSession, UiSessionCreation>
{
    /// <summary>Creates and tracks a caller-owned session. Root ownership transfers on success.</summary>
    UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null);

    UiSession IFactory<UiSession, UiSessionCreation>.Create(UiSessionCreation arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Create(arguments.Root, arguments.Target, arguments.Viewport, arguments.Clipboard);
    }
}
