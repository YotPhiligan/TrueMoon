namespace TrueMoon.Aether.Abstractions.Ecs;

/// <summary>A synchronous system, exclusively owned by one host scheduler after successful factory activation.</summary>
/// <remarks>Stop runs once in reverse execution order while data remains available; Dispose completes owned cleanup.
/// Failed activation is cleaned by its creating factory/owner. Cancellation is cooperative, never forced abort.</remarks>
public interface ISystem : IDisposable
{
    /// <summary>Executes with invocation-bound typed access. Do not retain refs or schedule concurrent access.</summary>
    /// <param name="context">Current scoped context.</param>
    void Execute(EcsSystemContext context);
    /// <summary>Stops before Dispose. Reentrant structural playback or scheduler disposal inside Execute is forbidden.</summary>
    void Stop() { }
}

/// <summary>Explicit ECS name for the same system lifecycle. Preserves Execute/Stop/Dispose semantics in the new ABI.</summary>
public interface IEcsSystem : ISystem;
