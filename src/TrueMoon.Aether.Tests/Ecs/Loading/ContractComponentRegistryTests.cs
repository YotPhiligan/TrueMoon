using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using Xunit;
using A = TrueMoon.Aether.Scenes;
using ComponentData = TrueMoon.Aether.Scenes.ComponentData;
using ComponentSchema = TrueMoon.Aether.Scenes.ComponentSchema;
using EntityPatch = TrueMoon.Aether.Scenes.EntityPatch;
using SceneDocument = TrueMoon.Aether.Scenes.SceneDocument;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class ContractComponentRegistryTests
{
    private readonly record struct Value(long Number);
    private readonly record struct Referenced(string Text);
    private static ComponentSchema Schema => new(new(LoadFixture.Type.Value), 3,
    [new("integer", PropertyValueKind.Integer, PropertyValue.From(long.MinValue)),
     new("boolean", PropertyValueKind.Boolean, PropertyValue.From(true)),
     new("number", PropertyValueKind.Number, PropertyValue.From(1.25)),
     new("text", PropertyValueKind.String, PropertyValue.From("строка Ω")),
     new("nullable", PropertyValueKind.String, PropertyValue.Null, true)]);

    [Fact]
    public void ExactNeutralSchema_ImportsDetachedEffectiveDefaultsAndOverrides_IntoTypedPool()
    {
        ComponentData? captured = null;
        var registry = new ComponentRegistryBuilder().RegisterValues(Schema, new Factory<Value, ComponentData>(data => { captured = data; return new(data.Properties["integer"].GetInteger()); })).Build();
        var schema = Assert.Single(registry.Schemas); Assert.Equal(3, schema.Version); Assert.Equal(LoadFixture.Type, schema.TypeId);
        var source = LoadFixture.Scene(schema);
        var entity = source.Entities[0];
        var componentId = source.Definitions[0].Changes.AddedComponents[0].Id;
        var scene = new SceneDocument(source.Id, source.Revision,
            [new(entity.Id, entity.DefinitionId, new EntityPatch(propertyOverrides: [new(componentId, "integer", PropertyValue.From(17L))]))], source.Definitions, source.Schemas);
        var loader = new SceneWorldLoader(registry, LoadFixture.Worlds(), LoadFixture.Resolution());
        using (var loaded = loader.Load(scene)) Assert.Equal(17L, loaded.World.GetComponent<Value>(loaded.Entities[entity.Id]).Number);
        Assert.NotNull(captured); Assert.Equal(componentId.Value, captured.Id.Value); Assert.Equal(schema.TypeId.Value, captured.TypeId.Value); Assert.Equal(3, captured.SchemaVersion);
        Assert.Equal(17L, captured.Properties["integer"].GetInteger()); Assert.True(captured.Properties["boolean"].GetBoolean());
        Assert.Equal(1.25, captured.Properties["number"].GetNumber()); Assert.Equal("строка Ω", captured.Properties["text"].GetString());
        Assert.Equal(PropertyValueKind.Null, captured.Properties["nullable"].Kind); Assert.Equal(5, captured.Properties.Count);
    }

    [Fact]
    public void SchemaDrift_IsRejectedBeforeValueFactoryOrWorldCreation()
    {
        var calls = 0; var worlds = 0;
        var registry = new ComponentRegistryBuilder().RegisterValues(Schema, new Factory<Value, ComponentData>(_ => { calls++; return new(1); })).Build();
        var changed = new ComponentSchema(LoadFixture.Type, 3, [new("integer", PropertyValueKind.Integer, PropertyValue.From(0L))]);
        var loader = new SceneWorldLoader(registry, new Factory<World>(() => { worlds++; return new(); }), LoadFixture.Resolution());
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(changed)));
        Assert.Equal(SceneWorldLoadError.SchemaMismatch, error.Error); Assert.Equal((0, 0), (calls, worlds));
    }

    [Fact]
    public void ValueFactoryFailure_DisposesPrivateWorld_AndPreservesImporterCause()
    {
        World? world = null; var marker = new IOException("neutral importer marker");
        var registry = new ComponentRegistryBuilder().RegisterValues(Schema, new Factory<Value, ComponentData>(_ => throw marker)).Build();
        var loader = new SceneWorldLoader(registry, new Factory<World>(() => world = new()), LoadFixture.Resolution());
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(Assert.Single(registry.Schemas))));
        Assert.Equal(SceneWorldLoadError.ImportFailed, error.Error); Assert.Same(marker, error.InnerException); Assert.True(world!.IsDisposed);
    }

    [Fact]
    public void NeutralRegistration_RejectsManagedComponentsMultiplicityAndAmbiguousMappings()
    {
        Assert.Throws<NotSupportedException>(() => new ComponentRegistryBuilder().RegisterValues(Schema, new Factory<Referenced, ComponentData>(_ => new("text"))));
        var multiple = new ComponentSchema(Schema.TypeId, 3, Schema.Properties.Values, allowMultiple: true);
        Assert.Throws<NotSupportedException>(() => new ComponentRegistryBuilder().RegisterValues(multiple, new Factory<Value, ComponentData>(_ => new(1))));
        var builder = new ComponentRegistryBuilder().RegisterValues(Schema, new Factory<Value, ComponentData>(_ => new(1)));
        Assert.Throws<ArgumentException>(() => builder.RegisterValues(Schema, new Factory<Value, ComponentData>(_ => new(2))));
        var other = new ComponentSchema(new(Guid.NewGuid()), 3, Schema.Properties.Values);
        Assert.Throws<ArgumentException>(() => builder.RegisterValues(other, new Factory<Value, ComponentData>(_ => new(3))));
        Assert.Equal(1, builder.Build().Count);
    }
}
