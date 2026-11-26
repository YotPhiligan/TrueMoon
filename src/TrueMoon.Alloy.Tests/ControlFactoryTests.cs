using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public class ControlFactoryTests
{
    public static IEnumerable<object[]> Factories()
    {
        yield return [typeof(Button)];
        yield return [typeof(Text)];
        yield return [typeof(TextBox)];
        yield return [typeof(CheckBox)];
        yield return [typeof(ProgressBar)];
        yield return [typeof(Slider)];
        yield return [typeof(Image)];
        yield return [typeof(Panel)];
        yield return [typeof(ContentControl)];
        yield return [typeof(Border)];
        yield return [typeof(ScrollViewer)];
        yield return [typeof(VStack)];
        yield return [typeof(HStack)];
        yield return [typeof(Line)];
        yield return [typeof(Rectangle)];
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factories_MatchConstructorsAndKeepIndependentLifetime(Type type)
    {
        var constructor = type.GetConstructors().Single();
        using var expected = (Element)constructor.Invoke(constructor.GetParameters().Select(parameter => parameter.DefaultValue).ToArray());
        var registry = new UiControlFactories();
        var factory = typeof(UiControlFactories).GetMethod(nameof(UiControlFactories.Create), Type.EmptyTypes)!.MakeGenericMethod(type);
        using var first = (Element)factory.Invoke(registry, null)!;
        using var second = (Element)factory.Invoke(registry, null)!;
        Assert.IsType(type, first);
        Assert.IsType(type, second);
        Assert.NotSame(first, second);
        Assert.Equal(Defaults(expected), Defaults(first));
        Assert.Equal(Defaults(expected), Defaults(second));
        Assert.False(first.IsAttached);
        Assert.False(second.IsDisposed);
        first.Measure(new Size(300, 200), new FixedTextLayout());
        expected.Measure(new Size(300, 200), new FixedTextLayout());
        Assert.Equal(expected.DesiredSize, first.DesiredSize);
        first.Width = 220;
        using (var ui = new UiSession(first, new HeadlessSurface(), new UiViewport(300, 200)))
        {
            ui.SetTheme(Theme.Light);
            Assert.True(first.IsAttached);
            Assert.False(second.IsAttached);
            Assert.Same(Theme.Light, first.Theme);
            Assert.Equal(Defaults(expected), Defaults(second));
        }
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Null(second.Parent);
        // A call after disposal must still create a live element, never return a cached instance.
        using var third = (Element)factory.Invoke(registry, null)!;
        Assert.NotSame(first, third);
        Assert.NotSame(second, third);
        Assert.False(third.IsDisposed);
        Assert.Equal(Defaults(expected), Defaults(third));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<b>Текст 🧑‍💻</b>")]
    public void TextFactories_PreserveLiteralContent(string value)
    {
        using var view = new FactoryView(ui => ui.VStack(
            ui.Text(value).Width(200),
            ui.Button(value),
            ui.CheckBox(value).Configure(control => control.IsChecked = true)));
        using var session = new UiSession(view, new HeadlessSurface(), new UiViewport(300, 400));
        var stack = Assert.IsType<VStack>(view.Child);
        var text = Assert.IsType<Text>(stack.Items[0]);
        var button = Assert.IsType<Button>(stack.Items[1]);
        var check = Assert.IsType<CheckBox>(stack.Items[2]);
        Assert.Equal(value, text.Value);
        Assert.Equal(value, button.Value);
        Assert.Equal(value, check.Value);
        Assert.Equal(new Thickness(12, 8, 12, 8), button.Padding);
        Assert.Equal(new Thickness(36, 8, 12, 8), check.Padding);
        Assert.True(check.IsChecked);
        Assert.False(text.Focusable);
        Assert.True(button.Focusable);
    }

    [Fact]
    public void TextFactories_NullLabels_KeepConstructorValidation()
    {
        using var surface = new HeadlessSurface();
        foreach (var factory in new Func<IUiBuildContext<object>, Element>[] { ui => ui.Text(null!), ui => ui.Button(null!), ui => ui.CheckBox(null!) })
        {
            using var view = new FactoryView(factory);
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiSession(view, surface, new UiViewport(300, 400)));
            Assert.Null(view.Child);
        }
    }

    [Fact]
    public void FactoriesAndConstructors_MixedTreeRoutesInputAndOwnsChildren()
    {
        var activations = 0;
        var view = new FactoryView(ui => ui.VStack(
            ui.Border(ui.Text("Settings")),
            ui.TextBox().Width(200),
            ui.CheckBox("Notifications"),
            ui.Create<Slider>().Width(200),
            ui.Button("Apply").OnClick(() => activations++),
            new Text("Constructor label")).Width(240).Configure(stack => stack.Spacing = 8));
        VStack root;
        Element[] children;
        using (var ui = new UiSession(view, new HeadlessSurface(), new UiViewport(300, 400)))
        {
            root = Assert.IsType<VStack>(view.Child);
            children = root.Children.ToArray();
            var editor = Assert.IsType<TextBox>(children[1]);
            var toggle = Assert.IsType<CheckBox>(children[2]);
            var slider = Assert.IsType<Slider>(children[3]);
            var button = Assert.IsType<Button>(children[4]);
            Assert.True(ui.Update());
            ui.Focus(editor);
            Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "Привет 👋")).Handled);
            Assert.Equal("Привет 👋", editor.Value);
            Assert.Equal(editor.Value.Length, editor.CaretIndex);
            Click(ui, toggle);
            Assert.True(toggle.IsChecked);
            Click(ui, slider);
            Assert.Equal(.5f, slider.Value, 3);
            Click(ui, button);
            Assert.Equal(1, activations);
            Assert.False(button.IsPressed);
            Assert.Equal(children, root.Children);
            Assert.All(children, child => Assert.Same(root, child.Parent));
            Assert.True(ui.Update());
            Assert.False(ui.Update());
        }
        Assert.True(root.IsDisposed);
        Assert.All(children, child => { Assert.True(child.IsDisposed); Assert.False(child.IsAttached); });
        Assert.True(Assert.IsType<Border>(children[0]).Child!.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalFactories_CreateInteractiveControlsWithoutChangingSessionResolution(bool registeredArguments)
    {
        var factories = new UiControlFactories();
        factories.Register(new Factory<Button>(() => new("Session")));
        if (registeredArguments)
            factories.Register(new Factory<Button, string>(label => new($"Session: {label}")));
        var first = new Factory<Button>(() => new("First local"));
        var second = new Factory<Button>(() => new("Second local"));
        var withLabel = new Factory<Button, string>(label => new($"Local: {label}"));
        var activations = new List<Button>();
        var view = new FactoryView(ui =>
        {
            var root = ui.VStack(
                ui.Create(first).OnActivated((button, _) => activations.Add(button)),
                ui.Create(second),
                ui.Create(withLabel, "Apply").OnActivated((button, _) => activations.Add(button)),
                ui.Create<Button>());
            if (registeredArguments)
                root.Element.Add(ui.Create<Button, string>("Apply").Element);
            return root;
        });

        Button[] buttons;
        using (var session = new UiSession(view, new HeadlessSurface(), new UiViewport(300, 400),
            null, new UiDispatchOptions(), null, factories))
        {
            var stack = Assert.IsType<VStack>(view.Child);
            buttons = stack.Children.Select(child => Assert.IsType<Button>(child)).ToArray();
            string[] expected = registeredArguments
                ? ["First local", "Second local", "Local: Apply", "Session", "Session: Apply"]
                : ["First local", "Second local", "Local: Apply", "Session"];
            Assert.Equal(expected, buttons.Select(button => button.Value));
            session.Update();
            Click(session, buttons[0]);
            Click(session, buttons[2]);
            Assert.Equal(new[] { buttons[0], buttons[2] }, activations);
        }
        Assert.All(buttons, button => Assert.True(button.IsDisposed));
    }

    [Fact]
    public void LocalFactories_OmittedDraftsCauseRollbackAndAreDisposed()
    {
        using var surface = new HeadlessSurface();
        using var first = new Text("Unowned draft");
        using var second = new Text("Unowned argument draft");
        using var view = new FactoryView(ui =>
        {
            ui.Create(new Factory<Text>(() => first));
            ui.Create(new Factory<Text, string>(_ => second), "draft");
            return ui.VStack();
        });

        Assert.Throws<InvalidOperationException>(() => new UiSession(view, surface, new UiViewport(32, 32)));
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.Null(view.Child);
        Assert.False(view.IsAttached);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalFactories_RejectAnAttachedProductWithoutTakingItFromItsSession(bool withArguments)
    {
        using var surface = new HeadlessSurface();
        var clicks = 0;
        var owned = new Button("Owned").OnClick(() => clicks++);
        using var owner = new UiSession(owned, new HeadlessSurface(), new UiViewport(120, 60));
        owner.Update();
        var returnedForeignProduct = false;
        using var view = new FactoryView(ui =>
        {
            var product = withArguments
                ? ui.Create(new Factory<Button, string>(_ => owned), "foreign")
                : ui.Create(new Factory<Button>(() => owned));
            returnedForeignProduct = true;
            return product;
        });

        Assert.Throws<InvalidOperationException>(() => new UiSession(view, surface, new UiViewport(32, 32)));
        Assert.False(returnedForeignProduct);
        Assert.Null(view.Child);
        Assert.True(owned.IsAttached);
        Assert.False(owned.IsDisposed);
        Click(owner, owned);
        Assert.Equal(1, clicks);
        owner.Dispose();
        Assert.True(owned.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithChildren_RejectsOwnedLaterChildWithoutAdoptingEarlierChild(bool contextual)
    {
        using var candidate = new Text("Candidate");
        using var other = new Panel();
        var owned = new Text("Owned");
        other.Add(owned);
        UiControl<Panel, object> control = default;
        using var view = new FactoryView(ui => (control = ui.Create<Panel>()).Element);
        using var session = new UiSession(view, new HeadlessSurface(), new UiViewport(300, 400));
        var panel = control.Element;
        var existing = new Text("Existing");
        panel.Add(existing);

        Assert.Throws<InvalidOperationException>(() =>
        {
            if (contextual) control.WithChildren(candidate, owned);
            else panel.WithChildren(candidate, owned);
        });

        Assert.Same(existing, Assert.Single(panel.Children));
        Assert.Null(candidate.Parent);
        Assert.False(candidate.IsAttached);
        Assert.Same(other, owned.Parent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithChildren_ReportsObserverFailureAfterAdoptingEveryChild(bool contextual)
    {
        using var first = new Text("First");
        using var second = new Text("Second");
        UiControl<Panel, object> control = default;
        using var view = new FactoryView(ui => (control = ui.Create<Panel>()).Element);
        using var session = new UiSession(view, new HeadlessSurface(), new UiViewport(300, 400));
        var panel = control.Element;
        var failure = new InvalidOperationException("First child observer failed.");
        var secondNotified = false;
        first.AttachmentChanged += () =>
        {
            if (!first.IsAttached) return;
            Assert.Equal([first, second], panel.Children);
            Assert.True(second.IsAttached);
            throw failure;
        };
        second.AttachmentChanged += () => secondNotified = second.IsAttached;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
        {
            if (contextual) control.WithChildren(first, second);
            else panel.WithChildren(first, second);
        }));

        Assert.Same(panel, second.Parent);
        Assert.True(secondNotified);
    }

    [Fact]
    public void FailedDirectAttach_LeavesSuppliedSurfaceAndRootWithCaller()
    {
        using var surface = new HeadlessSurface();
        var failure = new InvalidOperationException("Cannot create layout");
        var factories = new UiControlFactories();
        factories.Register(new Factory<HStack>(() => throw failure));
        Text? draft = null;
        using var view = new FactoryView(ui =>
        {
            draft = ui.Create<Text>();
            return ui.Create<HStack>();
        });
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new UiSession(view, surface,
            new UiViewport(32, 32), null, new UiDispatchOptions(), null, factories)));
        Assert.True(Assert.IsType<Text>(draft).IsDisposed);
        Assert.Null(view.Child);
        Assert.False(view.IsAttached);
        Assert.False(view.IsDisposed);
        Assert.Equal(0, surface.Disposals);
    }

    private static void Click(UiSession ui, Element element)
    {
        var bounds = element.Bounds;
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        var x = bounds.X + bounds.Width / 2;
        var y = bounds.Y + bounds.Height / 2;
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, x, y)).Handled);
        var released = ui.HandleInput(new UiInput(InputKind.PointerUp, x, y));
        Assert.True(released.Handled);
        Assert.False(released.PointerCaptured);
    }

    private sealed class FactoryView : View
    {
        public FactoryView(Func<IUiBuildContext<object>, Element> factory) => Content(factory);
    }

    private static object?[] Defaults(Element element) =>
    [
        element.Width, element.Height, element.MinWidth, element.MinHeight, element.MaxWidth, element.MaxHeight,
        element.Padding, element.Margin, element.HorizontalAlignment, element.VerticalAlignment,
        element.IsVisible, element.IsEnabled, element.Focusable, element.Foreground, element.Background,
        element.FontSize, element.FontFamily, element.Theme, element.Style, element.Children.Count,
        (element as Text)?.Value, (element as TextBox)?.Value, (element as CheckBox)?.IsChecked,
        (element as ProgressBar)?.Value, (element as Image)?.Source, (element as ContentControl)?.Child,
        (element as ScrollViewer)?.Offset, (element as StackBase)?.Spacing
    ];

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) => TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(root, new RecordingDrawingContext(), presenters);
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
}
