namespace TrueMoon.Aether.Editor.UI;

/// <summary>Detached catalog row and its selection status for the displayed opening.</summary>
/// <param name="OpeningId">Exact scene opening, or empty when only browsing.</param>
/// <param name="SourceId">Persistent model source identity.</param>
/// <param name="Name">Display label.</param>
/// <param name="SourcePath">Project-relative root file, when registered.</param>
/// <param name="Selected">Whether the model belongs to the current scene selection.</param>
/// <param name="CanChange">Current scene selection admission.</param>
public sealed record EditorProjectModelRow(Guid OpeningId, Guid SourceId, string Name, string SourcePath, bool Selected, bool CanChange);
