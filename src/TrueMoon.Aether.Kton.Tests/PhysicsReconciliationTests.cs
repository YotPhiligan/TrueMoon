using System.Numerics;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using Xunit;
using A = TrueMoon.Aether.Ecs;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using EntityHandle = TrueMoon.Aether.Ecs.EntityHandle;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsReconciliationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GameplayHelpers_SpawnDetachReattachDeleteAndNeverReviveStaleGenerations(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft); await app.StartAsync();
        EntityHandle spawned = default;
        var description = PhysicsEntityCommands.DeclareWrites(new EcsSystemDescription("test.lifecycle")).Write<LinearVelocity>();
        var actions = new EcsSystemRegistration(description, new Factory<ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            switch (context.TickNumber)
            {
                case 1:
                case 5:
                    var token = PhysicsEntityCommands.CreateBody(context.Commands, PhysicsFixture.Body(), LocalTransform.Identity, new(new(6, 0, 0)));
                    context.Commands.AddComponent(token, new LinearVelocity(new(12, 0, 0))); break;
                case 2: PhysicsEntityCommands.RemoveBody(context.Commands, spawned); break;
                case 3: PhysicsEntityCommands.AddBody(context.Commands, spawned, PhysicsFixture.Body(), new(new(6, 0, 0))); break;
                case 4: context.Commands.DestroyEntity(spawned); break;
            }
        })));
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(PhysicsBodyMotion.Static), new(50, 0, 0))), actions);
        graph.Tick();
        graph.World.Query<PhysicsBody>().ForEach((EntityHandle entity, in PhysicsBody body) => { if (body.Motion == PhysicsBodyMotion.Dynamic) spawned = entity; });
        Assert.True(graph.Physics.TryGetBody(spawned, out var firstBody));
        Assert.InRange(graph.World.GetComponent<LocalTransform>(spawned).Position.X, .0999f, .1001f);
        graph.Tick(); Assert.False(graph.Physics.TryGetBody(spawned, out _)); Assert.False(graph.Physics.TryGetEntity(firstBody, out _));
        Assert.False(graph.World.HasComponent<ExternalMotion>(spawned));
        Assert.InRange(graph.World.GetComponent<LocalTransform>(spawned).Position.X, .2999f, .3001f);
        graph.Tick(); Assert.True(graph.Physics.TryGetBody(spawned, out var secondBody)); Assert.NotEqual(firstBody, secondBody);
        Assert.False(graph.Physics.TryGetEntity(firstBody, out _)); Assert.True(graph.Physics.TryGetEntity(secondBody, out var secondEntity)); Assert.Equal(spawned, secondEntity);
        Assert.InRange(graph.World.GetComponent<LocalTransform>(spawned).Position.X, .3999f, .4001f);
        var retiredEntity = spawned; graph.Tick(); Assert.False(graph.World.IsAlive(retiredEntity));
        Assert.False(graph.Physics.TryGetBody(retiredEntity, out _)); Assert.False(graph.Physics.TryGetEntity(secondBody, out _));
        graph.Tick();
        graph.World.Query<PhysicsBody>().ForEach((EntityHandle entity, in PhysicsBody body) => { if (body.Motion == PhysicsBodyMotion.Dynamic) spawned = entity; });
        Assert.Equal(retiredEntity.Index, spawned.Index); Assert.NotEqual(retiredEntity.Generation, spawned.Generation);
        Assert.True(graph.Physics.TryGetBody(spawned, out var replacement)); Assert.NotEqual(secondBody, replacement);
        Assert.False(graph.Physics.TryGetBody(retiredEntity, out _)); Assert.False(graph.Physics.TryGetEntity(secondBody, out _));
        Assert.False(graph.Physics.TryGetBody(default, out _)); Assert.False(graph.Physics.TryGetEntity(default, out _));
    }

    [Fact]
    public async Task DescriptorChange_RecreatesNativeIdentityWithoutChangingEcsIdentity()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero)));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)]; Assert.True(graph.Physics.TryGetBody(entity, out var oldBody));
        graph.World.AddComponent(entity, new PhysicsTeleport(4, new(new(3, 0, 0), Quaternion.Identity))); graph.Tick();
        graph.World.SetComponent(entity, PhysicsFixture.Body() with { Shape = PhysicsColliderShape.Sphere, Size = new(.5f, 0, 0), Mass = 2 }); graph.Tick();
        Assert.True(graph.World.IsAlive(entity)); Assert.True(graph.Physics.TryGetBody(entity, out var current)); Assert.NotEqual(oldBody, current);
        Assert.False(graph.Physics.TryGetEntity(oldBody, out _)); Assert.True(graph.Physics.TryGetEntity(current, out var mapped)); Assert.Equal(entity, mapped);
        Assert.Equal(new Vector3(3, 0, 0), graph.World.GetComponent<LocalTransform>(entity).Position);
        graph.World.SetComponent(entity, new PhysicsTeleport(3, PhysicsPose.Identity));
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.Equal(2UL, graph.Physics.LastCompletedTick);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task MissingRuntimeRoles_AreRejectedBeforeNativeMutation(int role)
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero)));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        switch (role)
        {
            case 0: graph.World.RemoveComponent<PhysicsVelocity>(entity); break;
            case 1: graph.World.RemoveComponent<ExternalMotion>(entity); break;
            case 2: graph.World.RemoveComponent<TransformParent>(entity); break;
            case 3: graph.World.RemoveComponent<WorldTransform>(entity); break;
        }
        Assert.Throws<InvalidDataException>(() => graph.Tick()); Assert.Equal(0UL, graph.Physics.LastCompletedTick); Assert.True(graph.World.IsFaulted);
    }

    [Theory]
    [InlineData(float.NaN)] [InlineData(61f)]
    public async Task InvalidChangedVelocity_IsRejectedWithoutAdvancingAnotherBody(float speed)
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(new(PhysicsFixture.Body(), Vector3.Zero), new(PhysicsFixture.Body(), new(0, 5, 0))));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(1)]; graph.World.SetComponent(entity, new PhysicsVelocity(new(speed, 0, 0)));
        var failure = Assert.Throws<PhysicsException>(() => graph.Tick()); Assert.Equal("Body.LinearVelocity", failure.Error.Field);
        Assert.Equal(0UL, graph.Physics.LastCompletedTick); Assert.Equal(Vector3.Zero, graph.World.GetComponent<LocalTransform>(graph.Scene.Entities[PhysicsFixture.Entity(0)]).Position);
    }

    [Fact]
    public async Task FailedCandidatePreparation_LeavesOldSessionRunningAndReplacementIndependent()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync(); var host = PhysicsFixture.Host(app);
        using var old = PhysicsFixture.Game(app); using var bad = PhysicsFixture.Game(app); using var replacement = PhysicsFixture.Game(app);
        old.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Velocity = new(new(6, 0, 0)) })));
        old.Tick(); var oldWorld = old.ActiveScene!.World.Id;
        Assert.Throws<NotSupportedException>(() => bad.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Scale = new(2) }))));
        Assert.Null(bad.ActiveScene); Assert.Equal(1, host.Resources.PublishedWorlds); Assert.False(old.IsFaulted);
        old.Tick(); Assert.InRange(PhysicsFixture.Position(old, 0).X, .1999f, .2001f);
        replacement.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero))));
        Assert.NotEqual(oldWorld, replacement.ActiveScene!.World.Id); Assert.Equal(2, host.Resources.PublishedWorlds);
        old.Dispose(); Assert.Equal(1, host.Resources.PublishedWorlds); replacement.Tick(); Assert.Equal(1UL, replacement.TickNumber);
    }
}
