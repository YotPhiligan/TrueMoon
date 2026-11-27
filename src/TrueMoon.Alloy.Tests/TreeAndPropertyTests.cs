using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class TreeAndPropertyTests
{
    [Fact]
    public void Children_MultipleOwnersAndCycles_AreRejectedBeforeMutation()
    {
        using var first = new Panel();
        using var second = new Panel();
        var child = new Panel();
        first.Items.Add(child);

        Assert.Throws<InvalidOperationException>(() => second.Items.Add(child));
        Assert.Throws<InvalidOperationException>(() => child.Items.Add(first));
        Assert.Throws<InvalidOperationException>(() => first.Items.Add(first));
        Assert.Same(first, child.Parent);
        Assert.Same(child, Assert.Single(first.Children));
        Assert.Empty(second.Children);
        Assert.Empty(child.Children);
    }

    [Fact]
    public void SetContent_InvalidReplacement_PreservesOriginalAttachedChild()
    {
        using var content = new ContentControl();
        using var other = new Panel();
        var original = new SizedElement();
        var owned = new SizedElement();
        content.SetContent(original);
        other.Items.Add(owned);
        content.Attach(() => { }, action => action());

        Assert.Throws<InvalidOperationException>(() => content.SetContent(owned));
        Assert.Same(original, content.Child);
        Assert.Same(content, original.Parent);
        Assert.True(original.IsAttached);
        Assert.Same(other, owned.Parent);
    }

    [Fact]
    public void RemoveThenReparent_PreservesInstanceButDetachesHostAndInteraction()
    {
        using var first = new Panel();
        using var second = new Panel();
        var child = new SizedElement { Width = 42 };
        first.Items.Add(child);
        first.Attach(() => { }, action => action());
        child.IsFocused = true;
        child.IsHovered = true;

        first.Items.Remove(child);
        Assert.Null(child.Parent);
        Assert.False(child.IsAttached);
        Assert.False(child.IsDisposed);
        Assert.False(child.IsFocused);
        Assert.False(child.IsHovered);

        second.Items.Add(child);
        Assert.Same(second, child.Parent);
        Assert.Equal(42, child.Width);
    }

    [Fact]
    public void RepeatedChildEdits_KeepBlockingReentrantMutationFromNotifications()
    {
        using var panel = new Panel();
        var first = new SizedElement();
        var second = new SizedElement();
        panel.Items.Add(first);
        panel.Attach(() => { }, action => action());
        // A change that releases more locks than it took leaves the tree unlocked for later observers: this handler
        // must still be rejected while the Add that attached the row owns the tree. It fires once so disposal stays clean.
        var armed = true;
        second.AttachmentChanged += () =>
        {
            if (!armed) return;
            armed = false;
            panel.Items.Add(new SizedElement());
        };
        var error = Assert.Throws<InvalidOperationException>(() => panel.Items.Add(second));
        Assert.Contains("reentrant", error.Message);
        Assert.Equal(2, panel.Items.Count);

        // The notification threw inside the change, so the change never completed. Dispose must still return every
        // lock it took, otherwise all later edits would keep being rejected as reentrant.
        panel.Items.Add(new SizedElement());
        panel.Items.RemoveAt(2);
        Assert.Equal(2, panel.Items.Count);
    }

    [Fact]
    public void Move_AttachedChildren_PreservesFocusAndAvoidsAttachmentEvents()
    {
        using var panel = new Panel();
        var first = new SizedElement();
        var second = new SizedElement();
        panel.Items.Add(first);
        panel.Items.Add(second);
        panel.Attach(() => { }, action => action());
        first.IsFocused = true;
        var attachmentChanges = 0;
        first.AttachmentChanged += () => attachmentChanges++;

        panel.Items.Move(0, 1);

        Assert.Equal([second, first], panel.Children);
        Assert.True(first.IsAttached);
        Assert.True(first.IsFocused);
        Assert.Same(panel, first.Parent);
        Assert.Equal(0, attachmentChanges);
    }

    [Fact]
    public void Dispose_Subtree_ReleasesOwnedSubscriptionsExactlyOnce()
    {
        var root = new Panel();
        var child = new SizedElement();
        var subscription = new DisposalCounter();
        child.Own(subscription);
        root.Items.Add(child);
        root.Attach(() => { }, action => action());

        root.Dispose();
        root.Dispose();

        Assert.Equal(1, subscription.Count);
        Assert.True(root.IsDisposed);
        Assert.True(child.IsDisposed);
        Assert.False(child.IsAttached);
        Assert.Throws<ObjectDisposedException>(() => child.Width = 10);
    }

    /// <summary>
    /// The detach protocol of one change: the host callback for every node in attach order, then node by node from the
    /// deepest one — its input cancel immediately before its attachment notification — so an observer never sees a
    /// parent reported detached before its children. Both teardown paths (an explicit <c>Detach</c> and a
    /// <c>Dispose</c> of the attached tree) must report the same sequence.
    /// </summary>
    [Fact]
    public void DetachAndDispose_ReportHostCallbackThenInputCancelThenAttachmentOrder()
    {
        var log = new List<string>();
        var root = new LoggedNode("a", log);
        var middle = new LoggedNode("b", log);
        var leaf = new LoggedLeaf("leaf", log);
        var names = new Dictionary<Element, string> { [root] = "a", [middle] = "b", [leaf] = "leaf" };
        middle.Items.Add(leaf);
        root.Items.Add(middle);
        root.Attach(() => { }, action => action(), node => log.Add("detach:" + names[node]));
        log.Clear();
        foreach (var (node, name) in new (Element, string)[] { (root, "a"), (middle, "b"), (leaf, "leaf") })
            node.AttachmentChanged += () => log.Add("attached:" + name);

        root.Detach();

        Assert.Equal(["detach:a", "detach:b", "detach:leaf", "cancel:leaf", "attached:leaf", "cancel:b", "attached:b", "cancel:a", "attached:a"], log);
        Assert.False(root.IsAttached);
        Assert.False(middle.IsAttached);
        Assert.False(leaf.IsAttached);

        root.Attach(() => { }, action => action(), node => log.Add("detach:" + names[node]));
        log.Clear();
        root.Dispose();

        Assert.Equal(["detach:a", "detach:b", "detach:leaf", "cancel:leaf", "attached:leaf", "cancel:b", "attached:b", "cancel:a", "attached:a"], log);
        Assert.True(root.IsDisposed && middle.IsDisposed && leaf.IsDisposed);
        Assert.False(leaf.IsAttached);
    }

    /// <summary>
    /// A notification of one change may dispose an unrelated attached subtree. That nested change owns its own
    /// buffers: it must finish its own teardown before the outer change delivers the notifications that are still
    /// queued, otherwise an observer of the outer root sees a half-disposed subtree.
    /// </summary>
    [Fact]
    public void NestedDisposeDuringNotification_KeepsOuterNotificationsQueued()
    {
        var outer = new Panel();
        var outerLeaf = new SizedElement();
        outer.Items.Add(outerLeaf);
        outer.Attach(() => { }, action => action());
        // One completed detach first: its change returns the notification queue to the thread slot, which is the buffer
        // a second live change must not be handed while the first one still holds entries in it.
        outer.Detach();
        outer.Attach(() => { }, action => action());
        var nested = new Panel();
        var nestedChild = new SizedElement();
        nested.Items.Add(nestedChild);
        nested.Attach(() => { }, action => action());

        var outerRootNotifications = 0;
        var nestedChildWasDisposed = false;
        var nestedChildWasAttached = true;
        outerLeaf.AttachmentChanged += () => nested.Dispose();
        outer.AttachmentChanged += () =>
        {
            outerRootNotifications++;
            nestedChildWasDisposed = nestedChild.IsDisposed;
            nestedChildWasAttached = nestedChild.IsAttached;
        };

        outer.Detach();

        Assert.Equal(1, outerRootNotifications);
        Assert.False(nestedChildWasAttached);
        Assert.True(nestedChildWasDisposed);
        Assert.True(nested.IsDisposed && nestedChild.IsDisposed);
        Assert.False(outer.IsAttached && outerLeaf.IsAttached);
    }

    [Fact]
    public void Property_LocalOverridesStyleThenTheme_ClearRestoresNextPriority()
    {
        using var element = new SizedElement();
        var baseStyle = new Style();
        var styled = new Color(10, 20, 30);
        element.ApplyTheme(Theme.Light);
        element.Style = baseStyle.With(UiProperties.Foreground, styled);
        Assert.Equal(styled, element.Foreground);

        element.Foreground = new Color(40, 50, 60);
        element.ApplyTheme(Theme.Dark);
        Assert.Equal(new Color(40, 50, 60), element.Foreground);
        element.Clear(UiProperties.Foreground);
        Assert.Equal(styled, element.Foreground);
        element.Style = baseStyle;
        Assert.Equal(Theme.Dark.Foreground, element.Foreground);
        Assert.Equal(17, element.Get(new UiProperty<int>("Custom", 17)));

        // Three local values on one node: clearing one of them keeps the other two local, still overriding style and
        // theme, and the effective values never depend on the order the writes arrived in.
        using var ordered = new SizedElement();
        using var reversed = new SizedElement();
        ordered.ApplyTheme(Theme.Light);
        reversed.ApplyTheme(Theme.Light);
        ordered.Style = new Style().With(UiProperties.Width, 7f).With(UiProperties.FontSize, 9f);
        reversed.Style = new Style().With(UiProperties.Width, 7f).With(UiProperties.FontSize, 9f);
        ordered.Set(UiProperties.Width, 80f);
        ordered.Set(UiProperties.FontSize, 13f);
        ordered.Set(UiProperties.IsModified, true);
        reversed.Set(UiProperties.IsModified, true);
        reversed.Set(UiProperties.FontSize, 13f);
        reversed.Set(UiProperties.Width, 80f);

        ordered.Clear(UiProperties.Width);
        reversed.Clear(UiProperties.FontSize);
        Assert.Equal(7f, ordered.Width);
        Assert.Equal(80f, reversed.Width);
        Assert.Equal(13f, ordered.FontSize);
        Assert.Equal(9f, reversed.FontSize);
        Assert.True(ordered.IsModified && reversed.IsModified);

        ordered.Clear(UiProperties.FontSize);
        ordered.Clear(UiProperties.IsModified);
        reversed.Clear(UiProperties.IsModified);
        reversed.Clear(UiProperties.Width);
        Assert.Equal(9f, ordered.FontSize);
        Assert.False(ordered.IsModified);
        Assert.False(reversed.IsModified);
        Assert.Equal(7f, reversed.Width);
        Assert.Equal(9f, reversed.FontSize);

        // Exactly two local values, so no dictionary is involved: clearing the first one collapses the second into its
        // slot, and clearing it afterwards must remove it instead of finding the stale copy again.
        using var pair = new SizedElement();
        pair.ApplyTheme(Theme.Light);
        pair.Style = new Style().With(UiProperties.Width, 7f).With(UiProperties.FontSize, 9f);
        pair.Set(UiProperties.Width, 80f);
        pair.Set(UiProperties.FontSize, 13f);
        pair.Clear(UiProperties.Width);
        Assert.Equal(7f, pair.Width);
        Assert.Equal(13f, pair.FontSize);
        pair.Clear(UiProperties.FontSize);
        Assert.Equal(9f, pair.FontSize);
        Assert.Equal(7f, pair.Width);
    }

    [Fact]
    public void Set_ChangedEffectiveValue_NotifiesAndBubblesItsInvalidationOnce()
    {
        using var root = new Panel();
        var child = new SizedElement();
        root.Items.Add(child);
        var property = new UiProperty<int>("Count", 1, Invalidation.Layout);
        var changes = new List<object>();
        var invalidations = new List<Invalidation>();
        child.PropertyChanged += (_, changed) => changes.Add(changed);
        root.Invalidated += invalidations.Add;

        child.Set(property, 5);
        child.Set(property, 5);

        Assert.Same(property, Assert.Single(changes));
        Assert.Equal(Invalidation.Layout, Assert.Single(invalidations));
        child.Clear(property);
        Assert.Equal(1, child.Get(property));
        Assert.Equal(2, changes.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(float.PositiveInfinity)]
    public void Width_InvalidLocalOrStyleValue_RejectsWithoutChangingLayout(float value)
    {
        using var element = new SizedElement { Width = 42 };
        var invalidations = 0;
        element.Invalidated += _ => invalidations++;

        Assert.Throws<ArgumentOutOfRangeException>(() => element.Width = value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.Width, value));
        Assert.Equal(42, element.Width);
        Assert.Equal(0, invalidations);
    }

    [Fact]
    public void ApplyTheme_ExistingAndNewChildren_ReceiveTypographyAndLayoutInvalidation()
    {
        using var root = new Panel();
        var child = new SizedElement();
        root.Items.Add(child);
        var invalidations = new List<Invalidation>();
        child.Invalidated += invalidations.Add;
        var theme = Theme.Light with { FontSize = 23, FontFamily = "Consolas" };

        root.ApplyTheme(theme);
        var added = new SizedElement();
        root.Items.Add(added);

        Assert.Equal(23, child.FontSize);
        Assert.Equal("Consolas", added.FontFamily);
        Assert.Same(theme, child.Theme);
        Assert.Same(theme, added.Theme);
        Assert.Contains(Invalidation.Layout, invalidations);
    }

    private sealed class LoggedLeaf(string name, List<string> log) : SizedElement
    {
        public override void OnInputCancelled() => log.Add("cancel:" + name);
    }

    private sealed class LoggedNode(string name, List<string> log) : VStack
    {
        public override void OnInputCancelled() => log.Add("cancel:" + name);
    }
}
