using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.Tooling.Hierarchy;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Editor.UI.Hierarchy;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class EditorSceneScenarioTests(ITestOutputHelper output)
{
    private const string SceneName = "uiqa-round-trip.aetherscene";
    private const string TreeId = "editor.hierarchy.tree";

    [Fact]
    public void SceneRoundTrip_ReplaysSemanticInputAndRestoresTheSavedTransform()
    {
        using var source = typeof(EditorSceneScenarioTests).Assembly.GetManifestResourceStream(
            "TrueMoon.Aether.Editor.Tests.DocumentsUi.scene-round-trip.scenario.json");
        Assert.NotNull(source);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        var scenario = Assert.IsType<Scenario>(JsonSerializer.Deserialize<Scenario>(source, options));
        Assert.Equal(1, scenario.FormatVersion);

        var codec = new SceneFileCodec(DocumentData.Factory());
        var store = new JsonSceneDocumentStore(codec);
        using var owner = new DocumentUiFixture(sceneStore: store);
        var path = Path.Combine(owner.Files.Project.ScenesDirectory, SceneName);
        var seed = DocumentData.Empty();
        DocumentUiFixture.Wait(store.WriteAsync(path, seed, expected: null));
        var seedBytes = File.ReadAllBytes(path);

        var workspace = new EditorWorkspace(new("Scene round trip", 0, []));
        using var documents = new EditorDocumentViewModel(owner.Commands);
        using var preview = new EditorPreview((scene, _) => Task.FromResult(EditingFixture.Receipt(scene)), () => Task.CompletedTask);
        using var inspector = new EditorInspectorViewModel(owner.Commands, preview);
        using var hierarchy = new EditorHierarchyViewModel(workspace, owner.Commands, inspector);
        using var model = new EditorViewModel(workspace, new((_, _) => Task.CompletedTask, CancellationToken.None), documents, inspector, hierarchy);
        var views = new Factory<EditorView>(() => new(model, new EditorViewStyle(),
            new Factory<EditorDocumentView>(() => new(documents, new EditorDocumentViewStyle(), compactHeader: true)),
            null, true, null,
            new Factory<EditorHierarchyView>(() => new(hierarchy, new EditorHierarchyViewStyle())),
            new Factory<EditorInspectorView>(() => new(inspector, new EditorInspectorViewStyle()))));
        var window = new EditorMainWindow(model, documents, views, new EditorMainWindowStyle(),
            new("Scene round trip", new(scenario.Width, scenario.Height)));
        var view = window.EditorView;
        using var frame = WindowFrame.CreateDefault(window.Title, window);
        frame.TitleBarOverContent = true;
        var header = new EditorHeaderView(model, documents, inspector, new EditorHeaderViewStyle(),
            new(view.Overlays, window.Title, () => throw new InvalidOperationException("The scenario must keep the editor open.")));
        frame.DefaultTitleBar!.SetHeader(header);
        view.Header = header;
        using var ui = InspectorFixture.Ui(frame, width: scenario.Width, height: scenario.Height);
        Refresh();

        var deadline = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(scenario.TimeoutMs);
        var opening = Guid.Empty;
        long? savedRevision = null;
        EntityId? entity = null;
        ComponentId? transform = null;
        foreach (var checkpoint in scenario.Checkpoints)
        {
            output.WriteLine($"Checkpoint {checkpoint.Id}");
            foreach (var step in checkpoint.Input)
            {
                Assert.True(deadline.Elapsed < timeout, $"Deadline elapsed before {checkpoint.Id} / {step.Target}.");
                var target = Resolve(step.Target);
                if (step.Kind == ActionKind.Click)
                {
                    var node = Assert.IsType<UiSemanticNode>(Find(ui.CaptureSemantics(), target, UiSemanticTargetStatus.Ready).Node);
                    var bounds = node.VisibleBounds;
                    Assert.True(bounds.Width > 0 && bounds.Height > 0, $"Target {target} has no visible hit area.");
                    Send(target, new(InputKind.PointerDown, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
                    Send(target, new(InputKind.PointerUp, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                        requireHandled: node.Role == UiSemanticRole.Button);
                }
                else
                {
                    var kind = step.Kind == ActionKind.Text ? InputKind.Text : InputKind.KeyDown;
                    var result = Send(target, new(kind, Key: step.Key, Text: step.Text, Control: step.Control),
                        step.Intercepted ? UiSemanticTargetStatus.Intercepted : UiSemanticTargetStatus.Routed);
                    if (kind == InputKind.Text) Assert.Equal(target, result.FocusedId);
                }

                var remaining = timeout - deadline.Elapsed;
                Assert.True(SpinWait.SpinUntil(() => owner.Commands.Pending.IsCompleted && !owner.Commands.Read().Busy,
                    remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero),
                    $"Document operation did not settle after {checkpoint.Id} / {step.Target}.");
                Assert.True(owner.Commands.Pending.IsCompletedSuccessfully);
                Assert.Null(owner.Commands.Read().Error);
                Refresh();
            }

            var state = owner.Document.Read();
            var content = state.Content;
            double? x = null;
            Assert.Equal(checkpoint.Scene, content == null ? SceneState.Closed : state.IsDirty ? SceneState.Dirty : SceneState.Clean);
            if (content != null)
            {
                Assert.Equal(path, state.Path);
                Assert.Equal(seed.Id, content.Document.Id);
                Assert.Equal(checkpoint.EntityCount, content.Document.Entities.Length);
                Assert.Equal(checkpoint.UndoCount, content.UndoCount);
                Assert.Equal(checkpoint.RedoCount, content.RedoCount);
                if (checkpoint.PositionX is { } expectedX)
                {
                    var created = Assert.Single(content.Resolved.Entities.Values);
                    var component = Assert.Single(created.Components.Values, c => c.TypeId == BuiltInComponents.TransformId);
                    entity ??= created.Id;
                    transform ??= component.Id;
                    Assert.Equal(entity.Value, created.Id);
                    Assert.Equal(transform.Value, component.Id);
                    x = component.Properties["x"].Value.GetNumber();
                    Assert.Equal(expectedX, x);
                    if (checkpoint.Selected)
                    {
                        var tree = view.Hierarchy!.Tree;
                        Assert.Equal(entity.Value.Value.ToString("D"), tree.SelectedId);
                        Assert.Equal(tree.SelectedId, model.SelectedId);
                    }
                    if (checkpoint.EditorX is { } editorX)
                        Assert.Equal(editorX, Assert.IsType<TextBox>(view.Inspector!.Properties[(transform.Value, "x")].Input).Value);
                }
            }

            var snapshot = ui.CaptureSemantics();
            foreach (var id in checkpoint.Ready ?? []) Find(snapshot, Resolve(id), UiSemanticTargetStatus.Ready);
            foreach (var id in checkpoint.Disabled ?? []) Find(snapshot, Resolve(id), UiSemanticTargetStatus.Disabled);
            foreach (var id in checkpoint.Missing ?? []) Find(snapshot, Resolve(id), UiSemanticTargetStatus.NotFound);
            Assert.Equal(UiSemanticRole.Tree, Find(snapshot, TreeId, UiSemanticTargetStatus.Ready).Node!.Role);
            if (checkpoint.Selected)
                Assert.Equal(UiSemanticRole.TextInput, Find(snapshot, Resolve("$transform-x"), UiSemanticTargetStatus.Ready).Node!.Role);
            if (checkpoint.Focused is { } focus)
            {
                var focused = Assert.Single(snapshot.Nodes, n => n.IsFocused);
                Assert.Equal(Resolve(focus), focused.Id);
            }

            switch (checkpoint.Id)
            {
                case "initial-open":
                    opening = state.OpeningId;
                    Assert.NotEqual(Guid.Empty, opening);
                    break;
                case "saved":
                    var savedBytes = File.ReadAllBytes(path);
                    Assert.False(seedBytes.AsSpan().SequenceEqual(savedBytes));
                    var saved = codec.Decode(savedBytes);
                    Assert.Equal(opening, state.OpeningId);
                    savedRevision = Assert.IsType<SceneAuthoringState>(content).Document.Revision;
                    Assert.Equal(savedRevision, saved.Revision);
                    break;
                case "closed":
                    Assert.Equal(Guid.Empty, state.OpeningId);
                    Assert.Null(model.SelectedId);
                    break;
                case "reopened":
                    Assert.NotEqual(Guid.Empty, state.OpeningId);
                    Assert.NotEqual(opening, state.OpeningId);
                    Assert.Equal(savedRevision, Assert.IsType<SceneAuthoringState>(content).Document.Revision);
                    break;
            }

            var focusedId = snapshot.Nodes.SingleOrDefault(node => node.IsFocused)?.Id;
            output.WriteLine($"  {checkpoint.Scene}, entities={content?.Document.Entities.Length ?? 0}, " +
                $"x={x}, undo={content?.UndoCount ?? 0}, redo={content?.RedoCount ?? 0}, focus={focusedId}");
        }

        void Refresh()
        {
            model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false));
            ui.Update();
            view.Documents!.SynchronizeFocus(ui);
            ui.Update();
        }

        string Resolve(string id) => id == "$transform-x"
            ? $"editor.inspector.transform.{(transform ?? throw new InvalidOperationException("Transform has not been observed.")).Value:N}.x"
            : id;

        UiSemanticTargetResult Find(UiSemanticSnapshot snapshot, string id, UiSemanticTargetStatus expected)
        {
            Assert.False(snapshot.IsTruncated);
            var lookup = ui.FindSemanticTarget(snapshot, id);
            Assert.True(lookup.Status == expected, $"Target {id}: expected {expected}, observed {lookup.Status}.");
            return lookup;
        }

        UiSemanticInputResult Send(string id, UiInput input, UiSemanticTargetStatus expected = UiSemanticTargetStatus.Routed,
            bool requireHandled = true)
        {
            var result = ui.HandleSemanticInput(ui.CaptureSemantics(), id, input);
            Assert.True(result.Status == expected,
                $"Input {input.Kind} to {id}: expected {expected}, observed {result.Status}, hit {result.HitTargetId}.");
            if (expected == UiSemanticTargetStatus.Routed) Assert.Equal(id, result.HitTargetId);
            if (requireHandled) Assert.True(result.Input.Handled, $"Input {input.Kind} to {id} was not handled.");
            ui.Update();
            return result;
        }
    }

    private enum ActionKind { Click, KeyDown, Text }
    private enum SceneState { Closed, Clean, Dirty }
    private sealed record Scenario(int FormatVersion, int TimeoutMs, int Width, int Height, Checkpoint[] Checkpoints);
    private sealed record Checkpoint(string Id, Action[] Input, SceneState Scene, int? EntityCount, int? UndoCount,
        int? RedoCount, double? PositionX, string? EditorX, bool Selected, string? Focused, string[]? Ready, string[]? Disabled, string[]? Missing);
    private sealed record Action(string Target, ActionKind Kind, string? Text, UiKey Key, bool Control, bool Intercepted);
}
