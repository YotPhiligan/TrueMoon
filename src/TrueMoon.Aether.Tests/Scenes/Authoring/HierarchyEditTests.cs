using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;
using static TrueMoon.Aether.Tests.Scenes.Authoring.AuthoringTestData;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class HierarchyEditTests
{
    [Fact]
    public void NamesAndPlacement_CommitTogetherAndShareHistoryWithComponentEdits()
    {
        using var session = Session(Document(count: 4, parented: false)); var before = session.Read();
        var batch = new HierarchyEditBatch(Operation(7), [new(First, "Моб 🛰️"), new(Second, "Моб 🛰️")],
            [new(Entity(4), null, EntityPlacement.Before, First)]);
        Assert.True(session.EditHierarchy(batch).Succeeded); var named = session.Read();
        Assert.Equal(8, named.Document.Revision); Assert.Equal(1, named.UndoCount);
        Assert.Equal(new[] { Entity(4), First, Second, Entity(3) }, named.Resolved.Roots);
        Assert.Equal(new[] { First, Second, Entity(3), Entity(4) }, named.Document.Entities.Select(entity => entity.Id));
        Assert.Equal("Моб 🛰️", named.Resolved.Entities[First].Name); Assert.Equal(1, named.Resolved.Entities[First].SiblingOrder);
        Assert.Same(before.Document.Entities[0].Changes, named.Document.Entities[0].Changes);
        Assert.True(session.Apply(Batch(8, 42)).Succeeded);
        Assert.True(session.Reparent(new(Operation(9), [new(Second, First)])).Succeeded);
        Assert.Equal("Моб 🛰️", session.Read().Resolved.Entities[First].Name);
        Assert.Equal(1, session.Read().Resolved.Entities[First].SiblingOrder);
        Assert.Equal("Моб 🛰️", session.Read().Resolved.Entities[Second].Name);
        Assert.True(session.Undo(Operation(10)).Succeeded); Assert.True(session.Undo(Operation(11)).Succeeded);
        Assert.Equal(3, Number(session.Read(), First, "x"));
        Assert.True(session.Undo(Operation(12)).Succeeded);
        Assert.Null(session.Read().Resolved.Entities[First].Name); Assert.Equal(before.Resolved.Roots.ToArray(), session.Read().Resolved.Roots);
        Assert.True(session.Redo(Operation(13)).Succeeded);
        Assert.Equal(named.Resolved.Roots.ToArray(), session.Read().Resolved.Roots); Assert.Equal("Моб 🛰️", session.Read().Resolved.Entities[First].Name);
        Assert.Null(before.Resolved.Entities[First].Name);
    }

    [Fact]
    public void Placement_ReparentsWithSubtreeThenOrdersRelativeToUpdatedSibling()
    {
        using var session = Session(Document(count: 5));
        Assert.True(session.EditHierarchy(new(Operation(7), placements:
            [new(Second, null), new(Entity(3), null, EntityPlacement.Before, Second), new(Entity(4), Second), new(Entity(5), Second, EntityPlacement.Before, Entity(4))])).Succeeded);
        var state = session.Read();
        Assert.Equal(new[] { First, Entity(3), Second }, state.Resolved.Roots);
        Assert.Equal(new[] { Entity(5), Entity(4) }, state.Resolved.Children[Second]);
        Assert.Equal(new[] { First, Entity(3), Second, Entity(5), Entity(4) }, state.Resolved.HierarchyOrder);
        Assert.Equal(3, Number(state, First, "x")); Assert.Equal(0, Number(state, Second, "x"));
    }

    [Theory]
    [InlineData("")] [InlineData("  ")] [InlineData("bad\nname")] [InlineData("bad\tname")]
    [InlineData("bad\u2028name")] [InlineData("bad\u2029name")] [InlineData("bad\u0000name")]
    public void InvalidName_RejectsWholeBatchAndPreservesState(string name)
    {
        using var session = Session(); var before = session.Read();
        var result = session.EditHierarchy(new(Operation(7), [new(First, "valid"), new(Second, name)], [new(Second, null)]));
        Assert.Equal(SceneAuthoringError.InvalidName, result.Error); Assert.EndsWith("/name", result.Path);
        Assert.Same(before, session.Read());
    }

    [Fact]
    public void NameBoundaries_RejectBrokenUnicodeAndOversizeAndAllowClear()
    {
        using var session = Session(); var before = session.Read();
        foreach (var text in new[] { new string('x', 129), "\ud800", "\udc00", "\ud800x" })
        {
            Assert.Equal(SceneAuthoringError.InvalidName, session.EditHierarchy(new(Operation(7), [new(First, text)])).Error);
            Assert.Throws<ArgumentException>(() => new EntityInstance(First, Definition, EntityPatch.Empty, null, text, null));
        }
        Assert.Same(before, session.Read());
        Assert.True(session.EditHierarchy(new(Operation(7), [new(First, new string('x', 128))])).Succeeded);
        Assert.True(session.EditHierarchy(new(Operation(8), [new(First, null)])).Succeeded);
        Assert.Null(session.Read().Resolved.Entities[First].Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityInstance(First, Definition, EntityPatch.Empty, null, null, -1));
    }

    [Theory]
    [InlineData("cycle")] [InlineData("missing")] [InlineData("self-anchor")] [InlineData("wrong-parent")]
    [InlineData("duplicate-name")] [InlineData("duplicate-placement")] [InlineData("missing-anchor")]
    [InlineData("extra-anchor")] [InlineData("invalid-placement")]
    public void InvalidHierarchy_RollsBackNamesOrderAndHistory(string scenario)
    {
        using var session = Session(Document(count: 3)); var before = session.Read();
        var placement = scenario switch
        {
            "cycle" => new EntityPlacementEdit(First, Second),
            "missing" => new(Second, Entity(99)),
            "self-anchor" => new(Second, First, EntityPlacement.Before, Second),
            "wrong-parent" => new(Entity(3), null, EntityPlacement.Before, Second),
            "missing-anchor" => new(Second, null, EntityPlacement.Before),
            "extra-anchor" => new(Second, null, EntityPlacement.Last, First),
            "invalid-placement" => new(Second, null, (EntityPlacement)99),
            _ => new(Second, null)
        };
        var result = session.EditHierarchy(new(Operation(7), scenario == "duplicate-name" ? [new(First, "A"), new(First, "B")] : [new(First, "A")],
            scenario == "duplicate-placement" ? [placement, placement] : [placement]));
        Assert.False(result.Succeeded); Assert.Same(before, session.Read());
        Assert.Equal(0, session.Read().UndoCount); Assert.Null(session.Read().Resolved.Entities[First].Name);
    }

    [Fact]
    public void ReceiptsRevisionCancellationAndBatchBounds_UseSharedAdmission()
    {
        using var session = Session(); var operation = Operation(7);
        var batch = new HierarchyEditBatch(operation, [new(First, "A")]); var result = session.EditHierarchy(batch);
        Assert.Same(result, session.EditHierarchy(new(operation, [new(First, "A")])));
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.EditHierarchy(new(operation, [new(First, "B")])).Error);
        Assert.Equal(SceneAuthoringError.OperationIdConflict, session.Apply(new(operation, [Edit(First, 1)])).Error);
        Assert.Equal(SceneAuthoringError.RevisionConflict, session.EditHierarchy(new(Operation(7), [new(First, "B")])).Error);
        var before = session.Read(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.EditHierarchy(new(Operation(8), [new(First, "B")]), cancel.Token));
        Assert.Same(before, session.Read());
        Assert.Throws<ArgumentOutOfRangeException>(() => new HierarchyEditBatch(Operation(8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HierarchyEditBatch(Operation(8), Enumerable.Repeat(new EntityNameEdit(First, "A"), 4097)));
        using var limited = Session(options: new() { MaxBatchEdits = 1 });
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, limited.EditHierarchy(new(Operation(7), [new(First, "A")], [new(Second, null)])).Error);
    }

    [Fact]
    public void Resolver_MissingAndEqualRanksUseStableIdsAndDoNotDependOnDeclarationOrder()
    {
        var source = Document(count: 4, parented: false);
        var entities = source.Entities.Reverse().Select(entity => new EntityInstance(entity.Id, entity.DefinitionId, entity.Changes, null,
            entity.Id == First ? "Named" : null, entity.Id == Entity(3) ? 2 : entity.Id == Second ? 0 : null));
        var resolved = SceneResolver.Resolve(new(source.Id, source.Revision, entities, source.Definitions, source.Schemas));
        Assert.Equal(new[] { First, Second, Entity(4), Entity(3) }, resolved.Roots);
        Assert.Equal("Named", resolved.Entities[First].Name);
    }
}
