using TrueMoon.Aether.Abstractions.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Ecs.Loading;

/// <summary>Runtime creation arguments; cancellation is never serialized.</summary>
public sealed record SceneWorldLoadRequest(SceneWorldData Data, CancellationToken CancellationToken = default);
