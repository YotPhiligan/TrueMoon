using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Alloy.Platform.Silk;

// GLFW otherwise keeps Windows client pixels constant across WM_GETDPISCALEDSIZE.
// Preserve logical client size and let GLFW apply the OS's WM_DPICHANGED rectangle once.
internal sealed partial class Win32WindowDpi : IDisposable
{
    private const nuint SubclassId = 3;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly BorrowedWindow _window;
    private readonly bool _fullClient;
    private GCHandle _root;
    private bool _installed;
    private ExceptionDispatchInfo? _failure;
    internal static int ActiveHooks;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassCallback(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data);
    private static readonly SubclassCallback Callback = Dispatch;
    private static readonly nint CallbackPointer = Marshal.GetFunctionPointerForDelegate(Callback);
    private sealed class BorrowedWindow : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal BorrowedWindow(nint hwnd) : base(false) => SetHandle(hwnd);
        protected override bool ReleaseHandle() => true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { internal int Width, Height; }
    // A window inherits its creating thread's context; never change the host application's process context.
    internal struct AwarenessScope : IDisposable
    {
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private nint _previous;
        public AwarenessScope()
        {
            _previous = SetThreadDpiAwarenessContext(-4); // PER_MONITOR_AWARE_V2
            if (_previous == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        public void Dispose()
        {
            if (_previous == 0) return;
            if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("DPI awareness scope requires its owner thread.");
            if (SetThreadDpiAwarenessContext(_previous) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            _previous = 0;
        }
    }
    internal Win32WindowDpi(nint hwnd, bool fullClient)
    {
        _window = new(hwnd); _fullClient = fullClient;
        if (_window.IsInvalid) throw new ArgumentException("A live HWND is required.", nameof(hwnd));
        _root = GCHandle.Alloc(this);
        try
        {
            _ = PixelScale;
            if (SetWindowSubclass(_window, CallbackPointer, SubclassId, (nuint)GCHandle.ToIntPtr(_root)) == 0)
                throw new InvalidOperationException("Could not install the window DPI hook.");
            _installed = true; Interlocked.Increment(ref ActiveHooks);
        }
        catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
    }
    internal WindowPixelScale PixelScale
    {
        get
        {
            Verify();
            var dpi = GetDpiForWindow(_window);
            if (dpi == 0) throw new InvalidOperationException("Could not query window DPI.");
            return new(dpi);
        }
    }
    internal void Verify()
    {
        ObjectDisposedException.ThrowIf(_window.IsClosed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window DPI requires its owner thread.");
        _failure?.Throw();
    }
    private unsafe void SetScaledSize(uint dpi, nint address)
    {
        if (address == 0) throw new ArgumentException("WM_GETDPISCALEDSIZE requires SIZE.");
        if (GetClientRect(_window, out var client) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var old = PixelScale; var next = new WindowPixelScale(dpi);
        var target = new Rectangle
        {
            Right = next.RoundPixels(old.ToLogical(client.Right - client.Left)),
            Bottom = next.RoundPixels(old.ToLogical(client.Bottom - client.Top))
        };
        if (!_fullClient)
        {
            var style = Style(-16); var extended = Style(-20);
            if (AdjustWindowRectExForDpi(ref target, style, 0, extended, dpi) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        ref var size = ref *(NativeSize*)address;
        size.Width = checked(target.Right - target.Left); size.Height = checked(target.Bottom - target.Top);
    }
    private uint Style(int index)
    {
        Marshal.SetLastPInvokeError(0); var value = GetWindowLongPtrW(_window, index);
        if (value == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return unchecked((uint)value);
    }
    private static nint Dispatch(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data)
    {
        Win32WindowDpi? owner = null;
        try
        {
            owner = (Win32WindowDpi?)GCHandle.FromIntPtr((nint)data).Target;
            if (owner != null)
            {
                if (message == 0x82) owner.Detach();
                else if (owner._failure == null && message == 0x2E4)
                { owner.SetScaledSize((uint)wparam, lparam); return 1; }
            }
        }
        catch (Exception error) { if (owner != null) owner._failure ??= ExceptionDispatchInfo.Capture(error); }
        return DefSubclassProc(hwnd, message, wparam, lparam);
    }
    private void Detach()
    {
        if (_installed)
        {
            if (RemoveWindowSubclass(_window, CallbackPointer, SubclassId) == 0)
                throw new InvalidOperationException("Could not remove the window DPI hook.");
            _installed = false; Interlocked.Decrement(ref ActiveHooks);
        }
        if (_root.IsAllocated) _root.Free();
    }
    public void Dispose()
    {
        if (_window.IsClosed) return;
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window DPI disposal requires its owner thread.");
        Detach(); _window.Dispose();
    }
    [LibraryImport("comctl32.dll")] private static partial int SetWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id, nuint data);
    [LibraryImport("comctl32.dll")] private static partial int RemoveWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id);
    [LibraryImport("comctl32.dll")] private static partial nint DefSubclassProc(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(BorrowedWindow hwnd);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int GetClientRect(BorrowedWindow hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint GetWindowLongPtrW(BorrowedWindow hwnd, int index);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int AdjustWindowRectExForDpi(ref Rectangle rectangle, uint style, int menu, uint extendedStyle, uint dpi);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint SetThreadDpiAwarenessContext(nint context);
}
