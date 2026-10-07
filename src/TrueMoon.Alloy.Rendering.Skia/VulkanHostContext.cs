using SkiaSharp;


namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>Resolves an entry point through the host's Vulkan loader.</summary>
/// <param name="name">UTF-8 Vulkan entry-point name represented as a managed string.</param>
/// <param name="instance">Instance supplied by Skia, or zero for global entry points.</param>
/// <param name="device">Device supplied by Skia, or zero for instance entry points.</param>
/// <returns>The function address, or zero when unavailable.</returns>
public delegate nint VulkanProcedureResolver(string name, nint instance, nint device);

/// <summary>Borrowed Vulkan handles and capabilities for a host-owned graphics queue.</summary>
/// <remarks>
/// This descriptor never creates or destroys an instance, device, queue, surface or swapchain.
/// Keep the handles, loader, callback targets and enabled-feature memory (including its pNext chain)
/// alive until every UI session using it has closed. Serialize all host and UI queue operations on
/// the session thread. Protected queues and multiple frames in flight are not supported.
/// </remarks>
public sealed class VulkanHostContext
{
    private readonly VulkanProcedureResolver _resolver;
    private readonly Action _waitIdle;
    private Exception? _resolutionError;
    private nint _enumerateInstanceExtensions, _enumerateDeviceExtensions;
    /// <summary>The borrowed VkInstance.</summary>
    public nint Instance { get; }
    /// <summary>The VkPhysicalDevice used to create Device.</summary>
    public nint PhysicalDevice { get; }
    /// <summary>The borrowed VkDevice.</summary>
    public nint Device { get; }
    /// <summary>An unprotected graphics queue belonging to Device.</summary>
    public nint Queue { get; }
    /// <summary>The family of Queue, also used for UI image ownership.</summary>
    public uint QueueFamily { get; }
    /// <summary>The API version requested in the host's VkApplicationInfo.</summary>
    public uint ApiVersion { get; }
    /// <summary>A snapshot of extensions enabled when creating Instance.</summary>
    public IReadOnlyList<string> InstanceExtensions { get; }
    /// <summary>A snapshot of extensions enabled when creating Device.</summary>
    public IReadOnlyList<string> DeviceExtensions { get; }
    /// <summary>Borrowed VkPhysicalDeviceFeatures containing enabled values; zero means none.</summary>
    public nint EnabledFeatures { get; }
    /// <summary>Borrowed VkPhysicalDeviceFeatures2 enabled values and pNext chain; takes precedence over EnabledFeatures.</summary>
    public nint EnabledFeatures2 { get; }

    /// <summary>Describes an existing device without taking ownership of any native resource.</summary>
    /// <param name="instance">Nonzero VkInstance handle.</param>
    /// <param name="physicalDevice">Nonzero VkPhysicalDevice handle belonging to instance.</param>
    /// <param name="device">Nonzero VkDevice handle created from physicalDevice.</param>
    /// <param name="queue">Nonzero graphics queue from device.</param>
    /// <param name="queueFamily">Queue's family index; queue-family sentinel values are invalid.</param>
    /// <param name="apiVersion">Vulkan API version requested by the host, at least 1.1.</param>
    /// <param name="getProcedureAddress">Host loader callback. Its exceptions are captured before returning to native code.</param>
    /// <param name="waitIdle">Checks host lifetime and completes device GPU work, throwing on a failed vkDeviceWaitIdle.</param>
    /// <param name="instanceExtensions">Actually enabled instance extensions, not supported extensions.</param>
    /// <param name="deviceExtensions">Actually enabled device extensions, not supported extensions.</param>
    /// <param name="enabledFeatures">Pointer to enabled core features, or zero if none are advertised to Skia.</param>
    /// <param name="enabledFeatures2">Pointer to enabled features2 and its chain, or zero. Memory remains host-owned.</param>
    public VulkanHostContext(nint instance, nint physicalDevice, nint device, nint queue, uint queueFamily,
        uint apiVersion, VulkanProcedureResolver getProcedureAddress, Action waitIdle,
        IEnumerable<string> instanceExtensions, IEnumerable<string> deviceExtensions,
        nint enabledFeatures = 0, nint enabledFeatures2 = 0)
    {
        if (instance == 0) throw new ArgumentOutOfRangeException(nameof(instance));
        if (physicalDevice == 0) throw new ArgumentOutOfRangeException(nameof(physicalDevice));
        if (device == 0) throw new ArgumentOutOfRangeException(nameof(device));
        if (queue == 0) throw new ArgumentOutOfRangeException(nameof(queue));
        if (queueFamily >= uint.MaxValue - 2) throw new ArgumentOutOfRangeException(nameof(queueFamily));
        // VK_MAKE_API_VERSION: variant/major/minor/patch. This integration uses standard Vulkan >= 1.1.
        if ((apiVersion >> 29) != 0 || (apiVersion >> 22) == 0 || apiVersion < ((1u << 22) | (1u << 12)))
            throw new ArgumentOutOfRangeException(nameof(apiVersion), "Skia Vulkan requires Vulkan 1.1 or later.");
        ArgumentNullException.ThrowIfNull(getProcedureAddress);
        ArgumentNullException.ThrowIfNull(waitIdle);
        Instance = instance; PhysicalDevice = physicalDevice; Device = device; Queue = queue;
        QueueFamily = queueFamily; ApiVersion = apiVersion;
        _resolver = getProcedureAddress; _waitIdle = waitIdle;
        InstanceExtensions = Snapshot(instanceExtensions, nameof(instanceExtensions));
        DeviceExtensions = Snapshot(deviceExtensions, nameof(deviceExtensions));
        EnabledFeatures = enabledFeatures; EnabledFeatures2 = enabledFeatures2;
    }

    private static IReadOnlyList<string> Snapshot(IEnumerable<string> extensions, string parameter)
    {
        ArgumentNullException.ThrowIfNull(extensions, parameter);
        var names = extensions.ToArray();
        if (names.Any(name => string.IsNullOrWhiteSpace(name) || name.Contains('\0')))
            throw new ArgumentException("Extension names must be nonempty and contain no NUL characters.", parameter);
        return Array.AsReadOnly(names.Distinct(StringComparer.Ordinal).ToArray());
    }


    internal nint ResolveProcedure(string name, nint instance, nint device)
    {
        // Extensions initialization calls these pointers immediately; use the exact addresses
        // checked before entering native Skia rather than resolving them for a second time.
        if (device == 0 && instance == 0 && name == "vkEnumerateInstanceExtensionProperties" && _enumerateInstanceExtensions != 0)
            return _enumerateInstanceExtensions;
        if (device == 0 && instance == Instance && name == "vkEnumerateDeviceExtensionProperties" && _enumerateDeviceExtensions != 0)
            return _enumerateDeviceExtensions;
        try { return _resolver(name, instance, device); }
        catch (Exception error)
        {
            // Skia invokes this delegate from native code. Never unwind a managed exception through it.
            Interlocked.CompareExchange(ref _resolutionError, error, null);
            return 0;
        }
    }
    internal void ThrowIfResolutionFailed()
    {
        if (_resolutionError is { } error)
            throw new InvalidOperationException("The host Vulkan procedure resolver failed.", error);
    }
    internal void VerifyLoader()
    {
        _enumerateInstanceExtensions = RequireProcedure("vkEnumerateInstanceExtensionProperties", 0);
        _enumerateDeviceExtensions = RequireProcedure("vkEnumerateDeviceExtensionProperties", Instance);
        RequireProcedure("vkGetPhysicalDeviceProperties", Instance);
    }
    private nint RequireProcedure(string name, nint instance)
    {
        var address = ResolveProcedure(name, instance, 0);
        ThrowIfResolutionFailed();
        return address != 0 ? address : throw new NotSupportedException($"The host Vulkan loader does not provide {name}.");
    }
    internal void WaitIdle() { ThrowIfResolutionFailed(); _waitIdle(); }
}
