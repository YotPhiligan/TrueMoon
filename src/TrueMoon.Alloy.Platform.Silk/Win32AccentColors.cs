using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Platform.Silk;

// Short-lived WinRT activation, owned entirely by the reading window thread.
// No RCW, persistent COM subscription, registry access or Windows-specific target framework.
internal static partial class Win32AccentColors
{
    internal static Color? TryRead()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10) || !Environment.Is64BitProcess) return null;
        try { return Read(); }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
        catch (BadImageFormatException) { return null; }
    }

    private static Color? Read()
    {
        var initialized = RoInitialize(1); // RO_INIT_MULTITHREADED
        if (initialized == unchecked((int)0x80010106)) initialized = RoInitialize(0); // Existing STA: RPC_E_CHANGED_MODE
        if (initialized < 0) return null;
        try
        {
            const string runtimeClass = "Windows.UI.ViewManagement.UISettings";
            var result = WindowsCreateString(runtimeClass, (uint)runtimeClass.Length, out var text);
            using var name = new HString(text);
            if (result < 0) return null;
            result = RoActivateInstance(name, out var instancePointer);
            using var instance = new ComReference(instancePointer);
            if (result < 0 || instance.IsInvalid) return null;
            var id = new Guid("03021be4-5254-4781-8194-5168f7d06d7b");
            result = instance.QueryInterface(in id, out var settingsPointer);
            using var settings = new ComReference(settingsPointer);
            if (result < 0 || settings.IsInvalid) return null;
            result = settings.ReadAccent(out var color);
            return result < 0 ? null : new Color(color.R, color.G, color.B, color.A);
        }
        finally { RoUninitialize(); } // Balance S_OK or S_FALSE exactly once; never a failed initialization.
    }

    // Windows SDK 10.0.26100.0 windows.ui.viewmanagement.h:
    // IUISettings3 : IInspectable; first method is GetColorValue(UIColorType, Color*).
    // IUnknown slots0..2, IInspectable slots3..5; UIColorType_Accent=5.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeColor { internal byte A, R, G, B; }
    private sealed class ComReference : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal ComReference(nint pointer) : base(true) => SetHandle(pointer);
        internal int QueryInterface(in Guid id, out nint result)
        {
            var added = false;
            try { DangerousAddRef(ref added); return Marshal.QueryInterface(DangerousGetHandle(), in id, out result); }
            finally { if (added) DangerousRelease(); }
        }
        internal unsafe int ReadAccent(out NativeColor color)
        {
            var added = false;
            try
            {
                DangerousAddRef(ref added);
                var pointer = (void*)DangerousGetHandle();
                var method = (delegate* unmanaged[Stdcall]<void*, int, NativeColor*, int>)(*(void***)pointer)[6];
                var value = default(NativeColor);
                var result = method(pointer, 5, &value);
                color = value; return result;
            }
            finally { if (added) DangerousRelease(); }
        }
        protected override bool ReleaseHandle() { Marshal.Release(handle); return true; }
    }
    private sealed class HString : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal HString(nint value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() => WindowsDeleteString(handle) >= 0;
    }

    [LibraryImport("combase.dll")] private static partial int RoInitialize(uint type);
    [LibraryImport("combase.dll")] private static partial void RoUninitialize();
    [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int WindowsCreateString(string value, uint length, out nint result);
    [LibraryImport("combase.dll")] private static partial int WindowsDeleteString(nint value);
    [LibraryImport("combase.dll")] private static partial int RoActivateInstance(HString name, out nint instance);
}
