namespace TrueMoon.Kton.Bepu;

/// <summary>Factory-local ownership counters. Successful factory products remain caller-owned, not retained by the factory.</summary>
/// <param name="ActiveResourceOwners">World resource owners not yet completely released, including failed creation candidates.</param>
/// <param name="PublishedWorlds">Caller-owned worlds whose cleanup has not completed.</param>
/// <param name="PendingFailedCreationCleanup">Whether the factory retains one failed candidate for cleanup retry.</param>
/// <param name="CompletedCleanups">Completed resource-owner cleanup count.</param>
/// <param name="MaximumSampledPoolBytes">Largest sampled pool allocation in a single world, not a hard RAM cap.</param>
/// <param name="MaximumPoolBytesAfterCleanup">Largest actual remaining BEPU pool counter after a reported successful cleanup.</param>
/// <param name="PoolBudgetExceededOwners">Number of distinct resource owners that exceeded their configured diagnostic pool threshold; never a hard allocation failure.</param>
public readonly record struct BepuResourceSnapshot(int ActiveResourceOwners, int PublishedWorlds, bool PendingFailedCreationCleanup,
    int CompletedCleanups, long MaximumSampledPoolBytes, long MaximumPoolBytesAfterCleanup, int PoolBudgetExceededOwners);
