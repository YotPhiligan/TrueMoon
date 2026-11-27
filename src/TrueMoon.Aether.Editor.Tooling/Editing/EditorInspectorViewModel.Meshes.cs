using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private readonly MeshesModel _meshes;

    /// <inheritdoc />
    public IEditorMeshesModel Meshes => _meshes;

    private sealed class MeshesModel(EditorInspectorViewModel inspector) : ViewModel, IEditorMeshesModel
    {
        private const string Loading = "Загрузка мешей…";
        private ProjectSession? _project;
        private ProjectMeshCatalogSnapshot? _snapshot, _draftSnapshot;
        private Operation? _operation;
        private InspectorMesh? _selected;
        private Guid? _previous, _sourceId;
        private MeshSourceChoice? _selector;
        private IReadOnlyList<ProjectModelCatalogItem>? _knownSources;
        private IReadOnlyList<ProjectMeshGeometryChoice> _choices = [];
        private IReadOnlyList<InspectorMeshChoice> _sources = [], _geometries = [];
        private IReadOnlyList<InspectorMesh> _items = [];
        private string _name = "", _committedName = "", _status = "", _error = "", _loadError = "";
        private bool _open, _available, _new, _draft, _hasEditor, _readRequested, _sourcesRequested;
        private bool _canSelect, _canEditName, _canEditGeometry, _canCreateDraft, _canCreate, _canRename, _canReplace, _canReload, _canCancel;
        private EditorDocumentCommands Commands => inspector._commands;
        private sealed record Operation(Task Work, Action Accept, string Status, bool CatalogRead);

        public bool IsAvailable => _available;
        public bool IsOpen
        {
            get => _open;
            set
            {
                inspector.VerifyOwner();
                if (!SetProperty(ref _open, value)) return;
                if (value) TryInitialRead(false);
                Update();
            }
        }
        public bool HasEditor => _hasEditor;
        public bool HasDraft => _draft;
        public bool IsGeometryAvailable => _project?.Documents?.CanEditMeshCatalog == true;
        public bool CanSelect => _canSelect;
        public bool CanEditName => _canEditName;
        public bool CanEditGeometry => _canEditGeometry;
        public bool CanCreateDraft => _canCreateDraft;
        public bool CanCreate => _canCreate;
        public bool CanRename => _canRename;
        public bool CanReplace => _canReplace;
        public bool CanReload => _canReload;
        public bool CanCancel => _canCancel;
        public string StatusText => _status;
        public string Name
        {
            get => _name;
            set
            {
                inspector.VerifyOwner();
                if (!CanEditName || value == _name || !BeginDraft()) return;
                _name = value;
                _error = "";
                UpdateDraft();
                Update();
                OnPropertyChanged(nameof(Name));
            }
        }
        public InspectorMesh? Selected => _selected;
        public Guid? SelectedSource => _sourceId;
        public Guid? SelectedGeometry
        {
            get
            {
                if (_selector == null || _selector.SourceId != _sourceId) return null;
                for (var i = 0; i < _choices.Count; i++)
                    if (_choices[i].Selector == _selector) return _selector.PublishedMeshId;
                return null;
            }
        }
        public IReadOnlyList<InspectorMesh> Items => _items;
        public IReadOnlyList<InspectorMeshChoice> Sources => _sources;
        public IReadOnlyList<InspectorMeshChoice> Geometries => _geometries;
        private bool NameChanged => _name != _committedName;
        private bool ValidName => _name.AsSpan().Trim().Length is > 0 and <= ProjectMeshCatalog.MaxNameLength;
        internal bool HasPendingOperation => _operation != null;
        internal string? CatalogDiagnostic => _project == null || _snapshot != null ? null : _loadError.Length == 0 ? Loading : _loadError;

        internal void Refresh(ProjectSession? project, IReadOnlyList<ProjectModelCatalogItem>? registered)
        {
            if (project?.Documents?.HasMeshCatalog != true) project = null;
            if (!ReferenceEquals(project, _project))
            {
                if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Mesh, false);
                _project = project;
                _operation = null;
                _snapshot = _draftSnapshot = null;
                _previous = null;
                _readRequested = _sourcesRequested = false;
                _knownSources = null;
                _error = _loadError = "";
                SetProperty(ref _open, false, nameof(IsOpen));
                SetProperty(ref _items, Array.Empty<InspectorMesh>(), nameof(Items));
                SetProperty(ref _sources, Array.Empty<InspectorMeshChoice>(), nameof(Sources));
                Show(null);
                SetProperty(ref _draft, false, nameof(HasDraft));
            }
            SetProperty(ref _available, project != null, nameof(IsAvailable));
            if (project?.Documents is not { } documents) return;
            if (_operation is { Work.IsCompleted: true } operation)
            {
                _operation = null;
                try
                {
                    operation.Work.GetAwaiter().GetResult();
                    _error = "";
                    operation.Accept();
                }
                catch (Exception error)
                {
                    _error = error is OperationCanceledException ? "Операция с мешами отменена." : error.Message;
                    if (operation.CatalogRead) _loadError = _error;
                }
            }
            if (documents.MeshSnapshot is { } shared && !ReferenceEquals(shared, _snapshot))
            {
                Publish(shared);
                if (!_draft && _selected != null) Show(Find(_selected.Id));
            }
            if (registered != null && !ReferenceEquals(registered, _knownSources))
            {
                _knownSources = registered;
                PublishSources(registered);
            }
        }

        internal void TryInitialRead(bool hasScene)
        {
            if ((hasScene || _open) && !_readRequested && _snapshot == null) StartRead(explicitReload: false);
        }

        internal void RefreshEditor()
        {
            if (_open && _hasEditor && !_sourcesRequested && _project?.Documents?.CanEditMeshCatalog == true &&
                CanStart(DocumentCommand.ReadMeshSource))
            {
                _sourcesRequested = true;
                Run(Commands.ReadMeshSourcesAsync(_project), PublishSources, "Загрузка источников мешей…");
            }
            Update();
        }

        private bool CanStart(DocumentCommand command) => _project != null && _operation == null &&
            !inspector._materials.HasPendingOperation && !inspector._textures.HasPendingOperation && Commands.CanExecute(command);

        private void Run<T>(Task<T> task, Action<T> accept, string status, bool catalogRead = false)
        {
            _error = "";
            _operation = new(task, () => accept(task.GetAwaiter().GetResult()), status, catalogRead);
            Update();
        }

        private void StartRead(bool explicitReload)
        {
            if (!CanStart(DocumentCommand.ReadMeshCatalog)) return;
            _readRequested = true;
            _loadError = "";
            Run(Commands.ReadMeshCatalogAsync(_project!), receipt =>
            {
                PublishReceipt(receipt);
                if (_selected != null) Show(Find(_selected.Id));
                if (explicitReload) _sourcesRequested = false;
            }, Loading, catalogRead: true);
        }

        private void PublishReceipt(ProjectMeshCatalogSnapshot receipt)
        {
            var documents = _project!.Documents!;
            documents.PublishMeshCatalog(receipt);
            Publish(documents.MeshSnapshot!);
        }

        private void Publish(ProjectMeshCatalogSnapshot snapshot)
        {
            if (ReferenceEquals(snapshot, _snapshot)) return;
            _snapshot = snapshot;
            SetProperty(ref _items, Array.AsReadOnly(snapshot.Meshes.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.Id).Select(static row => new InspectorMesh(row.Id, row.Name, row.Source.SourcePath,
                    row.Source.MeshLocator, row.Bounds, row.HasUv0, [.. row.Slots.Select(slot => new InspectorMeshSlot(slot.Id, slot.Name))])).ToArray()), nameof(Items));
        }

        private void PublishSources(IReadOnlyList<ProjectModelCatalogItem> sources)
        {
            _sourcesRequested = true;
            SetProperty(ref _sources, Array.AsReadOnly(sources.Select(static source =>
                new InspectorMeshChoice(source.SourceId, $"{source.Name} · {source.SourcePath}")).ToArray()), nameof(Sources));
        }

        private InspectorMesh? Find(Guid id)
        {
            foreach (var item in _items) if (item.Id == id) return item;
            return null;
        }

        public void Select(Guid id)
        {
            inspector.VerifyOwner();
            if (!CanSelect || _selected?.Id == id || Find(id) is not { } row) return;
            _error = "";
            Show(row);
            RefreshEditor();
        }

        public void New()
        {
            inspector.VerifyOwner();
            if (!CanCreateDraft || !BeginDraft()) return;
            _error = "";
            _previous = _selected?.Id;
            Show(null, isNew: true);
            UpdateDraft();
            RefreshEditor();
        }

        public void SelectSource(InspectorMeshChoice source)
        {
            inspector.VerifyOwner();
            if (!CanEditGeometry || !_sources.Any(row => ReferenceEquals(row, source)) || !CanStart(DocumentCommand.ReadMeshSource)) return;
            // Keep a previously selected exact candidate until a different geometry is explicitly chosen.
            SetProperty(ref _sourceId, source.Id, nameof(SelectedSource));
            SetGeometries([]);
            Run(Commands.ReadMeshSourceAsync(_project!, source.Id), SetGeometries, "Чтение геометрии модели…");
        }

        private void SetGeometries(IReadOnlyList<ProjectMeshGeometryChoice> choices)
        {
            _choices = choices;
            SetProperty(ref _geometries, Array.AsReadOnly(choices.Select(static choice =>
                new InspectorMeshChoice(choice.Selector.PublishedMeshId, $"{choice.Name} · {choice.MeshLocator}")).ToArray()), nameof(Geometries));
            OnPropertyChanged(nameof(SelectedGeometry));
        }

        public void SelectGeometry(InspectorMeshChoice geometry)
        {
            inspector.VerifyOwner();
            if (!CanEditGeometry || !CanStart(DocumentCommand.ReadMeshSource)) return;
            for (var i = 0; i < _geometries.Count; i++)
            {
                if (!ReferenceEquals(_geometries[i], geometry) || !BeginDraft()) continue;
                _error = "";
                _selector = _choices[i].Selector;
                OnPropertyChanged(nameof(SelectedGeometry));
                UpdateDraft();
                Update();
                return;
            }
        }

        public void Create()
        {
            inspector.VerifyOwner();
            if (!CanCreate || !CanStart(DocumentCommand.CreateMesh) || _draftSnapshot is not { } expected || _selector is not { } selector) return;
            Run(Commands.CreateMeshAsync(_project!, expected, selector, _name), receipt =>
            {
                var id = receipt.Meshes.First(row => expected.Find(row.Id) == null).Id;
                AcceptWrite(receipt, id, preserveName: false, preserveGeometry: false);
            }, "Создание меша…");
        }

        public void Rename()
        {
            inspector.VerifyOwner();
            if (!CanRename || !CanStart(DocumentCommand.RenameMesh) || _draftSnapshot is not { } expected || _selected is not { } row) return;
            Run(Commands.RenameMeshAsync(_project!, expected, row.Id, _name), receipt =>
                AcceptWrite(receipt, row.Id, preserveName: false, preserveGeometry: true), "Переименование меша…");
        }

        public void ReplaceGeometry()
        {
            inspector.VerifyOwner();
            if (!CanReplace || !CanStart(DocumentCommand.ReplaceMeshGeometry) || _draftSnapshot is not { } expected ||
                _selected is not { } row || _selector is not { } selector) return;
            Run(Commands.ReplaceMeshGeometryAsync(_project!, expected, row.Id, selector), receipt =>
                AcceptWrite(receipt, row.Id, preserveName: true, preserveGeometry: false), "Замена геометрии…");
        }

        private void AcceptWrite(ProjectMeshCatalogSnapshot receipt, Guid id, bool preserveName, bool preserveGeometry)
        {
            var name = preserveName && NameChanged ? _name : null;
            PublishReceipt(receipt);
            _previous = null;
            Show(Find(id), name: name, preserveGeometry: preserveGeometry);
            _draftSnapshot = _snapshot;
            UpdateDraft();
        }

        public void Reload()
        {
            inspector.VerifyOwner();
            if (CanReload) StartRead(explicitReload: true);
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

        private void Show(InspectorMesh? row, bool isNew = false, string? name = null, bool preserveGeometry = false)
        {
            _new = isNew;
            if (!preserveGeometry)
            {
                _selector = null;
                SetProperty(ref _sourceId, null, nameof(SelectedSource));
                SetGeometries([]);
            }
            _committedName = row?.Name.ReplaceLineEndings("\n") ?? "";
            SetProperty(ref _selected, row, nameof(Selected));
            SetProperty(ref _hasEditor, isNew || row != null, nameof(HasEditor));
            SetProperty(ref _name, name ?? _committedName, nameof(Name));
        }

        private bool BeginDraft() => _project != null && Commands.SetAssetDraft(_project, AssetDraftKind.Mesh, true);

        private void UpdateDraft()
        {
            var dirty = _new || NameChanged || _selector != null;
            if (dirty) _draftSnapshot ??= _snapshot;
            else
            {
                _draftSnapshot = null;
                if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Mesh, false);
                if (_selected != null && Find(_selected.Id) is var current && !ReferenceEquals(current, _selected)) Show(current);
            }
            SetProperty(ref _draft, dirty, nameof(HasDraft));
        }

        private void Update()
        {
            var canWrite = CanStart(DocumentCommand.RenameMesh);
            var canEdit = _project?.Documents?.CanEditMeshCatalog == true;
            var hasCurrentGeometry = SelectedGeometry.HasValue;
            SetProperty(ref _canSelect, canWrite && !_draft, nameof(CanSelect));
            SetProperty(ref _canEditName, canWrite && _hasEditor, nameof(CanEditName));
            SetProperty(ref _canEditGeometry, canWrite && _hasEditor && canEdit, nameof(CanEditGeometry));
            SetProperty(ref _canCreateDraft, canWrite && canEdit && !_draft && _snapshot != null, nameof(CanCreateDraft));
            SetProperty(ref _canCreate, canWrite && canEdit && _new && ValidName && hasCurrentGeometry, nameof(CanCreate));
            SetProperty(ref _canRename, canWrite && !_new && _selected != null && NameChanged && ValidName, nameof(CanRename));
            SetProperty(ref _canReplace, canWrite && canEdit && !_new && _selected != null && hasCurrentGeometry, nameof(CanReplace));
            SetProperty(ref _canReload, !_draft && CanStart(DocumentCommand.ReadMeshCatalog), nameof(CanReload));
            SetProperty(ref _canCancel, _operation == null && _draft, nameof(CanCancel));
            var status = _operation?.Status ?? (_error.Length != 0 ? _error : _draft && (_new || NameChanged) && !ValidName ?
                "Введите имя меша от 1 до 256 символов." : (_open || inspector._baseline != null) && CatalogDiagnostic is { } diagnostic ? diagnostic :
                !canEdit ? "Создание и замена геометрии недоступны в этой конфигурации." : "");
            SetProperty(ref _status, _available ? status : "", nameof(StatusText));
        }

        internal void Release()
        {
            if (_project != null) Commands.SetAssetDraft(_project, AssetDraftKind.Mesh, false);
            ClearPropertyChangedSubscribers();
        }
    }
}
