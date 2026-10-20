using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Deliberately exercises the public App-owned commands and actual GL presentation in one window.
public static class EditorViewportProbe
{
    public static async Task<int> RunAsync(string directory, bool gpu = false)
    {
        Directory.CreateDirectory(directory);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(300)); var token = limit.Token;
        var factory = new FaultFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var gpuBinding = gpu ? new EditorGpuViewportBinding(runtime => factory.Inner.Leases.Single(l => l.ProcessId == runtime.ProcessId)) : null;
        var colors = new ConcurrentDictionary<string, uint>(); var firstFrames = new ConcurrentDictionary<ulong, FrameMetadata>();
        var cycles = new List<object>(); var failures = new List<object>(); var commands = new List<string>();
        AetherHost host = null!; ViewportView? view = null; Task uiCommand = Task.CompletedTask;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        void Dispatch(string kind)
        {
            if (kind == "stop") { uiCommand = host.Editor.StopAsync(); return; }
            if (kind == "cancel") { uiCommand = host.Editor.CancelAsync(); return; }
            if (!uiCommand.IsCompleted) return;
            uiCommand = Task.Run(async () =>
            {
                try
                {
                    switch (kind)
                    {
                        case "build": await host.Editor.BuildAsync(token); break;
                        case "replace": await host.Editor.ReplaceAsync(token); break;
                        case "start": await host.Editor.StartAsync(160, 90, token); break;
                        case "play": await host.Editor.PlayAsync(token); break;
                        case "pause": await host.Editor.PauseAsync(token); break;
                        case "step": await host.Editor.StepAsync(token); break;
                        case "refresh": host.Editor.SetContext(host.Editor.Status.Context!); break;
                    }
                }
                catch (Exception error) { Console.Error.WriteLine(error); }
            });
        }
        await using var app = App.Build(c =>
        {
            c.UseAether(o =>
            {
                o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory;
                o.RuntimeFactory = _ => factory; o.BuildTimeout = TimeSpan.FromSeconds(15);
                if (gpuBinding != null) o.ViewportTransport = gpuBinding.CreateTransport;
                o.ReplacementDeadlines = new() { Total = TimeSpan.FromSeconds(12), Forward = TimeSpan.FromSeconds(4), Cleanup = TimeSpan.FromSeconds(3) };
            });
            c.UsePresentation<ViewportView>(o =>
            {
                o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w =>
                { w.Width = 1000; w.Height = 760; w.Title = "TrueMoon ENG3d Editor proof"; w.EnableDiagnostics = true; w.FrameMode = UiFrameMode.Continuous; });
            });
            c.Services(r => r.Singleton<Action<string>>(_ => Dispatch).Transient<ViewportView>());
        });
        host = app.Services.Resolve<AetherHost>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!; var windows = app.Services.Resolve<HostedUiSessionFactory>()!;
        var lifetime = app.Services.Resolve<IAppLifetime>()!; var windowId = window.Id;
        var pixelChecks = 0; (ulong, ulong, ulong)? presented = null; Task shutdown = Task.CompletedTask;
        window.FramePresented += ui =>
        {
            var currentView = (ViewportView)ui.Root;
            Probe.Check(view == null || ReferenceEquals(view, currentView), "Editor root was recreated."); view = currentView;
            gpuBinding?.Observe(ui);
            if (view.Canvas.DrawnViewport is { } ticket && ReferenceEquals(host.Frames.Presentation, ticket.Presentation))
            {
                var surface = ticket.Presentation.Surface; var identity = surface.Identity;
                var key = (identity.Generation, surface.Generation, ticket.Metadata.FrameId);
                if (presented != key)
                {
                    presented = key;
                    using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    ViewportProbe.VerifyPixels(bitmap, view.Canvas.Bounds, ui.Viewport.Scale, surface.Width, surface.Height, ticket.Metadata, colors[identity.GameBuildId]);
                    pixelChecks++; firstFrames.TryAdd(identity.Generation, ticket.Metadata);
                    if (pixelChecks == 1) { using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var output = File.Create(Path.Combine(directory, "editor.png")); data.SaveTo(output); }
                    host.Frames.Presented(ticket); // Stop/resize may supersede the ticket during diagnostic readback.
                }
            }
            if (view.Canvas.Drawn is { } drawn && ReferenceEquals(host.Frames.Display, drawn.Display))
            {
                var identity = drawn.Display.Surface.Identity;
                var key = (identity.Generation, drawn.Display.Surface.Generation, drawn.Metadata.FrameId);
                if (presented != key)
                {
                    presented = key;
                    using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    ViewportProbe.VerifyPixels(bitmap, view.Canvas.Bounds, ui.Viewport.Scale, drawn.Display.Surface, drawn.Metadata, colors[identity.GameBuildId]);
                    pixelChecks++; firstFrames.TryAdd(identity.Generation, drawn.Metadata);
                    if (pixelChecks == 1)
                    { using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var output = File.Create(Path.Combine(directory, "editor.png")); data.SaveTo(output); }
                    host.Frames.Presented(drawn.Display, drawn.Metadata);
                }
            }
            var display = host.Frames.Display;
            using var frame = display?.Receiver.TakeLatest();
            if (frame != null && ReferenceEquals(host.Frames.Display, display)) { view.Canvas.Upload(display!, frame); window.RequestFrame(); }
            var status = host.Editor.Status; var runtime = host.Supervisor.Status;
            view.Status.Value = $"{status.Operation} | source {status.Context?.Build.SourceRevision} | {runtime.State} | tick {runtime.Tick}\n" +
                $"active {runtime.Identity?.GameBuildId[..8]} | candidate {status.Candidate?.Build.BuildId[..8]} | {status.Context?.Build.ProjectPath}\n" +
                (status.Error is GameReplacementException e ? $"{e.Phase}: {e.InnerException?.Message}; recovery: {e.RecoveryError?.Message}" : status.Error?.Message ?? host.Frames.TransportError?.Message ?? "Ready");
        };
        var passed = false; Exception? failure = null;
        try
        {
            await app.StartAsync(token);
            if (gpuBinding != null) await gpuBinding.InitializeAsync(window, host, token);
            window.Closing += async _ =>
            {
                shutdown = host.DisposeAsync().AsTask(); await shutdown;
                Probe.Check(view is { IsDisposed: false } && !view.Canvas.IsDisposed && host.Frames.MappingCount == 0 && host.Builds.ProcessId == null,
                    "Native close disposed UI before draining engine commands."); return true;
            };
            var source = File.ReadAllText(Path.Combine(CheckpointProbe.FindWorkspace(), "ManualTests/Game.Logic/ProbeGame.cs"));
            var inputA = Project(directory, "a", source, 7); var inputB = Project(directory, "b", source.Replace("0xff2080c0", "0xffc06020").Replace("X + 0.125f", "X + 0.25f"), 8);
            var a = await BuildAsync(inputA); var b = await BuildAsync(inputB); colors[a.BuildId] = 0xff2080c0; colors[b.BuildId] = 0xffc06020;
            async Task<GameBuildArtifact> BuildAsync(GameBuildRequest request)
            { host.Editor.SetContext(Context(request)); return await host.Editor.BuildAsync(token); }
            GameEditorContext Context(GameBuildRequest request) => new(request, authoring.Snapshot(), authoring.Selection);
            async Task ResetAsync(string behavior = "normal")
            {
                await host.Editor.StopAsync(); host.Editor.SetContext(Context(inputA)); await host.Editor.SelectBuildAsync(a, token);
                factory.Next.Enqueue(behavior); await host.Editor.StartAsync(160, 90, token);
                for (var i = 0; i < 5; i++) await host.Editor.StepAsync(token);
            }
            async Task SelectBAsync() { host.Editor.SetContext(Context(inputB)); await host.Editor.SelectBuildAsync(b, token); }
            async Task RecordFailureAsync(string kind, Func<Task> action, Action<Exception> validate)
            {
                Exception? error = null; try { await action(); } catch (Exception e) { error = e; }
                Probe.Check(error != null, kind + " unexpectedly succeeded."); validate(error!);
                failures.Add(new { Case = kind, Error = error!.ToString(), State = host.Supervisor.Status.State.ToString(), Children = factory.Inner.Leases.Count });
            }
            await ResetAsync(); var sessionId = host.Supervisor.Status.Identity!.SessionId;
            if (gpu)
            {
                var oldTicket = view!.Canvas.DrawnViewport!;
                await host.Frames.ResizeAsync(64, 32, token); Probe.Check(!host.Frames.Presented(oldTicket), "Resize accepted a stale GPU ticket.");
                await host.Frames.ResizeAsync(0, 0, token); Probe.Check(host.Frames.Presentation!.Surface.Suspended && host.Frames.MappingCount == 0, "GPU suspend retained a CPU mapping/active surface.");
                await host.Frames.ResizeAsync(160, 90, token); commands.Add("gpu-resize-suspend-stale-ticket");
            }
            for (var cycle = 0; cycle < 20; cycle++)
            {
                var build = cycle % 2 == 0 ? b : a; var input = cycle % 2 == 0 ? inputB : inputA; var playing = cycle % 4 < 2;
                host.Editor.SetContext(Context(input)); await host.Editor.SelectBuildAsync(build, token);
                if (playing) await host.Editor.PlayAsync(token); else await host.Editor.PauseAsync(token);
                var replacement = await host.Editor.ReplaceAsync(token); var identity = replacement.Identity;
                Probe.Check(host.Supervisor.Status.State == (playing ? SessionState.Playing : SessionState.Paused), "Previous mode lost.");
                var restored = JsonSerializer.Deserialize<GameCheckpointState>(File.ReadAllText(Path.Combine(directory, $"restored-{identity.Generation}.json")))!;
                var first = firstFrames[identity.Generation]; var loaded = Loaded(directory, identity.Generation, build);
                Probe.Check(restored == replacement.Checkpoint.State && first.Tick == restored.Tick && restored.Tick >= 5 && restored.Interaction,
                    "Runtime restore/first GPU frame did not retain complete nondefault state.");
                await host.Editor.PauseAsync(token); var state = (await host.Supervisor.CaptureCheckpointAsync(token)).State;
                await host.Editor.StepAsync(token); var after = (await host.Supervisor.CaptureCheckpointAsync(token)).State;
                var step = cycle % 2 == 0 ? .25f : .125f;
                Probe.Check(after.Tick == state.Tick + 1 && after.Camera == state.Camera with { X = state.Camera.X + step, Yaw = state.Camera.Yaw + .01f } && after.Interaction,
                    "Step did not execute selected DLL behavior after restore.");
                Probe.Check(ReferenceEquals(host, app.Services.Resolve<AetherHost>()) && window.Id == windowId && !lifetime.AppCancellationToken.IsCancellationRequested &&
                    identity.SessionId == sessionId && view is { IsDisposed: false } && JsonSerializer.Serialize(authoring) == before, "Replace recreated Editor/App/window or changed authoring.");
                cycles.Add(new { Cycle = cycle + 1, replacement.Playing, identity, replacement.Checkpoint, Restored = restored, First = first, BeforeStep = state, AfterStep = after, Loaded = loaded, Color = colors[build.BuildId] });
            }
            commands.Add("20-replace-one-app-window-paused-playing-gpu-dll-state-step");
            await ResetAsync(); var running = host.Supervisor.Status.Identity;
            var invalid = Project(directory, "invalid", "invalid C# compilation", 90);
            await RecordFailureAsync("compiler-error", async () => { await BuildAsync(invalid); }, e => Probe.Check(e is GameBuildException && host.Supervisor.Status.Identity == running, "Compile failure changed active runtime."));
            foreach (var kind in new[] { "cancel", "context", "stale-session", "stop", "deadline" })
            {
                var held = HeldProject(directory, kind); host.Editor.SetContext(Context(held.Request)); var pending = host.Editor.BuildAsync(token);
                await Records.WaitAsync(held.Marker); var compiler = host.Builds.ProcessId!.Value; var worker = int.Parse(File.ReadAllText(held.Marker));
                if (kind == "cancel") await host.Editor.CancelAsync();
                if (kind == "context") host.Editor.SetContext(Context(inputB));
                if (kind == "stale-session")
                {
                    await host.Frames.StopAsync(); await host.Frames.StartAsync(authoring.Snapshot(), a, 160, 90, token: token);
                    File.WriteAllText(held.Release, "release");
                }
                if (kind == "stop") await host.Editor.StopAsync();
                await RecordFailureAsync("build-" + kind, () => pending, e => Probe.Check((kind == "deadline" ? e is TimeoutException : e is OperationCanceledException) &&
                    host.Editor.Status.Candidate == null && host.Builds.ProcessId == null && BuildProbe.HasExited(compiler) && BuildProbe.HasExited(worker), "Build interrupt/stale completion leaked or selected an artifact."));
                await ResetAsync();
            }
            foreach (var kind in new[] { "missing", "tampered" })
            {
                var copy = GameBuildArtifact.Publish(b.DirectoryPath, Path.Combine(directory, "copy-" + kind), BuildIdentity.HostId, b.EntryAssembly, b.EntryType, 8);
                host.Editor.SetContext(Context(inputB)); await host.Editor.SelectBuildAsync(copy, token); var old = host.Supervisor.Status.Identity; var count = factory.Inner.Leases.Count;
                if (kind == "missing") File.Delete(Path.Combine(copy.DirectoryPath, copy.EntryAssembly)); else File.AppendAllText(Path.Combine(copy.DirectoryPath, copy.EntryAssembly), "tampered");
                await RecordFailureAsync(kind, () => host.Editor.ReplaceAsync(token), e => Probe.Check(e is GameReplacementException && host.Supervisor.Status.Identity == old && factory.Inner.Leases.Count == count, "Invalid artifact touched the old child."));
            }
            foreach (var behavior in new[] { "capture-reject", "capture-timeout" })
            {
                await ResetAsync(behavior); await SelectBAsync(); var old = host.Supervisor.Status.Identity; var count = factory.Inner.Leases.Count;
                await RecordFailureAsync(behavior, () => host.Editor.ReplaceAsync(token), e => Probe.Check(e is GameReplacementException { RollbackAttempted: false } && factory.Inner.Leases.Count == count &&
                    host.Supervisor.Status.State == (behavior == "capture-reject" ? SessionState.Paused : SessionState.Faulted) && host.Supervisor.Status.Identity == old, "Capture outcome was misclassified."));
            }
            foreach (var behavior in new[] { "bad-hello", "restore-reject", "no-frames", "double-restore-reject" })
            {
                await ResetAsync(); await SelectBAsync(); var count = factory.Inner.Leases.Count;
                factory.Next.Enqueue(behavior == "double-restore-reject" ? "restore-reject" : behavior);
                factory.Next.Enqueue(behavior == "double-restore-reject" ? "restore-reject" : "normal");
                await RecordFailureAsync(behavior, () => host.Editor.ReplaceAsync(token), e =>
                {
                    var error = e as GameReplacementException;
                    Probe.Check(error is { RollbackAttempted: true } && error.RollbackSucceeded == (behavior != "double-restore-reject") &&
                        (behavior == "double-restore-reject" ? error.RecoveryError != null : error.RecoveryError == null) && ReferenceEquals(host.Editor.Status.Error, error) &&
                        factory.Inner.Leases.Count == count + 2, "Candidate/rollback errors or single-attempt recovery were lost.");
                });
            }
            foreach (var kind in new[] { "cancel", "context", "stop" })
            {
                await ResetAsync(); await SelectBAsync(); factory.Next.Enqueue("restore-held"); var generation = host.Supervisor.Status.Identity!.Generation + 1;
                var replacing = host.Editor.ReplaceAsync(token); await Records.WaitAsync(Path.Combine(directory, $"restore-held-{generation}.json")); var count = factory.Inner.Leases.Count;
                if (kind == "cancel") await host.Editor.CancelAsync();
                if (kind == "context") host.Editor.SetContext(Context(inputA));
                if (kind == "stop") await host.Editor.StopAsync();
                await RecordFailureAsync("replace-" + kind, () => replacing, e => Probe.Check(e is GameReplacementException re && re.RollbackSucceeded == (kind != "stop") &&
                    factory.Inner.Leases.Count == count + (kind == "stop" ? 0 : 1) && host.Editor.Status.Candidate == null, "Replace interrupt applied a late candidate or launched unwanted rollback."));
            }
            await ResetAsync(); await SelectBAsync(); factory.Last!.FailNextStop = true;
            await RecordFailureAsync("injected-owned-lease-stop-failure", () => host.Editor.ReplaceAsync(token), e => Probe.Check(e is GameReplacementException { RollbackAttempted: false } &&
                host.Supervisor.Status.State == SessionState.CleanupFailed && !factory.Last.ResourcesReleased, "Failed reap lost ownership."));
            await host.Editor.StopAsync(); Probe.Check(factory.Last.ResourcesReleased, "Explicit cleanup retry did not reap owned child.");
            commands.Add("cleanup-retry-after-injected-lease-failure");
            await ResetAsync(); await SelectBAsync(); factory.Next.Enqueue("restore-held"); var closingGeneration = host.Supervisor.Status.Identity!.Generation + 1;
            var closingReplace = host.Editor.ReplaceAsync(token); await Records.WaitAsync(Path.Combine(directory, $"restore-held-{closingGeneration}.json"));
            var closingBuildInput = HeldProject(directory, "close"); var closingBuild = host.Builds.BuildAsync(closingBuildInput.Request, token);
            await Records.WaitAsync(closingBuildInput.Marker); var closingCompiler = host.Builds.ProcessId!.Value; var closingWorker = int.Parse(File.ReadAllText(closingBuildInput.Marker));
            var closingChildren = factory.Inner.Leases.Count;
            using (var process = Process.GetCurrentProcess()) { process.Refresh(); Probe.Check(process.CloseMainWindow(), "Native Editor close request failed."); }
            await window.Completion.WaitAsync(token); await shutdown;
            await RecordFailureAsync("close-replace", () => closingReplace, e => Probe.Check(e is GameReplacementException { RollbackAttempted: false } && factory.Inner.Leases.Count == closingChildren, "Close launched rollback."));
            await RecordFailureAsync("close-build", () => closingBuild, e => Probe.Check(e is OperationCanceledException && BuildProbe.HasExited(closingCompiler) && BuildProbe.HasExited(closingWorker), "Close retained compiler tree."));
            commands.Add("native-close-drains-replace-and-build-before-ui"); await app.StopAsync(token);
            Probe.Check(host.Editor.Status.Closed && factory.Inner.Leases.All(l => l.Reaped && l.ResourcesReleased) && host.Frames.MappingCount == 0 && windows.ActiveSessionCount == 0 &&
                view is { IsDisposed: true } && view.Canvas.IsDisposed && window.Diagnostics!.Snapshot.Present.Failed == 0 && factory.MaxLive == 1 &&
                JsonSerializer.Serialize(authoring) == before && !AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == "Game.Logic"), "Final Editor ownership/authoring check failed.");
            passed = true;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
        finally
        {
            await host.Editor.StopAsync(); window.Close(); await app.StopAsync();
            Records.Write(directory, "editor-report.json", new
            {
                Completed = passed, Failure = failure?.ToString(), Cycles = cycles, Failures = failures, Commands = commands, PixelChecks = pixelChecks,
                WindowId = windowId, Presented = window.PresentedFrames, Diagnostics = window.Diagnostics?.Snapshot, MaxLive = factory.MaxLive,
                Children = factory.Inner.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }),
                ChildrenReleased = factory.Inner.Leases.All(l => l.Reaped && l.ResourcesReleased), Mappings = host.Frames.MappingCount, ActiveUiSessions = windows.ActiveSessionCount,
                EditorPreserved = before == JsonSerializer.Serialize(authoring), GameLoadedInEditor = AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == "Game.Logic"),
                Transport = host.Frames.TransportKind.ToString(), GpuCopies = gpuBinding?.Copies ?? 0, ImportReadbacks = gpuBinding?.ImportReadbacks ?? 0, GpuResources = host.Gpu.ResourceCount, GpuHandles = host.Gpu.OpenHandleCount,
                InputHashes = Directory.EnumerateFiles(directory, "Game.cs", SearchOption.AllDirectories).Select(p => new { Path = p, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) })
            });
        }
        return passed ? 0 : 1;
    }

    private static GameBuildRequest Project(string directory, string name, string source, long revision, string target = "")
    {
        var input = Path.Combine(directory, "input-" + name); Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "Game.cs"), source); File.WriteAllText(Path.Combine(input, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        var project = Path.Combine(input, "Game.Logic.csproj");
        File.WriteAllText(project, $$"""
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
            <ItemGroup><Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference></ItemGroup>{{target}}</Project>
            """);
        return new(project, "Game.Logic.dll", "Game.Logic.ProbeGame", revision);
    }
    private static (GameBuildRequest Request, string Marker, string Release) HeldProject(string directory, string name)
    {
        var input = Path.Combine(directory, "input-held-" + name); Directory.CreateDirectory(input);
        var marker = Path.Combine(input, "entered"); var release = Path.Combine(input, "release"); var script = Path.Combine(input, "hold.ps1");
        File.WriteAllText(script, "param($marker,$release)\n[IO.File]::WriteAllText($marker+'.tmp',[Environment]::ProcessId.ToString())\n[IO.File]::Move($marker+'.tmp',$marker)\nwhile (!(Test-Path -LiteralPath $release)) { Start-Sleep -Milliseconds 20 }\n");
        var command = SecurityElement.Escape($"pwsh -NoProfile -NonInteractive -File \"{script}\" \"{marker}\" \"{release}\"");
        var target = $"<Target Name=\"HoldBuild\" BeforeTargets=\"CoreCompile\"><Exec Command=\"{command}\" /></Target>";
        var source = File.ReadAllText(Path.Combine(CheckpointProbe.FindWorkspace(), "ManualTests/Game.Logic/ProbeGame.cs"));
        return (Project(directory, "held-" + name, source, 99, target), marker, release);
    }
    private static JsonElement Loaded(string directory, ulong generation, GameBuildArtifact build)
    {
        var loaded = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"loaded-{generation}.json")));
        Probe.Check(loaded.GetProperty("BuildId").GetString() == build.BuildId && loaded.GetProperty("Path").GetString() == Path.Combine(build.DirectoryPath, build.EntryAssembly) &&
            loaded.GetProperty("Sha256").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(build.DirectoryPath, build.EntryAssembly)))), "Native loaded DLL hash/path mismatch."); return loaded;
    }
    private sealed class FaultFactory : IRuntimeFactory
    {
        internal NativeRuntimeFactory Inner { get; } = new(); internal ConcurrentQueue<string> Next { get; } = new();
        internal FaultLease? Last; internal int MaxLive;
        public IRuntimeLease Create(SessionLaunch launch)
        {
            if (Next.TryDequeue(out var behavior)) launch = launch with { Behavior = behavior };
            Last = new(Inner.Create(launch)); MaxLive = Math.Max(MaxLive, Inner.Leases.Count(l => !l.ResourcesReleased)); return Last;
        }
    }
    private sealed class FaultLease(IRuntimeLease inner) : IRuntimeLease
    {
        internal bool FailNextStop;
        public int ProcessId => inner.ProcessId; public Task Exited => inner.Exited; public bool ResourcesReleased => inner.ResourcesReleased;
        public Task LaunchAsync(CancellationToken token) => inner.LaunchAsync(token);
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => inner.SendAsync(request, token);
        public Task StopAsync(CancellationToken gracefulDeadline = default)
        { if (FailNextStop) { FailNextStop = false; throw new IOException("Injected owned native lease Stop failure before reap; explicit retry required."); } return inner.StopAsync(gracefulDeadline); }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
