using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorDiagnosticsConfigurationTests
{
    [Fact]
    public void ExistingPublicConstructors_PreserveBinaryCallSignatures()
    {
        Assert.NotNull(typeof(EditorView).GetConstructor([typeof(EditorViewModel), typeof(IViewStyle<EditorView>),
            typeof(IFactory<EditorDocumentView>), typeof(IFactory<EditorTransformView>), typeof(bool),
            typeof(IFactory<EditorStatusBarView, EditorStatusBarArguments>), typeof(IFactory<EditorHierarchyView>), typeof(IFactory<EditorInspectorView>)]));
        Assert.NotNull(typeof(EditorHeaderView).GetConstructor([typeof(EditorViewModel), typeof(EditorDocumentViewModel), typeof(EditorInspectorViewModel),
            typeof(IViewStyle<EditorHeaderView>), typeof(EditorHeaderArguments)]));
        Assert.NotNull(typeof(EditorLayout).GetConstructor([typeof(Element), typeof(Element), typeof(Element), typeof(Element), typeof(Element), typeof(bool), typeof(float)]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_ResolveLateCollectorAndCreateParentOwnedIndependentViews(bool microsoft)
    {
        var processes = new EditorDiagnosticContextTests.Context();
        using var app = App.Builder(configuration => { if (microsoft) configuration.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<TrueMoon.Aether.Diagnostics.IProcessDiagnosticsSource>(_ => processes));
            Assert.Same(context, context.UseAetherEditorDiagnostics());
            context.UseDiagnostics(new DiagnosticPipelineOptions { MemoryCapacity = 16 });
        }).Build();
        var model = app.Services.Resolve<EditorDiagnosticsViewModel>()!; var factory = app.Services.Resolve<IFactory<EditorDiagnosticsView>>()!;
        Assert.True(model.CollectorAvailable); model.IsOpen = true;
        var source = app.Services.Resolve<IEventsSourceFactory>()!.Create("TrueMoon.TestConsole");
        source.Write(DiagnosticLevel.Warning, 1202, "Console.Integrated", "live parent event"); model.Refresh();
        Assert.NotNull(model.CollectorHealth); Assert.True(model.Processes!.RuntimeAvailable); Assert.Single(model.Processes.Processes);
        Assert.Contains(model.Entries, entry => entry.Record.EventName == "Console.Integrated");
        var first = factory.Create(); var second = factory.Create(); Assert.NotNull(first); Assert.NotNull(second); Assert.NotSame(first, second); Assert.Same(model, first.DataContext);
        using var ui1 = new UiSession(first, new EditorDiagnosticsViewTests.Surface(), new(1280, 360));
        using var ui2 = new UiSession(second, new EditorDiagnosticsViewTests.Surface(), new(1280, 360)); ui1.Update(); ui2.Update();
        ui1.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed);
        source.Write(DiagnosticLevel.Warning, 1203, "Console.StillLive", "after first close"); model.Refresh(); ui2.Update();
        Assert.Contains(second.Events.Items, item => item.Text.Contains("Console.StillLive"));
        app.Dispose(); Assert.False(second.IsDisposed); Assert.Throws<ObjectDisposedException>(model.Refresh);
        Assert.Equal(1, processes.Disposals);
        ui2.Dispose(); Assert.True(second.IsDisposed);
    }

    [Fact]
    public void MissingCollectorAndDuplicateRegistration_AreExplicitWithoutCreatingLogger()
    {
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        { context.UseAetherEditorDiagnostics(); context.UseAetherEditorDiagnostics(); }).Build());
        using var app = App.Builder(_ => { }).Setup(context => context.UseAetherEditorDiagnostics()).Build();
        var model = app.Services.Resolve<EditorDiagnosticsViewModel>()!; Assert.False(model.CollectorAvailable);
        Assert.Null(app.Services.GetService(typeof(IDiagnosticRecordReader))); model.Refresh(); Assert.Empty(model.Entries);
    }
}
