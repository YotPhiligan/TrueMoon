using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Kul;

/// <summary>Optional v1 authoring schema. Commands/state are runtime-only; no Transform requirement for nonspatial audio.</summary>
public static class AudioComponentSchemas
{
    /// <summary>Stable source schema identity.</summary>
    public static ComponentTypeId SourceId { get; } = new(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5b001"));
    /// <summary>Source metadata; asset must be a nonempty GUID present in the explicit audio capture.</summary>
    public static ComponentSchema Source { get; } = new(SourceId, 1,
    [
        new("asset", PropertyValueKind.String, PropertyValue.From("")),
        new("volume", PropertyValueKind.Number, PropertyValue.From(1d)),
        new("loop", PropertyValueKind.Boolean, PropertyValue.From(false)),
        new("autoPlay", PropertyValueKind.Boolean, PropertyValue.From(false))
    ]);
}
