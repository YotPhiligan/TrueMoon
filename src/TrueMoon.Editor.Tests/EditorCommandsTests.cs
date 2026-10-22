using System.Collections.Concurrent;
using Xunit;

namespace TrueMoon.Editor.Tests;

public sealed class EditorCommandsTests
{
    [Theory] [InlineData(EditorCommand.Stop)] [InlineData(EditorCommand.Cancel)]
    public async Task PendingWork_RejectsAnotherNormalCommandButAllowsInterrupt(EditorCommand interrupt)
    {
        var entered = Signal(); var release = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands(async (command, _) => { calls.Enqueue(command); if (command == EditorCommand.Build) { entered.SetResult(); await release.Task; } else release.SetResult(); }, CancellationToken.None);
        commands.Execute(EditorCommand.Build); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        commands.Execute(EditorCommand.Play); Assert.False(commands.Pending.IsCompleted);
        commands.Execute(interrupt); await commands.Pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { EditorCommand.Build, interrupt }, calls.ToArray()); Assert.Null(commands.Error);
    }
    [Fact]
    public async Task Close_FencesNewWorkAndWaitsForAcceptedCommandAfterStop()
    {
        var entered = Signal(); var release = Signal(); var stopped = Signal(); var calls = new ConcurrentQueue<EditorCommand>();
        var commands = new EditorCommands(async (command, token) => { calls.Enqueue(command); if (command == EditorCommand.Build) { entered.SetResult(); await release.Task; } else { Assert.False(token.CanBeCanceled); stopped.SetResult(); } }, CancellationToken.None);
        commands.Execute(EditorCommand.Build); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); var closing = commands.StopAsync(); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        commands.Execute(EditorCommand.Play); Assert.False(closing.IsCompleted); release.SetResult(); await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { EditorCommand.Build, EditorCommand.Stop }, calls.ToArray());
    }
    [Fact]
    public async Task Failure_IsObservedAndNextAcceptedCommandClearsIt()
    {
        var failure = new IOException("build failed"); var calls = 0;
        var commands = new EditorCommands((_, _) => ++calls == 1 ? Task.FromException(failure) : Task.CompletedTask, CancellationToken.None);
        commands.Execute(EditorCommand.Build); await commands.Pending; Assert.Same(failure, commands.Error);
        commands.Execute(EditorCommand.Play); await commands.Pending; Assert.Null(commands.Error); Assert.Equal(2, calls);
    }
    [Fact]
    public async Task Cancellation_IsObservedWithoutAUserError()
    {
        var commands = new EditorCommands((_, _) => Task.FromCanceled(new CancellationToken(true)), CancellationToken.None);
        commands.Execute(EditorCommand.Build); await commands.Pending; Assert.Null(commands.Error); Assert.True(commands.Pending.IsCompletedSuccessfully);
    }
    [Fact]
    public async Task FailedClose_ReopensAdmissionForCleanupRetry()
    {
        var error = new IOException("cleanup failed"); var stops = 0;
        var commands = new EditorCommands((command, _) => command == EditorCommand.Stop && ++stops == 1 ? Task.FromException(error) : Task.CompletedTask, CancellationToken.None);
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => commands.StopAsync()));
        commands.Execute(EditorCommand.Stop); await commands.Pending; Assert.Equal(2, stops); Assert.Null(commands.Error);
    }
    [Fact]
    public async Task NormalCommand_ReceivesLifetimeAndInvalidCommandsNeverReachTheApi()
    {
        using var lifetime = new CancellationTokenSource(); var tokens = new List<CancellationToken>();
        var commands = new EditorCommands((_, token) => { tokens.Add(token); return Task.CompletedTask; }, lifetime.Token);
        Assert.Throws<ArgumentOutOfRangeException>(() => commands.Execute((EditorCommand)99)); Assert.Empty(tokens);
        commands.Execute(EditorCommand.Build); await commands.Pending; Assert.Equal(lifetime.Token, Assert.Single(tokens));
        Assert.Throws<ArgumentNullException>(() => new EditorCommands((Func<EditorCommand, CancellationToken, Task>)null!, CancellationToken.None));
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
