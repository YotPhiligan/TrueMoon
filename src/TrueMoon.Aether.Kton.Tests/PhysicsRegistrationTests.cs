using System.Numerics;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;
using Xunit;
using BridgeConfiguration = TrueMoon.Aether.Kton.Runtime.AppConfigurationExtensions;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsRegistrationTests
{
    [Fact]
    public async Task Registry_RejectsUnsupportedSchemaVersionBeforeNativeCreation()
    {
        await using var app = PhysicsFixture.Build(false); await app.StartAsync();
        var source = PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero));
        var definition = source.Definitions.Single();
        var additions = definition.Changes.AddedComponents.Select(component => component.TypeId == PhysicsComponentSchemas.BodyId
            ? new ComponentData(component.Id, component.TypeId, 2, component.Properties) : component);
        var changed = new SceneDocument(source.Id, source.Revision, source.Entities, [new(definition.Id, new(additions))],
            source.Schemas.Select(schema => schema.TypeId == PhysicsComponentSchemas.BodyId
                ? new ComponentSchema(schema.TypeId, 2, schema.Properties.Values, requiredTypes: schema.RequiredTypes) : schema));
        var failure = Assert.Throws<SceneWorldLoadException>(() => PhysicsFixture.Loader(app).Load(changed));
        Assert.Equal(SceneWorldLoadError.UnsupportedSchemaVersion, failure.Error); Assert.Equal(0, PhysicsFixture.Host(app).Resources.ActiveResourceOwners);
    }

    [Theory]
    [InlineData(0)] [InlineData(257)]
    public void Registration_RejectsInvalidProjectionBoundsBeforeAnyNativeResources(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsFixture.Build(false, bridge: options => options.MaximumContactEvents = maximum));
    }

    [Theory]
    [InlineData(1)] [InlineData(256)]
    public async Task Registration_AdmitsExactProjectionBoundariesAndSnapshotsOptions(int maximum)
    {
        PhysicsBridgeOptions captured = null!;
        await using var app = PhysicsFixture.Build(false, bridge: options => { options.MaximumContactEvents = maximum; captured = options; });
        captured.MaximumContactEvents = 0; await app.StartAsync();
        using var game = PhysicsFixture.Game(app); game.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero))));
        game.Tick(); Assert.Equal(1UL, game.TickNumber); Assert.Equal(1, PhysicsFixture.Host(app).Resources.PublishedWorlds);
    }

    [Fact]
    public void Registration_RejectsNullDuplicateAndMissingProviderCompositions()
    {
        Assert.Throws<ArgumentNullException>(() => BridgeConfiguration.UseAetherKton(null!));
        Assert.Throws<ArgumentNullException>(() => BridgeConfiguration.AddKtonPhysics(null!));
        Assert.Throws<ArgumentNullException>(() => PhysicsComponentRegistry.Register(null!));
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context => context.UseAetherKton()).Build());
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context => { context.UseBepuPhysics(); context.UseAetherKton(); context.UseAetherKton(); }).Build());
        Assert.Throws<ArgumentException>(() => new SceneSubsystemsBuilder().AddKtonPhysics().AddKtonPhysics());
        var registry = PhysicsComponentRegistry.Register(new()); Assert.Throws<ArgumentException>(() => PhysicsComponentRegistry.Register(registry));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConfiguredRegistration_UsesExactSchemaAndExplicitOverrideBeforeSnapshot(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(PhysicsBridgeConfigurationSchema.Default); builder.Override("Kton:Bridge:MaximumContactEvents", 2); });
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.UseBepuPhysics(options => options.DefaultWorld = new() { Gravity = Vector3.Zero });
            context.UseAetherEcs().UseAetherPrimitiveEcs();
            context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
            context.UseConfiguredAetherKton(options => options.MaximumContactEvents = 1);
            context.UseAetherSceneSubsystems(builder => builder.AddKtonPhysics());
        }).Build();
        await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, PhysicsFixture.Document(
            new(PhysicsFixture.Body(PhysicsBodyMotion.Static) with { Size = new(10, 1, 10) }, new(0, -.5f, 0)),
            new(PhysicsFixture.Body() with { IsSensor = true }, new(-2, .4f, 0)),
            new(PhysicsFixture.Body() with { IsSensor = true }, new(2, .4f, 0))));
        var failure = Assert.Throws<PhysicsException>(() => graph.Tick());
        Assert.Equal(PhysicsErrorCode.ContactOverflow, failure.Error.Code); Assert.Equal("Bridge.ContactProjection", failure.Error.Field);
    }

    [Fact]
    public void ConfiguredRegistration_RequiresRegisteredExactSchema()
    {
        var profile = ConfigurationProfiles.Create(_ => { });
        Assert.Throws<ArgumentException>(() => App.Builder(_ => { }).Setup(context =>
        { context.UseConfiguration(profile); context.UseBepuPhysics(); context.UseConfiguredAetherKton(); }).Build());
    }
}
