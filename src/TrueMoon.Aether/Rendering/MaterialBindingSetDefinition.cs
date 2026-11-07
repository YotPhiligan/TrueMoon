using System.Collections.Immutable;

namespace TrueMoon.Aether.Rendering;

/// <summary>One stable mesh slot and its exact material revision.</summary>
/// <param name="SlotId">Persistent mesh slot.</param><param name="Material">Same-project opaque material key.</param>
public readonly record struct MaterialSlotBinding(Guid SlotId, RenderAssetKey Material);

/// <summary>Immutable bindings for one exact mesh revision; slot compatibility is checked against that mesh.</summary>
public sealed class MaterialBindingSetDefinition : RenderAssetDefinition
{
    /// <summary>Copies a bounded nonempty set without resolving services or acquiring leases.</summary>
    /// <param name="mesh">Exact mesh key.</param><param name="bindings">Distinct ordered slot/material pairs.</param><param name="schemaVersion">Exact version one.</param>
    public MaterialBindingSetDefinition(RenderAssetKey mesh, IReadOnlyList<MaterialSlotBinding> bindings, int schemaVersion = 1)
        : base(RenderContentProfile.BindingSetKindId, schemaVersion)
    {
        mesh.Validate(RenderContentProfile.MeshKindId);
        ArgumentNullException.ThrowIfNull(bindings);
        ContentValidation.Require(bindings.Count > 0, RenderContentError.InvalidBinding, "Bindings must be nonempty.");
        Mesh = mesh; Bindings = ContentValidation.Copy(bindings, RenderContentProfile.MaximumSlots, nameof(bindings));
        var ids = new HashSet<Guid>();
        foreach (var binding in Bindings)
        {
            ContentValidation.Require(binding.SlotId != Guid.Empty && ids.Add(binding.SlotId), RenderContentError.InvalidBinding, "Empty or duplicate material slot.");
            binding.Material.Validate(RenderContentProfile.MaterialKindId);
            ContentValidation.Require(binding.Material.ProjectId == mesh.ProjectId, RenderContentError.InvalidIdentity, "Bindings cannot cross projects.");
        }
        Complete();
    }
    /// <summary>Gets the exact mesh identity and revision.</summary>
    public RenderAssetKey Mesh { get; }
    /// <summary>Gets the detached immutable slot bindings.</summary>
    public ImmutableArray<MaterialSlotBinding> Bindings { get; }
    /// <summary>Rejects revision mismatch, missing/extra slots and different ordering.</summary>
    /// <param name="meshKey">Key resolving to the supplied geometry.</param><param name="mesh">Validated geometry.</param>
    public void ValidateAgainst(RenderAssetKey meshKey, MeshDefinition mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh); meshKey.Validate(RenderContentProfile.MeshKindId);
        ContentValidation.Require(meshKey == Mesh && meshKey.Revision == mesh.Revision && meshKey.SchemaVersion == mesh.SchemaVersion,
            RenderContentError.RevisionMismatch, "Bindings require their exact mesh revision.");
        ContentValidation.Require(mesh.Slots.Length == Bindings.Length, RenderContentError.InvalidBinding, "Bindings must cover the exact mesh slots.");
        for (var i = 0; i < Bindings.Length; i++)
            ContentValidation.Require(Bindings[i].SlotId == mesh.Slots[i].SlotId, RenderContentError.InvalidBinding, "Binding slots must exactly match mesh order.");
    }
}
