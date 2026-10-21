namespace TrueMoon.Aether.Transport;

// Experimental engine contracts. Mutable wire DTOs are decoded into validated snapshots
// before application; no transport, process, UI or graphics types cross this boundary.
public interface IEngineControl
{
    Task<ControlReply> SendAsync(ControlRequest request, CancellationToken cancellationToken = default);
}
