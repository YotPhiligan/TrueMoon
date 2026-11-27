using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Styling;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorInspectorCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_ShareGenericModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedInspectors(bool microsoft)
    {
        using var test = new InspectorFixture(); var style = new CountingStyle();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorDocumentCommands>(_ => test.Owner.Commands)
                .Singleton<EditorPreview>(_ => test.Preview).Singleton<IViewStyle<EditorInspectorView>>(_ => style));
            context.UseAetherEditorEditing();
        }).Build();
        var model = app.Services.Resolve<EditorInspectorViewModel>()!; model.Refresh(InspectorFixture.Child);
        var factory = app.Services.Resolve<IFactory<EditorInspectorView>>()!;
        using var first = Assert.IsType<EditorInspectorView>(factory.Create());
        using var second = Assert.IsType<EditorInspectorView>(factory.Create());
        Assert.Equal(0, style.Configurations);
        using var ui1 = InspectorFixture.Ui(first); using var ui2 = InspectorFixture.Ui(second); ui1.Update(); ui2.Update();
        Assert.Same(model, app.Services.Resolve<EditorInspectorViewModel>()); Assert.Same(factory, app.Services.Resolve<IFactory<EditorInspectorView>>());
        Assert.Same(style, app.Services.Resolve<IViewStyle<EditorInspectorView>>()); Assert.Equal(2, style.Configurations);
        Assert.Equal(new Thickness(19), first.Padding);
        Assert.Equal(new Thickness(19), second.Padding);
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext); Assert.NotSame(first, second);
        var address = (DocumentData.Custom, "text");
        Assert.NotSame(first.Properties[address].Input, second.Properties[address].Input);
        model.Components.Single(item => item.Id == DocumentData.Custom).Properties.Single(item => item.Key == "text").Value = "shared draft";
        ui1.Update(); ui2.Update();
        Assert.Equal("shared draft", Assert.IsType<RichTextEditor>(first.Properties[address].Input).Text);
        Assert.Equal("shared draft", Assert.IsType<RichTextEditor>(second.Properties[address].Input).Text);
        ui1.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.False(test.Owner.Commands.Read().Closed);
        model.Apply(); Assert.True(model.CanUndo, $"HasDraft={model.HasDraft}; Error={model.ErrorText}");
        ui2.Update(); ui2.Update(); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.True(second.Undo.IsEnabled);
        app.Dispose();
        Assert.Throws<ObjectDisposedException>(() => model.Refresh(InspectorFixture.Child));
        Assert.False(second.IsDisposed); Assert.True(test.Owner.Commands.Read().Closed);
        Assert.Equal(1, test.Owner.Authoring.Products[0].Disposals);
        var secondText = second.Properties[address].Input;
        ui2.Dispose(); Assert.True(second.IsDisposed); Assert.True(secondText.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_DefaultGenericStyleAndLegacyFactoryBorrowTheSameModel(bool microsoft)
    {
        using var test = new InspectorFixture();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorDocumentCommands>(_ => test.Owner.Commands).Singleton<EditorPreview>(_ => test.Preview));
            context.UseAetherEditorEditing();
        }).Build();
        var model = app.Services.Resolve<EditorInspectorViewModel>()!; model.Refresh(InspectorFixture.Child);
        Assert.IsType<EditorInspectorViewStyle>(app.Services.Resolve<IViewStyle<EditorInspectorView>>());
        using var current = Assert.IsType<EditorInspectorView>(app.Services.Resolve<IFactory<EditorInspectorView>>()!.Create());
        using var legacy = Assert.IsType<EditorTransformView>(app.Services.Resolve<IFactory<EditorTransformView>>()!.Create());
        Assert.Same(model, current.DataContext); Assert.Same(model, legacy.DataContext);
        Assert.Null(current.Child);
        using var ui = InspectorFixture.Ui(current);
        using var legacyUi = InspectorFixture.Ui(legacy);
        ui.Update(); legacyUi.Update();
        Assert.Equal(5, current.Components.Count); Assert.Equal(9, legacy.Fields.Count);
        app.Dispose(); Assert.False(current.IsDisposed); Assert.False(legacy.IsDisposed);
    }

    [Theory]
    [InlineData("commands", false)]
    [InlineData("commands", true)]
    [InlineData("preview", false)]
    [InlineData("preview", true)]
    [InlineData("factory", false)]
    [InlineData("factory", true)]
    [InlineData("duplicate", false)]
    [InlineData("duplicate", true)]
    public void BothProviders_RejectMissingDependenciesAndDuplicateInspector(string kind, bool microsoft)
    {
        using var test = new InspectorFixture();
        Assert.Throws<InvalidOperationException>(() => App.Builder(configuration => { if (microsoft) configuration.UseDI(); }).Setup(context =>
        {
            context.Services(services =>
            {
                if (kind != "commands") services.Singleton<EditorDocumentCommands>(_ => test.Owner.Commands);
                if (kind != "preview") services.Singleton<EditorPreview>(_ => test.Preview);
                if (kind == "factory") services.Singleton<IFactory<EditorInspectorView>>(_ => throw new NotSupportedException());
            });
            context.UseAetherEditorEditing();
            if (kind == "duplicate") context.UseAetherEditorEditing();
        }).Build());
        Assert.False(test.Owner.Commands.Read().Closed); Assert.Equal(0, test.Owner.Authoring.Products[0].Disposals);
        test.Property(DocumentData.Custom, "text").Value = "still alive"; test.Model.Apply();
        Assert.Equal("still alive", test.Owner.Document.Read().Content!.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["text"].Value.GetString());
    }

    private sealed class CountingStyle : IViewStyle<EditorInspectorView>
    {
        public int Configurations;
        public void Configure(IViewStyleBuilder<EditorInspectorView> styles)
        {
            Configurations++;
            styles.Root().Padding(19);
        }
    }
}
