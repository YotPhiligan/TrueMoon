using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

namespace PackageEditor;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        if (args is not [var runtimePath, var gameProject, var report, var provider]) return 2;
        runtimePath = Path.GetFullPath(runtimePath); gameProject = Path.GetFullPath(gameProject); report = Path.GetFullPath(report);
        Directory.CreateDirectory(report);
        try
        {
            // View creation/disposal is synchronous on this owner thread; async runtime work follows.
            var editor = EditorProof(provider == "di", Path.Combine(report, "editor"));
            var runtime = await RuntimeProof(runtimePath, gameProject, Path.Combine(report, "runtime"), provider == "di");
            Check(!GameLoaded(), "The Editor process loaded Game.Logic.");
            await File.WriteAllTextAsync(Path.Combine(report, "report.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Provider = provider, Editor = editor, Runtime = runtime,
                GameLoadedInParent = GameLoaded(), Entry = Assembly.GetEntryAssembly()!.Location,
                Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
                    .Select(a => new { Name = a.GetName().Name, Path = a.Location }).ToArray()
            }, Json));
            return 0;
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(report, "failure.txt"), error.ToString());
            Console.Error.WriteLine(error); return 1;
        }
    }

    private static object EditorProof(bool microsoft, string directory)
    {
        Directory.CreateDirectory(directory);
        using var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c =>
        {
            c.Services(s => s.Singleton<PackageDependency>().Singleton<PackageService>());
            c.UseAetherEditor(o =>
            {
                o.HostContractId = new('0', 64); o.GameBuildId = BuildIdentity.PreviewId; o.ReportDirectory = directory;
            });
        }).Build();
        app.StartAsync().GetAwaiter().GetResult();
        var service = app.Services.Resolve<PackageService>()!;
        Check(ReferenceEquals(service.Dependency, app.Services.Resolve<PackageDependency>()), "Constructor DI changed.");
        var factory = app.Services.Resolve<IFactory<EditorView>>()!;
        var first = factory.Create()!; var second = factory.Create()!;
        try
        {
            Check(!ReferenceEquals(first, second) && !ReferenceEquals(first.Inspector, second.Inspector), "View factory shared an owned tree.");
            Check(ReferenceEquals(first.DataContext, second.DataContext), "Views do not borrow the App model.");
            first.Dispose(); Check(first.IsDisposed && first.Inspector!.IsDisposed && !second.IsDisposed, "View cleanup crossed ownership.");
            using var third = factory.Create()!;
            Check(ReferenceEquals(third.DataContext, second.DataContext), "Disposed View released the borrowed model.");
            var store = app.Services.Resolve<ISceneDocumentStore>()!;
            var scene = new SceneDocument(new(Guid.NewGuid()), 17, [], [], []);
            var path = Path.Combine(directory, "scene.aetherscene");
            var stamp = store.WriteAsync(path, scene, null).GetAwaiter().GetResult();
            var read = store.ReadAsync(path).GetAwaiter().GetResult();
            Check(read.Document.Id == scene.Id && read.Document.Revision == 17 && read.Stamp == stamp, "Shared scene roundtrip changed.");
            try { store.WriteAsync(path, scene, null).GetAwaiter().GetResult(); throw new InvalidOperationException("Create overwrote the scene."); }
            catch (SceneFileConflictException) { }
            app.StopAsync().GetAwaiter().GetResult();
            Check(app.Services.Resolve<EditorProjects>()!.IsClosed && app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed,
                "Editor App did not close project/document owners.");
            app.Dispose();
            Check(!second.IsDisposed, "App disposed a parent-owned View.");
            Check(typeof(EditorView).Assembly.EntryPoint == null, "Editor package contains an executable.");
            var generated = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace?.StartsWith("TrueMoon.Generated", StringComparison.Ordinal) == true)
                .Select(t => t.FullName!).ToArray();
            Check(generated.Any(n => n.Contains("PackageEchoInvocation", StringComparison.Ordinal)), "Consumer Enerit generation did not run.");
            return new { Views = 3, BorrowedModelPreserved = true, ParentOwnsView = true, SceneRevision = read.Document.Revision,
                SceneSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), ConflictRejected = true,
                GeneratedTypes = generated, ThemeType = typeof(Theme).FullName, StyleType = typeof(EditorViewStyle).FullName };
        }
        finally { first.Dispose(); second.Dispose(); }
    }

    private static async Task<object> RuntimeProof(string runtimePath, string gameProject, string directory, bool microsoft)
    {
        Directory.CreateDirectory(directory);
        var runtimeDll = Path.ChangeExtension(runtimePath, ".dll");
        var dotnet = Environment.ProcessPath!;
        var launch = runtimePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? new NativeRuntimeLaunch(runtimePath, runtimeDll)
            : new NativeRuntimeLaunch(dotnet, runtimeDll, [runtimeDll]);
        var hostId = BuildIdentity.ForRuntime(runtimeDll);
        var maxLive = 0; var drainSignal = 0; NativeRuntimeFactory? factory = null;
        await using var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c =>
        {
            c.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(directory, "diagnostics", "host"), JsonUseRunDirectories = true });
            c.UseAetherEditorDiagnostics();
            c.UseAether(o =>
            {
                o.HostContractId = hostId; o.GameBuildId = BuildIdentity.PreviewId;
                o.ReportDirectory = directory; o.RuntimeLaunch = launch;
                o.StatusChanged = status =>
                {
                    if (factory != null) maxLive = Math.Max(maxLive, factory.Leases.Count(l => !l.ResourcesReleased));
                    if (status.State != SessionState.CleanupFailed || Interlocked.Exchange(ref drainSignal, 1) != 0) return;
                    // A synchronous marker survives a stalled async file sink and lets the
                    // external harness collect this owned process before its final exit.
                    try
                    {
                        var snapshotPath = Path.Combine(directory, "drain-snapshot.json");
                        File.WriteAllText(snapshotPath + ".tmp", JsonSerializer.Serialize(new
                        {
                            Environment.ProcessId, Utc = DateTimeOffset.UtcNow, ChildProcessId = status.ProcessId,
                            Generation = status.Identity?.Generation, Error = status.Failure?.ToString()
                        }));
                        File.Move(snapshotPath + ".tmp", snapshotPath);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { Console.Error.WriteLine("Could not persist drain snapshot: " + error.Message); }
                };
            });
        }).Build();
        await app.StartAsync();
        factory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var host = app.Services.Resolve<AetherHost>()!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(115));
        var token = deadline.Token;
        var scene = new SceneSnapshot("package-scene", 7, [new() { Id = "switch", Interactive = true }]);
        var a = await host.Builds.BuildAsync(new(gameProject, "Game.Logic.dll", "Game.Logic.ProbeGame", 7), token);
        // Build a second immutable artifact from a distinct owned source directory.
        var candidate = Path.Combine(directory, "candidate"); Directory.CreateDirectory(candidate);
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(gameProject)!, "*.*"))
            if (Path.GetExtension(path) is ".cs" or ".csproj") File.Copy(path, Path.Combine(candidate, Path.GetFileName(path)));
        File.Copy(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(gameProject)!)!, "Directory.Build.props"), Path.Combine(candidate, "Directory.Build.props"));
        File.WriteAllText(Path.Combine(candidate, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        var source = Path.Combine(candidate, "ProbeGame.cs");
        File.WriteAllText(source, File.ReadAllText(source).Replace("0xff2080c0", "0xffc06020", StringComparison.Ordinal));
        var b = await host.Builds.BuildAsync(new(Path.Combine(candidate, "Game.csproj"), "Game.Logic.dll", "Game.Logic.ProbeGame", 8), token);
        Check(a.BuildId != b.BuildId && host.Builds.ProcessId == null, "Build did not produce/reap distinct candidates.");
        var starting = host.Frames.StartAsync(scene, a, 32, 16, token: token);
        await Present(host.Frames, starting, 0xff2080c0, 1, token); await starting;
        for (var i = 0; i < 3; i++) await host.Supervisor.StepAsync(token);
        var checkpoint = await host.Supervisor.CaptureCheckpointAsync(token);
        Check(checkpoint.State.Tick == 3 && checkpoint.State.Interaction, "Game did not execute selected logic.");
        var old = host.Supervisor.Status.Identity!;
        var replacing = host.Replacement.ReplaceAsync(new(b, old, scene, 8), token);
        await Present(host.Frames, replacing, 0xffc06020, old.Generation + 1, token); var replacement = await replacing;
        Check((await host.Supervisor.CaptureCheckpointAsync(token)).State == checkpoint.State, "Replace lost complete checkpoint.");
        Check(replacement.Identity.GameBuildId == b.BuildId && replacement.Identity.Nonce != old.Nonce && maxLive == 1,
            "Replace reused identity or overlapped children.");
        await host.Frames.StopAsync();
        // Repeat Start/Stop from the restored checkpoint without rebuilding.
        var restarting = host.Frames.StartAsync(scene, b, checkpoint, 32, 16, token: token);
        await Present(host.Frames, restarting, 0xffc06020, replacement.Identity.Generation + 1, token); await restarting;
        Check((await host.Supervisor.CaptureCheckpointAsync(token)).State == checkpoint.State, "Restart did not restore checkpoint.");
        await host.Frames.StopAsync();
        var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
        using var deliveryDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deliveryDeadline.CancelAfter(TimeSpan.FromSeconds(10));
        await Wait(() => pipeline.Read().Any(r => r.Origin != null && r.EventName == "Package.Runtime.Ready"), deliveryDeadline.Token);
        var imported = pipeline.Read().Where(r => r.Origin != null && r.EventName == "Package.Runtime.Ready").ToArray();
        Check(imported.All(r => r.Properties.ContainsKey("SessionId") && !r.Properties.ContainsKey("Nonce")), "Child provenance/redaction changed.");
        // Wrong counterpart must fail before spawning any process.
        var invalid = new SessionLaunch(new(Guid.NewGuid(), 1, new('0', 64), BuildIdentity.PreviewId, new('1', 64)),
            "package-reject-" + Guid.NewGuid().ToString("N"), Path.Combine(directory, "rejected"), Role: RuntimeRole.EditPreview);
        await using (var rejected = (NativeRuntimeLease)factory.Create(invalid))
        {
            try { await rejected.LaunchAsync(token); throw new InvalidOperationException("Wrong fingerprint launched."); }
            catch (InvalidDataException) { }
            finally { await rejected.StopAsync(); }
            Check(rejected.ProcessId == 0 && rejected.Reaped && rejected.ResourcesReleased, "Wrong identity retained resources.");
        }
        await app.StopAsync(); await pipeline.FlushAsync();
        Check(host.Frames.MappingCount == 0 && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 &&
            host.Builds.ProcessId == null && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "App drain retained resources.");
        var children = factory.Leases.Where(l => l.ProcessId > 0).Select(l =>
        {
            var file = Directory.GetFiles(directory, "runtime-*.json").Single(p =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(p));
                return document.RootElement.GetProperty("Pid").GetInt32() == l.ProcessId;
            });
            var metadata = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
            Check(!metadata.GetProperty("EditorLoaded").GetBoolean() && !metadata.GetProperty("UiLoaded").GetBoolean() &&
                metadata.GetProperty("GameLoaded").GetBoolean() && metadata.GetProperty("Error").ValueKind == JsonValueKind.Null,
                "Runtime role isolation/cleanup failed.");
            var paths = metadata.GetProperty("GameAssemblyPaths").EnumerateArray().Select(x => x.GetString()!).ToArray();
            var selected = metadata.GetProperty("GameBuildId").GetString() == a.BuildId ? a : b;
            Check(paths.Length == 1 && string.Equals(Path.GetFullPath(paths[0]), Path.Combine(selected.DirectoryPath, selected.EntryAssembly),
                StringComparison.OrdinalIgnoreCase), "Game loaded outside the exact selected artifact.");
            Check(l.StandardOutput.Contains("PACKAGE_RUNTIME_STDOUT", StringComparison.Ordinal) &&
                l.StandardError.Contains("PACKAGE_RUNTIME_STDERR", StringComparison.Ordinal), "Raw child output capture failed.");
            Check(l.ChildDiagnosticsStatus?.RuntimeObservation?.Collector?.Sinks.Any(sink => sink.Name == "jsonl") == true &&
                l.ChildDiagnosticsStatus.RuntimeObservation.GraphicsDevice == null, "CPU game runtime lost child health or fabricated a graphics device.");
            return new { l.ProcessId, l.Reaped, l.ResourcesReleased, Metadata = metadata, l.ChildDiagnosticsStatus };
        }).ToArray();
        Check(children.Length == 3 && imported.Length > 0 && !GameLoaded(), "Incomplete package runtime proof.");
        var diagnostics = app.Services.Resolve<EditorDiagnosticsViewModel>()!; diagnostics.Refresh();
        var diagnosticReport = Path.Combine(directory, "game-diagnostic-report.json");
        await app.Services.Resolve<EditorDiagnosticReportWriter>()!.WriteReportAsync(diagnostics.Snapshot(), diagnosticReport, new());
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(diagnosticReport, token));
        var observedGames = report.RootElement.GetProperty("runtimeObservations").EnumerateArray().Where(row => row.GetProperty("ProcessId").ValueKind == JsonValueKind.Number && row.GetProperty("ProcessId").GetInt32() > 0).ToArray();
        Check(observedGames.Length == 3 && observedGames.All(row => row.GetProperty("CollectorAvailable").GetBoolean() && !row.GetProperty("DeviceAvailable").GetBoolean()), "Game diagnostic report lost numeric child health/unavailable CPU device state.");
        Check(children.Select(child => child.Metadata.GetProperty("GameBuildId").GetString()).SequenceEqual(new[] { a.BuildId, b.BuildId, b.BuildId }),
            "Runtime did not execute the selected build sequence.");
        var shaders = typeof(AetherHost).Assembly.GetManifestResourceNames().Where(n => n.EndsWith(".spv", StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = typeof(AetherHost).Assembly.GetManifestResourceStream(n)!;
                return new { Name = n, Sha256 = Convert.ToHexString(SHA256.HashData(stream)) };
            }).ToArray();
        Check(shaders.Length == 2, "Aether embedded shaders are missing.");
        return new { HostId = hostId, RuntimePath = runtimePath, Builds = new[] { a, b }, Checkpoint = checkpoint.State, Shaders = shaders,
            Replacement = replacement.Identity, MaxLive = maxLive, Children = children, ImportedRecords = imported.Length,
            IdentityRejected = true, Mappings = host.Frames.MappingCount, DiagnosticReport = diagnosticReport };
    }

    private static async Task Present(FrameSession frames, Task pending, uint color, ulong generation, CancellationToken token)
    {
        var observed = false; var observations = 0;
        while (!pending.IsCompleted)
        {
            var display = frames.Display;
            if (++observations == 1 || observations % 500 == 0)
                Console.WriteLine($"PRESENT state={frames.Supervisor.Status.State} generation={frames.Supervisor.Status.Identity?.Generation} display={display != null} received={display?.Receiver.Received} first={display?.Receiver.FirstFrame.Status} observed={observed}");
            if (display != null && display.Surface.Identity.Generation >= generation)
            {
                using var frame = display.Receiver.TakeLatest();
                if (frame != null)
                {
                    var pixel = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(10 * display.Surface.Stride + 16 * 4)..]);
                    Check(pixel == FramePattern.Pixel(16, 10, 32, 16, frame.Metadata.FrameId, frame.Metadata.Tick, color),
                        "Frame came from the wrong game build.");
                    frames.Presented(display, frame.Metadata); observed = true;
                }
            }
            await Task.Delay(2, token);
        }
        await pending; Check(observed, "No selected-game frame was presented.");
    }

    private static async Task Wait(Func<bool> ready, CancellationToken token)
    { while (!ready()) await Task.Delay(10, token); }
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
