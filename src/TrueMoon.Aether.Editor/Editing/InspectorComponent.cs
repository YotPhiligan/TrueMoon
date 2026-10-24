using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>One effective component and its schema-driven draft fields.</summary>
/// <param name="Id">Stable occurrence.</param><param name="TypeId">Stable type.</param><param name="SchemaVersion">Exact version.</param>
/// <param name="Title">Display label.</param><param name="SourceText">Presence provenance.</param><param name="Properties">Retained field models.</param>
public sealed record InspectorComponent(ComponentId Id, ComponentTypeId TypeId, int SchemaVersion,
    string Title, string SourceText, IReadOnlyList<InspectorProperty> Properties);

/// <summary>Schema that can be explicitly selected for a new component.</summary>
/// <param name="Schema">Exact immutable schema.</param><param name="Title">Display label.</param>
/// <param name="CanAdd">Multiplicity and required types currently permit adding this type.</param>
public sealed record InspectorComponentType(ComponentSchema Schema, string Title, bool CanAdd);

/// <summary>Hidden inherited occurrence that can be restored without creating a new identity.</summary>
/// <param name="Id">Occurrence identity.</param><param name="Title">Display label.</param>
/// <param name="LocalMask">Whether restoring clears this instance's own removal mask.</param>
public sealed record InspectorRemovedComponent(ComponentId Id, string Title, bool LocalMask);
