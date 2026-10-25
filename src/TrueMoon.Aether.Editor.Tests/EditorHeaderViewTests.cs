using System.Collections.Concurrent;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorHeaderViewTests
{
    [Fact]
    public void BrandMark_PrecedesMenuAndKeepsCaptionRegionAcrossThemes()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        var brand = test.Header.BrandIcon; var bounds = brand.Bounds;
        Assert.Equal("TrueMoon", brand.AccessibleName); Assert.False(brand.Focusable);
        Assert.Equal(test.Header.Theme.Accent, brand.Foreground);
        Assert.Equal(28, bounds.Width); Assert.True(bounds.Height >= 20);
        Assert.True(bounds.X + bounds.Width <= test.Header.Menu.MoreButton.Bounds.X);
        var map = WindowRegionMap.Create(test.Frame);
        Assert.Equal(WindowRegionRole.Caption, map.HitTest(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        ui.HandleInput(new(InputKind.PointerMove, test.Header.Menu.MoreButton.Bounds.X + 3, bounds.Y + bounds.Height / 2));
        test.Clock.Advance(180); ui.Update();
        Assert.Same(brand, test.Header.BrandIcon); Assert.Equal(bounds, brand.Bounds);
        ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(brand, test.Header.BrandIcon); Assert.Equal(bounds, brand.Bounds);
        Assert.Equal(Theme.Light.Accent, brand.Foreground);
    }

    [Fact]
    public void DisclosureClick_OpensFileAfterRevealAndReturnsFocusOnClose()
    {
        using var test = new HeaderFixture(false); using var ui = test.Ui(); ui.Update();
        var button = test.Header.Menu.MoreButton; var bounds = button.Bounds;
        DocumentUiFixture.Click(ui, button); test.Clock.Advance(179); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(button.IsFocused);
        test.Clock.Advance(1); ui.Update();
        Assert.True(test.Header.Menu.IsMenuOpen); Assert.Equal(bounds, button.Bounds);
        Assert.True(test.Row("file.exit").IsFocused);
        Assert.Equal(0, test.CloseRequests); Assert.Empty(test.RuntimeCalls);
        DocumentUiFixture.Click(ui, button); test.Clock.Advance(180); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(button.IsFocused);
        Assert.False(test.Header.Menu.IsExpanded); Assert.Equal(0, test.Header.Menu.RevealProgress);
        Assert.Equal(0, test.CloseRequests); Assert.Empty(test.RuntimeCalls);
    }

    [Fact]
    public void NoProject_MenuRemainsKeyboardAccessibleWhileDocumentAndSimulationActionsAreDisabled()
    {
        using var test = new HeaderFixture(false); using var ui = test.Ui(); ui.Update();
        Assert.False(test.Header.RunButton.IsEnabled); Assert.False(test.Header.PauseButton.IsEnabled); Assert.False(test.Header.StopButton.IsEnabled);
        Assert.All(test.Header.RuntimeCommands.Values, command => Assert.False(command.CanExecute));
        Assert.All(test.Header.DocumentCommands.Values, command => Assert.False(command.CanExecute));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); ui.Update();
        Assert.True(test.Header.Menu.IsExpanded); Assert.True(test.Header.Menu.Headings["file"].IsFocused);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down)); ui.Update();
        var popup = Assert.IsAssignableFrom<Element>(test.Overlays.Popup);
        Assert.Equal("file.exit", Assert.Single(DocumentUiFixture.Tree(popup).OfType<MenuItem>(), row => row.IsFocused).ActionId);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter)); ui.Update();
        Assert.Equal(1, test.CloseRequests); Assert.Null(test.Overlays.Popup); Assert.True(test.Header.Menu.MoreButton.IsFocused);
        Assert.Empty(test.RuntimeCalls);
    }

    [Fact]
    public void Reveal_ShiftsCaptionButKeepsSimulationAndNativeWindowButtonsFixed()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        var title = test.Header.Caption.Bounds; var run = test.Header.RunButton.Bounds;
        var pause = test.Header.PauseButton.Bounds; var stop = test.Header.StopButton.Bounds;
        var native = test.Frame.DefaultTitleBar!.Children.Take(3).Select(element => element.Bounds).ToArray();
        var more = test.Header.Menu.MoreButton.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, more.X + 4, more.Y + 4));
        test.Clock.Advance(90); ui.Update();
        Assert.InRange(test.Header.Menu.RevealProgress, 0.49f, 0.51f);
        Assert.True(test.Header.Caption.Bounds.X > title.X); Assert.True(test.Header.Caption.Bounds.Width < title.Width);
        Assert.Equal(run, test.Header.RunButton.Bounds); Assert.Equal(pause, test.Header.PauseButton.Bounds); Assert.Equal(stop, test.Header.StopButton.Bounds);
        test.Clock.Advance(90); ui.Update();
        Assert.Equal(1, test.Header.Menu.RevealProgress);
        Assert.Equal(native, test.Frame.DefaultTitleBar.Children.Take(3).Select(element => element.Bounds));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); test.Clock.Advance(180); ui.Update();
        Assert.Equal(0, test.Header.Menu.RevealProgress); Assert.Equal(title, test.Header.Caption.Bounds); Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(420, 260)]
    [InlineData(90, 60)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    public void NarrowWindows_KeepHeaderGeometryFiniteAndSimulationButtonsInsideHeader(int width, int height)
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(width, height); ui.Update();
        if (width == 0 || height == 0)
        {
            Assert.True(ui.Viewport.IsEmpty); Assert.Equal(default, test.Header.Bounds);
            Assert.All(new[] { test.Header.RunButton, test.Header.PauseButton, test.Header.StopButton }, button => Assert.Equal(default, button.Bounds));
            return;
        }
        var header = test.Header.Bounds;
        foreach (var button in new[] { test.Header.RunButton, test.Header.PauseButton, test.Header.StopButton })
        {
            Assert.True(float.IsFinite(button.Bounds.X + button.Bounds.Y + button.Bounds.Width + button.Bounds.Height));
            Assert.True(button.Bounds.Width >= 0); Assert.True(button.Bounds.Height >= 0);
            Assert.InRange(button.Bounds.X, header.X, header.X + header.Width);
            Assert.InRange(button.Bounds.X + button.Bounds.Width, header.X, header.X + header.Width);
        }
        Assert.True(test.Header.Caption.Bounds.Width >= 0);
        Assert.True(test.Header.Caption.Bounds.Height >= 0);
    }

    [Fact]
    public void MenuAndIconCommands_ShareIdentityAndRefreshWithAcknowledgedRuntimeState()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        var simulation = test.Header.Menu.Groups.Single(group => group.Id == "simulation");
        Assert.Same(test.Header.RunCommand, simulation.Actions.Single(action => action.Id == "simulation.run").Command);
        Assert.Same(test.Header.RuntimeCommands[EditorCommand.Pause], simulation.Actions.Single(action => action.Id == "runtime.Pause").Command);
        Assert.Same(test.Header.RuntimeCommands[EditorCommand.Stop], simulation.Actions.Single(action => action.Id == "runtime.Stop").Command);
        Assert.False(test.Header.RunButton.IsEnabled);
        test.Refresh(SessionState.Stopped, candidate: true); ui.Update();
        Assert.True(test.Header.RunButton.IsEnabled); Assert.False(test.Header.PauseButton.IsEnabled); Assert.False(test.Header.StopButton.IsEnabled);
        test.Header.Menu.OpenGroup("simulation", ui); ui.Update();
        var menuRun = test.Row("simulation.run"); Assert.Same(test.Header.RunCommand, menuRun.Command); Assert.True(menuRun.IsEnabled);
        test.Refresh(SessionState.Playing, candidate: true); ui.Update();
        Assert.False(test.Header.RunButton.IsEnabled); Assert.False(menuRun.IsEnabled);
        Assert.True(test.Header.PauseButton.IsEnabled); Assert.True(test.Header.StopButton.IsEnabled);
        Assert.True(test.Row("runtime.Pause").IsEnabled); Assert.True(test.Row("runtime.Stop").IsEnabled);
        test.Refresh(SessionState.Paused, candidate: true); ui.Update();
        Assert.True(test.Header.RunButton.IsEnabled); Assert.False(test.Header.PauseButton.IsEnabled); Assert.True(test.Row("runtime.Step").IsEnabled);
    }

    [Fact]
    public void RuntimeIcons_StartAndPlayAsOneGestureThenPauseResumeAndStopWithoutBuilding()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        test.Refresh(SessionState.Stopped, candidate: true); ui.Update();
        DocumentUiFixture.Click(ui, test.Header.RunButton); DocumentUiFixture.Wait(test.Commands.Pending);
        Assert.Equal(new[] { EditorCommand.Start, EditorCommand.Play }, test.RuntimeCalls.ToArray());
        test.Refresh(SessionState.Playing, candidate: true); ui.Update();
        DocumentUiFixture.Click(ui, test.Header.PauseButton); DocumentUiFixture.Wait(test.Commands.Pending);
        test.Refresh(SessionState.Paused, candidate: true); ui.Update();
        DocumentUiFixture.Click(ui, test.Header.RunButton); DocumentUiFixture.Wait(test.Commands.Pending);
        test.Refresh(SessionState.Playing, candidate: true); ui.Update();
        DocumentUiFixture.Click(ui, test.Header.StopButton); DocumentUiFixture.Wait(test.Commands.Pending);
        Assert.Equal(new[] { EditorCommand.Start, EditorCommand.Play, EditorCommand.Pause, EditorCommand.Play, EditorCommand.Stop }, test.RuntimeCalls.ToArray());
        Assert.Null(test.Commands.Error);
    }

    [Fact]
    public void OpenAuthoringDocument_BlocksRuntimeAndFileMenuUsesCurrentPathDraft()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        test.Documents.PathDraft = "room.aetherscene"; test.Refresh(SessionState.Stopped, candidate: true); ui.Update();
        var file = test.Header.Menu.Groups.Single(group => group.Id == "file");
        Assert.Same(test.Header.DocumentCommands[DocumentCommand.Open], file.Actions.Single(action => action.Id == "document.Open").Command);
        test.Header.Menu.OpenGroup("file", ui); ui.Update(); DocumentUiFixture.Click(ui, test.Row("document.Open"));
        Assert.Equal(DocumentCommand.Open, test.Documents.PathRequest); Assert.Empty(test.Owner.Store.Reads);
        test.Documents.ConfirmPath();
        test.Owner.Complete(); test.Refresh(SessionState.Stopped, candidate: true); ui.Update();
        Assert.Equal(test.Owner.Files.ScenePath, Assert.Single(test.Owner.Store.Reads));
        Assert.NotNull(test.Documents.Document!.Content); Assert.False(test.Header.RunButton.IsEnabled);
        test.Header.RunCommand.Execute(); test.Header.RuntimeCommands[EditorCommand.Start].Execute();
        Assert.Empty(test.RuntimeCalls); Assert.Equal("room.aetherscene", test.Documents.PathDraft);
    }

    [Fact]
    public void EditMenu_UsesTheSharedDocumentUndoRedoHistory()
    {
        using var test = new HeaderFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        test.Transform.Fields[0].Value = "42"; test.Transform.Apply(); ui.Update();
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!));
        test.Header.Menu.OpenGroup("edit", ui); ui.Update();
        Assert.True(test.Row("edit.undo").IsEnabled); Assert.False(test.Row("edit.redo").IsEnabled);
        DocumentUiFixture.Click(ui, test.Row("edit.undo")); ui.Update();
        Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.True(test.Transform.CanRedo);
        test.Header.Menu.OpenGroup("edit", ui); ui.Update(); DocumentUiFixture.Click(ui, test.Row("edit.redo")); ui.Update();
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        Assert.Empty(test.RuntimeCalls);
    }

    [Fact]
    public void HeaderFocusAfterMenu_HistoryShortcutsChangeTheSharedHistoryOncePerKeyGesture()
    {
        using var test = new HeaderFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        test.Transform.Fields[0].Value = "42"; test.Transform.Apply(); ui.Update();
        test.Header.Menu.OpenGroup("view", ui); ui.Update(); DocumentUiFixture.Click(ui, test.Row("view.theme")); ui.Update();
        Assert.True(test.Header.Menu.MoreButton.IsFocused); Assert.Null(test.Overlays.Popup);
        Assert.Equal(8, test.Owner.Document.Read().Content!.Document.Revision);
        Shortcut(ui, UiKey.Z); ui.Update();
        Assert.Equal(4, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(9, test.Owner.Document.Read().Content!.Document.Revision);
        Assert.Equal(0, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(1, test.Owner.Document.Read().Content!.RedoCount);
        Shortcut(ui, UiKey.Y); ui.Update();
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(10, test.Owner.Document.Read().Content!.Document.Revision);
        Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount); Assert.Equal(0, test.Owner.Document.Read().Content!.RedoCount);
        Shortcut(ui, UiKey.Z); Shortcut(ui, UiKey.Z, shift: true); ui.Update();
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(12, test.Owner.Document.Read().Content!.Document.Revision); Assert.Empty(test.RuntimeCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderFocusAfterMenu_FileShortcutsSaveOrSaveAsOnceAndCloseTheDocument(bool saveAs)
    {
        using var test = new HeaderFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        test.Transform.Fields[0].Value = "42"; test.Transform.Apply(); ui.Update();
        test.Documents.PathDraft = "copy.aetherscene";
        test.Header.Menu.OpenGroup("view", ui); ui.Update(); DocumentUiFixture.Click(ui, test.Row("view.theme")); ui.Update();
        Assert.True(test.Header.Menu.MoreButton.IsFocused);
        Shortcut(ui, UiKey.S, saveAs);
        if (saveAs) { Assert.Equal(DocumentCommand.SaveAs, test.Documents.PathRequest); Assert.Empty(test.Owner.Store.Writes); test.Documents.ConfirmPath(); }
        test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        var saved = Assert.Single(test.Owner.Store.Writes);
        Assert.Equal(saveAs ? Path.Combine(test.Owner.Files.Project.ScenesDirectory, "copy.aetherscene") : test.Owner.Files.ScenePath, saved.Path);
        Assert.Equal(8, saved.Document.Revision); Assert.False(test.Owner.Document.Read().IsDirty);
        if (saveAs) Assert.Null(saved.Expected); else Assert.Equal(new SceneFileStamp("observed-file"), saved.Expected);
        Shortcut(ui, UiKey.W); test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        Assert.Null(test.Owner.Document.Read().Content); Assert.Single(test.Owner.Store.Writes);
        Assert.False(test.Documents.AwaitingDecision); Assert.Equal("copy.aetherscene", test.Documents.PathDraft);
        Assert.Equal(0, test.CloseRequests); Assert.Empty(test.RuntimeCalls);
    }

    [Fact]
    public void HeaderFocus_DecisionEscapeCancelsAndSaveUsesThePromptPathWithoutReplacingThePendingIntent()
    {
        using var test = new HeaderFixture(); test.Owner.Run(DocumentCommand.New); test.Documents.Refresh();
        test.Documents.PathDraft = "ordinary-draft.aetherscene"; test.Documents.SavePathDraft = "prompt-save.aetherscene";
        using var ui = test.Ui(); ui.Update(); ui.Focus(test.Header.Menu.MoreButton);
        var original = test.Owner.Document.Read().Content!.Document;
        Shortcut(ui, UiKey.W); test.Owner.Decision(); test.Documents.Refresh(); ui.Update();
        var pending = test.Owner.Commands.Pending;
        Shortcut(ui, UiKey.W);
        Assert.Same(pending, test.Owner.Commands.Pending); Assert.Equal(DocumentCommand.Close, test.Owner.Commands.Read().Operation);
        Assert.True(test.Documents.AwaitingDecision); Assert.Empty(test.Owner.Store.Writes);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Escape));
        test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        Assert.False(test.Documents.AwaitingDecision); Assert.Same(original, test.Owner.Document.Read().Content!.Document);
        Assert.Empty(test.Owner.Store.Writes);
        Shortcut(ui, UiKey.W); test.Owner.Decision(); test.Documents.Refresh(); ui.Update();
        Shortcut(ui, UiKey.S, shift: true); test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        var saved = Assert.Single(test.Owner.Store.Writes);
        Assert.Same(original, saved.Document); Assert.EndsWith("prompt-save.aetherscene", saved.Path);
        Assert.Null(test.Owner.Document.Read().Content); Assert.False(test.Documents.AwaitingDecision);
        Assert.Equal("ordinary-draft.aetherscene", test.Documents.PathDraft); Assert.Empty(test.RuntimeCalls);
    }

    [Fact]
    public void HeaderFocus_CommitAndEscapeUseTheExistingTransformDraftWithoutDuplicatingHistory()
    {
        using var test = new HeaderFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        ui.Focus(test.Header.Menu.MoreButton); test.Transform.Fields[0].Value = "61";
        Shortcut(ui, UiKey.Enter); ui.Update();
        Assert.Equal(61, DocumentData.X(test.Owner.Document.Read().Content!)); Assert.False(test.Transform.HasDraft);
        Assert.Equal(8, test.Owner.Document.Read().Content!.Document.Revision); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
        test.Transform.Fields[0].Value = "62";
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Escape)); ui.Update();
        Assert.Equal("61", test.Transform.Fields[0].Value); Assert.False(test.Transform.HasDraft);
        Assert.Equal(61, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(8, test.Owner.Document.Read().Content!.Document.Revision); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    [Fact]
    public void BodyTextInput_HistoryKeysDoNotInvokeTheHeaderHistoryShortcut()
    {
        using var test = new HeaderFixture(openDocument: true); var input = new TextBox();
        ((Panel)test.Overlays.Content).Add(input); using var ui = test.Ui(); ui.Update();
        test.Transform.Fields[0].Value = "42"; test.Transform.Apply(); ui.Update();
        ui.Focus(input); ui.HandleInput(new(InputKind.Text, Text: "local draft")); Assert.Equal("local draft", input.Value);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); var selection = input.SelectionLength;
        Shortcut(ui, UiKey.Z); Shortcut(ui, UiKey.Y); ui.Update();
        Assert.Equal("local draft", input.Value); Assert.Equal(selection, input.SelectionLength); Assert.True(input.IsFocused);
        Assert.Equal(42, DocumentData.X(test.Owner.Document.Read().Content!));
        Assert.Equal(8, test.Owner.Document.Read().Content!.Document.Revision); Assert.Equal(1, test.Owner.Document.Read().Content!.UndoCount);
    }

    private static void Shortcut(UiSession ui, UiKey key, bool shift = false)
    { ui.HandleInput(new(InputKind.KeyDown, Key: key, Control: true, Shift: shift)); ui.HandleInput(new(InputKind.KeyUp, Key: key, Control: true, Shift: shift)); }

    [Fact]
    public void ThemeAndStyleReplacement_PreserveRetainedHeaderAndInputFocus()
    {
        using var test = new HeaderFixture(); using var ui = test.Ui(); ui.Update();
        var tree = DocumentUiFixture.Tree(test.Header).ToArray(); ui.Focus(test.Header.Menu.MoreButton);
        var font = test.Header.Theme.FontFamily;
        test.Header.Menu.Groups.Single(group => group.Id == "view").Actions.Single().Command.Execute();
        test.Header.WithViewStyle(new EditorHeaderViewStyle()); ui.Update();
        Assert.Equal(Theme.Light.Surface, test.Header.Theme.Surface); Assert.Equal(font, test.Header.Theme.FontFamily);
        Assert.Equal(tree, DocumentUiFixture.Tree(test.Header)); Assert.True(test.Header.Menu.MoreButton.IsFocused);
        Assert.Equal(ControlTone.Positive, test.Header.RunButton.Tone); Assert.Equal(ControlTone.Danger, test.Header.StopButton.Tone);
        Assert.Equal(WindowRegionRole.Caption, test.Header.WindowRegion); Assert.Equal(WindowRegionRole.Client, test.Header.Menu.WindowRegion);
        Assert.False(ui.Update());
    }

    [Fact]
    public void DisabledIconTooltip_AppearsOnlyAfterDelayWithoutStealingFocusAndDismissesOnInput()
    {
        using var test = new HeaderFixture(false); using var ui = test.Ui(); ui.Update();
        ui.Focus(test.Header.Menu.MoreButton); var button = test.Header.RunButton.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, button.X + 3, button.Y + 3)); test.Clock.Advance(499); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(test.Header.NeedsFrame); Assert.True(test.Header.Menu.MoreButton.IsFocused);
        test.Clock.Advance(1); ui.Update();
        var popup = Assert.IsAssignableFrom<Element>(test.Overlays.Popup);
        Assert.Contains(DocumentUiFixture.Tree(popup).OfType<Text>(), text => text.Value.Contains("сейчас недоступно", StringComparison.Ordinal));
        Assert.Same(test.Header, test.Overlays.PopupOwner); Assert.True(test.Header.Menu.MoreButton.IsFocused); Assert.False(test.Header.NeedsFrame);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(popup.IsDisposed); Assert.True(test.Header.Menu.MoreButton.IsFocused);
    }

    [Fact]
    public void MenuOpening_CancelsPendingIconTooltipAndDoesNotReplaceTheMenuPopup()
    {
        using var test = new HeaderFixture(false); using var ui = test.Ui(); ui.Update();
        var button = test.Header.RunButton.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, button.X + 3, button.Y + 3));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.F10)); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down)); ui.Update();
        var popup = test.Overlays.Popup; test.Clock.Advance(600); ui.Update();
        Assert.NotNull(popup); Assert.Same(popup, test.Overlays.Popup); Assert.Same(test.Header.Menu, test.Overlays.PopupOwner);
        Assert.False(test.Header.NeedsFrame);
    }

    [Fact]
    public void ViewDisposal_ReleasesSubscriptionsAndControlsWhileBorrowedModelsRemainUsable()
    {
        using var test = new HeaderFixture(); var ui = test.Ui(); ui.Update();
        var changes = 0; test.Header.RunCommand.CanExecuteChanged += () => changes++;
        var run = test.Header.RunButton; ui.Dispose(); Assert.True(test.Header.IsDisposed); Assert.True(run.IsDisposed);
        test.Refresh(SessionState.Stopped, candidate: true);
        Assert.True(test.Model.CanRun); Assert.False(run.IsEnabled); Assert.Equal(0, changes);
        test.Documents.PathDraft = "still usable.aetherscene";
        Assert.Equal("still usable.aetherscene", test.Documents.PathDraft); Assert.False(test.Owner.Commands.Read().Closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StyleFailure_DisposesUnpublishedHeaderAndPreservesBorrowedModelsAndErrors(bool cleanupFailure)
    {
        using var owner = new DocumentUiFixture(); using var documents = new EditorDocumentViewModel(owner.Commands);
        using var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        using var transform = new EditorTransformViewModel(owner.Commands, preview);
        using var model = new EditorViewModel(new(EditorScene.Demo()), new((_, _) => Task.CompletedTask, CancellationToken.None));
        using var overlays = new OverlayHost(new Panel());
        var failure = new IOException("header style failed"); var cleanup = new IOException("header cleanup failed");
        EditorHeaderView unpublished = null!; var resource = new Resource(cleanupFailure ? cleanup : null);
        var error = Record.Exception(() => new EditorHeaderView(model, documents, transform, new FailingStyle(view => { unpublished = view; view.Own(resource); }, failure), new(overlays, "Editor", () => { })));
        if (cleanupFailure) Assert.Collection(Assert.IsType<AggregateException>(error).Flatten().InnerExceptions, value => Assert.Same(failure, value), value => Assert.Same(cleanup, value));
        else Assert.Same(failure, error);
        Assert.True(unpublished.IsDisposed); Assert.All(DocumentUiFixture.Tree(unpublished), element => Assert.True(element.IsDisposed));
        Assert.Equal(1, resource.Disposals); Assert.False(overlays.IsDisposed);
        model.Select("green-cube"); documents.PathDraft = "next.aetherscene"; transform.Refresh(null);
        Assert.Equal("green-cube", model.SelectedId); Assert.False(owner.Commands.Read().Closed);
    }

    private sealed class FailingStyle(Action<EditorHeaderView> capture, Exception failure) : IViewStyle<EditorHeaderView>
    { public void Configure(IViewStyleBuilder<EditorHeaderView> styles) { styles.Part(view => { capture(view); return view.Caption; }).FontSize(14); throw failure; } }
    private sealed class Resource(Exception? failure) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; if (failure != null) throw failure; } }
}

internal sealed class HeaderFixture : IDisposable
{
    public DocumentUiFixture Owner { get; }
    public EditorDocumentViewModel Documents { get; }
    public EditorTransformViewModel Transform { get; }
    public EditorPreview Preview { get; }
    public EditorCommands Commands { get; }
    public EditorViewModel Model { get; }
    public EditorHeaderView Header { get; }
    public OverlayHost Overlays { get; } = new(new Panel());
    public WindowFrame Frame { get; }
    public HeaderClock Clock { get; } = new();
    public ConcurrentQueue<EditorCommand> RuntimeCalls { get; } = new();
    public int CloseRequests { get; private set; }
    public HeaderFixture(bool hasProject = true, bool openDocument = false)
    {
        Owner = new(hasProject); if (openDocument) Owner.Run(DocumentCommand.Open, "room.aetherscene");
        Documents = new(Owner.Commands); Documents.Refresh();
        Preview = new((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        Transform = new(Owner.Commands, Preview); Transform.Refresh(openDocument ? DocumentData.Child.Value.ToString() : null);
        Commands = new((command, _) => { RuntimeCalls.Enqueue(command); return Task.CompletedTask; }, CancellationToken.None);
        Model = new(new(EditorScene.Demo(), hasProject), Commands, Documents, Transform);
        if (openDocument)
        {
            Model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false));
            Model.Select(DocumentData.Child.Value.ToString());
        }
        Header = new(Model, Documents, Transform, new EditorHeaderViewStyle(), new(Overlays, "TrueMoon Aether Editor · room Мир 🛰️", () => CloseRequests++));
        Frame = WindowFrame.CreateDefault("Editor", Overlays); Frame.DefaultTitleBar!.SetHeader(Header);
    }
    public void Refresh(SessionState state, bool candidate = false) => Model.Refresh(new(state), EditorWorkspaceTests.Status(GameEditorOperation.Idle, candidate));
    public MenuItem Row(string id) => DocumentUiFixture.Tree(Overlays.Popup!).OfType<MenuItem>().Single(row => row.ActionId == id);
    public UiSession Ui(int width = 1280, int height = 800) => new(Frame, new HeaderSurface(), new(width, height), null, new UiDispatchOptions(), Clock);
    public void Dispose() { Frame.Dispose(); Model.Dispose(); Transform.Dispose(); Documents.Dispose(); Preview.Dispose(); Owner.Dispose(); }
    private sealed class HeaderSurface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new HeaderTextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
    private sealed class HeaderTextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}

internal sealed class HeaderClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => _ticks;
    public void Advance(long milliseconds) => _ticks += milliseconds;
}
