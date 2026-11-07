namespace TrueMoon.Aether.Ecs;

public sealed partial class World
{
    private object? _sceneOwner;
    private bool _sceneLoading, _sceneWrite;
    internal void ClaimScene(object owner)
    {
        CheckPlayback();
        if (_sceneOwner != null || _schedulerOwner != null || _nextIndex != 0 || _entityCount != 0 || _pools.Count != 0)
            throw new InvalidOperationException("A scene load requires a fresh empty unowned World.");
        _sceneOwner = owner; _sceneLoading = true;
    }
    internal bool SceneFits(object owner, int entities, int types)
    { CheckSceneOwner(owner); return entities <= _maximumEntities && types <= _maximumComponentTypes; }
    internal EntityHandle CreateSceneEntity(object owner)
    {
        CheckSceneOwner(owner); _sceneWrite = true;
        try { return CreateEntity(); } finally { _sceneWrite = false; }
    }
    internal void AddSceneComponent<T>(object owner, EntityHandle entity, T value) where T : struct
    {
        CheckSceneOwner(owner); _sceneWrite = true;
        try { AddComponent(entity, value); } finally { _sceneWrite = false; }
    }
    internal void PublishScene(object owner) { CheckSceneOwner(owner); _sceneLoading = false; }
    internal void DisposeScene(object owner)
    {
        CheckSceneOwner(owner);
        if (_activeQuery || _schedulerOwner != null || _systemPlan != null || _activatingSystems)
            throw new InvalidOperationException("Finish queries and scheduler cleanup before disposing SceneWorld.");
        _sceneOwner = null; _sceneLoading = false; Dispose();
    }
    private void CheckSceneOwner(object owner)
    {
        ThrowIfDisposed();
        if (!ReferenceEquals(_sceneOwner, owner)) throw new InvalidOperationException("Scene ownership mismatch.");
    }
}
