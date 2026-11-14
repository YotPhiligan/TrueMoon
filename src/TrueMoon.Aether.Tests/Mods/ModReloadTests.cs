using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Aether.Tooling.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;
using EcsSystemContext = TrueMoon.Aether.Abstractions.Ecs.EcsSystemContext;

namespace TrueMoon.Aether.Tests.Mods;

public sealed class ModReloadTests
{
    private sealed class Harness : IAsyncDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "TrueMoon-ARCH37-" + Guid.NewGuid().ToString("N"));
        internal readonly ConcurrentQueue<string> Events = new();
        internal readonly ConcurrentQueue<Action> Callbacks = new();
        internal readonly ConcurrentQueue<object> Contexts = new();
        internal readonly HashSet<string> Failures = new(StringComparer.Ordinal);
        internal Func<string, bool>? OnCheck;
        internal IApp App { get; }
        internal World World { get; }
        internal EntityHandle Entity { get; }
        internal ModHost Host { get; }
        internal ModSystemSession Session { get; }
        internal Harness(bool di = false)
        {
            App = TrueMoon.App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
            {
                c.Services(s => s.Singleton<Action<string>>(_ => Events.Enqueue)
                    .Singleton<Func<string, bool>>(_ => key => OnCheck?.Invoke(key) ?? Failures.Contains(key))
                    .Singleton<Action<Action>>(_ => callback => { callback(); Callbacks.Enqueue(callback); })
                    .Singleton<Action<object>>(_ => Contexts.Enqueue));
                c.UseAetherEcs(); c.UseAetherMods();
            }).Build();
            App.StartAsync().GetAwaiter().GetResult();
            World = App.Services.Resolve<IFactory<World>>()!.Create()!;
            Entity = World.CreateEntity(); World.AddComponent(Entity, new LinearVelocity(new Vector3(7, 0, 0)));
            Host = App.Services.Resolve<ModHost>()!; Session = Host.CreateSystemSession(World);
        }
        internal ModLoadRequest Request(bool second = false, string? entry = null, string group = "stable", string id = "root")
        {
            var manifest = new ModManifest { Id = id, Version = second ? "2.0.0" : "1.0.0", ReloadGroup = group, EntryAssembly = "ModFixture.dll",
                EntryType = "TrueMoon.ManualTests.ModFixture." + (entry ?? (second ? "SecondReloadEntry" : "ReloadEntry")) };
            var directory = ModArtifactPublisher.Publish(Root, manifest,
                [Path.Combine(AppContext.BaseDirectory, "ModFixture.dll"), Path.Combine(AppContext.BaseDirectory, "ModPrivateDependency.dll")]);
            return new(id, [directory]);
        }
        internal async Task Initialize()
        { var result = await Session.ReplaceAsync(Request()); Assert.True(result.Committed, result.Diagnostic.ToString()); Assert.Equal(1, Session.Generation); }
        internal bool Tick() => Session.Tick(TimeSpan.FromMilliseconds(20));
        internal float Value => World.GetComponent<LinearVelocity>(Entity).Value.X;
        internal int Count(string text) => Events.Count(e => e == text);
        internal void Dispatch() { foreach (var callback in Callbacks) callback(); }
        public async ValueTask DisposeAsync()
        {
            OnCheck = null; Failures.Clear();
            await App.DisposeAsync(); World.Dispose();
            var path = Path.GetFullPath(Root);
            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StableWorldAndStateSurviveCommit(bool di)
    {
        await using var h = new Harness(di); await h.Initialize(); var worldId = h.World.Id; var entity = h.Entity;
        Assert.DoesNotContain("v1:callback", h.Events); h.Dispatch(); Assert.Equal(1, h.Count("v1:callback"));
        Assert.True(h.Tick()); Assert.True(h.Tick()); Assert.Equal(9, h.Value);
        var old = h.Session.CurrentUnloadTicket!; var staleContext = (EcsSystemContext)h.Contexts.First();
        var result = await h.Session.ReplaceAsync(h.Request(true));
        Assert.True(result.Committed, result.Diagnostic.ToString()); Assert.Equal(ModLoadError.None, result.Cleanup.Error); Assert.Same(old, result.UnloadTicket);
        Assert.Equal(worldId, h.Session.WorldId); Assert.Equal(worldId, h.World.Id); Assert.True(h.World.IsAlive(entity)); Assert.Equal(1, h.World.EntityCount);
        Assert.Equal(9, h.Value); Assert.True(h.Tick()); Assert.Equal(19, h.Value);
        Assert.Contains("v2:tick:3:state:3", h.Events); Assert.Equal(2, h.Session.Generation);
        h.Dispatch(); Assert.Equal(1, h.Count("v1:callback")); Assert.Equal(1, h.Count("v2:callback"));
        Assert.Throws<InvalidOperationException>(() => { staleContext.Commands.CreateEntity(); }); Assert.Equal(1, h.World.EntityCount);
        Assert.True(old.UnloadRequested); Assert.False(h.World.IsFaulted); Assert.Equal(1, h.Host.OwnedGroupCount);
        Assert.Equal(1, h.Count("v1:start")); Assert.Equal(1, h.Count("v1:system-dispose"));
    }
    [Theory]
    [InlineData("v2:register", ModLoadError.ActivationFailed)]
    [InlineData("v2:start", ModLoadError.ActivationFailed)]
    [InlineData("v2:service-start", ModLoadError.ActivationFailed)]
    [InlineData("v2:factory", ModLoadError.ActivationFailed)]
    [InlineData("v1:quiesce", ModLoadError.ActivationFailed)]
    [InlineData("v1:service-quiesce", ModLoadError.ActivationFailed)]
    [InlineData("v1:capture", ModLoadError.StateRejected)]
    [InlineData("v1:oversize", ModLoadError.StateRejected)]
    [InlineData("v1:state-version", ModLoadError.StateRejected)]
    [InlineData("v2:restore", ModLoadError.StateRejected)]
    [InlineData("v2:different-id", ModLoadError.StateRejected)]
    public async Task RejectedCandidateResumesLastGood(string stage, ModLoadError error)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); h.Failures.Add(stage);
        var ticket = h.Session.CurrentUnloadTicket; var result = await h.Session.ReplaceAsync(h.Request(true));
        Assert.False(result.Committed); Assert.Equal(error, result.Diagnostic.Error); Assert.Equal(ModLoadError.None, result.Cleanup.Error);
        Assert.Same(ticket, h.Session.CurrentUnloadTicket); Assert.Equal(1, h.Session.Generation); Assert.Equal(8, h.Value); Assert.False(h.World.IsFaulted);
        h.Failures.Clear(); Assert.True(h.Tick()); Assert.Equal(9, h.Value); Assert.Contains("v1:tick:2:state:2", h.Events);
        Assert.Equal(1, h.Count("v1:start")); Assert.Equal(1, h.Count("v1:factory")); Assert.Equal(0, h.Count("v1:system-dispose"));
        h.Dispatch(); Assert.Equal(1, h.Count("v1:callback")); Assert.Equal(0, h.Count("v2:callback"));
    }
    [Theory]
    [InlineData("v2:factory")] [InlineData("v1:capture")] [InlineData("v2:restore")]
    public async Task CandidateCallbacksCannotWriteTheBorrowedWorldDuringPreparation(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick();
        h.OnCheck = key => { if (key == stage) h.World.SetComponent(h.Entity, new LinearVelocity(new Vector3(999, 0, 0))); return false; };
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed);
        Assert.Equal(8, h.Value); Assert.False(h.World.IsFaulted); h.OnCheck = null; h.Tick(); Assert.Equal(9, h.Value);
    }
    [Theory]
    [InlineData("start-wait")] [InlineData("quiesce-wait")]
    public async Task CancellationBeforeCommitPreservesStateAndReopensOldAdmission(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnCheck = key => { if (key == (stage == "start-wait" ? "v2:" : "v1:") + stage) { entered.TrySetResult(); return true; } return false; };
        using var cancel = new CancellationTokenSource(); var pending = h.Session.ReplaceAsync(h.Request(true), cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (stage == "start-wait") { Assert.True(h.Tick()); Assert.Equal(9, h.Value); } // Private preparation does not stall current ticks.
        h.Dispatch(); Assert.Equal(stage == "start-wait" ? 1 : 0, h.Count("v1:callback")); Assert.Equal(0, h.Count("v2:callback"));
        cancel.Cancel(); var result = await pending;
        Assert.False(result.Committed); Assert.Equal(ModLoadError.Cancelled, result.Diagnostic.Error); Assert.False(h.Session.ResumeRequired);
        h.OnCheck = null; Assert.True(h.Tick()); Assert.Equal(stage == "start-wait" ? 10 : 9, h.Value);
        h.Dispatch(); Assert.Equal(stage == "start-wait" ? 2 : 1, h.Count("v1:callback"));
    }
    [Fact]
    public async Task ResumeFailureIsExplicitAndRetryableWithoutRestart()
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); h.Failures.UnionWith(["v2:restore", "v1:resume"]);
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(ModLoadError.RollbackFailed, result.Diagnostic.Error);
        Assert.True(h.Session.ResumeRequired); Assert.Throws<ModOperationException>(() => h.Tick()); h.Dispatch(); Assert.Equal(0, h.Count("v1:callback"));
        h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.ResumeAsync()).Error); Assert.False(h.Session.ResumeRequired);
        h.Tick(); Assert.Equal(9, h.Value); Assert.Contains("v1:tick:2:state:2", h.Events); Assert.Equal(1, h.Count("v1:factory"));
    }
    [Theory]
    [InlineData("v1:stop")] [InlineData("v1:service-stop")] [InlineData("v1:system-dispose")]
    [InlineData("v1:service-dispose")] [InlineData("v1:dispose")]
    public async Task AppliedCommitReportsRetirementFailureWithoutRollback(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); h.Failures.Add(stage);
        var result = await h.Session.ReplaceAsync(h.Request(true));
        Assert.True(result.Committed); Assert.Equal(ModLoadError.None, result.Diagnostic.Error); Assert.Equal(ModLoadError.CleanupFailed, result.Cleanup.Error);
        Assert.False(result.UnloadTicket!.UnloadRequested); Assert.Equal(2, h.Host.OwnedGroupCount); Assert.Equal(2, h.Session.Generation);
        h.Tick(); Assert.Equal(18, h.Value); Assert.Contains("v2:tick:2:state:2", h.Events); Assert.False(h.World.IsFaulted);
        h.Dispatch(); Assert.Equal(0, h.Count("v1:callback")); Assert.Equal(1, h.Count("v2:callback"));
        h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.RetryCleanupAsync()).Error);
        Assert.True(result.UnloadTicket.UnloadRequested); Assert.Equal(1, h.Host.OwnedGroupCount);
    }
    [Fact]
    public async Task RejectedCandidateOwnerRetainedForRetry()
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); h.Failures.UnionWith(["v2:restore", "v2:system-dispose"]);
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(ModLoadError.StateRejected, result.Diagnostic.Error);
        Assert.Equal(ModLoadError.CleanupFailed, result.Cleanup.Error); Assert.False(result.UnloadTicket!.UnloadRequested);
        Assert.Equal(2, h.Host.OwnedGroupCount); Assert.True(h.Tick()); Assert.Equal(9, h.Value); Assert.False(h.World.IsFaulted);
        h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.RetryCleanupAsync()).Error); Assert.True(result.UnloadTicket.UnloadRequested);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationAtCommitBoundaryHasUnambiguousOutcome(bool afterCommit)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); using var cancel = new CancellationTokenSource();
        h.OnCheck = key => { if (key == (afterCommit ? "v1:stop" : "v2:restore")) cancel.Cancel(); return false; };
        var result = await h.Session.ReplaceAsync(h.Request(true), cancel.Token);
        Assert.Equal(afterCommit, result.Committed); Assert.Equal(afterCommit ? 2 : 1, h.Session.Generation);
        Assert.Equal(afterCommit ? ModLoadError.None : ModLoadError.Cancelled, result.Diagnostic.Error);
        Assert.Equal(ModLoadError.None, result.Cleanup.Error); h.OnCheck = null; h.Tick(); Assert.Equal(afterCommit ? 18 : 9, h.Value);
    }
    [Fact]
    public async Task NewTicksContinueWhileRetiredCleanupIsPending()
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        h.OnCheck = key => { if (key == "v1:stop") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); } return false; };
        var pending = Task.Run(() => h.Session.ReplaceAsync(h.Request(true)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); Assert.Equal(2, h.Session.Generation); Assert.False(pending.IsCompleted);
            Assert.True(h.Tick()); Assert.Equal(18, h.Value); Assert.Contains("v2:tick:2:state:2", h.Events);
            h.Dispatch(); Assert.Equal(0, h.Count("v1:callback")); Assert.Equal(1, h.Count("v2:callback"));
            release.Set(); Assert.True((await pending).Committed);
        }
        finally { release.Set(); await pending; }
    }
    [Fact]
    public async Task EmptyGraphRemainsEmptyAndAddingUncapturedSystemsIsRejected()
    {
        await using var h = new Harness(); h.Failures.Add("v1:no-systems"); await h.Initialize(); h.Tick(); Assert.Equal(7, h.Value);
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(ModLoadError.StateRejected, result.Diagnostic.Error);
        h.Tick(); Assert.Equal(7, h.Value); Assert.Equal(0, h.Count("v1:execute")); Assert.Equal(1, h.Session.Generation);
    }
    [Fact]
    public async Task GroupCaptureBudgetRejectsWithoutTruncatingOrResettingSystems()
    {
        await using var h = new Harness(); h.Failures.UnionWith(["v1:many-systems", "v2:many-systems"]); await h.Initialize(); h.Tick();
        Assert.Equal(72, h.Value); h.Failures.Add("v1:large-state");
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(ModLoadError.StateRejected, result.Diagnostic.Error);
        Assert.Equal(72, h.Value); h.Failures.Remove("v1:large-state"); h.Tick(); Assert.Equal(137, h.Value);
        Assert.Equal(65, h.Count("v1:factory")); Assert.Equal(65, h.Count("v1:tick:2:state:2"));
    }
    [Fact]
    public async Task AnotherSessionCannotStealWorldOwnership()
    {
        await using var h = new Harness(); await h.Initialize(); var other = h.Host.CreateSystemSession(h.World);
        var result = await other.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(0, other.Generation);
        Assert.True(h.Tick()); Assert.Equal(8, h.Value); Assert.False(h.World.IsFaulted);
        Assert.Equal(ModLoadError.None, (await other.StopAsync()).Error); Assert.True(h.Tick()); Assert.Equal(9, h.Value);
    }
    [Fact]
    public async Task InvalidTickArgumentsDoNotTerminateTheLastGoodGeneration()
    {
        await using var h = new Harness(); await h.Initialize();
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Session.Tick(TimeSpan.Zero));
        Assert.Throws<OperationCanceledException>(() => h.Session.Tick(TimeSpan.FromMilliseconds(20), new CancellationToken(true)));
        Assert.True(h.Tick()); Assert.Equal(8, h.Value); Assert.Equal(1, h.Session.Generation);
    }
    [Fact]
    public async Task ReloadWaitsForActiveTick()
    {
        await using var h = new Harness(); await h.Initialize();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        h.OnCheck = key => { if (key == "v1:execute") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); } return false; };
        var tick = Task.Run(h.Tick);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); var pending = h.Session.ReplaceAsync(h.Request(true));
            Assert.False(pending.IsCompleted); Assert.Equal(1, h.Session.Generation);
            release.Set(); Assert.True(await tick); var result = await pending; Assert.True(result.Committed);
            h.OnCheck = null; h.Tick(); Assert.Equal(18, h.Value); Assert.Contains("v2:tick:2:state:2", h.Events);
        }
        finally { release.Set(); await tick; }
    }
    [Fact]
    public async Task ReloadDrainsActiveGuardedCallbackBeforeCapture()
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        h.OnCheck = key => { if (key == "v1:callback") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); } return false; };
        var callback = Task.Run(h.Callbacks.First());
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10))); var pending = h.Session.ReplaceAsync(h.Request(true)); Assert.False(pending.IsCompleted);
            Assert.Equal(0, h.Count("v1:capture")); release.Set(); await callback; Assert.True((await pending).Committed);
            h.OnCheck = null; h.Dispatch(); Assert.Equal(1, h.Count("v1:callback")); Assert.Equal(1, h.Count("v2:callback"));
        }
        finally { release.Set(); await callback; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HostStopClosesReloadAdmissionAndDrains(bool di)
    {
        await using var h = new Harness(di); await h.Initialize(); h.Tick();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnCheck = key => { if (key == "v2:start-wait") { entered.TrySetResult(); return true; } return false; };
        var pending = h.Session.ReplaceAsync(h.Request(true)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await h.App.StopAsync(); Assert.Equal(ModLoadError.Cancelled, (await pending).Diagnostic.Error);
        Assert.True(h.Session.IsDisposed); Assert.Equal(0, h.Host.OwnedGroupCount); Assert.False(h.World.IsDisposed); Assert.Equal(8, h.Value);
        Assert.Throws<ModOperationException>(() => h.Tick()); Assert.Throws<ModOperationException>(() => h.Host.CreateSystemSession(h.World));
        Assert.Equal(ModLoadError.AdmissionClosed, (await h.Session.ReplaceAsync(h.Request())).Diagnostic.Error);
        h.Dispatch(); Assert.Equal(0, h.Count("v1:callback")); Assert.Equal(0, h.Count("v2:callback"));
    }
    private readonly record struct Unsupported(int Value);
    [Theory]
    [InlineData("entry")] [InlineData("component")] [InlineData("group")] [InlineData("root")]
    public async Task RejectUnsupportedReloadProfile(string partition)
    {
        await using var h = new Harness();
        if (partition != "component") await h.Initialize();
        else h.World.AddComponent(h.Entity, new Unsupported(1));
        var request = partition switch { "entry" => h.Request(entry: "Entry"), "group" => h.Request(true, group: "other"), "root" => h.Request(true, id: "other"), _ => h.Request() };
        var result = await h.Session.ReplaceAsync(request); Assert.False(result.Committed);
        Assert.Equal(partition is "group" or "root" ? ModLoadError.DependencyConflict : ModLoadError.ReloadNotSupported, result.Diagnostic.Error);
        Assert.Equal(7, h.Value); Assert.False(h.World.IsFaulted); Assert.Equal(0, h.Count("v2:execute"));
    }
    [Fact]
    public void StatePayloadCopiesBytesAndChecksExactBoundaries()
    {
        byte[] bytes = [1, 2]; var state = new ModSystemState(1, bytes); bytes[0] = 99;
        Assert.Equal(new byte[] { 1, 2 }, state.Bytes.ToArray()); Assert.Equal(ModSystemState.MaximumBytes, new ModSystemState(1, new byte[ModSystemState.MaximumBytes]).Bytes.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModSystemState(1, new byte[ModSystemState.MaximumBytes + 1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModSystemState(0, []));
    }
    [Theory]
    [InlineData("success")] [InlineData("reject")] [InlineData("retire-failure")] [InlineData("execute-failure")]
    public void RetiredAndRejectedContextsCollectWithRetainedCallbacksAndDisposedSession(string partition)
    {
        var (ticket, keep) = CollectibleScenario(partition);
        for (var i = 0; ticket.IsAlive && i < 30; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(10); }
        Assert.True(ticket.UnloadRequested); Assert.False(ticket.IsAlive); GC.KeepAlive(keep);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ModUnloadTicket, object) CollectibleScenario(string partition)
    {
        var h = new Harness();
        try
        {
            h.Initialize().GetAwaiter().GetResult(); h.Tick();
            if (partition == "execute-failure")
            {
                var ticket = h.Session.CurrentUnloadTicket!; h.Failures.Add("v1:execute");
                var error = Assert.Throws<ModOperationException>(() => h.Tick()); Assert.Null(error.InnerException);
                h.Failures.Clear(); Assert.Equal(ModLoadError.None, h.Session.StopAsync().GetAwaiter().GetResult().Error); Assert.True(h.World.IsFaulted);
                return (ticket, h);
            }
            if (partition == "reject") h.Failures.Add("v2:restore");
            if (partition == "retire-failure") h.Failures.Add("v1:system-dispose");
            var result = h.Session.ReplaceAsync(h.Request(true)).GetAwaiter().GetResult();
            h.Failures.Clear(); Assert.Equal(ModLoadError.None, h.Session.RetryCleanupAsync().GetAwaiter().GetResult().Error);
            return (result.UnloadTicket!, new object[] { h, result, h.Callbacks.ToArray(), h.Contexts.ToArray() });
        }
        finally { h.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
}
