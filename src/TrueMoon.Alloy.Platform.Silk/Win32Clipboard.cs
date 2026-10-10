using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrueMoon.Alloy.Platform.Silk;

// HWND/read memory are borrowed; write memory is owned until publication transfers it to Windows.
// Each successful Open is paired with Close.
internal sealed class Win32Clipboard(nint window, IWin32ClipboardApi api)
{
    internal string? GetText()
    {
        Open();
        Exception? failure = null;
        try { return api.HasText() ? api.ReadText() : null; }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            CloseAfter(failure);
        }
    }

    internal void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0')) throw new ArgumentException("Clipboard text cannot contain a null character.", nameof(text));
        var memory = api.AllocateText(text);
        Exception? failure = null;
        try
        {
            Open();
            Exception? publishFailure = null;
            try { api.Publish(memory); memory = 0; }
            catch (Exception error) { publishFailure = error; throw; }
            finally { CloseAfter(publishFailure); }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            if (memory != 0)
            {
                try { api.Free(memory); }
                catch (Exception freeError) when (failure != null)
                { throw new AggregateException("Clipboard writing and memory cleanup failed.", failure, freeError); }
            }
        }
    }

    private void CloseAfter(Exception? failure)
    {
        try { api.Close(); }
        catch (Exception closeError) when (failure != null)
        { throw new AggregateException("Clipboard operation and closing failed.", failure, closeError); }
    }

    internal static string DecodeText(nint pointer, nuint bytes)
    {
        if (bytes < 2 || bytes % 2 != 0 || bytes / 2 > int.MaxValue)
            throw new InvalidDataException("Windows clipboard text has an invalid size.");
        var chars = new char[(int)(bytes / 2)];
        Marshal.Copy(pointer, chars, 0, chars.Length);
        var end = Array.IndexOf(chars, '\0');
        if (end < 0) throw new InvalidDataException("Windows clipboard text is not null terminated.");
        return new string(chars, 0, end);
    }

    private void Open()
    {
        // Clipboard history and other applications briefly hold the process-wide lock.
        // Permit ten 10 ms waits; persistent failures remain observable.
        for (var attempt = 0; ; attempt++)
        {
            var error = api.TryOpen(window);
            if (error == 0) return;
            if (error != 5 || attempt == 10) throw new Win32Exception(error, "Failed to open Windows clipboard.");
            api.WaitForRetry();
        }
    }
}

internal interface IWin32ClipboardApi
{
    int TryOpen(nint window);
    void WaitForRetry();
    void Close();
    bool HasText();
    string ReadText();
    nint AllocateText(string text);
    void Publish(nint memory);
    void Free(nint memory);
}
