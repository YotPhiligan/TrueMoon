namespace TrueMoon.Nom.Storage;

// Per-factory IO seam. No process-wide overrides or public fault injection options.
internal class NomKvFileOperations
{
    internal virtual FileStream OpenFile(string path, FileMode mode, FileAccess access, FileShare share) =>
        new(path, mode, access, share, 1, FileOptions.RandomAccess);
    internal virtual void Write(FileStream file, ReadOnlyMemory<byte> bytes) => file.Write(bytes.Span);
    internal virtual void Flush(FileStream file) => file.Flush(flushToDisk: true);
    internal virtual void Truncate(FileStream file, long length) => file.SetLength(length);
    internal virtual void PublishRoot(string source, string destination) => File.Move(source, destination, overwrite: false);
    internal virtual void Close(FileStream file) => file.Dispose();
    // Both names are in the same verified local directory; never use a copy/delete fallback.
    internal virtual void ReplaceRoot(string source, string destination) => File.Move(source, destination, overwrite: true);
    internal virtual void DeleteFile(string path) => File.Delete(path);
}
