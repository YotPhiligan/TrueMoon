using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Rendering.Resources;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Opt-in scene import bridge retaining CPU leases in the existing SceneWorld owner.</summary>
public static class RenderResourceComponents
{
    /// <summary>Registers the two f.1 schemas while acquiring one counted scene lease per imported render/camera component.</summary>
    /// <remarks>The caller retains the input lease while the registry accepts new loads. Existing scenes own independent retains.
    /// The builder's single AssetValidator is installed explicitly; compose additional asset domains afterwards.</remarks>
    /// <param name="builder">Composition-time builder, with Transform registered separately.</param><param name="scope">Borrowed live CPU scope.</param><returns>The same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder, RenderManifestLease scope)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(scope); scope.CheckAdmission();
        builder.WithAssetValidator((id, token) => { scope.CheckAdmission(); scope.Manifest.ValidateAsset(id, token); });
        builder.Register<MeshRenderer>(MeshRenderingComponents.MeshRendererSchema, context =>
        {
            scope.CheckAdmission(); var value = MeshRenderingComponents.Renderer(context, scope.Manifest);
            context.Acquire(_ => scope.Retain()); return value;
        }, [new("mesh", ComponentReferenceKind.Asset), new("bindings", ComponentReferenceKind.Asset)],
            context => { scope.CheckAdmission(); MeshRenderingComponents.Renderer(context, scope.Manifest); });
        builder.Register<PerspectiveCamera>(MeshRenderingComponents.CameraSchema, context =>
        {
            scope.CheckAdmission(); var value = MeshRenderingComponents.Camera(context);
            context.Acquire(_ => scope.Retain()); return value;
        }, validate: context => { scope.CheckAdmission(); MeshRenderingComponents.Camera(context); });
        return builder;
    }
}
