using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

public sealed class AuthoringFactoryRegistrationTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Factory_ProductsHaveIndependentHistoryAndSnapshotsAndCloseWithoutDisposingBorrowedResolution()
    {
        using var resolvers = new AuthoringTestData.ResolverFactory();
        var factory = new SceneAuthoringSessionFactory(new(resolvers), new());
        var document = AuthoringTestData.Document();
        using var first = factory.Create(document);
        using var second = factory.Create(document);
        var initial = first.Read();
        Assert.NotSame(first, second);
        Assert.NotSame(initial.Resolved, second.Read().Resolved);
        Assert.Equal(7, initial.Document.Revision);
        Assert.Equal(7, initial.Resolved.Revision);
        Assert.Equal(7, initial.ContentRevision);
        Assert.Equal((0, 0), (initial.UndoCount, initial.RedoCount));

        Assert.Equal(SceneAuthoringError.None, first.Apply(AuthoringTestData.Batch(7, 30)).Error);
        var committed = first.Read();
        Assert.Equal((8L, 8L, 1, 0), (committed.Document.Revision, committed.ContentRevision, committed.UndoCount, committed.RedoCount));
        Assert.Equal(30, AuthoringTestData.Number(committed, AuthoringTestData.First, "x"));
        Assert.Equal(3, AuthoringTestData.Number(initial, AuthoringTestData.First, "x"));
        Assert.Equal(7, second.Read().Document.Revision);
        Assert.Equal(3, AuthoringTestData.Number(second.Read(), AuthoringTestData.First, "x"));
        Assert.Equal(SceneAuthoringError.NoUndo, second.Undo(AuthoringTestData.Operation(7)).Error);

        first.Dispose();
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.Read());
        Assert.Throws<ObjectDisposedException>(() => first.Apply(AuthoringTestData.Batch(8, 99)));
        Assert.Throws<ObjectDisposedException>(() => first.Undo(AuthoringTestData.Operation(8)));
        Assert.Throws<ObjectDisposedException>(() => first.Redo(AuthoringTestData.Operation(8)));
        Assert.Equal(8, committed.Document.Revision);
        Assert.Equal(30, AuthoringTestData.Number(committed, AuthoringTestData.First, "x"));
        Assert.Equal(7, initial.Document.Revision);
        Assert.Equal(3, AuthoringTestData.Number(initial, AuthoringTestData.First, "x"));
        Assert.Equal(0, resolvers.Disposals);
        Assert.Equal(SceneAuthoringError.None, second.Apply(AuthoringTestData.Batch(7, 40)).Error);
        using var third = factory.Create(document);
        Assert.Equal(7, third.Read().Document.Revision);
        Assert.Equal((0, 0), (third.Read().UndoCount, third.Read().RedoCount));
        Assert.Equal(0, resolvers.Disposals);
    }

    [Fact]
    public void Factory_InvalidInitialScenePublishesNoSessionAndAllowsLaterValidCreation()
    {
        using var resolvers = new AuthoringTestData.ResolverFactory();
        var factory = new SceneAuthoringSessionFactory(new(resolvers), new());
        var valid = AuthoringTestData.Document();
        var invalid = new SceneDocument(valid.Id, valid.Revision, valid.Entities, [], valid.Schemas);

        var error = Assert.Throws<SceneResolutionException>(() => factory.Create(invalid));
        Assert.Equal(SceneResolutionError.InvalidEntity, error.Code);
        Assert.Equal(AuthoringTestData.First, error.EntityId);
        Assert.Equal(1, resolvers.Calls);
        Assert.Equal(0, resolvers.Disposals);

        using var session = factory.Create(valid);
        Assert.Same(valid, session.Read().Document);
        Assert.Equal(2, session.Read().Resolved.Entities.Count);
        Assert.Equal(2, resolvers.Calls);
        session.Dispose();
        Assert.Equal(0, resolvers.Disposals);
    }

    [Fact]
    public void Factory_UnexpectedInitialResolutionFailureCanBeRetriedWithoutOwningResolverFactory()
    {
        var expected = new IOException("initial resolution failed");
        var fail = true;
        using var resolvers = new AuthoringTestData.ResolverFactory(_ => { if (fail) throw expected; });
        var factory = new SceneAuthoringSessionFactory(new(resolvers), new());
        var document = AuthoringTestData.Document();

        Assert.Same(expected, Assert.Throws<IOException>(() => factory.Create(document)));
        Assert.Equal(0, resolvers.Disposals);
        fail = false;
        using var session = factory.Create(document);
        Assert.Equal(7, session.Read().Document.Revision);
        Assert.Equal(3, AuthoringTestData.Number(session.Read(), AuthoringTestData.First, "x"));
        Assert.Equal(2, resolvers.Calls);
        session.Dispose();
        Assert.Equal(0, resolvers.Disposals);
    }

    [Fact]
    public void FactoryAndRegistration_RejectNullDependenciesAndInitialDocument()
    {
        var resolution = AuthoringTestData.Resolution();
        Assert.Equal("resolution", Assert.Throws<ArgumentNullException>(() => new SceneAuthoringSessionFactory(null!, new())).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SceneAuthoringSessionFactory(resolution, null!)).ParamName);
        var factory = new SceneAuthoringSessionFactory(resolution, new());
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => AuthoringConfigurationExtensions.UseAetherAuthoring(null!)).ParamName);
    }

    [Theory]
    [InlineData(nameof(SceneAuthoringOptions.MaxHistoryEntries), -1)]
    [InlineData(nameof(SceneAuthoringOptions.MaxHistoryEntries), 1025)]
    [InlineData(nameof(SceneAuthoringOptions.MaxBatchEdits), 0)]
    [InlineData(nameof(SceneAuthoringOptions.MaxBatchEdits), 4097)]
    [InlineData(nameof(SceneAuthoringOptions.MaxOperations), 0)]
    [InlineData(nameof(SceneAuthoringOptions.MaxOperations), 65537)]
    public void Factory_InvalidOptionBoundaryFailsBeforeResolverActivation(string property, int value)
    {
        using var resolvers = new AuthoringTestData.ResolverFactory();
        var options = new SceneAuthoringOptions(); SetOption(options, property, value);
        Assert.Equal(property, Assert.Throws<ArgumentOutOfRangeException>(() => new SceneAuthoringSessionFactory(new(resolvers), options)).ParamName);
        Assert.Equal(0, resolvers.Calls);
        Assert.Equal(0, resolvers.Disposals);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1024, 4096, 65536)]
    public void Factory_AcceptsExactOptionBoundaries(int history, int batch, int operations)
    {
        var factory = new SceneAuthoringSessionFactory(AuthoringTestData.Resolution(), new()
        { MaxHistoryEntries = history, MaxBatchEdits = batch, MaxOperations = operations });
        using var session = factory.Create(AuthoringTestData.Document());
        Assert.Equal(SceneAuthoringError.None, session.Apply(AuthoringTestData.Batch(7, 20)).Error);
        Assert.Equal(history == 0 ? 0 : 1, session.Read().UndoCount);
        Assert.Equal(8, session.Read().Document.Revision);
    }

    [Fact]
    public void Factory_CopiesEveryOptionBeforeCallerMutationAndProductsHaveIndependentOperationBudgets()
    {
        var options = SnapshotOptions();
        var factory = new SceneAuthoringSessionFactory(AuthoringTestData.Resolution(), options);
        ChangeOptions(options);
        using var first = factory.Create(AuthoringTestData.Document());
        using var second = factory.Create(AuthoringTestData.Document());
        AssertSnapshotLimits(first);
        AssertSnapshotLimits(second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task App_AuthoringAloneProvidesSingletonFactoryAndIndependentSessionsWithoutStart(bool di)
    {
        var resolverCreations = 0;
        var resolvers = new AuthoringTestData.ResolverFactory();
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<EntityResolver, SceneDocument>>(_ =>
            { resolverCreations++; return resolvers; }));
            Assert.Same(context, context.UseAetherAuthoring());
        }).Build();
        Assert.Equal(0, resolverCreations);
        var factory = app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>();
        Assert.IsType<SceneAuthoringSessionFactory>(factory);
        Assert.Same(factory, app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>());
        Assert.Equal(1, resolverCreations);
        Assert.Equal(0, resolvers.Calls);
        using var first = factory.Create(AuthoringTestData.Document());
        using var second = factory.Create(AuthoringTestData.Document());
        Assert.NotSame(first, second);
        Assert.Equal(SceneAuthoringError.None, first.Apply(AuthoringTestData.Batch(7, 12)).Error);
        Assert.Equal(7, second.Read().Document.Revision);
        Assert.Equal(3, AuthoringTestData.Number(second.Read(), AuthoringTestData.First, "x"));
        first.Dispose();
        Assert.Equal(0, resolvers.Disposals);
        Assert.Equal(SceneAuthoringError.None, second.Apply(AuthoringTestData.Batch(7, 24)).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task App_CopiesAllAuthoringOptionsDuringConfigurationBeforeFactoryResolution(bool di)
    {
        SceneAuthoringOptions configured = null!;
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherAuthoring(options =>
            {
                options.MaxHistoryEntries = 1; options.MaxBatchEdits = 1; options.MaxOperations = 3;
                configured = options;
            });
            ChangeOptions(configured);
        }).Build();
        using var session = Assert.IsType<SceneAuthoringSessionFactory>(app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()).Create(AuthoringTestData.Document());
        AssertSnapshotLimits(session);
    }

    [Theory]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxHistoryEntries), -1)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxHistoryEntries), -1)]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxHistoryEntries), 1025)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxHistoryEntries), 1025)]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxBatchEdits), 0)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxBatchEdits), 0)]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxBatchEdits), 4097)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxBatchEdits), 4097)]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxOperations), 0)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxOperations), 0)]
    [InlineData(false, nameof(SceneAuthoringOptions.MaxOperations), 65537)]
    [InlineData(true, nameof(SceneAuthoringOptions.MaxOperations), 65537)]
    public void App_InvalidOptionBoundaryFailsDuringBuildBeforeAnyResolverCreation(bool di, string property, int value)
    {
        var calls = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<EntityResolver, SceneDocument>>(_ =>
            { calls++; throw new InvalidOperationException("must remain lazy"); }));
            context.UseAetherAuthoring(options => SetOption(options, property, value));
        });
        Assert.Equal(property, Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build()).ParamName);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void App_DuplicateOrExistingSessionFactoryFailsBeforeActivatingServices(bool di, bool conflict)
    {
        var calls = 0;
        var builder = Builder(di).Setup(context =>
        {
            if (conflict) context.Services(services => services.Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ =>
            { calls++; throw new InvalidOperationException("must remain lazy"); }));
            context.UseAetherAuthoring();
            if (!conflict) context.UseAetherAuthoring();
        });
        Assert.Equal("Aether authoring is already registered.", Assert.Throws<InvalidOperationException>(() => builder.Build()).Message);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task App_AetherCompositionInEitherOrderSharesInjectedResolutionWithoutCreatingRuntime(bool di, bool authoringFirst)
    {
        var runtimeCreations = 0;
        var received = new List<SceneDocument>();
        var resolvers = new AuthoringTestData.ResolverFactory(received.Add);
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<EntityResolver, SceneDocument>>(_ => resolvers));
            if (authoringFirst) context.UseAetherAuthoring();
            context.UseAether(options =>
            {
                options.HostContractId = Hash; options.GameBuildId = Hash;
                options.ReportDirectory = "authoring-composition-tests";
                options.RuntimeFactory = _ => { runtimeCreations++; throw new InvalidOperationException("authoring must not create a runtime"); };
            });
            if (!authoringFirst) context.UseAetherAuthoring();
        }).Build();
        try
        {
            var resolution = Assert.IsType<SceneResolutionService>(app.Services.Resolve<SceneResolutionService>());
            Assert.Same(resolution, app.Services.Resolve<SceneResolutionService>());
            Assert.Same(resolvers, app.Services.Resolve<IFactory<EntityResolver, SceneDocument>>());
            Assert.Empty(received);
            Assert.Equal(0, runtimeCreations);
            var document = AuthoringTestData.Document();
            using (var session = Assert.IsType<SceneAuthoringSessionFactory>(app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()).Create(document))
            {
                Assert.Equal(3, AuthoringTestData.Number(session.Read(), AuthoringTestData.First, "x"));
                var separatelyResolved = resolution.Resolve(document);
                Assert.Equal(document.Revision, separatelyResolved.Revision);
                Assert.Equal(2, separatelyResolved.Entities.Count);
                Assert.Collection(received, item => Assert.Same(document, item), item => Assert.Same(document, item));
            }
            Assert.Equal(0, resolvers.Disposals);
            Assert.Equal(0, runtimeCreations);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, resolvers.Disposals);
        Assert.Equal(0, runtimeCreations);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
    private static SceneAuthoringOptions SnapshotOptions() => new() { MaxHistoryEntries = 1, MaxBatchEdits = 1, MaxOperations = 3 };
    private static void ChangeOptions(SceneAuthoringOptions options)
    { options.MaxHistoryEntries = 0; options.MaxBatchEdits = 2; options.MaxOperations = 1; }

    private static void AssertSnapshotLimits(ISceneAuthoringSession session)
    {
        var rejected = session.Apply(new(AuthoringTestData.Operation(7),
            [AuthoringTestData.Edit(AuthoringTestData.First, 10), AuthoringTestData.Edit(AuthoringTestData.Second, 20)]));
        Assert.Equal(SceneAuthoringError.BatchLimitExceeded, rejected.Error);
        Assert.Equal(7, session.Read().Document.Revision);
        Assert.Equal(SceneAuthoringError.None, session.Apply(AuthoringTestData.Batch(7, 30)).Error);
        Assert.Equal(SceneAuthoringError.None, session.Apply(AuthoringTestData.Batch(8, 40)).Error);
        var before = session.Read();
        Assert.Equal((9L, 9L, 1, 0), (before.Document.Revision, before.ContentRevision, before.UndoCount, before.RedoCount));
        Assert.Equal(40, AuthoringTestData.Number(before, AuthoringTestData.First, "x"));
        Assert.Equal(SceneAuthoringError.OperationLimitReached, session.Undo(AuthoringTestData.Operation(9)).Error);
        Assert.Same(before, session.Read());
    }

    private static void SetOption(SceneAuthoringOptions options, string property, int value)
    {
        switch (property)
        {
            case nameof(SceneAuthoringOptions.MaxHistoryEntries): options.MaxHistoryEntries = value; break;
            case nameof(SceneAuthoringOptions.MaxBatchEdits): options.MaxBatchEdits = value; break;
            case nameof(SceneAuthoringOptions.MaxOperations): options.MaxOperations = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(property));
        }
    }
}
