using TrueMoon.Aether.Runtime.Mods;
using Xunit;

namespace TrueMoon.Aether.Tests.Mods;

public sealed class ModGenerationTests
{
    [Fact]
    public async Task GuardStagesQuiescesResumesAndClearsAllRetiredDelegates()
    {
        var generation = new ModGeneration(); var calls = 0; var callback = generation.Guard(() => calls++);
        callback(); Assert.Equal(0, calls); generation.Open(); callback(); Assert.Equal(1, calls);
        await generation.QuiesceAsync(default); callback(); Assert.Equal(1, calls);
        generation.Open(); callback(); Assert.Equal(2, calls); await generation.QuiesceAsync(default); generation.Retire();
        callback(); Assert.Equal(2, calls); Assert.False(generation.IsActive); Assert.Throws<InvalidOperationException>(() => generation.Guard(() => calls++));
    }
    [Fact]
    public void GuardBudgetAndTerminalCloseAreExplicit()
    {
        var generation = new ModGeneration(); var calls = 0;
        var callbacks = Enumerable.Range(0, 256).Select(_ => generation.Guard(() => calls++)).ToArray();
        Assert.Throws<InvalidOperationException>(() => generation.Guard(() => calls++));
        generation.Open(); foreach (var callback in callbacks) callback(); Assert.Equal(256, calls);
        generation.Close(); generation.Open(); foreach (var callback in callbacks) callback(); Assert.Equal(256, calls); Assert.False(generation.IsActive);
    }
    [Fact]
    public async Task QuiesceDrainsEveryInFlightCallbackAndCancellationDoesNotRetireIt()
    {
        var generation = new ModGeneration(); using var first = new ManualResetEventSlim(); using var second = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim(); using var releaseSecond = new ManualResetEventSlim();
        var one = generation.Guard(() => { first.Set(); if (!releaseFirst.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); });
        var two = generation.Guard(() => { second.Set(); if (!releaseSecond.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); });
        generation.Open(); var runningOne = Task.Run(one); var runningTwo = Task.Run(two);
        try
        {
            Assert.True(first.Wait(TimeSpan.FromSeconds(10))); Assert.True(second.Wait(TimeSpan.FromSeconds(10)));
            using var cancel = new CancellationTokenSource(); var cancelled = generation.QuiesceAsync(cancel.Token); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            var drained = generation.QuiesceAsync(default); Assert.False(drained.IsCompleted); releaseFirst.Set(); await runningOne; Assert.False(drained.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => generation.Retire()); releaseSecond.Set(); await runningTwo; await drained; generation.Open(); Assert.True(generation.IsActive);
        }
        finally { releaseFirst.Set(); releaseSecond.Set(); await Task.WhenAll(runningOne, runningTwo); }
    }
    private sealed class CallbackException : Exception;
    [Fact]
    public async Task FailedCallbackExportsOnlyNeutralExceptionAndStillDrains()
    {
        var generation = new ModGeneration(); var callback = generation.Guard(() => throw new CallbackException()); generation.Open();
        var error = Assert.Throws<ModOperationException>(callback); Assert.Null(error.InnerException); Assert.Equal(ModLoadError.ActivationFailed, error.Diagnostic.Error);
        await generation.QuiesceAsync(default); generation.Retire(); callback();
    }
}
