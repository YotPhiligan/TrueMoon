using System.ComponentModel;
using System.Runtime.InteropServices;
using TrueMoon.Alloy;

namespace AlloyVulkanTest;

/// <summary>Windows-only observations and a legacy WGL control independent of GLFW/Skia.</summary>
internal static unsafe partial class NativeWindowDiagnostics
{
    internal sealed record WindowInfo(string Handle, uint Thread, string Class, bool MessageOnly);
    internal static uint ThreadId => GetCurrentThreadId();
    internal static object GraphicsInfo() => new
    {
        Vendor = Marshal.PtrToStringUTF8(glGetString(0x1F00)),
        Renderer = Marshal.PtrToStringUTF8(glGetString(0x1F01)),
        Version = Marshal.PtrToStringUTF8(glGetString(0x1F02))
    };

    internal static List<WindowInfo> OwnWindows()
    {
        var result = new List<WindowInfo>();
        var seen = new HashSet<nint>();
        Span<char> buffer = stackalloc char[256];
        // This is an HWND census, not an enumeration of all USER objects (DCs, cursors, menus...).
        foreach (var parent in new nint[] { 0, -3 })
        {
            nint previous = 0;
            while ((previous = FindWindowExW(parent, previous, 0, 0)) != 0)
            {
                var thread = GetWindowThreadProcessId(previous, out var process);
                if (process != (uint)Environment.ProcessId || !seen.Add(previous)) continue;
                int count;
                fixed (char* pointer = buffer) count = GetClassNameW(previous, pointer, buffer.Length);
                if (count == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
                result.Add(new WindowInfo($"0x{previous:X}", thread, new string(buffer[..count]), parent == -3));
            }
        }
        return result;
    }

    internal static void RunRaw(bool openGL, Action<object?> initialized)
    {
        nint window = 0, dc = 0, context = 0;
        Exception? failure = null;
        try
        {
            // Predefined STATIC uses the OS window procedure; no managed callback or GLFW initialization.
            window = CreateWindowExW(0, "STATIC", "Raw WGL resource control", 0x80000000,
                0, 0, 320, 200, 0, 0, 0, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (openGL)
            {
                dc = GetDC(window);
                if (dc == 0) throw new InvalidOperationException("GetDC failed.");
                var format = new PixelFormatDescriptor
                {
                    Size = (ushort)sizeof(PixelFormatDescriptor),
                    Version = 1,
                    Flags = 0x4 | 0x20 | 0x1,
                    ColorBits = 24,
                    AlphaBits = 8,
                    DepthBits = 24,
                    StencilBits = 8
                };
                if (format.Size != 40) throw new InvalidOperationException("Incorrect PIXELFORMATDESCRIPTOR layout.");
                var selected = ChoosePixelFormat(dc, in format);
                if (selected == 0 || SetPixelFormat(dc, selected, in format) == 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                context = wglCreateContext(dc);
                if (context == 0 || wglMakeCurrent(dc, context) == 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                initialized(GraphicsInfo());
                for (var frame = 0; frame < 3; frame++)
                    if (SwapBuffers(dc) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            else initialized(null);
        }
        catch (Exception error) { failure = error; }
        UiCleanup.Complete(failure,
            () => { if (context != 0 && wglMakeCurrent(0, 0) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError()); },
            () => { if (context != 0 && wglDeleteContext(context) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError()); },
            () => { if (dc != 0 && ReleaseDC(window, dc) == 0) throw new InvalidOperationException("ReleaseDC failed."); },
            () => { if (window != 0 && DestroyWindow(window) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError()); });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
    {
        public ushort Size, Version;
        public uint Flags;
        public byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift;
        public byte AlphaBits, AlphaShift, AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits;
        public byte DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
        public uint LayerMask, VisibleMask, DamageMask;
    }

    // HWNDs observed by the census are borrowed. RunRaw owns its HWND/HDC/HGLRC on the calling thread.
    [LibraryImport("kernel32.dll")] private static partial uint GetCurrentThreadId();
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW")] private static partial nint FindWindowExW(nint parent, nint after, nint windowClass, nint title);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(nint window, out uint process);
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)] private static partial int GetClassNameW(nint window, char* buffer, int count);
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(uint extendedStyle, string windowClass, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [LibraryImport("user32.dll")] private static partial nint GetDC(nint window);
    [LibraryImport("user32.dll")] private static partial int ReleaseDC(nint window, nint dc);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int DestroyWindow(nint window);
    [LibraryImport("gdi32.dll", SetLastError = true)] private static partial int ChoosePixelFormat(nint dc, in PixelFormatDescriptor format);
    [LibraryImport("gdi32.dll", SetLastError = true)] private static partial int SetPixelFormat(nint dc, int selected, in PixelFormatDescriptor format);
    [LibraryImport("gdi32.dll", SetLastError = true)] private static partial int SwapBuffers(nint dc);
    [LibraryImport("opengl32.dll", SetLastError = true)] private static partial nint wglCreateContext(nint dc);
    [LibraryImport("opengl32.dll", SetLastError = true)] private static partial int wglMakeCurrent(nint dc, nint context);
    [LibraryImport("opengl32.dll", SetLastError = true)] private static partial int wglDeleteContext(nint context);
    [LibraryImport("opengl32.dll")] private static partial nint glGetString(uint name);
}
