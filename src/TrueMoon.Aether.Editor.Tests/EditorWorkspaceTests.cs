using System.Globalization;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorWorkspaceTests
{
    public static TheoryData<SessionState, GameEditorOperation, bool, string> Availability => new()
    {
        { SessionState.Stopped, GameEditorOperation.Idle, false, "Build" },
        { SessionState.Stopped, GameEditorOperation.Idle, true, "Build,Start" },
        { SessionState.Paused, GameEditorOperation.Idle, false, "Build,Play,Step,Stop" },
        { SessionState.Paused, GameEditorOperation.Idle, true, "Build,Play,Step,Stop,Replace" },
        { SessionState.Playing, GameEditorOperation.Idle, true, "Build,Pause,Stop,Replace" },
        { SessionState.Playing, GameEditorOperation.Idle, false, "Build,Pause,Stop" },
        { SessionState.Starting, GameEditorOperation.Start, false, "Stop,Cancel" },
        { SessionState.Stopping, GameEditorOperation.Stop, false, "Stop" },
        { SessionState.CleanupFailed, GameEditorOperation.Idle, true, "Stop" },
        { SessionState.Faulted, GameEditorOperation.Idle, true, "Build,Start,Stop" },
        { SessionState.Paused, GameEditorOperation.Build, true, "Stop,Cancel" },
        { SessionState.Stopped, GameEditorOperation.Build, false, "Stop,Cancel" },
        { SessionState.Paused, GameEditorOperation.Replace, true, "Stop,Cancel" }
    };
    [Theory] [MemberData(nameof(Availability))]
    public void CommandAvailability_FollowsAcknowledgedRuntimeAndCandidate(SessionState runtime, GameEditorOperation operation, bool candidate, string expected)
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.Refresh(new(runtime), Status(operation, candidate));
        Assert.Equal(expected.Split(',').Order(StringComparer.Ordinal), Enum.GetValues<EditorCommand>().Where(workspace.CanExecute).Select(x => x.ToString()).Order(StringComparer.Ordinal));
    }
    [Fact]
    public void ClosedEditor_DisablesEveryCommandAndInvalidEnumIsRejected()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.Refresh(new(SessionState.Playing), Status(GameEditorOperation.Idle, true) with { Closed = true });
        Assert.All(Enum.GetValues<EditorCommand>(), command => Assert.False(workspace.CanExecute(command)));
        Assert.Throws<ArgumentOutOfRangeException>(() => workspace.CanExecute((EditorCommand)99));
    }
    [Fact]
    public void Selection_ChangesInspectorOnceAndInvalidSelectionPreservesIt()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var changes = 0; workspace.SelectionChanged += () => changes++;
        Assert.Equal("red-cube", workspace.SelectedId); Assert.Equal(7, workspace.EntityCount);
        workspace.Select("green-cube"); workspace.Select("green-cube");
        Assert.Equal(1, changes); Assert.Equal("green-cube", workspace.SelectedId);
        var rows = workspace.Properties().ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("1.5", rows["X"]); Assert.Equal("1", rows["Y"]); Assert.Equal("-1", rows["Z"]);
        Assert.Equal("0.2", rows["Красный"]); Assert.Equal("0.8", rows["Зелёный"]); Assert.Equal("0.3", rows["Синий"]); Assert.Equal("Да", rows["Интерактивен"]);
        Assert.Throws<ArgumentException>(() => workspace.Select("unknown")); Assert.Throws<ArgumentNullException>(() => workspace.Select(null!));
        Assert.Equal("green-cube", workspace.SelectedId); Assert.Equal(1, changes);
    }
    [Fact]
    public void EmptyScene_HasNoSelectionOrRowsAndRejectsUnknownEntity()
    {
        var workspace = new EditorWorkspace(new("empty", 0, [])); Assert.Null(workspace.SelectedId); Assert.Empty(workspace.EntityIds); Assert.Empty(workspace.Properties());
        Assert.Throws<ArgumentException>(() => workspace.Select("missing")); Assert.Null(workspace.SelectedId);
        Assert.Throws<ArgumentNullException>(() => new EditorWorkspace(null!));
    }
    [Fact]
    public void Inspector_UsesImmutableAuthoringSnapshotAndInvariantNumbers()
    {
        var source = new SceneEntity { Id = "object", X = 1.25f, Interactive = false }; var scene = new SceneSnapshot("test", 1, [source]); source.X = 99;
        var workspace = new EditorWorkspace(scene); var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU"); var rows = workspace.Properties().ToDictionary(x => x.Key, x => x.Value); Assert.Equal("1.25", rows["X"]); Assert.Equal("Группа", rows["Геометрия"]); Assert.Equal("Нет", rows["Интерактивен"]); Assert.False(rows.ContainsKey("Красный")); }
        finally { CultureInfo.CurrentCulture = culture; }
        scene.CopyEntities()[0].X = -999; Assert.Equal("1.25", workspace.Properties().Single(x => x.Key == "X").Value);
    }
    [Fact]
    public void Refresh_ReportsActualPidTickAndErrorPriorityWithoutResettingSelection()
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); workspace.Select("switch");
        workspace.Refresh(new(SessionState.Faulted, ProcessId: 1234, Tick: 17, Failure: new("runtime", new Exception("runtime"))), Status(GameEditorOperation.Build, false) with { Error = new Exception("build") }, new Exception("command"));
        Assert.Equal(1234, workspace.ProcessId); Assert.Equal(17UL, workspace.Tick); Assert.Equal("command", workspace.Error); Assert.Equal("switch", workspace.SelectedId);
        workspace.Refresh(new(SessionState.Stopped), Status(GameEditorOperation.Idle, false)); Assert.Null(workspace.Error); Assert.Null(workspace.ProcessId); Assert.Equal("switch", workspace.SelectedId);
    }
    [Theory]
    [InlineData(true, true, "build")]
    [InlineData(false, true, "runtime")]
    [InlineData(false, false, null)]
    public void Refresh_ErrorFallbackKeepsBuildAndRuntimeFailuresVisible(bool build, bool runtime, string? expected)
    {
        var workspace = new EditorWorkspace(EditorScene.Demo());
        workspace.Refresh(new(SessionState.Faulted, Failure: runtime ? new("runtime", new IOException("runtime")) : null),
            Status(GameEditorOperation.Idle, false) with { Error = build ? new IOException("build") : new OperationCanceledException() }, new OperationCanceledException());
        Assert.Equal(expected, workspace.Error);
    }
    internal static GameEditorStatus Status(GameEditorOperation operation, bool candidate) => new(1, null, operation,
        candidate ? new(null!, 1, null) : null, null, false);
}
