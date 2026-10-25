using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

// Directed acceptance in one owned window and disposable project; no global input/settings.
internal static class EditorInspectorSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorDocumentCommands commands, SceneDocumentSession document, ISceneDocumentStore store,
        EditorPreview preview, AetherHost engine, NativeRuntimeFactory runtime)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var panel = view.Inspector ?? throw new InvalidOperationException("Inspector is missing.");
        var model = (EditorInspectorViewModel)panel.DataContext!;
        var documentModel = (EditorDocumentViewModel)view.Documents!.DataContext!;
        var custom = new ComponentId(Guid.Parse("40000000-0000-0000-0000-000000000010"));
        var customType = new ComponentTypeId(Guid.Parse("30000000-0000-0000-0000-000000000010"));
        var cube = new EntityId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var empty = "60000000-0000-0000-0000-000000000004";
        var captures = new List<string>(); var owners = new HashSet<int>(); var owner = 0;
        var source = Path.Combine(commands.Read().ScenesDirectory!, "Inspector.aetherscene");
        var originalBytes = await File.ReadAllBytesAsync(source, token);
        await ui.InvokeAsync(() => { owner = Environment.CurrentManagedThreadId; model.PropertyChanged += Changed; }).WaitAsync(token);
        try
        {
            await Until(() => !view.InspectorPanel.IsVisible && !ui.NeedsUpdate);
            await Capture("inspector-hidden-empty.png");
            await Doc(DocumentCommand.Open, "Inspector.aetherscene");
            await Until(() => model.HasSelection && panel.Components.Count == 3 && !ui.NeedsUpdate);
            Require(view.Transform == null && view.PropertiesPanel.Children.Count == 0, "Fixed duplicate inspector was composed.");
            var first = document.Read().Content!;
            var visibleWidth = view.Canvas.Bounds.Width;
            await ui.InvokeAsync(() =>
            {
                Require(view.InspectorPanel.IsVisible && panel.Actions.Children.All(button => button is IconButton { Width: 26, Height: 26, Value.Length: 0 }),
                    "Inspector actions are not compact icon-only buttons.");
                EditorHeaderSmoke.Move(ui, panel.Apply);
            }).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => ReferenceEquals(view.Overlays.PopupOwner, panel) && !ui.NeedsUpdate, token,
                () => EditorHeaderSmoke.Move(ui, panel.Apply));
            await ui.InvokeAsync(() =>
            {
                Require(view.Overlays.Popup is Border { Child: Text hint } && hint.Value.Contains(panel.Apply.AccessibleName) &&
                    hint.Value.Contains("недоступно"), "Disabled icon did not expose its action Tooltip.");
            }).WaitAsync(token);
            await Capture("inspector-tooltip.png");
            await ui.InvokeAsync(() =>
            {
                EditorHeaderSmoke.Move(ui, view.Canvas);
                ui.Focus(view.Hierarchy!.Tree); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            }).WaitAsync(token);
            await Until(() => workspace.SelectedId == null && !view.InspectorPanel.IsVisible && !ui.NeedsUpdate);
            Require(view.Canvas.Bounds.Width == visibleWidth && Math.Abs(view.Canvas.Bounds.X + view.Canvas.Bounds.Width - (view.Bounds.X + view.Bounds.Width - 1)) <= 2 &&
                ReferenceEquals(first, document.Read().Content) && !panel.IsDisposed && view.Overlays.Popup == null,
                "Clearing selection resized the full-area viewport or mutated the document.");
            await Capture("inspector-hidden-selection.png");
            await EditorHierarchySmoke.ClickItemAsync(ui, view, cube.Value.ToString("D"), token);
            await Until(() => view.InspectorPanel.IsVisible && panel.Components.Count == 3 && !ui.NeedsUpdate);
            var transform = model.Components.Single(c => c.TypeId == BuiltInComponents.TransformId).Id;
            var appearance = model.Components.Single(c => c.TypeId == BuiltInComponents.AppearanceId).Id;
            var text = panel.Properties[(custom, "text")].Input;
            Require(text is RichTextEditor && panel.Properties[(custom, "flag")].Input is CheckBox &&
                panel.Properties[(custom, "count")].Input is TextBox, "Schema kinds did not choose actual editor controls.");
            await Capture("inspector-components-dark.png");
            await Click(panel.Components[transform].Header);
            await Click(panel.Components[appearance].Header);
            await Set(panel.Properties[(custom, "count")].Input, long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Set(panel.Properties[(custom, "measure")].Input, "2.5");
            await Click(panel.Properties[(custom, "flag")].Input);
            await Click(panel.Properties[(custom, "maybe")].Null!);
            await Set(panel.Properties[(custom, "maybe")].Input, "");
            await Set(text, "Edited line\nUnicode: Привет 🙂");
            await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = panel.Theme.FontFamily })).WaitAsync(token);
            await Until(() => !ui.NeedsUpdate);
            Require(ReferenceEquals(text, panel.Properties[(custom, "text")].Input) && model.HasDraft && ((RichTextEditor)text).Text.Contains("Привет"),
                "Theme recreated an editor or lost its multiline draft.");
            await Capture("inspector-draft-light.png");
            await ui.InvokeAsync(() => ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter, Control: true))).WaitAsync(token);
            await Until(() => !model.HasDraft && document.Read().Content!.UndoCount == 1);
            var properties = document.Read().Content!.Resolved.Entities[cube].Components[custom].Properties;
            Require(properties["count"].Value.GetInteger() == long.MaxValue && !properties["flag"].Value.GetBoolean() &&
                properties["measure"].Value.GetNumber() == 2.5 && properties["maybe"].Value == PropertyValue.From("") &&
                properties["text"].Value.GetString() == "Edited line\nUnicode: Привет 🙂", "Typed Apply lost scalar kind, Int64, null distinction or multiline text.");
            await Click(panel.Undo); await Until(() => !document.Read().IsDirty);
            await Click(panel.Redo); await Until(() => document.Read().IsDirty);
            await Click(panel.Properties[(custom, "count")].Reset);
            await Click(panel.Apply); await Until(() => !model.HasDraft);
            Require(document.Read().Content!.Resolved.Entities[cube].Components[custom].Properties["count"].Value.GetInteger() == 7,
                "Reset did not restore the schema default of a local occurrence.");
            var unchanged = document.Read().Content;
            await Click(panel.Components[transform].Remove);
            await Until(() => model.ErrorText.Length != 0);
            Require(ReferenceEquals(unchanged, document.Read().Content), "Dependency rejection mutated the scene/history.");
            await Capture("inspector-dependency-error.png");
            await Click(panel.Components[appearance].Remove);
            await Until(() => !panel.Components.ContainsKey(appearance) && panel.RestoreButtons.ContainsKey(appearance));
            Require(ReferenceEquals(text, panel.Properties[(custom, "text")].Input), "Removing a different component rebuilt scalar controls.");
            await Click(panel.RestoreButtons[appearance]);
            await Until(() => panel.Components.ContainsKey(appearance) && !panel.RestoreButtons.ContainsKey(appearance));
            await EditorHierarchySmoke.ClickItemAsync(ui, view, empty, token);
            await Until(() => model.HasSelection && panel.Components.Count == 0 && panel.Properties.Count == 0);
            await Click(panel.AddComponent); await Until(() => panel.AddChoices.IsVisible);
            await Click(panel.AddButtons[(customType, 1)]);
            await Until(() => panel.Components.Count == 1 && panel.Properties.Count == 5);
            Require(model.Components.Single().TypeId == customType && model.CanEditProperties && !model.CanEdit,
                "An entity without Transform did not expose only its actual custom component.");
            var added = model.Components.Single().Id;
            await Click(panel.Undo); await Until(() => panel.Components.Count == 0);
            await Click(panel.Redo); await Until(() => panel.Components.ContainsKey(added));
            await Capture("inspector-empty-entity-component.png");
            await EditorHierarchySmoke.ClickItemAsync(ui, view, cube.Value.ToString("D"), token);
            await Until(() => panel.Components.Count == 3);
            await Doc(DocumentCommand.SaveAs, "Inspector-edited.aetherscene");
            var saved = await store.ReadAsync(document.Read().Path!, token);
            await Doc(DocumentCommand.Close);
            await Doc(DocumentCommand.Open, "Inspector-edited.aetherscene");
            await Until(() => model.HasSelection && !ui.NeedsUpdate);
            Require(saved.Document.Id == first.Document.Id && !document.Read().IsDirty && document.Read().Content!.UndoCount == 0 &&
                document.Read().Content!.Resolved.Entities[new(Guid.Parse(empty))].Components.ContainsKey(added),
                "Save/reopen lost component identity or retained ephemeral history.");
            await Click(view.StatusBar!.PreviewButton);
            await Until(() => view.Overlays.Popup is EditorStatusPopupView && !ui.NeedsUpdate);
            await ui.InvokeAsync(() =>
            {
                EditorHeaderSmoke.Click(ui, ((EditorStatusPopupView)view.Overlays.Popup!).StartPreview);
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape));
            }).WaitAsync(token);
            await Until(() => preview.Read() is { Busy: false, Presented: { } stamp } && stamp.Revision == document.Read().Content!.Document.Revision);
            await Capture("inspector-preview.png", true);
            var viewport = view.Canvas.Bounds; var retainedText = panel.Properties[(custom, "text")].Input;
            foreach (var (theme, name) in new[]
            {
                (Theme.Dark, "islands-preview-dark.png"),
                (Theme.Light, "islands-preview-light.png"),
                (Theme.Dark with { PanelBackdrop = null }, "islands-preview-opaque.png")
            })
            {
                await ui.InvokeAsync(() =>
                {
                    panel.Scroll.Offset = 0;
                    ui.SetTheme(theme with { FontFamily = panel.Theme.FontFamily });
                    EditorHeaderSmoke.Move(ui, view.Canvas);
                }).WaitAsync(token);
                await Until(() => !ui.NeedsUpdate);
                await ui.InvokeAsync(() =>
                {
                    var tree = view.HierarchyIsland.Child!; var inspector = view.InspectorIsland.Child!;
                    Require(view.Canvas.Bounds == viewport && view.InspectorIsland.Reveal == 1 &&
                        tree.Bounds.X - view.ViewportFrame.Bounds.X == 16 &&
                        tree.Bounds.Y - view.ViewportFrame.Bounds.Y == 52 &&
                        view.ViewportFrame.Bounds.X + view.ViewportFrame.Bounds.Width - inspector.Bounds.X - inspector.Bounds.Width == 16 &&
                        tree.Bounds.Height < view.ViewportFrame.Bounds.Height / 2 &&
                        inspector.Bounds.Height < view.ViewportFrame.Bounds.Height && tree.CornerRadius == 8 && inspector.CornerRadius == 8,
                        "Floating panel geometry changed the viewport, margin, content height or reveal state.");
                    Require(tree.Backdrop == theme.PanelBackdrop && inspector.Backdrop == theme.PanelBackdrop &&
                        tree.Background == (theme.PanelBackdrop == null ? theme.Surface : Color.Transparent) &&
                        inspector.Background == (theme.PanelBackdrop == null ? theme.Surface : Color.Transparent) &&
                        ReferenceEquals(retainedText, panel.Properties[(custom, "text")].Input),
                        "Panel material/fallback or retained property controls changed across themes.");
                }).WaitAsync(token);
                await Capture(name, true);
            }
            var titleBar = ((WindowFrame)ui.Root).DefaultTitleBar!;
            await ui.InvokeAsync(() =>
            {
                ui.SetTheme(Theme.Dark with { FontFamily = panel.Theme.FontFamily });
                ui.Focus(view.Hierarchy!.Tree); EditorHeaderSmoke.Move(ui, view.Canvas);
            }).WaitAsync(token);
            await Until(() => titleBar.OverlayReveal == 0 && !ui.NeedsUpdate);
            Require(view.Canvas.Bounds == viewport && view.Canvas.Bounds.Y < titleBar.Bounds.Y + titleBar.Bounds.Height,
                "Idle chrome changed the full-area preview.");
            await Capture("viewport-chrome-idle.png", true);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Move(ui, view.Header!.BrandIcon)).WaitAsync(token);
            await Until(() => titleBar.OverlayReveal == 1 && !ui.NeedsUpdate);
            Require(view.Canvas.Bounds == viewport, "Hover reframed the scene.");
            await Capture("viewport-chrome-hover.png", true);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Move(ui, view.Header!.Caption)).WaitAsync(token);
            await EditorHeaderSmoke.UntilAsync(ui, () => ReferenceEquals(view.Overlays.PopupOwner, view.Header) && !ui.NeedsUpdate, token,
                () => EditorHeaderSmoke.Move(ui, view.Header!.Caption));
            Require(view.Overlays.Popup is Border { Child: Text captionHint } && captionHint.Value.Contains(document.Read().Path!),
                "Caption Tooltip did not show the full scene path.");
            await Capture("viewport-caption-tooltip.png", true);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Move(ui, view.Canvas)).WaitAsync(token);
            var footer = view.StatusBar!;
            await Until(() => footer.Bar.OverlayReveal == 0 && titleBar.OverlayReveal == 0 && !ui.NeedsUpdate);
            Require(view.Canvas.Bounds == viewport, "Idle status bar changed the full-area preview.");
            await Capture("viewport-status-idle.png", true);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Move(ui, footer.Bar)).WaitAsync(token);
            await Until(() => footer.Bar.OverlayReveal == 1 && !ui.NeedsUpdate);
            Require(view.Canvas.Bounds == viewport && footer.Bar.Backdrop == footer.Theme.PanelBackdrop && footer.SummaryButton.Opacity == 1,
                "Status hover reframed the scene or faded its foreground.");
            await Capture("viewport-status-hover.png", true);
            await Doc(DocumentCommand.Close);
            Require(runtime.Leases.Count == 1 && runtime.Leases.All(c => c.Reaped && c.ResourcesReleased) && engine.Builds.ProcessId == null,
                "Inspector preview retained a child or started a compiler.");
            var currentBytes = await File.ReadAllBytesAsync(source, token);
            Require(originalBytes.SequenceEqual(currentBytes), "Source fixture changed.");
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, ((WindowFrame)ui.Root).DefaultTitleBar!.CloseButton)).WaitAsync(token);
            await window.Completion.WaitAsync(token);
            Require(owners.SetEquals([owner]), "Inspector notifications escaped its UI owner.");
            File.WriteAllText(Path.Combine(directory, "inspector.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Captures = captures, TypedProperties = true, ActualComponentsOnly = true, ComponentActions = true,
                ResetProvenance = true, SharedHistory = true, DependencyRejection = true, SaveReopen = true, ThemeRetention = true,
                IconActions = true, Tooltips = true, ContextVisibility = true,
                FloatingIslands = true, StableViewport = true, PanelThemesAndOpaqueFallback = true,
                FullAreaViewport = true, HeaderHoverWithoutReframe = true, CaptionPathTooltip = true, StatusHoverWithoutReframe = true,
                Children = runtime.Leases.Count, CompilerStarted = false, SourceUnchanged = true, OwnerThread = owner, NotificationThreads = owners.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { model.PropertyChanged -= Changed; }
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => owners.Add(Environment.CurrentManagedThreadId);
        async Task Doc(DocumentCommand command, string path = "")
        {
            Trace("Document " + command + " " + path);
            await Until(() => view.Documents!.Commands[command].IsEnabled);
            await ui.InvokeAsync(() => documentModel.PathDraft = path).WaitAsync(token);
            await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "document." + command, token);
            await commands.Pending.WaitAsync(token); Require(commands.Read().Error == null, "Document command failed: " + commands.Read().Error);
            await Until(() => !commands.Read().Busy && ReferenceEquals(documentModel.Document?.Content?.Document, document.Read().Content?.Document));
        }
        async Task Reveal(Element element)
        {
            await ui.InvokeAsync(() => panel.Scroll.BringIntoView(element.Bounds)).WaitAsync(token);
            await Until(() => !ui.NeedsUpdate);
        }
        async Task Click(Element element)
        {
            Trace("Click " + (element is Text label ? label.Value : element.AccessibleName) + " " + element.GetType().Name);
            await Reveal(element); await Until(() => element.IsEffectivelyEnabled && element.Bounds.Width > 0);
            await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, element)).WaitAsync(token);
        }
        async Task Set(Element element, string value)
        {
            Trace("Set " + element.AccessibleName);
            await Click(element);
            await ui.InvokeAsync(() =>
            {
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
                ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Backspace));
                var lines = value.Split('\n');
                for (var index = 0; index < lines.Length; index++)
                {
                    if (index > 0) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
                    if (lines[index].Length != 0) ui.HandleInput(new(InputKind.Text, Text: lines[index]));
                }
            }).WaitAsync(token);
            Require(element is TextBox box ? box.Value == value : ((RichTextEditor)element).Text == value, "Directed scalar text input was not delivered.");
        }
        async Task Until(Func<bool> condition)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = condition()).WaitAsync(token);
                if (ready) return;
                if (preview.Read().Error is { } error) throw new InvalidOperationException("Inspector preview failed.", error);
                await Task.Delay(10, token);
            }
        }
        async Task Capture(string name, bool frame = false)
        {
            Trace("Capture " + name);
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate || frame && (view.Canvas.DrawnFrame is not { } shown || engine.Preview.Describe(shown)?.Document != preview.Read().Presented)) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage();
                    if (frame)
                    {
                        using var bitmap = SKBitmap.FromImage(image); var bounds = view.Canvas.ImageBounds; var count = 0;
                        for (var y = Math.Max(0, (int)(bounds.Y * current.Viewport.Scale)); y < Math.Min(bitmap.Height, (int)((bounds.Y + bounds.Height) * current.Viewport.Scale)) && count <= 50; y++)
                            for (var x = Math.Max(0, (int)(bounds.X * current.Viewport.Scale)); x < Math.Min(bitmap.Width, (int)((bounds.X + bounds.Width) * current.Viewport.Scale)) && count <= 50; x++)
                            { var pixel = bitmap.GetPixel(x, y); if (Math.Abs(pixel.Red - 204) <= 2 && Math.Abs(pixel.Green - 51) <= 2 && Math.Abs(pixel.Blue - 38) <= 2) count++; }
                        Require(count > 50, "The actual inspector preview did not draw the scene cube.");
                    }
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
        void Trace(string text) => File.AppendAllText(Path.Combine(directory, "progress.log"), text + Environment.NewLine);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
