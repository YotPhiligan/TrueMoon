using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace TrueMoon.Alloy;

public class GlGraphicsPlatform : IGlGraphicsPlatform
{
    private IWindow _nativeWindow;
    private GL _gl;

    public IWindow? GetNativeWindow() => _nativeWindow;

    public GL Gl => _gl;
    
    public void Initialize()
    {
        var opts = WindowOptions.Default with
        {
            Position = new(100, 100),
            Size = new(960, 540),
            Title = "Window",
            API = GraphicsAPI.Default,
            //VSync = false,
            //ShouldSwapAutomatically = false,
            //WindowBorder = WindowBorder.Hidden,
            TransparentFramebuffer = true,
            PreferredStencilBufferBits = 8,
            PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
            FramesPerSecond = 60,
            UpdatesPerSecond = 60,
        };

        _nativeWindow = Window.Create(opts);

        _nativeWindow.Load += () =>
        {
            _gl = GL.GetApi(_nativeWindow);
        };
        
        _nativeWindow.Render += _ =>
        {
            _gl.Clear((uint) ClearBufferMask.ColorBufferBit);
            
        };

        // _nativeWindow.Update += d =>
        // {
        //
        // };

        _nativeWindow.Closing += () =>
        {
            Release();
            _gl?.Dispose();
        };

        // _nativeWindow.Resize += size =>
        // {
        //     Resize(size.X, size.Y);
        // };

    }

    private void Release()
    {
        
    }

    public void Dispose()
    {
        _nativeWindow.Dispose();
    }
}