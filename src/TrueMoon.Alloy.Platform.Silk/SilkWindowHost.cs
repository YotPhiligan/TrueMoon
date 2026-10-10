using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Platform.Silk;

/// <summary>A standalone graphics window and normalized input adapter. Create/run/dispose on one thread.</summary>
public sealed class SilkWindowHost : IWindowAppearanceHost, IWindowChromeHost, IWindowSystemMenu, IUiClipboard
{
    private readonly global::Silk.NET.Input.IInputContext _input;
    private readonly Win32Clipboard? _windowsClipboard;
    private readonly Win32TransparentFramebuffer? _transparency;
    private readonly Win32WindowChrome? _chrome;
    private readonly Win32WindowDpi? _dpi;
    /// <summary>Requested custom frame settings, or null for the original GLFW frame behavior.</summary>
    public WindowChromeOptions? Chrome { get; }
    /// <summary>Whether this window has the Windows custom frame adapter installed.</summary>
    public bool SupportsCustomFrame => _chrome != null;
    /// <inheritdoc />
    public WindowChromeCapabilities ChromeCapabilities { get; } = new(false, false, false);
    /// <inheritdoc />
    public UiWindowState State
    {
        get
        {
            VerifyWindowAccess();
            return NativeWindow.WindowState switch
            { WindowState.Minimized => UiWindowState.Minimized, WindowState.Maximized => UiWindowState.Maximized, _ => UiWindowState.Normal };
        }
    }
    private readonly global::Silk.NET.GLFW.Glfw? _glfw;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private bool _disposed;
    private readonly WindowCallbackBoundary _callbacks;
    private readonly Action<double>? _renderHandler;
    private readonly Action<bool>? _focusHandler;
    /// <summary>The native graphics window. Windows client/position coordinates are physical pixels; use Viewport and Resize for UI coordinates.</summary>
    public IWindow NativeWindow { get; }
    /// <inheritdoc />
    public WindowAppearance Appearance { get; }
    /// <inheritdoc />
    public WindowAppearanceCapabilities AppearanceCapabilities { get; } = new(false, false);
    /// <inheritdoc />
    public unsafe float Opacity
    {
        get
        {
            VerifyWindowAccess();
            var value = _glfw?.GetWindowOpacity((global::Silk.NET.GLFW.WindowHandle*)NativeWindow.Native!.Glfw) ?? 1;
            global::Silk.NET.GLFW.Glfw.ThrowExceptions(); return value;
        }
    }
    internal int TransparencyClearCount => _transparency?.ClearCount ?? 0;
    /// <inheritdoc />
    public UiViewport Viewport
    {
        get
        {
            VerifyWindowAccess();
            using var awareness = _dpi != null ? new Win32WindowDpi.AwarenessScope() : default;
            var size = NativeWindow.FramebufferSize;
            var logical = NativeWindow.Size;
            return new UiViewport(Math.Max(0, size.X), Math.Max(0, size.Y),
                _dpi?.PixelScale.Scale ?? (logical.X > 0 && size.X > 0 ? (float)size.X / logical.X : 1));
        }
    }
    /// <inheritdoc />
    public event Action? RenderRequested;
    /// <inheritdoc />
    public event Action<UiInput>? Input;
    /// <summary>Initializes a window without starting its loop or creating a graphics device.</summary>
    public SilkWindowHost(int width = 960, int height = 540, string title = "TrueMoon UI")
        : this(width, height, title, false) { }
    /// <summary>Initializes a Vulkan or desktop OpenGL window without starting its loop.</summary>
    /// <param name="width">Logical width.</param>
    /// <param name="height">Logical height.</param>
    /// <param name="title">Window title.</param>
    /// <param name="openGL">Selects OpenGL instead of Vulkan; the host explicitly swaps buffers.</param>
    public SilkWindowHost(int width, int height, string title, bool openGL)
        : this(width, height, title, openGL, new WindowAppearance()) { }
    /// <summary>Creates a window with explicit desktop composition and decoration settings.</summary>
    /// <param name="width">Logical width.</param>
    /// <param name="height">Logical height.</param>
    /// <param name="title">Window title.</param>
    /// <param name="openGL">Whether to create an OpenGL context.</param>
    /// <param name="appearance">Immutable settings validated before allocating the window.</param>
    public SilkWindowHost(int width, int height, string title, bool openGL, WindowAppearance appearance)
        : this(width, height, title, openGL, appearance, null) { }
    /// <summary>Creates an explicitly configured window and optional Windows custom frame.</summary>
    /// <param name="width">Initial logical client width in 96-DPI units.</param>
    /// <param name="height">Initial logical client height in 96-DPI units.</param>
    /// <param name="title">Native title.</param>
    /// <param name="openGL">Selects OpenGL instead of Vulkan.</param>
    /// <param name="appearance">Immutable composition settings.</param>
    /// <param name="chrome">Custom frame settings; requires Decorated=false and Windows x64.</param>
    public unsafe SilkWindowHost(int width, int height, string title, bool openGL, WindowAppearance appearance, WindowChromeOptions? chrome)
    {
        _callbacks = new WindowCallbackBoundary(VerifyWindowAccess);
        ArgumentNullException.ThrowIfNull(appearance);
        appearance.Validate(); Appearance = appearance;
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (chrome != null)
        {
            chrome.Validate(); chrome.ValidateSize(new Size(width, height));
            if (appearance.Decorated) throw new ArgumentException("Custom chrome requires Decorated=false.", nameof(chrome));
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("Custom chrome currently requires Windows x64.");
            if (chrome.NativeSnapLayouts && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
                throw new PlatformNotSupportedException("Native Snap Layouts require Windows 11.");
        }
        Chrome = chrome;
        var options = openGL ? WindowOptions.Default : WindowOptions.DefaultVulkan;
        NativeWindow = Window.Create(options with
        { Size = new Vector2D<int>(width, height), Title = title, FramesPerSecond = 60, UpdatesPerSecond = 60,
            ShouldSwapAutomatically = false, PreferredStencilBufferBits = 8, IsVisible = false,
            TransparentFramebuffer = appearance.Transparency == WindowTransparencyMode.PerPixel,
            WindowBorder = appearance.Decorated ? WindowBorder.Resizable : WindowBorder.Hidden,
            PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8) });
        try
        {
            using var awareness = OperatingSystem.IsWindows() ? new Win32WindowDpi.AwarenessScope() : default;
            NativeWindow.Initialize();
            if (OperatingSystem.IsWindows() && NativeWindow.Native?.Win32 is { } dpiWindow)
                _dpi = new Win32WindowDpi(dpiWindow.Hwnd, chrome != null);
            if (chrome != null)
            {
                _chrome = new Win32WindowChrome(NativeWindow.Native?.Win32?.Hwnd
                    ?? throw new NotSupportedException("Custom chrome requires a Windows HWND."), chrome);
                ChromeCapabilities = new(true, true, true)
                { SnapLayouts = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000), SystemMenu = chrome.SystemMenu };
            }
            if (NativeWindow.Native?.Glfw is { } glfwHandle)
            {
                _glfw = global::Silk.NET.GLFW.Glfw.GetApi();
                var transparent = _glfw.GetWindowAttrib((global::Silk.NET.GLFW.WindowHandle*)glfwHandle,
                    global::Silk.NET.GLFW.WindowAttributeGetter.TransparentFramebuffer);
                AppearanceCapabilities = new(transparent, OperatingSystem.IsWindows());
                global::Silk.NET.GLFW.Glfw.ThrowExceptions();
            }
            if (appearance.Transparency == WindowTransparencyMode.PerPixel)
            {
                if (!AppearanceCapabilities.TransparentFramebuffer)
                    throw new NotSupportedException("The native window did not provide the requested transparent framebuffer.");
                if (OperatingSystem.IsWindows())
                    _transparency = new Win32TransparentFramebuffer(NativeWindow.Native?.Win32?.Hwnd
                        ?? throw new NotSupportedException("Transparent Windows framebuffer requires HWND."));
            }
            if (appearance.Transparency == WindowTransparencyMode.Opacity) SetOpacity(appearance.Opacity);
            Resize(width, height);
            NativeWindow.IsVisible = true;
            _input = NativeWindow.CreateInput();
            if (OperatingSystem.IsWindows() && NativeWindow.Native?.Win32 is { } win32)
                _windowsClipboard = new Win32Clipboard(win32.Hwnd, Win32ClipboardApi.Instance);
            Action render = () => RenderRequested?.Invoke();
            _renderHandler = _ => _callbacks.Execute(render);
            _focusHandler = focused => _callbacks.Execute(() => { if (!focused) Input?.Invoke(new UiInput(InputKind.FocusLost)); });
            NativeWindow.Render += _renderHandler;
            NativeWindow.FocusChanged += _focusHandler;
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseMove += (_, p) => _callbacks.Execute(() => PointerInput(InputKind.PointerMove, p.X, p.Y));
                mouse.MouseDown += (m, b) => _callbacks.Execute(() => PointerInput(InputKind.PointerDown, m.Position.X, m.Position.Y, (int)b));
                mouse.MouseUp += (m, b) => _callbacks.Execute(() => PointerInput(InputKind.PointerUp, m.Position.X, m.Position.Y, (int)b));
                mouse.Scroll += (m, wheel) => _callbacks.Execute(() => PointerInput(InputKind.Wheel, m.Position.X, m.Position.Y, wheel: wheel.Y));
            }
            foreach (var keyboard in _input.Keyboards)
            {
                keyboard.KeyDown += (k, key, _) => _callbacks.Execute(() => KeyInput(k, key, InputKind.KeyDown));
                keyboard.KeyUp += (k, key, _) => _callbacks.Execute(() => KeyInput(k, key, InputKind.KeyUp));
                keyboard.KeyChar += (_, c) => _callbacks.Execute(() => Input?.Invoke(new UiInput(InputKind.Text, Text: c.ToString())));
            }
        }
        catch (Exception error) { UiCleanup.Complete(error, DetachWindowHandlers, () => _input?.Dispose(), () => _transparency?.Dispose(), () => _chrome?.Dispose(), () => _dpi?.Dispose(), NativeWindow.Dispose, () => _glfw?.Dispose()); throw; }
    }
    /// <summary>Publishes copied title-bar geometry after layout. It is never queried through UI callbacks by native code.</summary>
    /// <param name="regions">Immutable geometry in logical client coordinates.</param>
    public void SetWindowRegions(WindowRegionMap regions)
    {
        VerifyWindowAccess(); ArgumentNullException.ThrowIfNull(regions);
        if (_chrome == null) throw new NotSupportedException("Custom frame was not requested.");
        _chrome.SetRegions(regions, Viewport);
    }
    /// <summary>Dispatches queued custom caption input outside native message callbacks. Direct hosts call this after DoEvents.</summary>
    public void DispatchPendingWindowInput()
    {
        VerifyWindowAccess();
        while (_chrome != null && _chrome.TryDequeueInput(out var input)) Input?.Invoke(input);
    }
    /// <inheritdoc />
    public void ShowSystemMenu(float x, float y)
    {
        VerifyWindowAccess();
        using var awareness = _dpi != null ? new Win32WindowDpi.AwarenessScope() : default;
        if (!float.IsFinite(x) || !float.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        if (_chrome == null) throw new NotSupportedException("The system menu service requires custom Chrome.");
        _chrome.ShowSystemMenu(x, y);
    }
    /// <summary>Requests a client resize, rejecting sizes outside custom frame limits.</summary>
    /// <param name="width">Positive logical width.</param>
    /// <param name="height">Positive logical height.</param>
    public void Resize(int width, int height)
    {
        VerifyWindowAccess();
        using var awareness = _dpi != null ? new Win32WindowDpi.AwarenessScope() : default;
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Chrome?.ValidateSize(new Size(width, height));
        var scale = _dpi?.PixelScale;
        var pixels = new Vector2D<int>(scale?.RoundPixels(width) ?? width, scale?.RoundPixels(height) ?? height);
        if (_chrome != null) _chrome.Resize(pixels.X, pixels.Y);
        else NativeWindow.Size = pixels;
    }
    /// <inheritdoc />
    public void Minimize() { VerifyWindowAccess(); NativeWindow.WindowState = WindowState.Minimized; }
    /// <inheritdoc />
    public void Maximize()
    {
        VerifyWindowAccess();
        if (Chrome is { Resizable: false }) throw new InvalidOperationException("Fixed custom windows cannot maximize.");
        NativeWindow.WindowState = WindowState.Maximized;
    }
    /// <inheritdoc />
    public void Restore() { VerifyWindowAccess(); NativeWindow.WindowState = WindowState.Normal; }
    /// <inheritdoc />
    public unsafe void SetOpacity(float opacity)
    {
        VerifyWindowAccess(); WindowAppearance.ValidateOpacity(opacity);
        if (Appearance.Transparency != WindowTransparencyMode.Opacity)
            throw new InvalidOperationException("Uniform opacity can only change on a window created in Opacity mode.");
        if (_glfw == null || !AppearanceCapabilities.WindowOpacity) throw new NotSupportedException("Native window opacity is unavailable.");
        _glfw.SetWindowOpacity((global::Silk.NET.GLFW.WindowHandle*)NativeWindow.Native!.Glfw, opacity);
        global::Silk.NET.GLFW.Glfw.ThrowExceptions();
        if (Math.Abs(Opacity - opacity) > .005f) throw new NotSupportedException("Native window did not apply the requested opacity.");
    }
    /// <summary>Checks owner-thread access and errors captured by native composition hooks.</summary>
    public void VerifyWindowAccess()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window access requires its owner thread.");
        _callbacks.Verify();
        _transparency?.Verify();
        _chrome?.Verify();
        _dpi?.Verify();
    }
    private void PointerInput(InputKind kind, float x, float y, int button = 0, float wheel = 0)
    {
        // GLFW coordinates are already logical on framebuffer-scaled platforms, but physical on Windows.
        var scale = _dpi?.PixelScale;
        Input?.Invoke(new UiInput(kind, scale?.ToLogical(x) ?? x, scale?.ToLogical(y) ?? y, Button: button, WheelDelta: wheel));
    }
    private void KeyInput(IKeyboard keyboard, Key key, InputKind kind)
    {
        var uiKey = key switch
        {
            Key.Tab => UiKey.Tab, Key.Enter => UiKey.Enter, Key.Space => UiKey.Space, Key.Escape => UiKey.Escape,
            Key.Left => UiKey.Left, Key.Right => UiKey.Right, Key.Home => UiKey.Home, Key.End => UiKey.End,
            Key.Backspace => UiKey.Backspace, Key.Delete => UiKey.Delete, Key.A => UiKey.A, Key.C => UiKey.C,
            Key.V => UiKey.V, Key.X => UiKey.X, _ => UiKey.None
        };
        Input?.Invoke(new UiInput(kind, Key: uiKey,
            Shift: keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight),
            Control: keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight)));
    }
    /// <inheritdoc />
    public string? GetText()
    {
        VerifyClipboardAccess();
        try { return _windowsClipboard != null ? _windowsClipboard.GetText() : _input.Keyboards.FirstOrDefault()?.ClipboardText; }
        catch (Exception error) when (IsClipboardFailure(error))
        { throw new UiClipboardException(UiClipboardOperation.Read, error); }
    }
    /// <inheritdoc />
    public void SetText(string text)
    {
        VerifyClipboardAccess();
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0')) throw new ArgumentException("Clipboard text cannot contain a null character.", nameof(text));
        try
        {
            if (_windowsClipboard != null) _windowsClipboard.SetText(text);
            else if (_input.Keyboards.FirstOrDefault() is { } keyboard) keyboard.ClipboardText = text;
        }
        catch (Exception error) when (IsClipboardFailure(error))
        { throw new UiClipboardException(UiClipboardOperation.Write, error); }
    }
    private void VerifyClipboardAccess()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Clipboard access requires the window owner thread.");
    }
    private static bool IsClipboardFailure(Exception error) => error is System.ComponentModel.Win32Exception or InvalidDataException or global::Silk.NET.GLFW.GlfwException
        || error is AggregateException aggregate && aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsClipboardFailure);
    /// <inheritdoc />
    /// <remarks>The native window and input remain alive after the loop returns, until Dispose on the same thread.</remarks>
    public void Run()
    {
        VerifyWindowAccess();
        using var awareness = _dpi != null ? new Win32WindowDpi.AwarenessScope() : default;
        // Silk's parameterless Run resets the window on return. Input still owns
        // GLFW callbacks, so keep the native handle alive until our Dispose.
        NativeWindow.Run(() =>
        {
            NativeWindow.DoEvents();
            VerifyWindowAccess();
            DispatchPendingWindowInput();
            if (!NativeWindow.IsClosing) NativeWindow.DoUpdate();
            if (!NativeWindow.IsClosing) NativeWindow.DoRender();
        });
        NativeWindow.DoEvents();
        VerifyWindowAccess();
    }
    /// <inheritdoc />
    public void Close() { VerifyWindowAccess(); NativeWindow.Close(); }
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window disposal requires its owner thread.");
        _disposed = true;
        UiCleanup.Complete(null, DetachWindowHandlers, () => _input.Dispose(), () => _transparency?.Dispose(), () => _chrome?.Dispose(), () => _dpi?.Dispose(), NativeWindow.Dispose, () => _glfw?.Dispose());
    }
    private void DetachWindowHandlers()
    {
        // GLFW's platform may retain its last disposed native window. Its managed events must not root this host.
        if (_renderHandler != null) NativeWindow.Render -= _renderHandler;
        if (_focusHandler != null) NativeWindow.FocusChanged -= _focusHandler;
    }
}
