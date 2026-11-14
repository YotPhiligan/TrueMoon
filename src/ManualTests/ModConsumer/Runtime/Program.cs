using System.Runtime.CompilerServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Mods;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.ModConsumer.Runtime;

internal static class Program
{
private static void Main(string[] args)
{
if (args.Length != 2) throw new ArgumentException("artifactDirectory cobalt|msdi");
var report = Proof.Run(args[0], args[1] == "msdi");
for (var i = 0; report.Ticket.IsAlive && i < 30; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Thread.Sleep(20); }
if (report.Ticket.IsAlive) throw new InvalidOperationException("Collectible context remains rooted.");
if (Directory.GetFiles(AppContext.BaseDirectory, "TrueMoon.Aether.*.dll").Any(p => p.Contains("Tooling", StringComparison.Ordinal) || p.Contains("Editor", StringComparison.Ordinal)))
    throw new InvalidOperationException("Runtime consumer unexpectedly received tooling/UI.");
Console.WriteLine(JsonSerializer.Serialize(new { Success = true, DI = args[1], Components = 1, Entities = 1, Ticks = 2,
    report.Events, UnloadRequested = report.Ticket.UnloadRequested, Collected = !report.Ticket.IsAlive,
    SdkPresent = Directory.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "sdk")), ToolingDelivered = false }));
GC.KeepAlive(report.Group); GC.KeepAlive(report.Session);
}
}

internal static class Proof
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (ModUnloadTicket Ticket, ModGroup Group, ModWorldSession Session, string[] Events) Run(string directory, bool di)
    {
        var events = new List<string>();
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => s.Singleton<Action<string>>(_ => events.Add).Singleton<Func<string, bool>>(_ => _ => false));
            c.UseAetherEcs(); c.UseAetherMods();
        }).Build();
        try
        {
            app.StartAsync().GetAwaiter().GetResult(); var host = app.Services.Resolve<ModHost>()!;
            var result = host.LoadAsync(new("root", [directory])).GetAwaiter().GetResult();
            if (!result.Success) throw new InvalidOperationException(result.Diagnostic.ToString());
            var group = result.Group!; var session = group.CreateWorld(Scene());
            if (session.EntityCount != 1 || !session.Tick(TimeSpan.FromMilliseconds(20)) || !session.Tick(TimeSpan.FromMilliseconds(20))) throw new InvalidOperationException("World/ticks failed.");
            if (group.UnloadAsync().GetAwaiter().GetResult().Error != ModLoadError.None) throw new InvalidOperationException("Cleanup failed.");
            string[] expected = ["root:register", "service:start", "root:start", "execute:7", "execute:8", "root:stop", "service:stop", "system:stop", "system:dispose", "service:dispose", "root:dispose"];
            if (!events.SequenceEqual(expected) || host.OwnedGroupCount != 0 || !session.IsDisposed) throw new InvalidOperationException("Ownership/order evidence differs.");
            return (group.UnloadTicket, group, session, events.ToArray());
        }
        finally { app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    private static SceneDocument Scene()
    {
        var type = new ComponentTypeId(new("f89ee66e-2568-4542-8797-874790d173a9"));
        var schema = new ComponentSchema(type, 1, [new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);
        var definition = new EntityDefinition(new(Guid.NewGuid()), new EntityPatch(addedComponents:
            [new ComponentData(new(Guid.NewGuid()), type, 1, [new("value", PropertyValue.From(7L))])]));
        return new(new(Guid.NewGuid()), 0, [new EntityInstance(new(Guid.NewGuid()), definition.Id)], [definition], [schema]);
    }
}
