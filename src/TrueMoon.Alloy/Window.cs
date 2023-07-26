using Silk.NET.Input;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Extensions.Veldrid;
using Veldrid;

namespace TrueMoon.Alloy;

public class Window : IDisposable
{
    private IWindow _window;
    private GraphicsDevice? _graphicsDevice = default;
    private CommandList? _commandList = default;
    private IInputContext _input;


    public void Initialize()
    {
        const GraphicsBackend preferredBackend = GraphicsBackend.Vulkan;
        var opts = WindowOptions.Default;
        opts.Position = new(100, 100);
        opts.Size = new(960, 540);
        opts.Title = "Window";
        opts.API = preferredBackend.ToGraphicsAPI();
        opts.VSync = false;
        opts.ShouldSwapAutomatically = false;
        //opts.WindowBorder = WindowBorder.Hidden;
        opts.TransparentFramebuffer = true;

        _window = Silk.NET.Windowing.Window.Create(opts);

        _window.Load += () =>
        {
            _input = _window.CreateInput();
            _graphicsDevice = _window.CreateGraphicsDevice(new()
            {
                PreferStandardClipSpaceYDirection = true, 
                PreferDepthRangeZeroToOne = true,
                SwapchainDepthFormat = PixelFormat.R16_UNorm
            }, preferredBackend);
            
            var factory = _graphicsDevice.ResourceFactory;

            // test.CreateResources(graphicsDevice);
            //
            // cube.CreateResources(graphicsDevice);
            //
            // test2.CreateResources(graphicsDevice);
            // test3.CreateResources(graphicsDevice);
            
            _commandList = factory.CreateCommandList();
        };

        _window.Render += t =>
        {
            // Begin() must be called before commands can be issued.
            _commandList.Begin(); 
          
            // We want to render directly to the output window.
            _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
            _commandList.ClearColorTarget(0, RgbaFloat.Clear);
            _commandList.ClearDepthStencil(1f);
            
            // test.Render(commandList);
            //
            // cube.Draw(commandList, (float)t);
            //
            // test2.Render(commandList);
            //
            // test3.Render(commandList, graphicsDevice);
            

            // End() must be called before commands can be submitted for execution.
            _commandList.End();
            _graphicsDevice.SubmitCommands(_commandList);

            // Once commands have been submitted, the rendered image can be presented to the application window.
            _graphicsDevice.SwapBuffers();
        };

        _window.Update += d =>
        {

        };

        _window.Closing += () =>
        {
            ReleaseResources();
            _commandList?.Dispose();
            _graphicsDevice?.Dispose();
            _input.Dispose();
        };

        _window.Resize += size =>
        {
            _graphicsDevice.ResizeMainWindow((uint)size.X, (uint)size.Y);
        };
    }

    private void ReleaseResources()
    {
        
    }

    public void Run()
    {
        _window.Run();
    }

    public void Invoke(Action action)
    {
        _window.Invoke(action);
    }
    
    public void Dispose()
    {
        _window.Dispose();
    }
}