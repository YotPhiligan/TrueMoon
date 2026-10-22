using Game.Logic;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes;

public sealed class SceneSnapshotAdapterTests
{
    [Fact]
    public void Projection_PreservesCanonicalIdsRevisionOrderAndExplicitWorldSpacePolicy()
    {
        var source = SceneResolver.Resolve(SceneFixture.Forest());
        var visited = new List<EntityId>();
        var projected = SceneSnapshotAdapter.Project(source, (scene, entity) => { visited.Add(entity.Id); return WorldProjection(scene, entity); });
        Assert.Equal(source.HierarchyOrder.AsEnumerable(), visited);
        Assert.Equal(source.Id.Value.ToString("D"), projected.Id); Assert.Equal(source.Revision, projected.Revision);
        var entities = projected.CopyEntities();
        Assert.Equal(source.HierarchyOrder.Select(id => id.Value.ToString("D")), entities.Select(e => e.Id));
        var child = entities.Single(e => e.Id == ModelFixture.First.Value.ToString("D"));
        var grandchild = entities.Single(e => e.Id == SceneFixture.Grandchild.Value.ToString("D"));
        Assert.Equal(6, child.X); Assert.Equal(7, grandchild.X);
        Assert.True(child.Interactive); Assert.False(grandchild.Interactive);
        Assert.Equal(0, child.Y); Assert.Equal(-2, child.Z);
        child.X = 999; entities.Clear();
        Assert.Equal(6, projected.CopyEntities().Single(e => e.Id == ModelFixture.First.Value.ToString("D")).X);
        Assert.Equal(5, ModelFixture.Property(source.Entities[ModelFixture.First], ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Equal(ModelFixture.Second, source.Entities[ModelFixture.First].ParentId);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public void ExistingEntityLimit_IsCheckedBeforeAnyProjectionAndDoesNotLimitAuthoringScenes(int count)
    {
        var document = SceneFixture.Document(Enumerable.Range(0, count).Select(i => new EntityInstance(new(ModelFixture.Guid(20000 + i)), ModelFixture.Door)),
            [new(ModelFixture.Door, EntityPatch.Empty)], []);
        var source = SceneResolver.Resolve(document); var calls = 0;
        if (count > SceneSnapshotAdapter.MaximumEntityCount)
        {
            var error = Assert.Throws<SceneResolutionException>(() => SceneSnapshotAdapter.Project(source, (_, _) => { calls++; return default; }));
            Assert.Equal(SceneResolutionError.LegacyEntityLimitExceeded, error.Code); Assert.Null(error.EntityId);
            Assert.Equal($"scenes/{document.Id.Value:D}", error.Path); Assert.Equal(0, calls);
        }
        else
        {
            var result = SceneSnapshotAdapter.Project(source, (_, _) => { calls++; return default; });
            Assert.Equal(count, calls); Assert.Equal(count, result.CopyEntities().Count);
            Assert.All(result.CopyEntities(), e => { Assert.Equal(0, e.X); Assert.False(e.Interactive); });
        }
        Assert.Equal(count, source.Entities.Count);
    }

    [Theory]
    [InlineData("x", float.NaN)] [InlineData("x", float.PositiveInfinity)] [InlineData("x", float.NegativeInfinity)]
    [InlineData("y", float.NaN)] [InlineData("y", float.PositiveInfinity)] [InlineData("y", float.NegativeInfinity)]
    [InlineData("z", float.NaN)] [InlineData("z", float.PositiveInfinity)] [InlineData("z", float.NegativeInfinity)]
    public void NonFiniteWorldCoordinates_ReportEntityAndOriginalValidationCause(string axis, float value)
    {
        var source = SceneResolver.Resolve(SceneFixture.Document([new(ModelFixture.First, ModelFixture.Door)]));
        var projection = axis switch { "x" => new SceneEntityProjection(value, 0, 0, false), "y" => new(0, value, 0, false), _ => new(0, 0, value, false) };
        var error = Assert.Throws<SceneResolutionException>(() => SceneSnapshotAdapter.Project(source, (_, _) => projection));
        Assert.Equal(SceneResolutionError.ProjectionFailed, error.Code); Assert.Equal(ModelFixture.First, error.EntityId);
        Assert.Equal($"scenes/{source.Id.Value:D}/entities/{ModelFixture.First.Value:D}/projection", error.Path);
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(1, ModelFixture.Property(source.Entities[ModelFixture.First], ModelFixture.Transform, "x").Value.GetNumber());
        Assert.Single(SceneSnapshotAdapter.Project(source, (_, _) => default).CopyEntities());
    }

    [Fact]
    public void ProjectorFailure_PreservesOriginalCauseAndPublishesNoPartialResult()
    {
        var source = SceneResolver.Resolve(SceneFixture.Forest()); var failure = new InvalidOperationException("projector failed");
        var visited = new List<EntityId>(); SceneSnapshot? published = null;
        var error = Assert.Throws<SceneResolutionException>(() => published = SceneSnapshotAdapter.Project(source, (scene, entity) =>
        {
            visited.Add(entity.Id); if (entity.Id == source.HierarchyOrder[^1]) throw failure;
            return WorldProjection(scene, entity);
        }));
        Assert.Null(published); Assert.Same(failure, error.InnerException);
        Assert.Equal(SceneResolutionError.ProjectionFailed, error.Code); Assert.Equal(source.HierarchyOrder[^1], error.EntityId);
        Assert.Equal(source.HierarchyOrder.AsEnumerable(), visited);
        Assert.Equal(5, source.Entities.Count);
        var recovered = SceneSnapshotAdapter.Project(source, WorldProjection);
        Assert.Equal(5, recovered.CopyEntities().Count);
        Assert.Equal(6, recovered.CopyEntities().Single(e => e.Id == ModelFixture.First.Value.ToString("D")).X);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(5)]
    public void Cancellation_BeforeOrDuringProjectionNeverPublishesOrCallsLaterEntities(int cancelAt)
    {
        var source = SceneResolver.Resolve(SceneFixture.Forest()); using var cancellation = new CancellationTokenSource(); var calls = 0;
        if (cancelAt == 0) cancellation.Cancel(); SceneSnapshot? published = null;
        var error = Assert.Throws<OperationCanceledException>(() => published = SceneSnapshotAdapter.Project(source, (scene, entity) =>
        {
            if (++calls == cancelAt) cancellation.Cancel(); return WorldProjection(scene, entity);
        }, cancellation.Token));
        Assert.Null(published); Assert.Equal(cancellation.Token, error.CancellationToken); Assert.Equal(cancelAt, calls);
        Assert.Equal(5, SceneSnapshotAdapter.Project(source, WorldProjection).CopyEntities().Count);
    }

    [Fact]
    public void ProjectorCancellation_IsNotWrappedAsAnAuthoringFailure()
    {
        var source = SceneResolver.Resolve(SceneFixture.Document([new(ModelFixture.First, ModelFixture.Door)]));
        var cause = new OperationCanceledException("caller cancellation");
        var error = Assert.Throws<OperationCanceledException>(() => SceneSnapshotAdapter.Project(source, (_, _) => throw cause));
        Assert.Same(cause, error); Assert.Single(source.Entities);
    }

    [Fact]
    public void Projection_RemainsLoadableByExistingGameAndCheckpointContract()
    {
        var source = SceneResolver.Resolve(SceneFixture.Forest());
        var projected = SceneSnapshotAdapter.Project(source, WorldProjection);
        using var game = new ProbeGame(); game.Load(projected);
        Assert.Equal(7, game.Revision); Assert.Equal(0UL, game.TickNumber); Assert.False(game.CaptureCheckpoint().Interaction);
        game.Tick(); game.Tick(); game.Tick();
        var checkpoint = game.CaptureCheckpoint(); Assert.True(checkpoint.Interaction); Assert.Equal(3UL, checkpoint.Tick);
        game.Load(SceneSnapshotAdapter.Project(source, (_, _) => default));
        Assert.Equal(0UL, game.TickNumber); game.Tick(); game.Tick(); game.Tick(); Assert.False(game.CaptureCheckpoint().Interaction);
        Assert.Equal(7, game.Revision);
    }

    [Fact]
    public void FiniteCoordinateBoundariesAndNullContracts_AreExplicit()
    {
        var source = SceneResolver.Resolve(SceneFixture.Document([new(ModelFixture.First, ModelFixture.Door)], revision: long.MaxValue));
        var result = SceneSnapshotAdapter.Project(source, (_, _) => new(float.MaxValue, -float.MaxValue, float.Epsilon, true));
        var entity = Assert.Single(result.CopyEntities());
        Assert.Equal(float.MaxValue, entity.X); Assert.Equal(-float.MaxValue, entity.Y); Assert.Equal(float.Epsilon, entity.Z);
        Assert.True(entity.Interactive); Assert.Equal(long.MaxValue, result.Revision);
        Assert.Throws<ArgumentNullException>(() => SceneSnapshotAdapter.Project(null!, (_, _) => default));
        Assert.Throws<ArgumentNullException>(() => SceneSnapshotAdapter.Project(source, null!));
    }

    private static SceneEntityProjection WorldProjection(ResolvedSceneSnapshot scene, ResolvedEntity entity)
    {
        var worldX = 0d; var current = entity;
        while (true)
        {
            worldX += ModelFixture.Property(current, ModelFixture.Transform, "x").Value.GetNumber();
            if (current.ParentId is not { } parent) break; current = scene.Entities[parent];
        }
        return new((float)worldX, (float)ModelFixture.Property(entity, ModelFixture.Transform, "y").Value.GetNumber(), -2,
            ModelFixture.Color(entity) == "red");
    }
}
