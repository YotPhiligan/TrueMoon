using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildSourceWatchDescriptorTests
{
    [Fact]
    public void SourceFingerprint_IsOrderedCaseInsensitiveAndIndependentOfOutsideArtifactOwners()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SourceWatchDescriptor"));
        var project = Path.Combine(root, "game", "game.csproj"); var tree = Path.GetDirectoryName(project)!;
        var files = new[] { new GameBuildInputFile(project, 3, "ABC"), new GameBuildInputFile(Path.Combine(tree, "z.txt"), 4, "DEF") };
        var probes = new[] { new GameBuildSourceWatchProbe(Path.Combine(root, "optional.props"), false), new GameBuildSourceWatchProbe(project, true) };
        var first = new GameBuildSourceWatchDescriptor(project, new(Path.Combine(root, "artifacts-a")), files, [tree], probes, files.Select(file => file.Path));
        var second = new GameBuildSourceWatchDescriptor(project.ToUpperInvariant(), new(Path.Combine(root, "artifacts-b")),
            files.Reverse().Select(file => file with { Path = file.Path.ToUpperInvariant() }), [tree.ToUpperInvariant()],
            probes.Reverse().Select(probe => probe with { Path = probe.Path.ToUpperInvariant() }), files.Reverse().Select(file => file.Path.ToUpperInvariant()));
        Assert.Equal(first.SourceFingerprint, second.SourceFingerprint);
        var inside = new GameBuildSourceWatchDescriptor(project, new(Path.Combine(tree, "artifacts")), files, [tree], probes, first.TreeFiles);
        Assert.NotEqual(first.SourceFingerprint, inside.SourceFingerprint);
        var presence = new GameBuildSourceWatchDescriptor(project, first.Policy, files, [tree],
            probes.Select(probe => probe with { Exists = true }), first.TreeFiles);
        Assert.NotEqual(first.SourceFingerprint, presence.SourceFingerprint);
        var membership = new GameBuildSourceWatchDescriptor(project, first.Policy, files, [tree], probes, [project]);
        Assert.NotEqual(first.SourceFingerprint, membership.SourceFingerprint); // Same known files, different tree membership.
        // Mutating the caller's arrays cannot replace descriptor entries.
        files[0] = new(project, 100, "BAD"); probes[0] = probes[0] with { Exists = true };
        Assert.Equal(3, Assert.Single(first.Files, file => file.Path == project).Length);
        Assert.Contains(first.Probes, probe => !probe.Exists);
    }
}
