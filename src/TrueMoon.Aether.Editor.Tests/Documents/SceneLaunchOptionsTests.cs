using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class SceneLaunchOptionsTests
{
    [Fact]
    public void CheckScene_PreservesScenesRelativePathAndExplicitManifestWithoutSelectingLegacyRuntime()
    {
        using var files = new DocumentFiles();
        var options = EditorLaunchOptions.Parse(["--project", files.Project.ManifestPath, "--check-scene", "nested/../room.AETHERSCENE", "--light", "--validation"]);
        Assert.Equal(files.Project.ManifestPath, options.ProjectPath);
        Assert.Equal("nested/../room.AETHERSCENE", options.CheckScenePath); Assert.False(options.LegacyProject);
        Assert.Null(options.CheckProjectPath); Assert.Null(options.SmokeDirectory); Assert.Null(options.EntryAssembly); Assert.Null(options.EntryType);
        Assert.True(options.Light); Assert.True(options.Validation);
        Assert.False(File.Exists(files.Project.ManifestPath)); Assert.False(File.Exists(files.ScenePath));
    }

    [Theory]
    [InlineData("missing-project")] [InlineData("legacy")] [InlineData("check-project")] [InlineData("smoke")]
    [InlineData("extension")] [InlineData("missing-value")] [InlineData("duplicate")]
    public void CheckScene_RejectsAmbiguousOrUnsupportedLaunchBeforeAnyFileAccess(string kind)
    {
        using var files = new DocumentFiles();
        var arguments = kind switch
        {
            "missing-project" => new[] { "--check-scene", "room.aetherscene" },
            "legacy" => ["--project", files.Project.GameProjectPath, "--check-scene", "room.aetherscene"],
            "check-project" => ["--check-project", files.Project.ManifestPath, "--check-scene", "room.aetherscene"],
            "smoke" => ["--project", files.Project.ManifestPath, "--check-scene", "room.aetherscene", "--smoke", files.Root],
            "extension" => ["--project", files.Project.ManifestPath, "--check-scene", "room.json"],
            "missing-value" => ["--project", files.Project.ManifestPath, "--check-scene"],
            _ => ["--project", files.Project.ManifestPath, "--check-scene", "room.aetherscene", "--check-scene", "another.aetherscene"]
        };
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments));
        Assert.False(File.Exists(files.Project.ManifestPath)); Assert.False(File.Exists(files.ScenePath));
    }
}
