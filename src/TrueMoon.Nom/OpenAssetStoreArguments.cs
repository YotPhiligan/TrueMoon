namespace TrueMoon.Nom;

/// <summary>Typed arguments for an explicitly opened, caller-disposable project store.</summary>
/// <param name="Root">Fully qualified local filesystem root; UNC/reparse roots are unsupported.</param>
/// <param name="ProjectId">Nonempty persistent project identity.</param><param name="StoreId">Nonempty persistent store identity.</param>
/// <param name="Mode">Create or Open; unspecified discriminator values are rejected.</param>
/// <param name="CancellationToken">Opening cancellation. A failed open remains owned until actual cleanup.</param>
public sealed record OpenAssetStoreArguments(string Root, Guid ProjectId, Guid StoreId, AssetStoreOpenMode Mode,
    CancellationToken CancellationToken = default);
