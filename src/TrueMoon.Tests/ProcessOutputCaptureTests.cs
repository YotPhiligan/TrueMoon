using System.Text;
using Xunit;

namespace TrueMoon.Tests;

public sealed class ProcessOutputCaptureTests
{
    [Theory]
    [InlineData("", 8, "", 0)]
    [InlineData("1234567", 8, "1234567", 0)]
    [InlineData("12345678", 8, "12345678", 0)]
    [InlineData("123456789", 8, "23456789", 1)]
    [InlineData("abcdefghijklmno", 8, "hijklmno", 7)]
    public async Task Capture_RetainsExactTailAtAndAroundCapacity(string output, int capacity, string expected, long dropped)
    {
        using var reader = new StringReader(output);
        var capture = new ProcessOutputCapture(reader, capacity);
        await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        var snapshot = capture.Read();
        Assert.Equal(expected, snapshot.Text);
        Assert.Equal(output.Length, snapshot.TotalCharacters);
        Assert.Equal(dropped, snapshot.DroppedCharacters);
        Assert.Equal(dropped != 0, snapshot.IsTruncated);
        Assert.Equal(0, snapshot.FileBytesWritten);
        Assert.Equal(0, snapshot.FileBytesDropped);
        Assert.Null(snapshot.FileError);
    }

    [Fact]
    public async Task Capture_DrainsLongOutputWithoutNewlinesAndKeepsLatestCharacters()
    {
        var output = new string('x', 200_000) + "latest-output";
        using var reader = new StringReader(output);
        var capture = new ProcessOutputCapture(reader, 32);
        await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        var snapshot = capture.Read();
        Assert.Equal(output[^32..], snapshot.Text);
        Assert.Equal(output.Length, snapshot.TotalCharacters);
        Assert.Equal(output.Length - 32, snapshot.DroppedCharacters);
        Assert.True(snapshot.IsTruncated);
    }

    [Fact]
    public async Task TwoCaptures_ExposeLiveIndependentTailsBeforeEofAndBorrowReaders()
    {
        using var stdout = new PausedReader("stdout-progress");
        using var stderr = new PausedReader("stderr-failure");
        var first = new ProcessOutputCapture(stdout, 12);
        var second = new ProcessOutputCapture(stderr, 12);
        try
        {
            await Task.WhenAll(stdout.WaitingForEnd.Task, stderr.WaitingForEnd.Task).WaitAsync(TimeSpan.FromSeconds(5));
            var firstSnapshot = first.Read();
            Assert.Equal("out-progress", firstSnapshot.Text);
            Assert.Equal("derr-failure", second.Read().Text);
            Assert.Equal(15, firstSnapshot.TotalCharacters);
            Assert.Equal(3, firstSnapshot.DroppedCharacters);
            Assert.False(first.Completion.IsCompleted);
            Assert.False(second.Completion.IsCompleted);
            Assert.False(stdout.Disposed);
            Assert.False(stderr.Disposed);
            Assert.Equal(firstSnapshot, first.Read());
        }
        finally { stdout.End.TrySetResult(); stderr.End.TrySetResult(); }
        await Task.WhenAll(first.Completion, second.Completion).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stdout.Disposed);
        Assert.False(stderr.Disposed);
        Assert.Equal("out-progress", first.Read().Text);
        Assert.Equal("derr-failure", second.Read().Text);
    }

    [Theory]
    [InlineData(1, "A", 5)]
    [InlineData(4, "A", 5)]
    [InlineData(5, "A😀", 1)]
    [InlineData(6, "A😀B", 0)]
    public async Task FileCapture_RespectsUtf8ByteBudgetAcrossSplitSurrogateChunks(int budget, string expected, long dropped)
    {
        var directory = NewDirectory();
        var path = Path.Combine(directory, "capture.log");
        using var reader = new ChunkedReader("A\uD83D", "\uDE00B");
        try
        {
            var capture = new ProcessOutputCapture(reader, 16, path, budget);
            await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            var bytes = await File.ReadAllBytesAsync(path);
            var snapshot = capture.Read();
            Assert.Equal(expected, new UTF8Encoding(false, true).GetString(bytes));
            Assert.InRange(bytes.Length, 0, budget);
            Assert.Equal(bytes.Length, snapshot.FileBytesWritten);
            Assert.Equal(dropped, snapshot.FileBytesDropped);
            Assert.Equal("A😀B", snapshot.Text);
            Assert.Equal(4, snapshot.TotalCharacters);
            Assert.Equal(0, snapshot.DroppedCharacters);
            Assert.Equal(dropped != 0, snapshot.IsTruncated);
            Assert.Null(snapshot.FileError);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task TailEviction_DropsOrphanedLowSurrogateAtStart()
    {
        using var reader = new StringReader("A😀BC");
        var capture = new ProcessOutputCapture(reader, 3);
        await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        var snapshot = capture.Read();
        Assert.Equal("BC", snapshot.Text);
        Assert.Equal(5, snapshot.TotalCharacters);
        Assert.Equal(3, snapshot.DroppedCharacters);
        Assert.True(snapshot.IsTruncated);
    }

    [Fact]
    public async Task FileFailure_IsObservableAndDoesNotStopReadingOrTailRetention()
    {
        var directory = NewDirectory();
        // An existing directory cannot be opened as a file on supported platforms.
        var path = directory;
        var output = new string('a', 50_000) + "end-marker";
        using var reader = new StringReader(output);
        try
        {
            var capture = new ProcessOutputCapture(reader, 16, path, 100_000);
            await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            var snapshot = capture.Read();
            Assert.Equal(output[^16..], snapshot.Text);
            Assert.Equal(output.Length, snapshot.TotalCharacters);
            Assert.Equal(output.Length - 16, snapshot.DroppedCharacters);
            Assert.Equal(0, snapshot.FileBytesWritten);
            Assert.Equal(Encoding.UTF8.GetByteCount(output), snapshot.FileBytesDropped);
            Assert.Contains(nameof(UnauthorizedAccessException), snapshot.FileError);
            Assert.True(snapshot.IsTruncated);
            Assert.False(File.Exists(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ReaderFailure_FaultsCompletionAfterDrainingAlreadyCapturedFileChunks()
    {
        var directory = NewDirectory();
        var path = Path.Combine(directory, "capture.log");
        var error = new IOException("read failure");
        using var reader = new FailingReader(error);
        try
        {
            var capture = new ProcessOutputCapture(reader, 16, path);
            Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => capture.Completion.WaitAsync(TimeSpan.FromSeconds(5))));
            Assert.Equal("prefix", capture.Read().Text);
            Assert.Equal(6, capture.Read().TotalCharacters);
            Assert.Equal("prefix", await File.ReadAllTextAsync(path));
            Assert.Equal(6, capture.Read().FileBytesWritten);
            Assert.False(reader.Disposed);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1_048_577, 1)]
    [InlineData(8, 0)]
    [InlineData(8, -1)]
    public void InvalidBudgets_AreRejectedBeforeBorrowedReaderIsRead(int capacity, long bytes)
    {
        using var reader = new PausedReader("unread");
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessOutputCapture(reader, capacity, maxFileBytes: bytes));
        Assert.Equal(0, reader.Reads);
        Assert.False(reader.Disposed);
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrueMoon-Output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class PausedReader(string output) : TextReader
    {
        internal readonly TaskCompletionSource WaitingForEnd = Signal(), End = Signal();
        internal int Reads;
        internal bool Disposed;
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref Reads) == 1) { output.AsMemory().CopyTo(buffer); return output.Length; }
            WaitingForEnd.TrySetResult(); await End.Task; return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class ChunkedReader(params string[] chunks) : TextReader
    {
        private int _index;
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_index == chunks.Length) return ValueTask.FromResult(0);
            var chunk = chunks[_index++]; chunk.AsMemory().CopyTo(buffer); return ValueTask.FromResult(chunk.Length);
        }
    }

    private sealed class FailingReader(IOException error) : TextReader
    {
        private bool _read;
        internal bool Disposed;
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_read) return ValueTask.FromException<int>(error);
            _read = true; "prefix".AsMemory().CopyTo(buffer); return ValueTask.FromResult(6);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
