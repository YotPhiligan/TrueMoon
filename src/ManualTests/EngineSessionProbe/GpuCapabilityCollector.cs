using Silk.NET.Vulkan;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;

namespace EngineSessionProbe;

// Read-only instance/physical-device queries; no VkDevice, image, allocation, external handle or GPU submission.
internal static unsafe class GpuCapabilityCollector
{
    internal static object Vulkan(SessionIdentity identity)
    {
        var devices = new List<GpuProducerDevice>(); var raw = new List<object>(); string? error = null; var destroyed = false;
        try
        {
            if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("Windows x64 only.");
            using var vk = Vk.GetApi(); uint version = 0; Check(vk.EnumerateInstanceVersion(&version), "EnumerateInstanceVersion");
            if (version < Vk.Version11) throw new NotSupportedException("Vulkan1.1 loader is unavailable.");
            var application = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version11 };
            var create = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo, PApplicationInfo = &application };
            Check(vk.CreateInstance(in create, null, out var instance), "CreateInstance");
            try
            {
                foreach (var physical in PhysicalDevices(vk, instance))
                {
                    var ids = new PhysicalDeviceIDProperties { SType = StructureType.PhysicalDeviceIDProperties };
                    var properties = new PhysicalDeviceProperties2 { SType = StructureType.PhysicalDeviceProperties2, PNext = &ids };
                    vk.GetPhysicalDeviceProperties2(physical, &properties);
                    var adapter = new GpuAdapterId(Convert.ToHexString(new ReadOnlySpan<byte>(ids.DeviceUuid, 16)), Convert.ToHexString(new ReadOnlySpan<byte>(ids.DriverUuid, 16)));
                    var extensions = Extensions(vk, physical);
                    var memoryWin32 = extensions.Contains("VK_KHR_external_memory_win32"); var semaphoreWin32 = extensions.Contains("VK_KHR_external_semaphore_win32");
                    var external = new PhysicalDeviceExternalImageFormatInfo { SType = StructureType.PhysicalDeviceExternalImageFormatInfo, HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit };
                    const ImageUsageFlags usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit;
                    var query = new PhysicalDeviceImageFormatInfo2 { SType = StructureType.PhysicalDeviceImageFormatInfo2, PNext = &external,
                        Format = Format.R8G8B8A8Unorm, Type = ImageType.Type2D, Tiling = ImageTiling.Optimal, Usage = usage };
                    var externalProperties = new ExternalImageFormatProperties { SType = StructureType.ExternalImageFormatProperties };
                    var image = new ImageFormatProperties2 { SType = StructureType.ImageFormatProperties2, PNext = &externalProperties };
                    var result = memoryWin32 ? vk.GetPhysicalDeviceImageFormatProperties2(physical, &query, &image) : Result.ErrorExtensionNotPresent;
                    var memory = externalProperties.ExternalMemoryProperties; var memoryFlags = memory.ExternalMemoryFeatures;
                    var semaphoreInfo = new PhysicalDeviceExternalSemaphoreInfo { SType = StructureType.PhysicalDeviceExternalSemaphoreInfo, HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit };
                    var semaphore = new ExternalSemaphoreProperties { SType = StructureType.ExternalSemaphoreProperties };
                    if (semaphoreWin32) vk.GetPhysicalDeviceExternalSemaphoreProperties(physical, &semaphoreInfo, &semaphore);
                    uint queueCount = 0; vk.GetPhysicalDeviceQueueFamilyProperties(physical, &queueCount, null);
                    if (queueCount > 256) throw new InvalidDataException("Too many queue families.");
                    var queues = new QueueFamilyProperties[queueCount];
                    fixed (QueueFamilyProperties* pointer = queues) vk.GetPhysicalDeviceQueueFamilyProperties(physical, &queueCount, pointer);
                    var graphics = queues.Take((int)queueCount).Any(q => q.QueueCount > 0 && q.QueueFlags.HasFlag(QueueFlags.GraphicsBit));
                    var name = Utf8(properties.Properties.DeviceName, 256);
                    devices.Add(new(adapter, name, properties.Properties.ApiVersion, graphics, memoryWin32, semaphoreWin32,
                        new((int)result, memoryFlags.HasFlag(ExternalMemoryFeatureFlags.ExportableBit), memoryFlags.HasFlag(ExternalMemoryFeatureFlags.ImportableBit),
                            memoryFlags.HasFlag(ExternalMemoryFeatureFlags.DedicatedOnlyBit), memory.CompatibleHandleTypes.HasFlag(ExternalMemoryHandleTypeFlags.OpaqueWin32Bit),
                            image.ImageFormatProperties.MaxExtent.Width, image.ImageFormatProperties.MaxExtent.Height, image.ImageFormatProperties.MaxResourceSize,
                            image.ImageFormatProperties.SampleCounts.HasFlag(SampleCountFlags.Count1Bit)),
                        new(semaphore.ExternalSemaphoreFeatures.HasFlag(ExternalSemaphoreFeatureFlags.ExportableBit), semaphore.ExternalSemaphoreFeatures.HasFlag(ExternalSemaphoreFeatureFlags.ImportableBit),
                            semaphore.CompatibleHandleTypes.HasFlag(ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit))));
                    raw.Add(new { name, ApiVersion = properties.Properties.ApiVersion, DriverVersion = properties.Properties.DriverVersion, properties.Properties.VendorID, properties.Properties.DeviceID,
                        Adapter = adapter, LuidValid = (bool)ids.DeviceLuidvalid, Luid = (bool)ids.DeviceLuidvalid ? Convert.ToHexString(new ReadOnlySpan<byte>(ids.DeviceLuid, 8)) : null,
                        Extensions = extensions.Order().ToArray(), ImageQuery = new { Format = "R8G8B8A8_UNORM", Type = "2D", Tiling = "OPTIMAL", Usage = (uint)usage, Result = (int)result,
                            MemoryFeatures = (uint)memoryFlags, CompatibleHandles = (uint)memory.CompatibleHandleTypes, Samples = (uint)image.ImageFormatProperties.SampleCounts,
                            MaxWidth = image.ImageFormatProperties.MaxExtent.Width, MaxHeight = image.ImageFormatProperties.MaxExtent.Height, image.ImageFormatProperties.MaxResourceSize },
                        Semaphore = new { Features = (uint)semaphore.ExternalSemaphoreFeatures, CompatibleHandles = (uint)semaphore.CompatibleHandleTypes } });
                }
            }
            finally { vk.DestroyInstance(instance, null); destroyed = true; }
        }
        catch (Exception e) { error = e.ToString(); }
        return new { Capabilities = new GpuProducerCapabilities(identity, devices, windows: OperatingSystem.IsWindows(), pointerBits: IntPtr.Size * 8, error: error), Raw = raw,
            InstanceDestroyed = destroyed, LogicalDevicesCreated = 0, AllocationsCreated = 0, HandlesExported = 0 };
    }

    internal static (GpuConsumerCapabilities Capabilities, object Raw) OpenGl(SessionIdentity identity, Func<string, nint> resolve)
    {
        var adapters = new List<GpuAdapterId>(); var extensions = new HashSet<string>(); string? error = null;
        var missing = new List<string>(); var rgba = false; var optimal = false; var fences = false;
        string? vendor = null, renderer = null, version = null; int[] tilings = [];
        var required = new[] { "glGetUnsignedBytevEXT", "glGetUnsignedBytei_vEXT", "glCreateMemoryObjectsEXT", "glDeleteMemoryObjectsEXT", "glMemoryObjectParameterivEXT",
            "glImportMemoryWin32HandleEXT", "glTexStorageMem2DEXT", "glGenSemaphoresEXT", "glDeleteSemaphoresEXT", "glImportSemaphoreWin32HandleEXT", "glWaitSemaphoreEXT", "glSignalSemaphoreEXT",
            "glGenTextures", "glDeleteTextures", "glBindTexture", "glTexParameteri", "glGetInternalformativ", "glFenceSync", "glClientWaitSync", "glDeleteSync", "glFlush" };
        try
        {
            nint Require(string name) => resolve(name) is var address && address != 0 ? address : throw new NotSupportedException(name + " is unavailable.");
            var getString = (delegate* unmanaged<uint, byte*>)Require("glGetString");
            var getInteger = (delegate* unmanaged<uint, int*, void>)Require("glGetIntegerv");
            var getStringI = (delegate* unmanaged<uint, uint, byte*>)Require("glGetStringi");
            var getError = (delegate* unmanaged<uint>)Require("glGetError");
            if (getError() != 0) throw new InvalidOperationException("GL context already contains an error before read-only queries.");
            vendor = Utf8(getString(0x1f00), 1024); renderer = Utf8(getString(0x1f01), 1024); version = Utf8(getString(0x1f02), 1024);
            int count = 0; getInteger(0x821d, &count); if (count is < 0 or > 4096) throw new InvalidDataException("Invalid GL extension count.");
            for (uint i = 0; i < count; i++) extensions.Add(Utf8(getStringI(0x1f03, i), 256));
            foreach (var name in required) if (resolve(name) == 0) missing.Add(name);
            fences = new[] { "glFenceSync", "glClientWaitSync", "glDeleteSync", "glFlush" }.All(n => resolve(n) != 0);
            if ((extensions.Contains("GL_EXT_memory_object") || extensions.Contains("GL_EXT_semaphore")) &&
                resolve("glGetUnsignedBytevEXT") != 0 && resolve("glGetUnsignedBytei_vEXT") != 0)
            {
                var bytes = (delegate* unmanaged<uint, byte*, void>)resolve("glGetUnsignedBytevEXT");
                var indexedBytes = (delegate* unmanaged<uint, uint, byte*, void>)resolve("glGetUnsignedBytei_vEXT");
                int deviceCount = 0; getInteger(0x9596, &deviceCount); if (deviceCount is < 0 or > 16) throw new InvalidDataException("Invalid GL UUID count.");
                byte* driver = stackalloc byte[16]; byte* device = stackalloc byte[16]; bytes(0x9598, driver);
                for (uint i = 0; i < deviceCount; i++) { indexedBytes(0x9597, i, device); adapters.Add(new(Convert.ToHexString(new ReadOnlySpan<byte>(device, 16)), Convert.ToHexString(new ReadOnlySpan<byte>(driver, 16)))); }
            }
            if (resolve("glGetInternalformativ") != 0)
            {
                var internalFormat = (delegate* unmanaged<uint, uint, uint, int, int*, void>)resolve("glGetInternalformativ");
                int supported = 0; internalFormat(0x0de1, 0x8058, 0x826f, 1, &supported); rgba = supported == 1;
                if (extensions.Contains("GL_EXT_memory_object"))
                {
                    int tilingCount = 0; internalFormat(0x0de1, 0x8058, 0x9582, 1, &tilingCount);
                    if (tilingCount is < 0 or > 16) throw new InvalidDataException("Invalid GL tiling count.");
                    tilings = new int[tilingCount]; fixed (int* pointer = tilings) if (tilingCount > 0) internalFormat(0x0de1, 0x8058, 0x9583, tilingCount, pointer);
                    optimal = tilings.Contains(0x9584);
                }
            }
            if (getError() is var glError && glError != 0) throw new InvalidOperationException("GL capability query failed: 0x" + glError.ToString("X"));
        }
        catch (Exception e) { error = e.ToString(); }
        var features = new GpuOpenGlFeatures(extensions.Contains("GL_EXT_memory_object"), extensions.Contains("GL_EXT_memory_object_win32"), extensions.Contains("GL_EXT_semaphore"),
            extensions.Contains("GL_EXT_semaphore_win32"), missing.Count == 0 && error == null, rgba, optimal, fences);
        return (new(identity, adapters, features, windows: OperatingSystem.IsWindows(), pointerBits: IntPtr.Size * 8, error: error),
            new { vendor, renderer, version, Extensions = extensions.Order().ToArray(), MissingProcedures = missing, Tilings = tilings, QueryError = error, TexturesCreated = 0, HandlesImported = 0 });
    }
    private static string Utf8(byte* pointer, int bound)
    {
        if (pointer == null) throw new InvalidDataException("Null native string.");
        var length = 0; while (length < bound && pointer[length] != 0) length++;
        if (length == bound) throw new InvalidDataException("Unterminated native string.");
        return System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(pointer, length));
    }
    private static PhysicalDevice[] PhysicalDevices(Vk vk, Instance instance)
    {
        for (var retry = 0; retry < 3; retry++)
        {
            uint count = 0; Check(vk.EnumeratePhysicalDevices(instance, &count, null), "EnumeratePhysicalDevices count");
            if (count > 16) throw new InvalidDataException("Too many physical devices.");
            var devices = new PhysicalDevice[count]; Result result;
            fixed (PhysicalDevice* pointer = devices) result = vk.EnumeratePhysicalDevices(instance, &count, pointer);
            if (result == Result.Success) return devices.Take((int)count).ToArray();
            if (result != Result.Incomplete) Check(result, "EnumeratePhysicalDevices");
        }
        throw new IOException("Physical-device enumeration kept changing.");
    }
    private static HashSet<string> Extensions(Vk vk, PhysicalDevice physical)
    {
        uint count = 0; Check(vk.EnumerateDeviceExtensionProperties(physical, (byte*)null, &count, null), "EnumerateDeviceExtensionProperties count");
        if (count > 1024) throw new InvalidDataException("Too many device extensions.");
        var properties = new ExtensionProperties[count];
        fixed (ExtensionProperties* pointer = properties) Check(vk.EnumerateDeviceExtensionProperties(physical, (byte*)null, &count, pointer), "EnumerateDeviceExtensionProperties");
        var result = new HashSet<string>(); for (var i = 0; i < count; i++) { var value = properties[i]; result.Add(Utf8(value.ExtensionName, 256)); } return result;
    }
    private static void Check(Result result, string operation) { if (result != Result.Success) throw new IOException(operation + ": " + result); }
}
