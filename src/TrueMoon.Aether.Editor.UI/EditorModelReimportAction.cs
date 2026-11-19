namespace TrueMoon.Aether.Editor.UI;

/// <summary>Presentation and exact target for reimporting an already selected model.</summary>
/// <param name="OpeningId">Displayed document opening.</param>
/// <param name="SourceId">Selected model source.</param>
/// <param name="Generation">Displayed ledger generation.</param>
/// <param name="Name">Model label for the action.</param>
/// <param name="Enabled">Current command admission as observed by the UI.</param>
public sealed record EditorModelReimportAction(Guid OpeningId, Guid SourceId, long Generation, string Name, bool Enabled);
