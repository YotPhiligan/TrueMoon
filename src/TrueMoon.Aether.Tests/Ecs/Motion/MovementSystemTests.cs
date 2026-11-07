using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Tests.Ecs.Transforms;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Motion;

public sealed class MovementSystemTests
{
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1048577)]
    public void Constructor_RejectsAdmissionOutsideBoundsWithoutMutation(int maximum)
    {
        using var world = new World();
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovementSystem(world, maximum));
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(1)] [InlineData(1048576)]
    public void Constructor_AcceptsAdmissionEndpointsAndEmptyAdvanceRemainsEmpty(int maximum)
    {
        using var world = new World(); using var system = new MovementSystem(world, maximum);
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
        system.Advance(TimeSpan.FromTicks(1));
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount)); Assert.False(world.IsFaulted);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MovementSystem(null!));
        using var world = new World(); using var system = new MovementSystem(world);
        Assert.Throws<ArgumentNullException>(() => system.Execute(null!));
    }

    public static IEnumerable<object[]> NonfiniteVelocities()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        for (var axis = 0; axis < 3; axis++)
        {
            var vector = new Vector3(2, -3, 5); vector[axis] = value; yield return [new LinearVelocity(vector)];
        }
    }

    [Theory]
    [MemberData(nameof(NonfiniteVelocities))]
    public void Velocity_RejectsNonfiniteValuesInEveryAxis(LinearVelocity velocity)
    {
        Assert.Throws<InvalidDataException>(() => velocity.Validate());
        using var world = new World(); var entity = Moving(world, new(1, 2, 3), velocity.Value); var old = world.GetComponent<LocalTransform>(entity);
        using var system = new MovementSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(TransformEvaluationError.InvalidVelocity, error.Error); Assert.Equal(entity, error.Entity);
        Assert.IsType<InvalidDataException>(error.InnerException); Assert.Equal(old, world.GetComponent<LocalTransform>(entity)); Assert.False(world.IsFaulted);
    }

    [Fact]
    public void Advance_UsesVelocityTimesFractionalSecondsForAllAxesAndPreservesOtherPoseFields()
    {
        using var world = new World(); var entity = Moving(world, new(7, -11, 13), new(2, -3, 5));
        var initial = new LocalTransform(new(7, -11, 13), new(.5f, .5f, .5f, .5f), new(2, 3, 4)); world.SetComponent(entity, initial);
        var stationary = world.CreateEntity(); world.AddComponent(stationary, LocalTransform.Identity with { Position = new(19, 23, 29) });
        using var system = new MovementSystem(world); system.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Equal(initial with { Position = new(7.5f, -11.75f, 14.25f) }, world.GetComponent<LocalTransform>(entity));
        Assert.Equal(new Vector3(19, 23, 29), world.GetComponent<LocalTransform>(stationary).Position);
        system.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(initial with { Position = new(11.5f, -17.75f, 24.25f) }, world.GetComponent<LocalTransform>(entity));
        Assert.Equal(new LinearVelocity(new(2, -3, 5)), world.GetComponent<LinearVelocity>(entity));
    }

    [Fact]
    public void Advance_ZeroVelocityAndLocalWithoutVelocityAreNoOps()
    {
        default(LinearVelocity).Validate(); new LinearVelocity(new(float.MaxValue, -float.MaxValue, float.Epsilon)).Validate();
        using var world = new World(); var zero = Moving(world, new(3, -5, 7), Vector3.Zero);
        var localOnly = world.CreateEntity(); world.AddComponent(localOnly, default(LocalTransform));
        var old = world.GetComponent<LocalTransform>(zero); using var system = new MovementSystem(world); system.Advance(TimeSpan.MaxValue);
        Assert.Equal(old, world.GetComponent<LocalTransform>(zero));
        Assert.Equal(default, world.GetComponent<LocalTransform>(localOnly)); Assert.False(world.IsFaulted);
    }

    [Fact]
    public void Advance_VelocityWithoutLocalIsTypedIncompleteNode()
    {
        using var world = new World(); var entity = world.CreateEntity(); world.AddComponent(entity, new LinearVelocity(new(2, 3, 5)));
        using var system = new MovementSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(TransformEvaluationError.IncompleteNode, error.Error); Assert.Equal(entity, error.Entity);
        Assert.False(world.HasComponent<LocalTransform>(entity)); Assert.False(world.IsFaulted);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(long.MinValue)]
    public void Advance_RejectsZeroAndNegativeDurationWithoutMoving(long ticks)
    {
        using var world = new World(); var entity = Moving(world, new(3, 5, 7), new(11, 13, 17)); var old = world.GetComponent<LocalTransform>(entity);
        using var system = new MovementSystem(world);
        Assert.Throws<ArgumentOutOfRangeException>(() => system.Advance(TimeSpan.FromTicks(ticks)));
        Assert.Equal(old, world.GetComponent<LocalTransform>(entity)); Assert.False(world.IsFaulted);
        system.Advance(TimeSpan.FromTicks(1)); Assert.True(world.GetComponent<LocalTransform>(entity).Position.X > 3);
    }

    [Fact]
    public void Advance_UsesDoubleIntermediateSoLargeFiniteTermsMayCancelBeforeFloatConversion()
    {
        using var world = new World(); var entity = Moving(world, new(-float.MaxValue, float.MaxValue, 2), new(float.MaxValue, -float.MaxValue, 3));
        using var system = new MovementSystem(world); system.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(new Vector3(float.MaxValue, -float.MaxValue, 8), world.GetComponent<LocalTransform>(entity).Position);
    }

    [Theory]
    [InlineData("velocity", TransformEvaluationError.InvalidVelocity)]
    [InlineData("overflow-x", TransformEvaluationError.InvalidVelocity)]
    [InlineData("overflow-y", TransformEvaluationError.InvalidVelocity)]
    [InlineData("overflow-z", TransformEvaluationError.InvalidVelocity)]
    [InlineData("local", TransformEvaluationError.InvalidLocalTransform)]
    [InlineData("missing-local", TransformEvaluationError.IncompleteNode)]
    public void LateInvalidCandidate_PreservesEveryPositionAndAllowsRetry(string kind, TransformEvaluationError expected)
    {
        using var world = new World(); var early = Moving(world, new(1, 2, 3), new(5, 7, 11));
        var late = Moving(world, new(13, 17, 19), new(23, 29, 31));
        if (kind == "velocity") world.SetComponent(late, new LinearVelocity(new(float.NaN, 0, 0)));
        if (kind.StartsWith("overflow", StringComparison.Ordinal))
        {
            var axis = kind[^1] == 'x' ? 0 : kind[^1] == 'y' ? 1 : 2;
            var position = new Vector3(13, 17, 19); position[axis] = float.MaxValue;
            var rate = Vector3.Zero; rate[axis] = float.MaxValue;
            world.SetComponent(late, LocalTransform.Identity with { Position = position }); world.SetComponent(late, new LinearVelocity(rate));
        }
        if (kind == "local") world.SetComponent(late, default(LocalTransform));
        if (kind == "missing-local") world.RemoveComponent<LocalTransform>(late);
        var oldEarly = world.GetComponent<LocalTransform>(early);
        var hasLate = world.TryGetComponent<LocalTransform>(late, out var oldLate);
        using var system = new MovementSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Advance(TimeSpan.FromSeconds(2)));
        Assert.Equal(expected, error.Error); Assert.Equal(late, error.Entity);
        Assert.Equal(oldEarly, world.GetComponent<LocalTransform>(early));
        Assert.Equal(hasLate, world.TryGetComponent<LocalTransform>(late, out var afterLate)); Assert.Equal(oldLate, afterLate); Assert.False(world.IsFaulted);
        if (hasLate) world.SetComponent(late, LocalTransform.Identity); else world.AddComponent(late, LocalTransform.Identity);
        world.SetComponent(late, new LinearVelocity(new(2, 3, 5)));
        system.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(new Vector3(11, 16, 25), world.GetComponent<LocalTransform>(early).Position);
        Assert.Equal(new Vector3(4, 6, 10), world.GetComponent<LocalTransform>(late).Position);
    }

    [Fact]
    public void PreCancelledAdvance_PreservesEveryPoseAndCanRetry()
    {
        using var world = new World(); var first = Moving(world, new(1, 2, 3), new(5, 7, 11)); var second = Moving(world, new(-1, -2, -3), new(2, 3, 5));
        var nodes = new[] { first, second }; var before = nodes.Select(world.GetComponent<LocalTransform>).ToArray();
        using var system = new MovementSystem(world); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => system.Advance(TimeSpan.FromSeconds(1), cancellation.Token));
        Assert.Equal(before, nodes.Select(world.GetComponent<LocalTransform>).ToArray()); Assert.False(world.IsFaulted);
        system.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(new Vector3(6, 9, 14), world.GetComponent<LocalTransform>(first).Position); Assert.Equal(new Vector3(1, 1, 2), world.GetComponent<LocalTransform>(second).Position);
    }

    [Fact]
    public void AdmissionAtOne_WorksThenExcessFailsBeforeAnyMovement()
    {
        using var world = new World(); var first = Moving(world, new(1, 2, 3), new(2, 3, 5)); using var system = new MovementSystem(world, 1);
        system.Advance(TimeSpan.FromSeconds(1)); Assert.Equal(new Vector3(3, 5, 8), world.GetComponent<LocalTransform>(first).Position);
        var second = Moving(world, new(11, 13, 17), new(1, 1, 1));
        var error = Assert.Throws<TransformEvaluationException>(() => system.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(TransformEvaluationError.LimitExceeded, error.Error); Assert.Equal(second, error.Entity);
        Assert.Equal(new Vector3(3, 5, 8), world.GetComponent<LocalTransform>(first).Position); Assert.Equal(new Vector3(11, 13, 17), world.GetComponent<LocalTransform>(second).Position);
        world.DestroyEntity(second); system.Advance(TimeSpan.FromSeconds(1)); Assert.Equal(new Vector3(5, 8, 13), world.GetComponent<LocalTransform>(first).Position);
    }

    [Theory]
    [InlineData("system")] [InlineData("world")] [InlineData("fault")]
    public void Advance_RejectsDisposedAndFaultedOwnerWithoutMoving(string kind)
    {
        using var world = new World(); var entity = Moving(world, new(1, 2, 3), new(5, 7, 11)); var old = world.GetComponent<LocalTransform>(entity);
        using var system = new MovementSystem(world);
        if (kind == "system") { system.Dispose(); system.Dispose(); }
        if (kind == "world") world.Dispose();
        if (kind == "fault") world.MarkFaulted(new IOException("Injected owner failure"));
        if (kind == "fault") Assert.Throws<InvalidOperationException>(() => system.Advance(TimeSpan.FromSeconds(1)));
        else Assert.Throws<ObjectDisposedException>(() => system.Advance(TimeSpan.FromSeconds(1)));
        if (kind != "world") { Assert.True(world.IsAlive(entity)); Assert.Equal(old, world.GetComponent<LocalTransform>(entity)); }
    }

    [Fact]
    public void Execute_RejectsForeignContextWithoutChangingOwner()
    {
        using var owner = new World(); var entity = Moving(owner, new(1, 2, 3), new(5, 7, 11));
        using var foreign = new World(); using var target = new MovementSystem(owner);
        using var scheduler = EcsScheduler.Create(foreign, [new(MovementSystem.Describe(), new Factory<IEcsSystem, World>(_ => new ContextProbe(target)))]);
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(TimeSpan.FromSeconds(1)));
        Assert.Equal(new Vector3(1, 2, 3), owner.GetComponent<LocalTransform>(entity).Position); Assert.False(owner.IsFaulted); Assert.True(foreign.IsFaulted);
    }

    [Fact]
    public void BuiltinDescriptors_DeclareActualAccessAndOrderMovementBeforeTransform()
    {
        using var world = new World();
        var root = TransformSystemTests.Node(world, new(new(10, 20, 30), new(.5f, .5f, .5f, .5f), new(2, 3, 4)));
        var child = TransformSystemTests.Node(world, new(new(5, 7, 11), new(1, 0, 0, 0), new(3, 5, 7)), root);
        world.AddComponent(child, new LinearVelocity(new(2, -3, 5)));
        var movement = MovementSystem.Describe(); var transform = TransformSystem.Describe();
        Assert.Equal("builtin.movement", movement.Id); Assert.Equal(EcsPhase.Movement, movement.Phase);
        Assert.Equal("builtin.transform", transform.Id); Assert.Equal(EcsPhase.Transform, transform.Phase);
        Assert.Equal(new[] { typeof(LinearVelocity) }, movement.Snapshot().Reads);
        Assert.Equal(new[] { typeof(LocalTransform) }, movement.Snapshot().Writes);
        Assert.True(transform.Snapshot().Reads.SetEquals([typeof(LocalTransform), typeof(TransformParent)]));
        Assert.Equal(new[] { typeof(WorldTransform) }, transform.Snapshot().Writes);
        using var scheduler = EcsScheduler.Create(world,
        [
            new(transform, new Factory<IEcsSystem, World>(w => new TransformSystem(w))),
            new(movement, new Factory<IEcsSystem, World>(w => new MovementSystem(w)))
        ]);
        Assert.True(scheduler.Tick(TimeSpan.FromSeconds(2)));
        // Local displacement remains parent-space despite the child's own 180-degree rotation.
        Assert.Equal(new Vector3(9, 1, 21), world.GetComponent<LocalTransform>(child).Position);
        Assert.Equal(new Vector3(94, 38, 33), world.GetComponent<WorldTransform>(child).Matrix.Translation);
        Assert.Equal(1UL, world.GetComponent<WorldTransform>(child).Tick); Assert.Equal(1UL, scheduler.TickNumber);
        Assert.Equal(EcsSchedulerState.Ready, scheduler.State); Assert.False(world.IsFaulted);
    }

    private static EntityHandle Moving(World world, Vector3 position, Vector3 velocity)
    {
        var entity = world.CreateEntity(); world.AddComponent(entity, LocalTransform.Identity with { Position = position });
        world.AddComponent(entity, new LinearVelocity(velocity)); return entity;
    }

    private sealed class ContextProbe(MovementSystem target) : IEcsSystem
    {
        public void Execute(EcsSystemContext context) => target.Execute(context);
        public void Dispose() { }
    }
}
