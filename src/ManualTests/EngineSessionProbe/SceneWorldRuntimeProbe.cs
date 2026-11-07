using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Also compiled unchanged by the independent package consumer. No game CLR types are referenced here.
public static class SceneWorldRuntimeProbe
{
    private const int EntityCount = 256, Width = 48, Height = 32;

    public static async Task<int> RunAsync(string runtimePath, string gameDirectory, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        Exception? failure = null;
        var generations = new List<object>();
        NativeRuntimeFactory? factory = null;
        GameBuildArtifact? artifact = null;
        NativeRuntimeLaunch? runtime = null;
        string? hostId = null;
        var payloadBytes = 0;
        var expectedColor = 0u;
        try
        {
            Check(!GameLoaded(), "The proof parent has already loaded Game.Logic.");
            runtime = Command(Path.GetFullPath(runtimePath));
            hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
            artifact = GameBuildArtifact.Publish(Path.GetFullPath(gameDirectory), Path.Combine(reportDirectory, "artifacts"),
                hostId, "Game.Logic.dll", "Game.Logic.WorldProbeGame", 23);
            var (data, color) = Corpus(); expectedColor = color;
            var resolution = new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas)));
            var codec = new SceneWorldCodec(new SceneFileCodec(new SceneAuthoringSessionFactory(resolution, new SceneAuthoringOptions())));
            var bytes = codec.Encode(data); payloadBytes = bytes.Length;
            Check(payloadBytes > 65536 && payloadBytes <= SceneWorldCodec.MaximumBytes, "The full scene must exceed the legacy control budget.");
            var decoded = codec.Decode(bytes).Scene;
            Check(decoded.Entities.Length == EntityCount && decoded.Definitions.Length == 2 && decoded.Schemas.Length == 1,
                "The complete source catalogs did not survive encoding.");
            Check(decoded.Entities[1].Name == "Probe 1" && decoded.Entities[1].SiblingOrder == 1 && decoded.Entities[1].ParentId == decoded.Entities[0].Id,
                "Names, hierarchy and sibling order did not survive the full payload.");

            factory = new NativeRuntimeFactory(runtime);
            await using var app = App.Build(context => context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId;
                options.RuntimeLaunch = runtime; options.RuntimeFactory = _ => factory;
                options.ReportDirectory = reportDirectory;
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(30), Command = TimeSpan.FromSeconds(10) };
            }));
            await app.StartAsync();
            var host = app.Services.Resolve<AetherHost>()!;
            Guid? firstWorld = null, firstRegistry = null;
            SessionIdentity? firstIdentity = null;
            for (var generation = 1; generation <= 2; generation++)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var start = host.Frames.StartWorldAsync(data, artifact, Width, Height, token: deadline.Token);
                var first = await ReadFrameAsync(host.Frames, start, 0, expectedColor, deadline.Token);
                await start;
                var status = host.Supervisor.Status;
                Check(status.State == SessionState.Paused && status.SceneLoaded && status.Tick == 0 && status.SceneRevision == data.Scene.Revision,
                    "Complete scene activation did not precede Paused readiness.");
                Check(status.SceneWorldId is { } world && world != Guid.Empty && status.SceneRegistryId is { } registry && registry != Guid.Empty,
                    "Activation omitted World or registry identity.");
                Check(status.SceneEntityCount == EntityCount && status.SceneComponentCount == EntityCount,
                    "The runtime did not acknowledge all inherited component instances.");
                var identity = status.Identity!;
                Check(identity.Generation == (ulong)generation && identity.GameBuildId == artifact.BuildId && identity.HostContractId == hostId,
                    "The selected child generation or build identity changed.");
                Check(status.ProcessId is > 0 && status.ProcessId != Environment.ProcessId, "The game did not run in a separate owned process.");
                if (generation == 1)
                { firstWorld = status.SceneWorldId; firstRegistry = status.SceneRegistryId; firstIdentity = identity; }
                else
                {
                    Check(status.SceneWorldId != firstWorld && status.SceneRegistryId != firstRegistry,
                        "A later runtime reused World or registry identity.");
                    Check(identity.SessionId == firstIdentity!.SessionId && identity.Nonce != firstIdentity.Nonce,
                        "The session failed to fence its new generation.");
                }
                var loadedPath = Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly);
                Check(IsPinned(loadedPath), "The selected artifact was not pinned while active.");
                var reply = await host.Supervisor.StepAsync(deadline.Token);
                Check(reply.Tick == 1 && reply.Phase == "Paused" && host.Supervisor.Status.Tick == 1,
                    "Step did not execute exactly one ECS tick.");
                var steppedColor = 0xff000000u | ((expectedColor + EntityCount) & 0xffffff);
                var second = await ReadFrameAsync(host.Frames, Task.CompletedTask, 1, steppedColor, deadline.Token);
                Check(second.FrameId > first.FrameId, "The stepped frame did not advance its identity.");
                Check(!GameLoaded(), "The parent loaded game CLR types during scene transfer.");
                await host.Frames.StopAsync();
                var lease = factory.Leases[generation - 1];
                Check(host.Frames.MappingCount == 0 && host.Frames.Display == null && host.Frames.Presentation == null,
                    "Stop retained CPU mappings or presentation ownership.");
                Check(lease.Reaped && lease.ResourcesReleased && lease.Exited.IsCompleted && host.Supervisor.Status.State == SessionState.Stopped,
                    "Stop did not drain and reap the child.");
                Check(!IsPinned(loadedPath), "A reaped child retained its artifact pin.");
                generations.Add(new
                {
                    Generation = identity.Generation, status.ProcessId, status.SceneWorldId, status.SceneRegistryId,
                    status.SceneEntityCount, status.SceneComponentCount, TickBefore = first.Tick, TickAfter = second.Tick,
                    FrameBefore = first.FrameId, FrameAfter = second.FrameId, ExpectedColor = expectedColor, SteppedColor = steppedColor,
                    PixelsBefore = first.Pixels, PixelsAfter = second.Pixels, lease.Reaped, lease.ResourcesReleased,
                    MappingsAfterStop = host.Frames.MappingCount, ArtifactPinReleased = true
                });
            }
            await app.StopAsync();
            Check(factory.Leases.Count == 2 && factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased),
                "App cleanup retained an owned runtime.");
            Check(hostId == BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath) && !GameLoaded(),
                "Runtime provenance changed or game code entered the parent.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            var report = new
            {
                Completed = failure == null, Scenario = "full-scene-world-runtime", ParentPid = Environment.ProcessId,
                ParentAssembly = Assembly.GetEntryAssembly()?.Location, RuntimeAssembly = runtime?.RuntimeAssemblyPath,
                HostContractId = hostId, artifact?.BuildId, EntryType = artifact?.EntryType, PayloadBytes = payloadBytes,
                ControlPayloadLimit = 65536, ChunkBytes = 32768, EntityCount, ComponentCount = EntityCount,
                ExpectedColor = expectedColor, GameLoadedInParent = GameLoaded(), Generations = generations,
                AllChildrenReleased = factory != null && factory.Leases.Count == 2 && factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased),
                Error = failure?.ToString()
            };
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return 0;
    }

    private static async Task<(ulong FrameId, ulong Tick, uint[] Pixels)> ReadFrameAsync(FrameSession frames, Task startup, ulong tick, uint color, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (startup.IsFaulted || startup.IsCanceled) await startup;
            var display = frames.Display;
            if (display?.Receiver.FirstFrame.IsCompletedSuccessfully == true)
            {
                using var frame = display.Receiver.TakeLatest();
                if (frame != null && frame.Metadata.Tick == tick)
                {
                    Check(display.Surface.Width == Width && display.Surface.Height == Height, "CPU frame dimensions changed.");
                    var samples = new[] { (0, 0), (Width - 1, 0), (0, Height - 1), (Width - 1, Height - 1), (20, 12), (31, 20) };
                    var pixels = new uint[samples.Length];
                    for (var i = 0; i < samples.Length; i++)
                    {
                        var (x, y) = samples[i];
                        pixels[i] = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(y * display.Surface.Stride + x * 4)..]);
                        Check(pixels[i] == FramePattern.Pixel(x, y, Width, Height, frame.Metadata.FrameId, tick, color),
                            $"The full-scene ECS state produced the wrong pixel at ({x}, {y}), tick {tick}.");
                    }
                    frames.Presented(display, frame.Metadata);
                    return (frame.Metadata.FrameId, frame.Metadata.Tick, pixels);
                }
            }
            await Task.Delay(10, token);
        }
    }

    private static (SceneWorldData Data, uint Color) Corpus()
    {
        var type = new ComponentTypeId(Guid.Parse("73f4de14-05d4-4654-81e2-f0d4e353ca10"));
        var component = new ComponentId(Id(1)); var template = new EntityDefinitionId(Id(2)); var variant = new EntityDefinitionId(Id(3));
        var schema = new ComponentSchema(type, 1,
        [new("value", PropertyValueKind.Integer, PropertyValue.From(3L)), new("note", PropertyValueKind.String, PropertyValue.From("from-template")),
            new("target", PropertyValueKind.String, PropertyValue.Null, isNullable: true)]);
        var root = new EntityId(Id(100));
        long sum = 0;
        var entities = new EntityInstance[EntityCount];
        for (var i = 0; i < entities.Length; i++)
        {
            var note = new string((char)('a' + i % 26), 512) + "\ud83c\udf19";
            var overrides = new List<ComponentPropertyOverride>
            { new(component, "note", PropertyValue.From(note)), new(component, "target", i == 0 ? PropertyValue.Null : PropertyValue.From(root.Value.ToString("D"))) };
            var value = i % 3 == 0 ? 5L : 10L + i;
            if (i % 3 != 0) overrides.Add(new(component, "value", PropertyValue.From(value)));
            entities[i] = new(new(Id(100 + i)), variant, new(propertyOverrides: overrides), i == 0 ? null : root, "Probe " + i, i);
            sum += value + note.Length + (i == 0 ? 0 : 17);
        }
        var scene = new SceneDocument(new(Id(10)), 23, entities,
            [new(template, new([new ComponentData(component, type, 1)])),
                new(variant, new(propertyOverrides: [new(component, "value", PropertyValue.From(5L))]), template)], [schema]);
        return (new(scene), 0xff000000u | (uint)(sum & 0xffffff));
    }
    private static Guid Id(int value) => new($"ce25b354-4e1f-4fd2-899f-{value:000000000000}");
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Game.Logic");
    private static bool IsPinned(string path)
    {
        try { using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; }
        catch (IOException) { return true; }
    }
    private static NativeRuntimeLaunch Command(string path)
    {
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return new(path, Path.ChangeExtension(path, ".dll"));
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select a runtime DLL or apphost EXE.", nameof(path));
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing parent executable path.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Launch the proof parent through dotnet when selecting a runtime DLL.");
        return new(executable, path, [path]);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
