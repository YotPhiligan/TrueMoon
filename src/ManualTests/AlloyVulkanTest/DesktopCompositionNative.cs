using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Silk.NET.Windowing;

namespace AlloyVulkanTest;

// Assertion-only desktop RGB sampling. Owns a screen DC, borrows HWND until the live Silk window is disposed.
internal static partial class DesktopCompositionNative
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    private sealed class BorrowedWindow : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal BorrowedWindow(IWindow window) : base(false)
        { SetHandle(window.Native?.Win32?.Hwnd ?? throw new NotSupportedException("Win32 HWND required.")); }
        protected override bool ReleaseHandle() => true; // Silk owns HWND; never DestroyWindow.
    }
    private sealed class ScreenDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal ScreenDc() : base(true)
        {
            SetHandle(GetDC(0));
            if (IsInvalid) throw new InvalidOperationException("Could not acquire desktop DC.");
        }
        protected override bool ReleaseHandle() => ReleaseDC(0, handle) != 0;
    }
    internal static WindowTransparencyProbe.Rgb Pixel(IWindow window, int x, int y)
    {
        if (Marshal.SizeOf<Point>() != 8) throw new InvalidOperationException("POINT ABI mismatch.");
        using var hwnd = new BorrowedWindow(window);
        var point = new Point { X = x, Y = y };
        if (ClientToScreen(hwnd, ref point) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        using var dc = new ScreenDc();
        var color = GetPixel(dc, point.X, point.Y);
        if (color == uint.MaxValue) throw new InvalidOperationException($"Desktop GetPixel unavailable: screen=({point.X},{point.Y}), state={window.WindowState}, visible={window.IsVisible}, position={window.Position}, size={window.Size}.");
        return new WindowTransparencyProbe.Rgb((int)(color & 255), (int)((color >> 8) & 255), (int)((color >> 16) & 255));
    }
    internal static void Flush() => Marshal.ThrowExceptionForHR(DwmFlush());
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        internal uint Size; internal int Width, Height; internal ushort Planes, Bits;
        internal uint Compression, ImageSize; internal int XResolution, YResolution;
        internal uint ColorsUsed, ColorsImportant, Color;
    }
    private sealed class MemoryDc : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal MemoryDc(ScreenDc screen) : base(true) { SetHandle(CreateCompatibleDC(screen)); if (IsInvalid) throw new InvalidOperationException("Memory DC unavailable."); }
        protected override bool ReleaseHandle() => DeleteDC(handle) != 0;
    }
    private sealed class CaptureBitmap : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal CaptureBitmap(ScreenDc screen, int width, int height) : base(true)
        { SetHandle(CreateCompatibleBitmap(screen, width, height)); if (IsInvalid) throw new InvalidOperationException("Capture bitmap unavailable."); }
        protected override bool ReleaseHandle() => DeleteObject(handle) != 0;
    }
    internal static unsafe void Capture(int x, int y, int width, int height, string path)
    {
        using var dc = new ScreenDc();
        using var memory = new MemoryDc(dc);
        using var nativeBitmap = new CaptureBitmap(dc, width, height);
        var previous = SelectObject(memory, nativeBitmap.DangerousGetHandle());
        if (previous == 0 || previous == -1) throw new InvalidOperationException("Select capture bitmap failed.");
        try
        {
            if (BitBlt(memory, 0, 0, width, height, dc, x, y, 0x40CC0020) == 0) throw new InvalidOperationException("Desktop capture unavailable.");
        }
        finally { SelectObject(memory, previous); }
        using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque);
        var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, Bits = 32 };
        if (Marshal.SizeOf<BitmapInfo>() != 44 || bitmap.RowBytes != width * 4) throw new InvalidOperationException("Capture bitmap ABI mismatch.");
        var pixels = (uint*)bitmap.GetPixels();
        if (GetDIBits(dc, nativeBitmap, 0, (uint)height, pixels, ref info, 0) != height) throw new InvalidOperationException("Capture readback failed.");
        for (var i = 0; i < width * height; i++) pixels[i] |= 0xff000000;
        using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path); data.SaveTo(file);
    }
    internal static void Repaint(IWindow window)
    {
        using var hwnd = new BorrowedWindow(window);
        if (RedrawWindow(hwnd, 0, 0, 0x0105) == 0) throw new InvalidOperationException("RedrawWindow failed.");
    }
    internal static bool ClientHitTargetsWindow(IWindow window)
        => ClientHitTargetsWindow(window, window);
    internal static bool ClientHitTargetsWindow(IWindow window, IWindow target)
    {
        using var hwnd = new BorrowedWindow(window);
        using var targetHwnd = new BorrowedWindow(target);
        for (var band = 0; band < 3; band++)
        {
            var point = new Point { X = window.Size.X * (band * 2 + 1) / 6, Y = window.Size.Y / 2 };
            if (ClientToScreen(hwnd, ref point) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (WindowFromPoint(point) != targetHwnd.DangerousGetHandle()) return false;
        }
        return true;
    }
    internal static bool IsDecorated(IWindow window)
    {
        using var hwnd = new BorrowedWindow(window);
        // GLFW uses WS_CAPTION for decorated windows and removes it for borderless ones.
        Marshal.SetLastPInvokeError(0);
        var style = GetWindowLongPtrW(hwnd, -16); // GWL_STYLE
        if (style == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        return (style & 0x00C00000) == 0x00C00000;
    }
    internal static bool HasFullClientArea(IWindow window)
    {
        using var hwnd = new BorrowedWindow(window);
        var origin = new Point();
        if (GetWindowRect(hwnd, out var outer) == 0 || GetClientRect(hwnd, out var client) == 0 || ClientToScreen(hwnd, ref origin) == 0)
            throw new InvalidOperationException("Native frame geometry unavailable.");
        return origin.X == outer.Left && origin.Y == outer.Top && client.Right == outer.Right - outer.Left && client.Bottom == outer.Bottom - outer.Top;
    }

    [LibraryImport("user32.dll")] private static partial nint GetDC(nint hwnd);
    [LibraryImport("user32.dll")] private static partial int ReleaseDC(nint hwnd, nint dc);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int ClientToScreen(BorrowedWindow hwnd, ref Point point);
    [LibraryImport("gdi32.dll")] private static partial uint GetPixel(ScreenDc dc, int x, int y);
    [LibraryImport("gdi32.dll")] private static partial nint CreateCompatibleDC(ScreenDc dc);
    [LibraryImport("gdi32.dll")] private static partial int DeleteDC(nint dc);
    [LibraryImport("gdi32.dll")] private static partial nint CreateCompatibleBitmap(ScreenDc dc, int width, int height);
    [LibraryImport("gdi32.dll")] private static partial int DeleteObject(nint bitmap);
    [LibraryImport("gdi32.dll")] private static partial nint SelectObject(MemoryDc dc, nint obj);
    [LibraryImport("gdi32.dll")] private static partial int BitBlt(MemoryDc destination, int x, int y, int width, int height, ScreenDc source, int sx, int sy, uint operation);
    [LibraryImport("gdi32.dll")] private static unsafe partial int GetDIBits(ScreenDc dc, CaptureBitmap bitmap, uint start, uint count, void* pixels, ref BitmapInfo info, uint usage);
    [LibraryImport("dwmapi.dll")] private static partial int DwmFlush();
    [LibraryImport("user32.dll")] private static partial int RedrawWindow(BorrowedWindow hwnd, nint rectangle, nint region, uint flags);
    [LibraryImport("user32.dll")] private static partial nint WindowFromPoint(Point point);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint GetWindowLongPtrW(BorrowedWindow hwnd, int index);
    [LibraryImport("user32.dll")] private static partial int GetWindowRect(BorrowedWindow hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial int GetClientRect(BorrowedWindow hwnd, out Rectangle rectangle);
}
