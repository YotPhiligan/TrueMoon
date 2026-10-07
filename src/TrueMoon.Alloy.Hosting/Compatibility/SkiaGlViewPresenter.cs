using SkiaSharp;

namespace TrueMoon.Alloy.Hosting.Compatibility;

/// <summary>Compatibility presenter for legacy VisualTree applications. New Argentis views use Hosting.UseSkiaOpenGL.</summary>
public class SkiaGlViewPresenter : IViewPresenter
{
    private GRGlInterface? _interface;
    private GRContext? _context;
    private GRBackendRenderTarget? _target;
    private SKSurface? _surface;
    private SkiaContentPresenterContext? _content;

    /// <inheritdoc />
    public void Initialize(IGraphicsPlatform graphicsPlatform)
    {
        if (graphicsPlatform is not IGlGraphicsPlatform) throw new ArgumentException("An OpenGL platform is required.", nameof(graphicsPlatform));
        var window = graphicsPlatform.GetNativeWindow() ?? throw new InvalidOperationException("The window is not initialized.");
        var gl = window.GLContext ?? throw new InvalidOperationException("The GL context is not initialized.");
        gl.MakeCurrent();
        try
        {
            Exception? loaderError = null;
            _interface = GRGlInterface.CreateOpenGl(name =>
            {
                try { return gl.TryGetProcAddress(name, out var address) ? address : 0; }
                catch (Exception error) { loaderError ??= error; return 0; }
            });
            if (loaderError != null || _interface == null || !_interface.Validate())
                throw new NotSupportedException("Invalid OpenGL interface.", loaderError);
            _context = GRContext.CreateGl(_interface) ?? throw new NotSupportedException("Skia OpenGL context creation failed.");
            Resize(window.FramebufferSize.X, window.FramebufferSize.Y);
        }
        catch { Release(); throw; }
    }
    /// <inheritdoc />
    public void Present(double t, IVisualTree visualTree)
    {
        if (_surface == null || _content == null) return;
        _context!.ResetContext();
        _surface.Canvas.Clear(SKColors.Transparent);
        foreach (var visual in visualTree) visual?.Presenter?.Present(t, _content);
        _context.Flush();
    }
    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        ReleaseTarget();
        if (width <= 0 || height <= 0) return;
        if (_context == null) throw new InvalidOperationException("Initialize the presenter first.");
        _context.ResetContext();
        _target = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, 0x8058));
        _surface = SKSurface.Create(_context, _target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
            ?? throw new NotSupportedException("Could not wrap the OpenGL window framebuffer.");
        _content = new SkiaContentPresenterContext(_surface.Canvas);
    }
    private void ReleaseTarget()
    {
        _content = null; _surface?.Dispose(); _surface = null; _target?.Dispose(); _target = null;
    }
    /// <inheritdoc />
    public void Release()
    {
        ReleaseTarget();
        _context?.AbandonContext(true); _context?.Dispose(); _context = null;
        _interface?.Dispose(); _interface = null;
    }
}
