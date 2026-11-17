using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Tooling.Importing;

// All directory ancestors are pinned without delete sharing before the next child is
// opened. OPEN_REPARSE_POINT plus handle attributes/final names reject path indirection.
// This is trusted local-source capture, not an ACL or hostile-filesystem sandbox.
internal sealed partial class ModelSourceFiles : IDisposable
{
    private readonly string _directory;
    private readonly Action<int> _handles;
    private readonly Dictionary<string, SafeFileHandle> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(CapturedModelFile File, FileStream Stream)> _files = [];

    internal ModelSourceFiles(string directory, Action<int> handles) { _directory = directory; _handles = handles; }

    internal async Task<CapturedModelFile> ReadAsync(string name, int maximum, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(Path.Combine(_directory, name.Replace('/', Path.DirectorySeparatorChar)));
        var parent = Path.GetDirectoryName(path)!; PinDirectory(parent);
        var handle = Open(path, directory: false); FileStream? stream = null;
        try
        {
            stream = new FileStream(handle, FileAccess.Read, 65536, isAsync: true);
            var length = stream.Length;
            if (length > maximum) throw new InvalidDataException("Model source file or closure byte budget exceeded before allocation.");
            var bytes = new byte[checked((int)length)];
            for (var position = 0; position < bytes.Length;)
            {
                token.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(bytes.AsMemory(position, Math.Min(65536, bytes.Length - position)), token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Model source changed while reading."); position += read;
            }
            if (stream.Length != length) throw new IOException("Model source length changed during capture.");
            var result = new CapturedModelFile(name, bytes); _files.Add((result, stream)); _handles(1);
            return result;
        }
        catch { if (stream != null) stream.Dispose(); else handle.Dispose(); throw; }
    }

    internal async Task RevalidateAsync(CancellationToken token)
    {
        foreach (var (file, stream) in _files)
        {
            token.ThrowIfCancellationRequested();
            Verify(stream.SafeFileHandle, Path.Combine(_directory, file.Name.Replace('/', Path.DirectorySeparatorChar)), directory: false);
            stream.Position = 0;
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
            if (stream.Length != file.Length || digest != file.Sha256) throw new IOException("Model bytes changed before capture publication.");
        }
        foreach (var (path, handle) in _directories) { token.ThrowIfCancellationRequested(); Verify(handle, path, directory: true); }
    }

    private void PinDirectory(string path)
    {
        var root = Path.GetPathRoot(path)!;
        if (path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length > 128)
            throw new InvalidDataException("Model source directories exceed the 128-level capture budget.");
        if (_directories.ContainsKey(path)) return;
        var pending = new Stack<string>();
        for (string? current = path; current != null && !_directories.ContainsKey(current); current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar)))
            pending.Push(current);
        foreach (var current in pending)
        {
            var handle = Open(current, directory: true);
            try { _directories.Add(current, handle); _handles(1); }
            catch { handle.Dispose(); throw; }
        }
    }

    private static SafeFileHandle Open(string path, bool directory)
    {
        var handle = CreateFileW(@"\\?\" + path, directory ? 0x81u : 0x80000000u,
            directory ? 3u : 1u, 0, 3, directory ? 0x02200000u : 0x48200000u, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError(); handle.Dispose();
            if (error is 2 or 3) throw new FileNotFoundException("A model source or dependency is missing.", path);
            throw new IOException("Cannot pin model source: " + new Win32Exception(error).Message, unchecked((int)(0x80070000u | (uint)error)));
        }
        try { Verify(handle, path, directory); return handle; }
        catch { handle.Dispose(); throw; }
    }

    private static void Verify(SafeFileHandle handle, string path, bool directory)
    {
        if (GetFileInformationByHandleEx(handle, 9, out var info, 8) == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var attributes = (FileAttributes)info.Attributes;
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("Model source capture cannot traverse reparse points.");
        if (((attributes & FileAttributes.Directory) != 0) != directory) throw new InvalidDataException("Model source path has the wrong file kind.");
        var expected = (@"\\?\" + Path.GetFullPath(path)).TrimEnd('\\');
        if (!string.Equals(FinalName(handle).TrimEnd('\\'), expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException("An opened model path differs from its requested final name.");
    }

    private static unsafe string FinalName(SafeFileHandle handle)
    {
        Span<char> buffer = stackalloc char[512]; uint length;
        fixed (char* pointer = buffer) length = GetFinalPathNameByHandleW(handle, pointer, (uint)buffer.Length, 0);
        if (length == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        if (length < buffer.Length) return new string(buffer[..(int)length]);
        if (length > 32768) throw new IOException("Model final path exceeds the Windows path budget.");
        var large = new char[length];
        fixed (char* pointer = large) length = GetFinalPathNameByHandleW(handle, pointer, (uint)large.Length, 0);
        if (length == 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        if (length >= large.Length) throw new IOException("Model final path changed while querying.");
        return new string(large, 0, (int)length);
    }

    public void Dispose()
    {
        foreach (var (_, stream) in _files.AsEnumerable().Reverse()) { stream.Dispose(); _handles(-1); } _files.Clear();
        foreach (var handle in _directories.Values.Reverse()) { handle.Dispose(); _handles(-1); } _directories.Clear();
    }

    // Win32 HANDLE is owned by SafeFileHandle; DWORD/BOOL are 32-bit, paths are UTF-16.
    // FILE_ATTRIBUTE_TAG_INFO has two DWORD fields (8 bytes). Final-name length is in UTF-16 chars.
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out AttributeTagInfo info, uint size);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial uint GetFinalPathNameByHandleW(SafeFileHandle handle, char* path, uint count, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTagInfo { public uint Attributes; public uint ReparseTag; }
}
