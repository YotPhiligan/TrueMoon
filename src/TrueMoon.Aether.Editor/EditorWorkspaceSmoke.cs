using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

internal static class EditorWorkspaceSmoke
{
    internal static async Task RunAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view,
        EditorWorkspace workspace, AetherHost engine, NativeRuntimeFactory factory)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var presented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Capture(UiSession current)
        {
            if (current.NeedsUpdate) return;
            window.FramePresented -= Capture;
            try
            {
                Require(workspace.EntityCount == 0 && workspace.SelectedId == null, "Workspace must not invent a demo scene.");
                Require(view.Commands[EditorCommand.Build].IsEnabled == workspace.HasProject, "Build availability disagrees with the selected project.");
                Require(view.Commands.Where(pair => pair.Key != EditorCommand.Build).All(pair => !pair.Value.IsEnabled), "Game actions must be unavailable before an explicit build.");
                Require(engine.Supervisor.Status.State == SessionState.Stopped && factory.Leases.Count == 0 && engine.Builds.ProcessId == null,
                    "Opening a workspace started a game or compiler.");
                Require((engine.Editor.Status.Context != null) == workspace.HasProject, "Unexpected game context.");
                using var image = current.ReadbackOpenGLImage();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var output = File.Create(Path.Combine(directory, "workspace.png"));
                data.SaveTo(output);
                presented.TrySetResult();
            }
            catch (Exception error) { presented.TrySetException(error); }
        }
        window.FramePresented += Capture;
        window.RequestFrame();
        try { await presented.Task.WaitAsync(deadline.Token); }
        finally { window.FramePresented -= Capture; }
        await EditorHeaderSmoke.ClickActionAsync(ui, view, "file", "file.exit", deadline.Token);
        await window.Completion.WaitAsync(deadline.Token);
        File.WriteAllText(Path.Combine(directory, "workspace.json"), JsonSerializer.Serialize(new
        {
            Passed = true, workspace.HasProject, workspace.EntityCount, Children = factory.Leases.Count,
            CompilerStarted = engine.Builds.ProcessId != null, Captures = new[] { "workspace.png" }
        }));
    }

    internal static void WriteProjectCleanup(string directory, EditorProjects projects, ProjectSession? selected)
    {
        Require(projects.IsClosed && projects.Active == null && (selected == null || selected.IsClosed), "Project owner retained a session.");
        File.WriteAllText(Path.Combine(directory, "project-cleanup.json"),
            JsonSerializer.Serialize(new { Passed = true, projects.IsClosed, ActiveCleared = projects.Active == null, SelectedClosed = selected?.IsClosed }));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
