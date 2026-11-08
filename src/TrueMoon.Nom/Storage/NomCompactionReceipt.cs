namespace TrueMoon.Nom.Storage;

/// <summary>Detached metadata maintenance result. Content generation and retained resource operations are unchanged.</summary>
/// <param name="Epoch">Selected root epoch.</param><param name="Sequence">Covered physical KV sequence.</param>
/// <param name="Generation">Unchanged content generation.</param><param name="Keys">Number of live catalog keys.</param>
/// <param name="CheckpointBytes">Verified checkpoint size, or zero without a checkpoint.</param>
/// <param name="CleanupPending">Obsolete files could not yet be closed/deleted; retry maintenance or dispose.</param>
/// <param name="Changed">Whether this attempt published a new checkpoint/root.</param>
public sealed record NomCompactionReceipt(long Epoch, long Sequence, long Generation, int Keys,
    long CheckpointBytes, bool CleanupPending, bool Changed);
