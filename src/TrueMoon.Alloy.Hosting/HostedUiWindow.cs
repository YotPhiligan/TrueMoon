using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Owns a standalone window and graphics resources on the shared Windows UI owner thread.</summary>
public sealed class HostedUiWindow : IStartable, IStoppable, IAsyncDisposable
{
    private readonly SilkUiWindowOptions _options;
    private readonly IUiSessionFactory _sessions;
    private readonly Func<Element> _root;
    private readonly Action? _closed;
    private readonly HostedUiDispatcher _dispatcher;
    private readonly object _gate = new();
    private SilkWindowHost? _window;
    private VulkanDevice? _device;
    private VulkanWindowPresenter? _presenter;
    private UiSession? _session;
    private Exception? _failure;
    private bool _rendering;
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
    /// <summary>Native window capabilities, available after successful StartAsync.</summary>
    public WindowAppearanceCapabilities? AppearanceCapabilities { get; private set; }
    /// <summary>Custom frame adapter capabilities, available after successful startup.</summary>
    public WindowChromeCapabilities? ChromeCapabilities { get; private set; }
    /// <summary>Whether the selected window and graphics surface support per-pixel presentation; available after startup.</summary>
    public bool SupportsPerPixelTransparency { get; private set; }
    /// <summary>Creates a window in the process default scope without allocating native resources.</summary>
    /// <param name="options">Window configuration copied before startup.</param>
    /// <param name="sessions">Factory owning the registered UI sessions.</param>
    /// <param name="root">Creates the root on the shared owner thread.</param>
    /// <param name="closed">Called on the owner thread after accepted-window cleanup.</param>
    public HostedUiWindow(SilkUiWindowOptions options, IUiSessionFactory sessions, Func<Element> root, Action? closed = null)
        : this(options, sessions, root, closed, null) { }
    /// <summary>Creates the service without allocating a window or GPU resources.</summary>
    /// <param name="options">Window configuration copied before startup.</param>
    /// <param name="sessions">Factory owning the registered UI sessions.</param>
    /// <param name="root">Creates the root on the shared owner thread.</param>
    /// <param name="closed">Called once on the owner thread after accepted-window cleanup.</param>
    /// <param name="dispatcher">Application scope; null uses the process default scope.</param>
    public HostedUiWindow(SilkUiWindowOptions options, IUiSessionFactory sessions, Func<Element> root, Action? closed, HostedUiDispatcher? dispatcher)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(sessions); ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options.Appearance); options.Appearance.Validate();
        options.ValidateChrome();
        _options = new SilkUiWindowOptions { Width = options.Width, Height = options.Height, Title = options.Title,
            OpenGL = options.OpenGL, Appearance = options.Appearance, Chrome = options.Chrome,
            TitleBarFactory = options.TitleBarFactory, ValidationMessage = options.ValidationMessage };
        _sessions = sessions; _root = root; _closed = closed; _dispatcher = dispatcher ?? HostedUiDispatcher.Shared;
    }
    /// <inheritdoc />
    /// <remarks>Cancellation cancels only the startup wait. Call Close or StopAsync to close an accepted window.</remarks>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schedule = false;
        lock (_gate)
        {
            if (_started == 0)
            {
                _started = 1;
                schedule = !_stop;
            }
        }
        if (schedule)
        {
            try { _dispatcher.Start(this, Initialize, Tick, Cleanup, () => _stop, _ready, _completion); }
            catch (Exception error) { _ready.TrySetException(error); _completion.TrySetException(error); }
        }
        return _ready.Task.WaitAsync(cancellationToken);
    }
    /// <summary>Queues a native window operation for its owner thread.</summary>
    public void PostWindow(Action<SilkWindowHost> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            if (_stop || Completion.IsCompleted) throw new ObjectDisposedException(nameof(HostedUiWindow));
            _commands.Enqueue(operation);
        }
        _dispatcher.Wake();
    }
    /// <summary>Requests a logical window resize.</summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        _options.Chrome?.ValidateSize(new Size(width, height)); PostWindow(w => w.Resize(width, height));
    }
    /// <summary>Queues minimize on the owner thread.</summary>
    public void Minimize() => PostWindow(w => w.Minimize());
    /// <summary>Queues maximize on the owner thread.</summary>
    public void Maximize()
    {
        if (_options.Chrome is { Resizable: false }) throw new InvalidOperationException("Fixed custom windows cannot maximize.");
        PostWindow(w => w.Maximize());
    }
    /// <summary>Queues restore on the owner thread.</summary>
    public void Restore() => PostWindow(w => w.Restore());
    /// <summary>Queues the native system menu at a logical client point.</summary>
    /// <param name="x">Horizontal logical client coordinate.</param>
    /// <param name="y">Vertical logical client coordinate.</param>
    public void ShowSystemMenu(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        if (_options.Chrome is not { SystemMenu: true }) throw new NotSupportedException("Enable custom Chrome.SystemMenu first.");
        PostWindow(w => w.ShowSystemMenu(x, y));
    }
    /// <summary>Requests the window to close on its owner thread.</summary>
    public void Close()
    {
        lock (_gate)
        {
            _stop = true;
            if (_started == 0)
            {
                _commands.Clear(); _ready.TrySetCanceled(); _completion.TrySetResult();
            }
        }
        _dispatcher.Wake();
    }
    /// <summary>Queues uniform opacity change for a window created in Opacity mode.</summary>
    /// <param name="opacity">Finite opacity in the inclusive range zero to one.</param>
    public void SetOpacity(float opacity)
    {
        WindowAppearance.ValidateOpacity(opacity);
        if (_options.Appearance.Transparency != WindowTransparencyMode.Opacity)
            throw new InvalidOperationException("Uniform opacity requires Opacity mode.");
        PostWindow(w => w.SetOpacity(opacity));
    }
    private void Initialize()
    {
        _window = new SilkWindowHost(_options.Width, _options.Height, _options.Title, _options.OpenGL, _options.Appearance, _options.Chrome);
        AppearanceCapabilities = _window.AppearanceCapabilities;
        ChromeCapabilities = _window.ChromeCapabilities;
        if (_options.OpenGL)
        {
            var gl = _window.NativeWindow.GLContext ?? throw new NotSupportedException("Silk did not create an OpenGL context.");
            gl.MakeCurrent();
            SupportsPerPixelTransparency = _window.AppearanceCapabilities.TransparentFramebuffer;
            _session = CreateSession(new OpenGLUiTarget(name => gl.TryGetProcAddress(name, out var address) ? address : 0, gl.MakeCurrent), _window);
        }
        else
        {
            _device = new VulkanDevice(_window.NativeWindow, _options.ValidationMessage);
            _presenter = new VulkanWindowPresenter(_device, _options.Appearance.Transparency);
            SupportsPerPixelTransparency = _window.AppearanceCapabilities.TransparentFramebuffer && _presenter.SupportsPerPixelTransparency;
            _session = CreateSession(SilkVulkanHost.CreateTarget(_device), _window);
        }
        _window.Input += Input;
        _window.RenderRequested += Render;
    }
    private bool Tick()
    {
        try
        {
            MakeCurrent();
            // Finite command batch: a producer cannot starve sibling windows.
            for (var i = 0; i < 64 && !_stop && _commands.TryDequeue(out var command); i++)
            {
                // A preceding command may have dispatched a sibling native callback.
                MakeCurrent(); command(_window!);
            }
            ThrowFailure();
            if (_stop) return true;
            var closing = _window!.Pump();
            ThrowFailure();
            return closing || _stop;
        }
        catch
        {
            ThrowFailure(); // A previously captured callback cause has precedence.
            throw;
        }
    }
    private void MakeCurrent()
    {
        if (_options.OpenGL) _window?.NativeWindow.GLContext?.MakeCurrent();
    }
    private void ThrowFailure()
    {
        if (_failure != null) ExceptionDispatchInfo.Capture(_failure).Throw();
    }
    private void Cleanup()
    {
        lock (_gate) { _stop = true; _commands.Clear(); }
        var window = _window; var session = _session; var presenter = _presenter; var device = _device;
        try
        {
            UiCleanup.Complete(null,
                () => { if (window != null) { window.Input -= Input; window.RenderRequested -= Render; } },
                () => { MakeCurrent(); if (window is { NativeWindow.IsClosing: false }) window.NativeWindow.Close(); },
                () => { MakeCurrent(); session?.Dispose(); },
                () =>
                {
                    if (session is { IsDisposed: false }) throw new InvalidOperationException("UI still owns a borrowed/in-flight frame; keep its presenter/device alive.");
                    presenter?.Dispose();
                },
                () =>
                {
                    if (session is { IsDisposed: false } || presenter is { IsDisposed: false }) throw new InvalidOperationException("UI/presenter cleanup is incomplete; keep the Vulkan device alive.");
                    device?.Dispose();
                },
                () =>
                {
                    if (device is { ValidationErrorCount: > 0 }) throw new InvalidOperationException($"Vulkan validation reported {device.ValidationErrorCount} errors including disposal.");
                },
                () => window?.Dispose(), () => _closed?.Invoke());
        }
        finally { _window = null; _session = null; _presenter = null; _device = null; }
    }
    private UiSession CreateSession(UiRenderTarget target, SilkWindowHost window)
    {
        var root = _root();
        Element? title = null;
        try
        {
            if (_options.TitleBarFactory != null)
            {
                title = _options.TitleBarFactory(window) ?? throw new InvalidOperationException("TitleBarFactory returned null.");
                root = new WindowFrame(title, root);
            }
            return _sessions.Create(root, target, window.Viewport, window);
        }
        catch (Exception error) { UiCleanup.Complete(error, root.Dispose,
            () => { if (title is { Parent: null, IsAttached: false, IsDisposed: false }) title.Dispose(); }); throw; }
    }
    private void Execute(Action operation)
    {
        if (_failure != null || _stop) return;
        try
        {
            // DoEvents may dispatch a sibling callback while a different GL context is current.
            MakeCurrent();
            operation();
            if (_session?.RenderingFailure is { } graphics) ExceptionDispatchInfo.Capture(graphics).Throw();
        }
        catch (Exception error)
        {
            _failure = error;
            if (error is UiRenderingException graphics)
            {
                try { _session?.ReportRenderingFailure(graphics); }
                catch (Exception reportingError) { _failure = reportingError; }
            }
        }
    }
    private void SynchronizeViewport()
    {
        var window = _window!; var session = _session!;
        var viewport = window.Viewport;
        if (viewport != session.Viewport) session.Resize(viewport);
        var updated = session.Update();
        if (window.SupportsCustomFrame && updated)
            window.SetWindowRegions(WindowRegionMap.Create(session.Root));
    }
    private void Input(UiInput input) => Execute(() =>
    {
        SynchronizeViewport();
        _session!.HandleInput(input);
    });
    private void Render()
    {
        if (_rendering) return;
        _rendering = true;
        try { Execute(PresentFrame); }
        finally { _rendering = false; }
    }
    private void PresentFrame()
    {
        var window = _window!; var session = _session!;
        if (session.IsDisposed) return;
        window.VerifyWindowAccess();
        SynchronizeViewport();
        if (session.Viewport.IsEmpty) return;
        if (_options.OpenGL)
        {
            session.PresentOpenGL();
            window.NativeWindow.GLContext!.SwapBuffers();
            PresentedFrames++;
        }
        else
        {
            if (!_presenter!.Present(session)) return;
            PresentedFrames = _presenter.PresentedFrames;
            SwapchainGenerations = _presenter.SwapchainGenerations;
        }
        FramePresented?.Invoke(session);
    }
    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Close();
        if (_dispatcher.IsOwnerThread && Volatile.Read(ref _started) != 0 && !Completion.IsCompleted)
            throw new InvalidOperationException("Await window shutdown outside the UI owner thread; use Close in callbacks.");
        if (Volatile.Read(ref _started) != 0) await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(StopAsync());
}
