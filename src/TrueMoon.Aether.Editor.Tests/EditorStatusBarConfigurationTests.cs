using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorStatusBarConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_FactoriesBorrowSharedModelsAndStylesWhileConsumersOwnIndependentProducts(bool microsoft)
    {
        using var source = new HeaderFixture();
        var barStyle = new CountingBarStyle(); var popupStyle = new CountingPopupStyle();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            Register(context, source);
            context.Services(services =>
            {
                services.Singleton<IViewStyle<EditorStatusBarView>>(_ => barStyle);
                services.Singleton<IViewStyle<EditorStatusPopupView>>(_ => popupStyle);
            });
            Assert.Same(context, context.UseAetherEditorStatusBar());
        }).Build();
        var factory = app.Services.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>()!;
        var popups = app.Services.Resolve<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>()!;
        var model = app.Services.Resolve<EditorStatusBarViewModel>()!;
        Assert.Same(factory, app.Services.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>());
        Assert.Same(popups, app.Services.Resolve<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>());
        Assert.Same(model, app.Services.Resolve<EditorStatusBarViewModel>());
        Assert.Same(barStyle, app.Services.Resolve<IViewStyle<EditorStatusBarView>>());
        Assert.Same(popupStyle, app.Services.Resolve<IViewStyle<EditorStatusPopupView>>());
        using var firstHost = new OverlayHost(new Panel());
        using var secondHost = new OverlayHost(new Panel());
        using var first = factory.Create(new(firstHost))!;
        using var second = factory.Create(new(secondHost))!;
        using var firstPopup = popups.Create(new(false))!;
        using var secondPopup = popups.Create(new(true))!;
        ((Panel)firstHost.Content).Add(first); ((Panel)secondHost.Content).Add(second);
        using var firstUi = new UiSession(firstHost, new Surface(), new(1280, 800));
        using var secondUi = new UiSession(secondHost, new Surface(), new(1280, 800));
        firstUi.Update(); secondUi.Update();
        firstHost.Open(first.DetailsButton, firstPopup, first.DetailsButton.Bounds, firstUi, first.DetailsButton);
        secondHost.Open(second.PreviewButton, secondPopup, second.PreviewButton.Bounds, secondUi, second.PreviewButton);
        firstUi.Update(); secondUi.Update();
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext);
        Assert.Same(model, firstPopup.DataContext); Assert.Same(model, secondPopup.DataContext);
        Assert.NotSame(first, second); Assert.NotSame(first.SummaryButton, second.SummaryButton);
        Assert.NotSame(first.Bar, second.Bar); Assert.NotSame(firstPopup.Body, secondPopup.Body);
        Assert.Equal("Сведения о редакторе", firstPopup.Title.Value); Assert.Equal("Edit Preview", secondPopup.Title.Value);
        Assert.Equal(2, barStyle.Configurations); Assert.Equal(2, popupStyle.Configurations);
        source.Refresh(SessionState.Playing, candidate: true);
        firstUi.Update(); secondUi.Update();
        Assert.Equal(EditorStatusKind.Running, model.Kind);
        Assert.Equal("Симуляция", first.SummaryButton.Value); Assert.Equal("Симуляция", second.SummaryButton.Value);
        firstUi.Dispose();
        Assert.True(first.SummaryButton.IsDisposed); Assert.True(firstPopup.Error.IsDisposed);
        Assert.False(second.IsDisposed); Assert.False(secondPopup.IsDisposed);
        Assert.True(firstHost.IsDisposed); Assert.False(secondHost.IsDisposed);
        Assert.Equal(0, barStyle.Disposals); Assert.Equal(0, popupStyle.Disposals);
        source.Refresh(SessionState.Paused, candidate: true);
        secondUi.Update();
        Assert.Equal("Симуляция на паузе", second.SummaryButton.Value);
        source.Documents.PathDraft = "borrowed models remain usable.aetherscene";
        Assert.Equal("borrowed models remain usable.aetherscene", source.Documents.PathDraft);
        Assert.False(source.Owner.Commands.Read().Closed);
        app.Dispose();
        Assert.False(second.IsDisposed); Assert.False(secondPopup.IsDisposed);
        Assert.Equal(1, barStyle.Disposals); Assert.Equal(1, popupStyle.Disposals);
        Assert.Throws<ObjectDisposedException>(() => model.ShowPreview());
        secondUi.Dispose();
        Assert.True(second.SummaryButton.IsDisposed); Assert.True(secondPopup.Error.IsDisposed);
    }

    [Theory]
    [InlineData(nameof(EditorViewModel))]
    [InlineData(nameof(EditorDocumentViewModel))]
    [InlineData(nameof(EditorInspectorViewModel))]
    public void Registration_RejectsEachMissingPresentationBoundary(string missing)
    {
        using var source = new HeaderFixture();
        var error = Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            Register(context, source, missing); context.UseAetherEditorStatusBar();
        }).Build());
        Assert.Equal("Configure editor presentation and editing before the status bar.", error.Message);
        Assert.False(source.Header.IsDisposed); Assert.False(source.Owner.Commands.Read().Closed);
    }

    [Fact]
    public void Registration_RejectsNullContextAndRepeatedComposition()
    {
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => StatusBarConfigurationExtensions.UseAetherEditorStatusBar(null!)).ParamName);
        using var source = new HeaderFixture();
        var error = Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            Register(context, source); context.UseAetherEditorStatusBar(); context.UseAetherEditorStatusBar();
        }).Build());
        Assert.Equal("Editor status bar is already registered.", error.Message);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("bar-factory")]
    [InlineData("popup-factory")]
    public void Registration_RejectsEachPreexistingStatusBoundary(string existing)
    {
        using var source = new HeaderFixture();
        var error = Assert.Throws<InvalidOperationException>(() => App.Builder(_ => { }).Setup(context =>
        {
            Register(context, source);
            context.Services(services =>
            {
                switch (existing)
                {
                    case "model":
                        services.Singleton<EditorStatusBarViewModel>(_ => new(source.Model, source.Documents, source.Transform)); break;
                    case "bar-factory":
                        services.Singleton<IFactory<EditorStatusBarView, EditorStatusBarArguments>>(_ =>
                            new Factory<EditorStatusBarView, EditorStatusBarArguments>(arguments => throw new InvalidOperationException("must not activate"))); break;
                    case "popup-factory":
                        services.Singleton<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>(_ =>
                            new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(arguments => throw new InvalidOperationException("must not activate"))); break;
                }
            });
            context.UseAetherEditorStatusBar();
        }).Build());
        Assert.Equal("Editor status bar is already registered.", error.Message);
        Assert.False(source.Owner.Commands.Read().Closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_DefaultStylesAndInvalidArgumentsPreserveBorrowedModelsAndHost(bool microsoft)
    {
        using var source = new HeaderFixture();
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        { Register(context, source); context.UseAetherEditorStatusBar(); }).Build();
        Assert.IsType<EditorStatusBarViewStyle>(app.Services.Resolve<IViewStyle<EditorStatusBarView>>());
        Assert.IsType<EditorStatusPopupViewStyle>(app.Services.Resolve<IViewStyle<EditorStatusPopupView>>());
        var factory = app.Services.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>()!;
        var popups = app.Services.Resolve<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>()!;
        var model = app.Services.Resolve<EditorStatusBarViewModel>()!;
        using var host = new OverlayHost(new Panel());
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new(null!)));
        Assert.Throws<ArgumentNullException>(() => popups.Create(null!));
        Assert.False(host.IsDisposed); Assert.Single(host.Children); Assert.Null(host.Popup);
        source.Refresh(SessionState.Playing, candidate: true);
        Assert.Equal(EditorStatusKind.Running, model.Kind);
        source.Documents.PathDraft = "next.aetherscene"; source.Transform.Refresh(null);
        Assert.Equal(SessionState.Playing, source.Model.RuntimeState); Assert.Equal("next.aetherscene", source.Documents.PathDraft);
        Assert.False(source.Owner.Commands.Read().Closed);
        using var bar = factory.Create(new(host))!; using var popup = popups.Create(new(false))!;
        Assert.Same(model, bar.DataContext); Assert.Same(model, popup.DataContext);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void FactoryStyleFailure_DisposesUnpublishedProductPreservesErrorsAndBorrowedModels(bool microsoft, bool popup, bool cleanupFailure)
    {
        using var source = new HeaderFixture();
        var failure = new IOException("status style failed"); var cleanup = new IOException("status cleanup failed");
        var resource = new Resource(cleanupFailure ? cleanup : null); Element unpublished = null!;
        using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            Register(context, source);
            context.Services(services =>
            {
                if (popup) services.Singleton<IViewStyle<EditorStatusPopupView>>(_ => new FailingPopupStyle(view => { unpublished = view; view.Own(resource); }, failure));
                else services.Singleton<IViewStyle<EditorStatusBarView>>(_ => new FailingBarStyle(view => { unpublished = view; view.Own(resource); }, failure));
            });
            context.UseAetherEditorStatusBar();
        }).Build();
        var model = app.Services.Resolve<EditorStatusBarViewModel>()!;
        using var host = new OverlayHost(new Panel());
        var error = Record.Exception(() =>
        {
            if (popup) app.Services.Resolve<IFactory<EditorStatusPopupView, EditorStatusPopupArguments>>()!.Create(new(false));
            else app.Services.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>()!.Create(new(host));
        });
        if (cleanupFailure)
            Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions,
                value => Assert.Same(failure, value), value => Assert.Same(cleanup, value));
        else Assert.Same(failure, error);
        Assert.NotNull(unpublished); Assert.True(unpublished.IsDisposed);
        Assert.All(Tree(unpublished), element => Assert.True(element.IsDisposed));
        Assert.Equal(1, resource.Disposals); Assert.False(host.IsDisposed); Assert.Null(host.Popup); Assert.Single(host.Children);
        source.Refresh(SessionState.Playing, candidate: true);
        Assert.Equal(EditorStatusKind.Running, model.Kind);
        source.Documents.PathDraft = "after failure.aetherscene"; source.Transform.Refresh(null);
        Assert.Equal(SessionState.Playing, source.Model.RuntimeState); Assert.Equal("after failure.aetherscene", source.Documents.PathDraft);
        Assert.False(source.Owner.Commands.Read().Closed);
    }

    private static IEnumerable<Element> Tree(Element element)
    { yield return element; foreach (var child in element.Children) foreach (var descendant in Tree(child)) yield return descendant; }

    private static void Register(IAppConfigurationContext context, HeaderFixture source, string? missing = null) => context.Services(services =>
    {
        if (missing != nameof(EditorViewModel)) services.Singleton<EditorViewModel>(_ => source.Model);
        if (missing != nameof(EditorDocumentViewModel)) services.Singleton<EditorDocumentViewModel>(_ => source.Documents);
        if (missing != nameof(EditorInspectorViewModel)) services.Singleton<EditorInspectorViewModel>(_ => source.Transform);
    });

    private sealed class CountingBarStyle : IViewStyle<EditorStatusBarView>, IDisposable
    {
        public int Configurations, Disposals;
        public void Configure(IViewStyleBuilder<EditorStatusBarView> styles) { Configurations++; new EditorStatusBarViewStyle().Configure(styles); }
        public void Dispose() => Disposals++;
    }
    private sealed class CountingPopupStyle : IViewStyle<EditorStatusPopupView>, IDisposable
    {
        public int Configurations, Disposals;
        public void Configure(IViewStyleBuilder<EditorStatusPopupView> styles) { Configurations++; new EditorStatusPopupViewStyle().Configure(styles); }
        public void Dispose() => Disposals++;
    }
    private sealed class FailingBarStyle(Action<EditorStatusBarView> capture, Exception failure) : IViewStyle<EditorStatusBarView>
    { public void Configure(IViewStyleBuilder<EditorStatusBarView> styles) { styles.Part(view => { capture(view); return view.SummaryButton; }).FontSize(12); throw failure; } }
    private sealed class FailingPopupStyle(Action<EditorStatusPopupView> capture, Exception failure) : IViewStyle<EditorStatusPopupView>
    { public void Configure(IViewStyleBuilder<EditorStatusPopupView> styles) { styles.Part(view => { capture(view); return view.Title; }).FontSize(12); throw failure; } }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
