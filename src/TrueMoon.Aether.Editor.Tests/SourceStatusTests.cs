using System.Collections.Concurrent;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class SourceStatusTests
{
    [Fact]
    public void Refresh_PullsCurrentSnapshotAndNotifiesOnlyChangedValuesOnOwnerThreadWithoutIo()
    {
        using var h = new SourcePresentationFixture(); var owner = Environment.CurrentManagedThreadId;
        Assert.Equal(EditorStatusKind.Ready, h.Status.SourceKind);
        var changes = new ConcurrentQueue<(string? Name, int Thread)>();
        h.Model.PropertyChanged += (_, args) => changes.Enqueue((args.PropertyName, Environment.CurrentManagedThreadId));
        var before = h.Model.Source; h.Bind(); var scan = SourcePresentationFixture.Wait(h.Control.Scanner.Next());
        SourcePresentationFixture.Wait(Task.Run(() => scan.Complete(SourceWatchFixture.Same)));
        h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged);
        Assert.Empty(changes); Assert.Same(before, h.Model.Source);
        h.Refresh(); Assert.Equal(EditorSourceState.ObservedUnchanged, h.Model.Source!.State);
        Assert.Equal(EditorStatusKind.Ready, h.Status.SourceKind);
        Assert.Contains(changes, change => change.Name == nameof(EditorViewModel.Source)); Assert.All(changes, change => Assert.Equal(owner, change.Thread));
        changes.Clear(); h.Refresh(); Assert.Empty(changes); Assert.Equal(1, h.Control.Scanner.Calls);
        var error = SourcePresentationFixture.Wait(Task.Run(() => Record.Exception(() => h.Refresh())));
        Assert.IsType<InvalidOperationException>(error); Assert.Empty(changes);
        h.Control.Hints.Latest.Signal(); h.Refresh();
        Assert.Equal(EditorSourceState.Checking, h.Model.Source.State); Assert.Equal(1, h.Control.Scanner.Calls);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ChangedAndUnavailableLeavePrimaryRuntimeStatusAndResumeStopAdmissionIntact(bool unavailable)
    {
        using var h = new SourcePresentationFixture(); h.StartPaused();
        h.Runtime = new(SessionState.Paused, Tick: 42, ProcessId: 317) { Identity = h.Runtime.Identity }; h.Refresh();
        var summary = h.Status.Summary; var kind = h.Status.Kind; var runtime = h.Status.RuntimeDetails;
        var calls = h.RuntimeCalls.ToArray();
        Assert.True(h.Model.CanPlay); Assert.True(h.Model.CanStop);
        h.Bind(); var scan = SourcePresentationFixture.Wait(h.Control.Scanner.Next());
        scan.Complete(unavailable ? EditorSourceScan.Unavailable("source coverage unavailable") : EditorSourceScan.Difference(h.Control.Files.Source));
        h.Until(snapshot => snapshot.State == (unavailable ? EditorSourceState.Unavailable : EditorSourceState.Changed)); h.Refresh();
        Assert.Equal(EditorStatusKind.Waiting, h.Status.SourceKind); Assert.Equal(summary, h.Status.Summary); Assert.Equal(kind, h.Status.Kind);
        Assert.Equal(runtime, h.Status.RuntimeDetails); Assert.True(h.Model.CanPlay); Assert.True(h.Model.CanStop);
        Assert.True(h.Status.HasSource); Assert.Contains(SourceWatchFixture.BuildA, h.Status.SourceDetails);
        Assert.Empty(h.Model.ErrorText); Assert.False(h.Status.HasError); Assert.Empty(h.Status.ErrorDetails); Assert.Equal(calls, h.RuntimeCalls);
        var source = h.Watch.Read();
        Assert.True(h.Playback.Execute(EditorCommand.Play)); SourcePresentationFixture.Wait(h.Playback.Pending);
        Assert.Equal(SessionState.Playing, h.Runtime.State); Assert.Same(source, h.Watch.Read());
        Assert.True(h.Playback.Execute(EditorCommand.Stop)); SourcePresentationFixture.Wait(h.Playback.Pending);
        Assert.Equal(SessionState.Stopped, h.Runtime.State); Assert.Same(source, h.Watch.Read()); Assert.Null(h.Playback.Error);
    }

    [Fact]
    public void SourceIndicatorUpdatesRetainFocusDraftSelectionHistoryAndControlsAcrossThemesAndDetach()
    {
        using var h = new SourcePresentationFixture(); using var view = h.CreateView(); h.Content.Add(view);
        using var ui = h.Ui(); ui.Update(); ui.Focus(h.Draft); h.Draft.Select(2, 3);
        h.Model.Select(DocumentData.Child.Value.ToString("D")); h.Transform.Fields[0].Value = "unfinished draft";
        var document = h.Active.Documents!.Read().Content!; var draftDocument = h.Draft.Document;
        var selected = h.Model.SelectedId; var sourceButton = view.SourceButton; var primary = view.SummaryButton;
        var body = h.Content.Children.ToArray(); var field = h.Transform.Fields[0]; var summary = h.Status.Summary;
        h.Bind(); var scan = SourcePresentationFixture.Wait(h.Control.Scanner.Next()); h.Refresh(); ui.Update();
        Assert.Equal(EditorStatusKind.Busy, h.Status.SourceKind);
        scan.Complete(EditorSourceScan.Difference(h.Control.Files.Source)); h.Until(snapshot => snapshot.State == EditorSourceState.Changed);
        h.Refresh(); ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(sourceButton, view.SourceButton); Assert.Same(primary, view.SummaryButton); Assert.Equal(body, h.Content.Children);
        Assert.True(h.Draft.IsFocused); Assert.Same(draftDocument, h.Draft.Document); Assert.Equal("unsaved text", h.Draft.Text);
        Assert.Equal(2, h.Draft.SelectionStart); Assert.Equal(3, h.Draft.SelectionLength);
        Assert.Equal("unfinished draft", field.Value); Assert.Equal(selected, h.Model.SelectedId);
        Assert.Same(document, h.Active.Documents.Read().Content); Assert.Equal(summary, h.Status.Summary); Assert.Empty(h.RuntimeCalls);
        Assert.Equal(view.Theme.Focus, sourceButton.Foreground);
        h.Content.Items.Remove(view); Assert.False(view.IsAttached);
        view.Dispose(); h.Refresh(); Assert.Equal(EditorSourceState.Changed, h.Watch.Read().State);
        using var recreated = h.CreateView(); Assert.NotSame(sourceButton, recreated.SourceButton);
        h.Content.Add(recreated);
        try
        {
            ui.Update(); Assert.Equal(h.Status.SourceSummary, recreated.SourceButton.Value);
            Assert.Equal(EditorSourceState.Changed, h.Model.Source!.State);
        }
        finally { h.Content.Items.Remove(recreated); }
    }

    [Fact]
    public void SourceDetailsPopupUsesRetainedLiveDataAndDoesNotPerformSourceIo()
    {
        using var h = new SourcePresentationFixture(); using var view = h.CreateView(); h.Content.Add(view);
        using var ui = h.Ui(); h.Bind();
        (SourcePresentationFixture.Wait(h.Control.Scanner.Next())).Complete(SourceWatchFixture.Same);
        h.Until(snapshot => snapshot.State == EditorSourceState.ObservedUnchanged); h.Refresh(); ui.Update();
        DocumentUiFixture.Click(ui, view.SourceButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(h.Overlays.Popup);
        Assert.Contains(DocumentUiFixture.Tree(popup).OfType<Text>(), text => text.Value == h.Status.SourceDetails);
        Assert.Equal(1, h.Control.Scanner.Calls); Assert.Empty(h.RuntimeCalls);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(popup, h.Overlays.Popup); Assert.Equal(1, h.Control.Scanner.Calls);
    }

    [Fact]
    public void CloseAndReopenSameManifestRejectOldOwnerAndDoNotRecoverBaselineFromOldSource()
    {
        using var h = new SourcePresentationFixture(); h.Bind(); var scan = SourcePresentationFixture.Wait(h.Control.Scanner.Next());
        var old = h.Active; var oldHints = h.Control.Hints.Latest;
        var closing = h.Projects.CloseAsync(DocumentCloseChoice.Discard);
        SourcePresentationFixture.Wait(SourceWatchFixture.Eventually(() => scan.Token.IsCancellationRequested));
        Assert.True(old.IsClosed); Assert.False(closing.IsCompleted); h.Refresh(); Assert.Null(h.Model.Source);
        scan.Complete(SourceWatchFixture.Same); SourcePresentationFixture.Wait(closing);
        SourcePresentationFixture.Wait(h.Projects.OpenAsync(h.Control.Files.Project.ManifestPath)); h.Refresh();
        Assert.NotEqual(old.OwnerKey, h.Active.OwnerKey); Assert.Equal(EditorSourceState.NoBuild, h.Model.Source!.State);
        Assert.Equal(h.Active.OwnerKey, h.Model.Source.OwnerKey); var current = h.Model.Source;
        oldHints.Signal(true); h.Refresh(); Assert.Same(current, h.Model.Source);
        SourcePresentationFixture.Wait(h.Projects.CloseAsync(DocumentCloseChoice.Discard)); h.Refresh();
        Assert.False(h.Model.HasProject); Assert.Null(h.Model.Source); Assert.False(h.Status.HasSource);
    }
}

internal sealed class SourcePresentationFixture : IDisposable
{
    internal SourceWatchFixture Control { get; } = new();
    internal EditorProjects Projects { get; }
    internal EditorDocumentCommands DocumentCommands { get; }
    internal EditorDocumentViewModel Documents { get; }
    internal EditorPreview Preview { get; }
    internal EditorTransformViewModel Transform { get; }
    internal EditorCommands Commands { get; }
    internal EditorScenePlayback Playback { get; }
    internal EditorWorkspace Workspace { get; } = new(EditorScene.Demo());
    internal EditorViewModel Model { get; }
    internal EditorStatusBarViewModel Status { get; }
    internal Panel Content { get; } = new();
    internal OverlayHost Overlays { get; }
    internal RichTextEditor Draft { get; } = new(new RichTextEditorOptions()) { Text = "unsaved text", Width = 280, Height = 70 };
    internal ConcurrentQueue<EditorCommand> RuntimeCalls { get; } = new();
    internal SessionStatus Runtime = new(SessionState.Stopped);
    internal ProjectSession Active => Projects.Active!;
    internal EditorSourceWatch Watch => Active.SourceWatch!;
    private readonly DocumentStore _store = new();
    private readonly DocumentData.Sessions _authoring = new();
    private bool _first = true;
    private GameEditorContext? _context;
    private long _version;
    internal SourcePresentationFixture()
    {
        Overlays = new(Content);
        Projects = new(new EditorProjectsTests.Store(Control.Files.Project), new EditorProjectsTests.Toolchain(), new EditorProjectsTests.Sessions
        {
            Activate = arguments =>
            {
                var watch = _first ? Control.Watch : new EditorSourceWatch(arguments.Project, new(), Control.Clock, Control.Scanner.Scan, Control.Hints.Create);
                _first = false; return new(arguments, new(arguments.Project, _store, _authoring), watch);
            }
        });
        Wait(Projects.OpenAsync(Control.Files.Project.ManifestPath)); Wait(Active.Documents!.NewAsync(DocumentData.Scene()));
        DocumentCommands = new(Projects, () => true, () => Task.CompletedTask);
        Documents = new(DocumentCommands); Documents.Refresh();
        Preview = new((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        Transform = new(DocumentCommands, Preview);
        Commands = new((command, _) => { RuntimeCalls.Enqueue(command); return Task.CompletedTask; }, default);
        Playback = new(DocumentCommands, Preview, () => Runtime,
            () => new(_version, _context, GameEditorOperation.Idle, null, null, false), context => { _context = context; _version++; },
            (command, _) =>
            {
                RuntimeCalls.Enqueue(command);
                if (command == EditorCommand.Play) Runtime = Runtime with { State = SessionState.Playing };
                if (command == EditorCommand.Stop) Runtime = new(SessionState.Stopped);
                return Task.CompletedTask;
            }, (_, _) => { RuntimeCalls.Enqueue(EditorCommand.Start); Runtime = new(SessionState.Paused) { Identity = new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64)) }; return Task.CompletedTask; });
        Model = new(Workspace, Commands, Documents, Transform, null, Playback); Refresh();
        Status = new(Model, Documents, Transform);
        Draft.HorizontalAlignment = LayoutAlignment.Start; Draft.VerticalAlignment = LayoutAlignment.Start; Content.Add(Draft);
    }
    internal void Refresh() { Workspace.SetProject(Projects.Active); Model.Refresh(Runtime, EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); }
    internal void StartPaused() { Assert.True(Playback.Execute(EditorCommand.Start)); Wait(Playback.Pending); Assert.Null(Playback.Error); }
    internal UiSession Ui() => UiSession.Create(Overlays, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(1280, 800));
    internal void Bind() => Watch.Bind(SourceWatchFixture.BuildA, SourceWatchFixture.Inputs, Control.Files.Descriptor());
    internal void Until(Func<EditorSourceSnapshot, bool> predicate) => Wait(SourceWatchFixture.Eventually(() => predicate(Watch.Read())));
    internal EditorStatusBarView CreateView() => new(Status, new EditorStatusBarViewStyle(),
        new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(args => new(Status, new EditorStatusPopupViewStyle(), args)), new(Overlays))
        { VerticalAlignment = LayoutAlignment.End };
    internal static void Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    internal static T Wait<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    public void Dispose()
    {
        if (Playback.CanExecute(EditorCommand.Stop)) { Playback.Execute(EditorCommand.Stop); Wait(Playback.Pending); }
        Overlays.Dispose(); Status.Dispose(); Model.Dispose(); Transform.Dispose(); Documents.Dispose(); Preview.Dispose();
        var closing = Projects.DisposeAsync().AsTask(); Control.Scanner.ReleaseAll(); Wait(closing);
        Wait(DocumentCommands.DisposeAsync().AsTask()); Wait(Control.DisposeAsync().AsTask()); _authoring.Dispose(); _store.Dispose();
    }
}
