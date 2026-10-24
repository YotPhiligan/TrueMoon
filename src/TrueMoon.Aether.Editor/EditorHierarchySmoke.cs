using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

// Directed input in this owned UI tree and disposable fixture; no global input or application settings.
internal static class EditorHierarchySmoke
{
    internal static async Task ClickItemAsync(UiSession ui, EditorView view, string id, CancellationToken token)
    {
        await ui.InvokeAsync(() =>
        {
            if (view.Hierarchy is not { } hierarchy) return;
            var items = hierarchy.Tree.Items.ToDictionary(item => item.Id);
            for (var parent = items[id].ParentId; parent != null; parent = items[parent].ParentId)
                hierarchy.Tree.SetExpanded(parent, true);
        }).WaitAsync(token);
        await EditorHeaderSmoke.UntilAsync(ui, () => !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() =>
        {
            if (view.Hierarchy is { } hierarchy)
                hierarchy.Scroll.BringIntoView(hierarchy.Tree.GetItemBounds(id) ?? throw new InvalidOperationException("Hierarchy item is not visible."));
        }).WaitAsync(token);
        await EditorHeaderSmoke.UntilAsync(ui, () => !ui.NeedsUpdate, token);
        await ui.InvokeAsync(() => ClickItem(ui, view, id)).WaitAsync(token);
    }
    internal static void ClickItem(UiSession ui, EditorView view, string id)
    {
        if (view.Hierarchy is not { } hierarchy) { EditorHeaderSmoke.Click(ui, view.Entities[id]); return; }
        var items = hierarchy.Tree.Items.ToDictionary(item => item.Id);
        for (var parent = items[id].ParentId; parent != null; parent = items[parent].ParentId) hierarchy.Tree.SetExpanded(parent, true);
        var bounds = hierarchy.Tree.GetItemBounds(id) ?? throw new InvalidOperationException("Hierarchy item is not visible.");
        hierarchy.Scroll.BringIntoView(bounds);
        ui.HandleInput(new(InputKind.PointerDown, bounds.X + Math.Max(1, bounds.Width - 4), bounds.Y + bounds.Height / 2));
        ui.HandleInput(new(InputKind.PointerUp, bounds.X + Math.Max(1, bounds.Width - 4), bounds.Y + bounds.Height / 2));
    }
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorDocumentCommands commands, SceneDocumentSession document, ISceneDocumentStore store,
        EditorPreview preview, AetherHost engine, NativeRuntimeFactory runtime)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var panel = view.Hierarchy ?? throw new InvalidOperationException("Hierarchy view was not composed.");
        var model = (EditorHierarchyViewModel)panel.DataContext!;
        var documentModel = (EditorDocumentViewModel)view.Documents!.DataContext!;
        var captures = new List<string>(); var owners = new HashSet<int>();
        var source = Path.Combine(commands.Read().ScenesDirectory!, "Hierarchy.aetherscene");
        var sourceBytes = await File.ReadAllBytesAsync(source, token);
        var owner = 0;
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Changed; }).WaitAsync(token);
        try
        {
            await Doc(DocumentCommand.Open, "Hierarchy.aetherscene");
            await Until(() => model.CanMove && panel.Tree.Items.Count == 3 && !ui.NeedsUpdate);
            var initial = document.Read().Content!; var ids = initial.Resolved.Roots;
            Require(ids.Length == 3, "Hierarchy fixture must initially contain three roots.");
            var child = ids[0]; var parent = ids[1]; var childKey = child.Value.ToString("D"); var parentKey = parent.Value.ToString("D");
            var before = initial.Document.Entities.Single(entity => entity.Id == child);
            var tree = panel.Tree; var inspector = view.Inspector!.Properties.Values.Single(p => p.Property.Key == "x").Input;
            await ui.InvokeAsync(() =>
            {
                ClickItem(ui, view, childKey);
                Require(workspace.SelectedId == childKey && tree.SelectedId == childKey, "Pointer selection did not reach the shared inspector.");
                var from = tree.GetItemBounds(childKey)!.Value; var target = tree.GetItemBounds(parentKey)!.Value;
                var x = from.X + from.Width - 4;
                ui.HandleInput(new(InputKind.PointerDown, x, from.Y + from.Height / 2));
                ui.HandleInput(new(InputKind.PointerMove, x, target.Y + target.Height / 2));
                ui.HandleInput(new(InputKind.PointerUp, x, target.Y + target.Height / 2));
            }).WaitAsync(token);
            await Until(() => Parent() == parent && tree.IsExpanded(parentKey) && !ui.NeedsUpdate);
            Require(document.Read().Content!.UndoCount == 1 && document.Read().IsDirty && workspace.SelectedId == childKey,
                "Drag did not create exactly one shared history transaction.");
            var moved = document.Read().Content!.Document.Entities.Single(entity => entity.Id == child);
            Require(ReferenceEquals(before.Changes, moved.Changes) && moved.DefinitionId == before.DefinitionId,
                "Reparent changed local data or inheritance.");
            await Capture("hierarchy-parented-dark.png");
            await ui.InvokeAsync(() =>
            {
                var row = tree.GetItemBounds(parentKey)!.Value;
                ui.HandleInput(new(InputKind.PointerDown, row.X + 7, row.Y + row.Height / 2));
                ui.HandleInput(new(InputKind.PointerUp, row.X + 7, row.Y + row.Height / 2));
                Require(tree.GetItemBounds(childKey) == null && workspace.SelectedId == parentKey, "Collapse left a hidden selected descendant.");
            }).WaitAsync(token);
            await ClickItemAsync(ui, view, childKey, token);
            await ui.InvokeAsync(() =>
            {
                Require(workspace.SelectedId == childKey, "Expanded child selection did not restore the inspector.");
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true));
            }).WaitAsync(token);
            await Until(() => Parent() == null && !document.Read().IsDirty);
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Y, Control: true))).WaitAsync(token);
            await Until(() => Parent() == parent && document.Read().Content!.RedoCount == 0);
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = panel.Theme.FontFamily })).WaitAsync(token);
            await Until(() => !ui.NeedsUpdate);
            Require(ReferenceEquals(tree, panel.Tree) && ReferenceEquals(inspector, view.Inspector.Properties.Values.Single(p => p.Property.Key == "x").Input) && tree.SelectedId == childKey,
                "Theme/history rebuilt hierarchy or inspector.");
            await Capture("hierarchy-redo-light.png");
            await Doc(DocumentCommand.SaveAs, "Hierarchy-edited.aetherscene");
            await Doc(DocumentCommand.Close);
            await Doc(DocumentCommand.Open, "Hierarchy-edited.aetherscene");
            await Until(() => model.CanMove && tree.Items.Count == 3);
            var saved = await store.ReadAsync(document.Read().Path!, token);
            Require(saved.Document.Id == initial.Document.Id && Parent() == parent && !document.Read().IsDirty &&
                document.Read().Content!.UndoCount == 0, "Save/reopen lost parent identity or retained ephemeral history.");
            await ClickItemAsync(ui, view, childKey, token);
            await Until(() => panel.RootButton.IsEnabled);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, panel.RootButton)).WaitAsync(token);
            await Until(() => Parent() == null && document.Read().IsDirty);
            await ui.InvokeAsync(() => { ui.Focus(tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Z, Control: true)); }).WaitAsync(token);
            await Until(() => Parent() == parent && !document.Read().IsDirty);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, view.StatusBar!.PreviewButton)).WaitAsync(token);
            await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
            await ui.InvokeAsync(() =>
            {
                EditorHeaderSmoke.Click(ui, ((EditorStatusPopupView)view.Overlays.Popup!).StartPreview);
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            }).WaitAsync(token);
            await Until(() => preview.Read() is { Busy: false, Presented: { } stamp } &&
                stamp.Revision == document.Read().Content!.Document.Revision && engine.Preview.LastPresented?.Document == stamp);
            await Capture("hierarchy-preview.png", requireFrame: true);
            await Doc(DocumentCommand.Close);
            Require(runtime.Leases.Count == 1 && runtime.Leases.All(lease => lease.Reaped && lease.ResourcesReleased),
                "Hierarchy preview did not drain its owned child.");
            var bytes = await File.ReadAllBytesAsync(source, token);
            Require(sourceBytes.SequenceEqual(bytes) && engine.Builds.ProcessId == null, "Hierarchy workflow built game code or changed the source fixture.");
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, ((WindowFrame)ui.Root).DefaultTitleBar!.CloseButton)).WaitAsync(token);
            await window.Completion.WaitAsync(token);
            Require(owners.SetEquals([owner]), "Hierarchy notifications escaped the UI owner.");
            File.WriteAllText(Path.Combine(directory, "hierarchy.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Captures = captures, ParentMove = true, LocalTransformPreserved = true, SharedHistory = true,
                SaveReopen = true, RootAction = true, SelectionAndExpansion = true, ThemeRetention = true,
                Children = runtime.Leases.Count, CompilerStarted = false, SourceUnchanged = true,
                OwnerThread = owner, NotificationThreads = owners.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { model.PropertyChanged -= Changed; }
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => owners.Add(Environment.CurrentManagedThreadId);
        EntityId? Parent() => document.Read().Content!.Resolved.Entities[childId()].ParentId;
        EntityId childId() => new(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        async Task Doc(DocumentCommand command, string path = "")
        {
            File.AppendAllText(Path.Combine(directory, "progress.log"), "Hierarchy document " + command + " " + path + Environment.NewLine);
            await Until(() => view.Documents.Commands[command].IsEnabled);
            await ui.InvokeAsync(() => documentModel.PathDraft = path).WaitAsync(token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document." + command, token);
            await commands.Pending.WaitAsync(token);
            Require(commands.Read().Error == null, "Hierarchy document command failed: " + commands.Read().Error);
            await Until(() => !commands.Read().Busy && ReferenceEquals(documentModel.Document?.Content?.Document, document.Read().Content?.Document));
        }
        async Task Until(Func<bool> condition)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = condition()).WaitAsync(token);
                if (ready) return; await Task.Delay(10, token);
                if (preview.Read().Error is { } error) throw new InvalidOperationException("Hierarchy preview failed.", error);
            }
        }
        async Task Capture(string name, bool requireFrame = false)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate || requireFrame && (view.Canvas.DrawnFrame is not { } frame ||
                    engine.Preview.Describe(frame)?.Document != preview.Read().Presented)) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    if (requireFrame)
                    {
                        var bounds = view.Canvas.ImageBounds; var scale = current.Viewport.Scale; var redPixels = 0;
                        for (var y = (int)(bounds.Y * scale); y < (bounds.Y + bounds.Height) * scale; y++)
                            for (var x = (int)(bounds.X * scale); x < (bounds.X + bounds.Width) * scale; x++)
                            {
                                var pixel = bitmap.GetPixel(x, y);
                                if (Math.Abs(pixel.Red - 204) <= 2 && Math.Abs(pixel.Green - 51) <= 2 && Math.Abs(pixel.Blue - 38) <= 2) redPixels++;
                            }
                        Require(redPixels > 50, "Parented cube was not visible in the actual preview frame.");
                    }
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var output = File.Create(Path.Combine(directory, name));
                    data.SaveTo(output); captures.Add(name); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
