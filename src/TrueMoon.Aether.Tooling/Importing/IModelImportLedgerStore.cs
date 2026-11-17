namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Complete candidate+ledger persistence. Implementations must serialize cooperating writers and commit all fields together.</summary>
public interface IModelImportLedgerStore
{
    /// <summary>Reads one immutable generation, or null if absent. Never creates or migrates files.</summary>
    /// <param name="path">Absolute sidecar path.</param><param name="sourceId">Expected persistent source identity.</param><param name="cancellationToken">Read cancellation.</param><returns>A complete generation or null.</returns>
    Task<PublishedModelImport?> ReadAsync(string path,Guid sourceId,CancellationToken cancellationToken=default);
    /// <summary>Checks source/generation, prepares IDs and atomically publishes candidate plus ledger. Cancellation after commit does not roll it back.</summary>
    /// <param name="path">Absolute sidecar path.</param><param name="sourceId">Expected source identity.</param><param name="expectedGeneration">Zero for absent, otherwise exact current generation.</param>
    /// <param name="candidate">Complete CPU candidate.</param><param name="edits">Explicit disambiguation/rename/restore decisions.</param><param name="cancellationToken">Cancellation before commit.</param><returns>The committed generation.</returns>
    Task<PublishedModelImport> PublishAsync(string path,Guid sourceId,long expectedGeneration,ModelImportCandidate candidate,ModelImportIdentityEdits? edits=null,CancellationToken cancellationToken=default);
}
