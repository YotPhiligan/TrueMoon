using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Alloy.Platform.Silk;

// Initializes the DWM redirection surface before GLFW's synchronous damage callback
// can present a frame. Applies only to explicitly transparent windows.
internal sealed partial class Win32TransparentFramebuffer : IDisposable
{
    private const uint Paint = 0x000F, Size = 0x0005, ShowWindow = 0x0018, NcDestroy = 0x0082,
        DpiChanged = 0x02E0, CompositionChanged = 0x031E;
    private const nuint SubclassId = 1;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassCallback(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data);
    private static readonly SubclassCallback Callback = Dispatch;
    private static readonly nint CallbackPointer = Marshal.GetFunctionPointerForDelegate(Callback);
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly BorrowedWindow _window;
    private GCHandle _root;
    private bool _installed;
    private ExceptionDispatchInfo? _failure;
    internal static int ActiveHooks;
    internal int ClearCount { get; private set; }

    private sealed class BorrowedWindow : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal BorrowedWindow(nint hwnd) : base(false) { SetHandle(hwnd); }
        protected override bool ReleaseHandle() => true;
    }
    private sealed class ClientDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly BorrowedWindow _window;
        internal ClientDc(BorrowedWindow window) : base(true)
        {
            _window = window; SetHandle(GetDC(window));
            if (IsInvalid) throw new InvalidOperationException("Could not acquire transparent window client DC.");
        }
        protected override bool ReleaseHandle() => ReleaseDC(_window.DangerousGetHandle(), handle) != 0;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { internal int Left, Top, Right, Bottom; }

    internal Win32TransparentFramebuffer(nint hwnd)
    {
        _window = new BorrowedWindow(hwnd);
        if (_window.IsInvalid) throw new ArgumentException("A live HWND is required.", nameof(hwnd));
        _root = GCHandle.Alloc(this); // Native callbacks retain their target until removal/WM_NCDESTROY.
        try
        {
            if (SetWindowSubclass(_window, CallbackPointer, SubclassId, (nuint)GCHandle.ToIntPtr(_root)) == 0)
                throw new InvalidOperationException("Could not install transparent framebuffer window hook.");
            _installed = true; Interlocked.Increment(ref ActiveHooks);
            Clear();
        }
        catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
    }
    private void Clear()
    {
        if (GetClientRect(_window, out var rectangle) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var width = rectangle.Right - rectangle.Left; var height = rectangle.Bottom - rectangle.Top;
        if (width <= 0 || height <= 0) return;
        using var dc = new ClientDc(_window);
        if (PatBlt(dc, 0, 0, width, height, 0x00000042) == 0 || GdiFlush() == 0)
            throw new InvalidOperationException("Could not initialize transparent window redirection surface.");
        ClearCount++;
    }
    private static nint Dispatch(nint hwnd, uint message, nuint wparam, nint lparam, nuint id, nuint data)
    {
        Win32TransparentFramebuffer? owner = null;
        try
        {
            owner = (Win32TransparentFramebuffer?)GCHandle.FromIntPtr((nint)data).Target;
            if (owner != null)
            {
                if (message == NcDestroy) owner.Detach();
                else if (owner._failure == null && (message is Paint or Size or DpiChanged or CompositionChanged || message == ShowWindow && wparam != 0))
                    owner.Clear();
            }
        }
        catch (Exception error) { if (owner != null) owner._failure ??= ExceptionDispatchInfo.Capture(error); }
        // Preserve GLFW paint, resize, DPI and input handling; never replace its WndProc.
        return DefSubclassProc(hwnd, message, wparam, lparam);
    }
    internal void Verify()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window transparency requires the owner thread.");
        _failure?.Throw(); // Report managed errors after returning from native message dispatch.
    }
    private void Detach()
    {
        if (_installed)
        {
            if (RemoveWindowSubclass(_window, CallbackPointer, SubclassId) == 0)
                throw new InvalidOperationException("Could not remove transparent framebuffer window hook.");
            _installed = false; Interlocked.Decrement(ref ActiveHooks);
        }
        if (_root.IsAllocated) _root.Free();
    }
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Window transparency disposal requires the owner thread.");
        Detach(); _window.Dispose();
    }

    [LibraryImport("comctl32.dll")] private static partial int SetWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id, nuint data);
    [LibraryImport("comctl32.dll")] private static partial int RemoveWindowSubclass(BorrowedWindow hwnd, nint callback, nuint id);
    [LibraryImport("comctl32.dll")] private static partial nint DefSubclassProc(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int GetClientRect(BorrowedWindow hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial nint GetDC(BorrowedWindow hwnd);
    [LibraryImport("user32.dll")] private static partial int ReleaseDC(nint hwnd, nint dc);
    [LibraryImport("gdi32.dll")] private static partial int PatBlt(ClientDc dc, int x, int y, int width, int height, uint operation);
    [LibraryImport("gdi32.dll")] private static partial int GdiFlush();
}
