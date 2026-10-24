using TrueMoon.Aether.Editor.Documents;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Compatibility name for the original Transform presentation.
/// New screens use EditorInspectorViewModel and its schema-driven Components.</summary>
public sealed class EditorTransformViewModel : EditorInspectorViewModel
{
    /// <summary>Creates shared drafts/history over borrowed document and preview services.</summary>
    /// <param name="commands">Document admission and mutations.</param><param name="preview">Preview coordinator.</param>
    public EditorTransformViewModel(EditorDocumentCommands commands, EditorPreview preview) : base(commands, preview) { }
    /// <inheritdoc />
    protected override bool ReplaceWholeTransform => true;
}
