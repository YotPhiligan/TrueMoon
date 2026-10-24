using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class InspectorLaunchOptionsTests
{
    [Fact]
    public void InspectorSmoke_RequiresExplicitManifestAndOutputAndNormalizesWithoutOpeningFiles()
    {
        var parsed = EditorLaunchOptions.Parse(["--project", "fixtures/../not-created.AETHERPROJ", "--smoke", "output/../fresh-inspector-output", "--smoke-inspector", "--validation", "--light"]);
        Assert.True(parsed.InspectorSmoke); Assert.True(parsed.Validation); Assert.True(parsed.Light);
        Assert.False(parsed.DocumentSmoke); Assert.False(parsed.TransformSmoke); Assert.False(parsed.HierarchySmoke); Assert.False(parsed.LegacyProject);
        Assert.Equal(Path.GetFullPath("not-created.AETHERPROJ"), parsed.ProjectPath);
        Assert.Equal(Path.GetFullPath("fresh-inspector-output"), parsed.SmokeDirectory);
        Assert.Null(parsed.CheckProjectPath); Assert.Null(parsed.CheckScenePath); Assert.Null(parsed.EntryAssembly); Assert.Null(parsed.EntryType);
        Assert.False(EditorLaunchOptions.Parse([]).InspectorSmoke);
    }

    [Theory]
    [InlineData("--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke-inspector")]
    [InlineData("--smoke", "output", "--smoke-inspector")]
    [InlineData("--project", "game.csproj", "--smoke", "output", "--smoke-inspector")]
    [InlineData("--project", "room.txt", "--smoke", "output", "--smoke-inspector")]
    [InlineData("--check-project", "room.aetherproj", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--check-scene", "room.aetherscene", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-inspector", "--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-transform", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-hierarchy", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-inspector", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke", "duplicate", "--smoke-inspector")]
    [InlineData("--project", "room.aetherproj", "--smoke", " ", "--smoke-inspector")]
    public void InspectorSmoke_RejectsMissingManifestOrOutputLegacyMixedProfilesAndDuplicates(params string[] arguments)
    { Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments)); }
}
