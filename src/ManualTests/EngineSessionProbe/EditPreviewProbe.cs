using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Directed native acceptance for the engine API. The existing viewport fixture is reused; no game project is opened/built.
internal static class EditPreviewProbe
{
    internal static async Task<int> RunAsync(string directory)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a fresh preview proof directory.");
        Directory.CreateDirectory(directory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        await using var app = App.Build(context =>
        {
            context.Services(services => services
                .Singleton<EditorGpuViewportBinding>(_ => new(runtime => (NativeRuntimeLease)runtime, () => BuildIdentity.PreviewId))
                .Singleton<IFactory<ViewportView>>(_ => new Factory<ViewportView>(() =>
                {
                    var view = new ViewportView(_ => { }); view.Canvas.PreserveAspectRatio = true;
                    view.Status.Value = "Static Edit preview / no game build"; return view;
                })));
            context.UseAetherAuthoring();
            context.UseAether(options =>
            {
                options.HostContractId = BuildIdentity.HostId; options.GameBuildId = BuildIdentity.PreviewId; options.ReportDirectory = directory;
                options.RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime");
                options.ViewportTransportFactory = resolver => new Factory<IGameViewportTransport, GpuResourceHost>(resolver.Resolve<EditorGpuViewportBinding>().CreateTransport);
            });
            context.UsePresentationFactory<ViewportView>(options =>
            {
                options.UseSkiaOpenGL(fonts); options.UseSilkWindow(window =>
                { window.Width = 1000; window.Height = 760; window.Title = "TrueMoon Edit preview proof"; window.FrameMode = UiFrameMode.Continuous; });
            });
        });
        var host = app.Services.Resolve<AetherHost>(); var window = app.Services.Resolve<HostedUiWindow>();
        var binding = app.Services.Resolve<EditorGpuViewportBinding>(); var uiSessions = app.Services.Resolve<HostedUiSessionFactory>();
        var runtimes = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>();
        var entity = new EntityId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var transform = new ComponentId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var appearance = new ComponentId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var definition = new EntityDefinition(new(Guid.Parse("30000000-0000-0000-0000-000000000001")), new(addedComponents:
        [
            new(transform, BuiltInComponents.TransformId, 1, [new("x", PropertyValue.From(-1d)), new("y", PropertyValue.From(1d))]),
            new(appearance, BuiltInComponents.AppearanceId, 1, [new("r", PropertyValue.From(1d)), new("g", PropertyValue.From(0d)), new("b", PropertyValue.From(0d))])
        ]));
        var source = new SceneDocument(new(Guid.Parse("40000000-0000-0000-0000-000000000001")), 7,
            [new(entity, definition.Id)], [definition], BuiltInComponents.Schemas);
        using var authoring = app.Services.Resolve<IFactory<ISceneAuthoringSession, SceneDocument>>().Create(source);
        var original = authoring.Read();
        ViewportView? view = null; Pending? pending = null; var captures = new List<object>(); var centroids = new List<double>();
        var acknowledge = true; Exception? failure = null;
        window.FramePresented += ui =>
        {
            var current = (ViewportView)ui.Root;
            Probe.Check(view == null || ReferenceEquals(view, current), "Preview recreated its UI tree."); view = current;
            binding.Observe(ui);
            if (!Volatile.Read(ref acknowledge) || current.Canvas.DrawnViewport is not { } frame || !host.Preview.Presented(frame)) return;
            var check = Volatile.Read(ref pending); var described = host.Preview.Describe(frame);
            if (check == null || described?.Document.Revision != check.Revision ||
                frame.Presentation.Surface.Width != check.Width || frame.Presentation.Surface.Height != check.Height ||
                Interlocked.CompareExchange(ref pending, null, check) != check) return;
            try
            {
                using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                var bounds = current.Canvas.ImageBounds; var scale = ui.Viewport.Scale;
                var count = 0; double total = 0;
                for (var y = 2; y < 98; y++) for (var x = 2; x < 98; x++)
                {
                    var pixel = bitmap.GetPixel((int)((bounds.X + bounds.Width * x / 100d) * scale), (int)((bounds.Y + bounds.Height * y / 100d) * scale));
                    if (pixel.Red > 180 && pixel.Green < 15 && pixel.Blue < 15) { count++; total += x / 100d; }
                }
                Probe.Check(count is > 60 and < 2500, "Expected a bounded red primitive in the actual presented image.");
                var centroid = total / count; centroids.Add(centroid);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using (var output = File.Create(Path.Combine(directory, check.Name + ".png"))) encoded.SaveTo(output);
                captures.Add(new { check.Name, Document = described!.Document, Surface = frame.Presentation.Surface, Frame = frame.Metadata,
                    RedSamples = count, CentroidX = centroid, Sha256 = Convert.ToHexString(SHA256.HashData(encoded.ToArray())) });
                check.Completion.TrySetResult();
            }
            catch (Exception error) { check.Completion.TrySetException(error); }
        };
        async Task Capture(string name, long revision, int width, int height)
        {
            var check = new Pending(name, revision, width, height); Volatile.Write(ref pending, check); window.RequestFrame();
            await check.Completion.Task.WaitAsync(token);
        }
        try
        {
            await app.StartAsync(token); await binding.InitializeAsync(window, host, token);
            window.Closing += async _ => { await host.DisposeAsync(); return true; };
            var first = await host.Preview.ShowAsync(original.Resolved, 640, 360, token);
            await Capture("01-original", 7, 640, 360);
            var stale = host.Preview.LastPresented!.Frame;
            var edited = authoring.Apply(new(new(Guid.NewGuid(), 7, "native-proof"), [new(entity, transform, new(1, 1, 0))]));
            Probe.Check(edited.Succeeded, "Headless Transform edit failed.");
            var second = await host.Preview.ShowAsync(authoring.Read().Resolved, 640, 360, token);
            await Capture("02-transform", 8, 640, 360);
            Probe.Check(second.Runtime.Generation > first.Runtime.Generation && !host.Preview.Presented(stale) && host.Preview.Describe(stale) == null,
                "Old revision ticket was reused/retagged.");
            Probe.Check(centroids[1] > centroids[0] + .12, "Transform edit did not move the rendered cube right.");
            var countBeforeRejection = runtimes.Leases.Count;
            try { await host.Preview.ShowAsync(original.Resolved, 640, 360, token); throw new Exception("Stale revision accepted."); }
            catch (InvalidOperationException) { }
            Probe.Check(runtimes.Leases.Count == countBeforeRejection && host.Preview.Document == second, "Preflight rejection stopped a valid preview.");
            Probe.Check(authoring.Undo(new(Guid.NewGuid(), 8, "native-proof")).Succeeded, "Undo failed.");
            await host.Preview.ShowAsync(authoring.Read().Resolved, 640, 360, token); await Capture("03-undo", 9, 640, 360);
            Probe.Check(Math.Abs(centroids[2] - centroids[0]) < .01, "Undo did not restore rendered position.");
            Probe.Check(authoring.Redo(new(Guid.NewGuid(), 9, "native-proof")).Succeeded, "Redo failed.");
            await host.Preview.ShowAsync(authoring.Read().Resolved, 640, 360, token); await Capture("04-redo", 10, 640, 360);
            Probe.Check(Math.Abs(centroids[3] - centroids[1]) < .01, "Redo did not restore rendered position.");
            await host.Preview.ResizeAsync(800, 450, token); await Capture("05-resize", 10, 800, 450);
            var beforeSuspend = host.Preview.LastPresented!.Frame;
            await host.Preview.ResizeAsync(0, 0, token);
            Probe.Check(host.Preview.LastPresented == null && !host.Preview.Presented(beforeSuspend), "Suspension retained a presented ticket.");
            await host.Preview.ResizeAsync(320, 240, token); await Capture("06-resume", 10, 320, 240);
            await host.Preview.StopAsync();
            Probe.Check(host.Preview.Document == null && host.Preview.LastPresented == null, "Stop retained a preview stamp.");
            Probe.Check(host.Builds.ProcessId == null && host.Builds.DiagnosticsDirectory == null && !Directory.Exists(Path.Combine(directory, "builds")),
                "Preview ran the game compiler.");
            // Native close while the next generation is waiting for actual first presentation.
            Volatile.Write(ref acknowledge, false);
            var starting = host.Preview.ShowAsync(authoring.Read().Resolved, 320, 240, token);
            await Probe.WaitAsync(() => host.Frames.Presentation is { Surface.Suspended: false } && !starting.IsCompleted, TimeSpan.FromSeconds(15));
            window.Close(); await window.Completion.WaitAsync(token);
            try { await starting; throw new Exception("Close allowed late preview readiness."); }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException) { }
            Probe.Check(authoring.Read().Document.Revision == 10 && original.Document.Revision == 7 &&
                original.Resolved.Entities[entity].Components[transform].Properties["x"].Value.GetNumber() == -1, "Preview mutated authoring snapshots.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await host.DisposeAsync(); window.Close(); await app.StopAsync(); await app.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            try
            {
                Probe.Check(captures.Count == 6 && runtimes.Leases.Count == 5, "Incomplete native preview matrix.");
                Probe.Check(host.Frames.MappingCount == 0 && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 &&
                    uiSessions.ActiveSessionCount == 0 && view is { IsDisposed: true } && binding.ImportReadbacks == 0, "Preview retained owned resources.");
                foreach (var lease in runtimes.Leases)
                {
                    Probe.Check(lease.Reaped && lease.ResourcesReleased && string.IsNullOrWhiteSpace(lease.StandardError), "Preview child was not cleanly reaped.");
                    var report = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"preview-runtime-{lease.Identity.Generation}.json")));
                    Probe.Check(!report.GetProperty("GameLoaded").GetBoolean() && report.GetProperty("Build").ValueKind == JsonValueKind.Null &&
                        report.GetProperty("Role").GetInt32() == (int)RuntimeRole.EditPreview &&
                        report.GetProperty("Resources").GetInt32() == 0 && report.GetProperty("Handles").GetInt32() == 0 &&
                        report.GetProperty("Validation").GetProperty("Errors").GetInt32() == 0 && report.GetProperty("Validation").GetProperty("Warnings").GetInt32() == 0,
                        "Preview loaded game code or leaked/failed native resources.");
                }
                Probe.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Game loaded in preview parent.");
            }
            catch (Exception validation) { failure = failure == null ? validation : new AggregateException(failure, validation); }
            Records.Write(directory, "preview-report.json", new { Passed = failure == null, Captures = captures,
                NoGameFile = !File.Exists(Path.Combine(AppContext.BaseDirectory, "Game.Logic.dll")), CompilerStarted = host.Builds.DiagnosticsDirectory != null,
                Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount, Mappings = host.Frames.MappingCount,
                UiSessions = uiSessions.ActiveSessionCount, Children = runtimes.Leases.Select(l => new
                { l.ProcessId, l.Identity.Generation, l.Reaped, l.ResourcesReleased, l.StandardError, l.StandardOutput }), Diagnostics = runtimes.Diagnostics,
                Error = failure?.ToString() });
        }
        if (failure != null) throw failure;
        return 0;
    }
    private sealed record Pending(string Name, long Revision, int Width, int Height)
    { internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
}
