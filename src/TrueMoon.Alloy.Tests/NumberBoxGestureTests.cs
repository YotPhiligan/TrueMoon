using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class NumberBoxGestureTests
{
    [Theory]
    [InlineData("")] [InlineData("X")]
    public void Drag_UsesCumulativeFourPixelTicksReversalAndReleasePositionWithoutApplying(string prefix)
    {
        using var test = new GestureFixture(prefix); var start = test.Start();
        Assert.True(test.Box.Editor.IsFocused); Assert.Equal(1, test.Starts); Assert.False(test.Box.TryStep(1));
        test.Move(start + 3); Assert.Equal("001.50", test.Box.Editor.Value);
        test.Move(start + 4); Assert.Equal("1.6", test.Box.Editor.Value);
        test.Move(start + 5); Assert.Equal("1.6", test.Box.Editor.Value);
        test.Move(start + 8); Assert.Equal("1.7", test.Box.Editor.Value);
        test.Move(start - 3); Assert.Equal("001.50", test.Box.Editor.Value);
        test.Move(start - 4); Assert.Equal("1.4", test.Box.Editor.Value);
        var release = test.Ui.HandleInput(new(InputKind.PointerUp, start + 12, 15));
        Assert.False(release.PointerCaptured); Assert.False(test.Box.IsDragging); Assert.Equal("1.8", test.Box.Editor.Value);
        Assert.Equal(new[] { NumericGestureResult.Completed }, test.Ends);
        test.Box.CancelGesture(); Assert.Equal("1.8", test.Box.Editor.Value); Assert.Single(test.Ends);
    }

    [Theory]
    [InlineData("escape")] [InlineData("input-lost")] [InlineData("focus")] [InlineData("hidden")]
    [InlineData("disabled")] [InlineData("readOnly")] [InlineData("detach")] [InlineData("adjustment")] [InlineData("style")]
    public void Cancellation_RestoresExactOriginalDraftAndReleasesCaptureOnce(string reason)
    {
        using var test = new GestureFixture();
        if (reason == "style") { test.Box.Style = new Style().With(NumberBox.AdjustmentEnabledProperty, true); test.Box.Clear(NumberBox.AdjustmentEnabledProperty); test.Ui.Update(); }
        var start = test.Start(); test.Move(start + 8);
        Assert.Equal("1.7", test.Box.Editor.Value);
        switch (reason)
        {
            case "escape": test.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); break;
            case "input-lost": test.Ui.HandleInput(new(InputKind.FocusLost)); break;
            case "focus": test.Ui.Focus(test.Other); break;
            case "hidden": test.Box.IsVisible = false; test.Ui.Update(); break;
            case "disabled": test.Box.IsEnabled = false; test.Ui.Update(); break;
            case "readOnly": test.Box.Editor.IsReadOnly = true; break;
            case "detach": test.Root.Items.Remove(test.Box); break;
            case "adjustment": test.Box.AdjustmentEnabled = false; break;
            case "style": test.Box.Style = new Style().With(NumberBox.AdjustmentEnabledProperty, false); break;
        }
        Assert.Equal("001.50", test.Box.Editor.Value); Assert.False(test.Box.IsDragging);
        Assert.False(test.Ui.HandleInput(new(InputKind.PointerMove, 300, 110)).PointerCaptured);
        Assert.Equal(new[] { NumericGestureResult.Cancelled }, test.Ends); test.Box.CancelGesture(); Assert.Single(test.Ends);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExternalValueOrStaleIdentity_InterruptsWithoutRollingBackExternalState(bool staleSameValue)
    {
        using var test = new GestureFixture(); var current = true; test.Box.IsGestureCurrent = () => current;
        var start = test.Start(); test.Move(start + 8);
        if (staleSameValue) { current = false; test.Box.CancelGesture(); }
        else test.Box.Editor.Value = "external canonical";
        Assert.Equal(staleSameValue ? "1.7" : "external canonical", test.Box.Editor.Value);
        Assert.False(test.Box.IsDragging); Assert.Equal(new[] { NumericGestureResult.Interrupted }, test.Ends);
        Assert.False(test.Ui.HandleInput(new(InputKind.PointerUp, start, 15)).PointerCaptured);
        test.Box.CancelGesture(); Assert.Single(test.Ends); Assert.Equal(staleSameValue ? "1.7" : "external canonical", test.Box.Editor.Value);
    }

    [Fact]
    public void DragModifiers_AreCapturedAtStartAndBoundsRejectRatherThanClamp()
    {
        using var test = new GestureFixture(); test.Box.Minimum = 0; test.Box.Maximum = 4;
        var start = test.Start(shift: true); test.Move(start + 4); Assert.Equal("2.5", test.Box.Editor.Value);
        test.Move(start + 12); Assert.Equal("2.5", test.Box.Editor.Value);
        test.Box.CancelGesture(); Assert.Equal("001.50", test.Box.Editor.Value);
        start = test.Start(control: true); test.Move(start + 4); Assert.Equal("1.51", test.Box.Editor.Value);
        test.Ui.HandleInput(new(InputKind.PointerUp, start + 4, 15, Shift: true)); Assert.Equal("1.51", test.Box.Editor.Value);
        Assert.Equal(new[] { NumericGestureResult.Cancelled, NumericGestureResult.Completed }, test.Ends);
    }

    [Theory]
    [InlineData("start")] [InlineData("move")] [InlineData("end")] [InlineData("identity")]
    public void CallbackFailures_ReleaseCaptureAndNeverLeaveActiveGesture(string phase)
    {
        using var test = new GestureFixture(); var failure = new IOException(phase); float start;
        if (phase == "start") test.Box.GestureStarted += () => throw failure;
        if (phase == "end") test.Box.GestureEnded += _ => throw failure;
        if (phase == "start") Assert.Same(failure, Assert.Throws<IOException>(() => test.Start()));
        else
        {
            start = test.Start();
            if (phase == "move") test.Box.Editor.PropertyChanged += (_, property) => { if (ReferenceEquals(property, TextBox.ValueProperty)) throw failure; };
            if (phase == "identity") test.Box.IsGestureCurrent = () => throw failure;
            var actual = Record.Exception(() => test.Ui.HandleInput(new(phase == "end" ? InputKind.PointerUp : InputKind.PointerMove, start + 4, 15)));
            Assert.NotNull(actual);
            IEnumerable<Exception> errors = actual is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : new[] { actual };
            Assert.Contains<Exception>(failure, errors);
        }
        Assert.False(test.Box.IsDragging); Assert.Single(test.Ends);
        Assert.False(test.Ui.HandleInput(new(InputKind.PointerMove, 300, 110)).PointerCaptured);
    }

    [Fact]
    public void DisposeDuringDrag_RestoresDraftReleasesCaptureAndOwnedPartsOnce()
    {
        var box = new NumberBox("X") { AdjustmentEnabled = true }; box.Editor.Value = "001.50";
        var resource = new DisposalCounter(); box.Editor.Own(resource);
        var results = new List<NumericGestureResult>(); box.GestureEnded += results.Add;
        using var ui = CompactControlHarness.Ui(box, 400, 32); ui.Update();
        var x = box.DragBounds.Width / 2; Assert.True(ui.HandleInput(new(InputKind.PointerDown, x, 15)).PointerCaptured);
        ui.HandleInput(new(InputKind.PointerMove, x + 4, 15)); Assert.True(box.IsDragging); Assert.Equal("1.6", box.Editor.Value);
        box.Dispose(); box.Dispose();
        Assert.Equal("001.50", box.Editor.Value); Assert.True(box.Editor.IsDisposed); Assert.True(box.Prefix.IsDisposed); Assert.Equal(1, resource.Count);
        Assert.Equal(new[] { NumericGestureResult.Cancelled }, results); Assert.False(box.IsDragging);
        Assert.False(ui.HandleInput(new(InputKind.PointerMove, 300, 20)).PointerCaptured);
    }

    private sealed class GestureFixture : IDisposable
    {
        public NumberBox Box { get; }
        public Button Other { get; } = new("other");
        public VStack Root { get; } = new();
        public UiSession Ui { get; }
        public int Starts;
        public List<NumericGestureResult> Ends { get; } = [];
        public GestureFixture(string prefix = "X")
        {
            Box = new(prefix) { AdjustmentEnabled = true, Height = 32 }; Box.Editor.Value = "001.50";
            Box.GestureStarted += () => Starts++; Box.GestureEnded += Ends.Add;
            Root.Add(Box); Root.Add(Other); Ui = CompactControlHarness.Ui(Root, 400, 120); Ui.Update();
        }
        public float Start(bool shift = false, bool control = false)
        {
            var x = Box.DragBounds.X + Box.DragBounds.Width / 2;
            Assert.True(Ui.HandleInput(new(InputKind.PointerDown, x, 15, Shift: shift, Control: control)).PointerCaptured); Assert.True(Box.IsDragging); return x;
        }
        public void Move(float x) => Ui.HandleInput(new(InputKind.PointerMove, x, 15));
        public void Dispose() { Ui.Dispose(); if (!Box.IsDisposed) Box.Dispose(); }
    }
}
