namespace TrueMoon.Argentis;

/// <summary>The host's exclusive borrowed connection to a Window. It owns no native, UI-tree or controller resources.</summary>
public sealed class WindowHostConnection : IDisposable
{
    private readonly Window _window;
    internal IWindowController Controller { get; }
    internal WindowHostConnection(Window window, IWindowController controller) { _window = window; Controller = controller; }
    /// <summary>Checks UI-owner access and rejects a disconnected or superseded connection before host side effects.</summary>
    public void VerifyAccess() => _window.VerifyConnection(this);
    /// <summary>Publishes actual native state on the UI owner without changing requested properties or rebuilding content.</summary>
    /// <param name="state">Observed presentation state.</param><param name="active">Observed native focus.</param>
    /// <param name="clientSize">Actual logical client area; zero is allowed during suspension.</param>
    public void Synchronize(WindowPresentationState state, bool active, Size clientSize) => _window.Synchronize(this, state, active, clientSize);
    /// <summary>Copies close handlers at the start of one coordinated attempt. The host executes them in order.</summary>
    /// <returns>A fresh snapshot. User attempts stop on veto/error; Shutdown drains every callback and preserves errors.</returns>
    public Func<WindowCloseReason, Task<bool>>[] SnapshotClosingHandlers() => _window.SnapshotClosingHandlers(this);
    /// <summary>Disconnects on the UI owner. Repeated disposal is harmless; borrowed resources remain live.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (!_window.IsDisposed) _window.VerifyAccess();
        _disposed = true; _window.Disconnect(this);
    }
    private bool _disposed;
}
