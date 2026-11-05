namespace TrueMoon.Argentis;

/// <summary>Borrowed window actions supplied by a host; contains no platform handles, renderer or event loop.</summary>
public interface IWindowController
{
    /// <summary>Requests minimize on the UI owner.</summary>
    void Minimize();
    /// <summary>Requests maximize on the UI owner.</summary>
    void Maximize();
    /// <summary>Requests restore on the UI owner.</summary>
    void Restore();
    /// <summary>Requests close, preserving host veto, drain and cleanup semantics.</summary>
    /// <param name="cancellationToken">Cancels this wait, not an already accepted close attempt.</param>
    /// <returns>False for a veto, true after successful cleanup; failures fault the task.</returns>
    Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default);
}
