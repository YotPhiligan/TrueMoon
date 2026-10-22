using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Aether.Editor;

// Borrows the UI owner/context; Aether owns imports/transport, EditorViewport owns retained Skia copies.
internal sealed class EditorViewportBinding : IFactory<IGameViewportTransport, GpuResourceHost>
{
    private HostedUiWindow _window = null!;
    private AetherHost _engine = null!;
    private UiSession _ui = null!;
    private EditorView _view = null!;
    private GpuOpenGlContext _context = null!;
    private GpuAdapterId _adapter = null!;
    private Action _current = null!;
    private int _owner;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Ready => _ready.Task;
    public IGameViewportTransport Create(GpuResourceHost gpu) => new GpuViewportTransport(gpu, () => (_adapter, _context), Consume, runtime => (NativeRuntimeLease)runtime);
    internal void Observe(UiSession ui, EditorView view) { _ui = ui; _view = view; }
    private void Consume(GpuViewportImage source)
    {
        if (!ReferenceEquals(_engine.Frames.Presentation, source.Frame.Presentation)) return;
        var image = ((SkiaOpenGLSurface)_ui.Rendering).CopyTexture(source.Texture.Name, source.Texture.Width, source.Texture.Height);
        try { _view.Canvas.Upload(image, source.Frame, () => ReferenceEquals(_engine.Frames.Presentation, source.Frame.Presentation)); }
        catch { image.Dispose(); throw; }
        _window.RequestFrame();
    }
    internal async Task InitializeAsync(HostedUiWindow window, AetherHost engine, CancellationToken token)
    {
        _window = window; _engine = engine;
        window.PostWindow(host =>
        {
            try
            {
                _owner = Environment.CurrentManagedThreadId; var gl = host.NativeWindow.GLContext!; _current = gl.MakeCurrent; _current();
                nint Resolve(string name) => gl.TryGetProcAddress(name, out var address) && address != 0 ? address : throw new NotSupportedException("Missing OpenGL entry point: " + name);
                _adapter = ReadAdapter(Resolve); _context = new(Resolve, _current, Invoke); _ready.TrySetResult();
            }
            catch (Exception error) { _ready.TrySetException(error); }
        });
        await Ready.WaitAsync(token);
    }
    private Task Invoke(Action action)
    {
        if (Environment.CurrentManagedThreadId == _owner) { _current(); action(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.PostWindow(_ => { try { _current(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } });
        return completion.Task;
    }
    private static unsafe GpuAdapterId ReadAdapter(Func<string, nint> resolve)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException("The current shared GPU viewport requires Windows x64.");
        var integer = (delegate* unmanaged<uint, int*, void>)resolve("glGetIntegerv");
        var bytes = (delegate* unmanaged<uint, byte*, void>)resolve("glGetUnsignedBytevEXT");
        var indexed = (delegate* unmanaged<uint, uint, byte*, void>)resolve("glGetUnsignedBytei_vEXT");
        var error = (delegate* unmanaged<uint>)resolve("glGetError");
        if (error() != 0) throw new InvalidOperationException("OpenGL context has a pending error.");
        int count = 0; integer(0x9596, &count); if (count != 1) throw new NotSupportedException("Viewport requires exactly one OpenGL device UUID.");
        byte* driver = stackalloc byte[16]; byte* device = stackalloc byte[16]; bytes(0x9598, driver); indexed(0x9597, 0, device);
        if (error() != 0) throw new NotSupportedException("OpenGL device UUID query failed.");
        return new(Convert.ToHexString(new ReadOnlySpan<byte>(device, 16)), Convert.ToHexString(new ReadOnlySpan<byte>(driver, 16)));
    }
}
