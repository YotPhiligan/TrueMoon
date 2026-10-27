using TrueMoon.Aether.Editor;
using System.Numerics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed input targets only this application's tree. No global pointer/keyboard or Windows settings changes.
internal static class EditorSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, EditorCommands commands, AetherHost engine, NativeRuntimeFactory factory)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = timeout.Token;
        var captures = new List<string>(); var executed = new List<string>(); var windowId = window.Id;
        await Click(EditorCommand.Build); Require(engine.Editor.Status.Candidate != null, "Build did not prepare a candidate.");
        await UntilAsync(async () => { var available = false; await ui.InvokeAsync(() => available = view.Header!.RunButton.IsEnabled); return available; });
        await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, view.Header!.RunButton)).WaitAsync(token);
        await commands.Pending.WaitAsync(token); Require(engine.Supervisor.Status.State == SessionState.Playing, "Title-bar Run did not start simulation.");
        executed.Add("Run"); await Click(EditorCommand.Pause);
        Require(engine.Supervisor.Status.State == SessionState.Paused && engine.Supervisor.Status.ProcessId != Environment.ProcessId, "Runtime is not a separate paused process.");
        await Capture("editor-dark.png");
        await ui.InvokeAsync(() => { EditorHierarchySmoke.ClickItem(ui, view, "green-cube"); Require(workspace.SelectedId == "green-cube", "Hierarchy selection failed."); Require(workspace.Properties().Single(x => x.Key == "X").Value == "1.5", "Inspector did not show the selected entity."); }).WaitAsync(token);
        await Click(EditorCommand.Play); await Until(() => engine.Supervisor.Status.Tick >= 2);
        await Click(EditorCommand.Pause); var tick = engine.Supervisor.Status.Tick;
        await Click(EditorCommand.Step); Require(engine.Supervisor.Status.Tick == tick + 1, "Step did not advance exactly one paused tick.");
        var identity = engine.Supervisor.Status.Identity;
        await Click(EditorCommand.Build); await Click(EditorCommand.Replace);
        Require(engine.Supervisor.Status.Identity!.Generation > identity!.Generation && engine.Supervisor.Status.Tick == tick + 1, "Replace did not preserve paused state.");
        Require(window.Id == windowId, "Replacement recreated the editor window.");
        await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" })).WaitAsync(token);
        await Capture("editor-light.png");
        window.Resize(1100, 700);
        await UntilAsync(async () => { var ready = false; await ui.InvokeAsync(() => ready = ui.Viewport.Width / ui.Viewport.Scale == 1100); return ready; });
        await Capture("editor-resized.png");
        await Click(EditorCommand.Stop);
        Require(engine.Frames.MappingCount == 0 && engine.Gpu.ResourceCount == 0 && factory.Leases.All(x => x.Reaped && x.ResourcesReleased), "Stop retained runtime resources.");
        await ui.InvokeAsync(() =>
        {
            Require(!view.IsDisposed && workspace.SelectedId == "green-cube", "Stop lost editor state.");
            var frame = (WindowFrame)ui.Root; Require(frame.DefaultTitleBar != null && frame.ResizeGrip!.IsVisible, "Missing standard custom frame."); InputClick(frame.DefaultTitleBar!.CloseButton);
        }).WaitAsync(token);
        await window.Completion.WaitAsync(token);
        File.WriteAllText(Path.Combine(directory, "interaction.json"), JsonSerializer.Serialize(new { Passed = true, EditorPid = Environment.ProcessId, Commands = executed, Captures = captures,
            WindowId = windowId, Selection = workspace.SelectedId, RuntimePids = factory.Leases.Select(x => x.ProcessId).ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        async Task Click(EditorCommand command)
        {
            await UntilAsync(async () => { var available = false; await ui.InvokeAsync(() => available = view.Commands[command].IsEnabled); return available; });
            if (command is EditorCommand.Play or EditorCommand.Pause or EditorCommand.Stop)
                await ui.InvokeAsync(() => EditorHeaderSmoke.Click(ui, command switch
                { EditorCommand.Play => view.Header!.RunButton, EditorCommand.Pause => view.Header!.PauseButton, _ => view.Header!.StopButton })).WaitAsync(token);
            else await EditorHeaderSmoke.ClickActionAsync(ui, view, command is EditorCommand.Build or EditorCommand.Replace or EditorCommand.Cancel ? "project" : "simulation", "runtime." + command, token);
            await commands.Pending.WaitAsync(token); if (commands.Error != null) throw new InvalidOperationException("UI command failed: " + command, commands.Error);
            executed.Add(command.ToString());
        }
        async Task Capture(string name)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate || view.Canvas.DrawnFrame is not { } frame || !ReferenceEquals(engine.Frames.Presentation, frame.Presentation)) return;
                window.FramePresented -= Presented;
                try
                {
                Require(ReferenceEquals(((WindowFrame)ui.Root).Content, view), "Editor view was recreated.");
                using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                var bounds = view.Canvas.ImageBounds; var scale = ui.Viewport.Scale;
                Require(Math.Abs(bounds.Width / bounds.Height - 16d / 9) < .001, "Viewport distorted the image.");
                var projection = new PrimitiveScene(new CameraTransform(0, 1.6f, 6, 0, 0, 0), 0, Vector3.One).CreateViewProjection(960, 540);
                foreach (var (position, expected) in new[] { (new Vector3(-1.5f, 1, -1), new SKColor(204, 51, 38)), (new Vector3(1.5f, 1, -1), new SKColor(51, 204, 77)), (new Vector3(0, 1, -2), new SKColor(38, 102, 230)) })
                {
                    var clip = Vector4.Transform(new Vector4(position, 1), projection);
                    var x = (int)((bounds.X + bounds.Width * (clip.X / clip.W + 1) / 2) * scale);
                    var y = (int)((bounds.Y + bounds.Height * (clip.Y / clip.W + 1) / 2) * scale); var actual = bitmap.GetPixel(x, y);
                    Require(Math.Abs(actual.Red - expected.Red) <= 2 && Math.Abs(actual.Green - expected.Green) <= 2 && Math.Abs(actual.Blue - expected.Blue) <= 2 && actual.Alpha == 255,
                        $"Viewport pixel {position}: {actual}, expected {expected}.");
                }
                using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name);
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await completion.Task.WaitAsync(token); }
            finally { window.FramePresented -= Presented; }
        }
        void InputClick(Button button) { var bounds = button.Bounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2; ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y)); }
        async Task Until(Func<bool> condition) { while (!condition()) await Task.Delay(10, token); }
        async Task UntilAsync(Func<Task<bool>> condition) { while (!await condition()) await Task.Delay(10, token); }
    }
    internal static void WriteCleanup(string directory, HostedUiSessionFactory sessions, AetherHost engine, NativeRuntimeFactory factory, EditorView view)
    {
        Require(view.IsDisposed && sessions.ActiveSessionCount == 0 && engine.Frames.MappingCount == 0 && engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0 && factory.Leases.All(x => x.Reaped && x.ResourcesReleased), "Final cleanup retained owned resources.");
        foreach (var child in factory.Leases)
        {
            using var cleanup = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, $"runtime-{child.Identity.Generation}.json")));
            var root = cleanup.RootElement; var validation = root.GetProperty("Validation");
            Require(root.GetProperty("Resources").GetInt32() == 0 && root.GetProperty("Handles").GetInt32() == 0, "Runtime retained GPU ownership.");
            Require(validation.GetProperty("Errors").GetInt32() == 0 && validation.GetProperty("Warnings").GetInt32() == 0, "Runtime Vulkan validation failed.");
            if (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1") Require(validation.GetProperty("ValidationEnabled").GetBoolean(), "Runtime validation was not enabled.");
        }
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { Passed = true, UiSessions = sessions.ActiveSessionCount, Mappings = engine.Frames.MappingCount, GpuResources = engine.Gpu.ResourceCount,
            GpuHandles = engine.Gpu.OpenHandleCount, Children = factory.Leases.Count, Reaped = factory.Leases.All(x => x.Reaped), ViewDisposed = view.IsDisposed,
            ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1", ValidationErrors = 0, ValidationWarnings = 0 }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Editor screen proof passed: interaction, close/drain and zero ownership.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
