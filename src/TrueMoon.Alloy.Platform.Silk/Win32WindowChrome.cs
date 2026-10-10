using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Platform.Silk;

// Borrowed HWND; geometry snapshots never retain UI elements or call user code from WndProc.
internal sealed partial class Win32WindowChrome : IDisposable
{
    private const uint NcCalcSize = 0x83, NcHitTest = 0x84, GetMinMaxInfo = 0x24, NcDestroy = 0x82;
    private const nuint SubclassId = 2;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassCallback(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data);
    private static readonly SubclassCallback Callback = Dispatch;
    private static readonly nint CallbackPointer = Marshal.GetFunctionPointerForDelegate(Callback);
    private readonly BorrowedWindow _window;
    private readonly WindowChromeOptions _options;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private GCHandle _root;
    private bool _installed;
    private ExceptionDispatchInfo? _failure;
    private WindowRegionMap _regions = WindowRegionMap.Empty;
    private float _scale = 1;
    private int _width, _height;
    private readonly Queue<UiInput> _input = new();
    private bool _maxPressed;
    internal static int ActiveHooks;
    private sealed class BorrowedWindow : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal BorrowedWindow(nint hwnd) : base(false) => SetHandle(hwnd);
        protected override bool ReleaseHandle() => true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
    { internal Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { internal uint Size; internal Rectangle Monitor, Work; internal uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct TrackMouse
    { internal uint Size, Flags; internal nint Window; internal uint HoverTime; }
    private sealed class BorrowedMenu : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal BorrowedMenu(nint menu) : base(false) => SetHandle(menu);
        protected override bool ReleaseHandle() => true; // GetSystemMenu is owned by the HWND.
    }
    internal Win32WindowChrome(nint hwnd, WindowChromeOptions options)
    {
        _options = options; _window = new(hwnd);
        if (_window.IsInvalid) throw new ArgumentException("A live HWND is required.", nameof(hwnd));
        _root = GCHandle.Alloc(this);
        try
        {
            if (SetWindowSubclass(_window, CallbackPointer, SubclassId, (nuint)GCHandle.ToIntPtr(_root)) == 0)
                throw new InvalidOperationException("Could not install custom window frame hook.");
            _installed = true; Interlocked.Increment(ref ActiveHooks);
            Marshal.SetLastPInvokeError(0);
            var style = GetWindowLongPtrW(_window, -16);
            if (style == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            // Keep native commands and resize semantics while WM_NCCALCSIZE removes their visible frame.
            // GLFW's undecorated WS_POPUP interferes with Shell dragging/Snap. Use overlapped semantics,
            // without WS_CAPTION: custom HTCAPTION supplies dragging and maximize fills the work area.
            style = (style | 0x80000 | 0x20000) & ~unchecked((nint)0x80C00000u);
            style = options.Resizable ? style | 0x40000 | 0x10000 : style & ~(0x40000 | 0x10000);
            Marshal.SetLastPInvokeError(0);
            if (SetWindowLongPtrW(_window, -16, style) == 0 && Marshal.GetLastPInvokeError() != 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (SetWindowPos(_window, 0, 0, 0, 0, 0, 0x37) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            Verify();
        }
        catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
    }
    internal void SetRegions(WindowRegionMap regions, UiViewport viewport)
    {
        Verify(); _regions = regions; _scale = viewport.Scale; _width = viewport.Width; _height = viewport.Height;
    }
    internal void Resize(int width, int height)
    {
        Verify();
        // GLFW AdjustWindowRect assumes its standard frame. Our whole HWND is client area.
        if (SetWindowPos(_window, 0, 0, 0, width, height, 0x16) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        Verify();
    }
    internal void Verify()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Custom frame requires its owner thread.");
        _failure?.Throw();
    }
    internal bool TryDequeueInput(out UiInput input) => _input.TryDequeue(out input);
    private static nint Dispatch(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data)
    {
        Win32WindowChrome? owner = null;
        try
        {
            owner = (Win32WindowChrome?)GCHandle.FromIntPtr((nint)data).Target;
            if (owner != null)
            {
                if (message == NcDestroy) owner.Detach();
                else if (owner._failure == null)
                {
                    if (message == NcCalcSize && wparam != 0) return 0; // Full-client layout; retain standard non-layout frame metrics.
                    if (message == 0x2E0) owner.InvalidateRegions();
                    if (message == NcHitTest) return owner.HitTest(lparam);
                    // Handle the chord before GLFW's disabled keyboard-menu / WM_SYSCHAR path.
                    // Bit 29 distinguishes Alt+Space; bit 30 suppresses held-key repeats.
                    if (message == 0x104 && wparam == 32 && ((long)lparam & 0x20000000) != 0)
                    {
                        if (owner._options.SystemMenu && ((long)lparam & 0x40000000) == 0) owner.ShowSystemMenu(0, 32);
                        return 0;
                    }
                    if (message == 0x112 && ((uint)wparam & 0xfff0) == 0xf100 && lparam == 32)
                    {
                        if (owner._options.SystemMenu) owner.ShowSystemMenu(0, 32);
                        return 0; // GLFW disables its keyboard menu by default.
                    }
                    if (message == 0xA5 && wparam == 2) // WM_NCRBUTTONUP / HTCAPTION
                    {
                        if (owner._options.SystemMenu) owner.ShowMenu(PackedPoint(lparam));
                        return 0;
                    }
                    if (owner.CaptionInput(message, wparam, lparam, out var result)) return result;
                    if (message == GetMinMaxInfo)
                    {
                        var defaultResult = DefSubclassProc(hwnd, message, wparam, lparam);
                        owner.SetLimits(lparam); return defaultResult;
                    }
                }
            }
        }
        catch (Exception error) { if (owner != null) owner._failure ??= ExceptionDispatchInfo.Capture(error); }
        return DefSubclassProc(hwnd, message, wparam, lparam);
    }
    private nint HitTest(nint packedPoint)
    {
        // GET_X/Y_LPARAM sign extension is required on monitors with negative coordinates.
        var point = PackedPoint(packedPoint);
        if (ScreenToClient(_window, ref point) == 0 || GetClientRect(_window, out var rectangle) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        var width = rectangle.Right; var height = rectangle.Bottom;
        if (point.X < 0 || point.Y < 0 || point.X >= width || point.Y >= height) return 0;
        if (_options.Resizable && IsZoomed(_window) == 0)
        {
            var dpi = GetDpiForWindow(_window);
            if (dpi == 0) throw new InvalidOperationException("Could not query custom frame DPI.");
            var edge = Math.Min(Math.Min(width, height) / 2, (int)Math.Ceiling(_options.ResizeBorder * dpi / 96));
            var left = point.X < edge; var right = point.X >= width - edge;
            var top = point.Y < edge; var bottom = point.Y >= height - edge;
            if (top) return left ? 13 : right ? 14 : 12;
            if (bottom) return left ? 16 : right ? 17 : 15;
            if (left) return 10; if (right) return 11;
        }
        // An old layout must not expose stale caption regions during resize.
        if (width != _width || height != _height || CurrentScale().Scale != _scale) return 1;
        var role = _regions.HitTest(point.X / _scale, point.Y / _scale);
        if (_options.NativeSnapLayouts && role == WindowRegionRole.Maximize) return 9;
        return _options.NativeDrag && role == WindowRegionRole.Caption ? 2 : 1;
    }
    private static Point PackedPoint(nint packed) => new()
    { X = (short)((long)packed & 0xffff), Y = (short)(((long)packed >> 16) & 0xffff) };
    private void QueueInput(InputKind kind, Point point, bool screen)
    {
        if (screen && ScreenToClient(_window, ref point) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var scale = CurrentScale();
        _input.Enqueue(new UiInput(kind, scale.ToLogical(point.X), scale.ToLogical(point.Y)));
    }
    private bool CaptionInput(uint message, nuint wparam, nint lparam, out nint result)
    {
        result = 0;
        if (message is 0x1F or 0x215 || message == 0x8 && _maxPressed) // cancel mode/capture changed/kill focus
        {
            if (_maxPressed)
            {
                _maxPressed = false; _input.Clear(); _input.Enqueue(new UiInput(InputKind.FocusLost));
                if (GetCapture() == _window.DangerousGetHandle()) ReleaseCapture();
            }
            return false;
        }
        if (!_options.NativeSnapLayouts) return false;
        // DWM needs non-client hover/leave to show and dismiss its own Snap UI.
        if (message is 0xA0 or 0x2A2)
        {
            if (message == 0xA0 && wparam == 9)
            {
                QueueInput(InputKind.PointerMove, PackedPoint(lparam), true);
                var track = new TrackMouse { Size = (uint)Marshal.SizeOf<TrackMouse>(), Flags = 0x12, Window = _window.DangerousGetHandle() };
                if (TrackMouseEvent(ref track) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            else if (!_maxPressed) _input.Enqueue(new UiInput(InputKind.PointerMove, -1, -1));
            return DwmDefWindowProc(_window, message, wparam, lparam, out result) != 0;
        }
        if (message == 0xA1 && wparam == 9) // WM_NCLBUTTONDOWN / HTMAXBUTTON
        {
            _maxPressed = true; SetCapture(_window);
            QueueInput(InputKind.PointerDown, PackedPoint(lparam), true);
            return true; // Click belongs to Argentis; don't also execute native maximize.
        }
        if (_maxPressed && message is 0x200 or 0x202) // captured client move/up
        {
            QueueInput(message == 0x202 ? InputKind.PointerUp : InputKind.PointerMove, PackedPoint(lparam), false);
            if (message == 0x202)
            { _maxPressed = false; if (GetCapture() == _window.DangerousGetHandle()) ReleaseCapture(); }
            return true;
        }
        return false;
    }
    internal void ShowSystemMenu(float x, float y)
    {
        Verify();
        if (!_options.SystemMenu) throw new NotSupportedException("System menu is disabled for this custom frame.");
        var scale = CurrentScale().Scale;
        var point = new Point { X = checked((int)Math.Round(x * scale)), Y = checked((int)Math.Round(y * scale)) };
        if (ClientToScreen(_window, ref point) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        ShowMenu(point); Verify();
    }
    private void ShowMenu(Point point)
    {
        using var menu = new BorrowedMenu(GetSystemMenu(_window, 0));
        if (menu.IsInvalid) throw new InvalidOperationException("Native system menu is unavailable.");
        var zoomed = IsZoomed(_window) != 0; var iconic = IsIconic(_window) != 0;
        EnableCommand(menu, 0xf120, zoomed || iconic); // Restore
        EnableCommand(menu, 0xf010, !zoomed && !iconic); // Move
        EnableCommand(menu, 0xf000, _options.Resizable && !zoomed && !iconic); // Size
        EnableCommand(menu, 0xf020, !iconic); // Minimize
        EnableCommand(menu, 0xf030, _options.Resizable && !zoomed); // Maximize
        var command = TrackPopupMenuEx(menu, 0x182, point.X, point.Y, _window, 0);
        if (command != 0 && PostMessageW(_window, 0x112, command, 0) == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    private static void EnableCommand(BorrowedMenu menu, uint command, bool enabled)
    {
        if (EnableMenuItem(menu, command, enabled ? 0u : 1u) == uint.MaxValue)
            throw new InvalidOperationException("Could not update native system menu state.");
    }
    private unsafe void SetLimits(nint address)
    {
        var monitor = MonitorFromWindow(_window, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || GetMonitorInfoW(monitor, ref info) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        ref var limits = ref *(MinMaxInfo*)address;
        // Position relative to the monitor, not the primary desktop. Work excludes the taskbar.
        limits.MaxPosition = new Point { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
        limits.MaxSize = new Point { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
        limits.MinTrackSize = new Point { X = Pixels(_options.MinimumSize.Width, true), Y = Pixels(_options.MinimumSize.Height, true) };
        if (float.IsFinite(_options.MaximumSize.Width)) limits.MaxTrackSize.X = Pixels(_options.MaximumSize.Width, false);
        if (float.IsFinite(_options.MaximumSize.Height)) limits.MaxTrackSize.Y = Pixels(_options.MaximumSize.Height, false);
        limits.MaxSize.X = Math.Min(limits.MaxSize.X, limits.MaxTrackSize.X);
        limits.MaxSize.Y = Math.Min(limits.MaxSize.Y, limits.MaxTrackSize.Y);
    }
    private int Pixels(float value, bool minimum)
    {
        var scale = CurrentScale();
        return Math.Max(1, minimum ? scale.MinimumPixels(value) : scale.MaximumPixels(value));
    }
    private WindowPixelScale CurrentScale()
    {
        var dpi = GetDpiForWindow(_window);
        if (dpi == 0) throw new InvalidOperationException("Could not query custom frame DPI.");
        return new(dpi);
    }
    private void InvalidateRegions()
    {
        _regions = WindowRegionMap.Empty; _width = _height = 0;
        _input.Clear();
        if (_maxPressed)
        {
            _maxPressed = false; _input.Enqueue(new UiInput(InputKind.FocusLost));
            if (GetCapture() == _window.DangerousGetHandle()) ReleaseCapture();
        }
    }
    private void Detach()
    {
        if (_installed)
        {
            if (RemoveWindowSubclass(_window, CallbackPointer, SubclassId) == 0)
                throw new InvalidOperationException("Could not remove custom window frame hook.");
            _installed = false; Interlocked.Decrement(ref ActiveHooks);
        }
        _regions = WindowRegionMap.Empty;
        _input.Clear(); _maxPressed = false;
        if (GetCapture() == _window.DangerousGetHandle()) ReleaseCapture();
        if (_root.IsAllocated) _root.Free();
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Custom frame disposal requires its owner thread.");
        Detach(); _window.Dispose();
    }
    [LibraryImport("comctl32.dll")] private static partial int SetWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id, nuint data);
    [LibraryImport("comctl32.dll")] private static partial int RemoveWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id);
    [LibraryImport("comctl32.dll")] private static partial nint DefSubclassProc(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint GetWindowLongPtrW(BorrowedWindow hwnd, int index);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint SetWindowLongPtrW(BorrowedWindow hwnd, int index, nint value);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int SetWindowPos(BorrowedWindow hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int ScreenToClient(BorrowedWindow hwnd, ref Point point);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int GetClientRect(BorrowedWindow hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial int IsZoomed(BorrowedWindow hwnd);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(BorrowedWindow hwnd);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(BorrowedWindow hwnd, uint flags);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [LibraryImport("dwmapi.dll")] private static partial int DwmDefWindowProc(BorrowedWindow hwnd, uint message, nuint wparam, nint lparam, out nint result);
    [LibraryImport("user32.dll")] private static partial nint SetCapture(BorrowedWindow hwnd);
    [LibraryImport("user32.dll")] private static partial nint GetCapture();
    [LibraryImport("user32.dll")] private static partial int ReleaseCapture();
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int TrackMouseEvent(ref TrackMouse track);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int ClientToScreen(BorrowedWindow hwnd, ref Point point);
    [LibraryImport("user32.dll")] private static partial nint GetSystemMenu(BorrowedWindow hwnd, int revert);
    [LibraryImport("user32.dll")] private static partial uint EnableMenuItem(BorrowedMenu menu, uint item, uint flags);
    [LibraryImport("user32.dll")] private static partial uint TrackPopupMenuEx(BorrowedMenu menu, uint flags, int x, int y, BorrowedWindow hwnd, nint parameters);
    [LibraryImport("user32.dll")] private static partial int IsIconic(BorrowedWindow hwnd);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int PostMessageW(BorrowedWindow hwnd, uint message, nuint wparam, nint lparam);
}
