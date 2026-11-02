using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Projects;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceWatchTests
{
    [Fact]
    public async Task Open_WithoutBaselineDoesNotCreateWorkerHintsOrSdkWork()
    {
        await using var first = new SourceWatchFixture();
        await using var second = new SourceWatchFixture(first.Files.Project);
        first.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(EditorSourceState.NoBuild, first.Watch.Read().State);
        Assert.NotEqual(first.Watch.OwnerKey, second.Watch.OwnerKey);
        Assert.Equal(first.Watch.OwnerKey, first.Watch.Read().OwnerKey);
        Assert.Equal((0, 0, 0), (first.Scanner.Calls, first.Hints.Created, first.Clock.TimerCount));
        Assert.Null(first.Watch.Read().BuildId); Assert.False(first.Watch.Read().IsScanning);
    }

    [Fact]
    public async Task Bind_ValidBuildChecksThenPublishesUnchangedWithBoundIdentity()
    {
        await using var h = new SourceWatchFixture(); var descriptor = h.Files.Descriptor();
        h.Watch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, descriptor);
        var call = await h.Scanner.Next();
        var checking = h.Watch.Read();
        Assert.Equal(EditorSourceState.Checking, checking.State); Assert.True(checking.IsScanning);
        Assert.Equal(SourceWatchFixture.BuildA, checking.BuildId); Assert.Equal(SourceWatchFixture.Inputs, checking.InputFingerprint);
        Assert.Equal(descriptor.SourceFingerprint, checking.SourceFingerprint); Assert.Null(checking.ObservedAt);
        Assert.Same(descriptor, call.Descriptor); Assert.Equal(1, checking.BaselineVersion);
        call.Complete(SourceWatchFixture.Same);
        var observed = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged && !snapshot.IsScanning);
        Assert.Equal(h.Clock.GetUtcNow(), observed.ObservedAt); Assert.True(observed.ObservationVersion > checking.ObservationVersion);
        Assert.Equal(checking.SignalVersion, observed.SignalVersion);
        Assert.Equal(EditorSourceObservationMode.NotificationsAndPolling, observed.Mode); Assert.Equal(1, observed.WatcherCount);
        Assert.Empty(observed.ChangedPaths); Assert.Null(observed.Reason); Assert.Equal(1, h.Scanner.MaximumActive);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Bind_UnusableDescriptorRemainsUnavailableWithoutPolling(bool disabled)
    {
        await using var h = new SourceWatchFixture(options: new() { Enabled = !disabled });
        h.Watch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, disabled ? h.Files.Descriptor() : null, "legacy witness unavailable");
        var state = await h.Until(snapshot => snapshot.State == EditorSourceState.Unavailable);
        Assert.False(string.IsNullOrWhiteSpace(state.Reason)); Assert.False(state.IsScanning);
        h.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal((0, 0, 0), (h.Scanner.Calls, h.Hints.Created, h.Clock.TimerCount));
        Assert.Equal(EditorSourceState.Unavailable, h.Watch.Read().State);
    }

    [Fact]
    public async Task HintDuringPassVetoesUnchangedAndRunsOneCoalescedPass()
    {
        await using var h = new SourceWatchFixture(); h.Bind();
        var first = await h.Scanner.Next(); h.Hints.Latest.Signal(); h.Hints.Latest.Signal();
        var signalled = h.Watch.Read(); Assert.Equal(EditorSourceState.Checking, signalled.State);
        first.Complete(SourceWatchFixture.Same);
        await h.Clock.WaitForDue(TimeSpan.FromMilliseconds(250));
        Assert.Equal(EditorSourceState.Checking, h.Watch.Read().State); Assert.Null(h.Watch.Read().ObservedAt);
        h.Clock.Advance(TimeSpan.FromMilliseconds(250));
        var next = await h.Scanner.Next(); Assert.Equal(2, h.Scanner.Calls); Assert.Equal(1, h.Scanner.MaximumActive);
        next.Complete(SourceWatchFixture.Same);
        var same = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Equal(signalled.SignalVersion, same.SignalVersion);
    }

    [Fact]
    public async Task RebindDuringIgnoredCancellationPreventsOldPublicationAndSerializesLatestPass()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var old = await h.Scanner.Next();
        h.Watch.Bind(new('B', 64), SourceWatchFixture.Inputs, h.Files.Descriptor());
        var latest = h.Files.Descriptor(); h.Watch.Bind(new('C', 64), SourceWatchFixture.Inputs, latest);
        await SourceWatchFixture.Eventually(() => old.Token.IsCancellationRequested);
        Assert.Equal(1, h.Scanner.Calls);
        old.Complete(SourceWatchFixture.Same);
        var current = await h.Scanner.Next(); Assert.Same(latest, current.Descriptor);
        Assert.Equal(1, h.Scanner.MaximumActive); Assert.Equal(2, h.Scanner.Calls);
        Assert.Equal(new string('C', 64), h.Watch.Read().BuildId); Assert.Equal(EditorSourceState.Checking, h.Watch.Read().State);
        current.Complete(SourceWatchFixture.Same);
        var result = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Equal(new string('C', 64), result.BuildId); Assert.Equal(3, result.BaselineVersion);
    }

    [Fact]
    public async Task Hints_DebounceAt250MillisecondsAndStormCannotStarveOneSecondDeadline()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); (await h.Scanner.Next()).Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); await h.Clock.WaitForDue(TimeSpan.FromSeconds(10));
        h.Hints.Latest.Signal(); await h.Clock.WaitForDue(TimeSpan.FromMilliseconds(250));
        h.Clock.Advance(TimeSpan.FromMilliseconds(249)); Assert.Equal(1, h.Scanner.Calls); Assert.Equal(EditorSourceState.Checking, h.Watch.Read().State);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1)); var quiet = await h.Scanner.Next(); quiet.Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); await h.Clock.WaitForDue(TimeSpan.FromMilliseconds(10250));

        for (var index = 0; index < 9; index++)
        {
            h.Hints.Latest.Signal();
            var due = Math.Min(h.Clock.GetTimestamp() + TimeSpan.FromMilliseconds(250).Ticks, TimeSpan.FromMilliseconds(1250).Ticks);
            await h.Clock.WaitForDue(TimeSpan.FromTicks(due)); h.Clock.Advance(TimeSpan.FromMilliseconds(100));
        }
        await h.Clock.WaitForDue(TimeSpan.FromMilliseconds(1250));
        h.Clock.Advance(TimeSpan.FromMilliseconds(99)); Assert.Equal(2, h.Scanner.Calls);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1)); var storm = await h.Scanner.Next();
        Assert.Equal(3, h.Scanner.Calls); Assert.Equal(1, h.Scanner.MaximumActive); storm.Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
    }

    [Fact]
    public async Task PendingDeadlineDuringPassStartsImmediatelyAfterActualDrain()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var first = await h.Scanner.Next();
        h.Hints.Latest.Signal(); h.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, h.Scanner.Calls); Assert.True(h.Watch.Read().IsScanning);
        first.Complete(SourceWatchFixture.Same);
        var next = await h.Scanner.Next(); // No additional fake time/debounce window.
        Assert.Equal(1, h.Scanner.MaximumActive); Assert.Equal(EditorSourceState.Checking, h.Watch.Read().State);
        next.Complete(SourceWatchFixture.Same); await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
    }

    [Fact]
    public async Task Maintenance_PollsTenSecondsAfterCompletionWithoutCheckingFlicker()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); (await h.Scanner.Next()).Complete(SourceWatchFixture.Same);
        var old = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); await h.Clock.WaitForDue(TimeSpan.FromSeconds(10));
        h.Clock.Advance(TimeSpan.FromMilliseconds(9999)); Assert.Equal(1, h.Scanner.Calls);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1)); var next = await h.Scanner.Next();
        var scanning = h.Watch.Read(); Assert.True(scanning.IsScanning); Assert.Equal(EditorSourceState.ObservedUnchanged, scanning.State);
        Assert.Equal(old.ObservedAt, scanning.ObservedAt); Assert.Equal(old.ObservationVersion, scanning.ObservationVersion);
        h.Clock.Advance(TimeSpan.FromSeconds(1)); next.Complete(SourceWatchFixture.Same);
        var same = await h.Until(snapshot => snapshot.ObservationVersion > old.ObservationVersion);
        Assert.Equal(h.Clock.GetUtcNow(), same.ObservedAt); Assert.False(same.IsScanning);
        await h.Clock.WaitForDue(TimeSpan.FromSeconds(21)); h.Clock.Advance(TimeSpan.FromMilliseconds(9999)); Assert.Equal(2, h.Scanner.Calls);
    }

    [Fact]
    public async Task Changed_RevertHintsAndCoverageWarningNeverClearLatchedDifference()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var first = await h.Scanner.Next();
        var changedPaths = Enumerable.Range(0, 20).Select(index => ("changed-" + index).PadRight(index == 0 ? 512 : 513, 'p')).ToArray();
        var warning = "coverage warning".PadRight(1025, 'r');
        first.Complete(new(true, false, true, warning, changedPaths));
        var changed = await h.Until(snapshot => snapshot.State == EditorSourceState.Changed);
        Assert.Equal(changedPaths.Take(16).Select(path => path[..512]), changed.ChangedPaths);
        Assert.Equal(warning[..1024], changed.Reason);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)changed.ChangedPaths)[0] = "tampered");
        Assert.Equal(h.Clock.GetUtcNow(), changed.ObservedAt);
        h.Hints.Latest.Signal(); h.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(EditorSourceState.Changed, h.Watch.Read().State); Assert.Equal(1, h.Scanner.Calls);
        Assert.Equal(changed.ChangedPaths, h.Watch.Read().ChangedPaths);
        h.Bind(); var next = await h.Scanner.Next(); Assert.Equal(EditorSourceState.Checking, h.Watch.Read().State);
        next.Complete(SourceWatchFixture.Same);
        var result = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Empty(result.ChangedPaths); Assert.Null(result.Reason); Assert.True(result.BaselineVersion > changed.BaselineVersion);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Unavailable_TransientFailurePollsAndRecoversOnlyAfterCompletePass(bool throws)
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var call = await h.Scanner.Next();
        if (throws) call.Completion.TrySetException(new IOException("locked")); else call.Complete(EditorSourceScan.Unavailable("locked"));
        var error = await h.Until(snapshot => snapshot.State == EditorSourceState.Unavailable);
        Assert.False(error.IsScanning); Assert.Null(error.ObservedAt); Assert.False(string.IsNullOrWhiteSpace(error.Reason));
        await h.Clock.WaitForDue(TimeSpan.FromSeconds(10)); h.Clock.Advance(TimeSpan.FromSeconds(10));
        var retry = await h.Scanner.Next(); Assert.Equal(EditorSourceState.Unavailable, h.Watch.Read().State);
        retry.Complete(SourceWatchFixture.Same);
        var same = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Null(same.Reason); Assert.Equal(h.Clock.GetUtcNow(), same.ObservedAt); Assert.Equal(2, h.Scanner.Calls);
    }

    [Fact]
    public async Task PermanentUnavailableIgnoresHintsAndPollingUntilNewBaseline()
    {
        await using var h = new SourceWatchFixture(); h.Bind();
        (await h.Scanner.Next()).Complete(EditorSourceScan.Unavailable("unsupported local path", false));
        await h.Until(snapshot => snapshot.State == EditorSourceState.Unavailable);
        h.Hints.Latest.Signal(true); h.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(EditorSourceState.Unavailable, h.Watch.Read().State); Assert.Equal(1, h.Scanner.Calls);
        h.Bind(); var next = await h.Scanner.Next(); next.Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); Assert.Equal(2, h.Scanner.Calls);
    }

    [Theory] [InlineData("disabled")] [InlineData("failed")] [InlineData("limited")]
    public async Task Hints_UnavailableNotificationsStillAllowCompletePollingCoverage(string mode)
    {
        await using var h = new SourceWatchFixture(options: new() { UseFileSystemNotifications = mode != "disabled" });
        if (mode == "failed") h.Hints.Failure = () => new IOException("attach");
        if (mode == "limited") { h.Hints.Count = 0; h.Hints.Warning = "handle limit"; }
        h.Bind(); (await h.Scanner.Next()).Complete(SourceWatchFixture.Same);
        var same = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Equal(EditorSourceObservationMode.Polling, same.Mode); Assert.Equal(0, same.WatcherCount);
        await h.Clock.WaitForDue(TimeSpan.FromSeconds(10)); h.Clock.Advance(TimeSpan.FromSeconds(10));
        var poll = await h.Scanner.Next(); poll.Complete(EditorSourceScan.Difference(h.Files.Source));
        var changed = await h.Until(snapshot => snapshot.State == EditorSourceState.Changed);
        Assert.Equal(new[] { h.Files.Source }, changed.ChangedPaths); Assert.Equal(2, h.Scanner.Calls);
    }

    [Fact]
    public async Task OverflowForcesCheckingAndOldBaselineCallbacksCannotInvalidateNewObservation()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); (await h.Scanner.Next()).Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); var oldHints = h.Hints.Latest;
        oldHints.Signal(true); var checking = h.Watch.Read();
        Assert.Equal(EditorSourceState.Checking, checking.State); Assert.Equal(EditorSourceObservationMode.Polling, checking.Mode);
        await h.Clock.WaitForDue(TimeSpan.FromMilliseconds(250)); h.Clock.Advance(TimeSpan.FromMilliseconds(250));
        (await h.Scanner.Next()).Complete(SourceWatchFixture.Same); await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Equal(EditorSourceObservationMode.Polling, h.Watch.Read().Mode);
        h.Bind(); (await h.Scanner.Next()).Complete(SourceWatchFixture.Same);
        var current = await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        oldHints.Signal(true); Assert.Equal(current, h.Watch.Read()); Assert.Equal(1, oldHints.Disposals);
    }

    [Fact]
    public async Task CooperativeBudgetCannotPublishSameOrStartAnotherPassBeforeActualIoDrain()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var call = await h.Scanner.Next();
        await h.Clock.WaitForDue(TimeSpan.FromSeconds(5)); h.Clock.Advance(TimeSpan.FromSeconds(5));
        await SourceWatchFixture.Eventually(() => call.Token.IsCancellationRequested);
        Assert.True(h.Watch.Read().IsScanning); Assert.Equal(1, h.Scanner.Calls);
        h.Clock.Advance(TimeSpan.FromSeconds(10)); Assert.Equal(1, h.Scanner.Calls);
        call.Complete(SourceWatchFixture.Same);
        var unavailable = await h.Until(snapshot => snapshot.State == EditorSourceState.Unavailable && !snapshot.IsScanning);
        Assert.Contains("budget", unavailable.Reason); Assert.Null(unavailable.ObservedAt);
        await h.Clock.WaitForDue(TimeSpan.FromSeconds(25)); h.Clock.Advance(TimeSpan.FromSeconds(10));
        var retry = await h.Scanner.Next(); Assert.Equal(1, h.Scanner.MaximumActive); retry.Complete(SourceWatchFixture.Same);
        await h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
    }

    [Fact]
    public async Task Dispose_FencesImmediatelyWaitsForActualIoAndRetriesFailedHintCleanup()
    {
        await using var h = new SourceWatchFixture(); h.Bind(); var scan = await h.Scanner.Next();
        var hints = h.Hints.Latest; hints.FailNextDispose = true;
        var first = h.Watch.DisposeAsync().AsTask(); var shared = h.Watch.DisposeAsync().AsTask();
        await SourceWatchFixture.Eventually(() => scan.Token.IsCancellationRequested && hints.Stops == 1);
        Assert.Equal(EditorSourceState.Stopped, h.Watch.Read().State); Assert.Same(first, shared); Assert.False(first.IsCompleted);
        Assert.Equal(0, hints.Disposals); Assert.Equal(1, hints.Stops);
        var closed = h.Watch.Read(); hints.Signal(true); h.Bind(); Assert.Equal(closed, h.Watch.Read());
        scan.Complete(SourceWatchFixture.Same);
        var failure = await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.NotNull(failure); Assert.Contains("hint cleanup failure", failure.ToString());
        Assert.Equal(EditorSourceState.Stopped, h.Watch.Read().State); Assert.Equal(1, hints.Disposals);
        Assert.Equal(closed.ObservationVersion, h.Watch.Read().ObservationVersion);
        await h.Watch.DisposeAsync(); Assert.Equal(2, hints.Disposals); Assert.Equal(0, h.Watch.Read().WatcherCount);
        await h.Watch.DisposeAsync(); Assert.Equal(2, hints.Disposals); Assert.Equal(1, h.Scanner.Calls);
    }

    [Fact]
    public async Task PublicConstructorCopiesOptionsBeforeDeferredNotificationActivation()
    {
        using var files = new SourceWatchFiles(); var options = new EditorSourceWatchOptions(); var clock = new SourceWatchClock();
        await using var watch = new EditorSourceWatch(files.Project, options, clock);
        options.Enabled = false; options.UseFileSystemNotifications = false; options.MaximumWatchers = 0;
        options.PollInterval = TimeSpan.FromMinutes(1);
        watch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, files.Descriptor());
        await SourceWatchFixture.Eventually(() => watch.Read().State == EditorSourceState.ObservedUnchanged);
        Assert.Equal(EditorSourceObservationMode.NotificationsAndPolling, watch.Read().Mode); Assert.Equal(1, watch.Read().WatcherCount);
        await clock.WaitForDue(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("debounce-zero")] [InlineData("debounce-over")] [InlineData("max-below")] [InlineData("max-over")]
    [InlineData("debounce-negative")] [InlineData("poll-negative")] [InlineData("budget-negative")]
    [InlineData("poll-zero")] [InlineData("poll-over")] [InlineData("budget-zero")] [InlineData("budget-over")]
    [InlineData("watchers-negative")] [InlineData("watchers-over")]
    public void Options_InvalidBoundsFailBeforeCreatingResources(string invalid)
    {
        using var files = new SourceWatchFiles(); var options = new EditorSourceWatchOptions();
        switch (invalid)
        {
            case "debounce-zero": options.Debounce = TimeSpan.Zero; break;
            case "debounce-negative": options.Debounce = TimeSpan.FromTicks(-1); break;
            case "debounce-over": options.Debounce = TimeSpan.FromMilliseconds(1001); break;
            case "max-below": options.MaximumDebounce = TimeSpan.FromMilliseconds(249); break;
            case "max-over": options.MaximumDebounce = TimeSpan.FromMilliseconds(1001); break;
            case "poll-zero": options.PollInterval = TimeSpan.Zero; break;
            case "poll-negative": options.PollInterval = TimeSpan.FromTicks(-1); break;
            case "poll-over": options.PollInterval = TimeSpan.FromMinutes(1) + TimeSpan.FromMilliseconds(1); break;
            case "budget-zero": options.ScanBudget = TimeSpan.Zero; break;
            case "budget-negative": options.ScanBudget = TimeSpan.FromTicks(-1); break;
            case "budget-over": options.ScanBudget = TimeSpan.FromSeconds(5) + TimeSpan.FromMilliseconds(1); break;
            case "watchers-negative": options.MaximumWatchers = -1; break;
            case "watchers-over": options.MaximumWatchers = 33; break;
        }
        var clock = new SourceWatchClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditorSourceWatch(files.Project, options, clock)); Assert.Equal(0, clock.TimerCount);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Options_ExactMinimumAndMaximumPoliciesRemainInertUntilBound(bool maximum)
    {
        var options = maximum ? new EditorSourceWatchOptions
        {
            Debounce = TimeSpan.FromSeconds(1), MaximumDebounce = TimeSpan.FromSeconds(1), PollInterval = TimeSpan.FromMinutes(1),
            ScanBudget = TimeSpan.FromSeconds(5), MaximumWatchers = 32
        } : new EditorSourceWatchOptions
        {
            Debounce = TimeSpan.FromTicks(1), MaximumDebounce = TimeSpan.FromTicks(1), PollInterval = TimeSpan.FromMilliseconds(1),
            ScanBudget = TimeSpan.FromTicks(1), MaximumWatchers = 0
        };
        await using var h = new SourceWatchFixture(options: options);
        Assert.Equal(EditorSourceState.NoBuild, h.Watch.Read().State);
        Assert.Equal((0, 0, 0), (h.Scanner.Calls, h.Hints.Created, h.Clock.TimerCount));
    }
}

internal sealed class SourceWatchFiles : IDisposable
{
    private readonly ProjectFixture _fixture = new();
    internal string Root => _fixture.Root;
    internal ResolvedProject Project => _fixture.Resolved;
    internal string Tree => Path.GetDirectoryName(Project.GameProjectPath)!;
    internal string Source => Path.Combine(Tree, "Original.cs");
    internal string Artifacts => Path.Combine(Root, "artifacts");
    internal SourceWatchFiles() => File.WriteAllText(Source, "original");
    internal GameBuildSourceWatchDescriptor Descriptor(IEnumerable<string>? files = null, IEnumerable<string>? trees = null,
        IEnumerable<GameBuildSourceWatchProbe>? probes = null, IEnumerable<string>? members = null)
    {
        var paths = (files ?? [Project.GameProjectPath, Source]).ToArray();
        return new(Project.GameProjectPath, new(Artifacts), paths.Select(path => new GameBuildInputFile(path,
            new FileInfo(path).Length, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))),
            trees ?? [Tree], probes ?? [], members ?? [Project.GameProjectPath, Source]);
    }
    public void Dispose() => _fixture.Dispose();
}

internal sealed class SourceWatchFixture : IAsyncDisposable
{
    internal static readonly string BuildA = new('A', 64), Inputs = new('D', 64);
    internal static EditorSourceScan Same => EditorSourceScan.Same;
    internal SourceWatchFiles Files { get; } = new();
    internal SourceWatchClock Clock { get; } = new();
    internal SourceWatchScans Scanner { get; } = new();
    internal SourceWatchHints Hints { get; } = new();
    internal EditorSourceWatch Watch { get; }
    internal SourceWatchFixture(ResolvedProject? project = null, EditorSourceWatchOptions? options = null) =>
        Watch = new(project ?? Files.Project, options ?? new(), Clock, Scanner.Scan, Hints.Create);
    internal void Bind() => Watch.Bind(BuildA, Inputs, Files.Descriptor());
    internal async Task<EditorSourceSnapshot> Until(Func<EditorSourceSnapshot, bool> predicate)
    {
        await Eventually(() => predicate(Watch.Read())); return Watch.Read();
    }
    internal static async Task Eventually(Func<bool> predicate)
    {
        var stop = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate())
        {
            if (stop.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Source watch test handshake did not complete.");
            await Task.Yield();
        }
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            var drain = Watch.DisposeAsync().AsTask(); Scanner.ReleaseAll(); await drain.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { Files.Dispose(); }
    }
}

internal sealed class SourceWatchScans
{
    private readonly Channel<SourceWatchScanCall> _calls = Channel.CreateUnbounded<SourceWatchScanCall>();
    private readonly ConcurrentBag<SourceWatchScanCall> _all = [];
    private int _count, _active, _maximumActive;
    internal int Calls => Volatile.Read(ref _count);
    internal int MaximumActive => Volatile.Read(ref _maximumActive);
    internal async Task<EditorSourceScan> Scan(GameBuildSourceWatchDescriptor descriptor, CancellationToken token)
    {
        var active = Interlocked.Increment(ref _active); InterlockedExtensions.Max(ref _maximumActive, active);
        var call = new SourceWatchScanCall(descriptor, token); _all.Add(call); Interlocked.Increment(ref _count);
        _calls.Writer.TryWrite(call);
        try { return await call.Completion.Task.ConfigureAwait(false); }
        finally { Interlocked.Decrement(ref _active); }
    }
    internal async Task<SourceWatchScanCall> Next() => await _calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    internal void ReleaseAll() { foreach (var call in _all) call.Complete(SourceWatchFixture.Same); }
    private static class InterlockedExtensions
    {
        internal static void Max(ref int location, int candidate)
        {
            var seen = Volatile.Read(ref location);
            while (candidate > seen)
            { var actual = Interlocked.CompareExchange(ref location, candidate, seen); if (actual == seen) return; seen = actual; }
        }
    }
}

internal sealed class SourceWatchScanCall(GameBuildSourceWatchDescriptor descriptor, CancellationToken token)
{
    internal GameBuildSourceWatchDescriptor Descriptor { get; } = descriptor;
    internal CancellationToken Token { get; } = token;
    internal TaskCompletionSource<EditorSourceScan> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal void Complete(EditorSourceScan result) => Completion.TrySetResult(result);
}

internal sealed class SourceWatchHints
{
    internal int Created;
    internal SourceWatchHintLease Latest = null!;
    internal int Count = 1;
    internal string? Warning;
    internal Func<Exception?>? Failure;
    internal IEditorSourceHints Create(GameBuildSourceWatchDescriptor descriptor, Action<bool> signal)
    {
        if (Failure?.Invoke() is { } error) throw error;
        Interlocked.Increment(ref Created); return Latest = new(signal, Count, Warning);
    }
}

internal sealed class SourceWatchHintLease(Action<bool> signal, int count, string? warning) : IEditorSourceHints
{
    internal int Disposals;
    internal int Stops;
    internal bool FailNextDispose;
    public int Count { get; } = count;
    public string? Warning { get; } = warning;
    internal void Signal(bool overflow = false) => signal(overflow);
    public void Stop() => Interlocked.Increment(ref Stops);
    public void Dispose()
    {
        Interlocked.Increment(ref Disposals);
        if (FailNextDispose) { FailNextDispose = false; throw new IOException("controlled hint cleanup failure"); }
    }
}

internal sealed class SourceWatchClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly HashSet<ManualTimer> _timers = [];
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    internal int TimerCount { get { lock (_gate) return _timers.Count(timer => timer.Due != long.MaxValue); } }
    internal long? NextDue { get { lock (_gate) return _timers.Where(timer => timer.Due != long.MaxValue).Select(timer => (long?)timer.Due).Min(); } }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state); timer.Change(dueTime, period); return timer;
    }
    internal async Task WaitForDue(TimeSpan timestamp) => await SourceWatchFixture.Eventually(() =>
    { lock (_gate) return _timers.Any(timer => timer.Due == timestamp.Ticks); });
    internal void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
        long target; lock (_gate) target = _ticks + amount.Ticks;
        while (true)
        {
            ManualTimer? next;
            lock (_gate)
            {
                next = _timers.Where(timer => timer.Due <= target).OrderBy(timer => timer.Due).FirstOrDefault();
                if (next == null) { _ticks = target; return; }
                _ticks = next.Due;
                next.Due = next.Period > 0 ? next.Due + next.Period : long.MaxValue;
            }
            next.Invoke();
        }
    }
    private sealed class ManualTimer(SourceWatchClock owner, TimerCallback callback, object? state) : ITimer
    {
        internal long Due = long.MaxValue, Period;
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (_disposed) return false;
                Period = period > TimeSpan.Zero ? period.Ticks : 0;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner._ticks + dueTime.Ticks;
                owner._timers.Add(this); return true;
            }
        }
        internal void Invoke() { if (!_disposed) callback(state); }
        public void Dispose() { lock (owner._gate) { _disposed = true; owner._timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
