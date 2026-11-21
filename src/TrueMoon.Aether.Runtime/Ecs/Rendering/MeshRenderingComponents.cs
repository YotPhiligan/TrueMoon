using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Ecs.Rendering;

/// <summary>Explicit CPU rendering schemas and registry bindings. No renderer, catalog service or GPU is activated.</summary>
public static class MeshRenderingComponents
{
    /// <summary>Persistent singleton mesh-renderer component type.</summary>
    public static ComponentTypeId MeshRendererId { get; } = new(RenderContentProfile.Id("1101"));
    /// <summary>Persistent singleton perspective-camera component type.</summary>
    public static ComponentTypeId CameraId { get; } = new(RenderContentProfile.Id("1102"));
    /// <summary>Persistent singleton orthographic-camera component type.</summary>
    public static ComponentTypeId OrthographicCameraId { get; } = new(RenderContentProfile.Id("1103"));
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
    /// <summary>Exact orthographic settings requiring the existing Transform v1.</summary>
    public static ComponentSchema OrthographicCameraSchema { get; } = new(OrthographicCameraId, 1,
        [Number("verticalSize", 10), Number("near", .1), Number("far", 100), Mask(),
         Number("clearR", .04), Number("clearG", .06), Number("clearB", .10)], requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Gets the mesh rendering schemas; legacy BuiltInComponents.Schemas stays unchanged.</summary>
    public static ImmutableArray<ComponentSchema> Schemas { get; } = [MeshRendererSchema, CameraSchema, OrthographicCameraSchema];

    /// <summary>Registers only mesh/camera values against one borrowed immutable CPU manifest.</summary>
    /// <remarks>The caller registers Transform separately. This explicitly installs the builder's single AssetValidator;
    /// consumers with additional asset domains must compose their validator afterwards. Discard the builder if registration fails.</remarks>
    /// <param name="builder">Composition-time exact registry builder.</param><param name="content">Frozen validated CPU scope, retained by callbacks.</param><returns>The same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder, RenderContentManifest content)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(content);
        builder.WithAssetValidator(content.ValidateAsset);
        builder.Register<MeshRenderer>(MeshRendererSchema, context => Renderer(context, content),
            [new("mesh", ComponentReferenceKind.Asset)],
            context => { Renderer(context, content); });
        builder.Register<PerspectiveCamera>(CameraSchema, context => Camera(context), validate: context => { Camera(context); });
        builder.Register<OrthographicCamera>(OrthographicCameraSchema, context => Orthographic(context),
            validate: context => { Orthographic(context); });
        return builder;
    }
    internal static MeshRenderer Renderer(ComponentValidationContext context, RenderContentManifest content)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        ContentValidation.Require(!context.SourceEntity.Components.Values.Any(static component => component.TypeId == BuiltInComponents.AppearanceId),
            RenderContentError.InvalidComponent, "Appearance and MeshRenderer are ambiguous on one entity.");
        var mesh = content.Resolve(Guid.ParseExact(context.GetString("mesh"), "D"), RenderContentProfile.MeshKindId).Key;
        if (!SceneMaterialBindingReference.TryParse(mesh.AssetId, context.GetString("bindings"), out var bindingId, out var overrides))
            throw BindingError("Invalid material binding reference.");
        try { content.ValidateAsset(bindingId, context.CancellationToken); }
        catch (RenderContentException error) { throw BindingError("Asset reference validation failed.", error); }
        var entry = content.Resolve(bindingId, RenderContentProfile.BindingSetKindId);
        var definition = (MaterialBindingSetDefinition)entry.Definition;
        if (overrides.Any(item => !definition.Bindings.Any(slot => slot.SlotId == item.SlotId && slot.Material.AssetId == item.MaterialId)))
            throw BindingError("Material binding does not match the declared slot overrides.");
        var bindings = entry.Key;
        var result = new MeshRenderer(mesh, bindings, context.GetBoolean("enabled"), Layers(context), context.GetBoolean("tintEnabled"),
            new(context.GetSingle("tintR", 0, 1), context.GetSingle("tintG", 0, 1), context.GetSingle("tintB", 0, 1)));
        result.Validate(content); return result;

        SceneWorldLoadException BindingError(string message, Exception? inner = null) =>
            new(SceneWorldLoadError.InvalidReference, message, context.Scene.Id, context.SourceEntity.Id, context.Component.Id, "bindings", inner);
    }
    internal static PerspectiveCamera Camera(ComponentValidationContext context)
    {
        ValidateCameraType(context, OrthographicCameraId);
        var fov = context.GetNumber("fov"); var near = context.GetNumber("near"); var far = context.GetNumber("far");
        ContentValidation.Require(fov > 0 && fov < Math.PI && near > 0 && far > near,
            RenderContentError.InvalidCamera, "Camera Number values are outside the projection domain.");
        var result = new PerspectiveCamera(context.GetSingle("fov"), context.GetSingle("near"), context.GetSingle("far"), Layers(context),
            ClearColor(context));
        result.Validate(); return result;
    }

    internal static OrthographicCamera Orthographic(ComponentValidationContext context)
    {
        ValidateCameraType(context, CameraId);
        var result = new OrthographicCamera(context.GetSingle("verticalSize"), context.GetSingle("near"),
            context.GetSingle("far"), Layers(context), ClearColor(context));
        result.Validate();
        return result;
    }

    private static void ValidateCameraType(ComponentValidationContext context, ComponentTypeId other)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        ContentValidation.Require(!context.SourceEntity.Components.Values.Any(component => component.TypeId == other),
            RenderContentError.InvalidCamera, "A camera entity must have exactly one projection.");
    }

    private static Vector3 ClearColor(ComponentValidationContext context)
        => new(context.GetSingle("clearR", 0, 1), context.GetSingle("clearG", 0, 1), context.GetSingle("clearB", 0, 1));
    private static uint Layers(ComponentValidationContext context)
    {
        var value = context.GetInteger("layers");
        ContentValidation.Require(value is >= 0 and <= uint.MaxValue, RenderContentError.InvalidComponent, "Layers require an unsigned 32-bit mask.");
        return (uint)value;
    }
    private static ComponentPropertySchema Mask() => new("layers", PropertyValueKind.Integer, PropertyValue.From((long)uint.MaxValue));
    private static ComponentPropertySchema Number(string id, double value) => new(id, PropertyValueKind.Number, PropertyValue.From(value));
}
