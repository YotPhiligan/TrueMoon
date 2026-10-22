namespace TrueMoon.Aether.Scenes;

/// <summary>Explicit property override addressed by stable component occurrence and property IDs.</summary>
public sealed class ComponentPropertyOverride
{
    /// <summary>Creates an override. Schema validation occurs during resolution.</summary>
    /// <param name="componentId">Target occurrence identity.</param>
    /// <param name="propertyId">Persistent property key.</param>
    /// <param name="value">Explicit value, including null/default.</param>
    public ComponentPropertyOverride(ComponentId componentId, string propertyId, PropertyValue value)
    {
        AuthoringValidation.Id(componentId.Value, nameof(componentId));
        ComponentId = componentId; PropertyId = AuthoringValidation.Key(propertyId, nameof(propertyId)); Value = value;
    }
    /// <summary>Gets the target occurrence ID.</summary>
    public ComponentId ComponentId { get; }
    /// <summary>Gets the case-sensitive target property ID.</summary>
    public string PropertyId { get; }
    /// <summary>Gets the explicit replacement value.</summary>
    public PropertyValue Value { get; }
}
