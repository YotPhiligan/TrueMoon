using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentLaunchOptionsTests
{
    [Fact]
    public void DocumentSmoke_RequiresExplicitFixtureAndFreshOutputArgumentsWithoutFileActivation()
    {
        var parsed = EditorLaunchOptions.Parse(["--project", "not-created.aetherproj", "--smoke", "fresh-output", "--smoke-documents", "--validation"]);
        Assert.True(parsed.DocumentSmoke); Assert.True(parsed.Validation); Assert.False(parsed.LegacyProject);
        Assert.Equal(Path.GetFullPath("not-created.aetherproj"), parsed.ProjectPath); Assert.Equal(Path.GetFullPath("fresh-output"), parsed.SmokeDirectory);
        Assert.Null(parsed.CheckProjectPath); Assert.Null(parsed.CheckScenePath); Assert.Null(parsed.EntryAssembly);
        Assert.False(EditorLaunchOptions.Parse([]).DocumentSmoke);
    }

    [Theory]
    [InlineData("--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--smoke-documents")]
    [InlineData("--smoke", "output", "--smoke-documents")]
    [InlineData("--project", "game.csproj", "--smoke", "output", "--smoke-documents")]
    [InlineData("--check-project", "room.aetherproj", "--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--check-scene", "room.aetherscene", "--smoke-documents")]
    [InlineData("--project", "room.aetherproj", "--smoke", "output", "--smoke-documents", "--smoke-documents")]
    public void DocumentSmoke_RejectsMissingOrAmbiguousScope(params string[] arguments)
    { Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments)); }
}
