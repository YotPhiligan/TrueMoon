using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;

namespace TrueMoon.ManualTests.ModConsumer.Runtime;

internal static class ReloadProof
{
    internal static void Run(string first, string second, string di)
    {
        var owner = Replace(first, second, di == "msdi");
        try
        {
            Collect(owner.Old); Require(!owner.Old.IsAlive, "Retired ALC remains rooted while current generation is live.");
            Require(owner.Current.IsAlive && !owner.Current.UnloadRequested, "Current generation was unexpectedly unloaded.");
        }
        finally { owner.App.DisposeAsync().AsTask().GetAwaiter().GetResult(); owner.World.Dispose(); }
        Collect(owner.Current); Require(!owner.Current.IsAlive, "Current ALC remains rooted after App cleanup.");
        if (Directory.GetFiles(AppContext.BaseDirectory, "TrueMoon.Aether.*.dll").Any(p => p.Contains("Tooling", StringComparison.Ordinal) || p.Contains("Editor", StringComparison.Ordinal)))
            throw new InvalidOperationException("Runtime received tooling/UI.");
        Console.WriteLine(JsonSerializer.Serialize(new { Success = true, DI = di, Components = 1, Entities = 1, Ticks = 3, Generation = owner.Session.Generation,
            WorldPreserved = true, StatePreserved = true, ValueAfterReload = 19, OldCommandsRejected = true, OldCallbacks = 0, CurrentCallbacks = 1,
            UnloadRequested = owner.Old.UnloadRequested && owner.Current.UnloadRequested, Collected = !owner.Old.IsAlive && !owner.Current.IsAlive,
            RetiredCollectedWhileCurrentLive = true, SdkPresent = Directory.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "sdk")), ToolingDelivered = false }));
        GC.KeepAlive(owner); // Includes retained stale contexts, Guard delegates, disposed World and session.
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
                .Singleton<Action<Action>>(_ => callback => { callback(); callbacks.Add(callback); })
                .Singleton<Action<object>>(_ => contexts.Add));
            c.UseAetherEcs(); c.UseAetherMods();
        }).Build();
        World? world = null;
        try
        {
            app.StartAsync().GetAwaiter().GetResult(); var host = app.Services.Resolve<ModHost>()!;
            world = app.Services.Resolve<IFactory<World>>()!.Create()!;
            var entity = world.CreateEntity(); world.AddComponent(entity, new LinearVelocity(new Vector3(7, 0, 0))); var worldId = world.Id;
            var session = host.CreateSystemSession(world); var initial = session.ReplaceAsync(new("root", [first])).GetAwaiter().GetResult();
            Require(initial.Committed, initial.Diagnostic.ToString()); var old = session.CurrentUnloadTicket!;
            Require(session.Tick(TimeSpan.FromMilliseconds(20)) && session.Tick(TimeSpan.FromMilliseconds(20)), "Initial ticks failed.");
            Require(world.GetComponent<LinearVelocity>(entity).Value.X == 9, "Old system did not mutate the stable typed component.");
            var result = session.ReplaceAsync(new("root", [second])).GetAwaiter().GetResult();
            Require(result.Committed && result.Cleanup.Error == ModLoadError.None && old.UnloadRequested, result.ToString());
            Require(world.Id == worldId && session.WorldId == worldId && world.IsAlive(entity) && world.EntityCount == 1, "World/handle/data identity changed.");
            Require(session.Tick(TimeSpan.FromMilliseconds(20)), "New tick failed.");
            Require(world.GetComponent<LinearVelocity>(entity).Value.X == 19 && events.Contains("v2:tick:3:state:3"), "Tick/system state was lost or reset.");
            foreach (var callback in callbacks) callback();
            Require(events.Count(e => e == "v1:callback") == 0 && events.Count(e => e == "v2:callback") == 1, "Generation callback fence failed.");
            var rejected = false;
            try { ((EcsSystemContext)contexts[0]).Commands.CreateEntity(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected && world.EntityCount == 1 && host.OwnedGroupCount == 1, "Stale commands/ownership fence failed.");
            return new(app, world, session, old, session.CurrentUnloadTicket!, callbacks, contexts);
        }
        catch { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); world?.Dispose(); throw; }
    }
    private sealed record Owner(IApp App, World World, ModSystemSession Session, ModUnloadTicket Old, ModUnloadTicket Current, List<Action> Callbacks, List<object> Contexts);
}
