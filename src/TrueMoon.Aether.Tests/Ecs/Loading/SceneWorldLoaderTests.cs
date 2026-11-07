using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class SceneWorldLoaderTests
{
    [Fact]
    public void Load_ResolvesInheritanceForwardReferencesHierarchyAndIndependentWorlds()
    {
        var third = LoadFixture.Entity(3);
        var variant = new EntityDefinitionId(Guid.Parse("a1000000-0000-0000-0000-000000000005"));
        var schema = LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(7L)),
            LoadFixture.Property("target", PropertyValue.Null, true));
        var document = new SceneDocument(LoadFixture.SceneId, 19,
        [
            new(third, variant, null, null, "Other", 1),
            new(LoadFixture.First, variant, new(propertyOverrides: [new(LoadFixture.Component, "value", PropertyValue.From(23L))]), LoadFixture.Second, "Child", 0),
            new(LoadFixture.Second, LoadFixture.Definition, null, null, "Root", 0)
        ],
        [
            new(LoadFixture.Definition, new([new(LoadFixture.Component, LoadFixture.Type, 1,
                [new("value", PropertyValue.From(11L)), new("target", PropertyValue.From(LoadFixture.First.Value.ToString("D")))])]), revision: 11),
            new(variant, new(propertyOverrides: [new(LoadFixture.Component, "value", PropertyValue.From(17L))]), LoadFixture.Definition, revision: 12)
        ], [schema]);
        var imports = new List<EntityId>();
        var registry = new ComponentRegistryBuilder().Register(schema, context =>
        {
            imports.Add(context.SourceEntity.Id);
            Assert.Equal(LoadFixture.First, context.GetEntityData("target")!.Id);
            return new LinkedValue(context.GetInt32("value"), context.GetEntityReference("target"), context.Parent);
        }, [new("target", ComponentReferenceKind.Entity)]).Build();
        var loader = Loader(registry);
        using var first = loader.Load(document);
        using var second = loader.Create(new(new SceneWorldData(document)));
        var root = first.Entities[LoadFixture.Second]; var child = first.Entities[LoadFixture.First];
        Assert.Equal(new[] { LoadFixture.Second, LoadFixture.First, third, LoadFixture.Second, LoadFixture.First, third }, imports);
        Assert.Equal(new LinkedValue(11, child, null), first.World.GetComponent<LinkedValue>(root));
        Assert.Equal(new LinkedValue(23, child, root), first.World.GetComponent<LinkedValue>(child));
        Assert.Equal(new LinkedValue(17, child, null), first.World.GetComponent<LinkedValue>(first.Entities[third]));
        Assert.Equal((0, 1, 2), (root.Index, child.Index, first.Entities[third].Index));
        Assert.Equal((19L, 11L, 12L), (first.Scene.Revision, first.Scene.DefinitionRevisions[LoadFixture.Definition], first.Scene.DefinitionRevisions[variant]));
        Assert.Equal("Child", first.Scene.Entities[LoadFixture.First].Name);
        Assert.Equal(LoadFixture.First, first.Scene.Entities[LoadFixture.First].Components[LoadFixture.Component].Properties["value"].Source.EntityId);
        Assert.NotEqual(first.World.Id, second.World.Id);
        Assert.False(second.World.IsAlive(child));
        first.World.DestroyEntity(child);
        Assert.Equal(child, first.Entities[LoadFixture.First]);
        Assert.False(first.World.IsAlive(child));
        Assert.True(second.World.IsAlive(second.Entities[LoadFixture.First]));
        Assert.Equal(23L, first.Scene.Entities[LoadFixture.First].Components[LoadFixture.Component].Properties["value"].Value.GetInteger());
        Assert.Equal(3, document.Entities.Length);
        Assert.True(first.IsReady);
    }

    [Fact]
    public void EmptyAndHiddenComponents_DoNotRequireUnusedRuntimeBindings()
    {
        var registry = new ComponentRegistryBuilder().Build();
        using var empty = Loader(registry).Load(LoadFixture.Empty());
        Assert.Empty(empty.Entities); Assert.Equal((0, 0), (empty.World.EntityCount, empty.World.ComponentTypeCount));
        var schema = LoadFixture.IntegerSchema;
        var unused = new ComponentSchema(new(Guid.Parse("a1000000-0000-0000-0000-000000000099")), 1, [], allowMultiple: true);
        var document = new SceneDocument(LoadFixture.SceneId, 13,
            [new(LoadFixture.First, LoadFixture.Definition, new(removedComponents: [LoadFixture.Component]))],
            [new(LoadFixture.Definition, new([new(LoadFixture.Component, schema.TypeId, 1)]))], [schema, unused]);
        using var hidden = Loader(registry).Load(document);
        Assert.Equal((1, 0), (hidden.World.EntityCount, hidden.World.ComponentTypeCount));
        Assert.Empty(hidden.Scene.ComponentSchemas);
        Assert.Equal(2, document.Schemas.Length);
    }

    [Theory]
    [InlineData("unknown", SceneWorldLoadError.UnknownComponent)]
    [InlineData("version", SceneWorldLoadError.UnsupportedSchemaVersion)]
    [InlineData("default", SceneWorldLoadError.SchemaMismatch)]
    [InlineData("nullable", SceneWorldLoadError.SchemaMismatch)]
    [InlineData("kind", SceneWorldLoadError.SchemaMismatch)]
    [InlineData("multiple", SceneWorldLoadError.UnsupportedMultiplicity)]
    [InlineData("missing-dependency", SceneWorldLoadError.InvalidScene)]
    [InlineData("missing-parent", SceneWorldLoadError.InvalidScene)]
    public void Preflight_RejectsInvalidBindingsAndSceneBeforeWorldCreation(string kind, SceneWorldLoadError expected)
    {
        var original = LoadFixture.IntegerSchema;
        var dependency = new ComponentTypeId(Guid.Parse("a1000000-0000-0000-0000-000000000006"));
        var schema = kind switch
        {
            "version" => new ComponentSchema(LoadFixture.Type, 2, original.Properties.Values),
            "default" => LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(8L))),
            "nullable" => LoadFixture.Schema(new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(7L), true)),
            "kind" => LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(7d))),
            "multiple" => new ComponentSchema(LoadFixture.Type, 1, original.Properties.Values, true),
            "missing-dependency" => new ComponentSchema(LoadFixture.Type, 1, original.Properties.Values, requiredTypes: [dependency]),
            _ => original
        };
        var imports = 0; var worlds = 0;
        var builder = new ComponentRegistryBuilder();
        if (kind != "unknown") builder.Register(kind == "missing-dependency" ? schema : original,
            context => { imports++; return new LoadFixture.Value(context.GetInt32("value")); });
        var document = LoadFixture.Scene(schema);
        if (kind == "missing-parent") document = new(document.Id, document.Revision,
            [new(LoadFixture.First, LoadFixture.Definition, parentId: LoadFixture.Second)], document.Definitions, document.Schemas);
        var loader = Loader(builder.Build(), new Factory<World>(() => { worlds++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(document));
        Assert.Equal(expected, error.Error);
        Assert.Equal(LoadFixture.SceneId, error.SceneId);
        if (expected != SceneWorldLoadError.InvalidScene)
            Assert.Equal((LoadFixture.First, LoadFixture.Component), (error.EntityId, error.ComponentId));
        else Assert.IsType<SceneResolutionException>(error.InnerException);
        Assert.Equal((0, 0), (worlds, imports));
        Assert.Null(error.CleanupOwner);
    }

    [Theory]
    [InlineData("empty-guid")]
    [InlineData("n-format")]
    [InlineData("whitespace")]
    [InlineData("malformed")]
    [InlineData("dangling-optional")]
    [InlineData("asset-no-validator")]
    [InlineData("asset-provider-failure")]
    public void References_RejectMalformedDanglingAndUnvalidatedAssetsBeforeImport(string kind)
    {
        var text = kind switch
        {
            "empty-guid" => Guid.Empty.ToString("D"), "n-format" => LoadFixture.First.Value.ToString("N"),
            "whitespace" => " " + LoadFixture.First.Value.ToString("D") + " ", "malformed" => "not-an-id",
            "dangling-optional" => LoadFixture.Second.Value.ToString("D"), _ => LoadFixture.First.Value.ToString("D")
        };
        var schema = LoadFixture.Schema(LoadFixture.Property("target", PropertyValue.Null, true));
        var imports = 0; var worlds = 0; var expected = new IOException("asset validator fixture");
        var builder = new ComponentRegistryBuilder().Register(schema, context => { imports++; return new LoadFixture.Value(1); },
            [new("target", kind.StartsWith("asset", StringComparison.Ordinal) ? ComponentReferenceKind.Asset : ComponentReferenceKind.Entity)]);
        if (kind == "asset-provider-failure") builder.WithAssetValidator((_, _) => throw expected);
        var loader = Loader(builder.Build(), new Factory<World>(() => { worlds++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(schema, [new("target", PropertyValue.From(text))])));
        Assert.Equal(SceneWorldLoadError.InvalidReference, error.Error);
        Assert.Equal((LoadFixture.SceneId, LoadFixture.First, LoadFixture.Component, "target"),
            (error.SceneId, error.EntityId, error.ComponentId, error.PropertyId));
        Assert.Equal((0, 0), (worlds, imports));
        if (kind == "asset-provider-failure") Assert.Same(expected, error.InnerException);
    }

    [Fact]
    public void AssetValidation_IsUniqueAndCompletesBeforeWorldAndImporters()
    {
        var asset = Guid.Parse("a1000000-0000-0000-0000-000000000077");
        var schema = LoadFixture.Schema(LoadFixture.Property("asset", PropertyValue.From(asset.ToString("D"))));
        var trace = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var registry = new ComponentRegistryBuilder().WithAssetValidator((id, token) =>
        { trace.Add("asset"); Assert.Equal(asset, id); Assert.Equal(cancellation.Token, token); }).Register(schema, context =>
        { trace.Add("import"); return new AssetValue(context.GetAssetReference("asset")!.Value); },
            [new("asset", ComponentReferenceKind.Asset)], _ => trace.Add("validate")).Build();
        var loader = Loader(registry, new Factory<World>(() => { trace.Add("world"); return new(); }));
        using var loaded = loader.Load(LoadFixture.Scene(schema, count: 3), cancellation.Token);
        Assert.Equal(new[] { "asset", "validate", "validate", "validate", "world", "import", "import", "import" }, trace);
        foreach (var entity in loaded.Entities.Values) Assert.Equal(new AssetValue(asset), loaded.World.GetComponent<AssetValue>(entity));
        Assert.Equal(3, loaded.World.EntityCount);
    }

    [Theory]
    [InlineData("entities")]
    [InlineData("components")]
    [InlineData("properties")]
    public void CardinalityBudgets_CountResolvedDefaultsBeforeFactoriesAndCallbacks(string budget)
    {
        var schema = LoadFixture.Schema(LoadFixture.Property("value", PropertyValue.From(7L)), LoadFixture.Property("other", PropertyValue.From(11L)));
        var options = new SceneWorldLoadOptions { MaximumEntities = 2, MaximumComponentInstances = 2, MaximumPropertyValues = 4, MaximumPreparedResources = 0 };
        var imports = 0; var worlds = 0; var validations = 0;
        var registry = new ComponentRegistryBuilder().Register(schema, context => { imports++; return new LoadFixture.Value(context.GetInt32("value")); },
            validate: _ => validations++).Build();
        using (var exact = Loader(registry, options: options).Load(LoadFixture.Scene(schema, count: 2)))
            Assert.Equal((2, 1), (exact.World.EntityCount, exact.World.ComponentTypeCount));
        imports = validations = 0;
        if (budget == "entities") options.MaximumEntities = 1;
        else if (budget == "components") options.MaximumComponentInstances = 1;
        else options.MaximumPropertyValues = 3;
        var loader = Loader(registry, new Factory<World>(() => { worlds++; return new(); }), options);
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(schema, count: 2)));
        Assert.Equal(SceneWorldLoadError.LimitExceeded, error.Error);
        Assert.Equal((0, 0, 0), (worlds, imports, validations));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(4096)]
    public void Load_SupportsFullEntityBudgetBeyondPrimitiveProjection(int count)
    {
        var registry = IntegerRegistry();
        using var result = Loader(registry).Load(LoadFixture.Scene(LoadFixture.IntegerSchema, count: count));
        Assert.Equal(count, result.Entities.Count); Assert.Equal(count, result.World.EntityCount);
        var seen = 0;
        result.World.Query<LoadFixture.Value>().ForEach((EntityHandle entity, scoped in LoadFixture.Value value) =>
        { seen++; Assert.Equal(7, value.Number); });
        Assert.Equal(count, seen);
        Assert.Equal(count - 1, result.Entities[LoadFixture.Entity(count)].Index);
    }

    [Fact]
    public void VersionsOfSameRuntimeType_UseOneWorldPoolAndCopiedLoadOptions()
    {
        var first = LoadFixture.IntegerSchema; var second = new ComponentSchema(first.TypeId, 2,
            [LoadFixture.Property("value", PropertyValue.From(17L))]);
        var secondDefinition = new EntityDefinitionId(Guid.Parse("a1000000-0000-0000-0000-000000000008"));
        var document = new SceneDocument(LoadFixture.SceneId, 13,
            [new(LoadFixture.First, LoadFixture.Definition), new(LoadFixture.Second, secondDefinition)],
            [new(LoadFixture.Definition, new([new(LoadFixture.Component, first.TypeId, 1)])), new(secondDefinition, new([new(LoadFixture.Component, second.TypeId, 2)]))],
            [first, second]);
        var registry = new ComponentRegistryBuilder().Register(first, context => new LoadFixture.Value(context.GetInt32("value")))
            .Register(second, context => new LoadFixture.Value(context.GetInt32("value") + 100)).Build();
        var options = new SceneWorldLoadOptions { MaximumEntities = 2, MaximumComponentInstances = 2, MaximumPropertyValues = 2 };
        var loader = Loader(registry, new Factory<World>(() => new(new() { InitialEntityCapacity = 0, MaximumEntityCount = 2, MaximumComponentTypes = 1 })), options);
        options.MaximumEntities = 1; options.MaximumComponentInstances = 1; options.MaximumPropertyValues = 0;
        using var result = loader.Load(document);
        Assert.Equal(new LoadFixture.Value(7), result.World.GetComponent<LoadFixture.Value>(result.Entities[LoadFixture.First]));
        Assert.Equal(new LoadFixture.Value(117), result.World.GetComponent<LoadFixture.Value>(result.Entities[LoadFixture.Second]));
        Assert.Equal((2, 1), (result.World.EntityCount, result.World.ComponentTypeCount));
    }

    [Theory]
    [InlineData("nonempty")]
    [InlineData("used-empty")]
    [InlineData("retired-empty")]
    [InlineData("empty-pool")]
    [InlineData("faulted")]
    [InlineData("disposed")]
    [InlineData("scheduled")]
    [InlineData("scene-owned")]
    public void InvalidFactoryWorld_IsRejectedWithoutDisposingForeignOwnership(string kind)
    {
        var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = kind == "retired-empty" ? 1 : 3 }, kind == "retired-empty" ? 1u : uint.MaxValue);
        EcsScheduler? scheduler = null; SceneWorld? existing = null;
        try
        {
            if (kind is "nonempty" or "used-empty" or "retired-empty" or "empty-pool")
            {
                var entity = world.CreateEntity();
                if (kind == "empty-pool") world.AddComponent(entity, 23);
                if (kind != "nonempty") world.DestroyEntity(entity);
            }
            else if (kind == "faulted") world.MarkFaulted(new IOException("preexisting fault"));
            else if (kind == "disposed") world.Dispose();
            else if (kind == "scheduled") scheduler = EcsScheduler.Create(world, []);
            else if (kind == "scene-owned") existing = Loader(IntegerRegistry(), new Factory<World>(() => world)).Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
            var beforeDisposed = world.IsDisposed; var beforeCount = beforeDisposed ? -1 : world.EntityCount;
            var imports = 0;
            var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context => { imports++; return new LoadFixture.Value(1); }).Build();
            var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, new Factory<World>(() => world)).Load(LoadFixture.Scene(LoadFixture.IntegerSchema)));
            Assert.Equal(SceneWorldLoadError.InvalidWorldFactory, error.Error);
            Assert.Equal(beforeDisposed, world.IsDisposed);
            if (!beforeDisposed) Assert.Equal(beforeCount, world.EntityCount);
            Assert.Equal(0, imports);
            if (scheduler != null) Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
            if (existing != null)
            { Assert.True(existing.IsReady); Assert.Equal(new LoadFixture.Value(7), world.GetComponent<LoadFixture.Value>(existing.Entities[LoadFixture.First])); }
        }
        finally { scheduler?.Dispose(); existing?.Dispose(); world.Dispose(); }
    }

    [Fact]
    public void NullFactoryResultAndInsufficientCapacity_HaveExplicitOutcomes()
    {
        var registry = IntegerRegistry();
        var nullError = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, new Factory<World>(() => null!)).Load(LoadFixture.Scene(LoadFixture.IntegerSchema)));
        Assert.Equal(SceneWorldLoadError.InvalidWorldFactory, nullError.Error);
        var privateWorld = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 1 });
        var capacity = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, new Factory<World>(() => privateWorld)).Load(LoadFixture.Scene(LoadFixture.IntegerSchema, count: 2)));
        Assert.Equal(SceneWorldLoadError.LimitExceeded, capacity.Error);
        Assert.True(privateWorld.IsDisposed);
    }

    [Fact]
    public void CancellationBeforeLoadAndFromFactory_PreservesOriginalCancellation()
    {
        var creations = 0; var registry = IntegerRegistry();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var loader = Loader(registry, new Factory<World>(() => { creations++; return new(); }));
        Assert.Throws<OperationCanceledException>(() => loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema), canceled.Token));
        Assert.Equal(0, creations);
        using var during = new CancellationTokenSource();
        var expected = new OperationCanceledException(during.Token);
        var factoryLoader = Loader(registry, new Factory<World>(() => { during.Cancel(); throw expected; }));
        Assert.Same(expected, Assert.Throws<OperationCanceledException>(() => factoryLoader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema), during.Token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationAfterCallbackReturn_IsObservedBeforeWorldPublication(bool duringValidation)
    {
        using var cancellation = new CancellationTokenSource();
        var creations = 0; var imports = 0; var lease = new Lease(); World? world = null;
        ComponentValidationContext? saved = null;
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            imports++; saved = context;
            context.Acquire(_ => lease);
            cancellation.Cancel();
            return new LoadFixture.Value(17);
        }, validate: context =>
        {
            if (!duringValidation) return;
            saved = context; cancellation.Cancel();
        }).Build();
        var loader = Loader(registry, new Factory<World>(() => { creations++; return world = new(); }));
        var error = Assert.Throws<OperationCanceledException>(() => loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema), cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.NotNull(saved);
        Assert.Throws<InvalidOperationException>(() => saved.GetValue("value"));
        Assert.Equal(duringValidation ? (0, 0, 0) : (1, 1, 1), (creations, imports, lease.Disposals));
        if (world != null) Assert.True(world.IsDisposed);
    }

    [Fact]
    public void ImportFailure_CleansPrivateWorldAndLeasesInReverseOrder()
    {
        World? world = null; var trace = new List<string>(); var imports = 0; var expected = new IOException("import fixture");
        var leases = Enumerable.Range(1, 3).Select(index => new Lease(() => { Assert.True(world!.IsDisposed); trace.Add(index.ToString()); })).ToArray();
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            imports++;
            Assert.Equal(0, world!.ComponentTypeCount);
            context.Acquire(_ => leases[imports == 1 ? 0 : 2]);
            if (imports == 1) context.Acquire(_ => leases[1]); else throw expected;
            return new LoadFixture.Value(context.GetInt32("value"));
        }).Build();
        var loader = Loader(registry, new Factory<World>(() => world = new()));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema, count: 2)));
        Assert.Equal(SceneWorldLoadError.ImportFailed, error.Error);
        Assert.Same(expected, error.InnerException);
        Assert.Equal(LoadFixture.Second, error.EntityId);
        Assert.True(world!.IsDisposed);
        Assert.Equal(new[] { "3", "2", "1" }, trace);
        Assert.All(leases, lease => Assert.Equal(1, lease.Disposals));
        Assert.Null(error.CleanupOwner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Acquire_BudgetIsCheckedBeforeProviderAndEarlierLeasesAreCleaned(int budget)
    {
        var calls = 0; var lease = new Lease(); World? world = null;
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            for (var i = 0; i <= budget; i++) context.Acquire(_ => { calls++; return lease; });
            return new LoadFixture.Value(0);
        }).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, new Factory<World>(() => world = new()),
            new() { MaximumPreparedResources = budget }).Load(LoadFixture.Scene(LoadFixture.IntegerSchema)));
        Assert.Equal(SceneWorldLoadError.LimitExceeded, error.Error);
        Assert.Equal(budget, calls); Assert.Equal(budget, lease.Disposals);
        Assert.True(world!.IsDisposed);
    }

    [Fact]
    public void Acquire_RejectsDuplicateReferenceWithoutDoubleDisposal()
    {
        var lease = new Lease(); var calls = 0;
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            context.Acquire(_ => { calls++; return lease; });
            context.Acquire(_ => { calls++; return lease; });
            return new LoadFixture.Value(0);
        }).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, options: new() { MaximumPreparedResources = 2 }).Load(LoadFixture.Scene(LoadFixture.IntegerSchema)));
        Assert.Equal(SceneWorldLoadError.ImportFailed, error.Error);
        Assert.Equal((2, 1), (calls, lease.Disposals));
    }

    [Fact]
    public void Acquire_AccountsReturnedLeaseEvenWhenProviderCancelsToken()
    {
        using var cancellation = new CancellationTokenSource(); var lease = new Lease(); World? world = null;
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            context.Acquire(token => { Assert.Equal(cancellation.Token, token); cancellation.Cancel(); return lease; });
            return new LoadFixture.Value(0);
        }).Build();
        var error = Assert.Throws<OperationCanceledException>(() => Loader(registry, new Factory<World>(() => world = new()))
            .Load(LoadFixture.Scene(LoadFixture.IntegerSchema), cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, lease.Disposals); Assert.True(world!.IsDisposed);
    }

    [Fact]
    public void Acquire_NestedProviderCannotConsumeOuterReservationBudget()
    {
        var outer = new Lease(); var innerCalls = 0;
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            context.Acquire(_ =>
            {
                var rejected = Assert.Throws<SceneWorldLoadException>(() => context.Acquire(_ => { innerCalls++; return new Lease(); }));
                Assert.Equal(SceneWorldLoadError.LimitExceeded, rejected.Error);
                return outer;
            });
            return new LoadFixture.Value(7);
        }).Build();
        var result = Loader(registry, options: new() { MaximumPreparedResources = 1 }).Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
        try
        {
            Assert.Equal(0, innerCalls); Assert.Equal(1, result.PreparedResourceCount);
            Assert.Equal(new LoadFixture.Value(7), result.World.GetComponent<LoadFixture.Value>(result.Entities[LoadFixture.First]));
            Assert.Equal(0, outer.Disposals);
        }
        finally { result.Dispose(); }
        Assert.Equal(1, outer.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Acquire_ProviderFailureReleasesReservationForExplicitRetryInsideImporter(bool returnsNull)
    {
        var expected = new IOException("provider fixture"); var lease = new Lease();
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        {
            if (returnsNull)
                Assert.Equal(SceneWorldLoadError.ImportFailed, Assert.Throws<SceneWorldLoadException>(() => context.Acquire<Lease>(_ => null!)).Error);
            else Assert.Same(expected, Assert.Throws<IOException>(() => context.Acquire<Lease>(_ => throw expected)));
            context.Acquire(_ => lease);
            return new LoadFixture.Value(11);
        }).Build();
        var result = Loader(registry, options: new() { MaximumPreparedResources = 1 }).Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
        Assert.Equal(1, result.PreparedResourceCount);
        Assert.True(result.IsReady);
        result.Dispose(); result.Dispose();
        Assert.Equal(1, lease.Disposals);
    }

    [Fact]
    public void CleanupFailure_RetainsOwnerOriginalErrorAndOnlyRetriesFailedLease()
    {
        var original = new IOException("import fixture"); var cleanup = new IOException("cleanup fixture");
        var attempts = 0; var successful = new Lease(); var failing = new Lease(() => { if (++attempts == 1) throw cleanup; });
        World? world = null;
        var registry = new ComponentRegistryBuilder().Register<LoadFixture.Value>(LoadFixture.IntegerSchema, context =>
        { context.Acquire(_ => successful); context.Acquire(_ => failing); throw original; }).Build();
        var error = Assert.Throws<SceneWorldLoadException>(() => Loader(registry, new Factory<World>(() => world = new())).Load(LoadFixture.Scene(LoadFixture.IntegerSchema)));
        Assert.Equal(SceneWorldLoadError.CleanupFailed, error.Error);
        var combined = Assert.IsType<AggregateException>(error.InnerException);
        var import = Assert.IsType<SceneWorldLoadException>(combined.InnerExceptions[0]);
        Assert.Same(original, import.InnerException);
        Assert.Contains(cleanup, combined.Flatten().InnerExceptions);
        var owner = Assert.IsType<SceneWorld>(error.CleanupOwner);
        try
        {
            Assert.True(world!.IsDisposed); Assert.False(owner.IsReady); Assert.False(owner.IsDisposed);
            Assert.Equal((1, 1, 1), (owner.PreparedResourceCount, successful.Disposals, failing.Disposals));
            owner.Dispose(); owner.Dispose();
            Assert.True(owner.IsDisposed); Assert.Equal(0, owner.PreparedResourceCount);
            Assert.Equal((1, 2), (successful.Disposals, failing.Disposals));
        }
        finally { owner.Dispose(); }
    }

    [Fact]
    public void SuccessfulSceneDisposal_InvalidatesWorldThenReleasesLeasesInReverseOrder()
    {
        World? world = null; var trace = new List<int>();
        var first = new Lease(() => { Assert.True(world!.IsDisposed); trace.Add(1); });
        var second = new Lease(() => { Assert.True(world!.IsDisposed); trace.Add(2); });
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        { context.Acquire(_ => first); context.Acquire(_ => second); return new LoadFixture.Value(13); }).Build();
        var owner = Loader(registry, new Factory<World>(() => world = new())).Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
        Assert.True(owner.IsReady); Assert.Equal(2, owner.PreparedResourceCount);
        owner.Dispose(); owner.Dispose();
        Assert.Equal(new[] { 2, 1 }, trace);
        Assert.Equal((1, 1), (first.Disposals, second.Disposals));
        Assert.Equal((false, true, 0), (owner.IsReady, owner.IsDisposed, owner.PreparedResourceCount));
        Assert.Throws<ObjectDisposedException>(() => world!.CreateEntity());
    }

    [Fact]
    public void PublishedSceneCleanupFailure_InvalidatesWorldAndRetriesOnlyFailedLease()
    {
        var trace = new List<int>(); var attempts = 0; var expected = new IOException("published cleanup fixture");
        var first = new Lease(() => trace.Add(1));
        var second = new Lease(() => { trace.Add(2); if (++attempts == 1) throw expected; });
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        { context.Acquire(_ => first); context.Acquire(_ => second); return new LoadFixture.Value(19); }).Build();
        var result = Loader(registry).Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
        try
        {
            Assert.True(result.IsReady);
            var error = Assert.Throws<AggregateException>(() => result.Dispose());
            Assert.Contains(expected, error.InnerExceptions);
            Assert.Equal((false, false, 1), (result.IsReady, result.IsDisposed, result.PreparedResourceCount));
            Assert.True(result.World.IsDisposed);
            Assert.Equal(new[] { 2, 1 }, trace);
            result.Dispose(); result.Dispose();
            Assert.Equal((false, true, 0), (result.IsReady, result.IsDisposed, result.PreparedResourceCount));
            Assert.Equal((1, 2), (first.Disposals, second.Disposals));
            Assert.Equal(new[] { 2, 1, 2 }, trace);
        }
        finally { result.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUnicode_IsRejectedBeforeFactoryIncludingUnusedCatalog(bool unused)
    {
        var schema = LoadFixture.Schema(LoadFixture.Property("text", PropertyValue.From("invalid\uD800")));
        var creations = 0; var imports = 0;
        var registry = new ComponentRegistryBuilder().Register(schema, _ => { imports++; return new LoadFixture.Value(0); }).Build();
        var document = unused ? new SceneDocument(LoadFixture.SceneId, 13, [], [], [schema]) : LoadFixture.Scene(schema);
        var loader = Loader(registry, new Factory<World>(() => { creations++; return new(); }));
        var error = Assert.Throws<SceneWorldLoadException>(() => loader.Load(new SceneWorldData(document)));
        Assert.Equal(SceneWorldLoadError.InvalidValue, error.Error);
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal((0, 0), (creations, imports));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void EmptyLoad_AdmitsZeroPropertyAndResourceBudgetBoundaries(int resources)
    {
        var options = new SceneWorldLoadOptions
        {
            MaximumEntities = 1, MaximumComponentInstances = 1,
            MaximumPropertyValues = 0, MaximumPreparedResources = resources
        };
        using var result = Loader(new ComponentRegistryBuilder().Build(), options: options).Load(LoadFixture.Empty());
        Assert.True(result.IsReady);
        Assert.Empty(result.Entities);
        Assert.Equal((0, 0), (result.World.EntityCount, result.World.ComponentTypeCount));
        Assert.Equal(0, result.PreparedResourceCount);
    }

    [Theory]
    [InlineData("entities", 0)] [InlineData("entities", 4097)]
    [InlineData("components", 0)] [InlineData("components", 65537)]
    [InlineData("properties", -1)] [InlineData("properties", 262145)]
    [InlineData("resources", -1)] [InlineData("resources", 65537)]
    public void Constructor_RejectsInvalidLimitsBeforeCreatingWorld(string kind, int value)
    {
        var options = new SceneWorldLoadOptions(); var creations = 0;
        switch (kind)
        {
            case "entities": options.MaximumEntities = value; break;
            case "components": options.MaximumComponentInstances = value; break;
            case "properties": options.MaximumPropertyValues = value; break;
            case "resources": options.MaximumPreparedResources = value; break;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Loader(IntegerRegistry(), new Factory<World>(() => { creations++; return new(); }), options));
        Assert.Equal(0, creations);
    }

    [Fact]
    public void ConstructorAndLoad_RejectNullsAndUnsupportedFormatWithoutActivation()
    {
        var registry = IntegerRegistry(); var worlds = LoadFixture.Worlds(); var resolution = LoadFixture.Resolution();
        Assert.Throws<ArgumentNullException>(() => new SceneWorldLoader(null!, worlds, resolution));
        Assert.Throws<ArgumentNullException>(() => new SceneWorldLoader(registry, null!, resolution));
        Assert.Throws<ArgumentNullException>(() => new SceneWorldLoader(registry, worlds, null!));
        var creations = 0; var loader = Loader(registry, new Factory<World>(() => { creations++; return new(); }));
        Assert.Throws<ArgumentNullException>(() => loader.Load((SceneDocument)null!));
        Assert.Throws<ArgumentNullException>(() => loader.Load((SceneWorldData)null!));
        Assert.Throws<ArgumentNullException>(() => loader.Create(null!));
        Assert.Throws<ArgumentNullException>(() => loader.Create(new(null!)));
        var format = Assert.Throws<SceneWorldLoadException>(() => loader.Load(new SceneWorldData(LoadFixture.Scene(LoadFixture.IntegerSchema), 2)));
        Assert.Equal(SceneWorldLoadError.UnsupportedFormat, format.Error);
        Assert.Equal(0, creations);
    }

    private static ComponentRegistry IntegerRegistry() => new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema,
        context => new LoadFixture.Value(context.GetInt32("value"))).Build();
    private static SceneWorldLoader Loader(ComponentRegistry registry, IFactory<World>? worlds = null, SceneWorldLoadOptions? options = null)
        => new(registry, worlds ?? LoadFixture.Worlds(), LoadFixture.Resolution(), options);
    private readonly record struct LinkedValue(int Number, EntityHandle? Target, EntityHandle? Parent);
    private readonly record struct AssetValue(Guid Asset);
    private sealed class Lease(Action? dispose = null) : IDisposable
    {
        internal int Disposals;
        public void Dispose() { Disposals++; dispose?.Invoke(); }
    }
}
