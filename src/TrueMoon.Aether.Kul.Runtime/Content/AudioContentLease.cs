using System.Security.Cryptography;
using TrueMoon.Aether.Runtime.Ecs.Runtime;

namespace TrueMoon.Aether.Kul.Runtime.Content;

/// <summary>Default trusted-capture reader. Retains read-only file handles against writes/deletes until native cleanup.</summary>
public sealed class AudioContentLease : IAudioContentLease
{
    private readonly List<FileStream> _pins = [];
    private readonly Dictionary<AudioAssetId, string> _paths = [];
    private bool _disposing, _disposed;
    private AudioContentLease() { }
    /// <inheritdoc/>
    public AudioContentManifest Manifest { get; private set; } = new([]);
    /// <summary>Validates the whole capture before returning pins. No native resources are acquired.</summary>
    /// <param name="arguments">Immutable identity and bounded policy.</param><returns>Caller-owned lease.</returns>
    public static AudioContentLease Open(AudioContentLoadArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Reference);
        if (arguments.MaximumAssets is < 1 or > 64 || arguments.MaximumEncodedBytes is < 1 or > 1024L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(arguments));
        var lease = new AudioContentLease();
        try { lease.Load(arguments); return lease; }
        catch (Exception error)
        {
            try { lease.Dispose(); }
            catch (Exception cleanup) { throw new SceneWorldPreparationException(new AggregateException(error, cleanup), lease); }
            throw;
        }
    }
    private void Load(AudioContentLoadArguments args)
    {
        var token = args.CancellationToken; token.ThrowIfCancellationRequested();
        var root = args.Reference.DirectoryPath;
        if ((File.GetAttributes(root) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory) throw new InvalidDataException("Capture root must be an ordinary directory.");
        var metadata = Pin(Path.Combine(root, AudioContentManifest.FileName));
        if (metadata.Length is < 1 or > AudioContentManifest.MaximumBytes) throw new InvalidDataException("Invalid audio metadata size.");
        var bytes = new byte[checked((int)metadata.Length)]; metadata.ReadExactly(bytes);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), args.Reference.ManifestSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Audio capture identity mismatch.");
        Manifest = AudioContentManifest.Decode(bytes);
        if (Manifest.Assets.Count > args.MaximumAssets) throw new InvalidDataException("Audio capture asset limit exceeded.");
        var expected = Manifest.Assets.Select(a => a.FileName).Append(AudioContentManifest.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFileSystemEntries(root))
        {
            token.ThrowIfCancellationRequested();
            if (!expected.Remove(Path.GetFileName(file))) throw new InvalidDataException("Unexpected capture entry.");
        }
        if (expected.Count != 0) throw new InvalidDataException("Missing capture entry.");
        long total = 0; byte[] buffer = new byte[65536];
        foreach (var asset in Manifest.Assets)
        {
            token.ThrowIfCancellationRequested();
            if (asset.Length > args.MaximumEncodedBytes - total) throw new InvalidDataException("Encoded audio limit exceeded.");
            total += asset.Length; var path = Path.Combine(root, asset.FileName); var file = Pin(path);
            if (file.Length != asset.Length) throw new InvalidDataException("Captured audio length mismatch.");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int read;
            while ((read = file.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, read); }
            if (!string.Equals(Convert.ToHexString(hash.GetHashAndReset()), asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Captured audio hash mismatch.");
            _paths.Add(asset.Id, path);
        }
        token.ThrowIfCancellationRequested();
    }
    private FileStream Pin(string path)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new InvalidDataException("Capture files must be ordinary files.");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); _pins.Add(stream); return stream;
    }
    /// <inheritdoc/>
    public string GetPath(AudioAssetId id)
    { ObjectDisposedException.ThrowIf(_disposed || _disposing, this); return _paths.TryGetValue(id, out var path) ? path : throw new KeyNotFoundException("Unknown captured audio identity."); }
    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return; _disposing = true;
        List<Exception>? errors = null;
        for (var i = _pins.Count - 1; i >= 0; i--)
        {
            try { _pins[i].Dispose(); _pins.RemoveAt(i); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors is not null) throw new AggregateException("Audio content pin cleanup failed; retry Dispose.", errors);
        _paths.Clear(); _disposed = true;
    }
}
