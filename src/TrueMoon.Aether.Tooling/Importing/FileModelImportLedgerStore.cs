using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Bounded checksummed sidecar with same-volume atomic replacement and generation CAS.</summary>
/// <remarks>Trusted local Windows filesystem supporting FileRenameInfoEx with POSIX semantics.
/// Cooperating writers honor the permanent .write-lock sidecar. File bytes are flushed
/// before replacement. This is process-crash consistency, not a hostile-filesystem or power-loss guarantee.</remarks>
public sealed partial class FileModelImportLedgerStore : IModelImportLedgerStore
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
            // Retry external sharing/lock violations only, not a genuinely absent head or corrupt data.
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
            else ReplaceHead(temporary, path);
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

    private static unsafe void ReplaceHead(string staging, string head)
    {
        const uint deleteAccess = 0x00010000, shareAll = 7, openExisting = 3;
        const uint replaceIfExists = 1, posixSemantics = 2;
        const int fileRenameInfoEx = 22;
        const string extendedPathPrefix = @"\\?\";

        using var file = CreateFileW(extendedPathPrefix + staging, deleteAccess, shareAll, 0, openExisting, 0, 0);
        if (file.IsInvalid) throw WindowsError("Cannot open the staged model ledger for rename", Marshal.GetLastPInvokeError());

        // Replace the directory entry once. New readers see the new file; open readers keep the old one.
        var prefix = head.Length >= 260 ? extendedPathPrefix.AsSpan() : ReadOnlySpan<char>.Empty;
        var nameLength = prefix.Length + head.Length;
        var capacity = sizeof(RenameInfo) + nameLength * sizeof(char);
        Span<byte> buffer = capacity <= 1024 ? stackalloc byte[capacity] : new byte[capacity];
        buffer.Clear();
        fixed (byte* pointer = buffer)
        {
            var info = (RenameInfo*)pointer;
            info->Flags = replaceIfExists | posixSemantics;
            info->FileNameLength = (uint)(nameLength * sizeof(char));
            var destination = new Span<char>(&info->FileName, nameLength);
            prefix.CopyTo(destination);
            head.AsSpan().CopyTo(destination[prefix.Length..]);
            if (SetFileInformationByHandle(file, fileRenameInfoEx, info, (uint)capacity) == 0)
                throw WindowsError("Cannot replace the model ledger head", Marshal.GetLastPInvokeError());
        }
    }

    private static IOException WindowsError(string operation, int error)
        => new(operation + ": " + new Win32Exception(error).Message, unchecked((int)(0x80070000u | (uint)error)));

    [StructLayout(LayoutKind.Sequential)]
    private struct RenameInfo
    {
        public uint Flags;
        public nint RootDirectory;
        public uint FileNameLength;
        public char FileName;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial int SetFileInformationByHandle(SafeFileHandle file, int informationClass, RenameInfo* information, uint size);

    internal static string Validate(string path,Guid sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (sourceId==Guid.Empty) throw new ArgumentException("Empty model source identity.",nameof(sourceId));
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Atomic model ledger publication currently requires Windows.");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\",StringComparison.Ordinal)) throw new ArgumentException("Absolute local model sidecar path required.",nameof(path));
        return Path.GetFullPath(path);
    }
}
