
namespace TrueMoon.Aether.Abstractions.Scenes;


/// <summary>Stable failure categories for authoring tools and API clients.</summary>
public enum EntityResolutionError
{
    /// <summary>An instance or base references an absent definition.</summary>
    MissingDefinition,
    /// <summary>The base graph contains a cycle.</summary>
    InheritanceCycle,
    /// <summary>The inheritance chain exceeds the supported bound.</summary>
    InheritanceDepthExceeded,
    /// <summary>Two catalog definitions have the same identity.</summary>
    DuplicateDefinition,
    /// <summary>Two schemas have the same type and version.</summary>
    DuplicateSchema,
    /// <summary>No schema is registered for the component type.</summary>
    UnknownSchema,
    /// <summary>The type exists but its exact requested version does not.</summary>
    SchemaVersionMismatch,
    /// <summary>An addition reuses an active or hidden occurrence ID.</summary>
    ComponentAlreadyExists,
    /// <summary>A structural or property change targets an absent occurrence.</summary>
    ComponentNotFound,
    /// <summary>A removal or property change targets a hidden occurrence.</summary>
    ComponentAlreadyRemoved,
    /// <summary>A restoration targets a visible occurrence.</summary>
    ComponentNotRemoved,
    /// <summary>A property ID is absent from the exact schema.</summary>
    UnknownProperty,
    /// <summary>A property value violates kind or nullability.</summary>
    PropertyTypeMismatch,
    /// <summary>The effective entity violates component multiplicity.</summary>
    ComponentMultiplicity,
    /// <summary>A required component type is absent from the effective entity.</summary>
    MissingComponentDependency
}
