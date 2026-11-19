using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;

namespace TrueMoon.Aether.Tooling.Documents;

/// <summary>Inputs for restoring App-provided reimport settings for a saved scene binding.</summary>
/// <param name="Project">Resolved project owning the scene and resources.</param>
/// <param name="Ledger">Portable saved ledger reference resolved in this project.</param>
public sealed record SceneModelReimportRestoreArguments(ResolvedProject Project, ModelImportLedgerArguments Ledger);
