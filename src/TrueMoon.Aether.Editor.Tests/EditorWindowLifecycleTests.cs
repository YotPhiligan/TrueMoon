using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorWindowLifecycleTests
{
    [Theory]
    [InlineData("component", DocumentCloseChoice.Save)]
    [InlineData("component", DocumentCloseChoice.Discard)]
    [InlineData("component", DocumentCloseChoice.Cancel)]
    [InlineData("header", DocumentCloseChoice.Save)]
    [InlineData("header", DocumentCloseChoice.Discard)]
    [InlineData("header", DocumentCloseChoice.Cancel)]
    [InlineData("native", DocumentCloseChoice.Save)]
    [InlineData("native", DocumentCloseChoice.Discard)]
    [InlineData("native", DocumentCloseChoice.Cancel)]
    public void ComponentHeaderAndNativeClose_ReachOneExistingDecisionWithoutDuplicateSaveOrOwnership(string source, DocumentCloseChoice choice)
    {
        using var h = new EditorCloseFixture(); h.Data.Owner.Run(DocumentCommand.New); h.Data.Documents.Refresh();
        Assert.Empty(h.Connection.SnapshotClosingHandlers());
        var before = h.Data.Owner.Document.Read().Content;
        var close = h.Request(source); h.Data.Owner.Decision();
        Assert.False(close.IsCompleted); Assert.True(h.Data.Owner.Commands.Read().Exiting);
        Assert.False(h.Root.IsDisposed); Assert.False(h.Root.EditorView.IsDisposed); Assert.Empty(h.Data.Owner.Store.Writes);
        Assert.True(h.Data.Owner.Commands.Choose(choice, choice == DocumentCloseChoice.Save ? "saved.aetherscene" : null));
        var accepted = EditorWindowTestGraph.Complete(close);
        Assert.Equal(choice != DocumentCloseChoice.Cancel, accepted);
        Assert.Equal(choice == DocumentCloseChoice.Save ? 1 : 0, h.Data.Owner.Store.Writes.Count);
        Assert.Equal(choice == DocumentCloseChoice.Cancel ? 0 : 1, h.Data.Owner.RuntimeStops);
        Assert.Equal(source == "header" ? 1 : 0, h.HeaderRequests);
        Assert.Equal(source == "native" ? 0 : 1, h.ControllerRequests);
        Assert.False(h.Root.IsDisposed); Assert.False(h.Hosted.IsClosed); Assert.True(h.Root.IsHosted);
        Assert.False(h.Data.Owner.Projects.IsClosed); Assert.Equal(0, h.Runtime.Launches); Assert.Equal(0, h.Engine.Gpu.ResourceCount);
        if (choice == DocumentCloseChoice.Cancel)
        {
            Assert.Same(before, h.Data.Owner.Document.Read().Content); Assert.False(h.Data.Owner.Commands.Read().Closed);
            Assert.False(h.Data.Owner.Commands.Read().Exiting); Assert.False(h.Data.Owner.Commands.Read().AwaitingDecision);
        }
        else { Assert.Null(h.Data.Owner.Document.Read().Content); Assert.True(h.Data.Owner.Commands.Read().Closed); }
    }

    [Fact]
    public void SaveErrorVeto_IsRetryableWithoutLateCloseOrSecondSaveAndBorrowedRootRemainsAlive()
    {
        using var h = new EditorCloseFixture(); h.Data.Owner.Run(DocumentCommand.New);
        var before = h.Data.Owner.Document.Read().Content; var failure = new IOException("save failed");
        h.Data.Owner.Store.Write = (_, _, _, _) => Task.FromException<SceneFileStamp>(failure);
        var close = h.Request("component"); h.Data.Owner.Decision();
        Assert.True(h.Data.Owner.Commands.Choose(DocumentCloseChoice.Save, "failed.aetherscene"));
        Assert.False(EditorWindowTestGraph.Complete(close)); Assert.Same(failure, h.Data.Owner.Commands.Read().Error);
        Assert.Same(before, h.Data.Owner.Document.Read().Content); Assert.True(h.Data.Owner.Document.Read().IsDirty);
        Assert.Single(h.Data.Owner.Store.Writes); Assert.Equal(0, h.Data.Owner.RuntimeStops); Assert.False(h.Root.IsDisposed);
        var retry = h.Request("native"); h.Data.Owner.Decision();
        Assert.True(h.Data.Owner.Commands.Choose(DocumentCloseChoice.Discard)); Assert.True(EditorWindowTestGraph.Complete(retry));
        Assert.Single(h.Data.Owner.Store.Writes); Assert.Equal(1, h.Data.Owner.RuntimeStops);
        Assert.False(h.Root.IsDisposed); Assert.False(h.Hosted.IsClosed); Assert.Empty(h.Connection.SnapshotClosingHandlers());
    }

    [Fact]
    public void ConcurrentComponentAndNativeClose_DrainIgnoredSaveOnceBeforeRuntimeStop()
    {
        using var h = new EditorCloseFixture(); h.Data.Owner.Run(DocumentCommand.Open, "room.aetherscene");
        var entered = DocumentData.Signal(); var release = DocumentData.Signal(); var canceled = DocumentData.Signal();
        h.Data.Owner.Store.Write = async (_, _, _, token) =>
        {
            using var registration = token.Register(() => canceled.TrySetResult()); entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); return new("committed");
        };
        Assert.True(h.Data.Owner.Commands.Execute(DocumentCommand.Save)); var save = h.Data.Owner.Commands.Pending;
        DocumentUiFixture.Wait(entered.Task);
        var first = h.Request("component"); var second = h.Request("native");
        try
        {
            DocumentUiFixture.Wait(canceled.Task);
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted); Assert.False(save.IsCompleted);
            Assert.Single(h.Data.Owner.Store.Writes); Assert.Equal(0, h.Data.Owner.RuntimeStops);
            Assert.False(h.Root.IsDisposed); Assert.True(h.Root.IsHosted);
        }
        finally { release.TrySetResult(); }
        Assert.True(EditorWindowTestGraph.Complete(first)); Assert.True(EditorWindowTestGraph.Complete(second)); DocumentUiFixture.Wait(save);
        Assert.Single(h.Data.Owner.Store.Writes); Assert.Equal(1, h.Data.Owner.RuntimeStops); Assert.Equal(1, h.ControllerRequests);
        Assert.False(h.Root.IsDisposed); Assert.False(h.Hosted.IsClosed);
    }

    [Fact]
    public void Shutdown_UpgradesPendingDecisionAndStopsOnceWithoutASecondUserExitOrSave()
    {
        using var h = new EditorCloseFixture(); h.Data.Owner.Run(DocumentCommand.New);
        var user = h.Request("component"); h.Data.Owner.Decision();
        var forced = h.Adapter.HandleClosingAsync(UiCloseReason.Shutdown);
        Assert.True(EditorWindowTestGraph.Complete(forced)); Assert.True(EditorWindowTestGraph.Complete(user));
        Assert.Empty(h.Data.Owner.Store.Writes); Assert.Equal(1, h.Data.Owner.RuntimeStops); Assert.Equal(1, h.ControllerRequests);
        Assert.True(h.Data.Owner.Commands.Read().Closed); Assert.False(h.Data.Owner.Commands.Read().AwaitingDecision);
        Assert.False(h.Root.IsDisposed); Assert.True(h.Root.IsHosted); Assert.False(h.Hosted.IsClosed);
        Assert.True(EditorWindowTestGraph.Complete(h.Adapter.HandleClosingAsync(UiCloseReason.Shutdown))); Assert.Equal(1, h.Data.Owner.RuntimeStops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyWorkspacePresentation_ReadiesTypedRootOnceWithoutGpuAndAdapterDisposalBorrowsTreeAndEngine(bool framed)
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime();
        using var app = EditorWindowTestGraph.Build(files, runtime);
        var engine = app.Services.Resolve<AetherHost>()!; var model = app.Services.Resolve<EditorViewModel>()!;
        var root = new EditorMainWindow(model, app.Services.Resolve<EditorDocumentViewModel>()!, app.Services.Resolve<IFactory<EditorView>>()!,
            new EditorMainWindowStyle(), new("Empty", new(1280, 800)));
        var hosted = new HostedUiWindow(new SilkUiWindowOptions(), new NoWindowSessions(), () => throw new Exception("No native root expected"));
        using var adapter = new EditorWindow(hosted, app.Services.Resolve<EditorSession>()!, app.Services.Resolve<EditorWorkspace>()!,
            app.Services.Resolve<EditorViewportBinding>()!, engine);
        Element actual = framed ? WindowFrame.CreateDefault(root.Title, root) : root;
        using var ui = InspectorFixture.Ui(actual); ui.Update();
        Assert.False(adapter.Ready.IsCompleted); Assert.Equal(0, engine.Gpu.ResourceCount); Assert.Equal(0, runtime.Launches);
        DocumentUiFixture.Wait(adapter.StartAsync(default)); DocumentUiFixture.Wait(adapter.StartAsync(default));
        adapter.Presented(ui); DocumentUiFixture.Wait(adapter.WaitReadyAsync(default)); var ready = adapter.Ready;
        adapter.Presented(ui); Assert.Same(ready, adapter.Ready); Assert.True(ready.IsCompletedSuccessfully);
        Assert.Same(root.EditorView, EditorMainWindow.FromRoot(ui.Root).EditorView); Assert.Null(root.EditorView.Canvas.DrawnFrame);
        Assert.Equal(0, engine.Gpu.ResourceCount); Assert.Equal(0, engine.Gpu.OpenHandleCount); Assert.Equal(0, runtime.Launches);
        var dispose = adapter.DisposeAsync().AsTask(); Assert.Same(dispose, adapter.DisposeAsync().AsTask()); DocumentUiFixture.Wait(dispose);
        Assert.False(root.IsDisposed); Assert.False(hosted.Completion.IsCompleted); Assert.False(app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed);
        model.Refresh(engine.Supervisor.Status, engine.Editor.Status); Assert.False(root.EditorView.IsDisposed);
        ui.Dispose(); DocumentUiFixture.Wait(hosted.DisposeAsync().AsTask()); Assert.True(root.IsDisposed);
    }

    private sealed class NoWindowSessions : IUiWindowSessionFactory
    {
        public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => true;
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null) => throw new Exception("No native session expected");
    }
}

internal sealed class EditorCloseFixture : IDisposable
{
    internal EditorMainWindowFixture Data { get; } = new();
    private readonly ProjectFixture _files = new();
    private readonly IApp _app;
    private readonly EditorPreview _preview;
    private readonly EditorTransformViewModel _transform;
    private readonly EditorHeaderView _header;
    internal EditorWindowNeverRuntime Runtime { get; } = new();
    internal AetherHost Engine { get; }
    internal EditorMainWindow Root { get; }
    internal WindowHostConnection Connection { get; }
    internal HostedUiWindow Hosted { get; }
    internal EditorWindow Adapter { get; }
    internal int HeaderRequests, ControllerRequests;
    private Task<bool>? _headerClose;
    internal EditorCloseFixture()
    {
        _app = EditorWindowTestGraph.Build(_files, Runtime); Engine = _app.Services.Resolve<AetherHost>()!;
        var viewport = new EditorViewportBinding(false); var workspace = new EditorWorkspace(EditorScene.Demo());
        var session = new EditorSession(new AetherEditorOptions(), Engine, Data.Owner.Projects, workspace,
            new EditorCommands((_, _) => Task.CompletedTask, default), Data.Owner.Commands, Data.Model, viewport);
        Hosted = new(new SilkUiWindowOptions(), new Sessions(), () => throw new Exception("No native allocation expected"));
        Adapter = new(Hosted, session, workspace, viewport, Engine);
        DocumentUiFixture.Wait(Adapter.StartAsync(default)); DocumentUiFixture.Wait(Adapter.StartAsync(default));
        Root = Data.Create(); Connection = Root.AttachHost(new Controller(() => { ControllerRequests++; return Adapter.HandleClosingAsync(UiCloseReason.Application); }));
        _preview = new((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        _transform = new(Data.Owner.Commands, _preview);
        _header = new(Data.Model, Data.Documents, _transform, new EditorHeaderViewStyle(), new(Root.EditorView.Overlays, "Editor", () =>
        { HeaderRequests++; _headerClose = Root.RequestCloseAsync(); }));
    }
    internal Task<bool> Request(string source)
    {
        if (source == "component") return Root.RequestCloseAsync();
        if (source == "native") return Adapter.HandleClosingAsync(UiCloseReason.Native);
        _header.Menu.Groups.Single(group => group.Id == "file").Actions.Single(action => action.Id == "file.exit").Command.Execute();
        return _headerClose ?? throw new InvalidOperationException("Header did not request coordinated close.");
    }
    public void Dispose()
    {
        Adapter.Dispose(); Connection.Dispose(); _header.Dispose(); Root.Dispose(); _transform.Dispose(); _preview.Dispose();
        DocumentUiFixture.Wait(Hosted.DisposeAsync().AsTask()); _app.Dispose(); Data.Dispose(); _files.Dispose();
    }
    private sealed class Controller(Func<Task<bool>> close) : IWindowController
    {
        public void Minimize() => throw new Exception("No native command expected");
        public void Maximize() => throw new Exception("No native command expected");
        public void Restore() => throw new Exception("No native command expected");
        public Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default) => close().WaitAsync(cancellationToken);
    }
    private sealed class Sessions : IUiWindowSessionFactory
    {
        public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => true;
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null) => throw new Exception("No native session expected");
    }
}
