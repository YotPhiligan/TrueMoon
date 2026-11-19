using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Tooling.Documents;

/// <summary>Advisory source status of one selected import in the active scene opening.</summary>
/// <param name="SourceId">Persistent model identity.</param>
/// <param name="Name">Caller-supplied model label.</param>
/// <param name="Status">Latest comparison, or null while a watch is awaiting its first result.</param>
public sealed record SceneModelImportStatus(Guid SourceId, string Name, ModelImportFreshnessStatus? Status)
{
    /// <summary>Whether the selected import has authoring setup for reimport.</summary>
    public bool CanReimport { get; init; }

    /// <summary>Exact ledger generation bound to the displayed selection.</summary>
    public long Generation { get; init; }
}

/// <summary>One explicit source and published generation contributing resources to the scene build.</summary>
/// <param name="Name">Model label displayed in Editor and build diagnostics.</param>
/// <param name="Watch">Source and published generation to compare.</param>
public sealed record SceneModelImportSelection(string Name, ModelSourceWatchArguments Watch)
{
    /// <summary>Optional worker, ledger and content publication setup for this model.</summary>
    public SceneModelReimportSettings? Reimport { get; init; }
    internal ModelImportLedgerArguments? SavedLedger { get; init; }
}
