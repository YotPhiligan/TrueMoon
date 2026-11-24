using System.Collections.Immutable;

namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>Detached snapshot of the retained tree, usable off the UI owner thread.</summary>
/// <param name="SessionId">Identity of the source UI session.</param>
/// <param name="TreeGeneration">Changes on tree invalidation, including target ID changes.</param>
/// <param name="LayoutGeneration">Increments on each completed arrange pass.</param>
/// <param name="Viewport">Physical framebuffer and scale. Node rectangles are in logical units.</param>
/// <param name="Nodes">Preorder of the retained tree, using sibling paint order.</param>
/// <param name="IsTruncated">The node budget omitted part of the tree.</param>
public sealed record UiSemanticSnapshot(long SessionId, long TreeGeneration, long LayoutGeneration,
    UiViewport Viewport, ImmutableArray<UiSemanticNode> Nodes, bool IsTruncated);
