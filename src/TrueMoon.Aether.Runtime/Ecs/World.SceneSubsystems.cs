namespace TrueMoon.Aether.Runtime.Ecs;

public sealed partial class World
{
    private object? _sceneSubsystemOwner;
    private bool _preparingSceneSubsystems;
    internal void ClaimSceneSubsystems(object owner)
    {
        CheckPlayback();
        if (_schedulerOwner != null || _sceneSubsystemOwner != null) throw new InvalidOperationException("Prepare subsystems once, before scheduler activation.");
        _sceneSubsystemOwner = owner; _preparingSceneSubsystems = true;
    }
    internal void CompleteSceneSubsystemPreparation(object owner)
    {
        if (!ReferenceEquals(_sceneSubsystemOwner, owner)) throw new InvalidOperationException("Subsystem ownership mismatch.");
        _preparingSceneSubsystems = false;
    }
    internal void ReleaseSceneSubsystems(object owner)
    {
        if (!ReferenceEquals(_sceneSubsystemOwner, owner)) throw new InvalidOperationException("Subsystem ownership mismatch.");
        if (_schedulerOwner != null) throw new InvalidOperationException("Dispose the scheduler before subsystem dependencies.");
        _sceneSubsystemOwner = null; _preparingSceneSubsystems = false;
    }
    internal void CheckSceneSubsystemCleanup()
    {
        if (_schedulerOwner != null) throw new InvalidOperationException("Dispose the scheduler before subsystem dependencies.");
    }
}
