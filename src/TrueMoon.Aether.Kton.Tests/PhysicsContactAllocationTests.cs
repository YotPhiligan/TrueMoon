using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Contacts;
using Xunit;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsContactAllocationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WarmedContactProjection_BoundsAllocationsAndPreservesEachPair(bool microsoft)
    {
        const int sensorCount = 32, warmupTicks = 64, measuredTicks = 32;
        await using var app = PhysicsFixture.Build(microsoft);
        await app.StartAsync();
        var bodies = new PhysicsFixture.BodySpec[sensorCount + 1];
        bodies[0] = new(PhysicsFixture.Body(PhysicsBodyMotion.Static) with { Size = new(256, 1, 256) }, new(0, -.5f, 0));
        for (var index = 1; index <= sensorCount; index++)
            bodies[index] = new(PhysicsFixture.Body() with { Shape = PhysicsColliderShape.Sphere, Size = new(.5f, 0, 0), IsSensor = true },
                new(index * 3 - 48, .25f, 0)) { Velocity = new(Vector3.UnitX) };
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(bodies));
        for (var tick = 0; tick < warmupTicks; tick++) graph.Tick();
        var previous = Events(graph);
        Assert.Equal(sensorCount, previous.Count);

        // Measure only warmed synchronous ticks, keeping query/assertion allocations outside the window.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++) graph.Tick();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 0L, measuredTicks * 16384L);
        var tickNumber = (ulong)(warmupTicks + measuredTicks);
        Assert.Equal(tickNumber, graph.Physics.LastCompletedTick);
        Assert.Equal(tickNumber, graph.Scheduler.TickNumber);
        Assert.Equal(sensorCount * 2 + 1, graph.World.EntityCount);
        Assert.False(graph.World.IsFaulted);
        foreach (var old in previous) Assert.False(graph.World.IsAlive(old.Entity));
        var current = Events(graph);
        Assert.Equal(sensorCount, current.Count);
        var floor = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        var sensors = new HashSet<EntityHandle>();
        foreach (var item in current)
        {
            Assert.True(graph.World.IsAlive(item.Entity));
            var contact = item.Value;
            Assert.Equal(tickNumber, contact.Contact.Tick);
            Assert.Equal(PhysicsContactKind.Stay, contact.Contact.Kind);
            Assert.Equal(PhysicsContactEndReason.None, contact.Contact.EndReason);
            Assert.True(contact.Contact.IsSensor); Assert.True(contact.Contact.HasContact);
            Assert.True(contact.EntityA == floor || contact.EntityB == floor);
            var sensor = contact.EntityA == floor ? contact.EntityB : contact.EntityA;
            Assert.True(sensors.Add(sensor));
            Assert.True(graph.World.IsAlive(sensor));
            Assert.True(graph.Physics.TryGetBody(contact.EntityA, out var bodyA));
            Assert.True(graph.Physics.TryGetBody(contact.EntityB, out var bodyB));
            Assert.Equal(bodyA, contact.Contact.BodyA); Assert.Equal(bodyB, contact.Contact.BodyB);
        }
        for (var index = 1; index <= sensorCount; index++)
            Assert.Contains(graph.Scene.Entities[PhysicsFixture.Entity(index)], sensors);
        graph.Dispose();
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.PublishedWorlds);
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.MaximumPoolBytesAfterCleanup);
    }

    private static List<(EntityHandle Entity, PhysicsContact Value)> Events(PhysicsGraph graph)
    {
        var events = new List<(EntityHandle Entity, PhysicsContact Value)>();
        graph.World.Query<PhysicsContact>().ForEach((EntityHandle entity, in PhysicsContact value) => events.Add((entity, value)));
        return events;
    }
}
