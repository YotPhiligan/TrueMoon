

namespace TrueMoon.Nom.Storage;

// Per-factory low-level IO dependency. No ambient overrides or public fault callbacks.
internal class NomFileOperations : NomKvFileOperations
{
    internal virtual void CreateBackupDirectory(string path) => Directory.CreateDirectory(path);
    internal virtual void PublishBackupDirectory(string source,string destination) => Directory.Move(source,destination);
    internal virtual FileStream CreateStaging(string path) => new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
    internal virtual ValueTask WriteStagingAsync(FileStream target, ReadOnlyMemory<byte> bytes, CancellationToken token) => target.WriteAsync(bytes, token);
    internal virtual Task FlushStagingAsync(FileStream target, CancellationToken token) => target.FlushAsync(token);
    internal virtual void FlushStagingToDisk(FileStream target) => target.Flush(true);
    internal virtual void MoveObject(string source, string destination) => File.Move(source, destination, false);
    internal virtual void DeleteStagingFile(string path) => File.Delete(path);
    internal virtual void DeleteStagingDirectory(string path) => Directory.Delete(path, false);
}
