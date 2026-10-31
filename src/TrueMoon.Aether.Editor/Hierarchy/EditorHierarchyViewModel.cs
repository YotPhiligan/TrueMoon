using TrueMoon.Argentis;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Hierarchy;

/// <summary>UI-owner hierarchy presentation; borrows document admission, selection and Transform presentation.
/// Parent changes use the same authoring session/history as inspector edits; no controls or theme are held.</summary>
public sealed partial class EditorHierarchyViewModel : ViewModel, IDisposable
{
    private readonly EditorWorkspace _workspace;
    private readonly EditorDocumentCommands _commands;
    private readonly EditorInspectorViewModel _transform;
    private SceneDocument? _baseline;
    private IReadOnlyList<HierarchyItem> _items;
    private string _caption, _error = "", _hint = "";
    private string? _selected;
    private bool _canMove, _canMoveToRoot, _disposed;
    private int _owner;
    private long _version;
    /// <summary>Creates App-owned presentation borrowing existing owners.</summary>
    /// <param name="workspace">Shared scene selection.</param><param name="commands">Shared mutation/file admission.</param>
    /// <param name="transform">Shared draft admission and committed preview synchronization.</param>
    public EditorHierarchyViewModel(EditorWorkspace workspace, EditorDocumentCommands commands, EditorInspectorViewModel transform)
    {
        ArgumentNullException.ThrowIfNull(workspace); ArgumentNullException.ThrowIfNull(commands); ArgumentNullException.ThrowIfNull(transform);
        _workspace = workspace; _commands = commands; _transform = transform;
        _items = workspace.HierarchyItems; _caption = workspace.Scene.Id; _selected = workspace.SelectedId;
        workspace.SceneChanged += Changed; workspace.SelectionChanged += Changed;
    }
    /// <summary>Gets immutable hierarchy values, independent of UI controls.</summary>
    public IReadOnlyList<HierarchyItem> Items => _items;
    /// <summary>Gets scene caption.</summary>
    public string Caption => _caption;
    /// <summary>Gets shared selected identity.</summary>
    public string? SelectedId => _selected;
    /// <summary>Changes for every committed document observation, fencing gestures from an older snapshot.</summary>
    public long SnapshotVersion => _version;
    /// <summary>Gets whether structural editing is currently admitted.</summary>
    public bool CanMove => _canMove;
    /// <summary>Gets whether the selected child can be moved to the scene root.</summary>
    public bool CanMoveToRoot => _canMoveToRoot;
    /// <summary>Gets movement/admission guidance.</summary>
    public string Hint => _hint;
    /// <summary>Gets the latest rejected mutation diagnostic.</summary>
    public string ErrorText => _error;
    /// <summary>Gets whether a complete selectable mutation diagnostic is present.</summary>
    public bool HasError => _error.Length != 0;
    private void Changed() { if (!_disposed) Refresh(); }
    /// <summary>Observes the current committed document/admission on its UI owner thread.</summary>
    public void Refresh()
    {
        VerifyOwner();
        var state = _commands.Read(); var document = state.Document?.Content?.Document;
        if (!ReferenceEquals(_baseline, document))
        {
            _baseline = document; _version++; OnPropertyChanged(nameof(SnapshotVersion));
            Set(ref _error, "", nameof(ErrorText));
        }
        if (!_items.SequenceEqual(_workspace.HierarchyItems))
        { _items = _workspace.HierarchyItems; OnPropertyChanged(nameof(Items)); }
        Set(ref _caption, _workspace.Scene.Id, nameof(Caption));
        Set(ref _selected, _workspace.SelectedId, nameof(SelectedId));
        var canMove = document != null && state.RuntimeIdle && !state.Busy && !state.Exiting && !state.Closed &&
            state.Document?.Operation == DocumentOperation.None && !_transform.CanCancel;
        if (IsRenaming && (!canMove || _renameId != _selected || !ReferenceEquals(_renameState, state.Document?.Content))) ClearRename();
        canMove &= !IsRenaming;
        Set(ref _canMove, canMove, nameof(CanMove));
        Set(ref _canRename, canMove && _items.Any(item => item.Id == _selected), nameof(CanRename));
        Set(ref _canMoveToRoot, canMove && _items.Any(item => item.Id == _selected && item.ParentId != null), nameof(CanMoveToRoot));
        RefreshEntityActions(document);
        Set(ref _hint, document == null ? "Иерархия · только просмотр" : _transform.CanCancel ? "Завершите правку свойств перед переносом" :
            IsRenaming ? "Имя · Enter сохранить · Esc отменить" : canMove ? "F2 имя · Ctrl+Up/Down порядок" : "Перенос временно недоступен", nameof(Hint));
    }
    /// <summary>Selects an existing occurrence and updates the borrowed inspector.</summary>
    /// <param name="id">Existing key, or null to clear inspection.</param>
    public void Select(string? id)
    {
        VerifyOwner();
        if (id == null) { _workspace.ClearSelection(); _transform.Refresh(null); Refresh(); return; }
        if (!_workspace.EntityIds.Contains(id)) return;
        _workspace.Select(id); _transform.Refresh(id); Refresh();
    }
    /// <summary>Requests an atomic scene parent change, preserving local component values.</summary>
    /// <param name="id">Source occurrence.</param><param name="parentId">New parent, or null for root.</param>
    /// <returns>True only after successful shared-history commit.</returns>
    public bool Move(string id, string? parentId)
    {
        return Place(id, parentId, null, EntityPlacement.Last);
    }
    /// <summary>Places an occurrence last or relative to a destination sibling through shared history.</summary>
    public bool Place(string id, string? parentId, string? anchorId, EntityPlacement placement)
    {
        VerifyOwner(); var expected = _baseline; Refresh();
        if (!ReferenceEquals(expected, _baseline))
        { Set(ref _error, "Сцена изменилась. Повторите перенос на актуальном дереве.", nameof(ErrorText)); return false; }
        if (!CanMove || !Guid.TryParse(id, out var source) || parentId != null && !Guid.TryParse(parentId, out _)) return false;
        var item = _items.FirstOrDefault(item => item.Id == id);
        if (item == null || item.ParentId == parentId && placement == EntityPlacement.Last) return false;
        if (parentId != null && !_items.Any(item => item.Id == parentId)) return false;
        if (placement != EntityPlacement.Last)
        {
            if (anchorId == id || !_items.Any(item => item.Id == anchorId && item.ParentId == parentId)) return false;
            var siblings = _items.Where(item => item.ParentId == parentId).Select(item => item.Id).ToList();
            var old = siblings.IndexOf(id); siblings.Remove(id);
            var position = siblings.IndexOf(anchorId!) + (placement == EntityPlacement.After ? 1 : 0);
            if (old >= 0 && old == position) return false;
        }
        try
        {
            var result = _commands.EditHierarchy(expected!, new(new(Guid.NewGuid(), expected!.Revision, "editor"), placements:
                [new(new(source), parentId == null ? null : new EntityId(Guid.Parse(parentId)), placement,
                    anchorId == null ? null : new EntityId(Guid.Parse(anchorId)))]));
            if (!result.Succeeded) { Set(ref _error, (result.Error + ": " + result.Message).ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
            _workspace.SetDocument(_commands.Read().Document!.Content);
            _transform.Refresh(_workspace.SelectedId); Refresh();
            return true;
        }
        catch (Exception error) { Set(ref _error, error.Message.ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
    }
    /// <summary>Moves the selected child to the scene root through the same guarded operation.</summary>
    public void MoveToRoot() { VerifyOwner(); if (_selected != null) Move(_selected, null); }
    private void VerifyOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed, this); var thread = Environment.CurrentManagedThreadId;
        if (Interlocked.CompareExchange(ref _owner, thread, 0) is var owner && owner != 0 && owner != thread)
            throw new InvalidOperationException("Hierarchy presentation must be updated on its UI owner thread.");
    }
    private void Set<T>(ref T field, T value, string name)
    {
        var hadError = HasError;
        if (!SetProperty(ref field, value, name)) return;
        if (hadError != HasError) OnPropertyChanged(nameof(HasError));
    }
    /// <summary>Disconnects selection callbacks; all dependencies remain borrowed.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _workspace.SceneChanged -= Changed; _workspace.SelectionChanged -= Changed; ClearPropertyChangedSubscribers();
    }
}
