using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Explicit v1 CPU rendering schemas and registry bindings. No renderer, catalog service or GPU is activated.</summary>
public static class MeshRenderingComponents
{
    /// <summary>Persistent singleton mesh-renderer component type.</summary>
    public static ComponentTypeId MeshRendererId { get; } = new(RenderContentProfile.Id("1101"));
    /// <summary>Persistent singleton perspective-camera component type.</summary>
    public static ComponentTypeId CameraId { get; } = new(RenderContentProfile.Id("1102"));
    /// <summary>Exact scalar mesh schema requiring the existing Transform v1.</summary>
    public static ComponentSchema MeshRendererSchema { get; } = new(MeshRendererId, 1,
        [new("mesh", PropertyValueKind.String, PropertyValue.From(RenderContentProfile.CubeAssetId.ToString("D"))),
         new("bindings", PropertyValueKind.String, PropertyValue.From(RenderContentProfile.CubeBindingSetAssetId.ToString("D"))),
         new("enabled", PropertyValueKind.Boolean, PropertyValue.From(true)), Mask(),
         new("tintEnabled", PropertyValueKind.Boolean, PropertyValue.From(false)), Number("tintR", 1), Number("tintG", 1), Number("tintB", 1)],
        requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Exact perspective settings requiring the existing Transform v1.</summary>
    public static ComponentSchema CameraSchema { get; } = new(CameraId, 1,
        [Number("fov", Math.PI / 3), Number("near", .1), Number("far", 100), Mask(),
         Number("clearR", .04), Number("clearG", .06), Number("clearB", .10)], requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Gets only the two new schemas; legacy BuiltInComponents.Schemas stays unchanged.</summary>
    public static ImmutableArray<ComponentSchema> Schemas { get; } = [MeshRendererSchema, CameraSchema];

    /// <summary>Registers only mesh/camera values against one borrowed immutable CPU manifest.</summary>
    /// <remarks>The caller registers Transform separately. This explicitly installs the builder's single AssetValidator;
    /// consumers with additional asset domains must compose their validator afterwards. Discard the builder if registration fails.</remarks>
    /// <param name="builder">Composition-time exact registry builder.</param><param name="content">Frozen validated CPU scope, retained by callbacks.</param><returns>The same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder, RenderContentManifest content)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(content);
        builder.WithAssetValidator(content.ValidateAsset);
        builder.Register<MeshRenderer>(MeshRendererSchema, context => Renderer(context, content),
            [new("mesh", ComponentReferenceKind.Asset), new("bindings", ComponentReferenceKind.Asset)],
            context => { Renderer(context, content); });
        builder.Register<PerspectiveCamera>(CameraSchema, context => Camera(context), validate: context => { Camera(context); });
        return builder;
    }
    internal static MeshRenderer Renderer(ComponentValidationContext context, RenderContentManifest content)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        ContentValidation.Require(!context.SourceEntity.Components.Values.Any(static component => component.TypeId == BuiltInComponents.AppearanceId),
            RenderContentError.InvalidComponent, "Appearance and MeshRenderer are ambiguous on one entity.");
        var mesh = content.Resolve(Guid.ParseExact(context.GetString("mesh"), "D"), RenderContentProfile.MeshKindId).Key;
        var bindings = content.Resolve(Guid.ParseExact(context.GetString("bindings"), "D"), RenderContentProfile.BindingSetKindId).Key;
        var result = new MeshRenderer(mesh, bindings, context.GetBoolean("enabled"), Layers(context), context.GetBoolean("tintEnabled"),
            new(context.GetSingle("tintR", 0, 1), context.GetSingle("tintG", 0, 1), context.GetSingle("tintB", 0, 1)));
        result.Validate(content); return result;
    }
    internal static PerspectiveCamera Camera(ComponentValidationContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var fov = context.GetNumber("fov"); var near = context.GetNumber("near"); var far = context.GetNumber("far");
        ContentValidation.Require(fov > 0 && fov < Math.PI && near > 0 && far > near,
            RenderContentError.InvalidCamera, "Camera Number values are outside the projection domain.");
        var result = new PerspectiveCamera(context.GetSingle("fov"), context.GetSingle("near"), context.GetSingle("far"), Layers(context),
            new(context.GetSingle("clearR", 0, 1), context.GetSingle("clearG", 0, 1), context.GetSingle("clearB", 0, 1)));
        result.Validate(); return result;
    }
    private static uint Layers(ComponentValidationContext context)
    {
        var value = context.GetInteger("layers");
        ContentValidation.Require(value is >= 0 and <= uint.MaxValue, RenderContentError.InvalidComponent, "Layers require an unsigned 32-bit mask.");
        return (uint)value;
    }
    private static ComponentPropertySchema Mask() => new("layers", PropertyValueKind.Integer, PropertyValue.From((long)uint.MaxValue));
    private static ComponentPropertySchema Number(string id, double value) => new(id, PropertyValueKind.Number, PropertyValue.From(value));
}
