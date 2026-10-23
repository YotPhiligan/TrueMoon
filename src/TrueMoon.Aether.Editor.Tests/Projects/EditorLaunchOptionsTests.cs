using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Projects;

public sealed class EditorLaunchOptionsTests
{
    [Fact]
    public void EmptyLaunch_SelectsNoImplicitDemoAndNoProjectWorkspaceDisablesEveryCommand()
    {
        var options = EditorLaunchOptions.Parse([]);
        Assert.Equal(new EditorLaunchOptions(null, null, null, null, null, false, false), options);
        Assert.False(options.LegacyProject);
        var workspace = new EditorWorkspace(new SceneSnapshot("empty", 0, []), hasProject: false);
        Assert.False(workspace.HasProject);
        Assert.Empty(workspace.EntityIds);
        Assert.Null(workspace.SelectedId);
        Assert.Empty(workspace.Properties());
        foreach (var state in new[] { SessionState.Stopped, SessionState.Paused, SessionState.Playing, SessionState.CleanupFailed })
        {
            workspace.Refresh(new(state), new(1, null, GameEditorOperation.Build, new(null!, 1, null), null, false));
            Assert.All(Enum.GetValues<EditorCommand>(), command => Assert.False(workspace.CanExecute(command)));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.CanExecute((EditorCommand)999));
    }

    [Theory]
    [InlineData("room.aetherproj", false)]
    [InlineData("room.AETHERPROJ", false)]
    [InlineData("Game/room.CSPROJ", true)]
    public void ExplicitProject_NormalizesPathAndPreservesIndependentThemeOptions(string path, bool legacy)
    {
        var options = EditorLaunchOptions.Parse(["--validation", "--project", path, "--light"]);
        Assert.Equal(Path.GetFullPath(path), options.ProjectPath);
        Assert.Equal(legacy, options.LegacyProject);
        Assert.True(options.Light);
        Assert.True(options.Validation);
        Assert.Null(options.CheckProjectPath);
        Assert.Null(options.SmokeDirectory);
    }

    [Fact]
    public void LegacyProject_PreservesExplicitEntryAndSmokeWithoutTouchingFilesystem()
    {
        var options = EditorLaunchOptions.Parse(["--project", "Game/room.csproj", "--smoke", "output/smoke", "--assembly", "Legacy.dll", "--entry", "Legacy.Game"]);
        Assert.True(options.LegacyProject);
        Assert.Equal(Path.GetFullPath("output/smoke"), options.SmokeDirectory);
        Assert.Equal("Legacy.dll", options.EntryAssembly);
        Assert.Equal("Legacy.Game", options.EntryType);
        Assert.Null(options.CheckProjectPath);
    }

    [Fact]
    public void CheckProject_SelectsOnlyManifestAndAllowsProcessLocalFlags()
    {
        var options = EditorLaunchOptions.Parse(["--check-project", "room.aetherproj", "--light", "--validation"]);
        Assert.Equal(Path.GetFullPath("room.aetherproj"), options.CheckProjectPath);
        Assert.Null(options.ProjectPath);
        Assert.False(options.LegacyProject);
        Assert.True(options.Light);
        Assert.True(options.Validation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkspaceSmoke_AllowsEmptyOrManifestWorkspaceWithoutSelectingLegacyGame(bool manifest)
    {
        var options = EditorLaunchOptions.Parse(manifest
            ? ["--project", "room.aetherproj", "--smoke", "smoke"] : ["--smoke", "smoke"]);
        Assert.Equal(Path.GetFullPath("smoke"), options.SmokeDirectory);
        Assert.Equal(manifest ? Path.GetFullPath("room.aetherproj") : null, options.ProjectPath);
        Assert.False(options.LegacyProject);
        Assert.Null(options.EntryAssembly);
        Assert.Null(options.EntryType);
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "--unknown" },
        new[] { "--light", "--light" },
        new[] { "--project", "one.aetherproj", "--project", "two.aetherproj" },
        new[] { "--project" },
        new[] { "--project", " " },
        new[] { "--project", "--light" },
        new[] { "--project", "room.sln" },
        new[] { "--assembly", "Game.dll" },
        new[] { "--project", "room.aetherproj", "--entry", "Override.Game" },
        new[] { "--project", "room.aetherproj", "--assembly", "Override.dll" },
        new[] { "--check-project", "room.csproj" },
        new[] { "--check-project", "room.aetherproj", "--project", "Game.csproj" },
        new[] { "--check-project", "room.aetherproj", "--smoke", "smoke" },
        new[] { "--check-project", "room.aetherproj", "--assembly", "Game.dll" },
        new[] { "--check-project", "room.aetherproj", "--entry", "Game.Entry" }
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Parse_RejectsAmbiguousMissingDuplicateAndUnsupportedArguments(string[] arguments)
        => Assert.Throws<ArgumentException>(() => EditorLaunchOptions.Parse(arguments));
}
