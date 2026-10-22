using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Alloy.Platform.Silk;
using System.Reflection;
using NativeKey = Silk.NET.Input.Key;

namespace EngineSessionProbe;

internal static class SceneInputProbe
{
    internal static async Task<int> RunAsync(string? reportDirectory, bool room = false)
    {
        var proof = reportDirectory != null;
        var directory = reportDirectory ?? Path.Combine(Path.GetTempPath(), "tm-scene-input-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(directory)) throw new IOException("Use a fresh input proof directory.");
        Directory.CreateDirectory(directory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(proof ? 3 : 60)); var token = deadline.Token;
        using var inputEnd = CancellationTokenSource.CreateLinkedTokenSource(token);
        var factory = new InputFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var binding = new EditorGpuViewportBinding(runtime => (NativeRuntimeLease)runtime);
        var cases = new List<string>(); var captures = new List<object>(); var snapshots = new List<object>();
        AetherHost host = null!; ViewportView view = null!; SceneViewportInput input = null!;
        Task command = Task.CompletedTask, sender = Task.CompletedTask; string? commandError = null;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        void Dispatch(string kind)
        {
            if (!command.IsCompleted && kind is not ("stop" or "cancel")) return;
            if (kind is "start" or "stop" or "pause" or "play" or "replace") input?.Cancel();
            command = Task.Run(async () =>
            {
                try
                {
                    switch (kind)
                    {
                        case "stop": await host.Editor.StopAsync(); break;
                        case "cancel": await host.Editor.CancelAsync(); break;
                        case "start": if (host.Editor.Status.Candidate == null) await host.Editor.BuildAsync(token); await host.Editor.StartAsync(640, 360, token); break;
                        case "build": await host.Editor.BuildAsync(token); break;
                        case "replace": await host.Editor.ReplaceAsync(token); break;
                        case "play": await host.Editor.PlayAsync(token); break;
                        case "pause": await host.Editor.PauseAsync(token); break;
                        case "step": await host.Editor.StepAsync(token); break;
                        case "refresh": var context = host.Editor.Status.Context!; host.Editor.SetContext(context with { Build = context.Build with { SourceRevision = context.Build.SourceRevision + 1 } }); break;
                    }
                    commandError = null;
                }
                catch (Exception error) { commandError = error.Message; }
            });
        }
        await using var app = App.Build(c =>
        {
            c.UseAether(o => { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; o.ViewportTransport = binding.CreateTransport; });
            c.UsePresentation<ViewportView>(o =>
            {
                o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w => { w.Width = 1000; w.Height = 760; w.Title = room ? "TrueMoon ENG5c — Room, WASD/mouse, Escape" : "TrueMoon ENG5b — Play, click viewport, WASD/mouse/E, Escape"; w.FrameMode = UiFrameMode.Continuous; });
            });
            c.Services(r => r.Transient<ViewportView>(_ => { var result = new ViewportView(Dispatch); result.Canvas.PreserveAspectRatio = true; return result; }));
        });
        host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!; var windowId = window.Id;
        UiSession ui = null!; var owner = 0; Pending? pending = null; Exception? failure = null; var passed = false;
        SilkWindowHost nativeWindow = null!; var rawKeys = 0;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += session =>
        {
            ui = session; owner = Environment.CurrentManagedThreadId; view = (ViewportView)session.Root; binding.Observe(session);
            shown.TrySetResult();
            if (view.Canvas.DrawnViewport is { } frame && ReferenceEquals(host.Frames.Presentation, frame.Presentation))
            {
                host.Frames.Presented(frame);
                var check = Volatile.Read(ref pending); var surface = frame.Presentation.Surface;
                if (check != null && frame.Metadata.Tick == check.State.Tick && surface.Identity.Generation == check.Generation && surface.Width == check.Width &&
                    Interlocked.CompareExchange(ref pending, null, check) == check)
                {
                    try
                    {
                        using var image = session.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                        var bounds = view.Canvas.ImageBounds; var scale = session.Viewport.Scale;
                        SKColor At(float x, float y) => bitmap.GetPixel((int)((bounds.X + bounds.Width * x) * scale), (int)((bounds.Y + bounds.Height * y) * scale));
                        var center = At(.5f, .5f); var background = At(.02f, .02f);
                        Probe.Check(Math.Abs(center.Red - (byte)(check.Color >> 16)) <= 2 && Math.Abs(center.Green - (byte)(check.Color >> 8)) <= 2 && Math.Abs(center.Blue - (byte)check.Color) <= 2 && center.Alpha == 255,
                            "Input game material/depth pixel mismatch.");
                        Probe.Check(Math.Abs(background.Red - 10) <= 2 && Math.Abs(background.Green - 15) <= 2 && Math.Abs(background.Blue - 26) <= 2 && background.Alpha == 255 &&
                            Math.Abs(bounds.Width / bounds.Height - (double)surface.Width / surface.Height) < .001, $"Input game clear/aspect mismatch: {background}, {bounds}, {surface.Width}x{surface.Height}.");
                        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100); var fileName = check.Name + ".png";
                        using (var file = File.Create(Path.Combine(directory, fileName))) encoded.SaveTo(file);
                        captures.Add(new { check.Name, check.State, check.Generation, Frame = frame.Metadata, Surface = surface, Image = fileName, Sha256 = Convert.ToHexString(SHA256.HashData(encoded.ToArray())) });
                        check.Completion.TrySetResult();
                    }
                    catch (Exception error) { check.Completion.TrySetException(error); }
                }
            }
            // Update only after diagnostic readback of the just-presented output.
            view.Status.Value = $"{host.Supervisor.Status.State} | tick {host.Supervisor.Status.Tick} | input {(input?.Captured == true ? "captured" : "Editor")} | {commandError ?? input?.Error?.Message ?? host.Supervisor.Status.Failure?.Cause.Message ?? "Play, click image, WASD / mouse / E; Escape returns input"}";
        };
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == owner) { action(); return Task.CompletedTask; }
            var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(w => { try { nativeWindow = w; action(); result.TrySetResult(); } catch (Exception error) { result.TrySetException(error); } }); return result.Task;
        }
        // Probe only: exercise the actual Silk -> UiKey -> hosted UiSession callback without sending OS keystrokes.
        void Key(NativeKey key, bool down)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var devices = (Silk.NET.Input.IInputContext)(typeof(SilkWindowHost).GetField("_input", flags)?.GetValue(nativeWindow) ?? throw new MissingFieldException("Silk input context"));
            var callback = typeof(SilkWindowHost).GetMethod("KeyInput", flags) ?? throw new MissingMethodException("Silk key callback");
            callback.Invoke(nativeWindow, [devices.Keyboards[0], key, down ? InputKind.KeyDown : InputKind.KeyUp]); rawKeys++;
        }
        async Task DrainInput()
        {
            inputEnd.Cancel(); await sender;
            if (input != null)
            {
                await Owner(() => input.Cancel());
                using var end = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await input.FlushAsync(end.Token);
            }
        }
        void Case(string name, bool condition) { Probe.Check(condition, name); cases.Add(name); }
        async Task<GameCheckpointState> Snapshot(string name)
        { var state = (await host.Supervisor.CaptureCheckpointAsync(token)).State; snapshots.Add(new { Name = name, State = state }); return state; }
        async Task Capture(string name, GameCheckpointState state, uint color, int width = 640)
        {
            var check = new Pending(name, state, host.Supervisor.Status.Identity!.Generation, color, width);
            await Owner(() => { Volatile.Write(ref pending, check); window.RequestFrame(); }); await check.Completion.Task.WaitAsync(token);
        }
        async Task Click()
        {
            await Owner(() =>
            {
                var bounds = view.Canvas.ImageBounds; var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
                var result = ui.HandleInput(new(InputKind.PointerDown, x, y)); ui.HandleInput(new(InputKind.PointerUp, x, y));
                Probe.Check(result.Handled && result.PointerCaptured && result.KeyboardFocused && input.Captured, "Viewport did not capture routed input.");
            });
        }
        try
        {
            await app.StartAsync(token); await binding.InitializeAsync(window, host, token);
            await shown.Task.WaitAsync(token);
            await Owner(() => { input = new(host, window, view); view.Canvas.GameplayInput = input; }); sender = input.RunAsync(inputEnd.Token);
            window.Closing += async _ => { await DrainInput(); await host.DisposeAsync(); Probe.Check(!view.IsDisposed && host.Gpu.ResourceCount == 0, "Input/UI closed before GPU drain."); return true; };
            var source = File.ReadAllText(Path.Combine(CheckpointProbe.FindWorkspace(), "ManualTests/Game.Logic/CameraGame.cs"));
            var a = proof ? Project("a", source, 1) : new GameBuildRequest(Path.Combine(CheckpointProbe.FindWorkspace(), "ManualTests/Game.Logic/Game.Logic.csproj"), "Game.Logic.dll", room ? "Game.Logic.RoomGame" : "Game.Logic.CameraGame", 1);
            GameEditorContext Context(GameBuildRequest build) => new(build, room ? RoomScene.Snapshot() : authoring.Snapshot(), authoring.Selection);
            host.Editor.SetContext(Context(a)); await host.Editor.BuildAsync(token); await host.Editor.StartAsync(640, 360, token);
            if (!proof) { await window.Completion.WaitAsync(token); passed = true; }
            else
            {
                var initial = await Snapshot("initial"); await Capture("01-initial", initial, 0xff2080c0);
                await Owner(() => { var b = view.Canvas.Bounds; ui.HandleInput(new(InputKind.PointerDown, b.X + 1, b.Y + b.Height / 2)); });
                Case("letterbox-does-not-capture", !input.Captured);
                await Click();
                await Owner(() =>
                {
                    Key(NativeKey.D, true);
                    Key(NativeKey.E, true); Key(NativeKey.E, true); Key(NativeKey.E, false);
                    var b = view.Canvas.ImageBounds; ui.HandleInput(new(InputKind.PointerMove, b.X + b.Width / 2 + 12, b.Y + b.Height / 2 - 6));
                });
                await input.FlushAsync(token); Case("input-ack-does-not-tick-paused-game", await Snapshot("staged") == initial);
                await host.Editor.StepAsync(token); var moved = await Snapshot("moved");
                Case("fixed-tick-wasd-look-and-quick-E", moved.Tick == 1 && moved.Camera.X > .049f && Math.Abs(moved.Camera.Yaw + .03f) < .0001f && Math.Abs(moved.Camera.Pitch - .015f) < .0001f && moved.Interaction);
                await Capture("02-moved-interacted", moved, 0xffc06020);
                await host.Editor.StepAsync(token); var second = await Snapshot("second");
                Case("held-movement-look-and-E-not-repeated", second.Camera.X > moved.Camera.X + .049f && second.Camera.Yaw == moved.Camera.Yaw && second.Interaction);
                await Owner(() => { Key(NativeKey.Escape, true); Probe.Check(!input.Captured, "Escape retained capture."); });
                await input.FlushAsync(token); await host.Editor.StepAsync(token); var escaped = await Snapshot("escaped");
                Case("escape-neutral-and-editor-focus", !input.Captured && escaped.Camera == second.Camera && view.PauseButton.IsFocused);
                await Click(); await Owner(() => { Key(NativeKey.W, true); Key(NativeKey.S, true); Key(NativeKey.A, true); Key(NativeKey.D, true); });
                await input.FlushAsync(token); await host.Editor.StepAsync(token);
                Case("silk-WASD-mapping-opposite-keys-cancel", (await Snapshot("opposite-keys")).Camera == escaped.Camera);
                await Owner(() => { Key(NativeKey.W, false); Key(NativeKey.S, false); Key(NativeKey.A, false); Key(NativeKey.D, false); Key(NativeKey.Escape, true); }); await input.FlushAsync(token);
                await Owner(() => Key(NativeKey.W, true)); await input.FlushAsync(token); await host.Editor.StepAsync(token);
                Case("editor-keys-do-not-move-camera", (await Snapshot("editor-key")).Camera == escaped.Camera);
                await Click(); await Owner(() => Key(NativeKey.W, true)); await input.FlushAsync(token);
                await Owner(() => ui.HandleInput(new(InputKind.FocusLost)));
                await input.FlushAsync(token); await host.Editor.StepAsync(token); var unfocused = await Snapshot("focus-lost");
                Probe.Check(!input.Captured && unfocused.Camera == escaped.Camera, "Focus loss retained staged input.");
                await Click(); await Owner(() => Key(NativeKey.D, true)); await input.FlushAsync(token);
                await Owner(() => ui.Capture(view.PauseButton)); await input.FlushAsync(token); await host.Editor.StepAsync(token);
                var captureLost = await Snapshot("capture-lost");
                Case("focus-and-capture-loss-clear-staged-held", !input.Captured && captureLost.Camera == escaped.Camera);
                await Owner(() => ui.ReleaseCapture(view.PauseButton));
                await Click(); await Owner(() => Key(NativeKey.D, true)); await input.FlushAsync(token);
                await host.Frames.ResizeAsync(800, 450, token); await input.FlushAsync(token); await Owner(() => { }); await host.Editor.StepAsync(token);
                var resized = await Snapshot("resized"); Probe.Check(!input.Captured && resized.Camera == captureLost.Camera, "Resize retained staged input.");
                await Capture("03-resized", resized, 0xffc06020, 800);
                await Click(); await Owner(() => Key(NativeKey.W, true)); await input.FlushAsync(token);
                await host.Frames.ResizeAsync(0, 0, token); await input.FlushAsync(token); await Owner(() => { });
                Probe.Check(!input.Captured, "Suspended viewport retained capture."); await host.Frames.ResizeAsync(320, 240, token); await host.Editor.StepAsync(token);
                Case("resize-and-suspend-clear-input", (await Snapshot("resumed")).Camera == resized.Camera);
                await Click(); await Owner(() => { Key(NativeKey.W, true); Key(NativeKey.E, true); }); await input.FlushAsync(token);
                var original = host.Supervisor.Status.Identity!; var beforeReplace = await Snapshot("before-replace");
                var b = Project("b", source.Replace("0xffc06020", "0xffa050d0"), 2); host.Editor.SetContext(Context(b)); await host.Editor.BuildAsync(token); await host.Editor.ReplaceAsync(token);
                await input.FlushAsync(token); await Owner(() => { });
                Case("replace-retains-camera-tick-interaction", await Snapshot("restored") == beforeReplace);
                var rejected = false; try { await host.Supervisor.SendInputAsync(original, new(true, GameButtons.Forward), token); } catch (OperationCanceledException) { rejected = true; }
                Case("old-input-not-forwarded-to-new-runtime", rejected && host.Supervisor.Status.State == SessionState.Paused);
                await host.Editor.StepAsync(token); var replaced = await Snapshot("after-replace-step");
                Case("replace-discards-held-look-and-pending-E", !input.Captured && replaced.Camera == beforeReplace.Camera && replaced.Interaction == beforeReplace.Interaction);
                await Capture("04-replaced", replaced, 0xffa050d0, 320);
                await host.Editor.PlayAsync(token); await Click(); await Owner(() => Key(NativeKey.W, true));
                var target = replaced.Tick + 10;
                while (host.Supervisor.Status.Tick < target) { await host.Supervisor.GetStatusAsync(token); await Task.Delay(5, token); }
                await Owner(() => { var r = view.PauseButton.Bounds; input.Cancel(); ui.HandleInput(new(InputKind.PointerDown, r.X + r.Width / 2, r.Y + r.Height / 2)); ui.HandleInput(new(InputKind.PointerUp, r.X + r.Width / 2, r.Y + r.Height / 2)); });
                await command; var paused = await Snapshot("playing-paused");
                Case("30Hz-sender-controls-playing-camera", input.Error == null && paused.Camera.Z < replaced.Camera.Z - .2f);
                await host.Editor.StepAsync(token); var final = await Snapshot("pause-step"); Case("pause-button-clears-held", final.Camera == paused.Camera);
                await Capture("05-playing-paused", final, 0xffa050d0, 320);
                await host.Editor.StopAsync();
                Case("editor-authoring-ownership-preserved", window.Id == windowId && !view.IsDisposed && JsonSerializer.Serialize(authoring) == before);
                passed = true;
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await DrainInput(); await host.Editor.StopAsync(); await command; await app.StopAsync(); await app.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            try
            {
                Probe.Check(host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && host.Frames.MappingCount == 0 && sessions.ActiveSessionCount == 0 && binding.ImportReadbacks == 0 && sender.IsCompletedSuccessfully,
                    "Input sender/native ownership not drained.");
                Probe.Check(factory.Inner.Leases.All(l => l.Reaped && l.ResourcesReleased) && !factory.Inner.Diagnostics.Any(d => d.Stage == TrueMoon.Mithril.UnitProcessStage.Killing || d.Stage == TrueMoon.Mithril.UnitProcessStage.ProcessReleased && d.ExitCode != 0), "Input child was killed/failed.");
                foreach (var lease in factory.Inner.Leases)
                {
                    var c = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"surface-cleanup-{lease.Identity.Generation}.json")));
                    var p = c.GetProperty("LastSurfaceProducer");
                    Probe.Check(p.GetProperty("Errors").GetInt32() == 0 && p.GetProperty("Warnings").GetInt32() == 0 && c.GetProperty("ResourceCount").GetInt32() == 0 && c.GetProperty("Error").ValueKind == JsonValueKind.Null &&
                        (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") != "1" || p.GetProperty("ValidationEnabled").GetBoolean()), "Input runtime GPU validation/cleanup failed.");
                }
                Probe.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Input game loaded in Editor.");
                if (proof && failure == null) Probe.Check(captures.Count == 5 && cases.Count == 15 && rawKeys >= 20 && factory.Inner.Leases.Count == 2, "Incomplete input proof.");
            }
            catch (Exception validation) { failure = failure == null ? validation : new AggregateException(failure, validation); }
            Records.Write(directory, "input-report.json", new { Passed = passed && failure == null, ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1", Cases = cases, Captures = captures, Snapshots = snapshots,
                RawKeyCallbacks = rawKeys, SentPackets = input?.SentPackets, SenderCompleted = sender.IsCompletedSuccessfully, ImportReadbacks = binding.ImportReadbacks, Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount, Mappings = host.Frames.MappingCount, UiSessions = sessions.ActiveSessionCount,
                Children = factory.Inner.Leases.Select(l => new { l.ProcessId, l.Identity.Generation, l.Reaped, l.ResourcesReleased, l.StandardError }), Error = failure?.ToString() });
        }
        if (failure != null) throw failure; return 0;

        GameBuildRequest Project(string name, string text, long revision)
        {
            var path = Path.Combine(directory, "input-" + name); Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Game.cs"), text); File.WriteAllText(Path.Combine(path, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            var project = Path.Combine(path, "Game.Logic.csproj");
            File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include=\"TrueMoon.Aether\"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference></ItemGroup></Project>");
            return new(project, "Game.Logic.dll", "Game.Logic.CameraGame", revision);
        }
    }
    private sealed record Pending(string Name, GameCheckpointState State, ulong Generation, uint Color, int Width)
    { internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private sealed class InputFactory : IRuntimeFactory
    {
        internal NativeRuntimeFactory Inner { get; } = new();
        public IRuntimeLease Create(SessionLaunch launch) => Inner.Create(launch with { Behavior = "scene-3d" });
    }
}
