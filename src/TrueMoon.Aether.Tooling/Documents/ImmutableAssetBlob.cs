using TrueMoon.Aether.Tooling.Scenes;

namespace TrueMoon.Aether.Tooling.Documents;

// Paths are generated from validated hashes in the trusted project content directory.
internal static class ImmutableAssetBlob
{
    internal static async Task<byte[]> ReadAsync(string path, int maximumBytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RejectReparsePoint(Path.GetDirectoryName(path)!);
        RejectReparsePoint(path);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSceneDocumentStore.ReadBytesAsync(input, maximumBytes, token).ConfigureAwait(false);
    }

    internal static async Task PublishAsync(string path, byte[] canonical, int maximumBytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (canonical.Length > maximumBytes) throw new InvalidDataException("Ресурс превышает предел размера.");
        var directory = Path.GetDirectoryName(path)!;
        try { RejectReparsePoint(directory); }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            Directory.CreateDirectory(directory);
        }
        RejectReparsePoint(directory);
        try
        {
            await RequireMatchAsync(path, canonical, maximumBytes, token).ConfigureAwait(false);
            return;
        }
        catch (FileNotFoundException) { }

        try
        {
            await JsonSceneDocumentStore.WriteAtomicAsync(path, canonical, null, maximumBytes, token).ConfigureAwait(false);
        }
        catch (SceneFileConflictException)
        {
            await RequireMatchAsync(path, canonical, maximumBytes, token).ConfigureAwait(false);
        }
    }

    internal static async Task RequireMatchAsync(string path, byte[] canonical, int maximumBytes, CancellationToken token)
    {
        var existing = await ReadAsync(path, maximumBytes, token).ConfigureAwait(false);
        if (!existing.AsSpan().SequenceEqual(canonical))
            throw new InvalidDataException("Сохранённые данные ресурса не совпадают с каноническими байтами.");
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Хранилище ресурсов не допускает перенаправления файлов или каталогов.");
    }
}
