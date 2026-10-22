using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Fluent editor composition borrowing its presentation model and style declaration.</summary>
public sealed class EditorView : View<EditorViewModel>
{
    public static ViewStyleRole<Border> PaneRole { get; } = new("Pane");
    public static ViewStyleRole<VStack> PaneContentRole { get; } = new("PaneContent");
    public static ViewStyleRole<VStack> PaneItemsRole { get; } = new("PaneItems");
    public static ViewStyleRole<Text> PaneTitleRole { get; } = new("PaneTitle");
    public static ViewStyleRole<Button> EntityButtonRole { get; } = new("EntityButton");
    public static ViewStyleRole<PropertyRow> PropertyRowRole { get; } = new("PropertyRow");
    public static ViewStyleRole<Text> PropertyNameRole { get; } = new("PropertyName");
    public static ViewStyleRole<VStack> PropertyRowsRole { get; } = new("PropertyRows");
    private readonly EditorViewModel _model;
    private readonly Dictionary<EditorCommand, Button> _commands = [];
    private readonly Dictionary<string, Button> _entities = new(StringComparer.Ordinal);
    public HStack Toolbar { get; } = HStack.Create();
    public VStack EntityList { get; } = VStack.Create();
    public ContentControl PropertiesPanel { get; } = ContentControl.Create();
    public Text Selection { get; }
    public Text Status { get; }
    public Text Error { get; }
    public Text SceneTitle { get; }
    public Text InspectorHint { get; } = Text.Plain("Свойства сцены · только просмотр");
    public Text ViewportTitle { get; } = Text.Plain("VIEWPORT · Предпросмотр сцены");
    public ContentControl ViewportFrame { get; }
    public EditorViewport Canvas { get; } = new();
    public IReadOnlyDictionary<EditorCommand, Button> Commands => _commands;
    public IReadOnlyDictionary<string, Button> Entities => _entities;

    public EditorView(EditorViewModel model, IViewStyle<EditorView> style)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(style);
        try
        {
            _model = model;
            DataContext = model;
            Selection = Text.Plain().BindText(model, x => x.SelectedName);
            Status = Text.Plain().BindText(model, x => x.StatusText);
            Error = Text.Plain().BindText(model, x => x.ErrorText);
            SceneTitle = Text.Plain(model.Scene.Id);
            AddCommand(EditorCommand.Build, "Собрать", x => x.CanBuild);
            AddCommand(EditorCommand.Start, "Запустить", x => x.CanStart);
            AddCommand(EditorCommand.Play, "Play", x => x.CanPlay);
            AddCommand(EditorCommand.Pause, "Пауза", x => x.CanPause);
            AddCommand(EditorCommand.Step, "Шаг", x => x.CanStep);
            AddCommand(EditorCommand.Stop, "Стоп", x => x.CanStop);
            AddCommand(EditorCommand.Replace, "Применить сборку", x => x.CanReplace);
            AddCommand(EditorCommand.Cancel, "Отмена", x => x.CanCancel);
            Toolbar.Add(Button.Simple("Тема").OnClick(ToggleTheme));
            foreach (var id in model.EntityIds)
            {
                var button = Button.Simple(EditorScene.Name(id)).WithRole(EntityButtonRole)
                    .SetValue(UiProperties.AccessibleName, id).OnClick(() => model.Select(id));
                _entities.Add(id, button);
                EntityList.Add(button);
            }

            var hierarchy = Pane("СЦЕНА", SceneTitle, ScrollViewer.Create().Content(EntityList));
            ViewportFrame = ContentControl.Create().Content(new ViewportLayout(Canvas, ViewportTitle));
            var inspector = Pane("ИНСПЕКТОР", Selection, InspectorHint, PropertiesPanel, Error);
            Content(model, _ => new EditorLayout(Toolbar, hierarchy, ViewportFrame, inspector, Status));
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
        if (change.PropertyName == nameof(EditorViewModel.SelectedId)) Dispatch(UpdateSelection);
        if (change.PropertyName == nameof(EditorViewModel.Properties)) Dispatch(UpdateProperties);
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
            var requested = float.IsNaN(Children[1].Height) ? Children[1].DesiredSize.Height : Children[1].Height;
            var height = Math.Min(requested, area.Height);
            Children[1].Arrange(new(area.X, area.Y, area.Width, height));
            Children[0].Arrange(new(area.X, area.Y + height, area.Width, Math.Max(0, area.Height - height)));
        }
    }
}