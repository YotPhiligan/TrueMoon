using System.Runtime.CompilerServices;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private readonly TexturesModel _textures;

    /// <inheritdoc />
    public IEditorTexturesModel Textures => _textures;

    private sealed class TexturesModel(EditorInspectorViewModel inspector) : ViewModel, IEditorTexturesModel
    {
        private const string Loading = "Загрузка текстур…";
        private const string InvalidName = "Введите имя текстуры от 1 до 256 символов.";
        private const string InvalidPath = "Укажите путь к PNG относительно корня проекта, без перехода в родительский каталог.";
        private ProjectSession? _project;
        private ProjectTextureCatalogSnapshot? _snapshot, _draftSnapshot;
        private Operation? _operation;
        private InspectorTexture? _selected;
        private Guid? _previous;
        private IReadOnlyList<InspectorTexture> _items = [];
        private string _name = "", _committedName = "", _path = "", _status = "", _error = "", _loadError = "";
        private bool _open, _available, _hasEditor, _new, _draft, _readRequested, _validPath;
        private bool _canSelect, _canEditName, _canEditSource, _canCreate, _canImport, _canRename, _canReplace, _canReload, _canCancel;
        private EditorDocumentCommands Commands => inspector._commands;
        private enum ActionKind { Read, Import, Rename, Replace }
        private sealed record Operation(Task<ProjectTextureCatalogSnapshot> Work, ActionKind Kind, ProjectTextureCatalogSnapshot? Expected = null);

        public bool IsAvailable => _available;
        public bool IsOpen
        {
            get => _open;
            set
            {
                inspector.VerifyOwner();
                if (!SetProperty(ref _open, value)) return;
                if (value) TryInitialRead(demanded: true);
                Update();
            }
        }
        public bool HasEditor => _hasEditor;
        public bool HasDraft => _draft;
        public bool CanSelect => _canSelect;
        public bool CanEditName => _canEditName;
        public bool CanEditSource => _canEditSource;
        public bool CanCreate => _canCreate;
        public bool CanImport => _canImport;
        public bool CanRename => _canRename;
        public bool CanReplace => _canReplace;
        public bool CanReload => _canReload;
        public bool CanCancel => _canCancel;
        public string StatusText => _status;
        public string Name { get => _name; set => SetInput(ref _name, value, CanEditName); }
        public string SourcePath { get => _path; set => SetInput(ref _path, value, CanEditSource); }
        public InspectorTexture? Selected => _selected;
        public IReadOnlyList<InspectorTexture> Items => _items;
        private bool NameChanged => _name != _committedName;
        private bool PathChanged => _path != (_selected?.SourcePath ?? "");
        private bool ValidName => _name.AsSpan().Trim().Length is > 0 and <= ProjectTextureCatalog.MaxNameLength;
        internal bool HasPendingOperation => _operation != null;
        internal string? CatalogDiagnostic => _project == null || _snapshot != null ? null : _loadError.Length != 0 ? _loadError : Loading;

        internal void Refresh(ProjectSession? project)
        {
            if (project?.Documents?.HasTextureCatalog != true) project = null;
            if (!ReferenceEquals(project, _project))
            {
                if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Texture, false);
                _project = project;
                _operation = null;
                _snapshot = _draftSnapshot = null;
                _previous = null;
                _readRequested = false;
                _error = _loadError = "";
                SetProperty(ref _open, false, nameof(IsOpen));
                SetProperty(ref _items, Array.Empty<InspectorTexture>(), nameof(Items));
                Show(null);
                SetProperty(ref _draft, false, nameof(HasDraft));
            }
            SetProperty(ref _available, project != null, nameof(IsAvailable));
            if (project?.Documents is { } documents)
            {
                if (_operation is { Work.IsCompleted: true } operation)
                {
                    _operation = null;
                    try
                    {
                        var committed = operation.Work.GetAwaiter().GetResult();
                        documents.PublishTextureCatalog(committed);
                        Publish(documents.TextureSnapshot!);
                        _error = _loadError = "";
                        Accept(operation);
                    }
                    catch (Exception error)
                    {
                        _error = error is OperationCanceledException ? "Операция с текстурами отменена." : error.Message;
                        if (operation.Kind == ActionKind.Read) _loadError = _error;
                    }
                }
                else if (documents.TextureSnapshot is { } shared && !ReferenceEquals(shared, _snapshot))
                {
                    Publish(shared);
                    if (!_draft && _selected != null) Show(Find(_selected.Id));
                }
            }
        }

        private void Accept(Operation operation)
        {
            if (operation.Kind == ActionKind.Read)
            {
                if (_selected != null) Show(Find(_selected.Id));
                return;
            }
            _previous = null;
            var id = operation.Kind == ActionKind.Import
                ? FindImportedId(operation.Expected!.Textures, _snapshot!.Textures)
                : _selected!.Id;
            var row = Find(id)!;
            var nameDraft = operation.Kind == ActionKind.Replace && NameChanged ? _name : null;
            var pathDraft = operation.Kind == ActionKind.Rename && PathChanged ? _path : null;
            Show(row, name: nameDraft, path: pathDraft);
            _draftSnapshot = _snapshot;
            UpdateDraft();
        }

        private static Guid FindImportedId(IReadOnlyList<ProjectTexture> before, IReadOnlyList<ProjectTexture> after)
        {
            // Receipts are in GUID order and a successful import adds exactly one row.
            for (var i = 0; i < before.Count; i++)
                if (before[i].Id != after[i].Id) return after[i].Id;
            return after[^1].Id;
        }

        private void Publish(ProjectTextureCatalogSnapshot snapshot)
        {
            if (ReferenceEquals(_snapshot, snapshot)) return;
            _snapshot = snapshot;
            SetProperty(ref _items, Array.AsReadOnly(snapshot.Textures.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.Id).Select(static row => new InspectorTexture(row.Id, row.Name, row.SourcePath,
                    row.Width, row.Height, row.Revision.ToString())).ToArray()), nameof(Items));
        }

        private InspectorTexture? Find(Guid id)
        {
            foreach (var item in _items) if (item.Id == id) return item;
            return null;
        }

        internal void TryInitialRead(bool demanded)
        {
            if ((demanded || _open) && !_readRequested && _snapshot == null) StartRead();
        }

        private void StartRead()
        {
            if (_project == null || _operation != null || inspector._materials.HasPendingOperation || inspector._meshes.HasPendingOperation ||
                !Commands.CanExecute(DocumentCommand.ReadTextureCatalog)) return;
            _readRequested = true;
            _error = _loadError = "";
            _operation = new(Commands.ReadTextureCatalogAsync(_project), ActionKind.Read);
        }

        public void Select(Guid id)
        {
            inspector.VerifyOwner();
            if (!CanSelect || _selected?.Id == id || Find(id) is not { } row) return;
            _error = "";
            Show(row);
            Update();
        }

        public void New()
        {
            inspector.VerifyOwner();
            if (!CanCreate || _project == null || !Commands.SetAssetDraft(_project, AssetDraftKind.Texture, true)) return;
            _error = "";
            _previous = _selected?.Id;
            Show(null, isNew: true);
            UpdateDraft();
            Update();
        }

        public void Import()
        {
            inspector.VerifyOwner();
            if (!CanImport || _project == null || _draftSnapshot is not { } expected) return;
            StartWrite(new(Commands.ImportTextureAsync(_project, expected, _name, _path), ActionKind.Import, expected));
        }

        public void Rename()
        {
            inspector.VerifyOwner();
            if (!CanRename || _project == null || _selected == null || _draftSnapshot is not { } expected) return;
            StartWrite(new(Commands.RenameTextureAsync(_project, expected, _selected.Id, _name), ActionKind.Rename));
        }

        public void ReplaceSource()
        {
            inspector.VerifyOwner();
            if (!CanReplace || _project == null || _selected == null || (_draftSnapshot ?? _snapshot) is not { } expected) return;
            StartWrite(new(Commands.ReplaceTextureSourceAsync(_project, expected, _selected.Id, _path), ActionKind.Replace));
        }

        private void StartWrite(Operation operation)
        {
            _error = "";
            _operation = operation;
            Update();
        }

        public void Reload()
        {
            inspector.VerifyOwner();
            if (!CanReload) return;
            StartRead();
            Update();
        }

        public void Cancel()
        {
            inspector.VerifyOwner();
            if (!CanCancel) return;
            _error = "";
            var id = _new ? _previous : _selected?.Id;
            _previous = null;
            Show(id is { } selected ? Find(selected) : null);
            UpdateDraft();
            Update();
        }

        private void Show(InspectorTexture? row, bool isNew = false, string? name = null, string? path = null)
        {
            _new = isNew;
            _committedName = row?.Name.ReplaceLineEndings("\n") ?? "";
            SetProperty(ref _selected, row, nameof(Selected));
            SetProperty(ref _hasEditor, isNew || row != null, nameof(HasEditor));
            SetProperty(ref _name, name ?? _committedName, nameof(Name));
            SetProperty(ref _path, path ?? (isNew ? "" : row?.SourcePath ?? ""), nameof(SourcePath));
            _validPath = IsValidPath(_path);
        }

        private void SetInput(ref string field, string value, bool editable, [CallerMemberName] string? property = null)
        {
            inspector.VerifyOwner();
            if (!editable || value == field || _project == null || !Commands.SetAssetDraft(_project, AssetDraftKind.Texture, true)) return;
            field = value;
            if (property == nameof(SourcePath)) _validPath = IsValidPath(value);
            _error = "";
            UpdateDraft();
            Update();
            OnPropertyChanged(property);
        }

        private static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { ProjectTextureCatalog.NormalizeSourcePath(path); return true; }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or NotSupportedException) { return false; }
        }

        private void UpdateDraft()
        {
            var dirty = _new || NameChanged || PathChanged;
            if (dirty) _draftSnapshot ??= _snapshot;
            else
            {
                _draftSnapshot = null;
                if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Texture, false);
                if (_selected != null)
                {
                    var current = Find(_selected.Id);
                    if (current != _selected) Show(current);
                }
            }
            SetProperty(ref _draft, dirty, nameof(HasDraft));
        }

        internal void Update()
        {
            var idle = _available && _operation == null && !inspector._materials.HasPendingOperation && !inspector._meshes.HasPendingOperation;
            var canWrite = idle && Commands.CanExecute(DocumentCommand.RenameTexture);
            var canDecode = _project?.Documents?.CanEditTextureCatalog == true;
            SetProperty(ref _canSelect, canWrite && !_draft, nameof(CanSelect));
            SetProperty(ref _canEditName, canWrite && _hasEditor, nameof(CanEditName));
            SetProperty(ref _canEditSource, canWrite && _hasEditor && canDecode, nameof(CanEditSource));
            SetProperty(ref _canCreate, canWrite && canDecode && !_draft && _snapshot != null, nameof(CanCreate));
            SetProperty(ref _canImport, canWrite && canDecode && _new && ValidName && _validPath, nameof(CanImport));
            SetProperty(ref _canRename, canWrite && !_new && _selected != null && NameChanged && ValidName, nameof(CanRename));
            SetProperty(ref _canReplace, canWrite && canDecode && !_new && _selected != null && _validPath, nameof(CanReplace));
            SetProperty(ref _canReload, idle && !_draft && Commands.CanExecute(DocumentCommand.ReadTextureCatalog), nameof(CanReload));
            SetProperty(ref _canCancel, _operation == null && _draft, nameof(CanCancel));
            var status = _operation is { } operation ? operation.Kind == ActionKind.Read ? Loading : "Сохранение текстуры…" :
                _error.Length != 0 ? _error : _draft && (_new || NameChanged) && !ValidName ? InvalidName :
                _draft && (_new || PathChanged) && !_validPath ? InvalidPath :
                (_open || inspector._baseline != null || inspector._materials.IsOpen) && CatalogDiagnostic is { } load ? load :
                canDecode ? "" : "Импорт и замена PNG недоступны в этой конфигурации.";
            SetProperty(ref _status, _available ? status : "", nameof(StatusText));
        }

        internal void Release()
        {
            if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Texture, false);
            ClearPropertyChangedSubscribers();
        }
    }
}
