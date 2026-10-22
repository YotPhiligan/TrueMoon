using System.Collections.Immutable;

namespace TrueMoon.Aether.Scenes;

/// <summary>Stable v1 component roles understood by the primitive scene consumer. IDs and property keys are persistent contracts.</summary>
public static class BuiltInComponents
{
    /// <summary>Local position, pitch/yaw/roll radians and positive scale; right-handed +Y up, forward -Z.</summary>
    public static ComponentTypeId TransformId { get; } = new(new Guid("6fc9fce0-9547-42f5-b4df-f05f306f1001"));
    /// <summary>Opaque origin-centered unit cube with stored-sRGB material.</summary>
    public static ComponentTypeId AppearanceId { get; } = new(new Guid("6fc9fce0-9547-42f5-b4df-f05f306f1002"));
    /// <summary>Interaction eligibility; reach/raycast behavior is a separate gameplay policy.</summary>
    public static ComponentTypeId InteractionId { get; } = new(new Guid("6fc9fce0-9547-42f5-b4df-f05f306f1003"));
    /// <summary>Exact v1 schemas. Missing Transform means identity for non-rendering groups.</summary>
    public static ImmutableArray<ComponentSchema> Schemas { get; } =
    [
        new(TransformId, 1, [Number("x", 0), Number("y", 0), Number("z", 0),
            Number("pitch", 0), Number("yaw", 0), Number("roll", 0), Number("sx", 1), Number("sy", 1), Number("sz", 1)]),
        new(AppearanceId, 1, [Number("r", 1), Number("g", 1), Number("b", 1)], requiredTypes: [TransformId]),
        new(InteractionId, 1, [new("enabled", PropertyValueKind.Boolean, PropertyValue.From(true))], requiredTypes: [TransformId])
    ];
    private static ComponentPropertySchema Number(string key, double value) => new(key, PropertyValueKind.Number, PropertyValue.From(value));

    internal static void ValidateSchema(ComponentSchema actual)
    {
        var expected = Schemas.FirstOrDefault(s => s.TypeId == actual.TypeId);
        if (expected == null) return;
        if (actual.Version != expected.Version || actual.AllowMultiple != expected.AllowMultiple ||
            !actual.RequiredTypes.SetEquals(expected.RequiredTypes) || actual.Properties.Count != expected.Properties.Count ||
            expected.Properties.Any(p => !actual.Properties.TryGetValue(p.Key, out var value) || value.Kind != p.Value.Kind ||
                value.IsNullable != p.Value.IsNullable || value.DefaultValue != p.Value.DefaultValue))
            throw new InvalidDataException($"Unsupported built-in component schema: {actual.TypeId.Value:D}, version {actual.Version}.");
    }
}
