namespace TrueMoon.Kton.Bepu;

/// <summary>App startup settings. Binding retains a validated, detached snapshot.</summary>
public sealed class BepuPhysicsOptions
{
    /// <summary>Defaults used by IFactory&lt;IPhysicsWorld&gt;.Create(). Explicit runtime arguments replace this whole value.</summary>
    public PhysicsWorldOptions DefaultWorld { get; set; } = new();

    internal BepuPhysicsOptions Copy() => new() { DefaultWorld = Snapshot() };
    internal PhysicsWorldOptions Snapshot()
    {
        PhysicsValidation.Validate(DefaultWorld).ThrowIfFailure();
        return DefaultWorld with { Limits = DefaultWorld.Limits with { } };
    }
}
