namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>Finite limits for one caller-owned authoring session. Configuration is copied by its factory.</summary>
public sealed class SceneAuthoringOptions
{
    /// <summary>Gets or sets the combined Undo/Redo transaction limit; 0 disables history. Default 100, maximum 1024.</summary>
    public int MaxHistoryEntries { get; set; } = 100;
    /// <summary>Gets or sets edits per atomic batch. Default 64, allowed 1..4096.</summary>
    public int MaxBatchEdits { get; set; } = 64;
    /// <summary>Gets or sets retained terminal operation receipts. Default 4096, allowed 1..65536.
    /// Receipts are never evicted: at capacity new IDs are rejected until a new session is opened.</summary>
    public int MaxOperations { get; set; } = 4096;

    internal SceneAuthoringOptions Snapshot()
    {
        var copy = (SceneAuthoringOptions)MemberwiseClone();
        if (copy.MaxHistoryEntries is < 0 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaxHistoryEntries));
        if (copy.MaxBatchEdits is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaxBatchEdits));
        if (copy.MaxOperations is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(MaxOperations));
        return copy;
    }
}
