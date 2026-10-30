using System.Text.Json.Nodes;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

internal static class DocumentData
{
    public static Guid Id(int number) => Guid.Parse($"a846e4a2-7c35-4f21-92e8-{number:D12}");
    public static readonly EntityId Parent = new(Id(1)), Child = new(Id(2));
    public static readonly ComponentId Transform = new(Id(10)), Appearance = new(Id(11)), Interaction = new(Id(12)), Custom = new(Id(13)), Local = new(Id(14));
    public static readonly ComponentTypeId CustomType = new(Id(20));
    public static readonly EntityDefinitionId Base = new(Id(30)), Hidden = new(Id(31)), Restored = new(Id(32));
    public const string Text = "room \"A\"\n\t\\ / Мир 🛰️";

    public static SceneDocument Empty(long revision = 0) => new(new(Id(100)), revision, [], [], []);

    public static SceneDocument Scene(long revision = 7)
    {
        var customSchema = new ComponentSchema(CustomType, 2,
        [
            new("integer", PropertyValueKind.Integer, PropertyValue.From(long.MinValue)),
            new("number", PropertyValueKind.Number, PropertyValue.From(1d)),
            new("nullable", PropertyValueKind.String, PropertyValue.Null, true),
            new("text", PropertyValueKind.String, PropertyValue.From(Text)),
            new("flag", PropertyValueKind.Boolean, PropertyValue.From(true))
        ], true, [BuiltInComponents.TransformId]);
        var source = new EntityDefinition(Base, new(addedComponents:
        [
            new(Transform, BuiltInComponents.TransformId, 1, [KeyValuePair.Create("x", PropertyValue.From(2d))]),
            new(Appearance, BuiltInComponents.AppearanceId, 1),
            new(Interaction, BuiltInComponents.InteractionId, 1),
            new(Custom, CustomType, 2,
            [KeyValuePair.Create("integer", PropertyValue.From(long.MaxValue)), KeyValuePair.Create("number", PropertyValue.From(1d)),
                KeyValuePair.Create("nullable", PropertyValue.From("inherited")), KeyValuePair.Create("text", PropertyValue.From(Text))])
        ]), revision: 19);
        var hidden = new EntityDefinition(Hidden, new(removedComponents: [Interaction]), Base, 20);
        var restored = new EntityDefinition(Restored, new(restoredComponents: [Interaction], propertyOverrides:
            [new(Custom, "flag", PropertyValue.From(false))]), Hidden, 21);
        return new(new(Id(100)), revision,
        [
            new(Parent, Hidden, new(removedComponents: [Appearance])),
            new(Child, Restored, new(addedComponents: [new(Local, CustomType, 2)], propertyOverrides:
                [new(Transform, "x", PropertyValue.From(4d)), new(Custom, "nullable", PropertyValue.Null)]), Parent)
        ], [source, hidden, restored], [.. BuiltInComponents.Schemas, customSchema]);
    }

    public static IFactory<ISceneAuthoringSession, SceneDocument> Factory() => new Sessions();
    public static AuthoringOperation Operation(long revision) => new(Guid.NewGuid(), revision, "document-test");
    public static TransformEditBatch Edit(long revision, double x) => new(Operation(revision), [new(Child, Transform, new(x, 1, 2))]);
    public static double X(SceneAuthoringState state) => state.Resolved.Entities[Child].Components[Transform].Properties["x"].Value.GetNumber();
    public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static byte[] Bytes(JsonNode json) => System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());

    public static void AssertScene(SceneDocument scene)
    {
        Assert.Equal(new SceneId(Id(100)), scene.Id); Assert.Equal(7, scene.Revision);
        Assert.Equal(new[] { Parent, Child }, scene.Entities.Select(e => e.Id));
        Assert.Null(scene.Entities[0].ParentId); Assert.Equal(Parent, scene.Entities[1].ParentId);
        Assert.Equal(Hidden, scene.Entities[0].DefinitionId); Assert.Equal(Restored, scene.Entities[1].DefinitionId);
        Assert.Equal(new[] { Appearance }, scene.Entities[0].Changes.RemovedComponents);
        Assert.Equal(Local, Assert.Single(scene.Entities[1].Changes.AddedComponents).Id);
        Assert.Empty(scene.Entities[1].Changes.AddedComponents[0].Properties);
        Assert.Equal(new[] { "x", "nullable" }, scene.Entities[1].Changes.PropertyOverrides.Select(p => p.PropertyId));
        Assert.Equal(PropertyValueKind.Null, scene.Entities[1].Changes.PropertyOverrides[1].Value.Kind);
        Assert.Equal(new[] { Base, Hidden, Restored }, scene.Definitions.Select(d => d.Id));
        Assert.Equal(new long[] { 19, 20, 21 }, scene.Definitions.Select(d => d.Revision));
        Assert.Null(scene.Definitions[0].BaseDefinitionId); Assert.Equal(Base, scene.Definitions[1].BaseDefinitionId);
        Assert.Equal(Hidden, scene.Definitions[2].BaseDefinitionId);
        Assert.Equal(new[] { Interaction }, scene.Definitions[1].Changes.RemovedComponents);
        Assert.Equal(new[] { Interaction }, scene.Definitions[2].Changes.RestoredComponents);
        Assert.Equal(new[] { Transform, Appearance, Interaction, Custom }, scene.Definitions[0].Changes.AddedComponents.Select(c => c.Id));
        var data = scene.Definitions[0].Changes.AddedComponents[3];
        Assert.Equal(CustomType, data.TypeId); Assert.Equal(2, data.SchemaVersion);
        Assert.Equal(PropertyValueKind.Integer, data.Properties["integer"].Kind); Assert.Equal(long.MaxValue, data.Properties["integer"].GetInteger());
        Assert.Equal(PropertyValueKind.Number, data.Properties["number"].Kind); Assert.Equal(1d, data.Properties["number"].GetNumber());
        Assert.Equal(Text, data.Properties["text"].GetString()); Assert.Equal("inherited", data.Properties["nullable"].GetString());
        Assert.Equal(4, scene.Schemas.Length);
        var schema = scene.Schemas.Single(s => s.TypeId == CustomType);
        Assert.Equal(2, schema.Version); Assert.True(schema.AllowMultiple); Assert.Equal(new[] { BuiltInComponents.TransformId }, schema.RequiredTypes);
        Assert.Equal(5, schema.Properties.Count); Assert.True(schema.Properties["nullable"].IsNullable);
        Assert.Equal(PropertyValueKind.String, schema.Properties["nullable"].Kind); Assert.Equal(PropertyValueKind.Null, schema.Properties["nullable"].DefaultValue.Kind);
        Assert.Equal(long.MinValue, schema.Properties["integer"].DefaultValue.GetInteger());
        Assert.Equal(1d, schema.Properties["number"].DefaultValue.GetNumber());
        Assert.True(schema.Properties["flag"].DefaultValue.GetBoolean());
        Assert.False(Assert.Single(scene.Definitions[2].Changes.PropertyOverrides).Value.GetBoolean());
    }

    public sealed class Resolvers : IFactory<EntityResolver, SceneDocument>
    {
        public EntityResolver Create(SceneDocument arguments) => new(arguments.Definitions, arguments.Schemas);
    }

    public sealed class Sessions : IFactory<ISceneAuthoringSession, SceneDocument>, IDisposable
    {
        private readonly SceneAuthoringSessionFactory _inner = new(new(new Resolvers()), new());
        public List<Product> Products { get; } = [];
        public int Disposals { get; private set; }
        public Func<SceneDocument, ISceneAuthoringSession>? CreateSession { get; set; }
        public ISceneAuthoringSession Create(SceneDocument arguments)
        {
            var value = new Product(CreateSession?.Invoke(arguments) ?? _inner.Create(arguments));
            Products.Add(value); return value;
        }
        public void Dispose() => Disposals++;
    }

    public sealed class Product(ISceneAuthoringSession inner) : ISceneAuthoringSession
    {
        public int Disposals { get; private set; }
        public Action? BeforeRead { get; set; }
        public Action? BeforeDispose { get; set; }
        public SceneAuthoringState Read() { BeforeRead?.Invoke(); return inner.Read(); }
        public SceneAuthoringResult Apply(TransformEditBatch batch, CancellationToken cancellationToken = default) => inner.Apply(batch, cancellationToken);
        public SceneAuthoringResult Reparent(ReparentBatch batch, CancellationToken cancellationToken = default) => inner.Reparent(batch, cancellationToken);
        public SceneAuthoringResult EditComponents(ComponentEditBatch batch, CancellationToken cancellationToken = default) => inner.EditComponents(batch, cancellationToken);
        public SceneAuthoringResult EditHierarchy(HierarchyEditBatch batch, CancellationToken cancellationToken = default) => inner.EditHierarchy(batch, cancellationToken);
        public SceneAuthoringResult EditEntities(EntityEditBatch batch, CancellationToken cancellationToken = default) => inner.EditEntities(batch, cancellationToken);
        public SceneAuthoringResult Undo(AuthoringOperation operation, CancellationToken cancellationToken = default) => inner.Undo(operation, cancellationToken);
        public SceneAuthoringResult Redo(AuthoringOperation operation, CancellationToken cancellationToken = default) => inner.Redo(operation, cancellationToken);
        public void Dispose() { Disposals++; BeforeDispose?.Invoke(); inner.Dispose(); }
    }
}

internal sealed class DocumentStore : ISceneDocumentStore, IDisposable
{
    public SceneFile File { get; set; } = new(DocumentData.Scene(), new("observed-file"));
    public List<string> Reads { get; } = [];
    public List<(string Path, SceneDocument Document, SceneFileStamp? Expected)> Writes { get; } = [];
    public Func<string, CancellationToken, Task<SceneFile>>? Read { get; set; }
    public Func<string, SceneDocument, SceneFileStamp?, CancellationToken, Task<SceneFileStamp>>? Write { get; set; }
    public int Disposals { get; private set; }
    public Task<SceneFile> ReadAsync(string path, CancellationToken cancellationToken = default)
    { Reads.Add(path); return Read?.Invoke(path, cancellationToken) ?? Task.FromResult(File); }
    public Task<SceneFileStamp> WriteAsync(string path, SceneDocument document, SceneFileStamp? expected, CancellationToken cancellationToken = default)
    {
        Writes.Add((path, document, expected));
        return Write?.Invoke(path, document, expected, cancellationToken) ?? Task.FromResult(new SceneFileStamp("written-" + Writes.Count));
    }
    public void Dispose() => Disposals++;
}

internal sealed class DocumentFiles : IDisposable
{
    private readonly Projects.ProjectFixture _project = new();
    public string Root => _project.Root;
    public ResolvedProject Project => _project.Resolved;
    public string ScenePath => Path.Combine(Project.ScenesDirectory, "room.aetherscene");
    public void Dispose() => _project.Dispose();
}
