using SkiaSharp;
using TrueMoon.Argentis;


namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>A borrowed host Vulkan device. The caller keeps it alive until all UI sessions close.</summary>
public sealed class VulkanUiTarget : UiRenderTarget
{

    /// <summary>The borrowed handles, enabled capabilities and host callbacks.</summary>
    public VulkanHostContext Host { get; }

    /// <summary>Connects an external host without creating or owning its Vulkan resources.</summary>
    /// <param name="host">Borrowed host descriptor which outlives the UI session.</param>
    public VulkanUiTarget(VulkanHostContext host) => Host = host ?? throw new ArgumentNullException(nameof(host));

}
/// <summary>Creates Skia UI surfaces on externally owned Vulkan devices without creating a window or frame loop.</summary>
public sealed class SkiaVulkanRenderBackend : IRenderBackend
{
    /// <inheritdoc />
    public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
    {
        if (target is not VulkanUiTarget vulkan) throw new ArgumentException("VulkanUiTarget is required.", nameof(target));
        viewport.Validate();
        SkiaNativeLibrary.VerifyVulkanInterop();
        return new SkiaVulkanSurface(vulkan.Host, viewport);
    }
}
/// <summary>Session-owned context/surface on a borrowed device, with explicit host texture leases.</summary>
public sealed class SkiaVulkanSurface : IUiRenderSurface
{
    private readonly VulkanHostContext _device;
    private readonly GRVkExtensions _extensions;
    private readonly GRVkBackendContext _backend;
    private readonly GRContext _context;
    private readonly SKSurface _measurement;
    private VulkanInteropSurface? _surface;
    private bool _disposed;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    /// <inheritdoc />
    public ITextLayoutService TextLayout { get; }
    internal SkiaVulkanSurface(VulkanHostContext device, UiViewport viewport)
    {
        _device = device;
        device.WaitIdle();
        device.VerifyLoader();
        try
        {
            _extensions = GRVkExtensions.Create(device.ResolveProcedure, device.Instance, device.PhysicalDevice,
                device.InstanceExtensions.ToArray(), device.DeviceExtensions.ToArray());
            device.ThrowIfResolutionFailed();
            _backend = new GRVkBackendContext
            {
                VkInstance = device.Instance, VkPhysicalDevice = device.PhysicalDevice,
                VkDevice = device.Device, VkQueue = device.Queue, GraphicsQueueIndex = device.QueueFamily,
                MaxAPIVersion = device.ApiVersion, Extensions = _extensions, GetProcedureAddress = device.ResolveProcedure,
                VkPhysicalDeviceFeatures = device.EnabledFeatures, VkPhysicalDeviceFeatures2 = device.EnabledFeatures2
            };
            _context = GRContext.CreateVulkan(_backend)!;
            device.ThrowIfResolutionFailed();
            if (_context == null) throw new NotSupportedException("Skia Vulkan context creation failed.");
            _measurement = SKSurface.Create(new SKImageInfo(1, 1)) ?? throw new InvalidOperationException("Could not create text service surface.");
            TextLayout = new SkiaDrawingContext(_measurement.Canvas);
            Resize(viewport);
        }
        catch { _surface?.Dispose(); _measurement?.Dispose(); _context?.Dispose(); _backend?.Dispose(); _extensions?.Dispose(); throw; }
    }
    /// <inheritdoc />
    public void VerifyAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Vulkan rendering requires its creator thread.");
        _surface?.VerifyAvailable();
    }
    /// <inheritdoc />
    public void Resize(UiViewport viewport)
    {
        VerifyAvailable(); viewport.Validate();
        var replacement = viewport.IsEmpty ? null : new VulkanInteropSurface(_device, _context, new SKSizeI(viewport.Width, viewport.Height));
        _surface?.Dispose();
        _surface = replacement;
    }
    /// <inheritdoc />
    public void Render(Element root, UiViewport viewport)
    {
        VerifyAvailable();
        if (_surface == null) return;
        _surface.Draw(canvas =>
        {
            canvas.Clear(SKColors.Transparent); canvas.Save();
            try { canvas.Scale(viewport.Scale); root.Draw(new SkiaDrawingContext(canvas)); }
            finally { canvas.Restore(); }
        });
    }
    /// <summary>Flushes UI work and borrows the GPU image until its actual state is returned.</summary>
    public VulkanTextureLease AcquireTexture() { VerifyAvailable(); return (_surface ?? throw new InvalidOperationException("Viewport is suspended.")).Acquire(); }
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        VerifyAvailable(); _surface?.Dispose(); _measurement.Dispose(); _context.Dispose(); _backend.Dispose(); _extensions.Dispose(); _disposed = true;
    }
}
/// <summary>Vulkan-specific operations on the backend-independent session.</summary>
public static class VulkanUiSessionExtensions
{
    /// <summary>Borrows the current rendered UI texture; call Update before the first acquisition.</summary>
    public static VulkanTextureLease AcquireVulkanTexture(this UiSession session)
    {
        session.VerifyAccess();
        if (session.NeedsUpdate) throw new InvalidOperationException("Update UI before acquiring its texture.");
        return (session.Rendering as SkiaVulkanSurface ?? throw new InvalidOperationException("Session is not using Skia Vulkan.")).AcquireTexture();
    }
}
