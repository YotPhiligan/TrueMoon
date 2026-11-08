namespace TrueMoon.Nom.Resources;

/// <summary>Store-issued operation identity. Committed retry replays the outcome without rereading replacement inputs.</summary>
/// <param name="StoreId">Issuing store.</param><param name="Sequence">Never-reused positive sequence.</param>
/// <param name="RequestDigest">Canonical hash of the reserved descriptors and declared budgets.</param>
public sealed record AssetWriteTicket(Guid StoreId, long Sequence, string RequestDigest);
