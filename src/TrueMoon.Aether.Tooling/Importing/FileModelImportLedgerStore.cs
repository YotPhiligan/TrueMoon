namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Bounded checksummed sidecar with same-volume atomic replacement and generation CAS.</summary>
/// <remarks>Trusted local Windows filesystem; cooperating writers honor the permanent .write-lock sidecar.
/// File bytes are flushed before replacement. This is process-crash consistency, not a hostile-filesystem or power-loss guarantee.</remarks>
public sealed class FileModelImportLedgerStore : IModelImportLedgerStore
{
    /// <inheritdoc />
    public async Task<PublishedModelImport?> ReadAsync(string path,Guid sourceId,CancellationToken cancellationToken=default)
    {
        path=Validate(path,sourceId); cancellationToken.ThrowIfCancellationRequested();
        var input=await OpenReadAsync(path,cancellationToken).ConfigureAwait(false); if(input==null) return null;
        await using (input)
        {
            if (input.Length>ModelImportLedgerCodec.MaximumBytes) throw new InvalidDataException("Published model byte budget exceeded before allocation.");
            var bytes=new byte[checked((int)input.Length)]; await input.ReadExactlyAsync(bytes,cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); return ModelImportLedgerCodec.Decode(bytes,sourceId);
        }
    }
    private static async Task<FileStream?> OpenReadAsync(string path,CancellationToken token)
    {
        for(var attempt=0;;attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return new(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,65536,FileOptions.Asynchronous); }
            catch(Exception error) when(error is FileNotFoundException or DirectoryNotFoundException) { token.ThrowIfCancellationRequested(); return null; }
            // ReplaceFileW briefly holds metadata handles. Retry only sharing/lock violations, never corrupt contents or access denial.
            catch(IOException error) when(attempt<19 && (error.HResult&0xffff) is 32 or 33) { await Task.Delay(5,token).ConfigureAwait(false); }
        }
    }
    /// <inheritdoc />
    public async Task<PublishedModelImport> PublishAsync(string path,Guid sourceId,long expectedGeneration,ModelImportCandidate candidate,ModelImportIdentityEdits? edits=null,CancellationToken cancellationToken=default)
    {
        path=Validate(path,sourceId); ArgumentNullException.ThrowIfNull(candidate);
        if (expectedGeneration<0) throw new ArgumentOutOfRangeException(nameof(expectedGeneration));
        if (candidate.SourceId!=sourceId) throw new InvalidDataException("Changed model source identity.");
        cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Keep this empty file permanently: deleting it could split the writer lock into two different file objects.
        using var lease=new FileStream(path+".write-lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var previous=await ReadAsync(path,sourceId,cancellationToken).ConfigureAwait(false);
        var published=PublishedModelImport.Prepare(candidate,previous,expectedGeneration,edits);
        var bytes=ModelImportLedgerCodec.Encode(published); cancellationToken.ThrowIfCancellationRequested();
        var temporary=path+"."+Guid.NewGuid().ToString("N")+".pending"; var staged=false; var committed=false; Exception? failure=null;
        try
        {
            await using (var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous))
            {
                staged=true;
                for (var offset=0;offset<bytes.Length;offset+=65536)
                    await output.WriteAsync(bytes.AsMemory(offset,Math.Min(65536,bytes.Length-offset)),cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false); output.Flush(flushToDisk:true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (previous==null) File.Move(temporary,path,overwrite:false);
            else File.Replace(temporary,path,destinationBackupFileName:null);
            committed=true; return published; // No cancellation check or fallible application callback after the commit point.
        }
        catch (Exception error) { failure=error; throw; }
        finally
        {
            if (staged && !committed)
            {
                try { File.Delete(temporary); }
                catch (Exception cleanup) when (failure!=null) { throw new AggregateException(failure,cleanup); }
            }
        }
    }
    internal static string Validate(string path,Guid sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (sourceId==Guid.Empty) throw new ArgumentException("Empty model source identity.",nameof(sourceId));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Atomic model ledger publication currently requires Windows.");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\",StringComparison.Ordinal)) throw new ArgumentException("Absolute local model sidecar path required.",nameof(path));
        return Path.GetFullPath(path);
    }
}
