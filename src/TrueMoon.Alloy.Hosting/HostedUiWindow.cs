using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Silk.NET.Maths;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Owns a standalone window and its graphics resources on a dedicated UI thread.</summary>
public sealed class HostedUiWindow : IStartable, IStoppable, IAsyncDisposable
{
    private readonly SilkUiWindowOptions _options;
    private readonly IUiSessionFactory _sessions;
    private readonly Func<Element> _root;
    private readonly Action? _closed;
    private readonly ConcurrentQueue<Action<SilkWindowHost>> _commands = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;
    private volatile bool _stop;
    /// <summary>Completes when the window loop and resource disposal finish.</summary>
    public Task Completion => _completion.Task;
    /// <summary>Runs on the UI thread after presentation; handlers may update the session.</summary>
    public event Action<UiSession>? FramePresented;
    /// <summary>Number of successfully presented frames.</summary>
    public int PresentedFrames { get; private set; }
    /// <summary>Number of created swapchain generations.</summary>
    public int SwapchainGenerations { get; private set; }
    /// <summary>Creates the service without allocating a window or GPU resources.</summary>
    public HostedUiWindow(SilkUiWindowOptions options, IUiSessionFactory sessions, Func<Element> root, Action? closed = null)
    { _options = options; _sessions = sessions; _root = root; _closed = closed; }
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) == 0)
            new Thread(Run) { IsBackground = true, Name = "TrueMoon Alloy UI" }.Start();
        return _ready.Task.WaitAsync(cancellationToken);
    }
    /// <summary>Queues a native window operation for its owner thread.</summary>
    public void PostWindow(Action<SilkWindowHost> operation)
    { ArgumentNullException.ThrowIfNull(operation); if (Completion.IsCompleted) throw new ObjectDisposedException(nameof(HostedUiWindow)); _commands.Enqueue(operation); }
    /// <summary>Requests a logical window resize.</summary>
    public void Resize(int width, int height)
    { if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width)); PostWindow(w => w.NativeWindow.Size = new Vector2D<int>(width, height)); }
    /// <summary>Requests the window to close on its owner thread.</summary>
    public void Close() => _stop = true;
    private void Run()
    {
        try
        {
            using (var window = new SilkWindowHost(_options.Width, _options.Height, _options.Title, _options.OpenGL))
            {
                if (_options.OpenGL) RunOpenGL(window);
                else RunVulkan(window);
            }
            _closed?.Invoke();
            _completion.TrySetResult();
        }
        catch (Exception error)
        {
            _ready.TrySetException(error);
            try { _closed?.Invoke(); }
            catch (Exception closeError) { error = new AggregateException(error, closeError); }
            _completion.TrySetException(error);
        }
    }
    private void RunVulkan(SilkWindowHost window)
    {
        var device = new VulkanDevice(window.NativeWindow, _options.ValidationMessage);
        using (device)
        using (var presenter = new VulkanWindowPresenter(device))
        using (var session = _sessions.Create(_root(), SilkVulkanHost.CreateTarget(device), window.Viewport, window))
        {
            RunSession(window, session, () =>
            {
                if (!presenter.Present(session)) return false;
                PresentedFrames = presenter.PresentedFrames;
                SwapchainGenerations = presenter.SwapchainGenerations;
                return true;
            });
        }
        if (device.ValidationErrorCount != 0)
            throw new InvalidOperationException($"Vulkan validation reported {device.ValidationErrorCount} errors including disposal.");
    }
    private void RunOpenGL(SilkWindowHost window)
    {
        var gl = window.NativeWindow.GLContext ?? throw new NotSupportedException("Silk did not create an OpenGL context.");
        var target = new OpenGLUiTarget(name => gl.TryGetProcAddress(name, out var address) ? address : 0, gl.MakeCurrent);
        using var session = _sessions.Create(_root(), target, window.Viewport, window);
        // Release Skia on Closing; SilkWindowHost keeps the context alive until window disposal.
        window.NativeWindow.Closing += session.Dispose;
        try
        {
            RunSession(window, session, () =>
            {
                session.PresentOpenGL(); gl.SwapBuffers(); PresentedFrames++; return true;
            });
        }
        finally { window.NativeWindow.Closing -= session.Dispose; }
    }
    private void RunSession(SilkWindowHost window, UiSession session, Func<bool> present)
    {
        Exception? failure = null;
        // GLFW may invoke these events from a native refresh/input callback. Never unwind managed exceptions through it.
        void Execute(Action operation)
        {
            if (failure != null) return;
            try { operation(); }
            catch (Exception error) { failure = error; _stop = true; }
        }
        void Input(UiInput input) => Execute(() => session.HandleInput(input));
        void Update(double delta)
        {
            Execute(() => { while (_commands.TryDequeue(out var command)) command(window); });
            if (_stop) window.Close();
        }
        void Render()
        {
            Execute(() =>
            {
                if (session.IsDisposed) return;
                var viewport = window.Viewport;
                if (viewport != session.Viewport) session.Resize(viewport);
                session.Update();
                if (!viewport.IsEmpty && present()) FramePresented?.Invoke(session);
            });
        }
        window.Input += Input; window.NativeWindow.Update += Update; window.RenderRequested += Render;
        try { _ready.TrySetResult(); window.Run(); }
        finally { window.Input -= Input; window.NativeWindow.Update -= Update; window.RenderRequested -= Render; }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    { _stop = true; if (Volatile.Read(ref _started) != 0) await Completion.WaitAsync(cancellationToken).ConfigureAwait(false); }
    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(StopAsync());
}
