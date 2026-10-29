using TrueMoon.Aether.Hosting;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class OwnedProcessOutputTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Drain_TimeoutRetainsStreamsAndRetryPersistsOutputBeforeClosingOnce(bool redirectInput)
    {
        var directory = NewDirectory();
        using var stdout = new GatedReader("out");
        using var stderr = new GatedReader("err");
        using var stdin = new TrackedWriter();
        var output = new OwnedProcessOutput(stdout, stderr, redirectInput ? stdin : null, directory, "runtime");
        try
        {
            await Task.WhenAll(stdout.Waiting.Task, stderr.Waiting.Task).WaitAsync(TimeSpan.FromSeconds(5));
            var error = await Assert.ThrowsAsync<TimeoutException>(() => output.DrainAsync(TimeSpan.Zero));
            Assert.Contains("after process reap", error.Message);
            Assert.Contains("stdout: read=", error.Message);
            Assert.Contains("stderr: read=", error.Message);
            Assert.Contains("characters=3", error.Message);
            Assert.False(output.Stdout.ReadCompletion.IsCompleted);
            Assert.False(output.Stderr.ReadCompletion.IsCompleted);
            Assert.Equal(0, stdout.Disposals);
            Assert.Equal(0, stderr.Disposals);
            Assert.Equal(0, stdin.Disposals);

            stdout.End.TrySetResult(); stderr.End.TrySetResult();
            await Task.WhenAll(output.DrainAsync(TimeSpan.FromSeconds(5)), output.DrainAsync(TimeSpan.FromSeconds(5)));
            await output.DrainAsync(TimeSpan.Zero);
            Assert.Equal(1, stdout.Disposals);
            Assert.Equal(1, stderr.Disposals);
            Assert.Equal(redirectInput ? 1 : 0, stdin.Disposals);
            Assert.True(output.Stdout.ReadCompletion.IsCompletedSuccessfully);
            Assert.True(output.Stdout.FileCompletion.IsCompletedSuccessfully);
            Assert.True(output.Stderr.ReadCompletion.IsCompletedSuccessfully);
            Assert.True(output.Stderr.FileCompletion.IsCompletedSuccessfully);
            Assert.Equal("out", await File.ReadAllTextAsync(Path.Combine(directory, "runtime.stdout.log")));
            Assert.Equal("err", await File.ReadAllTextAsync(Path.Combine(directory, "runtime.stderr.log")));
        }
        finally
        {
            stdout.End.TrySetResult(); stderr.End.TrySetResult();
            await output.DrainAsync(TimeSpan.FromSeconds(5));
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Drain_ReadFailurePreservesOriginalErrorAndClosesEveryStreamAfterFileDrain()
    {
        var directory = NewDirectory();
        var expected = new IOException("reader failed");
        using var stdout = new GatedReader("prefix", expected);
        using var stderr = new GatedReader("error");
        using var stdin = new TrackedWriter();
        stdout.End.TrySetResult(); stderr.End.TrySetResult();
        var output = new OwnedProcessOutput(stdout, stderr, stdin, directory, "runtime");
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => output.DrainAsync(TimeSpan.FromSeconds(5))));
            Assert.True(output.Stdout.ReadCompletion.IsFaulted);
            Assert.True(output.Stdout.FileCompletion.IsCompletedSuccessfully);
            Assert.Equal("prefix", await File.ReadAllTextAsync(Path.Combine(directory, "runtime.stdout.log")));
            Assert.Equal(1, stdout.Disposals);
            Assert.Equal(1, stderr.Disposals);
            Assert.Equal(1, stdin.Disposals);
            Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => output.DrainAsync(TimeSpan.FromSeconds(5))));
            Assert.Equal(1, stdout.Disposals);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Drain_CloseFailureAttemptsOtherStreamsAndRetriesOnlyUnreleasedOwner()
    {
        var directory = NewDirectory();
        var expected = new IOException("close failed once");
        using var stdout = new GatedReader("out") { CloseFailure = expected };
        using var stderr = new GatedReader("err");
        using var stdin = new TrackedWriter();
        stdout.End.TrySetResult(); stderr.End.TrySetResult();
        var output = new OwnedProcessOutput(stdout, stderr, stdin, directory, "runtime");
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => output.DrainAsync(TimeSpan.FromSeconds(5))));
            Assert.Equal(1, stdout.Disposals);
            Assert.Equal(1, stderr.Disposals);
            Assert.Equal(1, stdin.Disposals);
            await output.DrainAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, stdout.Disposals);
            Assert.Equal(1, stderr.Disposals);
            Assert.Equal(1, stdin.Disposals);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-output-drain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); return directory;
    }

    private sealed class GatedReader(string text, Exception? failure = null) : TextReader
    {
        internal readonly TaskCompletionSource Waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Disposals;
        internal Exception? CloseFailure;
        private bool _read;
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!_read) { _read = true; text.AsMemory().CopyTo(buffer); return text.Length; }
            Waiting.TrySetResult(); await End.Task;
            if (failure != null) throw failure;
            return 0;
        }
        protected override void Dispose(bool disposing)
        {
            Disposals++;
            if (CloseFailure is { } error) { CloseFailure = null; throw error; }
            base.Dispose(disposing);
        }
    }

    private sealed class TrackedWriter : StringWriter
    {
        internal int Disposals;
        protected override void Dispose(bool disposing) { Disposals++; base.Dispose(disposing); }
    }
}
