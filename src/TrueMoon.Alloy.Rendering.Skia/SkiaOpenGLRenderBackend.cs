using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>Borrowed desktop OpenGL context. The host keeps the context and callbacks alive until all sessions close.</summary>
/// <remarks>All operations use the session's creator thread. Skia changes GL state; the host must rebind its own state afterwards.</remarks>
public sealed class OpenGLUiTarget : UiRenderTarget
{
    internal Func<string, nint> GetProcedureAddress { get; }
    internal Action VerifyCurrent { get; }
    /// <summary>Describes an existing context without taking ownership of it.</summary>
    /// <param name="getProcedureAddress">Resolves desktop GL procedures for this context.</param>
    /// <param name="verifyCurrent">Ensures this live context is current, or throws before native calls.</param>
    public OpenGLUiTarget(Func<string, nint> getProcedureAddress, Action verifyCurrent)
    {
        ArgumentNullException.ThrowIfNull(getProcedureAddress); ArgumentNullException.ThrowIfNull(verifyCurrent);
        GetProcedureAddress = getProcedureAddress; VerifyCurrent = verifyCurrent;
    }
}

/// <summary>Creates session-owned GPU surfaces on a borrowed desktop OpenGL context.</summary>
public sealed class SkiaOpenGLRenderBackend : IRenderBackend
{
    /// <inheritdoc />
    public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
    {
        if (target is not OpenGLUiTarget gl) throw new ArgumentException("OpenGLUiTarget is required.", nameof(target));
        viewport.Validate(); gl.VerifyCurrent(); SkiaNativeLibrary.Initialize();
        return new SkiaOpenGLSurface(gl, viewport);
    }
}

/// <summary>A retained premultiplied RGBA GPU surface. Owns Skia resources, never the host GL context or destination framebuffer.</summary>
public sealed class SkiaOpenGLSurface : IUiRenderSurface
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly OpenGLUiTarget _host;
    private readonly GRGlInterface _interface;
    private readonly GRContext _context;
    private readonly SKSurface _measurement;
    private SKSurface? _surface;
    private UiViewport _viewport;
    private bool _disposed, _drawing, _hasFrame;
    /// <inheritdoc />
    public ITextLayoutService TextLayout { get; }

    internal SkiaOpenGLSurface(OpenGLUiTarget host, UiViewport viewport)
    {
        _host = host;
        foreach (var name in new[] { "glGetString", "glGetIntegerv" })
            if (host.GetProcedureAddress(name) == 0) throw new NotSupportedException($"The host GL loader is missing {name}.");
        Exception? loaderError = null;
        _interface = GRGlInterface.CreateOpenGl(name =>
        {
            try { return host.GetProcedureAddress(name); }
            catch (Exception error) { loaderError ??= error; return 0; }
        })!;
        if (loaderError != null || _interface == null || !_interface.Validate())
        {
            _interface?.Dispose();
            throw new NotSupportedException("Could not load a valid desktop OpenGL interface.", loaderError);
        }
        try
        {
            _context = GRContext.CreateGl(_interface) ?? throw new NotSupportedException("Skia OpenGL context creation failed.");
            try
            {
                _measurement = SKSurface.Create(new SKImageInfo(1, 1)) ?? throw new InvalidOperationException("Could not create text service surface.");
                TextLayout = new SkiaDrawingContext(_measurement.Canvas);
                try { Resize(viewport); }
                catch { _measurement.Dispose(); throw; }
            }
            catch { _context.AbandonContext(true); _context.Dispose(); throw; }
        }
        catch { _interface.Dispose(); throw; }
    }
    /// <inheritdoc />
    public void VerifyAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("OpenGL rendering requires its creator thread.");
        if (_drawing) throw new InvalidOperationException("The OpenGL surface is currently being drawn.");
        _host.VerifyCurrent();
        if (_context.IsAbandoned) throw new InvalidOperationException("The Skia OpenGL context was abandoned.");
    }
    /// <inheritdoc />
    public void Resize(UiViewport viewport)
    {
        VerifyAvailable(); viewport.Validate();
        if (viewport == _viewport) return;
        _context.ResetContext();
        if (viewport.IsEmpty || _surface == null || viewport.Width != _viewport.Width || viewport.Height != _viewport.Height)
        {
            var replacement = viewport.IsEmpty ? null : SKSurface.Create(_context, false,
                new SKImageInfo(viewport.Width, viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (!viewport.IsEmpty && replacement == null) throw new NotSupportedException("Could not allocate the OpenGL UI surface.");
            _surface?.Dispose(); _surface = replacement;
        }
        _viewport = viewport; _hasFrame = false;
    }
    /// <inheritdoc />
    public void Render(Element root, UiViewport viewport)
    {
        VerifyAvailable(); ArgumentNullException.ThrowIfNull(root); viewport.Validate();
        if (viewport != _viewport) throw new ArgumentException("Resize before rendering a different viewport.", nameof(viewport));
        if (_surface == null) return;
        _drawing = true; _hasFrame = false;
        try
        {
            _context.ResetContext();
            var canvas = _surface.Canvas; canvas.Clear(SKColors.Transparent); canvas.Save();
            try { canvas.Scale(viewport.Scale); root.Draw(new SkiaDrawingContext(canvas)); }
            finally { canvas.Restore(); }
            _context.Flush(); _hasFrame = true;
        }
        finally { _drawing = false; }
    }
    private void VerifyFrame()
    {
        VerifyAvailable();
        if (!_hasFrame || _surface == null) throw new InvalidOperationException("Render a nonempty viewport before using its OpenGL output.");
        _context.ResetContext();
    }
    /// <summary>Composites the retained UI on a borrowed RGBA8 framebuffer of the session's pixel size, entirely on the GPU.</summary>
    /// <param name="framebuffer">Host-owned destination FBO; zero selects the window's default framebuffer.</param>
    /// <param name="stencilBits">Actual stencil bit count of the destination.</param>
    /// <param name="sampleCount">Actual sample count of the destination; zero for a single sample.</param>
    /// <param name="clear">Clears the destination to transparent before drawing; false preserves the host scene.</param>
    /// <remarks>Does not swap buffers or preserve GL state. Host completes its scene first, then rebinds state before subsequent GL work.</remarks>
    public void PresentFramebuffer(uint framebuffer = 0, int stencilBits = 8, int sampleCount = 0, bool clear = true)
    {
        VerifyFrame();
        if (stencilBits < 0 || sampleCount < 0) throw new ArgumentOutOfRangeException(nameof(stencilBits));
        using var target = new GRBackendRenderTarget(_viewport.Width, _viewport.Height, sampleCount, stencilBits,
            new GRGlFramebufferInfo(framebuffer, 0x8058)); // GL_RGBA8
        using var destination = SKSurface.Create(_context, target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new NotSupportedException("Could not wrap the host OpenGL framebuffer.");
        using var image = _surface!.Snapshot();
        if (!image.IsTextureBacked) throw new InvalidOperationException("Expected a GPU-backed OpenGL UI frame.");
        if (clear) destination.Canvas.Clear(SKColors.Transparent);
        destination.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        _context.Flush();
    }
    /// <summary>Copies the completed GPU frame to an independent CPU image for diagnostics/export.</summary>
    /// <returns>A caller-owned immutable raster image valid after resize or session disposal.</returns>
    public SKImage ReadbackImage()
    {
        VerifyFrame();
        using var bitmap = new SKBitmap(new SKImageInfo(_viewport.Width, _viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (!_surface!.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            throw new InvalidOperationException("Could not read OpenGL UI pixels.");
        return SKImage.FromPixelCopy(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes)
            ?? throw new InvalidOperationException("Could not copy OpenGL readback.");
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        VerifyAvailable(); _context.ResetContext();
        _surface?.Dispose(); _measurement.Dispose();
        _context.AbandonContext(true); _context.Dispose(); _interface.Dispose(); _disposed = true;
    }
}

/// <summary>OpenGL output helpers for backend-independent sessions.</summary>
public static class OpenGLUiSessionExtensions
{
    private static SkiaOpenGLSurface Surface(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session); session.VerifyAccess();
        if (session.NeedsUpdate) throw new InvalidOperationException("Update UI before using its OpenGL output.");
        return session.Rendering as SkiaOpenGLSurface ?? throw new InvalidOperationException("Session is not using Skia OpenGL.");
    }
    /// <summary>Composites UI on the current host framebuffer without swapping or CPU copies.</summary>
    /// <param name="session">Updated OpenGL session.</param>
    /// <param name="framebuffer">Borrowed RGBA8 FBO at the session's size.</param>
    /// <param name="stencilBits">Actual destination stencil bits.</param>
    /// <param name="sampleCount">Actual destination sample count.</param>
    /// <param name="clear">Whether to clear the destination first.</param>
    public static void PresentOpenGL(this UiSession session, uint framebuffer = 0, int stencilBits = 8, int sampleCount = 0, bool clear = true)
        => Surface(session).PresentFramebuffer(framebuffer, stencilBits, sampleCount, clear);
    /// <summary>Reads UI pixels to an independent caller-owned CPU image for diagnostics.</summary>
    /// <param name="session">Updated OpenGL session.</param>
    /// <returns>Dispose the returned image when finished.</returns>
    public static SKImage ReadbackOpenGLImage(this UiSession session) => Surface(session).ReadbackImage();
}
