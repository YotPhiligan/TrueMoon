namespace TrueMoon.Aether.Editor.Hierarchy;

/// <summary>Semantic occurrence kind, independent of UI symbols and themes.</summary>
public enum HierarchyItemKind { Entity, Renderable, Interactive }

/// <summary>Immutable scene hierarchy presentation addressed by persistent identity.</summary>
/// <param name="Id">Entity key.</param><param name="ParentId">Scene parent key.</param>
/// <param name="Name">Display name; current GUID-backed scenes use a shortened ID.</param><param name="Kind">Component-derived role.</param>
public sealed record HierarchyItem(string Id, string? ParentId, string Name, HierarchyItemKind Kind);
