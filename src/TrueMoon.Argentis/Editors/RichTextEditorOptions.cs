namespace TrueMoon.Argentis.Editors;

/// <summary>Immutable editing and presentation options. Assign a new snapshot to reconfigure an editor.</summary>
public sealed record RichTextEditorOptions
{
    private int _undoLimit = 100;
    /// <summary>Allows selection/copy/navigation while preventing user edits and formatting.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>Wraps paragraphs to the available content width.</summary>
    public bool WordWrap { get; init; } = true;
    /// <summary>Inserts a new paragraph for Enter.</summary>
    public bool AcceptsReturn { get; init; } = true;
    /// <summary>Consumes Tab and inserts four spaces. Otherwise Tab remains focus navigation.</summary>
    public bool AcceptsTab { get; init; }
    /// <summary>Whether an unfocused editor paints its selection.</summary>
    public bool ShowSelectionWhenUnfocused { get; init; }
    /// <summary>Paragraph alignment.</summary>
    public RichTextAlignment Alignment { get; init; }
    /// <summary>Optional selection highlight; null uses the theme accent.</summary>
    public Color? SelectionColor { get; init; }
    /// <summary>Maximum retained undo steps, from zero to one thousand. Zero disables history.</summary>
    public int UndoLimit { get => _undoLimit; init { if (value is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(value)); _undoLimit = value; } }
}