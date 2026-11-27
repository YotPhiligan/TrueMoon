using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using System.ComponentModel;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

/// <summary>How the inspector drives one conditional slot for the render-asset choice list.</summary>
public sealed class InspectorRenderChoicesWiringTests
{
    private static readonly ComponentId Renderer = new(DocumentData.Id(80));
    private static readonly Guid MeshOne = DocumentData.Id(81), MeshTwo = DocumentData.Id(82), MeshThree = DocumentData.Id(83);
    private static readonly ComponentTypeId RendererType = new(DocumentData.Id(84));

    [Fact]
    public void SameFieldToggle_ClosesAndReleasesRows_ReopenAfterTheCloseBuildsNewOnes()
    {
        var (model, _) = Model();
        using var view = new EditorInspectorView(model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        var choices = Choices(view);

        Assert.False(choices.Host.IsVisible);
        Assert.False(choices.Slot.HasBranch);

        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        Assert.True(choices.Host.IsVisible);
        ui.Update();
        var first = Assert.IsType<VStack>(choices.Slot.Child);
        Assert.Equal(["Один", "Два"], Names(first));

        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        Assert.False(choices.Host.IsVisible);
        Assert.True(choices.Slot.HasBranch);
        ui.Update();
        Assert.False(choices.Slot.HasBranch);
        Assert.Null(choices.Slot.AppliedState);
        Assert.True(first.IsDisposed);

        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        Assert.True(choices.Host.IsVisible);
        ui.Update();
        var second = Assert.IsType<VStack>(choices.Slot.Child);
        Assert.NotSame(first, second);
        Assert.Equal(["Один", "Два"], Names(second));
    }

    [Fact]
    public void ChoiceRow_ClosesTheListAndStagesTheAssetOnThePresentedModel()
    {
        var (model, property) = Model();
        using var view = new EditorInspectorView(model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        var choices = Choices(view);
        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        ui.Update();
        var rows = Assert.IsType<VStack>(choices.Slot.Child);

        Activate(ui, (Button)rows.Items[1]);
        Assert.False(choices.Host.IsVisible);
        ui.Update();
        Assert.False(choices.Slot.HasBranch);
        Assert.NotNull(model.Staged);
        Assert.Same(model.Assets, model.Staged!.Value.Selection);
        Assert.Same(property, model.Staged.Value.Property);
        Assert.Equal(MeshTwo, model.Staged.Value.Asset);
    }

    [Fact]
    public void NewAssetSnapshot_ClosesTheOpenList_TheReopenPresentsTheNewChoices()
    {
        var (model, _) = Model();
        using var view = new EditorInspectorView(model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        var choices = Choices(view);
        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        ui.Update();
        var first = Assert.IsType<VStack>(choices.Slot.Child);

        model.Assets = new InspectorRenderAssetSelection(Renderer,
            [new(MeshOne, "Один"), new(MeshTwo, "Два"), new(MeshThree, "Три")], [], MeshOne, null, true);
        model.Raise(nameof(model.RenderAssets));
        ui.Update();
        Assert.False(choices.Host.IsVisible);
        Assert.False(choices.Slot.HasBranch);
        Assert.True(first.IsDisposed);

        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        ui.Update();
        Assert.Equal(["Один", "Два", "Три"], Names(Assert.IsType<VStack>(choices.Slot.Child)));
    }

    [Fact]
    public void UnavailableChoice_ReopensLabelledButNeverStages()
    {
        var (model, _) = Model();
        using var view = new EditorInspectorView(model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(view);
        ui.Update();
        var choices = Choices(view);
        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        ui.Update();
        var first = Assert.IsType<VStack>(choices.Slot.Child);

        model.Assets = model.Assets with { Meshes = [new(MeshOne, "Один") { CanSelect = false }] };
        model.Raise(nameof(model.RenderAssets));
        ui.Update();
        Assert.False(choices.Host.IsVisible);
        Assert.False(choices.Slot.HasBranch);
        Assert.True(first.IsDisposed);

        Activate(ui, (Button)view.Properties[(Renderer, "mesh")].Input);
        ui.Update();
        var rows = Assert.IsType<VStack>(choices.Slot.Child);
        Assert.Equal(["Один · недоступен"], Names(rows));
        var row = (Button)rows.Items[0];
        Assert.False(row.IsEnabled);
        Assert.True(choices.Host.IsVisible);
        Assert.True(choices.Slot.HasBranch);
        Assert.Null(model.Staged);
    }

    private static (ScrollViewer Host, ConditionalContent<EditorInspectorView.RenderChoicesState?> Slot) Choices(
        EditorInspectorView view)
    {
        var controls = view.Properties[(Renderer, "mesh")];
        Assert.NotNull(controls.RenderChoices);
        return controls.RenderChoices!.Value;
    }

    private static string[] Names(VStack rows) => rows.Items.OfType<Button>().Select(button => button.Value).ToArray();

    private static void Activate(UiSession ui, Button button)
    {
        ui.Focus(button);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
        ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter));
    }

    private static (ChoicesModel Model, InspectorProperty Property) Model()
    {
        var schema = new ComponentPropertySchema("mesh", PropertyValueKind.String, PropertyValue.From(""), isNullable: true);
        var property = new InspectorProperty(Renderer, schema, "Меш", static () => { }) { IsRenderReference = true };
        var component = new InspectorComponent(Renderer, RendererType, 1, "MeshRenderer", "Документ", [property]);
        return (new ChoicesModel(component), property);
    }

    /// <summary>Minimal presented model: one renderer component with a mesh reference and detached asset choices.</summary>
    private sealed class ChoicesModel(InspectorComponent component) : IEditorInspectorViewModel
    {
        internal InspectorRenderAssetSelection Assets { get; set; } = new(Renderer,
            [new(MeshOne, "Один"), new(MeshTwo, "Два")], [], MeshOne, null, true);
        internal (InspectorRenderAssetSelection Selection, InspectorProperty Property, Guid Asset)? Staged { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public InspectorRenderAssetSelection? RenderAssets => Assets;

        public void SelectRenderAsset(InspectorRenderAssetSelection selection, InspectorProperty property, Guid assetId)
            => Staged = (selection, property, assetId);

        public IReadOnlyList<InspectorComponent> Components => [component];
        public IReadOnlyList<InspectorComponentType> AddableTypes => [];
        public IReadOnlyList<InspectorRemovedComponent> RemovedComponents => [];
        public IReadOnlyList<TransformField> Fields => [];

        public bool HasSelection => true;
        public bool NoSelection => false;
        public InspectorSelection Selection => new(null, true);
        public bool CanEdit => true;
        public bool CanEditProperties => true;
        public bool CanChangeComponents => true;
        public bool CanApply => false;
        public bool CanCancel => false;
        public bool CanUndo => false;
        public bool CanRedo => false;
        public bool CanPreview => false;
        public bool CanStopPreview => false;
        public bool HasDraft => false;
        public bool HasError => false;
        public string ErrorText => "";
        public string DiagnosticsText => "";
        public bool HasDiagnostics => false;
        public string PreviewText => "";
        public bool ScaleLinked { get; set; }
        public bool CanLinkScale => false;
        public bool CanResetPosition => false;
        public bool CanResetRotation => false;
        public bool CanResetScale => false;
        public bool AnglesInDegrees { get; set; } = true;
        public bool CanChangeAngleUnits => true;
        public string RotationLabel => "0°";
        public double RotationStep => 1;
        public double RotationMaximum => 360;
        public double RotationMinimum => -360;

        public bool CanAdjustNumeric(InspectorProperty property) => false;
        public bool IsNumericGestureCurrent(InspectorProperty property, long id) => false;
        public long BeginNumericGesture(InspectorProperty property) => 0;
        public void FinishNumericGesture(TrueMoon.Argentis.Editors.NumericGestureResult result, long id) { }

        public void Refresh(string? selectedId) { }
        public void Apply() { }
        public void ResetProperty(InspectorProperty property) { }
        public void ResetTransformGroup(TransformGroup group) { }
        public void AddComponent(ComponentTypeId type, int version) { }
        public void RemoveComponent(ComponentId component) { }
        public void RestoreComponent(ComponentId component) { }
        public void ResetRemoval(ComponentId component) { }
        public void Cancel() { }
        public void Undo() { }
        public void Redo() { }
        public void ShowPreview() { }
        public Task StopPreviewAsync() => Task.CompletedTask;
    }
}
