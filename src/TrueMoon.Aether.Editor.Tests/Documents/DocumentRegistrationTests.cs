using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Documents;

public sealed class DocumentRegistrationTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task App_DefaultCodecAndStoreAreSharedWhileDocumentFactoryProductsRemainCallerOwned(bool di, bool persistenceFirst)
    {
        using var files = new DocumentFiles();
        var app = Builder(di).Setup(context =>
        {
            context.UseAetherAuthoring(); if (persistenceFirst) context.UseAetherScenePersistence();
            Assert.Same(context, context.UseAetherEditorDocuments());
        }).Build();
        SceneDocumentSession? first = null, second = null;
        try
        {
            var codec = Assert.IsType<SceneFileCodec>(app.Services.Resolve<SceneFileCodec>());
            Assert.Same(codec, app.Services.Resolve<SceneFileCodec>());
            var store = Assert.IsType<JsonSceneDocumentStore>(app.Services.Resolve<ISceneDocumentStore>());
            Assert.Same(store, app.Services.Resolve<ISceneDocumentStore>());
            var factory = Assert.IsAssignableFrom<IFactory<SceneDocumentSession, ResolvedProject>>(app.Services.Resolve<IFactory<SceneDocumentSession, ResolvedProject>>());
            Assert.Same(factory, app.Services.Resolve<IFactory<SceneDocumentSession, ResolvedProject>>());
            first = factory.Create(files.Project); second = factory.Create(files.Project); Assert.NotSame(first, second);
            Assert.True(await first.NewAsync(DocumentData.Scene())); Assert.Null(second.Read().Content);
            var saved = await first.SaveAsAsync("room.aetherscene"); Assert.False(first.Read().IsDirty);
            Assert.True(await second.LoadAsync("room.aetherscene")); DocumentData.AssertScene(second.Read().Content!.Document);
            Assert.Equal(saved.Stamp, second.Read().Stamp); Assert.True(first.Apply(DocumentData.Edit(7, 10)).Succeeded);
            Assert.Equal(4d, DocumentData.X(second.Read().Content!));
            await app.DisposeAsync();
            Assert.False(first.Read().IsClosed); Assert.False(second.Read().IsClosed);
            Assert.Equal(10d, DocumentData.X(first.Read().Content!)); Assert.Equal(4d, DocumentData.X(second.Read().Content!));
        }
        finally { if (first != null) await first.DisposeAsync(); if (second != null) await second.DisposeAsync(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task App_InjectedStoreAndAuthoringFactoryAreBorrowedByProductsAndDisposedOnlyByApp(bool di)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var sessions = new DocumentData.Sessions();
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<ISceneDocumentStore>(_ => store)
                .Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => sessions));
            context.UseAetherEditorDocuments();
        }).Build();
        try
        {
            Assert.Same(store, app.Services.Resolve<ISceneDocumentStore>());
            Assert.Same(sessions, app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>());
            var factory = Assert.IsAssignableFrom<IFactory<SceneDocumentSession, ResolvedProject>>(app.Services.Resolve<IFactory<SceneDocumentSession, ResolvedProject>>());
            var owner = factory.Create(files.Project); Assert.True(await owner.LoadAsync(files.ScenePath));
            await owner.DisposeAsync(); await owner.DisposeAsync();
            Assert.Equal(1, Assert.Single(sessions.Products).Disposals); Assert.Equal((0, 0), (store.Disposals, sessions.Disposals));
            await app.DisposeAsync(); Assert.Equal((1, 1), (store.Disposals, sessions.Disposals));
            await app.DisposeAsync(); Assert.Equal((1, 1), (store.Disposals, sessions.Disposals));
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false, "missing")] [InlineData(true, "missing")]
    [InlineData(false, "duplicate")] [InlineData(true, "duplicate")]
    public void App_RejectsMissingAuthoringAndDuplicateAttachmentBeforeAnyServiceActivation(bool di, string kind)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<ISceneDocumentStore>(_ => { activations++; throw new IOException("stay lazy"); }));
            if (kind != "missing") context.UseAetherAuthoring();
            context.UseAetherEditorDocuments(); if (kind == "duplicate") context.UseAetherEditorDocuments();
        });
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(kind == "missing" ? "Call UseAetherAuthoring before UseAetherEditorDocuments." : "Editor documents are already registered.", error.Message);
        Assert.Equal(0, activations);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task App_ExistingCodecIsReusedAndDefaultStoreUsesItsBorrowedValidationFactory(bool di)
    {
        using var files = new DocumentFiles(); var sessions = new DocumentData.Sessions(); var codecActivations = 0;
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => sessions)
                .Singleton<SceneFileCodec>(resolver => { codecActivations++; return new(resolver.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>()!); }));
            context.UseAetherEditorDocuments();
        }).Build();
        await app.StartAsync(); Assert.Equal(0, codecActivations); Assert.Empty(sessions.Products);
        var codec = Assert.Single(app.Services.ResolveAll<SceneFileCodec>());
        var store = Assert.IsType<JsonSceneDocumentStore>(app.Services.Resolve<ISceneDocumentStore>());
        Assert.Equal(1, codecActivations); Assert.Same(codec, app.Services.Resolve<SceneFileCodec>());
        var path = Path.Combine(files.Project.ScenesDirectory, "engine-override.aetherscene");
        var stamp = await store.WriteAsync(path, DocumentData.Scene(), null); var loaded = await store.ReadAsync(path);
        Assert.Equal(stamp, loaded.Stamp); DocumentData.AssertScene(loaded.Document);
        Assert.Equal(2, sessions.Products.Count); Assert.All(sessions.Products, product => Assert.Equal(1, product.Disposals));
        Assert.Equal(0, sessions.Disposals); await app.DisposeAsync(); Assert.Equal(1, sessions.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProjectOwnedDocuments_BlockDirtySwitchAndNormalCloseUntilExplicitSaveOrDiscard(bool di)
    {
        using var files = new DocumentFiles(); var projectStore = new Projects.EditorProjectsTests.Store(files.Project);
        var toolchain = new Projects.EditorProjectsTests.Toolchain(); var sceneStore = new DocumentStore();
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => projectStore).Singleton<IProjectToolchain>(_ => toolchain)
                .Singleton<ISceneDocumentStore>(_ => sceneStore));
            context.UseAetherAuthoring().UseAetherEditorDocuments().UseAetherEditorProjects();
        }).Build();
        await app.StartAsync(); var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>()); var first = await projects.OpenAsync(files.Project.ManifestPath);
        var document = Assert.IsType<SceneDocumentSession>(first.Documents); Assert.Null(document.Read().Content);
        Assert.True(await document.NewAsync(DocumentData.Scene())); Assert.True(document.Apply(DocumentData.Edit(7, 15)).Succeeded);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => projects.OpenAsync(files.Project.ManifestPath));
        Assert.Equal("Save/discard the current scene and finish document IO before switching projects.", error.Message); Assert.Same(first, projects.Active); Assert.False(first.IsClosed);
        Assert.True(document.Read().IsDirty); Assert.Equal(15d, DocumentData.X(document.Read().Content!)); Assert.Empty(sceneStore.Writes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.CloseAsync()); Assert.Same(first, projects.Active);
        await document.SaveAsAsync("room.aetherscene"); Assert.False(document.Read().IsDirty);
        var second = await projects.OpenAsync(files.Project.ManifestPath); Assert.NotSame(first, second);
        Assert.True(first.IsClosed); Assert.True(document.Read().IsClosed); Assert.Null(document.Read().Content);
        Assert.False(second.Documents!.Read().IsClosed); Assert.Null(second.Documents.Read().Content);
        Assert.True(await second.Documents.NewAsync(DocumentData.Scene()));
        Assert.True(await second.Documents.CloseAsync(DocumentCloseChoice.Discard)); await projects.CloseAsync();
        Assert.Null(projects.Active); Assert.True(second.IsClosed); Assert.False(projects.IsClosed);
        Assert.Equal((0, 0, 0), (projectStore.Disposals, toolchain.Disposals, sceneStore.Disposals));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppStop_ForceDiscardsDirtyDocumentsButDrainsAcceptedSaveBeforeBorrowedStoreDisposal(bool di)
    {
        using var files = new DocumentFiles(); var projectStore = new Projects.EditorProjectsTests.Store(files.Project);
        var toolchain = new Projects.EditorProjectsTests.Toolchain(); var sceneStore = new DocumentStore(); var authoring = new DocumentData.Sessions();
        var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => projectStore).Singleton<IProjectToolchain>(_ => toolchain)
                .Singleton<ISceneDocumentStore>(_ => sceneStore).Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => authoring));
            context.UseAetherEditorDocuments().UseAetherEditorProjects();
        }).Build();
        try
        {
            await app.StartAsync(); var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>()); var project = await projects.OpenAsync(files.Project.ManifestPath);
            var document = project.Documents!; Assert.True(await document.NewAsync(DocumentData.Scene()));
            var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancelled = DocumentData.Signal();
            sceneStore.Write = async (_, _, _, token) =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult()); entered.SetResult();
                await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("late-committed");
            };
            var saving = document.SaveAsAsync("room.aetherscene"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = app.StopAsync(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted); Assert.True(projects.IsClosed); Assert.True(project.IsClosed); Assert.True(document.Read().IsClosed);
            Assert.Equal(0, Assert.Single(authoring.Products).Disposals); Assert.Equal((0, 0, 0, 0), (projectStore.Disposals, toolchain.Disposals, sceneStore.Disposals, authoring.Disposals));
            finish.SetResult(); await saving; await stopping;
            Assert.Null(projects.Active); Assert.Null(document.Read().Content); Assert.Null(document.Read().Path); Assert.Equal(1, authoring.Products[0].Disposals);
            await app.DisposeAsync(); Assert.Equal((1, 1, 1, 1), (projectStore.Disposals, toolchain.Disposals, sceneStore.Disposals, authoring.Disposals));
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProjectTransition_RefusesCleanDocumentWithAcceptedIoThenFencesOldOwnerAtSuccessfulSwitch(bool di)
    {
        using var files = new DocumentFiles(); var sceneStore = new DocumentStore();
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => new Projects.EditorProjectsTests.Store(files.Project))
                .Singleton<IProjectToolchain>(_ => new Projects.EditorProjectsTests.Toolchain()).Singleton<ISceneDocumentStore>(_ => sceneStore));
            context.UseAetherAuthoring().UseAetherEditorDocuments().UseAetherEditorProjects();
        }).Build();
        var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>()); var first = await projects.OpenAsync(files.Project.ManifestPath);
        var documents = first.Documents!; Assert.True(await documents.LoadAsync("room.aetherscene")); Assert.False(documents.Read().IsDirty);
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal(); var cancellationObserved = false;
        sceneStore.Write = async (_, _, _, token) =>
        {
            using var registration = token.Register(() => cancellationObserved = true); entered.SetResult();
            await finish.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("same-content");
        };
        var saving = documents.SaveAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(documents.Read().IsDirty); Assert.Equal(DocumentOperation.Saving, documents.Read().Operation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.OpenAsync(files.Project.ManifestPath));
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.CloseAsync());
        Assert.Same(first, projects.Active); Assert.False(first.IsClosed); Assert.False(documents.Read().IsClosed); Assert.False(cancellationObserved);
        finish.SetResult(); await saving;
        var second = await projects.OpenAsync(files.Project.ManifestPath); Assert.NotSame(first, second); Assert.True(first.IsClosed); Assert.True(documents.Read().IsClosed);
        Assert.Throws<ObjectDisposedException>(() => documents.Apply(DocumentData.Edit(7, 30)));
        Assert.Throws<ObjectDisposedException>(() => { _ = documents.NewAsync(DocumentData.Empty()); });
        Assert.Null(second.Documents!.Read().Content);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProjectRegistrationBeforeDocuments_PreservesExistingMetadataOnlyComposition(bool di)
    {
        using var files = new DocumentFiles();
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEditorProjects(); context.UseAetherAuthoring().UseAetherEditorDocuments();
        }).Build();
        var factory = Assert.IsAssignableFrom<IFactory<ProjectSession, ProjectSessionArguments>>(app.Services.Resolve<IFactory<ProjectSession, ProjectSessionArguments>>());
        await using var project = factory.Create(new(files.Project, new("10.0.402")));
        Assert.Null(project.Documents); Assert.False(project.IsClosed); Assert.Equal(0, project.CreateBuildRequest(0).SourceRevision);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProjectFactory_RespectsCustomDocumentFactoryAndRejectsNullProductWithoutSelectingProject(bool di)
    {
        using var files = new DocumentFiles(); var calls = new List<ResolvedProject>();
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => new Projects.EditorProjectsTests.Store(files.Project))
                .Singleton<IProjectToolchain>(_ => new Projects.EditorProjectsTests.Toolchain())
                .Singleton<IFactory<SceneDocumentSession, ResolvedProject>>(_ => new Factory<SceneDocumentSession, ResolvedProject>(project => { calls.Add(project); return null!; })));
            context.UseAetherAuthoring().UseAetherEditorDocuments().UseAetherEditorProjects();
        }).Build();
        var projects = Assert.IsType<EditorProjects>(app.Services.Resolve<EditorProjects>());
        Assert.Equal("Document session factory returned null.", (await Assert.ThrowsAsync<InvalidOperationException>(() => projects.OpenAsync(files.Project.ManifestPath))).Message);
        Assert.Equal(files.Project, Assert.Single(calls)); Assert.Null(projects.Active); Assert.False(projects.IsClosed);
    }

    [Fact]
    public async Task ProjectCandidateCleanupFailure_CompletesRejectedOpenAndRetainsOwnedCandidateUntilCloseRetry()
    {
        using var files = new DocumentFiles(); var authoring = new DocumentData.Sessions(); var store = new DocumentStore();
        var cleanup = new IOException("candidate cleanup retry"); var attempt = 0;
        var sessions = new Projects.EditorProjectsTests.Sessions
        {
            Activate = arguments =>
            {
                var document = new SceneDocumentSession(arguments.Project, store, authoring);
                Assert.True(document.NewAsync(DocumentData.Scene()).GetAwaiter().GetResult());
                if (authoring.Products.Count == 2) authoring.Products[1].BeforeDispose = () => { if (++attempt == 1) throw cleanup; };
                return new(arguments, document);
            }
        };
        await using var projects = new EditorProjects(new Projects.EditorProjectsTests.Store(files.Project), new Projects.EditorProjectsTests.Toolchain(), sessions);
        var first = await projects.OpenAsync(files.Project.ManifestPath);
        var rejected = projects.OpenAsync(files.Project.ManifestPath);
        var error = await Assert.ThrowsAsync<AggregateException>(() => rejected.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, error.InnerExceptions.Count); Assert.IsType<InvalidOperationException>(error.InnerExceptions[0]); Assert.Same(cleanup, error.InnerExceptions[1]);
        Assert.Same(first, projects.Active); Assert.False(first.IsClosed); Assert.True(first.Documents!.Read().IsDirty);
        Assert.Equal(2, sessions.Products.Count); Assert.True(sessions.Products[1].IsClosed);
        Assert.Equal(1, authoring.Products[1].Disposals); Assert.Equal(0, authoring.Products[0].Disposals);
        Assert.Throws<InvalidOperationException>(() => { _ = projects.OpenAsync(files.Project.ManifestPath); });
        Assert.True(await first.Documents.CloseAsync(DocumentCloseChoice.Discard)); await projects.CloseAsync();
        Assert.Equal(2, authoring.Products[1].Disposals); Assert.Equal(1, authoring.Products[0].Disposals); Assert.Null(projects.Active);
        Assert.Null(projects.Error); Assert.Equal(0, store.Disposals); Assert.Equal(0, authoring.Disposals);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
}
