namespace TrueMoon.Aether.Diagnostics;

/// <summary>Independent numeric health; lost and unconfirmed are different delivery outcomes.</summary>
/// <param name="Enabled">Whether the lane has its required local dependencies.</param><param name="Closed">Admission closed.</param>
/// <param name="Released">Owned transport actually released.</param><param name="DrainComplete">Captured drain target completed before cancellation.</param>
/// <param name="Accepted">Records acknowledged/imported.</param><param name="Rejected">Records refused by the receiver/importer.</param>
/// <param name="Lost">Known unavailable or locally dropped records.</param><param name="Unconfirmed">Records with unknown remote outcome.</param>
/// <param name="Failures">Local exchange/import failures.</param><param name="Duplicates">Repeated packets/record sequences.</param>
/// <param name="RateLimited">Records dropped by rate budgets, included in Lost or Rejected.</param>
/// <param name="Suppressed">Already-imported records excluded from forwarding.</param>
/// <param name="ChildReportedLost">Latest cumulative loss reported by the child.</param>
/// <param name="ChildReportedUnconfirmed">Latest cumulative unknown outcomes reported by the child.</param>
/// <param name="LastFailureType">Bounded type name only; no endpoint/nonce/error text.</param>
public sealed record ChildDiagnosticStatus(bool Enabled, bool Closed, bool Released, bool DrainComplete,
    long Accepted, long Rejected, long Lost, long Unconfirmed, long Failures, long Duplicates, long RateLimited,
    long Suppressed, long ChildReportedLost, long ChildReportedUnconfirmed, string? LastFailureType);
