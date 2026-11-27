using System.Globalization;
using System.Numerics;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Argentis.Bindings;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

public partial class EditorInspectorViewModel
{
    private readonly MaterialsModel _materials;

    /// <inheritdoc />
    public IEditorMaterialsModel Materials => _materials;

    private sealed class MaterialsModel(EditorInspectorViewModel inspector) : ViewModel, IEditorMaterialsModel
    {
        private const string CopyNotAssigned = "Копия создана, слот не изменён.";
        private const string InvalidFields = "Введите имя и конечные значения R, G и B от 0 до 1.";
        private readonly Dictionary<Guid, History> _histories = [];
        private ProjectSession? _project;
        private ProjectMaterialCatalogSnapshot? _snapshot;
        private ProjectMaterialCatalogSnapshot? _draftSnapshot;
        private SceneRenderAssets? _assets;
        private Operation? _operation;
        private ProjectMaterial? _shown;
        private Guid? _previousMaterial;
        private bool _readRequested, _new, _draft, _readOnly, _open, _available;
        private string _message = "", _loadError = "", _status = "", _name = "", _red = "", _green = "", _blue = "";
        private (string Red, string Green, string Blue) _originalColor;
        private Guid? _texture, _selected;
        private bool _hasDocument, _canEdit, _canCreate, _canReload, _canSave, _canCancel, _canUndo, _canRedo;
        private IReadOnlyList<InspectorRenderAssetChoice> _items = [], _textures = [];
        private EditorDocumentCommands Commands => inspector._commands;

        private enum ActionKind { Read, Save, Undo, Redo, Copy }
        private sealed record Operation(Task<ProjectMaterialCatalogSnapshot> Work, ActionKind Kind,
            ProjectMaterial? Before = null, ProjectMaterial? Value = null, CopyTarget? Target = null);
        private sealed record CopyTarget(ProjectSession Project, Guid Opening, SceneDocument Document, EntityId Entity,
            ComponentId Component, Guid Mesh, Guid Slot, Guid Source, ulong InspectorSelectionRevision, ulong? WorkspaceSelectionRevision);
        private sealed class History
        {
            public readonly List<(ProjectMaterial Before, ProjectMaterial After)> Entries = [];
            public int Cursor;
            public ProjectMaterial Edge => Cursor == 0 ? Entries[0].Before : Entries[Cursor - 1].After;
            public void Add(ProjectMaterial before, ProjectMaterial after)
            {
                Entries.RemoveRange(Cursor, Entries.Count - Cursor);
                Entries.Add((before, after));
                Cursor++;
            }
        }

        public bool IsAvailable => _available;
        public bool IsOpen
        {
            get => _open;
            set
            {
                inspector.VerifyOwner();
                if (!SetProperty(ref _open, value)) return;
                if (value) TryInitialRead(inspector._baseline != null);
                Update();
            }
        }
        public bool HasDocument => _hasDocument;
        public bool HasDraft => _draft;
        public bool IsReadOnly => _readOnly;
        public bool CanEditFields => _canEdit;
        public bool CanCreate => _canCreate;
        public bool CanReload => _canReload;
        public bool CanSave => _canSave;
        public bool CanCancel => _canCancel;
        public bool CanUndo => _canUndo;
        public bool CanRedo => _canRedo;
        public string StatusText => _status;
        public string Name { get => _name; set => SetInput(ref _name, value); }
        public string Red { get => _red; set => SetInput(ref _red, value); }
        public string Green { get => _green; set => SetInput(ref _green, value); }
        public string Blue { get => _blue; set => SetInput(ref _blue, value); }
        public Guid? TextureId { get => _texture; set => SetInput(ref _texture, value); }
        public Guid? SelectedMaterialId => _selected;
        public IReadOnlyList<InspectorRenderAssetChoice> Items => _items;
        public IReadOnlyList<InspectorRenderAssetChoice> Textures => _textures;
        private bool Idle => _project != null && _operation == null && Commands.CanExecute(DocumentCommand.WriteMaterial);
        internal bool HasPendingOperation => _operation != null;
        private History? CurrentHistory => _shown != null && _histories.TryGetValue(_shown.Id, out var branch) ? branch : null;
        private bool TextureAvailable(Guid? id)
        {
            if (!id.HasValue) return true;
            for (var i = 0; i < _textures.Count; i++) if (_textures[i].Id == id.Value) return true;
            return false;
        }
        internal bool CatalogPending => _project != null && _snapshot == null && _loadError.Length == 0;
        internal string? CatalogError => _snapshot == null && _loadError.Length != 0 ? _loadError : null;

        internal Action? RefreshCatalog(ProjectSession? project, bool hasScene)
        {
            if (project?.Documents?.HasMaterialCatalog != true) project = null;
            if (!ReferenceEquals(project, _project))
            {
                if (_project != null) Commands.SetMaterialDraft(_project, false);
                _project = project;
                _operation = null;
                _snapshot = _draftSnapshot = null;
                _assets = null;
                _histories.Clear();
                _readRequested = false;
                _message = _loadError = "";
                _previousMaterial = null;
                SetProperty(ref _open, false, nameof(IsOpen));
                SetProperty(ref _draft, false, nameof(HasDraft));
                SetProperty(ref _items, Array.Empty<InspectorRenderAssetChoice>(), nameof(Items));
                SetProperty(ref _textures, Array.Empty<InspectorRenderAssetChoice>(), nameof(Textures));
                Show(null);
            }
            SetProperty(ref _available, project != null, nameof(IsAvailable));
            if (project == null) return null;

            Action? assignCopy = null;
            var documents = project.Documents!;
            if (_operation is { Work.IsCompleted: true } operation)
            {
                _operation = null;
                try
                {
                    var committed = operation.Work.GetAwaiter().GetResult();
                    documents.PublishMaterialCatalog(committed);
                    Publish(documents.MaterialSnapshot!);
                    _message = _loadError = "";
                    if (operation.Kind == ActionKind.Read)
                    {
                        ReconcileHistory();
                        if (_shown != null && !_readOnly) Show(_snapshot!.Find(_shown.Id));
                    }
                    else
                    {
                        var value = _snapshot!.Find(operation.Value!.Id)!;
                        switch (operation.Kind)
                        {
                            case ActionKind.Save when operation.Before is { } before && value != before:
                                if (!_histories.TryGetValue(value.Id, out var branch)) _histories.Add(value.Id, branch = new());
                                branch.Add(before, value);
                                break;
                            case ActionKind.Undo: CurrentHistory!.Cursor--; break;
                            case ActionKind.Redo: CurrentHistory!.Cursor++; break;
                            case ActionKind.Copy: assignCopy = () => AssignCopy(operation.Target!, value.Id); break;
                        }
                        _previousMaterial = null;
                        _draftSnapshot = null;
                        Commands.SetMaterialDraft(project, false);
                        SetProperty(ref _draft, false, nameof(HasDraft));
                        Show(value);
                    }
                }
                catch (Exception error)
                {
                    _message = error is OperationCanceledException ? "Операция с материалами отменена." : error.Message;
                    if (operation.Kind == ActionKind.Read) _loadError = _message;
                }
            }
            else if (documents.MaterialSnapshot is { } shared && !ReferenceEquals(shared, _snapshot))
            {
                Publish(shared);
                ReconcileHistory();
                if (!_draft && _shown != null && !_readOnly) Show(shared.Find(_shown.Id));
            }
            TryInitialRead(hasScene);
            return assignCopy;
        }

        internal void RefreshAssets(SceneRenderAssets? assets)
        {
            if (_project == null) assets = null;
            if (!ReferenceEquals(_assets, assets))
            {
                _assets = assets;
                SetProperty(ref _textures, assets == null ? [] : Array.AsReadOnly(assets.Textures.Select(
                    static texture => new InspectorRenderAssetChoice(texture.Id, texture.Name)).ToArray()), nameof(Textures));
                if (_readOnly && _shown != null) Show(assets?.ReadMaterial(_shown.Id), readOnly: true);
            }
            Update();
        }

        private void Publish(ProjectMaterialCatalogSnapshot snapshot)
        {
            if (ReferenceEquals(_snapshot, snapshot)) return;
            _snapshot = snapshot;
            SetProperty(ref _items, Array.AsReadOnly(snapshot.Materials.OrderBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.Id).Select(static row => new InspectorRenderAssetChoice(row.Id, row.Name)).ToArray()), nameof(Items));
        }

        private void ReconcileHistory()
        {
            foreach (var (id, branch) in _histories)
                if (_snapshot!.Find(id) != branch.Edge) _histories.Remove(id);
        }

        private void TryInitialRead(bool hasScene)
        {
            if (!_readRequested && _snapshot == null && (hasScene || _open)) StartRead();
        }

        private void StartRead()
        {
            if (_project == null || _operation != null || !Commands.CanExecute(DocumentCommand.ReadMaterialCatalog)) return;
            _readRequested = true;
            _message = _loadError = "";
            _operation = new(Commands.ReadMaterialCatalogAsync(_project), ActionKind.Read);
        }

        private bool CanSwitch()
        {
            if (!_draft) return Idle;
            _message = EditorDocumentCommands.MaterialDraftWarning;
            Update();
            return false;
        }

        public void Open(Guid id)
        {
            inspector.VerifyOwner();
            if (_shown?.Id == id || !CanSwitch() || _snapshot?.Find(id) is not { } row) return;
            _message = "";
            Show(row);
            Update();
        }

        public void New()
        {
            inspector.VerifyOwner();
            if (!CanSwitch() || _project == null || _snapshot == null || !Commands.SetMaterialDraft(_project, true)) return;
            _previousMaterial = _shown?.Id;
            _draftSnapshot = _snapshot;
            _message = "";
            SetProperty(ref _draft, true, nameof(HasDraft));
            Show(new(Guid.NewGuid(), "Новый материал", Vector3.One, null), isNew: true);
            Update();
        }

        public void Reload()
        {
            inspector.VerifyOwner();
            if (CanSwitch()) StartRead();
            Update();
        }

        public void Save()
        {
            inspector.VerifyOwner();
            if (Idle && _draft && _shown != null && TryFields(out var color))
                StartWrite(ActionKind.Save, new(_shown.Id, _name.Trim(), color, _texture), _new ? null : _shown);
        }

        public void Cancel()
        {
            inspector.VerifyOwner();
            if (!_draft || _operation != null || _project == null) return;
            Commands.SetMaterialDraft(_project, false);
            _draftSnapshot = null;
            SetProperty(ref _draft, false, nameof(HasDraft));
            _message = "";
            if (!_new) Show(_snapshot?.Find(_shown!.Id));
            else if (_previousMaterial is { } previous && _snapshot?.Find(previous) is { } authored) Show(authored);
            else if (_previousMaterial is { } imported) Show(_assets?.ReadMaterial(imported), readOnly: true);
            else Show(null);
            _previousMaterial = null;
            Update();
        }

        public void Undo()
        {
            inspector.VerifyOwner();
            if (Idle && !_draft && !_readOnly && CurrentHistory is { Cursor: > 0 } branch)
            {
                var before = branch.Entries[branch.Cursor - 1].Before;
                if (TextureAvailable(before.BaseColorTextureId)) StartWrite(ActionKind.Undo, before);
            }
        }

        public void Redo()
        {
            inspector.VerifyOwner();
            if (Idle && !_draft && !_readOnly && CurrentHistory is { } branch && branch.Cursor < branch.Entries.Count)
            {
                var after = branch.Entries[branch.Cursor].After;
                if (TextureAvailable(after.BaseColorTextureId)) StartWrite(ActionKind.Redo, after);
            }
        }

        private void StartWrite(ActionKind kind, ProjectMaterial value, ProjectMaterial? before = null, CopyTarget? target = null)
        {
            var expected = kind == ActionKind.Save ? _draftSnapshot : _snapshot;
            if (_project == null || expected == null) return;
            _message = "";
            _operation = new(Commands.WriteMaterialAsync(_project, expected, value), kind, before, value, target);
            Update();
        }

        public void OpenSlot(InspectorRenderAssetSelection selection, Guid slotId)
        {
            inspector.VerifyOwner();
            if (ReferenceEquals(selection, inspector.RenderAssets) && SlotMaterial(selection, slotId) is { } id)
            {
                IsOpen = true;
                if (_shown?.Id == id || !CanSwitch()) return;
                var authored = _snapshot?.Find(id);
                if ((authored ?? _assets?.ReadMaterial(id)) is { } value)
                {
                    _message = "";
                    Show(value, readOnly: authored == null);
                    Update();
                }
            }
        }

        public bool CanCopySlot(InspectorRenderAssetSelection selection, Guid slotId) =>
            Idle && _snapshot != null && !_draft && !inspector.HasDraft && !inspector._conflict && inspector.IsCopyGestureIdle &&
            ReferenceEquals(selection, inspector.RenderAssets) && selection.Enabled && selection.Mesh is { } mesh &&
            SlotMaterial(selection, slotId) is { } id && _assets is { } assets && assets.MaterialDiagnostic(mesh, slotId, id) == null;

        public void CopyForSlot(InspectorRenderAssetSelection selection, Guid slotId)
        {
            inspector.VerifyOwner();
            inspector.Refresh(inspector._selection);
            if (!CanCopySlot(selection, slotId) || _assets?.ReadMaterial(SlotMaterial(selection, slotId)!.Value) is not { } source ||
                inspector._baseline is not { } document || _project == null) return;
            const string suffix = " — копия";
            var name = source.Name[..Math.Min(source.Name.Length, ProjectMaterialCatalog.MaxNameLength - suffix.Length)].TrimEnd();
            var copy = source with { Id = Guid.NewGuid(), Name = name + suffix };
            var target = new CopyTarget(_project, inspector._openingId, document, inspector._entity, selection.Component,
                selection.Mesh!.Value, slotId, source.Id, inspector._inspectorSelectionRevision, inspector._workspace?.SelectionRevision);
            IsOpen = true;
            StartWrite(ActionKind.Copy, copy, target: target);
        }

        private void AssignCopy(CopyTarget target, Guid material)
        {
            var state = Commands.Read();
            if (ReferenceEquals(state.Project, target.Project) && state.Document is { } document && document.OpeningId == target.Opening &&
                ReferenceEquals(document.Content?.Document, target.Document) && inspector._entity == target.Entity &&
                inspector._inspectorSelectionRevision == target.InspectorSelectionRevision &&
                inspector._workspace?.SelectionRevision == target.WorkspaceSelectionRevision && !inspector.HasDraft && inspector.IsCopyGestureIdle &&
                inspector.RenderAssets is { Enabled: true } selection && selection.Component == target.Component &&
                selection.Mesh == target.Mesh && SlotMaterial(selection, target.Slot) == target.Source &&
                inspector.TryStageSlotMaterial(selection, target.Slot, material))
            {
                if (inspector.ApplyCurrentDraft(target.Project, target.Opening)) return;
                // The assignment starts with a clean scene inspector, so this only discards the assignment we staged.
                inspector.Cancel();
            }
            _message = CopyNotAssigned;
            Update();
        }

        private static Guid? SlotMaterial(InspectorRenderAssetSelection selection, Guid slot) =>
            selection.SingleSlotId == slot ? selection.Material : selection.Slots.FirstOrDefault(item => item.Id == slot)?.Material;

        private void Show(ProjectMaterial? row, bool readOnly = false, bool isNew = false)
        {
            _shown = row;
            _new = isNew;
            SetProperty(ref _hasDocument, row != null, nameof(HasDocument));
            SetProperty(ref _readOnly, row != null && readOnly, nameof(IsReadOnly));
            SetProperty(ref _selected, row?.Id, nameof(SelectedMaterialId));
            SetProperty(ref _name, row?.Name ?? "", nameof(Name));
            _originalColor = row == null ? ("", "", "") :
                (Format(row.BaseColorSrgb.X), Format(row.BaseColorSrgb.Y), Format(row.BaseColorSrgb.Z));
            SetProperty(ref _red, _originalColor.Red, nameof(Red));
            SetProperty(ref _green, _originalColor.Green, nameof(Green));
            SetProperty(ref _blue, _originalColor.Blue, nameof(Blue));
            SetProperty(ref _texture, row?.BaseColorTextureId, nameof(TextureId));
        }

        private void SetInput<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
        {
            inspector.VerifyOwner();
            if (!CanEditFields || _shown == null || _project == null || EqualityComparer<T>.Default.Equals(field, value) ||
                !Commands.SetMaterialDraft(_project, true)) return;
            _message = "";
            field = value;
            var draft = _new || _name != _shown.Name || _texture != _shown.BaseColorTextureId ||
                _red != _originalColor.Red || _green != _originalColor.Green || _blue != _originalColor.Blue;
            if (draft) _draftSnapshot ??= _snapshot;
            else
            {
                Commands.SetMaterialDraft(_project, false);
                _draftSnapshot = null;
                if (_snapshot?.Find(_shown.Id) != _shown) Show(_snapshot?.Find(_shown.Id));
            }
            SetProperty(ref _draft, draft, nameof(HasDraft));
            Update();
            OnPropertyChanged(property);
        }

        private bool TryFields(out Vector3 color)
        {
            color = default;
            if (_shown == null || _readOnly || !TextureAvailable(_texture) ||
                _name.AsSpan().Trim().Length is 0 or > ProjectMaterialCatalog.MaxNameLength ||
                !Parse(_red, out var r) || !Parse(_green, out var g) || !Parse(_blue, out var b)) return false;
            color = new(r, g, b);
            return true;
        }

        private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static bool Parse(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value) && value is >= 0 and <= 1;

        private void Update()
        {
            var idle = Idle;
            var valid = _draft && TryFields(out _);
            var branch = CurrentHistory;
            SetProperty(ref _canEdit, idle && _shown != null && !_readOnly, nameof(CanEditFields));
            SetProperty(ref _canCreate, idle && !_draft && _snapshot != null, nameof(CanCreate));
            SetProperty(ref _canReload, idle && !_draft, nameof(CanReload));
            SetProperty(ref _canSave, idle && valid, nameof(CanSave));
            SetProperty(ref _canCancel, _operation == null && _draft, nameof(CanCancel));
            SetProperty(ref _canUndo, idle && !_draft && !_readOnly && branch is { Cursor: > 0 } &&
                TextureAvailable(branch.Entries[branch.Cursor - 1].Before.BaseColorTextureId), nameof(CanUndo));
            SetProperty(ref _canRedo, idle && !_draft && !_readOnly && branch != null && branch.Cursor < branch.Entries.Count &&
                TextureAvailable(branch.Entries[branch.Cursor].After.BaseColorTextureId), nameof(CanRedo));
            var status = _operation is { } operation ? (operation.Kind == ActionKind.Read ? "Загрузка материалов…" : "Сохранение материала…") :
                _message.Length != 0 ? _message : _assets?.Collision?.Message ??
                (_texture is { } missing && !TextureAvailable(missing) ? $"Текстура недоступна: {missing:D}." :
                _draft ? (valid ? EditorDocumentCommands.MaterialDraftWarning : InvalidFields + " " + EditorDocumentCommands.MaterialDraftWarning) :
                CatalogPending && (_open || inspector._baseline != null) ? "Загрузка материалов…" : "");
            SetProperty(ref _status, _available ? status : "", nameof(StatusText));
        }

        internal void Release()
        {
            if (_project != null) Commands.SetMaterialDraft(_project, false);
            ClearPropertyChangedSubscribers();
        }
    }
}
