using System.Runtime.InteropServices;
using SkiaSharp;

namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>Loads one extended native Skia for SkiaSharp and Vulkan interop before their first native calls.</summary>
public static class SkiaNativeLibrary
{
    private static readonly object Gate = new();
    private static string? _path;
    private static nint _library;
    /// <summary>The configured native DLL path, or null before initialization.</summary>
    public static string? LoadedPath => _path;
    /// <summary>Loads the bundled Windows x64 DLL or an explicit path. The library remains loaded until process exit.</summary>
    public static void Initialize(string? path = null)
    {
        lock (Gate) { if (path == null && _path != null) return; }
        if (path == null && (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64))
            throw new PlatformNotSupportedException("Bundled Skia Vulkan interop currently supports Windows x64.");
        path = Path.GetFullPath(path ?? Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll"));
        lock (Gate)
        {
            if (_path != null)
            {
                if (!string.Equals(_path, path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("A different native Skia library is already configured for this process.");
                return;
            }
            if (!File.Exists(path)) throw new FileNotFoundException("Extended native Skia library is missing.", path);
            _library = NativeLibrary.Load(path);
            DllImportResolver resolver = (name, _, _) => name == "libSkiaSharp" ? _library : 0;
            NativeLibrary.SetDllImportResolver(typeof(SKSurface).Assembly, resolver);
            NativeLibrary.SetDllImportResolver(typeof(VulkanInteropNative).Assembly, resolver);
            _path = path;
        }
    }
    /// <summary>Checks official managed/native compatibility and the interop ABI.</summary>
    public static void VerifyVulkanInterop() { if (_path == null) Initialize(); VulkanInteropNative.VerifyAbi(); }
}
