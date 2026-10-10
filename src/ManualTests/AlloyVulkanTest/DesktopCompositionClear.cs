using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Silk.NET.Windowing;

namespace AlloyVulkanTest;

internal static partial class DesktopCompositionNative
{
    private sealed class ClientDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly BorrowedWindow _window;
        internal ClientDc(BorrowedWindow window) : base(true)
        {
            _window = window;
            SetHandle(GetClientDC(window));
            if (IsInvalid) throw new InvalidOperationException("Could not acquire client DC.");
        }
        protected override bool ReleaseHandle() => ReleaseDC(_window.DangerousGetHandle(), handle) != 0;
    }

    // Experimental workaround for glfw/glfw#2815. No shared window-class or style changes.
    internal static void ClearRedirection(IWindow window)
    {
        using var hwnd = new BorrowedWindow(window);
        using var dc = new ClientDc(hwnd);
        if (PatBlt(dc, 0, 0, window.Size.X, window.Size.Y, 0x00000042) == 0) // BLACKNESS
            throw new InvalidOperationException("PatBlt(BLACKNESS) failed.");
        if (GdiFlush() == 0) throw new InvalidOperationException("GdiFlush failed.");
        Flush();
    }

    [LibraryImport("user32.dll", EntryPoint = "GetDC")] private static partial nint GetClientDC(BorrowedWindow hwnd);
    [LibraryImport("gdi32.dll")] private static partial int PatBlt(ClientDc dc, int x, int y, int width, int height, uint operation);
    [LibraryImport("gdi32.dll")] private static partial int GdiFlush();
}
