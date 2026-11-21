using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Backup;

// One worker-owned transfer. Every open is retained until actual close, including failed cleanup.
internal sealed class NomBackupTransfer(NomFileOperations files)
{
    private sealed record Owned(FileStream Stream,SafeFileHandle Handle);
    private readonly List<Owned> owned=[];
    internal static string LocalRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        NomBinary.Require(Path.IsPathFullyQualified(root) && !root.StartsWith(@"\\",StringComparison.Ordinal) && !root.StartsWith("//",StringComparison.Ordinal),"Full local backup root required.");
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        NomBinary.Require(root!=Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!),"Volume root is not a backup destination."); CheckPath(root); return root;
    }
    internal static void CheckPath(string path)
    {
        NomBinary.Require(!(File.Exists(path)||Directory.Exists(path)) || (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Reparse backup path unsupported.");
        for(var directory=new DirectoryInfo(Path.GetDirectoryName(path)!);directory!=null;directory=directory.Parent)
            NomBinary.Require(!directory.Exists || (directory.Attributes&FileAttributes.ReparsePoint)==0,"Reparse backup ancestor unsupported.");
    }
    internal static void Separate(string source,string destination)
    {
        var comparison=OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        NomBinary.Require(!source.Equals(destination,comparison) && !source.StartsWith(destination+Path.DirectorySeparatorChar,comparison) &&
            !destination.StartsWith(source+Path.DirectorySeparatorChar,comparison),"Backup roots must not overlap.");
    }
    internal static string PrepareDestination(string source,string destination)
    {
        Separate(source,destination); NomBinary.Require(!Directory.Exists(destination) && !File.Exists(destination),"Backup/restore destination already exists.");
        var parent=Path.GetDirectoryName(destination)!; NomBinary.Require(Directory.Exists(parent),"Backup destination parent must exist."); CheckPath(parent);
        return destination+".nom-"+Guid.NewGuid().ToString("N")+".pending";
    }
    internal FileStream Open(string path,FileMode mode,FileAccess access)
    {
        // The live active journal already has a read/write handle. The owner worker serializes mutation.
        CheckPath(path); var file=files.OpenFile(path,mode,access,access==FileAccess.Read ? FileShare.ReadWrite : FileShare.None);
        owned.Add(new(file,file.SafeFileHandle)); return file;
    }
    internal void Close(FileStream file)
    {
        var item=owned.First(o => ReferenceEquals(o.Stream,file));
        if(!item.Handle.IsClosed) files.Close(file);
        NomBinary.Require(item.Handle.IsClosed,"Backup file close did not release its handle."); owned.Remove(item);
    }
    internal void CloseAll()
    {
        var errors=new List<Exception>();
        foreach(var item in owned.ToArray()) { try { Close(item.Stream); } catch(Exception error) { errors.Add(error); } }
        if(errors.Count!=0) throw new AggregateException("Backup handles retained until actual close.",errors);
    }
    internal NomBackupEntry Copy(string source,string destination,string relative,long length,string? expected,CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); CheckPath(source); CheckPath(destination);
        files.CreateBackupDirectory(Path.GetDirectoryName(destination)!); var input=Open(source,FileMode.Open,FileAccess.Read);
        NomBinary.Require(input.Length==length,"Backup source length changed."); var output=Open(destination,FileMode.CreateNew,FileAccess.Write);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer=new byte[ResourceCodec.BufferBytes]; long copied=0;
        while(copied<length)
        {
            token.ThrowIfCancellationRequested(); var count=input.Read(buffer,0,(int)Math.Min(buffer.Length,length-copied)); NomBinary.Require(count>0,"Truncated backup source.");
            hash.AppendData(buffer.AsSpan(0,count)); files.Write(output,buffer.AsMemory(0,count)); copied+=count;
        }
        NomBinary.Require(input.ReadByte()==-1,"Backup source length changed."); var digest=Convert.ToHexStringLower(hash.GetHashAndReset());
        NomBinary.Require(expected==null || expected==digest,"Backup source hash mismatch."); files.Flush(output); Close(output); Close(input);
        Verify(destination,length,digest,token); return new(relative,length,digest);
    }
    internal void Verify(string path,long length,string digest,CancellationToken token)
    {
        var file=Open(path,FileMode.Open,FileAccess.Read); NomBinary.Require(file.Length==length,"Prepared backup length mismatch.");
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer=new byte[ResourceCodec.BufferBytes];
        int count; while((count=file.Read(buffer))!=0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer.AsSpan(0,count)); }
        NomBinary.Require(Convert.ToHexStringLower(hash.GetHashAndReset())==digest,"Prepared backup hash mismatch."); Close(file);
    }
    internal void Write(string path,byte[] bytes,CancellationToken token)
    {
        var file=Open(path,FileMode.CreateNew,FileAccess.Write);
        for(var offset=0;offset<bytes.Length;offset+=ResourceCodec.BufferBytes)
        { token.ThrowIfCancellationRequested(); files.Write(file,bytes.AsMemory(offset,Math.Min(ResourceCodec.BufferBytes,bytes.Length-offset))); }
        files.Flush(file); Close(file);
    }
    internal byte[] ReadManifest(string root)
    {
        var file=Open(Path.Combine(root,"nom.backup"),FileMode.Open,FileAccess.Read);
        NomBinary.Require(file.Length is >= 160 and <= NomBackupCodec.MaximumManifestBytes,"Backup manifest size."); var bytes=new byte[(int)file.Length]; file.ReadExactly(bytes); Close(file); return bytes;
    }
    internal static string Resolve(string root,string relative)
    { NomBackupCodec.ValidatePath(relative); return Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)); }
}
