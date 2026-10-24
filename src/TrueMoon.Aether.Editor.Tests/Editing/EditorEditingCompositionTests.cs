using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorEditingCompositionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothProviders_ShareModelAndBorrowedDependenciesWhileFactoryCreatesIndependentUiOwnedViews(bool microsoft)
    {
        using var owner = new DocumentUiFixture(); owner.Run(DocumentCommand.Open, "room.aetherscene");
        var stops = 0; var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => { stops++; return Task.CompletedTask; });
        var style = new CountingStyle();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorDocumentCommands>(_ => owner.Commands)
                .Singleton<EditorPreview>(_ => preview).Singleton<IViewStyle<EditorTransformView>>(_ => style));
            context.UseAetherEditorEditing();
        }).Build();
        var model = app.Services.Resolve<EditorTransformViewModel>()!; model.Refresh(DocumentData.Child.Value.ToString());
        var factory = app.Services.Resolve<IFactory<EditorTransformView>>()!;
        var first = Assert.IsType<EditorTransformView>(factory.Create()); var second = Assert.IsType<EditorTransformView>(factory.Create()); using var ui1 = DocumentUiFixture.Ui(first); using var ui2 = DocumentUiFixture.Ui(second); ui1.Update(); ui2.Update();
        Assert.Same(model, app.Services.Resolve<EditorTransformViewModel>()); Assert.Same(factory, app.Services.Resolve<IFactory<EditorTransformView>>());
        Assert.Same(owner.Commands, app.Services.Resolve<EditorDocumentCommands>()); Assert.Same(preview, app.Services.Resolve<EditorPreview>()); Assert.Same(style, app.Services.Resolve<IViewStyle<EditorTransformView>>());
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext); Assert.NotSame(first, second); Assert.NotSame(first.Fields["x"], second.Fields["x"]); Assert.Equal(2, style.Configurations);
        model.Fields[0].Value = "67"; ui1.Update(); ui2.Update(); Assert.Equal("67", first.Fields["x"].Value); Assert.Equal("67", second.Fields["x"].Value);
        ui1.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.Equal(0, stops);
        model.Apply(); ui2.Update(); Assert.Equal(67, DocumentData.X(owner.Document.Read().Content!)); Assert.True(second.Undo.IsEnabled); Assert.Equal(0, owner.Authoring.Products[0].Disposals);
        var snapshot = owner.Document.Read().Content!.Resolved;
        app.Dispose(); Assert.Throws<ObjectDisposedException>(() => model.Refresh(null)); Assert.False(second.IsDisposed); Assert.True(owner.Commands.Read().Closed);
        Assert.Equal(1, owner.Authoring.Products[0].Disposals); Assert.Equal(1, stops); Assert.False(preview.Show(snapshot));
    }

    [Fact]
    public void Registration_RejectsMissingRuntimeBoundariesAndDuplicateComposition()
    {
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context => context.UseAetherEditorEditing()).Build());
        using var owner = new DocumentUiFixture();
        using var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorDocumentCommands>(_ => owner.Commands).Singleton<EditorPreview>(_ => preview));
            context.UseAetherEditorEditing(); context.UseAetherEditorEditing();
        }).Build());
    }

    [Fact]
    public void TransformCommit_SaveCloseAndReopenWithRealCodecPreserveAllNineValuesAndClearHistoryAtNewSession()
    {
        var store = new JsonSceneDocumentStore(new SceneFileCodec(DocumentData.Factory())); using var owner = new DocumentUiFixture(sceneStore: store);
        DocumentUiFixture.Wait(owner.Document.NewAsync(DocumentData.Scene()));
        using var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        using var model = new EditorTransformViewModel(owner.Commands, preview); model.Refresh(DocumentData.Child.Value.ToString());
        var values = new[] { "15", "-2", "3", "0.1", "-0.2", "0.3", "2", "3", "4" };
        for (var i = 0; i < values.Length; i++) model.Fields[i].Value = values[i]; model.Apply(); Assert.True(owner.Document.Read().IsDirty);
        owner.Run(DocumentCommand.Save, "edited.aetherscene"); Assert.False(owner.Document.Read().IsDirty);
        owner.Run(DocumentCommand.Close); owner.Run(DocumentCommand.Open, "edited.aetherscene"); model.Refresh(DocumentData.Child.Value.ToString());
        Assert.Equal(values, model.Fields.Select(field => field.Value)); Assert.Equal(8, owner.Document.Read().Content!.Document.Revision);
        Assert.Equal(0, owner.Document.Read().Content!.UndoCount); Assert.Equal(0, owner.Document.Read().Content!.RedoCount); Assert.False(model.CanUndo); Assert.False(model.CanRedo);
        Assert.False(model.HasDraft); Assert.False(owner.Document.Read().IsDirty); Assert.Empty(model.ErrorText);
    }

    private sealed class CountingStyle : IViewStyle<EditorTransformView>
    {
        public int Configurations;
        public void Configure(IViewStyleBuilder<EditorTransformView> styles) { Configurations++; new EditorTransformViewStyle().Configure(styles); }
    }
}
