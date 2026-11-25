using System.Runtime.CompilerServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;

namespace TrueMoon.ManualTests.ModConsumer.Runtime;

internal static class MigrationProof
{
    internal static ModWorldSnapshot CreateCycleSeed() => Seed();
    internal static void Run(string first, string second, string di)
    {
        var owner = Replace(first, second, di == "msdi");
        try { Collect(owner.Old); Require(!owner.Old.IsAlive && owner.Current.IsAlive && !owner.Current.UnloadRequested, "Old private component Types remain rooted while the new World is live."); }
        finally { owner.App.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        Collect(owner.Current); Require(!owner.Current.IsAlive, "Current private component Types remain rooted after App cleanup.");
        Require(owner.Session.IsDisposed && owner.World.IsDisposed, "App did not own World cleanup.");
        Require(!Directory.GetFiles(AppContext.BaseDirectory, "TrueMoon.Aether.*.dll").Any(p => p.Contains("Tooling", StringComparison.Ordinal) || p.Contains("Editor", StringComparison.Ordinal)), "Runtime received Tooling/UI.");
        Console.WriteLine(JsonSerializer.Serialize(new { Success = true, DI = di, Components = 4, Entities = 2, Ticks = 3, Generation = owner.Session.Generation,
            NewWorld = true, PersistentIdsPreserved = true, ReferencesPreserved = true, StatePreserved = true, ValueAfterReload = 19,
            OldCommandsRejected = true, OldCallbacks = 0, CurrentCallbacks = 1, RetiredCollectedWhileCurrentLive = true,
            UnloadRequested = owner.Old.UnloadRequested && owner.Current.UnloadRequested, Collected = !owner.Old.IsAlive && !owner.Current.IsAlive,
            SdkPresent = Directory.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "sdk")), ToolingDelivered = false }));
        GC.KeepAlive(owner); // Old disposed World, current disposed World, callbacks and stale contexts remain held.
    }
    private static void Collect(ModUnloadTicket ticket)
    { for (var i = 0; ticket.IsAlive && i < 30; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(20); } }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Owner Replace(string first, string second, bool di)
    {
        var events = new List<string>(); var callbacks = new List<Action>(); var contexts = new List<object>();
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => s.Singleton<Action<string>>(_ => events.Add).Singleton<Func<string, bool>>(_ => _ => false)
                .Singleton<Action<Action>>(_ => a => { a(); callbacks.Add(a); }).Singleton<Action<object>>(_ => contexts.Add));
            c.UseAetherEcs(); c.UseAetherMods();
        }).Build();
        try
        {
            app.StartAsync().GetAwaiter().GetResult(); var host = app.Services.Resolve<ModHost>()!; var session = host.CreateWorldMigrationSession(); var seed = Seed();
            var initial = session.LoadAsync(new("root", [first]), seed).GetAwaiter().GetResult(); Require(initial.Committed, initial.ToString());
            var old = session.CurrentWorld!; var oldTicket = session.CurrentUnloadTicket!; var id = seed.Scene.Entities[0].Id; var handle = session.Entity(id);
            Require(session.Tick(TimeSpan.FromMilliseconds(20)) && session.Tick(TimeSpan.FromMilliseconds(20)), "Initial ticks failed.");
            var result = session.ReplaceAsync(new("root", [second])).GetAwaiter().GetResult(); Require(result.Committed && result.Cleanup.Error == ModLoadError.None, result.ToString());
            var world = session.CurrentWorld!; Require(world.Id != old.Id && old.IsDisposed && !world.IsAlive(handle), "World/handle retirement failed.");
            Require(session.Tick(TimeSpan.FromMilliseconds(20)) && events.Contains("m2:tick:3:state:3"), "System state/tick was reset.");
            var snapshot = session.CaptureAsync().GetAwaiter().GetResult(); var components = snapshot.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).ToArray();
            Require(components.Where(c => c.SchemaVersion == 2).All(c => c.Properties["value"].GetNumber() == 19 && c.Properties["bonus"].GetInteger() == 42 && c.Properties["obsolete"].GetInteger() == 99), "Migration lost/failed to convert data.");
            Require(components.Select(c => c.Id).OrderBy(i => i.Value).SequenceEqual(seed.Scene.Definitions.SelectMany(d => d.Changes.AddedComponents).Select(c => c.Id).OrderBy(i => i.Value)), "Persistent occurrence IDs changed.");
            Require(snapshot.Scene.Entities.Single(e => e.Id != id).ParentId == id && components.Where(c => c.SchemaVersion == 1).All(c => c.Properties["target"].GetString() == seed.Scene.Entities[1].Id.Value.ToString("D")), "Parent/forward/self references were lost.");
            foreach (var callback in callbacks) callback(); Require(events.Count(e => e == "m1:callback") == 0 && events.Count(e => e == "m2:callback") == 1, "Generation callback fence failed.");
            var rejected = false; try { ((EcsSystemContext)contexts[0]).Commands.CreateEntity(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && world.EntityCount == 2 && host.OwnedGroupCount == 1, "Stale commands/ownership fence failed.");
            return new(app, old, world, session, oldTicket, session.CurrentUnloadTicket!, callbacks, contexts);
        }
        catch { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); throw; }
    }
    private static ModWorldSnapshot Seed()
    {
        var counter = new ComponentTypeId(Guid.Parse("38000000-0000-0000-0000-000000000001")); var link = new ComponentTypeId(Guid.Parse("38000000-0000-0000-0000-000000000002"));
        var schemas = new ComponentSchema[] { new(counter, 1, [new("value", PropertyValueKind.Integer, PropertyValue.From(0L)), new("obsolete", PropertyValueKind.Integer, PropertyValue.From(0L))]), new(link, 1, [new("target", PropertyValueKind.String, PropertyValue.Null, true)]) };
        var ids = new EntityId[] { new(Guid.NewGuid()), new(Guid.NewGuid()) }; var definitions = new List<EntityDefinition>(); var instances = new List<EntityInstance>();
        foreach (var id in ids)
        {
            var data = new ComponentData(new(Guid.NewGuid()), counter, 1, new Dictionary<string, PropertyValue> { ["value"] = PropertyValue.From(7L), ["obsolete"] = PropertyValue.From(99L) });
            var reference = new ComponentData(new(Guid.NewGuid()), link, 1, new Dictionary<string, PropertyValue> { ["target"] = PropertyValue.From(ids[1].Value.ToString("D")) });
            var definition = new EntityDefinition(new(id.Value), new EntityPatch(addedComponents: [data, reference])); definitions.Add(definition); instances.Add(new(id, definition.Id, null, id == ids[0] ? null : ids[0]));
        }
        return new(new(new(Guid.NewGuid()), 0, instances, definitions, schemas));
    }
    private sealed record Owner(IApp App, World OldWorld, World World, ModWorldMigrationSession Session, ModUnloadTicket Old, ModUnloadTicket Current, List<Action> Callbacks, List<object> Contexts);
}
