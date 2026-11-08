namespace TrueMoon.Nom.Storage;

/// <summary>Maintenance failed after preparation started. Close/reopen before using the store again.</summary>
/// <param name="rootPublicationAttempted">Root replacement was invoked, so its outcome must be discovered by authoritative reopen.</param>
/// <param name="cause">Original IO or verification failure.</param>
public sealed class NomCompactionException(bool rootPublicationAttempted, Exception cause)
    : IOException(rootPublicationAttempted ? "Compaction root outcome requires close/reopen." : "Compaction preparation failed; close/reopen required.", cause)
{
    /// <summary>Whether root replacement was invoked; failure does not prove which epoch is selected.</summary>
    public bool RootPublicationAttempted { get; } = rootPublicationAttempted;
}
