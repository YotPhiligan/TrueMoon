using System.ComponentModel;
using TrueMoon.Alloy;
using TrueMoon.Argentis;

namespace AlloyPackageConsumer;

/// <summary>State owned by the application, independently of the UI session.</summary>
public sealed class ConsumerModel : INotifyPropertyChanged
{
    private string _name = "Package consumer";
    private PropertyChangedEventHandler? _changed;

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

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _changed += value;
        remove => _changed -= value;
    }
}

/// <summary>A view authored entirely in the consuming application.</summary>
public sealed class ConsumerView : View<ConsumerModel>
{
    private readonly ConsumerModel _model;
    private readonly TextBox _editor;
    private int _step;

    /// <summary>Creates the view using an application-owned model supplied by DI.</summary>
    /// <param name="model">State that survives UI disposal.</param>
    public ConsumerView(ConsumerModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _editor = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, state => state.Name);
        Content(model, _ => VStack.Create().Padding(16).WithChildren(
            Text.Plain("TrueMoon from NuGet"), _editor,
            Button.Simple("Rename").OnClick(() => model.Name = "Button update")));
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
                break;
            case 2:
                _model.Name = "Model update";
                break;
            case 3:
                Require(_editor.Value == "Model update", "Model-to-view binding failed.");
                _editor.Value = "Editor update";
                break;
            case 4:
                Require(_model.Name == "Editor update", "View-to-model binding failed.");
                session.SetTheme(Theme.Light);
                break;
            case 5:
                session.Post(() => _model.Name = "Queued update");
                break;
            case 6:
                Require(_editor.Value == "Queued update", "Queued change did not reach the view.");
                break;
        }
    }

    /// <summary>Checks that this consumer completed its interaction sequence and released the tree.</summary>
    public void VerifyClosed()
    {
        Require(_step >= 6, "Interaction sequence did not complete.");
        Require(IsDisposed && !IsAttached && _editor.IsDisposed, "UI tree was not disposed.");
        Require(_model.SubscriptionCount == 0, "Model subscriptions survived UI disposal.");
        Require(_model.Name == "Queued update", "Application model was not preserved.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
