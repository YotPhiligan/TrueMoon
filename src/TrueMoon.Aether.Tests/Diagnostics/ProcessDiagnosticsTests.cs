using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

public sealed class ProcessDiagnosticsTests
{
    [Fact]
    public async Task RuntimeHistory_ConcurrentReadsAreBoundedAndPreserveSessionGenerationWithoutSecrets()
    {
        var factory = new NativeRuntimeFactory(); var leases = new List<IRuntimeLease>();
        using var reading = new CancellationTokenSource(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = Task.Factory.StartNew(() =>
        {
            entered.TrySetResult();
            while (!reading.IsCancellationRequested)
            {
                var snapshots = factory.ReadDiagnostics(8, 0); Assert.InRange(snapshots.Count, 0, 8);
                Assert.All(snapshots, item => { Assert.Null(item.StandardOutput); Assert.Null(item.StandardError); Assert.False(item.Completed); });
                Thread.Yield();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for (ulong generation = 1; generation <= 65; generation++)
                leases.Add(factory.Create(new(new(Guid.NewGuid(), generation, new('A', 64), new('B', 64), new('C', 64)), "private-endpoint", "unused")));
            reading.Cancel(); await reader.WaitAsync(TimeSpan.FromSeconds(5));
            var all = factory.ReadDiagnostics(64, 0); Assert.Equal(64, all.Count); Assert.Equal(65UL, all[0].Generation); Assert.Equal(2UL, all[^1].Generation);
            Assert.Equal(64, all.Select(item => item.Id).Distinct().Count());
            Assert.DoesNotContain(new string('C', 64), System.Text.Json.JsonSerializer.Serialize(all)); Assert.DoesNotContain("private-endpoint", System.Text.Json.JsonSerializer.Serialize(all));
            var recent = factory.ReadDiagnostics(1, 128); Assert.Equal(65UL, Assert.Single(recent).Generation);
        }
        finally { reading.Cancel(); await reader; foreach (var lease in leases) await lease.DisposeAsync(); }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(65, 1)]
    [InlineData(1, -1)]
    [InlineData(1, 32769)]
    public void InvalidSnapshotBudgets_FailBeforeOpeningProcesses(int count, int characters)
    { var factory = new NativeRuntimeFactory(); Assert.Throws<ArgumentOutOfRangeException>(() => factory.ReadDiagnostics(count, characters)); Assert.Empty(factory.Leases); }
}
