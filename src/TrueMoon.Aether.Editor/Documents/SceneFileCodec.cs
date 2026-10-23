using System.Text.Json;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Strict version-1 authoring JSON. Persists catalogs and local patches, never resolved/runtime state or history.</summary>
/// <param name="authoring">Borrowed validation factory; temporary products are disposed before returning.</param>
public sealed class SceneFileCodec(IFactory<ISceneAuthoringSession, SceneDocument> authoring)
{
    /// <summary>Maximum UTF8 file length, including an optional BOM.</summary>
    public const int MaximumBytes = 4 * 1024 * 1024;
    /// <summary>Maximum instances in one file.</summary>
    public const int MaximumEntities = 4096;
    /// <summary>Maximum definitions in one file.</summary>
    public const int MaximumDefinitions = 4096;
    /// <summary>Maximum exact schemas in one file.</summary>
    public const int MaximumSchemas = 1024;
    private readonly IFactory<ISceneAuthoringSession, SceneDocument> _authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));

    /// <summary>Encodes a complete validated snapshot into bounded deterministic UTF8 without BOM.</summary>
    /// <param name="document">Immutable committed authoring snapshot.</param><param name="cancellationToken">Preparation cancellation.</param>
    /// <returns>Independent encoded bytes.</returns>
    public byte[] Encode(SceneDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(document);
        using var buffer = new LimitedBuffer();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteNumber("formatVersion", 1);
            writer.WriteString("id", document.Id.Value); writer.WriteNumber("revision", document.Revision);
            Array(writer, "entities", document.Entities, MaximumEntities, entity =>
            {
                writer.WriteStartObject(); writer.WriteString("id", entity.Id.Value); writer.WriteString("definitionId", entity.DefinitionId.Value);
                NullableId(writer, "parentId", entity.ParentId?.Value);
                writer.WritePropertyName("changes"); WritePatch(writer, entity.Changes); writer.WriteEndObject();
            });
            Array(writer, "definitions", document.Definitions, MaximumDefinitions, definition =>
            {
                writer.WriteStartObject(); writer.WriteString("id", definition.Id.Value); writer.WriteNumber("revision", definition.Revision);
                NullableId(writer, "baseDefinitionId", definition.BaseDefinitionId?.Value);
                writer.WritePropertyName("changes"); WritePatch(writer, definition.Changes); writer.WriteEndObject();
            });
            Array(writer, "schemas", document.Schemas, MaximumSchemas, schema =>
            {
                writer.WriteStartObject(); writer.WriteString("typeId", schema.TypeId.Value); writer.WriteNumber("version", schema.Version);
                writer.WriteBoolean("allowMultiple", schema.AllowMultiple);
                Array(writer, "requiredTypes", schema.RequiredTypes.OrderBy(id => id.Value), 256, id => writer.WriteStringValue(id.Value));
                Array(writer, "properties", schema.Properties.Values.OrderBy(p => p.Id, StringComparer.Ordinal), 256, property =>
                {
                    writer.WriteStartObject(); writer.WriteString("id", property.Id); writer.WriteString("kind", Kind(property.Kind));
                    writer.WriteBoolean("isNullable", property.IsNullable);
                    writer.WritePropertyName("defaultValue"); WriteValue(writer, property.DefaultValue); writer.WriteEndObject();
                });
                writer.WriteEndObject();
            });
            writer.WriteEndObject(); writer.Flush();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }

    /// <summary>Decodes and validates a complete scene before publication. Unknown versions require an explicit future migration.</summary>
    /// <param name="bytes">UTF8 JSON, with or without BOM.</param><param name="cancellationToken">Preparation cancellation.</param>
    /// <returns>Immutable authoring document.</returns>
    public SceneDocument Decode(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length > MaximumBytes) throw new InvalidDataException($"Scene exceeds {MaximumBytes} bytes.");
        if (bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
        try
        {
            using var json = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            var root = Object(json.RootElement, "formatVersion", "id", "revision", "entities", "definitions", "schemas");
            if (root.GetProperty("formatVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported scene format; expected version 1.");
            var entities = Items(root.GetProperty("entities"), MaximumEntities).Select(element =>
            {
                var value = Object(element, "id", "definitionId", "parentId", "changes");
                var parent = OptionalId(value.GetProperty("parentId"));
                return new EntityInstance(new(value.GetProperty("id").GetGuid()), new(value.GetProperty("definitionId").GetGuid()),
                    ReadPatch(value.GetProperty("changes")), parent is { } id ? new EntityId(id) : null);
            }).ToArray();
            var definitions = Items(root.GetProperty("definitions"), MaximumDefinitions).Select(element =>
            {
                var value = Object(element, "id", "revision", "baseDefinitionId", "changes");
                var parent = OptionalId(value.GetProperty("baseDefinitionId"));
                return new EntityDefinition(new(value.GetProperty("id").GetGuid()), ReadPatch(value.GetProperty("changes")),
                    parent is { } id ? new EntityDefinitionId(id) : null, value.GetProperty("revision").GetInt64());
            }).ToArray();
            var schemas = Items(root.GetProperty("schemas"), MaximumSchemas).Select(element =>
            {
                var value = Object(element, "typeId", "version", "allowMultiple", "requiredTypes", "properties");
                var properties = Items(value.GetProperty("properties"), 256).Select(property =>
                {
                    var data = Object(property, "id", "kind", "isNullable", "defaultValue");
                    return new ComponentPropertySchema(data.GetProperty("id").GetString()!, ParseKind(data.GetProperty("kind").GetString()),
                        ReadValue(data.GetProperty("defaultValue")), data.GetProperty("isNullable").GetBoolean());
                }).ToArray();
                return new ComponentSchema(new(value.GetProperty("typeId").GetGuid()), value.GetProperty("version").GetInt32(), properties,
                    value.GetProperty("allowMultiple").GetBoolean(),
                    Items(value.GetProperty("requiredTypes"), 256).Select(id => new ComponentTypeId(id.GetGuid())));
            }).ToArray();
            var document = new SceneDocument(new(root.GetProperty("id").GetGuid()), root.GetProperty("revision").GetInt64(), entities, definitions, schemas);
            cancellationToken.ThrowIfCancellationRequested();
            Validate(document);
            cancellationToken.ThrowIfCancellationRequested();
            return document;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or SceneResolutionException)
        { throw new InvalidDataException("Invalid authoring scene: " + error.Message, error); }
    }

    private void Validate(SceneDocument document)
    {
        if (document.Entities.Length > MaximumEntities || document.Definitions.Length > MaximumDefinitions || document.Schemas.Length > MaximumSchemas)
            throw new InvalidDataException("Scene catalog limit exceeded.");
        using var validation = _authoring.Create(document) ?? throw new InvalidOperationException("Authoring factory returned null.");
        _ = validation.Read();
    }

    private static JsonElement Object(JsonElement element, params string[] members)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a JSON object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!members.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new InvalidDataException("Unknown or duplicate member: " + property.Name);
        if (names.Count != members.Length) throw new InvalidDataException("Missing required scene member.");
        return element;
    }

    private static IEnumerable<JsonElement> Items(JsonElement element, int maximum)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > maximum) throw new InvalidDataException("Invalid scene array or collection limit exceeded.");
        return element.EnumerateArray();
    }

    private static Guid? OptionalId(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetGuid();

    private static EntityPatch ReadPatch(JsonElement element)
    {
        var value = Object(element, "added", "removed", "restored", "overrides");
        return new(
            Items(value.GetProperty("added"), 256).Select(data =>
            {
                var component = Object(data, "id", "typeId", "schemaVersion", "properties");
                var properties = component.GetProperty("properties");
                if (properties.ValueKind != JsonValueKind.Object || properties.EnumerateObject().Count() > 256)
                    throw new InvalidDataException("Invalid component properties.");
                // ComponentData rejects duplicate property keys and validates every key.
                return new ComponentData(new(component.GetProperty("id").GetGuid()), new(component.GetProperty("typeId").GetGuid()),
                    component.GetProperty("schemaVersion").GetInt32(), properties.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, ReadValue(p.Value))));
            }),
            Items(value.GetProperty("removed"), 256).Select(id => new ComponentId(id.GetGuid())),
            Items(value.GetProperty("overrides"), 1024).Select(data =>
            {
                var property = Object(data, "componentId", "propertyId", "value");
                return new ComponentPropertyOverride(new(property.GetProperty("componentId").GetGuid()),
                    property.GetProperty("propertyId").GetString()!, ReadValue(property.GetProperty("value")));
            }),
            Items(value.GetProperty("restored"), 256).Select(id => new ComponentId(id.GetGuid())));
    }

    private static PropertyValue ReadValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("kind", out var kind)) throw new InvalidDataException("Expected tagged scalar.");
        var type = ParseKind(kind.GetString());
        var value = Object(element, type == PropertyValueKind.Null ? new[] { "kind" } : new[] { "kind", "value" });
        if (type == PropertyValueKind.Null) return PropertyValue.Null;
        var data = value.GetProperty("value");
        return type switch
        {
            PropertyValueKind.Boolean => PropertyValue.From(data.GetBoolean()),
            PropertyValueKind.Integer => PropertyValue.From(data.GetInt64()),
            PropertyValueKind.Number => PropertyValue.From(data.GetDouble()),
            PropertyValueKind.String => PropertyValue.From(data.GetString()!),
            _ => throw new InvalidDataException("Unsupported scalar kind.")
        };
    }

    private static void WritePatch(Utf8JsonWriter writer, EntityPatch patch)
    {
        writer.WriteStartObject();
        Array(writer, "added", patch.AddedComponents, 256, component =>
        {
            writer.WriteStartObject(); writer.WriteString("id", component.Id.Value); writer.WriteString("typeId", component.TypeId.Value);
            writer.WriteNumber("schemaVersion", component.SchemaVersion); writer.WriteStartObject("properties");
            if (component.Properties.Count > 256) throw new InvalidDataException("Component property limit exceeded.");
            foreach (var property in component.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Key); WriteValue(writer, property.Value); }
            writer.WriteEndObject(); writer.WriteEndObject();
        });
        Array(writer, "removed", patch.RemovedComponents.OrderBy(id => id.Value), 256, id => writer.WriteStringValue(id.Value));
        Array(writer, "restored", patch.RestoredComponents.OrderBy(id => id.Value), 256, id => writer.WriteStringValue(id.Value));
        Array(writer, "overrides", patch.PropertyOverrides, 1024, property =>
        {
            writer.WriteStartObject(); writer.WriteString("componentId", property.ComponentId.Value); writer.WriteString("propertyId", property.PropertyId);
            writer.WritePropertyName("value"); WriteValue(writer, property.Value); writer.WriteEndObject();
        });
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, PropertyValue value)
    {
        writer.WriteStartObject(); writer.WriteString("kind", Kind(value.Kind));
        if (value.Kind != PropertyValueKind.Null)
        {
            writer.WritePropertyName("value");
            switch (value.Kind)
            {
                case PropertyValueKind.Boolean: writer.WriteBooleanValue(value.GetBoolean()); break;
                case PropertyValueKind.Integer: writer.WriteNumberValue(value.GetInteger()); break;
                case PropertyValueKind.Number: writer.WriteNumberValue(value.GetNumber()); break;
                case PropertyValueKind.String: writer.WriteStringValue(value.GetString()); break;
                default: throw new InvalidDataException("Unsupported scalar kind.");
            }
        }
        writer.WriteEndObject();
    }

    private static string Kind(PropertyValueKind kind) => kind switch
    {
        PropertyValueKind.Null => "null", PropertyValueKind.Boolean => "boolean", PropertyValueKind.Integer => "integer",
        PropertyValueKind.Number => "number", PropertyValueKind.String => "string", _ => throw new InvalidDataException("Unsupported scalar kind.")
    };
    private static PropertyValueKind ParseKind(string? value) => value switch
    {
        "null" => PropertyValueKind.Null, "boolean" => PropertyValueKind.Boolean, "integer" => PropertyValueKind.Integer,
        "number" => PropertyValueKind.Number, "string" => PropertyValueKind.String, _ => throw new InvalidDataException("Unsupported scalar kind.")
    };
    private static void NullableId(Utf8JsonWriter writer, string key, Guid? value)
    { if (value is { } id) writer.WriteString(key, id); else writer.WriteNull(key); }
    private static void Array<T>(Utf8JsonWriter writer, string name, IEnumerable<T> values, int maximum, Action<T> write)
    {
        writer.WriteStartArray(name); var count = 0;
        foreach (var value in values) { if (++count > maximum) throw new InvalidDataException("Scene collection limit exceeded."); write(value); }
        writer.WriteEndArray();
    }
    private sealed class LimitedBuffer : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        private void Check(int count) { if (Length + count > MaximumBytes) throw new InvalidDataException("Encoded scene exceeds the byte limit."); }
    }
}
