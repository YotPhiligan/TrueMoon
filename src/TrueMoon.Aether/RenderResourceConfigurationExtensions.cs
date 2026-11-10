using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Gpu.Vulkan;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;

namespace TrueMoon.Aether;

/// <summary>Explicit lazy App integration for CPU content ownership and resource-device preparation.</summary>
public static class RenderResourceConfigurationExtensions
{
    /// <summary>Registers one owned resource root and non-owning typed factory wrappers. Startup creates no GPU context.</summary>
    /// <param name="context">App composition root.</param><param name="configure">Optional copied lower limits.</param><returns>The same context.</returns>
    /// <remarks>Dispose scene/view products before App. Host fallback retains failed cleanup. An existing backend factory is preserved.</remarks>
    public static IAppConfigurationContext UseAetherRenderResources(this IAppConfigurationContext context, Action<RenderResourceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); var options = new RenderResourceOptions(); configure?.Invoke(options); var limits = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<RenderResourceHost>() || services.Exist<IFactory<RenderManifestLease, RenderContentManifest>>() ||
                services.Exist<IFactory<RenderDeviceOwner, RenderDeviceRequest>>() || services.Exist<IFactory<RenderPreparation, RenderPreparationRequest>>() ||
                services.Exist<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>() ||
                services.Exist<IFactory<RenderSnapshotView, RenderSnapshotViewRequest>>()) throw new InvalidOperationException("Aether render resources are already registered.");
            if (!services.Exist<IFactory<IRenderResourceBackend, RenderDeviceRequest>>())
                services.Singleton<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(_ => new Factory<IRenderResourceBackend, RenderDeviceRequest>(request => new VulkanRenderResourceBackend(request)));
            services.Singleton<RenderResourceHost>(resolver => new(new RenderContentCatalog(limits), resolver.Resolve<IFactory<IRenderResourceBackend, RenderDeviceRequest>>(), limits));
            services.Singleton<IFactory<RenderManifestLease, RenderContentManifest>>(resolver =>
                new Factory<RenderManifestLease, RenderContentManifest>(resolver.Resolve<RenderResourceHost>().Content.Create));
            services.Singleton<IFactory<RenderDeviceOwner, RenderDeviceRequest>>(resolver =>
                new Factory<RenderDeviceOwner, RenderDeviceRequest>(resolver.Resolve<RenderResourceHost>().Create));
            services.Singleton<IFactory<RenderPreparation, RenderPreparationRequest>>(resolver =>
                new Factory<RenderPreparation, RenderPreparationRequest>(resolver.Resolve<RenderResourceHost>().Create));
            services.Singleton<IFactory<RenderWorldBinding, RenderWorldBindingRequest>>(resolver =>
                new Factory<RenderWorldBinding, RenderWorldBindingRequest>(resolver.Resolve<RenderResourceHost>().Create));
            services.Singleton<IFactory<RenderSnapshotView, RenderSnapshotViewRequest>>(resolver =>
                new Factory<RenderSnapshotView, RenderSnapshotViewRequest>(resolver.Resolve<RenderResourceHost>().Create));
        });
        return context;
    }
}
