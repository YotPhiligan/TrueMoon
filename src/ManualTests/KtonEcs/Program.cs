using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Scenes;
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
using A = TrueMoon.Aether.Ecs;

namespace TrueMoon.ManualTests.KtonEcs;

internal static partial class Program
{
    private static readonly List<object> Checks = [], Owners = [];
    private static string _provider = "";
    private static void Check(string name, bool value) { if (!value) throw new InvalidOperationException(_provider + ": " + name); Checks.Add(new { Provider = _provider, Name = name }); }
    private static async Task<int> Main(string[] args)
    {
        if (args is ["--t3", var fixture, var report]) return await RunT3(fixture, report);
        if (args.Length != 1 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Usage: KtonEcsProbe <report.json> | --t3 <absolute-fixture.json> <absolute-new-report.json>");
            return 1;
        }
        foreach (var microsoft in new[] { false, true })
        {
            await Run(microsoft);
            await RunAutoStep(microsoft, PhysicsQualityProfile.Baseline60);
            await RunAutoStep(microsoft, PhysicsQualityProfile.FastLinear60);
        }
        _provider = "Microsoft";
        var neutral = typeof(PhysicsBody).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Check("neutral-sdk-no-runtime-provider", !neutral.Any(name => name != null && (name.StartsWith("TrueMoon.Aether.Runtime", StringComparison.Ordinal) || name.Contains("Bepu") || name.Contains("Tooling") || name.Contains("Alloy"))));
        var runtime = typeof(PhysicsSceneSubsystem).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Check("runtime-bridge-provider-neutral", !runtime.Any(name => name != null && (name.Contains("Bepu") || name.Contains("Tooling") || name.Contains("Alloy"))));
        var assemblies = ReadLoadedAssemblies();
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Completed = true, Stage = "PHYS6", CheckCount = Checks.Count, Checks, Owners, Assemblies = assemblies, NativeRendering = false }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static LoadedAssembly[] ReadLoadedAssemblies() =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.GetName().Name is { } name &&
            (name.StartsWith("TrueMoon", StringComparison.Ordinal) || name is "BepuPhysics" or "BepuUtilities"))
        .Select(assembly => new LoadedAssembly(assembly.GetName().Name!, assembly.Location,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
            assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray())).ToArray();

    private sealed record LoadedAssembly(string Name, string Location, string Sha256, string?[] References);

    private const float StepCapsuleHalfHeight = .60f;

    private static IApp BuildApp(bool microsoft, PhysicsQualityProfile quality, List<Observer>? witnesses,
        Action<PhysicsBridgeOptions>? configure = null) =>
        App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseBepuPhysics(options => options.DefaultWorld = witnesses is null
                ? new PhysicsWorldOptions { Quality = quality }
                : new PhysicsWorldOptions { Quality = quality, Limits = new() { MaxBodies = 16, MaxShapes = 32, MaxContactEvents = 128 } });
            context.UseAetherEcs().UseAetherPrimitiveEcs().UseAetherKton(configure);
            context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.Services(services =>
            {
                if (witnesses is not null)
                    services.Singleton<IFactory<Observer, SceneSubsystemArguments>>(_ => new Factory<Observer, SceneSubsystemArguments>(arguments =>
                    { var observer = new Observer(arguments.Scene.World); witnesses.Add(observer); return observer; }));
                services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
                {
                    var loader = resolver.Resolve<SceneWorldLoader>()!; var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                    return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
                });
            });
            context.UseAetherSceneSubsystems(subsystems =>
            {
                subsystems.AddKtonPhysics();
                if (witnesses is not null) subsystems.Add<Observer>("proof.gameplay");
            });
        }).Build();

    private static async Task Run(bool microsoft)
    {
        _provider = microsoft ? "Microsoft" : "Cobalt";
        var witnesses = new List<Observer>();
        await using var app = BuildApp(microsoft, PhysicsQualityProfile.Baseline60, witnesses);
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
        using (var characterGame = games.Create() ?? throw new InvalidOperationException("Null character game."))
        {
            var characterDocument = CharacterDocument(); characterGame.LoadSceneWorld(new(characterDocument));
            var world = characterGame.ActiveScene!.World;
            var character = characterGame.ActiveScene.Entities[characterDocument.Entities[1].Id];
            var floor = characterGame.ActiveScene.Entities[characterDocument.Entities[0].Id];
            for (var i = 0; i < 90; i++) characterGame.Tick();
            var support = world.GetComponent<PhysicsCharacterState>(character);
            Check("character-schema-and-support", support.IsSupported && support.SupportEntity == floor && support.SupportNormal.Y > .99f);
            world.SetComponent(character, new PhysicsCharacterInput(new(2, 0)));
            for (var i = 0; i < 60; i++) characterGame.Tick();
            var walking = world.GetComponent<LocalTransform>(character);
            Check("character-walk-same-tick-pose", walking.Position.X is > 1.8f and < 2.1f && MathF.Abs(world.GetComponent<PhysicsVelocity>(character).Linear.X - 2) < .05f);
            world.SetComponent(character, new PhysicsCharacterInput(Vector2.Zero, 1)); characterGame.Tick();
            Check("character-sequenced-jump", !world.GetComponent<PhysicsCharacterState>(character).IsSupported && world.GetComponent<PhysicsVelocity>(character).Linear.Y > 4.5f);
            for (var i = 0; i < 150; i++) characterGame.Tick();
            Check("character-jump-not-replayed", world.GetComponent<PhysicsCharacterState>(character).IsSupported && world.GetComponent<LocalTransform>(character).Position.Y is > .97f and < 1.03f);
            world.AddComponent(character, new PhysicsTeleport(1, new(new(0, 4, 0), Quaternion.Identity), true)); characterGame.Tick();
            Check("character-teleport-loses-support", !world.GetComponent<PhysicsCharacterState>(character).IsSupported && world.GetComponent<LocalTransform>(character).Position.Y > 3.9f);
        }
        await app.StopAsync();
        RecordCleanup(host, "legacy", "final-owners-pools-zero");
    }

    private static void RecordCleanup(BepuPhysicsHost host, string profile, string check)
    {
        var resources = host.Resources;
        Check(check, host.IsReleased && resources.PublishedWorlds == 0 && resources.ActiveResourceOwners == 0 && resources.MaximumPoolBytesAfterCleanup == 0 && !resources.PendingFailedCreationCleanup);
        Owners.Add(new { Provider = _provider, Profile = profile, Resources = resources });
    }

    private static async Task RunAutoStep(bool microsoft, PhysicsQualityProfile profile)
    {
        _provider = microsoft ? "Microsoft" : "Cobalt";
        var witnesses = new List<Observer>();
        await using var app = BuildApp(microsoft, profile, witnesses);
        var host = app.Services.Resolve<BepuPhysicsHost>()!;
        await app.StartAsync();
        var games = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!;
        var legacy = new ComponentSchema(PhysicsComponentSchemas.CharacterId, 1,
            [DefaultNumber("speed", 6), DefaultNumber("slope", MathF.PI / 4), DefaultNumber("horizontalForce", 1000),
                DefaultNumber("verticalForce", 1000), DefaultNumber("jumpSpeed", 5)],
            requiredTypes: [PhysicsComponentSchemas.BodyId]);
        var current = PhysicsComponentSchemas.CharacterV2;
        (string Name, ComponentSchema Schema, bool OptIn, float Rise)[] cases =
        [
            ("legacy-v1", legacy, false, .25f), ("default-v2", current, false, .25f),
            ("enabled-v2", current, true, .25f), ("tall-v2", current, true, .30f)
        ];

        foreach (var (name, schema, optIn, rise) in cases)
        {
            var prefix = $"auto-step-{profile}-{name}";
            var document = StepDocument(schema, optIn, rise);
            using var game = games.Create() ?? throw new InvalidOperationException("Null game factory result.");
            game.LoadSceneWorld(new(document));
            var scene = game.ActiveScene!;
            var world = scene.World;
            var character = scene.Entities[document.Entities[0].Id];
            var floor = scene.Entities[document.Entities[1].Id];
            var tread = scene.Entities[document.Entities[2].Id];
            var settings = world.GetComponent<PhysicsCharacter>(character);
            Check(prefix + "-settings", settings.MaximumStepHeight == (optIn ? .25f : 0f) && settings.MinimumTreadDepth == .30f);

            for (var i = 0; i < 60; i++) game.Tick();
            var feet = ReadFeet();
            var support = world.GetComponent<PhysicsCharacterState>(character);
            CheckSample("settled", MathF.Abs(feet.Y) <= .02f && SupportedBy(floor));

            var shouldStep = optIn && rise == .25f;
            var lifted = false;
            world.SetComponent(character, new PhysicsCharacterInput(new(0, -2.4f)));
            for (var i = 0; i < 50; i++)
            {
                game.Tick();
                feet = ReadFeet();
                support = world.GetComponent<PhysicsCharacterState>(character);
                if (shouldStep)
                {
                    if (!lifted && feet.Y > .15f)
                    {
                        if (!(feet.Z < 0 && OnTread())) throw Failure("first rise did not publish upward tread support");
                        lifted = true;
                    }
                }
                else if (!(MathF.Abs(feet.Y) <= .02f && feet.Z >= 0))
                    throw Failure("crossed or rose at a blocked riser");
            }

            if (shouldStep)
                CheckSample("outcome", lifted && feet.Z < -.50f && OnTread());
            else
            {
                var velocityZ = world.GetComponent<PhysicsVelocity>(character).Linear.Z;
                CheckSample("outcome", feet.Z is >= .24f and <= .36f && MathF.Abs(velocityZ) <= .10f && SupportedBy(floor));
            }

            Vector3 ReadFeet() => world.GetComponent<LocalTransform>(character).Position - Vector3.UnitY * StepCapsuleHalfHeight;
            bool SupportedBy(EntityHandle entity) => support.IsSupported && support.SupportEntity == entity && support.SupportNormal.Y > .99f;
            bool OnTread() => feet.Y is >= .23f and <= .27f && SupportedBy(tread);
            InvalidOperationException Failure(string reason) =>
                new($"{_provider}: {prefix}: {reason}, tick {game.TickNumber}, feet={feet}, support={support}");
            void CheckSample(string suffix, bool valid)
            {
                if (!valid) throw Failure(suffix);
                Check($"{prefix}-{suffix}", valid);
            }
        }

        await app.StopAsync();
        RecordCleanup(host, profile.ToString(), $"auto-step-{profile}-cleanup");

        static ComponentPropertySchema DefaultNumber(string key, double value) =>
            new(key, PropertyValueKind.Number, PropertyValue.From(value));
    }

    private static SceneDocument StepDocument(ComponentSchema schema, bool optIn, float rise)
    {
        var character = new EntityDefinitionId(Guid.NewGuid());
        var floor = new EntityDefinitionId(Guid.NewGuid());
        var tread = new EntityDefinitionId(Guid.NewGuid());
        return new(new(Guid.NewGuid()), 1,
            [new(new(Guid.NewGuid()), character), new(new(Guid.NewGuid()), floor), new(new(Guid.NewGuid()), tread)],
            [
                Definition(character, new(0, StepCapsuleHalfHeight, 1), new(.30f, StepCapsuleHalfHeight, 0), capsule: true),
                Definition(floor, new(0, -.5f, 1.5f), new(4, 1, 3)),
                Definition(tread, new(0, (rise - 1) / 2, -1.5f), new(4, 1 + rise, 3))
            ],
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body, schema]));

        EntityDefinition Definition(EntityDefinitionId id, Vector3 position, Vector3 size, bool capsule = false)
        {
            List<ComponentData> components =
            [
                new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1, [Number("y", position.Y), Number("z", position.Z)]),
                new(new(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
                new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1,
                    [Integer("motion", (long)(capsule ? PhysicsBodyMotion.Dynamic : PhysicsBodyMotion.Static)),
                        Integer("shape", (long)(capsule ? PhysicsColliderShape.Capsule : PhysicsColliderShape.Box)),
                        Number("x", size.X), Number("y", size.Y), Number("z", size.Z), Number("mass", capsule ? 1 : 0)])
            ];
            if (capsule)
                components.Add(new(new(Guid.NewGuid()), schema.TypeId, schema.Version,
                    optIn ? [Number("maximumStepHeight", .25), Number("minimumTreadDepth", .30)] : []));
            return new(id, new(components));
        }
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
    private static SceneDocument CharacterDocument()
    {
        var floor = new EntityDefinitionId(Guid.NewGuid()); var character = new EntityDefinitionId(Guid.NewGuid());
        EntityDefinition Definition(EntityDefinitionId id, bool capsule) => new(id, new(new ComponentData[]
        {
            new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1, [Number("y",capsule ? 2 : -.5)]),
            new(new(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
            new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1,
                [Integer("motion",capsule ? 1 : 0),Integer("shape",capsule ? 3 : 1),Number("x",capsule ? .5 : 20),Number("y",1),Number("z",capsule ? 0 : 20),Number("mass",capsule ? 1 : 0)])
        }.Concat(capsule ? new ComponentData[] { new(new(Guid.NewGuid()), PhysicsComponentSchemas.CharacterId, 1) } : [])));
        return new(new(Guid.NewGuid()), 1, [new(new(Guid.NewGuid()),floor),new(new(Guid.NewGuid()),character)],
            [Definition(floor,false),Definition(character,true)],
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body,PhysicsComponentSchemas.Character]));
    }
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
