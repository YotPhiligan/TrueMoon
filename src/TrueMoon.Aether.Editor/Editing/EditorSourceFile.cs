using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Editor.Editing;

// One census read owns one synchronous local-file handle. The existing observer worker
// performs IO; cancellation is cooperative between reads, never an early handle release.
internal sealed partial class EditorSourceFile : Stream
{
    private readonly SafeFileHandle _handle;
    private readonly string _nativePath;
    internal readonly record struct Snapshot(FileAttributes Attributes, long Length, long Written);
    internal Snapshot Before { get; }

    private EditorSourceFile(SafeFileHandle handle, string nativePath, Snapshot before)
    { _handle = handle; _nativePath = nativePath; Before = before; }

    internal static EditorSourceFile? Open(string path)
    {
        // GENERIC_READ, READ|WRITE|DELETE sharing, OPEN_EXISTING,
        // SEQUENTIAL_SCAN|OPEN_REPARSE_POINT; no OVERLAPPED/IOCP binding or backup privilege path.
        var nativePath = NativePath(path);
        var handle = CreateFileW(nativePath, 0x80000000, 7, 0, 3, 0x08200000, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError(); handle.Dispose();
            if (error is 2 or 3) return null;
            if (error == 5 && ReadMetadata(path) is { } occupied)
            {
                RejectReparse(occupied);
                if ((occupied.Attributes & FileAttributes.Directory) != 0) return null;
            }
            throw Failure(error, path);
        }
        try
        {
            var before = ReadOpenedMetadata(handle, path); RejectReparse(before);
            if ((before.Attributes & FileAttributes.Directory) != 0) { handle.Dispose(); return null; }
            return new(handle, nativePath, before);
        }
        catch { handle.Dispose(); throw; }
    }

    internal static Snapshot? ReadMetadata(string path) => QueryMetadata(NativePath(path));

    internal Snapshot? ReadMetadata()
    {
        ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
        // Reuse only the prepared name, never its previous metadata result.
        return QueryMetadata(_nativePath);
    }

    private static Snapshot? QueryMetadata(string nativePath)
    {
        if (GetFileAttributesExW(nativePath, 0, out var data) != 0) return Value(data);
        var error = Marshal.GetLastPInvokeError();
        if (error is 2 or 3) return null;
        throw Failure(error, nativePath);
    }

    private static Snapshot ReadOpenedMetadata(SafeFileHandle handle, string path)
    {
        int error;
        if (GetFileInformationByHandleEx(handle, 0, out var basic, 40) != 0)
        {
            var before = new Snapshot((FileAttributes)basic.Attributes, 0, basic.Written);
            RejectReparse(before);
            if ((before.Attributes & FileAttributes.Directory) != 0) return before;
            if (GetFileSizeEx(handle, out var length) != 0)
            {
                if (length < 0) throw new InvalidDataException("Source file length exceeds its representable byte budget.");
                return before with { Length = length };
            }
            error = Marshal.GetLastPInvokeError();
        }
        else error = Marshal.GetLastPInvokeError();
        // Providers may not implement the newer basic-info class. Preserve the previous
        // query only for unsupported APIs; access/sharing/IO errors still propagate.
        if (error is not (1 or 50 or 87)) throw Failure(error, path);
        if (GetFileInformationByHandle(handle, out var info) == 0) throw Failure(Marshal.GetLastPInvokeError(), path);
        return Value(new AttributeData { Attributes = info.Attributes, WriteLow = info.WriteLow, WriteHigh = info.WriteHigh,
            SizeHigh = info.SizeHigh, SizeLow = info.SizeLow });
    }

    internal static void RejectReparse(Snapshot value)
    {
        if ((value.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new NotSupportedException("Source observation does not traverse reparse points.");
    }

    private static string NativePath(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Source observation requires Windows.");
        return @"\\?\" + Path.GetFullPath(path);
    }
    private static Snapshot Value(AttributeData data)
    {
        var length = ((ulong)data.SizeHigh << 32) | data.SizeLow;
        if (length > long.MaxValue) throw new InvalidDataException("Source file length exceeds its representable byte budget.");
        return new((FileAttributes)data.Attributes, (long)length, unchecked((long)(((ulong)data.WriteHigh << 32) | data.WriteLow)));
    }
    internal static Exception Failure(int error, string path)
    {
        var native = new Win32Exception(error); var message = "Source file coverage failed: " + path + ": " + native.Message;
        return error == 5 ? new UnauthorizedAccessException(message, native) : new IOException(message, unchecked((int)(0x80070000u | (uint)error)));
    }

    public override bool CanRead => !_handle.IsClosed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override unsafe int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
        if (buffer.IsEmpty) return 0;
        fixed (byte* destination = buffer)
        {
            if (ReadFile(_handle, destination, (uint)buffer.Length, out var read, 0) == 0)
                throw Failure(Marshal.GetLastPInvokeError(), "opened source");
            return checked((int)read);
        }
    }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var read = Read(buffer.Span);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(read);
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _handle.Dispose(); base.Dispose(disposing); }

    // DWORD/FILETIME fields preserve the Win32 layouts: WIN32_FILE_ATTRIBUTE_DATA=36,
    // BY_HANDLE_FILE_INFORMATION=52 bytes. All BOOL returns are 32-bit integers.
    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeData
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, SizeHigh, SizeLow;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HandleData
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicData { public long Creation, Access, Written, Change; public uint Attributes; }
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandle(SafeFileHandle handle, out HandleData info);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out BasicData info, uint size);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileSizeEx(SafeFileHandle handle, out long size);
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial int GetFileAttributesExW(string name, int level, out AttributeData data);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static unsafe partial int ReadFile(SafeFileHandle handle, byte* buffer, uint bytes, out uint read, nint overlapped);
}
