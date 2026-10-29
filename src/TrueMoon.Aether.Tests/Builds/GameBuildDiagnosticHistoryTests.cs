using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildDiagnosticHistoryTests
{
    [Fact]
    public async Task CompletedHistory_EvictsOnlyOldestAfterSixteenJobsAndSurvivesCompilerDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), "TrueMoon-build-history-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var project = Path.Combine(root, "NoBuildTarget.csproj"); await File.WriteAllTextAsync(project, "<Project />");
        await using var compiler = new GameBuildCompiler(new('A', 64), Path.Combine(root, "artifacts"));
        for (var revision = 1; revision <= 16; revision++)
            await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "None.dll", "None.Entry", revision)));
        var before = compiler.ReadDiagnostics(64, 32); Assert.Equal(16, before.Count); Assert.Equal(1, before[^1].SourceRevision);
        await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "None.dll", "None.Entry", 17)));
        var after = compiler.ReadDiagnostics(64, 32); Assert.Equal(16, after.Count); Assert.Equal(17, after[0].SourceRevision); Assert.Equal(2, after[^1].SourceRevision);
        Assert.Equal(1, before[^1].SourceRevision); Assert.All(after, item => { Assert.True(item.Completed); Assert.True(item.ResourcesReleased); Assert.Null(item.GameBuildId); });
        await compiler.DisposeAsync(); Assert.Equal(after, compiler.ReadDiagnostics(64, 32));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("History fixture escaped TEMP.");
        Directory.Delete(root, true);
    }
}
