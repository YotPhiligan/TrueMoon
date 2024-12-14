using Silk.NET.OpenGL;
using SkiaSharp;

namespace TrueMoon.Alloy;

public class SkiaGlViewPresenter : IViewPresenter
{
    private GRGlInterface? _grGlInterface;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private GL? _gl;
    private SkiaContentPresenterContext? _contentPresenterContext;

    private void RecreateRenderTarget(int width, int height)
    {
        ReleaseRenderTarget();
        
        _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, new GRGlFramebufferInfo(0, 0x8058)); // 0x8058 = GL_RGBA8`
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        _canvas = _surface.Canvas;
        
        _contentPresenterContext = new SkiaContentPresenterContext(_canvas);
    }

    public void Present(double t, IVisualTree visualTree)
    {
        _grContext.ResetContext();
        
        //_canvas.Clear(SKColors.Transparent);
        using var red = new SKPaint();
        red.Color = new SKColor(255, 0, 0, 255);
        _canvas.DrawCircle(150, 150, 100, red);
        
        using var skFont = new SKFont();
        skFont.Size = 14;
        skFont.Typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var blob = SKTextBlob.Create($"text test {DateTime.Now}".AsSpan(), skFont);
        
        using var fontPaint = new SKPaint();
        fontPaint.Color = SKColors.White;
        _canvas.DrawText(blob, 100, 100, fontPaint);
        
        foreach (var visual in visualTree)
        {
            if (_contentPresenterContext == null)
            {
                break;
            }

            visual.ContentPresenter?.Present(t, _contentPresenterContext);
        }
        
        _canvas.Flush();
        
        ClearState();
    }

    private void ClearState()
    {
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.ProgramPointSize);
        _gl.BindVertexArray(0); // Restore default VAO 
        _gl.FrontFace(FrontFaceDirection.CW);
        _gl.Enable(EnableCap.FramebufferSrgb);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.FramebufferOes, 0); // FramebufferExt
        _gl.UseProgram(0);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.DrawBuffer(DrawBufferMode.Back);
        _gl.Enable(EnableCap.Dither);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.Multisample);
        _gl.Disable(EnableCap.ScissorTest);
    }

    public void Release()
    {
        ReleaseRenderTarget();

        _grContext?.Dispose();
        _grGlInterface?.Dispose();
    }

    private void ReleaseRenderTarget()
    {
        _canvas?.Dispose();
        _surface?.Dispose(); 
        _renderTarget?.Dispose();

        _contentPresenterContext = null;
    }

    public void Resize(int width, int height) => RecreateRenderTarget(width, height);

    public void Initialize(IGraphicsPlatform graphicsPlatform)
    {
        var view = graphicsPlatform.GetNativeWindow()!;
        _gl = graphicsPlatform is IGlGraphicsPlatform platform ? platform.Gl : throw new InvalidOperationException($"Invalid graphics platform - \"{graphicsPlatform}\"");
        _grGlInterface = GRGlInterface.Create((name => view.GLContext!.TryGetProcAddress(name, out var addr) ? addr : 0));
        _grGlInterface.Validate();
        _grContext = GRContext.CreateGl(_grGlInterface);
        RecreateRenderTarget(view.FramebufferSize.X, view.FramebufferSize.Y);
    }
}