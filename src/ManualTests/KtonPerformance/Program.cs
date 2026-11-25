using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Kton;
using TrueMoon.Aether.Kton.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bepu.Hosting;

namespace TrueMoon.ManualTests.KtonPerformance;

internal static class Program
{
    private const int Warmup = 120;
    private const int Samples = 600;
    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || args[1] is not ("smoke" or "baseline" or "attribution")) throw new ArgumentException("KtonPerformanceProbe <new-report.json> <smoke|baseline|attribution>");
        var output = Path.GetFullPath(args[0]);
        if (File.Exists(output)) throw new IOException("Choose a fresh report; existing evidence is immutable.");
        var results = new List<object>();
        var report = new Dictionary<string, object?>
        {
            ["Completed"] = false, ["Stage"] = "PHYS8.3", ["Protocol"] = "PHYS8.1 workloads; PHYS8.2 allocation markers", ["Utc"] = DateTimeOffset.UtcNow, ["Mode"] = args[1],
            ["Environment"] = new { OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
                VectorFloatLanes = Vector<float>.Count, Vector.IsHardwareAccelerated, Avx2 = Avx2.IsSupported, Avx512F = Avx512F.IsSupported,
                GCSettings.IsServerGC, Stopwatch.Frequency, Stopwatch.IsHighResolution },
            ["WarmupTicks"] = Warmup, ["MeasuredTicks"] = Samples, ["Results"] = results,
            ["Measurement"] = "Macrobaseline of synchronous ticks; nearest-rank percentiles. Inner timer measures public IPhysicsWorld.Step including validation/events/join. Full timer measures PrimitiveGameSession.Tick. Factory/scene setup, input application, state validation, sorting, serialization and teardown are outside sample windows. No induced GC; process-wide allocations include runtime/other-thread activity. Primary-thread and all-thread allocations are reported separately. No numeric performance gate.",
            ["Scope"] = "Windows x64; Baseline60;1worker;100/1000separated active and sleeping boxes,1/32/256walking capsules on one static floor; Cobalt/Microsoft DI; standalone and Aether. No10000bodies,2/4/8workers,ragdolls,other OS,GPU or release readiness."
        };
        try
        {
            if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("This baseline is explicitly Windows x64.");
            var workloads = args[1] == "smoke"
                ? new[] { new Workload("active-bodies", 100), new Workload("sleeping-bodies", 100), new Workload("walking-characters", 1) }
                : args[1] == "attribution" ? new[] { new Workload("active-bodies", 1000), new Workload("sleeping-bodies", 1000), new Workload("walking-characters", 256) }
                : new[] { new Workload("active-bodies", 100), new Workload("active-bodies", 1000), new Workload("sleeping-bodies", 100), new Workload("sleeping-bodies", 1000),
                    new Workload("walking-characters", 1), new Workload("walking-characters", 32), new Workload("walking-characters", 256) };
            foreach (var microsoft in new[] { false, true })
                foreach (var ecs in new[] { false, true })
                    foreach (var workload in workloads)
                        results.Add(await Run(workload, microsoft, ecs, args[1] == "attribution"));
            report["Completed"] = true;
            return 0;
        }
        catch (Exception error) { report["Failure"] = error.ToString(); Console.Error.WriteLine(error); return 1; }
        finally
        {
            report["Assemblies"] = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name is { } name &&
                (name.StartsWith("TrueMoon", StringComparison.Ordinal) || name is "BepuPhysics" or "BepuUtilities"))
                .Select(a => new { Name = a.GetName().Name, Version = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    private static async Task<object> Run(Workload workload, bool microsoft, bool ecs, bool attribution)
    {
        var allocationCounters = new AllocationCounters();
        var contactCapacity = workload.IsCharacter ? Math.Max(64, workload.Count) : 64;
        await using var app = App.Builder(context => { if (microsoft) context.UseDI(); }).Setup(context =>
        {
            context.UseBepuPhysics(options => options.DefaultWorld = workload.Options);
            if (ecs)
            {
                context.UseAetherEcs().UseAetherPrimitiveEcs().UseAetherKton(options => options.MaximumContactEvents = contactCapacity);
                context.UseAetherSceneLoading(_ => PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new())).Build());
                context.UseAetherSceneSubsystems(builder =>
                {
                    builder.AddKtonPhysics();
                    if (attribution) builder.Add<AllocationMarkerSubsystem>("probe.allocations");
                });
            }
            context.Services(services =>
            {
                services.RemoveAll<IFactory<IPhysicsWorld>>();
                services.Singleton<MeasuredWorldFactory>(resolver => new(resolver.Resolve<IFactory<IPhysicsWorld, PhysicsWorldOptions>>()!) { Arguments = workload.Options });
                services.Singleton<IFactory<IPhysicsWorld>>(resolver => resolver.Resolve<MeasuredWorldFactory>()!);
                if (ecs && attribution) services.Singleton<IFactory<AllocationMarkerSubsystem, SceneSubsystemArguments>>(_ =>
                    new Factory<AllocationMarkerSubsystem, SceneSubsystemArguments>(_ => new(allocationCounters)));
                if (ecs) services.Singleton<IFactory<PrimitiveGameSession>>(resolver =>
                {
                    var loader = resolver.Resolve<SceneWorldLoader>()!;
                    var simulations = resolver.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
                    return new Factory<PrimitiveGameSession>(() => new(loader, simulations));
                });
            });
        }).Build();
        await app.StartAsync();
        var host = app.Services.Resolve<BepuPhysicsHost>()!;
        var factory = app.Services.Resolve<MeasuredWorldFactory>()!;
        PrimitiveGameSession? game = null; MeasuredWorld? world = null;
        var label = (microsoft ? "Microsoft" : "Cobalt") + "/" + (ecs ? "ecs" : "standalone") + "/" + workload.Kind + "/" + workload.Count;
        Console.WriteLine("Starting " + label);
        try
        {
            Action tick;
            if (ecs)
            {
                game = app.Services.Resolve<IFactory<PrimitiveGameSession>>()!.Create()!;
                game.LoadSceneWorld(new(workload.Document())); world = factory.Created!;
                if (workload.IsCharacter) game.ActiveScene!.World.Query<PhysicsCharacterInput>().ForEachWrite((EntityHandle entity, ref PhysicsCharacterInput input) =>
                    input = new(new(2, 0)));
                game.SetPlaying(true); tick = () => game.Tick();
            }
            else { world = (MeasuredWorld)factory.Create(); workload.Populate(world); tick = () => world.Step(world.LastCompletedTick + 1); }
            workload.Sleep(world);
            for (var index = 0; index < Warmup; index++) tick();
            var beforeState = workload.Validate(world);
            var physics = new long[Samples]; var total = new long[Samples];
            world.StepTicks = physics;
            world.MeasureAllocations = attribution; allocationCounters.Enabled = attribution;
            var heapBefore = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0); var gen1 = GC.CollectionCount(1); var gen2 = GC.CollectionCount(2);
            var primaryBefore = GC.GetAllocatedBytesForCurrentThread(); var allBefore = GC.GetTotalAllocatedBytes(true);
            for (var index = 0; index < Samples; index++)
            {
                var start = Stopwatch.GetTimestamp(); tick(); total[index] = Stopwatch.GetTimestamp() - start;
            }
            var allBytes = GC.GetTotalAllocatedBytes(true) - allBefore; var primaryBytes = GC.GetAllocatedBytesForCurrentThread() - primaryBefore;
            var collections = new[] { GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2 };
            var heapAfter = GC.GetTotalMemory(false);
            world.StepTicks = null;
            allocationCounters.Enabled = false;
            Check(world.Samples == Samples && world.LastCompletedTick == Warmup + Samples, "Tick/sample accounting mismatch.");
            if (ecs && attribution) Check(allocationCounters.Frames == Samples, "Allocation marker accounting mismatch.");
            var afterState = workload.Validate(world);
            var profile = PhysicsProfileSettings.For(world.Options.Quality);
            var events = world.PublishedEvents;
            if (workload.IsCharacter) Check(events >= (long)Samples * workload.Count, "Walking workload did not publish support contacts.");
            else Check(events == 0, "Separated rigid-body workload unexpectedly made contact.");
            var peak = host.Resources.MaximumSampledPoolBytes;
            var teardownStart = Stopwatch.GetTimestamp();
            if (game != null) { game.SetPlaying(false); game.Dispose(); game = null; }
            else world.Dispose();
            var teardown = Stopwatch.GetElapsedTime(teardownStart).TotalMilliseconds;
            await app.StopAsync();
            var resources = host.Resources;
            Check(host.IsReleased && resources.PublishedWorlds == 0 && resources.ActiveResourceOwners == 0 && resources.MaximumPoolBytesAfterCleanup == 0, "Teardown leaked an owner/pool.");
            var innerStats = Summarize(physics); var totalStats = Summarize(total);
            Console.WriteLine($"Passed {label}: Step p99={innerStats.P99Milliseconds:F4}ms; full p99={totalStats.P99Milliseconds:F4}ms; allocated={allBytes}B");
            return new { Provider = microsoft ? "Microsoft" : "Cobalt", Path = ecs ? "ecs-physics-ecs" : "standalone", workload.Kind, workload.Count,
                Profile = new { Quality = world.Options.Quality.ToString(), profile.FixedDeltaSeconds, profile.SolverIterations, profile.SolverSubsteps, profile.CcdMode, profile.Workers,
                    world.Options.StepDiagnosticBudgetMilliseconds, world.Options.Limits, BridgeContactProjectionCapacity = ecs ? contactCapacity : 0 },
                StateBefore = beforeState, StateAfter = afterState, PhysicsStep = innerStats, FullTick = totalStats,
                DiagnosticStepP99BudgetExceeded = innerStats.P99Milliseconds > world.Options.StepDiagnosticBudgetMilliseconds,
                PrimaryThreadAllocatedBytes = primaryBytes, AllThreadsAllocatedBytes = allBytes, PrimaryThreadBytesPerTick = primaryBytes / (double)Samples,
                AllThreadsBytesPerTick = allBytes / (double)Samples,
                AllocationBreakdown = attribution ? allocationCounters.Snapshot(primaryBytes, world.StepAllocatedBytes, Samples, ecs) : null,
                GcCollections = collections, ManagedHeapBeforeBytes = heapBefore, ManagedHeapAfterBytes = heapAfter,
                PoolHighWaterBytes = peak, PoolCounterScope = "Factory maximum sampled BEPU main and dispatcher pool bytes, one world; not total process/native RSS or a hard RAM cap.",
                PublishedContactEvents = events, CallbackScope = "Gameplay event count, not raw narrowphase callback invocation count; raw callbacks are not instrumented.",
                SceneAndWorldTeardownMilliseconds = teardown, ResourcesAfterStop = resources };
        }
        finally { if (game != null) game.Dispose(); else if (world is { State: not PhysicsWorldState.Disposed }) world.Dispose(); }
    }
    private sealed record Distribution(double P50Milliseconds, double P95Milliseconds, double P99Milliseconds, double MaximumMilliseconds, double MeanMilliseconds);
    private static Distribution Summarize(long[] values)
    {
        Array.Sort(values);
        double Rank(double percentile) => values[(int)Math.Ceiling(values.Length * percentile) - 1] * 1000d / Stopwatch.Frequency;
        return new(Rank(.5), Rank(.95), Rank(.99), values[^1] * 1000d / Stopwatch.Frequency, values.Average(value => (double)value) * 1000d / Stopwatch.Frequency);
    }
}
