using TrueMoon.Aether.Frames;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

public sealed class EditorViewport : Element
{
    private SkiaImageSource? _image;
    private ViewportFrame? _frame;
    private Func<bool>? _current;
    public EditorViewport() => Own(new ImageLifetime(this));
    public ViewportFrame? DrawnFrame { get; private set; }
    public Rect ImageBounds { get; private set; }
    public void Upload(SkiaImageSource image, ViewportFrame frame, Func<bool> current)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(image); ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(current);
        var previous = _image; _image = image; _frame = frame; _current = current; previous?.Dispose(); Invalidate();
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(600, 420);
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, new(10, 15, 26)); DrawnFrame = null; ImageBounds = Bounds;
        if (_image != null && _frame != null && _current!())
        {
            var surface = _frame.Presentation.Surface; var scale = Math.Min(Bounds.Width / surface.Width, Bounds.Height / surface.Height);
            ImageBounds = new(Bounds.X + (Bounds.Width - surface.Width * scale) / 2, Bounds.Y + (Bounds.Height - surface.Height * scale) / 2, surface.Width * scale, surface.Height * scale);
            context.Image(_image, ImageBounds); DrawnFrame = _frame; return;
        }
        var line = new Color(28, 37, 52);
        for (var x = Bounds.X; x < Bounds.X + Bounds.Width; x += 32) context.Fill(new(x, Bounds.Y, 1, Bounds.Height), line);
        for (var y = Bounds.Y; y < Bounds.Y + Bounds.Height; y += 32) context.Fill(new(Bounds.X, y, Bounds.Width, 1), line);
        context.Text("Соберите проект и запустите сцену", Bounds.X + 24, Bounds.Y + Math.Max(16, Bounds.Height / 2 - 12), new(142, 154, 170), 16, FontFamily);
    }
    private sealed class ImageLifetime(EditorViewport owner) : IDisposable
    { public void Dispose() { owner._image?.Dispose(); owner._image = null; owner._frame = null; owner._current = null; owner.DrawnFrame = null; } }
}
