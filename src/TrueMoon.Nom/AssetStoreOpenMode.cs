namespace TrueMoon.Nom;

/// <summary>Explicit catalog creation policy; Open never creates a replacement store.</summary>
public enum AssetStoreOpenMode
{
    /// <summary>Create a new catalog in an otherwise empty local root.</summary>
    Create,
    /// <summary>Open an existing supported catalog with matching project/store identity.</summary>
    Open
}
