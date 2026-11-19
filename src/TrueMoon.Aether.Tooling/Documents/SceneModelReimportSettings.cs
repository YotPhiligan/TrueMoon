using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Tooling.Documents;

/// <summary>Explicit authoring setup for reimporting one model in the open scene.</summary>
/// <param name="Ledger">Existing caller-owned ledger sidecar.</param>
/// <param name="Worker">Trusted model import worker deployment.</param>
/// <param name="ScratchDirectory">Absolute scratch parent for worker operations.</param>
/// <param name="Publisher">Borrowed project content publisher, kept alive by the caller.</param>
/// <param name="Deadline">Optional worker execution deadline.</param>
public sealed record SceneModelReimportSettings(ModelImportLedgerArguments Ledger, ModelImportWorkerDeployment Worker,
    string ScratchDirectory, IModelImportContentPublisher Publisher, TimeSpan? Deadline = null);
