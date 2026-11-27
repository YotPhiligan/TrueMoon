using System.Linq.Expressions;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;

namespace TrueMoon.Aether.Editor.UI.Editing;

public sealed partial class EditorInspectorView
{
    private const string TexturePrefix = "editor.textures";
    private static readonly RichTextEditorOptions TextureEditable = new() { AcceptsReturn = false };
    private static readonly RichTextEditorOptions TextureReadOnly = TextureEditable with { IsReadOnly = true };
    private readonly TextureContext _textureContext = new();
    private readonly List<InspectorTexture> _projectTextureRows = [];
    private IReadOnlyList<InspectorTexture>? _shownProjectTextures;
    private long _shownTextureGeneration;
    private long _textureRowGeneration;
    private IEditorTexturesModel? _shownTextureModel;
    private InspectorTexture? _shownProjectTexture;
    private Border TexturesPart => GetRequiredPart<Border>("TexturesPanel");
    private RichTextEditor TextureName => GetRequiredPart<RichTextEditor>("TextureName");
    private RichTextEditor TexturePath => GetRequiredPart<RichTextEditor>("TexturePath");
    internal static ViewStyleRole<RichTextEditor> TextureMetadataRole { get; } = new("InspectorTextureMetadata");

    private Element BuildTextures(IUiBuildContext<IEditorInspectorViewModel> ui) => ui.Border(ui.VStack(
        ui.Create(MaterialHeaderFactory.Instance).Named("TexturesHeader").WithRole(HeaderRole).SemanticId(TexturePrefix)
            .Value("Текстуры").OnClick(() => { if (CurrentTextures is { } model) model.IsOpen = !model.IsOpen; }),
        ui.RichTextEditor(TextureReadOnly).Named("TexturesStatus").SemanticId(TexturePrefix + ".status")
            .WithRole(MaterialStatusRole).Configure(ConfigureTextureText),
        ui.Text("Есть несохранённая правка текстуры").WithRole(MetadataRole).SemanticId(TexturePrefix + ".draft")
            .Bind(UiProperties.Visible, _textureContext, model => model.HasDraft),
        ui.VStack(
            ui.HStack(
                TextureCommand(ui, "Новая текстура", "new", model => model.CanCreate, model => model.New()).Fill(),
                TextureCommand(ui, "Обновить текстуры", "reload", model => model.CanReload, model => model.Reload()).Fill())
                .WithRole(RowRole),
            ui.Text("Нет авторских текстур").Named("TexturesEmpty").WithRole(MetadataRole),
            ui.ScrollViewer(ui.VStack().Named("TexturesItems").WithRole(StackRole))
                .Named("TexturesList").WithRole(MaterialListRole).Configure(ConfigureMaterialList),
            ui.VStack(
                ui.RichTextEditor(TextureReadOnly).Named("TextureMetadata").SemanticId(TexturePrefix + ".metadata")
                    .WithRole(TextureMetadataRole).Configure(ConfigureTextureText),
                ui.Text("Имя").WithRole(MetadataRole),
                TextureInput(ui, "TextureName", "name", "Имя текстуры", model => model.Name),
                ui.Text("Путь к PNG от корня проекта").WithRole(MetadataRole),
                TextureInput(ui, "TexturePath", "path", "Путь к исходному PNG", model => model.SourcePath),
                TextureCommand(ui, "Импортировать PNG", "import", model => model.CanImport, model => model.Import()),
                TextureCommand(ui, "Переименовать текстуру", "rename", model => model.CanRename, model => model.Rename()),
                TextureCommand(ui, "Заменить источник PNG", "replace", model => model.CanReplace, model => model.ReplaceSource()),
                TextureCommand(ui, "Отменить правку текстуры", "cancel", model => model.CanCancel, model => model.Cancel()))
                .WithRole(StackRole).Bind(UiProperties.Visible, _textureContext, model => model.HasEditor))
            .WithRole(StackRole).Bind(UiProperties.Visible, _textureContext, model => model.IsOpen))
        .WithRole(StackRole)).Named("TexturesPanel").WithRole(SectionRole).Configure(panel => panel.IsVisible = false);

    private UiControl<Button, IEditorInspectorViewModel> TextureCommand(IUiBuildContext<IEditorInspectorViewModel> ui,
        string label, string suffix, Expression<Func<IEditorTexturesModel, bool>> enabled, Action<IEditorTexturesModel> action) =>
        ui.Button(label).WithRole(ActionRole).SemanticId(TexturePrefix + "." + suffix)
            .Bind(UiProperties.Enabled, _textureContext, enabled)
            .OnClick(() => { if (CurrentTextures is { } model) action(model); });

    private UiControl<RichTextEditor, IEditorInspectorViewModel> TextureInput(IUiBuildContext<IEditorInspectorViewModel> ui,
        string part, string suffix, string label, Expression<Func<IEditorTexturesModel, string>> value) =>
        ui.RichTextEditor(TextureReadOnly).Named(part).WithRole(MaterialNameRole).SemanticId(TexturePrefix + "." + suffix)
            .Configure(input => { ConfigureTextureText(input); input.AccessibleName = label; input.ColorRole = EditorColorRole.Text; })
            .BindTwoWay(RichTextEditor.TextProperty, _textureContext, value);

    private static void ConfigureTextureText(RichTextEditor input) { input.MinWidth = 0; input.MinHeight = 0; }

    private IEditorTexturesModel? CurrentTextures => ReferenceEquals(DataContext, _presentedContext) &&
        _textureContext.DataContext is { IsAvailable: true } model && ReferenceEquals(DataContext?.Textures, model) ? model : null;

    private void PrepareTextures()
    {
        GetRequiredPart<VStack>("TexturesItems").BindItems(_projectTextureRows, CreateProjectTextureRow,
            item => (item.Id, _textureRowGeneration),
            (row, item) => ((TextureActionRow)row).Present(item, item.Id == _shownProjectTexture?.Id));
        TexturesPart.ObservePropertyChanged(_textureContext, (_, change) =>
        {
            if (change.PropertyName is null or "" or nameof(IEditorTexturesModel.IsAvailable) or nameof(IEditorTexturesModel.IsOpen) or
                nameof(IEditorTexturesModel.Selected) or nameof(IEditorTexturesModel.Items) or nameof(IEditorTexturesModel.StatusText) or
                nameof(IEditorTexturesModel.CanSelect) or nameof(IEditorTexturesModel.CanEditName) or nameof(IEditorTexturesModel.CanEditSource))
                SynchronizeTextures();
        }, SynchronizeTextures, UiSynchronization.AfterTreeChange);
    }

    private Element CreateProjectTextureRow(InspectorTexture item)
    {
        var generation = _textureRowGeneration;
        return new TextureActionRow(item, item.Id == _shownProjectTexture?.Id, row =>
        {
            if (generation == _textureRowGeneration && CurrentTextures is { CanSelect: true } current)
                current.Select(row.Id);
        });
    }

    private void SynchronizeTextures()
    {
        var model = _textureContext.DataContext;
        TexturesPart.IsVisible = model?.IsAvailable == true;
        GetRequiredPart<ComponentHeader>("TexturesHeader").Expanded = model?.IsOpen == true;
        var status = GetRequiredPart<RichTextEditor>("TexturesStatus");
        status.Text = model?.StatusText.ReplaceLineEndings("\n") ?? "";
        status.IsVisible = status.Text.Length != 0;
        var items = model?.Items;
        var selected = model?.Selected;
        var rows = GetRequiredPart<VStack>("TexturesItems");
        var refreshRows = _shownTextureGeneration != _presentationGeneration || !ReferenceEquals(_shownTextureModel, model);
        if (refreshRows) _textureRowGeneration++;
        _shownTextureGeneration = _presentationGeneration;
        _shownTextureModel = model;
        var selectionChanged = selected?.Id != _shownProjectTexture?.Id;
        var metadataChanged = !Equals(selected, _shownProjectTexture);
        _shownProjectTexture = selected;
        rows.IsEnabled = model?.CanSelect == true;
        if (!ReferenceEquals(_shownProjectTextures, items))
        {
            _shownProjectTextures = items;
            _projectTextureRows.Clear();
            if (items != null) _projectTextureRows.AddRange(items);
            refreshRows = true;
        }
        if (refreshRows) rows.RefreshItems();
        if (selectionChanged)
            for (var i = 0; i < rows.Items.Count; i++)
            {
                var row = (TextureActionRow)rows.Items[i];
                row.Highlight(row.Id == selected?.Id);
            }
        GetRequiredPart<Text>("TexturesEmpty").IsVisible = _projectTextureRows.Count == 0 && status.Text.Length == 0;
        GetRequiredPart<ScrollViewer>("TexturesList").IsVisible = _projectTextureRows.Count != 0;
        var metadata = GetRequiredPart<RichTextEditor>("TextureMetadata");
        metadata.IsVisible = selected != null;
        if (metadataChanged)
            metadata.Text = selected == null ? "" :
                $"{selected.Width} × {selected.Height} · sRGB\nID: {selected.Id:D}\nИсточник: {selected.SourcePath}\nРевизия: {selected.Revision}";
        TextureName.Options = model?.CanEditName == true ? TextureEditable : TextureReadOnly;
        TexturePath.Options = model?.CanEditSource == true ? TextureEditable : TextureReadOnly;
    }

    private bool TextureTextHasFocus() => IsAttached && TexturesPart.IsVisible && (TextureName.IsFocused || TexturePath.IsFocused);

    private sealed class TextureContext : IDataContext<IEditorTexturesModel>
    {
        private IEditorTexturesModel? _current;
        public IEditorTexturesModel? DataContext
        {
            get => _current;
            set
            {
                if (ReferenceEquals(_current, value)) return;
                _current = value;
                DataContextChanged?.Invoke();
            }
        }
        public event Action? DataContextChanged;
    }

    private sealed class TextureActionRow : View
    {
        private InspectorTexture _item;
        private bool _selected;
        private Button? _button;

        internal Guid Id => _item.Id;

        internal TextureActionRow(InspectorTexture item, bool selected, Action<InspectorTexture> select)
        {
            _item = item;
            _selected = selected;
            Content(ui => ui.Button().SemanticId($"{TexturePrefix}.item.{item.Id:N}").OnClick(() => select(_item)).Configure(button =>
            {
                button.Style = EditorInspectorViewStyle.MaterialRowAction;
                _button = button;
                Present(_item, _selected);
            }));
        }

        internal void Present(InspectorTexture item, bool selected)
        {
            _item = item;
            _selected = selected;
            if (_button == null) return;
            _button.Value = $"{item.Name.ReplaceLineEndings(" ")} · {item.Width} × {item.Height} · {item.Id.ToString("N")[..8]}";
            _button.AccessibleName = _button.Value;
            _button.Tone = selected ? ControlTone.Accent : ControlTone.Neutral;
        }

        internal void Highlight(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            if (_button != null) _button.Tone = selected ? ControlTone.Accent : ControlTone.Neutral;
        }
    }
}
