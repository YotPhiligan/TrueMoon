namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>Semantic target lookup checked against the live session.</summary>
/// <param name="Status">Lookup outcome.</param>
/// <param name="Node">Detached node data from the supplied snapshot when a unique node was found.</param>
public readonly record struct UiSemanticTargetResult(UiSemanticTargetStatus Status, UiSemanticNode? Node);
