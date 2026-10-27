using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class SceneFileCodecTests
{
    [Fact]
    public void RoundTrip_PreservesRawCatalogsInheritanceMasksRestorationParentsAndExactTaggedScalars()
    {
        var sessions = new DocumentData.Sessions(); var codec = new SceneFileCodec(sessions);
        var source = DocumentData.Scene(); var bytes = codec.Encode(source);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        using (var json = JsonDocument.Parse(bytes))
        {
            Assert.Equal(1, json.RootElement.GetProperty("formatVersion").GetInt32());
            var properties = json.RootElement.GetProperty("definitions")[0].GetProperty("changes").GetProperty("added")[3].GetProperty("properties");
            Assert.Equal("integer", properties.GetProperty("integer").GetProperty("kind").GetString());
            Assert.Equal(long.MaxValue, properties.GetProperty("integer").GetProperty("value").GetInt64());
            Assert.Equal("number", properties.GetProperty("number").GetProperty("kind").GetString());
            Assert.Equal("null", json.RootElement.GetProperty("entities")[1].GetProperty("changes").GetProperty("overrides")[1].GetProperty("value").GetProperty("kind").GetString());
            Assert.False(json.RootElement.TryGetProperty("resolved", out _)); Assert.False(json.RootElement.TryGetProperty("undo", out _));
        }
        var result = codec.Decode(bytes);
        DocumentData.AssertScene(result); DocumentData.AssertScene(source);
        Assert.NotSame(source, result); Assert.NotSame(source.Definitions[0], result.Definitions[0]);
        Assert.Equal(bytes, codec.Encode(result));
        Assert.Equal(3, sessions.Products.Count); Assert.All(sessions.Products, product => Assert.Equal(1, product.Disposals));
        Assert.Equal(0, sessions.Disposals);
        using var authoring = DocumentData.Factory().Create(result);
        var state = authoring.Read();
        Assert.Equal((7L, 0, 0), (state.ContentRevision, state.UndoCount, state.RedoCount));
        Assert.Equal(4d, DocumentData.X(state));
        Assert.False(state.Resolved.Entities[DocumentData.Parent].Components.ContainsKey(DocumentData.Appearance));
        Assert.True(state.Resolved.Entities[DocumentData.Child].Components.ContainsKey(DocumentData.Interaction));
        Assert.Equal(PropertyValueKind.Null, state.Resolved.Entities[DocumentData.Child].Components[DocumentData.Custom].Properties["nullable"].Value.Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Decode_EmptyCatalogsAndOptionalBomRetainIdentityAndMaxRevision(bool bom)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var bytes = codec.Encode(DocumentData.Empty(long.MaxValue));
        if (bom) bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        var result = codec.Decode(bytes);
        Assert.Equal(new SceneId(DocumentData.Id(100)), result.Id); Assert.Equal(long.MaxValue, result.Revision);
        Assert.Empty(result.Entities); Assert.Empty(result.Definitions); Assert.Empty(result.Schemas);
    }

    [Theory]
    [InlineData("root")] [InlineData("entity")] [InlineData("definition")] [InlineData("patch")]
    [InlineData("component")] [InlineData("schema")] [InlineData("propertySchema")] [InlineData("override")] [InlineData("scalar")]
    public void Decode_RejectsUnknownDuplicateMissingAndWrongCaseMembersAtEveryStructuredLevel(string level)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var pristine = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        var selected = Select(pristine, level); var first = selected.First();
        var unknown = pristine.DeepClone(); Select(unknown, level)["unexpected"] = 1;
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(unknown)));
        var missing = pristine.DeepClone(); Select(missing, level).Remove(first.Key);
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(missing)));
        var wrongCase = pristine.DeepClone(); var target = Select(wrongCase, level);
        target.Remove(first.Key); target[char.ToUpperInvariant(first.Key[0]) + first.Key[1..]] = first.Value?.DeepClone();
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(wrongCase)));
        var text = pristine.ToJsonString(); var original = selected.ToJsonString();
        var duplicate = original.Insert(1, JsonSerializer.Serialize(first.Key) + ":" + first.Value!.ToJsonString() + ",");
        Assert.Throws<InvalidDataException>(() => codec.Decode(Encoding.UTF8.GetBytes(text.Replace(original, duplicate, StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("[]")] [InlineData("{")]
    [InlineData("{\"kind\":\"Integer\",\"value\":1}")]
    [InlineData("{\"kind\":\"object\",\"value\":{}}")]
    [InlineData("{\"kind\":\"integer\",\"value\":1.5}")]
    [InlineData("{\"kind\":\"integer\",\"value\":9223372036854775808}")]
    [InlineData("{\"kind\":\"number\",\"value\":1e400}")]
    [InlineData("{\"kind\":\"boolean\",\"value\":1}")]
    [InlineData("{\"kind\":\"string\",\"value\":null}")]
    [InlineData("{\"kind\":\"null\",\"value\":null}")]
    public void Decode_RejectsMalformedScalarTagsWithoutCoercion(string scalar)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var json = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        var properties = json["definitions"]![0]!["changes"]!["added"]![3]!["properties"]!.AsObject();
        var text = json.ToJsonString(); var old = properties["integer"]!.ToJsonString();
        var changed = text.Replace(old, scalar, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => codec.Decode(Encoding.UTF8.GetBytes(changed)));
    }

    [Theory]
    [InlineData("formatVersion", "0")] [InlineData("formatVersion", "2")] [InlineData("formatVersion", "1.5")]
    [InlineData("revision", "-1")] [InlineData("revision", "9223372036854775808")]
    [InlineData("id", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("id", "\"not-a-guid\"")] [InlineData("entities", "null")]
    public void Decode_RejectsInvalidVersionIdentityRevisionOrContainer(string field, string raw)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var json = JsonNode.Parse(codec.Encode(DocumentData.Empty()))!;
        json[field] = JsonNode.Parse(raw);
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
    }

    [Fact]
    public void Codec_RejectsInvalidGraphBeforeReturningOrPublishingBytesAndAllowsLaterValidWork()
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var source = DocumentData.Scene();
        var missingParent = new SceneDocument(source.Id, source.Revision,
            [new(DocumentData.Child, DocumentData.Base, parentId: new(DocumentData.Id(999)))], source.Definitions, source.Schemas);
        Assert.Throws<SceneResolutionException>(() => codec.Encode(missingParent));
        var bytes = codec.Encode(source); var json = JsonNode.Parse(bytes)!;
        json["entities"]![1]!["parentId"] = DocumentData.Id(999).ToString("D");
        var error = Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
        Assert.IsType<SceneResolutionException>(error.InnerException);
        DocumentData.AssertScene(codec.Decode(bytes));
    }

    [Theory]
    [InlineData("entities", 4096)] [InlineData("definitions", 4096)] [InlineData("schemas", 1024)]
    public void Codec_EnforcesCatalogLimitsExactlyBeforeAndAfterBoundary(string collection, int maximum)
    {
        var codec = new SceneFileCodec(DocumentData.Factory());
        var definitions = collection == "definitions"
            ? Enumerable.Range(0, maximum).Select(i => new EntityDefinition(new(DocumentData.Id(10000 + i)), EntityPatch.Empty)).ToArray()
            : new[] { new EntityDefinition(DocumentData.Base, EntityPatch.Empty) };
        var entities = collection == "entities"
            ? Enumerable.Range(0, maximum).Select(i => new EntityInstance(new(DocumentData.Id(20000 + i)), DocumentData.Base)).ToArray()
            : Array.Empty<EntityInstance>();
        var schemas = collection == "schemas"
            ? Enumerable.Range(0, maximum).Select(i => new ComponentSchema(new(DocumentData.Id(30000 + i)), 1, [])).ToArray()
            : Array.Empty<ComponentSchema>();
        var accepted = new SceneDocument(new(DocumentData.Id(100)), 0, entities, definitions, schemas);
        var bytes = codec.Encode(accepted); var decoded = codec.Decode(bytes);
        Assert.Equal(entities.Length, decoded.Entities.Length); Assert.Equal(definitions.Length, decoded.Definitions.Length); Assert.Equal(schemas.Length, decoded.Schemas.Length);
        var json = JsonNode.Parse(bytes)!; var array = json[collection]!.AsArray(); array.Add(array[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
        var overflow = collection switch
        {
            "entities" => new SceneDocument(accepted.Id, 0, entities.Append(new(new(DocumentData.Id(999999)), DocumentData.Base)), definitions, schemas),
            "definitions" => new SceneDocument(accepted.Id, 0, entities, definitions.Append(new(new(DocumentData.Id(999999)), EntityPatch.Empty)), schemas),
            _ => new SceneDocument(accepted.Id, 0, entities, definitions, schemas.Append(new(new(DocumentData.Id(999999)), 1, [])))
        };
        Assert.Throws<InvalidDataException>(() => codec.Encode(overflow));
    }

    [Theory]
    [InlineData("added", 256)] [InlineData("removed", 256)] [InlineData("restored", 256)] [InlineData("overrides", 1024)]
    public void Decode_RejectsPatchCollectionsPastTheirBoundBeforeSemanticResolution(string name, int maximum)
    {
        var sessions = new DocumentData.Sessions(); var codec = new SceneFileCodec(sessions); var json = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        var patch = json["definitions"]![0]!["changes"]!; var values = new JsonArray();
        for (var i = 0; i <= maximum; i++) values.Add(name is "removed" or "restored"
            ? JsonValue.Create(DocumentData.Id(40000 + i).ToString("D"))
            : name == "added" ? patch["added"]![0]!.DeepClone() : json["entities"]![1]!["changes"]!["overrides"]![0]!.DeepClone());
        patch[name] = values;
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(json)));
        Assert.Single(sessions.Products); Assert.Equal(1, sessions.Products[0].Disposals);
    }

    [Theory]
    [InlineData("added")] [InlineData("removed")] [InlineData("restored")] [InlineData("overrides")] [InlineData("properties")] [InlineData("requiredTypes")]
    public void Codec_AcceptsExactPatchPropertyAndDependencyBoundsWithoutFlatteningThem(string kind)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var schemaId = new ComponentTypeId(DocumentData.Id(50000));
        var properties = Enumerable.Range(0, 256).Select(i => new ComponentPropertySchema("p" + i, PropertyValueKind.Number, PropertyValue.From(0d))).ToArray();
        var schema = new ComponentSchema(schemaId, 1, kind is "overrides" or "properties" ? properties : [], true);
        var added = Enumerable.Range(0, kind == "overrides" ? 4 : 256)
            .Select(i => new ComponentData(new(DocumentData.Id(51000 + i)), schemaId, 1,
                kind == "properties" && i == 0 ? properties.Select(p => KeyValuePair.Create(p.Id, PropertyValue.From(3d))) : null)).ToArray();
        var baseDefinition = new EntityDefinition(DocumentData.Base, new(addedComponents: added));
        var definitions = new List<EntityDefinition> { baseDefinition };
        var target = DocumentData.Base;
        if (kind is "removed" or "restored")
        {
            definitions.Add(new(DocumentData.Hidden, new(removedComponents: added.Select(c => c.Id)), DocumentData.Base)); target = DocumentData.Hidden;
            if (kind == "restored") { definitions.Add(new(DocumentData.Restored, new(restoredComponents: added.Select(c => c.Id)), DocumentData.Hidden)); target = DocumentData.Restored; }
        }
        var changes = kind == "overrides" ? new EntityPatch(propertyOverrides: added.SelectMany(c => properties.Select(p => new ComponentPropertyOverride(c.Id, p.Id, PropertyValue.From(2d))))) : EntityPatch.Empty;
        var schemas = new List<ComponentSchema> { schema };
        var entities = new[] { new EntityInstance(DocumentData.Child, target, changes) };
        if (kind == "requiredTypes")
        {
            var dependencies = Enumerable.Range(0, 256).Select(i => new ComponentTypeId(DocumentData.Id(52000 + i))).ToArray();
            schemas = dependencies.Select(id => new ComponentSchema(id, 1, [])).ToList();
            schemas.Add(new(schemaId, 1, [], requiredTypes: dependencies)); entities = [];
        }
        var source = new SceneDocument(new(DocumentData.Id(100)), 0, entities, definitions, schemas);
        var bytes = codec.Encode(source); var decoded = codec.Decode(bytes);
        Assert.Equal(bytes, codec.Encode(decoded));
        Assert.Equal(kind == "overrides" ? 1024 : 0, decoded.Entities.FirstOrDefault()?.Changes.PropertyOverrides.Length ?? 0);
        if (kind == "removed") Assert.Equal(256, decoded.Definitions[1].Changes.RemovedComponents.Count);
        else if (kind == "restored") Assert.Equal(256, decoded.Definitions[2].Changes.RestoredComponents.Count);
        else if (kind == "properties") Assert.Equal(256, decoded.Definitions[0].Changes.AddedComponents[0].Properties.Count);
        else if (kind == "requiredTypes") Assert.Equal(256, decoded.Schemas.Single(s => s.TypeId == schemaId).RequiredTypes.Count);
        else Assert.Equal(kind == "overrides" ? 4 : 256, decoded.Definitions[0].Changes.AddedComponents.Length);
    }

    [Theory]
    [InlineData("schema-properties")] [InlineData("component-properties")] [InlineData("requiredTypes")]
    public void Decode_RejectsPropertyAndDependencyCollectionsPast256BeforeCreatingSession(string kind)
    {
        var sessions = new DocumentData.Sessions(); var codec = new SceneFileCodec(sessions); var root = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        if (kind == "component-properties")
        {
            var properties = root["definitions"]![0]!["changes"]!["added"]![3]!["properties"]!.AsObject(); properties.Clear();
            for (var i = 0; i <= 256; i++) properties["p" + i] = new JsonObject { ["kind"] = "number", ["value"] = i };
        }
        else
        {
            var key = kind == "requiredTypes" ? "requiredTypes" : "properties";
            var array = root["schemas"]![3]![key]!.AsArray(); var exemplar = array[0]!.DeepClone(); array.Clear();
            for (var i = 0; i <= 256; i++)
            {
                var value = kind == "requiredTypes" ? JsonValue.Create(DocumentData.Id(60000 + i).ToString("D"))! : exemplar.DeepClone();
                if (kind != "requiredTypes") value["id"] = "p" + i;
                array.Add(value);
            }
        }
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(root))); Assert.Single(sessions.Products);
    }

    [Theory]
    [InlineData("added")] [InlineData("removed")] [InlineData("restored")] [InlineData("overrides")]
    [InlineData("schema-properties")] [InlineData("component-properties")] [InlineData("requiredTypes")]
    public void Encode_RejectsOversizedLocalCollectionsEvenWhenTheEngineCanResolveTheirContent(string kind)
    {
        var type = new ComponentTypeId(DocumentData.Id(80000)); var properties = Enumerable.Range(0, kind.Contains("properties", StringComparison.Ordinal) ? 257 : kind == "overrides" ? 205 : 0)
            .Select(i => new ComponentPropertySchema("p" + i, PropertyValueKind.Number, PropertyValue.From(0d))).ToArray();
        var schema = new ComponentSchema(type, 1, properties, true);
        var count = kind == "overrides" ? 5 : kind.Contains("properties", StringComparison.Ordinal) ? 1 : 257;
        var components = Enumerable.Range(0, count).Select(i => new ComponentData(new(DocumentData.Id(81000 + i)), type, 1,
            kind == "component-properties" ? properties.Select(p => KeyValuePair.Create(p.Id, p.DefaultValue)) : null)).ToArray();
        var definitions = new List<EntityDefinition> { new(DocumentData.Base, new(addedComponents: components.Take(kind == "added" ? 257 : 256))) };
        var target = DocumentData.Base;
        if (kind is "removed" or "restored")
        {
            definitions.Add(new(DocumentData.Hidden, new(addedComponents: components.Skip(256)), DocumentData.Base));
            definitions.Add(new(DocumentData.Restored, new(removedComponents: components.Take(kind == "restored" ? 256 : 257).Select(c => c.Id)), DocumentData.Hidden)); target = DocumentData.Restored;
            if (kind == "restored")
            {
                var masked = new EntityDefinitionId(DocumentData.Id(80001));
                definitions.Add(new(masked, new(removedComponents: components.Skip(256).Select(c => c.Id)), DocumentData.Restored));
                target = new(DocumentData.Id(80002)); definitions.Add(new(target, new(restoredComponents: components.Select(c => c.Id)), masked));
            }
        }
        var changes = kind == "overrides" ? new EntityPatch(propertyOverrides: components.SelectMany(c => properties.Select(p => new ComponentPropertyOverride(c.Id, p.Id, p.DefaultValue)))) : EntityPatch.Empty;
        var entities = new[] { new EntityInstance(DocumentData.Child, target, changes) }; var schemas = new List<ComponentSchema> { schema };
        if (kind == "requiredTypes")
        {
            var ids = Enumerable.Range(0, 257).Select(i => new ComponentTypeId(DocumentData.Id(82000 + i))).ToArray();
            schemas = ids.Select(id => new ComponentSchema(id, 1, [])).ToList(); schemas.Add(new(type, 1, [], requiredTypes: ids));
            entities = []; definitions = [];
        }
        var source = new SceneDocument(new(DocumentData.Id(100)), 0, entities, definitions, schemas);
        using var independentlyValid = DocumentData.Factory().Create(source); Assert.Equal(entities.Length, independentlyValid.Read().Resolved.Entities.Count);
        var codec = new SceneFileCodec(DocumentData.Factory()); var error = Assert.Throws<InvalidDataException>(() => codec.Encode(source));
        Assert.Contains(kind == "component-properties" ? "Component property limit" : "collection limit", error.Message);
    }

    [Fact]
    public void Codec_EnforcesStringAndEncodedByteBoundsBeforeReturningAnyBytes()
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var root = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        var property = root["definitions"]![0]!["changes"]!["added"]![3]!["properties"]!["text"]!;
        property["value"] = new string('x', PropertyValue.MaximumStringLength);
        Assert.Equal(PropertyValue.MaximumStringLength, codec.Decode(DocumentData.Bytes(root)).Definitions[0].Changes.AddedComponents[3].Properties["text"].GetString().Length);
        property["value"] = new string('x', PropertyValue.MaximumStringLength + 1);
        Assert.Throws<InvalidDataException>(() => codec.Decode(DocumentData.Bytes(root)));
        var type = new ComponentTypeId(DocumentData.Id(70000));
        var properties = Enumerable.Range(0, 256).Select(i => new ComponentPropertySchema("p" + i, PropertyValueKind.String, PropertyValue.From(new string('x', 4096)))).ToArray();
        var schema = new ComponentSchema(type, 1, properties, true);
        var data = properties.Select(p => KeyValuePair.Create(p.Id, p.DefaultValue)).ToArray();
        var components = Enumerable.Range(0, 4).Select(i => new ComponentData(new(DocumentData.Id(71000 + i)), type, 1, data));
        var document = new SceneDocument(new(DocumentData.Id(100)), 0, [new(DocumentData.Child, DocumentData.Base)], [new(DocumentData.Base, new(addedComponents: components))], [schema]);
        Assert.Throws<InvalidDataException>(() => codec.Encode(document));
        Assert.Empty(codec.Decode(codec.Encode(DocumentData.Empty())).Entities);
    }

    [Theory]
    [InlineData("removed")] [InlineData("restored")] [InlineData("override")] [InlineData("properties")] [InlineData("requiredTypes")]
    public void Decode_RejectsDuplicateSetAddressesAndPropertyNames(string kind)
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var json = JsonNode.Parse(codec.Encode(DocumentData.Scene()))!;
        string? duplicateObject = null;
        if (kind == "properties")
        {
            var properties = json["definitions"]![0]!["changes"]!["added"]![3]!["properties"]!;
            var original = properties.ToJsonString(); var pair = properties.AsObject().First();
            duplicateObject = json.ToJsonString().Replace(original, original.Insert(1, JsonSerializer.Serialize(pair.Key) + ":" + pair.Value!.ToJsonString() + ","), StringComparison.Ordinal);
        }
        else
        {
            var array = kind switch
            {
                "removed" => json["definitions"]![1]!["changes"]!["removed"]!.AsArray(),
                "restored" => json["definitions"]![2]!["changes"]!["restored"]!.AsArray(),
                "override" => json["entities"]![1]!["changes"]!["overrides"]!.AsArray(),
                _ => json["schemas"]![3]!["requiredTypes"]!.AsArray()
            };
            array.Add(array[0]!.DeepClone());
        }
        Assert.Throws<InvalidDataException>(() => codec.Decode(duplicateObject == null ? DocumentData.Bytes(json) : Encoding.UTF8.GetBytes(duplicateObject)));
    }

    [Fact]
    public void Decode_EnforcesByteLimitIncludingBomAndRejectsMalformedDepth()
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var bytes = codec.Encode(DocumentData.Empty());
        var exact = new byte[SceneFileCodec.MaximumBytes]; exact.AsSpan().Fill((byte)' '); bytes.CopyTo(exact, 0);
        Assert.Empty(codec.Decode(exact).Entities);
        Assert.Throws<InvalidDataException>(() => codec.Decode(new byte[SceneFileCodec.MaximumBytes + 1]));
        var withBom = new byte[SceneFileCodec.MaximumBytes]; withBom.AsSpan().Fill((byte)' ');
        withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF; bytes.CopyTo(withBom, 3);
        Assert.Empty(codec.Decode(withBom).Entities);
        byte[] tooLong = [.. withBom, (byte)' '];
        Assert.Throws<InvalidDataException>(() => codec.Decode(tooLong));
        var deep = Encoding.UTF8.GetBytes(new string('[', 33) + "0" + new string(']', 33));
        Assert.Throws<InvalidDataException>(() => codec.Decode(deep));
    }

    [Fact]
    public void Codec_CancellationBeforeAndDuringValidationReleasesTemporaryProductWithoutReturningSuccess()
    {
        var sessions = new DocumentData.Sessions(); var codec = new SceneFileCodec(sessions); var bytes = codec.Encode(DocumentData.Scene());
        using var pre = new CancellationTokenSource(); pre.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => codec.Encode(DocumentData.Scene(), pre.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => codec.Decode(bytes, pre.Token));
        Assert.Single(sessions.Products);
        using var cancellation = new CancellationTokenSource();
        sessions.CreateSession = document =>
        {
            var inner = DocumentData.Factory().Create(document);
            return new DocumentData.Product(inner) { BeforeRead = cancellation.Cancel };
        };
        Assert.ThrowsAny<OperationCanceledException>(() => codec.Decode(bytes, cancellation.Token));
        Assert.Equal(2, sessions.Products.Count); Assert.All(sessions.Products, product => Assert.Equal(1, product.Disposals));
        Assert.Equal(0, sessions.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Codec_ValidationReadFailureDisposesItsTemporaryProductAndPreservesOriginalFailure(bool decode)
    {
        var sessions = new DocumentData.Sessions(); var codec = new SceneFileCodec(sessions); var bytes = codec.Encode(DocumentData.Scene());
        var failure = new IOException("validation collaborator failed");
        sessions.CreateSession = document => new DocumentData.Product(DocumentData.Factory().Create(document)) { BeforeRead = () => throw failure };
        var actual = Assert.Throws<IOException>(() => { if (decode) codec.Decode(bytes); else codec.Encode(DocumentData.Scene()); });
        Assert.Same(failure, actual); Assert.Equal(2, sessions.Products.Count); Assert.All(sessions.Products, p => Assert.Equal(1, p.Disposals));
        Assert.Equal(0, sessions.Disposals);
    }

    [Fact]
    public void Codec_RejectsNullValidationProductAndNullConstructorDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneFileCodec(null!));
        Assert.Throws<ArgumentNullException>(() => new JsonSceneDocumentStore(null!));
        var valid = new SceneFileCodec(DocumentData.Factory()).Encode(DocumentData.Empty());
        var codec = new SceneFileCodec(new Factory<ISceneAuthoringSession, SceneDocument>(_ => null!));
        Assert.Equal("Authoring factory returned null.", Assert.Throws<InvalidOperationException>(() => codec.Encode(DocumentData.Empty())).Message);
        var decoded = Assert.Throws<InvalidDataException>(() => codec.Decode(valid));
        Assert.IsType<InvalidOperationException>(decoded.InnerException);
    }

    private static JsonObject Select(JsonNode root, string level) => (level switch
    {
        "root" => root,
        "entity" => root["entities"]![1]!,
        "definition" => root["definitions"]![0]!,
        "patch" => root["definitions"]![0]!["changes"]!,
        "component" => root["definitions"]![0]!["changes"]!["added"]![3]!,
        "schema" => root["schemas"]![3]!,
        "propertySchema" => root["schemas"]![3]!["properties"]![0]!,
        "override" => root["entities"]![1]!["changes"]!["overrides"]![0]!,
        _ => root["definitions"]![0]!["changes"]!["added"]![3]!["properties"]!["integer"]!
    }).AsObject();
}
