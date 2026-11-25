namespace TrueMoon.Argentis;

/// <summary>Consumer-owned sibling placement request; the control never changes data.</summary>
/// <param name="Id">Source key.</param><param name="ParentId">Destination parent.</param>
/// <param name="AnchorId">Sibling anchor.</param><param name="Position">Before/After the anchor.</param>
public readonly record struct TreePlacement(string Id, string? ParentId, string AnchorId, TreeDropPosition Position);