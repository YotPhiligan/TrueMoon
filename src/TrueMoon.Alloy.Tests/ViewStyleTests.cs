using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis.Windows;
using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using System.Collections.ObjectModel;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ViewStyleTests
{
    private static readonly ViewStyleRole<Button> ActionRole = new("action"), AccentRole = new("accent");
    private static readonly ViewStyleRole<View> ViewRole = new("view");
    private static readonly UiProperty<int> Marker = new("Marker", 5);

    [Fact]
    public void ViewCreation_BuildsOnceAtAttachAndBeforeStyle()
    {
        using var draft = new FactoryView("ready", ui => ui.Text(ui.DataContext?.DataContext ?? "missing").Named("Label"));
        Assert.Null(draft.Child);
        var style = new Declaration<FactoryView>(styles =>
            styles.Part(view => view.GetRequiredPart<Text>("Label")).FontWeight(700));

        Assert.Same(draft, ViewCreation.Prepare(draft, style));
        Assert.Null(draft.Child);
        Assert.Equal(0, draft.Builds);
        ViewCreation.Prepare(draft, style);
        using var session = Create(draft);
        var label = draft.GetRequiredPart<Text>("Label");
        Assert.Equal("ready", label.Value);
        Assert.Equal(700, label.FontWeight);
        Assert.Same(label, draft.PreparedChild);

        Assert.Same(label, draft.Child);
        Assert.Equal(1, draft.Builds);
        Assert.Equal(1, draft.Preparations);
    }

    [Fact]
    public void ViewCreation_PreservesExplicitContent()
    {
        using var draft = new FactoryView("ready", _ => throw new InvalidOperationException("Unexpected build."));
        draft.SetContent(new Text("explicit").Named("Label"));
        var label = draft.GetRequiredPart<Text>("Label");

        ViewCreation.Prepare(draft);
        using var ui = Create(draft);

        Assert.Same(label, draft.Child);
        Assert.Equal(0, draft.Preparations);
    }

    [Fact]
    public void FirstAttach_DisposesUnadoptedTreeAndLeavesDraftWithCallerWhenInstallationFails()
    {
        Text first = null!, second = null!;
        Panel root = null!;
        using var draft = new FactoryView("ready", ui =>
        {
            first = ui.Text("first").Named("Label");
            second = ui.Text("second").Named("Label");
            return root = ui.Panel(first, second);
        });

        using var surface = new Surface();
        var failure = Assert.Throws<InvalidOperationException>(() => new UiSession(draft, surface, new(400, 480)));

        Assert.Contains("already contains a part named 'Label'", failure.Message);
        Assert.False(draft.IsDisposed);
        Assert.False(draft.IsAttached);
        Assert.Null(draft.Child);
        Assert.True(root.IsDisposed);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        Assert.Equal(0, draft.Preparations);
    }

    [Fact]
    public void NamedParts_UseNearestViewForTypedLookupAndStyles()
    {
        using var parent = new PartView().Named("Root");
        var outer = new TextBox().Named("Search");
        var inner = new PartView().Named("Root");
        var innerSearch = new TextBox().Named("Search");
        inner.SetContent(new Panel().WithChildren(innerSearch, new Text("private").Named("OnlyInner")));
        parent.SetContent(new Panel().WithChildren(outer, inner, new Text("separate case").Named("search")));

        parent.WithViewStyle(new Declaration<PartView>(style => style.Part(view => view.GetRequiredPart<TextBox>("Search")).FontSize(19)));
        inner.WithViewStyle(new Declaration<PartView>(style => style.Part(view => view.GetRequiredPart<TextBox>("Search")).FontSize(23)));
        Assert.Same(parent, parent.GetRequiredPart<View>("Root"));
        Assert.Same(inner, inner.GetRequiredPart<View>("Root"));
        Assert.Same(outer, parent.GetRequiredPart<TextBox>("Search"));
        Assert.Same(innerSearch, inner.GetRequiredPart<TextBox>("Search"));
        Assert.True(parent.TryGetPart<Text>("search", out var lowerCase));
        Assert.Equal("separate case", lowerCase.Value);
        Assert.Equal(19, outer.FontSize);
        Assert.Equal(23, innerSearch.FontSize);
        Assert.False(parent.TryGetPart<Text>("OnlyInner", out _));
        Assert.False(parent.TryGetPart<Button>("Search", out _));
        Assert.Throws<KeyNotFoundException>(() => parent.GetRequiredPart<Text>("OnlyInner"));
        Assert.Throws<InvalidOperationException>(() => parent.GetRequiredPart<Button>("Search"));
    }

    [Fact]
    public void NamedParts_RejectDuplicatesBeforeMutationAndFollowReplacementAndTransfer()
    {
        using var first = new PartView();
        using var second = new PartView();
        var part = new TextBox().Named("Search");
        var body = new Panel().WithChildren(part);
        first.SetContent(body);

        using var duplicate = new TextBox().Named("Search");
        Assert.Throws<InvalidOperationException>(() => body.Items.Add(duplicate));
        Assert.Null(duplicate.Parent);
        Assert.Throws<InvalidOperationException>(() => part.Named("Changed"));
        Assert.Same(part, first.GetRequiredPart<TextBox>("Search"));

        using var invalidContent = new Panel().WithChildren(new TextBox().Named("Search"), new TextBox().Named("Search"));
        Assert.Throws<InvalidOperationException>(() => first.SetContent(invalidContent));
        Assert.Same(body, first.Child);
        Assert.Same(part, first.GetRequiredPart<TextBox>("Search"));

        var replacement = new TextBox().Named("Search");
        body.Items[0] = replacement;
        Assert.Null(part.Parent);
        Assert.Same(replacement, first.GetRequiredPart<TextBox>("Search"));
        part.Dispose();
        Assert.True(body.Items.Remove(replacement));
        Assert.False(first.TryGetPart<TextBox>("Search", out _));
        second.SetContent(replacement);
        Assert.Same(replacement, second.GetRequiredPart<TextBox>("Search"));
        var newContent = new TextBox().Named("Search");
        second.SetContent(newContent);
        Assert.Same(newContent, second.GetRequiredPart<TextBox>("Search"));
        Assert.Null(replacement.Parent);
        replacement.Dispose();
    }

    [Fact]
    public void NamedParts_ReplacementIsIndexedBeforeDetachAndAttachNotifications()
    {
        var view = new PartView();
        using var previous = new TextBox().Named("Search");
        var replacement = new TextBox().Named("Search");
        view.SetContent(previous);
        using var ui = Create(view);
        var observed = new List<(Element Reporter, Element Indexed)>();
        previous.AttachmentChanged += () => observed.Add((previous, view.GetRequiredPart<TextBox>("Search")));
        replacement.AttachmentChanged += () => observed.Add((replacement, view.GetRequiredPart<TextBox>("Search")));

        view.SetContent(replacement);

        Assert.Collection(observed,
            item => { Assert.Same(previous, item.Reporter); Assert.Same(replacement, item.Indexed); },
            item => { Assert.Same(replacement, item.Reporter); Assert.Same(replacement, item.Indexed); });
    }

    [Fact]
    public void NamedParts_BoundBatchRejectsDuplicateDraftsAndAllowsReuseAfterRemoval()
    {
        var source = new ObservableCollection<NamedRow> { new("Search") };
        var drafts = new List<Element>();
        var rows = new Panel().BindItems(source, item =>
        {
            var draft = new TextBox().Named(item.Name);
            drafts.Add(draft);
            return draft;
        });
        var view = new PartView();
        view.SetContent(rows);
        using var ui = Create(view);
        ui.Update();
        var original = view.GetRequiredPart<TextBox>("Search");

        source.Clear();
        source.Add(new("Search"));
        source.Add(new("Search"));
        Assert.Throws<InvalidOperationException>(() => ui.Update());
        Assert.Same(original, Assert.Single(rows.Items));
        Assert.Same(original, view.GetRequiredPart<TextBox>("Search"));
        Assert.All(drafts.Skip(1), draft => Assert.True(draft.IsDisposed));

        source.RemoveAt(1);
        ui.Update();
        Assert.True(original.IsDisposed);
        Assert.Same(drafts[^1], view.GetRequiredPart<TextBox>("Search"));
    }

    [Fact]
    public void NamedParts_HeaderAndPopupPreflightKeepsExistingPartsOnConflict()
    {
        var view = new PartView();
        var titleBar = new WindowTitleBar("Window");
        var initialHeader = new TextBox().Named("Header");
        titleBar.SetHeader(initialHeader);
        var anchor = new Button("Open");
        var host = new OverlayHost(new Panel().WithChildren(titleBar, anchor));
        view.SetContent(host);
        using var ui = Create(view);
        ui.Update();
        var initialPopup = new TextBox().Named("Popup");
        host.Open(anchor, initialPopup, anchor.Bounds, ui);

        using var conflictingHeader = new TextBox().Named("Popup");
        using var conflictingPopup = new TextBox().Named("Header");
        Assert.Throws<InvalidOperationException>(() => titleBar.SetHeader(conflictingHeader));
        Assert.Throws<InvalidOperationException>(() => host.Open(anchor, conflictingPopup, anchor.Bounds, ui));
        Assert.Same(initialHeader, titleBar.HeaderContent);
        Assert.Same(initialPopup, host.Popup);
        Assert.Same(initialHeader, view.GetRequiredPart<TextBox>("Header"));
        Assert.Same(initialPopup, view.GetRequiredPart<TextBox>("Popup"));

        var nextHeader = new TextBox().Named("Header");
        var nextPopup = new TextBox().Named("Popup");
        titleBar.SetHeader(nextHeader);
        host.Open(anchor, nextPopup, anchor.Bounds, ui);
        Assert.Same(nextHeader, view.GetRequiredPart<TextBox>("Header"));
        Assert.Same(nextPopup, view.GetRequiredPart<TextBox>("Popup"));
        Assert.Null(initialHeader.Parent);
        Assert.True(initialPopup.IsDisposed);
        initialHeader.Dispose();
    }

    [Fact]
    public void HeaderReplacement_DeferredStyleFailureLeavesOriginalAttachedAndDraftWithCaller()
    {
        var title = new WindowTitleBar("Window");
        var original = new Text("Original");
        title.SetHeader(original);
        Text? draft = null;
        using var incoming = new FactoryView("incoming", ui => draft = ui.Text("Incoming").Named("Label"));
        var failure = new InvalidOperationException("Deferred style rejected the header.");
        ViewCreation.Prepare(incoming, new Declaration<FactoryView>(_ => throw failure));
        Assert.Equal(0, incoming.Builds);
        using var ui = Create(title);
        ui.Update();
        var before = title.Children.ToArray();
        var originalNotifications = 0;
        original.AttachmentChanged += () => originalNotifications++;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => title.SetHeader(incoming)));

        Assert.Equal(1, incoming.Builds);
        Assert.Equal(1, incoming.Preparations);
        Assert.True(Assert.IsType<Text>(draft).IsDisposed);
        Assert.Null(incoming.Child);
        Assert.Null(incoming.Parent);
        Assert.False(incoming.IsAttached);
        Assert.False(incoming.IsDisposed);
        Assert.Same(original, title.HeaderContent);
        Assert.Same(title, original.Parent);
        Assert.True(original.IsAttached);
        Assert.False(original.IsDisposed);
        Assert.Equal(0, originalNotifications);
        Assert.Equal<Element>(before, title.Children);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderChange_ThrowingDetachObserverSeesCommittedSlotAndChildOrder(bool clear)
    {
        var title = new WindowTitleBar("Window");
        using var original = new Text("Original");
        using var incoming = clear ? null : new Text("Incoming");
        var unrelated = new Text("Unrelated");
        title.SetHeader(original);
        title.Items.Add(unrelated);
        using var ui = Create(title);
        ui.Update();
        var bounds = original.Bounds;
        Element[] expected = incoming == null
            ? [title.MinimizeButton, title.MaximizeButton, title.CloseButton, unrelated]
            : [title.MinimizeButton, title.MaximizeButton, title.CloseButton, incoming, unrelated];
        var failure = new InvalidOperationException("Detach observer failed.");
        var attachNotifications = 0;

        void AssertCommitted()
        {
            Assert.Same(incoming, title.HeaderContent);
            Assert.Equal<Element>(expected, title.Children);
            Assert.Same(title, unrelated.Parent);
            Assert.True(unrelated.IsAttached);
            Assert.Null(original.Parent);
            Assert.False(original.IsAttached);
            Assert.False(original.IsDisposed);
            if (incoming != null)
            {
                Assert.Same(title, incoming.Parent);
                Assert.True(incoming.IsAttached);
            }
        }

        original.AttachmentChanged += () =>
        {
            AssertCommitted();
            throw failure;
        };
        if (incoming != null)
            incoming.AttachmentChanged += () =>
            {
                if (!incoming.IsAttached) return;
                AssertCommitted();
                attachNotifications++;
            };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => title.SetHeader(incoming)));

        AssertCommitted();
        Assert.Equal(clear ? 0 : 1, attachNotifications);
        Assert.True(ui.Update());
        if (incoming != null) Assert.Equal(bounds, incoming.Bounds);
        ui.Dispose();
        Assert.False(original.IsDisposed);
        Assert.True(unrelated.IsDisposed);
        if (incoming != null) Assert.True(incoming.IsDisposed);
    }

    [Fact]
    public void HeaderSlot_ExternalItemsChangesDoNotPromoteAnUnrelatedChild()
    {
        var title = new WindowTitleBar("Window");
        using var original = new Text("Original");
        var unrelated = new Text("Unrelated");
        title.SetHeader(original);
        using var ui = Create(title);
        ui.Update();

        title.Items.Insert(3, unrelated);
        Assert.Same(original, title.HeaderContent);
        original.AttachmentChanged += () =>
        {
            if (!original.IsAttached) Assert.Null(title.HeaderContent);
        };
        Assert.True(title.Items.Remove(original));
        title.SetHeader(null);

        Assert.Null(title.HeaderContent);
        Assert.Same(unrelated, title.Children[3]);
        Assert.True(unrelated.IsAttached);
        Assert.Null(original.Parent);
        Assert.False(original.IsAttached);
        Assert.False(original.IsDisposed);

        title.Items.Add(original);
        Assert.True(original.IsAttached);
        Assert.Null(title.HeaderContent);
    }

    [Fact]
    public void FontWeightAndItalic_RespectStylePrecedenceAndRecomputeWithTheme()
    {
        using var view = new SampleView();
        view.ApplyTheme(Theme.Dark with { FontWeight = 600, FontItalic = true });
        view.WithViewStyle(new Declaration<SampleView>(styles =>
            styles.Part(v => v.Action).FontWeight(theme => theme.FontWeight + 100).FontItalic(false)));
        Assert.Equal(700, view.Action.FontWeight); Assert.False(view.Action.FontItalic);
        Assert.Equal(600, view.Editor.FontWeight); Assert.True(view.Editor.FontItalic);
        view.Action.Style = new Style().With(UiProperties.FontWeight, 500);
        view.Action.FontWeight = 800; Assert.Equal(800, view.Action.FontWeight);
        view.Action.Clear(UiProperties.FontWeight); Assert.Equal(500, view.Action.FontWeight);
        view.Action.Style = null;
        view.ApplyTheme(Theme.Light with { FontWeight = 400 });
        Assert.Equal(500, view.Action.FontWeight); Assert.False(view.Action.FontItalic);
        Assert.Equal(400, view.Editor.FontWeight); Assert.False(view.Editor.FontItalic);
        view.WithViewStyle<SampleView>(null); Assert.Equal(400, view.Action.FontWeight);
    }

    [Fact]
    public void StyleScopes_TypedRootPartsAndRolesRespectBoundaryAndDefinitionOrder()
    {
        using var view = new SampleView();
        view.WithRole(ViewRole);
        view.Nested.WithRole(ViewRole).WithViewStyle(new Declaration<NestedView>(styles =>
        {
            styles.Root().Background(Theme.Dark.Positive);
            styles.Role(ActionRole).FontSize(27);
        }));
        var result = view.WithViewStyle(new Declaration<SampleView>(styles =>
        {
            styles.Root().Padding(2).Background(theme => theme.Surface);
            styles.Part(v => v.Editor).Padding(3).FontFamily("part-font");
            styles.Role(ActionRole).FontSize(21).Height(33).Tone(ControlTone.Accent);
            styles.Part(v => v.Action).FontSize(26);
            styles.Role(AccentRole).FontSize(30).Foreground(Theme.Dark.Error);
            styles.Role(ViewRole).BorderWidth(4);
        }));
        Assert.Same(view, result); Assert.Same(view.Action, view.Action.WithRole(AccentRole));
        Assert.Equal(new Thickness(2), view.Padding); Assert.Equal(Theme.Dark.Surface, view.Background); Assert.Equal(4, view.BorderWidth);
        Assert.Equal(new Thickness(3), view.Editor.Padding); Assert.Equal("part-font", view.Editor.FontFamily);
        Assert.Equal(30, view.Action.FontSize); Assert.Equal(Theme.Dark.Error, view.Action.Foreground);
        Assert.Equal(21, view.Secondary.FontSize); Assert.Equal(33, view.Secondary.Height); Assert.Equal(ControlTone.Accent, view.Secondary.Tone);
        Assert.Equal(Theme.Dark.FontSize, view.SameNameRole.FontSize);
        Assert.Equal(27, view.Nested.Action.FontSize); Assert.Equal(Theme.Dark.Positive, view.Nested.Background); Assert.Equal(0, view.Nested.BorderWidth);
    }

    [Fact]
    public void StylePriority_LocalThenDirectThenViewThenThemeThenDefault()
    {
        using var view = new SampleView();
        view.ApplyTheme(Theme.Dark with { FontSize = 17 });
        view.WithViewStyle(new Declaration<SampleView>(styles => styles.Role(ActionRole).FontSize(23).Set(Marker, 11)));
        view.Action.Style = new Style().With(UiProperties.FontSize, 29f).With(Marker, 13);
        view.Action.Set(UiProperties.FontSize, 31); view.Action.Set(Marker, 17);
        Assert.Equal(31, view.Action.FontSize); Assert.Equal(17, view.Action.Get(Marker));
        view.Action.Clear(UiProperties.FontSize); view.Action.Clear(Marker);
        Assert.Equal(29, view.Action.FontSize); Assert.Equal(13, view.Action.Get(Marker));
        view.Action.Style = null;
        Assert.Equal(23, view.Action.FontSize); Assert.Equal(11, view.Action.Get(Marker));
        view.WithViewStyle<SampleView>(null);
        Assert.Equal(17, view.Action.FontSize); Assert.Equal(5, view.Action.Get(Marker));
    }

    [Fact]
    public void GeneratedTreeStyle_PreservesRuleOrderPrecedenceAndThemeRefresh()
    {
        using var view = new PartView();
        var tree = new TreeView().Named("Tree");
        view.SetContent(tree);
        view.ApplyTheme(Theme.Dark with { FontSize = 16 });
        view.WithViewStyle(new Declaration<PartView>(styles =>
        {
            styles.Part(v => v.GetRequiredPart<TreeView>("Tree")).Set(TreeView.RowHeightProperty, 28f);
            styles.TypedPart(v => v.GetRequiredPart<TreeView>("Tree"))
                .FontSize(18)
                .RowHeight(theme => theme.FontSize + 16)
                .Indent(20);
        }));

        Assert.Equal(18, tree.FontSize);
        Assert.Equal(32, tree.RowHeight);
        Assert.Equal(20, tree.Indent);
        tree.Style = new Style().With(TreeView.RowHeightProperty, 36f);
        tree.RowHeight = 40;
        Assert.Equal(40, tree.RowHeight);
        tree.Clear(TreeView.RowHeightProperty);
        Assert.Equal(36, tree.RowHeight);
        tree.Style = null;
        Assert.Equal(32, tree.RowHeight);

        view.ApplyTheme(Theme.Light with { FontSize = 22 });
        Assert.Equal(38, tree.RowHeight);
        Assert.Equal(18, tree.FontSize);
        Assert.Equal(20, tree.Indent);
        Assert.Same(tree, view.GetRequiredPart<TreeView>("Tree"));
        view.WithViewStyle<PartView>(null);
        Assert.Equal(24, tree.RowHeight);
        Assert.Equal(16, tree.Indent);
        Assert.Equal(22, tree.FontSize);
    }

    [Fact]
    public void TypedPart_RejectsNestedTreeAndKeepsActiveStyles()
    {
        using var view = new PartView();
        var outerTree = new TreeView().Named("Tree");
        var inner = new PartView();
        var innerTree = new TreeView().Named("Tree");
        inner.SetContent(innerTree);
        view.SetContent(new Panel().WithChildren(outerTree, inner));
        view.WithViewStyle(new Declaration<PartView>(styles =>
            styles.TypedPart(v => v.GetRequiredPart<TreeView>("Tree")).RowHeight(30)));
        inner.WithViewStyle(new Declaration<PartView>(styles =>
            styles.TypedPart(v => v.GetRequiredPart<TreeView>("Tree")).RowHeight(34)));

        Assert.Throws<ArgumentException>(() => view.WithViewStyle(new Declaration<PartView>(styles =>
        {
            styles.TypedPart(v => v.GetRequiredPart<TreeView>("Tree")).RowHeight(42);
            styles.TypedPart(_ => innerTree).Indent(25);
        })));
        Assert.Equal(30, outerTree.RowHeight);
        Assert.Equal(34, innerTree.RowHeight);
        Assert.Equal(16, innerTree.Indent);
    }

    [Fact]
    public void StyleReplacementAndRemoval_ClearAllOldSetters()
    {
        using var view = new SampleView(); var content = view.Child;
        view.Action.Style = new Style().With(UiProperties.FontSize, 25f);
        view.WithViewStyle(new Declaration<SampleView>(styles =>
            styles.Role(ActionRole).Padding(9).BorderWidth(3).Height(41).Foreground(Theme.Dark.Accent).Set(Marker, 12)));
        Assert.Equal(new Thickness(9), view.Action.Padding); Assert.Equal(41, view.Action.Height); Assert.Equal(12, view.Action.Get(Marker));
        view.WithViewStyle(new Declaration<SampleView>(styles => styles.Role(ActionRole).Background(Theme.Dark.Positive)));
        Assert.Equal(Theme.Dark.ButtonPadding, view.Action.Padding); Assert.Equal(Theme.Dark.BorderWidth, view.Action.BorderWidth);
        Assert.True(float.IsNaN(view.Action.Height)); Assert.Equal(Theme.Dark.Foreground, view.Action.Foreground); Assert.Equal(5, view.Action.Get(Marker));
        Assert.Equal(25, view.Action.FontSize); Assert.Equal(Theme.Dark.Positive, view.Action.Background);
        view.WithViewStyle<SampleView>(null);
        Assert.Equal(Theme.Dark.Control, view.Action.Background); Assert.Equal(25, view.Action.FontSize); Assert.Same(content, view.Child);
    }

    [Fact]
    public void DynamicRoleControls_UseNearestStyleAndClearOldScopeAfterTransfer()
    {
        using var first = new SampleView(); using var second = new SampleView();
        first.WithViewStyle(new Declaration<SampleView>(styles => styles.Role(ActionRole).FontSize(22).Set(Marker, 10)));
        second.WithViewStyle(new Declaration<SampleView>(styles => styles.Role(ActionRole).FontSize(28)));
        var dynamic = new Button("dynamic"); first.Body.Items.Add(dynamic);
        Assert.Equal(Theme.Dark.FontSize, dynamic.FontSize); Assert.Equal(5, dynamic.Get(Marker));
        dynamic.WithRole(ActionRole);
        Assert.Equal(22, dynamic.FontSize); Assert.Equal(10, dynamic.Get(Marker));
        first.Body.Items.Remove(dynamic);
        Assert.Null(dynamic.Parent); Assert.Equal(Theme.Dark.FontSize, dynamic.FontSize); Assert.Equal(5, dynamic.Get(Marker));
        second.Body.Items.Add(dynamic);
        Assert.Equal(28, dynamic.FontSize); Assert.Equal(5, dynamic.Get(Marker));
        second.WithViewStyle<SampleView>(null);
        Assert.Equal(Theme.Dark.FontSize, dynamic.FontSize);
        first.Body.Items.Add(new Button("role before adoption").WithRole(ActionRole));
        Assert.Equal(22, first.Body.Items[^1].FontSize); Assert.Equal(10, first.Body.Items[^1].Get(Marker));
    }

    [Fact]
    public void GeneratedRoles_StyleLaterAdoptedControlsWithoutMatchingAnotherStylesSameName()
    {
        var view = ViewCreation.Prepare(new GeneratedRoleView(), new GeneratedRoleStyle());
        using var ui = Create(view);
        ui.Update();
        var panel = Assert.IsType<Panel>(view.Child);
        var tagged = new Text("Late").WithRole(GeneratedRoleStyle.AccentText);
        var foreignRole = new Text("Other").WithRole(OtherGeneratedRoleStyle.AccentText);
        panel.WithChildren(tagged, foreignRole);
        ui.Update();

        Assert.Equal(28, Assert.IsType<Text>(panel.Children[0]).FontSize);
        Assert.Equal(600, Assert.IsType<Text>(panel.Children[1]).FontWeight);
        Assert.Equal(28, tagged.FontSize);
        Assert.Equal(ui.Root.Theme.FontSize, foreignRole.FontSize);

        view.WithViewStyle(new OtherGeneratedRoleStyle());
        Assert.Equal(ui.Root.Theme.FontSize, tagged.FontSize);
        Assert.Equal(32, foreignRole.FontSize);
    }

    [Fact]
    public void InferredRoles_StyleRepeatedAndLaterAdoptedControlsWithoutMatchingAnotherViewsToken()
    {
        var model = new InferredRoleCaption();
        var style = new Declaration<InferredRoleView>(styles => styles.Role(InferredRoleViewRoles.Accent).FontSize(28));
        var view = ViewCreation.Prepare(new InferredRoleView(model), style);
        using var ui = Create(view);
        ui.Update();

        var panel = Assert.IsType<Panel>(view.Child);
        var bound = Assert.IsType<Text>(panel.Children[0]);
        Assert.Same(bound, view.BoundLabel);
        Assert.Same(panel.Children[1], view.SecondLabel);
        Assert.Equal("Initial", bound.Value);
        Assert.Equal(28, bound.FontSize);
        Assert.Equal(28, view.SecondLabel.FontSize);

        ViewStyleRole<Text> accent = InferredRoleViewRoles.Accent;
        var later = new Text("Late").WithRole(accent);
        var foreign = new Text("Other").WithRole(OtherInferredRoleViewRoles.Accent);
        panel.WithChildren(later, foreign);
        model.Caption = "Updated";
        ui.Update();

        Assert.Equal("Updated", bound.Value);
        Assert.Equal(28, later.FontSize);
        Assert.Equal(ui.Root.Theme.FontSize, foreign.FontSize);
    }

    [Fact]
    public void ThemeChange_RecomputesWithoutRebuildingOrLosingFocusSelectionAndScroll()
    {
        var view = new SampleView(); var content = view.Child; var editor = view.Editor; var scrollContent = view.Scroll.Child;
        var declarations = 0; var evaluations = 0; var attachments = 0;
        view.Editor.AttachmentChanged += () => attachments++;
        view.WithViewStyle(new Declaration<SampleView>(styles =>
        {
            declarations++;
            styles.Root().Background(theme => { evaluations++; return theme.Surface; });
            styles.Part(v => v.Editor).Foreground(theme => theme.Accent);
        }));
        using var ui = Create(view); ui.Update(); ui.Focus(editor);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        view.Scroll.Offset = 80; ui.Update();
        Assert.Equal(6, editor.SelectionLength); Assert.Equal(80, view.Scroll.Offset); Assert.Equal(1, attachments);
        var caret = editor.CaretIndex; var selection = editor.SelectionLength; var before = evaluations;
        ui.SetTheme(Theme.Light); Assert.True(ui.Update());
        Assert.Same(content, view.Child); Assert.Same(editor, view.Editor); Assert.Same(scrollContent, view.Scroll.Child);
        Assert.True(editor.IsFocused); Assert.Equal("abcdef", editor.Value); Assert.Equal(caret, editor.CaretIndex); Assert.Equal(selection, editor.SelectionLength);
        Assert.Equal(80, view.Scroll.Offset); Assert.Equal(1, attachments); Assert.Equal(1, declarations);
        Assert.Equal(Theme.Light.Surface, view.Background); Assert.Equal(Theme.Light.Accent, editor.Foreground); Assert.Equal(before + 1, evaluations);
        Assert.False(ui.Update()); Assert.Equal(before + 1, evaluations);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("foreign")]
    [InlineData("nested-root")]
    [InlineData("nested-part")]
    [InlineData("disposed")]
    public void InvalidParts_RejectBeforeChangingAnyEffectiveValues(string invalid)
    {
        var view = new SampleView(); using var foreign = new Button(); using var disposed = new Button(); disposed.Dispose();
        view.WithViewStyle(new Declaration<SampleView>(styles => { styles.Root().Padding(3); styles.Part(v => v.Action).Height(39); }));
        using var ui = Create(view); ui.Update(); var notifications = 0; view.Invalidated += _ => notifications++;
        Element? part = invalid switch { "null" => null, "foreign" => foreign, "nested-root" => view.Nested, "nested-part" => view.Nested.Action, _ => disposed };
        Assert.Throws<ArgumentException>(() => view.WithViewStyle(new Declaration<SampleView>(styles =>
        {
            styles.Root().Padding(11);
            styles.Part(_ => part!).Height(99);
        })));
        Assert.Equal(new Thickness(3), view.Padding); Assert.Equal(39, view.Action.Height); Assert.Equal(0, notifications);
        Assert.Same(Theme.Dark, view.Theme); Assert.False(ui.Update());
    }

    [Theory]
    [InlineData("configure")]
    [InlineData("theme-value")]
    [InlineData("invalid-value")]
    public void ConfigureOrThemeFailure_RetainsPreviousStyleAndSettledFrame(string failure)
    {
        var view = new SampleView(); var expected = new IOException("style declaration failed");
        view.WithViewStyle(new Declaration<SampleView>(styles => styles.Root().Padding(4)));
        using var ui = Create(view); ui.Update();
        var error = Record.Exception(() => view.WithViewStyle(new Declaration<SampleView>(styles =>
        {
            styles.Root().Padding(12);
            if (failure == "configure") throw expected;
            if (failure == "theme-value") styles.Part(v => v.Action).Background(_ => throw expected);
            else styles.Part(v => v.Action).Set(UiProperties.FontSize, _ => -1f);
        })));
        if (failure == "invalid-value") Assert.IsType<ArgumentOutOfRangeException>(error); else Assert.Same(expected, error);
        Assert.Equal(new Thickness(4), view.Padding); Assert.Equal(Theme.Dark.FontSize, view.Action.FontSize);
        Assert.Equal(Theme.Dark.Control, view.Action.Background); Assert.False(ui.Update());
    }

    [Fact]
    public void FailedThemeChange_KeepsPreviousThemeAndEffectiveValuesAcrossTheTree()
    {
        var view = new SampleView(); var expected = new IOException("light palette unavailable");
        view.WithViewStyle(new Declaration<SampleView>(styles =>
        {
            styles.Root().Background(theme => theme.Surface);
            styles.Part(v => v.Editor).Foreground(theme => ReferenceEquals(theme, Theme.Light) ? throw expected : theme.Foreground);
        }));
        using var ui = Create(view); ui.Update();
        Assert.Same(expected, Assert.Throws<IOException>(() => ui.SetTheme(Theme.Light)));
        Assert.Same(Theme.Dark, view.Theme); Assert.Same(Theme.Dark, view.Action.Theme); Assert.Same(Theme.Dark, view.Nested.Theme);
        Assert.Equal(Theme.Dark.Surface, view.Background); Assert.Equal(Theme.Dark.Foreground, view.Editor.Foreground); Assert.False(ui.Update());
        view.WithViewStyle(new Declaration<SampleView>(styles => styles.Root().Background(theme => theme.Surface)));
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(Theme.Light, view.Theme); Assert.Equal(Theme.Light.Surface, view.Background);
    }

    [Fact]
    public void FailedAdoption_ValidatesNestedViewThemeBeforeTransferringOwnership()
    {
        using var child = new NestedView(); var expected = new IOException("nested light style unavailable");
        child.WithViewStyle(new Declaration<NestedView>(styles => styles.Root().Background(theme =>
            ReferenceEquals(theme, Theme.Light) ? throw expected : theme.Surface)));
        var neighbor = new Text("existing"); var parent = new VStack().WithChildren(neighbor);
        parent.ApplyTheme(Theme.Light);
        using var ui = Create(parent); ui.Update();
        Assert.Same(expected, Assert.Throws<IOException>(() => parent.Items.Add(child)));
        Assert.Null(child.Parent); Assert.False(child.IsAttached); Assert.Same(Theme.Dark, child.Theme); Assert.Equal(Theme.Dark.Surface, child.Background);
        Assert.Same(neighbor, Assert.Single(parent.Items)); Assert.False(ui.Update());
        ui.SetTheme(Theme.Dark); ui.Update(); parent.Items.Add(child); ui.Update();
        Assert.Same(parent, child.Parent); Assert.True(child.IsAttached); Assert.Equal(Theme.Dark.Surface, child.Background);
    }

    [Fact]
    public void CompletedStyleDeclaration_CannotMutateRulesOrAddPartsAfterApplication()
    {
        using var view = new SampleView(); IViewStyleBuilder<SampleView> captured = null!; ViewStyleRule rule = null!;
        view.WithViewStyle(new Declaration<SampleView>(styles => { captured = styles; rule = styles.Root().Padding(6); }));
        Assert.Throws<InvalidOperationException>(() => rule.Padding(10));
        Assert.Throws<InvalidOperationException>(() => captured.Part(v => v.Action));
        Assert.Equal(new Thickness(6), view.Padding); Assert.Equal(Theme.Dark.ButtonPadding, view.Action.Padding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedView_OptionalDataSupportsFluentBindingsWithoutOwningTheModel(bool withData)
    {
        var model = new CaptionModel(); var view = new OptionalDataView(withData ? model : null);
        Assert.Equal(0, view.ContentBuilds); Assert.Equal(0, model.Subscribers);
        if (withData) Assert.Same(model, view.DataContext); else Assert.Null(view.DataContext);
        using var ui = Create(view); ui.Update();
        Assert.Equal(withData ? "initial" : "unbound", view.Label.Value);
        Assert.Equal(withData ? 2 : 0, model.Subscribers);
        if (withData)
        {
            model.Value = "from model"; ui.Update(); Assert.Equal("from model", view.Label.Value); Assert.Equal("from model", view.Editor.Value);
            ui.Focus(view.Editor); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: "from UI")); ui.Update();
            Assert.Equal("from UI", model.Value); Assert.Equal("from UI", view.Label.Value);
        }
        ui.Dispose();
        Assert.True(view.IsDisposed); Assert.Equal(0, model.Subscribers); Assert.Equal(0, model.Disposals); Assert.Equal(1, view.ContentBuilds);
        model.Value = "still usable"; Assert.Equal("still usable", model.Value);
        model.Dispose(); Assert.Equal(1, model.Disposals);
    }

    [Fact]
    public void StyleAndThemeChanges_PreserveFocusedEditorAndBothBindingDirections()
    {
        using var model = new CaptionModel();
        var view = new OptionalDataView(model);
        using var ui = Create(view);
        ui.Update();
        var editor = view.Editor;
        ui.Focus(editor);
        view.WithViewStyle(new Declaration<OptionalDataView>(style =>
            style.Part(v => v.Editor).Foreground(theme => theme.Focus)));
        Assert.Equal(Theme.Dark.Focus, editor.Foreground);
        ui.SetTheme(Theme.Light);
        ui.Update();
        Assert.Equal(Theme.Light.Focus, editor.Foreground);
        Assert.Same(editor, view.Editor);
        Assert.True(editor.IsFocused);
        Assert.Equal(2, model.Subscribers);

        model.Value = "model after restyle";
        ui.Update();
        Assert.Equal("model after restyle", editor.Value);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: "edited after restyle"));
        ui.Update();
        Assert.Equal("edited after restyle", model.Value);
        Assert.Equal("edited after restyle", view.Label.Value);
    }

    private sealed class FactoryView : View<string>
    {
        public FactoryView(string data, Func<IUiBuildContext<string>, Element> build)
            => Content(data, ui =>
            {
                Builds++;
                return build(ui);
            });

        public int Builds { get; private set; }
        public int Preparations { get; private set; }
        public Element? PreparedChild { get; private set; }

        protected override void OnContentPrepared()
        {
            Preparations++;
            PreparedChild = GetRequiredPart<Text>("Label");
        }
    }

    private sealed class PartView : View { }
    private sealed record NamedRow(string Name);
    private static UiSession Create(Element root) => new(root, new Surface(), new(400, 480));
    private sealed class Declaration<TView>(Action<IViewStyleBuilder<TView>> configure) : IViewStyle<TView> where TView : View
    { public void Configure(IViewStyleBuilder<TView> styles) => configure(styles); }
    private sealed class SampleView : View
    {
        public Button Action { get; } = new Button("primary").WithRole(ActionRole).WithRole(AccentRole);
        public Button Secondary { get; } = new Button("secondary").WithRole(ActionRole);
        public Button SameNameRole { get; } = new Button("independent").WithRole(new ViewStyleRole<Button>("action"));
        public TextBox Editor { get; } = new() { Value = "abcdef" };
        public ScrollViewer Scroll { get; } = new ScrollViewer().Height(80).Content(new SizedElement(100, 500));
        public NestedView Nested { get; } = new();
        public VStack Body { get; }
        public SampleView() { Body = new VStack().WithChildren(Action, Secondary, SameNameRole, Editor, Scroll, Nested); SetContent(Body); }
    }
    private sealed class NestedView : View
    {
        public Button Action { get; } = new Button("private").WithRole(ActionRole);
        public NestedView() => SetContent(Action);
    }
    private sealed class OptionalDataView : View<CaptionModel>
    {
        public Text Label { get; private set; } = null!;
        public TextBox Editor { get; private set; } = null!;
        public int ContentBuilds { get; private set; }
        public OptionalDataView(CaptionModel? data = null) => Content(data, ui =>
        {
            ContentBuilds++;
            var label = ui.Text("unbound");
            var editor = ui.TextBox();
            if (DataContext != null)
            {
                label.BindText(value => value.Value);
                editor.BindValueTwoWay(value => value.Value);
            }
            Label = label;
            Editor = editor;
            return ui.VStack(label, editor);
        });
    }
    private sealed class CaptionModel : INotifyPropertyChanged, IDisposable
    {
        private string _value = "initial";
        private PropertyChangedEventHandler? _changed;
        public int Subscribers, Disposals;
        public string Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
        public void Dispose() => Disposals++;
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }
}

internal sealed class GeneratedRoleView : View
{
    public GeneratedRoleView() => Content(ui => ui.Panel(
        ui.Text("Accent").WithRole(GeneratedRoleStyle.AccentText),
        ui.Text("Label").WithRole(GeneratedRoleStyle.Label)));
}

[ViewRole<Text>("AccentText")]
internal sealed partial class GeneratedRoleStyle : IViewStyle<GeneratedRoleView>
{
    public void Configure(IViewStyleBuilder<GeneratedRoleView> styles)
    {
        styles.Role(AccentText).FontSize(28);
        styles.Role(Label).FontWeight(600);
    }
}

[ViewRole<Text>("Label")]
internal sealed partial class GeneratedRoleStyle { }

[ViewRole<Text>("AccentText")]
internal sealed partial class OtherGeneratedRoleStyle : IViewStyle<GeneratedRoleView>
{
    public void Configure(IViewStyleBuilder<GeneratedRoleView> styles) =>
        styles.Role(AccentText).FontSize(32);
}

internal sealed class InferredRoleView : View<InferredRoleCaption>
{
    public InferredRoleView(InferredRoleCaption model) => Content(model, static ui => ui.Panel(
        ui.Text().BindText(data => data.Caption).WithRole(InferredRoleViewRoles.Accent).Named("BoundLabel"),
        ui.Text("Second").Named("SecondLabel").WithRole(InferredRoleViewRoles.Accent)));
}

internal sealed class OtherInferredRoleView : View
{
    public OtherInferredRoleView() => Content(static ui => ui.Text().WithRole(OtherInferredRoleViewRoles.Accent));
}

internal sealed class InferredRoleCaption : INotifyPropertyChanged
{
    private string _caption = "Initial";
    public string Caption
    {
        get => _caption;
        set
        {
            if (_caption == value) return;
            _caption = value;
            PropertyChanged?.Invoke(this, new(nameof(Caption)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
