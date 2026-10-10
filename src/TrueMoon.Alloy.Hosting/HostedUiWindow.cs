using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
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
    /// <summary>Native window capabilities, available after successful StartAsync.</summary>
    public WindowAppearanceCapabilities? AppearanceCapabilities { get; private set; }
    /// <summary>Custom frame adapter capabilities, available after successful startup.</summary>
    public WindowChromeCapabilities? ChromeCapabilities { get; private set; }
    /// <summary>Whether the selected window and graphics surface support per-pixel presentation; available after startup.</summary>
    public bool SupportsPerPixelTransparency { get; private set; }
    /// <summary>Creates the service without allocating a window or GPU resources.</summary>
    public HostedUiWindow(SilkUiWindowOptions options, IUiSessionFactory sessions, Func<Element> root, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(sessions); ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options.Appearance); options.Appearance.Validate();
        options.ValidateChrome();
        _options = new SilkUiWindowOptions { Width = options.Width, Height = options.Height, Title = options.Title,
            OpenGL = options.OpenGL, Appearance = options.Appearance, Chrome = options.Chrome,
            TitleBarFactory = options.TitleBarFactory, ValidationMessage = options.ValidationMessage };
        _sessions = sessions; _root = root; _closed = closed;
    }
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
    public void Close() => _stop = true;
    /// <summary>Queues uniform opacity change for a window created in Opacity mode.</summary>
    /// <param name="opacity">Finite opacity in the inclusive range zero to one.</param>
    public void SetOpacity(float opacity)
    {
        WindowAppearance.ValidateOpacity(opacity);
        if (_options.Appearance.Transparency != WindowTransparencyMode.Opacity)
            throw new InvalidOperationException("Uniform opacity requires Opacity mode.");
        PostWindow(w => w.SetOpacity(opacity));
    }
    private void Run()
    {
        try
        {
            SilkWindowHost? window = null;
            Exception? failure = null;
            try
            {
                window = new SilkWindowHost(_options.Width, _options.Height, _options.Title, _options.OpenGL, _options.Appearance, _options.Chrome);
                AppearanceCapabilities = window.AppearanceCapabilities;
                ChromeCapabilities = window.ChromeCapabilities;
                if (_options.OpenGL) RunOpenGL(window);
                else RunVulkan(window);
            }
            catch (Exception error) { failure = error; }
            UiCleanup.Complete(failure, () => window?.Dispose(), () => _closed?.Invoke());
            _completion.TrySetResult();
        }
        catch (Exception error)
        {
            _ready.TrySetException(error);
            _completion.TrySetException(error);
        }
    }
    private void RunVulkan(SilkWindowHost window)
    {
        VulkanDevice? device = null; VulkanWindowPresenter? presenter = null; UiSession? session = null;
        Exception? failure = null;
        try
        {
            device = new VulkanDevice(window.NativeWindow, _options.ValidationMessage);
            presenter = new VulkanWindowPresenter(device, _options.Appearance.Transparency);
            SupportsPerPixelTransparency = window.AppearanceCapabilities.TransparentFramebuffer && presenter.SupportsPerPixelTransparency;
            session = CreateSession(SilkVulkanHost.CreateTarget(device), window);
            RunSession(window, session, () =>
            {
                if (!presenter.Present(session)) return false;
                PresentedFrames = presenter.PresentedFrames;
                SwapchainGenerations = presenter.SwapchainGenerations;
                return true;
            });
        }
        catch (Exception error) { failure = error; }
        finally
        {
            UiCleanup.Complete(failure, () => session?.Dispose(),
                () =>
                {
                    if (session is { IsDisposed: false }) throw new InvalidOperationException("UI still owns a borrowed/in-flight frame; keep its presenter/device alive.");
                    presenter?.Dispose();
                },
                () =>
                {
                    if (session is { IsDisposed: false } || presenter is { IsDisposed: false }) throw new InvalidOperationException("UI/presenter cleanup is incomplete; keep the Vulkan device alive.");
                    device?.Dispose();
                });
        }
        if (device is { ValidationErrorCount: > 0 } completedDevice)
            throw new InvalidOperationException($"Vulkan validation reported {completedDevice.ValidationErrorCount} errors including disposal.");
    }
    private void RunOpenGL(SilkWindowHost window)
    {
        SupportsPerPixelTransparency = window.AppearanceCapabilities.TransparentFramebuffer;
        var gl = window.NativeWindow.GLContext ?? throw new NotSupportedException("Silk did not create an OpenGL context.");
        var target = new OpenGLUiTarget(name => gl.TryGetProcAddress(name, out var address) ? address : 0, gl.MakeCurrent);
        var session = CreateSession(target, window);
        // Release Skia on Closing; SilkWindowHost keeps the context alive until window disposal.
        Exception? failure = null, closingFailure = null;
        void Closing()
        {
            try { session.Dispose(); }
            catch (Exception error) { closingFailure = error; }
        }
        window.NativeWindow.Closing += Closing;
        try
        {
            RunSession(window, session, () =>
            {
                session.PresentOpenGL(); gl.SwapBuffers(); PresentedFrames++; return true;
            });
        }
        catch (Exception error) { failure = error; }
        finally
        {
            window.NativeWindow.Closing -= Closing;
            UiCleanup.Complete(failure,
                () => { if (closingFailure != null) ExceptionDispatchInfo.Capture(closingFailure).Throw(); },
                session.Dispose);
        }
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
    private void RunSession(SilkWindowHost window, UiSession session, Func<bool> present)
    {
        Exception? failure = null;
        // GLFW may invoke these events from a native refresh/input callback. Never unwind managed exceptions through it.
        void Execute(Action operation)
        {
            if (failure != null) return;
            try
            {
                operation();
                if (session.RenderingFailure is { } graphics) ExceptionDispatchInfo.Capture(graphics).Throw();
            }
            catch (Exception error)
            {
                failure = error; _stop = true;
                if (error is UiRenderingException graphics)
                    try { session.ReportRenderingFailure(graphics); }
                    catch (Exception reportingError) { failure = reportingError; }
            }
        }
        void Input(UiInput input) => Execute(() =>
        {
            SynchronizeViewport();
            session.HandleInput(input);
        });
        void SynchronizeViewport()
        {
            var viewport = window.Viewport;
            if (viewport == session.Viewport) return;
            session.Resize(viewport);
            var updated = session.Update();
            if (window.SupportsCustomFrame && updated)
                window.SetWindowRegions(WindowRegionMap.Create(session.Root));
        }
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
                window.VerifyWindowAccess();
                var viewport = window.Viewport;
                if (viewport != session.Viewport) session.Resize(viewport);
                var updated = session.Update();
                if (window.SupportsCustomFrame && updated)
                    window.SetWindowRegions(WindowRegionMap.Create(session.Root));
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
