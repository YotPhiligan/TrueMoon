using System.Numerics;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Translation;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Translation;

public sealed class TranslationTargetTests
{
    [Fact]
    public void InverseParent_RotatedNonuniformScaleIgnoresTranslationAndChildRotationScale()
    {
        var source = DocumentData.Scene();
        var parent = new EntityInstance(DocumentData.Parent, DocumentData.Base, new(propertyOverrides:
        [Set("x", 10), Set("y", 20), Set("z", 30), Set("roll", Math.PI / 2), Set("sx", 2), Set("sy", 3), Set("sz", 4)]));
        var child = new EntityInstance(DocumentData.Child, DocumentData.Restored, new(propertyOverrides:
        [Set("x", 1.25), Set("y", -2.5), Set("z", 3.75), Set("pitch", .4), Set("sx", .8), Set("sy", 1.2)]), DocumentData.Parent);
        using var session = DocumentData.Factory().Create(new(source.Id, source.Revision, [parent, child], source.Definitions, source.Schemas));
        var state = session.Read(); var picks = Picks(state);
        Assert.True(TranslationTarget.TryCapture(state, picks, DocumentData.Child, out var target));
        Assert.True(target!.TryPosition(new(3, 2, 4), out var position));
        Assert.InRange(Math.Abs(position.X - 2.25), 0, .00002);
        Assert.InRange(Math.Abs(position.Y + 3.5), 0, .00002);
        Assert.InRange(Math.Abs(position.Z - 4.75), 0, .00002);
        var edits = target.CreateEdits(position);
        Assert.Equal(new[] { "x", "y", "z" }, edits.Select(e => e.PropertyId));
        Assert.True(session.EditComponents(new(DocumentData.Operation(source.Revision), edits)).Succeeded);
        var result = session.Read().Resolved.Entities[DocumentData.Child].Components[DocumentData.Transform];
        Assert.Equal(.4, result.Properties["pitch"].Value.GetNumber()); Assert.Equal(.8, result.Properties["sx"].Value.GetNumber());
        Assert.Equal(1.2, result.Properties["sy"].Value.GetNumber());
    }

    [Fact]
    public void RootDelta_ChangesOnlyXAndNoOpPreservesExactDoubleValuesAndInheritance()
    {
        var source = DocumentData.Scene(); var entity = new EntityInstance(DocumentData.Parent, DocumentData.Base,
            new(propertyOverrides: [Set("x", .1), Set("y", .2), Set("z", .3)]));
        using var session = DocumentData.Factory().Create(new(source.Id, 7, [entity], source.Definitions, source.Schemas));
        var state = session.Read(); Assert.True(TranslationTarget.TryCapture(state, Picks(state), DocumentData.Parent, out var target));
        Assert.True(target!.TryPosition(Vector3.Zero, out var original)); Assert.Equal(new(.1, .2, .3), original); Assert.Empty(target.CreateEdits(original));
        Assert.True(target.TryPosition(new(.5f, 0, 0), out var moved));
        Assert.Equal(.2, moved.Y); Assert.Equal(.3, moved.Z);
        var edit = Assert.Single(target.CreateEdits(moved)); Assert.Equal("x", edit.PropertyId); Assert.Equal(.6, edit.Value.GetNumber());
        Assert.Equal(Matrix4x4.Identity, target.InverseParent);
        Assert.False(target.TryPosition(new(float.NaN, 0, 0), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.CreateEdits(new(double.MaxValue, .2, .3)));
        Assert.Same(state.Document, session.Read().Document); Assert.Equal(0, session.Read().UndoCount);
    }

    [Fact]
    public void Capture_RequiresMatchingVisibleSourceAndGhostsIncludeOnlySelectedSubtree()
    {
        var source = DocumentData.Scene(); var sibling = new EntityId(DocumentData.Id(90));
        using var session = DocumentData.Factory().Create(new(source.Id, 7,
            [new(DocumentData.Parent, DocumentData.Base), new(DocumentData.Child, DocumentData.Base, parentId: DocumentData.Parent), new(sibling, DocumentData.Base)],
            source.Definitions, source.Schemas));
        var state = session.Read(); var picks = Picks(state);
        Assert.True(TranslationTarget.TryCapture(state, picks, DocumentData.Parent, out var parent));
        Assert.Equal(new[] { DocumentData.Parent, DocumentData.Child }, parent!.Ghosts.Select(g => g.Entity));
        Assert.True(TranslationTarget.TryCapture(state, picks, DocumentData.Child, out var child)); Assert.Single(child!.Ghosts);
        Assert.False(TranslationTarget.TryCapture(state, picks, new(DocumentData.Id(999)), out _));
        Assert.False(TranslationTarget.TryCapture(state, PreviewPickSnapshot.Capture(PrimitiveSceneAdapter.Project(state.Resolved), "unknown"), DocumentData.Parent, out _));
        session.EditComponents(new(DocumentData.Operation(7), [new(DocumentData.Parent, DocumentData.Transform, "x", PropertyValue.From(3d))]));
        Assert.False(TranslationTarget.TryCapture(session.Read(), picks, DocumentData.Parent, out _));
    }
    private static PreviewPickSnapshot Picks(SceneAuthoringState state) => PreviewPickSnapshot.Capture(PrimitiveSceneAdapter.Project(state.Resolved), PreviewPickSnapshot.CubeGeometryProfile);
    private static ComponentPropertyOverride Set(string key, double value) => new(DocumentData.Transform, key, PropertyValue.From(value));
}
