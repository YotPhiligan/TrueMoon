using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class SceneAuthoringSessionTests
{
    [Fact]
    public void Apply_PublishesCoherentNinePropertyBatchByStableIdsWithoutMutatingEarlierSnapshots()
    {
        var source = Document();
        using var session = Session(source);
        var before = session.Read();
        var firstValue = new TransformValue(10, -2, 3, .1, .2, .3, 2, 3, 4);
        var secondValue = new TransformValue(-4, 5, 6, -.1, -.2, -.3, .5, .75, 1.25);
        var operation = Operation(7);
        var result = session.Apply(new(operation, [new(Second, Transform, secondValue), new(First, Transform, firstValue)]));
        Assert.True(result.Succeeded); Assert.Equal(SceneAuthoringError.None, result.Error);
        Assert.Same(operation, result.Operation); Assert.Equal(8, result.Revision); Assert.Equal(8, result.ContentRevision);
        Assert.Empty(result.Path); Assert.Empty(result.Message);
        var after = session.Read();
        Assert.NotSame(before, after); Assert.NotSame(source, after.Document);
        Assert.Equal(source.Id, after.Document.Id); Assert.Equal(source.Id, after.Resolved.Id);
        Assert.Equal(8, after.Document.Revision); Assert.Equal(8, after.Resolved.Revision);
        Assert.Equal(1, after.UndoCount); Assert.Equal(0, after.RedoCount);
        AssertTransform(after, First, firstValue); AssertTransform(after, Second, secondValue);
        var entity = after.Document.Entities.Single(e => e.Id == First);
        Assert.Equal(9, entity.Changes.PropertyOverrides.Count(p => p.ComponentId == Transform));
        Assert.Equal(new[] { Custom }, entity.Changes.AddedComponents.Select(c => c.Id));
        Assert.Contains(Interaction, entity.Changes.RemovedComponents);
        Assert.Equal("keep", after.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal(.25, after.Resolved.Entities[First].Components[Appearance].Properties["g"].Value.GetNumber());
        Assert.Equal(First, after.Document.Entities.Single(e => e.Id == Second).ParentId);
        Assert.Equal(source.Definitions.AsEnumerable(), after.Document.Definitions.AsEnumerable());
        Assert.Equal(source.Schemas.AsEnumerable(), after.Document.Schemas.AsEnumerable());
        Assert.Same(source, before.Document); Assert.Equal(7, before.Resolved.Revision); Assert.Equal(3, Number(before, First, "x"));
        Assert.Equal(1, source.Entities.First().Changes.PropertyOverrides.Count(p => p.ComponentId == Transform));
    }

    [Theory]
    [InlineData("missing-entity", SceneAuthoringError.MissingEntity)]
    [InlineData("default-entity", SceneAuthoringError.MissingEntity)]
    [InlineData("missing-component", SceneAuthoringError.MissingComponent)]
    [InlineData("default-component", SceneAuthoringError.MissingComponent)]
    [InlineData("hidden-component", SceneAuthoringError.MissingComponent)]
    [InlineData("unsupported", SceneAuthoringError.UnsupportedComponent)]
    [InlineData("duplicate", SceneAuthoringError.DuplicateTarget)]
    public void Apply_InvalidSecondTargetRejectsEntireBatchBeforeResolution(string invalid, SceneAuthoringError expected)
    {
        var resolutions = 0;
        using var session = Session(beforeCreate: _ => resolutions++);
        var before = session.Read();
        var invalidEdit = invalid switch
        {
            "missing-entity" => new TransformEdit(Entity(99), Transform, new(9, 0, 0)),
            "default-entity" => new(default, Transform, new(9, 0, 0)),
            "missing-component" => new(Second, new(Id(999)), new(9, 0, 0)),
            "default-component" => new(Second, default, new(9, 0, 0)),
            "hidden-component" => new(First, Interaction, new(9, 0, 0)),
            "unsupported" => new(Second, Appearance, new(9, 0, 0)),
            _ => Edit(First, 22)
        };
        var result = session.Apply(new(Operation(7), [Edit(First, 11), invalidEdit]));
        Assert.False(result.Succeeded); Assert.Equal(expected, result.Error);
        Assert.Equal(7, result.Revision); Assert.Contains("/entities/", result.Path); Assert.NotEmpty(result.Message);
        Assert.Same(before, session.Read()); Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.Equal(1, resolutions);
    }

    [Theory]
    [InlineData("nan")] [InlineData("infinity")] [InlineData("overflow")]
    [InlineData("zero-scale")] [InlineData("negative-scale")] [InlineData("underflow-scale")]
    public void Apply_InvalidTransformRejectsWithoutPartialPublish(string invalid)
    {
        using var session = Session(); var before = session.Read();
        var value = invalid switch
        {
            "nan" => new TransformValue(double.NaN, 0, 0),
            "infinity" => new(0, 0, 0, Yaw: double.PositiveInfinity),
            "overflow" => new(double.MaxValue, 0, 0),
            "zero-scale" => new(0, 0, 0, ScaleY: 0),
            "negative-scale" => new(0, 0, 0, ScaleZ: -1),
            _ => new(0, 0, 0, ScaleX: 1e-100)
        };
        var result = session.Apply(new(Operation(7), [Edit(First, 11), new(Second, Transform, value)]));
        Assert.Equal(SceneAuthoringError.InvalidTransform, result.Error); Assert.Same(before, session.Read());
        Assert.Contains(Second.Value.ToString("D"), result.Path); Assert.Equal(3, Number(before, First, "x"));
    }

    [Fact]
    public void Apply_WholeHierarchyValidationRejectsLocallyFiniteOverflowAtomically()
    {
        using var session = Session(); var before = session.Read();
        var batch = new TransformEditBatch(Operation(7), [Edit(First, float.MaxValue), Edit(Second, float.MaxValue)]);
        var rejected = session.Apply(batch);
        Assert.Equal(SceneAuthoringError.InvalidScene, rejected.Error); Assert.False(rejected.Succeeded);
        Assert.NotEmpty(rejected.Path); Assert.NotEmpty(rejected.Message); Assert.Same(before, session.Read());
        Assert.Same(rejected, session.Apply(batch));
        Assert.True(session.Apply(Batch(7, 20)).Succeeded); Assert.Equal(20, Number(session.Read(), First, "x"));
    }

    [Fact]
    public void Apply_FinitePositiveScalesWithSingularFloatDeterminantRejectAtomically()
    {
        using var session = Session(); var before = session.Read();
        var value = new TransformValue(0, 0, 0, ScaleX: 1e-20, ScaleY: 1e-20, ScaleZ: 1e-20);
        var rejected = session.Apply(new(Operation(7), [Edit(First, 11), new(Second, Transform, value)]));
        Assert.Equal(SceneAuthoringError.InvalidScene, rejected.Error);
        Assert.Contains("Singular", rejected.Message); Assert.Same(before, session.Read());
        Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.True(session.Apply(Batch(7, 22)).Succeeded);
    }

    [Fact]
    public void Resolution_ExactTransformSchemaRejectsFailedCreationWithoutPoisoningLaterValidEditing()
    {
        var source = Document(); var original = source.Schemas.Single(s => s.TypeId == BuiltInComponents.TransformId);
        var changed = new ComponentSchema(original.TypeId, original.Version,
            [.. original.Properties.Values, new("extra", PropertyValueKind.Number, PropertyValue.From(0d))]);
        var schemas = source.Schemas.Select(s => s.TypeId == changed.TypeId ? changed : s).ToArray();
        var malformed = new SceneDocument(source.Id, source.Revision, source.Entities, source.Definitions, schemas);
        var factory = new SceneAuthoringSessionFactory(Resolution(), new());
        var error = Assert.Throws<InvalidDataException>(() => factory.Create(malformed));
        Assert.Contains("schema", error.Message);
        using var session = factory.Create(source);
        Assert.True(session.Apply(Batch(7, 44)).Succeeded); Assert.Equal(44, Number(session.Read(), First, "x"));
        Assert.Same(original, session.Read().Document.Schemas.Single(s => s.TypeId == BuiltInComponents.TransformId));
    }

    [Fact]
    public void Apply_EnforcesConfiguredBatchLimitBeforeAnyCandidateResolution()
    {
        var resolutions = 0;
        using var session = Session(options: new() { MaxBatchEdits = 1 }, beforeCreate: _ => resolutions++);
        var before = session.Read();
        var rejected = session.Apply(new(Operation(7), [Edit(First, 1), Edit(Second, 2)]));
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, rejected.Error); Assert.Same(before, session.Read()); Assert.Equal(1, resolutions);
        Assert.True(session.Apply(Batch(7, 1)).Succeeded); Assert.Equal(2, resolutions);
    }

    [Fact]
    public void Apply_HeadlessAuthoringDoesNotImposePreviewEntityOrMaterialBounds()
    {
        var source = Document(count: 65, parented: false);
        var first = source.Entities[0];
        var changed = new EntityInstance(first.Id, first.DefinitionId, new(propertyOverrides: [new(Appearance, "r", PropertyValue.From(2d))]));
        source = new(source.Id, source.Revision, new[] { changed }.Concat(source.Entities.Skip(1)), source.Definitions, source.Schemas);
        using var session = Session(source);
        var result = session.Apply(new(Operation(7), [Edit(Entity(65), -10)]));
        Assert.True(result.Succeeded); Assert.Equal(65, session.Read().Resolved.Entities.Count);
        Assert.Equal(-10, Number(session.Read(), Entity(65), "x"));
        Assert.Equal(2, session.Read().Resolved.Entities[First].Components[Appearance].Properties["r"].Value.GetNumber());
    }

    [Theory]
    [InlineData("actor")] [InlineData("revision")] [InlineData("payload")] [InlineData("order")] [InlineData("kind")]
    public void Operations_ExactRetryReturnsOriginalReceiptButChangedIntentConflicts(string changed)
    {
        using var session = Session();
        var operation = Operation(7);
        var batch = new TransformEditBatch(operation, [Edit(First, 10), Edit(Second, 20)]);
        var receipt = session.Apply(batch);
        Assert.True(receipt.Succeeded);
        Assert.True(session.Apply(Batch(8, 30)).Succeeded);
        var before = session.Read();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Same(receipt, session.Apply(new(new(operation.Id, 7, operation.Actor), batch.Edits), cancelled.Token));
        var result = changed switch
        {
            "actor" => session.Apply(new(new(operation.Id, 7, "other"), batch.Edits)),
            "revision" => session.Apply(new(new(operation.Id, 8, operation.Actor), batch.Edits)),
            "payload" => session.Apply(new(operation, [Edit(First, 11), Edit(Second, 20)])),
            "order" => session.Apply(new(operation, batch.Edits.Reverse())),
            _ => session.Undo(operation)
        };
        Assert.Equal(SceneAuthoringError.OperationIdConflict, result.Error); Assert.Same(before, session.Read());
        Assert.Equal(9, session.Read().Document.Revision); Assert.Equal(30, Number(session.Read(), First, "x"));
    }

    [Fact]
    public void Operations_RevisionConflictIsCachedAndDoesNotBecomeSuccessAfterStateCatchesUp()
    {
        using var session = Session(); var before = session.Read();
        var future = Batch(8, 99);
        var receipt = session.Apply(future);
        Assert.Equal(SceneAuthoringError.RevisionConflict, receipt.Error); Assert.Equal(7, receipt.Revision); Assert.Same(before, session.Read());
        Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        var after = session.Read();
        Assert.Same(receipt, session.Apply(future)); Assert.Same(after, session.Read()); Assert.Equal(10, Number(after, First, "x"));
    }

    [Theory]
    [InlineData("apply")] [InlineData("undo")] [InlineData("redo")]
    public void Cancellation_DuringCandidatePreparationPublishesNothingAndSameIdCanBeRetried(string command)
    {
        using var cancellation = new CancellationTokenSource(); var armed = false;
        using var session = Session(beforeCreate: _ => { if (armed) cancellation.Cancel(); });
        if (command is "undo" or "redo") Assert.True(session.Apply(Batch(7, 10)).Succeeded);
        if (command == "redo") Assert.True(session.Undo(Operation(8)).Succeeded);
        var before = session.Read(); var operation = Operation(before.Document.Revision);
        var batch = new TransformEditBatch(operation, [Edit(First, 22)]);
        SceneAuthoringResult Invoke(CancellationToken token) => command switch
        { "undo" => session.Undo(operation, token), "redo" => session.Redo(operation, token), _ => session.Apply(batch, token) };
        armed = true;
        Assert.ThrowsAny<OperationCanceledException>(() => Invoke(cancellation.Token));
        Assert.Same(before, session.Read());
        armed = false;
        var retried = Invoke(default);
        Assert.True(retried.Succeeded); Assert.Equal(before.Document.Revision + 1, retried.Revision);
        Assert.Equal(command == "undo" ? 3 : command == "redo" ? 10 : 22, Number(session.Read(), First, "x"));
    }

    [Fact]
    public void Cancellation_PreCancelledNewIntentHasNoReceiptAndLateCancellationCannotUndoCommit()
    {
        using var session = Session(); var before = session.Read(); var batch = Batch(7, 11);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => session.Apply(batch, cancellation.Token)); Assert.Same(before, session.Read());
        var receipt = session.Apply(batch);
        using var late = new CancellationTokenSource(); late.Cancel();
        Assert.True(receipt.Succeeded); Assert.Same(receipt, session.Apply(batch, late.Token));
        Assert.Equal(8, session.Read().Document.Revision); Assert.Equal(11, Number(session.Read(), First, "x"));
    }

    [Fact]
    public void Preparation_UnexpectedFailurePreservesExceptionIdentityStateAndRetryableOperationId()
    {
        var expected = new IOException("resolver infrastructure failure"); var armed = false;
        using var session = Session(beforeCreate: _ => { if (armed) throw expected; });
        var before = session.Read(); var batch = Batch(7, 40); armed = true;
        Assert.Same(expected, Assert.Throws<IOException>(() => session.Apply(batch))); Assert.Same(before, session.Read());
        armed = false; Assert.True(session.Apply(batch).Succeeded); Assert.Equal(40, Number(session.Read(), First, "x"));
    }

    [Fact]
    public async Task ConcurrentSameRevision_OneAtomicBatchWinsAndOneConflicts()
    {
        using var session = Session(); var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstBatch = Batch(7, 10); var secondBatch = Batch(7, 20);
        var first = Task.Run(async () => { await start.Task; return session.Apply(firstBatch); });
        var second = Task.Run(async () => { await start.Task; return session.Apply(secondBatch); });
        start.SetResult(); var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        var winner = Assert.Single(results, r => r.Succeeded); Assert.Single(results, r => r.Error == SceneAuthoringError.RevisionConflict);
        var after = session.Read(); Assert.Equal(8, after.Document.Revision); Assert.Equal(1, after.UndoCount);
        Assert.Equal(winner.Operation.Id == firstBatch.Operation.Id ? 10 : 20, Number(after, First, "x"));
    }

    [Fact]
    public void Apply_InstanceAddedTransformRetainsAddedDataAndRestoredComponentPresence()
    {
        var root = new EntityDefinition(Definition, new(addedComponents: [new(Custom, CustomType, 1)]));
        var variantId = new EntityDefinitionId(Id(11));
        var variant = new EntityDefinition(variantId, new(removedComponents: [Custom]), Definition);
        var local = new ComponentData(Transform, BuiltInComponents.TransformId, 1, [new("x", PropertyValue.From(9d))]);
        var instance = new EntityInstance(First, variantId, new(addedComponents: [local], restoredComponents: [Custom],
            propertyOverrides: [new(Custom, "note", PropertyValue.From("restored"))]));
        var document = new SceneDocument(new(Id(500)), 7, [instance], [root, variant], Document().Schemas);
        using var session = Session(document);
        Assert.Equal(9, Number(session.Read(), First, "x"));
        Assert.True(session.Apply(Batch(7, 22)).Succeeded);
        var after = session.Read(); var patch = Assert.Single(after.Document.Entities).Changes;
        Assert.Same(local, Assert.Single(patch.AddedComponents));
        Assert.Contains(Custom, patch.RestoredComponents);
        Assert.Equal("restored", after.Resolved.Entities[First].Components[Custom].Properties["note"].Value.GetString());
        Assert.Equal(22, Number(after, First, "x"));
        Assert.Equal(9, patch.PropertyOverrides.Count(p => p.ComponentId == Transform));
        Assert.True(session.Undo(Operation(8)).Succeeded); Assert.Equal(9, Number(session.Read(), First, "x"));
    }

    [Fact]
    public void Preparation_ReadObservesPreviousCommitAndReentrantMutationOrDisposalIsRejected()
    {
        ISceneAuthoringSession? session = null; SceneAuthoringState? before = null; var armed = false; var observed = 0;
        session = Session(beforeCreate: _ =>
        {
            if (!armed) return;
            observed++; Assert.Same(before, session!.Read()); Assert.Equal(3, Number(session.Read(), First, "x"));
            Assert.Throws<InvalidOperationException>(() => session.Undo(Operation(7)));
            Assert.Throws<InvalidOperationException>(() => session.Dispose());
        });
        using (session)
        {
            before = session.Read(); armed = true;
            Assert.True(session.Apply(Batch(7, 10)).Succeeded);
            Assert.Equal(1, observed); Assert.Equal(10, Number(session.Read(), First, "x"));
            Assert.Equal(8, session.Read().Document.Revision); Assert.Equal(7, before.Document.Revision);
        }
    }

    [Fact]
    public void EmptyScene_ReadIsCoherentAndMissingTargetDoesNotCreateContent()
    {
        using var session = Session(Document(0, 0)); var before = session.Read();
        Assert.Empty(before.Document.Entities); Assert.Empty(before.Resolved.Entities);
        Assert.Equal(0, before.Document.Revision); Assert.Equal(0, before.Resolved.Revision); Assert.Equal(0, before.ContentRevision);
        Assert.Equal(SceneAuthoringError.MissingEntity, session.Apply(Batch(0, 10)).Error);
        Assert.Equal(SceneAuthoringError.NoUndo, session.Undo(Operation(0)).Error);
        Assert.Same(before, session.Read());
    }

    private static void AssertTransform(SceneAuthoringState state, EntityId entity, TransformValue value)
    {
        var expected = new Dictionary<string, double> { ["x"] = value.X, ["y"] = value.Y, ["z"] = value.Z,
            ["pitch"] = value.Pitch, ["yaw"] = value.Yaw, ["roll"] = value.Roll, ["sx"] = value.ScaleX, ["sy"] = value.ScaleY, ["sz"] = value.ScaleZ };
        Assert.Equal(expected.OrderBy(p => p.Key), state.Resolved.Entities[entity].Components[Transform].Properties
            .Select(p => new KeyValuePair<string, double>(p.Key, p.Value.Value.GetNumber())).OrderBy(p => p.Key));
        Assert.All(state.Resolved.Entities[entity].Components[Transform].Properties.Values, p =>
        { Assert.Equal(entity, p.Source.EntityId); Assert.False(p.IsSchemaDefault); });
    }
}
