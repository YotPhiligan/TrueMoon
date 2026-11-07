using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Resources;

public sealed class RenderResourceComponentTests
{
    [Fact]
    public void SceneWorld_OwnsDistinctCpuRetainsAfterRegistryRootLeaseReleases()
    {
        using var catalog = new RenderContentCatalog(); var content = ResourceTestFixture.Content();
        var root = catalog.Acquire(content); var worlds = new RecordingWorlds();
        var loader = Loader(root, worlds);
        var scene = loader.Load(Document());
        Assert.Equal(1, worlds.Calls); Assert.False(scene.World.IsDisposed);
        Assert.Equal(3, catalog.Counts.LeaseCount); Assert.Equal(1, catalog.Counts.ScopeCount);
        Assert.Equal(3, catalog.Counts.RevisionCount);
        var renderer = scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity]);
        Assert.Equal(content.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Key, renderer.Mesh);
        root.Dispose(); root.Dispose();
        Assert.Equal(2, catalog.Counts.LeaseCount); Assert.False(catalog.IsReleased);
        Assert.Equal(renderer, scene.World.GetComponent<MeshRenderer>(scene.Entities[Entity]));
        Assert.Throws<InvalidOperationException>(catalog.Dispose);
        Assert.True(catalog.Counts.AdmissionClosed); Assert.False(catalog.IsReleased);
        scene.Dispose(); scene.Dispose();
        Assert.True(worlds.Last!.IsDisposed); Assert.Equal(0, catalog.Counts.LeaseCount);
        Assert.Equal(0, catalog.Counts.RevisionCount); Assert.Equal(0L, catalog.Counts.ResidentBytes);
        catalog.Dispose(); Assert.True(catalog.IsReleased);
    }

    [Fact]
    public void InvalidInvisibleTint_IsRejectedBeforeWorldAndBeforeAnySceneLease()
    {
        using var catalog = new RenderContentCatalog(); using var root = catalog.Acquire(ResourceTestFixture.Content());
        var worlds = new RecordingWorlds(); var loader = Loader(root, worlds);
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(Document(
            [new("enabled", PropertyValue.From(false)), new("layers", PropertyValue.From(0L)),
             new("tintEnabled", PropertyValue.From(false)), new("tintR", PropertyValue.From(1.00000001))])));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.Equal(0, worlds.Calls); Assert.Equal(1, catalog.Counts.LeaseCount);
        Assert.Equal(1, catalog.Counts.ScopeCount);
    }

    [Fact]
    public void LaterImportResourceLimit_ReleasesAlreadyAcquiredCpuLeaseAndPrivateWorld()
    {
        using var catalog = new RenderContentCatalog(); using var root = catalog.Acquire(ResourceTestFixture.Content());
        var worlds = new RecordingWorlds(); var loader = Loader(root, worlds, new() { MaximumPreparedResources = 1 });
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(Document()));
        Assert.Equal(SceneWorldLoadError.LimitExceeded, error.Error);
        Assert.Equal(1, worlds.Calls); Assert.True(worlds.Last!.IsDisposed);
        Assert.Equal(1, catalog.Counts.LeaseCount); Assert.Equal(3, catalog.Counts.RevisionCount);
        using var retry = Loader(root, worlds).Load(Document());
        Assert.Equal(2, worlds.Calls); Assert.False(retry.World.IsDisposed);
        Assert.Equal(3, catalog.Counts.LeaseCount);
    }

    [Fact]
    public void ClosedOrDisposedScope_CannotAdmitAnotherWorldThroughCapturedRegistry()
    {
        using var catalog = new RenderContentCatalog(); var root = catalog.Acquire(ResourceTestFixture.Content());
        var worlds = new RecordingWorlds(); var loader = Loader(root, worlds);
        Assert.Throws<InvalidOperationException>(catalog.Dispose);
        var closed = Assert.Throws<SceneWorldLoadException>(() => loader.Load(Document()));
        Assert.Equal(SceneWorldLoadError.InvalidReference, closed.Error);
        Assert.Equal(0, worlds.Calls); Assert.Equal(1, catalog.Counts.LeaseCount);
        root.Dispose(); catalog.Dispose();
        var disposed = Assert.Throws<SceneWorldLoadException>(() => loader.Load(Document()));
        Assert.Equal(SceneWorldLoadError.InvalidReference, disposed.Error);
        Assert.Equal(0, worlds.Calls); Assert.Equal(0, catalog.Counts.LeaseCount);
    }

    private static readonly EntityId Entity = new(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0001"));
    private static SceneDocument Document(IEnumerable<KeyValuePair<string, PropertyValue>>? rendererValues = null)
    {
        var definition = new EntityDefinitionId(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0002"));
        return new(new(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0003")), 5,
            [new(Entity, definition)], [new(definition, new([
                new(new(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0004")), BuiltInComponents.TransformId, 1),
                new(new(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0005")), MeshRenderingComponents.MeshRendererId, 1, rendererValues),
                new(new(Guid.Parse("02b7b658-15c0-43d2-8476-970b46bb0006")), MeshRenderingComponents.CameraId, 1)]))],
            BuiltInComponents.Schemas.Concat(MeshRenderingComponents.Schemas));
    }
    private static SceneWorldLoader Loader(RenderManifestLease scope, RecordingWorlds worlds, SceneWorldLoadOptions? options = null)
        => new(RenderResourceComponents.Register(PrimitiveEcsComponents.Register(new()), scope).Build(), worlds,
            new(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas))), options);
    private sealed class RecordingWorlds : IFactory<World>
    {
        internal int Calls { get; private set; }
        internal World? Last { get; private set; }
        public World Create() { Calls++; return Last = new(); }
        public World Create<TData>(TData? data) => Create();
    }
}
