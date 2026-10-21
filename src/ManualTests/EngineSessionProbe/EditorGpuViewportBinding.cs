using System.Runtime.InteropServices;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

namespace EngineSessionProbe;

// Application composition only: Aether owns transport/imports, the Canvas owns each retained Skia image.
internal sealed class EditorGpuViewportBinding(Func<IRuntimeLease, NativeRuntimeLease> native)
{
    private HostedUiWindow _window = null!;
    private AetherHost _host = null!;
    private UiSession _ui = null!;
    private GpuOpenGlContext _context = null!;
    private GpuAdapterId _adapter = null!;
    private int _thread;
    private Action _current = null!;
    private static nint _readPixels;
    private static int _importReadbacks;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Ready => _ready.Task;
    internal int Copies { get; private set; }
    internal GpuAdapterId Adapter => _adapter;
    internal int ImportReadbacks => Volatile.Read(ref _importReadbacks);
    internal IGameViewportTransport CreateTransport(GpuResourceHost gpu) => new GpuViewportTransport(gpu, () => (_adapter, _context), Consume, native);
    internal void Observe(UiSession ui) => _ui = ui;
    private void Consume(GpuViewportImage source)
    {
        if (!ReferenceEquals(_host.Frames.Presentation, source.Frame.Presentation)) return;
        var image = ((SkiaOpenGLSurface)_ui.Rendering).CopyTexture(source.Texture.Name, source.Texture.Width, source.Texture.Height);
        try { ((ViewportView)_ui.Root).Canvas.UploadGpu(image, source.Frame, () => ReferenceEquals(_host.Frames.Presentation, source.Frame.Presentation)); Copies++; }
        catch { image.Dispose(); throw; }
        _window.RequestFrame();
    }
    internal async Task InitializeAsync(HostedUiWindow window, AetherHost host, CancellationToken token)
    {
        _window = window; _host = host; _importReadbacks = 0;
        var ready = _ready;
        window.PostWindow(w =>
        {
            try
            {
                _thread = Environment.CurrentManagedThreadId; var gl = w.NativeWindow.GLContext!; _current = gl.MakeCurrent; _current();
                nint Resolve(string name) { var address = gl.TryGetProcAddress(name, out var pointer) ? pointer : 0; return name == "glReadPixels" ? TraceReadPixels(address) : address; }
                _context = new(Resolve, _current, Owner);
                var profile = GpuCapabilityCollector.OpenGl(new(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.GameId, new('A', 64)), Resolve).Capabilities;
                Probe.Check(profile.Error == null && profile.Devices.Count == 1 && profile.Features is { RequiredProcedures: true, OptimalTiling: true }, "Editor GPU profile unavailable.");
                _adapter = profile.Devices[0]; ready.TrySetResult();
            }
            catch (Exception error) { ready.TrySetException(error); }
        });
        await ready.Task.WaitAsync(token);
    }
    private Task Owner(Action action)
    {
        if (Environment.CurrentManagedThreadId == _thread) { _current(); action(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.PostWindow(_ => { try { _current(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } }); return completion.Task;
    }
    private static unsafe nint TraceReadPixels(nint original) { _readPixels = original; return (nint)(delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)&ReadPixels; }
    [UnmanagedCallersOnly]
    private static unsafe void ReadPixels(int x, int y, int w, int h, uint format, uint type, void* pixels)
    { Interlocked.Increment(ref _importReadbacks); ((delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)_readPixels)(x, y, w, h, format, type, pixels); }
}
