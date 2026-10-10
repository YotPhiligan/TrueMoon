using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

/// <summary>App-scoped registry of sessions. Stop on their owner thread after returning all GPU leases.</summary>
public sealed class HostedUiSessionFactory : IUiSessionFactory, IStoppable, IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly HashSet<UiSession> _sessions = [];
    private readonly object _gate = new();
    private bool _stopped;
    /// <summary>Number of sessions still owned by this registry; safe to inspect from any thread.</summary>
    public int ActiveSessionCount { get { lock (_gate) return _sessions.Count; } }
    /// <summary>Creates a registry without allocating a graphics context.</summary>
    public HostedUiSessionFactory(IRenderBackend backend) => _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    /// <inheritdoc />
    public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopped, this);
            var session = UiSession.Create(root, _backend, target, viewport, clipboard);
            session.Disposed += Removed;
            _sessions.Add(session);
            return session;
        }
    }
    private void Removed(UiSession session) { lock (_gate) _sessions.Remove(session); }
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) { Dispose(); return Task.CompletedTask; }
    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopped) return;
            // Validate before changing any session: a game must close sessions on its frame thread
            // before stopping App from another thread. Never destroy a borrowed/in-flight frame.
            foreach (var session in _sessions) { session.VerifyAccess(); session.Rendering.VerifyAvailable(); }
            _stopped = true;
            UiCleanup.Complete(null, _sessions.ToArray().Select(session => (Action)session.Dispose).ToArray());
        }
    }
}
