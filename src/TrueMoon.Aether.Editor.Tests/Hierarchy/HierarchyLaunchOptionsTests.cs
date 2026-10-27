using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class HierarchyLaunchOptionsTests
{
    [Fact]
    public void HierarchySmoke_RequiresExplicitManifestAndOutputAndNormalizesPathsWithoutOpeningFiles()
    {
        var parsed = EditorLaunchOptions.Parse(["--project", "fixtures/../not-created.AETHERPROJ", "--smoke", "output/../fresh-hierarchy-output", "--smoke-hierarchy", "--validation", "--light"]);
        Assert.True(parsed.HierarchySmoke); Assert.True(parsed.Validation); Assert.True(parsed.Light);
        Assert.False(parsed.DocumentSmoke); Assert.False(parsed.TransformSmoke); Assert.False(parsed.LegacyProject);
        Assert.Equal(Path.GetFullPath("not-created.AETHERPROJ"), parsed.ProjectPath);
        Assert.Equal(Path.GetFullPath("fresh-hierarchy-output"), parsed.SmokeDirectory);
        Assert.Null(parsed.CheckProjectPath); Assert.Null(parsed.CheckScenePath); Assert.Null(parsed.EntryAssembly); Assert.Null(parsed.EntryType);
        Assert.False(EditorLaunchOptions.Parse([]).HierarchySmoke);
    }

    [Theory]
    [InlineData("--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--smoke-hierarchy")]
    [InlineData("--smoke", "output", "--smoke-hierarchy")]
    [InlineData("--project", "game.csproj", "--smoke", "output", "--smoke-hierarchy")]
    [InlineData("--project", "room.txt", "--smoke", "output", "--smoke-hierarchy")]
    [InlineData("--check-project", "room.aetherproj", "--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--check-scene", "room.aetherscene", "--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-hierarchy", "--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-transform", "--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-hierarchy", "--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke", "duplicate", "--smoke-hierarchy")]
    [InlineData("--project", "room.aetherproj", "--smoke", " ", "--smoke-hierarchy")]
    public void HierarchySmoke_RejectsMissingManifestOrOutputLegacyMixedProfilesAndDuplicates(params string[] arguments)
    { Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments)); }
}
