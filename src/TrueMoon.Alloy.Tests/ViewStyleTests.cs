using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ViewStyleTests
{
    private static readonly ViewStyleRole<Button> ActionRole = new("action"), AccentRole = new("accent");
    private static readonly ViewStyleRole<View> ViewRole = new("view");
    private static readonly UiProperty<int> Marker = new("Marker", 5);

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
        Assert.Equal(1, view.ContentBuilds); Assert.Equal(0, model.Subscribers);
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
        public SampleView() { Body = new VStack().WithChildren(Action, Secondary, SameNameRole, Editor, Scroll, Nested); Content(() => Body); }
    }
    private sealed class NestedView : View
    {
        public Button Action { get; } = new Button("private").WithRole(ActionRole);
        public NestedView() => Content(() => Action);
    }
    private sealed class OptionalDataView : View<CaptionModel>
    {
        public Text Label { get; } = new("unbound");
        public TextBox Editor { get; } = new();
        public int ContentBuilds { get; private set; }
        public OptionalDataView(CaptionModel? data = null) => Content(data, current =>
        {
            ContentBuilds++;
            if (current != null) { Label.BindText(current, value => value.Value); Editor.BindTwoWay(TextBox.ValueProperty, current, value => value.Value); }
            return new VStack().WithChildren(Label, Editor);
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
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
}
