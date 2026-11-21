namespace TrueMoon.Nom.Backup;

/// <summary>Explicit verified restore into an absent local root, preserving identities and exact retained state.</summary>
/// <param name="BackupRoot">Immutable exported directory.</param><param name="Root">Absent destination with an existing parent.</param>
/// <param name="ProjectId">Expected persistent project identity.</param><param name="StoreId">Expected persistent store identity.</param>
/// <param name="Options">Optional copied transfer limits.</param><param name="CancellationToken">Restore/opening cancellation.</param>
public sealed record RestoreAssetStoreArguments(string BackupRoot,string Root,Guid ProjectId,Guid StoreId,
    NomBackupOptions? Options = null,CancellationToken CancellationToken = default);
