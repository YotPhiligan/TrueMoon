using System.ComponentModel;
using System.Runtime.InteropServices;
using TrueMoon.Alloy.Platform.Silk;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed partial class Win32ClipboardTests
{
    [Fact]
    public void NontextClipboard_ReturnsNullAndClosesBeforeNextRead()
    {
        var api = new ClipboardApi { TextAvailable = false };
        var clipboard = new Win32Clipboard(42, api);
        Assert.Null(clipboard.GetText());
        Assert.Null(clipboard.GetText());
        Assert.Equal(new[] { "open:42", "format", "close", "open:42", "format", "close" }, api.Calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void TransientAccessDenied_RetriesThenReadsAndCloses(int failures)
    {
        var api = new ClipboardApi { OpenFailures = failures };
        Assert.Equal("Привет 🧑‍💻 e\u0301", new Win32Clipboard(42, api).GetText());
        Assert.Equal(failures, api.Calls.Count(call => call == "wait"));
        Assert.Equal(failures + 1, api.Calls.Count(call => call == "open:42"));
        Assert.Equal(new[] { "format", "read", "close" }, api.Calls.TakeLast(3));
    }

    [Fact]
    public void PersistentAccessDenied_StopsAfterTenWaitsWithoutReadingOrClosing()
    {
        var api = new ClipboardApi { OpenFailures = 11 };
        var error = Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).GetText());
        Assert.Equal(5, error.NativeErrorCode);
        Assert.Equal(11, api.Calls.Count(call => call == "open:42"));
        Assert.Equal(10, api.Calls.Count(call => call == "wait"));
        Assert.DoesNotContain("format", api.Calls);
        Assert.DoesNotContain("close", api.Calls);
    }

    [Fact]
    public void OtherOpenError_IsReportedImmediatelyWithoutRetryOrClose()
    {
        var api = new ClipboardApi { OpenFailures = 1, OpenError = 1400 };
        var error = Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).GetText());
        Assert.Equal(1400, error.NativeErrorCode);
        Assert.Equal(new[] { "open:42" }, api.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatOrReadFailure_ClosesAndPreservesOriginalError(bool failRead)
    {
        var failure = new InvalidOperationException("Expected read failure.");
        var api = new ClipboardApi { FormatFailure = failRead ? null : failure, ReadFailure = failRead ? failure : null };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new Win32Clipboard(42, api).GetText()));
        Assert.Equal("close", api.Calls.Last());
        Assert.Single(api.Calls, call => call == "close");
        Assert.Equal(failRead, api.Calls.Contains("read"));
    }

    [Fact]
    public void CloseFailure_IsObservableAfterSuccessfulRead()
    {
        var failure = new Win32Exception(6);
        var api = new ClipboardApi { CloseFailure = failure };
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).GetText()));
        Assert.Equal(new[] { "open:42", "format", "read", "close" }, api.Calls);
    }

    [Fact]
    public void ReadAndCloseFailures_PreserveBothErrorsInOrder()
    {
        var read = new InvalidDataException("Expected malformed text.");
        var close = new Win32Exception(6);
        var api = new ClipboardApi { ReadFailure = read, CloseFailure = close };
        var error = Assert.Throws<AggregateException>(() => new Win32Clipboard(42, api).GetText());
        Assert.Equal(new Exception[] { read, close }, error.InnerExceptions);
        Assert.Single(api.Calls, call => call == "close");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Привет 🧑‍💻 e\u0301", "Привет 🧑‍💻 e\u0301")]
    [InlineData("first\0unused", "first")]
    public void DecodeText_UsesUtf16AndFirstTerminatorWithinBorrowedBuffer(string value, string expected)
    {
        var pointer = Marshal.StringToHGlobalUni(value);
        try { Assert.Equal(expected, Win32Clipboard.DecodeText(pointer, (nuint)(value.Length + 1) * 2)); }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    [Theory]
    [InlineData(0ul)]
    [InlineData(1ul)]
    [InlineData(3ul)]
    [InlineData(4294967296ul)]
    public void DecodeText_InvalidSizeIsRejectedBeforeReadingPointer(ulong bytes)
    { Assert.Throws<InvalidDataException>(() => Win32Clipboard.DecodeText(0, (nuint)bytes)); }

    [Fact]
    public void DecodeText_MissingTerminatorDoesNotReadBeyondDeclaredBuffer()
    {
        var pointer = Marshal.StringToHGlobalUni("ab");
        try { Assert.Throws<InvalidDataException>(() => Win32Clipboard.DecodeText(pointer, 4)); }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Привет 🧑‍💻 e\u0301")]
    public void WriterSuccess_PreparesBeforeOpenTransfersBeforeCloseAndDoesNotFree(string text)
    {
        var api = new ClipboardApi();
        new Win32Clipboard(42, api).SetText(text);
        Assert.Equal(text, api.AllocatedText);
        Assert.Equal(new[] { "allocate", "open:42", "publish:69", "close" }, api.Calls);
        Assert.True(api.Transferred);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void WriterTransientAccessDenied_RetainsDraftUntilPublish(int failures)
    {
        var api = new ClipboardApi { OpenFailures = failures };
        new Win32Clipboard(42, api).SetText("draft");
        Assert.Equal("allocate", api.Calls.First());
        Assert.Equal(failures, api.Calls.Count(call => call == "wait"));
        Assert.Equal(new[] { "publish:69", "close" }, api.Calls.TakeLast(2));
        Assert.True(api.Transferred);
        Assert.DoesNotContain("free:69", api.Calls);
    }

    [Theory]
    [InlineData(11, 5, 10)]
    [InlineData(1, 1400, 0)]
    public void WriterOpenFailure_FreesDraftWithoutPublishingOrClosing(int failures, int errorCode, int waits)
    {
        var api = new ClipboardApi { OpenFailures = failures, OpenError = errorCode };
        var error = Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).SetText("draft"));
        Assert.Equal(errorCode, error.NativeErrorCode);
        Assert.Equal(waits, api.Calls.Count(call => call == "wait"));
        Assert.Equal("free:69", api.Calls.Last());
        Assert.Single(api.Calls, call => call == "free:69");
        Assert.DoesNotContain("close", api.Calls);
        Assert.False(api.Transferred);
    }

    [Fact]
    public void AllocationFailure_DoesNotOpenOrPublish()
    {
        var failure = new Win32Exception(8);
        var api = new ClipboardApi { AllocationFailure = failure };
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).SetText("draft")));
        Assert.Equal(new[] { "allocate" }, api.Calls);
    }

    [Fact]
    public void PublishFailure_ClosesAndFreesUntransferredDraft()
    {
        var failure = new Win32Exception(5);
        var api = new ClipboardApi { PublishFailure = failure };
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).SetText("draft")));
        Assert.Equal(new[] { "allocate", "open:42", "publish:69", "close", "free:69" }, api.Calls);
        Assert.False(api.Transferred);
    }

    [Fact]
    public void PublishCloseAndFreeFailures_PreserveAllErrorsInOrder()
    {
        var publish = new Win32Exception(5); var close = new Win32Exception(6); var free = new Win32Exception(8);
        var api = new ClipboardApi { PublishFailure = publish, CloseFailure = close, FreeFailure = free };
        var error = Assert.Throws<AggregateException>(() => new Win32Clipboard(42, api).SetText("draft"));
        var operation = Assert.IsType<AggregateException>(error.InnerExceptions[0]);
        Assert.Equal(new Exception[] { publish, close }, operation.InnerExceptions);
        Assert.Same(free, error.InnerExceptions[1]);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal(new[] { "allocate", "open:42", "publish:69", "close", "free:69" }, api.Calls);
    }

    [Fact]
    public void SuccessfulPublishAndCloseFailure_NeverFreeSystemOwnedMemory()
    {
        var failure = new Win32Exception(6);
        var api = new ClipboardApi { CloseFailure = failure };
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => new Win32Clipboard(42, api).SetText("draft")));
        Assert.True(api.Transferred);
        Assert.Equal(new[] { "allocate", "open:42", "publish:69", "close" }, api.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("before\0after")]
    public void InvalidWriterText_IsRejectedBeforeAllocation(string? text)
    {
        var api = new ClipboardApi();
        if (text == null) Assert.Throws<ArgumentNullException>(() => new Win32Clipboard(42, api).SetText(text!));
        else Assert.Throws<ArgumentException>(() => new Win32Clipboard(42, api).SetText(text));
        Assert.Empty(api.Calls);
    }

    [WindowsTheory]
    [InlineData("")]
    [InlineData("Привет 🧑‍💻 e\u0301")]
    public void NativeGlobalMemory_Utf16RoundTripAndRepeatedReadReleaseBorrowedLock(string text)
    {
        var api = Win32ClipboardApi.Instance;
        var memory = api.AllocateText(text);
        Assert.NotEqual(0, memory);
        try
        {
            Assert.Equal(0u, GlobalFlags(memory) & 0xff); // Allocation returns unlocked memory.
            Assert.Equal(text, Win32ClipboardApi.ReadMemory(memory));
            Assert.Equal(0u, GlobalFlags(memory) & 0xff); // Borrowed read releases its lock.
            Assert.Equal(text, Win32ClipboardApi.ReadMemory(memory));
            Assert.Equal(0u, GlobalFlags(memory) & 0xff);
        }
        finally { api.Free(memory); }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GlobalFlags(nint memory);

    private sealed class WindowsTheoryAttribute : TheoryAttribute
    { public WindowsTheoryAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Win32 native memory requires Windows."; } }

    private sealed class ClipboardApi : IWin32ClipboardApi
    {
        internal readonly List<string> Calls = [];
        internal bool TextAvailable = true;
        internal int OpenFailures, OpenError = 5;
        internal Exception? FormatFailure, ReadFailure, CloseFailure;
        internal Exception? AllocationFailure, PublishFailure, FreeFailure;
        internal string? AllocatedText;
        internal bool Transferred;
        public int TryOpen(nint window) { Calls.Add($"open:{window}"); return OpenFailures-- > 0 ? OpenError : 0; }
        public void WaitForRetry() => Calls.Add("wait");
        public void Close() { Calls.Add("close"); if (CloseFailure != null) throw CloseFailure; }
        public bool HasText() { Calls.Add("format"); if (FormatFailure != null) throw FormatFailure; return TextAvailable; }
        public string ReadText() { Calls.Add("read"); if (ReadFailure != null) throw ReadFailure; return "Привет 🧑‍💻 e\u0301"; }
        public nint AllocateText(string text) { Calls.Add("allocate"); if (AllocationFailure != null) throw AllocationFailure; AllocatedText = text; return 69; }
        public void Publish(nint memory) { Calls.Add($"publish:{memory}"); if (PublishFailure != null) throw PublishFailure; Transferred = true; }
        public void Free(nint memory) { Calls.Add($"free:{memory}"); if (Transferred) throw new InvalidOperationException("System-owned memory was freed."); if (FreeFailure != null) throw FreeFailure; }
    }
}
