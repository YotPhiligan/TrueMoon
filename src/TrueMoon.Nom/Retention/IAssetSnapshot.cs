using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Retention;

/// <summary>An owned snapshot lease. Dispose it before disposing its store.</summary>
/// <remarks>Closing prevents new opens. Existing streams protect the manifest until their actual close.
/// Durable retention is independent: retain/release a named pin explicitly.</remarks>
public interface IAssetSnapshot : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the frozen detached manifest.</summary>
    AssetSnapshotManifest Manifest { get; }
    /// <summary>Opens a verified exact member as an owned stream.</summary>
    /// <param name="exact">An exact version contained in this manifest.</param><param name="token">Opening cancellation.</param>
    /// <returns>An owned read stream that must be closed separately.</returns>
    Task<Stream> OpenResourceAsync(AssetResourceVersion exact, CancellationToken token = default);
}
