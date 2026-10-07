using TrueMoon.Argentis;

namespace TrueMoon.Alloy;

/// <summary>Framebuffer pixels and the number of pixels per logical UI unit.</summary>
public readonly record struct UiViewport(int Width, int Height, float Scale = 1)
{
    /// <summary>Validates dimensions; zero-sized framebuffers are suspended.</summary>
    public void Validate()
    {
        if (Width < 0 || Height < 0 || !float.IsFinite(Scale) || Scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(UiViewport));
    }
    /// <summary>Whether drawing must be suspended.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;
}
/// <summary>A backend-specific borrowed host connection. The UI never owns its device/window.</summary>
public abstract class UiRenderTarget;
/// <summary>Creates session-owned rendering resources on a host target.</summary>
public interface IRenderBackend
{
    /// <summary>Creates a surface. The caller owns the returned object.</summary>
    IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport);
}
/// <summary>Rendering resources owned by one UI session, independent of window management.</summary>
public interface IUiRenderSurface : IDisposable
{
    /// <summary>The text service shared by measurement and drawing.</summary>
    ITextLayoutService TextLayout { get; }
    /// <summary>Rejects mutations/disposal while a frame is borrowed by the host.</summary>
    void VerifyAvailable();
    /// <summary>Resizes the surface after the host returns its borrowed frame.</summary>
    void Resize(UiViewport viewport);
    /// <summary>Draws one UI tree using logical coordinates.</summary>
    void Render(Element root, UiViewport viewport);
}
/// <summary>Optional platform clipboard services. The host owns their lifetime.</summary>
public interface IUiClipboard
{
    /// <summary>Reads text.</summary>
    string? GetText();
    /// <summary>Writes text.</summary>
    void SetText(string text);
}
/// <summary>Input routing result for a game or window host.</summary>
public readonly record struct UiInputResult(bool Handled, bool PointerCaptured, bool KeyboardFocused);

/// <summary>A platform window that supplies normalized input, viewport and frame callbacks.</summary>
public interface IWindowHost : IDisposable
{
    /// <summary>Current pixel dimensions and DPI scale.</summary>
    UiViewport Viewport { get; }
    /// <summary>Raised on the window/session thread.</summary>
    event Action? RenderRequested;
    /// <summary>Normalized input in logical coordinates.</summary>
    event Action<UiInput>? Input;
    /// <summary>Runs the window event loop on its creator thread.</summary>
    void Run();
    /// <summary>Closes the window on its creator thread.</summary>
    void Close();
}
