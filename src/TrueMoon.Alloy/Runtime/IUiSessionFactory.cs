using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

/// <summary>Creates UI sessions after their external graphics target becomes ready.</summary>
public interface IUiSessionFactory
{
    /// <summary>Creates and tracks a caller-owned session. Root ownership transfers on success.</summary>
    UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null);
}
