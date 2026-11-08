namespace TrueMoon.Aether.Editor;

/// <summary>Caller-owned view for a UI-thread controller factory product. The parent owns the product, dependencies stay borrowed.</summary>
/// <param name="View">Live App-local editor tree.</param>
public sealed record EditorViewportArguments(EditorView View);
