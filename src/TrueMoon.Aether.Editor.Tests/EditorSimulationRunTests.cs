using System.Collections.Concurrent;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorSimulationRunTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_AdmitsOneGestureAndSequencesStartBeforePlayOrResumesOnly(bool start)
    {
        var entered = Signal(); var release = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command); Assert.True(token.CanBeCanceled);
            if (command == (start ? EditorCommand.Start : EditorCommand.Play)) { entered.SetResult(); await release.Task; }
        }, CancellationToken.None);
        commands.RunSimulation(start); await entered.Task.WaitAsync(Timeout);
        var admitted = commands.Pending;
        commands.RunSimulation(start); commands.Execute(EditorCommand.Build);
        Assert.Same(admitted, commands.Pending); Assert.False(admitted.IsCompleted);
        Assert.Equal(new[] { start ? EditorCommand.Start : EditorCommand.Play }, calls.ToArray());
        release.SetResult(); await admitted.WaitAsync(Timeout);
        Assert.Equal(start ? new[] { EditorCommand.Start, EditorCommand.Play } : new[] { EditorCommand.Play }, calls.ToArray());
        Assert.Null(commands.Error);
    }

    [Theory]
    [InlineData(EditorCommand.Stop)]
    [InlineData(EditorCommand.Cancel)]
    public async Task InterruptDuringUncooperativeStart_CancelsRunDrainsStartAndNeverPlays(EditorCommand interrupt)
    {
        var entered = Signal(); var release = Signal(); var interrupted = Signal();
        var calls = new ConcurrentQueue<EditorCommand>(); CancellationToken runToken = default;
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command);
            if (command == EditorCommand.Start) { runToken = token; entered.SetResult(); await release.Task; }
            else { Assert.False(token.CanBeCanceled); interrupted.SetResult(); }
        }, CancellationToken.None);
        commands.RunSimulation(true); await entered.Task.WaitAsync(Timeout); var run = commands.Pending;
        commands.Execute(interrupt); await interrupted.Task.WaitAsync(Timeout);
        var draining = commands.Pending;
        Assert.True(runToken.IsCancellationRequested); Assert.False(run.IsCompleted); Assert.False(draining.IsCompleted);
        commands.RunSimulation(true); Assert.Same(draining, commands.Pending);
        release.SetResult(); await draining.WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Start, interrupt }, calls.ToArray()); Assert.Null(commands.Error);
    }

    [Fact]
    public async Task CloseDuringStart_FencesNewRunAndWaitsForAcceptedStartAfterStop()
    {
        var entered = Signal(); var release = Signal(); var stopped = Signal();
        var calls = new ConcurrentQueue<EditorCommand>(); CancellationToken runToken = default;
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command);
            if (command == EditorCommand.Start) { runToken = token; entered.SetResult(); await release.Task; }
            else { Assert.Equal(EditorCommand.Stop, command); Assert.False(token.CanBeCanceled); stopped.SetResult(); }
        }, CancellationToken.None);
        commands.RunSimulation(true); await entered.Task.WaitAsync(Timeout);
        var closing = commands.StopAsync(); await stopped.Task.WaitAsync(Timeout);
        commands.RunSimulation(false); commands.Execute(EditorCommand.Build);
        Assert.True(runToken.IsCancellationRequested); Assert.False(closing.IsCompleted);
        release.SetResult(); await closing.WaitAsync(Timeout);
        commands.RunSimulation(true); await commands.Pending.WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Start, EditorCommand.Stop }, calls.ToArray()); Assert.Null(commands.Error);
    }

    [Fact]
    public async Task LifetimeCancellationBetweenStartAndPlay_UsesLinkedTokenAndSuppressesLatePlay()
    {
        using var lifetime = new CancellationTokenSource(); var entered = Signal(); var release = Signal();
        var calls = new ConcurrentQueue<EditorCommand>(); CancellationToken runToken = default;
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command); runToken = token; entered.SetResult(); await release.Task;
        }, lifetime.Token);
        commands.RunSimulation(true); await entered.Task.WaitAsync(Timeout);
        Assert.NotEqual(lifetime.Token, runToken); Assert.True(runToken.CanBeCanceled);
        lifetime.Cancel(); Assert.True(runToken.IsCancellationRequested);
        release.SetResult(); await commands.Pending.WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Start }, calls.ToArray()); Assert.Null(commands.Error);
        Assert.True(commands.Pending.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopDuringPlay_CancelsTheSameGestureAndDrainsItsOutstandingPlay()
    {
        var entered = Signal(); var release = Signal(); var stopped = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        CancellationToken startToken = default, playToken = default;
        var commands = new EditorCommands(async (command, token) =>
        {
            calls.Enqueue(command);
            if (command == EditorCommand.Start) startToken = token;
            if (command == EditorCommand.Play) { playToken = token; entered.SetResult(); await release.Task; }
            if (command == EditorCommand.Stop) { Assert.False(token.CanBeCanceled); stopped.SetResult(); }
        }, CancellationToken.None);
        commands.RunSimulation(true); await entered.Task.WaitAsync(Timeout);
        commands.Execute(EditorCommand.Stop); await stopped.Task.WaitAsync(Timeout);
        Assert.Equal(startToken, playToken); Assert.True(playToken.IsCancellationRequested); Assert.False(commands.Pending.IsCompleted);
        release.SetResult(); await commands.Pending.WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Start, EditorCommand.Play, EditorCommand.Stop }, calls.ToArray());
    }

    [Theory]
    [InlineData(EditorCommand.Start)]
    [InlineData(EditorCommand.Play)]
    public async Task RunFailure_IsObservedAndANewAcceptedRunClearsIt(EditorCommand failing)
    {
        var failure = new IOException("simulation failed"); var calls = new ConcurrentQueue<EditorCommand>(); var fail = true;
        var commands = new EditorCommands((command, _) =>
        {
            calls.Enqueue(command);
            return fail && command == failing ? Task.FromException(failure) : Task.CompletedTask;
        }, CancellationToken.None);
        commands.RunSimulation(true); await commands.Pending.WaitAsync(Timeout);
        Assert.Same(failure, commands.Error);
        Assert.Equal(failing == EditorCommand.Start ? new[] { EditorCommand.Start } : new[] { EditorCommand.Start, EditorCommand.Play }, calls.ToArray());
        fail = false; commands.RunSimulation(false); await commands.Pending.WaitAsync(Timeout);
        Assert.Null(commands.Error); Assert.Equal(EditorCommand.Play, calls.Last());
        Assert.Equal(failing == EditorCommand.Start ? 2 : 3, calls.Count);
    }

    [Fact]
    public async Task RunWhileNormalWorkIsPending_IsRejectedWithoutReplacingTheTrackedTask()
    {
        var entered = Signal(); var release = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands(async (command, _) => { calls.Enqueue(command); entered.SetResult(); await release.Task; }, CancellationToken.None);
        commands.Execute(EditorCommand.Build); await entered.Task.WaitAsync(Timeout); var pending = commands.Pending;
        commands.RunSimulation(true); commands.RunSimulation(false);
        Assert.Same(pending, commands.Pending); release.SetResult(); await pending.WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Build }, calls.ToArray());
    }

    [Fact]
    public async Task FailedClose_CancelsOldGestureButReopensAdmissionForCleanupAndANewRun()
    {
        var entered = Signal(); var release = Signal(); var calls = new ConcurrentQueue<EditorCommand>(); var stops = 0;
        var expected = new IOException("stop failed");
        var commands = new EditorCommands(async (command, _) =>
        {
            calls.Enqueue(command);
            if (command == EditorCommand.Start) { entered.SetResult(); await release.Task; }
            if (command == EditorCommand.Stop && Interlocked.Increment(ref stops) == 1) throw expected;
        }, CancellationToken.None);
        commands.RunSimulation(true); await entered.Task.WaitAsync(Timeout);
        Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => commands.StopAsync()));
        release.SetResult(); await commands.Pending.WaitAsync(Timeout);
        commands.RunSimulation(false); await commands.Pending.WaitAsync(Timeout);
        await commands.StopAsync().WaitAsync(Timeout);
        Assert.Equal(new[] { EditorCommand.Start, EditorCommand.Stop, EditorCommand.Play, EditorCommand.Stop }, calls.ToArray());
        Assert.Null(commands.Error);
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
