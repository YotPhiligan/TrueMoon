using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Scenes;
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

namespace TrueMoon.Aether.Kton.Tests;

internal static class PhysicsFixture
{
    internal static readonly TimeSpan Step = TimeSpan.FromSeconds(1d / 60);
    internal static EntityId Entity(int index) => new(new Guid($"cb23e647-8944-4fba-91f7-{index + 1:000000000000}"));
    internal static PhysicsBody Body(PhysicsBodyMotion motion = PhysicsBodyMotion.Dynamic) => new(motion, PhysicsColliderShape.Box, Vector3.One, motion == PhysicsBodyMotion.Dynamic ? 1 : 0);
    internal sealed record BodySpec(PhysicsBody Body, Vector3 Position)
    {
        internal PhysicsVelocity? Velocity { get; init; }
        internal Vector3 Scale { get; init; } = Vector3.One;
        internal float Yaw { get; init; }
        internal Vector3? OrdinaryVelocity { get; init; }
        internal int? ParentIndex { get; init; }
    }

    internal static IApp Build(bool microsoft, Action<IAppConfigurationContext>? extra = null,
        PhysicsWorldOptions? world = null, Action<PhysicsBridgeOptions>? bridge = null)
        => App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseBepuPhysics(options => options.DefaultWorld = world ?? new() { Gravity = Vector3.Zero });
            context.UseAetherEcs().UseAetherPrimitiveEcs();
            context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.UseAetherKton(bridge);
            context.UseAetherSceneSubsystems(builder => builder.AddKtonPhysics());
            context.Services(services => services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
            {
                var loader = resolver.Resolve<SceneWorldLoader>()!;
                var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
            }));
            extra?.Invoke(context);
        }).Build();

    internal static PrimitiveGameSession Game(IApp app) => app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
    internal static BepuPhysicsHost Host(IApp app) => app.Services.Resolve<BepuPhysicsHost>()!;
    internal static SceneWorldLoader Loader(IApp app) => app.Services.Resolve<SceneWorldLoader>()!;
    internal static IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments> Subsystems(IApp app)
        => app.Services.Resolve<IFactory<PhysicsSceneSubsystem, SceneSubsystemArguments>>()!;

    internal static SceneDocument Document(params BodySpec[] bodies)
    {
        var entities = new List<EntityInstance>(); var definitions = new List<EntityDefinition>();
        for (var i = 0; i < bodies.Length; i++)
        {
            var spec = bodies[i]; var definition = new EntityDefinitionId(Guid.NewGuid());
            var components = new List<ComponentData>
            {
                new(new(Guid.NewGuid()), BuiltInComponents.TransformId, 1,
                    [Number("x",spec.Position.X),Number("y",spec.Position.Y),Number("z",spec.Position.Z),Number("yaw",spec.Yaw),Number("sx",spec.Scale.X),Number("sy",spec.Scale.Y),Number("sz",spec.Scale.Z)]),
                new(new(Guid.NewGuid()), BuiltInComponents.AppearanceId, 1),
                new(new(Guid.NewGuid()), PhysicsComponentSchemas.BodyId, 1,
                    [Integer("motion",(long)spec.Body.Motion),Integer("shape",(long)spec.Body.Shape),Number("x",spec.Body.Size.X),Number("y",spec.Body.Size.Y),Number("z",spec.Body.Size.Z),
                     Number("mass",spec.Body.Mass),Number("friction",spec.Body.Material.Friction),Integer("layer",spec.Body.Filter.Layer),Integer("mask",spec.Body.Filter.Mask),
                     new("sensor",PropertyValue.From(spec.Body.IsSensor))])
            };
            if (spec.Velocity is { } velocity)
                components.Add(new(new(Guid.NewGuid()), PhysicsComponentSchemas.VelocityId, 1,
                    [Number("x",velocity.Linear.X),Number("y",velocity.Linear.Y),Number("z",velocity.Linear.Z),Number("ax",velocity.Angular.X),Number("ay",velocity.Angular.Y),Number("az",velocity.Angular.Z)]));
            if (spec.OrdinaryVelocity is { } ordinary)
                components.Add(new(new(Guid.NewGuid()), PrimitiveEcsComponents.VelocityId, 1,
                    [Number("x",ordinary.X),Number("y",ordinary.Y),Number("z",ordinary.Z)]));
            entities.Add(new(Entity(i), definition, parentId: spec.ParentIndex is { } parent ? Entity(parent) : null));
            definitions.Add(new(definition, new(components)));
        }
        return new(new(Guid.NewGuid()), 17, entities, definitions,
            BuiltInComponents.Schemas.Concat([PhysicsComponentSchemas.Body, PhysicsComponentSchemas.Velocity, PrimitiveEcsComponents.VelocitySchema]));
    }
    internal static KeyValuePair<string, PropertyValue> Number(string key, double value) => new(key, PropertyValue.From(value));
    internal static KeyValuePair<string, PropertyValue> Integer(string key, long value) => new(key, PropertyValue.From(value));
    internal static Vector3 Position(PrimitiveGameSession game, int index) => game.ActiveScene!.World.GetComponent<LocalTransform>(game.ActiveScene.Entities[Entity(index)]).Position;
}
