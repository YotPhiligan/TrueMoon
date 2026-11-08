namespace TrueMoon.Nom.Resources;

/// <summary>Committed publication outcome. Mutating the detached result array cannot change the catalog.</summary>
/// <param name="Generation">Committed content generation.</param><param name="Resources">Exact published versions.</param>
/// <param name="CleanupPending">Publication succeeded but staging cleanup retains the single write reservation.
/// Replay this ticket to retry cleanup, or stop/reopen after fixing the IO failure.</param>
public sealed record AssetWriteReceipt(long Generation, AssetResourceVersion[] Resources, bool CleanupPending = false);
