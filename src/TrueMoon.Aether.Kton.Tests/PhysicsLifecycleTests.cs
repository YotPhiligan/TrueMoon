using System.Numerics;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bodies;
using Xunit;

namespace TrueMoon.Aether.Kton.Tests;

public sealed class PhysicsLifecycleTests
{
    [Fact]
    public async Task SceneData_ForwardsCompleteDescriptorToSelectedProvider()
    {
        using var native = new FaultWorldFactory(); await using var app = Build(false, native); await app.StartAsync();
        var body = PhysicsFixture.Body() with { Shape = PhysicsColliderShape.Capsule, Size = new(.3f, .9f, 0), Mass = 2,
            Material = new(1.25f), Filter = new(4, 128), IsSensor = true };
        using var game = PhysicsFixture.Game(app);
        game.LoadSceneWorld(new(PhysicsFixture.Document(new PhysicsFixture.BodySpec(body, new(1, 2, 3))
            { Yaw = .4f, Velocity = new(new(2, 3, 4), new(0, .5f, .25f)) })));
        var world = Assert.Single(native.Worlds); var actual = Assert.Single(world.CreatedBodies);
        Assert.Equal(PhysicsBodyMotion.Dynamic, actual.Motion); Assert.Equal(2f, actual.Mass);
        Assert.Equal(new PhysicsMaterial(1.25f), actual.Material); Assert.Equal(new PhysicsCollisionFilter(4, 128), actual.Filter); Assert.True(actual.IsSensor);
        Assert.Equal(new Vector3(1, 2, 3), actual.Pose.Position); Assert.Equal(Quaternion.CreateFromAxisAngle(Vector3.UnitY, .4f), actual.Pose.Orientation);
        Assert.Equal(new Vector3(2, 3, 4), actual.LinearVelocity); Assert.Equal(new Vector3(0, .5f, .25f), actual.AngularVelocity);
        Assert.True(actual.Shape.IsValid); Assert.Equal(world.Id, actual.Shape.World); Assert.Equal(0UL, world.LastCompletedTick);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CleanupFailure_RetainsNativeOwnerAndBorrowedWorldUntilExplicitRetry(bool microsoft)
    {
        using var native = new FaultWorldFactory(); await using var app = Build(microsoft, native); await app.StartAsync();
        using var game = PhysicsFixture.Game(app); game.LoadSceneWorld(new(Document()));
        var scene = game.ActiveScene!; var world = Assert.Single(native.Worlds); world.DisposeFailures = 1;
        try
        {
            Assert.Throws<IOException>(() => game.Dispose()); Assert.Equal(1, world.DisposeAttempts);
            Assert.Equal(1, native.Resources.ActiveResourceOwners); Assert.False(scene.World.IsDisposed);
            Assert.Throws<InvalidOperationException>(() => scene.Dispose()); Assert.Throws<InvalidOperationException>(() => game.Tick());
            game.Dispose(); game.Dispose();
            Assert.Equal(2, world.DisposeAttempts); Assert.Equal(0, native.Resources.ActiveResourceOwners); Assert.True(scene.IsDisposed);
            Assert.Equal(PhysicsWorldState.Disposed, world.State);
        }
        finally { world.DisposeFailures = 0; game.Dispose(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationAfterNativeCreation_ReleasesWorldWithoutPublishingCandidate(bool microsoft)
    {
        using var end = new CancellationTokenSource(); using var native = new FaultWorldFactory { CancelOnCreate = end };
        await using var app = Build(microsoft, native); await app.StartAsync(); using var game = PhysicsFixture.Game(app);
        var failure = Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(new(Document()), end.Token));
        Assert.Equal(end.Token, failure.CancellationToken); Assert.Null(game.ActiveScene); Assert.Equal(-1, game.Revision);
        var world = Assert.Single(native.Worlds); Assert.Equal(PhysicsWorldState.Disposed, world.State); Assert.Equal(1, world.DisposeAttempts);
        Assert.Equal(0, native.Resources.ActiveResourceOwners); Assert.Equal(0, native.Resources.PublishedWorlds);
    }

    [Fact]
    public async Task CancellationAndCleanupFailure_PreserveOriginalErrorsAndRetainedCandidateForRetry()
    {
        using var end = new CancellationTokenSource(); using var native = new FaultWorldFactory { CancelOnCreate = end, FailAllDisposals = true };
        await using var app = Build(false, native); await app.StartAsync(); using var game = PhysicsFixture.Game(app);
        try
        {
            var failure = Assert.Throws<AggregateException>(() => game.LoadSceneWorld(new(Document()), end.Token));
            Assert.Contains(Flatten(failure), error => error is OperationCanceledException cancellation && cancellation.CancellationToken == end.Token);
            Assert.Contains(Flatten(failure), error => error is IOException { Message: "native cleanup" });
            Assert.Null(game.ActiveScene); Assert.Equal(1, native.Resources.ActiveResourceOwners);
            var world = Assert.Single(native.Worlds);
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(new(Document())));
            world.FailAllDisposals = false; game.Dispose();
            Assert.Equal(0, native.Resources.ActiveResourceOwners); Assert.Equal(PhysicsWorldState.Disposed, world.State);
        }
        finally { foreach (var world in native.Worlds) world.FailAllDisposals = false; game.Dispose(); }
    }

    [Theory]
    [InlineData("factory")] [InlineData("null")] [InlineData("shape")]
    public async Task PreparationFailures_DoNotPublishOrLeakNativeResources(string stage)
    {
        using var native = new FaultWorldFactory { FailCreate = stage == "factory", ReturnNull = stage == "null", FailShapeCreation = stage == "shape" };
        await using var app = Build(false, native); await app.StartAsync(); using var game = PhysicsFixture.Game(app);
        if (stage == "null") Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(new(Document())));
        else Assert.Equal(stage == "factory" ? "world create" : "shape create", Assert.Throws<IOException>(() => game.LoadSceneWorld(new(Document()))).Message);
        Assert.Null(game.ActiveScene); Assert.Equal(0, native.Resources.ActiveResourceOwners); Assert.Equal(0, native.Resources.PublishedWorlds);
        if (stage == "shape") Assert.Equal(PhysicsWorldState.Disposed, Assert.Single(native.Worlds).State); else Assert.Empty(native.Worlds);
    }

    [Fact]
    public async Task NativeStepFailure_PreservesErrorFaultsEcsAndDoesNotAdvancePublishedTick()
    {
        using var native = new FaultWorldFactory { FailNativeStep = true }; await using var app = Build(false, native); await app.StartAsync();
        using var graph = PhysicsGraph.Create(app, Document());
        Assert.Equal("native step", Assert.Throws<IOException>(() => graph.Tick()).Message);
        Assert.True(graph.World.IsFaulted); Assert.Equal(0UL, graph.Physics.LastCompletedTick); Assert.Equal(0UL, graph.Scheduler.TickNumber);
        Assert.Equal(1, Assert.Single(native.Worlds).StepAttempts);
        Assert.Throws<InvalidOperationException>(() => graph.Tick()); Assert.Equal(1, native.Worlds[0].StepAttempts);
        graph.Dispose(); Assert.Equal(0, native.Resources.ActiveResourceOwners); Assert.False(graph.Physics.TryGetBody(default, out _));
    }

    [Fact]
    public async Task PrecancelledPreparation_RejectsBeforeNativeCreation()
    {
        using var native = new FaultWorldFactory(); await using var app = Build(false, native); await app.StartAsync();
        using var scene = PhysicsFixture.Loader(app).Load(Document()); using var end = new CancellationTokenSource(); end.Cancel();
        Assert.Equal(end.Token, Assert.Throws<OperationCanceledException>(() => PhysicsFixture.Subsystems(app).Create(new(scene, PhysicsFixture.Step, end.Token))).CancellationToken);
        Assert.Empty(native.Worlds); Assert.False(scene.World.IsDisposed);
    }

    private static TrueMoon.Aether.Abstractions.Scenes.SceneDocument Document() => PhysicsFixture.Document(new PhysicsFixture.BodySpec(PhysicsFixture.Body(), Vector3.Zero));
    private static IEnumerable<Exception> Flatten(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate) foreach (var nested in aggregate.InnerExceptions) foreach (var inner in Flatten(nested)) yield return inner;
        else if (error.InnerException is { } nested) foreach (var inner in Flatten(nested)) yield return inner;
    }
    private static IApp Build(bool microsoft, FaultWorldFactory native) => App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
    {
        context.Services(services => services.Singleton<IFactory<IPhysicsWorld>>(_ => native));
        context.UseAetherEcs().UseAetherPrimitiveEcs();
        context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
        context.UseAetherKton(); context.UseAetherSceneSubsystems(builder => builder.AddKtonPhysics());
        context.Services(services => services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
        {
            var loader = resolver.Resolve<SceneWorldLoader>()!; var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
            return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
        }));
    }).Build();
}
