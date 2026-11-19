using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>A named render asset captured for the current inspector selection.</summary>
/// <param name="Id">Persistent asset identity.</param>
/// <param name="Name">Published model and subasset label.</param>
public sealed record InspectorRenderAssetChoice(Guid Id, string Name);

/// <summary>Current mesh and single-slot material choices for one renderer occurrence.</summary>
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
}
