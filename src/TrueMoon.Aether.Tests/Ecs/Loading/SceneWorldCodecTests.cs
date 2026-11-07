using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class SceneWorldCodecTests
{
    private static readonly SceneId Scene = new(new Guid("59b05992-1999-44a2-9779-000000000001"));
    private static readonly ComponentTypeId MainType = new(new Guid("59b05992-1999-44a2-9779-000000000002"));
    private static readonly ComponentTypeId ExtraType = new(new Guid("59b05992-1999-44a2-9779-000000000003"));
    private static readonly ComponentId Main = new(new Guid("59b05992-1999-44a2-9779-000000000004"));
    private static readonly ComponentId Extra = new(new Guid("59b05992-1999-44a2-9779-000000000005"));
    private static readonly EntityDefinitionId Base = new(new Guid("59b05992-1999-44a2-9779-000000000006"));
    private static readonly EntityDefinitionId Variant = new(new Guid("59b05992-1999-44a2-9779-000000000007"));
    private static readonly EntityDefinitionId Restored = new(new Guid("59b05992-1999-44a2-9779-000000000008"));
    private static readonly EntityId Root = new(new Guid("59b05992-1999-44a2-9779-000000000009"));
    private static readonly EntityId Child = new(new Guid("59b05992-1999-44a2-9779-000000000010"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RoundTrip_PreservesFullAuthoringMetadataAndResolvedInheritance(bool metadata)
    {
        var scenes = StrictCodec(); var codec = new SceneWorldCodec(scenes);
        var source = Document(metadata); var data = new SceneWorldData(source);
        var encoded = codec.Encode(data);
        var body = scenes.Encode(source);
        Assert.True(encoded.AsSpan(0, 4).SequenceEqual("TMWL"u8));
        Assert.Equal(44, SceneWorldCodec.HeaderBytes);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(4)));
        Assert.Equal(body.Length, BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(8)));
        Assert.Equal(SHA256.HashData(body), encoded.AsSpan(12, 32).ToArray());
        Assert.Equal(body, encoded.AsSpan(44).ToArray());
        using (var json = JsonDocument.Parse(body))
            Assert.Equal(metadata ? 2 : 1, json.RootElement.GetProperty("formatVersion").GetInt32());

        var decoded = codec.Decode(encoded);
        Assert.Equal(1, decoded.FormatVersion);
        Assert.NotSame(source, decoded.Scene);
        Assert.Equal((Scene, 19L), (decoded.Scene.Id, decoded.Scene.Revision));
        Assert.Equal(new[] { Child, Root }, decoded.Scene.Entities.Select(entity => entity.Id));
        var root = decoded.Scene.Entities.Single(entity => entity.Id == Root);
        var child = decoded.Scene.Entities.Single(entity => entity.Id == Child);
        Assert.Equal((Root, Variant), (child.ParentId!.Value, child.DefinitionId));
        Assert.Equal(Restored, root.DefinitionId);
        Assert.Equal(metadata ? "Дверь 🚪" : null, child.Name);
        Assert.Equal(metadata ? 2 : null, child.SiblingOrder);
        Assert.Equal(metadata ? "Корень" : null, root.Name);
        Assert.Equal(metadata ? 4 : null, root.SiblingOrder);
        var variant = decoded.Scene.Definitions.Single(definition => definition.Id == Variant);
        var restored = decoded.Scene.Definitions.Single(definition => definition.Id == Restored);
        Assert.Equal((Base, 5L), (variant.BaseDefinitionId!.Value, variant.Revision));
        Assert.Equal((Variant, 7L), (restored.BaseDefinitionId!.Value, restored.Revision));
        Assert.Contains(Extra, variant.Changes.RemovedComponents);
        Assert.Contains(Extra, restored.Changes.RestoredComponents);
        Assert.Contains(variant.Changes.PropertyOverrides, value => value.ComponentId == Main && value.PropertyId == "note" && value.Value == PropertyValue.Null);
        Assert.Contains(child.Changes.PropertyOverrides, value => value.ComponentId == Extra && value.PropertyId == "value" && value.Value.GetInteger() == 17);
        var schema = decoded.Scene.Schemas.Single(value => value.TypeId == MainType);
        Assert.True(schema.Properties["note"].IsNullable);
        Assert.Equal(PropertyValue.Null, schema.Properties["note"].DefaultValue);
        Assert.Equal(PropertyValue.From(9L), schema.Properties["count"].DefaultValue);
        Assert.Contains(MainType, decoded.Scene.Schemas.Single(value => value.TypeId == ExtraType).RequiredTypes);

        var resolved = SceneResolver.Resolve(decoded.Scene);
        Assert.Equal(new[] { Root, Child }, resolved.HierarchyOrder);
        Assert.Equal(new[] { Base, Variant, Restored }, resolved.Entities[Root].DefinitionChain);
        Assert.Equal(new[] { Base, Variant }, resolved.Entities[Child].DefinitionChain);
        Assert.Equal(3L, resolved.DefinitionRevisions[Base]);
        Assert.Equal(5L, resolved.DefinitionRevisions[Variant]);
        Assert.Equal(7L, resolved.DefinitionRevisions[Restored]);
        Assert.Equal(4.5d, resolved.Entities[Root].Components[Main].Properties["x"].Value.GetNumber());
        Assert.Equal(3.5d, resolved.Entities[Child].Components[Main].Properties["x"].Value.GetNumber());
        Assert.Equal(long.MaxValue, resolved.Entities[Root].Components[Main].Properties["count"].Value.GetInteger());
        Assert.Equal(9L, resolved.Entities[Child].Components[Main].Properties["count"].Value.GetInteger());
        Assert.True(resolved.Entities[Child].Components[Main].Properties["count"].IsSchemaDefault);
        Assert.Equal(PropertyValue.Null, resolved.Entities[Child].Components[Main].Properties["note"].Value);
        Assert.False(resolved.Entities[Child].Components[Main].Properties["note"].IsSchemaDefault);
        Assert.True(resolved.Entities[Root].Components[Main].Properties["active"].Value.GetBoolean());
        Assert.Equal(11L, resolved.Entities[Root].Components[Extra].Properties["value"].Value.GetInteger());
        Assert.Equal(17L, resolved.Entities[Child].Components[Extra].Properties["value"].Value.GetInteger());
        Assert.Equal(Restored, resolved.Entities[Root].Components[Extra].PresenceSource.DefinitionId);
        Assert.Equal(encoded, codec.Encode(decoded));
    }

    [Fact]
    public void Decode_ConsumesIndependentEnvelopeAndPreservesUnusedRuntimeUnsupportedCatalog()
    {
        var unsupported = new ComponentSchema(MainType, 3,
            [new("text", PropertyValueKind.String, PropertyValue.From("no runtime binding"))], allowMultiple: true);
        var source = new SceneDocument(Scene, 23, [], [], [unsupported]);
        var scenes = StrictCodec(); var codec = new SceneWorldCodec(scenes);
        var envelope = Envelope(scenes.Encode(source));
        var decoded = codec.Decode(envelope);
        Assert.Empty(decoded.Scene.Entities);
        Assert.Empty(decoded.Scene.Definitions);
        var schema = Assert.Single(decoded.Scene.Schemas);
        Assert.Equal((MainType, 3, true), (schema.TypeId, schema.Version, schema.AllowMultiple));
        Assert.Equal("no runtime binding", schema.Properties["text"].DefaultValue.GetString());
        Assert.Equal((Scene, 23L), (decoded.Scene.Id, decoded.Scene.Revision));
        Assert.Equal(envelope, codec.Encode(decoded));
    }

    [Fact]
    public void RoundTrip_SupportsMoreThan64EntitiesAndPayloadLargerThanLegacyControlBudget()
    {
        var schema = new ComponentSchema(MainType, 1, [new("label", PropertyValueKind.String, PropertyValue.From("fallback"))]);
        var definition = new EntityDefinition(Base, new([new(Main, MainType, 1)]));
        var text = new string('x', 512);
        var entities = Enumerable.Range(1, 128).Select(index =>
            new EntityInstance(new(new Guid(index, 0, 0, new byte[8])), Base,
                new(propertyOverrides: [new(Main, "label", PropertyValue.From(text))]), null, "Entity " + index, index)).ToArray();
        var codec = new SceneWorldCodec(StrictCodec());
        var encoded = codec.Encode(new(new SceneDocument(Scene, 29, entities, [definition], [schema])));
        Assert.True(encoded.Length > 64 * 1024);
        var decoded = codec.Decode(encoded);
        Assert.Equal(128, decoded.Scene.Entities.Length);
        var resolved = SceneResolver.Resolve(decoded.Scene);
        Assert.Equal(128, resolved.Entities.Count);
        var last = decoded.Scene.Entities[^1];
        Assert.Equal("Entity 128", last.Name);
        Assert.Equal(128, last.SiblingOrder);
        Assert.Equal(text, resolved.Entities[last.Id].Components[Main].Properties["label"].Value.GetString());
        Assert.Equal(encoded, codec.Encode(decoded));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("short-header")]
    [InlineData("magic")]
    [InlineData("zero-version")]
    [InlineData("next-version")]
    [InlineData("negative-version")]
    [InlineData("negative-length")]
    [InlineData("short-length")]
    [InlineData("long-length")]
    [InlineData("huge-length")]
    [InlineData("checksum")]
    [InlineData("body-corrupt")]
    [InlineData("truncated-body")]
    [InlineData("trailing-byte")]
    public void Decode_RejectsMalformedEnvelopeBeforeInvokingAuthoringValidation(string kind)
    {
        var valid = Envelope(StrictCodec().Encode(Document(false)));
        var malformed = valid.ToArray();
        switch (kind)
        {
            case "empty": malformed = []; break;
            case "short-header": malformed = valid[..43]; break;
            case "magic": malformed[0] ^= 1; break;
            case "zero-version": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(4), 0); break;
            case "next-version": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(4), 2); break;
            case "negative-version": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(4), -1); break;
            case "negative-length": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(8), -1); break;
            case "short-length": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(8), valid.Length - 45); break;
            case "long-length": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(8), valid.Length - 43); break;
            case "huge-length": BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(8), int.MaxValue); break;
            case "checksum": malformed[12] ^= 1; break;
            case "body-corrupt": malformed[^1] ^= 1; break;
            case "truncated-body": malformed = valid[..^1]; break;
            case "trailing-byte": malformed = [.. valid, (byte)0]; break;
        }
        var validations = 0;
        var codec = new SceneWorldCodec(new SceneFileCodec(new Factory<ISceneAuthoringSession, SceneDocument>(_ =>
        { validations++; throw new InvalidOperationException("Must reject envelope before authoring validation."); })));
        Assert.Throws<InvalidDataException>(() => codec.Decode(malformed));
        Assert.Equal(0, validations);
        Assert.Equal(valid.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(valid.AsSpan(8)));
    }

    [Theory]
    [InlineData("invalid-json")]
    [InlineData("unknown-body-version")]
    [InlineData("unknown-member")]
    [InlineData("invalid-parent")]
    public void Decode_RejectsHashCorrectInvalidAuthoringBody(string kind)
    {
        var body = StrictCodec().Encode(Document(false));
        if (kind == "invalid-json") body = "{not JSON}"u8.ToArray();
        else
        {
            var json = Encoding.UTF8.GetString(body);
            json = kind switch
            {
                "unknown-body-version" => json.Replace("\"formatVersion\":1", "\"formatVersion\":99", StringComparison.Ordinal),
                "unknown-member" => "{\"unexpected\":true," + json[1..],
                "invalid-parent" => json.Replace("\"parentId\":\"" + Root.Value.ToString("D") + "\"",
                    "\"parentId\":\"59b05992-1999-44a2-9779-000000009999\"", StringComparison.Ordinal),
                _ => throw new ArgumentException(nameof(kind))
            };
            body = Encoding.UTF8.GetBytes(json);
        }
        var codec = new SceneWorldCodec(StrictCodec());
        Assert.Throws<InvalidDataException>(() => codec.Decode(Envelope(body)));
    }

    [Fact]
    public void Decode_AcceptsExactMaximumBodyAndRejectsAdjacentOversize()
    {
        var codec = new SceneWorldCodec(StrictCodec());
        var empty = new SceneDocument(Scene, 31, [], [], []);
        var body = StrictCodec().Encode(empty);
        var padded = new byte[SceneFileCodec.MaximumBytes];
        padded.AsSpan().Fill((byte)' '); body.CopyTo(padded, 0);
        var envelope = Envelope(padded);
        Assert.Equal(4 * 1024 * 1024 + 44, SceneWorldCodec.MaximumBytes);
        Assert.Equal(SceneWorldCodec.MaximumBytes, envelope.Length);
        var decoded = codec.Decode(envelope);
        Assert.Equal((Scene, 31L), (decoded.Scene.Id, decoded.Scene.Revision));
        Assert.Empty(decoded.Scene.Entities);
        Assert.Throws<InvalidDataException>(() => codec.Decode(Envelope([.. padded, (byte)' '])));
    }

    [Fact]
    public void Encode_RejectsOversizedValidCatalogBeforePublishingEnvelope()
    {
        var text = new string('x', PropertyValue.MaximumStringLength);
        var schemas = Enumerable.Range(1, 12).Select(index => new ComponentSchema(
            new(new Guid(index, 1, 0, new byte[8])), 1,
            Enumerable.Range(0, 128).Select(property => new ComponentPropertySchema("p" + property,
                PropertyValueKind.String, PropertyValue.From(text))))).ToArray();
        var source = new SceneDocument(Scene, 37, [], [], schemas);
        Assert.Empty(SceneResolver.Resolve(source).Entities);
        var codec = new SceneWorldCodec(StrictCodec());
        Assert.Throws<InvalidDataException>(() => codec.Encode(new(source)));
        Assert.Equal((0, 12), (source.Entities.Length, source.Schemas.Length));
    }

    [Fact]
    public void Codec_RejectsNullVersionsAndPreCancellationWithoutAuthoringCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneWorldCodec(null!));
        Assert.Throws<ArgumentNullException>(() => new SceneWorldData(null!));
        var source = Document(false);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneWorldData(source, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneWorldData(source, -1));
        var validations = 0;
        var codec = new SceneWorldCodec(new SceneFileCodec(new Factory<ISceneAuthoringSession, SceneDocument>(_ =>
        { validations++; throw new InvalidOperationException("Must reject before authoring callback."); })));
        Assert.Throws<ArgumentNullException>(() => codec.Encode(null!));
        Assert.Throws<InvalidDataException>(() => codec.Encode(new(source, 2)));
        Assert.Throws<InvalidDataException>(() => codec.Encode(new(source, int.MaxValue)));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => codec.Encode(new(source), cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => codec.Decode(Array.Empty<byte>(), cancellation.Token));
        Assert.Equal(0, validations);
        Assert.Same(source, new SceneWorldData(source).Scene);
        Assert.Equal(1, SceneWorldData.CurrentFormatVersion);
    }

    [Fact]
    public void Encode_RejectsUnrepresentableStringsEvenInUnusedDefinitionCatalog()
    {
        var source = TextDocument(((char)0xD800).ToString(), "component");
        var unused = new SceneDocument(source.Id, source.Revision, [], source.Definitions, source.Schemas);
        Assert.Empty(SceneResolver.Resolve(unused).Entities);
        var codec = new SceneWorldCodec(StrictCodec());
        Assert.Throws<InvalidDataException>(() => codec.Encode(new(unused)));
    }

    [Theory]
    [InlineData("default", 0xD800)] [InlineData("default", 0xDC00)]
    [InlineData("component", 0xD800)] [InlineData("component", 0xDC00)]
    [InlineData("definition-override", 0xD800)] [InlineData("definition-override", 0xDC00)]
    [InlineData("instance-override", 0xD800)] [InlineData("instance-override", 0xDC00)]
    public void Encode_RejectsLoneUtf16SurrogatesInsteadOfSilentlyReplacingPayload(string location, int code)
    {
        var source = TextDocument(((char)code).ToString(), location);
        var codec = new SceneWorldCodec(StrictCodec());
        Assert.Throws<InvalidDataException>(() => codec.Encode(new(source)));
    }

    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public void Decode_RejectsUnrepresentableJsonUnicodeWithValidEnvelopeChecksum(string escape)
    {
        var sourceBody = Encoding.UTF8.GetString(StrictCodec().Encode(TextDocument("marker", "component")));
        var json = sourceBody.Replace("\"value\":\"marker\"", "\"value\":\"" + escape + "\"", StringComparison.Ordinal);
        Assert.NotEqual(sourceBody, json);
        var codec = new SceneWorldCodec(StrictCodec());
        Assert.Throws<InvalidDataException>(() => codec.Decode(Envelope(Encoding.UTF8.GetBytes(json))));
    }

    [Theory]
    [InlineData("default")] [InlineData("component")]
    [InlineData("definition-override")] [InlineData("instance-override")]
    public void RoundTrip_PreservesPairedUnicodeAndNulScalarsWithoutRejectingValidText(string location)
    {
        const string text = "A\0🚪Z";
        var codec = new SceneWorldCodec(StrictCodec());
        var encoded = codec.Encode(new(TextDocument(text, location)));
        Assert.Contains("\\u0000", Encoding.UTF8.GetString(encoded));
        var decoded = codec.Decode(encoded);
        Assert.Equal(text, SceneResolver.Resolve(decoded.Scene).Entities[Root].Components[Main].Properties["text"].Value.GetString());
        Assert.Equal(encoded, codec.Encode(decoded));
    }

    [Fact]
    public void MutableEnvelopeAndIndependentEncodings_DoNotMutateDecodedDocument()
    {
        var codec = new SceneWorldCodec(StrictCodec());
        var data = new SceneWorldData(Document(true));
        var first = codec.Encode(data); var second = codec.Encode(data);
        Assert.NotSame(first, second);
        var decoded = codec.Decode(first);
        first.AsSpan().Clear();
        Assert.Equal((Scene, 19L), (decoded.Scene.Id, decoded.Scene.Revision));
        Assert.Equal("Дверь 🚪", decoded.Scene.Entities.Single(entity => entity.Id == Child).Name);
        Assert.Equal(3.5d, SceneResolver.Resolve(decoded.Scene).Entities[Child].Components[Main].Properties["x"].Value.GetNumber());
        Assert.Equal(second, codec.Encode(decoded));
    }

    private static SceneFileCodec StrictCodec()
    {
        var resolution = new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(document => new(document.Definitions, document.Schemas)));
        return new(new SceneAuthoringSessionFactory(resolution, new()));
    }

    private static SceneDocument TextDocument(string text, string location)
    {
        var schema = new ComponentSchema(MainType, 1,
            [new("text", PropertyValueKind.String, PropertyValue.From(location == "default" ? text : "fallback"))]);
        var component = new ComponentData(Main, MainType, 1,
            location == "component" ? [new("text", PropertyValue.From(text))] : []);
        var baseline = new EntityDefinition(Base, new([component]));
        var variant = new EntityDefinition(Variant, location == "definition-override"
            ? new(propertyOverrides: [new(Main, "text", PropertyValue.From(text))]) : EntityPatch.Empty, Base);
        var entity = new EntityInstance(Root, Variant, location == "instance-override"
            ? new(propertyOverrides: [new(Main, "text", PropertyValue.From(text))]) : EntityPatch.Empty);
        return new(Scene, 43, [entity], [baseline, variant], [schema]);
    }

    private static byte[] Envelope(byte[] body)
    {
        var bytes = new byte[44 + body.Length];
        "TMWL"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), body.Length);
        SHA256.HashData(body, bytes.AsSpan(12, 32));
        body.CopyTo(bytes, 44);
        return bytes;
    }

    private static SceneDocument Document(bool metadata)
    {
        var main = new ComponentSchema(MainType, 1,
        [
            new("x", PropertyValueKind.Number, PropertyValue.From(0d)),
            new("label", PropertyValueKind.String, PropertyValue.From("fallback")),
            new("note", PropertyValueKind.String, PropertyValue.Null, true),
            new("active", PropertyValueKind.Boolean, PropertyValue.From(true)),
            new("count", PropertyValueKind.Integer, PropertyValue.From(9L))
        ]);
        var extra = new ComponentSchema(ExtraType, 1, [new("value", PropertyValueKind.Integer, PropertyValue.From(2L))], requiredTypes: [MainType]);
        var baseline = new EntityDefinition(Base, new(
        [
            new(Main, MainType, 1, [new("x", PropertyValue.From(1d)), new("label", PropertyValue.From("base")), new("note", PropertyValue.From("inherited"))]),
            new(Extra, ExtraType, 1, [new("value", PropertyValue.From(11L))])
        ]), revision: 3);
        var variant = new EntityDefinition(Variant, new(removedComponents: [Extra],
            propertyOverrides: [new(Main, "x", PropertyValue.From(3.5d)), new(Main, "note", PropertyValue.Null)]), Base, 5);
        var restored = new EntityDefinition(Restored, new(restoredComponents: [Extra],
            propertyOverrides: [new(Main, "count", PropertyValue.From(long.MaxValue))]), Variant, 7);
        var root = new EntityInstance(Root, Restored,
            new(propertyOverrides: [new(Main, "x", PropertyValue.From(4.5d))]), null, metadata ? "Корень" : null, metadata ? 4 : null);
        var child = new EntityInstance(Child, Variant,
            new(restoredComponents: [Extra], propertyOverrides: [new(Extra, "value", PropertyValue.From(17L))]), Root,
            metadata ? "Дверь 🚪" : null, metadata ? 2 : null);
        return new(Scene, 19, [child, root], [restored, baseline, variant], [extra, main]);
    }
}
