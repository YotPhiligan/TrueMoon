using A = TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Runtime.Ecs.Loading;

// Explicit staged value conversion, never CLR-name/schema coercion or runtime service access.
internal static class ContractComponentValues
{
    internal static A.ComponentSchema Legacy(A.ComponentSchema schema) => new(new(schema.TypeId.Value), schema.Version,
        schema.Properties.Values.Select(value => new A.ComponentPropertySchema(value.Id, (A.PropertyValueKind)value.Kind, Legacy(value.DefaultValue), value.IsNullable)),
        schema.AllowMultiple, schema.RequiredTypes.Select(value => new A.ComponentTypeId(value.Value)));
    private static A.PropertyValue Legacy(A.PropertyValue value) => value.Kind switch
    {
        A.PropertyValueKind.Null => A.PropertyValue.Null,
        A.PropertyValueKind.Boolean => A.PropertyValue.From(value.GetBoolean()),
        A.PropertyValueKind.Integer => A.PropertyValue.From(value.GetInteger()),
        A.PropertyValueKind.Number => A.PropertyValue.From(value.GetNumber()),
        A.PropertyValueKind.String => A.PropertyValue.From(value.GetString()),
        _ => throw new InvalidDataException("Unsupported neutral property kind.")
    };
    internal static A.PropertyValue Neutral(A.PropertyValue value) => value.Kind switch
    {
        A.PropertyValueKind.Null => A.PropertyValue.Null,
        A.PropertyValueKind.Boolean => A.PropertyValue.From(value.GetBoolean()),
        A.PropertyValueKind.Integer => A.PropertyValue.From(value.GetInteger()),
        A.PropertyValueKind.Number => A.PropertyValue.From(value.GetNumber()),
        A.PropertyValueKind.String => A.PropertyValue.From(value.GetString()),
        _ => throw new InvalidDataException("Unsupported runtime property kind.")
    };
}
