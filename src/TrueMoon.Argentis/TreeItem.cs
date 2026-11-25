using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>Immutable display data; IDs and parent links are independent of labels and icons.</summary>
/// <param name="Id">Stable nonblank key.</param><param name="ParentId">Parent key, or null for a root.</param>
/// <param name="Text">Single-line display label.</param><param name="Icon">Geometric row icon.</param>
public sealed record TreeItem(string Id, string? ParentId, string Text, UiSymbol Icon = UiSymbol.Objects);