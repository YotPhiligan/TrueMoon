using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class OwnershipTests
{
    private static UiSession Create(Element root, HeadlessSurface? surface = null) =>
        new(root, surface ?? new HeadlessSurface(), new UiViewport(240, 140));

    [Fact]
    public void Remove_ReattachBeforeUpdate_DoesNotRestoreFocusOrCapture()
    {
        var root = new Panel();
        var button = new Button("Action");
        var clicks = 0;
        button.Click += () => clicks++;
        root.Add(button);
        using var ui = Create(root);
        ui.Update();
        var down = ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        Assert.True(down.PointerCaptured);
        Assert.True(down.KeyboardFocused);
        Assert.True(button.IsPressed);
        Assert.True(button.IsHovered);

        Assert.True(root.Items.Remove(button));
        Assert.Null(button.Parent);
        Assert.False(button.IsAttached);
        Assert.False(button.IsDisposed);
        Assert.False(button.IsPressed);
        Assert.False(button.IsHovered);
        root.Add(button);

        var up = ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20));
        Assert.False(up.PointerCaptured);
        Assert.False(up.KeyboardFocused);
        Assert.False(button.IsFocused);
        Assert.False(ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space)).Handled);
        Assert.Equal(0, clicks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replace_FocusedCapturedSubtree_ReturnsLiveSubtreeToCaller(bool collection)
    {
        var root = new Panel();
        var holder = new ContentControl();
        var old = new Panel();
        var button = new Button("Old");
        var lifetime = new DisposalCounter();
        old.Own(lifetime);
        old.Add(button);
        if (collection) root.Add(old);
        else { root.Add(holder); holder.SetContent(old); }
        var replacement = new Button("New");
        using var ui = Create(root);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));

        if (collection) root.Items[0] = replacement;
        else holder.SetContent(replacement);

        Assert.Null(old.Parent);
        Assert.False(old.IsAttached);
        Assert.False(button.IsAttached);
        Assert.False(button.IsFocused);
        Assert.False(button.IsPressed);
        Assert.False(old.IsDisposed);
        Assert.Equal(0, lifetime.Count);
        Assert.True(replacement.IsAttached);
        Assert.True(ui.Update());
        var result = ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space));
        Assert.False(result.Handled);
        Assert.False(result.PointerCaptured);
        Assert.False(result.KeyboardFocused);
        Assert.False(replacement.IsFocused);
        ui.Dispose();
        Assert.True(replacement.IsDisposed);
        Assert.False(old.IsDisposed);
        old.Dispose();
        old.Dispose();
        Assert.True(button.IsDisposed);
        Assert.Equal(1, lifetime.Count);
    }

    [Fact]
    public void Replace_UnrelatedBranch_PreservesEditorSelectionFocusAndScroll()
    {
        var root = new Panel();
        var holder = new ContentControl();
        var old = new SizedElement();
        holder.SetContent(old);
        var editor = new TextBox { Value = "Привет world" };
        var scroll = new ScrollViewer { Height = 80 };
        scroll.SetContent(new SizedElement(200, 500));
        root.Add(holder).Add(scroll).Add(editor);
        using var ui = Create(root);
        ui.Update();
        ui.Focus(editor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.Capture(editor);
        scroll.Offset = 45;
        ui.Update();

        holder.SetContent(new SizedElement(40, 30));
        ui.Update();

        Assert.Same(editor, root.Items[2]);
        Assert.True(editor.IsFocused);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal("Привет world".Length, editor.SelectionLength);
        Assert.Equal(45, scroll.Offset);
        var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.C, Control: true));
        Assert.True(result.Handled);
        Assert.True(result.KeyboardFocused);
        Assert.True(result.PointerCaptured);
        old.Dispose();
    }

    [Fact]
    public void Transfer_BetweenSessions_OldSessionCannotCancelNewInteraction()
    {
        var first = new Panel();
        var second = new Panel();
        var subtree = new Panel();
        var button = new Button("Transfer");
        subtree.Add(button);
        first.Add(subtree);
        var clicks = 0;
        button.Click += () => clicks++;
        using var oldUi = Create(first);
        using var newUi = Create(second);
        oldUi.Update();
        oldUi.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));

        first.Items.Remove(subtree);
        second.Add(subtree);
        newUi.Update();
        newUi.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        oldUi.Update();
        var oldResult = oldUi.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space));
        oldUi.Dispose();

        Assert.False(oldResult.Handled);
        Assert.False(oldResult.KeyboardFocused);
        Assert.False(oldResult.PointerCaptured);
        Assert.True(button.IsFocused);
        Assert.True(button.IsHovered);
        Assert.True(button.IsPressed);
        Assert.False(subtree.IsDisposed);
        Assert.Same(second, subtree.Parent);
        Assert.True(newUi.HandleInput(new UiInput(InputKind.PointerUp, 20, 20)).Handled);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Move_AttachedChild_PreservesCaptureAndAttachmentSubscriptions()
    {
        var root = new Panel();
        var button = new Button("Move");
        root.Add(new SizedElement()).Add(button);
        using var ui = Create(root);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        var subscription = new DisposalCounter();
        button.OwnAttachment(subscription);
        var changes = 0;
        button.AttachmentChanged += () => changes++;

        root.Items.Move(1, 0);
        ui.Update();

        Assert.Same(button, root.Items[0]);
        Assert.True(button.IsFocused);
        Assert.True(button.IsPressed);
        Assert.Equal(0, changes);
        Assert.Equal(0, subscription.Count);
        var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None));
        Assert.True(result.PointerCaptured);
        ui.Dispose();
        Assert.Equal(1, subscription.Count);
    }

    [Fact]
    public void AttachmentSubscriptions_ReattachReconnects_LifetimeSubscriptionsSurviveRemoval()
    {
        var root = new Panel();
        var subtree = new Panel();
        var child = new SizedElement();
        subtree.Add(child);
        root.Add(subtree);
        var lifetime = new DisposalCounter();
        child.Own(lifetime);
        var publisher = new Publisher();
        child.AttachmentChanged += () =>
        {
            if (child.IsAttached) child.OwnAttachment(publisher.Subscribe(() => child.Width = 77));
        };
        using var ui = Create(root);
        Assert.Equal(1, publisher.Subscribers);
        publisher.Publish();
        Assert.Equal(77, child.Width);

        root.Items.Remove(subtree);
        Assert.Equal(0, publisher.Subscribers);
        Assert.Equal(0, lifetime.Count);
        child.Width = 33;
        publisher.Publish();
        Assert.Equal(33, child.Width);
        root.Add(subtree);
        Assert.Equal(1, publisher.Subscribers);
        publisher.Publish();
        Assert.Equal(77, child.Width);
        ui.Dispose();
        Assert.Equal(0, publisher.Subscribers);
        Assert.Equal(1, lifetime.Count);
        Assert.Equal(2, publisher.Unsubscribes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear_MultipleChildren_DetachesAllAndReturnsOwnership(bool throwOnDetach)
    {
        var root = new Panel();
        var first = new Panel();
        var button = new Button("Nested");
        first.Add(button);
        var second = new SizedElement();
        root.Add(first).Add(second);
        using var ui = Create(root);
        var firstSubscription = new DisposalCounter();
        var secondSubscription = new DisposalCounter();
        button.OwnAttachment(firstSubscription);
        second.OwnAttachment(secondSubscription);
        first.AttachmentChanged += () => { if (!first.IsAttached && throwOnDetach) throw new TestException(); };

        if (throwOnDetach) Assert.Throws<TestException>(root.Items.Clear);
        else root.Items.Clear();

        Assert.Empty(root.Children);
        Assert.Null(first.Parent);
        Assert.Null(second.Parent);
        Assert.False(first.IsAttached);
        Assert.False(button.IsAttached);
        Assert.False(second.IsAttached);
        Assert.Equal(1, firstSubscription.Count);
        Assert.Equal(1, secondSubscription.Count);
        ui.Dispose();
        Assert.False(first.IsDisposed);
        Assert.False(second.IsDisposed);
        first.Dispose();
        second.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispatch_RemovedOrReattachedElement_DropsWorkFromOldAttachment(bool transfer)
    {
        var first = new Panel();
        var second = new Panel();
        var child = new SizedElement { Width = 30 };
        first.Add(child);
        using var oldUi = Create(first);
        using var newUi = Create(second);
        child.Dispatch(() => child.Width = 99);

        first.Items.Remove(child);
        (transfer ? second : first).Add(child);
        oldUi.Update();
        Assert.Equal(30, child.Width);
        child.Dispatch(() => child.Width = 42);
        (transfer ? newUi : oldUi).Update();
        Assert.Equal(42, child.Width);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replacement_NotificationFailure_CommitsCoherentTreeAndNotifiesOtherObservers(bool collection)
    {
        var root = new Panel();
        var holder = new ContentControl();
        var original = new SizedElement();
        if (collection) root.Add(original);
        else { root.Add(holder); holder.SetContent(original); }
        using var ui = Create(root);
        ui.Update();
        var candidate = new SizedElement();
        var laterObserver = 0;
        original.AttachmentChanged += () => throw new TestException();
        original.AttachmentChanged += () =>
        {
            Assert.Null(original.Parent);
            Assert.False(original.IsAttached);
            Assert.Same(collection ? root : holder, candidate.Parent);
            Assert.True(candidate.IsAttached);
            laterObserver++;
        };

        Assert.Throws<TestException>(() =>
        {
            if (collection) root.Items[0] = candidate;
            else holder.SetContent(candidate);
        });

        Assert.Equal(1, laterObserver);
        Assert.True(ui.NeedsUpdate);
        Assert.True(ui.Update());
        original.Dispose();
    }

    [Fact]
    public void Add_ThemeAndAttachmentNotificationFailures_StillAttachesWholeSubtree()
    {
        var root = new Panel();
        using var ui = Create(root);
        ui.SetTheme(Theme.Light);
        ui.Update();
        var subtree = new Panel();
        var child = new SizedElement();
        subtree.Add(child);
        child.Invalidated += _ => throw new TestException();
        var notified = 0;
        subtree.AttachmentChanged += () => throw new TestException();
        subtree.AttachmentChanged += () => notified++;

        var error = Assert.Throws<AggregateException>(() => root.Add(subtree));

        Assert.NotEmpty(error.InnerExceptions);
        Assert.Same(subtree, Assert.Single(root.Children));
        Assert.Same(root, subtree.Parent);
        Assert.True(subtree.IsAttached);
        Assert.True(child.IsAttached);
        Assert.Same(Theme.Light, child.Theme);
        Assert.Equal(1, notified);
        Assert.True(ui.NeedsUpdate);
        Assert.True(ui.Update());
        // The deliberately throwing invalidation handler also participates in detach.
        Assert.ThrowsAny<Exception>(ui.Dispose);
        Assert.True(subtree.IsDisposed);
    }

    [Fact]
    public void Mutation_FromAttachmentNotification_IsRejectedBeforeChangingTree()
    {
        var root = new Panel();
        using var ui = Create(root);
        var subtree = new Panel();
        var unexpected = new SizedElement();
        subtree.AttachmentChanged += () =>
        {
            if (subtree.IsAttached) subtree.Add(unexpected);
        };

        Assert.Throws<InvalidOperationException>(() => root.Add(subtree));

        Assert.Same(subtree, Assert.Single(root.Children));
        Assert.True(subtree.IsAttached);
        Assert.Empty(subtree.Children);
        Assert.Null(unexpected.Parent);
        Assert.False(unexpected.IsAttached);
        subtree.Add(unexpected); // The guard is released even after a notification failure.
        Assert.Same(unexpected, Assert.Single(subtree.Children));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidReplacement_PreservesFocusedCapturedOriginal(bool disposed)
    {
        var holder = new ContentControl();
        var original = new Button("Original");
        holder.SetContent(original);
        using var ui = Create(holder);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        var candidate = new SizedElement();
        using var other = new Panel();
        if (disposed) candidate.Dispose();
        else other.Add(candidate);

        if (disposed) Assert.Throws<ObjectDisposedException>(() => holder.SetContent(candidate));
        else Assert.Throws<InvalidOperationException>(() => holder.SetContent(candidate));

        Assert.Same(original, holder.Child);
        Assert.Same(holder, original.Parent);
        Assert.True(original.IsAttached);
        Assert.True(original.IsFocused);
        Assert.True(original.IsPressed);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
    }

    [Fact]
    public void InvalidMoveAndInsert_DoNotDetachOrAdoptChildren()
    {
        var root = new Panel();
        var child = new Button("Keep");
        root.Add(child);
        using var candidate = new SizedElement();
        using var ui = Create(root);
        ui.Update();
        ui.Focus(child);
        ui.Capture(child);

        Assert.Throws<ArgumentOutOfRangeException>(() => root.Items.Insert(2, candidate));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.Items.Move(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => root.Items.Move(-1, 0));

        Assert.Same(child, Assert.Single(root.Children));
        Assert.True(child.IsAttached);
        Assert.True(child.IsFocused);
        Assert.Null(candidate.Parent);
        Assert.False(candidate.IsAttached);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
    }

    [Fact]
    public void OwnedChild_DirectDetachOrDispose_IsRejectedUntilRemoval()
    {
        var root = new Panel();
        var child = new Button("Keep");
        root.Add(child);
        using var ui = Create(root);
        var resource = new DisposalCounter();
        child.OwnAttachment(resource);
        var clicks = 0;
        child.Click += () => clicks++;

        Assert.Throws<InvalidOperationException>(child.Detach);
        Assert.Throws<InvalidOperationException>(child.Dispose);

        Assert.True(child.IsAttached);
        Assert.False(child.IsDisposed);
        Assert.Same(root, child.Parent);
        Assert.Equal(0, resource.Count);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        ui.HandleInput(new UiInput(InputKind.PointerUp, 20, 20));
        Assert.Equal(1, clicks);
        root.Items.Remove(child);
        child.Dispose();
        Assert.Equal(1, resource.Count);
        Assert.True(child.IsDisposed);
    }

    [Fact]
    public void AttachmentSubscription_UnattachedOrNull_IsRejectedWithoutTakingOwnership()
    {
        using var child = new SizedElement();
        var subscription = new DisposalCounter();
        Assert.Throws<InvalidOperationException>(() => child.OwnAttachment(subscription));
        Assert.Throws<ArgumentNullException>(() => child.OwnAttachment(null!));
        Assert.Throws<ArgumentNullException>(() => child.Own(null!));
        child.Dispose();
        Assert.Equal(0, subscription.Count);
    }

    [Fact]
    public void SessionConstruction_AttachmentFailure_RollsBackAllNodesWithoutTakingOwnership()
    {
        using var root = new Panel();
        var first = new SizedElement();
        var second = new SizedElement();
        root.Add(first).Add(second);
        var subscription = new DisposalCounter();
        first.AttachmentChanged += () =>
        {
            if (first.IsAttached) { first.OwnAttachment(subscription); throw new TestException(); }
        };
        using var surface = new HeadlessSurface();

        Assert.Throws<TestException>(() => Create(root, surface));

        Assert.False(root.IsAttached);
        Assert.False(first.IsAttached);
        Assert.False(second.IsAttached);
        Assert.False(root.IsDisposed);
        Assert.Equal(1, subscription.Count);
        Assert.Equal(0, surface.Disposals);
        first.Width = 42;
    }

    [Fact]
    public void Dispose_ThrowingSubscriptions_StillReleasesSiblingsSurfaceAndRegistryExactlyOnce()
    {
        var root = new Panel();
        var first = new Button("First");
        var second = new SizedElement();
        root.Add(first).Add(second);
        var bad = new ThrowingDisposable();
        var attachment = new DisposalCounter();
        var lifetime = new DisposalCounter();
        first.Own(bad);
        second.Own(lifetime);
        var surface = new HeadlessSurface();
        var ui = Create(root, surface);
        first.OwnAttachment(new ThrowingDisposable());
        second.OwnAttachment(attachment);
        var registryRemovals = 0;
        ui.Disposed += _ => registryRemovals++;

        Assert.Throws<AggregateException>(ui.Dispose);
        ui.Dispose();

        Assert.True(ui.IsDisposed);
        Assert.True(root.IsDisposed);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.False(first.IsAttached);
        Assert.Equal(1, bad.Count);
        Assert.Equal(1, attachment.Count);
        Assert.Equal(1, lifetime.Count);
        Assert.Equal(1, surface.Disposals);
        Assert.Equal(1, registryRemovals);
    }

    [Fact]
    public void SetContent_Null_DetachesSubtreeAndStopsItsInvalidationBubbling()
    {
        var holder = new ContentControl();
        var old = new Panel();
        var button = new Button("Remove");
        old.Add(button);
        holder.SetContent(old);
        using var ui = Create(holder);
        ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));

        holder.SetContent(null);
        Assert.Null(holder.Child);
        Assert.Empty(holder.Children);
        Assert.Null(old.Parent);
        Assert.False(button.IsAttached);
        Assert.False(button.IsPressed);
        Assert.True(ui.Update());
        Assert.False(ui.NeedsUpdate);
        button.Width = 50;
        Assert.False(ui.NeedsUpdate);
        var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Enter));
        Assert.False(result.KeyboardFocused);
        Assert.False(result.PointerCaptured);
        ui.Dispose();
        Assert.False(old.IsDisposed);
        old.Dispose();
    }

    [Fact]
    public void IdentityMutations_RequireOwnerThreadAndLiveOwnerWithoutDetaching()
    {
        var root = new Panel();
        var holder = new ContentControl();
        var child = new Button("Keep");
        holder.SetContent(child);
        root.Add(holder);
        using var ui = Create(root);
        ui.Update();
        ui.Focus(child);
        ui.Capture(child);
        var changes = 0;
        child.AttachmentChanged += () => changes++;
        var errors = new List<Exception?>();
        var thread = new Thread(() =>
        {
            errors.Add(Record.Exception(() => holder.SetContent(child)));
            errors.Add(Record.Exception(() => root.Items[0] = holder));
            errors.Add(Record.Exception(() => root.Items.Move(0, 0)));
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Equal(3, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));

        holder.SetContent(child);
        root.Items[0] = holder;
        root.Items.Move(0, 0);
        Assert.Equal(0, changes);
        Assert.True(child.IsFocused);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
        ui.Dispose();
        Assert.Throws<ObjectDisposedException>(() => holder.SetContent(child));
        Assert.Throws<ObjectDisposedException>(() => root.Items[0] = holder);
    }

    [Fact]
    public void SessionDispose_FromTreeNotification_IsRejectedBeforeReleasingResources()
    {
        var root = new Panel();
        var surface = new HeadlessSurface();
        using var ui = Create(root, surface);
        var child = new SizedElement();
        child.AttachmentChanged += () => { if (child.IsAttached) ui.Dispose(); };

        Assert.Throws<InvalidOperationException>(() => root.Add(child));

        Assert.False(ui.IsDisposed);
        Assert.False(root.IsDisposed);
        Assert.Equal(0, surface.Disposals);
        Assert.True(child.IsAttached);
        Assert.True(ui.Update());
        ui.Dispose();
        Assert.True(child.IsDisposed);
        Assert.Equal(1, surface.Disposals);
    }

    [Fact]
    public void Transfer_ToDifferentThread_OldSessionDropsQueuedWorkAndInputReferences()
    {
        var first = new Panel();
        var subtree = new Panel();
        var button = new Button("Transfer");
        subtree.Add(button);
        first.Add(subtree);
        using var oldUi = Create(first);
        oldUi.Update();
        oldUi.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        button.Dispatch(() => button.Width = 99);
        first.Items.Remove(subtree);
        using var ready = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var second = new Panel();
                using var newUi = Create(second);
                second.Add(subtree);
                newUi.Update();
                newUi.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
                ready.Set();
                Assert.True(finish.Wait(TimeSpan.FromSeconds(10)));
                Assert.True(button.IsFocused);
                Assert.True(button.IsPressed);
                Assert.True(float.IsNaN(button.Width));
                Assert.True(newUi.HandleInput(new UiInput(InputKind.PointerUp, 20, 20)).Handled);
            }
            catch (Exception error) { failure = error; ready.Set(); }
        });
        thread.Start();
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
            oldUi.Update();
            Assert.False(oldUi.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space)).KeyboardFocused);
            oldUi.Dispose();
        }
        finally { finish.Set(); Assert.True(thread.Join(TimeSpan.FromSeconds(10))); }

        Assert.Null(failure);
        Assert.True(subtree.IsDisposed);
    }

    [Fact]
    public void Invalidation_ThrowingObserver_StillNotifiesSessionAndBubblesToRoot()
    {
        var root = new Panel();
        var child = new SizedElement();
        root.Add(child);
        root.Invalidated += _ => throw new TestException();
        using var ui = Create(root);
        ui.Update();
        var laterObserver = 0;
        root.Invalidated += _ => laterObserver++;

        Assert.Throws<TestException>(() => child.Width = 90);

        Assert.Equal(1, laterObserver);
        Assert.True(ui.NeedsUpdate);
        Assert.True(ui.Update());
        Assert.Equal(90, child.Bounds.Width);
        ui.Dispose();
        Assert.True(child.IsDisposed);
    }

    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public int Disposals { get; private set; }
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() => Disposals++;
    }

    private sealed class TestException : Exception;
    private sealed class ThrowingDisposable : IDisposable
    {
        public int Count { get; private set; }
        public void Dispose() { Count++; throw new TestException(); }
    }

    private sealed class Publisher
    {
        private Action? _changed;
        public int Subscribers { get; private set; }
        public int Unsubscribes { get; private set; }
        public IDisposable Subscribe(Action changed)
        {
            _changed += changed;
            Subscribers++;
            return new Subscription(() => { _changed -= changed; Subscribers--; Unsubscribes++; });
        }
        public void Publish() => _changed?.Invoke();
        private sealed class Subscription(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;
            public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
