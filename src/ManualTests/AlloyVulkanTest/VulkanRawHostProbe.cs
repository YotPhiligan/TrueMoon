using Silk.NET.Vulkan;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

// The instance supplies validation; the extra logical devices below belong to the simulated engine.
// None is wrapped in VulkanDevice or created/destroyed by Alloy.
internal static unsafe class VulkanRawHostProbe
{
    internal static void Run(bool validation)
    {
        var provider = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        using (provider)
        {
            VerifyInvalidDescriptors(provider);
            for (var mode = 0; mode < 3; mode++) RunDevice(provider, mode);
        }
        Require(provider.ValidationErrorCount == 0 && provider.ValidationWarningCount == 0,
            "Raw host validation reported errors/warnings, including device destruction.");
        Reject<ObjectDisposedException>(() => SilkVulkanHost.CreateContext(provider));
        Reject<ObjectDisposedException>(provider.WaitIdle);
        Console.WriteLine("Raw Vulkan host passed: 3 engine-owned logical devices, 6 hosted UI sessions, 54 GPU readbacks; " +
            "no features / enabled core features / enabled features2 chain, input, resize/suspend, leases, App stop, callbacks and device lifetime.");
        if (validation) Console.WriteLine("Vulkan core/synchronization validation: 0 errors, 0 warnings, including raw host teardown.");
    }

    private static void RunDevice(VulkanDevice provider, int mode)
    {
        var vk = provider.Api;
        vk.GetPhysicalDeviceFeatures(provider.PhysicalDevice, out var supported);
        var enabled = mode == 0 ? default : new PhysicalDeviceFeatures
        { SamplerAnisotropy = supported.SamplerAnisotropy, DualSrcBlend = supported.DualSrcBlend };
        var storage = new PhysicalDevice16BitStorageFeatures { SType = StructureType.PhysicalDevice16BitStorageFeatures };
        var query = new PhysicalDeviceFeatures2 { SType = StructureType.PhysicalDeviceFeatures2, PNext = &storage };
        vk.GetPhysicalDeviceFeatures2(provider.PhysicalDevice, &query);
        storage = new PhysicalDevice16BitStorageFeatures
        { SType = StructureType.PhysicalDevice16BitStorageFeatures, StorageBuffer16BitAccess = storage.StorageBuffer16BitAccess };
        var features2 = new PhysicalDeviceFeatures2
        { SType = StructureType.PhysicalDeviceFeatures2, Features = enabled, PNext = &storage };
        float priority = 1;
        var queueInfo = new DeviceQueueCreateInfo
        { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = provider.QueueFamily, QueueCount = 1, PQueuePriorities = &priority };
        var create = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo, QueueCreateInfoCount = 1, PQueueCreateInfos = &queueInfo,
            PEnabledFeatures = mode == 1 ? &enabled : null, PNext = mode == 2 ? &features2 : null
        };
        VulkanDevice.Check(vk.CreateDevice(provider.PhysicalDevice, in create, null, out var logical), "CreateEngineDevice");
        try
        {
            vk.GetDeviceQueue(logical, provider.QueueFamily, 0, out var queue);
            var waits = 0; var resolutions = 0; var alive = true;
            var host = new VulkanHostContext(provider.Instance.Handle, provider.PhysicalDevice.Handle, logical.Handle, queue.Handle,
                provider.QueueFamily, Vk.Version11,
                (name, instance, device) =>
                {
                    ObjectDisposedException.ThrowIf(!alive, typeof(VulkanRawHostProbe));
                    Require(instance == 0 || instance == provider.Instance.Handle, "Resolver received a foreign instance.");
                    Require(device == 0 || device == logical.Handle, "Resolver received a foreign logical device.");
                    resolutions++;
                    return device != 0 ? vk.GetDeviceProcAddr(new Device(device), name) : vk.GetInstanceProcAddr(new Instance(instance), name);
                },
                () =>
                {
                    ObjectDisposedException.ThrowIf(!alive, typeof(VulkanRawHostProbe));
                    waits++; VulkanDevice.Check(vk.DeviceWaitIdle(logical), "EngineDeviceWaitIdle");
                }, provider.InstanceExtensions, [], mode == 1 ? (nint)(&enabled) : 0, mode == 2 ? (nint)(&features2) : 0);
            RunSessions(host, vk, logical, provider.PhysicalDevice, queue);
            Require(waits >= 36 && resolutions > 0, "Renderer must use the engine's loader and completion callbacks.");
            VerifyCallbackFailures(host, provider);
            // The host still owns a usable native device after every UI context and App has stopped.
            VulkanDevice.Check(vk.DeviceWaitIdle(logical), "EngineDeviceAfterUiStop");
            alive = false;
            using var disposedRoot = new Panel();
            Reject<ObjectDisposedException>(() => UiSession.Create(disposedRoot, new SkiaVulkanRenderBackend(),
                new VulkanUiTarget(host), new UiViewport(64, 64)));
            Console.WriteLine($"Raw host mode {mode}: anisotropy={(bool)enabled.SamplerAnisotropy}, dualSrcBlend={(bool)enabled.DualSrcBlend}, " +
                $"16bitChain={mode == 2 && storage.StorageBuffer16BitAccess}, loader calls={resolutions}, completion calls={waits}.");
        }
        finally { vk.DeviceWaitIdle(logical); vk.DestroyDevice(logical, null); }
    }

    private static void RunSessions(VulkanHostContext host, Vk api, Device logical, PhysicalDevice gpu, Queue queue)
    {
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost()));
        using var app = builder.Build();
        app.StartAsync().GetAwaiter().GetResult();
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        UiSession? last = null;
        for (var cycle = 0; cycle < 2; cycle++)
        {
            var root = new Panel();
            var button = new Button("Raw Vulkan HUD")
            { Width = 150, Height = 40, Margin = new Thickness(10), HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
            root.Add(button);
            var clicks = 0; button.Click += () => clicks++;
            var target = new VulkanUiTarget(host);
            Require(ReferenceEquals(target.Host, host), "Raw target must preserve the host descriptor identity.");
            var ui = factory.Create(root, target, new UiViewport(320, 180));
            last = ui;
            Require(factory.ActiveSessionCount == 1, "Expected one hosted UI session.");
            foreach (var viewport in new[] { new UiViewport(320, 180), new UiViewport(480, 270, 1.5f), new UiViewport(320, 180) })
            {
                ui.Resize(viewport);
                for (var frame = 0; frame < 3; frame++)
                {
                    button.Value = $"Raw frame {frame}";
                    Require(ui.Update() && !ui.Update(), "Retained UI must draw on change and skip unchanged frames.");
                    var lease = ui.AcquireVulkanTexture();
                    var layout = (ImageLayout)lease.Info.Layout;
                    try
                    {
                        Require(lease.Info.QueueFamily == host.QueueFamily || lease.Info.QueueFamily == Vk.QueueFamilyIgnored,
                            "Exported image belongs to another queue family.");
                        var pixels = VulkanHostReadback.Read(api, logical, gpu, queue, host.QueueFamily, lease.Info, state => layout = state);
                        var x = (int)((button.Bounds.X + button.Bounds.Width - 8) * viewport.Scale);
                        var y = (int)((button.Bounds.Y + button.Bounds.Height / 2) * viewport.Scale);
                        var offset = (y * viewport.Width + x) * 4;
                        var color = button.Theme.Control;
                        Require(pixels[offset] == color.R && pixels[offset + 1] == color.G && pixels[offset + 2] == color.B && pixels[offset + 3] == 255,
                            "Raw engine device must contain retained button pixels.");
                        Require(pixels[^1] == 0, "HUD background must preserve transparency.");
                        Reject<InvalidOperationException>(() => ui.Resize(viewport with { Width = viewport.Width + 1 }));
                        Reject<InvalidOperationException>(ui.Dispose);
                    }
                    finally { lease.Return(layout); }
                    Reject<InvalidOperationException>(() => lease.Return(layout));
                }
            }
            Require(ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20)).PointerCaptured, "Expected HUD pointer capture.");
            Require(ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20)).Handled && clicks == 1, "Expected one raw-host HUD click.");
            ui.Resize(new UiViewport(0, 0)); Require(!ui.Update(), "Suspended raw-host UI must not draw.");
            ui.Resize(new UiViewport(320, 180)); Require(ui.Update(), "Restored raw-host UI must draw.");
            if (cycle == 0) { ui.Dispose(); Require(factory.ActiveSessionCount == 0 && root.IsDisposed, "Explicit disposal must release registry/tree."); }
        }
        app.StopAsync().GetAwaiter().GetResult();
        Require(last!.IsDisposed && factory.ActiveSessionCount == 0, "App stop must dispose the remaining UI session.");
        using var next = UiSession.Create(new Panel(), new SkiaVulkanRenderBackend(), new VulkanUiTarget(host), new UiViewport(64, 64));
        Require(next.Update(), "Host device must support a new UI context after App stop.");
    }

    private static void VerifyCallbackFailures(VulkanHostContext good, VulkanDevice provider)
    {
        var missing = new VulkanHostContext(good.Instance, good.PhysicalDevice, good.Device, good.Queue, good.QueueFamily, good.ApiVersion,
            (_, _, _) => 0, () => { }, good.InstanceExtensions, good.DeviceExtensions);
        using var missingRoot = new Panel();
        Reject<NotSupportedException>(() => UiSession.Create(missingRoot, new SkiaVulkanRenderBackend(), new VulkanUiTarget(missing), new UiViewport(64, 64)));
        var completionError = new InvalidOperationException("Simulated engine completion failure.");
        var failedCompletion = new VulkanHostContext(good.Instance, good.PhysicalDevice, good.Device, good.Queue, good.QueueFamily, good.ApiVersion,
            (_, _, _) => throw new InvalidOperationException("Loader must not be invoked after completion failure."),
            () => throw completionError, good.InstanceExtensions, good.DeviceExtensions);
        using var completionRoot = new Panel();
        var completionRejected = false;
        try { using var ui = UiSession.Create(completionRoot, new SkiaVulkanRenderBackend(), new VulkanUiTarget(failedCompletion), new UiViewport(64, 64)); }
        catch (InvalidOperationException error) when (ReferenceEquals(error, completionError)) { completionRejected = true; }
        Require(completionRejected, "Failed GPU completion must prevent context creation.");
        var marker = new InvalidOperationException("Simulated engine loader failure.");
        var failing = new VulkanHostContext(good.Instance, good.PhysicalDevice, good.Device, good.Queue, good.QueueFamily, good.ApiVersion,
            (_, _, _) => throw marker, () => { }, good.InstanceExtensions, good.DeviceExtensions);
        using var root = new Panel();
        try { using var ui = UiSession.Create(root, new SkiaVulkanRenderBackend(), new VulkanUiTarget(failing), new UiViewport(64, 64)); }
        catch (InvalidOperationException error) when (ReferenceEquals(error.InnerException, marker))
        { provider.WaitIdle(); return; }
        throw new InvalidOperationException("Loader callback exceptions must be reported safely on the managed side.");
    }

    private static void VerifyInvalidDescriptors(VulkanDevice device)
    {
        VulkanHostContext Describe(nint instance, uint family, uint version, IEnumerable<string> extensions) =>
            new(instance, device.PhysicalDevice.Handle, device.Device.Handle, device.Queue.Handle, family, version,
                device.GetProcedureAddress, device.WaitIdle, extensions, []);
        Reject<ArgumentOutOfRangeException>(() => Describe(0, device.QueueFamily, Vk.Version11, []));
        Reject<ArgumentOutOfRangeException>(() => Describe(device.Instance.Handle, Vk.QueueFamilyIgnored, Vk.Version11, []));
        Reject<ArgumentOutOfRangeException>(() => Describe(device.Instance.Handle, device.QueueFamily, Vk.Version10, []));
        Reject<ArgumentException>(() => Describe(device.Instance.Handle, device.QueueFamily, Vk.Version11, ["VK_bad\0name"]));
        var names = new[] { "VK_original", "VK_original" };
        var descriptor = Describe(device.Instance.Handle, device.QueueFamily, Vk.Version11, names);
        names[0] = "VK_mutated";
        Require(descriptor.InstanceExtensions.Count == 1 && descriptor.InstanceExtensions[0] == "VK_original", "Extensions must be immutable snapshots.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
