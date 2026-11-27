using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Scenes;
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
        Assert.True(view.ApplyPart.IsEnabled);
        Click(view.ApplyPart);
        Assert.Equal(original.UndoCount + 1, fixture.Owner.Document.Read().Content!.UndoCount);
        Assert.Equal(Math.PI / 2, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.5, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(25d, Read(fixture, "far").Value.GetNumber());
        Assert.Equal("90", angle.Value);

        Click(view.UndoPart);
        Assert.Equal(Math.PI / 3, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.1, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(2d, Read(fixture, "far").Value.GetNumber());
        Assert.Equal("60", angle.Value);
        Click(view.RedoPart);
        Assert.Equal(Math.PI / 2, Read(fixture, "fov").Value.GetNumber());
        Assert.Equal(0.5, Read(fixture, "near").Value.GetNumber());
        Assert.Equal(25d, Read(fixture, "far").Value.GetNumber());
        Assert.Same(angle, view.Properties[(Camera, "fov")].Input);

        void Enter(string key, string value)
        {
            var input = view.Properties[(Camera, key)].Input;
            view.ScrollPart.BringIntoView(input.Bounds);
            Drain();
            ui.Focus(input);
            ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: value));
            Drain();
        }

        void Click(Button button)
        {
            view.ScrollPart.BringIntoView(button.Bounds);
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

    [Fact]
    public void ProjectionChoice_RoutedApplyUndoRedoAndCodecRoundTripKeepOneCamera()
    {
        using var fixture = new InspectorFixture(Scene());
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        Drain(ui);
        var original = fixture.Owner.Document.Read().Content!;
        var originalChild = original.Resolved.Entities[DocumentData.Child];
        var originalTransform = originalChild.Components[DocumentData.Transform];
        var near = view.Properties[(Camera, "near")].Input;
        var perspective = ProjectionButton(view, Camera, "perspective");
        var orthographic = ProjectionButton(view, Camera, "orthographic");
        Assert.False(perspective.IsEnabled);
        Assert.Equal(ControlTone.Accent, perspective.Tone);
        Assert.Equal(perspective.Theme.Accent, perspective.BorderColor);
        Assert.True(orthographic.IsEnabled);

        Click(ui, view, view.Components[Camera].Header);
        Assert.False(view.Components[Camera].Body.IsVisible);
        Click(ui, view, view.Components[Camera].Header);
        Assert.True(view.Components[Camera].Body.IsVisible);
        Click(ui, view, orthographic);

        var height = Assert.IsType<TextBox>(view.Properties[(Camera, "verticalSize")].Input);
        Assert.Equal("10", height.Value);
        Assert.DoesNotContain((Camera, "fov"), view.Properties.Keys);
        Assert.Same(near, view.Properties[(Camera, "near")].Input);
        Assert.Same(perspective, ProjectionButton(view, Camera, "perspective"));
        Assert.True(perspective.IsEnabled);
        Assert.False(orthographic.IsEnabled);
        Assert.Equal(ControlTone.Accent, orthographic.Tone);
        Assert.Same(original, fixture.Owner.Document.Read().Content);
        Assert.False(fixture.Owner.Document.Read().IsDirty);
        Assert.Equal(0, original.UndoCount);
        Assert.True(fixture.Model.HasDraft);
        Assert.True(view.ApplyPart.IsEnabled);

        Click(ui, view, view.DetailsButtons[Camera]);
        var reset = view.Properties[(Camera, "near")].Reset;
        Assert.True(reset.IsVisible && reset.IsEnabled);
        Click(ui, view, reset);
        Assert.True(fixture.Property(Camera, "near").IsReset);
        Enter(ui, view, height, "5");
        Click(ui, view, view.ApplyPart);

        var applied = fixture.Owner.Document.Read().Content!;
        var child = applied.Resolved.Entities[DocumentData.Child];
        var replacement = GameCamera(child);
        Assert.NotEqual(Camera, replacement.Id);
        AssertOrthographic(replacement, 5, .75);
        Assert.Equal(DocumentData.Child, replacement.PresenceSource.EntityId);
        Assert.Equal(original.UndoCount + 1, applied.UndoCount);
        Assert.True(fixture.Owner.Document.Read().IsDirty);
        Assert.Contains(applied.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.OrthographicCameraId && schema.Version == 1);
        var transform = child.Components[DocumentData.Transform];
        Assert.Equal(originalTransform.Id, transform.Id);
        Assert.Equal(originalTransform.PresenceSource, transform.PresenceSource);
        foreach (var property in originalTransform.Properties)
            Assert.Equal(property.Value.Value, transform.Properties[property.Key].Value);
        var sibling = GameCamera(applied.Resolved.Entities[DocumentData.Parent]);
        Assert.Equal(Camera, sibling.Id);
        Assert.Equal(MeshRenderingComponents.CameraId, sibling.TypeId);
        Assert.Equal(PropertyValue.From(.75), sibling.Properties["near"].Value);
        Assert.Same(Assert.Single(original.Document.Definitions), Assert.Single(applied.Document.Definitions));

        Click(ui, view, view.UndoPart);
        var undone = fixture.Owner.Document.Read().Content!;
        var restored = GameCamera(undone.Resolved.Entities[DocumentData.Child]);
        Assert.Equal(Camera, restored.Id);
        Assert.Equal(MeshRenderingComponents.CameraId, restored.TypeId);
        Assert.Equal(DocumentData.Base, restored.PresenceSource.DefinitionId);
        Assert.Equal(PropertyValue.From(.1), restored.Properties["near"].Value);
        Assert.Equal(DocumentData.Child, restored.Properties["near"].Source.EntityId);
        Assert.DoesNotContain(undone.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.OrthographicCameraId);

        Click(ui, view, view.RedoPart);
        var redone = fixture.Owner.Document.Read().Content!;
        var redoneCamera = GameCamera(redone.Resolved.Entities[DocumentData.Child]);
        Assert.Equal(replacement.Id, redoneCamera.Id);
        AssertOrthographic(redoneCamera, 5, .75);

        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory()));
        using var owner = new DocumentUiFixture(sceneStore: store);
        DocumentUiFixture.Wait(owner.Document.NewAsync(redone.Document));
        var opening = owner.Document.Read().OpeningId;
        owner.Run(DocumentCommand.Save, "camera-projection.aetherscene");
        Assert.False(owner.Document.Read().IsDirty);
        owner.Run(DocumentCommand.Close);
        owner.Run(DocumentCommand.Open, "camera-projection.aetherscene");

        var reopened = owner.Document.Read();
        Assert.NotEqual(Guid.Empty, reopened.OpeningId);
        Assert.NotEqual(opening, reopened.OpeningId);
        Assert.False(reopened.IsDirty);
        var state = reopened.Content!;
        Assert.Equal(0, state.UndoCount);
        Assert.Equal(0, state.RedoCount);
        var camera = GameCamera(state.Resolved.Entities[DocumentData.Child]);
        Assert.Equal(replacement.Id, camera.Id);
        AssertOrthographic(camera, 5, .75);
        Assert.Contains(state.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.CameraId && schema.Version == 1);
        Assert.Contains(state.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.OrthographicCameraId && schema.Version == 1);
    }

    [Theory]
    [InlineData("verticalSize", "0", "5")]
    [InlineData("verticalSize", "-1", "5")]
    [InlineData("verticalSize", "1e-300", "5")]
    [InlineData("verticalSize", "1e100", "5")]
    [InlineData("far", "1.000000001", "5")]
    public void OrthographicLens_InvalidNumbersAndFloatCollapsedPlanesDoNotApply(string key, string input, string corrected)
    {
        using var fixture = new InspectorFixture(LocalOrthographicScene());
        var original = fixture.Owner.Document.Read().Content!;
        var section = fixture.Model.Components.Single(component => component.Id == Camera);
        Assert.Equal("OrthographicCamera", section.Title);
        Assert.Equal(new[] { "verticalSize", "near", "far" }, section.Properties.Take(3).Select(property => property.Key));
        Assert.DoesNotContain(section.Properties, property => property.Key == "fov");
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        Drain(ui);
        var height = Assert.IsType<TextBox>(view.Properties[(Camera, "verticalSize")].Input);
        Assert.DoesNotContain("°", height.AccessibleName);

        var field = fixture.Property(Camera, key);
        var editor = Assert.IsType<TextBox>(view.Properties[(Camera, key)].Input);
        Enter(ui, view, editor, input);
        Assert.True(field.HasError);
        Assert.True(editor.HasError);
        Assert.NotEmpty(field.Error);
        Assert.Contains(field.Error, fixture.Model.DiagnosticsText);
        Assert.False(view.ApplyPart.IsEnabled);
        fixture.Model.Apply();
        Assert.Same(original, fixture.Owner.Document.Read().Content);

        Enter(ui, view, editor, corrected);
        Assert.False(field.HasError);
        Assert.True(view.ApplyPart.IsEnabled);
        Click(ui, view, view.ApplyPart);
        Assert.Equal(PropertyValue.From(5d), Read(fixture, key).Value);
        Assert.Equal(original.UndoCount + 1, fixture.Owner.Document.Read().Content!.UndoCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectionChoice_RoundTripBeforeApplyAndConflictKeepDraftsIsolated(bool replaceOpening)
    {
        using var fixture = new InspectorFixture(Scene());
        var original = fixture.Owner.Document.Read();
        var fov = fixture.Property(Camera, "fov");
        var far = fixture.Property(Camera, "far");
        fov.Value = "80";
        far.Value = "25";
        fixture.Model.SelectCameraProjection(Camera, InspectorCameraProjection.Orthographic);
        var discardedHeight = fixture.Property(Camera, "verticalSize");
        discardedHeight.Value = "4";
        fixture.Model.SelectCameraProjection(Camera, InspectorCameraProjection.Perspective);

        Assert.Same(fov, fixture.Property(Camera, "fov"));
        Assert.Equal("80", fov.Value);
        Assert.Same(far, fixture.Property(Camera, "far"));
        Assert.Equal("25", far.Value);
        Assert.Same(original.Content, fixture.Owner.Document.Read().Content);
        fixture.Model.SelectCameraProjection(Camera, InspectorCameraProjection.Orthographic);
        var height = fixture.Property(Camera, "verticalSize");
        Assert.NotSame(discardedHeight, height);
        Assert.Equal("10", height.Value);
        Assert.Equal("25", far.Value);

        if (replaceOpening)
        {
            fixture.Owner.Store.File = new(Scene(transformX: 57), new("camera-replacement"));
            fixture.Owner.Run(DocumentCommand.Open, "replacement.aetherscene");
            Assert.NotEqual(original.OpeningId, fixture.Owner.Document.Read().OpeningId);
            Assert.Equal(original.Content!.Document.Revision, fixture.Owner.Document.Read().Content!.Document.Revision);
        }
        else Assert.True(fixture.Owner.Document.Apply(DocumentData.Edit(original.Content!.Document.Revision, 57)).Succeeded);
        var external = fixture.Owner.Document.Read().Content!;
        fixture.Refresh();

        Assert.Same(height, fixture.Property(Camera, "verticalSize"));
        Assert.Equal("10", height.Value);
        Assert.Equal("25", far.Value);
        Assert.Equal(new InspectorCameraProjectionState(InspectorCameraProjection.Orthographic, false),
            fixture.Model.CameraProjection(Camera));
        Assert.True(fixture.Model.HasDraft);
        Assert.False(fixture.Model.CanApply);
        fixture.Model.Apply();
        Assert.Same(external, fixture.Owner.Document.Read().Content);

        fixture.Model.Cancel();
        Assert.False(fixture.Model.HasDraft);
        Assert.Equal("60", fixture.Property(Camera, "fov").Value);
        Assert.Equal("2", fixture.Property(Camera, "far").Value);
        Assert.Equal("57", fixture.Property(DocumentData.Transform, "x").Value);
        Assert.Equal(MeshRenderingComponents.CameraId, GameCamera(external.Resolved.Entities[DocumentData.Child]).TypeId);
        Assert.Equal(57d, DocumentData.X(external));
    }

    [Fact]
    public void ProjectionChoice_LocalOrthographicToPerspectiveCommitsDefaultDegrees()
    {
        using var fixture = new InspectorFixture(LocalOrthographicScene());
        var original = fixture.Owner.Document.Read().Content!;
        var inheritedTransform = fixture.Property(DocumentData.Transform, "x");
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        Drain(ui);
        Click(ui, view, ProjectionButton(view, Camera, "perspective"));
        var angle = Assert.IsType<TextBox>(view.Properties[(Camera, "fov")].Input);
        Assert.Equal("60", angle.Value);
        Assert.Contains("°", angle.AccessibleName);
        inheritedTransform.Value = "3.5";
        fixture.Property(Camera, "far").Value = "12.5";

        Click(ui, view, view.ApplyPart);
        var applied = fixture.Owner.Document.Read().Content!;
        var perspective = GameCamera(applied.Resolved.Entities[DocumentData.Child]);
        Assert.Equal(MeshRenderingComponents.CameraId, perspective.TypeId);
        Assert.Equal(Math.PI / 3, perspective.Properties["fov"].Value.GetNumber());
        Assert.DoesNotContain("verticalSize", perspective.Properties.Keys);
        AssertCommonCameraValues(perspective, 1, 12.5);
        Assert.NotEqual(Camera, perspective.Id);
        Assert.Equal(perspective.Id, Assert.Single(applied.Document.Entities.Single().Changes.AddedComponents).Id);
        Assert.Empty(applied.Document.Entities.Single().Changes.RemovedComponents);
        Assert.Equal(3.5, DocumentData.X(applied));
        Assert.Equal(original.UndoCount + 1, applied.UndoCount);
        Assert.Contains(applied.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.CameraId && schema.Version == 1);

        Click(ui, view, view.UndoPart);
        var undone = fixture.Owner.Document.Read().Content!;
        var orthographic = GameCamera(undone.Resolved.Entities[DocumentData.Child]);
        Assert.Equal(Camera, orthographic.Id);
        AssertOrthographic(orthographic, 8, 1, 2);
        Assert.Equal(DocumentData.X(original), DocumentData.X(undone));
        Assert.DoesNotContain(undone.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.CameraId);
    }

    [Fact]
    public void CameraPresence_AddAndRestoreCannotIntroduceSecondProjection()
    {
        using var empty = new InspectorFixture(Scene(inheritsCamera: false));
        var addView = new EditorInspectorView(empty.Model, new EditorInspectorViewStyle());
        using (var ui = InspectorFixture.Ui(addView))
        {
            Drain(ui);
            var original = empty.Owner.Document.Read().Content!;
            Assert.DoesNotContain(original.Document.Schemas, schema => schema.TypeId == MeshRenderingComponents.OrthographicCameraId);
            Click(ui, addView, addView.AddComponentPart);
            Assert.True(addView.AddChoicesPart.IsVisible);
            var addOrthographic = addView.AddButtons[(MeshRenderingComponents.OrthographicCameraId, 1)];
            Assert.True(addOrthographic.IsEnabled);
            Click(ui, addView, addOrthographic);
            var added = empty.Owner.Document.Read().Content!;
            var camera = GameCamera(added.Resolved.Entities[DocumentData.Child]);
            Assert.Equal(MeshRenderingComponents.OrthographicCameraId, camera.TypeId);
            Assert.Equal(PropertyValue.From(10d), camera.Properties["verticalSize"].Value);
            Assert.Contains(added.Document.Schemas, schema => schema.TypeId == camera.TypeId && schema.Version == 1);
            Assert.Equal(original.UndoCount + 1, added.UndoCount);
            Click(ui, addView, addView.AddComponentPart);
            Assert.False(addView.AddButtons[(MeshRenderingComponents.CameraId, 1)].IsEnabled);
            empty.Model.AddComponent(MeshRenderingComponents.CameraId, 1);
            Assert.Same(added, empty.Owner.Document.Read().Content);
        }

        using var inherited = new InspectorFixture(Scene());
        var view = new EditorInspectorView(inherited.Model, new EditorInspectorViewStyle());
        using var inheritedUi = InspectorFixture.Ui(view);
        Drain(inheritedUi);
        Click(inheritedUi, view, ProjectionButton(view, Camera, "orthographic"));
        Click(inheritedUi, view, view.ApplyPart);
        var switched = inherited.Owner.Document.Read().Content!;
        Assert.True(inherited.Model.CanChangeComponents);
        var removed = Assert.Single(inherited.Model.RemovedComponents, component => component.Id == Camera);
        Assert.True(removed.LocalMask);
        Assert.False(removed.CanRestore);
        var restore = view.RestoreButtons[Camera];
        Assert.True(restore.IsVisible);
        Assert.False(restore.IsEnabled);
        Click(inheritedUi, view, restore);
        inherited.Model.RestoreComponent(Camera);
        Assert.Same(switched, inherited.Owner.Document.Read().Content);
        inherited.Model.ResetRemoval(Camera);
        Assert.Same(switched, inherited.Owner.Document.Read().Content);

        Click(inheritedUi, view, view.Components[GameCamera(switched.Resolved.Entities[DocumentData.Child]).Id].Remove);
        Assert.Same(restore, view.RestoreButtons[Camera]);
        Assert.True(restore.IsEnabled);
        Click(inheritedUi, view, restore);
        Assert.Equal(Camera, GameCamera(inherited.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child]).Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProjectionChoice_RejectedForIncompatibleSchemaOrDependentComponent(bool incompatibleSchema)
    {
        var scene = Scene();
        if (incompatibleSchema)
        {
            var canonical = MeshRenderingComponents.OrthographicCameraSchema;
            var incompatible = new ComponentSchema(canonical.TypeId, canonical.Version,
                canonical.Properties.Values.Select(field => field.Id == "verticalSize"
                    ? new ComponentPropertySchema(field.Id, field.Kind, PropertyValue.From(7d))
                    : field), requiredTypes: canonical.RequiredTypes);
            scene = new(scene.Id, scene.Revision, scene.Entities, scene.Definitions, scene.Schemas.Add(incompatible));
        }
        else
        {
            var dependency = new ComponentSchema(DocumentData.CustomType, 1, [], requiredTypes: [MeshRenderingComponents.CameraId]);
            scene = new(scene.Id, scene.Revision, scene.Entities.Select(entity => entity.Id == DocumentData.Child
                ? new EntityInstance(entity.Id, entity.DefinitionId, new(
                    addedComponents: [new(DocumentData.Custom, dependency.TypeId, 1)],
                    propertyOverrides: entity.Changes.PropertyOverrides))
                : entity), scene.Definitions, scene.Schemas.Add(dependency));
        }

        using var fixture = new InspectorFixture(scene);
        var original = fixture.Owner.Document.Read().Content!;
        var view = new EditorInspectorView(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        Drain(ui);
        Assert.Equal(new InspectorCameraProjectionState(InspectorCameraProjection.Perspective, false),
            fixture.Model.CameraProjection(Camera));
        var selected = ProjectionButton(view, Camera, "perspective");
        Assert.Equal(ControlTone.Accent, selected.Tone);
        Assert.False(selected.IsEnabled);
        var blocked = ProjectionButton(view, Camera, "orthographic");
        Assert.False(blocked.IsEnabled);
        Click(ui, view, blocked);
        fixture.Model.SelectCameraProjection(Camera, InspectorCameraProjection.Orthographic);
        Assert.False(fixture.Model.HasDraft);
        Assert.False(view.ApplyPart.IsEnabled);
        Assert.Contains((Camera, "fov"), view.Properties.Keys);
        Assert.DoesNotContain((Camera, "verticalSize"), view.Properties.Keys);
        Assert.Same(original, fixture.Owner.Document.Read().Content);
    }

    private static Button ProjectionButton(EditorInspectorView view, ComponentId component, string projection) =>
        Assert.Single(DocumentUiFixture.Tree(view).OfType<Button>(),
            button => button.SemanticId == $"editor.inspector.camera.{component.Value:N}.{projection}");

    private static void Click(UiSession ui, EditorInspectorView view, Button button)
    {
        view.ScrollPart.BringIntoView(button.Bounds);
        Drain(ui);
        DocumentUiFixture.Click(ui, button);
        Drain(ui);
    }

    private static void Enter(UiSession ui, EditorInspectorView view, TextBox input, string value)
    {
        view.ScrollPart.BringIntoView(input.Bounds);
        Drain(ui);
        ui.Focus(input);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: value));
        Drain(ui);
    }

    private static void Drain(UiSession ui) { ui.Update(); ui.Update(); }

    private static ResolvedComponent GameCamera(ResolvedEntity entity) =>
        Assert.Single(entity.Components.Values, component => component.TypeId == MeshRenderingComponents.CameraId ||
            component.TypeId == MeshRenderingComponents.OrthographicCameraId);

    private static void AssertOrthographic(ResolvedComponent camera, double height, double near, double far = 2)
    {
        Assert.Equal(MeshRenderingComponents.OrthographicCameraId, camera.TypeId);
        Assert.Equal(1, camera.SchemaVersion);
        Assert.Equal(PropertyValue.From(height), camera.Properties["verticalSize"].Value);
        Assert.DoesNotContain("fov", camera.Properties.Keys);
        AssertCommonCameraValues(camera, near, far);
    }

    private static void AssertCommonCameraValues(ResolvedComponent camera, double near, double far)
    {
        Assert.Equal(PropertyValue.From(near), camera.Properties["near"].Value);
        Assert.Equal(PropertyValue.From(far), camera.Properties["far"].Value);
        Assert.Equal(PropertyValue.From(3L), camera.Properties["layers"].Value);
        Assert.Equal(PropertyValue.From(.08), camera.Properties["clearR"].Value);
        Assert.Equal(PropertyValue.From(.12), camera.Properties["clearG"].Value);
        Assert.Equal(PropertyValue.From(.16), camera.Properties["clearB"].Value);
    }

    private static ResolvedProperty Read(InspectorFixture fixture, string key) =>
        fixture.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[Camera].Properties[key];

    private static SceneDocument Scene(double far = 2, bool inheritsCamera = true, double transformX = 0)
    {
        var transform = new ComponentData(DocumentData.Transform, BuiltInComponents.TransformId, 1,
            [KeyValuePair.Create("x", PropertyValue.From(transformX))]);
        var camera = new ComponentData(Camera, MeshRenderingComponents.CameraId, 1, CameraValues(.75));
        ComponentData[] components = inheritsCamera ? [transform, camera] : [transform];
        var child = inheritsCamera ? new EntityPatch(propertyOverrides:
            [new(Camera, "near", PropertyValue.From(.1)), new(Camera, "far", PropertyValue.From(far))]) : EntityPatch.Empty;
        return new(new(DocumentData.Id(100)), 7,
            [new(DocumentData.Child, DocumentData.Base, child), new(DocumentData.Parent, DocumentData.Base)],
            [new(DocumentData.Base, new(addedComponents: components))],
            [BuiltInComponents.Schemas[0], MeshRenderingComponents.CameraSchema]);
    }

    private static SceneDocument LocalOrthographicScene() => new(new(DocumentData.Id(100)), 7,
        [new(DocumentData.Child, DocumentData.Base, new(addedComponents:
            [new(Camera, MeshRenderingComponents.OrthographicCameraId, 1,
                [.. CameraValues(1), KeyValuePair.Create("verticalSize", PropertyValue.From(8d))])]))],
        [new(DocumentData.Base, new(addedComponents: [new(DocumentData.Transform, BuiltInComponents.TransformId, 1)]))],
        [BuiltInComponents.Schemas[0], MeshRenderingComponents.OrthographicCameraSchema]);

    private static KeyValuePair<string, PropertyValue>[] CameraValues(double near) =>
    [
        KeyValuePair.Create("near", PropertyValue.From(near)),
        KeyValuePair.Create("far", PropertyValue.From(2d)),
        KeyValuePair.Create("layers", PropertyValue.From(3L)),
        KeyValuePair.Create("clearR", PropertyValue.From(.08)),
        KeyValuePair.Create("clearG", PropertyValue.From(.12)),
        KeyValuePair.Create("clearB", PropertyValue.From(.16))
    ];
}
