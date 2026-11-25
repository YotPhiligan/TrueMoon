using System.Numerics;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using Xunit;
using A = TrueMoon.Aether.Ecs;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using EntityHandle = TrueMoon.Aether.Ecs.EntityHandle;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsContactTests
{
    private static PhysicsFixture.BodySpec Floor => new(PhysicsFixture.Body(PhysicsBodyMotion.Static) with { Size = new(10, 1, 10) }, new(0, -.5f, 0));
    private static PhysicsFixture.BodySpec Sensor(float x = 0) => new(PhysicsFixture.Body() with { IsSensor = true }, new(x, .4f, 0));

    [Fact]
    public async Task CollisionMaskZero_DisablesContactProjectionWithoutStoppingPhysics()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        var sensor = Sensor() with { Body = Sensor().Body with { Filter = new(0, 0) } };
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, sensor));
        for (var tick = 0; tick < 3; tick++) { graph.Tick(); Assert.Empty(Events(graph)); }
        Assert.Equal(3UL, graph.Physics.LastCompletedTick); Assert.Equal(2, graph.World.EntityCount); Assert.False(graph.World.IsFaulted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Contacts_AreVisibleToGameplayOnNextTickAndPreviousBatchIsRetired(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft); await app.StartAsync();
        var seen = new List<(ulong Tick, List<PhysicsContact> Events)>();
        var observer = new EcsSystemRegistration(new EcsSystemDescription("test.contacts").Read<PhysicsContact>(),
            new Factory<ISystem>(() => new PhysicsGraph.Callback(context =>
            {
                var events = new List<PhysicsContact>(); context.Query<PhysicsContact>().ForEach((EntityHandle _, in PhysicsContact value) => events.Add(value));
                seen.Add((context.TickNumber, events));
            })));
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, Sensor()), observer);
        graph.Tick(); Assert.Empty(seen[0].Events);
        var first = Events(graph); var begin = Assert.Single(first); Assert.Equal(PhysicsContactKind.Begin, begin.Value.Contact.Kind); Assert.Equal(1UL, begin.Value.Contact.Tick);
        var sourceA = graph.Scene.Entities[PhysicsFixture.Entity(0)]; var sourceB = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        Assert.True(begin.Value.EntityA == sourceA && begin.Value.EntityB == sourceB || begin.Value.EntityA == sourceB && begin.Value.EntityB == sourceA);
        Assert.True(begin.Value.Contact.IsSensor); Assert.True(begin.Value.Contact.HasContact);
        graph.Tick();
        Assert.Equal((2UL, 1UL), (seen[1].Tick, Assert.Single(seen[1].Events).Contact.Tick));
        Assert.Equal(PhysicsContactKind.Begin, seen[1].Events[0].Contact.Kind); Assert.False(graph.World.IsAlive(begin.Entity));
        var stay = Assert.Single(Events(graph)); Assert.Equal(PhysicsContactKind.Stay, stay.Value.Contact.Kind); Assert.Equal(2UL, stay.Value.Contact.Tick); Assert.NotEqual(begin.Entity, stay.Entity);
        graph.World.AddComponent(sourceB, new PhysicsTeleport(1, new(new(0, 10, 0), Quaternion.Identity))); graph.Tick();
        var ended = Assert.Single(Events(graph)).Value; Assert.Equal(PhysicsContactKind.End, ended.Contact.Kind); Assert.Equal(PhysicsContactEndReason.Teleported, ended.Contact.EndReason);
        graph.Tick(); Assert.Equal(3UL, Assert.Single(seen[3].Events).Contact.Tick); Assert.Empty(Events(graph));
    }

    [Fact]
    public async Task RemovedBodyEnd_UsesOriginalGenerationsEvenWhenEntitySlotIsReused()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, Sensor()));
        graph.Tick(); var retired = graph.Scene.Entities[PhysicsFixture.Entity(1)]; Assert.True(graph.Physics.TryGetBody(retired, out var retiredBody));
        graph.World.DestroyEntity(retired); var replacement = graph.World.CreateEntity();
        graph.World.AddComponent(replacement, PhysicsFixture.Body()); graph.World.AddComponent(replacement, new PhysicsVelocity()); graph.World.AddComponent(replacement, new ExternalMotion());
        graph.World.AddComponent(replacement, LocalTransform.Identity with { Position = new(20, 5, 0) }); graph.World.AddComponent(replacement, default(TransformParent)); graph.World.AddComponent(replacement, default(WorldTransform));
        Assert.Equal(retired.Index, replacement.Index); Assert.NotEqual(retired.Generation, replacement.Generation);
        graph.Tick(); var ended = Assert.Single(Events(graph)).Value;
        Assert.Equal(PhysicsContactKind.End, ended.Contact.Kind); Assert.Equal(PhysicsContactEndReason.BodyRemoved, ended.Contact.EndReason);
        Assert.True(ended.EntityA == retired || ended.EntityB == retired); Assert.False(ended.EntityA == replacement || ended.EntityB == replacement);
        Assert.True(ended.Contact.BodyA == retiredBody || ended.Contact.BodyB == retiredBody);
        Assert.False(graph.Physics.TryGetEntity(retiredBody, out _)); Assert.False(graph.Physics.TryGetBody(retired, out _));
        Assert.True(graph.Physics.TryGetBody(replacement, out var newBody)); Assert.NotEqual(retiredBody, newBody);
    }

    [Fact]
    public async Task ProjectionAtCapacity_PublishesCompleteEventAndRetiresPreviousBatch()
    {
        await using var app = PhysicsFixture.Build(false, bridge: options => options.MaximumContactEvents = 1); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, Sensor()));
        graph.Tick(); var first = Assert.Single(Events(graph));
        Assert.Equal(PhysicsContactKind.Begin, first.Value.Contact.Kind); Assert.Equal(1UL, first.Value.Contact.Tick);
        graph.Tick(); var second = Assert.Single(Events(graph));
        Assert.Equal(PhysicsContactKind.Stay, second.Value.Contact.Kind); Assert.Equal(2UL, second.Value.Contact.Tick);
        Assert.False(graph.World.IsAlive(first.Entity)); Assert.Equal(3, graph.World.EntityCount); Assert.False(graph.World.IsFaulted);
    }

    [Fact]
    public async Task ProjectionOverflow_IsExplicitAndDoesNotPublishPartialContactEntities()
    {
        await using var app = PhysicsFixture.Build(false, bridge: options => options.MaximumContactEvents = 1); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, Sensor(-2), Sensor(2)));
        var failure = Assert.Throws<PhysicsException>(() => graph.Tick());
        Assert.Equal(PhysicsErrorCode.ContactOverflow, failure.Error.Code); Assert.Equal("Bridge.ContactProjection", failure.Error.Field);
        Assert.Equal(1UL, graph.Physics.LastCompletedTick); Assert.Equal(0UL, graph.Scheduler.TickNumber); Assert.True(graph.World.IsFaulted);
        Assert.Equal(3, graph.World.EntityCount);
    }

    [Fact]
    public async Task EcsCommandCapacityFailure_DoesNotPretendToPublishContactBatch()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(Floor, Sensor()), new EcsSchedulerOptions { MaximumCommandsPerPhase = 1 });
        Assert.Throws<InvalidOperationException>(() => graph.Tick());
        Assert.True(graph.World.IsFaulted); Assert.Equal(1UL, graph.Physics.LastCompletedTick); Assert.Equal(0UL, graph.Scheduler.TickNumber); Assert.Equal(2, graph.World.EntityCount);
    }

    private static List<(EntityHandle Entity, PhysicsContact Value)> Events(PhysicsGraph graph)
    {
        var result = new List<(EntityHandle Entity, PhysicsContact Value)>();
        graph.World.Query<PhysicsContact>().ForEach((EntityHandle entity, in PhysicsContact value) => result.Add((entity, value)));
        return result;
    }
}
