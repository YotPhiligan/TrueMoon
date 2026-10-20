using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Builds;
using TrueMoon.Aether.Tests.Replacements;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Tests.Editing;

public sealed class GameEditorTests(GameBuildFixture builds) : IClassFixture<GameBuildFixture>
{
    private static SceneSnapshot Scene() => new("room", 7, [new() { Id = "switch", Interactive = true }]);
    private GameEditorContext Context(GameBuildArtifact build) => new(new(builds.ProjectA, build.EntryAssembly, build.EntryType,
        build.SourceRevision, ["Game.Private.dll"]), Scene(), "switch");
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Build_PublishesCandidateWithoutLaunchingOrChangingRunningWorld()
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        var identity = c.Host.Supervisor.Status.Identity; var state = c.Factory.Leases[0].Game.CaptureCheckpoint();
        c.Host.Editor.SetContext(Context(builds.A)); var candidate = await c.Host.Editor.BuildAsync();
        Assert.Equal(7, candidate.SourceRevision); Assert.Equal(candidate.BuildId, GameBuildArtifact.Open(candidate.DirectoryPath, GameBuildFixture.Hash).BuildId);
        Assert.Equal(identity, c.Host.Editor.Status.Candidate!.Origin);
        Assert.Same(candidate, c.Host.Editor.Status.Candidate.Build); Assert.Equal(identity, c.Host.Supervisor.Status.Identity);
        Assert.Equal(state, c.Factory.Leases[0].Game.CaptureCheckpoint()); Assert.Single(c.Factory.Leases);
        Assert.Equal(GameEditorOperation.Idle, c.Host.Editor.Status.Operation); Assert.Null(c.Host.Editor.Status.Error);
    }

    [Fact]
    public async Task Context_SnapshotsDependenciesAndInvalidatesSelectionOnEveryChange()
    {
        await using var c = await CreateAsync(); var dependencies = new[] { "Game.Private.dll" };
        var context = Context(builds.A) with { Build = Context(builds.A).Build with { Dependencies = dependencies } };
        c.Host.Editor.SetContext(context); dependencies[0] = "changed.dll";
        await c.Host.Editor.SelectBuildAsync(builds.A); var selected = c.Host.Editor.Status;
        Assert.Equal(new[] { "Game.Private.dll" }, selected.Context!.Build.Dependencies);
        Assert.True(Path.IsPathFullyQualified(selected.Context.Build.ProjectPath)); Assert.Null(selected.Candidate!.Origin);
        c.Host.Editor.SetContext(selected.Context);
        Assert.Equal(selected.ContextVersion + 1, c.Host.Editor.Status.ContextVersion); Assert.Null(c.Host.Editor.Status.Candidate);
        Assert.Equal("switch", c.Host.Editor.Status.Context!.Selection); Assert.Empty(c.Factory.Leases);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Replace_UsesExplicitSelectionAndPreservesStateAndMode(bool playing)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        for (var i = 0; i < 5; i++) await c.Host.Supervisor.StepAsync();
        var state = c.Factory.Leases[0].Game.CaptureCheckpoint(); var old = c.Host.Supervisor.Status.Identity;
        c.Host.Editor.SetContext(Context(builds.B)); await c.Host.Editor.SelectBuildAsync(builds.B);
        if (playing) await c.Host.Editor.PlayAsync();
        var result = await c.Host.Editor.ReplaceAsync();
        Assert.Equal(old, result.PreviousIdentity); Assert.Equal(builds.B.BuildId, result.Identity.GameBuildId);
        Assert.Equal(state, result.Checkpoint.State); Assert.Equal(state, c.Factory.Leases[1].Game.CaptureCheckpoint());
        Assert.Equal(playing ? SessionState.Playing : SessionState.Paused, c.Host.Supervisor.Status.State);
        Assert.Null(c.Host.Editor.Status.Candidate); Assert.Null(c.Host.Editor.Status.Error); Assert.Equal(1, c.Factory.MaxLive);
        await c.Host.Editor.PauseAsync(); await c.Host.Editor.StepAsync(); Assert.Equal(state.Tick + 1, c.Host.Supervisor.Status.Tick);
        await c.Host.Editor.StopAsync(); Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased));
    }

    [Theory]
    [InlineData("source")] [InlineData("entry")]
    public async Task Select_MismatchDoesNotPauseOrStopOldRuntime(string mismatch)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A); var old = c.Host.Supervisor.Status;
        var context = Context(builds.B);
        c.Host.Editor.SetContext(context with { Build = mismatch == "source" ? context.Build with { SourceRevision = 10 } : context.Build with { EntryType = "Other.Game" } });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => c.Host.Editor.SelectBuildAsync(builds.B));
        Assert.Same(error, c.Host.Editor.Status.Error); Assert.Null(c.Host.Editor.Status.Candidate); Assert.Equal(old, c.Host.Supervisor.Status);
        Assert.Single(c.Factory.Leases); Assert.Equal(0, c.Factory.Leases[0].Stops);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RawSessionChange_HidesCandidateAndRejectsReplaceBeforeApplyingIt(bool pollStatus)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        c.Host.Editor.SetContext(Context(builds.B)); await c.Host.Editor.SelectBuildAsync(builds.B);
        await c.Host.Frames.StopAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A); var old = c.Host.Supervisor.Status.Identity;
        if (pollStatus) { Assert.Null(c.Host.Editor.Status.Candidate); await Assert.ThrowsAsync<InvalidOperationException>(() => c.Host.Editor.ReplaceAsync()); }
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.Host.Editor.ReplaceAsync());
        Assert.Equal(old, c.Host.Supervisor.Status.Identity); Assert.Equal(2, c.Factory.Leases.Count); Assert.Equal(0, c.Factory.Leases[1].Stops);
    }

    [Theory]
    [InlineData("context")] [InlineData("cancel")] [InlineData("stop")] [InlineData("close")]
    public async Task HeldBuild_InterruptDrainsOwnedCompilerAndNeverSelectsLateArtifact(string interrupt)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        var held = builds.HeldProject(); SetHeld(c, held.Project); var building = c.Host.Editor.BuildAsync();
        await TestWait.UntilAsync(() => File.Exists(held.Marker), TimeSpan.FromSeconds(15));
        var compiler = c.Host.Builds.ProcessId!.Value; var worker = int.Parse(File.ReadAllText(held.Marker));
        Assert.Equal(GameEditorOperation.Build, c.Host.Editor.Status.Operation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Host.Editor.BuildAsync());
        switch (interrupt)
        {
            case "context": c.Host.Editor.SetContext(Context(builds.B)); break;
            case "cancel": await c.Host.Editor.CancelAsync(); break;
            case "stop": await c.Host.Editor.StopAsync(); break;
            default: await c.App.StopAsync(); break;
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building);
        Assert.Null(c.Host.Editor.Status.Candidate); Assert.Null(c.Host.Builds.ProcessId);
        Assert.True(Exited(compiler)); Assert.True(Exited(worker)); Assert.Equal(GameEditorOperation.Idle, c.Host.Editor.Status.Operation);
        Assert.IsAssignableFrom<OperationCanceledException>(c.Host.Editor.Status.Error);
        if (interrupt is "stop" or "close") { Assert.Equal(SessionState.Stopped, c.Host.Supervisor.Status.State); Assert.True(c.Factory.Leases[0].ResourcesReleased); }
        else { Assert.Equal(SessionState.Paused, c.Host.Supervisor.Status.State); Assert.False(c.Factory.Leases[0].ResourcesReleased); }
        if (interrupt == "close") await Assert.ThrowsAsync<ObjectDisposedException>(() => c.Host.Editor.BuildAsync());
    }

    [Fact]
    public async Task CompletedBuild_ForAnotherSessionIsRejectedEvenWithoutCallerCancellation()
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        var held = builds.HeldProject(); SetHeld(c, held.Project); var building = c.Host.Editor.BuildAsync();
        await TestWait.UntilAsync(() => File.Exists(held.Marker), TimeSpan.FromSeconds(15));
        await c.Host.Frames.StopAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A); var current = c.Host.Supervisor.Status.Identity;
        File.WriteAllText(held.Release, "release"); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building);
        Assert.Null(c.Host.Editor.Status.Candidate); Assert.Equal(current, c.Host.Supervisor.Status.Identity);
        Assert.Equal(2, c.Factory.Leases.Count); Assert.Null(c.Host.Builds.ProcessId);
        Assert.NotEmpty(Directory.GetFiles(c.Host.Builds.DiagnosticsDirectory!, "build.stdout.log"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReplaceFailure_RetainsOriginalAndRecoveryErrorsUntilNextCommand(bool rollbackFails)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        var original = new IOException("candidate"); var recovery = new IOException("recovery");
        c.Factory.Configure = l => l.BeforeSend = (_, _) => throw (c.Factory.Leases.Count == 2 ? original : recovery);
        if (!rollbackFails) c.Factory.Configure = l => { if (c.Factory.Leases.Count == 2) l.BeforeSend = (_, _) => throw original; };
        c.Host.Editor.SetContext(Context(builds.B)); await c.Host.Editor.SelectBuildAsync(builds.B);
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => c.Host.Editor.ReplaceAsync());
        Assert.Same(error, c.Host.Editor.Status.Error); Assert.Same(original, error.InnerException);
        Assert.Equal(!rollbackFails, error.RollbackSucceeded); Assert.Equal(3, c.Factory.Leases.Count);
        if (rollbackFails) Assert.Same(recovery, error.RecoveryError); else Assert.Null(error.RecoveryError);
        await c.Host.Editor.StopAsync(); Assert.Same(error, c.Host.Editor.Status.Error);
        Assert.Null(c.Host.Editor.Status.Candidate); Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased));
    }

    [Theory]
    [InlineData("cancel")] [InlineData("context")] [InlineData("stop")] [InlineData("close")]
    public async Task HeldReplace_CancelRecoversButStopAndCloseNeverLaunchRollback(string interrupt)
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        var entered = Signal(); c.Factory.Configure = l =>
        {
            if (c.Factory.Leases.Count == 2) l.BeforeSend = async (r, t) =>
            { if (r.Kind == (int)CommandKind.RestoreCheckpoint) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, t); } };
        };
        c.Host.Editor.SetContext(Context(builds.B)); await c.Host.Editor.SelectBuildAsync(builds.B); var replacing = c.Host.Editor.ReplaceAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        switch (interrupt)
        {
            case "context": c.Host.Editor.SetContext(Context(builds.A)); break;
            case "cancel": await c.Host.Editor.CancelAsync(); break;
            case "stop": await c.Host.Editor.StopAsync(); break;
            default: await c.App.StopAsync(); break;
        }
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.Same(error, c.Host.Editor.Status.Error); Assert.Equal(interrupt is "cancel" or "context", error.RollbackSucceeded);
        Assert.Equal(interrupt is "cancel" or "context" ? 3 : 2, c.Factory.Leases.Count);
        Assert.Equal(interrupt is "cancel" or "context" ? SessionState.Paused : SessionState.Stopped, c.Host.Supervisor.Status.State);
        if (interrupt == "context") Assert.Null(c.Host.Editor.Status.Candidate);
        if (interrupt is "stop" or "close") Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased));
    }

    [Fact]
    public async Task StopFailure_RetainsOwnedLeaseAndAllowsExplicitCleanupRetry()
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A);
        c.Host.Editor.SetContext(Context(builds.B)); c.Factory.Leases[0].FailStop = true;
        try
        {
            var error = await Assert.ThrowsAsync<IOException>(() => c.Host.Editor.StopAsync());
            Assert.Same(error, c.Host.Editor.Status.Error); Assert.False(c.Factory.Leases[0].ResourcesReleased);
            Assert.Equal(SessionState.CleanupFailed, c.Host.Supervisor.Status.State);
        }
        finally { c.Factory.Leases[0].FailStop = false; }
        await c.Host.Editor.StopAsync(); Assert.True(c.Factory.Leases[0].ResourcesReleased); Assert.Equal(2, c.Factory.Leases[0].Stops);
        Assert.Equal(SessionState.Stopped, c.Host.Supervisor.Status.State);
    }

    [Fact]
    public async Task Start_SelectedBuildWaitsForPresentationThenClearsCandidate()
    {
        await using var c = await CreateAsync(); c.Factory.Produce = true;
        c.Host.Editor.SetContext(Context(builds.A)); await c.Host.Editor.SelectBuildAsync(builds.A);
        var starting = c.Host.Editor.StartAsync(32, 16);
        await TestWait.UntilAsync(() => c.Host.Frames.Display?.Receiver.FirstFrame.IsCompletedSuccessfully == true, TimeSpan.FromSeconds(3));
        Assert.False(starting.IsCompleted); Assert.Equal(GameEditorOperation.Start, c.Host.Editor.Status.Operation);
        var display = c.Host.Frames.Display!; using (var frame = Assert.IsType<FrameLease>(display.Receiver.TakeLatest())) c.Host.Frames.Presented(display, frame.Metadata);
        await starting; Assert.Equal(builds.A.BuildId, c.Host.Supervisor.Status.Identity!.GameBuildId);
        Assert.Null(c.Host.Editor.Status.Candidate); Assert.Equal(SessionState.Paused, c.Host.Supervisor.Status.State);
        await c.Host.Editor.StopAsync(); Assert.Equal(0, c.Host.Frames.MappingCount); Assert.True(c.Factory.Leases[0].ResourcesReleased);
    }

    [Fact]
    public async Task ConcurrentStop_SharesCompletionAndRejectsCommandsUntilOwnedReapCompletes()
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A); c.Host.Editor.SetContext(Context(builds.A));
        var entered = Signal(); var release = Signal(); c.Factory.Leases[0].BeforeStop = async () => { entered.TrySetResult(); await release.Task; };
        var stopping = c.Host.Editor.StopAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            Assert.Same(stopping, c.Host.Editor.StopAsync()); Assert.False(stopping.IsCompleted); Assert.Null(c.Host.Editor.Status.Candidate);
            Assert.Equal(GameEditorOperation.Stop, c.Host.Editor.Status.Operation);
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.Host.Editor.PlayAsync());
        }
        finally { release.TrySetResult(); }
        await stopping; Assert.True(c.Factory.Leases[0].ResourcesReleased); Assert.Equal(1, c.Factory.Leases[0].Stops);
        Assert.Equal(GameEditorOperation.Idle, c.Host.Editor.Status.Operation);
    }

    [Fact]
    public async Task CompilerError_PreservesActiveWorldAndRetainsDiagnosticsForEditor()
    {
        await using var c = await CreateAsync(); await c.Host.Supervisor.StartAsync(Scene(), builds.A); var identity = c.Host.Supervisor.Status.Identity;
        var project = builds.Project("invalid-editor", "Game.Invalid", "invalid C# compilation");
        c.Host.Editor.SetContext(new(new(project, "Game.Invalid.dll", "Game.Candidate.Game", 10), Scene(), "switch"));
        var error = await Assert.ThrowsAsync<GameBuildException>(() => c.Host.Editor.BuildAsync());
        Assert.Same(error, c.Host.Editor.Status.Error); Assert.NotEqual(0, error.ExitCode); Assert.Null(c.Host.Editor.Status.Candidate);
        Assert.True(File.Exists(Path.Combine(error.DiagnosticsDirectory, "build.stdout.log"))); Assert.Equal(identity, c.Host.Supervisor.Status.Identity);
        Assert.Equal(0, c.Factory.Leases[0].Stops); Assert.Null(c.Host.Builds.ProcessId);
    }

    private static void SetHeld(Owned c, string project) => c.Host.Editor.SetContext(new(new(project, "Game.Held.dll", "Game.Candidate.Game", 99), Scene(), "switch"));
    private static bool Exited(int pid) { try { using var p = System.Diagnostics.Process.GetProcessById(pid); return p.HasExited; } catch (ArgumentException) { return true; } }
    private async Task<Owned> CreateAsync()
    {
        var factory = new ReplacementRuntimeFixture();
        var app = App.Build(c => c.UseAether(o =>
        {
            o.HostContractId = GameBuildFixture.Hash; o.GameBuildId = GameBuildFixture.Hash;
            o.ReportDirectory = Path.Combine(builds.Root, "reports-" + Guid.NewGuid().ToString("N")); o.RuntimeFactory = _ => factory;
            o.Deadlines = new() { HeartbeatInterval = TimeSpan.FromSeconds(30) };
        }));
        await app.StartAsync(); return new(app, app.Services.Resolve<AetherHost>()!, factory);
    }
    private sealed record Owned(IApp App, AetherHost Host, ReplacementRuntimeFixture Factory) : IAsyncDisposable
    { public async ValueTask DisposeAsync() => await App.DisposeAsync(); }
}
