using System.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceNativeMetadataTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReadMetadata_PreparedNameQueriesFreshFieldsWithoutChangingOpenedSnapshot(bool longUnicodeName)
    {
        using var files = new SourceWatchFiles(); var directory = files.Root;
        if (longUnicodeName)
            for (var index = 0; directory.Length < 300; index++) directory = Path.Combine(directory, "длинный-λ-путь-" + index);
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "metadata-😀.bin");
        File.WriteAllBytes(path, [3, 5, 7]);
        var stamp = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp); File.SetAttributes(path, FileAttributes.Archive | FileAttributes.Hidden);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)); var before = source.Before;
        Assert.Equal(3, before.Length); Assert.Equal(stamp.ToFileTimeUtc(), before.Written);
        Assert.Equal(FileAttributes.Archive | FileAttributes.Hidden, before.Attributes & RelevantAttributes);
        Assert.Equal(before, source.ReadMetadata());

        // CREATE_ALWAYS rejects an existing Hidden file without matching creation flags.
        // Editing through an existing handle preserves that attribute until the explicit change below.
        using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        { writer.SetLength(5); writer.Write(new byte[] { 11, 13, 17, 19, 23 }); }
        File.SetLastWriteTimeUtc(path, stamp.AddSeconds(2)); File.SetAttributes(path, FileAttributes.Archive);
        var changed = Assert.IsType<EditorSourceFile.Snapshot>(source.ReadMetadata());
        Assert.Equal(5, changed.Length); Assert.Equal(stamp.AddSeconds(2).ToFileTimeUtc(), changed.Written);
        Assert.Equal(FileAttributes.Archive, changed.Attributes & RelevantAttributes);
        Assert.Equal(changed, EditorSourceFile.ReadMetadata(path)); Assert.Equal(before, source.Before);

        File.SetLastWriteTimeUtc(path, stamp.AddSeconds(4));
        File.SetAttributes(path, FileAttributes.Archive | FileAttributes.Hidden);
        var changedAgain = Assert.IsType<EditorSourceFile.Snapshot>(source.ReadMetadata());
        Assert.Equal(5, changedAgain.Length); Assert.Equal(stamp.AddSeconds(4).ToFileTimeUtc(), changedAgain.Written);
        Assert.Equal(FileAttributes.Archive | FileAttributes.Hidden, changedAgain.Attributes & RelevantAttributes);
        Assert.NotEqual(changed, changedAgain); Assert.Equal(before, source.Before);
    }

    [Fact]
    public void ReadMetadata_QueriesDoNotConsumeDataAndDisposedOwnerCannotQueryAgain()
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "position.bin");
        File.WriteAllBytes(path, [5, 7, 11, 13]);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path));
        var buffer = new byte[2]; Assert.Equal(2, source.Read(buffer, 0, buffer.Length));
        Assert.Equal(new byte[] { 5, 7 }, buffer);
        for (var index = 0; index < 3; index++) Assert.Equal(source.Before, source.ReadMetadata());
        Assert.Equal(2, source.Read(buffer, 0, buffer.Length)); Assert.Equal(new byte[] { 11, 13 }, buffer);
        Assert.Equal(0, source.Read(buffer, 0, buffer.Length));
        source.Dispose(); source.Dispose(); Assert.False(source.CanRead);
        Assert.Throws<ObjectDisposedException>(() => source.ReadMetadata());
        Assert.Equal(4, Assert.IsType<EditorSourceFile.Snapshot>(EditorSourceFile.ReadMetadata(path)).Length);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(4, exclusive.Length);
    }

    [Fact]
    public void ReadMetadata_PreparedNameObservesReplacementInsteadOfFollowingMovedOpenedFile()
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "original.bin"); var moved = path + ".moved";
        File.WriteAllBytes(path, [17, 23]);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)); var before = source.Before;
        File.Move(path, moved); Assert.Null(source.ReadMetadata()); Assert.True(File.Exists(moved));
        File.WriteAllBytes(path, [31, 41, 43]);
        var written = new DateTime(2024, 5, 6, 7, 8, 8, DateTimeKind.Utc); File.SetLastWriteTimeUtc(path, written);
        var replacement = Assert.IsType<EditorSourceFile.Snapshot>(source.ReadMetadata());
        Assert.Equal(3, replacement.Length); Assert.Equal(written.ToFileTimeUtc(), replacement.Written);
        Assert.Equal(replacement, EditorSourceFile.ReadMetadata(path)); Assert.Equal(before, source.Before);
        var bytes = new byte[4]; Assert.Equal(2, source.Read(bytes, 1, 2));
        Assert.Equal(new byte[] { 0, 17, 23, 0 }, bytes); Assert.Equal(0, source.Read(bytes, 0, bytes.Length));
        source.Dispose();
        using var movedExclusive = new FileStream(moved, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var replacementExclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(2, movedExclusive.Length); Assert.Equal(3, replacementExclusive.Length);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReadMetadata_PreparedNameReportsDirectoryAndReparseOccupationBeforeClosingOriginalOwner(bool junction)
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "occupied.bin"); var moved = path + ".moved";
        var target = Path.Combine(files.Root, "target"); Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "marker.txt"); File.WriteAllText(marker, "target remains");
        File.WriteAllBytes(path, [47, 53]);
        using var source = Assert.IsType<EditorSourceFile>(EditorSourceFile.Open(path)); var before = source.Before;
        File.Move(path, moved);
        if (junction) await CreateJunction(path, target); else Directory.CreateDirectory(path);
        try
        {
            var occupied = Assert.IsType<EditorSourceFile.Snapshot>(source.ReadMetadata());
            Assert.True((occupied.Attributes & FileAttributes.Directory) != 0);
            Assert.Equal(junction, (occupied.Attributes & FileAttributes.ReparsePoint) != 0);
            if (junction) Assert.Throws<NotSupportedException>(() => EditorSourceFile.RejectReparse(occupied));
            var bytes = new byte[2]; Assert.Equal(2, source.Read(bytes, 0, bytes.Length));
            Assert.Equal(new byte[] { 47, 53 }, bytes); Assert.Equal(before, source.Before);
            Directory.Delete(path, false); Assert.False(Directory.Exists(path)); Assert.Null(source.ReadMetadata());
            Assert.True(Directory.Exists(target)); Assert.Equal("target remains", File.ReadAllText(marker));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, false); }
        source.Dispose();
        using var exclusive = new FileStream(moved, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(2, exclusive.Length);
    }

    private const FileAttributes RelevantAttributes = FileAttributes.Archive | FileAttributes.Hidden |
        FileAttributes.ReadOnly | FileAttributes.Directory | FileAttributes.ReparsePoint;

    private static async Task CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Junction helper did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { child.Kill(true); await child.WaitForExitAsync(); throw; }
        var output = await stdout + await stderr; Assert.True(child.ExitCode == 0, output); Assert.True(Directory.Exists(link), output);
    }
}
