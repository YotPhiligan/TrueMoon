using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>An in-memory rendering target; requires neither a window nor a GPU device.</summary>
public sealed class RasterUiTarget : UiRenderTarget;

/// <summary>Creates session-owned CPU surfaces using the same Skia drawing/text adapter as Vulkan.</summary>
public sealed class SkiaRasterRenderBackend : IRenderBackend
{
    /// <inheritdoc />
    public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
    {
        if (target is not RasterUiTarget) throw new ArgumentException("RasterUiTarget is required.", nameof(target));
        viewport.Validate();
        SkiaNativeLibrary.Initialize();
        return new SkiaRasterSurface(viewport);
    }
}

/// <summary>A CPU RGBA premultiplied surface owned by one UI session.</summary>
/// <remarks>Operations run on the creator thread. Snapshots have their own lifetime and do not block resize or disposal.</remarks>
public sealed class SkiaRasterSurface : IUiRenderSurface
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly SKSurface _measurement;
    private readonly SkiaDrawingResources _drawingResources;
    private SKSurface? _surface;
    private UiViewport _viewport;
    private bool _disposed, _drawing, _hasFrame;
    /// <inheritdoc />
    public ITextLayoutService TextLayout { get; }

    internal SkiaRasterSurface(UiViewport viewport)
    {
        _measurement = SKSurface.Create(new SKImageInfo(1, 1))
            ?? throw new InvalidOperationException("Could not create text service surface.");
        try
        {
            _drawingResources = new SkiaDrawingResources();
            TextLayout = new SkiaDrawingContext(_measurement.Canvas, _drawingResources);
            Resize(viewport);
        }
        catch (Exception error)
        { UiCleanup.Complete(error, () => _drawingResources?.Dispose(), _measurement.Dispose); throw; }
    }

    /// <inheritdoc />
    public void VerifyAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Raster rendering requires its creator thread.");
        if (_drawing) throw new InvalidOperationException("The raster surface is currently being drawn.");
    }

    /// <inheritdoc />
    public void Resize(UiViewport viewport)
    {
        VerifyAvailable(); viewport.Validate();
        if (viewport == _viewport) return;
        if (viewport.IsEmpty || _surface == null || viewport.Width != _viewport.Width || viewport.Height != _viewport.Height)
        {
            var replacement = viewport.IsEmpty ? null : SKSurface.Create(
                new SKImageInfo(viewport.Width, viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!viewport.IsEmpty && replacement == null)
                throw new InvalidOperationException("Could not allocate the raster UI surface.");
            _surface?.Dispose();
            _surface = replacement;
        }
        _viewport = viewport; _hasFrame = false;
    }

    /// <inheritdoc />
    public void Render(Element root, UiViewport viewport)
    {
        VerifyAvailable(); ArgumentNullException.ThrowIfNull(root); viewport.Validate();
        if (viewport != _viewport) throw new ArgumentException("Resize the surface before rendering a different viewport.", nameof(viewport));
        if (_surface == null) return;
        _drawing = true; _hasFrame = false;
        try
        {
            var canvas = _surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            try { canvas.Scale(viewport.Scale); root.Draw(new SkiaDrawingContext(canvas, _drawingResources)); }
            finally { canvas.Restore(); }
            _hasFrame = true;
        }
        finally { _drawing = false; }
    }

    /// <summary>Returns an immutable CPU image of the last successful render.</summary>
    /// <returns>A caller-owned SKImage, valid across later renders, resize and session disposal.</returns>
    public SKImage Snapshot()
    {
        VerifyAvailable();
        if (!_hasFrame || _surface == null) throw new InvalidOperationException("Render a nonempty viewport before taking a snapshot.");
        return _surface.Snapshot() ?? throw new InvalidOperationException("Could not snapshot the raster UI surface.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        VerifyAvailable();
        _disposed = true;
        UiCleanup.Complete(null, _drawingResources.Dispose, () => _surface?.Dispose(), _measurement.Dispose);
    }
}

/// <summary>CPU-image export from the backend-independent UI session.</summary>
public static class RasterUiSessionExtensions
{
    /// <summary>Takes a snapshot after Update has applied all pending changes.</summary>
    /// <param name="session">A live raster session on its creator thread.</param>
    /// <returns>A caller-owned immutable SKImage; dispose it when finished.</returns>
    public static SKImage SnapshotRasterImage(this UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.VerifyRendering();
        if (session.NeedsUpdate) throw new InvalidOperationException("Update UI before taking its raster snapshot.");
        return (session.Rendering as SkiaRasterSurface
            ?? throw new InvalidOperationException("Session is not using Skia raster.")).Snapshot();
    }
}
