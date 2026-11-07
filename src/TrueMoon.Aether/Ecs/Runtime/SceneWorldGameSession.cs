using System.Runtime.ExceptionServices;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Ecs.Runtime;

/// <summary>Initial-scene ECS game contract with atomic activation and explicit scene ownership.</summary>
/// <remarks>The runtime serializes calls. The injected loader and its dependencies are borrowed;
/// this session owns the returned scene, including retained failed-cleanup resources. No checkpoint or live replacement is implied.</remarks>
public abstract class SceneWorldGameSession : IGameSession
{
    private readonly SceneWorldLoader _loader;
    private SceneWorld? _scene, _cleanup;
    private IDisposable? _sceneResources;
    private bool _busy, _disposed, _gameReleased, _disposing;

    /// <summary>Creates a session without loading a scene.</summary><param name="loader">Borrowed registry-specific factory.</param>
    protected SceneWorldGameSession(SceneWorldLoader loader)
    { ArgumentNullException.ThrowIfNull(loader); _loader = loader; }

    /// <summary>Gets the exact process-local registry generation advertised during Hello.</summary>
    public Guid RegistryId => _loader.RegistryId;
    /// <summary>Gets the borrowed active scene, or null before complete activation. Dispose the session, not this scene.</summary>
    public SceneWorld? ActiveScene => _scene;
    /// <summary>Gets the last completed fixed tick.</summary>
    public ulong TickNumber { get; private set; }
    /// <summary>Gets the active authoring revision, or -1 before activation.</summary>
    public long Revision => _scene?.Scene.Revision ?? -1;
    /// <summary>Gets whether a failed tick made the active World terminally faulted. Inspection and disposal remain available.</summary>
    public bool IsFaulted => _scene?.World.IsFaulted == true;
    /// <summary>Gets initial resolved component cardinality; runtime mutations do not rewrite source metadata.</summary>
    public int SceneComponentCount { get; private set; }
    /// <summary>Gets the game's CPU frame pattern color.</summary>
    public abstract uint PatternColor { get; }

    /// <summary>Rejects the legacy primitive projection, which cannot carry complete ECS data.</summary>
    /// <param name="scene">Legacy projection.</param>
    public void Load(SceneSnapshot scene) => throw new NotSupportedException("This game requires the full scene World capability.");

    /// <summary>Prepares a private candidate and publishes it only after all validation and cancellation checks succeed.</summary>
    /// <param name="data">Complete neutral source.</param><param name="cancellationToken">Cooperative preparation cancellation.</param>
    public void LoadSceneWorld(SceneWorldData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data); Enter();
        SceneWorld? candidate = null;
        try
        {
            if (_scene != null || _cleanup != null) throw new InvalidOperationException("A fresh session with no retained cleanup is required.");
            cancellationToken.ThrowIfCancellationRequested();
            candidate = _loader.Create(new(data, cancellationToken));
            var components = checked(candidate.Scene.Entities.Values.Sum(entity => entity.Components.Count));
            _sceneResources = PrepareSceneWorld(candidate, cancellationToken);
            if (!candidate.IsReady || candidate.World.IsFaulted) throw new InvalidOperationException("Preparation must retain a healthy ready candidate.");
            cancellationToken.ThrowIfCancellationRequested();
            // No user callback or fallible work after this publication point.
            SceneComponentCount = components; _scene = candidate; candidate = null;
        }
        catch (Exception error)
        {
            if (error is SceneWorldLoadException { CleanupOwner: { } retained }) _cleanup = retained;
            if (error is SceneWorldPreparationException preparation) _sceneResources = preparation.CleanupOwner;
            if (candidate != null)
            {
                try { _sceneResources?.Dispose(); _sceneResources = null; candidate.Dispose(); }
                catch (Exception cleanup) { _cleanup = candidate; throw new AggregateException(error, cleanup); }
            }
            ExceptionDispatchInfo.Capture(error).Throw(); throw;
        }
        finally { _busy = false; }
    }

    /// <summary>Runs one caller-driven fixed tick on the active World.</summary>
    public void Tick()
    {
        Enter();
        try
        {
            var scene = _scene ?? throw new InvalidOperationException("No scene World is active.");
            if (scene.World.IsFaulted) throw new InvalidOperationException("The scene World is faulted; dispose and recreate the session.");
            try { var next = checked(TickNumber + 1); TickWorld(scene, next); TickNumber = next; }
            catch (Exception error) { scene.World.MarkFaulted(error); throw; }
        }
        finally { _busy = false; }
    }
    /// <summary>Updates the borrowed active scene. Do not retain ownership or dispose it.</summary>
    /// <param name="scene">Active scene.</param><param name="nextTick">The tick being executed.</param>
    protected abstract void TickWorld(SceneWorld scene, ulong nextTick);

    /// <summary>Prepares derived data and owned systems on a private candidate before game activation.</summary>
    /// <remarks>The returned resource becomes owned before the final cancellation check and is disposed before the scene.
    /// A failed factory must clean itself up or throw SceneWorldPreparationException with its retained owner.</remarks>
    /// <param name="scene">Private borrowed candidate; ActiveScene is still null.</param><param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>Exclusively owned controller/graph, or null when none is required.</returns>
    protected virtual IDisposable? PrepareSceneWorld(SceneWorld scene, CancellationToken cancellationToken) => null;

    /// <summary>Releases the active scene or retained failed preparation. Failed disposals remain owned for retry.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Enter(disposing: true); _disposing = true;
        try
        {
            if (!_gameReleased) { DisposeGameResources(); _gameReleased = true; }
            _sceneResources?.Dispose(); _sceneResources = null;
            _scene?.Dispose(); _scene = null;
            _cleanup?.Dispose(); _cleanup = null;
            _disposed = true;
        }
        finally { _busy = false; }
    }
    /// <summary>Releases derived owned systems before their borrowed World. Failed cleanup is retried; successful cleanup is not repeated.</summary>
    protected virtual void DisposeGameResources() { }
    private void Enter(bool disposing = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_disposing && !disposing) throw new InvalidOperationException("Scene session cleanup has started; only disposal retry is allowed.");
        if (_busy) throw new InvalidOperationException("Scene session operations are not reentrant.");
        _busy = true;
    }
}
