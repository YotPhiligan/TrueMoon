using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class TwoWayBindingTests
{
    private static UiSession Create(Element root, IUiClipboard? clipboard = null) =>
        new(root, new HeadlessSurface(), new UiViewport(240, 140), clipboard);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindTwoWay_InitialAttach_SourceWinsWithoutWriteBack(bool alreadyAttached)
    {
        var model = new Model<string>("source");
        var editor = new TextBox { Value = "local" };
        using var ui = alreadyAttached ? Create(editor) : null;
        Assert.Same(editor, editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value).Width(200));
        if (!alreadyAttached)
        {
            Assert.Equal("local", editor.Value);
            Assert.Equal(0, model.Reads);
            Assert.Equal(0, model.Subscribers);
        }
        using var laterUi = alreadyAttached ? null : Create(editor);
        Assert.Equal("source", editor.Value);
        Assert.Equal(0, model.Writes);
        Assert.Equal(1, model.Subscribers);
    }

    [Fact]
    public void TextInput_Normalization_ImmediatelyWritesCanonicalTextAndClampsCaret()
    {
        var model = new Model<string>("") { Normalize = value => value.Trim().ToUpperInvariant() };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        ui.Update();
        ui.Focus(editor);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "Привет 🧑‍💻  ")).Handled);
        Assert.Equal("ПРИВЕТ 🧑‍💻", model.Value);
        Assert.Equal(model.Value, editor.Value);
        Assert.Equal(editor.Value.Length, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(1, model.Writes);
        Assert.Equal(Environment.CurrentManagedThreadId, model.LastWriteThread);
        ui.Update();
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void ClipboardAndGraphemeEditing_WritesModelAndKeepsValidSelection()
    {
        var model = new Model<string>("A👩‍💻Б");
        var clipboard = new Clipboard { Text = "latin\r\nкириллица" };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor, clipboard);
        ui.Update(); ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal("👩‍💻Б".Length, editor.SelectionLength);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.C, Control: true));
        Assert.Equal("👩‍💻Б", clipboard.Text);
        Assert.Equal(0, model.Writes);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.X, Control: true));
        Assert.Equal("A", model.Value);
        Assert.Equal(1, editor.CaretIndex);
        clipboard.Text = "latin\r\nкириллица";
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.V, Control: true));
        Assert.Equal("Alatinкириллица", model.Value);
        Assert.Equal(model.Value, editor.Value);
        Assert.Equal(editor.Value.Length, editor.CaretIndex);
        Assert.Equal(2, model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckBox_PointerOrKeyboardActivation_WritesOnceWithoutSourceFeedback(bool keyboard)
    {
        var model = new Model<bool>(false);
        var control = new CheckBox("Toggle").BindTwoWay(CheckBox.IsCheckedProperty, model, x => x.Value);
        using var ui = Create(control);
        ui.Update();
        if (keyboard)
        {
            ui.Focus(control);
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Space));
            ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space));
        }
        else
        {
            ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
            var released = ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20));
            Assert.False(released.PointerCaptured);
        }
        Assert.True(control.IsChecked);
        Assert.True(model.Value);
        Assert.Equal(1, model.Writes);
        model.Publish(false);
        ui.Update();
        Assert.False(control.IsChecked);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void Slider_DragAndArrowKey_WriteModelAndReleaseCapture()
    {
        var model = new Model<float>(0);
        var slider = new Slider().Width(200).BindTwoWay(ProgressBar.ValueProperty, model, x => x.Value);
        using var ui = Create(slider);
        ui.Update();
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 50, 20)).PointerCaptured);
        Assert.Equal(.25f, model.Value, 3);
        ui.HandleInput(new UiInput(InputKind.PointerMove, 150, 20));
        Assert.Equal(.75f, model.Value, 3);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerUp, 120, 20)).PointerCaptured);
        Assert.Equal(.6f, model.Value, 3);
        Assert.Equal(3, model.Writes);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left));
        Assert.Equal(.55f, slider.Value, 3);
        Assert.Equal(.55f, model.Value, 3);
        Assert.Equal(4, model.Writes);
    }

    [Fact]
    public void Slider_NormalizingModelWithoutNotify_AppliesCanonicalValueImmediately()
    {
        var model = new Model<float>(0) { NotifyOnWrite = false, Normalize = value => MathF.Round(value * 4) / 4 };
        var slider = new Slider().Width(200).BindTwoWay(ProgressBar.ValueProperty, model, x => x.Value);
        using var ui = Create(slider);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 51, 20));
        Assert.Equal(.25f, slider.Value);
        Assert.Equal(.25f, model.Value);
        Assert.Equal(1, model.Writes);
        ui.HandleInput(new UiInput(InputKind.PointerUp, 51, 20));
        Assert.Equal(.25f, slider.Value);
        Assert.Equal(2, model.Writes);
        ui.Update();
        Assert.Equal(2, model.Writes);
    }

    [Fact]
    public void UiEdit_AlreadyEqualToSource_DoesNotInvokeSetterOrRedrawAgain()
    {
        var model = new Model<string>("old");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        ui.Update();
        model.SetSilently("same");
        editor.Value = "same";
        Assert.Equal(0, model.Writes);
        Assert.True(ui.Update());
        model.Notify();
        Assert.False(ui.Update());
        Assert.Equal(0, model.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetterFailure_RestoresActualModelStateAndLaterInputRecovers(bool partialCommit)
    {
        var model = new Model<string>("original") { Reject = !partialCommit, ThrowAfterCommit = partialCommit };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        ui.Update(); ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.Throws<SetterException>(() => ui.HandleInput(new UiInput(InputKind.Text, Text: "attempt")));
        Assert.Equal(partialCommit ? "attempt" : "original", model.Value);
        Assert.Equal(model.Value, editor.Value);
        Assert.InRange(editor.CaretIndex, 0, editor.Value.Length);
        Assert.Equal(1, model.Writes);
        Assert.Equal(1, model.Subscribers);
        model.Reject = model.ThrowAfterCommit = false;
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "recovered")).Handled);
        Assert.Equal("recovered", model.Value);
        Assert.Equal("recovered", editor.Value);
        Assert.Equal(2, model.Writes);
    }

    [Fact]
    public void SetterAndReadbackFailure_AggregatesBothErrorsWithoutLosingConnection()
    {
        var model = new Model<string>("initial") { ThrowAfterCommit = true };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        model.BeforeWrite = _ => model.ThrowOnRead = true;
        var error = Assert.Throws<AggregateException>(() => editor.Value = "committed locally");
        Assert.Collection(error.InnerExceptions, item => Assert.IsType<SetterException>(item), item => Assert.IsType<ReadException>(item));
        Assert.Equal("committed locally", editor.Value);
        Assert.Equal(1, model.Writes);
        Assert.Equal(1, model.Subscribers);
        model.ThrowAfterCommit = model.ThrowOnRead = false; model.BeforeWrite = null;
        model.Publish("recover"); ui.Update();
        Assert.Equal("recover", editor.Value);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void CanonicalValue_FailsUiValidation_ReportsErrorAndRecoversOnValidSourceEvent()
    {
        var model = new Model<string>("initial") { Normalize = _ => "invalid\ntext" };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Value = "valid input");
        Assert.Equal("invalid\ntext", model.Value);
        Assert.Equal("valid input", editor.Value);
        Assert.Equal(1, model.Writes);
        model.Normalize = null; model.Publish("recovered"); ui.Update();
        Assert.Equal("recovered", editor.Value);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void ReentrantUiChange_InSourceSetter_DoesNotReenterSetterAndEndsCanonical()
    {
        var model = new Model<string>("initial") { Normalize = value => value.ToUpperInvariant() };
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        model.BeforeWrite = _ => editor.Value = "setter side effect";
        editor.Value = "requested";
        Assert.Equal("REQUESTED", model.Value);
        Assert.Equal("REQUESTED", editor.Value);
        Assert.Equal(1, model.Writes);
        ui.Update();
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void EarlierThrowingObservers_StillRunBindingAndLaterPropertyObservers()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox();
        using var ui = Create(editor);
        editor.Invalidated += _ => throw new ObserverException();
        editor.PropertyChanged += (_, _) => throw new ObserverException();
        var later = 0;
        editor.PropertyChanged += (_, _) => later++;
        // Bind an equal initial value so subscription creation itself is successful.
        model.SetSilently(editor.Value);
        editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        var error = Assert.Throws<AggregateException>(() => editor.Value = "edit");
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.All(error.InnerExceptions, item => Assert.IsType<ObserverException>(item));
        Assert.Equal("edit", model.Value);
        Assert.Equal("edit", editor.Value);
        Assert.Equal(1, model.Writes);
        Assert.Equal(1, later);
        Assert.True(ui.NeedsUpdate);
    }

    [Fact]
    public void GenericSet_TextBoxSelection_ClampsToGraphemeBeforeObservers()
    {
        var editor = new TextBox { Value = "abcd" };
        using var ui = Create(editor);
        ui.Update(); ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right, Shift: true));
        Assert.Equal(1, editor.SelectionStart);
        editor.PropertyChanged += (_, _) =>
        {
            Assert.Equal(0, editor.SelectionStart);
            Assert.Equal(0, editor.SelectionLength);
            Assert.Equal(0, editor.CaretIndex);
        };
        editor.Set(TextBox.ValueProperty, "👩‍💻");
        Assert.Equal("👩‍💻", editor.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slider_FailedDrag_ReleasesCaptureAndNextDragRecovers(bool withBinding)
    {
        var model = new Model<float>(0) { Reject = true };
        var slider = new Slider().Width(200);
        Action<Element, object> observer = (_, _) => throw new SetterException();
        if (withBinding) slider.BindTwoWay(ProgressBar.ValueProperty, model, x => x.Value);
        else slider.PropertyChanged += observer;
        using var ui = Create(slider);
        ui.Update();
        Assert.Throws<SetterException>(() => ui.HandleInput(new UiInput(InputKind.PointerDown, 50, 20)));
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerMove, 220, 20)).PointerCaptured);
        model.Reject = false;
        slider.PropertyChanged -= observer;
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, 100, 20)).PointerCaptured);
        Assert.False(ui.HandleInput(new UiInput(InputKind.PointerUp, 100, 20)).PointerCaptured);
        Assert.Equal(.5f, slider.Value);
        if (withBinding) Assert.Equal(.5f, model.Value);
    }

    [Fact]
    public void DetachReattachAndMove_KeepExactlyOneBidirectionalConnection()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        var root = new Panel().WithChildren(editor, new Text("neighbor"));
        using var ui = Create(root);
        root.Items.Move(0, 1);
        Assert.Equal(1, model.Adds);
        for (var i = 0; i < 3; i++)
        {
            root.Items.Remove(editor);
            Assert.Equal(0, model.Subscribers);
            model.Publish($"model {i}"); editor.Value = "detached UI";
            Assert.Equal(0, model.Writes);
            root.Items.Add(editor);
            Assert.Equal($"model {i}", editor.Value);
            Assert.Equal(1, model.Subscribers);
        }
        editor.Value = "attached UI";
        Assert.Equal("attached UI", model.Value);
        Assert.Equal(1, model.Writes);
        ui.Dispose();
        Assert.Equal(model.Adds, model.Removes);
        Assert.Equal(0, model.Subscribers);
        Assert.Throws<ObjectDisposedException>(() => editor.Value = "disposed");
    }

    [Fact]
    public void QueuedOldSourceEvent_ReattachSameSession_CannotOverwriteCurrentUi()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        var root = new Panel().WithChildren(editor);
        using var ui = Create(root);
        model.Publish("old queued");
        root.Items.Remove(editor);
        model.SetSilently("new attachment"); root.Items.Add(editor);
        var reads = model.Reads;
        model.SetSilently("silent value"); ui.Update();
        Assert.Equal("new attachment", editor.Value);
        Assert.Equal(reads, model.Reads);
        editor.Value = "user edit";
        Assert.Equal("user edit", model.Value);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void Transfer_ToNewUiThread_SkipsOldQueuedAndInFlightSourceHandlers()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        var root = new Panel().WithChildren(editor);
        using var oldUi = Create(root);
        model.Publish("old queued");
        var oldHandler = model.CaptureNotification();
        root.Items.Remove(editor);
        RunOnWorker(() =>
        {
            model.SetSilently("new source");
            using var newUi = Create(editor);
            Assert.Equal("new source", editor.Value);
            Assert.Equal(0, model.Writes);
            var reads = model.Reads;
            model.SetSilently("silent"); oldHandler(); newUi.Update();
            Assert.Equal(reads, model.Reads);
            Assert.Equal("new source", editor.Value);
            editor.Value = "new UI edit";
            Assert.Equal("new UI edit", model.Value);
            Assert.Equal(Environment.CurrentManagedThreadId, model.LastWriteThread);
            Assert.Equal(1, model.Writes);
        });
        var finalReads = model.Reads; oldUi.Update();
        Assert.Equal(finalReads, model.Reads);
        Assert.Equal("new UI edit", editor.Value);
        Assert.Equal(0, model.Subscribers);
        Assert.Equal(2, model.Adds);
        Assert.Equal(2, model.Removes);
    }

    [Fact]
    public void BackgroundSourceEvent_OnlyReadsAndMutatesDuringOwnerUpdate()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        ui.Update();
        var reads = model.Reads;
        RunOnWorker(() => model.Publish("background"));
        Assert.Equal("initial", editor.Value);
        Assert.Equal(reads, model.Reads);
        ui.Update();
        Assert.Equal("background", editor.Value);
        Assert.Equal(Environment.CurrentManagedThreadId, model.LastReadThread);
        Assert.Equal(0, model.Writes);
        RunOnWorker(() => Assert.Throws<InvalidOperationException>(() => editor.Value = "foreign edit"));
        Assert.Equal("background", editor.Value);
    }

    [Fact]
    public void Notification_DuringDetachBeforeUnsubscribe_CannotReadOrWriteSource()
    {
        var model = new Model<string>("initial");
        using var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        var queue = new Queue<Action>();
        var owner = Environment.CurrentManagedThreadId;
        var reads = 0;
        editor.Attach(() => Assert.Equal(owner, Environment.CurrentManagedThreadId), queue.Enqueue, _ =>
        {
            Assert.Equal(1, model.Subscribers);
            RunOnWorker(() => model.Publish("detach race"));
            editor.Value = "detached local";
            Assert.Equal(reads, model.Reads);
            Assert.Equal(0, model.Writes);
        });
        reads = model.Reads; editor.Detach();
        Assert.Equal(0, model.Subscribers);
        Assert.Equal("detached local", editor.Value);
        Assert.Empty(queue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bind_DuplicateDirection_IsRejectedWithoutReplacingOriginal(bool firstTwoWay)
    {
        var model = new Model<string>("original");
        var other = new Model<string>("replacement");
        var editor = new TextBox();
        if (firstTwoWay) editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        else editor.Bind(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        Assert.Throws<InvalidOperationException>(() => editor.BindTwoWay(TextBox.ValueProperty, other, x => x.Value));
        Assert.Equal(1, model.Subscribers); Assert.Equal(0, other.Subscribers);
        editor.Value = "edit";
        Assert.Equal(firstTwoWay ? "edit" : "original", model.Value);
        Assert.Equal(firstTwoWay ? 1 : 0, model.Writes);
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("private")]
    [InlineData("init")]
    [InlineData("nested")]
    [InlineData("computed")]
    [InlineData("field")]
    public void BindTwoWay_UnsupportedSelector_IsRejectedBeforeSubscription(string kind)
    {
        var model = new Model<string>("value");
        Expression<Func<Model<string>, string>> selector = kind switch
        {
            "readonly" => x => x.ReadOnly,
            "private" => x => x.PrivateSetter,
            "init" => x => x.InitOnly,
            "nested" => x => x.Child.Value,
            "computed" => x => x.Value.ToUpperInvariant(),
            _ => x => x.Field
        };
        var editor = new TextBox();
        using var ui = Create(editor);
        Assert.Throws<ArgumentException>(() => editor.BindTwoWay(TextBox.ValueProperty, model, selector));
        Assert.Equal(0, model.Subscribers);
        editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        Assert.Equal("value", editor.Value);
        Assert.Equal(1, model.Subscribers);
    }

    [Fact]
    public void BindTwoWay_ConvertedOrNullSelectorsAndArguments_AreRejected()
    {
        var model = new Model<string>("value");
        using var editor = new TextBox();
        var property = new UiProperty<object>("Object", "");
        Assert.Throws<ArgumentException>(() => editor.BindTwoWay(property, model, x => (object)x.Value));
        Assert.Throws<ArgumentNullException>(() => ((TextBox)null!).BindTwoWay(TextBox.ValueProperty, model, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => editor.BindTwoWay(null!, model, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => editor.BindTwoWay(TextBox.ValueProperty, (Model<string>)null!, x => x.Value));
        Assert.Throws<ArgumentNullException>(() => editor.BindTwoWay(TextBox.ValueProperty, model, null!));
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public void InitialGetterFailure_SessionRollsBackBothDirectionsAndRetryWorks()
    {
        var model = new Model<string>("initial") { ThrowOnRead = true };
        using var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        Assert.Throws<ReadException>(() => Create(editor));
        Assert.False(editor.IsAttached);
        Assert.Equal(0, model.Subscribers);
        editor.Value = "detached";
        Assert.Equal(0, model.Writes);
        model.ThrowOnRead = false;
        using var ui = Create(editor);
        Assert.Equal("initial", editor.Value);
        editor.Value = "works";
        Assert.Equal("works", model.Value);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void SourceChange_FromUiObserverDuringSync_IsQueuedWithoutWriteBack()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        editor.PropertyChanged += (_, _) =>
        {
            if (editor.Value == "first source") model.Publish("second source");
        };
        model.Publish("first source");
        ui.Update();
        Assert.Equal("second source", editor.Value);
        Assert.Equal("second source", model.Value);
        Assert.Equal(0, model.Writes);
    }

    [Fact]
    public void BackgroundEvent_DuringUiWrite_IsStillQueuedForOwnerThread()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        ui.Update();
        model.BeforeWrite = _ => RunOnWorker(() => model.Publish("background before commit"));
        editor.Value = "ui commit";
        Assert.Equal("ui commit", editor.Value);
        var reads = model.Reads;
        model.SetSilently("later source state");
        ui.Update();
        Assert.True(model.Reads > reads);
        Assert.Equal("later source state", editor.Value);
        Assert.Equal(Environment.CurrentManagedThreadId, model.LastReadThread);
        Assert.Equal(1, model.Writes);
    }

    [Fact]
    public void InitialGetterFailure_AttachedBind_ReleasesSlotAndUiHandlerBeforeRetry()
    {
        var model = new Model<string>("source") { ThrowOnRead = true };
        var editor = new TextBox { Value = "local" };
        using var ui = Create(editor);
        Assert.Throws<ReadException>(() => editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value));
        Assert.Equal(0, model.Subscribers);
        editor.Value = "after failure";
        Assert.Equal(0, model.Writes);
        model.ThrowOnRead = false;
        editor.BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        editor.Value = "retry";
        Assert.Equal("retry", model.Value);
        Assert.Equal(1, model.Writes);
        Assert.Equal(1, model.Subscribers);
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(1f, true)]
    [InlineData(-.01f, false)]
    [InlineData(float.NaN, false)]
    public void LocalUiValidation_RejectsBeforeWriteBackAndAcceptsEndpoints(float value, bool valid)
    {
        var model = new Model<float>(.5f);
        var slider = new Slider().BindTwoWay(ProgressBar.ValueProperty, model, x => x.Value);
        using var ui = Create(slider);
        if (valid) slider.Value = value;
        else Assert.Throws<ArgumentOutOfRangeException>(() => slider.Value = value);
        Assert.Equal(valid ? value : .5f, slider.Value);
        Assert.Equal(valid ? value : .5f, model.Value);
        Assert.Equal(valid ? 1 : 0, model.Writes);
    }

    [Fact]
    public void SourceGetterFailure_QueuedSync_DoesNotWriteAndNextEventRecovers()
    {
        var model = new Model<string>("initial");
        var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, model, x => x.Value);
        using var ui = Create(editor);
        model.ThrowOnRead = true; model.Publish("pending");
        Assert.Throws<ReadException>(() => ui.Update());
        Assert.Equal("initial", editor.Value);
        Assert.Equal(0, model.Writes);
        model.ThrowOnRead = false; model.Notify();
        ui.Update();
        Assert.Equal("pending", editor.Value);
        Assert.Equal(0, model.Writes);
        Assert.Equal(1, model.Subscribers);
    }

    private static void RunOnWorker(Action action)
    {
        Exception? error = null;
        var worker = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }) { IsBackground = true };
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "Worker did not finish.");
        Assert.Null(error);
    }

    private sealed class SetterException : Exception;
    private sealed class ReadException : Exception;
    private sealed class ObserverException : Exception;
    private sealed class Model<T>(T initial) : INotifyPropertyChanged
    {
        private T _value = initial;
        private PropertyChangedEventHandler? _changed;
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public int LastReadThread { get; private set; }
        public int LastWriteThread { get; private set; }
        public Func<T, T>? Normalize { get; set; }
        public Action<T>? BeforeWrite { get; set; }
        public bool Reject { get; set; }
        public bool ThrowAfterCommit { get; set; }
        public bool ThrowOnRead { get; set; }
        public bool NotifyOnWrite { get; set; } = true;
        public Model<T> Child => this;
        public T ReadOnly => _value;
        public T PrivateSetter { get; private set; } = initial;
        public T InitOnly { get; init; } = initial;
        public T Field = initial;
        public T Value
        {
            get { Reads++; LastReadThread = Environment.CurrentManagedThreadId; if (ThrowOnRead) throw new ReadException(); return _value; }
            set
            {
                Writes++; LastWriteThread = Environment.CurrentManagedThreadId; BeforeWrite?.Invoke(value);
                if (Reject) throw new SetterException();
                _value = Normalize == null ? value : Normalize(value);
                if (NotifyOnWrite) Notify();
                if (ThrowAfterCommit) throw new SetterException();
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; Adds++; }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
        public void SetSilently(T value) => _value = value;
        public void Publish(T value) { _value = value; Notify(); }
        public void Notify() => _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        public Action CaptureNotification() { var handler = _changed; return () => handler?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    private sealed class Clipboard : IUiClipboard
    {
        public string? Text { get; set; }
        public string? GetText() => Text;
        public void SetText(string text) => Text = text;
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
