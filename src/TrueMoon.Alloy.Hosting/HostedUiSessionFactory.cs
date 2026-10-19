using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Hosting;

/// <summary>App-scoped registry of sessions. Stop on their owner thread after returning all GPU leases.</summary>
public sealed class HostedUiSessionFactory : IUiWindowSessionFactory, IStoppable, IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly HashSet<UiSession> _sessions = [];
    private readonly object _gate = new();
    private bool _stopRequested, _disposing;
    private int _creating;
    /// <summary>Number of sessions still owned by this registry; safe to inspect from any thread.</summary>
    public int ActiveSessionCount { get { lock (_gate) return _sessions.Count; } }
    /// <summary>Built-in renderer compatibility, or Custom when the backend owns its target contract.</summary>
    public UiRendererKind Renderer { get; }
    /// <summary>Creates a registry without allocating a graphics context.</summary>
    public HostedUiSessionFactory(IRenderBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Renderer = UiRendererCompatibility.Identify(backend);
    }
    /// <inheritdoc />
    public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => UiRendererCompatibility.SupportsWindow(Renderer, graphicsApi);
    /// <inheritdoc />
    public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopRequested, this);
            _creating++;
            try
            {
                var session = UiSession.Create(root, _backend, target, viewport, clipboard);
                session.Disposed += Removed;
                _sessions.Add(session);
                return session;
            }
            finally { _creating--; }
        }
    }
    private void Removed(UiSession session) { lock (_gate) _sessions.Remove(session); }
    /// <summary>Requests shutdown and disposes sessions on their owner thread.</summary>
    /// <remarks>New sessions remain rejected after shutdown is requested. If a guard prevents
    /// disposal, return borrowed frames/leave Update and retry StopAsync or Dispose on the owner.
    /// Each attempt reports its errors and retains sessions that have not completed disposal.</remarks>
    /// <param name="cancellationToken">Does not interrupt synchronous owner-thread cleanup.</param>
    /// <returns>A completed task after cleanup succeeds; failures are thrown synchronously.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default) { Dispose(); return Task.CompletedTask; }
    /// <inheritdoc />
    /// <remarks>Failed attempts remain retryable. Reentrant cleanup calls return to the outer attempt.
    /// Shutdown from a session-construction callback is rejected before accepting the request.</remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_creating != 0) throw new InvalidOperationException("Cannot stop the registry during session creation.");
            _stopRequested = true;
            if (_disposing) return; // The outer owner-thread attempt continues its snapshot.
            _disposing = true;
            try
            {
                var sessions = _sessions.ToArray();
                // Validate before changing any session. Never destroy a borrowed/in-flight
                // frame or dispose a session on a foreign thread; failures remain retryable.
                foreach (var session in sessions) { session.VerifyAccess(); session.Rendering.VerifyAvailable(); }
                UiCleanup.Complete(null, sessions.Select(session => (Action)session.Dispose).ToArray());
            }
            finally { _disposing = false; }
        }
    }
}
