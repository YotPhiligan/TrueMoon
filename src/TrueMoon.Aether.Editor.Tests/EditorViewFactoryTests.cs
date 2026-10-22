using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewFactoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiFactory_BorrowsSharedModelAndStyleWhileSessionsOwnIndependentViews(bool di)
    {
        var workspace = new EditorWorkspace(EditorScene.Demo()); var style = new CountingStyle(); var models = 0;
        var app = App.Builder(configuration => { if (di) configuration.UseDI(); }).Setup(context => context.Services(services => services
            .Singleton<EditorWorkspace>(_ => workspace)
            .Singleton<EditorCommands>(_ => new((_, _) => Task.CompletedTask, CancellationToken.None))
            .Singleton<EditorViewModel>(resolver => { models++; return new(resolver.Resolve<EditorWorkspace>()!, resolver.Resolve<EditorCommands>()!); })
            .Singleton<IViewStyle<EditorView>>(_ => style)
            .Singleton<IFactory<EditorView>>(resolver => new Factory<EditorView>(() =>
                new(resolver.Resolve<EditorViewModel>()!, resolver.Resolve<IViewStyle<EditorView>>()!))))).Build();
        var factory = app.Services.Resolve<IFactory<EditorView>>()!; var model = app.Services.Resolve<EditorViewModel>()!;
        var first = factory.Create()!; var second = factory.Create()!;
        var firstOwned = new CleanupCounter(); var secondOwned = new CleanupCounter(); first.Own(firstOwned); second.Own(secondOwned);
        using var firstUi = Create(first); using var secondUi = Create(second); firstUi.Update(); secondUi.Update();
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext); Assert.Equal(1, models); Assert.Equal(2, style.Configurations);
        Assert.NotSame(first, second); Assert.NotSame(first.Canvas, second.Canvas); Assert.NotSame(first.Entities["red-cube"], second.Entities["red-cube"]);
        model.Select("green-cube"); firstUi.Update(); secondUi.Update();
        Assert.Equal(ControlTone.Accent, first.Entities["green-cube"].Tone); Assert.Equal(ControlTone.Accent, second.Entities["green-cube"].Tone);
        firstUi.Dispose(); Assert.Equal(1, firstOwned.Disposals); Assert.Equal(0, secondOwned.Disposals);
        model.Select("switch"); secondUi.Update(); Assert.Equal(ControlTone.Accent, second.Entities["switch"].Tone);
        app.Dispose();
        Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.Equal(0, secondOwned.Disposals);
        Assert.Throws<ObjectDisposedException>(() => model.Select("red-cube"));
        secondUi.Dispose(); app.Dispose();
        Assert.Equal(1, firstOwned.Disposals); Assert.Equal(1, secondOwned.Disposals); Assert.Equal(2, style.Configurations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryFailure_DisposesUnpublishedViewAndPreservesBorrowedModelAndErrors(bool cleanupFailure)
    {
        using var model = new EditorViewModel(new(EditorScene.Demo()), new EditorCommands((_, _) => Task.CompletedTask, CancellationToken.None));
        var expected = new IOException("style creation failed"); var cleanup = new IOException("unpublished view cleanup failed");
        EditorView unpublished = null!; Element content = null!;
        var resource = new CleanupCounter(cleanupFailure ? cleanup : null);
        var style = new FailingStyle(view => { unpublished = view; content = view.Child!; view.Own(resource); }, expected);
        IFactory<EditorView> factory = new Factory<EditorView>(() => new(model, style));
        var error = Record.Exception(() => factory.Create());
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions,
            cause => Assert.Same(expected, cause), cause => Assert.Same(cleanup, cause));
        else Assert.Same(expected, error);
        Assert.True(unpublished.IsDisposed); Assert.True(content.IsDisposed); Assert.True(unpublished.Canvas.IsDisposed); Assert.Equal(1, resource.Disposals);
        model.Select("green-cube"); Assert.Equal("green-cube", model.SelectedId); Assert.True(model.CanBuild);
    }

    private static UiSession Create(Element root) => new(root, new Surface(), new(1280, 800));
    private sealed class CountingStyle : IViewStyle<EditorView>
    {
        public int Configurations;
        public void Configure(IViewStyleBuilder<EditorView> styles) { Configurations++; new EditorViewStyle().Configure(styles); }
    }
    private sealed class FailingStyle(Action<EditorView> capture, Exception error) : IViewStyle<EditorView>
    {
        public void Configure(IViewStyleBuilder<EditorView> styles)
        { styles.Part(view => { capture(view); return view.Error; }).FontSize(13); throw error; }
    }
    private sealed class CleanupCounter(Exception? error = null) : IDisposable
    {
        public int Disposals;
        public void Dispose() { Disposals++; if (error != null) throw error; }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
