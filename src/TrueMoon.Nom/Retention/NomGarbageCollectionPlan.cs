namespace TrueMoon.Nom.Retention;

/// <summary>An immutable dry-run result. Applying it requires the same catalog and object inventory.</summary>
public sealed class NomGarbageCollectionPlan
{
    /// <summary>Gets the issuing store identity.</summary>
    public Guid StoreId { get; }
    /// <summary>Gets the complete metadata sequence, including retention changes.</summary>
    public long Sequence { get; }
    /// <summary>Gets the canonical plan fingerprint.</summary>
    public string Id { get; }
    /// <summary>Gets the number of unreachable revision records.</summary>
    public int RevisionsToPrune => Revisions.Length;
    /// <summary>Gets the number of unreachable canonical CAS objects.</summary>
    public int ObjectsToDelete => Objects.Length;
    /// <summary>Gets the bytes represented by unreachable CAS files.</summary>
    public long BytesToDelete { get; }
    /// <summary>Gets whether further dry-run/collect slices are needed after this plan completes.</summary>
    public bool HasMore { get; }
    internal string[] Revisions { get; }
    internal (string Digest, long Length)[] Objects { get; }
    internal NomGarbageCollectionPlan(Guid store, long sequence, string id, string[] revisions, (string Digest, long Length)[] objects,bool hasMore)
    { StoreId = store; Sequence = sequence; Id = id; Revisions = revisions; Objects = objects; BytesToDelete = objects.Sum(o => o.Length); HasMore = hasMore; }
}

/// <summary>The acknowledged portion of a GC operation; pending deletion is resumed explicitly.</summary>
/// <param name="RevisionsPruned">Revision records pruned by this call.</param><param name="ObjectsDeleted">Deletion intents acknowledged by this call.</param>
/// <param name="BytesDeleted">Bytes represented by acknowledged intents.</param><param name="CleanupPending">Whether durable deletion intents remain.</param>
public sealed record NomGarbageCollectionReceipt(int RevisionsPruned, int ObjectsDeleted, long BytesDeleted, bool CleanupPending);
