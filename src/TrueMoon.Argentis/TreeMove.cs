namespace TrueMoon.Argentis;

/// <summary>Requested parent change; the consumer validates and commits its own data.</summary>
/// <param name="Id">Dragged/keyboard-selected key.</param><param name="ParentId">New parent, or null for root.</param>
public readonly record struct TreeMove(string Id, string? ParentId);