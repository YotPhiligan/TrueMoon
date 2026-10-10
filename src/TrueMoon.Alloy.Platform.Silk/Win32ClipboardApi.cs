using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrueMoon.Alloy.Platform.Silk;

internal sealed partial class Win32ClipboardApi : IWin32ClipboardApi
{
    internal static readonly Win32ClipboardApi Instance = new();
    private const uint UnicodeText = 13;

    public int TryOpen(nint window) => OpenClipboard(window) != 0 ? 0 : Marshal.GetLastPInvokeError();
    public void WaitForRetry() => Thread.Sleep(10);
    public void Close() { if (CloseClipboard() == 0) throw Error("Failed to close Windows clipboard."); }
    public bool HasText() => IsClipboardFormatAvailable(UnicodeText) != 0;
    public string ReadText()
    {
        var memory = GetClipboardData(UnicodeText);
        if (memory == 0) throw Error("Failed to read Windows clipboard text.");
        return ReadMemory(memory);
    }
    internal static string ReadMemory(nint memory)
    {
        var size = GlobalSize(memory);
        var pointer = GlobalLock(memory);
        if (pointer == 0) throw Error("Failed to lock Windows clipboard text.");
        Exception? failure = null;
        try
        {
            return Win32Clipboard.DecodeText(pointer, size);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { Unlock(memory); }
            catch (Exception unlockError) when (failure != null)
            { throw new AggregateException("Clipboard decoding and unlocking failed.", failure, unlockError); }
        }
    }
    public nint AllocateText(string text)
    {
        var memory = GlobalAlloc(0x42, checked(((nuint)text.Length + 1) * 2));
        if (memory == 0) throw Error("Failed to allocate Windows clipboard text.");
        try
        {
            var pointer = GlobalLock(memory);
            if (pointer == 0) throw Error("Failed to lock allocated clipboard text.");
            Exception? failure = null;
            try
            {
                var chars = (text + '\0').ToCharArray();
                Marshal.Copy(chars, 0, pointer, chars.Length);
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                try { Unlock(memory); }
                catch (Exception unlockError) when (failure != null)
                { throw new AggregateException("Clipboard encoding and unlocking failed.", failure, unlockError); }
            }
            return memory;
        }
        catch (Exception failure)
        {
            try { Free(memory); }
            catch (Exception freeError) { throw new AggregateException("Clipboard allocation and cleanup failed.", failure, freeError); }
            throw;
        }
    }
    public void Publish(nint memory)
    {
        // Same replacement semantics as the previous GLFW setter. Used only by explicit clipboard writes.
        if (EmptyClipboard() == 0) throw Error("Failed to empty Windows clipboard.");
        if (SetClipboardData(UnicodeText, memory) == 0) throw Error("Failed to publish Windows clipboard text.");
    }
    public void Free(nint memory)
    { if (GlobalFree(memory) != 0) throw Error("Failed to free Windows clipboard text."); }
    private static void Unlock(nint memory)
    {
        if (GlobalUnlock(memory) == 0 && Marshal.GetLastPInvokeError() is var error && error != 0)
            throw new Win32Exception(error, "Failed to unlock Windows clipboard text.");
    }
    private static Win32Exception Error(string message) => new(Marshal.GetLastPInvokeError(), message);

    [LibraryImport("user32.dll", SetLastError = true)] private static partial int OpenClipboard(nint window);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int CloseClipboard();
    [LibraryImport("user32.dll")] private static partial int IsClipboardFormatAvailable(uint format);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint GetClipboardData(uint format);
    [LibraryImport("user32.dll", SetLastError = true)] private static partial int EmptyClipboard();
    [LibraryImport("user32.dll", SetLastError = true)] private static partial nint SetClipboardData(uint format, nint memory);
    [LibraryImport("kernel32.dll", SetLastError = true)] private static partial nint GlobalAlloc(uint flags, nuint bytes);
    [LibraryImport("kernel32.dll", SetLastError = true)] private static partial nint GlobalFree(nint memory);
    [LibraryImport("kernel32.dll", SetLastError = true)] private static partial nuint GlobalSize(nint memory);
    [LibraryImport("kernel32.dll", SetLastError = true)] private static partial nint GlobalLock(nint memory);
    [LibraryImport("kernel32.dll", SetLastError = true)] private static partial int GlobalUnlock(nint memory);
}
