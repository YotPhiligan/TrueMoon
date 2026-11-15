using System.Collections.Immutable;

namespace TrueMoon.Aether.Runtime.Ecs.Runtime;

/// <summary>Session-owned prepared subsystem resources, released after the scheduler and before the borrowed scene.</summary>
/// <remarks>Serialized caller access is required. Dispose failures retain the owner for retry. This does not migrate
/// native state during mod reload or provide rollback for external side effects.</remarks>
public sealed class SceneSubsystemSession : IDisposable
{
    private readonly List<IDisposable> _owners = [];
    private readonly List<ISceneSubsystem> _subsystems = [];
    private readonly World _world;
    private bool _playing, _busy, _disposing, _disposed, _faulted;
    private SceneSubsystemSession(World world) => _world = world;
    /// <summary>Gets the copied registrations. Their system instances belong to the consumer scheduler.</summary>
    public ImmutableArray<EcsSystemRegistration> Systems { get; private set; } = [];
    internal static SceneSubsystemSession Create(SceneSubsystemArguments arguments, IFactory<ISceneSubsystem, SceneSubsystemArguments>[] factories)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Scene);
        if (arguments.FixedStep <= TimeSpan.Zero || arguments.FixedStep > TimeSpan.FromSeconds(1)) throw new ArgumentOutOfRangeException(nameof(arguments));
        if (!arguments.Scene.IsReady || arguments.Scene.World.IsFaulted || arguments.Scene.IsDisposed) throw new InvalidOperationException("A healthy private scene is required.");
        arguments.CancellationToken.ThrowIfCancellationRequested();
        var owner = new SceneSubsystemSession(arguments.Scene.World);
        owner._world.ClaimSceneSubsystems(owner);
        try
        {
            var systems = ImmutableArray.CreateBuilder<EcsSystemRegistration>();
            foreach (var factory in factories)
            {
                arguments.CancellationToken.ThrowIfCancellationRequested();
                ISceneSubsystem subsystem;
                try { subsystem = factory.Create(arguments) ?? throw new InvalidOperationException("A subsystem factory returned null."); }
                catch (SceneWorldPreparationException error)
                {
                    if (!owner._owners.Contains(error.CleanupOwner, ReferenceEqualityComparer.Instance)) owner._owners.Add(error.CleanupOwner);
                    throw;
                }
                if (owner._owners.Contains(subsystem, ReferenceEqualityComparer.Instance)) throw new InvalidOperationException("Subsystem factory results require exclusive ownership.");
                owner._owners.Add(subsystem); owner._subsystems.Add(subsystem);
                var registrations = subsystem.Systems ?? throw new InvalidOperationException("Subsystem systems cannot be null.");
                // Bound enumeration as well as declared Count: implementations need not be immutable collections.
                foreach (var registration in registrations)
                {
                    if (systems.Count == 1024) throw new InvalidOperationException("Subsystem system budget exhausted.");
                    systems.Add(registration ?? throw new InvalidOperationException("A subsystem system registration is null."));
                }
                subsystem.SetPlaying(false);
            }
            arguments.CancellationToken.ThrowIfCancellationRequested();
            owner.Systems = systems.ToImmutable(); owner._world.CompleteSceneSubsystemPreparation(owner); return owner;
        }
        catch (Exception error)
        {
            try { owner.Dispose(); }
            catch (Exception cleanup) { throw new SceneWorldPreparationException(new AggregateException(error, cleanup), owner); }
            throw;
        }
    }
    /// <summary>Notifies resources of actual gameplay mode. Failure faults the World and prevents resuming this session.</summary>
    /// <param name="playing">Whether continuous gameplay is allowed.</param>
    public void SetPlaying(bool playing)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_busy || _disposing || playing && (_faulted || _world.IsFaulted || _world.IsDisposed)) throw new InvalidOperationException("Subsystem session cannot change playback.");
        if (_playing == playing && !_faulted) return;
        _busy = true;
        try
        {
            _playing = false;
            try { foreach (var subsystem in _subsystems) subsystem.SetPlaying(playing); _playing = playing; }
            catch (Exception error)
            {
                _faulted = true; _world.MarkFaulted(error);
                var errors = new List<Exception> { error };
                foreach (var subsystem in _subsystems) try { subsystem.SetPlaying(false); } catch (Exception pause) { errors.Add(pause); }
                if (errors.Count > 1) throw new AggregateException(errors);
                throw;
            }
        }
        finally { _busy = false; }
    }
    /// <summary>Pauses and releases resources in reverse preparation order. Successful releases are not repeated.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_busy) throw new InvalidOperationException("Leave subsystem callbacks before cleanup.");
        _world.CheckSceneSubsystemCleanup();
        _disposing = true; _busy = true;
        try
        {
            for (var i = _owners.Count - 1; i >= 0; i--)
            {
                var resource = _owners[i];
                // Stop at a failed release: earlier dependencies must remain alive for retry.
                if (resource is ISceneSubsystem subsystem) subsystem.SetPlaying(false);
                resource.Dispose(); _owners.RemoveAt(i);
                if (resource is ISceneSubsystem released) _subsystems.Remove(released);
            }
            _world.ReleaseSceneSubsystems(this);
            _subsystems.Clear(); Systems = []; _playing = false; _disposed = true;
        }
        finally { _busy = false; }
    }
}
