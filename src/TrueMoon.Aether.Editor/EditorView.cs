using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Fluent editor composition borrowing its presentation model and style declaration.</summary>
public sealed class EditorView : View<EditorViewModel>
{
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
    private readonly EditorViewModel _model;
    private readonly Dictionary<EditorCommand, Button> _commands = [];
    private readonly Dictionary<string, Button> _entities = new(StringComparer.Ordinal);
    public HStack Toolbar { get; } = HStack.Create();
    public VStack EntityList { get; } = VStack.Create().WithRole(EntityListRole);
    public ContentControl PropertiesPanel { get; } = ContentControl.Create();
    public Text Selection { get; }
    public Text Status { get; }
    public Text Error { get; }
    public Text SceneTitle { get; }
    public Text InspectorHint { get; } = Text.Plain("Свойства сцены · только просмотр");
    public Text ViewportTitle { get; } = Text.Plain("VIEWPORT · Предпросмотр сцены");
    public ContentControl ViewportFrame { get; }
    public EditorViewport Canvas { get; } = new();
    /// <summary>Gets the nested project-document view owned by this tree, when configured.</summary>
    public EditorDocumentView? Documents { get; }
    /// <summary>Gets the nested Transform form owned by this tree.</summary>
    public EditorTransformView? Transform { get; }
    /// <summary>Gets the schema-driven inspector owned by this tree.</summary>
    public EditorInspectorView? Inspector { get; }
    /// <summary>Retained inspector island above the full-area viewport.</summary>
    public Element InspectorPanel { get; }
    /// <summary>Owned reveal/shadow host for the tree.</summary>
    public EditorIsland HierarchyIsland { get; }
    /// <summary>Owned reveal/shadow host for contextual inspection.</summary>
    public EditorIsland InspectorIsland { get; }
    public IReadOnlyDictionary<EditorCommand, Button> Commands => _commands;
    public IReadOnlyDictionary<string, Button> Entities => _entities;
    /// <summary>Owns document-area popups; the title header borrows this host in the same window tree.</summary>
    public OverlayHost Overlays { get; }
    /// <summary>Borrowed window-owned compact header, when configured by the application.</summary>
    public EditorHeaderView? Header { get; internal set; }
    /// <summary>Parent-owned compact footer, when configured.</summary>
    public EditorStatusBarView? StatusBar { get; }
    /// <summary>Parent-owned hierarchy, or null for the legacy flat-list constructor.</summary>
    public EditorHierarchyView? Hierarchy { get; }

    public EditorView(EditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents = null,
        IFactory<EditorTransformView>? transform = null, bool compactHeader = false)
        : this(model, style, documents, transform, compactHeader, null) { }

    /// <summary>Creates a retained editor and its optional factory-owned status footer.</summary>
    public EditorView(EditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar)
        : this(model, style, documents, transform, compactHeader, statusBar, null) { }

    /// <summary>Creates an editor with an optional parent-owned hierarchical explorer.</summary>
    public EditorView(EditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar, IFactory<EditorHierarchyView>? hierarchy)
        : this(model, style, documents, transform, compactHeader, statusBar, hierarchy, null) { }

    /// <summary>Creates the editor with a schema-driven inspector instead of the original fixed form.</summary>
    public EditorView(EditorViewModel model, IViewStyle<EditorView> style, IFactory<EditorDocumentView>? documents,
        IFactory<EditorTransformView>? transform, bool compactHeader,
        IFactory<EditorStatusBarView, EditorStatusBarArguments>? statusBar, IFactory<EditorHierarchyView>? hierarchy,
        IFactory<EditorInspectorView>? inspector)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(style);
        try
        {
            _model = model;
            DataContext = model;
            var shell = Panel.Create();
            Overlays = new(shell);
            Content(model, _ => Overlays);
            StatusBar = statusBar?.Create(new(Overlays));
            if (statusBar != null && StatusBar == null) throw new InvalidOperationException("Status bar factory returned null.");
            Status = StatusBar?.SummaryButton ?? Text.Plain().BindText(model, x => x.StatusText);
            var footer = StatusBar ?? (Element)Status;
            shell.Add(footer);
            Documents = documents?.Create();
            if (documents != null && Documents == null) throw new InvalidOperationException("Document view factory returned null.");
            Inspector = inspector?.Create();
            if (inspector != null && Inspector == null) throw new InvalidOperationException("Inspector factory returned null.");
            Transform = Inspector == null ? transform?.Create() : null;
            if (Inspector == null && transform != null && Transform == null) throw new InvalidOperationException("Transform view factory returned null.");
            Hierarchy = hierarchy?.Create();
            if (hierarchy != null && Hierarchy == null) throw new InvalidOperationException("Hierarchy view factory returned null.");
            if (Hierarchy != null) EntityList.Dispose();
            if (Transform != null) InspectorHint.Value = "Transform · Ctrl+Enter — применить";
            Selection = Text.Plain().BindText(model, x => x.SelectedName);
            Error = Text.Plain().BindText(model, x => x.ErrorText);
            SceneTitle = Hierarchy?.Caption ?? Text.Plain(model.Scene.Id).WithRole(SceneCaptionRole);
            AddCommand(EditorCommand.Build, "Собрать", x => x.CanBuild);
            AddCommand(EditorCommand.Start, "Запустить", x => x.CanStart);
            AddCommand(EditorCommand.Play, "Play", x => x.CanPlay);
            AddCommand(EditorCommand.Pause, "Пауза", x => x.CanPause);
            AddCommand(EditorCommand.Step, "Шаг", x => x.CanStep);
            AddCommand(EditorCommand.Stop, "Стоп", x => x.CanStop);
            AddCommand(EditorCommand.Replace, "Применить сборку", x => x.CanReplace);
            AddCommand(EditorCommand.Cancel, "Отмена", x => x.CanCancel);
            Toolbar.Add(Button.Simple("Тема").OnClick(ToggleTheme));
            Toolbar.IsVisible = !compactHeader;
            UpdateEntities();

            var hierarchyPanel = (Element?)Hierarchy ?? Pane("СЦЕНА", SceneTitle, ScrollViewer.Create().Content(EntityList));
            if (hierarchyPanel is Border legacyPane) legacyPane.WithRole(IslandSurfaceRole);
            HierarchyIsland = new EditorIsland(hierarchyPanel).WithRole(IslandRole);
            ViewportFrame = ContentControl.Create().Content(new ViewportLayout(Canvas, ViewportTitle));
            ViewportTitle.IsVisible = false;
            InspectorHint.IsVisible = Inspector == null;
            PropertiesPanel.IsVisible = Inspector == null;
            var inspectorSurface = Inspector != null ? Border.Create().WithRole(PaneRole).Content(
                new InspectorLayout(Text.Plain("ИНСПЕКТОР").WithRole(PaneTitleRole), Selection, InspectorHint, Inspector, PropertiesPanel, Error)) :
                Transform == null ? Pane("ИНСПЕКТОР", Selection, InspectorHint, PropertiesPanel, Error) :
                Pane("ИНСПЕКТОР", ScrollViewer.Create().Content(VStack.Create().WithRole(PaneItemsRole)
                    .WithChildren(Selection, InspectorHint, Transform, PropertiesPanel, Error)));
            inspectorSurface.WithRole(IslandSurfaceRole);
            InspectorIsland = new EditorIsland(inspectorSurface, fromRight: true) { IsOpen = model.IsInspectorVisible }.WithRole(IslandRole)
                .Bind(EditorIsland.IsOpenProperty, model, x => x.IsInspectorVisible);
            InspectorPanel = InspectorIsland;
            Element tools = Documents == null ? Toolbar : VStack.Create().WithChildren(Toolbar, Documents);
            shell.Items.Remove(footer);
            shell.Add(new EditorLayout(tools, HierarchyIsland, ViewportFrame, InspectorIsland, footer));
            UpdateSelection();
            UpdateProperties();
            this.WithViewStyle(style);
            model.PropertyChanged += ModelChanged;
            Own(new ModelSubscription(model, ModelChanged));

            void AddCommand(EditorCommand command, string label, Expression<Func<EditorViewModel, bool>> enabled)
            {
                var button = Button.Simple(label).Bind(UiProperties.Enabled, model, enabled)
                    .OnClick(() => model.Execute(command));
                _commands.Add(command, button);
                Toolbar.Add(button);
            }
        }
        catch (Exception original)
        {
            try
            {
                Dispose();
                if (Documents is { IsDisposed: false, Parent: null }) Documents.Dispose();
                if (Transform is { IsDisposed: false, Parent: null }) Transform.Dispose();
                if (StatusBar is { IsDisposed: false, Parent: null }) StatusBar.Dispose();
                if (Hierarchy is { IsDisposed: false, Parent: null }) Hierarchy.Dispose();
                if (Inspector is { IsDisposed: false, Parent: null }) Inspector.Dispose();
                if (HierarchyIsland is { IsDisposed: false, Parent: null }) HierarchyIsland.Dispose();
                if (InspectorIsland is { IsDisposed: false, Parent: null }) InspectorIsland.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Editor view creation and cleanup failed.", original, cleanup);
            }

            throw;
        }
    }

    private static Border Pane(string title, params Element[] children) => Border.Create().WithRole(PaneRole).Content(
        VStack.Create().WithRole(PaneContentRole).WithChildren(Text.Plain(title).WithRole(PaneTitleRole),
            VStack.Create().WithRole(PaneItemsRole).WithChildren(children)));

    private void ModelChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(EditorViewModel.Scene) && Hierarchy == null) Dispatch(() => SceneTitle.Value = _model.Scene.Id);
        if (change.PropertyName == nameof(EditorViewModel.EntityIds)) Dispatch(UpdateEntities);
        if (change.PropertyName == nameof(EditorViewModel.SelectedId)) Dispatch(UpdateSelection);
        if (change.PropertyName == nameof(EditorViewModel.Properties)) Dispatch(UpdateProperties);
    }

    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => Documents?.HandleShortcut(input) == true ||
        Inspector?.HandleShortcut(input) == true || Transform?.HandleShortcut(input) == true;

    private void UpdateEntities()
    {
        VerifyAccess();
        if (Hierarchy != null) return;
        var ids = _model.EntityIds;
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var id in _entities.Keys.Where(id => !wanted.Contains(id)).ToArray())
        {
            var old = _entities[id]; EntityList.Items.Remove(old); old.Dispose(); _entities.Remove(id);
        }
        foreach (var id in ids)
        {
            if (_entities.ContainsKey(id)) continue;
            var button = Button.Simple(EditorScene.Name(id)).WithRole(EntityButtonRole)
                .SetValue(UiProperties.AccessibleName, id).OnClick(() => _model.Select(id));
            _entities.Add(id, button); EntityList.Add(button);
        }
        for (var index = 0; index < ids.Count; index++)
        {
            var button = _entities[ids[index]];
            if (ReferenceEquals(EntityList.Children[index], button)) continue;
            EntityList.Items.Remove(button); EntityList.Items.Insert(index, button);
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
        if (Inspector != null) return;
        var rows = VStack.Create().WithRole(PropertyRowsRole);
        foreach (var row in _model.Properties) rows.Add(new PropertyRow(row.Key, row.Value).WithRole(PropertyRowRole));
        var old = PropertiesPanel.Child;
        PropertiesPanel.SetContent(rows);
        old?.Dispose();
    }

    private void ToggleTheme()
    {
        Element root = this;
        while (root.Parent != null) root = root.Parent;
        root.ApplyTheme((Theme.Surface == Theme.Dark.Surface ? Theme.Light : Theme.Dark) with
        {
            FontFamily = Theme.FontFamily
        });
    }

    private sealed class ModelSubscription(EditorViewModel model, PropertyChangedEventHandler callback) : IDisposable
    {
        public void Dispose() => model.PropertyChanged -= callback;
    }

    private sealed class InspectorLayout : ElementList
    {
        public InspectorLayout(params Element[] parts) { foreach (var part in parts) Add(part); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var fixedHeight = 0f;
            for (var i = 0; i < 3; i++)
            {
                Children[i].Measure(available, text);
                if (Children[i].IsVisible) fixedHeight += Children[i].DesiredSize.Height + 6;
            }
            Children[5].Measure(available, text);
            var error = (Text)Children[5]; var errorHeight = error.Value.Length == 0 ? 0 : Math.Min(60, error.DesiredSize.Height);
            Children[3].Measure(new(available.Width, Math.Max(0, available.Height - fixedHeight - errorHeight)), text);
            Children[4].Measure(default, text);
            return new(Math.Min(280, available.Width), Math.Min(available.Height, fixedHeight + Children[3].DesiredSize.Height + errorHeight));
        }
        protected override void ArrangeCore(Rect area)
        {
            var y = area.Y; var bottom = area.Y + area.Height;
            for (var i = 0; i < 3; i++)
            {
                var part = Children[i]; var height = part.IsVisible ? Math.Min(part.DesiredSize.Height, Math.Max(0, bottom - y)) : 0;
                part.Arrange(new(area.X, y, area.Width, height)); if (height > 0) y += height + 6;
            }
            var error = (Text)Children[5]; var errorHeight = error.Value.Length == 0 ? 0 : Math.Min(60, error.DesiredSize.Height);
            Children[3].Arrange(new(area.X, y, area.Width, Math.Max(0, bottom - y - errorHeight)));
            Children[4].Arrange(new(area.X, bottom, 0, 0));
            error.Arrange(new(area.X, bottom - errorHeight, area.Width, errorHeight));
        }
    }

    private sealed class ViewportLayout : ElementList
    {
        public ViewportLayout(Element canvas, Element title)
        {
            Add(canvas);
            Add(title);
        }

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
