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

        Assert.Equal(new Element[] { second, first }, panel.Children);
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
}
