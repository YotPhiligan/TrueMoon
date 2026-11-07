using System.Runtime.CompilerServices;

namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Service-retained array accounting, distinct from packed payload and total process memory.</summary>
public static class RenderResourceAccounting
{
    /// <summary>Counts owned definition array elements using their actual managed value sizes.</summary>
    /// <param name="definition">Validated immutable CPU data.</param><returns>Array payload bytes; metadata is count bounded separately.</returns>
    public static long ResidentBytes(RenderAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition switch
        {
            MeshDefinition mesh => (long)mesh.Vertices.Length * Unsafe.SizeOf<System.Numerics.Vector3>() +
                (long)mesh.Indices.Length * sizeof(uint) + (long)mesh.Slots.Length * Unsafe.SizeOf<MeshSlot>(),
            MaterialBindingSetDefinition bindings => (long)bindings.Bindings.Length * Unsafe.SizeOf<MaterialSlotBinding>(),
            OpaqueUnlitMaterialDefinition => 0,
            _ => throw new RenderResourceException(RenderResourceError.InvalidIdentity, "Unsupported CPU definition.")
        };
    }
}
