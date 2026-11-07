using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tests.Ecs.Loading;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Transforms;

public sealed class TransformSystemTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1048577)]
    public void Constructor_RejectsInvalidAdmissionWithoutMutatingWorld(int maximum)
    {
        using var world = new World();
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransformSystem(world, maximum));
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1048576)]
    public void Constructor_IsReadonlyAndAcceptsBothAdmissionEndpoints(int maximum)
    {
        using var world = new World();
        using var system = new TransformSystem(world, maximum);
        Assert.Null(system.LastTick);
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
        system.Update(0);
        Assert.Equal(0UL, system.LastTick);
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TransformSystem(null!));
        using var world = new World(); using var system = new TransformSystem(world);
        Assert.Throws<ArgumentNullException>(() => system.Execute(null!));
        Assert.Throws<ArgumentNullException>(() => system.InitializeScene(null!));
        Assert.Null(system.LastTick);
    }

    [Fact]
    public void Update_SingleRootPublishesLiteralMatrixAndStrictlyIncreasingTicks()
    {
        using var world = new World();
        var entity = Node(world, new(new(5, -7, 11), new(.5f, .5f, .5f, .5f), new(2, 3, 4)));
        using var system = new TransformSystem(world);
        system.Update(0);
        var expected = new Matrix4x4(0, 2, 0, 0, 0, 0, 3, 0, 4, 0, 0, 0, 5, -7, 11, 1);
        Assert.Equal(new WorldTransform(expected, 0), world.GetComponent<WorldTransform>(entity));
        system.Update(17);
        foreach (var tick in new ulong[] { 17, 16, 0 })
        {
            Assert.Throws<InvalidOperationException>(() => system.Update(tick));
            Assert.Equal(new WorldTransform(expected, 17), world.GetComponent<WorldTransform>(entity));
            Assert.Equal(17UL, system.LastTick);
        }
        system.Update(ulong.MaxValue);
        Assert.Equal(new WorldTransform(expected, ulong.MaxValue), world.GetComponent<WorldTransform>(entity));
        Assert.Throws<InvalidOperationException>(() => system.Update(ulong.MaxValue));
        Assert.Equal(ulong.MaxValue, system.LastTick);
    }

    [Fact]
    public void Update_ComposesChildLocalBeforeAsymmetricParent_AndKeepsIndependentRoots()
    {
        using var world = new World();
        var child = Node(world, new(new(13, 17, 19), new(1, 0, 0, 0), new(3, 5, 7)));
        var parent = Node(world, new(new(5, -7, 11), new(.5f, .5f, .5f, .5f), new(2, 3, 4)));
        var independent = Node(world, LocalTransform.Identity with { Position = new(-2, -3, -5) });
        world.SetComponent(child, new TransformParent(parent));
        using var system = new TransformSystem(world); system.Update(23);
        var expected = new Matrix4x4(0, 6, 0, 0, 0, 0, -15, 0, -28, 0, 0, 0, 81, 19, 62, 1);
        Assert.Equal(new WorldTransform(expected, 23), world.GetComponent<WorldTransform>(child));
        Assert.Equal(new Vector3(53, 25, 47), Vector3.Transform(Vector3.One, world.GetComponent<WorldTransform>(child).Matrix));
        Assert.Equal(new Vector3(-2, -3, -5), world.GetComponent<WorldTransform>(independent).Matrix.Translation);
        Assert.Equal(new Vector3(5, -7, 11), world.GetComponent<WorldTransform>(parent).Matrix.Translation);
    }

    [Fact]
    public void Update_PreservesShearAndEquivalentQuaternionSigns()
    {
        using var world = new World();
        var parent = Node(world, new(new(10, 20, 30), Quaternion.Identity, new(2, 3, 4)));
        var local = new LocalTransform(new(5, 7, 11), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4), Vector3.One);
        var child = Node(world, local, parent); var twin = Node(world, local with { Rotation = -local.Rotation }, parent);
        using var system = new TransformSystem(world); system.Update(1);
        var matrix = world.GetComponent<WorldTransform>(child).Matrix;
        MatrixNear(new(1.41421356f, 2.12132034f, 0, 0, -1.41421356f, 2.12132034f, 0, 0,
            0, 0, 4, 0, 20, 41, 74, 1), matrix);
        Assert.InRange(Vector3.Dot(new(matrix.M11, matrix.M12, matrix.M13), new(matrix.M21, matrix.M22, matrix.M23)), 2.4999f, 2.5001f);
        Assert.Equal(matrix, world.GetComponent<WorldTransform>(twin).Matrix);
        var point = Vector3.Transform(new(2, -3, 5), matrix);
        Assert.InRange(point.X, 27.0709f, 27.0712f); Assert.InRange(point.Y, 38.8785f, 38.8789f); Assert.Equal(94, point.Z);
    }

    [Fact]
    public void Update_EvaluatesTenThousandDeepReverseIndexChainWithoutRecursion()
    {
        using var world = new World();
        const int count = 10000;
        var nodes = Enumerable.Range(0, count).Select(_ => Node(world, LocalTransform.Identity with { Position = new(1, -2, 3) })).ToArray();
        for (var i = 0; i < count - 1; i++) world.SetComponent(nodes[i], new TransformParent(nodes[i + 1]));
        using var system = new TransformSystem(world, count); system.Update(42);
        Assert.Equal(new Vector3(10000, -20000, 30000), world.GetComponent<WorldTransform>(nodes[0]).Matrix.Translation);
        Assert.Equal(new Vector3(5000, -10000, 15000), world.GetComponent<WorldTransform>(nodes[5000]).Matrix.Translation);
        Assert.Equal(new Vector3(1, -2, 3), world.GetComponent<WorldTransform>(nodes[^1]).Matrix.Translation);
        Assert.All(nodes, entity => Assert.Equal(42UL, world.GetComponent<WorldTransform>(entity).Tick));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Update_RejectsEveryIncompleteTransformRoleSubset(int roles)
    {
        using var world = new World(); var entity = world.CreateEntity();
        if ((roles & 1) != 0) world.AddComponent(entity, LocalTransform.Identity);
        if ((roles & 2) != 0) world.AddComponent(entity, new TransformParent());
        if ((roles & 4) != 0) world.AddComponent(entity, new WorldTransform(Matrix4x4.Identity, 99));
        using var system = new TransformSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(1));
        Assert.Equal(TransformEvaluationError.IncompleteNode, error.Error); Assert.Equal(entity, error.Entity);
        Assert.Null(system.LastTick); Assert.False(world.IsFaulted);
        if ((roles & 4) != 0) Assert.Equal(99UL, world.GetComponent<WorldTransform>(entity).Tick);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("stale")]
    [InlineData("reused")]
    [InlineData("nonnode")]
    public void Update_RejectsNonliveForeignAndNonnodeParentsWithoutPublishing(string kind)
    {
        using var world = new World(); using var foreign = new World();
        var child = Node(world, LocalTransform.Identity);
        var parent = kind == "foreign" ? foreign.CreateEntity() : world.CreateEntity();
        if (kind is "stale" or "reused") world.DestroyEntity(parent);
        if (kind == "reused")
        {
            var replacement = Node(world, LocalTransform.Identity with { Position = new(100, 0, 0) });
            Assert.Equal(parent.Index, replacement.Index); Assert.NotEqual(parent.Generation, replacement.Generation);
        }
        world.SetComponent(child, new TransformParent(parent));
        using var system = new TransformSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(3));
        Assert.Equal(TransformEvaluationError.InvalidParent, error.Error); Assert.Equal(child, error.Entity); Assert.Equal(parent, error.Parent);
        Assert.Equal(99UL, world.GetComponent<WorldTransform>(child).Tick); Assert.Null(system.LastTick); Assert.False(world.IsFaulted);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Update_RejectsSelfAndLongCycles(int count)
    {
        using var world = new World(); var nodes = Enumerable.Range(0, count).Select(_ => Node(world, LocalTransform.Identity)).ToArray();
        for (var i = 0; i < count; i++) world.SetComponent(nodes[i], new TransformParent(nodes[(i + 1) % count]));
        using var system = new TransformSystem(world);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(4));
        Assert.Equal(TransformEvaluationError.Cycle, error.Error);
        Assert.Contains(error.Entity, nodes); Assert.Equal(world.GetComponent<TransformParent>(error.Entity).Parent, error.Parent);
        Assert.All(nodes, entity => Assert.Equal(99UL, world.GetComponent<WorldTransform>(entity).Tick));
        Assert.Null(system.LastTick);
    }

    [Fact]
    public void DestroyingParent_DoesNotCascadeOrReparent_AndNextUpdateFails()
    {
        using var world = new World(); var parent = Node(world, LocalTransform.Identity); var child = Node(world, LocalTransform.Identity, parent);
        using var system = new TransformSystem(world); system.Update(1); var old = world.GetComponent<WorldTransform>(child);
        world.DestroyEntity(parent);
        Assert.True(world.IsAlive(child)); Assert.Equal(parent, world.GetComponent<TransformParent>(child).Parent);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(2));
        Assert.Equal(TransformEvaluationError.InvalidParent, error.Error); Assert.Equal(child, error.Entity); Assert.Equal(parent, error.Parent);
        Assert.Equal(old, world.GetComponent<WorldTransform>(child)); Assert.Equal(1UL, system.LastTick);
    }

    [Fact]
    public void Update_AdmissionAtOneWorks_SecondNodeFailsAtomically_AndCanRetry()
    {
        using var world = new World(); var first = Node(world, LocalTransform.Identity);
        using var system = new TransformSystem(world, 1); system.Update(0);
        var second = Node(world, LocalTransform.Identity); var before = world.GetComponent<WorldTransform>(first);
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(1));
        Assert.Equal(TransformEvaluationError.LimitExceeded, error.Error); Assert.Equal(second, error.Entity);
        Assert.Equal(before, world.GetComponent<WorldTransform>(first)); Assert.Equal(99UL, world.GetComponent<WorldTransform>(second).Tick); Assert.Equal(0UL, system.LastTick);
        world.DestroyEntity(second); system.Update(1); Assert.Equal(1UL, system.LastTick);
    }

    [Theory]
    [InlineData("local", TransformEvaluationError.InvalidLocalTransform)]
    [InlineData("world", TransformEvaluationError.InvalidWorldTransform)]
    [InlineData("parent", TransformEvaluationError.InvalidParent)]
    [InlineData("cycle", TransformEvaluationError.Cycle)]
    public void LatePreflightFailure_PreservesEveryOldCacheAndTick_AndAllowsSameTickRetry(string kind, TransformEvaluationError expected)
    {
        using var world = new World();
        var root = Node(world, LocalTransform.Identity); var early = Node(world, LocalTransform.Identity, root); var late = Node(world, LocalTransform.Identity, root);
        var nodes = new[] { root, early, late }; using var system = new TransformSystem(world); system.Update(5);
        var old = nodes.Select(world.GetComponent<WorldTransform>).ToArray();
        world.SetComponent(root, LocalTransform.Identity with { Position = new(100, 200, 300) });
        if (kind == "local") world.SetComponent(late, default(LocalTransform));
        if (kind == "world")
        {
            world.SetComponent(root, LocalTransform.Identity with { Scale = new(1e15f, 1, 1) });
            world.SetComponent(late, LocalTransform.Identity with { Scale = new(1e30f, 1, 1) });
        }
        if (kind == "parent") world.SetComponent(late, new TransformParent(world.CreateEntity()));
        if (kind == "cycle") world.SetComponent(late, new TransformParent(late));
        var error = Assert.Throws<TransformEvaluationException>(() => system.Update(6));
        Assert.Equal(expected, error.Error); Assert.Equal(late, error.Entity);
        Assert.Equal(old, nodes.Select(world.GetComponent<WorldTransform>).ToArray()); Assert.Equal(5UL, system.LastTick); Assert.False(world.IsFaulted);
        world.SetComponent(late, LocalTransform.Identity); world.SetComponent(late, new TransformParent(root));
        system.Update(6); Assert.Equal(6UL, system.LastTick); Assert.All(nodes, entity => Assert.Equal(6UL, world.GetComponent<WorldTransform>(entity).Tick));
    }

    [Fact]
    public void PreCancelledUpdate_PreservesAllCachesAndCanRetrySameTick()
    {
        using var world = new World(); var nodes = new[] { Node(world, LocalTransform.Identity), Node(world, LocalTransform.Identity) };
        using var system = new TransformSystem(world); system.Update(0); var old = nodes.Select(world.GetComponent<WorldTransform>).ToArray();
        foreach (var entity in nodes) world.SetComponent(entity, LocalTransform.Identity with { Position = new(5, 7, 11) });
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => system.Update(1, cancellation.Token));
        Assert.Equal(old, nodes.Select(world.GetComponent<WorldTransform>).ToArray()); Assert.Equal(0UL, system.LastTick);
        system.Update(1); Assert.All(nodes, entity => Assert.Equal(new Vector3(5, 7, 11), world.GetComponent<WorldTransform>(entity).Matrix.Translation));
    }

    [Theory]
    [InlineData("system")]
    [InlineData("world")]
    [InlineData("fault")]
    public void Update_RejectsDisposedOrFaultedOwners(string kind)
    {
        using var world = new World(); var entity = Node(world, LocalTransform.Identity); using var system = new TransformSystem(world); system.Update(2);
        if (kind == "system") { system.Dispose(); system.Dispose(); }
        if (kind == "world") world.Dispose();
        if (kind == "fault") world.MarkFaulted(new InvalidOperationException("Injected owner failure"));
        if (kind == "fault") Assert.Throws<InvalidOperationException>(() => system.Update(3));
        else Assert.Throws<ObjectDisposedException>(() => system.Update(3));
        Assert.Equal(2UL, system.LastTick);
        if (kind != "world") { Assert.True(world.IsAlive(entity)); Assert.Equal(2UL, world.GetComponent<WorldTransform>(entity).Tick); }
    }

    [Fact]
    public void Execute_RejectsSchedulerContextFromAnotherWorld()
    {
        using var owner = new World(); using var foreign = new World(); using var system = new TransformSystem(owner);
        using var scheduler = EcsScheduler.Create(foreign, [new(TransformSystem.Describe(), new Factory<IEcsSystem, World>(_ => new ContextProbe(system)))]);
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(TimeSpan.FromSeconds(1)));
        Assert.Null(system.LastTick); Assert.Equal(0UL, scheduler.TickNumber); Assert.False(owner.IsFaulted); Assert.True(foreign.IsFaulted);
    }

    [Fact]
    public void InitializeScene_SeedsIdentityOnlyWhereMissingAndCopiesSceneParentEdges()
    {
        using var scene = LoadScene(); var root = scene.Entities[LoadFixture.First]; var child = scene.Entities[LoadFixture.Second];
        var rootLocal = LocalTransform.Identity with { Position = new(3, 5, 7) }; scene.World.AddComponent(root, rootLocal);
        using var system = new TransformSystem(scene.World); system.InitializeScene(scene);
        Assert.Equal(rootLocal, scene.World.GetComponent<LocalTransform>(root)); Assert.Equal(LocalTransform.Identity, scene.World.GetComponent<LocalTransform>(child));
        Assert.Equal(default, scene.World.GetComponent<TransformParent>(root).Parent); Assert.Equal(root, scene.World.GetComponent<TransformParent>(child).Parent);
        Assert.Equal(default(WorldTransform), scene.World.GetComponent<WorldTransform>(child)); Assert.Null(system.LastTick);
        Assert.Throws<InvalidOperationException>(() => system.InitializeScene(scene));
        system.Update(0); Assert.Equal(new Vector3(3, 5, 7), scene.World.GetComponent<WorldTransform>(child).Matrix.Translation);
        Assert.Throws<InvalidOperationException>(() => system.InitializeScene(scene));
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("cache")]
    [InlineData("extra-entity")]
    [InlineData("replaced-entity")]
    [InlineData("foreign")]
    [InlineData("updated")]
    public void InitializeScene_RejectsDerivedRolesOrNonfreshEntitySets(string kind)
    {
        using var scene = LoadScene(); using var other = LoadScene(); var first = scene.Entities[LoadFixture.First];
        if (kind == "parent") scene.World.AddComponent(first, new TransformParent());
        if (kind == "cache") scene.World.AddComponent(first, new WorldTransform(Matrix4x4.Identity, 18));
        if (kind == "extra-entity") scene.World.CreateEntity();
        if (kind == "replaced-entity") { scene.World.DestroyEntity(first); scene.World.CreateEntity(); }
        using var system = new TransformSystem(scene.World);
        if (kind == "updated") system.Update(0);
        Assert.Throws<InvalidOperationException>(() => system.InitializeScene(kind == "foreign" ? other : scene));
        Assert.False(scene.World.IsFaulted);
        Assert.False(scene.World.HasComponent<LocalTransform>(scene.Entities[LoadFixture.Second]));
    }

    [Fact]
    public void InitializeScene_AdmissionAndCancellationPreflightLeaveUnseededScene()
    {
        using var scene = LoadScene(); using var limited = new TransformSystem(scene.World, 1);
        Assert.Equal(TransformEvaluationError.LimitExceeded, Assert.Throws<TransformEvaluationException>(() => limited.InitializeScene(scene)).Error);
        using var system = new TransformSystem(scene.World); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => system.InitializeScene(scene, cancellation.Token));
        Assert.All(scene.Entities.Values, entity => Assert.False(scene.World.HasComponent<LocalTransform>(entity)));
        system.InitializeScene(scene); system.Update(0);
        Assert.All(scene.Entities.Values, entity => Assert.Equal(new WorldTransform(Matrix4x4.Identity, 0), scene.World.GetComponent<WorldTransform>(entity)));
    }

    [Fact]
    public void InitializeScene_InvalidAuthoredLocalIsRejectedBeforeAnyRoleIsSeeded()
    {
        using var scene = LoadScene(); var malformed = scene.Entities[LoadFixture.Second];
        scene.World.AddComponent(malformed, default(LocalTransform)); using var system = new TransformSystem(scene.World);
        Assert.Throws<InvalidDataException>(() => system.InitializeScene(scene));
        Assert.All(scene.Entities.Values, entity =>
        {
            Assert.False(scene.World.HasComponent<TransformParent>(entity));
            Assert.False(scene.World.HasComponent<WorldTransform>(entity));
        });
        Assert.Null(system.LastTick); Assert.False(scene.World.IsFaulted);
        scene.World.SetComponent(malformed, LocalTransform.Identity); system.InitializeScene(scene); system.Update(0);
        Assert.All(scene.Entities.Values, entity => Assert.Equal(new WorldTransform(Matrix4x4.Identity, 0), scene.World.GetComponent<WorldTransform>(entity)));
    }

    [Fact]
    public void InitializeScene_EmptySceneRemainsEmptyAndEvaluatesInitialZero()
    {
        using var scene = new SceneWorldLoader(new ComponentRegistryBuilder().Build(), LoadFixture.Worlds(), LoadFixture.Resolution()).Load(LoadFixture.Empty());
        using var system = new TransformSystem(scene.World); system.InitializeScene(scene); system.Update(0);
        Assert.Empty(scene.Entities); Assert.Equal((0, 0), (scene.World.EntityCount, scene.World.ComponentTypeCount)); Assert.Equal(0UL, system.LastTick);
        Assert.True(scene.IsReady); Assert.False(scene.World.IsFaulted);
    }
    internal static EntityHandle Node(World world, LocalTransform local, EntityHandle parent = default)
    {
        var entity = world.CreateEntity(); world.AddComponent(entity, local); world.AddComponent(entity, new TransformParent(parent));
        world.AddComponent(entity, new WorldTransform(Matrix4x4.CreateTranslation(-91, -92, -93), 99)); return entity;
    }

    private static SceneWorld LoadScene()
    {
        var document = new SceneDocument(LoadFixture.SceneId, 0,
            [new(LoadFixture.Second, LoadFixture.Definition, parentId: LoadFixture.First), new(LoadFixture.First, LoadFixture.Definition)],
            [new(LoadFixture.Definition, new())], []);
        return new SceneWorldLoader(new ComponentRegistryBuilder().Build(), LoadFixture.Worlds(), LoadFixture.Resolution()).Load(document);
    }

    private static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual)
    {
        var left = new[] { expected.M11, expected.M12, expected.M13, expected.M14, expected.M21, expected.M22, expected.M23, expected.M24,
            expected.M31, expected.M32, expected.M33, expected.M34, expected.M41, expected.M42, expected.M43, expected.M44 };
        var right = new[] { actual.M11, actual.M12, actual.M13, actual.M14, actual.M21, actual.M22, actual.M23, actual.M24,
            actual.M31, actual.M32, actual.M33, actual.M34, actual.M41, actual.M42, actual.M43, actual.M44 };
        for (var i = 0; i < left.Length; i++) Assert.InRange(right[i], left[i] - .0001f, left[i] + .0001f);
    }

    private sealed class ContextProbe(TransformSystem target) : IEcsSystem
    {
        public void Execute(EcsSystemContext context) => target.Execute(context);
        public void Dispose() { }
    }
}
