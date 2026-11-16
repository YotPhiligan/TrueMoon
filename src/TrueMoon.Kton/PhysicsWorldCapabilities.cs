namespace TrueMoon.Kton;

/// <summary>Runtime features a provider has actually implemented. Consumers must require needed features before publishing a session.</summary>
[Flags]
public enum PhysicsWorldCapabilities
{
    /// <summary>No executable features.</summary>
    None = 0,
    /// <summary>Shapes, bodies, BallSocket, commands and fixed Step.</summary>
    RigidBodySimulation = 1,
    /// <summary>Bounded ray/sweep/overlap queries.</summary>
    Queries = 2,
    /// <summary>Generation-safe gameplay contact events.</summary>
    ContactEvents = 4,
    /// <summary>Upright dynamic capsule controllers through IPhysicsCharacterWorld.</summary>
    Characters = 8
}
