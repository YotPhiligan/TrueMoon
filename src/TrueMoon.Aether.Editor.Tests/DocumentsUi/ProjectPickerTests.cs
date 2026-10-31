using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class ProjectPickerTests
{
    [Fact]
    public void LegacyCompositionWithoutBinding_KeepsDocumentCommandsAndRejectsProjectMenuActions()
    {
        using var owner = new DocumentUiFixture(); var pending = owner.Commands.Pending;
        Assert.False(owner.Commands.Read().ProjectSwitchEnabled); Assert.True(owner.Commands.Read().ProjectReady);
        Assert.False(owner.Commands.CanExecute(DocumentCommand.OpenProject)); Assert.False(owner.Commands.CanExecute(DocumentCommand.CloseProject));
        Assert.False(owner.Commands.Execute(DocumentCommand.OpenProject, "unused.aetherproj"));
        Assert.Same(pending, owner.Commands.Pending); Assert.True(owner.Commands.CanExecute(DocumentCommand.New));
        owner.Run(DocumentCommand.New); Assert.True(owner.Document.Read().IsDirty); Assert.Single(owner.Authoring.Products);
    }

    [Fact]
    public void EmptyWorkspace_OpenCloseReopen_BindsActualProjectBeforeCommandsReopen()
    {
        using var h = new Fixture(false);
        Assert.True(h.Commands.CanExecute(DocumentCommand.OpenProject)); Assert.False(h.Commands.CanExecute(DocumentCommand.New));
        Assert.False(h.Commands.CanExecute(DocumentCommand.CloseProject));
        h.OpenNext(); h.Complete(); var selected = h.Projects.Active!;
        Assert.Same(h.NextProject, selected.Project); Assert.Same(selected, Assert.Single(h.Bindings));
        Assert.True(h.Commands.Read().ProjectReady); Assert.True(h.Commands.CanExecute(DocumentCommand.New));
        h.Run(DocumentCommand.CloseProject); Assert.Null(h.Projects.Active); Assert.Null(h.Bindings[1]);
        Assert.True(selected.IsClosed); Assert.False(h.Commands.Read().Closed); Assert.True(h.Commands.Read().ProjectReady);
        h.OpenNext(); h.Complete(); Assert.NotSame(selected, h.Projects.Active); Assert.Equal(3, h.Bindings.Count);
        Assert.Empty(h.Authoring.Products);
    }

    [Theory]
    [InlineData(DocumentCloseChoice.Save)] [InlineData(DocumentCloseChoice.Discard)] [InlineData(DocumentCloseChoice.Cancel)]
    public void DirtySwitch_UsesSharedDecisionAndAdmissionWithoutDroppingOldHistory(DocumentCloseChoice choice)
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var document = h.Current.Read().Content!.Document;
        var stops = h.PreviewStops; h.OpenNext(); h.Decision();
        Assert.Same(old, h.Commands.Read().Project); Assert.Equal(stops, h.PreviewStops);
        Assert.Equal(2, h.Sessions.Products.Count);
        foreach (var action in new[] { DocumentCommand.New, DocumentCommand.Save, DocumentCommand.OpenProject, DocumentCommand.CloseProject })
        { Assert.False(h.Commands.CanExecute(action)); Assert.False(h.Commands.Execute(action)); }
        Assert.Throws<InvalidOperationException>(() => h.Commands.Apply(document, DocumentData.Edit(document.Revision, 99)));
        Assert.True(h.Commands.Choose(choice, "saved-before-switch.aetherscene")); h.Complete();
        Assert.Null(h.Commands.Read().Error); Assert.False(h.Commands.Read().Busy);
        if (choice == DocumentCloseChoice.Cancel)
        {
            Assert.Same(old, h.Projects.Active); Assert.Same(document, h.Current.Read().Content!.Document);
            Assert.Equal(1, h.Current.Read().Content!.UndoCount); Assert.False(old.IsClosed);
            Assert.Equal(stops, h.PreviewStops); Assert.Single(h.Bindings); Assert.True(h.Sessions.Products[1].IsClosed);
        }
        else
        {
            Assert.NotSame(old, h.Projects.Active); Assert.True(old.IsClosed); Assert.Null(h.Current.Read().Content);
            Assert.Same(h.Projects.Active, h.Bindings.Last()); Assert.Equal(stops + 1, h.PreviewStops);
            if (choice == DocumentCloseChoice.Save)
            {
                var saved = Assert.Single(h.Files.Writes); Assert.Same(document, saved.Document);
                Assert.Equal(Path.Combine(h.First.Resolved.ScenesDirectory, "saved-before-switch.aetherscene"), saved.Path);
            }
        }
        if (choice != DocumentCloseChoice.Save) Assert.Empty(h.Files.Writes);
    }

    [Fact]
    public void InvalidCandidate_DoesNotAskDiscardStopPreviewOrChangeContext()
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var content = h.Current.Read().Content!;
        var error = new IOException("SDK rejected candidate"); var stops = h.PreviewStops;
        h.Sdk.Probe = (_, _) => Task.FromException<ProjectToolchainInfo>(error);
        h.OpenNext(); h.Complete();
        Assert.Same(error, h.Commands.Read().Error); Assert.False(h.Commands.Read().AwaitingDecision);
        Assert.Same(old, h.Projects.Active); Assert.Same(content.Document, h.Current.Read().Content!.Document);
        Assert.Equal(content.UndoCount, h.Current.Read().Content!.UndoCount);
        Assert.Single(h.Bindings); Assert.Equal(stops, h.PreviewStops); Assert.Empty(h.Files.Writes);
    }

    [Fact]
    public void PreviewDrainFailure_PreservesOwnerAndHistoryAndCanRetrySwitch()
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var content = h.Current.Read().Content!;
        var error = new IOException("Preview drain failed"); h.StopPreview = () => Task.FromException(error);
        h.OpenNext(); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Discard); h.Complete();
        Assert.Same(error, h.Commands.Read().Error); Assert.Same(old, h.Projects.Active);
        Assert.Same(content.Document, h.Current.Read().Content!.Document); Assert.Single(h.Bindings);
        Assert.True(h.Sessions.Products[1].IsClosed); h.StopPreview = null;
        h.OpenNext(); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Discard); h.Complete();
        Assert.Null(h.Commands.Read().Error); Assert.True(old.IsClosed); Assert.Same(h.NextProject, h.Projects.Active!.Project);
    }

    [Fact]
    public void CancelDuringUncooperativePreparation_DrainsAndCannotPublishLate()
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var expected = h.Current.Read().Content!;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Sdk.Probe = async (_, _) => { entered.SetResult(); await finish.Task; return new("10.0.403"); };
        h.OpenNext(); DocumentUiFixture.Wait(entered.Task); h.Commands.Cancel();
        Assert.True(h.Commands.Read().Busy); Assert.False(h.Commands.Pending.IsCompleted); Assert.Same(old, h.Projects.Active);
        finish.SetResult(); h.Complete(); Assert.Null(h.Commands.Read().Error);
        Assert.Same(expected.Document, h.Current.Read().Content!.Document); Assert.Equal(1, expected.UndoCount);
        Assert.Single(h.Bindings); Assert.Single(h.Sessions.Products); Assert.False(old.IsClosed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PostCommitCleanupAndBindingFailures_KeepActualIdentityAndAllowCloseRetry(bool bindingFails)
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!;
        var cleanup = new IOException("cleanup"); var binding = new IOException("binding");
        h.Authoring.Products[0].BeforeDispose = () => throw cleanup;
        h.OnBind = _ => { if (bindingFails) throw binding; };
        h.OpenNext(); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Discard); h.Complete();
        var state = h.Commands.Read(); Assert.NotSame(old, state.Project); Assert.Same(h.Projects.Active, state.Project);
        Assert.Same(state.Project, h.Bindings.Last()); Assert.True(old.IsClosed);
        Assert.Equal(!bindingFails, state.ProjectReady); Assert.False(h.Commands.CanExecute(DocumentCommand.OpenProject));
        Assert.True(h.Commands.CanExecute(DocumentCommand.CloseProject));
        if (bindingFails)
        {
            var errors = Assert.IsType<AggregateException>(state.Error).Flatten().InnerExceptions;
            Assert.Contains(cleanup, errors); Assert.Contains(binding, errors); Assert.False(h.Commands.CanExecute(DocumentCommand.New));
        }
        else Assert.Same(cleanup, state.Error);
        h.OnBind = null; h.Authoring.Products[0].BeforeDispose = null; h.Run(DocumentCommand.CloseProject);
        Assert.Null(h.Commands.Read().Error); Assert.True(h.Commands.Read().ProjectReady);
        Assert.Null(h.Bindings.Last()); Assert.Null(h.Projects.Active); Assert.Equal(2, h.Authoring.Products[0].Disposals);
    }

    [Fact]
    public void RebindingFailure_BlocksRuntimeAndDocumentActionsUntilRecovery()
    {
        using var h = new Fixture(); h.OnBind = _ => throw new IOException("context unavailable");
        h.OpenNext(); h.Complete(); using var model = new EditorDocumentViewModel(h.Commands); model.Refresh();
        Assert.False(h.Commands.Read().ProjectReady); Assert.True(model.BlocksRuntime);
        Assert.False(model.CanNew); Assert.True(model.CanCloseProject);
        h.OnBind = null; h.Run(DocumentCommand.CloseProject); model.Refresh();
        Assert.True(h.Commands.Read().ProjectReady); Assert.False(model.CanNew); Assert.True(model.CanOpenProject);
    }

    [Fact]
    public void CancellationDuringPreviewDrain_KeepsAdmissionUntilActualDrainAndPreservesOldOwner()
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var document = h.Current.Read().Content!.Document;
        var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.StopPreview = () => { entered.SetResult(); return finish.Task; };
        h.OpenNext(); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Discard); DocumentUiFixture.Wait(entered.Task);
        h.Commands.Cancel(); Assert.True(h.Commands.Read().Busy); Assert.False(h.Commands.Pending.IsCompleted);
        Assert.Same(old, h.Projects.Active); Assert.False(h.Commands.CanExecute(DocumentCommand.OpenProject));
        finish.SetResult(); h.Complete();
        Assert.Null(h.Commands.Read().Error); Assert.Same(old, h.Projects.Active);
        Assert.Same(document, h.Current.Read().Content!.Document); Assert.Equal(1, h.Current.Read().Content!.UndoCount);
        Assert.Single(h.Bindings); Assert.True(h.Sessions.Products[1].IsClosed);
    }

    [Fact]
    public void RuntimeOrDocumentIo_RejectsProjectActionsBeforeStartingWork()
    {
        using var h = new Fixture(); var pending = h.Commands.Pending; var reads = h.Metadata.ReadCalls;
        h.Idle = false;
        Assert.False(h.Commands.Execute(DocumentCommand.OpenProject, h.Next.Manifest));
        Assert.False(h.Commands.CanExecute(DocumentCommand.CloseProject)); Assert.Same(pending, h.Commands.Pending);
        Assert.Equal(reads, h.Metadata.ReadCalls); h.Idle = true;
        h.NewEditedScene(); var entered = DocumentData.Signal(); var finish = DocumentData.Signal();
        h.Files.Write = async (_, _, _, _) => { entered.SetResult(); await finish.Task; return new("saved"); };
        var saving = h.Current.SaveAsAsync("io.aetherscene"); DocumentUiFixture.Wait(entered.Task);
        Assert.False(h.Commands.CanExecute(DocumentCommand.OpenProject)); Assert.False(h.Commands.CanExecute(DocumentCommand.CloseProject));
        finish.SetResult(); DocumentUiFixture.Wait(saving); Assert.True(h.Commands.CanExecute(DocumentCommand.OpenProject));
    }

    [Fact]
    public void RetainedProjectPrompt_SeparatesDraftsRestoresFocusAndKeepsControlsAcrossThemeAndSwitch()
    {
        using var h = new Fixture(); using var model = new EditorDocumentViewModel(h.Commands);
        var tools = new EditorDocumentView(model, new EditorDocumentViewStyle(), compactHeader: true);
        var trigger = Button.Simple("Project"); var content = new VStack().WithChildren(trigger, tools);
        using var ui = DocumentUiFixture.Ui(content); ui.Update(); ui.Focus(trigger); tools.SynchronizeFocus(ui);
        var tree = DocumentUiFixture.Tree(tools).ToArray(); var field = tools.PathInput;
        model.PathDraft = "scene-draft.aetherscene"; model.ProjectPathDraft = h.Next.Manifest;
        model.Request(DocumentCommand.OpenProject); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.True(field.IsFocused); Assert.True(field.IsEnabled); Assert.Equal(h.Next.Manifest, field.Value);
        Assert.Equal("Путь проекта", field.AccessibleName); Assert.Contains(".aetherproj", tools.Title.Value);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(field, tools.PathInput);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.True(trigger.IsFocused); Assert.Equal(h.Next.Manifest, model.ProjectPathDraft);
        Assert.Equal("scene-draft.aetherscene", model.PathDraft);
        model.Request(DocumentCommand.Open); ui.Update(); Assert.Equal("scene-draft.aetherscene", field.Value);
        model.CancelPath(); h.Metadata.Project = h.NextProject;
        model.Request(DocumentCommand.OpenProject); ui.Update(); tools.SynchronizeFocus(ui);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); h.Complete(); model.Refresh(); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.Null(model.Document!.Content); Assert.Same(h.NextProject, model.Project!.Project);
        Assert.Equal(tree, DocumentUiFixture.Tree(tools).ToArray()); Assert.True(trigger.IsFocused);
    }

    [Fact]
    public void WorkspaceProjectIdentity_ResetsSelectionAndPreventsStaleRuntimeActionsBeforeRefresh()
    {
        using var h = new Fixture(); h.NewEditedScene(); using var docs = new EditorDocumentViewModel(h.Commands);
        var workspace = new EditorWorkspace(new("empty", 0, []), false); var calls = 0;
        var commands = new EditorCommands((_, _) => { calls++; return Task.CompletedTask; }, default);
        using var model = new EditorViewModel(workspace, commands, docs);
        var status = new GameEditorStatus(0, null, GameEditorOperation.Idle, null, null, false);
        model.Refresh(new(TrueMoon.Aether.Sessions.SessionState.Stopped), status);
        model.Select(DocumentData.Child.Value.ToString("D")); Assert.Equal(2, workspace.EntityCount);
        h.OpenNext(); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Discard); h.Complete();
        docs.Refresh(); model.Execute(EditorCommand.Build); DocumentUiFixture.Wait(commands.Pending); Assert.Equal(0, calls);
        model.Refresh(new(TrueMoon.Aether.Sessions.SessionState.Stopped), status);
        Assert.Same(h.Projects.Active, workspace.Project); Assert.True(model.HasProject); Assert.Null(model.SelectedId);
        Assert.Equal(0, model.EntityCount); Assert.True(model.CanBuild); model.Execute(EditorCommand.Build);
        DocumentUiFixture.Wait(commands.Pending); Assert.Equal(1, calls);
        h.Run(DocumentCommand.CloseProject); model.Refresh(new(TrueMoon.Aether.Sessions.SessionState.Stopped), status);
        Assert.False(model.HasProject); Assert.False(model.CanBuild); Assert.Empty(workspace.HierarchyItems);
    }

    [Fact]
    public void ProjectMenu_SharesCommandsAndRetainedPromptAndTracksCaptionAfterSwitch()
    {
        using var h = new Fixture(); using var docs = new EditorDocumentViewModel(h.Commands);
        using var preview = new EditorPreview((_, _) => throw new InvalidOperationException(), () => Task.CompletedTask);
        using var inspector = new EditorInspectorViewModel(h.Commands, preview);
        using var model = new EditorViewModel(new(new("empty", 0, []), false), new((_, _) => Task.CompletedTask, default), docs);
        var content = new VStack(); var overlays = new OverlayHost(content);
        var header = new EditorHeaderView(model, docs, inspector, new EditorHeaderViewStyle(), new(overlays, "Editor", () => { }));
        var tools = new EditorDocumentView(docs, new EditorDocumentViewStyle(), compactHeader: true);
        content.WithChildren(header, tools); using var ui = DocumentUiFixture.Ui(overlays); ui.Update();
        header.Menu.OpenGroup("project", ui); ui.Update();
        var item = DocumentUiFixture.Tree(overlays.Popup!).OfType<MenuItem>().Single(row => row.ActionId == "document.OpenProject");
        Assert.Same(header.DocumentCommands[DocumentCommand.OpenProject], item.Command);
        DocumentUiFixture.Click(ui, item); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.Null(overlays.Popup); Assert.True(tools.PathInput.IsFocused); Assert.Empty(h.Files.Reads);
        h.Metadata.Project = h.NextProject; tools.PathInput.Value = h.Next.Manifest;
        DocumentUiFixture.Click(ui, tools.ConfirmPath); h.Complete();
        docs.Refresh(); ui.Update(); tools.SynchronizeFocus(ui);
        Assert.Contains("Second project", header.Caption.Value); Assert.Equal(h.Next.Manifest, docs.ProjectPathDraft);
        Assert.Same(h.Projects.Active, docs.Project); Assert.True(header.DocumentCommands[DocumentCommand.CloseProject].CanExecute);
    }

    [Fact]
    public void ProjectSnapshot_PrecedesRuntimeReadersAndMixedFrameCannotEnableStaleCandidate()
    {
        using var h = new Fixture(); var old = h.Projects.Active;
        using var docs = new EditorDocumentViewModel(h.Commands); var workspace = new EditorWorkspace(new("empty", 0, []), false);
        var calls = 0; var commands = new EditorCommands((_, _) => { calls++; return Task.CompletedTask; }, default);
        using var model = new EditorViewModel(workspace, commands, docs);
        model.Refresh(() =>
        {
            Assert.Same(old, docs.Project);
            h.OpenNext(); h.Complete();
            return new(TrueMoon.Aether.Sessions.SessionState.Stopped);
        }, () => EditorWorkspaceTests.Status(GameEditorOperation.Idle, candidate: true));
        Assert.Same(old, workspace.Project); Assert.NotSame(workspace.Project, h.Projects.Active);
        Assert.False(model.CanStart); Assert.False(model.CanBuild);
        model.RunSimulation(); model.Execute(EditorCommand.Build); DocumentUiFixture.Wait(commands.Pending); Assert.Equal(0, calls);
        model.Refresh(() => new(TrueMoon.Aether.Sessions.SessionState.Stopped), () => EditorWorkspaceTests.Status(GameEditorOperation.Idle, candidate: false));
        Assert.Same(h.Projects.Active, workspace.Project); Assert.True(model.CanBuild); Assert.False(model.CanStart);
    }

    [Fact]
    public void FailedCloseSave_PreservesOldContextAndUndoAndSupportsUserRetry()
    {
        using var h = new Fixture(); h.NewEditedScene(); var old = h.Projects.Active!; var document = h.Current.Read().Content!.Document;
        var failure = new IOException("Save failed"); h.Files.Write = (_, _, _, _) => Task.FromException<TrueMoon.Aether.Scenes.SceneFileStamp>(failure);
        Assert.True(h.Commands.Execute(DocumentCommand.CloseProject)); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Save, "retry.aetherscene"); h.Complete();
        Assert.Same(failure, h.Commands.Read().Error); Assert.Same(old, h.Projects.Active);
        Assert.Same(document, h.Current.Read().Content!.Document); Assert.Equal(1, h.Current.Read().Content!.UndoCount);
        Assert.Single(h.Bindings); Assert.True(h.Commands.Read().ProjectReady);
        h.Files.Write = null;
        Assert.True(h.Commands.Execute(DocumentCommand.CloseProject)); h.Decision(); h.Commands.Choose(DocumentCloseChoice.Save, "retry.aetherscene"); h.Complete();
        Assert.Null(h.Projects.Active); Assert.Null(h.Commands.Read().Error); Assert.Null(h.Bindings.Last());
        Assert.Same(document, h.Files.Writes.Last().Document);
    }

    private sealed class Fixture : IDisposable
    {
        public ProjectFixture First { get; } = new(); public ProjectFixture Next { get; } = new();
        public ResolvedProject NextProject { get; }
        public DocumentStore Files { get; } = new(); public DocumentData.Sessions Authoring { get; } = new();
        public EditorProjectsTests.Store Metadata { get; } public EditorProjectsTests.Toolchain Sdk { get; } = new();
        public EditorProjectsTests.Sessions Sessions { get; } public EditorProjects Projects { get; }
        public EditorDocumentCommands Commands { get; }
        public List<ProjectSession?> Bindings { get; } = []; public Action<ProjectSession?>? OnBind;
        public Func<Task>? StopPreview; public int PreviewStops; public bool Idle = true;
        public SceneDocumentSession Current => Projects.Active!.Documents!;
        public Fixture(bool open = true)
        {
            NextProject = Next.Resolved with { Description = Next.Description with { Id = Guid.NewGuid(), Name = "Second project" } };
            Metadata = new(First.Resolved); Sessions = new() { Activate = args => new(args, new(args.Project, Files, Authoring)) };
            Projects = new(Metadata, Sdk, Sessions);
            Commands = new(Projects, () => Idle, () => Task.CompletedTask, default,
                () => { PreviewStops++; return StopPreview?.Invoke() ?? Task.CompletedTask; }, project => { Bindings.Add(project); OnBind?.Invoke(project); });
            if (open) Run(DocumentCommand.OpenProject, First.Manifest);
        }
        public void Run(DocumentCommand action, string path = "") { Assert.True(Commands.Execute(action, path)); Complete(); }
        public void OpenNext() { Metadata.Project = NextProject; Assert.True(Commands.Execute(DocumentCommand.OpenProject, Next.Manifest)); }
        public void Complete() => DocumentUiFixture.Wait(Commands.Pending);
        public void Decision() => Assert.True(SpinWait.SpinUntil(() => Commands.Read().AwaitingDecision, TimeSpan.FromSeconds(5)));
        public void NewEditedScene()
        { DocumentUiFixture.Wait(Current.NewAsync(DocumentData.Scene())); Assert.True(Current.Apply(DocumentData.Edit(7, 17)).Succeeded); }
        public void Dispose()
        {
            StopPreview = null; OnBind = null;
            foreach (var product in Authoring.Products) product.BeforeDispose = null;
            try { Commands.Dispose(); } finally { try { Projects.Dispose(); } finally { First.Dispose(); Next.Dispose(); } }
        }
    }
}
