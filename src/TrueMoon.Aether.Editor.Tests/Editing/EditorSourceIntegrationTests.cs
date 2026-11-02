using System.Reflection;
using System.Reflection.Emit;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed class EditorSourceIntegrationTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task App_PreservesSourceFactoryAndSuccessfulProductsHaveOnlyProjectOwnership(bool microsoft)
    {
        using var files = new SourceWatchFiles(); var sources = new SourceFactory(); var runtime = new NeverRuntime();
        var app = Builder(microsoft).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<EditorSourceWatch, ResolvedProject>>(_ => sources));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }).Build();
        ProjectSession? product = null;
        try
        {
            Assert.Same(sources, app.Services.Resolve<IFactory<EditorSourceWatch, ResolvedProject>>()); Assert.Empty(sources.Products);
            var factory = app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>()!;
            product = factory.Create(new(files.Project, new("10.0.403")));
            Assert.Same(Assert.Single(sources.Products), product.SourceWatch); Assert.Equal(product.OwnerKey, product.SourceWatch!.OwnerKey);
            Assert.Equal(EditorSourceState.NoBuild, product.SourceWatch.Read().State); Assert.Equal(0, runtime.Launches);
            await app.DisposeAsync();
            Assert.Equal(1, sources.Disposals); Assert.False(product.IsClosed); Assert.False(product.Documents!.Read().IsClosed);
            Assert.Equal(EditorSourceState.NoBuild, product.SourceWatch.Read().State);
            await product.DisposeAsync(); Assert.Equal(EditorSourceState.Stopped, product.SourceWatch.Read().State);
            await product.DisposeAsync(); await app.DisposeAsync(); Assert.Equal(1, sources.Disposals);
        }
        finally { if (product != null) await product.DisposeAsync(); await app.DisposeAsync(); }
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task App_CopiesDefaultSourceOptionsBeforeProjectFactoryCapturesDependencies(bool microsoft, bool enabled)
    {
        using var files = new SourceWatchFiles(); var runtime = new NeverRuntime(); var clock = new SourceWatchClock();
        AetherEditorOptions configured = null!;
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(services => services.Singleton<TimeProvider>(_ => clock));
            context.UseAetherEditor(engine => Configure(engine, files, runtime), options =>
            { configured = options; options.SourceTracking.Enabled = enabled; options.SourceTracking.UseFileSystemNotifications = false; });
        }).Build();
        configured.SourceTracking.Enabled = !enabled; configured.SourceTracking.PollInterval = TimeSpan.FromMinutes(1);
        using var product = app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>()!.Create(new(files.Project, new("10.0.403")));
        Assert.NotNull(product.SourceWatch); Assert.Equal(EditorSourceState.NoBuild, product.SourceWatch.Read().State);
        product.SourceWatch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, files.Descriptor());
        await SourceWatchFixture.Eventually(() => product.SourceWatch.Read().State == (enabled ? EditorSourceState.ObservedUnchanged : EditorSourceState.Unavailable));
        Assert.Equal(0, product.SourceWatch.Read().WatcherCount); Assert.Equal(0, runtime.Launches);
        if (enabled) await clock.WaitForDue(TimeSpan.FromSeconds(10)); else Assert.Equal(0, clock.TimerCount);
    }

    [Fact]
    public void Factory_PartialSourceCreationFailureRetainsFailedDocumentCleanupForRetry()
    {
        using var files = new SourceWatchFiles(); using var authoring = new DocumentData.Sessions(); var store = new DocumentStore();
        var creation = new IOException("source factory failed"); var cleanup = new IOException("document cleanup failed"); var fail = true;
        SceneDocumentSession document = null!;
        using var factory = new ProjectSessionFactory(new Factory<SceneDocumentSession, ResolvedProject>(project =>
        {
            document = new(project, store, authoring); document.NewAsync(DocumentData.Scene()).GetAwaiter().GetResult();
            authoring.Products.Single().BeforeDispose = () => { if (fail) { fail = false; throw cleanup; } };
            return document;
        }), new Factory<EditorSourceWatch, ResolvedProject>(_ => throw creation));
        var error = Assert.Throws<AggregateException>(() => factory.Create(new(files.Project, new("10.0.403"))));
        Assert.Collection(error.Flatten().InnerExceptions, item => Assert.Same(creation, item), item => Assert.Same(cleanup, item));
        Assert.True(document.Read().IsClosed); Assert.Equal(1, authoring.Products.Single().Disposals); Assert.Equal(0, store.Disposals);
        factory.Dispose(); Assert.Equal(2, authoring.Products.Single().Disposals);
        factory.Dispose(); Assert.Equal(2, authoring.Products.Single().Disposals); Assert.Equal(0, authoring.Disposals);
    }

    [Fact]
    public async Task ProjectDisposeDrainsSourceAndDocumentAggregatesFailuresAndRetriesBothOwnedProducts()
    {
        await using var h = new SourceWatchFixture(); using var authoring = new DocumentData.Sessions(); var store = new DocumentStore();
        var document = new SceneDocumentSession(h.Files.Project, store, authoring); await document.NewAsync(DocumentData.Scene());
        var documentFailure = new IOException("document release failed"); var fail = true;
        authoring.Products.Single().BeforeDispose = () => { if (fail) { fail = false; throw documentFailure; } };
        var session = new ProjectSession(new(h.Files.Project, new("10.0.403")), document, h.Watch);
        h.Bind(); var scan = await h.Scanner.Next(); h.Hints.Latest.FailNextDispose = true;
        var closing = session.DisposeAsync().AsTask();
        await SourceWatchFixture.Eventually(() => scan.Token.IsCancellationRequested && authoring.Products.Single().Disposals == 1);
        Assert.True(session.IsClosed); Assert.False(closing.IsCompleted); Assert.True(document.Read().IsClosed);
        Assert.Throws<ObjectDisposedException>(() => session.CreateBuildRequest(2)); Assert.Equal(EditorSourceState.Stopped, h.Watch.Read().State);
        scan.Complete(SourceWatchFixture.Same);
        var error = Assert.IsType<AggregateException>(await Record.ExceptionAsync(() => closing));
        Assert.Contains(error.Flatten().InnerExceptions, item => ReferenceEquals(item, documentFailure));
        Assert.Contains("hint cleanup failure", error.ToString());
        await session.DisposeAsync(); Assert.Equal(2, authoring.Products.Single().Disposals); Assert.Equal(2, h.Hints.Latest.Disposals);
        Assert.Equal(0, h.Watch.Read().WatcherCount); Assert.Equal(0, store.Disposals); Assert.Equal(0, authoring.Disposals);
        await session.DisposeAsync(); Assert.Equal(2, authoring.Products.Single().Disposals); Assert.Equal(2, h.Hints.Latest.Disposals);
    }

    [Fact]
    public async Task CancelledSwitchPreservesBaselineWhileCloseAndReopenCreateNewOwnerForSameManifest()
    {
        using var files = new SourceWatchFiles(); var documents = new DocumentData.Sessions(); var store = new DocumentStore();
        using var sourceFactory = new SourceFactory();
        using var sessions = new ProjectSessionFactory(new Factory<SceneDocumentSession, ResolvedProject>(project => new(project, store, documents)), sourceFactory);
        await using var projects = new EditorProjects(new EditorProjectsTests.Store(files.Project), new EditorProjectsTests.Toolchain(), sessions);
        var first = await projects.OpenAsync(files.Project.ManifestPath); await first.Documents!.NewAsync(DocumentData.Scene());
        first.SourceWatch!.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, files.Descriptor());
        await SourceWatchFixture.Eventually(() => first.SourceWatch.Read().State == EditorSourceState.ObservedUnchanged);
        var baseline = first.SourceWatch!.Read();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => projects.SwitchAsync(files.Project.ManifestPath, DocumentCloseChoice.Cancel));
        Assert.Same(first, projects.Active); Assert.False(first.IsClosed); Assert.Same(baseline, first.SourceWatch.Read());
        await projects.CloseAsync(DocumentCloseChoice.Discard); Assert.Null(projects.Active); Assert.True(first.IsClosed);
        Assert.Equal(EditorSourceState.Stopped, first.SourceWatch.Read().State);
        var reopened = await projects.OpenAsync(files.Project.ManifestPath);
        Assert.Equal(first.Project.Description.Id, reopened.Project.Description.Id); Assert.NotEqual(first.OwnerKey, reopened.OwnerKey);
        Assert.NotSame(first.SourceWatch, reopened.SourceWatch); Assert.Equal(EditorSourceState.NoBuild, reopened.SourceWatch!.Read().State);
        Assert.Equal(0, store.Disposals); Assert.Equal(0, documents.Disposals);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AppStopClosesActiveProjectSourceWhileMetadataOnlyCompositionRemainsSupported(bool microsoft)
    {
        using var files = new SourceWatchFiles(); var runtime = new NeverRuntime(); var sources = new SourceFactory();
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => new EditorProjectsTests.Store(files.Project))
                .Singleton<IProjectToolchain>(_ => new EditorProjectsTests.Toolchain())
                .Singleton<IFactory<EditorSourceWatch, ResolvedProject>>(_ => sources));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }).Build();
        await app.StartAsync(); var active = await app.Services.Resolve<EditorProjects>()!.OpenAsync(files.Project.ManifestPath);
        Assert.Equal(EditorSourceState.NoBuild, active.SourceWatch!.Read().State); await app.StopAsync();
        Assert.True(active.IsClosed); Assert.Equal(EditorSourceState.Stopped, active.SourceWatch.Read().State); Assert.Equal(0, runtime.Launches);
        await using var metadata = Builder(microsoft).Setup(context => context.UseAetherEditorProjects()).Build();
        using var product = metadata.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>()!.Create(new(files.Project, new("10.0.403")));
        Assert.Null(product.SourceWatch); Assert.Null(product.Documents); Assert.False(product.IsClosed);
    }

    [Fact]
    public void SuccessfulSceneBuildBindsActualCandidateAndLegacySourceUnavailableDoesNotFailCommand()
    {
        using var h = new SourcePresentationFixture(); var artifact = LegacyArtifact(h.Control.Files);
        GameEditorContext? context = null; GameEditorCandidate? candidate = null; long version = 0;
        var playback = new EditorScenePlayback(h.DocumentCommands, h.Preview, () => h.Runtime,
            () => new(version, context, GameEditorOperation.Idle, candidate, null, false),
            selected => { context = selected; version++; candidate = null; },
            (command, _) => { if (command == EditorCommand.Build) candidate = new(artifact, version, null); return Task.CompletedTask; },
            (_, _) => throw new InvalidOperationException("Explicit Build must not start a runtime."));
        var document = h.Active.Documents!.Read().Content;
        Assert.True(playback.Execute(EditorCommand.Build)); SourcePresentationFixture.Wait(playback.Pending);
        Assert.Null(playback.Error); Assert.True(playback.CanExecute(EditorCommand.Build));
        var source = h.Watch.Read(); Assert.Equal(artifact.BuildId, source.BuildId); Assert.Equal(1, source.BaselineVersion);
        Assert.Equal(EditorSourceState.Unavailable, source.State); Assert.Contains("process-local", source.Reason);
        Assert.Same(document, h.Active.Documents.Read().Content); Assert.True(h.Active.Documents.Read().IsDirty);
        Assert.Equal(0, h.Control.Scanner.Calls); Assert.Empty(h.RuntimeCalls);
    }

    [Theory] [InlineData("failure")] [InlineData("cancel")] [InlineData("context")] [InlineData("document")]
    public void FailedCancelledAndStaleSceneBuildCompletionPreservesSuccessfulSourceBaseline(string problem)
    {
        using var h = new SourcePresentationFixture(); h.Bind();
        SourcePresentationFixture.Wait(h.Control.Scanner.Next()).Complete(SourceWatchFixture.Same);
        h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); var previous = h.Watch.Read();
        var artifact = LegacyArtifact(h.Control.Files); var failure = new IOException("compiler failed");
        var entered = DocumentData.Signal(); var release = DocumentData.Signal();
        GameEditorContext? context = null; GameEditorCandidate? candidate = null; long version = 0;
        var playback = new EditorScenePlayback(h.DocumentCommands, h.Preview, () => h.Runtime,
            () => new(version, context, GameEditorOperation.Idle, candidate, null, false),
            selected => { context = selected; version++; candidate = null; }, async (command, token) =>
            {
                if (command != EditorCommand.Build) return;
                entered.TrySetResult(); await release.Task; token.ThrowIfCancellationRequested();
                if (problem == "failure") throw failure;
                candidate = new(artifact, version, null);
                if (problem == "context") version++;
                if (problem == "document")
                { var state = h.Active.Documents!.Read().Content!; h.Active.Documents.Apply(DocumentData.Edit(state.Document.Revision, 27)); }
            }, (_, _) => throw new InvalidOperationException("Explicit Build must not start a runtime."));
        Assert.True(playback.Execute(EditorCommand.Build)); SourcePresentationFixture.Wait(entered.Task);
        try { if (problem == "cancel") Assert.True(playback.Execute(EditorCommand.Cancel)); }
        finally { release.TrySetResult(); SourcePresentationFixture.Wait(playback.Pending); }
        Assert.Same(previous, h.Watch.Read()); Assert.Same(problem == "failure" ? failure : null, playback.Error);
        Assert.Equal(1, h.Control.Scanner.Calls); Assert.Empty(h.RuntimeCalls); Assert.Equal(SourceWatchFixture.BuildA, h.Watch.Read().BuildId);
    }

    private static GameBuildArtifact LegacyArtifact(SourceWatchFiles files)
    {
        var directory = Path.Combine(files.Root, "legacy-output"); Directory.CreateDirectory(directory);
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("Room.Logic"), typeof(object).Assembly);
        var parent = assembly.DefineDynamicModule("Room.Logic").DefineType("Room.Logic.Game", TypeAttributes.Public);
        parent.DefineNestedType("Nested", TypeAttributes.NestedPublic).CreateType(); parent.CreateType();
        assembly.Save(Path.Combine(directory, "Room.Logic.dll"));
        return GameBuildArtifact.Publish(directory, files.Artifacts, new('A', 64), "Room.Logic.dll", files.Project.Description.EntryType, 1);
    }

    private static IAppBuilder Builder(bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); });
    private static void Configure(TrueMoon.Aether.Hosting.AetherOptions options, SourceWatchFiles files, NeverRuntime runtime)
    { options.HostContractId = new('A', 64); options.GameBuildId = new('0', 64); options.ReportDirectory = Path.Combine(files.Root, "reports"); options.RuntimeFactory = _ => runtime; }
    private sealed class NeverRuntime : IRuntimeFactory, IDisposable
    { internal int Launches; public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("Tracking must not start a runtime."); } public void Dispose() { } }
    private sealed class SourceFactory : IFactory<EditorSourceWatch, ResolvedProject>, IDisposable
    {
        internal List<EditorSourceWatch> Products { get; } = [];
        internal int Disposals;
        public EditorSourceWatch Create(ResolvedProject project) { var watch = new EditorSourceWatch(project, new(), TimeProvider.System); Products.Add(watch); return watch; }
        public void Dispose() => Disposals++;
    }
}
