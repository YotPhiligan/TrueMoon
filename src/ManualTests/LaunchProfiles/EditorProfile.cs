using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.LaunchProfiles;

internal static class EditorProfile
{
    internal static async Task RunAsync(AetherLaunchOptions options, bool microsoft)
    {
        var directory = options.ReportPath ?? throw new ArgumentException("Select --report.");
        // The runner writes this bounded test input before launching the consumer.
        var artifactPath = (await File.ReadAllTextAsync(Path.Combine(directory, "artifact.txt"))).Trim();
        var launch = options.RuntimeHostPath is { } executable
            ? new NativeRuntimeLaunch(executable, Path.ChangeExtension(executable, ".dll"), ["--runtime"])
            : NativeRuntimeLaunch.CurrentProcess("--runtime");
        var hostId = BuildIdentity.ForRuntime(launch.RuntimeAssemblyPath);
        var artifact = GameBuildArtifact.Open(artifactPath, hostId);
        var checks = new List<string>(); var cycles = new List<object>(); var faults = new List<object>();
        var validations = new List<JsonElement>();
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        await using var app = App.Builder(configuration => { if (microsoft) configuration.UseDI(); }).Setup(context =>
        {
            context.UseAetherEditor(engine =>
            {
                engine.RuntimeLaunch = launch; engine.HostContractId = hostId; engine.GameBuildId = BuildIdentity.PreviewId;
                engine.ReportDirectory = directory;
            }, editor => editor.ProjectPath = options.ProjectPath ?? throw new ArgumentException("Select --project."));
            context.UseAetherEditorWindow(fonts);
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        var factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var codec = app.Services.Resolve<SceneFileCodec>()!;
        SceneDocumentSession? authoring = null;
        SceneAuthoringState? baseline = null; byte[] savedBytes = [];
        var definition = new EntityDefinition(new(Guid.NewGuid()), new EntityPatch());
        var firstEntity = new EntityInstance(new(Guid.NewGuid()), definition.Id);
        var firstFrame = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Presented(UiSession ui) => firstFrame.TrySetResult(ui);
        window.FramePresented += Presented;
        Exception? failure = null; EditorView? view = null; object? pixelProof = null;
        try
        {
            Require(!GameLoaded(), "game-not-loaded-before-editor-start");
            Require(!app.Services.ResolveAll<PlayerSession>().Any(), "editor-has-no-local-player-graph");
            await app.StartAsync(token);
            var ui = await firstFrame.Task.WaitAsync(token);
            await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(token);
            authoring = app.Services.Resolve<EditorProjects>()!.Active!.Documents!;
            Require(await authoring.NewAsync(new(new(Guid.NewGuid()), 41, [], [], []), cancellationToken: token), "new-project-owned-document");
            Require(authoring.EditEntities(new CreateEntitiesBatch(Operation(), [firstEntity], [definition])).Succeeded, "first-authoring-edit");
            Require(authoring.EditEntities(new CreateEntitiesBatch(Operation(), [new(new(Guid.NewGuid()), definition.Id)])).Succeeded, "second-authoring-edit");
            Require(authoring.Undo(Operation()).Succeeded, "prepare-undo-redo-history");
            baseline = authoring.Read().Content!; savedBytes = codec.Encode(baseline.Document);
            var saved = await authoring.SaveAsAsync("authoring.aetherscene", token); var scenePath = saved.Path;
            await Wait(() => ReferenceEquals(workspace.Document, baseline));
            await ui.InvokeAsync(() =>
            {
                view = EditorMainWindow.FromRoot(ui.Root).EditorView;
                workspace.Select(firstEntity.Id.Value.ToString("D"));
            }).WaitAsync(token);
            var windowId = window.Id;
            engine.Editor.SetContext(new(new(Path.Combine(directory, "not-built.csproj"), artifact.EntryAssembly,
                artifact.EntryType, artifact.SourceRevision), new("launch-profile", baseline.Document.Revision, []), firstEntity.Id.Value.ToString("D")));
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await engine.Editor.SelectBuildAsync(artifact, token);
                await engine.Editor.StartBuildAsync(640, 360, engine.Editor.Status.ContextVersion, token);
                Require(engine.Supervisor.Status.SceneLoaded && engine.Frames.Presentation?.FirstPresentedFrame != null, $"cycle-{cycle}-presentation-fence");
                await engine.Editor.PlayAsync(token);
                await Wait(() => engine.Supervisor.Status.Tick > 1);
                await engine.Editor.PauseAsync(token);
                var tick = engine.Supervisor.Status.Tick;
                await engine.Editor.StepAsync(token);
                Require(engine.Supervisor.Status.Tick == tick + 1, $"cycle-{cycle}-one-paused-step");
                if (cycle == 0) pixelProof = await Capture(ui, view!);
                var pid = engine.Supervisor.Status.ProcessId!.Value;
                Require(pid != Environment.ProcessId && !GameLoaded(), $"cycle-{cycle}-child-only-game-load");
                var identity = engine.Supervisor.Status.Identity!;
                if (cycle == 1)
                {
                    // This PID comes from our live lease. The parent and its document remain alive.
                    using var child = Process.GetProcessById(pid);
                    child.Kill(); await child.WaitForExitAsync(token);
                    await Wait(() => engine.Supervisor.Status.State == SessionState.Faulted);
                    Require(!app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested, "crash-keeps-editor-app-alive");
                }
                await engine.Editor.StopAsync();
                Require(factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased) && engine.Frames.MappingCount == 0 &&
                    engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0, $"cycle-{cycle}-reap-and-release");
                if (cycle != 1)
                {
                    var completion = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(directory, $"runtime-{identity.Generation}.json"), token));
                    var validation = completion.GetProperty("Validation");
                    Require(completion.GetProperty("GameLoaded").GetBoolean() && !completion.GetProperty("EditorLoaded").GetBoolean() &&
                        (!completion.TryGetProperty("ToolingLoaded", out var tooling) || !tooling.GetBoolean()) &&
                        completion.GetProperty("Resources").GetInt32() == 0 && completion.GetProperty("Handles").GetInt32() == 0,
                        $"cycle-{cycle}-runtime-only-graph-and-cleanup");
                    Require(validation.GetProperty("ValidationEnabled").GetBoolean() && validation.GetProperty("Errors").GetInt32() == 0 &&
                        validation.GetProperty("Warnings").GetInt32() == 0 && validation.GetProperty("LiveSlots").GetInt32() == 0 &&
                        validation.GetProperty("OpenHandles").GetInt32() == 0 &&
                        validation.GetProperty("SurfacesCreated").GetInt32() == validation.GetProperty("SurfacesReleased").GetInt32() &&
                        validation.GetProperty("DevicesCreated").GetInt32() == validation.GetProperty("DevicesReleased").GetInt32(), $"cycle-{cycle}-native-validation");
                    validations.Add(validation);
                }
                Require(window.Id == windowId && !window.Completion.IsCompleted && view is { IsDisposed: false }, $"cycle-{cycle}-retained-editor-window");
                Require(ReferenceEquals(workspace.Document?.Document, baseline.Document) && workspace.SelectedId == firstEntity.Id.Value.ToString("D"), $"cycle-{cycle}-retained-authoring-selection");
                Require(ReferenceEquals(authoring.Read().Content, baseline) && authoring.Read().Content!.UndoCount == 1 && authoring.Read().Content!.RedoCount == 1 &&
                    codec.Encode(authoring.Read().Content!.Document).AsSpan().SequenceEqual(savedBytes) &&
                    (await File.ReadAllBytesAsync(scenePath, token)).AsSpan().SequenceEqual(savedBytes), $"cycle-{cycle}-document-and-history-preserved");
                cycles.Add(new { Cycle = cycle, ProcessId = pid, identity.Generation, Tick = tick, Crash = cycle == 1,
                    Reaped = true, ResourcesReleased = true, DocumentPreserved = true });
            }
            Require(cycles.Count == 3 && factory.Leases.Select(lease => lease.ProcessId).Distinct().Count() == 3, "fresh-child-after-crash");
            faults.Add(await IdentityFaults.RejectAsync(launch, hostId, directory, false, token));
            faults.Add(await IdentityFaults.RejectAsync(launch, hostId, directory, true, token));
            Require(authoring.Redo(Operation()).Succeeded && authoring.Read().Content!.Document.Entities.Length == 2, "redo-survives-session-crash");
            Require(authoring.Undo(Operation()).Succeeded && authoring.Read().Content!.Document.Entities.Length == 1, "undo-survives-session-crash");
            Require(!GameLoaded() && engine.Builds.ProcessId == null, "editor-never-loads-or-compiles-selected-game");
            await authoring.CloseAsync(DocumentCloseChoice.Discard, cancellationToken: token);
            window.Close(); await window.Completion.WaitAsync(token); await app.StopAsync(token);
            Require(factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased), "final-owned-children-reaped");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            window.FramePresented -= Presented;
            try { await engine.Editor.StopAsync(); if (authoring != null) await authoring.CloseAsync(DocumentCloseChoice.Discard); window.Close(); await window.Completion.WaitAsync(TimeSpan.FromSeconds(20)); await app.StopAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            await File.WriteAllTextAsync(Path.Combine(directory, "editor.json"), JsonSerializer.Serialize(new
            {
                Passed = failure == null, Provider = microsoft ? "Microsoft" : "Cobalt", Pid = Environment.ProcessId,
                Counterpart = launch.RuntimeAssemblyPath, SameExecutable = options.RuntimeHostPath == null, HostId = hostId,
                artifact.BuildId, artifact.EntryType, GameLoadedInParent = GameLoaded(), Checks = checks, Cycles = cycles,
                IdentityFaults = faults, Validations = validations, PixelProof = pixelProof, DocumentSha256 = Convert.ToHexString(SHA256.HashData(savedBytes)),
                UndoCount = baseline?.UndoCount, RedoCount = baseline?.RedoCount,
                Children = factory.Leases.Select(lease => new { lease.ProcessId, lease.Reaped, lease.ResourcesReleased }).ToArray(),
                Error = failure?.ToString()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) throw failure;
        return;

        AuthoringOperation Operation() => new(Guid.NewGuid(), authoring!.Read().Content!.Document.Revision, "launch-profile");
        void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        async Task Wait(Func<bool> condition)
        { while (!condition()) { token.ThrowIfCancellationRequested(); await Task.Delay(20, token); } }
        async Task<object> Capture(UiSession ui, EditorView currentView)
        {
            var done = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Readback(UiSession current)
            {
                if (currentView.Canvas.DrawnFrame is not { } drawn || !ReferenceEquals(drawn.Presentation, engine.Frames.Presentation)) return;
                try
                {
                    using var image = current.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    var bounds = currentView.Canvas.ImageBounds; var scale = current.Viewport.Scale; var white = 0;
                    for (var y = (int)(bounds.Y * scale); y < (int)((bounds.Y + bounds.Height) * scale); y++)
                        for (var x = (int)(bounds.X * scale); x < (int)((bounds.X + bounds.Width) * scale); x++)
                        { var color = bitmap.GetPixel(x, y); if (color.Red > 240 && color.Green > 240 && color.Blue > 240) white++; }
                    Require(white > 200, "actual-native-mesh-pixels");
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, "editor.png")); data.SaveTo(output);
                    done.TrySetResult(new { WhitePixels = white, engine.Supervisor.Status.Tick });
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Readback; window.RequestFrame();
            try { return await done.Task.WaitAsync(token); }
            finally { window.FramePresented -= Readback; }
        }
    }

    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "RenderSurface.Game");
}
