namespace TrueMoon.Aether.Kul.Runtime.Content;

/// <summary>Explicit immutable-content admission input, used before acquiring any native session.</summary>
/// <param name="Reference">Captured identity.</param><param name="MaximumAssets">Positive asset limit.</param>
/// <param name="MaximumEncodedBytes">Positive total encoded limit.</param><param name="CancellationToken">Cooperative pre-publication cancellation.</param>
public sealed record AudioContentLoadArguments(AudioContentReference Reference, int MaximumAssets, long MaximumEncodedBytes, CancellationToken CancellationToken = default);
