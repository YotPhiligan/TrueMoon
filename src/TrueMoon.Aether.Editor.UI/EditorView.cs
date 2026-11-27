using System.Collections.Immutable;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Aether.Editor.UI.Diagnostics;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Fluent editor composition borrowing its presentation model and style declaration.</summary>
public sealed class EditorView : View<IEditorViewModel>, IUiInputObserver
{
    private static readonly IFactory<ViewportLayout> Viewports = new Factory<ViewportLayout>(static () => new());
    private static readonly IFactory<InspectorLayout> Inspectors = new Factory<InspectorLayout>(static () => new());
    private static readonly IFactory<EditorIsland, (Element Content, bool FromRight)> Islands =
        new Factory<EditorIsland, (Element Content, bool FromRight)>(static x => new(x.Content, x.FromRight));
    private static readonly IFactory<EditorLayout, ShellRegions> Layouts = new Factory<EditorLayout, ShellRegions>(static x =>
        new(x.Tools, x.Hierarchy, x.Viewport, x.Inspector, x.Footer, x.Diagnostics, overlayChrome: x.Compact));

    private readonly record struct ShellRegions(Element? Tools, EditorIsland Hierarchy, ContentControl Viewport,
        EditorIsland Inspector, Element Footer, EditorDiagnosticsView? Diagnostics, bool Compact);

    public static ViewStyleRole<Border> PaneRole { get; } = new("Pane");
    /// <summary>Persistent floating panel surfaces; separate from ordinary content sections.</summary>
    public static ViewStyleRole<Border> IslandSurfaceRole { get; } = new("IslandSurface");
    /// <summary>Shadow gutters of the anchored island hosts.</summary>
    public static ViewStyleRole<EditorIsland> IslandRole { get; } = new("Island");
    public static ViewStyleRole<VStack> PaneContentRole { get; } = new("PaneContent");
    public static ViewStyleRole<VStack> PaneItemsRole { get; } = new("PaneItems");
    public static ViewStyleRole<Text> PaneTitleRole { get; } = new("PaneTitle");
    public static ViewStyleRole<Button> EntityButtonRole { get; } = new("EntityButton");
    public static ViewStyleRole<VStack> EntityListRole { get; } = new("LegacyEntityList");
    public static ViewStyleRole<Text> SceneCaptionRole { get; } = new("LegacySceneCaption");
    public static ViewStyleRole<PropertyRow> PropertyRowRole { get; } = new("PropertyRow");
    public static ViewStyleRole<Text> PropertyNameRole { get; } = new("PropertyName");
    public static ViewStyleRole<VStack> PropertyRowsRole { get; } = new("PropertyRows");
    private readonly IEditorViewModel _model;
    private readonly Dictionary<string, Button> _entities = new(StringComparer.Ordinal);

    /// <summary>The live summary in the prepared status bar or runtime footer.</summary>
    public Text Status => StatusBar?.SummaryButton ?? this.FallbackStatus ?? throw new InvalidOperationException("Editor content is not prepared.");
    /// <summary>The live scene caption in the prepared hierarchy or runtime explorer.</summary>
    public Text SceneTitle => Hierarchy?.Caption ?? this.FallbackSceneTitle ?? throw new InvalidOperationException("Editor content is not prepared.");
    /// <summary>The viewport borrowed by the host before attachment and retained by the editor tree.</summary>
    public EditorViewport Canvas { get; }
    /// <summary>Parent-owned exclusive viewport controller, created lazily through the App factory on first presentation.</summary>
    public IEditorViewportController? ViewportController { get; private set; }
    internal void EnsureViewportController(IFactory<IEditorViewportController,EditorViewportArguments> factory)
    {
        if(ViewportController!=null)return;
        var controller=factory.Create(new(this))??throw new InvalidOperationException("Viewport controller factory returned null.");
        ViewportController=controller;Own(controller);
    }
    /// <summary>The project-document view, when configured and prepared.</summary>
    public EditorDocumentView? Documents { get; private set; }
    /// <summary>The standalone Transform form, when configured and prepared.</summary>
    public EditorTransformView? Transform { get; private set; }
    /// <summary>The schema-driven inspector, when configured and prepared.</summary>
    public EditorInspectorView? Inspector { get; private set; }
    /// <summary>Retained inspector island above the full-area viewport, available after preparation.</summary>
    public Element InspectorPanel => this.InspectorIsland;
    /// <summary>Standalone toolbar buttons. Compact actions are provided by the window header.</summary>
    public IReadOnlyDictionary<EditorCommand, Button> Commands { get; private set; } = ImmutableDictionary<EditorCommand, Button>.Empty;
    public IReadOnlyDictionary<string, Button> Entities => _entities;
    /// <summary>Owns editor popups; the window title borrows it before the editor attaches.</summary>
    public OverlayHost Overlays { get; }
    /// <summary>Borrowed window-owned compact header, when configured by the application.</summary>
    public EditorHeaderView? Header { get; internal set; }
    /// <summary>The compact footer, when configured and prepared.</summary>
    public EditorStatusBarView? StatusBar { get; private set; }
    /// <summary>The hierarchical explorer, when configured and prepared.</summary>
    public EditorHierarchyView? Hierarchy { get; private set; }
    /// <summary>The diagnostics console, when configured and prepared; its model and collector remain App-owned.</summary>
    public EditorDiagnosticsView? Diagnostics { get; private set; }

    public EditorView(IEditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents = null,
        IFactory<EditorTransformView>? transform = null, bool compactHeader = false)
        : this(model, style, documents, transform, compactHeader, null) { }

    /// <summary>Creates a retained editor and its optional factory-owned status footer.</summary>
    public EditorView(IEditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar)
        : this(model, style, documents, transform, compactHeader, statusBar, null) { }

    /// <summary>Creates an editor with an optional parent-owned hierarchical explorer.</summary>
    public EditorView(IEditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar, IFactory<EditorHierarchyView>? hierarchy)
        : this(model, style, documents, transform, compactHeader, statusBar, hierarchy, null) { }

    /// <summary>Creates the editor with a schema-driven inspector instead of the original fixed form.</summary>
    public EditorView(IEditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar, IFactory<EditorHierarchyView>? hierarchy,
        IFactory<EditorInspectorView>? inspector)
        : this(model, style, documents, transform, compactHeader, statusBar, hierarchy, inspector, null) { }

    /// <summary>Creates the editor with a parent-owned diagnostics console in addition to its authoring panels.</summary>
    public EditorView(IEditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar, IFactory<EditorHierarchyView>? hierarchy,
        IFactory<EditorInspectorView>? inspector, IFactory<EditorDiagnosticsView>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(style);
        _model = model;
        var shell = new Panel();
        Canvas = OwnDraft(new EditorViewport());
        Overlays = OwnDraft(new OverlayHost(shell));
        if (compactHeader) Overlays.PopupInsets = new(0, 36, 0, 0);

        Content(model, ui =>
        {
            var root = ui.Adopt(Overlays);
            var canvas = ui.Adopt(Canvas);

            StatusBar = statusBar == null ? null : ui.Create(statusBar, new EditorStatusBarArguments(Overlays)).Element;
            Documents = documents == null ? null : ui.Create(documents).Element;
            Inspector = inspector == null ? null : ui.Create(inspector).Element;
            Transform = Inspector != null || transform == null ? null : ui.Create(transform).Element;
            Hierarchy = hierarchy == null ? null : ui.Create(hierarchy).Element;
            Diagnostics = diagnostics == null ? null : ui.Create(diagnostics).Element;

            // Keep the contextual surface clipped to its slot, even without a toolbar.
            Element? tools = compactHeader && Documents is { } compactTools ? ui.VStack(compactTools).Element : null;
            if (!compactHeader)
            {
                var commands = new Dictionary<EditorCommand, Button>();
                var toolbar = ui.HStack(
                    Command(EditorCommand.Build, "Собрать", x => x.CanBuild),
                    Command(EditorCommand.Start, "Запустить", x => x.CanStart).WithRole(EditorViewRoles.StartCommand),
                    Command(EditorCommand.Play, "Play", x => x.CanPlay).WithRole(EditorViewRoles.PlayCommand),
                    Command(EditorCommand.Pause, "Пауза", x => x.CanPause),
                    Command(EditorCommand.Step, "Шаг", x => x.CanStep),
                    Command(EditorCommand.Stop, "Стоп", x => x.CanStop).WithRole(EditorViewRoles.StopCommand),
                    Command(EditorCommand.Replace, "Применить сборку", x => x.CanReplace),
                    Command(EditorCommand.Cancel, "Отмена", x => x.CanCancel),
                    ui.Button("Тема").OnClick(ToggleTheme)).NamedOptional("Toolbar").WithRole(EditorViewRoles.RuntimeToolbar);
                if (Diagnostics is { } console)
                    toolbar.WithChildren(ui.Button("Диагностика").OnClick(() => console.DataContext!.Toggle()));
                tools = Documents is { } documentsView ? ui.VStack(toolbar, documentsView).Element : toolbar.Element;
                Commands = commands;

                UiControl<Button, IEditorViewModel> Command(EditorCommand command, string label, Expression<Func<IEditorViewModel, bool>> enabled)
                {
                    var button = ui.Button(label).Bind(UiProperties.Enabled, model, enabled).OnClick(() => model.Execute(command));
                    commands.Add(command, button.Element);
                    return button;
                }
            }

            Element footer = (Element?)StatusBar ?? ui.Text().NamedOptional("FallbackStatus").Bind(Text.ValueProperty, model, x => x.StatusText);
            Element explorer = (Element?)Hierarchy ?? Pane(ui, "СЦЕНА",
                ui.Text(model.Scene.Id).NamedOptional("FallbackSceneTitle").WithRole(SceneCaptionRole),
                ui.ScrollViewer(ui.VStack().NamedOptional("EntityList").WithRole(EntityListRole)))
                .WithRole(IslandSurfaceRole);
            var left = ui.Create(Islands, (explorer, false)).Named("HierarchyIsland").WithRole(IslandRole);

            var viewport = ui.Create<ContentControl>().Named("ViewportFrame").Content(
                ui.Create(Viewports).WithChildren(canvas,
                    ui.Text("VIEWPORT · Предпросмотр сцены").Named("ViewportTitle").SetValue(UiProperties.Visible, false)));

            var selection = ui.Text().Named("Selection").Bind(Text.ValueProperty, model, x => x.SelectedName);
            var error = ui.Text().Named("Error").Bind(Text.ValueProperty, model, x => x.ErrorText);
            UiControl<Border, IEditorViewModel> inspectorSurface;
            if (Inspector is { } inspectorView)
            {
                inspectorSurface = ui.Border(ui.Create(Inspectors).WithChildren(
                    ui.Text("ИНСПЕКТОР").WithRole(PaneTitleRole), selection, inspectorView, error)).WithRole(PaneRole);
            }
            else
            {
                var hint = ui.Text(Transform == null ? "Свойства сцены · только просмотр" : "Transform · Ctrl+Enter — применить")
                    .NamedOptional("InspectorHint").WithRole(EditorViewRoles.LegacyInspectorHint);
                var properties = ui.Create<ContentControl>().NamedOptional("PropertiesPanel");
                properties.Element.SetContent(_propertySlot = CreatePropertySlot());
                inspectorSurface = Transform is { } form
                    ? Pane(ui, "ИНСПЕКТОР", ui.ScrollViewer(ui.VStack(selection, hint, form, properties, error).WithRole(PaneItemsRole)))
                    : Pane(ui, "ИНСПЕКТОР", selection, hint, properties, error);
            }
            var right = ui.Create(Islands, (inspectorSurface.WithRole(IslandSurfaceRole).Element, true))
                .Named("InspectorIsland").WithRole(IslandRole)
                .SetValue(EditorIsland.IsOpenProperty, model.IsInspectorVisible)
                .Bind(EditorIsland.IsOpenProperty, model, x => x.IsInspectorVisible);

            shell.Add(ui.Create(Layouts, new ShellRegions(tools, left, viewport, right, footer, Diagnostics, compactHeader)));
            return root;
        });
        this.WithViewStyle(style);
    }

    private static UiControl<Border, IEditorViewModel> Pane(IUiBuildContext<IEditorViewModel> ui, string title, params Element[] children) =>
        ui.Border(ui.VStack(ui.Text(title).WithRole(PaneTitleRole), ui.VStack(children).WithRole(PaneItemsRole))
            .WithRole(PaneContentRole)).WithRole(PaneRole);

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        if (Hierarchy != null && Inspector != null) return;
        this.ObservePropertyChanged(_model, ModelChanged, SynchronizeLegacyParts, UiSynchronization.AfterTreeChange);
    }

    private void SynchronizeLegacyParts()
    {
        if (this.FallbackSceneTitle is { } title)
        {
            title.Value = _model.Scene.Id;
            UpdateEntities();
        }
        if (Inspector == null) UpdateProperties();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs change)
    {
        switch (change.PropertyName)
        {
            case null or "": SynchronizeLegacyParts(); break;
            case nameof(IEditorViewModel.Scene) when this.FallbackSceneTitle is { } title: title.Value = _model.Scene.Id; break;
            case nameof(IEditorViewModel.EntityIds) when Hierarchy == null: UpdateEntities(); break;
            case nameof(IEditorViewModel.SelectedId) when Hierarchy == null: UpdateSelection(); break;
            case nameof(IEditorViewModel.Properties) when Inspector == null: UpdateProperties(); break;
        }
    }

    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => Documents?.HandleShortcut(input) == true ||
        Inspector?.HandleShortcut(input) == true || Transform?.HandleShortcut(input) == true;
    /// <summary>Routes document shortcuts even when an exposed scene has no focused UI element.</summary>
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if(ViewportController?.PreviewInput(input,context)==true)return true;
        if (Hierarchy is { IsAttached: true } hierarchy && hierarchy.NameEditor.IsFocused) return false;
        if (Documents == null || input.Kind != InputKind.KeyDown || !input.Control || input.Key is not (UiKey.S or UiKey.W)) return false;
        if (Header is { IsAttached: true } header) header.Menu.CloseMenu();
        return Documents.HandleShortcut(input);
    }

    private void UpdateEntities()
    {
        VerifyAccess();
        if (this.EntityList is not { } list) return;
        var ids = _model.EntityIds;
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var id in _entities.Keys.Where(id => !wanted.Contains(id)).ToArray())
        {
            var old = _entities[id]; list.Items.Remove(old); old.Dispose(); _entities.Remove(id);
        }
        foreach (var id in ids)
        {
            if (_entities.ContainsKey(id)) continue;
            var button = new Button(EditorScene.Name(id)).WithRole(EntityButtonRole)
                .SetValue(UiProperties.AccessibleName, id).OnClick(() => _model.Select(id));
            _entities.Add(id, button); list.Add(button);
        }
        for (var index = 0; index < ids.Count; index++)
        {
            var button = _entities[ids[index]];
            if (ReferenceEquals(list.Children[index], button)) continue;
            list.Items.Move(list.Items.IndexOf(button), index);
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        VerifyAccess();
        foreach (var (id, button) in _entities)
            button.Tone = id == _model.SelectedId ? ControlTone.Accent : ControlTone.Neutral;
    }

    private void UpdateProperties()
    {
        VerifyAccess();
        if (_propertySlot is not { } slot) return;
        // The model publishes a fresh property list on every refresh: the token compares keys and values by content,
        // so a notification that changes nothing keeps the installed rows, their drafts and focus.
        slot.State = Token(_model.Properties);
    }

    private ConditionalContent<PropertySetToken?>? _propertySlot;

    /// <summary>The identity of a presented property block: equal keys and equal values keep the installed rows.</summary>
    /// <param name="Keys">Property names in display order.</param>
    /// <param name="Values">Property values in the same order.</param>
    /// <remarks><c>ImmutableArray</c> itself compares by reference, so the token compares its own contents.</remarks>
    public sealed record PropertySetToken(ImmutableArray<string> Keys, ImmutableArray<string> Values)
    {
        /// <inheritdoc />
        public bool Equals(PropertySetToken? other) => other != null &&
            Keys.AsSpan().SequenceEqual(other.Keys.AsSpan()) && Values.AsSpan().SequenceEqual(other.Values.AsSpan());

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var key in Keys) hash.Add(key);
            foreach (var value in Values) hash.Add(value);
            return hash.ToHashCode();
        }
    }

    private ConditionalContent<PropertySetToken?> CreatePropertySlot() => new(null, CreatePropertyRows);

    private static VStack? CreatePropertyRows(PropertySetToken? token)
    {
        if (token == null) return null;
        var rows = new VStack().WithRole(PropertyRowsRole);
        for (var index = 0; index < token.Keys.Length; index++)
            rows.Add(new PropertyRow(token.Keys[index], token.Values[index]).WithRole(PropertyRowRole));
        return rows;
    }

    private static PropertySetToken Token(IReadOnlyList<KeyValuePair<string, string>> properties)
    {
        var keys = ImmutableArray.CreateBuilder<string>(properties.Count);
        var values = ImmutableArray.CreateBuilder<string>(properties.Count);
        foreach (var property in properties)
        {
            keys.Add(property.Key);
            values.Add(property.Value);
        }

        return new PropertySetToken(keys.ToImmutable(), values.ToImmutable());
    }

    private void ToggleTheme()
    {
        Element root = this;
        while (root.Parent != null) root = root.Parent;
        root.ApplyTheme((Theme.Surface == Theme.Dark.Surface ? Theme.Light : Theme.Dark) with
        {
            FontFamily = Theme.FontFamily, FontSize = Theme.FontSize, FontWeight = Theme.FontWeight, FontItalic = Theme.FontItalic
        });
    }

    private sealed class InspectorLayout : ElementList
    {
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var fixedHeight = 0f;
            for (var i = 0; i < 2; i++)
            {
                Children[i].Measure(available, text);
                if (Children[i].IsVisible) fixedHeight += Children[i].DesiredSize.Height + 6;
            }
            var error = (Text)Children[3];
            error.Measure(available, text);
            var errorHeight = error.Value.Length == 0 ? 0 : Math.Min(60, error.DesiredSize.Height);
            Children[2].Measure(new(available.Width, Math.Max(0, available.Height - fixedHeight - errorHeight)), text);
            return new(Math.Min(280, available.Width), Math.Min(available.Height, fixedHeight + Children[2].DesiredSize.Height + errorHeight));
        }
        protected override void ArrangeCore(Rect area)
        {
            var y = area.Y; var bottom = area.Y + area.Height;
            for (var i = 0; i < 2; i++)
            {
                var part = Children[i]; var height = part.IsVisible ? Math.Min(part.DesiredSize.Height, Math.Max(0, bottom - y)) : 0;
                part.Arrange(new(area.X, y, area.Width, height)); if (height > 0) y += height + 6;
            }
            var error = (Text)Children[3]; var errorHeight = error.Value.Length == 0 ? 0 : Math.Min(60, error.DesiredSize.Height);
            Children[2].Arrange(new(area.X, y, area.Width, Math.Max(0, bottom - y - errorHeight)));
            error.Arrange(new(area.X, bottom - errorHeight, area.Width, errorHeight));
        }
    }

    private sealed class ViewportLayout : ElementList
    {
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            foreach (var child in Children) child.Measure(available, text);
            return new(600, 420);
        }

        protected override void ArrangeCore(Rect area)
        {
            var requested = !Children[1].IsVisible ? 0 : float.IsNaN(Children[1].Height) ? Children[1].DesiredSize.Height : Children[1].Height;
            var height = Math.Min(requested, area.Height);
            Children[1].Arrange(new(area.X, area.Y, area.Width, height));
            Children[0].Arrange(new(area.X, area.Y + height, area.Width, Math.Max(0, area.Height - height)));
        }
    }
}
