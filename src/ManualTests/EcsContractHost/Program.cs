using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using A = TrueMoon.Aether.Ecs;
using ComponentData = TrueMoon.Aether.Scenes.ComponentData;
using ComponentSchema = TrueMoon.Aether.Scenes.ComponentSchema;
using EcsSystemMetadata = TrueMoon.Aether.Ecs.EcsSystemMetadata;
using EntityDefinition = TrueMoon.Aether.Scenes.EntityDefinition;
using EntityInstance = TrueMoon.Aether.Scenes.EntityInstance;
using EntityPatch = TrueMoon.Aether.Scenes.EntityPatch;
using IEcsSystem = TrueMoon.Aether.Ecs.IEcsSystem;
using ISystem = TrueMoon.Aether.Ecs.ISystem;
using S = TrueMoon.Aether.Scenes;
using SceneDocument = TrueMoon.Aether.Scenes.SceneDocument;

namespace TrueMoon.ManualTests.EcsContractHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 2 or > 3 || args.Length == 3 && args[2] != "microsoft") throw new ArgumentException("EcsContractHost <extension.dll> <report.json> [microsoft]");
        var path = Path.GetFullPath(args[0]);
        using var load = new ExtensionLoadContext(path);
        var assembly = load.LoadFromAssemblyPath(path);
        Type Type(string name) => assembly.GetType("TrueMoon.ManualTests.ContractExtension." + name, true)!;
        var method = typeof(Program).GetMethod(nameof(Run), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(Type("ExtensionCounter"), Type("UnmarkedVelocity"), Type("ExtensionSystem"), Type("ObserverSystem"));
        await (Task)method.Invoke(null, [args, assembly, load])!;
        return 0;
    }

    private static async Task Run<TCounter, TVelocity, TSystem, TObserver>(string[] args, Assembly assembly, ExtensionLoadContext load)
        where TCounter : struct where TVelocity : struct where TSystem : class, ISystem where TObserver : class, ISystem
    {
        var calls = 0; TSystem? system = null; TObserver? observer = null;
        var schema = (ComponentSchema)typeof(TSystem).GetProperty("ValueSchema")!.GetValue(null)!;
        EcsSystemMetadata Metadata(Type type) => (EcsSystemMetadata)type.GetMethod("Describe")!.Invoke(null, null)!;
        await using var app = App.Builder(builder => { if (args.Length == 3) builder.UseDI(); }).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<TSystem>>(_ => new Factory<TSystem>(() => { calls++; return system = (TSystem)Activator.CreateInstance(typeof(TSystem))!; }));
                services.Singleton<IFactory<TObserver>>(_ => new Factory<TObserver>(() => { calls++; return observer = (TObserver)Activator.CreateInstance(typeof(TObserver))!; }));
                services.Singleton<IFactory<TCounter, ComponentData>>(_ => (IFactory<TCounter, ComponentData>)Activator.CreateInstance(assembly.GetType("TrueMoon.ManualTests.ContractExtension.CounterFactory", true)!)!);
                services.Singleton<IFactory<TVelocity>>(_ => new Factory<TVelocity>(() => (TVelocity)Activator.CreateInstance(typeof(TVelocity), 2)!));
            });
            context.UseAetherEcs();
            context.UseAetherEcsSystems(systems => systems.Add<TSystem>(Metadata(typeof(TSystem))).Add<TObserver>(Metadata(typeof(TObserver))));
            context.UseAetherSceneLoading(resolver => new ComponentRegistryBuilder().RegisterValues(schema, resolver.Resolve<IFactory<TCounter, ComponentData>>()!).Build());
        }).Build();
        if (calls != 0) throw new InvalidOperationException("Eager system activation.");
        await app.StartAsync();
        var registry = app.Services.Resolve<ComponentRegistry>()!;
        var loader = app.Services.Resolve<SceneWorldLoader>()!;
        var schedulers = app.Services.Resolve<IFactory<EcsScheduler, World>>()!;
        if (calls != 0) throw new InvalidOperationException("Resolving factories must not create systems.");
        var component = new ComponentData(new(Guid.NewGuid()), new(schema.TypeId.Value), schema.Version);
        var definition = new EntityDefinition(new(Guid.NewGuid()), new EntityPatch(addedComponents: [component]));
        var entity = new EntityInstance(new(Guid.NewGuid()), definition.Id);
        var document = new SceneDocument(new(Guid.NewGuid()), 0, [entity], [definition], registry.Schemas);
        Guid worldId;
        using (var scene = loader.Load(document))
        {
            var world = scene.World; worldId = world.Id;
            world.AddComponent(scene.Entities[entity.Id], app.Services.Resolve<IFactory<TVelocity>>()!.Create());
            using (var scheduler = schedulers.Create(world))
            {
                for (var i = 1; i <= 2; i++)
                {
                    if (!scheduler.Tick(TimeSpan.FromSeconds(1d / 60))) throw new InvalidOperationException("Unexpected stop.");
                    if (Number(observer!, "Total") != 44L * i || Number(observer!, "Count") != i + 1 || Number(system!, "Executions") != i || calls != 2)
                        throw new InvalidOperationException("Real typed storage, callback or phase playback mismatch.");
                }
                if ((Guid)typeof(TObserver).GetProperty("WorldId")!.GetValue(observer)! != world.Id || world.EntityCount != 3 || world.ComponentTypeCount != 2)
                    throw new InvalidOperationException("System used a different World or component column.");
            }
            foreach (var owned in new object[] { system!, observer! })
                if (Number(owned, "Stops") != 1 || Number(owned, "Disposals") != 1) throw new InvalidOperationException("Scheduler ownership mismatch.");
        }
        await app.StopAsync();
        foreach (var owned in new object[] { system!, observer! })
            if (Number(owned, "Stops") != 1 || Number(owned, "Disposals") != 1) throw new InvalidOperationException("DI disposed caller-owned systems twice.");
        var shared = typeof(TSystem).GetInterfaces().Single(t => t.FullName == typeof(IEcsSystem).FullName);
        var privateExtension = AssemblyLoadContext.GetLoadContext(assembly) == load;
        var sharedContracts = ReferenceEquals(shared, typeof(IEcsSystem)) && load.Assemblies.All(a => a.GetName().Name is not ("TrueMoon.Contracts" or "TrueMoon.Aether.Abstractions"));
        var forbidden = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is { } name &&
            (name == "TrueMoon.Aether" || name.StartsWith("TrueMoon.Aether.Editor") || name.StartsWith("TrueMoon.Aether.Tooling") || name.StartsWith("TrueMoon.Alloy") || name.StartsWith("SkiaSharp") || name.StartsWith("Silk.NET"))).Select(a => a.GetName().Name).ToArray();
        if (!privateExtension || !sharedContracts || forbidden.Length != 0) throw new InvalidOperationException("Unexpected runtime assembly graph.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
        await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new
        {
            Completed = true, Stage = "ARCH3.2", Provider = args.Length == 3 ? "Microsoft" : "Cobalt", RealWorld = true,
            FactoryCalls = calls, PrivateExtension = privateExtension, SharedContracts = sharedContracts, ForbiddenAssemblies = forbidden,
            Executions = Number(system!, "Executions"), Stops = Number(system!, "Stops"), Disposals = Number(system!, "Disposals"),
            Total = Number(observer!, "Total"), Count = Number(observer!, "Count"), WorldId = worldId, Schema = schema.TypeId.Value,
            ExtensionAssembly = assembly.Location, ContractAssembly = typeof(ISystem).Assembly.Location,
            Unload = "Unload requested after scope; collectible mod replacement is ARCH3.7 acceptance"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static long Number(object value, string name) => Convert.ToInt64(value.GetType().GetProperty(name)!.GetValue(value));

    // Acceptance helper only; production mod dependency resolution and unload are separate work.
    private sealed class ExtensionLoadContext : AssemblyLoadContext, IDisposable
    {
        private readonly Dictionary<string, Assembly> _shared = new(StringComparer.Ordinal);
        public ExtensionLoadContext(string path) : base(isCollectible: true)
        {
            foreach (var shared in new[] { typeof(ISystem).Assembly, typeof(IFactory<>).Assembly })
            {
                var name = shared.GetName().Name!; _shared.Add(name, shared);
                var copy = Path.Combine(Path.GetDirectoryName(path)!, name + ".dll");
                if (File.Exists(copy) && !SHA256.HashData(File.ReadAllBytes(copy)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(shared.Location))))
                    throw new InvalidDataException("Mismatched contract copy: " + name);
            }
        }
        protected override Assembly? Load(AssemblyName name)
        {
            if (!_shared.TryGetValue(name.Name!, out var shared)) return null;
            if (name.FullName != shared.GetName().FullName) throw new InvalidDataException("Shared contract identity mismatch.");
            return shared;
        }
        public void Dispose() => Unload();
    }
}
