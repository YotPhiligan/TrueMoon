using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

// Directed input stays inside this app's tree; fixtures live in a disposable external project.
internal static class EditorDocumentSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorDocumentCommands commands, SceneDocumentSession document,
        ISceneDocumentStore store, AetherHost engine, NativeRuntimeFactory factory)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = deadline.Token;
        var panel = view.Documents ?? throw new InvalidOperationException("Document view is missing.");
        var model = (EditorDocumentViewModel)panel.DataContext!;
        var root = commands.Read().ScenesDirectory!;
        var source = Path.Combine(root, "Rich.aetherscene");
        var originalHash = Hash(source);
        var captures = new List<string>(); var actions = new List<string>(); var threads = new HashSet<int>();
        var pathCaptured = false;
        var closeRequests = 0;
        var presentations = new List<object>();
        var windowId = window.Id; var owner = 0;
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Observe; }).WaitAsync(token);
        try
        {
            await PathDraft("Rich.aetherscene"); await Click(DocumentCommand.Open);
            var scene = document.Read().Content!.Document; var ids = workspace.EntityIds.ToArray();
            Require(ids.Length == 2 && document.Read().IsDirty == false, "Scene was not loaded into the actual workspace.");
            await Until(() => (view.Hierarchy?.Tree.Items.Count ?? view.Entities.Count) == 2);
            await EditorHierarchySmoke.ClickItemAsync(ui, view, ids[1], token);
            Require(workspace.SelectedId == ids[1] && workspace.Properties().Any(x => x.Key == "count" && x.Value == "0"), "Raw authoring selection/properties were not presented.");
            Require(view.Commands.All(x => !x.Value.IsEnabled), "Document context must not run a stale game snapshot.");
            await Capture("documents-open-dark.png", 2);
            var pathControl = panel.PathInput; var canvas = view.Canvas; var firstEntity = (Element?)view.Hierarchy?.Tree ?? view.Entities[ids[0]];
            await PathDraft("Copied.aetherscene"); await Click(DocumentCommand.SaveAs);
            Require(File.Exists(Path.Combine(root, "Copied.aetherscene")) && Hash(source) == originalHash, "SaveAs damaged the source or did not publish.");
            File.AppendAllText(Path.Combine(root, "Copied.aetherscene"), " ");
            var changedHash = Hash(Path.Combine(root, "Copied.aetherscene"));
            await Click(DocumentCommand.Save, failure: true);
            Require(commands.Read().Error is SceneFileConflictException && Hash(Path.Combine(root, "Copied.aetherscene")) == changedHash, "External file change was silently overwritten.");
            await PathDraft("Recovered.aetherscene"); await Click(DocumentCommand.SaveAs);
            Require(commands.Read().Error == null && !document.Read().IsDirty, "SaveAs did not recover from conflict.");
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" })).WaitAsync(token);
            await Until(() => panel.Theme.Surface == Theme.Light.Surface);
            Require(ReferenceEquals(panel.PathInput, pathControl) && ReferenceEquals(canvas, view.Canvas) &&
                ReferenceEquals(firstEntity, (Element?)view.Hierarchy?.Tree ?? view.Entities[ids[0]]) && model.PathDraft == "Recovered.aetherscene", "Save/theme rebuilt the tree or lost path draft.");
            await Click(DocumentCommand.Close);
            await Until(() => (view.Hierarchy?.Tree.Items.Count ?? view.Entities.Count) == 0);
            await PathDraft("Recovered.aetherscene"); await Click(DocumentCommand.Open);
            Require(document.Read().Content!.Document.Id == scene.Id && workspace.EntityIds.SequenceEqual(ids), "Reopen changed stable scene/entity identity.");

            await Click(DocumentCommand.New);
            var firstNew = document.Read().Content!.Document.Id;
            await Begin(DocumentCommand.Close); await Decision();
            await Capture("documents-unsaved-light.png", 0);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape))).WaitAsync(token);
            await commands.Pending.WaitAsync(token);
            Require(document.Read().IsDirty && document.Read().Content!.Document.Id == firstNew, "Cancel lost unsaved document.");
            await Until(() => panel.PathInput.IsEnabled);
            await PathDraft("../Escape.aetherscene"); await Shortcut(UiKey.S);
            Require(commands.Read().Error is ArgumentException && document.Read().Path == null && document.Read().IsDirty, "Invalid first Save lost untitled content.");
            await Capture("documents-save-error.png", 0);
            await PathDraft("New.aetherscene"); await Shortcut(UiKey.S);
            Require(document.Read().Path == Path.Combine(root, "New.aetherscene") && !document.Read().IsDirty, "Ctrl+S did not use the current path draft for first Save.");

            await Click(DocumentCommand.New);
            var unsaved = document.Read().Content!.Document.Id;
            await PathDraft("Unsupported.aetherscene"); await Begin(DocumentCommand.Open); await Decision();
            await ui.InvokeAsync(() => InputClick(panel.DiscardChoice)).WaitAsync(token);
            await commands.Pending.WaitAsync(token);
            Require(commands.Read().Error is InvalidDataException && document.Read().IsDirty &&
                document.Read().Content!.Document.Id == unsaved, "Invalid Open discarded the previous document.");
            await PathDraft("Recovered.aetherscene"); await Begin(DocumentCommand.Open); await Decision();
            await ui.InvokeAsync(() => { SetText(panel.SavePathInput, "BeforeOpen.aetherscene"); InputClick(panel.SaveChoice); }).WaitAsync(token);
            await commands.Pending.WaitAsync(token);
            Require(commands.Read().Error == null && File.Exists(Path.Combine(root, "BeforeOpen.aetherscene")) &&
                document.Read().Content!.Document.Id == scene.Id, "Save-before-Open did not persist old content before selecting the candidate.");
            await Click(DocumentCommand.New);
            var discarded = document.Read().Content!.Document.Id;
            await PathDraft("Recovered.aetherscene"); await Begin(DocumentCommand.Open); await Decision();
            await ui.InvokeAsync(() => InputClick(panel.DiscardChoice)).WaitAsync(token); await commands.Pending.WaitAsync(token);
            Require(document.Read().Content!.Document.Id == scene.Id && discarded != scene.Id, "Discard-before-Open did not select the requested candidate.");
            window.Resize(1100, 700);
            await Until(() => ui.Viewport.Width / ui.Viewport.Scale == 1100);
            await Capture("documents-open-resized.png", 2);

            await Click(DocumentCommand.New); var exiting = document.Read().Content!.Document.Id;
            await NativeClose(); await Decision();
            await ui.InvokeAsync(() => { SetText(panel.SavePathInput, "../Exit.aetherscene"); InputClick(panel.SaveChoice); }).WaitAsync(token);
            await Until(() => !commands.Read().Busy);
            Require(!window.Completion.IsCompleted && document.Read().IsDirty && document.Read().Content!.Document.Id == exiting &&
                commands.Read().Error is ArgumentException, "Failed Exit Save closed the window or lost the document.");
            await NativeClose(); await Decision();
            await ui.InvokeAsync(() => InputClick(panel.CancelChoice)).WaitAsync(token); await Until(() => !commands.Read().Busy);
            Require(!window.Completion.IsCompleted && document.Read().Content!.Document.Id == exiting, "Exit Cancel did not veto close.");
            await NativeClose(); await Decision();
            await ui.InvokeAsync(() => { SetText(panel.SavePathInput, "Exit.aetherscene"); InputClick(panel.SaveChoice); }).WaitAsync(token);
            await window.Completion.WaitAsync(token);
            var reopened = await store.ReadAsync(Path.Combine(root, "Exit.aetherscene"), token);
            Require(reopened.Document.Id == exiting && document.Read().Content == null && document.Read().IsDirty == false,
                "Final Exit did not save and release the exact document.");
            Require(window.Id == windowId && engine.Supervisor.Status.State == SessionState.Stopped &&
                factory.Leases.Count == 0 && engine.Builds.ProcessId == null && Hash(source) == originalHash, "Document UI started compiler/game or mutated the fixture.");
            Require(threads.SetEquals([owner]), "Document notifications escaped the UI owner thread.");
            File.WriteAllText(Path.Combine(directory, "documents.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Commands = actions, Captures = captures, WindowId = windowId, OwnerThread = owner,
                NotificationThreads = threads.ToArray(), Presentations = presentations, OriginalScene = scene.Id.Value, ExitScene = exiting.Value,
                Children = factory.Leases.Count, CompilerStarted = engine.Builds.ProcessId != null,
                SavedFiles = new[] { "Copied.aetherscene", "Recovered.aetherscene", "New.aetherscene", "BeforeOpen.aetherscene", "Exit.aetherscene" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { model.PropertyChanged -= Observe; }

        void Observe(object? sender, System.ComponentModel.PropertyChangedEventArgs change) => threads.Add(Environment.CurrentManagedThreadId);
        async Task PathDraft(string value)
        {
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document.Open", token, confirmPath: false);
            await Until(() => panel.IsVisible && panel.Actions.IsVisible && panel.PathInput.IsEnabled && !ui.NeedsUpdate);
            await ui.InvokeAsync(() => SetText(panel.PathInput, value)).WaitAsync(token);
            if (!pathCaptured) { await Capture("documents-path-prompt.png", 0); pathCaptured = true; }
            await ui.InvokeAsync(() => InputClick(panel.CancelPath)).WaitAsync(token);
            await Until(() => !panel.IsVisible && !ui.NeedsUpdate);
        }
        void SetText(TextBox target, string value)
        {
            InputClick(target); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new(InputKind.Text, Text: value));
            Require(target.Value == value, "Directed text input was not delivered.");
        }
        async Task Begin(DocumentCommand command)
        {
            await Until(() => panel.Commands[command].IsEnabled);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document." + command, token); actions.Add(command.ToString());
        }
        async Task Click(DocumentCommand command, bool failure = false)
        {
            await Begin(command); await commands.Pending.WaitAsync(token);
            if (!failure) Require(commands.Read().Error == null, "Document UI action failed: " + commands.Read().Error);
            await Until(() => !commands.Read().Busy && panel.Commands[DocumentCommand.Open].IsEnabled &&
                ReferenceEquals(model.Document?.Content?.Document, document.Read().Content?.Document));
        }
        async Task Decision() => await Until(() => commands.Read().AwaitingDecision && panel.Decision.IsVisible && panel.CancelChoice.IsFocused);
        async Task NativeClose()
        {
            if (closeRequests++ == 0)
            {
                await ui.InvokeAsync(() => InputClick(((WindowFrame)ui.Root).DefaultTitleBar!.CloseButton)).WaitAsync(token); actions.Add("NativeClose");
            }
            else
            {
                await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "file.exit", token); actions.Add("MenuExit");
            }
        }
        async Task Shortcut(UiKey key)
        {
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: key, Control: true))).WaitAsync(token);
            await EditorHeaderSmoke.ConfirmDocumentPathAsync(ui, view, token);
            actions.Add("Ctrl+" + key); await commands.Pending.WaitAsync(token); await Until(() => !commands.Read().Busy);
        }
        async Task Until(Func<bool> condition)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = condition()).WaitAsync(token);
                if (ready) return; await Task.Delay(10, token);
            }
        }
        void InputClick(Element element)
        {
            Require(element.IsEffectivelyEnabled && element.IsVisible, "Cannot click an unavailable document control.");
            var bounds = element.Bounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
            ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y));
        }
        async Task Capture(string name, int expectedEntities)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate) return;
                var shell = (EditorViewModel)view.DataContext!;
                var status = view.StatusBar?.DataContext as EditorStatusBarViewModel;
                var count = $"{expectedEntities} объектов";
                if (workspace.EntityCount != expectedEntities) return;
                if (status != null)
                {
                    if (status.Summary != view.Status.Value || status.EntityText != count ||
                        ((Text)view.StatusBar!.Entities).Value != count) return;
                }
                else if (shell.StatusText != view.Status.Value ||
                    !view.Status.Value.EndsWith(" " + count, StringComparison.Ordinal)) return;
                window.FramePresented -= Presented;
                try
                {
                    Require(ReferenceEquals(view.Documents, panel) && !view.IsDisposed, "Document UI recreated the shell.");
                    using var image = current.ReadbackOpenGLImage(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name); completion.TrySetResult();
                    presentations.Add(new { Name = name, workspace.EntityCount, shell.StatusText,
                        BoundStatus = view.Status.Value, BoundEntityCount = status?.EntityText });
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await completion.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
