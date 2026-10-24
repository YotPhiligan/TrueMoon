using System.ComponentModel;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Schema-driven retained inspector. Borrows one model/style and owns only its control tree and subscriptions.</summary>
public sealed class EditorInspectorView : View<EditorInspectorViewModel>, IUiInputObserver, IUiFrameParticipant
{
    /// <summary>Component section appearance.</summary>
    public static ViewStyleRole<Border> SectionRole { get; } = new("InspectorSection");
    /// <summary>Stack spacing inside dynamic sections.</summary>
    public static ViewStyleRole<VStack> StackRole { get; } = new("InspectorStack");
    /// <summary>Compact action rows.</summary>
    public static ViewStyleRole<HStack> RowRole { get; } = new("InspectorRow");
    /// <summary>Component disclosure buttons.</summary>
    public static ViewStyleRole<Button> HeaderRole { get; } = new("InspectorHeader");
    /// <summary>Compact inspector actions, including dynamic schema and presence buttons.</summary>
    public static ViewStyleRole<Button> ActionRole { get; } = new("InspectorAction");
    /// <summary>Square icon-only actions, separate from labeled schema choices.</summary>
    public static ViewStyleRole<IconButton> IconRole { get; } = new("InspectorIcon");
    /// <summary>Scalar property labels.</summary>
    public static ViewStyleRole<Text> LabelRole { get; } = new("InspectorLabel");
    /// <summary>Numeric scalar inputs.</summary>
    public static ViewStyleRole<TextBox> InputRole { get; } = new("InspectorInput");
    /// <summary>Boolean scalar inputs.</summary>
    public static ViewStyleRole<CheckBox> BooleanRole { get; } = new("InspectorBoolean");
    /// <summary>Nullable switches.</summary>
    public static ViewStyleRole<CheckBox> NullRole { get; } = new("InspectorNull");
    /// <summary>Multiline string inputs.</summary>
    public static ViewStyleRole<RichTextEditor> StringRole { get; } = new("InspectorString");
    /// <summary>Reset action.</summary>
    public static ViewStyleRole<Button> ResetRole { get; } = new("InspectorReset");
    /// <summary>Metadata and provenance.</summary>
    public static ViewStyleRole<Text> MetadataRole { get; } = new("InspectorMetadata");
    /// <summary>Field validation diagnostics.</summary>
    public static ViewStyleRole<Text> ErrorRole { get; } = new("InspectorError");

    private readonly EditorInspectorViewModel _model;
    private readonly Dictionary<ComponentId, InspectorComponentControls> _components = [];
    private readonly Dictionary<(ComponentId, string), InspectorPropertyControls> _properties = [];
    private readonly Dictionary<(ComponentTypeId, int), Button> _addButtons = [];
    private readonly Dictionary<ComponentId, Button> _restoreButtons = [];
    private readonly HashSet<ComponentId> _collapsed = [];
    private IconButton? _hint;
    private OverlayHost? _hintHost;
    private Rect _hintBounds;
    private float _hintX, _hintY;
    private TimeSpan _hintRemaining;
    private readonly List<Element> _hintAncestors = [];
    /// <summary>Gets retained component parts.</summary>
    public IReadOnlyDictionary<ComponentId, InspectorComponentControls> Components => _components;
    /// <summary>Gets property editors using stable addresses.</summary>
    public IReadOnlyDictionary<(ComponentId, string), InspectorPropertyControls> Properties => _properties;
    /// <summary>Gets addition choices.</summary>
    public IReadOnlyDictionary<(ComponentTypeId, int), Button> AddButtons => _addButtons;
    /// <summary>Gets hidden occurrence restoration actions.</summary>
    public IReadOnlyDictionary<ComponentId, Button> RestoreButtons => _restoreButtons;
    /// <summary>Gets the complete inspector layout.</summary>
    public VStack Form { get; } = VStack.Create().WithRole(StackRole);
    /// <summary>Gets the dynamic component host.</summary>
    public VStack ComponentList { get; } = VStack.Create().WithRole(StackRole);
    /// <summary>Gets scrolling for the whole inspector.</summary>
    public ScrollViewer Scroll { get; } = ScrollViewer.Create();
    /// <summary>Gets the empty-selection guidance.</summary>
    public Text Empty { get; }
    /// <summary>Gets Apply.</summary>
    public Button Apply { get; }
    /// <summary>Gets Cancel.</summary>
    public Button Cancel { get; }
    /// <summary>Gets shared Undo.</summary>
    public Button Undo { get; }
    /// <summary>Gets shared Redo.</summary>
    public Button Redo { get; }
    /// <summary>Gets the schema-choice disclosure.</summary>
    public Button AddComponent { get; }
    /// <summary>Gets the retained schema choices.</summary>
    public VStack AddChoices { get; } = VStack.Create().WithRole(StackRole);
    /// <summary>Gets hidden component choices.</summary>
    public VStack Removed { get; } = VStack.Create().WithRole(StackRole);
    /// <summary>Gets complete selectable mutation diagnostics.</summary>
    public RichTextEditor Error { get; }
    /// <summary>Compact primary actions; each icon has a semantic name and delayed hover hint.</summary>
    public HStack Actions { get; } = HStack.Create().WithRole(RowRole);
    /// <inheritdoc />
    public bool NeedsFrame => _hintRemaining > TimeSpan.Zero || _hint != null && !HintIsCurrent();

    /// <summary>Builds a parent-owned inspector on the UI owner thread.</summary>
    /// <param name="model">Borrowed presentation.</param><param name="style">Borrowed scoped appearance.</param>
    public EditorInspectorView(EditorInspectorViewModel model, IViewStyle<EditorInspectorView> style)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        try
        {
            DataContext = model;
            Empty = Text.Plain().BindText(model, x => x.SelectionText).Bind(UiProperties.Visible, model, x => x.NoSelection);
            Apply = Action(UiSymbol.Check, "Применить изменения · Ctrl+Enter").Bind(UiProperties.Enabled, model, x => x.CanApply).OnClick(model.Apply);
            Cancel = Action(UiSymbol.Close, "Отменить правку · Esc").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel);
            Undo = Action(UiSymbol.Undo, "Отменить изменение сцены · Ctrl+Z вне текстового поля").Bind(UiProperties.Enabled, model, x => x.CanUndo).OnClick(model.Undo);
            Redo = Action(UiSymbol.Redo, "Повторить изменение сцены · Ctrl+Y вне текстового поля").Bind(UiProperties.Enabled, model, x => x.CanRedo).OnClick(model.Redo);
            AddComponent = Action(UiSymbol.Add, "Добавить компонент").Bind(UiProperties.Enabled, model, x => x.CanChangeComponents)
                .OnClick(() => AddChoices.IsVisible = !AddChoices.IsVisible);
            AddChoices.IsVisible = false;
            Error = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
            Error.MinWidth = 0; Error.MinHeight = 0;
            Error.Bind(RichTextEditor.TextProperty, model, x => x.ErrorText).Bind(UiProperties.Visible, model, x => x.HasError);
            Actions.WithChildren(Apply, Cancel, Undo, Redo, AddComponent);
            Form.WithChildren(Empty, Actions, AddChoices, Error, ComponentList, Removed);
            Scroll.Content(Form); Content(model, _ => Scroll); this.WithViewStyle(style);
            Synchronize();
            model.PropertyChanged += ModelChanged;
            AttachmentChanged += Attachment;
        }
        catch (Exception original)
        { try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); } throw; }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(EditorInspectorViewModel.HasSelection)) Dispatch(CloseHint);
        if (change.PropertyName is nameof(EditorInspectorViewModel.Components) or nameof(EditorInspectorViewModel.AddableTypes) or
            nameof(EditorInspectorViewModel.RemovedComponents) or nameof(EditorInspectorViewModel.CanChangeComponents)) Dispatch(Synchronize);
    }
    private void Synchronize()
    {
        VerifyAccess();
        var wanted = _model.Components.Select(c => c.Id).ToHashSet();
        foreach (var id in _components.Keys.Where(id => !wanted.Contains(id)).ToArray())
        {
            var section = _components[id]; ComponentList.Items.Remove(section.Section); section.Section.Dispose(); _components.Remove(id);
            foreach (var address in _properties.Keys.Where(key => key.Item1 == id).ToArray()) _properties.Remove(address);
            _collapsed.Remove(id);
        }
        for (var index = 0; index < _model.Components.Count; index++)
        {
            var component = _model.Components[index];
            if (!_components.TryGetValue(component.Id, out var parts))
            {
                var body = VStack.Create().WithRole(StackRole);
                var header = new ComponentHeader().WithRole(HeaderRole);
                header.OnClick(() => { if (!_collapsed.Add(component.Id)) _collapsed.Remove(component.Id); body.IsVisible = !_collapsed.Contains(component.Id); header.Expanded = body.IsVisible; });
                var remove = Action(UiSymbol.Delete, "Удалить компонент " + component.Title).OnClick(() => _model.RemoveComponent(component.Id));
                var metadata = Text.Plain().WithRole(MetadataRole);
                var section = Border.Create().WithRole(SectionRole).Content(VStack.Create().WithRole(StackRole).WithChildren(
                    new ComponentLine(header, remove), metadata, body));
                parts = new(section, header, body, remove, metadata); _components.Add(component.Id, parts); ComponentList.Add(section);
            }
            parts.Header.Value = component.Title;
            parts.Header.AccessibleName = component.Title + " · свернуть или раскрыть";
            parts.Metadata.Value = component.SourceText + " · v" + component.SchemaVersion + " · " + component.Id.Value.ToString("D")[..8];
            parts.Metadata.AccessibleName = $"Type {component.TypeId.Value:D}; component {component.Id.Value:D}; schema {component.SchemaVersion}";
            parts.Remove.IsEnabled = _model.CanChangeComponents;
            var addresses = component.Properties.Select(p => (component.Id, p.Key)).ToHashSet();
            foreach (var address in _properties.Keys.Where(key => key.Item1 == component.Id && !addresses.Contains(key)).ToArray()) RemoveProperty(address, parts.Body);
            for (var position = 0; position < component.Properties.Count; position++)
            {
                var property = component.Properties[position]; var address = (component.Id, property.Key);
                if (_properties.TryGetValue(address, out var existing) && !ReferenceEquals(existing.Property, property)) RemoveProperty(address, parts.Body);
                if (!_properties.TryGetValue(address, out var row))
                { row = CreateProperty(property, component.Title); _properties.Add(address, row); parts.Body.Add(row.Row); }
                Reorder(parts.Body, row.Row, position);
            }
            Reorder(ComponentList, parts.Section, index);
        }
        var types = _model.AddableTypes.Select(t => (t.Schema.TypeId, t.Schema.Version)).ToHashSet();
        foreach (var key in _addButtons.Keys.Where(key => !types.Contains(key)).ToArray())
        { var old = _addButtons[key]; AddChoices.Items.Remove(old); old.Dispose(); _addButtons.Remove(key); }
        for (var i = 0; i < _model.AddableTypes.Count; i++)
        {
            var choice = _model.AddableTypes[i]; var key = (choice.Schema.TypeId, choice.Schema.Version);
            if (!_addButtons.TryGetValue(key, out var button))
            {
                button = Button.Simple(choice.Title).WithRole(ActionRole).OnClick(() => { _model.AddComponent(choice.Schema.TypeId, choice.Schema.Version); AddChoices.IsVisible = false; });
                _addButtons.Add(key, button); AddChoices.Add(button);
            }
            button.IsEnabled = _model.CanChangeComponents && choice.CanAdd; Reorder(AddChoices, button, i);
        }
        var hidden = _model.RemovedComponents.Select(c => c.Id).ToHashSet();
        foreach (var id in _restoreButtons.Keys.Where(id => !hidden.Contains(id)).ToArray())
        { var old = _restoreButtons[id]; var row = old.Parent!; Removed.Items.Remove(row); row.Dispose(); _restoreButtons.Remove(id); }
        for (var i = 0; i < _model.RemovedComponents.Count; i++)
        {
            var removed = _model.RemovedComponents[i];
            if (!_restoreButtons.TryGetValue(removed.Id, out var button))
            {
                button = Action(UiSymbol.Redo, "Восстановить компонент " + removed.Title).OnClick(() => _model.RestoreComponent(removed.Id));
                _restoreButtons.Add(removed.Id, button);
                Removed.Add(HStack.Create().WithRole(RowRole).WithChildren(button, Text.Plain(removed.Title).WithRole(MetadataRole)));
            }
            button.IsEnabled = _model.CanChangeComponents; Reorder(Removed, button.Parent!, i);
        }
    }
    private InspectorPropertyControls CreateProperty(InspectorProperty property, string component)
    {
        Element input;
        if (property.Kind == PropertyValueKind.Boolean)
            input = CheckBox.Create().WithRole(BooleanRole).BindTwoWay(CheckBox.IsCheckedProperty, property, x => x.BooleanValue);
        else if (property.Kind == PropertyValueKind.String)
            input = RichTextEditor.Create(new() { WordWrap = true }).WithRole(StringRole)
                .BindTwoWay(RichTextEditor.TextProperty, property, x => x.Value);
        else input = TextBox.Create().WithRole(InputRole).BindTwoWay(TextBox.ValueProperty, property, x => x.Value);
        input.MinWidth = 0; input.MinHeight = 0;
        input.Bind(UiProperties.Enabled, property, x => x.CanWrite);
        input.AccessibleName = component + " " + property.Key;
        var reset = Action(UiSymbol.Undo, "Сбросить " + component + " · " + property.Label + " к наследуемому значению или умолчанию").WithRole(ResetRole).Bind(UiProperties.Enabled, property, x => x.CanReset)
            .OnClick(() => _model.ResetProperty(property));
        var nullable = property.IsNullable ? CheckBox.Create("null").WithRole(NullRole)
            .BindTwoWay(CheckBox.IsCheckedProperty, property, x => x.IsNull)
            .Bind(UiProperties.Enabled, property, x => x.CanChangeNull) : null;
        var source = Text.Plain().WithRole(MetadataRole).BindText(property, x => x.SourceText);
        var row = VStack.Create().WithRole(StackRole);
        HStack line = property.Kind == PropertyValueKind.String ? HStack.Create() : new ScalarLine();
        line.WithRole(RowRole).WithChildren(Text.Plain(property.Label).WithRole(LabelRole));
        if (property.Kind != PropertyValueKind.String) line.Add(input);
        line.Add(reset); if (nullable != null) line.Add(nullable);
        row.Add(line); if (property.Kind == PropertyValueKind.String) row.Add(input);
        row.WithChildren(source, Text.Plain().WithRole(ErrorRole).BindText(property, x => x.Error).Bind(UiProperties.Visible, property, x => x.HasError));
        return new(property, row, input, nullable, reset, source);
    }
    private void RemoveProperty((ComponentId, string) address, VStack body)
    { var old = _properties[address]; body.Items.Remove(old.Row); old.Row.Dispose(); _properties.Remove(address); }
    private static void Reorder(VStack parent, Element child, int index)
    { if (ReferenceEquals(parent.Children[index], child)) return; parent.Items.Remove(child); parent.Items.Insert(index, child); }

    /// <summary>Routes shared actions while leaving text-edit undo to the focused scalar editor.</summary>
    /// <param name="input">Normalized input.</param><returns>Whether consumed.</returns>
    public bool HandleShortcut(UiInput input)
    {
        VerifyAccess(); if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape && _model.CanCancel) { _model.Cancel(); return true; }
        if (!input.Control) return false;
        if (input.Key == UiKey.Enter) { _model.Apply(); return true; }
        if (_properties.Values.Any(p => p.Input is TextBox or RichTextEditor && p.Input.IsFocused)) return false;
        if (input.Key == UiKey.Z) { if (input.Shift) _model.Redo(); else _model.Undo(); return true; }
        if (input.Key == UiKey.Y) { _model.Redo(); return true; }
        return false;
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => HandleShortcut(input);
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.PointerMove)
        {
            var hint = IconAt(this, input.X, input.Y);
            if (!ReferenceEquals(hint, _hint))
            {
                CloseHint();
                if (hint != null && FindOverlay() is { Popup: null } host)
                {
                    _hint = hint; _hintHost = host; _hintBounds = hint.Bounds;
                    _hintX = input.X; _hintY = input.Y; _hintRemaining = TimeSpan.FromMilliseconds(500);
                    host.Closed += HintClosed;
                    for (Element? node = this; node != null; node = node.Parent)
                    { _hintAncestors.Add(node); node.PropertyChanged += HintAncestorChanged; }
                }
            }
        }
        else if (input.Kind is InputKind.PointerDown or InputKind.KeyDown or InputKind.FocusLost or InputKind.Wheel) CloseHint();
        return FocusWithin(this) && input.Kind == InputKind.KeyDown &&
            (input.Control && input.Key == UiKey.Enter || input.Key == UiKey.Escape) && HandleShortcut(input);
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_hint == null) return;
        if (!HintIsCurrent()) { CloseHint(); return; }
        if (_hintRemaining <= TimeSpan.Zero) return;
        _hintRemaining -= elapsed;
        if (_hintRemaining > TimeSpan.Zero) return;
        _hintRemaining = TimeSpan.Zero;
        if (_hintHost!.Popup != null) { CloseHint(); return; }
        var popup = new HintSurface { Style = EditorInspectorViewStyle.HintBorder }.Content(
            new Text(_hint.AccessibleName + (_hint.IsEffectivelyEnabled ? "" : " · сейчас недоступно")) { Style = EditorInspectorViewStyle.HintText });
        try { _hintHost.Open(this, popup, _hint.Bounds, context, tooltip: true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private bool HintIsCurrent()
    {
        if (_hint is not { IsDisposed: false, IsAttached: true } || _hint.Bounds != _hintBounds ||
            !ReferenceEquals(IconAt(this, _hintX, _hintY), _hint) || !ReferenceEquals(FindOverlay(), _hintHost)) return false;
        for (var node = Parent; node != null; node = node.Parent)
            if (!node.IsVisible || !node.ChildClipBounds.Contains(_hintX, _hintY)) return false;
        return true;
    }
    private OverlayHost? FindOverlay()
    { for (var node = Parent; node != null; node = node.Parent) if (node is OverlayHost host) return host; return null; }
    private static IconButton? IconAt(Element node, float x, float y)
    {
        if (!node.IsVisible || node.IsDisposed || !node.Bounds.Contains(x, y)) return null;
        if (node is IconButton icon) return icon;
        if (!node.ChildClipBounds.Contains(x, y)) return null;
        for (var i = node.Children.Count - 1; i >= 0; i--)
            if (IconAt(node.Children[i], x, y) is { } found) return found;
        return null;
    }
    private void CloseHint()
    {
        var host = _hintHost;
        ForgetHint();
        if (host is { IsDisposed: false } && ReferenceEquals(host.PopupOwner, this)) host.Close(false);
    }
    private void HintAncestorChanged(Element sender, object property)
    { if (ReferenceEquals(property, UiProperties.Visible) || ReferenceEquals(property, UiProperties.Enabled)) CloseHint(); }
    private void HintClosed() => ForgetHint();
    private void ForgetHint()
    {
        if (_hintHost != null) _hintHost.Closed -= HintClosed;
        foreach (var node in _hintAncestors) node.PropertyChanged -= HintAncestorChanged;
        _hintAncestors.Clear(); _hint = null; _hintHost = null; _hintRemaining = TimeSpan.Zero;
    }
    private void Attachment()
    { if (!IsAttached) ForgetHint(); }
    private static IconButton Action(UiSymbol symbol, string name)
    {
        var button = new IconButton(symbol, name);
        button.Clear(UiProperties.Width); button.Clear(UiProperties.Height); button.Clear(UiProperties.Padding);
        return button.WithRole(ActionRole).WithRole(IconRole);
    }
    private static bool FocusWithin(Element element) => element.IsFocused || element.Children.Any(FocusWithin);
    /// <inheritdoc />
    public override void Dispose()
    {
        if (IsDisposed) return;
        _model.PropertyChanged -= ModelChanged; AttachmentChanged -= Attachment;
        ForgetHint(); base.Dispose();
    }

    private sealed class ComponentLine : HStack
    {
        public ComponentLine(Element header, Element remove) { Add(header); Add(remove); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            foreach (var child in Children) child.Measure(available, text);
            return new(Math.Min(available.Width, Children.Sum(child => child.DesiredSize.Width) + 3), Children.Max(child => child.DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var width = Math.Min(area.Width, Children[1].DesiredSize.Width);
            Children[0].Arrange(new(area.X, area.Y, Math.Max(0, area.Width - width - 3), area.Height));
            Children[1].Arrange(new(area.X + area.Width - width, area.Y, width, area.Height));
        }
    }

    private sealed class HintSurface : Border
    {
        protected override Color ThemeBackground => Theme.Control;
    }

    private sealed class ComponentHeader : Button
    {
        private bool _expanded = true;
        public bool Expanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Invalidate(Invalidation.Render); } }
        protected override void DrawCore(IDrawingContext context)
        {
            base.DrawCore(context);
            UiSymbols.Draw(context, Expanded ? UiSymbol.ChevronDown : UiSymbol.ChevronRight,
                new(Bounds.X + 3, Bounds.Y + (Bounds.Height - 12) / 2, 12, 12), Foreground);
        }
    }

    private sealed class ScalarLine : HStack
    {
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            foreach (var child in Children) child.Measure(available, text);
            return new(Math.Min(available.Width, Children.Sum(c => c.DesiredSize.Width) + Spacing * (Children.Count - 1)),
                Children.Max(c => c.DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var fixedWidth = Children.Where((_, i) => i != 1).Sum(c => c.DesiredSize.Width);
            var inputWidth = Math.Max(0, area.Width - fixedWidth - Spacing * (Children.Count - 1));
            var x = area.X;
            for (var i = 0; i < Children.Count; i++)
            {
                var width = i == 1 ? inputWidth : Children[i].DesiredSize.Width;
                Children[i].Arrange(new(x, area.Y, width, area.Height)); x += width + Spacing;
            }
        }
    }
}
