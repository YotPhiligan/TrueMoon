using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class BindableTests
{
    [Fact]
    public void ViewModel_InheritsBindableAndNotifiesWithCommittedValue()
    {
        var model = new NameViewModel();
        var observations = new List<(object? Sender, string? Property, string Value)>();
        model.PropertyChanged += (sender, args) => observations.Add((sender, args.PropertyName, model.Name));

        model.Name = "changed";

        Assert.Equal(typeof(Bindable), typeof(ViewModel).BaseType);
        Assert.Equal(typeof(ViewModel), typeof(NameViewModel).BaseType);
        var observation = Assert.Single(observations);
        Assert.Same(model, observation.Sender);
        Assert.Equal(nameof(NameViewModel.Name), observation.Property);
        Assert.Equal("changed", observation.Value);
    }

    [Fact]
    public void SetProperty_EqualValuesUseDefaultEqualityAndKeepStoredInstance()
    {
        var model = new DataObject();
        var first = new Value("same");
        var events = new List<string?>();
        model.PropertyChanged += (_, args) => events.Add(args.PropertyName);

        Assert.False(model.SetCount(0));
        Assert.True(model.SetCount(1));
        Assert.False(model.SetCount(1));
        Assert.True(model.SetValue(first));
        Assert.False(model.SetValue(new Value("same")));

        Assert.Equal(1, model.Count);
        Assert.Same(first, model.Value);
        Assert.Equal(new[] { nameof(DataObject.Count), nameof(DataObject.Value) }, events);
    }

    [Fact]
    public void SetProperty_NullTransitionsNotifyOncePerChange()
    {
        var model = new DataObject();
        var values = new List<Value?>();
        model.PropertyChanged += (_, _) => values.Add(model.Value);
        var value = new Value("present");

        Assert.False(model.SetValue(null));
        Assert.True(model.SetValue(value));
        Assert.True(model.SetValue(null));
        Assert.False(model.SetValue(null));

        Assert.Equal(new Value?[] { value, null }, values);
        Assert.Null(model.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DependentProperty")]
    public void OnPropertyChanged_PreservesExplicitPropertyAndAllPropertyNotifications(string? name)
    {
        var model = new DataObject();
        var events = new List<PropertyChangedEventArgs>();
        model.PropertyChanged += (_, args) => events.Add(args);

        model.Notify(name);

        Assert.Equal(name, Assert.Single(events).PropertyName);
        Assert.Equal(0, model.Count);
        Assert.Null(model.Value);
    }

    [Fact]
    public void SetProperty_UsesVirtualNotificationHook()
    {
        var model = new HookModel();
        var events = new List<string?>();
        model.PropertyChanged += (_, args) => events.Add(args.PropertyName);

        model.Name = "updated";
        model.Name = "updated";

        Assert.Equal(new[] { nameof(HookModel.Name) }, model.Hooks);
        Assert.Equal(model.Hooks, events);
    }

    [Fact]
    public void SetProperty_ObserverFailureKeepsCommittedValueAndAllowsUnsubscribe()
    {
        var model = new NameViewModel();
        var failure = new InvalidOperationException("observer failed");
        var calls = 0;
        PropertyChangedEventHandler observer = (_, _) => { calls++; throw failure; };
        model.PropertyChanged += observer;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => model.Name = "committed"));
        Assert.Equal("committed", model.Name);
        model.Name = "committed";
        model.PropertyChanged -= observer;
        model.Name = "next";

        Assert.Equal(1, calls);
        Assert.Equal("next", model.Name);
    }

    [Fact]
    public void ClearPropertyChangedSubscribers_ReleasesCurrentObserversAndAllowsNewSubscription()
    {
        var model = new DataObject();
        var oldCalls = 0;
        var newCalls = 0;
        model.PropertyChanged += (_, _) => oldCalls++;
        model.ReleaseObservers();
        model.ReleaseObservers();
        model.PropertyChanged += (_, _) => newCalls++;

        Assert.True(model.SetCount(3));

        Assert.Equal(0, oldCalls);
        Assert.Equal(1, newCalls);
        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void ViewModel_UpdatesRetainedTwoWayBindingThroughInheritedNotifications()
    {
        var model = new NameViewModel();
        var text = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, state => state.Name);
        using var ui = new UiSession(text, new HeadlessSurface(), new UiViewport(240, 140));
        ui.Update();
        Assert.Equal("initial", text.Value);

        model.Name = "from model";
        Assert.Equal("initial", text.Value);
        ui.Update();
        Assert.Equal("from model", text.Value);
        text.Value = "from control";
        Assert.Equal("from control", model.Name);

        ui.Dispose();
        model.Name = "after detach";
        Assert.True(text.IsDisposed);
        Assert.Equal("after detach", model.Name);
    }

    private sealed record Value(string Text);

    private sealed class DataObject : Bindable
    {
        private int _count;
        private Value? _value;
        public int Count => _count;
        public Value? Value => _value;
        public bool SetCount(int value) => SetProperty(ref _count, value, nameof(Count));
        public bool SetValue(Value? value) => SetProperty(ref _value, value, nameof(Value));
        public void Notify(string? name) => OnPropertyChanged(name);
        public void ReleaseObservers() => ClearPropertyChangedSubscribers();
    }

    private sealed class NameViewModel : ViewModel
    {
        private string _name = "initial";
        public string Name { get => _name; set => SetProperty(ref _name, value); }
    }

    private sealed class HookModel : ViewModel
    {
        private string _name = "";
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public List<string?> Hooks { get; } = [];
        protected override void OnPropertyChanged(string? propertyName = null)
        {
            Hooks.Add(propertyName);
            base.OnPropertyChanged(propertyName);
        }
    }

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
}
