using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using EntityHandle = TrueMoon.Aether.Abstractions.Ecs.EntityHandle;
using EcsSystemContext = TrueMoon.Aether.Abstractions.Ecs.EcsSystemContext;

namespace TrueMoon.ManualTests.ModConsumer.Runtime;

// Acceptance driver: no production GC loop, reflection caches or private mod Types.
internal static class CycleProof
{
    internal static void Run(string first, string second, string di, string kind, int cycles)
    {
        Require(cycles is >= 1 and <= 1000 && kind is "reload" or "migration", "Invalid cycle profile.");
        var state = Start(first, di == "msdi", kind == "migration");
        var tickets = new List<ModUnloadTicket>();
        try
        {
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                var nextSecond = state.Migration != null || cycle % 2 != 0;
                var ticket = Replace(state, nextSecond ? second : first, nextSecond);
                Collect(ticket); Require(ticket.UnloadRequested && !ticket.IsAlive, $"Retired ALC {cycle} is rooted while current World is live.");
                Require(state.CurrentTicket.IsAlive && !state.CurrentTicket.UnloadRequested && state.Host.OwnedGroupCount == 1, "Current/retired ownership differs.");
                tickets.Add(ticket);
            }
        }
        finally { state.App.DisposeAsync().AsTask().GetAwaiter().GetResult(); if (state.Stable != null) state.World.Dispose(); }
        var current = state.CurrentTicket; Collect(current);
        Require(current.UnloadRequested && !current.IsAlive && state.Host.OwnedGroupCount == 0, "Final generation was not released.");
        Require(state.Worlds.All(w => w.IsDisposed), "Owned/borrowed Worlds were not released.");
        Require(state.Events.Count(e => e is "v1:dispose" or "v2:dispose" or "m1:dispose" or "m2:dispose") == cycles + 1, "Entry disposal count differs.");
        Require(state.Events.Count(e => e.EndsWith(":system-dispose", StringComparison.Ordinal)) == cycles + 1, "System disposal count differs.");
        Require(!Directory.GetFiles(AppContext.BaseDirectory, "TrueMoon.Aether.*.dll").Any(p => p.Contains("Tooling", StringComparison.Ordinal) || p.Contains("Editor", StringComparison.Ordinal)), "Runtime received Tooling/UI.");
        Console.WriteLine(JsonSerializer.Serialize(new { Success = true, DI = di, Kind = kind, Cycles = cycles, RetiredCollected = tickets.Count,
            FinalCollected = !current.IsAlive, Collected = tickets.All(t => !t.IsAlive) && !current.IsAlive, UnloadRequested = current.UnloadRequested,
            Generation = state.Generation, Ticks = cycles + 2, Value = state.Expected, Components = state.Migration == null ? 1 : 4,
            Entities = state.Migration == null ? 1 : 2, Owners = state.Host.OwnedGroupCount, WorldsDisposed = state.Worlds.Count,
            OldCallbacks = 0, OldCommandsRejected = true, StatePreserved = true, PersistentIdsPreserved = true,
            RetiredCollectedWhileCurrentLive = true, ToolingDelivered = false,
            SdkPresent = Directory.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "sdk")) }));
        GC.KeepAlive(state); GC.KeepAlive(tickets); // Retain all disposed Worlds, stale contexts, snapshots and guard delegates.
    }
    private static void Collect(ModUnloadTicket ticket)
    { for (var i = 0; ticket.IsAlive && i < 30; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(20); } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static State Start(string first, bool di, bool migration)
    {
        var events = new List<string>(); var callbacks = new List<Action>(); var contexts = new List<object>();
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => s.Singleton<Action<string>>(_ => events.Add).Singleton<Func<string, bool>>(_ => _ => false)
                .Singleton<Action<Action>>(_ => a => { a(); callbacks.Add(a); }).Singleton<Action<object>>(_ => contexts.Add));
            c.UseAetherEcs(); c.UseAetherMods();
        }).Build();
        World? borrowed = null;
        try
        {
            app.StartAsync().GetAwaiter().GetResult(); var host = app.Services.Resolve<ModHost>()!;
            var state = new State(app, host, events, callbacks, contexts);
            ModReloadResult result;
            if (migration)
            {
                state.Migration = host.CreateWorldMigrationSession(); var seed = MigrationProof.CreateCycleSeed();
                state.EntityId = seed.Scene.Entities[0].Id;
                result = state.Migration.LoadAsync(new("root", [first]), seed).GetAwaiter().GetResult();
                Require(result.Committed, result.ToString()); state.World = state.Migration.CurrentWorld!;
                state.Ids = seed.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).Select(c => c.Id).OrderBy(c => c.Value).ToArray();
            }
            else
            {
                borrowed = app.Services.Resolve<IFactory<World>>()!.Create()!; state.World = borrowed;
                state.Entity = borrowed.CreateEntity(); borrowed.AddComponent(state.Entity, new LinearVelocity(new Vector3(7, 0, 0)));
                state.Stable = host.CreateSystemSession(borrowed); result = state.Stable.ReplaceAsync(new("root", [first])).GetAwaiter().GetResult();
                Require(result.Committed, result.ToString());
            }
            state.Worlds.Add(state.World); state.Tick(); state.Tick(); Require(state.Value() == 9, "Initial component ticks differ.");
            state.Expected = 9; state.CurrentTicket = state.Migration?.CurrentUnloadTicket ?? state.Stable!.CurrentUnloadTicket!;
            return state;
        }
        catch { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); borrowed?.Dispose(); throw; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ModUnloadTicket Replace(State state, string next, bool second)
    {
        var oldWorld = state.World; var oldId = oldWorld.Id;
        var oldHandle = state.Migration?.Entity(state.EntityId) ?? state.Entity;
        var generation = state.Generation;
        var result = state.Migration != null ? state.Migration.ReplaceAsync(new("root", [next])).GetAwaiter().GetResult() :
            state.Stable!.ReplaceAsync(new("root", [next])).GetAwaiter().GetResult();
        Require(result.Committed && result.Cleanup.Error == ModLoadError.None && state.Generation == generation + 1, result.ToString());
        if (state.Migration != null)
        {
            state.World = state.Migration.CurrentWorld!; state.Worlds.Add(state.World);
            Require(state.World.Id != oldId && oldWorld.IsDisposed && !state.World.IsAlive(oldHandle), "Old World/handle retirement differs.");
        }
        else Require(state.World.Id == oldId && state.World.IsAlive(oldHandle) && !state.World.IsDisposed, "Stable World identity changed.");
        Require(state.Value() == state.Expected, "Replacement changed live values before a tick.");
        state.Tick(); state.Expected += second ? 10 : 1; Require(state.Value() == state.Expected, "Component migration/reset lost data.");
        var prefix = state.Migration != null ? "m2:" : second ? "v2:" : "v1:";
        Require(state.Events.Contains(prefix + "tick:" + (generation + 2) + ":state:" + (generation + 2)), "System state/tick sequence reset.");
        var before = state.Events.Count(e => e.EndsWith(":callback", StringComparison.Ordinal));
        foreach (var callback in state.Callbacks) callback();
        Require(state.Events.Count(e => e.EndsWith(":callback", StringComparison.Ordinal)) == before + 1, "Retired callback admission remains open.");
        foreach (var context in state.Contexts)
        {
            var rejected = false; try { ((EcsSystemContext)context).Commands.CreateEntity(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Retained command context was reused.");
        }
        state.CurrentTicket = state.Migration?.CurrentUnloadTicket ?? state.Stable!.CurrentUnloadTicket!;
        return result.UnloadTicket!;
    }
    private sealed class State(IApp app, ModHost host, List<string> events, List<Action> callbacks, List<object> contexts)
    {
        internal readonly IApp App = app; internal readonly ModHost Host = host;
        internal readonly List<string> Events = events; internal readonly List<Action> Callbacks = callbacks; internal readonly List<object> Contexts = contexts;
        internal readonly List<World> Worlds = []; internal readonly List<ModWorldSnapshot> Snapshots = [];
        internal World World = null!; internal ModUnloadTicket CurrentTicket = null!;
        internal ModSystemSession? Stable; internal ModWorldMigrationSession? Migration; internal EntityHandle Entity;
        internal EntityId EntityId; internal ComponentId[] Ids = [];
        internal double Expected; internal long Generation => Migration?.Generation ?? Stable!.Generation;
        internal void Tick() => Require(Migration?.Tick(TimeSpan.FromMilliseconds(20)) ?? Stable!.Tick(TimeSpan.FromMilliseconds(20)), "Tick failed.");
        internal double Value()
        {
            if (Migration == null) return World.GetComponent<LinearVelocity>(Entity).Value.X;
            var snapshot = Migration.CaptureAsync().GetAwaiter().GetResult(); Snapshots.Add(snapshot);
            var components = snapshot.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).ToArray();
            Require(components.Select(c => c.Id).OrderBy(c => c.Value).SequenceEqual(Ids), "Persistent occurrence IDs changed.");
            var values = components.Where(c => c.TypeId.Value == Guid.Parse("38000000-0000-0000-0000-000000000001")).ToArray();
            Require(values.Length == 2 && World.EntityCount == 2 && World.IsAlive(Migration.Entity(EntityId)), "Persistent entity/type catalog changed.");
            var value = values[0].Properties["value"]; var result = value.Kind == PropertyValueKind.Integer ? value.GetInteger() : value.GetNumber();
            Require(values.All(c => { var v = c.Properties["value"]; return (v.Kind == PropertyValueKind.Integer ? v.GetInteger() : v.GetNumber()) == result; }), "Entity component values diverged.");
            Require(values.All(c => c.Properties["obsolete"].GetInteger() == 99), "Unchanged data disappeared.");
            var child = snapshot.Scene.Entities.Single(e => e.Id != EntityId);
            var links = components.Where(c => c.Properties.ContainsKey("target")).ToArray();
            Require(child.ParentId == EntityId && links.Length == 2 && links.All(c => c.Properties["target"].GetString() == child.Id.Value.ToString("D")), "Relations changed.");
            return result;
        }
    }
}
