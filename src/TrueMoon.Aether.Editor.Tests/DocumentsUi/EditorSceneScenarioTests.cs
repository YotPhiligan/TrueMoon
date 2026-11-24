using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
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
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Editor.Tests.DocumentsUi;

public sealed class EditorSceneScenarioTests(ITestOutputHelper output)
{
    private const string SceneName = "uiqa-round-trip.aetherscene";
    private const string TreeId = "editor.hierarchy.tree";
    private static readonly JsonSerializerOptions ScenarioJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [Fact]
    public void SceneRoundTrip_ReplaysSemanticInputAndRestoresTheSavedTransform()
    {
        var scenarioPath = Environment.GetEnvironmentVariable("TRUEMOON_EDITOR_SCENARIO_INPUT");
        if (string.IsNullOrWhiteSpace(scenarioPath))
        {
            Replay(LoadScenario());
            return;
        }

        var parameterPath = Environment.GetEnvironmentVariable("TRUEMOON_EDITOR_SCENARIO_PARAMETERS");
        Dictionary<string, string>? parameters = null;
        if (!string.IsNullOrWhiteSpace(parameterPath))
        {
            using var values = File.OpenRead(parameterPath);
            parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(values, ScenarioJson)
                ?? throw new InvalidDataException("The scenario parameter file must contain an object.");
        }
        using var input = File.OpenRead(scenarioPath);
        Replay(ReadScenario(input), parameters: parameters);
    }

    [Fact]
    public void SceneRoundTrip_ComparesIndependentRunsAndWritesDifferences()
    {
        var scenario = LoadScenario();
        var outputRoot = Environment.GetEnvironmentVariable("TRUEMOON_EDITOR_SCENARIO_RESULTS");
        var root = string.IsNullOrWhiteSpace(outputRoot)
            ? Directory.CreateTempSubdirectory("TrueMoon-EditorScenario-").FullName
            : Path.Combine(Path.GetFullPath(outputRoot), Guid.NewGuid().ToString("N"));
        output.WriteLine($"Comparison results: {root}");

        try
        {
            var recorder = new ScenarioRecorder();
            var baseline = Replay(scenario, capture: true, recorder: recorder);
            Directory.CreateDirectory(root);
            var recordingPath = Path.Combine(root, "recorded.scenario.json");
            using (var file = new FileStream(recordingPath, FileMode.CreateNew))
                JsonSerializer.Serialize(file, recorder.Complete(scenario), ScenarioJson);
            var recording = File.ReadAllText(recordingPath);
            Assert.DoesNotContain(SceneName, recording, StringComparison.Ordinal);
            var recorded = Assert.IsType<Scenario>(JsonSerializer.Deserialize<Scenario>(recording, ScenarioJson));
            Assert.Equal(scenario.Checkpoints.Select(c => c.Input.Length), recorded.Checkpoints.Select(c => c.Input.Length));
            Assert.Equal(2, recorded.Checkpoints.SelectMany(c => c.Input).Count(a => a.TextParameter == "scene-path" && a.Text == null));
            Assert.Throws<InvalidDataException>(() => ResolveText(new("editor.document.path", ActionKind.Text, null, UiKey.None,
                false, false, "scene-path"), null));
            var repeat = Replay(recorded, capture: true, parameters: new Dictionary<string, string> { ["scene-path"] = SceneName });
            var identical = EditorScenarioComparison.Compare(baseline, repeat, Path.Combine(root, "repeat"));
            Assert.True(identical.Passed, string.Join(", ", identical.Checkpoints.Where(c => !c.Passed).Select(c => c.Id)));
            Assert.Equal(scenario.Checkpoints.Length, identical.Checkpoints.Length);
            Assert.All(identical.Checkpoints, checkpoint =>
            {
                Assert.Empty(checkpoint.StateChanges);
                Assert.Empty(checkpoint.GeometryChanges);
                Assert.Equal(0, checkpoint.Pixels!.DifferentPixels);
                Assert.Null(checkpoint.Frames);
            });

            var variant = scenario with
            {
                Width = scenario.Width + 80,
                Checkpoints = scenario.Checkpoints.Select(checkpoint => checkpoint with
                {
                    Input = checkpoint.Input.Select(step => step.Target == "$transform-x" && step.Kind == ActionKind.Text
                        ? step with { Text = "53" } : step).ToArray(),
                    PositionX = checkpoint.PositionX == 37 ? 53 : checkpoint.PositionX,
                    EditorX = checkpoint.EditorX == "37" ? "53" : checkpoint.EditorX
                }).ToArray()
            };
            var changed = Replay(variant, capture: true);
            var comparison = EditorScenarioComparison.Compare(baseline, changed, Path.Combine(root, "variant"));
            Assert.False(comparison.Passed);
            Assert.Equal(scenario.Checkpoints.Length, comparison.Checkpoints.Length);
            var reopened = Assert.Single(comparison.Checkpoints, checkpoint => checkpoint.Id == "reopened");
            var position = Assert.Single(reopened.StateChanges, change => change.Path == "document.positionX");
            Assert.Equal(37, position.Baseline.GetDouble());
            Assert.Equal(53, position.Candidate.GetDouble());
            var width = Assert.Single(reopened.GeometryChanges, change => change.Path == "viewport.width");
            Assert.Equal(scenario.Width, width.Baseline.GetInt32());
            Assert.Equal(variant.Width, width.Candidate.GetInt32());
            Assert.Contains(reopened.GeometryChanges, change => change.Path == "editor.inspector.apply.bounds");
            Assert.True(reopened.Pixels!.DifferentPixels > 0);
            var frames = Assert.IsType<EditorScenarioComparison.Frames>(reopened.Frames);
            var variantDirectory = Path.Combine(root, "variant");
            using var before = SKBitmap.Decode(Path.Combine(variantDirectory, frames.Baseline!));
            using var after = SKBitmap.Decode(Path.Combine(variantDirectory, frames.Candidate!));
            using var diff = SKBitmap.Decode(Path.Combine(variantDirectory, frames.Diff!));
            Assert.Equal(scenario.Width, before.Width);
            Assert.Equal(variant.Width, after.Width);
            Assert.Equal(variant.Width, diff.Width);
            Assert.Equal(scenario.Height, diff.Height);
            Assert.True(diff.GetPixel(reopened.Pixels.FirstX!.Value, reopened.Pixels.FirstY!.Value).Red > 0);
            using var savedReport = JsonDocument.Parse(File.ReadAllText(Path.Combine(variantDirectory, "comparison.json")));
            Assert.False(savedReport.RootElement.GetProperty("passed").GetBoolean());
            var savedPoints = savedReport.RootElement.GetProperty("checkpoints");
            Assert.Equal(scenario.Checkpoints.Length, savedPoints.GetArrayLength());
            var savedReopened = Assert.Single(savedPoints.EnumerateArray(), point => point.GetProperty("id").GetString() == "reopened");
            Assert.Equal(frames.Diff, savedReopened.GetProperty("frames").GetProperty("diff").GetString());
            var savedPosition = Assert.Single(savedReopened.GetProperty("stateChanges").EnumerateArray(),
                change => change.GetProperty("path").GetString() == "document.positionX");
            Assert.Equal(53, savedPosition.GetProperty("candidate").GetDouble());
            output.WriteLine($"Reopened checkpoint changed {reopened.Pixels.DifferentPixels} pixels.");
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) Directory.Delete(root, recursive: true);
        }
    }

    private static Scenario LoadScenario()
    {
        using var source = typeof(EditorSceneScenarioTests).Assembly.GetManifestResourceStream(
            "TrueMoon.Aether.Editor.Tests.DocumentsUi.scene-round-trip.scenario.json");
        Assert.NotNull(source);
        return ReadScenario(source);
    }

    private static Scenario ReadScenario(Stream source)
    {
        if (source.Length is <= 0 or > 1024 * 1024) throw new InvalidDataException("Invalid Editor scenario size.");
        var scenario = Assert.IsType<Scenario>(JsonSerializer.Deserialize<Scenario>(source, ScenarioJson));
        Assert.Equal(1, scenario.FormatVersion);
        Assert.NotEmpty(scenario.Checkpoints);
        return scenario;
    }

    private EditorScenarioComparison.Checkpoint[] Replay(Scenario scenario, bool capture = false,
        ScenarioRecorder? recorder = null, IReadOnlyDictionary<string, string>? parameters = null)
    {
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
        var clock = capture ? new HeaderClock() : null;
        using var ui = InspectorFixture.Ui(frame, clock: clock, width: scenario.Width, height: scenario.Height);
        Refresh();

        var deadline = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(scenario.TimeoutMs);
        var opening = Guid.Empty;
        long? savedRevision = null;
        EntityId? entity = null;
        ComponentId? transform = null;
        var checkpoints = new List<EditorScenarioComparison.Checkpoint>();
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
                    var result = Send(target, new(kind, Key: step.Key, Text: ResolveText(step, parameters), Control: step.Control,
                        Shift: step.Shift),
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
            var actualScene = content == null ? SceneState.Closed : state.IsDirty ? SceneState.Dirty : SceneState.Clean;
            Assert.Equal(checkpoint.Scene, actualScene);
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

            if (clock != null && view.InspectorIsland.NeedsFrame)
            {
                clock.Advance(200);
                ui.Update();
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
            if (capture)
            {
                Assert.Equal(view.InspectorIsland.IsOpen ? 1f : 0f, view.InspectorIsland.Reveal);
                var inspectorX = transform is { } componentId && view.Inspector!.Properties.TryGetValue((componentId, "x"), out var row)
                    ? Assert.IsType<TextBox>(row.Input).Value : null;
                var selected = entity.HasValue && model.SelectedId == entity.Value.Value.ToString("D");
                var document = new EditorScenarioComparison.Document(actualScene.ToString(), content?.Document.Revision,
                    content?.Document.Entities.Length ?? 0, x, inspectorX, content?.UndoCount ?? 0, content?.RedoCount ?? 0,
                    selected ? "$entity" : model.SelectedId);
                Rect? selectionIdLabel = selected && view.InspectorIsland.IsOpen && view.Selection.IsVisible &&
                    view.Selection.Value == EditorScene.Name(entity!.Value.Value.ToString("D")) ? view.Selection.Bounds : null;
                using var image = ui.SnapshotRasterImage();
                checkpoints.Add(EditorScenarioComparison.Capture(checkpoint.Id, document, snapshot, transform?.Value, selectionIdLabel, image));
            }
            recorder?.Checkpoint(checkpoint);
        }
        return checkpoints.ToArray();

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
            var recordedId = transform is { } t && id == $"editor.inspector.transform.{t.Value:N}.x" ? "$transform-x" : id;
            recorder?.Accept(recordedId, input, result.Status);
            ui.Update();
            return result;
        }
    }

    private enum ActionKind { Click, KeyDown, Text }
    private enum SceneState { Closed, Clean, Dirty }
    private sealed record Scenario(int FormatVersion, int TimeoutMs, int Width, int Height, Checkpoint[] Checkpoints);
    private sealed record Checkpoint(string Id, Action[] Input, SceneState Scene, int? EntityCount, int? UndoCount,
        int? RedoCount, double? PositionX, string? EditorX, bool Selected, string? Focused, string[]? Ready, string[]? Disabled, string[]? Missing);
    private sealed record Action(string Target, ActionKind Kind, string? Text,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] UiKey Key,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Control,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Intercepted,
        string? TextParameter = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Shift = false);

    private static string? ResolveText(Action input, IReadOnlyDictionary<string, string>? parameters)
    {
        if (input.TextParameter is not { } parameter) return input.Text;
        if (input.Kind != ActionKind.Text || input.Text != null || parameters == null ||
            !parameters.TryGetValue(parameter, out var value) || string.IsNullOrEmpty(value))
            throw new InvalidDataException($"Unresolved or invalid scenario text parameter '{parameter}'.");
        return value;
    }

    // The test harness explicitly starts recording and writes only actions admitted by UiSession.
    private sealed class ScenarioRecorder
    {
        private readonly List<Checkpoint> _checkpoints = [];
        private readonly List<Action> _actions = [];
        private string? _pressed;
        private int _textParameters;

        internal void Accept(string target, UiInput input, UiSemanticTargetStatus status)
        {
            if (status is not (UiSemanticTargetStatus.Routed or UiSemanticTargetStatus.Intercepted))
                throw new InvalidOperationException("Only admitted semantic input can be recorded.");
            var intercepted = status == UiSemanticTargetStatus.Intercepted;
            switch (input.Kind)
            {
                case InputKind.PointerDown when _pressed == null && !intercepted:
                    _pressed = target;
                    break;
                case InputKind.PointerUp when _pressed == target && !intercepted:
                    _actions.Add(new(target, ActionKind.Click, null, UiKey.None, false, false));
                    _pressed = null;
                    break;
                case InputKind.KeyDown when _pressed == null:
                    _actions.Add(new(target, ActionKind.KeyDown, null, input.Key, input.Control, intercepted, Shift: input.Shift));
                    break;
                case InputKind.Text when _pressed == null && !intercepted:
                    // The synthetic numeric edit is the fixture's only opt-in literal.
                    var literal = target == "$transform-x" && input.Text is "37";
                    var parameter = literal ? null : target == "editor.document.path" ? "scene-path" : $"text-{++_textParameters}";
                    _actions.Add(new(target, ActionKind.Text, literal ? input.Text : null, UiKey.None, false, false, parameter));
                    break;
                default: throw new InvalidOperationException($"The recorder cannot represent {input.Kind} in this scenario.");
            }
        }

        internal void Checkpoint(Checkpoint expectation)
        {
            if (_pressed != null) throw new InvalidOperationException("A recording checkpoint requires a completed gesture.");
            _checkpoints.Add(expectation with { Input = _actions.ToArray() });
            _actions.Clear();
        }

        internal Scenario Complete(Scenario source)
        {
            if (_pressed != null || _actions.Count != 0 || _checkpoints.Count != source.Checkpoints.Length)
                throw new InvalidOperationException("The scenario recording is incomplete.");
            return source with { Checkpoints = _checkpoints.ToArray() };
        }
    }
}
