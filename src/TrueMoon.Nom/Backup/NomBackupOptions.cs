namespace TrueMoon.Nom.Backup;

/// <summary>Logical transfer admission limits; these do not reserve physical disk space.</summary>
/// <param name="MaximumFiles">Maximum listed data files, from1to100256.</param>
/// <param name="MaximumBytes">Data plus manifest byte ceiling, from256bytes to1TiB.</param>
public sealed record NomBackupOptions(int MaximumFiles = 100256, long MaximumBytes = 16L * 1024 * 1024 * 1024)
{
    internal NomBackupOptions Snapshot()
    {
        if (MaximumFiles is < 1 or > 100256 || MaximumBytes is < 256 or > 1024L * 1024 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(NomBackupOptions),"Backup admission budget.");
        return this with { };
    }
}

/// <summary>Acknowledged publication of a verified immutable backup directory.</summary>
/// <param name="Root">Published backup directory.</param><param name="ProjectId">Persistent project identity.</param>
/// <param name="StoreId">Persistent store identity.</param><param name="Sequence">Exact metadata sequence.</param>
/// <param name="Generation">Content generation.</param><param name="Files">Listed data files, excluding manifest and empty owner.lock.</param>
/// <param name="Bytes">Data plus manifest bytes.</param><param name="ManifestDigest">SHA256 of the complete manifest.</param>
public sealed record NomBackupReceipt(string Root,Guid ProjectId,Guid StoreId,long Sequence,long Generation,int Files,long Bytes,string ManifestDigest);
