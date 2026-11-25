using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;

namespace TrueMoon.Aether.Tests.Mods;

public sealed class ModMigrationTests
{
    private static readonly EntityId First = new(Guid.Parse("38000000-0000-0000-0000-000000000010")), Second = new(Guid.Parse("38000000-0000-0000-0000-000000000011"));
    private static readonly ComponentTypeId Counter = new(Guid.Parse("38000000-0000-0000-0000-000000000001")), Link = new(Guid.Parse("38000000-0000-0000-0000-000000000002"));
    private static ModWorldSnapshot Seed()
    {
        var counter = new ComponentSchema(Counter, 1, [new("value", PropertyValueKind.Integer, PropertyValue.From(0L)), new("obsolete", PropertyValueKind.Integer, PropertyValue.From(0L))]);
        var link = new ComponentSchema(Link, 1, [new("target", PropertyValueKind.String, PropertyValue.Null, true)]);
        var definitions = new List<EntityDefinition>(); var instances = new List<EntityInstance>();
        foreach (var id in new[] { First, Second })
        {
            var data = new ComponentData(new(Guid.NewGuid()), Counter, 1, new Dictionary<string, PropertyValue> { ["value"] = PropertyValue.From(7L), ["obsolete"] = PropertyValue.From(99L) });
            var reference = new ComponentData(new(Guid.NewGuid()), Link, 1, new Dictionary<string, PropertyValue> { ["target"] = PropertyValue.From(Second.Value.ToString("D")) });
            var definition = new EntityDefinition(new(id.Value), new EntityPatch(addedComponents: [data, reference])); definitions.Add(definition);
            instances.Add(new(id, definition.Id, null, id == First ? null : First, "entity", id == First ? 0 : 1));
        }
        return new(new(new(Guid.NewGuid()), 3, instances, definitions, [counter, link]));
    }
    private sealed class Harness : IAsyncDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "TrueMoon-ARCH38-" + Guid.NewGuid().ToString("N"));
        internal readonly ConcurrentQueue<string> Events = new(); internal readonly ConcurrentQueue<Action> Callbacks = new(); internal readonly ConcurrentQueue<object> Contexts = new();
        internal readonly HashSet<string> Failures = new(StringComparer.Ordinal); internal Func<string, bool>? OnCheck;
        internal readonly SharedSystem Shared = new();
        internal readonly IApp App; internal readonly ModHost Host; internal readonly ModWorldMigrationSession Session; internal readonly ModWorldSnapshotCodec Codec;
        internal Harness(bool di = false)
        {
            App = TrueMoon.App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
            {
                c.Services(s => s.Singleton<Action<string>>(_ => Events.Enqueue).Singleton<Func<string, bool>>(_ => key => OnCheck?.Invoke(key) ?? Failures.Contains(key))
                    .Singleton<Action<Action>>(_ => a => { a(); Callbacks.Enqueue(a); }).Singleton<Action<object>>(_ => Contexts.Enqueue)
                    .Singleton<IFactory<ISystem>>(_ => new Factory<ISystem>(() => Shared)));
                c.UseAetherEcs(); c.UseAetherMods();
            }).Build();
            App.StartAsync().GetAwaiter().GetResult(); Host = App.Services.Resolve<ModHost>()!; Session = Host.CreateWorldMigrationSession(); Codec = App.Services.Resolve<ModWorldSnapshotCodec>()!;
        }
        internal ModLoadRequest Request(bool second = false, string group = "migration", string id = "root", bool dependency = false, bool missingDependency = false, string? entry = null)
        {
            var manifest = new ModManifest { Id = id, Version = second ? "2.0.0" : "1.0.0", ReloadGroup = group, EntryAssembly = "ModFixture.dll",
                EntryType = "TrueMoon.ManualTests.ModFixture." + (entry ?? (second ? "SecondMigrationEntry" : "MigrationEntry")), Dependencies = dependency ? [new("dependency", "1.0.0", "3.0.0")] : [] };
            string Publish(ModManifest value) => ModArtifactPublisher.Publish(Root, value, [Path.Combine(AppContext.BaseDirectory, "ModFixture.dll"), Path.Combine(AppContext.BaseDirectory, "ModPrivateDependency.dll")]);
            var root = Publish(manifest);
            return new(id, dependency && !missingDependency ? [root, Publish(manifest with { Id = "dependency", EntryType = "TrueMoon.ManualTests.ModFixture.DependencyMigrationEntry", Dependencies = [] })] : [root]);
        }
        internal async Task Initialize(bool dependency = false) { var result = await Session.LoadAsync(Request(dependency: dependency), Seed()); Assert.True(result.Committed, result.Diagnostic.ToString()); }
        internal bool Tick() => Session.Tick(TimeSpan.FromMilliseconds(20));
        internal async Task<ResolvedComponent> Value() { var s = await Session.CaptureAsync(); return App.Services.Resolve<SceneResolutionService>()!.Resolve(s.Scene).Entities[First].Components.Values.Single(c => c.TypeId == Counter); }
        internal async Task<double> Number() { var v = (await Value()).Properties["value"].Value; return v.Kind == PropertyValueKind.Integer ? v.GetInteger() : v.GetNumber(); }
        public async ValueTask DisposeAsync()
        {
            OnCheck = null; Failures.Clear(); await App.DisposeAsync();
            var path = Path.GetFullPath(Root); if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetTempPath()) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
    private sealed class SharedSystem : IModReloadableSystem
    {
        internal int Ticks, Disposals;
        public void Execute(EcsSystemContext context) => Ticks++;
        public ModSystemState CaptureState() => new(1, BitConverter.GetBytes(Ticks));
        public void RestoreState(ModSystemState state) => Ticks = BitConverter.ToInt32(state.Bytes.Span);
        public void Stop() { }
        public void Dispose() => Disposals++;
    }
    [Fact]
    public async Task CandidateCannotTakeOwnershipOfAnOldSystemInAnotherWorld()
    {
        await using var h = new Harness(); h.Failures.UnionWith(["m1:shared-system", "m2:shared-system"]); await h.Initialize(); h.Tick();
        var world = h.Session.CurrentWorld; var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Same(world, h.Session.CurrentWorld); Assert.Equal(0, h.Shared.Disposals);
        Assert.True(h.Tick()); Assert.Equal(2, h.Shared.Ticks); Assert.Equal(ModLoadError.None, (await h.Session.StopAsync()).Error); Assert.Equal(1, h.Shared.Disposals);
    }
    [Fact]
    public async Task PartialStopDoesNotExposeAReleasedWorldOwner()
    {
        await using var h = new Harness(); await h.Initialize(); var old = h.Session.CurrentWorld; h.Failures.Add("m2:converter-dispose"); Assert.False((await h.Session.ReplaceAsync(h.Request(true))).Committed);
        Assert.Equal(ModLoadError.CleanupFailed, (await h.Session.StopAsync()).Error); Assert.True(old!.IsDisposed); Assert.Null(h.Session.CurrentWorld); Assert.False(h.Session.IsDisposed); Assert.Equal(1, h.Host.OwnedGroupCount);
        h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.StopAsync()).Error); Assert.True(h.Session.IsDisposed); Assert.Equal(0, h.Host.OwnedGroupCount);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task NewWorldPreservesDataIdsRelationsAndSystemState(bool di)
    {
        await using var h = new Harness(di); await h.Initialize(); h.Tick(); h.Tick();
        var old = h.Session.CurrentWorld!; var handle = h.Session.Entity(First); var before = await h.Session.CaptureAsync(); var ticket = h.Session.CurrentUnloadTicket;
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.True(result.Committed, result.Diagnostic.ToString()); Assert.Equal(ModLoadError.None, result.Cleanup.Error);
        Assert.NotEqual(old.Id, h.Session.CurrentWorld!.Id); Assert.True(old.IsDisposed); Assert.False(h.Session.CurrentWorld.IsAlive(handle)); Assert.Same(ticket, result.UnloadTicket);
        Assert.Equal(9, await h.Number()); Assert.True(h.Tick()); Assert.Equal(19, await h.Number()); Assert.Contains("m2:tick:3:state:3", h.Events);
        var after = await h.Session.CaptureAsync(); Assert.Equal(before.Scene.Id, after.Scene.Id); Assert.Equal(before.Scene.Revision, after.Scene.Revision);
        Assert.Equal(First, after.Scene.Entities.Single(e => e.Id == Second).ParentId); Assert.Equal("entity", after.Scene.Entities[0].Name);
        var oldIds = before.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).Select(c => c.Id).OrderBy(c => c.Value);
        Assert.Equal(oldIds, after.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).Select(c => c.Id).OrderBy(c => c.Value));
        Assert.Equal(42, (await h.Value()).Properties["bonus"].Value.GetInteger()); Assert.Equal(99, (await h.Value()).Properties["obsolete"].Value.GetInteger());
        foreach (var callback in h.Callbacks) callback(); Assert.DoesNotContain("m1:callback", h.Events); Assert.Contains("m2:callback", h.Events);
        Assert.Throws<InvalidOperationException>(() => ((EcsSystemContext)h.Contexts.First()).Commands.CreateEntity()); Assert.Equal(1, h.Host.OwnedGroupCount);
    }
    [Fact]
    public async Task UnchangedSchemaUsesNewPrivateTypesWithoutAMigrator()
    { await using var h = new Harness(); await h.Initialize(); h.Tick(); var old = h.Session.CurrentWorld; Assert.True((await h.Session.ReplaceAsync(h.Request())).Committed); Assert.True(old!.IsDisposed); h.Tick(); Assert.Equal(9, await h.Number()); Assert.DoesNotContain("m1:migrate", h.Events); }
    [Theory]
    [InlineData("m2:no-migrator", ModLoadError.MigrationNotSupported)] [InlineData("m2:unknown-type", ModLoadError.MigrationNotSupported)]
    [InlineData("m2:same-version", ModLoadError.MigrationFailed)] [InlineData("m2:migrate", ModLoadError.MigrationFailed)]
    [InlineData("m2:bad-identity", ModLoadError.MigrationFailed)] [InlineData("m2:import", ModLoadError.MigrationFailed)]
    [InlineData("m2:factory", ModLoadError.MigrationFailed)] [InlineData("m2:restore", ModLoadError.MigrationFailed)]
    [InlineData("m1:export", ModLoadError.InvalidSnapshot)] [InlineData("m1:capture", ModLoadError.InvalidSnapshot)]
    [InlineData("m2:start", ModLoadError.ActivationFailed)] [InlineData("m1:quiesce", ModLoadError.ActivationFailed)]
    public async Task RejectionResumesLastGoodWithoutReset(string failure, ModLoadError expected)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); var world = h.Session.CurrentWorld; var handle = h.Session.Entity(First); h.Failures.Add(failure);
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(expected, result.Diagnostic.Error); Assert.Same(world, h.Session.CurrentWorld);
        Assert.False(world!.IsDisposed); Assert.True(world.IsAlive(handle)); Assert.Equal(1, h.Session.Generation); h.Failures.Clear(); Assert.True(h.Tick()); Assert.Equal(9, await h.Number()); Assert.Contains("m1:tick:2:state:2", h.Events);
    }
    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task FieldRemovalRequiresExactDeclarationAndHostAdmission(bool allow, bool undeclared)
    {
        await using var h = new Harness(); await h.Initialize(); h.Failures.Add("m2:drop-field"); if (undeclared) h.Failures.Add("m2:undeclared-removal");
        var result = await h.Session.ReplaceAsync(h.Request(true), new(allow)); Assert.Equal(allow && !undeclared, result.Committed);
        if (result.Committed) { Assert.False((await h.Value()).Properties.ContainsKey("obsolete")); Assert.Equal(42, (await h.Value()).Properties["bonus"].Value.GetInteger()); }
        else { Assert.Equal(ModLoadError.MigrationNotSupported, result.Diagnostic.Error); Assert.Equal(7, await h.Number()); }
    }
    [Theory] [InlineData("group")] [InlineData("root")] [InlineData("legacy")]
    public async Task ProfileIdentityRejectsWithoutRetiringCurrent(string partition)
    { await using var h = new Harness(); await h.Initialize(); var world = h.Session.CurrentWorld; var result = await h.Session.ReplaceAsync(h.Request(true, group: partition == "group" ? "other" : "migration", id: partition == "root" ? "other" : "root", entry: partition == "legacy" ? "Entry" : null)); Assert.False(result.Committed); Assert.Same(world, h.Session.CurrentWorld); Assert.True(h.Tick()); }
    [Fact]
    public async Task ResetRequiresExplicitAdmissionAndCannotBeAnAutomaticFallback()
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); h.Failures.Add("m2:no-migrator"); Assert.False((await h.Session.ReplaceAsync(h.Request(true))).Committed);
        var world = h.Session.CurrentWorld; Assert.Equal(ModLoadError.ResetNotAdmitted, (await h.Session.ResetAsync(h.Request(), Seed(), ModWorldResetAdmission.Deny)).Diagnostic.Error); Assert.Same(world, h.Session.CurrentWorld);
        Assert.True((await h.Session.ResetAsync(h.Request(), Seed(), ModWorldResetAdmission.Allow)).Committed); Assert.True(world!.IsDisposed); Assert.Equal(7, await h.Number()); h.Tick(); Assert.Contains("m1:tick:1:state:1", h.Events);
    }
    [Fact]
    public async Task CorruptInitialSnapshotIsRejectedBeforeEntryCode()
    { await using var h = new Harness(); var seed = Seed(); var result = await h.Session.LoadAsync(h.Request(), new(seed.Scene, formatVersion: 2)); Assert.False(result.Committed); Assert.Equal(ModLoadError.InvalidSnapshot, result.Diagnostic.Error); Assert.Empty(h.Events); Assert.Null(h.Session.CurrentWorld); }
    [Fact]
    public async Task UntrackedLiveEntityCannotSilentlyDisappear()
    {
        await using var h = new Harness(); await h.Initialize(); var entity = h.Session.CurrentWorld!.CreateEntity(); Assert.False((await h.Session.ReplaceAsync(h.Request())).Committed);
        var id = new EntityId(Guid.NewGuid()); h.Session.TrackEntity(id, entity); Assert.True((await h.Session.ReplaceAsync(h.Request())).Committed); Assert.True(h.Session.CurrentWorld.IsAlive(h.Session.Entity(id)));
    }
    private readonly record struct Unknown(int Value);
    [Fact]
    public async Task UnexportableLiveStorageIsRejected()
    { await using var h = new Harness(); await h.Initialize(); h.Session.CurrentWorld!.AddComponent(h.Session.Entity(First), new Unknown(5)); var result = await h.Session.ReplaceAsync(h.Request()); Assert.False(result.Committed); Assert.Equal(ModLoadError.InvalidSnapshot, result.Diagnostic.Error); Assert.Equal(5, h.Session.CurrentWorld.GetComponent<Unknown>(h.Session.Entity(First)).Value); }
    [Theory] [InlineData("m1:export")] [InlineData("m2:migrate")] [InlineData("m2:import")] [InlineData("m2:factory")] [InlineData("m2:restore")]
    public async Task PreparationCannotStructurallyMutateLastGoodWorld(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); var old = h.Session.CurrentWorld;
        h.OnCheck = key => { if (key == stage) old!.CreateEntity(); return false; };
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Same(old, h.Session.CurrentWorld); Assert.Equal(2, old!.EntityCount); Assert.False(old.IsFaulted); h.OnCheck = null; Assert.True(h.Tick()); Assert.Equal(9, await h.Number());
    }
    [Fact]
    public async Task LateCancellationBeforePublicationRejectsPreparedWorld()
    {
        await using var h = new Harness(); await h.Initialize(); var old = h.Session.CurrentWorld; using var cancel = new CancellationTokenSource();
        h.OnCheck = key => { if (key == "m2:restore") cancel.Cancel(); return false; };
        var result = await h.Session.ReplaceAsync(h.Request(true), cancellationToken: cancel.Token); Assert.False(result.Committed); Assert.Equal(ModLoadError.Cancelled, result.Diagnostic.Error); Assert.Same(old, h.Session.CurrentWorld); Assert.False(old!.IsDisposed); h.OnCheck = null; Assert.True(h.Tick());
    }
    [Fact]
    public async Task DanglingReferenceIsRejectedWithoutLosingOldData()
    { await using var h = new Harness(); await h.Initialize(); h.Session.CurrentWorld!.DestroyEntity(h.Session.Entity(Second)); Assert.False((await h.Session.ReplaceAsync(h.Request())).Committed); Assert.Equal(1, h.Session.CurrentWorld.EntityCount); }
    [Theory] [InlineData("start-wait")] [InlineData("quiesce-wait")]
    public async Task CancellationLeavesOldWorldAndDrainsAdmission(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); h.Tick(); var world = h.Session.CurrentWorld; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnCheck = key => { if (key == (stage == "start-wait" ? "m2:" : "m1:") + stage) { entered.TrySetResult(); return true; } return false; };
        using var cancel = new CancellationTokenSource(); var pending = h.Session.ReplaceAsync(h.Request(true), cancellationToken: cancel.Token); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (stage == "start-wait") Assert.True(h.Tick()); cancel.Cancel(); var result = await pending; Assert.False(result.Committed); Assert.Equal(ModLoadError.Cancelled, result.Diagnostic.Error); Assert.Same(world, h.Session.CurrentWorld); h.OnCheck = null; Assert.True(h.Tick());
    }
    [Fact]
    public async Task ResumeFailureAndConverterCleanupAreExplicitAndRetryable()
    {
        await using var h = new Harness(); await h.Initialize(); h.Failures.UnionWith(["m2:converter-dispose", "m1:resume"]);
        var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.False(result.Committed); Assert.Equal(ModLoadError.RollbackFailed, result.Diagnostic.Error); Assert.Equal(ModLoadError.CleanupFailed, result.Cleanup.Error); Assert.Equal(2, h.Host.OwnedGroupCount);
        Assert.True(h.Session.ResumeRequired); Assert.Throws<ModOperationException>(() => h.Tick()); h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.ResumeAsync()).Error); Assert.Equal(ModLoadError.None, (await h.Session.RetryCleanupAsync()).Error); Assert.Equal(1, h.Host.OwnedGroupCount); Assert.True(h.Tick());
    }
    [Theory] [InlineData("m1:stop")] [InlineData("m1:system-dispose")] [InlineData("m1:dispose")]
    public async Task CommittedWorldSurvivesRetirementFailure(string stage)
    {
        await using var h = new Harness(); await h.Initialize(); var old = h.Session.CurrentWorld; h.Failures.Add(stage); var result = await h.Session.ReplaceAsync(h.Request(true)); Assert.True(result.Committed); Assert.Equal(ModLoadError.CleanupFailed, result.Cleanup.Error); Assert.NotSame(old, h.Session.CurrentWorld); Assert.Equal(2, h.Host.OwnedGroupCount); Assert.True(h.Tick()); Assert.Equal(17, await h.Number());
        h.Failures.Clear(); Assert.Equal(ModLoadError.None, (await h.Session.RetryCleanupAsync()).Error); Assert.True(old!.IsDisposed); Assert.True(result.UnloadTicket!.UnloadRequested);
    }
    [Fact]
    public async Task DependencyClosureMigratesAsOneGroup()
    {
        await using var h = new Harness(); await h.Initialize(true); h.Tick(); var old = h.Session.CurrentWorld;
        Assert.False((await h.Session.ReplaceAsync(h.Request(true, dependency: true, missingDependency: true))).Committed); Assert.Same(old, h.Session.CurrentWorld);
        Assert.True((await h.Session.ReplaceAsync(h.Request(true, dependency: true))).Committed); Assert.Contains("dep:quiesce", h.Events); Assert.Contains("dep:dispose", h.Events); h.Tick(); Assert.Equal(18, await h.Number());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task HostStopOwnsCurrentWorldAndCancelsCandidate(bool di)
    {
        await using var h = new Harness(di); await h.Initialize(); var world = h.Session.CurrentWorld; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnCheck = key => { if (key == "m2:start-wait") { entered.TrySetResult(); return true; } return false; }; var pending = h.Session.ReplaceAsync(h.Request(true)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await h.App.StopAsync(); Assert.Equal(ModLoadError.Cancelled, (await pending).Diagnostic.Error); Assert.True(world!.IsDisposed); Assert.True(h.Session.IsDisposed); Assert.Null(h.Session.CurrentWorld); Assert.Equal(0, h.Host.OwnedGroupCount); Assert.Throws<ModOperationException>(() => h.Host.CreateWorldMigrationSession());
    }
    [Fact]
    public async Task HostStopCancelsAnActiveSnapshotQuiesce()
    {
        await using var h = new Harness(); await h.Initialize(); var world = h.Session.CurrentWorld; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnCheck = key => { if (key == "m1:quiesce-wait") { entered.TrySetResult(); return true; } return false; };
        var capture = h.Session.CaptureAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await h.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ModLoadError.Cancelled, (await Assert.ThrowsAsync<ModOperationException>(() => capture)).Diagnostic.Error); Assert.True(world!.IsDisposed); Assert.Equal(0, h.Host.OwnedGroupCount);
    }
    [Theory] [InlineData("success")] [InlineData("reject")] [InlineData("retire")] [InlineData("execute")]
    public void PrivateTypesCollectEvenWithRetainedOldWorldAndCallbacks(string partition)
    {
        var (ticket, keep) = CollectionCase(partition); for (var i = 0; ticket.IsAlive && i < 30; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(10); }
        Assert.True(ticket.UnloadRequested); Assert.False(ticket.IsAlive); GC.KeepAlive(keep);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ModUnloadTicket, object) CollectionCase(string partition)
    {
        var h = new Harness();
        try
        {
            h.Initialize().GetAwaiter().GetResult(); h.Tick(); var old = h.Session.CurrentWorld!;
            if (partition == "execute") { var ticket = h.Session.CurrentUnloadTicket!; h.Failures.Add("m1:execute"); Assert.Null(Assert.Throws<ModOperationException>(() => h.Tick()).InnerException); h.Failures.Clear(); Assert.Equal(ModLoadError.None, h.Session.StopAsync().GetAwaiter().GetResult().Error); return (ticket, new object[] { h, old }); }
            if (partition == "reject") h.Failures.Add("m2:restore"); if (partition == "retire") h.Failures.Add("m1:system-dispose");
            var result = h.Session.ReplaceAsync(h.Request(true)).GetAwaiter().GetResult(); h.Failures.Clear(); Assert.Equal(ModLoadError.None, h.Session.RetryCleanupAsync().GetAwaiter().GetResult().Error);
            return (result.UnloadTicket!, new object[] { h, old, result, h.Callbacks, h.Contexts });
        }
        finally { h.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    [Fact]
    public async Task CodecRejectsCorruptUnknownDuplicateAndOversizedData()
    {
        await using var h = new Harness(); var bytes = h.Codec.Encode(Seed()); var json = Encoding.UTF8.GetString(bytes);
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode("{"u8));
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode("{}"u8));
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode(Encoding.UTF8.GetBytes(json.Replace("\"formatVersion\":1", "\"formatVersion\":99"))));
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode(Encoding.UTF8.GetBytes(json.Replace("\"tickNumber\":0", "\"tickNumber\":0,\"tickNumber\":0"))));
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode(Encoding.UTF8.GetBytes(json.Replace("\"systems\":[]", "\"unknown\":0,\"systems\":[]"))));
        Assert.Throws<InvalidDataException>(() => h.Codec.Decode(new byte[ModWorldSnapshotCodec.MaximumBytes + 1]));
        Assert.Equal(bytes, h.Codec.Encode(h.Codec.Decode(bytes)));
        var state = new ModWorldSnapshot(Seed().Scene, [new("system", new(3, new byte[] { 1, 2 }))], 123);
        var roundTrip = h.Codec.Decode(h.Codec.Encode(state)); Assert.Equal(123UL, roundTrip.TickNumber); Assert.Equal(3, roundTrip.Systems["system"].Version); Assert.Equal(new byte[] { 1, 2 }, roundTrip.Systems["system"].Bytes.ToArray());
        Assert.Throws<ArgumentException>(() => new ModWorldSnapshot(Seed().Scene, [new("same", new(1, [])), new("same", new(1, []))]));
        Assert.Throws<ArgumentException>(() => new ModWorldSnapshot(Seed().Scene, Enumerable.Range(0, 129).Select(i => new KeyValuePair<string, ModSystemState>(i.ToString(), new(1, [])))));
    }
}
