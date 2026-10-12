using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Configuration;

namespace TrueMoon.Mithril.Tests;

public class UnitParentProcessEventsHandlerTests
{
    [Theory]
    [InlineData("qe")]
    [InlineData("eof")]
    [InlineData("exit")]
    [InlineData("reentrant")]
    public async Task ParentExitOrControlClosure_CancelsOnceAndDrainsOwnedTasks(string signal)
    {
        await using var lease = await StartParentAsync();
        var parent = lease.Process;
        using var lifetime = new TestLifetime();
        var endpoint = "tm-parent-test-" + Guid.NewGuid().ToString("N");
        await using var listener = Listener(parent.Id, endpoint, lifetime);
        if (signal == "reentrant") lifetime.OnCancel = listener.Dispose;
        await listener.StartAsync();
        if (signal == "exit")
        { parent.Kill(); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        else
        {
            using var client = new NamedPipeClientStream(".", endpoint, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (signal == "eof") client.Dispose();
            else await client.WriteAsync(System.Text.Encoding.UTF8.GetBytes("ignored\nqe\n"));
        }
        await lifetime.Notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await listener.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(listener.Completion.IsCompletedSuccessfully);
        Assert.Equal(1, lifetime.Calls);
        if (signal != "exit") Assert.False(parent.HasExited);
        await StopParentAsync(parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_InterruptsPendingAcceptOrReadWithoutCancellingAppOrKillingParent(bool connected)
    {
        await using var lease = await StartParentAsync();
        var parent = lease.Process;
        using var lifetime = new TestLifetime();
        var endpoint = "tm-parent-test-" + Guid.NewGuid().ToString("N");
        var listener = Listener(parent.Id, endpoint, lifetime);
        using var client = new NamedPipeClientStream(".", endpoint, PipeDirection.Out, PipeOptions.Asynchronous);
        await listener.StartAsync();
        if (connected) await client.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await listener.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        listener.Dispose();
        Assert.True(listener.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, lifetime.Calls);
        Assert.False(parent.HasExited);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => listener.StartAsync());
        await StopParentAsync(parent);
    }

    [Fact]
    public async Task MissingParent_CancelsOnceAndPreCancelledStartDoesNotObserve()
    {
        using var lifetime = new TestLifetime();
        await using var missing = Listener(int.MaxValue, null, lifetime);
        await missing.StartAsync();
        Assert.Equal(1, lifetime.Calls);
        Assert.True(missing.Completion.IsCompletedSuccessfully);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var untouched = new TestLifetime();
        await using var other = Listener(int.MaxValue, null, untouched);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => other.StartAsync(cancelled.Token));
        Assert.Equal(0, untouched.Calls);
    }

    private static UnitParentProcessEventsHandler Listener(int parent, string? endpoint, TestLifetime lifetime)
    {
        var configuration = new ConfigurationBuilder().Build();
        configuration.Set("-p", parent.ToString());
        if (endpoint is not null) configuration.Set(UnitParentProcessEventsHandler.ControlPipeKey, endpoint);
        return new UnitParentProcessEventsHandler(configuration, lifetime, new TestEvents<UnitParentProcessEventsHandler>());
    }
    private static async Task<OwnedParent> StartParentAsync()
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "composition-fixture", "MithrilCompositionProbe.dll")); info.ArgumentList.Add("--idle");
        var process = Process.Start(info)!;
        try { Assert.Equal("READY", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))); return new OwnedParent(process); }
        catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } process.Dispose(); throw; }
    }
    private static async Task StopParentAsync(Process parent)
    {
        if (!parent.HasExited) { await parent.StandardInput.WriteLineAsync("qe"); await parent.StandardInput.FlushAsync(); await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }
    private sealed class OwnedParent(Process process) : IAsyncDisposable
    {
        internal Process Process => process;
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            finally { process.Dispose(); }
        }
    }
}
