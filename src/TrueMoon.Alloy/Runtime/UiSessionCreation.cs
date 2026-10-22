using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Explicit root/target arguments. Root ownership transfers only after successful creation.</summary>
/// <param name="Root">Detached root.</param><param name="Target">Borrowed graphics target.</param>
/// <param name="Viewport">Initial viewport.</param><param name="Clipboard">Borrowed clipboard.</param>
public sealed record UiSessionCreation(Element Root, UiRenderTarget Target, UiViewport Viewport, IUiClipboard? Clipboard = null);
