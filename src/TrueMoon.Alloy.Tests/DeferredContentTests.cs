using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class DeferredContentTests
{
    [Fact]
    public void FirstAttach_BuildsThroughFactoriesBeforeLayout()
    {
        var created = new List<Element>();
        var initial = new SearchModel("initial");
        var current = new SearchModel("before attach");
        using var view = ViewCreation.Prepare(new SearchView(initial), new SearchStyle());
        Assert.Same(initial, view.DataContext);
        view.DataContext = current;
        Assert.Null(view.Child);
        Assert.Empty(created);
        Assert.Equal(0, current.Subscribers);
        Assert.False(view.TryGetPart<TextBox>("Search", out _));

        using var ui = CreateSession(view, CreateFactories(created));
        TextBox editor = view.Search;
        HStack layout = view.SearchLayout;
        Assert.Same(view.Child, layout);
        Assert.Collection(created,
            node => Assert.IsType<Text>(node),
            node => Assert.Same(editor, node),
            node => Assert.Same(view.Child, Assert.IsType<HStack>(node)));
        Assert.Same(editor, view.PreparedEditor);
        Assert.True(editor.IsAttached);
        Assert.Equal(18, editor.FontSize);
        Assert.Equal("before attach", editor.Value);
        Assert.Equal(0, initial.Subscribers);
        Assert.Equal(1, current.Subscribers);
        Assert.True(ui.Update());
        Assert.True(editor.Bounds.Width > 0);
    }

    [Fact]
    public void Reattach_ReusesLateInsertedTreeAndRestoresOneSubscription()
    {
        var created = new List<Element>();
        var model = new SearchModel("first");
        using var view = new SearchView(model);
        using var ui = CreateSession(new Panel(), CreateFactories(created));
        var host = Assert.IsType<Panel>(ui.Root);

        host.Add(view);
        var root = view.Child;
        TextBox editor = view.Search;
        Assert.Equal("first", editor.Value);
        Assert.Equal(1, model.Subscribers);

        host.Items.Remove(view);
        Assert.False(editor.IsAttached);
        Assert.Equal(0, model.Subscribers);
        model.SearchText = "detached";
        ui.Update();
        Assert.Equal("first", editor.Value);

        host.Add(view);
        Assert.Same(root, view.Child);
        Assert.Same(editor, view.GetRequiredPart<TextBox>("Search"));
        Assert.Equal(3, created.Count);
        Assert.Equal(1, view.Preparations);
        Assert.Equal(1, model.Subscribers);
        Assert.Equal("detached", editor.Value);
    }

    [Fact]
    public void DataContextReplacement_RoutesInputToNewModelAndDiscardsOldNotification()
    {
        var created = new List<Element>();
        var first = new SearchModel("first");
        var second = new SearchModel("");
        using var ui = CreateSession(new SearchView(first), CreateFactories(created));
        var view = Assert.IsType<SearchView>(ui.Root);
        TextBox editor = view.Search;
        ui.Update();
        ui.Focus(editor);
        first.SearchText = "queued old";
        Assert.Equal("first", editor.Value);

        view.DataContext = second;
        ui.Update();
        Assert.Equal("", editor.Value);
        Assert.True(editor.IsFocused);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(1, second.Subscribers);
        Assert.Same(editor, view.GetRequiredPart<TextBox>("Search"));
        Assert.Equal(3, created.Count);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "new input")).Handled);
        Assert.Equal("new input", second.SearchText);
        Assert.Equal("queued old", first.SearchText);
    }

    [Fact]
    public void FailedFactory_DisposesDraftsAndPreservesMountedContent()
    {
        var created = new List<Element>();
        Func<HStack> fail = () => throw new InvalidOperationException("Failed layout factory");
        using var view = new SearchView(new SearchModel("draft"));
        var host = new ContentControl();
        var previous = new Text("mounted");
        host.SetContent(previous);
        using var ui = CreateSession(host, CreateFactories(created, fail));

        var error = Assert.Throws<InvalidOperationException>(() => host.SetContent(view));
        Assert.Equal("Failed layout factory", error.Message);
        Assert.Collection(created,
            node => Assert.True(Assert.IsType<Text>(node).IsDisposed),
            node => Assert.True(Assert.IsType<TextBox>(node).IsDisposed));
        Assert.Same(previous, host.Child);
        Assert.True(previous.IsAttached);
        Assert.Null(view.Parent);
        Assert.Null(view.Child);
        Assert.False(view.IsAttached);
    }

    [Fact]
    public void LatePreparation_BlocksReentrantMutationOfReceivingTree()
    {
        var previous = new Text("mounted");
        var host = new ContentControl();
        host.SetContent(previous);
        var factories = CreateFactories([], () =>
        {
            host.SetContent(null);
            return new HStack();
        });
        using var ui = CreateSession(host, factories);
        using var view = new SearchView(new SearchModel("candidate"));

        var error = Assert.Throws<InvalidOperationException>(() => host.SetContent(view));

        Assert.Contains("reentrant", error.Message);
        Assert.Same(previous, host.Child);
        Assert.True(previous.IsAttached);
        Assert.Null(view.Parent);
        Assert.Null(view.Child);
    }

    [Fact]
    public void MissingFactoryRegistry_RejectsBeforePreparationAndAllowsFirstAttachWithFactories()
    {
        using var view = new SearchView(new SearchModel("ready"));
        var error = Assert.Throws<InvalidOperationException>(() => view.Attach(static () => { }, static action => action()));
        Assert.Contains("factory registry", error.Message);
        Assert.Null(view.Child);
        Assert.False(view.IsAttached);

        using var ui = CreateSession(view, CreateFactories([]));
        Assert.Equal(1, view.Preparations);
        Assert.Equal("ready", view.Search.Value);
    }

    [Fact]
    public void DisposeBeforeAttach_DoesNotBuildContent()
    {
        var model = new SearchModel("unused");
        var view = new SearchView(model);
        Assert.Null(view.Child);

        view.Dispose();

        Assert.True(view.IsDisposed);
        Assert.Null(view.Child);
        Assert.Equal(0, view.Preparations);
        Assert.Equal(0, model.Subscribers);
    }

    private static UiControlFactories CreateFactories(List<Element> created, Func<HStack>? stack = null)
    {
        var factories = new UiControlFactories();
        Register(() => new Text());
        Register(() => new TextBox());
        Register(stack ?? (() => new HStack()));
        return factories;

        void Register<TControl>(Func<TControl> create) where TControl : Element =>
            factories.Register(new Factory<TControl>(() =>
            {
                var control = create();
                created.Add(control);
                return control;
            }));
    }

    private static UiSession CreateSession(Element root, UiControlFactories factories)
        => new(root, new Surface(), new UiViewport(360, 140), null, new UiDispatchOptions(), null, factories);

    private sealed class SearchStyle : IViewStyle<SearchView>
    {
        public void Configure(IViewStyleBuilder<SearchView> styles) =>
            styles.Part(view => view.Search).FontSize(18);
    }

    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }
}

internal sealed class SearchView : View<SearchModel>
{
    public SearchView(SearchModel model)
        => Content(model, static ui => ui.HStack(
            ui.Text("Search"),
            ui.Create<TextBox>()
                .Named("Search")
                .BindValueTwoWay(data => data.SearchText)).Named("SearchLayout"));

    public TextBox? PreparedEditor { get; private set; }
    public int Preparations { get; private set; }

    protected override void OnContentPrepared()
    {
        PreparedEditor = this.Search;
        Preparations++;
    }
}

internal sealed class SearchModel(string text) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private string _text = text;

    public string SearchText
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            _changed?.Invoke(this, new(nameof(SearchText)));
        }
    }

    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}
