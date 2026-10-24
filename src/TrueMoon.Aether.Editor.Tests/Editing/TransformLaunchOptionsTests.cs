using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class TransformLaunchOptionsTests
{
    [Fact]
    public void TransformSmoke_RequiresExplicitProjectAndOutputWithoutActivatingFiles()
    {
        var parsed = EditorLaunchOptions.Parse(["--project", "not-created.aetherproj", "--smoke", "fresh-transform-output", "--smoke-transform", "--validation"]);
        Assert.True(parsed.TransformSmoke); Assert.True(parsed.Validation); Assert.False(parsed.DocumentSmoke); Assert.False(parsed.LegacyProject);
        Assert.Equal(Path.GetFullPath("not-created.aetherproj"), parsed.ProjectPath); Assert.Equal(Path.GetFullPath("fresh-transform-output"), parsed.SmokeDirectory);
        Assert.Null(parsed.CheckProjectPath); Assert.Null(parsed.CheckScenePath); Assert.Null(parsed.EntryAssembly); Assert.False(EditorLaunchOptions.Parse([]).TransformSmoke);
    }

    [Theory]
    [InlineData("--smoke-transform")]
    [InlineData("--project", "room.aetherproj", "--smoke-transform")]
    [InlineData("--smoke", "output", "--smoke-transform")]
    [InlineData("--project", "game.csproj", "--smoke", "output", "--smoke-transform")]
    [InlineData("--check-project", "room.aetherproj", "--smoke-transform")]
    [InlineData("--project", "room.aetherproj", "--check-scene", "room.aetherscene", "--smoke-transform")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-transform", "--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-transform", "--smoke-transform")]
    public void TransformSmoke_RejectsMissingFixtureOutputLegacyAndMixedAcceptanceScopes(params string[] arguments)
    { Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments)); }
}
