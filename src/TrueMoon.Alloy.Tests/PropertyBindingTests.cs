using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class PropertyBindingTests
{
    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));

    [Fact]
    public void Bind_BeforeAttach_SynchronizesOnAttachAndInvalidatesLayout()
    {
        var source = new Model { Value = "one" };
        var text = new Text("placeholder");
        Assert.Same(text, text.BindText(source, x => x.Value).Width(100));
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(0, source.Reads);
        Assert.Equal("placeholder", text.Value);
        using var ui = Create(text);
        Assert.Equal("one", text.Value);
        Assert.Equal(1, source.Subscribers);
        Assert.True(ui.Update());
        text.Width = float.NaN;
        ui.Update();
        source.Value = "новое";
        Assert.Equal("one", text.Value);
        Assert.True(ui.Update());
        Assert.Equal("новое", text.Value);
        Assert.Equal(40, text.DesiredSize.Width);
        ui.Dispose();
        Assert.Equal(0, source.Subscribers);
    }

    [Theory]
    [InlineData("Value", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Other", false)]
    public void Notification_MatchingOrAllProperties_OnlyReadsExpectedSource(string? propertyName, bool refresh)
    {
        var source = new Model { Value = "initial" };
        using var ui = Create(new Text().BindText(source, x => x.Value));
        ui.Update();
        var reads = source.Reads;
        source.SetSilently("updated");
        source.Notify(propertyName);
        Assert.Equal(refresh, ui.NeedsUpdate);
        Assert.Equal(refresh, ui.Update());
        Assert.Equal(refresh ? "updated" : "initial", ((Text)ui.Root).Value);
        Assert.Equal(reads + (refresh ? 1 : 0), source.Reads);
    }

    [Fact]
    public void Bind_AttachedTextBox_SynchronizesImmediatelyWithoutWritingBack()
    {
        var source = new Model { Value = "model" };
        var text = new TextBox();
        using var ui = Create(text);
        Assert.Same(text, text.Bind(TextBox.ValueProperty, source, x => x.Value));
        Assert.Equal("model", text.Value);
        var writes = source.Writes;
        text.Value = "local edit";
        Assert.Equal(writes, source.Writes);
        Assert.Equal("model", source.Value);
        source.Value = "next";
        ui.Update();
        Assert.Equal("next", text.Value);
        Assert.Equal(writes + 1, source.Writes);
    }

    [Fact]
    public void Detach_ReattachAndMove_RestoresExactlyOneSubscription()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        var root = new Panel().WithChildren(text, new Text("neighbor"));
        using var ui = Create(root);
        root.Items.Move(0, 1);
        Assert.Equal(1, source.Adds);
        Assert.Equal(0, source.Removes);
        for (var i = 0; i < 3; i++)
        {
            root.Items.Remove(text);
            Assert.False(text.IsDisposed);
            Assert.Equal(0, source.Subscribers);
            var previous = text.Value;
            var reads = source.Reads;
            source.Value = $"reattach {i}";
            ui.Update();
            Assert.Equal(previous, text.Value);
            Assert.Equal(reads, source.Reads);
            root.Items.Add(text);
            Assert.Equal($"reattach {i}", text.Value);
            Assert.Equal(1, source.Subscribers);
            Assert.Equal(i + 2, source.Adds);
        }
        ui.Dispose();
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(source.Adds, source.Removes);
        source.Value = "after dispose";
        Assert.Equal("reattach 2", text.Value);
    }

    [Fact]
    public void QueuedUpdate_ReattachSameSession_SkipsOldAttachment()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        var root = new Panel().WithChildren(text);
        using var ui = Create(root);
        source.Value = "queued";
        root.Items.Remove(text);
        source.SetSilently("reattached");
        root.Items.Add(text);
        var reads = source.Reads;
        source.SetSilently("must not be read by old work");
        ui.Update();
        Assert.Equal("reattached", text.Value);
        Assert.Equal(reads, source.Reads);
        Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void BackgroundNotification_ReadsAndMutatesOnlyDuringOwnerUpdate()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        using var ui = Create(text);
        ui.Update();
        var reads = source.Reads;
        var propertyThread = 0;
        text.PropertyChanged += (_, _) => propertyThread = Environment.CurrentManagedThreadId;
        RunOnWorker(() => source.Value = "background");
        Assert.Equal("initial", text.Value);
        Assert.Equal(reads, source.Reads);
        Assert.True(ui.NeedsUpdate);
        ui.Update();
        Assert.Equal("background", text.Value);
        Assert.Equal(Environment.CurrentManagedThreadId, source.LastReadThread);
        Assert.Equal(Environment.CurrentManagedThreadId, propertyThread);
    }

    [Fact]
    public void Transfer_ToDifferentOwnerThread_DropsOldWorkAndInFlightHandlers()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        var root = new Panel().WithChildren(text);
        using var oldUi = Create(root);
        source.Value = "old queue";
        var inFlight = source.CaptureNotification();
        root.Items.Remove(text);
        RunOnWorker(() =>
        {
            source.SetSilently("new attachment");
            using var newUi = Create(text);
            Assert.Equal("new attachment", text.Value);
            Assert.Equal(Environment.CurrentManagedThreadId, source.LastReadThread);
            var reads = source.Reads;
            source.SetSilently("silent");
            inFlight();
            newUi.Update();
            Assert.Equal(reads, source.Reads);
            Assert.Equal("new attachment", text.Value);
            source.Value = "current notification";
            newUi.Update();
            Assert.Equal("current notification", text.Value);
            Assert.Equal(1, source.Subscribers);
        });
        var finalReads = source.Reads;
        oldUi.Update();
        Assert.Equal(finalReads, source.Reads);
        Assert.Equal("current notification", text.Value);
        Assert.True(text.IsDisposed);
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(2, source.Adds);
        Assert.Equal(2, source.Removes);
    }

    [Fact]
    public void InFlightNotification_AfterDetach_DoesNotReadOrMutateUnattachedElement()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        var root = new Panel().WithChildren(text);
        using var ui = Create(root);
        var inFlight = source.CaptureNotification();
        root.Items.Remove(text);
        var reads = source.Reads;
        RunOnWorker(() => { source.SetSilently("late"); inFlight(); });
        ui.Update();
        Assert.Equal("initial", text.Value);
        Assert.Equal(reads, source.Reads);
        Assert.Equal(0, source.Subscribers);
        text.Dispose();
    }

    [Fact]
    public void Bind_DuplicateProperty_IsRejectedWithoutReplacingExistingBinding()
    {
        var first = new Model { Value = "first" };
        var second = new Model { Value = "second" };
        var text = new Text().BindText(first, x => x.Value);
        using var ui = Create(text);
        Assert.Throws<InvalidOperationException>(() => text.BindText(second, x => x.Value));
        Assert.Equal(1, first.Subscribers);
        Assert.Equal(0, second.Subscribers);
        first.Value = "kept";
        ui.Update();
        Assert.Equal("kept", text.Value);
    }

    [Theory]
    [InlineData("nested")]
    [InlineData("computed")]
    [InlineData("field")]
    public void Bind_UnsupportedSelector_IsRejectedBeforeSubscription(string kind)
    {
        var source = new Model();
        Expression<Func<Model, string>> selector = kind switch
        {
            "nested" => x => x.Child.Value,
            "computed" => x => x.Value.ToUpperInvariant(),
            _ => x => x.Field
        };
        using var text = new Text();
        using var ui = Create(text);
        Assert.Throws<ArgumentException>(() => text.Bind(Text.ValueProperty, source, selector));
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(0, source.Reads);
        text.BindText(source, x => x.Value);
        Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void Bind_NullArguments_AreRejectedBeforeRegistration()
    {
        var source = new Model();
        using var text = new Text();
        Assert.Throws<ArgumentNullException>(() => ((Text)null!).BindText(source, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => text.Bind(null!, source, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => text.BindText((Model)null!, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => text.BindText(source, null!));
        Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public void InitialGetterFailure_AttachedRegistration_CleansUpAndAllowsRetry()
    {
        var source = new Model { ThrowOnRead = true };
        var text = new Text("unchanged");
        using var ui = Create(text);
        Assert.Throws<TestException>(() => text.BindText(source, x => x.Value));
        Assert.Equal(0, source.Subscribers);
        Assert.Equal("unchanged", text.Value);
        source.ThrowOnRead = false;
        source.Value = "retry";
        text.BindText(source, x => x.Value);
        Assert.Equal("retry", text.Value);
        Assert.Equal(1, source.Subscribers);
        ui.Dispose();
        Assert.Equal(source.Adds, source.Removes);
    }

    [Fact]
    public void InitialGetterFailure_SessionAttach_RollsBackAndReconnectsOnRetry()
    {
        var source = new Model { ThrowOnRead = true };
        var text = new Text("unchanged").BindText(source, x => x.Value);
        using var root = new Panel().WithChildren(text);
        Assert.Throws<TestException>(() => Create(root));
        Assert.False(root.IsAttached);
        Assert.False(text.IsAttached);
        Assert.False(text.IsDisposed);
        Assert.Equal(0, source.Subscribers);
        source.ThrowOnRead = false;
        source.Value = "retry";
        using var ui = Create(root);
        Assert.Equal("retry", text.Value);
        Assert.Equal(1, source.Subscribers);
        Assert.Equal(2, source.Adds);
    }

    [Fact]
    public void InvalidDestinationValue_UpdateReportsFailureAndLaterEventRecovers()
    {
        var source = new Model { Number = 50 };
        var text = new Text().Bind(UiProperties.Width, source, x => x.Number);
        using var ui = Create(text);
        Assert.Equal(50, text.Width);
        source.Number = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => ui.Update());
        Assert.Equal(50, text.Width);
        Assert.Equal(1, source.Subscribers);
        source.Number = 75;
        Assert.True(ui.Update());
        Assert.Equal(75, text.Bounds.Width);
    }

    [Fact]
    public void Bind_StandardNumericConversion_PreservesSourcePropertyNotification()
    {
        var property = new UiProperty<double>("Converted", 0);
        var source = new Model { Number = 12.5f };
        using var ui = Create(new Text().Bind(property, source, x => (double)x.Number));
        Assert.Equal(12.5, ui.Root.Get(property));
        source.Number = 25.5f;
        ui.Update();
        Assert.Equal(25.5, ui.Root.Get(property));
    }

    [Fact]
    public void Notification_UnchangedValue_DoesNotRedrawOrRaiseUiPropertyEvent()
    {
        var source = new Model { Value = "same" };
        var text = new Text().BindText(source, x => x.Value);
        using var ui = Create(text);
        ui.Update();
        var changes = 0;
        text.PropertyChanged += (_, _) => changes++;
        source.Value = "same";
        Assert.False(ui.Update());
        Assert.Equal(0, changes);
        Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void GetterFailure_QueuedUpdate_ReportsErrorAndNextNotificationRecovers()
    {
        var source = new Model { Value = "initial" };
        var text = new Text().BindText(source, x => x.Value);
        using var ui = Create(text);
        ui.Update();
        source.ThrowOnRead = true;
        source.Value = "failed read";
        Assert.Throws<TestException>(() => ui.Update());
        Assert.Equal("initial", text.Value);
        Assert.Equal(1, source.Subscribers);
        source.ThrowOnRead = false;
        source.Value = "recovered";
        Assert.True(ui.Update());
        Assert.Equal("recovered", text.Value);
    }

    [Fact]
    public void Bind_FromForeignThread_IsRejectedBeforeSubscribing()
    {
        var source = new Model();
        var text = new Text();
        using var ui = Create(text);
        RunOnWorker(() => Assert.Throws<InvalidOperationException>(() => text.BindText(source, x => x.Value)));
        Assert.Equal(0, source.Subscribers);
        text.BindText(source, x => x.Value);
        Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void Notification_DuringDetachBeforeUnsubscribe_DoesNotReadOnWorker()
    {
        var source = new Model { Value = "initial" };
        using var text = new Text().BindText(source, x => x.Value);
        var ownerThread = Environment.CurrentManagedThreadId;
        var reads = 0;
        var queued = new Queue<Action>();
        text.Attach(() => Assert.Equal(ownerThread, Environment.CurrentManagedThreadId), queued.Enqueue, _ =>
        {
            Assert.Equal(1, source.Subscribers);
            RunOnWorker(() => source.Value = "during detach");
            Assert.Equal(reads, source.Reads);
            Assert.Equal("initial", text.Value);
        });
        reads = source.Reads;
        text.Detach();
        Assert.Equal(0, source.Subscribers);
        Assert.Empty(queued);
        Assert.Equal(reads, source.Reads);
        Assert.Equal("initial", text.Value);
    }

    private static void RunOnWorker(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Worker did not finish.");
        Assert.Null(error);
    }

    private sealed class TestException : Exception;
    private sealed class Model : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private string _value = "";
        private float _number;
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int LastReadThread { get; private set; }
        public bool ThrowOnRead { get; set; }
        public Model Child => this;
        public string Field = "field";
        public string Value
        {
            get
            {
                Reads++;
                LastReadThread = Environment.CurrentManagedThreadId;
                if (ThrowOnRead) throw new TestException();
                return _value;
            }
            set { _value = value; Writes++; Notify(nameof(Value)); }
        }
        public float Number { get => _number; set { _number = value; Notify(nameof(Number)); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; Adds++; }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
        public void SetSilently(string value) => _value = value;
        public void Notify(string? property) => _changed?.Invoke(this, new PropertyChangedEventArgs(property));
        public Action CaptureNotification()
        {
            var captured = _changed;
            return () => captured?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
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
