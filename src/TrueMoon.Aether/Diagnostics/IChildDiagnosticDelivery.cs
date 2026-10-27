namespace TrueMoon.Aether.Diagnostics;

/// <summary>Experimental same-build diagnostic lane, independent of engine control.</summary>
public interface IChildDiagnosticDelivery
{
    /// <summary>Offers one bounded packet. A failed or missing acknowledgement is never replayed.</summary>
    /// <param name="packet">Versioned identity and JSON snapshots.</param><param name="cancellationToken">Local wait cancellation.</param>
    /// <returns>Per-packet admission summary.</returns>
    Task<DiagnosticDeliveryReply> PushAsync(DiagnosticDeliveryPacket packet, CancellationToken cancellationToken = default);
}

internal static class DiagnosticGeneratorDiscovery
{
    internal static void Register() => UseInvocationService<IChildDiagnosticDelivery>();
    private static void UseInvocationService<T>() { }
}
