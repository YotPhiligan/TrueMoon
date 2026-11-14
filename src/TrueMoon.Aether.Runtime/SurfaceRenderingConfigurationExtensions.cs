using TrueMoon.Aether.Runtime.Rendering.Resources;

namespace TrueMoon.Aether.Runtime;

/// <summary>Explicit App integration for the required mesh surface profile; startup creates no GPU or World.</summary>
public static class SurfaceRenderingConfigurationExtensions
{
    /// <summary>Registers one App-owned view manager and typed resource-root factory. Runtime drains products before game/surface teardown.</summary>
    /// <param name="context">Child/Player composition root.</param><param name="configure">Copied existing render-resource limits.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherSurfaceRendering(this IAppConfigurationContext context, Action<RenderResourceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(context); var options = new RenderResourceOptions(); configure?.Invoke(options); var limits = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<RuntimeMeshRenderHost>()) throw new InvalidOperationException("Required surface rendering is already registered.");
            services.Singleton<IFactory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>>(_ =>
                new Factory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>(backends => new(new RenderContentCatalog(limits), backends, limits)));
            services.Singleton<RuntimeMeshRenderHost>(resolver => new(resolver.Resolve<IFactory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>>>()));
        });
        return context;
    }
}
