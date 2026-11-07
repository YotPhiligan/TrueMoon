using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Unmanaged render data referencing an exact CPU manifest; it owns no resource leases.</summary>
/// <param name="Mesh">Pinned mesh key.</param><param name="Bindings">Pinned binding-set key for that exact mesh.</param>
/// <param name="Enabled">Render visibility, independent of simulation.</param><param name="Layers">Visibility mask.</param>
/// <param name="TintEnabled">Selects the stored renderer-wide tint.</param><param name="TintLinear">Linear multiplier, validated even when disabled.</param>
public readonly record struct MeshRenderer(RenderAssetKey Mesh, RenderAssetKey Bindings, bool Enabled, uint Layers, bool TintEnabled, Vector3 TintLinear)
{
    /// <summary>Gets the validated effective multiplier without mutating stored override values.</summary>
    public Vector3 EffectiveTint { get { RenderColor.Validate(TintLinear); return TintEnabled ? TintLinear : Vector3.One; } }
    /// <summary>Checks all references and values, including invisible/disabled renderers.</summary><param name="content">Borrowed frozen CPU scope.</param>
    public void Validate(RenderContentManifest content)
    {
        ArgumentNullException.ThrowIfNull(content); RenderColor.Validate(TintLinear);
        Mesh.Validate(RenderContentProfile.MeshKindId); Bindings.Validate(RenderContentProfile.BindingSetKindId);
        content.Resolve(Mesh);
        var bindings = (MaterialBindingSetDefinition)content.Resolve(Bindings).Definition;
        ContentValidation.Require(bindings.Mesh == Mesh, RenderContentError.InvalidBinding, "Renderer bindings belong to a different mesh revision.");
    }
}
