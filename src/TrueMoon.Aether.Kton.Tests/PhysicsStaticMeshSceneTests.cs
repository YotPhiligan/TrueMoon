using System.Numerics;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using Xunit;
using EcsSystemDescription = TrueMoon.Aether.Ecs.EcsSystemDescription;
using EntityHandle = TrueMoon.Aether.Ecs.EntityHandle;
using ISystem = TrueMoon.Aether.Ecs.ISystem;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsStaticMeshSceneTests
{
    private static readonly Guid RampId = new("a92347ef-7384-4f4d-8f0f-6a33dcab9e01");
    private static readonly Guid OtherRampId = new("a92347ef-7384-4f4d-8f0f-6a33dcab9e02");
    private static readonly PhysicsBody Ramp = new(PhysicsBodyMotion.Static, PhysicsColliderShape.StaticMesh, Vector3.Zero, 0)
    { MeshAssetId = RampId };
    private static readonly PhysicsBody Floor = PhysicsFixture.Body(PhysicsBodyMotion.Static) with { Size = new(8, 1, 8) };
    private static readonly PhysicsBody Capsule = new(PhysicsBodyMotion.Dynamic, PhysicsColliderShape.Capsule, new(.30f, .60f, 0), 1);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AuthoredMesh_CapturesInputAndPublishesSupport(bool microsoft)
    {
        var vertices = RampVertices();
        var indices = RampIndices();
        void Configure(PhysicsBridgeOptions options) => options.AddStaticMesh(RampId, vertices, indices);
        await using var app = microsoft ? BuildConfigured(Configure) : PhysicsFixture.Build(false, world: new(), bridge: Configure);
        Array.Fill(vertices, Vector3.Zero);
        Array.Fill(indices, -1);
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(CharacterDocument(Ramp, Vector3.Zero, new(1.75f, .55f, 0), RampId.ToString("D"))));
        var scene = game.ActiveScene!;
        var mesh = scene.Entities[PhysicsFixture.Entity(0)];
        var player = scene.Entities[PhysicsFixture.Entity(1)];
        Assert.Equal(Ramp, scene.World.GetComponent<PhysicsBody>(mesh));
        Assert.Equal(Capsule, scene.World.GetComponent<PhysicsBody>(player));

        for (var i = 0; i < 60; i++) game.Tick();
        AssertRampSupport(scene, player, mesh);
        var settled = PhysicsFixture.Position(game, 1);
        scene.World.SetComponent(player, new PhysicsCharacterInput(new(0, -2.4f)));
        for (var i = 0; i < 10; i++) game.Tick();
        var finish = PhysicsFixture.Position(game, 1);
        Assert.True(finish.Y > settled.Y && finish.Z < settled.Z, $"Expected uphill motion from {settled}, got {finish}.");
        AssertRampSupport(scene, player, mesh);
        Assert.Equal(70UL, game.TickNumber);
    }

    [Fact]
    public async Task BodyV2_PrimitiveWithoutAssetStillSupportsCapsule()
    {
        await using var app = PhysicsFixture.Build(false, world: new());
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(CharacterDocument(Floor, new(0, -.5f, 0), Vector3.Zero)));
        var scene = game.ActiveScene!;
        var floor = scene.Entities[PhysicsFixture.Entity(0)];
        var player = scene.Entities[PhysicsFixture.Entity(1)];
        Assert.Equal(Floor, scene.World.GetComponent<PhysicsBody>(floor));
        for (var i = 0; i < 60; i++) game.Tick();
        var support = scene.World.GetComponent<PhysicsCharacterState>(player);
        Assert.True(support.IsSupported);
        Assert.Equal(floor, support.SupportEntity);
        Assert.True(support.SupportNormal.Y > .99f);
    }

    public enum InvalidDescriptor { LegacyMesh, MissingId, EmptyId, MalformedId, UndashedId, Dynamic, Kinematic, Size, DefaultSize, Mass, PrimitiveAsset }

    [Theory]
    [InlineData(InvalidDescriptor.LegacyMesh)]
    [InlineData(InvalidDescriptor.MissingId)]
    [InlineData(InvalidDescriptor.EmptyId)]
    [InlineData(InvalidDescriptor.MalformedId)]
    [InlineData(InvalidDescriptor.UndashedId)]
    [InlineData(InvalidDescriptor.Dynamic)]
    [InlineData(InvalidDescriptor.Kinematic)]
    [InlineData(InvalidDescriptor.Size)]
    [InlineData(InvalidDescriptor.DefaultSize)]
    [InlineData(InvalidDescriptor.Mass)]
    [InlineData(InvalidDescriptor.PrimitiveAsset)]
    public async Task Registry_RejectsInvalidMeshDescriptor(InvalidDescriptor invalid)
    {
        await using var app = PhysicsFixture.Build(false);
        await app.StartAsync();
        var body = invalid switch
        {
            InvalidDescriptor.Dynamic => Ramp with { Motion = PhysicsBodyMotion.Dynamic, Mass = 1 },
            InvalidDescriptor.Kinematic => Ramp with { Motion = PhysicsBodyMotion.Kinematic },
            InvalidDescriptor.Size => Ramp with { Size = new(1, 0, 0) },
            InvalidDescriptor.Mass => Ramp with { Mass = 1 },
            InvalidDescriptor.PrimitiveAsset => Floor,
            _ => Ramp
        };
        var asset = invalid switch
        {
            InvalidDescriptor.LegacyMesh or InvalidDescriptor.MissingId => null,
            InvalidDescriptor.EmptyId => Guid.Empty.ToString("D"),
            InvalidDescriptor.MalformedId => new string('x', 36),
            InvalidDescriptor.UndashedId => RampId.ToString("N"),
            _ => RampId.ToString("D")
        };
        var source = PhysicsFixture.Document(new PhysicsFixture.BodySpec(body, Vector3.Zero));
        var document = invalid == InvalidDescriptor.LegacyMesh ? source : WithBodyV2(source, 0, asset, omitDimensions: invalid == InvalidDescriptor.DefaultSize);

        var failure = Assert.Throws<SceneWorldLoadException>(() => PhysicsFixture.Loader(app).Load(document));
        Assert.Equal(SceneWorldLoadError.InvalidValue, failure.Error);
        var resources = PhysicsFixture.Host(app).Resources;
        Assert.Equal(0, resources.PublishedWorlds);
        Assert.Equal(0, resources.ActiveResourceOwners);
    }

    [Fact]
    public void MeshRegistration_RejectsBadIdsAndGeometryBeforePublication()
    {
        var options = new PhysicsBridgeOptions();
        var vertices = RampVertices();
        var indices = RampIndices();
        Assert.Throws<ArgumentException>(() => options.AddStaticMesh(Guid.Empty, vertices, indices));
        var outside = Assert.Throws<PhysicsException>(() => options.AddStaticMesh(RampId, vertices, new[] { 0, 1, 4 }));
        Assert.Equal((PhysicsErrorCode.InvalidArgument, "Shape.Indices"), (outside.Error.Code, outside.Error.Field));
        var degenerate = Assert.Throws<PhysicsException>(() => options.AddStaticMesh(RampId, vertices, new[] { 0, 1, 0 }));
        Assert.Equal((PhysicsErrorCode.InvalidArgument, "Shape.DegenerateTriangle"), (degenerate.Error.Code, degenerate.Error.Field));
        options.AddStaticMesh(RampId, vertices, indices);
        Assert.Throws<ArgumentException>(() => options.AddStaticMesh(RampId, vertices, indices));
        Assert.Throws<ArgumentException>(() => options.AddStaticMesh(RampId, default, default));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MeshPreparation_RejectsMissingOrOverLimitAssetAndLeavesSessionPrivate(bool overLimit)
    {
        await using var app = PhysicsFixture.Build(false,
            world: new() { Limits = new() { MaxMeshVertices = 3 } }, bridge: overLimit ? RegisterRamp : null);
        await app.StartAsync();
        using var game = PhysicsFixture.Game(app);
        var document = RuntimeDocument(withMesh: true);
        if (overLimit)
        {
            var failure = Assert.Throws<PhysicsException>(() => game.LoadSceneWorld(new(document)));
            Assert.Equal((PhysicsErrorCode.CapacityExceeded, "Shape.MeshInput"), (failure.Error.Code, failure.Error.Field));
        }
        else
        {
            var failure = Assert.Throws<InvalidDataException>(() => game.LoadSceneWorld(new(document)));
            Assert.Contains(RampId.ToString("D"), failure.Message);
        }
        Assert.Null(game.ActiveScene);
        Assert.Equal(0UL, game.TickNumber);
        var resources = PhysicsFixture.Host(app).Resources;
        Assert.Equal(0, resources.PublishedWorlds);
        Assert.Equal(0, resources.ActiveResourceOwners);
        Assert.False(resources.PendingFailedCreationCleanup);
    }

    public enum RuntimeEdit { PrimitiveToMesh, MeshToPrimitive, AssetId, StaticLinearVelocity, StaticAngularVelocity, Reattach, Spawn }

    [Theory]
    [InlineData(RuntimeEdit.PrimitiveToMesh)]
    [InlineData(RuntimeEdit.MeshToPrimitive)]
    [InlineData(RuntimeEdit.AssetId)]
    [InlineData(RuntimeEdit.StaticLinearVelocity)]
    [InlineData(RuntimeEdit.StaticAngularVelocity)]
    [InlineData(RuntimeEdit.Reattach)]
    [InlineData(RuntimeEdit.Spawn)]
    public async Task RuntimeMesh_RejectsAttachmentDescriptorOrVelocityEditBeforeStep(RuntimeEdit edit)
    {
        await using var app = PhysicsFixture.Build(false, bridge: options =>
        {
            RegisterRamp(options);
            options.AddStaticMesh(OtherRampId, RampVertices(), RampIndices());
        });
        await app.StartAsync();
        var startsWithMesh = edit is not (RuntimeEdit.PrimitiveToMesh or RuntimeEdit.Spawn);
        EntityHandle target = default;
        var description = PhysicsEntityCommands.DeclareWrites(new EcsSystemDescription("test.mesh-attachment"));
        var commands = new EcsSystemRegistration(description, new Factory<ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            if (edit == RuntimeEdit.Reattach)
            {
                if (context.TickNumber == 1) PhysicsEntityCommands.RemoveBody(context.Commands, target);
                if (context.TickNumber == 2) PhysicsEntityCommands.AddBody(context.Commands, target, Ramp);
            }
            else if (edit == RuntimeEdit.Spawn && context.TickNumber == 1)
                PhysicsEntityCommands.CreateBody(context.Commands, Ramp, LocalTransform.Identity);
        })));
        using var graph = PhysicsGraph.Create(app, RuntimeDocument(startsWithMesh), commands);
        target = graph.Scene.Entities[PhysicsFixture.Entity(1)];
        var neighbor = graph.Scene.Entities[PhysicsFixture.Entity(0)];
        switch (edit)
        {
            case RuntimeEdit.PrimitiveToMesh: graph.World.SetComponent(target, Ramp); break;
            case RuntimeEdit.MeshToPrimitive: graph.World.SetComponent(target, Floor); break;
            case RuntimeEdit.AssetId: graph.World.SetComponent(target, Ramp with { MeshAssetId = OtherRampId }); break;
            case RuntimeEdit.StaticLinearVelocity: graph.World.SetComponent(target, new PhysicsVelocity(Vector3.UnitX)); break;
            case RuntimeEdit.StaticAngularVelocity: graph.World.SetComponent(target, new PhysicsVelocity(Vector3.Zero, Vector3.UnitY)); break;
            case RuntimeEdit.Reattach:
                graph.Tick();
                Assert.False(graph.Physics.TryGetBody(target, out _));
                Assert.False(graph.World.HasComponent<PhysicsBody>(target));
                break;
        }

        var tick = graph.Physics.LastCompletedTick;
        Assert.Equal(edit == RuntimeEdit.Reattach ? 1UL : 0UL, tick);
        var before = graph.World.GetComponent<LocalTransform>(neighbor).Position;
        if (edit is RuntimeEdit.StaticLinearVelocity or RuntimeEdit.StaticAngularVelocity)
        {
            var failure = Assert.Throws<PhysicsException>(() => graph.Tick());
            Assert.Equal((PhysicsErrorCode.InvalidArgument, "Body.StaticVelocity"), (failure.Error.Code, failure.Error.Field));
        }
        else
        {
            var failure = Assert.Throws<InvalidDataException>(() => graph.Tick());
            Assert.Contains("only be attached during authored scene preparation", failure.Message);
        }
        Assert.Equal(tick, graph.Physics.LastCompletedTick);
        Assert.Equal(before, graph.World.GetComponent<LocalTransform>(neighbor).Position);
    }

    [Fact]
    public async Task RuntimeMesh_TeleportUsesSameBodyAndRemovalRetiresMapping()
    {
        await using var app = PhysicsFixture.Build(false, bridge: RegisterRamp);
        await app.StartAsync();
        var host = PhysicsFixture.Host(app);
        EntityHandle mesh = default;
        var commands = new EcsSystemRegistration(new EcsSystemDescription("test.delete-mesh"), new Factory<ISystem>(() => new PhysicsGraph.Callback(context =>
        {
            if (context.TickNumber == 2) context.Commands.DestroyEntity(mesh);
        })));
        using (var graph = PhysicsGraph.Create(app, RuntimeDocument(withMesh: true), commands))
        {
            mesh = graph.Scene.Entities[PhysicsFixture.Entity(1)];
            Assert.True(graph.Physics.TryGetBody(mesh, out var body));
            var destination = new Vector3(0, 2, 3);
            var teleport = new PhysicsTeleport(1, new(destination, Quaternion.Identity), ClearVelocity: true);
            graph.World.AddComponent(mesh, teleport);
            Assert.Equal(Vector3.Zero, graph.World.GetComponent<LocalTransform>(mesh).Position);
            graph.Tick();
            Assert.Equal(destination, graph.World.GetComponent<LocalTransform>(mesh).Position);
            Assert.True(graph.Physics.TryGetBody(mesh, out var afterTeleport));
            Assert.Equal(body, afterTeleport);
            Assert.True(graph.Physics.TryGetEntity(body, out var mapped));
            Assert.Equal(mesh, mapped);
            graph.Tick();
            Assert.False(graph.World.IsAlive(mesh));
            graph.Tick();
            Assert.Equal(3UL, graph.Physics.LastCompletedTick);
            Assert.False(graph.Physics.TryGetBody(mesh, out _));
            Assert.False(graph.Physics.TryGetEntity(body, out _));
        }
        await app.StopAsync();
        Assert.True(host.IsReleased);
        var resources = host.Resources;
        Assert.Equal(0, resources.PublishedWorlds);
        Assert.Equal(0, resources.ActiveResourceOwners);
        Assert.Equal(0L, resources.MaximumPoolBytesAfterCleanup);
        Assert.False(resources.PendingFailedCreationCleanup);
    }

    private static Vector3[] RampVertices() => [new(1, 1, -1), new(2.5f, 1, -1), new(1, 0, 1), new(2.5f, 0, 1)];
    private static int[] RampIndices() => [0, 1, 2, 1, 3, 2];
    private static void RegisterRamp(PhysicsBridgeOptions options) => options.AddStaticMesh(RampId, RampVertices(), RampIndices());

    private static void AssertRampSupport(SceneWorld scene, EntityHandle player, EntityHandle mesh)
    {
        var support = scene.World.GetComponent<PhysicsCharacterState>(player);
        Assert.True(support.IsSupported);
        Assert.Equal(mesh, support.SupportEntity);
        Assert.True(support.SupportNormal.Y is > .85f and < .95f && support.SupportNormal.Z > .4f, $"Unexpected ramp normal: {support.SupportNormal}.");
    }

    private static SceneDocument CharacterDocument(PhysicsBody surface, Vector3 position, Vector3 feet, string? asset = null)
    {
        var source = PhysicsFixture.Document(new(surface, position), new(Capsule, feet + new Vector3(0, .60f, 0)));
        var document = WithBodyV2(source, 0, asset);
        var player = document.Definitions[1];
        var character = new ComponentData(new(Guid.NewGuid()), PhysicsComponentSchemas.CharacterId, 2, [PhysicsFixture.Number("speed", 2.4)]);
        return new(document.Id, document.Revision, document.Entities,
            document.Definitions.SetItem(1, new(player.Id, new(player.Changes.AddedComponents.Add(character)))),
            document.Schemas.Add(PhysicsComponentSchemas.CharacterV2));
    }

    private static SceneDocument RuntimeDocument(bool withMesh)
    {
        var source = PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), new(20, 4, 0)) { Velocity = new(Vector3.UnitX) },
            new(withMesh ? Ramp : Floor, Vector3.Zero));
        return withMesh ? WithBodyV2(source, 1, RampId.ToString("D")) : source;
    }

    private static SceneDocument WithBodyV2(SceneDocument source, int index, string? asset, bool omitDimensions = false)
    {
        var definition = source.Definitions[index];
        var components = definition.Changes.AddedComponents.Select(component =>
        {
            if (component.TypeId != PhysicsComponentSchemas.BodyId) return component;
            IEnumerable<KeyValuePair<string, PropertyValue>> properties = component.Properties;
            if (omitDimensions) properties = properties.Where(property => property.Key is not ("x" or "y" or "z"));
            if (asset is not null) properties = properties.Append(new("meshAsset", PropertyValue.From(asset)));
            return new ComponentData(component.Id, component.TypeId, 2, properties);
        });
        return new(source.Id, source.Revision, source.Entities, source.Definitions.SetItem(index, new(definition.Id, new(components))),
            source.Schemas.Add(PhysicsComponentSchemas.BodyV2));
    }

    private static IApp BuildConfigured(Action<PhysicsBridgeOptions> bridge)
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(PhysicsBridgeConfigurationSchema.Default));
        return App.Builder(context => context.UseDI()).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.UseBepuPhysics();
            context.UseAetherEcs().UseAetherPrimitiveEcs();
            context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.UseConfiguredAetherKton(bridge);
            context.UseAetherSceneSubsystems(builder => builder.AddKtonPhysics());
            context.Services(services => services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
            {
                var loader = resolver.Resolve<SceneWorldLoader>()!;
                var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
            }));
        }).Build();
    }
}
