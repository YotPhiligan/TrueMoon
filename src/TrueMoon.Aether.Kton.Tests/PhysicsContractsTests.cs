using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Ecs.Rendering;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;
using Xunit;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsContractsTests
{
    [Fact]
    public void CaptureShape_PreservesBoxDimensionsAndExplicitPrimitiveSemantics()
    {
        var box = new PhysicsBody(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Box, new(2, 3, 4), 2);
        var sphere = box with { Shape = PhysicsColliderShape.Sphere, Size = new(.4f, 0, 0) };
        var capsule = box with { Shape = PhysicsColliderShape.Capsule, Size = new(.3f, 1.1f, 0) };
        Assert.Equal(new Vector3(2, 3, 4), Assert.IsType<PhysicsBox>(box.CaptureShape()).Size);
        Assert.Equal(.4f, Assert.IsType<PhysicsSphere>(sphere.CaptureShape()).Radius);
        var captured = Assert.IsType<PhysicsCapsule>(capsule.CaptureShape());
        Assert.Equal(.3f, captured.Radius); Assert.Equal(1.1f, captured.CylinderLength);
        Assert.Equal(PhysicsMaterial.Default, box.Material); Assert.Equal(PhysicsCollisionFilter.All, box.Filter); Assert.False(box.IsSensor);
    }

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(99, 0, 0)]
    [InlineData((int)PhysicsColliderShape.Sphere, 1, 0)] [InlineData((int)PhysicsColliderShape.Sphere, 0, 1)]
    [InlineData((int)PhysicsColliderShape.Capsule, 1, 1)]
    public void CaptureShape_RejectsUnknownKindAndUnusedNonzeroDimensions(int shape, float y, float z)
    {
        var body = new PhysicsBody(PhysicsBodyMotion.Dynamic, (PhysicsColliderShape)shape, new(1, y, z), 1);
        Assert.Throws<InvalidDataException>(() => body.CaptureShape());
    }

    [Fact]
    public void Schemas_PinExactIdentitiesKindsDefaultsAndRequiredTypes()
    {
        Assert.Equal(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5a001"), PhysicsComponentSchemas.BodyId.Value);
        Assert.Equal(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5a002"), PhysicsComponentSchemas.VelocityId.Value);
        var body = PhysicsComponentSchemas.Body; var velocity = PhysicsComponentSchemas.Velocity;
        Assert.Equal((PhysicsComponentSchemas.BodyId, 1, false), (body.TypeId, body.Version, body.AllowMultiple));
        Assert.Equal(new[] { BuiltInComponents.TransformId }, body.RequiredTypes);
        Assert.Equal(new[] { PhysicsComponentSchemas.BodyId }, velocity.RequiredTypes);
        Assert.Equal(10, body.Properties.Count); Assert.Equal(6, velocity.Properties.Count);
        Assert.Equal(PropertyValueKind.Integer, body.Properties["shape"].Kind);
        Assert.Equal(1L, body.Properties["shape"].DefaultValue.GetInteger());
        Assert.Equal(1L, body.Properties["motion"].DefaultValue.GetInteger());
        Assert.Equal((long)uint.MaxValue, body.Properties["mask"].DefaultValue.GetInteger());
        Assert.Equal(PropertyValueKind.Number, body.Properties["mass"].Kind);
        Assert.Equal(1d, body.Properties["mass"].DefaultValue.GetNumber());
        Assert.Equal(.8, body.Properties["friction"].DefaultValue.GetNumber());
        Assert.False(body.Properties["sensor"].DefaultValue.GetBoolean());
        Assert.All(velocity.Properties.Values, field => { Assert.Equal(PropertyValueKind.Number, field.Kind); Assert.Equal(0d, field.DefaultValue.GetNumber()); });
    }

    [Fact]
    public void ComponentValues_AreReferenceFreeAndContractsDoNotReferenceRuntimeOrBepu()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsBody>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsVelocity>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsTeleport>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsContact>());
        var references = typeof(PhysicsBody).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.Contains("TrueMoon.Aether.Abstractions", references); Assert.Contains("TrueMoon.Kton", references);
        Assert.DoesNotContain(references, name => name is "TrueMoon.Aether.Runtime" or "TrueMoon.Kton.Bepu" or "BepuPhysics" or "BepuUtilities");
    }

    [Fact]
    public void PhaseHelpers_SpawnDetachReattachAndDeleteCompleteRolesWithoutChangingNeighbor()
    {
        using var world = new World(); var neighbor = world.CreateEntity(); world.AddComponent(neighbor, new PhysicsVelocity(new(9, 8, 7)));
        A.EntityHandle bodyEntity = default;
        var body = new PhysicsBody(PhysicsBodyMotion.Kinematic, PhysicsColliderShape.Box, Vector3.One, 0);
        var pose = LocalTransform.Identity with { Position = new(2, 4, 6) };
        var velocity = new PhysicsVelocity(new(3, 0, 0));
        var description = PhysicsEntityCommands.DeclareWrites(new A.EcsSystemDescription("test.commands"));
        using var scheduler = EcsScheduler.Create(world,
        [new(description, new Factory<A.ISystem>(() => new Callback(context =>
        {
            switch (context.TickNumber)
            {
                case 1: PhysicsEntityCommands.CreateBody(context.Commands, body, pose, velocity); break;
                case 2: PhysicsEntityCommands.RemoveBody(context.Commands, bodyEntity); break;
                case 3: PhysicsEntityCommands.AddBody(context.Commands, bodyEntity, body, velocity); break;
                case 4: context.Commands.DestroyEntity(bodyEntity); break;
            }
        }))) ]);
        Assert.True(scheduler.Tick(TimeSpan.FromSeconds(1d / 60)));
        world.Query<PhysicsBody>().ForEach((A.EntityHandle entity, in PhysicsBody _) => bodyEntity = entity);
        Assert.NotEqual(default, bodyEntity); Assert.Equal(2, world.EntityCount);
        Assert.Equal(pose, world.GetComponent<LocalTransform>(bodyEntity)); Assert.Equal(velocity, world.GetComponent<PhysicsVelocity>(bodyEntity));
        Assert.Equal(default, world.GetComponent<TransformParent>(bodyEntity).Parent);
        Assert.True(world.HasComponent<WorldTransform>(bodyEntity)); Assert.True(world.HasComponent<ExternalMotion>(bodyEntity));
        Assert.True(scheduler.Tick(TimeSpan.FromSeconds(1d / 60)));
        Assert.False(world.HasComponent<PhysicsBody>(bodyEntity)); Assert.False(world.HasComponent<PhysicsVelocity>(bodyEntity));
        Assert.False(world.HasComponent<ExternalMotion>(bodyEntity)); Assert.True(world.HasComponent<LocalTransform>(bodyEntity));
        Assert.True(scheduler.Tick(TimeSpan.FromSeconds(1d / 60))); Assert.Equal(body, world.GetComponent<PhysicsBody>(bodyEntity));
        Assert.True(scheduler.Tick(TimeSpan.FromSeconds(1d / 60))); Assert.False(world.IsAlive(bodyEntity)); Assert.Equal(1, world.EntityCount);
        Assert.Equal(new PhysicsVelocity(new(9, 8, 7)), world.GetComponent<PhysicsVelocity>(neighbor));
    }

    [Fact]
    public void PhaseHelpers_RequireDeclaredWritesAndActiveWriterBeforeStructuralChanges()
    {
        var body = new PhysicsBody(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Box, Vector3.One, 1);
        Assert.Throws<InvalidOperationException>(() => PhysicsEntityCommands.CreateBody(default, body, LocalTransform.Identity));
        Assert.Throws<ArgumentNullException>(() => PhysicsEntityCommands.DeclareWrites(null!));
        using var world = new World();
        using var scheduler = EcsScheduler.Create(world,
        [new(new A.EcsSystemDescription("test.missing-writes"), new Factory<A.ISystem>(() => new Callback(context =>
            PhysicsEntityCommands.CreateBody(context.Commands, body, LocalTransform.Identity))))]);
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(TimeSpan.FromSeconds(1d / 60)));
        Assert.True(world.IsFaulted); Assert.Equal(0, world.EntityCount); Assert.Equal(0UL, scheduler.TickNumber);
    }

    private sealed class Callback(Action<A.EcsSystemContext> execute) : A.ISystem
    {
        public void Execute(A.EcsSystemContext context) => execute(context);
        public void Dispose() { }
    }
}
