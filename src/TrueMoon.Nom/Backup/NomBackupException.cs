namespace TrueMoon.Nom.Backup;

/// <summary>A failed backup/restore. The prepared directory is retained; publication may have completed after a lost acknowledgement.</summary>
public sealed class NomBackupException : IOException
{
    /// <summary>Gets the uniquely named prepared directory; it is never recursively deleted by Nom.</summary>
    public string StagingRoot { get; }
    /// <summary>Whether namespace publication was attempted. Inspect the final destination before retrying.</summary>
    public bool PublicationAttempted { get; }
    internal NomBackupException(string stagingRoot,bool publicationAttempted,Exception cause)
        : base("Nom backup transfer failed; the prepared directory is retained and the destination is never reset.",cause)
    { StagingRoot=stagingRoot; PublicationAttempted=publicationAttempted; }
}
