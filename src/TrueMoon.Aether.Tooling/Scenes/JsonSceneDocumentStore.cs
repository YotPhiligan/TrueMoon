using System.Security.Cryptography;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Tooling.Scenes;

/// <summary>Bounded scene files with same-directory atomic publication and optimistic content-conflict detection.</summary>
/// <remarks>A persistent .write-lock sidecar serializes cooperating TrueMoon writers. Fingerprints are checked immediately before
/// replacement; arbitrary external rename races are outside this trusted-local optimistic profile.</remarks>
/// <param name="codec">Borrowed authoring codec.</param>
public sealed class JsonSceneDocumentStore(SceneFileCodec codec) : ISceneDocumentStore
{
    private readonly SceneFileCodec _codec = codec ?? throw new ArgumentNullException(nameof(codec));

    /// <inheritdoc />
    public async Task<SceneFile> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Normalize(path);
        cancellationToken.ThrowIfCancellationRequested();
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var bytes = await ReadBytesAsync(input, SceneFileCodec.MaximumBytes, cancellationToken).ConfigureAwait(false);
        return new(_codec.Decode(bytes, cancellationToken), Stamp(bytes));
    }

    /// <inheritdoc />
    public async Task<SceneFileStamp> WriteAsync(string path, SceneDocument document, SceneFileStamp? expected,
        CancellationToken cancellationToken = default)
    {
        path = Normalize(path);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = _codec.Encode(document, cancellationToken);
        return await WriteAtomicAsync(path, bytes, expected, SceneFileCodec.MaximumBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<SceneFileStamp> WriteAtomicAsync(string path, byte[] bytes, SceneFileStamp? expected,
        int maximumBytes, CancellationToken cancellationToken)
    {
        var stamp = Stamp(bytes);
        // The empty sidecar remains after closing; no stale ownership survives a process exit.
        using var writerLock = new FileStream(path + ".write-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Exception? failure = null;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (expected == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException error) when (File.Exists(path)) { throw new SceneFileConflictException(path, error); }
            }
            else
            {
                await using var original = OpenExisting(path);
                var observed = Stamp(await ReadBytesAsync(original, maximumBytes, cancellationToken).ConfigureAwait(false));
                if (observed != expected) throw new SceneFileConflictException(path);
                cancellationToken.ThrowIfCancellationRequested();
                File.Replace(temporary, path, destinationBackupFileName: null);
            }
            return stamp;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private static FileStream OpenExisting(string path)
    {
        try { return new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous); }
        catch (FileNotFoundException error) { throw new SceneFileConflictException(path, error); }
    }

    internal static async Task<byte[]> ReadBytesAsync(FileStream input, int maximumBytes, CancellationToken token)
    {
        if (input.Length > maximumBytes) throw new InvalidDataException("Scene file exceeds the byte limit.");
        var bytes = new byte[checked((int)input.Length)];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await input.ReadAsync(bytes.AsMemory(count), token).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        if (count != bytes.Length || input.ReadByte() >= 0) throw new InvalidDataException("Scene file length changed during read.");
        return bytes;
    }

    internal static SceneFileStamp Stamp(byte[] bytes) => new(Convert.ToHexString(SHA256.HashData(bytes)));
    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = System.IO.Path.GetFullPath(path);
        if (!path.EndsWith(".aetherscene", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected a .aetherscene path.", nameof(path));
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(path))) throw new DirectoryNotFoundException("The scene directory must exist before saving.");
        return path;
    }
}
