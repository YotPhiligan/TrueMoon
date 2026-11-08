namespace TrueMoon.Nom.Resources;

/// <summary>Close/reopen the store and look up the ticket before deciding whether publication succeeded.</summary>
/// <param name="ticket">Operation whose acknowledgement was lost.</param><param name="inner">Underlying failure.</param>
public sealed class UnknownOutcomeException(AssetWriteTicket ticket, Exception? inner = null)
    : IOException("Commit outcome requires store close/reopen and journal lookup.", inner)
{
    /// <summary>Gets the operation to resolve after reopening.</summary>
    public AssetWriteTicket Ticket { get; } = ticket;
}
