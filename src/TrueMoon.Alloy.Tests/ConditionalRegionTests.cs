using System.ComponentModel;
using AlloyTest;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ConditionalRegionTests
{
    private static UiSession Create(Element root) => new(root, new HeadlessSurface(), new UiViewport(240, 140));
    private static LoadStatus Loading(string message = "loading") => new(LoadPhase.Loading, message);
    private static LoadStatus Error(string message = "error") => new(LoadPhase.Error, message);
    private static LoadStatus Ready(string message = "ready") => new(LoadPhase.Ready, message);

    [Theory]
    [InlineData(LoadPhase.Loading)]
    [InlineData(LoadPhase.Error)]
    public void InitialNondefaultBinding_ReplacesDefaultBranchDuringUpdateBeforeLayout(LoadPhase phase)
    {
        var source = new Source { Status = new LoadStatus(phase, "initial message") };
        var calls = new List<LoadStatus>();
        var region = new LoadStatusRegion(status => { calls.Add(status); return new Text(status.Message); })
            .Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        var initial = Assert.IsType<Text>(region.Child);
        Assert.Equal(LoadStatus.Ready, region.DisplayedStatus);
        Assert.Equal(LoadStatus.Ready.Message, initial.Value);
        Assert.Equal(0, source.Reads); Assert.Equal(0, source.Subscribers);
        using var ui = Create(region);
        Assert.Same(initial, region.Child); Assert.Equal(source.Snapshot, region.Status); Assert.Equal(1, source.Subscribers); Assert.Single(calls);
        ui.Update();
        var current = Assert.IsType<Text>(region.Child);
        Assert.NotSame(initial, current); Assert.True(initial.IsDisposed);
        Assert.Equal("initial message", current.Value); Assert.Equal(source.Snapshot, region.DisplayedStatus);
        Assert.Same(region, current.Parent); Assert.True(current.IsAttached); Assert.True(current.Bounds.Height > 0);
        Assert.Equal(new[] { LoadStatus.Ready, source.Snapshot }, calls);
        ui.Dispose(); Assert.Equal(0, source.Subscribers); Assert.Equal(source.Adds, source.Removes);
    }

    [Fact]
    public void BoundTransitions_LoadingErrorReadyAndMessageReplaceOnlyTheirBranch()
    {
        var source = new Source(); var requested = new List<LoadStatus>();
        var region = new LoadStatusRegion(status =>
        {
            requested.Add(status);
            return status.Phase switch
            {
                LoadPhase.Loading => new VStack().WithChildren(new Text(status.Message), new ProgressBar()),
                LoadPhase.Error => new HStack().WithChildren(new Text(status.Message), new Button("retry")),
                _ => new Text(status.Message)
            };
        }).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = Create(region); ui.Update();
        var initial = region.Child;
        source.Status = Loading(); Assert.Same(initial, region.Child); ui.Update();
        var loading = Assert.IsType<VStack>(region.Child); Assert.Equal("loading", ((Text)loading.Items[0]).Value); Assert.True(initial!.IsDisposed);
        source.Status = Error(); ui.Update();
        var error = Assert.IsType<HStack>(region.Child); Assert.Equal("error", ((Text)error.Items[0]).Value); Assert.True(loading.IsDisposed);
        source.Status = Error("new error detail"); ui.Update();
        var updatedError = Assert.IsType<HStack>(region.Child); Assert.NotSame(error, updatedError); Assert.True(error.IsDisposed);
        Assert.Equal("new error detail", ((Text)updatedError.Items[0]).Value);
        source.Status = Ready(); ui.Update();
        Assert.Equal("ready", Assert.IsType<Text>(region.Child).Value); Assert.True(updatedError.IsDisposed);
        Assert.Equal(new[] { LoadStatus.Ready, Loading(), Error(), Error("new error detail"), Ready() }, requested);
        Assert.Equal(Ready(), region.DisplayedStatus); Assert.Equal(1, source.Subscribers);
    }

    [Fact]
    public void Coalescing_SkipsIntermediateBranchesAndFactoryReadsLatestSnapshot()
    {
        var source = new Source(); var calls = new List<LoadStatus>();
        var region = new LoadStatusRegion(status => { calls.Add(status); return new Text(status.Message); })
            .Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = Create(region); ui.Update(); var kept = region.Child;
        source.Status = Loading(); source.Status = Error(); source.Status = Ready("latest");
        Assert.Same(kept, region.Child); ui.Update();
        Assert.Equal(new[] { LoadStatus.Ready, Ready("latest") }, calls); Assert.Equal("latest", Assert.IsType<Text>(region.Child).Value);
        Assert.True(kept!.IsDisposed); Assert.Equal(Ready("latest"), region.DisplayedStatus);
    }

    [Fact]
    public void FactoryFailure_KeepsOldBranchAndNeighborInputScrollAndSubscriptionsUntilRefreshRecovers()
    {
        var source = new Source(); var fail = false; var calls = 0; var neighborSource = new TextSource();
        var region = new LoadStatusRegion(status => { calls++; if (fail) throw new TestException(); return new Text(status.Message); })
            .Height(40).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        var editor = new TextBox().Height(80).BindTwoWay(TextBox.ValueProperty, neighborSource, x => x.Value);
        var content = new VStack().WithChildren(editor, new SizedElement(80, 200));
        var scroll = new ScrollViewer().Height(100).Content(content);
        var root = new VStack().WithChildren(region, scroll);
        using var ui = Create(root); ui.Update(); var kept = region.Child;
        ui.Focus(editor); ui.Capture(editor); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true)); scroll.Offset = 40; ui.Update();
        fail = true; source.Status = Loading(); Assert.Throws<TestException>(() => ui.Update());
        Assert.Same(kept, region.Child); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus); Assert.Equal(Loading(), region.Status);
        Assert.False(kept!.IsDisposed); Assert.True(kept.IsAttached); Assert.Same(region, kept.Parent);
        Assert.True(editor.IsFocused); Assert.Equal(neighborSource.Value.Length, editor.SelectionLength);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured); Assert.Equal(40, scroll.Offset);
        Assert.Equal(1, neighborSource.Subscribers); Assert.Equal(1, neighborSource.Adds); Assert.Equal(0, neighborSource.Removes);
        fail = false; region.RefreshContent(); ui.Update();
        Assert.Equal(Loading(), region.DisplayedStatus); Assert.Equal("loading", Assert.IsType<Text>(region.Child).Value); Assert.True(kept.IsDisposed);
        Assert.Equal(3, calls); Assert.True(editor.IsFocused); Assert.Equal(neighborSource.Value.Length, editor.SelectionLength); Assert.Equal(40, scroll.Offset);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("disposed")]
    [InlineData("foreign")]
    [InlineData("attached")]
    [InlineData("self")]
    [InlineData("ancestor")]
    public void InvalidFactoryResult_PreservesBranchAndNeverDisposesForeignOrAncestor(string kind)
    {
        var foreign = new Text(); using var owner = new Panel().WithChildren(foreign);
        var attached = new Text(); using var otherUi = Create(attached);
        var disposed = new Text(); disposed.Dispose();
        var reject = false; LoadStatusRegion? region = null; var root = new Panel();
        region = new LoadStatusRegion(status => !reject ? new Text(status.Message) : kind switch
        {
            "null" => null!, "disposed" => disposed, "foreign" => foreign, "attached" => attached,
            "self" => region!, _ => root
        });
        root.Items.Add(region); using var ui = Create(root); ui.Update(); var kept = region.Child;
        reject = true; region.Status = Error();
        if (kind == "disposed") Assert.Throws<ObjectDisposedException>(() => ui.Update());
        else Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Same(kept, region.Child); Assert.True(kept!.IsAttached); Assert.False(kept.IsDisposed); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus);
        Assert.False(foreign.IsDisposed); Assert.Same(owner, foreign.Parent); Assert.False(attached.IsDisposed); Assert.True(attached.IsAttached);
        Assert.False(region.IsDisposed); Assert.False(root.IsDisposed);
        reject = false; region.RefreshContent(); ui.Update(); Assert.Equal(Error(), region.DisplayedStatus); Assert.True(kept.IsDisposed);
    }

    [Fact]
    public void InvalidCandidateSubtree_IsDisposedWithItsResourcesAfterValidationFailure()
    {
        var reject = false; var resource = new DisposalCounter(); var badChild = new Text(); badChild.Dispose(); InvalidSubtree? draft = null;
        var region = new LoadStatusRegion(status => !reject ? new Text(status.Message) : draft = new InvalidSubtree(badChild).Configure(x => x.Own(resource)));
        using var ui = Create(region); ui.Update(); var kept = region.Child;
        reject = true; region.Status = Error(); Assert.Throws<ObjectDisposedException>(() => ui.Update());
        Assert.Same(kept, region.Child); Assert.False(kept!.IsDisposed); Assert.True(draft!.IsDisposed); Assert.Null(draft.Parent);
        Assert.Equal(1, resource.Count); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus); draft.Dispose(); Assert.Equal(1, resource.Count);
    }

    [Fact]
    public void FactoryThrowBeforeReturn_CleansFactoryOwnedAllocationAndPreservesAcceptedBranch()
    {
        var fail = false; var resource = new DisposalCounter();
        var region = new LoadStatusRegion(status =>
        {
            if (!fail) return new Text(status.Message);
            using var allocation = new Panel().WithChildren(new Text()).Configure(x => x.Own(resource));
            throw new TestException();
        });
        using var ui = Create(region); ui.Update(); var kept = region.Child;
        fail = true; region.Status = Loading(); Assert.Throws<TestException>(() => ui.Update());
        Assert.Same(kept, region.Child); Assert.False(kept!.IsDisposed); Assert.Equal(1, resource.Count); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus);
    }

    [Fact]
    public void AcceptedReplacement_DisposesOldSubtreeSubscriptionsAndInputExactlyOnce()
    {
        var textSource = new TextSource(); var resources = new List<DisposalCounter>(); var editors = new List<TextBox>();
        var region = new LoadStatusRegion(_ =>
        {
            var resource = new DisposalCounter(); resources.Add(resource);
            var editor = new TextBox().BindTwoWay(TextBox.ValueProperty, textSource, x => x.Value); editors.Add(editor);
            return new Panel().WithChildren(editor).Configure(x => x.Own(resource));
        });
        using var ui = Create(region); ui.Update(); var old = region.Child!; var editor = editors[0]; var attachmentResource = new DisposalCounter(); editor.OwnAttachment(attachmentResource);
        ui.Focus(editor); ui.Capture(editor); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.Throws<InvalidOperationException>(old.Dispose); region.Status = Error(); ui.Update();
        Assert.True(old.IsDisposed); Assert.True(editor.IsDisposed); Assert.Null(old.Parent); Assert.False(editor.IsAttached); Assert.False(editor.IsFocused);
        Assert.Equal(1, resources[0].Count); Assert.Equal(1, attachmentResource.Count); Assert.Equal(1, textSource.Subscribers); Assert.Equal(2, textSource.Adds); Assert.Equal(1, textSource.Removes);
        var input = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)); Assert.False(input.KeyboardFocused); Assert.False(input.PointerCaptured);
        old.Dispose(); ui.Dispose(); ui.Dispose(); Assert.All(resources, resource => Assert.Equal(1, resource.Count)); Assert.Equal(0, textSource.Subscribers);
        Assert.True(editors[1].IsDisposed); Assert.Equal(textSource.Adds, textSource.Removes);
    }

    [Fact]
    public void OldDetachObserverFailure_CommitsNewBranchNotifiesOthersAndCleansOldWithoutFactoryRetry()
    {
        var calls = 0; var oldResource = new DisposalCounter(); var region = new LoadStatusRegion(status => { calls++; return new Text(status.Message); });
        using var ui = Create(region); ui.Update(); var old = region.Child!; old.Own(oldResource); var later = 0;
        old.AttachmentChanged += () => { if (!old.IsAttached) throw new TestException(); };
        old.AttachmentChanged += () =>
        {
            Assert.Null(old.Parent); Assert.False(old.IsAttached); Assert.NotSame(old, region.Child); Assert.Same(region, region.Child!.Parent); Assert.True(region.Child.IsAttached); later++;
        };
        region.Status = Loading(); Assert.Throws<TestException>(() => ui.Update());
        var accepted = region.Child; Assert.True(old.IsDisposed); Assert.Equal(1, oldResource.Count); Assert.Equal(1, later);
        Assert.Equal(Loading(), region.DisplayedStatus); Assert.False(accepted!.IsDisposed);
        region.RefreshContent(); ui.Update(); Assert.Same(accepted, region.Child); Assert.Equal(2, calls); Assert.Equal(1, later);
    }

    [Fact]
    public void CandidateAttachmentObserverFailure_KeepsAcceptedLiveChildAndDisplayedSnapshot()
    {
        var fail = false; var calls = 0; var resource = new DisposalCounter(); var later = 0;
        var region = new LoadStatusRegion(status =>
        {
            calls++; var row = new Text(status.Message);
            if (fail)
            {
                row.Own(resource);
                row.AttachmentChanged += () => { if (row.IsAttached) throw new TestException(); };
                row.AttachmentChanged += () => { if (row.IsAttached) later++; };
            }
            return row;
        });
        using var ui = Create(region); ui.Update(); var old = region.Child; fail = true; region.Status = Error();
        Assert.Throws<TestException>(() => ui.Update());
        var accepted = region.Child!; Assert.NotSame(old, accepted); Assert.True(accepted.IsAttached); Assert.Same(region, accepted.Parent); Assert.False(accepted.IsDisposed);
        Assert.True(old!.IsDisposed); Assert.Equal(Error(), region.DisplayedStatus); Assert.Equal(1, later); Assert.Equal(0, resource.Count);
        region.RefreshContent(); ui.Update(); Assert.Same(accepted, region.Child); Assert.Equal(2, calls);
        ui.Dispose(); Assert.True(accepted.IsDisposed); Assert.Equal(1, resource.Count);
    }

    [Fact]
    public void NotificationAndOldCleanupErrors_AggregateAfterCommitAndFinishAllResources()
    {
        var calls = 0; var region = new LoadStatusRegion(status => { calls++; return new Panel().WithChildren(new Text(status.Message)); });
        using var ui = Create(region); ui.Update(); var old = region.Child!; var first = new ThrowingDisposable(); var second = new ThrowingDisposable();
        old.Own(first); old.Children[0].Own(second);
        old.AttachmentChanged += () => { if (!old.IsAttached) throw new TestException(); };
        region.Status = Error(); var error = Assert.Throws<AggregateException>(() => ui.Update());
        Assert.Equal(3, error.Flatten().InnerExceptions.Count); Assert.True(old.IsDisposed); Assert.True(old.Children[0].IsDisposed);
        Assert.Equal(1, first.Count); Assert.Equal(1, second.Count); Assert.Equal(Error(), region.DisplayedStatus);
        var accepted = region.Child!; Assert.True(accepted.IsAttached); Assert.False(accepted.IsDisposed); Assert.Same(region, accepted.Parent);
        region.RefreshContent(); ui.Update(); Assert.Same(accepted, region.Child); Assert.Equal(2, calls); Assert.Equal(1, first.Count); Assert.Equal(1, second.Count);
    }

    [Fact]
    public void RejectedDraftCleanupFailure_ReportsValidationAndCleanupWhilePreservingOldBranch()
    {
        var fail = false; var resource = new ThrowingDisposable(); var disposed = new Text(); disposed.Dispose(); InvalidSubtree? draft = null;
        var region = new LoadStatusRegion(status => !fail ? new Text(status.Message) : draft = new InvalidSubtree(disposed).Configure(x => x.Own(resource)));
        using var ui = Create(region); ui.Update(); var kept = region.Child;
        fail = true; region.Status = Error(); var error = Assert.Throws<AggregateException>(() => ui.Update());
        Assert.Equal(2, error.InnerExceptions.Count); Assert.IsType<ObjectDisposedException>(error.InnerExceptions[0]); Assert.IsType<TestException>(error.InnerExceptions[1]);
        Assert.True(draft!.IsDisposed); Assert.Equal(1, resource.Count); Assert.Same(kept, region.Child); Assert.False(kept!.IsDisposed); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus);
    }

    [Fact]
    public void EqualSnapshotOrExplicitRefresh_PreservesBranchAndDoesNotRedraw()
    {
        var source = new Source(); var calls = 0; var surface = new HeadlessSurface();
        var region = new LoadStatusRegion(status => { calls++; return new Text(status.Message); }).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = new UiSession(region, surface, new UiViewport(240, 140)); ui.Update(); var kept = region.Child; var frames = surface.Renders;
        source.Status = LoadStatus.Ready; Assert.False(ui.Update()); Assert.Same(kept, region.Child); Assert.Equal(1, calls); Assert.Equal(frames, surface.Renders);
        region.RefreshContent(); Assert.False(ui.Update()); Assert.Same(kept, region.Child); Assert.Equal(1, calls); Assert.Equal(frames, surface.Renders);
    }

    [Fact]
    public void DetachReattach_DropsPendingRefreshAndRereadsChangesMadeWhileDetached()
    {
        var source = new Source(); var calls = new List<LoadStatus>();
        var region = new LoadStatusRegion(status => { calls.Add(status); return new Text(status.Message); }).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        var root = new Panel().WithChildren(region); using var ui = Create(root); ui.Update(); var kept = region.Child;
        region.Status = Loading("stale queue"); var inFlight = source.CaptureNotification(); root.Items.Remove(region);
        Assert.Equal(0, source.Subscribers); Assert.False(kept!.IsDisposed); Assert.False(kept.IsAttached);
        source.Status = Error("changed detached"); var reads = source.Reads; RunOnWorker(inFlight); ui.Update();
        Assert.Equal(reads, source.Reads); Assert.Same(kept, region.Child); Assert.Single(calls);
        root.Items.Add(region); Assert.Same(kept, region.Child); Assert.Equal(1, source.Subscribers); ui.Update();
        Assert.Equal(Error("changed detached"), region.DisplayedStatus); Assert.Equal("changed detached", Assert.IsType<Text>(region.Child).Value); Assert.True(kept.IsDisposed);
        Assert.Equal(new[] { LoadStatus.Ready, Error("changed detached") }, calls); Assert.Equal(2, source.Adds); Assert.Equal(1, source.Removes);
        reads = source.Reads; RunOnWorker(inFlight); Assert.False(ui.Update()); Assert.Equal(reads, source.Reads);
    }

    [Fact]
    public void ReattachWithEqualStatus_PreservesOldChildAndRestoresOneModelSubscription()
    {
        var source = new Source(); var calls = 0;
        var region = new LoadStatusRegion(status => { calls++; return new Text(status.Message); }).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        var root = new Panel().WithChildren(region); using var ui = Create(root); ui.Update(); var kept = region.Child;
        for (var i = 0; i < 3; i++)
        {
            root.Items.Remove(region); Assert.Equal(0, source.Subscribers); Assert.False(kept!.IsDisposed);
            root.Items.Add(region); ui.Update(); Assert.Same(kept, region.Child); Assert.Equal(1, source.Subscribers);
        }
        Assert.Equal(1, calls); Assert.Equal(4, source.Adds); Assert.Equal(3, source.Removes);
        ui.Dispose(); Assert.Equal(0, source.Subscribers); Assert.True(kept!.IsDisposed); Assert.Equal(source.Adds, source.Removes);
    }

    [Fact]
    public void BackgroundNotification_OnlyReadsAndCreatesBranchDuringOwnerUpdate()
    {
        var source = new Source(); var factoryThreads = new List<int>();
        var region = new LoadStatusRegion(status => { factoryThreads.Add(Environment.CurrentManagedThreadId); return new Text(status.Message); })
            .Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = Create(region); ui.Update(); var kept = region.Child; var reads = source.Reads;
        RunOnWorker(() => source.Status = Loading());
        Assert.Same(kept, region.Child); Assert.Equal(LoadStatus.Ready, region.Status); Assert.Equal(reads, source.Reads); Assert.Single(factoryThreads);
        ui.Update(); Assert.Equal(Loading(), region.DisplayedStatus); Assert.Equal(Environment.CurrentManagedThreadId, source.LastReadThread);
        Assert.Equal(new[] { Environment.CurrentManagedThreadId, Environment.CurrentManagedThreadId }, factoryThreads);
    }

    [Fact]
    public void TransferToWorker_DropsOldQueuedAndInflightWorkAndRefreshesOnNewOwner()
    {
        var source = new Source(); var factoryThreads = new List<int>();
        var region = new LoadStatusRegion(status => { factoryThreads.Add(Environment.CurrentManagedThreadId); return new Text(status.Message); })
            .Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        var root = new Panel().WithChildren(region); using var oldUi = Create(root); oldUi.Update(); var initial = region.Child;
        region.Status = Loading("old queued"); source.Status = Error("old source queue"); var stale = source.CaptureNotification(); root.Items.Remove(region);
        RunOnWorker(() =>
        {
            source.SetSilently(Ready("new attachment")); using var newUi = Create(region); newUi.Update();
            Assert.Equal(Ready("new attachment"), region.DisplayedStatus); Assert.True(initial!.IsDisposed);
            Assert.Equal(Environment.CurrentManagedThreadId, source.LastReadThread); Assert.Equal(Environment.CurrentManagedThreadId, factoryThreads[1]);
            var reads = source.Reads; source.SetSilently(Error("silent")); stale(); Assert.False(newUi.Update()); Assert.Equal(reads, source.Reads);
            source.Status = Loading("current event"); newUi.Update(); Assert.Equal(Loading("current event"), region.DisplayedStatus);
        });
        var finalReads = source.Reads; oldUi.Update(); Assert.Equal(finalReads, source.Reads); Assert.Equal(3, factoryThreads.Count);
        Assert.True(region.IsDisposed); Assert.True(region.Child!.IsDisposed); Assert.Equal(0, source.Subscribers); Assert.Equal(2, source.Adds); Assert.Equal(2, source.Removes);
    }

    [Fact]
    public void DetachedStatusChange_AppliesImmediatelyAndSamePreviousNodeCanBeReused()
    {
        var resource = new DisposalCounter(); var reused = new Text("kept").Configure(x => x.Own(resource)); var calls = 0;
        using var region = new LoadStatusRegion(_ => { calls++; return reused; }); var attachmentChanges = 0;
        reused.AttachmentChanged += () => attachmentChanges++;
        region.Status = Loading(); Assert.Same(reused, region.Child); Assert.Equal(Loading(), region.DisplayedStatus); Assert.Equal(2, calls);
        region.Status = Error(); Assert.Same(reused, region.Child); Assert.Equal(Error(), region.DisplayedStatus); Assert.Equal(3, calls);
        region.RefreshContent(); Assert.Equal(3, calls); Assert.Equal(0, attachmentChanges); Assert.Equal(0, resource.Count); Assert.False(reused.IsDisposed);
        using var ui = Create(region); ui.Update(); region.Status = Ready(); ui.Update();
        Assert.Same(reused, region.Child); Assert.Equal(Ready(), region.DisplayedStatus); Assert.Equal(4, calls); Assert.Equal(1, attachmentChanges); Assert.Equal(0, resource.Count);
        ui.Dispose(); Assert.True(reused.IsDisposed); Assert.Equal(1, resource.Count);
    }

    [Theory]
    [InlineData(-1, "invalid phase")]
    [InlineData(3, "invalid phase")]
    [InlineData(0, null)]
    public void InvalidStatus_RejectsBeforePropertyOrBranchChanges(int phase, string? message)
    {
        var calls = 0; using var region = new LoadStatusRegion(status => { calls++; return new Text(status.Message); });
        using var ui = Create(region); ui.Update(); var kept = region.Child; var changes = 0; region.PropertyChanged += (_, _) => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Status = new LoadStatus((LoadPhase)phase, message!));
        Assert.Equal(LoadStatus.Ready, region.Status); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus); Assert.Same(kept, region.Child);
        Assert.Equal(0, changes); Assert.Equal(1, calls); Assert.False(ui.Update());
    }

    [Fact]
    public void InvalidBoundStatus_ReportsDuringUpdateAndNextNotificationRecovers()
    {
        var source = new Source(); var region = new LoadStatusRegion(status => new Text(status.Message)).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = Create(region); ui.Update(); var kept = region.Child;
        source.Status = new LoadStatus((LoadPhase)99, "bad"); Assert.Throws<ArgumentOutOfRangeException>(() => ui.Update());
        Assert.Same(kept, region.Child); Assert.Equal(LoadStatus.Ready, region.Status); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus); Assert.Equal(1, source.Subscribers);
        source.Status = Error("recovered"); ui.Update(); Assert.Equal(Error("recovered"), region.DisplayedStatus); Assert.True(kept!.IsDisposed);
    }

    [Fact]
    public void NullFactoryDisposedRegionAndForeignThreadAccess_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new LoadStatusRegion(null!));
        using var region = new LoadStatusRegion(status => new Text(status.Message)); using var ui = Create(region); ui.Update(); var kept = region.Child;
        RunOnWorker(() => { Assert.Throws<InvalidOperationException>(() => region.Status = Error()); Assert.Throws<InvalidOperationException>(region.RefreshContent); });
        Assert.Same(kept, region.Child); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus);
        ui.Dispose(); Assert.Throws<ObjectDisposedException>(region.RefreshContent); Assert.Throws<ObjectDisposedException>(() => region.Status = Error());
    }

    [Fact]
    public void DispatchFailure_RefreshCanRetryWithoutReplacingUnchangedBranch()
    {
        var calls = 0; using var region = new LoadStatusRegion(status => { calls++; return new Text(status.Message); });
        var queue = new Queue<Action>(); var fail = false;
        region.Attach(() => { }, action => { if (fail) throw new TestException(); queue.Enqueue(action); }); queue.Dequeue()();
        var kept = region.Child; fail = true; Assert.Throws<TestException>(region.RefreshContent);
        fail = false; region.RefreshContent(); Assert.Single(queue)(); queue.Clear();
        Assert.Same(kept, region.Child); Assert.Equal(1, calls);
        region.Status = Error(); Assert.Single(queue)(); queue.Clear();
        Assert.Equal(Error(), region.DisplayedStatus); Assert.NotSame(kept, region.Child); Assert.True(kept!.IsDisposed); Assert.Equal(2, calls);
    }

    [Fact]
    public void ErrorBranchRetryButton_UpdatesModelAndReplacesBranchOnNextUpdate()
    {
        var source = new Source { Status = Error("retry available") }; Button? retry = null;
        var region = new LoadStatusRegion(status => status.Phase == LoadPhase.Error
            ? new VStack().WithChildren(new Text(status.Message), retry = new Button("retry").OnClick(() => source.Status = Loading("retrying")))
            : new Text(status.Message)).Bind(LoadStatusRegion.StatusProperty, source, x => x.Status);
        using var ui = Create(region); ui.Update(); var errorBranch = region.Child!; var button = retry!;
        var x = button.Bounds.X + button.Bounds.Width / 2; var y = button.Bounds.Y + button.Bounds.Height / 2;
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerDown, x, y)).Handled);
        Assert.True(ui.HandleInput(new UiInput(InputKind.PointerUp, x, y)).Handled);
        Assert.Equal(Loading("retrying"), source.Snapshot); Assert.Same(errorBranch, region.Child); Assert.Equal(Error("retry available"), region.DisplayedStatus);
        ui.Update(); Assert.Equal(Loading("retrying"), region.DisplayedStatus); Assert.Equal("retrying", Assert.IsType<Text>(region.Child).Value);
        Assert.True(errorBranch.IsDisposed); Assert.True(button.IsDisposed); Assert.False(button.IsFocused);
        Assert.False(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).KeyboardFocused);
        Assert.Equal(1, source.Subscribers);
    }

    [Theory]
    [InlineData(LoadPhase.Ready)]
    [InlineData(LoadPhase.Loading)]
    [InlineData(LoadPhase.Error)]
    public void EmptyMessage_IsValidForEveryDefinedPhase(LoadPhase phase)
    {
        using var region = new LoadStatusRegion(status => new Text(status.Message)); using var ui = Create(region); ui.Update();
        var empty = new LoadStatus(phase, ""); region.Status = empty; ui.Update();
        Assert.Equal(empty, region.Status); Assert.Equal(empty, region.DisplayedStatus); Assert.Equal("", Assert.IsType<Text>(region.Child).Value); Assert.True(region.Child!.IsAttached);
    }

    [Fact]
    public void DetachedFactoryFailure_LeavesDesiredStatusAndOldBranchAvailableForExplicitRefresh()
    {
        var fail = false; using var region = new LoadStatusRegion(status => { if (fail) throw new TestException(); return new Text(status.Message); });
        var kept = region.Child!; fail = true;
        Assert.Throws<TestException>(() => region.Status = Error("detached failure"));
        Assert.Equal(Error("detached failure"), region.Status); Assert.Equal(LoadStatus.Ready, region.DisplayedStatus); Assert.Same(kept, region.Child);
        Assert.False(kept.IsDisposed); Assert.False(region.IsAttached); Assert.Same(region, kept.Parent);
        fail = false; region.RefreshContent(); Assert.Equal(Error("detached failure"), region.DisplayedStatus); Assert.True(kept.IsDisposed); Assert.Equal("detached failure", Assert.IsType<Text>(region.Child).Value);
    }

    private static void RunOnWorker(Action action)
    {
        Exception? error = null;
        var worker = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }) { IsBackground = true };
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(10)), "Worker did not finish."); Assert.Null(error);
    }

    private sealed class InvalidSubtree(Element child) : Element
    {
        public override IReadOnlyList<Element> Children { get; } = [child];
    }
    private sealed class TestException : Exception;
    private sealed class ThrowingDisposable : IDisposable
    {
        public int Count { get; private set; }
        public void Dispose() { Count++; throw new TestException(); }
    }
    private sealed class Source : INotifyPropertyChanged
    {
        private LoadStatus _status = LoadStatus.Ready;
        private PropertyChangedEventHandler? _changed;
        public int Reads { get; private set; }
        public int LastReadThread { get; private set; }
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public LoadStatus Snapshot => _status;
        public LoadStatus Status
        {
            get { Reads++; LastReadThread = Environment.CurrentManagedThreadId; return _status; }
            set { _status = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; Adds++; }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
        public void SetSilently(LoadStatus status) => _status = status;
        public Action CaptureNotification() { var captured = _changed; return () => captured?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
    }
    private sealed class TextSource : INotifyPropertyChanged
    {
        private string _value = "neighbor editable";
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public int Adds { get; private set; }
        public int Removes { get; private set; }
        public string Value { get => _value; set { _value = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; Subscribers++; Adds++; }
            remove { _changed -= value; Subscribers--; Removes++; }
        }
    }
    private sealed class HeadlessSurface : IUiRenderSurface
    {
        public int Renders { get; private set; }
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { root.Draw(new RecordingDrawingContext()); Renders++; }
        public void Dispose() { }
    }
}
