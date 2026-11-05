using System.Diagnostics;
using TrueMoon.Aether.Editor.Editing;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceDirectoryPinTests
{
    [Fact]
    public void Open_NormalDirectoryFencesRenameUntilDisposeButPermitsChildEditingAndMembershipChanges()
    {
        using var files = new SourceWatchFiles();
        var directory = Path.Combine(files.Root, "pinned"); var moved = directory + "-moved"; Directory.CreateDirectory(directory);
        using var lease = EditorSourceDirectoryPin.Open(directory);
        Assert.NotNull(lease);
        Assert.ThrowsAny<IOException>(() => Directory.Move(directory, moved));
        Assert.True(Directory.Exists(directory)); Assert.False(Directory.Exists(moved));
        var child = Path.Combine(directory, "child.txt"); File.WriteAllText(child, "first");
        File.WriteAllText(child, "second"); Assert.Equal("second", File.ReadAllText(child));
        var renamedChild = Path.Combine(directory, "renamed.txt"); File.Move(child, renamedChild);
        Assert.False(File.Exists(child)); Assert.Equal("second", File.ReadAllText(renamedChild));
        File.Delete(renamedChild); Assert.False(File.Exists(renamedChild));
        var subtree = Path.Combine(directory, "subtree"); Directory.CreateDirectory(subtree); Directory.Delete(subtree);
        Assert.False(Directory.Exists(subtree));
        lease.Dispose(); lease.Dispose();
        Directory.Move(directory, moved); Assert.True(Directory.Exists(moved)); Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Open_MissingAndNonDirectoryReturnNoLeaseAndLeaveNoDeleteFence(bool fileExists)
    {
        using var files = new SourceWatchFiles(); var path = Path.Combine(files.Root, "unleased");
        if (fileExists) File.WriteAllText(path, "ordinary file");
        Assert.Null(EditorSourceDirectoryPin.Open(path));
        if (fileExists) { File.Delete(path); Assert.False(File.Exists(path)); }
        Directory.CreateDirectory(path); var moved = path + "-new"; Directory.Move(path, moved);
        Assert.True(Directory.Exists(moved)); Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task Open_PreexistingJunctionRejectsWithoutTraversingOrKeepingFailedPin()
    {
        using var files = new SourceWatchFiles(); var target = Path.Combine(files.Root, "target"); Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "proof.txt"); File.WriteAllText(marker, "target retained");
        var link = Path.Combine(files.Root, "junction"); var moved = link + "-moved";
        await CreateJunction(link, target);
        try
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            var error = Assert.Throws<NotSupportedException>(() => EditorSourceDirectoryPin.Open(link));
            Assert.Contains("reparse", error.Message);
            Directory.Move(link, moved); // Rejection must have released the OPEN_REPARSE_POINT handle.
            Assert.False(Directory.Exists(link)); Assert.True(Directory.Exists(moved));
            Assert.Equal("target retained", File.ReadAllText(marker));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link, false);
            if (Directory.Exists(moved)) Directory.Delete(moved, false);
        }
        Assert.True(Directory.Exists(target)); Assert.Equal("target retained", File.ReadAllText(marker));
    }

    [Fact]
    public async Task Cache_RejectsPreexistingAncestorJunctionAndReleasesItsProtectedNormalParents()
    {
        using var files = new SourceWatchFiles(); var target = Path.Combine(files.Root, "target"); Directory.CreateDirectory(target);
        var link = Path.Combine(files.Root, "junction"); await CreateJunction(link, target);
        try
        {
            using var cache = new EditorSourceDirectoryCache(default);
            Assert.Throws<NotSupportedException>(() => cache.ValidatePath(Path.Combine(link, "absent.props")));
            Assert.True(cache.Count > 0); cache.Dispose(); Assert.Equal(0, cache.Count);
            Directory.Delete(link, false);
            var moved = files.Root + "-cache-released";
            Directory.Move(files.Root, moved); Directory.Move(moved, files.Root);
            Assert.True(Directory.Exists(files.Root)); Assert.True(Directory.Exists(target));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link, false); }
    }

    private static async Task CreateJunction(string link, string target)
    {
        // Both paths are consumer-owned GUID fixture children. Only creation uses cmd's mklink builtin;
        // cleanup removes the junction itself with non-recursive Directory.Delete before fixture disposal.
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Junction helper did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { child.Kill(true); await child.WaitForExitAsync(); throw; }
        var output = await stdout + await stderr;
        Assert.True(child.ExitCode == 0, output);
        Assert.True(Directory.Exists(link), output);
    }
}
