using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis.Windows;
using System.ComponentModel;
using System.Collections.ObjectModel;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyPackageConsumer;

/// <summary>State owned by the application, independently of the UI session.</summary>
public sealed class ConsumerModel : INotifyPropertyChanged, IDisposable
{
    private string _name = "Package consumer";
    private PropertyChangedEventHandler? _changed;

    internal ObservableCollection<ConsumerItem> Items { get; } = [new("First"), new("Second")];

    /// <summary>The text edited by the view.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Number of live UI binding subscriptions.</summary>
    public int SubscriptionCount => _changed?.GetInvocationList().Length ?? 0;

    /// <summary>Number of disposal calls made by the selected provider.</summary>
    public int DisposeCalls { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCalls++;
        if (SubscriptionCount != 0) throw new InvalidOperationException("Model was disposed before UI bindings were released.");
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}

internal sealed record ConsumerItem(string Caption);

internal sealed class ConsumerChild : View<ConsumerModel>
{
    public ConsumerChild(ConsumerModel model) => Content(model, ui => ui.Text().BindText(state => state.Name));
}

internal sealed class ConsumerChildStyle : IViewStyle<ConsumerChild>
{
    public void Configure(IViewStyleBuilder<ConsumerChild> styles) => styles.Root().Padding(3);
}

internal sealed class ConsumerRow : View<ConsumerItem>
{
    public ConsumerRow(ConsumerModel model, ConsumerItem item) =>
        Content(item, ui => ui.HStack(ui.Text(item.Caption), new Text().BindText(model, state => state.Name)));
}

internal sealed class ConsumerRowStyle : IViewStyle<ConsumerRow>
{
    public void Configure(IViewStyleBuilder<ConsumerRow> styles) => styles.Root().Padding(2);
}

/// <summary>A view authored entirely in the consuming application.</summary>
internal sealed class ConsumerView : View<ConsumerModel>
{
    private readonly ConsumerModel _model;
    private readonly ConsumerChild _child;
    private VStack _rows = null!;
    private ConsumerRow _first = null!;
    private ConsumerRow _second = null!;
    private TextBox _editor = null!;
    private RichTextEditor _richEditor = null!;
    private int _step;

    public ConsumerView(ConsumerModel model, ConsumerChild child, TrueMoon.IFactory<ConsumerRow, ConsumerItem> rows)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _child = child;
        Content(model, ui => ui.VStack(
            ui.Text("TrueMoon from NuGet").Named("Heading"),
            _editor = ui.TextBox().BindValueTwoWay(state => state.Name),
            _richEditor = ui.RichTextEditor().Height(80).Configure(editor => editor.Text = "Rich text from NuGet"),
            child,
            (_rows = ui.VStack().BindItems(model.Items, rows.Create).Element),
            ui.Button("Rename").WithRole(ConsumerViewRoles.PrimaryAction).Named("RenameButton")
                .OnClick(() => model.Name = "Button update"))
            .Padding(16));
    }

    /// <summary>Exercises binding, layout, theme and queued changes on the UI owner thread.</summary>
    /// <param name="session">The active UI session.</param>
    public void VerifyFrame(UiSession session)
    {
        switch (++_step)
        {
            case 1:
                Require(_editor.Value == "Package consumer", "Initial binding failed.");
                Require(_editor.Bounds.Width > 0 && _editor.Bounds.Height > 0, "Editor was not laid out.");
                Require(_model.SubscriptionCount > 0, "Binding did not subscribe to the model.");
                Require(this.Heading.FontSize == 18 && this.RenameButton.Tone == ControlTone.Accent, "Generated parts or role styles were not applied.");
                Require(_editor.CornerRadius == 5 && _richEditor.CornerRadius == 5 && _editor.BorderWidth == 1 &&
                    _richEditor.Background == Theme.Dark.Input, "Packaged default appearance missing.");
                Require(_child.IsAttached && _child.Padding == new Thickness(3) && _child.Child is Text { Value: "Package consumer" },
                    "Direct child creation, style or binding failed.");
                Require(_rows.Items.Count == 2, "The collection did not create its initial rows.");
                _first = (ConsumerRow)_rows.Items[0];
                _second = (ConsumerRow)_rows.Items[1];
                Require(ReferenceEquals(_first.DataContext, _model.Items[0]) && ReferenceEquals(_second.DataContext, _model.Items[1]) &&
                    _first.Padding == new Thickness(2) && _first.Child is HStack { Items: [Text { Value: "First" }, Text { Value: "Package consumer" }] },
                    "Typed row arguments, DI dependency or optional style were not applied.");
                using (var frame = WindowFrame.CreateDefault("Package frame", new Panel()))
                {
                    frame.ApplyWindowState(new(true, false, true, true, true));
                    Require(frame.DefaultTitleBar!.CloseButton.Symbol == UiSymbol.Close && frame.ResizeGrip!.IsVisible, "Packaged default frame API missing.");
                }
                break;
            case 2:
                _model.Name = "Model update";
                _model.Items.Move(0, 1);
                _richEditor.Select(0, 4);
                _richEditor.ApplyStyle(new() { Underline = true, Foreground = new(230, 80, 40) });
                Require(_richEditor.Document.Runs[0].Style.Underline == true, "Packaged RichTextKit formatting failed.");
                break;
            case 3:
                Require(_editor.Value == "Model update", "Model-to-view binding failed.");
                Require(ReferenceEquals(_rows.Items[0], _second) && ReferenceEquals(_rows.Items[1], _first) &&
                    _first.Child is HStack { Items: [_, Text { Value: "Model update" }] }, "Moving a typed row recreated it or severed its binding.");
                _model.Items.RemoveAt(0);
                _editor.Value = "Editor update";
                break;
            case 4:
                Require(_model.Name == "Editor update", "View-to-model binding failed.");
                Require(_second.IsDisposed && ReferenceEquals(_rows.Items.Single(), _first), "Removing a typed row retained its tree or disturbed its neighbor.");
                Require(_richEditor.Undo() && _richEditor.Text == "Rich text from NuGet", "Rich document undo failed.");
                session.SetTheme(Theme.Light);
                break;
            case 5:
                session.Post(() => _model.Name = "Queued update");
                break;
            case 6:
                Require(_editor.Value == "Queued update", "Queued change did not reach the view.");
                Require(_editor.Background == Theme.Light.Input && _editor.Value == "Queued update", "Packaged theme switch lost editor state.");
                if (session.Root is WindowFrame windowFrame) Require(windowFrame.DefaultTitleBar != null, "Native package consumer did not use the default frame.");
                break;
        }
    }

    /// <summary>Checks that this consumer completed its interaction sequence and released the tree.</summary>
    public void VerifyClosed()
    {
        Require(_step >= 6, "Interaction sequence did not complete.");
        Require(IsDisposed && !IsAttached && _editor.IsDisposed && _richEditor.IsDisposed, "UI tree was not disposed.");
        Require(_child.IsDisposed && _first.IsDisposed && _second.IsDisposed, "Generated factory products survived window disposal.");
        Require(_model.SubscriptionCount == 0, "Model subscriptions survived UI disposal.");
        Require(_model.Name == "Queued update", "Application model was not preserved.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class ConsumerStyle : IViewStyle<ConsumerView>
{
    public void Configure(IViewStyleBuilder<ConsumerView> styles)
    {
        styles.Part(view => view.Heading).FontSize(18);
        styles.Role(ConsumerViewRoles.PrimaryAction).Tone(ControlTone.Accent);
    }
}
