using System.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceFileTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(65535)] [InlineData(65536)] [InlineData(65537)]
    public async Task Open_SynchronousReadsPreserveBufferSlicesAndConsumeEveryByteBeforeEof(int length)
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "прочесть-λ-😀.bin");
        var expected = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
        File.WriteAllBytes(path, expected);
        var stamp = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path));
        Assert.True(source.CanRead); Assert.False(source.CanWrite); Assert.False(source.CanSeek);
        Assert.Equal(length, source.Before.Length); Assert.Equal(stamp, source.Before.Written);
        Assert.Equal((FileAttributes)0, source.Before.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint));
        using var actual = new MemoryStream(); var buffer = Enumerable.Repeat((byte)213, 65542).ToArray();
        while (true)
        {
            var pending = source.ReadAsync(buffer.AsMemory(3, 65536));
            Assert.True(pending.IsCompletedSuccessfully);
            var count = await pending; Assert.InRange(count, 0, 65536);
            Assert.Equal(new byte[] { 213, 213, 213 }, buffer[..3]);
            Assert.Equal(new byte[] { 213, 213, 213 }, buffer[^3..]);
            if (count == 0) break;
            actual.Write(buffer, 3, count);
        }
        Assert.Equal(expected, actual.ToArray()); Assert.Equal(length, actual.Length);
        Assert.Equal(0, source.Read(buffer, 3, 7)); Assert.Equal(0, source.Read(Span<byte>.Empty));
        source.Dispose(); source.Dispose(); Assert.False(source.CanRead);
        Assert.Throws<ObjectDisposedException>(() => source.Read(buffer, 0, 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        { var count = await source.ReadAsync(buffer); Assert.Fail($"Disposed reader unexpectedly returned {count} bytes."); });
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(length, exclusive.Length);
    }

    [Fact]
    public async Task Read_CancelledAsyncRequestDoesNotConsumeBytesAndDisposeReleasesNativeOwner()
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "cancel.bin");
        File.WriteAllBytes(path, [17, 31, 47, 63]);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var buffer = new byte[] { 99, 99, 99, 99 };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        { var count = await source.ReadAsync(buffer, cancellation.Token); Assert.Fail($"Cancelled reader unexpectedly returned {count} bytes."); });
        Assert.Equal(new byte[] { 99, 99, 99, 99 }, buffer);
        Assert.Equal(0, await source.ReadAsync(Memory<byte>.Empty));
        Assert.Equal(4, source.Read(buffer, 0, buffer.Length)); Assert.Equal(new byte[] { 17, 31, 47, 63 }, buffer);
        Assert.Equal(0, source.Read(buffer, 0, buffer.Length)); source.Dispose();
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(4, exclusive.Length);
    }

    [Fact]
    public void Open_SnapshotStaysOwnedWhileFreshMetadataAndReadWriteDeleteSharingTrackThePath()
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "sharing.bin"); var moved = path + ".moved";
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)); var before = source.Before;
        using (var peer = File.OpenRead(path)) Assert.Equal(1, peer.ReadByte());
        File.WriteAllBytes(path, [5, 6, 7, 8, 9]);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
        var current = Assert.IsType<EditorSourceFile.Snapshot>(EditorSourceFile.ReadMetadata(path));
        Assert.Equal(4, before.Length); Assert.Equal(5, current.Length); Assert.NotEqual(before.Written, current.Written);
        Assert.Equal(before, source.Before);
        File.Move(path, moved); Assert.Null(EditorSourceFile.ReadMetadata(path));
        Assert.Equal(current, EditorSourceFile.ReadMetadata(moved));
        File.Delete(moved); Assert.False(File.Exists(moved));
        var buffer = new byte[8]; Assert.Equal(5, source.Read(buffer, 1, 6));
        Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, buffer[1..6]); Assert.Equal(0, buffer[0]); Assert.Equal(0, buffer[^1]);
        Assert.Equal(0, source.Read(buffer, 0, buffer.Length)); source.Dispose();
        Assert.False(File.Exists(path)); Assert.False(File.Exists(moved)); Assert.Null(EditorSourceFile.ReadMetadata(moved));
    }

    [Theory]
    [InlineData("missing")] [InlineData("missing-parent")] [InlineData("directory")]
    public void Open_MissingPathsAndDirectoryOccupancyDoNotLeaveAReadOwner(string kind)
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "entry");
        if (kind == "missing-parent") path = Path.Combine(path, "missing.bin");
        if (kind == "directory") Directory.CreateDirectory(path);
        Assert.Null(EditorSourceFile.Open(path));
        var metadata = EditorSourceFile.ReadMetadata(path);
        if (kind != "directory") Assert.Null(metadata);
        else
        {
            var directory = Assert.IsType<EditorSourceFile.Snapshot>(metadata);
            Assert.True((directory.Attributes & FileAttributes.Directory) != 0);
            var moved = path + ".moved"; Directory.Move(path, moved);
            Assert.False(Directory.Exists(path)); Assert.True(Directory.Exists(moved));
            Directory.Move(moved, path);
        }
    }

    [Fact]
    public void Open_LockedFileRetainsTheSharingErrorAndRejectedCreationDoesNotLeak()
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "locked.bin"); File.WriteAllBytes(path, [7, 8, 9]);
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var error = Assert.Throws<IOException>(() => EditorSourceFile.Open(path));
            Assert.Equal(unchecked((int)0x80070020), error.HResult); Assert.Contains(path, error.Message);
        }
        using (var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)))
        { var buffer = new byte[3]; Assert.Equal(3, source.Read(buffer, 0, buffer.Length)); Assert.Equal(new byte[] { 7, 8, 9 }, buffer); }
        var moved = path + ".moved"; File.Move(path, moved); Assert.True(File.Exists(moved)); Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Open_PreexistingJunctionRejectsWithoutTraversingOrKeepingTheRejectedHandle()
    {
        using var files = new SourceWatchFiles(); var target = Path.Combine(files.Root, "target"); Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "retained.txt"); File.WriteAllText(marker, "target retained");
        var link = Path.Combine(files.Root, "junction"); await CreateJunction(link, target);
        try
        {
            var metadata = Assert.IsType<EditorSourceFile.Snapshot>(EditorSourceFile.ReadMetadata(link));
            Assert.True((metadata.Attributes & FileAttributes.ReparsePoint) != 0);
            var error = Assert.Throws<NotSupportedException>(() => EditorSourceFile.Open(link)); Assert.Contains("reparse", error.Message);
            Directory.Delete(link, false); Assert.False(Directory.Exists(link));
            Assert.True(Directory.Exists(target)); Assert.Equal("target retained", File.ReadAllText(marker));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link, false); }
    }

    [Fact]
    public void Open_UnicodeExtendedLengthPathUsesTheSameMetadataAndReadContract()
    {
        using var files = new SourceWatchFiles(); var directory = files.Root;
        for (var index = 0; directory.Length < 300; index++) directory = Path.Combine(directory, "длинный-путь-" + index.ToString("D2"));
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "λ-файл.bin"); File.WriteAllBytes(path, [19, 37, 53]);
        Assert.True(path.Length > 260);
        using (var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)))
        {
            Assert.Equal(source.Before, EditorSourceFile.ReadMetadata(path));
            var buffer = new byte[5]; Assert.Equal(3, source.Read(buffer, 1, 3));
            Assert.Equal(new byte[] { 0, 19, 37, 53, 0 }, buffer); Assert.Equal(0, source.Read(buffer, 0, 5));
        }
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(3, exclusive.Length);
    }

    [Theory]
    [InlineData(5)] [InlineData(32)] [InlineData(123)] [InlineData(995)]
    public void Failure_DeniedAndIoErrorsPreserveThePathAndNativeErrorCode(int code)
    {
        const string path = @"C:\source\input.bin"; var error = EditorSourceFile.Failure(code, path);
        if (code == 5) Assert.IsType<UnauthorizedAccessException>(error); else Assert.IsType<IOException>(error);
        Assert.Equal(unchecked((int)(0x80070000u | (uint)code)), error.HResult);
        Assert.Contains(path, error.Message); Assert.Contains("coverage failed", error.Message);
    }

    private static async Task CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Junction helper did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { child.Kill(true); await child.WaitForExitAsync(); throw; }
        var output = await stdout + await stderr; Assert.True(child.ExitCode == 0, output);
        Assert.True(Directory.Exists(link), output);
    }
}
