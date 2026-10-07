using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Silk.NET.Vulkan;
using SkiaSharp;


namespace TrueMoon.Alloy.Rendering.Skia;

// Experimental integration: device/context are borrowed; this object owns its surface.
// All operations, including host queue access, must run serially on the creating thread.
/// <summary>A Skia-owned UI surface on borrowed host device/context; operations are sequential and thread-affine.</summary>
public sealed class VulkanInteropSurface : IDisposable
{
    private readonly VulkanHostContext _device;
    private readonly GRContext _context;
    private readonly SKSurface _surface;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private VulkanTextureLease? _lease;
    private bool _drawing;
    private bool _disposed;


    /// <summary>Creates an offscreen surface on raw host handles; context and host remain borrowed.</summary>
    /// <param name="device">Host descriptor which outlives this surface.</param>
    /// <param name="context">Vulkan Skia context created on the same device and graphics queue.</param>
    /// <param name="size">Pixel dimensions of the UI surface.</param>
    public VulkanInteropSurface(VulkanHostContext device, GRContext context, SKSizeI size)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(context);
        VulkanInteropNative.VerifyAbi();
        _device = device;
        _context = context;
        _surface = SKSurface.Create(context, false,
            new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul),
            0, GRSurfaceOrigin.TopLeft)
            ?? throw new NotSupportedException("Could not create a Vulkan interop surface.");
    }

    /// <summary>Draws while no texture lease is outstanding.</summary>
    public void Draw(Action<SKCanvas> draw)
    {
        VerifyAvailable();
        _drawing = true;
        try { draw(_surface.Canvas); }
        finally { _drawing = false; }
    }

    /// <summary>Submits Skia work and borrows its Vulkan image until explicit Return.</summary>
    public VulkanTextureLease Acquire()
    {
        VerifyAvailable();
        // GetBackendTexture flushes the surface. Submit and wait only after that flush.
        var handle = VulkanInteropNative.Acquire(_surface.Handle, out var info);
        GC.KeepAlive(_surface);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new NotSupportedException("Native extension could not export a single-level RGBA8888 Vulkan surface.");
        }
        try
        {
            _context.Submit(true);
            // Submit's managed API returns void; explicitly check completion on the device too.
            _device.WaitIdle();
            if (info.QueueFamily != Vk.QueueFamilyIgnored && info.QueueFamily != _device.QueueFamily)
                throw new NotSupportedException("The sequential probe requires the host graphics queue family.");
            _lease = new VulkanTextureLease(this, handle, info);
            return _lease;
        }
        catch { handle.Dispose(); throw; }
    }

    private void VerifyThread()
    {
        if (_thread != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Vulkan interop must run on its owner thread.");
    }

    /// <summary>Checks lifetime, owner thread and outstanding drawing/texture access.</summary>
    public void VerifyAvailable()
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_lease != null || _drawing)
            throw new InvalidOperationException("The surface is currently borrowed or being drawn.");
    }

    internal void Return(VulkanTextureLease lease, ImageLayout layout)
    {
        VerifyThread();
        if (_lease != lease) throw new InvalidOperationException("This texture lease is not active.");
        if (layout is ImageLayout.Undefined or ImageLayout.Preinitialized)
            throw new ArgumentOutOfRangeException(nameof(layout), "Returning an image must preserve its contents.");
        // Same-device/same-queue sequential contract. Host has already recorded its barriers.
        _device.WaitIdle();
        if (VulkanInteropNative.Return(lease.Handle, (uint)layout, _device.QueueFamily) != 1)
            throw new InvalidOperationException("Could not apply the host's Vulkan image state to Skia.");
        lease.Handle.Dispose();
        _lease = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        VerifyThread();
        if (_disposed) return;
        if (_lease != null || _drawing)
            throw new InvalidOperationException("Return the host texture before destroying the UI surface.");
        _device.WaitIdle();
        _surface.Dispose();
        _disposed = true;
    }
}

// No implicit Dispose/return: the host must supply the actual layout it left behind.
/// <summary>Borrowed Vulkan texture. Return explicitly with the layout left by completed host GPU work.</summary>
public sealed class VulkanTextureLease
{
    private readonly VulkanInteropSurface _owner;
    private readonly VulkanTextureInfo _info;
    internal VulkanTextureHandle Handle { get; }
    internal VulkanTextureLease(VulkanInteropSurface owner, VulkanTextureHandle handle, VulkanTextureInfo info)
    { _owner = owner; Handle = handle; _info = info; }
    /// <summary>The borrowed image descriptor, valid until Return.</summary>
    public VulkanTextureInfo Info => !Handle.IsClosed ? _info : throw new ObjectDisposedException(nameof(VulkanTextureLease));
    /// <summary>Waits for completed host work and returns its actual state to Skia.</summary>
    public void Return(ImageLayout finalLayout) => _owner.Return(this, finalLayout);
}
/// <summary>ABI v1 Vulkan image descriptor; no ownership of the image is transferred.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct VulkanTextureInfo
{
    /// <summary>Vulkan descriptor field Image.</summary>
    public ulong Image;
    /// <summary>Vulkan descriptor field Width.</summary>
    public uint Width;
    /// <summary>Vulkan descriptor field Height.</summary>
    public uint Height;
    /// <summary>Vulkan descriptor field Format.</summary>
    public uint Format;
    /// <summary>Vulkan descriptor field Layout.</summary>
    public uint Layout;
    /// <summary>Vulkan descriptor field QueueFamily.</summary>
    public uint QueueFamily;
    /// <summary>Vulkan descriptor field Usage.</summary>
    public uint Usage;
    /// <summary>Vulkan descriptor field LevelCount.</summary>
    public uint LevelCount;
    /// <summary>Vulkan descriptor field SampleCount.</summary>
    public uint SampleCount;
    /// <summary>Vulkan descriptor field Tiling.</summary>
    public uint Tiling;
    /// <summary>Vulkan descriptor field SharingMode.</summary>
    public uint SharingMode;
}

internal sealed class VulkanTextureHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public VulkanTextureHandle() : base(true) { }
    protected override bool ReleaseHandle()
    {
        VulkanInteropNative.Delete(handle);
        return true;
    }
}

internal static partial class VulkanInteropNative
{
    private const string Library = "libSkiaSharp";

    internal static void VerifyAbi()
    {
        try { SkiaSharpVersion.CheckNativeLibraryCompatible(true); }
        catch (Exception error) when (error is InvalidOperationException or TypeInitializationException)
        {
            throw new NotSupportedException("Native/managed Skia version mismatch: " + error.GetBaseException().Message, error);
        }
        try
        {
            if (AbiVersion() != 1 || InfoSize() != Marshal.SizeOf<VulkanTextureInfo>())
                throw new NotSupportedException("Vulkan interop native/managed ABI mismatch.");
        }
        catch (EntryPointNotFoundException error)
        {
            throw new NotSupportedException("The loaded libSkiaSharp has no TrueMoon Vulkan interop exports. " +
                "Build the matching pinned SkiaSharp version with NativeInterop/BuildNativeInterop.ps1; " +
                "a managed binding alone cannot extend the packaged DLL.", error);
        }
    }

    [LibraryImport(Library, EntryPoint = "tm_skia_vk_interop_abi_version")]
    private static partial uint AbiVersion();
    [LibraryImport(Library, EntryPoint = "tm_skia_vk_texture_info_size")]
    private static partial uint InfoSize();
    [LibraryImport(Library, EntryPoint = "tm_skia_vk_surface_acquire")]
    internal static partial VulkanTextureHandle Acquire(nint surface, out VulkanTextureInfo info);
    [LibraryImport(Library, EntryPoint = "tm_skia_vk_texture_return")]
    internal static partial int Return(VulkanTextureHandle texture, uint layout, uint queueFamily);
    [LibraryImport(Library, EntryPoint = "tm_skia_vk_texture_delete")]
    internal static partial void Delete(nint texture);
}
