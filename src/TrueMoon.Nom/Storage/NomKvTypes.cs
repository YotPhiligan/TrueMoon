using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Storage;

internal sealed record NomKvOpenArguments(string Root, Guid ProjectId, Guid StoreId,
    NomFormatLimits? Limits = null, CancellationToken Token = default, bool RepairIncompleteTail = true, bool AllowBackupRoot = false);

internal sealed record NomKvWriteTicket(Guid ProjectId, Guid StoreId, long Sequence,
    Guid OperationId, string FrameDigest);
internal sealed record NomKvWriteReceipt(NomKvWriteTicket Ticket, long Generation);
internal enum NomKvWriteOutcome { NotCommitted, Committed, Expired }
internal sealed record NomKvReadResult(long Sequence, long Generation, IReadOnlyList<byte[]?> Values);
internal sealed record NomKvPair(byte[] Key, byte[] Value);
internal sealed record NomKvPage(long Sequence, long Generation, IReadOnlyList<NomKvPair> Items, bool HasMore);
internal sealed record NomKvAppendBudget(int Batches, long PayloadBytes, long MaximumBatchPayload,
    int MaximumBatchMutations, int MaximumKeyBytes, int MaximumValueBytes, int AdditionalKeys, long AdditionalIndexBytes);

internal sealed class NomKvUnknownOutcomeException(NomKvWriteTicket ticket, Exception cause)
    : IOException("Nom KV write outcome requires close/reopen and ticket lookup.", cause)
{
    internal NomKvWriteTicket Ticket { get; } = ticket;
}
