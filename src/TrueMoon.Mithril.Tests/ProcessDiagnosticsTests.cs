using System.ComponentModel;
using System.Diagnostics;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril.Tests;

public class ProcessDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Diagnostics_FailedExplicitStarts_PreserveErrorAndUseFreshRunIdentity(bool throwingSink)
    {
        var events = new DeferredEvents { ThrowAfterWrite = throwingSink };
        await using var handle = new MissingHandle(events);
        var firstError = await Assert.ThrowsAsync<Win32Exception>(() => handle.StartAsync());
        Assert.True(await handle.StopAsync());
        var secondError = await Assert.ThrowsAsync<Win32Exception>(() => handle.StartAsync());
        Assert.Null(handle.OwnedProcess);
        Assert.Null(handle.LastRestartError); // Initial failure is returned to the caller, not the restart monitor.
        Assert.Equal(0, handle.RestartCount);

        // Evaluate after a later Start reset the handle state: the payload must still describe its own attempt.
        var traces = events.Read();
        Assert.Equal(new[] { UnitProcessStage.LaunchStarting, UnitProcessStage.LaunchFailed, UnitProcessStage.StopRequested,
            UnitProcessStage.Stopped, UnitProcessStage.LaunchStarting, UnitProcessStage.LaunchFailed }, traces.Select(x => x.Stage));
        Assert.NotEqual(Guid.Empty, traces[0].RunId);
        Assert.NotEqual(traces[0].RunId, traces[4].RunId);
        Assert.All(traces.Take(4), x => Assert.Equal(traces[0].RunId, x.RunId));
        Assert.All(traces.Skip(4), x => Assert.Equal(traces[4].RunId, x.RunId));
        Assert.Same(firstError, traces[1].Error);
        Assert.Same(secondError, traces[5].Error);
        Assert.All(traces, x =>
        {
            Assert.Equal(1, x.Generation);
            Assert.Null(x.ProcessId);
            Assert.Equal(12, x.UnitIndex);
            Assert.Equal("diagnostic-unit", x.UnitName);
            Assert.Equal(Environment.ProcessId, x.ParentProcessId);
            Assert.Equal(UnitRestartPolicy.Always, x.RestartPolicy);
            Assert.Equal(3, x.RestartLimit);
            Assert.Equal(250, x.RestartDelay);
            Assert.Equal(1000, x.TerminationDelay);
        });
        Assert.Equal("LaunchFailure", traces[1].Reason);
        Assert.Equal("NoProcess", traces[3].Reason);
    }

    [Fact]
    public async Task Diagnostics_CancelledPreparation_IsReportedWithoutStartedOrTerminal()
    {
        var events = new DeferredEvents();
        using var cancellation = new CancellationTokenSource();
        await using var handle = new MissingHandle(events) { Preparing = cancellation.Cancel };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.StartAsync(cancellation.Token));
        Assert.Null(handle.OwnedProcess);
        var traces = events.Read();
        Assert.Equal(new[] { UnitProcessStage.LaunchStarting, UnitProcessStage.LaunchFailed }, traces.Select(x => x.Stage));
        Assert.Equal("Cancelled", traces[1].Reason);
        Assert.Same(error, traces[1].Error);
        Assert.Null(traces[1].ProcessId);
    }

    [Fact]
    public async Task Diagnostics_DisposedSource_DoesNotReplaceLaunchFailureOrPreventStop()
    {
        using var events = new EventsSource("MithrilDisposedSource");
        events.Dispose();
        await using var handle = new MissingHandle(events);
        await Assert.ThrowsAsync<Win32Exception>(() => handle.StartAsync());
        Assert.True(await handle.StopAsync());
        Assert.Null(handle.OwnedProcess);
    }

    private sealed class MissingHandle(IEventsSource events)
        : ChildProcessUnitHandle(new UnitConfiguration(12) { Name = "diagnostic-unit" }, events)
    {
        internal Action? Preparing { get; init; }
        internal Process? OwnedProcess => Process;
        protected override Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
        {
            Preparing?.Invoke();
            return Task.FromResult(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "missing-mithril-diagnostics.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true });
        }
    }

    private sealed class DeferredEvents : IEventsSource
    {
        private readonly List<Func<object>> _payloads = [];
        internal bool ThrowAfterWrite { get; init; }
        public string Name => "DeferredMithrilEvents";
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? func, string? category = null, string? caller = null)
        {
            if (category == "UnitProcess" && func is not null) _payloads.Add(func);
            if (ThrowAfterWrite) throw new InvalidOperationException("Sink failure must not replace lifecycle outcomes.");
        }
        internal UnitProcessDiagnostic[] Read() => _payloads.Select(x => Assert.IsType<UnitProcessDiagnostic>(x())).ToArray();
    }
}
