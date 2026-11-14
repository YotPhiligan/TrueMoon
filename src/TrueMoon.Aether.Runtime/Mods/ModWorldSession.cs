using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Group-owned private World/scheduler. Calls are serialized with group cleanup.</summary>
/// <remarks>This does not attach module CLR types to an existing game World or perform hot replacement.</remarks>
public sealed class ModWorldSession
{
    private ModGroup? _group;
    private IFactory<SceneWorldLoader, ComponentRegistry>? _loaders;
    private SceneWorld? _scene;
    private EcsScheduler? _scheduler;
    internal ModWorldSession(IFactory<SceneWorldLoader, ComponentRegistry> loaders) => _loaders = loaders;
    /// <summary>Whether every owned system and scene resource has been released.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Number of live entities, while the group is open.</summary>
    public int EntityCount => Owner.Invoke(() => _scene!.World.EntityCount);
    /// <summary>Executes one positive fixed step without resolving dependencies.</summary>
    /// <param name="deltaTime">Positive step.</param><param name="cancellationToken">Cooperative tick cancellation.</param><returns>Whether a complete tick ran.</returns>
    public bool Tick(TimeSpan deltaTime, CancellationToken cancellationToken = default)
        => Owner.Invoke(() => _scheduler!.Tick(deltaTime, cancellationToken));
    private ModGroup Owner => _group ?? throw new ObjectDisposedException(nameof(ModWorldSession));

    internal void Load(ModGroup group, SceneDocument document, ModRegistration registration, CancellationToken token)
    {
        _group = group;
        try
        {
            _scene = _loaders!.Create(registration.Components!.Build()).Load(document, token);
            _scheduler = EcsScheduler.Create(_scene.World, registration.Systems);
        }
        catch (EcsSchedulerCreationException error) { _scheduler = error.CleanupOwner; throw; }
        catch (SceneWorldLoadException error) { _scene = error.CleanupOwner as SceneWorld; throw; }
        finally { _loaders = null; }
    }
    internal void DisposeCore()
    {
        if (IsDisposed) return;
        _scheduler?.Dispose(); _scheduler = null;
        _scene?.Dispose(); _scene = null;
        _loaders = null; _group = null; IsDisposed = true;
    }
}
