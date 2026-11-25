namespace TrueMoon.Argentis;

/// <summary>Selection, expansion and parent-move capability of a single-selection tree.</summary>
public interface ITreeView
{
    /// <summary>Gets the selected stable key.</summary>
    string? SelectedId { get; }
    /// <summary>Snapshots and validates a complete ordered forest before publication.</summary>
    /// <param name="items">At most 4096 distinct items with valid acyclic parent links.</param>
    void SetItems(IEnumerable<TreeItem> items);
    /// <summary>Selects an item and reveals its ancestors, or clears selection.</summary>
    /// <param name="id">Existing key, or null.</param>
    void Select(string? id);
    /// <summary>Sets branch expansion; collapsing the selected descendant selects this parent.</summary>
    /// <param name="id">Existing key.</param><param name="expanded">Desired expansion.</param>
    void SetExpanded(string id, bool expanded);
    /// <summary>Raised when the selected key changes.</summary>
    event Action<string?>? SelectionChanged;
    /// <summary>Raised for an admitted parent-move request; never mutates the source data.</summary>
    event Action<TreeMove>? MoveRequested;
}