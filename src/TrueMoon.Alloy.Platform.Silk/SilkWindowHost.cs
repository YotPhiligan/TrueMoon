using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Platform.Silk;

/// <summary>A standalone graphics window and normalized input adapter. Create/run/dispose on one thread.</summary>
public sealed class SilkWindowHost : IWindowHost, IUiClipboard
{
    private readonly global::Silk.NET.Input.IInputContext _input;
    private bool _disposed;
    /// <summary>The native window used by the selected graphics integration.</summary>
    public IWindow NativeWindow { get; }
    /// <inheritdoc />
    public UiViewport Viewport
    {
        get
        {
            var size = NativeWindow.FramebufferSize;
            var logical = NativeWindow.Size;
            return new UiViewport(Math.Max(0, size.X), Math.Max(0, size.Y),
                logical.X > 0 && size.X > 0 ? (float)size.X / logical.X : 1);
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
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var options = openGL ? WindowOptions.Default : WindowOptions.DefaultVulkan;
        NativeWindow = Window.Create(options with
        { Size = new Vector2D<int>(width, height), Title = title, FramesPerSecond = 60, UpdatesPerSecond = 60,
            ShouldSwapAutomatically = false, PreferredStencilBufferBits = 8,
            PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8) });
        try
        {
            NativeWindow.Initialize();
            _input = NativeWindow.CreateInput();
            NativeWindow.Render += _ => RenderRequested?.Invoke();
            NativeWindow.FocusChanged += focused => { if (!focused) Input?.Invoke(new UiInput(InputKind.FocusLost)); };
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseMove += (_, p) => Input?.Invoke(new UiInput(InputKind.PointerMove, p.X, p.Y));
                mouse.MouseDown += (m, b) => Input?.Invoke(new UiInput(InputKind.PointerDown, m.Position.X, m.Position.Y, Button: (int)b));
                mouse.MouseUp += (m, b) => Input?.Invoke(new UiInput(InputKind.PointerUp, m.Position.X, m.Position.Y, Button: (int)b));
                mouse.Scroll += (m, wheel) => Input?.Invoke(new UiInput(InputKind.Wheel, m.Position.X, m.Position.Y, WheelDelta: wheel.Y));
            }
            foreach (var keyboard in _input.Keyboards)
            {
                keyboard.KeyDown += (k, key, _) => KeyInput(k, key, InputKind.KeyDown);
                keyboard.KeyUp += (k, key, _) => KeyInput(k, key, InputKind.KeyUp);
                keyboard.KeyChar += (_, c) => Input?.Invoke(new UiInput(InputKind.Text, Text: c.ToString()));
            }
        }
        catch { _input?.Dispose(); NativeWindow.Dispose(); throw; }
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
    public string? GetText() => _input.Keyboards.FirstOrDefault()?.ClipboardText;
    /// <inheritdoc />
    public void SetText(string text) { if (_input.Keyboards.FirstOrDefault() is { } keyboard) keyboard.ClipboardText = text; }
    /// <inheritdoc />
    /// <remarks>The native window and input remain alive after the loop returns, until Dispose on the same thread.</remarks>
    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Silk's parameterless Run resets the window on return. Input still owns
        // GLFW callbacks, so keep the native handle alive until our Dispose.
        NativeWindow.Run(() =>
        {
            NativeWindow.DoEvents();
            if (!NativeWindow.IsClosing) NativeWindow.DoUpdate();
            if (!NativeWindow.IsClosing) NativeWindow.DoRender();
        });
        NativeWindow.DoEvents();
    }
    /// <inheritdoc />
    public void Close() => NativeWindow.Close();
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _input.Dispose(); }
        finally { NativeWindow.Dispose(); }
    }
}
