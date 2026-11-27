using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>A named render asset captured for the current inspector selection.</summary>
/// <param name="Id">Persistent asset identity.</param>
/// <param name="Name">Published model and subasset label.</param>
public sealed record InspectorRenderAssetChoice(Guid Id, string Name)
{
    /// <summary>Whether the choice is compatible with the destination slot.</summary>
    public bool CanSelect { get; init; } = true;
}

/// <summary>Current mesh and material choices for one renderer occurrence.</summary>
/// <param name="Component">Selected renderer occurrence.</param>
/// <param name="Meshes">Available published meshes.</param>
/// <param name="Materials">Available materials for the effective mesh.</param>
/// <param name="Mesh">Current draft or committed mesh identity.</param>
/// <param name="Material">Current draft or committed material identity.</param>
/// <param name="Enabled">Whether the current scene admits editing.</param>
public sealed record InspectorRenderAssetSelection(ComponentId Component, IReadOnlyList<InspectorRenderAssetChoice> Meshes,
    IReadOnlyList<InspectorRenderAssetChoice> Materials, Guid? Mesh, Guid? Material, bool Enabled)
{
    /// <summary>Whether the current mesh has a different published default binding available.</summary>
    public bool CanRestoreMeshMaterial { get; init; }

    /// <summary>Destination meshes that admit transfer of all current material overrides.</summary>
    public IReadOnlyList<Guid> MeshesWithMaterialTransfer { get; init; } = [];

    /// <summary>The actual slot ID for a single-slot mesh; such a mesh has no entries in <see cref="Slots"/>.</summary>
    public Guid? SingleSlotId { get; init; }

    /// <summary>Named slots for a mesh with several material ranges.</summary>
    public IReadOnlyList<InspectorRenderSlot> Slots { get; init; } = [];
}

/// <summary>One published slot and its effective material in the inspector.</summary>
/// <param name="Id">Persistent mesh slot identity.</param>
/// <param name="Name">Imported slot label.</param>
/// <param name="Material">Effective material identity, if resolved.</param>
/// <param name="CanSelect">Whether the slot currently admits a material change.</param>
public sealed record InspectorRenderSlot(Guid Id, string Name, Guid? Material, bool CanSelect)
{
    /// <summary>Choices specific to this slot, or null to use the selection's shared choices.</summary>
    public IReadOnlyList<InspectorRenderAssetChoice>? Materials { get; init; }
}
