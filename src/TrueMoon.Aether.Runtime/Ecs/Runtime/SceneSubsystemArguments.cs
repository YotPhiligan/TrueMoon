using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Ecs.Runtime;

/// <summary>Preparation input for an exclusively session-owned subsystem; dependencies and scene are borrowed.</summary>
/// <param name="Scene">Private ready candidate. Do not dispose it or retain its ownership.</param>
/// <param name="FixedStep">The exact positive duration used by the session scheduler.</param>
/// <param name="CancellationToken">Cooperative preparation cancellation, never cleanup cancellation.</param>
public sealed record SceneSubsystemArguments(SceneWorld Scene, TimeSpan FixedStep, CancellationToken CancellationToken = default);
