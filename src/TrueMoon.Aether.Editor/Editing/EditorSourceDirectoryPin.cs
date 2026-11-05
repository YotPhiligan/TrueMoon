using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrueMoon.Aether.Editor.Editing;

// A scan owns these short-lived namespace pins. Read/write sharing permits child edits;
// omitting delete sharing fences directory rename/replacement. Attributes/ACL/reparse
// changes are not frozen by sharing: the owner must revalidate paths before publishing Same.
internal static partial class EditorSourceDirectoryPin
{
    internal static IDisposable? Open(string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Directory observation requires Windows.");
        var handle = CreateFileW(@"\\?\" + Path.GetFullPath(directory), 0x81, 3, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError(); handle.Dispose();
            if (error is 2 or 3) return null;
            throw Failure(error, directory);
        }
        try
        {
            if (GetFileInformationByHandleEx(handle, 9, out var info, 8) == 0)
                throw Failure(Marshal.GetLastPInvokeError(), directory);
            var attributes = (FileAttributes)info.Attributes;
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new NotSupportedException("Source observation does not traverse reparse points.");
            if ((attributes & FileAttributes.Directory) == 0) { handle.Dispose(); return null; }
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private static Exception Failure(int error, string path)
    {
        var native = new Win32Exception(error);
        var message = "Source directory coverage failed: " + path + ": " + native.Message;
        return error == 5 ? new UnauthorizedAccessException(message, native) : new IOException(message, unchecked((int)(0x80070000u | (uint)error)));
    }

    // DWORD flags/access, UTF-16 path, owned HANDLE. LIST_DIRECTORY|READ_ATTRIBUTES, READ|WRITE
    // sharing, OPEN_EXISTING, BACKUP_SEMANTICS|OPEN_REPARSE_POINT; query FILE_ATTRIBUTE_TAG_INFO.
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out AttributeTagInfo info, uint size);
    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTagInfo { public uint Attributes; public uint ReparseTag; }
}
