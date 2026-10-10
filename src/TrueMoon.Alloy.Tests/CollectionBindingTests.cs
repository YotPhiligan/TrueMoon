using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class CollectionBindingTests
{
    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void InitialAttach_DefersFactoryUntilUpdateAndLayoutsEveryRow(int count)
    {
        var source = new Source(Enumerable.Range(0, count).Select(x => new Model(x)));
        var calls = 0;
        var list = VStack.Create();
        Assert.Same(list, list.BindItems(source, _ => { calls++; return new SizedElement(30, 20); }));
        Assert.Equal(0, source.Subscribers);
        using var ui = Create(list);
        Assert.Empty(list.Items);
        Assert.Equal(0, calls);
        Assert.Equal(1, source.Subscribers);
        ui.Update();
        Assert.Equal(count, calls);
        Assert.Equal(count, list.Items.Count);
        Assert.All(list.Items, row => { Assert.Same(list, row.Parent); Assert.True(row.IsAttached); Assert.True(row.Bounds.Height > 0); });
        ui.Dispose();
        Assert.Equal(0, source.Subscribers);
        Assert.Equal(source.Adds, source.Removes);
    }

    [Fact]
    public void ReadOnlySource_AndRegistrationOnAttachedContainer_SynchronizeNextUpdate()
    {
        var source = new ObservableCollection<Model> { new(1) };
        var readOnly = new ReadOnlyObservableCollection<Model>(source);
        var list = new Panel();
        using var ui = Create(list);
        ui.Update();
        Assert.Same(list, list.BindItems(readOnly, item => new Text(item.Name)));
        Assert.Empty(list.Items);
        ui.Update();
        var kept = Assert.Single(list.Items);
        source.Add(new Model(2));
        Assert.Single(list.Items);
        ui.Update();
        Assert.Equal(2, list.Items.Count);
        Assert.Same(kept, list.Items[0]);
        source.Clear();
        ui.Update();
        Assert.Empty(list.Items);
        Assert.True(kept.IsDisposed);
    }

    [Fact]
    public void AddRemoveReplaceMoveReset_PreserveOnlySurvivingReferences()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3); var d = new Model(4);
        var source = new Source([a, b]);
        var rows = new Dictionary<Model, Element>(ReferenceEqualityComparer.Instance);
        var list = new Panel().BindItems(source, item => rows[item] = new Text(item.Name));
        using var ui = Create(list);
        ui.Update();
        source.Insert(1, c); ui.Update();
        AssertOrder(list, rows[a], rows[c], rows[b]);
        source.Move(2, 0); ui.Update();
        AssertOrder(list, rows[b], rows[a], rows[c]);
        source[1] = a; ui.Update();
        AssertOrder(list, rows[b], rows[a], rows[c]);
        source[1] = d; ui.Update();
        AssertOrder(list, rows[b], rows[d], rows[c]);
        Assert.True(rows[a].IsDisposed);
        source.Remove(c); ui.Update();
        AssertOrder(list, rows[b], rows[d]);
        Assert.True(rows[c].IsDisposed);
        source.ResetTo([d, b, c]); ui.Update();
        AssertOrder(list, rows[d], rows[b], rows[c]);
        Assert.False(rows[b].IsDisposed);
        source.Clear(); ui.Update();
        Assert.Empty(list.Items);
        Assert.All(rows.Values, row => Assert.True(row.IsDisposed));
    }

    [Fact]
    public void RangeNotifications_ReconcileFinalOrderForAllActions()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3); var d = new Model(4); var e = new Model(5);
        var source = new Source([a]);
        var rows = new Dictionary<Model, Element>(ReferenceEqualityComparer.Instance);
        var list = new Panel().BindItems(source, item => rows[item] = new Text(item.Name));
        using var ui = Create(list); ui.Update();
        source.Change([a, b, c], new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new[] { b, c }, 1)); ui.Update();
        AssertOrder(list, rows[a], rows[b], rows[c]);
        source.Change([a, d, e], new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, new[] { d, e }, new[] { b, c }, 1)); ui.Update();
        AssertOrder(list, rows[a], rows[d], rows[e]);
        Assert.True(rows[b].IsDisposed); Assert.True(rows[c].IsDisposed);
        source.Change([d, e, a], new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, new[] { d, e }, 0, 1)); ui.Update();
        AssertOrder(list, rows[d], rows[e], rows[a]);
        source.Change([a], new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new[] { d, e }, 0)); ui.Update();
        Assert.Same(rows[a], Assert.Single(list.Items));
        Assert.True(rows[d].IsDisposed); Assert.True(rows[e].IsDisposed);
    }

    [Fact]
    public void Identity_UsesReferencesEvenWhenEqualsAndIdsMatch()
    {
        var a = new Model(1); var equal = new Model(1); var replacement = new Model(1);
        var source = new Source([a, equal]);
        var calls = 0;
        var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        using var ui = Create(list); ui.Update();
        Assert.Equal(2, calls);
        var first = list.Items[0]; var second = list.Items[1];
        Assert.NotSame(first, second);
        source[0] = replacement; ui.Update();
        Assert.Equal(3, calls); Assert.True(first.IsDisposed); Assert.Same(second, list.Items[1]); Assert.NotSame(first, list.Items[0]);
        source.ResetTo([equal, replacement]); ui.Update();
        Assert.Same(second, list.Items[0]); Assert.Equal(3, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOrNullItems_RejectSnapshotBeforeFactoryAndPreserveTree(bool nullItem)
    {
        var a = new Model(1); var source = new Source([a]); var calls = 0;
        var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        using var ui = Create(list); ui.Update();
        var kept = Assert.Single(list.Items);
        source.ResetTo([new Model(2), a, nullItem ? null! : a]);
        Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Same(kept, Assert.Single(list.Items)); Assert.True(kept.IsAttached); Assert.False(kept.IsDisposed); Assert.Equal(1, calls);
        source.ResetTo([a]); ui.Update(); Assert.Same(kept, Assert.Single(list.Items));
    }

    [Fact]
    public void CoalescedEvents_SkipTransientItemsAndRetainRemovedThenReaddedRow()
    {
        var a = new Model(1); var transient = new Model(2); var source = new Source([a]); var calls = 0;
        var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        using var ui = Create(list); ui.Update(); var kept = Assert.Single(list.Items);
        source.Add(transient); source.Remove(transient); source.Remove(a); source.Add(a);
        ui.Update();
        Assert.Equal(1, calls); Assert.Same(kept, Assert.Single(list.Items)); Assert.False(kept.IsDisposed);
    }

    [Fact]
    public void MoveAndReset_KeepFocusSelectionCaptureScrollAndPropertySubscriptions()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3);
        var source = new Source([a, b, c]);
        var list = new VStack().BindItems(source, item => new TextBox().Height(80).BindTwoWay(TextBox.ValueProperty, item, x => x.Name));
        var scroll = new ScrollViewer().Content(list);
        using var ui = Create(scroll); ui.Update();
        var selected = (TextBox)list.Items[1]; var attachmentChanges = 0;
        selected.AttachmentChanged += () => attachmentChanges++;
        ui.Focus(selected); ui.Capture(selected);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        scroll.Offset = 40; ui.Update();
        Assert.Equal(b.Name.Length, selected.SelectionLength);
        source.Move(1, 0); ui.Update();
        Assert.Same(selected, list.Items[0]);
        source.ResetTo([c, b, a]); ui.Update();
        Assert.Same(selected, list.Items[1]); Assert.True(selected.IsFocused);
        Assert.Equal(b.Name.Length, selected.SelectionLength); Assert.Equal(b.Name.Length, selected.CaretIndex);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
        Assert.Equal(40, scroll.Offset); Assert.Equal(0, attachmentChanges); Assert.Equal(1, b.Subscribers); Assert.Equal(1, b.Adds); Assert.Equal(0, b.Removes);
        b.Name = "changed"; ui.Update(); Assert.Equal("changed", selected.Value);
        source.ResetTo([b]); ui.Update(); Assert.Equal(0, scroll.Offset); Assert.Same(selected, Assert.Single(list.Items));
    }

    [Fact]
    public void Removal_DisposesGeneratedSubtreeOnceAndDisconnectsModel()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a, b]);
        var resources = new List<DisposalCounter>(); var children = new List<Text>();
        var list = new Panel().BindItems(source, item =>
        {
            var resource = new DisposalCounter(); resources.Add(resource);
            var child = new Text().BindText(item, x => x.Name); children.Add(child);
            return new Panel().WithChildren(child).Configure(row => row.Own(resource));
        });
        using var ui = Create(list); ui.Update(); var removed = list.Items[0]; var retained = list.Items[1];
        Assert.Throws<InvalidOperationException>(removed.Dispose);
        source.Remove(a); ui.Update();
        Assert.True(removed.IsDisposed); Assert.True(children[0].IsDisposed); Assert.Null(removed.Parent); Assert.Equal(0, a.Subscribers); Assert.Equal(1, resources[0].Count);
        Assert.False(retained.IsDisposed); Assert.Equal(1, b.Subscribers);
        removed.Dispose(); ui.Dispose(); ui.Dispose();
        Assert.True(retained.IsDisposed); Assert.All(resources, resource => Assert.Equal(1, resource.Count)); Assert.Equal(0, b.Subscribers);
        Assert.Same(b, Assert.Single(source)); // Source/model ownership remains with the caller.
    }

    [Theory]
    [InlineData("null")]
    [InlineData("disposed")]
    [InlineData("foreign")]
    [InlineData("attached")]
    [InlineData("same-draft")]
    [InlineData("retained")]
    [InlineData("ancestor")]
    [InlineData("throw")]
    public void FactoryFailure_PreservesOldTreeCleansDraftsAndNeverDisposesForeignNodes(string kind)
    {
        var a = new Model(1); var source = new Source([a]);
        var draftResource = new DisposalCounter(); var draft = new Text().Configure(x => x.Own(draftResource));
        var foreign = new Text(); using var owner = new Panel().WithChildren(foreign);
        var attached = new Text(); using var otherUi = Create(attached);
        var disposed = new Text(); disposed.Dispose();
        var failing = false; Element? kept = null; var list = new Panel();
        list.BindItems(source, item =>
        {
            if (!failing) return new Text();
            if (item.Id == 2) return draft;
            return kind switch
            {
                "null" => null!, "disposed" => disposed, "foreign" => foreign, "attached" => attached,
                "same-draft" => draft, "retained" => kept!, "ancestor" => list, _ => throw new TestException()
            };
        });
        using var ui = Create(list); ui.Update(); kept = Assert.Single(list.Items);
        failing = true; source.Add(new Model(2)); source.Add(new Model(3));
        if (kind == "throw") Assert.Throws<TestException>(() => ui.Update());
        else Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Same(kept, Assert.Single(list.Items)); Assert.True(kept.IsAttached); Assert.False(kept.IsDisposed);
        Assert.True(draft.IsDisposed); Assert.Equal(1, draftResource.Count); Assert.False(foreign.IsDisposed); Assert.Same(owner, foreign.Parent);
        Assert.False(attached.IsDisposed); Assert.True(attached.IsAttached); Assert.False(list.IsDisposed);
        failing = false; list.RefreshItems(); ui.Update(); Assert.Equal(3, list.Items.Count); Assert.Same(kept, list.Items[0]);
    }

    [Fact]
    public void FactoryThrowsBeforeReturning_FactoryOwnsItsUnreturnedResources()
    {
        var source = new Source([new Model(1)]); var resource = new DisposalCounter();
        var list = new Panel().BindItems(source, _ =>
        {
            using var unreturned = new Text().Configure(row => row.Own(resource));
            throw new TestException();
        });
        using var ui = Create(list);
        Assert.Throws<TestException>(() => ui.Update());
        Assert.Empty(list.Items); Assert.Equal(1, resource.Count);
    }

    [Fact]
    public void FailedPreparation_CleanupErrorsStillDisposeAllDrafts()
    {
        var resources = new List<ThrowingDisposable>(); var drafts = new List<Text>();
        var source = new Source([new Model(1), new Model(2), new Model(3)]);
        var list = new Panel().BindItems(source, item =>
        {
            if (item.Id == 3) throw new TestException();
            var resource = new ThrowingDisposable(); resources.Add(resource);
            var draft = new Text().Configure(x => x.Own(resource)); drafts.Add(draft); return draft;
        });
        using var ui = Create(list);
        var error = Assert.Throws<AggregateException>(() => ui.Update());
        Assert.Equal(3, error.InnerExceptions.Count); Assert.Empty(list.Items);
        Assert.All(drafts, draft => Assert.True(draft.IsDisposed)); Assert.All(resources, resource => Assert.Equal(1, resource.Count));
    }

    [Fact]
    public void SnapshotFailure_PreservesTreeAndRefreshRecoversWithoutNewFactory()
    {
        var source = new Source([new Model(1)]); var calls = 0;
        var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        using var ui = Create(list); ui.Update(); var kept = Assert.Single(list.Items);
        source.ThrowOnSnapshot = true; list.RefreshItems();
        Assert.Throws<TestException>(() => ui.Update());
        Assert.Same(kept, Assert.Single(list.Items)); Assert.Equal(1, calls); Assert.Equal(1, source.Subscribers);
        source.ThrowOnSnapshot = false; list.RefreshItems(); ui.Update();
        Assert.Same(kept, Assert.Single(list.Items)); Assert.Equal(1, calls);
    }

    [Fact]
    public void CommitObservers_SeeFinalOrderParentsAndAttachmentsDespiteEarlierFailure()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3); var d = new Model(4);
        var source = new Source([a, b]); var rows = new Dictionary<Model, Element>(ReferenceEqualityComparer.Instance); var calls = 0;
        var list = new Panel().BindItems(source, item => { calls++; return rows[item] = new Text(); });
        using var ui = Create(list); ui.Update(); var later = 0;
        rows[a].AttachmentChanged += () => throw new TestException();
        rows[a].AttachmentChanged += () =>
        {
            AssertOrder(list, rows[d], rows[b], rows[c]);
            Assert.Null(rows[a].Parent); Assert.False(rows[a].IsAttached);
            Assert.All(list.Items, row => { Assert.Same(list, row.Parent); Assert.True(row.IsAttached); }); later++;
        };
        source.ResetTo([d, b, c]);
        Assert.Throws<TestException>(() => ui.Update());
        Assert.Equal(1, later); Assert.True(rows[a].IsDisposed); Assert.Equal(4, calls);
        list.RefreshItems(); ui.Update();
        AssertOrder(list, rows[d], rows[b], rows[c]); Assert.Equal(4, calls); Assert.Equal(1, later);
    }

    [Fact]
    public void RemovalCleanupErrors_CommitMappingAndReleaseAllRowsWithoutRetryingFactory()
    {
        var source = new Source([new Model(1), new Model(2)]); var resources = new List<ThrowingDisposable>(); var calls = 0;
        var list = new Panel().BindItems(source, _ =>
        {
            calls++; var resource = new ThrowingDisposable(); resources.Add(resource); return new Text().Configure(row => row.Own(resource));
        });
        using var ui = Create(list); ui.Update(); var old = list.Items.ToArray(); source.Clear();
        var error = Assert.Throws<AggregateException>(() => ui.Update());
        Assert.Equal(2, error.InnerExceptions.Count); Assert.Empty(list.Items); Assert.All(old, row => Assert.True(row.IsDisposed));
        Assert.All(resources, resource => Assert.Equal(1, resource.Count)); list.RefreshItems(); ui.Update(); Assert.Equal(2, calls);
    }

    [Fact]
    public void FactoryChangesSource_AbortPreparedSnapshotAndNextUpdateUsesLatestItems()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a]); var mutate = true; var drafts = new List<Text>();
        var list = new Panel().BindItems(source, _ =>
        {
            var row = new Text(); drafts.Add(row); if (mutate) { mutate = false; source.Add(b); } return row;
        });
        using var ui = Create(list);
        Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Empty(list.Items); Assert.True(drafts[0].IsDisposed);
        ui.Update(); Assert.Equal(2, list.Items.Count); Assert.Equal(3, drafts.Count); Assert.All(list.Items, row => Assert.False(row.IsDisposed));
    }

    [Fact]
    public void ObserverChangesSource_QueuesAnotherCoherentReconciliation()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a]); var once = true; var calls = 0;
        var list = new Panel().BindItems(source, item =>
        {
            calls++; var row = new Text(); row.AttachmentChanged += () => { if (row.IsAttached && once) { once = false; source.Add(b); } }; return row;
        });
        using var ui = Create(list); ui.Update(); if (list.Items.Count == 1) ui.Update();
        Assert.Equal(2, list.Items.Count); Assert.Equal(2, calls); Assert.All(list.Items, row => { Assert.True(row.IsAttached); Assert.Same(list, row.Parent); });
    }

    [Fact]
    public void DetachReattach_RetainsRowsReconnectsAndDropsOldQueuedAndInflightEvents()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a]); var calls = 0;
        var list = new Panel().BindItems(source, _ => { calls++; return new Text(); }); var root = new Panel().WithChildren(list);
        using var ui = Create(root); ui.Update(); var kept = Assert.Single(list.Items);
        source.Add(b); var inFlight = source.CaptureNotification(); root.Items.Remove(list);
        Assert.Equal(0, source.Subscribers); Assert.False(kept.IsAttached); Assert.False(kept.IsDisposed);
        source.Remove(b); ui.Update(); Assert.Equal(1, calls);
        root.Items.Add(list); ui.Update(); Assert.Same(kept, Assert.Single(list.Items)); Assert.Equal(1, source.Subscribers);
        var reads = source.Snapshots; RunOnWorker(inFlight); ui.Update(); Assert.Equal(reads, source.Snapshots); Assert.Equal(1, calls);
        source.Add(b); ui.Update(); Assert.Equal(2, calls); Assert.Equal(2, source.Adds); Assert.Equal(1, source.Removes);
    }

    [Fact]
    public void TransferToWorker_EnumeratesAndCreatesOnlyOnNewOwnerAndIgnoresOldWork()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a]); var threads = new List<int>();
        var list = new Panel().BindItems(source, _ => { threads.Add(Environment.CurrentManagedThreadId); return new Text(); });
        var root = new Panel().WithChildren(list); using var oldUi = Create(root); oldUi.Update(); var kept = Assert.Single(list.Items);
        source.Add(b); var stale = source.CaptureNotification(); root.Items.Remove(list);
        RunOnWorker(() =>
        {
            using var newUi = Create(list); Assert.Single(list.Items); newUi.Update(); Assert.Equal(2, list.Items.Count); Assert.Same(kept, list.Items[0]);
            Assert.Equal(Environment.CurrentManagedThreadId, source.LastSnapshotThread); Assert.Equal(Environment.CurrentManagedThreadId, threads[1]);
            var snapshots = source.Snapshots; stale(); newUi.Update(); Assert.Equal(snapshots, source.Snapshots);
            source.Remove(b); newUi.Update(); Assert.Same(kept, Assert.Single(list.Items));
        });
        var reads = source.Snapshots; oldUi.Update(); Assert.Equal(reads, source.Snapshots); Assert.True(list.IsDisposed); Assert.Equal(0, source.Subscribers);
    }

    [Fact]
    public void BackgroundEvents_QueueEnumerationAndFactoryOnUiOwner()
    {
        var source = new Source([]); var factoryThread = 0; var list = new Panel().BindItems(source, _ => { factoryThread = Environment.CurrentManagedThreadId; return new Text(); });
        using var ui = Create(list); ui.Update(); var snapshots = source.Snapshots;
        RunOnWorker(() => source.Add(new Model(1)));
        Assert.Empty(list.Items); Assert.Equal(snapshots, source.Snapshots); Assert.Equal(0, factoryThread);
        ui.Update(); Assert.Single(list.Items); Assert.Equal(Environment.CurrentManagedThreadId, factoryThread); Assert.Equal(Environment.CurrentManagedThreadId, source.LastSnapshotThread);
    }

    [Fact]
    public void BoundItems_RejectEveryManualMutationAndDuplicateRegistrationWithoutChangingRows()
    {
        var source = new Source([new Model(1)]); var list = new Panel().BindItems(source, _ => new Text());
        using var ui = Create(list); ui.Update(); var kept = Assert.Single(list.Items); using var candidate = new Text();
        Assert.Throws<InvalidOperationException>(() => list.Items.Add(candidate));
        Assert.Throws<InvalidOperationException>(() => list.Items.Insert(0, candidate));
        Assert.Throws<InvalidOperationException>(() => list.Items[0] = kept);
        Assert.Throws<InvalidOperationException>(() => list.Items.Remove(kept));
        Assert.Throws<InvalidOperationException>(() => list.Items.Clear());
        Assert.Throws<InvalidOperationException>(() => list.Items.Move(0, 0));
        Assert.Throws<InvalidOperationException>(() => list.BindItems(source, _ => new Text()));
        Assert.Same(kept, Assert.Single(list.Items)); Assert.True(kept.IsAttached); Assert.Null(candidate.Parent); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void RegistrationValidation_RejectsNullNonemptyDisposedUnboundRefreshAndForeignThread()
    {
        var source = new Source([]); using var list = new Panel();
        Assert.Throws<ArgumentNullException>(() => ((Panel)null!).BindItems(source, _ => new Text()));
        Assert.Throws<ArgumentNullException>(() => list.BindItems((ObservableCollection<Model>)null!, _ => new Text()));
        Assert.Throws<ArgumentNullException>(() => list.BindItems((ReadOnlyObservableCollection<Model>)null!, _ => new Text()));
        Assert.Throws<ArgumentNullException>(() => list.BindItems(source, null!));
        Assert.Throws<ArgumentNullException>(() => ((Panel)null!).RefreshItems());
        Assert.Throws<InvalidOperationException>(() => list.RefreshItems());
        using var nonempty = new Panel().WithChildren(new Text()); Assert.Throws<InvalidOperationException>(() => nonempty.BindItems(source, _ => new Text()));
        var disposed = new Panel(); disposed.Dispose(); Assert.Throws<ObjectDisposedException>(() => disposed.BindItems(source, _ => new Text()));
        using var ui = Create(list);
        RunOnWorker(() => Assert.Throws<InvalidOperationException>(() => list.BindItems(source, _ => new Text())));
        Assert.Equal(0, source.Subscribers); list.BindItems(source, _ => new Text()); ui.Update();
        RunOnWorker(() => Assert.Throws<InvalidOperationException>(() => list.RefreshItems())); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void SubscribeFailure_ReleasesReservationAndAllowsRetry()
    {
        var source = new Source([new Model(1)]) { ThrowOnSubscribe = true }; var list = new Panel(); using var ui = Create(list);
        Assert.Throws<TestException>(() => list.BindItems(source, _ => new Text()));
        Assert.Equal(0, source.Subscribers); Assert.Empty(list.Items);
        source.ThrowOnSubscribe = false; list.BindItems(source, _ => new Text()); ui.Update(); Assert.Single(list.Items); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void RemoveLastItem_LeavesEmptyTreeAndReleasesFocusCaptureAndSubscription()
    {
        var a = new Model(1); var source = new Source([a]);
        var list = new Panel().BindItems(source, item => new TextBox().BindTwoWay(TextBox.ValueProperty, item, x => x.Name));
        using var ui = Create(list); ui.Update(); var row = Assert.Single(list.Items); ui.Focus(row); ui.Capture(row);
        source.RemoveAt(0); ui.Update();
        Assert.Empty(list.Items); Assert.True(row.IsDisposed); Assert.False(row.IsAttached); Assert.False(row.IsFocused); Assert.Null(row.Parent);
        Assert.Equal(0, a.Subscribers);
        var input = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None));
        Assert.False(input.KeyboardFocused); Assert.False(input.PointerCaptured);
    }

    [Fact]
    public void ResetMixedItems_RemovesMissingRetainsSurvivorAndCreatesOnlyNewRows()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3); var source = new Source([a, b]); var calls = 0;
        var list = new Panel().BindItems(source, item => { calls++; return new Text().BindText(item, x => x.Name); });
        using var ui = Create(list); ui.Update(); var removed = list.Items[0]; var survivor = list.Items[1];
        source.ResetTo([c, b]); ui.Update();
        Assert.Equal(3, calls); Assert.Equal(2, list.Items.Count); Assert.Same(survivor, list.Items[1]); Assert.NotSame(removed, list.Items[0]);
        Assert.True(removed.IsDisposed); Assert.Equal(0, a.Subscribers); Assert.Equal(1, b.Subscribers); Assert.Equal(1, c.Subscribers);
        Assert.Equal(1, b.Adds); Assert.Equal(0, b.Removes);
    }

    [Fact]
    public void SourceMutationDuringPreparation_PreservesExistingFocusAndOwnershipThenRecovers()
    {
        var a = new Model(1); var b = new Model(2); var c = new Model(3); var source = new Source([a]); var mutate = true; Text? draft = null;
        var list = new Panel().BindItems(source, item =>
        {
            if (item.Id == 1) return new TextBox().BindTwoWay(TextBox.ValueProperty, item, x => x.Name);
            if (mutate) { mutate = false; source.Add(c); return draft = new Text(); }
            return new Text();
        });
        using var ui = Create(list); ui.Update(); var kept = (TextBox)Assert.Single(list.Items);
        ui.Focus(kept); ui.Capture(kept); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        source.Add(b); Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Same(kept, Assert.Single(list.Items)); Assert.Same(list, kept.Parent); Assert.True(kept.IsAttached); Assert.True(kept.IsFocused);
        Assert.Equal(a.Name.Length, kept.SelectionLength); Assert.Equal(1, a.Subscribers); Assert.True(draft!.IsDisposed);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
        ui.Update(); Assert.Equal(3, list.Items.Count); Assert.Same(kept, list.Items[0]); Assert.True(kept.IsFocused);
    }

    [Fact]
    public void NewRowAttachmentFailure_IsPostcommitAndRefreshDoesNotRecreateOrDisposeIt()
    {
        var source = new Source([]); var calls = 0; var later = 0; var resource = new DisposalCounter();
        var list = new Panel().BindItems(source, _ =>
        {
            calls++; var row = new Text().Configure(x => x.Own(resource));
            row.AttachmentChanged += () => { if (row.IsAttached) throw new TestException(); };
            row.AttachmentChanged += () => { if (row.IsAttached) later++; }; return row;
        });
        using var ui = Create(list); ui.Update(); source.Add(new Model(1));
        Assert.Throws<TestException>(() => ui.Update());
        var committed = Assert.Single(list.Items); Assert.True(committed.IsAttached); Assert.Same(list, committed.Parent);
        Assert.False(committed.IsDisposed); Assert.Equal(0, resource.Count); Assert.Equal(1, later);
        list.RefreshItems(); ui.Update(); Assert.Same(committed, Assert.Single(list.Items)); Assert.Equal(1, calls); Assert.Equal(1, later);
        ui.Dispose(); Assert.True(committed.IsDisposed); Assert.Equal(1, resource.Count);
    }

    [Fact]
    public void DetachBeforeUnsubscribe_BackgroundInflightEventCannotEnumerateOrQueueStaleWork()
    {
        var a = new Model(1); var b = new Model(2); var source = new Source([a]); var calls = 0;
        using var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        var queue = new Queue<Action>(); var ownerThread = Environment.CurrentManagedThreadId;
        list.Attach(() => Assert.Equal(ownerThread, Environment.CurrentManagedThreadId), queue.Enqueue, detaching =>
        {
            if (!ReferenceEquals(detaching, list)) return;
            Assert.Equal(1, source.Subscribers); var reads = source.Snapshots;
            RunOnWorker(() => source.Add(b)); Assert.Equal(reads, source.Snapshots);
        });
        Assert.Single(queue)(); queue.Clear(); var kept = Assert.Single(list.Items); var stale = source.CaptureNotification();
        list.Detach(); Assert.Empty(queue); Assert.Equal(0, source.Subscribers); Assert.False(kept.IsDisposed);
        RunOnWorker(stale); Assert.Empty(queue);
        list.RefreshItems(); Assert.Empty(queue);
        using var ui = Create(list); ui.Update(); Assert.Equal(2, calls); Assert.Same(kept, list.Items[0]);
        var snapshots = source.Snapshots; RunOnWorker(stale); ui.Update(); Assert.Equal(snapshots, source.Snapshots); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void DispatchFailure_RefreshCanQueueAgainWithoutLosingBinding()
    {
        var source = new Source([new Model(1)]); using var list = new Panel().BindItems(source, _ => new Text());
        var queue = new Queue<Action>(); var fail = false;
        list.Attach(() => { }, action => { if (fail) throw new TestException(); queue.Enqueue(action); });
        queue.Dequeue()(); fail = true;
        Assert.Throws<TestException>(() => list.RefreshItems());
        fail = false; list.RefreshItems(); Assert.Single(queue)(); queue.Clear();
        Assert.Single(list.Items); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void InitialSubscribeFailure_SessionAttachRollsBackAndReattachesOnceOnRetry()
    {
        var source = new Source([new Model(1)]) { ThrowOnSubscribe = true }; var calls = 0;
        using var list = new Panel().BindItems(source, _ => { calls++; return new Text(); });
        Assert.Throws<TestException>(() => Create(list));
        Assert.False(list.IsAttached); Assert.False(list.IsDisposed); Assert.Empty(list.Items); Assert.Equal(0, source.Subscribers); Assert.Equal(0, calls);
        source.ThrowOnSubscribe = false; using var ui = Create(list); Assert.Empty(list.Items); ui.Update();
        Assert.Single(list.Items); Assert.Equal(1, calls); Assert.Equal(1, source.Subscribers); Assert.Equal(2, source.Adds); Assert.Equal(1, source.Removes);
        ui.Dispose(); Assert.Equal(0, source.Subscribers); Assert.Equal(source.Adds, source.Removes);
    }

    [Fact]
    public void EarlierSourceSubscriberThrows_RefreshRecoversCommittedSourceChange()
    {
        var a = new Model(1); var source = new ObservableCollection<Model> { a }; var fail = true;
        source.CollectionChanged += (_, _) => { if (fail) throw new TestException(); };
        var list = new Panel().BindItems(source, item => new Text(item.Name));
        using var ui = Create(list); ui.Update(); var kept = Assert.Single(list.Items); var b = new Model(2);
        Assert.Throws<TestException>(() => source.Add(b)); Assert.Equal(2, source.Count);
        Assert.False(ui.Update()); Assert.Same(kept, Assert.Single(list.Items));
        list.RefreshItems(); ui.Update(); Assert.Equal(2, list.Items.Count); Assert.Same(kept, list.Items[0]); Assert.Equal(b.Name, ((Text)list.Items[1]).Value);
        fail = false; source.Remove(b); ui.Update(); Assert.Same(kept, Assert.Single(list.Items));
    }

    private static void AssertOrder(ElementList list, params Element[] expected)
    {
        Assert.Equal(expected.Length, list.Items.Count);
        for (var index = 0; index < expected.Length; index++) Assert.Same(expected[index], list.Items[index]);
    }

    private static void RunOnWorker(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Worker did not finish."); Assert.Null(error);
    }

    private sealed class TestException : Exception;
    private sealed class ThrowingDisposable : IDisposable
    {
        public int Count { get; private set; }
        public void Dispose() { Count++; throw new TestException(); }
    }

    private sealed class Model(int id) : INotifyPropertyChanged
    {
        private string _name = $"row {id}";
        private PropertyChangedEventHandler? _changed;
        public int Id { get; } = id;
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; Adds++; }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
        public override bool Equals(object? obj) => obj is Model other && other.Id == Id;
        public override int GetHashCode() => Id;
    }

    private sealed class Source(IEnumerable<Model> items) : ObservableCollection<Model>(items), INotifyCollectionChanged, ICollection<Model>
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public int Snapshots { get; private set; }
        public int LastSnapshotThread { get; private set; }
        public bool ThrowOnSnapshot { get; set; }
        public bool ThrowOnSubscribe { get; set; }
        event NotifyCollectionChangedEventHandler? INotifyCollectionChanged.CollectionChanged
        {
            add { _changed += value; Subscribers++; Adds++; if (ThrowOnSubscribe) throw new TestException(); }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) { base.OnCollectionChanged(e); _changed?.Invoke(this, e); }
        public void ResetTo(IEnumerable<Model> values) => Change(values, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        public void Change(IEnumerable<Model> values, NotifyCollectionChangedEventArgs args)
        {
            var snapshot = values.ToArray(); Items.Clear(); foreach (var item in snapshot) Items.Add(item); OnCollectionChanged(args);
        }
        public Action CaptureNotification()
        {
            var captured = _changed; return () => captured?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        void ICollection<Model>.CopyTo(Model[] array, int index)
        {
            Snapshots++; LastSnapshotThread = Environment.CurrentManagedThreadId;
            if (ThrowOnSnapshot) throw new TestException();
            for (var i = 0; i < Count; i++) array[index + i] = this[i];
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
