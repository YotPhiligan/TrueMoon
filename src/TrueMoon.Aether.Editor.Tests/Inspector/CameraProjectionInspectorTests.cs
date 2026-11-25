using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class CameraProjectionInspectorTests
{
    private static readonly ComponentId Camera = new(DocumentData.Id(60));

    [Fact]
    public void ProjectionInput_DisplaysDegreesAndCommitsRadiansWithPlanesInOneUndo()
    {
        using var fixture = new InspectorFixture(Scene());
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        var fields = fixture.Model.Components.Single(item => item.Id == Camera);
        Assert.Equal("Camera", fields.Title);
        Assert.Equal(new[] { "fov", "near", "far" }, fields.Properties.Take(3).Select(field => field.Key));
        var angle = Assert.IsType<TextBox>(view.Properties[(Camera, "fov")].Input);
        Assert.Equal("60", angle.Value);
        Assert.Contains("°", angle.AccessibleName);

        var original = fixture.Owner.Document.Read().Content!;
        Enter("fov", "90");
        Enter("near", "0.5");
        Enter("far", "25");
        Assert.Same(original, fixture.Owner.Document.Read().Content);
        Assert.True(view.Apply.IsEnabled);
        Click(view.Apply);
        Assert.Equal(original.UndoCount + 1, fixture.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(Math.PI / 2, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.5, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(25d, Read(fixture, "far").Value.GetNumber());
        Assert.Equal("90", angle.Value);

        Click(view.Undo);
        Assert.Equal(Math.PI / 3, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.1, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(2d, Read(fixture, "far").Value.GetNumber());
        Assert.Equal("60", angle.Value);
        Click(view.Redo);
        Assert.Equal(Math.PI / 2, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.5, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(25d, Read(fixture, "far").Value.GetNumber());
        Assert.Same(angle, view.Properties[(Camera, "fov")].Input);

        void Enter(string key, string value)
        {
            var input = view.Properties[(Camera, key)].Input;
            view.Scroll.BringIntoView(input.Bounds);
            Drain();
            ui.Focus(input);
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: value));
            Drain();
        }

        void Click(Button button)
        {
            view.Scroll.BringIntoView(button.Bounds);
            Drain();
            DocumentUiFixture.Click(ui, button);
            Drain();
        }

        void Drain() { ui.Update(); ui.Update(); }
    }

    [Theory]
    [InlineData("fov", "0")]
    [InlineData("fov", "180")]
    [InlineData("fov", "179.9999999")]
    [InlineData("near", "0")]
    [InlineData("near", "1e-300")]
    [InlineData("far", "0.1")]
    [InlineData("far", "1e100")]
    public void InvalidProjection_RejectedBeforeDocumentChange(string key, string input)
    {
        using var fixture = new InspectorFixture(Scene());
        var original = fixture.Owner.Document.Read().Content;
        var field = fixture.Property(Camera, key);
        var committed = field.Value;
        field.Value = input;

        Assert.True(field.HasError);
        Assert.Equal(input, field.Value);
        Assert.Contains(field.Error, fixture.Model.DiagnosticsText);
        Assert.False(fixture.Model.CanApply);
        fixture.Model.Apply();
        Assert.Same(original, fixture.Owner.Document.Read().Content);
        fixture.Model.Cancel();
        Assert.Equal(committed, field.Value);
        Assert.False(field.HasError);
    }

    [Fact]
    public void ResetNear_ValidatesInheritedValueAgainstDraftFar()
    {
        using var fixture = new InspectorFixture(Scene(far: 0.5));
        var original = fixture.Owner.Document.Read().Content;
        var near = fixture.Property(Camera, "near");
        var far = fixture.Property(Camera, "far");
        fixture.Model.ResetProperty(near);

        Assert.True(near.IsReset);
        Assert.True(far.HasError);
        Assert.False(fixture.Model.CanApply);
        fixture.Model.Apply();
        Assert.Same(original, fixture.Owner.Document.Read().Content);

        far.Value = "2";
        Assert.True(fixture.Model.CanApply);
        fixture.Model.Apply();
        var resolved = Read(fixture, "near");
        Assert.Equal(0.75, resolved.Value.GetNumber());
        Assert.Equal(DocumentData.Base, resolved.Source.DefinitionId);
        Assert.Equal(2d, Read(fixture, "far").Value.GetNumber());
        Assert.Equal(1, fixture.Owner.Document.Read().Content!.UndoCount);
    }

    private static ResolvedProperty Read(InspectorFixture fixture, string key) =>
        fixture.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[Camera].Properties[key];

    private static SceneDocument Scene(double far = 2) => new(new(DocumentData.Id(100)), 7,
        [new(DocumentData.Child, DocumentData.Base, new(propertyOverrides:
            [new(Camera, "near", PropertyValue.From(0.1)), new(Camera, "far", PropertyValue.From(far))]))],
        [new(DocumentData.Base, new(addedComponents:
        [
            new(DocumentData.Transform, BuiltInComponents.TransformId, 1),
            new(Camera, MeshRenderingComponents.CameraId, 1, [KeyValuePair.Create("near", PropertyValue.From(0.75))])
        ]))], [BuiltInComponents.Schemas[0], MeshRenderingComponents.CameraSchema]);
}
