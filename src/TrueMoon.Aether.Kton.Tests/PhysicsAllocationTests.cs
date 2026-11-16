using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using TrueMoon.Kton.Shapes;
using Xunit;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsAllocationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WarmedTick_ReusesScratchWithoutPerBodyAllocations(bool microsoft)
    {
        const int bodyCount = 128, warmupTicks = 128, measuredTicks = 32;
        await using var app = PhysicsFixture.Build(microsoft);
        await app.StartAsync();
        var bodies = Enumerable.Range(0, bodyCount).Select(index =>
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), new(index * 3, 0, 0)) { Velocity = new(new(6, 0, 0)) }).ToArray();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(bodies));
        for (var tick = 0; tick < warmupTicks; tick++) graph.Tick();

        // Keep the byte window synchronous and assertion-free. This is a generous allocation guard, not a timing SLA.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++) graph.Tick();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 0L, measuredTicks * 8192L);
        Assert.Equal((ulong)(warmupTicks + measuredTicks), graph.Physics.LastCompletedTick);
        Assert.Equal(graph.Physics.LastCompletedTick, graph.Scheduler.TickNumber);
        foreach (var index in new[] { 0, bodyCount - 1 })
        {
            var entity = graph.Scene.Entities[PhysicsFixture.Entity(index)];
            var transform = graph.World.GetComponent<LocalTransform>(entity);
            var expectedX = index * 3 + (warmupTicks + measuredTicks) * .1f;
            Assert.InRange(MathF.Abs(transform.Position.X - expectedX), 0, .01f);
            Assert.Equal(new Vector3(6, 0, 0), graph.World.GetComponent<PhysicsVelocity>(entity).Linear);
            var cache = graph.World.GetComponent<WorldTransform>(entity);
            Assert.Equal(graph.Physics.LastCompletedTick, cache.Tick);
            Assert.InRange(Vector3.Distance(cache.Matrix.Translation, transform.Position), 0, .0001f);
        }
        graph.Dispose();
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.PublishedWorlds);
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CandidateReuse_ClearsCharacterRolesAcrossDeletionAndGenerationReuse(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft);
        await app.StartAsync();
        A.EntityHandle original = default, replacement = default;
        var description = PhysicsEntityCommands.DeclareWrites(new A.EcsSystemDescription("test.scratch.reuse"));
        var actions = new EcsSystemRegistration(description, new Factory<A.ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            if (context.TickNumber == 1)
                PhysicsEntityCommands.AddCharacter(context.Commands, original, new PhysicsCharacter { MaximumSpeed = 1.5f }, new(new(1, 0), 4));
            if (context.TickNumber == 2) context.Commands.DestroyEntity(original);
            if (context.TickNumber == 3)
            {
                var token = PhysicsEntityCommands.CreateBody(context.Commands, Capsule(), LocalTransform.Identity);
                PhysicsEntityCommands.AddCharacter(context.Commands, token, new PhysicsCharacter { MaximumSpeed = 1.5f }, new(new(1.5f, 0)));
            }
            if (context.TickNumber == 4) PhysicsEntityCommands.RemoveCharacter(context.Commands, replacement);
            if (context.TickNumber == 5)
                PhysicsEntityCommands.AddCharacter(context.Commands, replacement, new PhysicsCharacter { MaximumSpeed = 3 }, new(new(2, 0)));
        })));
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new(Capsule(), Vector3.Zero),
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), new(10, 0, 0)) { Velocity = new(new(6, 0, 0)) }), actions);
        original = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        var regular = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        Assert.True(graph.Physics.TryGetBody(original, out var originalBody));
        Assert.True(graph.Physics.TryGetBody(regular, out var regularBody));
        graph.Tick();
        Assert.True(graph.World.HasComponent<PhysicsCharacter>(original));
        graph.Tick();
        Assert.False(graph.World.IsAlive(original));
        Assert.False(graph.Physics.TryGetEntity(originalBody, out _));
        Assert.False(graph.World.HasComponent<PhysicsCharacter>(regular));
        Assert.True(graph.Physics.TryGetBody(regular, out var unchangedRegular));
        Assert.Equal(regularBody, unchangedRegular);
        Assert.InRange(graph.World.GetComponent<LocalTransform>(regular).Position.X, 10.199f, 10.201f);

        graph.Tick();
        graph.World.Query<PhysicsCharacter>().ForEach((A.EntityHandle entity, in PhysicsCharacter _) => replacement = entity);
        Assert.Equal(original.Index, replacement.Index);
        Assert.NotEqual(original.Generation, replacement.Generation);
        Assert.False(graph.Physics.TryGetBody(original, out _));
        Assert.True(graph.Physics.TryGetBody(replacement, out var replacementBody));
        Assert.NotEqual(originalBody, replacementBody);
        Assert.False(graph.Physics.TryGetEntity(originalBody, out _));
        graph.Tick();
        Assert.False(graph.World.HasComponent<PhysicsCharacter>(replacement));
        Assert.False(graph.World.HasComponent<PhysicsCharacterInput>(replacement));
        Assert.False(graph.World.HasComponent<PhysicsCharacterState>(replacement));
        Assert.True(graph.Physics.TryGetBody(replacement, out var detachedBody));
        Assert.Equal(replacementBody, detachedBody);
        graph.Tick();
        Assert.Equal(5UL, graph.Physics.LastCompletedTick);
        Assert.False(graph.World.IsFaulted);
        Assert.True(graph.Physics.TryGetBody(replacement, out var reattachedBody));
        Assert.Equal(replacementBody, reattachedBody);
        Assert.InRange(graph.World.GetComponent<LocalTransform>(regular).Position.X, 10.499f, 10.501f);
        graph.Dispose();
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CachedCharacterOptions_ChangedLimitIsValidatedBeforeAnyWriteback(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft);
        await app.StartAsync();
        A.EntityHandle character = default;
        var description = PhysicsEntityCommands.DeclareWrites(new A.EcsSystemDescription("test.cached.character"));
        var actions = new EcsSystemRegistration(description, new Factory<A.ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            if (context.TickNumber == 1)
                PhysicsEntityCommands.AddCharacter(context.Commands, character, new PhysicsCharacter { MaximumSpeed = 2 }, new(new(1.5f, 0)));
        })));
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Velocity = new(new(6, 0, 0)) },
            new(Capsule(), new(20, 0, 0))), actions);
        var neighbor = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        character = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        graph.Tick(); graph.Tick();
        var neighborBefore = graph.World.GetComponent<LocalTransform>(neighbor);
        var characterBefore = graph.World.GetComponent<LocalTransform>(character);
        Assert.True(graph.Physics.TryGetBody(character, out var oldBody));
        graph.World.SetComponent(character, new PhysicsCharacter { MaximumSpeed = 1 });

        var error = Assert.Throws<PhysicsException>(() => graph.Tick());

        Assert.Equal(new PhysicsError(PhysicsErrorCode.InvalidArgument, "Character.Input"), error.Error);
        Assert.Equal(2UL, graph.Physics.LastCompletedTick);
        Assert.Equal(neighborBefore, graph.World.GetComponent<LocalTransform>(neighbor));
        Assert.Equal(characterBefore, graph.World.GetComponent<LocalTransform>(character));
        Assert.True(graph.Physics.TryGetBody(character, out var currentBody));
        Assert.Equal(oldBody, currentBody);
        Assert.True(graph.World.IsFaulted);
    }

    [Fact]
    public async Task CachedBodySettings_ChangedDescriptorFailsBeforeNativeReplacement()
    {
        await using var app = PhysicsFixture.Build(false);
        await app.StartAsync();
        var moving = new PhysicsVelocity(new(6, 0, 0));
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Velocity = moving },
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), new(10, 0, 0)) { Velocity = moving }));
        var first = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        var changed = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        graph.Tick(); graph.Tick();
        var before = graph.World.GetComponent<LocalTransform>(first);
        Assert.True(graph.Physics.TryGetBody(changed, out var oldBody));
        graph.World.SetComponent(changed, PhysicsFixture.Body(PhysicsBodyMotion.Static));

        var error = Assert.Throws<PhysicsException>(() => graph.Tick());

        Assert.Equal(new PhysicsError(PhysicsErrorCode.InvalidArgument, "Body.StaticVelocity"), error.Error);
        Assert.Equal(2UL, graph.Physics.LastCompletedTick);
        Assert.Equal(before, graph.World.GetComponent<LocalTransform>(first));
        Assert.True(graph.Physics.TryGetBody(changed, out var currentBody));
        Assert.Equal(oldBody, currentBody);
        Assert.True(graph.Physics.TryGetEntity(oldBody, out var mapped));
        Assert.Equal(changed, mapped);
        Assert.True(graph.World.IsFaulted);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(100.00001f, true)]
    public async Task CachedMassEnvelope_UsesInclusiveRatioBeforeReplacement(float mass, bool rejected)
    {
        await using var app = PhysicsFixture.Build(false);
        await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero) { Velocity = new(new(6, 0, 0)) },
            new(PhysicsFixture.Body(), new(10, 0, 0))));
        var first = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        var changed = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        graph.Tick(); graph.Tick();
        var before = graph.World.GetComponent<LocalTransform>(first);
        Assert.True(graph.Physics.TryGetBody(changed, out var oldBody));
        graph.World.SetComponent(changed, PhysicsFixture.Body() with { Mass = mass });

        if (rejected)
        {
            var error = Assert.Throws<PhysicsException>(() => graph.Tick());
            Assert.Equal(new PhysicsError(PhysicsErrorCode.InvalidArgument, "Body.MassRatio"), error.Error);
            Assert.Equal(2UL, graph.Physics.LastCompletedTick);
            Assert.Equal(before, graph.World.GetComponent<LocalTransform>(first));
            Assert.True(graph.Physics.TryGetBody(changed, out var currentBody));
            Assert.Equal(oldBody, currentBody);
            Assert.True(graph.World.IsFaulted);
        }
        else
        {
            graph.Tick();
            Assert.Equal(3UL, graph.Physics.LastCompletedTick);
            Assert.True(graph.Physics.TryGetBody(changed, out var currentBody));
            Assert.NotEqual(oldBody, currentBody);
            Assert.False(graph.Physics.TryGetEntity(oldBody, out _));
            Assert.InRange(graph.World.GetComponent<LocalTransform>(first).Position.X, .2999f, .3001f);
            Assert.False(graph.World.IsFaulted);
        }
    }

    [Fact]
    public void BodyValidation_UsesSuppliedMotionAndPreservesLegacySnapshotValidation()
    {
        var options = new PhysicsWorldOptions();
        var shape = new PhysicsBox(Vector3.One);
        var body = Description() with
        {
            Pose = new(new(float.NaN, 0, 0), Quaternion.Identity),
            LinearVelocity = new(float.NaN, 0, 0), AngularVelocity = new(0, float.NaN, 0)
        };
        var supplied = new PhysicsPose(new(2, 3, 4), Quaternion.Identity);

        var result = PhysicsValidation.ValidateBody(options, body, shape, supplied, new(4, 0, 0), new(0, .5f, 0));

        Assert.Equal(default, result);
        Assert.Equal(new PhysicsError(PhysicsErrorCode.InvalidArgument, "Pose.Position"), PhysicsValidation.ValidateBody(options, body, shape));
        Assert.True(float.IsNaN(body.LinearVelocity.X));
        Assert.True(float.IsNaN(body.AngularVelocity.Y));
    }

    [Theory]
    [InlineData(0, "Pose.Position")]
    [InlineData(1, "Pose.Orientation")]
    [InlineData(2, "Body.LinearVelocity")]
    [InlineData(3, "Body.AngularVelocity")]
    [InlineData(4, "Body.StaticVelocity")]
    [InlineData(5, "Body.StaticVelocity")]
    public void BodyValidation_SuppliedMotionIsValidated(int invalid, string field)
    {
        var body = invalid is 4 or 5 ? Description() with { Motion = PhysicsBodyMotion.Static, Mass = 0 } : Description();
        var pose = invalid switch
        {
            0 => new PhysicsPose(new(float.NaN, 0, 0), Quaternion.Identity),
            1 => new PhysicsPose(Vector3.Zero, default),
            _ => PhysicsPose.Identity
        };
        var linear = invalid switch { 2 => new Vector3(60, 1, 0), 4 => Vector3.UnitX, _ => Vector3.Zero };
        var angular = invalid switch { 3 => new Vector3(0, float.NaN, 0), 5 => Vector3.UnitY, _ => Vector3.Zero };

        var result = PhysicsValidation.ValidateBody(new(), body, new PhysicsBox(Vector3.One), pose, linear, angular);

        Assert.Equal(new PhysicsError(PhysicsErrorCode.InvalidArgument, field), result);
    }

    [Theory]
    [InlineData(PhysicsQualityProfile.Baseline60, 60f)]
    [InlineData(PhysicsQualityProfile.FastLinear60, 600f)]
    public void BodyValidation_MotionCeilingsRemainInclusive(PhysicsQualityProfile profile, float maximumSpeed)
    {
        var options = new PhysicsWorldOptions { Quality = profile };
        var body = Description(); var shape = new PhysicsBox(Vector3.One);
        var pose = new PhysicsPose(new(10000, 0, 0), Quaternion.Identity);
        Assert.Equal(default, PhysicsValidation.ValidateBody(options, body, shape, pose, new(maximumSpeed, 0, 0), new(0, 64, 0)));
        Assert.Equal("Pose.Position", PhysicsValidation.ValidateBody(options, body, shape,
            pose with { Position = new(MathF.BitIncrement(10000), 0, 0) }, Vector3.Zero, Vector3.Zero).Field);
        Assert.Equal("Body.LinearVelocity", PhysicsValidation.ValidateBody(options, body, shape,
            pose, new(MathF.BitIncrement(maximumSpeed), 0, 0), Vector3.Zero).Field);
        Assert.Equal("Body.AngularVelocity", PhysicsValidation.ValidateBody(options, body, shape,
            pose, Vector3.Zero, new(0, MathF.BitIncrement(64), 0)).Field);
    }

    [Theory]
    [InlineData(0, PhysicsErrorCode.InvalidIdentifier, "Body.Shape")]
    [InlineData(1, PhysicsErrorCode.InvalidArgument, "Body.Motion")]
    [InlineData(2, PhysicsErrorCode.InvalidArgument, "Body.Mass")]
    [InlineData(3, PhysicsErrorCode.InvalidArgument, "Body.Material.Friction")]
    [InlineData(4, PhysicsErrorCode.InvalidArgument, "Body.Filter.Layer")]
    [InlineData(5, PhysicsErrorCode.InvalidArgument, "Shape.Size")]
    [InlineData(6, PhysicsErrorCode.UnsupportedGeometry, "Body.Shape")]
    [InlineData(7, PhysicsErrorCode.UnsupportedProfile, "Quality")]
    [InlineData(8, PhysicsErrorCode.InvalidArgument, "Gravity")]
    [InlineData(9, PhysicsErrorCode.InvalidArgument, "Limits.MaxBodies")]
    [InlineData(10, PhysicsErrorCode.InvalidArgument, "Body")]
    [InlineData(11, PhysicsErrorCode.InvalidArgument, "Shape")]
    [InlineData(12, PhysicsErrorCode.InvalidArgument, "Body.Mass")]
    public void BodyValidation_ImmutableSettingsStillValidated(int invalid, PhysicsErrorCode code, string field)
    {
        PhysicsBodyDescription? body = Description();
        PhysicsShapeDefinition? shape = new PhysicsBox(Vector3.One);
        var options = new PhysicsWorldOptions();
        switch (invalid)
        {
            case 0: body = body with { Shape = default }; break;
            case 1: body = body with { Motion = (PhysicsBodyMotion)99 }; break;
            case 2: body = body with { Mass = 0 }; break;
            case 3: body = body with { Material = new(float.NaN) }; break;
            case 4: body = body with { Filter = new(32, uint.MaxValue) }; break;
            case 5: shape = new PhysicsBox(Vector3.Zero); break;
            case 6: shape = new PhysicsStaticMesh(new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(0, 0, 1) }, new[] { 0, 1, 2 }); break;
            case 7: options = options with { Quality = (PhysicsQualityProfile)99 }; break;
            case 8: options = options with { Gravity = new(float.NaN, 0, 0) }; break;
            case 9: options = options with { Limits = new() { MaxBodies = 0 } }; break;
            case 10: body = null; break;
            case 11: shape = null; break;
            case 12: body = body with { Motion = PhysicsBodyMotion.Static }; break;
        }

        var result = PhysicsValidation.ValidateBody(options, body, shape, PhysicsPose.Identity, Vector3.Zero, Vector3.Zero);

        Assert.Equal(new PhysicsError(code, field), result);
    }

    private static PhysicsBody Capsule() => new(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Capsule, new(.5f, 1, 0), 1);
    private static PhysicsBodyDescription Description() => new(new PhysicsShapeId(new(Guid.NewGuid()), 0, 1), PhysicsBodyMotion.Dynamic, PhysicsPose.Identity);
}
