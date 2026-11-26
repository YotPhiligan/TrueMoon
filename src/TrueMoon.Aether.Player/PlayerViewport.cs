using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Aether.Player;

/// <summary>The window's owner-thread link to its attached viewport. The UI tree owns the control.</summary>
internal sealed class PlayerViewport
{
    private PlayerCanvas? _canvas;

    public bool IsAttached => _canvas is { IsAttached: true };
    public ViewportFrame? Drawn => IsAttached ? _canvas!.Drawn : null;

    // Only the window root factory connects a view, not every product of IFactory<PlayerView>.
    public PlayerView Track(PlayerView view)
    {
        if (view is not { Parent: null, IsAttached: false, IsDisposed: false })
            throw new InvalidOperationException("The Player view factory must return a fresh, detached root.");

        view.AttachmentChanged += OnAttachmentChanged;
        return view;

        void OnAttachmentChanged()
        {
            if (!view.IsAttached) return;
            if (_canvas != null) throw new InvalidOperationException("A Player viewport is already attached.");
            var canvas = view.Canvas;
            canvas.OwnAttachment(new Attachment(this));
            _canvas = canvas;
        }
    }

    public void Upload(SkiaImageSource image, ViewportFrame frame, GameHudSnapshot? hud, Func<bool> current)
    {
        if (_canvas is not { IsAttached: true } canvas)
            throw new InvalidOperationException("The Player viewport is not attached.");
        canvas.Upload(image, frame, hud, current);
    }

    private sealed class Attachment(PlayerViewport owner) : IDisposable
    {
        public void Dispose() => owner._canvas = null;
    }
}
