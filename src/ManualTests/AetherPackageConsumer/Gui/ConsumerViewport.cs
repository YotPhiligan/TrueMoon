using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AetherGuiConsumer;

// Consumer composition through public APIs. FrameSession owns each created transport.
// The factory borrows the window/UI/view only until the consumer drains and detaches it.
internal sealed class ConsumerViewport : IFactory<IGameViewportTransport, GpuResourceHost>
{
    private HostedUiWindow? _window;
    private AetherHost? _engine;
    private UiSession? _ui;
    private EditorViewReference? _view;
    private GpuOpenGlContext _context = null!;
    private GpuAdapterId _adapter = null!;
    private int _owner;
    private Action _current = null!;
    internal int Copies { get; private set; }

    public IGameViewportTransport Create(GpuResourceHost arguments)
        => new GpuViewportTransport(arguments, () => (_adapter, _context), Consume, lease => (NativeRuntimeLease)lease);

    internal async Task Initialize(HostedUiWindow window, AetherHost engine, UiSession ui,
        EditorViewReference view, string hostId, CancellationToken token)
    {
        _window = window; _engine = engine; _ui = ui; _view = view;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host =>
        {
            try
            {
                _owner = Environment.CurrentManagedThreadId;
                var gl = host.NativeWindow.GLContext!; _current = gl.MakeCurrent; _current();
                nint Resolve(string name) => gl.TryGetProcAddress(name, out var address) && address != 0
                    ? address : throw new NotSupportedException("Missing OpenGL procedure: " + name);
                _adapter = ReadAdapter(Resolve); _context = new(Resolve, _current, Invoke);
                ready.TrySetResult();
            }
            catch (Exception error) { ready.TrySetException(error); }
        });
        await ready.Task.WaitAsync(token);
    }

    private Task Invoke(Action action)
    {
        if (Environment.CurrentManagedThreadId == _owner) { _current(); action(); return Task.CompletedTask; }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        (_window ?? throw new ObjectDisposedException(nameof(ConsumerViewport))).PostWindow(_ =>
        {
            try { _current(); action(); ready.TrySetResult(); } catch (Exception error) { ready.TrySetException(error); }
        });
        return ready.Task;
    }

    private void Consume(GpuViewportImage source)
    {
        if (_engine == null || _ui == null || _view?.View is not { IsDisposed: false } view ||
            !ReferenceEquals(_engine.Frames.Presentation, source.Frame.Presentation)) return;
        var image = ((SkiaOpenGLSurface)_ui.Rendering).CopyTexture(source.Texture.Name, source.Texture.Width, source.Texture.Height);
        try { view.Canvas.Upload(image, source.Frame, () => ReferenceEquals(_engine.Frames.Presentation, source.Frame.Presentation)); Copies++; }
        catch { image.Dispose(); throw; }
        _window!.RequestFrame();
    }

    internal void Detach() { _view = null; _ui = null; _engine = null; _window = null; }

    // Same GL_EXT_memory_object queries as the standard adapter. Borrowed procedures,
    // fixed-size stack buffers, Windows x64; no transferred handles or callbacks.
    private static unsafe GpuAdapterId ReadAdapter(Func<string, nint> resolve)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException("Windows x64 is required.");
        var integer = (delegate* unmanaged<uint, int*, void>)resolve("glGetIntegerv");
        var bytes = (delegate* unmanaged<uint, byte*, void>)resolve("glGetUnsignedBytevEXT");
        var indexed = (delegate* unmanaged<uint, uint, byte*, void>)resolve("glGetUnsignedBytei_vEXT");
        var error = (delegate* unmanaged<uint>)resolve("glGetError");
        if (error() != 0) throw new InvalidOperationException("OpenGL context has a pending error.");
        int count = 0; integer(0x9596, &count);
        if (count != 1) throw new NotSupportedException("Viewport requires exactly one OpenGL device UUID.");
        byte* driver = stackalloc byte[16]; byte* device = stackalloc byte[16];
        bytes(0x9598, driver); indexed(0x9597, 0, device);
        if (error() != 0) throw new NotSupportedException("OpenGL device UUID query failed.");
        return new(Convert.ToHexString(new ReadOnlySpan<byte>(device, 16)), Convert.ToHexString(new ReadOnlySpan<byte>(driver, 16)));
    }
}

// A mutable reference shared only by this composition; the UI parent owns its current View.
internal sealed class EditorViewReference
{
    internal TrueMoon.Aether.Editor.EditorView? View;
    internal Border? Parent;
}
