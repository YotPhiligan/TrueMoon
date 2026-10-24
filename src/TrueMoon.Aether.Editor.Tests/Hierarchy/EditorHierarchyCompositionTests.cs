using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

public sealed class EditorHierarchyCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_ShareModelAndCustomStyleWhileFactoriesCreateIndependentParentOwnedTrees(bool microsoft)
    {
        using var test = new HierarchyFixture(); var style = new CountingStyle();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorWorkspace>(_ => test.Workspace)
                .Singleton<EditorDocumentCommands>(_ => test.Owner.Commands)
                .Singleton<EditorInspectorViewModel>(_ => test.Transform)
                .Singleton<IViewStyle<EditorHierarchyView>>(_ => style));
            context.UseAetherEditorHierarchy();
        }).Build();
        var model = app.Services.Resolve<EditorHierarchyViewModel>()!; model.Refresh();
        var factory = app.Services.Resolve<IFactory<EditorHierarchyView>>()!;
        var first = Assert.IsType<EditorHierarchyView>(factory.Create()); var second = Assert.IsType<EditorHierarchyView>(factory.Create());
        using var ui1 = DocumentUiFixture.Ui(first); using var ui2 = DocumentUiFixture.Ui(second); ui1.Update(); ui2.Update();
        Assert.Same(model, app.Services.Resolve<EditorHierarchyViewModel>()); Assert.Same(factory, app.Services.Resolve<IFactory<EditorHierarchyView>>());
        Assert.Same(test.Workspace, app.Services.Resolve<EditorWorkspace>()); Assert.Same(test.Transform, app.Services.Resolve<EditorInspectorViewModel>());
        Assert.Same(test.Owner.Commands, app.Services.Resolve<EditorDocumentCommands>()); Assert.Same(style, app.Services.Resolve<IViewStyle<EditorHierarchyView>>());
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext); Assert.NotSame(first, second); Assert.NotSame(first.Tree, second.Tree);
        Assert.Equal(2, style.Configurations);
        first.Tree.Select(HierarchyFixture.Parent); ui1.Update(); ui2.Update();
        Assert.Equal(HierarchyFixture.Parent, second.Tree.SelectedId); Assert.Equal("2", test.Transform.Fields[0].Value);
        ui1.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.False(test.Owner.Commands.Read().Closed);
        model.Select(HierarchyFixture.Child); Assert.True(model.Move(HierarchyFixture.Child, null)); ui2.Update();
        Assert.Null(second.Tree.Items.Single(item => item.Id == HierarchyFixture.Child).ParentId); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        app.Dispose();
        Assert.Throws<ObjectDisposedException>(model.Refresh); Assert.False(second.IsDisposed); Assert.True(test.Owner.Commands.Read().Closed);
        Assert.Equal(1, test.Owner.Authoring.Products[0].Disposals);
        ui2.Dispose(); app.Dispose(); Assert.True(second.Tree.IsDisposed); Assert.Equal(2, style.Configurations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_DefaultStyleAndFactoryAreRegisteredWithoutOwningCreatedViews(bool microsoft)
    {
        using var test = new HierarchyFixture();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<EditorWorkspace>(_ => test.Workspace)
                .Singleton<EditorDocumentCommands>(_ => test.Owner.Commands)
                .Singleton<EditorInspectorViewModel>(_ => test.Transform));
            context.UseAetherEditorHierarchy();
        }).Build();
        Assert.IsType<EditorHierarchyViewStyle>(app.Services.Resolve<IViewStyle<EditorHierarchyView>>());
        app.Services.Resolve<EditorHierarchyViewModel>()!.Refresh();
        var view = app.Services.Resolve<IFactory<EditorHierarchyView>>()!.Create()!;
        using var ui = DocumentUiFixture.Ui(view); ui.Update();
        Assert.Equal(HierarchyFixture.Child, view.Tree.SelectedId); Assert.True(view.Tree.MoveEnabled);
        app.Dispose(); Assert.False(view.IsDisposed); Assert.Equal(1, test.Owner.Authoring.Products[0].Disposals);
    }

    [Theory]
    [InlineData("workspace")]
    [InlineData("commands")]
    [InlineData("transform")]
    [InlineData("duplicate")]
    public void Registration_RejectsEachMissingBoundaryAndDuplicateComposition(string missing)
    {
        using var test = new HierarchyFixture();
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            context.Services(services =>
            {
                if (missing != "workspace") services.Singleton<EditorWorkspace>(_ => test.Workspace);
                if (missing != "commands") services.Singleton<EditorDocumentCommands>(_ => test.Owner.Commands);
                if (missing != "transform") services.Singleton<EditorInspectorViewModel>(_ => test.Transform);
            });
            context.UseAetherEditorHierarchy();
            if (missing == "duplicate") context.UseAetherEditorHierarchy();
        }).Build());
        Assert.False(test.Owner.Commands.Read().Closed); Assert.True(test.Transform.CanEdit);
    }

    private sealed class CountingStyle : IViewStyle<EditorHierarchyView>
    {
        public int Configurations;
        public void Configure(IViewStyleBuilder<EditorHierarchyView> styles) { Configurations++; new EditorHierarchyViewStyle().Configure(styles); }
    }
}
