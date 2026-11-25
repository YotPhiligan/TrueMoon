using A = TrueMoon.Aether.Scenes;
using ComponentPropertySchema = TrueMoon.Aether.Scenes.ComponentPropertySchema;
using ComponentSchema = TrueMoon.Aether.Scenes.ComponentSchema;
using ComponentTypeId = TrueMoon.Aether.Scenes.ComponentTypeId;
using PropertyValue = TrueMoon.Aether.Scenes.PropertyValue;
using PropertyValueKind = TrueMoon.Aether.Scenes.PropertyValueKind;

namespace TrueMoon.Aether.Runtime.Ecs.Loading;

// Explicit staged value conversion, never CLR-name/schema coercion or runtime service access.
internal static class ContractComponentValues
{
    internal static ComponentSchema Legacy(ComponentSchema schema) => new(new(schema.TypeId.Value), schema.Version,
        schema.Properties.Values.Select(value => new ComponentPropertySchema(value.Id, (PropertyValueKind)value.Kind, Legacy(value.DefaultValue), value.IsNullable)),
        schema.AllowMultiple, schema.RequiredTypes.Select(value => new ComponentTypeId(value.Value)));
    private static PropertyValue Legacy(PropertyValue value) => value.Kind switch
    {
        PropertyValueKind.Null => PropertyValue.Null,
        PropertyValueKind.Boolean => PropertyValue.From(value.GetBoolean()),
        PropertyValueKind.Integer => PropertyValue.From(value.GetInteger()),
        PropertyValueKind.Number => PropertyValue.From(value.GetNumber()),
        PropertyValueKind.String => PropertyValue.From(value.GetString()),
        _ => throw new InvalidDataException("Unsupported neutral property kind.")
    };
    internal static PropertyValue Neutral(PropertyValue value) => value.Kind switch
    {
        PropertyValueKind.Null => PropertyValue.Null,
        PropertyValueKind.Boolean => PropertyValue.From(value.GetBoolean()),
        PropertyValueKind.Integer => PropertyValue.From(value.GetInteger()),
        PropertyValueKind.Number => PropertyValue.From(value.GetNumber()),
        PropertyValueKind.String => PropertyValue.From(value.GetString()),
        _ => throw new InvalidDataException("Unsupported runtime property kind.")
    };
}
