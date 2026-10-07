using System.Reflection;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public class ControlFactoryTests
{
    public static IEnumerable<object[]> Factories()
    {
        yield return [typeof(Button), nameof(Button.Simple)];
        yield return [typeof(Text), nameof(Text.Plain)];
        yield return [typeof(TextBox), nameof(TextBox.Create)];
        yield return [typeof(CheckBox), nameof(CheckBox.Create)];
        yield return [typeof(ProgressBar), nameof(ProgressBar.Create)];
        yield return [typeof(Slider), nameof(Slider.Create)];
        yield return [typeof(Image), nameof(Image.Create)];
        yield return [typeof(Panel), nameof(Panel.Create)];
        yield return [typeof(ContentControl), nameof(ContentControl.Create)];
        yield return [typeof(Border), nameof(Border.Create)];
        yield return [typeof(ScrollViewer), nameof(ScrollViewer.Create)];
        yield return [typeof(VStack), nameof(VStack.Create)];
        yield return [typeof(HStack), nameof(HStack.Create)];
        yield return [typeof(Line), nameof(Line.Create)];
        yield return [typeof(Rectagle), nameof(Rectagle.Create)];
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factories_DeclareConcreteReturnTypes(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.NotNull(method);
        Assert.Equal(type, method.ReturnType);
        Assert.Equal(type, method.DeclaringType);
        Assert.All(method.GetParameters(), parameter => Assert.True(parameter.IsOptional));
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factories_MatchConstructorsAndKeepIndependentLifetime(Type type, string name)
    {
        using var expected = (Element)Invoke(type.GetConstructors().Single());
        using var first = (Element)Invoke(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!);
        using var second = (Element)Invoke(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!);
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
        using var third = (Element)Invoke(type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!);
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
        using Text text = Text.Plain(value).Width(200);
        using Button button = Button.Simple(value).OnClick(() => { });
        using CheckBox check = CheckBox.Create(value).Configure(control => control.IsChecked = true);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => Text.Plain(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Button.Simple(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => CheckBox.Create(null!));
    }

    [Fact]
    public void FactoriesAndConstructors_MixedTreeRoutesInputAndOwnsChildren()
    {
        var activations = 0;
        TextBox editor = TextBox.Create().Width(200);
        CheckBox toggle = new CheckBox("Notifications");
        Slider slider = Slider.Create().Width(200);
        Button button = Button.Simple("Apply").OnClick(() => activations++);
        Border border = Border.Create().Content(Text.Plain("Settings"));
        VStack root = VStack.Create().Width(240).Configure(stack => stack.Spacing = 8)
            .WithChildren(border, editor, toggle, slider, button, new Text("Constructor label"));
        var children = root.Children.ToArray();
        using (var ui = new UiSession(root, new HeadlessSurface(), new UiViewport(300, 400)))
        {
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
        Assert.True(border.Child!.IsDisposed);
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

    private static object Invoke(MethodBase method) =>
        method switch
        {
            ConstructorInfo constructor => constructor.Invoke(method.GetParameters().Select(parameter => parameter.DefaultValue).ToArray()),
            MethodInfo factory => factory.Invoke(null, method.GetParameters().Select(parameter => parameter.DefaultValue).ToArray())!,
            _ => throw new InvalidOperationException()
        };

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
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
}
