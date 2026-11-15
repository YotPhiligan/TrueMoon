using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Kton;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bepu.Hosting;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.ManualTests.KtonEcs;

internal static class Program
{
    private static readonly List<object> Checks = [], Owners = [];
    private static string _provider = "";
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException(_provider + ": " + name); Checks.Add(new { Provider = _provider, Name = name }); }
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("KtonEcsProbe <report.json>");
        foreach (var microsoft in new[] { false, true }) await Run(microsoft);
        var neutral = typeof(PhysicsBody).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Check("neutral-sdk-no-runtime-provider", !neutral.Any(name => name != null && (name.StartsWith("TrueMoon.Aether.Runtime", StringComparison.Ordinal) || name.Contains("Bepu") || name.Contains("Tooling") || name.Contains("Alloy"))));
        var runtime = typeof(PhysicsSceneSubsystem).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Check("runtime-bridge-provider-neutral", !runtime.Any(name => name != null && (name.Contains("Bepu") || name.Contains("Tooling") || name.Contains("Alloy"))));
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name is { } name &&
            (name.StartsWith("TrueMoon", StringComparison.Ordinal) || name is "BepuPhysics" or "BepuUtilities")).Select(assembly => new
            { Name = assembly.GetName().Name, assembly.Location, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), References = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray() }).ToArray();
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Completed = true, Stage = "PHYS5", CheckCount = Checks.Count, Checks, Owners, Assemblies = assemblies, NativeRendering = false }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    private static async Task Run(bool microsoft)
    {
        _provider = microsoft ? "Microsoft" : "Cobalt";
        var witnesses = new List<Observer>();
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseBepuPhysics(options => options.DefaultWorld = new() { Limits = new() { MaxBodies = 16, MaxShapes = 32, MaxContactEvents = 128 } });
            context.UseAetherEcs().UseAetherPrimitiveEcs().UseAetherKton(options => options.MaximumContactEvents = 64);
            context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.Services(services =>
            {
                services.Singleton<IFactory<Observer, SceneSubsystemArguments>>(_ => new Factory<Observer, SceneSubsystemArguments>(arguments =>
                { var observer = new Observer(arguments.Scene.World); witnesses.Add(observer); return observer; }));
                services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
                {
                    var loader = resolver.Resolve<SceneWorldLoader>()!; var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                    return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
                });
            });
            context.UseAetherSceneSubsystems(subsystems => subsystems.AddKtonPhysics().Add<Observer>("proof.gameplay"));
        }).Build();
        var host = app.Services.Resolve<BepuPhysicsHost>()!;
        Check("build-opens-no-world", host.Resources.ActiveResourceOwners == 0);
        await app.StartAsync(); var games = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!;
        Check("start-and-resolution-remain-lazy", host.Resources.PublishedWorlds == 0 && witnesses.Count == 0);
        using var first = games.Create() ?? throw new InvalidOperationException("Null game factory result.");
        using var second = games.Create() ?? throw new InvalidOperationException("Null game factory result.");
        var data = Document(); first.LoadSceneWorld(new(data)); second.LoadSceneWorld(new(data));
        var scene = first.ActiveScene!; var observer = witnesses[0];
        var dynamicEntity = scene.Entities[data.Entities[1].Id]; var kinematic = scene.Entities[data.Entities[2].Id];
        Check("two-independent-session-worlds", scene.World.Id != second.ActiveScene!.World.Id && host.Resources.PublishedWorlds == 2);
        Check("private-preparation-has-no-tick", first.TickNumber == 0 && second.TickNumber == 0);
        Check("physics-owns-pose", scene.World.HasComponent<ExternalMotion>(dynamicEntity) && scene.World.HasComponent<PhysicsVelocity>(dynamicEntity));
        first.SetPlaying(true);
        for (var i = 0; i < 180; i++) first.Tick();
        first.SetPlaying(false);
        var pose = scene.World.GetComponent<LocalTransform>(dynamicEntity);
        Check("dynamic-cube-fell-and-settled", pose.Position.Y is > .45f and < .6f);
        Check("kinematic-velocity-integrates", MathF.Abs(scene.World.GetComponent<LocalTransform>(kinematic).Position.X - 4.3f) < .01f);
        Check("other-session-not-advanced", second.TickNumber == 0 && second.ActiveScene!.World.GetComponent<LocalTransform>(second.ActiveScene.Entities[data.Entities[1].Id]).Position.Y == 2);
        Check("gameplay-reads-prior-tick-contacts", observer.ContactReads > 0 && !observer.WrongContactTick);
        Check("same-tick-cpu-render-pose", first.CaptureRenderScene().Primitives.Any(primitive => Vector3.Distance(primitive.World.Translation, pose.Position) < .0001f));
        Check("pause-does-not-advance", first.TickNumber == 180);
        observer.Spawn = true; first.Tick();
        var spawned = default(EntityHandle);
        scene.World.Query<PhysicsBody>().ForEach((EntityHandle entity, in PhysicsBody body) => { if (body.Shape == PhysicsColliderShape.Sphere) spawned = entity; });
        Check("phase-spawn-reconciled-same-tick", spawned != default && scene.World.GetComponent<LocalTransform>(spawned).Position.Y < 4);
        observer.Destroy = spawned; first.Tick();
        Check("phase-delete-retired-entity", !scene.World.IsAlive(spawned));
        observer.Spawn = true; first.Tick();
        var replacement = default(EntityHandle);
        scene.World.Query<PhysicsBody>().ForEach((EntityHandle entity, in PhysicsBody body) => { if (body.Shape == PhysicsColliderShape.Sphere) replacement = entity; });
        Check("spawn-generation-not-revived", replacement != spawned && !scene.World.IsAlive(spawned));
        observer.Teleport = dynamicEntity; first.Tick();
        Check("explicit-teleport-clear-velocity", scene.World.GetComponent<LocalTransform>(dynamicEntity).Position.Y is > 4.9f and < 5);
        using (var invalid = games.Create() ?? throw new InvalidOperationException("Null game."))
        {
            try { invalid.LoadSceneWorld(new(Document(scale: 2))); throw new InvalidOperationException("Scaled physics accepted."); }
            catch (NotSupportedException) { }
            Check("invalid-candidate-not-published", invalid.ActiveScene == null && host.Resources.PublishedWorlds == 2);
        }
        var previous = first.TickNumber; first.Tick();
        Check("failed-candidate-preserves-old-session", first.TickNumber == previous + 1 && !first.IsFaulted);
        first.Dispose();
        Check("stop-releases-own-native-world", scene.IsDisposed && host.Resources.PublishedWorlds == 1 && observer.SystemDisposals == 1 && observer.Disposed);
        second.Tick(); second.Dispose();
        using (var replaced = games.Create() ?? throw new InvalidOperationException("Null replacement."))
        { replaced.LoadSceneWorld(new(data)); replaced.Tick(); Check("replace-starts-fresh-scene", replaced.ActiveScene!.World.Id != scene.World.Id && replaced.TickNumber == 1); }
        await app.StopAsync();
        var resources = host.Resources;
        Check("final-owners-pools-zero", host.IsReleased && resources.PublishedWorlds == 0 && resources.ActiveResourceOwners == 0 && resources.MaximumPoolBytesAfterCleanup == 0 && !resources.PendingFailedCreationCleanup);
        Owners.Add(new { Provider = _provider, Resources = resources });
    }
    private static SceneDocument Document(double scale = 1)
    {
        var ground = new EntityDefinitionId(Guid.NewGuid()); var falling = new EntityDefinitionId(Guid.NewGuid()); var moving = new EntityDefinitionId(Guid.NewGuid());
        EntityDefinition Definition(EntityDefinitionId id, PhysicsBodyMotion motion, double y, double x, double width, double mass, bool velocity = false) => new(id, new(new ComponentData[]
        {
            new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1, [Number("x", x), Number("y", y), Number("sx",scale), Number("sy",scale), Number("sz",scale)]),
            new(new(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
            new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1, [Integer("motion",(long)motion), Number("mass",mass), Number("x",width), Number("y",1), Number("z",width)])
        }.Concat(velocity ? new ComponentData[] { new(new(Guid.NewGuid()), PhysicsComponentSchemas.VelocityId,1,[Number("x",.1)]) } : [])));
        return new(new(Guid.NewGuid()), 7,
            [new(new(Guid.NewGuid()),ground),new(new(Guid.NewGuid()),falling),new(new(Guid.NewGuid()),moving)],
            [Definition(ground,PhysicsBodyMotion.Static,-.5,0,20,0),Definition(falling,PhysicsBodyMotion.Dynamic,2,0,1,1),Definition(moving,PhysicsBodyMotion.Kinematic,1,4,1,0,true)],
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body,PhysicsComponentSchemas.Velocity]));
    }
    private static KeyValuePair<string, PropertyValue> Number(string key, double value) => new(key, PropertyValue.From(value));
    private static KeyValuePair<string, PropertyValue> Integer(string key, long value) => new(key, PropertyValue.From(value));
    private sealed class Observer : ISceneSubsystem
    {
        internal readonly World World;
        internal bool Spawn, WrongContactTick, Disposed;
        internal EntityHandle Destroy, Teleport;
        internal int ContactReads, SystemDisposals;
        public IReadOnlyList<EcsSystemRegistration> Systems { get; }
        internal Observer(World world)
        {
            World = world;
            var description = PhysicsEntityCommands.DeclareWrites(new A.EcsSystemDescription("proof.gameplay", EcsPhase.Gameplay)).Read<PhysicsContact>();
            Systems = [new(description,new Factory<A.ISystem,World>(_ => new ObserveSystem(this)))];
        }
        public void SetPlaying(bool playing) { if (Disposed) throw new ObjectDisposedException(nameof(Observer)); }
        public void Dispose() { if (SystemDisposals != 1) throw new InvalidOperationException("Wrong cleanup order."); Disposed = true; }
        private sealed class ObserveSystem(Observer owner) : A.ISystem
        {
            public void Execute(A.EcsSystemContext context)
            {
                context.Query<PhysicsContact>().ForEach((EntityHandle entity,in PhysicsContact contact) => { owner.ContactReads++; owner.WrongContactTick |= contact.Contact.Tick != context.TickNumber - 1; });
                if (owner.Spawn)
                { PhysicsEntityCommands.CreateBody(context.Commands,new(PhysicsBodyMotion.Dynamic,PhysicsColliderShape.Sphere,new(.25f,0,0),1),new(new(2,4,0),Quaternion.Identity,Vector3.One)); owner.Spawn = false; }
                if (owner.Destroy != default) { context.Commands.DestroyEntity(owner.Destroy); owner.Destroy = default; }
                if (owner.Teleport != default)
                { context.Commands.AddComponent(owner.Teleport,new PhysicsTeleport(1,new(new(0,5,0),Quaternion.Identity),true)); owner.Teleport = default; }
            }
            public void Dispose() { if (owner.Disposed) throw new InvalidOperationException("Dependency released first."); owner.SystemDisposals++; }
        }
    }
}
