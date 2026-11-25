using System.Numerics;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using Xunit;
using A = TrueMoon.Aether.Ecs;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsSceneSubsystemTests
{
    [Fact]
    public async Task EmptyScene_StepsWithoutInventingBodiesOrEventsAndReleasesNativeWorld()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document());
        for (var tick = 0; tick < 3; tick++) graph.Tick();
        Assert.Equal(0, graph.World.EntityCount); Assert.Equal(3UL, graph.Physics.LastCompletedTick);
        Assert.False(graph.Physics.TryGetBody(default, out _)); Assert.Equal(1, PhysicsFixture.Host(app).Resources.PublishedWorlds);
        graph.Dispose(); Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_IsLazyAndSessionsOwnIndependentNativeWorlds(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft);
        var host = PhysicsFixture.Host(app); Assert.Equal(0, host.Resources.ActiveResourceOwners);
        await app.StartAsync(); var factory = PhysicsFixture.Subsystems(app);
        Assert.NotNull(factory); Assert.Equal(0, host.Resources.ActiveResourceOwners);
        var document = PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), new(0, 5, 0)) { Velocity = new(new(6, 0, 0)) });
        using var first = PhysicsFixture.Game(app); using var second = PhysicsFixture.Game(app);
        first.LoadSceneWorld(new(document)); second.LoadSceneWorld(new(document));
        Assert.Equal(2, host.Resources.PublishedWorlds); Assert.NotEqual(first.ActiveScene!.World.Id, second.ActiveScene!.World.Id);
        first.Tick();
        Assert.InRange(PhysicsFixture.Position(first, 0).X, .0999f, .1001f); Assert.Equal(0f, PhysicsFixture.Position(second, 0).X);
        Assert.Equal((1UL, 0UL), (first.TickNumber, second.TickNumber));
        first.Dispose(); Assert.Equal(1, host.Resources.PublishedWorlds);
        second.Tick(); Assert.InRange(PhysicsFixture.Position(second, 0).X, .0999f, .1001f);
        second.Dispose(); await app.StopAsync();
        Assert.Equal(0, host.Resources.ActiveResourceOwners); Assert.Equal(0, host.Resources.PublishedWorlds); Assert.Equal(0, host.Resources.MaximumPoolBytesAfterCleanup);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Step_RespectsStaticDynamicKinematicModesAndSkipsOrdinaryMovement(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft); await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(PhysicsFixture.Document(
            new(PhysicsFixture.Body(PhysicsBodyMotion.Static), new(20, 0, 0)),
            new(PhysicsFixture.Body(), new(0, 5, 0)) { Velocity = new(new(6, 0, 0)), OrdinaryVelocity = new(600, 0, 0) },
            new(PhysicsFixture.Body(PhysicsBodyMotion.Kinematic), new(0, 10, 0)) { Velocity = new(new(0, 3, 0)) })));
        var world = game.ActiveScene!.World;
        Assert.All(game.ActiveScene.Entities.Values, entity => { Assert.True(world.HasComponent<ExternalMotion>(entity)); Assert.True(world.HasComponent<PhysicsVelocity>(entity)); });
        game.SetPlaying(true); game.Tick(); game.SetPlaying(false);
        Assert.Equal(new Vector3(20, 0, 0), PhysicsFixture.Position(game, 0));
        Assert.InRange(PhysicsFixture.Position(game, 1).X, .0999f, .1001f);
        Assert.InRange(PhysicsFixture.Position(game, 2).Y, 10.0499f, 10.0501f);
        var paused = PhysicsFixture.Position(game, 1); game.SetPlaying(false);
        Assert.Equal(1UL, game.TickNumber); Assert.Equal(paused, PhysicsFixture.Position(game, 1));
        game.Tick(); Assert.Equal(2UL, game.TickNumber); Assert.InRange(PhysicsFixture.Position(game, 1).X, .1999f, .2001f);
        Assert.Contains(game.CaptureRenderScene().Primitives, primitive => primitive.World.Translation.X is > .1999f and < .2001f);
    }

    [Theory]
    [InlineData(PhysicsColliderShape.Box)] [InlineData(PhysicsColliderShape.Sphere)] [InlineData(PhysicsColliderShape.Capsule)]
    public async Task Gravity_AdvancesAllSupportedPrimitivesAndWritesBackVelocity(PhysicsColliderShape shape)
    {
        await using var app = PhysicsFixture.Build(false, world: new() { Gravity = new(0, -6, 0) }); await app.StartAsync();
        var body = PhysicsFixture.Body() with { Shape = shape, Size = shape switch { PhysicsColliderShape.Sphere => new(.5f, 0, 0), PhysicsColliderShape.Capsule => new(.25f, .5f, 0), _ => Vector3.One } };
        using var game = PhysicsFixture.Game(app); game.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(body, new(0, 5, 0)))));
        for (var tick = 0; tick < 60; tick++) game.Tick();
        Assert.InRange(PhysicsFixture.Position(game, 0).Y, 1.9f, 2.1f);
        var velocity = game.ActiveScene!.World.GetComponent<PhysicsVelocity>(game.ActiveScene.Entities[PhysicsFixture.Entity(0)]);
        Assert.InRange(velocity.Linear.Y, -6.05f, -5.95f); Assert.Equal(60UL, game.TickNumber);
    }

    [Theory]
    [InlineData(PhysicsBodyMotion.Dynamic)] [InlineData(PhysicsBodyMotion.Kinematic)]
    public async Task AngularVelocity_RotatesNativePoseAndChangedCommandWritesBack(PhysicsBodyMotion motion)
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(motion), Vector3.Zero)
            { Velocity = new(Vector3.Zero, new(0, 1, 0)) }));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        for (var tick = 0; tick < 60; tick++) graph.Tick();
        var rotation = graph.World.GetComponent<LocalTransform>(entity).Rotation;
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1), rotation)) > .9999f);
        Assert.Equal(new Vector3(0, 1, 0), graph.World.GetComponent<PhysicsVelocity>(entity).Angular);
        graph.World.SetComponent(entity, new PhysicsVelocity(Vector3.Zero, new(0, -2, 0))); graph.Tick();
        Assert.Equal(new Vector3(0, -2, 0), graph.World.GetComponent<PhysicsVelocity>(entity).Angular);
        Assert.True(graph.World.GetComponent<LocalTransform>(entity).Rotation.Y < rotation.Y);
        Assert.Equal(Vector3.Zero, graph.World.GetComponent<LocalTransform>(entity).Position);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Teleport_IsSequencedClearsVelocityAndReplaysWithoutRepeatingDiscontinuity(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Velocity = new(new(6, 0, 0)), Yaw = .4f }));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        Assert.True(graph.Physics.TryGetBody(entity, out var id)); Assert.True(graph.Physics.TryGetEntity(id, out var mapped)); Assert.Equal(entity, mapped);
        graph.Tick(); Assert.InRange(graph.World.GetComponent<LocalTransform>(entity).Position.X, .0999f, .1001f);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.CreateFromAxisAngle(Vector3.UnitY, .4f), graph.World.GetComponent<LocalTransform>(entity).Rotation)) > .9999f);
        var targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
        var teleport = new PhysicsTeleport(1, new(new(10, 2, 3), targetRotation), true);
        graph.World.AddComponent(entity, teleport); graph.Tick();
        Assert.Equal(new Vector3(10, 2, 3), graph.World.GetComponent<LocalTransform>(entity).Position);
        Assert.True(MathF.Abs(Quaternion.Dot(targetRotation, graph.World.GetComponent<LocalTransform>(entity).Rotation)) > .9999f);
        Assert.Equal(default, graph.World.GetComponent<PhysicsVelocity>(entity));
        graph.World.SetComponent(entity, new PhysicsVelocity(new(6, 0, 0))); graph.Tick();
        Assert.InRange(graph.World.GetComponent<LocalTransform>(entity).Position.X, 10.0999f, 10.1001f);
        graph.World.SetComponent(entity, teleport with { Sequence = 2, Pose = new(new(20, 2, 3), Quaternion.Identity), ClearVelocity = false }); graph.Tick();
        Assert.InRange(graph.World.GetComponent<LocalTransform>(entity).Position.X, 20.0999f, 20.1001f);
        Assert.True(MathF.Abs(Quaternion.Dot(Quaternion.Identity, graph.World.GetComponent<LocalTransform>(entity).Rotation)) > .9999f);
        Assert.True(graph.Physics.TryGetBody(entity, out var current)); Assert.Equal(id, current); Assert.Equal(4UL, graph.Physics.LastCompletedTick);
    }

    [Fact]
    public async Task Mapping_RejectsForeignWorldIdentitiesEvenWhenSlotsMatch()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        var document = PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero));
        using var first = PhysicsGraph.Create(app, document); using var second = PhysicsGraph.Create(app, document);
        var firstEntity = first.Scene.Entities[PhysicsFixture.Entity(0)]; var secondEntity = second.Scene.Entities[PhysicsFixture.Entity(0)];
        Assert.Equal(firstEntity.Index, secondEntity.Index); Assert.True(first.Physics.TryGetBody(firstEntity, out var firstBody)); Assert.True(second.Physics.TryGetBody(secondEntity, out var secondBody));
        Assert.NotEqual(first.Physics.PhysicsWorldId, second.Physics.PhysicsWorldId); Assert.Equal(firstBody.Slot, secondBody.Slot);
        Assert.False(first.Physics.TryGetBody(secondEntity, out _)); Assert.False(first.Physics.TryGetEntity(secondBody, out _));
        Assert.False(second.Physics.TryGetBody(firstEntity, out _)); Assert.False(second.Physics.TryGetEntity(firstBody, out _));
        var diagnostic = first.Physics.PhysicsWorldId; first.Dispose();
        Assert.True(first.Physics.IsDisposed); Assert.Equal(diagnostic, first.Physics.PhysicsWorldId);
        Assert.False(first.Physics.TryGetBody(firstEntity, out _)); Assert.False(first.Physics.TryGetEntity(firstBody, out _));
        second.Tick(); Assert.Equal(1UL, second.Physics.LastCompletedTick);
    }

    [Theory]
    [InlineData(0UL, false)] [InlineData(1UL, true)]
    public async Task Teleport_RejectsZeroOrChangedReplayWithoutAdvancingNativeTick(ulong sequence, bool changedReplay)
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero)));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        if (changedReplay) { graph.World.AddComponent(entity, new PhysicsTeleport(1, PhysicsPose.Identity)); graph.Tick(); }
        var before = graph.Physics.LastCompletedTick;
        var request = new PhysicsTeleport(sequence, new(new(2, 0, 0), Quaternion.Identity));
        if (changedReplay) graph.World.SetComponent(entity, request); else graph.World.AddComponent(entity, request);
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.True(graph.World.IsFaulted); Assert.Equal(before, graph.Physics.LastCompletedTick);
    }

    [Fact]
    public async Task DirectPoseEdit_IsRejectedBeforeAnyOtherBodyNativeWriteback()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new(PhysicsFixture.Body(), Vector3.Zero) { Velocity = new(new(6, 0, 0)) }, new(PhysicsFixture.Body(), new(0, 5, 0))));
        var first = graph.Scene.Entities[PhysicsFixture.Entity(0)]; var second = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        var pose = graph.World.GetComponent<LocalTransform>(second); graph.World.SetComponent(second, pose with { Position = new(9, 5, 0) });
        Assert.Throws<InvalidDataException>(() => graph.Tick());
        Assert.Equal(0UL, graph.Physics.LastCompletedTick); Assert.Equal(Vector3.Zero, graph.World.GetComponent<LocalTransform>(first).Position);
    }

    [Fact]
    public async Task DirectBodyRemoval_WithResidualRolesRejectsBeforeStepping()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero)));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)]; graph.World.RemoveComponent<PhysicsBody>(entity);
        Assert.False(graph.Physics.TryGetBody(entity, out _));
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.Equal(0UL, graph.Physics.LastCompletedTick); Assert.True(graph.World.IsFaulted);
    }

    [Theory]
    [InlineData(PhysicsBodyMotion.Static)] [InlineData(PhysicsBodyMotion.Dynamic)] [InlineData(PhysicsBodyMotion.Kinematic)]
    public async Task UnitScaleAndRootParent_AreRequiredForEveryMode(PhysicsBodyMotion motion)
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync(); var host = PhysicsFixture.Host(app);
        using var scene = PhysicsFixture.Loader(app).Load(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(motion), Vector3.Zero) { Scale = new(2, 1, 1) }));
        Assert.Throws<NotSupportedException>(() => PhysicsFixture.Subsystems(app).Create(new(scene, PhysicsFixture.Step)));
        Assert.Equal(0, host.Resources.ActiveResourceOwners); Assert.False(scene.World.IsDisposed);
        var parented = PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero), new(PhysicsFixture.Body(motion), new(0, 5, 0)) { ParentIndex = 0 });
        Assert.Throws<TrueMoon.Aether.Runtime.Ecs.Loading.SceneWorldLoadException>(() => PhysicsFixture.Loader(app).Load(parented));
        Assert.Equal(0, host.Resources.ActiveResourceOwners);
    }

    [Fact]
    public async Task IncompatibleFixedStep_IsRejectedBeforeNativeWorldCreation()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var scene = PhysicsFixture.Loader(app).Load(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero)));
        Assert.Throws<NotSupportedException>(() => PhysicsFixture.Subsystems(app).Create(new(scene, TimeSpan.FromSeconds(1d / 30))));
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }
}
