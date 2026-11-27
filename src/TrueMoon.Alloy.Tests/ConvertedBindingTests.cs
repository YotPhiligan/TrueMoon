using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis.Painting;
using System.ComponentModel;
using System.Globalization;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ConvertedBindingTests
{
    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) => UiTreeRenderer.Draw(root, new RecordingDrawingContext(), presenters);
        public void Dispose() { }
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    [Fact]
    public void ConvertedOneWay_ShowsProjectedSourceAndFollowsNotifications()
    {
        var model = new Model { Number = 42d };
        var text = new Text().Bind(Text.ValueProperty, model, m => m.Number, Format);
        using var ui = Create(text);
        ui.Update();
        Assert.Equal("42", text.Value);
        model.Publish(43d);
        ui.Update();
        Assert.Equal("43", text.Value);
    }

    [Fact]
    public void ConvertedOneWay_ContextReplacement_ReprojectsNewSource()
    {
        var context = new BindingContext { DataContext = new Model() };
        var text = new Text().Bind(Text.ValueProperty, context, m => m.Number, Format);
        using var ui = Create(text);
        ui.Update();
        Assert.Equal("0", text.Value);

        context.DataContext = new Model { Number = 7d };
        ui.Update();
        Assert.Equal("7", text.Value);
    }

    [Fact]
    public void ConvertedTwoWay_RoutedInput_WritesTypedValue()
    {
        var model = new Model();
        var box = new TextBox().BindTwoWay(TextBox.ValueProperty, model, m => m.Number, new NumberText());
        using var ui = Create(box);
        ui.Update();
        Assert.Equal("0", box.Value);
        ui.Focus(box);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "12.5")).Handled);
        Assert.Equal(12.5d, model.Number);
        Assert.Equal(1, model.Writes);
        Assert.Equal("12.5", box.Value);
        Assert.False(box.HasError);
    }

    [Fact]
    public void ConvertedTwoWay_InvalidInput_KeepsDraftAndReportsError()
    {
        var model = new Model();
        var box = new TextBox().BindTwoWay(TextBox.ValueProperty, model, m => m.Number, new NumberText());
        using var ui = Create(box);
        ui.Update();
        ui.Focus(box);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "abc")).Handled);
        Assert.Equal("abc", box.Value);
        Assert.Equal(0d, model.Number);
        Assert.Equal(0, model.Writes);
        Assert.True(box.HasError);
        Assert.Equal("Введите число", box.ErrorMessage);
    }

    [Fact]
    public void ConvertedTwoWay_ValidInputAfterFailure_ClearsOwnError()
    {
        var model = new Model();
        var box = new TextBox().BindTwoWay(TextBox.ValueProperty, model, m => m.Number, new NumberText());
        using var ui = Create(box);
        ui.Update();
        ui.Focus(box);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new UiInput(InputKind.Text, Text: "abc"));
        Assert.True(box.HasError);

        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new UiInput(InputKind.Text, Text: "7"));
        Assert.Equal(7d, model.Number);
        Assert.Equal("7", box.Value);
        Assert.False(box.HasError);
        Assert.Equal("", box.ErrorMessage);
    }

    [Fact]
    public void ConvertedTwoWay_SourceChangeDuringInvalidDraft_ReprojectsAndClearsError()
    {
        var model = new Model();
        var box = new TextBox().BindTwoWay(TextBox.ValueProperty, model, m => m.Number, new NumberText());
        using var ui = Create(box);
        ui.Update();
        ui.Focus(box);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new UiInput(InputKind.Text, Text: "abc"));
        Assert.True(box.HasError);

        model.Publish(9d);
        ui.Update();
        Assert.Equal("9", box.Value);
        Assert.False(box.HasError);
        Assert.Equal("", box.ErrorMessage);
    }

    [Fact]
    public void ConvertedTwoWay_ConsumerErrorState_IsNeverClearedByBinding()
    {
        var model = new Model();
        var box = new TextBox().BindTwoWay(TextBox.ValueProperty, model, m => m.Number, new NumberText());
        box.Set(UiProperties.HasError, true);
        box.Set(UiProperties.ErrorMessage, "чужая ошибка");
        using var ui = Create(box);
        ui.Update();
        ui.Focus(box);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new UiInput(InputKind.Text, Text: "3"));
        Assert.Equal(3d, model.Number);
        Assert.True(box.HasError);
        Assert.Equal("чужая ошибка", box.ErrorMessage);
    }

    [Fact]
    public void ConvertedTwoWay_SourcePropertyWithoutPublicSetter_ThrowsArgumentException()
    {
        var model = new Model();
        var box = new TextBox();
        var error = Assert.Throws<ArgumentException>(
            () => box.BindTwoWay(TextBox.ValueProperty, model, m => m.Half, new NumberText()));
        Assert.Contains("exactly the converter's source type", error.Message);
    }

    [Fact]
    public void ConvertedOneWay_ConverterThrows_LeavesUiValueAndRecovers()
    {
        var model = new Model();
        var text = new Text().Bind(Text.ValueProperty, model, m => m.Number,
            n => n < 0 ? throw new FormatException("negative") : Format(n));
        using var ui = Create(text);
        ui.Update();
        Assert.Equal("0", text.Value);

        model.Publish(-1d);
        Assert.Throws<FormatException>(() => ui.Update());
        Assert.Equal("0", text.Value);

        model.Publish(2d);
        ui.Update();
        Assert.Equal("2", text.Value);
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private double _number;
        private PropertyChangedEventHandler? _changed;
        public int Writes { get; private set; }
        public double Number
        {
            get => _number;
            set
            {
                _number = value;
                Writes++;
                _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
            }
        }

        public double Half { get; private set; } = 1.5d;
        public string WrongType { get; set; } = "";
        public void Publish(double value)
        {
            _number = value;
            _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
    }

    private sealed class NumberText : ITwoWayValueConverter<double, string>
    {
        public string Convert(double value) => Format(value);

        public bool TryConvertBack(string value, out double number, out string? error)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                error = null;
                return true;
            }

            error = "Введите число";
            return false;
        }
    }

    private sealed class BindingContext : IDataContext<Model>
    {
        private Model? _data;
        public Model? DataContext
        {
            get => _data;
            set { _data = value; DataContextChanged?.Invoke(); }
        }

        public event Action? DataContextChanged;
    }
}
