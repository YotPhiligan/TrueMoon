using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Windows;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorHeaderConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_FactoryBorrowsSharedModelsAndStyleWhileWindowsOwnIndependentHeaders(bool microsoft)
    {
        using var source = new HeaderFixture(); var style = new CountingStyle();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            Register(context, source);
            context.Services(services => services.Singleton<IViewStyle<EditorHeaderView>>(_ => style));
            Assert.Same(context, context.UseAetherEditorHeader());
        }).Build();
        var factory = app.Services.Resolve<IFactory<EditorHeaderView, EditorHeaderArguments>>()!;
        Assert.Same(factory, app.Services.Resolve<IFactory<EditorHeaderView, EditorHeaderArguments>>());
        Assert.Same(style, app.Services.Resolve<IViewStyle<EditorHeaderView>>());
        var firstOverlays = new OverlayHost(new Panel()); var secondOverlays = new OverlayHost(new Panel());
        var first = factory.Create(new(firstOverlays, "First project", () => { }))!;
        var second = factory.Create(new(secondOverlays, "Second project", () => { }))!;
        var firstFrame = WindowFrame.CreateDefault("First", firstOverlays); firstFrame.DefaultTitleBar!.SetHeader(first);
        var secondFrame = WindowFrame.CreateDefault("Second", secondOverlays); secondFrame.DefaultTitleBar!.SetHeader(second);
        Assert.Equal(0, style.Configurations);
        Assert.Same(source.Model, first.DataContext); Assert.Same(source.Model, second.DataContext);
        Assert.NotSame(first.RunCommand, second.RunCommand);
        using var ui1 = new UiSession(firstFrame, new Surface(), new(1280, 800));
        using var ui2 = new UiSession(secondFrame, new Surface(), new(1280, 800));
        ui1.Update(); ui2.Update();
        var firstMenu = first.Menu; var secondMenu = second.Menu;
        var firstRun = first.RunButton; var secondRun = second.RunButton;
        Assert.NotSame(first, second); Assert.NotSame(firstMenu, secondMenu); Assert.NotSame(firstRun, secondRun);
        Assert.Equal(2, style.Configurations);
        Assert.Equal("First project", first.Caption.Value); Assert.Equal("Second project", second.Caption.Value);
        source.Refresh(SessionState.Stopped, candidate: true); ui1.Update(); ui2.Update();
        Assert.True(firstRun.IsEnabled); Assert.True(secondRun.IsEnabled);
        ui1.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed);
        Assert.True(firstMenu.IsDisposed); Assert.True(firstRun.IsDisposed);
        Assert.False(secondMenu.IsDisposed); Assert.False(secondRun.IsDisposed);
        source.Refresh(SessionState.Playing, candidate: true); ui2.Update();
        Assert.True(second.PauseButton.IsEnabled); Assert.False(secondRun.IsEnabled);
        app.Dispose(); Assert.False(second.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => source.Model.Select("green-cube"));
        ui2.Dispose(); Assert.True(second.IsDisposed); Assert.True(secondOverlays.IsDisposed);
        Assert.True(secondMenu.IsDisposed); Assert.True(secondRun.IsDisposed);
    }

    [Theory]
    [InlineData(nameof(EditorViewModel))]
    [InlineData(nameof(EditorDocumentViewModel))]
    [InlineData(nameof(EditorInspectorViewModel))]
    public void Registration_RejectsEachMissingPresentationBoundary(string missing)
    {
        using var source = new HeaderFixture();
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            Register(context, source, missing); context.UseAetherEditorHeader();
        }).Build());
        Assert.False(source.Header.IsDisposed); Assert.False(source.Owner.Commands.Read().Closed);
    }

    [Fact]
    public void Registration_RejectsNullContextAndDuplicateHeaderComposition()
    {
        Assert.Throws<ArgumentNullException>(() => HeaderConfigurationExtensions.UseAetherEditorHeader(null!));
        using var source = new HeaderFixture();
        Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            Register(context, source); context.UseAetherEditorHeader(); context.UseAetherEditorHeader();
        }).Build());
    }

    [Fact]
    public void DefaultStyle_IsRegisteredAndInvalidCreationArgumentsDoNotDisposeBorrowedModelsOrHost()
    {
        using var source = new HeaderFixture();
        using var app = App.Builder(_ => { }).Setup(context => { Register(context, source); context.UseAetherEditorHeader(); }).Build();
        Assert.IsType<EditorHeaderViewStyle>(app.Services.Resolve<IViewStyle<EditorHeaderView>>());
        var factory = app.Services.Resolve<IFactory<EditorHeaderView, EditorHeaderArguments>>()!;
        using var overlays = new OverlayHost(new Panel());
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new(null!, "Editor", () => { })));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new(overlays, null!, () => { })));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new(overlays, "Editor", null!)));
        Assert.False(overlays.IsDisposed); Assert.Empty(overlays.Children.Skip(1));
        source.Model.Select("green-cube"); source.Documents.PathDraft = "still usable";
        Assert.Equal("green-cube", source.Model.SelectedId); Assert.False(source.Owner.Commands.Read().Closed);
    }

    private static void Register(IAppConfigurationContext context, HeaderFixture source, string? missing = null) => context.Services(services =>
    {
        if (missing != nameof(EditorViewModel)) services.Singleton<EditorViewModel>(_ => source.Model);
        if (missing != nameof(EditorDocumentViewModel)) services.Singleton<EditorDocumentViewModel>(_ => source.Documents);
        if (missing != nameof(EditorInspectorViewModel)) services.Singleton<EditorInspectorViewModel>(_ => source.Transform);
    });
    private sealed class CountingStyle : IViewStyle<EditorHeaderView>
    { public int Configurations; public void Configure(IViewStyleBuilder<EditorHeaderView> styles) { Configurations++; new EditorHeaderViewStyle().Configure(styles); } }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { } public void Dispose() { }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
