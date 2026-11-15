using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Configuration;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using A = TrueMoon.Aether.Abstractions.Ecs;
using S = TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.ManualTests.RuntimeConsumer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length is < 3 or > 4 || args.Length == 4 && args[3] != "microsoft") throw new ArgumentException("RuntimeConsumer <artifact-directory> <prepared-scene> <report.json> [microsoft]");
        var artifact = GameBuildArtifact.Open(Path.GetFullPath(args[0]), BuildIdentity.HostId);
        IFactory<GameBuildLoader, GameBuildReference> games = new Factory<GameBuildLoader, GameBuildReference>(build => GameBuildLoader.Load(build, BuildIdentity.HostId));
        using var load = games.Create(new(artifact.DirectoryPath, artifact.BuildId));
        var assembly = load.Session.GetType().Assembly;
        Type Type(string name) => assembly.GetType("TrueMoon.ManualTests.ContractExtension." + name, true)!;
        var method = typeof(Program).GetMethod(nameof(Run), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(Type("ExtensionCounter"), Type("UnmarkedVelocity"), Type("ExtensionSystem"), Type("ObserverSystem"));
        await (Task)method.Invoke(null, [args, assembly, load])!;
        return 0;
    }

    private static async Task Run<TCounter, TVelocity, TSystem, TObserver>(string[] args, Assembly assembly, GameBuildLoader load)
        where TCounter : struct where TVelocity : struct where TSystem : class, A.ISystem where TObserver : class, A.ISystem
    {
        var calls = 0; TSystem? system = null; TObserver? observer = null;
        var schema = (S.ComponentSchema)typeof(TSystem).GetProperty("ValueSchema")!.GetValue(null)!;
        A.EcsSystemMetadata Metadata(Type type) => (A.EcsSystemMetadata)type.GetMethod("Describe")!.Invoke(null, null)!;
        var prepared = File.ReadAllBytes(Path.GetFullPath(args[1]));
        var resolution = new SceneResolutionService(new Factory<EntityResolver, S.SceneDocument>(scene => new(scene.Definitions, scene.Schemas)));
        var document = new SceneDocumentCodec(resolution).Decode(prepared);
        if (document.Revision != 41 || document.Entities.Length != 1) throw new InvalidDataException("Prepared scene witness mismatch.");
        load.Session.Load(new("prepared", document.Revision, [new() { Id = "seed", X = 7 }]));
        for (var i = 0; i < 4; i++) load.Session.Tick();
        if (load.Session.TickNumber != 4 || load.Session.Revision != 41 || load.Session.PatternColor != 19) throw new InvalidOperationException("Prepared game did not execute.");
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(RuntimeConfigurationSchemas.Engine); builder.Override("Aether:ReportDirectory", Path.GetDirectoryName(Path.GetFullPath(args[2]))!); });
        await using var app = App.Builder(builder => { if (args.Length == 4) builder.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(profile).UseConfiguredAetherRuntime(new RuntimeComposition { HostContractId = BuildIdentity.HostId, GameBuildId = load.Artifact.BuildId });
            context.Services(services =>
            {
                services.Singleton<IFactory<TSystem>>(_ => new Factory<TSystem>(() => { calls++; return system = (TSystem)Activator.CreateInstance(typeof(TSystem))!; }));
                services.Singleton<IFactory<TObserver>>(_ => new Factory<TObserver>(() => { calls++; return observer = (TObserver)Activator.CreateInstance(typeof(TObserver))!; }));
                services.Singleton<IFactory<TCounter, S.ComponentData>>(_ => (IFactory<TCounter, S.ComponentData>)Activator.CreateInstance(assembly.GetType("TrueMoon.ManualTests.ContractExtension.CounterFactory", true)!)!);
                services.Singleton<IFactory<TVelocity>>(_ => new Factory<TVelocity>(() => (TVelocity)Activator.CreateInstance(typeof(TVelocity), 2)!));
            });
            context.UseAetherEcs();
            context.UseAetherEcsSystems(systems => systems.Add<TSystem>(Metadata(typeof(TSystem))).Add<TObserver>(Metadata(typeof(TObserver))));
            context.UseAetherSceneLoading(resolver => new ComponentRegistryBuilder().RegisterValues(schema, resolver.Resolve<IFactory<TCounter, S.ComponentData>>()!).Build());
        }).Build();
        if (calls != 0) throw new InvalidOperationException("Eager system activation.");
        await app.StartAsync();
        var registry = app.Services.Resolve<ComponentRegistry>()!;
        var loader = app.Services.Resolve<SceneWorldLoader>()!;
        var schedulers = app.Services.Resolve<IFactory<EcsScheduler, World>>()!;
        if (calls != 0) throw new InvalidOperationException("Resolving factories must not create systems.");
        if (!registry.Schemas.Single().Properties.ContainsKey("value")) throw new InvalidOperationException("Exact component binding was lost.");
        var entity = document.Entities.Single();
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
        var engine = app.Services.Resolve<RuntimeEngine>()!;
        var batch = app.Services.Resolve<IProcessDiagnosticsSource>()!.Read(1, 0);
        if (batch.BuildAvailable || engine.Gpu.ResourceCount != 0) throw new InvalidOperationException("Runtime activated tooling/native resources.");
        await app.StopAsync();
        foreach (var owned in new object[] { system!, observer! })
            if (Number(owned, "Stops") != 1 || Number(owned, "Disposals") != 1) throw new InvalidOperationException("DI disposed caller-owned systems twice.");
        var shared = typeof(TSystem).GetInterfaces().Single(t => t.FullName == typeof(A.IEcsSystem).FullName);
        var subsystemChecks = await SessionSubsystemProof.Run(args.Length == 4);
        var context = AssemblyLoadContext.GetLoadContext(assembly)!;
        var privateExtension = context.IsCollectible && context != AssemblyLoadContext.Default;
        var sharedContracts = ReferenceEquals(shared, typeof(A.IEcsSystem)) && context.Assemblies.All(a => a.GetName().Name is not ("TrueMoon.Contracts" or "TrueMoon.Aether.Abstractions"));
        var forbidden = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is { } name &&
            (name == "TrueMoon.Aether" || name.StartsWith("TrueMoon.Aether.Editor") || name.StartsWith("TrueMoon.Aether.Tooling") || name.StartsWith("TrueMoon.Alloy") || name.StartsWith("SkiaSharp") || name.StartsWith("Silk.NET"))).Select(a => a.GetName().Name).ToArray();
        if (!privateExtension || !sharedContracts || forbidden.Length != 0) throw new InvalidOperationException("Unexpected runtime assembly graph.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
        await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new
        {
            Completed = true, Stage = "ARCH3.3", GameTicks = load.Session.TickNumber, GameRevision = load.Session.Revision, GameColor = load.Session.PatternColor, load.Artifact.BuildId, RuntimeOnly = true, Provider = args.Length == 4 ? "Microsoft" : "Cobalt", RealWorld = true,
            FactoryCalls = calls, PrivateExtension = privateExtension, SharedContracts = sharedContracts, ForbiddenAssemblies = forbidden, SceneSubsystemChecks = subsystemChecks,
            Executions = Number(system!, "Executions"), Stops = Number(system!, "Stops"), Disposals = Number(system!, "Disposals"),
            Total = Number(observer!, "Total"), Count = Number(observer!, "Count"), WorldId = worldId, Schema = schema.TypeId.Value,
            ExtensionAssembly = assembly.Location, ContractAssembly = typeof(A.ISystem).Assembly.Location,
            Unload = "Unload requested after scope; collectible mod replacement is ARCH3.7 acceptance"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static long Number(object value, string name) => Convert.ToInt64(value.GetType().GetProperty(name)!.GetValue(value));

}
