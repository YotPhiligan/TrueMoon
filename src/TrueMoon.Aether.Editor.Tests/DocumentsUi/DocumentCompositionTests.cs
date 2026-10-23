using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class DocumentCompositionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothProviders_ResolveSharedModelStyleAndCoordinatorWhileUiOwnsIndependentFactoryProducts(bool di)
    {
        using var files = new DocumentFiles(); var store = new DocumentStore(); var authoring = new DocumentData.Sessions();
        var models = 0; var stops = 0; var style = new CountingStyle();
        using var app = App.Builder(context => { if (di) context.UseDI(); }).Setup(context =>
        {
            context.Services(services => services
                .Singleton<IProjectStore>(_ => new EditorProjectsTests.Store(files.Project))
                .Singleton<IProjectToolchain>(_ => new EditorProjectsTests.Toolchain())
                .Singleton<ISceneDocumentStore>(_ => store)
                .Singleton<IFactory<TrueMoon.Aether.Scenes.Authoring.ISceneAuthoringSession, TrueMoon.Aether.Scenes.SceneDocument>>(_ => authoring));
            context.UseAetherEditorDocuments().UseAetherEditorProjects();
            context.Services(services => services
                .Singleton<EditorDocumentCommands>(resolver => new(resolver.Resolve<EditorProjects>()!, () => true, () => { stops++; return Task.CompletedTask; }))
                .Singleton<EditorDocumentViewModel>(resolver => { models++; return new(resolver.Resolve<EditorDocumentCommands>()!); })
                .Singleton<IViewStyle<EditorDocumentView>>(_ => style)
                .Singleton<IFactory<EditorDocumentView>>(resolver => new Factory<EditorDocumentView>(() => new(resolver.Resolve<EditorDocumentViewModel>()!, resolver.Resolve<IViewStyle<EditorDocumentView>>()!))));
        }).Build();
        var projects = app.Services.Resolve<EditorProjects>()!; DocumentUiFixture.Wait(projects.OpenAsync(files.Project.ManifestPath));
        var commands = app.Services.Resolve<EditorDocumentCommands>()!; var model = app.Services.Resolve<EditorDocumentViewModel>()!;
        var factory = app.Services.Resolve<IFactory<EditorDocumentView>>()!;
        var first = Assert.IsType<EditorDocumentView>(factory.Create()); var second = Assert.IsType<EditorDocumentView>(factory.Create());
        using var firstUi = DocumentUiFixture.Ui(first); using var secondUi = DocumentUiFixture.Ui(second); firstUi.Update(); secondUi.Update();
        Assert.Same(commands, app.Services.Resolve<EditorDocumentCommands>()); Assert.Same(model, app.Services.Resolve<EditorDocumentViewModel>());
        Assert.Same(style, app.Services.Resolve<IViewStyle<EditorDocumentView>>()); Assert.Same(factory, app.Services.Resolve<IFactory<EditorDocumentView>>());
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext); Assert.NotSame(first, second); Assert.NotSame(first.PathInput, second.PathInput);
        Assert.Equal(1, models); Assert.Equal(2, style.Configurations);
        model.PathDraft = "shared.aetherscene"; firstUi.Update(); secondUi.Update(); Assert.Equal("shared.aetherscene", first.PathInput.Value); Assert.Equal("shared.aetherscene", second.PathInput.Value);
        firstUi.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed);
        model.Execute(DocumentCommand.New); DocumentUiFixture.Wait(commands.Pending); model.Refresh(); secondUi.Update(); Assert.True(second.Commands[DocumentCommand.Save].IsEnabled);
        Assert.Equal(0, store.Disposals); Assert.Equal(0, authoring.Disposals); Assert.Equal(0, stops);
        app.Dispose();
        Assert.True(commands.Read().Closed); Assert.Equal(1, stops); Assert.Equal(1, store.Disposals); Assert.Equal(1, authoring.Disposals);
        Assert.Equal(1, Assert.Single(authoring.Products).Disposals); Assert.False(second.IsDisposed);
        Assert.Throws<ObjectDisposedException>(model.Refresh); secondUi.Dispose(); app.Dispose(); Assert.Equal(1, stops);
    }

    [Fact]
    public async Task CommandsWithRealStore_SaveReopenAndExternalConflictPreserveFileAndRecoverBySaveAs()
    {
        var codec = new SceneFileCodec(DocumentData.Factory()); var store = new JsonSceneDocumentStore(codec);
        await using var owner = new DocumentUiFixture(sceneStore: store);
        Assert.True(await owner.Document.NewAsync(DocumentData.Scene()));
        owner.Run(DocumentCommand.Save, "original.aetherscene"); var originalPath = owner.Document.Read().Path!;
        Assert.True(File.Exists(originalPath)); var initialBytes = await File.ReadAllBytesAsync(originalPath);
        owner.Run(DocumentCommand.Close); Assert.Null(owner.Document.Read().Content);
        owner.Run(DocumentCommand.Open, "original.aetherscene"); DocumentData.AssertScene(owner.Document.Read().Content!.Document);
        Assert.True(owner.Document.Apply(DocumentData.Edit(7, 63)).Succeeded); owner.Run(DocumentCommand.Save, "ignored.aetherscene");
        var savedBytes = await File.ReadAllBytesAsync(originalPath);
        Assert.False(owner.Document.Read().IsDirty); Assert.False(initialBytes.SequenceEqual(savedBytes));
        owner.Run(DocumentCommand.Close); owner.Run(DocumentCommand.Open, originalPath);
        Assert.Equal(63d, DocumentData.X(owner.Document.Read().Content!)); Assert.Equal(8, owner.Document.Read().Content!.Document.Revision);
        Assert.True(owner.Document.Apply(DocumentData.Edit(8, 91)).Succeeded);
        var externallyChanged = codec.Encode(DocumentData.Empty(30)); await File.WriteAllBytesAsync(originalPath, externallyChanged);
        owner.Run(DocumentCommand.Save); Assert.IsType<SceneFileConflictException>(owner.Commands.Read().Error);
        Assert.Equal(externallyChanged, await File.ReadAllBytesAsync(originalPath)); Assert.True(owner.Document.Read().IsDirty);
        Assert.Equal(originalPath, owner.Document.Read().Path); Assert.Equal(91d, DocumentData.X(owner.Document.Read().Content!));
        owner.Run(DocumentCommand.SaveAs, "recovered.aetherscene"); Assert.Null(owner.Commands.Read().Error); Assert.False(owner.Document.Read().IsDirty);
        var recovered = await store.ReadAsync(owner.Document.Read().Path!); Assert.Equal(9, recovered.Document.Revision);
        using var resolved = DocumentData.Factory().Create(recovered.Document); Assert.Equal(91d, DocumentData.X(resolved.Read()));
        Assert.Equal(externallyChanged, await File.ReadAllBytesAsync(originalPath)); Assert.False(File.Exists(Path.Combine(owner.Files.Project.ScenesDirectory, "ignored.aetherscene")));
    }

    private sealed class CountingStyle : IViewStyle<EditorDocumentView>
    { public int Configurations; public void Configure(IViewStyleBuilder<EditorDocumentView> styles) { Configurations++; new EditorDocumentViewStyle().Configure(styles); } }
}
