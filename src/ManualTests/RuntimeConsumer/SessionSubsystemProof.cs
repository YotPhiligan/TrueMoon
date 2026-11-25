using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using A = TrueMoon.Aether.Ecs;
using EcsSystemContext = TrueMoon.Aether.Ecs.EcsSystemContext;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.ManualTests.RuntimeConsumer;

internal static class SessionSubsystemProof
{
    internal static async Task<int> Run(bool microsoft)
    {
        var checks = 0; var created = new List<ProofSubsystem>();
        void Check(bool condition) { if (!condition) throw new InvalidOperationException("Scene subsystem acceptance failed at check " + checks); checks++; }
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseAetherEcs().UseAetherPrimitiveEcs();
            context.UseAetherSceneLoading(_ => PrimitiveEcsComponents.Register(new()).Build());
            context.Services(services =>
            {
                services.Singleton<IFactory<ProofSubsystem, SceneSubsystemArguments>>(_ => new Factory<ProofSubsystem, SceneSubsystemArguments>(args =>
                { var owner = new ProofSubsystem(args); created.Add(owner); return owner; }));
                services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
                {
                    var loader = resolver.Resolve<SceneWorldLoader>()!;
                    var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                    return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
                });
            });
            context.UseAetherSceneSubsystems(builder => builder.Add<ProofSubsystem>("consumer.probe"));
        }).Build();
        await app.StartAsync(); var games = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!;
        Check(created.Count == 0);
        var id = new SceneId(Guid.NewGuid()); var definition = new EntityDefinitionId(Guid.NewGuid()); var entity = new EntityId(Guid.NewGuid());
        var document = new SceneDocument(id, 31, [new(entity, definition)], [new(definition, new([
            new(new ComponentId(Guid.NewGuid()), BuiltInComponents.TransformId, 1),
            new(new ComponentId(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
            new(new ComponentId(Guid.NewGuid()), PrimitiveEcsComponents.VelocityId, 1, [new("x", PropertyValue.From(60d))])]))],
            BuiltInComponents.Schemas.Concat([PrimitiveEcsComponents.VelocitySchema]));
        using var first = games.Create() ?? throw new InvalidOperationException("Null session.");
        using var second = games.Create() ?? throw new InvalidOperationException("Null session.");
        first.LoadSceneWorld(new(document)); second.LoadSceneWorld(new(document));
        Check(created.Count == 2 && !created[0].Playing && !created[1].Playing);
        Check(first.ActiveScene!.World.Id != second.ActiveScene!.World.Id && first.TickNumber == 0 && second.TickNumber == 0);
        first.SetPlaying(true); first.Tick(); first.SetPlaying(false);
        Check(!created[0].Playing && first.TickNumber == 1 && second.TickNumber == 0);
        Check(first.ActiveScene!.World.GetComponent<LocalTransform>(first.ActiveScene.Entities[entity]).Position == new Vector3(4, 0, 0));
        Check(first.CaptureRenderScene().Primitives.Single().World.Translation == new Vector3(4, 0, 0));
        var scene = first.ActiveScene; first.Dispose();
        Check(scene.IsDisposed && created[0].Disposed && created[0].SystemDisposals == 1 && !created[1].Disposed);
        second.Tick(); second.Dispose(); await app.StopAsync();
        Check(created.All(owner => owner.Disposed && owner.SystemDisposals == 1));
        return checks;
    }

    private sealed class ProofSubsystem : ISceneSubsystem
    {
        internal bool Playing, Disposed;
        internal int SystemDisposals;
        public IReadOnlyList<EcsSystemRegistration> Systems { get; }
        internal ProofSubsystem(SceneSubsystemArguments arguments)
        {
            if (arguments.FixedStep != TimeSpan.FromSeconds(1d / 60)) throw new InvalidOperationException("Fixed step drift.");
            var world = arguments.Scene.World; var entity = arguments.Scene.Entities.Values.Single();
            world.AddComponent(entity, new ExternalMotion());
            Systems = [new(new EcsSystemDescription("consumer.external-motion", EcsPhase.Movement).After("builtin.movement").Write<LocalTransform>(),
                new Factory<ISystem, World>(borrowed => ReferenceEquals(borrowed, world) ? new Motion(this, world, entity) : throw new InvalidOperationException("World drift.")))];
        }
        public void SetPlaying(bool playing) { if (Disposed) throw new ObjectDisposedException(nameof(ProofSubsystem)); Playing = playing; }
        public void Dispose() { if (SystemDisposals != 1 || Playing) throw new InvalidOperationException("Resource release order."); Disposed = true; }
        private sealed class Motion(ProofSubsystem owner, World world, EntityHandle entity) : ISystem
        {
            public void Execute(EcsSystemContext context)
            { var pose = world.GetComponent<LocalTransform>(entity); world.SetComponent(entity, pose with { Position = pose.Position + new Vector3(4, 0, 0) }); }
            public void Dispose() { if (owner.Disposed) throw new InvalidOperationException("Dependency released before system."); owner.SystemDisposals++; }
        }
    }
}
