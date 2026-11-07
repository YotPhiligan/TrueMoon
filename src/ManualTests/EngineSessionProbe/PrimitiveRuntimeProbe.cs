using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Standalone source included by the fresh seven-package headless consumer.
public static class PrimitiveRuntimeProbe
{
    public const int LogicalEntityCount = 133, SourceComponentCount = 7;

    public static async Task<int> RunAsync(string runtimePath, string gameDirectory, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        Exception? failure = null; var generations = new List<object>();
        NativeRuntimeFactory? factory = null; string? hostId = null; GameBuildArtifact? build = null;
        try
        {
            Check(!GameLoaded(), "Parent loaded Game.Logic before the primitive proof.");
            var runtime = RuntimeCommand(runtimePath); hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
            factory = new NativeRuntimeFactory(runtime);
            build = GameBuildArtifact.Publish(gameDirectory, Path.Combine(reportDirectory, "artifacts"), hostId,
                "Game.Logic.dll", "Game.Logic.PrimitiveProbeGame", 31);
            var data = CreateScene(); var initialColor = ExpectedColor(0); var movedColor = ExpectedColor(1);
            Check(initialColor != movedColor, "Movement oracle did not distinguish completed ticks.");
            await using var app = App.Build(context => context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId;
                options.RuntimeLaunch = runtime; options.RuntimeFactory = _ => factory; options.ReportDirectory = reportDirectory;
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(30), Command = TimeSpan.FromSeconds(10) };
            }));
            await app.StartAsync(); var host = app.Services.Resolve<AetherHost>()!;
            Guid? priorWorld = null, priorRegistry = null; SessionIdentity? priorIdentity = null;
            for (var generation = 1; generation <= 2; generation++)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var start = host.Frames.StartWorldAsync(data, build, 48, 32, token: deadline.Token);
                var initial = await ObserveFrame(host.Frames, start, 0, initialColor, deadline.Token); await start;
                var status = host.Supervisor.Status;
                Check(status.State == SessionState.Paused && status.SceneLoaded && status.Tick == 0 && status.SceneRevision == 31,
                    "Primitive graph was not completely primed before initial readiness.");
                Check(status.SceneEntityCount == LogicalEntityCount && status.SceneComponentCount == SourceComponentCount,
                    "Runtime confused source component count with derived roles or truncated logical groups.");
                Check(status.SceneWorldId is { } world && world != Guid.Empty && status.SceneRegistryId is { } registry && registry != Guid.Empty,
                    "Primitive activation omitted World/registry identity.");
                var identity = status.Identity!;
                Check(identity.Generation == (ulong)generation && identity.GameBuildId == build.BuildId && status.ProcessId != Environment.ProcessId,
                    "Selected primitive consumer did not run in the expected separate generation.");
                if (generation > 1)
                    Check(status.SceneWorldId != priorWorld && status.SceneRegistryId != priorRegistry &&
                        identity.SessionId == priorIdentity!.SessionId && identity.Nonce != priorIdentity.Nonce,
                        "Primitive graph reused a previous runtime identity.");
                priorWorld = status.SceneWorldId; priorRegistry = status.SceneRegistryId; priorIdentity = identity;
                var stepped = await host.Supervisor.StepAsync(deadline.Token);
                Check(stepped.Tick == 1 && stepped.Phase == "Paused", "Primitive graph did not complete exactly one fixed tick.");
                var moved = await ObserveFrame(host.Frames, Task.CompletedTask, 1, movedColor, deadline.Token);
                Check(moved.FrameId > initial.FrameId && host.Supervisor.Status.Tick == 1, "Completed primitive tick/frame did not advance.");
                Check(!GameLoaded(), "Parent loaded selected game CLR types.");
                await host.Frames.StopAsync(); var lease = factory.Leases[^1];
                Check(host.Frames.MappingCount == 0 && host.Frames.Display == null && host.Supervisor.Status.State == SessionState.Stopped &&
                    lease.Reaped && lease.ResourcesReleased, "Primitive Stop retained frame/controller/runtime ownership.");
                using (var released = new FileStream(Path.Combine(build.DirectoryPath, build.EntryAssembly), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                generations.Add(new { identity.Generation, status.ProcessId, status.SceneWorldId, status.SceneRegistryId,
                    status.SceneEntityCount, status.SceneComponentCount, TickBefore = initial.Tick, TickAfter = moved.Tick,
                    ColorBefore = initialColor, ColorAfter = movedColor, PixelBefore = initial.Pixel, PixelAfter = moved.Pixel,
                    lease.Reaped, lease.ResourcesReleased, MappingsAfterStop = host.Frames.MappingCount, ArtifactPinReleased = true });
            }
            await app.StopAsync();
            Check(!GameLoaded() && factory.Leases.Count == 2 && factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased),
                "Primitive App cleanup retained child resources or loaded game code in parent.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            var report = new { Completed = failure == null, Scenario = "ecs-primitive-runtime", ParentPid = Environment.ProcessId,
                ParentAssembly = Assembly.GetEntryAssembly()?.Location, HostContractId = hostId, build?.BuildId,
                EntryType = build?.EntryType, LogicalEntityCount, SourceComponentCount, Drawables = 2,
                ExpectedInitialColor = ExpectedColor(0), ExpectedMovedColor = ExpectedColor(1),
                GameLoadedInParent = GameLoaded(), Generations = generations,
                AllChildrenReleased = factory != null && factory.Leases.Count == 2 && factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased),
                Error = failure?.ToString() };
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return 0;
    }

    public static SceneWorldData CreateScene()
    {
        var transform = new ComponentId(Id(1)); var appearance = new ComponentId(Id(2));
        var velocity = new ComponentId(Id(3)); var interaction = new ComponentId(Id(4));
        var basic = new EntityDefinitionId(Id(10)); var red = new EntityDefinitionId(Id(11)); var green = new EntityDefinitionId(Id(12));
        var rootDefinition = new EntityDefinitionId(Id(13)); var empty = new EntityDefinitionId(Id(14)); var root = new EntityId(Id(100));
        var definitions = new EntityDefinition[]
        {
            new(basic, new([new ComponentData(transform, BuiltInComponents.TransformId, 1), new ComponentData(appearance, BuiltInComponents.AppearanceId, 1)])),
            new(red, new(addedComponents: [new ComponentData(velocity, PrimitiveEcsComponents.VelocityId, 1,
                    new Dictionary<string, PropertyValue> { ["x"] = PropertyValue.From(60d) }), new ComponentData(interaction, BuiltInComponents.InteractionId, 1,
                    new Dictionary<string, PropertyValue> { ["enabled"] = PropertyValue.From(false) })],
                propertyOverrides: [new(transform, "x", PropertyValue.From(-.75d)), new(transform, "y", PropertyValue.From(.5d)),
                    new(appearance, "g", PropertyValue.From(0d)), new(appearance, "b", PropertyValue.From(0d))]), basic),
            new(green, new(propertyOverrides: [new(transform, "x", PropertyValue.From(1.25d)), new(transform, "y", PropertyValue.From(.5d)),
                new(transform, "z", PropertyValue.From(-.5d)), new(appearance, "r", PropertyValue.From(0d)), new(appearance, "b", PropertyValue.From(0d))]), basic),
            new(rootDefinition, new([new ComponentData(transform, BuiltInComponents.TransformId, 1,
                new Dictionary<string, PropertyValue> { ["sy"] = PropertyValue.From(2d) })])),
            new(empty, EntityPatch.Empty)
        };
        var entities = new List<EntityInstance>
        {
            new(root, rootDefinition, null, null, "Root", 0),
            new(new(Id(101)), red, null, root, "Moving red", 1),
            new(new(Id(102)), green, null, root, "Stationary green", 2)
        };
        for (var i = 0; i < 130; i++) entities.Add(new(new(Id(1000 + i)), empty, null, root, "Identity group " + i, 3 + i));
        return new(new SceneDocument(new(Id(99)), 31, entities, definitions, [.. BuiltInComponents.Schemas, PrimitiveEcsComponents.VelocitySchema]));
    }

    public static uint ExpectedColor(int tick)
    {
        // Independent literal world matrices: parent scales Y by2; local Y=.5 becomes1.
        // Only the red entity moves, in parent coordinates, at60units/sec using the documented default step.
        var redX = -.75f + (float)(60d * TimeSpan.FromSeconds(1d / 60).TotalSeconds * tick);
        double[][] expected =
        [
            [1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, redX, 1, 0, 1, 1, 0, 0],
            [1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 1.25, 1, -.5, 1, 0, 1, 0]
        ];
        ulong hash = 14695981039346656037UL;
        foreach (var draw in expected) foreach (var value in draw)
        { var quantized = checked((long)Math.Round(value * 4096, MidpointRounding.AwayFromZero)); hash = unchecked((hash ^ (ulong)quantized) * 1099511628211UL); }
        hash = unchecked((hash ^ 2u) * 1099511628211UL);
        return 0xff000000u | (uint)(hash & 0xffffff);
    }

    public static NativeRuntimeLaunch RuntimeCommand(string path)
    {
        path = Path.GetFullPath(path);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return new(path, Path.ChangeExtension(path, ".dll"));
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select a runtime DLL or apphost.", nameof(path));
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Parent executable path missing.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Launch this proof parent through dotnet when selecting a runtime DLL.");
        return new(executable, path, [path]);
    }
    private static async Task<(ulong FrameId, ulong Tick, uint Pixel)> ObserveFrame(FrameSession frames, Task start, ulong tick, uint color, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested(); if (start.IsFaulted || start.IsCanceled) await start;
            if (frames.Display is { } display && display.Receiver.FirstFrame.IsCompletedSuccessfully)
            {
                using var frame = display.Receiver.TakeLatest();
                if (frame != null && frame.Metadata.Tick == tick)
                {
                    const int x = 20, y = 12;
                    var pixel = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(y * display.Surface.Stride + x * 4)..]);
                    Check(pixel == FramePattern.Pixel(x, y, 48, 32, frame.Metadata.FrameId, tick, color),
                        "Extracted primitive matrices/materials do not match the independent source checksum oracle.");
                    frames.Presented(display, frame.Metadata); return (frame.Metadata.FrameId, tick, pixel);
                }
            }
            await Task.Delay(10, token);
        }
    }
    private static Guid Id(int value) => new($"8a90b433-8750-4e59-8225-{value:000000000000}");
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Game.Logic");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
