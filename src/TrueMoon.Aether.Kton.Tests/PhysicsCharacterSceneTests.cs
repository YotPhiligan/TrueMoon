using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using Xunit;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsCharacterSceneTests
{
    [Fact]
    public void CharacterSettings_CaptureNeutralOptionsAndRemainReferenceFree()
    {
        var descriptor = new PhysicsCharacter
        {
            MaximumSpeed = 4, MaximumSlopeRadians = .6f, MaximumHorizontalForce = 500,
            MaximumVerticalForce = 700, JumpSpeed = 3
        };
        var options = descriptor.CaptureOptions();
        Assert.Equal((4f, .6f, 500f, 700f, 3f),
            (options.MaximumSpeed, options.MaximumSlopeRadians, options.MaximumHorizontalForce, options.MaximumVerticalForce, options.JumpSpeed));
        Assert.False(options.Validate().IsFailure);
        Assert.True(default(PhysicsCharacter).CaptureOptions().Validate().IsFailure);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsCharacter>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsCharacterInput>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhysicsCharacterState>());
        Assert.Equal(new[] { PhysicsComponentSchemas.BodyId }, PhysicsComponentSchemas.Character.RequiredTypes);
        Assert.Equal(1, PhysicsComponentSchemas.Character.Version);
        Assert.Equal(5, PhysicsComponentSchemas.Character.Properties.Count);
    }

    [Fact]
    public async Task AuthoringSchema_RegistryCapturesEveryExplicitControllerSetting()
    {
        await using var app = PhysicsFixture.Build(false, world: new());
        await app.StartAsync();
        var marker = new PhysicsCharacter
        {
            MaximumSpeed = 3, MaximumSlopeRadians = .7f, MaximumHorizontalForce = 400,
            MaximumVerticalForce = 600, JumpSpeed = 4
        };
        using var graph = PhysicsGraph.Create(app, CharacterDocument(marker: marker));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        Assert.Equal(marker, graph.World.GetComponent<PhysicsCharacter>(entity));
        graph.Tick();
        Assert.True(graph.World.GetComponent<PhysicsCharacterState>(entity).IsSupported);
        Assert.Equal(graph.Scene.Entities[PhysicsFixture.Entity(1)], graph.World.GetComponent<PhysicsCharacterState>(entity).SupportEntity);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AuthoredCharacter_MovesAndPublishesSupportAndRenderPoseInSameTick(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft, world: new());
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(CharacterDocument()));
        var world = game.ActiveScene!.World;
        var character = game.ActiveScene.Entities[PhysicsFixture.Entity(0)];
        var floor = game.ActiveScene.Entities[PhysicsFixture.Entity(1)];
        Assert.True(world.HasComponent<PhysicsCharacterInput>(character));
        Assert.True(world.HasComponent<PhysicsCharacterState>(character));
        for (var tick = 0; tick < 60; tick++) game.Tick();
        var supported = world.GetComponent<PhysicsCharacterState>(character);
        Assert.True(supported.IsSupported);
        Assert.Equal(floor, supported.SupportEntity);
        Assert.InRange(supported.SupportNormal.Y, .999f, 1.001f);
        world.SetComponent(character, new PhysicsCharacterInput(new(2, 0)));
        for (var tick = 0; tick < 60; tick++) game.Tick();
        var transform = world.GetComponent<LocalTransform>(character);
        Assert.InRange(transform.Position.X, 1.8f, 2.1f);
        Assert.InRange(world.GetComponent<PhysicsVelocity>(character).Linear.X, 1.95f, 2.05f);
        Assert.True(world.GetComponent<PhysicsCharacterState>(character).IsSupported);
        Assert.Contains(game.CaptureRenderScene().Primitives,
            primitive => Vector3.Distance(primitive.World.Translation, transform.Position) < .001f);
        game.Dispose();
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task JumpSequence_ReplayDoesNotRepeatJumpAndLaterSequenceJumpsAgain(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft, world: new());
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(CharacterDocument()));
        var world = game.ActiveScene!.World;
        var entity = game.ActiveScene.Entities[PhysicsFixture.Entity(0)];
        for (var tick = 0; tick < 60; tick++) game.Tick();
        world.SetComponent(entity, new PhysicsCharacterInput(Vector2.Zero, 1));
        game.Tick();
        Assert.False(world.GetComponent<PhysicsCharacterState>(entity).IsSupported);
        Assert.True(world.GetComponent<PhysicsVelocity>(entity).Linear.Y > 4.5f);
        for (var tick = 0; tick < 150; tick++) game.Tick();
        Assert.True(world.GetComponent<PhysicsCharacterState>(entity).IsSupported);
        Assert.InRange(world.GetComponent<LocalTransform>(entity).Position.Y, .97f, 1.03f);
        world.SetComponent(entity, new PhysicsCharacterInput(new(1, 0), 1));
        for (var tick = 0; tick < 30; tick++) game.Tick();
        Assert.True(world.GetComponent<PhysicsCharacterState>(entity).IsSupported);
        Assert.InRange(world.GetComponent<LocalTransform>(entity).Position.Y, .97f, 1.03f);
        Assert.True(world.GetComponent<LocalTransform>(entity).Position.X > .35f);
        world.SetComponent(entity, new PhysicsCharacterInput(Vector2.Zero, 2));
        game.Tick();
        Assert.False(world.GetComponent<PhysicsCharacterState>(entity).IsSupported);
        Assert.True(world.GetComponent<PhysicsVelocity>(entity).Linear.Y > 4.5f);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task InvalidCharacterEdit_FailsBeforeNativeTickOrNeighborWriteback(int invalid)
    {
        await using var app = PhysicsFixture.Build(false, world: new());
        await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, CharacterDocument(includeMovingNeighbor: true));
        var entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        var neighbor = graph.Scene.Entities[PhysicsFixture.Entity(2)];
        graph.World.SetComponent(entity, new PhysicsCharacterInput(Vector2.Zero, 2));
        graph.Tick();
        var before = graph.Physics.LastCompletedTick;
        var position = graph.World.GetComponent<LocalTransform>(entity).Position;
        var neighborPosition = graph.World.GetComponent<LocalTransform>(neighbor).Position;
        switch (invalid)
        {
            case 0: graph.World.SetComponent(entity, new PhysicsCharacterInput(Vector2.Zero, 1)); break;
            case 1: graph.World.SetComponent(entity, new PhysicsCharacterInput(new(7, 0), 2)); break;
            case 2: graph.World.SetComponent(entity, new PhysicsCharacterState(true, entity, Vector3.UnitX)); break;
            case 3: graph.World.SetComponent(entity, new PhysicsVelocity(Vector3.Zero, Vector3.UnitY)); break;
            case 4: graph.World.RemoveComponent<PhysicsCharacterInput>(entity); break;
        }
        if (invalid == 1)
            Assert.Equal(PhysicsErrorCode.InvalidArgument, Assert.Throws<PhysicsException>(() => graph.Tick()).Error.Code);
        else
            Assert.Throws<InvalidDataException>(() => graph.Tick());
        Assert.Equal(before, graph.Physics.LastCompletedTick);
        Assert.Equal(position, graph.World.GetComponent<LocalTransform>(entity).Position);
        Assert.Equal(neighborPosition, graph.World.GetComponent<LocalTransform>(neighbor).Position);
        Assert.True(graph.World.IsFaulted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeHelpers_DetachReattachReplaceAndDeleteReleaseControllerOwnership(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft, world: new());
        await app.StartAsync();
        A.EntityHandle entity = default;
        var description = PhysicsEntityCommands.DeclareWrites(new A.EcsSystemDescription("test.character.lifecycle"));
        var actions = new EcsSystemRegistration(description, new Factory<A.ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            if (context.TickNumber == 2) PhysicsEntityCommands.RemoveCharacter(context.Commands, entity);
            if (context.TickNumber == 3) PhysicsEntityCommands.AddCharacter(context.Commands, entity, new PhysicsCharacter());
            if (context.TickNumber == 4) context.Commands.SetComponent(entity, new PhysicsCharacter { MaximumSpeed = 2 });
            if (context.TickNumber == 5) PhysicsEntityCommands.RemoveBody(context.Commands, entity);
            if (context.TickNumber == 6)
            {
                PhysicsEntityCommands.AddBody(context.Commands, entity, CapsuleBody());
                PhysicsEntityCommands.AddCharacter(context.Commands, entity, new PhysicsCharacter());
            }
            if (context.TickNumber == 7) context.Commands.DestroyEntity(entity);
        })));
        using var graph = PhysicsGraph.Create(app, CharacterDocument(), actions);
        entity = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        graph.Tick();
        Assert.True(graph.Physics.TryGetBody(entity, out var originalBody));
        graph.Tick();
        Assert.False(graph.World.HasComponent<PhysicsCharacter>(entity));
        Assert.False(graph.World.HasComponent<PhysicsCharacterInput>(entity));
        Assert.False(graph.World.HasComponent<PhysicsCharacterState>(entity));
        Assert.True(graph.Physics.TryGetBody(entity, out var detachedBody));
        Assert.Equal(originalBody, detachedBody);
        graph.Tick();
        Assert.True(graph.World.HasComponent<PhysicsCharacter>(entity));
        graph.Tick();
        Assert.Equal(2, graph.World.GetComponent<PhysicsCharacter>(entity).MaximumSpeed);
        Assert.True(graph.Physics.TryGetBody(entity, out var sameBody));
        Assert.Equal(originalBody, sameBody);
        graph.Tick();
        Assert.False(graph.Physics.TryGetBody(entity, out _));
        Assert.False(graph.World.HasComponent<PhysicsCharacter>(entity));
        Assert.False(graph.World.HasComponent<PhysicsCharacterInput>(entity));
        Assert.False(graph.World.HasComponent<PhysicsCharacterState>(entity));
        graph.Tick();
        Assert.True(graph.Physics.TryGetBody(entity, out var replacement));
        Assert.NotEqual(originalBody, replacement);
        graph.Tick();
        Assert.False(graph.World.IsAlive(entity));
        Assert.False(graph.Physics.TryGetEntity(replacement, out _));
        graph.Dispose();
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidAuthoredCharacter_ReleasesFailedWorldAndDoesNotPublishSession(bool microsoft)
    {
        await using var app = PhysicsFixture.Build(microsoft, world: new());
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        Assert.Throws<InvalidDataException>(() => game.LoadSceneWorld(new(CharacterDocument(body: PhysicsFixture.Body()))));
        Assert.Null(game.ActiveScene);
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
        Assert.Equal(0, PhysicsFixture.Host(app).Resources.PublishedWorlds);
    }

    private static PhysicsBody CapsuleBody() => new(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Capsule, new(.5f, 1, 0), 1);

    private static SceneDocument CharacterDocument(PhysicsBody? body = null, PhysicsCharacter? marker = null, bool includeMovingNeighbor = false)
    {
        var bodies = new List<PhysicsFixture.BodySpec>
        {
            new PhysicsFixture.BodySpec(body ?? CapsuleBody(), new(0, 1, 0)),
            new PhysicsFixture.BodySpec(new(PhysicsBodyMotion.Static, PhysicsColliderShape.Box, new(40, 1, 20), 0), new(0, -.5f, 0))
        };
        if (includeMovingNeighbor)
            bodies.Add(new(PhysicsFixture.Body(), new(10, 5, 0)) { Velocity = new(new(2, 0, 0)) });
        var source = PhysicsFixture.Document(bodies.ToArray());
        var definitions = source.Definitions.ToArray();
        var first = definitions[0];
        var descriptor = marker ?? new PhysicsCharacter();
        definitions[0] = new(first.Id, new EntityPatch(first.Changes.AddedComponents.Append(
            new ComponentData(new(Guid.NewGuid()), PhysicsComponentSchemas.CharacterId, 1,
            [
                PhysicsFixture.Number("speed", descriptor.MaximumSpeed), PhysicsFixture.Number("slope", descriptor.MaximumSlopeRadians),
                PhysicsFixture.Number("horizontalForce", descriptor.MaximumHorizontalForce), PhysicsFixture.Number("verticalForce", descriptor.MaximumVerticalForce),
                PhysicsFixture.Number("jumpSpeed", descriptor.JumpSpeed)
            ]))));
        return new(source.Id, source.Revision, source.Entities, definitions, source.Schemas.Append(PhysicsComponentSchemas.Character));
    }
}
