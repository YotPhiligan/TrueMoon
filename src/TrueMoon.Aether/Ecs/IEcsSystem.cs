namespace TrueMoon.Aether.Ecs;

/// <summary>A sequential system whose factory result is exclusively owned by one scheduler.</summary>
public interface IEcsSystem : IDisposable
{
    /// <summary>Executes synchronously with declared access. Do not retain borrowed refs or schedule concurrent World access.</summary>
    /// <param name="context">Active invocation context, invalid outside this call.</param>
    void Execute(EcsSystemContext context);
    /// <summary>Stops once in reverse execution order while World is still available. Dispose must finish owned cleanup.</summary>
    void Stop() { }
}
