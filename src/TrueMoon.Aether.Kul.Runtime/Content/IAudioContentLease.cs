namespace TrueMoon.Aether.Kul.Runtime.Content;

/// <summary>Caller-owned immutable capture pins. Dispose only after native playback/stream cleanup has completed.</summary>
public interface IAudioContentLease : IDisposable
{
    /// <summary>Validated descriptors.</summary>
    AudioContentManifest Manifest { get; }
    /// <summary>Returns a pinned immutable encoded path; never resolves project/source paths.</summary>
    /// <param name="id">Exact captured identity.</param><returns>Absolute path.</returns>
    string GetPath(AudioAssetId id);
}
