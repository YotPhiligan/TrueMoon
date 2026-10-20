using System.Diagnostics;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Builds;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Replacements;

public sealed class GameReplacementTests(GameBuildFixture builds) : IClassFixture<GameBuildFixture>
{
    private static SceneSnapshot Scene() => new("room", 7, [new() { Id = "switch", Interactive = true }]);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ReplacementDeadlines Fast() => new() { Total = TimeSpan.FromSeconds(2), Forward = TimeSpan.FromMilliseconds(350), Cleanup = TimeSpan.FromMilliseconds(250) };
    private GameReplacementRequest Request(Context c) => new(builds.B, c.Supervisor.Status.Identity!, Scene(), 8);

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task Replace_PreservesCheckpointAndPreviousModeOnFreshSelectedGeneration(bool playing, bool viewport)
    {
        await using var c = new Context(); await c.StartAsync(builds.A, viewport);
        var old = c.Supervisor.Status.Identity!; var before = c.Factory.Leases[0].Game.CaptureCheckpoint();
        await c.Supervisor.SetPlayingAsync(playing);
        var replacing = c.Replacement.ReplaceAsync(Request(c));
        if (viewport)
        {
            await TestWait.UntilAsync(() => c.Frames.Display is { } d && d.Surface.Identity.Generation == old.Generation + 1 && d.Receiver.FirstFrame.IsCompletedSuccessfully, TimeSpan.FromSeconds(3));
            var display = c.Frames.Display!; using var frame = Assert.IsType<FrameLease>(display.Receiver.TakeLatest());
            Assert.Equal(before.Tick, frame.Metadata.Tick); Assert.False(replacing.IsCompleted); c.Frames.Presented(display, frame.Metadata);
        }
        await PumpAsync(c.Frames, replacing);
        var result = await replacing; var next = c.Factory.Leases[1];
        Assert.Equal(before, result.Checkpoint.State); Assert.Equal(before, next.Game.CaptureCheckpoint());
        Assert.Equal(playing, result.Playing); Assert.Equal(playing ? SessionState.Playing : SessionState.Paused, c.Supervisor.Status.State);
        Assert.Equal(old, result.PreviousIdentity); Assert.Equal(builds.B.BuildId, result.Identity.GameBuildId);
        Assert.Equal(old.Generation + 1, result.Identity.Generation); Assert.NotEqual(old.Nonce, result.Identity.Nonce);
        Assert.True(c.Factory.Leases[0].ResourcesReleased); Assert.False(next.ResourcesReleased); Assert.Equal(1, c.Factory.MaxLive);
        Assert.Equal(viewport ? 1 : 0, c.Frames.MappingCount);
        Assert.Equal(new[] { CommandKind.Hello, CommandKind.LoadScene, CommandKind.RestoreCheckpoint, CommandKind.SetMode }, next.Commands.Take(4));
        await c.Frames.StopAsync(); Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased));
    }
    [Theory]
    [InlineData("hello", false)] [InlineData("hello", true)] [InlineData("restore", false)]
    [InlineData("frame", false)] [InlineData("mode", false)]
    public async Task CandidateFailure_RestoresOldBuildStatePresentationAndModeOnce(string failure, bool fixedOld)
    {
        var expected = new IOException("candidate failure");
        await using var c = new Context(Fast());
        await c.StartAsync(fixedOld ? null : builds.A, true); await c.Supervisor.SetPlayingAsync(true);
        var original = c.Supervisor.Status.Identity!; var state = c.Factory.Leases[0].Game.CaptureCheckpoint();
        c.Factory.Configure = lease =>
        {
            if (c.Factory.Leases.Count != 2) return;
            lease.RejectRestore = failure == "restore"; lease.NoFrames = failure == "frame";
            lease.BeforeSend = (request, _) =>
            { if (failure == "hello" && request.Kind == (int)CommandKind.Hello || failure == "mode" && request.Kind == (int)CommandKind.SetMode && request.Playing) throw expected; return Task.CompletedTask; };
        };
        var replacing = c.Replacement.ReplaceAsync(Request(c)); await PumpAsync(c.Frames, replacing);
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.True(error.RollbackAttempted); Assert.True(error.RollbackSucceeded); Assert.Null(error.RecoveryError);
        if (failure is "hello" or "mode") Assert.Same(expected, error.InnerException);
        Assert.Equal(3, c.Factory.Leases.Count); Assert.True(c.Factory.Leases[1].ResourcesReleased);
        Assert.Equal(original.GameBuildId, c.Supervisor.Status.Identity!.GameBuildId);
        Assert.Equal(original.Generation + 2, c.Supervisor.Status.Identity.Generation); Assert.NotEqual(original.Nonce, c.Supervisor.Status.Identity.Nonce);
        Assert.Equal(state, c.Factory.Leases[2].Game.CaptureCheckpoint()); Assert.Equal(SessionState.Playing, c.Supervisor.Status.State);
        Assert.Equal(1, c.Frames.MappingCount); Assert.Equal(1, c.Factory.MaxLive);
    }
    [Theory]
    [InlineData("hello")] [InlineData("restore")] [InlineData("frame")] [InlineData("mode")]
    public async Task RollbackFailure_RetainsOriginalCandidateErrorWithoutSecondAttempt(string failure)
    {
        var original = new IOException("original candidate"); var recovery = new IOException("rollback failure");
        await using var c = new Context(Fast()); await c.StartAsync(builds.A, true); await c.Supervisor.SetPlayingAsync(true);
        c.Factory.Configure = lease =>
        {
            if (c.Factory.Leases.Count == 2) lease.BeforeSend = (_, _) => throw original;
            if (c.Factory.Leases.Count != 3) return;
            lease.RejectRestore = failure == "restore"; lease.NoFrames = failure == "frame";
            lease.BeforeSend = (r, _) =>
            { if (failure == "hello" && r.Kind == (int)CommandKind.Hello || failure == "mode" && r.Kind == (int)CommandKind.SetMode && r.Playing) throw recovery; return Task.CompletedTask; };
        };
        var replacing = c.Replacement.ReplaceAsync(Request(c)); await PumpAsync(c.Frames, replacing);
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.Same(original, error.InnerException); Assert.Equal("Candidate", error.Phase);
        Assert.True(error.RollbackAttempted); Assert.False(error.RollbackSucceeded); Assert.NotNull(error.RecoveryError);
        if (failure is "hello" or "mode") Assert.Same(recovery, error.RecoveryError);
        if (failure == "restore") Assert.IsType<CheckpointRejectedException>(error.RecoveryError);
        if (failure == "frame") Assert.IsAssignableFrom<OperationCanceledException>(error.RecoveryError);
        Assert.Equal(3, c.Factory.Leases.Count); Assert.Equal(1, c.Factory.MaxLive);
        Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased)); Assert.Equal(0, c.Frames.MappingCount);
        Assert.Equal(SessionState.Faulted, c.Supervisor.Status.State);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConfirmedCaptureRejection_PreservesOldProcessFrameAndPreviousMode(bool playing)
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true); await c.Supervisor.SetPlayingAsync(playing);
        var old = c.Supervisor.Status.Identity; var display = c.Frames.Display; var lease = c.Factory.Leases[0];
        var state = lease.Game.CaptureCheckpoint(); lease.RejectCapture = true;
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => c.Replacement.ReplaceAsync(Request(c)));
        Assert.IsType<CheckpointRejectedException>(error.InnerException); Assert.Equal("Capture", error.Phase);
        Assert.False(error.RollbackAttempted); Assert.Null(error.RecoveryError); Assert.Equal(old, c.Supervisor.Status.Identity);
        Assert.Equal(playing ? SessionState.Playing : SessionState.Paused, c.Supervisor.Status.State);
        Assert.Equal(state.Tick, c.Supervisor.Status.Tick); Assert.Same(display, c.Frames.Display); Assert.Equal(0, lease.Stops);
        Assert.Single(c.Factory.Leases); Assert.False(lease.ResourcesReleased); await c.Supervisor.GetStatusAsync();
    }
    [Theory]
    [InlineData("identity")] [InlineData("source")] [InlineData("scene")] [InlineData("artifact")] [InlineData("suspended")]
    public async Task StaleOrInvalidSelection_IsRejectedBeforePauseOrOldStop(string invalid)
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true); await c.Supervisor.SetPlayingAsync(true);
        if (invalid == "suspended") await c.Frames.ResizeAsync(0, 0);
        var request = Request(c); var identity = c.Supervisor.Status.Identity;
        if (invalid == "identity") request = request with { ExpectedIdentity = identity! with { Generation = identity.Generation + 1 } };
        if (invalid == "source") request = request with { ExpectedSourceRevision = 9 };
        if (invalid == "scene") request = request with { Scene = new("room", 8, []) };
        if (invalid == "artifact")
        {
            var copy = GameBuildArtifact.Publish(builds.B.DirectoryPath, Path.Combine(builds.Root, "replace-tampered"), builds.B.HostContractId,
                builds.B.EntryAssembly, builds.B.EntryType, 8, ["Game.Private.dll"]);
            File.AppendAllText(Path.Combine(copy.DirectoryPath, copy.EntryAssembly), "tampered"); request = request with { Candidate = copy };
        }
        var count = c.Factory.Leases[0].Commands.Count;
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => c.Replacement.ReplaceAsync(request));
        Assert.Equal("Preflight", error.Phase); Assert.False(error.RollbackAttempted); Assert.Null(error.RecoveryError);
        Assert.Equal(identity, c.Supervisor.Status.Identity); Assert.Equal(SessionState.Playing, c.Supervisor.Status.State);
        Assert.DoesNotContain(c.Factory.Leases[0].Commands.Skip(count), k => k is CommandKind.SetMode or CommandKind.CaptureCheckpoint or CommandKind.Stop);
        Assert.Equal(0, c.Factory.Leases[0].Stops); Assert.Single(c.Factory.Leases);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnknownCaptureOutcome_FaultsAndDrainsWithoutRollback(bool timeout)
    {
        await using var c = new Context(Fast()); await c.StartAsync(builds.A, true);
        c.Factory.Leases[0].BeforeSend = async (r, t) =>
        { if (r.Kind == (int)CommandKind.CaptureCheckpoint) { if (timeout) await Task.Delay(Timeout.InfiniteTimeSpan, t); else throw new IOException("unknown outcome"); } };
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => c.Replacement.ReplaceAsync(Request(c)));
        Assert.Equal("Capture", error.Phase); Assert.False(error.RollbackAttempted); Assert.Equal(SessionState.Faulted, c.Supervisor.Status.State);
        Assert.True(Assert.Single(c.Factory.Leases).ResourcesReleased); Assert.Equal(0, c.Frames.MappingCount); Assert.Null(c.Frames.Display);
    }
    [Fact]
    public async Task Reservation_RejectsCompetingCommandsAndSecondReplaceWithoutDisturbingOwner()
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true);
        var entered = Signal(); var release = Signal(); var request = Request(c);
        c.Factory.Leases[0].BeforeSend = async (r, t) => { if (r.Kind == (int)CommandKind.CaptureCheckpoint) { entered.TrySetResult(); await release.Task.WaitAsync(t); } };
        var replacing = c.Replacement.ReplaceAsync(request); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        // Gate-queued public commands are checked when they enter, after capture returns.
        var commands = new Func<Task>[] { () => c.Supervisor.StepAsync(), () => c.Supervisor.SetPlayingAsync(true),
            () => c.Supervisor.StartAsync(Scene()), () => c.Frames.ResizeAsync(16, 16), () => c.Supervisor.CaptureCheckpointAsync(),
            () => c.Replacement.ReplaceAsync(request) };
        var rejected = commands.Select(command => Assert.ThrowsAsync<InvalidOperationException>(command)).ToArray();
        release.TrySetResult(); await Task.WhenAll(rejected); await PumpAsync(c.Frames, replacing); await replacing;
        Assert.Equal(2, c.Factory.Leases.Count); Assert.Equal(builds.B.BuildId, c.Supervisor.Status.Identity!.GameBuildId); Assert.Equal(1, c.Factory.MaxLive);
    }
    [Theory]
    [InlineData("capture")] [InlineData("old-stop")] [InlineData("hello")] [InlineData("restore")] [InlineData("present")] [InlineData("rollback")]
    public async Task StopDuringTransaction_FencesCandidateAndRollbackAndDrainsAllOwnership(string phase)
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true);
        var entered = Signal(); var release = Signal();
        if (phase == "capture") c.Factory.Leases[0].BeforeSend = Hold(CommandKind.CaptureCheckpoint, entered);
        if (phase == "old-stop") c.Factory.Leases[0].BeforeStop = async () => { entered.TrySetResult(); await release.Task; };
        c.Factory.Configure = lease =>
        {
            var number = c.Factory.Leases.Count;
            if (number == 2 && phase == "rollback") lease.BeforeSend = (_, _) => throw new IOException("candidate");
            if (number == 2 && phase is "hello" or "restore") lease.BeforeSend = Hold(phase == "hello" ? CommandKind.Hello : CommandKind.RestoreCheckpoint, entered);
            if (number == 2 && phase == "present") lease.NoFrames = true;
            if (number == 3 && phase == "rollback") lease.BeforeSend = Hold(CommandKind.RestoreCheckpoint, entered);
        };
        var replacing = c.Replacement.ReplaceAsync(Request(c));
        if (phase == "present") await TestWait.UntilAsync(() => c.Frames.Display?.Surface.Identity.Generation == 2, TimeSpan.FromSeconds(3));
        else await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stopping = c.Frames.StopAsync(); release.TrySetResult(); await stopping.WaitAsync(TimeSpan.FromSeconds(3));
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.False(error.RollbackSucceeded); Assert.Equal(phase == "rollback" ? 3 : phase is "capture" or "old-stop" ? 1 : 2, c.Factory.Leases.Count);
        Assert.All(c.Factory.Leases, l => Assert.True(l.ResourcesReleased)); Assert.Equal(0, c.Frames.MappingCount);
        Assert.Equal(SessionState.Stopped, c.Supervisor.Status.State); Assert.Equal(1, c.Factory.MaxLive);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CallerCancellation_PreservesOldBeforeStopOrRollsBackAfterReap(bool afterStop)
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true); await c.Supervisor.SetPlayingAsync(true);
        using var cancellation = new CancellationTokenSource(); var entered = Signal(); var old = c.Supervisor.Status.Identity!;
        var state = c.Factory.Leases[0].Game.CaptureCheckpoint();
        if (afterStop) c.Factory.Configure = lease => { if (c.Factory.Leases.Count == 2) lease.BeforeSend = Hold(CommandKind.Hello, entered); };
        else c.Factory.Leases[0].AfterSend = (r, _) => { if (r.Kind == (int)CommandKind.CaptureCheckpoint) cancellation.Cancel(); };
        var replacing = c.Replacement.ReplaceAsync(Request(c), cancellation.Token);
        if (afterStop) { await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel(); }
        await PumpAsync(c.Frames, replacing); var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException); Assert.Equal(afterStop, error.RollbackAttempted); Assert.Equal(afterStop, error.RollbackSucceeded);
        Assert.Null(error.RecoveryError); Assert.Equal(SessionState.Playing, c.Supervisor.Status.State);
        Assert.Equal(state, c.Factory.Leases[^1].Game.CaptureCheckpoint()); Assert.Equal(old.GameBuildId, c.Supervisor.Status.Identity!.GameBuildId);
        Assert.Equal(afterStop ? old.Generation + 2 : old.Generation, c.Supervisor.Status.Identity.Generation); Assert.Equal(1, c.Factory.MaxLive);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnconfirmedCleanup_RetainsOneTaskAndBlocksLaunchUntilExplicitRetry(bool candidate)
    {
        await using var c = new Context(Fast()); await c.StartAsync(builds.A, true);
        var entered = Signal(); var release = Signal(); ReplacementRuntimeFixture.Lease held = c.Factory.Leases[0];
        if (candidate) c.Factory.Configure = lease =>
        { held = lease; lease.BeforeSend = (_, _) => throw new IOException("candidate"); lease.BeforeStop = async () => { entered.TrySetResult(); await release.Task; }; };
        else held.BeforeStop = async () => { entered.TrySetResult(); await release.Task; };
        try
        {
            var replacing = c.Replacement.ReplaceAsync(Request(c)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(error.RollbackAttempted); Assert.Equal(SessionState.CleanupFailed, c.Supervisor.Status.State);
            Assert.False(held.ResourcesReleased); Assert.Equal(1, held.Stops);
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.Supervisor.StartAsync(Scene()));
            release.TrySetResult(); await TestWait.UntilAsync(() => held.ResourcesReleased, TimeSpan.FromSeconds(3));
            Assert.Equal(SessionState.CleanupFailed, c.Supervisor.Status.State); Assert.Equal(candidate ? 2 : 1, c.Factory.Leases.Count);
            await c.Frames.StopAsync(); Assert.Equal(1, held.Stops); Assert.Equal(0, c.Frames.MappingCount);
            c.Factory.Configure = null; await c.StartAsync(builds.A, false); Assert.Equal(SessionState.Paused, c.Supervisor.Status.State);
        }
        finally { c.Factory.Configure = null; release.TrySetResult(); }
    }
    [Fact]
    public async Task ExistingResize_DrainsBeforeReservationAndQueuedReplaceIsFencedByStop()
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true);
        var entered = Signal(); var release = Signal(); var old = c.Supervisor.Status.Identity;
        c.Factory.Leases[0].BeforeSend = async (r, t) =>
        { if (r.Kind == (int)CommandKind.ConfigureSurface) { entered.TrySetResult(); await release.Task.WaitAsync(t); } };
        var resize = c.Frames.ResizeAsync(16, 16); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var replacing = c.Replacement.ReplaceAsync(Request(c)); Assert.False(replacing.IsCompleted);
        release.TrySetResult(); await PumpAsync(c.Frames, resize); await resize;
        await PumpAsync(c.Frames, replacing); await replacing;
        Assert.Equal(old!.Generation + 1, c.Supervisor.Status.Identity!.Generation); Assert.Equal(2, c.Factory.Leases.Count);
        Assert.Equal(16, c.Frames.Surface!.Width); Assert.Equal(1, c.Factory.MaxLive);
        entered = Signal(); release = Signal();
        c.Factory.Leases[^1].BeforeSend = async (r, t) =>
        { if (r.Kind == (int)CommandKind.ConfigureSurface) { entered.TrySetResult(); await release.Task.WaitAsync(t); } };
        resize = c.Frames.ResizeAsync(32, 16); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        replacing = c.Replacement.ReplaceAsync(Request(c)); var stopping = c.Frames.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resize);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replacing);
        await stopping; Assert.Equal(2, c.Factory.Leases.Count); Assert.Equal(0, c.Frames.MappingCount);
    }
    [Fact]
    public async Task RawSupervisorStop_InterruptsReplacementAndDrainsItsFrames()
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true); var entered = Signal();
        c.Factory.Leases[0].BeforeSend = Hold(CommandKind.CaptureCheckpoint, entered);
        var replacing = c.Replacement.ReplaceAsync(Request(c)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await c.Supervisor.StopAsync(); await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.Single(c.Factory.Leases); Assert.True(c.Factory.Leases[0].ResourcesReleased);
        Assert.Equal(0, c.Frames.MappingCount); Assert.Equal(SessionState.Stopped, c.Supervisor.Status.State);
    }
    [Fact]
    public async Task FailedReap_BlocksRollbackAndCanBeRetriedWithoutLosingOriginalError()
    {
        await using var c = new Context(); await c.StartAsync(builds.A, true); var lease = c.Factory.Leases[0]; lease.FailStop = true;
        try
        {
            var error = await Assert.ThrowsAsync<GameReplacementException>(() => c.Replacement.ReplaceAsync(Request(c)));
            Assert.Equal("StopOld", error.Phase); Assert.IsType<IOException>(error.InnerException); Assert.False(error.RollbackAttempted);
            Assert.Equal(SessionState.CleanupFailed, c.Supervisor.Status.State); Assert.Single(c.Factory.Leases); Assert.False(lease.ResourcesReleased);
            await Assert.ThrowsAsync<InvalidOperationException>(() => c.Supervisor.StartAsync(Scene()));
        }
        finally { lease.FailStop = false; }
        await c.Frames.StopAsync(); Assert.True(lease.ResourcesReleased); Assert.Equal(2, lease.Stops); Assert.Equal(0, c.Frames.MappingCount);
        await c.StartAsync(builds.A, false);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppClose_CancelsAcceptedReplaceBeforeDisposalAndSnapshotsBudgets(bool di)
    {
        var factory = new ReplacementRuntimeFixture { Produce = true }; AetherOptions configured = null!;
        await using var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c => c.UseAether(o =>
        {
            configured = o; o.HostContractId = GameBuildFixture.Hash; o.GameBuildId = GameBuildFixture.Hash; o.ReportDirectory = "replacement-app-tests";
            o.RuntimeFactory = _ => factory; o.ReplacementDeadlines = Fast();
        })).Build();
        configured.ReplacementDeadlines = new() { Total = TimeSpan.Zero }; await app.StartAsync();
        var host = app.Services.Resolve<AetherHost>()!; var starting = host.Frames.StartAsync(Scene(), builds.A, 32, 16); await PumpAsync(host.Frames, starting); await starting;
        var entered = Signal(); factory.Configure = lease => { if (factory.Leases.Count == 2) lease.BeforeSend = Hold(CommandKind.RestoreCheckpoint, entered); };
        var request = new GameReplacementRequest(builds.B, host.Supervisor.Status.Identity!, Scene(), 8);
        var replacing = host.Replacement.ReplaceAsync(request); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)); var error = await Assert.ThrowsAsync<GameReplacementException>(() => replacing);
        Assert.False(error.RollbackAttempted); Assert.All(factory.Leases, l => Assert.True(l.ResourcesReleased));
        Assert.Equal(2, factory.Leases.Count); Assert.Equal(0, host.Frames.MappingCount); Assert.Equal(SessionState.Stopped, host.Supervisor.Status.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Replacement.ReplaceAsync(request));
    }
    [Theory]
    [InlineData("zero")] [InlineData("negative")] [InlineData("over")] [InlineData("forward")] [InlineData("cleanup")]
    [InlineData("forward-over")] [InlineData("cleanup-over")] [InlineData("reserve")] [InlineData("overflow")]
    public async Task InvalidBudgets_AreRejectedBeforeAnyReservationOrProcess(string invalid)
    {
        await using var c = new Context(); var deadlines = new ReplacementDeadlines();
        deadlines = invalid switch
        {
            "zero" => deadlines with { Total = TimeSpan.Zero }, "negative" => deadlines with { Total = TimeSpan.FromTicks(-1) },
            "over" => deadlines with { Total = TimeSpan.FromSeconds(46) }, "forward" => deadlines with { Forward = TimeSpan.Zero },
            "cleanup" => deadlines with { Cleanup = TimeSpan.Zero }, "forward-over" => deadlines with { Forward = TimeSpan.FromMilliseconds(18001) },
            "cleanup-over" => deadlines with { Cleanup = TimeSpan.FromMilliseconds(10001) },
            "reserve" => deadlines with { Total = TimeSpan.FromSeconds(2), Forward = TimeSpan.FromSeconds(1), Cleanup = TimeSpan.FromSeconds(1) },
            _ => deadlines with { Forward = TimeSpan.MaxValue, Cleanup = TimeSpan.MaxValue }
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameReplacement(c.Supervisor, c.Frames, deadlines)); Assert.Empty(c.Factory.Leases);
        Assert.Equal(TimeSpan.FromSeconds(45), new ReplacementDeadlines().Total);
        _ = new GameReplacement(c.Supervisor, c.Frames, new() { Total = TimeSpan.FromMilliseconds(3), Forward = TimeSpan.FromMilliseconds(1), Cleanup = TimeSpan.FromMilliseconds(1) });
    }
    private static Func<ControlRequest, CancellationToken, Task> Hold(CommandKind kind, TaskCompletionSource entered)
        => async (r, t) => { if (r.Kind == (int)kind) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, t); } };
    private static async Task PumpAsync(FrameSession frames, Task pending)
    {
        var deadline = Stopwatch.StartNew();
        while (!pending.IsCompleted)
        {
            var display = frames.Display;
            using var frame = display?.Receiver.TakeLatest();
            if (frame != null) frames.Presented(display!, frame.Metadata);
            if (deadline.Elapsed > TimeSpan.FromSeconds(6)) throw new TimeoutException("Test presentation did not complete.");
            await Task.Delay(2);
        }
    }
    private sealed class Context : IAsyncDisposable
    {
        internal ReplacementRuntimeFixture Factory { get; } = new();
        internal SessionSupervisor Supervisor { get; }
        internal FrameSession Frames { get; }
        internal GameReplacement Replacement { get; }
        internal Context(ReplacementDeadlines? deadlines = null)
        {
            Supervisor = new(Factory, GameBuildFixture.Hash, GameBuildFixture.Hash, "replacement-tests", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
            Frames = new(Supervisor); Replacement = new(Supervisor, Frames, deadlines);
        }
        internal async Task StartAsync(GameBuildArtifact? build, bool viewport)
        {
            Factory.Produce = viewport;
            var starting = viewport ? build == null ? Frames.StartAsync(Scene(), 32, 16) : Frames.StartAsync(Scene(), build, 32, 16)
                : build == null ? Supervisor.StartAsync(Scene()) : Supervisor.StartAsync(Scene(), build);
            await PumpAsync(Frames, starting); await starting;
            for (var i = 0; i < 5; i++) await Supervisor.StepAsync();
        }
        public async ValueTask DisposeAsync() { await Frames.DisposeAsync(); await Supervisor.DisposeAsync(); }
    }
}
